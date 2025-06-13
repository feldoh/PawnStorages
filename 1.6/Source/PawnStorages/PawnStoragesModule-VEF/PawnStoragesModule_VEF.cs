using HarmonyLib;
using Verse;

namespace PawnStorages.VEF;

public class PawnStoragesModule_VEF : Mod
{
    public PawnStoragesModule_VEF(ModContentPack content) : base(content)
    {
        Log.Message("PawnStoragesModule_VEF loaded");
#if DEBUG
        Harmony.DEBUG = true;
#endif
        new Harmony("PawnStorages.Mod.VEF").PatchAll();
    }
}
