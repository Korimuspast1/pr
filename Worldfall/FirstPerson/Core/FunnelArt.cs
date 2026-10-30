using System;

namespace FirstPerson.Core
{
	public static class FunnelArt
	{
		public const int W = 40;

		public const int H = 16;

		public const int Phases = 12;

		public const int Lights = 9;

		public const int StripTwist = 4;

		private const float Bands = 4f;

		private static readonly SpriteImage[] Strips = new SpriteImage[108];

		private static readonly uint[] Tones = new uint[8]
		{
			TerrainCache.Rgb(22, 24, 30),
			TerrainCache.Rgb(32, 35, 42),
			TerrainCache.Rgb(44, 47, 56),
			TerrainCache.Rgb(58, 62, 72),
			TerrainCache.Rgb(76, 80, 90),
			TerrainCache.Rgb(98, 102, 112),
			TerrainCache.Rgb(126, 129, 138),
			TerrainCache.Rgb(160, 162, 170)
		};

		private static readonly int[] Bayer4 = new int[16]
		{
			0, 8, 2, 10, 12, 4, 14, 6, 3, 11,
			1, 9, 15, 7, 13, 5
		};

		private static float Bayer(int x, int y)
		{
			return ((float)Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
		}

		public static SpriteImage Strip(int light, int phase)
		{
			light = Math.Max(0, Math.Min(8, light));
			phase = (phase % 12 + 12) % 12;
			int num = light * 12 + phase;
			SpriteImage spriteImage = Strips[num];
			if (spriteImage != null)
			{
				return spriteImage;
			}
			bool flag = light < 8;
			float num2 = (float)light * (MathF.PI * 2f) / 8f;
			float num3 = (flag ? Math.Max(0f, 0f - (float)Math.Cos(num2)) : 0f);
			uint[] array = new uint[640];
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 40; j++)
				{
					float num4 = ((float)j + 0.5f) / 40f * 2f - 1f;
					float num5 = Math.Abs(num4);
					float num6 = (float)Math.Asin(Math.Max(-1f, Math.Min(1f, num4)));
					float num7 = ((float)i + 0.5f) / 16f;
					float num8 = 4f * num6 / (MathF.PI * 2f) - (float)phase / 12f + num7 * 4f / 12f;
					float num9 = BlastFx.Smooth(((float)Math.Sin(MathF.PI * 2f * num8) + 0.35f) / 0.9f) * 2f - 1f;
					float num10 = 0.45f * num9 + 0.3f * (float)Math.Sin(MathF.PI * 4f * num8 + 1.7f) + 0.25f * (float)Math.Sin(MathF.PI * 6f * num8 + 0.4f);
					float num11 = (float)(TerrainModel.Hash(((int)Math.Floor(num8 * 20f) % 20 + 20) % 20, 7) & 0xFF) / 255f - 0.5f;
					float num12 = 1f - BlastFx.Smooth((num5 - 0.66f + 0.07f * num10 + 0.05f * num11) / 0.32f);
					if (!(num12 < 0.03f))
					{
						float num13 = (float)Math.Sqrt(Math.Max(0f, 1f - num4 * num4));
						float num14 = (flag ? Math.Max(0f, ((float)Math.Cos(num6 - num2) + 0.4f) / 1.4f) : (0.15f + 0.45f * num13));
						float num15 = num3 * (float)Math.Pow(num5, 4.0) * 0.6f;
						int val = (int)Math.Floor((0.08f + 0.62f * num14 * (0.5f + 0.5f * num13) + 0.12f * num10 + 0.1f * num11 + num15) * (float)Tones.Length + (Bayer(j, i) - 0.5f) * 0.8f);
						val = Math.Max(0, Math.Min(Tones.Length - 1, val));
						array[i * 40 + j] = (Tones[val] & 0xFFFFFF) | ((uint)(num12 * 255f) << 24);
					}
				}
			}
			spriteImage = new SpriteImage
			{
				Width = 40,
				Height = 16,
				Pixels = array,
				PivotX = 0.5f,
				UnitWidth = 1f,
				UnitHeight = 1f
			};
			spriteImage.MeasureOpaqueRows();
			Strips[num] = spriteImage;
			return spriteImage;
		}
	}
}
