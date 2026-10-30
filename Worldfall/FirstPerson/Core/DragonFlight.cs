using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class DragonFlight
	{
		private enum Act
		{
			Cruise,
			Swoop,
			Land,
			Stand,
			Burn,
			TakeOff,
			Lie,
			Rise,
			Fall,
			Dead
		}

		private sealed class Route
		{
			private sealed class Seg
			{
				public Vec3 P0;

				public Vec3 P1;

				public Vec3 P2;

				public Vec3 P3;

				public float V0;

				public float V1;

				public float Start;

				public float Length;

				public readonly float[] Table = new float[33];
			}

			private readonly List<Seg> _segs = new List<Seg>();

			public float Length;

			public void Clear()
			{
				_segs.Clear();
				Length = 0f;
			}

			public void Add(Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3, float v0, float v1)
			{
				Seg seg = new Seg
				{
					P0 = p0,
					P1 = p1,
					P2 = p2,
					P3 = p3,
					V0 = v0,
					V1 = v1,
					Start = Length
				};
				Vec3 vec = p0;
				float num = 0f;
				seg.Table[0] = 0f;
				for (int i = 1; i <= 32; i++)
				{
					Vec3 vec2 = Bez(seg, (float)i / 32f);
					Vec3 vec3 = vec2 - vec;
					num += (float)Math.Sqrt(Vec3.Dot(vec3, vec3));
					seg.Table[i] = num;
					vec = vec2;
				}
				seg.Length = Math.Max(0.001f, num);
				Length += seg.Length;
				_segs.Add(seg);
			}

			private static Vec3 Bez(Seg s, float t)
			{
				float num = 1f - t;
				return s.P0 * (num * num * num) + s.P1 * (3f * num * num * t) + s.P2 * (3f * num * t * t) + s.P3 * (t * t * t);
			}

			private static Vec3 BezD(Seg s, float t)
			{
				float num = 1f - t;
				return (s.P1 - s.P0) * (3f * num * num) + (s.P2 - s.P1) * (6f * num * t) + (s.P3 - s.P2) * (3f * t * t);
			}

			public int SegmentIndex(float at)
			{
				for (int i = 0; i < _segs.Count; i++)
				{
					if (at < _segs[i].Start + _segs[i].Length)
					{
						return i;
					}
				}
				return _segs.Count - 1;
			}

			public float SegmentStart(int i)
			{
				if (i >= _segs.Count)
				{
					return Length;
				}
				return _segs[i].Start;
			}

			public float SegmentLength(int i)
			{
				if (i >= _segs.Count)
				{
					return 0f;
				}
				return _segs[i].Length;
			}

			private float T(Seg s, float local)
			{
				local = Math.Max(0f, Math.Min(s.Length, local));
				for (int i = 1; i <= 32; i++)
				{
					if (s.Table[i] >= local)
					{
						float num = s.Table[i - 1];
						float num2 = s.Table[i];
						return ((float)(i - 1) + ((num2 > num) ? ((local - num) / (num2 - num)) : 0f)) / 32f;
					}
				}
				return 1f;
			}

			public Vec3 Point(float at)
			{
				if (_segs.Count == 0)
				{
					return new Vec3(0f, 0f, 0f);
				}
				Seg seg = _segs[SegmentIndex(at)];
				return Bez(seg, T(seg, at - seg.Start));
			}

			public Vec3 Tangent(float at)
			{
				if (_segs.Count == 0)
				{
					return new Vec3(1f, 0f, 0f);
				}
				Seg seg = _segs[SegmentIndex(at)];
				Vec3 vec = BezD(seg, T(seg, at - seg.Start));
				if (!(Vec3.Dot(vec, vec) > 1E-08f))
				{
					return new Vec3(1f, 0f, 0f);
				}
				return vec.Normalized();
			}

			public float SpeedAt(float at)
			{
				if (_segs.Count == 0)
				{
					return 8f;
				}
				Seg seg = _segs[SegmentIndex(at)];
				float num = Math.Max(0f, Math.Min(1f, (at - seg.Start) / seg.Length));
				return Math.Max(1.2f, seg.V0 + (seg.V1 - seg.V0) * num);
			}
		}

		public const int Fly = 0;

		public const int LandAttack = 1;

		public const int Death = 2;

		public const int SleepStart = 3;

		public const int SleepLoop = 4;

		public const int SleepUp = 5;

		public const int Landing = 6;

		public const int Slide = 7;

		public const int Up = 8;

		public const int Idle = 9;

		private const float Pi = MathF.PI;

		private const float G = 9.8f;

		public float Scale = 0.8f;

		public Vec3 Pos;

		public float Heading;

		public float Pitch;

		public float Roll;

		public readonly DragonPose Pose = new DragonPose();

		public float Fire;

		public Vec3 FireAt;

		public float Altitude;

		public int HoldFrame = -1;

		public float Speed;

		public bool Downbeat;

		public bool Touchdown;

		public bool Liftoff;

		public bool Roar;

		public bool Whoosh;

		public bool Snort;

		public float BeatPower;

		public float Thump;

		public static int Faults;

		private Act _act;

		private float _actTime;

		private bool _started;

		private int _gameState = -1;

		private int _gameFrame;

		private float _stateTime;

		private float _gx;

		private float _gy;

		private float _gvx;

		private float _gvy;

		private float _vz;

		private float _turn;

		private int _orbit = 1;

		private float _parked;

		private uint _rng = 2654435769u;

		private float _phase;

		private float _power;

		private float _powerWant;

		private float _period = 1.5f;

		private bool _gliding;

		private float _glideLeft;

		private float _beatsLeft;

		private float _lastA;

		private readonly DragonPose _base = new DragonPose();

		private readonly DragonPose _want = new DragonPose();

		private float _blendRate = 3f;

		private readonly Route _route = new Route();

		private float _routeS;

		private float _strafeFrom;

		private float _strafeTo;

		private float _stripY;

		private float _stripDir;

		private float _stripX0;

		private float _stripX1;

		private float _lookYaw;

		private float _lookPitch;

		private float _lookYawWant;

		private float _lookPitchWant;

		private float _lookNext;

		private float _ruffle;

		private float _ruffleNext = 6f;

		private float _faceWant;

		private float _burnPhaseTime;

		private int _burnStage;

		private float _nextRoar = 12f;

		private float _deathRoll;

		private readonly float _seed;

		private float _pitchExtra;

		private bool _whooshed;

		private bool _swoopAgain;

		private DragonInput _input;

		private float _snortNext = 4f;

		private float _routeLift;

		private float _roar = 9f;

		private float _tailClock;

		public float FireReach
		{
			get
			{
				if (_act != Act.Swoop)
				{
					return 11f;
				}
				return H(26f);
			}
		}

		public float Glow => Pose.Glow;

		public bool Airborne
		{
			get
			{
				if (_act != Act.Stand && _act != Act.Burn && _act != Act.Lie && _act != Act.Rise)
				{
					return _act != Act.Dead;
				}
				return false;
			}
		}

		public string ActName => _act.ToString();

		public float FlapPhase => _phase;

		public float FlapPower => _power;

		public bool Dead => _act == Act.Dead;

		public float CruiseHeight => 13f + 6f * Scale;

		private float Rand()
		{
			_rng ^= _rng << 13;
			_rng ^= _rng >> 17;
			_rng ^= _rng << 5;
			return (float)(_rng & 0xFFFFFF) / 16777216f;
		}

		public DragonFlight(uint seed)
		{
			_rng = (uint)((int)seed * -1640531535 + 19088743);
			if (_rng == 0)
			{
				_rng = 1u;
			}
			_seed = (float)(seed % 1000) * 0.0137f;
		}

		private float H(float x)
		{
			return x * Scale;
		}

		public void Step(DragonInput g, float dt, Func<float, float, float> ground, Vec3 viewer)
		{
			Downbeat = (Touchdown = (Liftoff = (Roar = (Whoosh = (Snort = false)))));
			BeatPower = 0f;
			if (dt <= 0f)
			{
				Compose(0f, viewer);
				return;
			}
			dt = Math.Min(dt, 0.1f);
			if (!_started)
			{
				Begin(g, ground);
			}
			else
			{
				float num = (g.X - _gx) / dt;
				float num2 = (g.Y - _gy) / dt;
				if (num * num + num2 * num2 > 3600f)
				{
					Begin(g, ground);
				}
				else
				{
					float num3 = 1f - (float)Math.Exp(-6f * dt);
					_gvx += (num - _gvx) * num3;
					_gvy += (num2 - _gvy) * num3;
				}
			}
			_gx = g.X;
			_gy = g.Y;
			_input = g;
			if (g.State != _gameState || g.Frame < _gameFrame - 2)
			{
				int gameState = _gameState;
				_gameState = g.State;
				_stateTime = 0f;
				OnGameState(gameState, g, ground);
			}
			_gameFrame = g.Frame;
			_stateTime += dt;
			_actTime += dt;
			Thump = Math.Max(0f, Thump - dt * 1.6f);
			if (!g.Alive && _act != Act.Fall && _act != Act.Dead)
			{
				Die(ground);
			}
			HoldFrame = -1;
			if (_act != Act.Swoop && _act != Act.Burn)
			{
				Fire = MoveTowards(Fire, 0f, dt * 4f);
			}
			switch (_act)
			{
			case Act.Cruise:
				Cruise(dt, ground, climbing: false);
				break;
			case Act.TakeOff:
				TakeOff(dt, ground);
				break;
			case Act.Swoop:
				Swoop(dt, ground);
				break;
			case Act.Land:
				Land(dt, ground);
				break;
			case Act.Stand:
				Stand(dt, ground, viewer);
				break;
			case Act.Burn:
				Burn(dt, ground);
				break;
			case Act.Lie:
				Lie(dt, ground);
				break;
			case Act.Rise:
				Rise(dt, ground);
				break;
			case Act.Fall:
				Fall(dt, ground);
				break;
			case Act.Dead:
				DeadStill(dt, ground);
				break;
			}
			Wingbeat(dt);
			if (!Finite(Pos) || !Finite(Heading) || !Finite(Pitch) || !Finite(Roll) || !Finite(Speed) || !Finite(_turn) || !Finite(_vz))
			{
				Faults++;
				_turn = (_vz = 0f);
				Pitch = (Roll = 0f);
				Begin(g, ground);
			}
			Compose(dt, viewer);
			Altitude = Pos.Z - ground(Pos.X, Pos.Y);
		}

		private void Begin(DragonInput g, Func<float, float, float> ground)
		{
			_started = true;
			_gx = g.X;
			_gy = g.Y;
			_gvx = (_gvy = 0f);
			_gameState = g.State;
			_gameFrame = g.Frame;
			float num = ground(g.X, g.Y);
			Heading = (g.Flip ? 0f : MathF.PI);
			int num2;
			if (!g.Flying && g.State != 0 && g.State != 7)
			{
				num2 = ((g.State == 8) ? 1 : 0);
				if (num2 == 0)
				{
					Pos = new Vec3(g.X, g.Y, num);
					Speed = 0f;
					bool flag = g.State == 4 || g.State == 3;
					SetAct(flag ? Act.Lie : Act.Stand);
					if (flag)
					{
						DragonPoses.Asleep(_base);
						_actTime = 10f;
					}
					else
					{
						DragonPoses.Folded(_base);
					}
					_power = 0f;
					goto IL_0160;
				}
			}
			else
			{
				num2 = 1;
			}
			Pos = new Vec3(g.X, g.Y, num + CruiseHeight);
			Speed = 8f;
			SetAct(Act.Cruise);
			DragonPoses.Glide(_base);
			_power = 0.5f;
			goto IL_0160;
			IL_0160:
			if (num2 != 0 && (g.State == 6 || g.State == 7))
			{
				OnGameState(-1, g, ground);
			}
		}

		private void SetAct(Act a)
		{
			_act = a;
			_actTime = 0f;
		}

		private void OnGameState(int old, DragonInput g, Func<float, float, float> ground)
		{
			switch (g.State)
			{
			case 7:
				if (_act == Act.Swoop)
				{
					_swoopAgain = true;
				}
				else if (_act == Act.Cruise || _act == Act.TakeOff)
				{
					PlanSwoop(g, ground);
				}
				break;
			case 6:
				if (_act == Act.Cruise || _act == Act.TakeOff || _act == Act.Swoop)
				{
					PlanLanding(g, ground);
				}
				break;
			case 1:
				if (_act == Act.Stand || _act == Act.Burn)
				{
					StartBurn();
				}
				break;
			case 8:
				if (_act == Act.Land)
				{
					SetAct(Act.Cruise);
					_beatsLeft = 4f;
				}
				else if (!Airborne)
				{
					StartTakeOff();
				}
				break;
			case 3:
			case 4:
				if (_act == Act.Stand)
				{
					SetAct(Act.Lie);
				}
				break;
			case 5:
				if (_act == Act.Lie)
				{
					SetAct(Act.Rise);
				}
				break;
			case 0:
				if (!Airborne)
				{
					StartTakeOff();
				}
				break;
			case 2:
				break;
			}
		}

		private void Cruise(float dt, Func<float, float, float> ground, bool climbing)
		{
			if (!_input.Flying && (_gameState == 9 || _gameState == 1 || _gameState == 3 || _gameState == 4 || _gameState == 5 || _gameState == 6))
			{
				PlanLanding(_input, ground);
				return;
			}
			float num = (float)Math.Sqrt(_gvx * _gvx + _gvy * _gvy);
			_parked = ((num < 0.8f) ? (_parked + dt) : 0f);
			float num2 = _gx + _gvx * 1.4f;
			float num3 = _gy + _gvy * 1.4f;
			float num4 = num2 - Pos.X;
			float num5 = num3 - Pos.Y;
			float num6 = (float)Math.Sqrt(num4 * num4 + num5 * num5);
			float num7 = H(16f);
			float x;
			float y;
			float num16;
			if (_parked > 0.4f && num6 < num7 * 2.2f)
			{
				float num8 = Pos.X - _gx;
				float num9 = Pos.Y - _gy;
				float num10 = Math.Max(0.01f, (float)Math.Sqrt(num8 * num8 + num9 * num9));
				num8 /= num10;
				num9 /= num10;
				if (_parked < 0.45f)
				{
					float num11 = (float)Math.Cos(Heading);
					float num12 = (float)Math.Sin(Heading);
					_orbit = ((num8 * num12 - num9 * num11 > 0f) ? 1 : (-1));
				}
				float num13 = (0f - num9) * (float)_orbit;
				float num14 = num8 * (float)_orbit;
				float num15 = (num7 - num10) * 0.08f;
				x = num13 + num8 * num15;
				y = num14 + num9 * num15;
				num16 = 7.5f;
			}
			else
			{
				x = num4;
				y = num5;
				num16 = Math.Max(num, 6.5f) + Math.Max(0f, num6 - H(10f)) * 0.35f;
				num16 = Math.Min(num16, 17f);
				if (num6 < H(4f))
				{
					num16 = Math.Max(5f, num);
				}
			}
			Steer(Atan2(y, x), num16, dt, climbing ? 0.55f : 0.7f);
			float num17 = (float)Math.Cos(Heading);
			float num18 = (float)Math.Sin(Heading);
			float num19 = ground(Pos.X, Pos.Y);
			for (int i = 1; i <= 3; i++)
			{
				num19 = Math.Max(num19, ground(Pos.X + num17 * (float)i * 7f, Pos.Y + num18 * (float)i * 7f));
			}
			float num20 = 2.2f * (float)Math.Sin(_actTime * 0.21f + _seed * 7f) + 1.2f * (float)Math.Sin(_actTime * 0.53f);
			float to = Clamp((Math.Max(num19, 0f) + CruiseHeight + num20 - Pos.Z) * 0.6f, -3.5f, 4.5f);
			_vz = MoveTowards(_vz, to, 2.5f * dt);
			Pos = new Vec3(Pos.X + num17 * Speed * dt, Pos.Y + num18 * Speed * dt, Pos.Z + _vz * dt);
			float num21 = num19 + H(5f);
			if (Pos.Z < num21)
			{
				Pos = new Vec3(Pos.X, Pos.Y, num21);
				_vz = Math.Max(_vz, 0f);
			}
			float need = 0.28f + Math.Max(0f, _vz) * 0.15f + Math.Max(0f, num16 - Speed) * 0.08f;
			Flapping(need, dt, 1.55f);
			PitchFromClimb(dt, 0.85f);
			BankFromTurn(dt);
			DragonPoses.Glide(_want);
			_blendRate = 2.5f;
			_want.NeckYaw = Clamp(_turn * 0.5f, -0.4f, 0.4f);
			_want.TailYaw = Clamp((0f - _turn) * 0.35f, -0.3f, 0.3f);
			if (_actTime > _nextRoar)
			{
				RoarNow();
				_nextRoar = _actTime + 18f + Rand() * 25f;
			}
		}

		private void Steer(float wantHeading, float wantSpeed, float dt, float maxTurn)
		{
			float num = DeltaAngle(Heading, wantHeading);
			wantSpeed *= 0.6f + 0.4f * Math.Max(0f, (float)Math.Cos(num));
			float to = Clamp(num * 1.2f, 0f - maxTurn, maxTurn);
			_turn = MoveTowards(_turn, to, 1.1f * dt);
			Heading = Wrap(Heading + _turn * dt);
			float num2 = ((wantSpeed > Speed) ? 3.2f : 2.5f);
			Speed = MoveTowards(Speed, wantSpeed, num2 * dt);
		}

		private void PitchFromClimb(float dt, float k)
		{
			float num = (float)Math.Atan2(_vz, Math.Max(2f, Speed)) * k;
			Pitch += (num - Pitch) * (1f - (float)Math.Exp(-3f * dt));
		}

		private void BankFromTurn(float dt)
		{
			float num = Clamp((float)Math.Atan(_turn * Speed / 9.8f), -0.95f, 0.95f);
			Roll += (num - Roll) * (1f - (float)Math.Exp(-2.5f * dt));
		}

		private void Flapping(float need, float dt, float period)
		{
			if (need >= 0.55f)
			{
				_gliding = false;
				_powerWant = Math.Min(1.3f, 0.55f + need * 0.5f);
				_period = period / (0.85f + 0.3f * Math.Min(1f, need));
			}
			else if (_gliding)
			{
				_glideLeft -= dt;
				_powerWant = 0f;
				if (_glideLeft <= 0f)
				{
					_gliding = false;
					_beatsLeft = 2 + (int)(Rand() * 3f);
				}
			}
			else
			{
				_powerWant = 0.62f + need * 0.4f;
				_period = period;
				if (_beatsLeft <= 0f)
				{
					_gliding = true;
					_glideLeft = 2.2f + Rand() * 3.2f;
				}
			}
		}

		private void Wingbeat(float dt)
		{
			float num = 0.21f;
			float num2 = Frac(_phase / (MathF.PI * 2f));
			if (_powerWant > 0.02f || _power > 0.05f)
			{
				bool flag = _powerWant <= 0.02f;
				float num3 = num2 + dt / Math.Max(0.4f, _period);
				if (flag && num2 < num && num3 >= num)
				{
					num3 = num;
				}
				if (flag && Math.Abs(num2 - num) < 0.002f)
				{
					num3 = num;
				}
				if (num3 >= 1f)
				{
					num3 -= 1f;
					if (!flag)
					{
						_beatsLeft -= 1f;
					}
				}
				if (_lastA > 0.8f && num3 < 0.2f && _power > 0.25f)
				{
					Downbeat = true;
					BeatPower = Math.Min(1f, _power);
				}
				_lastA = num3;
				_phase = num3 * 2f * MathF.PI;
			}
			float num4 = ((_powerWant > _power) ? 2.2f : (Airborne ? 1.4f : 5f));
			_power = MoveTowards(_power, _powerWant, num4 * dt);
		}

		private void PlanSwoop(DragonInput g, Func<float, float, float> ground)
		{
			float num = (g.Flip ? (-1f) : 1f);
			float num2 = g.Y + 0.5f;
			float num3 = g.X + num * 5f;
			float num4 = g.X + num * 39f;
			float num5 = H(8.5f);
			float num6 = Math.Max(ground(num3, num2), 0f);
			float num7 = Math.Max(ground(num4, num2), 0f);
			Vec3 pos = Pos;
			float num8 = (float)Math.Cos(Heading);
			float num9 = (float)Math.Sin(Heading);
			Vec3 vec = new Vec3(num3 - num * H(9f), num2, num6 + num5 + H(2f));
			Vec3 vec2 = new Vec3(num3, num2, num6 + num5);
			new Vec3(num4, num2, num7 + num5);
			float num10 = ((Rand() < 0.5f) ? (-1f) : 1f);
			Vec3 vec3 = new Vec3(num4 + num * H(6f), num2 + num10 * H(16f), Math.Max(num7, ground(num4, num2 + num10 * 12f)) + CruiseHeight * 0.75f);
			_route.Clear();
			float num11 = Math.Max(H(14f), Dist2(pos, vec) * 0.45f);
			bool flag = (vec.X - pos.X) * num < H(10f);
			Vec3 p = pos + new Vec3(num8 * num11, num9 * num11, H(flag ? 5f : 2f));
			Vec3 p2 = vec - new Vec3(num * num11 * (flag ? 1.1f : 0.8f), 0f, 0f - H(flag ? 10f : 7f));
			_route.Add(pos, p, p2, vec, Math.Max(Speed, 8f), 26f);
			_route.Add(vec, vec + new Vec3(num * H(3f), 0f, 0f - H(1f)), vec2 - new Vec3(num * H(3f), 0f, 0f), vec2, 26f, 25f);
			int num12 = 4;
			Vec3 vec4 = vec2;
			for (int i = 1; i <= num12; i++)
			{
				float num13 = num3 + (num4 - num3) * (float)i / (float)num12;
				Vec3 vec5 = new Vec3(num13, num2, Math.Max(ground(num13, num2), 0f) + num5);
				_route.Add(vec4, vec4 + (vec5 - vec4) * 0.33f, vec4 + (vec5 - vec4) * 0.67f, vec5, 25f, 24f);
				vec4 = vec5;
			}
			_route.Add(vec4, vec4 + new Vec3(num * H(12f), 0f, H(4f)), vec3 + new Vec3(num * H(4f), 0f, 0f - H(2f)), vec3, 24f, 9f);
			_strafeFrom = _route.SegmentStart(1) + _route.SegmentLength(1) * 0.6f;
			_stripY = num2;
			_stripDir = num;
			_stripX0 = num3;
			_stripX1 = num4;
			_strafeTo = _route.SegmentStart(2 + num12);
			_routeS = 0f;
			_routeLift = Math.Max(0f, _routeLift);
			_whooshed = false;
			SetAct(Act.Swoop);
			RoarNow();
		}

		private void Swoop(float dt, Func<float, float, float> ground)
		{
			float num = _route.SpeedAt(_routeS);
			_routeS += num * dt;
			if (_routeS >= _strafeFrom)
			{
				_ = _routeS <= _strafeTo + H(4f);
			}
			else
				_ = 0;
			if ((_routeS < _strafeFrom || _swoopAgain) && _gameState == 7)
			{
				HoldFrame = 7;
			}
			if (_routeS >= _route.Length)
			{
				if (_swoopAgain && _gameState == 7)
				{
					_swoopAgain = false;
					PlanSwoop(_input, ground);
					return;
				}
				_swoopAgain = false;
				_vz = 2f;
				_turn = 0f;
				SetAct(Act.Cruise);
				_actTime = 3f;
				_beatsLeft = 4f;
				return;
			}
			FollowRoute(dt, ground, H(4f));
			float num2 = Clamp((0f - _route.Tangent(_routeS).Z) * 2.2f, 0f, 1f);
			if (num2 > 0.4f && _route.SegmentIndex(_routeS) == 0 && !_whooshed)
			{
				Whoosh = true;
				_whooshed = true;
			}
			float num3 = _routeS / Math.Max(1f, _route.Length);
			if (_route.SegmentIndex(_routeS) == 0)
			{
				DragonPoses.Glide(_want);
				DragonPose dragonPose = new DragonPose();
				DragonPoses.Dive(dragonPose);
				_want.Blend(dragonPose, Clamp(num2 * 1.4f, 0f, 1f));
				Flapping((_routeS < H(8f)) ? 0.9f : 0.1f, dt, 1.2f);
				_blendRate = 3.5f;
			}
			else if (_routeS < _strafeTo)
			{
				DragonPoses.Glide(_want);
				for (int i = 0; i < 2; i++)
				{
					_want.Elev[i] = 0.16f;
					_want.Sweep[i] = 0.2f;
					_want.Billow[i] = 0.35f;
					_want.Elbow[i] = 0.1f;
				}
				Flapping(0.1f, dt, 1.2f);
				_blendRate = 4f;
			}
			else
			{
				DragonPoses.Glide(_want);
				Flapping(1.1f, dt, 1.1f);
				_blendRate = 3f;
			}
			float num4 = ((_routeS > _strafeFrom - H(2f) && _routeS < _strafeTo) ? 1f : 0f);
			Fire = MoveTowards(Fire, num4, dt * ((num4 > Fire) ? 5f : 3.5f));
			float num5 = ((_stripDir > 0f) ? Math.Max(Pos.X, _stripX0) : Math.Min(Pos.X, _stripX0)) + _stripDir * H(22f);
			FireAt = new Vec3(num5, _stripY, ground(num5, _stripY));
			_want.Glow = Math.Max(Fire, Clamp((_routeS - (_strafeFrom - H(14f))) / H(14f), 0f, 1f) * ((_routeS < _strafeTo) ? 1f : 0f));
			_want.Jaw = Fire;
			if (num3 > 0.98f)
			{
				Fire = 0f;
			}
		}

		private void FollowRoute(float dt, Func<float, float, float> ground, float clearance)
		{
			Vec3 pos = _route.Point(_routeS);
			Vec3 vec = _route.Tangent(_routeS);
			Vec3 vec2 = _route.Point(Math.Min(_route.Length, _routeS + Math.Max(4f, Speed * 0.7f)));
			float num = Math.Max(ground(pos.X, pos.Y), 0f);
			float num2 = Math.Max(ground(vec2.X, vec2.Y), 0f);
			float num3 = Math.Max(num + clearance - pos.Z, num2 + clearance - vec2.Z);
			_routeLift = ((num3 > _routeLift) ? MoveTowards(_routeLift, num3, H(40f) * dt) : MoveTowards(_routeLift, Math.Max(0f, num3), H(5f) * dt));
			_routeLift = Math.Max(_routeLift, num + clearance * 0.5f - pos.Z);
			if (_routeLift > 0f)
			{
				pos = new Vec3(pos.X, pos.Y, pos.Z + _routeLift);
			}
			float num4 = Atan2(vec.Y, vec.X);
			float val = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
			float v = ((dt > 0f) ? (DeltaAngle(Heading, num4) / dt) : 0f);
			_turn += (Clamp(v, -2.5f, 2.5f) - _turn) * (1f - (float)Math.Exp(-6f * dt));
			Heading = num4;
			float num5 = (float)Math.Atan2(vec.Z, Math.Max(0.05f, val)) + _pitchExtra;
			Pitch += (num5 - Pitch) * (1f - (float)Math.Exp(-5f * dt));
			float num6 = Clamp((float)Math.Atan(_turn * Speed / 9.8f), -1.15f, 1.15f);
			Roll += (num6 - Roll) * (1f - (float)Math.Exp(-4f * dt));
			Speed = _route.SpeedAt(_routeS);
			_vz = vec.Z * Speed;
			Pos = pos;
		}

		private void PlanLanding(DragonInput g, Func<float, float, float> ground)
		{
			Vec3 pos = Pos;
			Vec3 vec = new Vec3(g.X, g.Y, ground(g.X, g.Y));
			float num = vec.X - pos.X;
			float num2 = vec.Y - pos.Y;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			float num4 = (float)Math.Cos(Heading);
			float num5 = (float)Math.Sin(Heading);
			float num6;
			float num7;
			if (g.Hostile)
			{
				num6 = 0f;
				num7 = -1f;
			}
			else if (num3 > H(22f) && num * num4 + num2 * num5 > num3 * 0.3f)
			{
				num6 = num / num3;
				num7 = num2 / num3;
			}
			else
			{
				num6 = 0f - num4;
				num7 = 0f - num5;
				if (num3 > 1f)
				{
					num6 = (num / num3 - num4) * 0.5f;
					num7 = (num2 / num3 - num5) * 0.5f;
					float num8 = (float)Math.Sqrt(num6 * num6 + num7 * num7);
					if (num8 < 0.2f)
					{
						num6 = 0f - num5;
						num7 = num4;
					}
					else
					{
						num6 /= num8;
						num7 /= num8;
					}
				}
			}
			float num9 = pos.Z - vec.Z;
			float num10 = Clamp(num9 * 1.2f, H(11f), H(24f));
			Vec3 vec2 = vec - new Vec3(num6 * num10, num7 * num10, 0f - Math.Min(H(6f), Math.Max(H(2.5f), num9 * 0.5f)));
			float num11 = Math.Max(H(12f), Dist2(pos, vec2) * 0.4f);
			_route.Clear();
			_route.Add(pos, pos + new Vec3(num4 * num11, num5 * num11, 0f - H(1f)), vec2 - new Vec3(num6 * num11 * 0.7f, num7 * num11 * 0.7f, 0f - H(2f)), vec2, Math.Max(10f, Speed), 11f);
			_route.Add(vec2, vec2 + new Vec3(num6 * num10 * 0.45f, num7 * num10 * 0.45f, 0f - H(3f)), vec - new Vec3(num6 * num10 * 0.25f, num7 * num10 * 0.25f, 0f - H(1.2f)), vec, 11f, 1.5f);
			_routeS = 0f;
			SetAct(Act.Land);
		}

		private void Land(float dt, Func<float, float, float> ground)
		{
			if (_gameState == 6)
			{
				HoldFrame = 4;
			}
			float num = _route.Length - _routeS;
			float num2 = _route.SpeedAt(_routeS);
			_routeS = Math.Min(_route.Length, _routeS + num2 * dt);
			float num3 = H(13f);
			float num4 = Clamp(1f - num / num3, 0f, 1f);
			_pitchExtra = Smooth(num4) * 0.95f;
			FollowRoute(dt, ground, H(3f) * Clamp(num / H(9f), 0f, 1f));
			_pitchExtra = 0f;
			DragonPoses.Glide(_want);
			if (num4 > 0f)
			{
				DragonPose dragonPose = new DragonPose();
				DragonPoses.Flare(dragonPose);
				_want.Blend(dragonPose, Smooth(num4));
				Flapping((num4 > 0.25f) ? 1.2f : 0.3f, dt, 0.95f);
				_blendRate = 5f;
			}
			else
			{
				Flapping((_routeS < H(10f)) ? 0.5f : 0.15f, dt, 1.5f);
				_blendRate = 2.5f;
			}
			if (num <= 0.01f)
			{
				Touchdown = true;
				Thump = 1f;
				Pos = new Vec3(Pos.X, Pos.Y, ground(Pos.X, Pos.Y));
				Pitch = 0.35f;
				_vz = 0f;
				Speed = 0f;
				_turn = 0f;
				SetAct(Act.Stand);
				_faceWant = Heading;
				_powerWant = 0f;
			}
		}

		private void Settle(float dt, Func<float, float, float> ground)
		{
			Pos = new Vec3(Pos.X + (_gx - Pos.X) * Math.Min(1f, dt * 2f), Pos.Y + (_gy - Pos.Y) * Math.Min(1f, dt * 2f), 0f);
			Pos = new Vec3(Pos.X, Pos.Y, ground(Pos.X, Pos.Y));
			Pitch += (0f - Pitch) * (1f - (float)Math.Exp(-4f * dt));
			Roll += (0f - Roll) * (1f - (float)Math.Exp(-4f * dt));
			Speed = 0f;
			_vz = 0f;
			_powerWant = 0f;
		}

		private bool TurnTo(float want, float dt)
		{
			float num = DeltaAngle(Heading, want);
			if (Math.Abs(num) < 0.08f)
			{
				_want.Stride = 0f;
				return false;
			}
			float num2 = 1.7f;
			Heading = Wrap(Heading + Clamp(num, (0f - num2) * dt, num2 * dt));
			_want.Stride = 0.7f;
			Pose.WalkPhase += dt * 5.5f;
			return true;
		}

		private void Stand(float dt, Func<float, float, float> ground, Vec3 viewer)
		{
			Settle(dt, ground);
			DragonPoses.Folded(_want);
			_blendRate = ((_actTime < 1.4f) ? 1.6f : 2.5f);
			if (_actTime < 0.9f)
			{
				float num = (float)Math.Sin(Math.Min(1f, _actTime / 0.9f) * MathF.PI);
				_want.Crouch = 0.55f * num;
				if (_actTime < 0.5f)
				{
					DragonPoses.Flare(_want);
					_want.Tuck = 0f;
					_want.Reach = 0f;
					_want.Crouch = 0.55f * num;
				}
			}
			TurnTo(_faceWant, dt);
			LookAbout(dt, viewer);
			if (_gameState == 1)
			{
				StartBurn();
			}
			else if (_gameState == 3 || _gameState == 4)
			{
				SetAct(Act.Lie);
			}
			else if (_gameState == 8 || (_gameState == 0 && _actTime > 0.5f))
			{
				StartTakeOff();
			}
		}

		private void LookAbout(float dt, Vec3 viewer)
		{
			_lookNext -= dt;
			if (_lookNext <= 0f)
			{
				_lookNext = 2.5f + Rand() * 3.5f;
				float num = viewer.X - Pos.X;
				float num2 = viewer.Y - Pos.Y;
				float num3 = (float)Math.Sqrt(num * num + num2 * num2);
				if (num3 < 45f && Rand() < 0.6f)
				{
					_lookYawWant = Clamp(DeltaAngle(Heading, Atan2(num2, num)), -1.3f, 1.3f);
					_lookPitchWant = Clamp((float)Math.Atan2(viewer.Z - (Pos.Z + H(9f)), Math.Max(1f, num3)), -0.5f, 0.4f);
				}
				else
				{
					_lookYawWant = (Rand() - 0.5f) * 1.8f;
					_lookPitchWant = (Rand() - 0.6f) * 0.4f;
				}
			}
			_lookYaw += (_lookYawWant - _lookYaw) * (1f - (float)Math.Exp(-1.8f * dt));
			_lookPitch += (_lookPitchWant - _lookPitch) * (1f - (float)Math.Exp(-1.8f * dt));
			_want.HeadYaw += _lookYaw;
			_want.HeadPitch += _lookPitch;
			_want.NeckYaw += _lookYaw * 0.5f;
			_want.TailSway = 0.12f;
			_snortNext -= dt;
			if (_snortNext <= 0f)
			{
				Snort = true;
				_snortNext = 5f + Rand() * 6f;
			}
			_ruffleNext -= dt;
			if (_ruffleNext <= 0f)
			{
				_ruffle = 1.6f;
				_ruffleNext = 9f + Rand() * 10f;
			}
			if (_ruffle > 0f)
			{
				_ruffle -= dt;
				float num4 = (float)Math.Sin(Clamp(1f - _ruffle / 1.6f, 0f, 1f) * MathF.PI);
				DragonPose dragonPose = new DragonPose();
				DragonPoses.Threat(dragonPose);
				for (int i = 0; i < 2; i++)
				{
					_want.Elev[i] += (dragonPose.Elev[i] - _want.Elev[i]) * num4 * 0.7f;
					_want.Sweep[i] += (dragonPose.Sweep[i] - _want.Sweep[i]) * num4 * 0.7f;
					_want.Elbow[i] += (dragonPose.Elbow[i] - _want.Elbow[i]) * num4 * 0.7f;
					_want.Wrist[i] += (dragonPose.Wrist[i] - _want.Wrist[i]) * num4 * 0.7f;
					_want.Spread[i] += (dragonPose.Spread[i] - _want.Spread[i]) * num4 * 0.7f;
				}
			}
		}

		private void StartBurn()
		{
			SetAct(Act.Burn);
			_burnStage = 0;
			_burnPhaseTime = 0f;
		}

		private void Burn(float dt, Func<float, float, float> ground)
		{
			Settle(dt, ground);
			DragonPoses.Folded(_want);
			_blendRate = 3.5f;
			_burnPhaseTime += dt;
			float want = -MathF.PI / 2f;
			if (_gameState == 1 && _burnStage < 3)
			{
				HoldFrame = 5;
			}
			switch (_burnStage)
			{
			case 0:
				if (!TurnTo(want, dt) || _burnPhaseTime > 3f)
				{
					_burnStage = 1;
					_burnPhaseTime = 0f;
					RoarNow();
				}
				break;
			case 1:
			{
				float num6 = Smooth(Clamp(_burnPhaseTime / 0.7f, 0f, 1f));
				DragonPose dragonPose3 = new DragonPose();
				DragonPoses.Threat(dragonPose3);
				_want.Blend(dragonPose3, num6);
				_want.Jaw = 0.9f * (float)Math.Sin(Clamp(_burnPhaseTime / 1f, 0f, 1f) * MathF.PI);
				_want.HeadPitch = 0.35f * num6;
				_want.Glow = num6 * 0.4f;
				if (_burnPhaseTime > 1f)
				{
					_burnStage = 2;
					_burnPhaseTime = 0f;
				}
				break;
			}
			case 2:
			{
				DragonPose dragonPose = new DragonPose();
				DragonPoses.Threat(dragonPose);
				_want.Blend(dragonPose, 1f);
				float num = Clamp(_burnPhaseTime / 0.45f, 0f, 1f);
				_want.NeckCurl = 0.35f + 0.35f * num;
				_want.HeadPitch = 0.3f + 0.1f * num;
				_want.Chest = num;
				_want.Glow = 0.4f + 0.6f * num;
				_want.Jaw = 0.15f;
				if (_burnPhaseTime > 0.45f)
				{
					_burnStage = 3;
					_burnPhaseTime = 0f;
				}
				break;
			}
			case 3:
			{
				DragonPose dragonPose2 = new DragonPose();
				DragonPoses.Threat(dragonPose2);
				_want.Blend(dragonPose2, 1f);
				float num2 = Clamp(_burnPhaseTime / 1.5f, 0f, 1f);
				float num3 = (float)Math.Sin((num2 - 0.5f) * MathF.PI) * 0.75f;
				_want.NeckCurl = -0.1f;
				_want.NeckPitch = -0.15f;
				_blendRate = 7f;
				_want.NeckYaw = num3 * 0.6f;
				_want.HeadYaw = num3;
				_want.HeadPitch = -0.38f;
				_want.Jaw = 1f;
				_want.Glow = 1f;
				_want.Chest = 1f - num2;
				for (int i = 0; i < 2; i++)
				{
					_want.Elev[i] -= 0.15f;
				}
				Fire = MoveTowards(Fire, (_burnPhaseTime > 0.22f && num2 < 0.9f) ? 1f : 0f, dt * 6f);
				float num4 = Heading + num3;
				float num5 = H(8f);
				FireAt = new Vec3(Pos.X + (float)Math.Cos(num4) * num5, Pos.Y + (float)Math.Sin(num4) * num5, 0f);
				FireAt = new Vec3(FireAt.X, FireAt.Y, ground(FireAt.X, FireAt.Y));
				if (_burnPhaseTime > 1.5f)
				{
					_burnStage = 4;
					_burnPhaseTime = 0f;
					Fire = 0f;
				}
				break;
			}
			default:
				Fire = 0f;
				_want.Glow = 0f;
				if (_burnPhaseTime > 0.6f)
				{
					SetAct(Act.Stand);
					_actTime = 2f;
					_faceWant = Heading;
				}
				break;
			}
			if (_burnStage < 3)
			{
				Fire = MoveTowards(Fire, 0f, dt * 4f);
			}
			if (_burnStage >= 4 && _gameState == 1 && _stateTime < 0.2f)
			{
				_burnStage = 2;
				_burnPhaseTime = 0f;
			}
		}

		private void StartTakeOff()
		{
			SetAct(Act.TakeOff);
			Fire = 0f;
			_phase = MathF.PI / 25f;
			_power = 0f;
		}

		private void TakeOff(float dt, Func<float, float, float> ground)
		{
			float actTime = _actTime;
			float num = ground(Pos.X, Pos.Y);
			if (actTime < 0.7f)
			{
				DragonPoses.Folded(_want);
				float num2 = Smooth(actTime / 0.7f);
				_want.Crouch = 0.75f * num2;
				for (int i = 0; i < 2; i++)
				{
					_want.Elev[i] = 1.05f;
					_want.Sweep[i] = 0.1f * (1f - num2);
					_want.Elbow[i] = 0.3f;
					_want.Wrist[i] = 0.35f;
					_want.Spread[i] = 0.8f;
				}
				_blendRate = 6f;
				_powerWant = 0f;
				Pos = new Vec3(Pos.X, Pos.Y, num);
				_phase = MathF.PI / 25f;
				return;
			}
			if (actTime - dt < 0.7f)
			{
				Liftoff = true;
				Thump = Math.Max(Thump, 0.7f);
				_vz = H(7.5f);
				Speed = 1.5f;
				_phase = 0.01f;
				_power = 1.2f;
			}
			DragonPoses.Glide(_want);
			_blendRate = 4f;
			_want.Reach = Clamp(1f - (actTime - 0.7f) / 2.2f, 0f, 1f) * 0.7f;
			_want.Tuck = 1f - _want.Reach;
			Flapping(1.3f, dt, 0.95f);
			float num3 = (float)Math.Sqrt(_gvx * _gvx + _gvy * _gvy);
			float wantSpeed = Math.Max(7f, num3);
			float wantHeading = ((num3 > 1f) ? Atan2(_gvy, _gvx) : Heading);
			Steer(wantHeading, wantSpeed, dt, 0.6f);
			float num4 = Math.Max(num, 0f) + CruiseHeight;
			float num5 = Clamp((actTime - 0.7f) / 2.5f, 0f, 1f);
			float to = ((Pos.Z < num4) ? (H(2.5f + 4.5f * num5) + (float)Math.Sin(_phase - 0.6f) * H(2.5f) * (1f - num5)) : 0f);
			_vz = MoveTowards(_vz, to, 9f * dt);
			float num6 = (float)Math.Cos(Heading);
			float num7 = (float)Math.Sin(Heading);
			Pos = new Vec3(Pos.X + num6 * Speed * dt, Pos.Y + num7 * Speed * dt, Math.Max(num, Pos.Z + _vz * dt));
			float num8 = 0.25f + 0.25f * num5;
			Pitch += (num8 - Pitch) * (1f - (float)Math.Exp(-3f * dt));
			BankFromTurn(dt);
			if (Pos.Z > num4 - 2f || actTime > 7f)
			{
				SetAct(Act.Cruise);
				_beatsLeft = 3f;
			}
		}

		private void Lie(float dt, Func<float, float, float> ground)
		{
			Settle(dt, ground);
			DragonPoses.Asleep(_want);
			_blendRate = 0.9f;
			float num = (float)Math.Sin(_actTime * 2f * MathF.PI / 4.2f);
			if ((float)Math.Sin((_actTime - dt) * 2f * MathF.PI / 4.2f) > -0.9f && num <= -0.9f)
			{
				Snort = true;
			}
			_want.Chest = 0.5f + 0.5f * num;
			_want.Bob = 0.06f * num;
			if (_gameState == 5 || _gameState == 9 || _gameState == 8 || _gameState == 0)
			{
				SetAct(Act.Rise);
			}
		}

		private void Rise(float dt, Func<float, float, float> ground)
		{
			Settle(dt, ground);
			DragonPoses.Folded(_want);
			_blendRate = 2f;
			float num = (float)Math.Sin(Clamp(_actTime / 1.6f, 0f, 1f) * MathF.PI);
			DragonPose dragonPose = new DragonPose();
			DragonPoses.Threat(dragonPose);
			_want.Blend(dragonPose, num * 0.8f);
			_want.Jaw = 0.7f * num;
			_want.HeadPitch = 0.3f * num;
			if (_actTime > 1.8f)
			{
				SetAct(Act.Stand);
				_actTime = 2f;
				_faceWant = Heading;
			}
		}

		private void Die(Func<float, float, float> ground)
		{
			Fire = 0f;
			_deathRoll = ((Rand() < 0.5f) ? (-1f) : 1f);
			if (Airborne)
			{
				SetAct(Act.Fall);
			}
			else
			{
				SetAct(Act.Dead);
			}
		}

		private void Fall(float dt, Func<float, float, float> ground)
		{
			_vz -= 8.82f * dt;
			Speed = MoveTowards(Speed, 3f, 2f * dt);
			float num = (float)Math.Cos(Heading);
			float num2 = (float)Math.Sin(Heading);
			float num3 = ground(Pos.X, Pos.Y);
			Pos = new Vec3(Pos.X + num * Speed * dt, Pos.Y + num2 * Speed * dt, Pos.Z + _vz * dt);
			Roll += _deathRoll * 0.9f * dt;
			Pitch += (-0.6f - Pitch) * (1f - (float)Math.Exp(-1.5f * dt));
			DragonPoses.Glide(_want);
			float actTime = _actTime;
			for (int i = 0; i < 2; i++)
			{
				_want.Elev[i] = 0.6f + 0.5f * (float)Math.Sin(actTime * 7f + (float)i * 2.1f);
				_want.Elbow[i] = 0.35f + 0.25f * (float)Math.Sin(actTime * 5f + (float)i);
				_want.Wrist[i] = 0.4f;
			}
			_want.NeckPitch = 0.3f;
			_want.Jaw = 0.5f;
			_want.Tuck = 0.3f;
			_blendRate = 4f;
			_powerWant = 0f;
			if (Pos.Z <= num3)
			{
				Pos = new Vec3(Pos.X, Pos.Y, num3);
				Touchdown = true;
				Thump = 1f;
				SetAct(Act.Dead);
				_actTime = 1.2f;
			}
		}

		private void DeadStill(float dt, Func<float, float, float> ground)
		{
			Pos = new Vec3(Pos.X, Pos.Y, ground(Pos.X, Pos.Y));
			Speed = 0f;
			_vz = 0f;
			_powerWant = 0f;
			Pitch += (0f - Pitch) * (1f - (float)Math.Exp(-2f * dt));
			DragonPoses.Folded(_want);
			float num = Smooth(Clamp(_actTime / 1.4f, 0f, 1f));
			_want.Crouch = num;
			_want.NeckPitch = -0.9f * num;
			_want.NeckCurl = -0.3f * num;
			_want.HeadPitch = -0.3f * num;
			_want.Jaw = 0.55f * num;
			_want.TailPitch = -0.2f;
			for (int i = 0; i < 2; i++)
			{
				_want.Elev[i] = 1.05f - 1.1f * num;
				_want.Sweep[i] = 0.9f;
				_want.Spread[i] = 0.35f;
			}
			_blendRate = 2f;
			Roll += (_deathRoll * 1.1f * num - Roll) * (1f - (float)Math.Exp(-2f * dt));
		}

		private void RoarNow()
		{
			Roar = true;
		}

		private void Compose(float dt, Vec3 viewer)
		{
			_base.Blend(_want, 1f - (float)Math.Exp((0f - _blendRate) * dt));
			float walkPhase = Pose.WalkPhase;
			Pose.CopyFrom(_base);
			Pose.WalkPhase = walkPhase;
			_tailClock += dt * ((_power > 0.05f) ? (MathF.PI * 2f / Math.Max(0.4f, _period) * 0.5f) : (Airborne ? 1.4f : 0.9f));
			Pose.TailWave = _tailClock;
			if (_power > 0.01f)
			{
				DragonPoses.Stroke(Pose, _phase, _power);
			}
			if (Roar)
			{
				_roar = 0f;
			}
			_roar += dt;
			if (_roar < 1.4f && Fire < 0.1f)
			{
				float num = (float)Math.Sin(Clamp(_roar / 1.4f, 0f, 1f) * MathF.PI);
				Pose.Jaw = Math.Max(Pose.Jaw, num);
				Pose.HeadPitch += 0.3f * num;
				Pose.NeckCurl += 0.15f * num;
			}
			Pose.BodyPitch = Pitch;
			Pose.BodyRoll = Roll;
			if (Airborne)
			{
				Pose.HeadPitch += Pitch * 0.55f;
				Pose.HeadYaw += Clamp(_turn * 0.35f, -0.4f, 0.4f);
			}
			float num2 = ((_act == Act.Swoop) ? Math.Max(Fire, _base.Glow) : 0f);
			if (num2 > 0.05f)
			{
				Vec3 vec = FireAt - Pos;
				float num3 = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
				if (num3 > 0.5f)
				{
					float v = DeltaAngle(Heading, Atan2(vec.Y, vec.X));
					Pose.HeadYaw += Clamp(v, -0.5f, 0.5f) * num2 * 0.5f;
					Pose.HeadPitch = Pose.HeadPitch * (1f - num2) + num2 * Clamp((float)Math.Atan2(vec.Z - (Pos.Z + H(5.5f)), Math.Max(1f, num3 - H(8f))), -0.95f, 0.1f);
				}
			}
			Pose.Glow = Math.Max(Pose.Glow, Fire);
		}

		private static float Clamp(float v, float lo, float hi)
		{
			if (!(v < lo))
			{
				if (!(v > hi))
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		private static float MoveTowards(float v, float to, float step)
		{
			if (!(Math.Abs(to - v) <= step))
			{
				return v + (float)Math.Sign(to - v) * step;
			}
			return to;
		}

		private static float Smooth(float t)
		{
			t = Clamp(t, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		private static float Frac(float v)
		{
			return v - (float)Math.Floor(v);
		}

		private static float Atan2(float y, float x)
		{
			return (float)Math.Atan2(y, x);
		}

		private static float Wrap(float a)
		{
			if (float.IsNaN(a) || float.IsInfinity(a))
			{
				return 0f;
			}
			return a - MathF.PI * 2f * (float)Math.Floor((a + MathF.PI) / (MathF.PI * 2f));
		}

		private static bool Finite(float v)
		{
			if (!float.IsNaN(v))
			{
				return !float.IsInfinity(v);
			}
			return false;
		}

		private static bool Finite(Vec3 v)
		{
			if (Finite(v.X) && Finite(v.Y))
			{
				return Finite(v.Z);
			}
			return false;
		}

		private static float DeltaAngle(float from, float to)
		{
			return Wrap(to - from);
		}

		private static float Dist2(Vec3 a, Vec3 b)
		{
			float num = a.X - b.X;
			float num2 = a.Y - b.Y;
			return (float)Math.Sqrt(num * num + num2 * num2);
		}
	}
}
