using System.Linq;
using RimWorld;
using Verse;

namespace PawnStorages.CaptureMode;

public static class CaptureModeUtility
{
    public static CaptureModeDef DefaultMode => DefDatabase<CaptureModeDef>.GetNamed("PS_Capture_Mode_Enslave", false) ?? DefDatabase<CaptureModeDef>.AllDefs.FirstOrDefault();

    /// <summary>
    /// Bring a captured mech under the capturer's faction. Without Biotech there is no mech control,
    /// so the mech keeps its faction. With Biotech, a mechanitor launcher becomes its overseer
    /// (vanilla assigns the control group and applies bandwidth limits); otherwise it stays uncontrolled.
    /// </summary>
    public static bool TryTakeMech(Pawn mech, Faction faction, Pawn launcher)
    {
        if (!ModsConfig.BiotechActive)
            return false;

        RecruitUtility.Recruit(mech, faction, launcher);
        if (launcher != null && MechanitorUtility.IsMechanitor(launcher) && mech.GetOverseer() == null)
            launcher.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
        return true;
    }
}
