using System;
using System.Collections.Generic;
using System.Linq;
using PawnStorages.Farm.Interfaces;
using UnityEngine;
using Verse;

namespace PawnStorages.Farm.Comps;

public class CompFarmProducer : CompPawnStorageProducer
{
    public static List<IProductionHandler> ProductionHandlers = [new ProductionHandlerEggLayer(), new ProductionHandlerGatherable(), new ProductionHandlerSpawner()];
    public static List<IExtraProductionHandler> ExtraProductionHandlers = [];
    public override void CompTick()
    {
        base.CompTick();

        if (!PawnStoragesMod.settings.AllowNeedsDrop) return;

        if (parent.IsHashIntervalTick(ParentAsProductionParent.TickInterval))
        {
            foreach (IProductionHandler handler in ProductionHandlers)
            {
                handler.TickPawns(ParentAsProductionParent, DaysProduce, ExtraProductionHandlers, ParentAsProductionParent.TickInterval);
            }
        }

        if (!ProduceNow && (!parent.IsHashIntervalTick(60000 / Math.Max(PawnStoragesMod.settings.ProductionsPerDay, 1)) || DaysProduce.Count <= 0 || !ParentAsProductionParent.IsActive)) return;
        List<Thing> failedToPlace = [];
        failedToPlace.AddRange(DaysProduce.Where(thing => !GenPlace.TryPlaceThing(thing, parent.Position, parent.Map, ThingPlaceMode.Near)));
        DaysProduce.Clear();
        DaysProduce.AddRange(failedToPlace);
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

                    foreach (IProductionHandler handler in ProductionHandlers)
                    {
                        handler.MakePawnReadyToProduce(storedPawn);
                    }
                }
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
        };
    }
}
