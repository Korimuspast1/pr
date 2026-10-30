using System;

namespace FirstPerson.Core
{
	public sealed class Gusts
	{
		public float Strength;

		public float DirX = 1f;

		public float DirY;

		public float Force = -1f;

		public int Count;

		private readonly Random _rnd;

		private float _wait;

		private float _len;

		private float _t;

		private float _peak;

		private float _angle;

		private float _veer;

		private float _clock;

		public float Fury;

		public float CalmLeft
		{
			get
			{
				if (!(_len > 0f))
				{
					return _wait;
				}
				return 0f;
			}
		}

		public float Speed => 2.5f + 8.5f * Strength;

		public bool Blowing => Strength > 0.05f;

		public Gusts(int seed)
		{
			_rnd = new Random(seed);
			_wait = 25f + 45f * (float)_rnd.NextDouble();
			_angle = 0.32f;
		}

		private float R()
		{
			return (float)_rnd.NextDouble();
		}

		public void Step(float dt, float windX, float windY, float storm)
		{
			Fury = 0f;
			if (dt <= 0f)
			{
				return;
			}
			_clock += dt;
			float num = ((windX * windX + windY * windY > 1E-06f) ? ((float)Math.Atan2(windY, windX)) : 0.32f);
			if (_len <= 0f)
			{
				_wait -= dt * (1f + 3f * storm);
				if (_wait <= 0f)
				{
					Begin(num, storm);
				}
			}
			else
			{
				_t += dt;
				if (_t >= _len)
				{
					_len = 0f;
					_t = 0f;
					_wait = 45f + 75f * R();
				}
			}
			float num2 = 0f;
			if (_len > 0f)
			{
				float num3 = Math.Min(2.4f, _len * 0.25f);
				float num4 = Math.Min(4f, _len * 0.35f);
				num2 = ((_t < num3) ? BlastFx.Smooth(_t / num3) : ((_t > _len - num4) ? BlastFx.Smooth((_len - _t) / num4) : 1f));
			}
			float num5 = 0.62f + 0.38f * (0.5f + 0.5f * (float)Math.Sin(_clock * 1.7f + 2f * (float)Math.Sin(_clock * 0.61f)));
			float v = num2 * _peak * num5 + storm * 0.3f * (0.75f + 0.25f * (float)Math.Sin(_clock * 1.1f));
			v = ((Force >= 0f) ? Force : BlastFx.Clamp01(v));
			float num6 = ((v > Strength) ? 2.2f : 1.1f);
			Strength += (v - Strength) * (1f - (float)Math.Exp((0f - num6) * dt));
			if (Force >= 0f)
			{
				Strength = Force;
			}
			float num7 = ((_len > 0f) ? _angle : num) + _veer * (float)Math.Sin(_clock * 0.33f);
			DirX = (float)Math.Cos(num7);
			DirY = (float)Math.Sin(num7);
		}

		private void Begin(float prevailing, float storm)
		{
			Count++;
			bool flag = R() < 0.22f;
			_len = (flag ? (22f + 18f * R()) : (7f + 7f * R()));
			_peak = Math.Min(1f, 0.55f + 0.45f * R() + 0.25f * storm);
			_angle = prevailing + (R() - 0.5f) * 0.9f;
			_veer = 0.12f + 0.18f * R();
			_t = 0f;
		}

		public void CalmFor(float seconds)
		{
			_len = 0f;
			_t = 0f;
			_wait = seconds;
		}

		public void GustNow(float seconds = 10f, float peak = 1f)
		{
			Count++;
			_len = seconds;
			_peak = peak;
			_t = 0f;
			_veer = 0.15f;
		}

		public void Pull(float strength, float dirX, float dirY)
		{
			Fury = BlastFx.Clamp01(strength);
			if (!(Fury <= 0.001f))
			{
				float num = Fury / Math.Max(0.001f, Fury + Strength * 0.5f);
				float num2 = DirX + (dirX - DirX) * num;
				float num3 = DirY + (dirY - DirY) * num;
				float num4 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
				if (num4 > 0.0001f)
				{
					DirX = num2 / num4;
					DirY = num3 / num4;
				}
				Strength = Math.Max(Strength, Fury);
			}
		}
	}
}
