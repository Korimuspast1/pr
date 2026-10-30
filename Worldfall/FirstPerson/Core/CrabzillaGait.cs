using System;

namespace FirstPerson.Core
{
	public sealed class CrabzillaGait
	{
		public readonly Vec3[] Feet = new Vec3[8];

		public float Reach = 2.8f;

		public float Lift = 2.2f;

		private static readonly bool[] SetA = new bool[8] { true, false, true, false, false, true, false, true };

		private readonly Vec3[] _from = new Vec3[8];

		private readonly Vec3[] _to = new Vec3[8];

		private readonly float[] _t = new float[8];

		private readonly float[] _dur = new float[8];

		private bool _started;

		private float _still;

		private float _dipVel;

		public int Landed { get; private set; }

		public float Dip { get; private set; }

		public void Reset()
		{
			_started = false;
		}

		public void Update(Vec3[] homes, float vx, float vy, float dt, Func<float, float, float> ground)
		{
			Landed = 0;
			if (!_started)
			{
				for (int i = 0; i < Feet.Length; i++)
				{
					Feet[i] = new Vec3(homes[i].X, homes[i].Y, ground(homes[i].X, homes[i].Y));
					_t[i] = -1f;
				}
				_started = true;
				Dip = 0f;
			}
			else
			{
				if (dt <= 0f)
				{
					return;
				}
				float num = (float)Math.Sqrt(vx * vx + vy * vy);
				_still = ((num < 0.4f) ? (_still + dt) : 0f);
				float num2 = Clamp(0.5f - num * 0.018f, 0.24f, 0.5f);
				for (int j = 0; j < Feet.Length; j++)
				{
					if (!(_t[j] < 0f))
					{
						_t[j] += dt / _dur[j];
						float num3 = Math.Max(0f, 1f - _t[j]) * _dur[j];
						Vec3 vec = new Vec3(homes[j].X + vx * (num3 + _dur[j] * 0.45f), homes[j].Y + vy * (num3 + _dur[j] * 0.45f), 0f);
						_to[j] = new Vec3(vec.X, vec.Y, ground(vec.X, vec.Y));
						if (_t[j] >= 1f)
						{
							Feet[j] = _to[j];
							_t[j] = -1f;
							Landed |= 1 << j;
						}
						else
						{
							float num4 = _t[j];
							float num5 = num4 * num4 * (3f - 2f * num4);
							Vec3 vec2 = _from[j] + (_to[j] - _from[j]) * num5;
							float num6 = (float)Math.Sin((double)num4 * Math.PI);
							Feet[j] = new Vec3(vec2.X, vec2.Y, vec2.Z + Lift * num6 * Clamp(0.45f + num * 0.08f, 0.45f, 1f));
						}
					}
				}
				bool flag = true;
				bool flag2 = true;
				for (int k = 0; k < Feet.Length; k++)
				{
					if (_t[k] >= 0f)
					{
						if (SetA[k])
						{
							flag = false;
						}
						else
						{
							flag2 = false;
						}
					}
				}
				float num7 = 0f;
				float num8 = 0f;
				for (int l = 0; l < Feet.Length; l++)
				{
					if (!(_t[l] >= 0f))
					{
						float num9 = Feet[l].X - homes[l].X;
						float num10 = Feet[l].Y - homes[l].Y;
						float val = (float)Math.Sqrt(num9 * num9 + num10 * num10);
						if (SetA[l])
						{
							num7 = Math.Max(num7, val);
						}
						else
						{
							num8 = Math.Max(num8, val);
						}
					}
				}
				float num11 = ((_still > 0.35f) ? 0.45f : Reach);
				bool flag3 = num7 > num11 && flag2 && flag && (num7 >= num8 || !(num8 > num11));
				bool flag4 = !flag3 && num8 > num11 && flag && flag2;
				if (flag3 || flag4)
				{
					for (int m = 0; m < Feet.Length; m++)
					{
						if (SetA[m] == flag3 && !(_t[m] >= 0f))
						{
							float num12 = Feet[m].X - homes[m].X;
							float num13 = Feet[m].Y - homes[m].Y;
							if (!(num12 * num12 + num13 * num13 < 0.04f))
							{
								_from[m] = Feet[m];
								_t[m] = 0f;
								_dur[m] = num2 * (0.92f + 0.16f * (float)(m * 37 % 7) / 6f);
								_to[m] = new Vec3(homes[m].X, homes[m].Y, ground(homes[m].X, homes[m].Y));
							}
						}
					}
				}
				int num14 = 0;
				for (int n = 0; n < Feet.Length; n++)
				{
					if ((Landed & (1 << n)) != 0)
					{
						num14++;
					}
				}
				if (num14 > 0)
				{
					_dipVel += 0.55f * (float)num14 / 4f * Clamp(0.5f + num * 0.06f, 0.5f, 1.2f);
				}
				float num15 = Math.Min(dt, 0.05f);
				_dipVel += ((0f - Dip) * 70f - _dipVel * 11f) * num15;
				Dip = Math.Max(-0.4f, Dip + _dipVel * num15);
			}
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
	}
}
