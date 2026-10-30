using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class GiveGuard
	{
		private const int GameIlBytes = 189;

		private static bool _installed;

		internal static int Kept;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(City).GetMethod("giveItem", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[3]
				{
					typeof(Actor),
					typeof(List<long>),
					typeof(City)
				}, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 189)
				{
					throw new InvalidOperationException("the game's City.giveItem has changed (" + num + " bytes of IL, not " + 189 + ")");
				}
				Detour.Apply(method, typeof(GiveGuard).GetMethod("GiveItem", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		internal static bool Guarded(Actor a)
		{
			if (a == null)
			{
				return false;
			}
			try
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (instance != null && (a == instance.Host || a == instance.Body))
				{
					return true;
				}
				if (ControllableUnit.isControllingUnit() && a == ControllableUnit._unit_main)
				{
					return true;
				}
				return WorldSync.IsPlayerBody(a);
			}
			catch
			{
				return false;
			}
		}

		private static bool GiveItem(Actor pActor, List<long> pItems, City pCity)
		{
			if (pItems.Count == 0)
			{
				return false;
			}
			if (!pActor.understandsHowToUseItems())
			{
				return false;
			}
			if (Guarded(pActor))
			{
				Kept++;
				return false;
			}
			long num = pItems[UnityEngine.Random.Range(0, pItems.Count)];
			Item item = World.world.items.get(num);
			EquipmentAsset asset = item.getAsset();
			ActorEquipmentSlot slot = pActor.equipment.getSlot(asset.equipment_type);
			if (!slot.isEmpty())
			{
				int value = slot.getItem().getValue();
				if (item.getValue() <= value)
				{
					return false;
				}
			}
			Item item2 = null;
			if (!slot.isEmpty())
			{
				item2 = slot.getItem();
				slot.takeAwayItem();
			}
			pItems.Remove(num);
			slot.setItem(item, pActor);
			pActor.setStatsDirty();
			if (item2 != null)
			{
				pCity.data.equipment.addItem(pCity, item2, pItems);
			}
			pCity._storage_version++;
			return true;
		}
	}
}
