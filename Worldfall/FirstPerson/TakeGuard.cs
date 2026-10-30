using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace FirstPerson
{
	internal static class TakeGuard
	{
		private const int GameIlBytes = 225;

		private static bool _installed;

		private static bool _reported;

		internal static int Kept;

		internal static int Bagged;

		[ThreadStatic]
		private static Random _rng;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(Actor).GetMethod("takeItems", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[3]
				{
					typeof(Actor),
					typeof(float),
					typeof(int)
				}, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 225)
				{
					throw new InvalidOperationException("the game's takeItems has changed (" + num + " bytes of IL, not " + 225 + ")");
				}
				Detour.Apply(method, typeof(TakeGuard).GetMethod("TakeItems", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static bool Mine(Actor a)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null && a != null)
			{
				if (a != instance.Host)
				{
					return a == instance.Body;
				}
				return true;
			}
			return false;
		}

		private static bool TakeItems(Actor self, Actor pActorTarget, float pChance, int pMaxItems)
		{
			if (!self.understandsHowToUseItems() || !pActorTarget.hasEquipment())
			{
				return false;
			}
			bool flag = false;
			try
			{
				flag = Mine(self);
			}
			catch
			{
			}
			if (flag && pMaxItems == 0)
			{
				Kept++;
				return false;
			}
			List<ActorEquipmentSlot> list = new List<ActorEquipmentSlot>(pActorTarget.equipment);
			if (pMaxItems == 0)
			{
				pMaxItems = list.Count;
			}
			Random random = _rng ?? (_rng = new Random(Environment.TickCount ^ (Thread.CurrentThread.ManagedThreadId * 7919)));
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = random.Next(num + 1);
				ActorEquipmentSlot value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			bool result = false;
			int num2 = 0;
			foreach (ActorEquipmentSlot item3 in list)
			{
				if (num2++ >= pMaxItems)
				{
					break;
				}
				if (item3.isEmpty())
				{
					continue;
				}
				Item item = item3.getItem();
				if (item.isCursed())
				{
					continue;
				}
				if (flag)
				{
					try
					{
						Gear.Piece piece = Gear.FromItem(item);
						if (piece != null)
						{
							item3.takeAwayItem();
							Gear.LetGo(item);
							Bag.PutPiece(self, piece, (WorldBoxMod.Instance != null) ? new Action<string>(WorldBoxMod.Instance.Toast) : null);
							Bagged++;
							pActorTarget.setStatsDirty();
							result = true;
						}
					}
					catch (Exception ex)
					{
						if (!_reported)
						{
							_reported = true;
							Log.Error("take guard: " + ex.Message);
						}
					}
					continue;
				}
				ActorEquipmentSlot slot = self.equipment.getSlot(item3.type);
				if (!slot.isEmpty())
				{
					Item item2 = slot.getItem();
					if (item2.isCursed() || item.getValue() <= item2.getValue())
					{
						continue;
					}
				}
				item3.takeAwayItem();
				slot.setItem(item, self);
				self.setStatsDirty();
				pActorTarget.setStatsDirty();
				result = true;
			}
			return result;
		}
	}
}
