using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class JournalTest : CivilTest
	{
		private Actor _host;

		private readonly List<Actor> _people = new List<Actor>();

		protected override string Prefix => "JOURNAL";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 10, 0);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			foreach (Actor item2 in Founded)
			{
				if (item2 != null && item2 != actor && item2.isAdult())
				{
					_people.Add(item2);
				}
			}
			int num = _people.FindIndex((Actor u) => !Service.IsSoldier(u));
			if (num > 0)
			{
				Actor item = _people[num];
				_people.RemoveAt(num);
				_people.Insert(0, item);
			}
			_host = _people[0];
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				yield return Part("empty", Empty());
				yield return Part("tasks", Tasks());
				yield return Part("pings", PingsTab());
				yield return Part("tabs", Tabs());
				yield return Part("giving up and closing", GiveUp());
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

		private IEnumerator Empty()
		{
			Mod.CloseMapWindow();
			MapTabs.Last = null;
			Mod.MapKeyForTest();
			Journal.Tab = 0;
			Check("M opens the Map page", Journal.Open && !Mod.FamilyMap.Open && !Mod.WarMap.Open && !Mod.SoldierMap.Open);
			Check("a villager's tabs: Map and Family", Mod.HasMapPage(MapTabs.Page.Map) && Mod.HasMapPage(MapTabs.Page.Family) && !Mod.HasMapPage(MapTabs.Page.War) && !Mod.HasMapPage(MapTabs.Page.Orders), Service.IsSoldier(_host) ? "a soldier" : "");
			yield return Shot("journal_empty.png");
			Journal.Tab = 1;
			yield return Shot("journal_pings_empty.png");
			Mod.MapKeyForTest();
			Check("M again closes it", !Mod.MapWindowOpen);
		}

		private static Choice Find(Conversation c, string start)
		{
			for (int i = 0; i < 8; i++)
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
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private IEnumerator Take(Actor giver, QuestKind kind)
		{
			Journal.Open = false;
			Quests.ForceKindForTest = (int)kind;
			_host.current_position = giver.current_position - new Vector2(1.3f, 0f);
			try
			{
				giver.makeWait();
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(giver);
			Conversation converse = Mod.Converse;
			string[] array = ((!Quests.Quick(kind)) ? new string[3] { "Any work for me?", "I'll do it.", "Yes, that's right." } : new string[2] { "Any work for me?", "I'll do it." });
			foreach (string text in array)
			{
				Choice choice = Find(converse, text);
				if (choice == null)
				{
					Note("(no \"" + text + "\": " + string.Join(" | ", converse.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
					break;
				}
				converse.LineStart -= 999f;
				converse.Pick(converse.Choices.IndexOf(choice));
			}
			Mod.EndConversation();
			Quests.ForceKindForTest = -1;
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator Tasks()
		{
			Actor actor = CivilTest.SpawnNear("wolf", _host.current_position + new Vector2(25f, 6f));
			try
			{
				actor?.makeWait(120f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			yield return Take(_people[1], QuestKind.Hunt);
			City city = _host.city;
			string[] array = new string[5] { "berries", "wheat", "fish", "meat", "bread" };
			foreach (string pResourceID in array)
			{
				try
				{
					int resourcesAmount = city.getResourcesAmount(pResourceID);
					if (resourcesAmount > 0)
					{
						city.takeResource(pResourceID, resourcesAmount);
					}
				}
				catch
				{
				}
			}
			yield return Take(_people[2], QuestKind.Supply);
			Check("two tasks taken", Quests.Log.Count == 2, string.Join(", ", Quests.Log.ConvertAll((Quest q) => Quests.Title(q)).ToArray()));
			Quest hunt = Quests.Log.Find((Quest q) => q.Kind == QuestKind.Hunt);
			if (Check("the hunt is one", hunt != null))
			{
				string text = Quests.Promise(hunt);
				Note("promise: " + text);
				Check("what you promised, in their words", text.Contains("kill the wolf") && text.Contains(hunt.GiverName) && !text.Contains("told"), text);
				string text2 = Quests.TimeLeft(hunt);
				Check("how long is left", text2?.Contains("year") ?? false, text2);
				Mod.ShowMapPage(MapTabs.Page.Map);
				Journal.Tab = 0;
				Journal.PickedGiver = hunt.Giver;
				Journal.PickedKind = hunt.Kind;
				Check("the window is open", Journal.Open);
				yield return Shot("journal_tasks.png");
				int count = Mod.Pings.List.Count;
				Check("the hunt has a way to show", Quests.Heading(hunt, out var at), $"{at.x:F0},{at.y:F0}");
				Mod.Pings.Add(PingKind.Go, new Vector3(at.x, at.y, 0f), null, null, far: true);
				Check("marked on the compass", Mod.Pings.List.Count == count + 1 && Mod.Pings.List.Exists((Ping p) => p.Kind == PingKind.Go));
				yield return Shot("journal_tasks_marked.png");
			}
		}

		private IEnumerator PingsTab()
		{
			Journal.Tab = 1;
			Mod.SetPitchDegrees(-25f);
			yield return CivilTest.Wait(0.4f);
			int count = Mod.Pings.List.Count;
			bool flag = Mod.PingFromJournal(PingKind.Danger);
			Check("a ping put from the window", flag && Mod.Pings.List.Count == count + 1 && Mod.Pings.List[Mod.Pings.List.Count - 1].Kind == PingKind.Danger, Mod.Pings.List.Count + " pings");
			yield return Shot("journal_pings.png");
			if (Mod.Pings.List.Count > 0)
			{
				Ping ping = Mod.Pings.List[Mod.Pings.List.Count - 1];
				Mod.Pings.Remove(ping);
				Check("take off: one goes", !Mod.Pings.List.Contains(ping));
			}
			Mod.Pings.Clear();
			Check("take all off", Mod.Pings.List.Count == 0);
			Vector2 spot = _host.current_position + new Vector2(12f, -7f);
			Mod.MarkFromMap(spot, null);
			Ping go = Mod.Pings.List.Find((Ping p) => p.Kind == PingKind.Go);
			Check("a click on the map marks the way", go != null && (new Vector2(go.At.x, go.At.y) - spot).magnitude < 0.01f, Mod.Pings.List.Count + " pings");
			yield return Shot("journal_map_marked.png");
			Mod.MarkFromMap(spot, go);
			Check("a click on the mark takes it off", !Mod.Pings.List.Exists((Ping p) => p.Kind == PingKind.Go));
		}

		private IEnumerator Tabs()
		{
			if (!Journal.Open)
			{
				Mod.ShowMapPage(MapTabs.Page.Map);
			}
			Mod.WantMapPage(MapTabs.Page.Family);
			yield return null;
			yield return null;
			Check("the Family tab swaps the page", Mod.FamilyMap.Open && !Journal.Open && Mod.MapWindowOpen);
			yield return Shot("journal_tab_family.png");
			Mod.MapKeyForTest();
			Check("M closes it from any page", !Mod.MapWindowOpen);
			Mod.MapKeyForTest();
			yield return null;
			Check("M reopens the page used last", Mod.FamilyMap.Open);
			Mod.WantMapPage(MapTabs.Page.Map);
			yield return null;
			yield return null;
			Check("back to the Map page", Journal.Open && !Mod.FamilyMap.Open);
		}

		private IEnumerator GiveUp()
		{
			Quest quest = Quests.Log.Find((Quest q) => q.Kind == QuestKind.Supply);
			if (Check("the supply task is there", quest != null))
			{
				Mod.AbandonFromJournal(quest);
				Check("given up, it leaves the log", !Quests.Log.Exists((Quest q) => q.Kind == QuestKind.Supply), Quests.Log.Count + " left");
			}
			Journal.Tab = 0;
			yield return Shot("journal_after.png");
			Check("Esc closes it", Mod.CivilEscape() && !Journal.Open);
			bool talking = false;
			int tries = 0;
			while (!talking && tries < 4)
			{
				tries++;
				if (Mod.Converse.Open)
				{
					Mod.EndConversation();
				}
				yield return null;
				Mod.ShowMapPage(MapTabs.Page.Map);
				CivilTest.StandBefore(_people[3], _host, 1.2f);
				yield return null;
				Mod.StartConversationForTest(_people[3]);
				for (int i = 0; i < 3; i++)
				{
					yield return null;
					talking |= Mod.Converse.Open;
				}
			}
			Check("a talk closes it", !Journal.Open && talking, (!talking) ? ("the talk ended at once, " + tries + " tries") : ((tries > 1) ? (tries + " tries") : ""));
			Mod.EndConversation();
		}
	}
}
