using System;
using PawnStorages.Farm.Comps;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PawnStorages.Farm;

public class Dialog_KindCullDetails : Window
{
    private readonly CompFarmBreeder breeder;
    private readonly PawnKindDef kind;

    public override Vector2 InitialSize => new(440f, 300f);

    public Dialog_KindCullDetails(CompFarmBreeder breeder, PawnKindDef kind)
    {
        this.breeder = breeder;
        this.kind = kind;
        forcePause = false;
        doCloseButton = true;
        doCloseX = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (kind == null || breeder == null)
        {
            Close();
            return;
        }

        AutoSlaughterMinimums mins = breeder.GetOrPopulateAutoSlaughterMinimums()[kind];
        AutoSlaughterConfig config = breeder.GetOrPopulateAutoSlaughterSettings().TryGetValue(kind);

        Rect contentRect = inRect;
        contentRect.yMax -= Window.CloseButSize.y + 4f;

        Listing_Standard listing = new();
        listing.Begin(contentRect);

        Text.Font = GameFont.Medium;
        listing.Label("PS_KindCullDetails_Title".Translate(kind.LabelCap));
        Text.Font = GameFont.Small;
        listing.GapLine();

        listing.Label("PS_KindCullDetails_KeepAtLeast".Translate());
        DrawMinRow(listing, "PS_KindCullDetails_AdultMales".Translate(), ref mins.MinMales, config?.maxMales ?? -1);
        DrawMinRow(listing, "PS_KindCullDetails_AdultFemales".Translate(), ref mins.MinFemales, config?.maxFemales ?? -1);
        DrawMinRow(listing, "PS_KindCullDetails_YoungMales".Translate(), ref mins.MinMalesYoung, config?.maxMalesYoung ?? -1);
        DrawMinRow(listing, "PS_KindCullDetails_YoungFemales".Translate(), ref mins.MinFemalesYoung, config?.maxFemalesYoung ?? -1);

        listing.GapLine();

        if (config != null && AnyMinExceedsMax(mins, config))
        {
            Color savedColor = GUI.color;
            GUI.color = Color.yellow;
            listing.Label("PS_KindCullDetails_MinExceedsMax".Translate());
            GUI.color = savedColor;
        }

        Color startColor = GUI.color;
        GameFont savedFont = Text.Font;
        GUI.color = Color.gray;
        Text.Font = GameFont.Tiny;
        listing.Label("PS_KindCullDetails_Footnote".Translate());
        Text.Font = savedFont;
        GUI.color = startColor;

        listing.End();
    }

    private static void DrawMinRow(Listing_Standard listing, string label, ref int val, int maxValue)
    {
        Rect rowRect = listing.GetRect(28f);

        Rect labelRect = new(rowRect.x, rowRect.y + 2f, 220f, rowRect.height - 4f);
        Widgets.Label(labelRect, label);

        float xRight = rowRect.x + 230f;
        Rect minus = new(xRight, rowRect.y + 2f, 26f, 24f);
        Rect numRect = new(xRight + 30f, rowRect.y, 50f, 28f);
        Rect plus = new(xRight + 84f, rowRect.y + 2f, 26f, 24f);

        if (Widgets.ButtonText(minus, "-"))
        {
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            val = Math.Max(0, val - GenUI.CurrentAdjustmentMultiplier());
        }

        Color savedColor = GUI.color;
        if (maxValue >= 0 && val > maxValue)
            GUI.color = Color.yellow;
        TextAnchor savedAnchor = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(numRect, val.ToString());
        Text.Anchor = savedAnchor;
        GUI.color = savedColor;

        if (Widgets.ButtonText(plus, "+"))
        {
            SoundDefOf.DragSlider.PlayOneShotOnCamera();
            val += GenUI.CurrentAdjustmentMultiplier();
        }

        listing.Gap(2f);
    }

    private static bool AnyMinExceedsMax(AutoSlaughterMinimums mins, AutoSlaughterConfig cfg)
    {
        return (cfg.maxMales > 0 && mins.MinMales > cfg.maxMales)
            || (cfg.maxFemales > 0 && mins.MinFemales > cfg.maxFemales)
            || (cfg.maxMalesYoung > 0 && mins.MinMalesYoung > cfg.maxMalesYoung)
            || (cfg.maxFemalesYoung > 0 && mins.MinFemalesYoung > cfg.maxFemalesYoung);
    }
}
