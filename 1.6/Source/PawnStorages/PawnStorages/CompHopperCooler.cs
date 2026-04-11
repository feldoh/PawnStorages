using RimWorld;
using Verse;

namespace PawnStorages;

public class CompHopperCooler : ThingComp
{
    public bool IsPowered
    {
        get
        {
            CompPowerTrader power = parent.TryGetComp<CompPowerTrader>();
            return power != null && power.PowerOn;
        }
    }

    public override string CompInspectStringExtra()
    {
        if (IsPowered)
            return "PS_HopperFrozen".Translate();
        return "PS_HopperCoolerNoPower".Translate();
    }
}
