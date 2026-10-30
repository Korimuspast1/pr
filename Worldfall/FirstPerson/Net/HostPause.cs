using System;
using System.Reflection;

namespace FirstPerson.Net
{
	internal static class HostPause
	{
		private static bool _installed;

		private static bool _failed;

		private static bool _toldTester;

		public static void Install()
		{
			if (_installed || _failed)
			{
				return;
			}
			try
			{
				MethodInfo method = typeof(AutoTesterBot).GetMethod("update", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(float) }, null);
				MethodInfo method2 = typeof(HostPause).GetMethod("Update", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Detour.Apply(method, method2);
				_installed = true;
				Log.Info("NET the host's world keeps running under the game's windows");
			}
			catch (Exception ex)
			{
				_failed = true;
				Log.Error("NET could not keep the world running under windows (it pauses while the host has one open): " + ex.Message);
			}
		}

		private static void Update(AutoTesterBot self, float elapsed)
		{
			try
			{
				if (self != null && self.active && !_toldTester)
				{
					_toldTester = true;
					Log.Info("NET the game's test bot is off while Worldfall's multiplayer is installed");
				}
				NetSession current = NetSession.Current;
				if (current == null || !current.IsHost || World.world == null)
				{
					return;
				}
				bool flag = false;
				foreach (NetPlayer value in current.Players.Values)
				{
					if (!value.Host && value.Ready)
					{
						flag = true;
						break;
					}
				}
				if (flag && World.world._is_paused && !Config.paused)
				{
					World.world._is_paused = false;
				}
			}
			catch
			{
			}
		}
	}
}
