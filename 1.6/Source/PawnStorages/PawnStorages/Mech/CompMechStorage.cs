using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PawnStorages.Mech;

public class CompMechStorage : CompPawnStorage
{
    private Dictionary<int, int> mechStoringTick = new();

    /// <summary>Per-building minimum energy percentage before a mech will exit for work.</summary>
    public float mechMinExitThreshold = 0.5f;

    public new CompProperties_MechStorage Props => props as CompProperties_MechStorage;

    public override bool CanAssign(Pawn pawn, bool couldMakePrisoner) =>
        ModsConfig.BiotechActive && pawn.IsColonyMech && (compAssignable == null || compAssignable.AssignedPawns.Contains(pawn) || compAssignable.HasFreeSlot);

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref mechStoringTick, "mechStoringTick", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref mechMinExitThreshold, "mechMinExitThreshold", 0.5f);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            mechStoringTick ??= new Dictionary<int, int>();
    }

    /// <summary>True if there is an active hostile threat on the map (same check as the speed widget).</summary>
    public static bool IsMapInCombat(Map map) => map != null && GenHostility.AnyHostileActiveThreatToPlayer(map);

    /// <summary>True if this mech is a combat type (fighters, default for most mechs).</summary>
    public static bool IsCombatMech(Pawn pawn) => pawn.kindDef?.isFighter == true;

    public override void StorePawn(Pawn pawn, bool effects = true)
    {
        base.StorePawn(pawn, effects);
        if (ModsConfig.BiotechActive && pawn.IsColonyMech)
            mechStoringTick.SetOrAdd(pawn.thingIDNumber, Find.TickManager.TicksGame);
    }

    /// <summary>
    /// Returns the energy level this mech would have if released right now
    /// </summary>
    public float GetProjectedEnergyLevel(Pawn pawn)
    {
        Need_MechEnergy energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
        if (energy == null)
            return 0f;

        if (!mechStoringTick.TryGetValue(pawn.thingIDNumber, out int storedAtTick))
            return energy.CurLevel;

        CompPowerTrader powerTrader = parent.TryGetComp<CompPowerTrader>();
        if (powerTrader?.PowerOn != true)
            return energy.CurLevel;

        int ticksStored = Find.TickManager.TicksGame - storedAtTick;
        return Mathf.Min(energy.MaxLevel, energy.CurLevel + PawnStoragesMod.settings.MechChargeRate * ticksStored);
    }

    /// <summary>
    /// Handles power state changes for stored mechs:
    /// - PowerTurnedOff: apply accumulated charge immediately and reset baseline, so unpowered time doesn't count as charging.
    /// - PowerTurnedOn: reset baseline to now, so the unpowered gap betweennpower-off and power-on isn't counted as charging time.
    /// </summary>
    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);

        if (signal != "PowerTurnedOff" && signal != "PowerTurnedOn")
            return;
        if (!ModsConfig.BiotechActive)
            return;

        int now = Find.TickManager.TicksGame;

        foreach (Pawn pawn in innerContainer)
        {
            if (!pawn.IsColonyMech)
                continue;
            if (!mechStoringTick.TryGetValue(pawn.thingIDNumber, out int storedAtTick))
                continue;

            if (signal == "PowerTurnedOff")
            {
                int ticksCharged = now - storedAtTick;
                if (ticksCharged > 0)
                {
                    // Apply charge first (repair costs energy)
                    Need_MechEnergy energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
                    if (energy != null)
                        energy.CurLevel = Mathf.Min(energy.MaxLevel, energy.CurLevel + PawnStoragesMod.settings.MechChargeRate * ticksCharged);

                    // Apply repair for powered duration
                    ApplyRepair(pawn, ticksCharged);
                }
            }

            // Both cases: reset baseline to now
            mechStoringTick[pawn.thingIDNumber] = now;
        }
    }

    /// <summary>
    /// Apply repairs for the given number of powered ticks using a single time budget.
    /// Injuries heal at MechRepairRate HP/tick, consuming ticks proportionally.
    /// Missing parts and weapons each take MechRepairPartTicks to restore.
    /// Deducts MechEnergyLossPerHP from energy per HP healed (vanilla balance).
    /// </summary>
    private void ApplyRepair(Pawn pawn, int ticksStored)
    {
        if (ticksStored <= 0)
            return;
        float repairRate = PawnStoragesMod.settings.MechRepairRate;
        if (repairRate <= 0f)
            return;
        if (pawn.TryGetComp<CompMechRepairable>() == null)
            return;

        Need_MechEnergy energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
        float energyCostPerHP = pawn.GetStatValue(StatDefOf.MechEnergyLossPerHP);
        float ticksRemaining = ticksStored;
        int partTicks = Mathf.Max(1, PawnStoragesMod.settings.MechRepairPartTicks);

        while (ticksRemaining > 0f && MechRepairUtility.CanRepair(pawn))
        {
            Hediff hediff = MechRepairUtility.GetHediffToHeal(pawn);
            if (hediff is Hediff_Injury injury)
            {
                // Max HP we can heal with remaining time
                float maxHP = ticksRemaining * repairRate;
                float healAmount = Mathf.Min(injury.Severity, maxHP);
                float energyCost = healAmount * energyCostPerHP;
                if (energy != null && energy.CurLevel < energyCost)
                    break;

                injury.Heal(healAmount);
                ticksRemaining -= healAmount / repairRate;
                if (energy != null)
                    energy.CurLevel -= energyCost;
            }
            else if (hediff is Hediff_MissingPart)
            {
                if (ticksRemaining < partTicks)
                    break;
                pawn.health.RemoveHediff(hediff);
                ticksRemaining -= partTicks;
            }
            else if (MechRepairUtility.IsMissingWeapon(pawn))
            {
                if (ticksRemaining < partTicks)
                    break;
                MechRepairUtility.GenerateWeapon(pawn);
                ticksRemaining -= partTicks;
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Apply charge and repair for the stored duration. Called on pawn release.
    /// </summary>
    public override void ApplyNeedsForStoredPeriodFor(Pawn pawn)
    {
        base.ApplyNeedsForStoredPeriodFor(pawn);

        if (!ModsConfig.BiotechActive || !pawn.IsColonyMech)
            return;
        if (!mechStoringTick.TryGetValue(pawn.thingIDNumber, out int storedAtTick))
            return;
        mechStoringTick.Remove(pawn.thingIDNumber);

        CompPowerTrader powerTrader = parent.TryGetComp<CompPowerTrader>();
        if (powerTrader?.PowerOn != true)
            return;

        int ticksStored = Find.TickManager.TicksGame - storedAtTick;
        if (ticksStored <= 0)
            return;

        // Apply charge first (repair costs energy, so charge must come first)
        Need_MechEnergy energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
        if (energy != null)
            energy.CurLevel = Mathf.Min(energy.MaxLevel, energy.CurLevel + PawnStoragesMod.settings.MechChargeRate * ticksStored);

        // Apply deferred repair
        ApplyRepair(pawn, ticksStored);
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        // Only colony mechs can use mech storage
        if (!ModsConfig.BiotechActive || !selPawn.IsColonyMech)
            yield break;

        if (!CanStore)
        {
            yield return new FloatMenuOption("PS_CannotStore".Translate(selPawn.LabelShort), null);
            yield break;
        }

        // Check assignment: if there's an assignable comp, ensure there's a free slot or the mech is already assigned
        if (compAssignable != null && !compAssignable.AssignedPawns.Contains(selPawn) && !compAssignable.HasFreeSlot)
        {
            yield return new FloatMenuOption("PS_CannotStore".Translate(selPawn.LabelShort), null);
            yield break;
        }

        yield return new FloatMenuOption(
            "PS_Enter".Translate(),
            delegate
            {
                // Pre-assign the mech so it has a reserved slot
                if (compAssignable != null && !compAssignable.AssignedPawns.Contains(selPawn))
                    compAssignable.TryAssignPawn(selPawn);

                Job job = EnterJob(selPawn);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        );
    }

    public override IEnumerable<FloatMenuOption> CompMultiSelectFloatMenuOptions(IEnumerable<Pawn> selPawns)
    {
        if (!ModsConfig.BiotechActive)
            yield break;

        List<Pawn> mechs = selPawns.Where(p => p.IsColonyMech).ToList();
        if (mechs.Count == 0)
            yield break;

        int freeSlots = MaxStoredPawns() - innerContainer.Count;
        if (freeSlots <= 0)
        {
            yield return new FloatMenuOption("PS_CannotStore".Translate(parent.LabelShort), null);
            yield break;
        }

        yield return new FloatMenuOption(
            "PS_Enter".Translate(),
            delegate
            {
                int slots = MaxStoredPawns() - innerContainer.Count;
                for (int i = 0; i < slots && i < mechs.Count; i++)
                {
                    Pawn mech = mechs[i];
                    if (compAssignable != null && !compAssignable.AssignedPawns.Contains(mech))
                        compAssignable.TryAssignPawn(mech);

                    Job job = EnterJob(mech);
                    mech.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            }
        );
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            yield return gizmo;

        if (ModsConfig.BiotechActive && schedulingEnabled)
            yield return new Gizmo_MechExitThreshold(this);
    }

    public override void CompTick()
    {
        base.CompTick();

        if (!ModsConfig.BiotechActive)
            return;
        if (!schedulingEnabled || compAssignable == null)
            return;
        if (!parent.IsHashIntervalTick(Mathf.Max(100, PawnStoragesMod.settings.MechCheckWorkInterval)))
            return;

        Map map = parent.Map;
        MechWorkTracker tracker = map?.GetComponent<MechWorkTracker>();
        if (tracker == null)
            return;

        bool inCombat = IsMapInCombat(map);

        foreach (Pawn pawn in compAssignable.AssignedPawns.ToList())
        {
            if (!pawn.IsColonyMech || pawn.Spawned || !innerContainer.Contains(pawn))
                continue;

            // During combat, release combat mechs immediately regardless of charge
            if (inCombat && IsCombatMech(pawn))
            {
                if (PawnStoragesMod.settings.DebugLogging)
                    Log.Message($"[CompMechStorage] {pawn.LabelShort}: combat detected, releasing combat mech");
                ReleasePawn(pawn, parent.Position, map);
                continue;
            }

            Need_MechEnergy energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
            if (energy == null)
                continue;

            float projectedPct = GetProjectedEnergyLevel(pawn) / energy.MaxLevel;
            if (projectedPct < mechMinExitThreshold)
                continue;

            bool hasWork = tracker.HasWorkForMech(pawn);
            if (PawnStoragesMod.settings.DebugLogging)
                Log.Message($"[CompMechStorage] {pawn.LabelShort}: projectedPct={projectedPct:P1}, hasWork={hasWork}, releasing={hasWork}");
            if (hasWork)
                ReleasePawn(pawn, parent.Position, map);
        }
    }

    public override string PawnTypeLabel => "PS_StoredMechs".Translate();

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new(base.CompInspectStringExtra());

        if (!ModsConfig.BiotechActive)
            return sb.ToString().TrimStart().TrimEnd();

        CompPowerTrader powerTrader = parent.TryGetComp<CompPowerTrader>();
        bool powered = powerTrader?.PowerOn == true;

        foreach (Pawn pawn in innerContainer)
        {
            if (!pawn.IsColonyMech)
                continue;
            Need_MechEnergy energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
            if (energy == null)
                continue;
            float projectedPct = GetProjectedEnergyLevel(pawn) / energy.MaxLevel;
            sb.AppendLine("PS_MechCharging".Translate(pawn.LabelShort, projectedPct.ToStringPercent()));

            if (MechRepairUtility.CanRepair(pawn))
            {
                if (powered)
                    sb.AppendLine("PS_MechRepairing".Translate(pawn.LabelShort));
                else
                    sb.AppendLine("PS_MechDamaged".Translate(pawn.LabelShort));
            }
        }

        if (!powered && innerContainer.Any<Pawn>())
            sb.AppendLine("PS_MechNoPower".Translate());

        return sb.ToString().TrimStart().TrimEnd();
    }
}
