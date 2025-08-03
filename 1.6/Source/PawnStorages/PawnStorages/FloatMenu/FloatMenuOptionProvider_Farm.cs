using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.FloatMenu;

public class FloatMenuOptionProvider_Farm : FloatMenuOptionProvider
{
    public override bool Drafted => true;

    public override bool Undrafted => true;

    public override bool Multiselect => false;

    public override bool RequiresManipulation => true;

    public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context)
    {
        return base.TargetPawnValid(pawn, context) && WorkGiver_Warden_TakeToStorage.GetStorageForFarmAnimal(pawn, assign: false, breeding: false) != null;
    }

    public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
    {
        if (!context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.OnCell, Danger.Deadly))
        {
            yield return new FloatMenuOption("PS_NoFarm".Translate((NamedArgument)clickedPawn.Label) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
        }
        else
        {
            ThingWithComps building = WorkGiver_Warden_TakeToStorage.GetStorageForFarmAnimal(clickedPawn, assign: false);

            if (building != null)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption(
                        "PS_FarmAnimal".Translate((NamedArgument)clickedPawn.Label, (NamedArgument)building.LabelCap),
                        () =>
                        {
                            Job job = JobMaker.MakeJob(PS_DefOf.PS_CaptureAnimalToFarm, clickedPawn, (LocalTargetInfo)(Thing)building);
                            job.count = 1;
                            context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job);
                        }
                    ),
                    context.FirstSelectedPawn,
                    clickedPawn
                );
            }
            else
            {
                yield return new FloatMenuOption("PS_NoFarm".Translate((NamedArgument)clickedPawn.Label), null);
            }
        }
    }
}
