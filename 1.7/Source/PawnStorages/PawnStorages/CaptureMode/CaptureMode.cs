using RimWorld;
using Verse;

namespace PawnStorages.CaptureMode;

/// <summary>
/// Base class for capture post-capture effects. Add a subclass plus a PS_Capture_Mode def
/// to register a new mode without touching the mod's own code.
/// </summary>
public abstract class CaptureMode
{
    public CaptureModeDef Def { get; internal set; }

    public abstract void ApplyPostCaptureEffects(Pawn pawn, Faction playerFaction, Pawn launcher, ThingWithComps equipment);
}
