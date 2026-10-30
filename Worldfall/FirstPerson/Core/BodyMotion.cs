using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FirstPerson.Core
{
	public sealed class BodyMotion
	{
		public static class Tune
		{
			public static float LookBase = 0.7f;

			public static float LookSize = 0.45f;

			public static float LookSpeed = 0.35f;

			public static float LookMin = 0.9f;

			public static float LookMax = 3f;

			public static float TurnTime = 0.25f;

			public static float TurnTimeSize = 0.1f;

			public static float TurnRate = 180f;

			public static float TurnRateMin = 80f;

			public static float TurnRateMax = 420f;

			public static float AlignFrom = 30f;

			public static float AlignTo = 120f;

			public static float Accel = 12f;

			public static float Decel = 14f;

			public static float CatchUp = 2.2f;

			public static float CatchUpMost = 0.6f;

			public static float CatchUpAlign = 30f;

			public static float Delay = 0.2f;

			public static float LeashBase = 0.45f;

			public static float LeashSpeed = 0.28f;

			public static float LeashMin = 0.6f;

			public static float LeashMax = 1.6f;

			public static float Still = 0.3f;

			public static float Settle = 0.25f;

			public static float BriskLeash = 0.5f;

			public static float BriskTurn = 1.6f;

			public static float PaceWindow = 0.3f;

			public static float Horizon = 5f;

			public static float Corridor = 0.5f;

			public static float CorridorSize = 0.1f;

			public static float CorridorLeash = 0.6f;

			public static float CorridorOff = 1f;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct FloatBits
		{
			[FieldOffset(0)]
			public float F;

			[FieldOffset(0)]
			public uint U;
		}

		private const int TrailSize = 96;

		private const float Crumb = 0.08f;

		private const float Teleport = 2.5f;

		private const int MaxAhead = 40;

		private const float Probe = 0.25f;

		private const int PaceSlots = 64;

		private readonly float[] _tx = new float[96];

		private readonly float[] _ty = new float[96];

		private int _newest;

		private int _count;

		private readonly float[] _ax = new float[40];

		private readonly float[] _ay = new float[40];

		private int _ahead;

		private float _aheadLen;

		private float _fromX;

		private float _fromY;

		private bool _aheadEnds;

		private readonly float[] _px = new float[138];

		private readonly float[] _py = new float[138];

		private readonly float[] _ps = new float[138];

		private int _pn;

		private readonly float[] _bx = new float[97];

		private readonly float[] _by = new float[97];

		private readonly float[] _bs = new float[97];

		private readonly float[] _hx = new float[64];

		private readonly float[] _hy = new float[64];

		private readonly float[] _ht = new float[64];

		private int _hi;

		private int _hn;

		private float _clock;

		private float _lastX;

		private float _lastY;

		private float _turnRate;

		private bool _have;

		public float X;

		public float Y;

		public float Facing;

		public float Speed;

		public float RealSpeed;

		public float Gait;

		public float Lag;

		public float AimX;

		public float AimY;

		public bool Settled;

		public bool Dragged;

		public bool Kept;

		private const float Rad = 57.29578f;

		private const float Deg2Rad = MathF.PI / 180f;

		private static bool _fast = true;

		private static int _epoch = 1;

		private int _madeIn;

		private readonly float[] _gap = new float[96];

		private int _fresh;

		private int _trail;

		private bool _routeOk;

		private int _route;

		private int _rTrail;

		private int _rAhead;

		private uint _rLastX;

		private uint _rLastY;

		private uint _rSpeed;

		private uint _rGait;

		private uint _rLeash0;

		private uint _rLeash1;

		private uint _rLeash2;

		private uint _rLeash3;

		private readonly uint[] _rAx = new uint[40];

		private readonly uint[] _rAy = new uint[40];

		private int _sRoute = -1;

		private int _sSeg;

		private uint _sX;

		private uint _sY;

		private float _sExpect;

		private float _sAt;

		private float _sT;

		private float _sOff2;

		private double _sExpectWide;

		public static long RoutesKept;

		public static long PlacesKept;

		private static uint _expDt;

		private static bool _expKnown;

		private static float _exp15;

		private static float _exp4;

		private static float _exp10;

		public static bool Verify;

		public static long Verified;

		public static long Mismatched;

		public static string FirstMismatch;

		private BodyMotion _shadow;

		public bool Placed => _have;

		public static bool Fast
		{
			get
			{
				return _fast;
			}
			set
			{
				if (value != _fast)
				{
					_epoch++;
				}
				_fast = value;
			}
		}

		public void Reset(float x, float y, float facing)
		{
			_newest = 0;
			_count = 1;
			_tx[0] = x;
			_ty[0] = y;
			_hi = 0;
			_hn = 1;
			_hx[0] = x;
			_hy[0] = y;
			_ht[0] = _clock;
			_lastX = (X = (AimX = x));
			_lastY = (Y = (AimY = y));
			Facing = facing;
			Speed = (RealSpeed = (Lag = 0f));
			_turnRate = 0f;
			_have = true;
			_trail++;
		}

		public void BeginAhead(float x, float y)
		{
			_ahead = 0;
			_aheadLen = 0f;
			_aheadEnds = true;
			_fromX = x;
			_fromY = y;
		}

		public bool AddAhead(float x, float y, float enough, Func<float, float, bool> solid = null)
		{
			if (_ahead >= 40 || _aheadLen >= enough)
			{
				_aheadEnds = false;
				return false;
			}
			float num = ((_ahead > 0) ? _ax[_ahead - 1] : _fromX);
			float num2 = ((_ahead > 0) ? _ay[_ahead - 1] : _fromY);
			float num3 = Dist(x - num, y - num2);
			if (num3 < 0.0001f)
			{
				return true;
			}
			if (solid != null)
			{
				float num4 = Math.Min(num3, enough - _aheadLen + 0.25f);
				float num5 = 0.25f;
				while (true)
				{
					bool flag = num5 >= num4;
					if (flag)
					{
						num5 = num4;
					}
					float num6 = num5 / num3;
					if (solid(num + (x - num) * num6, num2 + (y - num2) * num6))
					{
						float num7 = Math.Max(0f, num5 - 0.25f) / num3;
						if (num7 > 0f)
						{
							Push(num + (x - num) * num7, num2 + (y - num2) * num7, num3 * num7);
						}
						_aheadEnds = false;
						return false;
					}
					if (flag)
					{
						break;
					}
					num5 += 0.25f;
				}
			}
			Push(x, y, num3);
			return true;
		}

		private void Push(float x, float y, float d)
		{
			_ax[_ahead] = x;
			_ay[_ahead] = y;
			_ahead++;
			_aheadLen += d;
		}

		public void AheadGoesOn()
		{
			_aheadEnds = false;
		}

		public float AheadWanted(float size)
		{
			return Math.Max(Look(Math.Max(0.2f, size)) + 0.5f, Tune.Horizon);
		}

		public void Step(float rx, float ry, float dt, float size, bool follow, bool face, Func<float, float, bool> blocked = null, bool brisk = false)
		{
			if (!_fast)
			{
				StepOld(rx, ry, dt, size, follow, face, blocked, brisk);
			}
			else if (Verify)
			{
				StepVerified(rx, ry, dt, size, follow, face, blocked, brisk);
			}
			else
			{
				StepFast(rx, ry, dt, size, follow, face, blocked, brisk);
			}
		}

		private void StepOld(float rx, float ry, float dt, float size, bool follow, bool face, Func<float, float, bool> blocked, bool brisk)
		{
			if (!_have)
			{
				Reset(rx, ry, Facing);
			}
			Settled = (Dragged = (Kept = false));
			float num = Dist(rx - _lastX, ry - _lastY);
			if (num > 2.5f)
			{
				Reset(rx, ry, Facing);
				num = 0f;
			}
			float num2 = rx - _lastX;
			float num3 = ry - _lastY;
			_lastX = rx;
			_lastY = ry;
			if (Dist(rx - _tx[_newest], ry - _ty[_newest]) >= 0.08f)
			{
				_newest = (_newest + 1) % 96;
				_tx[_newest] = rx;
				_ty[_newest] = ry;
				if (_count < 96)
				{
					_count++;
				}
			}
			if (dt <= 0f)
			{
				return;
			}
			_clock += dt;
			_hi = (_hi + 1) % 64;
			_hx[_hi] = rx;
			_hy[_hi] = ry;
			_ht[_hi] = _clock;
			if (_hn < 64)
			{
				_hn++;
			}
			RealSpeed = Pace();
			if (RealSpeed > Tune.Still)
			{
				Gait = ((Gait <= 0f) ? RealSpeed : (Gait + (RealSpeed - Gait) * (1f - (float)Math.Exp(-1.5f * dt))));
			}
			size = Math.Max(0.2f, size);
			if (!follow)
			{
				X = (AimX = rx);
				Y = (AimY = ry);
				Speed = num / dt;
				Lag = 0f;
				if (face && num > 0.01f)
				{
					Facing = Turn.Damp(Facing, ref _turnRate, (float)Math.Atan2(num3, num2), dt, TurnTime(size), TurnRate(size));
				}
				return;
			}
			BuildRoute();
			float num4 = Lag + num - Speed * dt;
			int seg = 0;
			float num5 = float.MaxValue;
			float num6 = 0f;
			float t = 0f;
			float num7 = 0f;
			for (int i = 0; i + 1 < _pn; i++)
			{
				float num8 = _px[i];
				float num9 = _py[i];
				float num10 = _px[i + 1] - num8;
				float num11 = _py[i + 1] - num9;
				float num12 = num10 * num10 + num11 * num11;
				float num13 = ((num12 > 1E-08f) ? Clamp01(((X - num8) * num10 + (Y - num9) * num11) / num12) : 0f);
				float num14 = X - (num8 + num10 * num13);
				float num15 = Y - (num9 + num11 * num13);
				float num16 = _ps[i] + (_ps[i + 1] - _ps[i]) * num13;
				float num17 = 0f - num16 - num4;
				float num18 = num14 * num14 + num15 * num15 + 0.25f * num17 * num17;
				if (num18 < num5)
				{
					num5 = num18;
					seg = i;
					t = num13;
					num6 = num16;
					num7 = num14 * num14 + num15 * num15;
				}
			}
			if (_pn < 2)
			{
				num6 = 0f;
			}
			Lag = 0f - num6;
			float val = _ps[_pn - 1] - num6;
			bool flag = RealSpeed < Tune.Still;
			float num19 = Dist(rx - X, ry - Y);
			float num20 = ((num19 > 0.0001f) ? (Math.Abs(Turn.Wrap((float)Math.Atan2(ry - Y, rx - X) - Facing)) * 57.29578f) : 0f);
			if (flag && (num19 < Tune.Settle || (num19 < 2f * Tune.Settle && num20 > 60f)))
			{
				float num21 = 1f - (float)Math.Exp(-4f * dt);
				X += (rx - X) * num21;
				Y += (ry - Y) * num21;
				Speed = Math.Max(0f, Speed - Tune.Decel * dt);
				_turnRate *= (float)Math.Exp(-10f * dt);
				AimX = rx;
				AimY = ry;
				Settled = true;
				return;
			}
			float num22 = (flag ? Math.Max(Gait, 1f) : Math.Max(RealSpeed, 0.6f * Gait));
			float num23 = Look(size);
			if (flag)
			{
				AimX = rx;
				AimY = ry;
			}
			else
			{
				if (!Reach(seg, t, num23, out AimX, out AimY, out var s))
				{
					Along(seg, s = num6 + num23, out AimX, out AimY);
				}
				float num24 = Corridor(size) - Tune.CorridorOff * (float)Math.Sqrt(num7);
				if (!brisk && Tune.Horizon > 0f && num24 > 0.05f && Straighten(seg, num6, s, num24, blocked, out var x, out var y))
				{
					AimX = x;
					AimY = y;
				}
			}
			float num25 = AimX - X;
			float num26 = AimY - Y;
			float num27 = 0f;
			if (Dist(num25, num26) > 0.05f)
			{
				float num28 = (float)Math.Atan2(num26, num25);
				float num29 = (brisk ? Tune.BriskTurn : 1f);
				Facing = Turn.Damp(Facing, ref _turnRate, num28, dt, TurnTime(size) / num29, TurnRate(size) * num29);
				num27 = Math.Abs(Turn.Wrap(num28 - Facing)) * 57.29578f;
			}
			float num30;
			if (flag)
			{
				num30 = Math.Min(num22, (float)Math.Sqrt(2f * Tune.Decel * Math.Max(0f, num19 - 0.5f * Tune.Settle)));
			}
			else
			{
				float num31 = (brisk ? 0f : Math.Min(Math.Max(0f, num23 - _aheadLen), Tune.Delay * RealSpeed));
				float num32 = Tune.CatchUp * (Lag - num31);
				if (num32 > 0f)
				{
					num32 *= Clamp01(1f - num27 / Tune.CatchUpAlign);
				}
				num30 = Math.Max(0f, Math.Min(num22 + num32, num22 * (1f + Tune.CatchUpMost) + 0.3f));
				if (_aheadEnds)
				{
					num30 = Math.Min(num30, (float)Math.Sqrt(2f * Tune.Decel * Math.Max(0f, val)));
				}
			}
			num30 *= Align(num27);
			if (num30 > Speed)
			{
				Speed = Math.Min(num30, Speed + Tune.Accel * dt);
			}
			else
			{
				Speed = Math.Max(num30, Speed - Tune.Decel * dt);
			}
			float num33 = X + (float)Math.Cos(Facing) * Speed * dt;
			float num34 = Y + (float)Math.Sin(Facing) * Speed * dt;
			if (blocked != null && Speed > 0f && num19 > 0.001f && blocked(num33, num34) && !blocked(X, Y))
			{
				float num35 = Math.Min(num19, Speed * dt) / num19;
				num33 = X + (rx - X) * num35;
				num34 = Y + (ry - Y) * num35;
				Kept = true;
			}
			X = num33;
			Y = num34;
			float num36 = (brisk ? Math.Min(Leash(), Tune.BriskLeash) : Leash());
			float num37 = X - rx;
			float num38 = Y - ry;
			float num39 = Dist(num37, num38);
			if (num39 > num36)
			{
				X = rx + num37 * num36 / num39;
				Y = ry + num38 * num36 / num39;
				Dragged = true;
			}
		}

		private float Pace()
		{
			for (int i = 1; i < _hn; i++)
			{
				int num = (_hi - i + 64) % 64;
				float num2 = _clock - _ht[num];
				if (num2 >= Tune.PaceWindow || i == _hn - 1)
				{
					if (!(num2 > 0.0001f))
					{
						return 0f;
					}
					return Dist(_hx[_hi] - _hx[num], _hy[_hi] - _hy[num]) / num2;
				}
			}
			return 0f;
		}

		private void BuildRoute()
		{
			float num = Leash() * 2f + 1f;
			float num2 = 0f;
			float num3 = _lastX;
			float num4 = _lastY;
			int num5 = 0;
			_bx[num5] = num3;
			_by[num5] = num4;
			_bs[num5++] = 0f;
			for (int i = 0; i < _count; i++)
			{
				if (!(num2 < num))
				{
					break;
				}
				int num6 = (_newest - i + 96) % 96;
				float num7 = Dist(_tx[num6] - num3, _ty[num6] - num4);
				if (!(num7 < 0.0001f))
				{
					num2 += num7;
					num3 = _tx[num6];
					num4 = _ty[num6];
					_bx[num5] = num3;
					_by[num5] = num4;
					_bs[num5++] = 0f - num2;
				}
			}
			_pn = 0;
			for (int num8 = num5 - 1; num8 >= 0; num8--)
			{
				_px[_pn] = _bx[num8];
				_py[_pn] = _by[num8];
				_ps[_pn++] = _bs[num8];
			}
			float num9 = 0f;
			num3 = _lastX;
			num4 = _lastY;
			for (int j = 0; j < _ahead; j++)
			{
				float num10 = Dist(_ax[j] - num3, _ay[j] - num4);
				if (!(num10 < 0.0001f))
				{
					num9 += num10;
					num3 = _ax[j];
					num4 = _ay[j];
					_px[_pn] = num3;
					_py[_pn] = num4;
					_ps[_pn++] = num9;
				}
			}
		}

		private bool Reach(int seg, float t, float reach, out float x, out float y, out float s)
		{
			x = X;
			y = Y;
			s = 0f;
			bool result = false;
			float num = reach * reach;
			for (int i = Math.Max(0, seg); i + 1 < _pn; i++)
			{
				float num2 = _px[i];
				float num3 = _py[i];
				float num4 = _px[i + 1] - num2;
				float num5 = _py[i + 1] - num3;
				float num6 = num2 - X;
				float num7 = num3 - Y;
				float num8 = num4 * num4 + num5 * num5;
				float num9 = ((i == seg) ? t : 0f);
				float num10;
				if ((_px[i + 1] - X) * (_px[i + 1] - X) + (_py[i + 1] - Y) * (_py[i + 1] - Y) <= num)
				{
					num10 = 1f;
				}
				else
				{
					if (num8 < 1E-10f)
					{
						continue;
					}
					float num11 = num6 * num4 + num7 * num5;
					float num12 = num6 * num6 + num7 * num7 - num;
					float num13 = num11 * num11 - num8 * num12;
					if (num13 < 0f)
					{
						continue;
					}
					num10 = (0f - num11 + (float)Math.Sqrt(num13)) / num8;
					if (num10 < num9 || num10 > 1f)
					{
						continue;
					}
				}
				if (!(num10 < num9))
				{
					x = num2 + num4 * num10;
					y = num3 + num5 * num10;
					s = _ps[i] + (_ps[i + 1] - _ps[i]) * num10;
					result = true;
				}
			}
			return result;
		}

		private bool Straighten(int seg, float at, float beyond, float corridor, Func<float, float, bool> blocked, out float x, out float y)
		{
			x = X;
			y = Y;
			float num = at + Tune.Horizon;
			float num2 = at;
			int num3 = seg + 1;
			for (int i = num3; i < _pn; i++)
			{
				float num4 = 1f;
				bool flag = _ps[i] >= num || i == _pn - 1;
				if (_ps[i] > num)
				{
					float num5 = _ps[i] - _ps[i - 1];
					num4 = ((num5 > 1E-06f) ? Clamp01((num - _ps[i - 1]) / num5) : 1f);
				}
				float num6 = _px[i - 1] + (_px[i] - _px[i - 1]) * num4;
				float num7 = _py[i - 1] + (_py[i] - _py[i - 1]) * num4;
				if (!InLine(num3, i, num6, num7, corridor))
				{
					float num8 = 0f;
					float num9 = num4;
					for (int j = 0; j < 6; j++)
					{
						float num10 = 0.5f * (num8 + num9);
						if (InLine(num3, i, _px[i - 1] + (_px[i] - _px[i - 1]) * num10, _py[i - 1] + (_py[i] - _py[i - 1]) * num10, corridor))
						{
							num8 = num10;
						}
						else
						{
							num9 = num10;
						}
					}
					if (num8 > 0f)
					{
						x = _px[i - 1] + (_px[i] - _px[i - 1]) * num8;
						y = _py[i - 1] + (_py[i] - _py[i - 1]) * num8;
						num2 = _ps[i - 1] + (_ps[i] - _ps[i - 1]) * num8;
					}
					break;
				}
				x = num6;
				y = num7;
				num2 = _ps[i - 1] + (_ps[i] - _ps[i - 1]) * num4;
				if (flag)
				{
					break;
				}
			}
			if (num2 <= beyond + 0.05f)
			{
				return false;
			}
			if (blocked == null || blocked(X, Y) || Clear(x, y, blocked))
			{
				return true;
			}
			float num11 = beyond;
			float num12 = num2;
			bool flag2 = false;
			for (int k = 0; k < 5; k++)
			{
				float num13 = 0.5f * (num11 + num12);
				Along(seg, num13, out var x2, out var y2);
				if (Clear(x2, y2, blocked))
				{
					num11 = num13;
					x = x2;
					y = y2;
					flag2 = true;
				}
				else
				{
					num12 = num13;
				}
			}
			if (flag2)
			{
				return num11 > beyond + 0.05f;
			}
			return false;
		}

		private bool InLine(int from, int to, float ex, float ey, float corridor)
		{
			float num = ex - X;
			float num2 = ey - Y;
			float num3 = num * num + num2 * num2;
			float num4 = corridor * corridor;
			for (int i = from; i < to; i++)
			{
				float num5 = _px[i] - X;
				float num6 = _py[i] - Y;
				float num7 = ((num3 > 1E-08f) ? Clamp01((num5 * num + num6 * num2) / num3) : 0f);
				float num8 = num5 - num * num7;
				float num9 = num6 - num2 * num7;
				if (num8 * num8 + num9 * num9 > num4)
				{
					return false;
				}
			}
			return true;
		}

		private bool Clear(float ex, float ey, Func<float, float, bool> blocked)
		{
			float num = Dist(ex - X, ey - Y);
			for (float num2 = 0.25f; num2 < num; num2 += 0.25f)
			{
				if (blocked(X + (ex - X) * num2 / num, Y + (ey - Y) * num2 / num))
				{
					return false;
				}
			}
			return !blocked(ex, ey);
		}

		private void Along(int seg, float s, out float x, out float y)
		{
			for (int i = Math.Max(0, seg); i + 1 < _pn; i++)
			{
				if (!(_ps[i + 1] < s))
				{
					float num = _ps[i + 1] - _ps[i];
					float num2 = ((num > 1E-06f) ? Clamp01((s - _ps[i]) / num) : 1f);
					x = _px[i] + (_px[i + 1] - _px[i]) * num2;
					y = _py[i] + (_py[i + 1] - _py[i]) * num2;
					return;
				}
			}
			x = _px[_pn - 1];
			y = _py[_pn - 1];
		}

		private float Look(float size)
		{
			return Clamp(Tune.LookBase + Tune.LookSize * size + Tune.LookSpeed * Math.Max(RealSpeed, Gait * 0.5f), Tune.LookMin, Tune.LookMax);
		}

		private float Leash()
		{
			return Clamp(Tune.LeashBase + Tune.LeashSpeed * Math.Max(RealSpeed, Gait), Tune.LeashMin, Tune.LeashMax);
		}

		private float Corridor(float size)
		{
			return Math.Min(Tune.Corridor + Tune.CorridorSize * size, Tune.CorridorLeash * Leash());
		}

		private static float TurnTime(float size)
		{
			return Clamp(Tune.TurnTime + Tune.TurnTimeSize * size, 0.15f, 0.7f);
		}

		private static float TurnRate(float size)
		{
			return Clamp(Tune.TurnRate / (float)Math.Sqrt(size), Tune.TurnRateMin, Tune.TurnRateMax) * (MathF.PI / 180f);
		}

		private static float Align(float err)
		{
			if (err <= Tune.AlignFrom)
			{
				return 1f;
			}
			if (err >= Tune.AlignTo)
			{
				return 0f;
			}
			float num = (err - Tune.AlignFrom) / (Tune.AlignTo - Tune.AlignFrom);
			return 1f - num * num * (3f - 2f * num);
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

		private static float Clamp01(float v)
		{
			if (!(v < 0f))
			{
				if (!(v > 1f))
				{
					return v;
				}
				return 1f;
			}
			return 0f;
		}

		private static float Dist(float dx, float dy)
		{
			return (float)Math.Sqrt(dx * dx + dy * dy);
		}

		private void StepFast(float rx, float ry, float dt, float size, bool follow, bool face, Func<float, float, bool> blocked, bool brisk)
		{
			if (_madeIn != _epoch)
			{
				Remake();
			}
			if (!_have)
			{
				Reset(rx, ry, Facing);
			}
			Settled = (Dragged = (Kept = false));
			float num = Dist(rx - _lastX, ry - _lastY);
			if (num > 2.5f)
			{
				Reset(rx, ry, Facing);
				num = 0f;
			}
			float num2 = rx - _lastX;
			float num3 = ry - _lastY;
			_lastX = rx;
			_lastY = ry;
			if (Dist(rx - _tx[_newest], ry - _ty[_newest]) >= 0.08f)
			{
				int newest = _newest;
				_newest = (_newest + 1) % 96;
				_tx[_newest] = rx;
				_ty[_newest] = ry;
				if (_count < 96)
				{
					_count++;
				}
				_gap[_newest] = Dist(_tx[newest] - _tx[_newest], _ty[newest] - _ty[_newest]);
				if (_fresh < 96)
				{
					_fresh++;
				}
				_trail++;
			}
			if (dt <= 0f)
			{
				return;
			}
			_clock += dt;
			_hi = (_hi + 1) % 64;
			_hx[_hi] = rx;
			_hy[_hi] = ry;
			_ht[_hi] = _clock;
			if (_hn < 64)
			{
				_hn++;
			}
			RealSpeed = PaceFast();
			if (Bits(dt) != _expDt || !_expKnown)
			{
				Exps(dt);
			}
			if (RealSpeed > Tune.Still)
			{
				Gait = ((Gait <= 0f) ? RealSpeed : (Gait + (RealSpeed - Gait) * (1f - _exp15)));
			}
			size = Math.Max(0.2f, size);
			if (!follow)
			{
				X = (AimX = rx);
				Y = (AimY = ry);
				Speed = num / dt;
				Lag = 0f;
				if (face && num > 0.01f)
				{
					Facing = Turn.Damp(Facing, ref _turnRate, (float)Math.Atan2(num3, num2), dt, TurnTime(size), TurnRate(size));
				}
				return;
			}
			if (!RouteKept())
			{
				BuildRouteFast();
				KeepRoute();
			}
			else if (Verify)
			{
				RoutesKept++;
			}
			float num4 = Lag + num - Speed * dt;
			int num5 = 0;
			float num6 = float.MaxValue;
			float num7 = 0f;
			float num8 = 0f;
			float num9 = 0f;
			if (RealSpeed < Tune.Still && _sRoute == _route && _sX == Bits(X) && _sY == Bits(Y) && (double)num4 == _sExpectWide && Bits(num4) == Bits(_sExpect))
			{
				if (Verify)
				{
					PlacesKept++;
				}
				num5 = _sSeg;
				num7 = _sAt;
				num8 = _sT;
				num9 = _sOff2;
			}
			else
			{
				double num10 = X;
				double num11 = Y;
				double num12 = Guess(num4);
				double num13 = 0.001 + 1E-06 * (Math.Abs(num10) + Math.Abs(num11));
				int from = 0;
				int to = _pn - 2;
				if (num12 < double.PositiveInfinity)
				{
					Window(num4, num12, ref from, ref to);
				}
				for (int i = from; i <= to; i++)
				{
					double num14 = _px[i];
					double num15 = _px[i + 1];
					double num16 = _py[i];
					double num17 = _py[i + 1];
					if (num15 < num14)
					{
						double num18 = num14;
						num14 = num15;
						num15 = num18;
					}
					if (num17 < num16)
					{
						double num19 = num16;
						num16 = num17;
						num17 = num19;
					}
					double num20 = ((num14 - num10 > num10 - num15) ? (num14 - num10) : (num10 - num15));
					double num21 = ((num16 - num11 > num11 - num17) ? (num16 - num11) : (num11 - num17));
					num20 -= num13;
					num21 -= num13;
					if (num20 > 0.0 || num21 > 0.0)
					{
						double num22 = ((num20 > 0.0) ? (num20 * num20) : 0.0) + ((num21 > 0.0) ? (num21 * num21) : 0.0);
						if (num22 > num12 || num22 > (double)num6)
						{
							continue;
						}
					}
					float num23 = _px[i];
					float num24 = _py[i];
					float num25 = _px[i + 1] - num23;
					float num26 = _py[i + 1] - num24;
					float num27 = num25 * num25 + num26 * num26;
					float num28 = ((num27 > 1E-08f) ? Clamp01(((X - num23) * num25 + (Y - num24) * num26) / num27) : 0f);
					float num29 = X - (num23 + num25 * num28);
					float num30 = Y - (num24 + num26 * num28);
					float num31 = _ps[i] + (_ps[i + 1] - _ps[i]) * num28;
					float num32 = 0f - num31 - num4;
					float num33 = num29 * num29 + num30 * num30 + 0.25f * num32 * num32;
					if (num33 < num6)
					{
						num6 = num33;
						num5 = i;
						num8 = num28;
						num7 = num31;
						num9 = num29 * num29 + num30 * num30;
					}
				}
				_sRoute = _route;
				_sX = Bits(X);
				_sY = Bits(Y);
				_sExpect = num4;
				_sExpectWide = num4;
				_sSeg = num5;
				_sAt = num7;
				_sT = num8;
				_sOff2 = num9;
			}
			if (_pn < 2)
			{
				num7 = 0f;
			}
			Lag = 0f - num7;
			float val = _ps[_pn - 1] - num7;
			bool flag = RealSpeed < Tune.Still;
			float num34 = Dist(rx - X, ry - Y);
			float num35 = ((num34 > 0.0001f) ? (Math.Abs(Turn.Wrap((float)Math.Atan2(ry - Y, rx - X) - Facing)) * 57.29578f) : 0f);
			if (flag && (num34 < Tune.Settle || (num34 < 2f * Tune.Settle && num35 > 60f)))
			{
				float num36 = 1f - _exp4;
				X += (rx - X) * num36;
				Y += (ry - Y) * num36;
				Speed = Math.Max(0f, Speed - Tune.Decel * dt);
				_turnRate *= _exp10;
				AimX = rx;
				AimY = ry;
				Settled = true;
				return;
			}
			float num37 = (flag ? Math.Max(Gait, 1f) : Math.Max(RealSpeed, 0.6f * Gait));
			float num38 = Look(size);
			if (flag)
			{
				AimX = rx;
				AimY = ry;
			}
			else
			{
				if (!Reach(num5, num8, num38, out AimX, out AimY, out var s))
				{
					Along(num5, s = num7 + num38, out AimX, out AimY);
				}
				float num39 = Corridor(size) - Tune.CorridorOff * (float)Math.Sqrt(num9);
				if (!brisk && Tune.Horizon > 0f && num39 > 0.05f && Straighten(num5, num7, s, num39, blocked, out var x, out var y))
				{
					AimX = x;
					AimY = y;
				}
			}
			float num40 = AimX - X;
			float num41 = AimY - Y;
			float num42 = 0f;
			if (Dist(num40, num41) > 0.05f)
			{
				float num43 = (float)Math.Atan2(num41, num40);
				float num44 = (brisk ? Tune.BriskTurn : 1f);
				Facing = Turn.Damp(Facing, ref _turnRate, num43, dt, TurnTime(size) / num44, TurnRate(size) * num44);
				num42 = Math.Abs(Turn.Wrap(num43 - Facing)) * 57.29578f;
			}
			float num45;
			if (flag)
			{
				num45 = Math.Min(num37, (float)Math.Sqrt(2f * Tune.Decel * Math.Max(0f, num34 - 0.5f * Tune.Settle)));
			}
			else
			{
				float num46 = (brisk ? 0f : Math.Min(Math.Max(0f, num38 - _aheadLen), Tune.Delay * RealSpeed));
				float num47 = Tune.CatchUp * (Lag - num46);
				if (num47 > 0f)
				{
					num47 *= Clamp01(1f - num42 / Tune.CatchUpAlign);
				}
				num45 = Math.Max(0f, Math.Min(num37 + num47, num37 * (1f + Tune.CatchUpMost) + 0.3f));
				if (_aheadEnds)
				{
					num45 = Math.Min(num45, (float)Math.Sqrt(2f * Tune.Decel * Math.Max(0f, val)));
				}
			}
			num45 *= Align(num42);
			if (num45 > Speed)
			{
				Speed = Math.Min(num45, Speed + Tune.Accel * dt);
			}
			else
			{
				Speed = Math.Max(num45, Speed - Tune.Decel * dt);
			}
			float num48 = X + (float)Math.Cos(Facing) * Speed * dt;
			float num49 = Y + (float)Math.Sin(Facing) * Speed * dt;
			if (blocked != null && Speed > 0f && num34 > 0.001f && blocked(num48, num49) && !blocked(X, Y))
			{
				float num50 = Math.Min(num34, Speed * dt) / num34;
				num48 = X + (rx - X) * num50;
				num49 = Y + (ry - Y) * num50;
				Kept = true;
			}
			X = num48;
			Y = num49;
			float num51 = (brisk ? Math.Min(Leash(), Tune.BriskLeash) : Leash());
			float num52 = X - rx;
			float num53 = Y - ry;
			float num54 = Dist(num52, num53);
			if (num54 > num51)
			{
				X = rx + num52 * num51 / num54;
				Y = ry + num53 * num51 / num54;
				Dragged = true;
			}
		}

		private double Guess(float expect)
		{
			if (_pn < 2)
			{
				return double.PositiveInfinity;
			}
			double num = 0.0 - (double)expect;
			int num2 = 0;
			int num3 = _pn - 2;
			while (num2 < num3)
			{
				int num4 = num2 + num3 + 1 >> 1;
				if ((double)_ps[num4] <= num)
				{
					num2 = num4;
				}
				else
				{
					num3 = num4 - 1;
				}
			}
			double num5 = _px[num2];
			double num6 = _py[num2];
			double num7 = (double)_px[num2 + 1] - num5;
			double num8 = (double)_py[num2 + 1] - num6;
			double num9 = X;
			double num10 = Y;
			double num11 = num7 * num7 + num8 * num8;
			double num12 = 0.0;
			if (num11 > 1E-08)
			{
				num12 = ((num9 - num5) * num7 + (num10 - num6) * num8) / num11;
				num12 = ((num12 < 0.0) ? 0.0 : ((num12 > 1.0) ? 1.0 : num12));
			}
			double num13 = num9 - (num5 + num7 * num12);
			double num14 = num10 - (num6 + num8 * num12);
			double num15 = 0.0 - ((double)_ps[num2] + ((double)_ps[num2 + 1] - (double)_ps[num2]) * num12) - (double)expect;
			double num16 = num13 * num13 + num14 * num14 + 0.25 * num15 * num15;
			num16 += 0.0001 * (num16 + 3.0);
			if (!(num16 >= 0.0))
			{
				return double.PositiveInfinity;
			}
			return num16;
		}

		private void Window(float expect, double most, ref int from, ref int to)
		{
			if (_pn < 2)
			{
				return;
			}
			double num = 0.0 - (double)expect;
			double d = _ps[0];
			double d2 = _ps[_pn - 1];
			if (double.IsNaN(num) || double.IsInfinity(num) || double.IsNaN(d) || double.IsInfinity(d) || double.IsNaN(d2) || double.IsInfinity(d2))
			{
				return;
			}
			double num2 = 2.0 * Math.Sqrt(most) * 1.00001 + 1E-05;
			double num3 = num - num2;
			double num4 = num + num2;
			int num5 = 0;
			int num6 = _pn - 1;
			while (num5 < num6)
			{
				int num7 = num5 + num6 >> 1;
				if ((double)_ps[num7 + 1] >= num3)
				{
					num6 = num7;
				}
				else
				{
					num5 = num7 + 1;
				}
			}
			from = num5;
			num5 = -1;
			num6 = _pn - 2;
			while (num5 < num6)
			{
				int num8 = num5 + num6 + 1 >> 1;
				if ((double)_ps[num8] <= num4)
				{
					num5 = num8;
				}
				else
				{
					num6 = num8 - 1;
				}
			}
			to = num5;
		}

		private static void Exps(float dt)
		{
			_expKnown = true;
			_expDt = Bits(dt);
			_exp15 = (float)Math.Exp(-1.5f * dt);
			_exp4 = (float)Math.Exp(-4f * dt);
			_exp10 = (float)Math.Exp(-10f * dt);
		}

		private bool RouteKept()
		{
			if (!_routeOk || _rTrail != _trail || _rAhead != _ahead || _rLastX != Bits(_lastX) || _rLastY != Bits(_lastY) || _rSpeed != Bits(RealSpeed) || _rGait != Bits(Gait) || _rLeash0 != Bits(Tune.LeashBase) || _rLeash1 != Bits(Tune.LeashSpeed) || _rLeash2 != Bits(Tune.LeashMin) || _rLeash3 != Bits(Tune.LeashMax))
			{
				return false;
			}
			for (int i = 0; i < _ahead; i++)
			{
				if (_rAx[i] != Bits(_ax[i]) || _rAy[i] != Bits(_ay[i]))
				{
					return false;
				}
			}
			return true;
		}

		private void KeepRoute()
		{
			_routeOk = true;
			_route++;
			_rTrail = _trail;
			_rAhead = _ahead;
			_rLastX = Bits(_lastX);
			_rLastY = Bits(_lastY);
			_rSpeed = Bits(RealSpeed);
			_rGait = Bits(Gait);
			_rLeash0 = Bits(Tune.LeashBase);
			_rLeash1 = Bits(Tune.LeashSpeed);
			_rLeash2 = Bits(Tune.LeashMin);
			_rLeash3 = Bits(Tune.LeashMax);
			for (int i = 0; i < _ahead; i++)
			{
				_rAx[i] = Bits(_ax[i]);
				_rAy[i] = Bits(_ay[i]);
			}
		}

		private void Remake()
		{
			_madeIn = _epoch;
			_routeOk = false;
			_sRoute = -1;
			_fresh = 0;
		}

		private float PaceFast()
		{
			int num = _hi;
			for (int i = 1; i < _hn; i++)
			{
				num = ((num == 0) ? 63 : (num - 1));
				float num2 = _clock - _ht[num];
				if (num2 >= Tune.PaceWindow || i == _hn - 1)
				{
					if (!(num2 > 0.0001f))
					{
						return 0f;
					}
					return Dist(_hx[_hi] - _hx[num], _hy[_hi] - _hy[num]) / num2;
				}
			}
			return 0f;
		}

		private void BuildRouteFast()
		{
			float num = Leash() * 2f + 1f;
			float num2 = 0f;
			float num3 = _lastX;
			float num4 = _lastY;
			int num5 = 0;
			_px[96] = num3;
			_py[96] = num4;
			_ps[96] = 0f;
			num5++;
			int num6 = _newest;
			int num7 = -1;
			for (int i = 0; i < _count; i++)
			{
				if (!(num2 < num))
				{
					break;
				}
				float num8 = ((num7 >= 0 && i <= _fresh) ? _gap[num7] : Dist(_tx[num6] - num3, _ty[num6] - num4));
				if (num8 < 0.0001f)
				{
					num7 = -1;
					num6 = ((num6 == 0) ? 95 : (num6 - 1));
					continue;
				}
				num2 += num8;
				num3 = _tx[num6];
				num4 = _ty[num6];
				_px[96 - num5] = num3;
				_py[96 - num5] = num4;
				_ps[96 - num5] = 0f - num2;
				num5++;
				num7 = num6;
				num6 = ((num6 == 0) ? 95 : (num6 - 1));
			}
			Array.Copy(_px, 96 - num5 + 1, _px, 0, num5);
			Array.Copy(_py, 96 - num5 + 1, _py, 0, num5);
			Array.Copy(_ps, 96 - num5 + 1, _ps, 0, num5);
			_pn = num5;
			float num9 = 0f;
			num3 = _lastX;
			num4 = _lastY;
			for (int j = 0; j < _ahead; j++)
			{
				float num10 = Dist(_ax[j] - num3, _ay[j] - num4);
				if (!(num10 < 0.0001f))
				{
					num9 += num10;
					num3 = _ax[j];
					num4 = _ay[j];
					_px[_pn] = num3;
					_py[_pn] = num4;
					_ps[_pn++] = num9;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint Bits(float f)
		{
			FloatBits floatBits = new FloatBits
			{
				F = f
			};
			return floatBits.U;
		}

		private void StepVerified(float rx, float ry, float dt, float size, bool follow, bool face, Func<float, float, bool> blocked, bool brisk)
		{
			BodyMotion bodyMotion = _shadow ?? (_shadow = new BodyMotion());
			CopyTo(bodyMotion);
			StepFast(rx, ry, dt, size, follow, face, blocked, brisk);
			bodyMotion.StepOld(rx, ry, dt, size, follow, face, blocked, brisk);
			Verified++;
			string text = Differs(bodyMotion);
			if (text != null)
			{
				Mismatched++;
				if (FirstMismatch == null)
				{
					FirstMismatch = text + $" (step to {rx:R},{ry:R} dt {dt:R} size {size:R} follow {follow} face {face} brisk {brisk} blocked {blocked != null}; trail {_count}, route {_pn}, ahead {_ahead})";
				}
			}
		}

		private void CopyTo(BodyMotion o)
		{
			Array.Copy(_tx, o._tx, 96);
			Array.Copy(_ty, o._ty, 96);
			Array.Copy(_ax, o._ax, 40);
			Array.Copy(_ay, o._ay, 40);
			Array.Copy(_px, o._px, _px.Length);
			Array.Copy(_py, o._py, _py.Length);
			Array.Copy(_ps, o._ps, _ps.Length);
			Array.Copy(_bx, o._bx, _bx.Length);
			Array.Copy(_by, o._by, _by.Length);
			Array.Copy(_bs, o._bs, _bs.Length);
			Array.Copy(_hx, o._hx, 64);
			Array.Copy(_hy, o._hy, 64);
			Array.Copy(_ht, o._ht, 64);
			o._newest = _newest;
			o._count = _count;
			o._ahead = _ahead;
			o._aheadLen = _aheadLen;
			o._fromX = _fromX;
			o._fromY = _fromY;
			o._aheadEnds = _aheadEnds;
			o._pn = _pn;
			o._hi = _hi;
			o._hn = _hn;
			o._clock = _clock;
			o._lastX = _lastX;
			o._lastY = _lastY;
			o._turnRate = _turnRate;
			o._have = _have;
			o.X = X;
			o.Y = Y;
			o.Facing = Facing;
			o.Speed = Speed;
			o.RealSpeed = RealSpeed;
			o.Gait = Gait;
			o.Lag = Lag;
			o.AimX = AimX;
			o.AimY = AimY;
			o.Settled = Settled;
			o.Dragged = Dragged;
			o.Kept = Kept;
		}

		private string Differs(BodyMotion o)
		{
			string text = Field("X", X, o.X) ?? Field("Y", Y, o.Y) ?? Field("Facing", Facing, o.Facing) ?? Field("Speed", Speed, o.Speed) ?? Field("RealSpeed", RealSpeed, o.RealSpeed) ?? Field("Gait", Gait, o.Gait) ?? Field("Lag", Lag, o.Lag) ?? Field("AimX", AimX, o.AimX) ?? Field("AimY", AimY, o.AimY) ?? Field("turn rate", _turnRate, o._turnRate) ?? Field("last X", _lastX, o._lastX) ?? Field("last Y", _lastY, o._lastY) ?? Field("clock", _clock, o._clock) ?? Field("ahead length", _aheadLen, o._aheadLen) ?? Field("from X", _fromX, o._fromX) ?? Field("from Y", _fromY, o._fromY);
			if (text != null)
			{
				return text;
			}
			if (Settled != o.Settled || Dragged != o.Dragged || Kept != o.Kept || _have != o._have || _aheadEnds != o._aheadEnds)
			{
				return $"flags settled/dragged/kept/placed/ahead ends {Settled}/{Dragged}/{Kept}/{_have}/{_aheadEnds} vs {o.Settled}/{o.Dragged}/{o.Kept}/{o._have}/{o._aheadEnds}";
			}
			if (_newest != o._newest || _count != o._count || _ahead != o._ahead || _pn != o._pn || _hi != o._hi || _hn != o._hn)
			{
				return $"counts newest/count/ahead/route/pace slot/pace count {_newest}/{_count}/{_ahead}/{_pn}/{_hi}/{_hn} vs {o._newest}/{o._count}/{o._ahead}/{o._pn}/{o._hi}/{o._hn}";
			}
			return Floats("trail x", _tx, o._tx, 96) ?? Floats("trail y", _ty, o._ty, 96) ?? Floats("ahead x", _ax, o._ax, 40) ?? Floats("ahead y", _ay, o._ay, 40) ?? Floats("route x", _px, o._px, _pn) ?? Floats("route y", _py, o._py, _pn) ?? Floats("route s", _ps, o._ps, _pn) ?? Floats("pace x", _hx, o._hx, 64) ?? Floats("pace y", _hy, o._hy, 64) ?? Floats("pace t", _ht, o._ht, 64);
		}

		private static string Field(string name, float a, float b)
		{
			if (Bits(a) != Bits(b))
			{
				return $"{name} {a:R} (0x{Bits(a):X8}) vs old {b:R} (0x{Bits(b):X8})";
			}
			return null;
		}

		private static string Floats(string name, float[] a, float[] b, int n)
		{
			for (int i = 0; i < n; i++)
			{
				if (Bits(a[i]) != Bits(b[i]))
				{
					return Field(name + " [" + i + "]", a[i], b[i]);
				}
			}
			return null;
		}
	}
}
