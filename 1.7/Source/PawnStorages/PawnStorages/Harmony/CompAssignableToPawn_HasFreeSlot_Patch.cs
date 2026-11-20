using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(CompAssignableToPawn), "get_HasFreeSlot")]
public static class CompAssignableToPawn_HasFreeSlot_Patch
{
    public static bool Prefix(CompAssignableToPawn __instance, ref bool __result)
    {
        if (__instance is not CompAssignableToPawn_PawnStorage)
            return true;

        CompPawnStorage storageComp = __instance.parent.TryGetComp<CompPawnStorage>();
        if (storageComp == null)
            return true;

        __result = __instance.assignedPawns.Count < storageComp.MaxStoredPawns();
        return false;
    }
}
