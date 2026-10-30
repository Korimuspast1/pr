using System;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal struct Hitbox
	{
		public Vec3 Centre;

		public float AX;

		public float AY;

		public float HalfA;

		public float HalfB;

		public float HalfUp;

		public float Bottom => Centre.Z - HalfUp;

		public float Top => Centre.Z + HalfUp;

		public bool Segment(Vec3 a, Vec3 b, float pad, out float t)
		{
			t = 0f;
			float num = a.X - Centre.X;
			float num2 = a.Y - Centre.Y;
			float o = a.Z - Centre.Z;
			float num3 = b.X - a.X;
			float num4 = b.Y - a.Y;
			float d = b.Z - a.Z;
			float o2 = num * AX + num2 * AY;
			float o3 = (0f - num) * AY + num2 * AX;
			float d2 = num3 * AX + num4 * AY;
			float d3 = (0f - num3) * AY + num4 * AX;
			float t2 = 0f;
			float t3 = 1f;
			if (Slab(o2, d2, HalfA + pad, ref t2, ref t3) && Slab(o3, d3, HalfB + pad, ref t2, ref t3) && Slab(o, d, HalfUp + pad, ref t2, ref t3))
			{
				return (t = t2) >= 0f;
			}
			return false;
		}

		private static bool Slab(float o, float d, float half, ref float t0, ref float t1)
		{
			if (Math.Abs(d) < 1E-07f)
			{
				if (o >= 0f - half)
				{
					return o <= half;
				}
				return false;
			}
			float num = 1f / d;
			float num2 = (0f - half - o) * num;
			float num3 = (half - o) * num;
			if (num2 > num3)
			{
				float num4 = num2;
				num2 = num3;
				num3 = num4;
			}
			if (num2 > t0)
			{
				t0 = num2;
			}
			if (num3 < t1)
			{
				t1 = num3;
			}
			return t0 <= t1;
		}

		public float FlatDistance(float x, float y, out Vector2 nearest)
		{
			float num = x - Centre.X;
			float num2 = y - Centre.Y;
			float num3 = num * AX + num2 * AY;
			float num4 = (0f - num) * AY + num2 * AX;
			float num5 = Mathf.Clamp(num3, 0f - HalfA, HalfA);
			float num6 = Mathf.Clamp(num4, 0f - HalfB, HalfB);
			nearest = new Vector2(Centre.X + num5 * AX - num6 * AY, Centre.Y + num5 * AY + num6 * AX);
			float num7 = num3 - num5;
			float num8 = num4 - num6;
			return Mathf.Sqrt(num7 * num7 + num8 * num8);
		}
	}
}
