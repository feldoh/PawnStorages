using System.Collections.Generic;
using System.Linq;
using System.Text;
using PawnStorages.Farm.Comps;
using PawnStorages.Farm.Interfaces;
using PawnStorages.TickedStorage;
using PawnStorages.Interfaces;
using RimWorld;
using Verse;

namespace PawnStorages.Farm;

public class Building_PSFarm : Building, IStoreSettingsParent, INutritionStorageParent, IBreederParent, IProductionParent, IFarmTabParent, IPawnListParent
{
    public CompFarmStorage pawnStorage;
    public CompPawnStorageNutrition FarmNutrition;
    public CompFarmBreeder FarmBreeder;
    public CompFarmProducer FarmProducer;
    private StorageSettings allowedNutritionSettings;

    protected Dictionary<ThingDef, bool> allowedThings;

    public Dictionary<ThingDef, bool> AllowedThings => allowedThings;
    public HashSet<ThingDef> AllowedThingDefs => [..allowedThings.Keys];

    public bool Allowed(ThingDef potentialDef)
    {
        return allowedThings.GetValueOrDefault(potentialDef, false);
    }

    public bool IsBreeder => FarmBreeder != null;
    public bool IsProducer => FarmProducer != null;

    public bool IsFull => pawnStorage?.IsFull ?? true;

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref allowedThings, "allowedThings", LookMode.Def);
        base.ExposeData();
    }

    public void AllowAll()
    {
        foreach (ThingDef allowedThingsKey in AllowedThingDefs)
        {
            AllowedThings[allowedThingsKey] = true;
        }
    }

    public void DenyAll()
    {
        foreach (ThingDef allowedThingsKey in AllowedThingDefs)
        {
            AllowedThings[allowedThingsKey] = false;
        }
    }

    public bool NutritionAvailable = true;

    public List<ThingDef> AllowableThing => Utility.Animals(IsProducer);

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        pawnStorage = GetComp<CompFarmStorage>();
        FarmNutrition = GetComp<CompPawnStorageNutrition>();
        FarmBreeder = GetComp<CompFarmBreeder>();
        FarmProducer = GetComp<CompFarmProducer>();
        base.SpawnSetup(map, respawningAfterLoad);
        allowedNutritionSettings = new StorageSettings(this);
        if (def.building.defaultStorageSettings == null)
            return;
        allowedNutritionSettings.CopyFrom(def.building.defaultStorageSettings);

        allowedThings ??= new Dictionary<ThingDef, bool>();

        foreach (ThingDef thingDef in AllowableThing.Where(t => !AllowedThingDefs.Contains(t)))
        {
            AllowedThings[thingDef] = true;
        }
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
            yield return gizmo;
        Designator_Build allowedHopperDesignator = BuildCopyCommandUtility.FindAllowedDesignator(ThingDefOf.Hopper);
        Designator_Build allowedFarmHopperDesignator = BuildCopyCommandUtility.FindAllowedDesignator(PS_DefOf.PS_FarmHopper);
        if (allowedHopperDesignator != null) yield return allowedHopperDesignator;
        if (allowedFarmHopperDesignator != null) yield return allowedFarmHopperDesignator;
        foreach (Thing thing in (IEnumerable<Thing>)StoredPawns)
        {
            Gizmo gizmo;
            if ((gizmo = SelectContainedItemGizmo(thing, thing)) != null)
                yield return gizmo;
        }
    }

    public override string GetInspectString()
    {
        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine(base.GetInspectString());
        stringBuilder.AppendLine("PS_NutritionPerDay".Translate(pawnStorage.NutritionRequiredPerDay().ToStringDecimalIfSmall()));
        if (FarmNutrition is { IsPiped: false } || FarmNutrition.storedNutrition > 0)
        {
            stringBuilder.AppendLine("PS_AvailableNutrition".Translate(FarmNutrition.storedNutrition, FarmNutrition.TargetNutritionLevel));
        }
        return stringBuilder.ToString().Trim();
    }

    public StorageSettings GetStoreSettings()
    {
        return allowedNutritionSettings;
    }

    public StorageSettings GetParentStoreSettings()
    {
        return def.building.fixedStorageSettings;
    }

    public void Notify_SettingsChanged()
    {
    }

    public bool StorageTabVisible => true;

    public bool IsActive => true;

    public void ReleasePawn(Pawn pawn)
    {
        pawnStorage.ReleaseSingle(Map, pawn, true, true);
    }

    public bool HasSuggestiveSilos => true;
    public bool HasStoredPawns => true;
    public List<Pawn> StoredPawns => pawnStorage?.GetDirectlyHeldThings()?.Select(p => p as Pawn).ToList() ?? [];

    public void Notify_NutritionEmpty() => NutritionAvailable = false;

    public void Notify_NutritionNotEmpty() => NutritionAvailable = true;

    public List<Pawn> AllHealthyPawns
    {
        get
        {
            return StoredPawns.Select(p => p).Where(pawn => !pawn.health.Dead && !pawn.health.Downed)
                .ToList();
        }
    }

    public List<Pawn> BreedablePawns
    {
        get
        {
            return !IsBreeder
                ? []
                : StoredPawns.Select(p => p).Where(pawn => pawn.ageTracker.Adult && !pawn.health.Dead && !pawn.health.Downed)
                    .ToList();
        }
    }

    public List<Pawn> ProducingPawns
    {
        get
        {
            return !IsProducer
                ? []
                : StoredPawns.Select(p => p)
                    .Where(pawn => pawn.ageTracker.Adult && !pawn.health.Dead && !pawn.health.Downed)
                    .ToList();
        }
    }

    public int TickInterval => 250;

    public void Notify_PawnBorn(Pawn newPawn)
    {
        if(newPawn.Spawned)
            newPawn.DeSpawn();

        if (pawnStorage.innerContainer.Count >= pawnStorage.MaxStoredPawns())
        {
            Thing storageParent = pawnStorage.parent;

            Messages.Message("PS_StorageFull".Translate(storageParent.LabelCap, newPawn.LabelCap), (Thing) newPawn, MessageTypeDefOf.NeutralEvent);

            PawnComponentsUtility.AddComponentsForSpawn(newPawn);
            pawnStorage.compAssignable?.TryUnassignPawn(newPawn);
            GenDrop.TryDropSpawn(newPawn, storageParent.Position, storageParent.Map, ThingPlaceMode.Near, out Thing _);
            FilthMaker.TryMakeFilth(storageParent.Position, storageParent.Map, ThingDefOf.Filth_Slime, new IntRange(3, 6).RandomInRange);
            return;
        }

        pawnStorage.StorePawn(newPawn, false);
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, pawnStorage.GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return pawnStorage?.GetDirectlyHeldThings();
    }

    public bool NeedsDrop()
    {
        return PawnStoragesMod.settings.AllowNeedsDrop && (pawnStorage == null || pawnStorage.Props.needsDrop);
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        pawnStorage?.ReleaseContents(Map);
        base.Destroy(mode);
    }

    public void Notify_PawnAdded(Pawn pawn)
    {
    }

    public void Notify_PawnRemoved(Pawn pawn)
    {
    }
}
