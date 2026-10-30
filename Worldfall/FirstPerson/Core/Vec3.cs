using System;

namespace FirstPerson.Core
{
	public struct Vec3
	{
		public float X;

		public float Y;

		public float Z;

		public Vec3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static Vec3 operator +(Vec3 a, Vec3 b)
		{
			return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
		}

		public static Vec3 operator -(Vec3 a, Vec3 b)
		{
			return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
		}

		public static Vec3 operator *(Vec3 a, float s)
		{
			return new Vec3(a.X * s, a.Y * s, a.Z * s);
		}

		public static float Dot(Vec3 a, Vec3 b)
		{
			return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
		}

		public static Vec3 Cross(Vec3 a, Vec3 b)
		{
			return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
		}

		public Vec3 Normalized()
		{
			float num = (float)Math.Sqrt(X * X + Y * Y + Z * Z);
			if (!(num > 1E-06f))
			{
				return this;
			}
			return new Vec3(X / num, Y / num, Z / num);
		}

		public static Vec3 Rotate(Vec3 v, Vec3 k, float radians)
		{
			float num = (float)Math.Cos(radians);
			float num2 = (float)Math.Sin(radians);
			return v * num + Cross(k, v) * num2 + k * (Dot(k, v) * (1f - num));
		}
	}
}
