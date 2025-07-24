using System;
using System.Linq;
using PipeSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.VEF;

public class CompPipedPawnStorageNutrition : CompPawnStorageNutrition
{
    private Lazy<CompResourceStorage> _resourceStorage;

    public virtual CompResourceStorage ResourceStorage => _resourceStorage.Value;
    public PipeNet PipeNet => ResourceStorage?.PipeNet;
    public override bool IsPiped => true;
    public override float storedNutrition => PipeNet?.Stored ?? 0;
    public override float MaxNutrition => PipeNet?.AvailableCapacity ?? 0;

    public override void Initialize(CompProperties props)
    {
        base.Initialize(props);
        _resourceStorage = new Lazy<CompResourceStorage>(() => parent?.GetComp<CompResourceStorage>());
    }

    public override bool AbsorbToFeedIfNeeded(Need_Food foodNeeds, float desiredFeed, out float amountFed)
    {
        amountFed = 0;
        if (PipeNet == null || ResourceStorage == null)
            return false;

        // Try to absorb from the network if needed
        if (desiredFeed > ResourceStorage.AmountStored)
        {
            // Try to pull what we need to make up the desire
            float toPull = desiredFeed - ResourceStorage.AmountStored;
            PipeNet.DrawAmongStorage(toPull, PipeNet.storages.Except(ResourceStorage).ToList());
        }

        amountFed = Mathf.Min(ResourceStorage.AmountStored, desiredFeed);

        // If we have enough to fulfil the desire, return true
        return Mathf.Approximately(amountFed, desiredFeed);
    }

    public override bool TryAbsorbNutritionFromHopper(float requestedNutrition)
    {
        if (!IsValidNutritionRequest(requestedNutrition) || PipeNet == null)
            return false;

        Thing feedSource = FindFeedInAnyHopper();
        if (feedSource == null)
            return false;

        float remainingNutrition = ProcessFeedAbsorption(feedSource, requestedNutrition);
        float absorbedNutrition = requestedNutrition - remainingNutrition;

        if (absorbedNutrition <= 0)
            return false;

        PipeNet.DistributeAmongStorage(absorbedNutrition, out float _);
        return true;
    }

    private bool IsValidNutritionRequest(float nutrition)
    {
        return nutrition > 0 && HasEnoughFeedstockInHoppers();
    }

    private float ProcessFeedAbsorption(Thing feedSource, float nutritionToAbsorb)
    {
        /*
         * Processes the absorption of specified nutrition from a given feed source. Determines the amount of nutrition
         * that can be absorbed from the provided feed source and updates its state accordingly. The remaining
         * unabsorbed nutrition is returned.
         */
        if (feedSource == null)
            return nutritionToAbsorb;

        float remainingNutrition = nutritionToAbsorb;
        float nutritionPerUnit = feedSource.GetStatValue(StatDefOf.Nutrition);

        while (remainingNutrition > 0)
        {
            int unitsToAbsorb = Mathf.Min(feedSource.stackCount, Mathf.FloorToInt(remainingNutrition / nutritionPerUnit));

            if (unitsToAbsorb <= 0)
                break;

            float nutritionAbsorbed = unitsToAbsorb * nutritionPerUnit;
            if (nutritionAbsorbed <= 0)
                break;

            remainingNutrition -= nutritionAbsorbed;

            if (unitsToAbsorb > 1)
            {
                feedSource.SplitOff(unitsToAbsorb);
            }
            else
            {
                feedSource.DeSpawn();
                break;
            }
        }

        return remainingNutrition;
    }
}
