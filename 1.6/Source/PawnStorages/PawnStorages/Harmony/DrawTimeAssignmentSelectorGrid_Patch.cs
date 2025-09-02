using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PawnStorages;

[HarmonyPatch(typeof(TimeAssignmentSelector), "DrawTimeAssignmentSelectorGrid")]
public static class DrawTimeAssignmentSelectorGrid_Patch
{
    public static HashSet<string> conflictingMods = new HashSet<string>() { "aoba.exosuit.framework" };

    public static bool HasActiveModWithPackageIdCaseInsensitive(string name)
    {
        foreach (ModMetaData mod in ModLister.mods)
        {
            if (mod.Active && string.Equals(mod.PackageId, name, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }
        return false;
    }

    public static void Postfix(Rect rect)
    {
        rect.yMax -= 2f;
        rect.xMax = rect.center.x;
        rect.yMax = rect.center.y;
        rect.x += 4f * rect.width;
        if (ModsConfig.RoyaltyActive)
            rect.x += rect.width;

        int conflictingModCount = conflictingMods.Count(HasActiveModWithPackageIdCaseInsensitive);
        if (conflictingModCount > 0)
        {
            rect.x += conflictingModCount * rect.width;
        }

        DrawTimeAssignmentSelectorFor(rect, PS_DefOf.PS_Home, "PS_Home_Tooltip".Translate());
    }

    public static void DrawTimeAssignmentSelectorFor(Rect rect, TimeAssignmentDef ta, string tooltip)
    {
        rect = rect.ContractedBy(2f);
        GUI.DrawTexture(rect, ta.ColorTexture);
        if (Widgets.ButtonInvisible(rect))
        {
            TimeAssignmentSelector.selectedAssignment = ta;
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        TooltipHandler.TipRegion(rect, (TipSignal) tooltip);
        GUI.color = Color.white;
        if (Mouse.IsOver(rect))
            Widgets.DrawHighlight(rect);
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
