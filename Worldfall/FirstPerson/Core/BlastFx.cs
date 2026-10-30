using System;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class BlastFx
	{
		public sealed class CloudSlots
		{
			public int Top = -1;

			public int Side = -1;

			public int Under = -1;

			public int Patch = -1;

			public int Rim = -1;

			public uint[] Palette;

			public static CloudSlots Of(MeshModel m)
			{
				return new CloudSlots
				{
					Top = m.SlotIndex("top"),
					Side = m.SlotIndex("side"),
					Under = m.SlotIndex("under"),
					Patch = m.SlotIndex("patch"),
					Rim = m.SlotIndex("rim"),
					Palette = new uint[m.SlotNames.Length]
				};
			}

			private void Set(int slot, uint c)
			{
				if (slot >= 0)
				{
					Palette[slot] = c;
				}
			}

			public void Fire(float heat, float soot, float rimHeat)
			{
				Set(Top, Heat(heat + 0.14f));
				Set(Side, Heat(heat));
				Set(Under, Heat(heat - 0.2f));
				Set(Patch, Heat(heat - 0.24f - 0.32f * soot));
				Set(Rim, Heat(Math.Max(heat + 0.04f, rimHeat)));
			}

			public void Smoked(float warmth)
			{
				uint b = TerrainCache.Rgb(120, 70, 56);
				Set(Top, TerrainCache.Lerp(Smoke(1f), b, warmth));
				Set(Side, TerrainCache.Lerp(Smoke(0.72f), b, warmth));
				Set(Under, TerrainCache.Lerp(Smoke(0.35f), b, warmth * 0.7f));
				Set(Patch, TerrainCache.Lerp(Smoke(0.15f), b, warmth * 0.5f));
				Set(Rim, TerrainCache.Lerp(Smoke(0.6f), TerrainCache.Rgb(150, 60, 34), warmth));
			}

			public void Dust(uint land, float glow)
			{
				uint num = TerrainCache.Lerp(TerrainCache.Rgb(150, 132, 108), land, 0.3f);
				Set(Top, TerrainCache.Lerp(TerrainCache.Scale(num, 1.15f), Heat(0.75f), glow));
				Set(Side, TerrainCache.Lerp(num, Heat(0.6f), glow));
				Set(Under, TerrainCache.Lerp(TerrainCache.Scale(num, 0.7f), Heat(0.45f), glow));
				Set(Patch, TerrainCache.Lerp(TerrainCache.Scale(num, 0.55f), Heat(0.3f), glow));
				Set(Rim, TerrainCache.Lerp(TerrainCache.Scale(num, 0.9f), Heat(0.65f), glow));
			}
		}

		private static readonly float[] HeatStops = new float[10] { -0.2f, 0f, 0.12f, 0.25f, 0.4f, 0.55f, 0.7f, 0.85f, 1f, 1.2f };

		private static readonly uint[] HeatColors = new uint[10]
		{
			TerrainCache.Rgb(64, 58, 60),
			TerrainCache.Rgb(58, 48, 48),
			TerrainCache.Rgb(62, 40, 36),
			TerrainCache.Rgb(104, 34, 24),
			TerrainCache.Rgb(172, 46, 22),
			TerrainCache.Rgb(228, 92, 28),
			TerrainCache.Rgb(255, 146, 42),
			TerrainCache.Rgb(255, 204, 84),
			TerrainCache.Rgb(255, 240, 170),
			TerrainCache.Rgb(255, 255, 238)
		};

		private static readonly uint[] StreakColors = new uint[7]
		{
			TerrainCache.Rgb(255, 255, 236),
			TerrainCache.Rgb(255, 232, 120),
			TerrainCache.Rgb(255, 160, 60),
			TerrainCache.Rgb(244, 80, 52),
			TerrainCache.Rgb(232, 64, 108),
			TerrainCache.Rgb(164, 56, 150),
			TerrainCache.Rgb(90, 40, 120)
		};

		private static SpriteImage[] _puffs;

		private static SpriteImage _glow;

		private static SpriteImage _star;

		private static SpriteImage _streak;

		private static SpriteImage _ring;

		private static readonly SpriteImage[] _sparks = new SpriteImage[3];

		public const int PuffVariants = 6;

		private static readonly int[] Bayer4 = new int[16]
		{
			0, 8, 2, 10, 12, 4, 14, 6, 3, 11,
			1, 9, 15, 7, 13, 5
		};

		private static SpriteImage[] _soft;

		public static SpriteImage GlowSprite
		{
			get
			{
				if (_glow != null)
				{
					return _glow;
				}
				uint[] array = new uint[2304];
				for (int i = 0; i < 48; i++)
				{
					for (int j = 0; j < 48; j++)
					{
						float num = ((float)j + 0.5f) / 48f * 2f - 1f;
						float num2 = ((float)i + 0.5f) / 48f * 2f - 1f;
						float num3 = (float)Math.Sqrt(num * num + num2 * num2);
						if (!(num3 >= 1f))
						{
							float num4 = (1f - num3) * (1f - num3) * (0.6f + 0.4f * (1f - num3));
							int num5 = Math.Min(255, (int)(num4 * 255f));
							if (num5 > 0)
							{
								array[i * 48 + j] = (uint)(0xFFFFFF | (num5 << 24));
							}
						}
					}
				}
				return _glow = Image(48, 48, array);
			}
		}

		public static SpriteImage StarSprite
		{
			get
			{
				if (_star != null)
				{
					return _star;
				}
				uint[] array = new uint[4096];
				Random random = new Random(5);
				float[] array2 = new float[14];
				float[] array3 = new float[14];
				float[] array4 = new float[14];
				for (int i = 0; i < 14; i++)
				{
					array3[i] = ((float)i + (float)random.NextDouble() * 0.4f) / 14f * MathF.PI * 2f;
					array2[i] = ((i % 2 == 0) ? (0.85f + (float)random.NextDouble() * 0.15f) : (0.4f + (float)random.NextDouble() * 0.2f));
					array4[i] = ((i % 2 == 0) ? 0.07f : 0.055f);
				}
				for (int j = 0; j < 64; j++)
				{
					for (int k = 0; k < 64; k++)
					{
						float num = ((float)k + 0.5f) / 64f * 2f - 1f;
						float num2 = ((float)j + 0.5f) / 64f * 2f - 1f;
						float num3 = (float)Math.Sqrt(num * num + num2 * num2);
						float num4 = (float)Math.Atan2(num2, num);
						float num5 = Math.Max(0f, 1f - num3 / 0.22f);
						float num6 = num5 * num5;
						for (int l = 0; l < 14; l++)
						{
							float num7 = num4 - array3[l];
							while ((double)num7 > Math.PI)
							{
								num7 -= MathF.PI * 2f;
							}
							for (; (double)num7 < -Math.PI; num7 += MathF.PI * 2f)
							{
							}
							float num8 = Math.Abs(num7) * num3;
							float num9 = array4[l] * (1f - num3 / array2[l]);
							if (!(num9 <= 0f) && !(num8 > num9))
							{
								float num10 = (1f - num8 / num9) * (1f - num3 / array2[l]);
								num6 = Math.Max(num6, num10 * 1.2f);
							}
						}
						num6 = Math.Min(1f, num6);
						if (!(num6 < 0.04f))
						{
							uint num11 = TerrainCache.Lerp(TerrainCache.Rgb(255, 214, 120), TerrainCache.Rgb(255, 255, 250), Math.Min(1f, num6 * 1.4f));
							int num12 = (int)(num6 * 255f);
							array[j * 64 + k] = (num11 & 0xFFFFFF) | (uint)(num12 << 24);
						}
					}
				}
				return _star = Image(64, 64, array);
			}
		}

		public static SpriteImage Streak
		{
			get
			{
				if (_streak != null)
				{
					return _streak;
				}
				uint[] array = new uint[5];
				for (int i = 0; i < 5; i++)
				{
					array[i] = (uint)(0xFFFFFF | (255 - i * 46 << 24));
				}
				return _streak = Image(1, 5, array);
			}
		}

		public static SpriteImage Ring
		{
			get
			{
				if (_ring != null)
				{
					return _ring;
				}
				uint[] array = new uint[1024];
				for (int i = 0; i < 32; i++)
				{
					for (int j = 0; j < 32; j++)
					{
						float num = ((float)j + 0.5f) / 32f * 2f - 1f;
						float num2 = ((float)i + 0.5f) / 32f * 2f - 1f;
						float num3 = (float)Math.Sqrt(num * num + num2 * num2);
						float num4 = Math.Max(0f, 1f - Math.Abs(num3 - 0.85f) / 0.13f);
						if (!(num4 <= 0f))
						{
							array[i * 32 + j] = 0xFFFFFF | ((uint)(num4 * 255f) << 24);
						}
					}
				}
				return _ring = Image(32, 32, array);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint AddScaled(uint dst, uint add, float w)
		{
			int num = (int)(w * 256f);
			int num2 = (int)(dst & 0xFF) + ((int)(add & 0xFF) * num >> 8);
			int num3 = (int)((dst >> 8) & 0xFF) + ((int)((add >> 8) & 0xFF) * num >> 8);
			int num4 = (int)((dst >> 16) & 0xFF) + ((int)((add >> 16) & 0xFF) * num >> 8);
			if (num2 > 255)
			{
				num2 = 255;
			}
			if (num3 > 255)
			{
				num3 = 255;
			}
			if (num4 > 255)
			{
				num4 = 255;
			}
			return (uint)(num2 | (num3 << 8) | (num4 << 16) | -16777216);
		}

		public static float Clamp01(float v)
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

		public static float Smooth(float v)
		{
			v = Clamp01(v);
			return v * v * (3f - 2f * v);
		}

		public static float Rand(uint seed, int i)
		{
			return (float)(TerrainModel.Hash((int)seed + i * 7919, i * 31 + 17) & 0xFFFF) / 65535f;
		}

		public static uint Heat(float h)
		{
			if (h <= HeatStops[0])
			{
				return HeatColors[0];
			}
			for (int i = 1; i < HeatStops.Length; i++)
			{
				if (h <= HeatStops[i])
				{
					return TerrainCache.Lerp(HeatColors[i - 1], HeatColors[i], (h - HeatStops[i - 1]) / (HeatStops[i] - HeatStops[i - 1]));
				}
			}
			return HeatColors[HeatColors.Length - 1];
		}

		public static uint StreakColor(float k)
		{
			float num = Clamp01(k) * (float)(StreakColors.Length - 1);
			int num2 = Math.Min(StreakColors.Length - 2, (int)num);
			return TerrainCache.Lerp(StreakColors[num2], StreakColors[num2 + 1], num - (float)num2);
		}

		public static uint Smoke(float shade)
		{
			return TerrainCache.Lerp(TerrainCache.Rgb(72, 64, 64), TerrainCache.Rgb(150, 138, 132), Clamp01(shade));
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

		private static float BayerAt(int x, int y)
		{
			return ((float)Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
		}

		public static SpriteImage Puff(int variant)
		{
			if (_puffs == null)
			{
				_puffs = new SpriteImage[6];
				for (int i = 0; i < 6; i++)
				{
					_puffs[i] = MakePuff(i);
				}
			}
			return _puffs[(variant % 6 + 6) % 6];
		}

		private static SpriteImage MakePuff(int v)
		{
			Random random = new Random(v * 977 + 13);
			int count = 4 + v % 3;
			float[] cx = new float[count];
			float[] cy = new float[count];
			float[] cr = new float[count];
			cx[0] = 16f;
			cy[0] = 14f;
			cr[0] = 8.5f + (float)random.NextDouble() * 2f;
			for (int i = 1; i < count; i++)
			{
				double num = random.NextDouble() * Math.PI * 2.0;
				float num2 = 4.5f + (float)random.NextDouble() * 4f;
				float num3 = 5f + (float)random.NextDouble() * 4f;
				cx[i] = Math.Max(num2 + 0.5f, Math.Min(32f - num2 - 0.5f, 16f + (float)Math.Cos(num) * num3));
				cy[i] = Math.Max(num2 + 0.5f, Math.Min(32f - num2 - 0.5f, 14f + (float)Math.Sin(num) * num3 * 0.8f + 1.5f));
				cr[i] = num2;
			}
			uint[] array = new uint[1024];
			float num4 = -0.45f;
			float num5 = 0.55f;
			float num6 = 0.7f;
			for (int j = 0; j < 32; j++)
			{
				for (int k = 0; k < 32; k++)
				{
					float num7 = (float)k + 0.5f;
					float num8 = (float)j + 0.5f;
					float num9 = H(num7, num8);
					if (!(num9 < 0f) && (!(num9 < 1.6f) || !(BayerAt(k, j) > num9 / 1.6f)))
					{
						float num10 = H(num7 + 1f, num8);
						float num11 = H(num7, num8 + 1f);
						if (num10 < 0f)
						{
							num10 = num9 - 1.5f;
						}
						if (num11 < 0f)
						{
							num11 = num9 - 1.5f;
						}
						float num12 = num9 - num10;
						float num13 = num9 - num11;
						float num14 = 1f;
						float num15 = 1f / (float)Math.Sqrt(num12 * num12 + num13 * num13 + num14 * num14);
						float num16 = (num12 * num4 + num13 * num5 + num14 * num6) * num15;
						int num17 = ((num16 > 0.8f) ? 255 : ((num16 > 0.55f) ? 226 : ((num16 > 0.25f) ? 190 : 150)));
						array[j * 32 + k] = TerrainCache.Rgb(num17, num17, num17);
					}
				}
			}
			return Image(32, 32, array);
			float H(float x, float y)
			{
				float num18 = -1f;
				for (int l = 0; l < count; l++)
				{
					float num19 = x - cx[l];
					float num20 = y - cy[l];
					float num21 = cr[l] * cr[l] - num19 * num19 - num20 * num20;
					if (num21 > 0f)
					{
						num18 = Math.Max(num18, (float)Math.Sqrt(num21) + cr[l] * 0.15f);
					}
				}
				return num18;
			}
		}

		public static SpriteImage Soft(int variant)
		{
			if (_soft == null)
			{
				_soft = new SpriteImage[6];
				for (int i = 0; i < 6; i++)
				{
					_soft[i] = MakeSoft(i);
				}
			}
			return _soft[(variant % 6 + 6) % 6];
		}

		private static SpriteImage MakeSoft(int v)
		{
			Random random = new Random(v * 613 + 7);
			int num = 5 + v % 3;
			float[] array = new float[num];
			float[] array2 = new float[num];
			float[] array3 = new float[num];
			array[0] = 16f;
			array2[0] = 15f;
			array3[0] = 9f + (float)random.NextDouble() * 2f;
			for (int i = 1; i < num; i++)
			{
				double num2 = random.NextDouble() * Math.PI * 2.0;
				float num3 = 4.5f + (float)random.NextDouble() * 4.5f;
				float num4 = 4f + (float)random.NextDouble() * 6f;
				array[i] = Math.Max(num3 + 1f, Math.Min(32f - num3 - 1f, 16f + (float)Math.Cos(num2) * num4));
				array2[i] = Math.Max(num3 + 1f, Math.Min(32f - num3 - 1f, 15f + (float)Math.Sin(num2) * num4 * 0.8f));
				array3[i] = num3;
			}
			uint[] array4 = new uint[1024];
			for (int j = 0; j < 32; j++)
			{
				for (int k = 0; k < 32; k++)
				{
					float num5 = (float)k + 0.5f;
					float num6 = (float)j + 0.5f;
					float num7 = 0f;
					for (int l = 0; l < num; l++)
					{
						float num8 = (num5 - array[l]) / array3[l];
						float num9 = (num6 - array2[l]) / array3[l];
						float num10 = 1f - (num8 * num8 + num9 * num9);
						if (num10 > num7)
						{
							num7 = num10;
						}
					}
					if (!(num7 <= 0f))
					{
						float num11 = Math.Min(1f, num7 * 1.6f);
						num11 *= num11;
						int num12 = 170 + (int)(70f * (num6 / 32f)) + (int)(15f * num7);
						if (num12 > 255)
						{
							num12 = 255;
						}
						array4[j * 32 + k] = (TerrainCache.Rgb(num12, num12, num12) & 0xFFFFFF) | ((uint)(num11 * 255f) << 24);
					}
				}
			}
			return Image(32, 32, array4);
		}

		public static SpriteImage Spark(int size)
		{
			size = Math.Max(1, Math.Min(3, size));
			if (_sparks[size - 1] != null)
			{
				return _sparks[size - 1];
			}
			uint[] array = new uint[size * size];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = uint.MaxValue;
			}
			if (size == 3)
			{
				array[0] = (array[2] = (array[6] = (array[8] = 0u)));
			}
			return _sparks[size - 1] = Image(size, size, array);
		}

		public static Mat3 AlongAxis(Vec3 axis, float spin)
		{
			Vec3 vec = axis.Normalized();
			Vec3 vec2 = ((Math.Abs(vec.Z) < 0.9f) ? Vec3.Cross(new Vec3(0f, 0f, 1f), vec).Normalized() : (new Vec3(1f, 0f, 0f) - vec * vec.X).Normalized());
			Vec3 vec3 = Vec3.Cross(vec, vec2);
			float num = (float)Math.Cos(spin);
			float num2 = (float)Math.Sin(spin);
			Vec3 c = vec2 * num + vec3 * num2;
			Vec3 c2 = vec3 * num - vec2 * num2;
			return new Mat3(c, c2, vec);
		}
	}
}
