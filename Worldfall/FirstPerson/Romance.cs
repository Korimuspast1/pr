using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FirstPerson
{
	internal static class Romance
	{
		internal enum GiftTaste : byte
		{
			Loved,
			Liked,
			Plain,
			Disliked
		}

		internal struct Result
		{
			public FlirtOutcome Outcome;

			public int Gain;

			public string Reply;

			public bool NowReady;

			public bool Capped;

			public int Left;
		}

		public const string Key = "fp_court";

		public const int Most = 100;

		public const int AskAt = 100;

		public const int ExchangesADay = 3;

		private const int FadeAfter = 3;

		private const int FadeADay = 5;

		public const int TaskWorth = 20;

		private static readonly System.Random Rng = new System.Random();

		private static readonly StringBuilder Text = new StringBuilder(128);

		internal static bool SteadyForTest;

		private static readonly string[] Gifts = new string[8] { "berries", "honey", "bread", "pie", "cider", "jam", "fish", "gems" };

		internal static int TodayForTest = -1;

		internal static int WelcomeForTest = -1;

		public static string LeftBehind;

		internal static int Today => Mind.Today;

		private static int Day
		{
			get
			{
				if (TodayForTest < 0)
				{
					return Today;
				}
				return TodayForTest;
			}
		}

		public static Charm Likes(Actor npc)
		{
			if (Has(npc, "lustful") || Has(npc, "bloodlust") || Has(npc, "savage") || Has(npc, "hotheaded"))
			{
				return Charm.Bold;
			}
			if (Has(npc, "honest") || Has(npc, "wise") || Has(npc, "genius") || Has(npc, "pacifist"))
			{
				return Charm.Deep;
			}
			if (Has(npc, "peaceful") || Has(npc, "content"))
			{
				return Charm.Sweet;
			}
			if (Has(npc, "ambitious") || Has(npc, "deceitful") || Has(npc, "greedy") || Has(npc, "stupid"))
			{
				return Charm.Witty;
			}
			return (Charm)(Hash(npc) % 4);
		}

		public static Charm Dislikes(Actor npc)
		{
			switch (Likes(npc))
			{
			case Charm.Sweet:
				return Charm.Bold;
			case Charm.Bold:
				return Charm.Sweet;
			case Charm.Witty:
				return Charm.Deep;
			default:
				return Charm.Witty;
			}
		}

		public static string Favourite(Actor npc)
		{
			if ((Has(npc, "greedy") || Has(npc, "lustful")) && Exists("gems"))
			{
				return "gems";
			}
			List<string> list = new List<string>();
			string[] gifts = Gifts;
			foreach (string text in gifts)
			{
				if (Exists(text))
				{
					list.Add(text);
				}
			}
			if (list.Count == 0)
			{
				return "berries";
			}
			return list[(int)(Hash(npc) / 4 % (ulong)list.Count)];
		}

		public static Hope HopeOf(Actor npc)
		{
			if (Has(npc, "ambitious"))
			{
				return Hope.Glory;
			}
			if (Has(npc, "greedy"))
			{
				return Hope.Wealth;
			}
			if (Has(npc, "wise") || Has(npc, "genius"))
			{
				return Hope.Wisdom;
			}
			if (Has(npc, "content") || Has(npc, "peaceful") || Has(npc, "pacifist"))
			{
				return Hope.Quiet;
			}
			if (Has(npc, "brave") || Has(npc, "bloodlust") || Has(npc, "savage") || Has(npc, "hotheaded"))
			{
				return Hope.Adventure;
			}
			Hope[] array = new Hope[4]
			{
				Hope.Quiet,
				Hope.Adventure,
				Hope.Family,
				Hope.Family
			};
			return array[(uint)(Hash(npc) / 16 % (ulong)array.Length)];
		}

		public static string FavouritePlace(Actor npc)
		{
			string[] array = new string[4] { "flowers", "hill", "shore", "woods" };
			if (Hash(npc) / 64 % 2 == 0L)
			{
				return array[(uint)Likes(npc)];
			}
			return array[(uint)(Hash(npc) / 128 % 4)];
		}

		public static string PlaceWords(string kind)
		{
			switch (kind)
			{
			default:
				return "the quiet fields";
			case "woods":
				return "the deep woods";
			case "shore":
				return "the water's edge";
			case "hill":
				return "the hilltops";
			case "flowers":
				return "the flower meadows";
			}
		}

		public static string Disliked(Actor npc)
		{
			string text = Favourite(npc);
			List<string> list = new List<string>();
			string[] gifts = Gifts;
			foreach (string text2 in gifts)
			{
				if (text2 != text && Exists(text2))
				{
					list.Add(text2);
				}
			}
			if (list.Count != 0)
			{
				return list[(int)(Hash(npc) / 512 % (ulong)list.Count)];
			}
			return null;
		}

		public static bool Tell(Actor npc, Actor you, Fact f)
		{
			Suitor suitor = Of(npc, you);
			if (((uint)suitor.Known & (uint)f) != 0)
			{
				return false;
			}
			suitor.Known |= (int)f;
			Warm(npc, you, suitor, 4, out var _);
			Save(npc, suitor);
			return true;
		}

		public static bool KnowsFact(Actor npc, Actor you, Fact f)
		{
			return ((uint)Of(npc, you).Known & (uint)f) != 0;
		}

		public static GiftTaste TasteFor(Actor npc, string res)
		{
			if (res == Favourite(npc))
			{
				return GiftTaste.Loved;
			}
			if (res == Disliked(npc))
			{
				return GiftTaste.Disliked;
			}
			if (Array.IndexOf(Gifts, res) >= 0 || res == "gold" || res == "gems")
			{
				return GiftTaste.Liked;
			}
			return GiftTaste.Plain;
		}

		public static bool GaveToday(Actor npc, Actor you)
		{
			Suitor suitor = Of(npc, you);
			Roll(suitor);
			return suitor.Gift;
		}

		public static int? Give(Actor you, Actor npc, string res, out GiftTaste taste, out bool capped)
		{
			taste = TasteFor(npc, res);
			capped = false;
			if (Blocker(you, npc).HasValue || GaveToday(npc, you) || !Civil.TakeFromBag(you, res, 1))
			{
				return null;
			}
			Suitor suitor = Of(npc, you);
			Roll(suitor);
			suitor.Gift = true;
			int num = ((taste == GiftTaste.Loved) ? 25 : ((taste == GiftTaste.Liked) ? 10 : ((taste == GiftTaste.Plain) ? 2 : (-5))));
			int num2 = Warm(npc, you, suitor, num, out capped);
			Save(npc, suitor);
			try
			{
				if (taste != GiftTaste.Disliked)
				{
					npc.changeHappiness("just_received_gift");
				}
			}
			catch
			{
			}
			Chronicle.ChangeOpinion(npc, you, (taste == GiftTaste.Loved) ? 4 : ((taste != GiftTaste.Disliked) ? 1 : (-2)));
			Chronicle.Note(npc, you, 0, 1);
			Log.Info(string.Format("romance: gave {0} to {1} ({2}): {3:+0;-0;0}{4} -> {5}", res, Dialogue.NameOf(npc), taste, num2, capped ? " (capped)" : "", suitor.Affection));
			return num2;
		}

		private static bool Exists(string res)
		{
			try
			{
				return AssetManager.resources.get(res) != null;
			}
			catch
			{
				return false;
			}
		}

		public static int TasksNeeded(Actor npc)
		{
			if (Has(npc, "lustful"))
			{
				return 1;
			}
			if (!Picky(npc))
			{
				return 2;
			}
			return 3;
		}

		public static bool Picky(Actor npc)
		{
			if (Has(npc, "lustful"))
			{
				return false;
			}
			try
			{
				if (npc.isKing() || npc.isCityLeader())
				{
					return true;
				}
			}
			catch
			{
			}
			if (!Has(npc, "ambitious") && !Has(npc, "greedy"))
			{
				return Has(npc, "paranoid");
			}
			return true;
		}

		public static int Days(Actor npc)
		{
			if (!Picky(npc))
			{
				return 2;
			}
			return 3;
		}

		public static int CapFor(Actor npc, Actor you)
		{
			float num = Mathf.Ceil(100f / (float)Days(npc));
			if (Taken(npc, you))
			{
				num *= Mathf.Lerp(1f, 0.5f, (float)Devotion(npc) / 100f);
			}
			return Mathf.Max(10, Mathf.RoundToInt(num));
		}

		private static void Roll(Suitor s)
		{
			int day = Day;
			if (s.Day != day)
			{
				s.Day = day;
				s.Gained = 0;
				s.Exchanges = 0;
				s.Same = 0;
				s.Gift = false;
			}
		}

		public static int RoomToday(Actor npc, Actor you, Suitor s)
		{
			Roll(s);
			return Mathf.Max(0, CapFor(npc, you) - s.Gained);
		}

		public static int Warm(Actor npc, Actor you, Suitor s, int by, out bool capped, bool underCap = true)
		{
			capped = false;
			Roll(s);
			if (by > 0 && underCap)
			{
				int num = Mathf.Max(0, CapFor(npc, you) - s.Gained);
				if (by > num)
				{
					capped = true;
					by = num;
				}
				s.Gained += by;
			}
			int affection = s.Affection;
			s.Affection = Mathf.Clamp(s.Affection + by, 0, 100);
			if (s.Affection > s.Best)
			{
				s.Best = s.Affection;
			}
			return s.Affection - affection;
		}

		private static bool Courting(Suitor s)
		{
			if (!s.Asked)
			{
				return s.Done > 0;
			}
			return true;
		}

		public static string Meet(Actor npc, Actor you)
		{
			if (npc == null || you == null || Blocker(you, npc).HasValue)
			{
				return null;
			}
			Suitor suitor = ReadAll(npc).Find((Suitor x) => x.Body == you.data.id);
			if (suitor == null)
			{
				return null;
			}
			suitor = Fitted(npc, you, suitor);
			int day = Day;
			int num = ((suitor.LastTalk >= 0) ? (day - suitor.LastTalk) : 0);
			string result = null;
			if (num >= 3 && !Courting(suitor))
			{
				int num2 = suitor.Best / 3;
				int affection = suitor.Affection;
				if (suitor.Affection > num2)
				{
					suitor.Affection = Mathf.Max(num2, suitor.Affection - 5 * (num - 3 + 1));
				}
				if (affection != suitor.Affection)
				{
					Log.Info($"romance: {Dialogue.NameOf(npc)} after {num} days apart: {affection} -> {suitor.Affection}");
				}
			}
			if (num >= 2 && suitor.Affection > 0)
			{
				float num3 = 0.35f;
				if (Has(npc, "content") || Has(npc, "peaceful") || Has(npc, "lustful"))
				{
					num3 += 0.2f;
				}
				if (Picky(npc))
				{
					num3 -= 0.1f;
				}
				if ((WelcomeForTest >= 0) ? (WelcomeForTest == 1) : (Rng.NextDouble() < (double)num3))
				{
					Warm(npc, you, suitor, CapFor(npc, you) / 2, out var _, underCap: false);
					result = Dialogue.WelcomeBack(npc, you, num);
					Log.Info($"romance: {Dialogue.NameOf(npc)} is glad you're back after {num} days -> {suitor.Affection}");
				}
			}
			suitor.LastTalk = day;
			Save(npc, suitor);
			return result;
		}

		public static void SetBack(Actor npc, Actor you, int by)
		{
			Suitor suitor = Of(npc, you);
			Warm(npc, you, suitor, -by, out var _);
			suitor.CoolUntil = Day + 1;
			Save(npc, suitor);
		}

		public static bool Cool(Suitor s)
		{
			if (s.CoolUntil >= 0)
			{
				return Day <= s.CoolUntil;
			}
			return false;
		}

		public static Suitor Of(Actor npc, Actor you)
		{
			List<Suitor> list = ReadAll(npc);
			long id = you.data.id;
			foreach (Suitor item in list)
			{
				if (item.Body == id)
				{
					return Fitted(npc, you, item);
				}
			}
			return Fitted(npc, you, new Suitor
			{
				Body = id,
				Need = TasksNeeded(npc)
			});
		}

		private static Suitor Fitted(Actor npc, Actor you, Suitor s)
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
				if (actor == null && s.Rival != 0L)
				{
					s.Rival = 0L;
					s.Extra = 0;
				}
				return s;
			}
			long num = 0L;
			try
			{
				num = actor.data.id;
			}
			catch
			{
			}
			if (s.Rival != num)
			{
				s.Rival = num;
				s.Extra = ExtraTasks(Devotion(npc));
			}
			return s;
		}

		public static void Save(Actor npc, Suitor s)
		{
			List<Suitor> list = ReadAll(npc);
			list.RemoveAll((Suitor x) => x.Body == s.Body);
			list.Add(s);
			while (list.Count > 4)
			{
				list.RemoveAt(0);
			}
			Write(npc, list);
		}

		public static void Forget(Actor npc, Actor you)
		{
			List<Suitor> list = ReadAll(npc);
			if (list.RemoveAll((Suitor x) => x.Body == you.data.id) > 0)
			{
				Write(npc, list);
			}
		}

		private static List<Suitor> ReadAll(Actor npc)
		{
			List<Suitor> list = new List<Suitor>();
			string pResult = null;
			try
			{
				npc.data.get("fp_court", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return list;
			}
			string[] array = pResult.Split('|');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(':');
				if (array2.Length < 10)
				{
					continue;
				}
				try
				{
					list.Add(new Suitor
					{
						Body = long.Parse(array2[0]),
						Affection = int.Parse(array2[1]),
						Done = int.Parse(array2[2]),
						Need = int.Parse(array2[3]),
						Kinds = int.Parse(array2[4]),
						Last = int.Parse(array2[5]),
						Same = int.Parse(array2[6]),
						Stamp = double.Parse(array2[7], CultureInfo.InvariantCulture),
						Asked = (array2[8] == "1"),
						Knows = (array2[9] == "1"),
						Extra = ((array2.Length > 10) ? int.Parse(array2[10]) : 0),
						Rival = ((array2.Length > 11) ? long.Parse(array2[11]) : 0)
					});
					Suitor suitor = list[list.Count - 1];
					if (array2.Length > 19)
					{
						suitor.Day = int.Parse(array2[12]);
						suitor.Gained = int.Parse(array2[13]);
						suitor.Exchanges = int.Parse(array2[14]);
						suitor.Gift = array2[15] == "1";
						suitor.Best = int.Parse(array2[16]);
						suitor.Known |= int.Parse(array2[17]);
						suitor.LastTalk = int.Parse(array2[18]);
						suitor.CoolUntil = int.Parse(array2[19]);
					}
					else
					{
						suitor.Best = suitor.Affection;
					}
				}
				catch
				{
				}
			}
			return list;
		}

		private static void Write(Actor npc, List<Suitor> all)
		{
			Text.Length = 0;
			foreach (Suitor item in all)
			{
				if (Text.Length > 0)
				{
					Text.Append('|');
				}
				Text.Append(item.Body).Append(':').Append(item.Affection)
					.Append(':')
					.Append(item.Done)
					.Append(':')
					.Append(item.Need)
					.Append(':')
					.Append(item.Kinds)
					.Append(':')
					.Append(item.Last)
					.Append(':')
					.Append(item.Same)
					.Append(':')
					.Append(item.Stamp.ToString("R", CultureInfo.InvariantCulture))
					.Append(':')
					.Append(item.Asked ? 1 : 0)
					.Append(':')
					.Append(item.Knows ? 1 : 0)
					.Append(':')
					.Append(item.Extra)
					.Append(':')
					.Append(item.Rival)
					.Append(':')
					.Append(item.Day)
					.Append(':')
					.Append(item.Gained)
					.Append(':')
					.Append(item.Exchanges)
					.Append(':')
					.Append(item.Gift ? 1 : 0)
					.Append(':')
					.Append(item.Best)
					.Append(':')
					.Append(item.Known)
					.Append(':')
					.Append(item.LastTalk)
					.Append(':')
					.Append(item.CoolUntil);
			}
			try
			{
				if (Text.Length == 0)
				{
					npc.data.removeString("fp_court");
				}
				else
				{
					npc.data.set("fp_court", Text.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("romance: save: " + ex.Message);
			}
		}

		public static FlirtOutcome? Blocker(Actor you, Actor npc)
		{
			try
			{
				if (npc.lover == you)
				{
					return FlirtOutcome.AlreadyYours;
				}
				if (!npc.isAdult() || !you.isAdult())
				{
					return FlirtOutcome.TooYoung;
				}
				if (!npc.isSameSpecies(you))
				{
					return FlirtOutcome.Incompatible;
				}
				if (you.hasLover())
				{
					return FlirtOutcome.YouAreTaken;
				}
			}
			catch
			{
				return FlirtOutcome.Rejected;
			}
			if (Chronicle.Opinion(npc, you) <= -45)
			{
				return FlirtOutcome.Rejected;
			}
			return null;
		}

		public static Result Flirt(Actor you, Actor npc, Charm charm, bool personal = false)
		{
			Result result = default(Result);
			FlirtOutcome? flirtOutcome = Blocker(you, npc);
			if (flirtOutcome.HasValue)
			{
				result.Outcome = flirtOutcome.Value;
				if (flirtOutcome.Value == FlirtOutcome.AlreadyYours)
				{
					Chronicle.ChangeOpinion(npc, you, 1);
				}
				result.Reply = Dialogue.Flirt(npc, you, result.Outcome);
				return result;
			}
			Suitor suitor = Of(npc, you);
			Roll(suitor);
			if (suitor.Exchanges >= 3)
			{
				result.Outcome = FlirtOutcome.Enough;
				result.Reply = Dialogue.EnoughLine(npc);
				return result;
			}
			suitor.Exchanges++;
			result.Left = 3 - suitor.Exchanges;
			Charm charm2 = Likes(npc);
			Charm charm3 = Dislikes(npc);
			suitor.Same = ((suitor.Last == (int)charm && suitor.Exchanges > 1) ? (suitor.Same + 1) : 0);
			suitor.Last = (int)charm;
			float num = ((charm == charm2) ? 14f : ((charm == charm3) ? (-6f) : 6f));
			if (personal && charm != charm3)
			{
				num += 6f;
			}
			int num2 = Chronicle.Opinion(npc, you);
			num += Mathf.Clamp((float)num2 / 20f, -4f, 5f);
			if (Has(you, "attractive"))
			{
				num += 3f;
			}
			if (Has(you, "ugly"))
			{
				num -= 3f;
			}
			if (charm == Charm.Bold && Has(npc, "lustful"))
			{
				num += 3f;
			}
			bool flag = Taken(npc, you);
			if (flag && num > 0f)
			{
				num *= Mathf.Lerp(1f, 0.6f, (float)Devotion(npc) / 100f);
			}
			bool flag2 = Dialogue.SafeHappiness(npc) < -20;
			if (flag2 && num > 0f)
			{
				num *= 0.4f;
			}
			if (Cool(suitor) && num > 0f)
			{
				num *= 0.5f;
			}
			bool flag3 = suitor.Same >= 2 && charm != charm2;
			if (flag3)
			{
				num = Mathf.Min(num * 0.3f, 2f);
			}
			if (!SteadyForTest && num > 0f)
			{
				num += (float)Rng.Next(-2, 3);
			}
			int num3 = Mathf.RoundToInt(num);
			int affection = suitor.Affection;
			result.Gain = Warm(npc, you, suitor, num3, out result.Capped);
			suitor.Stamp = Quests.Now;
			result.Outcome = (flag3 ? FlirtOutcome.Bored : ((flag2 && num3 <= 2) ? FlirtOutcome.Moody : ((charm == charm3 || num3 <= 0) ? FlirtOutcome.Cool : ((charm == charm2 || personal) ? FlirtOutcome.Warm : FlirtOutcome.Mild))));
			result.NowReady = !suitor.Asked && affection < 100 && suitor.Affection >= 100;
			Save(npc, suitor);
			if (result.Outcome == FlirtOutcome.Warm)
			{
				Chronicle.ChangeOpinion(npc, you, 2);
			}
			else if (result.Outcome == FlirtOutcome.Cool || result.Outcome == FlirtOutcome.Bored)
			{
				Chronicle.ChangeOpinion(npc, you, -2);
			}
			result.Reply = Dialogue.FlirtReply(npc, you, result.Outcome, charm, charm2);
			if (result.NowReady)
			{
				ref string reply = ref result.Reply;
				reply = reply + " " + Dialogue.ReadyLine(npc);
			}
			else if (result.Capped && result.Gain >= 0 && num3 > 0)
			{
				ref string reply2 = ref result.Reply;
				reply2 = reply2 + " " + Dialogue.CappedLine(npc);
			}
			Log.Info(string.Format("romance: {0}{1} at {2} (likes {3}{4}): {5} {6:+0;-0;0}{7} -> {8}, today {9}/{10}", charm, personal ? " (personal)" : "", Dialogue.NameOf(npc), charm2, flag ? (", taken, devotion " + Devotion(npc)) : "", result.Outcome, result.Gain, result.Capped ? " (capped)" : "", suitor.Affection, suitor.Gained, CapFor(npc, you)));
			Rivals.Flirted(you, npc);
			return result;
		}

		public static int ExchangesLeft(Actor npc, Actor you)
		{
			Suitor suitor = Of(npc, you);
			Roll(suitor);
			return Mathf.Max(0, 3 - suitor.Exchanges);
		}

		public static void TaskDone(Actor npc, Actor you, int kindBit)
		{
			Suitor suitor = Of(npc, you);
			suitor.Asked = true;
			suitor.Done++;
			suitor.Kinds |= kindBit;
			Warm(npc, you, suitor, 20, out var _, underCap: false);
			Save(npc, suitor);
		}

		public static bool WouldSayYes(Actor npc, Actor you)
		{
			if (npc == null || you == null || Blocker(you, npc).HasValue)
			{
				return false;
			}
			Suitor suitor = Of(npc, you);
			return suitor.Done >= suitor.Wanted;
		}

		public static bool Wed(Actor you, Actor npc)
		{
			LeftBehind = null;
			try
			{
				Actor actor = Rivals.Leave(npc, you);
				if (actor != null)
				{
					LeftBehind = Dialogue.NameOf(actor);
				}
				you.becomeLoversWith(npc);
				if (npc.lover != you)
				{
					return false;
				}
				try
				{
					npc.changeHappiness("fallen_in_love");
					you.changeHappiness("fallen_in_love");
				}
				catch
				{
				}
				Chronicle.ChangeOpinion(npc, you, 25);
				Chronicle.AddOwnMemory(npc, "fp_courted", Dialogue.NameOf(you));
				Forget(npc, you);
				Log.Info("became lovers: " + Dialogue.NameOf(you) + " + " + Dialogue.NameOf(npc));
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("could not make lovers: " + ex.Message);
				return false;
			}
		}

		public static string Standing(Actor npc, Actor you)
		{
			if (npc == null || you == null || Blocker(you, npc).HasValue)
			{
				return null;
			}
			Suitor suitor = Of(npc, you);
			string text = ((Taken(npc, you) && (suitor.Affection > 0 || suitor.Asked)) ? TakenWord(npc) : null);
			string text2 = ((suitor.Done > 0 || suitor.Asked) ? ((suitor.Done >= suitor.Wanted) ? "waiting for you to ask" : $"courting, {suitor.Done} of {suitor.Wanted}") : ((suitor.Affection >= AskAtFor(npc, you)) ? "would let you court them" : ((suitor.Affection >= 60) ? "fond of you" : ((suitor.Affection >= 25) ? "warming to you" : ((suitor.Affection < 5) ? null : "curious about you")))));
			if (text != null)
			{
				if (text2 != null)
				{
					return text + "  ·  " + text2;
				}
				return text;
			}
			return text2;
		}

		private static string TakenWord(Actor npc)
		{
			string text = Dialogue.NameOf(npc.lover);
			int num = Devotion(npc);
			if (num < 75)
			{
				if (num >= 25)
				{
					return "with " + text;
				}
				return "unhappy with " + text;
			}
			return "devoted to " + text;
		}

		public static bool Taken(Actor npc, Actor you)
		{
			try
			{
				return npc.lover != null && npc.lover != you && npc.lover.isAlive();
			}
			catch
			{
				return false;
			}
		}

		public static int Devotion(Actor npc)
		{
			Actor actor = null;
			try
			{
				actor = npc.lover;
			}
			catch
			{
			}
			if (actor == null)
			{
				return 0;
			}
			float num = 50f;
			try
			{
				int num2 = 0;
				Record record = Chronicle.Of(npc);
				if (record != null)
				{
					foreach (Kin item in record.Kin)
					{
						if (!(item.Role != "ch"))
						{
							Actor actor2 = Chronicle.Find(item.Id);
							if (actor2 != null && actor2.isAlive() && actor2.isChildOf(actor))
							{
								num2++;
							}
						}
					}
				}
				num += (float)Mathf.Min(num2 * 8, 24);
				int num3 = Dialogue.SafeHappiness(npc);
				num += ((num3 < -20) ? (-25f) : ((num3 < 10) ? (-10f) : ((num3 > 60) ? 10f : 0f)));
				if (npc.city != actor.city)
				{
					num -= 15f;
				}
				if (Has(npc, "lustful"))
				{
					num -= 20f;
				}
				if (Has(npc, "deceitful"))
				{
					num -= 10f;
				}
				if (Has(npc, "honest"))
				{
					num += 10f;
				}
				if (Has(npc, "content"))
				{
					num += 8f;
				}
				if (Has(actor, "evil"))
				{
					num -= 10f;
				}
				if (Has(actor, "ugly"))
				{
					num -= 8f;
				}
				if (Has(actor, "bloodlust") || Has(actor, "hotheaded") || Has(actor, "savage"))
				{
					num -= 6f;
				}
				if (Has(actor, "attractive"))
				{
					num += 8f;
				}
				if (actor.isKing() || actor.isCityLeader())
				{
					num += 10f;
				}
			}
			catch
			{
			}
			return Mathf.Clamp(Mathf.RoundToInt(num), 0, 100);
		}

		public static int ExtraTasks(int devotion)
		{
			if (devotion >= 25)
			{
				if (devotion >= 75)
				{
					return 2;
				}
				return 1;
			}
			return 0;
		}

		public static int AskAtFor(Actor npc, Actor you)
		{
			return 100;
		}

		public static void EndIt(Actor you, Actor lover)
		{
			if (you == null || lover == null)
			{
				return;
			}
			try
			{
				if (lover.lover == you)
				{
					lover.setLover(null);
				}
				if (you.lover == lover)
				{
					you.setLover(null);
				}
			}
			catch (Exception ex)
			{
				Log.Error("romance: end it: " + ex.Message);
			}
			Rivals.Heartbreak(lover, 40);
			Chronicle.ChangeOpinion(lover, you, -50);
			Chronicle.AddOwnMemory(lover, "fp_ended", Dialogue.NameOf(you));
			int num = 0;
			try
			{
				Record record = Chronicle.Of(lover);
				HashSet<long> hashSet = new HashSet<long>
				{
					you.data.id,
					lover.data.id
				};
				if (record != null)
				{
					List<Kin> list = new List<Kin>();
					foreach (Kin item in record.Kin)
					{
						if (item.Role == "p1" || item.Role == "p2" || item.Role == "ch")
						{
							list.Add(item);
						}
					}
					list.AddRange(Chronicle.SiblingsOf(record));
					foreach (Kin item2 in list)
					{
						if (hashSet.Add(item2.Id))
						{
							Actor actor = Chronicle.Find(item2.Id);
							if (actor != null && actor.isAlive())
							{
								Chronicle.ChangeOpinion(actor, you, -15);
								num++;
							}
						}
					}
				}
			}
			catch (Exception ex2)
			{
				Log.Error("romance: end it, family: " + ex2.Message);
			}
			Log.Info($"romance: {Dialogue.NameOf(you)} ends it with {Dialogue.NameOf(lover)} ({num} of their family cool on you)");
		}

		public static string WithStanding(string relation, Actor npc, Actor you)
		{
			string text = null;
			try
			{
				text = Standing(npc, you);
			}
			catch
			{
			}
			if (text != null)
			{
				return relation + "  ·  " + text;
			}
			return relation;
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

		private static ulong Hash(Actor a)
		{
			ulong num = 1469598103934665603uL;
			long num2 = 0L;
			try
			{
				num2 = a.data.id;
			}
			catch
			{
			}
			for (int i = 0; i < 8; i++)
			{
				num ^= (ulong)((num2 >> i * 8) & 0xFF);
				num *= 1099511628211L;
			}
			return num >> 8;
		}
	}
}
