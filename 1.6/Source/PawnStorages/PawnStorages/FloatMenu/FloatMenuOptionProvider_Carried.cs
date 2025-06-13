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

    public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context)
    {
        return base.TargetPawnValid(pawn, context) && pawn.carryTracker?.CarriedThing is Pawn;
    }

    public override bool TargetThingValid(Thing thing, FloatMenuContext context)
    {
        return base.TargetThingValid(thing, context) && thing is ThingWithComps twc && twc.HasComp<CompPawnStorage>();
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(
        Thing clickedThing,
        FloatMenuContext context)
    {
        Pawn carriedPawn = context.FirstSelectedPawn.carryTracker.CarriedThing as Pawn;
        if (carriedPawn == null) yield break;

        TaggedString label = "PlaceIn".Translate((NamedArgument) (Thing) carriedPawn, (NamedArgument) clickedThing);
        Action action = () =>
        {
            clickedThing.TryGetComp<CompPawnStorage>()?.TryAssignPawn(carriedPawn);
            Job job = JobMaker.MakeJob(
                carriedPawn.IsPrisonerOfColony || carriedPawn.InAggroMentalState ||
                carriedPawn.HostileTo(Faction.OfPlayer)
                    ? PS_DefOf.PS_CaptureCarriedToPawnStorage
                    : PS_DefOf.PS_TakeToPawnStorage, carriedPawn, clickedThing);
            job.count = 1;
            job.playerForced = true;
            context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
        };
        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), context.FirstSelectedPawn,
            clickedThing);
    }
}
