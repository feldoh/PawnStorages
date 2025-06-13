using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Farm.Comps
{
    public class CompFarmProducer : CompPawnStorageProducer
    {
        public override void CompTick()
        {
            base.CompTick();

            if (!PawnStoragesMod.settings.AllowNeedsDrop) return;

            if (parent.IsHashIntervalTick(ParentAsProductionParent.BuildingTickInterval))
            {
                foreach (Pawn pawn in ParentAsProductionParent.ProducingPawns)
                {
                    if (pawn.TryGetComp(out CompEggLayer compLayer) && (pawn.gender == Gender.Female || !compLayer.Props.eggLayFemaleOnly))
                    {
                        EggLayerTick(compLayer, ParentAsProductionParent.BuildingTickInterval);
                    }

                    if (pawn.TryGetComp(out CompHasGatherableBodyResource compGatherable) &&
                        (pawn.gender == Gender.Female || compGatherable is not CompMilkable milkable || !milkable.Props.milkFemaleOnly))
                    {
                        GatherableTick(compGatherable, ParentAsProductionParent.BuildingTickInterval);
                    }
                }
            }

            if (!ProduceNow && (!parent.IsHashIntervalTick(60000 / Math.Max(PawnStoragesMod.settings.ProductionsPerDay, 1)) || DaysProduce.Count <= 0 || !ParentAsProductionParent.IsActive)) return;
            List<Thing> failedToPlace = [];
            failedToPlace.AddRange(DaysProduce.Where(thing => !GenPlace.TryPlaceThing(thing, parent.Position, parent.Map, ThingPlaceMode.Near)));
            DaysProduce.Clear();
            DaysProduce.AddRange(failedToPlace);
        }

        public void EggLayerTick(CompEggLayer layer, int tickInterval = 1)
        {
            if (!layer.Active) return;
            float eggReadyIncrement = (float)(1f / ((double)layer.Props.eggLayIntervalDays * 60000f));
            if (layer.parent is not Pawn layingPawn) return;
            eggReadyIncrement *= PawnUtility.BodyResourceGrowthSpeed(layingPawn);
            // we're not doing this every tick so bump the progress
            eggReadyIncrement *= tickInterval;
            eggReadyIncrement *= PawnStoragesMod.settings.ProductionScale;
            layer.eggProgress += eggReadyIncrement;
            layer.eggProgress = Mathf.Clamp(layer.eggProgress, 0f, 1f);

            if (!(layer.eggProgress >= 1f)) return;
            Thing egg = null;
            if (layer.Props.eggFertilizedDef != null &&
                layer.Props.eggFertilizationCountMax > 0 &&
                ParentAsProductionParent.ProducingPawns.Find(p => p.kindDef == layingPawn.kindDef) is {} fertilizer &&
                (layer.Props.eggUnfertilizedDef == null || Rand.Bool)) // Flip a coin to see if fertilised unless there is no unfertilised option
            {
                layer.Fertilize(fertilizer);
                egg = layer.ProduceEgg();
            }

            // if there was no fertilised def, or we lost the coin flip, make an unfertilised egg if possible
            if (egg == null && layer.Props.eggUnfertilizedDef != null)
            {
                egg = layer.ProduceEgg();
            }

            if (egg != null)
            {
                DaysProduce.Add(egg);
            }
        }

        public void GatherableTick(CompHasGatherableBodyResource gatherable, int tickInterval = 1)
        {
            if (!gatherable.Active) return;
            float gatherableReadyIncrement = (float)(1f / ((double)gatherable.GatherResourcesIntervalDays * 60000f));
            gatherableReadyIncrement *= PawnUtility.BodyResourceGrowthSpeed(gatherable.parent as Pawn);
            // we're not doing this every tick so bump the progress
            gatherableReadyIncrement *= tickInterval;
            gatherableReadyIncrement *= PawnStoragesMod.settings.ProductionScale;
            gatherable.fullness += gatherableReadyIncrement;
            gatherable.fullness = Mathf.Clamp(gatherable.fullness, 0f, 1f);

            if (!gatherable.ActiveAndFull) return;
            int amountToGenerate = GenMath.RoundRandom(gatherable.ResourceAmount * gatherable.fullness);
            while (amountToGenerate > 0f)
            {
                int generateThisLoop = Mathf.Clamp(amountToGenerate, 1, gatherable.ResourceDef.stackLimit);
                amountToGenerate -= generateThisLoop;
                Thing thing = ThingMaker.MakeThing(gatherable.ResourceDef);
                thing.stackCount = generateThisLoop;
                DaysProduce.Add(thing);
            }

            gatherable.fullness = 0f;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra()) yield return gizmo;
            if (!DebugSettings.ShowDevGizmos) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Make all animals ready to produce",
                action = delegate
                {
                    foreach (Pawn storedPawn in ParentAsProductionParent.ProducingPawns)
                    {
                        storedPawn.needs.food.CurLevel = storedPawn.needs.food.MaxLevel;

                        if (storedPawn.TryGetComp(out CompEggLayer compLayer))
                        {
                            compLayer.eggProgress = 1f;
                        }

                        if (storedPawn.TryGetComp(out CompHasGatherableBodyResource compGatherable))
                        {
                            compGatherable.fullness = 1f;
                        }
                    }
                },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };
        }
    }
}
