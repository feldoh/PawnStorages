using UnityEngine;
using Verse;

namespace PawnStorages;

public class Settings : ModSettings
{
    public bool AllowNeedsDrop = true;
    public bool SpecialReleaseAll = false;
    public string ForcedPawn = "";
    public bool ShowStoredPawnsInBar = false;
    public float ProductionScale = 0.5f;
    public float BreedingScale = 2f;
    public int MaxPawnsInFarm = 16;
    public float MaxFarmStoredNutrition = 500f;
    public int TicksToAbsorbNutrients = 50;
    public int AnimalTickInterval = 250;
    public int ProductionsPerDay = 1;
    public bool SuggestiveSilo = false;
    public bool RusticFarms = false;
    public bool DebugLogging = false;

    public const float GapHeight = 8f;

    public void DoWindowContents(Rect wrect)
    {
        Listing_Standard options = new();
        options.Begin(wrect);

        options.CheckboxLabeled("PS_Settings_AllowNeedsDrop".Translate(), ref AllowNeedsDrop);
        options.CheckboxLabeled("PS_Settings_DebugLogging".Translate(), ref DebugLogging);
        if (ModsConfig.anomalyActive)
            options.CheckboxLabeled("PS_Settings_SpecialReleaseAll".Translate(), ref SpecialReleaseAll);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Advanced".Translate());
        ForcedPawn = options.TextEntryLabeled("PS_Settings_ForceNextPawnStatue".Translate(), ForcedPawn);
        options.Gap(GapHeight);
        bool showStoredPawnsInBarBefore = ShowStoredPawnsInBar;
        options.CheckboxLabeled("PS_Settings_ShowStoredPawnsInBar".Translate(), ref ShowStoredPawnsInBar);
        options.CheckboxLabeled("PS_Settings_RusticFarms".Translate(), ref RusticFarms);
        if (showStoredPawnsInBarBefore != ShowStoredPawnsInBar)
        {
            Find.ColonistBar.MarkColonistsDirty();
        }
        options.CheckboxLabeled("PS_Settings_SuggestiveSilo".Translate(), ref SuggestiveSilo);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Production_Scale".Translate(ProductionScale.ToString("0.00")));
        ProductionScale = options.Slider(ProductionScale, 0f, 10f);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Breeding_Scale".Translate(BreedingScale.ToString("0.00")));
        BreedingScale = options.Slider(BreedingScale, 0f, 10f);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Max_Farm".Translate(MaxPawnsInFarm));
        options.IntAdjuster(ref MaxPawnsInFarm, 1, 1);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Max_Farm_Nutrition".Translate(MaxFarmStoredNutrition));
        MaxFarmStoredNutrition = options.Slider(MaxFarmStoredNutrition, 0f, 500f);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Ticks_To_Absorb_Nutrients".Translate(TicksToAbsorbNutrients));
        options.IntAdjuster(ref TicksToAbsorbNutrients, 1, 1);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Animal_Tick_Interval".Translate(AnimalTickInterval));
        options.IntAdjuster(ref AnimalTickInterval, 1, 1);
        options.Gap(GapHeight);
        options.Label("PS_Settings_Farm_ProductionsPerDay".Translate(ProductionsPerDay));
        options.IntAdjuster(ref ProductionsPerDay, 1, 1);
        options.Gap(GapHeight);

        options.Gap(GapHeight);
        if (options.ButtonText("PS_Reset".Translate()))
        {
            AllowNeedsDrop = true;
            SpecialReleaseAll = false;
            ForcedPawn = "";
            ShowStoredPawnsInBar = false;
            ProductionScale = 0.5f;
            BreedingScale = 2f;
            MaxPawnsInFarm = 16;
            MaxFarmStoredNutrition = 500f;
            TicksToAbsorbNutrients = 50;
            AnimalTickInterval = 250;
            ProductionsPerDay = 1;
            SuggestiveSilo = false;
            RusticFarms = false;
            DebugLogging = false;
        }

        options.End();
    }

    public void AllReleased()
    {
        SpecialReleaseAll = false;
        Write();
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref AllowNeedsDrop, "AllowNeedsDrop", true);
        Scribe_Values.Look(ref SpecialReleaseAll, "SpecialReleaseAll", false);
        Scribe_Values.Look(ref ShowStoredPawnsInBar, "ShowStoredPawnsInBar", false);
        Scribe_Values.Look(ref ProductionScale, "ProductionScale", 0.5f);
        Scribe_Values.Look(ref BreedingScale, "BreedingScale", 2);
        Scribe_Values.Look(ref MaxPawnsInFarm, "MaxPawnsInFarm", 16);
        Scribe_Values.Look(ref MaxFarmStoredNutrition, "MaxFarmStoredNutrition", 500);
        Scribe_Values.Look(ref ProductionsPerDay, "ProductionsPerDay", 1);
        Scribe_Values.Look(ref SuggestiveSilo, "SuggestiveSilo", false);
        Scribe_Values.Look(ref RusticFarms, "RusticFarms", false);
        Scribe_Values.Look(ref DebugLogging, "DebugLogging", false);
        Scribe_Values.Look(ref ForcedPawn, "ForcedPawn", "");
    }
}
