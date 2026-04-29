using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace PawnStorages.Anomaly;

public class AllFeature_GameComponent : GameComponent
{
    private AllDiscoveryStage stage;
    private int lastStageChangeTick;
    private Thing markedStandStage1;
    private Thing markedStandStage2;
    private Pawn allPawn;
    private Pawn nothingPawn;
    private bool allDiedUnresearched;

    private const int TickInterval = 2500;
    private const float Stage1_MTB_Days = 5f;
    private const float Stage2_MTB_Days = 3f;
    private const float ProximityRadius = 10f;

    public AllDiscoveryStage Stage => stage;
    public Pawn AllPawn => allPawn;
    public Pawn NothingPawn => nothingPawn;

    public AllFeature_GameComponent(Game game) { }

    public override void GameComponentTick()
    {
        if (!ModsConfig.AnomalyActive)
            return;

        if (Find.TickManager.TicksGame % TickInterval != 0)
            return;

        switch (stage)
        {
            case AllDiscoveryStage.None:
                TryAdvanceToStage1();
                break;
            case AllDiscoveryStage.Stage1_Noticed:
                TryAdvanceToStage2();
                break;
            case AllDiscoveryStage.Stage2_Hint:
                TryAdvanceToAllSpawned();
                break;
            case AllDiscoveryStage.AllSpawned:
                CheckResearchComplete();
                break;
        }
    }

    private List<Thing> GetOccupiedStands(Map map)
    {
        List<Thing> stands = [];
        foreach (Thing thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
        {
            if (thing is not Building_PawnStorage building)
                continue;
            if (building.storageComp?.innerContainer == null || !building.storageComp.innerContainer.Any<Pawn>())
                continue;
            if (!IsStandDef(thing.def))
                continue;
            stands.Add(thing);
        }
        return stands;
    }

    private static bool IsStandDef(ThingDef def) => def == PS_DefOf.PS_PawnStatue || def.defName == "PS_PawnStatueSelfRelease" || def.defName == "PS_Plastinite";

    private Pawn FindColonistNear(Thing stand)
    {
        if (stand?.Map == null)
            return null;
        foreach (Pawn pawn in stand.Map.mapPawns.FreeColonistsSpawned.InRandomOrder())
        {
            if (pawn.Position.InHorDistOf(stand.Position, 15f) && GenSight.LineOfSight(pawn.Position, stand.Position, stand.Map))
                return pawn;
        }
        return null;
    }

    private void TryAdvanceToStage1()
    {
        if (!Rand.MTBEventOccurs(Stage1_MTB_Days, GenDate.TicksPerDay, TickInterval))
            return;

        foreach (Map map in Find.Maps)
        {
            if (!map.IsPlayerHome)
                continue;

            List<Thing> stands = GetOccupiedStands(map);
            if (stands.Count == 0)
                continue;

            Thing stand = stands.RandomElement();
            Pawn colonist = FindColonistNear(stand);
            if (colonist == null)
                continue;

            markedStandStage1 = stand;
            stage = AllDiscoveryStage.Stage1_Noticed;
            lastStageChangeTick = Find.TickManager.TicksGame;

            Find.LetterStack.ReceiveLetter(
                "PS_Anomaly_Stage1_LetterLabel".Translate(),
                "PS_Anomaly_Stage1_LetterText".Translate(colonist.Named("PAWN")),
                LetterDefOf.NeutralEvent,
                new LookTargets(stand)
            );
            return;
        }
    }

    private void TryAdvanceToStage2()
    {
        // If the Stage 1 stand was destroyed, reset gracefully
        if (markedStandStage1 is { Destroyed: true } or null)
            markedStandStage1 = null;

        int ticksSinceStage1 = Find.TickManager.TicksGame - lastStageChangeTick;
        if (ticksSinceStage1 < GenDate.TicksPerDay)
            return;

        if (!Rand.MTBEventOccurs(Stage2_MTB_Days, GenDate.TicksPerDay, TickInterval))
            return;

        foreach (Map map in Find.Maps)
        {
            if (!map.IsPlayerHome)
                continue;

            List<Thing> stands = GetOccupiedStands(map);
            Thing differentStand = stands.FirstOrDefault(s => s != markedStandStage1);
            if (differentStand == null)
                continue;

            Pawn colonist = FindColonistNear(differentStand);
            if (colonist == null)
                continue;

            markedStandStage2 = differentStand;
            stage = AllDiscoveryStage.Stage2_Hint;
            lastStageChangeTick = Find.TickManager.TicksGame;

            Find.LetterStack.ReceiveLetter(
                "PS_Anomaly_Stage2_LetterLabel".Translate(),
                "PS_Anomaly_Stage2_LetterText".Translate(colonist.Named("PAWN")),
                LetterDefOf.NeutralEvent,
                new LookTargets(differentStand)
            );
            return;
        }
    }

    private void TryAdvanceToAllSpawned()
    {
        foreach (Map map in Find.Maps)
        {
            if (!map.IsPlayerHome)
                continue;

            List<Thing> stands = GetOccupiedStands(map);
            foreach (Thing stand in stands)
            {
                int nearbyCount = stands.Count(other => other != stand && other.Position.InHorDistOf(stand.Position, ProximityRadius));
                if (nearbyCount < 2)
                    continue;

                SpawnAll(stand.Position, map);
                return;
            }
        }
    }

    public void SpawnAll(IntVec3 pos, Map map)
    {
        bool grignrActive = ModsConfig.IsActive("taggerung.grignrhappensby");
        PawnKindDef kind = grignrActive ? DefDatabase<PawnKindDef>.GetNamedSilentFail("Taggerung_ShardOfGrignr") ?? PawnKindDefOf.Ghoul : PawnKindDefOf.Ghoul;

        Pawn p = PawnGenerator.GeneratePawn(kind, Faction.OfAncients);
        p.Name = grignrActive ? new NameTriple("Grignr", "PS_Anomaly_AllName".Translate(), "Grignrson") : (Name)new NameSingle("PS_Anomaly_AllName".Translate());

        p.health.AddHediff(HediffDefOf.Inhumanized);
        p.health.AddHediff(HediffDefOf.ShardHolder);

        GenSpawn.Spawn(p, pos, map);

        foreach (IntVec3 cell in GridShapeMaker.IrregularLump(pos, map, 5).InRandomOrder().Where(c => c.InBounds(map)))
        {
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Voidmetal);
        }

        EffecterDefOf.Skip_EntryNoDelay.Spawn(p, map).Cleanup();

        allPawn = p;
        stage = AllDiscoveryStage.AllSpawned;
        lastStageChangeTick = Find.TickManager.TicksGame;

        Messages.Message(AllTextStyling.Eldritchify("PS_Anomaly_Stage3_Message".Translate()), new LookTargets(p), MessageTypeDefOf.NeutralEvent, false);

        // Discover the codex entry
        if (PS_DefOf.PS_AllCodexEntry != null)
            Find.EntityCodex.SetDiscovered(PS_DefOf.PS_AllCodexEntry, null, p);
    }

