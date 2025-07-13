using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(JobGiver_Work), "GetPriority")]
public static class JobGiver_Work_Patch
{
    public static bool Prefix(ref float __result, Pawn pawn)
    {
        TimeAssignmentDef timeAssignmentDef = pawn.timetable == null ? TimeAssignmentDefOf.Anything : pawn.timetable.CurrentAssignment;
        if (timeAssignmentDef != PS_DefOf.PS_Home) return true;
        __result = 0f;
        return false;

    }
}
