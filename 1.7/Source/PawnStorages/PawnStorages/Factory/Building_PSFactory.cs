using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using LudeonTK;
using PawnStorages.Interfaces;
using PawnStorages.TickedStorage;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Factory;

public class Building_PSFactory : Building, IStoreSettingsParent, INutritionStorageParent, IProductionParent, IBillGiver, IPawnListParent
{
    public CompPawnStorage pawnStorage;
    public CompPawnStorageNutrition pawnStorageNutrition;
    public CompAssignableToPawn compAssignable;
    public CompFactoryProducer factoryProducer;
    public BillStack billStack;
    public StorageSettings allowedNutritionSettings;

    protected Dictionary<ThingDef, bool> allowedThings;

    public Dictionary<ThingDef, bool> AllowedThings => allowedThings;
    public HashSet<ThingDef> AllowedThingDefs => [.. allowedThings.Keys];

    public bool CurrentlyUsableForBills() => false;

    public bool UsableForBillsAfterFueling() => false;

    public void Notify_BillDeleted(Bill bill) => factoryProducer?.Notify_BillDeleted(bill);

    public BillStack BillStack => billStack;
    public Bill CurrentBill => factoryProducer?.CurrentBill;
    public IEnumerable<IntVec3> IngredientStackCells => [];

    public Building_PSFactory() => billStack = new BillStack(this);

    public bool Allowed(ThingDef potentialDef)
    {
        return allowedThings.TryGetValue(potentialDef, out bool allowed) && allowed;
    }

    public bool IsProducer => factoryProducer != null;

    public bool IsFull => pawnStorage?.IsFull ?? true;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref allowedThings, "allowedThings", LookMode.Def);
        Scribe_Deep.Look(ref billStack, "billStack", this);
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
    private List<RecipeDef> allRecipesCached;

    public List<ThingDef> AllowableThing => Utility.Animals(IsProducer);

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        pawnStorage = GetComp<CompPawnStorage>();
        pawnStorageNutrition = GetComp<CompPawnStorageNutrition>();
        compAssignable = GetComp<CompAssignableToPawn>();
        factoryProducer = GetComp<CompFactoryProducer>();
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
        Designator_Build allowedFactoryHopperDesignator = BuildCopyCommandUtility.FindAllowedDesignator(PS_DefOf.PS_FactoryHopper);
        Designator_Build allowedHopperDesignator = BuildCopyCommandUtility.FindAllowedDesignator(ThingDefOf.Hopper);
        yield return new Command_Action
        {
            defaultLabel = "PS_Factory_RecacheRecipes".Translate(),
            action = RecacheRecipes,
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/PS_ArrowUp"),
        };
        if (allowedFactoryHopperDesignator != null)
            yield return allowedFactoryHopperDesignator;
        if (allowedHopperDesignator != null)
            yield return allowedHopperDesignator;
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
        stringBuilder.AppendLine("PS_CurrentBill".Translate(factoryProducer.CurrentBill?.LabelCap ?? "PS_NoBill".Translate()));
        stringBuilder.AppendLine("PS_NutritionPerDay".Translate(pawnStorage.NutritionRequiredPerDay().ToStringDecimalIfSmall()));
        if (pawnStorageNutrition is { IsPiped: false })
        {
            stringBuilder.AppendLine("PS_AvailableNutrition".Translate(pawnStorageNutrition.StoredNutrition, pawnStorageNutrition.MaxNutrition));
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

    public void Notify_SettingsChanged() { }

    public bool StorageTabVisible => true;

    public bool IsActive => true;

    public void ReleasePawn(Pawn pawn)
    {
        pawnStorage.ReleaseSingle(Map, pawn, true, true);
    }

    public bool HasSuggestiveSilos => false;

    public bool HasStoredPawns => true;
    public List<Pawn> StoredPawns => pawnStorage?.GetDirectlyHeldThings()?.Select(p => p as Pawn).ToList() ?? [];

    public void Notify_NutritionEmpty() => NutritionAvailable = false;

    public void Notify_NutritionNotEmpty() => NutritionAvailable = true;

    public List<Pawn> AllHealthyPawns => StoredPawns.Select(p => p).Where(pawn => !pawn.health.Dead && !pawn.health.Downed).ToList();

    public List<Pawn> ProducingPawns => !IsProducer ? [] : StoredPawns.Select(p => p).Where(pawn => pawn.ageTracker.Adult && !pawn.health.Dead && !pawn.health.Downed).ToList();

    public int BuildingTickInterval => 250;

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, pawnStorage.GetDirectlyHeldThings());
    }

    public static List<RecipeDef> GetAllRecipes() => DefDatabase<RecipeDef>.AllDefsListForReading.Where(t => !t.IsSurgery && t.AvailableNow).ToList();

    private void RecacheRecipes() => allRecipesCached = GetAllRecipes();

    public List<RecipeDef> AllRecipesUnfiltered
    {
        get
        {
            if (allRecipesCached != null)
                return allRecipesCached;
            allRecipesCached = [];
            RecacheRecipes();

            return allRecipesCached;
        }
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (signal == ResearchManager.ResearchCompletedSignal)
        {
            Task.Run(RecacheRecipes);
        }
    }

    public ThingOwner GetDirectlyHeldThings() => pawnStorage?.GetDirectlyHeldThings();

    public bool NeedsDrop() => PawnStoragesMod.settings.AllowNeedsDrop && (pawnStorage == null || pawnStorage.Props.needsDrop);

    public virtual void Notify_PawnAdded(Pawn pawn) => compAssignable?.TryAssignPawn(pawn);

    public virtual void Notify_PawnRemoved(Pawn pawn) { }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        pawnStorage?.ReleaseContents(Map);
        base.Destroy(mode);
    }
}
