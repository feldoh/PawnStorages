using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PawnStorages;

public class CompProperties_PawnStorageNutrition : CompProperties
{
    public float maxNutrition = 0;
    public int ticksToAbsorbNutrients = 0;
    public int pawnTickInterval = 0;

    // don't feed pawns until they reach this level
    public float pawnFeedThreshold = 0.75f;
    public float MaxNutrition => maxNutrition > 0 ? maxNutrition : PawnStoragesMod.settings.MaxFarmStoredNutrition;
    public int TicksToAbsorbNutrients => ticksToAbsorbNutrients > 0 ? ticksToAbsorbNutrients : PawnStoragesMod.settings.TicksToAbsorbNutrients;
    public virtual int PawnTickInterval => pawnTickInterval > 0 ? pawnTickInterval : PawnStoragesMod.settings.AnimalTickInterval;

    public bool HasTip = false;
    public float TipScale;
    public string MainTex;
    public string AltTex;

    public string Tip;
    public string SansTip;

    public static Dictionary<string, Material> MaterialCache = new();

    public Material MainTexture
    {
        get
        {
            string matPath = !HasTip ? MainTex : SansTip;

            if (MaterialCache.TryGetValue(matPath, out Material mat))
                return mat;

            mat = MaterialPool.MatFrom(matPath, ShaderDatabase.Transparent, Color.white);

            MaterialCache[matPath] = mat;

            return mat;
        }
    }

    public Material TipTexture
    {
        get
        {
            if (MaterialCache.TryGetValue(Tip, out Material mat))
                return mat;

            mat = MaterialPool.MatFrom(Tip, ShaderDatabase.Transparent, Color.white);

            MaterialCache[Tip] = mat;

            return mat;
        }
    }

    public CompProperties_PawnStorageNutrition()
    {
        compClass = typeof(CompPawnStorageNutrition);
    }
}
