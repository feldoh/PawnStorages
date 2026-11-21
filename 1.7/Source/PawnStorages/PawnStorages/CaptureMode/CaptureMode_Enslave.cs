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
        else if (pawn.RaceProps.IsMechanoid)
        {
            // Mechs can't be prisoners or slaves: bring them under the capturer's control instead
            if (CaptureModeUtility.TryTakeMech(pawn, playerFaction, launcher))
                Messages.Message("PS_MechCaptured".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, false);
        }
        else if (pawn.RaceProps.Humanlike && pawn.guest != null)
        {
            if (ModsConfig.IdeologyActive)
            {
                pawn.guest.SetGuestStatus(playerFaction, GuestStatus.Slave);
                Messages.Message("PS_PawnCapturedEnslaved".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent, false);
            }
            else
            {
                // No slavery without Ideology: take them prisoner instead (keeps recruitment resistance)
                pawn.guest.SetGuestStatus(playerFaction, GuestStatus.Prisoner);
                Messages.Message("PS_SphereImprisoned".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent, false);
            }
        }
    }
}
