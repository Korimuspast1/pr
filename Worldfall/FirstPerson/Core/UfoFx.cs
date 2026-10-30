using System;

namespace FirstPerson.Core
{
	public sealed class UfoFx
	{
		public const float ModelSize = 7f;

		private const float RimR = 3.29f;

		private const float RimZ = 2.1000001f;

		private const float BellyZ = 1.4f;

		private const float BellyR = 1.19f;

		private const float DomeZ = 3.5f;

		private const float CentreZ = 2.1000001f;

		private const float LampR = 3.7450001f;

		private const float ModelWidth = 7.2799997f;

		private const int Bulbs = 8;

		public const int BeamFrames = 15;

		public const int StrikeFrame = 4;

		public readonly uint Seed;

		public Vec3 At;

		public float Diameter = 4.8f;

		public uint Land = TerrainCache.Rgb(110, 150, 70);

		public float Spin;

		public static readonly uint Fill = (UfoArt.Hull3 & 0xFFFFFF) | 0x64000000;

		private float _leanX;

		private float _leanY;

		private float _leanVX;

		private float _leanVY;

		private float _speed;

		private float _health = 1f;

		private Vec3 _vel;

		public float Age;

		private int _beamFrame = -1;

		private int _lastBeamFrame = -1;

		private float _frameClock;

		private float _beamFade;

		private bool _struck;

		private bool _broken;

		private float _strikeAge = 99f;

		private float _strikeX;

		private float _strikeY;

		private float _strikeGround;

		private float _strikeReach = 8f;

		private float _moteClock;

		private float _smokeClock;

		private float _fireClock;

		private float _hitAge = 99f;

		private float _hitAngle;

		public bool Dead;

		public float DeadAge;

		private float _tumbleX;

		private float _tumbleY;

		private float _deadSpin;

		public bool Arriving;

		private const int TrailSamples = 14;

		private readonly Vec3[,] _trail = new Vec3[4, 14];

		private readonly float[] _trailAge = new float[14];

		private int _trailHead;

		private int _trailCount;

		private float _trailClock;

		public int Strikes;

		public float Scale => Diameter / 7.2799997f;

		public Vec3 Centre => new Vec3(At.X, At.Y, At.Z + 2.1000001f * Scale);

		public bool BeamOn
		{
			get
			{
				if (_beamFrame < 0)
				{
					return _beamFade > 0.01f;
				}
				return true;
			}
		}

		public bool Struck => _strikeAge < 1f;

		public float BeamTime
		{
			get
			{
				if (_beamFrame < 0)
				{
					return -1f;
				}
				return (float)_beamFrame * 0.1f + Math.Min(0.1f, _frameClock);
			}
		}

		private float Bob
		{
			get
			{
				if (!Dead)
				{
					return 0.14f * (float)Math.Sin(Age * 4.7f + (float)(Seed & 0xF)) + 0.05f * (float)Math.Sin(Age * 2.3f + 1f);
				}
				return 0f;
			}
		}

		public float WarpFade
		{
			get
			{
				if (!Arriving || !(Age < 0.8f))
				{
					return 1f;
				}
				return BlastFx.Smooth(Age / 0.7f);
			}
		}

		public UfoFx(uint seed)
		{
			Seed = seed;
			Spin = BlastFx.Rand(seed, 3) * 6.283f;
		}

		public void Update(float dt, Vec3 at, Vec3 vel, float health, bool hitNow, int beamFrame)
		{
			At = at;
			_vel = vel;
			_speed = (float)Math.Sqrt(vel.X * vel.X + vel.Y * vel.Y);
			_health = health;
			Age += dt;
			if (hitNow && !Dead)
			{
				_hitAge = 0f;
				_hitAngle = BlastFx.Rand(Seed, (int)(Age * 97f)) * 6.283f;
			}
			_hitAge += dt;
			_strikeAge += dt;
			if (beamFrame != _lastBeamFrame)
			{
				if (beamFrame >= 0 && (_lastBeamFrame < 0 || beamFrame < _lastBeamFrame))
				{
					_struck = false;
					_broken = false;
				}
				_frameClock = 0f;
				_lastBeamFrame = beamFrame;
			}
			else
			{
				_frameClock += dt;
			}
			_beamFrame = (Dead ? (-1) : beamFrame);
			_beamFade = ((_beamFrame >= 0) ? 1f : Math.Max(0f, _beamFade - dt * 6f));
			if (Dead)
			{
				DeadAge += dt;
				_deadSpin = Math.Min(7f, _deadSpin + dt * 5f);
				Spin += dt * _deadSpin;
				float num = 0.75f + 0.12f * (float)Math.Sin(DeadAge * 9f);
				Spring(dt, _tumbleX * num, _tumbleY * num, 30f, 6f);
			}
			else
			{
				Spin += dt * (1.1f + Math.Min(2.5f, _speed * 0.12f));
				float num2 = Math.Min(1f, _speed / 14f) * 0.26f;
				float num3 = ((_speed > 0.05f) ? (vel.X / _speed * num2) : 0f);
				float num4 = ((_speed > 0.05f) ? (vel.Y / _speed * num2) : 0f);
				num3 += 0.035f * (float)Math.Sin(Age * 1.3f + (float)(Seed % 7));
				num4 += 0.035f * (float)Math.Cos(Age * 1.07f + (float)(Seed % 5));
				Spring(dt, num3, num4, 18f, 4.5f);
			}
		}

