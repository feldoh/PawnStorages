using System;
using System.Collections.Generic;
using Verse;

namespace PawnStorages;

public class CompProperties_RightClickToWorkOnBills : CompProperties
{
    public List<RecipeDef> recipeToCallRightClick;

    [Obsolete("Redundant way for a very very specific way to do recipes")]
    public CompProperties_RightClickToWorkOnBills()
    {
        compClass = typeof(CompRightClickToWorkOnBills);
    }
}
