using System;
using System.Reflection;
using FirstPerson.Core;
using ai;

namespace FirstPerson
{
	internal static class ZombieGear
	{
		private const int GameIlBytes = 134;

		public const string Key = "fp_wore";

		public const string LookKey = "fp_looked";

		private static bool _installed;

		private static bool _reported;

		internal static int Dressed;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(ActorTool).GetMethod("copyUnitToOtherUnit", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[3]
				{
					typeof(Actor),
					typeof(Actor),
					typeof(bool)
				}, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 134)
				{
					throw new InvalidOperationException("the game's copyUnitToOtherUnit has changed (" + num + " bytes of IL, not " + 134 + ")");
				}
				Detour.Apply(method, typeof(ZombieGear).GetMethod("CopyUnitToOtherUnit", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static void CopyUnitToOtherUnit(Actor pParent, Actor pCloneTarget, bool pCopyAge)
		{
			try
			{
				Note(pParent, pCloneTarget);
			}
			catch (Exception ex)
			{
				if (!_reported)
				{
					_reported = true;
					Log.Error("zombie gear: " + ex.Message);
				}
			}
			pCloneTarget.current_position = pParent.current_position;
			((BaseSimObject)pCloneTarget).current_rotation = ((BaseSimObject)pParent).current_rotation;
			ActorTool.copyImportantData(pParent.data, pCloneTarget.data, pCopyAge);
			pCloneTarget.takeItems(pParent);
			foreach (ActorTrait trait in pParent.getTraits())
			{
				pCloneTarget.addTrait(trait);
			}
			pCloneTarget.setStatsDirty();
			if (MoveCamera.inSpectatorMode() && pParent.isCameraFollowingUnit())
			{
				MoveCamera.setFocusUnit(pCloneTarget);
			}
		}

		private static void Note(Actor living, Actor dead)
		{
			if (living == null || dead == null || dead.asset == null || dead.data == null || !dead.asset.unit_zombie)
			{
				return;
			}
			if (People.IsHumanoid(living) && People.ColorsOf(living, out var p))
			{
				dead.data.set("fp_looked", ZombieColors.Write(p));
			}
			if (living.equipment != null && living.hasEquipment())
			{
				string text = People.ItemIdOf(living.equipment.helmet);
				string text2 = People.ItemIdOf(living.equipment.armor);
				string text3 = People.ItemIdOf(living.equipment.boots);
				if (text != null || text2 != null || text3 != null)
				{
					dead.data.set("fp_wore", text + "|" + text2 + "|" + text3);
					Dressed++;
				}
			}
		}

		public static ZombieColors.Living Looked(Actor a)
		{
			string pResult = null;
			try
			{
				a.data.get("fp_looked", out pResult, null);
			}
			catch
			{
			}
			return ZombieColors.Read(pResult);
		}

		public static bool Wore(Actor a, out string helmet, out string chest, out string boots)
		{
			helmet = (chest = (boots = null));
			string pResult = null;
			try
			{
				a.data.get("fp_wore", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return false;
			}
			string[] array = pResult.Split('|');
			helmet = ((array.Length != 0 && array[0].Length > 0) ? array[0] : null);
			chest = ((array.Length > 1 && array[1].Length > 0) ? array[1] : null);
			boots = ((array.Length > 2 && array[2].Length > 0) ? array[2] : null);
			if (helmet == null && chest == null)
			{
				return boots != null;
			}
			return true;
		}
	}
}
