using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RivalsTest : CivilTest
	{
		private Actor _host;

		private Actor _a;

		private Actor _b;

		private Actor _w;

		private Actor _e;

		private Actor _king;

		protected override string Prefix => "RIVALS";

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
			if (!Check("enough grown-ups", list.Count >= 5, list.Count.ToString()))
			{
				yield break;
			}
			_host = list[0];
			_a = list[1];
			_b = list[2];
			_w = list[3];
			_e = list[4];
			_king = actor;
			CivilTest.SetSex(_host, female: false);
			CivilTest.SetSex(_b, female: false);
			CivilTest.SetSex(_w, female: false);
			CivilTest.SetSex(_a, female: true);
			CivilTest.SetSex(_e, female: true);
			foreach (Actor item2 in Founded)
			{
				try
				{
					item2.data.removeString("fp_court");
					item2.data.removeString("fp_jealous");
				}
				catch
				{
				}
			}
			Unlove(_host, _a, _b, _e, _king);
			try
			{
				_a.becomeLoversWith(_b);
			}
			catch (Exception ex)
			{
				Note("lovers: " + ex.Message);
			}
			if (Check("A and B are a couple", _a.lover == _b && _b.lover == _a))
			{
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
					Quests.ProposalForTest = true;
					Quests.Log.Clear();
					yield return Part("harder when taken", Harder());
					yield return Part("the partner's warning", Warning());
					yield return Part("the brawl", Brawl());
					yield return Part("the yes", Yes());
					yield return Part("ending it", EndingIt());
					yield return Part("a ruler's partner", Ruler());
					Romance.SteadyForTest = false;
					Quests.ProposalForTest = false;
					Rivals.End();
				}
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

		private bool FlirtLine(Conversation c)
		{
			if (c.With != null)
			{
				Suitor suitor = Romance.Of(c.With, _host);
				suitor.Exchanges = 0;
				Romance.Save(c.With, suitor);
			}
			if (!Say(c, "Flirt"))
			{
				return false;
			}
			if (!Say(c, "Flirt  ("))
			{
				return false;
			}
			Choice choice = c.Choices.Find((Choice ch) => ch.Tone == ChoiceTone.Romance);
			if (choice == null)
			{
				Note("(no flirt line: " + Labels(c) + ")");
				return false;
			}
			Pick(c, choice);
			return true;
		}

		private static void Put(Actor a, Vector2 at)
		{
			try
			{
				a.exitBuilding();
			}
			catch
			{
			}
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile != null)
			{
				try
				{
					a.spawnOn(tile, 0f);
				}
				catch
				{
				}
			}
			a.current_position = at;
			a.dirty_current_tile = true;
			try
			{
				a.cancelAllBeh();
				a.makeWait(20f);
			}
			catch
			{
			}
		}

		private void Away(Actor a)
		{
			Put(a, _a.current_position + new Vector2(40f, 0f));
		}

		private void Unlove(params Actor[] who)
		{
			foreach (Actor actor in who)
			{
				try
				{
					if (actor != null && actor.lover != null)
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

		private void Set(Actor npc, int affection, int done = -1)
		{
			Suitor suitor = Romance.Of(npc, _host);
			suitor.Affection = affection;
			if (done >= 0)
			{
				suitor.Done = done;
			}
			suitor.Stamp = 0.0;
			suitor.Last = -1;
			suitor.Same = 0;
			suitor.Day = -1;
			suitor.Gained = (suitor.Exchanges = 0);
			Romance.Save(npc, suitor);
		}

		private static bool Remembers(Actor a, string kind)
		{
			return Chronicle.Of(a)?.Memories.Exists((Memory m) => m.Kind == kind) ?? false;
		}

		private IEnumerator Harder()
		{
			Away(_b);
			Away(_w);
			yield return CivilTest.Wait(0.3f);
			Check("someone taken can be flirted with", !Romance.Blocker(_host, _a).HasValue, Romance.Blocker(_host, _a)?.ToString());
			int num = Romance.Devotion(_a);
			Suitor suitor = Romance.Of(_a, _host);
			Note($"A's devotion to B {num}; wants {suitor.Need} + {suitor.Extra}; a day's cap {Romance.CapFor(_a, _host)}");
			Check("they want more tasks while taken", suitor.Rival == _b.data.id && suitor.Extra == Romance.ExtraTasks(num) && suitor.Wanted == suitor.Need + suitor.Extra, $"{suitor.Need}+{suitor.Extra}, rival {suitor.Rival}");
			int num2 = Mathf.Max(10, Mathf.RoundToInt(Mathf.Ceil(100f / (float)Romance.Days(_a))));
			Check("and their meter takes more days", (num == 0) ? (Romance.CapFor(_a, _host) == num2) : (Romance.CapFor(_a, _host) < num2), $"cap {Romance.CapFor(_a, _host)} of {num2}");
			Charm charm = Romance.Likes(_a);
			Set(_a, 0);
			Romance.Result result = Romance.Flirt(_host, _a, charm);
			string text = Romance.Standing(_a, _host);
			Check("once flirted with, the header says who they're with", text?.Contains(Dialogue.NameOf(_b)) ?? false, text);
			Unlove(_a);
			Set(_a, 0);
			Romance.Result result2 = Romance.Flirt(_host, _a, charm);
			try
			{
				_a.becomeLoversWith(_b);
			}
			catch
			{
			}
			Check("a flirt lands weaker on someone taken", result.Gain < result2.Gain || (num == 0 && result.Gain == result2.Gain), $"taken {result.Gain}, single {result2.Gain}");
			int num3 = 0;
			int num4 = 0;
			try
			{
				_a.setHappiness(_a.getMaxHappiness());
				num3 = Romance.Devotion(_a);
				_a.setHappiness(_a.getMinHappiness());
				num4 = Romance.Devotion(_a);
				_a.setHappiness(0);
			}
			catch (Exception ex)
			{
				Note("mood: " + ex.Message);
			}
			Check("an unhappy one is easier to win", num4 < num3 && Romance.ExtraTasks(num4) <= Romance.ExtraTasks(num3), $"happy {num3}, unhappy {num4}");
			Check("nobody saw, nobody comes", Rivals.ComingForTest == 0L && Rivals.BrawlForTest == 0);
			Set(_a, 0);
		}

		private IEnumerator Warning()
		{
			Rivals.End();
			Rivals.ForgetGossipForTest();
			Conversation c = Mod.Converse;
			Put(_b, _a.current_position + new Vector2(3.5f, 0f));
			Put(_w, _a.current_position + new Vector2(0f, 4f));
			City town = _a.city;
			int rep = Rep.Points(_host, town);
			yield return Open(_a);
			Check("a flirt said", FlirtLine(c), Labels(c));
			Check("the partner saw", Rivals.ComingForTest == _b.data.id || (c.Open && c.With == _b), Rivals.ComingForTest.ToString());
			Check("others saw it: the town thinks less of you", Rep.Points(_host, town) <= rep - 25, $"{rep} -> {Rep.Points(_host, town)}");
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (c.Open && c.With == _b)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("the partner cuts in and warns you", c.Open && c.With == _b && AllPages(c).Contains(Dialogue.NameOf(_a)), c.Open ? (Dialogue.NameOf(c.With) + ": " + AllPages(c)) : "no talk");
			Check("the warning is kept on them", Rivals.Warned(_b, _host) == 1);
			c.LineStart -= 999f;
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "rivals_warning.png"));
			yield return CivilTest.Wait(0.4f);
			Note("choices: " + Labels(c));
			Check("you can say sorry", Say(c, "I'm sorry."), Labels(c));
			Note("they say: " + AllPages(c));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.5f);
			Check("sorry: no fight", Rivals.BrawlForTest == 0);
		}

		private IEnumerator Brawl()
		{
			Conversation c = Mod.Converse;
			try
			{
				_host.finishStatusEffect("invincible");
				_host.data.health = _host.getMaxHealth();
			}
			catch
			{
			}
			Put(_b, _a.current_position + new Vector2(3.5f, 0f));
			yield return Open(_a);
			FlirtLine(c);
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (Rivals.BrawlForTest == _b.data.id)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("seen again, the partner comes at you", Rivals.BrawlForTest == _b.data.id, Rivals.LastForTest);
			Check("with their fists", Rivals.BareHanded(_b));
			Check("the talk is over", !c.Open);
			Check("it's no crime to hit back", Quests.Sparring(_b));
			yield return CivilTest.Wait(1.2f);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "rivals_brawl.png"));
			yield return CivilTest.Wait(0.4f);
			int max = Mathf.Max(1, _host.getMaxHealth());
			int full = _host.getHealth();
			yield return CivilTest.Wait(8f);
			Note($"8 s of brawling: {Rivals.TheirBlowsForTest} blows from them, health {full} -> {_host.getHealth()} of {max}");
			try
			{
				_host.data.health = Mathf.Max(2, Mathf.RoundToInt((float)max * 0.36f));
			}
			catch
			{
			}
			int lowest = _host.getHealth();
			for (float t = 0f; t < 25f; t += 0.2f)
			{
				if (Rivals.BrawlForTest == 0L)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
				try
				{
					lowest = Mathf.Min(lowest, _host.getHealth());
				}
				catch
				{
				}
			}
			Note($"your health fell to {lowest} of {max}");
			Check("beaten, not killed", _host.isAlive() && lowest >= 1 && InControl(_host));
			Check("the brawl ends when you're beaten", Rivals.BrawlForTest == 0L && Rivals.LastForTest == "brawl lost", Rivals.LastForTest);
			Check("no bounty for it", Law.BountyOn(_b.kingdom, _host) == 0);
			Check("they stop fighting", !_b.has_attack_target || _b.attack_target != _host);
			try
			{
				_host.data.health = max;
				((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			Put(_b, _a.current_position + new Vector2(3.5f, 0f));
			_host.current_position = _a.current_position - new Vector2(1.3f, 0f);
			yield return CivilTest.Wait(0.3f);
			Rivals.Flirted(_host, _a);
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (Rivals.BrawlForTest == _b.data.id)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("a third time, another brawl", Rivals.BrawlForTest == _b.data.id, Rivals.LastForTest);
			try
			{
				_b.data.health = Mathf.Max(1, Mathf.RoundToInt((float)_b.getMaxHealth() * 0.2f));
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.6f);
			Check("their partner beaten backs off", Rivals.BrawlForTest == 0L && Rivals.LastForTest == "brawl won", Rivals.LastForTest);
			try
			{
				_b.data.health = _b.getMaxHealth();
			}
			catch
			{
			}
		}

		private IEnumerator Yes()
		{
			Rivals.End();
			Away(_b);
			Away(_w);
			Conversation c = Mod.Converse;
			Suitor suitor = Romance.Of(_a, _host);
			Set(_a, 80, suitor.Wanted);
			Check("their yes waits on the extra tasks too", Romance.WouldSayYes(_a, _host), $"{Romance.Of(_a, _host).Done}/{Romance.Of(_a, _host).Wanted}");
			yield return Open(_a);
			Say(c, "Flirt");
			Check("you can ask them", Say(c, "Will you be mine?"), Labels(c));
			Note("yes: " + AllPages(c));
			Check("lovers, through the game", _a.lover == _host && _host.lover == _a);
			Check("they left their partner", _b.lover == null, (_b.lover != null) ? Dialogue.NameOf(_b.lover) : null);
			Check("they say so", AllPages(c).Contains(Dialogue.NameOf(_b)), AllPages(c));
			Check("the one left holds it against you", Chronicle.Opinion(_b, _host) <= -40, Chronicle.Opinion(_b, _host).ToString());
			Check("both remember it", Remembers(_b, "fp_jilted") && Remembers(_a, "fp_left"));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator EndingIt()
		{
			Conversation c = Mod.Converse;
			int before = Chronicle.Opinion(_a, _host);
			yield return Open(_a);
			Check("with your partner: it's over between us", Say(c, "It's over between us"), Labels(c));
			Note("they ask: " + AllPages(c));
			Check("you mean it", Say(c, "It's over. I'm sorry."), Labels(c));
			Note("they say: " + AllPages(c));
			Check("free again, both of you", _a.lover == null && _host.lover == null);
			Check("heartbroken, they think far less of you", Chronicle.Opinion(_a, _host) <= before - 40, $"{before} -> {Chronicle.Opinion(_a, _host)}");
			Check("they remember it", Remembers(_a, "fp_ended"));
			Check("the talk ends there", c.EndAfter);
			Mod.EndConversation();
			Check("now you may court someone else", !Romance.Blocker(_host, _e).HasValue, Romance.Blocker(_host, _e)?.ToString());
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator Ruler()
		{
			Unlove(_king, _e);
			try
			{
				_king.becomeLoversWith(_e);
			}
			catch (Exception ex)
			{
				Note("lovers: " + ex.Message);
			}
			bool flag = false;
			try
			{
				flag = _king.isKing() || _king.isCityLeader();
			}
			catch
			{
			}
			if (Check("the ruler has a partner", _king.lover == _e && flag, flag ? "" : "not a ruler"))
			{
				City city = _king.city;
				int num = Rep.Points(_host, city);
				Set(_e, 80, Romance.Of(_e, _host).Wanted);
				bool flag2 = Romance.Wed(_host, _e);
				Check("a ruler's partner won", flag2 && _e.lover == _host && _king.lover == null);
				Check("a scandal in their town", Rep.Points(_host, city) <= num - 400, $"{num} -> {Rep.Points(_host, city)}");
				yield return CivilTest.Wait(0.2f);
			}
		}
	}
}
