using RimWorld;
using Verse;

namespace PawnStorages;

public class Verb_LaunchCaptureSphere : Verb_LaunchProjectile
{
    public CompPawnStorage PawnStorage => EquipmentSource.GetComp<CompPawnStorage>();

    protected TargetingParameters targetParamsFilled = new()
    {
        canTargetLocations = true,
        canTargetSelf = false,
        canTargetPawns = true,
    };
    public override TargetingParameters targetParams => (PawnStorage.GetDirectlyHeldThings()?.Count ?? 0) > 0 ? targetParamsFilled : verbProps.targetParams;
    
}