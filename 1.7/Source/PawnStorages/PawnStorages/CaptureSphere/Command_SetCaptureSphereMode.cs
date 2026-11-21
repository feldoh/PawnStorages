using System.Collections.Generic;
using PawnStorages.CaptureMode;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.CaptureSphere;

public class Command_SetCaptureSphereMode : Command
{
    public Command_SetCaptureSphereMode(CompCaptureSphereMode comp)
    {
        Comp = comp;
        RefreshLabel();
    }

    public CompCaptureSphereMode Comp { get; }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        RefreshLabel();
        List<FloatMenuOption> options = [];
        foreach (CaptureModeDef def in DefDatabase<CaptureModeDef>.AllDefs)
        {
            CaptureModeDef local = def;
            options.Add(new FloatMenuOption(local.LabelCap, () => Comp.SetMode(local), ModeIcon(), Color.white));
        }
        Find.WindowStack.Add(new Verse.FloatMenu(options));
    }

    private void RefreshLabel() => defaultLabel = Comp.EffectiveMode?.LabelCap ?? "PS_SphereMode".Translate();

    private static Texture2D ModeIcon() => ContentFinder<Texture2D>.Get("Things/Item/Special/PS_CaptureBall");
}
