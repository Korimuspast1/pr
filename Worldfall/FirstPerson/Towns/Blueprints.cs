using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;
using tools;

namespace FirstPerson.Towns
{
	internal static class Blueprints
	{
		private sealed class Tracked
		{
			public City City;

			public readonly List<Building> Sites = new List<Building>();
		}

		public const string OrderKey = "fp_plan";

		private const string OwnKey = "fp_paid_own";

		private const string PayerKey = "fp_paid_by";

		private const string WorkKey = "fp_work";

		private const string LastKey = "fp_lastp";

		private const string PaidKey = "fp_paid";

		private const string HomeKey = "fp_home";

		private const string ClaimKey = "fp_claims";

		private const string ByKey = "fp_plan_by";

		public const int MaxPlans = 12;

		private const float ClaimReach = 78f;

		private const float BlowWork = 2f;

		private const float YourPace = 4f;

		private const float HandsFactor = 1f / 3f;

		private const float BlowSeconds = 0.55f;

		private static readonly List<Tracked> Towns = new List<Tracked>();

		private static readonly List<long> Ids = new List<long>();

		private static readonly List<WorldTile> Feet = new List<WorldTile>();

		private static float _turn;

		private static float _blowAt;

		private static float _needsToastAt = -9f;

		private static readonly string[] Res = new string[4] { "wood", "stone", "common_metals", "gold" };

		private static readonly int[] Need = new int[4];

		internal static void Reset()
		{
			Towns.Clear();
			_turn = 0f;
		}

		internal static List<WorldTile> Footprint(WorldTile t, BuildingAsset a)
		{
			Feet.Clear();
			if (t == null || a == null)
			{
				return Feet;
			}
			BuildingFundament fundament = a.fundament;
			int num = fundament?.left ?? 0;
			int num2 = fundament?.right ?? 0;
			int num3 = fundament?.top ?? 0;
			int num4 = fundament?.bottom ?? 0;
			for (int i = t.y - num4; i <= t.y + num3; i++)
			{
				for (int j = t.x - num; j <= t.x + num2; j++)
				{
					Feet.Add(World.world.GetTile(j, i));
				}
			}
			return Feet;
		}

		internal static float DistanceTo(Building b, Vector2 you)
		{
			WorldTile worldTile = b?.current_tile;
			if (worldTile == null)
			{
				return 99f;
			}
			BuildingFundament fundament = b.asset.fundament;
			float num = worldTile.x - (fundament?.left ?? 0);
			float num2 = (float)(worldTile.x + (fundament?.right ?? 0)) + 1f;
			float num3 = worldTile.y - (fundament?.bottom ?? 0);
			float num4 = (float)(worldTile.y + (fundament?.top ?? 0)) + 1f;
			float num5 = Mathf.Max(0f, Mathf.Max(num - you.x, you.x - num2));
			float num6 = Mathf.Max(0f, Mathf.Max(num3 - you.y, you.y - num4));
			return Mathf.Sqrt(num5 * num5 + num6 * num6);
		}

