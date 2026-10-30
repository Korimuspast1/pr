using System;

namespace FirstPerson.Core
{
	public sealed class UfoCrashFx
	{
		public readonly float X;

		public readonly float Y;

		public readonly float Ground;

		public readonly float Diameter;

		public readonly uint Seed;

		public float Age;

		private readonly uint _land;

		private bool _burst;

		private float _smokeClock;

		private float _fireClock;

		private float _sparkClock;

		private MeshInstance _wreck;

		private Mat3 _wreckTurn;

		private float _wreckScale;

		public const float Lasts = 19f;

		public bool Finished => Age > 19f;

		public bool HasWreck => _wreck != null;

		public UfoCrashFx(float x, float y, float ground, float diameter, uint seed, uint land)
		{
			X = x;
			Y = y;
			Ground = ground;
			Diameter = Math.Max(2f, diameter);
			Seed = seed;
			_land = land;
		}

		public void Wreck(MeshModel model, Mat3 turn, float scale)
		{
			_wreck = new MeshInstance
			{
				Model = model,
				NoShadow = false
			};
			float num = BlastFx.Rand(Seed, 5) * 6.283f;
			_wreckTurn = Mat3.Rotation(new Vec3(0f - (float)Math.Sin(num), (float)Math.Cos(num), 0f), 0.42f) * Mat3.RotZ(BlastFx.Rand(Seed, 6) * 6.283f);
			_wreckScale = scale;
		}

