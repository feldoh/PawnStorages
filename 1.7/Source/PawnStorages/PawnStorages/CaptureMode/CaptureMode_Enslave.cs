using RimWorld;
using Verse;

namespace PawnStorages.CaptureMode;

public class CaptureMode_Enslave : CaptureMode
{
    public override void ApplyPostCaptureEffects(Pawn pawn, Faction playerFaction, Pawn launcher, ThingWithComps equipment)
    {
        if (pawn.RaceProps.Animal)
        {
            // SetFaction automatically handles tameness training for animals
            pawn.SetFaction(playerFaction, launcher);
            Messages.Message("PS_AnimalCapturedTamed".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, false);
        }
        else if (pawn.RaceProps.Humanlike)
        {
            if (ModsConfig.IdeologyActive && pawn.guest != null)
            {
                pawn.guest.SetGuestStatus(playerFaction, GuestStatus.Slave);
                Messages.Message("PS_PawnCapturedEnslaved".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent, false);
            }
            else
            {
                // Ideology isn't loaded / slavery isn't enabled — fall back to recruiting
                RecruitUtility.Recruit(pawn, playerFaction, launcher);
            }
        }
    }
}
