using System.Collections.Generic;
using PawnStorages.Farm.Interfaces;
using PawnStorages.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Farm.Comps;

public class ProductionHandlerEggLayer : IProductionHandler
{
    public void TickPawns(IProductionParent productionParent, List<Thing> daysProduce, List<IExtraProductionHandler> extraProductionHandlers, int? tickInterval)
    {
        List<Pawn> producingPawns = [..productionParent.ProducingPawns];
        foreach (Pawn pawn in producingPawns)
        {
            EggLayerTick(pawn, producingPawns, daysProduce, extraProductionHandlers, tickInterval ?? productionParent.TickInterval);
        }
    }

    public void EggLayerTick(Pawn pawn, List<Pawn> producingPawns, List<Thing> daysProduce, List<IExtraProductionHandler> extraProductionHandlers, int tickInterval)
    {
        if (!(pawn.TryGetComp(out CompEggLayer layer) && (pawn.gender == Gender.Female || !layer.Props.eggLayFemaleOnly))) return;
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

        foreach (IExtraProductionHandler extraProductionHandler in extraProductionHandlers) extraProductionHandler.ProduceExtraProducts(layer, daysProduce);

        Thing egg = null;
        if (layer.Props.eggFertilizedDef != null &&
            layer.Props.eggFertilizationCountMax > 0 &&
            producingPawns.Find(p => p.kindDef == layingPawn.kindDef) is {} fertilizer &&
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
            daysProduce.Add(egg);
        }
    }

    public void MakePawnReadyToProduce(Pawn pawn)
    {
        if (pawn.TryGetComp(out CompEggLayer compLayer))
        {
            compLayer.eggProgress = 1f;
        }
    }
}
