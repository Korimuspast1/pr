using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Court
	{
		private sealed class Table
		{
			public readonly Actor[] Holder = new Actor[5];

			public readonly long[] Ids = new long[5];

			public readonly bool[] Chosen = new bool[5];

			public float At = -99f;
		}

		private sealed class Nudge
		{
			public string Trait;

			public string Line;

			public float Price = 1f;

			public float Levy = 1f;

			public float Skim;

			public float Tribute = 1f;

			public float Stronger = 1f;

			public int Alliance;
		}

		public const string Key = "fp_court";

		public const string ChampionKey = "fp_champion";

		public static readonly Seat[] Seats = new Seat[5]
		{
			Seat.Marshal,
			Seat.Steward,
			Seat.Chancellor,
			Seat.Spymaster,
			Seat.Mage
		};

		private static readonly Dictionary<Kingdom, Table> Tables = new Dictionary<Kingdom, Table>();

		private const float Refresh = 3f;

		private static readonly Nudge[] MarshalNudges = new Nudge[6]
		{
			new Nudge
			{
				Trait = "pyromaniac",
				Price = 0.85f,
				Line = "Pyromaniac: war machines cost 15% less"
			},
			new Nudge
			{
				Trait = "veteran",
				Price = 0.9f,
				Line = "Veteran: war machines cost 10% less"
			},
			new Nudge
			{
				Trait = "genius",
				Price = 0.9f,
				Line = "Genius: war machines cost 10% less"
			},
			new Nudge
			{
				Trait = "pacifist",
				Price = 1.15f,
				Levy = 0.7f,
				Line = "Pacifist: war machines cost 15% more, the levy drafts fewer"
			},
			new Nudge
			{
				Trait = "weak",
				Levy = 0.7f,
				Line = "Weak: the levy drafts fewer"
			},
			new Nudge
			{
				Trait = "stupid",
				Price = 1.1f,
				Line = "Stupid: war machines cost 10% more"
			}
		};

		private static readonly Nudge[] StewardNudges = new Nudge[3]
		{
			new Nudge
			{
				Trait = "greedy",
				Tribute = 1.2f,
				Skim = 0.25f,
				Line = "Greedy: 20% more tribute, keeps a quarter of each gift"
			},
			new Nudge
			{
				Trait = "genius",
				Tribute = 1.1f,
				Line = "Genius: 10% more tribute"
			},
			new Nudge
			{
				Trait = "stupid",
				Tribute = 0.9f,
				Line = "Stupid: 10% less tribute"
			}
		};

		private static readonly Nudge[] ChancellorNudges = new Nudge[8]
		{
			new Nudge
			{
				Trait = "honest",
				Alliance = -15,
				Line = "Honest: alliances need less trust"
			},
			new Nudge
			{
				Trait = "wise",
				Alliance = -10,
				Line = "Wise: alliances need less trust"
			},
			new Nudge
			{
				Trait = "pacifist",
				Tribute = 0.75f,
				Stronger = 1.15f,
				Line = "Pacifist: softer tribute demands, stronger realms may pay"
			},
			new Nudge
			{
				Trait = "peaceful",
				Tribute = 0.75f,
				Stronger = 1.15f,
				Line = "Peaceful: softer tribute demands, stronger realms may pay"
			},
			new Nudge
			{
				Trait = "bloodlust",
				Tribute = 1.25f,
				Line = "Bloodlust: tribute demands 25% larger"
			},
			new Nudge
			{
				Trait = "ambitious",
				Tribute = 1.25f,
				Line = "Ambitious: tribute demands 25% larger"
			},
			new Nudge
			{
				Trait = "hotheaded",
				Alliance = 15,
				Line = "Hotheaded: alliances need more trust"
			},
			new Nudge
			{
				Trait = "stupid",
				Alliance = 15,
				Line = "Stupid: alliances need more trust"
			}
		};

		private static readonly Nudge[] SpymasterNudges = new Nudge[4]
		{
			new Nudge
			{
				Trait = "deceitful",
				Price = 0.85f,
				Line = "Deceitful: agents cost 15% less"
			},
			new Nudge
			{
				Trait = "thief",
				Price = 0.9f,
				Line = "Thief: agents cost 10% less"
			},
			new Nudge
			{
				Trait = "ambitious",
				Price = 0.9f,
				Line = "Ambitious: agents cost 10% less"
			},
			new Nudge
			{
				Trait = "honest",
				Price = 1.15f,
				Line = "Honest: agents cost 15% more"
			}
		};

		private static readonly Nudge[] MageNudges = new Nudge[4]
		{
			new Nudge
			{
				Trait = "heart_of_wizard",
				Price = 0.85f,
				Line = "Wizard's heart: the heavens cost 15% less"
			},
			new Nudge
			{
				Trait = "genius",
				Price = 0.9f,
				Line = "Genius: the heavens cost 10% less"
			},
			new Nudge
			{
				Trait = "wise",
				Price = 0.9f,
				Line = "Wise: the heavens cost 10% less"
			},
			new Nudge
			{
				Trait = "stupid",
				Price = 1.1f,
				Line = "Stupid: the heavens cost 10% more"
			}
		};

		private static readonly Nudge NoNudge = new Nudge();

		private const float TableReach = 1.8f;

		private const float ThroneReach = 1.6f;

		private const float ChairReach = 0.9f;

		internal static bool? SessionForTest;

		internal static bool? GatedForTest;

		internal static Seat? Focus;

		internal static float FocusAt = -99f;

		private const float CouncilFrom = 0.38f;

		private const float CouncilTo = 0.46f;

		public static string Title(Seat s)
		{
			switch (s)
			{
			case Seat.Marshal:
				return "Marshal";
			case Seat.Steward:
				return "Steward";
			case Seat.Chancellor:
				return "Chancellor";
			case Seat.Spymaster:
				return "Spymaster";
			default:
				return "Court mage";
			}
		}

		public static string Duty(Seat s)
		{
			switch (s)
			{
			case Seat.Marshal:
				return "the army and the war machines";
			case Seat.Steward:
				return "gifts, tribute and the levy";
			case Seat.Chancellor:
				return "war, peace and alliances";
			case Seat.Spymaster:
				return "the agents";
			default:
				return "the heavens";
			}
		}

		private static string Word(Seat s)
		{
			return s.ToString().ToLowerInvariant();
		}

		public static Actor Holder(Kingdom k, Seat s)
		{
			Table table = TableOf(k);
			if (table == null)
			{
				return null;
			}
			return table.Holder[(int)s];
		}

		public static bool Appointed(Kingdom k, Seat s)
		{
			Table table = TableOf(k);
			if (table != null)
			{
				return table.Chosen[(int)s];
			}
			return false;
		}

		public static bool SeatOf(Actor a, out Seat seat)
		{
			seat = Seat.Marshal;
			Table table = TableOf(KingdomOf(a));
			if (table == null)
			{
				return false;
			}
			for (int i = 0; i < 5; i++)
			{
				if (table.Holder[i] == a)
				{
					seat = (Seat)i;
					return true;
				}
			}
			return false;
		}

		public static bool Open(Kingdom k, Seat s)
		{
			return Holder(k, s) != null;
		}

		public static void Invalidate(Kingdom k = null)
		{
			if (k == null)
			{
				Tables.Clear();
			}
			else
			{
				Tables.Remove(k);
			}
		}

		private static Table TableOf(Kingdom k)
		{
			if (k == null)
			{
				return null;
			}
			try
			{
				if (!k.isAlive())
				{
					return null;
				}
			}
			catch
			{
				return null;
			}
			if (!Tables.TryGetValue(k, out var value))
			{
				if (Tables.Count > 32)
				{
					Tables.Clear();
				}
				value = (Tables[k] = new Table());
			}
			if (Time.unscaledTime - value.At < 3f && Valid(value, k))
			{
				return value;
			}
			Fill(k, value);
			return value;
		}

		private static bool Valid(Table t, Kingdom k)
		{
			for (int i = 0; i < 5; i++)
			{
				Actor actor = t.Holder[i];
				if (actor != null && (!Fit(actor, k) || IdOf(actor) != t.Ids[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static bool Fit(Actor a, Kingdom k)
		{
			try
			{
				return a != null && a.isAlive() && a.kingdom == k && k.king != a && a.isAdult() && Dialogue.IsPerson(a);
			}
			catch
			{
				return false;
			}
		}

		private static void Fill(Kingdom k, Table t)
		{
			t.At = Time.unscaledTime;
			HashSet<Actor> hashSet = new HashSet<Actor>();
			Dictionary<Seat, long> dictionary = ReadAppointments(k);
			Seat[] seats = Seats;
			foreach (Seat seat in seats)
			{
				int num = (int)seat;
				t.Holder[num] = null;
				t.Chosen[num] = false;
				if (dictionary.TryGetValue(seat, out var value) && value > 0)
				{
					Actor actor = Find(value);
					if (Fit(actor, k) && !hashSet.Contains(actor))
					{
						t.Holder[num] = actor;
						t.Ids[num] = IdOf(actor);
						t.Chosen[num] = true;
						hashSet.Add(actor);
					}
				}
			}
			List<Actor> pool = People(k, capitalOnly: true);
			List<Actor> list = null;
			seats = new Seat[5]
			{
				Seat.Marshal,
				Seat.Chancellor,
				Seat.Steward,
				Seat.Spymaster,
				Seat.Mage
			};
			foreach (Seat seat2 in seats)
			{
				int num2 = (int)seat2;
				if (t.Holder[num2] != null)
				{
					continue;
				}
				Actor actor2 = ByRule(k, seat2, pool, hashSet);
				if (actor2 == null)
				{
					if (list == null)
					{
						list = People(k, capitalOnly: false);
					}
					actor2 = ByRule(k, seat2, list, hashSet);
				}
				t.Holder[num2] = actor2;
				t.Ids[num2] = ((actor2 != null) ? IdOf(actor2) : (-1));
				if (actor2 != null)
				{
					hashSet.Add(actor2);
				}
			}
		}

		private static List<Actor> People(Kingdom k, bool capitalOnly)
		{
			List<Actor> list = new List<Actor>();
			try
			{
				if (capitalOnly)
				{
					City capital = k.capital;
					if (capital == null || !capital.isAlive() || capital.units == null)
					{
						return list;
					}
					foreach (Actor unit in capital.units)
					{
						if (Fit(unit, k))
						{
							list.Add(unit);
						}
					}
				}
				else
				{
					foreach (Actor unit2 in k.getUnits())
					{
						if (Fit(unit2, k))
						{
							list.Add(unit2);
						}
					}
				}
			}
			catch
			{
			}
			return list;
		}

		private static Actor ByRule(Kingdom k, Seat s, List<Actor> pool, HashSet<Actor> taken)
		{
			switch (s)
			{
			case Seat.Marshal:
			{
				Actor actor3 = Champion(k);
				if (actor3 != null && !taken.Contains(actor3))
				{
					return actor3;
				}
				Actor actor4 = null;
				try
				{
					City capital = k.capital;
					if (capital != null && capital.hasArmy() && capital.army.hasCaptain())
					{
						actor4 = capital.army.getCaptain();
					}
				}
				catch
				{
				}
				if (Fit(actor4, k) && !taken.Contains(actor4))
				{
					return actor4;
				}
				return Best(pool, taken, (Actor a) => (a.isWarrior() ? 10000f : 0f) - (Has(a, "pacifist") ? 20000f : 0f) + Kills(a) + Level(a) * 5f);
			}
			case Seat.Steward:
				return Best(pool, taken, (Actor a) => Money(a) + Age(a) * 0.01f);
			case Seat.Chancellor:
			{
				Actor actor = null;
				try
				{
					actor = SuccessionTool.findNextHeir(k, k.king);
				}
				catch
				{
				}
				if (Fit(actor, k) && !taken.Contains(actor) && pool.Contains(actor))
				{
					return actor;
				}
				Actor actor2 = null;
				try
				{
					if (k.capital != null)
					{
						actor2 = k.capital.leader;
					}
				}
				catch
				{
				}
				if (Fit(actor2, k) && !taken.Contains(actor2) && pool.Contains(actor2))
				{
					return actor2;
				}
				return Best(pool, taken, (Actor a) => (Has(a, "wise") ? 200f : 0f) + (Has(a, "genius") ? 150f : 0f) + Age(a));
			}
			case Seat.Spymaster:
				return Best(pool, taken, (Actor a) => (Has(a, "deceitful") ? 300f : 0f) + (Has(a, "thief") ? 200f : 0f) + (Has(a, "ambitious") ? 200f : 0f) + (Has(a, "paranoid") ? 100f : 0f) - (Has(a, "honest") ? 300f : 0f) - Age(a) * 0.01f);
			default:
			{
				Religion faith = null;
				try
				{
					faith = k.getReligion();
				}
				catch
				{
				}
				return Best(pool, taken, delegate(Actor a)
				{
					float num = Age(a);
					try
					{
						if (a.hasSpells() || a.canUseReligionSpells())
						{
							num += 600f;
						}
					}
					catch
					{
					}
					if (Has(a, "heart_of_wizard"))
					{
						num += 500f;
					}
					try
					{
						if (faith != null && a.religion == faith)
						{
							num += 300f;
						}
					}
					catch
					{
					}
					if (Has(a, "wise"))
					{
						num += 100f;
					}
					if (Has(a, "genius"))
					{
						num += 100f;
					}
					return num;
				});
			}
			}
		}

		private static Actor Best(List<Actor> pool, HashSet<Actor> taken, Func<Actor, float> score)
		{
			Actor result = null;
			float num = float.MinValue;
			foreach (Actor item in pool)
			{
				if (!taken.Contains(item))
				{
					float num2;
					try
					{
						num2 = score(item);
					}
					catch
					{
						continue;
					}
					if (num2 > num)
					{
						num = num2;
						result = item;
					}
				}
			}
			return result;
		}

		private static Actor Champion(Kingdom k)
		{
			long result = -1L;
			try
			{
				k.data.get("fp_champion", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					long.TryParse(pResult, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
				}
				if (result <= 0)
				{
					k.data.get("fp_champion", out result, -1L);
				}
				if (result <= 0)
				{
					k.data.get("fp_champion", out var pResult2, -1);
					result = pResult2;
				}
			}
			catch
			{
			}
			Actor actor = ((result > 0) ? Find(result) : null);
			if (!Fit(actor, k))
			{
				return null;
			}
			return actor;
		}

		public static bool Appoint(Kingdom k, Seat s, Actor a)
		{
			if (k == null || !Fit(a, k))
			{
				return false;
			}
			Actor actor = Holder(k, s);
			Seat seat;
			bool flag = SeatOf(a, out seat);
			Dictionary<Seat, long> dictionary = ReadAppointments(k);
			long num = IdOf(a);
			Seat[] seats = Seats;
			foreach (Seat key in seats)
			{
				if (dictionary.TryGetValue(key, out var value) && value == num)
				{
					dictionary.Remove(key);
				}
			}
			dictionary[s] = num;
			WriteAppointments(k, dictionary);
			Invalidate(k);
			Actor actor2 = null;
			try
			{
				actor2 = k.king;
			}
			catch
			{
			}
			if (actor != null && actor != a && actor2 != null && !SeatOf(actor, out seat))
			{
				Chronicle.ChangeOpinion(actor, actor2, -15);
			}
			if (!flag && actor2 != null)
			{
				Chronicle.ChangeOpinion(a, actor2, 10);
			}
			Log.Info("court: " + WorldBoxMod.Describe(a) + " is " + Title(s) + " of " + Royal.NameOf(k) + ((actor != null && actor != a) ? (", in place of " + WorldBoxMod.Describe(actor)) : ""));
			return true;
		}

		private static Dictionary<Seat, long> ReadAppointments(Kingdom k)
		{
			Dictionary<Seat, long> dictionary = new Dictionary<Seat, long>();
			string pResult = null;
			try
			{
				k.data.get("fp_court", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return dictionary;
			}
			string[] array = pResult.Split('|');
			foreach (string text in array)
			{
				int num = text.IndexOf(':');
				if (num > 0 && TryParse(text.Substring(0, num), out var s) && long.TryParse(text.Substring(num + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					dictionary[s] = result;
				}
			}
			return dictionary;
		}

		private static void WriteAppointments(Kingdom k, Dictionary<Seat, long> map)
		{
			StringBuilder stringBuilder = new StringBuilder();
			Seat[] seats = Seats;
			foreach (Seat seat in seats)
			{
				if (map.TryGetValue(seat, out var value))
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('|');
					}
					stringBuilder.Append(Word(seat)).Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
				}
			}
			try
			{
				if (stringBuilder.Length == 0)
				{
					k.data.removeString("fp_court");
				}
				else
				{
					k.data.set("fp_court", stringBuilder.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("court: saving the council: " + ex.Message);
			}
		}

		private static bool TryParse(string word, out Seat s)
		{
			Seat[] seats = Seats;
			foreach (Seat seat in seats)
			{
				if (Word(seat) == word)
				{
					s = seat;
					return true;
				}
			}
			s = Seat.Marshal;
			return false;
		}

		internal static Kingdom KingdomOf(Actor a)
		{
			try
			{
				return (a != null && a.isAlive()) ? a.kingdom : null;
			}
			catch
			{
				return null;
			}
		}

		internal static long IdOf(Actor a)
		{
			try
			{
				return a.data.id;
			}
			catch
			{
				return -1L;
			}
		}

		private static Actor Find(long id)
		{
			try
			{
				return World.world.units.get(id);
			}
			catch
			{
				return null;
			}
		}

		internal static bool Has(Actor a, string trait)
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

		private static float Age(Actor a)
		{
			try
			{
				return a.getAge();
			}
			catch
			{
				return 0f;
			}
		}

		private static float Money(Actor a)
		{
			try
			{
				return a.money;
			}
			catch
			{
				return 0f;
			}
		}

		private static float Kills(Actor a)
		{
			try
			{
				return a.data.kills;
			}
			catch
			{
				return 0f;
			}
		}

		private static float Level(Actor a)
		{
			try
			{
				return a.data.level;
			}
			catch
			{
				return 0f;
			}
		}

		private static Nudge[] NudgesOf(Seat s)
		{
			switch (s)
			{
			case Seat.Marshal:
				return MarshalNudges;
			case Seat.Steward:
				return StewardNudges;
			case Seat.Chancellor:
				return ChancellorNudges;
			case Seat.Spymaster:
				return SpymasterNudges;
			default:
				return MageNudges;
			}
		}

		private static Nudge NudgeOf(Kingdom k, Seat s)
		{
			Actor actor = Holder(k, s);
			if (actor == null)
			{
				return NoNudge;
			}
			Nudge[] array = NudgesOf(s);
			foreach (Nudge nudge in array)
			{
				if (Has(actor, nudge.Trait))
				{
					return nudge;
				}
			}
			return NoNudge;
		}

		public static string EffectLine(Kingdom k, Seat s)
		{
			return NudgeOf(k, s).Line;
		}

		public static Seat SeatFor(Royal.Deed d)
		{
			for (int i = 0; i < Royal.DeedRows.Length; i++)
			{
				Royal.Deed[] array = Royal.DeedRows[i];
				for (int j = 0; j < array.Length; j++)
				{
					if (array[j] == d)
					{
						switch (i)
						{
						default:
							return Seat.Marshal;
						case 1:
							return Seat.Mage;
						case 0:
							return Seat.Spymaster;
						}
					}
				}
			}
			return Seat.Marshal;
		}

		public static int Price(Kingdom k, Royal.Deed d, int basePrice)
		{
			float num = ((k != null) ? NudgeOf(k, SeatFor(d)).Price : 1f);
			return Mathf.Max(1, Mathf.RoundToInt((float)basePrice * num));
		}

		public static float LevyShare(Kingdom k)
		{
			return NudgeOf(k, Seat.Marshal).Levy;
		}

		public static float GiftSkim(Kingdom k)
		{
			return NudgeOf(k, Seat.Steward).Skim;
		}

		public static float TributeScale(Kingdom k)
		{
			return NudgeOf(k, Seat.Steward).Tribute * NudgeOf(k, Seat.Chancellor).Tribute;
		}

		public static float TributeReach(Kingdom k)
		{
			return NudgeOf(k, Seat.Chancellor).Stronger;
		}

		public static int AllianceShift(Kingdom k)
		{
			return NudgeOf(k, Seat.Chancellor).Alliance;
		}

		public static Building HallOf(City c)
		{
			try
			{
				if (c == null || !c.isAlive() || c.buildings_dict_type == null)
				{
					return null;
				}
				if (!c.buildings_dict_type.TryGetValue("type_hall", out var value) || value == null)
				{
					return null;
				}
				foreach (Building item in value)
				{
					if (item != null && item.isAlive() && !item.isRuin() && !item.isUnderConstruction())
					{
						return item;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		public static Building CapitalHall(Kingdom k)
		{
			try
			{
				return (k != null && k.hasCapital()) ? HallOf(k.capital) : null;
			}
			catch
			{
				return null;
			}
		}

		public static bool Hosts(Building b, Kingdom k)
		{
			if (b == null || k == null)
			{
				return false;
			}
			Building building = CapitalHall(k);
			if (building != null)
			{
				return b == building;
			}
			try
			{
				City city = b.city;
				return city != null && city.kingdom == k && HallOf(city) == b;
			}
			catch
			{
				return false;
			}
		}

		public static bool Gated(Kingdom k)
		{
			if (GatedForTest.HasValue)
			{
				return GatedForTest.Value;
			}
			if (k == null)
			{
				return false;
			}
			try
			{
				Building building = CapitalHall(k);
				if (building != null)
				{
					return HallRooms.CanEnter(building);
				}
				foreach (City city in k.cities)
				{
					Building building2 = HallOf(city);
					if (building2 != null && HallRooms.CanEnter(building2))
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

		public static bool InSession(Actor host)
		{
			if (SessionForTest.HasValue)
			{
				return SessionForTest.Value;
			}
			Kingdom kingdom = KingdomOf(host);
			if (kingdom == null)
			{
				return false;
			}
			if (!Gated(kingdom))
			{
				return true;
			}
			HouseInterior houseInterior = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Interior : null);
			if (houseInterior == null || !houseInterior.Inside || houseInterior.Virtual || houseInterior.Room == null || !Hosts(houseInterior.House, kingdom))
			{
				return false;
			}
			return AtTable(houseInterior.Room, houseInterior.Eye);
		}

		private static bool AtTable(Interior room, Vector2 eye)
		{
			Anchors.Spot[] array = HallRooms.Spots(room, "a_war_table");
			Anchors.Spot[] array2 = HallRooms.Spots(room, "a_seat");
			if (array.Length == 0 && array2.Length == 0)
			{
				return true;
			}
			if (!Near(eye, array, 1.8f))
			{
				return Near(eye, array2, 0.9f);
			}
			return true;
		}

		public static bool AtThrone(Actor host)
		{
			if (SessionForTest.HasValue)
			{
				return SessionForTest.Value;
			}
			Kingdom kingdom = KingdomOf(host);
			if (kingdom == null)
			{
				return false;
			}
			if (!Gated(kingdom))
			{
				return true;
			}
			HouseInterior houseInterior = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Interior : null);
			if (houseInterior == null || !houseInterior.Inside || houseInterior.Virtual || houseInterior.Room == null || !Hosts(houseInterior.House, kingdom))
			{
				return false;
			}
			Anchors.Spot[] array = HallRooms.Spots(houseInterior.Room, "a_throne");
			if (array.Length != 0)
			{
				return Near(houseInterior.Eye, array, 1.6f);
			}
			return true;
		}

		public static bool AtCourt(Actor host)
		{
			if (SessionForTest.HasValue)
			{
				return SessionForTest.Value;
			}
			Kingdom kingdom = KingdomOf(host);
			if (kingdom == null)
			{
				return false;
			}
			if (!Gated(kingdom))
			{
				return true;
			}
			HouseInterior houseInterior = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Interior : null);
			if (houseInterior != null && houseInterior.Inside && !houseInterior.Virtual)
			{
				return Hosts(houseInterior.House, kingdom);
			}
			return false;
		}

		public static string WhereLine(Kingdom k)
		{
			Building building = CapitalHall(k);
			try
			{
				if (building != null)
				{
					return "The council sits in " + Renames.Apply(k.capital.name) + ".";
				}
				return "The council sits in any hall of " + Royal.NameOf(k) + ".";
			}
			catch
			{
				return "The council sits in the capital.";
			}
		}

		public static string AwayLine(Actor host)
		{
			Kingdom k = KingdomOf(host);
			if (!AtCourt(host))
			{
				return WhereLine(k);
			}
			return "Hold council at the war table.";
		}

		public static string ThroneLine(Actor host)
		{
			Kingdom k = KingdomOf(host);
			if (!AtCourt(host))
			{
				return WhereLine(k);
			}
			return "Hold court from the throne.";
		}

		public static string TalkHint(Actor host)
		{
			try
			{
				Kingdom kingdom = KingdomOf(host);
				return (kingdom != null && Royal.IsKing(host) && Gated(kingdom) && InSession(host)) ? "|Shift+E:Hold council" : "";
			}
			catch
			{
				return "";
			}
		}

		public static void OpenCouncil(Seat? at)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (!(instance == null))
			{
				Focus = at;
				FocusAt = Time.unscaledTime;
				instance.Royal.Open = true;
			}
		}

		public static void Update(Actor host, float dt)
		{
			CourtThrone.Update(host, dt);
		}

		public static bool RoomPrompt(PromptAsk ask)
		{
			HouseInterior houseInterior = ask?.Room;
			if (houseInterior == null || houseInterior.Room == null || ask.House == null)
			{
				return false;
			}
			Actor host = ask.Host;
			Kingdom kingdom = KingdomOf(host);
			if (kingdom == null || !Royal.IsKing(host) || !Hosts(ask.House, kingdom))
			{
				return false;
			}
			Anchors.Spot[] array = HallRooms.Spots(houseInterior.Room, "a_throne");
			if (array.Length != 0 && Near(ask.Eye, array, 1.6f))
			{
				int num = CourtThrone.Waiting(host);
				ask.Prompt = ((num > 0) ? ("E:Hold court (" + num + " waiting)") : "E:Hold court");
				if (PlayInput.GetKeyDown(KeyCode.E))
				{
					CourtThrone.Begin(host, (WorldBoxMod.Instance != null) ? new Action<string>(WorldBoxMod.Instance.Toast) : null);
				}
				return true;
			}
			Anchors.Spot[] array2 = HallRooms.Spots(houseInterior.Room, "a_war_table");
			Anchors.Spot[] array3 = HallRooms.Spots(houseInterior.Room, "a_seat");
			if ((array2.Length != 0 || array3.Length != 0) && AtTable(houseInterior.Room, ask.Eye))
			{
				ask.Prompt = "E:Hold council";
				if (PlayInput.GetKeyDown(KeyCode.E))
				{
					OpenCouncil(null);
				}
				return true;
			}
			return false;
		}

		private static bool Near(Vector2 eye, Anchors.Spot[] spots, float reach, int count = 0)
		{
			if (spots == null)
			{
				return false;
			}
			int num = ((count > 0) ? Mathf.Min(count, spots.Length) : spots.Length);
			for (int i = 0; i < num; i++)
			{
				float num2 = spots[i].At.X - eye.x;
				float num3 = spots[i].At.Y - eye.y;
				if (num2 * num2 + num3 * num3 <= reach * reach)
				{
					return true;
				}
			}
			return false;
		}

		public static void Seated(Building b, Interior room, Actor host, List<RoomSeat> into)
		{
			if (HallRooms.KindOf(room) == null)
			{
				return;
			}
			City city;
			Kingdom kingdom;
			try
			{
				city = b.city;
				kingdom = city?.kingdom;
			}
			catch
			{
				return;
			}
			if (kingdom == null || !Hosts(b, kingdom))
			{
				return;
			}
			Anchors.Spot[] array = HallRooms.Spots(room, "a_seat");
			Anchors.Spot[] array2 = HallRooms.Spots(room, "a_throne");
			Anchors.Spot[] array3 = HallRooms.Spots(room, "a_petition");
			WorldBoxMod instance = WorldBoxMod.Instance;
			float num = ((instance != null) ? instance.Day.Hour : 0.5f);
			bool num2 = num > 0.27f && num < 0.75f;
			Actor actor = null;
			if (array3.Length != 0 && CourtThrone.Holding && instance != null && instance.Converse.Open && instance.Converse.With == CourtThrone.Audience)
			{
				actor = CourtThrone.Audience;
			}
			if (actor != null)
			{
				Add(into, actor, array3[0], 0f);
			}
			if (!num2)
			{
				return;
			}
			Actor actor2 = null;
			try
			{
				actor2 = kingdom.king;
			}
			catch
			{
			}
			if (actor2 != null && actor2 != host && InTown(actor2, city))
			{
				if ((num >= 0.38f && num < 0.46f && array.Length != 0) || array2.Length == 0)
				{
					if (array.Length != 0)
					{
						Add(into, actor2, array[0], 1f);
					}
				}
				else
				{
					Add(into, actor2, array2[0], 1f);
				}
			}
			for (int i = 0; i < Seats.Length && i + 1 < array.Length; i++)
			{
				Actor actor3 = Holder(kingdom, Seats[i]);
				if (actor3 != null && actor3 != host && actor3 != actor && InTown(actor3, city))
				{
					Add(into, actor3, array[i + 1], 1f);
				}
			}
		}

		private static void Add(List<RoomSeat> into, Actor a, Anchors.Spot spot, float sit)
		{
			foreach (RoomSeat item in into)
			{
				if (item.Who == a)
				{
					return;
				}
			}
			into.Add(new RoomSeat
			{
				Who = a,
				X = spot.At.X,
				Y = spot.At.Y,
				Z = spot.At.Z,
				Yaw = -90f + (spot.HasYaw ? spot.Yaw : 0f),
				Sit = sit
			});
		}

		private static bool InTown(Actor a, City town)
		{
			try
			{
				if (a == null || !a.isAlive())
				{
					return false;
				}
				WorldTile current_tile = a.current_tile;
				return current_tile != null && current_tile.zone != null && current_tile.zone.city == town;
			}
			catch
			{
				return false;
			}
		}
	}
}
