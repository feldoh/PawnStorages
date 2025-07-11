using System.Collections.Generic;
using PipeSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.VEF;

public class CompPipedPawnStorageNutrition: CompPawnStorageNutrition
{
    public CompResource _compResource;
    public bool _haveCheckedForComp = false;
    public bool disablePipedPawnStorageNutrition = false;
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
    public override bool ShowNutritionLevelGizmo => !IsPiped || disablePipedPawnStorageNutrition || base.storedNutrition > 0;
    public override float storedNutrition => (IsAttachedToNet(out PipeNet pipeNet, out CompResource resource) ? pipeNet.Stored : 0) + base.storedNutrition;
    public override float MaxNutrition
    {
        get
        {
            if (IsAttachedToNet(out PipeNet pipeNet, out CompResource resource))
            {
                return pipeNet.AvailableCapacity <= 0 ? base.MaxNutrition : pipeNet.AvailableCapacity;
            }

            return base.MaxNutrition;
        }
    }

    public bool IsAttachedToNet(out PipeNet pipeNet, out CompResource resource)
    {
        pipeNet = null;
        resource = compResource;
        if (disablePipedPawnStorageNutrition) return false;
        if (resource is not { PipeNet: { } net }) return false;
        pipeNet = net;
        return pipeNet.connectors.Count > 1;
    }

    public override bool AbsorbFromAlternateSource(Need_Food foodNeeds, float desiredFeed, out float amountFed)
    {
        amountFed = 0f;
        if (!IsAttachedToNet(out PipeNet pipeNet, out CompResource resource) || pipeNet.Stored <= 0) return false;

        amountFed = Mathf.Min(pipeNet.Stored, desiredFeed);
        pipeNet.DrawAmongStorage(amountFed, pipeNet.storages);
        return true;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref disablePipedPawnStorageNutrition, "disablePipedPawnStorageNutrition", false);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        yield return new Command_Toggle()
        {
            defaultLabel = "PS_DisallowPipedNutrition".Translate(),
            defaultDesc = "PS_DisallowPipedNutritionDesc".Translate(),
            icon = TexCommand.ForbidOn,
            isActive = () => disablePipedPawnStorageNutrition,
            toggleAction = () => disablePipedPawnStorageNutrition = !disablePipedPawnStorageNutrition
        };
    }
}
