using System;

namespace FirstPerson.Core
{
	public sealed class Momentum
	{
		public float Vx;

		public float Vy;

		public const float ReverseAngle = 120f;

		public const float ReverseBrake = 1.4f;

		public float Speed => (float)Math.Sqrt(Vx * Vx + Vy * Vy);

		public void Reset()
		{
			Vx = (Vy = 0f);
		}

		public void Step(float dt, float tx, float ty, float run, float sprint, in Feel f, float grip, bool grounded)
		{
			if (dt <= 0f)
			{
				return;
			}
			if (f.Instant)
			{
				if (grounded)
				{
					Vx = tx;
					Vy = ty;
				}
				return;
			}
			grip = ((grip <= 0f) ? 1f : grip);
			run = Math.Max(0.05f, run);
			sprint = Math.Max(run + 0.05f, sprint);
			float num = grip * (grounded ? 1f : Math.Max(0f, f.Air));
			if (num <= 0f)
			{
				return;
			}
			float num2 = run / Math.Max(0.01f, f.Start) * num;
			float num3 = (sprint - run) / Math.Max(0.01f, f.Build) * num;
			float num4 = run / Math.Max(0.01f, f.Stop) * num;
			float num5 = sprint / Math.Max(0.01f, f.Plant) * num;
			float num6 = run / Math.Max(0.01f, f.Turn) * num;
			float speed = Speed;
			float num7 = (float)Math.Sqrt(tx * tx + ty * ty);
			if (!grounded)
			{
				if (num7 < 1E-05f)
				{
					return;
				}
				if (((speed > 1E-05f) ? ((Vx * tx + Vy * ty) / (speed * num7)) : 1f) >= (float)Math.Cos(Math.PI * 2.0 / 3.0) && num7 < speed)
				{
					tx *= speed / num7;
					ty *= speed / num7;
					num7 = speed;
				}
			}
			if (speed < 0.02f)
			{
				if (num7 < 1E-05f)
				{
					Vx = (Vy = 0f);
					return;
				}
				float num8 = Math.Min(num7, speed + num2 * dt);
				Vx = tx / num7 * num8;
				Vy = ty / num7 * num8;
				return;
			}
			float num9 = 1f;
			if (num7 > 1E-05f)
			{
				num9 = (Vx * tx + Vy * ty) / (speed * num7);
				float num10 = num6 / Math.Max(speed, 0.5f * run);
				Rotate(tx / num7, ty / num7, num10 * dt);
			}
			float num12;
			if (num7 > speed)
			{
				float num11 = ((speed < run) ? num2 : num3);
				num12 = Math.Min(num7, speed + num11 * dt);
			}
			else
			{
				float num13 = ((speed <= run) ? num4 : (num4 + (num5 - num4) * Math.Min(1f, (speed - run) / Math.Max(0.001f, sprint - run))));
				if (speed > sprint)
				{
					num13 = num5 * 1.6f;
				}
				if (num7 > 1E-05f && num9 < (float)Math.Cos(Math.PI * 2.0 / 3.0))
				{
					num13 *= 1.4f;
				}
				num12 = Math.Max(num7, speed - num13 * dt);
			}
			float num14 = num12 / speed;
			Vx *= num14;
			Vy *= num14;
		}

		private void Rotate(float ux, float uy, float most)
		{
			float speed = Speed;
			if (!(speed < 1E-06f))
			{
				double num = Math.Atan2(Vy, Vx);
				double num2;
				for (num2 = Math.Atan2(uy, ux) - num; num2 > Math.PI; num2 -= Math.PI * 2.0)
				{
				}
				for (; num2 < -Math.PI; num2 += Math.PI * 2.0)
				{
				}
				if (Math.Abs(num2) > (double)most)
				{
					num2 = (float)Math.Sign(num2) * most;
				}
				num += num2;
				Vx = (float)Math.Cos(num) * speed;
				Vy = (float)Math.Sin(num) * speed;
			}
		}

		public void Slid(float dx, float dy, float share)
		{
			float num = (float)Math.Sqrt(dx * dx + dy * dy);
			float speed = Speed;
			if (num < 1E-05f || share <= 0f)
			{
				Vx = (Vy = 0f);
				return;
			}
			float num2 = speed * Math.Min(1f, share);
			Vx = dx / num * num2;
			Vy = dy / num * num2;
		}

		public void Push(float dx, float dy)
		{
			Vx += dx;
			Vy += dy;
		}
	}
}
