using Verse;

namespace PawnStorages.Factory;

public class CompProperties_FactoryProducer : CompProperties
{
    public CompProperties_FactoryProducer() => compClass = typeof(CompFactoryProducer);
}
