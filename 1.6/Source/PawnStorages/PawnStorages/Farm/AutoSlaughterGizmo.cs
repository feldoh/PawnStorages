using System.Collections.Generic;
using System.Linq;
using PawnStorages.Farm.Comps;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Farm;

public class AutoSlaughterGizmo(CompFarmBreeder breeder) : Gizmo
{
    private float lastTargetValue;
    private float targetValue = breeder.AutoSlaughterTarget;

    public static int increments = 16;

    public static IEnumerable<float> BandPercentages => from num in Enumerable.Range(0, increments) select 1/(num * 1f);

    public int TargetValue
    {
        get
        {
            return Mathf.CeilToInt(targetValue*increments);
        }
        set
        {
            targetValue = value / (increments * 1.0f);
        }
    }

    public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
    {
      Rect outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
      Rect insetRect = outerRect.ContractedBy(6f);

      Widgets.DrawWindowBackground(outerRect);
      Text.Font = GameFont.Small;

      Rect autoSlaughterLabel = insetRect;
      autoSlaughterLabel.y += 38f;
      autoSlaughterLabel.height = Text.LineHeight;
      Widgets.Label(autoSlaughterLabel, "PS_AutoSlaughterLevel".Translate());

      Rect autoSlaughterBarRect = insetRect;
      autoSlaughterBarRect.x += 63f;
      autoSlaughterBarRect.y += 38f;
      autoSlaughterBarRect.width = 100f;
      autoSlaughterBarRect.height = 22f;
      lastTargetValue = targetValue;


      Widgets.DraggableBar(
          autoSlaughterBarRect,
          PsychicEntropyGizmo.PsyfocusBarTex,
          PsychicEntropyGizmo.PsyfocusBarHighlightTex,
          PsychicEntropyGizmo.EmptyBarTex,
          PsychicEntropyGizmo.PsyfocusTargetTex,
          ref PsychicEntropyGizmo.draggingBar,
          targetValue,
          ref targetValue,
          BandPercentages,
          increments);
      if (Mathf.CeilToInt(lastTargetValue) != TargetValue)
      {
          breeder.AutoSlaughterTarget = TargetValue;
      }

      return new GizmoResult(GizmoState.Clear);
    }

    public override float GetWidth(float maxWidth) => 212f;
}
