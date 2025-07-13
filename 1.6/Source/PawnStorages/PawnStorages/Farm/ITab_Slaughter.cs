using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using PawnStorages.Farm.Comps;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Color = UnityEngine.Color;

namespace PawnStorages.Farm;

public class ITab_Slaughter : ITab
{
    private static readonly Vector2 WinSize = new(940f, 500f);
    public readonly ThingFilterUI.UIState ThingFilterState = new();
    private Vector2 scrollPos;
    private Rect viewRect;
    private List<Rect> tmpMouseoverHighlightRects = [];
    private List<Rect> tmpGroupRects = [];
    private QuickSearchWidget QuickSearchWidget = new();

    public const float LineHeight = 24f;
    public const float CurrWidth = 60f;
    public const float MaxWidth = 84f;
    public const float LabelWidth = 124f;
    public const float GapWidth = 4f;

    public CompFarmStorage compFarmStorage => SelThing.TryGetComp<CompFarmStorage>();
    public CompFarmBreeder compFarmBreeder => SelThing.TryGetComp<CompFarmBreeder>();

    public Dictionary<PawnKindDef,AutoSlaughterConfig> AutoSlaughterSettings => compFarmBreeder.GetOrPopulateAutoSlaughterSettings();
    public Dictionary<PawnKindDef,AutoSlaughterCullOrder> CullOrder => compFarmBreeder.GetOrPopulateAutoSlaughterCullOrder();

    private List<PawnKindDef> _orderedSettingsCache;

    public override void OnOpen()
    {
        _orderedSettingsCache = AutoSlaughterSettings
            .OrderByDescending(c => compFarmStorage.CountForType(c.Value.animal).total)
            .ThenBy(c => c.Value.animal.label)
            .Select(s => s.Key)
            .ToList();
    }

    public ITab_Slaughter()
    {
        size = WinSize;
        labelKey = "PS_SlaughterTab";
    }

    public void DoMaxColumn(WidgetRow row, ref int val, ref string buffer, int current)
    {
        if (val == -1)
        {
            row.Gap(-GapWidth);
            if (row.ButtonIconWithBG(TexButton.Infinity, 48f, "AutoSlaughterTooltipSetLimit".Translate()))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                val = current;
            }

            row.Gap(-GapWidth);
        }
        else
        {
            row.CellGap = 0.0f;
            row.Gap(-GapWidth);
            row.TextFieldNumeric<int>(ref val, ref buffer, 40f);
            val = Mathf.Max(0, val);
            if (row.ButtonIcon(TexButton.CloseXSmall, mouseoverColor: Color.white, overrideSize: 16f))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                val = -1;
                buffer = null;
            }

