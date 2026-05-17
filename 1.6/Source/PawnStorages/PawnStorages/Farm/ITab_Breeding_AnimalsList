using System;
using System.Collections.Generic;
using PawnStorages.Farm.Comps;
using RimWorld;
using UnityEngine;
using Verse;

//Mirror of Production Tab shows male and female animals allows ejecting each animal

namespace PawnStorages.Farm
{
	// Token: 0x020000B9 RID: 185
	public class ITab_Breeding_Animals : ITab
	{
		// Token: 0x170000DF RID: 223
		// (get) Token: 0x0600044A RID: 1098 RVA: 0x0000496F File Offset: 0x00002B6F
		public CompFarmStorage compFarmStorage
		{
			get
			{
				return base.SelThing.TryGetComp<CompFarmStorage>();
			}
		}

		// Token: 0x0600044B RID: 1099 RVA: 0x0000497C File Offset: 0x00002B7C
		public ITab_Breeding_Animals()
		{
			this.size = ITab_Breeding_Animals.WinSize;
			this.labelKey = "PS_BreedingAnimalsTab";
		}

		// Token: 0x0600044C RID: 1100 RVA: 0x00014158 File Offset: 0x00012358
		public bool DrawLine(float position, float width, Pawn pawn)
		{
			Rect position2 = new Rect(0f, position, width, 60f);
			if (this.alternate)
			{
				Widgets.DrawRectFast(position2, new Color(1f, 1f, 1f, 0.5f), null);
			}
			this.alternate = !this.alternate;
			Widgets.DefIcon(new Rect(5f, position + 7.5f, 45f, 45f), pawn.def, null, 1f, null, true, null, null, null, 1f);
			Rect rect = new Rect(55f, position, width - 90f, 20f);
			Pawn_NeedsTracker needs = pawn.needs;
			bool? flag;
			if (needs == null)
			{
				flag = null;
			}
			else
			{
				Need_Food food = needs.food;
				flag = ((food != null) ? new bool?(food.Starving) : null);
			}
			Widgets.Label(rect, ((flag ?? false) ? "PS_FarmTab_NameStarving" : "PS_FarmTab_Name").Translate(pawn.LabelShort));
			Rect rect2 = new Rect(55f, position + 20f, width - 90f, 20f);
			string key = "PS_FarmTab_Nutrition";
			Pawn_NeedsTracker needs2 = pawn.needs;
			float? num;
			if (needs2 == null)
			{
				num = null;
			}
			else
			{
				Need_Food food2 = needs2.food;
				num = ((food2 != null) ? new float?(food2.CurLevelPercentage) : null);
			}
			Widgets.Label(rect2, key.Translate((num ?? 0f).ToStringPercent()));
			Widgets.Label(new Rect(55f, position + 40f, width - 90f, 20f), pawn.gender.GetLabel(true).CapitalizeFirst());
			return Widgets.ButtonImage(new Rect(new Vector2(width - 50f, position + 15f), new Vector2(30f, 30f)), TexButton.Drop, Color.white, GenUI.MouseoverColor, true, null);
		}

		// Token: 0x0600044D RID: 1101 RVA: 0x00014380 File Offset: 0x00012580
		protected override void FillTab()
		{
			if (this.compFarmStorage == null)
			{
				return;
			}
			if (this.compFarmStorage.GetDirectlyHeldThings().Count <= 0)
			{
				return;
			}
			Widgets.Label(new Rect(5f, 0f, ITab_Breeding_Animals.WinSize.x, 30f), "PS_BreedingAnimalsTab_TopLabel".Translate());
			Rect rect = new Rect(0f, 30f, ITab_Breeding_Animals.WinSize.x, ITab_Breeding_Animals.WinSize.y - 30f).ContractedBy(10f);
			Rect outRect = new Rect(rect);
			float height = (float)this.compFarmStorage.GetDirectlyHeldThings().Count * 60f;
			Rect viewRect = new Rect(0f, 0f, outRect.width, height);
			Widgets.AdjustRectsForScrollView(rect, ref outRect, ref viewRect);
			Widgets.BeginScrollView(outRect, ref this.ThingFilterState.scrollPosition, viewRect, true);
			this.alternate = false;
			float num = 0f;
			List<Pawn> list = new List<Pawn>();
			foreach (Thing thing in ((IEnumerable<Thing>)this.compFarmStorage.GetDirectlyHeldThings()))
			{
				Pawn pawn = (Pawn)thing;
				if (this.DrawLine(num, outRect.width, pawn))
				{
					list.Add(pawn);
				}
				num += 60f;
			}
			FarmJob_MapComponent component = base.SelThing.Map.GetComponent<FarmJob_MapComponent>();
			foreach (Pawn pawn2 in list)
			{
				this.compFarmStorage.ReleaseSingle(this.compFarmStorage.parent.Map, pawn2, true, false);
				if (component != null && component.farmAssignments.ContainsKey(pawn2))
				{
					component.farmAssignments.Remove(pawn2);
				}
			}
			Widgets.EndScrollView();
		}

		// Token: 0x04000258 RID: 600
		private static readonly Vector2 WinSize = new Vector2(300f, 480f);

		// Token: 0x04000259 RID: 601
		public readonly ThingFilterUI.UIState ThingFilterState = new ThingFilterUI.UIState();

		// Token: 0x0400025A RID: 602
		public const float LineHeight = 60f;

		// Token: 0x0400025B RID: 603
		private bool alternate;
	}
}
