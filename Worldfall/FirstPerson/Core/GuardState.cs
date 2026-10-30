using System;

namespace FirstPerson.Core
{
	public sealed class GuardState
	{
		public const float RaiseTime = 0.12f;

		public const float LowerTime = 0.15f;

		public const float ParryLockout = 0.6f;

		public const float StaggerTime = 0.7f;

		public const float RiposteTime = 0.8f;

		public float ParryWindow = 0.2f;

		private float _lockout;

		private float _riposte;

		private float _from;

		private bool _parried;

		private bool _windowOver;

		private bool _pressPending;

		public GuardPhase Phase { get; private set; }

		public float Time { get; private set; }

		public float Held { get; private set; }

		public int Presses { get; private set; }

		public float Raise
		{
			get
			{
				switch (Phase)
				{
				case GuardPhase.Raising:
					return _from + (1f - _from) * Ease(Time / 0.12f);
				case GuardPhase.Up:
					return 1f;
				case GuardPhase.Lowering:
					return _from * (1f - Ease(Time / 0.15f));
				default:
					return 0f;
				}
			}
		}

		public bool Raised
		{
			get
			{
				if (Phase != GuardPhase.Raising)
				{
					return Phase == GuardPhase.Up;
				}
				return true;
			}
		}

		public bool ParryOpen
		{
			get
			{
				if (Raised && Held <= ParryWindow && _lockout <= 0f)
				{
					return !_parried;
				}
				return false;
			}
		}

		public bool Blocking
		{
			get
			{
				if (Raised && Held >= 0.12f)
				{
					return !ParryOpen;
				}
				return false;
			}
		}

		public bool Staggered => Phase == GuardPhase.Staggered;

		public bool Riposte => _riposte > 0f;

		public void Update(float dt, bool held, bool allowed, bool pressed)
		{
			if (pressed)
			{
				_pressPending = true;
			}
			if (!held)
			{
				_pressPending = false;
			}
			_lockout = Math.Max(0f, _lockout - dt);
			_riposte = Math.Max(0f, _riposte - dt);
			Time += dt;
			bool flag = held && allowed;
			switch (Phase)
			{
			case GuardPhase.Staggered:
				if (Time >= 0.7f)
				{
					Set(GuardPhase.Down, 0f);
				}
				return;
			case GuardPhase.Down:
				if (flag)
				{
					Press(0f);
				}
				break;
			case GuardPhase.Lowering:
				if (flag)
				{
					Press(Raise);
				}
				else if (Time >= 0.15f)
				{
					Set(GuardPhase.Down, 0f);
				}
				break;
			case GuardPhase.Raising:
			case GuardPhase.Up:
				if (!flag)
				{
					if (!_windowOver && !_parried)
					{
						_windowOver = true;
						_lockout = 0.6f;
					}
					Set(GuardPhase.Lowering, Raise);
				}
				else
				{
					Held += dt;
					if (Phase == GuardPhase.Raising && Time >= 0.12f)
					{
						Set(GuardPhase.Up, 1f);
					}
				}
				break;
			}
			if (Raised && !_windowOver && Held > ParryWindow)
			{
				_windowOver = true;
				if (!_parried)
				{
					_lockout = 0.6f;
				}
			}
		}

		private void Press(float from)
		{
			Set(GuardPhase.Raising, from);
			Held = 0f;
			_parried = !_pressPending;
			_pressPending = false;
			_windowOver = false;
			Presses++;
		}

		private void Set(GuardPhase phase, float from)
		{
			Phase = phase;
			Time = 0f;
			_from = Math.Max(0f, Math.Min(1f, from));
		}

		public void Parried()
		{
			_parried = true;
			_riposte = 0.8f;
		}

		public bool TakeRiposte()
		{
			if (_riposte <= 0f)
			{
				return false;
			}
			_riposte = 0f;
			return true;
		}

		public void Stagger()
		{
			Set(GuardPhase.Staggered, 0f);
			Held = 0f;
			_riposte = 0f;
		}

		public void Reset()
		{
			Set(GuardPhase.Down, 0f);
			Held = 0f;
			_lockout = (_riposte = 0f);
			_parried = (_windowOver = (_pressPending = false));
		}

		private static float Ease(float x)
		{
			x = ((x < 0f) ? 0f : ((x > 1f) ? 1f : x));
			return x * x * (3f - 2f * x);
		}
	}
}
