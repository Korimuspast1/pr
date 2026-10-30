using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class FastMath
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int FloorInt(float v)
		{
			int num = (int)v;
			if (!(v < (float)num))
			{
				return num;
			}
			return num - 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CeilInt(float v)
		{
			int num = (int)v;
			if (!(v > (float)num))
			{
				return num;
			}
			return num + 1;
		}
	}
}