    private void CheckResearchComplete()
    {
        if (PS_DefOf.PS_AllStudy == null)
            return;

        if (!PS_DefOf.PS_AllStudy.IsFinished)
            return;

        // Research complete — despawn All, advance stage
        if (allPawn is { Spawned: true })
        {
            EffecterDefOf.Skip_EntryNoDelay.Spawn(allPawn, allPawn.Map).Cleanup();
            allPawn.DeSpawn();
        }

        stage = AllDiscoveryStage.AllResearched;
        lastStageChangeTick = Find.TickManager.TicksGame;

        Find.LetterStack.ReceiveLetter("PS_Anomaly_Researched_LetterLabel".Translate(), "PS_Anomaly_Researched_LetterText".Translate(), LetterDefOf.NeutralEvent);
    }

    public void StartExtractionQuest(IntVec3 pos, Map map)
    {
        if (stage != AllDiscoveryStage.AllResearched)
            return;

        stage = AllDiscoveryStage.QuestActive;
        lastStageChangeTick = Find.TickManager.TicksGame;

        bool grignrActive = ModsConfig.IsActive("taggerung.grignrhappensby");
        PawnKindDef allKind = grignrActive ? DefDatabase<PawnKindDef>.GetNamedSilentFail("Taggerung_ShardOfGrignr") ?? PawnKindDefOf.Ghoul : PawnKindDefOf.Ghoul;

        // Spawn "All" as a controllable colony pawn (player faction for draft control)
        Pawn all = PawnGenerator.GeneratePawn(allKind, Faction.OfPlayer);
        all.Name = grignrActive ? new NameTriple("Grignr", "PS_Anomaly_AllName".Translate(), "Grignrson") : (Name)new NameSingle("PS_Anomaly_AllName".Translate());
        all.health.AddHediff(HediffDefOf.Inhumanized);
        all.health.AddHediff(HediffDefOf.ShardHolder);
        GenSpawn.Spawn(all, pos, map);
        EffecterDefOf.Skip_EntryNoDelay.Spawn(all, map).Cleanup();

        // Spawn "Nothing" as hostile enemy
        PawnKindDef nothingKind = grignrActive ? DefDatabase<PawnKindDef>.GetNamedSilentFail("Taggerung_ShardOfGrignr") ?? PawnKindDefOf.Ghoul : PawnKindDefOf.Ghoul;
        Faction hostileFaction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.AncientsHostile) ?? Faction.OfAncientsHostile;
        Pawn nothing = PawnGenerator.GeneratePawn(nothingKind, hostileFaction);
        nothing.Name = new NameSingle("PS_Anomaly_NothingName".Translate());
        nothing.health.AddHediff(HediffDefOf.Inhumanized);
        nothing.health.AddHediff(HediffDefOf.ShardHolder);

