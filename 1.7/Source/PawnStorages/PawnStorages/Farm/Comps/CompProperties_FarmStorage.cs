namespace PawnStorages.Farm.Comps;

public class CompProperties_FarmStorage : CompProperties_PawnStorage
{
    public override int MaxStoredPawns => PawnStoragesMod.settings.MaxPawnsInFarm;

    public CompProperties_FarmStorage()
    {
        compClass = typeof(CompFarmStorage);
    }
}
