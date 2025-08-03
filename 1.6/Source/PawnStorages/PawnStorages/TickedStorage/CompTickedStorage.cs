using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace PawnStorages.TickedStorage;

public class CompTickedStorage : CompPawnStorage
{
    public PSBuilding ParentAsPSBuilding => parent as PSBuilding;
    public List<Pawn> StoredPawns => GetDirectlyHeldThings().Select(p => p as Pawn).ToList();
    public new CompProperties_TickedStorage Props => props as CompProperties_TickedStorage;

    public override float NutritionRequiredPerDay() =>
        compAssignable.AssignedPawns.Sum(animal => SimplifiedPastureNutritionSimulator.NutritionConsumedPerDay(animal.def, animal.ageTracker.CurLifeStage));

    public IEnumerable<PawnKindDef> HeldPawnTypes => innerContainer.innerList.Select(p => p.kindDef).Distinct();

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new();
        sb.AppendLine($"Stored: {innerContainer.Count}/{MaxStoredPawns()}");
        if (innerContainer?.Any<Pawn>() != true)
            return sb.ToString().TrimStart().TrimEnd();
        sb.AppendLine("PS_StoredPawns".Translate());
        foreach (Pawn pawn in innerContainer)
        {
            sb.AppendLine(pawn.needs.food.Starving ? $"    - {pawn.LabelCap} ({pawn.gender.GetLabel()}) [Starving!]" : $"    - {pawn.LabelCap} ({pawn.gender.GetLabel()})");
        }

        return sb.ToString().TrimStart().TrimEnd();
    }

    public void EmulateScaledPawnAgeTick(Pawn pawn)
    {
        int interval = Props.tickInterval;

        int ageBioYears = pawn.ageTracker.AgeBiologicalYears;

        if (pawn.ageTracker.lifeStageChange)
            pawn.ageTracker.PostResolveLifeStageChange();

        pawn.ageTracker.TickBiologicalAge(interval);

        if (pawn.ageTracker.lockedLifeStageIndex > -0)
            return;

        if (Find.TickManager.TicksGame >= pawn.ageTracker.nextGrowthCheckTick)
            pawn.ageTracker.CalculateGrowth(interval);

        if (ageBioYears < pawn.ageTracker.AgeBiologicalYears)
            pawn.ageTracker.BirthdayBiological(pawn.ageTracker.AgeBiologicalYears);
    }

    public void EmulateScaledPawnNutritionTick(Pawn pawn)
    {
        int interval = Props.tickInterval;

        Need_Food foodNeeds = pawn.needs?.food;
        if (foodNeeds == null)
            return;

        // Need_Food.NeedInterval hardcodes 150 ticks, so adjust
        float adjustedMalnutritionSeverityPerInterval = foodNeeds.MalnutritionSeverityPerInterval / 150f * interval;

        if (foodNeeds.Starving)
            HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, adjustedMalnutritionSeverityPerInterval);
        else
            HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, -adjustedMalnutritionSeverityPerInterval);
    }

    public void TickHediffs(Pawn pawn)
    {
        Pawn_HealthTracker health = pawn.health;

        if (health.Dead)
            return;
        Pawn_HealthTracker.tmpRemovedHediffs.Clear();
        Pawn_HealthTracker.tmpHediffs.Clear();
        Pawn_HealthTracker.tmpHediffs.AddRange((IEnumerable<Hediff>)health.hediffSet.hediffs);
        foreach (Hediff tmpHediff in Pawn_HealthTracker.tmpHediffs.Where(tmpHediff => !Pawn_HealthTracker.tmpRemovedHediffs.Contains(tmpHediff)))
        {
            try
            {
                tmpHediff.Tick();
                tmpHediff.PostTick();
            }
            catch (Exception ex1)
            {
                Log.Error("PS_HediffTickException".Translate(tmpHediff.ToStringSafe(), pawn.ToStringSafe(), ex1.ToStringSafe()));
                try
                {
                    health.RemoveHediff(tmpHediff);
                }
                catch (Exception ex2)
                {
                    Log.Error("PS_HediffRemovalException".Translate(ex2.ToStringSafe()));
                }
            }
            if (health.Dead)
                return;
        }
    }

    public override void CompTick()
    {
        base.CompTick();

        // Looks like we'd need to emulate each hediff individually to appropriately emulate this,
        // So take the hit and just do per-tick
        if (Props.tickHediffs)
        {
            foreach (Pawn pawn in StoredPawns)
            {
                TickHediffs(pawn);
            }
        }

        if (!parent.IsHashIntervalTick(Props.tickInterval))
        {
            return;
        }

        foreach (Pawn pawn in StoredPawns)
        {
            if (Props.tickAge)
            {
                EmulateScaledPawnAgeTick(pawn);
            }

            if (Props.tickNutrition)
            {
                EmulateScaledPawnNutritionTick(pawn);
            }
        }
    }
}
