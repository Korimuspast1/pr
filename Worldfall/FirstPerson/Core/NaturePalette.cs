using System;
using System.Collections.Generic;
using System.Linq;

namespace FirstPerson.Core
{
	public static class NaturePalette
	{
		private static readonly Comparison<uint> ByLuma = (uint a, uint b) => Luma(a).CompareTo(Luma(b));

		public static bool Handles(string family)
		{
			if (family != null)
			{
				if (!family.StartsWith("bush", StringComparison.Ordinal) && !family.StartsWith("rock_", StringComparison.Ordinal))
				{
					switch (family)
					{
					default:
						return family == "plant_grass_b";
					case "plant_flower":
					case "plant_mushroom":
					case "plant_herb":
					case "plant_grass":
						break;
					}
				}
				return true;
			}
			return false;
		}

		public static float LumpHeight(string family)
		{
			if (family == null || !family.StartsWith("bush", StringComparison.Ordinal))
			{
				return 1f;
			}
			return 1.15f;
		}

		public static uint[] Lump(MeshModel model, SpriteImage img, uint ground)
		{
			uint[] array = new uint[model.SlotNames.Length];
			Split(img, out var foliage, out var wood, out var accent, out var _);
			if (Has(model, "stone"))
			{
				List<uint> list = new List<uint>();
				List<uint> list2 = new List<uint>();
				uint[] pixels = img.Pixels;
				foreach (uint num in pixels)
				{
					if (num >> 24 < 128)
					{
						continue;
					}
					uint num2 = num | 0xFF000000u;
					if (!(Value(num2) < 0.13f))
					{
						if (Saturation(num2) < 0.34f)
						{
							list.Add(num2);
						}
						else
						{
							list2.Add(num2);
						}
					}
				}
				list.Sort(ByLuma);
				uint c = ((list.Count > 0) ? list[list.Count * 55 / 100] : Rgb(120, 118, 114));
				uint c2 = ((list.Count > 0) ? list[Math.Min(list.Count - 1, list.Count * 90 / 100)] : Rgb(150, 148, 142));
				c = Stone(c);
				c2 = Stone(c2);
				uint color = TerrainCache.Scale(c, 0.72f);
				uint num3 = OreColor(list2, c2, img);
				if (Has(model, "bone"))
				{
					Fill(model, array, "bone", c2);
					Fill(model, array, "bone_dark", TerrainCache.Scale(c2, 0.8f));
					c = Lerp(TerrainCache.Scale(c, 0.62f), Rgb(104, 92, 80), 0.5f);
					color = TerrainCache.Scale(c, 0.72f);
					c2 = Lerp(c, c2, 0.25f);
				}
				Fill(model, array, "stone", Lerp(c, c2, 0.35f));
				Fill(model, array, "stone_dark", color);
				Fill(model, array, "ore", num3);
				Fill(model, array, "ore_light", Lerp(num3, Rgb(255, 255, 255), 0.4f));
			}
			else
			{
				Leaves(foliage, ground, out var dark, out var mid, out var light);
				wood.Sort(ByLuma);
				uint a = ((wood.Count > 0) ? wood[wood.Count / 2] : TerrainCache.Scale(dark, 0.7f));
				Fill(model, array, "leaf_dark", dark);
				Fill(model, array, "leaf_mid", mid);
				Fill(model, array, "leaf_light", light);
				Fill(model, array, "leaf_core", TerrainCache.Scale(dark, 0.86f));
				Fill(model, array, "leaf_dark_s", dark);
				Fill(model, array, "leaf_mid_s", mid);
				Fill(model, array, "leaf_light_s", light);
				Fill(model, array, "bark", Lerp(a, Rgb(92, 70, 52), 0.35f));
				uint num4 = Berry(accent);
				Fill(model, array, "fruit", num4);
				Fill(model, array, "fruit_light", Lerp(num4, Rgb(255, 236, 220), 0.35f));
			}
			Fill(model, array, "ground", Ground(ground));
			return array;
		}

