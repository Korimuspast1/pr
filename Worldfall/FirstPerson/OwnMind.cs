using System;
using System.Reflection;

namespace FirstPerson
{
	internal static class OwnMind
	{
		private const int StrikeIlBytes = 33;

		private const int LookIlBytes = 39;

		private static bool _installed;

		internal static bool Enabled = true;

		internal static bool PossessedFirst = true;

		internal static bool Verify;

		internal static int Compared;

		internal static int Differed;

		internal static string First;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo original = Step("b2_checkCurrentEnemyTarget", 33, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo original2 = Step("b3_findEnemyTarget", 39, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				Detour.Apply(original, typeof(OwnMind).GetMethod("Strike", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(original2, typeof(OwnMind).GetMethod("Look", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static MethodInfo Step(string name, int ilBytes, BindingFlags flags)
		{
			MethodInfo method = typeof(Actor).GetMethod(name, flags, null, new Type[1] { typeof(float) }, null);
			int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
			if (num != ilBytes)
			{
				throw new InvalidOperationException("the game's " + name + " has changed (" + num + " bytes of IL, not " + ilBytes + ")");
			}
			return method;
		}

		private static bool Yours(Actor self)
		{
			if (!Enabled)
			{
				return false;
			}
			Actor unit_main = ControllableUnit._unit_main;
			if (unit_main == null)
			{
				return false;
			}
			if (PossessedFirst)
			{
				return Controlled(self, unit_main);
			}
			if (self != unit_main)
			{
				if (ControllableUnit._units.Contains(self))
				{
					return self._has_status_possessed;
				}
				return false;
			}
			return true;
		}

		internal static bool Controlled(Actor a, Actor main)
		{
			bool flag = a == main || (a._has_status_possessed && ControllableUnit._units.Contains(a));
			if (Verify)
			{
				bool flag2 = a == main || (ControllableUnit._units.Contains(a) && a._has_status_possessed);
				Compared++;
				if (flag2 != flag)
				{
					Differed++;
					if (First == null)
					{
						First = "unit " + ((a.data != null) ? a.data.id.ToString() : "?") + ": " + flag + " vs " + flag2;
					}
				}
			}
			return flag;
		}

		private static void Strike(Actor self, float elapsed)
		{
			if (!self._update_done && !self._beh_skip && !Yours(self) && !(WarMap.Ordered(self, out var firing) && firing) && self.checkCurrentEnemyTarget())
			{
				self.skipBehaviour();
			}
		}

		private static void Look(Actor self, float elapsed)
		{
			if (!self._update_done && !self._beh_skip && !Yours(self) && !WarMap.Ordered(self, out var _) && self.checkEnemyTargets())
			{
				if (WarMap.Spares(self))
				{
					self.clearAttackTarget();
					return;
				}
				self.stopMovement();
				self.skipBehaviour();
			}
		}
	}
}
