using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SimpleWarrants;
using Verse;

namespace PawnStorages.SimpleWarrants;

[HarmonyPatch(typeof(WarrantRequestComp))]
public static class WarrantRequestComp_Patch
{
    public static (Thing, Building_PawnStorage) TryGetWarrantTargetAsPawnStorageInCaravan(Warrant warrant, Caravan caravan)
    {
        var tame = warrant as Warrant_TameAnimal;
        
        foreach (var bldthing in CaravanInventoryUtility.AllInventoryItems(caravan).Where(t => t is Building_PawnStorage))
        {
            if (bldthing is not Building_PawnStorage storage) return (null, null);

            foreach (var thing in storage.storageComp.StoredPawns)
            {
                    
                // Tame warrant requires any pawn of the required type.
                if (tame != null && thing.RaceProps.Animal && thing.kindDef == tame.AnimalRace)
                {
                    // Check tameness.
                    var isTame = thing.training?.HasLearned(TrainableDefOf.Tameness) ?? false;

                    // Check health.
                    var healthPct = thing.health.summaryHealth.SummaryHealthPercent;

                    if (isTame && healthPct >= 0.9f)
                        return (thing, storage);
                }

                // Living pawn for pawn warrant.
                if (thing == warrant.thing)
                {
                    return (thing, storage);
                }
            }
        }
        return  (null, null);
    }
    
    [HarmonyPostfix]
    [HarmonyPatch("TryGetWarrantTargetInCaravan")]
    public static Thing TryGetWarrantTargetInCaravan_Patch(Warrant warrant, Caravan caravan)
    {
        (var thing, var _) = TryGetWarrantTargetAsPawnStorageInCaravan(warrant, caravan);
        return thing;
    }
    
    [HarmonyPrefix]
    [HarmonyPatch("Fulfill")]
    public static void Fulfill_Patch(WarrantRequestComp __instance, Caravan caravan)
    {
        foreach (var warrant in __instance.ActiveWarrants.ToList())
        {
            (var target, var storage) = TryGetWarrantTargetAsPawnStorageInCaravan(warrant, caravan);
            if (target == null || storage == null)
                continue;
            
            storage.storageComp.StoredPawns.Remove((Pawn)target);
            storage.storageComp.SetLabelDirty();
            
            warrant.GiveReward(caravan, target);
            QuestUtility.SendQuestTargetSignals(target.questTags, "WarrantRequestFulfilled", __instance.parent.Named("SUBJECT"), caravan.Named("CARAVAN"));
				
            // Force quest to end. Only necessary with animal quests because reasons.
            if (warrant.relatedQuest is { State: <= QuestState.Ongoing })
                warrant.relatedQuest.End(QuestEndOutcome.Success);

            WarrantsManager.Instance.acceptedWarrants.Remove(warrant);
            target.Destroy();
            Messages.Message("Warrant completed. Your caravan has received the payment.", MessageTypeDefOf.PositiveEvent, false);
        }
    }
}