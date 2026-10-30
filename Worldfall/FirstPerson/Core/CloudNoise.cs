using System;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class CloudNoise
	{
		private const int N = 256;

		private const int Mask = 255;

		private static readonly float[] Tex = Build();

		private static float[] Build()
		{
			float[] array = new float[65536];
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = 0; i < 256; i++)
			{
				for (int j = 0; j < 256; j++)
				{
					float num3 = 0f;
					float num4 = 0.5f;
					float num5 = 0f;
					for (int k = 0; k < 5; k++)
					{
						int num6 = 4 << k;
						num3 += num4 * Tileable((float)(j * num6) / 256f, (float)(i * num6) / 256f, num6, k * 131);
						num5 += num4;
						num4 *= 0.5f;
					}
					float num7 = (array[i * 256 + j] = num3 / num5);
					if (num7 < num)
					{
						num = num7;
					}
					if (num7 > num2)
					{
						num2 = num7;
					}
				}
			}
			float num8 = Math.Max(0.0001f, num2 - num);
			for (int l = 0; l < array.Length; l++)
			{
				array[l] = (array[l] - num) / num8;
			}
			return array;
		}

		private static float Tileable(float x, float y, int period, int seed)
		{
			int num = (int)Math.Floor(x);
			int num2 = (int)Math.Floor(y);
			float num3 = x - (float)num;
			float num4 = y - (float)num2;
			num3 = num3 * num3 * (3f - 2f * num3);
			num4 = num4 * num4 * (3f - 2f * num4);
			float num5 = Cell(num, num2, period, seed);
			float num6 = Cell(num + 1, num2, period, seed);
			float num7 = Cell(num, num2 + 1, period, seed);
			float num8 = Cell(num + 1, num2 + 1, period, seed);
			float num9 = num5 + (num6 - num5) * num3;
			float num10 = num7 + (num8 - num7) * num3;
			return num9 + (num10 - num9) * num4;
		}

		private static float Cell(int x, int y, int period, int seed)
		{
			x = (x % period + period) % period;
			y = (y % period + period) % period;
			return (float)(TerrainModel.Hash(x + seed * 7, y - seed * 3) & 0xFFFF) / 65535f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Sample(float u, float v)
		{
			int num = (int)u;
			int num2 = (int)v;
			if ((float)num > u)
			{
				num--;
			}
			if ((float)num2 > v)
			{
				num2--;
			}
			float num3 = num;
			float num4 = num2;
			int num5 = (int)num3 & 0xFF;
			int num6 = (int)num4 & 0xFF;
			int num7 = (num5 + 1) & 0xFF;
			int num8 = (num6 + 1) & 0xFF;
			float num9 = u - num3;
			float num10 = v - num4;
			float num11 = Tex[num6 * 256 + num5];
			float num12 = Tex[num6 * 256 + num7];
			float num13 = Tex[num8 * 256 + num5];
			float num14 = Tex[num8 * 256 + num7];
			float num15 = num11 + (num12 - num11) * num9;
			float num16 = num13 + (num14 - num13) * num9;
			return num15 + (num16 - num15) * num10;
		}
	}
}
