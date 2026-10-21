using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages;

public class JobDriver_Release : JobDriver
{
    private bool HasStation => TargetB is { IsValid: true, HasThing: true, Thing: Building };
    private bool ReleasingSpecific => TargetC is { IsValid: true, HasThing: true, Thing: Pawn };

    private IntVec3 ReleaseCell => HasStation ? TargetB.Thing.InteractionCell : TargetA.Cell;

    public override string GetReport()
    {
        if (HasStation && ReleasingSpecific)
            return "PS_ReleaseReportA".Translate(TargetC.Pawn, TargetA.Thing, TargetB.Thing);
        if (HasStation)
            return "PS_ReleaseReportB".Translate(TargetA.Thing, TargetB.Thing);
        return ReleasingSpecific ? "PS_ReleaseReportC".Translate(TargetC.Pawn, TargetA.Thing) : "PS_ReleaseReportD".Translate(TargetA.Thing);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        if (HasStation)
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => !(TargetB.Thing.TryGetComp<CompPowerTrader>()?.PowerOn ?? true));
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
        toil.WithProgressBarToilDelay(TargetIndex.A);
        yield return toil;
        Toil release = new();
        release.initAction = delegate
        {
            Pawn actor = release.actor;
            CompPawnStorage comp = TargetA.Thing.TryGetComp<CompPawnStorage>();
            if (comp == null)
                return;
            if (ReleasingSpecific)
            {
                comp.ReleasePawn(TargetC.Pawn, ReleaseCell, actor.Map);
            }
            else
                for (int num = comp.GetDirectlyHeldThings().Count - 1; num >= 0; num--)
                {
                    comp.ReleasePawn((Pawn)comp.GetDirectlyHeldThings().GetAt(num), ReleaseCell, actor.Map);
                }
        };
        release.defaultCompleteMode = ToilCompleteMode.Instant;
        release.AddFinishAction(() =>
        {
            if (pawn.carryTracker.CarriedThing == TargetA.Thing)
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
        });
        yield return release;
    }
}
