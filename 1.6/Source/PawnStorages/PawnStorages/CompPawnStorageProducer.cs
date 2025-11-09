using System.Collections.Generic;
using PawnStorages.Interfaces;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class CompPawnStorageProducer : ThingComp, IActive
{
    public IProductionParent ParentAsProductionParent => parent as IProductionParent;

    protected List<Thing> DaysProduce = [];
    public bool ProduceNow = false;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref DaysProduce, "daysProduce", LookMode.Deep);
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Command_Action
        {
            defaultLabel = "PS_ProduceNow".Translate(DaysProduce.Count),
            action = delegate
            {
                ProduceNow = true;
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
            disabled = DaysProduce.Count <= 0,
            disabledReason = "PS_NothingToProduce".Translate(),
        };
    }

    public virtual bool IsActive => true;
}
