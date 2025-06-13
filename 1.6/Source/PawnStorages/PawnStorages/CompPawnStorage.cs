using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using PawnStorages.TickedStorage;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PawnStorages;

public class CompPawnStorage : ThingComp, IThingHolder
{
    public ThingOwner<Pawn> innerContainer;
    public const int TICKRATE = 60;
    public const int NEEDS_INTERVAL = 150;

    public CompAssignableToPawn_PawnStorage compAssignable;
    public bool labelDirty = true;
    public int chargesRemaining;
    public bool schedulingEnabled;
    public List<Pawn> storedPawns = []; // Deprecated, to remove in next major version
    private Dictionary<int, int> pawnStoringTick = new();
    public string transformLabelCache;

    public Rot4 Rotation = Rot4.North.Opposite;

    public IPawnListParent ParentAsPawnListParent => parent as IPawnListParent;

    public CompPawnStorage()
    {
        innerContainer = new ThingOwner<Pawn>(this);
    }

    public CompProperties_PawnStorage Props => props as CompProperties_PawnStorage;
    public virtual int MaxStoredPawns() => Props.MaxStoredPawns;

    public bool IsFull => (GetDirectlyHeldThings()?.Count ?? 0) >= MaxStoredPawns();
    public bool CanStore => innerContainer.Count < MaxStoredPawns();

    public bool CanAssign(Pawn pawn, bool couldMakePrisoner) =>
        compAssignable?.OwnerType switch
        {
            BedOwnerType.Colonist => pawn.IsColonist,
            BedOwnerType.Slave => pawn.IsSlave,
            BedOwnerType.Prisoner => pawn.IsPrisoner || couldMakePrisoner,
            _ => true
        } && (compAssignable == null || compAssignable.AssignedPawns.Contains(pawn) || compAssignable.HasFreeSlot);

    public void TryAssignPawn(Pawn pawn) => compAssignable?.TryAssignPawn(pawn);

