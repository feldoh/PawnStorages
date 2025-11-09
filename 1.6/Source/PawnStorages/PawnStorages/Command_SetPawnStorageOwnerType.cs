using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages;

[StaticConstructorOnStartup]
public class Command_SetPawnStorageOwnerType : Command
{
    private CompAssignableToPawn_PawnStorage assignable;

    public Command_SetPawnStorageOwnerType(CompAssignableToPawn_PawnStorage assignable)
    {
        this.assignable = assignable;
        switch (assignable.OwnerType)
        {
            case BedOwnerType.Colonist:
                defaultLabel = "ForColonistUse".Translate();
                icon = Command_SetBedOwnerType.ForColonistsTex;
                break;
            case BedOwnerType.Prisoner:
                defaultLabel = "CommandBedSetForPrisonersLabel".Translate();
                icon = Command_SetBedOwnerType.ForPrisonersTex;
                break;
            case BedOwnerType.Slave:
                defaultLabel = "PS_Storage_Use_Slaves".Translate();
                icon = Command_SetBedOwnerType.ForSlavesTex;
                break;
            default:
                Log.Error($"Unknown owner type selected for assignable: {assignable.OwnerType}");
                break;
        }

        defaultDesc = "PS_Storage_Use_Desc".Translate();
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        List<FloatMenuOption> options =
        [
            new FloatMenuOption("ForColonistUse".Translate(), () => assignable.OwnerType = BedOwnerType.Colonist, Command_SetBedOwnerType.ForColonistsTex, Color.white),
            new FloatMenuOption(
                "CommandBedSetForPrisonersLabel".Translate(),
                () => assignable.OwnerType = BedOwnerType.Prisoner,
                Command_SetBedOwnerType.ForPrisonersTex,
                Color.white
            ),
        ];
        if (ModsConfig.IdeologyActive)
            options.Add(new("PS_Storage_Use_Slaves".Translate(), () => assignable.OwnerType = BedOwnerType.Prisoner, Command_SetBedOwnerType.ForSlavesTex, Color.white));
        Find.WindowStack.Add(new Verse.FloatMenu(options));
    }
}
