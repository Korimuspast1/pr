using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class Growth
	{
		private sealed class Seen
		{
			public int Version = -1;

			public readonly Dictionary<string, int> Amounts = new Dictionary<string, int>();

			public readonly Dictionary<string, int> Ignored = new Dictionary<string, int>();
		}

		public const string PointsKey = "fp_growth";

		public const string LevelKey = "fp_level";

		private static readonly Dictionary<long, Seen> Towns = new Dictionary<long, Seen>();

		private static float _read;

		private static object _world;

		public static int PointsFor(int level)
		{
			if (level > 1)
			{
				return 10 * level * (level - 1);
			}
			return 0;
		}

		public static int PointsOf(City c)
		{
			try
			{
				c.data.get("fp_growth", out var pResult, 0);
				return Mathf.Max(0, pResult);
			}
			catch
			{
				return 0;
			}
		}

		public static int LevelOf(City c)
		{
			if (c == null)
			{
				return 1;
			}
			if (!Village.IsVillage(c))
			{
				int num = Village.HallLevel(c);
				if (num < 2)
				{
					if (num != 1)
					{
						return 4;
					}
					return 9;
				}
				return 99;
			}
			try
			{
				c.data.get("fp_level", out var pResult, 1);
				return Mathf.Max(1, pResult);
			}
			catch
			{
				return 1;
			}
		}

		public static float Progress(City c)
		{
			int num = LevelOf(c);
			int num2 = PointsOf(c);
			int num3 = PointsFor(num);
			int num4 = PointsFor(num + 1);
			return Mathf.Clamp01((float)(num2 - num3) / (float)Mathf.Max(1, num4 - num3));
		}

		public static int HouseCap(City c)
		{
			if (!Village.IsVillage(c))
			{
				return 5;
			}
			int num = LevelOf(c);
			if (num < 10)
			{
				if (num < 5)
				{
					return 1;
				}
				return 2;
			}
			return 5;
		}

		public static void Ignore(City c, string res, int n)
		{
			if (c != null && n > 0 && Towns.TryGetValue(c.data.id, out var value))
			{
				value.Ignored.TryGetValue(res, out var value2);
				value.Ignored[res] = value2 + n;
			}
		}

		private static int Worth(string res)
		{
			ResourceAsset resourceAsset = null;
			try
			{
				resourceAsset = AssetManager.resources.get(res);
			}
			catch
			{
			}
			if (resourceAsset == null)
			{
				return 1;
			}
			switch (res)
			{
			default:
				if (!resourceAsset.food && resourceAsset.type != ResType.Food && resourceAsset.type != ResType.Ingredient_Food)
				{
					return 2;
				}
				goto case "wood";
			case "wood":
			case "stone":
			case "gold":
				return 1;
			}
		}

		public static void Tick(float dt)
		{
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				_world = obj;
				Towns.Clear();
			}
			if ((_read -= dt) > 0f)
			{
				return;
			}
			_read = 1f;
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive() && Village.IsVillage(item))
					{
						Read(item);
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("growth", e);
			}
		}

		private static void Read(City c)
		{
			if (!Towns.TryGetValue(c.data.id, out var value))
			{
				value = (Towns[c.data.id] = new Seen());
			}
			bool flag = value.Version < 0;
			value.Version = 0;
			int num = 0;
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			try
			{
				foreach (ResourceAsset item in AssetManager.resources.list)
				{
					int resourcesAmount = c.getResourcesAmount(item.id);
					if (resourcesAmount > 0)
					{
						dictionary[item.id] = resourcesAmount;
					}
				}
			}
			catch
			{
				return;
			}
			foreach (KeyValuePair<string, int> item2 in dictionary)
			{
				value.Amounts.TryGetValue(item2.Key, out var value2);
				int num2 = item2.Value - value2;
				if (!(num2 <= 0 || flag))
				{
					if (value.Ignored.TryGetValue(item2.Key, out var value3) && value3 > 0)
					{
						int num3 = Mathf.Min(value3, num2);
						num2 -= num3;
						value.Ignored[item2.Key] = value3 - num3;
					}
					num += num2 * Worth(item2.Key);
				}
			}
			value.Amounts.Clear();
			foreach (KeyValuePair<string, int> item3 in dictionary)
			{
				value.Amounts[item3.Key] = item3.Value;
			}
			if (num > 0)
			{
				Add(c, num, null);
			}
		}

		public static void Add(City c, int points, string why)
		{
			if (c != null && points > 0 && Village.IsVillage(c))
			{
				int num = PointsOf(c) + points;
				try
				{
					c.data.set("fp_growth", num);
				}
				catch
				{
					return;
				}
				if (why != null)
				{
					Log.Info($"growth: {Rep.NameOf(c)} +{points} ({why})");
				}
				int i;
				for (i = LevelOf(c); num >= PointsFor(i + 1); i++)
				{
				}
				if (i > LevelOf(c))
				{
					LevelUp(c, i);
				}
			}
		}

		private static void LevelUp(City c, int level)
		{
			try
			{
				c.data.set("fp_level", level);
			}
			catch
			{
				return;
			}
			List<string> list = new List<string>();
			foreach (PlanPieces.Category item in PlanPieces.All)
			{
				foreach (PlanPieces.Piece piece in item.Pieces)
				{
					if (piece.Level == level && PlanPieces.AssetOf(piece, c) != null)
					{
						list.Add(piece.Name.ToLowerInvariant());
					}
				}
			}
			if (level == 5)
			{
				list.Add("raising the hall");
			}
			if (level == 10)
			{
				list.Add("the city hall");
			}
			string text = Rep.NameOf(c) + " is now level " + level + ((list.Count > 0) ? (". Opens: " + string.Join(", ", list.ToArray())) : "");
			Log.Info("growth: " + text);
			Hub.Toast?.Invoke(text);
		}

		public static void RaiseHall(City c)
		{
			if (!Village.IsVillage(c) || c.hasBuildingToBuild())
			{
				return;
			}
			int num = LevelOf(c);
			int num2 = Village.HallLevel(c);
			int num3 = ((num >= 10) ? 2 : ((num >= 5) ? 1 : 0));
			if (num2 < 0 || num2 >= num3)
			{
				return;
			}
			foreach (Building building in c.buildings)
			{
				if (building != null && building.asset != null && !(building.asset.type != "type_hall") && !building.isUnderConstruction() && !building.isRuin())
				{
					BuildingAsset buildingAsset = (string.IsNullOrEmpty(building.asset.upgrade_to) ? null : AssetManager.buildings.get(building.asset.upgrade_to));
					if (buildingAsset != null && c.hasEnoughResourcesFor(buildingAsset.cost) && CityBehBuild.upgradeBuilding(building, c))
					{
						string text = ((num3 >= 2) ? "a city hall" : "a town hall");
						Log.Info("growth: " + Rep.NameOf(c) + " raised its hall to " + building.asset.id);
						Hub.Toast?.Invoke(Rep.NameOf(c) + " has raised " + text);
					}
					break;
				}
			}
		}
	}
}
