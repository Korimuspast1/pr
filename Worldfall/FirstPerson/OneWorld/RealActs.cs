using System;
using System.Collections.Generic;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.OneWorld
{
	internal static class RealActs
	{
		internal sealed class DoorTile : BehGetTargetBuildingMainTile
		{
			public override BehResult execute(Actor pActor)
			{
				BehResult result = base.execute(pActor);
				try
				{
					Building beh_building_target = pActor.beh_building_target;
					if (!DoorsOn || beh_building_target == null || !Near(pActor))
					{
						return result;
					}
					WorldTile worldTile = InsideSpots.SafeDoor(beh_building_target);
					if (worldTile == null || worldTile == beh_building_target.current_tile)
					{
						return result;
					}
					pActor.beh_tile_target = worldTile;
					DoorEntriesForTest++;
				}
				catch
				{
				}
				return result;
			}
		}

		private sealed class SeatHold : BehaviourActionActor
		{
			public override BehResult execute(Actor pActor)
			{
				try
				{
					long key = pActor.data.id;
					if (_seat.TryGetValue(key, out var value))
					{
						if ((pActor.current_position - value).sqrMagnitude < 2.25f)
						{
							pActor.current_position = value;
						}
						pActor.timer_action = (_sitFor.TryGetValue(key, out var value2) ? value2 : 20f);
						_seat.Remove(key);
						_sitFor.Remove(key);
					}
				}
				catch
				{
				}
				return BehResult.Continue;
			}
		}

		private static float _next;

		private static readonly List<Actor> _near = new List<Actor>();

		internal static bool CourtOn = true;

		internal static bool TorchOn = true;

		internal static bool FacingOn = true;

		internal static bool DrillOn = true;

		internal static int CourtSentForTest;

		internal static int CourtOutForTest;

		internal static int TorchesForTest;

		internal static int FacedForTest;

		internal static int PostedForTest;

		public const string CourtTask = "fp_court";

		private static bool _courtRegistered;

		private static readonly Dictionary<Actor, Building> _courted = new Dictionary<Actor, Building>();

		private static readonly List<Actor> _released = new List<Actor>();

		public const string TorchStatus = "fp_torch";

		private static bool _torchRegistered;

		public const float Reach = 36f;

		internal static bool DoorsOn = true;

		internal static int DoorEntriesForTest;

		private static int _tasksSeen = -1;

		public const string SitTask = "fp_sit";

		private static readonly Dictionary<long, Vector2> _seat = new Dictionary<long, Vector2>();

		private static readonly Dictionary<long, float> _sitFor = new Dictionary<long, float>();

		private static bool _sitRegistered;

		internal static bool SitOn = true;

		internal static int SitsForTest;

		private static Actor You
		{
			get
			{
				try
				{
					return ControllableUnit.isControllingUnit() ? ControllableUnit._unit_main : null;
				}
				catch
				{
					return null;
				}
			}
		}

		public static void Tick(DayCycle day)
		{
			InstallDoors();
			if (Time.unscaledTime < _next)
			{
				return;
			}
			_next = Time.unscaledTime + 1f;
			Actor you = You;
			if (WorldSync.Mirroring || WorldBoxMod.WorldLoading)
			{
				return;
			}
			if (you == null)
			{
				try
				{
					ReleaseCourt(null, 0f);
					return;
				}
				catch
				{
					return;
				}
			}
			Gather(you);
			float hour = day?.Hour ?? 0.5f;
			try
			{
				Court(you, hour);
			}
			catch (Exception ex)
			{
				Log.Error("real acts: court: " + ex.Message);
			}
			try
			{
				ReleaseCourt(you, hour);
			}
			catch (Exception ex2)
			{
				Log.Error("real acts: court's end: " + ex2.Message);
			}
			try
			{
				Torches(hour);
			}
			catch (Exception ex3)
			{
				Log.Error("real acts: torches: " + ex3.Message);
			}
			try
			{
				Facing();
			}
			catch (Exception ex4)
			{
				Log.Error("real acts: facing: " + ex4.Message);
			}
			try
			{
				Posts();
			}
			catch (Exception ex5)
			{
				Log.Error("real acts: drill: " + ex5.Message);
			}
		}

		private static void Gather(Actor you)
		{
			_near.Clear();
			Vector2 current_position = you.current_position;
			List<Actor> simpleList;
			try
			{
				simpleList = World.world.units.getSimpleList();
			}
			catch
			{
				return;
			}
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor != null && actor != you && actor.isAlive() && !InsideSpots.Possessed(actor) && (actor.current_position - current_position).sqrMagnitude <= 1296f)
				{
					_near.Add(actor);
				}
			}
		}

		private static void Court(Actor you, float hour)
		{
			CourtSentForTest = (CourtOutForTest = 0);
			if (!CourtOn)
			{
				return;
			}
			City city = null;
			try
			{
				city = you.current_tile?.zone?.city;
			}
			catch
			{
			}
			Kingdom kingdom = city?.kingdom;
			if (kingdom == null)
			{
				return;
			}
			Building building = FirstPerson.Towns.Court.CapitalHall(kingdom);
			if (building == null || building.city != city || !building.isUsable())
			{
				return;
			}
			bool flag = hour > 0.27f && hour < 0.75f;
			List<Actor> list = new List<Actor>();
			try
			{
				if (kingdom.king != null)
				{
					list.Add(kingdom.king);
				}
			}
			catch
			{
			}
			Seat[] seats = FirstPerson.Towns.Court.Seats;
			foreach (Seat s in seats)
			{
				Actor actor = FirstPerson.Towns.Court.Holder(kingdom, s);
				if (actor != null && !list.Contains(actor))
				{
					list.Add(actor);
				}
			}
			foreach (Actor item in list)
			{
				if (item != null && item != you && item.isAlive() && !InsideSpots.Possessed(item) && item.current_tile?.zone?.city == city)
				{
					bool flag2 = item.is_inside_building && item.inside_building == building;
					string text = null;
					try
					{
						text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item.ai)?.task?.id;
					}
					catch
					{
					}
					if (flag && !flag2 && text != "fp_court" && Free(item))
					{
						RegisterCourt();
						item.setTask("fp_court");
						item.beh_building_target = building;
						_courted[item] = building;
						CourtSentForTest++;
					}
					else if (!flag && flag2 && text == "fp_court")
					{
						Release(item, building);
					}
				}
			}
		}

		private static void ReleaseCourt(Actor you, float hour)
		{
			if (_courted.Count == 0)
			{
				return;
			}
			bool flag = hour > 0.27f && hour < 0.75f;
			City city = null;
			try
			{
				city = you?.current_tile?.zone?.city;
			}
			catch
			{
			}
			_released.Clear();
			foreach (KeyValuePair<Actor, Building> item in _courted)
			{
				Actor key = item.Key;
				Building value = item.Value;
				bool flag2 = key?.isAlive() ?? false;
				string text = null;
				try
				{
					text = ((!flag2) ? null : ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)key.ai)?.task?.id);
				}
				catch
				{
				}
				if (!flag2 || text != "fp_court")
				{
					_released.Add(key);
				}
				else if (!flag || !CourtOn || city == null || value == null || value.city != city || InsideSpots.Possessed(key))
				{
					Release(key, value);
					_released.Add(key);
				}
			}
			foreach (Actor item2 in _released)
			{
				_courted.Remove(item2);
			}
		}

		private static void Release(Actor a, Building hall)
		{
			try
			{
				bool num = a.is_inside_building && a.inside_building == hall;
				a.cancelAllBeh();
				if (num)
				{
					WorldTile worldTile = InsideSpots.SafeDoor(hall);
					if (worldTile != null)
					{
						a.setCurrentTilePosition(worldTile);
					}
				}
				CourtOutForTest++;
			}
			catch (Exception ex)
			{
				Log.Error("real acts: out of court: " + ex.Message);
			}
		}

		private static bool Free(Actor a)
		{
			try
			{
				if (a.has_attack_target || a.is_inside_building || a.isInsideSomething())
				{
					return false;
				}
				string text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id;
				return text == null || text.StartsWith("wait", StringComparison.Ordinal) || text.StartsWith("random_move", StringComparison.Ordinal) || text.StartsWith("socialize", StringComparison.Ordinal) || text == "fp_sit";
			}
			catch
			{
				return false;
			}
		}

		private static void RegisterCourt()
		{
			if (_courtRegistered)
			{
				return;
			}
			_courtRegistered = true;
			try
			{
				if (AssetManager.tasks_actor.get("fp_court") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_court",
						locale_key = "task_unit_nothing",
						cancellable_by_reproduction = false,
						cancellable_by_socialize = false
					};
					behaviourTaskActor.setIcon("ui/Icons/iconHoused");
					BehaviourActionActor[] array = new BehaviourActionActor[4]
					{
						new DoorTile(),
						new BehGoToTileTarget(),
						new BehStayInBuildingTarget(600f, 900f),
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
			catch (Exception ex)
			{
				Log.Error("real acts: the court task: " + ex.Message);
			}
		}

		private static void Torches(float hour)
		{
			TorchesForTest = 0;
			if (!TorchOn || !(MapNight.NightAt(hour) > 0.3f) || !RegisterTorch())
			{
				return;
			}
			foreach (Actor item in _near)
			{
				if (!item.is_inside_building && People.CarriesTorchOutside(item))
				{
					try
					{
						((BaseSimObject)item).addStatusEffect("fp_torch", 2.5f, pColorEffect: false);
						TorchesForTest++;
					}
					catch
					{
					}
				}
			}
		}

		private static bool RegisterTorch()
		{
			if (_torchRegistered)
			{
				return AssetManager.status.get("fp_torch") != null;
			}
			_torchRegistered = true;
			try
			{
				if (AssetManager.status.get("fp_torch") != null)
				{
					return true;
				}
				StatusAsset statusAsset = new StatusAsset
				{
					id = "fp_torch",
					duration = 2.5f,
					draw_light_area = true,
					draw_light_size = 0.35f,
					path_icon = "ui/Icons/iconFire",
					locale_id = "status_title_fp_torch",
					locale_description = "status_description_fp_torch"
				};
				try
				{
					statusAsset.material = LibraryMaterials.instance.dict["mat_world_object"];
				}
				catch
				{
				}
				AssetManager.status.add(statusAsset);
				Lang.AddGameText("status_title_fp_torch", "Torch");
				Lang.AddGameText("status_description_fp_torch", "Carries a lit torch");
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("real acts: the torch status: " + ex.Message);
				return false;
			}
		}

		private static void Facing()
		{
			FacedForTest = 0;
			if (!FacingOn)
			{
				return;
			}
			foreach (Actor item in _near)
			{
				if (!item.is_inside_building && !item.is_moving && Attention.FaceTo(item, out var face))
				{
					try
					{
						item.lookTowardsPosition(face);
						FacedForTest++;
					}
					catch
					{
					}
				}
			}
		}

		private static void Posts()
		{
			PostedForTest = 0;
			if (!DrillOn)
			{
				return;
			}
			foreach (Actor item in _near)
			{
				if (!item.is_inside_building && !item.is_moving)
				{
					Vector2 current_position = item.current_position;
					Vector2 vector = Drill.Spot(item, current_position);
					if (!((vector - current_position).sqrMagnitude < 0.0025f) && !((vector - current_position).sqrMagnitude > 4f))
					{
						item.current_position = vector;
						PostedForTest++;
					}
				}
			}
		}

		public static bool Near(Actor a)
		{
			Actor you = You;
			if (a == null || you == null || WorldSync.Mirroring)
			{
				return false;
			}
			return (a.current_position - you.current_position).sqrMagnitude <= 1296f;
		}

		public static void InstallDoors()
		{
			List<BehaviourTaskActor> list;
			try
			{
				list = AssetManager.tasks_actor?.list;
			}
			catch
			{
				return;
			}
			if (list == null || list.Count == _tasksSeen)
			{
				return;
			}
			_tasksSeen = list.Count;
			int num = 0;
			foreach (BehaviourTaskActor item in list)
			{
				if (item?.list == null)
				{
					continue;
				}
				for (int i = 0; i + 2 < item.list.Count; i++)
				{
					BehaviourActionActor behaviourActionActor = item.list[i];
					if (behaviourActionActor != null && !(behaviourActionActor.GetType() != typeof(BehGetTargetBuildingMainTile)) && item.list[i + 1] is BehGoToTileTarget && item.list[i + 2] is BehStayInBuildingTarget)
					{
						DoorTile doorTile = new DoorTile();
						try
						{
							((BehaviourActionBase<Actor>)doorTile).setupErrorChecks();
						}
						catch
						{
						}
						item.list[i] = doorTile;
						num++;
					}
				}
			}
			if (num > 0)
			{
				Log.Info($"real acts: {num} ways into buildings go by the door near you");
			}
		}

		public static bool Sit(Actor a, Vector2 seat, float seconds)
		{
			if (!SitOn || a == null || !a.isAlive() || !Near(a) || InsideSpots.Possessed(a))
			{
				return false;
			}
			string text = null;
			try
			{
				text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id;
			}
			catch
			{
			}
			if (text != null && !text.StartsWith("wait", StringComparison.Ordinal) && !text.StartsWith("random_move", StringComparison.Ordinal) && !(text == "fp_sit"))
			{
				return false;
			}
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile((int)Mathf.Floor(seat.x), (int)Mathf.Floor(seat.y));
			}
			catch
			{
			}
			if (worldTile == null || worldTile.Type == null || !worldTile.Type.ground || worldTile.Type.block)
			{
				return false;
			}
			RegisterSit();
			try
			{
				_seat[a.data.id] = seat;
				_sitFor[a.data.id] = seconds;
				a.setTask("fp_sit");
				a.beh_tile_target = worldTile;
				SitsForTest++;
				return ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id == "fp_sit";
			}
			catch (Exception ex)
			{
				Log.Error("real acts: sit: " + ex.Message);
				return false;
			}
		}

		public static bool Sitting(Actor a)
		{
			try
			{
				return a != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id == "fp_sit";
			}
			catch
			{
				return false;
			}
		}

		private static void RegisterSit()
		{
			if (_sitRegistered)
			{
				return;
			}
			_sitRegistered = true;
			try
			{
				if (AssetManager.tasks_actor.get("fp_sit") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_sit",
						locale_key = "task_unit_nothing",
						cancellable_by_reproduction = true,
						cancellable_by_socialize = true
					};
					behaviourTaskActor.setIcon("ui/Icons/iconHoused");
					BehaviourActionActor[] array = new BehaviourActionActor[2]
					{
						new BehGoToTileTarget(),
						new SeatHold()
					};
					foreach (BehaviourActionActor behaviourActionActor in array)
					{
						((BehaviourActionBase<Actor>)behaviourActionActor).setupErrorChecks();
						behaviourTaskActor.addBeh(behaviourActionActor);
					}
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
			}
			catch (Exception ex)
			{
				Log.Error("real acts: the sit task: " + ex.Message);
			}
		}
	}
}
