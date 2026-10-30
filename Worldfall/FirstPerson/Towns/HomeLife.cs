using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class HomeLife
	{
		internal const string StayHome = "fp_home_visit";

		private const float DarkAt = 1.1f;

		private const float SleepEnd = 3.2f;

		private const float BedReach = 1.3f;

		private const float LoftReach = 2f;

		private const float BedOver = 0.5f;

		private const float BedFacing = 50f;

		private static float _keep;

		private static float _visit;

		private static float _sleepStart = -10f;

		private static bool _slept;

		private static bool _registered;

		private static readonly System.Random Rng = new System.Random();

		internal static bool VisitsOff;

		internal static int SleptToDay = -1;

		internal static bool Sleeping => Time.unscaledTime - _sleepStart < 3.2f;

		public static Building OwnHouse(Actor you)
		{
			Building building = Workbench.HomeOf(you);
			if (building == null || !Homes.OwnedBy(building, you))
			{
				return null;
			}
			return building;
		}

		public static void MoveIn(Actor you, Building b)
		{
			if (you == null || b == null)
			{
				return;
			}
			Homes.SetOwner(b, you);
			List<Actor> list = Household(you);
			OutStrangers(you, b, list);
			try
			{
				you.setHomeBuilding(b);
			}
			catch (Exception e)
			{
				Hub.Fail("home move in", e);
			}
			City city = null;
			try
			{
				city = b.city;
			}
			catch
			{
			}
			foreach (Actor item in list)
			{
				try
				{
					if (item.city == city && item.getHomeBuilding() != b)
					{
						item.setHomeBuilding(b);
					}
				}
				catch
				{
				}
			}
			Log.Info($"home: you and {list.Count} of your family live in {b.asset.id} now");
		}

		public static void Update(Actor host, float dt)
		{
			if (WorldBoxMod.WorldLoading || host == null)
			{
				return;
			}
			Sleep(host);
			if ((_keep -= dt) > 0f)
			{
				return;
			}
			_keep = 3f;
			Building building = OwnHouse(host);
			if (building == null)
			{
				return;
			}
			List<Actor> list = Household(host);
			OutStrangers(host, building, list);
			City city = null;
			try
			{
				city = building.city;
			}
			catch
			{
			}
			foreach (Actor item in list)
			{
				try
				{
					if (item.city == city && item.getHomeBuilding() != building)
					{
						item.setHomeBuilding(building);
					}
				}
				catch
				{
				}
			}
			if (!((_visit -= 3f) > 0f) && !VisitsOff)
			{
				_visit = 8f;
				Visits(host, building, list, city);
			}
		}

		private static void OutStrangers(Actor you, Building b, List<Actor> kin)
		{
			try
			{
				if (b.residents == null || b.residents.Count == 0)
				{
					return;
				}
				foreach (long item in new List<long>(b.residents))
				{
					if (item == you.data.id)
					{
						continue;
					}
					Actor actor = World.world.units.get(item);
					if (actor == null || !kin.Contains(actor))
					{
						if (actor != null)
						{
							actor.clearHomeBuilding();
						}
						else
						{
							b.residents.Remove(item);
						}
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("home strangers", e);
			}
		}

		private static void Visits(Actor you, Building house, List<Actor> kin, City town)
		{
			float timeOfDay = Calendar.TimeOfDay;
			bool flag = timeOfDay > 0.78f || timeOfDay < 0.24f;
			bool flag2 = false;
			try
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				flag2 = instance != null && instance.Interior.Inside && instance.Interior.House == house;
			}
			catch
			{
			}
			float num = (flag ? 0.35f : 0.08f) + (flag2 ? 0.25f : 0f);
			foreach (Actor item in kin)
			{
				try
				{
					if (item.isAlive() && item.city == town && item.getHomeBuilding() == house && FamilyFollow.OrderOf(item) == FamilyFollow.Orders.Free && !item.is_inside_building && !item.is_inside_boat && !item.isWarrior() && !item.has_attack_target && !FamilyFollow.Asleep(item) && (item.ai == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item.ai).task.id == "fp_home_visit")) && !(Rng.NextDouble() >= (double)num) && SendHome(item))
					{
						Log.Info("home: " + WorldBoxMod.Describe(item) + " comes home");
					}
				}
				catch (Exception e)
				{
					Hub.Fail("home visits", e);
				}
			}
		}

		internal static bool SendHome(Actor a)
		{
			Register();
			try
			{
				if (a == null || !a.isAlive() || a.getHomeBuilding() == null)
				{
					return false;
				}
				a.setTask("fp_home_visit");
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_home_visit";
			}
			catch (Exception e)
			{
				Hub.Fail("home task", e);
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
				if (AssetManager.tasks_actor.get("fp_home_visit") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_home_visit",
						locale_key = "task_unit_nothing",
						cancellable_by_reproduction = false,
						cancellable_by_socialize = false
					};
					behaviourTaskActor.setIcon("ui/Icons/iconHoused");
					BehaviourActionActor[] array = new BehaviourActionActor[6]
					{
						new BehBuildingTargetHome(),
						new BehGetTargetBuildingMainTile(),
						new BehGoToTileTarget(),
						new BehStayInBuildingTarget(20f, 60f),
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
				Hub.Fail("home task register", e);
			}
		}

		public static List<Actor> Household(Actor you)
		{
			List<Actor> list = new List<Actor>();
			try
			{
				Actor lover = you.lover;
				if (lover != null && lover != you && lover.isAlive())
				{
					list.Add(lover);
				}
				long id = you.data.id;
				long num = ((lover != null && lover.isAlive()) ? lover.data.id : (-1));
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple == null || simple == you || simple == lover || !simple.isAlive())
					{
						continue;
					}
					long parent_id_ = simple.data.parent_id_1;
					long parent_id_2 = simple.data.parent_id_2;
					bool num2 = parent_id_ == id || parent_id_2 == id;
					bool flag = num >= 0 && (parent_id_ == num || parent_id_2 == num) && !simple.isAdult();
					if ((num2 || flag) && (!simple.isAdult() || !simple.hasLover()))
					{
						list.Add(simple);
						if (list.Count >= 12)
						{
							break;
						}
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("household", e);
			}
			return list;
		}

		public static bool RoomPrompt(PromptAsk ask)
		{
			if (ask?.Room?.Room == null || ask.House == null || ask.Host == null)
			{
				return false;
			}
			if (!HouseDoors.MaySleepIn(ask.House, ask.Host))
			{
				return false;
			}
			if (!NearBed(ask.Room.Room, ask.Eye, ask.Yaw))
			{
				return false;
			}
			ask.Prompt = "E:Sleep until dawn";
			if (PlayInput.GetKeyDown(KeyCode.E) && !Sleeping)
			{
				if (Economy.InAFight(ask.Host))
				{
					Hub.Toast?.Invoke("You can't sleep in a fight");
				}
				else
				{
					StartSleep();
				}
			}
			return true;
		}

		internal static bool NearBed(Interior room, Vector2 eye, float yaw = float.NaN)
		{
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (!IsBed(mesh.Model, out var loft))
				{
					continue;
				}
				float num = mesh.Position.X - eye.x;
				float num2 = mesh.Position.Y - eye.y;
				float num3 = (loft ? 2f : 1.3f);
				float num4 = num * num + num2 * num2;
				if (!(num4 > num3 * num3))
				{
					if (float.IsNaN(yaw) || num4 < 0.25f)
					{
						return true;
					}
					if (Mathf.Abs(Mathf.DeltaAngle(yaw * 57.29578f, Mathf.Atan2(num2, num) * 57.29578f)) <= 50f)
					{
						return true;
					}
				}
			}
			return false;
		}

		internal static bool BedOf(Interior room, out Vector2 at)
		{
			at = default(Vector2);
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (IsBed(mesh.Model, out var _))
				{
					at = new Vector2(mesh.Position.X, mesh.Position.Y);
					return true;
				}
			}
			return false;
		}

		private static bool IsBed(MeshModel model, out bool loft)
		{
			loft = false;
			if (model == null)
			{
				return false;
			}
			string text = model.Info("use");
			loft = text == "loft";
			if (!((text == "bed") | loft))
			{
				if (model.Name != null)
				{
					return model.Name.IndexOf("bed", StringComparison.Ordinal) >= 0;
				}
				return false;
			}
			return true;
		}

		internal static void StartSleep()
		{
			_sleepStart = Time.unscaledTime;
			_slept = false;
		}

		private static void Sleep(Actor host)
		{
			if (!_slept && Sleeping && !(Time.unscaledTime - _sleepStart < 1.1f))
			{
				_slept = true;
				int num = (SleptToDay = Calendar.SkipToDawn());
				try
				{
					host.restoreHealthPercent(1f);
					host.data.stamina = host.getMaxStamina();
					((BaseSimObject)host).addStatusEffect("had_good_dream", 0f, pColorEffect: false);
				}
				catch (Exception e)
				{
					Hub.Fail("sleep", e);
				}
				Hub.Toast?.Invoke("You sleep until dawn");
				Log.Info($"home: slept until the dawn of day {num}");
				WorldBoxMod instance = WorldBoxMod.Instance;
				FirstPersonSave.SaveNow(host, (instance != null && instance.Interior.Inside) ? instance.Interior.House : null, Hub.Toast);
			}
		}

		public static void DrawHud(float sw, float sh)
		{
			float num = Time.unscaledTime - _sleepStart;
			if (!(num < 0f) && !(num >= 3.2f))
			{
				float value = ((num < 1.1f) ? (num / 1.1f) : ((num < 2f) ? 1f : (1f - (num - 1.1f - 0.9f) / 1.1999999f)));
				GameArt.Flat(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0.02f, Mathf.Clamp01(value)));
			}
		}
	}
}
