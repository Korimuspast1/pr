using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class FireField
	{
		private struct Flame
		{
			public Billboard B;

			public float D;
		}

		private const float Reach = 40f;

		[ThreadStatic]
		private static List<Flame> _scratch;

		public const int SmokeShades = 6;

		private static readonly SpriteImage[] SmokeImages = new SpriteImage[24];

		private static readonly uint AshDark = TerrainCache.Rgb(58, 54, 54);

		private static readonly uint AshPale = TerrainCache.Rgb(168, 166, 166);

		private static readonly SpriteImage[] SoftImages = new SpriteImage[24];

		public static void Add(List<Billboard> list, TerrainCache t, float eyeX, float eyeY, float fx, float fy, float cullTan, float time, int max, float reach = 0f)
		{
			float num = ((reach > 0f) ? reach : 40f);
			if (t.Width == 0)
			{
				return;
			}
			List<Flame> list2 = _scratch ?? (_scratch = new List<Flame>(512));
			list2.Clear();
			int num2 = Math.Max(0, (int)(eyeX - num));
			int num3 = Math.Min(t.Width - 1, (int)(eyeX + num));
			int num4 = Math.Max(0, (int)(eyeY - num));
			int num5 = Math.Min(t.Height - 1, (int)(eyeY + num));
			byte[] flags = t.Flags;
			for (int i = num4; i <= num5; i++)
			{
				int num6 = i * t.Width;
				for (int j = num2; j <= num3; j++)
				{
					if ((flags[num6 + j] & 4) == 0)
					{
						continue;
					}
					float num7 = (float)j + 0.5f;
					float num8 = (float)i + 0.5f;
					float num9 = num7 - eyeX;
					float num10 = num8 - eyeY;
					float num11 = num9 * num9 + num10 * num10;
					if (num11 > num * num)
					{
						continue;
					}
					float num12 = num9 * fx + num10 * fy;
					if (num12 < -1.5f || Math.Abs(num9 * fy - num10 * fx) > Math.Max(0f, num12) * cullTan + 2.5f)
					{
						continue;
					}
					float num13 = (float)Math.Sqrt(num11);
					uint num14 = TerrainModel.Hash(j * 3 + 11, i * 5 - 7);
					int num15 = ((num13 < 9f) ? 3 : ((!(num13 < 20f)) ? 1 : 2));
					if (num13 > 30f && (num14 & 1) != 0)
					{
						continue;
					}
					for (int k = 0; k < num15; k++)
					{
						uint num16 = TerrainModel.Hash((int)num14 + k * 7919, k * 31 + 5);
						float num17 = 0.15f + 0.7f * ((float)(num16 & 0xFF) / 255f);
						float num18 = 0.15f + 0.7f * ((float)((num16 >> 8) & 0xFF) / 255f);
						if (num15 == 1)
						{
							num17 = 0.5f;
							num18 = 0.5f;
						}
						float x = (float)j + num17;
						float y = (float)i + num18;
						float num19 = 0.85f + 0.15f * (float)Math.Sin(time * (2.6f + (float)(num16 >> 28) * 0.15f) + (float)(num16 & 0x3F));
						float num20 = (float)((num16 >> 16) & 0xFF) / 255f;
						float num21 = (0.35f + 0.85f * num20 * num20) * num19 * ((num15 == 1) ? 1.35f : 1f);
						SpriteImage spriteImage = Flames.At(time, num16, num13 > 24f);
						float num22 = t.SampleHeight(x, y);
						list2.Add(new Flame
						{
							D = num13,
							B = new Billboard
							{
								X = x,
								Y = y,
								Z = num22 - 0.04f,
								GroundZ = num22,
								Width = num21 * (float)spriteImage.Width / (float)spriteImage.Height * 1.2f,
								Height = num21,
								Image = spriteImage,
								Kind = BillboardKind.Facing,
								Flip = ((num16 & 0x40000) != 0),
								Emissive = true,
								Tag = -1
							}
						});
					}
					if (num13 < 26f)
					{
						AddEmbers(list, t, j, i, num14, time);
					}
					if ((num14 & 7) == 0)
					{
						AddSmoke(list, t, num7, num8, num14, time, num13);
					}
				}
			}
			if (list2.Count > max)
			{
				list2.Sort((Flame a, Flame b) => a.D.CompareTo(b.D));
				list2.RemoveRange(max, list2.Count - max);
			}
			for (int num23 = 0; num23 < list2.Count; num23++)
			{
				list.Add(list2[num23].B);
			}
		}

		public static void AddBlaze(List<Billboard> list, float cx, float cy, float halfX, float halfY, float z0, float z1, int count, float size, uint seed, float time)
		{
			for (int i = 0; i < count; i++)
			{
				uint num = TerrainModel.Hash((int)seed + i * 104729, i * 17 + 3);
				float num2 = (float)(num & 0x3FF) / 1023f * 2f - 1f;
				float num3 = (float)((num >> 10) & 0x3FF) / 1023f * 2f - 1f;
				float num4 = (float)((num >> 20) & 0x3FF) / 1023f;
				float num5 = 0.8f + 0.2f * (float)Math.Sin(time * (2.4f + (float)(num >> 29) * 0.2f) + (float)(num & 0x3F));
				float num6 = size * (0.7f + 0.5f * (float)((num >> 4) & 0xFF) / 255f) * num5;
				SpriteImage spriteImage = Flames.At(time, num);
				list.Add(new Billboard
				{
					X = cx + num2 * halfX,
					Y = cy + num3 * halfY,
					Z = z0 + (z1 - z0) * num4 - num6 * 0.15f,
					GroundZ = z0,
					Width = num6 * (float)spriteImage.Width / (float)spriteImage.Height * 1.25f,
					Height = num6,
					Image = spriteImage,
					Kind = BillboardKind.Facing,
					Flip = ((num & 0x200000) != 0),
					Emissive = true,
					Tag = -1
				});
			}
		}

		public static void AddPlume(List<Billboard> list, float cx, float cy, float z, float spread, int puffs, uint seed, float time, float dark)
		{
			for (int i = 0; i < puffs; i++)
			{
				uint num = TerrainModel.Hash((int)seed + i * 613, i * 29 + 1);
				float num2 = 4f + (float)((num >> 24) & 0xF) * 0.15f;
				float num3 = Frac(time / num2 + (float)(num & 0x3FF) / 1023f) * num2;
				float num4 = num3 / num2;
				float num5 = num3 * 0.3f;
				float num6 = (0.55f + spread * 0.35f) * (0.5f + 1.5f * num4);
				float x = cx + ((float)((num >> 10) & 0xFF) / 255f - 0.5f) * spread + num5;
				float y = cy + ((float)((num >> 18) & 0x3F) / 63f - 0.5f) * spread + num5 * 0.4f;
				float num7 = z + num3 * 0.75f;
				int shade = Math.Min(5, (int)((num4 * 0.65f + (1f - dark) * 0.3f) * 6f));
				float num8 = ((num4 < 0.12f) ? (num4 / 0.12f) : ((float)Math.Pow(1f - (num4 - 0.12f) / 0.88f, 1.3)));
				list.Add(new Billboard
				{
					X = x,
					Y = y,
					Z = num7 - num6 * 0.5f,
					GroundZ = num7 - num6,
					Width = num6,
					Height = num6,
					Image = SoftSmoke(shade, (int)(num & 3)),
					Kind = BillboardKind.Facing,
					Flip = ((num & 4) != 0),
					Tag = -1,
					Alpha = (byte)Math.Max(1, (int)(170f * num8))
				});
			}
		}

		private static void AddEmbers(List<Billboard> list, TerrainCache t, int x, int y, uint h, float time)
		{
			for (int i = 0; i < 2; i++)
			{
				uint num = TerrainModel.Hash((int)h + i * 97, i + 13);
				float num2 = Frac(time / 1.6f + (float)(num & 0x3FF) / 1023f) * 1.6f;
				float x2 = (float)x + 0.5f + ((float)((num >> 10) & 0xFF) / 255f - 0.5f) * 0.8f + (float)Math.Sin(num2 * 3f + (float)i) * 0.15f;
				float y2 = (float)y + 0.5f + ((float)((num >> 18) & 0xFF) / 255f - 0.5f) * 0.8f + (float)Math.Cos(num2 * 2.3f + (float)i) * 0.15f;
				float num3 = t.SampleHeight(x2, y2);
				float num4 = 0.05f * (1f - num2 / 1.6f * 0.6f);
				list.Add(new Billboard
				{
					X = x2,
					Y = y2,
					Z = num3 + 0.35f + num2 * 1.6f,
					GroundZ = num3,
					Width = num4,
					Height = num4,
					Image = Flames.Ember,
					Kind = BillboardKind.Facing,
					Emissive = true,
					Tag = -1
				});
			}
		}

		private static void AddSmoke(List<Billboard> list, TerrainCache t, float cx, float cy, uint h, float time, float d)
		{
			if (!(d > 36f))
			{
				AddPlume(list, cx, cy, t.SampleHeight(cx, cy) + 0.9f, 0.6f, (!(d < 14f)) ? 1 : 2, h, time, 0.7f);
			}
		}

		private static float Frac(float v)
		{
			return v - (float)Math.Floor(v);
		}

		public static SpriteImage SoftSmoke(int shade, int variant)
		{
			int num = shade * 4 + (variant & 3);
			if (SoftImages[num] != null)
			{
				return SoftImages[num];
			}
			uint c = TerrainCache.Lerp(AshDark, AshPale, (float)shade / 5f);
			uint[] array = new uint[1024];
			for (int i = 0; i < 32; i++)
			{
				for (int j = 0; j < 32; j++)
				{
					float num2 = ((float)j + 0.5f) / 32f * 2f - 1f;
					float num3 = ((float)i + 0.5f) / 32f * 2f - 1f;
					float num4 = (float)Math.Atan2(num3, num2);
					float num5 = 0.8f + 0.12f * (float)Math.Sin(num4 * 3f + (float)variant * 1.7f) + 0.07f * (float)Math.Sin(num4 * 5f + 0.8f + (float)variant * 2.3f);
					float num6 = (float)Math.Sqrt(num2 * num2 + num3 * num3) / num5;
					if (!(num6 > 1f))
					{
						float s = 1.04f + 0.14f * num3 - 0.1f * num6 * num6;
						float num7 = Math.Min(1f, (1f - num6) / 0.45f);
						uint num8 = (uint)(255f * num7 * num7 * (3f - 2f * num7));
						if (num8 != 0)
						{
							array[i * 32 + j] = (TerrainCache.Scale(c, s) & 0xFFFFFF) | (num8 << 24);
						}
					}
				}
			}
			SpriteImage spriteImage = new SpriteImage
			{
				Width = 32,
				Height = 32,
				Pixels = array,
				PivotX = 0.5f,
				UnitWidth = 1f,
				UnitHeight = 1f
			};
			spriteImage.MeasureOpaqueRows();
			SoftImages[num] = spriteImage;
			return spriteImage;
		}

		public static SpriteImage Smoke(int shade, int thin, int variant)
		{
			int num = (shade * 2 + thin) * 2 + variant;
			if (SmokeImages[num] != null)
			{
				return SmokeImages[num];
			}
			uint c = TerrainCache.Lerp(AshDark, AshPale, (float)shade / 5f);
			uint[] array = new uint[256];
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					float num2 = ((float)j + 0.5f) / 16f * 2f - 1f;
					float num3 = ((float)i + 0.5f) / 16f * 2f - 1f;
					float num4 = (float)Math.Atan2(num3, num2);
					float num5 = 0.78f + 0.12f * (float)Math.Sin(num4 * 3f + (float)variant * 1.7f) + 0.06f * (float)Math.Sin(num4 * 5f + 0.8f + (float)variant);
					float num6 = (float)Math.Sqrt(num2 * num2 + num3 * num3) / num5;
					bool flag = ((j + i) & 1) == 0;
					bool num7;
					if (thin != 0)
					{
						num7 = num6 < 0.9f && flag;
					}
					else
					{
						if (num6 < 0.72f)
						{
							goto IL_0129;
						}
						num7 = num6 < 1f && flag;
					}
					if (!num7)
					{
						continue;
					}
					goto IL_0129;
					IL_0129:
					float s = 1f + 0.18f * (0f - num2 - num3) * 0.7f - ((num6 > 0.72f) ? 0.06f : 0f);
					array[i * 16 + j] = TerrainCache.Scale(c, s);
				}
			}
			SpriteImage spriteImage = new SpriteImage
			{
				Width = 16,
				Height = 16,
				Pixels = array,
				PivotX = 0.5f,
				UnitWidth = 1f,
				UnitHeight = 1f
			};
			spriteImage.MeasureOpaqueRows();
			SmokeImages[num] = spriteImage;
			return spriteImage;
		}
	}
}
