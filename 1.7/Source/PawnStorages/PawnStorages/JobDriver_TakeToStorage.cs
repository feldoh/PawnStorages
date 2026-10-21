using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnStorages;

public class JobDriver_TakeToStorage : JobDriver
{
    protected const TargetIndex TakeeIndex = TargetIndex.A;

    private const TargetIndex StorageIndex = TargetIndex.B;

    protected Pawn Takee => (Pawn)job.GetTarget(TakeeIndex).Thing;
    protected Thing Storage => job.GetTarget(StorageIndex).Thing;

    protected bool TakeeRescued
    {
        get
        {
            if (Takee.RaceProps.Humanlike && job.def != JobDefOf.Arrest && !Takee.IsPrisonerOfColony)
            {
                return !Takee.ageTracker.CurLifeStage.alwaysDowned || HealthAIUtility.ShouldSeekMedicalRest(Takee);
            }

            return false;
        }
    }

    protected ThingWithComps PawnStorageAssigned => (ThingWithComps)job.GetTarget(StorageIndex).Thing;
    protected CompPawnStorage PawnStorageComp => PawnStorageAssigned.TryGetComp<CompPawnStorage>();
    protected CompAssignableToPawn_PawnStorage PawnStorageAssignmentComp => PawnStorageAssigned.TryGetComp<CompAssignableToPawn_PawnStorage>();

    public override string GetReport()
    {
        if (job.def != JobDefOf.Rescue || TakeeRescued)
        {
            return base.GetReport();
        }

        if (Storage.TryGetComp<CompAssignableToPawn_PawnStorage>()?.Props.drawAsFrozenInCarbonite ?? false)
            return "PS_TakingToPlastinite".Translate(Takee.Label, Storage.Label);
        return "PS_TakingToStorage".Translate(Takee.Label, Storage.Label);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed) && pawn.Reserve(PawnStorageAssigned, job, PawnStorageComp.MaxStoredPawns(), 0, null, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TakeeIndex);
        this.FailOnDestroyedOrNull(StorageIndex);
        this.FailOnAggroMentalStateAndHostile(TakeeIndex);
        this.FailOn(
            delegate
            {
                if (
                    (job.def == PS_DefOf.PS_CaptureEntityInPawnStorage && PawnStorageAssignmentComp.OwnerType == BedOwnerType.Prisoner)
                    || job.def == PS_DefOf.PS_CaptureAnimalInPawnStorage
                )
                    return false;
                if (job.def.makeTargetPrisoner)
                {
                    if (PawnStorageAssignmentComp.OwnerType != BedOwnerType.Prisoner)
                    {
                        return true;
                    }
                }
                else if ((PawnStorageAssignmentComp.OwnerType == BedOwnerType.Prisoner) != Takee.IsPrisoner)
                {
                    return true;
                }

                return false;
            }
        );

