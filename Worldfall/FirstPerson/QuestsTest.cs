using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class QuestsTest : CivilTest
	{
		private Actor _host;

		private Actor _kingA;

		private Actor _kingB;

		private Actor _single;

		private Actor _single2;

		private City _a;

		private City _b;

		private readonly List<Actor> _peopleA = new List<Actor>();

		private readonly List<Actor> _peopleB = new List<Actor>();

		private int _used;

		private bool _talked;

		private Quest _q;

		protected override string Prefix => "QUESTS";

		protected override IEnumerator Body()
		{
			_kingA = Village("human", 18, 2);
			if (!Check("a first village", _kingA != null))
			{
				yield break;
			}
			_a = _kingA.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _kingA && item.isAdult())
				{
					_peopleA.Add(item);
				}
			}
			WorldTile tile = _a.getTile();
			_kingB = Village("human", 10, 2, tile, 28f);
			if (!Check("a second village a little way off", _kingB != null))
			{
				yield break;
			}
			_b = _kingB.city;
			foreach (Actor item2 in Founded)
			{
				if (item2 != null && item2.isAlive() && item2 != _kingB && item2.isAdult())
				{
					_peopleB.Add(item2);
				}
			}
			WorldTile tile2 = _b.getTile();
			Note($"villages {_a.name} ({_peopleA.Count} adults) and {_b.name} ({_peopleB.Count}), {(new Vector2(tile.x, tile.y) - new Vector2(tile2.x, tile2.y)).magnitude:F0} tiles apart");
			_host = _peopleA[_used++];
			_single = CivilTest.SpawnNear("human", _kingA.current_position, 2f);
			_single2 = CivilTest.SpawnNear("human", _kingA.current_position, 3f);
			CivilTest.SetSex(_single, female: true);
			CivilTest.SetSex(_single2, female: false);
			Actor[] array = new Actor[2] { _single, _single2 };
			foreach (Actor actor in array)
			{
				if (actor != null)
				{
					try
					{
						actor.joinCity(_a);
					}
					catch
					{
					}
				}
			}
			try
			{
				_host.addMoney(10);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				Quests.MatchChanceForTest = 1f;
				yield return Part("supply", Supply());
				yield return Part("hunt", Hunt());
				yield return Part("family errand", Errand());
				yield return Part("craft", Craft());
				yield return Part("matchmaker", Match());
				yield return Part("escort", Escort());
				yield return Part("faith", Faith());
				yield return Part("pilgrimage", Pilgrimage());
				yield return Part("investigation", Investigation());
				yield return Part("the log", TheLog());
				yield return Part("the daily board", Daily());
				Quests.MatchChanceForTest = -1f;
				Note($"done {Quests.Completed}, failed {Quests.Failed}");
			}
		}

		private Actor NextA()
		{
			while (_used < _peopleA.Count)
			{
				Actor actor = _peopleA[_used++];
				if (actor != null && actor.isAlive() && actor != _host && !actor.isKing() && !actor.isCityLeader())
				{
					return actor;
				}
			}
			return null;
		}

		private void Near(Actor who, float d = 1.4f)
		{
			_host.current_position = who.current_position - new Vector2(d, 0f);
			Mod.SetYaw(0f);
			try
			{
				who.makeWait(8f);
			}
			catch
			{
			}
		}

		private static Choice Find(Conversation c, string start)
		{
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private IEnumerator Talk(Actor npc, params string[] says)
		{
			Conversation c = Mod.Converse;
			Near(npc);
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(npc);
			_talked = c.Open;
			foreach (string say in says)
			{
				for (int j = 0; j < 6; j++)
				{
					if (c.Choices.Count <= 0)
					{
						break;
					}
					if (!(c.Choices[0].Label == "Go on..."))
					{
						break;
					}
					c.Next();
					c.Next();
				}
				if (c.Open)
				{
					_host.current_position = npc.current_position - new Vector2(1.4f, 0f);
					try
					{
						npc.makeWait(8f);
					}
					catch
					{
					}
				}
				Choice choice = Find(c, say);
				if (choice == null)
				{
					Note("(no \"" + say + "\" with " + Dialogue.NameOf(npc) + ": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
					_talked = false;
					break;
				}
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
				yield return CivilTest.Wait(0.2f);
				Note("  " + say + " -> \"" + c.Line + "\"");
			}
			Mod.EndConversation();
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator Offered(Actor giver, QuestKind kind, Action<Quest> got)
		{
			Quest quest = Quests.MakeForTest(kind, giver, _host);
			Check($"{Dialogue.NameOf(giver)} has a {kind} quest to offer", quest != null && (quest.Kind == kind || kind == QuestKind.Faith), (quest != null) ? (Quests.Title(quest) + " / " + Quests.Status(quest)) : null);
			got(quest);
			yield break;
		}

		private IEnumerator Accept(Actor giver, QuestKind kind)
		{
			int before = Quests.Log.Count;
			Quests.ForceKindForTest = (int)kind;
			if (Quests.Quick(kind))
			{
				yield return Talk(giver, "Any work for me?", "I'll do it.");
			}
			else
			{
				yield return Talk(giver, "Any work for me?", "I'll do it.", "Yes, that's right.");
			}
			Quests.ForceKindForTest = -1;
			Quest quest = Quests.Log.Find((Quest x) => x.Giver == giver.data.id);
			Check($"took on {kind} from {Dialogue.NameOf(giver)} in talk", _talked && quest != null && quest.Kind == kind && Quests.Log.Count == before + 1, (quest != null) ? (quest.Kind.ToString() + ": " + Quests.Title(quest)) : "none");
			_q = quest;
		}

		private IEnumerator Report(Actor giver, Quest q, params string[] extra)
		{
			int money = CivilTest.Money(_host);
			int renown = 0;
			try
			{
				renown = _host.renown;
			}
			catch
			{
			}
			List<string> list = new List<string> { "About what you asked of me..." };
			list.AddRange(extra);
			yield return Talk(giver, list.ToArray());
			int num = CivilTest.Money(_host) - money;
			int num2 = 0;
			try
			{
				num2 = _host.renown - renown;
			}
			catch
			{
			}
			bool flag = !Quests.Log.Exists((Quest x) => x.Giver == q.Giver && x.Kind == q.Kind);
			Check($"{q.Kind} done and paid", (q.Stage == 2 || flag) && num >= q.Money && flag, $"stage {q.Stage}, +{num} gold (promised {q.Money}), +{num2} renown");
		}

		private IEnumerator Supply()
		{
			Actor g = NextA();
			string[] array = new string[5] { "berries", "wheat", "fish", "meat", "bread" };
			foreach (string pResourceID in array)
			{
				try
				{
					int resourcesAmount = _a.getResourcesAmount(pResourceID);
					if (resourcesAmount > 0)
					{
						_a.takeResource(pResourceID, resourcesAmount);
					}
				}
				catch
				{
				}
			}
			yield return Accept(g, QuestKind.Supply);
			Quest q = _q;
			if (q != null)
			{
				int inStore = _a.getResourcesAmount(q.Thing);
				_host.addToInventory(q.Thing, q.Need);
				yield return CivilTest.Wait(0.7f);
				Check("the bag counts toward it", q.Have >= q.Need, $"{q.Have}/{q.Need} {q.Thing}");
				yield return Report(g, q, "Here: ");
				int num = Civil.BagCount(g, q.Thing);
				Check("the town's stores got it (or the giver carries it there)", _a.getResourcesAmount(q.Thing) + num >= inStore + q.Need, $"stores {inStore} -> {_a.getResourcesAmount(q.Thing)}, carried {num}");
				Check("it left your bag", Civil.BagCount(_host, q.Thing) == 0);
			}
		}

		private IEnumerator Hunt()
		{
			Actor g = NextA();
			Actor wolf = CivilTest.SpawnNear("wolf", g.current_position, 9f);
			if (Check("a wolf near the town", wolf != null))
			{
				yield return Accept(g, QuestKind.Hunt);
				Quest q = _q;
				if (q == null)
				{
					CivilTest.Kill(wolf);
					yield break;
				}
				Check("the hunt is for that wolf", q.Target == wolf.data.id, q.TargetName);
				Check("the compass points to it", Quests.Heading(q, out var at) && (at - wolf.current_position).magnitude < 0.5f);
				Quests.OnKill(wolf);
				CivilTest.Kill(wolf);
				yield return CivilTest.Wait(0.8f);
				Check("its death marks the hunt done", q.Stage == 1, Quests.Status(q));
				yield return Report(g, q);
			}
		}

		private IEnumerator Errand()
		{
			Actor g = NextA();
			Actor son = null;
			foreach (Actor item in _peopleB)
			{
				if (item != null && item.isAlive() && !item.isKing())
				{
					son = item;
					break;
				}
			}
			if (Check("someone in the other town", son != null))
			{
				CivilTest.SetSex(son, female: false);
				son.setParent1(g);
				Chronicle.Capture(son);
				Chronicle.Capture(g);
				yield return Accept(g, QuestKind.Errand);
				Quest q = _q;
				if (q != null)
				{
					Check("the errand is to the son in the other town", q.Target == son.data.id && q.Place == _b.name, q.TargetName + " in " + q.Place);
					yield return Talk(son, "I bring word from");
					Check("the word is delivered", q.Stage == 1);
					yield return Report(g, q);
				}
			}
		}

		private IEnumerator Craft()
		{
			Actor g = NextA();
			try
			{
				g.equipment.getSlot(EquipmentType.Weapon).takeAwayItem();
			}
			catch
			{
			}
			yield return Accept(g, QuestKind.Craft);
			Quest q = _q;
			if (q == null)
			{
				yield break;
			}
			EquipmentAsset equipmentAsset = null;
			foreach (EquipmentAsset item3 in AssetManager.items.list)
			{
				if (item3 != null && item3.id != null && item3.id.StartsWith(q.Thing, StringComparison.Ordinal))
				{
					equipmentAsset = item3;
					break;
				}
			}
			if (Check("an item of the kind exists", equipmentAsset != null, q.Thing))
			{
				Item item = World.world.items.generateItem(equipmentAsset, _host.kingdom, _host.getName(), 1, _host, 0, pByPlayer: true);
				ActorEquipmentSlot slot = _host.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				slot.setItem(item, _host);
				yield return CivilTest.Wait(0.7f);
				Check("holding it marks the task ready", q.Stage == 1);
				yield return Report(g, q, "Here's your new");
				Item item2 = null;
				try
				{
					item2 = g.equipment.getSlot(EquipmentType.Weapon).getItem();
				}
				catch
				{
				}
				Check("they hold it now, you don't", item2 == item && _host.equipment.getSlot(EquipmentType.Weapon).getItem() == null);
			}
		}

		private IEnumerator Match()
		{
			_single = CivilTest.SpawnNear("human", _kingA.current_position, 2f);
			_single2 = CivilTest.SpawnNear("human", _kingA.current_position, 3f);
			CivilTest.SetSex(_single, female: true);
			CivilTest.SetSex(_single2, female: false);
			Actor[] array = new Actor[2] { _single, _single2 };
			foreach (Actor actor in array)
			{
				if (actor != null)
				{
					try
					{
						actor.joinCity(_a);
						actor.makeWait(20f);
					}
					catch
					{
					}
				}
			}
			yield return CivilTest.Wait(1.5f);
			Actor g = ((_single != null && _single.isAlive() && !_single.hasLover()) ? _single : null);
			if (Check("a lonely heart", g != null))
			{
				Note(string.Format("{0}, free single {1}, can love: {2}, loved back: {3}, lovers {4}/{5}", Dialogue.NameOf(g), (_single2 != null) ? Dialogue.NameOf(_single2) : "none", _single2 != null && Quests.CanLove(g, _single2), _single2 != null && Quests.CanLove(_single2, g), g.hasLover(), _single2?.hasLover()));
				yield return Accept(g, QuestKind.Matchmaker);
				Quest q = _q;
				if (q != null)
				{
					Actor t = Chronicle.Find(q.Target);
					yield return Talk(t, q.GiverName + " is sweet on you");
					Check("they become lovers", g.lover == t && t.lover == g, Dialogue.NameOf(g) + " + " + q.TargetName);
					yield return CivilTest.Wait(0.6f);
					Check("the match marks the task ready", q.Stage == 1);
					yield return Report(g, q);
				}
			}
		}

		private IEnumerator Escort()
		{
			Actor g = NextA();
			yield return Accept(g, QuestKind.Escort);
			Quest q = _q;
			if (q != null)
			{
				Check("the escort goes to the other town", q.Place == _b.name, q.Place);
				Vector2 from = g.current_position;
				_host.current_position = g.current_position + new Vector2(6f, 0f);
				yield return CivilTest.Wait(4f);
				float magnitude = (g.current_position - from).magnitude;
				Check("they follow you", magnitude > 1f, $"{magnitude:F1} tiles");
				int money = CivilTest.Money(_host);
				Vector2 vector = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
				_host.current_position = vector + new Vector2(2f, 0f);
				g.current_position = vector + new Vector2(1f, 1f);
				yield return CivilTest.Wait(1f);
				Check("arriving with them finishes it and pays", q.Stage == 2 && CivilTest.Money(_host) >= money + q.Money, $"stage {q.Stage}");
			}
		}

		private IEnumerator Faith()
		{
			Actor g = NextA();
			Religion rel = null;
			try
			{
				rel = World.world.religions.newReligion(g, pAddDefaultTraits: true);
				g.setReligion(rel);
				_host.setReligion(rel);
			}
			catch (Exception ex)
			{
				Note("religion: " + ex.Message);
			}
			if (!Check("a faith you share", rel != null && _host.religion == rel))
			{
				yield break;
			}
			yield return Accept(g, QuestKind.Faith);
			Quest q = _q;
			if (q == null)
			{
				yield break;
			}
			for (int i = 0; i < q.Need; i++)
			{
				Actor actor = CivilTest.SpawnNear("human", _host.current_position, 3f + (float)i);
				if (actor != null)
				{
					try
					{
						actor.forgetReligion();
					}
					catch
					{
					}
					if (actor.tryToConvertToReligion(rel))
					{
						Quests.Converted(_host, actor);
					}
				}
			}
			yield return CivilTest.Wait(0.7f);
			Check("the converts are counted", q.Have >= q.Need && q.Stage == 1, $"{q.Have}/{q.Need}");
			yield return Report(g, q);
			try
			{
				_host.forgetReligion();
			}
			catch
			{
			}
		}

		private IEnumerator Pilgrimage()
		{
			Religion religion = null;
			try
			{
				religion = World.world.religions.newReligion(_kingA, pAddDefaultTraits: true);
				_kingA.setReligion(religion);
			}
			catch (Exception ex)
			{
				Note("religion: " + ex.Message);
			}
			Actor g = null;
			foreach (Actor item in _peopleB)
			{
				if (item != null && item.isAlive() && !item.isKing() && !item.isCityLeader())
				{
					g = item;
				}
			}
			if (Check("a believer in the other town", religion != null && g != null))
			{
				g.tryToConvertToReligion(religion);
				Note(religion.name + " began in " + (Quests.CityById(religion.data.creator_city_id)?.name ?? "?"));
				yield return Accept(g, QuestKind.Pilgrimage);
				Quest q = _q;
				if (q != null)
				{
					_host.current_position = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
					yield return CivilTest.Wait(0.8f);
					Check("standing in the holy town marks it done", q.Stage == 1, Quests.Status(q));
					yield return Report(g, q);
				}
			}
		}

		private IEnumerator Investigation()
		{
			Actor g = NextA();
			try
			{
				g.changeHappiness("got_robbed");
			}
			catch
			{
			}
			Chronicle.Capture(g);
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				try
				{
					if (simple != null && simple.isAlive() && simple.hasTrait("thief") && (simple.current_position - g.current_position).sqrMagnitude < 5625f)
					{
						simple.removeTrait("thief");
					}
				}
				catch
				{
				}
			}
			Actor thief = CivilTest.SpawnNear("human", g.current_position, 8f);
			if (!Check("a thief about", thief != null))
			{
				yield break;
			}
			try
			{
				thief.addTrait("thief");
				thief.addMoney(30);
			}
			catch
			{
			}
			yield return Accept(g, QuestKind.Investigation);
			Quest q = _q;
			if (q != null)
			{
				Check("the suspect is the thief", q.Target == thief.data.id, q.TargetName);
				yield return Talk(thief, "I know you robbed");
				Quest quest = Quests.Log.Find((Quest x) => x.Giver == q.Giver && x.Kind == q.Kind) ?? q;
				Check("confronting them marks it done", quest.Stage == 1, $"stage {quest.Stage}");
				yield return Report(g, q);
			}
		}

		private IEnumerator TheLog()
		{
			Actor g = NextA();
			Actor wolf = CivilTest.SpawnNear("wolf", g.current_position, 9f);
			yield return Accept(g, QuestKind.Hunt);
			Quest q = _q;
			if (q == null)
			{
				yield break;
			}
			Quest quest = Quests.Read(g.data);
			Check("the quest is in the giver's saved data", quest != null && quest.Kind == q.Kind && quest.Holder == _host.data.id && quest.Target == q.Target);
			Quests.Log.Clear();
			Quests.Rebuild(_host);
			Check("the log is rebuilt from the world", Quests.Log.Exists((Quest x) => x.Giver == g.data.id && x.Target == q.Target), Quests.Log.Count + " in the log");
			List<KeyValuePair<float, Color>> list = Mod.CompassMarks();
			Check("a gold mark on the compass", list.Exists((KeyValuePair<float, Color> m) => m.Value.r > 0.9f && m.Value.g > 0.7f && m.Value.b < 0.3f));
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "quests_hud.png"));
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			q = Quests.Log.Find((Quest x) => x.Giver == g.data.id);
			int opinion = Chronicle.Opinion(g, _host);
			q.Deadline = Quests.Now - 1.0;
			yield return CivilTest.Wait(0.8f);
			Check("a missed deadline fails it", q.Stage == 3 && !Quests.Log.Contains(q) && Quests.Read(g.data) == null);
			Check("...and they think less of you", Chronicle.Opinion(g, _host) < opinion);
			if (wolf != null)
			{
				CivilTest.Kill(wolf);
			}
			List<Quest> extra = new List<Quest>();
			for (int num = 0; num < 6; num++)
			{
				Quest quest2 = new Quest
				{
					Kind = QuestKind.Pilgrimage,
					Giver = _kingB.data.id + 1000000 + num,
					GiverName = "test",
					Place = "nowhere",
					X = 1,
					Y = 1
				};
				if (Quests.Accept(quest2, _host, null))
				{
					extra.Add(quest2);
				}
			}
			Check("no more than five tasks at a time", Quests.Log.Count <= 5, Quests.Log.Count.ToString());
			foreach (Quest item in extra)
			{
				Quests.Abandon(item, _host);
			}
			Check("giving up clears them", !Quests.Log.Exists((Quest x) => extra.Contains(x)));
		}

		private IEnumerator Daily()
		{
			Quest[] array = Quests.Log.ToArray();
			for (int i = 0; i < array.Length; i++)
			{
				Quests.Abandon(array[i], _host);
			}
			try
			{
				string[] array2 = new string[5] { "berries", "wheat", "fish", "meat", "bread" };
				foreach (string pResourceID in array2)
				{
					_a.takeResource(pResourceID, _a.getResourcesAmount(pResourceID));
				}
			}
			catch
			{
			}
			try
			{
				_a.data.removeString("fp_dq");
			}
			catch
			{
			}
			int day = Calendar.BillDay;
			List<Quest> list = Quests.Today(_a, _host);
			Note("today's notices: " + string.Join(", ", list.Select((Quest q) => q.Kind.ToString() + " for " + q.GiverName + " (" + q.Money + " gold)").ToArray()));
			Check("the board posts a contract for each kind of work, up to nine", list.Count >= 1 && list.Count <= 9 && (from q in list
				group q by q.Kind).All((IGrouping<QuestKind, Quest> g) => Enumerable.Count(g) <= 2), list.Count.ToString());
			Check("...posted today, by the town's own people (or the town, its hunt)", list.All(delegate(Quest q)
			{
				if (q.Posted == day)
				{
					if (!q.FromBoard)
					{
						return Chronicle.Find(q.Giver)?.city == _a;
					}
					if (q.Kind == QuestKind.Meat)
					{
						return q.Giver == _a.data.id;
					}
					return false;
				}
				return false;
			}));
			Check("...a hunt among them", list.Exists((Quest q) => q.Kind == QuestKind.Meat));
			Check("...the same all day", string.Join(",", (from q in Quests.Today(_a, _host)
				select q.Giver.ToString()).ToArray()) == string.Join(",", list.Select((Quest q) => q.Giver.ToString()).ToArray()));
			Quest first = list.FirstOrDefault((Quest q) => q.Kind != QuestKind.Meat && !q.FromBoard);
			if (first == null)
			{
				Note("(only a hunt on the board today)");
				yield break;
			}
			Actor giver = Chronicle.Find(first.Giver);
			bool flag = Quests.TakeNotice(_a, first, _host, null);
			Check("taken: your task from whoever pinned it up", flag && Quests.Log.Contains(first) && Quests.Read(giver.data)?.Holder == _host.data.id);
			Check("...off the board, and listed as taken today", !Quests.Today(_a, _host).Any((Quest q) => q.Giver == first.Giver) && Quests.TakenToday(_a).Contains(first));
			Calendar.SetDayForTest(day + 1);
			yield return CivilTest.Wait(1.2f);
			Check("at the next dawn, a taken notice is still yours", Quests.Log.Any((Quest q) => q.Giver == first.Giver && q.Kind == first.Kind && q.Stage == 0) && Quests.Read(giver.data)?.Holder == _host.data.id);
			List<Quest> list2 = Quests.Today(_a, _host);
			Check("...and the new day's go up", list2.All((Quest q) => q.Posted == day + 1), list2.Count + " notices");
		}
	}
}
