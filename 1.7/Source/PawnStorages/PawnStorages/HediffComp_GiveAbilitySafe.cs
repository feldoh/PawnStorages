using RimWorld;
using Verse;

namespace PawnStorages;

public class HediffComp_GiveAbilitySafe : HediffComp_GiveAbility
{
    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        parent.pawn.abilities ??= new Pawn_AbilityTracker(parent.pawn);
        base.CompPostPostAdd(dinfo);
    }
}
