using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class HomeGrowth
	{
		private static readonly string[] Sizes = new string[6] { "Cottage", "Small house", "House", "Good house", "Large house", "Manor" };

		private static readonly string[] Halls = new string[3] { "a village hall", "a town hall", "a city hall" };

		public static string GrowNow(Building b)
		{
			List<KeyValuePair<string, bool>> list = new List<KeyValuePair<string, bool>>();
			if (!Next(b, out var _, list))
			{
				return "As big as a home gets";
			}
			foreach (KeyValuePair<string, bool> item in list)
			{
				if (!item.Value)
				{
					return "Needs " + Lower(item.Key);
				}
			}
			string id = b.asset.id;
			bool flag = false;
			try
			{
				flag = CityBehBuild.upgradeBuilding(b, b.city);
			}
			catch (Exception e)
			{
				Hub.Fail("home grow", e);
			}
			if (!flag)
			{
				return "The town can't build it right now";
			}
			Log.Info("home: " + id + " grown to " + b.asset.id + " by its owner");
			return null;
		}

		public static string SizeName(Building b)
		{
			int num = LandPlot.TierOf(b);
			if (num >= 0)
			{
				return Sizes[Mathf.Clamp(num, 0, Sizes.Length - 1)];
			}
			return "Tent";
		}

		public static bool IsTent(Building b)
		{
			if (b != null && b.asset?.id != null)
			{
				return b.asset.id.StartsWith("tent", StringComparison.Ordinal);
			}
			return false;
		}

		public static bool Next(Building b, out string next, List<KeyValuePair<string, bool>> needs)
		{
			next = null;
			needs?.Clear();
			if (b?.asset == null || string.IsNullOrEmpty(b.asset.upgrade_to))
			{
				return false;
			}
			BuildingAsset buildingAsset = AssetManager.buildings.get(b.asset.upgrade_to);
			if (buildingAsset == null)
			{
				return false;
			}
			int num = LandPlot.TierOf(b);
			next = Sizes[Mathf.Clamp(num + 1, 0, Sizes.Length - 1)];
			if (needs == null)
			{
				return true;
			}
			City city = b.city;
			if (city == null)
			{
				needs.Add(new KeyValuePair<string, bool>("A town to stand in", value: false));
				return true;
			}
			try
			{
				if (num < 0)
				{
					needs.Add(new KeyValuePair<string, bool>("A bonfire in town", city.countBuildingsType("type_bonfire") > 0));
				}
				else
				{
					int num2 = ((num != 0) ? ((num <= 2) ? 1 : 2) : 0);
					needs.Add(new KeyValuePair<string, bool>(Upper(Halls[num2]), Village.HallLevel(city) >= num2));
				}
				int num3 = Growth.HouseCap(city);
				if (num + 1 > num3)
				{
					needs.Add(new KeyValuePair<string, bool>("Town level " + ((num3 <= 1) ? 5 : 10), value: false));
				}
				ConstructionCost cost = buildingAsset.cost;
				if (cost != null)
				{
					Cost(needs, city, "wood", cost.wood);
					Cost(needs, city, "stone", cost.stone);
					Cost(needs, city, "common_metals", cost.common_metals);
					Cost(needs, city, "gold", cost.gold);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("home growth", e);
			}
			return true;
		}

		private static void Cost(List<KeyValuePair<string, bool>> needs, City c, string res, int n)
		{
			if (n > 0)
			{
				int num = 0;
				try
				{
					num = c.getResourcesAmount(res);
				}
				catch
				{
				}
				needs.Add(new KeyValuePair<string, bool>($"{n} {Work.ResourceName(res).ToLowerInvariant()} in the stores ({num})", num >= n));
			}
		}

		public static string Hint(Building b)
		{
			List<KeyValuePair<string, bool>> list = new List<KeyValuePair<string, bool>>();
			if (!Next(b, out var next, list))
			{
				return null;
			}
			List<string> list2 = new List<string>();
			foreach (KeyValuePair<string, bool> item in list)
			{
				if (!item.Value)
				{
					list2.Add(Lower(item.Key));
				}
			}
			string text = next.ToLowerInvariant();
			if (list2.Count != 0)
			{
				return "Grows to a " + text + " with " + string.Join(" and ", list2);
			}
			return "Grows to a " + text;
		}

		private static string Upper(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpperInvariant(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static string Lower(string s)
		{
			if (!string.IsNullOrEmpty(s) && !s.StartsWith("Town level", StringComparison.Ordinal))
			{
				return char.ToLowerInvariant(s[0]) + s.Substring(1);
			}
			return s?.Replace("Town level", "town level");
		}
	}
}
