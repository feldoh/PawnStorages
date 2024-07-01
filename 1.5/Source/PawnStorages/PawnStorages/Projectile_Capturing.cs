using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class Projectile_Capturing : Projectile
{
    public ThingWithComps Equipment;

    public override void Launch(
        Thing launcher,
        Vector3 origin,
        LocalTargetInfo usedTarget,
        LocalTargetInfo intendedTarget,
        ProjectileHitFlags hitFlags,
        bool preventFriendlyFire = false,
        Thing equipment = null,
        ThingDef targetCoverDef = null)
    {
        this.Equipment = (ThingWithComps) equipment;
        base.Launch(launcher,origin,usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, base.targetCoverDef);
    }

    public override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        this.landed = true;
        this.Destroy(DestroyMode.Vanish);

        if (Equipment != null)
        {
            var storageComp = Equipment.GetComp<CompPawnStorage>();
            if ((storageComp.GetDirectlyHeldThings()?.Count ?? 0) > 0)
            {
                Release(hitThing, storageComp);
            }
            else
            {
                Capture(hitThing, blockedByShield);
            }
        }

    }

    public void Capture(Thing hitThing, bool blockedByShield = false)
    {
        if(blockedByShield) return;
        
        if (!(hitThing is Pawn pawn)) return;

        var thing = (ThingWithComps)GenSpawn.Spawn(this.equipmentDef, hitThing.Position, hitThing.Map);

        var storageComp = thing?.GetComp<CompPawnStorage>();
        
        if(storageComp == null) return;
        
        if(!storageComp.CanAssign(pawn, true)) return;

        var launcherPawn = this.launcher as Pawn;

        if (pawn.Faction != launcherPawn?.Faction)
        {
            if (!pawn.Downed) return;
            // GenGuest.EnslavePrisoner(this.launcher as Pawn, pawn);
            pawn.guest?.CapturedBy(launcherPawn?.Faction, launcherPawn);
        }
        
        storageComp.StorePawn(pawn);
        
        Equipment.Destroy(DestroyMode.Vanish);
    }

    public void Release(Thing hitThing, CompPawnStorage storageComp)
    {
        var position = hitThing?.Position ?? this.Position;
        var map = hitThing?.Map ?? Find.CurrentMap;
        
        storageComp.ReleaseContentsAt(map, position);
        var thing = (ThingWithComps)GenSpawn.Spawn(this.equipmentDef, position, map);
        Equipment.Destroy(DestroyMode.Vanish);
    }
    
}