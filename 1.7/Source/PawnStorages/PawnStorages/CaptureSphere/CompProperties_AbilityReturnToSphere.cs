using RimWorld;
using Verse;

namespace PawnStorages.CaptureSphere;

public class CompProperties_AbilityReturnToSphere : CompProperties_AbilityEffect
{
    public EffecterDef Effector;
    public ThingDef SphereDef;

    public CompProperties_AbilityReturnToSphere() => this.compClass = typeof(CompAbilityReturnToSphere);
}
