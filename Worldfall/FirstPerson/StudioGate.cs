using System;
using System.Threading;
using FMOD;
using FMOD.Studio;
using FMODUnity;

namespace FirstPerson
{
	internal static class StudioGate
	{
		private static readonly object _gate = new object();

		private static FMOD.Studio.SYSTEM_CALLBACK _callback;

		private static IntPtr _installedOn;

		[ThreadStatic]
		private static bool _studioHolds;

		public static int StudioMisses;

		public static int FrameMisses;

		public static bool Installed { get; private set; }

		public static bool Enter(int waitMs = 50)
		{
			Install();
			if (Monitor.TryEnter(_gate, waitMs))
			{
				return true;
			}
			FrameMisses++;
			return false;
		}

		public static void Exit()
		{
			Monitor.Exit(_gate);
		}

		private static void Install()
		{
			FMOD.Studio.System studioSystem = RuntimeManager.StudioSystem;
			if (studioSystem.isValid() && !(studioSystem.handle == _installedOn))
			{
				if (_callback == null)
				{
					_callback = OnUpdate;
				}
				RESULT rESULT = studioSystem.setCallback(_callback, FMOD.Studio.SYSTEM_CALLBACK_TYPE.PREUPDATE | FMOD.Studio.SYSTEM_CALLBACK_TYPE.POSTUPDATE);
				_installedOn = studioSystem.handle;
				Installed = rESULT == RESULT.OK;
				if (!Installed)
				{
					Log.Error("audio: Studio update gate not set (" + rESULT.ToString() + ")");
				}
			}
		}

		private static RESULT OnUpdate(IntPtr system, FMOD.Studio.SYSTEM_CALLBACK_TYPE type, IntPtr data, IntPtr user)
		{
			try
			{
				switch (type)
				{
				case FMOD.Studio.SYSTEM_CALLBACK_TYPE.PREUPDATE:
					if (_studioHolds)
					{
						return RESULT.OK;
					}
					if (Monitor.TryEnter(_gate, 250))
					{
						_studioHolds = true;
					}
					else
					{
						StudioMisses++;
					}
					break;
				case FMOD.Studio.SYSTEM_CALLBACK_TYPE.POSTUPDATE:
					if (_studioHolds)
					{
						_studioHolds = false;
						Monitor.Exit(_gate);
					}
					break;
				}
			}
			catch
			{
			}
			return RESULT.OK;
		}
	}
}
