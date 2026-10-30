using System;

namespace FirstPerson.Core
{
	public sealed class StrikeState
	{
		public const float Buffer = 0.3f;

		public bool Started;

		public bool StruckOut;

		public bool Landed;

		public float HoldAt = -1f;

		private bool _queued;

		private bool _landed;

		public bool Swinging { get; private set; }

		public float Time { get; private set; }

		public StrikeTiming Timing { get; private set; }

		public float Cadence { get; private set; }

		public int Blows { get; private set; }

		public float Readiness
		{
			get
			{
				if (!Swinging)
				{
					return 1f;
				}
				float strikeEnd = Timing.StrikeEnd;
				float num = Cadence - strikeEnd;
				if (!(num <= 0f))
				{
					return Math.Max(0f, Math.Min(1f, (Time - strikeEnd) / num));
				}
				return 1f;
			}
		}

		public void Press()
		{
			if (!Swinging || Time >= Cadence - 0.3f)
			{
				_queued = true;
			}
		}

		public void Update(float dt, bool held, bool allowed, StrikeTiming timing, float cadence)
		{
			Started = (StruckOut = (Landed = false));
			float time = 0f;
			if (Swinging)
			{
				float time2 = Time;
				Time += dt;
				if (HoldAt >= 0f && Time > HoldAt)
				{
					Time = Math.Max(time2, HoldAt);
				}
				if (time2 < Timing.StrikeStart && Time >= Timing.StrikeStart)
				{
					StruckOut = true;
				}
				if (!_landed && Time >= Timing.Hit)
				{
					_landed = true;
					Landed = allowed;
				}
				else if (!_landed && !allowed)
				{
					_landed = true;
				}
				if (Time >= Cadence)
				{
					Swinging = false;
					time = Math.Min(Time - Cadence, 0.1f);
				}
			}
			if (!Swinging)
			{
				if ((held || _queued) && allowed)
				{
					Start(timing, cadence, time);
				}
				if (allowed || !held)
				{
					_queued = false;
				}
			}
		}

		private void Start(StrikeTiming timing, float cadence, float time)
		{
			Swinging = true;
			Time = time;
			Timing = timing;
			Cadence = Math.Max(cadence, timing.StrikeEnd + 0.05f);
			Blows++;
			Started = true;
			_landed = false;
			_queued = false;
			HoldAt = -1f;
			if (timing.StrikeStart <= time)
			{
				StruckOut = true;
			}
			if (timing.Hit <= time)
			{
				_landed = true;
				Landed = true;
			}
		}

		public void Reset()
		{
			Swinging = false;
			Time = 0f;
			Started = (StruckOut = (Landed = false));
			_queued = (_landed = false);
			HoldAt = -1f;
		}
	}
}
