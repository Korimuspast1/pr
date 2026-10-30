using System;
using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class TownTier
	{
		internal static readonly Dictionary<long, int> ForcedLevel = new Dictionary<long, int>();

		private static readonly Dictionary<long, Tier> Seen = new Dictionary<long, Tier>();

		private static float _timer;

		public static event Action<City, Tier, Tier> Changed;

		public static Tier Of(City c)
		{
			int num = HallLevel(c);
			if (num >= 2)
			{
				return Tier.City;
			}
			if (num == 1)
			{
				return Tier.Town;
			}
			if (num < 0 && IsCivPeople(c))
			{
				return ByGrowth(c);
			}
			return Tier.Village;
		}

		public static int HallLevel(City c)
		{
			if (c == null)
			{
				return -1;
			}
			try
			{
				if (ForcedLevel.Count > 0 && ForcedLevel.TryGetValue(c.data.id, out var value))
				{
					return value;
				}
				if (c.buildings_dict_type == null || !c.buildings_dict_type.TryGetValue("type_hall", out var value2) || value2 == null)
				{
					return -1;
				}
				int num = -1;
				foreach (Building item in value2)
				{
					if (item == null || !item.isAlive() || item.asset == null)
					{
						continue;
					}
					string text = item.asset.id ?? "";
					if (text.Contains("_civ_"))
					{
						continue;
					}
					try
					{
						if (item.isUnderConstruction() || item.isRuin())
						{
							continue;
						}
						goto IL_00cc;
					}
					catch
					{
						goto IL_00cc;
					}
					IL_00cc:
					char c2 = ((text.Length > 0) ? text[text.Length - 1] : '0');
					int num2 = ((c2 >= '0' && c2 <= '9') ? (c2 - 48) : 0);
					if (num2 > num)
					{
						num = num2;
					}
				}
				return num;
			}
			catch
			{
				return -1;
			}
		}

		public static bool IsCapital(City c)
		{
			try
			{
				return c != null && c.kingdom != null && c.kingdom.capital == c;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsCivPeople(City c)
		{
			try
			{
				if (c.buildings_dict_type == null || !c.buildings_dict_type.TryGetValue("type_hall", out var value) || value == null)
				{
					return false;
				}
				foreach (Building item in value)
				{
					if (item != null && item.asset != null && (item.asset.id ?? "").Contains("_civ_"))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		private static Tier ByGrowth(City c)
		{
			int num = 0;
			int num2 = 0;
			try
			{
				num = c.getPopulationPeople();
				num2 = c.countBuildings();
			}
			catch
			{
			}
			if (num >= 100 && num2 >= 20)
			{
				return Tier.City;
			}
			if (num >= 30 && num2 >= 8)
			{
				return Tier.Town;
			}
			return Tier.Village;
		}

		public static string Word(Tier t)
		{
			switch (t)
			{
			default:
				return "village";
			case Tier.Town:
				return "town";
			case Tier.City:
				return "city";
			}
		}

		internal static void Reset()
		{
			Seen.Clear();
			ForcedLevel.Clear();
			_timer = 0f;
		}

		internal static void Watch(IEnumerable<City> near, float dt, Action<string> toast)
		{
			_timer -= dt;
			if (_timer > 0f)
			{
				return;
			}
			_timer = 1f;
			foreach (City item in near)
			{
				long id;
				try
				{
					if (item == null || !item.isAlive())
					{
						continue;
					}
					id = item.data.id;
					goto IL_005a;
				}
				catch
				{
				}
				continue;
				IL_005a:
				Tier tier = Of(item);
				if (Seen.TryGetValue(id, out var value) && value != tier)
				{
					Log.Info("towns: " + item.name + " is a " + Word(tier) + " now (was a " + Word(value) + ")");
					if (tier > value)
					{
						toast?.Invoke(item.name + " is a " + Word(tier) + " now.");
					}
					try
					{
						TownTier.Changed?.Invoke(item, value, tier);
					}
					catch (Exception ex)
					{
						Log.Error("towns: tier changed: " + ex);
					}
				}
				Seen[id] = tier;
			}
		}
	}
}
