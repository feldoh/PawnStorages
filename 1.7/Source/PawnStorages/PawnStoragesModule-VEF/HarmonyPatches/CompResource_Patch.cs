using HarmonyLib;
using PipeSystem;
using Verse;

namespace PawnStorages.VEF.HarmonyPatches;

[HarmonyPatch(typeof(CompResource))]
public static class CompResource_Patch
{
    [HarmonyPatch(nameof(CompResource.CompInspectStringExtra))]
    [HarmonyPrefix]
    public static bool CompInspectStringExtra_Prefix(CompResource __instance, ref string __result)
    {
        if (__instance.PipeNet == null || __instance.PipeNet.connectors.Count <= 1)
        {
            __result = "PS_PipeSystem_NotConnected".Translate((NamedArgument)__instance.Resource.name);
            return false;
        }

        return true;
    }
}
