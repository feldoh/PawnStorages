using System.Collections.Generic;
using PawnStorages.Farm;
using PawnStorages.Farm.Comps;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages.Jobs;

public class JobDriver_RopeToFarm: JobDriver_RopeToDestination
{
    protected Building_PSFarm Farm => TargetThingC as Building_PSFarm;
    protected CompFarmStorage Comp => Farm?.GetComp<CompFarmStorage>();

    public PawnStorages_GameComponent PawnStoragesGameComponent => Current.Game.GetComponent<PawnStorages_GameComponent>();


    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.C);
        return base.MakeNewToils();
    }

    public override bool HasRopeeArrived(Pawn ropee, bool roperWaitingAtDest)
    {
        if (!Farm.Position.IsValid || !pawn.Position.InHorDistOf(Farm.Position, 2f))
            return false;
        District district = Farm.Position.GetDistrict(pawn.Map);
        return district == pawn.GetDistrict() && district == ropee.GetDistrict();
    }

    public override void ProcessArrivedRopee(Pawn ropee)
    {
        if(Comp == null) return;
        if(!Comp.CanAssign(ropee)) return;
        Map map = ropee.Map;
        Comp.TryAssignPawn(ropee);
        Comp.StorePawn(ropee);

        // So they're not put straight back in if ejected
        FarmJob_MapComponent comp = map.GetComponent<FarmJob_MapComponent>();
        if(comp.farmAssignments.ContainsKey(ropee))
            comp.farmAssignments.Remove(ropee);
    }

    public override bool ShouldOpportunisticallyRopeAnimal(Pawn animal)
    {
        if (animal.roping.RopedByPawn == pawn)
            return false;

        if (!animal.Spawned) return false;

        FarmJob_MapComponent comp = animal.Map.GetComponent<FarmJob_MapComponent>();

        if (comp == null) return false;

        Building dest = comp.GetFarmAnimalShouldBeTakenTo(pawn, animal, out string _);

        if (!comp.farmAssignments.ContainsKey(animal)) return false;

        return dest != null && Farm == dest;
    }
}
