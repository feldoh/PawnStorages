using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(TradeUtility), "GetPricePlayerSell")]
public static class TradeUtilitySell_Patch
{
    [HarmonyPostfix]
    public static float Postfix(float __result, Thing thing,
        float priceFactorSell_TraderPriceType,
        float priceFactorSell_HumanPawn,
        float priceGain_PlayerNegotiator,
        float priceGain_FactionBase,
        float priceGain_DrugBonus,
        float priceGain_AnimalProduceBonus,
        TradeCurrency currency)
    {
        return __result + (thing.GetInnerIfMinified().TryGetComp<CompPawnStorage>() is { } holder
            ? holder.GetDirectlyHeldThings().Select(p => TradeUtility.GetPricePlayerSell(p, priceFactorSell_TraderPriceType,
                priceFactorSell_HumanPawn,
                priceGain_PlayerNegotiator,
                priceGain_FactionBase,
                priceGain_DrugBonus,
                priceGain_AnimalProduceBonus,
                currency)).Sum()
            : 0);
    }
}
