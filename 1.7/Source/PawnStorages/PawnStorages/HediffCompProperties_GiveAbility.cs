using Verse;

namespace PawnStorages;

public class HediffCompProperties_GiveAbilitySafe : HediffCompProperties_GiveAbility
{
    public HediffCompProperties_GiveAbilitySafe() => compClass = typeof(HediffComp_GiveAbilitySafe);
}
