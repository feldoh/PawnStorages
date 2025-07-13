using System.Collections.Generic;
using System.Linq;
using PawnStorages.Farm.Comps;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages;

public class PawnStorages_GameComponent : GameComponent
{

    private static HashSet<CompAssignableToPawn_PawnStorage> _CompAssignables = [];

    public static bool AssignablesDirty = false;

    public static HashSet<CompAssignableToPawn_PawnStorage> CompAssignables
    {
        get
        {
            if (!AssignablesDirty) return _CompAssignables;

            _CompAssignables.Clear();
            foreach (CompAssignableToPawn_PawnStorage comp in Find.CurrentMap.spawnedThings.Select(thing =>
                         thing.TryGetComp<CompAssignableToPawn_PawnStorage>()))
            {
                if (comp != null)
                    _CompAssignables.Add(comp);
            }

            AssignablesDirty = false;

            return _CompAssignables;
        }
    }

    public static CompAssignableToPawn_PawnStorage GetAssignedStorage(Pawn pawn) => CompAssignables.FirstOrDefault(x => x.AssignedPawns.Contains(pawn));

    public PawnStorages_GameComponent(Game game)
    {
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        AssignablesDirty = true;
    }

    public override void StartedNewGame()
    {
        base.LoadedGame();
        AssignablesDirty = true;
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        AssignablesDirty = true;
    }

}
