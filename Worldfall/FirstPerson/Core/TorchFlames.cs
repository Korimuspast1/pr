using System;

namespace FirstPerson.Core
{
	public static class TorchFlames
	{
		public const int Frames = 12;

		public const int Variants = 3;

		public const float Fps = 13f;

		private const int W = 22;

		private const int H = 36;

		private const int SmallW = 12;

		private const int SmallH = 20;

		public const int Below = 3;

		public const int SmallBelow = 2;

		public const float Foot = 0.56f;

		public const float WidthInRims = 2.6f;

		private static readonly SpriteImage[] Cache = new SpriteImage[36];

		private static readonly SpriteImage[] Small = new SpriteImage[36];

		private static readonly uint Core = TerrainCache.Rgb(255, 250, 220);

		private static readonly uint Yellow = TerrainCache.Rgb(255, 216, 76);

		private static readonly uint Orange = TerrainCache.Rgb(255, 146, 34);

		private static readonly uint Red = TerrainCache.Rgb(230, 76, 22);

		private static readonly uint Deep = TerrainCache.Rgb(156, 36, 16);

		public static SpriteImage At(float time, uint seed, bool small = false)
		{
			int num = (int)(seed % 3);
			int num2 = (int)(time * 13f + (float)((seed >> 3) % 12)) % 12;
			if (num2 < 0)
			{
				num2 += 12;
			}
			int num3 = num * 12 + num2;
			SpriteImage[] array = (small ? Small : Cache);
			return array[num3] ?? (array[num3] = Build(num, num2, small));
		}

		public static float BelowShare(bool small)
		{
			if (!small)
			{
				return 1f / 12f;
			}
			return 0.1f;
		}

		private static float Smooth(float x)
		{
			x = ((x < 0f) ? 0f : ((x > 1f) ? 1f : x));
			return x * x * (3f - 2f * x);
		}

		private static SpriteImage Build(int variant, int frame, bool small)
		{
			int num = (small ? 12 : 22);
			int num2 = (small ? 20 : 36);
			int num3 = (small ? 2 : 3);
			uint[] array = new uint[num * num2];
			float num4 = (float)frame * 256f / 12f;
			float num5 = (float)variant * 97f + 29f;
			int num6 = num2 - num3;
			float[] array2 = new float[num];
			float[] array3 = new float[num];
			for (int i = 0; i < num; i++)
			{
				float num7 = ((float)i + 0.5f) / (float)num * 2f - 1f;
				float num8 = 1f - (float)Math.Pow(Math.Abs(num7 * 0.92f), 1.7);
				float num9 = CloudNoise.Sample(num5 + (float)i * 19f, (0f - num4) * 2f) * 0.7f + CloudNoise.Sample(num5 + (float)i * 53f + 71f, (0f - num4) * 3f) * 0.3f;
				float num10 = 0.5f + 0.5f * (float)Math.Cos(num7 * 7.5f + (float)variant * 1.9f);
				array2[i] = Math.Max(0.22f, num8 * (0.45f + 0.75f * num9 * num9 + 0.18f * num10));
				array3[i] = (CloudNoise.Sample(num5 + 151f, 0f - num4 + (float)i * 3f) - 0.5f) * 0.22f;
			}
			for (int j = 0; j < num2; j++)
			{
				float num11 = ((float)j + 0.5f - (float)num3) / (float)num6;
				float num12 = ((num11 <= 0f) ? 0.476f : (0.56f + 0.44f * Smooth(num11 / 0.34f)));
				for (int k = 0; k < num; k++)
				{
					float num13 = ((float)k + 0.5f) / (float)num * 2f - 1f;
					float num14 = Math.Abs(num13);
					if (num14 > num12)
					{
						continue;
					}
					int num15 = Math.Max(0, Math.Min(num - 1, (int)Math.Round((float)k - array3[k] * Math.Max(0f, num11) * (float)num)));
					float num16 = array2[num15];
					float num17 = Math.Max(0f, num11) / num16;
					float num18 = CloudNoise.Sample(num5 * 2f + num13 * 15f + 40f, num11 * 90f - num4 * 3f) - 0.5f;
					float num19 = 1f - num14 / Math.Max(0.05f, num12);
					float num20 = (1f - num17) * (0.55f + 0.75f * num19) + num18 * 0.5f - ((num11 < 0f) ? 0.15f : 0f);
					if (num17 > 1f)
					{
						if (num17 < 1.35f && num18 > 0.34f)
						{
							array[j * num + k] = Red | 0xFF000000u;
						}
					}
					else if (!(num20 < 0.1f))
					{
						uint num21 = ((num20 > 1.02f) ? Core : ((num20 > 0.74f) ? Yellow : ((num20 > 0.47f) ? Orange : ((num20 > 0.24f) ? Red : Deep))));
						array[j * num + k] = num21 | 0xFF000000u;
					}
				}
			}
			SpriteImage spriteImage = new SpriteImage();
			spriteImage.Width = num;
			spriteImage.Height = num2;
			spriteImage.Pixels = array;
			spriteImage.PivotX = 0.5f;
			spriteImage.UnitWidth = 1f;
			spriteImage.UnitHeight = (float)num2 / (float)num;
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}
	}
}
