using PipeSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.VEF;

public class CompPipedPawnStorageNutrition: CompPawnStorageNutrition
{
    public CompResource _compResource;
    public bool _haveCheckedForComp = false;
    public virtual CompResource compResource
    {
        get
        {
            if (!_haveCheckedForComp && _compResource == null && parent.HasComp<CompResource>())
            {
                _compResource = parent.GetComp<CompResource>();
                // make sure we only check once.
                _haveCheckedForComp = true;
            }
            return _compResource;
        }
    }

    public override bool IsPiped => IsAttachedToNet(out PipeNet pipeNet, out CompResource resource);
    public override float storedNutrition => IsAttachedToNet(out PipeNet pipeNet, out CompResource resource) ? pipeNet.Stored : base.storedNutrition;
    public override float MaxNutrition => IsAttachedToNet(out PipeNet pipeNet, out CompResource resource) ? pipeNet.AvailableCapacity : base.MaxNutrition;

    public bool IsAttachedToNet(out PipeNet pipeNet, out CompResource resource)
    {
        pipeNet = null;
        resource = compResource;
        if (resource is not { PipeNet: { } net }) return false;
        pipeNet = net;
        return pipeNet.connectors.Count > 1;
    }

    public override bool AbsorbToFeedIfNeeded(Need_Food foodNeeds, float desiredFeed, out float amountFed)
    {
        if (!IsAttachedToNet(out PipeNet pipeNet, out CompResource resource)) return base.AbsorbToFeedIfNeeded(foodNeeds, desiredFeed, out amountFed);

        amountFed = Mathf.Min(pipeNet.Stored, desiredFeed);
        pipeNet.DrawAmongStorage(amountFed, pipeNet.storages);
        return true;
    }

    public override bool TryAbsorbNutritionFromHopper(float nutrition)
    {
        if (!IsAttachedToNet(out PipeNet pipeNet, out CompResource resource)) return base.TryAbsorbNutritionFromHopper(nutrition);

        return !(pipeNet.Stored < nutrition);
    }

}
