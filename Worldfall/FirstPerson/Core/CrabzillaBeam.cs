using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class CrabzillaBeam
	{
		public const uint Turquoise = 4291362560u;

		public const uint Core = 4294310143u;

		public static float Radius = 1.5f;

		public static float OrbRadius = 1.25f;

		public static float DetailRange = 70f;

		private static MeshModel _core;

		private static MeshModel _bolt;

		private static SpriteImage _flare;

		private static SpriteImage _spark;

		private static SpriteImage _haze;

		private static SpriteImage _ring;

		private static SpriteImage _mote;

		private static SpriteImage[] _plasma;

		private const int PlasmaFrames = 8;

		private static readonly List<MeshInstance> Pool = new List<MeshInstance>();

		private static int _used;

		public static void Begin()
		{
			_used = 0;
		}

		public static void Orb(List<MeshInstance> meshes, List<Billboard> bills, List<PointLight> lights, Vec3 centre, float charge, float power, Vec3[] sources, float time, uint seed, Vec3 eye)
		{
			float num = Smooth(charge);
			if (num <= 0.01f)
			{
				return;
			}
			Build();
			float num2 = 1f + 0.07f * (float)Math.Sin(time * 31f + (float)seed) + 0.04f * (float)Math.Sin(time * 53f + (float)seed * 2f);
			float num3 = OrbRadius * (0.22f + 0.78f * num) * num2 * (1f + 0.12f * power);
			bool num4 = Length(centre - eye) < DetailRange;
			int num5 = (int)(time * 14f + (float)seed) % 8;
			Ball(bills, centre, num3 * 4.2f, _haze, (byte)(70f + 60f * num));
			Ball(bills, centre, num3 * 2.35f, _plasma[(num5 + 3) % 8], (byte)(70f + 60f * num));
			Ball(bills, centre, num3 * 2f, _plasma[num5], (byte)(170f + 85f * num));
			Ball(bills, centre, num3 * 2.2f, _ring, (byte)(60f + 110f * num * (0.7f + 0.3f * (float)Math.Sin(time * 23f + (float)seed))));
			Ball(bills, centre, num3 * (0.5f + 0.25f * power), _flare, byte.MaxValue);
			if (num4)
			{
				int num6 = (int)(time * 24f);
				if (sources != null)
				{
					for (int i = 0; i < sources.Length; i++)
					{
						uint num7 = Hash(seed * 131 + (uint)(i * 7919), num6);
						if ((charge < 0.999f) ? ((byte)(num7 & 3) != 0) : ((num7 & 7) == 0))
						{
							Vec3 b = centre + RandomDir(num7 >> 3) * (num3 * 0.7f);
							Arc(meshes, bills, sources[i], b, 7, 0.28f, 0.05f, num7);
							if ((num7 & 0x40) != 0)
							{
								Arc(meshes, bills, sources[i], b, 5, 0.35f, 0.035f, num7 * 31);
							}
						}
					}
				}
				int num8 = 3 + (int)(4f * num);
				for (int j = 0; j < num8; j++)
				{
					uint num9 = Hash(seed * 977 + (uint)(j * 104729), num6 + j);
					Vec3 vec = RandomDir(num9);
					Vec3 a = centre + vec * (num3 * 0.85f);
					Vec3 b2 = centre + (vec + RandomDir(num9 * 13) * 0.6f).Normalized() * (num3 * (1.35f + 0.5f * (float)((num9 >> 20) & 7) / 7f));
					Arc(meshes, bills, a, b2, 4, 0.35f, 0.035f, num9 * 7);
				}
				bool flag = charge < 0.999f;
				int num10 = (flag ? 14 : 8);
				for (int k = 0; k < num10; k++)
				{
					uint num11 = Hash(seed * 31 + (uint)(k * 7349), k * 17 + 3);
					float num12 = Frac(time * (1.3f + (float)(num11 & 7) * 0.12f) + (float)(num11 & 0xFF) / 255f);
					Vec3 vec2 = RandomDir(num11 >> 4);
					float num13 = (flag ? (num3 * (3.4f - 2.5f * num12)) : (num3 * (1.1f + 3f * num12)));
					float size = 0.22f + 0.18f * (float)((num11 >> 9) & 3) / 3f;
					Mote(bills, centre + vec2 * num13, size, (byte)Math.Max(1, (int)(255f * (flag ? num12 : (1f - num12)))));
				}
			}
			lights.Add(new PointLight
			{
				X = centre.X,
				Y = centre.Y,
				Z = centre.Z,
				Radius = 6f + 5f * num,
				R = 0.18f * num * num2,
				G = 0.75f * num * num2,
				B = 0.65f * num * num2,
				Glow = num3 * (0.9f + 0.4f * power)
			});
		}

		public static void Emit(List<MeshInstance> meshes, List<Billboard> bills, List<PointLight> lights, Vec3 from, Vec3 to, float power, float time, bool strikes, uint seed, Vec3 eye)
		{
			if (power <= 0.01f)
			{
				return;
			}
			Build();
			Vec3 vec = to - from;
			float num = Length(vec);
			if (num < 0.05f)
			{
				return;
			}
			Vec3 vec2 = vec * (1f / num);
			Vec3 b = ((Math.Abs(vec2.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f));
			Vec3 vec3 = Vec3.Cross(vec2, b).Normalized();
			Vec3 vec4 = Vec3.Cross(vec3, vec2);
			float num2 = Smooth(power);
			float num3 = 1f + 0.09f * (float)Math.Sin(time * 44f + (float)seed) + 0.05f * (float)Math.Sin(time * 71f);
			float num4 = Radius * num2 * num3;
			float num5 = num * Math.Min(1f, 0.15f + power * 1.2f);
			Place(Take(_core), meshes, from, vec2, vec3, vec4, num5, Radius * 0.2f * num2 * (0.9f + 0.2f * (float)Math.Sin(time * 63f + (float)seed)));
			float num6 = num4 * 1.5f;
			float num7 = num4 * 3.1f;
			for (float num8 = 0.4f; num8 < num5; num8 += num6 * 0.3f)
			{
				Ball(bills, from + vec2 * num8, num6, _haze, 115);
			}
			for (float num9 = 1.2f; num9 < num5; num9 += num7 * 0.5f)
			{
				Ball(bills, from + vec2 * num9, num7 * (0.92f + 0.16f * (float)Math.Sin(num9 * 0.7f - time * 9f)), _haze, 38);
			}
			bool flag = Length(from - eye) < DetailRange;
			if (flag)
			{
				int num10 = Math.Min(140, (int)(num5 / 0.9f));
				for (int i = 0; i < 2; i++)
				{
					for (int j = 0; j < num10; j++)
					{
						float num11 = Frac((float)j / (float)num10 + time * 36f / Math.Max(1f, num5)) * num5;
						if (!(num11 < 0.6f))
						{
							float num12 = num11 * 0.75f - time * 7f + (float)i * MathF.PI;
							Mote(bills, from + vec2 * num11 + (vec3 * (float)Math.Cos(num12) + vec4 * (float)Math.Sin(num12)) * (num4 * 0.62f), 0.42f * num2, 230);
						}
					}
				}
				float num13 = Math.Min(num5, 60f);
				for (int k = 0; k < 6; k++)
				{
					float num14 = Frac((float)k / 6f + time * 1.6f) * num13;
					float num15 = 1f - num14 / num13;
					Ball(bills, from + vec2 * num14, num4 * (2.1f + 0.9f * (1f - num15)), _ring, (byte)Math.Max(1, (int)(130f * num15 * num2)));
				}
				int num16 = (int)(time * 20f);
				for (int l = 0; l < 6; l++)
				{
					uint num17 = Hash(seed * 31 + (uint)(l * 977), num16 + l * 13);
					float num18 = (float)(num17 & 0x3FF) / 1023f * 0.9f * num5 + 0.5f;
					float num19 = 2.5f + (float)((num17 >> 10) & 0xF) * 0.3f;
					float num20 = (float)((num17 >> 14) & 0xFF) / 255f * 6.283f;
					float num21 = num20 + ((((num17 >> 22) & 1) == 0) ? 1.2f : (-1.2f));
					Vec3 a = from + vec2 * num18 + (vec3 * (float)Math.Cos(num20) + vec4 * (float)Math.Sin(num20)) * (num4 * 0.75f);
					Vec3 b2 = from + vec2 * Math.Min(num5, num18 + num19) + (vec3 * (float)Math.Cos(num21) + vec4 * (float)Math.Sin(num21)) * (num4 * 0.9f);
					Arc(meshes, bills, a, b2, 5, 0.2f, 0.05f, num17);
				}
			}
			for (float num22 = 8f; num22 < num5 - 4f; num22 += 10f)
			{
				Vec3 vec5 = from + vec2 * num22;
				lights.Add(new PointLight
				{
					X = vec5.X,
					Y = vec5.Y,
					Z = vec5.Z,
					Radius = 7f,
					R = 0.06f * num2,
					G = 0.42f * num2,
					B = 0.36f * num2
				});
			}
			if (!strikes || num5 < num - 0.01f)
			{
				return;
			}
			float num23 = (5.2f + 1.3f * (float)Math.Sin(time * 37f + (float)seed)) * num2;
			Ball(bills, to, num23 * 1.7f, _haze, 80);
			Ball(bills, to, num23, _flare, 240);
			for (int m = 0; m < 12; m++)
			{
				uint num24 = Hash(seed * 7 + (uint)(m * 7919), m * 31 + 5);
				float num25 = Frac(time * (1.6f + (float)(num24 & 7) * 0.15f) + (float)(num24 & 0xFF) / 255f);
				float num26 = (float)((num24 >> 8) & 0x3FF) / 1023f * 6.283f;
				float num27 = (1f + 6f * num25) * num2;
				float z = to.Z + (4f * num25 - 5f * num25 * num25) * (0.6f + (float)((num24 >> 20) & 3) * 0.2f) * num2;
				float num28 = (0.8f - 0.5f * num25) * num2;
				bills.Add(new Billboard
				{
					X = to.X + (float)Math.Cos(num26) * num27,
					Y = to.Y + (float)Math.Sin(num26) * num27,
					Z = z,
					GroundZ = to.Z,
					Width = num28,
					Height = num28,
					Image = _spark,
					Kind = BillboardKind.Facing,
					Emissive = true,
					Tag = -1,
					Far = true,
					Alpha = (byte)Math.Max(1, (int)(255f * (1f - num25)))
				});
			}
			if (flag || Length(to - eye) < DetailRange)
			{
				int num29 = (int)(time * 18f);
				for (int n = 0; n < 5; n++)
				{
					uint num30 = Hash(seed * 13 + (uint)(n * 3571), num29 + n * 3);
					float num31 = (float)(num30 & 0x3FF) / 1023f * 6.283f;
					float num32 = 2.5f + (float)((num30 >> 10) & 7) * 0.5f;
					Vec3 b3 = to + new Vec3((float)Math.Cos(num31) * num32, (float)Math.Sin(num31) * num32, 0.2f);
					Arc(meshes, bills, to + new Vec3(0f, 0f, 0.4f), b3, 5, 0.3f, 0.06f, num30);
				}
			}
			for (int num33 = 0; num33 < 4; num33++)
			{
				uint num34 = Hash(seed * 5 + (uint)(num33 * 1013), num33 * 7 + 1);
				float num35 = Frac(time * 0.55f + (float)num33 / 4f);
				float num36 = (2f + 4f * num35) * num2;
				bills.Add(new Billboard
				{
					X = to.X + ((float)(num34 & 0xFF) / 255f - 0.5f) * 2f,
					Y = to.Y + ((float)((num34 >> 8) & 0xFF) / 255f - 0.5f) * 2f,
					Z = to.Z + 1f + 7f * num35,
					GroundZ = to.Z,
					Width = num36,
					Height = num36,
					Image = FireField.SoftSmoke(Math.Min(5, 1 + (int)(num35 * 3f)), (int)(num34 & 3)),
					Kind = BillboardKind.Facing,
					Tag = -1,
					Far = true,
					Alpha = (byte)Math.Max(1, (int)(150f * (1f - num35)))
				});
			}
			lights.Add(new PointLight
			{
				X = to.X,
				Y = to.Y,
				Z = to.Z + 1f,
				Radius = 13f,
				R = 0.25f * num2,
				G = 0.85f * num2,
				B = 0.75f * num2,
				Glow = 2.4f * num2
			});
		}

		private static void Arc(List<MeshInstance> meshes, List<Billboard> bills, Vec3 a, Vec3 b, int segments, float jitter, float thick, uint h)
		{
			Vec3 vec = b - a;
			float num = Length(vec);
			if (num < 0.05f)
			{
				return;
			}
			Vec3 vec2 = vec * (1f / num);
			Vec3 b2 = ((Math.Abs(vec2.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f));
			Vec3 vec3 = Vec3.Cross(vec2, b2).Normalized();
			Vec3 vec4 = Vec3.Cross(vec3, vec2);
			Vec3 vec5 = a;
			for (int i = 1; i <= segments; i++)
			{
				float num2 = (float)i / (float)segments;
				Vec3 vec6 = a + vec * num2;
				if (i < segments)
				{
					uint num3 = Hash(h + (uint)(i * -1640531535), i);
					float num4 = ((float)(num3 & 0x3FF) / 1023f - 0.5f) * 2f;
					float num5 = ((float)((num3 >> 10) & 0x3FF) / 1023f - 0.5f) * 2f;
					float num6 = jitter * num * (float)Math.Sin((double)num2 * Math.PI);
					vec6 += (vec3 * num4 + vec4 * num5) * num6;
				}
				Vec3 vec7 = vec6 - vec5;
				float num7 = Length(vec7);
				if (num7 > 0.02f)
				{
					Vec3 vec8 = vec7 * (1f / num7);
					Vec3 b3 = ((Math.Abs(vec8.Z) < 0.9f) ? new Vec3(0f, 0f, 1f) : new Vec3(1f, 0f, 0f));
					Vec3 vec9 = Vec3.Cross(vec8, b3).Normalized();
					Place(Take(_bolt), meshes, vec5, vec8, vec9, Vec3.Cross(vec9, vec8), num7, thick * ((i == 1) ? 1.2f : 1f));
					float num8 = thick * 9f;
					for (float num9 = 0f; num9 < num7; num9 += num8 * 0.7f)
					{
						Ball(bills, vec5 + vec8 * num9, num8, _haze, 60);
					}
				}
				vec5 = vec6;
			}
		}

		private static void Ball(List<Billboard> bills, Vec3 at, float size, SpriteImage image, byte alpha)
		{
			bills.Add(new Billboard
			{
				X = at.X,
				Y = at.Y,
				Z = at.Z - size * 0.5f,
				GroundZ = at.Z - 30f,
				Width = size,
				Height = size,
				Far = true,
				Image = image,
				Kind = BillboardKind.Facing,
				Emissive = true,
				Tag = -1,
				Alpha = alpha
			});
		}

		private static void Mote(List<Billboard> bills, Vec3 at, float size, byte alpha)
		{
			Ball(bills, at, size, _mote, alpha);
		}

		private static MeshInstance Take(MeshModel model)
		{
			if (_used == Pool.Count)
			{
				Pool.Add(new MeshInstance
				{
					Emissive = true,
					NoShadow = true,
					Tag = -1
				});
			}
			MeshInstance meshInstance = Pool[_used++];
			meshInstance.Model = model;
			meshInstance.Hidden = 0uL;
			return meshInstance;
		}

		private static void Place(MeshInstance m, List<MeshInstance> meshes, Vec3 from, Vec3 dir, Vec3 side, Vec3 up, float length, float radius)
		{
			m.Position = from;
			m.Basis = new Mat3(side * (0f - radius), dir * (0f - length), up * radius);
			m.Fade = 1f;
			m.SortDepth = 0f;
			meshes.Add(m);
		}

		private static void Build()
		{
			if (_core == null)
			{
				_core = Tube("crab_beam_core", 4294310143u, 6);
				_bolt = Tube("crab_beam_bolt", 4294310143u, 4);
				_flare = Glow(24, 1f, soft: true);
				_spark = Glow(8, 0.55f, soft: false);
				_mote = Glow(8, 0.7f, soft: true);
				_haze = Glow(16, 0f, soft: true);
				_ring = Ring(32);
				_plasma = new SpriteImage[8];
				for (int i = 0; i < 8; i++)
				{
					_plasma[i] = Plasma(44, 9173 + i * 31);
				}
			}
		}

		private static MeshModel Tube(string name, uint color, int sides)
		{
			MeshBuilder meshBuilder = new MeshBuilder(name);
			int slot = meshBuilder.Slot("beam", 0, color, 1);
			meshBuilder.Part("beam", -1, new Vec3(0f, 0f, 0f));
			meshBuilder.Tube(slot, new Vec3(0f, 0f, 0f), new Vec3(0f, -1f, 0f), 1f, 1f, sides);
			return meshBuilder.Build();
		}

		private static uint Pack(float r, float g, float b, float a)
		{
			return (uint)(Clamp255(r) | (Clamp255(g) << 8) | (Clamp255(b) << 16) | (Clamp255(a) << 24));
		}

		private static int Clamp255(float v)
		{
			if (!(v < 0f))
			{
				if (!(v > 255f))
				{
					return (int)v;
				}
				return 255;
			}
			return 0;
		}

		private static SpriteImage Glow(int size, float white, bool soft)
		{
			SpriteImage spriteImage = Blank(size);
			float num = (float)(size - 1) * 0.5f;
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					float num2 = ((float)j - num) / num;
					float num3 = ((float)i - num) / num;
					float num4 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
					if (!(num4 > 1f))
					{
						float num5 = ((num4 < 0.42f * white) ? 0f : Math.Min(1f, (num4 - 0.42f * white) / (1f - 0.42f * white)));
						float a = (soft ? Math.Max(1f, 255f * (1f - num4 * num4) * (1f - num4 * 0.35f)) : 255f);
						spriteImage.Pixels[i * size + j] = Pack(255f * (1f - num5), 255f, 245f * (1f - num5) + 200f * num5, a);
					}
				}
			}
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}

		private static SpriteImage Ring(int size)
		{
			SpriteImage spriteImage = Blank(size);
			float num = (float)(size - 1) * 0.5f;
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					float num2 = ((float)j - num) / num;
					float num3 = ((float)i - num) / num;
					float num4 = ((float)Math.Sqrt(num2 * num2 + num3 * num3) - 0.8f) / 0.13f;
					float num5 = 255f * (float)Math.Exp((0f - num4) * num4);
					if (!(num5 < 3f))
					{
						float num6 = (float)Math.Exp((0f - num4) * num4 * 4f);
						spriteImage.Pixels[i * size + j] = Pack(220f * num6, 255f, 200f + 50f * num6, num5);
					}
				}
			}
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}

		private static SpriteImage Plasma(int size, int seed)
		{
			SpriteImage spriteImage = Blank(size);
			float num = (float)(size - 1) * 0.5f;
			Random random = new Random(seed);
			for (int i = 0; i < size; i++)
			{
				for (int j = 0; j < size; j++)
				{
					float num2 = ((float)j - num) / num;
					float num3 = ((float)i - num) / num;
					float num4 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
					if (!(num4 > 1f))
					{
						float num5 = num4 * num4 * num4 * num4;
						float num6 = Math.Max(0f, 1f - num4 / 0.5f);
						float num7 = 0.85f + 0.3f * (float)(TerrainModel.Hash(j * 3 + seed, i * 5) & 0x3FF) / 1023f;
						float a = Math.Min(1f, 0.42f + 0.55f * num5 + 0.45f * num6) * 255f;
						float num8 = Math.Min(1f, num6 * 0.9f + num5 * 0.35f);
						spriteImage.Pixels[i * size + j] = Pack(40f * num7 + 215f * num8, (215f + 40f * num8) * num7, (185f + 70f * num8) * num7, a);
					}
				}
			}
			for (int k = 0; k < 10; k++)
			{
				Vein(spriteImage, num, random, num + (float)(random.NextDouble() - 0.5) * num * 0.5f, num + (float)(random.NextDouble() - 0.5) * num * 0.5f, (float)(random.NextDouble() * Math.PI * 2.0), (int)(num * 1.1f), 3);
			}
			for (int l = 0; l < 6; l++)
			{
				float num9 = (float)(random.NextDouble() * Math.PI * 2.0);
				float x = num + (float)Math.Cos(num9) * num * 0.9f;
				float y = num + (float)Math.Sin(num9) * num * 0.9f;
				Vein(spriteImage, num, random, x, y, num9 + MathF.PI / 2f * (float)((random.Next(2) == 0) ? 1 : (-1)), (int)(num * 0.6f), 0);
			}
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}

		private static void Vein(SpriteImage img, float c, Random rnd, float x, float y, float angle, int steps, int forks)
		{
			int width = img.Width;
			for (int i = 0; i < steps; i++)
			{
				angle += (float)(rnd.NextDouble() - 0.5) * 1.1f;
				x += (float)Math.Cos(angle);
				y += (float)Math.Sin(angle);
				int num = (int)Math.Round(x);
				int num2 = (int)Math.Round(y);
				if (num < 0 || num2 < 0 || num >= width || num2 >= width)
				{
					break;
				}
				float num3 = ((float)num - c) / c;
				float num4 = ((float)num2 - c) / c;
				if (num3 * num3 + num4 * num4 > 1.02f)
				{
					break;
				}
				float num5 = 0.75f + 0.25f * (float)rnd.NextDouble();
				img.Pixels[num2 * width + num] = Pack(225f * num5 + 30f, 255f, 245f, 255f);
				int num6 = num + ((Math.Abs(Math.Sin(angle)) > 0.7) ? 1 : 0);
				int num7 = num2 + ((!(Math.Abs(Math.Sin(angle)) > 0.7)) ? 1 : 0);
				if (num6 < width && num7 < width)
				{
					img.Pixels[num7 * width + num6] = Pack(150f * num5 + 40f, 255f, 235f, 255f);
				}
				if (forks > 0 && rnd.NextDouble() < 0.07)
				{
					Vein(img, c, rnd, x, y, angle + (float)(rnd.NextDouble() - 0.5) * 2f, steps / 2, forks - 1);
					forks--;
				}
			}
		}

		private static SpriteImage Blank(int size)
		{
			return new SpriteImage
			{
				Width = size,
				Height = size,
				Pixels = new uint[size * size],
				PivotX = 0.5f,
				PivotY = 0f,
				UnitWidth = 1f,
				UnitHeight = 1f
			};
		}

		private static Vec3 RandomDir(uint h)
		{
			float num = (float)(h & 0x3FF) / 1023f * 2f - 1f;
			float num2 = (float)((h >> 10) & 0x3FF) / 1023f * 6.283f;
			float num3 = (float)Math.Sqrt(Math.Max(0f, 1f - num * num));
			return new Vec3(num3 * (float)Math.Cos(num2), num3 * (float)Math.Sin(num2), num);
		}

		private static uint Hash(uint a, int b)
		{
			return TerrainModel.Hash((int)a, b);
		}

		private static float Length(Vec3 v)
		{
			return (float)Math.Sqrt(Vec3.Dot(v, v));
		}

		private static float Smooth(float t)
		{
			t = ((t < 0f) ? 0f : ((t > 1f) ? 1f : t));
			return t * t * (3f - 2f * t);
		}

		private static float Frac(float x)
		{
			return x - (float)Math.Floor(x);
		}
	}
}
