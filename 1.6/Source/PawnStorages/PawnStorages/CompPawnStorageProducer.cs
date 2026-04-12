using System.Collections.Generic;
using PawnStorages.Interfaces;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class CompPawnStorageProducer : ThingComp, IActive
{
    public IProductionParent ParentAsProductionParent => parent as IProductionParent;

    protected List<Thing> DaysProduce = [];
    public bool ProduceNow = false;
    protected List<IntVec3> outputCells = new List<IntVec3>();

    public List<IntVec3> OutputCells => outputCells;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref DaysProduce, "daysProduce", LookMode.Deep);
        Scribe_Collections.Look(ref outputCells, "outputCells", LookMode.Value);
        if (outputCells == null)
            outputCells = new List<IntVec3>();
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (outputCells.Count > 0)
        {
            List<IntVec3> validCandidates = ValidOutputCellCandidates();
            outputCells.RemoveAll(cell => !validCandidates.Contains(cell));
        }
    }

    protected List<Thing> TryPlaceProducts()
    {
        List<Thing> failedToPlace = new List<Thing>();
        foreach (Thing thing in DaysProduce)
        {
            if (!TryPlaceThingInOutputCells(thing))
            {
                failedToPlace.Add(thing);
            }
        }
        return failedToPlace;
    }

    private bool TryPlaceThingInOutputCells(Thing thing)
    {
        if (outputCells.Count == 0)
        {
            return GenPlace.TryPlaceThing(thing, parent.Position, parent.Map, ThingPlaceMode.Near);
        }

        // First pass: try to merge with existing stacks
        for (int i = 0; i < outputCells.Count; i++)
        {
            IntVec3 cell = outputCells[i];
            if (!cell.InBounds(parent.Map))
                continue;

            List<Thing> thingsAtCell = cell.GetThingList(parent.Map);
            for (int j = 0; j < thingsAtCell.Count; j++)
            {
                Thing existingThing = thingsAtCell[j];
                if (existingThing.CanStackWith(thing))
                {
                    existingThing.TryAbsorbStack(thing, true);
                    if (thing.Destroyed || thing.stackCount == 0)
                        return true;
                }
            }
        }

        // Second pass: try to place on any output cell with room
        for (int i = 0; i < outputCells.Count; i++)
        {
            IntVec3 cell = outputCells[i];
            if (!cell.InBounds(parent.Map))
                continue;

            if (GenPlace.TryPlaceThing(thing, cell, parent.Map, ThingPlaceMode.Direct))
                return true;
        }

        // All output cells full — keep for next cycle
        return false;
    }

    public List<IntVec3> ValidOutputCellCandidates()
    {
        List<IntVec3> candidates = new List<IntVec3>();
        foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(parent))
        {
            if (cell.InBounds(parent.Map) && cell.Standable(parent.Map))
            {
                candidates.Add(cell);
            }
        }
        return candidates;
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Command_Action
        {
            defaultLabel = "PS_ProduceNow".Translate(DaysProduce.Count),
            action = delegate
            {
                ProduceNow = true;
            },
            icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
            disabled = DaysProduce.Count <= 0,
            disabledReason = "PS_NothingToProduce".Translate(),
        };

        yield return new Command_Action
        {
            defaultLabel = "PS_SetOutputCell".Translate(),
            defaultDesc = "PS_SetOutputCellDesc".Translate(),
            icon = TexCommand.Install,
            action = delegate
            {
                List<IntVec3> candidates = ValidOutputCellCandidates();
                Find.Targeter.BeginTargeting(
                    new TargetingParameters
                    {
                        canTargetLocations = true,
                        canTargetBuildings = false,
                        canTargetPawns = false,
                        validator = delegate(TargetInfo target)
                        {
                            return candidates.Contains(target.Cell);
                        }
                    },
                    delegate(LocalTargetInfo target)
                    {
                        if (!outputCells.Contains(target.Cell))
                        {
                            outputCells.Add(target.Cell);
                        }
                    }
                );
            }
        };

        if (outputCells.Count > 0)
        {
            yield return new Command_Action
            {
                defaultLabel = "PS_ClearOutputCells".Translate(),
                defaultDesc = "PS_ClearOutputCellsDesc".Translate(),
                icon = TexCommand.ClearPrioritizedWork,
                action = delegate
                {
                    outputCells.Clear();
                }
            };
        }
    }

    public virtual bool IsActive => true;
}
