using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class TownsTest : CivilTest
	{
		private Actor _king;

		private Actor _you;

		private City _a;

		private City _b;

		private City _c;

		private readonly List<Actor> _people = new List<Actor>();

		private readonly List<Building> _houses = new List<Building>();

		private int _dayBefore = -1;

		private static readonly Type[] StubTypes = new Type[5]
		{
			typeof(TownArtTest),
			typeof(HouseLifeTest),
			typeof(PlanningTest),
			typeof(CalendarTest),
			typeof(CourtTest)
		};

		protected override string Prefix => "TOWNS";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			RepHud.Install();
			_king = Village("human", 12, 0);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			_a = _king.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _king && item.isAdult())
				{
					_people.Add(item);
				}
			}
			if (!Check("villagers to be", _people.Count >= 4, _people.Count.ToString()))
			{
				yield break;
			}
			_you = _people[0];
			foreach (Actor person in _people)
			{
				try
				{
					((BaseSimObject)person).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			try
			{
				((BaseSimObject)_king).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			_b = Village("human", 4, 0, _king.current_tile, 40f)?.city;
			_c = Village("human", 4, 0, _king.current_tile, 60f)?.city;
			Kingdom kingdom = _a.kingdom;
			City[] array = new City[2] { _b, _c };
			foreach (City city in array)
			{
				if (city != null)
				{
					try
					{
						city.joinAnotherKingdom(kingdom);
					}
					catch (Exception ex)
					{
						Note("join: " + ex.Message);
					}
				}
			}
			yield return CivilTest.Wait(1f);
			ClaimLand(_a);
			yield return Possess(_you);
			if (Check("first person on a villager", InControl(_you)))
			{
				yield return CivilTest.Wait(1f);
				yield return Part("packs", Packs());
				yield return Part("tiers", Tiers());
				yield return Part("reputation", Reputation());
				yield return Part("gating and talk", Gating());
				yield return Part("keys and windows", KeysAndWindows());
				yield return Part("items, homes and shops", ItemsHomesShops());
				yield return Part("rooms", RoomsPart());
				yield return Part("heirs and the calendar", HeirsAndCalendar());
				yield return Part("pictures", Pictures());
				yield return Part("stub tests", Stubs());
				yield return Part("save and load", SaveAndLoad());
			}
		}

		private IEnumerator Packs()
		{
			List<string> list = Models.TownsPacks();
			Check("the towns packs are indexed", list.Contains("towns.found"), string.Join(", ", list.ToArray()));
			Check("the test model is in an index", Models.Has("tw_test_marker"));
			long bytes;
			int models;
			int num = Models.TownsPackState("towns.found", out bytes, out models);
			Note($"towns.found: state {num}, {bytes} bytes");
			Check("not loaded before anything asks", num == 0, num.ToString());
			Building house = AddHouse("house_human_1");
			if (Check("a house to draw", house != null))
			{
				string before = Models.BuildingFamily(house);
				string plain = Models.BuildingFamily(house.asset);
				int gen = TownModels.Generation;
				TownModels.PickForTest = (Building b, string family) => (!family.StartsWith("house_", StringComparison.Ordinal)) ? null : "tw_test_marker";
				string text = Models.BuildingFamily(house);
				Check("while its pack loads, today's model draws it", text == before, text);
				float waited = 0f;
				while (!Models.PackReady("tw_test_marker") && waited < 8f)
				{
					yield return CivilTest.Wait(0.1f);
					waited += 0.1f;
				}
				Check("the pack loads on a worker", Models.PackReady("tw_test_marker"), waited.ToString("0.0") + " s");
				Check("...and moves the generation on", TownModels.Generation > gen, gen + " -> " + TownModels.Generation);
				string text2 = Models.BuildingFamily(house);
				Check("then the tw_ model draws it", text2 == "tw_test_marker", text2);
				Check("the tw_ name remembers the model it stands in for", TownModels.Base("tw_test_marker") == plain, TownModels.Base("tw_test_marker") + " vs " + plain);
				MeshModel meshModel = Models.Get("tw_test_marker");
				Check("the model reads whole", meshModel != null && meshModel.Parts.Length == 1 && meshModel.Info("kind") == "test");
				Check("a name in no pack gives today's model", Models.BuildingFamily(house) != null && !Models.Has("tw_no_such_model"));
				yield return LookAt(house, 4.5f);
				Mod.SaveFrame("towns_marker.png");
				yield return CivilTest.Wait(0.3f);
				TownModels.PickForTest = null;
				TownModels.Bump();
				Check("with the picker gone, today's model again", Models.BuildingFamily(house) == before, Models.BuildingFamily(house));
				Mod.DebugEyeSet = false;
			}
		}

		private Building HallOf(City c)
		{
			try
			{
				if (c.buildings_dict_type != null && c.buildings_dict_type.TryGetValue("type_hall", out var value))
				{
					foreach (Building item in value)
					{
						if (item != null && item.isAlive())
						{
							return item;
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private IEnumerator Tiers()
		{
			Building hall = HallOf(_a);
			if (hall == null)
			{
				hall = AddBuilding("hall_human_0", _a, 0);
				Note("no hall yet: one placed (" + ((hall != null) ? hall.asset.id : "failed") + ")");
			}
			if (!Check("the town has a hall", hall != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(1.5f);
			try
			{
				if (hall.isUnderConstruction())
				{
					hall.completeConstruction();
					Note("the hall finished building");
				}
			}
			catch
			{
			}
			List<string> seen = new List<string>();
			Action<City, Tier, Tier> onChange = delegate(City c, Tier from, Tier to)
			{
				if (c == _a)
				{
					seen.Add(from.ToString() + ">" + to);
				}
			};
			TownTier.Changed += onChange;
			try
			{
				Check("hall 0 is a village", TownTier.HallLevel(_a) == 0 && TownTier.Of(_a) == Tier.Village, hall.asset.id + ", level " + TownTier.HallLevel(_a));
				yield return CivilTest.Wait(1.5f);
				bool flag = false;
				try
				{
					flag = hall.canBeUpgraded() && hall.upgradeBuilding();
				}
				catch (Exception ex)
				{
					Note("upgrade: " + ex.Message);
				}
				if (!flag)
				{
					Note("the game's upgrade didn't take: " + hall.asset.id);
				}
				hall = HallOf(_a) ?? hall;
				Check("the game's upgrade makes hall 1 a town", TownTier.HallLevel(_a) == 1 && TownTier.Of(_a) == Tier.Town, hall.asset.id);
				yield return CivilTest.Wait(1.5f);
				try
				{
					flag = hall.canBeUpgraded() && hall.upgradeBuilding();
				}
				catch (Exception ex2)
				{
					Note("upgrade: " + ex2.Message);
					flag = false;
				}
				if (!flag)
				{
					Note("the game's second upgrade didn't take: " + hall.asset.id + (hall.isRuin() ? ", a ruin" : "") + (hall.isUnderConstruction() ? ", being built" : ""));
				}
				Check(detail: (HallOf(_a) ?? hall).asset.id, what: "...and hall 2 a city", ok: TownTier.HallLevel(_a) == 2 && TownTier.Of(_a) == Tier.City);
				yield return CivilTest.Wait(1.5f);
				Check("the change is announced as the town grows", seen.Contains("Village>Town") && seen.Contains("Town>City"), string.Join(", ", seen.ToArray()));
			}
			finally
			{
				TownTier.Changed -= onChange;
			}
			Check("the first town is its realm's capital", TownTier.IsCapital(_a));
			Kingdom kingdom = _a.kingdom;
			Check("a town that joined is no capital", _b != null && _b.kingdom == kingdom && !TownTier.IsCapital(_b), _b?.kingdom?.name);
		}

		private IEnumerator Reputation()
		{
			Kingdom realm = _a.kingdom;
			ClearStanding(_you);
			Check("a stranger at first", Rep.Points(_you, _a) == 0 && Rep.Level(_you, _a) == RepLevel.Stranger && Rep.StandingPoints(_you, realm) == 0);
			List<string> events = new List<string>();
			List<string> realmEvents = new List<string>();
			Action<Actor, City, RepLevel, RepLevel> value = delegate(Actor a, City c, RepLevel from, RepLevel to)
			{
				if (a == _you)
				{
					events.Add(from.ToString() + ">" + to);
				}
			};
			Action<Actor, Kingdom, RepLevel, RepLevel> value2 = delegate(Actor a, Kingdom k, RepLevel from, RepLevel to)
			{
				if (a == _you && k == realm)
				{
					realmEvents.Add(from.ToString() + ">" + to);
				}
			};
			Rep.LevelChanged += value;
			Rep.StandingChanged += value2;
			try
			{
				Rep.Add(_you, _a, 100, "test");
				Check("+100: still a stranger, no level event", Rep.Points(_you, _a) == 100 && Rep.Level(_you, _a) == RepLevel.Stranger && events.Count == 0);
				Check("...and the realm hears half", Rep.StandingPoints(_you, realm) == 50, Rep.StandingPoints(_you, realm).ToString());
				Rep.Add(_you, _a, 250, "test");
				Check("+250: trusted at 350, one level event", Rep.Points(_you, _a) == 350 && Rep.Level(_you, _a) == RepLevel.Trusted && events.Count == 1 && events[0] == "Stranger>Trusted", string.Join(", ", events.ToArray()));
				Check("the realm a step behind: a stranger at 175", Rep.StandingPoints(_you, realm) == 175 && Rep.Standing(_you, realm) == RepLevel.Stranger, Rep.StandingPoints(_you, realm).ToString());
				Check("trusted, not honored", Rep.AtLeast(_you, _a, RepLevel.Trusted) && !Rep.AtLeast(_you, _a, RepLevel.Honored));
				Check("the king has every right in his town", Rep.Points(_king, _a) == 0 && Rep.AtLeast(_king, _a, RepLevel.Champion));
				Rep.Add(_you, _a, 99999, "test");
				Check("clamped at the top", Rep.Points(_you, _a) == 5000 && Rep.Level(_you, _a) == RepLevel.Champion && Rep.StandingPoints(_you, realm) == 5000);
				Rep.DropTo(_you, _a, RepLevel.Trusted, "plot found out");
				Check("dropped to the start of a level", Rep.Points(_you, _a) == 300, Rep.Points(_you, _a).ToString());
				Check("...the town alone", Rep.StandingPoints(_you, realm) == 5000, Rep.StandingPoints(_you, realm).ToString());
				Rep.Add(_you, _a, -99999, "test");
				Check("clamped at the floor: an outlaw", Rep.Points(_you, _a) == -3000 && Rep.Level(_you, _a) == RepLevel.Outlaw, Rep.Points(_you, _a).ToString());
				Check("...and the realm loses all of it", Rep.StandingPoints(_you, realm) == -3000 && Rep.Standing(_you, realm) == RepLevel.Outlaw, Rep.StandingPoints(_you, realm).ToString());
				Check("level events on the way down", events.Contains("Trusted>Outlaw") && realmEvents.Contains("Champion>Outlaw"), string.Join(", ", events.ToArray()) + " / " + string.Join(", ", realmEvents.ToArray()));
				Check("below zero only the talk changes: a stranger's rights hold", Rep.AtLeast(_you, _a, RepLevel.Stranger) && !Rep.AtLeast(_you, _a, RepLevel.Trusted) && Rep.Gate(new Choice("Sell.", delegate
				{
				}), _you, _a, RepLevel.Stranger).Enabled);
				Rep.Add(_you, _a, 1600, "test", realm: false);
				Check("halfway back: distrusted", Rep.Points(_you, _a) == -1400 && Rep.Level(_you, _a) == RepLevel.Distrusted && Mathf.Abs(Rep.Progress(-1400) - 0.0667f) < 0.01f, Rep.Points(_you, _a) + ", bar " + Rep.Progress(-1400).ToString("0.000"));
				Check("a town alone leaves the realm as it was", Rep.StandingPoints(_you, realm) == -3000);
				Rep.Add(_you, _a, 1400, "test", realm: false);
				string pResult = null;
				try
				{
					_you.data.get("fp_rep", out pResult, null);
				}
				catch
				{
				}
				Check("back to nothing, and the entry gone", Rep.Points(_you, _a) == 0 && string.IsNullOrEmpty(pResult), pResult);
			}
			finally
			{
				Rep.LevelChanged -= value;
				Rep.StandingChanged -= value2;
			}
			ClearStanding(_you);
			Rep.Add(_you, _a, 500, "test");
			Actor actor = PersonOf(_b, null);
			Actor item = PersonOf(_b, actor);
			Actor actor2 = PersonOf(_c, null);
			WorldTile worldTile = null;
			try
			{
				worldTile = _a.getTile();
			}
			catch
			{
			}
			Vector2 at = ((worldTile != null) ? new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f) : Vector2.zero);
			Check("the middle of the first town is its land", worldTile != null && Hub.TownAt(at) == _a);
			int num = ((_b != null) ? Rep.Points(_you, _b) : 0);
			Hub.OnCrime(_you, at, realm, new List<Actor> { actor }, "theft");
			Check("a theft seen in a town costs that town 200", Rep.Points(_you, _a) == 300, Rep.Points(_you, _a).ToString());
			Check("...not the town of one who saw it", _b == null || Rep.Points(_you, _b) == num);
			Check("...and the realm all of it, once", Rep.StandingPoints(_you, realm) == 50, Rep.StandingPoints(_you, realm).ToString());
			Vector2 vector = OpenLandNear(_a);
			if (Check("open land near the towns", vector != Vector2.zero && Hub.TownAt(vector) == null) && actor != null && actor2 != null)
			{
				Hub.OnCrime(_you, vector, realm, new List<Actor> { actor, item, actor2 }, "assault");
				Check("on open land the home town of each who saw it pays, once each", Rep.Points(_you, _b) == num - 100 && Rep.Points(_you, _c) == -100 && Rep.Points(_you, _a) == 300, $"{Rep.Points(_you, _a)}, {Rep.Points(_you, _b)}, {Rep.Points(_you, _c)}");
				Check("...distrusted there", Rep.Level(_you, _c) == RepLevel.Distrusted);
				Check("...and their realm once, however many towns heard", Rep.StandingPoints(_you, realm) == -50 && Rep.Standing(_you, realm) == RepLevel.Distrusted, Rep.StandingPoints(_you, realm).ToString());
				Hub.OnCrime(_you, vector, realm, new List<Actor>(), "murder");
				Check("seen by no one of a town, the realm alone pays", Rep.StandingPoints(_you, realm) == -800 && Rep.Points(_you, _c) == -100, Rep.StandingPoints(_you, realm).ToString());
				Hub.OnFinePaid(_you, realm);
				Check("a fine paid lifts the realm and its towns back to Stranger", Rep.StandingPoints(_you, realm) == 0 && Rep.Points(_you, _b) == Mathf.Max(0, num - 100) && Rep.Points(_you, _c) == 0, $"{Rep.StandingPoints(_you, realm)}, {Rep.Points(_you, _b)}, {Rep.Points(_you, _c)}");
				Check("...and leaves good standing as it was", Rep.Points(_you, _a) == 300);
			}
			else
			{
				Note($"(no open land or witnesses from the other towns: b {actor != null}, c {actor2 != null})");
			}
			ClearStanding(_you);
			Hub.OnQuestDone(_you, _a, 6);
			Check("a task done gives 75..200 by its renown", Rep.Points(_you, _a) == 465 && Rep.StandingPoints(_you, realm) == 82, Rep.Points(_you, _a) + ", realm " + Rep.StandingPoints(_you, realm));
			Hub.OnQuestDone(_you, _a, 30, bounty: true);
			Check("a bounty gives at most 300", Rep.Points(_you, _a) == 765, Rep.Points(_you, _a).ToString());
			try
			{
				_you.data.get("fp_rep", out var pResult2, "");
				_you.data.set("fp_rep", pResult2 + "|987654321:777");
			}
			catch
			{
			}
			int num2 = Rep.Points(_you, _a);
			string pResult3 = null;
			try
			{
				_you.data.get("fp_rep", out pResult3, null);
			}
			catch
			{
			}
			Check("a town that's gone is dropped", num2 == 765 && pResult3 != null && !pResult3.Contains("987654321"), pResult3);
			try
			{
				_you.data.set("fp_rep", pResult3 + "|" + _a.data.id + ":20");
			}
			catch
			{
			}
			string pResult4 = null;
			int num3 = Rep.Points(_you, _a);
			try
			{
				_you.data.get("fp_rep", out pResult4, null);
			}
			catch
			{
			}
			Check("a town written twice is merged, the higher kept", num3 == 765 && pResult4 == pResult3, pResult4);
			try
			{
				_you.data.get("fp_realm", out var pResult5, "");
				_you.data.set("fp_realm", pResult5 + "|987654321:-900");
			}
			catch
			{
			}
			Check("so is a realm that's gone", Rep.Realms(_you).Count == 1 && Rep.Realms(_you)[0].Key == realm, Rep.Realms(_you).Count.ToString());
			ClearStanding(_you);
			Rep.Add(_you, _a, 2000 - Rep.Points(_you, _a), "test", realm: false);
			if (_b != null && Rep.RealmOf(_b) == realm)
			{
				Rep.Add(_you, _b, 1000, "test");
			}
			if (_c != null && Rep.RealmOf(_c) == realm)
			{
				Rep.Add(_you, _c, 400, "test");
			}
			int num4 = ((_b != null && Rep.RealmOf(_b) == realm) ? 500 : 0) + ((_c != null && Rep.RealmOf(_c) == realm) ? 200 : 0);
			Check("a realm's meter is half of what its towns gave", Rep.StandingPoints(_you, realm) == num4 && Rep.Standing(_you, realm) == Rep.LevelOf(num4), Rep.StandingPoints(_you, realm) + " vs " + num4);
			Check("the king stands highest in his realm", Rep.Standing(_king, realm) == RepLevel.Champion);
			Rep.AddStanding(_you, realm, -1800 - Rep.StandingPoints(_you, realm), "test");
			Check("honored in the town, an outlaw in its realm", Rep.Level(_you, _a) == RepLevel.Honored && Rep.Standing(_you, realm) == RepLevel.Outlaw);
			yield return null;
			RepHud.Reset();
			Mod.Toast("Seen by 3");
			Rep.AddStanding(_you, realm, 1000, "test");
			Rep.AddStanding(_you, realm, 1000, "test");
			Check("a new level waits for the line showing now", Mod.ToastText == "Seen by 3" && RepHud.WaitingForTest == 1, Mod.ToastText + ", " + RepHud.WaitingForTest + " waiting");
			float waited = 0f;
			while (Mod.ToastText == "Seen by 3" && waited < 6f)
			{
				yield return CivilTest.Wait(0.2f);
				waited += 0.2f;
			}
			Check("...then shows, the latest word of that realm only", Mod.ToastText == "No longer distrusted in the realm of " + realm.name + "." && RepHud.WaitingForTest == 0, Mod.ToastText + ", " + RepHud.WaitingForTest + " waiting");
			Rep.AddStanding(_you, realm, -2000, "test");
			Rep.AddStanding(_you, realm, -1000, "test");
			yield return null;
		}

		private Actor PersonOf(City c, Actor skip)
		{
			if (c == null)
			{
				return null;
			}
			Actor actor = null;
			try
			{
				foreach (Actor item in new List<Actor>(c.units))
				{
					if (item != null && item != skip && item != _you && item.isAlive())
					{
						if (item.isAdult())
						{
							return item;
						}
						if (actor == null)
						{
							actor = item;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Note("people of " + c.name + ": " + ex.Message);
			}
			return actor;
		}

		private static Vector2 OpenLandNear(City c)
		{
			try
			{
				WorldTile tile = c.getTile();
				for (int i = 8; i < 80; i += 2)
				{
					for (int j = 0; j < 24; j++)
					{
						float f = (float)j * MathF.PI / 12f;
						WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * (float)i), tile.y + (int)(Mathf.Sin(f) * (float)i));
						if (tile2 != null && tile2.Type != null && tile2.Type.ground && (tile2.zone == null || tile2.zone.city == null))
						{
							return new Vector2((float)tile2.x + 0.5f, (float)tile2.y + 0.5f);
						}
					}
				}
			}
			catch
			{
			}
			return Vector2.zero;
		}

		private static void ClearStanding(Actor body)
		{
			try
			{
				body.data.removeString("fp_realm");
			}
			catch
			{
			}
		}

		private IEnumerator Gating()
		{
			Choice choice = Rep.Gate(new Choice("Sit with the council.", delegate
			{
			}), _you, _a, RepLevel.Champion);
			Check("a choice above your level is greyed", !choice.Enabled && choice.Note == "Champion in " + _a.name, choice.Note);
			Choice choice2 = Rep.Gate(new Choice("Buy a house.", delegate
			{
			}), _you, _a, RepLevel.Trusted);
			Check("one at your level is open", choice2.Enabled && choice2.Note == null);
			Choice choice3 = Rep.GateStanding(new Choice("An audience with the king.", delegate
			{
			}), _you, _a.kingdom, RepLevel.Champion);
			Check("a realm's standing greys one too", !choice3.Enabled && choice3.Note.StartsWith("Champion in ", StringComparison.Ordinal), choice3.Note);
			Actor npc = null;
			foreach (Actor person in _people)
			{
				try
				{
					if (person != _you && person.isAlive() && person.lover != _you && (person.family == null || person.family != _you.family))
					{
						npc = person;
						break;
					}
				}
				catch
				{
				}
			}
			if (npc == null)
			{
				npc = Alive(1);
			}
			if (Check("someone to talk to", npc?.isAlive() ?? false))
			{
				CivilTest.StandBefore(npc, _you, 1.2f);
				yield return CivilTest.Wait(0.3f);
				int calls = Talk.Calls;
				bool started = Mod.StartConversationForTest(npc);
				yield return CivilTest.Wait(0.3f);
				Check("a talk opens", started && Mod.Converse.Open);
				List<Choice> list = Dialogue.Topics(Mod.Converse);
				Check("the Towns topics are asked for in every talk", Talk.Calls > calls, (Talk.Calls - calls).ToString());
				Check("...and the topics still end with goodbye", list.Count > 0 && list[list.Count - 1].Label == "Goodbye.");
				bool flag = false;
				try
				{
					flag = npc.lover == _you || (npc.family != null && npc.family == _you.family);
				}
				catch
				{
				}
				if (flag)
				{
					Note("(only kin to talk to: no greeting checks)");
				}
				else if (npc.city != _a)
				{
					Note("(the one to talk to isn't of the first town: no greeting checks)");
				}
				else
				{
					string text = Talk.Greeting(Mod.Converse);
					Check("honored here, an outlaw in the realm: they'd hide you", text != null && (text.Contains("crown") || text.Contains("one of us")), text);
					Rep.Add(_you, _a, -100 - Rep.Points(_you, _a), "test", realm: false);
					text = Talk.Greeting(Mod.Converse);
					Check("distrusted here: a curt word", text != null && (text.Contains("short") || text.Contains("move on") || text.Contains("smile")), text);
					Rep.Add(_you, _a, -2000 - Rep.Points(_you, _a), "test", realm: false);
					text = Talk.Greeting(Mod.Converse);
					Check("an outlaw here: a cold one", text != null && (text.Contains("nerve") || text.Contains("whole town") || text.Contains("watching")), text);
					Check("...but the talk's topics are all still there", Dialogue.Topics(Mod.Converse).Count == list.Count, Dialogue.Topics(Mod.Converse).Count + " vs " + list.Count);
					Rep.Add(_you, _a, 2000 - Rep.Points(_you, _a), "test", realm: false);
					int num = Rep.StandingPoints(_you, _a.kingdom);
					Rep.AddStanding(_you, _a.kingdom, -num, "test");
					string text2 = Talk.Greeting(Mod.Converse);
					Check("nothing held against you: no word of it", text2 == null, text2);
					Rep.AddStanding(_you, _a.kingdom, num, "test");
				}
				yield return null;
				Check("the Towns keys are held while you talk", !Mod.TownsKeysFreeForTest);
				Mod.EndConversation();
				yield return CivilTest.Wait(0.3f);
			}
		}

		private IEnumerator KeysAndWindows()
		{
			yield return CivilTest.Wait(0.2f);
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			Check("the Towns keys are free with nothing open", Mod.TownsKeysFreeForTest);
			Planning.Open = true;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Check("a Towns window counts as a menu: no swings", PossessionHooks.BlockAttack);
			Check("...and the cursor is free", Cursor.lockState == CursorLockMode.None);
			Check("...and the game's own keys are muted", Mod.GameKeysMuted);
			Planning.Open = false;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Check("closed, the keys are free again", Mod.TownsKeysFreeForTest && !Mod.GameKeysMuted);
			Check("Esc has no Towns window to close", !Hub.Escape());
			Planning.Aiming = true;
			Planning.Prompt = "LMB:Place|Y:Done";
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Check("an aiming mode keeps the view: no menu", Cursor.lockState != CursorLockMode.None && !Mod.GameKeysMuted);
			Check("...takes the clicks", PossessionHooks.BlockAttack);
			Check("...holds talk back", Mod.Talk.Blocked);
			Check("...and shows its own prompt", Mod.Prompt == "LMB:Place|Y:Done", Mod.Prompt);
			Planning.Aiming = false;
			Planning.Prompt = null;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Check("done, the prompt is the world's again", Mod.Prompt != "LMB:Place|Y:Done");
			StrikeTiming strikeTiming = StrikeTiming.For(WeaponKind.Sword, melee: true, hands: true);
			StrikeTiming strikeTiming2 = strikeTiming.WithWindup(0.5f);
			Check("a faster wind-up lands sooner and frees the next blow sooner", strikeTiming2.Hit < strikeTiming.Hit && strikeTiming2.Total < strikeTiming.Total && strikeTiming2.Total >= strikeTiming2.StrikeEnd && Mathf.Abs(strikeTiming.WithWindup(1f).Hit - strikeTiming.Hit) < 1E-05f && Mathf.Abs(strikeTiming.WithWindup(0f).Total - strikeTiming.Total) < 1E-05f, $"hit {strikeTiming.Hit:0.00} -> {strikeTiming2.Hit:0.00}, pace {strikeTiming.Total:0.00} -> {strikeTiming2.Total:0.00}");
		}

		private IEnumerator ItemsHomesShops()
		{
			Items.Give(_you, "tw_item_bandage", 3);
			Items.Give(_you, "tw_tool_pickaxe");
			Check("items given", Items.Count(_you, "tw_item_bandage") == 3 && Items.Count(_you, "tw_tool_pickaxe") == 1);
			Check("taking two of three", Items.Take(_you, "tw_item_bandage", 2) && Items.Count(_you, "tw_item_bandage") == 1);
			Check("taking more than there is takes nothing", !Items.Take(_you, "tw_item_bandage", 5) && Items.Count(_you, "tw_item_bandage") == 1);
			string[] obj = new string[4] { "house_human_1", "house_human_3", "house_human_2", "house_human_0" };
			int num = 1;
			string[] array = obj;
			foreach (string id in array)
			{
				Building building = AddBuilding(id, _a, _houses.Count + 1);
				if (building == null)
				{
					continue;
				}
				_houses.Add(building);
				int num2 = 0;
				while (num2 < 2 && num < _people.Count)
				{
					try
					{
						if (_people[num] != null && _people[num].isAlive())
						{
							_people[num].setHomeBuilding(building);
						}
					}
					catch (Exception ex)
					{
						Note("home: " + ex);
					}
					num2++;
					num++;
				}
			}
			yield return CivilTest.Wait(0.5f);
			num = 1;
			foreach (Building house in _houses)
			{
				int num3 = 0;
				while (num3 < 2 && num < _people.Count)
				{
					try
					{
						if (_people[num].getHomeBuilding() != house)
						{
							_people[num].setHomeBuilding(house);
						}
					}
					catch
					{
					}
					num3++;
					num++;
				}
			}
			yield return null;
			int num4 = 0;
			foreach (Building house2 in _houses)
			{
				try
				{
					if (house2.hasResidents())
					{
						num4++;
					}
				}
				catch
				{
				}
			}
			Note($"{_houses.Count} houses placed, {num4} lived in; the town is a {TownTier.Word(TownTier.Of(_a))}");
			if (!Check("houses with people in them", num4 >= 2, num4.ToString()))
			{
				yield break;
			}
			Shops.Assign(_a);
			Dictionary<ShopKind, Building> kinds = new Dictionary<ShopKind, Building>();
			foreach (Building item in Shops.In(_a))
			{
				kinds[Shops.Of(item)] = item;
			}
			Note("shops: " + string.Join(", ", new List<ShopKind>(kinds.Keys).ConvertAll((ShopKind k) => k.ToString() + " " + kinds[k].asset.id).ToArray()));
			Check("a smithy is picked", kinds.ContainsKey(ShopKind.Smithy));
			Check("...in the grandest house lived in", kinds.TryGetValue(ShopKind.Smithy, out var smithy) && smithy.asset.id.EndsWith(BestLivedTier()), smithy?.asset.id);
			Check("one house, one kind", kinds.Count == new HashSet<Building>(kinds.Values).Count);
			Check("the pick stays put", Shops.Assign(_a) == 0);
			Actor actor = Shops.Keeper(smithy);
			Check("its keeper lives there", actor != null && smithy.residents.Contains(actor.data.id), (actor != null) ? WorldBoxMod.Describe(actor) : "none");
			Building b = _houses.Find((Building h) => h != smithy && Shops.Of(h) == ShopKind.None) ?? _houses.Find((Building h) => h != smithy);
			Shops.Set(b, ShopKind.Smithy);
			Shops.Assign(_a);
			Check("a shop made on purpose stays", Shops.Of(b) == ShopKind.Smithy && Shops.Chosen(b));
			Homes.SetOwner(smithy, _you);
			Check("a house owned", Homes.OwnedBy(smithy, _you) && Homes.OwnerId(smithy) == _you.data.id);
			Check("...is among the body's houses", new List<Building>(Homes.Owned(_you)).Contains(smithy));
			Shops.Assign(_a);
			Check("your house is no shop: the smithy moves", Shops.Of(smithy) == ShopKind.None, Shops.Of(smithy).ToString());
			Homes.SetOwner(smithy, null);
			Check("the owner taken off", Homes.OwnerId(smithy) == 0);
			TownTier.ForcedLevel[_a.data.id] = 0;
			Shops.Assign(_a);
			bool flag = true;
			foreach (Building item2 in Shops.In(_a))
			{
				ShopKind shopKind = Shops.Of(item2);
				if (!Shops.Chosen(item2) && shopKind != ShopKind.Smithy)
				{
					flag = false;
				}
			}
			Check("a village keeps only a village's shops (and those made on purpose)", flag && Shops.Of(b) == ShopKind.Smithy);
			TownTier.ForcedLevel.Remove(_a.data.id);
			Shops.Assign(_a);
			Homes.SetOwner(smithy, _you);
			yield return null;
		}

		private string BestLivedTier()
		{
			int num = -1;
			List<Building> list = new List<Building>();
			try
			{
				list.AddRange(_a.buildings);
			}
			catch
			{
			}
			foreach (Building item in list)
			{
				try
				{
					if (item != null && item.isAlive() && !(item.asset.type != "type_house") && !item.asset.id.StartsWith("tent", StringComparison.Ordinal) && item.hasResidents() && Homes.OwnerId(item) <= 0 && !item.isUnderConstruction() && !item.isRuin())
					{
						string id = item.asset.id;
						int num2 = id[id.Length - 1] - 48;
						if (num2 > num)
						{
							num = num2;
						}
					}
				}
				catch
				{
				}
			}
			return num.ToString();
		}

		private IEnumerator RoomsPart()
		{
			Building house = _houses.Find((Building h) => HouseInterior.IsEnterable(h));
			if (Check("a house to walk into", house != null))
			{
				int asked = Rooms.Asked;
				Mod.EnterHouseForTest(house);
				for (int i = 0; i < 4; i++)
				{
					yield return null;
				}
				yield return CivilTest.Wait(0.3f);
				Check("the Towns rooms are asked for a room", Rooms.Asked > asked);
				Check("...and with none of theirs, today's room opens", Mod.Interior.Inside && Mod.Interior.House == house && Mod.Interior.Name == "house", Mod.Interior.Name);
				Check("inside, Y is for decorating", Hub.InRoom);
				Mod.LeaveRoomForTest();
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				Check("out again, Y is for planning", !Mod.Interior.Inside && !Hub.InRoom);
			}
		}

		private IEnumerator HeirsAndCalendar()
		{
			Actor actor = Alive(2, Alive(1));
			if (!Check("an heir to be", actor?.isAlive() ?? false))
			{
				yield break;
			}
			int num = Rep.Points(_you, _a);
			int num2 = ((_b != null) ? Rep.Points(_you, _b) : 0);
			Rep.Add(actor, _a, num + 500, "test");
			try
			{
				_you.data.set("fp_skills", "sword:50|bow:10");
				actor.data.set("fp_skills", "sword:20|axe:5");
				_you.data.set("fp_tally", ((_a.kingdom != null) ? _a.kingdom.data.id : 1) + ":3");
			}
			catch (Exception ex)
			{
				Note("skills: " + ex.Message);
			}
			Kingdom kingdom = _a.kingdom;
			int a = Rep.StandingPoints(actor, kingdom);
			Rep.AddStanding(_you, kingdom, 4000 - Rep.StandingPoints(_you, kingdom), "test");
			if (_c != null)
			{
				Rep.Add(_you, _c, -500 - Rep.Points(_you, _c), "test", realm: false);
			}
			int num3 = ((_c != null) ? Rep.Points(actor, _c) : 0);
			Hub.OnBodyDied(_you, actor);
			Check("half your standing in a realm passes, where that's higher than theirs", Rep.StandingPoints(actor, kingdom) == Mathf.Max(a, 2000), Rep.StandingPoints(actor, kingdom) + " (theirs " + a + ")");
			Check("ill fame in a town doesn't pass", _c == null || Rep.Points(actor, _c) == num3, (_c != null) ? (Rep.Points(actor, _c) + " vs " + num3) : "");
			Check("the heir keeps their own higher standing", Rep.Points(actor, _a) == num + 500, Rep.Points(actor, _a).ToString());
			Check("...and half of yours where that's higher", _b == null || Rep.Points(actor, _b) == num2 / 2, (_b != null) ? (Rep.Points(actor, _b) + " vs " + num2 / 2) : "");
			string pResult = null;
			string pResult2 = null;
			try
			{
				actor.data.get("fp_skills", out pResult, null);
				actor.data.get("fp_tally", out pResult2, null);
			}
			catch
			{
			}
			Check("skills: their own or half of yours, the higher", pResult != null && pResult.Contains("sword:25") && pResult.Contains("axe:5") && pResult.Contains("bow:5"), pResult);
			Check("half the realm's tally of tasks passes, rounded down", pResult2?.EndsWith(":1") ?? false, pResult2);
			Check("you keep yours (a leap copies nothing; only the heir screen does this)", Rep.Points(_you, _a) == num);
			Actor actor2 = Alive(3, actor);
			Actor actor3 = null;
			foreach (Actor person in _people)
			{
				try
				{
					if (person != null && person != _you && person != actor && person != actor2 && person.isAlive())
					{
						actor3 = person;
						break;
					}
				}
				catch
				{
				}
			}
			if (actor2 != null && actor3 != null && actor2 != actor3)
			{
				Rep.Add(actor2, _a, 800, "test");
				Rep.AddStanding(actor2, kingdom, -1000, "test");
				int num4 = Rep.StandingPoints(actor3, kingdom);
				try
				{
					actor2.data.set("fp_skills", "axe:40");
				}
				catch
				{
				}
				Hub.OnBodyDying(actor2);
				try
				{
					actor2.data.removeString("fp_rep");
					actor2.data.removeString("fp_realm");
					actor2.data.removeString("fp_skills");
				}
				catch
				{
				}
				Hub.OnBodyDied(actor2, actor3);
				string pResult3 = null;
				try
				{
					actor3.data.get("fp_skills", out pResult3, null);
				}
				catch
				{
				}
				Check("a body gone by the heir screen still passes half on", Rep.Points(actor3, _a) == 400 && pResult3 == "axe:20", Rep.Points(actor3, _a) + ", " + pResult3);
				Check("...but not its ill fame in the realm", Rep.StandingPoints(actor3, kingdom) == num4, Rep.StandingPoints(actor3, kingdom) + " vs " + num4);
			}
			else
			{
				Note("(not enough villagers left for the gone-body check)");
			}
			List<int> dawns = new List<int>();
			Action<int> value = delegate(int d)
			{
				dawns.Add(d);
			};
			Calendar.Dawn += value;
			try
			{
				float num5 = 0.25f;
				Calendar.SetDayForTest(5);
				Check("the day is set and read back", Calendar.Day == 5 && Calendar.Season >= Season.Spring && Calendar.Season < (Season)4, Calendar.Season.ToString());
				Calendar.Tick(num5 - 0.05f);
				Calendar.Tick(num5 + 0.05f);
				Check("a dawn crossed counts a day", Calendar.Day == 6 && dawns.Count == 1 && dawns[0] == 6, string.Join(",", dawns.ConvertAll((int d) => d.ToString()).ToArray()));
				Calendar.Tick(0.95f);
				Calendar.Tick(Mathf.Max(0.01f, num5 - 0.1f));
				Check("midnight is no dawn", Calendar.Day == 6 && dawns.Count == 1);
				Calendar.Tick(num5 + 0.01f);
				Check("...the next dawn is", Calendar.Day == 7 && dawns.Count == 2);
				int num6 = Calendar.SkipToDawn();
				Check("sleeping jumps to the next dawn", num6 == 8 && Calendar.Day == 8 && dawns.Count == 3, Calendar.TimeOfDay.ToString("0.000"));
			}
			finally
			{
				Calendar.Dawn -= value;
			}
			_dayBefore = Calendar.Day;
			yield return null;
		}

		private IEnumerator Pictures()
		{
			Rep.AddStanding(_you, _a.kingdom, -600 - Rep.StandingPoints(_you, _a.kingdom), "test");
			Rep.Add(_you, _a, 120, "raid repelled");
			yield return CivilTest.Wait(1.2f);
			Check("a change brings the banner up", Mod.TownShown == _a && Mod.TownUntil > Time.unscaledTime);
			yield return Shot("towns_banner.png");
			Journal.Open = true;
			Journal.Tab = 2;
			yield return Shot("towns_standing.png");
			Journal.Open = false;
			Journal.Tab = 0;
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

		private IEnumerator Stubs()
		{
			StubTest.NoQuit = true;
			StubTest.Ran.Clear();
			GameObject holder = new GameObject("TownsStubTests");
			int added = 0;
			Type[] stubTypes = StubTypes;
			foreach (Type type in stubTypes)
			{
				if (!typeof(StubTest).IsAssignableFrom(type))
				{
					Note(type.Name + " is a package's own test now");
					continue;
				}
				holder.AddComponent(type);
				added++;
			}
			for (float t = 0f; t < 10f; t += 0.25f)
			{
				if (StubTest.Ran.Count >= added)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check($"every stub test runs and says so ({added})", StubTest.Ran.Count == added, string.Join(" ", StubTest.Ran.ToArray()));
			UnityEngine.Object.Destroy(holder);
			StubTest.NoQuit = false;
		}

		private IEnumerator SaveAndLoad()
		{
			long you = _you.data.id;
			long city = _a.data.id;
			Building building = _houses.Find((Building h) => Homes.OwnedBy(h, _you));
			Building building2 = _houses.Find((Building h) => Shops.Chosen(h));
			long houseId = building?.data.id ?? (-1);
			long shopId = building2?.data.id ?? (-1);
			int points = Rep.Points(_you, _a);
			long realmId = Rep.IdOf(_a.kingdom);
			int standing = Rep.StandingPoints(_you, _a.kingdom);
			string pResult = null;
			string pResult2 = null;
			try
			{
				_you.data.get("fp_rep", out pResult, null);
				_you.data.get("fp_items", out pResult2, null);
			}
			catch
			{
			}
			Note($"before: rep \"{pResult}\", items \"{pResult2}\", owned house {houseId}, chosen shop {shopId}");
			try
			{
				HotkeyLibrary.control_unit.just_pressed_action(HotkeyLibrary.control_unit);
			}
			catch (Exception ex)
			{
				Note("G: " + ex.Message);
			}
			yield return CivilTest.Wait(1f);
			Check("G lets go of the creature", !ControllableUnit.isControllingUnit() && !Mod.IsFirstPerson);
			try
			{
				if (ControllableUnit.isControllingUnit())
				{
					ControllableUnit.clear(pCallKill: false);
				}
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			_dayBefore = Calendar.Day;
			byte[] pMapData;
			try
			{
				SavedMap savedMap = SaveManager.currentWorldToSavedMap();
				string text = savedMap.toJson();
				Check("the save holds the Towns data", text.Contains("fp_rep") && text.Contains("fp_realm") && text.Contains("fp_items") && text.Contains("fp_owner") && text.Contains("fp_shop"), text.Length + " chars");
				pMapData = savedMap.toZip();
			}
			catch (Exception ex2)
			{
				Check("the world saved to bytes", ok: false, ex2.Message);
				yield break;
			}
			try
			{
				SaveManager.loadMapFromBytes(pMapData);
			}
			catch (Exception ex3)
			{
				Check("the world loaded from bytes", ok: false, ex3.Message);
				yield break;
			}
			for (float t = 0f; t < 60f; t += 0.5f)
			{
				yield return CivilTest.Wait(0.5f);
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t > 2f)
				{
					break;
				}
			}
			yield return CivilTest.Wait(1f);
			Actor actor = null;
			City city2 = Quests.CityById(city);
			try
			{
				actor = World.world.units.get(you);
			}
			catch
			{
			}
			if (!Check("you are back after the reload", actor?.isAlive() ?? false))
			{
				yield break;
			}
			Check("your standing in the town survived", city2 != null && Rep.Points(actor, city2) == points, (city2 != null) ? (Rep.Points(actor, city2) + " vs " + points) : "no town");
			Kingdom kingdom = Rep.KingdomById(realmId);
			Check("...and in its realm, below zero too", kingdom != null && standing != 0 && Rep.StandingPoints(actor, kingdom) == standing, (kingdom != null) ? (Rep.StandingPoints(actor, kingdom) + " vs " + standing) : "no realm");
			Check("your items survived", Items.Count(actor, "tw_item_bandage") == 1 && Items.Count(actor, "tw_tool_pickaxe") == 1);
			Building building3 = null;
			Building building4 = null;
			try
			{
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple != null)
					{
						if (simple.data.id == houseId)
						{
							building3 = simple;
						}
						if (simple.data.id == shopId)
						{
							building4 = simple;
						}
					}
				}
			}
			catch
			{
			}
			Check("the house's owner survived", building3 != null && Homes.OwnedBy(building3, actor), (building3 == null) ? ("house " + houseId + " not found") : ("owner " + Homes.OwnerId(building3) + " vs " + you + ", " + building3.asset.id));
			Check("the chosen shop survived", building4 != null && Shops.Of(building4) == ShopKind.Smithy && Shops.Chosen(building4), (building4 == null) ? ("shop " + shopId + " not found") : (Shops.Of(building4).ToString() + (Shops.Chosen(building4) ? " chosen" : "") + ", " + building4.asset.id));
			Calendar.Tick(Calendar.TimeOfDay);
			Check("the date survived", _dayBefore > 0 && Calendar.Day == _dayBefore, Calendar.Day + " vs " + _dayBefore);
		}

		private Actor Alive(int from, Actor skip = null)
		{
			for (int i = from; i < _people.Count; i++)
			{
				Actor actor = _people[i];
				try
				{
					if (actor != null && actor != _you && actor != skip && actor.isAlive())
					{
						return actor;
					}
				}
				catch
				{
				}
			}
			return null;
		}

		private void ClaimLand(City c)
		{
			int num = 0;
			try
			{
				TileZone tileZone = c.getTile()?.zone;
				if (tileZone == null)
				{
					return;
				}
				HashSet<TileZone> hashSet = new HashSet<TileZone> { tileZone };
				for (int i = 0; i < 2; i++)
				{
					foreach (TileZone item in new List<TileZone>(hashSet))
					{
						TileZone[] neighbours_all = item.neighbours_all;
						foreach (TileZone tileZone2 in neighbours_all)
						{
							if (tileZone2 != null)
							{
								hashSet.Add(tileZone2);
							}
						}
					}
				}
				foreach (TileZone item2 in hashSet)
				{
					if (item2.city == null)
					{
						c.addZone(item2);
						num++;
					}
				}
			}
			catch (Exception ex)
			{
				Note("land: " + ex.Message);
			}
			Note($"{num} zones of land claimed for {c.name}");
		}

		private Building AddHouse(string id)
		{
			return AddBuilding(id, _a, 6);
		}

		private Building AddBuilding(string id, City city, int ring)
		{
			for (int i = 0; i < 2; i++)
			{
				try
				{
					WorldTile tile = city.getTile();
					if (tile == null)
					{
						return null;
					}
					for (int j = 0; j < 200; j++)
					{
						float f = (float)ring * 2.4f + (float)j * 0.61f;
						float num = 5f + (float)ring * 2.5f + (float)j * 0.15f;
						WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * num), tile.y + (int)(Mathf.Sin(f) * num));
						if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || (tile2.building != null && AssetStyle.IsSolid(tile2.building)) || (i == 0 && (tile2.zone == null || tile2.zone.city != city)))
						{
							continue;
						}
						Building building = World.world.buildings.addBuilding(id, tile2, pCheckForBuild: true, pSfx: false, BuildPlacingType.New);
						if (building != null)
						{
							try
							{
								building.completeConstruction();
								building.setKingdom(city.kingdom);
							}
							catch
							{
							}
							if (building.city != city)
							{
								city.listBuilding(building);
							}
							if (i == 1)
							{
								Note(id + " placed outside the town's land: the game may abandon it");
							}
							return building;
						}
					}
				}
				catch (Exception ex)
				{
					Note("place " + id + ": " + ex.Message);
				}
			}
			return null;
		}

		private IEnumerator LookAt(Building b, float back)
		{
			WorldTile current_tile = b.current_tile;
			Mod.DebugEye = new Vector2((float)current_tile.x + 0.5f, (float)current_tile.y + 0.5f - back);
			Mod.DebugEyeSet = true;
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-6f);
			yield return CivilTest.Wait(1f);
		}
	}
}
