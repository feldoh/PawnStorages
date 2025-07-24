using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using PawnStorages.Farm.Comps;
using PawnStorages.Farm.Interfaces;
using PawnStorages.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

[StaticConstructorOnStartup]
public class CompPawnStorageNutrition : ThingComp
{
    public INutritionStorageParent ParentAsNutritionStorageParent => parent as INutritionStorageParent;

    private float _storedNutrition = 0f;
    private float _targetNutritionLevel = -1f;

    public virtual bool IsPiped
    {
        get => false;
    }

    public virtual float storedNutrition
    {
        get => _storedNutrition;
        set
        {
            _storedNutrition = value;
        }
    }

    public virtual float TargetNutritionLevel
    {
        get => _targetNutritionLevel <= 0 ? MaxNutrition : _targetNutritionLevel;
        set => _targetNutritionLevel = value;
    }

    public virtual float MaxNutrition => Props.MaxNutrition;

    private List<IntVec3> cachedAdjCellsCardinal;

    public CompProperties_PawnStorageNutrition Props => props as CompProperties_PawnStorageNutrition;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _storedNutrition, "storedNutrition");
        Scribe_Values.Look(ref _targetNutritionLevel, "targetNutritionLevel", -1f);
    }

    public virtual bool AbsorbToFeedIfNeeded(Need_Food foodNeeds, float desiredFeed, out float amountFed)
    {
        amountFed = 0f;
        if (storedNutrition <= 0 && !TryAbsorbNutritionFromHopper(TargetNutritionLevel)) return false;
        float available = Mathf.Min(desiredFeed, storedNutrition);
        storedNutrition -= available;
        foodNeeds.CurLevel += available;
        amountFed = available;
        return true;
    }

    public virtual float ResolveStarvationIfPossibleAndNecessary(Need_Food foodNeeds, Pawn pawn) =>
        !foodNeeds.Starving ? 0f : FeedAndRecordWantedAmount(foodNeeds, foodNeeds.NutritionWanted, pawn);

    public virtual float FeedAndRecordWantedAmount(Need_Food foodNeeds, float neededFood, Pawn pawn, bool record = true)
    {
        float totalFeed = 0f;
        while (neededFood > 0 && AbsorbToFeedIfNeeded(foodNeeds, neededFood, out float amountFed) && amountFed > 0)
        {
            totalFeed += amountFed;
            neededFood -= amountFed;
        }

        if (totalFeed > 0f && record) pawn.records.AddTo(RecordDefOf.NutritionEaten, totalFeed);
        return totalFeed;
    }

    public override void CompTick()
    {
        base.CompTick();

        if (!PawnStoragesMod.settings.AllowNeedsDrop) return;

        if (!IsPiped && parent.IsHashIntervalTick(Props.TicksToAbsorbNutrients) && ParentAsNutritionStorageParent.IsActive)
        {
            parent.DirtyMapMesh(parent.Map);
            if (storedNutrition <= TargetNutritionLevel)
            {
                TryAbsorbNutritionFromHopper(TargetNutritionLevel - storedNutrition);
            }
        }

        if (parent.IsHashIntervalTick(Props.PawnTickInterval) && ParentAsNutritionStorageParent.HasStoredPawns)
        {
            parent.DirtyMapMesh(parent.Map);
            foreach (Pawn pawn in ParentAsNutritionStorageParent.StoredPawns)
            {
                EmulateScaledPawnAgeTick(pawn);

                //Need fall ticker
                Need_Food foodNeeds = pawn.needs?.food;
                if (foodNeeds == null)
                    continue;

                foodNeeds.CurLevel -= foodNeeds.FoodFallPerTick * Props.PawnTickInterval;
                ResolveStarvationIfPossibleAndNecessary(foodNeeds, pawn);

                // If still starving, apply malnutrition
                if (!foodNeeds.Starving)
                    foodNeeds.lastNonStarvingTick = Find.TickManager.TicksGame;

                // Need_Food.NeedInterval hardcodes 150 ticks, so adjust
                float adjustedMalnutritionSeverityPerInterval =
                    foodNeeds.MalnutritionSeverityPerInterval / 150f * Props.PawnTickInterval;
                if (foodNeeds.Starving)
                    HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, adjustedMalnutritionSeverityPerInterval);
                else
                    HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, -adjustedMalnutritionSeverityPerInterval);

                if (pawn.health.hediffSet.TryGetHediff(HediffDefOf.Malnutrition, out Hediff malnutritionHediff) && malnutritionHediff.Severity >= 0.75f)
                {
                    ParentAsNutritionStorageParent.ReleasePawn(pawn);
                    SendStavingLetter(pawn);
                }
            }
        }

        DoFeed();

        if (storedNutrition > 0)
        {
            ParentAsNutritionStorageParent.Notify_NutritionNotEmpty();
        }
        else
        {
            ParentAsNutritionStorageParent.Notify_NutritionEmpty();
        }
    }

    public virtual void DoFeed()
    {
        foreach (Pawn pawn in ParentAsNutritionStorageParent.StoredPawns)
        {
            //Need fall ticker
            Need_Food foodNeeds = pawn.needs?.food;
            if (foodNeeds == null) continue;
            if (!parent.IsHashIntervalTick(foodNeeds.TicksUntilHungryWhenFed)) continue;
            float nutritionDesired = foodNeeds.NutritionWanted;
            FeedAndRecordWantedAmount(foodNeeds, nutritionDesired, pawn);
        }
    }

    public virtual void SendStavingLetter(Pawn pawn)
    {
        LookTargets targets = new(pawn);
        ChoiceLetter letter = LetterMaker.MakeLetter(
            "PS_PawnEjectedStarvationTitle".Translate(pawn.LabelShort),
            "PS_PawnEjectedStarvation".Translate(pawn.LabelShort, parent.LabelShort),
            LetterDefOf.NegativeEvent,
            targets
        );
        Find.LetterStack.ReceiveLetter(letter);
    }

    public virtual void EmulateScaledPawnAgeTick(Pawn pawn)
    {
        int interval = Props.PawnTickInterval;
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


    public List<IntVec3> AdjCellsCardinalInBounds =>
        cachedAdjCellsCardinal ??= GenAdj.CellsAdjacentCardinal(parent)
            .Where(c => c.InBounds(parent.Map))
            .ToList();

    public virtual bool TryAbsorbNutritionFromHopper(float nutrition)
    {
        if (nutrition <= 0) return false;
        if (!HasEnoughFeedstockInHoppers()) return false;

        Thing feedInAnyHopper = FindFeedInAnyHopper();
        if (feedInAnyHopper == null)
        {
            return false;
        }

        int count = Mathf.Min(feedInAnyHopper.stackCount, Mathf.CeilToInt(nutrition / feedInAnyHopper.GetStatValue(StatDefOf.Nutrition)));
        storedNutrition += count * feedInAnyHopper.GetStatValue(StatDefOf.Nutrition);

        feedInAnyHopper.SplitOff(count);

        return true;
    }

    public virtual bool ValidFeedstock(ThingDef def) => def.IsNutritionGivingIngestible && def.ingestible.preferability != FoodPreferability.Undefined;

    public virtual Thing FindFeedInAnyHopper()
    {
        for (int index1 = 0; index1 < AdjCellsCardinalInBounds.Count; ++index1)
        {
            Thing feedInAnyHopper = null;
            Thing hopper = null;
            List<Thing> thingList = AdjCellsCardinalInBounds[index1].GetThingList(parent.Map);
            foreach (Thing maybeHopper in thingList)
            {
                if (ValidFeedstock(maybeHopper.def))
                    feedInAnyHopper = maybeHopper;
                if (maybeHopper.IsHopper())
                    hopper = maybeHopper;
            }

            if (feedInAnyHopper != null && hopper != null)
                return feedInAnyHopper;
        }

        return null;
    }

    public virtual bool HasEnoughFeedstockInHoppers()
    {
        float num = 0.0f;
        foreach (IntVec3 cellsCardinalInBound in AdjCellsCardinalInBounds)
        {
            Thing feedStockThing = null;
            Thing hopper = null;
            Map map = parent.Map;
            List<Thing> potentialFeedStockThings = cellsCardinalInBound.GetThingList(map);
            foreach (Thing potentialFeedStockThing in potentialFeedStockThings)
            {
                if (Building_NutrientPasteDispenser.IsAcceptableFeedstock(potentialFeedStockThing.def))
                    feedStockThing = potentialFeedStockThing;
                if (potentialFeedStockThing.IsHopper())
                    hopper = potentialFeedStockThing;
            }

            if (feedStockThing != null && hopper != null)
                num += feedStockThing.stackCount * feedStockThing.GetStatValue(StatDefOf.Nutrition);
            if (num >= (double) parent.def.building.nutritionCostPerDispense)
                return true;
        }

        return false;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!IsPiped)
        {
            yield return new Command_SetTargetNutritionLevel
            {
                nutritionComp = this,
                defaultLabel = "PS_CommandSetNutritionLevel".Translate(),
                defaultDesc = "PS_CommandSetNutritionLevelDesc".Translate(),
                icon = CompRefuelable.SetTargetFuelLevelCommand
            };
        }
        if (!DebugSettings.ShowDevGizmos) yield break;
        yield return new Command_Action
        {
            defaultLabel = "Feed Now", action = DoFeed, icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
        };
        yield return new Command_Action
        {
            defaultLabel = "Fill Nutrition", action = delegate { storedNutrition = MaxNutrition; }, icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
        };
        yield return new Command_Action
        {
            defaultLabel = "+10 Nutrition",
            action = delegate { storedNutrition = Mathf.Clamp(storedNutrition + 10f, 0f, 500f); },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
        };
        yield return new Command_Action
        {
            defaultLabel = "Empty Nutrition", action = delegate { storedNutrition = 0; }, icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
        };
        yield return new Command_Action
        {
            defaultLabel = "Absorb Nutrition from Hopper",
            action = delegate { TryAbsorbNutritionFromHopper(TargetNutritionLevel - storedNutrition); },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
        };
    }

    public bool doesBreeding => parent.GetComp<CompFarmBreeder>() != null;

    public override void PostDraw()
    {
        base.PostDraw();
        if (!Props.HasTip) return;

        ((Graphic_Single) parent.Graphic).mat = Props.MainTexture;

        float filled = 0.6f;

        if (doesBreeding && PawnStoragesMod.settings.SuggestiveSilo)
        {
            filled = Mathf.Clamp01(storedNutrition / MaxNutrition) * 0.6f;
        }

        Vector3 pos = parent.DrawPos;
        pos.z += filled;
        pos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();

        Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0.0f, 0f, 0.0f), new Vector3(Props.TipScale, 1f, Props.TipScale));
        Graphics.DrawMesh(MeshPool.plane10, matrix, Props.TipTexture, 0);
    }
}
