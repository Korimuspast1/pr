using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BriefTest : CivilTest
	{
		private Actor _host;

		private Actor _giver;

		private Actor _faithful;

		protected override string Prefix => "BRIEF";

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 8, 0);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAdult())
				{
					list.Add(item);
				}
			}
			_host = list[0];
			_giver = list[1];
			_faithful = list[2];
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
				Actor actor2 = CivilTest.SpawnNear("wolf", _giver.current_position + new Vector2(20f, 0f));
				try
				{
					actor2?.makeWait(60f);
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.5f);
				Quests.ForceKindForTest = 1;
				yield return Part("plain work", PlainWork());
				Quests.ForceKindForTest = 6;
				yield return Part("summing up", SummingUp());
				yield return Part("backing out", BackOut());
				Quests.ForceKindForTest = -1;
			}
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

		private static string Labels(Conversation c)
		{
			return string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray());
		}

		private static string AllPages(Conversation c)
		{
			return string.Join(" / ", c.Pages.ToArray());
		}

		private bool Say(Conversation c, string start)
		{
			Choice choice = Find(c, start);
			if (choice == null)
			{
				Note("(no \"" + start + "\": " + Labels(c) + ")");
				return false;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			return true;
		}

		private IEnumerator Open(Actor giver)
		{
			Mod.EndConversation();
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
		}

		private IEnumerator PlainWork()
		{
			Conversation c = Mod.Converse;
			int before = Quests.Log.Count;
			yield return Open(_giver);
			Choice choice = Find(c, "Any work for me?");
			if (Check("they have work", choice != null, Labels(c)))
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
				Check("they think a moment", c.Thinking, $"age {c.Age:F2}");
				string text = AllPages(c);
				Note("brief: " + text);
				Check("the job in one line", c.Pages.Count == 1, c.Pages.Count + " pages");
				Check("what, by when and for what", text.Contains("wolf") && text.Contains("within") && text.Contains("gold"), text);
				Check("the choices come straight after", Find(c, "I'll do it.") != null && Find(c, "I have a question.") != null, Labels(c));
				Check("you can still ask", Say(c, "I have a question."));
				Note("questions: " + Labels(c));
				Check("where and how dangerous are on offer", Find(c, "Where is it?") != null && Find(c, "How dangerous is it?") != null);
				Say(c, "Where is it?");
				string text2 = AllPages(c);
				Note("where: " + text2);
				Check("the answer gives a direction", text2.Contains("East") || text2.Contains("east") || text2.Contains("Close by"), text2);
				Check("a question asked is gone from the list", Find(c, "Where is it?") == null && Find(c, "How dangerous") != null, Labels(c));
				Say(c, "How long do I have?");
				Check("the time they give", AllPages(c).Contains("Two years"), AllPages(c));
				Say(c, "No more questions.");
				Check("no task before you've agreed", Quests.Log.Count == before);
				Say(c, "I'll do it.");
				Note("after: " + AllPages(c));
				Check("no summing up for plain work", !AllPages(c).Contains("understood"), AllPages(c));
				Quest quest = Quests.Log.Find((Quest x) => x.Giver == _giver.data.id);
				Check("the task is yours on \"I'll do it.\"", quest != null && quest.Kind == QuestKind.Hunt, (quest != null) ? Quests.Title(quest) : "none");
				Mod.EndConversation();
				if (quest != null)
				{
					Quests.Abandon(quest, _host);
				}
				yield return CivilTest.Wait(0.3f);
				before = Quests.Log.Count;
				yield return Open(_giver);
				Say(c, "Any work for me?");
				Check("\"Not now.\" turns it down", Say(c, "Not now."), Labels(c));
				Mod.EndConversation();
				Check("and leaves your log as it was", Quests.Log.Count == before);
			}
		}

		private IEnumerator SummingUp()
		{
			Conversation c = Mod.Converse;
			int before = Quests.Log.Count;
			Religion religion = null;
			try
			{
				religion = World.world.religions.newReligion(_faithful, pAddDefaultTraits: true);
				_faithful.setReligion(religion);
				_host.setReligion(religion);
			}
			catch (Exception ex)
			{
				Note("religion: " + ex.Message);
			}
			if (Check("a faith you share", religion != null && _host.religion == religion))
			{
				yield return Open(_faithful);
				Say(c, "Any work for me?");
				Check("they think before they speak", c.Thinking, $"age {c.Age:F2}");
				string text = AllPages(c);
				Note("brief: " + text);
				Check("they say plainly what they want", text.Contains("To be clear"), text);
				Check("and ask if you have questions", text.Contains("question") || text.Contains("unclear"), text);
				Say(c, "I'll do it.");
				string text2 = AllPages(c);
				Note("recap: " + text2);
				Check("they say the deal back", text2.Contains("souls") && text2.Contains("understood"), text2);
				Check("still no task until you agree", Quests.Log.Count == before);
				Say(c, "Not quite. I have a question.");
				Check("back to the questions", Find(c, "How do I win souls?") != null, Labels(c));
				Say(c, "How do I win souls?");
				Say(c, "No more questions.");
				Say(c, "I'll do it.");
				Check("you agree", Say(c, "Yes, that's right."));
				Mod.EndConversation();
				Quest quest = Quests.Log.Find((Quest x) => x.Giver == _faithful.data.id);
				Check("now the task is yours", quest != null && quest.Kind == QuestKind.Faith, (quest != null) ? Quests.Title(quest) : "none");
				if (quest != null)
				{
					Quests.Abandon(quest, _host);
				}
				yield return CivilTest.Wait(0.3f);
			}
		}

		private IEnumerator BackOut()
		{
			Conversation c = Mod.Converse;
			int before = Quests.Log.Count;
			yield return Open(_faithful);
			Say(c, "Any work for me?");
			Say(c, "I'll do it.");
			Check("backing out at the summing up", Say(c, "On second thought, no."), Labels(c));
			Mod.EndConversation();
			Check("leaves your log as it was", Quests.Log.Count == before);
		}
	}
}
