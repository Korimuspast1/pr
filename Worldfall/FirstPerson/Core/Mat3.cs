using System;

namespace FirstPerson.Core
{
	public struct Mat3
	{
		public Vec3 C0;

		public Vec3 C1;

		public Vec3 C2;

		public static bool Fast = true;

		private static readonly Mat3 _identity = new Mat3(new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f));

		public static Mat3 Identity
		{
			get
			{
				if (!Fast)
				{
					return IdentityOld;
				}
				return _identity;
			}
		}

		public static Mat3 IdentityOld => new Mat3(new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f));

		public Mat3(Vec3 c0, Vec3 c1, Vec3 c2)
		{
			C0 = c0;
			C1 = c1;
			C2 = c2;
		}

		public Vec3 Mul(Vec3 v)
		{
			if (!Fast)
			{
				return MulOld(v);
			}
			float x = v.X;
			float y = v.Y;
			float z = v.Z;
			Vec3 vec = default(Vec3);
			vec.X = C0.X * x + C1.X * y + C2.X * z;
			vec.Y = C0.Y * x + C1.Y * y + C2.Y * z;
			vec.Z = C0.Z * x + C1.Z * y + C2.Z * z;
			if (Mat3Verify.On)
			{
				Mat3Verify.Mul(this, v, vec);
			}
			return vec;
		}

		public static Mat3 operator *(in Mat3 a, in Mat3 b)
		{
			if (!Fast)
			{
				return ProductOld(a, b);
			}
			float x = a.C0.X;
			float y = a.C0.Y;
			float z = a.C0.Z;
			float x2 = a.C1.X;
			float y2 = a.C1.Y;
			float z2 = a.C1.Z;
			float x3 = a.C2.X;
			float y3 = a.C2.Y;
			float z3 = a.C2.Z;
			float x4 = b.C0.X;
			float y4 = b.C0.Y;
			float z4 = b.C0.Z;
			Mat3 mat = default(Mat3);
			mat.C0.X = x * x4 + x2 * y4 + x3 * z4;
			mat.C0.Y = y * x4 + y2 * y4 + y3 * z4;
			mat.C0.Z = z * x4 + z2 * y4 + z3 * z4;
			x4 = b.C1.X;
			y4 = b.C1.Y;
			z4 = b.C1.Z;
			mat.C1.X = x * x4 + x2 * y4 + x3 * z4;
			mat.C1.Y = y * x4 + y2 * y4 + y3 * z4;
			mat.C1.Z = z * x4 + z2 * y4 + z3 * z4;
			x4 = b.C2.X;
			y4 = b.C2.Y;
			z4 = b.C2.Z;
			mat.C2.X = x * x4 + x2 * y4 + x3 * z4;
			mat.C2.Y = y * x4 + y2 * y4 + y3 * z4;
			mat.C2.Z = z * x4 + z2 * y4 + z3 * z4;
			if (Mat3Verify.On)
			{
				Mat3Verify.Product(a, b, mat);
			}
			return mat;
		}

		public static Mat3 operator *(in Mat3 a, float s)
		{
			if (!Fast)
			{
				return ScaledOld(a, s);
			}
			float num = s;
			Mat3 mat = default(Mat3);
			mat.C0.X = a.C0.X * num;
			mat.C0.Y = a.C0.Y * num;
			mat.C0.Z = a.C0.Z * num;
			mat.C1.X = a.C1.X * num;
			mat.C1.Y = a.C1.Y * num;
			mat.C1.Z = a.C1.Z * num;
			mat.C2.X = a.C2.X * num;
			mat.C2.Y = a.C2.Y * num;
			mat.C2.Z = a.C2.Z * num;
			if (Mat3Verify.On)
			{
				Mat3Verify.Scaled(a, s, mat);
			}
			return mat;
		}

		public static Mat3 Rotation(Vec3 axis, float angle)
		{
			return new Mat3(Vec3.Rotate(new Vec3(1f, 0f, 0f), axis, angle), Vec3.Rotate(new Vec3(0f, 1f, 0f), axis, angle), Vec3.Rotate(new Vec3(0f, 0f, 1f), axis, angle));
		}

		public static Mat3 RotX(float a)
		{
			if (!Fast)
			{
				return RotXOld(a);
			}
			float num = a;
			Mat3 identity = default(Mat3);
			if (num == 0f)
			{
				identity = _identity;
			}
			else
			{
				float num2 = (float)Math.Cos(num);
				float num3 = (float)Math.Sin(num);
				if (num2 != num2 || num3 != num3 || num2 == 0f)
				{
					return RotXOld(a);
				}
				identity.C0.X = num2 + (1f - num2);
				identity.C0.Y = 0f;
				identity.C0.Z = 0f;
				identity.C1.X = 0f;
				identity.C1.Y = num2;
				identity.C1.Z = num3 + 0f;
				identity.C2.X = 0f;
				identity.C2.Y = 0f - num3 + 0f;
				identity.C2.Z = num2;
			}
			if (Mat3Verify.On)
			{
				Mat3Verify.Turn(0, a, identity);
			}
			return identity;
		}

		public static Mat3 RotY(float a)
		{
			if (!Fast)
			{
				return RotYOld(a);
			}
			float num = a;
			Mat3 identity = default(Mat3);
			if (num == 0f)
			{
				identity = _identity;
			}
			else
			{
				float num2 = (float)Math.Cos(num);
				float num3 = (float)Math.Sin(num);
				if (num2 != num2 || num3 != num3 || num2 == 0f)
				{
					return RotYOld(a);
				}
				identity.C0.X = num2;
				identity.C0.Y = 0f;
				identity.C0.Z = 0f - num3 + 0f;
				identity.C1.X = 0f;
				identity.C1.Y = num2 + (1f - num2);
				identity.C1.Z = 0f;
				identity.C2.X = num3 + 0f;
				identity.C2.Y = 0f;
				identity.C2.Z = num2;
			}
			if (Mat3Verify.On)
			{
				Mat3Verify.Turn(1, a, identity);
			}
			return identity;
		}

		public static Mat3 RotZ(float a)
		{
			if (!Fast)
			{
				return RotZOld(a);
			}
			float num = a;
			Mat3 identity = default(Mat3);
			if (num == 0f)
			{
				identity = _identity;
			}
			else
			{
				float num2 = (float)Math.Cos(num);
				float num3 = (float)Math.Sin(num);
				if (num2 != num2 || num3 != num3 || num2 == 0f)
				{
					return RotZOld(a);
				}
				identity.C0.X = num2;
				identity.C0.Y = num3 + 0f;
				identity.C0.Z = 0f;
				identity.C1.X = 0f - num3 + 0f;
				identity.C1.Y = num2;
				identity.C1.Z = 0f;
				identity.C2.X = 0f;
				identity.C2.Y = 0f;
				identity.C2.Z = num2 + (1f - num2);
			}
			if (Mat3Verify.On)
			{
				Mat3Verify.Turn(2, a, identity);
			}
			return identity;
		}

		public Vec3 MulOld(Vec3 v)
		{
			return C0 * v.X + C1 * v.Y + C2 * v.Z;
		}

		public static Mat3 ProductOld(Mat3 a, Mat3 b)
		{
			return new Mat3(a.MulOld(b.C0), a.MulOld(b.C1), a.MulOld(b.C2));
		}

		public static Mat3 ScaledOld(Mat3 a, float s)
		{
			return new Mat3(a.C0 * s, a.C1 * s, a.C2 * s);
		}

		public static Mat3 RotXOld(float a)
		{
			return Rotation(new Vec3(1f, 0f, 0f), a);
		}

		public static Mat3 RotYOld(float a)
		{
			return Rotation(new Vec3(0f, 1f, 0f), a);
		}

		public static Mat3 RotZOld(float a)
		{
			return Rotation(new Vec3(0f, 0f, 1f), a);
		}
	}
}