		public static uint[] Plant(MeshModel model, SpriteImage img, uint ground)
		{
			uint[] array = new uint[model.SlotNames.Length];
			Split(img, out var foliage, out var wood, out var accent, out var neutral);
			if (Has(model, "cap"))
			{
				List<uint> list = new List<uint>();
				list.AddRange(accent);
				list.AddRange(foliage);
				if (list.Count == 0)
				{
					list.AddRange(neutral);
				}
				if (list.Count == 0)
				{
					list.Add(Rgb(220, 214, 200));
				}
				list.Sort(ByLuma);
				uint num = list[list.Count * 40 / 100];
				uint num2 = list[list.Count - 1];
				bool flag = Saturation(num) < 0.15f;
				if (flag)
				{
					num = Lerp(num, Rgb(226, 214, 190), 0.45f);
					num2 = Lerp(num2, Rgb(240, 232, 214), 0.4f);
				}
				else
				{
					num = Desaturate(num, 0.12f);
					num2 = Desaturate(num2, 0.12f);
				}
				uint num3 = Lerp(Rgb(232, 222, 198), num, flag ? 0.05f : 0.1f);
				Fill(model, array, "cap", num);
				Fill(model, array, "cap_light", num2);
				Fill(model, array, "gills", Lerp(TerrainCache.Scale(num3, 0.78f), num, 0.18f));
				Fill(model, array, "flesh", num3);
				Fill(model, array, "spots", flag ? Lerp(num2, Rgb(205, 190, 160), 0.5f) : Lerp(num3, Rgb(255, 250, 240), 0.5f));
			}
			bool flag2 = foliage.Count > 0;
			Leaves(flag2 ? foliage : ((wood.Count + neutral.Count > 0) ? new List<uint>(wood.Concat(neutral)) : accent), flag2 ? ground : 0u, out var dark, out var mid, out var light);
			uint num4 = ((accent.Count > 0) ? Commonest(accent) : Lerp(light, Rgb(210, 70, 60), 0.5f));
			Fill(model, array, "fur", mid);
			Fill(model, array, "fur_light", light);
			Fill(model, array, "dark", dark);
			Fill(model, array, "accent", num4);
			Fill(model, array, "accent_dark", TerrainCache.Scale(num4, 0.72f));
			Fill(model, array, "horn", Rgb(236, 198, 84));
			Fill(model, array, "ground", Ground(ground));
			return array;
		}

		internal static void Split(SpriteImage img, out List<uint> foliage, out List<uint> wood, out List<uint> accent, out List<uint> neutral)
		{
			foliage = new List<uint>();
			wood = new List<uint>();
			accent = new List<uint>();
			neutral = new List<uint>();
			if (img == null || img.Pixels == null)
			{
				return;
			}
			uint[] pixels = img.Pixels;
			foreach (uint num in pixels)
			{
				if (num >> 24 >= 128)
				{
					uint num2 = num | 0xFF000000u;
					float num3 = Saturation(num2);
					float num4 = Hue(num2);
					float num5 = Value(num2);
					if (num3 < 0.16f || num5 < 0.13f)
					{
						neutral.Add(num2);
					}
					else if (num4 > 0.16f && num4 < 0.47f)
					{
						foliage.Add(num2);
					}
					else if (num4 < 0.12f && num3 < 0.62f && num5 < 0.6f && num5 > 0.2f)
					{
						wood.Add(num2);
					}
					else
					{
						accent.Add(num2);
					}
				}
			}
		}

		private static void Leaves(List<uint> foliage, uint ground, out uint dark, out uint mid, out uint light)
		{
			if (foliage.Count == 0)
			{
				foliage = new List<uint> { Rgb(64, 132, 58) };
			}
			foliage.Sort(ByLuma);
			uint num = foliage[foliage.Count * 15 / 100];
			uint num2 = foliage[foliage.Count / 2];
			uint num3 = foliage[Math.Min(foliage.Count - 1, foliage.Count * 88 / 100)];
			float num4 = Hue(ground);
			bool flag = Saturation(ground) > 0.2f && num4 > 0.14f && num4 < 0.45f;
			dark = (flag ? Toward(num, ground, 0.45f, 0.35f) : num);
			mid = (flag ? Toward(num2, ground, 0.45f, 0.35f) : num2);
			light = (flag ? Toward(num3, ground, 0.4f, 0.3f) : num3);
			dark = Lerp(mid, dark, 0.7f);
			light = Lerp(mid, light, 0.75f);
			if (flag)
			{
				float num5 = Value(ground);
				float num6 = Math.Min(1f, Math.Min(num5 * 0.95f / Math.Max(0.001f, Value(light)), num5 * 0.82f / Math.Max(0.001f, Value(mid))));
				if (num6 < 1f)
				{
					dark = TerrainCache.Scale(dark, num6);
					mid = TerrainCache.Scale(mid, num6);
					light = TerrainCache.Scale(light, num6);
				}
			}
		}

		private static uint Berry(List<uint> accent)
		{
			uint num = 0u;
			float num2 = -1f;
			foreach (uint item in accent)
			{
				float num3 = Saturation(item) * (0.4f + Value(item));
				if (num3 > num2)
				{
					num2 = num3;
					num = item;
				}
			}
			if (num == 0)
			{
				return Rgb(200, 52, 48);
			}
			return num;
		}

