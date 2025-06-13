using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.FloatMenu;

public class FloatMenuOptionProvider_Enter : FloatMenuOptionProvider
{
    public override bool Drafted => true;

    public override bool Undrafted => true;

    public override bool Multiselect => true;

    public override bool TargetThingValid(Thing thing, FloatMenuContext context)
    {
        return base.TargetThingValid(thing, context) && thing is ThingWithComps twc && twc.HasComp<CompPawnStorage>();
    }

    public override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
    {
        CompPawnStorage storageComp = clickedThing.TryGetComp<CompPawnStorage>();
        if (storageComp.Props.convertOption && storageComp.CanStore)
            return new FloatMenuOption("PS_Enter".Translate(), delegate
            {
                foreach (Pawn selectedPawn in context.allSelectedPawns)
                {
                    Job job = storageComp.EnterJob(selectedPawn);
                    selectedPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                }
            });

        return null;
    }
}
