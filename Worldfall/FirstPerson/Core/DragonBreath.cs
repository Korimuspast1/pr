using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class DragonBreath
	{
		private const int Size = 16;

		private const int Variants = 4;

		private const int Heats = 3;

		private static readonly SpriteImage[] Balls = new SpriteImage[12];

		public static SpriteImage Ball(int heat, int variant)
		{
			int num = heat * 4 + (variant & 3);
			if (Balls[num] != null)
			{
				return Balls[num];
			}
			uint[] array = new uint[256];
			uint[] array2 = (new uint[3][]
			{
				new uint[3]
				{
					TerrainCache.Rgb(255, 255, 236),
					TerrainCache.Rgb(255, 236, 140),
					TerrainCache.Rgb(255, 176, 52)
				},
				new uint[3]
				{
					TerrainCache.Rgb(255, 228, 120),
					TerrainCache.Rgb(255, 150, 40),
					TerrainCache.Rgb(226, 74, 22)
				},
				new uint[3]
				{
					TerrainCache.Rgb(255, 160, 52),
					TerrainCache.Rgb(214, 70, 22),
					TerrainCache.Rgb(110, 34, 18)
				}
			})[heat];
			float num2 = 7.5f;
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					float num3 = ((float)j - num2) / num2;
					float num4 = ((float)i - num2) / num2;
					float num5 = (float)Math.Sqrt(num3 * num3 + num4 * num4);
					float num6 = (float)Math.Atan2(num4, num3);
					float num7 = 0.78f + 0.13f * (float)Math.Sin(num6 * 3f + (float)variant * 1.7f) + 0.08f * (float)Math.Sin(num6 * 5f - (float)variant * 2.3f);
					if (!(num5 > num7))
					{
						float num8 = num5 / num7;
						uint num9 = TerrainModel.Hash(j * 31 + variant * 7, i * 17 + heat * 13);
						num8 += ((float)(num9 & 0xF) / 15f - 0.5f) * 0.16f;
						uint num10 = ((num8 < 0.45f) ? TerrainCache.Lerp(array2[0], array2[1], Math.Max(0f, num8) / 0.45f) : TerrainCache.Lerp(array2[1], array2[2], Math.Min(1f, (num8 - 0.45f) / 0.55f)));
						array[i * 16 + j] = num10 | 0xFF000000u;
					}
				}
			}
			SpriteImage spriteImage = new SpriteImage
			{
				Width = 16,
				Height = 16,
				Pixels = array,
				UnitWidth = 1f,
				UnitHeight = 1f,
				PivotX = 0.5f,
				PivotY = 0f
			};
			spriteImage.MeasureOpaqueRows();
			Balls[num] = spriteImage;
			return spriteImage;
		}

		public static void Emit(List<Billboard> bills, List<PointLight> lights, Vec3 mouth, Vec3 dir, float reach, float width, float strength, float time, uint seed, Func<float, float, float> ground)
		{
			if (strength <= 0.02f || !(reach > 0f) || float.IsNaN(mouth.X + mouth.Y + mouth.Z + dir.X + dir.Y + dir.Z))
			{
				return;
			}
			reach = Math.Min(reach, 60f);
			float sag = 0.1f;
			float num = reach;
			for (float num2 = 0.3f; num2 <= reach; num2 += 0.25f)
			{
				Vec3 vec = Along(num2);
				if (vec.Z <= ground(vec.X, vec.Y) + 0.05f)
				{
					num = num2;
					break;
				}
			}
			Vec3 vec2 = Along(num);
			float num3 = ground(vec2.X, vec2.Y);
			bool flag = num < reach - 0.01f;
			Vec3 vec3 = Vec3.Cross((Math.Abs(dir.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f), dir).Normalized();
			Vec3 vec4 = Vec3.Cross(dir, vec3);
			int num4 = (int)(14f * Math.Max(0.4f, strength));
			for (int i = 0; i < num4; i++)
			{
				uint num5 = TerrainModel.Hash((int)seed + i * 6151, i * 19 + 9);
				float num6 = Frac(time * 2.6f + (float)i / (float)num4) * 0.42f;
				Vec3 vec5 = Along(num6 * num) + vec3 * ((float)Math.Sin(time * 17f + (float)i) * 0.03f * width);
				float num7 = width * (0.22f + 0.5f * num6) * (0.6f + 0.4f * strength);
				bills.Add(new Billboard
				{
					X = vec5.X,
					Y = vec5.Y,
					Z = vec5.Z - num7 * 0.5f,
					GroundZ = ground(vec5.X, vec5.Y),
					Width = num7,
					Height = num7,
					Image = Ball(0, (int)num5),
					Kind = BillboardKind.Facing,
					Flip = ((num5 & 2) != 0),
					Emissive = true,
					Tag = -1
				});
			}
			int num8 = (int)(40f * Math.Max(0.4f, strength));
			for (int j = 0; j < num8; j++)
			{
				uint num9 = TerrainModel.Hash((int)seed + j * 7919, j * 31 + 5);
				float num10 = Frac(time * 1.7f + (float)j / (float)num8 + (float)(num9 & 0xFF) / 255f * 0.05f);
				float s = num10 * num;
				float num11 = (0.04f + 0.3f * num10) * width;
				float num12 = time * (5f + (float)(num9 >> 28)) + (float)j * 1.3f;
				Vec3 vec6 = Along(s) + vec3 * ((float)Math.Sin(num12) * num11) + vec4 * ((float)Math.Cos(num12 * 1.3f) * num11);
				float num13 = width * (0.25f + 1.25f * num10) * (0.8f + 0.4f * (float)((num9 >> 8) & 0xFF) / 255f) * (0.6f + 0.4f * strength);
				int heat = ((!(num10 < 0.18f)) ? ((num10 < 0.62f) ? 1 : 2) : 0);
				bills.Add(new Billboard
				{
					X = vec6.X,
					Y = vec6.Y,
					Z = vec6.Z - num13 * 0.5f,
					GroundZ = ground(vec6.X, vec6.Y),
					Width = num13,
					Height = num13,
					Image = Ball(heat, (int)(num9 >> 4)),
					Kind = BillboardKind.Facing,
					Flip = ((num9 & 2) != 0),
					Emissive = true,
					Tag = -1,
					Alpha = (byte)((num10 > 0.82f) ? ((byte)Math.Max(1, (int)(255f * (1f - (num10 - 0.82f) / 0.18f)))) : 0)
				});
			}
			for (int k = 0; k < 8; k++)
			{
				uint variant = TerrainModel.Hash((int)seed + k * 4099, k * 23 + 1);
				float num14 = Frac(time * 1.2f + (float)k / 8f);
				Vec3 vec7 = Along(num14 * num);
				float num15 = width * (1.1f + 1.6f * num14);
				bills.Add(new Billboard
				{
					X = vec7.X,
					Y = vec7.Y,
					Z = vec7.Z - num15 * 0.5f,
					GroundZ = ground(vec7.X, vec7.Y),
					Width = num15,
					Height = num15,
					Image = Ball(1, (int)variant),
					Kind = BillboardKind.Facing,
					Emissive = true,
					Tag = -1,
					Alpha = (byte)(70f * strength * (1f - num14 * 0.6f))
				});
			}
			for (int l = 0; l < 16; l++)
			{
				uint num16 = TerrainModel.Hash((int)seed + l * 7477, l * 41 + 3);
				float num17 = Frac(time * 0.9f + (float)l / 16f + (float)(num16 & 0xFF) / 255f);
				float s2 = (float)((num16 >> 8) & 0xFF) / 255f * num;
				Vec3 vec8 = vec3 * (((float)((num16 >> 16) & 0xFF) / 255f - 0.5f) * 2f) + vec4 * (((float)((num16 >> 24) & 0xFF) / 255f - 0.2f) * 1.5f);
				Vec3 vec9 = Along(s2) + vec8 * (num17 * width * 2.2f) + new Vec3(0f, 0f, num17 * width * 0.8f - num17 * num17 * width * 1.4f);
				SpriteImage image = (((num16 & 1) != 0) ? Flames.Spark : Flames.Ember);
				float num18 = width * 0.12f;
				bills.Add(new Billboard
				{
					X = vec9.X,
					Y = vec9.Y,
					Z = vec9.Z,
					GroundZ = ground(vec9.X, vec9.Y),
					Width = num18,
					Height = num18,
					Image = image,
					Kind = BillboardKind.Facing,
					Emissive = true,
					Tag = -1
				});
			}
			if (flag)
			{
				Vec3 vec10 = new Vec3(dir.X, dir.Y, 0f);
				if (Vec3.Dot(vec10, vec10) < 0.0001f)
				{
					vec10 = vec3;
				}
				vec10 = vec10.Normalized();
				Vec3 vec11 = new Vec3(0f - vec10.Y, vec10.X, 0f);
				int num19 = (int)(12f * strength) + 4;
				for (int m = 0; m < num19; m++)
				{
					uint num20 = TerrainModel.Hash((int)seed + m * 104729, m * 13 + 77);
					float num21 = Frac(time * 1.3f + (float)m / (float)num19 + (float)(num20 & 0x7F) / 127f * 0.1f);
					float num22 = (float)((num20 >> 8) & 0x3FF) / 1023f * 2.6f - 1.3f;
					float num23 = width * (0.2f + 1.6f * num21);
					Vec3 vec12 = vec10 * (float)Math.Cos(num22) + vec11 * (float)Math.Sin(num22);
					float num24 = vec2.X + vec12.X * num23;
					float num25 = vec2.Y + vec12.Y * num23;
					float num26 = ground(num24, num25);
					float num27 = width * (0.5f + 0.7f * (1f - num21)) * strength;
					bills.Add(new Billboard
					{
						X = num24,
						Y = num25,
						Z = num26 + num27 * (0.1f + 0.4f * num21),
						GroundZ = num26,
						Width = num27,
						Height = num27,
						Image = Ball((num21 < 0.35f) ? 1 : 2, (int)(num20 >> 3)),
						Kind = BillboardKind.Facing,
						Flip = ((num20 & 4) != 0),
						Emissive = true,
						Tag = -1,
						Alpha = (byte)((num21 > 0.7f) ? ((byte)Math.Max(1, (int)(255f * (1f - (num21 - 0.7f) / 0.3f)))) : 0)
					});
				}
				FireField.AddPlume(bills, vec2.X, vec2.Y, num3 + width * 0.4f, width * 1.4f, (int)(6f * strength) + 2, seed, time, 0.8f);
			}
			float num28 = 0.82f + 0.18f * (float)Math.Sin(time * 23f + (float)seed) * (float)Math.Sin(time * 13.7f);
			float num29 = strength * num28;
			lights.Add(new PointLight
			{
				X = mouth.X + dir.X * 0.4f,
				Y = mouth.Y + dir.Y * 0.4f,
				Z = mouth.Z + dir.Z * 0.4f,
				Radius = 3f + width * 2f,
				R = 1.1f * num29,
				G = 0.85f * num29,
				B = 0.45f * num29,
				Glow = 0.45f * width
			});
			Vec3 vec13 = Along(num * 0.5f);
			lights.Add(new PointLight
			{
				X = vec13.X,
				Y = vec13.Y,
				Z = vec13.Z,
				Radius = 4f + width * 3f,
				R = 1.1f * num29,
				G = 0.6f * num29,
				B = 0.22f * num29
			});
			lights.Add(new PointLight
			{
				X = vec2.X,
				Y = vec2.Y,
				Z = num3 + 0.6f,
				Radius = 5f + width * 4f,
				R = 1.2f * num29,
				G = 0.55f * num29,
				B = 0.18f * num29
			});
			Vec3 Along(float num30)
			{
				return mouth + dir * num30 + new Vec3(0f, 0f, (0f - sag) * num30 * num30 / Math.Max(1f, reach));
			}
		}

		private static float Frac(float x)
		{
			return x - (float)Math.Floor(x);
		}
	}
}
