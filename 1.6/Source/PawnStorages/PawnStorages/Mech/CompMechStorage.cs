using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Mech;

public class CompMechStorage : CompPawnStorage
{
    // Separate tick tracking for mech energy charging (base class dict is private)
    private Dictionary<int, int> mechStoringTick = new();

    public new CompProperties_MechStorage Props => props as CompProperties_MechStorage;

    public override bool CanAssign(Pawn pawn, bool couldMakePrisoner) =>
        ModsConfig.BiotechActive
        && pawn.IsColonyMech
        && (compAssignable == null || compAssignable.AssignedPawns.Contains(pawn) || compAssignable.HasFreeSlot);

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

        var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
        if (energy == null)
            return;

        energy.CurLevel = Mathf.Min(energy.MaxLevel, energy.CurLevel + Props.mechChargeRate * ticksStored);
    }

    public override void CompTick()
    {
        base.CompTick();

        if (!ModsConfig.BiotechActive)
            return;
        if (!schedulingEnabled || compAssignable == null)
            return;
        if (!parent.IsHashIntervalTick(Props.mechCheckWorkInterval))
            return;

        foreach (Pawn pawn in compAssignable.AssignedPawns.ToList())
        {
            if (!pawn.IsColonyMech || pawn.Spawned || !innerContainer.Contains(pawn))
                continue;

            var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
            if (energy == null || energy.CurLevelPercentage < Props.mechMinExitThreshold)
                continue;

            if (HasWorkAvailable())
                ReleasePawn(pawn, parent.Position, parent.Map);
        }
    }

    // Cheap broad check: are there things that might need doing on this map?
    private bool HasWorkAvailable()
    {
        Map map = parent.Map;
        if (map == null) return false;

        if (map.listerHaulables.ThingsPotentiallyNeedingHauling().Count > 0) return true;
        if (map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Mine)) return true;
        if (map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Deconstruct)) return true;
        if (map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer).Any()) return true;
        return false;
    }

    public override string PawnTypeLabel => "PS_StoredMechs".Translate();

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new(base.CompInspectStringExtra());

        if (!ModsConfig.BiotechActive)
            return sb.ToString().TrimStart().TrimEnd();

        foreach (Pawn pawn in innerContainer)
        {
            if (!pawn.IsColonyMech) continue;
            var energy = pawn.needs?.TryGetNeed<Need_MechEnergy>();
            if (energy == null) continue;
            sb.AppendLine("PS_MechCharging".Translate(pawn.LabelShort, energy.CurLevelPercentage.ToStringPercent()));
        }

        var powerTrader = parent.TryGetComp<CompPowerTrader>();
        if (powerTrader?.PowerOn == false && innerContainer.Any<Pawn>())
            sb.AppendLine("PS_MechNoPower".Translate());

        return sb.ToString().TrimStart().TrimEnd();
    }
}
