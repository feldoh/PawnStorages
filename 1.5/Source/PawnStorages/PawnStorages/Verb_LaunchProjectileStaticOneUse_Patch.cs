using HarmonyLib;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(Verb_LaunchProjectileStaticOneUse))]
public static class Verb_LaunchProjectileStaticOneUse_Patch
{
    // [HarmonyPatch(nameof(Verb_LaunchProjectileStaticOneUse.SelfConsume))]
    // [HarmonyPrefix]
    // public static bool SelfConsume(Verb_LaunchProjectileStaticOneUse __instance)
    // {
    //     return __instance is not Verb_LaunchCaptureSphere;
    // }
}