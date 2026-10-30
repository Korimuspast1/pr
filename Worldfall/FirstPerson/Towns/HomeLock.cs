using System;
using System.Collections.Generic;
using System.Reflection;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class HomeLock
	{
		public const string Flag = "fp_locked";

		private const int StayIlBytes = 70;

		private const int LoverIlBytes = 128;

		private static bool _installed;

		internal static int TurnedAway;

		public static bool Installed => _installed;

		public static bool Locked(Building b)
		{
			try
			{
				return b?.data.hasFlag("fp_locked") ?? false;
			}
			catch
			{
				return false;
			}
		}

		public static void SetLocked(Building b, bool locked)
		{
			if (b == null)
			{
				return;
			}
			try
			{
				if (locked)
				{
					b.data.addFlag("fp_locked");
				}
				else
				{
					b.data.removeFlag("fp_locked");
				}
			}
			catch (Exception ex)
			{
				Log.Error("home lock: " + ex.Message);
			}
		}

		public static Actor OwnerOf(Building b)
		{
			long num = HouseKeep.Held(b);
			if (num == 0L)
			{
				return null;
			}
			try
			{
				return World.world.units.get(num);
			}
			catch
			{
				return null;
			}
		}

		public static bool IsFamily(Actor a, Actor owner)
		{
			if (a == null || owner == null)
			{
				return false;
			}
			try
			{
				if (a == owner)
				{
					return true;
				}
				long id = a.data.id;
				long id2 = owner.data.id;
				if (id == id2 || a.lover == owner || owner.lover == a)
				{
					return true;
				}
				long a2 = a.data.parent_id_1;
				long a3 = a.data.parent_id_2;
				long parent_id_ = owner.data.parent_id_1;
				long parent_id_2 = owner.data.parent_id_2;
				if (a2 == id2 || a3 == id2)
				{
					return true;
				}
				if (parent_id_ == id || parent_id_2 == id)
				{
					return true;
				}
				return Shared(parent_id_) || Shared(parent_id_2);
				bool Shared(long p)
				{
					if (p > 0)
					{
						if (a2 != p)
						{
							return a3 == p;
						}
						return true;
					}
					return false;
				}
			}
			catch
			{
				return false;
			}
		}

		public static bool MayEnter(Building b, Actor a)
		{
			if (!Locked(b))
			{
				return true;
			}
			if (FamilyFollow.IsFamily(a))
			{
				return true;
			}
			try
			{
				if (a != null && b.residents != null && b.residents.Contains(a.data.id))
				{
					return true;
				}
			}
			catch
			{
			}
			Actor actor = OwnerOf(b);
			if (actor != null)
			{
				return IsFamily(a, actor);
			}
			return true;
		}

		public static List<Actor> FamilyOf(Actor owner)
		{
			List<Actor> list = new List<Actor>();
			if (owner == null)
			{
				return list;
			}
			try
			{
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					Actor actor = simpleList[i];
					if (actor != null && actor != owner && actor.isAlive() && IsFamily(actor, owner))
					{
						list.Add(actor);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("home lock: family: " + ex.Message);
			}
			return list;
		}

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(BehStayInBuildingTarget).GetMethod("execute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Actor) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 70)
				{
					throw new InvalidOperationException("the game's going into buildings has changed (" + num + " bytes of IL, not " + 70 + ")");
				}
				Detour.Apply(method, typeof(HomeLock).GetMethod("StayIn", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
				MethodInfo method2 = typeof(BehCheckForLover).GetMethod("execute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Actor) }, null);
				int num2 = method2?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num2 == 128)
				{
					Detour.Apply(method2, typeof(HomeLock).GetMethod("LoverIn", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				}
				else
				{
					Log.Error("home lock: the game's couples going in has changed (" + num2 + " bytes of IL), left as it is");
				}
			}
		}

		private static BehResult StayIn(BehStayInBuildingTarget self, Actor pActor)
		{
			Building beh_building_target = pActor.beh_building_target;
			if (beh_building_target != null && !MayEnter(beh_building_target, pActor))
			{
				TurnedAway++;
				return BehResult.Stop;
			}
			pActor.timer_action = Randy.randomFloat(self.min, self.max);
			pActor.stayInBuilding(beh_building_target);
			pActor.beh_tile_target = null;
			beh_building_target.startShake(0.5f);
			return BehResult.Continue;
		}

		private static BehResult LoverIn(BehCheckForLover self, Actor pActor)
		{
			if (!pActor.hasLover())
			{
				return BehResult.Stop;
			}
			Actor lover = pActor.lover;
			if (lover.isTask("sexual_reproduction_civ_go") && lover.beh_building_target == pActor.beh_building_target && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)lover.ai).action_index > 3)
			{
				Building beh_building_target = pActor.beh_building_target;
				if (beh_building_target != null && (!MayEnter(beh_building_target, pActor) || !MayEnter(beh_building_target, lover)))
				{
					TurnedAway++;
					return BehResult.Stop;
				}
				pActor.stayInBuilding(beh_building_target);
				lover.stayInBuilding(lover.beh_building_target);
				lover.setTask("sexual_reproduction_civ_wait", pClean: false, pCleanJob: false, pForceAction: true);
				return self.forceTask(pActor, "sexual_reproduction_civ_action", pClean: false, pForceAction: true);
			}
			if (!lover.isTask("sexual_reproduction_civ_go"))
			{
				return BehResult.Stop;
			}
			return BehResult.Continue;
		}
	}
}
