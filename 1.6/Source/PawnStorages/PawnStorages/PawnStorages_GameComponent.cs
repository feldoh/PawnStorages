using System.Collections.Generic;
using System.Linq;
using Verse;

namespace PawnStorages;

public class PawnStorages_GameComponent : GameComponent
{
    public static bool AssignablesDirty = false;

    public static HashSet<CompAssignableToPawn_PawnStorage> CompAssignables
    {
        get
        {
            if (!AssignablesDirty)
                return field;

            field.Clear();
            foreach (CompAssignableToPawn_PawnStorage comp in Find.CurrentMap.spawnedThings.Select(thing => thing.TryGetComp<CompAssignableToPawn_PawnStorage>()))
            {
                if (comp != null)
                    field.Add(comp);
            }

            AssignablesDirty = false;

            return field;
        }
    } = [];

    public static CompAssignableToPawn_PawnStorage GetAssignedStorage(Pawn pawn) => CompAssignables.FirstOrDefault(x => x.AssignedPawns.Contains(pawn));

    public PawnStorages_GameComponent(Game game) { }

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
