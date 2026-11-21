using Verse;

namespace PawnStorages.TickedStorage;

public interface IPawnListParent
{
    public ThingOwner GetDirectlyHeldThings();

    public void ReleasePawn(Pawn pawn);

    public bool NeedsDrop();

    public void Notify_PawnAdded(Pawn pawn);
    public void Notify_PawnRemoved(Pawn pawn);
}
