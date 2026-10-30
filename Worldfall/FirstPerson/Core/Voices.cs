using System;
using System.Collections.Generic;
using System.Text;

namespace FirstPerson.Core
{
	public static class Voices
	{
		public enum Temper : byte
		{
			Plain,
			Cruel,
			Hothead,
			Sly,
			Greedy,
			Wary,
			Lustful,
			Ambitious,
			Pious,
			Gentle,
			Honest,
			Clever,
			Wise,
			Dull,
			Hardened,
			Easy
		}

		public enum AgeBand : byte
		{
			Grown,
			Child,
			Young,
			Elder
		}

		public enum Accent : byte
		{
			Human,
			Elf,
			Orc,
			Dwarf,
			Aloof,
			Flock,
			Hive,
			Pack,
			Burrow,
			Swamp
		}

		public enum Mood : byte
		{
			Normal,
			Happy,
			Sad,
			Angry,
			Dark
		}

		public enum Bond : byte
		{
			Stranger,
			Acquaintance,
			Friend,
			Close
		}

		public enum Hour : byte
		{
			Dawn,
			Day,
			Dusk,
			Night
		}

		public enum Sky : byte
		{
			Clear,
			Rain,
			Snow,
			Storm
		}

		[Flags]
		public enum Plight : ushort
		{
			None = 0,
			Hungry = 1,
			Hurt = 2,
			Sick = 4,
			InLove = 8,
			Bereaved = 0x10,
			Rich = 0x20,
			Poor = 0x40
		}

		public enum Tie : byte
		{
			None,
			Lover,
			Kin,
			Sovereign,
			Enemy,
			Foreign
		}

		public struct Persona
		{
			public long Id;

			public Temper First;

			public Temper Second;

			public AgeBand Age;

			public Accent Accent;

			public bool Female;

			public string Job;

			public Mood Mood;

			public Bond Bond;

			public Plight Plight;

			public Tie Tie;

			public int Warmth;

			public bool Met;

			public bool Child => Age == AgeBand.Child;

			public bool Has(Temper t)
			{
				if (First != t)
				{
					return Second == t;
				}
				return true;
			}
		}

		public struct Scene
		{
			public Hour Hour;

			public Sky Sky;

			public int Season;
		}

		public sealed class Slots
		{
			public string You;

			public string Name;

			public string Town;

			public string Realm;

			public string Lover;

			public string Thing;

			public string Title;

			public string Get(string hole)
			{
				switch (hole)
				{
				case "you":
					return You;
				case "name":
					return Name;
				case "town":
					return Town;
				case "realm":
					return Realm;
				case "lover":
					return Lover;
				case "thing":
					return Thing;
				case "title":
					return Title;
				default:
					return null;
				}
			}
		}

		public struct Candidate
		{
			public string Line;

			public float Weight;
		}

		public const int Picks = 24;

		private static readonly string[] TemperTags = new string[16]
		{
			"plain", "cruel", "hothead", "sly", "greedy", "wary", "lustful", "ambitious", "pious", "gentle",
			"honest", "clever", "wise", "dull", "hardened", "easy"
		};

		private static readonly string[] AccentTags = new string[10] { "human", "elf", "orc", "dwarf", "aloof", "flock", "hive", "pack", "burrow", "swamp" };

		private static readonly string[] MoodTags = new string[5] { null, "happy", "sad", "angry", "dark" };

		private static readonly string[] HourTags = new string[4] { "dawn", "day", "dusk", "night" };

		private static readonly string[] SkyTags = new string[4] { null, "rain", "snow", "storm" };

		private static readonly string[] SeasonTags = new string[4] { "spring", "summer", "autumn", "winter" };

		private static readonly string[] TieTags = new string[6] { null, "lover", "kin", "sovereign", "enemy", "foreign" };

		private static Dictionary<string, string[]> _pools;

		private static readonly object PoolLock = new object();

		[ThreadStatic]
		private static List<Candidate> _cands;

		[ThreadStatic]
		private static StringBuilder _fill;

		private static Dictionary<string, string[]> Pools
		{
			get
			{
				if (_pools != null)
				{
					return _pools;
				}
				lock (PoolLock)
				{
					if (_pools == null)
					{
						Dictionary<string, string[]> dictionary = new Dictionary<string, string[]>(StringComparer.Ordinal);
						Greetings(dictionary);
						TalkLines(dictionary);
						Partings(dictionary);
						_pools = dictionary;
					}
				}
				return _pools;
			}
		}

		public static IEnumerable<KeyValuePair<string, string[]>> AllPools => Pools;

		public static string TagOf(Temper t)
		{
			return TemperTags[(uint)t];
		}

		public static string TagOf(Accent a)
		{
			return AccentTags[(uint)a];
		}

		public static bool HasPool(string key)
		{
			return Pools.ContainsKey(key);
		}

		private static List<Candidate> Fresh()
		{
			if (_cands == null)
			{
				_cands = new List<Candidate>(64);
			}
			_cands.Clear();
			return _cands;
		}

		private static void Take(List<Candidate> into, string key, float weight, Slots slots)
		{
			if (weight <= 0f || key == null || !Pools.TryGetValue(key, out var value))
			{
				return;
			}
			int count = into.Count;
			string[] array = value;
			foreach (string line in array)
			{
				if (Fillable(line, slots))
				{
					into.Add(new Candidate
					{
						Line = line,
						Weight = 1f
					});
				}
			}
			int num = into.Count - count;
			for (int j = count; j < into.Count; j++)
			{
				Candidate value2 = into[j];
				value2.Weight = weight / (float)num;
				into[j] = value2;
			}
		}

		private static void TakeTag(List<Candidate> into, string beat, string tag, float weight, Slots slots)
		{
			if (tag != null)
			{
				Take(into, beat + "." + tag, weight, slots);
			}
		}

		public static string Line(string beat, in Persona p, in Scene s, Slots slots, ulong seed, IList<ushort> recent)
		{
			ushort id;
			return Line(beat, in p, in s, slots, seed, recent, out id);
		}

