using System;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class TerrainModel
	{
		public struct TileInput
		{
			public int X;

			public int Y;

			public int HeightMin;

			public int BandTop;

			public int DataHeight;

			public bool Liquid;

			public bool Lava;

			public bool Pit;

			public float LandDepth;

			public float HillDepth;

			public float MountainDepth;

			public float VolcanoLift;

			public int Form;

			public float SummitDepth;

			public float RangeSize;

			public float Relief;

			public float Terraces;

			public float Dug;

			public float RollLess;
		}

		public const float WaterLevel = 0f;

		public const float LavaLevel = 0.08f;

		public const float WallHeight = 3.4f;

		public const float MerlonHeight = 0.45f;

		public const float MaxTerrainHeight = 100f;

		public const float SnowLine = 18f;

		public const float RollHeight = 2.4f;

		public const float HillSwell = 3.6f;

		public const float RangeSwell = 5.5f;

		private static readonly float[] GenHeights = new float[7] { 98f, 108f, 128f, 199f, 210f, 230f, 255f };

		private static readonly float[] RenderHeights = new float[7] { 0.1f, 0.28f, 0.65f, 1.8f, 2.6f, 5.6f, 8.5f };

		public static bool Shaped = true;

		public static float TerraceStep = 0.5f;

		public static float TerraceRiser = 0.35f;

		public static float TerraceDepth = 0.7f;

		public static bool PitsFollowLand = true;

		public static float OutOfPlace = 0.9f;

		public static float HeartReach = 5f;

		public static float KnotSize = 2.5f;

		public static float GreatSize = 11f;

		public static float Rise = 26f;

		public static float RiseReach = 11f;

		public static float KnotRise = 0.45f;

		public static float KnotRidges = 0.15f;

		public static float RidgeHeight = 8.5f;

		public static float HeartLift = 3.3f;

		public static float SummitLift = 5f;

		public static float RidgePower = 1.25f;

		public static float Steep = 2.1f;

		public static float FootRun = 1f;

		public static float KnotSteep = 0.6f;

		public const float VolcanoRadius = 11f;

		public const float VolcanoHeight = 28f;

		public const float CraterRadius = 2.2f;

		public const float CraterDepth = 4.5f;

		public static float MapHeight(float v)
		{
			if (v <= GenHeights[0])
			{
				return (v - GenHeights[0]) * 0.022f;
			}
			for (int i = 1; i < GenHeights.Length; i++)
			{
				if (v <= GenHeights[i])
				{
					float num = (v - GenHeights[i - 1]) / (GenHeights[i] - GenHeights[i - 1]);
					return RenderHeights[i - 1] + (RenderHeights[i] - RenderHeights[i - 1]) * num;
				}
			}
			return RenderHeights[RenderHeights.Length - 1];
		}

		public static float NoiseAmplitude(int heightMin)
		{
			if (heightMin >= 230)
			{
				return 3.4f;
			}
			if (heightMin >= 210)
			{
				return 2.6f;
			}
			if (heightMin >= 199)
			{
				return 0.7f;
			}
			if (heightMin >= 128)
			{
				return 0.2f;
			}
			if (heightMin >= 98)
			{
				return 0.06f;
			}
			return 0f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ClassOf(int heightMin)
		{
			if (heightMin < 210)
			{
				if (heightMin < 199)
				{
					return 1;
				}
				return 2;
			}
			return 3;
		}

		public static float ComputeHeight(in TileInput t)
		{
			if (t.Lava || t.Liquid)
			{
				return NaturalHeight(in t);
			}
			float num = NaturalHeight(in t);
			if (t.Terraces > 0f)
			{
				num = Terraced(num, in t);
			}
			return num + t.Dug;
		}

		private static float Terraced(float h, in TileInput t)
		{
			int num = ClassOf(t.HeightMin);
			if (((t.Form > 0) ? t.Form : num) != 2 || t.HeightMin < 98 || t.Pit)
			{
				return h;
			}
			float num2 = t.Terraces * TerraceDepth * Smooth01(0.5f, 2.5f, t.HillDepth);
			if (num2 <= 0f)
			{
				return h;
			}
			float num3 = h / TerraceStep;
			float num4 = (float)Math.Floor(num3);
			float num5 = (num4 + Smooth01(1f - TerraceRiser, 1f, num3 - num4)) * TerraceStep;
			return h + (num5 - h) * num2;
		}

		private static float NaturalHeight(in TileInput t)
		{
			if (t.Lava)
			{
				if (!Shaped)
				{
					return 0.08f + t.VolcanoLift;
				}
				if (!t.Liquid)
				{
					TileInput t2 = t;
					t2.Lava = false;
					return ComputeHeight(in t2) - 0.05f;
				}
				return LavaOnLand(in t) + t.VolcanoLift;
			}
			if (t.Liquid)
			{
				return 0f;
			}
			if (Shaped)
			{
				return ShapedHeight(in t) + t.VolcanoLift + t.Relief;
			}
			float num;
			if (t.HeightMin < 98)
			{
				num = (t.Pit ? PitHeight(in t) : 0.03f);
			}
			else
			{
				int val = Math.Max(t.HeightMin, t.BandTop);
				float num2 = MapHeight(Math.Min(Math.Max(t.DataHeight, t.HeightMin), val));
				if (t.HeightMin >= 199 && t.HeightMin < 210 && t.DataHeight <= t.HeightMin)
				{
					float num3 = MapHeight(128f);
					num2 = num3 + (num2 - num3) * Math.Min(1f, Math.Max(0f, t.HillDepth - 0.5f) / 3f);
				}
				num = num2 + Fbm(t.X, t.Y) * NoiseAmplitude(t.HeightMin) + Rolling(in t) * (1f + 0.5f * RangeHeart(in t)) + Massif(in t);
			}
			return num + t.VolcanoLift + t.Relief;
		}

		private static float PitHeight(in TileInput t)
		{
			if (!PitsFollowLand)
			{
				return MapHeight(t.HeightMin);
			}
			return MapHeight(t.HeightMin) + Rolling(in t);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Smooth01(float a, float b, float v)
		{
			float num = (v - a) / (b - a);
			num = ((num < 0f) ? 0f : ((num > 1f) ? 1f : num));
			return num * num * (3f - 2f * num);
		}

		private static float ShapedHeight(in TileInput t)
		{
			int num = ClassOf(t.HeightMin);
			int num2 = ((t.Form > 0) ? t.Form : num);
			float num3 = ((num2 >= 3) ? Math.Max(0f, t.MountainDepth - 0.3f) : 0f);
			float num4 = Math.Min(1f, num3 / HeartReach);
			float num5 = Rolling(in t) * (1f + 0.5f * num4) + HillSwellOf(in t, num2, num4);
			float num6 = ((num2 >= 3) ? RangeLift(in t, num3, num4) : 0f);
			if (t.HeightMin < 98)
			{
				if (!t.Pit)
				{
					return 0.03f;
				}
				num5 += MapHeight(t.HeightMin);
			}
			else if (num == 3)
			{
				float num7 = MapHeight(Math.Min(Math.Max(t.DataHeight, 210), Math.Max(230, t.BandTop)));
				float num8 = Math.Min(1f, num3 / 2.5f);
				float num9 = 1.8999999f * num8;
				if (t.HeightMin >= 230)
				{
					float num10 = Smooth01(0.4f, 3f, t.SummitDepth);
					num7 += Math.Max(0f, MapHeight(230f) - num7) * num10;
					num9 += 0.8000002f * num10 * num8;
				}
				float num11 = Fbm(t.X, t.Y);
				num5 += MapHeight(210f) + num11 * (0.7f + num9);
				num6 += num7 - MapHeight(210f);
			}
			else
			{
				int val = Math.Max(t.HeightMin, t.BandTop);
				float num12 = MapHeight(Math.Min(Math.Max(t.DataHeight, t.HeightMin), val));
				float num13 = NoiseAmplitude(t.HeightMin);
				if (num == 2)
				{
					float num14 = Math.Min(1f, Math.Max(0f, t.HillDepth - 0.5f) / 3f);
					if (t.DataHeight <= t.HeightMin)
					{
						float num15 = MapHeight(128f);
						num12 = num15 + (num12 - num15) * num14;
					}
					num13 = 0.2f + (num13 - 0.2f) * Math.Min(1f, 0.3f + num14);
				}
				num5 += num12 + Fbm(t.X, t.Y) * num13;
			}
			if (num > num2)
			{
				num6 += (float)(num - num2) * OutOfPlace;
			}
			else if (num < num2 && t.HeightMin >= 98)
			{
				num5 -= (float)(num2 - num) * OutOfPlace;
			}
			float num16 = ((t.RangeSize > 0f) ? Smooth01(KnotSize, GreatSize, t.RangeSize) : 1f);
			float num17 = Steep * (KnotSteep + (1f - KnotSteep) * num16) * (num3 + FootRun);
			num5 += num17 * (1f - (float)Math.Exp((0f - num6) / num17));
			if (num2 >= 3)
			{
				num5 += RidgeHeight * Ridged(t.X, t.Y) * (KnotRidges + (1f - KnotRidges) * num16) * (float)Math.Pow(num4, RidgePower);
			}
			return num5;
		}

		private static float HillSwellOf(in TileInput t, int form, float inside)
		{
			if (form < 2)
			{
				return 0f;
			}
			float num = Math.Max(0f, t.HillDepth - 0.5f);
			return (3.6f + 1.9000001f * inside) * (1f - (float)Math.Exp((0f - num) / 5f));
		}

		private static float RangeLift(in TileInput t, float d, float inside)
		{
			float num = ((t.RangeSize > 0f) ? Smooth01(KnotSize, GreatSize, t.RangeSize) : 1f);
			float num2 = Rise * (1f - (float)Math.Exp((0f - d) / RiseReach)) * (KnotRise + (1f - KnotRise) * num);
			num2 += HeartLift * inside;
			if (t.HeightMin >= 230)
			{
				num2 += SummitLift * inside * num * Smooth01(0.4f, 3f, t.SummitDepth);
			}
			return num2;
		}

		private static float LavaOnLand(in TileInput t)
		{
			float val = MapHeight((t.DataHeight >= 98) ? Math.Min(t.DataHeight, 198) : 108) + Rolling(in t) - 0.06f;
			float num = Math.Min(1f, t.LandDepth / 2f);
			return 0.08f + (Math.Max(0.08f, val) - 0.08f) * num;
		}

		private static float Rolling(in TileInput t)
		{
			if (t.LandDepth <= 0f)
			{
				return 0f;
			}
			float num = ValueNoise((float)t.X / 34f + 11.3f, (float)t.Y / 34f - 7.7f) * 0.68f + ValueNoise((float)t.X / 13f - 3.1f, (float)t.Y / 13f + 5.9f) * 0.32f;
			num = num * num * (3f - 2f * num);
			float num2 = 1f - (float)Math.Exp((0f - t.LandDepth) / 9f);
			return 2.4f * num * num2 * (1f - t.RollLess);
		}

		private static float Massif(in TileInput t)
		{
			if (t.HeightMin < 199)
			{
				return 0f;
			}
			float num = Math.Max(0f, t.HillDepth - 0.5f);
			float num2 = RangeHeart(in t);
			float num3 = (3.6f + 1.9000001f * num2) * (1f - (float)Math.Exp((0f - num) / 5f));
			if (t.HeightMin < 210)
			{
				return num3;
			}
			float num4 = Math.Max(0f, t.MountainDepth - 0.3f);
			num3 += 26f * (1f - (float)Math.Exp((0f - num4) / 11f)) + 8f * Ridged(t.X, t.Y) * num2 + 3.3f * num2;
			if (t.HeightMin >= 230)
			{
				num3 += 5f * num2;
			}
			return num3;
		}

		private static float RangeHeart(in TileInput t)
		{
			if (t.HeightMin >= 210)
			{
				return Math.Min(1f, Math.Max(0f, t.MountainDepth - 0.3f) / 3f);
			}
			return 0f;
		}

		public static void Depths(byte[] codes, int w, int h, float[] landDepth, float[] hillDepth, float[] mountainDepth)
		{
			Chamfer(codes, w, h, 1, landDepth);
			Chamfer(codes, w, h, 2, hillDepth);
			Chamfer(codes, w, h, 3, mountainDepth);
			float[] scratch = new float[w * h];
			for (int i = 0; i < 2; i++)
			{
				Smooth(mountainDepth, codes, 3, w, h, scratch);
				Smooth(hillDepth, codes, 2, w, h, scratch);
				Smooth(landDepth, codes, 1, w, h, scratch);
			}
		}

		private static void Smooth(float[] d, byte[] codes, byte minCode, int w, int h, float[] scratch)
		{
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					int num = i * w + j;
					if (codes[num] < minCode)
					{
						scratch[num] = 0f;
						continue;
					}
					float num2 = 0f;
					int num3 = 0;
					for (int k = -1; k <= 1; k++)
					{
						int num4 = i + k;
						if (num4 < 0 || num4 >= h)
						{
							continue;
						}
						for (int l = -1; l <= 1; l++)
						{
							int num5 = j + l;
							if (num5 >= 0 && num5 < w)
							{
								num2 += d[num4 * w + num5];
								num3++;
							}
						}
					}
					scratch[num] = Math.Max(num2 / (float)num3, d[num] * 0.6f);
				}
			}
			Array.Copy(scratch, d, d.Length);
		}

		private static void Chamfer(byte[] codes, int w, int h, byte minCode, float[] d)
		{
			int num = w * h;
			for (int i = 0; i < num; i++)
			{
				d[i] = ((codes[i] >= minCode) ? 64f : 0f);
			}
			for (int j = 0; j < h; j++)
			{
				int num2 = j * w;
				for (int k = 0; k < w; k++)
				{
					int num3 = num2 + k;
					float num4 = d[num3];
					if (num4 == 0f)
					{
						continue;
					}
					if (k > 0)
					{
						num4 = Math.Min(num4, d[num3 - 1] + 1f);
					}
					if (j > 0)
					{
						num4 = Math.Min(num4, d[num3 - w] + 1f);
						if (k > 0)
						{
							num4 = Math.Min(num4, d[num3 - w - 1] + 1.4142f);
						}
						if (k < w - 1)
						{
							num4 = Math.Min(num4, d[num3 - w + 1] + 1.4142f);
						}
					}
					d[num3] = num4;
				}
			}
			for (int num5 = h - 1; num5 >= 0; num5--)
			{
				int num6 = num5 * w;
				for (int num7 = w - 1; num7 >= 0; num7--)
				{
					int num8 = num6 + num7;
					float num9 = d[num8];
					if (num9 != 0f)
					{
						if (num7 < w - 1)
						{
							num9 = Math.Min(num9, d[num8 + 1] + 1f);
						}
						if (num5 < h - 1)
						{
							num9 = Math.Min(num9, d[num8 + w] + 1f);
							if (num7 < w - 1)
							{
								num9 = Math.Min(num9, d[num8 + w + 1] + 1.4142f);
							}
							if (num7 > 0)
							{
								num9 = Math.Min(num9, d[num8 + w - 1] + 1.4142f);
							}
						}
						d[num8] = num9;
					}
				}
			}
		}

		public static float VolcanoCone(float d, out bool crater)
		{
			crater = d < 1.8700001f;
			if (d >= 11f)
			{
				return 0f;
			}
			float num = 28f * (float)Math.Pow(0.800000011920929, 1.6);
			if (d < 2.2f)
			{
				float num2 = d / 2.2f;
				return num - 4.5f * (1f - num2 * num2);
			}
			return 28f * (float)Math.Pow(1f - d / 11f, 1.6);
		}

		public static bool LavaStream(float dx, float dy)
		{
			float num = (float)Math.Sqrt(dx * dx + dy * dy);
			if (num < 2.2f || num > 7.92f)
			{
				return false;
			}
			float num2 = (float)Math.Atan2(dy, dx) / (MathF.PI * 2f) + 0.5f;
			float num3 = (ValueNoise(num * 0.5f, num2 * 3f) - 0.5f) * 0.05f;
			float num4 = (num2 + num3) * 5f;
			float num5 = num4 - (float)Math.Floor(num4);
			float num6 = 0.07f * (1f - num / 7.92f);
			return Math.Abs(num5 - 0.5f) < num6;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Hash(int x, int y)
		{
			int num = x * 374761393 + y * 668265263;
			uint num2 = ((uint)num ^ ((uint)num >> 13)) * 1274126177;
			return num2 ^ (num2 >> 16);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Hash01(int x, int y)
		{
			return (float)(Hash(x, y) & 0xFFFF) / 65535f;
		}

		public static float ValueNoise(float x, float y)
		{
			int num = (int)Math.Floor(x);
			int num2 = (int)Math.Floor(y);
			float num3 = x - (float)num;
			float num4 = y - (float)num2;
			num3 = num3 * num3 * (3f - 2f * num3);
			num4 = num4 * num4 * (3f - 2f * num4);
			float num5 = Hash01(num, num2);
			float num6 = Hash01(num + 1, num2);
			float num7 = Hash01(num, num2 + 1);
			float num8 = Hash01(num + 1, num2 + 1);
			float num9 = num5 + (num6 - num5) * num3;
			float num10 = num7 + (num8 - num7) * num3;
			return num9 + (num10 - num9) * num4;
		}

		public static float Ridged(int x, int y)
		{
			float num = ValueNoise((float)x / 15f + 3.7f, (float)y / 15f - 1.3f);
			float num2 = ValueNoise((float)x / 6.5f - 8.1f, (float)y / 6.5f + 2.9f);
			float num3 = 1f - Math.Abs(2f * num - 1f);
			float num4 = 1f - Math.Abs(2f * num2 - 1f);
			return num3 * num3 * 0.72f + num4 * num4 * 0.28f;
		}

		public static float Fbm(int x, int y)
		{
			return ValueNoise((float)x / 9f, (float)y / 9f) * 0.6f + ValueNoise((float)x / 4f + 17.3f, (float)y / 4f - 5.1f) * 0.3f + Hash01(x, y) * 0.1f;
		}
	}
}
