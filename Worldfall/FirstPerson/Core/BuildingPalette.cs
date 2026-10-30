using System;

namespace FirstPerson.Core
{
	public static class BuildingPalette
	{
		private static readonly uint Soot = TerrainCache.Rgb(40, 35, 32);

		private static readonly uint Grime = TerrainCache.Rgb(128, 120, 110);

		private static readonly uint Ash = TerrainCache.Rgb(104, 98, 92);

		public static bool TownArt(string family)
		{
			if (family != null)
			{
				if (!family.StartsWith("tw_", StringComparison.Ordinal) && !family.StartsWith("ruin_tw_", StringComparison.Ordinal))
				{
					return family.StartsWith("site_tw_", StringComparison.Ordinal);
				}
				return true;
			}
			return false;
		}

		public static string RaceOf(string family)
		{
			if (!family.Contains("elf"))
			{
				if (!family.Contains("orc"))
				{
					if (!family.Contains("dwarf"))
					{
						return "human";
					}
					return "dwarf";
				}
				return "orc";
			}
			return "elf";
		}

		private static uint WallOf(string race)
		{
			switch (race)
			{
			default:
				return TerrainCache.Rgb(220, 204, 170);
			case "dwarf":
				return TerrainCache.Rgb(214, 196, 160);
			case "orc":
				return TerrainCache.Rgb(150, 118, 84);
			case "elf":
				return TerrainCache.Rgb(228, 226, 214);
			}
		}

		private static uint StoneOf(string race)
		{
			if (!(race == "elf"))
			{
				if (!(race == "dwarf"))
				{
					return TerrainCache.Rgb(140, 136, 128);
				}
				return TerrainCache.Rgb(128, 122, 116);
			}
			return TerrainCache.Rgb(178, 180, 176);
		}

		private static uint TimberOf(string race)
		{
			if (!(race == "orc"))
			{
				return TerrainCache.Rgb(86, 58, 38);
			}
			return TerrainCache.Rgb(92, 66, 44);
		}

		private static uint RoofOf(uint kingdom)
		{
			int num = (int)((kingdom & 0xFF) * 3 + ((kingdom >> 8) & 0xFF) * 6 + ((kingdom >> 16) & 0xFF)) / 10;
			return TerrainCache.Scale(TerrainCache.Lerp(kingdom, TerrainCache.Rgb(num, num, num), 0.3f), 0.86f);
		}

		public static uint[] Whole(MeshModel model, string family, uint kingdom)
		{
			if (Species.IsCivFamily(family) || TownArt(family))
			{
				return Civ(model, kingdom, ruin: false);
			}
			string race = RaceOf(family);
			uint[] array = new uint[model.SlotNames.Length];
			for (int i = 0; i < array.Length; i++)
			{
				switch (model.SlotNames[i])
				{
				case "roof":
					array[i] = RoofOf(kingdom);
					break;
				case "banner":
					array[i] = kingdom;
					break;
				case "wall":
					array[i] = WallOf(race);
					break;
				case "stone":
					array[i] = StoneOf(race);
					break;
				case "timber":
					array[i] = TimberOf(race);
					break;
				}
			}
			return array;
		}

		private static uint[] Civ(MeshModel model, uint kingdom, bool ruin)
		{
			float num = model.InfoFloat("kingdom_tone", 0.3f);
			int num2 = (int)((kingdom & 0xFF) * 3 + ((kingdom >> 8) & 0xFF) * 6 + ((kingdom >> 16) & 0xFF)) / 10;
			uint num3 = TerrainCache.Scale(TerrainCache.Lerp(kingdom, TerrainCache.Rgb(num2, num2, num2), num), 1f - num * 0.46f);
			uint[] array = new uint[model.SlotNames.Length];
			for (int i = 0; i < array.Length; i++)
			{
				switch (model.SlotNames[i])
				{
				case "roof":
					array[i] = (ruin ? TerrainCache.Scale(TerrainCache.Lerp(num3, Grime, 0.4f), 0.8f) : num3);
					break;
				case "roof_dark":
					array[i] = TerrainCache.Scale(ruin ? TerrainCache.Lerp(num3, Grime, 0.4f) : num3, ruin ? 0.55f : 0.68f);
					break;
				case "banner":
					array[i] = (ruin ? TerrainCache.Scale(TerrainCache.Lerp(kingdom, Grime, 0.45f), 0.72f) : kingdom);
					break;
				case "banner_dark":
					array[i] = TerrainCache.Scale(ruin ? TerrainCache.Lerp(kingdom, Grime, 0.45f) : kingdom, ruin ? 0.5f : 0.62f);
					break;
				}
			}
			return array;
		}

		public static uint[] Ruin(MeshModel model, string family, uint kingdom)
		{
			if (Species.IsCivFamily(family) || TownArt(family))
			{
				return Civ(model, kingdom, ruin: true);
			}
			string race = RaceOf(family);
			uint num = TerrainCache.Scale(TerrainCache.Lerp(WallOf(race), Grime, 0.12f), 0.97f);
			uint num2 = TerrainCache.Scale(TerrainCache.Lerp(StoneOf(race), Grime, 0.15f), 0.9f);
			uint[] array = new uint[model.SlotNames.Length];
			for (int i = 0; i < array.Length; i++)
			{
				switch (model.SlotNames[i])
				{
				case "wall":
					array[i] = num;
					break;
				case "soot":
					array[i] = TerrainCache.Lerp(num, Soot, 0.34f);
					break;
				case "smoke":
					array[i] = TerrainCache.Lerp(num, Soot, 0.68f);
					break;
				case "stone":
					array[i] = num2;
					break;
				case "rubble":
					array[i] = TerrainCache.Scale(TerrainCache.Lerp(num2, num, 0.3f), 0.86f);
					break;
				case "timber":
					array[i] = TerrainCache.Lerp(TimberOf(race), Soot, 0.35f);
					break;
				case "char":
					array[i] = TerrainCache.Rgb(50, 41, 35);
					break;
				case "ash":
					array[i] = TerrainCache.Lerp(Ash, num2, 0.2f);
					break;
				case "roof":
					array[i] = TerrainCache.Scale(TerrainCache.Lerp(RoofOf(kingdom), Grime, 0.4f), 0.8f);
					break;
				case "banner":
					array[i] = TerrainCache.Scale(TerrainCache.Lerp(kingdom, Grime, 0.45f), 0.72f);
					break;
				case "door":
					array[i] = TerrainCache.Lerp(TerrainCache.Rgb(104, 72, 46), Grime, 0.3f);
					break;
				}
			}
			return array;
		}
	}
}
