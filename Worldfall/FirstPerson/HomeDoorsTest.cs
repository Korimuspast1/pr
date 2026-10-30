using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class HomeDoorsTest : CivilTest
	{
		private Actor _you;

		private City _city;

		private Building _home;

		protected override string Prefix => "HOMEDOORS";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			Actor actor = Village("human", 6, 0, worldTile);
			if (!Check("a village", actor != null && actor.city != null))
			{
				yield break;
			}
			_city = actor.city;
			_you = ((Founded.Count > 1) ? Founded[1] : actor);
			yield return Possess(_you);
			if (!Check("first person", InControl(_you)))
			{
				yield break;
			}
			_home = AddHouse("house_human_2", _city);
			if (Check("your house built", _home != null))
			{
				Homes.SetOwner(_home, _you);
				FirstPersonSave.PathForTest = Path.Combine(Log.Folder, "fpsave_test");
				yield return Part("lock", Lock());
				if (!Options.Contains("nosave"))
				{
					yield return Part("bedroll", Bedroll());
				}
				Homebodies.Off = true;
				yield return Part("doors", Doors());
				yield return Part("homebody", Homebody());
				Homebodies.Off = false;
				if (!Options.Contains("nosave"))
				{
					yield return Part("save", Save());
				}
				FirstPersonSave.PathForTest = null;
			}
		}

		private IEnumerator Lock()
		{
			Check("the lock is on the game's going in", HomeLock.Installed);
			Vector2 at = _home.current_position + new Vector2(0f, -2f);
			Actor stranger = CivilTest.Spawn("human", at);
			Actor child = CivilTest.Spawn("human", at, adult: false);
			if (Check("a stranger and your child", stranger != null && child != null))
			{
				child.setParent1(_you);
				Check("your child is family", HomeLock.IsFamily(child, _you) && !HomeLock.IsFamily(stranger, _you));
				HomeLock.SetLocked(_home, locked: true);
				Check("your house locked", HomeLock.Locked(_home) && HomeLock.OwnerOf(_home) == _you);
				int turnedAway = HomeLock.TurnedAway;
				BehStayInBuildingTarget stay = new BehStayInBuildingTarget(20f, 30f);
				stranger.beh_building_target = _home;
				BehResult behResult = stay.execute(stranger);
				Check("a stranger the game sends in turned away", behResult == BehResult.Stop && !stranger.is_inside_building && HomeLock.TurnedAway == turnedAway + 1, $"{behResult}, inside {stranger.is_inside_building}");
				child.beh_building_target = _home;
				behResult = stay.execute(child);
				Check("your child goes in", behResult == BehResult.Continue && child.is_inside_building && child.inside_building == _home, $"{behResult}");
				Check("may enter: you and yours, not him", HomeLock.MayEnter(_home, _you) && HomeLock.MayEnter(_home, child) && !HomeLock.MayEnter(_home, stranger));
				yield return CivilTest.Wait(0.3f);
				HomeCard.Show(_home, _you);
				Check("the Home card lists your family", HomeCard.Open && HomeCard.House == _home && HomeCard.Family.Contains(child), $"{HomeCard.Family.Count} listed");
				yield return CivilTest.Wait(0.5f);
				Mod.SaveFrame("homedoors_card.png");
				HomeCard.Close();
				Check("the Home card closed", !HomeCard.Open);
				HomeLock.SetLocked(_home, locked: false);
				stranger.beh_building_target = _home;
				behResult = stay.execute(stranger);
				Check("unlocked, the stranger goes in", behResult == BehResult.Continue && stranger.is_inside_building, $"{behResult}");
				Check("your own door opens to you", HouseDoors.MayWalkIn(_home, _you) && HouseDoors.MaySleepIn(_home, _you));
				try
				{
					stranger.exitBuilding();
					child.exitBuilding();
				}
				catch
				{
				}
				HomeLock.SetLocked(_home, locked: true);
			}
		}

		private IEnumerator Doors()
		{
			Building building = AddHouse("house_human_0", _city);
			Building building2 = AddHouse("house_human_0", _city);
			Building building3 = AddHouse("house_human_0", _city);
			Building building4 = AddHouse("house_human_0", _city);
			if (!Check("four houses of others", building != null && building2 != null && building3 != null && building4 != null, $"{building != null} {building2 != null} {building3 != null} {building4 != null}"))
			{
				yield break;
			}
			Actor actor = In(building);
			Actor actor2 = In(building2);
			Actor actor3 = In(building4);
			if (!Check("someone in three of them", actor != null && actor2 != null && actor3 != null))
			{
				yield break;
			}
			Chronicle.ChangeOpinion(actor2, _you, 40);
			Chronicle.ChangeOpinion(actor3, _you, 40);
			Homes.SetOwner(building4, actor3);
			HomeLock.SetLocked(building4, locked: true);
			Check("a stranger's door opens to you", HouseDoors.MayWalkIn(building, _you) && HouseDoors.MaySleepIn(building, _you));
			Check("a friend's door opens to you", HouseDoors.MayWalkIn(building2, _you));
			Check("an empty house's door opens to you", HouseDoors.MayWalkIn(building3, _you));
			Check("a locked house: only the family", !HouseDoors.MayWalkIn(building4, _you) && HouseDoors.MayWalkIn(building4, actor3));
			Actor[] array = new Actor[3] { actor, actor2, actor3 };
			foreach (Actor actor4 in array)
			{
				try
				{
					actor4.exitBuilding();
				}
				catch
				{
				}
			}
		}

		private IEnumerator Homebody()
		{
			Building house = AddHouse("house_human_0", _city);
			if (!Check("a house of others", house != null))
			{
				yield break;
			}
			Actor a = CivilTest.Spawn("human", house.current_position + new Vector2(2f, -3f));
			if (!Check("someone who lives there", a != null))
			{
				yield break;
			}
			try
			{
				if (a.city != _city)
				{
					a.joinCity(_city);
				}
				a.setHomeBuilding(house);
				a.cancelAllBeh();
			}
			catch (Exception ex)
			{
				Note("home: " + ex.Message);
			}
			yield return CivilTest.Wait(0.2f);
			try
			{
				a.cancelAllBeh();
			}
			catch
			{
			}
			Check("their home, nobody in", a.getHomeBuilding() == house && HouseOccupancy.Count(house) == 0, $"home {a.getHomeBuilding() == house}");
			int sent = Homebodies.Sent;
			bool flag = Homebodies.CallHome(house, _you);
			Check("called home", flag && Homebodies.Sent == sent + 1, ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id ?? "(no task)");
			for (float t = 0f; t < 25f; t += 0.5f)
			{
				if (a.is_inside_building)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			HouseOccupancy.Scan();
			Check("in the house now", a.is_inside_building && a.inside_building == house && HouseOccupancy.Count(house) == 1, ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id ?? "(no task)");
			try
			{
				a.exitBuilding();
			}
			catch
			{
			}
		}

		private Actor In(Building b)
		{
			Actor actor = CivilTest.Spawn("human", b.current_position + new Vector2(0f, -2f));
			if (actor == null)
			{
				return null;
			}
			actor.stayInBuilding(b);
			actor.timer_action = 300f;
			return actor;
		}

		private IEnumerator Save()
		{
			string folder = FirstPersonSave.PathForTest;
			Mod.EnterHouseForTest(_home);
			for (float t = 0f; t < 5f; t += 0.2f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			if (!Check("in your house", Mod.Interior.Inside && Mod.Interior.House == _home))
			{
				yield break;
			}
			yield return CivilTest.Wait(1f);
			long youId = _you.data.id;
			long homeId = _home.getID();
			int saves = FirstPersonSave.Saves;
			HomeLife.StartSleep();
			for (float t = 0f; t < 4f; t += 0.1f)
			{
				if (FirstPersonSave.Saves != saves)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			if (!Check("a sleep saves", FirstPersonSave.Saves == saves + 1, FirstPersonSave.LastPath))
			{
				yield break;
			}
			string[] array = (Directory.Exists(folder) ? Directory.GetFiles(folder) : new string[0]);
			Check("saved to the test's folder", array.Length != 0 && Path.GetFullPath(FirstPersonSave.LastPath) == Path.GetFullPath(folder), string.Format("{0} files: {1}", array.Length, string.Join(", ", Array.ConvertAll(array, Path.GetFileName))));
			World.world.map_stats.custom_data.get("fp_player", out var pResult, null);
			World.world.map_stats.custom_data.get("fp_player_in", out var pResult2, null);
			Check("the save knows who and where you are", pResult == youId.ToString() && pResult2 == homeId.ToString(), pResult + " in " + pResult2);
			for (float t = 0f; t < 4f; t += 0.1f)
			{
				if (!HomeLife.Sleeping)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			object before = World.world.map_stats;
			int restores = FirstPersonSave.Restores;
			Mod.LeaveRoomForTest();
			SaveManager.setCurrentPathAndId(folder, -1);
			World.world.save_manager.startLoadSlot();
			float t2 = Time.unscaledTime;
			while (Time.unscaledTime - t2 < 90f && (World.world.map_stats == before || WorldBoxMod.WorldLoading || !Config.game_loaded))
			{
				yield return CivilTest.Wait(0.25f);
			}
			if (Check("the world loaded again", World.world.map_stats != before && !WorldBoxMod.WorldLoading, $"after {Time.unscaledTime - t2:F0} s"))
			{
				Check("it knows the folder it came from", FirstPersonSave.SlotForTest == folder.TrimEnd('/', '\\'), FirstPersonSave.SlotForTest ?? "(none)");
				t2 = Time.unscaledTime;
				while (Time.unscaledTime - t2 < 20f && (!Mod.IsFirstPerson || Mod.Host == null || Mod.Host.data.id != youId))
				{
					yield return CivilTest.Wait(0.25f);
				}
				Check("straight back in your body", FirstPersonSave.Restores == restores + 1 && Mod.IsFirstPerson && Mod.Host != null && Mod.Host.data.id == youId, string.Format("after {0:F1} s, {1}", Time.unscaledTime - t2, (Mod.Host != null) ? WorldBoxMod.Describe(Mod.Host) : "nobody"));
				if (Mod.Host != null)
				{
					_you = Mod.Host;
				}
				t2 = Time.unscaledTime;
				while (Time.unscaledTime - t2 < 25f && (!Mod.Interior.Inside || Mod.Interior.House == null || Mod.Interior.House.getID() != homeId))
				{
					yield return CivilTest.Wait(0.25f);
				}
				Check("and in your house", Mod.Interior.Inside && Mod.Interior.House != null && Mod.Interior.House.getID() == homeId, $"after {Time.unscaledTime - t2:F1} s");
				_home = Mod.Interior.House;
				Check("your door still locked", _home != null && HomeLock.Locked(_home) && HomeLock.OwnerOf(_home) == _you);
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame("homedoors_resumed.png");
			}
		}

		private string Peace()
		{
			StringBuilder stringBuilder = new StringBuilder();
			try
			{
				foreach (KeyValuePair<Kingdom, int> item in Law.WantedIn(_you))
				{
					Law.SetBounty(item.Key, _you, 0);
				}
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.attack_target == _you)
					{
						stringBuilder.Append(WorldBoxMod.Describe(simple)).Append(' ').Append((simple.current_position - _you.current_position).magnitude.ToString("F0"))
							.Append(" away; ");
						simple.clearAttackTarget();
					}
				}
			}
			catch (Exception ex)
			{
				Note("peace: " + ex.Message);
			}
			return stringBuilder.ToString();
		}

		private IEnumerator Bedroll()
		{
			if (_you == null || !InControl(_you))
			{
				Check("in control for the bedroll", ok: false);
				yield break;
			}
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			yield return CivilTest.Wait(1f);
			float calm = Time.unscaledTime;
			while (Time.unscaledTime - calm < 10f)
			{
				string text = Peace();
				if (text.Length > 0)
				{
					Note("after you: " + text);
				}
				if (text.Length == 0 && !Economy.InAFight(_you))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Note(string.Format("calm after {0:F1} s{1}", Time.unscaledTime - calm, Economy.InAFight(_you) ? ", still in a fight" : ""));
			Supplies.Give(_you, "tw_item_bedroll", 1);
			int bedrollNights = WorldBoxMod.BedrollNights;
			int saves = FirstPersonSave.Saves;
			string text2 = Mod.SleepOnBedrollForTest(_you);
			Check("laid out the bedroll", text2 == null && WorldBoxMod.BedrollNights == bedrollNights + 1 && HomeLife.Sleeping, text2);
			for (float t = 0f; t < 4f; t += 0.1f)
			{
				if (FirstPersonSave.Saves != saves)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Check("a night on it saves", FirstPersonSave.Saves == saves + 1);
			for (float t = 0f; t < 4f; t += 0.1f)
			{
				if (!HomeLife.Sleeping)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Mod.EnterHouseForTest(_home);
			for (float t = 0f; t < 5f; t += 0.2f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			bedrollNights = WorldBoxMod.BedrollNights;
			text2 = Mod.SleepOnBedrollForTest(_you);
			Check("no bedroll indoors", text2 != null && WorldBoxMod.BedrollNights == bedrollNights && !HomeLife.Sleeping, text2);
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
		}
	}
}