		private static uint OreColor(List<uint> accent, uint lite, SpriteImage img)
		{
			if (accent.Count > 0)
			{
				uint result = 0u;
				float num = -1f;
				{
					foreach (uint item in accent)
					{
						float num2 = Saturation(item) + Value(item) * 0.5f;
						if (num2 > num)
						{
							num = num2;
							result = item;
						}
					}
					return result;
				}
			}
			return Lerp(lite, Rgb(214, 218, 222), 0.5f);
		}

		private static uint Stone(uint c)
		{
			uint a = Desaturate(c, 0.75f);
			a = Lerp(a, TerrainCache.Multiply(a, Rgb(255, 244, 226)), 0.6f);
			float num = Value(a);
			if (!(num < 0.5f))
			{
				return a;
			}
			return Lerp(a, Rgb(140, 136, 128), (0.5f - num) / 0.5f);
		}

		internal static uint Ground(uint ground)
		{
			return TerrainCache.Scale((ground == 0) ? Rgb(110, 150, 70) : (ground | 0xFF000000u), 0.76f);
		}

		private static bool Has(MeshModel model, string slot)
		{
			string[] slotNames = model.SlotNames;
			for (int i = 0; i < slotNames.Length; i++)
			{
				if (slotNames[i] == slot)
				{
					return true;
				}
			}
			return false;
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

		private static uint Commonest(List<uint> list)
		{
			Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
			uint result = list[0];
			int num = 0;
			foreach (uint item in list)
			{
				uint key = item & 0xFFF8F8F8u;
				dictionary.TryGetValue(key, out var value);
				value = (dictionary[key] = value + 1);
				if (value > num)
				{
					num = value;
					result = item;
				}
			}
			return result;
		}

		private static uint Rgb(int r, int g, int b)
		{
			return TerrainCache.Rgb(r, g, b);
		}

		private static uint Lerp(uint a, uint b, float t)
		{
			return TerrainCache.Lerp(a, b, t);
		}

		private static int Luma(uint c)
		{
			return (int)((c & 0xFF) * 3 + ((c >> 8) & 0xFF) * 6 + ((c >> 16) & 0xFF));
		}

		internal static float Value(uint c)
		{
			return (float)Math.Max(c & 0xFF, Math.Max((c >> 8) & 0xFF, (c >> 16) & 0xFF)) / 255f;
		}

		internal static float Saturation(uint c)
		{
			float val = (float)(c & 0xFF) / 255f;
			float val2 = (float)((c >> 8) & 0xFF) / 255f;
			float val3 = (float)((c >> 16) & 0xFF) / 255f;
			float num = Math.Max(val, Math.Max(val2, val3));
			float num2 = Math.Min(val, Math.Min(val2, val3));
			if (!(num <= 0f))
			{
				return (num - num2) / num;
			}
			return 0f;
		}

		internal static float Hue(uint c)
		{
			float num = (float)(c & 0xFF) / 255f;
			float num2 = (float)((c >> 8) & 0xFF) / 255f;
			float num3 = (float)((c >> 16) & 0xFF) / 255f;
			float num4 = Math.Max(num, Math.Max(num2, num3));
			float num5 = Math.Min(num, Math.Min(num2, num3));
			float num6 = num4 - num5;
			if (num6 < 1E-05f)
			{
				return 0f;
			}
			float num7 = ((num4 == num) ? ((num2 - num3) / num6) : ((num4 == num2) ? (2f + (num3 - num) / num6) : (4f + (num - num2) / num6)));
			num7 /= 6f;
			if (!(num7 < 0f))
			{
				return num7;
			}
			return num7 + 1f;
		}

		private static uint FromHsv(float h, float s, float v)
		{
			h = (h % 1f + 1f) % 1f;
			s = Math.Max(0f, Math.Min(1f, s));
			v = Math.Max(0f, Math.Min(1f, v));
			float num = h * 6f;
			int num2 = (int)num;
			float num3 = num - (float)num2;
			float num4 = v * (1f - s);
			float num5 = v * (1f - s * num3);
			float num6 = v * (1f - s * (1f - num3));
			float num7;
			float num8;
			float num9;
			switch (num2 % 6)
			{
			case 0:
				num7 = v;
				num8 = num6;
				num9 = num4;
				break;
			case 1:
				num7 = num5;
				num8 = v;
				num9 = num4;
				break;
			case 2:
				num7 = num4;
				num8 = v;
				num9 = num6;
				break;
			case 3:
				num7 = num4;
				num8 = num5;
				num9 = v;
				break;
			case 4:
				num7 = num6;
				num8 = num4;
				num9 = v;
				break;
			default:
				num7 = v;
				num8 = num4;
				num9 = num5;
				break;
			}
			return Rgb((int)(num7 * 255f + 0.5f), (int)(num8 * 255f + 0.5f), (int)(num9 * 255f + 0.5f));
		}

		private static uint Toward(uint c, uint target, float hueK, float satK)
		{
			float num = Hue(c);
			float num2 = Hue(target) - num;
			if (num2 > 0.5f)
			{
				num2 -= 1f;
			}
			if (num2 < -0.5f)
			{
				num2 += 1f;
			}
			float s = Saturation(c) + (Saturation(target) - Saturation(c)) * satK;
			return FromHsv(num + num2 * hueK, s, Value(c));
		}

		internal static uint Desaturate(uint c, float k)
		{
			return FromHsv(Hue(c), Saturation(c) * (1f - k), Value(c));
		}

		public static uint[] Rock(MeshModel model, SpriteImage img, uint ground, string biome)
		{
			uint[] array = Lump(model, img, ground);
			if (!Has(model, "stone"))
			{
				return array;
			}
			uint num = 0u;
			for (int i = 0; i < array.Length; i++)
			{
				if (model.SlotNames[i] == "stone")
				{
					num = array[i];
				}
			}
			bool flag = Has(model, "bone");
			uint num2 = num;
			if (BiomeStone(biome, out var stone, out var share))
			{
				num2 = Lerp(num, stone, flag ? (share * 0.5f) : share);
			}
			Fill(model, array, "stone", num2);
			Fill(model, array, "stone_light", Lerp(num2, Rgb(236, 232, 222), 0.2f));
			Fill(model, array, "stone_dark", TerrainCache.Scale(num2, 0.7f));
			Fill(model, array, "fresh", Lerp(num2, Rgb(226, 222, 212), 0.3f));
			Fill(model, array, "crack", TerrainCache.Scale(num2, 0.34f));
			Fill(model, array, "lichen", Lichen(biome, num2));
			return array;
		}

		public static uint[] Boulders(MeshModel model, uint[] pal, string biome)
		{
			if (pal == null || !Has(model, "boulder") || !BiomeStone(biome, out var stone, out var share))
			{
				return null;
			}
			uint[] array = (uint[])pal.Clone();
			uint num = 0u;
			for (int i = 0; i < array.Length && i < model.SlotNames.Length; i++)
			{
				if (model.SlotNames[i] == "boulder")
				{
					num = Lerp(array[i], stone, share);
				}
			}
			Fill(model, array, "boulder", num);
			Fill(model, array, "boulder_light", Lerp(num, Rgb(236, 232, 222), 0.22f));
			Fill(model, array, "boulder_dark", TerrainCache.Scale(num, 0.7f));
			return array;
		}

		private static bool BiomeStone(string biome, out uint stone, out float share)
		{
			share = 0.7f;
			string text = biome ?? "";
			if (text.Length == 0)
			{
				stone = 0u;
				return false;
			}
			if (text.Contains("desert"))
			{
				stone = Rgb(196, 166, 122);
			}
			else if (text.Contains("savanna"))
			{
				stone = Rgb(178, 150, 112);
			}
			else if (text.Contains("infernal"))
			{
				stone = Rgb(70, 60, 58);
			}
			else if (text.Contains("permafrost") || text.Contains("snow"))
			{
				stone = Rgb(176, 184, 194);
			}
			else if (text.Contains("corrupt"))
			{
				stone = Rgb(114, 98, 118);
			}
			else if (text.Contains("singularity"))
			{
				stone = Rgb(88, 74, 102);
			}
			else if (text.Contains("swamp"))
			{
				stone = Rgb(96, 96, 84);
			}
			else if (text.Contains("jungle"))
			{
				stone = Rgb(104, 110, 98);
			}
			else if (text.Contains("crystal"))
			{
				stone = Rgb(156, 174, 186);
			}
			else if (text.Contains("candy"))
			{
				stone = Rgb(216, 194, 204);
			}
			else if (text.Contains("mushroom"))
			{
				stone = Rgb(122, 110, 106);
			}
			else if (text.Contains("wasteland"))
			{
				stone = Rgb(126, 116, 100);
			}
			else if (text.Contains("celestial"))
			{
				stone = Rgb(154, 144, 174);
			}
			else if (text.Contains("paradox"))
			{
				stone = Rgb(132, 108, 92);
			}
			else if (text.Contains("rockland"))
			{
				stone = Rgb(138, 136, 132);
			}
			else
			{
				stone = Rgb(150, 148, 142);
				share = 0.55f;
			}
			return true;
		}

		private static uint Lichen(string biome, uint stone)
		{
			string text = biome ?? "";
			if (!text.Contains("desert") && !text.Contains("infernal") && !text.Contains("permafrost") && !text.Contains("snow") && !text.Contains("wasteland") && !text.Contains("corrupt") && !text.Contains("singularity") && !text.Contains("candy") && !text.Contains("crystal"))
			{
				return Lerp(stone, Rgb(176, 182, 104), 0.7f);
			}
			return Lerp(stone, Rgb(236, 232, 222), 0.1f);
		}
	}
}
