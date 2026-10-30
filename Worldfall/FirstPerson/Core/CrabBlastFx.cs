using System;

namespace FirstPerson.Core
{
	public sealed class CrabBlastFx
	{
		public readonly float X;

		public readonly float Y;

		public readonly float Ground;

		public readonly float Scale;

		public readonly uint Seed;

		public float Age;

		private readonly uint _land;

		private readonly Billows _billows = new Billows
		{
			Max = 80
		};

		private readonly SpeedLines _lines = new SpeedLines();

		private bool _burst;

		private float _fireClock;

		private float _smokeClock;

		public bool Finished
		{
			get
			{
				if (Age > 7f)
				{
					return _billows.List.Count == 0;
				}
				return false;
			}
		}

		public CrabBlastFx(float x, float y, float ground, float scale, uint seed, uint land)
		{
			X = x;
			Y = y;
			Ground = ground;
			Scale = scale;
			Seed = seed;
			_land = land;
		}

		public void Step(float dt, FxSink sink, FxParticles parts)
		{
			Age += dt;
			float age = Age;
			float scale = Scale;
			float num = sink.Distance(X, Y);
			if (!_burst)
			{
				_burst = true;
				Burst(sink, parts);
				sink.Shake = Math.Max(sink.Shake, 0.6f * BlastFx.Clamp01(1f - num / 30f));
			}
			if (age < 0.22f)
			{
				float num2 = 1f - age / 0.22f;
				sink.Glow(X, Y, Ground + 0.9f * scale, scale * (3.2f + 4f * age), TerrainCache.Rgb(255, 255, 250), num2);
				sink.Sprite(BlastFx.StarSprite, X, Y, Ground + 1f * scale, scale * 6f, scale * 6f, TerrainCache.Rgb(255, 240, 200), (byte)Math.Max(2, (int)(230f * num2)), emissive: true, additive: true);
				sink.AddFlash(0.18f * num2 * BlastFx.Clamp01(1f - num / 40f), TerrainCache.Rgb(255, 236, 200));
			}
			float num3 = scale * 5.5f * BlastFx.Smooth(age / 0.35f);
			if (age < 1.1f && parts != null && dt > 0f)
			{
				_fireClock += dt * 42f;
				while (_fireClock > 1f)
				{
					_fireClock -= 1f;
					int num4 = parts.List.Count + (int)(age * 911f);
					float num5 = PowerFx.Rand(Seed, num4);
					bool flag = age > 0.3f && PowerFx.Rand(Seed, num4 + 1) < 0.6f;
					float z = (flag ? (Ground + num3 + scale * (0.3f + 0.6f * num5)) : (Ground + num3 * num5));
					float num6 = (flag ? (scale * (0.8f + 1.2f * age)) : (scale * 0.35f));
					float num7 = PowerFx.Rand(Seed, num4 + 2) * 6.2832f;
					float num8 = num6 * PowerFx.Rand(Seed, num4 + 3);
					float num9 = (flag ? (scale * (1.2f + 0.9f * PowerFx.Rand(Seed, num4 + 4))) : (scale * (0.8f + 0.5f * PowerFx.Rand(Seed, num4 + 4))));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X + (float)Math.Cos(num7) * num8, Y + (float)Math.Sin(num7) * num8, z),
						Vel = new Vec3((float)Math.Cos(num7) * num8 * 1.5f, (float)Math.Sin(num7) * num8 * 1.5f, flag ? (scale * 2.5f) : (scale * 7f)),
						Life = (flag ? 0.7f : 0.35f),
						Size = num9,
						EndSize = num9 * 1.5f,
						Drag = 2.5f,
						Gravity = -1f,
						Color = BlastFx.Heat(flag ? 0.95f : 1.05f),
						EndColor = BlastFx.Heat(flag ? 0.3f : 0.5f),
						Kind = FxParticles.Kind.Fire,
						Variant = num4
					});
				}
			}
			if (age < 1.2f)
			{
				sink.Glow(X, Y, Ground + num3 * 0.6f, scale * (4f + 2f * age), BlastFx.Heat(1f), 0.55f * (1f - age / 1.2f));
			}
			if (age > 0.15f && age < 1.6f)
			{
				FireRing(sink, age);
			}
			sink.Light(X, Y, Ground + 2f * scale, 10f * scale, BlastFx.Heat(0.95f), 1.5f * (float)Math.Exp((0f - age) / 0.3f) + 0.3f * (float)Math.Exp((0f - age) / 2f), (age < 0.4f) ? (2f * scale) : 0f);
			if (age > 0.45f && age < 1.6f && dt > 0f)
			{
				_smokeClock += dt * 16f;
				while (_smokeClock > 1f)
				{
					_smokeClock -= 1f;
					int num10 = _billows.List.Count * 11 + (int)(age * 701f);
					float num11 = PowerFx.Rand(Seed, num10) * 6.2832f;
					float num12 = scale * (0.8f + 1.4f * PowerFx.Rand(Seed, num10 + 1));
					float num13 = scale * (0.7f + 0.5f * PowerFx.Rand(Seed, num10 + 2));
					float num14 = scale * (1.4f + 1.8f * PowerFx.Rand(Seed, num10 + 3));
					_billows.Add(new Vec3(X + (float)Math.Cos(num11) * num12, Y + (float)Math.Sin(num11) * num12, Ground + num3 * (0.35f + 0.45f * PowerFx.Rand(Seed, num10 + 4))), new Vec3((float)Math.Cos(num11) * num14, (float)Math.Sin(num11) * num14, scale * 0.3f), num13, num13 * 1.9f, 1.1f + 1.1f * PowerFx.Rand(Seed, num10 + 5), 1f, num10, 0.2f);
				}
			}
			float num15 = (float)Math.Exp((0f - age) / 2.5f);
			if (age > 0.3f && num15 > 0.03f)
			{
				for (int i = 0; i < 5; i++)
				{
					float num16 = (float)i * 1.257f + PowerFx.Rand(Seed, i + 60);
					float num17 = scale * 0.9f * PowerFx.Rand(Seed, i + 61);
					float num18 = X + (float)Math.Cos(num16) * num17;
					float num19 = Y + (float)Math.Sin(num16) * num17;
					float num20 = ((sink.Ground != null) ? sink.Ground(num18, num19) : Ground);
					sink.Glow(num18, num19, num20 + 0.12f, scale * 1.6f, BlastFx.Heat(0.55f + 0.4f * num15), 0.6f * num15);
				}
			}
			_billows.Step(dt);
			_billows.Draw(sink);
			_lines.Step(dt, 6f);
			_lines.Draw(sink);
		}

		private void FireRing(FxSink sink, float t)
		{
			float scale = Scale;
			float num = (t - 0.15f) / 1.45f;
			float num2 = scale * (0.6f + 2.6f * (1f - (1f - num) * (1f - num)));
			int num3 = 14;
			for (int i = 0; i < num3; i++)
			{
				float num4 = ((float)i + PowerFx.Rand(Seed, i + 80) * 0.6f) / (float)num3 * 6.2832f;
				float num5 = X + (float)Math.Cos(num4) * num2;
				float num6 = Y + (float)Math.Sin(num4) * num2;
				float num7 = ((sink.Ground != null) ? sink.Ground(num5, num6) : Ground);
				float num8 = scale * (0.9f + 0.4f * PowerFx.Rand(Seed, i + 81)) * (1f - 0.5f * num);
				byte alpha = (byte)((!(num < 0.5f)) ? ((byte)Math.Max(2, (int)(255f * (1f - (num - 0.5f) / 0.5f)))) : 0);
				sink.Sprite(BlastFx.Puff(i), num5, num6, num7 + num8 * 0.35f, num8, num8 * 0.8f, BlastFx.Heat(1.05f - 0.6f * num), alpha, emissive: true, additive: false, (i & 1) != 0);
			}
		}

		private void Burst(FxSink sink, FxParticles parts)
		{
			float scale = Scale;
			if (parts != null)
			{
				for (int i = 0; i < 26; i++)
				{
					float num = PowerFx.Rand(Seed, i + 100) * 6.283f;
					float num2 = scale * (4f + 8f * PowerFx.Rand(Seed, i + 101));
					parts.Spark(new Vec3(X, Y, Ground + 0.5f * scale), new Vec3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, scale * (3f + 8f * PowerFx.Rand(Seed, i + 102))), 0.7f + 0.8f * PowerFx.Rand(Seed, i + 103), 0.1f + 0.08f * PowerFx.Rand(Seed, i + 104), BlastFx.Heat(1.15f), BlastFx.Heat(0.4f), 9.8f);
				}
				uint num3 = TerrainCache.Lerp(TerrainCache.Rgb(96, 74, 54), _land, 0.4f);
				for (int j = 0; j < 12; j++)
				{
					float num4 = PowerFx.Rand(Seed, j + 200) * 6.283f;
					float num5 = scale * (2f + 4f * PowerFx.Rand(Seed, j + 201));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X, Y, Ground + 0.3f),
						Vel = new Vec3((float)Math.Cos(num4) * num5, (float)Math.Sin(num4) * num5, scale * (3f + 4f * PowerFx.Rand(Seed, j + 202))),
						Life = 2.5f,
						Size = 0.16f * scale,
						EndSize = 0.14f * scale,
						Gravity = 9.8f,
						Drag = 0.2f,
						Color = num3,
						EndColor = num3,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
			}
			for (int k = 0; k < 8; k++)
			{
				float num6 = PowerFx.Rand(Seed, k + 300) * 6.283f;
				float z = 0.3f + 0.9f * PowerFx.Rand(Seed, k + 301);
				Vec3 vec = new Vec3((float)Math.Cos(num6), (float)Math.Sin(num6), z).Normalized();
				_lines.Add(new Vec3(X, Y, Ground + 0.6f * scale), vec * (scale * (12f + 8f * PowerFx.Rand(Seed, k + 302))), scale * 1.8f, 0.16f * scale, 0.35f, TerrainCache.Rgb(255, 250, 220));
			}
		}
	}
}
