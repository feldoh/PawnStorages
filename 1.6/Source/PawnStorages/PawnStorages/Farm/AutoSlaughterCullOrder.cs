using Verse;

namespace PawnStorages.Farm;

public class AutoSlaughterCullOrder: IExposable
{
    public bool AllAscending = false;
    public bool AdultMaleAscending = false;
    public bool AdultFemaleAscending = false;
    public bool ChildMaleAscending = true;
    public bool ChildFemaleAscending = true;

    public bool IsAscending(bool adult, Gender gender)
    {
        return adult switch
        {
            true when gender == Gender.Male => AdultMaleAscending,
            true when gender == Gender.Female => AdultFemaleAscending,
            false when gender == Gender.Male => ChildMaleAscending,
            false when gender == Gender.Female => ChildFemaleAscending,
            _ => AllAscending
        };
    }
    public void ExposeData()
    {
        Scribe_Values.Look(ref AllAscending, "AllAscending");
        Scribe_Values.Look(ref AdultMaleAscending, "AdultMaleAscending");
        Scribe_Values.Look(ref AdultFemaleAscending, "AdultFemaleAscending");
        Scribe_Values.Look(ref ChildMaleAscending, "ChildMaleAscending");
        Scribe_Values.Look(ref ChildFemaleAscending, "ChildFemaleAscending");
    }
}
