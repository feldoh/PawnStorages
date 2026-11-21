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

        // Kinds can appear after a save was made (an animal mod added later), so every GetOrPopulate* fills in
        // missing kinds rather than only populating an empty dictionary. Each runs once per session per comp;
        // later calls are a cheap flag check (the tab calls these every frame).
        private bool settingsPopulated;
        private bool cullOrderPopulated;
        private bool minimumsPopulated;

        private static void AddMissingKinds<T>(Dictionary<PawnKindDef, T> dict, System.Func<PawnKindDef, T> make)
        {
            foreach (PawnKindDef kind in Utility.AllAnimalKinds.Value)
            {
                if (!dict.ContainsKey(kind))
                    dict.Add(kind, make(kind));
            }
        }

        public Dictionary<PawnKindDef, AutoSlaughterConfig> GetOrPopulateAutoSlaughterSettings()
        {
            AutoSlaughterSettings ??= new Dictionary<PawnKindDef, AutoSlaughterConfig>();
            if (!settingsPopulated)
            {
                AddMissingKinds(AutoSlaughterSettings, kind => new AutoSlaughterConfig { animal = kind.race });
                settingsPopulated = true;
            }
            return AutoSlaughterSettings;
        }

        public Dictionary<PawnKindDef, AutoSlaughterCullOrder> AutoSlaughterCullOrder = new();

        public Dictionary<PawnKindDef, AutoSlaughterCullOrder> GetOrPopulateAutoSlaughterCullOrder()
        {
            AutoSlaughterCullOrder ??= new Dictionary<PawnKindDef, AutoSlaughterCullOrder>();
            if (!cullOrderPopulated)
            {
                AddMissingKinds(AutoSlaughterCullOrder, _ => new AutoSlaughterCullOrder());
                cullOrderPopulated = true;
            }
            return AutoSlaughterCullOrder;
        }

        // The cache of AutoSlaughter orders can miss some kinds of animal (e.g. a modded animal added after load)
        // This provides a safe way to get a cull order for a kind which will always return a valid order even if the cache is missing that kind.
        public AutoSlaughterCullOrder GetCullOrder(PawnKindDef kind)
        {
            Dictionary<PawnKindDef, AutoSlaughterCullOrder> orders = GetOrPopulateAutoSlaughterCullOrder();
            if (!orders.TryGetValue(kind, out AutoSlaughterCullOrder order))
            {
                order = new AutoSlaughterCullOrder();
                orders[kind] = order;
            }
            return order;
        }

        public Dictionary<PawnKindDef, AutoSlaughterMinimums> AutoSlaughterMinimums = new();

        public Dictionary<PawnKindDef, AutoSlaughterMinimums> GetOrPopulateAutoSlaughterMinimums()
        {
            AutoSlaughterMinimums ??= new Dictionary<PawnKindDef, AutoSlaughterMinimums>();
            if (!minimumsPopulated)
            {
                AddMissingKinds(AutoSlaughterMinimums, _ => new AutoSlaughterMinimums());
                minimumsPopulated = true;
            }
            return AutoSlaughterMinimums;
        }

        // Safe lookup for UI: always returns a valid entry even for a kind the dictionary has never seen.
        public AutoSlaughterMinimums GetMinimums(PawnKindDef kind)
        {
            Dictionary<PawnKindDef, AutoSlaughterMinimums> mins = GetOrPopulateAutoSlaughterMinimums();
            if (!mins.TryGetValue(kind, out AutoSlaughterMinimums result))
            {
                result = new AutoSlaughterMinimums();
                mins[kind] = result;
            }
            return result;
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
            settingsPopulated = cullOrderPopulated = minimumsPopulated = false;
            GetOrPopulateAutoSlaughterSettings();
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

        // Executes the first (highest-priority) candidate from an already-filtered,
        // already-capped candidate list. Returns the culled pawn, or null when there was
        // no eligible candidate so callers can take their no-cull fallback path.
        public Pawn CullFirstCandidate(List<Pawn> candidates)
        {
            if (candidates.NullOrEmpty())
                return null;
            Pawn victim = candidates[0];
            ExecutionInt(victim);
            return victim;
        }

        public bool IsProtectedFromCull(Pawn p)
        {
            if (ProtectPregnant && p.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Pregnant) != null)
                return true;
            return ProtectBonded && p.relations != null && p.relations.GetDirectRelationsCount(PawnRelationDefOf.Bond) > 0;
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
                return [];
            List<Pawn> nonProtected = bucket.Where(p => !IsProtectedFromCull(p)).ToList();
            AutoSlaughterCullOrder order = GetCullOrder(type.Key);
            bool ascending = order.IsAscending(adult, gender);
            nonProtected = ascending
                ? nonProtected.OrderBy(p => p.ageTracker.ageBiologicalTicksInt).ToList()
                : nonProtected.OrderByDescending(p => p.ageTracker.ageBiologicalTicksInt).ToList();
            return nonProtected.Take(allowedCullCount).ToList();
        }

        // Cross-bucket ordered cull candidates for a kind. If newbornInBracket is supplied,
        // its bracket gets +1 to the effective count (so cull from that bracket is allowed
        // when bucket count == min - the newborn refills the slot post-cull).
        public List<Pawn> GetCullCandidatesOrdered(IGrouping<PawnKindDef, Pawn> type, Pawn newbornInBracket = null)
        {
            PawnKindDef kind = type.Key;
            bool genderless = !(kind?.RaceProps?.hasGenders ?? true);
            AutoSlaughterCullOrder order = GetCullOrder(kind);

            if (genderless)
            {
                List<Pawn> all = type.Where(p => !IsProtectedFromCull(p)).ToList();
                return order.AllAscending ? all.OrderBy(p => p.ageTracker.ageBiologicalTicksInt).ToList() : all.OrderByDescending(p => p.ageTracker.ageBiologicalTicksInt).ToList();
            }

            List<Pawn> ordered = new();
            // Iterate buckets in FarmAnimalCharacteristics priority order: adults before young, males before females.
            (bool adult, Gender gender)[] buckets = [(true, Gender.Male), (true, Gender.Female), (false, Gender.Male), (false, Gender.Female)];
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
                        if (max < 0)
                            continue;
                        int bucketCount = type.Count(p => p.ageTracker.Adult == adult && p.gender == gender);
                        if (bucketCount <= max)
                            continue;
                        if (CullFirstCandidate(BucketCullCandidates(type, adult, gender)) != null)
                        {
                            culled = true;
                            break;
                        }
                    }
                }

                if (culled)
                    continue;

                int totalCount = type.Count();
                if (config.maxTotal >= 0 && totalCount > config.maxTotal)
                {
                    CullFirstCandidate(GetCullCandidatesOrdered(type));
                }
            }
        }

        // Cull a same-kind candidate to make room for an inbound newborn. Returns the culled
        // pawn (so the caller can log it), or null if no candidate survived the guards - in
        // which case the caller should fall back to the drop-on-floor path.
        public Pawn TryCullForRoom(Pawn newborn)
        {
            if (newborn?.kindDef == null)
                return null;
            List<Pawn> kindMembers = ParentAsBreederParent.AllHealthyPawns.Where(p => p.kindDef == newborn.kindDef).ToList();
            if (kindMembers.Count == 0)
                return null;
            IGrouping<PawnKindDef, Pawn> grouping = kindMembers.GroupBy(p => p.kindDef).FirstOrDefault();
            return grouping == null ? null : CullFirstCandidate(GetCullCandidatesOrdered(grouping, newborn));
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

                BreedingProgress.TryAdd(type.Key, 0f);
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
