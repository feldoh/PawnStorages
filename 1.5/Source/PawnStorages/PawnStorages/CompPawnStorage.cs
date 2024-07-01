using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using PawnStorages.Farm.Comps;
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

    protected CompAssignableToPawn_PawnStorage compAssignable;
    protected bool labelDirty = true;
    protected int chargesRemaining;
    public bool schedulingEnabled;
    protected List<Pawn> storedPawns = []; // Deprecated, to remove in next major version
    private Dictionary<int, int> pawnStoringTick = new();
    protected string transformLabelCache;

    public Rot4 Rotation = Rot4.North.Opposite;

    public Thing Parent => parent;

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
        foreach (Pawn pawn in innerContainer.InnerListForReading)
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
            transformLabelCache = $"{base.TransformLabel(label)} {"PS_Filled".Translate()}";
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

        if (Props.convertOption && CanStore)
            yield return new FloatMenuOption("PS_Enter".Translate(), delegate
            {
                Job job = EnterJob(selPawn);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            });

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
                yield return new FloatMenuOption("PS_Release".Translate(pawn.LabelCap), delegate { selPawn.jobs.TryTakeOrderedJob(ReleaseJob(selPawn, pawn), JobTag.Misc); });
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
                                   !HealthAIUtility.ShouldSeekMedicalRest(pawn):
                    {
                        Job job = EnterJob(pawn);
                        pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                        break;
                    }
                    case false when innerContainer.Contains(pawn) && pawn.timetable?.CurrentAssignment is { } assignmentDef && assignmentDef != PS_DefOf.PS_Home:
                        ReleasePawn(pawn, parent.Position, parent.Map);
                        break;
                }
        }
        base.CompTick();
    }

    //Funcs
    public void ReleasePawn(Pawn pawn, IntVec3 cell, Map map)
    {
        Utility.ReleasePawn(this, pawn, cell, map);
    }

    public virtual void ApplyNeedsForStoredPeriodFor(Pawn pawn)
    {
        int storedAtTick = pawnStoringTick.TryGetValue(pawn.thingIDNumber, -1);
        pawnStoringTick.Remove(pawn.thingIDNumber);
        if (storedAtTick <= 0 || !PawnStoragesMod.settings.AllowNeedsDrop) return;

        // We drop one tick interval to make sure we don't boost need drops from being at home, the slight reduction can be seen as a benefit of being at home.
        int ticksStored = Mathf.Max(0, Find.TickManager.TicksGame - storedAtTick - NEEDS_INTERVAL);
        if (Props.pawnRestIncreaseTick != 0 && pawn.needs?.rest is { } restNeed)
        {
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

        CompFarmNutrition nutritionComp;
        nutritionComp = parent.TryGetComp<CompFarmNutrition>();
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

    public void StorePawn(Pawn pawn)
    {
        Map pawnMap = pawn.Map;
        if (Props.lightEffect) FleckMaker.ThrowLightningGlow(pawn.Position.ToVector3Shifted(), pawnMap, 0.5f);
        if (Props.transformEffect) FleckMaker.ThrowExplosionCell(pawn.Position, pawnMap, FleckDefOf.ExplosionFlash, Color.white);

        //Spawn the store effecter
        Props.storeEffect?.Spawn(pawn.Position, pawnMap);
        pawn.DeSpawn();
        innerContainer.TryAdd(pawn);

        pawnMap?.mapDrawer?.MapMeshDirty(parent.Position, MapMeshFlagDefOf.Things);

        if (compAssignable != null && !compAssignable.AssignedPawns.Contains(pawn)) compAssignable.TryAssignPawn(pawn);
        labelDirty = true;
        pawnStoringTick.SetOrAdd(pawn.thingIDNumber, Find.TickManager.TicksGame);
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

    public override IEnumerable<FloatMenuOption> CompMultiSelectFloatMenuOptions(List<Pawn> selPawns)
    {
        var selPawnsCopy = selPawns.ListFullCopy();
        if (Props.convertOption && MaxStoredPawns() > innerContainer.Count)
            yield return new FloatMenuOption("PS_Enter".Translate(), delegate
            {
                var diff = MaxStoredPawns() - innerContainer.Count;
                for (var i = 0; i < diff; i++)
                    if (i < selPawnsCopy.Count)
                    {
                        Job job = JobMaker.MakeJob(PS_DefOf.PS_Enter, parent);
                        selPawnsCopy[i].jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    }
            });

        foreach (FloatMenuOption f in base.CompMultiSelectFloatMenuOptions(selPawns)) yield return f;
    }

    public virtual string PawnTypeLabel => "PS_StoredPawns".Translate();

    public override string CompInspectStringExtra()
    {
        StringBuilder sb = new(base.CompInspectStringExtra());
        if (innerContainer?.Any<Pawn>() != true) return sb.ToString().TrimStart().TrimEnd();
        sb.AppendLine();
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
                    ? "PS_Release".Translate(((Pawn)innerContainer.FirstOrDefault<Pawn>()).Name.ToStringShort)
                    : "PS_ReleaseAll".Translate(),
                action = delegate
                {
                    for (int num = innerContainer.Count - 1; num >= 0; num--)
                    {
                        Pawn pawn = (Pawn)innerContainer.GetAt(num);
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
                                Pawn pawn = (Pawn)compPawnStorage.innerContainer.GetAt(num);
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

        yield return new Command_Toggle
        {
            defaultLabel = "PS_Rotate".Translate(),
            toggleAction = () =>
            {
                Rotation.Rotate(RotationDirection.Clockwise);
                this.SetLabelDirty();
                this.parent.Map.mapDrawer.MapMeshDirty(this.parent.Position, (ulong)MapMeshFlagDefOf.Things);
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
    }

    public void ReleaseContents(Map map)
    {
        map ??= parent.Map;

        foreach (Pawn pawn in innerContainer)
        {
            ReleaseSingle(map, pawn, false);
        }

        innerContainer.Clear();
    }

    public void ReleaseContentsAt(Map map, IntVec3 at)
    {
        map ??= parent.Map;

        for(var i = 0; i < innerContainer.Count; i++)
            ReleaseSingleAt(map, innerContainer.innerList[i], at, false, true);
        innerContainer.Clear();
    }

    public void EjectContents(Map map)
    {
        map ??= parent.Map;
        EffecterDef flightEffecterDef = DefDatabase<EffecterDef>.GetNamed("JumpFlightEffect", false);
        SoundDef landingSound = DefDatabase<SoundDef>.GetNamed("Longjump_Land", false);

        foreach (Pawn pawn in innerContainer)
        {
            PawnComponentsUtility.AddComponentsForSpawn(pawn);
            compAssignable.TryUnassignPawn(pawn);
            GenDrop.TryDropSpawn(pawn, parent.Position, map, ThingPlaceMode.Near, out Thing _);

            IntVec3 cell = CellFinder.RandomClosewalkCellNear(parent.Position, map, 18);

            bool pawnIsSelected = Find.Selector.IsSelected(pawn);
            PawnFlyer pawnFlyer = PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer, pawn, cell, flightEffecterDef,
                landingSound);
            if (pawnFlyer == null)
                return;
            FleckMaker.ThrowDustPuff(pawn.Position.ToVector3Shifted() + Gen.RandomHorizontalVector(0.5f), map, 2f);
            GenSpawn.Spawn(pawnFlyer, cell, map);
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
        
        if(!remove) return;
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

    public ThingOwner<Pawn> GetDirectlyHeldPawns() => innerContainer;

    public void Notify_ReleasedFromStorage(Pawn pawn)
    {
        if (Props.useCharges && chargesRemaining > 0) chargesRemaining--;
        if (chargesRemaining <= 0 && Props.destroyOnZeroCharges) Parent.Destroy();
    }
}
