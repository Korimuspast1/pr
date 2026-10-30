using System;

namespace FirstPerson.Core
{
	public sealed class BlasterState
	{
		public float Heat;

		public float BarrelHeat;

		public bool Overheated;

		public float Vent = -1f;

		public float SinceShot = 99f;

		public int Shots;

		public float Clock;

		public const float VentTime = 1.3f;

		public const float HeatPerShot = 0.15f;

		public bool StartedVent;

		public bool Twisted;

		public bool Clicked;

		public bool Alarm;

		public bool Finished;

		public bool Venting => Vent >= 0f;

		public bool Locked
		{
			get
			{
				if (!Overheated)
				{
					return Venting;
				}
				return true;
			}
		}

		public float VentProgress
		{
			get
			{
				if (!Venting)
				{
					return 0f;
				}
				return Vent / 1.3f;
			}
		}

		public void Reset()
		{
			Heat = (BarrelHeat = 0f);
			Overheated = false;
			Vent = -1f;
			SinceShot = 99f;
		}

		public void Shot()
		{
			SinceShot = 0f;
			Shots++;
			Heat = Math.Min(1f, Heat + 0.15f);
			BarrelHeat = Math.Min(1.2f, BarrelHeat + 0.16f);
			if (Heat >= 0.999f && !Overheated)
			{
				Overheated = true;
				Alarm = true;
			}
		}

		public bool StartVent()
		{
			if (Venting || Heat < 0.08f)
			{
				return false;
			}
			Vent = 0f;
			StartedVent = true;
			return true;
		}

		public void Update(float dt)
		{
			StartedVent = (Twisted = (Clicked = (Alarm = (Finished = false))));
			if (dt <= 0f)
			{
				return;
			}
			Clock += dt;
			SinceShot += dt;
			if (Venting)
			{
				float vent = Vent;
				Vent += dt;
				float[] array = new float[2] { 0.55f, 0.88f };
				foreach (float num in array)
				{
					if (vent < num * 1.3f && Vent >= num * 1.3f)
					{
						Clicked = true;
					}
				}
				if (vent < 0.39000002f && Vent >= 0.39000002f)
				{
					Twisted = true;
				}
				float ventProgress = VentProgress;
				if (ventProgress > 0.3f)
				{
					Heat = Math.Max(0f, Heat - dt * 1.8f);
				}
				BarrelHeat = Math.Max(0f, BarrelHeat - dt * ((ventProgress > 0.3f) ? 1.1f : 0.1f));
				if (Vent >= 1.3f)
				{
					Vent = -1f;
					Heat = 0f;
					Overheated = false;
					Finished = true;
				}
			}
			else
			{
				if (!Overheated && SinceShot > 0.6f)
				{
					Heat = Math.Max(0f, Heat - dt * 0.11f);
				}
				BarrelHeat = Math.Max(0f, BarrelHeat - dt * ((SinceShot > 1f) ? 0.085f : 0.02f));
				if (Overheated && (int)(Clock * 0.8f) != (int)((Clock - dt) * 0.8f))
				{
					Alarm = true;
				}
			}
		}
	}
}
