using UnityEngine;

namespace FirstPerson
{
	internal static class Doorstep
	{
		internal static int SteppedOut;

		internal static bool On = true;

		internal static bool Drawn(Actor a, out Vector2 at)
		{
			at = default(Vector2);
			if (!On || a == null || RoomCombat.In(a))
			{
				return false;
			}
			return ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.SceneForTest : null)?.DrawnWalkingIn(a, out at) ?? false;
		}

		internal static void StepOut(Actor a)
		{
			try
			{
				if (On && a != null && a.isAlive() && a.is_inside_building && !RoomCombat.In(a))
				{
					SceneCollector sceneCollector = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.SceneForTest : null);
					if (sceneCollector != null && sceneCollector.DrawnWalkingIn(a, out var at))
					{
						a.exitBuilding();
						a.current_position = at;
						a.dirty_current_tile = true;
						SteppedOut++;
					}
				}
			}
			catch
			{
			}
		}
	}
}