		public void Step(float dt, FxSink sink, FxParticles parts, float reach = 160f)
		{
			Age += dt;
			float age = Age;
			float diameter = Diameter;
			float num = diameter / 4.8f;
			float num2 = sink.Distance(X, Y);
			if (!_burst)
			{
				_burst = true;
				Burst(sink, parts);
				sink.Shake = Math.Max(sink.Shake, 0.9f * BlastFx.Clamp01(1f - num2 / 45f) + 0.1f * BlastFx.Clamp01(1f - num2 / 100f));
			}
			if (age < 0.35f)
			{
				float num3 = 1f - age / 0.35f;
				sink.AddFlash(0.45f * num3 * num3 * BlastFx.Clamp01(1.15f - num2 / 70f) * (0.45f + 0.55f * Math.Max(0f, sink.Facing(X, Y))), UfoArt.BeamCream);
				float num4 = num * (7f + 14f * age);
				sink.Sprite(BlastFx.StarSprite, X, Y, Ground + 1.2f * num, num4, num4, UfoArt.BeamCream, (byte)Math.Max(2, (int)(255f * num3)), emissive: true, additive: true);
			}
			if (age < 1f)
			{
				sink.Glow(X, Y, Ground + 1.4f * num, num * (8f + 8f * age), (age < 0.3f) ? UfoArt.BeamYellow : BlastFx.Heat(0.85f), (1f - age) * 0.8f);
			}
			float num5 = (float)Math.Exp((0f - age) / 6f) * BlastFx.Clamp01((16f - age) / 3f);
			sink.Light(X, Y, Ground + 2f * num, 22f * num, (age < 0.4f) ? UfoArt.BeamLime : BlastFx.Heat(0.85f), 3f * (float)Math.Exp((0f - age) / 0.35f) + 0.8f * num5, (age < 0.5f) ? (3f * num) : 0f);
			if (age < 0.8f)
			{
				float num6 = age / 0.8f;
				float num7 = num * (2f + 9f * BlastFx.Smooth(Math.Min(1f, num6 * 1.15f)));
				float num8 = (1f - num6) * (1f - num6);
				for (int i = 0; i < 32; i++)
				{
					if (i % 7 != 3)
					{
						float num9 = ((float)i + 0.5f) / 32f * 6.2832f;
						float num10 = X + (float)Math.Cos(num9) * num7;
						float num11 = Y + (float)Math.Sin(num9) * num7;
						float num12 = ((sink.Ground != null) ? sink.Ground(num10, num11) : Ground);
						sink.Sprite(PowerFx.Dash, num10, num11, num12 + 0.25f, 1.3f + num7 * 0.1f, 0.5f, TerrainCache.Lerp(UfoArt.BeamCream, UfoArt.BeamGreen, num6), (byte)Math.Max(2, (int)(255f * num8)), emissive: true, additive: true);
					}
				}
			}
			if (age < 0.7f && sink.Streaks != null)
			{
				int num13 = (int)(age * 22f);
				for (int j = 0; j < 7; j++)
				{
					float num14 = ((float)j + BlastFx.Rand(Seed, num13 * 7 + j)) / 7f * 6.283f;
					float num15 = num * (3f + 5f * BlastFx.Rand(Seed, num13 * 7 + j + 50));
					float num16 = ((BlastFx.Rand(Seed, num13 * 7 + j + 90) < 0.35f) ? (num * (2f + 4f * BlastFx.Rand(Seed, num13 * 7 + j + 91))) : 0f);
					float num17 = X + (float)Math.Cos(num14) * num15;
					float num18 = Y + (float)Math.Sin(num14) * num15;
					float z = ((sink.Ground != null) ? sink.Ground(num17, num18) : Ground) + 0.2f + num16;
					UfoArt.Lightning(sink, new Vec3(X, Y, Ground + 0.8f * num), new Vec3(num17, num18, z), 8, 0.7f * num, Seed, num13 * 7 + j, 1f - age / 0.7f);
				}
			}
			if (_wreck != null && age > 0.12f)
			{
				MeshInstance wreck = _wreck;
				float num19 = BlastFx.Clamp01((age - 15f) / 4f);
				float wreckScale = _wreckScale;
				wreck.Basis = _wreckTurn * wreckScale;
				Vec3 v = new Vec3(0f, 0f, 2.1000001f * wreckScale);
				Vec3 vec = new Vec3(X, Y, Ground + 1.26f * wreckScale - 0.35f * num19 * 7f * wreckScale);
				wreck.Position = vec - _wreckTurn.Mul(v);
				if (wreck.Palette == null || wreck.Palette.Length != wreck.Model.SlotNames.Length)
				{
					wreck.Palette = new uint[wreck.Model.SlotNames.Length];
				}
				bool flag = BlastFx.Rand(Seed, (int)(age * 9f)) < 0.3f && age < 12f;
				for (int k = 0; k < wreck.Model.SlotNames.Length; k++)
				{
					string text = wreck.Model.SlotNames[k];
					uint[] palette = wreck.Palette;
					int num20 = k;
					int num21;
					switch (text)
					{
					default:
						num21 = 0;
						break;
					case "accent":
					case "dark":
						num21 = (int)TerrainCache.Scale(UfoArt.Hull0, 0.75f);
						break;
					case "glow":
						num21 = (int)(flag ? UfoArt.Dome2 : UfoArt.Dome0);
						break;
					case "metal":
						num21 = (int)TerrainCache.Lerp(UfoArt.Hull1, UfoArt.Hull0, BlastFx.Clamp01(age / 3f) * 0.6f);
						break;
					}
					palette[num20] = (uint)num21;
				}
				wreck.Fade = 1f - num19;
				wreck.DissolveScale = ((num19 > 0f) ? 0.5f : 0f);
				wreck.SortDepth = num2;
				wreck.Emissive = false;
				sink.AddMesh(wreck);
				if (flag)
				{
					sink.Glow(vec.X, vec.Y, vec.Z + 0.8f * num, diameter * 0.4f, UfoArt.Dome2, 0.4f);
				}
			}
			if (parts == null || dt <= 0f || num2 > reach)
			{
				return;
			}
			float num22 = BlastFx.Clamp01(1f - age / 17f);
			_smokeClock += dt * ((age < 2f) ? 14f : 5f) * num22;
			while (_smokeClock > 1f)
			{
				_smokeClock -= 1f;
				int num23 = parts.List.Count + (int)(age * 733f);
				Vec3 at = new Vec3(X + (BlastFx.Rand(Seed, num23) - 0.5f) * diameter * 0.6f, Y + (BlastFx.Rand(Seed, num23 + 1) - 0.5f) * diameter * 0.6f, Ground + 0.8f * num);
				parts.Smoke(at, new Vec3(0f, 0f, 2.2f + 1.4f * BlastFx.Rand(Seed, num23 + 2)), 5f + 3f * BlastFx.Rand(Seed, num23 + 3), 1.2f * num, 4.5f * num, TerrainCache.Rgb(40, 36, 40), BlastFx.Smoke(0.5f), 200, (int)(BlastFx.Rand(Seed, num23 + 4) * 6f), 0.35f);
			}
			if (age > 0.3f && age < 14f)
			{
				_fireClock += dt * 9f * num5;
				while (_fireClock > 1f)
				{
					_fireClock -= 1f;
					int num24 = parts.List.Count + (int)(age * 977f);
					float num25 = BlastFx.Rand(Seed, num24) * 6.283f;
					float num26 = diameter * 0.35f * BlastFx.Rand(Seed, num24 + 1);
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X + (float)Math.Cos(num25) * num26, Y + (float)Math.Sin(num25) * num26, Ground + 0.4f * num + 0.6f * num * BlastFx.Rand(Seed, num24 + 2)),
						Vel = new Vec3(0f, 0f, 1.6f),
						Life = 0.5f + 0.4f * BlastFx.Rand(Seed, num24 + 3),
						Size = 0.5f * num,
						EndSize = 1.1f * num,
						Color = BlastFx.Heat(1.05f),
						EndColor = BlastFx.Heat(0.2f),
						Variant = (num24 & 7),
						Drag = 1f,
						Gravity = -2f,
						Kind = FxParticles.Kind.Fire
					});
				}
			}
			if (!(age > 0.8f) || !(age < 12f))
			{
				return;
			}
			_sparkClock += dt;
			if (_sparkClock > 0.5f + 0.9f * BlastFx.Rand(Seed, (int)(age * 3f)))
			{
				_sparkClock = 0f;
				Vec3 at2 = new Vec3(X + (BlastFx.Rand(Seed, (int)(age * 50f)) - 0.5f) * diameter * 0.6f, Y + (BlastFx.Rand(Seed, (int)(age * 50f) + 1) - 0.5f) * diameter * 0.6f, Ground + 0.9f * num);
				for (int l = 0; l < 8; l++)
				{
					parts.Spark(at2, new Vec3((BlastFx.Rand(Seed, l + (int)(age * 40f)) - 0.5f) * 5f, (BlastFx.Rand(Seed, l + 9 + (int)(age * 40f)) - 0.5f) * 5f, 2f + 3f * BlastFx.Rand(Seed, l + 19)), 0.5f, 0.1f, UfoArt.BeamCream, UfoArt.BeamGreen, 9f, 0.5f);
				}
			}
			if (BlastFx.Rand(Seed, (int)(age * 12f) + 500) < 0.12f)
			{
				int num27 = (int)(age * 12f);
				float num28 = BlastFx.Rand(Seed, num27 + 7) * 6.283f;
				UfoArt.Lightning(sink, new Vec3(X, Y, Ground + 1.1f * num), new Vec3(X + (float)Math.Cos(num28) * diameter * 0.5f, Y + (float)Math.Sin(num28) * diameter * 0.5f, Ground + 0.3f * num), 5, 0.3f * num, Seed + 5, num27, 0.85f);
			}
		}

		private void Burst(FxSink sink, FxParticles parts)
		{
			if (parts != null)
			{
				float num = Diameter / 4.8f;
				for (int i = 0; i < 22; i++)
				{
					float num2 = BlastFx.Rand(Seed, i) * 6.283f;
					float num3 = BlastFx.Rand(Seed, i + 1);
					float num4 = num * (3f + 6f * BlastFx.Rand(Seed, i + 2));
					bool flag = i < 9;
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X, Y, Ground + 0.6f * num),
						Vel = new Vec3((float)Math.Cos(num2) * num4 * (1f - num3 * 0.6f), (float)Math.Sin(num2) * num4 * (1f - num3 * 0.6f), num * (2f + 7f * num3)),
						Life = (flag ? (0.5f + 0.3f * BlastFx.Rand(Seed, i + 3)) : (0.9f + 0.7f * BlastFx.Rand(Seed, i + 3))),
						Size = num * (flag ? 1.6f : 1.3f),
						EndSize = num * (flag ? 3.2f : 3.6f),
						Color = (flag ? UfoArt.BeamCream : BlastFx.Heat(1.1f)),
						EndColor = (flag ? UfoArt.BeamGreen : BlastFx.Smoke(0.2f)),
						Variant = i,
						Drag = 2.4f,
						Gravity = -1.5f,
						Kind = FxParticles.Kind.Fire
					});
				}
				for (int j = 0; j < 30; j++)
				{
					float num5 = BlastFx.Rand(Seed, j + 100) * 6.283f;
					float num6 = num * (4f + 8f * BlastFx.Rand(Seed, j + 101));
					bool flag2 = j % 3 == 0;
					uint color = (flag2 ? UfoArt.BeamYellow : ((j % 3 == 1) ? UfoArt.Hull3 : UfoArt.Hull1));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X, Y, Ground + 0.8f * num),
						Vel = new Vec3((float)Math.Cos(num5) * num6, (float)Math.Sin(num5) * num6, num * (4f + 7f * BlastFx.Rand(Seed, j + 102))),
						Life = 1.6f + 1.2f * BlastFx.Rand(Seed, j + 103),
						Size = 0.14f + 0.12f * BlastFx.Rand(Seed, j + 104),
						EndSize = 0.12f,
						Color = color,
						EndColor = (flag2 ? UfoArt.BeamGreen : UfoArt.Hull0),
						Gravity = 9.8f,
						Drag = 0.3f,
						Kind = ((!flag2) ? FxParticles.Kind.Drop : FxParticles.Kind.Spark),
						Matte = !flag2,
						Bounce = true,
						Trail = (flag2 && j % 2 == 0)
					});
				}
				uint num7 = TerrainCache.Lerp(TerrainCache.Rgb(140, 124, 100), _land, 0.35f);
				for (int k = 0; k < 18; k++)
				{
					float num8 = ((float)k + BlastFx.Rand(Seed, k + 200)) / 18f * 6.283f;
					float num9 = num * (4f + 3f * BlastFx.Rand(Seed, k + 201));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X + (float)Math.Cos(num8) * num, Y + (float)Math.Sin(num8) * num, Ground + 0.5f),
						Vel = new Vec3((float)Math.Cos(num8) * num9, (float)Math.Sin(num8) * num9, 0.6f + BlastFx.Rand(Seed, k + 202)),
						Life = 1.6f + BlastFx.Rand(Seed, k + 203),
						Size = 1.4f * num,
						EndSize = 3.6f * num,
						Color = num7,
						EndColor = TerrainCache.Scale(num7, 0.8f),
						Alpha = 180,
						Variant = k,
						Drag = 1.6f,
						Gravity = -0.3f,
						Kind = FxParticles.Kind.Smoke
					});
				}
			}
		}
	}
}
