using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(CompRottable), "CompTickRare")]
public static class CompRottable_CompTickRare_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(CompRottable __instance)
    {
        if (!__instance.parent.Spawned)
            return true;

        Map map = __instance.parent.Map;
        IntVec3 position = __instance.parent.Position;

        List<Thing> thingsAtCell = position.GetThingList(map);
        return thingsAtCell.Select(thing => thing.TryGetComp<CompHopperCooler>()).All(cooler => cooler is not { IsPowered: true });
    }
}
