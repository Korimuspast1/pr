using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;
using db;

namespace FirstPerson
{
	internal static class Dialogue
	{
		private sealed class Opening
		{
			public string Id;

			public Func<Actor, Actor, bool> When;

			public Func<Actor, Actor, string> Theirs;

			public string[] Replies;

			public bool TellsHope;
		}

		private static readonly HashSet<string> Answers = new HashSet<string> { "family", "faith", "life", "news", "king", "realm", "petition", "court" };

		private static readonly HashSet<string> Remembered = new HashSet<string> { "family", "faith", "life", "news", "trade", "quest" };

		private static readonly string[] Gossipable = new string[14]
		{
			"death_lover", "death_child", "fallen_in_love", "just_had_child", "got_robbed", "become_king", "become_leader", "just_lost_house", "fp_named", "fp_adopted",
			"just_injured", "lost_fight", "wrote_book", "fp_joined"
		};

		private const string PetitionedKey = "fp_petitioned";

		private static readonly System.Random Rng = new System.Random();

		private static readonly Opening[] Openings = new Opening[14]
		{
			new Opening
			{
				Id = "morning",
				When = (Actor n, Actor y) => HourNow > 0.22f && HourNow < 0.4f,
				Theirs = (Actor n, Actor y) => Pick("Up early, same as me. The light's nice this time of day.", "*stretches* Morning. I'm barely awake."),
				Replies = new string[4] { "It's nicer with you in it.", "I got up early hoping to catch you.", "I'm only up because I heard you would be.", "Mornings feel like a second chance. Do you feel that too?" }
			},
			new Opening
			{
				Id = "evening",
				When = (Actor n, Actor y) => HourNow > 0.66f && HourNow < 0.85f,
				Theirs = (Actor n, Actor y) => Pick("The day's nearly done. I'm worn through.", "*watches the sun go down* Another day gone."),
				Replies = new string[4] { "Sit a moment. I'll keep you company.", "Then let me walk you home.", "Worn through and still the best sight in town.", "What wore you down today? Tell me." }
			},
			new Opening
			{
				Id = "night",
				When = (Actor n, Actor y) => HourNow >= 0.85f || HourNow < 0.2f,
				Theirs = (Actor n, Actor y) => Pick("What are you doing about at this hour?", "*pulls their cloak close* Couldn't sleep either?"),
				Replies = new string[4] { "I couldn't sleep. Then I saw you.", "Looking for you, if I'm honest.", "Counting stars. I lost count when you came out.", "The night's when I think most clearly. I keep thinking of you." }
			},
			new Opening
			{
				Id = "work",
				When = (Actor n, Actor y) => JobName(n) != null && JobName(n) != "king" && JobName(n) != "queen",
				Theirs = (Actor n, Actor y) => "Long day at " + WorkPlace(JobName(n)) + ". My back's paying for it.",
				Replies = new string[4] { "Let me carry something for you tomorrow.", "Your back looks fine from where I'm standing.", "I'd pay you to do nothing at all, just to keep you near.", "Do you love the work, or do you just do it?" }
			},
			new Opening
			{
				Id = "crown",
				When = (Actor n, Actor y) => JobName(n) == "king" || JobName(n) == "queen" || JobName(n) == "leader",
				Theirs = (Actor n, Actor y) => "Everyone wants something from me. Petitions, quarrels, grain counts.",
				Replies = new string[4] { "I don't want anything. Only to see you smile.", "I want something too. Your evening.", "I'll file a petition. For one walk, with you.", "Who looks after you, while you look after everyone?" }
			},
			new Opening
			{
				Id = "happy",
				When = (Actor n, Actor y) => SafeHappiness(n) > 40,
				Theirs = (Actor n, Actor y) => Pick("*humming* Sorry. It's been a good day.", "*grinning* Everything's going right today."),
				Replies = new string[4] { "Don't stop. It suits you.", "Let me make it a better one.", "Hum louder. I'll pretend I know the words.", "What made it good? I want to know what makes you happy." }
			},
			new Opening
			{
				Id = "sad",
				When = (Actor n, Actor y) => SafeHappiness(n) < -10,
				Theirs = (Actor n, Actor y) => Pick("*sighs* Not the best of days.", "*rubs their eyes* Don't mind me. It's nothing."),
				Replies = new string[4] { "Come here. Tell me what's wrong.", "Point me at whoever spoiled it.", "I could make a fool of myself. Would that help?", "Bad days pass. I'll stay until this one does." }
			},
			new Opening
			{
				Id = "town",
				When = (Actor n, Actor y) => CityName(n) != null,
				Theirs = (Actor n, Actor y) => CityName(n) + "'s grown since I was small. Hardly know it some days.",
				Replies = new string[4] { "It's lucky to have you in it.", "Stay. I'd hate this place without you.", "Still the prettiest thing in it, though.", "Do you ever want to leave? See what's past the hills?" }
			},
			new Opening
			{
				Id = "you",
				When = (Actor n, Actor y) => true,
				Theirs = (Actor n, Actor y) => Pick("So what brings you round here so often?", "*looks at you sideways* You keep finding me."),
				Replies = new string[4] { "I'm glad I do. Every time.", "You. I thought that was clear.", "You're not hard to find. I just follow the sunshine.", "I'm still working that out. I think it's you." }
			},
			new Opening
			{
				Id = "winter",
				When = (Actor n, Actor y) => SeasonNow == Season.Winter,
				Theirs = (Actor n, Actor y) => "This cold gets into everything.",
				Replies = new string[4] { "Here, take my cloak.", "Stand closer, then.", "I hear the cure is a warm drink and better company.", "Winter makes me want someone to come home to." }
			},
			new Opening
			{
				Id = "summer",
				When = (Actor n, Actor y) => SeasonNow == Season.Summer,
				Theirs = (Actor n, Actor y) => "Hot enough to bake bread on the stones.",
				Replies = new string[4] { "Let's find some shade together.", "Come and swim with me, then.", "And here I thought it was just you.", "Summers go fast. I'd like to spend this one well." }
			},
			new Opening
			{
				Id = "spring",
				When = (Actor n, Actor y) => SeasonNow == Season.Spring,
				Theirs = (Actor n, Actor y) => "Everything's in bloom. It makes me restless.",
				Replies = new string[4] { "I'll pick you the first flowers I find.", "Restless? Then come away with me for a day.", "Restless suits you. So does everything.", "Spring always feels like something's about to begin." }
			},
			new Opening
			{
				Id = "autumn",
				When = (Actor n, Actor y) => SeasonNow == Season.Autumn,
				Theirs = (Actor n, Actor y) => "The leaves are turning. I always feel a little sad this time of year.",
				Replies = new string[4] { "Then I'll be here to cheer you up.", "Let's make this one a good autumn. Together.", "I'll glue every leaf back on, if it helps.", "Things end so others can start. I'm hoping something's starting." }
			},
			new Opening
			{
				Id = "hope",
				When = (Actor n, Actor y) => true,
				TellsHope = true,
				Theirs = (Actor n, Actor y) => "*quietly* Can I tell you something? " + HopeLine(Romance.HopeOf(n), n),
				Replies = new string[4] { "I hope you get it. You deserve to.", "I'll help you get there. Starting now.", "Save me a seat when you do.", "That's the most honest thing anyone's said to me in a while." }
			}
		};

		private static readonly Dictionary<long, string> _lastOpening = new Dictionary<long, string>();

		internal static string OpeningForTest;

		private const int NamesPerPage = 8;

		private static int _flirtTurn;

		private static readonly int[] ReworkPrice = new int[5] { 0, 0, 30, 60, 120 };

		public const string WarGoldKey = "fp_war_gold";

		public const int WarBribeBase = 800;

		private const int WarBribeYears = 5;

		private static readonly System.Random PlotRng = new System.Random();

		private static readonly Dictionary<string, int> DialogCounts = new Dictionary<string, int>();

		private static readonly string[] Weighty = new string[8] { "death_lover", "death_child", "fallen_in_love", "just_had_child", "become_king", "death_family_member", "was_conquered", "just_lost_house" };

		private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

		private static readonly List<KeyValuePair<Vector2, string>> RecentLog = new List<KeyValuePair<Vector2, string>>();

		private static float _logReadAt = -100f;

		private static readonly Dictionary<long, int> SmallTalkTurn = new Dictionary<long, int>();

		private const int Bundle = 5;

		private static readonly string[] Wares = new string[18]
		{
			"bread", "wheat", "fish", "meat", "berries", "pie", "cider", "wood", "stone", "common_metals",
			"silver", "leather", "bones", "herbs", "honey", "sushi", "mythril", "adamantine"
		};

		private static Actor _sleptIn;

		private static int _sleptYear = -99;

		private static readonly System.Random BribeRng = new System.Random();

		private const string RefusedKey = "fp_bribe_no";

		private static readonly string[] Manners = new string[17]
		{
			"psychopath", "evil", "savage", "bloodlust", "hotheaded", "peaceful", "pacifist", "honest", "deceitful", "stupid",
			"genius", "wise", "greedy", "paranoid", "ambitious", "content", "lustful"
		};

		private static readonly Regex Emote = new Regex("\\*[^*]*\\*", RegexOptions.Compiled);

		private static readonly Regex Articles = new Regex("\\b(the|a|an|is|are|am|was|were|to|of|that|very|really)\\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

		private static readonly Regex Spaces = new Regex("\\s{2,}", RegexOptions.Compiled);

		private static float HourNow
		{
			get
			{
				try
				{
					return (WorldBoxMod.Instance != null && WorldBoxMod.Instance.Day != null) ? WorldBoxMod.Instance.Day.Hour : 0.5f;
				}
				catch
				{
					return 0.5f;
				}
			}
		}

		private static Season SeasonNow
		{
			get
			{
				try
				{
					return Calendar.Season;
				}
				catch
				{
					return Season.Summer;
				}
			}
		}

		internal static bool Voiced(Conversation c)
		{
			if (c == null || c.With == null || c.Slots == null)
			{
				return false;
			}
			try
			{
				return IsPerson(c.With) && !c.With.isBaby();
			}
			catch
			{
				return false;
			}
		}

		internal static string Beat(Conversation c, string beat)
		{
			if (!Voiced(c))
			{
				return null;
			}
			Regard regard = Chronicle.RegardOf(c.With, c.You, create: false);
			ulong seed = Voices.Seed(c.Persona.Id, regard?.Talks ?? 0, c.Beats++, beat);
			ushort id;
			string text = Voices.Line(beat, in c.Persona, in c.Scene, c.Slots, seed, c.Memory?.Recent, out id);
			if (text != null && beat != "aside" && beat != "turn" && !beat.StartsWith("bye.", StringComparison.Ordinal))
			{
				Mind.Heard(c.Memory, id);
			}
			return text;
		}

		private static bool Chance(Conversation c, string what, float p)
		{
			Regard regard = Chronicle.RegardOf(c.With, c.You, create: false);
			ulong s = Voices.Seed(c.Persona.Id, regard?.Talks ?? 0, 7919, what);
			return Voices.Unit(ref s) < (double)p;
		}

		private static void SayBeats(Conversation c, List<string> beats)
		{
			List<string> list = new List<string>(2);
			Voices.Pack(beats, list);
			c.Say(list.ToArray());
			Mind.Save(c.With, c.Memory);
		}

		private static void GreetBeats(Conversation c)
		{
			Actor with = c.With;
			Actor you = c.You;
			List<string> list = new List<string>(3) { Beat(c, "open") ?? Greeting(with, you) };
			string text = null;
			Memory memory = RecentGrief(with);
			text = ((memory != null && !c.Voice.Forgetful) ? GriefLine(memory, Models.IsFemale(with)) : (Births.IsExpecting(with) ? Births.ExpectingGreeting(with, you) : ((with.lover == you && Births.IsExpecting(you)) ? Births.PartnerGreeting(you) : ((c.Welcome == null) ? Talk.Greeting(c) : c.Welcome))));
			if (text != null)
			{
				list.Add(text);
			}
			else
			{
				string text2 = Callback(c);
				if (text2 != null)
				{
					list.Add(text2);
				}
				string text3 = ((text2 == null || Chance(c, "hook after callback", 0.35f)) ? Hook(c) : null);
				if (text3 != null)
				{
					list.Add(text3);
				}
			}
			SayBeats(c, list);
			c.Offer(Topics(c));
		}

		private static string Callback(Conversation c)
		{
			if (c.Voice.Forgetful)
			{
				return null;
			}
			Actor with = c.With;
			Actor you = c.You;
			Regard regard = Chronicle.RegardOf(with, you, create: false);
			Mind.Rec memory = c.Memory;
			int today = Mind.Today;
			if (regard != null && regard.Crimes > 0 && regard.Opinion < 20 && Chance(c, "call.crime", 0.7f))
			{
				return Beat(c, "call.crime");
			}
			if (memory != null && memory.PrevDay >= 0)
			{
				if (memory.PrevDay == today && Chance(c, "call.sameday", 0.6f))
				{
					return Beat(c, "call.sameday");
				}
				if (today - memory.PrevDay >= 4 && Chance(c, "call.longago", 0.8f))
				{
					return Beat(c, "call.longago");
				}
			}
			if (regard != null && regard.Quests > 0 && Chance(c, "call.quest", 0.45f))
			{
				c.Slots.Thing = QuestThing(with);
				return Beat(c, "call.quest");
			}
			if (regard != null && regard.Gifts > 0 && Chance(c, "call.gift", 0.3f))
			{
				return Beat(c, "call.gift");
			}
			if (memory != null && memory.PrevTopic != null && Chance(c, "call.topic", 0.3f))
			{
				string text = "call.topic_" + memory.PrevTopic;
				if (Voices.HasPool(text + ".plain"))
				{
					return Beat(c, text);
				}
			}
			return null;
		}

		private static string QuestThing(Actor npc)
		{
			Record record = Chronicle.Of(npc);
			if (record == null)
			{
				return null;
			}
			for (int num = record.Memories.Count - 1; num >= 0; num--)
			{
				Memory memory = record.Memories[num];
				if (!(memory.Kind != "fp_quest") && !string.IsNullOrEmpty(memory.Other))
				{
					if (!memory.Other.StartsWith("the ", StringComparison.Ordinal) && !memory.Other.StartsWith("our ", StringComparison.Ordinal))
					{
						return null;
					}
					return memory.Other;
				}
			}
			return null;
		}

		private static string Hook(Conversation c)
		{
			Voices.Plight plight = c.Persona.Plight;
			if ((plight & Voices.Plight.Sick) != Voices.Plight.None && Chance(c, "hook.sick", 0.8f))
			{
				return Beat(c, "hook.sick");
			}
			if ((plight & Voices.Plight.Hurt) != Voices.Plight.None && Chance(c, "hook.hurt", 0.7f))
			{
				return Beat(c, "hook.hurt");
			}
			if ((plight & Voices.Plight.Hungry) != Voices.Plight.None && Chance(c, "hook.hungry", 0.7f))
			{
				return Beat(c, "hook.hungry");
			}
			if ((plight & Voices.Plight.InLove) != Voices.Plight.None && Chance(c, "hook.inlove", 0.4f))
			{
				return Beat(c, "hook.inlove");
			}
			if ((plight & Voices.Plight.Rich) != Voices.Plight.None && Chance(c, "hook.rich", 0.25f))
			{
				return Beat(c, "hook.rich");
			}
			if ((plight & Voices.Plight.Poor) != Voices.Plight.None && Chance(c, "hook.poor", 0.25f))
			{
				return Beat(c, "hook.poor");
			}
			return null;
		}

		private static void SmallTalkBeats(Conversation c, int turn)
		{
			Voices.Persona persona = c.Persona;
			string text = null;
			if (persona.Child)
			{
				text = Beat(c, "small.child");
			}
			else
			{
				if (persona.Age == Voices.AgeBand.Elder && Chance(c, "elder rambles " + turn, 0.4f))
				{
					text = Beat(c, "small.elder");
				}
				int num = 0;
				while (text == null && num < 4)
				{
					switch ((turn + num) % 4)
					{
					case 0:
						if (persona.Job != null)
						{
							text = Beat(c, "small." + persona.Job);
						}
						break;
					case 1:
						if (c.Slots.Town != null)
						{
							text = Beat(c, "small.home");
						}
						break;
					case 2:
						text = Beat(c, "small.self");
						break;
					default:
						if (c.Scene.Sky != Voices.Sky.Clear)
						{
							text = Beat(c, "small." + SkyWord(c.Scene.Sky));
						}
						else if (c.Scene.Season >= 0)
						{
							text = Beat(c, "small." + SeasonWord(c.Scene.Season));
						}
						break;
					}
					num++;
				}
			}
			List<string> list = new List<string>(3) { text ?? AboutTraits(c.With) };
			if (!persona.Child && Chance(c, "aside " + turn, 0.4f))
			{
				string text2 = Beat(c, "aside");
				if (text2 != null)
				{
					list.Add(text2);
				}
			}
			if (Chance(c, "turn " + turn, persona.Child ? 0.6f : 0.25f))
			{
				string text3 = Beat(c, "turn");
				if (text3 != null)
				{
					list.Add(text3);
				}
			}
			SayBeats(c, list);
		}

		private static string SkyWord(Voices.Sky s)
		{
			switch (s)
			{
			default:
				return "storm";
			case Voices.Sky.Snow:
				return "snow";
			case Voices.Sky.Rain:
				return "rain";
			}
		}

		private static string SeasonWord(int s)
		{
			switch (s)
			{
			default:
				return "winter";
			case 2:
				return "autumn";
			case 1:
				return "summer";
			case 0:
				return "spring";
			}
		}

		private static void FarewellBeats(Conversation c, string topic)
		{
			List<string> list = new List<string>(2) { Beat(c, "part") ?? Farewell(c.With, c.You) };
			string text = ByeTopic(topic);
			if (text != null && Chance(c, "bye", 0.4f))
			{
				string text2 = Beat(c, text);
				if (text2 != null)
				{
					list.Add(text2);
				}
			}
			SayBeats(c, list);
		}

		private static string ByeTopic(string topic)
		{
			if (string.IsNullOrEmpty(topic))
			{
				return null;
			}
			if (topic.StartsWith("trade", StringComparison.Ordinal))
			{
				return "bye.trade";
			}
			switch (topic)
			{
			case "quest":
			case "family":
			case "news":
			case "faith":
			case "gift":
			case "smalltalk":
				return "bye." + topic;
			default:
				return null;
			}
		}

		private static string WhatElse(Conversation c)
		{
			return Beat(c, "back") ?? Pick("What else?", "Something else, then?", "Go on.", "Mm?");
		}

		private static string Thanks(Conversation c)
		{
			return Beat(c, "thanks");
		}

		internal static void AfterTopic(Conversation c, string topic, bool again)
		{
			if (!Voiced(c) || string.IsNullOrEmpty(topic))
			{
				return;
			}
			if (again && Answers.Contains(topic) && c.Pages.Count > 0 && c.YoursUpTo == 0)
			{
				string text = Beat(c, "again");
				if (text != null)
				{
					c.Pages[0] = Speak(c.Voice, text) + "\n" + c.Pages[0];
				}
			}
			string text2 = (topic.StartsWith("trade", StringComparison.Ordinal) ? "trade" : topic);
			if (Remembered.Contains(text2))
			{
				Mind.Topic(c.With, text2);
			}
			else
			{
				Mind.Save(c.With, c.Memory);
			}
		}

		internal static void Refeel(Conversation c)
		{
			if (c?.Slots != null)
			{
				c.Persona.Warmth = c.Voice.Warmth;
				c.Persona.Bond = Bonds.Of(c.With, c.You, c.Persona.Tie);
				c.MoodWord = Voices.MoodWord(in c.Persona);
				c.CardSuffix = CardSuffix(c);
			}
		}

		internal static string CardSuffix(Conversation c)
		{
			if (c?.Slots == null)
			{
				return null;
			}
			Voices.Tie tie = c.Persona.Tie;
			string text = (((int)c.Persona.Bond >= 1 && (tie == Voices.Tie.None || tie == Voices.Tie.Foreign)) ? Bonds.Word(c.Persona.Bond) : null);
			string moodWord = c.MoodWord;
			if (text == null && moodWord == null)
			{
				return null;
			}
			return ((text != null) ? ("  ·  " + text) : "") + ((moodWord != null) ? (" (" + moodWord + ")") : "");
		}

		internal static Color MoodTint(Conversation c)
		{
			if (c?.Slots == null)
			{
				return Color.clear;
			}
			if ((c.Persona.Plight & Voices.Plight.Bereaved) != Voices.Plight.None)
			{
				return new Color(0.42f, 0.47f, 0.58f, 0.22f);
			}
			switch (c.Persona.Mood)
			{
			case Voices.Mood.Happy:
				return new Color(1f, 0.82f, 0.45f, 0.14f);
			case Voices.Mood.Sad:
				return new Color(0.45f, 0.55f, 0.85f, 0.16f);
			case Voices.Mood.Angry:
				return new Color(1f, 0.3f, 0.25f, 0.16f);
			case Voices.Mood.Dark:
				return new Color(0.35f, 0.2f, 0.45f, 0.24f);
			default:
				return Color.clear;
			}
		}

		private static void BelongChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			City city = null;
			City theirs = null;
			try
			{
				city = you.city;
				theirs = with.city;
			}
			catch
			{
			}
			if (theirs == null || !theirs.isAlive())
			{
				return;
			}
			if (city == theirs)
			{
				list.Add(new Choice("I'm leaving " + theirs.name + " for good.", delegate
				{
					LeaveAsk(c);
				}, ChoiceTone.Risk));
			}
			else if (CanTakeYouIn(with, theirs))
			{
				string label = ((city != null && city.kingdom == theirs.kingdom) ? ("May I move to " + theirs.name + "?") : ("May I live in " + theirs.name + "?"));
				list.Add(new Choice(label, delegate
				{
					JoinAsk(c, theirs);
				}));
			}
		}

		private static bool CanTakeYouIn(Actor npc, City city)
		{
			try
			{
				if (npc.isBaby())
				{
					return false;
				}
				if (npc.isCityLeader())
				{
					return true;
				}
				if (npc.isKing() && npc.kingdom == city.kingdom)
				{
					return true;
				}
				return !city.hasLeader() && npc.isAdult();
			}
			catch
			{
				return false;
			}
		}

