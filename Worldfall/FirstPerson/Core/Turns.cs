using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	internal static class Turns
	{
		public static readonly Mat3 Ident = Mat3.Identity;

		private static bool Finite(float a)
		{
			if (!float.IsNaN(a))
			{
				return !float.IsInfinity(a);
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static Mat3 X(float a)
		{
			if (a != 0f)
			{
				return Mat3.RotX(a);
			}
			return Ident;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static Mat3 Y(float a)
		{
			if (a != 0f)
			{
				return Mat3.RotY(a);
			}
			return Ident;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static Mat3 ZX(float z, float x)
		{
			if (z == 0f && Finite(x))
			{
				return X(x);
			}
			if (x == 0f && Finite(z))
			{
				return Mat3.RotZ(z);
			}
			return Mat3.RotZ(z) * Mat3.RotX(x);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static Mat3 XY(float x, float y)
		{
			if (x == 0f && Finite(y))
			{
				return Y(y);
			}
			if (y == 0f && Finite(x))
			{
				return Mat3.RotX(x);
			}
			return Mat3.RotX(x) * Mat3.RotY(y);
		}
	}
}
