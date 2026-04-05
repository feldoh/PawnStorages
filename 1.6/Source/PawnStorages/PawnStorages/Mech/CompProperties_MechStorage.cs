using Verse;

namespace PawnStorages.Mech;

public class CompProperties_MechStorage : CompProperties_PawnStorage
{
    // Mech enters storage when energy drops below this level (0-1)
    public float mechEnterThreshold = 0.3f;

    // Mech will not exit for work unless energy is at or above this level (0-1)
    public float mechMinExitThreshold = 0.5f;

    // Energy restored per tick while stored in a powered building (Need_MechEnergy units).
    // Vanilla Building_MechCharger charges at 0.00083333f/tick (50 energy/day).
    public float mechChargeRate = 0.00083333f;

    public CompProperties_MechStorage()
    {
        compClass = typeof(CompMechStorage);
    }
}
