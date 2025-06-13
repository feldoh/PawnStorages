using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.Jobs;

public class WorkGiver_RopeToFarm : WorkGiver_InteractAnimal
  {
    protected bool targetRoamingAnimals = true;

    public WorkGiver_RopeToFarm() => canInteractWhileSleeping = true;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
      return pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction);
    }

    public override Job JobOnThing(Pawn worker, Thing t, bool forced = false)
    {
      if (t is not Pawn { IsAnimal: true } animal)
        return null;

      if (animal.MentalStateDef != null)
      {
          JobFailReason.Is("CantRopeAnimalMentalState".Translate((NamedArgument) (Thing) animal, (NamedArgument) animal.MentalStateDef.label));
          return null;
      }

      if (animal.Position.IsForbidden(worker))
      {
        JobFailReason.Is("CannotPrioritizeForbiddenOutsideAllowedArea".Translate());
        return null;
      }

      if (t.Map.designationManager.DesignationOn(t, DesignationDefOf.ReleaseAnimalToWild) != null)
        return null;

      Map map = animal.Map;
      FarmJob_MapComponent comp = map.GetComponent<FarmJob_MapComponent>();

      if (comp == null) return null;


      Building building = comp.GetFarmAnimalShouldBeTakenTo(worker, animal, out string _);

      string jobFailReason1 = null;

      if (building != null)
      {
        Job job = MakeJob(animal, building, out jobFailReason1);
        if (job != null)
          return job;
      }

      if (jobFailReason1 != null)
        JobFailReason.Is(jobFailReason1);

      return null;
    }

    private Job MakeUnropeJob(Pawn roper, Pawn animal, bool forced, out string jobFailReason)
    {
      jobFailReason = null;
      if (AnimalPenUtility.RopeAttachmentInteractionCell(roper, animal) == IntVec3.Invalid)
      {
        jobFailReason = "CantRopeAnimalCantTouch".Translate();
        return null;
      }
      if (!forced && !roper.CanReserve((LocalTargetInfo) (Thing) animal))
        return null;
      return !roper.CanReach((LocalTargetInfo) (Thing) animal, PathEndMode.Touch, Danger.Deadly) ? null : JobMaker.MakeJob(JobDefOf.Unrope, (LocalTargetInfo) (Thing) animal);
    }

    public static Job MakeJob(
      Pawn animal,
      Building farm,
      out string jobFailReason)
    {
      jobFailReason = null;
      if (!farm.Position.IsValid)
      {
        jobFailReason = "CantRopeAnimalNoSpace".Translate();
        return null;
      }

      Job job = JobMaker.MakeJob(PS_DefOf.PS_RopeToFarm, (LocalTargetInfo) (Thing) animal, farm.Position, (LocalTargetInfo) (Thing) farm);
      return job;
    }
  }
