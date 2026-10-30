using System;
using System.Reflection;

namespace FirstPerson
{
	internal static class DryBoards
	{
		private const int GameLoopIlBytes = 63;

		private static bool _installed;

		private static bool _warned;

		internal static bool Enabled = true;

		internal static bool KeepIndoorsDry = true;

		internal static int StepsForTest;

		public static bool Installed => _installed;

		internal static bool DocksNow
		{
			get
			{
				if (Enabled)
				{
					return DockDecks.Count > 0;
				}
				return false;
			}
		}

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(BatchActors).GetMethod("u5_curTileAction", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 63)
				{
					throw new InvalidOperationException("the game's tile step has changed (" + num + " bytes of IL, not " + 63 + ")");
				}
				Detour.Apply(method, typeof(DryBoards).GetMethod("Step", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static void Step(BatchActors self)
		{
			if (((Batch<Actor>)self).check(((Batch<Actor>)self)._cur_container) && !World.world.isPaused())
			{
				Actor[] array = ((Batch<Actor>)self)._array;
				int count = ((Batch<Actor>)self)._count;
				bool docksNow = DocksNow;
				Climbing.Watch();
				for (int i = 0; i < count; i++)
				{
					StepActor(array[i], docksNow);
				}
			}
		}

		internal static void StepActor(Actor a, bool docks)
		{
			if ((docks && OnBoards(a)) || IndoorsOverWater(a))
			{
				BoardsStep(a);
			}
			else if (Enabled && Wading.Stands(a))
			{
				WadeStep(a);
			}
			else if (!Climbing.Step(a))
			{
				a.u5_curTileAction();
			}
		}

		private static bool OnBoards(Actor a)
		{
			try
			{
				if (a == null || a._update_done || a.position_height > 0f)
				{
					return false;
				}
				TileTypeBase tileTypeBase = a.current_tile?.Type;
				if (tileTypeBase == null || !tileTypeBase.liquid || tileTypeBase.lava)
				{
					return false;
				}
				if (a.asset == null || a.asset.is_boat || a.isWaterCreature() || ((BaseSimObject)a).isFlying())
				{
					return false;
				}
				return DockDecks.On(a.current_position);
			}
			catch
			{
				return false;
			}
		}

		private static bool IndoorsOverWater(Actor a)
		{
			try
			{
				if (!KeepIndoorsDry || a == null || a._update_done || !a.is_inside_building || a.position_height > 0f)
				{
					return false;
				}
				TileTypeBase tileTypeBase = ((a.current_tile != null) ? a.current_tile.Type : null);
				return tileTypeBase != null && tileTypeBase.liquid && !tileTypeBase.lava && a.asset != null && !a.asset.is_boat && !a.isWaterCreature();
			}
			catch
			{
				return false;
			}
		}

		private static void WadeStep(Actor a)
		{
			try
			{
				if (!Wading.Frozen(a.current_tile) && ((BaseSimObject)a).hasStatus("burning"))
				{
					a.finishStatusEffect("burning");
				}
			}
			catch
			{
			}
			BoardsStep(a);
		}

		private static void BoardsStep(Actor a)
		{
			StepsForTest++;
			try
			{
				WorldTile current_tile = a.current_tile;
				TileTypeBase type = current_tile.Type;
				if (type.damage_units && !a.isUnderDamageCooldown())
				{
					((BaseSimObject)a).getHit((float)type.damage, pFlash: true, AttackType.Fire, (BaseSimObject)null, pSkipIfShake: true, pMetallicWeapon: false, pCheckDamageReduction: true);
					if (!a.hasHealth())
					{
						a.skipUpdates();
					}
				}
				if (current_tile.hasBuilding() && current_tile.building.asset.has_step_action)
				{
					current_tile.building.asset.step_action(a, current_tile.building);
					if (!a.hasHealth())
					{
						a.skipUpdates();
					}
				}
			}
			catch (Exception ex)
			{
				if (!_warned)
				{
					_warned = true;
					Log.Error("dock boards step: " + ex);
				}
			}
		}
	}
}
