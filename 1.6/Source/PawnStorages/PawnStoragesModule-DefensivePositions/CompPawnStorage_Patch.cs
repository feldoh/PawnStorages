using System;
using System.Collections.Generic;
using System.Reflection;
using DefensivePositions;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace PawnStorages.DefensivePositions;

[HarmonyPatch(typeof(CompPawnStorage))]
public static class CompPawnStorage_Patch
{
    private static Lazy<MethodInfo> hasSavedPositionMethod = new(() => AccessTools.Method(typeof(PawnSavedPositionHandler), "HasSavedPosition"));
    private static Lazy<MethodInfo> getPositionMethod = new(() => AccessTools.Method(typeof(PawnSavedPositionHandler), "GetPosition"));
    private static Lazy<MethodInfo> draftToPositionMethod = new(() => AccessTools.Method(typeof(PawnSavedPositionHandler), "DraftToPosition"));
    private static Lazy<Texture2D> releaseToDefensivePositionTex = new(() => ContentFinder<Texture2D>.Get("UIPositionLargeActive", true));

    [HarmonyPatch(nameof(CompPawnStorage.CompGetGizmosExtra))]
    [HarmonyPostfix]
    public static IEnumerable<Gizmo> CompGetGizmosExtra_Patch(IEnumerable<Gizmo> __result, CompPawnStorage __instance)
    {
        foreach (Gizmo g in __result) yield return g;
        if (__instance.innerContainer.Count <= 0) yield break;

        Command_Action action = new()
        {
            defaultLabel = "PS_ReleaseAllToDefensivePositions".Translate(),
            defaultDesc = "PS_ReleaseAllToDefensivePositions_Tooltip".Translate(),
            icon = releaseToDefensivePositionTex.Value,
            action = delegate
            {
                for (int num = __instance.innerContainer.Count - 1; num >= 0; num--)
                {
                    Pawn pawn = (Pawn) __instance.GetAt(num);
                    Utility.ReleasePawn(__instance, pawn, __instance.parent.Position, __instance.parent.Map);

                    PawnSavedPositionHandler mgr = DefensivePositionsManager.Instance.GetHandlerForPawn(pawn);

                    if (hasSavedPositionMethod.Value == null || getPositionMethod.Value == null || draftToPositionMethod.Value == null ||
                        !(bool) hasSavedPositionMethod.Value.Invoke(mgr, [0]))
                    {
                        continue;
                    }

                    IntVec3 pos = (IntVec3) getPositionMethod.Value.Invoke(mgr, [0]);
                    draftToPositionMethod.Value.Invoke(mgr, [pos]);
                    DefensivePositionsManager.Instance.Reporter.ReportPawnInteraction(ScheduledReportManager.ReportType.SentToSavedPosition, pawn, true, 0);
                }
            }
        };
        yield return action;
    }
}
