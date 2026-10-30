using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Rep
	{
		internal static class Earn
		{
			public const int HiredLoad = 12;

			public const int FreelanceLoad = 6;

			public const int ShiftDone = 30;

			public const int QuestMin = 75;

			public const int QuestMax = 200;

			public const int QuestPerRenown = 15;

			public const int BountyMin = 100;

			public const int BountyMax = 300;

			public const int BountyPerRenown = 25;

			public const int VillageThreat = 120;

			public const int VillageThreatKill = 25;

			public const int BanditRaid = 400;

			public const int BanditRaidKill = 40;

			public const int BigThreat = 1000;

			public const int BigThreatKill = 75;

			public const int WarHeld = 1500;

			public const int WarKill = 50;

			public const int PlanFinished = 100;

			public const int DonationGoldPerPoint = 2;

			public const int DonationMaxPerDawn = 250;

			public const int TaxCollected = 10;

			public const int Bandaged = 8;
		}

		internal static class Lose
		{
			public const int Strike = 100;

			public const int Theft = 200;

			public const int Murder = 750;

			public const int TaskAbandoned = 50;

			public const int WalkedOff = 15;

			public const int InnocentExecuted = 300;

			public const int TaxPressed = 10;
		}

		private struct Entry
		{
			public long Id;

			public int Points;
		}

		private sealed class Ledger
		{
			private readonly string _key;

			private readonly Func<long, bool> _exists;

			private Actor _cachedBody;

			private string _cachedRaw;

			private List<Entry> _cachedEntries;

			public Ledger(string key, Func<long, bool> exists)
			{
				_key = key;
				_exists = exists;
			}

			public int Points(Actor body, long id)
			{
				if (body == null || id < 0)
				{
					return 0;
				}
				foreach (Entry item in Cached(body))
				{
					if (item.Id == id)
					{
						return item.Points;
					}
				}
				return 0;
			}

			private List<Entry> Cached(Actor body)
			{
				string text = Raw(body);
				if (body == _cachedBody && (object)text == _cachedRaw && _cachedEntries != null)
				{
					return _cachedEntries;
				}
				List<Entry> list = Read(body);
				_cachedBody = body;
				_cachedRaw = Raw(body);
				_cachedEntries = list;
				return list;
			}

			private string Raw(Actor body)
			{
				string pResult = null;
				try
				{
					body.data.get(_key, out pResult, null);
				}
				catch
				{
				}
				return pResult;
			}

			public List<Entry> Read(Actor body)
			{
				if (body == null)
				{
					return new List<Entry>();
				}
				bool dropped;
				List<Entry> list = Parse(Raw(body), out dropped);
				if (dropped)
				{
					Write(body, list);
				}
				return list;
			}

			public List<Entry> Parse(string raw)
			{
				bool dropped;
				return Parse(raw, out dropped);
			}

			private List<Entry> Parse(string raw, out bool dropped)
			{
				List<Entry> list = new List<Entry>();
				dropped = false;
				if (string.IsNullOrEmpty(raw))
				{
					return list;
				}
				bool flag = WorldSettled();
				string[] array = raw.Split('|');
				foreach (string text in array)
				{
					int num = text.IndexOf(':');
					if (num <= 0 || !long.TryParse(text.Substring(0, num), out var id) || !int.TryParse(text.Substring(num + 1), out var result))
					{
						continue;
					}
					if (flag && !_exists(id))
					{
						dropped = true;
						continue;
					}
					result = Mathf.Clamp(result, -3000, 5000);
					int num2 = list.FindIndex((Entry e) => e.Id == id);
					if (num2 >= 0)
					{
						if (result > list[num2].Points)
						{
							list[num2] = new Entry
							{
								Id = id,
								Points = result
							};
						}
						dropped = true;
					}
					else
					{
						list.Add(new Entry
						{
							Id = id,
							Points = result
						});
					}
				}
				return list;
			}

			public void Write(Actor body, List<Entry> entries)
			{
				Text.Length = 0;
				foreach (Entry entry in entries)
				{
					if (entry.Points != 0)
					{
						if (Text.Length > 0)
						{
							Text.Append('|');
						}
						Text.Append(entry.Id).Append(':').Append(entry.Points);
					}
				}
				try
				{
					if (Text.Length == 0)
					{
						body.data.removeString(_key);
					}
					else
					{
						body.data.set(_key, Text.ToString());
					}
				}
				catch (Exception ex)
				{
					Log.Error("rep: could not save " + _key + ": " + ex.Message);
				}
			}

			public int Add(Actor body, long id, int amount, out int before)
			{
				List<Entry> list = Read(body);
				before = 0;
				int num = -1;
				for (int i = 0; i < list.Count; i++)
				{
					if (list[i].Id == id)
					{
						before = list[i].Points;
						num = i;
						break;
					}
				}
				int num2 = Mathf.Clamp(before + amount, -3000, 5000);
				if (num2 == before)
				{
					return num2;
				}
				if (num >= 0)
				{
					list[num] = new Entry
					{
						Id = id,
						Points = num2
					};
				}
				else
				{
					list.Add(new Entry
					{
						Id = id,
						Points = num2
					});
				}
				Write(body, list);
				return num2;
			}
		}

		public const string Key = "fp_rep";

		public const string RealmKey = "fp_realm";

		public const int Trusted = 300;

		public const int Honored = 1500;

		public const int Champion = 3000;

		public const int Max = 5000;

		public const int Outlaw = -1500;

		public const int Min = -3000;

		private static readonly Ledger TownBook = new Ledger("fp_rep", (long id) => Quests.CityById(id) != null);

		private static readonly Ledger RealmBook = new Ledger("fp_realm", (long id) => KingdomById(id) != null);

		internal static Action<Actor, City, int, int, string> Shown;

		internal static Action<Actor, Kingdom, int, int, string, bool> ShownRealm;

		private static Actor _dying;

		private static readonly Dictionary<string, string> Dying = new Dictionary<string, string>();

		private static readonly string[] Inherited = new string[6] { "fp_rep", "fp_realm", "fp_skills", "fp_tally", "fp_qlv", "fp_learned_spells" };

		private static readonly StringBuilder Text = new StringBuilder(128);

		public static event Action<Actor, City, RepLevel, RepLevel> LevelChanged;

		public static event Action<Actor, Kingdom, RepLevel, RepLevel> StandingChanged;

		public static int Points(Actor body, City c)
		{
			if (body != null && c != null)
			{
				return TownBook.Points(body, IdOf(c));
			}
			return 0;
		}

		public static RepLevel Level(Actor body, City c)
		{
			return LevelOf(Points(body, c));
		}

		public static RepLevel LevelOf(int points)
		{
			if (points < 3000)
			{
				if (points < 1500)
				{
					if (points < 300)
					{
						if (points < 0)
						{
							if (points <= -1500)
							{
								return RepLevel.Outlaw;
							}
							return RepLevel.Distrusted;
						}
						return RepLevel.Stranger;
					}
					return RepLevel.Trusted;
				}
				return RepLevel.Honored;
			}
			return RepLevel.Champion;
		}

		public static int Threshold(RepLevel level)
		{
			switch (level)
			{
			case RepLevel.Champion:
				return 3000;
			case RepLevel.Honored:
				return 1500;
			case RepLevel.Trusted:
				return 300;
			case RepLevel.Stranger:
				return 0;
			case RepLevel.Distrusted:
				return -1499;
			default:
				return -3000;
			}
		}

		public static void Span(RepLevel level, out int lo, out int hi)
		{
			switch (level)
			{
			case RepLevel.Champion:
				lo = 3000;
				hi = 5000;
				break;
			case RepLevel.Honored:
				lo = 1500;
				hi = 3000;
				break;
			case RepLevel.Trusted:
				lo = 300;
				hi = 1500;
				break;
			case RepLevel.Stranger:
				lo = 0;
				hi = 300;
				break;
			case RepLevel.Distrusted:
				lo = -1500;
				hi = 0;
				break;
			default:
				lo = -3000;
				hi = -1500;
				break;
			}
		}

		public static float Progress(int points)
		{
			Span(LevelOf(points), out var lo, out var hi);
			if (hi <= lo)
			{
				return 1f;
			}
			return Mathf.Clamp01((float)(points - lo) / (float)(hi - lo));
		}

		public static RepLevel Next(RepLevel level)
		{
			if (level < RepLevel.Champion)
			{
				return level + 1;
			}
			return RepLevel.Champion;
		}

		public static bool AtLeast(Actor body, City c, RepLevel level)
		{
			if (body == null || c == null)
			{
				return false;
			}
			if (level <= RepLevel.Stranger)
			{
				return true;
			}
			if (Rules(body, c))
			{
				return true;
			}
			return Level(body, c) >= level;
		}

		public static bool Rules(Actor body, City c)
		{
			try
			{
				if (body == null || c == null || !body.isAlive())
				{
					return false;
				}
				if (c.leader == body)
				{
					return true;
				}
				Kingdom kingdom = c.kingdom;
				return kingdom != null && kingdom.king == body;
			}
			catch
			{
				return false;
			}
		}

		public static bool Reigns(Actor body, Kingdom k)
		{
			try
			{
				return body != null && k != null && body.isAlive() && k.king == body;
			}
			catch
			{
				return false;
			}
		}

		public static int StandingPoints(Actor body, Kingdom k)
		{
			if (body != null && k != null)
			{
				return RealmBook.Points(body, IdOf(k));
			}
			return 0;
		}

		public static RepLevel Standing(Actor body, Kingdom k)
		{
			if (!Reigns(body, k))
			{
				return LevelOf(StandingPoints(body, k));
			}
			return RepLevel.Champion;
		}

		public static string Name(RepLevel level)
		{
			return level.ToString();
		}

		public static List<KeyValuePair<City, int>> Towns(Actor body)
		{
			List<KeyValuePair<City, int>> list = new List<KeyValuePair<City, int>>();
			foreach (Entry item in TownBook.Read(body))
			{
				City city = Quests.CityById(item.Id);
				if (city != null)
				{
					list.Add(new KeyValuePair<City, int>(city, item.Points));
				}
			}
			list.Sort((KeyValuePair<City, int> a, KeyValuePair<City, int> b) => b.Value.CompareTo(a.Value));
			return list;
		}

		public static List<KeyValuePair<Kingdom, int>> Realms(Actor body)
		{
			List<KeyValuePair<Kingdom, int>> list = new List<KeyValuePair<Kingdom, int>>();
			foreach (Entry item in RealmBook.Read(body))
			{
				Kingdom kingdom = KingdomById(item.Id);
				if (kingdom != null)
				{
					list.Add(new KeyValuePair<Kingdom, int>(kingdom, item.Points));
				}
			}
			list.Sort((KeyValuePair<Kingdom, int> a, KeyValuePair<Kingdom, int> b) => b.Value.CompareTo(a.Value));
			return list;
		}

		public static int Add(Actor body, City c, int amount, string reason)
		{
			return Add(body, c, amount, reason, realm: true);
		}

		public static int Add(Actor body, City c, int amount, string reason, bool realm)
		{
			if (body == null || c == null || amount == 0)
			{
				return Points(body, c);
			}
			long num = IdOf(c);
			if (num < 0)
			{
				return 0;
			}
			int before;
			int num2 = TownBook.Add(body, num, amount, out before);
			if (num2 != before)
			{
				Changed(body, c, before, num2, reason);
			}
			if (realm)
			{
				Kingdom kingdom = RealmOf(c);
				if (kingdom != null)
				{
					ChangeStanding(body, kingdom, (amount > 0) ? (amount / 2) : amount, reason, alone: false);
				}
			}
			return num2;
		}

		public static int AddStanding(Actor body, Kingdom k, int amount, string reason)
		{
			return ChangeStanding(body, k, amount, reason, alone: true);
		}

		private static int ChangeStanding(Actor body, Kingdom k, int amount, string reason, bool alone)
		{
			if (body == null || !Civ(k) || amount == 0)
			{
				return StandingPoints(body, k);
			}
			long num = IdOf(k);
			if (num < 0)
			{
				return 0;
			}
			int before;
			int num2 = RealmBook.Add(body, num, amount, out before);
			if (num2 != before)
			{
				ChangedRealm(body, k, before, num2, reason, alone);
			}
			return num2;
		}

		public static void DropTo(Actor body, City c, RepLevel level, string reason)
		{
			int num = Points(body, c);
			int num2 = Threshold(level);
			if (num > num2)
			{
				Add(body, c, num2 - num, reason, realm: false);
			}
		}

		public static void Pardon(Actor body, Kingdom k, string reason)
		{
			if (body == null || k == null)
			{
				return;
			}
			foreach (KeyValuePair<City, int> item in Towns(body))
			{
				if (item.Value < 0)
				{
					Kingdom kingdom = null;
					try
					{
						kingdom = item.Key.kingdom;
					}
					catch
					{
					}
					if (kingdom == k)
					{
						Add(body, item.Key, -item.Value, reason, realm: false);
					}
				}
			}
			int num = StandingPoints(body, k);
			if (num < 0)
			{
				AddStanding(body, k, -num, reason);
			}
		}

		public static void Inherit(Actor from, Actor to)
		{
			if (from == null || to == null || from == to)
			{
				return;
			}
			Dictionary<long, int> dictionary = new Dictionary<long, int>();
			foreach (Entry item in TownBook.Read(to))
			{
				dictionary[item.Id] = item.Points;
			}
			int num = 0;
			foreach (Entry item2 in TownBook.Parse(DataOf(from, "fp_rep")))
			{
				dictionary.TryGetValue(item2.Id, out var value);
				int num2 = item2.Points / 2;
				if (num2 > value)
				{
					dictionary[item2.Id] = num2;
					num++;
				}
			}
			List<Entry> list = new List<Entry>();
			foreach (KeyValuePair<long, int> item3 in dictionary)
			{
				list.Add(new Entry
				{
					Id = item3.Key,
					Points = item3.Value
				});
			}
			TownBook.Write(to, list);
			MergeHalf(from, to, "fp_realm");
			MergeHalf(from, to, "fp_skills");
			MergeHalf(from, to, "fp_tally");
			Quests.InheritLevel(DataOf(from, "fp_qlv"), to);
			Abilities.InheritLearned(DataOf(from, "fp_learned_spells"), to);
			Log.Info($"rep: {WorldBoxMod.Describe(to)} inherits from {WorldBoxMod.Describe(from)}: {num} towns raised to half");
			if (from == _dying)
			{
				_dying = null;
				Dying.Clear();
			}
		}

		internal static void MergeHalf(Actor from, Actor to, string dataKey)
		{
			string text = DataOf(from, dataKey);
			string pResult = null;
			try
			{
				to.data.get(dataKey, out pResult, null);
			}
			catch
			{
				return;
			}
			if (string.IsNullOrEmpty(text))
			{
				return;
			}
			Dictionary<string, long> dictionary = new Dictionary<string, long>();
			List<string> list = new List<string>();
			for (int i = 0; i < 2; i++)
			{
				string text2 = ((i == 0) ? pResult : text);
				bool flag = i == 1;
				if (string.IsNullOrEmpty(text2))
				{
					continue;
				}
				string[] array = text2.Split('|');
				foreach (string text3 in array)
				{
					int num = text3.LastIndexOf(':');
					if (num <= 0 || !long.TryParse(text3.Substring(num + 1), out var result))
					{
						continue;
					}
					if (flag)
					{
						result /= 2;
						if (result <= 0)
						{
							continue;
						}
					}
					string text4 = text3.Substring(0, num);
					if (!dictionary.TryGetValue(text4, out var value))
					{
						list.Add(text4);
						dictionary[text4] = result;
					}
					else if (result > value)
					{
						dictionary[text4] = result;
					}
				}
			}
			Text.Length = 0;
			foreach (string item in list)
			{
				if (Text.Length > 0)
				{
					Text.Append('|');
				}
				Text.Append(item).Append(':').Append(dictionary[item]);
			}
			try
			{
				if (Text.Length == 0)
				{
					to.data.removeString(dataKey);
				}
				else
				{
					to.data.set(dataKey, Text.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("rep: inherit " + dataKey + ": " + ex.Message);
			}
		}

		public static Choice Gate(Choice choice, Actor body, City c, RepLevel needs)
		{
			if (choice == null || needs <= RepLevel.Stranger || AtLeast(body, c, needs))
			{
				return choice;
			}
			return choice.Off(Name(needs) + " in " + (NameOf(c) ?? "this town"));
		}

		public static Choice GateStanding(Choice choice, Actor body, Kingdom k, RepLevel needs)
		{
			if (choice == null || needs <= RepLevel.Stranger || Standing(body, k) >= needs)
			{
				return choice;
			}
			string text = null;
			try
			{
				text = k?.name;
			}
			catch
			{
			}
			return choice.Off(Name(needs) + " in " + (text ?? "this realm"));
		}

		private static void Changed(Actor body, City c, int before, int after, string reason)
		{
			RepLevel repLevel = LevelOf(before);
			RepLevel repLevel2 = LevelOf(after);
			Log.Info(string.Format("rep: {0} in {1}: {2} -> {3} ({4}{5})", WorldBoxMod.Describe(body), NameOf(c), before, after, Signed(after - before), (reason != null) ? (", " + reason) : "") + ((repLevel != repLevel2) ? $", now {repLevel2}" : ""));
			try
			{
				Shown?.Invoke(body, c, before, after, reason);
			}
			catch (Exception ex)
			{
				Log.Error("rep: shown: " + ex.Message);
			}
			if (repLevel != repLevel2)
			{
				try
				{
					Rep.LevelChanged?.Invoke(body, c, repLevel, repLevel2);
				}
				catch (Exception ex2)
				{
					Log.Error("rep: level changed: " + ex2);
				}
			}
		}

		private static void ChangedRealm(Actor body, Kingdom k, int before, int after, string reason, bool alone)
		{
			RepLevel repLevel = LevelOf(before);
			RepLevel repLevel2 = LevelOf(after);
			Log.Info(string.Format("rep: {0} in the realm of {1}: {2} -> {3} ({4}{5})", WorldBoxMod.Describe(body), NameOf(k), before, after, Signed(after - before), (reason != null) ? (", " + reason) : "") + ((repLevel != repLevel2) ? $", now {repLevel2}" : ""));
			try
			{
				ShownRealm?.Invoke(body, k, before, after, reason, alone);
			}
			catch (Exception ex)
			{
				Log.Error("rep: shown realm: " + ex.Message);
			}
			if (repLevel != repLevel2 && !Reigns(body, k))
			{
				try
				{
					Rep.StandingChanged?.Invoke(body, k, repLevel, repLevel2);
				}
				catch (Exception ex2)
				{
					Log.Error("rep: standing changed: " + ex2);
				}
			}
		}

		private static string Signed(int d)
		{
			return ((d >= 0) ? "+" : "") + d;
		}

		internal static void Remember(Actor body)
		{
			_dying = body;
			Dying.Clear();
			if (body == null)
			{
				return;
			}
			string[] inherited = Inherited;
			foreach (string text in inherited)
			{
				string pResult = null;
				try
				{
					body.data.get(text, out pResult, null);
				}
				catch
				{
				}
				if (!string.IsNullOrEmpty(pResult))
				{
					Dying[text] = pResult;
				}
			}
		}

		private static string DataOf(Actor body, string dataKey)
		{
			if (body != null && body == _dying)
			{
				if (!Dying.TryGetValue(dataKey, out var value))
				{
					return null;
				}
				return value;
			}
			string pResult = null;
			try
			{
				body.data.get(dataKey, out pResult, null);
			}
			catch
			{
			}
			return pResult;
		}

		private static bool WorldSettled()
		{
			try
			{
				if (!Config.game_loaded || World.world == null || World.world.cities == null || World.world.cities.Count == 0)
				{
					return false;
				}
				return !SmoothLoader.isLoading();
			}
			catch
			{
				return false;
			}
		}

		internal static long IdOf(City c)
		{
			try
			{
				return c?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		internal static long IdOf(Kingdom k)
		{
			try
			{
				return k?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		internal static string NameOf(City c)
		{
			try
			{
				return c?.name;
			}
			catch
			{
				return null;
			}
		}

		internal static string NameOf(Kingdom k)
		{
			try
			{
				return k?.name;
			}
			catch
			{
				return null;
			}
		}

		internal static bool Civ(Kingdom k)
		{
			try
			{
				return k != null && k.isAlive() && k.isCiv();
			}
			catch
			{
				return false;
			}
		}

		internal static Kingdom RealmOf(City c)
		{
			Kingdom kingdom = null;
			try
			{
				kingdom = c?.kingdom;
			}
			catch
			{
			}
			if (!Civ(kingdom))
			{
				return null;
			}
			return kingdom;
		}

		internal static Kingdom KingdomById(long id)
		{
			try
			{
				Kingdom kingdom = World.world.kingdoms.get(id);
				return (kingdom != null && kingdom.isAlive()) ? kingdom : null;
			}
			catch
			{
				return null;
			}
		}
	}
}
