using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class Flora
	{
		private static readonly Dictionary<string, string> Trees = new Dictionary<string, string>
		{
			{ "savanna_tree_1", "tree_savanna" },
			{ "savanna_tree_2", "tree_savanna_b" },
			{ "savanna_tree_big_1", "tree_baobab" },
			{ "savanna_tree_big_2", "tree_baobab_b" },
			{ "jungle_tree", "tree_jungle" },
			{ "desert_tree", "tree_date_palm" },
			{ "lemon_tree", "tree_lemon" },
			{ "pine_tree", "tree_fir_snow" },
			{ "swamp_tree", "tree_swamp" },
			{ "crystal_tree", "tree_crystal" },
			{ "enchanted_tree", "tree_enchanted" },
			{ "corrupted_tree", "tree_corrupted" },
			{ "corrupted_tree_big", "tree_corrupted_big" },
			{ "infernal_tree", "tree_infernal" },
			{ "infernal_tree_big", "tree_infernal_big" },
			{ "infernal_tree_small", "tree_infernal_stump" },
			{ "candy_tree", "tree_candy" },
			{ "mushroom_tree", "tree_amanita" },
			{ "wasteland_tree", "tree_wasteland" },
			{ "maple_tree", "tree_maple" },
			{ "rocklands_tree", "tree_rocklands" },
			{ "garlic_tree", "tree_garlic" },
			{ "flower_tree_1", "tree_sunflower" },
			{ "flower_tree_2", "tree_poppy" },
			{ "flower_tree_3", "tree_bloom" },
			{ "celestial_tree", "tree_celestial" },
			{ "celestial_tree_small", "tree_celestial_small" },
			{ "singularity_tree", "tree_singularity" },
			{ "clover_tree", "tree_clover" },
			{ "paradox_tree", "tree_paradox" },
			{ "cacti_tree", "tree_saguaro" },
			{ "birch_tree", "tree_birch_b" }
		};

		private static readonly Dictionary<string, string> Plants = new Dictionary<string, string>
		{
			{ "flame_flower", "plant_flame_flower" },
			{ "jungle_flower", "plant_jungle_flower" },
			{ "wasteland_flower", "plant_wasteland_flower" },
			{ "birch_plant", "plant_fern" },
			{ "maple_plant", "plant_maple" },
			{ "flower_plant", "plant_tulips" },
			{ "garlic_plant", "plant_garlic" },
			{ "rocklands_plant", "plant_pebbles" },
			{ "celestial_plant", "plant_celestial" },
			{ "singularity_plant", "plant_singularity" },
			{ "clover_plant", "plant_clover" },
			{ "paradox_plant", "plant_paradox" },
			{ "desert_plant", "plant_desert" },
			{ "crystal_plant", "plant_crystal_cluster" },
			{ "candy_plant", "plant_candy" },
			{ "snow_plant", "plant_snow" },
			{ "corrupted_plant", "plant_corrupted" },
			{ "jungle_plant", "plant_jungle" },
			{ "savanna_plant", "plant_savanna" },
			{ "swamp_plant", "plant_cattails" },
			{ "swamp_plant_big", "plant_marsh" },
			{ "mushroom_white", "plant_mushroom_button" },
			{ "mushroom_teal", "plant_mushroom_tall" },
			{ "mushroom_yellow", "plant_chanterelle" },
			{ "mushroom_green", "plant_mushroom_puff" }
		};

		private static readonly Comparison<uint> ByLuma = (uint a, uint b) => Luma(a).CompareTo(Luma(b));

		public static string TreeFamily(string id)
		{
			if (id == null || !Trees.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		public static string PlantFamily(string id)
		{
			if (id == null || !Plants.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		public static bool Handles(MeshModel model)
		{
			if (model != null)
			{
				return model.Info("palette") != null;
			}
			return false;
		}

		public static uint[] Colors(MeshModel model, SpriteImage img, uint ground)
		{
			string text = model.Info("palette");
			if (text == "plant")
			{
				return NaturePalette.Plant(model, img, ground);
			}
			uint[] array = new uint[model.SlotNames.Length];
			switch (text)
			{
			case "foliage":
			case "crown":
			case "canopy":
				if (img != null && img.Pixels != null)
				{
					Crown(img, text == "foliage", text == "canopy", out var dark, out var mid, out var light, out var bark);
					Fill(model, array, "leaf_dark", dark);
					Fill(model, array, "leaf_mid", mid);
					Fill(model, array, "leaf_light", light);
					Fill(model, array, "leaf_core", TerrainCache.Scale(dark, 0.86f));
					Fill(model, array, "leaf_dark_s", dark);
					Fill(model, array, "leaf_mid_s", mid);
					Fill(model, array, "leaf_light_s", light);
					if (bark != 0)
					{
						Fill(model, array, "bark", bark);
					}
				}
				break;
			}
			Fill(model, array, "ground", NaturePalette.Ground(ground));
			return array;
		}

		private static void Crown(SpriteImage img, bool greens, bool vivid, out uint dark, out uint mid, out uint light, out uint bark)
		{
			int opaqueBottom = img.OpaqueBottom;
			int num = Math.Max(opaqueBottom + 1, img.OpaqueTop);
			int num2 = num - opaqueBottom;
			int width = img.Width;
			List<uint> list = new List<uint>();
			List<uint> list2 = new List<uint>();
			for (int i = opaqueBottom; i < num; i++)
			{
				for (int j = 0; j < width; j++)
				{
					uint num3 = img.Pixels[i * width + j];
					if (num3 >> 24 >= 128)
					{
						num3 |= 0xFF000000u;
						if ((float)i < (float)opaqueBottom + (float)num2 * 0.28f && (float)j > (float)width * 0.25f && (float)j < (float)width * 0.75f)
						{
							list2.Add(num3);
						}
						else
						{
							list.Add(num3);
						}
					}
				}
			}
			NaturePalette.Split(img, out var foliage, out var wood, out var _, out var _);
			List<uint> list3 = ((greens && foliage.Count >= 4) ? foliage : list);
			if (list3 == list)
			{
				List<uint> list4 = list.FindAll((uint c4) => !(NaturePalette.Hue(c4) < 0.12f) || !(NaturePalette.Saturation(c4) < 0.62f) || !(NaturePalette.Value(c4) < 0.6f));
				if (list4.Count >= list.Count * 3 / 10 && list4.Count >= 4)
				{
					list3 = list4;
				}
			}
			if (vivid)
			{
				List<uint> list5 = list3.FindAll((uint c4) => NaturePalette.Saturation(c4) >= 0.22f);
				if (list5.Count >= 4)
				{
					list3 = list5;
				}
			}
			if (list3.Count == 0)
			{
				list3.Add(TerrainCache.Rgb(64, 130, 58));
			}
			list3.Sort(ByLuma);
			uint c = list3[list3.Count * 15 / 100];
			uint c2 = list3[list3.Count / 2];
			uint c3 = list3[Math.Min(list3.Count - 1, list3.Count * 88 / 100)];
			mid = NaturePalette.Desaturate(c2, 0.08f);
			dark = TerrainCache.Lerp(mid, NaturePalette.Desaturate(c, 0.08f), 0.55f);
			light = TerrainCache.Lerp(mid, NaturePalette.Desaturate(c3, 0.08f), 0.6f);
			List<uint> list6 = ((wood.Count >= 3) ? wood : list2.FindAll((uint c4) => NaturePalette.Saturation(c4) < 0.2f || (NaturePalette.Hue(c4) < 0.13f && NaturePalette.Saturation(c4) < 0.7f)));
			list6.Sort(ByLuma);
			bark = ((list6.Count >= 2) ? TerrainCache.Lerp(list6[list6.Count * 62 / 100], TerrainCache.Rgb(96, 72, 52), 0.2f) : 0u);
		}

		public static uint[] SpriteColors(MeshModel model, SpriteImage img)
		{
			uint[] array = new uint[model.SlotNames.Length];
			int opaqueBottom = img.OpaqueBottom;
			int num = Math.Max(opaqueBottom + 1, img.OpaqueTop);
			int num2 = num - opaqueBottom;
			List<uint> list = new List<uint>();
			List<uint> list2 = new List<uint>();
			List<uint> list3 = new List<uint>();
			int width = img.Width;
			for (int i = opaqueBottom; i < num; i++)
			{
				for (int j = 0; j < width; j++)
				{
					uint num3 = img.Pixels[i * width + j];
					if (num3 >> 24 >= 128)
					{
						num3 |= 0xFF000000u;
						list3.Add(num3);
						if ((float)i < (float)opaqueBottom + (float)num2 * 0.28f && (float)j > (float)width * 0.25f && (float)j < (float)width * 0.75f)
						{
							list2.Add(num3);
						}
						else
						{
							list.Add(num3);
						}
					}
				}
			}
			if (list.Count == 0)
			{
				list.AddRange(list3);
			}
			list.Sort((uint a, uint c) => Luma(a).CompareTo(Luma(c)));
			uint b = ((list.Count > 0) ? list[list.Count * 15 / 100] : TerrainCache.Rgb(40, 90, 40));
			uint num4 = ((list.Count > 0) ? list[list.Count / 2] : TerrainCache.Rgb(60, 130, 60));
			uint b2 = ((list.Count > 0) ? list[Math.Min(list.Count - 1, list.Count * 88 / 100)] : TerrainCache.Rgb(100, 170, 70));
			b = TerrainCache.Lerp(num4, b, 0.55f);
			b2 = TerrainCache.Lerp(num4, b2, 0.6f);
			uint num5 = 0u;
			float num6 = 0.25f;
			foreach (uint item in list3)
			{
				float num7 = NaturePalette.Saturation(item);
				if (num7 > num6 && HueDistance(item, num4) > 0.12f)
				{
					num6 = num7;
					num5 = item;
				}
			}
			list2.Sort((uint a, uint c) => Luma(a).CompareTo(Luma(c)));
			uint num8 = ((list2.Count > 0) ? list2[list2.Count / 2] : TerrainCache.Scale(b, 0.7f));
			for (int num9 = 0; num9 < array.Length; num9++)
			{
				switch (model.SlotNames[num9])
				{
				case "bark":
					array[num9] = num8;
					break;
				case "leaf_dark":
					array[num9] = b;
					break;
				case "leaf_mid":
					array[num9] = num4;
					break;
				case "leaf_light":
					array[num9] = b2;
					break;
				case "leaf_core":
					array[num9] = TerrainCache.Scale(b, 0.86f);
					break;
				case "leaf_dark_s":
					array[num9] = b;
					break;
				case "leaf_mid_s":
					array[num9] = num4;
					break;
				case "leaf_light_s":
					array[num9] = b2;
					break;
				case "fruit":
					array[num9] = ((num5 != 0) ? num5 : TerrainCache.Rgb(196, 58, 48));
					break;
				case "stone":
					array[num9] = num4;
					break;
				case "stone_dark":
					array[num9] = b;
					break;
				case "ore":
					array[num9] = ((num5 != 0) ? num5 : b2);
					break;
				}
			}
			return array;
		}

		private static void Fill(MeshModel model, uint[] pal, string slot, uint color)
		{
			for (int i = 0; i < pal.Length; i++)
			{
				if (model.SlotNames[i] == slot)
				{
					pal[i] = color | 0xFF000000u;
				}
			}
		}

		private static float HueDistance(uint a, uint b)
		{
			float num = Math.Abs(NaturePalette.Hue(a) - NaturePalette.Hue(b));
			return Math.Min(num, 1f - num);
		}

		private static int Luma(uint c)
		{
			return (int)((c & 0xFF) * 3 + ((c >> 8) & 0xFF) * 6 + ((c >> 16) & 0xFF));
		}
	}
}
