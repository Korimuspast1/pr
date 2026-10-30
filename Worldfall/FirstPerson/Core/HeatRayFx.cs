using System;

namespace FirstPerson.Core
{
	public sealed class HeatRayFx
	{
		public float X;

		public float Y;

		public float Reach;

		public bool Touching;

		public float Held;

		public static float Width = 3.5f;

		public static float Top = 260f;

		private const float Segment = 10f;

		private readonly uint _seed;

		private readonly Billows _billows = new Billows
		{
			Max = 220
		};

		private readonly SpeedLines _lines = new SpeedLines();

		private bool _placed;

		private float _base;

		private float _billowClock;

		private float _lineClock;

		private float _ashClock;

		private float _lavaClock;

		private float _plumeClock;

		private float _wasTouching;

		private float _tx;

		private float _ty;

		public bool Done
		{
			get
			{
				if (Reach <= 0.001f && _base <= 0.001f && _billows.List.Count == 0)
				{
					return _lines.Count == 0;
				}
				return false;
			}
		}

		public HeatRayFx(uint seed)
		{
			_seed = seed;
		}

		public void Aim(float x, float y, float reach, bool touching)
		{
			_tx = x;
			_ty = y;
			if (!_placed)
			{
				X = x;
				Y = y;
				_placed = true;
			}
			Reach = BlastFx.Clamp01(reach);
			Touching = touching && Reach > 0.98f;
		}

		public void Step(float dt, FxSink sink, FxParticles parts, float time, uint land)
		{
			float num = _tx - X;
			float num2 = _ty - Y;
			if (num * num + num2 * num2 > 900f)
			{
				X = _tx;
				Y = _ty;
			}
			else
			{
				float num3 = 1f - (float)Math.Exp(-16f * dt);
				X += num * num3;
				Y += num2 * num3;
			}
			Held = (Touching ? (Held + dt) : 0f);
			_base = (Touching ? Math.Min(1f, _base + dt * 5f) : Math.Max(0f, _base - dt * 2.2f));
			float ground = ((sink.Ground != null) ? sink.Ground(X, Y) : 0f);
			float num4 = sink.Distance(X, Y);
			if (Reach > 0.001f)
			{
				DrawBeam(sink, ground, time, num4);
			}
			if (_base > 0.001f)
			{
				DrawStrike(sink, parts, ground, time, num4, dt, land);
				DrawWind(sink, time);
			}
			if (Touching && _wasTouching < 0.5f)
			{
				float num5 = BlastFx.Clamp01(1.2f - num4 / 90f);
				sink.AddFlash(0.32f * num5 * (0.4f + 0.6f * Math.Max(0f, sink.Facing(X, Y))), TerrainCache.Rgb(255, 214, 140));
				sink.Shake = Math.Max(sink.Shake, 0.5f * BlastFx.Clamp01(1f - num4 / 60f));
			}
			_wasTouching = (Touching ? 1f : 0f);
			_billows.Step(dt);
			_billows.Draw(sink);
			_lines.Step(dt, 3f);
			_lines.Draw(sink);
		}

		private void DrawBeam(FxSink sink, float ground, float time, float dist)
		{
			float num = ground + Top;
			float num2 = num - Top * Reach - (Touching ? 0.4f : 0f);
			float num3;
			switch ((int)(time * 10f) & 3)
			{
			default:
				num3 = 1f;
				break;
			case 2:
				num3 = 0.93f;
				break;
			case 0:
				num3 = 0.78f;
				break;
			}
			float num4 = num3 * (1f + 0.05f * (float)Math.Sin(time * 47f + (float)_seed));
			float num5 = Width * num4 * (0.6f + 0.4f * BlastFx.Smooth(Reach * 3f));
			int frame = (int)(time * 24f);
			float num6 = ((dist > 120f) ? 20f : 10f);
			float num7 = Math.Min(num, num2 + Math.Max(60f, dist * 2.6f + 40f));
			int num8 = 0;
			float num9 = num2;
			while (num9 < num7 && num8 < 40)
			{
				float z = num9 + num6 * 0.5f;
				sink.Billboards.Add(Band(PowerFx.BeamHalo, num9, num6, num5 * 3.4f, 120, additive: true));
				sink.Billboards.Add(Band(PowerFx.Beam(frame), num9, num6, num5, 235, additive: false));
				sink.Billboards.Add(Band(PowerFx.BeamCore, num9, num6, num5 * 0.34f, 210, additive: true));
				if (num8 == 0 || (num8 & 3) == 0)
				{
					sink.Glow(X, Y, z, num5 * 5f, TerrainCache.Rgb(255, 120, 30), 0.18f);
				}
				num9 += num6;
				num8++;
			}
			if (!Touching)
			{
				sink.Glow(X, Y, num2 + 0.6f, num5 * 3.2f, PowerFx.RayCream, 0.9f);
				sink.Glow(X, Y, num2 + 0.6f, num5 * 6.5f, PowerFx.RayOrange, 0.5f);
			}
			sink.Light(X, Y, Math.Max(ground + 6f, num2 + 4f), 16f, TerrainCache.Rgb(255, 140, 50), 0.45f * BlastFx.Clamp01(Reach * 2f));
		}

