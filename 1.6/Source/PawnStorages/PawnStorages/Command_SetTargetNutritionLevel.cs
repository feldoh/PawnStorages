using UnityEngine;
using Verse;

namespace PawnStorages;

public class Command_SetTargetNutritionLevel : Command
{
    public CompPawnStorageNutrition nutritionComp;

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        int to = (int) nutritionComp.MaxNutrition;
        Dialog_Slider dialogSlider = new(x => "PS_NutritionLevel".Translate(x), 0, to,
            value =>
            {
                nutritionComp.TargetNutritionLevel = value;
            }, nutritionComp.TargetNutritionLevel <= 0 ? to : (int) nutritionComp.TargetNutritionLevel);
        Find.WindowStack.Add(dialogSlider);
    }
}
