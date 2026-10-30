using System;

namespace FirstPerson.Core
{
	public static class ShotgunFx
	{
		public enum Surface : byte
		{
			Ground,
			Wall,
			Water,
			Body
		}

		public const int FlashFrames = 3;

		public const int FlashVariants = 3;

		public const float FlashFrameTime = 0.032f;

		private static readonly SpriteImage[,] Stars = new SpriteImage[3, 3];

		private static readonly SpriteImage[,] Tongues = new SpriteImage[3, 3];

		private static readonly uint White = TerrainCache.Rgb(255, 254, 240);

		private static readonly uint Pale = TerrainCache.Rgb(255, 242, 168);

		private static readonly uint Yellow = TerrainCache.Rgb(255, 214, 86);

		private static readonly uint Orange = TerrainCache.Rgb(255, 150, 46);

		private static readonly uint Red = TerrainCache.Rgb(222, 74, 34);

		private static readonly uint Dark = TerrainCache.Rgb(140, 40, 26);

		public static readonly uint PelletHead = TerrainCache.Rgb(255, 240, 150);

		public static readonly uint PelletTail = TerrainCache.Rgb(255, 150, 54);

		public static readonly uint SmokeNear = TerrainCache.Rgb(170, 164, 160);

		public static readonly uint SmokeFar = TerrainCache.Rgb(214, 210, 206);

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

		public static SpriteImage FlashStar(int frame, int variant)
		{
			frame = Math.Max(0, Math.Min(2, frame));
			variant = (variant % 3 + 3) % 3;
			if (Stars[frame, variant] != null)
			{
				return Stars[frame, variant];
			}
			uint[] array = new uint[729];
			Random random = new Random(variant * 131 + frame * 17 + 5);
			float num = 13f;
			int num2 = 7 + variant % 3;
			float[] array2 = new float[num2];
			float[] array3 = new float[num2];
			float[] array4 = new float[num2];
			float num3 = (float)random.NextDouble() * 6.2832f;
			for (int i = 0; i < num2; i++)
			{
				array2[i] = num3 + ((float)i + (float)random.NextDouble() * 0.5f - 0.25f) / (float)num2 * 6.2832f;
				float num4 = ((i % 2 == 0) ? 1f : 0.62f);
				int num5 = i;
				float num6;
				switch (frame)
				{
				default:
					num6 = 6f;
					break;
				case 1:
					num6 = 9.5f;
					break;
				case 0:
					num6 = 12.5f;
					break;
				}
				array3[num5] = num6 * num4 * (0.78f + 0.3f * (float)random.NextDouble());
				array4[i] = ((frame == 0) ? 2.3f : 1.7f) * (0.85f + 0.3f * (float)random.NextDouble());
			}
			float num7;
			switch (frame)
			{
			default:
				num7 = 1.6f;
				break;
			case 1:
				num7 = 3.2f;
				break;
			case 0:
				num7 = 4.6f;
				break;
			}
			float num8 = num7;
			for (int j = 0; j < 27; j++)
			{
				for (int k = 0; k < 27; k++)
				{
					float num9 = (float)k - num;
					float num10 = (float)j - num;
					float num11 = (float)Math.Sqrt(num9 * num9 + num10 * num10);
					float num12 = (float)Math.Atan2(num10, num9);
					float num13 = Math.Max(0f, 1f - num11 / num8);
					for (int l = 0; l < num2; l++)
					{
						float num14 = num12 - array2[l];
						while ((double)num14 > Math.PI)
						{
							num14 -= 6.2832f;
						}
						for (; (double)num14 < -Math.PI; num14 += 6.2832f)
						{
						}
						float num15 = Math.Abs(num14) * Math.Max(num11, 1f);
						float num16 = array4[l] * (1f - num11 / array3[l]);
						if (!(num16 <= 0f) && !(num15 > num16))
						{
							num13 = Math.Max(num13, (1f - num11 / array3[l]) * (1f - 0.5f * num15 / num16) * 0.95f);
						}
					}
					if (frame == 2)
					{
						num13 *= 0.8f;
					}
					if (!(num13 <= 0.04f) && (frame <= 0 || !(random.NextDouble() < ((frame == 1) ? 0.12 : 0.35) * (double)(1f - num13))))
					{
						uint num17 = ((num13 > 0.72f) ? White : ((num13 > 0.5f) ? Pale : ((num13 > 0.3f) ? Yellow : ((num13 > 0.15f) ? Orange : Red))));
						if (frame == 2)
						{
							num17 = ((num13 > 0.45f) ? Yellow : ((num13 > 0.2f) ? Orange : Red));
						}
						array[j * 27 + k] = num17 | 0xFF000000u;
					}
				}
			}
			int num18;
			switch (frame)
			{
			default:
				num18 = 6;
				break;
			case 1:
				num18 = 9;
				break;
			case 0:
				num18 = 7;
				break;
			}
			int num19 = num18;
			for (int m = 0; m < num19; m++)
			{
				double num20 = random.NextDouble() * 6.2832;
				float num21 = ((frame == 0) ? 7f : 6f) + (float)random.NextDouble() * ((frame == 0) ? 5f : 6.5f);
				int num22 = (int)Math.Round((double)num + Math.Cos(num20) * (double)num21);
				int num23 = (int)Math.Round((double)num + Math.Sin(num20) * (double)num21);
				if (num22 >= 0 && num23 >= 0 && num22 < 27 && num23 < 27 && array[num23 * 27 + num22] == 0)
				{
					array[num23 * 27 + num22] = ((random.NextDouble() < 0.5) ? Orange : ((frame == 2) ? Dark : Yellow)) | 0xFF000000u;
				}
			}
			return Stars[frame, variant] = Image(27, 27, array);
		}

