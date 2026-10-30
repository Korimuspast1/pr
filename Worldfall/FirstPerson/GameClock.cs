using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class GameClock
	{
		private const float MinStep = 0.004f;

		private const float MaxStep = 0.05f;

		private static float _ownStep = -1f;

		private static float _step;

		internal static bool OffForTest;

		public static bool Active => _ownStep > 0f;

		public static bool EffectsPaused
		{
			get
			{
				try
				{
					if (NetSession.Joined)
					{
						return WorldSync.HostPaused;
					}
					return Config.paused || (World.world != null && World.world.isPaused());
				}
				catch
				{
					return false;
				}
			}
		}

		public static void Update(bool realTime)
		{
			if (!realTime || OffForTest)
			{
				Restore();
				return;
			}
			if (_ownStep <= 0f)
			{
				_ownStep = Time.fixedDeltaTime;
				_step = _ownStep;
			}
			float num = Mathf.Clamp(Time.unscaledDeltaTime, 0.004f, 0.1f);
			bool flag = false;
			try
			{
				flag = Config.fps_lock_30;
			}
			catch
			{
			}
			if (flag)
			{
				num *= 0.5f;
			}
			_step = Mathf.Lerp(_step, num, 0.25f);
			Time.fixedDeltaTime = Mathf.Clamp(_step, 0.004f, flag ? 0.025f : 0.05f);
		}

		public static void Restore()
		{
			if (!(_ownStep <= 0f))
			{
				Time.fixedDeltaTime = _ownStep;
				_ownStep = -1f;
			}
		}
	}
}
