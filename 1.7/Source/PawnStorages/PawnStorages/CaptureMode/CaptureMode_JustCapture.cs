using RimWorld;
using Verse;

namespace PawnStorages.CaptureMode;

public class CaptureMode_JustCapture : CaptureMode
{
    // Store the pawn as-is: no prisoner status, no faction change
    public override bool MarksCaptured => false;

    public override void ApplyPostCaptureEffects(Pawn pawn, Faction playerFaction, Pawn launcher, ThingWithComps equipment) { }
}
