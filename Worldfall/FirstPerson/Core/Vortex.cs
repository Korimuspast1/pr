using System;

namespace FirstPerson.Core
{
	public struct Vortex
	{
		public float X;

		public float Y;

		public float Core;

		public float Reach;

		public float Speed;

		public void Flow(float x, float y, out float vx, out float vy, out float vz)
		{
			vx = (vy = (vz = 0f));
			float num = x - X;
			float num2 = y - Y;
			float num3 = num * num + num2 * num2;
			if (!(num3 >= Reach * Reach))
			{
				float num4 = (float)Math.Sqrt(num3) + 0.001f;
				float num5 = 1f - BlastFx.Smooth((num4 - Reach * 0.45f) / (Reach * 0.55f));
				float num6 = ((num4 < Core) ? (num4 / Core) : ((float)Math.Pow(Core / num4, 0.5)));
				float num7 = Speed * num6 * num5;
				float num8 = (0f - Speed) * 0.55f * Math.Min(1f, num4 / Core) * (float)Math.Sqrt(Core / Math.Max(num4, Core)) * num5;
				float num9 = num / num4;
				float num10 = num2 / num4;
				vx = (0f - num10) * num7 + num9 * num8;
				vy = num9 * num7 + num10 * num8;
				float num11 = num4 / Core;
				vz = Speed * 0.6f * (float)Math.Exp((0f - num11) * num11);
			}
		}

		public float Strength(float x, float y)
		{
			Flow(x, y, out var vx, out var vy, out var _);
			return Math.Min(1f, (float)Math.Sqrt(vx * vx + vy * vy) / Math.Max(1f, Speed * 0.75f));
		}
	}
}
