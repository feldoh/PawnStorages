using RimWorld;
using Verse;

namespace PawnStorages.CaptureMode;

public class CaptureMode_Convert : CaptureMode
{
    public override void ApplyPostCaptureEffects(Pawn pawn, Faction playerFaction, Pawn launcher, ThingWithComps equipment)
    {
        if (!pawn.RaceProps.Humanlike && !pawn.RaceProps.Animal)
            return;

        RecruitUtility.Recruit(pawn, playerFaction, launcher);
        Messages.Message("PS_SphereConverted".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, false);
    }
}
