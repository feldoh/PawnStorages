using UnityEngine;
using Verse;

namespace PawnStorages.Mech;

public class Gizmo_MechExitThreshold : Gizmo_Slider
{
    private readonly CompMechStorage comp;
    private static bool dragging;

    public Gizmo_MechExitThreshold(CompMechStorage comp)
    {
        this.comp = comp;
    }

    public override float Target
    {
        get => comp.mechMinExitThreshold;
        set => comp.mechMinExitThreshold = value;
    }

    public override float ValuePercent
    {
        get
        {
            // Show average projected charge of stored mechs as the fill bar
            int count = 0;
            float total = 0f;
            foreach (Pawn pawn in comp.innerContainer)
            {
                if (!pawn.IsColonyMech)
                    continue;
                var energy = pawn.needs?.TryGetNeed<RimWorld.Need_MechEnergy>();
                if (energy == null)
                    continue;
                total += comp.GetProjectedEnergyLevel(pawn) / energy.MaxLevel;
                count++;
            }
            return count > 0 ? total / count : 0f;
        }
    }

    public override bool IsDraggable => true;
    public override bool DraggingBar
    {
        get => dragging;
        set => dragging = value;
    }
    public override string Title => "PS_MechExitThreshold".Translate();

    public override string GetTooltip() => "PS_MechExitThresholdTooltip".Translate();
}