		private void Spring(float dt, float tx, float ty, float k, float damp)
		{
			if (!(dt <= 0f))
			{
				float num = Math.Min(dt, 0.05f);
				for (float num2 = dt; num2 > 1E-05f; num2 -= num)
				{
					float num3 = Math.Min(num, num2);
					_leanVX += ((tx - _leanX) * k - _leanVX * damp) * num3;
					_leanVY += ((ty - _leanY) * k - _leanVY * damp) * num3;
					_leanX += _leanVX * num3;
					_leanY += _leanVY * num3;
				}
			}
		}

		public void Die()
		{
			if (!Dead)
			{
				Dead = true;
				DeadAge = 0f;
				_deadSpin = 3f;
				float num = BlastFx.Rand(Seed, 77) * 6.283f;
				if (_speed > 1f)
				{
					num = (float)Math.Atan2(_vel.Y, _vel.X) + (BlastFx.Rand(Seed, 78) - 0.5f);
				}
				_tumbleX = (float)Math.Cos(num);
				_tumbleY = (float)Math.Sin(num);
				_hitAge = 0f;
			}
		}

		public Mat3 Turn()
		{
			Mat3 b = Mat3.RotZ(Spin);
			float num = (float)Math.Sqrt(_leanX * _leanX + _leanY * _leanY);
			if (num < 0.0001f)
			{
				return b;
			}
			return Mat3.Rotation(new Vec3((0f - _leanY) / num, _leanX / num, 0f), num) * b;
		}

		public void Pose(MeshInstance inst, float extraScale = 1f)
		{
			float num = Scale * extraScale;
			if (Arriving && Age < 0.9f)
			{
				num *= 0.55f + 0.45f * BlastFx.Smooth(Age / 0.7f) + 0.06f * (float)Math.Sin((double)Math.Min(1f, Age / 0.9f) * Math.PI);
			}
			Mat3 a = Turn();
			Vec3 vec = new Vec3(0f, 0f, 2.1000001f * num);
			Vec3 vec2 = new Vec3(At.X, At.Y, At.Z + Bob) + vec;
			inst.Basis = a * num;
			inst.Position = vec2 - a.Mul(vec);
		}

		public void Colour(MeshInstance inst, MeshModel model, float time, float day = 1f)
		{
			if (model == null)
			{
				return;
			}
			if (inst.Palette == null || inst.Palette.Length != model.SlotNames.Length)
			{
				inst.Palette = new uint[model.SlotNames.Length];
			}
			float num = 0.5f + 0.5f * (float)Math.Sin(time * 2.6f + (float)(Seed & 7));
			float t = LightsOn(time);
			inst.Flash = ((!Dead) ? ((Fill & 0xFFFFFF) | ((uint)(50f + 50f * BlastFx.Clamp01(day)) << 24)) : 0u);
			for (int i = 0; i < model.SlotNames.Length; i++)
			{
				string text = model.SlotNames[i];
				uint num2 = 0u;
				switch (text)
				{
				case "metal":
					num2 = (Dead ? TerrainCache.Lerp(UfoArt.Hull2, UfoArt.Hull0, BlastFx.Clamp01(DeadAge * 1.5f)) : UfoArt.Hull2);
					break;
				case "glow":
					num2 = TerrainCache.Lerp(UfoArt.Dome0, TerrainCache.Lerp(UfoArt.Dome2, UfoArt.Dome3, num * 0.45f), t);
					break;
				case "accent":
					num2 = TerrainCache.Lerp(UfoArt.Hull1, UfoArt.Hull4, t);
					break;
				case "dark":
					num2 = (Dead ? TerrainCache.Scale(UfoArt.Hull0, 0.7f) : UfoArt.Hull0);
					break;
				}
				inst.Palette[i] = num2;
			}
		}

		private float LightsOn(float time)
		{
			if (Dead)
			{
				return Math.Max(0f, 1f - DeadAge / 0.9f) * ((BlastFx.Rand(Seed, (int)(time * 23f)) < 0.55f) ? 1f : 0.15f);
			}
			if (_hitAge < 0.45f)
			{
				if (!(BlastFx.Rand(Seed, (int)(time * 30f) + 7) < 0.5f))
				{
					return 1f;
				}
				return 0.2f;
			}
			if (_health < 0.3f && BlastFx.Rand(Seed, (int)(time * 12f) + 3) < 0.15f)
			{
				return 0.35f;
			}
			return 1f;
		}

		private Vec3 Local(Mat3 basis, Vec3 pos, float x, float y, float z)
		{
			return pos + basis.Mul(new Vec3(x, y, z));
		}