		public static string Line(string beat, in Persona p, in Scene s, Slots slots, ulong seed, IList<ushort> recent, out ushort id)
		{
			id = 0;
			List<Candidate> list = Fresh();
			Gather(beat, in p, in s, slots, list);
			if (list.Count == 0)
			{
				return null;
			}
			string text = Pick(list, seed, recent);
			if (text == null)
			{
				return null;
			}
			id = IdOf(text);
			return Fill(text, slots);
		}

		public static int Count(string beat, in Persona p, in Scene s, Slots slots)
		{
			List<Candidate> list = Fresh();
			Gather(beat, in p, in s, slots, list);
			return list.Count;
		}

		private static void Gather(string beat, in Persona p, in Scene s, Slots slots, List<Candidate> c)
		{
			string text = TemperTags[(uint)p.First];
			string tag = ((p.Second != p.First) ? TemperTags[(uint)p.Second] : null);
			if (p.Child && HasPool(beat + ".child"))
			{
				TakeTag(c, beat, "child", 1f, slots);
				if (beat == "open" || beat == "part")
				{
					TakeTag(c, beat, HourTags[(uint)s.Hour], 0.3f, slots);
				}
				return;
			}
			string text2 = TieTags[(uint)p.Tie];
			if (text2 != null && HasPool(beat + "." + text2))
			{
				bool flag = p.Tie == Tie.Sovereign || p.Tie == Tie.Lover || p.Tie == Tie.Enemy;
				if (p.Tie == Tie.Sovereign && ((int)p.Mood >= 2 || p.Warmth <= 0) && HasPool(beat + ".sovereign_cold"))
				{
					text2 = "sovereign_cold";
				}
				TakeTag(c, beat, text2, flag ? 1f : 4f, slots);
				if (flag)
				{
					return;
				}
			}
			TakeTag(c, beat, "plain", 2f, slots);
			if (beat == "open")
			{
				TakeTag(c, beat, p.Met ? "known" : "new", 2f, slots);
			}
			TakeTag(c, beat, text, (text == "plain") ? 0f : 3f, slots);
			TakeTag(c, beat, tag, 1.5f, slots);
			if (p.Age == AgeBand.Elder)
			{
				TakeTag(c, beat, "elder", 2.5f, slots);
			}
			if (p.Accent != Accent.Human)
			{
				TakeTag(c, beat, AccentTags[(uint)p.Accent], 1.2f, slots);
			}
			TakeTag(c, beat, MoodTags[(uint)p.Mood], 2.5f, slots);
			if (beat == "open")
			{
				TakeTag(c, beat, HourTags[(uint)s.Hour], 1f, slots);
				TakeTag(c, beat, SkyTags[(uint)s.Sky], 2f, slots);
				if (s.Season >= 0 && s.Season < 4)
				{
					TakeTag(c, beat, SeasonTags[s.Season], 0.6f, slots);
				}
				if (p.Warmth <= -1)
				{
					TakeTag(c, beat, "cold", (p.Warmth <= -2) ? 8f : 4f, slots);
				}
				else if (p.Bond == Bond.Close)
				{
					TakeTag(c, beat, "close", 4f, slots);
				}
				else if (p.Bond == Bond.Friend || p.Warmth >= 2)
				{
					TakeTag(c, beat, "friend", 3f, slots);
				}
			}
			else if (beat == "part")
			{
				if (s.Hour == Hour.Dusk || s.Hour == Hour.Night)
				{
					TakeTag(c, beat, HourTags[(uint)s.Hour], 1.5f, slots);
				}
				if ((int)p.Bond >= 2 && p.Warmth >= 0)
				{
					TakeTag(c, beat, "friend", 2f, slots);
				}
			}
		}

		public static string Pick(List<Candidate> c, ulong seed, IList<ushort> recent)
		{
			float num = 0f;
			int num2 = 0;
			for (int i = 0; i < c.Count; i++)
			{
				if (recent == null || !recent.Contains(IdOf(c[i].Line)))
				{
					num += c[i].Weight;
					num2++;
				}
			}
			if (num2 == 0)
			{
				string text = null;
				int num3 = int.MaxValue;
				for (int j = 0; j < c.Count; j++)
				{
					int num4 = recent.IndexOf(IdOf(c[j].Line));
					if (num4 >= 0 && num4 < num3)
					{
						num3 = num4;
						text = c[j].Line;
					}
				}
				return text ?? c[0].Line;
			}
			double num5 = Unit(ref seed) * (double)num;
			for (int k = 0; k < c.Count; k++)
			{
				if (recent == null || !recent.Contains(IdOf(c[k].Line)))
				{
					num5 -= (double)c[k].Weight;
					if (num5 <= 0.0)
					{
						return c[k].Line;
					}
				}
			}
			for (int num6 = c.Count - 1; num6 >= 0; num6--)
			{
				if (recent == null || !recent.Contains(IdOf(c[num6].Line)))
				{
					return c[num6].Line;
				}
			}
			return c[0].Line;
		}

		public static bool Fillable(string line, Slots slots)
		{
			int num = line.IndexOf('{');
			while (num >= 0)
			{
				int num2 = line.IndexOf('}', num + 1);
				if (num2 < 0)
				{
					return true;
				}
				if (string.IsNullOrEmpty(slots?.Get(line.Substring(num + 1, num2 - num - 1))))
				{
					return false;
				}
				num = line.IndexOf('{', num2 + 1);
			}
			return true;
		}

