namespace PawnStorages.Interfaces;

public interface INutritionStoreAlternative
{
    public float MaxStoreSize { get; }
    public float CurrentStored { get; set; }
}
