using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages.Anomaly;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Patch_PawnDied
{
    public static void Postfix(Pawn __instance)
    {
        if (!ModsConfig.AnomalyActive)
            return;

        AllFeature_GameComponent comp = Current.Game?.GetComponent<AllFeature_GameComponent>();
        if (comp == null)
            return;

        if (comp.AllPawn == __instance)
        {
            comp.Notify_AllDied();
        }
        else if (comp.NothingPawn == __instance)
        {
            comp.Notify_NothingKilled();
        }
    }
}
