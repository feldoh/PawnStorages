using LudeonTK;
using RimWorld;
using Verse;

namespace PawnStorages.Anomaly;

public static class DebugActions_AllFeature
{
    [DebugAction("Pawn Storages", "All Feature: Advance Stage", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void AdvanceStage()
    {
        AllFeature_GameComponent comp = Current.Game?.GetComponent<AllFeature_GameComponent>();
        if (comp == null)
        {
            Log.Warning("AllFeature_GameComponent not found");
            return;
        }

        switch (comp.Stage)
        {
            case AllDiscoveryStage.None:
                // Force Stage1: pick any occupied stand
                foreach (Map map in Find.Maps)
                {
                    if (!map.IsPlayerHome)
                        continue;
                    foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
                    {
                        if (thing is Building_PawnStorage { storageComp: not null })
                        {
                            comp.ForceStage(AllDiscoveryStage.Stage1_Noticed);
                            Messages.Message("Debug: Advanced to Stage1_Noticed", MessageTypeDefOf.NeutralEvent);
                            return;
                        }
                    }
                }
                Messages.Message("Debug: No stands found on player maps", MessageTypeDefOf.RejectInput);
                break;

            case AllDiscoveryStage.Stage1_Noticed:
                comp.ForceStage(AllDiscoveryStage.Stage2_Hint);
                Messages.Message("Debug: Advanced to Stage2_Hint", MessageTypeDefOf.NeutralEvent);
                break;

            case AllDiscoveryStage.Stage2_Hint:
                // Spawn All at map center
                Map homeMap = Find.CurrentMap ?? Find.AnyPlayerHomeMap;
                if (homeMap != null)
                {
                    comp.SpawnAll(homeMap.Center, homeMap);
                    Messages.Message("Debug: Spawned All", MessageTypeDefOf.NeutralEvent);
                }
                break;

            case AllDiscoveryStage.AllSpawned:
                comp.ForceStage(AllDiscoveryStage.AllResearched);
                if (comp.AllPawn is { Spawned: true })
                    comp.AllPawn.DeSpawn();
                Messages.Message("Debug: Advanced to AllResearched", MessageTypeDefOf.NeutralEvent);
                break;

            case AllDiscoveryStage.AllResearched:
                Messages.Message("Debug: Use extraction gizmo on a stand to advance", MessageTypeDefOf.NeutralEvent);
                break;

            default:
                Messages.Message($"Debug: Current stage is {comp.Stage}, cannot advance further", MessageTypeDefOf.NeutralEvent);
                break;
        }
    }

    [DebugAction("Pawn Storages", "All Feature: Reset to None", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void ResetStage()
    {
        AllFeature_GameComponent comp = Current.Game?.GetComponent<AllFeature_GameComponent>();
        if (comp == null)
            return;

        comp.ForceStage(AllDiscoveryStage.None);
        Messages.Message("Debug: Reset to None", MessageTypeDefOf.NeutralEvent);
    }

    [DebugAction("Pawn Storages", "All Feature: Show Stage", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    public static void ShowStage()
    {
        AllFeature_GameComponent comp = Current.Game?.GetComponent<AllFeature_GameComponent>();
        if (comp == null)
            return;

        Messages.Message($"All Feature Stage: {comp.Stage}", MessageTypeDefOf.NeutralEvent);
    }
}
