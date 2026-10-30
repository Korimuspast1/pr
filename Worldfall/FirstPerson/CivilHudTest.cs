using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class CivilHudTest : CivilTest
	{
		protected override string Prefix => "CHUD";

		private IEnumerator Capture(string name)
		{
			yield return CivilTest.Wait(0.8f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			yield return null;
			yield return null;
			Note("captured " + name);
		}

		private void Say(Conversation c, string label)
		{
			for (int i = 0; i < 6; i++)
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
			Choice choice = c.Choices.Find((Choice x) => x.Label.StartsWith(label));
			if (choice == null)
			{
				Note("(no \"" + label + "\": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
			}
			else
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
			}
		}

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 14, 2);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>(Founded);
			Actor host = list[1];
			Actor npc = list[2];
			try
			{
				host.addMoney(120);
			}
			catch
			{
			}
			yield return Possess(host);
			if (!Check("first person", InControl(host)))
			{
				yield break;
			}
			Conversation c = Mod.Converse;
			CivilTest.StandBefore(npc, host, 1.4f);
			yield return CivilTest.Wait(0.4f);
			Mod.StartConversationForTest(npc);
			yield return Capture("civhud_talk.png");
			Choice choice = c.Choices.Find((Choice x) => x.Label.StartsWith("Tell me about your family"));
			if (choice != null)
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
			}
			yield return Capture("civhud_family.png");
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			Quests.ForceKindForTest = 0;
			string[] array = new string[5] { "berries", "wheat", "fish", "meat", "bread" };
			foreach (string pResourceID in array)
			{
				try
				{
					int resourcesAmount = npc.city.getResourcesAmount(pResourceID);
					if (resourcesAmount > 0)
					{
						npc.city.takeResource(pResourceID, resourcesAmount);
					}
				}
				catch
				{
				}
			}
			Mod.StartConversationForTest(npc);
			Say(c, "Any work");
			Say(c, "I'll do it");
			Quests.ForceKindForTest = -1;
			Mod.EndConversation();
			yield return Capture("civhud_quest.png");
			Check("a quest on the tracker", Quests.Log.Count > 0);
			yield return CivilTest.Wait(5.5f);
			Law.Board board = null;
			foreach (Law.Board item in Law.BoardsNear(host.current_position, 200f))
			{
				board = item;
			}
			Actor wolf = ((board != null) ? CivilTest.SpawnNear("wolf", board.At, 14f) : null);
			yield return CivilTest.Wait(0.6f);
			if (Check("a board", board != null))
			{
				board.PostersAt = -99f;
				Law.OpenBoard = board;
				Law.BoardOpen = true;
				yield return Capture("civhud_board.png");
				Law.BoardOpen = false;
			}
			Actor actor2 = CivilTest.SpawnNear("human", host.current_position, 1f, adult: false);
			if (actor2 != null)
			{
				actor2.setParent1(host);
				Births.Naming = actor2;
				Births.NameText = "Rowan";
				yield return Capture("civhud_name.png");
				Births.FinishNaming(host, keep: false);
			}
			yield return CivilTest.Wait(0.5f);
			CivilTest.Kill(host);
			for (float t = 0f; t < 3f; t += 0.2f)
			{
				if (Mod.ChoosingHeir)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(1.4f);
			Check("the heir picker", Mod.ChoosingHeir);
			yield return Capture("civhud_heir.png");
			if (Mod.ChoosingHeir)
			{
				Mod.ContinueAs(0);
			}
			yield return CivilTest.Wait(1f);
			if (wolf != null)
			{
				CivilTest.Kill(wolf);
			}
		}
	}
}
