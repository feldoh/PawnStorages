using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PawnStorages.Farm.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PawnStorages.Farm;

public class ITab_Farm : ITab
{
    public ITab_Farm()
    {
        size = ITab_PenAnimals.WinSize;
        labelKey = "PS_FarmTab";
    }

    public IFarmTabParent ParentAsFarmTabParent => SelThing as IFarmTabParent;

    protected float TopAreaHeight => 20f;

    protected const float LineHeight = 20f;
    protected Vector2 ScrollPosition = Vector2.zero;
    protected QuickSearchWidget QuickSearchWidget = new();

    public override void FillTab()
    {
        Rect tabRect = new Rect(0.0f, 0.0f, ITab_Storage.WinSize.x, ITab_Storage.WinSize.y).ContractedBy(10f);
        Widgets.BeginGroup(tabRect);

        Rect menuRect = new Rect(0.0f, 20f, tabRect.width, tabRect.height - 20f);
        Widgets.DrawMenuSection(menuRect);
        float num1 = menuRect.width - 2f;
        Rect buttonRect = new Rect(menuRect.x + 3f, menuRect.y + 3f, (float)(num1 / 2.0 - 3.0 - 1.5), 24f);
        if (Widgets.ButtonText(buttonRect, "ClearAll".Translate()))
        {
            ParentAsFarmTabParent.DenyAll();
            SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
        }

        if (Widgets.ButtonText(new Rect(buttonRect.xMax + 3f, buttonRect.y, buttonRect.width, 24f), "AllowAll".Translate()))
        {
            ParentAsFarmTabParent.AllowAll();
            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
        }

        Rect searchWidgetRect = new(menuRect.x + 3f, menuRect.yMin + 26f, (float)(tabRect.width - 16.0 - 6.0), 24f);
        QuickSearchWidget.OnGUI(searchWidgetRect);

        float totalHeight = ParentAsFarmTabParent.AllowableThing.Count * (LineHeight + 2f);

        Rect viewRect = new(0.0f, 0.0f, menuRect.width - 20f, totalHeight);

        menuRect.yMin += 52f;
        menuRect.yMax -= 6f;
        menuRect.xMax -= 4f;

        Widgets.BeginScrollView(menuRect, ref ScrollPosition, viewRect);

        // alternate = false;
        float num = 0.0f;

        List<ThingDef> allowable;
        if (QuickSearchWidget.filter.Active)
        {
            allowable = ParentAsFarmTabParent.AllowableThing.Where(tDef => tDef.LabelCap.ToString().ToLower().Contains(QuickSearchWidget.filter.Text.ToLower())).ToList();
        }
        else
        {
            allowable = ParentAsFarmTabParent.AllowableThing;
        }

        allowable.Sort(ThingDefLabelCapComparer.ThingLabelIgnoreCaseComparator);

        foreach (ThingDef tDef in allowable)
        {
            float iconWidth = 20f;
            Widgets.DefIcon(new Rect(5f, num, iconWidth, LineHeight), tDef, drawPlaceholder: true);

            float labelX = iconWidth + 2f + 5f;
            Rect labelLeft = new(labelX, num, viewRect.width - 26f - labelX - 5f, LineHeight);

            Widgets.DrawHighlightIfMouseover(labelLeft);
            if (!tDef.DescriptionDetailed.NullOrEmpty())
            {
                if (Mouse.IsOver(labelLeft))
                    GUI.DrawTexture(labelLeft, TexUI.HighlightTex);
                TooltipHandler.TipRegion(labelLeft, (TipSignal)tDef.DescriptionDetailed);
            }

            string label = tDef.LabelCap;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.white;

            Widgets.Label(labelLeft, label.Truncate(labelLeft.width));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            bool checkOn = ParentAsFarmTabParent.AllowedThings[tDef];
            bool flag = checkOn;

            Widgets.Checkbox(new Vector2(labelLeft.xMax, num), ref flag, LineHeight, paintable: true);
            if (checkOn != flag)
                ParentAsFarmTabParent.AllowedThings[tDef] = flag;

            num += LineHeight + 2f;
        }

        Widgets.EndScrollView();

        Widgets.EndGroup();
    }
}

public class ThingDefLabelCapComparer : IComparer<ThingDef>
{
    public int Compare(ThingDef x, ThingDef y)
    {
        return x switch
        {
            // Handle null cases first
            null when y == null => 0,
            null => -1,
            _
                => y == null
                    ? 1
                    :
                    // Compare based on LabelCap
                    string.Compare(x.LabelCap, y.LabelCap, StringComparison.InvariantCultureIgnoreCase)
        };
    }

    public static ThingDefLabelCapComparer ThingLabelIgnoreCaseComparator { get; } = new();
}
