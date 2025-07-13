using System;
using Verse;

namespace PawnStorages.Farm.Comps;

public partial class CompFarmBreeder
{
    public class FarmAnimalCharacteristics(bool adult, Gender gender) : IComparable<FarmAnimalCharacteristics>
    {
        public bool Adult = adult;
        public Gender Gender = gender;


        public override string ToString()
        {
            return $"BreedingCharacteristics[Adult:{Adult}, Gender:{Gender}]";
        }

        public int CullValue(AutoSlaughterConfig config)
        {
            return Adult switch
            {
                true when Gender == Gender.Male => config.maxMales,
                true when Gender == Gender.Female => config.maxFemales,
                false when Gender == Gender.Male => config.maxMalesYoung,
                false when Gender == Gender.Female => config.maxFemalesYoung,
                _ => config.maxTotal
            };
        }

        public int CompareTo(FarmAnimalCharacteristics other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (ReferenceEquals(null, other))
            {
                return 1;
            }

            switch (Adult)
            {
                case true when !other.Adult:
                    return -1;
                case false when other.Adult:
                    return 1;
            }

            if (Adult == other.Adult)
            {
                return Gender switch
                {
                    Gender.Male when other.Gender == Gender.Female => -1,
                    Gender.Female when other.Gender == Gender.Male => 1,
                    _ => 0
                };
            }

            return 0;
        }
    }
}
