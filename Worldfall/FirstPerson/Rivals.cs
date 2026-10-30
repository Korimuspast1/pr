using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Rivals
	{
		public enum Ending : byte
		{
			Won,
			Lost,
			Ran,
			Off
		}

		public const string Key = "fp_jealous";

		public const int SeenLoss = 25;

		public const int ScandalLoss = 400;

		private const float FistShare = 0.07f;

		private const float Beaten = 0.3f;

		private const float Reach = 2.4f;

		private const float CutIn = 5f;

		private const float GiveUpAfter = 45f;

		private const float LostAt = 30f;

		private const float RanAt = 14f;

		private const float LongestBrawl = 60f;

		private static long _coming;

		private static long _comingFor;

		private static Actor _comingYou;

		private static float _comingTime;

		private static float _order;

		private static long _brawl;

		private static long _brawlFor;

		private static long _brawlAfterTalk;

		private static Actor _brawlYou;

		private static float _brawlTime;

		private static float _calmTick;

		private static int _theirBlows;

		private static int _yourBlows;

		private static readonly Dictionary<long, float> _gossip = new Dictionary<long, float>();

		private static readonly FieldInfo Damage = typeof(AttackData).GetField("damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		internal static string LastForTest;

		public static Ending LastEnding;

		public static long LastEndedWith;

		internal static long ComingForTest => _coming;

		internal static long BrawlForTest => _brawl;

		internal static int TheirBlowsForTest => _theirBlows;

		internal static void ForgetGossipForTest()
		{
			_gossip.Clear();
		}

		public static void Flirted(Actor you, Actor npc)
		{
			try
			{
				if (you == null || npc == null)
				{
					return;
				}
				Actor lover = npc.lover;
				if (lover != null && lover != you && lover.isAlive())
				{
					Vector2 current_position = you.current_position;
					List<Actor> list = Law.Witnesses(npc, you, current_position);
					bool num = list.Contains(lover) || (lover.kingdom != npc.kingdom && Sees(lover, current_position));
					int num2 = list.Count - (list.Contains(lover) ? 1 : 0);
					if (num2 > 0)
					{
						Gossip(you, npc, lover, current_position, num2);
					}
					if (num && _brawl == 0L && _coming != lover.data.id && !PlayerTalk.IsPlayer(lover))
					{
						Coming(lover, npc, you);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("rivals: flirted: " + ex.Message);
			}
		}

		private static bool Sees(Actor a, Vector2 at)
		{
			try
			{
				int result;
				if (!a.is_inside_building)
				{
					WorldBoxMod instance = WorldBoxMod.Instance;
					if ((object)instance == null || !instance.Interior.Inside)
					{
						result = (((a.current_position - at).sqrMagnitude <= 121f) ? 1 : 0);
						goto IL_0041;
					}
				}
				result = 0;
				goto IL_0041;
				IL_0041:
				return (byte)result != 0;
			}
			catch
			{
				return false;
			}
		}

		private static void Gossip(Actor you, Actor npc, Actor partner, Vector2 at, int others)
		{
			long id = npc.data.id;
			if (_gossip.TryGetValue(id, out var value) && Time.unscaledTime - value < 60f)
			{
				return;
			}
			if (_gossip.Count > 64)
			{
				_gossip.Clear();
			}
			_gossip[id] = Time.unscaledTime;
			City city = null;
			try
			{
				city = Hub.TownAt(at) ?? npc.city;
			}
			catch
			{
			}
			if (city != null)
			{
				try
				{
					Rep.Add(you, city, -25, "courting someone taken");
				}
				catch (Exception ex)
				{
					Log.Error("rivals: gossip: " + ex.Message);
				}
				WorldBoxMod.Instance?.Toast("Seen courting " + Dialogue.NameOf(npc) + ", who is with " + Dialogue.NameOf(partner));
				Log.Info($"rivals: {others} saw you court {Dialogue.NameOf(npc)} ({Dialogue.NameOf(partner)}'s): -{25} in {city.name}");
			}
		}

		private static void Coming(Actor partner, Actor npc, Actor you)
		{
			_coming = partner.data.id;
			_comingFor = npc.data.id;
			_comingYou = you;
			_comingTime = 0f;
			_order = 0f;
			WorldBoxMod.Instance?.Toast(Dialogue.NameOf(partner) + " saw that");
			Log.Info($"rivals: {Dialogue.NameOf(partner)} saw you court {Dialogue.NameOf(npc)}, warned {Warned(partner, you)}: coming over");
		}

		public static void Update(Actor you, float dt, Action<string> toast)
		{
			if (you == null)
			{
				return;
			}
			try
			{
				if (_brawl != 0L)
				{
					Brawl(you, dt, toast);
					return;
				}
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (_brawlAfterTalk != 0L && instance != null && !instance.Converse.Open)
				{
					Actor actor = Chronicle.Find(_brawlAfterTalk);
					_brawlAfterTalk = 0L;
					if (actor != null && actor.isAlive())
					{
						StartBrawl(actor, you, _comingFor, null, toast);
					}
				}
				else
				{
					if (_coming == 0L)
					{
						return;
					}
					Actor actor2 = Chronicle.Find(_coming);
					if (you != _comingYou || actor2 == null || !actor2.isAlive() || instance == null)
					{
						StopComing(actor2);
						return;
					}
					_comingTime += dt;
					float magnitude = (actor2.current_position - you.current_position).magnitude;
					if (_comingTime > 45f || magnitude > 30f)
					{
						StopComing(actor2);
						Log.Info("rivals: they gave up coming after you");
					}
					else if (!Hub.AnyWindowOpen && Births.Naming == null)
					{
						Conversation converse = instance.Converse;
						bool flag = converse.Open && converse.With != null && converse.With.data.id == _comingFor;
						if (magnitude <= 2.4f || (flag && magnitude <= 5f))
						{
							Arrive(actor2, you, toast);
						}
						else
						{
							Walk(actor2, you);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("rivals: update: " + ex.Message);
			}
		}

		private static void Walk(Actor partner, Actor you)
		{
			_order -= Time.unscaledDeltaTime;
			if (_order > 0f)
			{
				return;
			}
			_order = 1.2f;
			Royal.RegisterTask();
			WorldTile tile = World.world.GetTile((int)you.current_position.x, (int)you.current_position.y);
			if (tile == null)
			{
				return;
			}
			try
			{
				partner.timer_action = 0f;
				partner.setTask("fp_royal_march");
				partner.beh_tile_target = tile;
			}
			catch
			{
			}
		}

		private static void StopComing(Actor partner)
		{
			_coming = 0L;
			_comingYou = null;
			try
			{
				partner?.cancelAllBeh();
			}
			catch
			{
			}
		}

		private static void Arrive(Actor partner, Actor you, Action<string> toast)
		{
			long comingFor = _comingFor;
			StopComing(partner);
			if (Warned(partner, you) == 0)
			{
				Confront(partner, you, comingFor, toast);
			}
			else
			{
				StartBrawl(partner, you, comingFor, "\"I warned you.\"", toast);
			}
		}

		private static void Confront(Actor partner, Actor you, long courted, Action<string> toast)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = Chronicle.Find(courted);
			string text = ((actor != null) ? Dialogue.NameOf(actor) : "them");
			SetWarned(partner, you, 1);
			Chronicle.ChangeOpinion(partner, you, -10);
			LastForTest = "warned";
			if (instance == null || !instance.StartConfrontation(partner))
			{
				toast?.Invoke(Dialogue.NameOf(partner) + ": \"Stay away from " + text + ".\"");
				return;
			}
			Conversation c = instance.Converse;
			c.LastTopic = "rival";
			c.Say(Warning(partner, text));
			c.Offer(new List<Choice>
			{
				new Choice("I'm sorry. It won't happen again.", delegate
				{
					c.LastTopic = "rival sorry";
					c.EndAfter = true;
					c.Say(Dialogue.Pick("See that it doesn't.", "*glares* You'd better mean that.", "*steps back* Good."));
				}),
				new Choice(text + " can choose for themselves.", delegate
				{
					c.LastTopic = "rival defy";
					Chronicle.ChangeOpinion(partner, you, -10);
					c.EndAfter = true;
					c.Say(Dialogue.Pick("Then we settle it. Now.", "*cracks knuckles* Have it your way.", "Wrong answer."));
					_brawlAfterTalk = partner.data.id;
					_comingFor = courted;
				}, ChoiceTone.Risk),
				new Choice("Walk away", c.End, ChoiceTone.Leave)
			});
			Log.Info("rivals: " + Dialogue.NameOf(partner) + " warns you off " + text);
		}

		private static string Warning(Actor partner, string name)
		{
			if (Has(partner, "hotheaded") || Has(partner, "bloodlust") || Has(partner, "savage"))
			{
				return Dialogue.Pick("Touch " + name + " again and I'll break your jaw.", "*shoves you* " + name + " is mine. Next time I won't use words.");
			}
			if (Has(partner, "paranoid"))
			{
				return "I've seen how you look at " + name + ". I see everything. Stay away.";
			}
			if (Has(partner, "peaceful") || Has(partner, "pacifist"))
			{
				return "*voice shaking* Please. Leave " + name + " alone. We're happy.";
			}
			if (Has(partner, "honest") || Has(partner, "wise"))
			{
				return "I'll say this once, plainly. " + name + " is with me. Leave it be.";
			}
			return Dialogue.Pick("*steps between you* " + name + " is with me. Find someone else.", "I saw that. Stay away from " + name + ".", "*jaw tight* That's " + name + " you're sweet-talking. Mine.");
		}

		internal static void StartBrawl(Actor partner, Actor you, long courted, string line, Action<string> toast)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null && instance.Converse.Open)
			{
				instance.EndConversation();
			}
			_brawl = partner.data.id;
			_brawlFor = courted;
			_brawlYou = you;
			_brawlTime = 0f;
			_calmTick = 0f;
			_theirBlows = (_yourBlows = 0);
			try
			{
				partner.cancelAllBeh();
				partner.timer_action = 0f;
				partner.addAggro(you);
				partner.startFightingWith(you);
			}
			catch
			{
			}
			LastForTest = "brawl";
			toast?.Invoke((line != null) ? (Dialogue.NameOf(partner) + ": " + line) : (Dialogue.NameOf(partner) + " comes at you"));
			Log.Info("rivals: brawl with " + Dialogue.NameOf(partner) + " begins");
		}

		private static void Brawl(Actor you, float dt, Action<string> toast)
		{
			Actor actor = Chronicle.Find(_brawl);
			if (you != _brawlYou || actor == null || !actor.isAlive())
			{
				EndBrawl(actor, _brawlYou ?? you, Ending.Off, toast);
				return;
			}
			try
			{
				if (!actor.has_attack_target || actor.attack_target != you)
				{
					actor.addAggro(you);
					actor.startFightingWith(you);
				}
			}
			catch
			{
			}
			_calmTick -= dt;
			if (_calmTick <= 0f)
			{
				_calmTick = 0.4f;
				Quests.CalmOthers(you, actor);
			}
			_brawlTime += dt;
			float magnitude = (actor.current_position - you.current_position).magnitude;
			if (actor.getHealthRatio() < 0.3f)
			{
				EndBrawl(actor, you, Ending.Won, toast);
			}
			else if (you.getHealthRatio() < 0.3f)
			{
				EndBrawl(actor, you, Ending.Lost, toast);
			}
			else if (magnitude > 14f)
			{
				EndBrawl(actor, you, Ending.Ran, toast);
			}
			else if (_brawlTime > 60f)
			{
				EndBrawl(actor, you, Ending.Off, toast);
			}
		}

		private static void EndBrawl(Actor partner, Actor you, Ending how, Action<string> toast)
		{
			LastEnding = how;
			LastEndedWith = _brawl;
			_brawl = 0L;
			_brawlYou = null;
			Quests.CalmFight(partner, you);
			LastForTest = "brawl " + how.ToString().ToLowerInvariant();
			if (partner == null || you == null)
			{
				return;
			}
			Actor actor = Chronicle.Find(_brawlFor);
			string text = Dialogue.NameOf(partner);
			string text2 = ((actor != null) ? Dialogue.NameOf(actor) : "them");
			bool flag = actor != null && actor.isAlive() && (actor.current_position - you.current_position).sqrMagnitude < 225f;
			switch (how)
			{
			case Ending.Won:
				try
				{
					partner.changeHappiness("lost_fight");
				}
				catch
				{
				}
				Chronicle.ChangeOpinion(partner, you, -10);
				if (flag)
				{
					Warm(actor, you, (Romance.Likes(actor) == Charm.Bold) ? 8 : 3);
				}
				toast?.Invoke(text + " backs off");
				break;
			case Ending.Lost:
				try
				{
					you.changeHappiness("lost_fight");
				}
				catch
				{
				}
				if (flag)
				{
					Warm(actor, you, -6);
				}
				toast?.Invoke(text + " stands over you. \"Stay away from " + text2 + ".\"");
				break;
			case Ending.Ran:
				if (flag)
				{
					Warm(actor, you, -4);
				}
				toast?.Invoke("You ran from " + text);
				break;
			}
			Log.Info($"rivals: brawl with {text} over: {how} after {_brawlTime:F0} s, {_theirBlows} blows from them, {_yourBlows} from you");
		}

		private static void Warm(Actor npc, Actor you, int by)
		{
			if (npc.lover != you)
			{
				Suitor suitor = Romance.Of(npc, you);
				if (suitor.Affection != 0 || by >= 0)
				{
					suitor.Affection = Mathf.Clamp(suitor.Affection + by, 0, 100);
					Romance.Save(npc, suitor);
				}
			}
		}

		public static bool Brawling(Actor a)
		{
			try
			{
				return a != null && _brawl != 0L && a.data.id == _brawl;
			}
			catch
			{
				return false;
			}
		}

		public static bool BareHanded(Actor a)
		{
			return Brawling(a);
		}

		public static void BrawlBlow(ref AttackData data, BaseSimObject target)
		{
			if (_brawl == 0L || target == null || !target.isActor() || data.initiator == null || data.damage <= 0)
			{
				return;
			}
			try
			{
				Actor host = Blocks.Host;
				if (host == null)
				{
					return;
				}
				bool flag = data.initiator.isActor() && data.initiator.a.data.id == _brawl;
				bool flag2 = (data.initiator == host && target.a.data.id == _brawl) || (target == host && flag);
				if (!flag2 && !flag)
				{
					return;
				}
				if (flag2)
				{
					if (flag)
					{
						_theirBlows++;
					}
					else
					{
						_yourBlows++;
					}
				}
				int num = 0;
				if (flag2)
				{
					int num2 = Mathf.Min(Mathf.Max(1, Mathf.RoundToInt((float)target.a.getMaxHealth() * 0.07f)), target.a.getHealth() - 1);
					num = Mathf.Max(0, num2 / Mathf.Max(1, (!data.critical) ? 1 : data.critical_damage_multiplier));
				}
				if (data.damage > num)
				{
					object obj = data;
					Damage.SetValue(obj, num);
					data = (AttackData)obj;
				}
			}
			catch
			{
			}
		}

		public static void End()
		{
			if (_brawl != 0L)
			{
				Actor a = Chronicle.Find(_brawl);
				Actor brawlYou = _brawlYou;
				_brawl = 0L;
				_brawlYou = null;
				Quests.CalmFight(a, brawlYou);
			}
			if (_coming != 0L)
			{
				StopComing(Chronicle.Find(_coming));
			}
			_brawlAfterTalk = 0L;
		}

		public static Actor Leave(Actor npc, Actor you)
		{
			Actor actor = null;
			try
			{
				actor = npc.lover;
			}
			catch
			{
			}
			if (actor == null || actor == you)
			{
				return null;
			}
			try
			{
				if (actor.lover == npc)
				{
					actor.setLover(null);
				}
				npc.setLover(null);
			}
			catch (Exception ex)
			{
				Log.Error("rivals: leave: " + ex.Message);
			}
			Heartbreak(actor, 40);
			Chronicle.ChangeOpinion(actor, you, -60);
			Chronicle.AddOwnMemory(actor, "fp_jilted", Dialogue.NameOf(you));
			Chronicle.AddOwnMemory(npc, "fp_left", Dialogue.NameOf(actor));
			SetWarned(actor, you, 1);
			if (_coming == actor.data.id)
			{
				StopComing(actor);
			}
			bool flag = false;
			try
			{
				flag = actor.isKing() || actor.isCityLeader();
			}
			catch
			{
			}
			if (flag)
			{
				City city = null;
				try
				{
					city = actor.city;
				}
				catch
				{
				}
				if (city != null)
				{
					try
					{
						Rep.Add(you, city, -400, "took the ruler's partner");
					}
					catch (Exception ex2)
					{
						Log.Error("rivals: scandal: " + ex2.Message);
					}
					WorldBoxMod.Instance?.Toast("A scandal in " + city.name + ". You took " + Dialogue.NameOf(actor) + "'s partner");
				}
			}
			Log.Info("rivals: " + Dialogue.NameOf(npc) + " leaves " + Dialogue.NameOf(actor) + " for you" + (flag ? " (a ruler: scandal)" : ""));
			return actor;
		}

		public static void Heartbreak(Actor a, int by)
		{
			try
			{
				a.setHappiness(Mathf.Clamp(a.getHappiness() - by, a.getMinHappiness(), a.getMaxHappiness()));
			}
			catch
			{
			}
		}

		public static int Warned(Actor partner, Actor you)
		{
			string pResult = null;
			try
			{
				partner.data.get("fp_jealous", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return 0;
			}
			string text = you.data.id + ":";
			string[] array = pResult.Split('|');
			foreach (string text2 in array)
			{
				if (text2.StartsWith(text, StringComparison.Ordinal) && int.TryParse(text2.Substring(text.Length), out var result))
				{
					return result;
				}
			}
			return 0;
		}

		public static void SetWarned(Actor partner, Actor you, int n)
		{
			try
			{
				string pResult = null;
				partner.data.get("fp_jealous", out pResult, null);
				List<string> list = new List<string>();
				string text = you.data.id + ":";
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array = pResult.Split('|');
					foreach (string text2 in array)
					{
						if (text2.Length > 0 && !text2.StartsWith(text, StringComparison.Ordinal))
						{
							list.Add(text2);
						}
					}
				}
				if (n > 0)
				{
					list.Add(text + n);
				}
				while (list.Count > 4)
				{
					list.RemoveAt(0);
				}
				if (list.Count == 0)
				{
					partner.data.removeString("fp_jealous");
				}
				else
				{
					partner.data.set("fp_jealous", string.Join("|", list.ToArray()));
				}
			}
			catch (Exception ex)
			{
				Log.Error("rivals: save: " + ex.Message);
			}
		}

		private static bool Has(Actor a, string trait)
		{
			try
			{
				return a.hasTrait(trait);
			}
			catch
			{
				return false;
			}
		}
	}
}
