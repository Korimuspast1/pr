using System;

namespace FirstPerson.Core
{
	public sealed class BowState
	{
		public enum Phase
		{
			Ready,
			Drawing,
			Held,
			LettingDown,
			Loosed
		}

		public float DrawTime = 0.9f;

		public const float ReloadTime = 1.15f;

		public const float MinimumDraw = 0.45f;

		public const float Buffer = 0.45f;

		public const float SteadySeconds = 2.4f;

		public bool StartedDraw;

		public bool Fired;

		public bool LetDown;

		public bool Nocked;

		public bool ReachedFull;

		private bool _snap;

		private bool _queued;

		public Phase State { get; private set; }

		public float Draw { get; private set; }

		public float Time { get; private set; }

		public float Tremble { get; private set; }

		public float LastShotDraw { get; private set; }

		public bool ArrowNocked
		{
			get
			{
				if (State == Phase.Loosed)
				{
					return Time >= 1.15f;
				}
				return true;
			}
		}

		public void Press()
		{
			if (State == Phase.Ready || (State == Phase.Loosed && Time >= 0.7f))
			{
				_queued = true;
			}
		}

		public void Update(float dt, bool held, bool allowed)
		{
			StartedDraw = (Fired = (LetDown = (Nocked = (ReachedFull = false))));
			Time += dt;
			switch (State)
			{
			case Phase.Ready:
				Draw = Math.Max(0f, Draw - dt * 3f);
				if ((held || _queued) && allowed)
				{
					StartDraw(held);
				}
				_queued = false;
				break;
			case Phase.Drawing:
			{
				if (!allowed)
				{
					_snap = false;
					Go(Phase.LettingDown, ref LetDown);
					break;
				}
				if (held)
				{
					_snap = false;
				}
				else
				{
					if (Draw >= 0.45f)
					{
						Loose();
						break;
					}
					_snap = true;
				}
				float num = Math.Min(1f, Time / DrawTime);
				Draw = 1f - (1f - num) * (1f - num) * (1f - 0.35f * num);
				if (_snap && Draw >= 0.45f)
				{
					Loose();
				}
				else if (num >= 1f)
				{
					Draw = 1f;
					Go(Phase.Held, ref ReachedFull);
				}
				break;
			}
			case Phase.Held:
				Draw = 1f;
				Tremble = Math.Min(1f, Math.Max(0f, (Time - 2.4f) / 2.5f));
				if (!allowed)
				{
					Go(Phase.LettingDown, ref LetDown);
				}
				else if (!held)
				{
					Loose();
				}
				break;
			case Phase.LettingDown:
				Tremble = Math.Max(0f, Tremble - dt * 4f);
				Draw = Math.Max(0f, Draw - dt * 3.2f);
				if (Draw <= 0f)
				{
					State = Phase.Ready;
					Time = 0f;
				}
				break;
			case Phase.Loosed:
				Draw = 0f;
				Tremble = 0f;
				if (Time >= 1.15f)
				{
					Nocked = true;
					State = Phase.Ready;
					Time = 0f;
					if ((held || _queued) && allowed)
					{
						StartDraw(held);
					}
					_queued = false;
				}
				break;
			}
		}

		public void UpdatePulled(float dt, float pulled, bool allowed)
		{
			StartedDraw = (Fired = (LetDown = (Nocked = (ReachedFull = false))));
			Time += dt;
			_snap = (_queued = false);
			bool flag = pulled >= 0f && allowed;
			switch (State)
			{
			case Phase.Ready:
				Draw = 0f;
				Tremble = 0f;
				if (flag)
				{
					Go(Phase.Drawing, ref StartedDraw);
					Draw = Math.Min(1f, pulled);
				}
				break;
			case Phase.Drawing:
			case Phase.Held:
				if (!allowed)
				{
					Go(Phase.LettingDown, ref LetDown);
					break;
				}
				if (pulled < 0f)
				{
					if (Draw >= 0.45f)
					{
						Loose();
						break;
					}
					Draw = 0f;
					Tremble = 0f;
					Go(Phase.Ready, ref LetDown);
					break;
				}
				Draw = Math.Min(1f, pulled);
				if (State == Phase.Drawing && Draw >= 0.98f)
				{
					Go(Phase.Held, ref ReachedFull);
				}
				else if (State == Phase.Held && Draw < 0.9f)
				{
					State = Phase.Drawing;
					Time = 0f;
				}
				Tremble = ((State == Phase.Held) ? Math.Min(1f, Math.Max(0f, (Time - 2.4f) / 2.5f)) : Math.Max(0f, Tremble - dt * 4f));
				break;
			case Phase.LettingDown:
				Tremble = Math.Max(0f, Tremble - dt * 4f);
				Draw = Math.Max(0f, Draw - dt * 3.2f);
				if (Draw <= 0f)
				{
					State = Phase.Ready;
					Time = 0f;
				}
				break;
			case Phase.Loosed:
				Draw = 0f;
				Tremble = 0f;
				if (Time >= 1.15f)
				{
					Nocked = true;
					State = Phase.Ready;
					Time = 0f;
				}
				break;
			}
		}

		private void StartDraw(bool held)
		{
			Go(Phase.Drawing, ref StartedDraw);
			_snap = !held;
		}

		private void Loose()
		{
			_snap = false;
			LastShotDraw = Draw;
			Fired = true;
			State = Phase.Loosed;
			Time = 0f;
			Draw = 0f;
			Tremble = 0f;
		}

		private void Go(Phase p, ref bool flag)
		{
			State = p;
			Time = 0f;
			flag = true;
			if (p == Phase.Drawing)
			{
				Tremble = 0f;
			}
		}

		public void Reset()
		{
			State = Phase.Ready;
			Time = 0f;
			Draw = 0f;
			Tremble = 0f;
			StartedDraw = (Fired = (LetDown = (Nocked = (ReachedFull = false))));
			_snap = (_queued = false);
		}

		public void Pose(Phase p, float time, float draw)
		{
			State = p;
			Time = time;
			Draw = draw;
			Tremble = 0f;
		}
	}
}
