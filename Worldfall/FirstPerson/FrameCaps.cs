using UnityEngine;

namespace FirstPerson
{
	internal static class FrameCaps
	{
		public static readonly string[] Names = new string[7] { "Game's own", "30", "60", "90", "120", "144", "No cap" };

		public static readonly int[] Rates = new int[7] { 0, 30, 60, 90, 120, 144, -1 };

		private static bool _held;

		private static int _own;

		public static void Update(bool drawn3D, int cap)
		{
			int num = ((cap > 0 && cap < Rates.Length) ? Rates[cap] : 0);
			if (!drawn3D || num == 0)
			{
				Restore();
				return;
			}
			if (!_held)
			{
				_own = Application.targetFrameRate;
				_held = true;
			}
			if (Application.targetFrameRate != num)
			{
				Application.targetFrameRate = num;
			}
		}

		public static void Restore()
		{
			if (_held)
			{
				_held = false;
				Application.targetFrameRate = _own;
			}
		}
	}
}
