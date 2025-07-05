using System;
using System.Collections.Generic;
using AnimalBehaviours;
using PawnStorages.Farm.Interfaces;
using Verse;

namespace PawnStorages.VEF;

public class ProductionHandlerExtraAnimalProduct : IExtraProductionHandler
{
    private Random rand = new();

    public void ProduceExtraProducts(ThingComp comp, List<Thing> daysProduce)
    {
        if (comp is not CompAnimalProduct animalProductComp) return;
        if (!animalProductComp.Props.hasAditional || !(rand.NextDouble() <= animalProductComp.Props.additionalItemsProb / 100.0)) return;
        if (animalProductComp.Props.goInOrder)
        {
            foreach (string defName in animalProductComp.Props.additionalItems.InRandomOrder())
            {
                if (DefDatabase<ThingDef>.GetNamedSilentFail(defName) == null) continue;
                Thing thing = ThingMaker.MakeThing(ThingDef.Named(animalProductComp.Props.additionalItems.RandomElement()));
                thing.stackCount = animalProductComp.Props.additionalItemsNumber;
                daysProduce.Add(thing);
            }
        }
        else
        {
            Thing thing = ThingMaker.MakeThing(ThingDef.Named(animalProductComp.Props.additionalItems.RandomElement()));
            thing.stackCount = animalProductComp.Props.additionalItemsNumber;
            daysProduce.Add(thing);
        }
    }
}
