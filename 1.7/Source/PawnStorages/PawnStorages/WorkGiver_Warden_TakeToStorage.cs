using System.Collections.Generic;
using System.Linq;
using PawnStorages.Farm;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace PawnStorages;

public class WorkGiver_Warden_TakeToStorage : WorkGiver_Warden
{
    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return !ShouldTakeCareOfPrisoner(pawn, t) ? null : TryMakeJob(pawn, t, forced);
    }

    public static Job TryMakeJob(Pawn pawn, Thing t, bool forced = false)
    {
        Pawn prisoner = (Pawn)t;
        if (!forced || prisoner.CurJobDef == PS_DefOf.PS_Enter)
            return null;
        Job job2 = TakeToPreferredStorageJob(prisoner, pawn);
        return job2;
    }

    private static Job TakeToPreferredStorageJob(Pawn prisoner, Pawn warden)
    {
        if (
            !prisoner.Spawned
            || prisoner.InAggroMentalState
            || prisoner.IsForbidden(warden)
            || prisoner.IsFormingCaravan()
            || !warden.CanReserveAndReach(prisoner, PathEndMode.OnCell, warden.NormalMaxDanger(), 1, -1, null, ignoreOtherReservations: true)
        )
        {
            return null;
        }

        if (GetStorageForPawn(prisoner) is not { } storage)
        {
            return null;
        }

        Job job = JobMaker.MakeJob(PS_DefOf.PS_CaptureInPawnStorage, prisoner, storage);
        job.count = 1;
        return job;
    }

    public static IEnumerable<CompAssignableToPawn_PawnStorage> GetPossibleStorages(Pawn pawn, bool asPrisoner = true, Def ofDef = null)
    {
        BedOwnerType bedOwnerType = BedOwnerType.Prisoner;
        if (pawn.Faction == Faction.OfPlayer)
            bedOwnerType = BedOwnerType.Colonist;

        return PawnStorages_GameComponent.CompAssignables.Where(c =>
            c.parent is not Building_PSFarm
            && (ofDef == null || c.parent.def == ofDef)
            && ((c.HasFreeSlot && (!asPrisoner || c.OwnerType == bedOwnerType)) || c.assignedPawns.Contains(pawn))
        );
    }

    public static ThingWithComps GetStorageForPawn(Pawn prisoner, bool assign = false, CompAssignableToPawn_PawnStorage preferredStorage = null) =>
        GetStorageGeneral(prisoner, assign: assign, asPrisoner: true, preferredStorage: preferredStorage);

    public static ThingWithComps GetStorageGeneral(Pawn prisoner, bool assign = false, bool asPrisoner = true, CompAssignableToPawn_PawnStorage preferredStorage = null)
    {
        Def preferredDef = preferredStorage?.parent?.def;
        if (preferredStorage is not null)
        {
            if (preferredStorage.HasFreeSlot && assign)
                preferredStorage.TryAssignPawn(prisoner);
            if ((!assign && preferredStorage.HasFreeSlot) || preferredStorage.AssignedPawns.Contains(prisoner))
                return preferredStorage.parent;
        }

        ThingWithComps existingAssigned = PawnStorages_GameComponent
            .CompAssignables.FirstOrDefault(c => c.parent is not Building_PSFarm && c.assignedPawns.Contains(prisoner))
            ?.parent;
        if (existingAssigned != null)
        {
            return existingAssigned;
        }

        if (GetPossibleStorages(prisoner, asPrisoner: asPrisoner, ofDef: preferredDef).FirstOrDefault() is not { } assignable)
            return null;

        if (assign)
            assignable.TryAssignPawn(prisoner);
        return assignable.parent;
    }

    public static ThingWithComps GetStorageForFarmAnimal(Pawn prisoner, bool assign = false, bool breeding = false)
    {
        ThingWithComps existingAssigned = PawnStorages_GameComponent
            .CompAssignables.FirstOrDefault(c => c.parent is Building_PSFarm farm && farm.IsBreeder == breeding && c.assignedPawns.Contains(prisoner))
            ?.parent;
        if (existingAssigned != null)
        {
            return existingAssigned;
        }

        CompAssignableToPawn_PawnStorage assignable = PawnStorages_GameComponent.CompAssignables.FirstOrDefault(c =>
            c.parent is Building_PSFarm { IsFull: false } farm && farm.Allowed(prisoner.def) && (farm.IsBreeder == breeding)
        );
        if (assignable == null)
            return null;
        if (assign)
            assignable.TryAssignPawn(prisoner);
        return assignable.parent;
    }
}
