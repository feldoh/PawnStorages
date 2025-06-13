using HarmonyLib;
using Verse;

namespace PawnStorages.DefensivePositions;

public class PawnStoragesModule_DefensivePositions(ModContentPack content) : Mod(content);

[StaticConstructorOnStartup]

public static class HarmonyConfig
{
    static HarmonyConfig()
    {
#if DEBUG
        Harmony.DEBUG = true;
#endif
        new Harmony("PawnStorages.Mod.DefensivePositions").PatchAll();
    }
}
