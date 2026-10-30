using System;

namespace FirstPerson.Core
{
	public static class PowerFx
	{
		public static readonly uint RayEdge = TerrainCache.Rgb(255, 60, 0);

		public static readonly uint RayOrange = TerrainCache.Rgb(255, 119, 0);

		public static readonly uint RayAmber = TerrainCache.Rgb(255, 181, 43);

		public static readonly uint RayCream = TerrainCache.Rgb(255, 248, 194);

		private static readonly int[] Bayer4 = new int[16]
		{
			0, 8, 2, 10, 12, 4, 14, 6, 3, 11,
			1, 9, 15, 7, 13, 5
		};

		public const int BeamFrames = 8;

		private const int BeamW = 21;

		private const int BeamH = 48;

		private static SpriteImage[] _beam;

		private static SpriteImage _halo;

		private static SpriteImage _core;

		private static SpriteImage _cross;

		private static SpriteImage _dash;

		private static SpriteImage _line;

		public const int PuffVariants = 6;

		private static SpriteImage[] _dark;

		private static SpriteImage[] _rim;

		private static SpriteImage[] _wisps;

		public static SpriteImage BeamHalo
		{
			get
			{
				if (_halo != null)
				{
					return _halo;
				}
				uint[] array = new uint[96];
				for (int i = 0; i < 24; i++)
				{
					float num = Math.Abs(((float)i + 0.5f) / 24f - 0.5f) * 2f;
					float a = (float)Math.Pow(1f - num, 1.600000023841858);
					uint c = TerrainCache.Lerp(TerrainCache.Rgb(255, 150, 40), TerrainCache.Rgb(255, 70, 10), num);
					for (int j = 0; j < 4; j++)
					{
						array[j * 24 + i] = WithAlpha(c, a);
					}
				}
				return _halo = Image(24, 4, array);
			}
		}

		public static SpriteImage BeamCore
		{
			get
			{
				if (_core != null)
				{
					return _core;
				}
				uint[] array = new uint[32];
				for (int i = 0; i < 8; i++)
				{
					float num = Math.Abs(((float)i + 0.5f) / 8f - 0.5f) * 2f;
					for (int j = 0; j < 4; j++)
					{
						array[j * 8 + i] = WithAlpha(TerrainCache.Rgb(255, 252, 230), (1f - num) * (1f - num));
					}
				}
				return _core = Image(8, 4, array);
			}
		}

		public static SpriteImage HeatCross
		{
			get
			{
				if (_cross != null)
				{
					return _cross;
				}
				uint[] array = new uint[1681];
				for (int i = 0; i < 41; i++)
				{
					for (int j = 0; j < 41; j++)
					{
						int num = Math.Abs(j - 20);
						int num2 = Math.Abs(i - 20);
						int num3 = num + num2;
						uint num4 = 0u;
						if (num3 <= 2)
						{
							num4 = WithAlpha(RayCream, 1f);
						}
						else if (num3 <= 4)
						{
							num4 = WithAlpha(RayAmber, 1f);
						}
						else if (num3 <= 5)
						{
							num4 = WithAlpha(RayOrange, 0.8f);
						}
						else if (num == 0 || num2 == 0)
						{
							int num5 = Math.Max(num, num2);
							if (num5 <= 11 || (num5 <= 15 && num5 % 3 != 0) || (num5 <= 20 && num5 % 4 == 1))
							{
								num4 = WithAlpha((num5 < 8) ? RayCream : RayAmber, 1f - (float)num5 / 24f);
							}
						}
						array[i * 41 + j] = num4;
					}
				}
				return _cross = Image(41, 41, array);
			}
		}

		public static SpriteImage Dash
		{
			get
			{
				if (_dash != null)
				{
					return _dash;
				}
				uint[] array = new uint[24];
				for (int i = 0; i < 3; i++)
				{
					for (int j = 0; j < 8; j++)
					{
						float val = Math.Min((float)j + 0.5f, (float)(8 - j) - 0.5f) / 2f;
						float num = ((i == 1) ? 1f : 0.45f);
						array[i * 8 + j] = WithAlpha(TerrainCache.Rgb(255, 255, 255), Math.Min(1f, val) * num);
					}
				}
				return _dash = Image(8, 3, array);
			}
		}

