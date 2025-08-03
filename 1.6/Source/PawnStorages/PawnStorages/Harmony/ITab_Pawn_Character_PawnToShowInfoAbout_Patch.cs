using System.Linq;
using HarmonyLib;
using PawnStorages.TickedStorage;
using RimWorld;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(ITab_Pawn_Character), "PawnToShowInfoAbout", MethodType.Getter)]
public static class ITab_Pawn_Character_PawnToShowInfoAbout_Patch
{
    public static bool Prefix(ref Pawn __result)
    {
        CompPawnStorage comp = Find.Selector.SingleSelectedThing.TryGetComp<CompPawnStorage>();
        IPawnListParent holder = comp?.ParentAsPawnListParent;

        if (!(holder?.GetDirectlyHeldThings()?.Any() ?? false))
            return true;
        __result = (Pawn)holder.GetDirectlyHeldThings().First();
        return false;
    }
}
