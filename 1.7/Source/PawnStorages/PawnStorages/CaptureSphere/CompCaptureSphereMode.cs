using System.Collections.Generic;
using PawnStorages.CaptureMode;
using RimWorld;
using Verse;

namespace PawnStorages.CaptureSphere;

public class CompCaptureSphereMode : ThingComp
{
    public CaptureModeDef Mode;

    public CaptureModeDef EffectiveMode => Mode ?? CaptureModeUtility.DefaultMode;

    public void SetMode(CaptureModeDef mode) => Mode = mode;

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo g in base.CompGetGizmosExtra())
            yield return g;
        yield return new Command_SetCaptureSphereMode(this);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Defs.Look(ref Mode, "PS_SphereMode");
    }
}
