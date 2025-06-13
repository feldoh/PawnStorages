using System.Linq;
using PawnStorages.TickedStorage;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class Building_PawnStorage : PSBuilding, IPawnListParent, IThingGlower
{
    public CompPawnStorage storageComp;

    public CompAssignableToPawn_PawnStorage compAssignable;

    public CompGlower glower;

    public override bool ShouldUseAlternative => base.ShouldUseAlternative && !storageComp.GetDirectlyHeldThings().NullOrEmpty();

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        storageComp = this.TryGetComp<CompPawnStorage>();
        compAssignable = this.TryGetComp<CompAssignableToPawn_PawnStorage>();
        glower = this.TryGetComp<CompGlower>();

        if (storageComp == null)
            Log.Warning($"{this} has null CompPawnStorage even though of type {GetType().FullName}");
    }

    /*
    [TweakValue("STATUE ROTATION", 0, 4)]
    public static int ROTATION = 0;

    [TweakValue("STATUE FLIPPING", 0f, 100f)]
    public static bool FLIPBOOL = false;
    */

    public override void Print(SectionLayer layer)
    {
        Pawn pawn = (Pawn)storageComp.GetDirectlyHeldThings().FirstOrDefault();

        if (storageComp.Props.showStoredPawn && pawn != null)
        {
            // Grab the set rotation from the storage comp so the user can tweak the rotation
            Rot4 rot = storageComp.Rotation;
            Vector3 pos = DrawPos;
            pos.y += Altitudes.AltInc;
            Vector3 s = new(1.3f, 1f, 1.3f);

            if (compAssignable.Props.drawAsFrozenInCarbonite)
            {
                s.z *= 0.5f;
                pos += StatueOffset.RotatedBy(Rotation);
            }

            pos += Graphic.DrawOffset(rot);
            pos += StatueOffset;
            pos.x += compAssignable.Props.pawnDrawOffsetX;
            pos.z += compAssignable.Props.pawnDrawOffsetZ;

            s.z *= compAssignable.Props.pawnDrawScaleZ;
            s.x *= compAssignable.Props.pawnDrawScaleX;

            if (OnlyRenderPawnNorth && Rotation == Rot4.North || !OnlyRenderPawnNorth)
            {
                // Pass in PawnHealthState.Mobile as an override to ensure the pawn is drawn upright
                RenderTexture texture = PortraitsCache.Get(pawn, new Vector2(175f, 175f), rot, new Vector3(0f, 0f, 0.1f), compAssignable.Props.cameraZoom, healthStateOverride: PawnHealthState.Mobile);

                MaterialRequest req2 = default;
                if (compAssignable.Props.drawGrayscale)
                {
                    req2.mainTex = texture.GetGreyscale();
                }
                else
                {
                    req2.mainTex = texture;
                }

                req2.shader = Graphic.data?.shaderType?.Shader;
                if (req2.shader == null) req2.shader = ShaderDatabase.DefaultShader;
                req2.color = DrawColor;
                req2.colorTwo = DrawColorTwo;

                Mesh mesh = Object.Instantiate(Graphic.MeshAt(rot));
                Material mat = MaterialPool.MatFrom(req2);

                //Somehow this magically fixes the flipping issue, just keeping it this way.
                mesh.SetUVs(false);
                Printer_Mesh.PrintMesh(layer, Matrix4x4.TRS(pos, Rotation.AsQuat, s), mesh, mat);

                if (ShouldShowOverlay)
                {
                    pos -= StatueOffset;
                    pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                    Printer_Mesh.PrintMesh(layer, Matrix4x4.TRS(pos, Rot4.North.AsQuat, new Vector3(1,1,1)), OverlayGraphic.MeshAt(Rotation), OverlayGraphic.MatAt(Rotation));
                }
            }
        }

        base.Print(layer);
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return storageComp?.GetDirectlyHeldThings();
    }

    public void ReleasePawn(Pawn pawn)
    {
        storageComp?.ReleaseSingle(Map, pawn, true, true);

    }

    public bool NeedsDrop()
    {
        return PawnStoragesMod.settings.AllowNeedsDrop && (storageComp == null || storageComp.Props.needsDrop);
    }

    public bool ShouldBeLitNow()
    {
        return storageComp != null && storageComp.innerContainer.Count > 0;
    }

    public virtual void Notify_PawnAdded(Pawn pawn)
    {
        glower?.UpdateLit(Map);
    }

    public virtual void Notify_PawnRemoved(Pawn pawn)
    {
        glower?.UpdateLit(Map);
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        storageComp?.ReleaseContents(Map);
        base.Destroy(mode);
    }
}
