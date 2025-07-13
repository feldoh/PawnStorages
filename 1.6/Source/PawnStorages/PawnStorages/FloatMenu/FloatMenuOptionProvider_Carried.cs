using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.FloatMenu;

public class FloatMenuOptionProvider_Carried : FloatMenuOptionProvider
{
    public override bool Drafted => true;

    public override bool Undrafted => true;

    public override bool Multiselect => false;

    public override bool RequiresManipulation => true;

    public override bool MechanoidCanDo => true;

    public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
    {
        return base.SelectedPawnValid(pawn, context) && pawn.carryTracker?.CarriedThing is Pawn;
    }

    public override bool TargetThingValid(Thing thing, FloatMenuContext context)
    {
        return base.TargetThingValid(thing, context) && thing is ThingWithComps twc && twc.HasComp<CompPawnStorage>();
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(
        Thing clickedThing,
        FloatMenuContext context)
    {
        if (context.FirstSelectedPawn.carryTracker.CarriedThing is not Pawn carriedPawn) yield break;

        TaggedString label = "PlaceIn".Translate((NamedArgument) (Thing) carriedPawn, (NamedArgument) clickedThing);

        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, Action), context.FirstSelectedPawn,
            clickedThing);
        yield break;

        void Action()
        {
            clickedThing.TryGetComp<CompPawnStorage>()?.TryAssignPawn(carriedPawn);
            Job job = JobMaker.MakeJob(carriedPawn.IsPrisonerOfColony || carriedPawn.InAggroMentalState || carriedPawn.HostileTo(Faction.OfPlayer)
                ? PS_DefOf.PS_CaptureCarriedToPawnStorage
                : PS_DefOf.PS_TakeToPawnStorage, carriedPawn, clickedThing);
            job.count = 1;
            job.playerForced = true;
            context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
        }
    }
}
