using System.Linq;
using HarmonyLib;
using KCSG;
using RimWorld;
using Verse;

namespace PawnStorages.VEF;

[HarmonyPatch(typeof(SymbolUtils), "Generate")]
public static class GenerateBuildingAt_Patch
{
    [HarmonyPrefix]
    public static bool Generate(Map map, SymbolDef symbol, IntVec3 cell, Faction faction)
    {
        if (symbol.thing != "PS_PawnStatue" || symbol.chanceToContainPawn <= 0 || Rand.Value > (double) symbol.chanceToContainPawn) return true;
        ThingDef stuff = (symbol.stuff == null || symbol.randomizeStuff) ? GenStuff.RandomStuffByCommonalityFor(PS_DefOf.PS_PawnStatue) : DefDatabase<ThingDef>.GetNamed(symbol.stuff, false);
        ThingWithComps storageItem = ThingMaker.MakeThing(PS_DefOf.PS_PawnStatue, stuff) as ThingWithComps;
        if (storageItem == null) return true;
        storageItem.InitializeComps();
        CompPawnStorage storageComp = storageItem.GetInnerIfMinified()?.TryGetComp<CompPawnStorage>();
        Faction chosenFaction = symbol.spawnPartOfFaction ? map.ParentFaction : null;
        Pawn pawn = null;
        if (!PawnStoragesMod.settings.ForcedPawn.NullOrEmpty() &&
            PawnsFinder.AllMapsAndWorld_Alive.Where(p => p.ThingID == PawnStoragesMod.settings.ForcedPawn).FirstOrFallback() is { } forcedPawn)
        {
            pawn = forcedPawn;
            PawnStoragesMod.settings.ForcedPawn = "";
        }
        else
        {
            pawn = SymbolUtils.GeneratePawnForContainer(symbol, map);
        }

        storageComp?.StoredPawns?.Add(pawn);
        storageComp?.SetLabelDirty();
        storageComp?.SetBarDirty();

        GenSpawn.Spawn(storageItem, cell, map, symbol.rotation, WipeMode.VanishOrMoveAside);
        // Set the faction if applicable
        if (symbol.spawnPartOfFaction && faction != null && storageItem.def.CanHaveFaction)
        {
            storageItem.SetFaction(faction);
        }

        return false;
    }
}
