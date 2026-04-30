using Verse;

namespace PawnStorages.Farm;

public class AutoSlaughterMinimums : IExposable
{
    public int MinMales;
    public int MinFemales;
    public int MinMalesYoung;
    public int MinFemalesYoung;

    public string uiMinMalesBuffer;
    public string uiMinFemalesBuffer;
    public string uiMinMalesYoungBuffer;
    public string uiMinFemalesYoungBuffer;

    public int MinFor(bool adult, Gender gender)
    {
        return adult switch
        {
            true when gender == Gender.Male => MinMales,
            true when gender == Gender.Female => MinFemales,
            false when gender == Gender.Male => MinMalesYoung,
            false when gender == Gender.Female => MinFemalesYoung,
            _ => 0,
        };
    }

    public bool AnySet => MinMales > 0 || MinFemales > 0 || MinMalesYoung > 0 || MinFemalesYoung > 0;

    public void ExposeData()
    {
        Scribe_Values.Look(ref MinMales, "MinMales");
        Scribe_Values.Look(ref MinFemales, "MinFemales");
        Scribe_Values.Look(ref MinMalesYoung, "MinMalesYoung");
        Scribe_Values.Look(ref MinFemalesYoung, "MinFemalesYoung");
    }
}
