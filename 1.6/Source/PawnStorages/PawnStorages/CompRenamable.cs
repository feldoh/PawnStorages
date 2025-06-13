using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PawnStorages;


[StaticConstructorOnStartup]
public class CompRenamable: ThingComp
{
    private static readonly Texture2D Rename;

    static CompRenamable()
    {
        Rename = ContentFinder<Texture2D>.Get("UI/Buttons/PS_Rename");
    }

    public string NewLabel;

    public override string TransformLabel(string label)
    {
        label = base.TransformLabel(label);
        return NewLabel.NullOrEmpty() ? label : NewLabel;
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look<string>(ref NewLabel, "NewLabel");
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (Gizmo gizmo in base.CompGetGizmosExtra())
        {
            yield return gizmo;
        }

        Command_Action rename = new Command_Action();
        rename.defaultLabel = "Rename";
        rename.icon = Rename;
        rename.action = delegate()
        {
            Find.WindowStack.Add(new RenameDialog(this, NewLabel));
        };

        yield return rename;
    }
}