		public void Draw(FxSink sink, FxParticles parts, MeshInstance inst, float dt, float time)
		{
			Mat3 basis = inst.Basis;
			Vec3 position = inst.Position;
			float scale = Scale;
			float num = sink.Distance(At.X, At.Y);
			float num2 = LightsOn(time);
			float num3 = ((sink.Ground != null) ? sink.Ground(At.X, At.Y) : (At.Z - 8f));
			float num4 = (0f - time) * 7.5f;
			int num5 = ((!(num > 60f)) ? 1 : 2);
			for (int i = 0; i < 8; i += num5)
			{
				float num6 = (float)i / 8f * 6.283f;
				Vec3 vec = Local(basis, position, (float)Math.Cos(num6) * 3.7450001f, (float)Math.Sin(num6) * 3.7450001f, 2.1000001f);
				float num7 = (float)Math.Pow(Math.Max(0.0, Math.Cos(num4 - num6)), 5.0);
				float num8 = (0.3f + 0.7f * num7) * num2;
				uint num9 = (((i & 1) == 0) ? UfoArt.Dome3 : UfoArt.Hull4);
				sink.Glow(vec.X, vec.Y, vec.Z, Diameter * (0.16f + 0.14f * num7), num9, 0.75f * num8);
				if (num2 > 0.5f && num < 60f)
				{
					sink.Sprite(BlastFx.Spark((num7 > 0.5f) ? 3 : 2), vec.X, vec.Y, vec.Z, 0.13f * Diameter / 4.8f, 0.13f * Diameter / 4.8f, num9, (byte)(120f + 135f * num7), emissive: true, additive: true);
				}
			}
			Vec3 vec2 = Local(basis, position, 0f, 0f, 3.5f);
			float num10 = 0.5f + 0.5f * (float)Math.Sin(time * 2.6f + (float)(Seed & 7));
			sink.Glow(vec2.X, vec2.Y, vec2.Z, Diameter * 0.55f, UfoArt.Dome2, (0.16f + 0.08f * num10) * num2);
			Vec3 belly = Local(basis, position, 0f, 0f, 1.288f);
			if (!Dead)
			{
				sink.Glow(belly.X, belly.Y, belly.Z - 0.1f * scale, Diameter * 0.42f, UfoArt.BeamLime, (0.22f + 0.1f * num10) * num2);
				sink.Light(belly.X, belly.Y, belly.Z - 2.5f, 3f + Diameter * 0.9f, TerrainCache.Lerp(UfoArt.BeamLime, UfoArt.Hull4, 0.35f), 0.3f * num2);
				float num11 = belly.Z - num3;
				if (num11 > 2f)
				{
					sink.Light(belly.X, belly.Y, num3 + 0.8f, Math.Min(3f + Diameter * 0.6f, num11 - 1.5f), UfoArt.BeamLime, 0.3f * num2);
				}
				int num12 = ((num < 30f) ? 10 : 6);
				for (int j = 0; j < num12; j++)
				{
					float num13 = (float)j / (float)num12 * 6.283f - time * 1.7f;
					Vec3 vec3 = Local(basis, position, (float)Math.Cos(num13) * 1.19f, (float)Math.Sin(num13) * 1.19f, 1.34f);
					sink.Glow(vec3.X, vec3.Y, vec3.Z, Diameter * 0.11f, ((j & 1) == 0) ? UfoArt.BeamLime : UfoArt.Dome3, 0.5f * num2);
				}
			}
			DrawTrail(sink, basis, position, dt);
			if (_beamFrame >= 0 || _beamFade > 0.01f)
			{
				DrawBeam(sink, parts, belly, num3, num, dt, time);
			}
			if (_strikeAge < 1.2f)
			{
				DrawStrike(sink, time);
			}
			DrawHurt(sink, parts, basis, position, dt, time);
			if (Arriving && Age < 1.4f)
			{
				DrawWarp(sink, parts, dt, time);
			}
		}

		private void DrawTrail(FxSink sink, Mat3 b, Vec3 p, float dt)
		{
			if (sink.Streaks == null)
			{
				return;
			}
			for (int i = 0; i < 14; i++)
			{
				_trailAge[i] += dt;
			}
			_trailClock += dt;
			if (_speed > 2.5f && !Dead && _trailClock > 0.025f)
			{
				_trailClock = 0f;
				_trailHead = (_trailHead + 1) % 14;
				_trailCount = Math.Min(14, _trailCount + 1);
				_trailAge[_trailHead] = 0f;
				for (int j = 0; j < 4; j++)
				{
					float num = (float)j / 4f * 6.283f;
					_trail[j, _trailHead] = Local(b, p, (float)Math.Cos(num) * 3.7450001f, (float)Math.Sin(num) * 3.7450001f, 2.1000001f);
				}
			}
			if (_trailCount < 2)
			{
				return;
			}
			float num2 = Math.Min(1f, (_speed - 2f) / 4f);
			for (int k = 0; k < 4; k++)
			{
				uint color = (((k & 1) == 0) ? UfoArt.BeamLime : UfoArt.BeamCream);
				int num3 = _trailHead;
				for (int l = 1; l < _trailCount; l++)
				{
					int num4 = (num3 - 1 + 14) % 14;
					float val = 1f - _trailAge[num3] / 0.32f;
					float num5 = 1f - _trailAge[num4] / 0.32f;
					if (num5 <= 0f)
					{
						break;
					}
					sink.Line(_trail[k, num3], _trail[k, num4], color, Math.Max(0f, val) * num2, Math.Max(0f, num5) * num2, 0.11f);
					num3 = num4;
				}
			}
		}

