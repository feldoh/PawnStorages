﻿using System.Collections.Generic;
using System.Linq;
using PawnStorages.Farm.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace PawnStorages.Farm.Comps
{
    public partial class CompFarmBreeder : ThingComp
    {
        public int AutoSlaughterTarget = 0;
        public IBreederParent Parent => parent as IBreederParent;

        public Dictionary<PawnKindDef, AutoSlaughterConfig> AutoSlaughterSettings = new();

        private void TryPopulateMissingAnimals()
        {
            foreach (PawnKindDef allDef in DefDatabase<PawnKindDef>.AllDefs)
            {
                if (allDef.race == null || !allDef.race.race.Animal || !(allDef.race.race.wildness < 1.0) || allDef.race.race.Dryad || allDef.race.IsCorpse ||
                    AutoSlaughterSettings.ContainsKey(allDef))
                    continue;

                AutoSlaughterConfig config = new() { animal = allDef.race };
                AutoSlaughterSettings.Add(allDef, config);
            }
        }

        private Dictionary<PawnKindDef, float> breedingProgress = new();

        public Dictionary<PawnKindDef, float> BreedingProgress
        {
            get
            {
                return breedingProgress ??= new Dictionary<PawnKindDef, float>();
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref breedingProgress, "breedingProgress", LookMode.Def);

            Scribe_Collections.Look(ref AutoSlaughterSettings, "AutoSlaughterSettings", LookMode.Def, LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
                return;
            AutoSlaughterSettings ??= new Dictionary<PawnKindDef, AutoSlaughterConfig>();
            if (AutoSlaughterSettings.RemoveAll(x => x.Value.animal == null || x.Value.animal.IsCorpse) != 0)
                Log.Warning("Some auto-slaughter configs had null animals after loading.");
            TryPopulateMissingAnimals();
        }

        public void ExecutionInt(
            Pawn victim)
        {
            Parent.ReleasePawn(victim);
            int num = Mathf.Max(GenMath.RoundRandom(victim.BodySize * 8), 1);
            for (int index = 0; index < num; ++index)
                victim.health.DropBloodFilth();

            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.SlaughteredAnimal, parent.Named(HistoryEventArgsNames.Doer)));
            BodyPartRecord bodyPartRecord = ExecutionUtility.ExecuteCutPart(victim);
            int partHealth = (int) victim.health.hediffSet.GetPartHealth(bodyPartRecord);
            int amount = Mathf.Min(partHealth - 1, 1);

            DamageInfo dinfo = new(DamageDefOf.ExecutionCut, amount, 999f, instigator: parent, hitPart: bodyPartRecord, instigatorGuilty: false, spawnFilth: true);
            victim.forceNoDeathNotification = true;
            victim.TakeDamage(dinfo);
            if (!victim.Dead)
                victim.Kill(dinfo, null);
            SoundDefOf.Execute_Cut.PlayOneShot((SoundInfo) (Thing) victim);
        }

        public bool CullOldestOverLimit(int max, List<Pawn> pawns)
        {
            if (max <= 0 || max >= pawns.Count)
            {
                return false;
            }

            Pawn pawn = pawns.First();
            ExecutionInt(pawn);
            return true;
        }

        public void TryCull(List<IGrouping<PawnKindDef, Pawn>> types)
        {
            foreach (IGrouping<PawnKindDef, Pawn> type in types)
            {
                AutoSlaughterConfig config = AutoSlaughterSettings.TryGetValue(type.Key);

                if (config == null)
                {
                    config = new AutoSlaughterConfig { animal = type.Key.race };
                    AutoSlaughterSettings.SetOrAdd(type.Key, config);

                    // If we just created the config, there's no rules set, so skip eval
                    continue;
                }

                var groupedByAgeAndGender = type
                    .GroupBy(p => new { p.ageTracker.Adult, p.gender })
                    .Select(group => new
                    {
                        FarmAnimalCharacteristics = new FarmAnimalCharacteristics(group.Key.Adult, group.Key.gender),
                        Pawns = group.OrderByDescending(p => p.ageTracker.ageBiologicalTicksInt)
                    })
                    .OrderBy(g => g.FarmAnimalCharacteristics).ToList();

                bool culledThisCycle = Enumerable.Any(groupedByAgeAndGender,
                    group => CullOldestOverLimit(group.FarmAnimalCharacteristics.CullValue(config),
                        group.Pawns.ToList()));

                if (!culledThisCycle) CullOldestOverLimit(config.maxTotal, type.ToList());
            }
        }

        public void TryBreed(List<IGrouping<PawnKindDef, Pawn>> types)
        {
            foreach (IGrouping<PawnKindDef, Pawn> type in types)
            {
                bool males = type.Any(p => p.gender == Gender.Male);
                List<Pawn> females = type.Where(p => p.gender != Gender.Male).ToList();

                if (!females.Any())
                {
                    // no more females, reset
                    BreedingProgress[type.Key] = 0f;
                }

                // no males, stop progress
                if (!males)
                {
                    continue;
                }

                float gestationTicks = AnimalProductionUtility.GestationDaysEach(type.Key.race) * 60000 * PawnStoragesMod.settings.BreedingScale;

                float progressPerCycle = Parent.TickInterval / gestationTicks;

                BreedingProgress.TryAdd(type.Key, 0.0f);

                BreedingProgress[type.Key] = Mathf.Clamp(BreedingProgress[type.Key] + progressPerCycle * females.Count, 0f, 1f);

                if (!(BreedingProgress[type.Key] >= 1f)) continue;

                if (Parent.BreedablePawns.Count >= PawnStoragesMod.settings.MaxPawnsInFarm) break;

                BreedingProgress[type.Key] = 0f;
                Pawn newPawn = PawnGenerator.GeneratePawn(
                    new PawnGenerationRequest(
                        type.Key,
                        Faction.OfPlayer,
                        allowDowned: true,
                        forceNoIdeo: true,
                        developmentalStages: DevelopmentalStage.Newborn
                    ));

                Parent.Notify_PawnBorn(newPawn);
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!PawnStoragesMod.settings.AllowNeedsDrop) return;


            if (!parent.IsHashIntervalTick(Parent.TickInterval))
            {
                return;
            }

            List<IGrouping<PawnKindDef, Pawn>> types = (from p in Parent.AllHealthyPawns
                group p by p.kindDef
                into def
                select def).ToList();

            TryCull(types);
            TryBreed(types);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!DebugSettings.ShowDevGizmos) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Make breeding progress 100%",
                action = delegate
                {
                    foreach (PawnKindDef thing in BreedingProgress.Keys)
                    {
                        BreedingProgress[thing] = 1f;
                    }
                },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll")
            };
        }
    }
}
