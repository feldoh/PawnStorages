using Verse;

namespace PawnStorages.Mech;

public class CompProperties_MechStorage : CompProperties_PawnStorage
{
    // Mech enters storage when energy drops below this level (0-1)
    public float mechEnterThreshold = 0.3f;

    // Mech will not exit for work unless energy is at or above this level (0-1)
    public float mechMinExitThreshold = 0.5f;

    // How often (in ticks) to check if stored mechs should exit for work
    public int mechCheckWorkInterval = 500;

    // Energy restored per tick while stored in a powered building (Need_MechEnergy units)
    public float mechChargeRate = 0.0002f;

    public CompProperties_MechStorage()
    {
        compClass = typeof(CompMechStorage);
    }
}
