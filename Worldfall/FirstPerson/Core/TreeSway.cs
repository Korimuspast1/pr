using System;

namespace FirstPerson.Core
{
	public static class TreeSway
	{
		public static float PushX = 0.7f;

		public static float PushY;

		public static float Gust;

		public static float Clock;

		public static void Step(float dt, float windX, float windY, float gustX, float gustY, float gust)
		{
			if (!(dt <= 0f))
			{
				Clock += dt;
				float num = (float)Math.Sqrt(windX * windX + windY * windY);
				float num2 = ((num > 0.0001f) ? (windX / num) : 0.7f);
				float num3 = ((num > 0.0001f) ? (windY / num) : 0.7f);
				float num4 = Math.Max(0f, Math.Min(1f, gust));
				float num5 = num2 + (gustX - num2) * num4;
				float num6 = num3 + (gustY - num3) * num4;
				float num7 = (float)Math.Sqrt(num5 * num5 + num6 * num6);
				float num8 = 0.5f + 0.25f * Math.Min(1f, num * 0.6f) + 0.25f * num4;
				if (num7 > 0.0001f)
				{
					num5 = num5 / num7 * num8;
					num6 = num6 / num7 * num8;
				}
				float num9 = Math.Min(1f, dt * 0.8f);
				PushX += (num5 - PushX) * num9;
				PushY += (num6 - PushY) * num9;
				Gust += (num4 - Gust) * Math.Min(1f, dt * 2.5f);
			}
		}

		public static float Tree(float top, bool giant)
		{
			if (!giant)
			{
				return Math.Min(0.2f, top * 0.02f);
			}
			return top * 0.0025f;
		}

		public static float Bush(float top)
		{
			return Math.Min(0.06f, top * 0.03f);
		}

		public static float Plant(float top)
		{
			return Math.Min(0.05f, top * 0.06f);
		}
	}
}
