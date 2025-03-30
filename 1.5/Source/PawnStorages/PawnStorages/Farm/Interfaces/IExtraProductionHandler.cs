using System.Collections.Generic;
using PawnStorages.Interfaces;
using Verse;

namespace PawnStorages.Farm.Interfaces;

/**
 * This interface is used to handle extra production of products.
 * This handles cases where people have made their own production methods which build on an existing one.
 * For example, the AnimalBehaviours mod adds a new production method for animals which produces extra products.
 * We don't want to pass it through the normal production handler, as it would duplicate the production.
 * So we use this interface to split off the secondary part and handle only that.
 * The base ones in core will call this interface to handle the extra production before resetting fullness etc.
 */
public interface IExtraProductionHandler
{
    public void ProduceExtraProducts(ThingComp comp, List<Thing> daysProduce);
}
