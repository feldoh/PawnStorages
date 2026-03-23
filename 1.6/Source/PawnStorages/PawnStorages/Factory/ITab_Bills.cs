using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PawnStorages.Factory;

public class ITab_Bills : ITab
{
    private float viewHeight = 1000f;
    private Vector2 scrollPosition;
    private static readonly Vector2 WinSize = new(420f, 480f);

    [TweakValue("Interface", 0.0f, 128f)]
    private static float PasteX = 48f;

    [TweakValue("Interface", 0.0f, 128f)]
    private static float PasteY = 3f;

    [TweakValue("Interface", 0.0f, 32f)]
    private static float PasteSize = 24f;

    protected Building_PSFactory SelFactory => (Building_PSFactory)SelThing;

    public ITab_Bills()
    {
        size = WinSize;
        labelKey = "TabBills";
        tutorTag = "Bills";
    }

    public override void FillTab()
    {
        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.BillsTab, KnowledgeAmount.FrameDisplayed);
        Rect rect = new(WinSize.x - PasteX, PasteY, PasteSize, PasteSize);
        if (BillUtility.Clipboard != null)
        {
            if (
                !SelFactory.AllRecipesUnfiltered.Contains(BillUtility.Clipboard.recipe)
                || !BillUtility.Clipboard.recipe.AvailableNow
                || !BillUtility.Clipboard.recipe.AvailableOnNow(SelFactory)
            )
            {
                GUI.color = Color.gray;
                Widgets.DrawTextureFitted(rect, TexButton.Paste, 1f);
                GUI.color = Color.white;
                if (Mouse.IsOver(rect))
                    TooltipHandler.TipRegion(rect, "ClipboardBillNotAvailableHere".Translate() + ": " + BillUtility.Clipboard.LabelCap);
            }
            else
            {
                if (Widgets.ButtonImageFitted(rect, TexButton.Paste, Color.white))
                {
                    Bill bill = BillUtility.Clipboard.Clone();
                    bill.InitializeAfterClone();
                    SelFactory.BillStack.AddBill(bill);
                    SoundDefOf.Tick_Low.PlayOneShotOnCamera();
                }

                if (Mouse.IsOver(rect))
                    TooltipHandler.TipRegion(rect, "PasteBillTip".Translate() + ": " + BillUtility.Clipboard.LabelCap);
            }
        }

        int windowCountBefore = Find.WindowStack.Count;
        mouseoverBill = SelFactory.BillStack.DoListing(new Rect(0.0f, 0.0f, WinSize.x, WinSize.y).ContractedBy(10f), RecipeOptionsMaker, ref scrollPosition, ref viewHeight);

        // DoListing opened a FloatMenu from our dummy option — remove it, our dialog is already open
        if (Find.WindowStack.Count <= windowCountBefore)
            return;

        Window last = Find.WindowStack.Windows[Find.WindowStack.Count - 1];
        if (last is Verse.FloatMenu)
            Find.WindowStack.TryRemove(last, false);
        return;

        List<FloatMenuOption> RecipeOptionsMaker()
        {
            if (!Find.WindowStack.Windows.Any(w => w is Dialog_AddBill))
                Find.WindowStack.Add(new Dialog_AddBill(SelFactory));
            // Return a single dummy option — DoListing requires a non-null non-empty list
            return [new FloatMenuOption("", null)];
        }
    }

    private Bill mouseoverBill;

    public override void TabUpdate()
    {
        if (mouseoverBill == null)
            return;
        mouseoverBill.TryDrawIngredientSearchRadiusOnMap(SelFactory.Position);
        mouseoverBill = null;
    }
}
