using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.FloatMenu;

public class FloatMenuOptionProvider_Capture : FloatMenuOptionProvider
{
    public override bool Drafted => true;
    public override bool Undrafted => true;
    public override bool Multiselect => true;

    public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context)
    {
        return base.TargetPawnValid(pawn, context) && !pawn.RaceProps.Humanlike;
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
    {
        if (!context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.OnCell, Danger.Deadly))
        {
            yield return new FloatMenuOption("PS_CannotStore".Translate((NamedArgument)clickedPawn.Label) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
            yield break;
        }

        bool anyStorage = false;
        foreach (
            CompAssignableToPawn_PawnStorage storage in WorkGiver_Warden_TakeToStorage
                .GetPossibleStorages(clickedPawn)
                .GroupBy(s => s.parent.def)
                .Select(sGroup => sGroup.FirstOrDefault())
        )
        {
            if (storage == null || (storage.Props?.disallowEntityStoringCommand ?? false))
                continue;
            anyStorage = true;
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    "PS_StoreEntity".Translate((NamedArgument)clickedPawn.Label, (NamedArgument)storage.parent.LabelNoParenthesisCap),
                    () =>
                    {
                        ThingWithComps building = WorkGiver_Warden_TakeToStorage.GetStorageGeneral(clickedPawn, assign: true, preferredStorage: storage);
                        Job job = JobMaker.MakeJob(
                            clickedPawn.Faction == Faction.OfPlayer ? PS_DefOf.PS_CaptureAnimalInPawnStorage : PS_DefOf.PS_CaptureEntityInPawnStorage,
                            clickedPawn,
                            (Thing)building
                        );
                        job.count = 1;
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                    }
                ),
                context.FirstSelectedPawn,
                clickedPawn
            );
        }

        foreach (
            CompPawnStorage comp in context
                .FirstSelectedPawn.inventory.GetDirectlyHeldThings()
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
                        Job job = JobMaker.MakeJob(PS_DefOf.PS_CaptureInPawnStorageItem, (Thing)clickedPawn, (Thing)comp.parent);
                        job.count = 1;
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                    },
                    MenuOptionPriority.High,
                    revalidateClickTarget: clickedPawn
                ),
                context.FirstSelectedPawn,
                (Thing)clickedPawn
            );
        }

        if (!anyStorage)
        {
            yield return new FloatMenuOption("PS_NoEntityStore".Translate((NamedArgument)clickedPawn.Label), null);
        }
    }
}
