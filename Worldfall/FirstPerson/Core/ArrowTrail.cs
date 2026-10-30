using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class ArrowTrail
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

		public static float Seconds = 0.2f;

		public static float Peak = 0.95f;

		public static float Width = 0.028f;

		public static float FadeIn = 0.8f;

		public static float Glow = 0.35f;

		public static float Edge = 0.45f;

		public static uint Color = TerrainCache.Rgb(255, 249, 232);

		public const float NockBehind = 0.42f;

		public bool Done
		{
			get
			{
				if (Ended)
				{
					return EndedFor >= Seconds;
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
				Head = Math.Max(Head, along - 0.42f);
			}
		}

		public void End(float along)
		{
			if (!Ended)
			{
				Head = Math.Max(Head, along - 0.21f);
				Ended = true;
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
			float num = Math.Max(0.5f, Flat * Seconds);
			float num2 = (Ended ? (Flat * EndedFor) : 0f);
			float num3 = Math.Max(0f, Head - num + num2);
			if (Head - num3 < 0.02f)
			{
				return;
			}
			int num4 = Math.Max(1, Math.Min(48, (int)Math.Ceiling((Head - num3) / 0.35f)));
			float num5 = (Head - num3) / (float)num4;
			Vec3 a = At(num3);
			float num6 = Alpha(num3, num, num2);
			for (int i = 1; i <= num4; i++)
			{
				float d = num3 + num5 * (float)i;
				Vec3 vec = At(d);
				float num7 = Alpha(d, num, num2);
				if (num6 > 0.004f || num7 > 0.004f)
				{
					into.Add(new Streak
					{
						A = a,
						B = vec,
						AlphaA = num6,
						AlphaB = num7,
						Color = Color,
						Width = Width,
						Glow = Glow,
						Edge = Edge
					});
				}
				a = vec;
				num6 = num7;
			}
		}

		private float Alpha(float d, float length, float drained)
		{
			float num = (Head - d + drained) / length;
			if (num >= 1f)
			{
				return 0f;
			}
			float num2 = 1f - Math.Max(0f, num);
			float num3 = Math.Min(1f, 0.25f + 0.75f * d / FadeIn);
			return Peak * num2 * num3;
		}
	}
}
