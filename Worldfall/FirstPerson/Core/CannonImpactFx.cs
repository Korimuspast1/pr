using System;

namespace FirstPerson.Core
{
	public sealed class CannonImpactFx
	{
		public enum Kind : byte
		{
			Land,
			Water,
			Hull
		}

		public readonly float X;

		public readonly float Y;

		public readonly float Z;

		public readonly float Scale;

		public readonly uint Seed;

		public readonly Kind What;

		public readonly float Water;

		public float Detail = 1f;

		public float Age;

		private readonly uint _land;

		private readonly Billows _billows = new Billows
		{
			Max = 30
		};

		private readonly SpeedLines _lines = new SpeedLines();

		private bool _burst;

		private float _smokeClock;

		public bool Finished
		{
			get
			{
				if (Age > 4.2f && _billows.List.Count == 0)
				{
					return _lines.Count == 0;
				}
				return false;
			}
		}

		public CannonImpactFx(float x, float y, float z, float scale, uint seed, Kind what, uint land, float water)
		{
			X = x;
			Y = y;
			Z = z;
			Scale = scale;
			Seed = seed;
			What = what;
			_land = land;
			Water = water;
		}

		private float R(int i)
		{
			return CannonFx.R(Seed, i);
		}

		private int Count(int full)
		{
			return Math.Max(1, (int)((float)full * Detail + 0.5f));
		}

		public void Step(float dt, FxSink sink, FxParticles parts)
		{
			Age += dt;
			float age = Age;
			_ = Scale;
			float dist = sink.Distance(X, Y);
			if (!_burst)
			{
				_burst = true;
				Burst(sink, parts, dist);
			}
			if (What == Kind.Water)
			{
				StepSplash(sink, age, dist);
			}
			else
			{
				StepBlast(sink, age, dist, dt);
			}
			_billows.Step(dt);
			_billows.Draw(sink);
			_lines.Step(dt, 5f);
			_lines.Draw(sink);
		}

		private void StepBlast(FxSink sink, float t, float dist, float dt)
		{
			float num = ((What == Kind.Hull) ? (Scale * 0.85f) : Scale);
			if (t < 0.16f)
			{
				float num2 = 1f - t / 0.16f;
				sink.Glow(X, Y, Z + 0.7f * num, num * (3.8f + 8f * t), TerrainCache.Rgb(255, 254, 246), num2);
				float num3 = 1f - t / 0.09f;
				if (num3 > 0f)
				{
					sink.Sprite(BlastFx.StarSprite, X, Y, Z + 0.8f * num, num * (4f + 3f * t / 0.09f), num * (4f + 3f * t / 0.09f), TerrainCache.Rgb(255, 238, 196), (byte)Math.Max(2, (int)(220f * num3 * num3)), emissive: true, additive: true);
				}
				sink.AddFlash(0.14f * num2 * BlastFx.Clamp01(1f - dist / 34f) * BlastFx.Clamp01(0.3f + sink.Facing(X, Y)), TerrainCache.Rgb(255, 236, 200));
			}
			if (t < 0.7f)
			{
				sink.Glow(X, Y, Z + 1.2f * num, num * (3.6f + 2.5f * t), BlastFx.Heat(1f), 0.6f * (1f - t / 0.7f));
			}
			if (t > 0.08f && t < 0.8f && dt > 0f)
			{
				_smokeClock += dt * 20f * Math.Max(0.5f, Detail);
				while (_smokeClock > 1f)
				{
					_smokeClock -= 1f;
					int num4 = _billows.List.Count * 13 + (int)(t * 613f);
					float num5 = R(num4) * 6.2832f;
					float num6 = num * (0.2f + 0.7f * R(num4 + 1));
					float num7 = num * (0.85f + 0.5f * R(num4 + 2));
					float num8 = num * (0.8f + 1.4f * R(num4 + 3));
					_billows.Add(new Vec3(X + (float)Math.Cos(num5) * num6, Y + (float)Math.Sin(num5) * num6, Z + num * (0.6f + 1.4f * R(num4 + 4))), new Vec3((float)Math.Cos(num5) * num8, (float)Math.Sin(num5) * num8, num * (1.4f + 1.6f * R(num4 + 5))), num7, num7 * 2.2f, 1.6f + 1f * R(num4 + 6), 1f, num4, 0.35f);
				}
			}
			float num9 = (float)Math.Exp((0f - t) / 2.2f);
			if (What == Kind.Land && t > 0.2f && num9 > 0.03f)
			{
				for (int i = 0; i < 4; i++)
				{
					float num10 = (float)i * 1.571f + R(i + 60);
					float num11 = num * 0.7f * R(i + 61);
					float num12 = X + (float)Math.Cos(num10) * num11;
					float num13 = Y + (float)Math.Sin(num10) * num11;
					float num14 = ((sink.Ground != null) ? sink.Ground(num12, num13) : Z);
					sink.Glow(num12, num13, num14 + 0.1f, num * 1.4f, BlastFx.Heat(0.55f + 0.4f * num9), 0.55f * num9);
				}
			}
			if (What == Kind.Hull && !float.IsNaN(Water) && t < 1.5f)
			{
				CannonFx.FoamRing(sink, new Vec3(X, Y, Water), num * (0.6f + 2f * (1f - (float)Math.Exp(-2.5f * t))), num * 0.8f, 0.6f * (1f - t / 1.5f), Seed, 10, CannonFx.Foam);
			}
			sink.Light(X, Y, Z + 1.2f * num, 10f * num, BlastFx.Heat(0.95f), 2.1f * (float)Math.Exp((0f - t) / 0.08f) + 0.45f * (float)Math.Exp((0f - t) / 0.6f), (t < 0.12f) ? (1.6f * num) : 0f);
		}

