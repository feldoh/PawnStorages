using System.Collections.Generic;
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
        for (int i = 0; i < thingsAtCell.Count; i++)
        {
            Thing thing = thingsAtCell[i];
            CompHopperCooler cooler = thing.TryGetComp<CompHopperCooler>();
            if (cooler != null && cooler.IsPowered)
                return false;
        }

        return true;
    }
}
