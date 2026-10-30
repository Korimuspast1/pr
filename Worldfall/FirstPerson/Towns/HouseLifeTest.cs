using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal sealed class HouseLifeTest : CivilTest
	{
		private Actor _you;

		private Actor _a;

		private Actor _b;

		private Building _house;

		private Vector2 _door;

		protected override string Prefix => "HOUSELIFE";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			Actor actor = Village("human", 9, 0, worldTile);
			if (!Check("a village", actor != null && actor.city != null))
			{
				yield break;
			}
			City city = actor.city;
			_house = AddHouse("house_human_1", city);
			if (!Check("a house of the town", _house != null && _house.city == city && HouseInterior.IsEnterable(_house), (_house != null) ? ("city " + ((_house.city != null) ? _house.city.name : "none")) : "none built"))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAlive() && !item.isWarrior())
				{
					list.Add(item);
				}
			}
			if (list.Count < 2)
			{
				list.Insert(0, actor);
			}
			_you = ((list.Count > 2) ? list[2] : Founded[1]);
			_a = list[0];
			_b = list[1];
			if (!Check("three villagers", _you != null && _a != null && _b != null))
			{
				yield break;
			}
			try
			{
				_a.setHomeBuilding(_house);
				_b.setHomeBuilding(_house);
			}
			catch (Exception ex)
			{
				Log.Error("HOUSELIFE homes: " + ex.Message);
			}
			yield return Possess(_you);
			if (Check("first person", InControl(_you)))
			{
				Vector2 at = _house.current_position;
				Mod.DebugEye = at + new Vector2(0f, -5f);
				Mod.DebugEyeHeight = 0f;
				Mod.DebugEyeSet = true;
				Mod.SetYaw(MathF.PI / 2f);
				yield return CivilTest.Wait(1.5f);
				if (Check("the house's door is known", Steering.DoorForTest(_house, out _door), "no door registered near the eye"))
				{
					Note($"house at {at}, door {_door}; a {WorldBoxMod.Describe(_a)}, b {WorldBoxMod.Describe(_b)}");
					yield return Part("walking in by the door", WalkIn(_a, "a"));
					yield return Part("in the room: who the game has in", RoomFollows());
					yield return Part("asleep in a bed", Sleeping());
					yield return Part("out by the door", GoingOut());
					Mod.LeaveRoomForTest();
					yield return CivilTest.Wait(0.5f);
					Check("out of the house again", !Mod.Interior.Inside);
					yield return Part("taken over at home", TakenAtHome());
				}
			}
		}

		private IEnumerator TakenAtHome()
		{
			Actor who = null;
			Actor[] array = new Actor[2] { _a, _b };
			foreach (Actor actor in array)
			{
				if (actor != null && actor.isAlive() && actor.is_inside_building && actor.inside_building == _house)
				{
					who = actor;
					break;
				}
			}
			if (who == null)
			{
				SendHome(_a);
				for (float t = 0f; t < 60f; t += 0.5f)
				{
					if (_a.is_inside_building && _a.inside_building == _house)
					{
						break;
					}
					yield return CivilTest.Wait(0.5f);
				}
				who = _a;
			}
			if (!Check("someone at home to take over", who.is_inside_building && who.inside_building == _house))
			{
				yield break;
			}
			Note($"taking over {WorldBoxMod.Describe(who)} at {who.current_position}, house at {_house.current_position} (tile {_house.current_tile?.x},{_house.current_tile?.y})");
			yield return Possess(who);
			Note($"after: at {who.current_position}, inside {who.is_inside_building}, house there {HouseInterior.HouseAt(who) != null}");
			if (!Check("in control of them", InControl(who)))
			{
				yield break;
			}
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("you're in their room", Mod.Interior.Inside && Mod.Interior.House == _house, $"inside {Mod.Interior.Inside}, at {who.current_position}");
			Mod.SaveFrame("houselife_taken_at_home.png");
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
		}

		private static bool SendHome(Actor a)
		{
			string text = "?";
			try
			{
				text = ((a.getHomeBuilding() != null) ? a.getHomeBuilding().asset.id : "none");
			}
			catch
			{
			}
			bool flag = HomeLife.SendHome(a);
			Log.Info("HOUSELIFE send home: home " + text + ", mod task " + (flag ? "set" : "not set") + ", task now " + TaskOf(a));
			if (flag)
			{
				return true;
			}
			try
			{
				a.setTask("stay_in_own_home");
			}
			catch (Exception ex)
			{
				Log.Error("HOUSELIFE send home: " + ex.Message);
			}
			Log.Info("HOUSELIFE send home: the game's own task: now " + TaskOf(a));
			return TaskOf(a) == "stay_in_own_home";
		}

		private static string TaskOf(Actor a)
		{
			try
			{
				return (a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id : "none";
			}
			catch
			{
				return "?";
			}
		}

		private IEnumerator WalkIn(Actor a, string tag)
		{
			Mod.DebugEye = _door + new Vector2(0f, -4f);
			Mod.DebugEyeHeight = 0f;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(MathF.PI / 2f);
			yield return CivilTest.Wait(0.5f);
			if ((a.current_position - _door).sqrMagnitude < 36f)
			{
				try
				{
					a.current_position = _door + new Vector2(0f, -7f);
					a.dirty_current_tile = true;
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.3f);
			}
			if (!Check(tag + " sent home", SendHome(a)))
			{
				yield break;
			}
			float nearest = 99f;
			float nearestInside = 99f;
			float hidAt = -1f;
			float insideAt = -1f;
			float swung = -1f;
			bool shot = false;
			int lastFrame = -1;
			int unseen = 0;
			float t0 = Time.unscaledTime;
			float said = 0f;
			while (Time.unscaledTime - t0 < 40f)
			{
				yield return null;
				bool flag = false;
				try
				{
					flag = a.is_inside_building;
				}
				catch
				{
				}
				if (Time.unscaledTime - t0 > said + 5f)
				{
					said = Time.unscaledTime - t0;
					Note($"{tag} at {said:F0} s: task {TaskOf(a)}, at {a.current_position}, inside {flag}");
				}
				if (flag && insideAt < 0f)
				{
					insideAt = Time.unscaledTime - t0;
				}
				Vector2 drawn;
				Vector2 real;
				float angle;
				float speed;
				int frame;
				float dt;
				bool flag2 = Mod.SceneForTest.MotionForTest(a, out drawn, out real, out angle, out speed, out frame, out dt) && frame != lastFrame;
				lastFrame = frame;
				unseen = ((!flag2) ? (unseen + 1) : 0);
				if (flag2)
				{
					float magnitude = (drawn - _door).magnitude;
					nearest = Mathf.Min(nearest, magnitude);
					if (flag)
					{
						nearestInside = Mathf.Min(nearestInside, magnitude);
					}
					if (flag && magnitude < 0.6f && !shot)
					{
						shot = true;
						Mod.SaveFrame("houselife_" + tag + "_at_door.png");
					}
				}
				else if (flag && hidAt < 0f && insideAt >= 0f && unseen > 5)
				{
					hidAt = Time.unscaledTime - t0;
				}
				if (HouseLife.OpenOf(_house) > 0.5f && swung < 0f)
				{
					swung = Time.unscaledTime - t0;
				}
				if (hidAt >= 0f)
				{
					break;
				}
			}
			Check(tag + " goes in as the game has it", insideAt >= 0f, (insideAt < 0f) ? ("never flagged inside in 40 s (task " + TaskOf(a) + ")") : $"after {insideAt:F1} s");
			Check(tag + "'s body goes in by the door", nearest < 0.5f, $"nearest to the door {nearest:F2} (while the game had them inside {nearestInside:F2})");
			Check(tag + "'s body is hidden once in through it", hidAt >= 0f && hidAt - insideAt < 8f, $"hidden {hidAt:F1} s, inside {insideAt:F1} s");
			Note("door swing: " + ((swung >= 0f) ? $"open at {swung:F1} s" : ((HouseLife.OpenOf(_house) < 0f) ? "no leaf on this model" : "never opened")));
			Check("someone at the door was noted", Steering.SinceCrossing(_house) < 30f);
			HouseOccupancy.Scan();
			Check("the house counts them in", HouseOccupancy.Of(_house).Inside.Contains(a), $"{HouseOccupancy.Count(_house)} in");
			Mod.DebugEyeSet = false;
		}

		private IEnumerator RoomFollows()
		{
			Mod.EnterHouseForTest(_house);
			yield return CivilTest.Wait(0.8f);
			if (!Check("in the house", Mod.Interior.Inside && Mod.Interior.House == _house))
			{
				yield break;
			}
			Mod.SaveFrame("houselife_room_in.png");
			Check("a is in the room", Of(_a) != null);
			bool flag = false;
			try
			{
				flag = _b.is_inside_building;
			}
			catch
			{
			}
			if (flag)
			{
				Note("b went in of their own accord already");
				foreach (Actor item in Founded)
				{
					if (item != null && item != _a && item != _b && item != _you && item.isAlive() && !item.is_inside_building && !item.isWarrior())
					{
						_b = item;
						break;
					}
				}
				try
				{
					_b.setHomeBuilding(_house);
				}
				catch
				{
				}
				Note("b is now " + WorldBoxMod.Describe(_b));
			}
			Check("b (outside) isn't", Of(_b) == null);
			if (!Check("b sent home", SendHome(_b)))
			{
				yield break;
			}
			float t0 = Time.unscaledTime;
			float inAt = -1f;
			float cameAt = -1f;
			Vector2 first = default(Vector2);
			while (Time.unscaledTime - t0 < 70f)
			{
				yield return null;
				bool flag2 = false;
				try
				{
					flag2 = _b.is_inside_building;
				}
				catch
				{
				}
				if (flag2 && inAt < 0f)
				{
					inAt = Time.unscaledTime - t0;
				}
				RoomWalkers.Walker walker = Of(_b);
				if (walker != null && cameAt < 0f)
				{
					cameAt = Time.unscaledTime - t0;
					first = walker.Pos;
				}
				if (cameAt >= 0f && Time.unscaledTime - t0 - cameAt > 1.5f)
				{
					break;
				}
			}
			Check("b comes in once the game has them in", inAt >= 0f && cameAt >= inAt - 0.1f && cameAt - inAt < 2.5f, $"inside {inAt:F1} s, in the room {cameAt:F1} s");
			Check("...at the door", cameAt >= 0f && (first - new Vector2(Mod.Interior.Room.DoorX, Mod.Interior.Room.DoorY)).magnitude < 0.5f, (cameAt >= 0f) ? $"first seen at {first}" : "never came");
			Mod.SaveFrame("houselife_room_b_in.png");
		}

		private RoomWalkers.Walker Of(Actor a)
		{
			foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
			{
				if (item.Who == a)
				{
					return item;
				}
			}
			return null;
		}

		private IEnumerator Sleeping()
		{
			if (Of(_a) == null)
			{
				Note("a is out again: sent home once more");
				SendHome(_a);
				for (float t = 0f; t < 70f; t += 0.5f)
				{
					if (Of(_a) != null)
					{
						break;
					}
					yield return CivilTest.Wait(0.5f);
				}
			}
			if (!Check("a is in the room", Of(_a) != null))
			{
				yield break;
			}
			bool flag = false;
			try
			{
				flag = _a.makeSleep(60f);
			}
			catch (Exception ex)
			{
				Log.Error("HOUSELIFE sleep: " + ex.Message);
			}
			if (!Check("a sent to sleep", flag && HouseOccupancy.Asleep(_a)))
			{
				yield break;
			}
			float t2 = Time.unscaledTime;
			float lyingAt = -1f;
			RoomWalkers.Walker w = null;
			while (Time.unscaledTime - t2 < 20f)
			{
				yield return null;
				w = Of(_a);
				if (w != null && w.Lie > 0.95f)
				{
					lyingAt = Time.unscaledTime - t2;
					break;
				}
			}
			Check("a lies down in a bed", lyingAt >= 0f, (!(lyingAt < 0f)) ? $"after {lyingAt:F1} s at {w.Pos}, {w.Z:F2} up" : ((w == null) ? "no walker" : $"lie {w.Lie:F2}, bed {w.Bed}, role {w.Role}"));
			if (w != null && w.Lie > 0.95f)
			{
				Mod.DebugEyeSet = false;
				Mod.SetYaw(Mathf.Atan2(w.Pos.y - Mod.Interior.Eye.y, w.Pos.x - Mod.Interior.Eye.x));
				yield return CivilTest.Wait(0.4f);
				Mod.SaveFrame("houselife_room_asleep.png");
			}
			try
			{
				_a.stopSleeping();
			}
			catch
			{
			}
			t2 = Time.unscaledTime;
			float upAt = -1f;
			while (Time.unscaledTime - t2 < 10f)
			{
				yield return null;
				w = Of(_a);
				if (w != null && w.Lie <= 0f && w.Bed < 0)
				{
					upAt = Time.unscaledTime - t2;
					break;
				}
			}
			Check("woken, a gets up", upAt >= 0f, (upAt < 0f && w != null) ? $"lie {w.Lie:F2}, role {w.Role}" : $"after {upAt:F1} s");
		}

		private IEnumerator GoingOut()
		{
			if (Of(_b) == null)
			{
				Note("b is out again: sent home once more");
				SendHome(_b);
				for (float t = 0f; t < 70f; t += 0.5f)
				{
					if (Of(_b) != null)
					{
						break;
					}
					yield return CivilTest.Wait(0.5f);
				}
			}
			RoomWalkers.Walker walker = Of(_b);
			if (!Check("b is in the room to begin with", walker != null))
			{
				yield break;
			}
			float leavingAt = (walker.Leaving ? 0f : (-1f));
			Vector2 last = walker.Pos;
			Note($"b before: leaving {walker.Leaving}, at {walker.Pos}, stay left {HouseOccupancy.StayLeft(_b):F1} s");
			try
			{
				_b.exitBuilding();
				_b.clearTasks();
			}
			catch (Exception ex)
			{
				Log.Error("HOUSELIFE let out: " + ex.Message);
			}
			float t2 = Time.unscaledTime;
			float goneAt = -1f;
			while (Time.unscaledTime - t2 < 20f)
			{
				yield return null;
				RoomWalkers.Walker walker2 = Of(_b);
				if (walker2 == null)
				{
					goneAt = Time.unscaledTime - t2;
					break;
				}
				last = walker2.Pos;
				if (walker2.Leaving && leavingAt < 0f)
				{
					leavingAt = Time.unscaledTime - t2;
				}
			}
			Check("b sets off for the door", leavingAt >= 0f, $"{leavingAt:F1} s");
			Check("...and is gone by it", goneAt >= 0f && (last - new Vector2(Mod.Interior.Room.DoorX, Mod.Interior.Room.DoorY)).magnitude < 0.6f, $"gone {goneAt:F1} s, last at {last}");
			HouseOccupancy.Scan();
			Check("the house no longer counts b", !HouseOccupancy.Of(_house).Inside.Contains(_b));
		}
	}
}
