using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace PawnStorages;

public class JobDriver_CaptureInStorageItem : JobDriver_TakeToStorage
{
    public override string GetReport()
    {
        return "PS_CapturingToStorage".Translate(Takee.Label, Storage.Label);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TakeeIndex);
        this.FailOnAggroMentalStateAndHostile(TakeeIndex);

        Toil goToTakee = Toils_Goto
            .GotoThing(TakeeIndex, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(TakeeIndex)
            .FailOn(() => !(Takee.Downed || !Takee.HostileTo(pawn)));
        Toil checkArrestResistance = ToilMaker.MakeToil();
        checkArrestResistance.initAction = delegate
        {
            if (job.def == PS_DefOf.PS_CaptureEntityInPawnStorage || job.def == PS_DefOf.PS_CaptureAnimalInPawnStorage)
                return;
            if (!job.def.makeTargetPrisoner)
                return;
            Pawn victim = (Pawn)job.targetA.Thing;
            victim.GetLord()?.Notify_PawnAttemptArrested(victim);
            GenClamor.DoClamor(victim, 10f, ClamorDefOf.Harm);
            if (victim.IsPrisoner || victim.IsSlave)
                return;
            QuestUtility.SendQuestTargetSignals(victim.questTags, "Arrested", victim.Named("SUBJECT"));
            if (victim.Faction != null)
            {
                QuestUtility.SendQuestTargetSignals(victim.Faction.questTags, "FactionMemberArrested", victim.Faction.Named("FACTION"));
            }
        };
        checkArrestResistance.debugName = "checkArrestResistance";
        yield return goToTakee;
        yield return checkArrestResistance;
        Toil setTakeeSettings = ToilMaker.MakeToil();
        setTakeeSettings.debugName = "takeeSettings";
        setTakeeSettings.initAction = delegate
        {
            if (job.def != PS_DefOf.PS_CaptureEntityInPawnStorage && job.def != PS_DefOf.PS_CaptureAnimalInPawnStorage)
                CheckMakeTakeePrisoner();
            Takee.playerSettings ??= new Pawn_PlayerSettings(Takee);
        };
        yield return setTakeeSettings;
        yield return StoreIntoStorageItem(PawnStorageAssigned, pawn, Takee, TakeeRescued);
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
    }

    public static Toil StoreIntoStorageItem(ThingWithComps storage, Pawn taker, Pawn takee, bool rescued = false)
    {
        Toil toil = ToilMaker.MakeToil();
        toil.AddFinishAction(
            delegate
            {
                takee.Notify_Teleported(false);
                takee.stances.CancelBusyStanceHard();
                if (takee.Downed || !takee.HostileTo(taker))
                {
                    CompPawnStorage comp = storage.TryGetComp<CompPawnStorage>();
                    if (comp.CanStore)
                        comp.StorePawn(takee);
                }

                if (rescued)
                    takee.relations.Notify_RescuedBy(taker);
                takee.mindState.Notify_TuckedIntoBed();
            }
        );
        toil.defaultCompleteMode = ToilCompleteMode.Delay;
        toil.defaultDuration = 75;
        toil.WithProgressBarToilDelay(TakeeIndex);
        return toil;
    }

    private void CheckMakeTakeePrisoner()
    {
        if (job.def.makeTargetPrisoner && Takee.guest != null)
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
}
