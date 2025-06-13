using System.Collections.Generic;
using System.Text;
using PawnStorages.Farm.Comps;
using RimWorld;
using UnityEngine;
using Verse;
using Color = UnityEngine.Color;

namespace PawnStorages.Farm
{
    public class ITab_Production : ITab
    {
        private static readonly Vector2 WinSize = new(300f, 480f);
        public readonly ThingFilterUI.UIState ThingFilterState = new();
        public const float LineHeight = 60f;
        private bool alternate;

        public CompFarmStorage compFarmStorage => SelThing.TryGetComp<CompFarmStorage>();
        public CompFarmProducer compFarmProducer => SelThing.TryGetComp<CompFarmProducer>();

        public bool NeedsDrop => PawnStoragesMod.settings.AllowNeedsDrop || compFarmStorage.Props.needsDrop;

        public ITab_Production()
        {
            size = ITab_PenAnimals.WinSize;
            labelKey = "PS_ProductionTab";
        }

        public float PawnFullness(Pawn pawn)
        {
            if (pawn.TryGetComp(out CompEggLayer compLayer))
            {
                return compLayer.eggProgress;
            }

            if (pawn.TryGetComp(out CompHasGatherableBodyResource compGatherable))
            {
                return compGatherable.Fullness;
            }

            return 0;
        }

        public bool DrawLine(float position, float width, Pawn pawn)
        {
            Rect rect = new Rect(0.0f, position, width, LineHeight);

            if (alternate)
            {
                Widgets.DrawRectFast(rect, new Color(1f, 1f, 1f, ITab_Pawn_Log_Utility.AlternateAlpha));
            }

            alternate = !alternate;

            Widgets.DefIcon(new Rect(5f, position + 7.5f, 45f, 45f), pawn.def, drawPlaceholder: true, color: Listing_TreeThingFilter.NoMatchColor);

            StringBuilder label = new(pawn.LabelShort);

            if (NeedsDrop)
            {
                Widgets.Label(new Rect(55f, position, width - 90f, 20f),
                    (pawn.needs?.food?.Starving ?? false ? "PS_FarmTab_NameStarving" : "PS_FarmTab_Name").Translate(pawn.LabelShort));

                if (compFarmProducer != null)
                {
                    Widgets.Label(new Rect(55f, position + 20f, width - 90f, 20f),
                        "PS_FarmTab_Nutrition".Translate((pawn.needs?.food?.CurLevelPercentage ?? 0f).ToStringPercent()));
                    Widgets.Label(new Rect(55f, position + 40f, width - 90f, 20f),
                        "PS_FarmTab_Fullness".Translate(PawnFullness(pawn).ToStringPercent(), pawn.gender.GetLabel(animal: true)));
                }
            }

            Rect btn = new Rect(new Vector2(width - 50f, position + 15f), new Vector2(30f, 30f));

            return Widgets.ButtonImage(btn, TexButton.Drop, Color.white, GenUI.MouseoverColor);
        }

        public override void FillTab()
        {
            if (compFarmStorage == null) return;
            if (compFarmStorage.GetDirectlyHeldThings().Count <= 0) return;

            Widgets.Label(new Rect(5.0f, 0.0f, WinSize.x, 30f), "PS_FarmTab_TopLabel".Translate());

            Rect tabRect = new Rect(0.0f, 30.0f, WinSize.x, WinSize.y - 30f).ContractedBy(10f);
            Rect scrollViewRect = new Rect(tabRect);

            float totalHeight = compFarmStorage.GetDirectlyHeldThings().Count * LineHeight;

            Rect viewRect = new Rect(0.0f, 0.0f, scrollViewRect.width, totalHeight);

            Widgets.AdjustRectsForScrollView(tabRect, ref scrollViewRect, ref viewRect);
            Widgets.BeginScrollView(scrollViewRect, ref ThingFilterState.scrollPosition, viewRect);

            alternate = false;
            float num = 0.0f;
            List<Pawn> removed = [];
            foreach (Thing thing in compFarmStorage.GetDirectlyHeldThings())
            {
                Pawn pawn = (Pawn) thing;
                if (DrawLine(num, scrollViewRect.width, pawn))
                {
                    removed.Add(pawn);
                }

                num += LineHeight;
            }

            FarmJob_MapComponent comp = SelThing.Map.GetComponent<FarmJob_MapComponent>();
            foreach (Pawn pawn in removed)
            {
                compFarmStorage.ReleaseSingle(compFarmStorage.parent.Map, pawn);
                if (comp != null && comp.farmAssignments.ContainsKey(pawn))
                {
                    comp.farmAssignments.Remove(pawn);
                }
            }

            Widgets.EndScrollView();
        }
    }
}
