using HarmonyLib;
using Verse;

namespace PawnStorages.SimpleWarrants;

public class PawnStoragesModule_SimpleWarrants(ModContentPack content) : Mod(content);

[StaticConstructorOnStartup]

public static class HarmonyConfig
{
    static HarmonyConfig()
    {
#if DEBUG
        Harmony.DEBUG = true;
#endif
        new Harmony("PawnStorages.Mod.SimpleWarrants").PatchAll();
    }
}
