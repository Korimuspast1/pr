using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class PelletTrail
	{
		public Vec3 From;

		public float DirX;

		public float DirY;

		public float Rise;

		public float Drop;

		public float Flat;

		public float Head;

		public bool Ended;

		public float EndedFor;

		public bool Spent;

		public static float Seconds = 0.05f;

		public static float Width = 0.016f;

		public static float FadeIn = 0.6f;

		public bool Done
		{
			get
			{
				if (Ended)
				{
					return EndedFor >= (Spent ? 0.12f : Seconds);
				}
				return false;
			}
		}

		public Vec3 At(float d)
		{
			return new Vec3(From.X + DirX * d, From.Y + DirY * d, From.Z + d * Rise - d * d * Drop);
		}

		public void Reach(float along)
		{
			if (!Ended)
			{
				Head = Math.Max(Head, along);
			}
		}

		public void End(float along, bool spent = false)
		{
			if (!Ended)
			{
				Head = Math.Max(Head, along);
				Ended = true;
				Spent = spent;
				EndedFor = 0f;
			}
		}

		public void Step(float dt)
		{
			if (Ended)
			{
				EndedFor += dt;
			}
		}

		public void Emit(List<Streak> into)
		{
			float num = Math.Max(0.6f, Math.Min(3.2f, Flat * Seconds));
			float num2 = ((Ended && !Spent) ? (Flat * EndedFor) : 0f);
			float num3 = ((Ended && Spent) ? Math.Max(0f, 1f - EndedFor / 0.12f) : 1f);
			float num4 = Math.Max(0f, Head - num + num2);
			if (!(Head - num4 < 0.02f) && !(num3 <= 0f))
			{
				float num5 = Math.Max(num4, Head - num * 0.3f);
				float num6 = Math.Min(1f, 0.2f + 0.8f * Head / FadeIn);
				Vec3 b = At(Head);
				Vec3 vec = At(num5);
				Vec3 a = At(num4);
				float alphaB = 1f * num6 * num3;
				float num7 = 0.85f * num6 * num3 * Math.Min(1f, (Head - num4) / (num * 0.3f + 0.001f));
				into.Add(new Streak
				{
					A = vec,
					B = b,
					AlphaA = num7,
					AlphaB = alphaB,
					Color = ShotgunFx.PelletHead,
					Width = Width,
					Glow = 1f,
					Edge = 0.15f
				});
				if (num5 - num4 > 0.02f)
				{
					into.Add(new Streak
					{
						A = a,
						B = vec,
						AlphaA = 0f,
						AlphaB = num7 * 0.9f,
						Color = ShotgunFx.PelletTail,
						Width = Width * 0.8f,
						Glow = 1f,
						Edge = 0.1f
					});
				}
			}
		}
	}
}
