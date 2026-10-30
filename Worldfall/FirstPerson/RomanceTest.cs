using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RomanceTest : CivilTest
	{
		private Actor _host;

		private Actor _a;

		private Actor _b;

		private Actor _c;

		private Actor _d;

		private Actor _kid;

		private Actor _leader;

		private Actor _rival;

		protected override string Prefix => "ROMANCE";

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 10, 2);
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
			_kid = Founded.Find((Actor u) => u != null && !u.isAdult());
			if (!Check("enough grown-ups", list.Count >= 5, list.Count.ToString()))
			{
				yield break;
			}
			_host = list[0];
			_a = list[1];
			_b = list[2];
			_c = list[3];
			_d = list[4];
			CivilTest.SetSex(_host, female: false);
			CivilTest.SetSex(_d, female: false);
			CivilTest.SetSex(_a, female: true);
			CivilTest.SetSex(_b, female: true);
			CivilTest.SetSex(_c, female: true);
			foreach (Actor item2 in list)
			{
				try
				{
					item2.data.removeString("fp_court");
				}
				catch
				{
				}
			}
			Fresh();
			_leader = actor;
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
				((BaseSimObject)_a).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				Romance.SteadyForTest = true;
				Quests.Log.Clear();
				Fresh();
				yield return Part("who can be flirted with", Who());
				Fresh();
				yield return Part("flirting", Flirting());
				Fresh();
				yield return Part("asking to court", AskToCourt());
				Fresh();
				yield return Part("a token", Token());
				Fresh();
				yield return Part("a walk", Walk());
				Fresh();
				yield return Part("valour", Valor());
				Fresh();
				yield return Part("a blessing", Blessing());
				Fresh();
				yield return Part("a poem", Poem());
				Fresh();
				yield return Part("stargazing", Stars());
				Fresh();
				yield return Part("retired tasks (keepsake, fishing)", RetiredTasks());
				Fresh();
				yield return Part("a rival suitor", Rival());
				Fresh();
				yield return Part("a date at the fire", Fire());
				Fresh();
				yield return Part("will you be mine", Yes());
				yield return Part("taken", Taken());
				Romance.SteadyForTest = false;
				Romance.WelcomeForTest = -1;
				Romance.TodayForTest = -1;
				Quests.ForceCourtForTest = -1;
				Quests.NightForTest = false;
				Hud.MetersForTest = false;
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
			Pick(c, choice);
			return true;
		}

		private static void Pick(Conversation c, Choice ch)
		{
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(ch));
		}

		private IEnumerator Open(Actor npc)
		{
			Mod.EndConversation();
			_host.current_position = npc.current_position - new Vector2(1.3f, 0f);
			try
			{
				npc.makeWait(15f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(npc);
		}

		private void Set(Actor npc, int affection, int done = -1, int need = -1, bool asked = false)
		{
			Suitor suitor = Romance.Of(npc, _host);
			suitor.Affection = affection;
			if (done >= 0)
			{
				suitor.Done = done;
			}
			if (need >= 0)
			{
				suitor.Need = need;
			}
			suitor.Asked = asked || suitor.Asked;
			suitor.Stamp = 0.0;
			suitor.Last = -1;
			suitor.Same = 0;
			suitor.Day = -1;
			suitor.Gained = (suitor.Exchanges = 0);
			suitor.Gift = false;
			suitor.Best = affection;
			suitor.LastTalk = (suitor.CoolUntil = -1);
			Romance.Save(npc, suitor);
		}

		private Suitor Of(Actor npc)
		{
			return Romance.Of(npc, _host);
		}

		private void Fresh()
		{
			Actor[] array = new Actor[3] { _host, _a, _c };
			foreach (Actor actor in array)
			{
				try
				{
					if (actor.lover != null)
					{
						if (actor.lover.lover == actor)
						{
							actor.lover.setLover(null);
						}
						actor.setLover(null);
					}
				}
				catch (Exception ex)
				{
					Note("unlove: " + ex.Message);
				}
			}
		}

		private Quest Mine(QuestKind kind)
		{
			return Quests.Log.Find((Quest q) => q.Kind == kind && q.Giver == _a.data.id);
		}

		private IEnumerator TakeTask(QuestKind kind)
		{
			Fresh();
			Quests.ForceCourtForTest = (int)kind;
			Conversation c = Mod.Converse;
			yield return Open(_a);
			Say(c, "Flirt");
			if (Say(c, "How else can I win your heart?") || Say(c, "May I court you?"))
			{
				Say(c, "I'll do it.");
				Say(c, "Yes, that's right.");
				Mod.EndConversation();
				Quest quest = Mine(kind);
				Check($"took a {kind} task", quest != null, (quest != null) ? (Quests.Title(quest) + " / " + Quests.Status(quest)) : Labels(c));
			}
		}

		private void Toast(string s)
		{
			Note("toast: " + s);
		}

		private IEnumerator Who()
		{
			Check("a child can't be flirted with", _kid == null || Romance.Blocker(_host, _kid) == FlirtOutcome.TooYoung, (_kid == null) ? "no child" : Romance.Blocker(_host, _kid)?.ToString());
			Check("a single grown-up of your people can", !Romance.Blocker(_host, _a).HasValue && Dialogue.CanFlirt(_host, _a));
			try
			{
				_b.becomeLoversWith(_d);
			}
			catch (Exception ex)
			{
				Note("lovers: " + ex.Message);
			}
			Check("someone taken can be courted too (harder: RivalsTest)", !Romance.Blocker(_host, _b).HasValue, Romance.Blocker(_host, _b)?.ToString());
			Check("the game won't pair off the body you control", LoveGuard.Installed && !_a.canFallInLoveWith(_host) && !_host.canFallInLoveWith(_a));
			bool flag = false;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _host && item != _a && item.isAdult() && _a.canFallInLoveWith(item))
				{
					flag = true;
				}
			}
			Note("others can still fall for her: " + flag);
			yield break;
		}

		private IEnumerator Flirting()
		{
			Charm charm = Romance.Likes(_a);
			Charm charm2 = Romance.Dislikes(_a);
			Charm charm3 = Charm.Sweet;
			Charm[] array = new Charm[4]
			{
				Charm.Sweet,
				Charm.Bold,
				Charm.Witty,
				Charm.Deep
			};
			foreach (Charm charm4 in array)
			{
				if (charm4 != charm && charm4 != charm2)
				{
					charm3 = charm4;
					break;
				}
			}
			Note($"A likes {charm}, dislikes {charm2}; favourite {Romance.Favourite(_a)}, hopes for {Romance.HopeOf(_a)}, loves {Romance.FavouritePlace(_a)}; picky {Romance.Picky(_a)}, {Romance.Days(_a)} days, cap {Romance.CapFor(_a, _host)}, wants {Romance.TasksNeeded(_a)} tasks");
			Check("the picky take three days and three tasks, the rest two days", Romance.Days(_a) == (Romance.Picky(_a) ? 3 : 2) && Romance.TasksNeeded(_a) == 3 == Romance.Picky(_a));
			int num = (Romance.TodayForTest = 100);
			Set(_a, 0);
			Romance.Result result = Romance.Flirt(_host, _a, charm);
			Check("the way they like warms them", result.Outcome == FlirtOutcome.Warm && result.Gain >= 10, $"{result.Outcome} {result.Gain}: {result.Reply}");
			Set(_a, 0);
			Romance.Result result2 = Romance.Flirt(_host, _a, charm3);
			Check("another way warms them less", result2.Outcome == FlirtOutcome.Mild && result2.Gain > 0 && result2.Gain < result.Gain, $"{result2.Outcome} {result2.Gain}: {result2.Reply}");
			Set(_a, 0);
			Romance.Result result3 = Romance.Flirt(_host, _a, charm, personal: true);
			Check("a reply that shows you listened lands better", result3.Gain > result.Gain, $"{result3.Gain} vs {result.Gain}");
			Set(_a, 20);
			Romance.Result result4 = Romance.Flirt(_host, _a, charm2);
			Check("the way they dislike cools them", result4.Outcome == FlirtOutcome.Cool && result4.Gain <= 0, $"{result4.Outcome} {result4.Gain}: {result4.Reply}");
			Set(_a, 20);
			Romance.Result result5 = default(Romance.Result);
			for (int j = 0; j < 3; j++)
			{
				result5 = Romance.Flirt(_host, _a, charm3);
			}
			Check("the same way thrice in a day wears thin", result5.Outcome == FlirtOutcome.Bored && result5.Gain <= 2, $"{result5.Outcome} {result5.Gain}: {result5.Reply}");
			result5 = Romance.Flirt(_host, _a, charm);
			Check("three exchanges a day, then they've had enough", result5.Outcome == FlirtOutcome.Enough && result5.Gain == 0 && Romance.ExchangesLeft(_a, _host) == 0, $"{result5.Outcome}: {result5.Reply}");
			Set(_a, 0);
			int num2 = Romance.CapFor(_a, _host);
			for (int k = 0; k < 3; k++)
			{
				Romance.Flirt(_host, _a, charm, personal: true);
			}
			Check("a day's talk fills at most its share", Of(_a).Affection <= num2 && Of(_a).Affection >= num2 - 2, $"{Of(_a).Affection} of {num2}");
			num = (Romance.TodayForTest = num + 1);
			Romance.Result result6 = Romance.Flirt(_host, _a, charm);
			Check("the next day it fills further", result6.Gain > 0 && Of(_a).Affection > num2, $"{result6.Gain} -> {Of(_a).Affection}");
			Set(_a, 0);
			try
			{
				_host.addToInventory(Romance.Favourite(_a), 6);
			}
			catch (Exception ex)
			{
				Note("bag: " + ex.Message);
			}
			int num3 = 0;
			Romance.GiftTaste taste;
			bool capped;
			for (int l = 0; l < 6; l++)
			{
				if (Of(_a).Affection >= 100)
				{
					break;
				}
				num = (Romance.TodayForTest = num + 1);
				num3++;
				for (int m = 0; m < 3; m++)
				{
					Romance.Flirt(_host, _a, (Charm)(((int)charm + ((m == 1) ? 0 : 0)) % 4), personal: true);
				}
				Romance.Give(_host, _a, Romance.Favourite(_a), out taste, out capped);
			}
			Check("the meter fills in its days, no sooner", num3 == Romance.Days(_a), $"{num3} days, wanted {Romance.Days(_a)}");
			Set(_a, 0);
			num = (Romance.TodayForTest = num + 1);
			Romance.GiftTaste taste2;
			int? num4 = Romance.Give(_host, _a, Romance.Favourite(_a), out taste2, out capped);
			Check("the gift they love warms them most", num4 >= 20 && taste2 == Romance.GiftTaste.Loved, $"{num4} {taste2}");
			Check("one gift a day", !Romance.Give(_host, _a, Romance.Favourite(_a), out taste, out capped).HasValue);
			string text = Romance.Disliked(_a);
			if (text != null)
			{
				num = (Romance.TodayForTest = num + 1);
				try
				{
					_host.addToInventory(text, 1);
				}
				catch
				{
				}
				Check("the one they dislike cools them", Romance.Give(_host, _a, text, out taste, out capped) < 0);
			}
			Set(_a, 60);
			Suitor suitor = Of(_a);
			suitor.LastTalk = num;
			Romance.Save(_a, suitor);
			Romance.TodayForTest = num + 5;
			Romance.WelcomeForTest = 0;
			Romance.Meet(_a, _host);
			Check("five days apart fade the meter", Of(_a).Affection == 45, $"60 -> {Of(_a).Affection}");
			Set(_a, 20);
			suitor = Of(_a);
			suitor.Best = 90;
			suitor.LastTalk = num;
			Romance.Save(_a, suitor);
			Romance.TodayForTest = num + 40;
			Romance.Meet(_a, _host);
			Check("but never below a third of the best it's been", Of(_a).Affection == 20, $"20 -> {Of(_a).Affection}");
			suitor = Of(_a);
			suitor.LastTalk = num + 38;
			Romance.Save(_a, suitor);
			Romance.WelcomeForTest = 1;
			string text2 = Romance.Meet(_a, _host);
			Check("after days away they may be glad you're back", text2 != null && Of(_a).Affection > 20, $"{text2} -> {Of(_a).Affection}");
			Romance.WelcomeForTest = -1;
			num = (Romance.TodayForTest = num + 41);
			Set(_a, 50);
			Romance.Result result7 = Romance.Flirt(_host, _a, charm);
			Set(_a, 50);
			Romance.SetBack(_a, _host, 10);
			Check("a setback drops the meter and cools them a day", Of(_a).Affection == 40 && Romance.Cool(Of(_a)));
			Romance.Result result8 = Romance.Flirt(_host, _a, charm);
			Check("while cool, flirts land at half", result8.Gain > 0 && result8.Gain <= result7.Gain / 2 + 1, $"{result8.Gain} vs {result7.Gain}");
			Romance.TodayForTest = num + 2;
			Check("cool no more two days on", !Romance.Cool(Of(_a)));
			Set(_a, 95);
			Romance.Flirt(_host, _a, charm);
			Check("flirting alone never makes lovers", _a.lover == null && !Romance.WouldSayYes(_a, _host), $"affection {Of(_a).Affection}");
			Set(_a, 99);
			Romance.Result result9 = Romance.Flirt(_host, _a, charm);
			Check("the flirt that tips them says to ask properly", result9.NowReady && result9.Reply.Contains("ask"), result9.Reply);
			Check("what they tell you is kept", Romance.Tell(_a, _host, Fact.Hope) && !Romance.Tell(_a, _host, Fact.Hope) && Romance.KnowsFact(_a, _host, Fact.Hope));
			string pResult = null;
			try
			{
				_a.data.get("fp_court", out pResult, null);
			}
			catch
			{
			}
			Check("the courtship is kept in their saved data", pResult != null && pResult.StartsWith(_host.data.id + ":", StringComparison.Ordinal) && pResult.Split(':').Length >= 20, pResult);
			Romance.TodayForTest = -1;
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator AskToCourt()
		{
			Conversation c = Mod.Converse;
			Set(_a, 0);
			Suitor suitor = Of(_a);
			suitor.Known = 0;
			Romance.Save(_a, suitor);
			yield return Open(_a);
			Check("the talk is open", c.Open && c.With == _a);
			Check("Flirt is a topic", Say(c, "Flirt"), Labels(c));
			Note("flirt menu: " + Labels(c));
			Check("the menu: flirt, ask about them, a gift", Find(c, "Flirt  (3 left today)") != null && Find(c, "Ask about them") != null && Find(c, "I brought you something") != null, Labels(c));
			Check("no courting before they like you", Find(c, "May I court you?") == null);
			Check("you can ask about them", Say(c, "Ask about them"), Labels(c));
			Check("what they look for in a person", Say(c, "What do you look for in a person?"), Labels(c));
			Note("taste: " + AllPages(c));
			Check("they tell you, and it's kept", Of(_a).Knows && Find(c, "What do you look for") == null, Labels(c));
			Check("where they go to be alone", Say(c, "Where do you go to be alone?"), Labels(c));
			Note("place: " + AllPages(c));
			Say(c, "Let's talk about something else.");
			Say(c, "Flirt");
			Check("a flirt is an exchange", Say(c, "Flirt  ("), Labels(c));
			Note("they say: " + AllPages(c) + "   replies: " + Labels(c));
			int count = c.Choices.FindAll((Choice ch) => ch.Tone == ChoiceTone.Romance).Count;
			Check("three replies to what they said, each its way named", count == 3 && c.Choices.FindAll((Choice ch) => ch.Label.Contains("   · ")).Count == 3, Labels(c));
			Hud.MetersForTest = true;
			c.LineStart -= 999f;
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "romance_exchange.png"));
			yield return CivilTest.Wait(0.4f);
			Hud.MetersForTest = false;
			int said = c.Said;
			Pick(c, c.Choices[0]);
			Check("a reply gets an answer", c.Said > said, AllPages(c));
			Note("answer: " + AllPages(c) + "   header: " + c.Relation);
			Check("and one less today", Find(c, "Flirt  (2 left today)") != null, Labels(c));
			Set(_a, 105, 0, 12);
			Say(c, "Let's talk about something else.");
			Say(c, "Flirt");
			Check("now you may ask to court them", Find(c, "May I court you?") != null, Labels(c));
			Check("the header says so", c.Relation != null && c.Relation.Contains("court"), c.Relation);
			c.LineStart -= 999f;
			Hud.MetersForTest = true;
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "romance_flirt.png"));
			yield return CivilTest.Wait(0.4f);
			Hud.MetersForTest = false;
			_rival = null;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != _host && simple != _a && simple.isAlive() && _a.canFallInLoveWith(simple))
				{
					_rival = simple;
					break;
				}
			}
			Note("rival: " + ((_rival != null) ? Dialogue.NameOf(_rival) : "none who'd suit"));
			Quests.ForceCourtForTest = 10;
			Say(c, "May I court you?");
			string text = AllPages(c);
			Note("brief: " + text);
			Check("they set a task and say it plainly", text.Contains("To be clear"), text);
			Say(c, "I'll do it.");
			string text2 = AllPages(c);
			Check("the summing up promises heart, not gold", text2.Contains("think a little more of you") && !text2.Contains(" gold"), text2);
			Say(c, "Yes, that's right.");
			Mod.EndConversation();
			Quest quest = Mine(QuestKind.CourtGift);
			Check("a courting task in your log", quest != null && quest.Money == 0, (quest != null) ? Quests.Title(quest) : "none");
			Check("it's titled as courting", quest != null && Quests.Title(quest).StartsWith("Courting ", StringComparison.Ordinal), (quest != null) ? Quests.Title(quest) : "");
			Check("someone you court waits for you (the game won't pair them off)", Quests.SpokenFor(_a) && (_rival == null || (!_a.canFallInLoveWith(_rival) && !_rival.canFallInLoveWith(_a))), (_rival != null) ? Dialogue.NameOf(_rival) : "no rival");
		}

		private void Craft(string id)
		{
			Gear.Piece p = new Gear.Piece
			{
				Id = id,
				By = Dialogue.NameOf(_host),
				Name = id.Replace('_', ' ')
			};
			Check("a " + id + " in your bag", Gear.Add(_host, p), Gear.Count(_host).ToString());
		}

		private IEnumerator Token()
		{
			Quest q = Mine(QuestKind.CourtGift);
			if (Check("the made-gift task", q != null && q.Thing != null && q.Thing.EndsWith("_"), q?.Thing))
			{
				Note("gift: " + Quests.Title(q) + " / " + Quests.Status(q));
				Craft((q.Thing == "sword_") ? "sword_iron" : "amulet_silver");
				yield return CivilTest.Wait(1.2f);
				Check("with it in your bag it's ready", q.Have == 1, Quests.Status(q));
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "romance_task.png"));
				yield return CivilTest.Wait(0.4f);
				int before = Of(_a).Affection;
				Conversation c = Mod.Converse;
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				Check("you can give it", Say(c, "For you:"), Labels(c));
				Note("thanks: " + AllPages(c));
				Mod.EndConversation();
				Suitor suitor = Of(_a);
				Check("the task is done", Mine(QuestKind.CourtGift) == null && suitor.Done == 1, $"done {suitor.Done}");
				Check("and it won them over a little more", suitor.Affection == Mathf.Min(100, before + 20), $"{before} -> {suitor.Affection}");
				Check("a task done, they still wait a while", Quests.SpokenFor(_a));
				Check("the piece left your bag", Quests.BagPieceLike(_host, q.Thing) < 0);
				Item item = null;
				try
				{
					item = _a.equipment.getSlot((!(q.Thing == "sword_")) ? EquipmentType.Amulet : EquipmentType.Weapon).getItem();
				}
				catch
				{
				}
				Check("and they wear it", item != null && item.asset != null && item.asset.id.StartsWith(q.Thing), item?.asset?.id);
			}
		}

		private IEnumerator Walk()
		{
			yield return TakeTask(QuestKind.CourtWalk);
			Quest q = Mine(QuestKind.CourtWalk);
			if (q == null)
			{
				yield break;
			}
			Check("a spot to walk to", q.X >= 0 && q.Place != null, q.Place);
			Vector2 spot = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
			Note($"walk to {q.Place} at {q.X},{q.Y}, {(spot - _a.current_position).magnitude:F0} tiles off");
			bool flag = false;
			for (float t = 0f; t < 14f; t += 0.25f)
			{
				if (flag)
				{
					break;
				}
				_host.current_position = spot;
				if ((_a.current_position - spot).magnitude > 3f)
				{
					_a.current_position = spot + new Vector2(1f, 0f);
				}
				yield return CivilTest.Wait(0.25f);
				flag = Mine(QuestKind.CourtWalk) == null;
			}
			Check("staying there together does it", flag && Of(_a).Done == 2, $"done {Of(_a).Done}, have {q.Have}");
			Check("they say something there", Mod.Talk.Speaker == _a, Mod.Talk.Line);
		}

		private IEnumerator Valor()
		{
			Actor wolf = CivilTest.SpawnNear("wolf", _a.current_position + new Vector2(12f, 0f));
			if (!Check("a wolf near town", wolf != null))
			{
				yield break;
			}
			try
			{
				wolf.makeWait(60f);
			}
			catch
			{
			}
			yield return TakeTask(QuestKind.CourtValor);
			Quest q = Mine(QuestKind.CourtValor);
			if (q != null)
			{
				Note("valour: " + Quests.Title(q));
				_host.current_position = wolf.current_position - new Vector2(2f, 0f);
				_a.current_position = wolf.current_position - new Vector2(12f, 0f);
				yield return CivilTest.Wait(1.2f);
				Check("you close with it, and they stop to watch", q.Have == 1, $"have {q.Have}, {(_a.current_position - wolf.current_position).magnitude:F1} tiles off");
				Quests.OnKill(wolf);
				CivilTest.Kill(wolf);
				yield return CivilTest.Wait(1.2f);
				Check("killed by you as they watched, it's ready", q.Stage == 1 && q.Need >= 80, $"{Quests.Status(q)}, score {q.Need}");
				Conversation c = Mod.Converse;
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				Note("thanks: " + AllPages(c));
				Mod.EndConversation();
				Check("valour shown", Mine(QuestKind.CourtValor) == null && Of(_a).Done == 3, $"done {Of(_a).Done}");
				Actor wolf2 = CivilTest.SpawnNear("wolf", _a.current_position + new Vector2(-12f, 0f));
				try
				{
					wolf2.makeWait(60f);
				}
				catch
				{
				}
				yield return TakeTask(QuestKind.CourtValor);
				q = Mine(QuestKind.CourtValor);
				if (q != null)
				{
					int aff = Of(_a).Affection;
					int failed = Quests.Failed;
					CivilTest.Kill(wolf2);
					yield return CivilTest.Wait(1.2f);
					Check("killed by someone else, it fails", Mine(QuestKind.CourtValor) == null && Quests.Failed == failed + 1);
					Check("and cools them a little, for a day", Of(_a).Affection == Mathf.Max(0, aff - 10) && Romance.Cool(Of(_a)), $"{aff} -> {Of(_a).Affection}");
					Check("a task failed, they're free to fall for others", !Quests.SpokenFor(_a));
				}
			}
		}

		private IEnumerator Blessing()
		{
			yield return TakeTask(QuestKind.CourtBlessing);
			Quest q = Mine(QuestKind.CourtBlessing);
			if (q == null)
			{
				yield break;
			}
			Actor elder = Chronicle.Find(q.Target);
			Check("whose blessing: a living elder", elder != null && elder != _a && elder != _host, q.Thing + " " + q.TargetName);
			if (elder != null)
			{
				Chronicle.RegardOf(elder, _host).Opinion = -40;
				Chronicle.Flush(elder);
				Conversation c = Mod.Converse;
				try
				{
					Note($"the elder: person {Dialogue.IsPerson(elder)}, baby {elder.isBaby()}, alive {elder.isAlive()}, {elder.asset?.id}, inside {elder.isInsideSomething()}, id {elder.data.id}/{q.Target}");
				}
				catch (Exception ex)
				{
					Note("the elder: " + ex.Message);
				}
				yield return Open(elder);
				Note(string.Format("talk open {0} with {1}", c.Open, (c.With != null) ? Dialogue.NameOf(c.With) : "nobody"));
				Check("you can ask them", Say(c, "I wish to court "), Labels(c));
				Note("refused: " + AllPages(c));
				Check("someone who dislikes you refuses", q.Stage == 0 && AllPages(c).Contains("trust"), AllPages(c));
				Chronicle.RegardOf(elder, _host).Opinion = -5;
				Chronicle.Flush(elder);
				try
				{
					_host.addMoney(300);
				}
				catch
				{
				}
				int num = CivilTest.Money(_host);
				Check("ask again", Say(c, "I wish to court "), Labels(c));
				Note("terms: " + AllPages(c));
				Check("the doubtful want proof you can provide", AllPages(c).Contains("gold"));
				Check("you pay", Say(c, "Here:"), Labels(c));
				Check("and have the blessing", q.Stage == 1 && CivilTest.Money(_host) < num, $"{num} -> {CivilTest.Money(_host)}");
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				Note("thanks: " + AllPages(c));
				Mod.EndConversation();
				Check("blessing told", Mine(QuestKind.CourtBlessing) == null && Of(_a).Done == 4, $"done {Of(_a).Done}");
			}
		}

		private Choice Line(Conversation c, Quest q, int round, bool best)
		{
			Charm charm = Romance.Likes(_a);
			Charm charm2 = Romance.Dislikes(_a);
			Choice result = null;
			int num = (best ? int.MinValue : int.MaxValue);
			foreach (Choice choice in c.Choices)
			{
				for (int i = 0; i < 4; i++)
				{
					string[] array = Quests.Verses[round][i];
					foreach (string text in array)
					{
						if (!(Quests.Fill(text, q, _a) != choice.Label))
						{
							int num2 = ((i == (int)charm) ? 2 : ((i == (int)charm2) ? (-1) : 0)) + (text.Contains("{") ? 1 : 0);
							if (best ? (num2 > num) : (num2 < num))
							{
								num = num2;
								result = choice;
							}
						}
					}
				}
			}
			return result;
		}

		private IEnumerator Recite(Quest q, bool best)
		{
			Conversation c = Mod.Converse;
			yield return Open(_a);
			Say(c, "About what you asked of me...");
			if (!Say(c, "I wrote you something."))
			{
				yield break;
			}
			for (int round = 0; round < 3; round++)
			{
				Choice ch = Line(c, q, round, best);
				if (ch == null)
				{
					Note($"(round {round}: no known line in {Labels(c)})");
					yield break;
				}
				if (best && round == 1)
				{
					Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "romance_poem.png"));
					yield return CivilTest.Wait(0.4f);
				}
				Pick(c, ch);
			}
			Note($"verdict ({Quests.LastVerseScore}): " + AllPages(c));
		}

		private IEnumerator Poem()
		{
			yield return TakeTask(QuestKind.CourtVerse);
			Quest q = Mine(QuestKind.CourtVerse);
			if (q != null)
			{
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "romance_courting.png"));
				yield return CivilTest.Wait(0.4f);
				Conversation c = Mod.Converse;
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				Check("no poem before you've seen the place", Find(c, "I wrote you something.") == null, Labels(c));
				Mod.EndConversation();
				_host.current_position = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
				yield return CivilTest.Wait(1.2f);
				Check("seeing the place readies the poem", q.Stage == 1, Quests.Status(q));
				yield return Recite(q, best: false);
				Check("a poem not in their taste falls flat", Quests.LastVerseScore < 5 && Mine(QuestKind.CourtVerse) != null, Quests.LastVerseScore.ToString());
				Quest quest = Quests.Read(_a.data);
				Check("and they want time before another (kept in their data)", quest != null && quest.Wait > Quests.Now, (quest != null) ? quest.Wait.ToString("F1") : "none");
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				Choice choice = Find(c, "I wrote you something.");
				Check("you can't try again at once", choice != null && !choice.Enabled, choice?.Note);
				Mod.EndConversation();
				q.Wait = 0.0;
				yield return Recite(q, best: true);
				Mod.EndConversation();
				Check("a poem in their taste wins them", Quests.LastVerseScore >= 5 && Mine(QuestKind.CourtVerse) == null && Of(_a).Done == 5, $"score {Quests.LastVerseScore}, done {Of(_a).Done}");
			}
		}

		private IEnumerator Together(Vector2 spot, float seconds, Func<bool> done)
		{
			for (float t = 0f; t < seconds; t += 0.25f)
			{
				if (done())
				{
					break;
				}
				_host.current_position = spot;
				if ((_a.current_position - spot).magnitude > 3f)
				{
					_a.current_position = spot + new Vector2(1f, 0f);
				}
				yield return CivilTest.Wait(0.25f);
			}
		}

		private IEnumerator Stars()
		{
			yield return TakeTask(QuestKind.CourtStars);
			Quest q = Mine(QuestKind.CourtStars);
			if (q != null)
			{
				int done = Of(_a).Done;
				Note($"stars from {q.Place} at {q.X},{q.Y}");
				Quests.NightForTest = false;
				yield return CivilTest.Wait(1f);
				Note("by day: " + Quests.Status(q));
				Quests.NightForTest = true;
				bool shower = false;
				yield return Together(new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f), 35f, delegate
				{
					shower |= Mod.Renderer.ForceShower > 0.5f;
					return Mine(QuestKind.CourtStars) == null;
				});
				Check("stars fall while you watch together", shower);
				Check("and the night does it", Mine(QuestKind.CourtStars) == null && Of(_a).Done == done + 1, $"done {Of(_a).Done}, have {q.Have}");
				Check("they say something there", Mod.Talk.Speaker == _a, Mod.Talk.Line);
				yield return CivilTest.Wait(2f);
				Check("the shower ends with it", Mod.Renderer.ForceShower < 0f, Mod.Renderer.ForceShower.ToString());
				Quests.NightForTest = false;
			}
		}

		private IEnumerator RetiredTasks()
		{
			Check("keepsake and fishing are never set", Quests.Retired(QuestKind.CourtKeepsake) && Quests.Retired(QuestKind.CourtFishing) && !Quests.Retired(QuestKind.CourtStars));
			yield break;
		}

		private IEnumerator Rival()
		{
			yield return TakeTask(QuestKind.CourtRival);
			Quest q = Mine(QuestKind.CourtRival);
			if (q == null)
			{
				Note("(no rival in town: not set)");
				yield break;
			}
			int done = Of(_a).Done;
			Actor rival = Chronicle.Find(q.Target);
			if (Check("a rival of their town", rival != null && rival != _host && rival != _a, q.TargetName))
			{
				try
				{
					((BaseSimObject)rival).addStatusEffect("invincible", 0f, pColorEffect: false);
					rival.finishStatusEffect("invincible");
				}
				catch
				{
				}
				Conversation c = Mod.Converse;
				yield return Open(rival);
				Check("you can tell them to stay away", Say(c, "Stay away from "), Labels(c));
				Note("rival: " + AllPages(c));
				Check("and settle it with fists", Say(c, "Then we settle it."), Labels(c));
				yield return CivilTest.Wait(1f);
				Check("a brawl, not a killing", Rivals.Brawling(rival));
				try
				{
					rival.data.health = Mathf.Max(1, (int)((float)rival.getMaxHealth() * 0.2f));
				}
				catch (Exception ex)
				{
					Note("health: " + ex.Message);
				}
				yield return CivilTest.Wait(1.5f);
				Check("beaten, they back off", q.Stage == 1 && rival.isAlive(), Quests.Status(q));
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				Note("thanks: " + AllPages(c));
				Mod.EndConversation();
				Check("the rival seen off", Mine(QuestKind.CourtRival) == null && Of(_a).Done == done + 1, $"done {Of(_a).Done}");
			}
		}

		private IEnumerator Fire()
		{
			Building fire = AddHouse("bonfire", _a.city);
			if (!Check("a bonfire in town", fire != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(2.5f);
			RomanceTest romanceTest = this;
			Vector2 fire2 = (Vector2)fire.current_tile.posV3 - _a.current_position;
			romanceTest.Note($"the fire {fire2.magnitude:F0} tiles from them, found {Seats.FireNear(_a.current_position, 40f, out fire2)}");
			yield return TakeTask(QuestKind.CourtFire);
			Quest q = Mine(QuestKind.CourtFire);
			if (q == null)
			{
				yield break;
			}
			int done = Of(_a).Done;
			Quests.NightForTest = true;
			yield return Together(new Vector2((float)q.X + 1.5f, (float)q.Y + 0.5f), 6f, () => (Mine(QuestKind.CourtFire)?.Stage ?? (-1)) == 1);
			q = Mine(QuestKind.CourtFire) ?? q;
			Check("at the fire in the evening, they wait for you", q.Stage == 1, Quests.Status(q));
			for (int pass = 0; pass < 2; pass++)
			{
				bool right = pass == 1;
				Conversation c = Mod.Converse;
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				if (!Check(right ? "sit with them again" : "sit with them", Say(c, "*sits beside them*"), Labels(c)))
				{
					break;
				}
				for (int num = 0; num < 3; num++)
				{
					Find(c, "");
					string text = AllPages(c);
					Choice choice = (right ? FireAnswer(c, text) : WrongFireAnswer(c, text));
					if (choice == null)
					{
						Note($"(round {num}: no answer known for {text}: {Labels(c)})");
						break;
					}
					Pick(c, choice);
				}
				Note($"fire ({Quests.LastFireScore}): " + AllPages(c));
				Mod.EndConversation();
				if (!right)
				{
					Check("wrong answers spoil the evening", Quests.LastFireScore < 2 && Mine(QuestKind.CourtFire) == null && Romance.Cool(Of(_a)), Quests.LastFireScore.ToString());
					yield return TakeTask(QuestKind.CourtFire);
					q = Mine(QuestKind.CourtFire);
					if (q == null)
					{
						yield break;
					}
					yield return Together(new Vector2((float)q.X + 1.5f, (float)q.Y + 0.5f), 6f, () => (Mine(QuestKind.CourtFire)?.Stage ?? (-1)) == 1);
				}
			}
			Check("knowing them wins the evening", Quests.LastFireScore >= 2 && Mine(QuestKind.CourtFire) == null && Of(_a).Done == done + 1, $"score {Quests.LastFireScore}, done {Of(_a).Done}");
			Quests.NightForTest = false;
		}

		private Choice FireAnswer(Conversation c, string question)
		{
			string want;
			if (question.Contains("want most"))
			{
				want = Dialogue.HopeWordsOf(Romance.HopeOf(_a));
			}
			else if (question.Contains("look for me"))
			{
				want = Romance.PlaceWords(Romance.FavouritePlace(_a));
			}
			else if (question.Contains("bring me"))
			{
				want = Work.ResourceName(Romance.Favourite(_a)).ToLowerInvariant();
			}
			else
			{
				int num = Dialogue.SafeHappiness(_a);
				want = ((num > 30) ? "Happy" : ((num < -10) ? "Troubled" : "Tired"));
			}
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(char.ToUpperInvariant(want[0]) + want.Substring(1), StringComparison.Ordinal));
		}

		private Choice WrongFireAnswer(Conversation c, string question)
		{
			Choice right = FireAnswer(c, question);
			return c.Choices.Find((Choice ch) => ch != right && ch.Tone == ChoiceTone.Romance);
		}

		private IEnumerator Yes()
		{
			Fresh();
			Suitor suitor = Of(_a);
			Check("not yet theirs while they want more", !Romance.WouldSayYes(_a, _host), $"{suitor.Done}/{suitor.Need}");
			suitor.Need = suitor.Done;
			suitor.Extra = 0;
			Romance.Save(_a, suitor);
			Conversation c = Mod.Converse;
			yield return Open(_a);
			Note("topics: " + Labels(c));
			Check("you can ask them", Say(c, "Will you be mine?"), Labels(c));
			Note("ask properly: " + AllPages(c));
			Mod.EndConversation();
			Quest q = Mine(QuestKind.CourtProposal);
			Check("they want it asked properly: a ring, at their place", q != null && q.X >= 0 && _a.lover == null, (q != null) ? (Quests.Title(q) + " / " + Quests.Status(q)) : "no proposal");
			if (q != null)
			{
				yield return CivilTest.Wait(1f);
				Check("no ring yet", q.Have == 0 && Quests.Status(q).Contains("ring"), Quests.Status(q));
				Craft("ring_silver");
				Vector2 spot = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
				yield return Together(spot, 6f, () => (Mine(QuestKind.CourtProposal)?.Stage ?? (-1)) == 1);
				q = Mine(QuestKind.CourtProposal) ?? q;
				Check("with a ring, at their place, they wait for you to ask", q.Stage == 1, Quests.Status(q));
				Note($"{Quests.CrowdForTest} of their people gather");
				yield return CivilTest.Wait(2f);
				_a.current_position = spot + new Vector2(1f, 0f);
				yield return Together(spot, 3f, () => (Mine(QuestKind.CourtProposal)?.Stage ?? (-1)) == 1);
				yield return Open(_a);
				Say(c, "About what you asked of me...");
				Check("you kneel and ask", Say(c, "*kneels* Will you be mine?"), Labels(c));
				Note("yes: " + AllPages(c));
				yield return CivilTest.Wait(0.5f);
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "romance_proposal.png"));
				yield return CivilTest.Wait(0.4f);
				Check("lovers, through the game", _a.lover == _host && _host.lover == _a);
				Check("the header says so", c.Relation == "your sweetheart", c.Relation);
				Item item = null;
				try
				{
					item = _a.equipment.getSlot(EquipmentType.Ring).getItem();
				}
				catch
				{
				}
				Check("they wear your ring", item != null && item.asset != null && item.asset.id == "ring_silver", item?.asset?.id);
				Check("the proposal is done", Mine(QuestKind.CourtProposal) == null);
				Check("and the talk moves on from it", Find(c, "About what you asked") == null, Labels(c));
				string pResult = null;
				try
				{
					_a.data.get("fp_court", out pResult, null);
				}
				catch
				{
				}
				Check("the courtship is put away", string.IsNullOrEmpty(pResult) || !pResult.Contains(_host.data.id + ":"), pResult);
				Check("they remember being won", Chronicle.Of(_a)?.Memories.Exists((Memory m) => m.Kind == "fp_courted") ?? false);
				Mod.EndConversation();
				yield return CivilTest.Wait(0.2f);
			}
		}

		private IEnumerator Taken()
		{
			Check("now you're taken", Romance.Blocker(_host, _c) == FlirtOutcome.YouAreTaken, Romance.Blocker(_host, _c)?.ToString());
			string text = Dialogue.Flirt(_c, _host, FlirtOutcome.YouAreTaken);
			Check("and they say so", text.Contains("Shouldn't you be with"), text);
			yield break;
		}
	}
}
