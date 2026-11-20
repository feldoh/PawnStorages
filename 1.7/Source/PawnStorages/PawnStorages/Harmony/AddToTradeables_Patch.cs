using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnStorages;

[HarmonyPatch(typeof(TradeDeal), "AddToTradeables")]
public static class AddToTradeables_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(Thing t, Transactor trans, List<Tradeable> ___tradeables)
    {
        Tradeable tradeable = TransferableUtility.TradeableMatching(t, ___tradeables);
        CompPawnStorage storageComp = t.GetInnerIfMinified().TryGetComp<CompPawnStorage>();
        if (tradeable == null && storageComp != null)
        {
            tradeable = new Tradeable_StoredPawn();
            ___tradeables.Add(tradeable);
        }

        tradeable?.AddThing(t, trans);
        return tradeable == null;
    }
}
