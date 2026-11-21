using RimWorld;
using Verse;

namespace PawnStorages;

public class CompHopperCooler : ThingComp
{
    public bool IsPowered => parent.TryGetComp<CompPowerTrader>() is { PowerOn: true };

    public override string CompInspectStringExtra() => IsPowered ? "PS_HopperFrozen".Translate() : "PS_HopperCoolerNoPower".Translate();
}
