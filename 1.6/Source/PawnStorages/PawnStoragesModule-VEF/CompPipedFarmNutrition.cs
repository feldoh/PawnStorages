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
    public override float StoredNutrition => PipeNet?.Stored ?? 0;
    public override float MaxNutrition => PipeNet?.AvailableCapacity ?? 0;

    public override void Initialize(CompProperties properties)
    {
        base.Initialize(properties);
        _resourceStorage = new Lazy<CompResourceStorage>(() => parent?.GetComp<CompResourceStorage>());
    }

    public override bool AbsorbToFeedIfNeeded(Need_Food foodNeeds, float desiredFeed, out float amountFed)
    {
        /*
         * Priority:
         * 1) pull from internal storage
         * 2) pull from the network
         * 3) pull from hoppers
         */
        amountFed = 0;
        if (PipeNet == null || ResourceStorage == null)
            return false;

        // Try to absorb from the network if needed
        if (desiredFeed > ResourceStorage.AmountStored)
        {
            // Try to pull what we need to make up the desire
            float toPull = desiredFeed - ResourceStorage.AmountStored;
            PipeNet.DrawAmongStorage(toPull, PipeNet.storages.Except(ResourceStorage).ToList());
            AddNutritionToStorage(toPull);
        }

        // Pull from storage, and update needs accordingly
        amountFed = Mathf.Min(ResourceStorage.AmountStored, desiredFeed);
        foodNeeds.CurLevel += amountFed;
        ResourceStorage.DrawResource(amountFed);

        // If we've not filled desire - try to absorb from the hoppers.
        if (!Mathf.Approximately(amountFed, desiredFeed))
        {
            // Calculate remaining nutrition needed after initial feeding
            float newDesire = desiredFeed - amountFed;
            // Try to get more nutrition from hoppers if needed
            if (TryAbsorbNutritionFromSource(newDesire))
            {
                // Feed the pawn with additional nutrition pulled from hoppers
                float nextAmountFed = Mathf.Min(ResourceStorage.AmountStored, newDesire);
                foodNeeds.CurLevel += nextAmountFed;
                ResourceStorage.DrawResource(nextAmountFed);
            }
        }

        // If we have enough to fulfil the desire, return true
        return Mathf.Approximately(amountFed, desiredFeed);
    }

    public override bool AddNutritionToStorage(float amount)
    {
        /*
         * Adds a specified amount of nutrition to the storage, utilizing the associated pipe network if necessary.
         *  Returns true if any amount of nutrition was successfully added to the storage or distributed through the pipe network.
         *  Returns false if the specified amount is <= zero
         */

        // Return false if amount is zero or negative
        if (amount <= 0 || Mathf.Approximately(amount, 0)) return false;

        // Calculate how much can actually be added based on storage capacity
        float amountToAdd = Mathf.Min(ResourceStorage.AmountCanAccept, amount);

        // Add the calculated amount to storage
        ResourceStorage.AddResource(amountToAdd);

        // If we couldn't add all of the requested amount
        if (!Mathf.Approximately(amountToAdd, amount))
        {
            // Try to distribute remaining amount to other storage on the network
            PipeNet.DistributeAmongStorage(amount - amountToAdd, out float stored);

            // Return true if any amount was stored in network
            return stored > 0;
        }
        // Return true if we added anything to storage
        return amountToAdd > 0;
    }
}