		private Billboard Band(SpriteImage img, float z, float h, float w, byte alpha, bool additive)
		{
			return new Billboard
			{
				X = X,
				Y = Y,
				Z = z,
				GroundZ = z,
				Width = w,
				Height = h,
				Image = img,
				Kind = BillboardKind.Facing,
				Tag = -1,
				Emissive = true,
				Alpha = alpha,
				Additive = additive,
				Far = true
			};
		}

		private void DrawStrike(FxSink sink, FxParticles parts, float ground, float time, float dist, float dt, uint land)
		{
			float num = _base;
			float num2 = BlastFx.Clamp01(Held / 1.5f);
			uint seed = _seed;
			Func<float, float, float> func = sink.Ground ?? ((Func<float, float, float>)((float x, float y) => ground));
			float num3;
			switch ((int)(time * 10f) % 5)
			{
			default:
				num3 = 0.4f;
				break;
			case 2:
				num3 = 0.8f;
				break;
			case 1:
				num3 = 1f;
				break;
			}
			float num4 = (5.5f + 2.2f * num3) * num;
			sink.Sprite(PowerFx.HeatCross, X, Y, ground + 0.9f, num4 * 1.6f, num4 * 1.6f, 0u, (byte)Math.Max(2, (int)(235f * num)), emissive: true, additive: true);
			sink.Glow(X, Y, ground + 0.8f, 6f * num, PowerFx.RayCream, 0.85f * num);
			sink.Glow(X, Y, ground + 0.6f, 12f * num, PowerFx.RayOrange, 0.45f * num);
			for (int num5 = 0; num5 < 2; num5++)
			{
				float num6 = (time * 1.9f + (float)num5 * 0.5f) % 1f;
				float num7 = 1.8f + 8.4f * BlastFx.Smooth(num6 * 1.1f);
				float num8 = (1f - num6) * (1f - num6) * num;
				if (num8 < 0.03f)
				{
					continue;
				}
				int num9 = 26;
				for (int num10 = 0; num10 < num9; num10++)
				{
					if ((num10 + num5 * 3) % 7 != 0)
					{
						float num11 = ((float)num10 + 0.5f) / (float)num9 * 6.2832f + (float)num5 * 0.4f;
						float num12 = X + (float)Math.Cos(num11) * num7;
						float num13 = Y + (float)Math.Sin(num11) * num7;
						float num14 = func(num12, num13);
						sink.Sprite(PowerFx.Dash, num12, num13, num14 + 0.25f, 1.2f + num7 * 0.09f, 0.45f, TerrainCache.Lerp(PowerFx.RayCream, PowerFx.RayAmber, num6), (byte)Math.Max(2, (int)(255f * num8)), emissive: true, additive: true);
					}
				}
			}
			float num15 = (1.6f + 1.6f * num2) * num;
			for (int num16 = 0; num16 < 7; num16++)
			{
				float num17 = (float)num16 * 0.9f + time * 0.7f;
				float num18 = num15 * (0.25f + 0.45f * PowerFx.Rand(seed, num16));
				float num19 = X + (float)Math.Cos(num17) * num18;
				float num20 = Y + (float)Math.Sin(num17) * num18;
				float num21 = 0.6f + 0.4f * (float)Math.Sin(time * (5f + (float)num16) + (float)num16 * 2.1f);
				sink.Glow(num19, num20, func(num19, num20) + 0.15f, num15 * (0.9f + 0.5f * num21), BlastFx.Heat(0.75f + 0.2f * num21), 0.55f * num);
			}
			sink.Light(X, Y, ground + 1.6f, 13f + 4f * num2, TerrainCache.Rgb(255, 150, 60), (1.05f + 0.25f * (float)Math.Sin(time * 23f)) * num, 3f * num);
			if (dt <= 0f)
			{
				return;
			}
			bool flag = dist < 140f;
			_billowClock += dt * (flag ? 28f : 10f) * num * (0.6f + 0.4f * num2);
			int num22 = 0;
			while (_billowClock > 1f && num22 < 6)
			{
				_billowClock -= 1f;
				num22++;
				int num23 = _billows.List.Count * 7 + (int)(time * 131f);
				float num24 = PowerFx.Rand(seed, num23) * 6.2832f;
				float num25 = 2.2f + 2.6f * PowerFx.Rand(seed, num23 + 1);
				float num26 = X + (float)Math.Cos(num24) * num25;
				float num27 = Y + (float)Math.Sin(num24) * num25;
				float num28 = 0.8f + 0.8f * PowerFx.Rand(seed, num23 + 2);
				float num29 = 2.4f + 2.8f * PowerFx.Rand(seed, num23 + 3);
				_billows.Add(new Vec3(num26, num27, func(num26, num27) + num28 * 0.25f), new Vec3((float)Math.Cos(num24) * num29, (float)Math.Sin(num24) * num29, 0.2f + 0.8f * PowerFx.Rand(seed, num23 + 4)), num28, num28 * (1.7f + 0.7f * PowerFx.Rand(seed, num23 + 5)), 1.3f + 1.1f * PowerFx.Rand(seed, num23 + 6), 1f, num23, 0.5f);
			}
			_plumeClock += dt * (flag ? 3f : 2f) * num * (0.3f + 0.7f * num2);
			if (_plumeClock > 1f)
			{
				_plumeClock -= 1f;
				int num30 = _billows.List.Count * 13 + (int)(time * 211f);
				float num31 = PowerFx.Rand(seed, num30) * 6.2832f;
				float num32 = 3.2f + 1.6f * PowerFx.Rand(seed, num30 + 1);
				float num33 = X + (float)Math.Cos(num31) * num32;
				float num34 = Y + (float)Math.Sin(num31) * num32;
				float num35 = 1.5f + 0.9f * PowerFx.Rand(seed, num30 + 2);
				_billows.Add(new Vec3(num33, num34, func(num33, num34) + 1.5f), new Vec3((float)Math.Cos(num31) * 0.8f, (float)Math.Sin(num31) * 0.8f, 4.5f + 2.5f * PowerFx.Rand(seed, num30 + 3)), num35, num35 * 2.1f, 3.4f + 1.2f * PowerFx.Rand(seed, num30 + 4), 0.8f, num30, 1.4f);
			}
			if (parts == null)
			{
				return;
			}
			_lavaClock += dt * 14f * num;
			while (_lavaClock > 1f)
			{
				_lavaClock -= 1f;
				int num36 = parts.List.Count + (int)(time * 977f);
				float num37 = PowerFx.Rand(seed, num36) * 6.2832f;
				float num38 = 2f + 4f * PowerFx.Rand(seed, num36 + 1);
				parts.Add(new FxParticles.P
				{
					Pos = new Vec3(X + (float)Math.Cos(num37) * 0.8f, Y + (float)Math.Sin(num37) * 0.8f, ground + 0.3f),
					Vel = new Vec3((float)Math.Cos(num37) * num38, (float)Math.Sin(num37) * num38, 3f + 5f * PowerFx.Rand(seed, num36 + 2)),
					Life = 1.6f,
					Size = 0.22f + 0.14f * PowerFx.Rand(seed, num36 + 3),
					EndSize = 0.14f,
					Gravity = 12f,
					Drag = 0.3f,
					Color = BlastFx.Heat(1.05f),
					EndColor = BlastFx.Heat(0.45f),
					Kind = FxParticles.Kind.Drop
				});
			}
			_ashClock += dt * 26f * num;
			while (_ashClock > 1f)
			{
				_ashClock -= 1f;
				int num39 = parts.List.Count + (int)(time * 613f);
				float num40 = PowerFx.Rand(seed, num39) * 6.2832f;
				float num41 = 0.6f + 2.4f * PowerFx.Rand(seed, num39 + 1);
				bool num42 = PowerFx.Rand(seed, num39 + 2) < 0.45f;
				Vec3 pos = new Vec3(X + (float)Math.Cos(num40) * num41, Y + (float)Math.Sin(num40) * num41, ground + 0.5f + 2f * PowerFx.Rand(seed, num39 + 3));
				Vec3 vel = new Vec3((0f - (float)Math.Sin(num40)) * 3f - (float)Math.Cos(num40) * num41 * 0.6f, (float)Math.Cos(num40) * 3f - (float)Math.Sin(num40) * num41 * 0.6f, 6f + 8f * PowerFx.Rand(seed, num39 + 4));
				if (num42)
				{
					FxParticles.P p = new FxParticles.P
					{
						Pos = pos,
						Vel = vel,
						Life = 1.4f,
						Size = 0.5f,
						EndSize = 0.3f,
						Gravity = -5f,
						Drag = 0.7f,
						Color = BlastFx.Heat(1.1f),
						EndColor = BlastFx.Heat(0.6f),
						Kind = FxParticles.Kind.Ember
					};
					parts.Add(in p);
				}
				else
				{
					FxParticles.P p = new FxParticles.P
					{
						Pos = pos,
						Vel = vel,
						Life = 1.6f,
						Size = 0.2f + 0.15f * PowerFx.Rand(seed, num39 + 5),
						EndSize = 0.1f,
						Gravity = -6f,
						Drag = 0.6f,
						Color = TerrainCache.Rgb(30, 24, 26),
						EndColor = TerrainCache.Rgb(60, 40, 34),
						Kind = FxParticles.Kind.Drop,
						Matte = true
					};
					parts.Add(in p);
				}
			}
			_lineClock += dt * 9f * num;
			while (_lineClock > 1f)
			{
				_lineClock -= 1f;
				int num43 = _lines.Count * 5 + (int)(time * 337f);
				float num44 = PowerFx.Rand(seed, num43) * 6.2832f;
				float z = 0.25f + 0.9f * PowerFx.Rand(seed, num43 + 1);
				float num45 = 14f + 12f * PowerFx.Rand(seed, num43 + 2);
				Vec3 vec = new Vec3((float)Math.Cos(num44), (float)Math.Sin(num44), z).Normalized();
				_lines.Add(new Vec3(X, Y, ground + 0.8f) + vec * 1.5f, vec * num45, 2.4f + 2f * PowerFx.Rand(seed, num43 + 3), 0.22f, 0.45f + 0.25f * PowerFx.Rand(seed, num43 + 4), (PowerFx.Rand(seed, num43 + 5) < 0.5f) ? PowerFx.RayCream : TerrainCache.Rgb(255, 255, 255));
			}
		}

