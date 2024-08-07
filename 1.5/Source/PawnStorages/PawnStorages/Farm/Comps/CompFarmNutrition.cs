﻿using System.Collections.Generic;
using System.Linq;
using PawnStorages.Farm.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Farm.Comps
{
    [StaticConstructorOnStartup]
    public class CompFarmNutrition : ThingComp
    {
        public INutritionStorageParent Parent => parent as INutritionStorageParent;

        private float _storedNutrition = 0f;

        private INutritionStoreAlternative AlternativeStore => (INutritionStoreAlternative) parent.AllComps.FirstOrDefault(c => c is INutritionStoreAlternative);

        public bool doesBreeding => parent.GetComp<CompFarmBreeder>() != null;

        public bool HasAltStore => AlternativeStore != null;

        public float storedNutrition
        {
            get => HasAltStore ? AlternativeStore.CurrentStored : _storedNutrition;
            set
            {
                if (HasAltStore)
                {
                    AlternativeStore.CurrentStored = value;
                }
                else
                    _storedNutrition = value;
            }
        }

        public float MaxNutrition => HasAltStore ? AlternativeStore.MaxStoreSize : Props.maxNutrition;

        private List<IntVec3> cachedAdjCellsCardinal;

        public CompProperties_FarmNutrition Props => props as CompProperties_FarmNutrition;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref _storedNutrition, "storedNutrition");
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!PawnStoragesMod.settings.AllowNeedsDrop) return;

            if (parent.IsHashIntervalTick(Props.ticksToAbsorbNutrients) && Parent.IsActive)
            {
                if (storedNutrition <= MaxNutrition)
                {
                    TryAbsorbNutritionFromHopper(MaxNutrition - storedNutrition);
                }
            }

            if (parent.IsHashIntervalTick(Props.animalTickInterval) && Parent.HasStoredPawns)
            {
                foreach (Pawn pawn in Parent.StoredPawns)
                {
                    EmulateScaledPawnAgeTick(pawn);

                    //Need fall ticker
                    Need_Food foodNeeds = pawn.needs?.food;
                    if (foodNeeds == null)
                        continue;

                    foodNeeds.CurLevel -= foodNeeds.FoodFallPerTick * Props.animalTickInterval;
                    if (!foodNeeds.Starving)
                        foodNeeds.lastNonStarvingTick = Find.TickManager.TicksGame;

                    // Need_Food.NeedInterval hardcodes 150 ticks, so adjust
                    float adjustedMalnutritionSeverityPerInterval =
                        foodNeeds.MalnutritionSeverityPerInterval / 150f * Props.animalTickInterval;

                    if (foodNeeds.Starving)
                        HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, adjustedMalnutritionSeverityPerInterval);
                    else
                        HealthUtility.AdjustSeverity(pawn, HediffDefOf.Malnutrition, -adjustedMalnutritionSeverityPerInterval);

                    if (pawn.health.hediffSet.TryGetHediff(HediffDefOf.Malnutrition, out Hediff malnutritionHediff) && malnutritionHediff.Severity >= 0.75f)
                    {
                        Parent.ReleasePawn(pawn);
                        SendStavingLetter(pawn);
                        continue;
                    }

                    // if not powered
                    if (!Parent.IsActive) continue;

                    //Hopper absorption ticker
                    if (storedNutrition <= 0)
                    {
                        TryAbsorbNutritionFromHopper(MaxNutrition - storedNutrition);
                        // Still no food available, no point trying to feed
                        if (storedNutrition <= 0) continue;
                    }
                }
            }

            foreach (Pawn pawn in Parent.StoredPawns)
            {
                //Need fall ticker
                Need_Food foodNeeds = pawn.needs?.food;
                if (foodNeeds == null) continue;
                if (!parent.IsHashIntervalTick(foodNeeds.TicksUntilHungryWhenFed)) continue;
                 float available = Mathf.Min(foodNeeds.NutritionWanted, storedNutrition);
                storedNutrition -= available;

                foodNeeds.CurLevel += available;
                pawn.records.AddTo(RecordDefOf.NutritionEaten, available);
            }

            if (storedNutrition > 0)
            {
                Parent.Notify_NutritionNotEmpty();
            }
            else
            {
                Parent.Notify_NutritionEmpty();
            }
        }

        public void SendStavingLetter(Pawn pawn)
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

        public void EmulateScaledPawnAgeTick(Pawn pawn)
        {
            int interval = Props.animalTickInterval;

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

        public bool TryAbsorbNutritionFromHopper(float nutrition)
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
                if (num >= (double)parent.def.building.nutritionCostPerDispense)
                    return true;
            }

            return false;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!DebugSettings.ShowDevGizmos) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Fill Nutrition",
                action = delegate { storedNutrition = MaxNutrition; },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };
            yield return new Command_Action
            {
                defaultLabel = "+10 Nutrition",
                action = delegate { storedNutrition = Mathf.Clamp(storedNutrition + 10f, 0f, 500f); },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };
            yield return new Command_Action
            {
                defaultLabel = "Empty Nutrition",
                action = delegate { storedNutrition = 0; },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };
            yield return new Command_Action
            {
                defaultLabel = "Absorb Nutrition from Hopper",
                action = delegate { TryAbsorbNutritionFromHopper(MaxNutrition - storedNutrition); },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };
        }

        public override void PostDraw()
        {
            ((Graphic_Single) parent.Graphic).mat = Props.MainTexture;

            base.PostDraw();

            if (!doesBreeding || !PawnStoragesMod.settings.SuggestiveSilo)
                return;

            float filled = (storedNutrition / Props.maxNutrition) * 0.6f;

            Vector3 pos = parent.DrawPos;
            pos.z += filled;
            pos.y = AltitudeLayer.BuildingOnTop.AltitudeFor();

            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0.0f, 0f, 0.0f), new Vector3(Props.TipScale, 1f, Props.TipScale));
            Graphics.DrawMesh(MeshPool.plane10, matrix, Props.TipTexture, 0);
        }
    }
}
