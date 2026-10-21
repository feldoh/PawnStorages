using System.Collections.Generic;
using System.Linq;
using PawnStorages.Farm.Comps;
using RimWorld;
using UnityEngine;
using Verse;
using Color = UnityEngine.Color;

namespace PawnStorages.Farm
{
    public class ITab_Assignments : ITab
    {
        private static readonly Vector2 WinSize = new(300f, 480f);
        private Vector2 scrollPos = new(0, 0);
        public const float LineHeight = 60f;

        public CompFarmStorage compFarmStorage => SelThing.TryGetComp<CompFarmStorage>();

        public ITab_Assignments()
        {
            size = ITab_PenAnimals.WinSize;
            labelKey = "PS_AssignmentTab";
        }

        public void DrawLine(float position, float width, Pawn animal, ref bool alternate)
        {
            Rect rect = new Rect(0.0f, position, width, LineHeight);

            FarmJob_MapComponent comp = SelThing.Map.GetComponent<FarmJob_MapComponent>();
            if (comp == null)
                return;

            if (alternate)
            {
                Widgets.DrawRectFast(rect, new Color(1f, 1f, 1f, ITab_Pawn_Log_Utility.AlternateAlpha));
            }

            alternate = !alternate;

            Widgets.DefIcon(new Rect(5f, position + 7.5f, 45f, 45f), animal.def, drawPlaceholder: true, color: Listing_TreeThingFilter.NoMatchColor);

            Widgets.Label(new Rect(55f, position, width - 90f, 20f), "PS_FarmTab_Name".Translate(animal.NameFullColored));
            if (comp.farmAssignments.TryGetValue(animal, out Building f))
            {
                if (f.HasComp<CompFarmStorage>())
                {
                    Widgets.Label(new Rect(55f, position + 20f, width - 120f, 40f), "PS_AssignmentTab_AssignedTo".Translate(f.Label).ToString());
                }
            }

            Rect btn = new Rect(new Vector2(width - 80f, position + 15f), new Vector2(70f, 30f));

            if (!Widgets.ButtonText(btn, "PS_AssignmentTab_Set".Translate(), true, false, true))
                return;

            string message = "PS_AssignmentTab_Assigned".Translate(animal.NameFullColored, compFarmStorage.parent.Label);

            if (comp.farmAssignments.TryGetValue(animal, out Building farm))
            {
                if (farm.HasComp<CompFarmStorage>())
                {
                    message = "PS_AssignmentTab_Reassigned".Translate(animal.NameFullColored, farm.Label, compFarmStorage.parent.Label);
                }
            }

            Messages.Message(message, MessageTypeDefOf.RejectInput, false);
            comp.farmAssignments[animal] = compFarmStorage.parent as Building;
        }

        public override void FillTab()
        {
            if (compFarmStorage == null)
                return;

            Widgets.Label(new Rect(5.0f, 0.0f, WinSize.x, 30f), "PS_AssignmentTab_TopLabel".Translate());

            Rect tabRect = new Rect(0.0f, 30.0f, WinSize.x, WinSize.y - 30f).ContractedBy(10f);
            Rect scrollViewRect = new Rect(tabRect);

            List<Pawn> animals = Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).Where(p => p.IsAnimal).ToList();

            float totalHeight = animals.Count * LineHeight;

            Rect viewRect = new Rect(0.0f, 0.0f, scrollViewRect.width, totalHeight);

            Widgets.AdjustRectsForScrollView(tabRect, ref scrollViewRect, ref viewRect);
            Widgets.BeginScrollView(scrollViewRect, ref scrollPos, viewRect);

            bool alternate = false;
            float num = 0.0f;
            List<Pawn> removed = [];
            foreach (Pawn thing in animals)
            {
                Pawn animal = (Pawn)thing;
                DrawLine(num, scrollViewRect.width, animal, ref alternate);
                num += LineHeight;
            }

            Widgets.EndScrollView();
        }
    }
}
