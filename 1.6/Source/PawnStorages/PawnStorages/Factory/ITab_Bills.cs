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
    [TweakValue("Interface", 0.0f, 128f)] private static float PasteX = 48f;
    [TweakValue("Interface", 0.0f, 128f)] private static float PasteY = 3f;
    [TweakValue("Interface", 0.0f, 32f)] private static float PasteSize = 24f;

    protected Building_PSFactory SelFactory => (Building_PSFactory) SelThing;

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
            if (!SelFactory.AllRecipesUnfiltered.Contains(BillUtility.Clipboard.recipe) || !BillUtility.Clipboard.recipe.AvailableNow ||
                !BillUtility.Clipboard.recipe.AvailableOnNow(SelFactory))
            {
                GUI.color = Color.gray;
                Widgets.DrawTextureFitted(rect, TexButton.Paste, 1f);
                GUI.color = Color.white;
                if (Mouse.IsOver(rect))
                    TooltipHandler.TipRegion(rect, "ClipboardBillNotAvailableHere".Translate() + ": " + BillUtility.Clipboard.LabelCap);
            }
            else if (SelFactory.BillStack.Count >= 15)
            {
                GUI.color = Color.gray;
                Widgets.DrawTextureFitted(rect, TexButton.Paste, 1f);
                GUI.color = Color.white;
                if (Mouse.IsOver(rect))
                    TooltipHandler.TipRegion(rect, "PasteBillTip".Translate() + " (" + "PasteBillTip_LimitReached".Translate() + "): " + BillUtility.Clipboard.LabelCap);
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

        mouseoverBill = SelFactory.BillStack.DoListing(new Rect(0.0f, 0.0f, WinSize.x, WinSize.y).ContractedBy(10f), RecipeOptionsMaker, ref scrollPosition, ref viewHeight);
        return;

        List<FloatMenuOption> RecipeOptionsMaker()
        {
            List<FloatMenuOption> list = SelFactory.AllRecipesUnfiltered.Where(recipeDef => recipeDef.AvailableNow)
                .Select(recipe => new FloatMenuOption(recipe.LabelCap, delegate
                    {
                        Bill bill2 = recipe.MakeNewBill();
                        SelFactory.BillStack.AddBill(bill2);
                        if (recipe.conceptLearned != null) PlayerKnowledgeDatabase.KnowledgeDemonstrated(recipe.conceptLearned, KnowledgeAmount.Total);
                        if (TutorSystem.TutorialMode) TutorSystem.Notify_Event((EventPack) "AddBill-" + recipe.LabelCap);
                    }, MenuOptionPriority.Default, null, null, 29f,
                    billOptionRect => Widgets.InfoCardButton(billOptionRect.x + 5f, billOptionRect.y + (billOptionRect.height - 24f) / 2f, recipe), null))
                .ToList();
            return list.Any() ? list : [new FloatMenuOption("NoneBrackets".Translate(), null, MenuOptionPriority.Default, null, null, 0f, null, null)];
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
