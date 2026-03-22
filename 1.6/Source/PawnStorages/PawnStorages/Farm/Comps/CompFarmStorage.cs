using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnStorages.Farm.Comps
{
    [StaticConstructorOnStartup]
    public class CompFarmStorage : CompPawnStorage
    {
        public override string PawnTypeLabel => "PS_StoredAnimals".Translate();
        public new CompProperties_FarmStorage Props => props as CompProperties_FarmStorage;

        public override int MaxStoredPawns() => PawnStoragesMod.settings.MaxPawnsInFarm;

        public new bool CanAssign(Pawn pawn, bool couldMakePrisoner = false) =>
            compAssignable != null && pawn.Faction == Faction.OfPlayer && !pawn.RaceProps.Humanlike && (compAssignable.AssignedPawns.Contains(pawn) || compAssignable.HasFreeSlot);

        public IEnumerable<PawnKindDef> HeldPawnTypes => innerContainer.innerList.Select(p => p.kindDef).Distinct();

        public Dialog_AutoSlaughter.AnimalCountRecord CountForType(ThingDef def)
        {
            List<Pawn> pawns = innerContainer.innerList.Where(p => p.kindDef.race == def).ToList();
            int total = pawns.Count;
            int male = pawns.Count(p => p.gender == Gender.Male && p.ageTracker.Adult);
            int maleYoung = pawns.Count(p => p.gender == Gender.Male && !p.ageTracker.Adult);
            int female = pawns.Count(p => p.gender == Gender.Female && p.ageTracker.Adult);
            int femaleYoung = pawns.Count(p => p.gender == Gender.Female && !p.ageTracker.Adult);

            return new Dialog_AutoSlaughter.AnimalCountRecord(total, male, maleYoung, female, femaleYoung, 0, 0);
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption floatMenuOption in base.CompFloatMenuOptions(selPawn))
            {
                yield return floatMenuOption;
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (innerContainer.Count > 0)
            {
                yield return new Command_Action
                {
                    defaultLabel = "PS_ReleaseAnimals".Translate(),
                    action = delegate
                    {
                        ReleaseContents(parent.Map);
                    },
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/PS_Release"),
                };
                yield return new Command_Action
                {
                    defaultLabel = "PS_EjectAnimals".Translate(),
                    action = delegate
                    {
                        EjectContents(parent.Map);
                    },
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/PS_Eject"),
                };
            }

            if (DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Produce",
                    action = TryProduce,
                    icon = ContentFinder<Texture2D>.Get("UI/Buttons/ReleaseAll"),
                };
            }
        }

        public void TryProduce()
        {
            foreach (Pawn pawn in innerContainer)
            {
                foreach (CompHasGatherableBodyResource bodyResource in pawn.GetComps<CompHasGatherableBodyResource>())
                {
                    if (!bodyResource.Active)
                        continue;
                    int amount = bodyResource.ResourceAmount;
                    ThingDef thingDef = bodyResource.ResourceDef;
                    while (amount > 0)
                    {
                        int toSpawn = Mathf.Clamp(amount, 1, thingDef.stackLimit);
                        amount -= toSpawn;
                        Thing thingStack = ThingMaker.MakeThing(thingDef);
                        thingStack.stackCount = toSpawn;
                        GenPlace.TryPlaceThing(thingStack, parent.Position, parent.Map, ThingPlaceMode.Near);
                    }
                }

                CompEggLayer eggLayer = pawn.TryGetComp<CompEggLayer>();
                if (eggLayer is not { Active: true })
                    continue;
                ThingDef eggDef = eggLayer.Props.eggUnfertilizedDef;
                int eggAmount = eggLayer.Props.eggCountRange.RandomInRange;
                while (eggAmount > 0)
                {
                    int toSpawn = Mathf.Clamp(eggAmount, 1, eggDef.stackLimit);
                    eggAmount -= toSpawn;
                    Thing thingStack = ThingMaker.MakeThing(eggDef);
                    thingStack.stackCount = toSpawn;
                    GenPlace.TryPlaceThing(thingStack, parent.Position, parent.Map, ThingPlaceMode.Near);
                }
            }
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new();
            sb.AppendLine($"Stored: {innerContainer.Count}/{MaxStoredPawns()}");
            if (innerContainer?.Any<Pawn>() != true)
                return sb.ToString().TrimStart().TrimEnd();
            sb.AppendLine("PS_StoredPawns".Translate());
            foreach (Pawn pawn in innerContainer)
            {
                sb.AppendLine(pawn.needs.food.Starving ? $"    - {pawn.LabelCap} ({pawn.gender.GetLabel()}) [Starving!]" : $"    - {pawn.LabelCap} ({pawn.gender.GetLabel()})");
            }

            return sb.ToString().TrimStart().TrimEnd();
        }
    }
}
