using System.Collections.Generic;
using System.Linq;
using PawnStorages.TickedStorage;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class Building_PawnDoor : Building_Door, IPawnListParent
{
    CompPawnStorage storageComp;
    CompAssignableToPawn_PawnStorage compAssignable;

    public override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        DoorPreDraw();
        float offsetDist = (float)(0.0 + 0.45 * OpenPct);
        DrawMovers(drawLoc, offsetDist, Graphic, def.altitudeLayer.AltitudeFor(), Vector3.one, Graphic.ShadowGraphic);
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        storageComp = this.TryGetComp<CompPawnStorage>();
        compAssignable = this.TryGetComp<CompAssignableToPawn_PawnStorage>();

        if (storageComp == null)
            Log.Warning($"{this} has null CompPawnStorage even though of type {GetType().FullName}");
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return storageComp?.GetDirectlyHeldThings();
    }

    public void ReleasePawn(Pawn pawn)
    {
        storageComp.ReleaseSingle(Map, pawn, true, false);
    }

    public bool NeedsDrop()
    {
        return PawnStoragesMod.settings.AllowNeedsDrop && (storageComp == null || storageComp.Props.needsDrop);
    }

    public virtual void Notify_PawnAdded(Pawn pawn)
    {
        compAssignable?.TryAssignPawn(pawn);
    }

    public virtual void Notify_PawnRemoved(Pawn pawn) { }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        storageComp?.ReleaseContents(Map);
        base.Destroy(mode);
    }
}
