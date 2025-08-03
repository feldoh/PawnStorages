using System.Collections.Generic;
using PawnStorages.Farm.Comps;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.Farm;

public class JobDriver_TakeToFarm : JobDriver
{
    private const TargetIndex TakeeIndex = TargetIndex.A;

    private const TargetIndex StorageIndex = TargetIndex.B;

    protected Pawn Takee => (Pawn)job.GetTarget(TakeeIndex).Thing;

    protected ThingWithComps PawnStorageAssigned => (ThingWithComps)job.GetTarget(StorageIndex).Thing;
    protected CompFarmStorage PawnStorageComp => PawnStorageAssigned.TryGetComp<CompFarmStorage>();
    protected CompAssignableToPawn_PawnStorage PawnStorageAssignmentComp => PawnStorageAssigned.TryGetComp<CompAssignableToPawn_PawnStorage>();

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed) && pawn.Reserve(PawnStorageAssigned, job, PawnStorageComp.Props.MaxStoredPawns, 0, null, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TakeeIndex);
        this.FailOnDestroyedOrNull(StorageIndex);
        this.FailOnAggroMentalStateAndHostile(TakeeIndex);
        Toil goToTakee = Toils_Goto
            .GotoThing(TakeeIndex, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(TakeeIndex)
            .FailOnDespawnedNullOrForbidden(StorageIndex)
            .FailOn(() => job.def == JobDefOf.Arrest && !Takee.CanBeArrestedBy(pawn))
            .FailOn(() => !pawn.CanReach(PawnStorageAssigned, PathEndMode.OnCell, Danger.Deadly))
            .FailOn(() => (job.def == JobDefOf.Rescue || job.def == JobDefOf.Capture) && !Takee.Downed)
            .FailOnSomeonePhysicallyInteracting(TargetIndex.A);
        yield return goToTakee;
        Toil startCarrying = Toils_Haul.StartCarryThing(TakeeIndex);
        startCarrying.AddFinishAction(
            delegate
            {
                if (job.def == PS_DefOf.PS_CaptureEntityInPawnStorage || job.def == PS_DefOf.PS_CaptureAnimalInPawnStorage)
                    return;
            }
        );
        startCarrying.debugName = "startCarrying";
        Toil goToStorage = Toils_Goto.GotoThing(StorageIndex, PathEndMode.Touch).FailOn(() => !pawn.IsCarryingPawn(Takee));
        goToStorage.FailOnDespawnedNullOrForbidden(StorageIndex);
        goToStorage.debugName = "goToStorage";
        yield return Toils_Jump.JumpIf(goToStorage, () => pawn.IsCarryingPawn(Takee));
        yield return startCarrying;
        yield return goToStorage;
        Toil setTakeeSettings = ToilMaker.MakeToil();
        setTakeeSettings.debugName = "takeeSettings";
        setTakeeSettings.initAction = delegate
        {
            Takee.playerSettings ??= new Pawn_PlayerSettings(Takee);
        };
        yield return setTakeeSettings;
        yield return Toils_Reserve.Release(StorageIndex);
        yield return StoreIntoStorage(PawnStorageAssigned, pawn, Takee);
        yield return Toils_General.WaitWith(StorageIndex, 75);
    }

    public static Toil StoreIntoStorage(ThingWithComps storage, Pawn taker, Pawn takee)
    {
        Toil toil = ToilMaker.MakeToil();
        toil.initAction = delegate
        {
            IntVec3 position = storage.Position;
            taker.carryTracker.TryDropCarriedThing(position, ThingPlaceMode.Direct, out Thing _);
            takee.Notify_Teleported(false);
            takee.stances.CancelBusyStanceHard();
            if (takee.Downed)
            {
                CompFarmStorage comp = storage.TryGetComp<CompFarmStorage>();
                if (comp.CanStore)
                    comp.StorePawn(takee);
            }

            takee.jobs.StartJob(JobMaker.MakeJob(PS_DefOf.PS_Enter, (LocalTargetInfo)storage), JobCondition.InterruptForced, tag: JobTag.Misc);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return toil;
    }
}
