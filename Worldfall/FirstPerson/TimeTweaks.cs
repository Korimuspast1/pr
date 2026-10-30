using UnityEngine;

namespace FirstPerson
{
	internal static class TimeTweaks
	{
		static float _savedDayMinutes = 20f;
		static bool _paused;

		public static void TogglePause(Actor host, System.Action<string> toast)
		{
			if (WorldBoxMod.Instance == null) return;
			var s = WorldBoxMod.Instance.Settings;
			if (s == null) return;
			if (!_paused)
			{
				_savedDayMinutes = s.DayMinutes;
				s.DayMinutes = 9999f; // effectively pause
				_paused = true;
				toast?.Invoke("Time paused (O to resume, [/] speed)");
			}
			else
			{
				s.DayMinutes = _savedDayMinutes;
				_paused = false;
				toast?.Invoke("Time resumed");
			}
			s.Save();
			Log.Info("time: " + (_paused ? "paused" : "resumed") + " dayMinutes=" + s.DayMinutes);
		}

		public static void AdjustSpeed(int dir, System.Action<string> toast)
		{
			var s = WorldBoxMod.Instance?.Settings;
			if (s == null) return;
			if (_paused) { TogglePause(null, toast); return; }
			float[] speeds = new float[] { 5f, 10f, 20f, 40f, 80f };
			int idx = 0;
			for (int i=0;i<speeds.Length;i++) if (Mathf.Abs(s.DayMinutes - speeds[i]) < 0.1f) idx = i;
			idx = Mathf.Clamp(idx + dir, 0, speeds.Length-1);
			s.DayMinutes = speeds[idx];
			s.Save();
			toast?.Invoke($"Day length: {s.DayMinutes} min");
		}
	}
}
