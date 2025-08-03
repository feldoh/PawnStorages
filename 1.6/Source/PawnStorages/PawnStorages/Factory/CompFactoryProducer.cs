using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Factory;

public class CompFactoryProducer : CompPawnStorageProducer
{
    public float storedWork = 0f;
    public Bill currentBill;
    public bool shouldBeActive = true;

    private List<IntVec3> cachedAdjCellsCardinal;

    public List<IntVec3> AdjCellsCardinalInBounds => cachedAdjCellsCardinal ??= GenAdj.CellsAdjacentCardinal(parent).Where(c => c.InBounds(parent.Map)).ToList();

    public void Notify_BillDeleted(Bill bill)
    {
        if (CurrentBill == bill)
            CurrentBill = null;
    }

    public Bill CurrentBill
    {
        get => currentBill;
        set => currentBill = value;
    }

    public override void CompTick()
    {
        base.CompTick();

        if (!shouldBeActive || !PawnStoragesMod.settings.AllowNeedsDrop)
            return;

        if (
            parent.IsHashIntervalTick(ParentAsProductionParent.BuildingTickInterval)
            && ParentAsProductionParent.ProducingPawns is { } parentProducingPawns
            && parentProducingPawns.Any()
        )
        {
            if (CurrentBill == null)
                TryPickNextBill();
            if (CurrentBill != null)
                storedWork += parentProducingPawns.Count * ParentAsProductionParent.BuildingTickInterval;
            float workAmount = CurrentBill?.GetWorkAmount() ?? 0f;
            while (CurrentBill != null && storedWork > workAmount && TryFinishBill(CurrentBill, BillForeman(parentProducingPawns)))
            {
                storedWork -= workAmount;
                if (!CurrentBill.ShouldDoNow())
                    TryPickNextBill();
            }
        }

        if (
            !ProduceNow
            && (!parent.IsHashIntervalTick(60000 / Math.Max(PawnStoragesMod.settings.ProductionsPerDay, 1)) || DaysProduce.Count <= 0 || !ParentAsProductionParent.IsActive)
        )
            return;
        List<Thing> failedToPlace = [];
        failedToPlace.AddRange(DaysProduce.Where(thing => !GenPlace.TryPlaceThing(thing, parent.Position, parent.Map, ThingPlaceMode.Near)));
        DaysProduce.Clear();
        DaysProduce.AddRange(failedToPlace);
    }

    public bool TryFinishBill(Bill bill, Pawn billForeman)
    {
        List<Thing> chosenIngredients = SelectedIngredientsFor(bill)?.Select(pair => pair.Key.SplitOff(pair.Value)).ToList() ?? [];
        if (chosenIngredients.Count == 0)
        {
            if (TryPickNextBill() is { } newBill && storedWork >= newBill.GetWorkAmount())
            {
                bill = newBill;
                chosenIngredients = SelectedIngredientsFor(bill)?.Select(pair => pair.Key.SplitOff(pair.Value)).ToList() ?? [];
            }
        }

        if (chosenIngredients.Count == 0)
            return false;

        DaysProduce.AddRange(GenRecipe.MakeRecipeProducts(bill.recipe, billForeman, chosenIngredients, CalculateDominantIngredient(chosenIngredients, bill.recipe), ParentFactory));
        bill.Notify_IterationCompleted(billForeman, chosenIngredients);
        ConsumeIngredients(chosenIngredients, bill.recipe, parent.Map);
        return true;
    }

    public Building_PSFactory ParentFactory => parent as Building_PSFactory;

    public Pawn BillForeman(List<Pawn> possiblePawns = null) =>
        (possiblePawns?.Any() ?? false ? possiblePawns : ParentAsProductionParent.ProducingPawns)?.RandomElementWithFallback();

    public Dictionary<Thing, int> SelectedIngredientsFor(Bill bill)
    {
        List<IngredientCount> ingredientList = [];
        bill.MakeIngredientsListInProcessingOrder(ingredientList);
        Dictionary<Thing, int> reserved = [];
        Dictionary<IngredientCount, int> countSoFar = [];
        Dictionary<IngredientCount, bool> done = [];
        foreach (IngredientCount ingredientCount in ingredientList)
            done.Add(ingredientCount, false);

        foreach (IntVec3 cellsCardinalInBound in AdjCellsCardinalInBounds)
        {
            Map map = parent.Map;
            List<Thing> potentialInputItemThings = cellsCardinalInBound.GetThingList(map);
            foreach (Thing potentialInputItemThing in potentialInputItemThings)
            {
                foreach (IngredientCount ingredientCount in ingredientList)
                {
                    if (done[ingredientCount] || !ingredientCount.filter.Allows(potentialInputItemThing))
                        continue;
                    int countSoFarForIngredient = countSoFar.GetWithFallback(ingredientCount, 0);
                    int required = ingredientCount.CountRequiredOfFor(potentialInputItemThing.def, bill.recipe);
                    required -= countSoFarForIngredient;
                    if (required > 0)
                    {
                        int reservedSoFar = reserved.GetWithFallback(potentialInputItemThing, 0);
                        int reservable = potentialInputItemThing.stackCount - reservedSoFar;
                        int toReserve = Math.Min(required, reservable);
                        if (toReserve >= required)
                            done[ingredientCount] = true;
                        reserved.SetOrAdd(potentialInputItemThing, toReserve + reservedSoFar);
                        countSoFar.SetOrAdd(ingredientCount, countSoFarForIngredient + toReserve);
                    }
                }
            }
        }

        return done.Any(pair => !pair.Value) ? [] : reserved;
    }

    public static Thing CalculateDominantIngredient(List<Thing> ingredients, RecipeDef recipeDef)
    {
        if (recipeDef.productHasIngredientStuff)
            return ingredients[0];
        return recipeDef.products.Any(x => x.thingDef.MadeFromStuff) || recipeDef.unfinishedThingDef is { MadeFromStuff: true }
            ? ingredients.Where(x => x.def.IsStuff).RandomElementByWeightWithFallback(x => x.stackCount, ingredients[0])
            : ingredients.RandomElementByWeight(x => x.stackCount);
    }

    public static void ConsumeIngredients(List<Thing> ingredients, RecipeDef recipe, Map map)
    {
        foreach (Thing t in ingredients)
            if (!t.Destroyed)
                recipe.Worker.ConsumeIngredient(t, recipe, map);
    }

    public Bill TryPickNextBill()
    {
        CurrentBill = ParentFactory?.BillStack?.bills?.FirstOrDefault(b => b.ShouldDoNow() && SelectedIngredientsFor(b).Any());
        return CurrentBill;
    }

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new(base.CompInspectStringExtra());
        sb.Append("PS_WorkProgress".Translate(storedWork, CurrentBill?.GetWorkAmount() ?? 0f));
        return sb.ToString();
    }

    public override bool IsActive => shouldBeActive;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref storedWork, "storedWork");
        Scribe_Values.Look(ref shouldBeActive, "shouldBeActive", true);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra() ?? [])
            yield return gizmo;
        yield return new Command_Toggle
        {
            defaultLabel = "Enabled".Translate(),
            toggleAction = delegate
            {
                shouldBeActive = !shouldBeActive;
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
            isActive = () => shouldBeActive
        };
    }
}
