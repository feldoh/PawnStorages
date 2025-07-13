using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages;

//This is a redundant way for a very very specific way to do recipes, making the bench a normal workbench with a bills tab is a lot more intuitive
[Obsolete("Redundant way for a very very specific way to do recipes")] //For now
public class CompRightClickToWorkOnBills : ThingComp
{
    public IBillGiver BillGiver => parent as IBillGiver;
    public CompProperties_RightClickToWorkOnBills Props => props as CompProperties_RightClickToWorkOnBills;

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        foreach (FloatMenuOption f in base.CompFloatMenuOptions(selPawn)) yield return f;

        foreach (RecipeDef recipe in Props.recipeToCallRightClick)
            yield return new FloatMenuOption("PS_WorkOn".Translate(recipe.label), delegate
            {
                if (!BillGiver.BillStack.Bills.Any(x => x.recipe == recipe)) BillGiver.BillStack.AddBill(recipe.MakeNewBill());
                Bill_Production bill = BillGiver.BillStack.bills.First(x => x.recipe == recipe) as Bill_Production;
                if (BillGiver.BillStack.bills.First() != bill)
                {
                    BillGiver.BillStack.bills.Remove(bill);
                    BillGiver.BillStack.bills.Insert(0, bill);
                }

                if (bill.targetCount == 0) bill.targetCount = 1;
                WorkGiver_DoBill workGiver = new WorkGiver_DoBill();
                workGiver.def = PS_DefOf.PS_DoBillsDigitalBench;
                Job job = workGiver.JobOnThing(selPawn, parent);
                if (job != null) selPawn.jobs.TryTakeOrderedJob(job);
            });
    }
}
