using HarmonyLib;
using PipeSystem;
using RimWorld;
using Verse;

namespace PawnStorages.VEF.HarmonyPatches;

[HarmonyPatch(typeof(CompResource))]
public static class CompResource_Patch
{
    [HarmonyPatch(nameof(CompResource.CompInspectStringExtra))]
    [HarmonyPrefix]
    public static bool CompInspectStringExtra_Prefix(CompResource __instance, ref string __result)
    {
        if (__instance.PipeNet != null && __instance.PipeNet.connectors.Count > 1) return true;

        __result = "PipeSystem_NotConnected".Translate((NamedArgument) (__instance.Resource?.name ?? ThingDefOf.MealNutrientPaste.label));
        return false;

    }
}
