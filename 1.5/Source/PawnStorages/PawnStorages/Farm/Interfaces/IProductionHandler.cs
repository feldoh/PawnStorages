using System.Collections.Generic;
using PawnStorages.Interfaces;
using Verse;

namespace PawnStorages.Farm.Interfaces;

public interface IProductionHandler
{
    public void TickPawns(IProductionParent productionParent, List<Thing> daysProduce, List<IExtraProductionHandler> extraProductionHandlers, int? tickInterval);
    public void MakePawnReadyToProduce(Pawn pawn);
}
