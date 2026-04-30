using System;
using System.Collections.Generic;
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
        public IBreederParent ParentAsBreederParent => parent as IBreederParent;

        public bool MakeRoomOnBirth = true;
        public bool ProtectBreedingPair = true;
        public bool ProtectPregnant = true;
        public bool ProtectBonded = true;

        public Dictionary<PawnKindDef, AutoSlaughterConfig> AutoSlaughterSettings = new();

        public Dictionary<PawnKindDef, AutoSlaughterConfig> GetOrPopulateAutoSlaughterSettings()
        {
            if (!AutoSlaughterSettings.NullOrEmpty())
                return AutoSlaughterSettings;
            AutoSlaughterSettings = new Dictionary<PawnKindDef, AutoSlaughterConfig>();
            TryPopulateMissingAnimals();
            return AutoSlaughterSettings;
        }

        public Dictionary<PawnKindDef, AutoSlaughterCullOrder> AutoSlaughterCullOrder = new();

        public Dictionary<PawnKindDef, AutoSlaughterCullOrder> GetOrPopulateAutoSlaughterCullOrder()
        {
            if (!AutoSlaughterCullOrder.NullOrEmpty())
                return AutoSlaughterCullOrder;
            AutoSlaughterCullOrder = new Dictionary<PawnKindDef, AutoSlaughterCullOrder>();
            foreach (PawnKindDef allDef in Utility.AllAnimalKinds.Value.Where(d => !AutoSlaughterCullOrder.ContainsKey(d)))
            {
                AutoSlaughterCullOrder.Add(allDef, new AutoSlaughterCullOrder());
            }

            return AutoSlaughterCullOrder;
        }

        public Dictionary<PawnKindDef, AutoSlaughterMinimums> AutoSlaughterMinimums = new();

        public Dictionary<PawnKindDef, AutoSlaughterMinimums> GetOrPopulateAutoSlaughterMinimums()
        {
            AutoSlaughterMinimums ??= new Dictionary<PawnKindDef, AutoSlaughterMinimums>();
            foreach (PawnKindDef allDef in Utility.AllAnimalKinds.Value.Where(d => !AutoSlaughterMinimums.ContainsKey(d)))
            {
                AutoSlaughterMinimums.Add(allDef, new AutoSlaughterMinimums());
            }
            return AutoSlaughterMinimums;
        }

        private void TryPopulateMissingAnimals()
        {
            foreach (PawnKindDef allDef in Utility.AllAnimalKinds.Value.Where(d => !AutoSlaughterSettings.ContainsKey(d)))
            {
                AutoSlaughterConfig config = new() { animal = allDef.race };
                AutoSlaughterSettings.Add(allDef, config);
            }
        }

        private Dictionary<PawnKindDef, float> breedingProgress = new();

        public Dictionary<PawnKindDef, float> BreedingProgress
        {
            get { return breedingProgress ??= new Dictionary<PawnKindDef, float>(); }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref breedingProgress, "breedingProgress", LookMode.Def);

            Scribe_Values.Look(ref MakeRoomOnBirth, "MakeRoomOnBirth", true);
            Scribe_Values.Look(ref ProtectBreedingPair, "ProtectBreedingPair", true);
            Scribe_Values.Look(ref ProtectPregnant, "ProtectPregnant", true);
            Scribe_Values.Look(ref ProtectBonded, "ProtectBonded", true);

            Scribe_Collections.Look(ref AutoSlaughterSettings, "AutoSlaughterSettings", LookMode.Def, LookMode.Deep);
            Scribe_Collections.Look(ref AutoSlaughterCullOrder, "AutoSlaughterCullOrder", LookMode.Def, LookMode.Deep);
            Scribe_Collections.Look(ref AutoSlaughterMinimums, "AutoSlaughterMinimums", LookMode.Def, LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit)
                return;
            AutoSlaughterSettings ??= new Dictionary<PawnKindDef, AutoSlaughterConfig>();
            AutoSlaughterCullOrder ??= new Dictionary<PawnKindDef, AutoSlaughterCullOrder>();
            AutoSlaughterMinimums ??= new Dictionary<PawnKindDef, AutoSlaughterMinimums>();
            if (AutoSlaughterSettings.RemoveAll(x => x.Value.animal == null || x.Value.animal.IsCorpse) != 0)
                Log.Warning("Some auto-slaughter configs had null animals after loading.");
            TryPopulateMissingAnimals();
            GetOrPopulateAutoSlaughterCullOrder();
            GetOrPopulateAutoSlaughterMinimums();
        }

        public void ExecutionInt(Pawn victim)
        {
            ParentAsBreederParent.ReleasePawn(victim);
            int num = Mathf.Max(GenMath.RoundRandom(victim.BodySize * 8), 1);
            for (int index = 0; index < num; ++index)
                victim.health.DropBloodFilth();

            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.SlaughteredAnimal, parent.Named(HistoryEventArgsNames.Doer)));
            BodyPartRecord bodyPartRecord = ExecutionUtility.ExecuteCutPart(victim);
            int partHealth = (int)victim.health.hediffSet.GetPartHealth(bodyPartRecord);
            int amount = Mathf.Min(partHealth - 1, 1);

            DamageInfo dinfo = new(DamageDefOf.ExecutionCut, amount, 999f, instigator: parent, hitPart: bodyPartRecord, instigatorGuilty: false, spawnFilth: true);
            victim.forceNoDeathNotification = true;
            victim.TakeDamage(dinfo);
            if (!victim.Dead)
                victim.Kill(dinfo, null);
            SoundDefOf.Execute_Cut.PlayOneShot((SoundInfo)(Thing)victim);
        }

        public bool CullFirstOverLimit(int max, List<Pawn> pawns)
        {
            if (max <= 0 || max >= pawns.Count)
            {
                return false;
            }

            Pawn pawn = pawns.First();
            ExecutionInt(pawn);
            return true;
        }

        public bool IsProtectedFromCull(Pawn p)
        {
            if (ProtectPregnant && p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) != null)
                return true;
            if (ProtectBonded && p.relations != null && p.relations.GetDirectRelationsCount(PawnRelationDefOf.Bond) > 0)
                return true;
            return false;
        }

        public int EffectiveMin(PawnKindDef kind, bool adult, Gender gender)
        {
            int baseMin = GetOrPopulateAutoSlaughterMinimums().TryGetValue(kind)?.MinFor(adult, gender) ?? 0;
            if (ProtectBreedingPair && adult && (kind?.RaceProps?.hasGenders ?? false))
                baseMin = Math.Max(baseMin, 1);
            return baseMin;
        }

        // Candidates from one (adult, gender) bucket within a kind, ordered by cull priority,
        // capped at the count we're allowed to cull (bucket - effective min). Pregnant/bonded
        // pawns are filtered out before the cap.
        public List<Pawn> BucketCullCandidates(IGrouping<PawnKindDef, Pawn> type, bool adult, Gender gender, int extraInBracket = 0)
        {
            List<Pawn> bucket = type.Where(p => p.ageTracker.Adult == adult && p.gender == gender).ToList();
            int min = EffectiveMin(type.Key, adult, gender);
            int allowedCullCount = Math.Max(0, bucket.Count + extraInBracket - min);
            if (allowedCullCount == 0)
                return new List<Pawn>();
            List<Pawn> nonProtected = bucket.Where(p => !IsProtectedFromCull(p)).ToList();
            AutoSlaughterCullOrder order = GetOrPopulateAutoSlaughterCullOrder()[type.Key];
            bool ascending = order.IsAscending(adult, gender);
            nonProtected = ascending
                ? nonProtected.OrderBy(p => p.ageTracker.ageBiologicalTicksInt).ToList()
                : nonProtected.OrderByDescending(p => p.ageTracker.ageBiologicalTicksInt).ToList();
            return nonProtected.Take(allowedCullCount).ToList();
        }

        // Cross-bucket ordered cull candidates for a kind. If newbornInBracket is supplied,
        // its bracket gets +1 to the effective count (so cull from that bracket is allowed
        // when bucket count == min — the newborn refills the slot post-cull).
        public List<Pawn> GetCullCandidatesOrdered(IGrouping<PawnKindDef, Pawn> type, Pawn newbornInBracket = null)
        {
            PawnKindDef kind = type.Key;
            bool genderless = !(kind?.RaceProps?.hasGenders ?? true);
            AutoSlaughterCullOrder order = GetOrPopulateAutoSlaughterCullOrder()[kind];

            if (genderless)
            {
                List<Pawn> all = type.Where(p => !IsProtectedFromCull(p)).ToList();
                return order.AllAscending
                    ? all.OrderBy(p => p.ageTracker.ageBiologicalTicksInt).ToList()
                    : all.OrderByDescending(p => p.ageTracker.ageBiologicalTicksInt).ToList();
            }

            List<Pawn> ordered = new();
            // Iterate buckets in FarmAnimalCharacteristics priority order: adults before young, males before females.
            (bool adult, Gender gender)[] buckets =
            [
                (true, Gender.Male),
                (true, Gender.Female),
                (false, Gender.Male),
                (false, Gender.Female),
            ];
            foreach ((bool adult, Gender gender) in buckets)
            {
                int extra = (newbornInBracket != null && newbornInBracket.ageTracker.Adult == adult && newbornInBracket.gender == gender) ? 1 : 0;
                ordered.AddRange(BucketCullCandidates(type, adult, gender, extra));
            }
            return ordered;
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

                bool culled = false;
                if (type.Key.RaceProps.hasGenders)
                {
                    (bool adult, Gender gender, int max)[] orderArr =
                    [
                        (true, Gender.Male, config.maxMales),
                        (true, Gender.Female, config.maxFemales),
                        (false, Gender.Male, config.maxMalesYoung),
                        (false, Gender.Female, config.maxFemalesYoung),
                    ];
                    foreach ((bool adult, Gender gender, int max) in orderArr)
                    {
                        if (max <= 0)
                            continue;
                        int bucketCount = type.Count(p => p.ageTracker.Adult == adult && p.gender == gender);
                        if (bucketCount <= max)
                            continue;
                        List<Pawn> candidates = BucketCullCandidates(type, adult, gender);
                        if (candidates.Count == 0)
                            continue;
                        ExecutionInt(candidates[0]);
                        culled = true;
                        break;
                    }
                }

                if (culled)
                    continue;

                int totalCount = type.Count();
                if (config.maxTotal > 0 && totalCount > config.maxTotal)
                {
                    List<Pawn> ordered = GetCullCandidatesOrdered(type);
                    if (ordered.Count > 0)
                        ExecutionInt(ordered[0]);
                }
            }
        }

        // Cull a same-kind candidate to make room for an inbound newborn. Returns the culled
        // pawn (so the caller can log it), or null if no candidate survived the guards — in
        // which case the caller should fall back to the drop-on-floor path.
        public Pawn TryEjectForRoom(Pawn newborn)
        {
            if (newborn?.kindDef == null)
                return null;
            List<Pawn> kindMembers = ParentAsBreederParent.AllHealthyPawns.Where(p => p.kindDef == newborn.kindDef).ToList();
            if (kindMembers.Count == 0)
                return null;
            IGrouping<PawnKindDef, Pawn> grouping = kindMembers.GroupBy(p => p.kindDef).FirstOrDefault();
            if (grouping == null)
                return null;
            List<Pawn> candidates = GetCullCandidatesOrdered(grouping, newborn);
            if (candidates.Count == 0)
                return null;
            Pawn victim = candidates[0];
            ExecutionInt(victim);
            return victim;
        }

        public void TryBreed(List<IGrouping<PawnKindDef, Pawn>> types)
        {
            foreach (IGrouping<PawnKindDef, Pawn> type in types)
            {
                bool males = type.Any(p => p.gender == Gender.Male);
                List<Pawn> nonMales = type.Where(p => p.gender != Gender.Male).ToList();

                bool genderless = !type.Key.RaceProps.hasGenders;

                // no more females, reset
                if (!nonMales.Any() && !genderless)
                    BreedingProgress[type.Key] = 0f;

                // no males, stop progress
                if (!males && !genderless)
                    continue;

                float gestationDays = AnimalProductionUtility.GestationDaysEach(type.Key.race);
                if (gestationDays <= 0f)
                    continue;

                float gestationTicks = gestationDays * GenDate.TicksPerDay * PawnStoragesMod.settings.BreedingScale;

                float progressPerInterval = ParentAsBreederParent.BuildingTickInterval / gestationTicks;

                if (!BreedingProgress.ContainsKey(type.Key))
                    BreedingProgress[type.Key] = 0f;
                BreedingProgress[type.Key] = Mathf.Clamp01(BreedingProgress[type.Key] + progressPerInterval * nonMales.Count);

                if (BreedingProgress[type.Key] < 1f)
                    continue;

                BreedingProgress[type.Key] = 0f;
                Pawn newPawn = PawnGenerator.GeneratePawn(
                    new PawnGenerationRequest(type.Key, Faction.OfPlayer, allowDowned: true, forceNoIdeo: true, developmentalStages: DevelopmentalStage.Newborn)
                );

                ParentAsBreederParent.Notify_PawnBorn(newPawn);
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!PawnStoragesMod.settings.AllowNeedsDrop)
                return;

            if (!parent.IsHashIntervalTick(ParentAsBreederParent.BuildingTickInterval))
            {
                return;
            }

            List<IGrouping<PawnKindDef, Pawn>> pawnsByKind = (from p in ParentAsBreederParent.AllHealthyPawns group p by p.kindDef into def select def).ToList();

            TryCull(pawnsByKind);
            TryBreed(pawnsByKind);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!DebugSettings.ShowDevGizmos)
                yield break;
            yield return new Command_Action
            {
                defaultLabel = "Make breeding progress 100%",
                action = delegate
                {
                    foreach (PawnKindDef thing in BreedingProgress.Keys.ToList())
                    {
                        BreedingProgress[thing] = 1f;
                    }
                },
                icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
            };
        }
    }
}
