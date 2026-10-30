using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Peace
	{
		internal enum Want : byte
		{
			Refuse,
			TwoTowns,
			Town,
			Gold,
			Even,
			TheyPay,
			TheyCede
		}

		internal struct Why
		{
			public string Text;

			public float Points;
		}

		internal sealed class Stance
		{
			public Kingdom Them;

			public Kingdom You;

			public float Base;

			public float D;

			public float Position;

			public float Wear;

			public float Strikes;

			public bool YouStarted;

			public Want Level;

			public readonly List<Why> Reasons = new List<Why>();
		}

		internal sealed class Deal
		{
			public Kingdom Them;

			public Kingdom You;

			public Want Level;

			public float D;

			public readonly List<City> YouCede = new List<City>();

			public int YouPay;

			public City TheyCede;

			public int TheyPay;
		}

		private sealed class Grudge
		{
			public long At;

			public double Pts;

			public double T;

			public long War;

			public double WarPts;

			public double WarAt;
		}

		private struct Push
		{
			public double Pts;

			public double T;
		}

		private sealed class Talk
		{
			public Kingdom Them;

			public Kingdom You;

			public Stance Stance;

			public Deal Deal;

			public bool AtCourt;

			public bool Offered;

			public readonly HashSet<string> Refused = new HashSet<string>();
		}

		internal sealed class Mood
		{
			public string Word;

			public Color Color;

			public readonly List<KeyValuePair<string, Color>> Lines = new List<KeyValuePair<string, Color>>();

			public float At;
		}

		internal const double Year = 60.0;

		private const double GrudgeHalfLife = 600.0;

		private const double PressureHalfLife = 120.0;

		private const double TruceTime = 300.0;

		private const double AskAgainAfter = 60.0;

		internal const float StartedWar = 25f;

		internal const float BrokeTruce = 35f;

		internal const float GoldAt = 10f;

		internal const float TownAt = 35f;

		internal const float TwoTownsAt = 70f;

		internal const float RefuseAt = 100f;

		private const float GoldPerPoint = 6f;

		private const float GoldForATown = 300f;

		private const float MaxPressure = 90f;

		internal static float? ForceD;

		internal static float? ForcePosition;

		private static object _world;

		private static float _watchIn;

		internal const string Key = "fp_grudge";

		private const string CountedKey = "fp_grudged";

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		private static readonly Dictionary<string, Push> Pushes = new Dictionary<string, Push>();

		private static string _armed;

		private static float _armedUntil;

		internal const float WarOverSlain = 45f;

		private static Talk _talk;

		private static Kingdom _asked;

		private static Action<string> _toast;

		private static readonly Dictionary<string, double> RefusedUntil = new Dictionary<string, double>();

		private static readonly Dictionary<string, Mood> Moods = new Dictionary<string, Mood>();

		private static readonly Color Hard = new Color(1f, 0.55f, 0.45f);

		private static readonly Color Soft = new Color(0.6f, 1f, 0.55f);

		private static readonly Color Plain = new Color(1f, 0.92f, 0.7f);

		internal static void ClearForTest()
		{
			RefusedUntil.Clear();
			Moods.Clear();
		}

		internal static void Update(float dt)
		{
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				Pushes.Clear();
				RefusedUntil.Clear();
				Moods.Clear();
				_asked = null;
				_talk = null;
			}
			if (_asked != null)
			{
				Kingdom asked = _asked;
				_asked = null;
				try
				{
					OpenTalk(asked);
				}
				catch (Exception ex)
				{
					Log.Error("peace: talk: " + ex);
				}
			}
			_watchIn -= dt;
			if (_watchIn > 0f)
			{
				return;
			}
			_watchIn = 2f;
			try
			{
				Watch();
			}
			catch (Exception ex2)
			{
				Log.Error("peace: watching wars: " + ex2.Message);
			}
		}

		internal static void WatchForTest()
		{
			Watch();
		}

		private static void Watch()
		{
			if (World.world == null || World.world.wars == null)
			{
				return;
			}
			try
			{
				if (!Config.game_loaded || SmoothLoader.isLoading())
				{
					return;
				}
			}
			catch
			{
				return;
			}
			List<War> list = new List<War>();
			foreach (War activeWar in World.world.wars.getActiveWars())
			{
				if (activeWar != null)
				{
					list.Add(activeWar);
				}
			}
			foreach (War item in list)
			{
				if (item.hasEnded() || item.isTotalWar())
				{
					continue;
				}
				Kingdom mainAttacker = item.getMainAttacker();
				Kingdom mainDefender = item.getMainDefender();
				if (!Alive(mainAttacker) || !Alive(mainDefender))
				{
					continue;
				}
				long iD = item.getID();
				bool pResult = false;
				try
				{
					item.data.get("fp_grudged", out pResult, pDefault: false);
				}
				catch
				{
				}
				if (!pResult)
				{
					Grudge grudge = Entry(mainDefender, mainAttacker);
					Grudge grudge2 = Entry(mainAttacker, mainDefender);
					bool flag = (grudge != null && grudge.War == iD) || (grudge2 != null && grudge2.War == iD);
					try
					{
						item.data.set("fp_grudged", pData: true);
					}
					catch
					{
					}
					if (!flag)
					{
						bool flag2 = TruceBroken(mainAttacker, mainDefender, item.data.created_time);
						Count(mainDefender, mainAttacker, iD, 25f + (flag2 ? 35f : 0f));
						Log.Info(string.Format("peace: {0} started a war on {1}{2}; {3}'s grudge is now {4:F0}", Royal.NameOf(mainAttacker), Royal.NameOf(mainDefender), flag2 ? ", breaking a truce" : "", Royal.NameOf(mainDefender), GrudgeOf(mainDefender, mainAttacker)));
					}
				}
			}
		}

		private static bool TruceBroken(Kingdom a, Kingdom b, double start)
		{
			double num = WarEndedAt(a, b);
			if (num > 0.0 && start >= num - 1.0)
			{
				return start - num < 300.0;
			}
			return false;
		}

		internal static double WarEndedAt(Kingdom a, Kingdom b)
		{
			try
			{
				if (a == null || b == null || a == b)
				{
					return 0.0;
				}
				long iD = a.getID();
				long iD2 = b.getID();
				string pID = ((iD > iD2) ? (iD + "_" + iD2) : (iD2 + "_" + iD));
				if (!World.world.diplomacy.tryGet(pID, out var pObject) || pObject == null)
				{
					return 0.0;
				}
				return pObject.data.timestamp_last_war_ended;
			}
			catch
			{
				return 0.0;
			}
		}

		internal static bool PeaceLately(Kingdom a, Kingdom b, double seconds = 20.0)
		{
			double num = WarEndedAt(a, b);
			if (num <= 0.0)
			{
				return false;
			}
			double num2 = 0.0;
			try
			{
				num2 = World.world.getCurWorldTime();
			}
			catch
			{
				return false;
			}
			if (num2 - num >= -1.0)
			{
				return num2 - num < seconds;
			}
			return false;
		}

		private static Dictionary<long, Grudge> Read(Kingdom k)
		{
			Dictionary<long, Grudge> dictionary = new Dictionary<long, Grudge>();
			string pResult = null;
			try
			{
				k.data.get("fp_grudge", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return dictionary;
			}
			string[] array = pResult.Split('|');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(':');
				if (array2.Length >= 6)
				{
					try
					{
						Grudge grudge = new Grudge
						{
							At = long.Parse(array2[0], Inv),
							Pts = double.Parse(array2[1], Inv),
							T = double.Parse(array2[2], Inv),
							War = long.Parse(array2[3], Inv),
							WarPts = double.Parse(array2[4], Inv),
							WarAt = double.Parse(array2[5], Inv)
						};
						dictionary[grudge.At] = grudge;
					}
					catch
					{
					}
				}
			}
			return dictionary;
		}

		private static void Write(Kingdom k, Dictionary<long, Grudge> all)
		{
			double now = Quests.Now;
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Grudge value in all.Values)
			{
				if (!(Held(value, now) < 0.5) || !(now - value.WarAt > 1200.0))
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('|');
					}
					stringBuilder.Append(value.At.ToString(Inv)).Append(':').Append(value.Pts.ToString("F2", Inv))
						.Append(':')
						.Append(value.T.ToString("F1", Inv))
						.Append(':')
						.Append(value.War.ToString(Inv))
						.Append(':')
						.Append(value.WarPts.ToString("F1", Inv))
						.Append(':')
						.Append(value.WarAt.ToString("F1", Inv));
				}
			}
			try
			{
				if (stringBuilder.Length == 0)
				{
					k.data.removeString("fp_grudge");
				}
				else
				{
					k.data.set("fp_grudge", stringBuilder.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("peace: saving a grudge: " + ex.Message);
			}
			Moods.Clear();
		}

		private static Grudge Entry(Kingdom angry, Kingdom at)
		{
			if (angry == null || at == null)
			{
				return null;
			}
			if (!Read(angry).TryGetValue(at.getID(), out var value))
			{
				return null;
			}
			return value;
		}

		private static double Fade(double dt, double halfLife)
		{
			return Math.Pow(0.5, Math.Max(0.0, dt) / halfLife);
		}

		private static double Held(Grudge g, double now)
		{
			if (g != null)
			{
				return g.Pts * Fade(now - g.T, 600.0);
			}
			return 0.0;
		}

		internal static float GrudgeOf(Kingdom angry, Kingdom at)
		{
			return (float)Held(Entry(angry, at), Quests.Now);
		}

		private static void Count(Kingdom angry, Kingdom at, long war, float points)
		{
			Dictionary<long, Grudge> dictionary = Read(angry);
			long iD = at.getID();
			double now = Quests.Now;
			if (!dictionary.TryGetValue(iD, out var value))
			{
				Grudge obj = new Grudge
				{
					At = iD
				};
				value = obj;
				dictionary[iD] = obj;
			}
			value.Pts = Held(value, now) + (double)points;
			value.T = now;
			value.War = war;
			value.WarPts = points;
			value.WarAt = now;
			Write(angry, dictionary);
		}

		private static void Mark(Kingdom angry, Kingdom at, long war)
		{
			Dictionary<long, Grudge> dictionary = Read(angry);
			long iD = at.getID();
			double now = Quests.Now;
			if (!dictionary.TryGetValue(iD, out var value))
			{
				Grudge obj = new Grudge
				{
					At = iD,
					T = now,
					WarAt = now
				};
				value = obj;
				dictionary[iD] = obj;
			}
			value.War = war;
			value.WarPts = 0.0;
			Write(angry, dictionary);
		}

		private static void Settle(Kingdom angry, Kingdom at, float points)
		{
			if (!(points <= 0f))
			{
				Dictionary<long, Grudge> dictionary = Read(angry);
				if (dictionary.TryGetValue(at.getID(), out var value))
				{
					double now = Quests.Now;
					value.Pts = Math.Max(0.0, Held(value, now) - (double)points);
					value.T = now;
					Write(angry, dictionary);
				}
			}
		}

		private static string PairKey(Kingdom by, Kingdom on)
		{
			return by.getID().ToString(Inv) + ">" + on.getID().ToString(Inv);
		}

		internal static float PressureOf(Kingdom by, Kingdom on)
		{
			if (by == null || on == null || !Pushes.TryGetValue(PairKey(by, on), out var value))
			{
				return 0f;
			}
			return (float)(value.Pts * Fade(Quests.Now - value.T, 120.0));
		}

		internal static bool Open(Royal.Deed d)
		{
			if ((uint)d <= 5u || (uint)(d - 12) <= 1u)
			{
				return false;
			}
			return true;
		}

		internal static bool HoldOff(Kingdom you, Kingdom them, string what, Action<string> toast)
		{
			if (you == null || them == null || Royal.AtWar(you, them))
			{
				return false;
			}
			if (Royal.Allied(you, them))
			{
				toast(Royal.NameOf(them) + " is your ally. Strike an enemy.");
				return true;
			}
			string text = them.getID() + what;
			float unscaledTime = Time.unscaledTime;
			if (_armed == text && unscaledTime < _armedUntil)
			{
				return false;
			}
			_armed = text;
			_armedUntil = unscaledTime + 5f;
			toast(Royal.NameOf(them) + " is at peace with you. A strike means war. Click " + what + " again within 5 seconds to strike.");
			return true;
		}

		internal static void Struck(Kingdom you, Kingdom them, City town, int cost, Action<string> toast, Action horn)
		{
			if (Alive(you) && Alive(them) && you != them)
			{
				if (!Royal.AtWar(you, them))
				{
					Provoked(you, them, toast, horn);
				}
				bool flag = false;
				try
				{
					flag = town != null && town == them.capital;
				}
				catch
				{
				}
				float num = Mathf.Clamp(4f / (float)(Royal.CityCount(them) + 1), 0.35f, 1.5f);
				float num2 = (float)cost * 0.5f * num * (flag ? 1.5f : 1f);
				string key = PairKey(you, them);
				Pushes[key] = new Push
				{
					Pts = PressureOf(you, them) + num2,
					T = Quests.Now
				};
				Moods.Clear();
				Log.Info(string.Format("peace: a strike on {0}{1} weighs {2:F0}; {3:F0} in all", Royal.NameOf(them), flag ? "'s capital" : "", num2, PressureOf(you, them)));
			}
		}

		private static void Provoked(Kingdom you, Kingdom them, Action<string> toast, Action horn)
		{
			bool flag = TruceBroken(you, them, Quests.Now);
			try
			{
				try
				{
					World.world.diplomacy.startWar(them, you, WarTypeLibrary.normal, pLog: true);
				}
				catch (NullReferenceException)
				{
					if (Royal.AtWar(you, them))
					{
						throw;
					}
					World.world.diplomacy.startWar(them, you, WarTypeLibrary.normal, pLog: false);
				}
			}
			catch (Exception ex2)
			{
				Log.Error("peace: their declaration: " + ex2);
				return;
			}
			War war = WarBetween(you, them);
			if (war != null)
			{
				Count(them, you, war.getID(), 25f + (flag ? 35f : 0f));
				Mark(you, them, war.getID());
			}
			toast?.Invoke(Royal.NameOf(them) + " declares war on you!");
			horn?.Invoke();
			Log.Info("peace: a strike at peace: " + Royal.NameOf(them) + " declares war on " + Royal.NameOf(you) + (flag ? " (a truce broken)" : ""));
		}

		internal static War WarBetween(Kingdom a, Kingdom b)
		{
			try
			{
				return World.world.wars.getWar(a, b, pOnlyMain: false) ?? World.world.wars.getWar(b, a, pOnlyMain: false);
			}
			catch
			{
				return null;
			}
		}

		internal static Stance StanceOf(Kingdom them, Kingdom you)
		{
			Stance s = new Stance
			{
				Them = them,
				You = you
			};
			double now = Quests.Now;
			War war = WarBetween(them, you);
			long num = war?.getID() ?? (-1);
			float sum = 0f;
			Grudge grudge = Entry(them, you);
			Grudge grudge2 = Entry(you, them);
			float num2 = (float)Held(grudge, now);
			float num3 = (float)Held(grudge2, now);
			float num4 = ((grudge != null && grudge.War == num && grudge.WarPts > 0.0) ? Mathf.Min(num2, (float)(grudge.WarPts * Fade(now - grudge.WarAt, 600.0))) : 0f);
			s.YouStarted = num4 > 0f;
			if (num4 > 0f)
			{
				float num5 = Mathf.Min(num4, 25f * (float)Fade(now - grudge.WarAt, 600.0));
				Add("You started this war", num5);
				Add("You broke a truce", num4 - num5);
			}
			Add("You made war on them before", num2 - num4);
			float num6 = ((grudge2 != null && grudge2.War == num && grudge2.WarPts > 0.0) ? Mathf.Min(num3, (float)(grudge2.WarPts * Fade(now - grudge2.WarAt, 600.0))) : 0f);
			if (num6 > 0f)
			{
				float num7 = Mathf.Min(num6, 25f * (float)Fade(now - grudge2.WarAt, 600.0));
				Add("They started this war", 0f - num7);
				Add("They broke a truce", 0f - (num6 - num7));
			}
			Add("They made war on you before", 0f - (num3 - num6));
			float num8 = ForcePosition ?? PositionOf(them, you, war);
			s.Position = num8;
			float points = 60f * num8 + 100f * Mathf.Max(0f, num8 - 0.6f) + 100f * Mathf.Min(0f, num8 + 0.6f);
			Add((num8 > 0.6f) ? "Winning big" : ((num8 > 0.15f) ? "Winning" : ((num8 < -0.6f) ? "Losing badly" : ((num8 < -0.15f) ? "Losing" : "An even war"))), points);
			if (Besieging(them, you))
			{
				Add("Taking one of your towns", 15f);
			}
			if (Besieging(you, them))
			{
				Add("Losing a town to you", -15f);
			}
			int num9 = 0;
			try
			{
				num9 = war?.getDuration() ?? 0;
			}
			catch
			{
			}
			s.Wear = Mathf.Clamp((float)(num9 - 3) * 3f, 0f, 30f);
			Add("A long war", 0f - s.Wear);
			Actor actor = null;
			try
			{
				actor = them.king;
			}
			catch
			{
			}
			if (actor != null)
			{
				if (Dialogue.HasTrait(actor, "bloodlust"))
				{
					Add("Their king is bloodthirsty", 15f);
				}
				if (Dialogue.HasTrait(actor, "ambitious"))
				{
					Add("Their king is ambitious", 10f);
				}
				if (Dialogue.HasTrait(actor, "peaceful"))
				{
					Add("Their king is peaceful", -10f);
				}
				if (Dialogue.HasTrait(actor, "pacifist"))
				{
					Add("Their king is a pacifist", -25f);
				}
			}
			s.Base = sum;
			float num10 = Mathf.Min(90f, PressureOf(you, them));
			s.D = Mathf.Max(s.Base - num10, Mathf.Min(s.Base, -15f));
			s.Strikes = s.D - s.Base;
			if (s.Strikes < -2.5f)
			{
				s.Reasons.Add(new Why
				{
					Text = "Your strikes on their towns",
					Points = s.Strikes
				});
			}
			if (ForceD.HasValue)
			{
				s.D = ForceD.Value;
			}
			s.Level = LevelOf(s.D);
			return s;
			void Add(string text, float num11)
			{
				sum += num11;
				if (Mathf.Abs(num11) >= 2.5f)
				{
					s.Reasons.Add(new Why
					{
						Text = text,
						Points = num11
					});
				}
			}
		}

		internal static Want LevelOf(float d)
		{
			if (d >= 100f)
			{
				return Want.Refuse;
			}
			if (d >= 70f)
			{
				return Want.TwoTowns;
			}
			if (d >= 35f)
			{
				return Want.Town;
			}
			if (d >= 10f)
			{
				return Want.Gold;
			}
			if (d > -10f)
			{
				return Want.Even;
			}
			if (d > -35f)
			{
				return Want.TheyPay;
			}
			return Want.TheyCede;
		}

		private static float PositionOf(Kingdom them, Kingdom you, War w)
		{
			float num = 0f;
			float num2 = 0f;
			float num4;
			float num5;
			try
			{
				if (w != null)
				{
					bool num3 = w.isAttacker(them);
					num4 = (num3 ? ((float)w.countAttackersWarriors() + 5f * (float)w.countAttackersCities() + 0.05f * (float)w.countAttackersPopulation()) : ((float)w.countDefendersWarriors() + 5f * (float)w.countDefendersCities() + 0.05f * (float)w.countDefendersPopulation()));
					num5 = (num3 ? ((float)w.countDefendersWarriors() + 5f * (float)w.countDefendersCities() + 0.05f * (float)w.countDefendersPopulation()) : ((float)w.countAttackersWarriors() + 5f * (float)w.countAttackersCities() + 0.05f * (float)w.countAttackersPopulation()));
					num = (num3 ? w.getDeadAttackers() : w.getDeadDefenders());
					num2 = (num3 ? w.getDeadDefenders() : w.getDeadAttackers());
				}
				else
				{
					num4 = (float)Royal.Warriors(them) + 5f * (float)Royal.CityCount(them) + 0.05f * (float)Royal.Population(them);
					num5 = (float)Royal.Warriors(you) + 5f * (float)Royal.CityCount(you) + 0.05f * (float)Royal.Population(you);
				}
			}
			catch
			{
				return 0f;
			}
			float num6 = ((num4 + num5 > 0f) ? (num4 / (num4 + num5)) : 0.5f);
			float num7 = (num2 - num) / (num2 + num + 20f);
			return Mathf.Clamp((num6 - 0.5f) * 1.6f + num7 * 0.6f, -1f, 1f);
		}

		private static bool Besieging(Kingdom by, Kingdom of)
		{
			try
			{
				foreach (City city in of.getCities())
				{
					if (city != null && city.isAlive() && city.isGettingCapturedBy(by))
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

		internal static Deal TermsFor(Stance s)
		{
			Deal deal = new Deal
			{
				Them = s.Them,
				You = s.You,
				Level = s.Level,
				D = s.D
			};
			switch (s.Level)
			{
			case Want.TwoTowns:
			case Want.Town:
			{
				int num = ((s.Level != Want.TwoTowns) ? 1 : 2);
				float value = ((s.Level == Want.TwoTowns) ? ((s.D - 70f) / 30f) : ((s.D - 35f) / 35f));
				deal.YouCede.AddRange(NameTowns(s.You, s.Them, num, Mathf.Clamp01(value)));
				deal.YouPay = Round5((float)(num - deal.YouCede.Count) * 300f);
				break;
			}
			case Want.Gold:
				deal.YouPay = Round5(6f * s.D);
				break;
			}
			return deal;
		}

		internal static Deal OfferOf(Stance s)
		{
			Deal deal = new Deal
			{
				Them = s.Them,
				You = s.You,
				Level = s.Level,
				D = s.D
			};
			if (s.D <= -35f)
			{
				deal.TheyCede = TheirTown(s.Them, s.You);
				if (deal.TheyCede != null)
				{
					return deal;
				}
			}
			if (s.D <= -10f)
			{
				deal.TheyPay = GoldTheyGive(s);
			}
			return deal;
		}

		internal static int GoldTheyGive(Stance s)
		{
			int num = Mathf.Min(Treasury(s.Them), Round5(Mathf.Max(60f, 6f * (0f - s.D))));
			if (num < 20)
			{
				return 0;
			}
			return num;
		}

		private static List<City> NameTowns(Kingdom you, Kingdom them, int n, float t)
		{
			List<City> list = Spare(you, them);
			if (list.Count > n + 1)
			{
				list.RemoveRange(n + 1, list.Count - (n + 1));
			}
			list.Sort((City a, City b) => People(a).CompareTo(People(b)));
			int num = Mathf.Clamp(Mathf.RoundToInt(t * (float)(list.Count - n)), 0, Mathf.Max(0, list.Count - n));
			List<City> list2 = new List<City>();
			for (int num2 = num; num2 < list.Count; num2++)
			{
				if (list2.Count >= n)
				{
					break;
				}
				list2.Add(list[num2]);
			}
			return list2;
		}

		internal static List<City> Spare(Kingdom you, Kingdom them)
		{
			List<City> list = new List<City>();
			City city = null;
			City city2 = TreasuryOf(you);
			try
			{
				city = WorldBoxMod.Instance?.Host?.city;
			}
			catch
			{
			}
			try
			{
				foreach (City city3 in you.getCities())
				{
					if (city3 != null && city3.isAlive() && city3 != city && city3 != city2 && !city3.isCapitalCity())
					{
						list.Add(city3);
					}
				}
			}
			catch
			{
			}
			list.Sort((City a, City b) => DistanceTo(a, them).CompareTo(DistanceTo(b, them)));
			return list;
		}

		internal static List<City> InPlaceOf(Deal d, City named)
		{
			List<City> list = new List<City>();
			int num = ((named != null) ? People(named) : 0);
			foreach (City item in Spare(d.You, d.Them))
			{
				if (!d.YouCede.Contains(item) && People(item) >= num)
				{
					list.Add(item);
				}
			}
			return list;
		}

		internal static City TheirTown(Kingdom them, Kingdom you)
		{
			City result = null;
			City city = TreasuryOf(them);
			float num = float.MaxValue;
			try
			{
				foreach (City city2 in them.getCities())
				{
					if (city2 != null && city2.isAlive() && city2 != city && !city2.isCapitalCity())
					{
						float num2 = DistanceTo(city2, you);
						if (num2 < num)
						{
							num = num2;
							result = city2;
						}
					}
				}
			}
			catch
			{
			}
			return result;
		}

		private static float DistanceTo(City c, Kingdom k)
		{
			float num = float.MaxValue;
			try
			{
				Vector2 vector = Royal.Position(c);
				foreach (City city in k.getCities())
				{
					if (city != null && city.isAlive())
					{
						num = Mathf.Min(num, (Royal.Position(city) - vector).sqrMagnitude);
					}
				}
			}
			catch
			{
			}
			return num;
		}

		internal static int People(City c)
		{
			try
			{
				return c.getPopulationPeople();
			}
			catch
			{
				return 0;
			}
		}

		private static int Round5(float v)
		{
			return Mathf.Max(0, Mathf.RoundToInt(v / 5f) * 5);
		}

		internal static City TreasuryOf(Kingdom k)
		{
			try
			{
				if (k == null)
				{
					return null;
				}
				if (k.capital != null && k.capital.isAlive())
				{
					return k.capital;
				}
				City city = ((k.king != null) ? k.king.city : null);
				if (city != null && city.isAlive() && city.kingdom == k)
				{
					return city;
				}
				City city2 = null;
				foreach (City city3 in k.getCities())
				{
					if (city3 != null && city3.isAlive() && (city2 == null || People(city3) > People(city2)))
					{
						city2 = city3;
					}
				}
				return city2;
			}
			catch
			{
				return null;
			}
		}

		internal static int Treasury(Kingdom k)
		{
			try
			{
				return TreasuryOf(k)?.getResourcesAmount("gold") ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		private static int Move(Kingdom from, Kingdom to, int gold)
		{
			if (gold <= 0)
			{
				return 0;
			}
			try
			{
				City city = TreasuryOf(from);
				City city2 = TreasuryOf(to);
				if (city2 != null && !city2.hasStockpiles())
				{
					city2 = null;
				}
				if (city2 == null)
				{
					foreach (City city3 in to.getCities())
					{
						if (city3 != null && city3.isAlive() && city3.hasStockpiles() && (city2 == null || People(city3) > People(city2)))
						{
							city2 = city3;
						}
					}
				}
				if (city == null || city2 == null)
				{
					return 0;
				}
				gold = Mathf.Min(gold, city.getResourcesAmount("gold"));
				if (gold <= 0)
				{
					return 0;
				}
				city.takeResource("gold", gold);
				city2.addResourcesToRandomStockpile("gold", gold);
				return gold;
			}
			catch (Exception ex)
			{
				Log.Error("peace: gold: " + ex.Message);
				return 0;
			}
		}

		private static bool Cede(City c, Kingdom from, Kingdom to)
		{
			try
			{
				if (c == null || !c.isAlive() || to == null || !to.isAlive() || c.kingdom != from)
				{
					return false;
				}
				c.joinAnotherKingdom(to);
				return c.kingdom == to;
			}
			catch (Exception ex)
			{
				Log.Error("peace: ceding " + c?.name + ": " + ex);
				return false;
			}
		}

		internal static bool Seal(Actor host, Deal d, Action<string> toast)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || d == null || !Alive(d.Them) || !Alive(d.You))
			{
				return false;
			}
			if (d.YouPay > 0 && Treasury(d.You) < d.YouPay)
			{
				toast?.Invoke("The treasury holds only " + Treasury(d.You) + " gold.");
				return false;
			}
			if (!Royal.AtWar(d.You, d.Them))
			{
				toast?.Invoke("The war with " + Royal.NameOf(d.Them) + " is already over.");
				return false;
			}
			instance.Royal.MakePeace(host, d.Them, delegate(string m)
			{
				Log.Info("peace: " + m);
			});
			if (Royal.AtWar(d.You, d.Them))
			{
				toast?.Invoke("The envoys could not agree.");
				return false;
			}
			int num = Move(d.You, d.Them, d.YouPay);
			int num2 = Move(d.Them, d.You, d.TheyPay);
			List<string> list = new List<string>();
			foreach (City item in d.YouCede)
			{
				string name = item.name;
				if (Cede(item, d.You, d.Them))
				{
					list.Add(name);
				}
			}
			string text = null;
			if (d.TheyCede != null)
			{
				string name2 = d.TheyCede.name;
				if (Cede(d.TheyCede, d.Them, d.You))
				{
					text = name2;
				}
			}
			if (d.D > 0f && (num > 0 || list.Count > 0))
			{
				Settle(d.Them, d.You, d.D);
			}
			if (d.D < 0f && (num2 > 0 || text != null))
			{
				Settle(d.You, d.Them, 0f - d.D);
			}
			Moods.Clear();
			string text2 = Royal.NameOf(d.Them);
			string text3 = "Peace with " + text2 + ".";
			if (list.Count > 0)
			{
				text3 = text3 + " " + Join(list) + ((list.Count == 1) ? " is theirs now." : " are theirs now.");
			}
			if (num > 0)
			{
				text3 = text3 + " You paid " + num + " gold.";
			}
			if (text != null)
			{
				text3 = text3 + " " + text + " is yours now.";
			}
			if (num2 > 0)
			{
				text3 = text3 + " They paid " + num2 + " gold.";
			}
			toast?.Invoke(text3);
			Log.Info($"peace: sealed with {text2} at {d.D:F0}: you cede [{Join(list)}], pay {num}; they cede [{text}], pay {num2}");
			return true;
		}

		internal static string Join(List<string> names)
		{
			if (names.Count == 0)
			{
				return "";
			}
			if (names.Count == 1)
			{
				return names[0];
			}
			return string.Join(", ", names.GetRange(0, names.Count - 1).ToArray()) + " and " + names[names.Count - 1];
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

		internal static float Offend(Kingdom angry, Kingdom at, float points)
		{
			if (angry == null || at == null || points <= 0f)
			{
				return 0f;
			}
			Dictionary<long, Grudge> dictionary = Read(angry);
			long iD = at.getID();
			double now = Quests.Now;
			if (!dictionary.TryGetValue(iD, out var value))
			{
				Grudge obj = new Grudge
				{
					At = iD,
					T = now,
					WarAt = now
				};
				value = obj;
				dictionary[iD] = obj;
			}
			value.Pts = Held(value, now) + (double)points;
			value.T = now;
			Write(angry, dictionary);
			return (float)value.Pts;
		}

		internal static float WaitFor(Kingdom you, Kingdom them)
		{
			if (you == null || them == null || !RefusedUntil.TryGetValue(PairKey(you, them), out var value))
			{
				return 0f;
			}
			return Mathf.Max(0f, (float)(value - Quests.Now));
		}

		internal static void Ask(Actor host, Kingdom them, Action<string> toast)
		{
			Kingdom kingdom = null;
			try
			{
				kingdom = ((host != null && host.isAlive()) ? host.kingdom : null);
			}
			catch
			{
			}
			if (kingdom == null || !Royal.IsKing(host))
			{
				toast("You no longer rule a kingdom.");
				return;
			}
			if (!Alive(them))
			{
				toast("That kingdom is no more.");
				return;
			}
			if (!Royal.AtWar(kingdom, them))
			{
				toast("You are not at war with " + Royal.NameOf(them) + ".");
				return;
			}
			float num = WaitFor(kingdom, them);
			if (num > 0f)
			{
				toast(Royal.NameOf(them) + " sent your envoy back. Ask again in " + Mathf.CeilToInt(num) + " s.");
				return;
			}
			_asked = them;
			_toast = toast;
		}

		private static void OpenTalk(Kingdom them)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = instance?.Host;
			Action<string> action = _toast ?? ((Action<string>)delegate
			{
			});
			if (actor == null || !Royal.IsKing(actor) || !Alive(them) || !Royal.AtWar(actor.kingdom, them))
			{
				return;
			}
			Actor actor2 = CourtThrone.EnvoyOf(them);
			if (actor2 == null)
			{
				action(Royal.NameOf(them) + " sent no one to talk.");
				return;
			}
			instance.Royal.Open = false;
			if (!instance.StartAudience(actor2))
			{
				action(Royal.NameOf(them) + "'s envoy could not come.");
				return;
			}
			Stance stance = StanceOf(them, actor.kingdom);
			_talk = new Talk
			{
				Them = them,
				You = actor.kingdom,
				Stance = stance,
				Deal = TermsFor(stance)
			};
			Log.Info("peace: you ask " + Royal.NameOf(them) + " for peace: " + Describe(stance));
			Present(instance.Converse, "");
		}

		internal static bool WouldSendEnvoy(Kingdom you, Kingdom them, out bool terms)
		{
			terms = false;
			if (!Alive(you) || !Alive(them) || !Royal.AtWar(you, them))
			{
				return false;
			}
			Stance stance = StanceOf(them, you);
			if (stance.D <= -10f)
			{
				return true;
			}
			if (stance.D < 10f)
			{
				if (!(stance.Strikes < -5f) && !(stance.Wear > 0f))
				{
					return stance.Position < -0.2f;
				}
				return true;
			}
			if (stance.D < 100f && stance.YouStarted && stance.Position > 0.3f)
			{
				terms = true;
				return true;
			}
			return false;
		}

		internal static void HearEnvoy(Conversation c, Kingdom them, bool terms)
		{
			Actor you = c.You;
			Kingdom kingdom = null;
			try
			{
				kingdom = you.kingdom;
			}
			catch
			{
			}
			string text = KingOf(them);
			if (!Alive(them) || kingdom == null || !Royal.AtWar(kingdom, them))
			{
				_talk = new Talk
				{
					Them = them,
					You = kingdom,
					AtCourt = true
				};
				End(c, "The war is already over. I'll take that home.");
				return;
			}
			Stance stance = StanceOf(them, kingdom);
			Talk talk = (_talk = new Talk
			{
				Them = them,
				You = kingdom,
				Stance = stance,
				AtCourt = true
			});
			Log.Info("peace: " + Royal.NameOf(them) + "'s envoy at court (" + (terms ? "terms" : "offer") + "): " + Describe(stance));
			if (stance.Level == Want.Refuse)
			{
				End(c, "Your Majesty, " + text + " has changed his mind. There will be no peace.");
			}
			else if (stance.D >= 10f)
			{
				talk.Deal = TermsFor(stance);
				Present(c, "Your Majesty, " + text + " sends his terms. ");
			}
			else
			{
				talk.Deal = OfferOf(stance);
				talk.Offered = true;
				Present(c, "Your Majesty, " + text + " asks for peace. ");
			}
		}

		private static void Present(Conversation c, string lead)
		{
			Talk talk = _talk;
			if (talk == null)
			{
				return;
			}
			Deal deal = talk.Deal;
			c.LastTopic = "peace_talk";
			List<Choice> list = new List<Choice>();
			string text;
			if (talk.Stance.Level == Want.Refuse)
			{
				text = RefusalLine(talk.Stance);
				RefusedUntil[PairKey(talk.You, talk.Them)] = Quests.Now + 60.0;
				list.Add(new Choice("Then the war goes on.", delegate
				{
					End(c, Dialogue.Pick("So it does.", "*bows stiffly* I'll tell my king."));
				}, ChoiceTone.Leave));
				Toast(Royal.NameOf(talk.Them) + " refuses peace. Ask again in a year.");
			}
			else if (deal.YouCede.Count > 0 || deal.YouPay > 0)
			{
				List<string> list2 = new List<string>();
				foreach (City item in deal.YouCede)
				{
					list2.Add(item.name);
				}
				if (deal.YouPay > 0)
				{
					list2.Add(deal.YouPay + " gold");
				}
				text = "My king's price is " + Join(list2) + ". " + ((deal.YouCede.Count > 0) ? ("Cede " + ((deal.YouCede.Count == 1) ? "it" : "them") + " and the war is over.") : "Pay it and the war is over.");
				List<string> list3 = new List<string>();
				if (deal.YouCede.Count > 0)
				{
					list3.Add("Cede " + Join(Names(deal.YouCede)));
				}
				if (deal.YouPay > 0)
				{
					list3.Add(((deal.YouCede.Count > 0) ? "pay " : "Pay ") + deal.YouPay + " gold");
				}
				Choice choice = new Choice(string.Join(" and ", list3.ToArray()), delegate
				{
					Accept(c);
				}, ChoiceTone.Quest);
				int num = Treasury(talk.You);
				if (deal.YouPay > num)
				{
					choice.Off("The treasury holds only " + num + " gold.");
				}
				list.Add(choice);
				foreach (City item2 in deal.YouCede)
				{
					City named = item2;
					if (InPlaceOf(deal, named).Count > 0)
					{
						list.Add(new Choice("Offer another town" + ((deal.YouCede.Count > 1) ? (" for " + named.name) : ""), delegate
						{
							OfferInstead(c, named);
						}));
					}
				}
				if (deal.YouPay > 0 && InPlaceOf(deal, null).Count > 0)
				{
					list.Add(new Choice("Offer a town instead of gold", delegate
					{
						OfferInstead(c, null);
					}));
				}
				list.Add(new Choice("Refuse", delegate
				{
					End(c, Dialogue.Pick("As you wish. The war goes on.", "Then we have nothing more to say."));
				}, ChoiceTone.Risk));
			}
			else if (talk.Offered && (deal.TheyPay > 0 || deal.TheyCede != null))
			{
				string text2 = ((deal.TheyCede != null) ? deal.TheyCede.name : (deal.TheyPay + " gold"));
				text = "He offers " + text2 + " to end it.";
				list.Add(new Choice("Accept peace and " + text2, delegate
				{
					Accept(c);
				}, ChoiceTone.Quest));
				City city = TheirTown(talk.Them, talk.You);
				if (deal.TheyCede == null && city != null && !talk.Refused.Contains("town"))
				{
					list.Add(new Choice("Ask for " + city.name + " instead", delegate
					{
						Demand(c, "town");
					}));
				}
				list.Add(new Choice("Refuse", delegate
				{
					End(c, Dialogue.Pick("As you wish. I'll tell my king.", "*bows stiffly* Then I take your answer home."));
				}, ChoiceTone.Risk));
			}
			else
			{
				text = (talk.AtCourt ? "My king wants an end to this war." : ((talk.Stance.D <= -35f) ? "My king knows he is beaten. Name your terms." : ((talk.Stance.D <= -10f) ? "My king wants an end to it. Name your terms." : "My king will take peace as it stands.")));
				list.Add(new Choice(talk.AtCourt ? "Accept peace" : "Peace", delegate
				{
					Accept(c);
				}, ChoiceTone.Quest));
				int gold = GoldTheyGive(talk.Stance);
				if (gold <= 0)
				{
					gold = Mathf.Min(Treasury(talk.Them), 60);
				}
				if (gold >= 20 && !talk.Refused.Contains("gold"))
				{
					list.Add(new Choice("Peace, and " + gold + " gold", delegate
					{
						Demand(c, "gold", gold);
					}));
				}
				City city2 = TheirTown(talk.Them, talk.You);
				if (city2 != null && !talk.Refused.Contains("town"))
				{
					list.Add(new Choice("Peace, and " + city2.name, delegate
					{
						Demand(c, "town");
					}));
				}
				list.Add(new Choice(talk.AtCourt ? "Refuse" : "Not yet", delegate
				{
					End(c, Dialogue.Pick("As you wish. The war goes on.", "Then I'll take that home."));
				}, ChoiceTone.Risk));
			}
			c.Say(lead + text);
			c.Offer(list);
		}

		private static string RefusalLine(Stance s)
		{
			string text = null;
			float num = 0f;
			foreach (Why reason in s.Reasons)
			{
				if (reason.Points > num)
				{
					num = reason.Points;
					text = reason.Text;
				}
			}
			switch (text)
			{
			case "Winning big":
				return "No peace. We are winning this war, and my king knows it.";
			case "You broke a truce":
				return "No peace. You broke your word once. My king won't take it again.";
			case "You started this war":
				return "No peace. You started this war. My king means to finish it.";
			case "You made war on them before":
				return "No peace. Not with you, not again.";
			default:
				return "No peace. Not now.";
			}
		}

		private static void Accept(Conversation c)
		{
			Talk talk = _talk;
			if (talk != null && !Over(c, talk))
			{
				if (!Seal(c.You, talk.Deal, Toast))
				{
					Present(c, "");
					return;
				}
				End(c, Dialogue.Pick("Then it's peace. My king will keep his word.", "Peace, then. I'll ride home with the news."));
			}
		}

		private static void Demand(Conversation c, string what, int gold = 0)
		{
			Talk talk = _talk;
			if (talk == null || Over(c, talk))
			{
				return;
			}
			float d = talk.Stance.D;
			if (what == "town")
			{
				City city = TheirTown(talk.Them, talk.You);
				if (city != null && d <= -35f)
				{
					talk.Deal = new Deal
					{
						Them = talk.Them,
						You = talk.You,
						Level = talk.Stance.Level,
						D = d,
						TheyCede = city
					};
					if (Seal(c.You, talk.Deal, Toast))
					{
						End(c, Dialogue.Pick(city.name + " is yours. Let this be the end of it.", "*a long breath* " + city.name + ", then. It's peace."));
						return;
					}
				}
				talk.Refused.Add("town");
				Present(c, ((city != null) ? ("My king won't give up " + city.name + ". ") : "") + ((d <= -10f) ? "Gold, perhaps. " : ""));
				return;
			}
			if (d <= -10f && Treasury(talk.Them) >= gold)
			{
				talk.Deal = new Deal
				{
					Them = talk.Them,
					You = talk.You,
					Level = talk.Stance.Level,
					D = d,
					TheyPay = gold
				};
				if (Seal(c.You, talk.Deal, Toast))
				{
					End(c, Dialogue.Pick($"{gold} gold. It will be sent. Peace.", "The gold is yours. Peace."));
					return;
				}
			}
			talk.Refused.Add("gold");
			Present(c, "My king pays nothing for a war he isn't losing. ");
		}

		private static void OfferInstead(Conversation c, City named)
		{
			Talk t = _talk;
			if (t == null)
			{
				return;
			}
			List<Choice> list = new List<Choice>();
			foreach (City item in InPlaceOf(t.Deal, named))
			{
				if (list.Count >= 6)
				{
					break;
				}
				City offered = item;
				list.Add(new Choice(offered.name + " (" + People(offered) + " people)", delegate
				{
					Deal deal = t.Deal;
					if (named != null)
					{
						deal.YouCede[deal.YouCede.IndexOf(named)] = offered;
					}
					else
					{
						deal.YouCede.Add(offered);
						deal.YouPay = 0;
					}
					Log.Info("peace: you offer " + offered.name + " in place of " + ((named != null) ? named.name : "the gold"));
					Present(c, Dialogue.Pick(offered.name + ", then. ", "*nods* " + offered.name + " will do. "));
				}));
			}
			list.Add(new Choice("Back", delegate
			{
				Present(c, "");
			}, ChoiceTone.Back));
			c.LastTopic = "peace_talk";
			c.Say((named != null) ? ("Which town, in place of " + named.name + "? It must be as big.") : "Which town, in place of the gold?");
			c.Offer(list);
		}

		private static bool Over(Conversation c, Talk t)
		{
			if (Royal.AtWar(t.You, t.Them))
			{
				return false;
			}
			End(c, Dialogue.Pick("*shakes their head* The war's over already. There's nothing left to sign.", "There is no war now. My king has nothing to sign."));
			return true;
		}

		private static void End(Conversation c, string said)
		{
			bool flag = _talk != null && _talk.AtCourt;
			_talk = null;
			c.LastTopic = (flag ? "envoy" : "peace");
			c.EndAfter = true;
			c.Say(said);
		}

		private static void Toast(string m)
		{
			if (_toast != null)
			{
				_toast(m);
			}
			else
			{
				WorldBoxMod.Instance?.Toast(m);
			}
		}

		private static string KingOf(Kingdom k)
		{
			try
			{
				if (k.king != null)
				{
					return "King " + Dialogue.NameOf(k.king) + " of " + Royal.NameOf(k);
				}
			}
			catch
			{
			}
			return "the crown of " + Royal.NameOf(k);
		}

		private static List<string> Names(List<City> towns)
		{
			List<string> list = new List<string>();
			foreach (City town in towns)
			{
				list.Add(town.name);
			}
			return list;
		}

		internal static string Describe(Stance s)
		{
			List<string> list = new List<string>();
			foreach (Why reason in s.Reasons)
			{
				list.Add(reason.Text + ((reason.Points >= 0f) ? " +" : " ") + reason.Points.ToString("F0"));
			}
			return $"{s.Level} at {s.D:F0} (position {s.Position:F2}): " + string.Join(", ", list.ToArray());
		}

		internal static Mood MoodOf(Kingdom them, Kingdom you)
		{
			string key = PairKey(you, them);
			if (Moods.TryGetValue(key, out var value) && Time.unscaledTime - value.At < 1f)
			{
				return value;
			}
			value = new Mood
			{
				At = Time.unscaledTime
			};
			Stance stance = StanceOf(them, you);
			switch (stance.Level)
			{
			case Want.Refuse:
				value.Word = "Furious";
				value.Color = new Color(1f, 0.35f, 0.3f);
				break;
			case Want.TwoTowns:
			case Want.Town:
				value.Word = "Bitter";
				value.Color = new Color(1f, 0.6f, 0.3f);
				break;
			case Want.Gold:
				value.Word = "Wary";
				value.Color = new Color(1f, 0.85f, 0.4f);
				break;
			case Want.Even:
				value.Word = "Willing";
				value.Color = new Color(0.75f, 1f, 0.6f);
				break;
			default:
				value.Word = "Beaten";
				value.Color = new Color(0.45f, 1f, 0.5f);
				break;
			}
			Deal deal = TermsFor(stance);
			string key2;
			switch (stance.Level)
			{
			case Want.Refuse:
				key2 = "Won't make peace";
				break;
			case Want.Even:
				key2 = "Will take peace as it stands";
				break;
			case Want.TheyPay:
				key2 = "Would pay to end it";
				break;
			case Want.TheyCede:
				key2 = ((TheirTown(them, you) != null) ? "Would give up a town" : "Would pay to end it");
				break;
			default:
			{
				List<string> list = Names(deal.YouCede);
				if (deal.YouPay > 0)
				{
					list.Add(deal.YouPay + " gold");
				}
				key2 = "Wants " + Join(list);
				break;
			}
			}
			value.Lines.Add(new KeyValuePair<string, Color>(key2, Plain));
			float num = WaitFor(you, them);
			if (num > 0f)
			{
				value.Lines.Add(new KeyValuePair<string, Color>("Will hear you again in " + Mathf.CeilToInt(num) + " s", Plain));
			}
			foreach (Why reason in stance.Reasons)
			{
				value.Lines.Add(new KeyValuePair<string, Color>(reason.Text, (reason.Points > 0f) ? Hard : Soft));
			}
			Moods[key] = value;
			return value;
		}
	}
}
