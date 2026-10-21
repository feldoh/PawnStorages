using UnityEngine;
using Verse;

namespace PawnStorages;

public class PawnStoragesMod : Mod
{
    public static Settings settings;

    public PawnStoragesMod(ModContentPack content)
        : base(content)
    {
        // initialize settings
        settings = GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        settings.DoWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return "PS_Settings_Category".Translate();
    }
}
