using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Mech;

public class CompMechStorage : CompPawnStorage
{
    private Dictionary<int, int> mechStoringTick = new();

    public new CompProperties_MechStorage Props => props as CompProperties_MechStorage;

    public override bool CanAssign(Pawn pawn, bool couldMakePrisoner) =>
        ModsConfig.BiotechActive && pawn.IsColonyMech && (compAssignable == null || compAssignable.AssignedPawns.Contains(pawn) || compAssignable.HasFreeSlot);

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref mechStoringTick, "mechStoringTick", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            mechStoringTick ??= new Dictionary<int, int>();
    }

    public override void StorePawn(Pawn pawn, bool effects = true)
    {
        base.StorePawn(pawn, effects);
        if (ModsConfig.BiotechActive && pawn.IsColonyMech)
            mechStoringTick.SetOrAdd(pawn.thingIDNumber, Find.TickManager.TicksGame);
    }

    /// <summary>
    /// Returns the energy level this mech would have if released right now,
    /// accounting for charge accumulated while stored. Cheap math, no per-tick cost.
    /// </summary>
    public float GetProjectedEnergyLevel(Pawn pawn)
    {
        var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
        if (energy == null)
            return 0f;

        if (!mechStoringTick.TryGetValue(pawn.thingIDNumber, out int storedAtTick))
            return energy.CurLevel;

        var powerTrader = parent.TryGetComp<CompPowerTrader>();
        if (powerTrader?.PowerOn != true)
            return energy.CurLevel;

        int ticksStored = Find.TickManager.TicksGame - storedAtTick;
        return Mathf.Min(energy.MaxLevel, energy.CurLevel + PawnStoragesMod.settings.MechChargeRate * ticksStored);
    }

    /// <summary>
    /// Handles power state changes for stored mechs:
    /// - PowerTurnedOff: apply accumulated charge immediately and reset baseline,
    ///   so unpowered time doesn't count as charging.
    /// - PowerTurnedOn: reset baseline to now, so the unpowered gap between
    ///   power-off and power-on isn't counted as charging time.
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
                    var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
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

        var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
        float energyCostPerHP = pawn.GetStatValue(StatDefOf.MechEnergyLossPerHP);
        float ticksRemaining = ticksStored;
        int partTicks = PawnStoragesMod.settings.MechRepairPartTicks;

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

        var powerTrader = parent.TryGetComp<CompPowerTrader>();
        if (powerTrader?.PowerOn != true)
            return;

        int ticksStored = Find.TickManager.TicksGame - storedAtTick;
        if (ticksStored <= 0)
            return;

        // Apply charge first (repair costs energy, so charge must come first)
        var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
        if (energy != null)
            energy.CurLevel = Mathf.Min(energy.MaxLevel, energy.CurLevel + PawnStoragesMod.settings.MechChargeRate * ticksStored);

        // Apply deferred repair
        ApplyRepair(pawn, ticksStored);
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

        var tracker = parent.Map?.GetComponent<MechWorkTracker>();
        if (tracker == null)
            return;

        foreach (Pawn pawn in compAssignable.AssignedPawns.ToList())
        {
            if (!pawn.IsColonyMech || pawn.Spawned || !innerContainer.Contains(pawn))
                continue;

            var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
            if (energy == null)
                continue;

            float projectedPct = GetProjectedEnergyLevel(pawn) / energy.MaxLevel;
            if (projectedPct < Props.mechMinExitThreshold)
                continue;

            bool hasWork = tracker.HasWorkForMech(pawn);
            if (PawnStoragesMod.settings.DebugLogging)
                Log.Message($"[CompMechStorage] {pawn.LabelShort}: projectedPct={projectedPct:P1}, hasWork={hasWork}, releasing={hasWork}");
            if (hasWork)
                ReleasePawn(pawn, parent.Position, parent.Map);
        }
    }

    public override string PawnTypeLabel => "PS_StoredMechs".Translate();

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new(base.CompInspectStringExtra());

        if (!ModsConfig.BiotechActive)
            return sb.ToString().TrimStart().TrimEnd();

        var powerTrader = parent.TryGetComp<CompPowerTrader>();
        bool powered = powerTrader?.PowerOn == true;

        foreach (Pawn pawn in innerContainer)
        {
            if (!pawn.IsColonyMech)
                continue;
            var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
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
