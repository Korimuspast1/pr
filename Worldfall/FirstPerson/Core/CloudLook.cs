using System;

namespace FirstPerson.Core
{
	public sealed class CloudLook
	{
		public float Opacity = 0.6f;

		public float HalfDepth = 0.06f;

		public float Seam = 0.05f;

		public float Grain = 0.15f;

		public float Mottle = 0.5f;

		public float Bump = 0.5f;

		public Vec3 Churn = new Vec3(0f, 0f, 0.04f);

		public float FadeFrom = float.MaxValue;

		public float FadeTo = float.MaxValue;

		public float Wrap = 0.55f;

		public float Scatter = 0.3f;

		public float Ambient = 1f;

		public uint SelfColor = TerrainCache.Rgb(232, 230, 246);

		public float SelfLight = 0.12f;

		public Vec3 GlowAt;

		public float GlowRadius = 1f;

		public float Glow;

		public uint GlowColor = TerrainCache.Rgb(255, 214, 140);

		private const int N = 32;

		private const int Mask = 31;

		private static float[] _table;

		private static float[] Table
		{
			get
			{
				if (_table != null)
				{
					return _table;
				}
				float[] array = new float[32768];
				float num = float.MaxValue;
				float num2 = float.MinValue;
				for (int i = 0; i < 32; i++)
				{
					for (int j = 0; j < 32; j++)
					{
						for (int k = 0; k < 32; k++)
						{
							float num3 = (array[(i * 32 + j) * 32 + k] = Octave((float)k / 4f, (float)j / 4f, (float)i / 4f, 8) * 0.7f + Octave((float)k / 2f, (float)j / 2f, (float)i / 2f, 16) * 0.3f);
							if (num3 < num)
							{
								num = num3;
							}
							if (num3 > num2)
							{
								num2 = num3;
							}
						}
					}
				}
				float num4 = 1f / Math.Max(0.0001f, num2 - num);
				for (int l = 0; l < array.Length; l++)
				{
					float num5 = (array[l] - num) * num4;
					array[l] = num5 * num5 * (3f - 2f * num5);
				}
				float[] array2 = new float[131072];
				for (int m = 0; m < 32; m++)
				{
					for (int n = 0; n < 32; n++)
					{
						for (int num6 = 0; num6 < 32; num6++)
						{
							int num7 = (m * 32 + n) * 32 + num6;
							array2[num7 * 4] = array[num7];
							array2[num7 * 4 + 1] = (array[(m * 32 + n) * 32 + ((num6 + 1) & 0x1F)] - array[(m * 32 + n) * 32 + ((num6 - 1) & 0x1F)]) * 0.5f;
							array2[num7 * 4 + 2] = (array[(m * 32 + ((n + 1) & 0x1F)) * 32 + num6] - array[(m * 32 + ((n - 1) & 0x1F)) * 32 + num6]) * 0.5f;
							array2[num7 * 4 + 3] = (array[(((m + 1) & 0x1F) * 32 + n) * 32 + num6] - array[(((m - 1) & 0x1F) * 32 + n) * 32 + num6]) * 0.5f;
						}
					}
				}
				return _table = array2;
			}
		}

		private static float Octave(float x, float y, float z, int period)
		{
			int num = (int)Math.Floor(x);
			int num2 = (int)Math.Floor(y);
			int num3 = (int)Math.Floor(z);
			float num4 = x - (float)num;
			float num5 = y - (float)num2;
			float num6 = z - (float)num3;
			num4 = num4 * num4 * (3f - 2f * num4);
			num5 = num5 * num5 * (3f - 2f * num5);
			num6 = num6 * num6 * (3f - 2f * num6);
			float num7 = L(num, num2, num3) + (L(num + 1, num2, num3) - L(num, num2, num3)) * num4;
			float num8 = L(num, num2 + 1, num3) + (L(num + 1, num2 + 1, num3) - L(num, num2 + 1, num3)) * num4;
			float num9 = L(num, num2, num3 + 1) + (L(num + 1, num2, num3 + 1) - L(num, num2, num3 + 1)) * num4;
			float num10 = L(num, num2 + 1, num3 + 1) + (L(num + 1, num2 + 1, num3 + 1) - L(num, num2 + 1, num3 + 1)) * num4;
			float num11 = num7 + (num8 - num7) * num5;
			float num12 = num9 + (num10 - num9) * num5;
			return num11 + (num12 - num11) * num6;
			float L(int a, int b, int c)
			{
				return (float)(TerrainModel.Hash((a % period + period) % period * 7919 + (c % period + period) % period * 104729, (b % period + period) % period * 31 + 5) & 0xFFFF) * 1.5259022E-05f;
			}
		}

		public static float Lumps(float x, float y, float z, out float gx, out float gy, out float gz)
		{
			float[] array = _table ?? Table;
			int num = FastMath.FloorInt(x);
			int num2 = FastMath.FloorInt(y);
			int num3 = FastMath.FloorInt(z);
			float num4 = x - (float)num;
			float num5 = y - (float)num2;
			float num6 = z - (float)num3;
			int num7 = num & 0x1F;
			int num8 = num2 & 0x1F;
			int num9 = num3 & 0x1F;
			int num10 = (num7 + 1) & 0x1F;
			int num11 = (num8 + 1) & 0x1F;
			int num12 = (num9 + 1) & 0x1F;
			int num13 = (num9 * 32 + num8) * 32;
			int num14 = (num9 * 32 + num11) * 32;
			int num15 = (num12 * 32 + num8) * 32;
			int num16 = (num12 * 32 + num11) * 32;
			int num17 = (num13 + num7) * 4;
			int num18 = (num13 + num10) * 4;
			int num19 = (num14 + num7) * 4;
			int num20 = (num14 + num10) * 4;
			int num21 = (num15 + num7) * 4;
			int num22 = (num15 + num10) * 4;
			int num23 = (num16 + num7) * 4;
			int num24 = (num16 + num10) * 4;
			float num25 = (1f - num4) * (1f - num5) * (1f - num6);
			float num26 = num4 * (1f - num5) * (1f - num6);
			float num27 = (1f - num4) * num5 * (1f - num6);
			float num28 = num4 * num5 * (1f - num6);
			float num29 = (1f - num4) * (1f - num5) * num6;
			float num30 = num4 * (1f - num5) * num6;
			float num31 = (1f - num4) * num5 * num6;
			float num32 = num4 * num5 * num6;
			gx = array[num17 + 1] * num25 + array[num18 + 1] * num26 + array[num19 + 1] * num27 + array[num20 + 1] * num28 + array[num21 + 1] * num29 + array[num22 + 1] * num30 + array[num23 + 1] * num31 + array[num24 + 1] * num32;
			gy = array[num17 + 2] * num25 + array[num18 + 2] * num26 + array[num19 + 2] * num27 + array[num20 + 2] * num28 + array[num21 + 2] * num29 + array[num22 + 2] * num30 + array[num23 + 2] * num31 + array[num24 + 2] * num32;
			gz = array[num17 + 3] * num25 + array[num18 + 3] * num26 + array[num19 + 3] * num27 + array[num20 + 3] * num28 + array[num21 + 3] * num29 + array[num22 + 3] * num30 + array[num23 + 3] * num31 + array[num24 + 3] * num32;
			return array[num17] * num25 + array[num18] * num26 + array[num19] * num27 + array[num20] * num28 + array[num21] * num29 + array[num22] * num30 + array[num23] * num31 + array[num24] * num32;
		}
	}
}
