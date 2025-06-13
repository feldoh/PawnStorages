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
    public static (Thing, IThingHolder) TryGetWarrantTargetAsPawnStorageInCaravan(Warrant warrant, Caravan caravan)
    {
        Warrant_TameAnimal tame = warrant as Warrant_TameAnimal;

        foreach (Thing thing1 in CaravanInventoryUtility.AllInventoryItems(caravan).Where(t => t is MinifiedThing { InnerThing: Building_PawnStorage }))
        {
            MinifiedThing bldthing = (MinifiedThing)thing1;
            if (bldthing.InnerThing.TryGetComp<CompPawnStorage>() is not { } holder) continue;

            foreach (Thing thing in holder.GetDirectlyHeldThings())
            {
                if (thing is not Pawn pawn) continue;

                // Tame warrant requires any pawn of the required type.
                if (tame != null && pawn.RaceProps.Animal && pawn.kindDef == tame.AnimalRace)
                {
                    // Check tameness.
                    bool isTame = pawn.training?.HasLearned(TrainableDefOf.Tameness) ?? false;

                    // Check health.
                    float healthPct = pawn.health.summaryHealth.SummaryHealthPercent;

                    if (isTame && healthPct >= 0.9f)
                        return (pawn, holder);
                }

                // Living pawn for pawn warrant.
                if (thing == warrant.thing)
                {
                    return (thing, bldthing);
                }

                // force check when there's pawn duping happening
                if (thing.thingIDNumber == warrant.thing.thingIDNumber)
                {
                    return (thing, bldthing);
                }
            }
        }

        return (null, null);
    }

    [HarmonyPostfix]
    [HarmonyPatch("TryGetWarrantTargetInCaravan")]
    public static void TryGetWarrantTargetInCaravan_Patch(ref Thing __result, Warrant warrant, Caravan caravan)
    {
        if (__result != null) return;
        (Thing thing, IThingHolder _) = TryGetWarrantTargetAsPawnStorageInCaravan(warrant, caravan);
        __result = thing;
    }

    [HarmonyPrefix]
    [HarmonyPatch("Fulfill")]
    public static void Fulfill_Patch(WarrantRequestComp __instance, Caravan caravan)
    {
        foreach (Warrant warrant in __instance.ActiveWarrants.ToList())
        {
            (Thing target, IThingHolder storage) = TryGetWarrantTargetAsPawnStorageInCaravan(warrant, caravan);
            if (target == null || storage == null)
                continue;

            storage.GetDirectlyHeldThings().Remove(target);
            ((ThingWithComps)storage).TryGetComp<CompPawnStorage>()?.SetLabelDirty();

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