		public static string Fill(string line, Slots slots)
		{
			if (line.IndexOf('{') < 0)
			{
				return line;
			}
			if (_fill == null)
			{
				_fill = new StringBuilder(128);
			}
			StringBuilder fill = _fill;
			fill.Length = 0;
			int num = 0;
			while (num < line.Length)
			{
				int num2 = line.IndexOf('{', num);
				if (num2 < 0)
				{
					fill.Append(line, num, line.Length - num);
					break;
				}
				int num3 = line.IndexOf('}', num2 + 1);
				if (num3 < 0)
				{
					fill.Append(line, num, line.Length - num);
					break;
				}
				fill.Append(line, num, num2 - num);
				string text = slots?.Get(line.Substring(num2 + 1, num3 - num2 - 1)) ?? "";
				if (text.Length > 0 && char.IsLower(text[0]) && SentenceStart(fill))
				{
					text = char.ToUpperInvariant(text[0]) + text.Substring(1);
				}
				fill.Append(text);
				num = num3 + 1;
			}
			return fill.ToString();
		}

		private static bool SentenceStart(StringBuilder sb)
		{
			int length = sb.Length;
			if (length == 0)
			{
				return true;
			}
			if (length >= 2 && sb[length - 1] == ' ')
			{
				char c = sb[length - 2];
				if (c != '.' && c != '!' && c != '?')
				{
					return c == '*';
				}
				return true;
			}
			return false;
		}

		public static ushort IdOf(string line)
		{
			uint num = 2166136261u;
			for (int i = 0; i < line.Length; i++)
			{
				num ^= line[i];
				num *= 16777619;
			}
			return (ushort)(num ^ (num >> 16));
		}

		public static ulong Seed(long person, int talk, int index, string beat)
		{
			ulong h = 14695981039346656037uL;
			Mix(ref h, (ulong)person);
			Mix(ref h, (uint)talk);
			Mix(ref h, (uint)index);
			if (beat != null)
			{
				for (int i = 0; i < beat.Length; i++)
				{
					h ^= beat[i];
					h *= 1099511628211L;
				}
			}
			if (h != 0L)
			{
				return h;
			}
			return 1uL;
		}

		private static void Mix(ref ulong h, ulong v)
		{
			for (int i = 0; i < 8; i++)
			{
				h ^= (v >> i * 8) & 0xFF;
				h *= 1099511628211uL;
			}
		}

		public static double Unit(ref ulong s)
		{
			s ^= s << 13;
			s ^= s >> 7;
			s ^= s << 17;
			return (double)(s >> 11) * 1.1102230246251565E-16;
		}

		public static void Pack(List<string> beats, List<string> pages, int room = 140)
		{
			StringBuilder stringBuilder = new StringBuilder(160);
			foreach (string beat in beats)
			{
				if (!string.IsNullOrEmpty(beat))
				{
					if (stringBuilder.Length > 0 && stringBuilder.Length + 1 + beat.Length > room)
					{
						pages.Add(stringBuilder.ToString());
						stringBuilder.Length = 0;
					}
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('\n');
					}
					stringBuilder.Append(beat);
				}
			}
			if (stringBuilder.Length > 0)
			{
				pages.Add(stringBuilder.ToString());
			}
		}

		public static string MoodWord(in Persona p)
		{
			if ((p.Plight & Plight.Bereaved) != Plight.None)
			{
				return "grieving";
			}
			if ((p.Plight & Plight.Sick) != Plight.None)
			{
				return "unwell";
			}
			if ((p.Plight & Plight.Hurt) != Plight.None)
			{
				return "hurting";
			}
			if ((p.Plight & Plight.Hungry) != Plight.None)
			{
				return "hungry";
			}
			switch (p.Mood)
			{
			case Mood.Happy:
				if (p.Warmth < 1)
				{
					return "pleased";
				}
				return "glad to see you";
			case Mood.Sad:
				return "low";
			case Mood.Angry:
				return "sour";
			case Mood.Dark:
				return "bitter";
			default:
				if (p.Warmth <= -2)
				{
					return "hostile";
				}
				if (p.Warmth == -1)
				{
					return "wary";
				}
				if (p.Warmth >= 2)
				{
					return "fond of you";
				}
				if ((p.Plight & Plight.InLove) != Plight.None)
				{
					return "in love";
				}
				return null;
			}
		}

