using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(MapPawns), nameof(MapPawns.AllPawnsUnspawned), MethodType.Getter)]
public static class MapPawnsPatch
{
    [HarmonyPostfix]
    public static void AllPawnsUnspawned(ref List<Pawn> __result)
    {
        if (PawnStoragesMod.settings.ShowStoredPawnsInBar) return;
        __result.RemoveAll(pawn => pawn.holdingOwner?.Owner is CompPawnStorage);
    }
}

[StaticConstructorOnStartup]
[HarmonyPatch(typeof(ColonistBarColonistDrawer), "DrawIcons")]
public static class ColonistBarColonistDrawerTranspiler
{
    private static readonly Texture2D Icon_Storage;

    static ColonistBarColonistDrawerTranspiler() => Icon_Storage = ContentFinder<Texture2D>.Get("Things/Item/Special/PawnCrystal");

    public static void AddStorageIcons(List<ColonistBarColonistDrawer.IconDrawCall> iconDrawCalls, Pawn pawn)
    {
        if (pawn.holdingOwner?.Owner is CompPawnStorage) iconDrawCalls.Add(new ColonistBarColonistDrawer.IconDrawCall(Icon_Storage, "PS_ActivityIconStored".Translate()));
    }

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> DrawIcons(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (instruction.opcode == OpCodes.Callvirt && instruction.operand is MethodInfo methodInfo &&
                methodInfo == AccessTools.Method(typeof(List<ColonistBarColonistDrawer.IconDrawCall>), "Clear"))
            {
                yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ColonistBarColonistDrawer), "tmpIconsToDraw"));
                yield return new CodeInstruction(OpCodes.Ldarg_2);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ColonistBarColonistDrawerTranspiler), nameof(AddStorageIcons)));
            }
        }
    }
}
