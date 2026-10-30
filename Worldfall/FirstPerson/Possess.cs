using System;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class Possess
	{
		private static float _keptUntil;

		private static Actor _kept;

		internal static bool KeepEnabled = true;

		internal static bool CanJumpTo(Actor target)
		{
			try
			{
				if (target == null || !target.isAlive() || target.asset == null)
				{
					return false;
				}
				if (target.is_inside_building || target.is_inside_boat || !target.canBePossessed())
				{
					return false;
				}
				if (target.asset.id == "crabzilla")
				{
					return false;
				}
				if (ControllableUnit.isControllingCrabzilla())
				{
					return false;
				}
				if (target == ControllableUnit._unit_main)
				{
					return false;
				}
				if (NetSession.Current != null && WorldSync.IsOtherPlayersBody(target))
				{
					return false;
				}
				if (World.world == null)
				{
					return false;
				}
				WorldBoxMod instance = WorldBoxMod.Instance;
				return instance == null || !instance.IsFirstPerson || instance.CanLeap;
			}
			catch
			{
				return false;
			}
		}

		internal static bool JumpTo(Actor target)
		{
			if (!CanJumpTo(target))
			{
				return false;
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null && instance.IsFirstPerson)
			{
				return instance.LeapInto(target);
			}
			return Swap(target);
		}

		internal static bool Swap(Actor target)
		{
			Actor unit_main = ControllableUnit._unit_main;
			try
			{
				ControllableUnit.setControllableCreature(target);
				if (ControllableUnit._unit_main != target)
				{
					return false;
				}
				if (unit_main != null && unit_main != target)
				{
					ControllableUnit.remove(unit_main);
					Release(unit_main);
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("could not take over " + WorldBoxMod.Describe(target) + ": " + ex.Message);
				return false;
			}
		}

		internal static void Keep(Actor host)
		{
			try
			{
				if (host == null || !host.isAlive() || !KeepEnabled)
				{
					return;
				}
				float unscaledTime = Time.unscaledTime;
				bool flag = !((BaseSimObject)host).hasStatus("possessed");
				if (flag || host != _kept || !(unscaledTime < _keptUntil))
				{
					((BaseSimObject)host).addStatusEffect("possessed", 10f, pColorEffect: false);
					if (flag)
					{
						host.cancelAllBeh();
					}
					_kept = host;
					_keptUntil = unscaledTime + 2f;
				}
			}
			catch (Exception ex)
			{
				if (_kept != null)
				{
					Log.Error("could not keep hold of " + WorldBoxMod.Describe(host) + ": " + ex.Message);
				}
				_kept = null;
			}
		}

		private static void Release(Actor old)
		{
			try
			{
				if (old.isAlive())
				{
					old.finishStatusEffect("possessed");
					old.setPossessedMovement(pValue: false);
					old.cancelAllBeh();
				}
			}
			catch (Exception ex)
			{
				Log.Error("letting go of " + WorldBoxMod.Describe(old) + ": " + ex.Message);
			}
		}
	}
}
