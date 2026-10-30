using System;
using System.Reflection;

namespace FirstPerson
{
	internal static class LoveGuard
	{
		private const int IlBytes = 131;

		private static bool _installed;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(Actor).GetMethod("canFallInLoveWith", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Actor) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 131)
				{
					throw new InvalidOperationException("the game's canFallInLoveWith has changed (" + num + " bytes of IL, not " + 131 + ")");
				}
				Detour.Apply(method, typeof(LoveGuard).GetMethod("CanFallInLoveWith", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static bool Yours(Actor a)
		{
			Actor unit_main = ControllableUnit._unit_main;
			if (unit_main == null || a == null)
			{
				return false;
			}
			if (OwnMind.PossessedFirst)
			{
				return OwnMind.Controlled(a, unit_main);
			}
			if (a != unit_main)
			{
				if (ControllableUnit._units.Contains(a))
				{
					return a._has_status_possessed;
				}
				return false;
			}
			return true;
		}

		private static bool CanFallInLoveWith(Actor self, Actor target)
		{
			if (Yours(self) || Yours(target))
			{
				return false;
			}
			if (Quests.SpokenFor(self) || Quests.SpokenFor(target))
			{
				return false;
			}
			if (self.hasLover() || !self.isAdult() || !self.isBreedingAge())
			{
				return false;
			}
			if (!self.subspecies.needs_mate || !target.subspecies.needs_mate)
			{
				return false;
			}
			if (!self.isSameSpecies(target))
			{
				return false;
			}
			if (!self.subspecies.isPartnerSuitableForReproduction(self, target))
			{
				return false;
			}
			if (target.hasLover() || !target.isAdult() || !target.isBreedingAge())
			{
				return false;
			}
			return !self.isRelatedTo(target);
		}
	}
}