		private void StepSplash(FxSink sink, float t, float dist)
		{
			float scale = Scale;
			if (t < 0.09f)
			{
				float num = 1f - t / 0.09f;
				sink.Glow(X, Y, Z + 0.35f * scale, scale * 2.4f, BlastFx.Heat(1f), 0.75f * num);
				sink.Light(X, Y, Z + 0.5f * scale, 7f * scale, BlastFx.Heat(1f), 1.4f * num);
			}
			float num2 = BlastFx.Smooth(t / 0.26f);
			float num3 = BlastFx.Smooth((t - 0.5f) / 0.95f);
			float num4 = scale * 4.8f * num2 * (1f - 0.8f * num3);
			float num5 = (1f - BlastFx.Smooth((t - 0.6f) / 0.9f)) * BlastFx.Smooth(t / 0.04f);
			byte alpha = (byte)((!(num5 > 0.97f) || !(num3 < 0.05f)) ? ((byte)Math.Max(2, (int)(240f * num5))) : 0);
			if (num5 > 0.02f && num4 > 0.05f)
			{
				for (int i = 0; i < 6; i++)
				{
					float num6 = (float)i * 1.047f + R(i + 240) * 0.6f;
					float num7 = scale * (0.5f + 0.7f * num2 + 0.9f * num3);
					float num8 = scale * (0.9f + 0.4f * R(i + 241)) * (1f - 0.3f * num3);
					sink.Sprite(BlastFx.Puff(i + 1), X + (float)Math.Cos(num6) * num7, Y + (float)Math.Sin(num6) * num7, Z + num8 * 0.3f, num8, num8 * 0.8f, TerrainCache.Lerp(CannonFx.FoamShade, CannonFx.Foam, 0.6f), alpha, emissive: false, additive: false, (i & 1) != 0);
				}
				for (int j = 0; j < 9; j++)
				{
					float num9 = ((float)j + 0.5f) / 9f;
					float z = Z + num4 * num9;
					float num10 = scale * (1.15f + 0.9f * num9 * num9 + 1.4f * num3 * (1f - 0.4f * num9));
					float num11 = (R(j + 200) - 0.5f) * 0.3f * scale;
					uint multiply = TerrainCache.Lerp(CannonFx.FoamShade, CannonFx.Foam, 0.45f + 0.55f * num9);
					sink.Sprite(BlastFx.Puff(j + (int)(Seed & 3)), X + num11, Y + num11 * 0.6f, z, num10, num10 * 1.1f, multiply, alpha, emissive: false, additive: false, (j & 1) != 0);
				}
				for (int k = 0; k < 7; k++)
				{
					float num12 = (float)k * 0.898f + R(k + 220) * 0.5f;
					float num13 = scale * (0.4f + 1f * num2 + 0.9f * num3);
					float z2 = Z + num4 * (0.9f - 0.3f * num3) - scale * 0.6f * num3 * R(k + 222);
					float num14 = scale * (0.6f + 0.35f * R(k + 221));
					sink.Sprite(BlastFx.Puff(k + 3), X + (float)Math.Cos(num12) * num13, Y + (float)Math.Sin(num12) * num13, z2, num14, num14, CannonFx.Foam, alpha, emissive: false, additive: false, (k & 1) != 0);
				}
			}
			if (t < 2f)
			{
				float num15 = t / 2f;
				CannonFx.FoamRing(sink, new Vec3(X, Y, Water), scale * (0.5f + 3f * (1f - (float)Math.Exp(-2.2f * t))), scale * (0.8f + 0.4f * num15), 0.8f * (1f - num15) * BlastFx.Smooth(t / 0.08f), Seed, (Detail > 0.6f) ? 16 : 9, CannonFx.Foam);
			}
		}

