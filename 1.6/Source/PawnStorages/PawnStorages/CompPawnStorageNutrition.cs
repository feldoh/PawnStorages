using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using PawnStorages.Farm.Comps;
using PawnStorages.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

[StaticConstructorOnStartup]
public class CompPawnStorageNutrition : ThingComp
{
    [CanBeNull] public INutritionStorageParent ParentAsNutritionStorageParent => parent as INutritionStorageParent;

    private float _storedNutrition = 0f;
    private float _targetNutritionLevel = -1f;

    public virtual bool IsPiped
    {
        get => false;
    }

    public virtual float StoredNutrition
    {
        get => _storedNutrition;
        set { _storedNutrition = value; }
    }

    public virtual float TargetNutritionLevel
    {
        get => _targetNutritionLevel <= 0 ? MaxNutrition : _targetNutritionLevel;
        set => _targetNutritionLevel = value;
    }

    public virtual float MaxNutrition => Props.MaxNutrition;

    public CompProperties_PawnStorageNutrition Props => props as CompProperties_PawnStorageNutrition;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _storedNutrition, "storedNutrition");
        Scribe_Values.Look(ref _targetNutritionLevel, "targetNutritionLevel", -1f);
    }

    public virtual bool AbsorbToFeedIfNeeded(Need_Food foodNeeds, float desiredFeed, out float amountFed)
    {
        /*
         * Attempts to feed a pawn from stored nutrition, absorbing from hopper if needed
         */

        amountFed = 0f;

        // exit early if pawn is full
        if (Mathf.Approximately(foodNeeds.CurLevel, 1)) return false;

        desiredFeed = Mathf.Max(0, desiredFeed);

        // Return false if no stored nutrition and can't get more from hopper
        if (StoredNutrition <= 0 && !TryAbsorbNutritionFromSource(TargetNutritionLevel))
            return false;

        // Get minimum of desired amount and available stored nutrition
        float available = Mathf.Min(desiredFeed, StoredNutrition);

        // Deduct from storage and add to pawn's food level
        StoredNutrition -= available;
        foodNeeds.CurLevel += available;
        amountFed = available;
        return Mathf.Approximately(amountFed, desiredFeed);
    }


    public virtual float ResolveStarvationIfPossibleAndNecessary(Need_Food foodNeeds, Pawn pawn) =>
        !foodNeeds.Starving ? 0f : FeedAndRecordWantedAmount(foodNeeds, foodNeeds.NutritionWanted, pawn);

    public virtual float FeedAndRecordWantedAmount(Need_Food foodNeeds, float neededFood, Pawn pawn, bool record = true)
    {
        /*
         * Attempts to feed a pawn from the storage's nutrition supply and records the amount eaten
         */

        // Only feed pawns when they reach the threshold
        if (foodNeeds == null || foodNeeds.CurLevelPercentage >= Props.pawnFeedThreshold) return 0;

        float totalFeed = 0f;

        // Guard against infinite loops due to floating point precision issues
        int maxIterGuard = 20;

        // Keep feeding until needs are met or we run out of nutrition/iterations
        while (neededFood > 0 && --maxIterGuard > 0 && AbsorbToFeedIfNeeded(foodNeeds, neededFood, out float amountFed) && amountFed > 0 && !Mathf.Approximately(amountFed, 0))
        {
            totalFeed += amountFed;
            neededFood -= amountFed;
        }

        // Record nutrition eaten if requested
        if (totalFeed > 0f && record)
            pawn.records.AddTo(RecordDefOf.NutritionEaten, totalFeed);
        return totalFeed;
    }

    public virtual void AbsorbNutritionTick()
    {
        if (!parent.IsHashIntervalTick(Props.TicksToAbsorbNutrients) || !ParentAsNutritionStorageParent!.IsActive)
        {
            return;
        }

        parent.DirtyMapMesh(parent.Map);
        if (StoredNutrition <= TargetNutritionLevel)
        {
            TryAbsorbNutritionFromSource(TargetNutritionLevel - StoredNutrition);
        }
    }

    public virtual void ScaledPawnTick()
    {
        /*
         * Handles the periodic update of stored pawns' nutrition and health status at specified intervals.
         * This includes aging, food needs, and malnutrition management for pawns stored in the nutrition storage.
         */

        // Only process if it's time for an update and there are stored pawns
        if (!parent.IsHashIntervalTick(Props.PawnTickInterval) || !ParentAsNutritionStorageParent!.HasStoredPawns)
        {
            return;
        }

        foreach (Pawn pawn in ParentAsNutritionStorageParent.StoredPawns)
        {
            // Handle pawn aging
            EmulateScaledPawnAgeTick(pawn);

            // Get pawn's food needs
            Need_Food foodNeeds = pawn.needs?.food;
            if (foodNeeds is null)
                continue;

            // Decrease food level based on time passed
            foodNeeds.CurLevel -= foodNeeds.FoodFallPerTick * Props.PawnTickInterval;
            ResolveStarvationIfPossibleAndNecessary(foodNeeds, pawn);

            // Track when the pawn last wasn't starving
            if (!foodNeeds.Starving)
                foodNeeds.lastNonStarvingTick = Find.TickManager.TicksGame;

            // Calculate adjusted malnutrition severity based on tick interval.
            float adjustedMalnutritionSeverityPerInterval = foodNeeds.MalnutritionSeverityPerInterval / 150f * Props.PawnTickInterval;

            // Apply or heal malnutrition based on starving status
            if (foodNeeds.Starving)
                HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, adjustedMalnutritionSeverityPerInterval);
            else
                HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, -adjustedMalnutritionSeverityPerInterval);

            // Release pawn if malnutrition becomes too severe
            if (pawn.health.hediffSet.TryGetHediff(HediffDefOf.Malnutrition, out Hediff malnutritionHediff) && malnutritionHediff.Severity >= MalnutritionSeverityThreshold)
            {
                ParentAsNutritionStorageParent.ReleasePawn(pawn);
                SendStavingLetter(pawn);
            }
        }

        // Update map mesh to reflect changes for suggestive silos
        parent.DirtyMapMesh(parent.Map);
    }

    public override void CompTick()
    {
        base.CompTick();

        if (!PawnStoragesMod.settings.AllowNeedsDrop)
            return;

        if (ParentAsNutritionStorageParent is null)
            return;

        AbsorbNutritionTick();
        ScaledPawnTick();
        DoFeed();

        if (StoredNutrition > 0)
        {
            ParentAsNutritionStorageParent.Notify_NutritionNotEmpty();
        }
        else
        {
            ParentAsNutritionStorageParent.Notify_NutritionEmpty();
        }
    }

    private const float MalnutritionSeverityThreshold = 0.75f;

    public const int TimesToCheckPerHungerInterval = 8;

    public virtual void DoFeed()
    {
        if(ParentAsNutritionStorageParent is null) return;
        foreach (Pawn pawn in ParentAsNutritionStorageParent.StoredPawns)
        {
            //Need fall ticker
            Need_Food foodNeeds = pawn.needs?.food;
            if (foodNeeds is null)
                continue;
            // TODO: calls to .TicksUntilHungryWhenFed are a candidate for caching/optimisation
            // DoFeed is about 90% of the CompTick, and calls to .TicksUntilHungryWhenFed are about 90% of DoFeed
            if (!parent.IsHashIntervalTick(foodNeeds.TicksUntilHungryWhenFed / TimesToCheckPerHungerInterval))
                continue;
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

        if (pawn.ageTracker.lockedLifeStageIndex > 0)
            return;

        if (Find.TickManager.TicksGame >= pawn.ageTracker.nextGrowthCheckTick)
            pawn.ageTracker.CalculateGrowth(interval);

        if (ageBioYears < pawn.ageTracker.AgeBiologicalYears)
            pawn.ageTracker.BirthdayBiological(pawn.ageTracker.AgeBiologicalYears);
    }

    public List<IntVec3> AdjCellsCardinalInBounds => GenAdj.CellsAdjacentCardinal(parent).Where(c => c.InBounds(parent.Map)).ToList();


    public virtual bool IsValidNutritionRequest(float nutrition)
    {
        return nutrition > 0 && HasEnoughFeedstockInHoppers();
    }

    public virtual float ProcessFeedAbsorption(Thing feedSource, float nutritionToAbsorb)
    {
        /*
         * Processes the absorption of specified nutrition from a given feed source. Determines the amount of nutrition
         * that can be absorbed from the provided feed source and updates its state accordingly. The remaining
         * unabsorbed nutrition is returned.
         */
        if (feedSource == null)
            return nutritionToAbsorb;

        float remainingNutrition = nutritionToAbsorb;
        float nutritionPerUnit = feedSource.GetStatValue(StatDefOf.Nutrition);

        while (remainingNutrition > 0)
        {
            int unitsToAbsorb = Mathf.Min(feedSource.stackCount, Mathf.FloorToInt(remainingNutrition / nutritionPerUnit));

            if (unitsToAbsorb <= 0)
                break;

            float nutritionAbsorbed = unitsToAbsorb * nutritionPerUnit;
            if (nutritionAbsorbed <= 0)
                break;

            remainingNutrition -= nutritionAbsorbed;

            if (unitsToAbsorb > 1)
            {
                Thing thing = feedSource.SplitOff(unitsToAbsorb);
                thing.Destroy();
            }
            else
            {
                feedSource.DeSpawn();
                break;
            }
        }

        return remainingNutrition;
    }

    public virtual bool AddNutritionToStorage(float amount)
    {
        if (amount <= 0 || Mathf.Approximately(amount, 0)) return false;
        StoredNutrition += amount;
        return true;
    }

    public virtual bool TryAbsorbNutritionFromSource(float requestedNutrition)
    {
        // Absorb from hoppers
        if (requestedNutrition <= 0)
            return false;

        if (!IsValidNutritionRequest(requestedNutrition))
            return false;

        Thing feedSource = FindFeedInAnyHopper();
        if (feedSource is null) return false;

        float remainingNutrition = ProcessFeedAbsorption(feedSource, requestedNutrition);
        float absorbedNutrition = requestedNutrition - remainingNutrition;

        return AddNutritionToStorage(absorbedNutrition);
    }

    public static bool IsAcceptableFeedstock(ThingDef def)
    {
        return def.IsNutritionGivingIngestible && def.ingestible.preferability != FoodPreferability.Undefined;
    }

    public virtual bool ValidFeedstock(ThingDef def) => IsAcceptableFeedstock(def);

    public virtual Thing FindFeedInAnyHopper()
    {
        /*
         * Finds a valid feed item in any adjacent hopper that can be used as nutrition.
         * Returns the feed item if found, otherwise returns null.
         * This method iterates through all cardinally adjacent cells to the parent object,
         * checks if those cells contain valid feedstock, and ensures the presence of a hopper.
         */
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
        /*
         * Determines if there is enough feedstock available in the hoppers connected to the storage unit.
         * The method calculates the total nutrition provided by acceptable feedstock in conjunction with
         * hoppers adjacent to the parent object. If the total nutrition meets or exceeds the required
         * nutrition cost for dispensing, the method returns true.
         */
        float num = 0.0f;
        foreach (IntVec3 cellsCardinalInBound in AdjCellsCardinalInBounds)
        {
            Thing feedStockThing = null;
            Thing hopper = null;
            Map map = parent.Map;
            List<Thing> potentialFeedStockThings = cellsCardinalInBound.GetThingList(map);

            foreach (Thing potentialFeedStockThing in potentialFeedStockThings)
            {
                if (IsAcceptableFeedstock(potentialFeedStockThing.def) )
                    feedStockThing = potentialFeedStockThing;
                if (potentialFeedStockThing.IsHopper())
                    hopper = potentialFeedStockThing;
            }

            if (feedStockThing != null && hopper != null)
                num += feedStockThing.stackCount * feedStockThing.GetStatValue(StatDefOf.Nutrition);

            if (num > 0 && num >= (double)parent.def.building.nutritionCostPerDispense)
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
                icon = CompRefuelable.SetTargetFuelLevelCommand,
            };
        }
        if (!DebugSettings.ShowDevGizmos)
            yield break;
        yield return new Command_Action
        {
            defaultLabel = "Feed Now",
            action = DoFeed,
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
        };
        yield return new Command_Action
        {
            defaultLabel = "Fill Nutrition",
            action = delegate
            {
                StoredNutrition = MaxNutrition;
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
        };
        yield return new Command_Action
        {
            defaultLabel = "+10 Nutrition",
            action = delegate
            {
                StoredNutrition = Mathf.Clamp(StoredNutrition + 10f, 0f, Props.MaxNutrition);
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
        };
        yield return new Command_Action
        {
            defaultLabel = "Empty Nutrition",
            action = delegate
            {
                StoredNutrition = 0;
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
        };
        yield return new Command_Action
        {
            defaultLabel = "Absorb Nutrition from Hopper",
            action = delegate
            {
                TryAbsorbNutritionFromSource(TargetNutritionLevel - StoredNutrition);
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
        };
    }

    public bool doesBreeding => parent.GetComp<CompFarmBreeder>() != null;

    public override void PostDraw()
    {
        base.PostDraw();
        if (!Props.HasTip)
            return;

        if(parent.Graphic is not Graphic_Single graphic) return;
        graphic.mat = Props.MainTexture;

        float filled = 0.6f;

        if (doesBreeding && PawnStoragesMod.settings.SuggestiveSilo)
        {
            filled = Mathf.Clamp01(StoredNutrition / MaxNutrition) * 0.6f;
        }

        Vector3 pos = parent.DrawPos;
        pos.z += filled;
        pos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();

        Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0.0f, 0f, 0.0f), new Vector3(Props.TipScale, 1f, Props.TipScale));
        Graphics.DrawMesh(MeshPool.plane10, matrix, Props.TipTexture, 0);
    }
}
