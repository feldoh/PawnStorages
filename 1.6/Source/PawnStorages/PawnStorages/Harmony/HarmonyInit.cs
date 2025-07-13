using HarmonyLib;
using Verse;

namespace PawnStorages;

[StaticConstructorOnStartup]
public static class HarmonyInit
{
    public static Harmony harmonyInstance;

    static HarmonyInit()
    {
#if DEBUG
        Harmony.DEBUG = true;
#endif
        harmonyInstance = new Harmony("PawnStorages.Mod");
        harmonyInstance.PatchAll();
    }
}
