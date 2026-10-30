using System;

namespace FirstPerson.Core
{
	public static class DragonPoses
	{
		private const float Pi = MathF.PI;

		private static void Wings(DragonPose p, float elev, float sweep, float twist, float elbow, float wrist, float spread, float billow)
		{
			for (int i = 0; i < 2; i++)
			{
				p.Elev[i] = elev;
				p.Sweep[i] = sweep;
				p.Twist[i] = twist;
				p.Elbow[i] = elbow;
				p.Wrist[i] = wrist;
				p.Spread[i] = spread;
				p.Billow[i] = billow;
			}
		}

		public static void Neutral(DragonPose p)
		{
			Wings(p, 0f, 0f, 0f, 0f, 0f, 1f, 0f);
			p.NeckPitch = (p.NeckYaw = (p.NeckCurl = 0f));
			p.HeadPitch = (p.HeadYaw = (p.HeadRoll = (p.Jaw = 0f)));
			p.TailPitch = (p.TailYaw = (p.TailSway = (p.TailLift = 0f)));
			p.Tuck = (p.Reach = (p.Crouch = (p.Stride = 0f)));
			p.Chest = (p.Glow = 0f);
			p.BodyPitch = (p.BodyRoll = (p.Bob = 0f));
		}

		public static void Glide(DragonPose p)
		{
			Neutral(p);
			Wings(p, 0.1f, 0.06f, 0f, 0.04f, 0.04f, 1f, 0.18f);
			p.NeckPitch = -0.72f;
			p.NeckCurl = -0.1f;
			p.HeadPitch = -0.08f;
			p.TailPitch = 0.55f;
			p.TailSway = 0.12f;
			p.Tuck = 1f;
		}

		public static void Stroke(DragonPose p, float phase, float power)
		{
			float num = Wrap(phase) / (MathF.PI * 2f);
			float num2;
			float num3;
			if (num < 0.42f)
			{
				num2 = (float)Math.Cos(num / 0.42f * MathF.PI);
				num3 = 0f;
			}
			else
			{
				float num4 = (num - 0.42f) / 0.58000004f;
				num2 = 0f - (float)Math.Cos(num4 * MathF.PI);
				num3 = (float)Math.Sin(num4 * MathF.PI);
			}
			float num5 = ((num < 0.42f) ? ((float)Math.Sin(num / 0.42f * MathF.PI)) : 0f);
			float num6 = 0.78f * power;
			for (int i = 0; i < 2; i++)
			{
				p.Elev[i] += 0.18f * power + num6 * num2;
				p.Sweep[i] += power * (0.24f * num3 - 0.16f * num5);
				p.Twist[i] += power * (0.2f * num5 - 0.22f * num3);
				p.Elbow[i] += power * 0.3f * num3;
				p.Wrist[i] += power * 0.55f * num3;
				p.Spread[i] -= power * 0.28f * num3;
				p.Billow[i] = p.Billow[i] * (1f - power) + power * (0.75f * num5 - 0.35f * num3);
			}
			p.Bob += power * 0.32f * (float)Math.Sin(num * 2f * MathF.PI - 0.4f);
			p.NeckPitch += power * 0.08f * (float)Math.Sin(num * 2f * MathF.PI - 1.4f);
			p.TailLift = 0.16f * power;
			p.Chest += power * 0.6f * num5;
		}

		public static void Folded(DragonPose p)
		{
			Neutral(p);
			Wings(p, 1.15f, 0.85f, -0.25f, 1f, 1.12f, 0f, 0f);
			p.NeckPitch = 0f;
			p.NeckCurl = 0.15f;
			p.HeadPitch = -0.12f;
			p.TailPitch = -0.05f;
			p.TailSway = 0.1f;
		}

		public static void Dive(DragonPose p)
		{
			Glide(p);
			Wings(p, 0.22f, 0.7f, 0.05f, 0.45f, 0.6f, 0.45f, 0.08f);
			p.NeckPitch = -0.95f;
			p.NeckCurl = -0.2f;
			p.TailPitch = 0.7f;
			p.TailSway = 0.04f;
		}

		public static void Flare(DragonPose p)
		{
			Glide(p);
			Wings(p, 0.38f, -0.42f, -0.45f, 0.08f, 0.05f, 1f, 0.7f);
			p.NeckPitch = -0.15f;
			p.NeckCurl = 0.25f;
			p.HeadPitch = -0.25f;
			p.TailPitch = -0.25f;
			p.Tuck = 0f;
			p.Reach = 1f;
		}

		public static void Threat(DragonPose p)
		{
			Folded(p);
			Wings(p, 0.95f, -0.05f, -0.1f, 0.28f, 0.25f, 0.85f, 0.15f);
			p.NeckPitch = 0.25f;
			p.NeckCurl = 0.35f;
			p.HeadPitch = 0.05f;
			p.TailPitch = 0.1f;
		}

		public static void Asleep(DragonPose p)
		{
			Folded(p);
			p.Crouch = 1f;
			p.NeckPitch = -1f;
			p.NeckYaw = 1.5f;
			p.NeckCurl = -0.15f;
			p.HeadPitch = -0.25f;
			p.HeadYaw = 2.3f;
			p.TailYaw = 2f;
			p.TailPitch = -0.15f;
		}

		private static float Wrap(float a)
		{
			float num = a % (MathF.PI * 2f);
			if (!(num < 0f))
			{
				return num;
			}
			return num + MathF.PI * 2f;
		}
	}
}
