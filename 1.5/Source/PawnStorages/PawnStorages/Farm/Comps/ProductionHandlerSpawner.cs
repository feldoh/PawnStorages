using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PawnStorages.Farm.Interfaces;
using PawnStorages.Interfaces;
using RimWorld;
using Verse;

namespace PawnStorages.Farm.Comps;

public class ProductionHandlerSpawner : IProductionHandler
{
    public static Lazy<FieldInfo> CompSpawner_ticksUntilSpawn = new(()=>AccessTools.Field(typeof(CompSpawner), "ticksUntilSpawn"));
    public void TickPawns(IProductionParent productionParent, List<Thing> daysProduce, List<IExtraProductionHandler> extraProductionHandlers, int? tickInterval)
    {
        List<Pawn> producingPawns = [..productionParent.ProducingPawns];
        foreach (Pawn pawn in producingPawns)
        {
            SpawnerTick(pawn, daysProduce, extraProductionHandlers, tickInterval ?? productionParent.TickInterval);
        }
    }

    public void SpawnerTick(Pawn pawn, List<Thing> daysProduce, List<IExtraProductionHandler> extraProductionHandlers, int tickInterval)
    {
        if (!pawn.TryGetComp(out CompSpawner spawner)) return;
        int ticksUntilSpawn = (int)CompSpawner_ticksUntilSpawn.Value.GetValue(spawner);
        ticksUntilSpawn -= tickInterval;
        CompSpawner_ticksUntilSpawn.Value.SetValue(spawner, ticksUntilSpawn);

        if(ticksUntilSpawn > 0) return;

        Thing thing = ThingMaker.MakeThing(spawner.PropsSpawner.thingToSpawn);
        thing.stackCount = spawner.PropsSpawner.spawnCount;

        if (spawner.PropsSpawner.inheritFaction && thing.Faction != spawner.parent.Faction)
            thing.SetFaction(spawner.parent.Faction);

        daysProduce.Add(thing);

        foreach (IExtraProductionHandler extraProductionHandler in extraProductionHandlers) extraProductionHandler.ProduceExtraProducts(spawner, daysProduce);

        CompSpawner_ticksUntilSpawn.Value.SetValue(spawner, spawner.PropsSpawner.spawnIntervalRange.RandomInRange);
    }

    public void MakePawnReadyToProduce(Pawn pawn)
    {
        if (pawn.TryGetComp(out CompSpawner spawner))
        {
            CompSpawner_ticksUntilSpawn.Value.SetValue(spawner, 0);
        }
    }
}
