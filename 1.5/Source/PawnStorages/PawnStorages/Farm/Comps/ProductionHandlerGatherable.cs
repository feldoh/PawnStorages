using System.Collections.Generic;
using PawnStorages.Farm.Interfaces;
using PawnStorages.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Farm.Comps;

public class ProductionHandlerGatherable : IProductionHandler
{
    public void TickPawns(IProductionParent productionParent, List<Thing> daysProduce, List<IExtraProductionHandler> extraProductionHandlers, int? tickInterval)
    {
        List<Pawn> producingPawns = [..productionParent.ProducingPawns];
        foreach (Pawn pawn in producingPawns)
        {
            GatherableTick(pawn, daysProduce, extraProductionHandlers, tickInterval ?? productionParent.TickInterval);
        }
    }

    public void GatherableTick(Pawn pawn, List<Thing> daysProduce, List<IExtraProductionHandler> extraProductionHandlers, int tickInterval)
    {
        if (!(pawn.TryGetComp(out CompHasGatherableBodyResource gatherable) &&
              (pawn.gender == Gender.Female || gatherable is not CompMilkable milkable || !milkable.Props.milkFemaleOnly))) return;
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
            daysProduce.Add(thing);
        }

        foreach (IExtraProductionHandler extraProductionHandler in extraProductionHandlers) extraProductionHandler.ProduceExtraProducts(gatherable, daysProduce);

        gatherable.fullness = 0f;
    }

    public void MakePawnReadyToProduce(Pawn pawn)
    {
        if (pawn.TryGetComp(out CompHasGatherableBodyResource compGatherable))
        {
            compGatherable.fullness = 1f;
        }
    }
}
