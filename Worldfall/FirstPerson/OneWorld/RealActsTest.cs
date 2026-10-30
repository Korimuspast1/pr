using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.OneWorld
{
	internal sealed class RealActsTest : CivilTest
	{
		protected override string Prefix => "REALACTS";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 12, 2);
			if (!Check("a village founded", king != null))
			{
				yield break;
			}
			City city = king.city;
			Building house = AddHouse("house_human_1", city);
			Building hall = AddHouse("hall_human_2", city);
			Check("a house and a hall", house != null && hall != null);
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Founded)
			{
				if (item != null && item != king && item.isAlive() && item.isAdult())
				{
					list.Add(item);
				}
			}
			if (!Check("villagers", list.Count >= 3))
			{
				yield break;
			}
			Actor you = list[0];
			Actor homebody = list[1];
			Actor sitter = list[2];
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			yield return Possess(you);
			if (Check("in first person", InControl(you)))
			{
				Mod.Day.TimeOfDay = 0.5f;
				yield return Part("in by the door", Door(homebody, house));
				yield return Part("sitting made real", Sit(sitter));
				if (hall != null)
				{
					yield return Part("court in the hall", CourtHall(king, hall));
				}
				yield return Part("torches after dark", Torch(you));
			}
		}

		private IEnumerator Door(Actor a, Building house)
		{
			if (house == null)
			{
				yield break;
			}
			try
			{
				a.setHomeBuilding(house);
			}
			catch
			{
			}
			int before = RealActs.DoorEntriesForTest;
			bool ok = Homebodies.Send(a);
			Check("sent home", ok, ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id);
			WorldTile door = house.door_tile;
			WorldTile last = null;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 40f && !a.is_inside_building)
			{
				last = a.current_tile;
				yield return null;
			}
			Note($"in after {Time.unscaledTime - t0:F1} s; last tile {last?.x},{last?.y}, door {door?.x},{door?.y}, front {house.current_tile.x},{house.current_tile.y}; door steps {RealActs.DoorEntriesForTest - before}");
			Check("...in", a.is_inside_building && a.inside_building == house);
			Check("...by the door tile", RealActs.DoorEntriesForTest > before && last != null && (last == door || Mathf.Max(Mathf.Abs(last.x - door.x), Mathf.Abs(last.y - door.y)) <= 1));
			RoomLayout layout;
			RoomLayout.Place place;
			for (float t1 = 0f; t1 < 12f; t1 += 0.25f)
			{
				if (InsideSpots.Where(a, out layout, out place))
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("...and has a place inside", InsideSpots.Where(a, out layout, out place));
		}

		private IEnumerator Sit(Actor a)
		{
			RealActs.CourtOn = false;
			IEnumerator body = SitBody(a);
			while (body.MoveNext())
			{
				yield return body.Current;
			}
			RealActs.CourtOn = true;
		}

		private IEnumerator SitBody(Actor a)
		{
			try
			{
				a.cancelAllBeh();
				a.makeWait(30f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			Vector2 seat = a.current_position + new Vector2(1.6f, 0.4f);
			bool flag = RealActs.Sit(a, seat, 10f);
			Check("a seat given", flag, ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id);
			if (flag)
			{
				float t0 = Time.unscaledTime;
				while (Time.unscaledTime - t0 < 15f && (a.current_position - seat).sqrMagnitude > 0.01f)
				{
					yield return CivilTest.Wait(0.2f);
				}
				Check("...walked there and sat", (a.current_position - seat).sqrMagnitude <= 0.01f, $"{(a.current_position - seat).magnitude:F2} off after {Time.unscaledTime - t0:F1} s");
				yield return CivilTest.Wait(2f);
				Check("...held there", (a.current_position - seat).sqrMagnitude <= 0.01f && RealActs.Sitting(a), ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id);
			}
		}

		private IEnumerator CourtHall(Actor king, Building hall)
		{
			Note("capital hall " + ((Court.CapitalHall(king.kingdom) == hall) ? "is" : "is not") + " the test hall");
			try
			{
				king.cancelAllBeh();
				king.makeWait(5f);
			}
			catch
			{
			}
			Mod.Day.TimeOfDay = 0.5f;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 45f && (!king.is_inside_building || king.inside_building != hall))
			{
				yield return CivilTest.Wait(0.5f);
			}
			Check("by day the king goes into the hall", king.is_inside_building && king.inside_building == hall, $"task {((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)king.ai)?.task?.id}, after {Time.unscaledTime - t0:F1} s");
			Mod.Day.TimeOfDay = 0.85f;
			t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 5f && king.is_inside_building)
			{
				yield return CivilTest.Wait(0.25f);
			}
			Check("at night out again, at its door", !king.is_inside_building && king.current_tile == hall.door_tile, $"tile {king.current_tile?.x},{king.current_tile?.y}");
			Mod.Day.TimeOfDay = 0.5f;
		}

		private IEnumerator Torch(Actor you)
		{
			Actor bandit = CivilTest.SpawnNear("bandit", you.current_position + new Vector2(4f, 0f));
			if (Check("a bandit near", bandit != null))
			{
				Mod.Day.TimeOfDay = 0f;
				yield return CivilTest.Wait(2.5f);
				bool lit = false;
				try
				{
					lit = ((BaseSimObject)bandit).hasStatus("fp_torch");
				}
				catch
				{
				}
				Check("after dark: the torch's light on them", lit, $"{RealActs.TorchesForTest} torches");
				Mod.Day.TimeOfDay = 0.5f;
				yield return CivilTest.Wait(4f);
				try
				{
					lit = ((BaseSimObject)bandit).hasStatus("fp_torch");
				}
				catch
				{
				}
				Check("by day: gone", !lit);
			}
		}
	}
}
