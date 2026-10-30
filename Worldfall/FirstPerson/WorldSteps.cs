using System;
using System.Reflection;

namespace FirstPerson
{
	internal static class WorldSteps
	{
		private const int GameIlBytes = 46;

		private static bool _installed;

		private static bool _tried;

		private static bool _skip;

		private static bool _double;

		private static int _parity;

		internal static long Skipped;

		internal static long Doubled;

		internal static bool Active { get; private set; }

		internal static void BeginFrame(bool wanted)
		{
			if (wanted && !_installed && !_tried)
			{
				Install();
			}
			if (!wanted || !_installed)
			{
				Active = (_skip = (_double = false));
				return;
			}
			Active = true;
			_parity ^= 1;
			_skip = _parity == 1;
			_double = !_skip;
		}

		private static void Install()
		{
			_tried = true;
			if (!Detour.Begin())
			{
				return;
			}
			try
			{
				MethodInfo method = typeof(MapBox).GetMethod("checkMainSimulationUpdate", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 46)
				{
					throw new InvalidOperationException("the game's checkMainSimulationUpdate has changed (" + num + " bytes of IL, not " + 46 + ")");
				}
				Detour.Apply(method, typeof(WorldSteps).GetMethod("CheckMainSimulationUpdate", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
				Log.Info("world steps: the world can be stepped 30 times a second in the 3D view (F2 Graphics, World steps)");
			}
			catch (Exception ex)
			{
				Log.Error("world steps: could not take over the world's step, it steps every frame: " + ex.Message);
			}
			finally
			{
				Detour.End();
			}
		}

		private static void CheckMainSimulationUpdate(MapBox self)
		{
			int num = (ScrollWindow.isWindowActive() ? 1 : Config.time_scale_asset.ticks);
			if (_skip)
			{
				Skipped++;
				return;
			}
			if (!_double)
			{
				for (int i = 0; i < num; i++)
				{
					self.updateSimulation(self.elapsed);
				}
				return;
			}
			float elapsed = self.elapsed;
			self.elapsed = elapsed * 2f;
			try
			{
				for (int j = 0; j < num; j++)
				{
					self.updateSimulation(self.elapsed);
				}
			}
			finally
			{
				self.elapsed = elapsed;
			}
			Doubled++;
		}
	}
}