		public static string WhyNotHere(City c, BuildingAsset a, WorldTile t, List<TileZone> claims)
		{
			claims?.Clear();
			if (c == null || a == null)
			{
				return "Nothing to place";
			}
			if (t == null || t.zone == null)
			{
				return "Look at the ground";
			}
			try
			{
				foreach (WorldTile item in Footprint(t, a))
				{
					if (item == null || item.zone == null)
					{
						return "Off the edge of the world";
					}
					TileTypeBase type = item.Type;
					if (type == null)
					{
						return "The ground isn't clear";
					}
					if (!a.docks && type.liquid)
					{
						return "Not on water";
					}
					if (a.docks && !type.liquid)
					{
						return "Only on water by the shore";
					}
					if (type.block)
					{
						return "Not on rock";
					}
					if (WallPlans.Pending(c, item, out var _))
					{
						return "A wall is planned here";
					}
					if (a.only_build_tiles && !type.can_build_on)
					{
						return type.rocks ? "Not on hills or rock" : "Not on this ground";
					}
				}
				List<WorldTile> list = new List<WorldTile>(Footprint(t, a));
				WorldTile tile = c.getTile();
				if (!a.docks && tile != null && !t.isSameIsland(tile))
				{
					return "Not on your town's island";
				}
				foreach (WorldTile item2 in list)
				{
					TileZone zone = item2.zone;
					if (zone.city != c)
					{
						if (zone.city != null)
						{
							return "That's " + Rep.NameOf(zone.city) + "'s land";
						}
						if (claims != null && !claims.Contains(zone))
						{
							claims.Add(zone);
						}
					}
				}
				if (claims != null && claims.Count > 0)
				{
					foreach (TileZone claim in claims)
					{
						WorldTile centerTile = claim.centerTile;
						if (tile != null && centerTile != null && new Vector2(centerTile.x - tile.x, centerTile.y - tile.y).magnitude > 78f)
						{
							return "Too far from the town";
						}
					}
					List<TileZone> list2 = new List<TileZone>();
					for (int i = 0; i < claims.Count; i++)
					{
						foreach (TileZone claim2 in claims)
						{
							if (list2.Contains(claim2))
							{
								continue;
							}
							TileZone[] neighbours = claim2.neighbours;
							foreach (TileZone tileZone in neighbours)
							{
								if (tileZone != null && (tileZone.city == c || list2.Contains(tileZone)))
								{
									list2.Add(claim2);
									break;
								}
							}
						}
					}
					if (list2.Count < claims.Count)
					{
						return "Not next to your town's land";
					}
				}
				if (a.docks)
				{
					string text = DockWhyNot(c, a, list);
					if (text != null)
					{
						return text;
					}
				}
				else if (!t.canBuildOn(a) || !World.world.buildings.canBuildFrom(t, a, (City)null, BuildPlacingType.New, pFloraGrowth: false))
				{
					return "Too close to another building";
				}
				if (a.needs_farms_ground)
				{
					foreach (WorldTile item3 in list)
					{
						if (item3.Type == null || !item3.Type.can_be_farm)
						{
							return "Needs farmland";
						}
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("plan check", e);
				return "It can't stand here";
			}
			return null;
		}

		private static string DockWhyNot(City c, BuildingAsset a, List<WorldTile> feet)
		{
			TileIsland tileIsland = c.getTile()?.region?.island;
			OceanHelper.clearOceanPools();
			bool flag = false;
			bool flag2 = false;
			int num = int.MaxValue;
			int num2 = int.MinValue;
			int num3 = int.MaxValue;
			int num4 = int.MinValue;
			foreach (WorldTile foot in feet)
			{
				if (foot.Type.ocean && OceanHelper.goodForNewDock(foot))
				{
					flag = true;
				}
				WorldTile[] neighbours = foot.neighbours;
				foreach (WorldTile worldTile in neighbours)
				{
					if (worldTile != null && worldTile.Type != null && worldTile.Type.ground && worldTile.region != null && worldTile.region.island == tileIsland)
					{
						flag2 = true;
					}
				}
				num = Math.Min(num, foot.x);
				num2 = Math.Max(num2, foot.x);
				num3 = Math.Min(num3, foot.y);
				num4 = Math.Max(num4, foot.y);
			}
			if (!flag)
			{
				return "Needs the open sea";
			}
			if (!flag2)
			{
				return "Must touch your town's shore";
			}
			foreach (WorldTile foot2 in feet)
			{
				if (!foot2.canBuildOn(a))
				{
					return "Something stands here";
				}
			}
			if (a.check_for_close_building)
			{
				foreach (WorldTile foot3 in feet)
				{
					BuildingManager buildings = World.world.buildings;
					if ((foot3.x == num && buildings.isBuildingNearby(foot3.tile_left)) || (foot3.x == num2 && buildings.isBuildingNearby(foot3.tile_right)) || (foot3.y == num3 && (buildings.isBuildingNearby(foot3.tile_down) || (foot3.has_tile_down && buildings.isBuildingNearby(foot3.tile_down.tile_down)))) || (foot3.y == num4 && (buildings.isBuildingNearby(foot3.tile_up) || (foot3.has_tile_up && buildings.isBuildingNearby(foot3.tile_up.tile_up)))))
					{
						return "Too close to another building";
					}
				}
			}
			return null;
		}

		public static WorldTile NearestFree(City c, BuildingAsset a)
		{
			WorldTile worldTile = c?.getTile();
			if (worldTile == null || a == null)
			{
				return null;
			}
			List<TileZone> list = new List<TileZone>();
			for (int i = 3; i <= 30; i++)
			{
				for (int j = 0; j < 8 * i; j++)
				{
					float f = (float)j * MathF.PI * 2f / (float)(8 * i);
					WorldTile tile = World.world.GetTile(worldTile.x + Mathf.RoundToInt(Mathf.Cos(f) * (float)i), worldTile.y + Mathf.RoundToInt(Mathf.Sin(f) * (float)i));
					if (tile != null && tile.zone != null && tile.zone.city == c && WhyNotHere(c, a, tile, list) == null && list.Count == 0)
					{
						return tile;
					}
				}
			}
			return null;
		}

		public static Building PlaceSite(City c, BuildingAsset a, WorldTile t, Actor by, bool home)
		{
			if (c == null || a == null || t == null)
			{
				return null;
			}
			List<TileZone> list = new List<TileZone>();
			string text = WhyNotHere(c, a, t, list);
			if (text != null)
			{
				Log.Info($"plans: can't place {a.id} at {t.x},{t.y}: {text}");
				return null;
			}
			bool flag = Village.PlansAlone(c);
			if (!flag && !CanPay(c, a.cost, by, out var missing))
			{
				Log.Info("plans: " + a.id + " needs " + missing);
				return null;
			}
			foreach (TileZone item in list)
			{
				try
				{
					c.addZone(item);
				}
				catch (Exception e)
				{
					Hub.Fail("plan claim", e);
				}
			}
			Building building = null;
			try
			{
				building = World.world.buildings.addBuilding(a, t, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building != null)
				{
					building.setKingdom(c.kingdom);
					c.listBuilding(building);
					building.setUnderConstruction();
				}
			}
			catch (Exception e2)
			{
				Hub.Fail("plan place", e2);
			}
			if (building == null)
			{
				foreach (TileZone item2 in list)
				{
					Unclaim(c, item2, null);
				}
				return null;
			}
			try
			{
				if (by != null)
				{
					building.data.set("fp_plan_by", by.data.id);
				}
				if (home && by != null)
				{
					building.data.set("fp_home", by.data.id);
				}
				if (list.Count > 0)
				{
					List<string> list2 = new List<string>();
					foreach (TileZone item3 in list)
					{
						if (item3.centerTile != null)
						{
							list2.Add(item3.centerTile.x + ":" + item3.centerTile.y);
						}
					}
					building.data.set("fp_claims", string.Join(";", list2.ToArray()));
				}
			}
			catch
			{
			}
			if (IsFree(a) || !flag)
			{
				if (!flag)
				{
					Pay(c, building, by, out var _);
				}
				else
				{
					MarkPaid(building);
				}
			}
			if (!building.isUnderConstruction())
			{
				Finished(c, building);
				return building;
			}
			AddToOrder(c, building);
			if (!flag && !YoursUnderWay(c))
			{
				c.under_construction_building = building;
			}
			Log.Info(string.Format("plans: {0} planned at {1},{2} in {3}{4}{5}{6}", a.id, t.x, t.y, Rep.NameOf(c), (list.Count > 0) ? $", claiming {list.Count} zone(s)" : "", home ? " (your home)" : "", IsPaid(building) ? ", paid" : ""));
			return building;
		}

		private static bool IsFree(BuildingAsset a)
		{
			ConstructionCost constructionCost = a?.cost;
			if (constructionCost != null)
			{
				if (constructionCost.wood <= 0 && constructionCost.stone <= 0 && constructionCost.common_metals <= 0)
				{
					return constructionCost.gold <= 0;
				}
				return false;
			}
			return true;
		}

		private static void Needs(ConstructionCost cost)
		{
			Need[0] = cost?.wood ?? 0;
			Need[1] = cost?.stone ?? 0;
			Need[2] = cost?.common_metals ?? 0;
			Need[3] = cost?.gold ?? 0;
		}

		private static int Stored(City c, string res)
		{
			try
			{
				return (c != null && c.hasStorages()) ? c.getResourcesAmount(res) : 0;
			}
			catch
			{
				return 0;
			}
		}

		private static int Carried(Actor payer, string res)
		{
			if (payer == null)
			{
				return 0;
			}
			if (!(res == "gold"))
			{
				return Civil.BagCount(payer, res);
			}
			return Economy.Money(payer);
		}

		public static bool CanPay(City c, ConstructionCost cost, Actor payer, out string missing)
		{
			Needs(cost);
			List<string> list = new List<string>(4);
			for (int i = 0; i < 4; i++)
			{
				int num = Stored(c, Res[i]) + Carried(payer, Res[i]);
				if (num < Need[i])
				{
					list.Add(Need[i] - num + " " + ((i == 2) ? "metal" : Res[i]));
				}
			}
			missing = string.Join(", ", list.ToArray());
			return list.Count == 0;
		}

		public static bool Pay(City c, Building b, Actor payer, out string missing)
		{
			missing = null;
			if (b == null)
			{
				return false;
			}
			if (IsPaid(b))
			{
				return true;
			}
			if (Village.PlansAlone(c))
			{
				return PayHauled(b, payer, out missing);
			}
			if (!CanPay(c, b.asset.cost, payer, out missing))
			{
				return false;
			}
			Needs(b.asset.cost);
			int[] array = new int[4];
			for (int i = 0; i < 4; i++)
			{
				int num = Need[i];
				if (num <= 0)
				{
					continue;
				}
				int num2 = Mathf.Min(num, Stored(c, Res[i]));
				try
				{
					if (num2 > 0)
					{
						c.takeResource(Res[i], num2);
					}
				}
				catch (Exception e)
				{
					Hub.Fail("plan pay", e);
					num2 = 0;
				}
				int num3 = num - num2;
				if (num3 <= 0 || payer == null)
				{
					continue;
				}
				try
				{
					if (Res[i] == "gold")
					{
						payer.spendMoney(num3);
					}
					else
					{
						Civil.TakeFromBag(payer, Res[i], num3);
					}
					array[i] = num3;
				}
				catch (Exception e2)
				{
					Hub.Fail("plan pay", e2);
				}
			}
			if (payer != null && (array[0] | array[1] | array[2] | array[3]) != 0)
			{
				try
				{
					b.data.set("fp_paid_own", string.Join(",", Array.ConvertAll(array, (int x) => x.ToString())));
					b.data.set("fp_paid_by", payer.data.id);
				}
				catch
				{
				}
			}
			MarkPaid(b);
			Log.Info("plans: " + b.asset.id + " paid (" + PlanPieces.CostText(b.asset) + ")" + ((payer != null) ? (" by " + WorldBoxMod.Describe(payer)) : " from the stockpile"));
			return true;
		}

		private static bool PayHauled(Building b, Actor payer, out string missing)
		{
			Needs(b.asset.cost);
			int[] array = Haul.Have(b);
			City city = null;
			try
			{
				city = b.city;
			}
			catch
			{
			}
			bool flag = payer != null || Haul.NoHands(city);
			List<string> list = new List<string>(4);
			for (int i = 0; i < 4; i++)
			{
				int num = Need[i] - array[i] - Carried(payer, Res[i]) - (flag ? Stored(city, Res[i]) : 0);
				if (num > 0)
				{
					list.Add(num + " " + ((i == 2) ? "metal" : Res[i]));
				}
			}
			missing = string.Join(", ", list.ToArray());
			if (list.Count > 0)
			{
				return false;
			}
			for (int j = 0; j < 4; j++)
			{
				int num2 = Need[j] - array[j];
				if (num2 <= 0)
				{
					continue;
				}
				int num3 = ((payer != null) ? Mathf.Min(num2, Carried(payer, Res[j])) : 0);
				try
				{
					if (num3 > 0)
					{
						if (Res[j] == "gold")
						{
							payer.spendMoney(num3);
						}
						else
						{
							Civil.TakeFromBag(payer, Res[j], num3);
						}
						AddOwn(b, j, num3, payer);
					}
					int num4 = num2 - num3;
					if (num4 > 0 && flag)
					{
						city.takeResource(Res[j], num4);
					}
				}
				catch (Exception e)
				{
					Hub.Fail("plan pay", e);
				}
			}
			Haul.Clear(b);
			MarkPaid(b);
			Log.Info("plans: " + b.asset.id + " stocked and paid" + ((payer != null) ? (" (the rest from " + WorldBoxMod.Describe(payer) + "'s bag and the stores)") : (flag ? " (the rest from the stockpile)" : "")));
			return true;
		}

		public static string CostAgainst(Building b, Actor you)
		{
			if (b?.asset == null)
			{
				return null;
			}
			Needs(b.asset.cost);
			City city = b.city;
			int[] array = (Village.PlansAlone(city) ? Haul.Have(b) : new int[4]);
			List<string> list = new List<string>(4);
			for (int i = 0; i < 4; i++)
			{
				if (Need[i] > 0)
				{
					int num = Mathf.Min(Need[i], array[i] + Carried(you, Res[i]) + Stored(city, Res[i]));
					list.Add(((i == 2) ? "metal" : Res[i]) + " " + num + "/" + Need[i]);
				}
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray());
			}
			return null;
		}

		private static string Upper(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpperInvariant(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static string WhereFrom(Building b, Actor you)
		{
			Needs(b.asset.cost);
			City city = b.city;
			int[] array = (Village.PlansAlone(city) ? Haul.Have(b) : new int[4]);
			for (int i = 0; i < 4; i++)
			{
				if (Need[i] > 0)
				{
					int num = Carried(you, Res[i]);
					int num2 = Stored(city, Res[i]);
					if (array[i] + num + num2 < Need[i])
					{
						string text = ((i == 2) ? "metal" : Res[i]);
						return string.Format("{0}: {1} {2}, the stores {3}", text, (i == 3) ? "your purse" : "your bag", num, num2) + ((array[i] > 0) ? $", here {array[i]}" : "");
					}
				}
			}
			return null;
		}

		internal static void CheckStocked(Building b)
		{
			if (b != null && !IsPaid(b) && Village.PlansAlone(b.city) && Haul.Stocked(b))
			{
				Pay(b.city, b, null, out var _);
			}
		}

		internal static void AddOwn(Building b, int res, int n, Actor payer)
		{
			if (b == null || payer == null || n <= 0)
			{
				return;
			}
			int[] array = new int[4];
			try
			{
				b.data.get("fp_paid_own", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array2 = pResult.Split(',');
					for (int i = 0; i < 4 && i < array2.Length; i++)
					{
						int.TryParse(array2[i], out array[i]);
					}
				}
				array[res] += n;
				b.data.set("fp_paid_own", string.Join(",", Array.ConvertAll(array, (int x) => x.ToString())));
				b.data.set("fp_paid_by", payer.data.id);
			}
			catch
			{
			}
		}

		private static void MarkPaid(Building b)
		{
			try
			{
				b.data.set("fp_paid", pData: true);
			}
			catch
			{
			}
		}

		public static bool IsPaid(Building b)
		{
			if (b == null)
			{
				return false;
			}
			try
			{
				b.data.get("fp_paid", out var pResult, pDefault: false);
				return pResult;
			}
			catch
			{
				return false;
			}
		}

		private static void GiveBack(City c, Building b, float share, Actor to)
		{
			Needs(b.asset.cost);
			int[] array = new int[4];
			Actor actor = null;
			try
			{
				b.data.get("fp_paid_own", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array2 = pResult.Split(',');
					for (int i = 0; i < 4 && i < array2.Length; i++)
					{
						int.TryParse(array2[i], out array[i]);
					}
					b.data.get("fp_paid_by", out var pResult2, 0L);
					actor = Village.ActorById(pResult2);
				}
			}
			catch
			{
			}
			bool flag = Economy.HasStores(c);
			for (int j = 0; j < 4; j++)
			{
				int num = ((actor != null) ? Mathf.FloorToInt((float)Mathf.Min(array[j], Need[j]) * share) : 0);
				int num2 = Mathf.FloorToInt((float)Need[j] * share) - num;
				try
				{
					if (num > 0)
					{
						Back(actor, Res[j], num);
					}
					if (num2 > 0)
					{
						if (flag)
						{
							Growth.Ignore(c, Res[j], num2);
							c.addResourcesToRandomStockpile(Res[j], num2);
						}
						else if (to != null)
						{
							Back(to, Res[j], num2);
						}
					}
				}
				catch (Exception e)
				{
					Hub.Fail("plan refund", e);
				}
			}
		}

		private static int[] OwnOf(Building b)
		{
			int[] array = new int[4];
			try
			{
				b.data.get("fp_paid_own", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array2 = pResult.Split(',');
					for (int i = 0; i < 4 && i < array2.Length; i++)
					{
						int.TryParse(array2[i], out array[i]);
					}
				}
			}
			catch
			{
			}
			return array;
		}

		private static void GiveBackOwn(Building b)
		{
			try
			{
				b.data.get("fp_paid_own", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return;
				}
				b.data.get("fp_paid_by", out var pResult2, 0L);
				Actor actor = Village.ActorById(pResult2);
				if (actor == null)
				{
					return;
				}
				string[] array = pResult.Split(',');
				for (int i = 0; i < 4 && i < array.Length; i++)
				{
					if (int.TryParse(array[i], out var result) && result > 0)
					{
						Back(actor, Res[i], result);
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("plan refund", e);
			}
		}

		private static void Back(Actor a, string res, int n)
		{
			if (res == "gold")
			{
				a.addMoney(n);
			}
			else
			{
				Bag.Put(a, res, n, spill: true, Hub.Toast);
			}
		}

		internal static bool TownsOwn(Building b)
		{
			City city = b?.city;
			if (city == null)
			{
				return false;
			}
			ReadOrder(city, Ids);
			return !Ids.Contains(b.data.id);
		}

		public static bool Refunds(Building b)
		{
			if (b != null && !IsFree(b.asset))
			{
				if (!IsPaid(b))
				{
					return TownsOwn(b);
				}
				return true;
			}
			return false;
		}

		public static string KnockPrompt(Building b)
		{
			int num = 0;
			try
			{
				if (b.asset.type == "type_house")
				{
					num = b.countResidents();
				}
			}
			catch
			{
			}
			return ((num == 1) ? "1 person lives here. " : ((num > 1) ? (num + " people live here and will look for other beds. ") : "")) + "Right-click again to knock it down (half its cost back)";
		}

		public static bool Remove(Building b, Actor by)
		{
			if (b == null || !b.isAlive() || !b.isUnderConstruction())
			{
				return false;
			}
			City city = b.city;
			bool flag = Refunds(b);
			string id = b.asset.id;
			if (flag)
			{
				GiveBack(city, b, 1f, by);
			}
			else
			{
				Haul.Return(city, b, OwnOf(b));
				GiveBackOwn(b);
			}
			string pResult = null;
			try
			{
				b.data.get("fp_claims", out pResult, null);
			}
			catch
			{
			}
			RemoveFromOrder(city, b);
			try
			{
				if (city != null && city.under_construction_building == b)
				{
					city.under_construction_building = null;
				}
			}
			catch
			{
			}
			try
			{
				b.removeBuildingFinal();
			}
			catch (Exception e)
			{
				Hub.Fail("plan remove", e);
			}
			if (city != null && !string.IsNullOrEmpty(pResult))
			{
				string[] array = pResult.Split(';');
				for (int i = 0; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(':');
					if (array2.Length == 2 && int.TryParse(array2[0], out var result) && int.TryParse(array2[1], out var result2))
					{
						Unclaim(city, World.world.GetTile(result, result2)?.zone, b);
					}
				}
			}
			Log.Info("plans: " + id + " removed in " + Rep.NameOf(city) + (flag ? ", its cost given back" : ""));
			return true;
		}

		public static string WhyNotKnockDown(Building b)
		{
			if (b == null || !b.isAlive() || b.isRuin())
			{
				return "Nothing to knock down";
			}
			if (!b.asset.can_be_demolished)
			{
				return "That can't be knocked down";
			}
			if (b.asset.type == "type_hall")
			{
				return "Not the hall";
			}
			City city = b.city;
			try
			{
				if (b.asset.type == "type_bonfire" && city != null && city.countBuildingsType("type_bonfire", pCountOnlyFinished: false) <= 1)
				{
					return "Not the only bonfire";
				}
			}
			catch
			{
			}
			return null;
		}

		public static bool KnockDown(Building b, Actor by)
		{
			if (WhyNotKnockDown(b) != null)
			{
				return false;
			}
			City city = b.city;
			string id = b.asset.id;
			GiveBack(city, b, 0.5f, by);
			try
			{
				b.startDestroyBuilding();
			}
			catch (Exception e)
			{
				Hub.Fail("knock down", e);
				return false;
			}
			Log.Info("plans: " + id + " knocked down in " + Rep.NameOf(city) + ", half its cost given back");
			return true;
		}

		private static void Unclaim(City c, TileZone z, Building except)
		{
			if (c == null || z == null || z.city != c || c.zones.Count <= 1)
			{
				return;
			}
			try
			{
				foreach (Building building in c.buildings)
				{
					if (building != null && building != except && building.isAlive() && building.current_tile != null && building.current_tile.zone == z)
					{
						return;
					}
				}
				c.removeZone(z);
			}
			catch (Exception e)
			{
				Hub.Fail("plan unclaim", e);
			}
		}

		private static void ReadOrder(City c, List<long> into)
		{
			into.Clear();
			string pResult = null;
			try
			{
				c.data.get("fp_plan", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return;
			}
			string[] array = pResult.Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				if (long.TryParse(array[i], out var result))
				{
					into.Add(result);
				}
			}
		}

		private static void WriteOrder(City c, List<long> ids)
		{
			try
			{
				if (ids.Count == 0)
				{
					c.data.removeString("fp_plan");
					return;
				}
				c.data.set("fp_plan", string.Join(",", ids.ConvertAll((long x) => x.ToString()).ToArray()));
			}
			catch
			{
			}
		}

		private static void AddToOrder(City c, Building b)
		{
			ReadOrder(c, Ids);
			if (!Ids.Contains(b.data.id))
			{
				Ids.Add(b.data.id);
			}
			WriteOrder(c, Ids);
			_turn = 0f;
		}

		private static void RemoveFromOrder(City c, Building b)
		{
			if (c != null && b != null)
			{
				ReadOrder(c, Ids);
				if (Ids.Remove(b.data.id))
				{
					WriteOrder(c, Ids);
				}
			}
		}

		public static bool BuildNext(Building b)
		{
			City city = b?.city;
			if (city == null || !b.isAlive() || !b.isUnderConstruction())
			{
				return false;
			}
			ReadOrder(city, Ids);
			if (!Ids.Remove(b.data.id))
			{
				return false;
			}
			Ids.Insert(0, b.data.id);
			WriteOrder(city, Ids);
			try
			{
				Building under_construction_building = city.under_construction_building;
				if (under_construction_building != b && under_construction_building != null && (Village.PlansAlone(city) || YoursUnderWay(city)))
				{
					city.under_construction_building = null;
				}
			}
			catch
			{
			}
			_turn = 0f;
			Log.Info("plans: " + b.asset.id + " is next in " + Rep.NameOf(city));
			return true;
		}

		public static void Shift(Building b, int by)
		{
			City city = b?.city;
			if (city == null)
			{
				return;
			}
			ReadOrder(city, Ids);
			int num = Ids.IndexOf(b.data.id);
			if (num >= 0)
			{
				int num2 = Mathf.Clamp(num + by, 0, Ids.Count - 1);
				if (num2 != num)
				{
					Ids.RemoveAt(num);
					Ids.Insert(num2, b.data.id);
					WriteOrder(city, Ids);
					_turn = 0f;
				}
			}
		}

		public static List<Building> SitesOf(City c)
		{
			List<Building> list = new List<Building>();
			if (c == null)
			{
				return list;
			}
			ReadOrder(c, Ids);
			foreach (long id in Ids)
			{
				Building building = null;
				try
				{
					building = World.world.buildings.get(id);
				}
				catch
				{
				}
				if (building != null && building.isAlive() && building.data.id == id && building.isUnderConstruction())
				{
					list.Add(building);
				}
			}
			return list;
		}

		public static int CountOf(City c)
		{
			return SitesOf(c).Count;
		}

		public static void Tick(float dt)
		{
			WallPlans.Tick(dt);
			if ((_turn -= dt) <= 0f)
			{
				_turn = 0.5f;
				BuildHelp.Tick();
				Refresh();
				foreach (Tracked town in Towns)
				{
					try
					{
						Turn(town);
					}
					catch (Exception e)
					{
						Hub.Fail("plan turn", e);
					}
				}
			}
			foreach (Tracked town2 in Towns)
			{
				if (!Village.PlansAlone(town2.City))
				{
					continue;
				}
				foreach (Building site in town2.Sites)
				{
					try
					{
						Sync(site);
					}
					catch (Exception e2)
					{
						Hub.Fail("plan work", e2);
					}
				}
			}
		}

		private static void Refresh()
		{
			Towns.Clear();
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item == null || !item.isAlive())
					{
						continue;
					}
					item.data.get("fp_plan", out var pResult, null);
					if (string.IsNullOrEmpty(pResult))
					{
						continue;
					}
					Tracked tracked = new Tracked
					{
						City = item
					};
					ReadOrder(item, Ids);
					bool flag = false;
					for (int num = Ids.Count - 1; num >= 0; num--)
					{
						Building building = null;
						try
						{
							building = World.world.buildings.get(Ids[num]);
						}
						catch
						{
						}
						if (building == null || !building.isAlive() || building.data.id != Ids[num] || building.isRuin())
						{
							Ids.RemoveAt(num);
							flag = true;
						}
						else if (!building.isUnderConstruction())
						{
							Ids.RemoveAt(num);
							flag = true;
							Finished(item, building);
						}
						else
						{
							tracked.Sites.Insert(0, building);
						}
					}
					if (flag)
					{
						WriteOrder(item, Ids);
					}
					if (tracked.Sites.Count > 0)
					{
						Towns.Add(tracked);
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("plan refresh", e);
			}
		}

		private static bool YoursUnderWay(City c)
		{
			Building building = c?.under_construction_building;
			if (building == null || !building.isAlive() || !building.isUnderConstruction())
			{
				return false;
			}
			ReadOrder(c, Ids);
			return Ids.Contains(building.data.id);
		}

		private static void Turn(Tracked t)
		{
			City city = t.City;
			if (BuildHelp.YourSite != null && BuildHelp.YourSite.city == city)
			{
				return;
			}
			bool flag = Village.PlansAlone(city);
			if (flag ? city.hasBuildingToBuild() : YoursUnderWay(city))
			{
				return;
			}
			foreach (Building site in t.Sites)
			{
				if (!site.isAlive() || !site.isUnderConstruction())
				{
					continue;
				}
				string missing;
				if (!flag && !IsPaid(site))
				{
					Haul.Return(city, site);
					if (!Pay(city, site, null, out missing))
					{
						continue;
					}
				}
				if (IsPaid(site) || Pay(city, site, null, out missing))
				{
					city.under_construction_building = site;
					Log.Info("plans: " + Rep.NameOf(city) + "'s builders take the " + site.asset.id);
					return;
				}
			}
			if (!flag)
			{
				return;
			}
			foreach (Building building in city.buildings)
			{
				if (building != null && building.isAlive() && building.isUnderConstruction() && !t.Sites.Contains(building))
				{
					city.under_construction_building = building;
					break;
				}
			}
		}

		private static void Sync(Building b)
		{
			if (b.isAlive() && b.isUnderConstruction())
			{
				int constructionProgress = b.getConstructionProgress();
				b.data.get("fp_lastp", out var pResult, 0);
				if (constructionProgress != pResult)
				{
					b.data.get("fp_work", out var pResult2, 0f);
					pResult2 = ((constructionProgress <= pResult) ? ((float)constructionProgress * 2f) : (pResult2 + (float)(constructionProgress - pResult) * 2f));
					int pData = Mathf.FloorToInt(pResult2 / 2f);
					b.data.set("construction_progress", pData);
					b.data.set("fp_work", pResult2);
					b.data.set("fp_lastp", pData);
				}
			}
		}

		public static string StateOf(Building b)
		{
			if (b == null)
			{
				return "";
			}
			if (!IsPaid(b) && Village.PlansAlone(b.city))
			{
				return Haul.Brought(b);
			}
			if (!IsPaid(b) && !CanPay(b.city, b.asset.cost, null, out var missing))
			{
				return "needs " + missing;
			}
			float num = ProgressOf(b);
			if (!(num <= 0f))
			{
				return Mathf.RoundToInt(num * 100f) + "%";
			}
			if (!IsPaid(b))
			{
				return "waiting";
			}
			return "ready";
		}

		public static float ProgressOf(Building b)
		{
			if (b == null)
			{
				return 0f;
			}
			try
			{
				int num = Mathf.Max(1, b.asset.construction_progress_needed);
				if (!b.isUnderConstruction())
				{
					return 1f;
				}
				if (!Village.PlansAlone(b.city))
				{
					return Mathf.Clamp01((float)b.getConstructionProgress() / (float)num);
				}
				b.data.get("fp_work", out var pResult, 0f);
				return Mathf.Clamp01(pResult / (2f * (float)num));
			}
			catch
			{
				return 0f;
			}
		}

		internal static float WorkOn(Building b)
		{
			try
			{
				b.data.get("fp_work", out var pResult, 0f);
				return pResult;
			}
			catch
			{
				return 0f;
			}
		}

		public static void Blow(Actor you, Building b, float factor)
		{
			if (b == null || !b.isAlive() || !b.isUnderConstruction())
			{
				return;
			}
			Mathf.Max(1, b.asset.construction_progress_needed);
			if (!Village.PlansAlone(b.city))
			{
				b.updateBuild(Mathf.Max(1, Mathf.RoundToInt(8f * factor)));
				if (!b.isUnderConstruction())
				{
					RemoveFromOrder(b.city, b);
					Finished(b.city, b);
				}
			}
			else if (AddWork(b, 8f * factor))
			{
				RemoveFromOrder(b.city, b);
				Finished(b.city, b);
			}
		}

		public static bool BuilderBlow(Building b, int speed)
		{
			if (b == null || !b.isAlive() || !b.isUnderConstruction())
			{
				return false;
			}
			return AddWork(b, speed);
		}

		private static bool AddWork(Building b, float amount)
		{
			int num = Mathf.Max(1, b.asset.construction_progress_needed);
			Sync(b);
			b.data.get("fp_work", out var pResult, 0f);
			pResult += amount;
			b.data.set("fp_work", pResult);
			int num2 = Mathf.FloorToInt(pResult / 2f);
			if (num2 >= num)
			{
				b.data.set("construction_progress", num);
				b.data.set("fp_lastp", num);
				b.updateBuild(1);
				return !b.isUnderConstruction();
			}
			b.data.set("construction_progress", num2);
			b.data.set("fp_lastp", num2);
			try
			{
				b.startShake(0.3f);
			}
			catch
			{
			}
			return false;
		}

		private static void Finished(City c, Building b)
		{
			long pResult = 0L;
			try
			{
				b.data.get("fp_home", out pResult, 0L);
			}
			catch
			{
			}
			Log.Info("plans: " + b.asset.id + " stands in " + Rep.NameOf(c));
			if (pResult == 0L)
			{
				return;
			}
			try
			{
				b.data.removeLong("fp_home");
			}
			catch
			{
			}
			Actor actor = Village.ActorById(pResult);
			if (actor != null)
			{
				Homes.SetOwner(b, actor);
				HomeLife.MoveIn(actor, b);
				string text = "Your house stands" + ((c != null) ? (" in " + Rep.NameOf(c)) : "");
				if (!WorldSync.ToastFor(actor, text, cheer: true))
				{
					Hub.Toast?.Invoke(text);
				}
				Log.Info("plans: " + WorldBoxMod.Describe(actor) + "'s house stands (" + b.asset.id + ")");
			}
		}

		public static bool HomePlanned(City c, Actor you)
		{
			if (c == null || you == null)
			{
				return false;
			}
			foreach (Building item in SitesOf(c))
			{
				try
				{
					item.data.get("fp_home", out var pResult, 0L);
					if (pResult == you.data.id)
					{
						return true;
					}
				}
				catch
				{
				}
			}
			return false;
		}

		public static Building SiteUnderAim(Actor host, Vector2 you)
		{
			if (Towns.Count == 0)
			{
				return null;
			}
			try
			{
				if (!FirstPersonAim.Ready(host) || !FirstPersonAim.Point(out var point, out var _))
				{
					return null;
				}
				if ((new Vector2(point.X, point.Y) - you).magnitude > 5.8f)
				{
					return null;
				}
				Building building = World.world.GetTile(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y))?.building;
				if (building == null || !building.isAlive() || !building.isUnderConstruction() || building.city == null)
				{
					return null;
				}
				return Rep.Rules(host, building.city) ? building : null;
			}
			catch
			{
				return null;
			}
		}

		internal static float ToolFactor(string tool)
		{
			if (tool == null)
			{
				return 0f;
			}
			if (tool == "hands")
			{
				return 1f / 3f;
			}
			if (!tool.Contains("hammer"))
			{
				return 0f;
			}
			if (tool.Contains("wood"))
			{
				return 1f;
			}
			if (tool.Contains("stone"))
			{
				return 1.15f;
			}
			if (tool.Contains("copper") || tool.Contains("bronze"))
			{
				return 1.3f;
			}
			if (tool.Contains("silver"))
			{
				return 1.4f;
			}
			return 1.5f;
		}

		public static bool SiteWork(Work w, Actor host, Building b, Vector2 you, bool mouseFree, bool holstered, SceneCollector scene, CombatFeedback feedback, Action<string> toast)
		{
			City city;
			try
			{
				if (b == null || !b.isUnderConstruction())
				{
					return false;
				}
				city = b.city;
				if (city == null || !Rep.Rules(host, city))
				{
					return false;
				}
				if (DistanceTo(b, you) > 2.3f)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			string text = Work.ToolInHand(host, holstered);
			if (text == null)
			{
				return false;
			}
			float num = ToolFactor(text);
			if (num <= 0f)
			{
				w.Needs = "Build with a hammer or bare hands|Hold Q:Put it away";
				return true;
			}
			bool flag = IsPaid(b) || TownsOwn(b);
			w.Target = b;
			w.Verb = (flag ? "Build" : ("Build (" + (CostAgainst(b, host) ?? PlanPieces.CostText(b.asset)) + ")"));
			w.Progress = ProgressOf(b);
			if (!mouseFree || (!w.ForceSwing && !PlayInput.GetMouseButton(0)))
			{
				return true;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _blowAt < 0.55f)
			{
				return true;
			}
			_blowAt = unscaledTime;
			if (WorldSync.ForwardSiteBlow(host, b, num))
			{
				feedback.WorkSwing(Swing.Mine);
				scene.Shake(b);
				scene.Chips(b, you, 0.6f, 3);
				WorldTile current_tile = b.current_tile;
				if (current_tile != null)
				{
					Sounds.PlayAt("event:/SFX/HIT/HitWood", new Vector2(current_tile.posV3.x, current_tile.posV3.y));
				}
				return true;
			}
			if (!flag && Village.PlansAlone(city))
			{
				Haul.Give(host, b);
				flag = IsPaid(b);
			}
			if (!flag && !Pay(city, b, host, out var missing))
			{
				if (unscaledTime - _needsToastAt > 2.5f)
				{
					_needsToastAt = unscaledTime;
					string text2 = WhereFrom(b, host);
					toast?.Invoke("Needs " + missing + " more" + ((text2 != null) ? (". " + Upper(text2)) : ", in the stores or your bag"));
				}
				return true;
			}
			feedback.WorkSwing(Swing.Mine);
			scene.Shake(b);
			scene.Chips(b, you, 0.6f, 3);
			WorldTile current_tile2 = b.current_tile;
			if (current_tile2 != null)
			{
				Sounds.PlayAt("event:/SFX/HIT/HitWood", new Vector2(current_tile2.posV3.x, current_tile2.posV3.y));
			}
			Blow(host, b, num);
			BuildHelp.Worked(host, b);
			w.Progress = ProgressOf(b);
			return true;
		}
	}
}
