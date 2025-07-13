using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.TickedStorage;

public class ITab_PawnList : ITab
{
    public ITab_PawnList()
    {
        size = ITab_PenAnimals.WinSize;
        labelKey = "PS_PawnListTab";
    }

    IPawnListParent ParentAsPawnListParent => SelThing as IPawnListParent;

    private static readonly Vector2 WinSize = new(300f, 480f);
    public readonly ThingFilterUI.UIState ThingFilterState = new();
    public const float LineHeight = 60f;
    private bool alternate;

    public bool DrawLine(float position, float width, Pawn pawn)
    {
        Rect rect = new Rect(0.0f, position, width, LineHeight);

        if (alternate)
        {
            Widgets.DrawRectFast(rect, new Color(1f, 1f, 1f, ITab_Pawn_Log_Utility.AlternateAlpha));
        }

        alternate = !alternate;

        Widgets.ThingIcon(new Rect(5f, position + 7.5f, 45f, 45f), pawn);

        if (ParentAsPawnListParent?.NeedsDrop() ?? false)
        {
            Widgets.Label(new Rect(55f, position, width - 90f, 20f),
                (pawn.needs?.food?.Starving ?? false ? "PS_FarmTab_NameStarving" : "PS_FarmTab_Name").Translate(pawn.LabelShort));
            Widgets.Label(new Rect(55f, position + 20f, width - 90f, 20f),
                "PS_FarmTab_Nutrition".Translate((pawn.needs?.food?.CurLevelPercentage ?? 0f).ToStringPercent()));
        }
        else
        {
            Widgets.Label(new Rect(55f, position, width - 90f, 20f),
                ("PS_FarmTab_Name").Translate(pawn.LabelShort));
        }

        Rect btn = new Rect(new Vector2(width - 50f, position + 15f), new Vector2(30f, 30f));

        return Widgets.ButtonImage(btn, TexButton.Drop, Color.white, GenUI.MouseoverColor);
    }

    public override void FillTab()
    {
        if ((ParentAsPawnListParent?.GetDirectlyHeldThings()?.Count ?? 0) <= 0) return;

        Widgets.Label(new Rect(5.0f, 0.0f, WinSize.x, 30f), "PS_PawnListTab".Translate());

        Rect tabRect = new Rect(0.0f, 30.0f, WinSize.x, WinSize.y - 30f).ContractedBy(10f);
        Rect scrollViewRect = new Rect(tabRect);

        float totalHeight = ParentAsPawnListParent.GetDirectlyHeldThings().Count * LineHeight;

        Rect viewRect = new Rect(0.0f, 0.0f, scrollViewRect.width, totalHeight);

        Widgets.AdjustRectsForScrollView(tabRect, ref scrollViewRect, ref viewRect);
        Widgets.BeginScrollView(scrollViewRect, ref ThingFilterState.scrollPosition, viewRect);

        alternate = false;
        float num = 0.0f;
        List<Pawn> removed = [];
        foreach (Thing thing in ParentAsPawnListParent.GetDirectlyHeldThings())
        {
            Pawn pawn = (Pawn) thing;
            if (DrawLine(num, scrollViewRect.width, pawn))
            {
                removed.Add(pawn);
            }

            num += LineHeight;
        }

        foreach (Pawn pawn in removed)
        {
            ParentAsPawnListParent.ReleasePawn(pawn);
        }

        Widgets.EndScrollView();
    }
}
