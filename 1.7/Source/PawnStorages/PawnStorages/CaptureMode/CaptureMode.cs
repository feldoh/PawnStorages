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

    /// <summary>Whether a standing/downed non-colony pawn is taken prisoner (vanilla CapturedBy) on capture.</summary>
    public virtual bool MarksCaptured => true;

    public abstract void ApplyPostCaptureEffects(Pawn pawn, Faction playerFaction, Pawn launcher, ThingWithComps equipment);
}
