using System.Linq;
using RimWorld;
using Verse;

namespace PawnStorages.CaptureMode;

public static class CaptureModeUtility
{
    public static CaptureModeDef DefaultMode => DefDatabase<CaptureModeDef>.GetNamed("PS_Capture_Mode_Enslave", false) ?? DefDatabase<CaptureModeDef>.AllDefs.FirstOrDefault();

    /// <summary>
    /// Bring a captured mech under the capturer's faction. Without Biotech there is no mech control,
    /// so the mech keeps its faction. With Biotech, a mechanitor launcher becomes its overseer;
    /// otherwise it stays uncontrolled.
    /// </summary>
    public static bool TryTakeMech(Pawn mech, Faction faction, Pawn launcher)
    {
        if (!ModsConfig.BiotechActive)
            return false;

        // A captured mech can still be tied to its old overseer (common for mechs taken from a mechanitor faction).
        mech.GetOverseer()?.relations.RemoveDirectRelation(PawnRelationDefOf.Overseer, mech);

        RecruitUtility.Recruit(mech, faction, launcher);

        // Adding the relation is all that's needed to wire up control
        // Check spare bandwidth first, as vanilla does when handing out mechs, so we don't create a
        // mech that immediately needs bandwidth it doesn't have and later goes feral.
        if (launcher is { mechanitor: not null } && mech.OverseerSubject != null)
        {
            int spareBandwidth = launcher.mechanitor.TotalBandwidth - launcher.mechanitor.UsedBandwidth;
            if (spareBandwidth >= mech.GetStatValue(StatDefOf.BandwidthCost))
                launcher.relations.AddDirectRelation(PawnRelationDefOf.Overseer, mech);
        }

        return true;
    }
}
