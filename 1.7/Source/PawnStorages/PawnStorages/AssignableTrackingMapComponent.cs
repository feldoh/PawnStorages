using Verse;

namespace PawnStorages;

/// <summary>
/// RimWorld's map teardown de-spawns pawns but clears buildings straight out of
/// the thing listers, so CompAssignableToPawn_PawnStorage.PostDeSpawn never fires
/// for storage buildings on a removed map. Prune those stale cache entries here.
/// </summary>
public class AssignableTrackingMapComponent : MapComponent
{
    public AssignableTrackingMapComponent(Map map)
        : base(map) { }

    public override void MapRemoved()
    {
        // Notify_MyMapRemoved marks things on the removed map as Discarded
        // (mapIndexOrState = -3), which is a reliable signal here. Comparing
        // parent.Map to this component's map would not work: by the time
        // MapRemoved runs, the map is out of Find.Maps and indices have shifted.
        PawnStorages_GameComponent.CompAssignables.RemoveWhere(c => c.parent == null || c.parent.Discarded);
    }
}
