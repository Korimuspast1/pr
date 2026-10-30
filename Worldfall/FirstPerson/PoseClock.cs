using UnityEngine;

namespace FirstPerson
{
	internal static class PoseClock
	{
		private static bool _frozen;

		private static float _at;

		public static float Now
		{
			get
			{
				if (!_frozen)
				{
					return Time.time;
				}
				return _at;
			}
		}

		public static bool Frozen => _frozen;

		public static void Freeze(float at)
		{
			_at = at;
			_frozen = true;
		}

		public static void Thaw()
		{
			_frozen = false;
		}
	}
}