		private void Burst(FxSink sink, FxParticles parts, float dist)
		{
			float scale = Scale;
			Vec3 axis = new Vec3(0f, 0f, 1f);
			sink.Shake = Math.Max(sink.Shake, ((What == Kind.Water) ? 0.3f : 0.55f) * BlastFx.Clamp01(1f - dist / 26f));
			if (What != Kind.Water)
			{
				for (int i = 0; i < 7; i++)
				{
					float num = R(i + 300) * 6.2832f;
					Vec3 vec = new Vec3((float)Math.Cos(num), (float)Math.Sin(num), 0.35f + 0.9f * R(i + 301)).Normalized();
					_lines.Add(new Vec3(X, Y, Z + 0.4f * scale), vec * (scale * (13f + 7f * R(i + 302))), scale * 1.5f, 0.13f * scale, 0.3f, TerrainCache.Rgb(255, 248, 222));
				}
			}
			if (parts == null)
			{
				return;
			}
			if (What == Kind.Water)
			{
				int j = 0;
				for (int num2 = Count(44); j < num2; j++)
				{
					float num3 = R(j);
					float num4 = R(j + 1) * 6.2832f;
					float num5 = scale * 0.4f * (float)Math.Sqrt(R(j + 2));
					float num6 = scale * (5f + 5.5f * (1f - num3));
					float num7 = scale * (0.5f + 2.8f * num3);
					float num8 = scale * (0.12f + 0.08f * R(j + 3));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X + (float)Math.Cos(num4) * num5, Y + (float)Math.Sin(num4) * num5, Z + 0.05f),
						Vel = new Vec3((float)Math.Cos(num4) * num7, (float)Math.Sin(num4) * num7, num6),
						Gravity = 9.8f,
						Drag = 0.25f,
						Life = Math.Min(2.4f, 2f * num6 / 9.8f * 0.97f),
						Size = num8,
						EndSize = num8 * 0.85f,
						Color = CannonFx.Foam,
						EndColor = CannonFx.FoamShade,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
				int k = 0;
				for (int num9 = Count(6); k < num9; k++)
				{
					float num10 = R(k + 40) * 6.2832f;
					float num11 = scale * (0.8f + 1.4f * R(k + 41));
					parts.Smoke(new Vec3(X, Y, Z + 0.4f * scale), new Vec3((float)Math.Cos(num10) * num11, (float)Math.Sin(num10) * num11, 0.5f * scale), 2.2f + 0.8f * R(k + 42), 0.8f * scale, scale * (2.4f + 0.8f * R(k + 43)), TerrainCache.Rgb(244, 248, 252), TerrainCache.Rgb(222, 230, 238), 160, k + 9, 1.4f);
				}
				return;
			}
			bool flag = What == Kind.Hull;
			int l = 0;
			for (int num12 = Count(flag ? 14 : 18); l < num12; l++)
			{
				Vec3 vec2 = CannonFx.Cone(axis, 1.4f, R(l + 10), R(l + 11));
				parts.Add(new FxParticles.P
				{
					Pos = new Vec3(X, Y, Z + 0.3f * scale),
					Vel = vec2 * (scale * (3f + 4.5f * R(l + 12))),
					Life = 0.3f + 0.25f * R(l + 13),
					Size = scale * (0.9f + 0.4f * R(l + 14)),
					EndSize = scale * (1.9f + 0.7f * R(l + 15)),
					Drag = 4f,
					Gravity = -2f,
					Color = BlastFx.Heat(1.12f),
					EndColor = BlastFx.Heat(0.55f),
					Kind = FxParticles.Kind.Fire,
					Variant = l
				});
			}
			int m = 0;
			for (int num13 = Count(8); m < num13; m++)
			{
				parts.Add(new FxParticles.P
				{
					Pos = new Vec3(X, Y, Z + 0.4f * scale),
					Vel = CannonFx.Cone(axis, 0.18f, R(m + 30), R(m + 31)) * (scale * (7f + 5f * R(m + 32))),
					Life = 0.28f + 0.12f * R(m + 33),
					Size = scale * (0.6f + 0.3f * R(m + 34)),
					EndSize = scale * (1.2f + 0.4f * R(m + 35)),
					Drag = 3f,
					Gravity = 2f,
					Color = BlastFx.Heat(1.15f),
					EndColor = BlastFx.Heat(0.6f),
					Kind = FxParticles.Kind.Fire,
					Variant = m + 5
				});
			}
			int n = 0;
			for (int num14 = Count(20); n < num14; n++)
			{
				float num15 = R(n + 100) * 6.283f;
				float num16 = scale * (4f + 8f * R(n + 101));
				parts.Spark(new Vec3(X, Y, Z + 0.4f * scale), new Vec3((float)Math.Cos(num15) * num16, (float)Math.Sin(num15) * num16, scale * (3f + 7f * R(n + 102))), 0.6f + 0.6f * R(n + 103), 0.09f + 0.06f * R(n + 104), BlastFx.Heat(1.15f), BlastFx.Heat(0.4f), 9.8f);
			}
			uint num17 = TerrainCache.Lerp(TerrainCache.Rgb(96, 74, 54), _land, 0.4f);
			uint[] array = new uint[3]
			{
				TerrainCache.Rgb(124, 86, 52),
				TerrainCache.Rgb(82, 56, 36),
				TerrainCache.Rgb(158, 118, 76)
			};
			int num18 = 0;
			for (int num19 = Count(flag ? 22 : 16); num18 < num19; num18++)
			{
				float num20 = R(num18 + 200) * 6.283f;
				float num21 = scale * (2f + 5f * R(num18 + 201));
				uint num22 = (flag ? array[num18 % 3] : num17);
				float num23 = scale * (flag ? (0.1f + 0.1f * R(num18 + 203)) : (0.12f + 0.08f * R(num18 + 203)));
				parts.Add(new FxParticles.P
				{
					Pos = new Vec3(X, Y, Z + 0.3f * scale),
					Vel = new Vec3((float)Math.Cos(num20) * num21, (float)Math.Sin(num20) * num21, scale * (3.5f + 4.5f * R(num18 + 202))),
					Life = 2.2f,
					Size = num23,
					EndSize = num23 * 0.9f,
					Gravity = 9.8f,
					Drag = 0.2f,
					Color = num22,
					EndColor = num22,
					Kind = FxParticles.Kind.Drop,
					Matte = true
				});
			}
			if (flag)
			{
				int num24 = 0;
				for (int num25 = Count(12); num24 < num25; num24++)
				{
					float num26 = R(num24 + 400) * 6.283f;
					float num27 = scale * (2.5f + 3f * R(num24 + 401));
					float num28 = scale * (1f + 2f * R(num24 + 402));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X, Y, Water + 0.05f),
						Vel = new Vec3((float)Math.Cos(num26) * num28, (float)Math.Sin(num26) * num28, num27),
						Gravity = 9.8f,
						Drag = 0.3f,
						Life = Math.Min(1.8f, 2f * num27 / 9.8f),
						Size = 0.09f * scale,
						EndSize = 0.08f * scale,
						Color = CannonFx.Foam,
						EndColor = CannonFx.FoamShade,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
			}
			else
			{
				uint num29 = TerrainCache.Lerp(TerrainCache.Rgb(170, 154, 128), _land, 0.35f);
				int num30 = 0;
				for (int num31 = Count(12); num30 < num31; num30++)
				{
					float num32 = ((float)num30 + R(num30 + 500) * 0.6f) / 12f * 6.283f;
					float num33 = scale * (3f + 2.5f * R(num30 + 501));
					parts.Smoke(new Vec3(X, Y, Z + 0.3f * scale), new Vec3((float)Math.Cos(num32) * num33, (float)Math.Sin(num32) * num33, 0.3f * scale), 1.5f + 0.5f * R(num30 + 502), 0.6f * scale, scale * (1.8f + 0.5f * R(num30 + 503)), num29, TerrainCache.Lerp(num29, TerrainCache.Rgb(200, 192, 180), 0.5f), 165, num30, 2.6f);
				}
			}
		}
	}
}
