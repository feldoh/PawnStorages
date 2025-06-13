namespace PawnStorages.TickedStorage;

public class CompProperties_TickedStorage: CompProperties_PawnStorage
{
    public bool tickHediffs = true;
    public bool tickAge = false;
    public bool tickNutrition => needsDrop;

    public int tickInterval = 300;

    public CompProperties_TickedStorage()
    {
        compClass = typeof(CompTickedStorage);
    }
}
