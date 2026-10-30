using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal sealed class FreedomTest : CivilTest
	{
		private readonly List<Actor> _capital = new List<Actor>();

		private readonly List<Actor> _second = new List<Actor>();

		private readonly List<Actor> _third = new List<Actor>();

		private Actor _king;

		private Actor _you;

		private Actor _lover;

		private City _home;

		private City _other;

		private Kingdom _realm;

		protected override string Prefix => "FREEDOM";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			_king = Village("human", 12, 2);
			if (!Check("the realm", _king != null))
			{
				yield break;
			}
			_capital.AddRange(Founded);
			_realm = _king.kingdom;
			Actor actor = Village("human", 10, 2, _king.current_tile, 30f) ?? Village("human", 10, 2);
			if (!Check("a second town", actor != null && actor.city != null))
			{
				yield break;
			}
			_second.AddRange(Founded);
			Actor actor2 = Village("human", 8, 1, _king.current_tile, 30f) ?? Village("human", 8, 1);
			if (!Check("a third town", actor2 != null && actor2.city != null))
			{
				yield break;
			}
			_third.AddRange(Founded);
			_home = actor.city;
			_other = actor2.city;
			try
			{
				_home.joinAnotherKingdom(_realm);
				_other.joinAnotherKingdom(_realm);
			}
			catch (Exception ex)
			{
				Note("joining the realm: " + ex.Message);
			}
			List<Actor>[] array = new List<Actor>[3] { _capital, _second, _third };
			for (int i = 0; i < array.Length; i++)
			{
				foreach (Actor item in array[i])
				{
					try
					{
						((BaseSimObject)item).addStatusEffect("invincible", 1200f, pColorEffect: false);
					}
					catch
					{
					}
				}
			}
			yield return CivilTest.Wait(1.5f);
			Check("three towns in one realm", _home.kingdom == _realm && _other.kingdom == _realm && _realm.countCities() == 3, _realm.countCities() + " towns, capital " + Rep.NameOf(_realm.capital));
			foreach (Actor item2 in _second)
			{
				try
				{
					if (item2.isAlive() && item2.isWarrior())
					{
						item2.stopBeingWarrior();
					}
				}
				catch
				{
				}
			}
			foreach (Actor item3 in _second)
			{
				if (item3 != null && item3.isAlive() && item3.isAdult() && !item3.isKing() && item3.city == _home && !item3.isCityLeader())
				{
					_you = item3;
					break;
				}
			}
			if (Check("someone of the second town to be", _you != null))
			{
				yield return Possess(_you);
				if (Check("in first person", InControl(_you)))
				{
					yield return Part("gifts", Gifts());
					Patrons.Off = true;
					yield return Part("land", Land());
					yield return Part("at home", AtHome());
					yield return Part("bought freedom", Bought());
					yield return Part("independence", Independence());
					Patrons.Off = false;
					LandPlot.AimForTest = null;
				}
			}
		}

		private void StandIn(Actor you, City c)
		{
			WorldTile tile = c.getTile();
			if (tile != null)
			{
				you.current_position = new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
			}
		}

		private void NotLeader()
		{
			if (!Rep.Rules(_you, _home))
			{
				return;
			}
			foreach (Actor item in _second)
			{
				if (item != null && item != _you && item != _lover && item.isAlive() && item.isAdult() && item.city == _home)
				{
					try
					{
						_home.setLeader(item, pNew: true);
					}
					catch (Exception ex)
					{
						Note("leader: " + ex.Message);
					}
					break;
				}
			}
			Note("you led the town: " + WorldBoxMod.Describe(_home.leader) + " leads it now");
		}

		private IEnumerator Gifts()
		{
			NotLeader();
			StandIn(_you, _home);
			yield return CivilTest.Wait(0.5f);
			Check("you stand in your town", Hub.Here == _home, Rep.NameOf(Hub.Here));
			Check("no gifts from a town that doesn't know you", !Patrons.Honours(_you, _home));
			Rep.Add(_you, _home, 1600, "test");
			Check("Honored in your town", Rep.Level(_you, _home) >= RepLevel.Honored, Rep.Name(Rep.Level(_you, _home)));
			int money = CivilTest.Money(_you);
			int gear = Gear.Count(_you);
			Patrons.SetNext(_you, _home, Quests.Now - 1.0);
			Actor giver = null;
			for (float t = 0f; t < 40f; t += 0.25f)
			{
				if (Mod.Converse.Open)
				{
					break;
				}
				if (giver == null && Patrons.GiverForTest != null)
				{
					giver = Patrons.GiverForTest;
					Note("giver: " + WorldBoxMod.Describe(giver));
				}
				yield return CivilTest.Wait(0.25f);
			}
			Conversation c = Mod.Converse;
			if (!Check("someone of the town comes with a gift", giver != null && c.Open && c.LastTopic == "gift" && c.With == giver, $"talk {c.Open}, topic {c.LastTopic}, with {WorldBoxMod.Describe(c.With)}"))
			{
				yield break;
			}
			Note("they say: " + c.Line);
			Check("\"Thank you.\" and turning it down", Find(c, "Thank you.") != null && Find(c, "Keep it.") != null);
			yield return Shot("freedom_gift.png");
			Pick(c, "Thank you.");
			yield return CivilTest.Wait(0.5f);
			Check("the gift is yours", CivilTest.Money(_you) > money || Gear.Count(_you) > gear, $"gold {money} -> {CivilTest.Money(_you)}, gear {gear} -> {Gear.Count(_you)}");
			Check("the next gift is minutes away", Patrons.NextAt(_you, _home) > Quests.Now + 150.0, (Patrons.NextAt(_you, _home) - Quests.Now).ToString("0") + " s");
			Mod.EndConversation();
			int num = 0;
			for (int i = 0; i < 30; i++)
			{
				if (Patrons.ArmorFor(_home, RepLevel.Honored, giver).Asset != null)
				{
					num++;
				}
			}
			Check("armor gifts are real items", num == 30);
			FreedomTest freedomTest = this;
			int num2 = Patrons.GoldFor(_home, RepLevel.Honored);
			freedomTest.Check("a village gives 25-45 gold", num2 >= 25 && num2 <= 45);
		}

		private IEnumerator Land()
		{
			_lover = null;
			foreach (Actor item in _second)
			{
				if (item != null && item != _you && item.isAlive() && item.isAdult() && item.city == _home && !item.isKing())
				{
					_lover = item;
					break;
				}
			}
			try
			{
				_you.becomeLoversWith(_lover);
			}
			catch (Exception ex)
			{
				Note("lovers: " + ex.Message);
			}
			Check("you have a lover", _lover != null && _you.lover == _lover);
			try
			{
				_you.addMoney(3000);
			}
			catch
			{
			}
			NotLeader();
			Check("a plain subject of the town", !Rep.Rules(_you, _home));
			StandIn(_you, _home);
			CivilTest.StandBefore(_king, _you, 1.2f);
			yield return CivilTest.Wait(0.3f);
			if (!Check("talking to the king", Mod.StartConversationForTest(_king)))
			{
				yield break;
			}
			Conversation c = Mod.Converse;
			yield return CivilTest.Wait(0.3f);
			Read(c);
			Choice choice = Find(c, "I'd like land for a house.");
			Check("the king sells land", choice?.Enabled ?? false, choice?.Note);
			Choice choice2 = Find(c, "I'd buy " + Rep.NameOf(_home));
			Check("and your town's freedom", choice2?.Enabled ?? false, choice2?.Note);
			Pick(c, "I'd like land for a house.");
			yield return CivilTest.Wait(0.3f);
			Note("sizes: " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()));
			Check("a cottage among the sizes", Find(c, "A cottage (120 gold)") != null);
			int money = CivilTest.Money(_you);
			Pick(c, "A cottage");
			yield return CivilTest.Wait(0.3f);
			Check("paid, and marking out the plot", LandPlot.Aiming && CivilTest.Money(_you) == money - 120, $"{money} -> {CivilTest.Money(_you)}");
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			Check("the crosshair mode's prompt", Hub.AnyModeActive && Hub.ModePrompt == LandPlot.Prompt);
			Check("Esc gives the gold back", LandPlot.Escape() && !LandPlot.Aiming && CivilTest.Money(_you) == money, CivilTest.Money(_you).ToString());
			string siteId = LandPlot.SiteOf(_home)?.id;
			Check("the town's houses are human", siteId == "tent_human", siteId);
			WorldTile good = null;
			WorldTile worldTile = null;
			foreach (WorldTile item2 in Tiles(_home.getTile(), 14))
			{
				bool flag = item2.zone != null && item2.zone.city == _home;
				if (good == null && flag && LandPlot.CanStand(_home, 2, item2, out var _))
				{
					good = item2;
				}
				if (worldTile == null && !flag && item2.Type != null && item2.Type.ground)
				{
					worldTile = item2;
				}
			}
			if (!Check("a clear spot on the town's land", good != null))
			{
				yield break;
			}
			int before = CivilTest.Money(_you);
			try
			{
				_you.spendMoney(LandPlot.PriceOf(2, ruler: false));
			}
			catch
			{
			}
			LandPlot.Begin(_you, _home, 2, LandPlot.PriceOf(2, ruler: false));
			if (worldTile != null)
			{
				LandPlot.AimForTest = worldTile;
				yield return CivilTest.Wait(0.3f);
				Check("off the town's land the ghost is red", !LandPlot.ValidForTest, LandPlot.WhyForTest);
			}
			LandPlot.AimForTest = good;
			yield return CivilTest.Wait(0.3f);
			Check("on clear town land it is green", LandPlot.ValidForTest, LandPlot.WhyForTest);
			Vector2 vector = new Vector2((float)good.x + 0.5f, (float)good.y + 0.5f);
			Vector2 debugEye = vector + new Vector2(-6f, -5.5f);
			Mod.DebugEye = debugEye;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(vector.y - debugEye.y, vector.x - debugEye.x));
			Mod.SetPitchDegrees(-14f);
			yield return CivilTest.Wait(0.6f);
			yield return Shot("freedom_ghost.png");
			Mod.DebugEyeSet = false;
			Mod.SetPitchDegrees(0f);
			Building site = LandPlot.Place(_you, good);
			LandPlot.AimForTest = null;
			if (!Check("the plot becomes a site", site != null && site.isUnderConstruction() && site.asset.id == siteId, site?.asset?.id))
			{
				yield break;
			}
			Check("the site is yours, in your town", Homes.OwnedBy(site, _you) && _home.buildings.Contains(site));
			Check("one plot at a time", LandPlot.PendingSite(_you) == site);
			for (float t = 0f; t < 45f; t += 0.5f)
			{
				if (!site.isAlive())
				{
					break;
				}
				if (!site.isUnderConstruction())
				{
					break;
				}
				site.getConstructionProgress();
				yield return CivilTest.Wait(0.5f);
			}
			FreedomTest freedomTest = this;
			object obj3;
			int tier;
			if (!site.isUnderConstruction())
			{
				obj3 = "built";
			}
			else
			{
				tier = site.getConstructionProgress();
				obj3 = "still a site, " + tier + "/" + site.asset.construction_progress_needed;
			}
			freedomTest.Note("after the builders: " + (string)obj3);
			if (site.isUnderConstruction())
			{
				for (int num = 0; num < 200; num++)
				{
					if (!site.isUnderConstruction())
					{
						break;
					}
					site.updateBuild(1);
				}
			}
			Building house = null;
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				yield return CivilTest.Wait(0.25f);
				if (HomeLife.OwnHouse(_you) != null)
				{
					house = HomeLife.OwnHouse(_you);
					break;
				}
			}
			if (!Check("built, it is your home", house != null && Workbench.HomeOf(_you) == house))
			{
				yield break;
			}
			Check("the house paid for", LandPlot.TierOf(house) == 2, house.asset.id);
			Check("the site record is cleared", LandPlot.PendingSite(_you) == null && !LandPlot.Read(_you, out var _, out var _, out tier, out var _));
			Check("your lover lives there", _lover.getHomeBuilding() == house);
			Check("the gold spent", CivilTest.Money(_you) == before - LandPlot.PriceOf(2, ruler: false), $"{before} -> {CivilTest.Money(_you)}");
			Actor stranger = null;
			foreach (Actor item3 in _second)
			{
				if (item3 != null && item3 != _you && item3 != _lover && item3.isAlive() && item3.isAdult() && item3.city == _home)
				{
					stranger = item3;
					break;
				}
			}
			if (stranger != null)
			{
				stranger.setHomeBuilding(house);
				yield return CivilTest.Wait(4f);
				Check("a stranger the game moves in moves out", stranger.getHomeBuilding() != house && !house.residents.Contains(stranger.data.id));
			}
			if (stranger != null)
			{
				Homes.SetOwner(house, null);
				try
				{
					house.data.set("fp_owner", "987654321");
				}
				catch
				{
				}
				Check("a dead owner's house is open to the town again", HouseKeep.Held(house) == 0L && HouseKeep.MayLive(house, stranger));
				Homes.SetOwner(house, _you);
				Check("a living owner's is not", HouseKeep.Held(house) != 0L && !HouseKeep.MayLive(house, stranger));
			}
			BuildingAsset buildingAsset = AssetManager.buildings.get("temple_human");
			MethodInfo method = typeof(CityBehBuild).GetMethod("getOnHouseTile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			int num2 = 0;
			int sparedForTest = HouseKeep.SparedForTest;
			for (int num3 = 0; num3 < 40; num3++)
			{
				if (buildingAsset == null)
				{
					break;
				}
				if (!(method != null))
				{
					break;
				}
				if (method.Invoke(null, new object[2] { _home, buildingAsset }) as WorldTile == house.current_tile)
				{
					num2++;
				}
			}
			Check("the town never picks your house to build over", HouseKeep.Installed && num2 == 0 && HouseKeep.SparedForTest > sparedForTest, $"installed {HouseKeep.Installed}, picked {num2}, passed over {HouseKeep.SparedForTest - sparedForTest}");
		}

		private IEnumerator AtHome()
		{
			Building house = HomeLife.OwnHouse(_you);
			if (!Check("a house to go home to", house != null))
			{
				yield break;
			}
			try
			{
				if (_lover.is_inside_building)
				{
					_lover.exitBuilding();
				}
			}
			catch
			{
			}
			Mod.EnterHouseForTest(house);
			yield return CivilTest.Wait(1f);
			if (!Check("in your house", Mod.Interior.Inside && Mod.Interior.House == house))
			{
				yield break;
			}
			Check("your lover isn't in yet", Mod.Interior.WalkersForTest.Count == 0 || !HasWalker(_lover));
			try
			{
				_lover.cancelAllBeh();
				_lover.current_position = (Vector2)house.current_tile.posV3 + new Vector2(5f, -3f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Check("your lover is sent home", HomeLife.SendHome(_lover));
			for (float t = 0f; t < 60f; t += 0.5f)
			{
				if (_lover.inside_building == house)
				{
					break;
				}
				if (Mathf.Repeat(t, 10f) < 0.25f && t > 1f && (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_lover.ai)?.task == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_lover.ai).task.id != "fp_home_visit"))
				{
					HomeLife.SendHome(_lover);
				}
				if (Mathf.Repeat(t, 5f) < 0.25f)
				{
					Note(string.Format("lover: task {0}, {1:F1} tiles from the house, inside {2}", ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_lover.ai)?.task?.id, (_lover.current_position - (Vector2)house.current_tile.posV3).magnitude, (_lover.inside_building != null) ? _lover.inside_building.asset.id : "nothing"));
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("your lover comes home", _lover.inside_building == house);
			for (float t = 0f; t < 3f; t += 0.25f)
			{
				if (HasWalker(_lover))
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("and in at the door", HasWalker(_lover));
			yield return CivilTest.Wait(2f);
			yield return Shot("freedom_home.png");
			HomeLife.VisitsOff = true;
			try
			{
				_lover.cancelAllBeh();
				_lover.exitBuilding();
				_lover.makeWait(6f);
			}
			catch
			{
			}
			bool left = false;
			for (int f = 0; f < 600; f++)
			{
				if (left)
				{
					break;
				}
				RoomWalkers.Walker walker = null;
				foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
				{
					if (item.Who == _lover)
					{
						walker = item;
					}
				}
				left = walker?.Leaving ?? true;
				yield return null;
			}
			Check("and out again", left, "inside " + ((_lover.inside_building != null) ? _lover.inside_building.asset.id : "nothing"));
			HomeLife.VisitsOff = false;
			Interior room = Mod.Interior.Room;
			if (!Check("a bed in the room", HomeLife.BedOf(room, out var bed)))
			{
				yield break;
			}
			Check("not at the bed from the door", !HomeLife.NearBed(room, new Vector2(room.DoorX, room.DoorY)) || (bed - new Vector2(room.DoorX, room.DoorY)).magnitude < 1.5f);
			Vector2 by = bed + (new Vector2(room.SpawnX, room.SpawnY) - bed).normalized * 1f;
			Mod.Interior.Eye = by;
			yield return CivilTest.Wait(0.3f);
			Check("at the bed", HomeLife.NearBed(room, Mod.Interior.Eye), Mod.Prompt);
			float num = Mathf.Atan2(bed.y - by.y, bed.x - by.x);
			Check("the bed asked when faced", HomeLife.NearBed(room, by, num));
			Check("...not with your back to it", !HomeLife.NearBed(room, by, num + MathF.PI));
			int day = Calendar.Day;
			HomeLife.StartSleep();
			yield return CivilTest.Wait(1.6f);
			yield return Shot("freedom_sleep.png");
			yield return CivilTest.Wait(2f);
			Check("you sleep to the next dawn", Calendar.Day == day + 1 && HomeLife.SleptToDay == day + 1, $"day {day} -> {Calendar.Day}");
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			Actor actor = null;
			foreach (Actor item2 in _second)
			{
				if (item2 != null && item2 != _you && item2 != _lover && item2.isAlive() && item2.city == _home)
				{
					actor = item2;
					break;
				}
			}
			if (actor != null)
			{
				LandPlot.OnBodyDying(_you);
				LandPlot.OnBodyDied(_you, actor);
				Check("your heir takes the house", Homes.OwnedBy(house, actor) && actor.getHomeBuilding() == house);
				HomeLife.MoveIn(_you, house);
			}
		}

		private bool HasWalker(Actor a)
		{
			foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
			{
				if (item.Who == a && !item.Leaving)
				{
					return true;
				}
			}
			return false;
		}

		private IEnumerator Bought()
		{
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.5f);
			}
			StandIn(_you, _home);
			Check("the capital can't be bought", Freedom.WhyNot(_you, _realm.capital) == "Not the capital");
			try
			{
				_you.addMoney(6000);
			}
			catch
			{
			}
			CivilTest.StandBefore(_king, _you, 1.2f);
			yield return CivilTest.Wait(0.3f);
			if (Check("talking to the king", Mod.StartConversationForTest(_king)))
			{
				Conversation c = Mod.Converse;
				yield return CivilTest.Wait(0.3f);
				Note("the king asks about " + Freedom.Price(_home, _king, _you));
				Pick(c, "I'd buy " + Rep.NameOf(_home));
				yield return CivilTest.Wait(0.3f);
				Note("he says: " + c.Line);
				int money = CivilTest.Money(_you);
				Pick(c, "Pay ");
				yield return CivilTest.Wait(1f);
				Kingdom kingdom = _home.kingdom;
				Check("your town is a realm of its own", kingdom != null && kingdom != _realm && kingdom.isAlive(), Royal.NameOf(kingdom));
				Check("you are its king", kingdom != null && kingdom.king == _you && Royal.IsKing(_you));
				Check("bought, not fought", kingdom != null && !Royal.AtWar(kingdom, _realm));
				Check("the gold paid", CivilTest.Money(_you) < money && money - CivilTest.Money(_you) >= 500, $"{money} -> {CivilTest.Money(_you)}");
				Check("the old realm keeps its other towns", _realm.isAlive() && _other.kingdom == _realm && _king.kingdom == _realm && Royal.IsKing(_king));
				Mod.EndConversation();
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator Independence()
		{
			Actor rebel = null;
			foreach (Actor item in _third)
			{
				if (item != null && item.isAlive() && item.isAdult() && !item.isKing() && item.city == _other && !item.isCityLeader())
				{
					rebel = item;
					break;
				}
			}
			if (Check("someone of the third town", rebel != null))
			{
				yield return Possess(rebel);
				if (Check("in their body", InControl(rebel)))
				{
					StandIn(rebel, _other);
					Check("Honored needed to break away", Freedom.WhyNot(rebel, _other) == "Honored in " + Rep.NameOf(_other), Freedom.WhyNot(rebel, _other));
					Rep.Add(rebel, _other, 1600, "test");
					Check("then nothing stands in the way", Freedom.WhyNot(rebel, _other) == null, Freedom.WhyNot(rebel, _other));
					Mod.ShowMapPage(MapTabs.Page.Map);
					yield return CivilTest.Wait(0.3f);
					Journal.Tab = 2;
					yield return Shot("freedom_standing.png");
					Check("the Standing tab shows your town's row", Journal.Open && H.FreedomHeightForTest(rebel) > 0f);
					Mod.CloseMapWindow();
					int standing = Rep.StandingPoints(rebel, _realm);
					bool ok = Freedom.Declare(rebel, Mod.Toast);
					yield return CivilTest.Wait(1f);
					Kingdom kingdom = _other.kingdom;
					Check("the town declares independence", ok && kingdom != null && kingdom != _realm && kingdom.king == rebel, Royal.NameOf(kingdom));
					Check("and its old realm goes to war with it", kingdom != null && Royal.AtWar(_realm, kingdom));
					Check("the old realm thinks less of you", Rep.StandingPoints(rebel, _realm) <= standing - 1000 + 1, $"{standing} -> {Rep.StandingPoints(rebel, _realm)}");
					Check("a king can't declare again", Freedom.WhyNot(rebel, _other) != null);
				}
			}
		}

		private static IEnumerable<WorldTile> Tiles(WorldTile c, int r)
		{
			if (c == null)
			{
				yield break;
			}
			for (int d = 2; d <= r; d++)
			{
				for (int y = -d; y <= d; y++)
				{
					for (int x = -d; x <= d; x++)
					{
						if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)) == d)
						{
							WorldTile tile = World.world.GetTile(c.x + x, c.y + y);
							if (tile != null)
							{
								yield return tile;
							}
						}
					}
				}
			}
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.4f);
			H.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!H.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}

		private static Choice Find(Conversation c, string label)
		{
			foreach (Choice choice in c.Choices)
			{
				if (choice.Label != null && choice.Label.StartsWith(label, StringComparison.Ordinal))
				{
					return choice;
				}
			}
			return null;
		}

		private static void Read(Conversation c)
		{
			for (int i = 0; i < 6; i++)
			{
				if (!c.MorePages)
				{
					break;
				}
				c.Next();
				c.Next();
			}
		}

		private void Pick(Conversation c, string label)
		{
			Read(c);
			Choice choice = Find(c, label);
			if (choice == null)
			{
				Note("(no choice \"" + label + "\": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
			}
			else
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
			}
		}
	}
}