		public static SpriteImage FlashTongue(int frame, int variant)
		{
			frame = Math.Max(0, Math.Min(2, frame));
			variant = (variant % 3 + 3) % 3;
			if (Tongues[frame, variant] != null)
			{
				return Tongues[frame, variant];
			}
			uint[] array = new uint[510];
			Random random = new Random(variant * 977 + frame * 31 + 3);
			float num = 7f;
			float num2;
			switch (frame)
			{
			default:
				num2 = 13f;
				break;
			case 1:
				num2 = 24f;
				break;
			case 0:
				num2 = 33f;
				break;
			}
			float num3 = num2;
			float[] array2 = new float[6];
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i] = (float)random.NextDouble();
			}
			for (int j = 0; j < 34; j++)
			{
				float num4 = (float)j / num3;
				if (num4 > 1f)
				{
					break;
				}
				float num5 = 0.75f + 0.5f * array2[Math.Min(array2.Length - 1, (int)(num4 * (float)array2.Length))];
				float num6 = ((frame == 2) ? 3.2f : 5.6f) * (1f - num4 * num4) * num5 + 0.6f;
				float num7 = (array2[j / 6 % array2.Length] - 0.5f) * 1.6f * num4;
				for (int k = 0; k < 15; k++)
				{
					float num8 = Math.Abs((float)k - num - num7);
					if (num8 > num6)
					{
						continue;
					}
					float num9 = (1f - num8 / num6) * (1f - num4) * 1.25f;
					if (frame <= 0 || !(random.NextDouble() < ((frame == 1) ? 0.1 : 0.3) * (double)(1f - num9)))
					{
						uint num10 = ((num9 > 0.85f) ? White : ((num9 > 0.6f) ? Pale : ((num9 > 0.35f) ? Yellow : ((num9 > 0.15f) ? Orange : Red))));
						if (frame == 2)
						{
							num10 = ((num9 > 0.5f) ? Yellow : ((num9 > 0.2f) ? Orange : Red));
						}
						array[k * 34 + j] = num10 | 0xFF000000u;
					}
				}
			}
			return Tongues[frame, variant] = Image(34, 15, array);
		}

		public static void MuzzleSmoke(FxParticles parts, Vec3 muzzle, Vec3 dir, float scale, uint seed, float heat = 1f)
		{
			if (parts != null)
			{
				dir = dir.Normalized();
				Vec3 vec = ((Math.Abs(dir.Z) < 0.95f) ? Vec3.Cross(dir, new Vec3(0f, 0f, 1f)).Normalized() : new Vec3(1f, 0f, 0f));
				Vec3 vec2 = Vec3.Cross(vec, dir);
				int num = 6;
				for (int i = 0; i < num; i++)
				{
					float num2 = (float)i / (float)(num - 1);
					float num3 = (0.4f + 2.2f * (1f - num2) * (0.7f + 0.6f * BlastFx.Rand(seed, i))) * scale;
					Vec3 vel = dir * num3 + vec * ((BlastFx.Rand(seed, i + 40) - 0.5f) * 0.7f * scale) + vec2 * ((BlastFx.Rand(seed, i + 80) - 0.2f) * 0.5f * scale);
					float num4 = (0.03f + 0.025f * num2) * scale;
					parts.Add(new FxParticles.P
					{
						Pos = muzzle + dir * (0.02f * scale),
						Vel = vel,
						Life = (0.45f + 0.45f * BlastFx.Rand(seed, i + 120)) * (0.75f + 0.25f * heat),
						Size = num4,
						EndSize = num4 * (4f + 1.5f * BlastFx.Rand(seed, i + 160)),
						Color = TerrainCache.Lerp(SmokeNear, TerrainCache.Rgb(130, 122, 118), 0.25f * (1f - num2)),
						EndColor = SmokeFar,
						Alpha = (byte)(105f + 45f * (1f - num2)),
						Variant = (int)(BlastFx.Rand(seed, i + 200) * 6f),
						Drag = 3.2f,
						Gravity = -0.3f,
						Kind = FxParticles.Kind.Smoke
					});
				}
				int num5 = 6;
				for (int j = 0; j < num5; j++)
				{
					float num6 = 0.28f;
					Vec3 vec3 = (dir + vec * ((BlastFx.Rand(seed, j + 300) - 0.5f) * num6 * 2f) + vec2 * ((BlastFx.Rand(seed, j + 340) - 0.4f) * num6 * 2f)).Normalized();
					float num7 = (6f + 9f * BlastFx.Rand(seed, j + 380)) * scale;
					uint color = ((BlastFx.Rand(seed, j + 420) < 0.5f) ? TerrainCache.Rgb(255, 226, 120) : TerrainCache.Rgb(255, 160, 60));
					parts.Add(new FxParticles.P
					{
						Pos = muzzle + dir * (0.03f * scale),
						Vel = vec3 * num7,
						Life = 0.07f + 0.13f * BlastFx.Rand(seed, j + 460),
						Size = 0.05f * scale,
						EndSize = 0.025f * scale,
						Color = color,
						EndColor = TerrainCache.Rgb(200, 60, 30),
						Gravity = 6f,
						Drag = 1.6f,
						Kind = FxParticles.Kind.Spark,
						Bounce = true
					});
				}
			}
		}

		public static void Wisp(FxParticles parts, Vec3 at, Vec3 dir, float scale, uint seed, byte alpha = 110)
		{
			if (parts != null)
			{
				for (int i = 0; i < 2; i++)
				{
					float num = 0.02f * scale;
					parts.Add(new FxParticles.P
					{
						Pos = at,
						Vel = dir * (0.35f * scale) + new Vec3((BlastFx.Rand(seed, i) - 0.5f) * 0.1f, (BlastFx.Rand(seed, i + 9) - 0.5f) * 0.1f, 0.25f) * scale,
						Life = 0.9f + 0.5f * BlastFx.Rand(seed, i + 3),
						Size = num,
						EndSize = num * 6f,
						Color = SmokeNear,
						EndColor = SmokeFar,
						Alpha = alpha,
						Variant = (int)(BlastFx.Rand(seed, i + 5) * 6f),
						Drag = 1.8f,
						Gravity = -0.3f,
						Kind = FxParticles.Kind.Smoke
					});
				}
			}
		}

		public static void Impact(FxParticles parts, Vec3 at, Vec3 back, Surface surface, uint color, uint seed)
		{
			if (parts == null)
			{
				return;
			}
			back = back.Normalized();
			switch (surface)
			{
			case Surface.Water:
			{
				for (int j = 0; j < 5; j++)
				{
					Vec3 vel = new Vec3((BlastFx.Rand(seed, j) - 0.5f) * 1.2f, (BlastFx.Rand(seed, j + 7) - 0.5f) * 1.2f, 2.2f + 1.6f * BlastFx.Rand(seed, j + 14));
					parts.Add(new FxParticles.P
					{
						Pos = at,
						Vel = vel,
						Life = 0.45f,
						Size = 0.05f,
						EndSize = 0.03f,
						Color = TerrainCache.Rgb(226, 240, 250),
						EndColor = TerrainCache.Rgb(170, 200, 230),
						Gravity = 9.8f,
						Drag = 0.5f,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
				FxParticles.P p = new FxParticles.P
				{
					Pos = at,
					Vel = new Vec3(0f, 0f, 0.25f),
					Life = 0.4f,
					Size = 0.06f,
					EndSize = 0.22f,
					Color = TerrainCache.Rgb(232, 242, 248),
					EndColor = TerrainCache.Rgb(200, 220, 236),
					Alpha = 150,
					Variant = (int)(seed % 6),
					Drag = 2f,
					Kind = FxParticles.Kind.Steam
				};
				parts.Add(in p);
				return;
			}
			case Surface.Body:
			{
				FxParticles.P p = new FxParticles.P
				{
					Pos = at,
					Vel = back * 0.4f,
					Life = 0.16f,
					Size = 0.07f,
					EndSize = 0.03f,
					Color = TerrainCache.Rgb(255, 250, 220),
					EndColor = TerrainCache.Rgb(255, 170, 80),
					Alpha = 220,
					Kind = FxParticles.Kind.Glow
				};
				parts.Add(in p);
				for (int i = 0; i < 3; i++)
				{
					Vec3 vec = (back + new Vec3(BlastFx.Rand(seed, i) - 0.5f, BlastFx.Rand(seed, i + 5) - 0.5f, BlastFx.Rand(seed, i + 9) * 0.8f)).Normalized();
					parts.Spark(at, vec * (2.5f + 2f * BlastFx.Rand(seed, i + 13)), 0.14f, 0.04f, TerrainCache.Rgb(255, 236, 160), TerrainCache.Rgb(255, 120, 60), 7f, 1.2f);
				}
				return;
			}
			}
			bool flag = surface == Surface.Wall;
			uint num = TerrainCache.Lerp(TerrainCache.Rgb(150, 134, 112), color, 0.55f);
			for (int k = 0; k < 2; k++)
			{
				float num2 = 0.05f + 0.03f * (float)k;
				parts.Add(new FxParticles.P
				{
					Pos = at + back * 0.03f,
					Vel = back * (0.6f + 0.5f * BlastFx.Rand(seed, k)) + new Vec3(0f, 0f, 0.5f + 0.4f * BlastFx.Rand(seed, k + 3)),
					Life = 0.55f + 0.4f * BlastFx.Rand(seed, k + 6),
					Size = num2,
					EndSize = num2 * 3.4f,
					Color = TerrainCache.Scale(num, 1.1f),
					EndColor = TerrainCache.Lerp(num, TerrainCache.Rgb(200, 192, 180), 0.4f),
					Alpha = 170,
					Variant = (int)(BlastFx.Rand(seed, k + 9) * 6f),
					Drag = 2.4f,
					Gravity = 0.4f,
					Kind = FxParticles.Kind.Dust
				});
			}
			for (int l = 0; l < 4; l++)
			{
				Vec3 vec2 = (back * 0.8f + new Vec3((BlastFx.Rand(seed, l + 20) - 0.5f) * 1.4f, (BlastFx.Rand(seed, l + 24) - 0.5f) * 1.4f, 0.9f + BlastFx.Rand(seed, l + 28))).Normalized();
				parts.Add(new FxParticles.P
				{
					Pos = at + back * 0.02f,
					Vel = vec2 * (1.6f + 1.8f * BlastFx.Rand(seed, l + 32)),
					Life = 0.5f + 0.3f * BlastFx.Rand(seed, l + 36),
					Size = 0.045f,
					EndSize = 0.04f,
					Color = TerrainCache.Scale(color, 0.8f),
					EndColor = TerrainCache.Scale(color, 0.6f),
					Gravity = 9.8f,
					Drag = 0.6f,
					Kind = FxParticles.Kind.Drop,
					Matte = true
				});
			}
			if (flag)
			{
				for (int m = 0; m < 4; m++)
				{
					Vec3 vec3 = (back + new Vec3(BlastFx.Rand(seed, m + 50) - 0.5f, BlastFx.Rand(seed, m + 54) - 0.5f, BlastFx.Rand(seed, m + 58) - 0.3f)).Normalized();
					parts.Spark(at + back * 0.02f, vec3 * (3f + 3f * BlastFx.Rand(seed, m + 62)), 0.12f + 0.12f * BlastFx.Rand(seed, m + 66), 0.04f, TerrainCache.Rgb(255, 240, 180), TerrainCache.Rgb(255, 140, 60), 8f, 1f);
				}
			}
		}
	}
}
