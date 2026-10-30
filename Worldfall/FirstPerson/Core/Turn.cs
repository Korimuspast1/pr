using System;

namespace FirstPerson.Core
{
	public static class Turn
	{
		private const float TwoPi = MathF.PI * 2f;

		public static float Damp(float angle, ref float rate, float target, float dt, float smoothTime, float maxRate)
		{
			if (dt <= 0f)
			{
				return angle;
			}
			float num = angle + Wrap(target - angle);
			smoothTime = Math.Max(0.0001f, smoothTime);
			float num2 = 2f / smoothTime;
			float num3 = num2 * dt;
			float num4 = 1f / (1f + num3 + 0.48f * num3 * num3 + 0.235f * num3 * num3 * num3);
			float num5 = angle - num;
			float num6 = maxRate * smoothTime;
			if (num5 > num6)
			{
				num5 = num6;
			}
			else if (num5 < 0f - num6)
			{
				num5 = 0f - num6;
			}
			float num7 = (rate + num2 * num5) * dt;
			rate = (rate - num2 * num7) * num4;
			float num8 = angle - num5 + (num5 + num7) * num4;
			if (num - angle > 0f == num8 > num)
			{
				num8 = num;
				rate = 0f;
			}
			float num9 = maxRate * dt;
			if (num8 - angle > num9)
			{
				num8 = angle + num9;
			}
			else if (num8 - angle < 0f - num9)
			{
				num8 = angle - num9;
			}
			if (rate > maxRate)
			{
				rate = maxRate;
			}
			else if (rate < 0f - maxRate)
			{
				rate = 0f - maxRate;
			}
			return Wrap(num8);
		}

		public static float Wrap(float a)
		{
			if ((double)a >= -Math.PI && (double)a <= Math.PI)
			{
				return a;
			}
			return a - MathF.PI * 2f * (float)Math.Round(a / (MathF.PI * 2f));
		}
	}
}
