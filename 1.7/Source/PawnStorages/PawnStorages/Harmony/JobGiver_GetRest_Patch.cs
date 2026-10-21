using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages;

[HarmonyPatch(typeof(JobGiver_GetRest), "GetPriority")]
public static class JobGiver_GetRest_Patch
{
    public static bool Prefix(ref float __result, Pawn pawn)
    {
        TimeAssignmentDef timeAssignmentDef = pawn.timetable == null ? TimeAssignmentDefOf.Anything : pawn.timetable.CurrentAssignment;
        if (timeAssignmentDef != PS_DefOf.PS_Home)
            return true;
        __result = 0f;
        return false;
    }
}

[HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
public static class JobGiver_GetRest_FloorSleepPatch
{
    [HarmonyPostfix]
    public static void Postfix(ref Job __result, Pawn pawn)
    {
        if (__result == null || !pawn.Spawned || PawnStorages_GameComponent.GetAssignedStorage(pawn) is not { } storage)
            return;
        CompPawnStorage compPawnStorage = storage.parent.TryGetComp<CompPawnStorage>();
        if (compPawnStorage?.schedulingEnabled != true)
            return;
        Job job = compPawnStorage.EnterJob(pawn);
        if (job != null)
            __result = job;
    }
}
