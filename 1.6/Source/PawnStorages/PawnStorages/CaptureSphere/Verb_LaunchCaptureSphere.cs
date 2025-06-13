using System;
using RimWorld;
using Verse;

namespace PawnStorages.CaptureSphere;

public class Verb_LaunchCaptureSphere : Verb_LaunchProjectile
{
    public CompPawnStorage PawnStorage
    {
        get
        {
            if (EquipmentSource.TryGetComp<CompPawnStorage>(out CompPawnStorage comp)) return comp;
            
            CompPawnStorage storageComp = (CompPawnStorage) Activator.CreateInstance(typeof(CompPawnStorage));
            storageComp.parent = EquipmentSource;
            EquipmentSource.comps.Add(storageComp);
            return storageComp;
        }
    }

    protected TargetingParameters targetParamsFilled = new()
    {
        canTargetLocations = true,
        canTargetSelf = false,
        canTargetPawns = true,
    };
    public override TargetingParameters targetParams => (PawnStorage.GetDirectlyHeldThings()?.Count ?? 0) > 0 ? targetParamsFilled : verbProps.targetParams;
    
}