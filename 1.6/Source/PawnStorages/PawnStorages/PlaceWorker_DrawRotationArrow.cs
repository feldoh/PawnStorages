using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class PlaceWorker_DrawRotationArrow : PlaceWorker
{
    public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
    {
        Material mat = MaterialPool.MatFrom(PSContent.DirectionArrow, ShaderTypeDefOf.EdgeDetect.Shader, Color.white);
        Graphics.DrawMesh(MeshPool.plane10, (center + rot.FacingCell).ToVector3Shifted(), rot.AsQuat, mat, 0, null);
    }
}
