using System;

namespace FirstPerson.Core
{
	public sealed class Leap
	{
		public const float Gravity = 10.7f;

		public const float FallFactor = 1.6f;

		public float Floor;

		public bool Active { get; private set; }

		public float Lift { get; private set; }

		public float Rise { get; private set; }

		public float Time { get; private set; }

		public float Peak { get; private set; }

		public float LandSpeed { get; private set; }

		public int Landings { get; private set; }

		public int Starts { get; private set; }

		public static float LaunchSpeed(float peak)
		{
			return (float)Math.Sqrt(21.4f * Math.Max(0f, peak));
		}

		public static float Airtime(float peak)
		{
			return LaunchSpeed(peak) / 10.7f + (float)Math.Sqrt(2f * Math.Max(0f, peak) / 17.12f);
		}

		public void Start(float peak)
		{
			Active = true;
			Starts++;
			Peak = Math.Max(0f, peak);
			Lift = 0f;
			Rise = LaunchSpeed(Peak);
			Time = 0f;
			Floor = 0f;
		}

		public void StartFall()
		{
			Active = true;
			Peak = 0f;
			Lift = 0f;
			Rise = 0f;
			Time = 0f;
			Floor = 0f;
		}

		public bool Step(float dt)
		{
			if (!Active || dt <= 0f)
			{
				return false;
			}
			Time += dt;
			float num = dt;
			if (Rise > 0f)
			{
				float val = Rise / 10.7f;
				float num2 = Math.Min(num, val);
				Lift += Rise * num2 - 5.35f * num2 * num2;
				Rise -= 10.7f * num2;
				num -= num2;
				if (Rise < 1E-05f)
				{
					Rise = 0f;
				}
			}
			if (num > 0f)
			{
				float num3 = 17.12f;
				Lift += Rise * num - 0.5f * num3 * num * num;
				Rise -= num3 * num;
			}
			if (Rise > 0f || Lift > Floor)
			{
				return false;
			}
			Landed(0f - Rise);
			return true;
		}

		public void Landed(float speed)
		{
			if (Active)
			{
				LandSpeed = Math.Max(0f, speed);
				Landings++;
				Active = false;
				Lift = 0f;
				Rise = 0f;
			}
		}

		public void Cancel()
		{
			Active = false;
			Lift = 0f;
			Rise = 0f;
		}
	}
}
