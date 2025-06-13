using System.Collections.Generic;
using Verse;

namespace PawnStorages.Farm.Interfaces
{
    public interface IFarmTabParent
    {
        public List<ThingDef> AllowableThing { get; }

        public Dictionary<ThingDef, bool> AllowedThings { get; }

        public bool Allowed(ThingDef potentialDef);

        public void AllowAll();
        public void DenyAll();
    }
}
