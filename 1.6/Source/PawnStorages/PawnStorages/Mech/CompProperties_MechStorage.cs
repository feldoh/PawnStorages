namespace PawnStorages.Mech;

public class CompProperties_MechStorage : CompProperties_PawnStorage
{
    // Mech enters storage when energy drops below this level (0-1)
    public float mechEnterThreshold = 0.3f;

    // Mech will not exit for work unless energy is at or above this level (0-1)
    public float mechMinExitThreshold = 0.5f;

    public CompProperties_MechStorage()
    {
        compClass = typeof(CompMechStorage);
    }
}