		private static void LeaveAsk(Conversation c)
		{
			c.LastTopic = "leave";
			Actor you = c.You;
			City city = you.city;
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = you.isKing();
				flag2 = you.isCityLeader();
			}
			catch
			{
			}
			string text = (flag ? (" You would give up the crown of " + you.kingdom.name + ".") : (flag2 ? (" You would give up the lead of " + city.name + ".") : ""));
			c.Say(Pick("Leave " + city.name + "? Where would you go?", "You'd leave us? Just like that?", "*frowns* " + city.name + " has been your home.") + ((text.Length > 0) ? ("\n" + text.Trim()) : ""));
			c.Offer(new List<Choice>
			{
				new Choice("Yes. I'm going.", delegate
				{
					LeaveNow(c);
				}, ChoiceTone.Risk),
				new Choice("No, I'll stay.", delegate
				{
					c.Say(Pick("Good. We'd miss you.", "*smiles* I thought so."));
					c.Offer(Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void LeaveNow(Conversation c)
		{
			Actor you = c.You;
			Actor with = c.With;
			string text = ((you.city != null) ? you.city.name : "your town");
			if (!Belong.Leave(you))
			{
				c.Say("*shakes head* Something holds you here yet.");
				c.Offer(Topics(c));
				return;
			}
			Chronicle.ChangeOpinion(with, you, -5);
			c.Relation = RelationTo(with, you);
			c.Say(Pick("Then go well. Don't forget us.", "*sighs* The road's long. Take care on it.", "Go, then. The door won't stay open forever."));
			c.Toast?.Invoke("You left " + text + ". You belong to no town now.");
			c.Offer(new List<Choice> { Goodbye(c) });
		}

		private static void JoinAsk(Conversation c, City city)
		{
			c.LastTopic = "join";
			Actor you = c.You;
			Actor with = c.With;
			string text = Belong.Refusal(with, you, city);
			if (text != null)
			{
				Log.Info("belong: " + NameOf(with) + " won't take you into " + city.name + ": " + text);
				c.Say(text);
				c.Offer(Topics(c));
				return;
			}
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = you.isKing() && you.kingdom != city.kingdom;
				flag2 = you.isCityLeader() && you.city != null;
			}
			catch
			{
			}
			string text2 = (flag ? ("\nYou would give up the crown of " + you.kingdom.name + ".") : (flag2 ? ("\nYou would give up the lead of " + you.city.name + ".") : ""));
			c.Say(Pick(city.name + " could use another pair of hands. You'd be welcome.", "*looks you over* Very well. There's room in " + city.name + ".", "Work hard, keep the peace, and you can stay.") + text2);
			c.Offer(new List<Choice>
			{
				new Choice("Thank you. " + city.name + " is my home now.", delegate
				{
					JoinNow(c, city);
				}, ChoiceTone.Quest),
				new Choice("Let me think about it.", delegate
				{
					c.Say(Pick("Take your time.", "The offer stands."));
					c.Offer(Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void JoinNow(Conversation c, City city)
		{
			Actor you = c.You;
			Actor with = c.With;
			if (!Belong.Join(you, city))
			{
				c.Say("*hesitates* Not today, I think.");
				c.Offer(Topics(c));
				return;
			}
			Chronicle.ChangeOpinion(with, you, 5);
			c.Relation = RelationTo(with, you);
			c.Say(Pick("Welcome to " + city.name + ".", "Welcome. Find yourself a roof; " + city.name + " looks after its own."));
			string text = ((city.kingdom != null) ? (" of " + city.kingdom.name) : "");
			c.Toast?.Invoke("You joined " + city.name + text + ".");
			c.Offer(Topics(c));
		}

		public static string Gossip(Actor npc, Actor you)
		{
			try
			{
				City city = npc.city;
				if (city == null)
				{
					return null;
				}
				int year = Chronicle.Year;
				List<string> list = new List<string>();
				foreach (Actor item in Civil.PeopleOf(city))
				{
					if (item == null || item == npc || item == you || !item.isAlive())
					{
						continue;
					}
					Record record = Chronicle.Cached(item.data.id);
					if (record == null)
					{
						continue;
					}
					for (int num = record.Memories.Count - 1; num >= 0; num--)
					{
						Memory memory = record.Memories[num];
						if (year - memory.Year <= 2 && Array.IndexOf(Gossipable, memory.Kind) >= 0)
						{
							string text = GossipAbout(NameOf(item), Models.IsFemale(item), memory);
							if (text != null)
							{
								list.Add(text);
							}
							break;
						}
					}
					if (list.Count >= 6)
					{
						break;
					}
				}
				if (list.Count == 0)
				{
					return null;
				}
				try
				{
					npc.changeHappiness("just_talked_gossip");
				}
				catch
				{
				}
				return list[Rng.Next(list.Count)];
			}
			catch
			{
				return null;
			}
		}

		private static string GossipAbout(string name, bool female, Memory m)
		{
			string text = (female ? "her" : "his");
			string other = m.Other;
			switch (m.Kind)
			{
			case "death_lover":
				if (other == null)
				{
					return name + " lost " + text + " love, you know.";
				}
				return Pick("Did you hear? " + name + " lost " + other + ". Poor thing.", name + " hasn't been the same since " + other + " died.");
			case "death_child":
				return name + " buried a child" + ((other != null) ? (", little " + other) : "") + ". Don't mention it.";
			case "fallen_in_love":
				if (other == null)
				{
					return name + " is in love. It's written all over " + text + " face.";
				}
				return Pick(name + " and " + other + "! Can you believe it?", "Have you seen " + name + " with " + other + "? Smitten, the pair of them.");
			case "just_had_child":
				if (other == null)
				{
					return name + " just had a baby!";
				}
				return name + " has a new baby. " + other + ", they call it.";
			case "fp_named":
				if (other == null)
				{
					return null;
				}
				return name + " named the baby " + other + ". Sweet, isn't it?";
			case "fp_adopted":
				if (other == null)
				{
					return null;
				}
				return name + " took in the orphan " + other + ". Kind soul.";
			case "got_robbed":
				return Pick("Someone robbed " + name + "! In broad daylight!", "Keep an eye on your purse. " + name + " was robbed.");
			case "become_king":
				return name + " wears the crown now. Who'd have thought?";
			case "become_leader":
				return name + " leads the town now. We'll see how that goes.";
			case "just_lost_house":
				return name + " lost " + text + " home. Sleeping rough, I hear.";
			case "lost_fight":
			case "just_injured":
				return name + " got a beating. Limping all over town.";
			case "wrote_book":
				return name + " wrote a book! Fancy that.";
			case "fp_joined":
				return Pick(name + " is new in town. Came from who knows where.", "Have you met " + name + "? Only just settled here.");
			default:
				return null;
			}
		}

		public static bool YouRule(Actor you, Actor npc)
		{
			try
			{
				if (npc == you || npc.kingdom == null || npc.kingdom != you.kingdom)
				{
					return false;
				}
				if (npc.isKing())
				{
					return false;
				}
				return you.isKing() || (you.isCityLeader() && npc.city != null && npc.city == you.city);
			}
			catch
			{
				return false;
			}
		}

		private static bool HeardThisYear(Actor npc)
		{
			try
			{
				npc.data.get("fp_petitioned", out var pResult, -1);
				return pResult == Chronicle.Year;
			}
			catch
			{
				return false;
			}
		}

		private static void MarkHeard(Actor npc)
		{
			try
			{
				npc.data.set("fp_petitioned", Chronicle.Year);
			}
			catch
			{
			}
		}

		internal static bool Petitioned(Actor npc)
		{
			return HeardThisYear(npc);
		}

		internal static void Petition(Conversation c)
		{
			c.LastTopic = "petition";
			Actor npc = c.With;
			Actor you = c.You;
			string title = ((!Models.IsFemale(you)) ? (you.isKing() ? "Your Majesty" : "My lord") : (you.isKing() ? "Your Majesty" : "My lady"));
			if (HeardThisYear(npc))
			{
				c.Say(title + ", you've already heard me this year. Thank you.");
				c.Offer(Topics(c));
				return;
			}
			City city = npc.city;
			Action granted = null;
			int num = ((city == null) ? 1 : Mathf.Max(1, city.getPopulationPeople()));
			int num2 = 0;
			try
			{
				string[] array = new string[5] { "berries", "wheat", "fish", "meat", "bread" };
				foreach (string pResourceID in array)
				{
					num2 += city.getResourcesAmount(pResourceID);
				}
			}
			catch
			{
			}
			bool flag = false;
			bool flag2 = false;
			Record record = Chronicle.Of(npc);
			if (record != null)
			{
				foreach (Memory memory in record.Memories)
				{
					if (memory.Kind == "got_robbed" && Chronicle.Year - memory.Year <= 1)
					{
						flag = true;
					}
				}
			}
			try
			{
				foreach (War war in npc.kingdom.getWars())
				{
					flag2 = war != null;
					if (flag2)
					{
						break;
					}
				}
			}
			catch
			{
			}
			string text;
			string label;
			int cost;
			int favour;
			int opinion;
			if (city != null && num2 < num * 2)
			{
				text = title + ", the stores of " + city.name + " are nearly bare. Could the crown send grain?";
				label = "Send grain from the royal stores (30 gold)";
				cost = 30;
				favour = 6;
				opinion = 18;
				granted = delegate
				{
					try
					{
						city.addResourcesToRandomStockpile("bread", 10);
						city.addResourcesToRandomStockpile("wheat", 10);
					}
					catch
					{
					}
				};
			}
			else if (flag)
			{
				text = title + ", I was robbed and the thief walks free. I've nothing left.";
				label = "Pay them back from the treasury (15 gold)";
				cost = 15;
				favour = 2;
				opinion = 22;
				granted = delegate
				{
					try
					{
						npc.addMoney(15);
					}
					catch
					{
					}
				};
			}
			else if (flag2)
			{
				text = title + ", this war takes our sons. The widows need bread.";
				label = "A pension for the widows (25 gold)";
				cost = 25;
				favour = 5;
				opinion = 14;
			}
			else
			{
				text = Pick(title + ", a feast! The people want a feast to lift their hearts.", title + ", the road to the fields is mud. Pave it?");
				label = "Grant it (10 gold)";
				cost = 10;
				favour = 3;
				opinion = 10;
			}
			c.Say(text);
			int num3 = 0;
			try
			{
				num3 = you.money;
			}
			catch
			{
			}
			Choice choice = new Choice(label, delegate
			{
				int num4 = 0;
				try
				{
					num4 = you.money;
				}
				catch
				{
				}
				if (num4 < cost)
				{
					c.Say(Pick("*glances at your purse* Another time, " + title + ".", "The purse is lighter than it looked."));
					c.Offer(Topics(c));
				}
				else
				{
					MarkHeard(npc);
					try
					{
						you.spendMoney(cost);
					}
					catch
					{
					}
					granted?.Invoke();
					if (city != null)
					{
						Law.LoyaltyBoost(city, favour);
					}
					Chronicle.ChangeOpinion(npc, you, opinion);
					try
					{
						npc.changeHappiness("just_received_gift");
					}
					catch
					{
					}
					c.Voice = VoiceOf(npc, you);
					c.Say(Pick("Bless you, " + title + "! The whole town will hear of this.", "*bows deeply* Thank you, " + title + "."));
					c.Toast?.Invoke((city != null) ? $"{city.name} is grateful (+{favour} loyalty)" : "Granted");
					c.Offer(Topics(c));
				}
			}, ChoiceTone.Quest);
			if (num3 < cost)
			{
				choice.Off($"You have only {num3} gold.");
			}
			Choice item = new Choice("Refuse", delegate
			{
				MarkHeard(npc);
				if (city != null)
				{
					Law.LoyaltyBoost(city, -Mathf.Max(2, favour / 2));
				}
				Chronicle.ChangeOpinion(npc, you, -opinion / 2);
				c.Voice = VoiceOf(npc, you);
				c.Say(Pick("I... see. As you wish, " + title + ".", "*bows stiffly* Of course."));
				c.Offer(Topics(c));
			}, ChoiceTone.Risk);
			c.Offer(new List<Choice>
			{
				choice,
				item,
				Back(c)
			});
		}

		internal static string Pick(params string[] options)
		{
			return options[Rng.Next(options.Length)];
		}

		public static string NameOf(Actor a)
		{
			string text = null;
			try
			{
				text = a.getName();
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
			if (a.asset == null)
			{
				return "someone";
			}
			return a.asset.id.Replace('_', ' ');
		}

		public static bool IsPerson(Actor a)
		{
			try
			{
				return a.isSapient();
			}
			catch
			{
				return false;
			}
		}

		public static bool CanFlirt(Actor a, Actor b)
		{
			if (!SameTribe(a, b))
			{
				return false;
			}
			try
			{
				return a.isAdult() && b.isAdult();
			}
			catch
			{
				return false;
			}
		}

		public static bool SameTribe(Actor a, Actor b)
		{
			if (a == null || b == null || !IsPerson(a) || !IsPerson(b))
			{
				return false;
			}
			try
			{
				if (a.isKingdomCiv() && b.isKingdomCiv())
				{
					return a.kingdom == b.kingdom;
				}
				City city = a.getCity();
				City city2 = b.getCity();
				return city != null && city == city2;
			}
			catch
			{
				return false;
			}
		}

		public static bool Enemies(Actor a, Actor b)
		{
			try
			{
				return a.kingdom != null && b.kingdom != null && a.kingdom != b.kingdom && a.kingdom.isEnemy(b.kingdom);
			}
			catch
			{
				return false;
			}
		}

		public static string Line(Actor npc, Actor you, int turn)
		{
			if (!IsPerson(npc))
			{
				return AnimalSound(npc);
			}
			try
			{
				if (npc.isBaby())
				{
					return Pick("Goo goo!", "*giggles*", "Ba-ba!", "*stares at you with big eyes*");
				}
			}
			catch
			{
			}
			switch (turn)
			{
			case 0:
				return Greeting(npc, you);
			case 1:
				return AboutWork(npc);
			case 2:
				return AboutHome(npc, you);
			case 3:
				return AboutTraits(npc);
			default:
				return Farewell(npc, you);
			}
		}

		internal static string Greeting(Actor npc, Actor you)
		{
			string text = NameOf(you);
			if (npc.lover == you)
			{
				return Pick("There you are, " + text + "! I missed you.", text + "! My favorite person.", "Oh, it's you... hi.");
			}
			if (npc.family != null && npc.family == you.family)
			{
				return Pick("Hey " + text + ". How's the family?", "Oh, " + text + ". Mother was asking about you.", "Family! Come here.");
			}
			if (Sovereign(npc, you))
			{
				return RoyalGreeting(npc, you);
			}
			if (Enemies(npc, you))
			{
				return Pick("You've got some nerve walking in here.", "One more step and I call the guards.", (KingdomName(you) != null) ? ("Get lost, " + KingdomName(you) + " scum.") : "Get lost, drifter.");
			}
			int num = SafeHappiness(npc);
			if (SameTribe(npc, you))
			{
				if (num > 40)
				{
					return Pick("Good day, " + text + "! Lovely weather, isn't it?", text + "! Always good to see you.", "Hello friend! What a day!");
				}
				if (num < -20)
				{
					return Pick("What do you want?", "*sighs* Not now...", "Leave me alone, I'm not in the mood.");
				}
				return Pick("Hello, " + text + ".", "Hey there.", "Oh, hi. Need something?");
			}
			return Pick("Hm? I don't know you, traveler.", "A stranger... what brings you here?", "Keep your distance, outsider.");
		}

		internal static string AboutWork(Actor npc)
		{
			try
			{
				switch (npc.getProfession())
				{
				case UnitProfession.King:
					return Pick("As ruler of " + KingdomName(npc) + ", I have little time. Speak quickly.", "The crown is heavy, friend.", "My kingdom needs me. What is it?");
				case UnitProfession.Leader:
					return Pick("Keeping " + CityName(npc) + " in order is no small task.", "Everyone comes to me with their problems.", "The village depends on me.");
				case UnitProfession.Warrior:
					return Pick("My blade is sworn to " + KingdomName(npc) + ".", "I've been training all day.", "Seen any enemies around? I'm itching for a fight.");
				}
			}
			catch
			{
			}
			string text = ((npc.citizen_job != null) ? npc.citizen_job.id : null);
			if (!string.IsNullOrEmpty(text))
			{
				if (text.Contains("farm"))
				{
					return Pick("The crops need tending again.", "Nothing beats fresh wheat.", "My back hurts from all that farming.");
				}
				if (text.Contains("wood") || text.Contains("lumber"))
				{
					return Pick("Chopped a whole forest today. Well, almost.", "Nice trees around here.", "Wood doesn't cut itself!");
				}
				if (text.Contains("mine") || text.Contains("miner"))
				{
					return Pick("Found a shiny rock earlier.", "The mines are dark and cold.", "Stone, stone, and more stone.");
				}
				if (text.Contains("build"))
				{
					return Pick("We're putting up a new house soon.", "Hand me that hammer, would you?", "This town keeps growing!");
				}
				if (text.Contains("hunt"))
				{
					return Pick("The deer are fast this season.", "Nothing like the thrill of the hunt.", "Fresh meat tonight!");
				}
				if (text.Contains("fish"))
				{
					return Pick("The fish are biting today.", "I love the smell of the sea.", "Caught one THIS big.");
				}
				if (text.Contains("gather"))
				{
					return Pick("Berries everywhere around here.", "Just gathering supplies.", "The storehouse is almost full.");
				}
				if (text.Contains("smith"))
				{
					return Pick("Need a new blade? I make the best.", "The forge is hot today.", "Iron and fire, my two loves.");
				}
				return Pick("I work as a " + text.Replace('_', ' ') + ".", "Work, work, work...", "Busy day today.");
			}
			return Pick("Just wandering, really.", "Not much to do today.", "I'm between jobs right now.");
		}

		internal static string AboutHome(Actor npc, Actor you)
		{
			int num = 0;
			try
			{
				num = npc.getAge();
			}
			catch
			{
			}
			List<string> list = new List<string>();
			string text = CityName(npc);
			if (text != null)
			{
				list.Add(text + " is the best place in the world, if you ask me.");
			}
			if (text != null)
			{
				list.Add("Have you been around " + text + " long?");
			}
			if (num > 0)
			{
				list.Add((num > 60) ? $"I've seen {num} years. The world has changed so much." : $"I'm only {num}, I've got my whole life ahead of me!");
			}
			string text2 = KingdomName(npc);
			if (text2 != null)
			{
				list.Add("Long live " + text2 + "!");
			}
			if (npc.lover != null && npc.lover != you)
			{
				list.Add("I should get back to " + NameOf(npc.lover) + " soon.");
			}
			list.Add("They say there are dragons in the far lands.");
			list.Add("Did you feel the earth shake the other day? Strange times.");
			return list[Rng.Next(list.Count)];
		}

		internal static string AboutTraits(Actor npc)
		{
			try
			{
				foreach (ActorTrait trait in npc.getTraits())
				{
					if (trait != null && Rng.Next(2) != 0)
					{
						switch (trait.id)
						{
						case "greedy":
							return "Got any gold? Asking for a friend.";
						case "peaceful":
							return "If you ever need help, just ask.";
						case "honest":
							return "I'll always tell you the truth, even when it stings.";
						case "deceitful":
							return "Trust me. Really. Would I lie to you?";
						case "ambitious":
							return "One day they'll know my name across the whole land.";
						case "hotheaded":
							return "Don't push me today. I mean it.";
						case "pacifist":
							return "Violence never solved anything worth solving.";
						case "psychopath":
							return "*smiles a little too long* People are so interesting.";
						case "savage":
							return "Soft town folk. Hah.";
						case "veteran":
							return "I've seen more battles than I care to count.";
						case "dragonslayer":
							return "Ever smelled a dragon's breath up close? I have. Once.";
						case "kingslayer":
							return "Crowns come off easier than you'd think.";
						case "eyepatch":
							return "The eye? Long story. You should see the other one.";
						case "crippled":
							return "My leg's not what it was, but I get by.";
						case "golden_tooth":
							return "*flashes a gold tooth* Worth every coin.";
						case "long_liver":
							return "My family lives long. Very long.";
						case "blessed":
							return "I've always felt someone up there likes me.";
						case "unlucky":
							return "If it can go wrong for me, it will.";
						case "thief":
							return "*pats your shoulder* Nice belt you've got there.";
						case "fat":
							return "I never say no to a second helping.";
						case "eagle_eyed":
							return "I can spot a hawk before it spots me.";
						case "strong":
							return "I once carried a cow. On each shoulder.";
						case "fast":
							return "Race you to the river!";
						case "wise":
							return "Patience is the root of all good things.";
						case "genius":
							return "I've been thinking about how the stars move...";
						case "stupid":
							return "Why is water wet? Nobody knows.";
						case "ugly":
							return "Stop staring. I know.";
						case "attractive":
							return "Like what you see?";
						case "lustful":
							return "You're looking nice today...";
						case "brave":
							return "I fear nothing! Except maybe bees.";
						case "coward":
							return "D-did you hear that noise?!";
						case "evil":
							return "Heh. You'd be surprised what I've done.";
						case "bloodlust":
							return "I'm itching for a good fight.";
						case "lucky":
							return "Found a four-leaf clover this morning!";
						case "content":
							return "Life is good. I wouldn't change a thing.";
						case "paranoid":
							return "Someone's been following me. I'm sure of it.";
						}
					}
				}
			}
			catch
			{
			}
			return Pick("I like long walks in the forest.", "My favorite thing is a warm meal after a long day.", "Sometimes I wonder who watches over us all.", "Don't tell anyone, but I'm scared of the dark.");
		}

		internal static string RoyalGreeting(Actor npc, Actor you)
		{
			string s = Majesty(you);
			string text = (Models.IsFemale(you) ? "queen" : "king");
			bool flag = false;
			try
			{
				flag = !npc.isAdult();
			}
			catch
			{
			}
			if (flag)
			{
				return Pick("*bows clumsily* " + Cap(s) + "!", "*stares* You're the " + text + "!", "*hides behind a hand, then bows*");
			}
			if (SafeHappiness(npc) < -20)
			{
				return Pick("*bows stiffly* " + Cap(s) + ".", "*lowers their eyes* " + Cap(s) + ". What do you require?");
			}
			return Pick("*bows* " + Cap(s) + ".", "*bows low* " + Cap(s) + ". You honour us.", Cap(s) + "! How may I serve?", "*drops to one knee* " + Cap(s) + ".");
		}

		internal static string Farewell(Actor npc, Actor you)
		{
			if (!Sovereign(npc, you))
			{
				return Pick("Anyway, I should get back to it.", "Good talking to you.", "See you around, " + NameOf(you) + ".", "*nods and looks away*", "I've said enough for today.");
			}
			return Pick("*bows* " + Cap(Majesty(you)) + ".", Models.IsFemale(you) ? "Long live the queen!" : "Long live the king!", "At your service, " + Majesty(you) + ".");
		}

		public static string Flirt(Actor npc, Actor you, FlirtOutcome outcome)
		{
			switch (outcome)
			{
			case FlirtOutcome.AlreadyYours:
				return Pick("*blushes* You always know what to say.", "Stop it, people are watching! ...don't stop.", "I love you too.");
			case FlirtOutcome.Accepted:
				return Pick("I... I feel the same way!", "*takes your hand* Yes. A thousand times yes.", "I was hoping you'd say that.");
			case FlirtOutcome.TakenByOther:
				return "Sorry, my heart belongs to " + NameOf(npc.lover) + ".";
			case FlirtOutcome.YouAreTaken:
				return "Shouldn't you be with " + NameOf(you.lover) + "? Settle that first.";
			case FlirtOutcome.TooYoung:
				return Pick("Ew, no!", "I'm way too young for that.", "Go away, weirdo.");
			case FlirtOutcome.Incompatible:
				return "We're... not really the same kind, are we?";
			case FlirtOutcome.Rejected:
				return Pick("Flirting? With you? Leave me be.", "Not a chance. Not ever.", "*turns away* Don't.");
			default:
				return Pick("Maybe another time.", "Ha! Nice try.", "*rolls eyes*", "I'm flattered, but no.", "Buy me dinner first.");
			}
		}

		public static string Retort(Actor npc, Actor you, Voice v, bool fights, bool child)
		{
			if (child)
			{
				return Pick("*bursts into tears*", "I'm telling my mother!", "*sticks out a tongue and runs*");
			}
			if (v.Sovereign && !fights)
			{
				return Pick("*bows their head* As you say, " + Majesty(you) + ".", "*swallows* Forgive me, " + Majesty(you) + ".", "*stares at the ground*");
			}
			if (fights)
			{
				return Pick("Say that again. I dare you.", "That's it. You'll pay for that.", "*raises a fist* You asked for this.");
			}
			if (Enemies(npc, you))
			{
				return Pick("From you, that's praise.", "Words. It's all your kind has.", "*spits at your feet*");
			}
			switch (v.Manner)
			{
			case "pacifist":
			case "peaceful":
				return Pick("There's no need for that.", "*shakes their head* I'll pray for you.");
			case "honest":
				return Pick("That was uncalled for.", "I don't deserve that, and you know it.");
			case "evil":
			case "deceitful":
				return Pick("*smiles thinly* I'll remember that.", "Careful. I keep a list.");
			case "genius":
			case "wise":
				return Pick("Anger is a poor guide.", "Is that the best you have?");
			case "stupid":
				return Pick("Huh? ...Hey!", "I don't get it. But I don't like it.");
			case "greedy":
				return Pick("Insults are free. Gold isn't.", "Say that with a coin in your hand.");
			default:
				if (v.Warmth >= 1)
				{
					return Pick(NameOf(you) + "? Why would you say that?", "*looks hurt* I thought we were friends.");
				}
				return Pick("How dare you!", "Watch your tongue.", "Who do you think you are?", "*glares at you*", "Charming.");
			}
		}

		internal static string AnimalSound(Actor npc)
		{
			string text = ((npc.asset != null) ? npc.asset.id : "");
			if (text.Contains("sheep"))
			{
				return Pick("Baaa!", "Baa baa.", "*chews grass*");
			}
			if (text.Contains("cow") || text.Contains("buffalo"))
			{
				return Pick("Mooo.", "*swishes tail*", "Moo?");
			}
			if (text.Contains("chicken"))
			{
				return Pick("Bawk!", "Buk buk buk...", "*pecks the ground*");
			}
			if (text.Contains("wolf") || text.Contains("hyena"))
			{
				return Pick("*growls*", "Grrrr...", "*bares teeth*");
			}
			if (text.Contains("dog"))
			{
				return Pick("Woof!", "*wags tail*", "Arf arf!");
			}
			if (text.Contains("cat"))
			{
				return Pick("Meow.", "*purrs*", "Mrrp?");
			}
			if (text.Contains("bear"))
			{
				return Pick("*ROAR*", "Grrr...", "*sniffs you*");
			}
			if (text.Contains("rabbit"))
			{
				return Pick("*twitches nose*", "*hops away a bit*", "...");
			}
			if (text.Contains("pig"))
			{
				return Pick("Oink!", "*snorts*", "Oink oink.");
			}
			if (text.Contains("fox"))
			{
				return Pick("*yips*", "*tilts head*", "...");
			}
			if (text.Contains("dragon"))
			{
				return Pick("*a deep rumble shakes the ground*", "ROAAAR!", "*smoke curls from its nostrils*");
			}
			if (text.Contains("skeleton") || text.Contains("zombie"))
			{
				return Pick("*rattle rattle*", "Braaains...", "*clacks jaw*");
			}
			return Pick("*stares at you*", "...", "*sniffs the air*");
		}

		internal static int SafeHappiness(Actor a)
		{
			try
			{
				return a.getHappiness();
			}
			catch
			{
				return 0;
			}
		}

		internal static string CityName(Actor a)
		{
			try
			{
				return a.getCity()?.name;
			}
			catch
			{
				return null;
			}
		}

		internal static string KingdomName(Actor a)
		{
			try
			{
				return (a.kingdom != null && a.kingdom.isCiv()) ? a.kingdom.name : null;
			}
			catch
			{
				return null;
			}
		}

		private static void EnlistChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			if (!Service.CanEnlistYou(with, you))
			{
				return;
			}
			try
			{
				if (you.isKing())
				{
					return;
				}
			}
			catch
			{
				return;
			}
			if (Service.IsSoldier(you))
			{
				list.Add(new Choice("About my orders...", delegate
				{
					OrdersTalk(c);
				}, ChoiceTone.Quest));
			}
			else if (Service.CommissionOf(you) != null)
			{
				list.Add(new Choice("About the new village...", delegate
				{
					CommissionTalk(c);
				}, ChoiceTone.Quest));
			}
			else
			{
				list.Add(new Choice("I want to enlist.", delegate
				{
					EnlistAsk(c);
				}, ChoiceTone.Quest));
			}
		}

		private static string Sir(Actor npc)
		{
			try
			{
				if (npc.isKing())
				{
					return "Your Majesty";
				}
				return npc.isSexFemale() ? "my lady" : "sir";
			}
			catch
			{
				return "sir";
			}
		}

		private static void EnlistAsk(Conversation c)
		{
			c.LastTopic = "enlist";
			Actor you = c.You;
			string text = ((you.kingdom != null) ? you.kingdom.name : "the realm");
			c.Think(0.5f);
			c.Say(Pick("Enlist? *looks you over* As what? " + text + " needs soldiers for its army, and settlers to found new villages.", "Good. We need hands. A spear in the army, or a founder to raise a new village for " + text + "?"));
			c.Offer(new List<Choice>
			{
				new Choice("As a soldier.", delegate
				{
					SoldierAsk(c);
				}, ChoiceTone.Quest),
				new Choice("As a founder. I'll start a new village.", delegate
				{
					FounderAsk(c);
				}, ChoiceTone.Quest),
				new Choice("Never mind.", delegate
				{
					c.Say(Pick("As you like.", "The offer stands."));
					c.Offer(Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void SoldierAsk(Conversation c)
		{
			c.LastTopic = "enlist_soldier";
			Actor you = c.You;
			Actor with = c.With;
			string text = Service.EnlistRefusal(with, you);
			if (text != null)
			{
				Log.Info("service: " + NameOf(with) + " won't enlist you: " + text);
				c.Say(text);
				c.Offer(Topics(c));
				return;
			}
			City city = you.city;
			string text2 = (with.isKing() ? "I call" : (Service.Lord(you.kingdom, city) + " calls"));
			c.Think(0.8f);
			c.Say(Pick("A soldier of " + city.name + ", then.", "*nods slowly* We can use you.", "The army of " + city.name + " could use another blade."), "You'll drill at the dummy, keep the town safe, and march when " + text2 + ". Your orders will be on your map (M): where to go, who to fight.", $"The pay is {4} gold a year from the treasury, and more when we take a town. Will you swear to serve {you.kingdom.name}?");
			c.Offer(new List<Choice>
			{
				new Choice("I swear to serve " + you.kingdom.name + ".", delegate
				{
					SoldierNow(c);
				}, ChoiceTone.Quest),
				new Choice("Let me think about it.", delegate
				{
					c.Say(Pick("Don't think too long. Wars don't wait.", "Take your time."));
					c.Offer(Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void SoldierNow(Conversation c)
		{
			Actor you = c.You;
			Actor with = c.With;
			if (!Service.Enlist(you))
			{
				c.Say("*frowns* The quartermaster says there's no place for you. Not today.");
				c.Offer(Topics(c));
				return;
			}
			Chronicle.ChangeOpinion(with, you, 5);
			string text = null;
			try
			{
				if (you.equipment != null && !you.equipment.weapon.isEmpty())
				{
					text = you.equipment.weapon.getItem().asset.id.Replace('_', ' ');
				}
			}
			catch
			{
			}
			c.Say(Pick("Welcome to the army.", "Then you're one of us now.", "Stand straight, soldier.") + ((text != null) ? (" Take this " + text + " from the armory.") : "") + " Look to your orders.");
			c.Toast?.Invoke("You enlisted in the army of " + you.city.name + ". M shows your orders.");
			c.Offer(new List<Choice>
			{
				new Choice("Yes, " + Sir(with) + ".", delegate
				{
					c.Say(Pick("Dismissed.", "Go on, then."));
					c.Offer(Topics(c));
				}),
				Goodbye(c)
			});
		}

		private static void FounderAsk(Conversation c)
		{
			c.LastTopic = "enlist_founder";
			Actor you = c.You;
			Actor with = c.With;
			string text = Service.FoundRefusal(with, you);
			if (text != null)
			{
				Log.Info("service: " + NameOf(with) + " won't send you to found a village: " + text);
				c.Say(text);
				c.Offer(Topics(c));
				return;
			}
			City city = you.city;
			TileZone site = Service.Sites(you, city, 1)[0];
			List<Actor> settlers = Service.Volunteers(you, city);
			string text2 = Service.Heading(Service.CityPoint(city), new Vector2((float)site.centerTile.x + 0.5f, (float)site.centerTile.y + 0.5f));
			string text3 = ((settlers.Count == 0) ? null : ((settlers.Count == 1) ? Kin(settlers[0]) : (string.Join(", ", settlers.GetRange(0, settlers.Count - 1).ConvertAll<string>(Kin).ToArray()) + " and " + Kin(settlers[settlers.Count - 1]))));
			string text4 = ((text3 == null) ? "You've no family here to go with you, so you'll go alone; others will come once it stands." : (char.ToUpperInvariant(text3[0]) + text3.Substring(1) + " will go with you."));
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = you.isCityLeader();
				flag2 = you.isWarrior();
			}
			catch
			{
			}
			string text5 = (flag ? ("\nYou'd give up the lead of " + city.name + " once the new village stands.") : (flag2 ? "\nYou'd leave the army." : ""));
			c.Think(1f);
			c.Say(Pick("A new village... *strokes chin* Yes. There's free land.", "*thinks it over* We've grown. It's time someone went."), "There's good land " + text2 + " of " + city.name + ". Go there, raise a village for " + you.kingdom.name + ", and lead it. " + text4 + "\nYour map (M) shows the way; any free land will do if you find better." + text5);
			c.Offer(new List<Choice>
			{
				new Choice("I'll found it.", delegate
				{
					FounderNow(c, site, settlers);
				}, ChoiceTone.Quest),
				new Choice("Let me think about it.", delegate
				{
					c.Say(Pick("The land won't wait forever.", "Take your time."));
					c.Offer(Topics(c));
				}, ChoiceTone.Back)
			});
			string Kin(Actor a)
			{
				return "your " + Service.Relation(you, a) + " " + NameOf(a);
			}
		}

		private static void FounderNow(Conversation c, TileZone site, List<Actor> settlers)
		{
			Actor you = c.You;
			Actor with = c.With;
			if (site == null || site.hasCity() || !site.isGoodForNewCity(you))
			{
				List<TileZone> list = Service.Sites(you, you.city, 1);
				site = ((list.Count > 0) ? list[0] : null);
			}
			if (site == null)
			{
				c.Say("*frowns* Someone's settled that land already. There's nowhere left.");
				c.Offer(Topics(c));
				return;
			}
			Commission commission = Service.Commission(you, with, site, settlers);
			Chronicle.ChangeOpinion(with, you, 5);
			string text = Service.Heading(you.current_position, commission.Site);
			c.Say(Pick("Go well, founder.", "May the land be kind to you.", "Make us proud."), "Walk " + text + ". Where the land is good, raise the village (E).");
			c.Toast?.Invoke("Go " + text + " and found a village for " + commission.RealmName + " (E on free land). M shows the way.");
			c.Offer(new List<Choice> { Goodbye(c) });
		}

		private static void OrdersTalk(Conversation c)
		{
			c.LastTopic = "orders";
			Actor you = c.You;
			Actor with = c.With;
			WorldBoxMod instance = WorldBoxMod.Instance;
			Order order = ((instance != null && instance.Orders != null) ? instance.Orders : Service.OrderFor(you, instance?.Royal, instance?.WarMap));
			if (order == null)
			{
				c.Say("*squints* You're no soldier of ours.");
				c.Offer(Topics(c));
				return;
			}
			string text = (order.HasWhere ? Service.Heading(you.current_position, order.Where) : null);
			c.Say(order.Title + ". " + order.Detail + ((text != null && text != "here") ? ("\nThat's " + text + " of here.") : ""));
			c.Offer(new List<Choice>
			{
				new Choice("Yes, " + Sir(with) + ".", delegate
				{
					c.Say(Pick("Good. Go.", "Dismissed."));
					c.Offer(Topics(c));
				}),
				new Choice("I want to leave the army.", delegate
				{
					LeaveArmyAsk(c);
				}, ChoiceTone.Risk),
				Back(c)
			});
		}

		private static void LeaveArmyAsk(Conversation c)
		{
			c.LastTopic = "leave_army";
			Actor you = c.You;
			List<Kingdom> wars = Service.Wars(you.kingdom);
			c.Say((wars.Count > 0) ? Pick("Now? With " + wars[0].name + " at our throats? *glares*", "Leave in the middle of a war? Others have hanged for less.") : Pick("You'd lay down your arms?", "*sighs* Peace makes soldiers restless."));
			c.Offer(new List<Choice>
			{
				new Choice("Yes. I'm done with fighting.", delegate
				{
					Service.Discharge(you);
					Chronicle.ChangeOpinion(c.With, you, (wars.Count > 0) ? (-10) : (-2));
					c.Say((wars.Count > 0) ? "Then go. And don't expect a welcome when it's over." : "Then hand in your orders. You're a free citizen again.");
					c.Toast?.Invoke("You left the army of " + you.city?.name + ".");
					c.Offer(Topics(c));
				}, ChoiceTone.Risk),
				new Choice("No. I'll stay.", delegate
				{
					c.Say(Pick("Good.", "I thought so."));
					c.Offer(Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void CommissionTalk(Conversation c)
		{
			c.LastTopic = "commission";
			Actor you = c.You;
			Commission commission = Service.CommissionOf(you);
			if (commission == null)
			{
				c.Offer(Topics(c));
				return;
			}
			int count = Service.SettlersOf(you).Count;
			c.Say("The land is " + Service.Heading(you.current_position, commission.Site) + " of here. Raise the village on good free land (E), and lead it." + ((count > 0) ? string.Format(" {0} settler{1} with you.", count, (count == 1) ? " walks" : "s walk") : ""));
			c.Offer(new List<Choice>
			{
				new Choice("I'm on my way.", delegate
				{
					c.Say(Pick("Then don't let me keep you.", "Go well."));
					c.Offer(Topics(c));
				}),
				new Choice("I give up the charge.", delegate
				{
					Service.GiveUpCommission(you, c.Toast);
					Chronicle.ChangeOpinion(c.With, you, -3);
					c.Say(Pick("*sighs* Someone else will go, then.", "A pity. The land will wait for braver feet."));
					c.Offer(Topics(c));
				}, ChoiceTone.Risk),
				Back(c)
			});
		}

		public static string WayWord(Charm c)
		{
			switch (c)
			{
			default:
				return "sincere";
			case Charm.Witty:
				return "playful";
			case Charm.Bold:
				return "bold";
			case Charm.Sweet:
				return "gentle";
			}
		}

		private static string WorkPlace(string job)
		{
			switch (job)
			{
			case "farmer":
				return "the fields";
			case "smith":
				return "the forge";
			case "woodcutter":
				return "the woods";
			case "miner":
				return "the mine";
			case "builder":
				return "the walls";
			case "hunter":
				return "the hunt";
			case "fisher":
				return "the nets";
			case "gatherer":
				return "the hedgerows";
			case "warrior":
				return "drill";
			default:
				return "work";
			}
		}

		private static string HopeLine(Hope h, Actor npc)
		{
			switch (h)
			{
			case Hope.Glory:
				return "One day people will know my name. You'll see.";
			case Hope.Wealth:
				return "If I had a little more coin, I'd never worry about anything again.";
			case Hope.Wisdom:
				return "I want to understand things. Why the stars move, why we're here at all.";
			case Hope.Adventure:
				return "I'd give anything to see what lies past the edge of the map.";
			case Hope.Family:
				return "I want a long table, full of children and noise. That's all.";
			default:
				return "A small house, a garden, and quiet. That's all I want out of life.";
			}
		}

		internal static string HopeWordsOf(Hope h)
		{
			switch (h)
			{
			case Hope.Glory:
				return "your name in songs";
			case Hope.Wealth:
				return "a full purse";
			case Hope.Wisdom:
				return "to understand the world";
			case Hope.Adventure:
				return "the far side of the map";
			case Hope.Family:
				return "a house full of family";
			default:
				return "a quiet life";
			}
		}

		private static Opening PickOpening(Actor npc, Actor you)
		{
			List<Opening> list = new List<Opening>();
			_lastOpening.TryGetValue(npc.data.id, out var value);
			Opening[] openings = Openings;
			foreach (Opening opening in openings)
			{
				if (OpeningForTest != null)
				{
					if (opening.Id == OpeningForTest)
					{
						return opening;
					}
					continue;
				}
				bool flag;
				try
				{
					flag = opening.When(npc, you);
				}
				catch
				{
					flag = false;
				}
				if (flag && !(opening.Id == value) && (!opening.TellsHope || !Romance.KnowsFact(npc, you, Fact.Hope)))
				{
					list.Add(opening);
				}
			}
			if (list.Count == 0)
			{
				return Openings[8];
			}
			return list[Rng.Next(list.Count)];
		}

		private static string RememberedReply(Actor npc, Actor you, out Fact which)
		{
			List<Fact> list = new List<Fact>();
			if (Romance.KnowsFact(npc, you, Fact.Hope))
			{
				list.Add(Fact.Hope);
			}
			if (Romance.KnowsFact(npc, you, Fact.Place))
			{
				list.Add(Fact.Place);
			}
			if (Romance.KnowsFact(npc, you, Fact.Gift))
			{
				list.Add(Fact.Gift);
			}
			which = Fact.Taste;
			if (list.Count == 0)
			{
				return null;
			}
			which = list[Rng.Next(list.Count)];
			switch (which)
			{
			case Fact.Hope:
				return "You told me you hope for " + HopeWordsOf(Romance.HopeOf(npc)) + ". I haven't forgotten.";
			case Fact.Place:
				return "Come out to " + Romance.PlaceWords(Romance.FavouritePlace(npc)) + " with me. I know you love it there.";
			default:
				return "I passed some " + Work.ResourceName(Romance.Favourite(npc)).ToLowerInvariant() + " today and thought of you.";
			}
		}

		private static List<Choice> FlirtMenuChoices(Conversation c)
		{
			Actor with = c.With;
			Actor you = c.You;
			List<Choice> list = new List<Choice>();
			int num = Romance.ExchangesLeft(with, you);
			Choice choice = new Choice((num > 0) ? $"Flirt  ({num} left today)" : "Flirt", delegate
			{
				StartExchange(c);
			}, ChoiceTone.Romance);
			list.Add((num > 0) ? choice : choice.Off("Enough for today"));
			if (AskChoices(c).Count > 0)
			{
				list.Add(new Choice("Ask about them", delegate
				{
					c.LastTopic = "flirt ask";
					c.Say(Pick("*tilts head* What do you want to know?", "Ask, then.", "*smiles* Curious about me?"));
					List<Choice> list2 = AskChoices(c);
					list2.Add(Back(c));
					c.Offer(list2);
				}));
			}
			Choice choice2 = new Choice("I brought you something", delegate
			{
				FlirtGiftMenu(c);
			}, ChoiceTone.Romance);
			list.Add(Romance.GaveToday(with, you) ? choice2.Off("Once a day") : ((GiftWares(you).Count == 0) ? choice2.Off("Nothing in your bag") : choice2));
			Quests.CourtChoices(c, Romance.Of(with, you), list);
			list.Add(Back(c));
			return list;
		}

		private static void StartExchange(Conversation c)
		{
			Actor with = c.With;
			Actor you = c.You;
			c.LastTopic = "flirt";
			if (Romance.ExchangesLeft(with, you) <= 0)
			{
				c.Say(EnoughLine(with));
				c.Offer(FlirtMenuChoices(c));
				return;
			}
			Opening opening = PickOpening(with, you);
			_lastOpening[with.data.id] = opening.Id;
			string text;
			try
			{
				text = opening.Theirs(with, you);
			}
			catch
			{
				text = "*looks at you* Well?";
			}
			if (opening.TellsHope)
			{
				Romance.Tell(with, you, Fact.Hope);
			}
			c.Say(text);
			List<Charm> list = new List<Charm>
			{
				Charm.Sweet,
				Charm.Bold,
				Charm.Witty,
				Charm.Deep
			};
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = Rng.Next(num + 1);
				Charm value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			List<Choice> list2 = new List<Choice>();
			Fact which;
			string text2 = ((Rng.NextDouble() < 0.6) ? RememberedReply(with, you, out which) : null);
			int num2 = ((text2 != null) ? 2 : 3);
			for (int i = 0; i < num2; i++)
			{
				Charm way = list[i];
				list2.Add(new Choice(opening.Replies[(uint)way] + "   · " + WayWord(way), delegate
				{
					Answer(c, way, personal: false);
				}, ChoiceTone.Romance));
			}
			if (text2 != null)
			{
				Charm way2 = Romance.Likes(with);
				list2.Insert(Rng.Next(list2.Count + 1), new Choice(text2 + "   · remembered", delegate
				{
					Answer(c, way2, personal: true);
				}, ChoiceTone.Romance));
			}
			list2.Add(new Choice("Say nothing", delegate
			{
				c.LastTopic = "flirt none";
				c.Say(Pick("*shrugs* Suit yourself.", "Hm."));
				c.Offer(FlirtMenuChoices(c));
			}, ChoiceTone.Back));
			c.Offer(list2);
		}

		private static void Answer(Conversation c, Charm charm, bool personal)
		{
			_flirtTurn++;
			Romance.Result result = Romance.Flirt(c.You, c.With, charm, personal);
			c.LastTopic = "flirt " + charm.ToString().ToLowerInvariant();
			c.Say(result.Reply);
			c.Romantic = result.Outcome == FlirtOutcome.Warm || result.Outcome == FlirtOutcome.AlreadyYours;
			if (result.Outcome == FlirtOutcome.Warm)
			{
				c.Hearts?.Invoke(c.With);
			}
			if (result.NowReady)
			{
				c.Toast?.Invoke(NameOf(c.With) + " would let you court them");
			}
			else if (result.Outcome != FlirtOutcome.Enough && result.Outcome >= FlirtOutcome.Warm)
			{
				c.Toast?.Invoke(MeterToast(result.Gain, result.Capped));
			}
			c.Relation = Romance.WithStanding(RelationTo(c.With, c.You), c.With, c.You);
			c.Offer(FlirtMenuChoices(c));
		}

		private static string MeterToast(int gain, bool capped)
		{
			if (!capped || gain > 0)
			{
				if (!capped)
				{
					if (gain < 0)
					{
						return $"Romance {gain}";
					}
					return $"Romance +{gain}";
				}
				return $"Romance +{gain}, full for today";
			}
			return "Romance full for today";
		}

		private static List<Choice> AskChoices(Conversation c)
		{
			Actor npc = c.With;
			Actor you = c.You;
			List<Choice> list = new List<Choice>();
			Q(Fact.Taste, "What do you look for in a person?", () => TasteLine(Romance.Likes(npc), null));
			Q(Fact.Gift, "What makes a good day for you?", () => GoodDayLine(npc));
			Q(Fact.Place, "Where do you go to be alone?", () => PlaceLine(npc));
			Q(Fact.Hope, "What do you hope for, someday?", () => HopeLine(Romance.HopeOf(npc), npc));
			return list;
			void Q(Fact f, string label, Func<string> answer)
			{
				if (!Romance.KnowsFact(npc, you, f))
				{
					list.Add(new Choice(label, delegate
					{
						c.LastTopic = "flirt ask " + f.ToString().ToLowerInvariant();
						c.Think(0.5f);
						Romance.Tell(npc, you, f);
						c.Say(answer());
						List<Choice> list2 = AskChoices(c);
						list2.Add(Back(c));
						c.Offer(list2);
					}));
				}
			}
		}

		private static string GoodDayLine(Actor npc)
		{
			string text = Work.ResourceName(Romance.Favourite(npc)).ToLowerInvariant();
			return Pick(Cap(text) + ", and nobody needing anything from me. That's a good day.", "Honestly? Someone bringing me " + text + ". I'm easy to please.");
		}

		private static string PlaceLine(Actor npc)
		{
			string text = CityName(npc) ?? "town";
			switch (Romance.FavouritePlace(npc))
			{
			case "flowers":
				return "Out past the fields, where the flowers grow wild. Nobody follows me there.";
			case "hill":
				return "Up on the hills. You can see the whole of " + text + " from the top.";
			case "shore":
				return "Down by the water. The sound of it settles me.";
			default:
				return "Into the woods, deep in, where it's green and quiet.";
			}
		}

		private static List<KeyValuePair<string, int>> GiftWares(Actor you)
		{
			List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
			try
			{
				foreach (KeyValuePair<string, int> item in Bag.Contents(you))
				{
					if (item.Value > 0 && !FirstPerson.Supplies.Is(item.Key))
					{
						list.Add(item);
					}
				}
			}
			catch
			{
			}
			list.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> b) => b.Value.CompareTo(a.Value));
			if (list.Count > 8)
			{
				list.RemoveRange(8, list.Count - 8);
			}
			return list;
		}

		private static void FlirtGiftMenu(Conversation c)
		{
			c.LastTopic = "flirt gift";
			c.Say(Pick("*raises an eyebrow* For me? What is it?", "Oh? Let's see it."));
			List<Choice> list = new List<Choice>();
			foreach (KeyValuePair<string, int> item in GiftWares(c.You))
			{
				string res = item.Key;
				list.Add(new Choice("Give " + Work.ResourceName(res).ToLowerInvariant(), delegate
				{
					GiveGift(c, res);
				}, ChoiceTone.Romance));
			}
			list.Add(Back(c));
			c.Offer(list);
		}

		private static void GiveGift(Conversation c, string res)
		{
			c.LastTopic = "flirt gift given";
			Romance.GiftTaste taste;
			bool capped;
			int? num = Romance.Give(c.You, c.With, res, out taste, out capped);
			if (!num.HasValue)
			{
				c.Say("*shakes head* Keep it.");
				c.Offer(FlirtMenuChoices(c));
				return;
			}
			string text = Work.ResourceName(res).ToLowerInvariant();
			switch (taste)
			{
			case Romance.GiftTaste.Loved:
				c.Say(Pick("*eyes go wide* " + Cap(text) + "! How did you know?", "*holds it close* " + Cap(text) + ". You remembered."));
				c.Hearts?.Invoke(c.With);
				c.Romantic = true;
				break;
			case Romance.GiftTaste.Liked:
				c.Say(Pick("*smiles* That's kind of you.", Cap(text) + "? Thank you, really."));
				break;
			case Romance.GiftTaste.Plain:
				c.Say(Pick("*turns it over* " + Cap(text) + ". Well. Thank you, I suppose.", "*puzzled* That's... useful."));
				break;
			default:
				c.Say(Pick("*wrinkles nose* " + Cap(text) + ". I can't stand " + text + ".", "*hands it back* Oh. No, thank you."));
				break;
			}
			c.Toast?.Invoke(MeterToast(num.Value, capped));
			c.Relation = Romance.WithStanding(RelationTo(c.With, c.You), c.With, c.You);
			c.Offer(FlirtMenuChoices(c));
		}

		public static string EnoughLine(Actor npc)
		{
			return Pick("*laughs* That's enough sweet talk for one day. Come and find me tomorrow.", "*pushes you off gently* Save some for tomorrow.", "You'll wear the words out. Tomorrow.");
		}

		public static string CappedLine(Actor npc)
		{
			return Pick("*smiles* Don't rush me. Come back tomorrow.", "*quietly* Slow down. There's always tomorrow.");
		}

		public static string WelcomeBack(Actor npc, Actor you, int days)
		{
			return Pick("*face lights up* You came back! I wondered where you'd gone.", "There you are. I missed you, you know.", $"{days} days! I was starting to think you'd forgotten me.");
		}

		private static bool HasFaith(Actor a)
		{
			try
			{
				return a.religion != null || a.culture != null || a.language != null;
			}
			catch
			{
				return false;
			}
		}

		private static void Faith(Conversation c)
		{
			c.LastTopic = "faith";
			Actor with = c.With;
			Actor you = c.You;
			List<string> list = new List<string>();
			Religion rel = null;
			try
			{
				rel = with.religion;
			}
			catch
			{
			}
			if (rel != null)
			{
				string text = null;
				try
				{
					text = rel.data.creator_name;
				}
				catch
				{
				}
				int num = 0;
				int num2 = 0;
				try
				{
					num = rel.getAge();
				}
				catch
				{
				}
				try
				{
					num2 = rel.countUnits();
				}
				catch
				{
				}
				string text2 = ((num <= 0) ? "only this year" : ((num == 1) ? "a year ago" : (num + " years ago")));
				string text3 = (string.IsNullOrEmpty(text) ? "" : ((text == NameOf(with)) ? (" I founded it myself, " + text2 + ".") : (" " + text + " founded it, " + text2 + ".")));
				list.Add(Pick("I follow " + rel.name + "." + text3, rel.name + " is my faith." + text3));
				string text4 = TraitNames(rel, 3);
				string text5 = ((num2 > 1) ? $"{num2} of us keep the faith" : "I may be the last who keeps it");
				list.Add((text4 != null) ? ("We hold to " + text4 + ". " + text5 + ".") : (text5 + "."));
			}
			else
			{
				list.Add(Pick("Gods? I've never seen one.", "I don't follow any faith.", "Faith is for those who need it."));
			}
			Culture cul = null;
			try
			{
				cul = with.culture;
			}
			catch
			{
			}
			if (cul != null)
			{
				string text6 = CultureCustoms(cul);
				list.Add("We are of the " + cul.name + " people." + ((text6 != null) ? (" " + text6) : ""));
			}
			Language language = null;
			try
			{
				language = with.language;
			}
			catch
			{
			}
			if (language != null && (cul == null || language.name != cul.name))
			{
				list.Add(Pick("At home we speak " + language.name + ".", "My mother tongue is " + language.name + "."));
			}
			c.Say(list.ToArray());
			List<Choice> list2 = new List<Choice>();
			Religion mine = null;
			try
			{
				mine = you.religion;
			}
			catch
			{
			}
			if (mine != null && mine != rel)
			{
				list2.Add(new Choice("Let me tell you about " + mine.name + ". (preach)", delegate
				{
					Preach(c, mine);
				}, ChoiceTone.Risk));
			}
			if (rel != null && mine != rel)
			{
				list2.Add(new Choice("I want to follow " + rel.name + ".", delegate
				{
					Join(c, rel);
				}));
			}
			if (cul != null)
			{
				list2.Add(new Choice("What are your people's customs?", delegate
				{
					Customs(c, cul);
				}));
			}
			list2.AddRange(BackOnly(c));
			c.Offer(list2);
		}

		private static string TraitNames<TData, TTrait>(MetaObjectWithTraits<TData, TTrait> owner, int max) where TData : MetaObjectData where TTrait : BaseTrait<TTrait>
		{
			try
			{
				List<string> list = new List<string>();
				foreach (TTrait trait in owner.getTraits())
				{
					if (trait != null)
					{
						string text = null;
						try
						{
							text = trait.getTranslatedName();
						}
						catch
						{
						}
						if (string.IsNullOrEmpty(text) || text.StartsWith("$"))
						{
							text = trait.id.Replace('_', ' ');
						}
						list.Add(text.ToLowerInvariant());
						if (list.Count >= max)
						{
							break;
						}
					}
				}
				if (list.Count == 0)
				{
					return null;
				}
				if (list.Count == 1)
				{
					return list[0];
				}
				return string.Join(", ", list.GetRange(0, list.Count - 1).ToArray()) + " and " + list[list.Count - 1];
			}
			catch
			{
				return null;
			}
		}

		private static string CultureCustoms(Culture cul)
		{
			List<string> list = new List<string>();
			try
			{
				if (cul.hasTrait("patriarchy"))
				{
					list.Add("Men lead our households.");
				}
				if (cul.hasTrait("matriarchy"))
				{
					list.Add("Our mothers lead the households.");
				}
				if (cul.hasTrait("elder_reverence"))
				{
					list.Add("We honour our elders above all.");
				}
				if (cul.hasTrait("ultimogeniture"))
				{
					list.Add("The youngest child inherits.");
				}
				if (cul.hasTrait("primogeniture"))
				{
					list.Add("The eldest child inherits.");
				}
			}
			catch
			{
			}
			if (list.Count != 0)
			{
				return string.Join(" ", list.ToArray());
			}
			return null;
		}

		private static void Customs(Conversation c, Culture cul)
		{
			string text = CultureCustoms(cul);
			string text2 = TraitNames(cul, 4);
			string text3 = null;
			try
			{
				int count = cul.getBooks().Count;
				if (count > 0)
				{
					text3 = ((count == 1) ? "We've one book in our tongue." : $"We've {count} books written in our tongue.");
				}
			}
			catch
			{
			}
			c.Say(text ?? "Our ways are simple.", (text2 != null) ? ("Our ways: " + text2 + ".") : null, text3);
			c.Offer(BackOnly(c));
		}

		private static void Preach(Conversation c, Religion mine)
		{
			Actor with = c.With;
			Actor you = c.You;
			int num = Chronicle.Opinion(with, you);
			float num2 = 0.25f + (float)num / 250f;
			if (with.religion == null)
			{
				num2 += 0.25f;
			}
			if (HasTrait(with, "stupid"))
			{
				num2 += 0.1f;
			}
			if (HasTrait(with, "wise") || HasTrait(with, "genius"))
			{
				num2 -= 0.1f;
			}
			if (Enemies(with, you))
			{
				num2 -= 0.3f;
			}
			bool flag = false;
			if (UnityEngine.Random.value < num2)
			{
				try
				{
					flag = with.tryToConvertToReligion(mine);
				}
				catch
				{
					flag = false;
				}
			}
			if (flag)
			{
				Quests.Converted(you, with);
				Chronicle.ChangeOpinion(with, you, 6);
				c.Voice = VoiceOf(with, you);
				c.Say(Pick("I... yes. I see it now. " + mine.name + " it is.", "You've opened my eyes. I'll follow " + mine.name + ".", "*kneels* Show me the way."));
			}
			else
			{
				Chronicle.ChangeOpinion(with, you, -3);
				c.Say(Pick("Spare me your sermons.", "My faith is my own.", "Interesting... but no.", (with.religion != null) ? ("I'll stay with " + with.religion.name + ", thank you.") : "I'll think about it. Maybe."));
			}
			c.Offer(Topics(c));
		}

		private static void Join(Conversation c, Religion theirs)
		{
			bool flag = false;
			try
			{
				flag = c.You.tryToConvertToReligion(theirs);
			}
			catch
			{
			}
			if (flag)
			{
				Chronicle.ChangeOpinion(c.With, c.You, 10);
				c.Say(Pick("Welcome to " + theirs.name + "!", "*clasps your hands* " + theirs.name + " welcomes you."));
				c.Toast?.Invoke("You now follow " + theirs.name);
			}
			else
			{
				c.Say("Your heart isn't ready, I can tell.");
			}
			c.Offer(Topics(c));
		}

		public static FamilyTree TreeOf(Actor a)
		{
			FamilyTree familyTree = new FamilyTree();
			Record record = Chronicle.Of(a);
			familyTree.Self = new TreeNode
			{
				Id = a.data.id,
				Name = NameOf(a),
				Role = "",
				Actor = a,
				Female = Models.IsFemale(a)
			};
			try
			{
				familyTree.Self.Born = Date.getYear(a.data.created_time);
			}
			catch
			{
			}
			if (record != null)
			{
				string[] array = new string[4] { "g1", "g2", "g3", "g4" };
				for (int i = 0; i < 4; i++)
				{
					familyTree.Grandparents[i] = Node(record.Get(array[i]), (i % 2 == 0) ? "grandfather" : "grandmother");
				}
				familyTree.Parents[0] = Node(record.Get("p1"), "parent");
				familyTree.Parents[1] = Node(record.Get("p2"), "parent");
				Kin kin = record.Get("lv");
				if (kin != null)
				{
					familyTree.Partners.Add(Node(kin, "lover"));
				}
				foreach (Kin item in record.All("xl"))
				{
					familyTree.Partners.Add(Node(item, "lover"));
				}
				foreach (Kin item2 in Chronicle.SiblingsOf(record))
				{
					familyTree.Siblings.Add(Node(item2, "sibling"));
				}
				foreach (Kin item3 in record.All("ch"))
				{
					familyTree.Children.Add(Node(item3, "child"));
				}
			}
			long num = 0L;
			long num2 = 0L;
			long id = a.data.id;
			try
			{
				num = a.data.parent_id_1;
				num2 = a.data.parent_id_2;
			}
			catch
			{
			}
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple == null || simple == a || !simple.isAlive())
					{
						continue;
					}
					long parent_id_ = simple.data.parent_id_1;
					long parent_id_2 = simple.data.parent_id_2;
					if (parent_id_ != 0L || parent_id_2 != 0L)
					{
						if (parent_id_ == id || parent_id_2 == id)
						{
							AddOnce(familyTree.Children, simple, "child");
						}
						else if ((num > 0 && (parent_id_ == num || parent_id_2 == num)) || (num2 > 0 && (parent_id_ == num2 || parent_id_2 == num2)))
						{
							AddOnce(familyTree.Siblings, simple, "sibling");
						}
					}
				}
			}
			catch
			{
			}
			TreeNode[] parents = familyTree.Parents;
			foreach (TreeNode treeNode in parents)
			{
				if (treeNode != null)
				{
					treeNode.Role = (treeNode.Female ? "mother" : "father");
				}
			}
			foreach (TreeNode sibling in familyTree.Siblings)
			{
				sibling.Role = (sibling.Female ? "sister" : "brother");
			}
			foreach (TreeNode child in familyTree.Children)
			{
				child.Role = (child.Female ? "daughter" : "son");
			}
			for (int k = 0; k < 4; k++)
			{
				if (familyTree.Grandparents[k] != null)
				{
					familyTree.Grandparents[k].Role = (familyTree.Grandparents[k].Female ? "grandmother" : "grandfather");
				}
			}
			for (int l = 0; l < familyTree.Partners.Count; l++)
			{
				familyTree.Partners[l].Role = ((l == 0 && !familyTree.Partners[l].Dead && record?.Get("lv")?.Id == familyTree.Partners[l].Id) ? "lover" : (familyTree.Partners[l].Dead ? "late lover" : "lover before"));
			}
			familyTree.Children.Sort((TreeNode x, TreeNode y) => x.Born.CompareTo(y.Born));
			familyTree.Siblings.Sort((TreeNode x, TreeNode y) => x.Born.CompareTo(y.Born));
			try
			{
				familyTree.Generation = a.data.generation;
				if (a.family != null)
				{
					familyTree.FamilyName = a.family.name;
					string founder_actor_name_ = a.family.data.founder_actor_name_1;
					string founder_actor_name_2 = a.family.data.founder_actor_name_2;
					familyTree.Founders = (string.IsNullOrEmpty(founder_actor_name_) ? null : (string.IsNullOrEmpty(founder_actor_name_2) ? founder_actor_name_ : (founder_actor_name_ + " and " + founder_actor_name_2)));
				}
				if (a.clan != null)
				{
					familyTree.ClanName = a.clan.name;
					familyTree.Motto = a.clan.getMotto();
					familyTree.ClanFounder = a.clan.data.founder_actor_name;
					Actor chief = a.clan.getChief();
					familyTree.Chief = ((chief != null) ? NameOf(chief) : null);
					familyTree.PastChiefs = ((a.clan.data.past_chiefs != null) ? a.clan.data.past_chiefs.Count : 0);
				}
			}
			catch
			{
			}
			return familyTree;
		}

		private static void AddOnce(List<TreeNode> list, Actor u, string role)
		{
			long id = u.data.id;
			foreach (TreeNode item in list)
			{
				if (item.Id == id)
				{
					item.Actor = u;
					item.Dead = false;
					return;
				}
			}
			TreeNode treeNode = new TreeNode
			{
				Id = id,
				Name = NameOf(u),
				Role = role,
				Actor = u,
				Female = Models.IsFemale(u)
			};
			try
			{
				treeNode.Born = Date.getYear(u.data.created_time);
			}
			catch
			{
			}
			list.Add(treeNode);
		}

		private static TreeNode Node(Kin k, string role)
		{
			if (k == null)
			{
				return null;
			}
			Actor actor = (k.Dead ? null : Chronicle.Find(k.Id));
			return new TreeNode
			{
				Id = k.Id,
				Name = ((actor != null) ? NameOf(actor) : k.Name),
				Role = role,
				Actor = actor,
				Female = ((actor != null) ? Models.IsFemale(actor) : k.Female),
				Dead = (actor == null),
				Born = k.Born,
				Died = k.Died
			};
		}

		private static void Family(Conversation c)
		{
			c.LastTopic = "family";
			Actor with = c.With;
			Actor you = c.You;
			bool flag = RelationTo(with, you).StartsWith("your") || (with.family != null && with.family == you.family);
			if (c.Voice.Warmth <= -2 && !flag)
			{
				c.Say(Pick("My family is none of your business.", "Leave my kin out of this.", "Why? So you can find them?"));
				c.Offer(BackOnly(c));
				return;
			}
			FamilyTree familyTree = TreeOf(with);
			c.Tree = familyTree;
			c.Panel = TalkPanel.Family;
			List<string> list = new List<string>();
			list.Add(ParentsLine(familyTree, with));
			string text = PartnerLine(familyTree);
			string text2 = ChildrenLine(familyTree);
			if (text != null || text2 != null)
			{
				list.Add(Join(text, text2));
			}
			string text3 = SiblingsLine(familyTree);
			string text4 = GrandparentsLine(familyTree);
			if (text3 != null || text4 != null)
			{
				list.Add(Join(text3, text4));
			}
			string text5 = ClanLine(familyTree);
			if (text5 != null)
			{
				list.Add(text5);
			}
			c.Say(list.ToArray());
			List<Choice> list2 = new List<Choice>();
			if (familyTree.Children.Count > 0)
			{
				list2.Add(new Choice("Tell me about your children.", delegate
				{
					Children(c);
				}));
			}
			if (familyTree.ClanName != null)
			{
				list2.Add(new Choice("Tell me about your clan.", delegate
				{
					Clan(c);
				}));
			}
			if (familyTree.Founders != null)
			{
				list2.Add(new Choice("Who started your family?", delegate
				{
					Founders(c);
				}));
			}
			list2.Add(new Choice("(Pick someone on the tree to mark them on your compass)", delegate
			{
				c.Toast?.Invoke("Click a living relative on the tree");
			}, ChoiceTone.Back));
			list2.AddRange(BackOnly(c));
			c.Offer(list2);
			Chronicle.ChangeOpinion(with, you, 1);
		}

		private static string Join(string a, string b)
		{
			if (a != null)
			{
				if (b != null)
				{
					return a + " " + b;
				}
				return a;
			}
			return b;
		}

		private static string Ago(int year)
		{
			if (year == int.MinValue)
			{
				return null;
			}
			int num = Chronicle.Year - year;
			if (num > 0)
			{
				if (num != 1)
				{
					return num + " years ago";
				}
				return "last year";
			}
			return "this year";
		}

		private static string ParentsLine(FamilyTree t, Actor npc)
		{
			TreeNode treeNode = t.Parents[0];
			TreeNode treeNode2 = t.Parents[1];
			if (treeNode == null && treeNode2 == null)
			{
				if (t.Generation > 1)
				{
					return Pick("I never knew my parents.", "My parents? I don't remember them at all.");
				}
				return Pick("Parents? I woke up in this world on my own, as the first of us did.", "I never had parents. The gods set me down here, they say.");
			}
			StringBuilder stringBuilder = new StringBuilder();
			TreeNode[] array = new TreeNode[2] { treeNode, treeNode2 };
			foreach (TreeNode treeNode3 in array)
			{
				if (treeNode3 != null)
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append(' ');
					}
					if (treeNode3.Dead)
					{
						string text = Ago(treeNode3.Died);
						stringBuilder.Append(Pick("My late " + treeNode3.Role + ", " + treeNode3.Name + ", passed " + (text ?? "long ago") + ".", "My " + treeNode3.Role + " " + treeNode3.Name + " is gone. " + Cap(text ?? "long ago") + ", now.", "I lost my " + treeNode3.Role + ", " + treeNode3.Name + ", " + (text ?? "a long time ago") + "."));
					}
					else
					{
						string text2 = WhereIs(treeNode3.Actor, npc);
						stringBuilder.Append(Pick("My " + treeNode3.Role + " is " + treeNode3.Name + text2 + ".", treeNode3.Name + " is my " + treeNode3.Role + text2 + "."));
					}
				}
			}
			return stringBuilder.ToString();
		}

		private static string Cap(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpper(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static string WhereIs(Actor who, Actor npc)
		{
			if (who == null)
			{
				return "";
			}
			try
			{
				City city = who.city;
				if (city == null)
				{
					return ", wandering the wilds";
				}
				if (npc.city == city)
				{
					return ", here in " + city.name;
				}
				return ", over in " + city.name;
			}
			catch
			{
				return "";
			}
		}

		private static string PartnerLine(FamilyTree t)
		{
			if (t.Partners.Count == 0)
			{
				return Pick("I'm not with anyone.", "No sweetheart for me, not yet.", null);
			}
			TreeNode treeNode = t.Partners[0];
			if (treeNode.Role == "lover")
			{
				return Pick("I'm with " + treeNode.Name + ".", treeNode.Name + " and I are together.", "My heart belongs to " + treeNode.Name + ".");
			}
			if (treeNode.Dead)
			{
				return Pick("I was with " + treeNode.Name + ", until " + (treeNode.Female ? "she" : "he") + " died " + (Ago(treeNode.Died) ?? "some time ago") + ".", "My " + treeNode.Name + "... gone " + (Ago(treeNode.Died) ?? "a while") + " now.");
			}
			return treeNode.Name + " and I were together, once.";
		}

		private static string ChildrenLine(FamilyTree t)
		{
			int num = 0;
			int num2 = 0;
			foreach (TreeNode child in t.Children)
			{
				if (child.Dead)
				{
					num2++;
				}
				else
				{
					num++;
				}
			}
			if (num == 0 && num2 == 0)
			{
				return null;
			}
			string text = NamesOf(t.Children, false, 3);
			object obj;
			switch (num)
			{
			default:
				obj = $"We have {num} children: {text}.";
				break;
			case 1:
				obj = "We have a child, " + text + ".";
				break;
			case 0:
				obj = "";
				break;
			}
			string text2 = (string)obj;
			if (num2 > 0)
			{
				text2 = text2 + ((text2.Length > 0) ? " " : "") + ((num2 == 1) ? ("We lost " + NamesOf(t.Children, true, 1) + ".") : $"We buried {num2} of our little ones.");
			}
			return text2;
		}

		private static string SiblingsLine(FamilyTree t)
		{
			if (t.Siblings.Count == 0)
			{
				return null;
			}
			int num = 0;
			int num2 = 0;
			foreach (TreeNode sibling in t.Siblings)
			{
				if (sibling.Female)
				{
					num2++;
				}
				else
				{
					num++;
				}
			}
			string text = ((num > 0 && num2 > 0) ? (Count(num, "brother") + " and " + Count(num2, "sister")) : ((num > 0) ? Count(num, "brother") : Count(num2, "sister")));
			return "I have " + text + ": " + NamesOf(t.Siblings, null, 4) + ".";
		}

		private static string Count(int n, string what)
		{
			return ((n == 1) ? "a" : n.ToString()) + " " + what + ((n == 1) ? "" : "s");
		}

		private static string GrandparentsLine(FamilyTree t)
		{
			List<TreeNode> list = new List<TreeNode>();
			List<TreeNode> list2 = new List<TreeNode>();
			TreeNode[] grandparents = t.Grandparents;
			foreach (TreeNode treeNode in grandparents)
			{
				if (treeNode != null)
				{
					(treeNode.Dead ? list2 : list).Add(treeNode);
				}
			}
			if (list.Count == 0 && list2.Count == 0)
			{
				return null;
			}
			if (list.Count > 0)
			{
				return "My " + list[0].Role + " " + list[0].Name + " is still with us" + ((list.Count > 1) ? (", and " + list[1].Name + " too.") : ".");
			}
			return "My " + list2[0].Role + " " + list2[0].Name + " died " + (Ago(list2[0].Died) ?? "before my time") + ".";
		}

		private static string NamesOf(List<TreeNode> nodes, bool? dead, int max)
		{
			List<string> list = new List<string>();
			foreach (TreeNode node in nodes)
			{
				if (!dead.HasValue || node.Dead == dead.Value)
				{
					list.Add((node.Dead && dead != true) ? (node.Name + " (late)") : node.Name);
				}
			}
			if (list.Count == 0)
			{
				return "";
			}
			if (list.Count > max)
			{
				return string.Join(", ", list.GetRange(0, max).ToArray()) + " and " + (list.Count - max) + " more";
			}
			if (list.Count == 1)
			{
				return list[0];
			}
			return string.Join(", ", list.GetRange(0, list.Count - 1).ToArray()) + " and " + list[list.Count - 1];
		}

		private static string ClanLine(FamilyTree t)
		{
			if (t.ClanName == null)
			{
				if (t.FamilyName == null)
				{
					return null;
				}
				return "We are the " + t.FamilyName + ".";
			}
			string text = (string.IsNullOrEmpty(t.Motto) ? "" : (" Our words: \"" + t.Motto + "\""));
			return "We are of the clan " + t.ClanName + "." + text;
		}

		private static void Children(Conversation c)
		{
			FamilyTree obj = c.Tree ?? TreeOf(c.With);
			List<string> list = new List<string>();
			foreach (TreeNode child in obj.Children)
			{
				if (list.Count >= 4)
				{
					break;
				}
				string text = null;
				try
				{
					if (child.Actor != null && child.Actor.isAlive())
					{
						text = child.Actor.getAge().ToString();
					}
				}
				catch
				{
				}
				if (text == null && child.Born != int.MinValue)
				{
					text = (Chronicle.Year - child.Born).ToString();
				}
				if (child.Dead)
				{
					list.Add(Pick(child.Name + "... we lost " + (child.Female ? "her" : "him") + " " + (Ago(child.Died) ?? "too soon") + ".", "Our " + child.Role + " " + child.Name + " is buried by the old tree."));
				}
				else
				{
					string text2 = JobName(child.Actor);
					string text3 = WhereIs(child.Actor, c.With);
					list.Add(child.Name + ", my " + child.Role + ((text != null) ? (", is " + text) : "") + ((text2 != null) ? (", a " + text2) : "") + text3 + ".");
				}
			}
			if (list.Count == 0)
			{
				list.Add("No children. Not yet.");
			}
			c.Say(list.ToArray());
			c.Offer(BackOnly(c));
		}

		private static void Clan(Conversation c)
		{
			FamilyTree familyTree = c.Tree ?? TreeOf(c.With);
			List<string> list = new List<string> { ClanLine(familyTree) };
			if (familyTree.ClanFounder != null)
			{
				list.Add(familyTree.ClanFounder + " founded it" + ((familyTree.PastChiefs > 0) ? $", and {familyTree.PastChiefs} chiefs have led it since." : "."));
			}
			if (familyTree.Chief != null)
			{
				list.Add((familyTree.Chief == familyTree.Self.Name) ? "I lead it now. Don't make me regret telling you." : (familyTree.Chief + " is our chief now."));
			}
			c.Say(list.ToArray());
			c.Offer(BackOnly(c));
		}

		private static void Founders(Conversation c)
		{
			FamilyTree familyTree = c.Tree ?? TreeOf(c.With);
			string text = ((familyTree.Generation > 1) ? (" I'm of the " + Ordinal(familyTree.Generation) + " generation.") : "");
			c.Say("The " + (familyTree.FamilyName ?? "family") + " began with " + familyTree.Founders + "." + text);
			c.Offer(BackOnly(c));
		}

		private static string Ordinal(int n)
		{
			int num = n % 100;
			string text = ((num >= 11 && num <= 13) ? "th" : ((n % 10 == 1) ? "st" : ((n % 10 == 2) ? "nd" : ((n % 10 == 3) ? "rd" : "th"))));
			return n + text;
		}

		private static void FindChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			try
			{
				if (with.city == null || !with.city.isAlive())
				{
					return;
				}
			}
			catch
			{
				return;
			}
			list.Add(new Choice("I'm looking for someone.", delegate
			{
				FindMenu(c);
			}));
		}

		private static int FindFee(Conversation c, out string refusal)
		{
			refusal = null;
			Actor with = c.With;
			Actor you = c.You;
			if (Enemies(with, you))
			{
				refusal = Pick("Find them yourself.", "I don't help your kind.", "Ask someone who cares.");
				return 0;
			}
			if (c.Voice.Warmth <= -2)
			{
				refusal = Pick("Ask someone else.", "Why would I help you?");
				return 0;
			}
			bool flag = false;
			try
			{
				flag = !with.isAdult();
			}
			catch
			{
			}
			if (flag || c.Voice.Sovereign || c.Voice.Warmth >= 2)
			{
				return 0;
			}
			if (RelationTo(with, you).StartsWith("your ", StringComparison.Ordinal))
			{
				return 0;
			}
			if (c.Voice.Warmth >= 1 && PeopleMet.Knows(you, with))
			{
				return 0;
			}
			if (HasTrait(with, "greedy"))
			{
				return 3;
			}
			if (!(c.Voice.Manner == "kind") && !(c.Voice.Manner == "honest") && c.Voice.Warmth < 1)
			{
				return 2;
			}
			return 1;
		}

		private static void FindMenu(Conversation c)
		{
			c.LastTopic = "finding";
			Actor with = c.With;
			Actor you = c.You;
			string refusal;
			int fee = FindFee(c, out refusal);
			if (refusal != null)
			{
				c.Say(refusal);
				c.Offer(BackOnly(c));
				return;
			}
			c.Say((fee > 0) ? Pick($"Who are you after? It'll cost you {fee} gold.", $"I might know. {fee} gold and I'll point the way.") : Pick("Who are you after?", "Who do you need?", "I know most people here. Who is it?"));
			City city = with.city;
			List<Choice> list = new List<Choice>();
			FindRole[] roles = Finding.Roles;
			foreach (FindRole findRole in roles)
			{
				Actor who = Finding.Holder(city, findRole, with.current_position);
				if (who != null && who != you)
				{
					FindRole role = findRole;
					list.Add(FeeChoice(c, Finding.Asked(findRole), fee, delegate
					{
						FindAnswer(c, who, Finding.Word(role, who), fee);
					}));
				}
			}
			list.Add(new Choice("Someone by name...", delegate
			{
				FindGroups(c, fee);
			}));
			list.Add(Back(c));
			c.Offer(list);
		}

		private static Choice FeeChoice(Conversation c, string label, int fee, Action act)
		{
			Choice choice = new Choice((fee > 0) ? $"{label} ({fee} gold)" : label, act);
			int num = 0;
			try
			{
				num = c.You.money;
			}
			catch
			{
			}
			if (fee > num)
			{
				choice.Off($"You have only {num} gold.");
			}
			return choice;
		}

		private static void FindGroups(Conversation c, int fee)
		{
			List<Actor> list = Finding.Residents(c.With.city, c.You, c.With);
			int[] array = new int[Finding.Groups.Length];
			foreach (Actor item in list)
			{
				array[Finding.GroupOf(NameOf(item))]++;
			}
			List<Choice> list2 = new List<Choice>();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] != 0)
				{
					int group = i;
					list2.Add(new Choice($"{Finding.GroupLabel(i)} ({array[i]})", delegate
					{
						FindNames(c, fee, group, 0);
					}));
				}
			}
			if (list2.Count == 0)
			{
				c.Say(Pick("There's no one else here.", "It's only me in this town."));
				c.Offer(BackOnly(c));
				return;
			}
			list2.Add(new Choice("Someone who does a job...", delegate
			{
				FindMenu(c);
			}, ChoiceTone.Back));
			list2.Add(Back(c));
			c.Say(Pick("What's their name?", "Their name starts with...?"));
			c.Offer(list2);
		}

		private static void FindNames(Conversation c, int fee, int group, int page)
		{
			List<Actor> list = Finding.Residents(c.With.city, c.You, c.With);
			List<Actor> list2 = new List<Actor>();
			foreach (Actor item in list)
			{
				if (Finding.GroupOf(NameOf(item)) == group)
				{
					list2.Add(item);
				}
			}
			List<Choice> list3 = new List<Choice>();
			int num = page * 8;
			for (int i = num; i < list2.Count && i < num + 8; i++)
			{
				Actor who = list2[i];
				string role = Finding.RoleOf(who);
				list3.Add(FeeChoice(c, (role != null) ? (NameOf(who) + ", " + role) : NameOf(who), fee, delegate
				{
					FindAnswer(c, who, role, fee);
				}));
			}
			if (num + 8 < list2.Count)
			{
				list3.Add(new Choice("More names...", delegate
				{
					FindNames(c, fee, group, page + 1);
				}, ChoiceTone.Back));
			}
			list3.Add(new Choice("Other letters", delegate
			{
				FindGroups(c, fee);
			}, ChoiceTone.Back));
			list3.Add(Back(c));
			if (page == 0)
			{
				c.Say(Pick("Let me think...", "From that end of the alphabet..."));
			}
			c.Offer(list3);
		}

		private static void FindAnswer(Conversation c, Actor who, string role, int fee)
		{
			Actor with = c.With;
			Actor you = c.You;
			c.LastTopic = "finding";
			bool flag = false;
			try
			{
				flag = who?.isAlive() ?? false;
			}
			catch
			{
			}
			if (!flag)
			{
				c.Say(Pick("Oh... they've passed on, I'm afraid.", "They're gone. You've come too late."));
				c.Offer(FindAgain(c));
				return;
			}
			if (fee > 0)
			{
				try
				{
					if (you.money < fee)
					{
						c.Say("You can't pay that.");
						c.Offer(BackOnly(c));
						return;
					}
					you.spendMoney(fee);
					with.addMoney(fee);
				}
				catch (Exception ex)
				{
					Log.Error("finding: tip: " + ex.Message);
				}
			}
			string text = NameOf(who);
			string text2 = ((fee > 0) ? "*pockets the coin* " : "");
			string text3;
			if (who == with)
			{
				text3 = Pick("That's me. You've found them.", "You're talking to them.");
			}
			else if (c.Voice.Mute)
			{
				string text4 = Finding.Whereabouts(who, with.current_position);
				text3 = ((text4 == "right over there") ? ("*points at " + text + "*") : ("*points " + text4.Split(' ')[0] + "*"));
			}
			else
			{
				string s = Finding.Whereabouts(who, with.current_position);
				string text5 = ((role != null) ? (text + ", the " + role) : text);
				bool flag2 = false;
				try
				{
					flag2 = !with.isAdult();
				}
				catch
				{
				}
				text3 = (flag2 ? Pick("I know " + text + "! " + Cap(s) + "!", text + "? " + Cap(s) + "!") : Pick(text5 + "? " + Cap(s) + ".", "You want " + text5 + ". " + Cap(s) + ".", text5 + "... " + Cap(s) + ". You can't miss them."));
			}
			c.Say(text2 + text3);
			if (who != with)
			{
				Finding.Mark(you, who);
			}
			Log.Info(string.Format("finding: asked {0} for {1} ({2}, {3} gold)", WorldBoxMod.Describe(with), WorldBoxMod.Describe(who), role ?? "by name", fee));
			c.Offer(FindAgain(c));
		}

		private static List<Choice> FindAgain(Conversation c)
		{
			return new List<Choice>
			{
				new Choice("I'm looking for someone else.", delegate
				{
					FindMenu(c);
				}),
				Back(c),
				Goodbye(c)
			};
		}

		private static void FlirtChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			if (with.lover == you)
			{
				list.Add(new Choice("It's over between us", delegate
				{
					EndItMenu(c);
				}, ChoiceTone.Risk));
			}
			else if (CanFlirt(you, with))
			{
				list.Add(new Choice("Flirt", delegate
				{
					FlirtMenu(c, null);
				}, ChoiceTone.Romance));
				if (Romance.WouldSayYes(with, you))
				{
					Quests.CourtChoices(c, Romance.Of(with, you), list);
				}
			}
		}

		private static void FlirtMenu(Conversation c, string opener)
		{
			c.LastTopic = "flirt";
			Actor with = c.With;
			Actor you = c.You;
			FlirtOutcome? flirtOutcome = Romance.Blocker(you, with);
			if (flirtOutcome.HasValue)
			{
				c.Say(Flirt(with, you, flirtOutcome.Value));
				c.Romantic = flirtOutcome.Value == FlirtOutcome.AlreadyYours;
				c.Offer(Topics(c));
			}
			else
			{
				c.Say(opener ?? FlirtOpener(with, you, Romance.Of(with, you)));
				c.Relation = Romance.WithStanding(RelationTo(with, you), with, you);
				c.Offer(FlirtMenuChoices(c));
			}
		}

		private static string FlirtOpener(Actor npc, Actor you, Suitor s)
		{
			if (Romance.Taken(npc, you))
			{
				return TakenOpener(npc, s);
			}
			if (s.Affection >= 100)
			{
				return Pick("*grins* You again. I was hoping.", "*leans in* Go on, then.");
			}
			if (s.Affection >= 20)
			{
				return Pick("*leans in a little* I'm listening.", "*smiles* Yes?");
			}
			if (s.Affection >= 8)
			{
				return Pick("*smiles* Yes?", "Oh? What is it?");
			}
			return Pick("*raises an eyebrow* Oh?", "Hm? Something on your mind?");
		}

		private static string TakenOpener(Actor npc, Suitor s)
		{
			string text = NameOf(npc.lover);
			int num = Romance.Devotion(npc);
			if (s.Affection >= 100)
			{
				return Pick("*keeps their voice low* Careful. " + text + " has eyes everywhere.", "*glances over your shoulder* Quickly, before someone sees.");
			}
			if (num >= 75)
			{
				return Pick("*steps back* You know I'm with " + text + ".", "I love " + text + ". Whatever this is, it's going nowhere.");
			}
			if (num < 25)
			{
				return Pick("*sighs* " + text + " hasn't looked at me like that in years.", "*glances away* Don't tell " + text + " I'm still listening.");
			}
			return Pick("*looks around* " + text + " could walk by any moment.", "I'm with " + text + ". You know that. *doesn't leave*");
		}

		private static void EndItMenu(Conversation c)
		{
			c.LastTopic = "end it";
			c.Say(Pick("*goes still* What are you saying?", "*searches your face* What do you mean?"));
			c.Offer(new List<Choice>
			{
				new Choice("It's over. I'm sorry.", delegate
				{
					EndItNow(c);
				}, ChoiceTone.Risk),
				new Choice("Nothing. Forget I said it.", delegate
				{
					c.LastTopic = "end it not";
					c.Say(Pick("*breathes out* Don't scare me like that.", "*laughs, unsure* All right."));
					c.Offer(Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void EndItNow(Conversation c)
		{
			c.LastTopic = "end it done";
			Actor with = c.With;
			Actor you = c.You;
			bool flag = false;
			try
			{
				flag = with.hasTrait("hotheaded") || with.hasTrait("bloodlust");
			}
			catch
			{
			}
			Romance.EndIt(you, with);
			c.Relation = RelationTo(with, you);
			c.EndAfter = true;
			c.Think(0.8f);
			c.Say(flag ? Pick("Get out of my sight.", "*throws your hand off* Then go. Now.") : Pick("*voice breaks* Then go.", "*turns away* I thought... never mind what I thought.", "*very quiet* I see."));
			c.Toast?.Invoke("You and " + NameOf(with) + " are no longer together");
		}

		public static string FlirtReply(Actor npc, Actor you, FlirtOutcome outcome, Charm charm, Charm likes)
		{
			switch (outcome)
			{
			case FlirtOutcome.Warm:
				switch (charm)
				{
				case Charm.Sweet:
					return Pick("*cheeks go pink* You noticed that?", "*smiles at the ground* That's a lovely thing to say.", "*touches your arm* You're sweet. Has anyone told you?");
				case Charm.Bold:
					return Pick("*laughs* Straight to it, then. I like that.", "*holds your gaze* Careful. I might say yes.", "Bold. Very bold. Keep going.");
				case Charm.Witty:
					return Pick("*bursts out laughing* That's terrible. Say another.", "*snorts* Oh, you're trouble.", "Ha! All right, that one got me.");
				default:
					return Pick("*goes quiet* I didn't expect that from you.", "*looks at you a long moment* You mean that, don't you?", "No one's asked me that in years.");
				}
			case FlirtOutcome.Mild:
				return Pick("*smiles* That's nice of you.", "Ha. Not bad.", "Hm. Go on, then.", "*tilts head* You're an odd one. I don't mind it.");
			case FlirtOutcome.Cool:
				switch (likes)
				{
				case Charm.Sweet:
					return Pick("Easy. You barely know me.", "*steps back* A little gentler, maybe?");
				case Charm.Bold:
					return Pick("*yawns* Is that all you've got?", "Say what you mean. I don't bite.");
				case Charm.Witty:
					return Pick("So serious. Do you ever laugh?", "*sighs* Lighten up a little.");
				default:
					return Pick("Clever. Do you ever say anything you mean?", "Pretty words. Where's the real you?");
				}
			case FlirtOutcome.Bored:
				return Pick("You've said that already.", "Do you know any other lines?", "*rolls eyes* Again?");
			case FlirtOutcome.Moody:
				return Pick("*sighs* Not today. I've too much on my mind.", "I'm not in the mood. Sorry.");
			default:
				return Flirt(npc, you, outcome);
			}
		}

		public static string ReadyLine(Actor npc)
		{
			return Pick("*quietly* You keep coming back to me. If you're serious, ask to court me properly.", "*bites lip* If you mean any of this... ask me properly.");
		}

		public static string TasteLine(Charm likes, string favourite)
		{
			string text;
			switch (likes)
			{
			case Charm.Sweet:
				text = "Kindness. Someone gentle, who notices the little things.";
				break;
			case Charm.Bold:
				text = "Someone who says what they want. I can't stand dithering.";
				break;
			case Charm.Witty:
				text = "Make me laugh and you're halfway there.";
				break;
			default:
				text = "Someone who means what they say. Honest words, not clever ones.";
				break;
			}
			if (favourite == null)
			{
				return text;
			}
			string text2 = Work.ResourceName(favourite).ToLowerInvariant();
			return text + " And " + text2 + ". I'd do anything for " + text2 + ".";
		}

		private static void ForgeMenu(Conversation c, string said = null)
		{
			c.LastTopic = "trade_forge";
			City city = c.With.city;
			float markup = Economy.Markup(c);
			List<Traders.Ware> list = Traders.Wares(city);
			List<Choice> list2 = new List<Choice>();
			foreach (Traders.Ware item in list)
			{
				Traders.Ware ware = item;
				int price = Traders.Price(ware.Asset, markup);
				list2.Add(Priced(c, An(ware.Name), price, delegate
				{
					BuyWare(c, ware, price);
				}, ChoiceTone.Quest));
			}
			if (said != null)
			{
				c.Say(said);
			}
			else if (list2.Count == 0)
			{
				c.Say(Pick("I've forged all I can today. Come back after dawn.", "The forge is cold till morning."));
			}
			else
			{
				string text = Pick("Fresh from the forge, today's work.", "What " + city.name + "'s forge can make. Pick one.", "Made this morning. Still warm, some of it.");
				int num = Traders.Grade(city);
				if (num >= 4)
				{
					c.Say(text, "Mythril, even, for those who can pay.");
				}
				else if (num <= 2)
				{
					c.Say(text, "Copper and bronze here. For iron, go to a bigger town, or one with a mine.");
				}
				else
				{
					c.Say(text);
				}
			}
			list2.Add(new Choice("Something else.", delegate
			{
				c.Say("What else?");
				c.Offer(TradeMenu(c));
			}, ChoiceTone.Back));
			c.Offer(list2);
		}

		private static void GearSaleChoice(Conversation c, List<Choice> list)
		{
			if (Gear.Count(c.You) != 0 && c.With.city != null)
			{
				list.Add(new Choice("I have gear to sell.", delegate
				{
					GearSaleMenu(c);
				}));
			}
		}

		private static int GearPrice(Conversation c, Gear.Piece p)
		{
			int num = Traders.Worth(p.Asset) + Traders.ModWorth(p.Asset, p.Mods);
			return Mathf.Max(3, Mathf.RoundToInt(0.4f * (float)num / Mathf.Max(1f, Economy.Markup(c))));
		}

		private static int PieceIndex(Actor you, Gear.Piece want)
		{
			List<Gear.Piece> list = Gear.Of(you);
			for (int i = 0; i < list.Count; i++)
			{
				Gear.Piece piece = list[i];
				if (piece == want)
				{
					return i;
				}
				if (piece.Id == want.Id && piece.Name == want.Name && piece.By == want.By && piece.Kills == want.Kills && string.Join("+", piece.Mods) == string.Join("+", want.Mods))
				{
					return i;
				}
			}
			return -1;
		}

		private static void GearSaleMenu(Conversation c, string said = null)
		{
			c.LastTopic = "trade_gear";
			List<Gear.Piece> list = Gear.Of(c.You);
			List<Choice> list2 = new List<Choice>();
			for (int i = 0; i < list.Count; i++)
			{
				if (list2.Count >= 8)
				{
					break;
				}
				Gear.Piece p = list[i];
				if (p.Asset != null)
				{
					int n = GearPrice(c, p);
					list2.Add(new Choice("Sell " + An(Gear.NameOf(p).ToLowerInvariant()) + " (" + Gold(n) + ")", delegate
					{
						SellPiece(c, p);
					}, ChoiceTone.Quest));
				}
			}
			c.Say(said ?? ((list2.Count == 0) ? "You've nothing I'd buy." : Pick("Let's see what you've got.", "Show me. I'll be fair.", "Hm. Some of this is worth something.")));
			list2.Add(new Choice("Something else.", delegate
			{
				c.Say("What else?");
				c.Offer(TradeMenu(c));
			}, ChoiceTone.Back));
			c.Offer(list2);
		}

		private static void SellPiece(Conversation c, Gear.Piece chosen)
		{
			Actor you = c.You;
			Actor with = c.With;
			City city = with.city;
			if (city == null)
			{
				c.Say("*shakes head* Not now.");
				c.Offer(Topics(c));
				return;
			}
			int num = PieceIndex(you, chosen);
			if (num < 0)
			{
				GearSaleMenu(c);
				return;
			}
			Gear.Piece piece = Gear.Of(you)[num];
			int num2 = GearPrice(c, piece);
			if (Economy.CanPay(city, with) < num2)
			{
				GearSaleMenu(c, "*counts their coins* I can't pay that much now.");
				return;
			}
			Item item = null;
			try
			{
				item = Gear.Build(piece, with);
			}
			catch
			{
			}
			if (item == null)
			{
				GearSaleMenu(c, "*turns it over* No. I can't use this.");
				return;
			}
			Gear.Remove(you, num);
			BagOrder.PieceGone(you, num);
			bool flag = false;
			try
			{
				city.tryToPutItem(item);
				flag = city.getEquipmentList(item.getAsset().equipment_type)?.Contains(item.data.id) ?? false;
			}
			catch
			{
			}
			if (!flag)
			{
				Gear.LetGo(item);
			}
			int num3 = Economy.PayOut(you, num2, city, with);
			string name = city.name;
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			c.Toast?.Invoke("Sold " + An(Gear.NameOf(piece).ToLowerInvariant()) + " for " + Gold(num3) + "." + (flag ? (" It's in " + name + "'s armory now.") : ""));
			Log.Info($"trade: sold {piece.Id} ({piece.Mods.Length} enchantments) for {num3} in {name}");
			GearSaleMenu(c, Pick("Done.", "Fair trade.", "It'll find a new owner soon enough."));
		}

		private static void BagChoice(Conversation c, List<Choice> list)
		{
			Actor you = c.You;
			if (!Bag.Next(you, out var tier, out var price))
			{
				return;
			}
			price = Mathf.RoundToInt((float)price * Economy.Markup(c));
			list.Add(Priced(c, $"A bigger bag: a {Bag.Names[tier].ToLowerInvariant()}, {Bag.Slots[tier]} slots", price, delegate
			{
				if (!Economy.Pay(you, price, c.With.city, c.With))
				{
					c.Say("You can't afford that.");
					c.Offer(TradeMenu(c));
				}
				else
				{
					string text = Bag.Name(you).ToLowerInvariant();
					Bag.SetTier(you, tier);
					Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
					c.Toast?.Invoke($"Bought a {Bag.Names[tier].ToLowerInvariant()} for {Gold(price)}: {Bag.Slots[tier]} slots. Your {text}'s things are in it.");
					Log.Info($"trade: bag {tier} ({Bag.Names[tier]}) for {price}");
					c.Say(Pick("Good leather, double stitched. It'll outlast you.", "Room for everything, and then some.", "Swap it over. There, that's better."));
					c.Offer(TradeMenu(c));
				}
			}, ChoiceTone.Quest));
		}

		private static void ReworkChoice(Conversation c, List<Choice> list)
		{
			Actor you = c.You;
			string[] array = new string[2] { "tw_item_shovel", "tw_item_pickaxe" };
			foreach (string text in array)
			{
				int num = 0;
				int num2 = 4;
				while (num2 >= 1 && num == 0)
				{
					if (FirstPerson.Supplies.Count(you, FirstPerson.Supplies.Leveled(text, num2)) > 0)
					{
						num = num2;
					}
					num2--;
				}
				if (num == 0 || num >= 4)
				{
					continue;
				}
				string from = FirstPerson.Supplies.Leveled(text, num);
				string to = FirstPerson.Supplies.Leveled(text, num + 1);
				string name = ((text == "tw_item_shovel") ? "shovel" : "pickaxe");
				int price = Mathf.Max(1, Mathf.RoundToInt((float)ReworkPrice[num + 1] * Economy.Markup(c)));
				int next = num + 1;
				list.Add(Priced(c, $"Rework my {name}: level {next}", price, delegate
				{
					if (FirstPerson.Supplies.Count(you, from) <= 0)
					{
						c.Say("Where's the " + name + "?");
						c.Offer(TradeMenu(c));
					}
					else if (!Economy.Pay(you, price, c.With.city, c.With))
					{
						c.Say("You can't afford that.");
						c.Offer(TradeMenu(c));
					}
					else
					{
						Rework(you, from, to);
						Sounds.PlayAt("event:/SFX/HIT/HitMetal", you.current_position);
						c.Toast?.Invoke($"Your {name} is level {next} now, for {Gold(price)}: it digs quicker.");
						Log.Info($"trade: {name} reworked to level {next} for {price}");
						c.Say(Pick("A new edge, a better socket. It'll bite the ground now.", "Balanced and sharpened. Mind your feet.", "There. Dig a well with that."));
						c.Offer(TradeMenu(c));
					}
				}, ChoiceTone.Quest));
			}
		}

		internal static void Rework(Actor you, string from, string to)
		{
			int num = BagOrder.Of(you).IndexOf(BagOrder.ResourceKey(from));
			if (FirstPerson.Supplies.Take(you, from, 1))
			{
				if (num >= 0)
				{
					BagOrder.Set(you, num, BagOrder.ResourceKey(to));
				}
				FirstPerson.Supplies.Give(you, to, 1);
			}
		}

		private static void BuyWare(Conversation c, Traders.Ware ware, int price)
		{
			Actor you = c.You;
			City city = c.With.city;
			if (!Traders.Buy(you, city, ware, price, c.With, out var made, out var note))
			{
				ForgeMenu(c, note);
				return;
			}
			Sounds.PlayAt("event:/SFX/HIT/HitMetal", you.current_position);
			c.Toast?.Invoke("Bought " + An(Economy.ItemName(made)) + ", fresh from the forge, for " + Gold(price) + ", and put it on." + note);
			Log.Info($"trade: forged {ware.Asset.id} ({made.getQuality()}) for {price} in {city.name}");
			ForgeMenu(c, Pick("Mind the edge.", "Good work, if I say so myself.", "Wear it in. It'll fit you better in a week."));
		}

		private static void IntrigueChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			bool flag = false;
			try
			{
				flag = with.isKing() && with.kingdom != null && with.kingdom.isCiv() && with.kingdom != you.kingdom;
			}
			catch
			{
			}
			if (flag)
			{
				list.Add(new Choice("I'd pay you to make war on another realm.", delegate
				{
					WarOffer(c);
				}, ChoiceTone.Risk));
			}
		}

		public static int WarPrice(Actor king, Kingdom target, Voice voice, out string why)
		{
			why = null;
			Kingdom kingdom = king.kingdom;
			if (HasTrait(king, "pacifist"))
			{
				why = "No gold buys a war from a pacifist.";
				return 0;
			}
			if (Royal.AtWar(kingdom, target))
			{
				why = "Already at war with them.";
				return 0;
			}
			if (Royal.Allied(kingdom, target))
			{
				why = "They're our allies.";
				return 0;
			}
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			try
			{
				foreach (Actor item in Civil.SubjectsOf(target))
				{
					num++;
					if (item.isWarrior())
					{
						num2++;
					}
				}
			}
			catch
			{
			}
			try
			{
				foreach (Actor item2 in Civil.SubjectsOf(kingdom))
				{
					if (item2.isWarrior())
					{
						num3++;
					}
				}
			}
			catch
			{
			}
			float num4 = 800f + (float)num * 4f + (float)num2 * 15f;
			if (num3 * 3 < num2)
			{
				why = "Their army is three times ours. Suicide.";
				return 0;
			}
			if (num3 < num2)
			{
				num4 *= 1.5f;
			}
			if (HasTrait(king, "bloodlust") || HasTrait(king, "ambitious"))
			{
				num4 *= 0.8f;
			}
			if (HasTrait(king, "greedy"))
			{
				num4 *= 0.85f;
			}
			if (HasTrait(king, "peaceful"))
			{
				num4 *= 1.5f;
			}
			num4 *= 1f - 0.08f * (float)voice.Warmth;
			return Mathf.Max(800, Mathf.RoundToInt(num4 / 50f) * 50);
		}

		private static void WarOffer(Conversation c)
		{
			c.LastTopic = "war_gold";
			Actor with = c.With;
			Actor you = c.You;
			Kingdom home = with.kingdom;
			string text = (Models.IsFemale(with) ? "queen" : "king");
			with.data.get("fp_war_gold", out var pResult, -99);
			if (Chronicle.Year - pResult < 5)
			{
				c.Say(Pick("I've taken gold for one war already. My people bleed enough.", "Not again. Not so soon. Ask me in a few years."));
				c.Offer(Topics(c));
				return;
			}
			if (HasTrait(with, "pacifist"))
			{
				Chronicle.ChangeOpinion(with, you, -10);
				c.Say(Pick("You'd buy the blood of my people? Get out of my sight.", "No gold buys a war from me."));
				c.Offer(Topics(c));
				return;
			}
			List<Choice> list = new List<Choice>();
			List<Kingdom> list2 = new List<Kingdom>();
			try
			{
				foreach (Kingdom item2 in World.world.kingdoms.list)
				{
					if (item2 != null && item2 != home && item2.isAlive() && item2.isCiv() && item2 != you.kingdom)
					{
						list2.Add(item2);
					}
				}
			}
			catch
			{
			}
			list2.Sort((Kingdom a, Kingdom b) => Distance(home, a).CompareTo(Distance(home, b)));
			foreach (Kingdom item3 in list2)
			{
				Kingdom target = item3;
				string why;
				int price = WarPrice(with, target, c.Voice, out why);
				Choice item = ((price > 0) ? Priced(c, "March on " + Royal.NameOf(target), price, delegate
				{
					BuyWar(c, target, price);
				}, ChoiceTone.Risk) : new Choice("March on " + Royal.NameOf(target), null, ChoiceTone.Risk).Off(why));
				list.Add(item);
				if (list.Count >= 6)
				{
					break;
				}
			}
			if (list.Count == 0)
			{
				c.Say("War on whom? There's no one left to fight.");
			}
			else
			{
				c.Say((c.Voice.Manner == "greedy" || c.Voice.Manner == "bloodlust") ? Pick("Now you're speaking my language. Who, and how much?", "*leans forward* Go on.") : Pick("*lowers " + ((text == "queen") ? "her" : "his") + " voice* A dangerous thing to ask a " + text + ". Name the realm. And the price had better be worth the blood.", "War is costly. Who would you have me fight?"));
			}
			list.Add(Back(c));
			c.Offer(list);
		}

		private static float Distance(Kingdom a, Kingdom b)
		{
			try
			{
				return Kingdom.distanceBetweenKingdom(a, b);
			}
			catch
			{
				return 0f;
			}
		}

		private static void BuyWar(Conversation c, Kingdom target, int price)
		{
			c.LastTopic = "war_bought";
			Actor with = c.With;
			Actor you = c.You;
			Kingdom kingdom = with.kingdom;
			if (Economy.Money(you) < price)
			{
				c.Say("You can't pay for a war with promises.");
				c.Offer(Topics(c));
				return;
			}
			if (!target.isAlive() || Royal.AtWar(kingdom, target))
			{
				c.Say("That's settled already.");
				c.Offer(Topics(c));
				return;
			}
			try
			{
				try
				{
					World.world.diplomacy.startWar(kingdom, target, WarTypeLibrary.normal, pLog: true);
				}
				catch (NullReferenceException)
				{
					if (!Royal.AtWar(kingdom, target))
					{
						World.world.diplomacy.startWar(kingdom, target, WarTypeLibrary.normal, pLog: false);
					}
				}
			}
			catch (Exception ex2)
			{
				Log.Error("intrigue: war: " + ex2);
			}
			if (!Royal.AtWar(kingdom, target))
			{
				c.Say("Something's wrong. The heralds won't ride. Keep your gold.");
				c.Offer(Topics(c));
				return;
			}
			try
			{
				you.spendMoney(price);
				with.addMoney(price / 2);
				if (!Economy.Deposit(kingdom.capital, price - price / 2))
				{
					with.addMoney(price - price / 2);
				}
			}
			catch (Exception ex3)
			{
				Log.Error("intrigue: pay: " + ex3.Message);
			}
			with.data.set("fp_war_gold", Chronicle.Year);
			Chronicle.ChangeOpinion(with, you, 15);
			c.Voice = VoiceOf(with, you);
			Log.Info($"intrigue: {WorldBoxMod.Describe(with)} of {Royal.NameOf(kingdom)} paid {price} to march on {Royal.NameOf(target)}");
			c.Toast?.Invoke($"{Royal.NameOf(kingdom)} declares war on {Royal.NameOf(target)}! ({price} gold)");
			if (PlotRng.NextDouble() < 0.25)
			{
				int num = Law.BountyOn(target, you) + price / 4;
				Law.SetBounty(target, you, num);
				c.Toast?.Invoke($"Word of your gold reached {Royal.NameOf(target)}: {num} gold on your head there");
				Log.Info($"intrigue: the plot leaked, bounty {num} in {Royal.NameOf(target)}");
			}
			c.Say(Pick("*pockets the gold* The banners of " + Royal.NameOf(kingdom) + " will fly over " + Royal.NameOf(target) + " before long.", "Consider it done. " + Royal.NameOf(target) + " won't know what hit them.", "*calls for the war horns* You have your war. We never spoke."));
			c.Offer(Topics(c));
		}

		public static string GameLine(string kind)
		{
			if (!DialogCounts.TryGetValue(kind, out var i))
			{
				i = 0;
				try
				{
					for (; i < 24; i++)
					{
						if (!LocalizedTextManager.stringExists("happiness_dialog_" + kind + "_" + i))
						{
							break;
						}
					}
				}
				catch
				{
				}
				DialogCounts[kind] = i;
			}
			if (i == 0)
			{
				return null;
			}
			try
			{
				string text = LocalizedTextManager.getText("happiness_dialog_" + kind + "_" + Rng.Next(i));
				return (string.IsNullOrEmpty(text) || text.StartsWith("happiness_dialog")) ? null : text;
			}
			catch
			{
				return null;
			}
		}

		public static string MomentOf(Memory m)
		{
			string other = m.Other;
			switch (m.Kind)
			{
			case "death_lover":
				if (other == null)
				{
					return "when I lost my love";
				}
				return "when " + other + " died";
			case "death_child":
				if (other == null)
				{
					return "when we lost our child";
				}
				return "when we lost little " + other;
			case "death_family_member":
				if (other == null)
				{
					return "when we had a death in the family";
				}
				return "when " + other + " passed";
			case "death_best_friend":
				if (other == null)
				{
					return "when my best friend died";
				}
				return "when my friend " + other + " died";
			case "fallen_in_love":
				if (other == null)
				{
					return "when I fell in love";
				}
				return "when I fell for " + other;
			case "just_had_child":
				if (other == null)
				{
					return "when my child was born";
				}
				return "when " + other + " was born";
			case "become_king":
				if (other == null)
				{
					return "when I was crowned";
				}
				return "when I was crowned in " + other;
			case "become_leader":
				if (other == null)
				{
					return "when I came to lead the town";
				}
				return "when " + other + " chose me to lead";
			case "lost_crown":
				return "when I lost the crown";
			case "just_won_war":
				return "when we won the war";
			case "just_lost_war":
				return "when we lost the war";
			case "just_started_war":
				return "when the war began";
			case "just_made_peace":
				return "when peace came";
			case "was_conquered":
				if (other == null)
				{
					return "when we were conquered";
				}
				return "when " + other + " took us";
			case "kingdom_fell_apart":
				return "when the kingdom fell apart";
			case "just_rebelled":
				return "when we rose up";
			case "just_lost_house":
				return "when I lost my home";
			case "just_found_house":
				if (other == null)
				{
					return "when I found a home";
				}
				return "when I found a home in " + other;
			case "got_robbed":
				return "when I was robbed";
			case "got_caught":
				return "when they caught me";
			case "just_injured":
				return "when I was hurt";
			case "just_killed":
				return "the first time I killed";
			case "lost_fight":
				return "when I lost a fight";
			case "wrote_book":
				return "when I wrote my book";
			case "had_nightmare":
				return "that nightmare";
			case "had_bad_dream":
				return "a bad dream I had";
			case "just_made_friend":
				if (other == null)
				{
					return "when I made a friend";
				}
				return "when I met " + other;
			case "just_became_adult":
				return "when I came of age";
			case "conquered_city":
				return "when we took a city";
			case "destroyed_city":
			case "razed_city":
				return "when we burned a city";
			case "lost_city":
				return "when we lost a city";
			case "razed_capital":
			case "lost_capital":
				return "when our capital fell";
			case "just_cursed":
				return "when I was cursed";
			case "just_felt_the_divine":
				return "when I felt the touch of the gods";
			case "just_enchanted":
				return "when the magic touched me";
			case "just_got_out_of_egg":
				return "when I hatched";
			case "just_finished_plot":
				return "when our plan came off";
			case "become_alpha":
				return "when I became the alpha";
			case "fp_quest":
				if (other == null)
				{
					return "when a stranger helped me";
				}
				return "when a stranger helped me with " + other;
			case "fp_named":
				if (other == null)
				{
					return "when we named our child";
				}
				return "when we named our child " + other;
			case "fp_adopted":
				if (other == null)
				{
					return "when we took in an orphan";
				}
				return "when we took in " + other;
			case "fp_wedding":
				if (other == null)
				{
					return "my wedding day";
				}
				return "when " + other + " and I were wed";
			case "fp_courted":
				if (other == null)
				{
					return "when I was courted";
				}
				return "when " + other + " won my heart";
			case "fp_left":
				if (other == null)
				{
					return "when I left my love";
				}
				return "when I left " + other;
			case "fp_jilted":
				if (other == null)
				{
					return "when my love left me";
				}
				return "when " + other + " took my love from me";
			case "fp_ended":
				if (other == null)
				{
					return "when my love left me";
				}
				return "when " + other + " left me";
			case "fp_crime":
				if (other == null)
				{
					return "when that criminal attacked us";
				}
				return "when " + other + " attacked us";
			case "fp_student":
				if (other == null)
				{
					return "when I took a student";
				}
				return "when I taught " + other + " what I know";
			case "fp_teacher":
				if (other == null)
				{
					return "when I learned from a master";
				}
				return "when " + other + " taught me";
			case "fp_boon":
				if (other == null)
				{
					return "when I honoured a loyal blade";
				}
				return "when I honoured " + other + "'s blade";
			case "fp_enchanted":
				if (other == null)
				{
					return "when my weapon was enchanted";
				}
				return "when " + other + " enchanted my weapon";
			case "fp_immortal":
				if (other == null)
				{
					return "when death let go of me";
				}
				return "when we destroyed " + other + " and death let go of me";
			default:
				return null;
			}
		}

		private static string WhenAgo(Memory m)
		{
			int num = Chronicle.Year - m.Year;
			if (num > 0)
			{
				if (num != 1)
				{
					return num + " years ago";
				}
				return "last year";
			}
			return "this year";
		}

		public static string TellMemory(Memory m)
		{
			string text = GameLine(m.Kind);
			string text2 = MomentOf(m);
			if (text2 == null)
			{
				return text;
			}
			string text3 = Cap(text2) + ", " + WhenAgo(m) + ".";
			if (text == null)
			{
				return text3;
			}
			return "\"" + text + "\" " + text3;
		}

		private static void Life(Conversation c)
		{
			c.LastTopic = "life";
			Actor with = c.With;
			Actor you = c.You;
			List<string> list = new List<string>();
			int num = 0;
			try
			{
				num = with.getAge();
			}
			catch
			{
			}
			string text = JobName(with);
			list.Add((num > 0) ? ($"I'm {num} years old" + ((text != null) ? (", a " + text) : "") + ((num > 60) ? ". I've seen a lot." : ((num < 18) ? ". Still young!" : "."))) : "Not much to tell.");
			Record record = Chronicle.Of(with);
			if (c.Voice.Forgetful)
			{
				list.Add(Pick("Remember? I don't remember much. Today is today.", "The past slips away from me like water."));
			}
			else if (record != null && record.Memories.Count > 0)
			{
				List<Memory> list2 = new List<Memory>();
				string[] weighty = Weighty;
				foreach (string text2 in weighty)
				{
					int num2 = record.Memories.Count - 1;
					while (num2 >= 0 && list2.Count < 2)
					{
						if (record.Memories[num2].Kind == text2 && !list2.Contains(record.Memories[num2]))
						{
							list2.Add(record.Memories[num2]);
						}
						num2--;
					}
				}
				int num3 = record.Memories.Count - 1;
				while (num3 >= 0 && list2.Count < 3)
				{
					if (!list2.Contains(record.Memories[num3]))
					{
						list2.Add(record.Memories[num3]);
					}
					num3--;
				}
				foreach (Memory item in list2)
				{
					string text3 = TellMemory(item);
					if (text3 != null)
					{
						list.Add(text3);
					}
				}
			}
			else
			{
				list.Add(Pick("Nothing much has happened to me. Yet.", "My life's been quiet. I like it that way."));
			}
			string text4 = Scars(with, c.Voice);
			if (text4 != null)
			{
				list.Add(text4);
			}
			c.Say(list.ToArray());
			List<Choice> list3 = new List<Choice>();
			if (!c.Voice.Forgetful && record != null && record.Memories.Count > 1)
			{
				list3.Add(new Choice("What's your oldest memory?", delegate
				{
					Oldest(c);
				}));
			}
			if (Rulers(with).Count > 0)
			{
				list3.Add(new Choice("Which rulers have you lived under?", delegate
				{
					RulersTalk(c);
				}));
			}
			list3.Add(new Choice("What makes you happy?", delegate
			{
				Happy(c);
			}));
			list3.AddRange(BackOnly(c));
			c.Offer(list3);
			Chronicle.ChangeOpinion(with, you, 1);
		}

		private static string Scars(Actor npc, Voice v)
		{
			List<string> list = new List<string>();
			if (HasTrait(npc, "eyepatch"))
			{
				list.Add(Pick("Lost this eye in a fight.", "The eyepatch? A souvenir."));
			}
			if (HasTrait(npc, "crippled"))
			{
				list.Add(Pick("My leg never healed right.", "I limp since the day I was hurt."));
			}
			if (HasTrait(npc, "skin_burns"))
			{
				list.Add(Pick("These burns? Fire doesn't forgive.", "I was caught in a fire once. The scars stay."));
			}
			int num = 0;
			try
			{
				num = npc.data.kills;
			}
			catch
			{
			}
			if (num > 0)
			{
				bool flag = v.Manner == "evil" || v.Manner == "bloodlust" || v.Manner == "psychopath" || v.Manner == "savage";
				list.Add(flag ? $"I've killed {num}. And counting." : ((num == 1) ? "I've taken a life. Once. I think of it often." : $"I've killed {num} in my time. It doesn't get easier."));
			}
			int num2 = 0;
			try
			{
				foreach (Book book in World.world.books)
				{
					if (book != null && book.data.author_id == npc.data.id)
					{
						num2++;
					}
				}
			}
			catch
			{
			}
			if (num2 > 0)
			{
				list.Add((num2 == 1) ? "I wrote a book, you know." : $"I've written {num2} books.");
			}
			if (list.Count != 0)
			{
				return string.Join(" ", list.ToArray());
			}
			return null;
		}

		private static void Oldest(Conversation c)
		{
			Record record = Chronicle.Of(c.With);
			Memory memory = ((record != null && record.Memories.Count > 0) ? record.Memories[0] : null);
			string text = ((memory != null) ? TellMemory(memory) : null);
			c.Say((text != null) ? ("The oldest thing I remember... " + text) : "I can't remember that far back.");
			c.Offer(BackOnly(c));
		}

		private static List<string> Rulers(Actor npc)
		{
			List<string> list = new List<string>();
			try
			{
				Kingdom kingdom = npc.kingdom;
				if (kingdom == null || !kingdom.isCiv())
				{
					return list;
				}
				double created_time = npc.data.created_time;
				if (kingdom.data.past_rulers != null)
				{
					foreach (LeaderEntry past_ruler in kingdom.data.past_rulers)
					{
						if (past_ruler != null && past_ruler.timestamp_end >= created_time && !string.IsNullOrEmpty(past_ruler.name))
						{
							list.Add(past_ruler.name);
						}
					}
				}
				if (kingdom.king != null && kingdom.king.isAlive())
				{
					list.Add(NameOf(kingdom.king) + " (now)");
				}
			}
			catch
			{
			}
			return list;
		}

		private static void RulersTalk(Conversation c)
		{
			List<string> list = Rulers(c.With);
			if (list.Count == 1)
			{
				c.Say("Only one: " + list[0] + ".");
			}
			else
			{
				string arg = string.Join(", ", list.GetRange(0, list.Count - 1).ToArray()) + " and " + list[list.Count - 1];
				c.Say($"I've lived under {list.Count} rulers: {arg}.", (list.Count > 3) ? "Kings come and go. The fields stay." : null);
			}
			c.Offer(BackOnly(c));
		}

		private static void Happy(Conversation c)
		{
			Actor with = c.With;
			string text = null;
			try
			{
				text = with.data.favorite_food;
			}
			catch
			{
			}
			int num = SafeHappiness(with);
			List<string> list = new List<string>();
			if (!string.IsNullOrEmpty(text))
			{
				list.Add(Pick("A plate of " + Work.ResourceName(text).ToLowerInvariant() + ". Nothing beats it.", "Give me " + Work.ResourceName(text).ToLowerInvariant() + " and I'm yours for the day."));
			}
			try
			{
				if (with.lover != null)
				{
					list.Add(NameOf(with.lover) + ", of course.");
				}
			}
			catch
			{
			}
			list.Add((num > 60) ? "Honestly? Life is good right now." : ((num < 0) ? "Happy? I can't remember the last time." : "A warm house and a quiet evening."));
			c.Say(list.ToArray());
			c.Offer(BackOnly(c));
		}

		private static List<KeyValuePair<Vector2, string>> WorldNews()
		{
			if (Time.unscaledTime - _logReadAt < 20f)
			{
				return RecentLog;
			}
			_logReadAt = Time.unscaledTime;
			RecentLog.Clear();
			try
			{
				int num = (int)World.world.getCurWorldTime();
				float num2 = 60f;
				using (ListPool<WorldLogMessage> listPool = DBGetter.getWorldLogMessages())
				{
					int num3 = 0;
					foreach (ref WorldLogMessage item in listPool)
					{
						WorldLogMessage current = item;
						if (num3++ > 60)
						{
							break;
						}
						if ((float)(num - current.timestamp) > num2 * 3f)
						{
							continue;
						}
						string text = null;
						text = FirstPerson.WorldNews.Format(current);
						if (string.IsNullOrEmpty(text))
						{
							continue;
						}
						text = Tags.Replace(text, "").Trim();
						Vector2 key = new Vector2(-9999f, -9999f);
						try
						{
							if (current.hasLocation())
							{
								Vector3 location = current.getLocation();
								key = new Vector2(location.x, location.y);
							}
						}
						catch
						{
						}
						RecentLog.Add(new KeyValuePair<Vector2, string>(key, text));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("talk: world log: " + ex.Message);
			}
			return RecentLog;
		}

		private static void News(Conversation c)
		{
			c.LastTopic = "news";
			Actor with = c.With;
			Actor you = c.You;
			List<string> list = new List<string>();
			string text = WarsLine(with);
			if (text != null)
			{
				list.Add(text);
			}
			Vector2 current_position = with.current_position;
			foreach (KeyValuePair<Vector2, string> item in WorldNews())
			{
				if (list.Count >= 3)
				{
					break;
				}
				if (!(item.Key.x > -9000f) || !((item.Key - current_position).sqrMagnitude > 8100f))
				{
					list.Add(Pick("Have you heard? ", "They say ", "Word is: ") + Lower(item.Value));
				}
			}
			string text2 = TownTroubles(with);
			if (text2 != null)
			{
				list.Add(text2);
			}
			if (c.Voice.Warmth >= 0)
			{
				string text3 = Gossip(with, you);
				if (text3 != null)
				{
					list.Add(text3);
				}
			}
			if (list.Count < 4 && c.Voice.Warmth >= 0)
			{
				string text4 = Teachers.Rumour(with, you);
				if (text4 != null)
				{
					list.Add(text4);
				}
			}
			if (list.Count == 0)
			{
				list.Add(Pick("Nothing ever happens around here.", "Quiet times. Long may they last.", "No news is good news, they say."));
			}
			c.Say(list.ToArray());
			List<Choice> list2 = new List<Choice>();
			if (HasPlot(with))
			{
				list2.Add(new Choice("Is something going on here? Between us.", delegate
				{
					PlotTalk(c);
				}, ChoiceTone.Risk));
			}
			list2.AddRange(BackOnly(c));
			c.Offer(list2);
		}

		private static string Lower(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToLower(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static string WarsLine(Actor npc)
		{
			try
			{
				Kingdom kingdom = npc.kingdom;
				if (kingdom == null || !kingdom.isCiv())
				{
					return null;
				}
				List<string> list = new List<string>();
				foreach (War war in kingdom.getWars())
				{
					if (war == null)
					{
						continue;
					}
					foreach (Kingdom item in war.getOppositeSideKingdom(kingdom))
					{
						if (item != null && !list.Contains(item.name))
						{
							list.Add(item.name);
						}
					}
				}
				if (list.Count == 0)
				{
					return Pick(kingdom.name + " is at peace, thank the gods.", "No wars, for now.");
				}
				string text = ((list.Count == 1) ? list[0] : (string.Join(", ", list.GetRange(0, list.Count - 1).ToArray()) + " and " + list[list.Count - 1]));
				return Pick("We're at war with " + text + ".", "The war with " + text + " goes on.", text + "... they want our land.");
			}
			catch
			{
				return null;
			}
		}

		private static string TownTroubles(Actor npc)
		{
			try
			{
				City city = npc.city;
				if (city == null)
				{
					return null;
				}
				int populationPeople = city.getPopulationPeople();
				int num = city.countHungry();
				int num2 = city.countHomeless();
				int num3 = city.getResourcesAmount("bread") + city.getResourcesAmount("berries") + city.getResourcesAmount("fish") + city.getResourcesAmount("meat") + city.getResourcesAmount("wheat");
				if (populationPeople > 0 && num * 3 > populationPeople)
				{
					return "Half of " + city.name + " goes to bed hungry.";
				}
				if (num3 < populationPeople)
				{
					return "The stores of " + city.name + " are nearly empty.";
				}
				if (num2 > 2)
				{
					return $"{num2} of us in {city.name} have no roof.";
				}
				if (populationPeople > 30)
				{
					return $"{city.name} keeps growing. {populationPeople} of us now!";
				}
			}
			catch
			{
			}
			return null;
		}

		private static bool HasPlot(Actor a)
		{
			try
			{
				return a.plot != null && a.plot.isAlive();
			}
			catch
			{
				return false;
			}
		}

		private static void PlotTalk(Conversation c)
		{
			Actor with = c.With;
			Actor you = c.You;
			int num = Chronicle.Opinion(with, you);
			Plot plot = with.plot;
			if (num < 50 || plot == null)
			{
				c.Say(Pick("Going on? Nothing's going on. Why do you ask?", "*looks away* I wouldn't know.", "You ask too many questions."));
				c.Offer(BackOnly(c));
				return;
			}
			string name = plot.name;
			int num2 = 0;
			try
			{
				num2 = plot.countUnits();
			}
			catch
			{
			}
			c.Say("*lowers voice* Some of us are part of " + name + ".", (num2 > 1) ? $"There are {num2} of us. Not a word to anyone." : "Not a word to anyone.");
			c.Offer(BackOnly(c));
		}

		private static void King(Conversation c)
		{
			c.LastTopic = "king";
			Actor with = c.With;
			Actor you = c.You;
			Kingdom kingdom = with.kingdom;
			Actor actor = kingdom?.king;
			if (actor == null)
			{
				c.Say("We have no king right now. Strange days.");
				c.Offer(BackOnly(c));
				return;
			}
			if (actor == you)
			{
				c.Say((c.Voice.Warmth >= 0) ? Pick("You ARE the king! Long live you, majesty!", "*bows low* Majesty.") : Pick("*grits teeth* Your majesty.", "As you command. Majesty."));
				c.Offer(BackOnly(c));
				return;
			}
			int num = 0;
			try
			{
				num = Date.getYearsSince(kingdom.data.timestamp_king_rule);
			}
			catch
			{
			}
			int num2 = 0;
			try
			{
				if (with.city != null)
				{
					num2 = with.city.getLoyalty();
				}
			}
			catch
			{
			}
			int num3 = SafeHappiness(with);
			string text = (Models.IsFemale(actor) ? "queen" : "king");
			string text2 = NameOf(actor);
			string text3 = ((num > 0) ? string.Format("{0} has ruled us {1} year{2}.", text2, num, (num == 1) ? "" : "s") : (text2 + " was crowned this year."));
			string text4 = ((HasPlot(with) && Chronicle.Opinion(with, you) > 30) ? ("*quietly* The " + text + "'s days are numbered.") : ((num2 < 0 || num3 < -20) ? Pick("The " + text + "? Squeezes us dry and gives nothing back.", "I wouldn't weep if the " + text + " fell off a horse.") : ((num2 <= 25 || num3 <= 30) ? Pick("The " + text + " is... the " + text + ". Could be worse.", "I keep my head down and pay my taxes.") : Pick("A good " + text + ". We prosper.", "Long live the " + text + "!"))));
			c.Say(text3, text4);
			c.Offer(BackOnly(c));
		}

		private static void Realm(Conversation c)
		{
			c.LastTopic = "realm";
			Actor with = c.With;
			List<string> list = new List<string>();
			try
			{
				if (with.isKing() && with.kingdom != null)
				{
					Kingdom kingdom = with.kingdom;
					int num = kingdom.countCities();
					int populationPeople = kingdom.getPopulationPeople();
					int num2 = kingdom.countTotalWarriors();
					list.Add(string.Format("{0} has {1} cit{2} and {3} souls. {4} warriors stand ready.", kingdom.name, num, (num == 1) ? "y" : "ies", populationPeople, num2));
					string text = WarsLine(with);
					if (text != null)
					{
						list.Add(text);
					}
					if (kingdom.data.motto != null && kingdom.data.motto.Length > 0)
					{
						list.Add("Our words: \"" + kingdom.data.motto + "\".");
					}
				}
				else if (with.city != null)
				{
					City city = with.city;
					list.Add($"{city.name} has {city.getPopulationPeople()} people, room for {city.getPopulationMaximum()}.");
					int loyalty = city.getLoyalty();
					list.Add((loyalty < 0) ? "Loyalty to the crown runs thin here." : ((loyalty > 30) ? "The town is loyal to the crown." : "The town keeps faith with the crown, mostly."));
					string text2 = TownTroubles(with);
					if (text2 != null)
					{
						list.Add(text2);
					}
				}
			}
			catch
			{
			}
			if (list.Count == 0)
			{
				list.Add("All is well. Mostly.");
			}
			c.Say(list.ToArray());
			c.Offer(BackOnly(c));
		}

		public static string RelationTo(Actor npc, Actor you)
		{
			try
			{
				bool flag = Models.IsFemale(npc);
				if (npc.lover == you)
				{
					return "your sweetheart";
				}
				if (npc.isParentOf(you))
				{
					return flag ? "your mother" : "your father";
				}
				if (you.isParentOf(npc))
				{
					return flag ? "your daughter" : "your son";
				}
				long parent_id_ = you.data.parent_id_1;
				long parent_id_2 = you.data.parent_id_2;
				long parent_id_3 = npc.data.parent_id_1;
				long parent_id_4 = npc.data.parent_id_2;
				if ((parent_id_ > 0 && (parent_id_ == parent_id_3 || parent_id_ == parent_id_4)) || (parent_id_2 > 0 && (parent_id_2 == parent_id_3 || parent_id_2 == parent_id_4)))
				{
					return flag ? "your sister" : "your brother";
				}
				Record record = Chronicle.Of(you);
				if (record != null)
				{
					foreach (Kin item in record.Kin)
					{
						if (item.Id == npc.data.id && item.Role[0] == 'g')
						{
							return flag ? "your grandmother" : "your grandfather";
						}
					}
				}
				Record record2 = Chronicle.Of(npc);
				if (record2 != null)
				{
					foreach (Kin item2 in record2.Kin)
					{
						if (item2.Id == you.data.id && item2.Role[0] == 'g')
						{
							return flag ? "your granddaughter" : "your grandson";
						}
					}
				}
				if (npc.data.best_friend_id == you.data.id)
				{
					return "your best friend";
				}
				if (npc.isKing())
				{
					return (flag ? "Queen of " : "King of ") + npc.kingdom.name;
				}
				string text = Regalia.TitleFor(npc, you, high: true);
				if (text != null)
				{
					return text;
				}
				if (npc.isCityLeader())
				{
					return "Leader of " + ((npc.city != null) ? npc.city.name : "a town");
				}
				if (npc.family != null && npc.family == you.family)
				{
					return "of your family";
				}
				if (Enemies(npc, you))
				{
					return "an enemy";
				}
				text = Regalia.TitleFor(npc, you, high: false);
				if (text != null)
				{
					return text;
				}
				if (SameTribe(npc, you))
				{
					string text2 = JobName(npc);
					if (SameRealmOtherTown(npc, you))
					{
						return (text2 != null) ? Lang.Format("a {0} from {1}", text2, CityName(npc)) : Lang.Format("one of your realm, from {0}", CityName(npc));
					}
					return (text2 != null) ? Lang.Format("a {0} of {1}", text2, CityName(npc) ?? "your people") : "one of your people";
				}
				Kingdom kingdom = npc.kingdom;
				Kingdom kingdom2 = you.kingdom;
				if (kingdom != null && kingdom2 != null && kingdom == kingdom2 && kingdom.isCiv())
				{
					string text3 = JobName(npc);
					string text4 = CityName(npc);
					return (text3 != null && text4 != null) ? Lang.Format("a {0} from {1}", text3, text4) : ((text4 != null) ? Lang.Format("one of your realm, from {0}", text4) : "one of your realm");
				}
				if (kingdom != null && kingdom2 != null && Royal.Allied(kingdom2, kingdom))
				{
					return Lang.Format("an ally from {0}", KingdomName(npc) ?? "a friendly realm");
				}
				string text5 = KingdomName(npc);
				if (text5 != null && kingdom2 != null && kingdom2 != kingdom && kingdom2.name == text5)
				{
					return Lang.Format("a stranger from another realm called {0}", text5);
				}
				return (text5 != null) ? ("a stranger from " + text5) : "a stranger";
			}
			catch
			{
				return "someone";
			}
		}

		private static bool SameRealmOtherTown(Actor npc, Actor you)
		{
			try
			{
				return npc.kingdom == you.kingdom && npc.city != null && you.city != null && npc.city != you.city;
			}
			catch
			{
				return false;
			}
		}

		public static string JobName(Actor a)
		{
			try
			{
				switch (a.getProfession())
				{
				case UnitProfession.King:
					return Models.IsFemale(a) ? "queen" : "king";
				case UnitProfession.Leader:
					return "leader";
				case UnitProfession.Warrior:
					return "warrior";
				default:
				{
					string text = ((a.citizen_job != null) ? a.citizen_job.id : null);
					if (string.IsNullOrEmpty(text))
					{
						return null;
					}
					if (text.Contains("farm"))
					{
						return "farmer";
					}
					if (text.Contains("wood") || text.Contains("lumber"))
					{
						return "woodcutter";
					}
					if (text.Contains("mine") || text.Contains("miner"))
					{
						return "miner";
					}
					if (text.Contains("build"))
					{
						return "builder";
					}
					if (text.Contains("hunt"))
					{
						return "hunter";
					}
					if (text.Contains("fish"))
					{
						return "fisher";
					}
					if (text.Contains("gather"))
					{
						return "gatherer";
					}
					if (text.Contains("smith"))
					{
						return "smith";
					}
					return text.Replace('_', ' ');
				}
				}
			}
			catch
			{
				return null;
			}
		}

		public static void Greet(Conversation c)
		{
			if (Voiced(c))
			{
				GreetBeats(c);
				return;
			}
			Actor with = c.With;
			Actor you = c.You;
			string text = Greeting(with, you);
			Regard regard = Chronicle.RegardOf(with, you);
			string text2 = null;
			Memory memory = RecentGrief(with);
			string text3;
			if (memory != null && !c.Voice.Forgetful)
			{
				text2 = GriefLine(memory, Models.IsFemale(with));
			}
			else if (Births.IsExpecting(with))
			{
				text2 = Births.ExpectingGreeting(with, you);
			}
			else if (with.lover == you && Births.IsExpecting(you))
			{
				text2 = Births.PartnerGreeting(you);
			}
			else if (c.Welcome != null)
			{
				text2 = c.Welcome;
			}
			else if ((text3 = Talk.Greeting(c)) != null)
			{
				text2 = text3;
			}
			else if (regard != null && regard.Talks > 3 && c.Voice.Warmth >= 1)
			{
				text2 = Pick("Back again? I don't mind.", "Always good to see you.", "You again! Sit, sit.");
			}
			else if (regard != null && regard.Talks <= 1 && !SameTribe(with, you) && c.Voice.Warmth <= 0)
			{
				text2 = Pick("What do you want here?", "State your business.");
			}
			if (text2 == null && regard != null && regard.Talks > 1 && (c.Voice.Warmth >= 2 || c.Voice.Warmth <= -1))
			{
				text2 = Cap(Address(c.Voice, with, you)) + ((c.Voice.Warmth > 0) ? ", it's good to see you." : "... what now?");
			}
			c.Say(Flourish(c.Voice) + text, text2);
			c.Offer(Topics(c));
		}

		public static void Introduce(Conversation c)
		{
			Actor with = c.With;
			Actor you = c.You;
			string text = NameOf(you);
			string yours = ((!HasTrait(you, "mute")) ? (c.Voice.Sovereign ? Pick("And you are?", "Your name?", "Who are you?") : Pick("Hello. I'm " + text + ".", "Hi. I'm " + text + ".", "Good day. I'm " + text + ".")) : (c.Voice.Sovereign ? "*you nod to them*" : "*you raise a hand in greeting, then point to yourself*"));
			if (c.Voice.Mute)
			{
				c.Hello(yours, "*taps a chest, then scratches a name in the dirt: " + NameOf(with) + "*", theirsAsIs: true);
			}
			else
			{
				c.Hello(yours, Introduction(with, you, c.Voice), theirsAsIs: false);
			}
			c.Offer(Topics(c));
		}

		private static string Introduction(Actor npc, Actor you, Voice v)
		{
			string text = NameOf(npc);
			string text2 = NameOf(you);
			bool flag = false;
			try
			{
				flag = !npc.isAdult();
			}
			catch
			{
			}
			if (v.Sovereign)
			{
				if (!flag)
				{
					return Pick("*bows* " + text + ", " + Majesty(you) + ". At your service.", text + ", " + Majesty(you) + ". It's an honour.", "*bows low* " + text + ". Whatever you need, " + Majesty(you) + ".");
				}
				return Pick("*bows clumsily* I'm " + text + "!", "*whispers* " + text + ", " + Majesty(you) + ".");
			}
			if (flag)
			{
				return Pick("I'm " + text + "!", "*shyly* " + text + ".", "My name's " + text + "!");
			}
			if (Enemies(npc, you))
			{
				return Pick(text + ". Not that it's yours to know.", "They call me " + text + ". Now keep walking.", text + ". And I know where you're from.");
			}
			int num = SafeHappiness(npc);
			string text3 = ((Rng.Next(3) > 0) ? IntroWork(npc) : "");
			if (SameTribe(npc, you))
			{
				if (v.Warmth < 0 || num < -20)
				{
					return Pick(text + ". What do you want?", "*sighs* " + text + ". Is there something you need?");
				}
				if (v.Warmth >= 1 || num > 40)
				{
					return Pick("I'm " + text + ". Good to meet you, " + text2 + "!", text + ". Welcome, " + text2 + ".") + text3;
				}
				return Pick("I'm " + text + ".", text + ". Pleased to meet you.", "Call me " + text + ".") + text3;
			}
			if (v.Warmth < 0)
			{
				return Pick(text + ". State your business.", "I'm " + text + ". You're not from around here.");
			}
			return Pick("I'm " + text + ". What brings you here, " + text2 + "?", text + ". You're a long way from home.", "Call me " + text + ". Welcome, traveller.") + text3;
		}

		private static string IntroWork(Actor npc)
		{
			try
			{
				if (npc.isCityLeader() && CityName(npc) != null)
				{
					return " I lead " + CityName(npc) + ".";
				}
				if (npc.getProfession() == UnitProfession.Warrior && KingdomName(npc) != null)
				{
					Actor actor = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Body : null);
					if (actor != null && actor.kingdom == npc.kingdom && CityName(npc) != null)
					{
						return " I serve in " + CityName(npc) + "'s army.";
					}
					return " I serve in the army of " + KingdomName(npc) + ".";
				}
			}
			catch
			{
			}
			switch (JobName(npc))
			{
			case "farmer":
				return " I work the fields here.";
			case "woodcutter":
				return " I cut wood.";
			case "miner":
				return " I work the mines.";
			case "builder":
				return " I build the houses here.";
			case "hunter":
				return " I hunt.";
			case "fisher":
				return " I fish.";
			case "gatherer":
				return " I gather what grows round here.";
			case "smith":
				return " I keep the forge.";
			default:
				return "";
			}
		}

		private static Memory RecentGrief(Actor npc)
		{
			Record record = Chronicle.Of(npc);
			if (record == null)
			{
				return null;
			}
			int year = Chronicle.Year;
			for (int num = record.Memories.Count - 1; num >= 0; num--)
			{
				Memory memory = record.Memories[num];
				if (year - memory.Year > 1)
				{
					break;
				}
				if (memory.Kind == "death_lover" || memory.Kind == "death_child" || memory.Kind == "death_family_member" || memory.Kind == "death_best_friend")
				{
					return memory;
				}
			}
			return null;
		}

		private static string GriefLine(Memory m, bool female)
		{
			string text = m.Other ?? "someone dear to me";
			switch (m.Kind)
			{
			case "death_lover":
				return Pick("*eyes red* Forgive me. I lost " + text + " not long ago.", "Since " + text + " died, the house is so quiet.", "I keep setting a bowl out for " + text + "...");
			case "death_child":
				return Pick("We buried " + text + " not long ago. Little " + text + "...", "*quietly* " + text + " should have outlived me.");
			case "death_best_friend":
				return Pick("My friend " + text + " is gone. Nobody tells jokes like that anymore.", "Have you heard? " + text + " died. My oldest friend.");
			default:
				return Pick("We've had a death in the family. " + text + ".", "I'm still in mourning for " + text + ".");
			}
		}

		public static List<Choice> Topics(Conversation c)
		{
			Actor with = c.With;
			Actor you = c.You;
			List<Choice> list = new List<Choice>();
			bool flag = false;
			try
			{
				flag = with.isBaby();
			}
			catch
			{
			}
			if (flag)
			{
				list.Add(new Choice("Pull a funny face", delegate
				{
					c.Say(Pick("*giggles and grabs at your nose*", "*shrieks with laughter*", "*stares, then bursts into tears*"));
					c.Offer(Topics(c));
				}));
				list.Add(Goodbye(c));
				return list;
			}
			list.Add(new Choice("Tell me about your family.", delegate
			{
				Family(c);
			}));
			if (HasFaith(with))
			{
				list.Add(new Choice("What do you believe in?", delegate
				{
					Faith(c);
				}));
			}
			list.Add(new Choice("Tell me about your life.", delegate
			{
				Life(c);
			}));
			list.Add(new Choice("Any news?", delegate
			{
				News(c);
			}));
			if (IsRuler(with))
			{
				list.Add(new Choice(with.isKing() ? "How fares your realm?" : "How is the town?", delegate
				{
					Realm(c);
				}));
			}
			else if (HasKing(with))
			{
				list.Add(new Choice("What do you think of the king?", delegate
				{
					King(c);
				}));
			}
			if (YouRule(you, with))
			{
				list.Add(new Choice("Do you have a petition for me?", delegate
				{
					Petition(c);
				}, ChoiceTone.Quest));
			}
			list.Add(new Choice("Small talk", delegate
			{
				SmallTalk(c);
			}));
			FindChoices(c, list);
			Quests.Choices(c, list);
			Quests.TeachChoices(c, list);
			WeaponTraits.BoonChoices(c, list);
			Births.Choices(c, list);
			FlirtChoices(c, list);
			list.Add(new Choice("Offer a gift", delegate
			{
				GiftMenu(c);
			}));
			TradeChoices(c, list);
			Law.Choices(c, list);
			BelongChoices(c, list);
			EnlistChoices(c, list);
			Talk.Choices(c, list);
			list.Add(Goodbye(c));
			return list;
		}

		public static Choice Goodbye(Conversation c)
		{
			return new Choice("Goodbye.", delegate
			{
				string lastTopic = c.LastTopic;
				c.LastTopic = "goodbye";
				c.EndAfter = true;
				if (Voiced(c))
				{
					FarewellBeats(c, lastTopic);
				}
				else
				{
					c.Say(Farewell(c.With, c.You));
				}
			}, ChoiceTone.Leave);
		}

		public static Choice Back(Conversation c)
		{
			return new Choice("Let's talk about something else.", delegate
			{
				c.Panel = TalkPanel.None;
				c.Say(WhatElse(c));
				c.Offer(Topics(c));
			}, ChoiceTone.Back);
		}

		private static List<Choice> BackOnly(Conversation c)
		{
			return new List<Choice>
			{
				Back(c),
				Goodbye(c)
			};
		}

		private static bool IsRuler(Actor a)
		{
			try
			{
				return a.isKing() || a.isCityLeader();
			}
			catch
			{
				return false;
			}
		}

		private static bool HasKing(Actor a)
		{
			try
			{
				return a.kingdom != null && a.kingdom.isCiv() && a.kingdom.king != null && a.kingdom.king.isAlive();
			}
			catch
			{
				return false;
			}
		}

		private static void SmallTalk(Conversation c)
		{
			c.LastTopic = "smalltalk";
			long id = c.With.data.id;
			SmallTalkTurn.TryGetValue(id, out var value);
			SmallTalkTurn[id] = value + 1;
			if (Voiced(c))
			{
				SmallTalkBeats(c, value);
			}
			else
			{
				string text;
				switch (value % 3)
				{
				case 0:
					text = AboutWork(c.With);
					break;
				case 1:
					text = AboutHome(c.With, c.You);
					break;
				default:
					text = AboutTraits(c.With);
					break;
				}
				c.Say(text);
			}
			List<Choice> list = new List<Choice>
			{
				new Choice("Go on, tell me more.", delegate
				{
					SmallTalk(c);
				})
			};
			list.AddRange(BackOnly(c));
			c.Offer(list);
			Chronicle.ChangeOpinion(c.With, c.You, (value < 4) ? 1 : 0);
		}

		private static void GiftMenu(Conversation c)
		{
			c.LastTopic = "gift";
			Actor you = c.You;
			int num = 0;
			try
			{
				num = you.money;
			}
			catch
			{
			}
			List<Choice> list = new List<Choice>();
			int[] array = new int[3] { 5, 20, 50 };
			foreach (int amount in array)
			{
				Choice choice = new Choice($"Give {amount} gold", delegate
				{
					GiveMoney(c, amount);
				});
				if (num < amount)
				{
					choice.Off($"You have only {num} gold.");
				}
				list.Add(choice);
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			bool flag = Quests.DoneForToday(c.With);
			if (instance != null)
			{
				foreach (KeyValuePair<string, int> item in instance.Work.Bag)
				{
					string res = item.Key;
					int count = Math.Min(item.Value, 5);
					Choice choice2 = new Choice($"Give {count} {Work.ResourceName(res)}", delegate
					{
						GiveResource(c, res, count);
					});
					if (flag)
					{
						choice2.Off("They've had enough from you today");
					}
					list.Add(choice2);
					if (list.Count >= 7)
					{
						break;
					}
				}
			}
			list.Add(Back(c));
			c.Say(Pick("A gift? For me?", "Oh? What have you got?", "You needn't, really... but go on."));
			c.Offer(list);
		}

		private static void GiveMoney(Conversation c, int amount)
		{
			try
			{
				if (c.You.money < amount)
				{
					c.Say("You don't have that much.");
					c.Offer(Topics(c));
				}
				else
				{
					c.You.spendMoney(amount);
					c.With.addMoney(amount);
					Gifted(c, (amount >= 50) ? 14 : ((amount >= 20) ? 8 : 3));
				}
			}
			catch (Exception ex)
			{
				Log.Error("gift: " + ex.Message);
				c.Say("*fumbles the coins*");
				c.Offer(Topics(c));
			}
		}

		private static void GiveResource(Conversation c, string res, int count)
		{
			if (Quests.DoneForToday(c.With))
			{
				c.Say(Quests.EnoughToday());
				c.Offer(Topics(c));
				return;
			}
			if (!Civil.TakeFromBag(c.You, res, count))
			{
				c.Say("You don't have that.");
				c.Offer(Topics(c));
				return;
			}
			Quests.NoteDeal(c.With);
			try
			{
				if (c.With.city != null)
				{
					c.With.city.addResourcesToRandomStockpile(res, count);
				}
			}
			catch
			{
			}
			Gifted(c, 5 + count);
		}

		private static void Gifted(Conversation c, int opinion)
		{
			Actor with = c.With;
			Actor you = c.You;
			try
			{
				with.changeHappiness("just_received_gift");
			}
			catch
			{
			}
			try
			{
				you.changeHappiness("just_gave_gift");
			}
			catch
			{
			}
			Chronicle.Note(with, you, 0, 1);
			int num = Math.Max(1, opinion - (Chronicle.RegardOf(with, you)?.Gifts ?? 0));
			if (HasTrait(with, "greedy"))
			{
				num += 4;
			}
			Chronicle.ChangeOpinion(with, you, num);
			c.Voice = VoiceOf(with, you);
			Refeel(c);
			c.Say(Thanks(c) ?? ((c.Voice.Manner == "greedy") ? Pick("Ooh. More of this, and we'll be fast friends.", "Now THAT is how you greet someone.") : Pick("Thank you, " + Address(c.Voice, with, you) + "! That's very kind of you.", "For me? You shouldn't have!", "I'll treasure it.", "*smiles warmly* Thank you.")));
			c.Offer(Topics(c));
		}

		internal static bool HasTrait(Actor a, string id)
		{
			try
			{
				return a.hasTrait(id);
			}
			catch
			{
				return false;
			}
		}

		private static void TradeChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			if (!IsPerson(with) || with == you)
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = with.isAdult();
			}
			catch
			{
			}
			if (!flag)
			{
				return;
			}
			if (Economy.HirelingOf(with) != null)
			{
				list.Add(new Choice("About your contract...", delegate
				{
					Contract(c);
				}, ChoiceTone.Quest));
				return;
			}
			if (with.city != null && !Enemies(with, you))
			{
				Choice choice = new Choice("Let's trade.", delegate
				{
					Trade(c);
				});
				if (Law.BountyOn(with.kingdom, you) > 0 && !HasTrait(with, "greedy") && !HasTrait(with, "deceitful"))
				{
					choice.Off("They won't trade with someone wanted here.");
				}
				list.Add(choice);
			}
			if (!Enemies(with, you))
			{
				list.Add(new Choice("I need protection.", delegate
				{
					Protection(c);
				}));
			}
			Bribe(c, list);
			IntrigueChoices(c, list);
		}

		private static string Gold(int n)
		{
			return n + " gold";
		}

		private static string An(string thing)
		{
			return ((thing.Length > 0 && "aeiou".IndexOf(thing[0]) >= 0) ? "an " : "a ") + thing;
		}

		private static Choice Priced(Conversation c, string label, int price, Action act, ChoiceTone tone = ChoiceTone.Plain)
		{
			Choice choice = new Choice(label + " (" + Gold(price) + ")", act, tone);
			int num = Economy.Money(c.You);
			if (num < price)
			{
				choice.Off($"You have only {num} gold.");
			}
			return choice;
		}

		internal static bool IsSmith(Actor a)
		{
			try
			{
				if (a.isWarrior() || a.isKing() || a.isCityLeader() || (a.citizen_job != null && a.citizen_job.id.Contains("smith")))
				{
					return true;
				}
				if (a.city != null)
				{
					foreach (Building item in Shops.In(a.city))
					{
						if (Shops.Of(item) == ShopKind.Smithy && Shops.Keeper(item) == a)
						{
							return true;
						}
					}
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		private static void Trade(Conversation c)
		{
			c.LastTopic = "trade";
			City city = c.With.city;
			float num = Economy.Markup(c);
			string text = ((num < 0.95f) ? " For you, a fair price." : ((num > 1.15f) ? " Prices are what they are." : ""));
			c.Say((c.Voice.Manner == "greedy") ? (Pick("Coin! My favorite word.", "Buying or selling? Either way I win.") + text) : (Pick("What do you need? " + city.name + "'s stores are open.", "Trade? Sure, let's see.", "Buying or selling?") + text));
			c.Offer(TradeMenu(c));
		}

		private static List<Choice> TradeMenu(Conversation c)
		{
			Actor with = c.With;
			bool num = Economy.HasStores(with.city);
			Choice choice = new Choice("What can I buy?", delegate
			{
				Supplies(c);
			});
			Choice choice2 = new Choice("I have goods to sell.", delegate
			{
				SellMenu(c);
			});
			if (!num)
			{
				choice.Off("The town has no storehouse yet.");
				choice2.Off("The town has no storehouse yet.");
			}
			List<Choice> list = new List<Choice> { choice, choice2 };
			if (IsSmith(with))
			{
				list.Add(new Choice("Show me your arms and armor.", delegate
				{
					ArmoryMenu(c);
				}));
			}
			if (IsSmith(with))
			{
				list.Add(new Choice("What can you forge for me?", delegate
				{
					ForgeMenu(c);
				}));
			}
			BagChoice(c, list);
			if (IsSmith(with))
			{
				GearSaleChoice(c, list);
			}
			if (IsSmith(with))
			{
				ReworkChoice(c, list);
			}
			int inn = Inn(c);
			Choice choice3 = Priced(c, "I need a bed for the night", inn, delegate
			{
				Rest(c, inn);
			});
			if (choice3.Enabled && Economy.InAFight(c.You))
			{
				choice3.Off("Not with a fight on your heels.");
			}
			else if (choice3.Enabled && Chronicle.Year == _sleptYear && c.You == _sleptIn)
			{
				choice3.Off("You've slept already this year.");
			}
			list.Add(choice3);
			list.Add(Back(c));
			return list;
		}

		private static void Supplies(Conversation c)
		{
			c.LastTopic = "trade_buy";
			City city = c.With.city;
			float num = Economy.Markup(c);
			List<Choice> list = new List<Choice>();
			Supplies.Kind[] all = FirstPerson.Supplies.All;
			foreach (Supplies.Kind kind in all)
			{
				Supplies.Kind k = kind;
				if (!k.Dug && k.Level <= 0)
				{
					int count = ((!(k.Id == "tw_item_bandage")) ? 1 : 3);
					int price = Mathf.Max(1, Mathf.CeilToInt((float)(k.Price * count) * num));
					list.Add(Priced(c, "Buy " + FirstPerson.Supplies.Counted(k, count), price, delegate
					{
						BuySupply(c, k, count, price);
					}, ChoiceTone.Quest));
				}
			}
			int count2 = list.Count;
			string[] wares = Wares;
			foreach (string text in wares)
			{
				int num2 = 0;
				try
				{
					num2 = city.getResourcesAmount(text);
				}
				catch
				{
				}
				if (num2 >= 10)
				{
					string r = text;
					int price2 = Economy.BuyPrice(text, 5, num);
					list.Add(Priced(c, $"Buy {5} {Work.ResourceName(text)}", price2, delegate
					{
						Buy(c, r, price2);
					}, ChoiceTone.Quest));
					if (list.Count >= 6 + count2)
					{
						break;
					}
				}
			}
			if (list.Count == count2)
			{
				c.Say(Pick("The stores of " + city.name + " are nearly bare. Only linen and salve to spare.", "We've barely enough for ourselves. Bandages, though, I have."));
			}
			else
			{
				c.Say(Pick("Here's what we can spare.", "Take your pick.", "Fresh from the stores of " + city.name + "."));
			}
			list.Add(new Choice("Something else.", delegate
			{
				c.Say("What else?");
				c.Offer(TradeMenu(c));
			}, ChoiceTone.Back));
			c.Offer(list);
		}

		private static void Buy(Conversation c, string res, int price)
		{
			Actor you = c.You;
			City city = c.With.city;
			if (WorldBoxMod.Instance != null && Bag.RoomFor(you, res) < 5)
			{
				c.Say("Your " + Bag.Name(you).ToLowerInvariant() + "'s full. Make room first.");
				c.Offer(TradeMenu(c));
				return;
			}
			int num = 0;
			try
			{
				num = city.getResourcesAmount(res);
			}
			catch
			{
			}
			if (num < 5)
			{
				c.Say("Ah... we've just run out.");
				c.Offer(TradeMenu(c));
				return;
			}
			if (!Economy.Pay(you, price, city, c.With))
			{
				c.Say("You can't afford that.");
				c.Offer(TradeMenu(c));
				return;
			}
			try
			{
				city.takeResource(res, 5);
				you.addToInventory(res, 5);
			}
			catch (Exception ex)
			{
				Log.Error("trade: buy: " + ex.Message);
			}
			Chronicle.ChangeOpinion(c.With, you, 1);
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			c.Toast?.Invoke($"Bought {5} {Work.ResourceName(res)} for {Gold(price)}");
			Log.Info($"trade: bought {5} {res} for {price} in {city.name}");
			c.Say(Pick("Pleasure doing business.", "Here you are.", "Mind you don't drop it."));
			Supplies(c);
		}

		private static void BuySupply(Conversation c, Supplies.Kind kind, int count, int price)
		{
			Actor you = c.You;
			if (Bag.RoomFor(you, kind.Id) < count)
			{
				c.Say("Your " + Bag.Name(you).ToLowerInvariant() + "'s full. Make room first.");
				c.Offer(TradeMenu(c));
				return;
			}
			if (!Economy.Pay(you, price, c.With.city, c.With))
			{
				c.Say("You can't afford that.");
				c.Offer(TradeMenu(c));
				return;
			}
			FirstPerson.Supplies.Give(you, kind.Id, count);
			Chronicle.ChangeOpinion(c.With, you, 1);
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			c.Toast?.Invoke("Bought " + FirstPerson.Supplies.Counted(kind, count) + " for " + Gold(price));
			Log.Info($"trade: bought {count} {kind.Id} for {price} in {c.With.city?.name}");
			c.Say(Pick("Clean linen. Keep it dry.", "Wrap it tight and it'll hold.", "Here. Mind you don't need them."));
			Supplies(c);
		}

		private static int BagTotal(Actor a)
		{
			int num = 0;
			try
			{
				foreach (KeyValuePair<string, ResourceContainer> resource in a.inventory.getResources())
				{
					num += Math.Max(0, resource.Value.amount);
				}
			}
			catch
			{
			}
			return num;
		}

		private static void SellMenu(Conversation c)
		{
			c.LastTopic = "trade_sell";
			Actor you = c.You;
			Actor with = c.With;
			City city = with.city;
			float markup = Economy.Markup(c);
			int num = Economy.CanPay(city, with);
			List<Choice> list = new List<Choice>();
			int all = 0;
			List<KeyValuePair<string, int>> bag = new List<KeyValuePair<string, int>>();
			try
			{
				foreach (KeyValuePair<string, ResourceContainer> resource in you.inventory.getResources())
				{
					if (resource.Value.amount > 0)
					{
						bag.Add(new KeyValuePair<string, int>(resource.Key, resource.Value.amount));
					}
				}
			}
			catch
			{
			}
			foreach (KeyValuePair<string, int> item in bag)
			{
				all += Economy.SellPrice(item.Key, item.Value, markup);
			}
			foreach (KeyValuePair<string, int> item2 in bag)
			{
				string res = item2.Key;
				int count = Math.Min(item2.Value, 5);
				int price = Economy.SellPrice(res, count, markup);
				Choice choice = new Choice($"Sell {count} {Work.ResourceName(res)} ({Gold(price)})", delegate
				{
					Sell(c, res, count, price);
				}, ChoiceTone.Quest);
				if (price > num)
				{
					choice.Off(city.name + " can't pay that much now.");
				}
				list.Add(choice);
				if (list.Count >= 6)
				{
					break;
				}
			}
			if (bag.Count > 1)
			{
				Choice choice2 = new Choice("Sell everything (" + Gold(all) + ")", delegate
				{
					SellAll(c, bag, all);
				}, ChoiceTone.Quest);
				if (all > num)
				{
					choice2.Off($"{city.name} can only pay {num} gold now.");
				}
				list.Insert(0, choice2);
			}
			if (list.Count == 0)
			{
				c.Say("You've nothing in your bag to sell. Go gather something.");
			}
			else
			{
				c.Say((num < 20) ? Pick("Times are lean. We can't pay much.", "The treasury of " + city.name + " is thin, mind.") : Pick("Let's see what you've got.", "Show me. I pay fair."));
			}
			list.Add(new Choice("Something else.", delegate
			{
				c.Say("What else?");
				c.Offer(TradeMenu(c));
			}, ChoiceTone.Back));
			c.Offer(list);
		}

		private static void Sell(Conversation c, string res, int count, int price)
		{
			Actor you = c.You;
			Actor with = c.With;
			if (Economy.CanPay(with.city, with) < price)
			{
				c.Say("I can't pay that much now.");
				SellMenu(c);
				return;
			}
			if (!Civil.TakeFromBag(you, res, count))
			{
				c.Say("You don't have that.");
				c.Offer(TradeMenu(c));
				return;
			}
			try
			{
				with.city.addResourcesToRandomStockpile(res, count);
			}
			catch
			{
			}
			int n = Economy.PayOut(you, price, with.city, with);
			Sold(c, $"Sold {count} {Work.ResourceName(res)} for {Gold(n)}");
			SellMenu(c);
		}

		private static void SellAll(Conversation c, List<KeyValuePair<string, int>> bag, int total)
		{
			Actor you = c.You;
			Actor with = c.With;
			float markup = Economy.Markup(c);
			int num = 0;
			foreach (KeyValuePair<string, int> item in bag)
			{
				int num2 = Civil.BagCount(you, item.Key);
				if (num2 > 0)
				{
					num += Economy.SellPrice(item.Key, num2, markup);
				}
			}
			if (Economy.CanPay(with.city, with) < Mathf.Max(total, num))
			{
				c.Say("I can't pay that much now.");
				SellMenu(c);
				return;
			}
			int num3 = 0;
			foreach (KeyValuePair<string, int> item2 in bag)
			{
				int num4 = Civil.BagCount(you, item2.Key);
				if (num4 > 0 && Civil.TakeFromBag(you, item2.Key, num4))
				{
					try
					{
						with.city.addResourcesToRandomStockpile(item2.Key, num4);
					}
					catch
					{
					}
					num3 += Economy.SellPrice(item2.Key, num4, markup);
				}
			}
			num3 = Economy.PayOut(you, num3, with.city, with);
			Sold(c, "Sold your whole bag for " + Gold(num3));
			c.Say(Pick("A fine haul. Come back with more.", "*counts it all twice* Done."));
			c.Offer(TradeMenu(c));
		}

		private static void Sold(Conversation c, string toast)
		{
			Chronicle.ChangeOpinion(c.With, c.You, 1);
			Sounds.PlayAt("event:/SFX/UI/YesPoint", c.You.current_position);
			c.Toast?.Invoke(toast);
			Log.Info("trade: " + toast.ToLowerInvariant() + " in " + c.With.city.name);
		}

		private static void ArmoryMenu(Conversation c, string said = null)
		{
			c.LastTopic = "trade_arms";
			City city = c.With.city;
			float markup = Economy.Markup(c);
			List<Item> list = Economy.Armory(city, 6);
			List<Choice> list2 = new List<Choice>();
			foreach (Item item2 in list)
			{
				Item item = item2;
				int price = Economy.ItemPrice(item, markup);
				list2.Add(Priced(c, "Buy " + An(Economy.ItemName(item)), price, delegate
				{
					BuyItem(c, item, price);
				}, ChoiceTone.Quest));
			}
			if (said != null)
			{
				c.Say(said);
			}
			else if (list2.Count == 0)
			{
				c.Say(Pick("The armory of " + city.name + " is empty. Every blade is in someone's hand.", "Nothing to sell. Got iron? Craft it yourself (Tab)."));
			}
			else
			{
				c.Say(Pick("Good steel, every piece.", "The best of " + city.name + "'s armory.", "Pick one. No refunds."));
			}
			list2.Add(new Choice("Something else.", delegate
			{
				c.Say("What else?");
				c.Offer(TradeMenu(c));
			}, ChoiceTone.Back));
			c.Offer(list2);
		}

		private static void BuyItem(Conversation c, Item item, int price)
		{
			Actor you = c.You;
			City city = c.With.city;
			if (Economy.Money(you) < price)
			{
				c.Say("You can't afford that.");
				ArmoryMenu(c);
				return;
			}
			if (!Economy.Equip(you, item, city, out var note))
			{
				c.Say(note);
				ArmoryMenu(c);
				return;
			}
			Economy.Pay(you, price, city, c.With);
			Sounds.PlayAt("event:/SFX/HIT/HitMetal", you.current_position);
			c.Toast?.Invoke("Bought " + An(Economy.ItemName(item)) + " for " + Gold(price) + " and put it on." + note);
			Log.Info($"trade: bought {item.getAsset().id} ({item.getQuality()}) for {price} in {city.name}");
			ArmoryMenu(c, Pick("It suits you.", "Treat it well and it'll treat you well.", "Now you look the part."));
		}

		private static int Inn(Conversation c)
		{
			return Mathf.Max(3, Mathf.RoundToInt(8f * Economy.Markup(c)));
		}

		private static void Rest(Conversation c, int price)
		{
			c.LastTopic = "trade_rest";
			Actor you = c.You;
			if (Economy.InAFight(you))
			{
				c.Say("Sleep? Now? With them after you?");
				c.Offer(TradeMenu(c));
				return;
			}
			if (!Economy.Pay(you, price, c.With.city, c.With))
			{
				c.Say("You can't afford a bed.");
				c.Offer(TradeMenu(c));
				return;
			}
			_sleptIn = you;
			_sleptYear = Chronicle.Year;
			try
			{
				you.restoreHealthPercent(1f);
				((BaseSimObject)you).addStatusEffect("had_good_dream", 0f, pColorEffect: false);
				you.data.stamina = you.getMaxStamina();
			}
			catch (Exception ex)
			{
				Log.Error("trade: rest: " + ex.Message);
			}
			c.Toast?.Invoke("You sleep in a warm bed (" + Gold(price) + "): fully rested");
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			c.SayAsIs("*You sleep soundly through the night, and wake rested and whole.*");
			c.Offer(TradeMenu(c));
		}

		private static void Protection(Conversation c)
		{
			c.LastTopic = "protection";
			Actor with = c.With;
			Actor you = c.You;
			List<Choice> list = new List<Choice>();
			bool flag = IsRuler(with);
			Kingdom k = with.kingdom;
			if (flag && k != null && k.isCiv())
			{
				if (Economy.IsProtected(k, you))
				{
					list.Add(new Choice("How long am I under " + k.name + "'s protection?", delegate
					{
						c.Say("Our guards stand with you for " + Economy.TimeLeft(Economy.ProtectedUntil(k, you)) + " more. Don't make me regret it.");
						c.Offer(ProtectionBack(c));
					}));
				}
				int[] array = new int[2] { 10, 30 };
				foreach (int num2 in array)
				{
					int y = num2;
					int fee = Economy.ProtectionFee(k, num2);
					Choice choice = Priced(c, $"Buy {k.name}'s protection for {num2} years", fee, delegate
					{
						BuyProtection(c, k, y, fee);
					}, ChoiceTone.Quest);
					if (Law.BountyOn(k, you) > 0)
					{
						choice.Off("Not while there's a price on your head here.");
					}
					list.Add(choice);
				}
			}
			if (Economy.CanHire(you, with, out var why))
			{
				int[] array = new int[2] { 5, 20 };
				foreach (int num3 in array)
				{
					int y2 = num3;
					int fee2 = Economy.GuardFee(with, num3);
					list.Add(Priced(c, string.Format("Hire {0} as your guard for {1} year{2}", NameOf(with), num3, (num3 > 1) ? "s" : ""), fee2, delegate
					{
						HireGuard(c, y2, fee2);
					}, ChoiceTone.Quest));
				}
			}
			else if (!flag)
			{
				list.Add(new Choice("Hire " + NameOf(with) + " as your guard", null).Off(why));
			}
			if (HasFaith(with))
			{
				string text = "their faith";
				try
				{
					text = with.religion.name;
				}
				catch
				{
				}
				int price = Mathf.Max(5, Mathf.RoundToInt(15f * Economy.Markup(c)));
				list.Add(Priced(c, "An offering for a blessing of " + text, price, delegate
				{
					Blessing(c, price);
				}));
			}
			int count = Economy.Hired.Count;
			string text2 = ((count > 0) ? string.Format("(You have {0} guard{1} already.)", count, (count > 1) ? "s" : "") : null);
			c.Say(flag ? Pick("Protection has its price. The crown's soldiers don't march for free.", "You want our swords at your back? That can be arranged.") : (Economy.CanHire(you, with, out var _) ? Pick("I can swing a sword, if the coin's right.", "Protection? I'm your best bet. For a price.", "Times are dangerous. I'll watch your back, for gold.") : Pick("I'm no sellsword.", "Find someone else to guard you.")), text2);
			list.Add(Back(c));
			c.Offer(list);
		}

		private static List<Choice> ProtectionBack(Conversation c)
		{
			return new List<Choice>
			{
				new Choice("Something else about protection.", delegate
				{
					Protection(c);
				}, ChoiceTone.Back),
				Back(c),
				Goodbye(c)
			};
		}

		private static void HireGuard(Conversation c, int years, int fee)
		{
			c.LastTopic = "hire";
			Actor you = c.You;
			Actor with = c.With;
			if (!Economy.CanHire(you, with, out var why))
			{
				c.Say(why);
				c.Offer(ProtectionBack(c));
				return;
			}
			if (!Economy.Pay(you, fee, null, with))
			{
				c.Say("Come back when you can pay.");
				c.Offer(ProtectionBack(c));
				return;
			}
			Economy.Hire(you, with, years, fee);
			c.Voice = VoiceOf(with, you);
			c.Toast?.Invoke(string.Format("{0} guards you for {1} year{2}: they follow you and fight whoever fights you", NameOf(with), years, (years > 1) ? "s" : ""));
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			c.Say(Pick("Lead the way. Nobody touches you while I'm around.", "*grips their weapon* You've hired the best.", "Gold's good. I'm with you."));
			c.EndAfter = true;
			c.Offer(new List<Choice>());
		}

		private static void BuyProtection(Conversation c, Kingdom k, int years, int fee)
		{
			c.LastTopic = "protect";
			Actor you = c.You;
			if (!Economy.Pay(you, fee, c.With.city, c.With))
			{
				c.Say("Come back when you can pay.");
				c.Offer(ProtectionBack(c));
				return;
			}
			Economy.Protect(k, you, years);
			Chronicle.ChangeOpinion(c.With, you, 8);
			c.Voice = VoiceOf(c.With, you);
			c.Toast?.Invoke($"{k.name}'s warriors will come to your aid on its lands ({years} years)");
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			c.Say(Pick("Done. Any soldier of " + k.name + " who sees you attacked will draw steel for you.", "The crown's protection is yours. Keep the peace and it stays that way."));
			c.Offer(ProtectionBack(c));
		}

		private static void Blessing(Conversation c, int price)
		{
			c.LastTopic = "blessing";
			Actor you = c.You;
			Actor with = c.With;
			if (!Economy.Pay(you, price, with.city, with))
			{
				c.Say("The gods want more than empty hands.");
				c.Offer(ProtectionBack(c));
				return;
			}
			try
			{
				((BaseSimObject)you).addStatusEffect("shield", 0f, pColorEffect: true);
				((BaseSimObject)you).addStatusEffect("inspired", 0f, pColorEffect: false);
			}
			catch (Exception ex)
			{
				Log.Error("trade: blessing: " + ex.Message);
			}
			Chronicle.ChangeOpinion(with, you, 4);
			c.Toast?.Invoke("Blessed: a holy shield guards you for a while");
			c.Say(Pick("*lays a hand on your brow* Go with the gods' favour.", "May the light turn aside every blade.", "*murmurs a prayer* There. You're watched over now."));
			c.Offer(ProtectionBack(c));
		}

		private static void Contract(Conversation c)
		{
			c.LastTopic = "contract";
			Actor npc = c.With;
			Actor you = c.You;
			Hireling h = Economy.HirelingOf(npc);
			if (h == null)
			{
				c.Say("Contract? What contract?");
				c.Offer(Topics(c));
				return;
			}
			c.Say("I'm yours for " + Economy.TimeLeft(h.Until) + " more. " + Pick("Nobody's laid a finger on you yet.", "Point me at trouble.", "Quiet so far. I don't trust quiet."));
			List<Choice> list = new List<Choice>();
			int fee = Economy.GuardFee(npc, 5);
			list.Add(Priced(c, "Here's pay for another 5 years", fee, delegate
			{
				if (!Economy.Pay(you, fee, null, npc))
				{
					c.Say("That's not enough.");
					c.Offer(Topics(c));
				}
				else
				{
					Economy.Hire(you, npc, 5, fee);
					c.Say(Pick("Five more years it is.", "Gold keeps me loyal. Good."));
					c.Offer(Topics(c));
				}
			}, ChoiceTone.Quest));
			list.Add(new Choice("You're dismissed. Go home.", delegate
			{
				Economy.Dismiss(h, null, null);
				c.Toast?.Invoke(NameOf(npc) + " no longer guards you");
				c.Say(Pick("Suit yourself. No refunds.", "Fair enough. Call me if you need me."));
				c.Offer(Topics(c));
			}, ChoiceTone.Risk));
			list.Add(Back(c));
			c.Offer(list);
		}

		private static void Bribe(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			Kingdom k = with.kingdom;
			if (k == null || !k.isCiv())
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = with.isWarrior() && !with.isKing() && !with.isCityLeader();
			}
			catch
			{
			}
			if (!flag)
			{
				return;
			}
			int num = Law.BountyOn(k, you);
			if (num > 0)
			{
				int price = Mathf.Max(10, Mathf.RoundToInt((float)num * 0.6f));
				Choice choice = Priced(c, "Slip them some coin to tear up your poster", price, delegate
				{
					TakeBribe(c, k, price);
				}, ChoiceTone.Risk);
				if (choice.Enabled && TurnedDown(with))
				{
					choice.Off("They've already turned your coin down.");
				}
				list.Add(choice);
			}
		}

		private static bool TurnedDown(Actor npc)
		{
			try
			{
				npc.data.get("fp_bribe_no", out var pResult, pDefault: false);
				return pResult;
			}
			catch
			{
				return false;
			}
		}

		private static void TakeBribe(Conversation c, Kingdom k, int price)
		{
			c.LastTopic = "bribe";
			Actor with = c.With;
			Actor you = c.You;
			int num = Law.BountyOn(k, you);
			if (num <= 0)
			{
				c.Say("Bribe me? For what? You're not wanted here.");
				c.Offer(Topics(c));
				return;
			}
			bool flag = HasTrait(with, "honest");
			double num2 = ((HasTrait(with, "greedy") || HasTrait(with, "deceitful")) ? 0.95 : (flag ? 0.0 : (0.6 + 0.1 * (double)c.Voice.Warmth)));
			if (Economy.Money(you) < price)
			{
				c.Say("Empty pockets don't bribe anyone.");
				c.Offer(Topics(c));
				return;
			}
			if (BribeRng.NextDouble() < num2 && Economy.Pay(you, price, null, with))
			{
				Law.SetBounty(k, you, 0);
				Law.StandDown(k, you);
				c.Toast?.Invoke("Bribed: you're no longer wanted in " + k.name);
				Log.Info($"trade: bribe of {price} took {num} off in {k.name}");
				c.Say(Pick("*pockets it* What poster? Never seen you before.", "*glances around* Go. Quickly. And never again.", "Hm. Your face must have blown away in the wind."));
			}
			else
			{
				int num3 = num + 25;
				Law.SetBounty(k, you, num3);
				try
				{
					with.data.set("fp_bribe_no", pData: true);
				}
				catch
				{
				}
				Chronicle.ChangeOpinion(with, you, -15);
				c.Toast?.Invoke("The bribe was refused! Wanted in " + k.name + ": " + Gold(num3));
				Log.Info($"trade: bribe refused in {k.name}, bounty now {num3}");
				c.Say(Pick("You'd try to buy me? That's another charge.", "*hand on sword* The law isn't for sale."));
			}
			c.Voice = VoiceOf(with, you);
			c.Offer(Topics(c));
		}

		public static Voice VoiceOf(Actor npc, Actor you)
		{
			Voice result = new Voice
			{
				Kind = ((npc.asset != null) ? npc.asset.id : "")
			};
			try
			{
				bool flag = npc.hasSubspeciesTrait("wernicke_area");
				bool flag2 = npc.hasSubspeciesTrait("advanced_hippocampus");
				result.Broken = !flag;
				result.Flat = !npc.hasSubspeciesTrait("amygdala") && !npc.hasEmotions();
				result.Forgetful = !flag2;
				result.Mute = npc.hasTrait("mute") || (!flag && !flag2 && !npc.hasSubspeciesTrait("prefrontal_cortex"));
				string[] manners = Manners;
				foreach (string text in manners)
				{
					if (npc.hasTrait(text))
					{
						result.Manner = text;
						break;
					}
				}
			}
			catch
			{
			}
			int num = ((you != null) ? Chronicle.Opinion(npc, you) : 0);
			result.Warmth = ((num <= -45) ? (-2) : ((num <= -12) ? (-1) : ((num >= 70) ? 2 : ((num >= 30) ? 1 : 0))));
			result.Sovereign = Sovereign(npc, you);
			if (result.Sovereign)
			{
				result.Warmth = Math.Max(result.Warmth, (num > -45) ? 1 : 0);
			}
			return result;
		}

		public static bool Sovereign(Actor npc, Actor you)
		{
			try
			{
				return npc != null && you != null && npc != you && Rep.Reigns(you, npc.kingdom);
			}
			catch
			{
				return false;
			}
		}

		public static string Majesty(Actor you)
		{
			bool flag = Models.IsFemale(you);
			return Pick("Your Majesty", "Majesty", flag ? "my queen" : "my king", flag ? "my lady" : "sire");
		}

		public static string Speak(Voice v, string line)
		{
			if (string.IsNullOrEmpty(line))
			{
				return line;
			}
			if (v.Mute)
			{
				return Gesture(v, line);
			}
			if (v.Broken)
			{
				line = Broken(line);
			}
			if (v.Flat)
			{
				line = Emote.Replace(line, "").Replace("!", ".").Replace("...", ".");
				line = Spaces.Replace(line, " ").Trim();
				if (line.Length == 0)
				{
					line = "...";
				}
			}
			return line;
		}

		private static string Broken(string line)
		{
			string input = Emote.Replace(line, (Match m) => "\u0001" + m.Value + "\u0002");
			StringBuilder stringBuilder = new StringBuilder();
			string[] array = Regex.Split(input, "(\u0001[^\u0002]*\u0002)");
			foreach (string text in array)
			{
				if (text.StartsWith("\u0001"))
				{
					stringBuilder.Append(text.Trim('\u0001', '\u0002'));
					continue;
				}
				string input2 = Articles.Replace(text, "");
				input2 = Regex.Replace(input2, "\\bI'm\\b|\\bI am\\b", "me", RegexOptions.IgnoreCase);
				input2 = Regex.Replace(input2, "\\bI've\\b|\\bI'd\\b|\\bI'll\\b", "me", RegexOptions.IgnoreCase);
				input2 = Regex.Replace(input2, "\\bI\\b", "me");
				input2 = Regex.Replace(input2, "\\bmy\\b", "me", RegexOptions.IgnoreCase);
				input2 = Regex.Replace(input2, "\\byou're\\b", "you", RegexOptions.IgnoreCase);
				input2 = Regex.Replace(input2, "n't\\b", " no", RegexOptions.IgnoreCase);
				input2 = input2.Replace(", ", ". ");
				stringBuilder.Append(input2);
			}
			string text2 = Spaces.Replace(stringBuilder.ToString(), " ").Trim();
			if (text2.Length > 0)
			{
				text2 = char.ToUpper(text2[0]) + text2.Substring(1);
			}
			if (text2.Length != 0)
			{
				return text2;
			}
			return "Hrm.";
		}

		private static string Gesture(Voice v, string line)
		{
			int h = line.GetHashCode() & 0x7FFFFFFF;
			bool flag = line.Contains("?");
			bool num = line.Contains("died") || line.Contains("lost") || line.Contains("late ") || line.Contains("miss");
			bool flag2 = v.Warmth < 0 || line.Contains("enem") || line.Contains("war");
			if (num)
			{
				return Pick2(h, "*lowers its head*", "*makes a low, sad sound*", "*looks away for a long moment*");
			}
			if (flag2)
			{
				return Pick2(h, "*bares its teeth*", "*huffs and turns its shoulder to you*", "*stamps the ground*");
			}
			if (flag)
			{
				return Pick2(h, "*tilts its head*", "*blinks at you slowly*", "*points at you, then at itself*");
			}
			return Pick2(h, "*nods*", "*waves its arms about*", "*points off into the distance*", "*makes a rumbling sound*", "*shrugs*");
		}

		private static string Pick2(int h, params string[] options)
		{
			return options[h % options.Length];
		}

		public static string Address(Voice v, Actor npc, Actor you)
		{
			try
			{
				if (npc.lover == you)
				{
					return Pick("my love", "dear heart", "love");
				}
			}
			catch
			{
			}
			if (v.Sovereign)
			{
				return Majesty(you);
			}
			if (v.Warmth <= -2)
			{
				return Pick("you", "wretch", "outsider");
			}
			if (v.Warmth == -1)
			{
				return Pick("stranger", "you");
			}
			if (v.Manner == "peaceful" || v.Manner == "pacifist" || v.Manner == "honest")
			{
				return Pick("friend", "dear");
			}
			if (v.Manner == "evil" || v.Manner == "psychopath")
			{
				return Pick("little one", "fool");
			}
			if (v.Warmth >= 1)
			{
				return Pick("friend", NameOf(you));
			}
			return Pick("traveler", NameOf(you));
		}

		public static string Flourish(Voice v)
		{
			if (Rng.Next(3) != 0)
			{
				return "";
			}
			switch (v.Manner)
			{
			case "stupid":
				return Pick("Uh... ", "Um. ", "Huh? Oh. ");
			case "genius":
				return Pick("Fascinating question. ", "Let me think. ", "Well, strictly speaking, ");
			case "evil":
			case "psychopath":
				return Pick("Heh. ", "Mm. ", "How curious. ");
			case "savage":
			case "hotheaded":
				return Pick("What?! ", "Out with it. ", "Hah! ");
			case "deceitful":
				return Pick("Between us... ", "Trust me. ");
			case "ambitious":
				return Pick("Make it quick. ", "Time is precious. ");
			case "greedy":
				return Pick("For free? Fine. ", "Hm. ");
			case "wise":
				return Pick("Hm. ", "Ah. ");
			case "paranoid":
				return Pick("*glances over a shoulder* ", "Keep your voice down. ");
			case "honest":
			case "peaceful":
			case "pacifist":
				return Pick("Of course. ", "Oh, gladly. ");
			default:
				return "";
			}
		}
	}
}
