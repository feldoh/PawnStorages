using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace PawnStorages;

public class JobDriver_Enter : JobDriver
{
    private bool HasStation => TargetB is { IsValid: true, HasThing: true, Thing: Building };

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true; // pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        if (HasStation)
        {
            this.FailOnDespawnedOrNull(TargetIndex.B);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.InteractionCell);
        }
        else
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
        }

        Toil toil = Toils_General.Wait(60);
        toil.WithProgressBarToilDelay(TargetIndex.B);
        yield return toil;
        Toil enter = new();
        enter.initAction = delegate
        {
            Pawn actor = enter.actor;
            CompPawnStorage comp = TargetA.Thing.TryGetComp<CompPawnStorage>();
            if (comp.CanStore)
                comp.StorePawn(actor);
        };
        enter.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return enter;
    }
}
