using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PawnStorages.Mech;

/// <summary>
/// Per-map component that checks whether stored mechs have available work.
/// Uses the game's own WorkGiver system so modded mechs with custom work types
/// are handled automatically.
///
/// For each of the mech's race-level WorkTypeDefs (RaceProps.mechEnabledWorkTypes — these are
/// fixed per mech kind, not configurable per individual), iterates the associated
/// WorkGiver_Scanners and calls ShouldSkip(pawn) — the same cheap gate the
/// game uses internally. If any WorkGiver says "don't skip", work is available.
///
/// Because stored pawns are despawned (pawn.Map == null), we temporarily inject
/// the storage building's map index before calling ShouldSkip, then restore it.
///
/// As a fast pre-filter, WorkGivers with a concrete ThingRequest are checked
/// against map.listerThings first — if zero matching things exist, ShouldSkip
/// is skipped entirely for that WorkGiver.
///
/// The static mapping from WorkTypeDef → WorkGiver_Scanner list is built once
/// from DefDatabase and cached for the session.
/// </summary>
public class MechWorkTracker : MapComponent
{
    // Static cache: WorkTypeDef → list of (scanner, thingRequest) pairs
    private static Dictionary<WorkTypeDef, List<WorkGiverEntry>> workersByWorkType;

    private readonly struct WorkGiverEntry
    {
        public readonly WorkGiver_Scanner Scanner;
        public readonly ThingRequest Request; // may be Undefined
        public readonly string DefName; // for debug logging

        public WorkGiverEntry(WorkGiver_Scanner scanner, ThingRequest request, string defName)
        {
            Scanner = scanner;
            Request = request;
            DefName = defName;
        }
    }

    public MechWorkTracker(Map map)
        : base(map) { }

    private static void BuildStaticCaches()
    {
        if (workersByWorkType != null)
            return;

        workersByWorkType = new Dictionary<WorkTypeDef, List<WorkGiverEntry>>();

        foreach (WorkGiverDef def in DefDatabase<WorkGiverDef>.AllDefs)
        {
            if (def.workType == null)
                continue;
            if (def.Worker is not WorkGiver_Scanner scanner)
                continue;

            if (!workersByWorkType.TryGetValue(def.workType, out List<WorkGiverEntry> list))
            {
                list = new List<WorkGiverEntry>();
                workersByWorkType[def.workType] = list;
            }

            list.Add(new WorkGiverEntry(scanner, scanner.PotentialWorkThingRequest, def.defName));
        }
    }

    /// <summary>
    /// Returns true if there is available work on the map that matches
    /// any of this mech's enabled work types.
    /// </summary>
    public bool HasWorkForMech(Pawn mech)
    {
        if (!ModsConfig.BiotechActive)
            return false;
        if (!mech.RaceProps.IsMechanoid)
            return false;

        BuildStaticCaches();

        List<WorkTypeDef> mechWorkTypes = mech.RaceProps.mechEnabledWorkTypes;
        if (mechWorkTypes.NullOrEmpty())
            return false;

        bool debug = PawnStoragesMod.settings.DebugLogging;

        // Temporarily give the stored pawn a valid map so ShouldSkip works
        // (mapIndexOrState is accessible via Krafs.Publicizer)
        sbyte originalIndex = mech.mapIndexOrState;
        mech.mapIndexOrState = (sbyte)map.Index;

        try
        {
            for (int i = 0; i < mechWorkTypes.Count; i++)
            {
                WorkTypeDef wt = mechWorkTypes[i];

                if (!workersByWorkType.TryGetValue(wt, out List<WorkGiverEntry> entries))
                    continue;

                for (int j = 0; j < entries.Count; j++)
                {
                    WorkGiverEntry entry = entries[j];

                    // Fast pre-filter: if this WorkGiver has a concrete ThingRequest and
                    // zero matching things exist on the map, skip it entirely
                    if (entry.Request.group != ThingRequestGroup.Undefined && map.listerThings.ThingsMatching(entry.Request).Count == 0)
                        continue;

                    // Authoritative check: ShouldSkip is what the game uses to gate work
                    if (!entry.Scanner.ShouldSkip(mech))
                    {
                        if (debug)
                            Log.Message($"[MechWorkTracker] {mech.LabelShort}: {wt.defName} has work via {entry.DefName} (ShouldSkip=false)");
                        return true;
                    }
                }
            }
        }
        finally
        {
            mech.mapIndexOrState = originalIndex;
        }

        if (debug)
            Log.Message($"[MechWorkTracker] {mech.LabelShort}: No work found across {mechWorkTypes.Count} work types");
        return false;
    }
}
