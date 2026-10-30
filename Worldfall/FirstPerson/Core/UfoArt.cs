using System;

namespace FirstPerson.Core
{
	public static class UfoArt
	{
		public static readonly uint Hull0 = TerrainCache.Rgb(63, 73, 91);

		public static readonly uint Hull1 = TerrainCache.Rgb(99, 119, 140);

		public static readonly uint Hull2 = TerrainCache.Rgb(131, 166, 207);

		public static readonly uint Hull3 = TerrainCache.Rgb(177, 213, 254);

		public static readonly uint Hull4 = TerrainCache.Rgb(223, 238, 255);

		public static readonly uint Dome0 = TerrainCache.Rgb(56, 83, 43);

		public static readonly uint Dome1 = TerrainCache.Rgb(78, 131, 53);

		public static readonly uint Dome2 = TerrainCache.Rgb(129, 215, 73);

		public static readonly uint Dome3 = TerrainCache.Rgb(204, 255, 126);

		public static readonly uint BeamGreen = TerrainCache.Rgb(109, 255, 30);

		public static readonly uint BeamLime = TerrainCache.Rgb(194, 248, 75);

		public static readonly uint BeamYellow = TerrainCache.Rgb(245, 255, 99);

		public static readonly uint BeamCream = TerrainCache.Rgb(252, 255, 209);

		public static readonly uint Bolt = TerrainCache.Rgb(255, 100, 250);

		public static readonly uint BoltCore = TerrainCache.Rgb(255, 214, 252);

		public const float Taper = 0.42f;

		public const int ColumnFrames = 8;

		private const int ColW = 25;

		private const int ColH = 64;

		private static SpriteImage[] _column;

		private static SpriteImage _halo;

		public static SpriteImage ColumnHalo
		{
			get
			{
				if (_halo != null)
				{
					return _halo;
				}
				uint[] array = new uint[768];
				for (int i = 0; i < 32; i++)
				{
					float num = 1f - ((float)i + 0.5f) / 32f;
					float num2 = (0.45f + 0.55f * num) * 24f * 0.5f;
					for (int j = 0; j < 24; j++)
					{
						float num3 = Math.Abs((float)j + 0.5f - 12f) / num2;
						if (!(num3 >= 1f))
						{
							float a = (float)Math.Pow(1f - num3, 1.7000000476837158) * (0.75f + 0.25f * num);
							array[i * 24 + j] = WithAlpha(TerrainCache.Lerp(BeamLime, BeamGreen, num3), a);
						}
					}
				}
				return _halo = Image(24, 32, array);
			}
		}

		private static uint WithAlpha(uint c, float a)
		{
			return (c & 0xFFFFFF) | (uint)(Math.Max(0, Math.Min(255, (int)(a * 255f))) << 24);
		}

		private static SpriteImage Image(int w, int h, uint[] px)
		{
			SpriteImage spriteImage = new SpriteImage();
			spriteImage.Width = w;
			spriteImage.Height = h;
			spriteImage.Pixels = px;
			spriteImage.PivotX = 0.5f;
			spriteImage.UnitWidth = 1f;
			spriteImage.UnitHeight = 1f;
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}

		public static float ColumnHalf(float v)
		{
			v = BlastFx.Clamp01(v);
			float num = 0.42f + 0.58000004f * (float)Math.Pow(v, 1.350000023841858);
			if (v > 0.9f)
			{
				num += 0.18f * (v - 0.9f) / 0.1f;
			}
			return Math.Min(1f, num / 1f) * 0.85f;
		}

		public static SpriteImage Column(int frame)
		{
			if (_column == null)
			{
				_column = new SpriteImage[8];
				Random random = new Random(77);
				(float, int, int)[] array = new(float, int, int)[14];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = ((float)random.NextDouble() * 1.6f - 0.8f, random.Next(64), 6 + random.Next(18));
				}
				for (int j = 0; j < 8; j++)
				{
					uint[] array2 = new uint[1600];
					int num = j * 64 / 8;
					for (int k = 0; k < 64; k++)
					{
						float val = ColumnHalf(1f - ((float)k + 0.5f) / 64f) * 25f * 0.5f;
						float num2 = 0.5f + 0.5f * (float)Math.Sin((double)((float)((k + num) % 64) / 64f) * Math.PI * 2.0 * 3.0);
						for (int l = 0; l < 25; l++)
						{
							float num3 = Math.Abs((float)l + 0.5f - 12.5f) / Math.Max(0.5f, val);
							if (!(num3 > 1f))
							{
								float a = 1f;
								uint c;
								if (num3 < 0.22f + 0.08f * num2)
								{
									c = BeamCream;
								}
								else if (num3 < 0.5f + 0.06f * num2)
								{
									c = BeamYellow;
								}
								else if (num3 < 0.78f)
								{
									c = BeamLime;
								}
								else
								{
									c = BeamGreen;
									a = 0.62f;
								}
								array2[k * 25 + l] = WithAlpha(c, a);
							}
						}
					}
					(float, int, int)[] array3 = array;
					for (int m = 0; m < array3.Length; m++)
					{
						(float, int, int) tuple = array3[m];
						for (int n = 0; n < tuple.Item3; n++)
						{
							int num4 = ((tuple.Item2 - n - num) % 64 + 64) % 64;
							float num5 = ColumnHalf(1f - ((float)num4 + 0.5f) / 64f) * 25f * 0.5f;
							int num6 = (int)Math.Round(12f + tuple.Item1 * num5 * 0.7f);
							if (num6 >= 0 && num6 < 25 && array2[num4 * 25 + num6] >> 24 != 0)
							{
								array2[num4 * 25 + num6] = WithAlpha(TerrainCache.Lerp(array2[num4 * 25 + num6], BeamCream, 0.75f), (float)(array2[num4 * 25 + num6] >> 24) / 255f);
							}
						}
					}
					_column[j] = Image(25, 64, array2);
				}
			}
			return _column[(frame % 8 + 8) % 8];
		}

		public static void Lightning(FxSink sink, Vec3 a, Vec3 b, int kinks, float jitter, uint seed, int tick, float alpha, float width = 0.05f, uint color = 0u)
		{
			if (sink.Streaks == null || alpha <= 0.01f)
			{
				return;
			}
			if (color == 0)
			{
				color = Bolt;
			}
			Vec3 vec = b - a;
			float num = (float)Math.Sqrt(Vec3.Dot(vec, vec));
			if (num < 0.001f)
			{
				return;
			}
			Vec3 vec2 = vec * (1f / num);
			Vec3 vec3 = ((Math.Abs(vec2.Z) < 0.9f) ? Vec3.Cross(vec2, new Vec3(0f, 0f, 1f)).Normalized() : new Vec3(1f, 0f, 0f));
			Vec3 vec4 = Vec3.Cross(vec3, vec2);
			Vec3 a2 = a;
			for (int i = 1; i <= kinks; i++)
			{
				float num2 = (float)i / (float)kinks;
				Vec3 vec5 = a + vec * num2;
				if (i < kinks)
				{
					float num3 = jitter * (float)Math.Sin((double)num2 * Math.PI);
					vec5 = vec5 + vec3 * ((BlastFx.Rand(seed, tick * 131 + i) - 0.5f) * 2f * num3) + vec4 * ((BlastFx.Rand(seed, tick * 131 + i + 61) - 0.5f) * 2f * num3);
				}
				sink.Line(a2, vec5, color, alpha, alpha, width);
				a2 = vec5;
			}
		}
	}
}
