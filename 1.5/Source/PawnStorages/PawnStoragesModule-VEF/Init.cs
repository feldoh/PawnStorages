using PawnStorages.Farm.Comps;
using Verse;

namespace PawnStorages.VEF;

[StaticConstructorOnStartup]
public class Init
{
    static Init()
    {
        CompFarmProducer.ExtraProductionHandlers.Add(new ProductionHandlerExtraAnimalProduct());
    }
}
