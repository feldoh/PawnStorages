using System.Collections.Generic;
using PawnStorages.Farm.Comps;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages;

public class FarmJob_MapComponent(Map map) : MapComponent(map)
{
    public Dictionary<Pawn, Building> farmAssignments = new Dictionary<Pawn, Building>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref farmAssignments, "farmStorageAssignments", LookMode.Reference, LookMode.Reference);
    }

    public Building GetFarmAnimalShouldBeTakenTo(
        Pawn roper,
        Pawn animal,
        out string jobFailReason,
        bool forced = false)
    {
        jobFailReason = null;
        if (animal == null || !farmAssignments.ContainsKey(animal)) return null;
        if (animal == roper)
            return null;
        if (animal.Faction != roper.Faction)
            return null;
        if (!forced && animal.roping.IsRopedByPawn && animal.roping.RopedByPawn != roper)
            return null;
        if (AnimalPenUtility.RopeAttachmentInteractionCell(roper, animal) == IntVec3.Invalid)
        {
            jobFailReason = "CantRopeAnimalCantTouch".Translate();
            return null;
        }
        if (!forced && !roper.CanReserve((LocalTargetInfo) (Thing) animal))
            return null;
        if (!WorkGiver_InteractAnimal.CanInteractWithAnimal(roper, animal, out jobFailReason, forced, true, true, true))
            return null;

        return farmAssignments[animal];
    }
}