		private static void Greetings(Dictionary<string, string[]> d)
		{
			d["open.plain"] = new string[14]
			{
				"What is it?", "Yes?", "Speak up, then.", "Something you need?", "I'm listening.", "Go on.", "Mm. Hello.", "There's work waiting. What is it?", "What brings you over?", "You look like you've come a way.",
				"Hello.", "Well?", "Hm. What can I do for you?", "Ah. Someone to talk to."
			};
			d["open.known"] = new string[9] { "You again.", "Back, are you?", "Ah. {you}.", "{you}. What is it this time?", "I wondered when you'd turn up.", "Thought I'd seen the last of you.", "Oh, it's you.", "{you}. Good.", "Look who it is." };
			d["open.new"] = new string[6] { "I don't know you.", "Haven't seen you before.", "A new face. What do you want?", "You're not from here.", "Do I know you?", "Who are you, then?" };
			d["open.dawn"] = new string[5] { "Early to be about.", "You're up early.", "Morning. The day's barely started.", "Morning.", "Sun's hardly up." };
			d["open.day"] = new string[4] { "Good day.", "Afternoon.", "Middle of the day. Busy time.", "Day's half gone already." };
			d["open.dusk"] = new string[4] { "Evening.", "Getting late.", "Sun's going down. Make it quick.", "Evening. Work's nearly done." };
			d["open.night"] = new string[6] { "It's late.", "Late to be walking about.", "Bit dark for visiting.", "Couldn't sleep either?", "Night. Keep your voice down.", "What are you doing out at this hour?" };
			d["open.rain"] = new string[5] { "Wet enough for you?", "Foul weather.", "Rain again.", "Get out of the rain, at least.", "This rain won't quit." };
			d["open.snow"] = new string[4] { "Cold enough for you?", "Snow again.", "Stamp your feet, you'll freeze.", "Bitter out there." };
			d["open.storm"] = new string[4] { "Hear that thunder?", "Some storm, this.", "Keep away from the trees in this.", "The sky's angry today." };
			d["open.spring"] = new string[3] { "Spring at last.", "Mud everywhere this time of year.", "The fields are waking up." };
			d["open.summer"] = new string[3] { "Hot one today.", "Long days, this time of year.", "Too warm to work." };
			d["open.autumn"] = new string[3] { "Harvest time. Everyone's busy.", "Leaves are turning.", "Days are getting shorter." };
			d["open.winter"] = new string[4] { "Winter. I hate it.", "Cold, isn't it.", "Long nights now.", "Keep warm. Winter's long." };
			d["open.happy"] = new string[5] { "Good day to you.", "Can't complain today.", "Good timing. I'm in a fine mood.", "Feeling well today. What is it?", "Things are going well. What do you need?" };
			d["open.sad"] = new string[5] { "*sighs* Hello.", "Not the best day.", "Oh. It's you.", "I'm not much company today.", "Sorry. My mind's elsewhere." };
			d["open.angry"] = new string[5] { "What now?", "This had better be worth it.", "Not now. Well, go on.", "I'm in no mood.", "Say it and go." };
			d["open.dark"] = new string[4] { "*stares* What.", "Everything's going wrong. What do you want?", "Leave me be. ...What?", "Some days I wonder why I bother." };
			d["open.cruel"] = new string[3] { "Well, well. Look what's walked up.", "*smiles thinly* Yes?", "You've got nerve, coming to me." };
			d["open.hothead"] = new string[3] { "What?", "Spit it out.", "Don't waste my time." };
			d["open.sly"] = new string[3] { "Ah, just the person.", "Keep your voice low. What is it?", "*glances round* Go on." };
			d["open.greedy"] = new string[3] { "Come to spend something?", "Got coin on you?", "If it's a favour, it'll cost you." };
			d["open.wary"] = new string[3] { "Who sent you?", "*looks you over* What do you want?", "Stay where I can see you." };
			d["open.lustful"] = new string[3] { "Well, hello.", "Now you've brightened my day.", "*looks you up and down* Yes?" };
			d["open.ambitious"] = new string[3] { "Make it quick. I've plans.", "Time's short. Talk.", "If it's worth my while, go on." };
			d["open.pious"] = new string[3] { "Peace be on you.", "The gods keep you.", "Blessings. What is it?" };
			d["open.gentle"] = new string[3] { "Good to see you.", "Come, sit a moment.", "Hello, friend." };
			d["open.honest"] = new string[3] { "Hello. What do you need?", "Say what you came to say.", "I'll help if I can." };
			d["open.clever"] = new string[3] { "Ah. I was just thinking about something.", "You've a question. I can see it.", "Go on. I'm curious." };
			d["open.wise"] = new string[3] { "Sit a while.", "Slow down. What is it?", "There's always time to talk." };
			d["open.dull"] = new string[3] { "Oh. Hi.", "Huh? Oh, hello.", "*blinks* Hello." };
			d["open.hardened"] = new string[3] { "Keep it short.", "*nods* What is it?", "Speak plain. I'm listening." };
			d["open.easy"] = new string[3] { "What's the hurry?", "Easy. What is it?", "No rush. What do you need?" };
			d["open.child"] = new string[13]
			{
				"Hi!", "Who are you?", "Are you a soldier?", "Mother says not to talk to strangers.", "Can you lift a sword?", "Where are you from?", "Are you going somewhere far?", "Hello!", "Are you lost?", "Is that a real sword?",
				"My brother is stronger than you.", "Did the king send you?", "Are you a hero?"
			};
			d["open.elder"] = new string[5] { "Come closer. My eyes aren't what they were.", "Visitors. Nobody visits the old.", "Sit, sit. My knees won't let me stand long.", "Eh? Speak up.", "Another one come to see the old fool." };
			d["open.elf"] = new string[3] { "The forest is quiet today. What brings you?", "Patience. Speak when you're ready.", "Walk softly here." };
			d["open.orc"] = new string[3] { "Talk.", "You. Speak.", "Quick. What?" };
			d["open.dwarf"] = new string[3] { "Mind your boots, I just swept.", "Well? Out with it, I've work at the anvil.", "Hmph. Visitors." };
			d["open.aloof"] = new string[2] { "*looks away, then back* Yes?", "You may speak." };
			d["open.flock"] = new string[2] { "*fluffs up* Hello, hello.", "Oh! A visitor." };
			d["open.hive"] = new string[2] { "We see you. Speak.", "*antennae twitch* Yes?" };
			d["open.pack"] = new string[2] { "*sniffs* You. What?", "Friend or not? Speak." };
			d["open.burrow"] = new string[2] { "*peeks out* Oh. Hello.", "Quick, quick. What is it?" };
			d["open.swamp"] = new string[2] { "*blinks slowly* Mm. Hello.", "Warm mud today. What do you want?" };
			d["open.lover"] = new string[6] { "There you are.", "I missed you.", "{you}. Come here.", "You're late. I was starting to worry.", "*smiles* I was hoping you'd come.", "Finally." };
			d["open.kin"] = new string[6] { "There you are. Have you eaten?", "{you}. Good to see you home.", "Look at you.", "Family. Come in.", "You never visit enough.", "Ah, {you}. Everyone well?" };
			d["open.sovereign"] = new string[6] { "*bows* {title}.", "*bows low* {title}. You honour us.", "{title}. How may I serve?", "*drops to one knee* {title}.", "We're honoured, {title}.", "*bows* What do you require, {title}?" };
			d["open.sovereign_cold"] = new string[3] { "*bows stiffly* {title}.", "*lowers their eyes* {title}. What do you require?", "{title}." };
			d["open.enemy"] = new string[5] { "You've some nerve walking in here.", "One step closer and I call the guards.", "Get away from me.", "Your kind aren't welcome here.", "I know where you're from." };
			d["open.foreign"] = new string[5] { "You're a long way from home.", "A foreigner. What brings you?", "Not from round here, are you?", "Travelling far?", "Strangers don't come often. What is it?" };
			d["open.cold"] = new string[5] { "Oh. You.", "What do you want now?", "I've nothing to say to you.", "You. Again.", "Make it short." };
			d["open.friend"] = new string[5] { "Good to see you, {you}.", "{you}! Come, sit.", "There's a face I like.", "Ah, {you}. Good.", "Glad you stopped by." };
			d["open.close"] = new string[4] { "{you}. I was hoping you'd come.", "My friend. Sit down.", "There you are. I've been wanting to talk.", "{you}. Always welcome here." };
			d["call.crime.plain"] = new string[5] { "I saw what you did. Don't think I've forgotten.", "I know what you are. I was there.", "People talk. I saw it myself.", "Keep your hands where I can see them.", "After what you did, you've some nerve." };
			d["call.crime.cruel"] = new string[1] { "I saw what you did. I liked it." };
			d["call.crime.gentle"] = new string[1] { "I saw what you did. I pray you've changed." };
			d["call.crime.greedy"] = new string[1] { "I saw what you did. My silence costs, you know." };
			d["call.crime.child"] = new string[1] { "I saw you. I'm telling." };
			d["call.quest.plain"] = new string[5] { "I haven't forgotten your help.", "You did right by me. I remember.", "Still owe you for that work.", "You saw to {thing}. I won't forget it.", "That business with {thing}. Good work." };
			d["call.quest.greedy"] = new string[1] { "You worked cheap last time. I liked that." };
			d["call.quest.gentle"] = new string[1] { "You were kind to help me. I still think of it." };
			d["call.gift.plain"] = new string[4] { "Still using what you gave me.", "That gift of yours. Thank you again.", "You've been generous with me.", "I still think about that gift." };
			d["call.gift.greedy"] = new string[1] { "Brought another gift? No? Pity." };
			d["call.sameday.plain"] = new string[4] { "Back so soon?", "Twice in one day.", "Forgot something?", "You were just here." };
			d["call.sameday.hothead"] = new string[1] { "You again? Already?" };
			d["call.longago.plain"] = new string[5] { "It's been a long while.", "Thought you'd forgotten us.", "Where have you been all this time?", "Seasons since I last saw you.", "Look who finally came back." };
			d["call.longago.elder"] = new string[1] { "Ah. I thought I'd be dead before you came back." };
			d["call.topic_family.plain"] = new string[2] { "Still thinking about my family?", "You asked after my family last time. Kind of you." };
			d["call.topic_faith.plain"] = new string[2] { "Thought more about the gods?", "Been praying, like I said?" };
			d["call.topic_news.plain"] = new string[2] { "Heard any more news since?", "Anything new since we last spoke?" };
			d["call.topic_life.plain"] = new string[2] { "You listened last time. Not many do.", "Still want to hear about my life?" };
			d["call.topic_trade.plain"] = new string[2] { "Come to buy again?", "Need more supplies?" };
			d["hook.hungry.plain"] = new string[4] { "There's no bread in the house.", "Haven't eaten since yesterday.", "Stores are empty. Everyone's hungry.", "Hard to think on an empty stomach." };
			d["hook.hungry.child"] = new string[1] { "I'm hungry. Do you have food?" };
			d["hook.hurt.plain"] = new string[4] { "Mind the arm. It's still mending.", "Took a bad knock. Still feel it.", "Forgive me if I don't stand. Hurt my leg.", "These wounds take their time." };
			d["hook.hurt.hardened"] = new string[1] { "Just a scratch. I've had worse." };
			d["hook.sick.plain"] = new string[4] { "Keep back. I've been coughing all week.", "Don't get close. I'm not well.", "Feverish all night.", "Something's in my chest. Won't shift." };
			d["hook.inlove.plain"] = new string[3] { "Can't stop thinking about {lover}.", "Have you met {lover}? You should.", "{lover} and I... well. Things are good." };
			d["hook.rich.plain"] = new string[3] { "Good month for me. Can't complain.", "Business is good.", "Things are looking up. Coin in my pocket for once." };
			d["hook.rich.greedy"] = new string[1] { "Doing well. Not that I'd share." };
			d["hook.poor.plain"] = new string[3] { "Not a coin to my name.", "Times are hard.", "Owe half the town, I do." };
		}

