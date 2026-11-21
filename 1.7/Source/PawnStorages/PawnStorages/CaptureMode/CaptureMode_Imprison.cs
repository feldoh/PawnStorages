using RimWorld;
using Verse;

namespace PawnStorages.CaptureMode;

public class CaptureMode_Imprison : CaptureMode
{
    public override void ApplyPostCaptureEffects(Pawn pawn, Faction playerFaction, Pawn launcher, ThingWithComps equipment)
    {
        if (pawn.RaceProps.Humanlike && pawn.guest != null)
        {
            pawn.guest.SetGuestStatus(playerFaction, GuestStatus.Prisoner);
            Messages.Message("PS_SphereImprisoned".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent, false);
        }
        else if (pawn.RaceProps.Animal)
        {
            // SetFaction automatically handles tameness training for animals
            pawn.SetFaction(playerFaction, launcher);
            Messages.Message("PS_AnimalCapturedTamed".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
