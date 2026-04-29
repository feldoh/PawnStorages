using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages.Anomaly;

/// <summary>
/// Detects when "Nothing" leaves the map without being killed.
/// Uses Pawn.ExitMap which is called when pawns walk off the map edge.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
public static class Patch_NothingLeftMap
{
    public static void Postfix(Pawn __instance)
    {
        if (!ModsConfig.AnomalyActive)
            return;

        AllFeature_GameComponent comp = Current.Game?.GetComponent<AllFeature_GameComponent>();
        if (comp == null)
            return;

        if (comp.NothingPawn == __instance && comp.Stage == AllDiscoveryStage.QuestActive)
        {
            comp.Notify_NothingLeftMap();
        }
    }
}