		private static void Partings(Dictionary<string, string[]> d)
		{
			d["part.plain"] = new string[12]
			{
				"Go on, then.", "Safe roads.", "I've work to do.", "Take care.", "Until next time.", "Off you go.", "That's enough talk.", "Back to it, then.", "Mind how you go.", "Well. Good day.",
				"I'll be here.", "Go well."
			};
			d["part.dusk"] = new string[3] { "Get home before dark.", "Light's going. Off you go.", "Evening's coming. Don't linger." };
			d["part.night"] = new string[4] { "Mind the dark on the road.", "Get some sleep.", "Wolves are out at night. Careful.", "Late. Go home." };
			d["part.friend"] = new string[4] { "Come back soon, {you}.", "Don't be a stranger.", "Always good, {you}.", "Look after yourself." };
			d["part.cruel"] = new string[2] { "Watch your back.", "Run along." };
			d["part.hothead"] = new string[2] { "Finally.", "Go, then." };
			d["part.sly"] = new string[2] { "We never spoke.", "Remember what I said." };
			d["part.greedy"] = new string[2] { "Come back with coin.", "Next time, bring something." };
			d["part.wary"] = new string[2] { "Don't follow me.", "Go. And don't tell anyone you saw me." };
			d["part.lustful"] = new string[2] { "Don't keep me waiting next time.", "*watches you go*" };
			d["part.ambitious"] = new string[2] { "I've places to be.", "Time's wasting." };
			d["part.pious"] = new string[2] { "Gods keep you.", "Go in peace." };
			d["part.gentle"] = new string[2] { "Take care of yourself.", "Be safe out there." };
			d["part.honest"] = new string[2] { "Good talking with you.", "Straight roads to you." };
			d["part.clever"] = new string[2] { "Think on what I said.", "Interesting talk." };
			d["part.wise"] = new string[2] { "Walk slowly. You'll see more.", "Until the next time." };
			d["part.dull"] = new string[2] { "Bye.", "Oh. Bye, then." };
			d["part.hardened"] = new string[2] { "Keep your guard up.", "Stay alive." };
			d["part.easy"] = new string[2] { "See you around.", "No hurry. Come back any time." };
			d["part.child"] = new string[13]
			{
				"Bye!", "Come back tomorrow!", "Wait! ...Never mind. Bye.", "I have to go home now.", "*waves*", "Bye-bye!", "Will you come back?", "I'll tell everyone I met you!", "Mother's calling. Bye!", "Don't forget me!",
				"See you!", "*runs off*", "I'm going to play now."
			};
			d["part.elder"] = new string[3] { "Off you go. Visit an old fool again sometime.", "Mind the step.", "Go on. I'll sit here a while." };
			d["part.elf"] = new string[2] { "May the trees shelter you.", "Walk softly." };
			d["part.dwarf"] = new string[2] { "Stone keep you.", "Mind your boots." };
			d["part.orc"] = new string[2] { "Go. Fight well.", "Strength." };
			d["part.aloof"] = new string[1] { "*turns away*" };
			d["part.flock"] = new string[1] { "Bye-bye now." };
			d["part.hive"] = new string[1] { "We will remember you." };
			d["part.pack"] = new string[1] { "Run well." };
			d["part.burrow"] = new string[1] { "Quick, quick, off you go." };
			d["part.swamp"] = new string[1] { "Mm. Go slow." };
			d["part.sovereign"] = new string[4] { "*bows* {title}.", "At your service, {title}.", "Long may you reign.", "*bows low* Go safely, {title}." };
			d["part.sovereign_cold"] = new string[2] { "*bows stiffly* {title}.", "*bows* As you wish, {title}." };
			d["part.lover"] = new string[4] { "Don't be long.", "Come home soon.", "*kisses your cheek* Go on.", "I'll be waiting." };
			d["part.kin"] = new string[4] { "Visit more often.", "Eat something on the way.", "Give everyone my love.", "Mind yourself out there." };
			d["part.enemy"] = new string[4] { "Good riddance.", "Don't come back.", "Get out of my sight.", "Next time, the guards." };
			d["bye.quest.plain"] = new string[3] { "See it done.", "Don't be long with that work.", "I'm counting on you." };
			d["bye.trade.plain"] = new string[2] { "Come back when you need more.", "Spend it well." };
			d["bye.family.plain"] = new string[2] { "My regards to your family.", "Mind your own kin." };
			d["bye.news.plain"] = new string[2] { "Keep your ears open.", "If you hear more, tell me." };
			d["bye.faith.plain"] = new string[2] { "The gods keep you.", "Pray now and then." };
			d["bye.gift.plain"] = new string[1] { "Thank you again." };
			d["bye.smalltalk.plain"] = new string[2] { "Good talk.", "Nice to just talk, for once." };
			d["thanks.plain"] = new string[8] { "Thank you.", "That's kind of you.", "I won't forget this.", "You didn't have to.", "*smiles* Thank you.", "Generous. Thank you.", "I'll put it to good use.", "That means something. Thank you." };
			d["thanks.greedy"] = new string[3] { "Now that's how you greet someone.", "More of this, and we'll be friends.", "*counts it twice* Good." };
			d["thanks.gentle"] = new string[2] { "Bless you. Truly.", "You're too good to me." };
			d["thanks.cruel"] = new string[2] { "Hm. Buying favour?", "I'll take it. Don't expect thanks." };
			d["thanks.honest"] = new string[2] { "Thank you. I mean it.", "That was decent of you." };
			d["thanks.sly"] = new string[2] { "Generous. What do you want?", "Gifts always come with strings. Thank you." };
			d["thanks.wary"] = new string[2] { "What's the catch?", "Why? ...Thank you, I suppose." };
			d["thanks.hothead"] = new string[1] { "Well. Didn't expect that. Thanks." };
			d["thanks.dull"] = new string[1] { "Oh! For me? Thanks." };
			d["thanks.lustful"] = new string[1] { "*smiles slowly* Thank you." };
			d["thanks.child"] = new string[3] { "For me? Thank you!", "Can I really keep it?", "*beams* Thank you!" };
			d["thanks.lover"] = new string[3] { "You spoil me.", "*holds your hand* Thank you.", "You didn't need to. I love it." };
			d["thanks.sovereign"] = new string[3] { "You honour me, {title}.", "*bows* Thank you, {title}.", "A gift from the crown. I'm humbled." };
		}

