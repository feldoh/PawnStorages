using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.FloatMenu;

public class FloatMenuOptionProvider_Arrest : FloatMenuOptionProvider
{
    public override bool Drafted => true;

    public override bool Undrafted => true;

    public override bool Multiselect => false;

    public override bool RequiresManipulation => true;

    public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context)
    {
        return base.TargetPawnValid(pawn, context) && (pawn.Downed || (pawn.guilt?.IsGuilty ?? false)) && (!pawn.IsWildMan() || pawn.IsPrisonerOfColony);
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
    {
        if (!(clickedPawn.CanBeArrestedBy(context.FirstSelectedPawn) || clickedPawn.CanBeCaptured()))
        {
            yield break;
        }

        if (
            context.FirstSelectedPawn.InSameExtraFaction(clickedPawn, ExtraFactionType.HomeFaction)
            || context.FirstSelectedPawn.InSameExtraFaction(clickedPawn, ExtraFactionType.MiniFaction)
        )
        {
            yield return new FloatMenuOption("CannotArrest".Translate() + ": " + "SameFaction".Translate((NamedArgument)(Thing)clickedPawn), (Action)null);
            yield break;
        }

        if (!context.FirstSelectedPawn.CanReach((LocalTargetInfo)(Thing)context.FirstSelectedPawn, PathEndMode.OnCell, Danger.Deadly))
        {
            yield return new FloatMenuOption("CannotArrest".Translate() + ": " + "NoPath".Translate().CapitalizeFirst(), (Action)null);
            yield break;
        }

        bool notArresting = clickedPawn.Faction == null || (clickedPawn.Faction == Faction.OfPlayer || clickedPawn.Faction.Hidden) && !clickedPawn.IsQuestLodger();

        bool anyStorage = false;
        foreach (
            CompAssignableToPawn_PawnStorage storage in WorkGiver_Warden_TakeToStorage
                .GetPossibleStorages(clickedPawn)
                .GroupBy(s => s.parent.def)
                .Select(sGroup => sGroup.FirstOrDefault())
        )
        {
            if (storage == null)
                continue;
            anyStorage = true;
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    (notArresting ? "PS_TakeToStorageFloatMenu" : "PS_CaptureToStorageFloatMenu").Translate(
                        (NamedArgument)clickedPawn.LabelCap,
                        (NamedArgument)storage.parent.LabelNoParenthesisCap
                    ),
                    () =>
                    {
                        ThingWithComps building = WorkGiver_Warden_TakeToStorage.GetStorageGeneral(clickedPawn, assign: true, preferredStorage: storage);
                        Job job = JobMaker.MakeJob(
                            notArresting ? PS_DefOf.PS_TakeToPawnStorage : PS_DefOf.PS_CaptureInPawnStorage,
                            (LocalTargetInfo)(Thing)clickedPawn,
                            (LocalTargetInfo)(Thing)building
                        );
                        job.count = 1;
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                        if (notArresting)
                            return;
                        TutorUtility.DoModalDialogIfNotKnown(ConceptDefOf.ArrestingCreatesEnemies, context.FirstSelectedPawn.GetAcceptArrestChance(clickedPawn).ToStringPercent());
                    },
                    MenuOptionPriority.High,
                    revalidateClickTarget: clickedPawn
                ),
                clickedPawn,
                (LocalTargetInfo)(Thing)clickedPawn
            );
        }

        foreach (
            CompPawnStorage comp in clickedPawn
                .inventory.GetDirectlyHeldThings()
                .Select(item => item.TryGetComp<CompPawnStorage>() is { } ps && ps.Props.useFromInventory && !ps.IsFull ? ps : null)
                .Where(ps => ps != null)
                .GroupBy(s => s.parent.def)
                .Select(sGroup => sGroup.FirstOrDefault())
        )
        {
            if (comp == null)
                continue;
            anyStorage = true;
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    "PS_CaptureToStorageFloatMenu".Translate((NamedArgument)clickedPawn.LabelCap, (NamedArgument)comp.parent.LabelNoParenthesisCap),
                    () =>
                    {
                        Job job = JobMaker.MakeJob(PS_DefOf.PS_CaptureInPawnStorageItem, (LocalTargetInfo)(Thing)clickedPawn, (LocalTargetInfo)(Thing)comp.parent);
                        job.count = 1;
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                        if (notArresting)
                            return;
                        TutorUtility.DoModalDialogIfNotKnown(ConceptDefOf.ArrestingCreatesEnemies, clickedPawn.GetAcceptArrestChance(context.FirstSelectedPawn).ToStringPercent());
                    },
                    MenuOptionPriority.High,
                    revalidateClickTarget: clickedPawn
                ),
                context.FirstSelectedPawn,
                (LocalTargetInfo)(Thing)clickedPawn
            );
        }

        if (!anyStorage)
        {
            yield return new FloatMenuOption("PS_NoPrisonerStorage".Translate((NamedArgument)clickedPawn.Label), null);
        }
    }
}
