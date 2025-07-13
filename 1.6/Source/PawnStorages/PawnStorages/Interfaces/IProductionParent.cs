using System.Collections.Generic;
using Verse;

namespace PawnStorages.Interfaces
{
    public interface IProductionParent : IActive, IPawnRelease
    {
        List<Pawn> ProducingPawns { get; }
        int BuildingTickInterval { get; }
    }
}
