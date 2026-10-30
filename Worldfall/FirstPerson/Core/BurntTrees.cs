using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class BurntTrees
	{
		private static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
		{
			{ "tree_broadleaf_b", "tree_broadleaf" },
			{ "tree_baobab_b", "tree_baobab" },
			{ "tree_pine", "tree_fir_snow" },
			{ "tree_acacia", "tree_savanna" },
			{ "tree_cactus", "tree_saguaro" },
			{ "tree_mushroom", "tree_amanita" },
			{ "tree_willow", "tree_swamp" },
			{ "tree_birch", "tree_birch_b" },
			{ "tree_gnarled", "tree_wasteland" }
		};

		public const string Fallback = "burnt_tree_round";

		private static readonly Dictionary<MeshModel, Dictionary<string, string>> Overrides = new Dictionary<MeshModel, Dictionary<string, string>>();

		private const float Lift = 1.35f;

		private static readonly uint Dull = TerrainCache.Rgb(92, 18, 8);

		private const int HeatSteps = 24;

		private static readonly Dictionary<MeshModel, uint[][]> EmberPalettes = new Dictionary<MeshModel, uint[][]>();

		public static string FamilyOf(string living)
		{
			string value;
			if (living != null)
			{
				return "burnt_" + (Alias.TryGetValue(living, out value) ? value : living);
			}
			return null;
		}

		public static bool Handles(MeshModel model)
		{
			if (model != null)
			{
				return model.Info("palette") == "burnt";
			}
			return false;
		}

		public static uint[] Colors(MeshModel model, SpriteImage img)
		{
			uint[] array = new uint[model.SlotNames.Length];
			if (img == null || img.Pixels == null)
			{
				return array;
			}
			Tones(img, out var dark, out var mid, out var light);
			uint num = Ash(mid, light);
			Dictionary<string, string> dictionary = ToneOverrides(model);
			for (int i = 0; i < array.Length; i++)
			{
				if ((model.SlotFlags[i] & 1) == 0)
				{
					string text = model.SlotNames[i];
					if (!dictionary.TryGetValue(text, out var value))
					{
						value = ((text == "ash") ? "ash" : (text.EndsWith("_dark", StringComparison.Ordinal) ? "dark" : (text.EndsWith("_light", StringComparison.Ordinal) ? "light" : "mid")));
					}
					switch (value)
					{
					case "dark":
						array[i] = dark;
						break;
					case "light":
						array[i] = light;
						break;
					case "ash":
						array[i] = num;
						break;
					default:
						array[i] = mid;
						break;
					case "keep":
						break;
					}
				}
			}
			return array;
		}

		private static Dictionary<string, string> ToneOverrides(MeshModel model)
		{
			lock (Overrides)
			{
				if (Overrides.TryGetValue(model, out var value))
				{
					return value;
				}
				value = new Dictionary<string, string>();
				string text = model.Info("tones");
				if (!string.IsNullOrEmpty(text))
				{
					string[] array = text.Split(',');
					foreach (string text2 in array)
					{
						int num = text2.IndexOf(':');
						if (num > 0)
						{
							value[text2.Substring(0, num).Trim()] = text2.Substring(num + 1).Trim();
						}
					}
				}
				return Overrides[model] = value;
			}
		}

		public static void Tones(SpriteImage img, out uint dark, out uint mid, out uint light)
		{
			List<uint> list = new List<uint>();
			for (int i = 0; i < img.Pixels.Length; i++)
			{
				uint num = img.Pixels[i];
				if (num >> 24 >= 128)
				{
					list.Add(num | 0xFF000000u);
				}
			}
			if (list.Count == 0)
			{
				dark = TerrainCache.Rgb(30, 27, 25);
				mid = TerrainCache.Rgb(54, 48, 44);
				light = TerrainCache.Rgb(88, 82, 76);
				return;
			}
			list.Sort((uint a, uint b) => Luma(a).CompareTo(Luma(b)));
			uint c = list[list.Count * 12 / 100];
			uint c2 = list[list.Count / 2];
			uint c3 = list[Math.Min(list.Count - 1, list.Count * 90 / 100)];
			mid = Brighter(c2, 1.35f, 8);
			dark = TerrainCache.Lerp(mid, Brighter(c, 1.35f, 4), 0.7f);
			light = TerrainCache.Lerp(mid, Brighter(c3, 1.35f, 10), 0.75f);
			if (Luma(mid) - Luma(dark) < 90)
			{
				dark = TerrainCache.Scale(mid, 0.72f);
			}
			if (Luma(light) - Luma(mid) < 120)
			{
				light = Brighter(mid, 1.3f, 16);
			}
		}

		private static uint Ash(uint mid, uint light)
		{
			uint c = TerrainCache.Lerp(mid, light, 0.5f);
			int num = Math.Min(255, (int)((float)Luma(c) / 10f * 1.45f) + 14);
			return TerrainCache.Lerp(TerrainCache.Rgb(Math.Min(255, num + 3), num, Math.Max(0, num - 3)), Brighter(c, 1.4f, 10), 0.2f);
		}

		private static uint Brighter(uint c, float k, int add)
		{
			int num = (int)(c & 0xFF);
			int num2 = (int)((c >> 8) & 0xFF);
			int num3 = (int)((c >> 16) & 0xFF);
			return TerrainCache.Rgb(Math.Min(255, (int)((float)num * k) + add), Math.Min(255, (int)((float)num2 * k) + add), Math.Min(255, (int)((float)num3 * k) + add));
		}

		private static int Luma(uint c)
		{
			return (int)((c & 0xFF) * 3 + ((c >> 8) & 0xFF) * 6 + ((c >> 16) & 0xFF));
		}

		public static uint[] Embers(MeshModel embers, float heat)
		{
			int num = Math.Max(0, Math.Min(24, (int)Math.Round(heat * 24f)));
			lock (EmberPalettes)
			{
				if (!EmberPalettes.TryGetValue(embers, out var value))
				{
					value = (EmberPalettes[embers] = new uint[25][]);
				}
				if (value[num] != null)
				{
					return value[num];
				}
				float num2 = (float)num / 24f;
				uint[] array2 = new uint[embers.SlotNames.Length];
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i] = TerrainCache.Lerp(Dull, embers.SlotColor[i] | 0xFF000000u, num2 * num2 * (3f - 2f * num2)) | 0xFF000000u;
				}
				return value[num] = array2;
			}
		}
	}
}