		private static void TalkLines(Dictionary<string, string[]> d)
		{
			d["small.farmer.plain"] = new string[6] { "The wheat needs rain.", "Up before dawn, every day. That's farming.", "Crows got half the seed last week.", "Good soil here. Better than most.", "My back aches from the rows.", "If the harvest holds, we eat this winter." };
			d["small.woodcutter.plain"] = new string[5] { "Good timber in these woods.", "Felled three trees before noon.", "Keep the axe sharp and the tree falls where you want.", "The forest gives, but it takes its time.", "Splinters. Always splinters." };
			d["small.miner.plain"] = new string[4] { "Dark down there. You get used to it.", "Found a good seam last week.", "Stone dust in everything. My food, my bed.", "The deep tunnels make you quiet." };
			d["small.builder.plain"] = new string[5] { "There's always another house to raise.", "Good walls keep a town alive.", "Need more stone. Always more stone.", "Built half the houses on this street.", "A roof that doesn't leak. That's the job." };
			d["small.hunter.plain"] = new string[5] { "Tracks are thin this season.", "Patience. That's all hunting is.", "Wolves have been bold lately.", "Fresh meat for the town tonight, if I'm lucky.", "The deer know my scent by now." };
			d["small.fisher.plain"] = new string[5] { "The fish come in at dawn.", "Nets need mending again.", "The water gives what it wants to.", "Caught nothing but weed yesterday.", "Calm water today. Good for the boats." };
			d["small.gatherer.plain"] = new string[5] { "Berries are thin this year.", "I know every bush from here to the river.", "Mushrooms after the rain. Best time.", "Fill the baskets, feed the town.", "Long walks for little, some days." };
			d["small.smith.plain"] = new string[5] { "The forge never cools.", "Good iron is hard to come by.", "Every blade in this town passed my anvil.", "Burnt my hand again this morning.", "Bring me ore and I'll make you something worth keeping." };
			d["small.warrior.plain"] = new string[5] { "Drilled since dawn.", "Quiet on the walls. For now.", "A soldier waits more than he fights.", "Keep your blade oiled. First thing they teach you.", "Seen any trouble on the roads?" };
			d["small.leader.plain"] = new string[4] { "Everyone brings me their troubles.", "Keeping {town} fed is no small thing.", "The stores, the walls, the quarrels. All mine.", "A town is a lot of people pulling different ways." };
			d["small.king.plain"] = new string[4] { "The crown is heavy.", "Every town of {realm} wants something from me.", "I have little time. The realm needs me.", "Ruling is mostly listening to complaints." };
			d["small.none.plain"] = new string[4] { "Not much work for me lately.", "Between jobs, you could say.", "I help where I'm needed.", "Just getting by." };
			d["small.home.plain"] = new string[8] { "{town} is a good place, mostly.", "Been in {town} all my life.", "{town} keeps growing. New faces all the time.", "Quiet town, {town}. I like it.", "{realm} has been good to us. Mostly.", "The roads out of {town} aren't safe at night.", "My family's been here since the town was a camp.", "Every year this place gets a bit bigger." };
			d["small.home.elf"] = new string[2] { "We planted these trees when {town} was young.", "Our people remember this land before the towns." };
			d["small.home.dwarf"] = new string[2] { "Good stone under {town}. That's why we stayed.", "Too much sky up here. I miss the halls." };
			d["small.home.orc"] = new string[2] { "{town} is strong. Strong is good.", "We took this land. We keep it." };
			d["small.self.plain"] = new string[6] { "Long day.", "Not much to say, really.", "Same as always.", "The days run together.", "Can't complain. Wouldn't help anyway.", "I keep to myself, mostly." };
			d["small.self.cruel"] = new string[3] { "People are easy to break, you know.", "I never miss a hanging.", "The weak never last." };
			d["small.self.hothead"] = new string[3] { "Someone shoved me in the market yesterday. They won't again.", "I've no patience for fools.", "One more insult and I swear, I'll swing." };
			d["small.self.sly"] = new string[3] { "Everyone has a secret. Most are cheap.", "I hear things. People forget I'm there.", "Never tell anyone everything." };
			d["small.self.greedy"] = new string[3] { "Gold's the only friend that never lies.", "I'm saving. For what is my business.", "Everything has a price. Everything." };
			d["small.self.wary"] = new string[3] { "Someone's been following me. I'm sure of it.", "Don't trust the guards. Any of them.", "I keep my door barred at night." };
			d["small.self.lustful"] = new string[3] { "Lonely nights here.", "You've a good look about you.", "Life's short. Enjoy it, I say." };
			d["small.self.ambitious"] = new string[3] { "One day they'll know my name across {realm}.", "I won't stay small forever.", "A seat on the council. That's what I want." };
			d["small.self.pious"] = new string[3] { "The gods see everything we do.", "I pray every night. You should too.", "There's a reason for all of it. Even the bad." };
			d["small.self.gentle"] = new string[3] { "If you ever need help, ask.", "Violence settles nothing worth settling.", "I just want everyone to get along." };
			d["small.self.honest"] = new string[3] { "I'll tell you straight: I don't trust easily.", "Say what you mean. That's all I ask.", "A lie costs more than it saves." };
			d["small.self.clever"] = new string[3] { "I've been watching how the stars move.", "There's a better way to do most things here. Nobody listens.", "Ask me anything. I like a puzzle." };
			d["small.self.wise"] = new string[3] { "Patience. Most things sort themselves.", "Young folk rush. The old wait.", "The river doesn't hurry, and it gets there." };
			d["small.self.dull"] = new string[3] { "Lost my way home twice this week.", "People talk too fast for me.", "I forget things. Don't mind me." };
			d["small.self.hardened"] = new string[3] { "I've buried more friends than I can count.", "Seen too many battles to like them.", "Sleep light. Old habit." };
			d["small.self.easy"] = new string[3] { "Life's good. I wouldn't change much.", "No sense worrying.", "A warm meal and a dry bed. What else is there?" };
			d["small.spring.plain"] = new string[3] { "Sowing time. Every hand's in the fields.", "Rivers are high with the thaw.", "Lambs are coming. Good sign." };
			d["small.summer.plain"] = new string[3] { "Long days. Good for work, bad for sleep.", "Too hot to be indoors.", "Crops are coming up well." };
			d["small.autumn.plain"] = new string[3] { "Harvest soon. Then the long wait.", "Everyone's stocking up for winter.", "The geese are leaving." };
			d["small.winter.plain"] = new string[3] { "Cold eats at you this time of year.", "We'll be burning wood all season.", "Winter's when you learn who your friends are." };
			d["small.rain.plain"] = new string[2] { "The rain's good for the crops, at least.", "Leaky roof again." };
			d["small.snow.plain"] = new string[2] { "Snow's piling up on the roads.", "The children love the snow. I don't." };
			d["small.storm.plain"] = new string[2] { "Lightning hit a tree near the well last night.", "Storms like this, I stay in." };
			d["small.child.plain"] = new string[8] { "Do you have a sword? Can I hold it?", "I found a frog by the river.", "When I grow up I'm going to be a soldier.", "My father says I talk too much.", "Have you seen a dragon? A real one?", "I can run faster than anyone.", "Where do you live?", "I'm not allowed past the fields." };
			d["small.elder.plain"] = new string[6] { "When I was young, this was all forest.", "I've seen three kings come and go.", "Nobody listens to the old anymore.", "My knees tell me rain's coming.", "I knew your sort, once. Long ago.", "The young think they invented trouble." };
			d["aside.plain"] = new string[8] { "That's how it is.", "Not that anyone asks.", "Anyway.", "So it goes.", "Make of that what you will.", "Same as ever.", "Well. There it is.", "Such is life." };
			d["aside.cruel"] = new string[2] { "Heh.", "Not that I care." };
			d["aside.hothead"] = new string[2] { "And I won't hear otherwise.", "Don't argue." };
			d["aside.sly"] = new string[3] { "Between us, of course.", "You didn't hear it from me.", "Don't repeat that." };
			d["aside.greedy"] = new string[2] { "Worth knowing, that. Free, this once.", "Information isn't cheap." };
			d["aside.wary"] = new string[2] { "*glances round*", "Keep it quiet." };
			d["aside.lustful"] = new string[2] { "*winks*", "Just saying." };
			d["aside.ambitious"] = new string[2] { "Mark my words.", "Things will change." };
			d["aside.pious"] = new string[2] { "The gods willing.", "Bless it." };
			d["aside.gentle"] = new string[2] { "I hope that helps.", "Be well." };
			d["aside.honest"] = new string[2] { "That's the truth of it.", "No use pretending otherwise." };
			d["aside.clever"] = new string[2] { "Curious, isn't it.", "I've thought about it." };
			d["aside.wise"] = new string[2] { "Everything passes.", "Time will tell." };
			d["aside.dull"] = new string[2] { "I think.", "Or something like that." };
			d["aside.hardened"] = new string[2] { "Trust me on that.", "Learned it the hard way." };
			d["aside.easy"] = new string[2] { "No harm in it.", "Ah, well." };
			d["aside.elder"] = new string[3] { "But what do I know.", "We had it harder, mind.", "Where was I? Ah." };
			d["aside.elf"] = new string[2] { "As the old songs say.", "Patience, as always." };
			d["aside.dwarf"] = new string[2] { "Mark it in stone.", "Hmph." };
			d["aside.orc"] = new string[2] { "That's it.", "Strong words." };
			d["turn.plain"] = new string[7] { "And you? What brings you here?", "What about you?", "Where are you headed?", "What's your trade, then?", "You've travelled, haven't you?", "Staying long?", "What do you make of it?" };
			d["turn.child"] = new string[5] { "Do you have children?", "Where are you going?", "Can you teach me to fight?", "Is there really a sea past the mountains?", "How old are you?" };
			d["turn.greedy"] = new string[1] { "Got any work that pays?" };
			d["turn.wary"] = new string[1] { "Why do you ask?" };
			d["turn.clever"] = new string[1] { "What do you think?" };
			d["turn.lustful"] = new string[1] { "Doing anything tonight?" };
			d["turn.hothead"] = new string[1] { "Got a problem with that?" };
			d["again.plain"] = new string[6] { "You asked me that already.", "Same answer as before.", "I just told you.", "Weren't you listening?", "We've been over this.", "Again?" };
			d["again.cruel"] = new string[1] { "Ask me again and see." };
			d["again.hothead"] = new string[1] { "I told you once!" };
			d["again.sly"] = new string[1] { "Testing me, are you?" };
			d["again.greedy"] = new string[1] { "The second time costs extra." };
			d["again.wary"] = new string[1] { "Why do you keep asking?" };
			d["again.lustful"] = new string[1] { "You just like hearing me talk." };
			d["again.ambitious"] = new string[1] { "I haven't time to repeat myself." };
			d["again.pious"] = new string[1] { "Patience. I'll say it once more." };
			d["again.gentle"] = new string[1] { "I don't mind saying it again." };
			d["again.honest"] = new string[1] { "Same as before. It hasn't changed." };
			d["again.clever"] = new string[1] { "You already know the answer." };
			d["again.wise"] = new string[1] { "You want to hear it twice. Fine." };
			d["again.dull"] = new string[1] { "Did you ask that? I forget." };
			d["again.hardened"] = new string[1] { "Already answered." };
			d["again.easy"] = new string[1] { "Sure, again." };
			d["again.child"] = new string[3] { "You asked that!", "I already said!", "*giggles* Again?" };
			d["again.elder"] = new string[3] { "Did I tell you this already? I tell everyone.", "Eh? Again? Well, all right.", "At my age I repeat myself. You've no excuse." };
			d["again.sovereign"] = new string[2] { "As I said, {title}.", "*bows* Once more, {title}." };
			d["back.plain"] = new string[14]
			{
				"What else?", "Something else, then?", "Go on.", "Mm?", "Anything else?", "What else is on your mind?", "Ask, then.", "I'm still here.", "Anything more?", "What next?",
				"Ask away.", "Go ahead.", "There's more?", "Out with it."
			};
			d["back.cruel"] = new string[1] { "More? Go on." };
			d["back.hothead"] = new string[1] { "What else? Quickly." };
			d["back.sly"] = new string[1] { "What else would you like to know?" };
			d["back.greedy"] = new string[1] { "Anything else? Cheaply?" };
			d["back.wary"] = new string[1] { "What else do you want?" };
			d["back.lustful"] = new string[1] { "Something else on your mind?" };
			d["back.ambitious"] = new string[1] { "Next." };
			d["back.pious"] = new string[1] { "What else troubles you?" };
			d["back.gentle"] = new string[1] { "Of course. What else?" };
			d["back.honest"] = new string[1] { "Ask whatever you like." };
			d["back.clever"] = new string[1] { "Another question? Good." };
			d["back.wise"] = new string[1] { "Take your time." };
			d["back.dull"] = new string[1] { "Uh. What else?" };
			d["back.hardened"] = new string[1] { "Next question." };
			d["back.easy"] = new string[1] { "Sure. What else?" };
			d["back.child"] = new string[6] { "What else? What else?", "Ask me something!", "Okay!", "And then what?", "Ask me another!", "More questions!" };
			d["back.sovereign"] = new string[3] { "Anything else, {title}?", "What else may I do for you?", "*waits*" };
		}
	}
}
