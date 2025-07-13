using System.Collections.Generic;
using PawnStorages.Interfaces;
using Verse;

namespace PawnStorages.Farm.Interfaces
{
    public interface IBreederParent : IActive
    {
        List<Pawn> BreedablePawns { get; }
        List<Pawn> AllHealthyPawns { get; }
        int BuildingTickInterval { get; }

        void Notify_PawnBorn(Pawn newPawn);

        void ReleasePawn(Pawn pawn);

        Map Map { get; }
    }
}