		private void DrawWind(FxSink sink, float time)
		{
			if (_base < 0.02f)
			{
				return;
			}
			float num = ((sink.Ground != null) ? sink.Ground(X, Y) : 0f);
			float num2 = _base;
			for (int i = 0; i < 2; i++)
			{
				float num3 = ((i == 0) ? 5.2f : 3.6f);
				float num4 = ((i == 0) ? 2.6f : 6.2f);
				float num5 = ((i == 0) ? 2.6f : (-3.4f)) * (0.8f + 0.2f * BlastFx.Clamp01(Held / 2f));
				int num6 = ((i == 0) ? 14 : 10);
				for (int j = 0; j < num6; j++)
				{
					float num7 = (float)j / (float)num6 * 6.2832f + time * num5 + (float)i;
					float num8 = (float)Math.Sin(num7 + time * 0.7f) * 0.6f;
					float x = X + (float)Math.Cos(num7) * num3;
					float y = Y + (float)Math.Sin(num7) * num3;
					float num9 = (float)Math.Sin(num7 - sink.Yaw);
					float num10 = 5f * (0.65f + 0.35f * Math.Abs((float)Math.Cos(num7 - sink.Yaw)));
					float num11 = (0.75f + 0.25f * (float)Math.Sin(time * 3f + (float)j)) * num2 * ((i == 0) ? 1f : 0.8f);
					sink.Sprite(PowerFx.Wisp(j), x, y, num + num4 + num8, num10, num10 * 0.34f, 0u, (byte)Math.Max(2, (int)(num11 * 250f)), emissive: true, additive: false, num9 * num5 < 0f);
				}
			}
		}
	}
}
