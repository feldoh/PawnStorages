using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PawnStorages;

public class PawnStorages_GameComponent : GameComponent
{
    // Populated by CompAssignableToPawn_PawnStorage.PostSpawnSetup/PostDeSpawn
    // across all maps — no need to rebuild from a single map.
    public static HashSet<CompAssignableToPawn_PawnStorage> CompAssignables { get; } = [];

    public static CompAssignableToPawn_PawnStorage GetAssignedStorage(Pawn pawn) => CompAssignables.FirstOrDefault(x => x.AssignedPawns.Contains(pawn));

    public PawnStorages_GameComponent(Game game)
    {
        CompAssignables.Clear();
    }
}
