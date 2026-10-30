using System;
using System.Reflection;

namespace FirstPerson
{
	internal static class WadeChecks
	{
		private const int GameLoopIlBytes = 56;

		private static bool _installed;

		private static bool _warned;

		internal static bool Enabled = true;

		internal static int ClearedForTest;

		private static volatile bool _iceHeld;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(BatchActors).GetMethod("updateParallelChecks", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 56)
				{
					throw new InvalidOperationException("the game's per-creature checks have changed (" + num + " bytes of IL, not " + 56 + ")");
				}
				Detour.Apply(method, typeof(WadeChecks).GetMethod("Step", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static void Step(BatchActors self)
		{
			if (!((Batch<Actor>)self).check(((Batch<Actor>)self)._cur_container))
			{
				return;
			}
			if (!_iceHeld)
			{
				HoldIce();
			}
			Actor[] array = ((Batch<Actor>)self)._array;
			int count = ((Batch<Actor>)self)._count;
			float elapsed = ((Batch<Actor>)self)._elapsed;
			bool enabled = Enabled;
			for (int i = 0; i < count; i++)
			{
				Actor actor = array[i];
				actor.updateParallelChecks(elapsed);
				if (enabled && actor._is_in_liquid)
				{
					Clear(actor);
				}
			}
		}

		private static void Clear(Actor a)
		{
			try
			{
				if (Wading.Stands(a) && !a.isDamagedByOcean())
				{
					a._is_in_liquid = false;
					ClearedForTest++;
				}
			}
			catch (Exception ex)
			{
				if (!_warned)
				{
					_warned = true;
					Log.Error("wading checks: " + ex.Message);
				}
			}
		}

		private static void HoldIce()
		{
			_iceHeld = true;
			try
			{
				TileTypeBase tileTypeBase = AssetManager.top_tiles.get("ice");
				if (tileTypeBase != null)
				{
					tileTypeBase.damaged_when_walked = false;
				}
			}
			catch (Exception ex)
			{
				Log.Error("ice: could not make it hold: " + ex.Message);
			}
		}
	}
}
