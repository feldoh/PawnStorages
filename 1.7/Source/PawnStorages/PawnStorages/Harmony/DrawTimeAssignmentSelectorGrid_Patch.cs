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
    private static int? _cachedColumnOffset;

    private static int ColumnOffset()
    {
        if (_cachedColumnOffset.HasValue)
            return _cachedColumnOffset.Value;

        // Vanilla base: count official (core + DLC) TimeAssignmentDefs drawn by the vanilla method.
        // e.g. Anything/Work/Joy/Sleep = 4; Royalty adds Meditate = 5.
        int vanillaBase = DefDatabase<TimeAssignmentDef>.AllDefs.Count(d => d.modContentPack == null || d.modContentPack.IsCoreMod || d.modContentPack.IsOfficialMod);

        // Build a load-order index for all running mods so we can sort by it.
        // Mod load order mirrors Harmony postfix execution order for patches at the same priority,
        // so this implicitly respects the order in which each mod's button patch will run.
        // It also means most collisions can be resolved by adjusting load-afters.
        Dictionary<ModContentPack, int> modLoadIndex = LoadedModManager.RunningMods.Select((mod, idx) => (mod, idx)).ToDictionary(x => x.mod, x => x.idx);

        // Grab the non-core TimeAssignment buttons and order by mod-order
        List<TimeAssignmentDef> customDefs = DefDatabase<TimeAssignmentDef>
            .AllDefs.Where(d => d.modContentPack is { IsCoreMod: false, IsOfficialMod: false })
            .OrderBy(d => modLoadIndex.TryGetValue(d.modContentPack, out int idx) ? idx : int.MaxValue)
            .ThenBy(d => d.defName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int myIndex = customDefs.IndexOf(PS_DefOf.PS_Home);
        if (myIndex < 0)
            myIndex = 0;

        return (_cachedColumnOffset = vanillaBase + myIndex).Value;
    }

    public static void Postfix(Rect rect)
    {
        rect.yMax -= 2f;
        Rect rect2 = rect;
        rect2.xMax = rect2.center.x;
        rect2.yMax = rect2.center.y;
        rect2.x += ColumnOffset() * rect2.width;
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
        if (Mouse.IsOver(rect))
            Widgets.DrawHighlight(rect);
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.white;
        Widgets.Label(rect, ta.LabelCap);
        Text.Anchor = TextAnchor.UpperLeft;
        if (TimeAssignmentSelector.selectedAssignment == ta)
        {
            Widgets.DrawBox(rect, 2);
        }
        else
        {
            UIHighlighter.HighlightOpportunity(rect, ta.cachedHighlightNotSelectedTag);
        }
    }
}
