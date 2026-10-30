using System;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class Strata
	{
		public const float Band = 0.3f;

		public const float FarBand = 1.2f;

		public const float From = 0.9f;

		public const float Full = 1.6f;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Share(float slope, bool marked)
		{
			float num = Smooth(0.9f, 1.6f, slope);
			if (marked)
			{
				num = Math.Max(num, Smooth(0.585f, 1.2f, slope));
			}
			return num;
		}

		public static float Shade(float z, float px, float py, float td)
		{
			float num = (CloudNoise.Sample(px * 0.9f + 3.1f, py * 0.9f - 7.3f) - 0.5f) * 0.5f;
			float num2 = (z + num) / 0.3f;
			int num3 = FastMath.FloorInt(num2);
			float num4 = num2 - (float)num3;
			float num5 = (float)(TerrainModel.Hash(num3, 4129) & 0xFF) / 255f;
			float num6 = ((num5 < 0.5f) ? (0.84f + num5 * 0.12f) : (1.03f + (num5 - 0.5f) * 0.18f));
			float num7 = 1f - Smooth(25f, 45f, td);
			if (num4 < 0.08f)
			{
				num6 *= 1f - 0.34f * num7;
			}
			float num8 = Smooth(35f, 70f, td);
			if (num8 > 0f)
			{
				float num9 = (((FastMath.FloorInt((z + num * 2f) / 1.2f) & 1) != 0) ? 0.86f : 1.08f);
				num6 += (num9 - num6) * num8;
			}
			return num6;
		}

		public static float SlopeAt(TerrainCache c, float px, float py, out bool marked)
		{
			marked = false;
			int width = c.Width;
			int height = c.Height;
			if (width == 0 || c.Marks.Length != c.Ground.Length)
			{
				return 0f;
			}
			float num = px - 0.5f;
			float num2 = py - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? ((num3 >= width) ? (width - 1) : num3) : 0);
			int num8 = ((num3 + 1 >= 0) ? ((num3 + 1 >= width) ? (width - 1) : (num3 + 1)) : 0);
			int num9 = ((num4 >= 0) ? ((num4 >= height) ? (height - 1) : num4) : 0);
			int num10 = ((num4 + 1 >= 0) ? ((num4 + 1 >= height) ? (height - 1) : (num4 + 1)) : 0);
			int num11 = num9 * width + num7;
			int num12 = num9 * width + num8;
			int num13 = num10 * width + num7;
			int num14 = num10 * width + num8;
			marked = ((c.Marks[num11] | c.Marks[num12] | c.Marks[num13] | c.Marks[num14]) & 2) != 0;
			float num15 = c.Ground[num11];
			float num16 = c.Ground[num12];
			float num17 = c.Ground[num13];
			float num18 = c.Ground[num14];
			float num19 = num16 - num15 + (num18 - num17 - (num16 - num15)) * num6;
			float num20 = num17 - num15 + (num18 - num16 - (num17 - num15)) * num5;
			return (float)Math.Sqrt(num19 * num19 + num20 * num20);
		}

		public static uint Apply(uint col, float k, float z, float px, float py, float td)
		{
			if (k <= 0f)
			{
				return col;
			}
			float s = 1f + (Shade(z, px, py, td) - 1f) * k;
			return TerrainCache.Scale(col, s);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Smooth(float a, float b, float v)
		{
			float num = (v - a) / (b - a);
			num = ((num < 0f) ? 0f : ((num > 1f) ? 1f : num));
			return num * num * (3f - 2f * num);
		}
	}
}
