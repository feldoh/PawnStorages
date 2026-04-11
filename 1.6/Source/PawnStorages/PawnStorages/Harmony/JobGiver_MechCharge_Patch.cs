using HarmonyLib;
using PawnStorages.Mech;
using RimWorld;
using Verse;
using Verse.AI;

namespace PawnStorages;

/// <summary>
/// When a colony mech is seeking a charger and has an assigned PS mech storage,
/// redirect it to enter that storage instead of seeking a vanilla mech charger.
/// </summary>
[HarmonyPatch(typeof(JobGiver_GetEnergy_Charger), "TryGiveJob")]
public static class JobGiver_GetEnergy_Charger_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref Job __result, Pawn pawn)
    {
        if (!ModsConfig.BiotechActive || !pawn.IsColonyMech)
            return true;

        // Don't redirect combat mechs to storage during active combat
        if (CompMechStorage.IsCombatMech(pawn) && CompMechStorage.IsMapInCombat(pawn.MapHeld))
            return true;

        CompAssignableToPawn_PawnStorage assignedStorage = PawnStorages_GameComponent.GetAssignedStorage(pawn);
        if (assignedStorage?.parent?.TryGetComp<CompMechStorage>() is not { } mechStorage)
            return true;

        // Only auto-enter when scheduling is enabled
        if (!mechStorage.schedulingEnabled)
            return true;

        // Don't redirect to an unpowered or full storage — let it find a vanilla charger
        CompPowerTrader powerTrader = mechStorage.parent.TryGetComp<CompPowerTrader>();
        if (powerTrader?.PowerOn != true || !mechStorage.CanStore)
            return true;

        Job job = mechStorage.EnterJob(pawn);
        if (job == null)
            return true;

        __result = job;
        return false;
    }
}

/// <summary>
/// When a colony mech is wandering (idle, nothing to do) and has an assigned
/// PS mech storage, redirect it to enter that storage to charge.
/// TryGiveJob is defined on the abstract base JobGiver_Wander, so we patch that
/// and filter to JobGiver_WanderColony instances only.
/// </summary>
[HarmonyPatch(typeof(JobGiver_Wander), "TryGiveJob")]
public static class JobGiver_WanderColony_MechCharge_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref Job __result, Pawn pawn, JobGiver_Wander __instance)
    {
        if (__instance is not JobGiver_WanderColony)
            return;
        if (!ModsConfig.BiotechActive || !pawn.IsColonyMech)
            return;

        // Don't redirect combat mechs to storage during active combat
        if (CompMechStorage.IsCombatMech(pawn) && CompMechStorage.IsMapInCombat(pawn.MapHeld))
            return;

        CompAssignableToPawn_PawnStorage assignedStorage = PawnStorages_GameComponent.GetAssignedStorage(pawn);
        if (assignedStorage?.parent?.TryGetComp<CompMechStorage>() is not { } mechStorage)
            return;

        // Only auto-enter when scheduling is enabled
        if (!mechStorage.schedulingEnabled)
            return;

        // Only redirect if the storage is powered and has space
        CompPowerTrader powerTrader = mechStorage.parent.TryGetComp<CompPowerTrader>();
        if (powerTrader?.PowerOn != true)
            return;
        if (!mechStorage.CanStore)
            return;

        Job job = mechStorage.EnterJob(pawn);
        if (job == null)
            return;

        __result = job;
    }
}
