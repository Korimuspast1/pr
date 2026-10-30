using System;

namespace FirstPerson.Core
{
	public static class Flames
	{
		public const int Frames = 12;

		public const int Variants = 3;

		public const float Fps = 14f;

		private const int W = 14;

		private const int H = 26;

		private static readonly SpriteImage[] Cache = new SpriteImage[36];

		private static readonly SpriteImage[] Small = new SpriteImage[36];

		private static SpriteImage _ember;

		private static SpriteImage _spark;

		private static readonly uint Core = TerrainCache.Rgb(255, 250, 214);

		private static readonly uint Yellow = TerrainCache.Rgb(255, 214, 72);

		private static readonly uint Orange = TerrainCache.Rgb(255, 142, 32);

		private static readonly uint Red = TerrainCache.Rgb(226, 72, 22);

		private static readonly uint Deep = TerrainCache.Rgb(150, 34, 14);

		public static SpriteImage Ember
		{
			get
			{
				if (_ember != null)
				{
					return _ember;
				}
				uint[] array = new uint[9];
				array[4] = Yellow | 0xFF000000u;
				array[1] = (array[3] = (array[5] = (array[7] = Orange | 0xFF000000u)));
				_ember = new SpriteImage
				{
					Width = 3,
					Height = 3,
					Pixels = array,
					PivotX = 0.5f,
					UnitWidth = 1f,
					UnitHeight = 1f
				};
				_ember.MeasureOpaqueRows();
				return _ember;
			}
		}

		public static SpriteImage Spark
		{
			get
			{
				if (_spark != null)
				{
					return _spark;
				}
				uint[] pixels = new uint[3]
				{
					Orange | 0xFF000000u,
					Yellow | 0xFF000000u,
					Core | 0xFF000000u
				};
				_spark = new SpriteImage
				{
					Width = 1,
					Height = 3,
					Pixels = pixels,
					PivotX = 0.5f,
					UnitWidth = 1f,
					UnitHeight = 3f
				};
				_spark.MeasureOpaqueRows();
				return _spark;
			}
		}

		public static SpriteImage At(float time, uint seed, bool small = false)
		{
			uint variant = seed % 3;
			int num = (int)(time * 14f + (float)((seed >> 3) % 12)) % 12;
			if (num < 0)
			{
				num += 12;
			}
			return Frame((int)variant, num, small);
		}

		public static SpriteImage Frame(int variant, int frame, bool small = false)
		{
			int num = variant * 12 + frame;
			SpriteImage[] array = (small ? Small : Cache);
			return array[num] ?? (array[num] = Build(variant, frame, small));
		}

		private static SpriteImage Build(int variant, int frame, bool small)
		{
			int num = (small ? 8 : 14);
			int num2 = (small ? 14 : 26);
			uint[] array = new uint[num * num2];
			float num3 = (float)frame * 256f / 12f;
			float num4 = (float)variant * 83f + 17f;
			float[] array2 = new float[num];
			float[] array3 = new float[num];
			for (int i = 0; i < num; i++)
			{
				float value = ((float)i + 0.5f) / (float)num * 2f - 1f;
				float num5 = 1f - (float)Math.Pow(Math.Abs(value), 2.4);
				float num6 = CloudNoise.Sample(num4 + (float)i * 23f, (0f - num3) * 2f) * 0.75f + CloudNoise.Sample(num4 + (float)i * 57f + 91f, (0f - num3) * 3f) * 0.25f;
				array2[i] = Math.Max(0.1f, num5 * (0.2f + 1.15f * num6 * num6));
				array3[i] = (CloudNoise.Sample(num4 + 140f, 0f - num3 + (float)i * 3f) - 0.5f) * 0.25f;
			}
			for (int j = 0; j < num2; j++)
			{
				float num7 = ((float)j + 0.5f) / (float)num2;
				for (int k = 0; k < num; k++)
				{
					float num8 = ((float)k + 0.5f) / (float)num * 2f - 1f;
					int num9 = Math.Max(0, Math.Min(num - 1, (int)Math.Round((float)k - array3[k] * num7 * (float)num)));
					float num10 = array2[num9];
					float num11 = num7 / num10;
					float num12 = CloudNoise.Sample(num4 * 2f + num8 * 17f + 40f, num7 * 96f - num3 * 3f) - 0.5f;
					float num13 = (1f - num11) * (1.15f - 0.65f * Math.Abs(num8)) + num12 * 0.55f;
					if (num11 > 1f)
					{
						if (num11 < 1.4f && num12 > 0.36f)
						{
							array[j * num + k] = Red | 0xFF000000u;
						}
					}
					else if (!(num13 < 0.08f))
					{
						uint num14 = ((num13 > 1f) ? Core : ((num13 > 0.7f) ? Yellow : ((num13 > 0.44f) ? Orange : ((num13 > 0.22f) ? Red : Deep))));
						array[j * num + k] = num14 | 0xFF000000u;
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
