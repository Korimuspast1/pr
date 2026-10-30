using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class HudPlanning
	{
		private static readonly string[] StoreRes = new string[4] { "wood", "stone", "common_metals", "gold" };

		private static readonly string[] StoreNames = new string[4] { "wood", "stone", "metal", "gold" };

		internal static Rect BarForTest;

		private static readonly List<KeyValuePair<string, int>> _stored = new List<KeyValuePair<string, int>>();

		private static float _storedAt = -10f;

		private static City _storedFor;

		public static void Draw()
		{
		}

		private static void DrawOrder(City town, Rect bar, float k)
		{
			List<Building> list = Blueprints.SitesOf(town);
			if (list.Count != 0)
			{
				int num = Mathf.Min(list.Count, 6);
				float num2 = Mathf.Round(8f * k);
				float width = Mathf.Round(104f * k);
				float num3 = Mathf.Round(12f * k + (float)num * num2);
				Rect r = new Rect(Mathf.Round(bar.xMax + 6f * k), Mathf.Round(bar.yMax - num3), width, num3);
				GameArt.TipPanel(r);
				float num4 = r.y + 3f * k;
				GameArt.Label(new Rect(r.x + 5f * k, num4, r.width - 10f * k, num2), "Next up (T: build next)", 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
				num4 += num2;
				Building building = null;
				try
				{
					building = town.under_construction_building;
				}
				catch
				{
				}
				for (int i = 0; i < num; i++)
				{
					Building building2 = list[i];
					string text = PlanPieces.OfOrder(building2.asset.group)?.Name ?? building2.asset.id;
					string text2 = Blueprints.StateOf(building2);
					Color color = ((building2 == building) ? GameArt.Orange : Color.white);
					GameArt.Label(new Rect(r.x + 5f * k, num4, r.width - 10f * k, num2), i + 1 + ". " + text + "  " + text2, 4.2f, TextAnchor.MiddleLeft, color);
					num4 += num2;
				}
			}
		}

		public static void DrawBar(float sw, float sh)
		{
			if (!Planning.Begun || Event.current.type != EventType.Repaint)
			{
				return;
			}
			GameArt.Ensure();
			float p = GameArt.P;
			float num = ((Hud.VitalsTop > 1f) ? (Hud.VitalsTop - 6f * p) : (sh - 58f * p));
			if (Planning.Founding)
			{
				string text = ((Planning.Why == null) ? "Your village will grow round its bonfire" : Planning.Why);
				float num2 = Mathf.Max(150f * p, GameArt.Measure(text, 5.5f).x + 24f * p);
				Rect rect = new Rect(Mathf.Round(sw * 0.5f - num2 * 0.5f), Mathf.Round(num - 18f * p), Mathf.Round(num2), Mathf.Round(18f * p));
				GameArt.TipPanel(rect);
				GameArt.Label(rect, text, 5.5f, TextAnchor.MiddleCenter, (Planning.Why == null) ? Color.white : GameArt.Peach);
				BarForTest = rect;
				return;
			}
			City town = Planning.Town;
			bool lightsOnly = Planning.LightsOnly;
			if (town == null && !lightsOnly)
			{
				return;
			}
			List<PlanPieces.Piece> pieces = Planning.Category.Pieces;
			float num3 = Mathf.Round(26f * p);
			float num4 = Mathf.Round(3f * p);
			float num5 = Mathf.Round(2f * p);
			float num6 = (float)Mathf.Max(1, pieces.Count) * (num3 + num4) - num4;
			float num7 = Mathf.Max(num6 + 16f * p, 220f * p);
			float num8 = Mathf.Round(num3 + 34f * p);
			if (!lightsOnly)
			{
				float num9 = 40f * p;
				for (int i = 0; i < PlanPieces.All.Count; i++)
				{
					num9 += GameArt.Measure(PlanPieces.All[i].Name, 4.6f).x + 8f * p;
				}
				num7 = Mathf.Max(num7, num9);
			}
			Rect rect2 = (BarForTest = new Rect(Mathf.Round(sw * 0.5f - num7 * 0.5f), Mathf.Round(num - num8), Mathf.Round(num7), num8));
			GameArt.BarSlab(rect2);
			float num10 = rect2.y + 3f * p;
			float num11 = Mathf.Round(9f * p);
			float num12 = rect2.x + 8f * p;
			if (lightsOnly)
			{
				bool num13 = Village.CanFound(Hub.Host);
				if (num13)
				{
					num12 += GameArt.KeyCap(num12, num10 + num11 * 0.5f, "Q", 4.2f) + 4f * p;
				}
				if (num13)
				{
					float x = GameArt.Measure("Found a village", 4.6f).x;
					GameArt.Label(new Rect(num12, num10, x + 2f, num11), "Found a village", 4.6f, TextAnchor.MiddleLeft, GameArt.Grey);
					num12 += x + 8f * p;
				}
				float x2 = GameArt.Measure(PlanPieces.Lights.Name, 4.6f).x;
				GameArt.Label(new Rect(num12, num10, x2 + 2f, num11), PlanPieces.Lights.Name, 4.6f, TextAnchor.MiddleLeft, GameArt.Orange);
				num12 += x2 + 8f * p;
				if (num13)
				{
					GameArt.KeyCap(num12, num10 + num11 * 0.5f, "E", 4.2f);
				}
			}
			else
			{
				num12 += GameArt.KeyCap(num12, num10 + num11 * 0.5f, "Q", 4.2f) + 4f * p;
				for (int j = 0; j < PlanPieces.All.Count; j++)
				{
					string name = PlanPieces.All[j].Name;
					float x3 = GameArt.Measure(name, 4.6f).x;
					bool flag = j == Planning.CategoryIndex;
					GameArt.Label(new Rect(num12, num10, x3 + 2f, num11), name, 4.6f, TextAnchor.MiddleLeft, flag ? GameArt.Orange : GameArt.Grey);
					num12 += x3 + 8f * p;
				}
				GameArt.KeyCap(num12, num10 + num11 * 0.5f, "E", 4.2f);
			}
			float num14 = Mathf.Round(rect2.center.x - num6 * 0.5f);
			float num15 = rect2.y + 14f * p;
			Color color = GUI.color;
			for (int k = 0; k < pieces.Count; k++)
			{
				PlanPieces.Piece piece = pieces[k];
				bool flag2 = k == Planning.PieceIndex;
				bool flag3 = PlanPieces.WhyNot(piece, town) == null;
				Rect r = new Rect(num14 + (float)k * (num3 + num4), flag2 ? (num15 - num5) : num15, num3, num3);
				GameArt.Sliced(r, GameArt.Well ?? GameArt.Inner, Color.white);
				Sprite sprite = ((piece.Light >= 0) ? SupplyIcons.Light(piece.Light) : PlanGhosts.SpriteOf(PlanPieces.AssetOf(piece, town)));
				if (sprite != null)
				{
					GUI.color = new Color(1f, 1f, 1f, flag3 ? 1f : 0.35f);
					GameArt.IconFit(new Rect(r.x + 3f * p, r.y + 3f * p, r.width - 6f * p, r.height - 6f * p), sprite, Color.white);
					GUI.color = color;
				}
				else if (piece.Wall >= 0)
				{
					Color c = ((piece.Wall == 0) ? new Color(0.52f, 0.36f, 0.2f, flag3 ? 1f : 0.35f) : ((piece.Wall == 1) ? new Color(0.6f, 0.6f, 0.58f, flag3 ? 1f : 0.35f) : new Color(0.32f, 0.33f, 0.37f, flag3 ? 1f : 0.35f)));
					for (int l = 0; l < 3; l++)
					{
						GameArt.Flat(new Rect(r.x + 4f * p + (float)l * (r.width - 8f * p) / 3f + p, r.y + 5f * p, (r.width - 8f * p) / 3f - 2f * p, r.height - 10f * p), c);
					}
				}
				else if (piece.Street)
				{
					GameArt.Flat(new Rect(r.x + 4f * p, r.center.y - 3f * p, r.width - 8f * p, 6f * p), new Color(0.55f, 0.47f, 0.36f, 1f));
					GameArt.Flat(new Rect(r.x + 4f * p, r.center.y - 0.5f * p, r.width - 8f * p, Mathf.Max(1f, p)), new Color(0.75f, 0.68f, 0.55f, 1f));
				}
				if (flag2)
				{
					GameArt.Sliced(r, GameArt.ButtonBorder, new Color(1f, 0.82f, 0.3f, 1f), 4f, 4f, 4f, 4f);
				}
				if (k < 8)
				{
					GameArt.Label(new Rect(r.x + 2f * p, r.y + 1f * p, 8f * p, 6f * p), (k + 1).ToString(), 3.8f, TextAnchor.UpperLeft, GameArt.KeyOrange);
				}
				if (piece.Light >= 0)
				{
					GameArt.Label(new Rect(r.x, r.yMax - 7f * p, r.width - 2f * p, 6f * p), "x" + PlacedLights.Carried(Hub.Host, (PlacedLights.Kind)piece.Light), 3.8f, TextAnchor.LowerRight, flag3 ? Color.white : GameArt.Peach);
				}
			}
			GUI.color = color;
			PlanPieces.Piece piece2 = Planning.Piece;
			if (piece2 != null)
			{
				string text2 = PlanPieces.WhyNot(piece2, town);
				string text3 = piece2.Name + "  " + (text2 ?? PlanPieces.CostLabel(piece2, town));
				GameArt.Label(new Rect(rect2.x, num15 + num3 + 2f * p, rect2.width, 9f * p), text3, 4.8f, TextAnchor.MiddleCenter, (text2 == null) ? Color.white : GameArt.Peach);
				if (piece2.Home)
				{
					GameArt.Label(new Rect(rect2.x, num15 + num3 + 10f * p, rect2.width, 8f * p), "Starts as a tent. Grows a size a day with the village, town and city halls", 3.9f, TextAnchor.MiddleCenter, GameArt.Grey);
				}
				if (piece2.Light >= 0)
				{
					GameArt.Label(new Rect(rect2.x, num15 + num3 + 10f * p, rect2.width, 8f * p), (piece2.Light == 1) ? "Hangs on a house's wall or a town wall. Burns for good" : "Stands anywhere on dry land. Burns for good", 3.9f, TextAnchor.MiddleCenter, GameArt.Grey);
				}
			}
			if (town == null)
			{
				return;
			}
			bool flag4 = Village.IsVillage(town);
			float num16 = Mathf.Round(92f * p);
			float num17 = Mathf.Round(flag4 ? (54f * p) : (46f * p));
			Rect rect3 = new Rect(Mathf.Round(rect2.x - num16 - 6f * p), Mathf.Round(rect2.yMax - num17), num16, num17);
			GameArt.TipPanel(rect3);
			float num18 = rect3.y + 4f * p;
			float num19 = Mathf.Round(8f * p);
			GameArt.Label(new Rect(rect3.x + 5f * p, num18, rect3.width - 10f * p, num19), Rep.NameOf(town), 5f, TextAnchor.MiddleLeft, Color.white);
			num18 += num19;
			GameArt.Label(new Rect(rect3.x + 5f * p, num18, rect3.width - 10f * p, num19), flag4 ? ("Level " + PlanPieces.LevelOf(town)) : "Grows by itself", 4.4f, TextAnchor.MiddleLeft, GameArt.Orange);
			if (flag4)
			{
				Rect r2 = new Rect(rect3.x + 40f * p, num18 + 2.5f * p, rect3.width - 45f * p, 3f * p);
				GameArt.Flat(r2, new Color(0f, 0f, 0f, 0.5f));
				GameArt.Flat(new Rect(r2.x, r2.y, Mathf.Round(r2.width * Growth.Progress(town)), r2.height), GameArt.Orange);
				num18 += num19;
				GameArt.Label(new Rect(rect3.x + 5f * p, num18, rect3.width - 10f * p, num19), "Grows with goods stored and tasks done", 3.6f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
			num18 += num19;
			List<string> list = new List<string>(4);
			for (int m = 0; m < StoreRes.Length; m++)
			{
				int num20 = 0;
				try
				{
					num20 = (town.hasStorages() ? town.getResourcesAmount(StoreRes[m]) : 0);
				}
				catch
				{
				}
				list.Add(num20 + " " + StoreNames[m]);
			}
			GameArt.Label(new Rect(rect3.x + 5f * p, num18, rect3.width - 10f * p, num19 * 2f), "Stores: " + string.Join(", ", list.ToArray()), 4.2f, TextAnchor.UpperLeft, GameArt.Grey, wrap: true);
			num18 += num19 * 2f;
			GameArt.Label(new Rect(rect3.x + 5f * p, num18, rect3.width - 10f * p, num19), "Plans " + Blueprints.CountOf(town) + "/" + 12, 4.2f, TextAnchor.MiddleLeft, GameArt.Grey);
			DrawStoresCard(town, rect3, p);
			DrawOrder(town, rect2, p);
		}

		private static void DrawStoresCard(City town, Rect under, float k)
		{
			if (town != _storedFor || Time.unscaledTime - _storedAt > 0.5f)
			{
				_storedFor = town;
				_storedAt = Time.unscaledTime;
				_stored.Clear();
				try
				{
					if (town.hasStorages())
					{
						foreach (ResourceAsset item in AssetManager.resources.list)
						{
							int resourcesAmount = town.getResourcesAmount(item.id);
							if (resourcesAmount > 0)
							{
								_stored.Add(new KeyValuePair<string, int>(item.id, resourcesAmount));
							}
						}
					}
				}
				catch
				{
				}
				_stored.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> keyValuePair) => keyValuePair.Value.CompareTo(a.Value));
			}
			int b = Mathf.Min(12, (_stored.Count + 2 - 1) / 2);
			float num = Mathf.Round(8f * k);
			float num2 = Mathf.Round(10f * k);
			float num3 = num2 + (float)Mathf.Max(1, b) * num + 4f * k;
			Rect r = new Rect(under.x, Mathf.Round(under.y - num3 - 4f * k), under.width, num3);
			GameArt.TipPanel(r);
			GameArt.Label(new Rect(r.x + 5f * k, r.y + 2f * k, r.width - 10f * k, num2 - 2f * k), "Town stores", 4.6f, TextAnchor.MiddleLeft, Color.white);
			if (_stored.Count == 0)
			{
				GameArt.Label(new Rect(r.x + 5f * k, r.y + num2, r.width - 10f * k, num), town.hasStorages() ? "Empty" : "No stockpile yet", 4f, TextAnchor.MiddleLeft, GameArt.Grey);
				return;
			}
			float num4 = (r.width - 10f * k) / 2f;
			for (int num5 = 0; num5 < _stored.Count && num5 < 24; num5++)
			{
				float num6 = r.x + 5f * k + (float)(num5 % 2) * num4;
				float num7 = r.y + num2 + (float)(num5 / 2) * num;
				Sprite sprite = null;
				try
				{
					sprite = AssetManager.resources.get(_stored[num5].Key)?.getSpriteIcon();
				}
				catch
				{
				}
				if (sprite != null)
				{
					GameArt.Icon(new Rect(num6, num7 + 0.5f * k, num - 1f * k, num - 1f * k), sprite);
				}
				GameArt.Label(new Rect(num6 + num, num7, num4 - num, num), _stored[num5].Value + " " + Work.ResourceName(_stored[num5].Key).ToLowerInvariant(), 3.8f, TextAnchor.MiddleLeft, GameArt.Grey);
			}
		}
	}
}
