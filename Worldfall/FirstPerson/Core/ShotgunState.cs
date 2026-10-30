using System;

namespace FirstPerson.Core
{
	public sealed class ShotgunState
	{
		public enum Phase : byte
		{
			Ready,
			Cycling,
			ReloadIn,
			Loading,
			ReloadOut,
			Racking
		}

		public const int Capacity = 6;

		public const float KickTime = 0.045f;

		public const float Settle = 0.2f;

		public const float PumpBackStart = 0.2f;

		public const float PumpBackEnd = 0.31f;

		public const float PumpFwdStart = 0.37f;

		public const float PumpFwdEnd = 0.47f;

		public const float CycleEnd = 0.62f;

		public const float EjectAt = 0.28f;

		public const float ReloadInTime = 0.3f;

		public const float ShellTime = 0.44f;

		public const float ShellPushAt = 0.3f;

		public const float ReloadOutTime = 0.28f;

		public const float RackTime = 0.42f;

		public const float Cadence = 0.82f;

		public int Loaded = 6;

		public bool Chambered = true;

		public Phase State;

		public float Time;

		public float SinceShot = 99f;

		public int Shots;

		public int Inserted;

		private bool _stopReload;

		private bool _wasEmpty;

		public bool Fired;

		public bool PumpedBack;

		public bool PumpedForward;

		public bool Ejected;

		public bool ShellPushed;

		public bool ShellTaken;

		public bool StartedReload;

		public bool FinishedReload;

		public bool DryFire;

		public bool Reloading
		{
			get
			{
				if (State != Phase.ReloadIn && State != Phase.Loading && State != Phase.ReloadOut)
				{
					return State == Phase.Racking;
				}
				return true;
			}
		}

		public bool Blocked
		{
			get
			{
				if (!Reloading && Loaded > 0)
				{
					if (!Chambered)
					{
						return State != Phase.Cycling;
					}
					return false;
				}
				return true;
			}
		}

		public bool Full => Loaded >= 6;

		public bool Stopping => _stopReload;

		public float Pump
		{
			get
			{
				if (State == Phase.Cycling)
				{
					return PumpAt(Time, 0.2f, 0.31f, 0.37f, 0.47f);
				}
				if (State == Phase.Racking)
				{
					return PumpAt(Time, 0.04f, 0.15f, 0.18f, 0.28f);
				}
				return 0f;
			}
		}

		public float Roll
		{
			get
			{
				switch (State)
				{
				case Phase.ReloadIn:
					return Smooth(Time / 0.3f);
				case Phase.Loading:
					return 1f;
				case Phase.ReloadOut:
					return 1f - Smooth(Time / 0.28f);
				default:
					return 0f;
				}
			}
		}

		public bool WasEmptyAtReload => _wasEmpty;

		public void Reset()
		{
			Loaded = 6;
			Chambered = true;
			State = Phase.Ready;
			Time = 0f;
			SinceShot = 99f;
			Inserted = 0;
			_stopReload = false;
			ClearEvents();
		}

		private void ClearEvents()
		{
			Fired = (PumpedBack = (PumpedForward = (Ejected = (ShellPushed = (ShellTaken = (StartedReload = (FinishedReload = (DryFire = false))))))));
		}

		public void Shot()
		{
			if (Loaded > 0)
			{
				Loaded--;
			}
			Chambered = false;
			Shots++;
			SinceShot = 0f;
			State = Phase.Cycling;
			Time = 0f;
			Fired = true;
		}

		public bool Dry()
		{
			DryFire = true;
			if (Loaded < 6)
			{
				return !Reloading;
			}
			return false;
		}

		public bool StartReload()
		{
			if (Full && Chambered)
			{
				return false;
			}
			if (State != Phase.Ready)
			{
				return false;
			}
			State = Phase.ReloadIn;
			Time = 0f;
			Inserted = 0;
			_stopReload = false;
			_wasEmpty = !Chambered;
			StartedReload = true;
			return true;
		}

		public void Equip()
		{
			State = Phase.Racking;
			Time = -0.3f;
			Chambered = false;
		}

		public void StopReload()
		{
			if (Reloading && (Loaded > 0 || State == Phase.Loading))
			{
				_stopReload = true;
			}
		}

		public void Update(float dt)
		{
			ClearEvents();
			if (dt <= 0f)
			{
				return;
			}
			SinceShot += dt;
			float time = Time;
			Time += dt;
			switch (State)
			{
			case Phase.Cycling:
				if (time < 0.2f && Time >= 0.2f)
				{
					PumpedBack = true;
				}
				if (time < 0.28f && Time >= 0.28f)
				{
					Ejected = true;
				}
				if (time < 0.37f && Time >= 0.37f)
				{
					PumpedForward = true;
				}
				if (time < 0.47f && Time >= 0.47f)
				{
					Chambered = Loaded > 0;
				}
				if (Time >= 0.62f)
				{
					Enter(Phase.Ready);
				}
				break;
			case Phase.ReloadIn:
				if (Time >= 0.3f)
				{
					Enter((Loaded < 6 && (!_stopReload || Loaded <= 0)) ? Phase.Loading : Phase.ReloadOut);
				}
				break;
			case Phase.Loading:
				if (time < 0.12f && Time >= 0.12f)
				{
					ShellTaken = true;
				}
				if (time < 0.3f && Time >= 0.3f)
				{
					Loaded = Math.Min(6, Loaded + 1);
					Inserted++;
					ShellPushed = true;
				}
				if (Time >= 0.44f)
				{
					if (Loaded >= 6 || _stopReload)
					{
						Enter(Phase.ReloadOut);
					}
					else
					{
						Enter(Phase.Loading);
					}
				}
				break;
			case Phase.ReloadOut:
				if (Time >= 0.28f)
				{
					if (!Chambered && Loaded > 0)
					{
						Enter(Phase.Racking);
					}
					else
					{
						Finish();
					}
				}
				break;
			case Phase.Racking:
				if (time < 0.04f && Time >= 0.04f)
				{
					PumpedBack = true;
				}
				if (time < 0.18f && Time >= 0.18f)
				{
					PumpedForward = true;
				}
				if (time < 0.26f && Time >= 0.26f)
				{
					Chambered = Loaded > 0;
				}
				if (Time >= 0.42f)
				{
					Finish();
				}
				break;
			}
		}

		private void Enter(Phase p)
		{
			State = p;
			Time = 0f;
		}

		private void Finish()
		{
			Enter(Phase.Ready);
			FinishedReload = true;
			_stopReload = false;
		}

		private static float PumpAt(float t, float b0, float b1, float f0, float f1)
		{
			if (t <= b0 || t >= f1)
			{
				return 0f;
			}
			if (t < b1)
			{
				float num = (t - b0) / (b1 - b0);
				return 1f - (1f - num) * (1f - num) * (1f - num);
			}
			if (t < f0)
			{
				return 1f;
			}
			float num2 = (t - f0) / (f1 - f0);
			return 1f - num2 * num2 * (3f - 2f * num2);
		}

		private static float Smooth(float x)
		{
			x = Math.Max(0f, Math.Min(1f, x));
			return x * x * (3f - 2f * x);
		}
	}
}
