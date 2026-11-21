using RimWorld;
using Verse;

namespace PawnStorages.Mech;

/// <summary>
/// Per-mech auto-release bookkeeping. If a mech comes back into storage soon after
/// being released (it found nothing it could actually do), its next auto-release is
/// delayed, doubling each time, so it does not cycle in and out every check interval.
/// </summary>
public class MechCycleState : IExposable
{
    /// <summary>A mech stored again within this many ticks of release counts as a bounce.</summary>
    public const int BounceWindowTicks = 1000;

    /// <summary>Longest delay between auto-releases (one in-game day).</summary>
    public const int MaxBackoffTicks = GenDate.TicksPerDay;

    public int lastReleaseTick = -1;
    public int backoffTicks;
    public int nextReleaseTick;

    public void Notify_Released(int now) => lastReleaseTick = now;

    public void Notify_Stored(int now, int checkInterval)
    {
        bool bounced = lastReleaseTick >= 0 && now - lastReleaseTick < BounceWindowTicks;
        backoffTicks = bounced ? System.Math.Min(System.Math.Max(backoffTicks * 2, checkInterval * 2), MaxBackoffTicks) : 0;
        nextReleaseTick = now + backoffTicks;
    }

    public bool CanAutoRelease(int now) => now >= nextReleaseTick;

    public void ExposeData()
    {
        Scribe_Values.Look(ref lastReleaseTick, "lastReleaseTick", -1);
        Scribe_Values.Look(ref backoffTicks, "backoffTicks");
        Scribe_Values.Look(ref nextReleaseTick, "nextReleaseTick");
    }
}
