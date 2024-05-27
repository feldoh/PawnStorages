﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class Building_Farm : PSBuilding,
    IStoreSettingsParent,
    IThingHolder
{
    public CompPawnStorage storageComp;
    private StorageSettings allowedNutritionSettings;
    public ThingOwner innerContainer;
    private float containedNutrition;

    public Building_Farm()
    { this.innerContainer = (ThingOwner) new ThingOwner<Thing>((IThingHolder) this);
    }
    public override bool ShouldUseAlternative =>
        base.ShouldUseAlternative && !(storageComp?.StoredPawns.NullOrEmpty() ?? true);

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        storageComp = this.TryGetComp<CompPawnStorage>();
        // Set the default rotation
        storageComp.Rotation = Rotation;
        if (storageComp == null)
            Log.Warning($"{this} has null CompPawnStorage even though of type {nameof(Building_PawnStorage)}");
    }

    public override void Tick()
    {
        base.Tick();
        if (storageComp != null && storageComp.StoredPawns.Any())
        {
            this.containedNutrition = Mathf.Clamp(this.containedNutrition - this.NutritionNeeded / 60000f, 0.0f,
                (float)int.MaxValue);
            if ((double)this.containedNutrition <= 0.0)
                this.TryAbsorbNutritiousThing();

        }
    }

    private void TryAbsorbNutritiousThing()
    {
        for (int index = 0; index < this.innerContainer.Count; index++)
        {
            var thing = this.innerContainer.GetAt(index);
            float statValue = thing.GetStatValue(StatDefOf.Nutrition);
            if ((double)statValue > 0.0)
            {
                this.containedNutrition += statValue;
                this.innerContainer[index].SplitOff(1).Destroy();
                break;
            }
        }
    }

    public float NutritionStored
    {
        get
        {
            float containedNutrition = this.containedNutrition;
            containedNutrition +=
                this.innerContainer.Sum(thing => thing.stackCount * thing.GetStatValue(StatDefOf.Nutrition));
            return containedNutrition;
        }
    }

    public float NutritionNeeded
    {
        get
        {
            var needed = 0f;
            foreach (var pawn in this.storageComp.StoredPawns)
            {
                needed += SimplifiedPastureNutritionSimulator.NutritionConsumedPerDay(pawn);
            }

            return needed;
        }
    }

    public StorageSettings GetStoreSettings() => this.allowedNutritionSettings;

    public StorageSettings GetParentStoreSettings() => this.def.building.fixedStorageSettings;

    public void Notify_SettingsChanged()
    {
    }

    public bool StorageTabVisible => false;

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, (IList<Thing>)this.GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings() => this.innerContainer;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look<ThingOwner>(ref this.innerContainer, "innerContainer", (object)this);
        Scribe_Values.Look<float>(ref this.containedNutrition, "containedNutrition");
        Scribe_Deep.Look<StorageSettings>(ref this.allowedNutritionSettings, "allowedNutritionSettings", (object)this);
        if (this.allowedNutritionSettings != null)
            return;
        this.allowedNutritionSettings = new StorageSettings((IStoreSettingsParent)this);
        if (this.def.building.defaultStorageSettings == null)
            return;
        this.allowedNutritionSettings.CopyFrom(this.def.building.defaultStorageSettings);
    }

    public override string GetInspectString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(base.GetInspectString());

        sb.AppendLineIfNotEmpty().Append((string)"Nutrition".Translate()).Append(": ").Append(this.NutritionStored.ToStringByStyle(ToStringStyle.FloatMaxOne));
        if (this.storageComp.StoredPawns.Any())
            sb.Append(" (-").Append((string)"PerDay".Translate((NamedArgument)this.NutritionNeeded.ToString("F1"))).Append(")");

        return sb.ToString();
    }


    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos())
            yield return gizmo;


        foreach (Gizmo gizmo in StorageSettingsClipboard.CopyPasteGizmosFor(this.allowedNutritionSettings))
            yield return gizmo;


        foreach (Thing thing in (IEnumerable<Thing>)this.innerContainer)
        {
            Gizmo gizmo;
            if ((gizmo = Building.SelectContainedItemGizmo((Thing)this, thing)) != null)
                yield return gizmo;
        }

        if (DebugSettings.ShowDevGizmos)
        {
            Command_Action fillAction = new Command_Action();
            fillAction.defaultLabel = "DEV: Fill nutrition";
            fillAction.action = new Action(() =>
            {
                // Create and add food to innerContainer
            });
            yield return (Gizmo)fillAction;
            Command_Action emptyAction = new Command_Action();
            emptyAction.defaultLabel = "DEV: Empty nutrition";
            emptyAction.action = new Action(() =>
            {
                innerContainer.Clear();
            });
            yield return (Gizmo)emptyAction;
        }
    }

    public bool CanAcceptNutrition(Thing thing)
    {
        return this.allowedNutritionSettings.AllowedToAccept(thing);
    }

    public override void PostMake()
    {
        base.PostMake();
        this.allowedNutritionSettings = new StorageSettings((IStoreSettingsParent)this);
        if (this.def.building.defaultStorageSettings == null)
            return;
        this.allowedNutritionSettings.CopyFrom(this.def.building.defaultStorageSettings);
    }
}