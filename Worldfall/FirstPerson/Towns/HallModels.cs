using System;
using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class HallModels
	{
		private static readonly string[] Races = new string[4] { "human", "elf", "orc", "dwarf" };

		private static readonly HashSet<long> Capitals = new HashSet<long>();

		private static readonly HashSet<long> Seen = new HashSet<long>();

		private static float _watch;

		private static bool _primed;

		public static string Pick(Building b, string family)
		{
			BuildingAsset asset = b.asset;
			if (asset == null || asset.type != "type_hall")
			{
				return null;
			}
			if (!Parse(asset.id, out var race, out var level))
			{
				return null;
			}
			bool flag = false;
			if (level == 2)
			{
				try
				{
					flag = TownTier.IsCapital(b.city);
				}
				catch
				{
				}
			}
			if (!flag)
			{
				return "tw_hall_" + race + "_" + level;
			}
			return "tw_keep_" + race;
		}

		public static void Watch(float dt)
		{
			_watch -= dt;
			if (_watch > 0f)
			{
				return;
			}
			_watch = 4f;
			Seen.Clear();
			foreach (Kingdom item in World.world.kingdoms.list)
			{
				City city = item?.capital;
				if (city != null)
				{
					Seen.Add(city.data.id);
				}
			}
			if (!_primed || !Seen.SetEquals(Capitals))
			{
				Capitals.Clear();
				Capitals.UnionWith(Seen);
				if (_primed)
				{
					TownModels.Bump();
				}
				_primed = true;
			}
		}

		internal static bool Parse(string id, out string race, out int level)
		{
			race = null;
			level = -1;
			if (id == null || !id.StartsWith("hall_", StringComparison.Ordinal) || id.Contains("_civ_"))
			{
				return false;
			}
			int num = id.LastIndexOf('_');
			if (num <= 5 || !int.TryParse(id.Substring(num + 1), out level) || level < 0 || level > 2)
			{
				return false;
			}
			race = id.Substring(5, num - 5);
			return Array.IndexOf(Races, race) >= 0;
		}
	}
}