            row.CellGap = GapWidth;
            row.Gap(-GapWidth);
        }
    }

    public void DoAgeOrderColumn(WidgetRow row, ref bool ascending, int val)
    {
        Texture2D tex = ascending ? PSContent.PS_ArrowUp : PSContent.PS_ArrowDown;
        row.Gap(val == -1 ? GapWidth : GapWidth*2 + 4);
        if (row.ButtonIconWithBG(tex, 8f, "PS_AutoSlaughterTooltipDirection".Translate()))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            ascending = !ascending;
        }

        row.Gap(-GapWidth);
    }

    public void DrawLine(Rect rowRect, AutoSlaughterConfig config, int index, PawnKindDef kind)
    {
        if (index % 2 == 1)
            Widgets.DrawLightHighlight(rowRect);
        Color color = GUI.color;
        Dialog_AutoSlaughter.AnimalCountRecord animalCount = compFarmStorage.CountForType(config.animal);
        AutoSlaughterCullOrder order = CullOrder[kind];

        Widgets.BeginGroup(rowRect);
        WidgetRow row = new WidgetRow(0.0f, 0.0f);
        row.DefIcon(config.animal);
        row.Gap(GapWidth);
        GUI.color = animalCount.total == 0 ? Color.gray : color;
        row.Label( config.animal.LabelCap.Truncate(LabelWidth), LabelWidth, GetTipForAnimal());
        GUI.color = color;
        DrawCurrentCol(animalCount.total, config.maxTotal);
        DoMaxColumn(row, ref config.maxTotal, ref config.uiMaxTotalBuffer, animalCount.total);
        DoAgeOrderColumn(row, ref order.AllAscending, config.maxTotal);

        DrawCurrentCol(animalCount.male, config.maxMales);
        DoMaxColumn(row, ref config.maxMales, ref config.uiMaxMalesBuffer, animalCount.male);
        DoAgeOrderColumn(row, ref order.AdultMaleAscending, config.maxMales);

        DrawCurrentCol(animalCount.maleYoung, config.maxMalesYoung);
        DoMaxColumn(row, ref config.maxMalesYoung, ref config.uiMaxMalesYoungBuffer, animalCount.maleYoung);
        DoAgeOrderColumn(row, ref order.ChildMaleAscending, config.maxMalesYoung);

        DrawCurrentCol(animalCount.female, config.maxFemales);
        DoMaxColumn(row, ref config.maxFemales, ref config.uiMaxFemalesBuffer, animalCount.female);
        DoAgeOrderColumn(row, ref order.AdultFemaleAscending, config.maxFemales);

        DrawCurrentCol(animalCount.femaleYoung, config.maxFemalesYoung);
        DoMaxColumn(row, ref config.maxFemalesYoung, ref config.uiMaxFemalesYoungBuffer, animalCount.femaleYoung);
        DoAgeOrderColumn(row, ref order.ChildFemaleAscending, config.maxFemalesYoung);

        Widgets.EndGroup();
        return;

        void DrawCurrentCol(int val, int? limit)
        {
            Color colColour = Color.white;

            if (val == 0)
                colColour = (Color.gray);
            else if (val > limit)
            {
                colColour = ColorLibrary.RedReadable;
            }

            Color startColor = GUI.color;
            int anchor = (int) Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = colColour;
            row.Label(val.ToString(), 56f);
            Text.Anchor = (TextAnchor) anchor;
            GUI.color = startColor;
        }

        string DevTipPartForPawn(Pawn pawn)
        {
            string label = pawn.LabelShortCap + " " + pawn.gender.GetLabel() + " (" + pawn.ageTracker.AgeBiologicalYears + "y)";
            Hediff firstHediffOfDef = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Pregnant);
            if (firstHediffOfDef != null)
                label = label + ", pregnant (" + firstHediffOfDef.Severity.ToStringPercent() + ")";
            return label;
        }

        string GetTipForAnimal()
        {
            TaggedString labelCap = config.animal.LabelCap;
            if (Prefs.DevMode)
                labelCap += "\n\nDEV: Animals to slaughter:\n" + compFarmBreeder.ParentAsBreederParent.Map.autoSlaughterManager.AnimalsToSlaughter
                    .Where(x => x.def == config.animal)
                    .Select(DevTipPartForPawn).ToLineList("  - ");
            return labelCap;
        }
    }

    private void DoAnimalHeader(Rect rect1, Rect rect2)
    {
        Widgets.BeginGroup(new Rect(rect1.x, rect1.y, rect1.width, (float) (rect1.height + (double) rect2.height + 1.0)));
        int num = 0;
        foreach (Rect tmpGroupRect in tmpGroupRects)
        {
            if (num % 2 == 1)
            {
                Widgets.DrawLightHighlight(tmpGroupRect);
                Widgets.DrawLightHighlight(tmpGroupRect);
            }
            else
                Widgets.DrawLightHighlight(tmpGroupRect);

            GUI.color = Color.gray;
            if (num > 0)
                Widgets.DrawLineVertical(tmpGroupRect.xMin, 0.0f, (float) ( rect1.height + (double) rect2.height + 1.0));
            if (num < tmpGroupRects.Count - 1)
                Widgets.DrawLineVertical(tmpGroupRect.xMax, 0.0f, (float) ( rect1.height + (double) rect2.height + 1.0));
            GUI.color = Color.white;
            ++num;
        }

        foreach (Rect mouseoverHighlightRect in tmpMouseoverHighlightRects)
            Widgets.DrawHighlightIfMouseover(mouseoverHighlightRect);
        Widgets.EndGroup();
        tmpMouseoverHighlightRects.Clear();
        tmpGroupRects.Clear();
        Widgets.BeginGroup(rect1);
        WidgetRow row = new WidgetRow(0.0f, 0.0f);
        int anchor1 = (int) Text.Anchor;
        Text.Anchor = TextAnchor.MiddleCenter;
        row.Label(string.Empty, 24f);
        float startX = row.FinalX;
        row.Label(string.Empty, LabelWidth,  "AutoSlaugtherHeaderTooltipLabel".Translate());
        Rect rect = new Rect(startX, rect1.height, row.FinalX - startX, rect2.height);
        tmpMouseoverHighlightRects.Add(rect);
        tmpGroupRects.Add(rect);
        AddCurrentAndMaxEntries("AutoSlaugtherHeaderColTotal", 0.0f, 0.0f);
        AddCurrentAndMaxEntries("AnimalMaleAdult", 0.0f, 0.0f);
        AddCurrentAndMaxEntries("AnimalMaleYoung", 0.0f, 0.0f);
        AddCurrentAndMaxEntries("AnimalFemaleAdult", 0.0f, 0.0f);
        AddCurrentAndMaxEntries("AnimalFemaleYoung", 0.0f, 0.0f);
        Text.Anchor = (TextAnchor) anchor1;
        Widgets.EndGroup();
        Widgets.BeginGroup(rect2);
        WidgetRow widgetRow = new (0.0f, 0.0f);
        TextAnchor anchor2 = Text.Anchor;
        Text.Anchor = TextAnchor.MiddleCenter;
        widgetRow.Label(string.Empty, 24f);
        widgetRow.Label( "AutoSlaugtherHeaderColLabel".Translate(), LabelWidth,  "AutoSlaugtherHeaderTooltipLabel".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColCurrent".Translate(), CurrWidth,  "AutoSlaugtherHeaderTooltipCurrentTotal".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColMax".Translate(), MaxWidth,  "AutoSlaugtherHeaderTooltipMaxTotal".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColCurrent".Translate(), CurrWidth,  "AutoSlaugtherHeaderTooltipCurrentMales".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColMax".Translate(), MaxWidth,  "AutoSlaugtherHeaderTooltipMaxMales".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColCurrent".Translate(), CurrWidth,  "AutoSlaughterHeaderTooltipCurrentMalesYoung".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColMax".Translate(), MaxWidth,  "AutoSlaughterHeaderTooltipMaxMalesYoung".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColCurrent".Translate(), CurrWidth,  "AutoSlaugtherHeaderTooltipCurrentFemales".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColMax".Translate(), MaxWidth,  "AutoSlaugtherHeaderTooltipMaxFemales".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColCurrent".Translate(), CurrWidth,  "AutoSlaugtherHeaderTooltipCurrentFemalesYoung".Translate());
        widgetRow.Label( "AutoSlaugtherHeaderColMax".Translate(), MaxWidth,  "AutoSlaughterHeaderTooltipMaxFemalesYoung".Translate());
        Text.Anchor = anchor2;
        Widgets.EndGroup();
        GUI.color = Color.gray;
        Widgets.DrawLineHorizontal(rect2.x, (float) ( rect2.y + (double) rect2.height + 1.0), rect2.width);
        GUI.color = Color.white;
        return;

        void AddCurrentAndMaxEntries(string headerKey, float extraWidthFirst, float extraWidthSecond)
        {
            startX = row.FinalX;
            row.Label(string.Empty, 60f + extraWidthFirst);
            tmpMouseoverHighlightRects.Add(new Rect(startX, rect1.height, row.FinalX - startX, rect2.height));
            float finalX = row.FinalX;
            row.Label(string.Empty, 84f + extraWidthSecond);
            tmpMouseoverHighlightRects.Add(new Rect(finalX, rect1.height, row.FinalX - finalX, rect2.height));
            Rect lblRect = new (startX, 0.0f, row.FinalX - startX, rect2.height);
            Widgets.Label(lblRect, headerKey.Translate());
            tmpGroupRects.Add(lblRect);
        }
    }

    public override void FillTab()
    {
        if (compFarmBreeder == null) return;

        Widgets.Label(new Rect(5.0f, 0.0f, WinSize.x, 30f), "PS_BreedingTab_TopLabel".Translate());

        Rect searchRect = new(0.0f, 30.0f, WinSize.x - 10, 25f);
        QuickSearchWidget.OnGUI(searchRect);
        Rect tabRect = new Rect(0.0f, 55.0f, WinSize.x, WinSize.y - 55f).ContractedBy(10f);

        List<PawnKindDef> filteredKinds = QuickSearchWidget.filter.Active
            ? _orderedSettingsCache.Where(kind => QuickSearchWidget.filter.Matches(kind.LabelCap.ToString().ToLower())).ToList()
            : _orderedSettingsCache;

        Rect insetRect = new (tabRect);
        insetRect.yMax -= Window.CloseButSize.y;
        insetRect.yMin += 8f;
        Listing_Standard listingStandard = new (insetRect,  () => scrollPos) { ColumnWidth = (float) ( tabRect.width - 16.0 - 4.0) };
        viewRect = new Rect(0.0f, 0.0f, insetRect.width - 16f, 30f * (filteredKinds.Count + 2));
        Rect other = insetRect with { x = scrollPos.x, y = scrollPos.y };
        Widgets.BeginScrollView(insetRect, ref scrollPos, viewRect);
        listingStandard.Begin(viewRect);
        DoAnimalHeader(listingStandard.GetRect(LineHeight), listingStandard.GetRect(LineHeight));
        listingStandard.Gap(6f);

        int row = 0;
        foreach (PawnKindDef kind in filteredKinds)
        {
            Rect rowRect = listingStandard.GetRect(LineHeight);
            if (AutoSlaughterSettings.TryGetValue(kind) is {} autoSlaughterConfig && (rowRect.Overlaps(other) || row == 0))
                DrawLine(rowRect, autoSlaughterConfig, row++, kind);
            listingStandard.Gap(6f);
        }
        listingStandard.End();

        Widgets.EndScrollView();
    }
}