		private void DrawBeam(FxSink sink, FxParticles parts, Vec3 belly, float ground, float dist, float dt, float time)
		{
			float num = ((_beamFrame >= 0) ? ((float)_beamFrame * 0.1f + Math.Min(0.1f, _frameClock)) : 1.5f);
			float num2 = ((_beamFrame >= 0) ? 1f : _beamFade);
			float z = belly.Z;
			float num3 = Math.Max(0.5f, z - ground);
			float num4 = Math.Max(1.4f, Diameter * 0.36f);
			float x = belly.X;
			float y = belly.Y;
			if (num < 0.3f)
			{
				float num5 = ((num < 0.2f) ? BlastFx.Smooth(num / 0.2f) : (1f - BlastFx.Smooth((num - 0.2f) / 0.1f) * 0.6f));
				float z2 = z - 0.35f - 0.5f * num5;
				sink.Glow(x, y, z2, Diameter * (0.6f + 0.9f * num5), UfoArt.BeamYellow, 0.95f * num5 * num2);
				sink.Glow(x, y, z2, Diameter * (0.25f + 0.35f * num5), UfoArt.BeamCream, num5 * num2);
				if (num > 0.08f)
				{
					sink.Sprite(BlastFx.StarSprite, x, y, z2, Diameter * 1.1f * num5, Diameter * 1.1f * num5, UfoArt.BeamYellow, (byte)Math.Max(2, (int)(230f * num5 * num2)), emissive: true, additive: true);
				}
				int num6 = (int)(time * 20f);
				for (int i = 0; i < 2; i++)
				{
					float num7 = BlastFx.Rand(Seed, num6 * 7 + i) * 6.283f;
					float num8 = num4 * (0.4f + 0.8f * BlastFx.Rand(Seed, num6 * 7 + i + 3));
					float num9 = BlastFx.Clamp01(num / 0.18f);
					UfoArt.Lightning(b: new Vec3(x + (float)Math.Cos(num7) * num8 * num9, y + (float)Math.Sin(num7) * num8 * num9, z - num3 * num9), sink: sink, a: new Vec3(x, y, z - 0.2f), kinks: 9, jitter: 0.55f, seed: Seed + (uint)(i * 17), tick: num6, alpha: 0.9f * num2);
				}
				sink.Light(x, y, z - 2.5f, 6f, UfoArt.BeamYellow, 0.7f * num5 * num2);
			}
			float num10 = BlastFx.Clamp01((num - 0.2f) / 0.07f);
			float num11 = BlastFx.Clamp01((num - 1.08f) / 0.22f);
			if (num10 > 0f && num11 < 1f)
			{
				float num12 = z - num11 * num3 * 0.88f;
				float num13 = z - num3 * num10;
				float num14 = 1f + 0.06f * (float)Math.Sin(time * 41f + (float)Seed) + (((int)(time * 10f) % 3 == 0) ? (-0.05f) : 0f);
				float num15 = num4 * 2f * num14 * (1f - 0.55f * num11) * num2;
				float num16 = num12 - num13;
				if (num16 > 0.05f)
				{
					int frame = (int)(time * 24f);
					sink.Billboards.Add(Band(UfoArt.ColumnHalo, x, y, num13 - 0.15f, num16 + 0.3f, num15 * 2.3f, (byte)(110f * num2), additive: true));
					sink.Billboards.Add(Band(UfoArt.Column(frame), x, y, num13, num16, num15, (byte)Math.Max(2, (int)(235f * num2)), additive: false));
					int num17 = Math.Max(2, (int)(num16 / 1.6f));
					for (int j = 0; j <= num17; j++)
					{
						float num18 = (float)j / (float)num17;
						float num19 = num13 + num16 * num18;
						float num20 = UfoArt.ColumnHalf(1f - (num19 - ground) / num3) * num4;
						sink.Glow(x, y, num19, num20 * 4.2f, UfoArt.BeamLime, 0.13f * num2);
					}
				}
				if (num10 >= 1f && num11 < 0.6f && num > 0.3f)
				{
					int num21 = (int)(time * 15f);
					int num22 = ((!(dist < 60f)) ? 1 : 3);
					for (int k = 0; k < num22; k++)
					{
						if (!(BlastFx.Rand(Seed, num21 * 13 + k) < 0.25f))
						{
							DrawWrapBolt(sink, x, y, num12, ground, num3, num4, num21, k, num2 * (1f - num11));
						}
					}
				}
				if (num10 >= 1f)
				{
					float num23 = 0.85f + 0.15f * (float)Math.Sin(time * 23f);
					float num24 = num2 * (1f - num11 * 0.7f);
					sink.Glow(x, y, ground + 0.25f, num4 * 3.2f * num23, UfoArt.BeamYellow, 0.55f * num24);
					sink.Glow(x, y, ground + 0.2f, num4 * 6.5f, UfoArt.BeamGreen, 0.28f * num24);
					sink.Light(x, y, ground + 1.2f, Math.Min(9f + num4 * 2f, num3 - 1.5f), UfoArt.BeamLime, 1.4f * num24 * num23);
					sink.Light(x, y, ground + num3 * 0.35f, num3 * 0.5f, UfoArt.BeamYellow, 0.4f * num24);
				}
				if (parts != null && num10 >= 1f && num11 < 0.5f && dt > 0f)
				{
					_moteClock += dt * 34f;
					while (_moteClock > 1f)
					{
						_moteClock -= 1f;
						int num25 = parts.List.Count + (int)(time * 811f);
						float num26 = BlastFx.Rand(Seed, num25) * 6.283f;
						float num27 = num4 * 0.9f * (float)Math.Sqrt(BlastFx.Rand(Seed, num25 + 1));
						parts.Add(new FxParticles.P
						{
							Pos = new Vec3(x + (float)Math.Cos(num26) * num27, y + (float)Math.Sin(num26) * num27, ground + 0.2f + BlastFx.Rand(Seed, num25 + 2) * num3 * 0.3f),
							Vel = new Vec3((0f - (float)Math.Sin(num26)) * 1.2f, (float)Math.Cos(num26) * 1.2f, 4f + 4f * BlastFx.Rand(Seed, num25 + 3)),
							Life = 0.7f + 0.5f * BlastFx.Rand(Seed, num25 + 4),
							Size = 0.3f + 0.25f * BlastFx.Rand(Seed, num25 + 5),
							EndSize = 0.12f,
							Color = ((BlastFx.Rand(Seed, num25 + 6) < 0.3f) ? UfoArt.BeamCream : UfoArt.BeamLime),
							EndColor = UfoArt.BeamGreen,
							Gravity = -1f,
							Drag = 0.4f,
							Kind = FxParticles.Kind.Glow,
							Alpha = 200
						});
					}
				}
			}
			if (!_struck && num >= 0.4f && _beamFrame >= 0)
			{
				_struck = true;
				Strike(sink, parts, x, y, ground, dist);
			}
			if (_broken || !(num >= 1.3f))
			{
				return;
			}
			_broken = true;
			if (parts != null)
			{
				for (int l = 0; l < 34; l++)
				{
					float num28 = BlastFx.Rand(Seed, l + 900) * 6.283f;
					float z3 = ground + 0.3f + BlastFx.Rand(Seed, l + 901) * num3 * 0.4f;
					float num29 = 0.6f + 1.8f * BlastFx.Rand(Seed, l + 902);
					bool flag = BlastFx.Rand(Seed, l + 903) < 0.3f;
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(x + (float)Math.Cos(num28) * num4 * 0.6f, y + (float)Math.Sin(num28) * num4 * 0.6f, z3),
						Vel = new Vec3((float)Math.Cos(num28) * num29, (float)Math.Sin(num28) * num29, 0.6f + 1.4f * BlastFx.Rand(Seed, l + 904)),
						Life = 0.6f + 0.7f * BlastFx.Rand(Seed, l + 905),
						Size = 0.16f,
						EndSize = 0.1f,
						Gravity = 0.5f,
						Drag = 1.2f,
						Color = (flag ? UfoArt.Bolt : UfoArt.BeamLime),
						EndColor = (flag ? UfoArt.Bolt : UfoArt.BeamGreen),
						Kind = FxParticles.Kind.Spark
					});
				}
			}
		}

		private void DrawWrapBolt(FxSink sink, float x, float y, float top, float ground, float len, float foot, int tick, int i, float alpha)
		{
			uint seed = Seed + (uint)(i * 7919);
			float num = BlastFx.Rand(seed, tick) * 6.283f;
			float num2 = (0.5f + 0.8f * BlastFx.Rand(seed, tick + 1)) * ((BlastFx.Rand(seed, tick + 2) < 0.5f) ? (-1f) : 1f);
			float num3 = BlastFx.Rand(seed, tick + 3) * 0.3f;
			float num4 = 0.65f + 0.35f * BlastFx.Rand(seed, tick + 4);
			Vec3 a = default(Vec3);
			for (int j = 0; j <= 12; j++)
			{
				float num5 = num3 + (num4 - num3) * (float)j / 12f;
				float num6 = top - (top - ground) * num5;
				float num7 = UfoArt.ColumnHalf(num5) * foot * 1.25f + (BlastFx.Rand(seed, tick * 31 + j) - 0.5f) * 0.5f;
				float num8 = num + num2 * 6.283f * num5;
				Vec3 vec = new Vec3(x + (float)Math.Cos(num8) * num7, y + (float)Math.Sin(num8) * num7, num6 + (BlastFx.Rand(seed, tick * 31 + j + 17) - 0.5f) * 0.35f);
				if (j > 0)
				{
					sink.Line(a, vec, UfoArt.Bolt, alpha, alpha, 0.06f);
				}
				if (j == 6)
				{
					sink.Glow(vec.X, vec.Y, vec.Z, 0.9f, UfoArt.Bolt, 0.3f * alpha);
				}
				a = vec;
			}
		}

		private static Billboard Band(SpriteImage img, float x, float y, float z, float h, float w, byte alpha, bool additive)
		{
			return new Billboard
			{
				X = x,
				Y = y,
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

		private void Strike(FxSink sink, FxParticles parts, float x, float y, float ground, float dist)
		{
			Strikes++;
			_strikeAge = 0f;
			_strikeX = x;
			_strikeY = y;
			_strikeGround = ground;
			_strikeReach = Math.Max(4f, Math.Min(16f, At.Z + 1.4f * Scale - ground - 1f));
			float num = BlastFx.Clamp01(1.2f - dist / 70f);
			sink.AddFlash(0.3f * num * (0.45f + 0.55f * Math.Max(0f, sink.Facing(x, y))), UfoArt.BeamCream);
			sink.Shake = Math.Max(sink.Shake, 0.55f * BlastFx.Clamp01(1f - dist / 40f));
			if (parts != null)
			{
				for (int i = 0; i < 34; i++)
				{
					float num2 = BlastFx.Rand(Seed, i + 300) * 6.283f;
					float num3 = 3f + 7f * BlastFx.Rand(Seed, i + 301);
					float num4 = BlastFx.Rand(Seed, i + 304);
					uint color = ((num4 < 0.2f) ? UfoArt.Bolt : ((num4 < 0.6f) ? UfoArt.BeamYellow : UfoArt.BeamLime));
					parts.Spark(new Vec3(x, y, ground + 0.3f), new Vec3((float)Math.Cos(num2) * num3, (float)Math.Sin(num2) * num3, 3f + 6f * BlastFx.Rand(Seed, i + 302)), 0.6f + 0.6f * BlastFx.Rand(Seed, i + 303), 0.14f, color, UfoArt.BeamGreen, 9f, 0.6f);
				}
				uint num5 = TerrainCache.Lerp(TerrainCache.Lerp(TerrainCache.Rgb(172, 158, 128), Land, 0.3f), UfoArt.BeamCream, 0.3f);
				for (int j = 0; j < 14; j++)
				{
					float num6 = ((float)j + BlastFx.Rand(Seed, j + 400)) / 14f * 6.283f;
					float num7 = 3f + 3f * BlastFx.Rand(Seed, j + 401);
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(x + (float)Math.Cos(num6) * 1.2f, y + (float)Math.Sin(num6) * 1.2f, ground + 0.4f),
						Vel = new Vec3((float)Math.Cos(num6) * num7, (float)Math.Sin(num6) * num7, 0.8f + BlastFx.Rand(Seed, j + 402)),
						Life = 0.9f + 0.6f * BlastFx.Rand(Seed, j + 403),
						Size = 1f,
						EndSize = 2.6f,
						Color = num5,
						EndColor = TerrainCache.Scale(num5, 0.9f),
						Alpha = (byte)(40f + 75f * BlastFx.Clamp01(sink.Day)),
						Variant = j,
						Drag = 1.8f,
						Gravity = -0.3f,
						Kind = FxParticles.Kind.Smoke
					});
				}
			}
		}

		private void DrawStrike(FxSink sink, float time)
		{
			float strikeAge = _strikeAge;
			float strikeX = _strikeX;
			float strikeY = _strikeY;
			float strikeGround = _strikeGround;
			if (strikeAge < 0.3f)
			{
				float num = 1f - strikeAge / 0.3f;
				float num2 = 5f + 9f * strikeAge / 0.3f;
				sink.Sprite(BlastFx.StarSprite, strikeX, strikeY, strikeGround + 0.8f, num2, num2, UfoArt.BeamCream, (byte)Math.Max(2, (int)(255f * num)), emissive: true, additive: true);
				sink.Glow(strikeX, strikeY, strikeGround + 0.6f, 9f, UfoArt.BeamYellow, 0.8f * num);
				sink.Light(strikeX, strikeY, strikeGround + 1f, _strikeReach, UfoArt.BeamCream, 2.2f * num, 2f * num);
			}
			if (strikeAge < 0.6f)
			{
				float num3 = strikeAge / 0.6f;
				float num4 = 1.5f + 6.5f * BlastFx.Smooth(Math.Min(1f, num3 * 1.2f));
				float num5 = (1f - num3) * (1f - num3);
				for (int i = 0; i < 28; i++)
				{
					if (i % 6 != 2)
					{
						float num6 = ((float)i + 0.5f) / 28f * 6.2832f;
						float num7 = strikeX + (float)Math.Cos(num6) * num4;
						float num8 = strikeY + (float)Math.Sin(num6) * num4;
						float num9 = ((sink.Ground != null) ? sink.Ground(num7, num8) : strikeGround);
						sink.Sprite(PowerFx.Dash, num7, num8, num9 + 0.22f, 1.1f + num4 * 0.1f, 0.42f, TerrainCache.Lerp(UfoArt.BeamCream, UfoArt.BeamGreen, num3), (byte)Math.Max(2, (int)(255f * num5)), emissive: true, additive: true);
					}
				}
			}
			if (strikeAge < 0.35f && sink.Streaks != null)
			{
				int num10 = (int)(time * 20f);
				for (int j = 0; j < 5; j++)
				{
					float num11 = ((float)j + BlastFx.Rand(Seed, num10 * 5 + j)) / 5f * 6.283f;
					float num12 = 2.5f + 3.5f * BlastFx.Rand(Seed, num10 * 5 + j + 40);
					float num13 = strikeX + (float)Math.Cos(num11) * num12;
					float num14 = strikeY + (float)Math.Sin(num11) * num12;
					float z = ((sink.Ground != null) ? sink.Ground(num13, num14) : strikeGround) + 0.15f;
					UfoArt.Lightning(sink, new Vec3(strikeX, strikeY, strikeGround + 0.3f), new Vec3(num13, num14, z), 7, 0.6f, Seed + 991, num10 * 5 + j, 0.95f * (1f - strikeAge / 0.35f));
				}
			}
		}

		private void DrawHurt(FxSink sink, FxParticles parts, Mat3 b, Vec3 p, float dt, float time)
		{
			if (_hitAge < 0.18f)
			{
				Vec3 vec = Local(b, p, (float)Math.Cos(_hitAngle) * 3.29f * 0.95f, (float)Math.Sin(_hitAngle) * 3.29f * 0.95f, 2.1000001f);
				if (parts != null && _hitAge <= dt + 0.0001f)
				{
					for (int i = 0; i < 24; i++)
					{
						Vec3 vel = new Vec3((float)Math.Cos(_hitAngle), (float)Math.Sin(_hitAngle), 0.4f) * (3f + 5f * BlastFx.Rand(Seed, i + 500)) + new Vec3(BlastFx.Rand(Seed, i + 501) - 0.5f, BlastFx.Rand(Seed, i + 502) - 0.5f, BlastFx.Rand(Seed, i + 503)) * 5f;
						parts.Spark(vec, vel, 0.4f + 0.5f * BlastFx.Rand(Seed, i + 504), 0.17f, (BlastFx.Rand(Seed, i + 505) < 0.5f) ? UfoArt.BeamCream : UfoArt.Hull4, BlastFx.Heat(0.6f), 9f, 0.5f);
					}
				}
				int num = (int)(time * 24f);
				float num2 = _hitAngle + 0.6f * (BlastFx.Rand(Seed, num) - 0.5f) * 2f;
				Vec3 b2 = Local(b, p, (float)Math.Cos(num2) * 3.29f, (float)Math.Sin(num2) * 3.29f, 2.5000002f);
				UfoArt.Lightning(sink, vec, b2, 5, 0.35f * Scale, Seed + 3, num, 1f - _hitAge / 0.18f);
				sink.Glow(vec.X, vec.Y, vec.Z, Diameter * 0.6f, UfoArt.BeamCream, 0.9f * (1f - _hitAge / 0.18f));
				if (_hitAge < 0.08f)
				{
					sink.Sprite(BlastFx.StarSprite, vec.X, vec.Y, vec.Z, Diameter * 0.5f, Diameter * 0.5f, UfoArt.Hull4, 230, emissive: true, additive: true);
				}
			}
			if (parts == null || dt <= 0f)
			{
				return;
			}
			float num3 = (Dead ? 1f : BlastFx.Clamp01((0.6f - _health) / 0.6f));
			if (num3 <= 0f)
			{
				return;
			}
			float num4 = 1.9f + (float)(Seed & 3);
			Vec3 vec2 = Local(b, p, (float)Math.Cos(num4) * 3.29f * 0.9f, (float)Math.Sin(num4) * 3.29f * 0.9f, 2.4f);
			_smokeClock += dt * (Dead ? 26f : (4f + 12f * num3));
			while (_smokeClock > 1f)
			{
				_smokeClock -= 1f;
				int num5 = parts.List.Count + (int)(time * 577f);
				Vec3 vec3 = new Vec3(BlastFx.Rand(Seed, num5) - 0.5f, BlastFx.Rand(Seed, num5 + 1) - 0.5f, BlastFx.Rand(Seed, num5 + 2) - 0.5f) * (0.6f * Scale);
				parts.Smoke(vec2 + vec3, new Vec3(0f, 0f, Dead ? 0.5f : 1.2f), Dead ? 2.6f : (1.8f + 1.2f * num3), 0.9f * Scale + 0.5f, 3f * Scale + 1.4f, TerrainCache.Rgb(48, 44, 46), BlastFx.Smoke(0.55f), (byte)(Dead ? 145f : (130f + 50f * num3)), num5 & 7, 0.5f);
			}
			if (!Dead && !(_health < 0.3f))
			{
				return;
			}
			_fireClock += dt * (Dead ? 30f : 8f);
			while (_fireClock > 1f)
			{
				_fireClock -= 1f;
				int num6 = parts.List.Count + (int)(time * 911f);
				float num7 = BlastFx.Rand(Seed, num6) * 6.283f;
				Vec3 vec4 = (Dead ? Local(b, p, (float)Math.Cos(num7) * 3.29f * 0.7f, (float)Math.Sin(num7) * 3.29f * 0.7f, 2.1000001f) : vec2);
				parts.Add(new FxParticles.P
				{
					Pos = vec4,
					Vel = new Vec3((BlastFx.Rand(Seed, num6 + 1) - 0.5f) * 1.5f, (BlastFx.Rand(Seed, num6 + 2) - 0.5f) * 1.5f, 1.5f),
					Life = 0.45f + 0.3f * BlastFx.Rand(Seed, num6 + 3),
					Size = 0.6f * Scale + 0.3f,
					EndSize = 1.4f * Scale + 0.6f,
					Color = BlastFx.Heat(1.05f),
					EndColor = BlastFx.Heat(0.25f),
					Variant = (num6 & 7),
					Drag = 1f,
					Gravity = -2f,
					Kind = FxParticles.Kind.Fire
				});
				if (BlastFx.Rand(Seed, num6 + 4) < 0.35f)
				{
					parts.Spark(vec4, new Vec3((BlastFx.Rand(Seed, num6 + 5) - 0.5f) * 6f, (BlastFx.Rand(Seed, num6 + 6) - 0.5f) * 6f, 2f + 3f * BlastFx.Rand(Seed, num6 + 7)), 0.6f, 0.1f, BlastFx.Heat(1.1f), BlastFx.Heat(0.5f), 9f, 0.6f);
				}
			}
			if (Dead)
			{
				int num8 = (int)(time * 18f);
				for (int j = 0; j < 2; j++)
				{
					float num9 = BlastFx.Rand(Seed, num8 * 3 + j) * 6.283f;
					float num10 = num9 + 0.9f;
					UfoArt.Lightning(sink, Local(b, p, (float)Math.Cos(num9) * 3.29f, (float)Math.Sin(num9) * 3.29f, 2.1000001f), Local(b, p, (float)Math.Cos(num10) * 3.29f * 0.6f, (float)Math.Sin(num10) * 3.29f * 0.6f, 3.1499999f), 6, 0.3f * Scale, Seed + 41, num8 * 3 + j, 0.85f);
				}
				Vec3 vec5 = Local(b, p, 0f, 0f, 2.1000001f);
				sink.Glow(vec5.X, vec5.Y, vec5.Z, Diameter * 1.1f, BlastFx.Heat(0.8f), 0.35f);
				sink.Light(vec5.X, vec5.Y, vec5.Z, 8f + Diameter, BlastFx.Heat(0.85f), 1.1f);
			}
		}

		public UfoCrashFx Crash(float ground, MeshInstance last)
		{
			UfoCrashFx ufoCrashFx = new UfoCrashFx(At.X, At.Y, ground, Diameter, Seed ^ 0xC0FFEE, Land);
			if (last != null && last.Model != null)
			{
				ufoCrashFx.Wreck(last.Model, Turn(), Scale);
			}
			return ufoCrashFx;
		}

		private void DrawWarp(FxSink sink, FxParticles parts, float dt, float time)
		{
			float age = Age;
			Vec3 centre = Centre;
			if (age < 0.4f)
			{
				float num = 1f - age / 0.4f;
				sink.Line(new Vec3(centre.X, centre.Y, centre.Z + 60f), centre, UfoArt.BeamCream, 0f, num, 0.12f);
				sink.Line(new Vec3(centre.X + 0.15f, centre.Y, centre.Z + 40f), centre, UfoArt.BeamLime, 0f, num * 0.8f, 0.3f);
				sink.Sprite(BlastFx.StarSprite, centre.X, centre.Y, centre.Z, Diameter * (1.5f + 2f * age), Diameter * (1.5f + 2f * age), UfoArt.BeamCream, (byte)Math.Max(2, (int)(240f * num)), emissive: true, additive: true);
				sink.AddFlash(0.12f * num * BlastFx.Clamp01(1.2f - sink.Distance(centre.X, centre.Y) / 60f), UfoArt.BeamCream);
			}
			if (age < 1.2f)
			{
				float num2 = age / 1.2f;
				float num3 = (1f - num2) * (1f - num2);
				float num4 = Diameter * (0.6f + 1.8f * BlastFx.Smooth(num2));
				for (int i = 0; i < 24; i++)
				{
					float num5 = (float)i / 24f * 6.283f + age * 2f;
					sink.Glow(centre.X + (float)Math.Cos(num5) * num4, centre.Y + (float)Math.Sin(num5) * num4, centre.Z, 0.7f, (i % 3 == 0) ? UfoArt.Bolt : UfoArt.BeamLime, 0.7f * num3);
				}
				sink.Glow(centre.X, centre.Y, centre.Z, Diameter * 2.2f * (1f - num2), UfoArt.BeamLime, 0.6f * num3);
			}
			if (parts != null && age <= dt + 0.0001f)
			{
				for (int j = 0; j < 26; j++)
				{
					float num6 = BlastFx.Rand(Seed, j + 700) * 6.283f;
					float num7 = BlastFx.Rand(Seed, j + 701) - 0.5f;
					float num8 = 2f + 4f * BlastFx.Rand(Seed, j + 702);
					parts.Add(new FxParticles.P
					{
						Pos = centre,
						Vel = new Vec3((float)Math.Cos(num6) * num8, (float)Math.Sin(num6) * num8, num7 * num8),
						Life = 0.8f + 0.6f * BlastFx.Rand(Seed, j + 703),
						Size = 0.4f,
						EndSize = 0.1f,
						Color = UfoArt.BeamCream,
						EndColor = ((BlastFx.Rand(Seed, j + 704) < 0.3f) ? UfoArt.Bolt : UfoArt.BeamGreen),
						Gravity = 0f,
						Drag = 1.6f,
						Kind = FxParticles.Kind.Glow,
						Alpha = 230
					});
				}
			}
		}
	}
}
