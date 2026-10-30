using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class ScreenSync
	{
		private static bool _held;

		private static int _ownCount;

		internal static bool Allowed = true;

		public static void Update(bool drawn3D, bool wanted)
		{
			if (!drawn3D || !wanted || !Allowed)
			{
				Restore();
				return;
			}
			if (!_held)
			{
				_ownCount = QualitySettings.vSyncCount;
				_held = true;
			}
			int num = Refreshes();
			if (QualitySettings.vSyncCount != num)
			{
				QualitySettings.vSyncCount = num;
			}
		}

		private static int Refreshes()
		{
			bool flag = false;
			try
			{
				flag = Config.fps_lock_30;
			}
			catch
			{
			}
			if (!flag)
			{
				return 1;
			}
			return Mathf.Clamp((int)(Screen.currentResolution.refreshRateRatio.value / 30.0), 1, 4);
		}

		public static void Restore()
		{
			if (!_held)
			{
				return;
			}
			_held = false;
			try
			{
				PlayerConfig.setVsync(PlayerConfig.optionBoolEnabled("vsync"));
			}
			catch (Exception ex)
			{
				QualitySettings.vSyncCount = _ownCount;
				Log.Error("vsync: could not read the game's own option, put back " + _ownCount + ": " + ex.Message);
			}
		}
	}
}
