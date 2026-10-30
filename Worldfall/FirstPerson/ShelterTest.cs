using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class ShelterTest : CivilTest
	{
		private static readonly string[] Foes = new string[5] { "bandit", "skeleton", "orc", "wolf", "bear" };

		private const float Watch = 6f;

		private Actor _you;

		private Actor _foe;

		private Vector2 _door;

		private string _foeKind;

		protected override string Prefix => "SHELTER";

		protected override IEnumerator Body()
		{
			WorldTile stage = CivilTest.Stage();
			if (!Check("open land", stage != null))
			{
				yield break;
			}
			CivilTest.ClearBox(stage.x - 10, stage.y - 8, stage.x + 16, stage.y + 8);
			Building house = House(stage);
			if (Check("a house to go into", house != null && HouseInterior.IsEnterable(house), (house != null) ? house.asset.id : "none built"))
			{
				_door = new Vector2((float)stage.x + 0.5f, (float)stage.y - 2.5f);
				_you = CivilTest.Spawn("human", _door);
				yield return Possess(_you);
				if (Check("first person in a villager", InControl(_you)))
				{
					yield return Part("outdoors, an enemy by you", Outdoors("outdoors"));
					yield return Part("in the house as before the fix", Indoors(house, shelter: false));
					yield return Part("in the house", Indoors(house, shelter: true));
					yield return Part("out again", Outdoors("out of the house again"));
					yield return Part("the house pulled down round you", PulledDown(house));
					yield return Part("in a mine", Mine(stage));
					HouseInterior.Shelter = true;
					CivilTest.Kill(_foe);
				}
			}
		}

		private static Building House(WorldTile at)
		{
			try
			{
				Building building = World.world.buildings.addBuilding("house_human_1", at, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("SHELTER house: " + ex.Message);
				return null;
			}
		}

		private bool Foe()
		{
			if (_foe != null && _foe.isAlive())
			{
				return true;
			}
			string[] foes = Foes;
			foreach (string text in foes)
			{
				Actor actor = CivilTest.Spawn(text, _you.current_position + new Vector2(0.9f, 0f));
				if (actor != null)
				{
					bool flag = false;
					try
					{
						flag = actor.areFoes(_you) && ((BaseSimObject)actor).canAttackTarget((BaseSimObject)_you, pCheckForFactions: true, pAttackBuildings: false);
					}
					catch
					{
					}
					if (flag)
					{
						_foe = actor;
						_foeKind = text;
						Note("enemy: a " + text);
						return true;
					}
					CivilTest.Kill(actor);
				}
			}
			return false;
		}

		private IEnumerator Watching(int[] result)
		{
			int hurt = 0;
			int after = 0;
			int samples = 0;
			int inside = 0;
			float watched;
			for (watched = 0f; watched < 6f; watched += Time.unscaledDeltaTime)
			{
				if (!_you.isAlive())
				{
					break;
				}
				if (_foe == null || !_foe.isAlive())
				{
					Foe();
				}
				if (_foe != null && _foe.isAlive())
				{
					try
					{
						_foe.setMaxHealth();
					}
					catch
					{
					}
					if ((_foe.current_position - _you.current_position).sqrMagnitude > 1.44f)
					{
						_foe.current_position = _you.current_position + new Vector2(0.9f, 0f);
						_foe.dirty_current_tile = true;
					}
					try
					{
						_foe.addAggro(_you);
						_foe.setAttackTarget(_you);
					}
					catch
					{
					}
				}
				float before = Health(_you);
				yield return null;
				if (!_you.isAlive())
				{
					break;
				}
				float num = Health(_you);
				bool flag = false;
				try
				{
					flag = _you.attackedBy == _foe;
				}
				catch
				{
				}
				if (num < before && flag)
				{
					hurt++;
					if (RoomCombat.In(_foe))
					{
						inside++;
					}
				}
				try
				{
					_you.setMaxHealth();
				}
				catch
				{
				}
				samples++;
				try
				{
					if (_foe != null && _foe.has_attack_target && _foe.attack_target == _you)
					{
						after++;
					}
				}
				catch
				{
				}
			}
			result[0] = hurt;
			result[1] = ((samples > 0) ? (100 * after / samples) : 0);
			result[2] = Mathf.RoundToInt(watched * 10f);
			if (result.Length > 3)
			{
				result[3] = inside;
			}
		}

		private IEnumerator Outdoors(string where)
		{
			bool flag = Foe();
			if (Check("an enemy of a villager by you", flag, flag ? null : ("none of " + string.Join(", ", Foes))))
			{
				Note(FoeState("before"));
				FriendlyFire.TraceOnYou = new List<string>();
				int[] r = new int[3];
				yield return Watching(r);
				Note(FoeState("after"));
				Note("blows at you: " + string.Join("; ", FriendlyFire.TraceOnYou.ToArray()));
				FriendlyFire.TraceOnYou = null;
				Note($"{where}: the {_foeKind} hurt you {r[0]} times in {(float)r[2] / 10f:F1} s, set on you {r[1]}% of the time, you inside a building {_you.is_inside_building}");
				Check(where + ", the " + _foeKind + " hurts you", r[0] > 0, $"{r[0]} blows");
			}
		}

		private IEnumerator Indoors(Building house, bool shelter)
		{
			HouseInterior.Shelter = shelter;
			if (!Foe())
			{
				Check("an enemy", ok: false);
				yield break;
			}
			Mod.EnterHouseForTest(house);
			yield return CivilTest.Wait(0.5f);
			bool ok = Mod.Interior.Inside && Mod.Interior.House == house;
			bool ok2 = _you.is_inside_building && _you.inside_building == house;
			if (!Check("in the house's room", ok))
			{
				yield break;
			}
			if (shelter)
			{
				Check("your body is inside the house as the game sees it", ok2, string.Format("inside {0}, in {1}", _you.is_inside_building, (_you.inside_building != null) ? _you.inside_building.asset.id : "nothing"));
			}
			int[] r = new int[4];
			int came = RoomCombat.CameIn;
			yield return Watching(r);
			Note($"in the house (shelter {shelter}): the {_foeKind} hurt you {r[0]} times in {(float)r[2] / 10f:F1} s ({r[3]} of them in the room with you), set on you {r[1]}% of the time, frozen {PossessionHooks.Frozen}");
			Check("nothing outside hurts you through the walls", r[0] - r[3] == 0, $"{r[0] - r[3]} blows from outside");
			Check("it comes in after you", RoomCombat.CameIn > came || RoomCombat.In(_foe), $"came in {RoomCombat.CameIn - came}");
			if (shelter)
			{
				try
				{
					_you.cancelAllBeh();
				}
				catch
				{
				}
				bool letOut = !_you.is_inside_building;
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				yield return new WaitForEndOfFrame();
				Check("back indoors after the game let your body out", letOut && _you.is_inside_building && _you.inside_building == house, $"let out {letOut}, inside now {_you.is_inside_building}");
			}
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.3f);
			Check("out of the house, out of it as the game sees it too", !Mod.Interior.Inside && !_you.is_inside_building, $"room {Mod.Interior.Inside}, inside {_you.is_inside_building}");
			HouseInterior.Shelter = true;
		}

		private IEnumerator PulledDown(Building house)
		{
			CivilTest.Kill(_foe);
			Mod.EnterHouseForTest(house);
			yield return CivilTest.Wait(0.5f);
			if (!Check("in the house again", Mod.Interior.Inside && _you.is_inside_building))
			{
				yield break;
			}
			try
			{
				house.startDestroyBuilding();
			}
			catch (Exception ex)
			{
				Log.Error("SHELTER pull down: " + ex.Message);
			}
			for (float t = 0f; t < 3f; t += 0.25f)
			{
				if (!Mod.Interior.Inside)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("the house gone puts you out", !Mod.Interior.Inside && !_you.is_inside_building && _you.isAlive(), $"room {Mod.Interior.Inside}, inside {_you.is_inside_building}, alive {_you.isAlive()}");
		}

		private IEnumerator Mine(WorldTile stage)
		{
			WorldTile tile = World.world.GetTile(stage.x + 9, stage.y + 2);
			Building mine = null;
			try
			{
				mine = World.world.buildings.addBuilding("mine", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				mine?.completeConstruction();
			}
			catch (Exception ex)
			{
				Log.Error("SHELTER mine: " + ex.Message);
			}
			if (!Check("a mine", mine != null))
			{
				yield break;
			}
			Vector2 vector = new Vector2((float)tile.x + 0.5f, (float)tile.y - 1.5f);
			if (MineInterior.Mouth(mine, out var mouth))
			{
				vector = mouth;
			}
			Put(_you, vector + new Vector2(0f, -1.4f));
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(2f);
			for (float t = 0f; t < 5f; t += 0.25f)
			{
				if (MineInterior.IsEnterable(mine))
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			if (!Check("the mine can be gone into", MineInterior.IsEnterable(mine)))
			{
				yield break;
			}
			if (!Foe())
			{
				Check("an enemy", ok: false);
				yield break;
			}
			Mod.EnterMine(mine);
			yield return CivilTest.Wait(0.5f);
			if (Check("in the mine", Mod.Mine.Inside))
			{
				Check("your body is inside the mine as the game sees it", _you.is_inside_building && _you.inside_building == mine);
				int[] r = new int[4];
				yield return Watching(r);
				Note($"in the mine: the {_foeKind} hurt you {r[0]} times in {(float)r[2] / 10f:F1} s ({r[3]} of them in the mine with you), set on you {r[1]}% of the time");
				Check("nothing outside hurts you through the mine's walls", r[0] - r[3] == 0, $"{r[0] - r[3]} blows from outside");
				Mod.LeaveMineForTest();
				yield return CivilTest.Wait(0.3f);
				Check("out of the mine, out of it as the game sees it too", !Mod.Mine.Inside && !_you.is_inside_building, $"mine {Mod.Mine.Inside}, inside {_you.is_inside_building}");
			}
		}

		private string FoeState(string when)
		{
			try
			{
				return $"the {_foeKind} {when}: inside {_foe.is_inside_building}, action timer {_foe.timer_action:F2}, attack timer {_foe.attack_timer:F2}, " + string.Format("task {0}, target you {1}, ", (_foe.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_foe.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_foe.ai).task.id : "none", _foe.has_attack_target && _foe.attack_target == _you) + string.Format("in reach {0}, ignores you {1}, stunned {2}, ", _foe.isInAttackRange((BaseSimObject)_you), ((BaseSimObject)_foe).shouldIgnoreTarget((BaseSimObject)_you), ((BaseSimObject)_foe).hasStatus("stunned")) + $"can attack you {((BaseSimObject)_foe).canAttackTarget((BaseSimObject)_you, pCheckForFactions: true, pAttackBuildings: true)}, rotation {_foe.target_angle.z:F0}, {(_foe.current_position - _you.current_position).magnitude:F2} off, " + $"paused {Config.paused}/{World.world.isPaused()}, world time {World.world.getCurWorldTime():F1}, winding {WindUps.Winding(_foe)}, last attack {_foe.last_attack_timestamp:F1}, wind-ups {WindUps.Begun}, held {WindUps.Held}, " + string.Format("guard blocks {0}, parries {1}, host {2}, you moving {3}, your next step {4}, your tile {5}, you at {6}", GuardControl.Blocked, GuardControl.Parried, Blocks.Host == _you, _you.is_moving, _you.next_step_position, (_you.current_tile != null) ? (_you.current_tile.x + "," + _you.current_tile.y) : "none", _you.current_position);
			}
			catch (Exception ex)
			{
				return "foe state: " + ex.Message;
			}
		}

		private static void Put(Actor a, Vector2 at)
		{
			a.current_position = at;
			a.dirty_current_tile = true;
		}

		private static float Health(Actor a)
		{
			try
			{
				return (a != null && a.isAlive()) ? ((float)a.getHealth()) : 0f;
			}
			catch
			{
				return 0f;
			}
		}
	}
}
