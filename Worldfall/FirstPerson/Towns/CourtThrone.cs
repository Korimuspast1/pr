using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class CourtThrone
	{
		internal enum Offer
		{
			Peace,
			Tribute,
			Alliance,
			Terms
		}

		internal sealed class Envoy
		{
			public Kingdom From;

			public Kingdom To;

			public string FromName;

			public Offer Kind;

			public int Gold;

			public float Until;

			public bool Talks;
		}

		private const float EnvoyEvery = 150f;

		private const float EnvoyStays = 300f;

		private const float NextAfter = 0.8f;

		private const int MaxEnvoys = 3;

		private const int MaxPetitioners = 4;

		internal static readonly List<Envoy> Envoys = new List<Envoy>();

		internal static Envoy Hearing;

		internal static Actor Audience;

		internal static int Heard;

		private static readonly Queue<Actor> Petitioners = new Queue<Actor>();

		private static Action<string> _toast;

		private static Actor _king;

		private static float _envoyTimer = 40f;

		private static float _gap = -1f;

		private static float _countAt = -99f;

		private static int _countCached;

		private static object _world;

		private static bool _asked;

		public static bool Holding { get; private set; }

		public static void Update(Actor host, float dt)
		{
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				Envoys.Clear();
				Stop();
			}
			if (host == null || !Royal.IsKing(host))
			{
				if (Holding)
				{
					Stop();
				}
				_asked = false;
				return;
			}
			bool flag = false;
			try
			{
				flag = Config.paused;
			}
			catch
			{
			}
			if (flag)
			{
				foreach (Envoy envoy in Envoys)
				{
					envoy.Until += dt;
				}
			}
			Expire(host);
			if (_asked)
			{
				_asked = false;
				if (WorldBoxMod.Instance != null && EnvoysFor(host) + FindPetitioners(host).Count > 0)
				{
					WorldBoxMod.Instance.Royal.Open = false;
				}
				Begin(host, _toast);
			}
			if (!flag)
			{
				_envoyTimer -= dt;
			}
			if (_envoyTimer <= 0f)
			{
				_envoyTimer = 150f * UnityEngine.Random.Range(0.7f, 1.3f);
				try
				{
					SendEnvoys(host);
				}
				catch (Exception e)
				{
					Hub.Fail("envoys", e);
				}
			}
			if (!Holding)
			{
				return;
			}
			if (host != _king || !Court.AtCourt(host))
			{
				Stop();
				return;
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || instance.Converse.Open)
			{
				_gap = 0.8f;
				return;
			}
			string lastTopic = instance.Converse.LastTopic;
			if (lastTopic != "goodbye" && lastTopic != "envoy")
			{
				Stop();
				return;
			}
			_gap -= dt;
			if (!(_gap > 0f))
			{
				Next(host);
			}
		}

		public static int Waiting(Actor host)
		{
			if (Time.unscaledTime - _countAt < 2f)
			{
				return _countCached;
			}
			_countAt = Time.unscaledTime;
			_countCached = 0;
			try
			{
				_countCached = EnvoysFor(host) + FindPetitioners(host).Count;
			}
			catch
			{
			}
			return _countCached;
		}

		private static int EnvoysFor(Actor host)
		{
			Kingdom home = Court.KingdomOf(host);
			int num = 0;
			foreach (Envoy envoy in Envoys)
			{
				if (For(envoy, home))
				{
					num++;
				}
			}
			return num;
		}

		private static bool For(Envoy e, Kingdom home)
		{
			if (home != null && Alive(e.From) && e.From != home)
			{
				if (e.To != null)
				{
					return e.To == home;
				}
				return true;
			}
			return false;
		}

		public static void Ask(Actor host, Action<string> toast)
		{
			_asked = host != null;
			_toast = toast;
		}

		public static void Begin(Actor host, Action<string> toast)
		{
			if (host == null || !Royal.IsKing(host))
			{
				return;
			}
			_toast = toast;
			_king = host;
			Petitioners.Clear();
			foreach (Actor item in FindPetitioners(host))
			{
				Petitioners.Enqueue(item);
			}
			Heard = 0;
			if (EnvoysFor(host) == 0 && Petitioners.Count == 0)
			{
				toast?.Invoke("No one waits for an audience.");
				return;
			}
			Holding = true;
			_gap = 0f;
			Log.Info($"court: {WorldBoxMod.Describe(host)} holds court: {EnvoysFor(host)} envoys, {Petitioners.Count} petitioners");
			Next(host);
		}

		public static void Stop()
		{
			if (Holding)
			{
				Log.Info("court: court is over after " + Heard + " audiences");
			}
			Holding = false;
			Hearing = null;
			Audience = null;
			Petitioners.Clear();
			_king = null;
		}

		private static void Next(Actor host)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null)
			{
				Stop();
				return;
			}
			Kingdom home = Court.KingdomOf(host);
			for (int i = 0; i < Envoys.Count; i++)
			{
				Envoy envoy = Envoys[i];
				if (For(envoy, home))
				{
					Envoys.RemoveAt(i--);
					Actor actor = EnvoyOf(envoy.From);
					if (actor != null && instance.StartAudience(actor))
					{
						Hearing = envoy;
						Audience = actor;
						Heard++;
						Present(instance.Converse, envoy);
						return;
					}
				}
			}
			Hearing = null;
			while (Petitioners.Count > 0)
			{
				Actor actor2 = Petitioners.Dequeue();
				if (actor2 != null && actor2.isAlive() && !Dialogue.Petitioned(actor2) && Dialogue.YouRule(host, actor2) && instance.StartAudience(actor2))
				{
					Audience = actor2;
					Heard++;
					Dialogue.Petition(instance.Converse);
					return;
				}
			}
			Audience = null;
			_toast?.Invoke("No one else waits.");
			Stop();
		}

		private static List<Actor> FindPetitioners(Actor host)
		{
			List<Actor> list = new List<Actor>();
			Kingdom kingdom = Court.KingdomOf(host);
			if (kingdom == null)
			{
				return list;
			}
			List<KeyValuePair<City, int>> list2 = new List<KeyValuePair<City, int>>();
			try
			{
				foreach (City city in kingdom.cities)
				{
					if (city != null && city.isAlive())
					{
						list2.Add(new KeyValuePair<City, int>(city, Trouble(city) + ((city == kingdom.capital) ? 1 : 0)));
					}
				}
			}
			catch
			{
			}
			list2.Sort((KeyValuePair<City, int> a, KeyValuePair<City, int> b) => b.Value.CompareTo(a.Value));
			int year = Chronicle.Year;
			foreach (KeyValuePair<City, int> item in list2)
			{
				if (list.Count >= 4)
				{
					break;
				}
				Actor actor = null;
				try
				{
					if (item.Key.units == null)
					{
						continue;
					}
					int count = item.Key.units.Count;
					int num = (int)((count > 0) ? ((item.Key.getID() + year) % count) : 0);
					for (int num2 = 0; num2 < count; num2++)
					{
						if (actor != null)
						{
							break;
						}
						Actor actor2 = item.Key.units[(num + num2) % count];
						if (actor2 != null && actor2 != host && actor2.isAlive() && actor2.isAdult() && Dialogue.IsPerson(actor2) && !Court.SeatOf(actor2, out var _) && !Dialogue.Petitioned(actor2) && Dialogue.YouRule(host, actor2))
						{
							actor = actor2;
						}
					}
					goto IL_018b;
				}
				catch
				{
					goto IL_018b;
				}
				IL_018b:
				if (actor != null)
				{
					list.Add(actor);
				}
			}
			return list;
		}

		private static int Trouble(City c)
		{
			int num = 0;
			try
			{
				int num2 = Mathf.Max(1, c.getPopulationPeople());
				int num3 = 0;
				string[] array = new string[5] { "berries", "wheat", "fish", "meat", "bread" };
				foreach (string pResourceID in array)
				{
					num3 += c.getResourcesAmount(pResourceID);
				}
				if (num3 < num2 * 2)
				{
					num += 2;
				}
				if (c.countHomeless() > 1)
				{
					num += 2;
				}
			}
			catch
			{
			}
			return num;
		}

		internal static Actor EnvoyOf(Kingdom k)
		{
			Actor actor = Court.Holder(k, Seat.Chancellor) ?? Court.Holder(k, Seat.Steward);
			if (actor != null)
			{
				return actor;
			}
			try
			{
				foreach (Actor unit in k.getUnits())
				{
					if (unit != null && unit.isAlive() && unit.isAdult() && !unit.isKing() && Dialogue.IsPerson(unit))
					{
						return unit;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static void SendEnvoys(Actor host)
		{
			Kingdom home = host.kingdom;
			if (home == null || EnvoysFor(host) >= 3)
			{
				return;
			}
			foreach (Kingdom other in Royal.Rivals(home))
			{
				if (EnvoysFor(host) >= 3)
				{
					break;
				}
				if (Envoys.Exists((Envoy e) => e.From == other && For(e, home)))
				{
					continue;
				}
				float num = ((home.power > 0) ? ((float)other.power / (float)home.power) : 1f);
				Envoy envoy = new Envoy
				{
					From = other,
					To = home,
					FromName = Royal.NameOf(other),
					Until = Time.unscaledTime + 300f
				};
				if (Royal.AtWar(home, other))
				{
					if (UnityEngine.Random.value > 0.5f || !Peace.WouldSendEnvoy(home, other, out var terms))
					{
						continue;
					}
					envoy.Kind = (terms ? Offer.Terms : Offer.Peace);
					envoy.Talks = true;
				}
				else
				{
					if (Royal.Allied(home, other))
					{
						continue;
					}
					if (num < 0.35f && Near(home, other) && UnityEngine.Random.value < 0.35f)
					{
						envoy.Kind = Offer.Tribute;
						envoy.Gold = Mathf.Min(30, Gold(other.capital));
						if (envoy.Gold <= 0)
						{
							continue;
						}
					}
					else
					{
						int total;
						try
						{
							total = World.world.diplomacy.getOpinion(other, home).total;
						}
						catch
						{
							continue;
						}
						float num2 = (SharesEnemy(home, other) ? 0.6f : 0.25f);
						if (total < 25 || UnityEngine.Random.value > num2)
						{
							continue;
						}
						envoy.Kind = Offer.Alliance;
					}
				}
				Envoys.Add(envoy);
				_countAt = -99f;
				Log.Info(string.Format("court: an envoy from {0} ({1}{2})", envoy.FromName, envoy.Kind, (envoy.Gold > 0) ? (", " + envoy.Gold + " gold") : ""));
				Hub.Toast?.Invoke("An envoy from " + envoy.FromName + ((envoy.Kind == Offer.Terms) ? " brings terms to court." : " waits at court."));
			}
		}

		private static void Expire(Actor host)
		{
			Kingdom home = Court.KingdomOf(host);
			for (int num = Envoys.Count - 1; num >= 0; num--)
			{
				Envoy envoy = Envoys[num];
				if (!Alive(envoy.From) || (envoy.To != null && !Alive(envoy.To)))
				{
					Envoys.RemoveAt(num);
				}
				else if (!(Time.unscaledTime < envoy.Until))
				{
					Envoys.RemoveAt(num);
					if (For(envoy, home))
					{
						Hub.Toast?.Invoke("The envoy from " + envoy.FromName + " went home unheard.");
					}
				}
			}
		}

		private static bool SharesEnemy(Kingdom a, Kingdom b)
		{
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item != a && item != b && item.isAlive() && Royal.AtWar(a, item) && Royal.AtWar(b, item))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		private static bool Near(Kingdom a, Kingdom b)
		{
			try
			{
				if (a.capital == null || b.capital == null)
				{
					return false;
				}
				return (Royal.Position(a.capital) - Royal.Position(b.capital)).sqrMagnitude < 22500f;
			}
			catch
			{
				return false;
			}
		}

		private static int Gold(City c)
		{
			try
			{
				return c?.getResourcesAmount("gold") ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		private static bool Alive(Kingdom k)
		{
			try
			{
				return k?.isAlive() ?? false;
			}
			catch
			{
				return false;
			}
		}

		private static void Present(Conversation c, Envoy e)
		{
			if (e.Talks)
			{
				Peace.HearEnvoy(c, e.From, e.Kind == Offer.Terms);
				return;
			}
			string text = "Your Majesty";
			string text2 = "my king";
			try
			{
				if (e.From.king != null)
				{
					text2 = "King " + Dialogue.NameOf(e.From.king);
				}
			}
			catch
			{
			}
			string text3;
			string label;
			switch (e.Kind)
			{
			case Offer.Peace:
				text3 = ((e.Gold > 0) ? $"{text}, {text2} of {e.FromName} asks for peace. He sends {e.Gold} gold as a token." : (text + ", " + text2 + " of " + e.FromName + " asks for peace. Our coffers are empty, but our word is good."));
				label = ((e.Gold > 0) ? $"Accept peace and {e.Gold} gold" : "Accept peace");
				break;
			case Offer.Tribute:
				text3 = $"{text}, {e.FromName} wants no quarrel with you. {text2} offers {e.Gold} gold in tribute.";
				label = $"Accept {e.Gold} gold";
				break;
			default:
				text3 = text + ", " + text2 + " of " + e.FromName + " offers an alliance. Our swords would stand with yours.";
				label = "Accept the alliance";
				break;
			}
			c.LastTopic = "envoy_offer";
			c.Say(text3);
			List<Choice> choices = new List<Choice>
			{
				new Choice(label, delegate
				{
					Answer(c, e, accept: true);
				}, ChoiceTone.Quest),
				new Choice("Refuse", delegate
				{
					Answer(c, e, accept: false);
				}, ChoiceTone.Risk)
			};
			c.Offer(choices);
		}

		private static void Answer(Conversation c, Envoy e, bool accept)
		{
			Actor you = c.You;
			Kingdom kingdom = Court.KingdomOf(you);
			string text;
			if (!accept || kingdom == null || !Alive(e.From))
			{
				text = Dialogue.Pick("As you wish. I will tell my king.", "*bows stiffly* Then I take your answer home.");
				Log.Info($"court: refused {e.FromName}'s {e.Kind}");
			}
			else
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				Action<string> action = ((instance != null) ? new Action<string>(instance.Toast) : ((Action<string>)delegate
				{
				}));
				switch (e.Kind)
				{
				case Offer.Peace:
					if (Royal.AtWar(kingdom, e.From) && instance != null)
					{
						instance.Royal.MakePeace(you, e.From, action);
					}
					Pay(e.From, kingdom, e.Gold);
					text = "Peace, then. My king will be glad of it.";
					break;
				case Offer.Tribute:
				{
					int num = Pay(e.From, kingdom, e.Gold);
					try
					{
						World.world.diplomacy.getRelation(kingdom, e.From).data.timestamp_last_war_ended = World.world.getCurWorldTime();
					}
					catch
					{
					}
					action((num > 0) ? (e.FromName + " pays " + num + " gold in tribute.") : (e.FromName + "'s gold has nowhere to go: your capital has no stockpile."));
					text = "The gold is yours. May there be no quarrel between us.";
					break;
				}
				default:
				{
					bool flag = false;
					try
					{
						flag = !Royal.AtWar(kingdom, e.From) && Royal.Join(kingdom, e.From);
					}
					catch (Exception ex)
					{
						Log.Error("court: alliance: " + ex.Message);
					}
					action(flag ? (Royal.NameOf(kingdom) + " and " + e.FromName + " are allies.") : "The alliance could not be sealed.");
					text = (flag ? "Then we stand together. My king will rejoice." : "Something stands between us still. Forgive me.");
					break;
				}
				}
				Log.Info($"court: accepted {e.FromName}'s {e.Kind}");
			}
			c.LastTopic = "envoy";
			c.EndAfter = true;
			c.Say(text);
		}

		private static int Pay(Kingdom from, Kingdom to, int gold)
		{
			if (gold <= 0)
			{
				return 0;
			}
			try
			{
				City capital = from.capital;
				City capital2 = to.capital;
				if (capital == null || capital2 == null || capital2.getRandomStockpile() == null)
				{
					return 0;
				}
				gold = Mathf.Min(gold, capital.getResourcesAmount("gold"));
				if (gold <= 0)
				{
					return 0;
				}
				capital.takeResource("gold", gold);
				capital2.addResourcesToRandomStockpile("gold", gold);
				return gold;
			}
			catch
			{
				return 0;
			}
		}
	}
}
