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
        foodNeeds.CurLevel += amountFed;
        ResourceStorage.DrawResource(amountFed);

        // If we have enough to fulfil the desire, return true
        return Mathf.Approximately(amountFed, desiredFeed);
    }

    public override bool IsValidNutritionRequest(float nutrition)
    {
        if (PipeNet is null) return false;
        return nutrition > 0 && HasEnoughFeedstockInHoppers();
    }

    public override bool AddNutritionToStorage(float amount)
    {
        if (amount <= 0 || Mathf.Approximately(amount, 0)) return false;
        if (PipeNet is null) return false;
        PipeNet.DistributeAmongStorage(amount, out float stored);
        return stored > 0;
    }
}
