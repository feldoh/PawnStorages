using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PawnStorages.Factory;

public class Dialog_AddBill : Window
{
    private readonly Building_PSFactory _factory;
    private readonly QuickSearchWidget _searchWidget = new();
    private Vector2 _scrollPosition;
    private readonly List<RecipeDef> _allRecipes;

    private const float TitleHeight = 32f;
    private const float GapAfterTitle = 4f;
    private const float SearchHeight = 24f;
    private const float GapAfterSearch = 8f;
    private const float ScrollStart = TitleHeight + GapAfterTitle + SearchHeight + GapAfterSearch; // 68f
    private const float RowHeight = 28f;
    private const float RowPadding = 2f;
    private const float IconSize = 24f;
    private const float InfoButtonSize = 24f;
    private const float Padding = 4f;

    private static readonly Color HeaderTextColor = new(0.9f, 0.8f, 0.5f, 1f);

    public override Vector2 InitialSize => new(480f, 600f);

    public Dialog_AddBill(Building_PSFactory factory)
    {
        _factory = factory;
        doCloseButton = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = false;
        draggable = true;
        _allRecipes = factory.AllRecipesUnfiltered.OrderBy(r => r.LabelCap.ToString()).ToList();
    }

    public override void WindowUpdate()
    {
        base.WindowUpdate();
        if (!_factory.Spawned)
            Close();
    }

    public override void DoWindowContents(Rect inRect)
    {
        // Title row: label left, bill count right
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, TitleHeight), "PS_AddBill_Title".Translate());
        Text.Font = GameFont.Small;

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = Color.gray;
        Widgets.Label(new Rect(inRect.x, inRect.y + 8f, inRect.width, TitleHeight - 8f), "PS_AddBill_BillCount".Translate(_factory.BillStack.Count));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        // Search bar
        Rect searchRect = new(inRect.x, inRect.y + TitleHeight + GapAfterTitle, inRect.width, SearchHeight);
        _searchWidget.OnGUI(searchRect);

        // Scroll area
        Rect scrollOuter = new(inRect.x, inRect.y + ScrollStart, inRect.width, inRect.yMax - (inRect.y + ScrollStart) - CloseButSize.y - 4f);

        List<RecipeDef> displayList;
        if (_searchWidget.filter.Active)
        {
            string searchText = _searchWidget.filter.Text.ToLowerInvariant();
            displayList = _allRecipes.Where(r => r.LabelCap.ToString().ToLowerInvariant().Contains(searchText)).ToList();
        }
        else
        {
            displayList = _allRecipes;
        }

        float contentHeight = Mathf.Max(displayList.Count * (RowHeight + RowPadding) + 20f, scrollOuter.height);
        Rect scrollView = new(0f, 0f, scrollOuter.width - 16f, contentHeight);
        Widgets.BeginScrollView(scrollOuter, ref _scrollPosition, scrollView);

        if (displayList.Count == 0)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, 0f, scrollView.width, RowHeight), "PS_AddBill_NoResults".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        else
        {
            float curY = 0f;
            for (int i = 0; i < displayList.Count; i++)
            {
                DrawRecipeRow(displayList[i], curY, scrollView.width, i);
                curY += RowHeight + RowPadding;
            }
        }

        Widgets.EndScrollView();
    }

    private void DrawRecipeRow(RecipeDef recipe, float curY, float width, int rowIndex)
    {
        Rect rowRect = new(0f, curY, width, RowHeight);

        if (rowIndex % 2 == 0)
            Widgets.DrawLightHighlight(rowRect);
        Widgets.DrawHighlightIfMouseover(rowRect);

        // Icon
        ThingDef iconDef = recipe.ProducedThingDef;
        Rect iconRect = new(Padding, curY + (RowHeight - IconSize) / 2f, IconSize, IconSize);
        if (iconDef != null)
            Widgets.DefIcon(iconRect, iconDef, drawPlaceholder: true);

        // Info card button (right-aligned)
        float infoX = width - InfoButtonSize - Padding;
        Rect infoRect = new(infoX, curY + (RowHeight - InfoButtonSize) / 2f, InfoButtonSize, InfoButtonSize);
        Widgets.InfoCardButton(infoRect.x, infoRect.y, recipe);

        // Label
        Rect labelRect = new(iconRect.xMax + Padding, curY, infoX - iconRect.xMax - Padding * 2f, RowHeight);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, recipe.LabelCap);
        Text.Anchor = TextAnchor.UpperLeft;

        // Click row to add bill (exclude info button area)
        Rect clickRect = new(rowRect.x, rowRect.y, infoX - rowRect.x, rowRect.height);
        if (Widgets.ButtonInvisible(clickRect))
            AddBill(recipe);
    }

    private void AddBill(RecipeDef recipe)
    {
        Bill bill = recipe.MakeNewBill();
        _factory.BillStack.AddBill(bill);
        SoundDefOf.Click.PlayOneShotOnCamera();

        if (recipe.conceptLearned != null)
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(recipe.conceptLearned, KnowledgeAmount.Total);
        if (TutorSystem.TutorialMode)
            TutorSystem.Notify_Event((EventPack)"AddBill-" + recipe.LabelCap);
    }
}
