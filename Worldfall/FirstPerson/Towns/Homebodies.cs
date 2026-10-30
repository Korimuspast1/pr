using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class Homebodies
	{
		internal const string Task = "fp_homebody";

		private const float Reach = 28f;

		private const float Walk = 30f;

		private const float Every = 2f;

		private const int SendsAtOnce = 2;

		private static float _timer;

		private static bool _registered;

		private static object _world;

		private static readonly Dictionary<Building, float> _next = new Dictionary<Building, float>();

		private static readonly List<Actor> _pick = new List<Actor>();

		private static readonly System.Random Rng = new System.Random();

		internal static bool Off;

		internal static int Sent;

		private static bool Idle(string task)
		{
			if (task != null && !task.StartsWith("wait", StringComparison.Ordinal))
			{
				return task.StartsWith("random_move", StringComparison.Ordinal);
			}
			return true;
		}

		private static float Share(float tod)
		{
			if (!(tod > 0.66f) || !(tod <= 0.8f))
			{
				if (!(tod > 0.8f) && !(tod < 0.24f))
				{
					return 0.3f;
				}
				return 0.8f;
			}
			return 0.6f;
		}

		public static void Update(Actor host, float dt)
		{
			if (Off || WorldBoxMod.WorldLoading || host == null)
			{
				return;
			}
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				_next.Clear();
			}
			if ((_timer -= dt) > 0f)
			{
				return;
			}
			_timer = 2f;
			if (_next.Count > 400)
			{
				_next.Clear();
			}
			float unscaledTime = Time.unscaledTime;
			float num = Share(Calendar.TimeOfDay);
			Vector2 current_position = host.current_position;
			Building building = HomeLife.OwnHouse(host);
			Building building2 = Workbench.HomeOf(host);
			int num2 = 0;
			foreach (KeyValuePair<Building, HouseWorld.Placement> item in HouseWorld.Near())
			{
				if (num2 >= 2)
				{
					break;
				}
				Building key = item.Key;
				try
				{
					if (key != building && key != building2 && key.residents != null && key.residents.Count != 0 && !((key.current_position - current_position).sqrMagnitude > 784f) && (!_next.TryGetValue(key, out var value) || !(unscaledTime < value)))
					{
						_next[key] = unscaledTime + 8f + 8f * (float)Rng.NextDouble();
						if (HouseOccupancy.Count(key) <= 0 && HouseInterior.IsEnterable(key) && HouseInterior.RoomName(key.asset) != null && !(Rng.NextDouble() >= (double)num) && CallHome(key, host))
						{
							_next[key] = unscaledTime + 40f + 20f * (float)Rng.NextDouble();
							num2++;
						}
					}
				}
				catch (Exception e)
				{
					Hub.Fail("homebodies", e);
				}
			}
		}

		internal static bool CallHome(Building b, Actor host)
		{
			Actor actor = Pick(b, host);
			if (actor == null || !Send(actor))
			{
				return false;
			}
			Sent++;
			Log.Info($"homebody: {WorldBoxMod.Describe(actor)} goes home to {b.asset.id} {b.getID()}");
			return true;
		}

		private static Actor Pick(Building b, Actor host)
		{
			_pick.Clear();
			foreach (long resident in b.residents)
			{
				Actor actor = World.world.units.get(resident);
				if (Free(actor, b, host))
				{
					_pick.Add(actor);
				}
			}
			if (_pick.Count != 0)
			{
				return _pick[Rng.Next(_pick.Count)];
			}
			return null;
		}

		private static bool Free(Actor a, Building b, Actor host)
		{
			try
			{
				if (a == null || a == host || !a.isAlive() || a.getHomeBuilding() != b)
				{
					return false;
				}
				if (a.is_inside_building || a.is_inside_boat || a.isWarrior() || a.has_attack_target || a.under_forces)
				{
					return false;
				}
				if (FamilyFollow.Asleep(a) || FamilyFollow.OrderOf(a) != FamilyFollow.Orders.Free)
				{
					return false;
				}
				if ((a.current_position - b.current_position).sqrMagnitude > 900f)
				{
					return false;
				}
				return Idle((a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id : null);
			}
			catch
			{
				return false;
			}
		}

		internal static bool Send(Actor a)
		{
			Register();
			try
			{
				if (a == null || !a.isAlive() || a.getHomeBuilding() == null)
				{
					return false;
				}
				a.setTask("fp_homebody");
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_homebody";
			}
			catch (Exception e)
			{
				Hub.Fail("homebody task", e);
				return false;
			}
		}

		private static void Register()
		{
			if (_registered)
			{
				return;
			}
			_registered = true;
			try
			{
				if (AssetManager.tasks_actor.get("fp_homebody") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_homebody",
						locale_key = "task_unit_nothing",
						cancellable_by_reproduction = true,
						cancellable_by_socialize = false
					};
					behaviourTaskActor.setIcon("ui/Icons/iconHoused");
					BehaviourActionActor[] array = new BehaviourActionActor[6]
					{
						new BehBuildingTargetHome(),
						new BehGetTargetBuildingMainTile(),
						new BehGoToTileTarget(),
						new BehStayInBuildingTarget(45f, 120f),
						new BehRestoreStats(0.1f, 0.2f),
						new BehExitBuilding()
					};
					foreach (BehaviourActionActor behaviourActionActor in array)
					{
						((BehaviourActionBase<Actor>)behaviourActionActor).setupErrorChecks();
						behaviourTaskActor.addBeh(behaviourActionActor);
					}
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("homebody task register", e);
			}
		}
	}
}