		public static SpriteImage Dot
		{
			get
			{
				if (_line != null)
				{
					return _line;
				}
				uint[] array = new uint[36];
				for (int i = 0; i < 6; i++)
				{
					for (int j = 0; j < 6; j++)
					{
						float num = ((float)j + 0.5f) / 6f * 2f - 1f;
						float num2 = ((float)i + 0.5f) / 6f * 2f - 1f;
						float num3 = (float)Math.Sqrt(num * num + num2 * num2);
						if (num3 < 1f)
						{
							array[i * 6 + j] = WithAlpha(TerrainCache.Rgb(255, 255, 255), Math.Min(1f, (1f - num3) * 1.8f));
						}
					}
				}
				return _line = Image(6, 6, array);
			}
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

		private static uint WithAlpha(uint c, float a)
		{
			return (c & 0xFFFFFF) | (uint)(Math.Max(0, Math.Min(255, (int)(a * 255f))) << 24);
		}

		private static float BayerAt(int x, int y)
		{
			return ((float)Bayer4[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;
		}

		public static SpriteImage Beam(int frame)
		{
			if (_beam == null)
			{
				Random random = new Random(41);
				uint[] array = new uint[1008];
				for (int i = 0; i < 48; i++)
				{
					for (int j = 0; j < 21; j++)
					{
						array[i * 21 + j] = BeamBase(j);
					}
				}
				for (int k = 0; k < 26; k++)
				{
					int num = 2 + random.Next(17);
					int num2 = random.Next(48);
					int num3 = 5 + random.Next(16);
					float num4 = Math.Abs(((float)num + 0.5f) / 21f - 0.5f) * 2f;
					uint b = ((num4 < 0.45f) ? RayCream : ((num4 < 0.75f) ? RayAmber : RayOrange));
					for (int l = 0; l < num3; l++)
					{
						int num5 = (num2 + l) % 48;
						uint num6 = array[num5 * 21 + num];
						float t = ((l < 2) ? 0.5f : ((l > num3 - 3) ? 0.6f : 1f));
						array[num5 * 21 + num] = (TerrainCache.Lerp(num6, b, t) & 0xFFFFFF) | (num6 & 0xFF000000u) | (uint)((num4 > 0.75f) ? (-1073741824) : 0);
					}
				}
				for (int m = 0; m < 7; m++)
				{
					int num7 = 5 + random.Next(11);
					int num8 = random.Next(48);
					int num9 = 2 + random.Next(4);
					for (int n = 0; n < num9; n++)
					{
						int num10 = (num8 + n) % 48;
						uint a = array[num10 * 21 + num7];
						array[num10 * 21 + num7] = (TerrainCache.Lerp(a, TerrainCache.Rgb(150, 34, 8), 0.75f) & 0xFFFFFF) | 0xFF000000u;
						if (n == 0 && num7 + 1 < 21)
						{
							array[num10 * 21 + num7 + 1] = (TerrainCache.Lerp(array[num10 * 21 + num7 + 1], TerrainCache.Rgb(190, 50, 10), 0.5f) & 0xFFFFFF) | 0xFF000000u;
						}
					}
				}
				_beam = new SpriteImage[8];
				for (int num11 = 0; num11 < 8; num11++)
				{
					uint[] array2 = new uint[1008];
					int num12 = num11 * 48 / 8;
					for (int num13 = 0; num13 < 48; num13++)
					{
						Array.Copy(array, (num13 + num12) % 48 * 21, array2, num13 * 21, 21);
					}
					_beam[num11] = Image(21, 48, array2);
				}
			}
			return _beam[(frame % 8 + 8) % 8];
		}

		private static uint BeamBase(int x)
		{
			float num = Math.Abs(((float)x + 0.5f) / 21f - 0.5f) * 2f;
			if (num < 0.2f)
			{
				return WithAlpha(RayCream, 1f);
			}
			if (num < 0.47f)
			{
				return WithAlpha(TerrainCache.Lerp(RayAmber, RayCream, (num < 0.3f) ? 0.35f : 0f), 1f);
			}
			if (num < 0.76f)
			{
				return WithAlpha(RayOrange, 1f);
			}
			return WithAlpha(RayEdge, 0.55f);
		}

		public static SpriteImage DarkPuff(int variant)
		{
			BuildPuffs();
			return _dark[(variant % 6 + 6) % 6];
		}

		public static SpriteImage FireRim(int variant)
		{
			BuildPuffs();
			return _rim[(variant % 6 + 6) % 6];
		}

		private static void BuildPuffs()
		{
			if (_dark == null)
			{
				_dark = new SpriteImage[6];
				_rim = new SpriteImage[6];
				for (int i = 0; i < 6; i++)
				{
					MakeLitPuff(i, out _dark[i], out _rim[i]);
				}
			}
		}

		private static void MakeLitPuff(int v, out SpriteImage dark, out SpriteImage rim)
		{
			Random random = new Random(v * 389 + 5);
			int count = 5 + v % 3;
			float[] cx = new float[count];
			float[] cy = new float[count];
			float[] cr = new float[count];
			cx[0] = 16f;
			cy[0] = 13f;
			cr[0] = 8.5f + (float)random.NextDouble() * 2f;
			for (int i = 1; i < count; i++)
			{
				double num = random.NextDouble() * Math.PI * 2.0;
				float num2 = 4f + (float)random.NextDouble() * 4.5f;
				float num3 = 5f + (float)random.NextDouble() * 4.5f;
				cx[i] = Math.Max(num2 + 0.5f, Math.Min(32f - num2 - 0.5f, 16f + (float)Math.Cos(num) * num3));
				cy[i] = Math.Max(num2 + 0.5f, Math.Min(32f - num2 - 0.5f, 14f + (float)Math.Sin(num) * num3 * 0.75f + 1f));
				cr[i] = num2;
			}
			uint[] array = new uint[1024];
			uint[] array2 = new uint[1024];
			for (int j = 0; j < 32; j++)
			{
				for (int k = 0; k < 32; k++)
				{
					float num4 = (float)k + 0.5f;
					float num5 = (float)j + 0.5f;
					float num6 = H(num4, num5);
					if (!(num6 < 0f) && (!(num6 < 1.6f) || !(BayerAt(k, j) > num6 / 1.6f)))
					{
						float num7 = H(num4 + 1f, num5);
						float num8 = H(num4, num5 + 1f);
						if (num7 < 0f)
						{
							num7 = num6 - 1.5f;
						}
						if (num8 < 0f)
						{
							num8 = num6 - 1.5f;
						}
						float num9 = num6 - num7;
						float num10 = num6 - num8;
						float num11 = 1f;
						float num12 = 1f / (float)Math.Sqrt(num9 * num9 + num10 * num10 + num11 * num11);
						float num13 = (num9 * -0.45f + num10 * 0.55f + num11 * 0.7f) * num12;
						float num14 = (num10 * -0.85f + num11 * 0.45f) * num12;
						int num15 = ((num13 > 0.8f) ? 3 : ((num13 > 0.55f) ? 2 : ((num13 > 0.25f) ? 1 : 0)));
						uint[] array3 = new uint[4]
						{
							TerrainCache.Rgb(28, 25, 31),
							TerrainCache.Rgb(44, 40, 48),
							TerrainCache.Rgb(66, 60, 70),
							TerrainCache.Rgb(96, 90, 100)
						};
						array[j * 32 + k] = array3[num15];
						float num16 = 1f - num5 / 32f;
						if (num14 > 0.5f && num16 > 0.28f)
						{
							array2[j * 32 + k] = WithAlpha(TerrainCache.Rgb(255, 255, 255), 1f);
						}
						else if (num14 > 0.3f && num16 > 0.25f)
						{
							array2[j * 32 + k] = WithAlpha(TerrainCache.Rgb(255, 255, 255), (BayerAt(k, j) < 0.5f) ? 0.7f : 0.35f);
						}
					}
				}
			}
			dark = Image(32, 32, array);
			rim = Image(32, 32, array2);
			float H(float x, float y)
			{
				float num17 = -1f;
				for (int l = 0; l < count; l++)
				{
					float num18 = x - cx[l];
					float num19 = y - cy[l];
					float num20 = cr[l] * cr[l] - num18 * num18 - num19 * num19;
					if (num20 > 0f)
					{
						num17 = Math.Max(num17, (float)Math.Sqrt(num20) + cr[l] * 0.15f);
					}
				}
				return num17;
			}
		}

		public static SpriteImage Wisp(int variant)
		{
			if (_wisps == null)
			{
				_wisps = new SpriteImage[3];
				for (int i = 0; i < 3; i++)
				{
					uint[] array = new uint[480];
					float num = 3f + (float)i * 1.5f;
					float num2 = 2.9f - (float)i * 0.45f;
					for (int j = 0; j < 12; j++)
					{
						for (int k = 0; k < 40; k++)
						{
							float num3 = ((float)k + 0.5f) / 40f;
							float num4 = 3f + num * (float)Math.Sin((double)num3 * Math.PI);
							float num5 = (float)Math.Sin((double)num3 * Math.PI);
							float num6 = Math.Abs((float)j + 0.5f - num4) / Math.Max(0.3f, num2 * num5);
							if (!(num6 > 1f))
							{
								float a = (1f - num6 * num6) * (0.35f + 0.65f * num3) * Math.Min(1f, num5 * 2f);
								array[j * 40 + k] = WithAlpha(TerrainCache.Rgb(255, 255, 255), a);
							}
						}
					}
					_wisps[i] = Image(40, 12, array);
				}
			}
			return _wisps[(variant % 3 + 3) % 3];
		}

		public static float Rand(uint seed, int i)
		{
			return BlastFx.Rand(seed, i);
		}

		public static float Clamp01(float v)
		{
			return BlastFx.Clamp01(v);
		}
	}
}
