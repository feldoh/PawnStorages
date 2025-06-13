using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(TradeUtility), "GetPricePlayerBuy")]
public static class TradeUtilityBuy_Patch
{
    [HarmonyPostfix]
    public static float Postfix(float __result, Thing thing, float priceFactorBuy_TraderPriceType,
        float priceFactorBuy_JoinAs,
        float priceGain_PlayerNegotiator,
        float priceGain_FactionBase)
    {
        return __result + (thing.GetInnerIfMinified().TryGetComp<CompPawnStorage>() is { } holder
            ? holder.GetDirectlyHeldThings().Select(p => TradeUtility.GetPricePlayerBuy(p, priceFactorBuy_TraderPriceType,
                priceFactorBuy_JoinAs,
                priceGain_PlayerNegotiator,
                priceGain_FactionBase)).Sum()
            : 0);
    }
}