        IntVec3 nothingPos = CellFinder.RandomSpawnCellForPawnNear(pos, map);
        GenSpawn.Spawn(nothing, nothingPos, map);
        EffecterDefOf.Skip_EntryNoDelay.Spawn(nothing, map).Cleanup();

        // Voidmetal terrain
        foreach (IntVec3 cell in GridShapeMaker.IrregularLump(pos, map, 5).InRandomOrder().Where(cell => cell.InBounds(map)))
        {
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Voidmetal);
        }

        allPawn = all;
        nothingPawn = nothing;

        // Create quest log entry for tracking
        if (PS_DefOf.PS_AllExtractionQuest != null)
        {
            Slate slate = new();
            slate.Set("map", map);
            slate.Set("allPawn", all);
            slate.Set("nothingPawn", nothing);
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(PS_DefOf.PS_AllExtractionQuest, slate);
            QuestUtility.SendLetterQuestAvailable(quest);
        }

        nothing.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk, forced: true);

        Messages.Message(AllTextStyling.Eldritchify("PS_Anomaly_Extraction_Message".Translate()), new LookTargets(all, nothing), MessageTypeDefOf.ThreatBig, false);
    }

    public void Notify_AllDied()
    {
        if (stage == AllDiscoveryStage.AllSpawned)
        {
            // All died before being researched — allow re-trigger
            allDiedUnresearched = true;
            stage = AllDiscoveryStage.None;
            lastStageChangeTick = Find.TickManager.TicksGame;
            allPawn = null;
        }
        else if (stage == AllDiscoveryStage.QuestActive)
        {
            // All died during the quest — quest fails
            stage = AllDiscoveryStage.QuestFailed;
            lastStageChangeTick = Find.TickManager.TicksGame;

            Find.LetterStack.ReceiveLetter("PS_Anomaly_QuestFail_LetterLabel".Translate(), "PS_Anomaly_QuestFail_LetterText".Translate(), LetterDefOf.NegativeEvent);

            allPawn = null;
            nothingPawn = null;
        }
    }

    public void Notify_NothingKilled()
    {
        if (stage != AllDiscoveryStage.QuestActive)
            return;

        stage = AllDiscoveryStage.QuestComplete;
        lastStageChangeTick = Find.TickManager.TicksGame;

        // All joins the colony permanently
        if (allPawn is { Spawned: true })
        {
            allPawn.SetFaction(Faction.OfPlayer);
            Find.LetterStack.ReceiveLetter(
                "PS_Anomaly_QuestSuccess_LetterLabel".Translate(),
                "PS_Anomaly_QuestSuccess_LetterText".Translate(),
                LetterDefOf.PositiveEvent,
                new LookTargets(allPawn)
            );
        }

        nothingPawn = null;
    }

    public void Notify_NothingLeftMap()
    {
        if (stage != AllDiscoveryStage.QuestActive)
            return;

        stage = AllDiscoveryStage.QuestFailed;
        lastStageChangeTick = Find.TickManager.TicksGame;

        // All despawns
        if (allPawn is { Spawned: true })
        {
            EffecterDefOf.Skip_EntryNoDelay.Spawn(allPawn, allPawn.Map).Cleanup();
            allPawn.DeSpawn();
        }

        Find.LetterStack.ReceiveLetter("PS_Anomaly_QuestFail_LetterLabel".Translate(), "PS_Anomaly_QuestFail_LetterText".Translate(), LetterDefOf.NegativeEvent);

        allPawn = null;
        nothingPawn = null;
    }

    /// <summary>
    /// Force the stage to a specific value. Used by debug actions.
    /// </summary>
    public void ForceStage(AllDiscoveryStage newStage)
    {
        stage = newStage;
        lastStageChangeTick = Find.TickManager.TicksGame;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref stage, "allFeatureStage");
        Scribe_Values.Look(ref lastStageChangeTick, "allFeatureLastStageTick");
        Scribe_Values.Look(ref allDiedUnresearched, "allDiedUnresearched");
        Scribe_References.Look(ref markedStandStage1, "markedStandStage1");
        Scribe_References.Look(ref markedStandStage2, "markedStandStage2");
        Scribe_References.Look(ref allPawn, "allPawn");
        Scribe_References.Look(ref nothingPawn, "nothingPawn");
    }
}
