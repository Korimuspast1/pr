using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TalksTest : CivilTest
	{
		private Actor _host;

		private Actor _npc;

		private readonly List<Actor> _village = new List<Actor>();

		protected override string Prefix => "TALKS";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 14, 3);
			if (!Check("a village founded", king != null))
			{
				yield break;
			}
			_ = king.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive())
				{
					_village.Add(item);
				}
			}
			foreach (Actor item2 in _village)
			{
				if (item2 != king && item2.isAdult() && _host == null)
				{
					_host = item2;
				}
			}
			foreach (Actor item3 in _village)
			{
				if (item3 != king && item3 != _host && item3.isAdult() && _npc == null)
				{
					_npc = item3;
				}
			}
			if (Check("a host and someone to talk to", _host != null && _npc != null))
			{
				try
				{
					Religion religion = World.world.religions.newReligion(king, pAddDefaultTraits: true);
					king.setReligion(religion);
					_npc.tryToConvertToReligion(religion);
					Note("religion " + religion.name + " founded by " + Dialogue.NameOf(king));
				}
				catch (Exception ex)
				{
					Note("no religion: " + ex.Message);
				}
				try
				{
					_host.addMoney(200);
				}
				catch
				{
				}
				yield return Possess(_host);
				if (Check("first person on a villager", InControl(_host)))
				{
					yield return Part("topics", Topics());
					yield return Part("the wheel and a long list", WheelAndLongList());
					yield return Part("voices", Voices());
					yield return Part("animals and babies", NotPeople());
					yield return Part("gossip and petitions", Court(king));
					yield return Part("endings", Endings());
				}
			}
		}

		private static Choice Find(Conversation c, string start)
		{
			foreach (Choice choice in c.Choices)
			{
				if (choice.Label.StartsWith(start, StringComparison.Ordinal))
				{
					return choice;
				}
			}
			return null;
		}

		private IEnumerator ReadAll(Conversation c, List<string> into)
		{
			into.Clear();
			for (int i = 0; i < 12; i++)
			{
				if (c.Line != null)
				{
					into.Add(c.Line);
				}
				if (!c.MorePages)
				{
					break;
				}
				c.Next();
				c.Next();
				yield return null;
			}
			if (Options.Contains("nolog"))
			{
				yield break;
			}
			foreach (string item in into)
			{
				Note("  \"" + item + "\"");
			}
		}

		private IEnumerator Say(Conversation c, string label, List<string> heard)
		{
			Choice choice = Find(c, label);
			if (choice == null)
			{
				heard.Clear();
				Note("(no choice \"" + label + "\" now: " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
			}
			else
			{
				int index = c.Choices.IndexOf(choice);
				c.LineStart -= 999f;
				c.Pick(index);
				yield return CivilTest.Wait(0.15f);
				yield return ReadAll(c, heard);
			}
		}

		private IEnumerator Shot(string name)
		{
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			}
			catch
			{
			}
			yield return null;
			yield return null;
		}

		private IEnumerator Topics()
		{
			Conversation c = Mod.Converse;
			CivilTest.StandBefore(_npc, _host, 1.4f);
			yield return CivilTest.Wait(0.4f);
			Vector2 hostAt = _host.current_position;
			bool flag = Mod.StartConversationForTest(_npc);
			Check("E on a villager opens a conversation", flag && c.Open);
			Check("the talk knows who they are to you", !string.IsNullOrEmpty(c.Relation), c.Relation);
			Check("you're held still", PossessionHooks.Frozen);
			Check("the game's hotkeys are held back", Mod.GameKeysMuted);
			List<string> heard = new List<string>();
			yield return ReadAll(c, heard);
			Check("they greet you", heard.Count > 0 && heard[0].Length > 2, (heard.Count > 0) ? heard[0] : null);
			Note("topics: " + string.Join(" | ", c.Choices.ConvertAll((Choice choice2) => choice2.Label + (choice2.Enabled ? "" : (" (off: " + choice2.Note + ")"))).ToArray()));
			yield return CivilTest.Wait(0.6f);
			yield return Shot("talks_greeting.png");
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return CivilTest.Wait(1f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			Check("you stay put while talking", (_host.current_position - hostAt).magnitude < 0.25f, $"{(_host.current_position - hostAt).magnitude:F2} tiles");
			Vector2 npcAt = _npc.current_position;
			yield return CivilTest.Wait(2.5f);
			Check("they wait for you", (_npc.current_position - npcAt).magnitude < 0.8f, $"{(_npc.current_position - npcAt).magnitude:F2} tiles");
			Vector2 vector = _npc.current_position - _host.current_position;
			float num = Mathf.Abs(Mathf.DeltaAngle(Mod.ViewYaw * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f));
			Check("you turned to face them", num < 30f, $"{num:F0} degrees off");
			if (Check("their head is found", Mod.TalkHeadOnScreenForTest(_npc, out var x, out var y, out var card)))
			{
				Check("you look at their head, over the talk's card", Mathf.Abs(x - 0.5f) < 0.05f && y > 0.2f && y < ((card > 0f) ? card : 0.5f), $"head at {x:F2} across, {y:F2} down (card top {card:F2}), pitch {Mathf.Atan(Mod.View.Pitch) * 57.29578f:F1} deg");
			}
			Mod.SaveFrame("talks_head.png");
			string[] array = new string[7] { "Tell me about your family.", "What do you believe in?", "Tell me about your life.", "Any news?", "What do you think of the king?", "Small talk", "Any work for me?" };
			string[] array2 = array;
			foreach (string t in array2)
			{
				Note("> " + t);
				yield return Say(c, t, heard);
				bool ok = heard.Count > 0 && heard.TrueForAll((string l) => !string.IsNullOrEmpty(l) && l.Length > 1);
				Check("\"" + t + "\" gets an answer", ok, heard.Count + " pages, topic " + c.LastTopic);
				if (t.StartsWith("Tell me about your family"))
				{
					Check("the family tree opens", c.Panel == TalkPanel.Family && c.Tree != null, (c.Tree != null) ? (c.Tree.Count + " on the tree") : null);
					yield return CivilTest.Wait(0.5f);
					yield return Shot("talks_family.png");
				}
				if (t.StartsWith("What do you believe") && _npc.religion != null)
				{
					Check("the faith topic names their religion", string.Join(" ", heard.ToArray()).Contains(_npc.religion.name));
				}
				if (t.StartsWith("Any work"))
				{
					yield return Shot("talks_work.png");
				}
				Choice choice = Find(c, "Let's talk about something else") ?? Find(c, "Not now") ?? Find(c, "Let's talk");
				if (choice != null)
				{
					c.LineStart -= 999f;
					c.Pick(c.Choices.IndexOf(choice));
					yield return null;
				}
				else if (c.Choices.Count > 0 && c.Choices[0].Label == "Go on...")
				{
					c.Next();
				}
			}
			int before = CivilTest.Money(_host);
			int theirs = CivilTest.Money(_npc);
			int opinion = Chronicle.Opinion(_npc, _host);
			yield return Say(c, "Offer a gift", heard);
			yield return Say(c, "Give 20 gold", heard);
			Check("a gift of 20 gold changes hands", CivilTest.Money(_host) == before - 20 && CivilTest.Money(_npc) == theirs + 20, $"{before}->{CivilTest.Money(_host)}, {theirs}->{CivilTest.Money(_npc)}");
			Check("...and they like you more", Chronicle.Opinion(_npc, _host) > opinion, $"{opinion} -> {Chronicle.Opinion(_npc, _host)}");
			Regard regard = Chronicle.RegardOf(_npc, _host, create: false);
			Check("talks and gifts are counted", regard != null && regard.Talks >= 1 && regard.Gifts >= 1);
			Mod.CivilEscape();
			Check("Esc ends the talk", !c.Open);
			Check("you can walk again", !PossessionHooks.Frozen);
			Check("the hotkeys are back", !Mod.GameKeysMuted);
		}

		private IEnumerator WheelAndLongList()
		{
			Conversation c = Mod.Converse;
			CivilTest.StandBefore(_npc, _host, 1.4f);
			yield return CivilTest.Wait(0.3f);
			if (!Check("a talk to scroll in", Mod.StartConversationForTest(_npc) && c.Open))
			{
				yield break;
			}
			List<string> list = new List<string>();
			yield return ReadAll(c, list);
			List<Choice> many = new List<Choice>();
			for (int i = 0; i < 60; i++)
			{
				many.Add(new Choice("Choice " + (i + 1), null));
			}
			many[1].Off("not now");
			c.Offer(many);
			c.LineStart -= 999f;
			yield return null;
			WorldBoxMod.TalkWheelForTest = -1f;
			yield return null;
			yield return null;
			Check("the wheel moves the pick down, past what can't be said", c.Selected == 2, "picked " + c.Selected);
			for (int j = 0; j < 2; j++)
			{
				WorldBoxMod.TalkWheelForTest = 1f;
				yield return null;
				yield return null;
			}
			Check("...and up, stopping at the top", c.Selected == 0, "picked " + c.Selected);
			float was = Mod.Settings.UiScale;
			float scale = GameArt.P;
			float big = Mathf.Min(10f, scale / Mathf.Clamp(was, 0.8f, 1.6f) * 1.6f);
			Mod.Settings.UiScale = 1.6f;
			for (float t = 0f; t < 3f; t += 0.1f)
			{
				if (!(GameArt.P < big - 0.01f))
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			yield return null;
			Check("the interface at its biggest", GameArt.P >= big - 0.01f, $"scale {scale:F2} -> {GameArt.P:F2}");
			int rows = (many.Count + 1) / 2;
			Check("a long list at the biggest interface size scrolls", Hud.TalkRowsShown >= 2 && Hud.TalkRowsShown < rows, $"{Hud.TalkRowsShown} of {rows} rows shown");
			Check("...with the card still on the screen", Hud.TalkCardTop > 0f, $"card top {Hud.TalkCardTop:F2}");
			c.Selected = many.Count - 1;
			yield return CivilTest.Wait(0.2f);
			Check("the last choice picked is scrolled into view", Hud.TalkFirstRow + Hud.TalkRowsShown == rows, $"rows {Hud.TalkFirstRow + 1}-{Hud.TalkFirstRow + Hud.TalkRowsShown} of {rows}");
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "talks_scroll.png"));
			for (int j = 0; j < 120; j++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			for (int j = 0; j < many.Count; j++)
			{
				WorldBoxMod.TalkWheelForTest = 1f;
				yield return null;
			}
			yield return null;
			Check("the wheel back up scrolls to the top", c.Selected == 0 && Hud.TalkFirstRow == 0, $"picked {c.Selected}, first row {Hud.TalkFirstRow}");
			c.Selected = many.Count - 1;
			yield return CivilTest.Wait(0.1f);
			c.Offer(many);
			yield return CivilTest.Wait(0.1f);
			Check("a new set of choices starts at the top", Hud.TalkFirstRow == 0, "first row " + Hud.TalkFirstRow);
			Mod.Settings.UiScale = was;
			Mod.EndConversation();
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator Voices()
		{
			string text = Dialogue.Speak(new Voice
			{
				Broken = true
			}, "I am going to the market, my friend! The bread is very good there.");
			string text2 = Dialogue.Speak(new Voice
			{
				Flat = true
			}, "Oh! *laughs* What a day!");
			string text3 = Dialogue.Speak(new Voice
			{
				Mute = true
			}, "I am going to the market, my friend! The bread is very good there.");
			Note("broken: " + text + " | flat: " + text2 + " | mute: " + text3);
			Check("a kind without the language centre speaks broken words", text != "I am going to the market, my friend! The bread is very good there." && text.Contains("me") && !text.Contains(" the "), text);
			Check("a kind without feeling speaks flat", !text2.Contains("!") && !text2.Contains("*"), text2);
			Check("a mute one only gestures", text3.StartsWith("*") && text3.EndsWith("*"), text3);
			Voice voice = Dialogue.VoiceOf(_npc, _host);
			Note(string.Format("human brain: wernicke {0}, amygdala {1}, hippocampus {2}, emotions {3}", _npc.hasSubspeciesTrait("wernicke_area"), _npc.hasSubspeciesTrait("amygdala"), _npc.hasSubspeciesTrait("advanced_hippocampus"), _npc.hasEmotions()));
			Check("a human speaks plainly", !voice.Broken && !voice.Mute && !voice.Flat, $"broken {voice.Broken} flat {voice.Flat} mute {voice.Mute} forgetful {voice.Forgetful} warmth {voice.Warmth} manner {voice.Manner}");
			string[] array = new string[5] { "civ_cat", "civ_frog", "orc", "civ_crystal_golem", "skeleton" };
			foreach (string text4 in array)
			{
				Actor actor = CivilTest.Spawn(text4, _host.current_position + new Vector2(5f, 5f));
				if (actor != null)
				{
					Voice v = Dialogue.VoiceOf(actor, _host);
					Note(string.Format("{0}: sapient {1} emotions {2} broken {3} flat {4} mute {5} forgetful {6} -> \"{7}\"", text4, actor.isSapient(), actor.hasEmotions(), v.Broken, v.Flat, v.Mute, v.Forgetful, Dialogue.Speak(v, "I am happy to see you again, my friend!")));
					CivilTest.Kill(actor);
				}
			}
			yield return null;
		}

		private IEnumerator NotPeople()
		{
			Actor actor = CivilTest.Spawn("sheep", _host.current_position + new Vector2(1f, 0f));
			if (actor != null)
			{
				Check("E on a sheep is not a conversation", !Mod.StartConversationForTest(actor) && !Mod.Converse.Open);
			}
			Actor actor2 = CivilTest.Spawn("human", _host.current_position + new Vector2(1f, 1f), adult: false);
			bool flag = false;
			try
			{
				flag = actor2?.isBaby() ?? false;
			}
			catch
			{
			}
			if (flag)
			{
				Check("E on a baby is not a conversation", !Mod.StartConversationForTest(actor2) && !Mod.Converse.Open);
			}
			else
			{
				Note("(the child spawned is no baby: skipped)");
			}
			if (actor != null)
			{
				CivilTest.Kill(actor);
			}
			if (actor2 != null)
			{
				CivilTest.Kill(actor2);
			}
			yield return null;
		}

		private IEnumerator Court(Actor king)
		{
			Conversation c = Mod.Converse;
			List<string> heard = new List<string>();
			Actor actor = null;
			Actor actor2 = null;
			foreach (Actor item in _village)
			{
				if (item != null && item.isAlive() && item != _host && item != _npc && item != king && item.isAdult())
				{
					if (actor == null)
					{
						actor = item;
					}
					else if (actor2 == null && item != actor)
					{
						actor2 = item;
					}
				}
			}
			if (actor != null && actor2 != null)
			{
				try
				{
					actor.becomeLoversWith(actor2);
					actor.changeHappiness("fallen_in_love");
				}
				catch
				{
				}
				Chronicle.Capture(actor);
				List<string> couples = new List<string>();
				foreach (Actor item2 in _village)
				{
					if (item2 != null && item2.isAlive() && item2.lover != null)
					{
						couples.Add(Dialogue.NameOf(item2));
					}
				}
				string gossip = Dialogue.Gossip(_npc, _host);
				Check("the neighbours gossip about a couple", gossip != null && couples.Exists((string n) => gossip.Contains(n)), gossip);
				CivilTest.StandBefore(_npc, _host, 1.4f);
				Mod.StartConversationForTest(_npc);
				yield return Say(c, "Any news?", heard);
				Check("the news includes gossip", heard.Exists((string l) => couples.Exists((string n) => l.Contains(n))), string.Join(" / ", heard.ToArray()));
				Mod.EndConversation();
			}
			Actor ruled = _npc;
			yield return Possess(king, 1.2f);
			if (!Check("in control of the king", InControl(king)))
			{
				yield break;
			}
			try
			{
				king.addMoney(200);
			}
			catch
			{
			}
			CivilTest.StandBefore(ruled, king, 1.4f);
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(ruled);
			Choice choice = c.Choices.Find((Choice ch) => ch.Label.StartsWith("Do you have a petition"));
			Check("a subject brings the king a petition", choice != null);
			if (choice != null)
			{
				int money = CivilTest.Money(king);
				int favour = Law.Favour(ruled.city);
				yield return Say(c, "Do you have a petition", heard);
				Choice choice2 = ((c.Choices.Count > 0) ? c.Choices[0] : null);
				Check("the petition asks for something real", heard.Count > 0 && choice2 != null && choice2.Enabled, (heard.Count > 0) ? (heard[0] + " / " + choice2?.Label) : null);
				if (choice2 != null)
				{
					c.LineStart -= 999f;
					c.Pick(0);
					yield return null;
				}
				Check("granting it costs the king gold", CivilTest.Money(king) < money, $"{money} -> {CivilTest.Money(king)}");
				Check("...and wins the town's favour (its loyalty)", Law.Favour(ruled.city) > favour, $"{favour} -> {Law.Favour(ruled.city)}");
				Check("the favour is in the game's own loyalty sum", AssetManager.loyalty_library.get("fp_favour") != null);
				yield return Say(c, "Do you have a petition", heard);
				Check("one petition a year each", heard.Count > 0 && heard[0].Contains("already"), (heard.Count > 0) ? heard[0] : null);
			}
			Mod.EndConversation();
			yield return Possess(_host, 1.2f);
		}

		private IEnumerator Endings()
		{
			Conversation c = Mod.Converse;
			List<string> heard = new List<string>();
			CivilTest.StandBefore(_npc, _host, 1.4f);
			Mod.StartConversationForTest(_npc);
			yield return Say(c, "Goodbye.", heard);
			Check("goodbye says farewell", heard.Count > 0 && c.EndAfter, (heard.Count > 0) ? heard[0] : null);
			for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
			{
				if (!c.Open)
				{
					break;
				}
				yield return null;
			}
			Check("...and ends the talk", !c.Open);
			Check("...and you can walk again", !PossessionHooks.Frozen);
			yield return null;
			Check("a click as it closes is no blow", Mod.ClickGrace && PossessionHooks.BlockAttack, $"grace {Mod.ClickGrace} block {PossessionHooks.BlockAttack}");
			yield return new WaitForSecondsRealtime(0.7f);
			Check("...and a moment later the button strikes again", !Mod.ClickGrace, $"grace {Mod.ClickGrace}");
			CivilTest.StandBefore(_npc, _host, 1.4f);
			Mod.StartConversationForTest(_npc);
			yield return Say(c, "Goodbye.", heard);
			for (float t = 0f; t < 3f; t += 0.1f)
			{
				if (!c.Open)
				{
					break;
				}
				if (c.Typed)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			int bye = c.Choices.FindIndex((Choice ch) => ch.Label == "Goodbye");
			c.Pick(bye);
			yield return CivilTest.Wait(0.2f);
			Check("Goodbye picked by key ends the talk and frees you", bye >= 0 && !c.Open && !PossessionHooks.Frozen, $"choice {bye} open {c.Open} frozen {PossessionHooks.Frozen}");
			Check("they're on your list of people met", PeopleMet.Of(_host).Exists((Met m) => m.Id == _npc.data.id && m.Talks >= 2), PeopleMet.Of(_host).Count + " met");
			CivilTest.StandBefore(_npc, _host, 1.4f);
			Mod.StartConversationForTest(_npc);
			yield return CivilTest.Wait(0.3f);
			_npc.current_position = _host.current_position + new Vector2(12f, 0f);
			yield return CivilTest.Wait(0.4f);
			Check("them walking off ends it", !c.Open);
			Actor other = null;
			foreach (Actor item in _village)
			{
				if (item != null && item.isAlive() && item != _host && item != _npc && item.isAdult())
				{
					other = item;
					break;
				}
			}
			if (other != null)
			{
				CivilTest.StandBefore(other, _host, 1.4f);
				Mod.StartConversationForTest(other);
				yield return CivilTest.Wait(0.3f);
				bool was = c.Open;
				CivilTest.Kill(other);
				yield return CivilTest.Wait(0.4f);
				Check("them dying ends it", was && !c.Open);
			}
			Mod.EnterRoomForTest(new List<Actor> { _npc });
			yield return CivilTest.Wait(1f);
			bool inside = Mod.Interior.Inside;
			Check("into a room with them", inside);
			if (inside)
			{
				bool flag = Mod.StartConversationForTest(_npc);
				Check("a talk indoors", flag && c.Open);
				yield return CivilTest.Wait(0.5f);
				yield return Shot("talks_indoors.png");
				yield return CivilTest.Wait(1.5f);
				if (Check("their head is found indoors", Mod.TalkHeadOnScreenForTest(_npc, out var x, out var y, out var card)))
				{
					Check("indoors too you look at their head, over the card", Mathf.Abs(x - 0.5f) < 0.08f && y > 0.2f && y < ((card > 0f) ? card : 0.5f), $"head at {x:F2} across, {y:F2} down (card top {card:F2}), pitch {Mathf.Atan(Mod.View.Pitch) * 57.29578f:F1} deg");
				}
				Mod.SaveFrame("talks_head_indoors.png");
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.3f);
				Check("leaving the room ends it", !c.Open);
			}
			CivilTest.StandBefore(_npc, _host, 1.4f);
			Mod.StartConversationForTest(_npc);
			yield return CivilTest.Wait(0.3f);
			Actor leapTo = null;
			foreach (Actor item2 in _village)
			{
				if (item2 != null && item2.isAlive() && item2 != _host && item2 != _npc && item2.isAdult())
				{
					leapTo = item2;
					break;
				}
			}
			if (leapTo != null)
			{
				yield return Possess(leapTo, 1f);
				Check("leaping into another body ends it", !c.Open && InControl(leapTo));
				yield return Possess(_host, 1f);
			}
			CivilTest.StandBefore(_npc, _host, 1.4f);
			Mod.StartConversationForTest(_npc);
			yield return CivilTest.Wait(0.3f);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1f);
			Check("leaving first person ends it", !c.Open && !PossessionHooks.Frozen);
		}
	}
}