    public void SetLabelDirty() => labelDirty = true;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        compAssignable = parent.TryGetComp<CompAssignableToPawn_PawnStorage>();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref pawnStoringTick, "pawnStoringTick", LookMode.Value, LookMode.Value);
        Scribe_Values.Look(ref schedulingEnabled, "schedulingEnabled");
        Scribe_Values.Look(ref chargesRemaining, "chargesRemaining");
        Scribe_Values.Look(ref Rotation, "Rotation");

        // Deprectated - To Remove in next major version
        Scribe_Collections.Look(ref storedPawns, "storedPawns", LookMode.Deep);
        Scribe_Deep.Look(ref innerContainer, "innerContainer", this);

        if (storedPawns?.Any() ?? false)
        {
            innerContainer ??= new ThingOwner<Pawn>(this);
            foreach (Pawn pawn in storedPawns)
            {
                innerContainer.TryAdd(pawn);
            }

            storedPawns.Clear();
        }

        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        pawnStoringTick ??= new Dictionary<int, int>();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        List<Pawn> innerContainerInnerListForReading = innerContainer.InnerListForReading.ToList();
        foreach (Pawn pawn in innerContainerInnerListForReading)
        {
            GenSpawn.Spawn(pawn, parent.Position, previousMap);
        }

        innerContainer.Clear();
        base.PostDestroy(mode, previousMap);
    }


    public override string TransformLabel(string label)
    {
        if (!labelDirty) return transformLabelCache;
        if (innerContainer.NullOrEmpty<Pawn>())
        {
            transformLabelCache = $"{base.TransformLabel(label)} {"PS_Empty".Translate()}";
        }
        else if (CanStore)
        {
            transformLabelCache = $"{base.TransformLabel(label)}";
        }
        else
        {
            transformLabelCache = Props.MaxStoredPawns == 1
                ? $"{base.TransformLabel(label)} ({innerContainer.innerList.First().Name})"
                : $"{base.TransformLabel(label)} {"PS_Filled".Translate()}";
        }

        labelDirty = false;

        return transformLabelCache;
    }

    public override bool AllowStackWith(Thing other)
    {
        return innerContainer.NullOrEmpty<Pawn>()
               && base.AllowStackWith(other)
               && (other.TryGetComp<CompPawnStorage>()?.innerContainer?.NullOrEmpty<Pawn>() ?? true);
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        foreach (FloatMenuOption f in base.CompFloatMenuOptions(selPawn)) yield return f;
        bool requiresStation = Utility.CheckStation(this, selPawn, out Thing station);

        if (Props.convertOption && CanStore)
        {
            if (!requiresStation || station != null)
            {
                yield return new FloatMenuOption("PS_Enter".Translate(), delegate
                {
                    Job job = EnterJob(selPawn);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            }
            else
            {
                yield return new FloatMenuOption("PS_NoEnter".Translate(Props.storageStation.label, parent.Label), null);
            }
        }

        if (!innerContainer.Any<Pawn>()) yield break;
        {
            if (Props.releaseAllOption && !(Props.releaseOption && innerContainer.Count == 1))
                yield return new FloatMenuOption("PS_ReleaseAll".Translate(), delegate
                {
                    Job job = JobMaker.MakeJob(PS_DefOf.PS_Release, parent);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                });
            if (!Props.releaseOption || !CanRelease(selPawn)) yield break;
            foreach (Pawn pawn in innerContainer)
            {
                Job releaseJob = ReleaseJob(selPawn, pawn);
                if (releaseJob != null)
                {
                    yield return new FloatMenuOption("PS_Release".Translate(pawn.LabelCap), delegate { selPawn.jobs.TryTakeOrderedJob(releaseJob, JobTag.Misc); });
                }
                else
                {
                    if (requiresStation)
                    {
                        yield return new FloatMenuOption((station == null ? "PS_NoStationForRelease" : "PS_StationButNoRelease")
                            .Translate(Props.storageStation.label, pawn.LabelCap), null);
                    }
                    else
                    {
                        yield return new FloatMenuOption("PS_NoRelease".Translate(pawn.LabelCap), null);
                    }
                }
            }
        }
    }

    public override void CompTick()
    {
        if (Find.TickManager.TicksGame % TICKRATE != 0) return;
        if (Props.idleResearch && Find.ResearchManager.currentProj != null)
            foreach (Pawn pawn in storedPawns)
                if (pawn.RaceProps.Humanlike)
                {
                    float value = pawn.GetStatValue(StatDefOf.ResearchSpeed);
                    value *= 0.5f;
                    Find.ResearchManager.ResearchPerformed(value * TICKRATE, pawn);
                    pawn.skills.Learn(SkillDefOf.Intellectual, 0.1f * TICKRATE);
                }

        if (!schedulingEnabled || compAssignable == null) return;
        {
            foreach (Pawn pawn in compAssignable.AssignedPawns)
                switch (pawn.Spawned)
                {
                    case true when pawn.timetable?.CurrentAssignment == PS_DefOf.PS_Home &&
                                   pawn.CurJobDef != PS_DefOf.PS_Enter &&
                                   pawn.health.State == PawnHealthState.Mobile &&
                                   !pawn.CurJob.restUntilHealed &&
                                   !pawn.Drafted &&
                                   !HealthAIUtility.ShouldSeekMedicalRest(pawn):
                        {
                            Job job = EnterJob(pawn);
                            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                            break;
                        }
                    case false when innerContainer.Contains(pawn) &&
                                    pawn.timetable?.CurrentAssignment is { } assignmentDef &&
                                    assignmentDef != PS_DefOf.PS_Home &&
                                    ShouldWake(pawn):
                        ReleasePawn(pawn, parent.Position, parent.Map);
                        break;
                }
        }
        base.CompTick();
    }

    //Funcs
    public bool ShouldWake(Pawn pawn)
    {
        if (pawn.needs?.rest is not { } restNeed) return true;
        if (pawn.timetable?.CurrentAssignment == TimeAssignmentDefOf.Sleep) return false;
        int storedAtTick = pawnStoringTick.TryGetValue(pawn.thingIDNumber, -1);
        if (storedAtTick < 0) pawnStoringTick.Add(pawn.thingIDNumber, storedAtTick = Find.TickManager.TicksGame);
        int ticksStored = Find.TickManager.TicksGame - storedAtTick;
        float restLevel = restNeed.CurLevel + Props.pawnRestIncreaseTick * ticksStored;
        if (pawn.timetable?.CurrentAssignment == TimeAssignmentDefOf.Anything && restLevel < Need_Rest.DefaultNaturalWakeThreshold) return false;
        return restLevel > Need_Rest.CanWakeThreshold;
    }

    public void ReleasePawn(Pawn pawn, IntVec3 cell, Map map)
    {
        Utility.ReleasePawn(this, pawn, cell, map);
    }

    public bool RequiresStation() => (parent.def?.EverHaulable ?? false) &&
                                     parent.def?.category == ThingCategory.Item &&
                                     Props?.storageStation != null;

    public virtual void ApplyNeedsForStoredPeriodFor(Pawn pawn)
    {
        int storedAtTick = pawnStoringTick.TryGetValue(pawn.thingIDNumber, -1);
        pawnStoringTick.Remove(pawn.thingIDNumber);
        if (storedAtTick <= 0 || !PawnStoragesMod.settings.AllowNeedsDrop) return;

        // We drop one tick interval to make sure we don't boost need drops from being at home, the slight reduction can be seen as a benefit of being at home.
        int actuallyStoredTicks = Find.TickManager.TicksGame - storedAtTick;
        int ticksStored = Mathf.Max(0, actuallyStoredTicks - NEEDS_INTERVAL);
        if (Props.pawnRestIncreaseTick != 0 && pawn.needs?.rest is { } restNeed)
        {
            restNeed.lastRestTick = Find.TickManager.TicksGame;
            restNeed.CurLevel += Props.pawnRestIncreaseTick * ticksStored;
            int periodsStored = Math.Max(ticksStored / NEEDS_INTERVAL, 1);
            IEnumerable<Hediff_Injury> hds = pawn.health.tmpHediffInjuries.TakeRandom(Math.Min(pawn.health.tmpHediffInjuries.Count, periodsStored)).ToList();
            if (hds.Any())
            {
                float multiplierPerInjury = periodsStored / (float) hds.Count();
                foreach (Hediff_Injury hd in hds)
                {
                    hd.Heal((float) (multiplierPerInjury * pawn.HealthScale * 0.04) * pawn.GetStatValue(StatDefOf.InjuryHealingFactor));
                }
            }
        }

        if (!Props.needsDrop) return;

        CompPawnStorageNutrition nutritionComp = parent.TryGetComp<CompPawnStorageNutrition>();
        if (nutritionComp == null) pawn.ageTracker?.AgeTickMothballed(actuallyStoredTicks); // this comp has already taken care of age so only apply if there isn't one
        foreach (Need need in pawn.needs?.AllNeeds ?? [])
        {
            switch (need)
            {
                case Need_Food foodNeed:
                    if (nutritionComp != null) continue; // this comp has already taken care of food
                    foodNeed.CurLevel -= foodNeed.FoodFallPerTick * ticksStored;
                    continue;
                case Need_Chemical chemicalNeed:
                    chemicalNeed.CurLevel -= chemicalNeed.ChemicalFallPerTick * ticksStored;
                    continue;
                case Need_Chemical_Any { Disabled: false } chemicalNeedAny:
                    chemicalNeedAny.CurLevel -= chemicalNeedAny.FallPerNeedIntervalTick / NEEDS_INTERVAL * ticksStored;
                    continue;
            }
        }
    }

    public bool TryTransferPawn(ThingWithComps otherStore, Pawn pawn)
    {
        if (!otherStore.TryGetComp<CompPawnStorage>(out var comp)) return false;
        TransferPawn(comp, pawn);
        return true;
    }

    public void TransferPawn(CompPawnStorage otherStore, Pawn pawn)
    {
        innerContainer.Remove(pawn);
        otherStore.innerContainer.TryAddOrTransfer(pawn);
        if (otherStore.compAssignable != null && !otherStore.compAssignable.AssignedPawns.Contains(pawn)) otherStore.compAssignable.TryAssignPawn(pawn);
        labelDirty = true;
        otherStore.labelDirty = true;

        otherStore.pawnStoringTick.SetOrAdd(pawn.thingIDNumber, Find.TickManager.TicksGame);

        ApplyNeedsForStoredPeriodFor(pawn);
        Notify_ReleasedFromStorage(pawn);
        otherStore.Notify_AddedToStorage(pawn);
    }

    public void StorePawn(Pawn pawn, bool effects = true)
    {
        Map pawnMap = pawn.Map;
        if (effects && Props.lightEffect) FleckMaker.ThrowLightningGlow(pawn.Position.ToVector3Shifted(), pawnMap, 0.5f);
        if (effects && Props.transformEffect) FleckMaker.ThrowExplosionCell(pawn.Position, pawnMap, FleckDefOf.ExplosionFlash, Color.white);

        //Spawn the store effecter
        if (effects) Props.storeEffect?.Spawn(pawn.Position, pawnMap);
        if (pawn.Spawned)
            pawn.DeSpawn();

        innerContainer.TryAdd(pawn);

        pawnMap?.mapDrawer?.MapMeshDirty(parent.Position, MapMeshFlagDefOf.Things);

        if (compAssignable != null && !compAssignable.AssignedPawns.Contains(pawn)) compAssignable.TryAssignPawn(pawn);
        labelDirty = true;
        pawnStoringTick.SetOrAdd(pawn.thingIDNumber, Find.TickManager.TicksGame);
        Notify_AddedToStorage(pawn);
    }

    public virtual bool CanRelease(Pawn releaser)
    {
        return Utility.CanRelease(this, releaser);
    }

    public virtual Job ReleaseJob(Pawn releaser, Pawn toRelease)
    {
        return Utility.ReleaseJob(this, releaser, toRelease);
    }


    public virtual Job EnterJob(Pawn enterer)
    {
        //Check is storage is item with station
        if (parent.def.EverHaulable && parent.def.category == ThingCategory.Item && Props.storageStation != null)
        {
            Thing station = GenClosest.ClosestThingReachable(enterer.Position, enterer.Map, ThingRequest.ForDef(Props.storageStation), PathEndMode.InteractionCell,
                TraverseParms.For(enterer), 9999f, x => enterer.CanReserve(x));
            Job job = JobMaker.MakeJob(PS_DefOf.PS_Enter, parent, station);
            job.count = 1;
            return job;
        }

        //Store in side parent directly
        return JobMaker.MakeJob(PS_DefOf.PS_Enter, parent);
    }

    public override IEnumerable<FloatMenuOption> CompMultiSelectFloatMenuOptions(IEnumerable<Pawn> selPawns)
    {
        List<Pawn> selectedPawns = selPawns.ToList();
        if (Props.convertOption && MaxStoredPawns() > innerContainer.Count)
            yield return new FloatMenuOption("PS_Enter".Translate(), delegate
            {
                var diff = MaxStoredPawns() - innerContainer.Count;
                for (var i = 0; i < diff; i++)
                    if (i < selectedPawns.Count)
                    {
                        Job job = JobMaker.MakeJob(PS_DefOf.PS_Enter, parent);
                        selectedPawns[i].jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
            });

        foreach (FloatMenuOption f in base.CompMultiSelectFloatMenuOptions(selectedPawns)) yield return f;
    }

    public virtual string PawnTypeLabel => "PS_StoredPawns".Translate();

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new(base.CompInspectStringExtra());
        sb.AppendLine("PS_StoredCapacity".Translate(innerContainer.Count, MaxStoredPawns()));
        if (innerContainer?.Any<Pawn>() != true) return sb.ToString().TrimStart().TrimEnd();
        sb.AppendLine(PawnTypeLabel);
        foreach (Pawn pawn in innerContainer) sb.AppendLine($"    - {pawn.LabelCap}");

        return sb.ToString().TrimStart().TrimEnd();
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo g in base.CompGetGizmosExtra()) yield return g;
        if (Props.releaseAllOption && innerContainer.Any<Pawn>())
        {
            yield return new Command_Action
            {
                defaultLabel = innerContainer.Count == 1
                    ? "PS_Release".Translate(innerContainer.FirstOrDefault<Pawn>()?.Name?.ToStringShort)
                    : "PS_ReleaseAll".Translate(),
                action = delegate
                {
                    for (int num = innerContainer.Count - 1; num >= 0; num--)
                    {
                        Pawn pawn = (Pawn) innerContainer.GetAt(num);
                        innerContainer.Remove(pawn);
                        GenSpawn.Spawn(pawn, parent.Position, parent.Map);
                        parent.Map.mapDrawer.MapMeshDirty(parent.Position, MapMeshFlagDefOf.Things);
                    }
                },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };

            if (Find.Selector.SelectedObjectsListForReading
                    .Select(o => (o as ThingWithComps)?.TryGetComp<CompPawnStorage>())
                    .Where(o => o?.innerContainer?.Any<Pawn>() == true)
                    .ToList() is { Count: > 1 } comps
                && this == comps.First())
            {
                yield return new Command_Action
                {
                    defaultLabel = "PS_ReleaseAllSelected".Translate(),
                    action = delegate
                    {
                        foreach (CompPawnStorage compPawnStorage in comps)
                        {
                            for (int num = compPawnStorage.innerContainer.Count - 1; num >= 0; num--)
                            {
                                Pawn pawn = (Pawn) compPawnStorage.innerContainer.GetAt(num);
                                compPawnStorage.innerContainer.Remove(pawn);
                                GenSpawn.Spawn(pawn, compPawnStorage.parent.Position, compPawnStorage.parent.Map);
                                compPawnStorage.parent.Map.mapDrawer.MapMeshDirty(compPawnStorage.parent.Position, MapMeshFlagDefOf.Things);
                            }
                        }
                    },
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
                };
                if (PawnStoragesMod.settings.SpecialReleaseAll && ModsConfig.anomalyActive)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = Translator.PseudoTranslated("PS_ReleaseAll".Translate()),
                        action = delegate
                        {
                            Pawn p;
                            if (ModsConfig.IsActive("taggerung.grignrhappensby"))
                            {
                                p = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Taggerung_ShardOfGrignr"), Faction.OfHoraxCult);
                                p.Name = new NameTriple("Grignr", "PS_All".Translate(), "Grignrson");
                            }
                            else
                            {
                                p = PawnGenerator.GeneratePawn(PawnKindDefOf.Ghoul, Faction.OfHoraxCult);
                                p.Name = new NameSingle("PS_All".Translate());
                            }

                            p.health.AddHediff(HediffDefOf.Inhumanized);
                            p.health.AddHediff(HediffDefOf.ShardHolder);
                            GenSpawn.Spawn(p, parent.Position, parent.Map);
                            Messages.Message(Translator.PseudoTranslated("PS_ReleaseAll_Anomaly_Message".Translate()), new LookTargets(p), MessageTypeDefOf.ThreatBig, false);
                            PawnStoragesMod.settings.AllReleased();

                            GridShapeMaker.IrregularLump(parent.Position, parent.Map, 5)
                                .InRandomOrder()
                                .Where(cell => cell.InBounds(parent.Map))
                                .Do(intVec3 => parent.Map.terrainGrid.SetTerrain(intVec3, TerrainDefOf.Voidmetal));

                            EffecterDefOf.Skip_EntryNoDelay.Spawn(p, parent.Map).Cleanup();
                            p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk, forced: true);
                        },
                        icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
                    };
                }
            }
        }

        if (Props.canBeScheduled)
            yield return new Command_Toggle
            {
                defaultLabel = "PS_EnableScheduling".Translate(),
                toggleAction = () => schedulingEnabled = !schedulingEnabled,
                isActive = () => schedulingEnabled,
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };

        if (Props.canBeRotated)
            yield return new Command_Toggle
            {
                defaultLabel = "PS_Rotate".Translate(),
                toggleAction = () =>
                {
                    Rotation.Rotate(RotationDirection.Clockwise);
                    SetLabelDirty();
                    parent.Map.mapDrawer.MapMeshDirty(parent.Position, (ulong) MapMeshFlagDefOf.Things);
                },
                isActive = () => true,
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/PS_Rotate")
            };

        if (Props.allowNonColonist && compAssignable != null) yield return new Command_SetPawnStorageOwnerType(compAssignable);

        foreach (Pawn pawn in innerContainer)
        {
            Gizmo gizmo;
            if ((gizmo = Building.SelectContainedItemGizmo(parent, pawn)) != null)
                yield return gizmo;
        }

        if (Props.selfReleaseOption && innerContainer.InnerListForReading.Any())
            yield return new Command_Action
            {
                defaultLabel = "PS_Release".Translate("..."),
                action = delegate
                {
                    Find.WindowStack.Add(new Verse.FloatMenu(GetDirectlyHeldPawnsDefensiveCopy().Select(p => new FloatMenuOption("PS_Release".Translate(p.LabelCap),
                        delegate { ReleaseSingle(parent.Map, p, false); })).ToList()));
                },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };
    }

    public void ReleaseContents(Map map)
    {
        map ??= parent.Map;

        foreach (Pawn pawn in GetDirectlyHeldPawnsDefensiveCopy())
        {
            ReleaseSingle(map, pawn, false);
        }

        innerContainer.Clear();
    }

    public void ReleaseContentsAt(Map map, IntVec3 at)
    {
        map ??= parent.Map;
        foreach (Pawn pawn in GetDirectlyHeldPawnsDefensiveCopy())
        {
            ReleaseSingleAt(map, pawn, at, false, true);
        }

        innerContainer.Clear();
    }

    public void EjectContents(Map map)
    {
        map ??= parent.Map;

        foreach (Pawn pawn in GetDirectlyHeldPawnsDefensiveCopy())
        {
            PawnComponentsUtility.AddComponentsForSpawn(pawn);
            compAssignable.TryUnassignPawn(pawn);
            GenDrop.TryDropSpawn(pawn, parent.Position, map, ThingPlaceMode.Near, out Thing _);
            bool pawnIsSelected = Find.Selector.IsSelected(pawn);
            Notify_ReleasedFromStorage(pawn);
            if (pawnIsSelected)
                Find.Selector.Select(pawn, false, false);
        }

        innerContainer.Clear();
    }

    public void ReleaseSingle(Map map, Pawn pawn, bool remove = true, bool makeFilth = false)
    {
        ReleaseSingleAt(map, pawn, parent.Position, remove, makeFilth);
    }

    public void ReleaseSingleAt(Map map, Pawn pawn, IntVec3 at, bool remove = false, bool makeFilth = false)
    {
        map ??= parent.Map;

        PawnComponentsUtility.AddComponentsForSpawn(pawn);
        compAssignable?.TryUnassignPawn(pawn);
        GenDrop.TryDropSpawn(pawn, at, map, ThingPlaceMode.Near, out Thing _);
        if (makeFilth) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Slime, new IntRange(3, 6).RandomInRange);

        if (!remove) return;
        Notify_ReleasedFromStorage(pawn);
        innerContainer.Remove(pawn);
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return innerContainer ??= new ThingOwner<Pawn>(this);
    }

    public virtual float NutritionRequiredPerDay() => compAssignable.AssignedPawns.Sum(pawn =>
        SimplifiedPastureNutritionSimulator.NutritionConsumedPerDay(pawn.def, pawn.ageTracker.CurLifeStage));

    public ThingOwner<Pawn> GetDirectlyHeldPawns() => innerContainer;
    public List<Pawn> GetDirectlyHeldPawnsDefensiveCopy() => [..innerContainer.InnerListForReading];

    public void Notify_ReleasedFromStorage(Pawn pawn)
    {
        if (Props.useCharges && chargesRemaining > 0) chargesRemaining--;
        if (chargesRemaining <= 0 && Props.destroyOnZeroCharges) parent.Destroy();
        ParentAsPawnListParent?.Notify_PawnRemoved(pawn);
    }

    public void Notify_AddedToStorage(Pawn pawn)
    {
        ParentAsPawnListParent?.Notify_PawnAdded(pawn);
    }

    public Thing GetAt(int idx)
    {
        return innerContainer.GetAt(idx);
    }
}
