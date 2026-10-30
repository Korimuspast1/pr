using System;

namespace FirstPerson.Core
{
	public static class SunPath
	{
		private const double Deg = Math.PI / 180.0;

		public const float Latitude = 0.4537856f;

		public const float Tilt = MathF.PI / 15f;

		public const float MoonDays = 8f;

		public const int ShadowSteps = 4000;

		public static readonly Vec3 StarPole = new Vec3(0f, 0f - (float)Math.Cos(0.45378559827804565), (float)Math.Sin(0.45378559827804565));

		private const double ClassicNoon = Math.PI * 16.0 / 45.0;

		private const double NoonBlend = 0.5;

		public static float Declination(float seasonAngle)
		{
			return (float)(0.20943951606750488 * Math.Sin(seasonAngle));
		}

		public static float HalfDay(float declination)
		{
			return (float)HalfDayAngle(declination);
		}

		private static double HalfDayAngle(float declination)
		{
			double num = (0.0 - Math.Tan(0.45378559827804565)) * Math.Tan(declination);
			return Math.Acos((num < -1.0) ? (-1.0) : ((num > 1.0) ? 1.0 : num));
		}

		public static float HourAngle(float t, float declination)
		{
			double num = HalfDayAngle(declination);
			double num2 = (double)t - Math.Floor(t);
			if (num2 >= 0.25 && num2 <= 0.75)
			{
				return (float)(0.0 - num + (num2 - 0.25) * 4.0 * num);
			}
			if (num2 < 0.25)
			{
				num2 += 1.0;
			}
			return (float)(num + (num2 - 0.75) * 2.0 * (Math.PI * 2.0 - 2.0 * num));
		}

		public static Vec3 Direction(float hourAngle, float declination)
		{
			double num = Math.Sin(0.45378559827804565);
			double num2 = Math.Cos(0.45378559827804565);
			double num3 = Math.Sin(declination);
			double num4 = Math.Cos(declination);
			double num5 = Math.Sin(hourAngle);
			double num6 = Math.Cos(hourAngle);
			return new Vec3((float)((0.0 - num4) * num5), (float)(num * num4 * num6 - num2 * num3), (float)(num * num3 + num2 * num4 * num6));
		}

		public static float Elevation(Vec3 direction)
		{
			return (float)Math.Asin((direction.Z < -1f) ? (-1f) : ((direction.Z > 1f) ? 1f : direction.Z));
		}

		public static void Moon(float sunHourAngle, float declination, float moonCycle, out float hourAngle, out float moonDeclination)
		{
			double num = Elongation(moonCycle);
			hourAngle = (float)((double)sunHourAngle - num);
			moonDeclination = (float)((double)declination * Math.Cos(num));
		}

		public static float MoonLit(float moonCycle)
		{
			return (float)((1.0 - Math.Cos(Elongation(moonCycle))) * 0.5);
		}

		private static double Elongation(float moonCycle)
		{
			return Math.PI * 2.0 * ((double)moonCycle - Math.Floor(moonCycle));
		}

		public static float MorningHour(float elevation, float declination)
		{
			return (float)(0.25 + Height(elevation, declination) / (Math.PI * 2.0));
		}

		public static float EveningHour(float elevation, float declination)
		{
			return (float)(0.75 - Height(elevation, declination) / (Math.PI * 2.0));
		}

		private static double Height(float elevation, float declination)
		{
			double num = ((elevation >= 0f) ? (Math.PI * 16.0 / 45.0) : Math.Min(Math.PI * 16.0 / 45.0, 1.117010728516851 - (double)declination));
			double num2 = (double)elevation / num;
			return Math.Asin((num2 < -1.0) ? (-1.0) : ((num2 > 1.0) ? 1.0 : num2));
		}

		public static float EveningShare(float hourAngle)
		{
			double num = ((double)hourAngle - Math.PI * 2.0 * Math.Floor(((double)hourAngle + Math.PI) / (Math.PI * 2.0)) + 0.5) / 1.0;
			num = ((num < 0.0) ? 0.0 : ((num > 1.0) ? 1.0 : num));
			return (float)(num * num * (3.0 - 2.0 * num));
		}

		public static float StepHour(float t)
		{
			return (float)(Math.Round((double)t * 4000.0) / 4000.0);
		}
	}
}
