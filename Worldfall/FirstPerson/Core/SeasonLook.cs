using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class SeasonLook
	{
		private sealed class ReferenceComparer : IEqualityComparer<uint[]>
		{
			public static readonly ReferenceComparer Instance = new ReferenceComparer();

			public bool Equals(uint[] a, uint[] b)
			{
				return a == b;
			}

			public int GetHashCode(uint[] a)
			{
				return RuntimeHelpers.GetHashCode(a);
			}
		}

		public static float Spring;

		public static float Summer;

		public static float Autumn;

		public static float Winter;

		private const int Steps = 24;

		private static readonly uint WinterStraw = TerrainCache.Rgb(150, 140, 102);

		private static readonly uint WinterDun = TerrainCache.Rgb(116, 110, 86);

		private static readonly uint WinterMud = TerrainCache.Rgb(112, 94, 72);

		private static readonly uint WinterStone = TerrainCache.Rgb(124, 128, 136);

		private static readonly uint WinterLeaf = TerrainCache.Rgb(112, 92, 70);

		private static readonly uint AutumnGold = TerrainCache.Rgb(196, 160, 72);

		private static readonly uint AutumnStraw = TerrainCache.Rgb(214, 186, 110);

		private static readonly uint[] AutumnLeaves = new uint[5]
		{
			TerrainCache.Rgb(200, 72, 40),
			TerrainCache.Rgb(214, 118, 44),
			TerrainCache.Rgb(222, 170, 54),
			TerrainCache.Rgb(168, 92, 46),
			TerrainCache.Rgb(186, 46, 38)
		};

		private static readonly uint[] Blossoms = new uint[3]
		{
			TerrainCache.Rgb(246, 196, 214),
			TerrainCache.Rgb(248, 240, 236),
			TerrainCache.Rgb(236, 168, 200)
		};

		private static readonly string[] Evergreen = new string[30]
		{
			"fir", "pine", "savanna", "baobab", "jungle", "date_palm", "palm", "saguaro", "cact", "crystal",
			"enchanted", "corrupted", "infernal", "candy", "amanita", "mushroom", "wasteland", "celestial", "singularity", "paradox",
			"garlic", "sunflower", "poppy", "clover", "gnarled", "acacia", "permafrost", "desert", "swamp", "rocklands"
		};

		private static readonly Dictionary<MeshModel, int[]> ModelFacts = new Dictionary<MeshModel, int[]>();

		private static readonly Dictionary<uint[], uint[][]> Leaves = new Dictionary<uint[], uint[][]>(ReferenceComparer.Instance);

		public static int Stamp { get; private set; }

		public static void Set(float spring, float summer, float autumn, float winter)
		{
			Spring = (float)Q(spring) / 23f;
			Summer = (float)Q(summer) / 23f;
			Autumn = (float)Q(autumn) / 23f;
			Winter = (float)Q(winter) / 23f;
			int num = Q(Spring) + Q(Summer) * 24 + Q(Autumn) * 24 * 24 + Q(Winter) * 24 * 24 * 24 + 1;
			if (num != Stamp)
			{
				Stamp = num;
				Leaves.Clear();
			}
		}

		private static int Q(float v)
		{
			return (int)Math.Round(Clamp01(v) * 23f);
		}

		public static uint Ground(uint c, byte material, uint hash)
		{
			if (Stamp == 0)
			{
				return c;
			}
			float num = (float)(hash & 0xFF) / 255f;
			switch (material)
			{
			case 0:
			case 1:
			case 3:
				if (Summer > 0f)
				{
					c = Richer(c, 0.08f * Summer);
				}
				if (Spring > 0f)
				{
					c = TerrainCache.Lerp(c, Fresh(c), 0.25f * Spring);
				}
				if (Autumn > 0f)
				{
					c = TerrainCache.Lerp(c, AutumnGold, (0.22f + 0.16f * num) * Autumn);
				}
				if (Winter > 0f)
				{
					c = TerrainCache.Lerp(c, (num < 0.5f) ? WinterStraw : WinterDun, (0.5f + 0.25f * num) * Winter);
				}
				return c;
			case 7:
				if (Autumn > 0f)
				{
					c = TerrainCache.Lerp(c, AutumnStraw, 0.55f * Autumn);
				}
				if (Winter > 0f)
				{
					c = TerrainCache.Lerp(c, WinterMud, 0.65f * Winter);
				}
				return c;
			case 6:
			case 9:
				if (Winter > 0f)
				{
					c = TerrainCache.Scale(c, 1f - 0.12f * Winter);
				}
				return c;
			case 4:
				if (Winter > 0f)
				{
					c = TerrainCache.Lerp(c, WinterStone, 0.15f * Winter);
				}
				return c;
			default:
				return c;
			}
		}

		public static bool Deciduous(string modelName)
		{
			if (string.IsNullOrEmpty(modelName))
			{
				return false;
			}
			if (modelName.StartsWith("tree_", StringComparison.Ordinal) || modelName.StartsWith("bush_", StringComparison.Ordinal))
			{
				string text = modelName.Substring(5);
				string[] evergreen = Evergreen;
				foreach (string value in evergreen)
				{
					if (text.StartsWith(value, StringComparison.Ordinal))
					{
						return false;
					}
				}
				return true;
			}
			if (!modelName.StartsWith("tree", StringComparison.Ordinal))
			{
				return modelName.StartsWith("bush", StringComparison.Ordinal);
			}
			return true;
		}

		private static int[] Facts(MeshModel model)
		{
			if (ModelFacts.TryGetValue(model, out var value))
			{
				return value;
			}
			value = new int[8]
			{
				Deciduous(model.Name) ? 1 : 0,
				model.SlotIndex("leaf_dark"),
				model.SlotIndex("leaf_mid"),
				model.SlotIndex("leaf_light"),
				model.SlotIndex("leaf_dark_s"),
				model.SlotIndex("leaf_mid_s"),
				model.SlotIndex("leaf_light_s"),
				model.SlotIndex("leaf_core")
			};
			if (ModelFacts.Count > 4096)
			{
				ModelFacts.Clear();
			}
			ModelFacts[model] = value;
			return value;
		}

		public static uint[] Tree(MeshModel model, uint[] pal, uint variety)
		{
			if (Stamp == 0 || pal == null || model == null)
			{
				return pal;
			}
			int[] array = Facts(model);
			bool deciduous = array[0] == 1;
			if (!deciduous && Winter <= 0f)
			{
				return pal;
			}
			int num = array[1];
			int num2 = array[2];
			int num3 = array[3];
			if (num < 0 && num2 < 0 && num3 < 0)
			{
				return pal;
			}
			bool crowned = num2 >= 0 && num2 < model.SlotFlags.Length && (model.SlotFlags[num2] & 0x40) != 0;
			variety %= 8;
			if (!Leaves.TryGetValue(pal, out var value))
			{
				value = (Leaves[pal] = new uint[8][]);
			}
			if (value[variety] != null)
			{
				return value[variety];
			}
			uint[] res = (uint[])pal.Clone();
			uint autumn = AutumnLeaves[variety % (uint)AutumnLeaves.Length];
			bool blossoms = variety < 3;
			uint bloom = Blossoms[variety % (uint)Blossoms.Length];
			Shade(num, 0f);
			Shade(num2, 0.5f);
			Shade(num3, 1f);
			for (int i = 0; i < 3; i++)
			{
				int num4 = array[4 + i];
				int num5 = array[1 + i];
				if (num4 >= 0 && num4 < res.Length && num5 >= 0 && num5 < res.Length)
				{
					res[num4] = res[num5];
				}
			}
			if (array[7] >= 0 && array[7] < res.Length && num >= 0 && num < res.Length)
			{
				res[array[7]] = TerrainCache.Scale(res[num], 0.86f) | 0xFF000000u;
			}
			if (Leaves.Count > 1024)
			{
				Leaves.Clear();
				Leaves[pal] = value;
			}
			value[variety] = res;
			return res;
			void Shade(int slot, float lift)
			{
				if (slot >= 0 && slot < res.Length)
				{
					uint num6 = ((res[slot] != 0) ? res[slot] : model.SlotColor[slot]);
					if (deciduous)
					{
						if (Summer > 0f)
						{
							num6 = Richer(num6, 0.1f * Summer);
						}
						if (Autumn > 0f)
						{
							num6 = TerrainCache.Lerp(num6, TerrainCache.Scale(autumn, 0.82f + 0.3f * lift), 0.85f * Autumn);
						}
						if (Spring > 0f)
						{
							num6 = ((blossoms && lift > 0.5f) ? TerrainCache.Lerp(num6, bloom, (crowned ? 0.32f : 0.8f) * Spring) : TerrainCache.Lerp(num6, Fresh(num6), 0.35f * Spring));
						}
					}
					if (Winter > 0f)
					{
						num6 = Wintry(num6, deciduous, crowned, lift);
					}
					res[slot] = num6 | 0xFF000000u;
				}
			}
		}

		public static uint Far(uint c, bool tree, bool deciduous, bool top, uint hash)
		{
			if (Stamp == 0)
			{
				return c;
			}
			float num = c & 0xFF;
			float num2 = (c >> 8) & 0xFF;
			float num3 = (c >> 16) & 0xFF;
			bool flag = tree && num2 > num && num2 >= num3;
			if (flag && deciduous && Autumn > 0f)
			{
				float num4 = Math.Min(1f, (num + num2 + num3) / 450f);
				c = TerrainCache.Lerp(c, TerrainCache.Scale(AutumnLeaves[hash % (uint)AutumnLeaves.Length], 0.82f + 0.3f * num4), 0.85f * Autumn);
			}
			if (Winter > 0f && flag)
			{
				c = Wintry(c, deciduous, crowned: false, Math.Min(1f, (num + num2 + num3) / 450f));
			}
			return c | 0xFF000000u;
		}

		private static uint Wintry(uint c, bool deciduous, bool crowned, float lift)
		{
			if (crowned)
			{
				return TerrainCache.Lerp(c, TerrainCache.Scale(NaturePalette.Desaturate(c, deciduous ? 0.5f : 0.3f), deciduous ? 0.8f : 0.86f), Winter);
			}
			if (deciduous)
			{
				return TerrainCache.Lerp(c, TerrainCache.Scale(WinterLeaf, 0.85f + 0.25f * lift), 0.7f * Winter);
			}
			return TerrainCache.Lerp(c, TerrainCache.Scale(NaturePalette.Desaturate(c, 0.3f), 0.84f), 0.7f * Winter);
		}

		public static void Light(Atmosphere a)
		{
			if (Stamp != 0 && a != null)
			{
				float winter = Winter;
				float autumn = Autumn;
				float summer = Summer;
				a.LightR *= 1f - 0.16f * winter + 0.06f * autumn + 0.04f * summer;
				a.LightG *= 1f - 0.1f * winter + 0.01f * autumn + 0.03f * summer;
				a.LightB *= 1f - 0.02f * winter - 0.08f * autumn;
				a.Clouds = Math.Min(1f, a.Clouds + 0.25f * winter + 0.08f * autumn);
				a.HazeDensity *= 1f + 0.4f * winter + 0.15f * autumn;
				a.Horizon = TerrainCache.Lerp(a.Horizon, TerrainCache.Rgb(196, 204, 214), 0.3f * winter);
				a.Zenith = TerrainCache.Lerp(a.Zenith, TerrainCache.Rgb(120, 140, 170), 0.25f * winter);
			}
		}

		private static uint Richer(uint c, float k)
		{
			float num = c & 0xFF;
			float num2 = (c >> 8) & 0xFF;
			float num3 = (c >> 16) & 0xFF;
			float num4 = (num + num2 + num3) / 3f;
			num = Math.Max(0f, Math.Min(255f, num + (num - num4) * k * 2f - 4f * k));
			num2 = Math.Max(0f, Math.Min(255f, num2 + (num2 - num4) * k * 2f));
			num3 = Math.Max(0f, Math.Min(255f, num3 + (num3 - num4) * k * 2f - 4f * k));
			return (uint)num | ((uint)num2 << 8) | ((uint)num3 << 16) | (c & 0xFF000000u);
		}

		private static uint Fresh(uint c)
		{
			float num = c & 0xFF;
			float num2 = (c >> 8) & 0xFF;
			float num3 = (c >> 16) & 0xFF;
			return TerrainCache.Rgb((int)Math.Min(255f, num * 0.9f + 20f), (int)Math.Min(255f, num2 * 1.08f + 18f), (int)Math.Min(255f, num3 * 0.85f + 6f));
		}

		private static float Clamp01(float v)
		{
			if (!(v < 0f))
			{
				if (!(v > 1f))
				{
					return v;
				}
				return 1f;
			}
			return 0f;
		}
	}
}