        Toil goToTakee = Toils_Goto
            .GotoThing(TakeeIndex, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(TakeeIndex)
            .FailOnDespawnedNullOrForbidden(StorageIndex)
            .FailOn(() => job.def == JobDefOf.Arrest && !Takee.CanBeArrestedBy(pawn))
            .FailOn(() => !pawn.CanReach(PawnStorageAssigned, PathEndMode.ClosestTouch, Danger.Deadly))
            .FailOn(() => (job.def == JobDefOf.Rescue || job.def == JobDefOf.Capture) && !Takee.Downed)
            .FailOnSomeonePhysicallyInteracting(TargetIndex.A);

        Toil checkArrestResistance = ToilMaker.MakeToil();
        checkArrestResistance.initAction = delegate
        {
            if (job.def == PS_DefOf.PS_CaptureEntityInPawnStorage || job.def == PS_DefOf.PS_CaptureAnimalInPawnStorage)
                return;
            if (job.def.makeTargetPrisoner)
            {
                Pawn victim = (Pawn)job.targetA.Thing;
                victim.GetLord()?.Notify_PawnAttemptArrested(victim);
                GenClamor.DoClamor(victim, 10f, ClamorDefOf.Harm);
                if (!victim.IsPrisoner && !victim.IsSlave)
                {
                    QuestUtility.SendQuestTargetSignals(victim.questTags, "Arrested", victim.Named("SUBJECT"));
                    if (victim.Faction != null)
                    {
                        QuestUtility.SendQuestTargetSignals(victim.Faction.questTags, "FactionMemberArrested", victim.Faction.Named("FACTION"));
                    }
                }

                if (job.def == JobDefOf.Arrest && !victim.CheckAcceptArrest(base.pawn))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            }
        };
        checkArrestResistance.debugName = "checkArrestResistance";
        yield return Toils_Jump.JumpIf(checkArrestResistance, () => pawn.IsCarryingPawn(Takee));
        yield return goToTakee;
        yield return checkArrestResistance;
        Toil startCarrying = Toils_Haul.StartCarryThing(TakeeIndex);
        startCarrying.AddPreInitAction(CheckMakeTakeeGuest);
        startCarrying.AddFinishAction(
            delegate
            {
                if (job.def == PS_DefOf.PS_CaptureEntityInPawnStorage || job.def == PS_DefOf.PS_CaptureAnimalInPawnStorage)
                    return;
                if (pawn.Faction == Takee.Faction)
                {
                    CheckMakeTakeePrisoner();
                }
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
            if (job.def != PS_DefOf.PS_CaptureEntityInPawnStorage && job.def != PS_DefOf.PS_CaptureAnimalInPawnStorage)
                CheckMakeTakeePrisoner();
            Takee.playerSettings ??= new Pawn_PlayerSettings(Takee);
        };
        yield return setTakeeSettings;
        yield return Toils_Reserve.Release(StorageIndex);
        yield return StoreIntoStorage(PawnStorageAssigned, pawn, Takee, TakeeRescued);
        yield return Toils_General.Do(
            delegate
            {
                if (!job.ritualTag.NullOrEmpty())
                {
                    if (Takee.GetLord()?.LordJob is LordJob_Ritual lordJob_Ritual)
                    {
                        lordJob_Ritual.AddTagForPawn(Takee, job.ritualTag);
                    }

                    if (pawn.GetLord()?.LordJob is LordJob_Ritual lordJob_Ritual2)
                    {
                        lordJob_Ritual2.AddTagForPawn(pawn, job.ritualTag);
                    }
                }
            }
        );
        yield return Toils_General.WaitWith(StorageIndex, 75);
    }

    public static Toil StoreIntoStorage(ThingWithComps storage, Pawn taker, Pawn takee, bool rescued = false)
    {
        Toil toil = ToilMaker.MakeToil();
        toil.initAction = delegate
        {
            IntVec3 position = storage.Position;
            taker.carryTracker.TryDropCarriedThing(position, ThingPlaceMode.Near, out Thing _);
            takee.Notify_Teleported(false);
            takee.stances.CancelBusyStanceHard();
            if (takee.Downed)
            {
                CompPawnStorage comp = storage.TryGetComp<CompPawnStorage>();
                if (comp.CanStore)
                    comp.StorePawn(takee);
            }

            takee.jobs.StartJob(JobMaker.MakeJob(PS_DefOf.PS_Enter, (LocalTargetInfo)(Thing)storage), JobCondition.InterruptForced, tag: JobTag.Misc);
            if (rescued)
                takee.relations.Notify_RescuedBy(taker);
            takee.mindState.Notify_TuckedIntoBed();
            if (!takee.IsPrisonerOfColony)
                return;
            LessonAutoActivator.TeachOpportunity(ConceptDefOf.PrisonerTab, takee, OpportunityType.GoodToKnow);
        };
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        return toil;
    }

    private void CheckMakeTakeePrisoner()
    {
        if (job.def.makeTargetPrisoner)
        {
            if (Takee.guest.Released)
            {
                Takee.guest.Released = false;
                Takee.guest.interactionMode = PrisonerInteractionModeDefOf.MaintainOnly;
                GenGuest.RemoveHealthyPrisonerReleasedThoughts(Takee);
            }

            if (!Takee.IsPrisonerOfColony)
            {
                Takee.guest.CapturedBy(Faction.OfPlayer, pawn);
            }
        }
    }

    private void CheckMakeTakeeGuest()
    {
        if (!job.def.makeTargetPrisoner && Takee.Faction != Faction.OfPlayer && Takee.HostFaction != Faction.OfPlayer && Takee.guest != null && !Takee.IsWildMan())
        {
            Takee.guest.SetGuestStatus(Faction.OfPlayer);
            QuestUtility.SendQuestTargetSignals(Takee.questTags, "Rescued", Takee.Named("SUBJECT"));
        }
    }
}
