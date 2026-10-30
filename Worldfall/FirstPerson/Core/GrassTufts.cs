using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class GrassTufts
	{
		public const float Range = 10f;

		private const int Variants = 6;

		private static SpriteImage[] _images;

		public static float WindX = 1f;

		public static float WindY;

		public static float WindStrength;

		public static float WindTime;

		private const int LeanLevels = 4;

		private static SpriteImage[] _leanRight;

		private static SpriteImage[] _leanLeft;

		public static void Add(List<Billboard> list, TerrainCache t, float eyeX, float eyeY, float fx, float fy, float cullTan)
		{
			if (t == null || t.Width == 0)
			{
				return;
			}
			if (_images == null)
			{
				Build();
			}
			bool flag = WindStrength > 0.03f;
			bool flag2 = t.SoftLandShadows && t.SunField != null;
			float num = 0f - fx;
			int num2 = Math.Max(0, (int)(eyeX - 10f));
			int num3 = Math.Min(t.Width - 1, (int)(eyeX + 10f));
			int num4 = Math.Max(0, (int)(eyeY - 10f));
			int num5 = Math.Min(t.Height - 1, (int)(eyeY + 10f));
			for (int i = num4; i <= num5; i++)
			{
				for (int j = num2; j <= num3; j++)
				{
					int num6 = i * t.Width + j;
					if (t.Materials[num6] != 1 || (t.Flags[num6] & 0xD) != 0)
					{
						continue;
					}
					uint num7 = TerrainModel.Hash(j * 3 + 11, i * 5 - 7);
					int num8 = (int)(2 + (num7 & 3));
					for (int k = 0; k < num8; k++)
					{
						uint num9 = TerrainModel.Hash((int)num7 + k * 97, k * 31 + j);
						float num10 = (float)j + ((float)(num9 & 0xFF) + 0.5f) / 256f;
						float num11 = (float)i + ((float)((num9 >> 8) & 0xFF) + 0.5f) / 256f;
						float num12 = num10 - eyeX;
						float num13 = num11 - eyeY;
						float num14 = num12 * num12 + num13 * num13;
						if (num14 > 100f || num14 < 0.04f || (t.PathSlot.Length != 0 && t.PathSlot[num6] != 0 && t.PathWear(num10, num11) > 40))
						{
							continue;
						}
						float num15 = num12 * fx + num13 * fy;
						if (num15 < -0.5f || Math.Abs(num12 * fy - num13 * fx) > Math.Max(0f, num15) * cullTan + 0.6f)
						{
							continue;
						}
						float num16 = (float)Math.Sqrt(num14);
						float num17 = Math.Min(1f, (10f - num16) / 3.5f);
						float num18 = (0.7f + 0.6f * (float)((num9 >> 16) & 0xFF) / 255f) * num17;
						if (num18 < 0.08f)
						{
							continue;
						}
						float num19 = t.SampleHeight(num10, num11);
						uint c = t.Colors[num6];
						float s = (((t.Flags[num6] & 0x20) != 0 && !flag2) ? 0.55f : (0.7f + 0.3f * (float)(int)t.Sun[num6] / 255f));
						int num20 = (int)((num9 >> 24) % 6);
						float num21 = (float)((num9 >> 5) & 0xFF) / 255f * 3.14159f;
						SpriteImage image = _images[num20];
						float num22 = 1f;
						if (flag)
						{
							float num23 = 0.5f + 0.5f * (float)Math.Sin((num10 * WindX + num11 * WindY) * 0.85f - WindTime * 3.1f + (float)((num9 >> 12) & 0xF) * 0.05f);
							float num24 = WindStrength * (0.3f + 0.7f * num23 * num23);
							float num25 = (float)Math.Cos(num21);
							float num26 = (float)Math.Sin(num21);
							if (Math.Abs(num25 * fy + num26 * num) < 0.7071f)
							{
								float num27 = num25;
								num25 = 0f - num26;
								num26 = num27;
							}
							float num28 = WindX * num25 + WindY * num26;
							int num29 = Math.Min(4, (int)(num24 * Math.Abs(num28) * 4f + 0.5f));
							if (num29 > 0)
							{
								image = ((num28 > 0f) ? _leanRight : _leanLeft)[(num29 - 1) * 6 + num20];
							}
							num22 = 1f - 0.22f * num24;
						}
						list.Add(new Billboard
						{
							X = num10,
							Y = num11,
							Z = num19 - 0.02f,
							GroundZ = num19,
							Width = 0.3f * num18,
							Height = 0.24f * num18 * num22,
							Image = image,
							Kind = BillboardKind.Cross,
							Angle = num21,
							Multiply = TerrainCache.Scale(c, s),
							Tag = -1
						});
					}
				}
			}
		}

		private static void Build()
		{
			_images = new SpriteImage[6];
			_leanRight = new SpriteImage[24];
			_leanLeft = new SpriteImage[24];
			Random random = new Random(5);
			for (int i = 0; i < 6; i++)
			{
				int num = 6 + random.Next(5);
				float[] array = new float[num];
				float[] array2 = new float[num];
				int[] array3 = new int[num];
				for (int j = 0; j < num; j++)
				{
					array[j] = 7.5f + (float)(random.NextDouble() - 0.5) * 5f;
					array2[j] = (float)(random.NextDouble() - 0.5) * 10f;
					array3[j] = 6 + random.Next(6);
				}
				_images[i] = Tuft(array, array2, array3, 0f);
				for (int k = 1; k <= 4; k++)
				{
					float num2 = (float)k / 4f;
					_leanRight[(k - 1) * 6 + i] = Tuft(array, array2, array3, num2);
					_leanLeft[(k - 1) * 6 + i] = Tuft(array, array2, array3, 0f - num2);
				}
			}
		}

		private static SpriteImage Tuft(float[] roots, float[] leans, int[] lens, float bend)
		{
			uint[] array = new uint[180];
			for (int i = 0; i < roots.Length; i++)
			{
				float num = roots[i];
				float num2 = leans[i] * (1f - 0.6f * Math.Abs(bend)) + bend * 9f;
				int num3 = Math.Max(3, (int)Math.Round((float)lens[i] * (1f - 0.3f * Math.Abs(bend))));
				for (int j = 0; j < num3; j++)
				{
					float num4 = (float)j / (float)(num3 - 1);
					int num5 = (int)Math.Round(num + num2 * num4 * num4);
					if (num5 >= 0 && num5 < 15)
					{
						int num6 = (int)(130f + 125f * num4);
						if ((i & 3) == 0)
						{
							num6 = Math.Max(110, num6 - 30);
						}
						array[j * 15 + num5] = TerrainCache.Rgb(num6, num6, num6);
						if (j < num3 / 3 && num5 + 1 < 15 && array[j * 15 + num5 + 1] == 0)
						{
							array[j * 15 + num5 + 1] = TerrainCache.Rgb(num6 - 10, num6 - 10, num6 - 10);
						}
					}
				}
			}
			return new SpriteImage
			{
				Width = 15,
				Height = 12,
				Pixels = array,
				PivotX = 0.5f,
				UnitWidth = 1f,
				UnitHeight = 1f,
				OpaqueBottom = 0,
				OpaqueTop = 12
			};
		}
	}
}
