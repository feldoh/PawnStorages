using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PawnStorages;

[HarmonyPatch(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorGrid")]
public static class DrawTimeAssignmentSelectorGrid_Patch
{
    public static void Postfix(Rect rect)
    {
        rect.yMax -= 2f;
        Rect rect2 = rect;
        rect2.xMax = rect2.center.x;
        rect2.yMax = rect2.center.y;
        rect2.x += 4f * rect2.width;
        if (ModsConfig.RoyaltyActive) rect2.x += rect2.width;
        DrawTimeAssignmentSelectorFor(rect2, PS_DefOf.PS_Home);
    }

    public static void DrawTimeAssignmentSelectorFor(Rect rect, TimeAssignmentDef ta)
    {
        rect = rect.ContractedBy(2f);
        GUI.DrawTexture(rect, ta.ColorTexture);
        if (Widgets.ButtonInvisible(rect))
        {
            TimeAssignmentSelector.selectedAssignment = ta;
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        GUI.color = Color.white;
        if (Mouse.IsOver(rect)) Widgets.DrawHighlight(rect);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.white;
        Widgets.Label(rect, ta.LabelCap);
        Text.Anchor = TextAnchor.UpperLeft;
        if (TimeAssignmentSelector.selectedAssignment == ta)
            Widgets.DrawBox(rect, 2);
        else
            UIHighlighter.HighlightOpportunity(rect, ta.cachedHighlightNotSelectedTag);
    }
}
