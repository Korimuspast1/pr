using System;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Workbench
	{
		public const int MaxLevel = 5;

		public const string Key = "fp_bench";

		private const float Reach = 5f;

		private static readonly string[] Adds = new string[6] { "wood, bone and leather", "stone", "copper and bronze", "iron, silver and steel", "mythril", "adamantine" };

		private static long _ownedBody = -1L;

		private static Building _owned;

		private static float _ownedAt = -99f;

		private static long _watchFor = -1L;

		private static int _watchLevel = -1;

		private static float _watchTimer;

		private static string _hint;

		private static float _hintAt;

		public static int Rank(EquipmentAsset a)
		{
			if (a == null)
			{
				return 0;
			}
			string text = null;
			string id = null;
			string id2 = null;
			try
			{
				text = a.material;
				id = a.cost_resource_id_1;
				id2 = a.cost_resource_id_2;
			}
			catch
			{
			}
			int val;
			switch (text)
			{
			case "stone":
				val = 1;
				break;
			case "bronze":
			case "copper":
				val = 2;
				break;
			case "steel":
			case "silver":
			case "iron":
				val = 3;
				break;
			case "mythril":
				val = 4;
				break;
			case "adamantine":
				val = 5;
				break;
			default:
				val = 0;
				break;
			}
			return Math.Max(val, Math.Max(CostRank(id), CostRank(id2)));
		}

		private static int CostRank(string id)
		{
			if (string.IsNullOrEmpty(id) || id == "none")
			{
				return 0;
			}
			if (id.Contains("adamantine"))
			{
				return 5;
			}
			if (id.Contains("mythril") || id.Contains("gem") || id.Contains("dragon"))
			{
				return 4;
			}
			if (id.Contains("iron") || id.Contains("silver") || id.Contains("steel"))
			{
				return 3;
			}
			if (id.Contains("metal") || id.Contains("copper") || id.Contains("bronze"))
			{
				return 2;
			}
			if (id.Contains("stone"))
			{
				return 1;
			}
			return 0;
		}

		public static string AddsAt(int level)
		{
			return Adds[Mathf.Clamp(level, 0, 5)];
		}

		public static Building HomeOf(Actor you)
		{
			try
			{
				if (you == null)
				{
					return null;
				}
				Building building = (you.hasHomeBuilding() ? you.getHomeBuilding() : null);
				if (building != null && building.isAlive())
				{
					return building;
				}
				return OwnedHouse(you);
			}
			catch
			{
				return null;
			}
		}

		private static Building OwnedHouse(Actor you)
		{
			long id = you.data.id;
			float unscaledTime = Time.unscaledTime;
			if (id != _ownedBody || unscaledTime - _ownedAt > 2f || (_owned != null && !_owned.isAlive()))
			{
				_ownedBody = id;
				_ownedAt = unscaledTime;
				_owned = null;
				foreach (Building item in Homes.Owned(you))
				{
					if (HouseInterior.IsEnterable(item))
					{
						_owned = item;
						break;
					}
				}
			}
			if (_owned == null || !_owned.isAlive() || !Homes.OwnedBy(_owned, you))
			{
				return null;
			}
			return _owned;
		}

		public static int HouseLevel(Actor you)
		{
			Building building = HomeOf(you);
			if (building == null)
			{
				return 0;
			}
			HouseInterior.RaceAndTier(building.asset, out var _, out var tier);
			return 1 + Mathf.Clamp(tier, 0, 5) / 2;
		}

		public static int Tools(Actor you)
		{
			int pResult = 0;
			try
			{
				you.data.get("fp_bench", out pResult, 0);
			}
			catch
			{
			}
			return Mathf.Clamp(pResult, 0, 4);
		}

		public static int Level(Actor you)
		{
			int num = HouseLevel(you);
			if (num != 0)
			{
				return Mathf.Min(5, num + Tools(you));
			}
			return 0;
		}

		public static bool AtHome(Actor you)
		{
			Building building = HomeOf(you);
			if (building == null)
			{
				return false;
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null && instance.Interior.Inside)
			{
				return instance.Interior.House == building;
			}
			try
			{
				WorldTile current_tile = building.current_tile;
				if (current_tile == null)
				{
					return false;
				}
				return (you.current_position - new Vector2(current_tile.posV3.x, current_tile.posV3.y)).sqrMagnitude < 25f;
			}
			catch
			{
				return false;
			}
		}

		public static int Usable(Actor you)
		{
			if (!AtHome(you))
			{
				return 0;
			}
			return Level(you);
		}

		public static bool WantsTools(Actor you)
		{
			return Tools(you) + Math.Max(1, HouseLevel(you)) < 5;
		}

		public static int GiveTools(Actor you)
		{
			if (you == null)
			{
				return 0;
			}
			try
			{
				you.data.set("fp_bench", Mathf.Min(Tools(you) + 1, 4));
			}
			catch (Exception ex)
			{
				Log.Error("workbench: tools: " + ex.Message);
			}
			int num = Level(you);
			Log.Info($"workbench: tools from a task for {WorldBoxMod.Describe(you)}: {Tools(you)} levels, workbench {num}");
			return num;
		}

		public static void Watch(Actor you, float dt, Action<string> toast)
		{
			if (_hint != null && Time.unscaledTime >= _hintAt)
			{
				toast?.Invoke(_hint);
				_hint = null;
			}
			_watchTimer -= dt;
			if (_watchTimer > 0f || you == null)
			{
				return;
			}
			_watchTimer = 2f;
			long id;
			try
			{
				id = you.data.id;
			}
			catch
			{
				return;
			}
			int num = HouseLevel(you);
			if (id != _watchFor)
			{
				_watchFor = id;
				_watchLevel = num;
				return;
			}
			int num2 = Level(you);
			if (num > _watchLevel)
			{
				Building building = null;
				try
				{
					building = HomeLife.OwnHouse(you);
				}
				catch
				{
				}
				string arg = ((building != null && HomeGrowth.IsTent(building)) ? "tent" : "house");
				toast?.Invoke((_watchLevel <= 0) ? $"A {arg} of your own. Its workbench works up to {AddsAt(num2)} (level {num2})" : $"Your {arg} has grown. Your workbench now works up to {AddsAt(num2)} (level {num2})");
				string text = ((building != null) ? HomeGrowth.Hint(building) : null);
				if (text != null)
				{
					_hint = text + ". E at its door for more";
					_hintAt = Time.unscaledTime + 3f;
				}
			}
			_watchLevel = num;
		}
	}
}
