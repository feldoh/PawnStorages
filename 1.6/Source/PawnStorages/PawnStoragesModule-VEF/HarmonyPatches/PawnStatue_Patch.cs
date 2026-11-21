using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KCSG;
using RimWorld;
using Verse;
using StructureLayoutDef = KCSG.StructureLayoutDef;

namespace PawnStorages.VEF.HarmonyPatches;

[HarmonyPatch]
public static class GenerateBuildingAt_Patch
{
    /**
     * There are two Generate methods, one with an ICollection which is newer and one without.
     * If the one with a collection is there, we want to patch that as that if the bottom of the call chain otherwise patch the old one
     */
    [HarmonyTargetMethod]
    public static MethodInfo TargetMethod()
    {
        Type[] paramTypes = [typeof(SymbolDef), typeof(StructureLayoutDef), typeof(Map), typeof(IntVec3), typeof(Faction), typeof(ThingDef)];
        MethodInfo newGenerateMethod = AccessTools.Method(typeof(SymbolUtils), "Generate", paramTypes.Append(typeof(ICollection<Thing>)).ToArray());
        return newGenerateMethod != null ? newGenerateMethod : AccessTools.Method(typeof(SymbolUtils), "Generate", paramTypes);
    }

    [HarmonyPrefix]
    public static bool Generate(Map map, SymbolDef symbol, IntVec3 cell, Faction faction)
    {
        if (symbol.thing != "PS_PawnStatue" || symbol.chanceToContainPawn <= 0 || Rand.Value > (double)symbol.chanceToContainPawn)
            return true;
        ThingDef stuff =
            (symbol.stuff == null || symbol.randomizeStuff) ? GenStuff.RandomStuffByCommonalityFor(PS_DefOf.PS_PawnStatue) : DefDatabase<ThingDef>.GetNamed(symbol.stuff, false);
        ThingWithComps storageItem = ThingMaker.MakeThing(PS_DefOf.PS_PawnStatue, stuff) as ThingWithComps;
        if (storageItem == null)
            return true;
        storageItem.InitializeComps();
        if (storageItem is not IThingHolder holder)
            return true;

        CompPawnStorage storageComp = storageItem.GetInnerIfMinified()?.TryGetComp<CompPawnStorage>();
        Faction chosenFaction = symbol.spawnPartOfFaction ? map.ParentFaction : null;
        Pawn pawn = null;
        if (
            !PawnStoragesMod.settings.ForcedPawn.NullOrEmpty()
            && PawnsFinder.AllMapsAndWorld_Alive.Where(p => p.ThingID == PawnStoragesMod.settings.ForcedPawn).FirstOrFallback() is { } forcedPawn
        )
        {
            pawn = forcedPawn;
            PawnStoragesMod.settings.ForcedPawn = "";
        }
        else
        {
            pawn = SymbolUtils.GeneratePawnForContainer(symbol, map);
        }

        holder.GetDirectlyHeldThings()?.TryAdd(pawn);
        storageComp?.SetLabelDirty();

        GenSpawn.Spawn(storageItem, cell, map, symbol.rotation, WipeMode.VanishOrMoveAside);
        // Set the faction if applicable
        if (symbol.spawnPartOfFaction && faction != null && storageItem.def.CanHaveFaction)
        {
            storageItem.SetFaction(faction);
        }

        return false;
    }
}
