using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using FirstPerson.Net;
using FirstPerson.OneWorld;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Quests
	{
		private sealed class Briefing
		{
			public Quest Q;

			public readonly HashSet<string> Asked = new HashSet<string>();
		}

		private sealed class FireAsk
		{
			public string Question;

			public string[] Answers;

			public int Right;
		}

		private sealed class Posted
		{
			public string Raw;

			public int Day;

			public List<Quest> Notices;
		}

		internal struct LevelState
		{
			public int Level;

			public int Done1;

			public int Done2;

			public int Done3;
		}

		internal sealed class Ground
		{
			public int X;

			public int Y;

			public int Count;

			public int PredCount;

			public string Prey;

			public string Pred = "";

			public string Land;

			public bool Spawned;

			public readonly List<long> Ids = new List<long>();
		}

		internal sealed class HuntPlan
		{
			public bool Big;

			public readonly List<Ground> Grounds = new List<Ground>();
		}

		private sealed class UndeadMemo
		{
			public string Extra;

			public List<long> Ids;

			public long Straggler = -1L;

			public float StragglerAt = float.NegativeInfinity;
		}

		private const int WalkStay = 12;

		public const int VerseGood = 5;

		internal static int ForceCourtForTest = -1;

		private const double HoldAfterDone = 120.0;

		private static readonly Dictionary<long, double> _heldUntil = new Dictionary<long, double>();

		private static object _heldWorld;

		internal static bool ProposalForTest;

		internal static readonly string[][][] Verses = new string[3][][]
		{
			new string[4][]
			{
				new string[2] { "Soft as the light that falls on {place},", "Like the first warm morning after snow," },
				new string[2] { "I'd cross every road in {realm} for you,", "Let the whole of {town} hear me say it:" },
				new string[2] { "Roses are red, and so is my face,", "I tried to write this by the fire, but the fire won," },
				new string[2] { "Before I knew your name, I knew I'd find you,", "There is a quiet in me only you can reach," }
			},
			new string[4][]
			{
				new string[2] { "{name}, your laugh is the gentlest sound I know,", "your hands, your voice, the way you say good morning," },
				new string[2] { "no wolf, no war, no king could keep me from you,", "I want you plainly, as the river wants the sea," },
				new string[2] { "and though I rhyme the way a sheep might sing,", "I've practised this on three cows and a goat," },
				new string[2] { "I stood at {place} and the whole world went still,", "and every road I walked has led me here," }
			},
			new string[4][]
			{
				new string[2] { "so stay by me, and let the seasons turn.", "be mine, and I'll be gentle with your heart." },
				new string[2] { "so say the word, and I am yours tonight.", "so name the day, and I'll be there." },
				new string[2] { "I hope at least this made you smile. It did? Good.", "the end. Please clap. Or kiss me. Either will do." },
				new string[2] { "and all I thought of, all that time, was you.", "and I would choose you, every time, again." }
			}
		};

		internal static int LastVerseScore;

		private const int StarsStay = 24;

		private const int FireAsks = 3;

		private const int FireGood = 2;

		private const int ValorGood = 35;

		internal static bool NightForTest;

		private static float _showerUntil = -1f;

		private static float _goOrder;

		private static readonly Dictionary<long, float> _valorHealth = new Dictionary<long, float>();

		internal static int LastFireScore = -1;

		private static readonly List<long> _crowd = new List<long>();

		public const string Key = "fp_q";

		public const string BoardKey = "fp_bq";

		public const int MaxActive = 5;

		public static readonly List<Quest> Log = new List<Quest>();

		private static long _logFor = -1L;

		private static object _world;

		private static float _checkTimer;

		private static float _rebuildTimer;

		private static readonly Dictionary<long, double> KilledByYou = new Dictionary<long, double>();

		public static int Completed;

		public static int Failed;

		private static readonly StringBuilder Text = new StringBuilder(256);

		private const float MonthTime = 5f;

		public const string PassedTrialKey = "fp_trial_done";

		public const string DailyKey = "fp_dq";

		private const int NoticesADay = 9;

		private static readonly Dictionary<long, Posted> DailyCache = new Dictionary<long, Posted>();

		public const int DealsADay = 3;

		private const string DealsKey = "fp_deals";

		private static readonly string[] FoodIds = new string[5] { "berries", "wheat", "fish", "meat", "bread" };

		private static readonly HashSet<string> Strong = new HashSet<string> { "demon", "necromancer", "evil_mage", "cold_one", "fire_elemental", "greg", "tumor_monster_unit", "tumor_monster_animal", "mush_unit" };

		private static readonly HashSet<string> Beasts = new HashSet<string>
		{
			"wolf", "bear", "hyena", "crocodile", "snake", "scorpion", "sand_spider", "skeleton", "zombie", "zombie_human",
			"zombie_elf", "zombie_orc", "zombie_dwarf", "demon", "fire_skull", "jumpy_skull", "ghost", "acid_blob", "fire_elemental", "bandit",
			"greg", "evil_mage", "necromancer", "cold_one", "snowman", "tumor_monster_unit", "tumor_monster_animal", "mush_unit", "lil_pumpkin"
		};

		internal static int ToolsForTest = -1;

		internal static int ForceKindForTest = -1;

		internal static float MatchChanceForTest = -1f;

		private static float _escortOrder;

		public const string LevelKey = "fp_qlv";

		public const int ForLevel2 = 10;

		public const int ForLevel3 = 15;

		public const int TopLevel = 3;

		private static string _parsedRaw;

		private static LevelState _parsed;

		public const string LevelTownKey = "fp_lq";

		public const string HuntKey = "fp_hq";

		private const string HuntsDoneKey = "fp_hunts";

		private const float HerdComes = 40f;

		private static readonly string[] PreyIds = new string[16]
		{
			"cow", "buffalo", "rhino", "sheep", "goat", "alpaca", "ostrich", "capybara", "penguin", "turtle",
			"armadillo", "rabbit", "chicken", "monkey", "raccoon", "crab"
		};

		private static readonly string[] PredatorIds = new string[5] { "wolf", "bear", "hyena", "crocodile", "snake" };

		private static readonly string[] FallbackPrey = new string[4] { "sheep", "cow", "goat", "chicken" };

		private static readonly Dictionary<string, HuntPlan> Plans = new Dictionary<string, HuntPlan>();

		internal static int BigHuntForTest = -1;

		private const string PicksKey = "fp_picks";

		internal static readonly string[] DecentTraits = new string[19]
		{
			"strong", "tough", "fast", "agile", "eagle_eyed", "hard_skin", "boosted_vitality", "block", "dodge", "backstep",
			"battle_reflexes", "fire_proof", "freeze_proof", "poison_immune", "immune", "dash", "deflect_projectile", "strong_minded", "lucky"
		};

		internal static readonly string[] GoodTraits = new string[10] { "regeneration", "super_health", "healing_aura", "thorns", "bubble_defense", "energized", "giant", "genius", "titan_lungs", "blessed" };

		private const float GoodTraitChance = 0.15f;

		private static readonly string[] WeaponKinds = new string[5] { "sword", "axe", "spear", "hammer", "bow" };

		internal static bool GoodPickForTest;

		private const float RaidLeave = 30f;

		private const float RaidLeaveSeconds = 15f;

		private static float _raidAway;

		private static Quest _raidFor;

		internal static float RaidWarningForTest;

		internal static float RaidBetweenForTest;

		private static bool _raidHooked;

		private static long _bout;

		private static Actor _boutYou;

		private static long _vigilFor;

		private static int _blocksSeen;

		private static int _shotFrame = -100;

		private static float _boutRest;

		private static float _fireLit;

		private static float _fireKept = -1f;

		private static float _relitAt;

		private static float _vigilAway;

		private static float _vigilLate = -99f;

		private static float _noFireLogged = -99f;

		private static Vector2Int _fire = new Vector2Int(-1, -1);

		private static readonly FieldInfo SparDamage = typeof(AttackData).GetField("damage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static float _calmTick;

		private const string RetryKey = "fp_tw";

		private const float DeadRise = 18f;

		private const float SiteClear = 14f;

		private static readonly ConditionalWeakTable<Quest, UndeadMemo> Memos = new ConditionalWeakTable<Quest, UndeadMemo>();

		private static readonly List<Actor> _siteAlive = new List<Actor>();

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

		private static bool NightNow
		{
			get
			{
				float hourNow = HourNow;
				if (!(hourNow > 0.83f))
				{
					return hourNow < 0.2f;
				}
				return true;
			}
		}

		private static bool FixedSky
		{
			get
			{
				try
				{
					return WorldBoxMod.Instance != null && WorldBoxMod.Instance.Settings != null && WorldBoxMod.Instance.Settings.TimeMode != 0;
				}
				catch
				{
					return false;
				}
			}
		}

		private static bool Night
		{
			get
			{
				if (!NightForTest && !NightNow)
				{
					return FixedSky;
				}
				return true;
			}
		}

		private static bool Evening
		{
			get
			{
				if (!NightForTest && !Seats.Evening)
				{
					return FixedSky;
				}
				return true;
			}
		}

		internal static int CrowdForTest => _crowd.Count;

		public static double Now
		{
			get
			{
				try
				{
					return World.world.getCurWorldTime();
				}
				catch
				{
					return 0.0;
				}
			}
		}

		internal static bool BoutForTest => _bout != 0;

		internal static Vector2Int FireForTest => _fire;

		public static int MonthsFor(Quest q)
		{
			if (!IsTrial(q.Kind))
			{
				if (!IsCourt(q.Kind))
				{
					if (q.Kind != QuestKind.Meat)
					{
						if (q.Kind != QuestKind.Undead)
						{
							if (q.Kind != QuestKind.Raid)
							{
								if (q.Kind != QuestKind.Errand && q.Kind != QuestKind.Pilgrimage && q.Kind != QuestKind.Escort)
								{
									return 24;
								}
								return TravelMonths(q);
							}
							return 120;
						}
						return 48;
					}
					if (!IsBigHunt(q))
					{
						return 48;
					}
					return 96;
				}
				return CourtMonths(q);
			}
			return TrialMonths(q);
		}

		private static int TravelMonths(Quest q)
		{
			try
			{
				Actor actor = Chronicle.Find(q.Giver);
				if (actor == null)
				{
					return 36;
				}
				Vector2 vector;
				if (q.Kind == QuestKind.Errand)
				{
					Actor actor2 = Chronicle.Find(q.Target);
					if (actor2 == null)
					{
						return 36;
					}
					vector = actor2.current_position;
				}
				else
				{
					vector = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
				}
				float magnitude = (vector - actor.current_position).magnitude;
				float num = ((q.Kind == QuestKind.Escort) ? 0.3f : 0.4f);
				return Mathf.Max(36, Mathf.CeilToInt(magnitude * num));
			}
			catch
			{
				return 36;
			}
		}

		internal static bool Quick(QuestKind k)
		{
			if (k != QuestKind.Supply && k != QuestKind.Hunt && k != QuestKind.Meat && k != QuestKind.Craft && k != QuestKind.Undead)
			{
				return k == QuestKind.Raid;
			}
			return true;
		}

		private static void Brief(Conversation c, Quest q)
		{
			Briefing b = new Briefing
			{
				Q = q
			};
			Actor with = c.With;
			Actor you = c.You;
			c.Think(ThinkSeconds(q));
			string text = Doubts(q, with, you);
			if (Quick(q.Kind))
			{
				string[] array = InOneBreath(q, with);
				if (text != null)
				{
					ref string reference = ref array[array.Length - 1];
					reference = reference + "\n" + text;
				}
				c.Say(array);
				c.Offer(BriefChoices(c, b));
				return;
			}
			List<string> list = new List<string>(Pitch(q, with));
			list.Add(Plainly(q, with));
			if (text != null)
			{
				list.Add(text);
			}
			list.Add(Dialogue.Pick("Do you have any questions?", "Is anything unclear? Ask me.", "Any questions before you decide?"));
			c.Say(list.ToArray());
			c.Offer(BriefChoices(c, b));
		}

		private static List<Choice> BriefChoices(Conversation c, Briefing b)
		{
			return new List<Choice>
			{
				new Choice("I'll do it.", delegate
				{
					if (Quick(b.Q.Kind))
					{
						Shake(c, b.Q);
					}
					else
					{
						Recap(c, b);
					}
				}, ChoiceTone.Quest),
				new Choice("I have a question.", delegate
				{
					QuestionMenu(c, b);
				}),
				new Choice("Not now.", delegate
				{
					c.Say("I understand. If you change your mind...");
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Back)
			};
		}

		private static string[] InOneBreath(Quest q, Actor npc)
		{
			string text = Span(MonthsFor(q));
			string text2 = Pay(q);
			switch (q.Kind)
			{
			case QuestKind.Supply:
				return new string[1] { $"Our stores in {Place(q, npc)} are running low. Bring me {q.Need} {Res(q)} within {text}. For that, {text2}." };
			case QuestKind.Hunt:
				return new string[1] { "There's a " + q.TargetName + " prowling near here. Kill it within " + text + ". For that, " + text2 + "." };
			case QuestKind.Craft:
				return new string[1] { "My " + ItemWord(q.Thing) + " broke and I can't work without one. Make me a new one within " + text + ". For that, " + text2 + "." };
			case QuestKind.Meat:
			{
				Vector2 vector = npc?.current_position ?? new Vector2(q.X, q.Y);
				if (!IsBigHunt(q))
				{
					return new string[1] { $"The stores in {Place(q, npc)} need meat. There's game at {GroundsInWords(q, vector, all: true)}. Bring me {q.Need} meat within {text}. For that, {text2}." };
				}
				return new string[2]
				{
					Place(q, npc) + " needs more meat than one hunter can bring. I've marked three hunting grounds: " + GroundsInWords(q, vector, all: true) + ".",
					$"Bring me {q.Need} meat from those herds, not from anywhere else, within {text}. For that, {text2}."
				};
			}
			case QuestKind.Undead:
				return UndeadOneBreath(q, npc, text, text2);
			case QuestKind.Raid:
				return RaidOneBreath(q, npc, text2);
			default:
				return new string[1] { Plainly(q, npc) };
			}
		}

		private static string Pay(Quest q)
		{
			if (q.Kind == QuestKind.Raid)
			{
				return $"{q.Money} gold, a weapon from the smith with a power of your choosing, and a good word for you about town";
			}
			if (q.Kind == QuestKind.Undead && (LevelOf(WorldBoxMod.Instance?.Host).Done2 + 1) % 3 == 0)
			{
				return $"{q.Money} gold, a gift of your choosing, and a good word for you about town";
			}
			if (q.Bench && q.Renown > 0)
			{
				return $"{q.Money} gold, tools for your workbench and a good word for you about town";
			}
			if (q.Bench)
			{
				return $"{q.Money} gold and tools for your workbench";
			}
			if (q.Renown > 0)
			{
				return $"{q.Money} gold and a good word for you about town";
			}
			return $"{q.Money} gold";
		}

		private static float ThinkSeconds(Quest q)
		{
			if (Quick(q.Kind))
			{
				return 0.5f;
			}
			if (q.Kind != QuestKind.Errand && q.Kind != QuestKind.Faith)
			{
				return 1.5f;
			}
			return 1.2f;
		}

		private static string Plainly(Quest q, Actor npc)
		{
			if (IsCourt(q.Kind))
			{
				return CourtPlainly(q);
			}
			if (IsTrial(q.Kind))
			{
				return TrialPlainly(q);
			}
			string text = "within " + Span(MonthsFor(q));
			string text2 = Pay(q);
			switch (q.Kind)
			{
			case QuestKind.Errand:
				return "To be clear: find my " + q.Thing + " " + q.TargetName + " in " + q.Place + ", speak to them face to face, then come back to me, " + text + ". For that, " + text2 + ".";
			case QuestKind.Matchmaker:
				return "To be clear: talk to " + q.TargetName + " about me, and come tell me what they said, " + text + ". For that, " + text2 + ".";
			case QuestKind.Escort:
				return "To be clear: I follow you to " + q.Place + ", and we both get there alive, " + text + ". For that, " + text2 + ".";
			case QuestKind.Faith:
				return $"To be clear: {q.Need} more people turned to {q.Thing} by your preaching, {text}. For that, {text2}.";
			case QuestKind.Pilgrimage:
				return "To be clear: walk to " + q.Place + " where " + q.Thing + " began, then come back and tell me, " + text + ". For that, " + text2 + ".";
			case QuestKind.Investigation:
				return "To be clear: find who robbed me and face them, then come back to me, " + text + ". For that, " + text2 + ".";
			default:
				return "Do it " + text + ", and " + text2 + " is yours.";
			}
		}

		private static string Doubts(Quest q, Actor npc, Actor you)
		{
			try
			{
				if (IsTrial(q.Kind))
				{
					return TrialDoubt(q, you);
				}
				if (Log.Count >= 4)
				{
					return "*looks you over* You're carrying a lot of promises already. Can you take one more?";
				}
				if (q.Kind == QuestKind.Hunt || q.Kind == QuestKind.Investigation || q.Kind == QuestKind.CourtValor)
				{
					Actor actor = Chronicle.Find(q.Target);
					if (actor != null && actor.isAlive())
					{
						float num = (float)actor.getMaxHealth() * Mathf.Max(1f, ((BaseSimObject)actor).stats["damage"]);
						float num2 = (float)you.getMaxHealth() * Mathf.Max(1f, ((BaseSimObject)you).stats["damage"]);
						if (num > num2 * 1.6f)
						{
							return "*frowns* I'll be honest: the " + Who(q) + " is stronger than you. Bring friends, or a better weapon.";
						}
						if (num < num2 * 0.4f)
						{
							return "For someone like you, this should be quick work.";
						}
					}
				}
				if (q.Kind == QuestKind.Supply || q.Kind == QuestKind.CourtGift)
				{
					if (Civil.BagCount(you, q.Thing) >= q.Need)
					{
						return "*glances at your bag* You've already got " + Res(q) + " there. That would do nicely.";
					}
					if ((q.Thing == "wood" || q.Thing == "stone") && !HasToolFor(you, q.Thing))
					{
						return (q.Thing == "wood") ? "You'll want an axe for the trees. Bare hands will do, but slowly." : "You'll need a pickaxe or an axe for the stone.";
					}
				}
				if ((q.X >= 0 || q.Kind == QuestKind.Errand) && Heading(q, out var at) && (at - you.current_position).magnitude > 90f)
				{
					return "It's a long road. Take food with you.";
				}
			}
			catch
			{
			}
			return null;
		}

		private static bool HasToolFor(Actor you, string res)
		{
			try
			{
				string text = you.getWeapon()?.asset?.id ?? "";
				return text.StartsWith("axe_", StringComparison.Ordinal) || (res == "stone" && (text.StartsWith("pickaxe", StringComparison.Ordinal) || Civil.BagCount(you, "tw_item_pickaxe") > 0));
			}
			catch
			{
				return false;
			}
		}

		private static void QuestionMenu(Conversation c, Briefing b)
		{
			List<Choice> list = new List<Choice>();
			foreach (KeyValuePair<string, Func<string>> item in Questions(c, b.Q))
			{
				if (b.Asked.Contains(item.Key))
				{
					continue;
				}
				string key = item.Key;
				Func<string> answer = item.Value;
				list.Add(new Choice(key, delegate
				{
					b.Asked.Add(key);
					c.LastTopic = "quest question";
					c.Think(0.5f);
					string text;
					try
					{
						text = answer();
					}
					catch (Exception ex)
					{
						FirstPerson.Log.Error("quests: answer: " + ex.Message);
						text = "Hm. I'm not sure.";
					}
					c.Say(text, Dialogue.Pick("Anything else?", "What else would you know?", "Something else?"));
					QuestionMenu(c, b);
				}));
			}
			if (list.Count == 0)
			{
				list.Add(new Choice("That's everything.", delegate
				{
					c.Say("Then you know as much as I do.");
					c.Offer(BriefChoices(c, b));
				}, ChoiceTone.Back));
			}
			else
			{
				list.Add(new Choice("No more questions.", delegate
				{
					c.Say(Dialogue.Pick("Good. So, will you do it?", "Then what do you say?"));
					c.Offer(BriefChoices(c, b));
				}, ChoiceTone.Back));
			}
			c.Offer(list);
		}

		private static List<KeyValuePair<string, Func<string>>> Questions(Conversation c, Quest q)
		{
			Actor npc = c.With;
			Actor you = c.You;
			List<KeyValuePair<string, Func<string>>> list = new List<KeyValuePair<string, Func<string>>>();
			switch (q.Kind)
			{
			case QuestKind.Supply:
				Add("Where do I find " + Res(q) + "?", () => FindStuff(q.Thing));
				Add("Why does the town need it?", () => (!(q.Thing == "wood") && !(q.Thing == "stone")) ? ("The stores are low and there are mouths to feed in " + Place(q, npc) + ". A hungry winter is a dangerous one.") : ("We can't build without it. " + Place(q, npc) + " is growing faster than the stockpile."));
				break;
			case QuestKind.Hunt:
				Add("Where is it?", () => Where(q, npc, you));
				Add("How dangerous is it?", () => Danger(q, you));
				break;
			case QuestKind.Errand:
				Add("Where do I find " + q.TargetName + "?", () => "In " + q.Place + ". " + Where(q, npc, you) + " Ask anyone there; folk know each other.");
				Add("What should I tell them?", () => "That I'm well, that I think of them. That's all. They'll be glad of it.");
				break;
			case QuestKind.Craft:
				Add("How do I make a " + ItemWord(q.Thing) + "?", () => "Press Tab to see what you can craft, and what it needs. Wood you can work anywhere, stone and metal at your workbench at home. What you make goes into your bag. Bring it to me.");
				Add("Why not buy one?", () => "*laughs* With what? And the smith's queue is a season long.");
				break;
			case QuestKind.Matchmaker:
				Add("Who is " + q.TargetName + "?", () => q.TargetName + " lives here in " + Place(q, npc) + ". " + Where(q, npc, you));
				Add("What do I say?", () => "Just... tell them I'm sweet on them. Gently. And tell me exactly what they say back.");
				Add("What if they say no?", () => "*winces* If " + q.TargetName + " is already spoken for, that's the end of it. If they just say no... hearts change. Try again another day.");
				break;
			case QuestKind.Escort:
				Add("How far is it?", () => Where(q, npc, you));
				Add("What if we're attacked?", () => "Then fight. I'll stay close behind you. If I die, it's over, so keep between me and the trouble.");
				Add("How fast should we go?", () => "Walk; I'll keep up. Don't run off and leave me behind.");
				break;
			case QuestKind.Faith:
				Add("How do I win souls?", () => "Talk to people who don't follow " + q.Thing + " and preach to them. Some will listen, some won't.");
				Add("Who should I talk to?", () => "Folk without faith listen best. Those of another faith hold to it hard.");
				break;
			case QuestKind.Pilgrimage:
				Add("Where is " + q.Place + "?", () => Where(q, npc, you));
				Add("What do I do there?", () => "Walk into " + q.Place + ", see where " + q.Thing + " began. Then come home and tell me.");
				break;
			case QuestKind.Meat:
				MeatQuestions(q, npc, you, Add);
				break;
			case QuestKind.Undead:
				UndeadQuestions(q, npc, you, Add);
				break;
			case QuestKind.Raid:
				RaidQuestions(q, npc, you, Add);
				break;
			case QuestKind.Investigation:
				Add("Any clues?", () => Clue(q, npc, you));
				Add("What do I do when I find them?", () => "Tell them you know. Some give the money back. Some go for their knife, so be ready. Either way, come and tell me who it was.");
				break;
			default:
				if (IsCourt(q.Kind))
				{
					CourtQuestions(q, npc, you, Add);
				}
				if (IsTrial(q.Kind))
				{
					TrialQuestions(q, npc, you, Add);
				}
				break;
			}
			if (MonthsFor(q) > 0)
			{
				Add("How long do I have?", () => Upper(Span(MonthsFor(q))) + ". After that it's no use to me.");
			}
			Add("How will you know it's done?", () => Proof(q));
			if (IsTrial(q.Kind))
			{
				Add("Why should I pay you?", () => $"Because when we're done you'll be {Teachers.Word(q.Thing)}, and that lasts. {q.Money} gold is cheap for it.");
				Add("What if I fail?", () => "Then the fee's gone; you paid for my time, not for a pass. Come back after the next dawn and try again, if you've the gold.");
				return list;
			}
			Add("What's in it for me?", () => (!IsCourt(q.Kind)) ? ($"{q.Money} gold" + (q.Bench ? ", and tools for your workbench at home. Better tools, better work" : "") + ((q.Renown > 0) ? $", and people will hear of it ({q.Renown} renown)" : "") + ". And my thanks, which count for something here.") : Dialogue.Pick("*laughs* Me, maybe. If you're lucky.", "*smiles* A little more of my heart. Isn't that enough?"));
			Add("What if I can't do it?", () => "Then come and tell me honestly, and I'll find another way. I'd think less of you if you just vanished.");
			return list;
			void Add(string ask, Func<string> answer)
			{
				list.Add(new KeyValuePair<string, Func<string>>(ask, answer));
			}
		}

		private static string Proof(Quest q)
		{
			if (IsCourt(q.Kind))
			{
				return CourtProof(q);
			}
			if (IsTrial(q.Kind))
			{
				return TrialProof(q);
			}
			switch (q.Kind)
			{
			case QuestKind.Supply:
				return "Bring the " + Res(q) + " in your bag and hand it to me. I'll count it.";
			case QuestKind.Hunt:
				return "Word travels. Once the " + q.TargetName + " is dead, I'll know. Then come to me for your pay.";
			case QuestKind.Errand:
				return q.TargetName + " will send word back with you. Then come to me.";
			case QuestKind.Craft:
				return "Come to me with the " + ItemWord(q.Thing) + " in your hand. I'll look it over.";
			case QuestKind.Matchmaker:
				return "You'll tell me what they said, and I'll see it in their face soon enough.";
			case QuestKind.Escort:
				return "When we're both standing in " + q.Place + ", it's done. I'll pay you there.";
			case QuestKind.Faith:
				return $"I'll hear of each new soul. When there are {q.Need}, come to me.";
			case QuestKind.Pilgrimage:
				return "Once you've stood in " + q.Place + ", come back and tell me of it.";
			case QuestKind.Investigation:
				return "Face the thief. Then come back and tell me who it was.";
			case QuestKind.Meat:
				return MeatProof(q);
			case QuestKind.Undead:
				return UndeadProof(q);
			case QuestKind.Raid:
				return RaidProof(q);
			default:
				return "Come back to me when it's done.";
			}
		}

		private static void Recap(Conversation c, Briefing b)
		{
			Quest q = b.Q;
			c.LastTopic = "quest recap";
			c.Think(0.6f);
			c.Say(Dialogue.Pick("Good. Let's be sure we understand each other.", "Wait. Before we shake on it, let me say it back."), "You'll " + Goal(q) + ((MonthsFor(q) > 0) ? (", within " + Span(MonthsFor(q))) : "") + ". Then " + Then(q) + ", and " + Payoff(q) + ".", "Is that how you understood it?");
			c.Offer(new List<Choice>
			{
				new Choice("Yes, that's right.", delegate
				{
					Shake(c, q);
				}, ChoiceTone.Quest),
				new Choice("Not quite. I have a question.", delegate
				{
					QuestionMenu(c, b);
				}),
				new Choice("On second thought, no.", delegate
				{
					c.Say("*nods slowly* Better to say so now than later.");
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void Shake(Conversation c, Quest q)
		{
			if (Accept(q, c.You, c.Toast))
			{
				if (IsTrial(q.Kind))
				{
					TrialBegun(c, q);
					return;
				}
				c.Say(Dialogue.Pick("Then we have a deal. Thank you!", "Good. I knew I could count on you.", "Shake on it, then. Come back when it's done."));
			}
			else if (Log.Count < 5)
			{
				c.Say("*shakes head* Not without the fee. Come back when you have it.");
			}
			else
			{
				c.Say("You look busy enough already. Finish what you've promised others first.");
			}
			c.Offer(Dialogue.Topics(c));
		}

		public static string Promise(Quest q)
		{
			if (q.Kind == QuestKind.Bounty)
			{
				return $"The town's board pays {q.Money} gold for the {q.TargetName}. Kill it, then claim the reward at the board or from a leader of the town.";
			}
			if (q.Kind == QuestKind.Meat && q.FromBoard)
			{
				return TownHuntPromise(q);
			}
			if (q.Kind == QuestKind.Undead || q.Kind == QuestKind.Raid)
			{
				return LevelPromise(q);
			}
			return q.GiverName + "'s words: \"You'll " + Goal(q) + ". Then " + Then(q) + ", and " + Payoff(q) + ".\"";
		}

		public static string TimeLeft(Quest q)
		{
			if (q.Deadline <= 0.0)
			{
				return null;
			}
			double num = (q.Deadline - Now) / 5.0;
			if (num <= 0.0)
			{
				return "out of time";
			}
			if (num < 1.5)
			{
				return "under a month left";
			}
			if (num < 18.0)
			{
				return Mathf.RoundToInt((float)num) + " months left";
			}
			return Mathf.RoundToInt((float)(num / 12.0)) + " years left";
		}

		private static string Goal(Quest q)
		{
			if (IsCourt(q.Kind))
			{
				return CourtGoal(q);
			}
			if (IsTrial(q.Kind))
			{
				return TrialGoal(q);
			}
			switch (q.Kind)
			{
			case QuestKind.Supply:
				return $"bring me {q.Need} {Res(q)}";
			case QuestKind.Hunt:
				return "kill the " + q.TargetName;
			case QuestKind.Errand:
				return "find my " + q.Thing + " " + q.TargetName + " in " + q.Place + " and give them my word";
			case QuestKind.Craft:
				return "make me a " + ItemWord(q.Thing);
			case QuestKind.Matchmaker:
				return "speak to " + q.TargetName + " for me";
			case QuestKind.Escort:
				return "lead me safely to " + q.Place;
			case QuestKind.Faith:
				return $"win {q.Need} souls for {q.Thing}";
			case QuestKind.Pilgrimage:
				return "go to " + q.Place + ", where " + q.Thing + " began";
			case QuestKind.Investigation:
				return "find the one who robbed me";
			case QuestKind.Meat:
				return MeatGoal(q);
			case QuestKind.Undead:
				return "destroy every one of the dead at " + q.Place;
			case QuestKind.Raid:
				return "hold " + q.Place + " until every raider is dead or fled";
			default:
				return "help me";
			}
		}

		private static string Then(Quest q)
		{
			if (IsCourt(q.Kind))
			{
				return CourtThen(q);
			}
			if (IsTrial(q.Kind))
			{
				return TrialThen(q);
			}
			switch (q.Kind)
			{
			case QuestKind.Supply:
				return "you hand me the " + Res(q) + " and I count it";
			case QuestKind.Errand:
				return "you come back and tell me how " + q.TargetName + " is";
			case QuestKind.Craft:
				return "you put it in my hands";
			case QuestKind.Matchmaker:
				return "you tell me what they said";
			case QuestKind.Escort:
				return "once we're both standing in " + q.Place + ", I pay you";
			case QuestKind.Faith:
				return $"you come to me once there are {q.Need}";
			case QuestKind.Pilgrimage:
				return "you come back and tell me of it";
			case QuestKind.Investigation:
				return "you face the thief and tell me who it was";
			case QuestKind.Meat:
				return "you hand me the meat and I count it";
			case QuestKind.Undead:
			case QuestKind.Raid:
				return "you report to " + q.GiverName + "'s leader, a soldier or its board";
			default:
				return "you come back to me";
			}
		}

		private static string Res(Quest q)
		{
			return Work.ResourceName(q.Thing).ToLowerInvariant();
		}

		private static string Place(Quest q, Actor npc)
		{
			string text = q.Place;
			if (text == null)
			{
				if (npc.city == null)
				{
					return "town";
				}
				text = npc.city.name;
			}
			return text;
		}

		private static string Who(Quest q)
		{
			if (q.Kind != QuestKind.Investigation)
			{
				return q.TargetName;
			}
			return "thief";
		}

		private static string Upper(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpperInvariant(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static string Lower(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToLowerInvariant(s[0]) + s.Substring(1);
			}
			return s;
		}

		private static string Span(int months)
		{
			if (months % 12 != 0)
			{
				return months + " months";
			}
			if (months / 12 != 1)
			{
				if (months / 12 != 2)
				{
					if (months / 12 != 3)
					{
						return months / 12 + " years";
					}
					return "three years";
				}
				return "two years";
			}
			return "a year";
		}

		private static string Where(Quest q, Actor npc, Actor you = null)
		{
			if (!Heading(q, out var at))
			{
				return "I can't say where, exactly. Ask around.";
			}
			Vector2 current_position = (you ?? npc).current_position;
			Vector2 d = at - current_position;
			float magnitude = d.magnitude;
			if (magnitude < 8f)
			{
				return "Close by. Look around you.";
			}
			string text = ((magnitude < 30f) ? "a short walk" : ((magnitude < 70f) ? "a fair walk" : ((magnitude < 130f) ? "a long walk" : "a long journey")));
			return Compass(d) + " of here, " + text + ". Your compass will show the way.";
		}

		internal static string Compass(Vector2 d)
		{
			string[] obj = new string[8] { "East", "North-east", "North", "North-west", "West", "South-west", "South", "South-east" };
			int num = Mathf.RoundToInt((Mathf.Atan2(d.y, d.x) * 57.29578f + 360f) % 360f / 45f) % 8;
			return obj[num];
		}

		private static string Danger(Quest q, Actor you)
		{
			Actor actor = Chronicle.Find(q.Target);
			if (actor == null || !actor.isAlive())
			{
				return "Maybe it's already gone. Go and look.";
			}
			float num = (float)actor.getMaxHealth() * Mathf.Max(1f, ((BaseSimObject)actor).stats["damage"]);
			float b = (float)you.getMaxHealth() * Mathf.Max(1f, ((BaseSimObject)you).stats["damage"]);
			float num2 = num / Mathf.Max(1f, b);
			if (num2 > 1.6f)
			{
				return "Very. It's stronger than you. Fight it with friends, or from a distance with a bow.";
			}
			if (num2 > 0.8f)
			{
				return "About a match for you. Don't let it corner you.";
			}
			return "Not very, for you. But don't get careless.";
		}

		private static string Clue(Quest q, Actor npc, Actor you)
		{
			Actor actor = Chronicle.Find(q.Target);
			if (actor == null || !actor.isAlive())
			{
				return "The trail's gone cold. Ask around.";
			}
			string text = (Models.IsFemale(actor) ? "a woman" : "a man");
			string text2 = ((actor.city != null) ? (", from " + actor.city.name) : ", with no town to call home");
			return "It was " + text + text2 + ". Someone with quick hands. Last I heard, " + Lower(Where(q, npc, you));
		}

		private static string FindStuff(string res)
		{
			switch (res)
			{
			case "berries":
				return "Berry bushes, out past the fields. Pick them by hand when they're ripe.";
			case "wheat":
				return "In the fields at the edge of town. Gather it by hand when it's ripe.";
			case "wood":
				return "Any tree. Chop it with an axe, or your bare hands if you must.";
			case "stone":
				return "Rocks in the hills. Break them with a pickaxe or an axe.";
			default:
				return "Out there, if you look. Your bag will hold it.";
			}
		}

		public static bool IsCourt(QuestKind k)
		{
			if ((int)k < 10 || (int)k > 14)
			{
				return IsCourt2(k);
			}
			return true;
		}

		public static int CourtBit(QuestKind k)
		{
			return 1 << (int)(k - 10);
		}

		public static bool SpokenFor(Actor a)
		{
			if (a == null)
			{
				return false;
			}
			long id = a.data.id;
			object obj = World.world?.map_stats;
			if (obj == _world)
			{
				for (int i = 0; i < Log.Count; i++)
				{
					Quest quest = Log[i];
					if (quest.Giver == id && !quest.FromBoard && IsCourt(quest.Kind) && quest.Stage < 2 && (!(quest.Deadline > 0.0) || !(Now > quest.Deadline)))
					{
						return true;
					}
				}
			}
			if (_heldUntil.Count == 0)
			{
				return false;
			}
			if (obj != _heldWorld)
			{
				_heldWorld = obj;
				_heldUntil.Clear();
				return false;
			}
			if (_heldUntil.TryGetValue(id, out var value))
			{
				return Now < value;
			}
			return false;
		}

		private static void HoldAfter(Actor giver)
		{
			if (giver != null)
			{
				object obj = World.world?.map_stats;
				if (obj != _heldWorld)
				{
					_heldWorld = obj;
					_heldUntil.Clear();
				}
				_heldUntil[giver.data.id] = Now + 120.0;
			}
		}

		private static void ReleaseHold(long giver)
		{
			_heldUntil.Remove(giver);
		}

		public static void CourtChoices(Conversation c, Suitor s, List<Choice> list)
		{
			if (s.Done >= s.Wanted)
			{
				Quest quest = Find(c.With.data.id);
				if (quest == null || quest.Kind != QuestKind.CourtProposal)
				{
					list.Add(new Choice("Will you be mine?", delegate
					{
						AskHand(c);
					}, ChoiceTone.Romance));
				}
			}
			else
			{
				if (s.Affection < Romance.AskAtFor(c.With, c.You) && !s.Asked)
				{
					return;
				}
				Quest quest2 = Find(c.With.data.id);
				if (quest2 == null || !IsCourt(quest2.Kind))
				{
					list.Add(new Choice((s.Done == 0) ? "May I court you?" : "How else can I win your heart?", delegate
					{
						AskCourt(c);
					}, ChoiceTone.Romance));
				}
			}
		}

		private static void AskCourt(Conversation c)
		{
			c.LastTopic = "court";
			Actor with = c.With;
			Actor you = c.You;
			Suitor suitor = Romance.Of(with, you);
			if (Find(with.data.id) != null)
			{
				c.Say("First, what I asked of you before. Then ask me again.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			Quest quest = Read(with.data);
			if (quest != null && quest.Stage < 2 && quest.Holder != you.data.id)
			{
				c.Say("Not today. Someone's helping me with something, and it's all I can think about. Ask me again soon.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			if (Log.Count >= 5)
			{
				c.Say($"You've promised {5} people already. Come back when you've time to give me.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			Quest quest2 = MakeCourt(with, you, suitor);
			if (quest2 == null)
			{
				c.Think(0.6f);
				c.Say("*thinks* Not now. Ask me again soon.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			if (!suitor.Asked)
			{
				suitor.Asked = true;
				Romance.Save(with, suitor);
			}
			Rivals.Flirted(you, with);
			Brief(c, quest2);
		}

		private static void AskHand(Conversation c)
		{
			c.LastTopic = "court yes";
			Actor with = c.With;
			Actor you = c.You;
			FlirtOutcome? flirtOutcome = Romance.Blocker(you, with);
			if (flirtOutcome.HasValue)
			{
				c.Say(Dialogue.Flirt(with, you, flirtOutcome.Value));
				c.Offer(Dialogue.Topics(c));
				return;
			}
			if (!Romance.WouldSayYes(with, you))
			{
				c.Say("*smiles* Not yet. You're not done courting me.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			c.Think(0.8f);
			Rivals.Flirted(you, with);
			if (!ProposalForTest)
			{
				Quest quest = Find(with.data.id);
				if (quest != null && quest.Kind != QuestKind.CourtProposal)
				{
					c.Say("First, what I asked of you before.");
					c.Offer(Dialogue.Topics(c));
					return;
				}
				quest = MakeProposal(with, you);
				if (quest == null || !Accept(quest, you, c.Toast))
				{
					c.Say("*smiles* Ask me again soon. Properly.");
					c.Offer(Dialogue.Topics(c));
					return;
				}
				c.Say(Dialogue.Pick("*laughs softly* Not like this. Ask me properly.", "*takes your hands, then lets go* Yes is waiting. But ask me properly."), "A ring. Made by you. Then ask me at " + quest.Place + ", where I love to be.");
				c.Romantic = true;
				c.Offer(Dialogue.Topics(c));
			}
			else if (!Married(c, null))
			{
				c.Say("*hesitates* I... something's wrong. Ask me another time.");
				c.Offer(Dialogue.Topics(c));
			}
		}

		private static bool Married(Conversation c, string line)
		{
			Actor with = c.With;
			Actor you = c.You;
			if (Romance.Wed(you, with))
			{
				string leftBehind = Romance.LeftBehind;
				if (leftBehind != null)
				{
					c.Say(Dialogue.Pick("*takes both your hands* Yes. I'll tell " + leftBehind + " tonight.", "*long breath* Yes. " + leftBehind + " will hate me. I don't care.", "*kisses you* Yes. It was over with " + leftBehind + " long before you came."));
				}
				else
				{
					c.Say(line ?? Dialogue.Pick("*takes both your hands* Yes. I've been waiting for you to ask.", "Yes! Yes. *laughs and wipes an eye* Of course, yes.", "*kisses you* Does that answer it?"));
				}
				c.Romantic = true;
				c.Hearts?.Invoke(with);
				c.Relation = Dialogue.RelationTo(with, you);
				c.Toast?.Invoke((leftBehind != null) ? (Dialogue.NameOf(with) + " left " + leftBehind + " for you") : (Dialogue.NameOf(with) + " is now your lover"));
				c.Offer(Dialogue.Topics(c));
				return true;
			}
			return false;
		}

		private static Quest MakeCourt(Actor giver, Actor you, Suitor s)
		{
			if (ForceCourtForTest >= 0)
			{
				return MakeCourtKind((QuestKind)ForceCourtForTest, giver, you);
			}
			QuestKind[] array;
			switch (Romance.Likes(giver))
			{
			case Charm.Sweet:
				array = new QuestKind[8]
				{
					QuestKind.CourtWalk,
					QuestKind.CourtStars,
					QuestKind.CourtGift,
					QuestKind.CourtFire,
					QuestKind.CourtVerse,
					QuestKind.CourtBlessing,
					QuestKind.CourtRival,
					QuestKind.CourtValor
				};
				break;
			case Charm.Bold:
				array = new QuestKind[8]
				{
					QuestKind.CourtValor,
					QuestKind.CourtRival,
					QuestKind.CourtWalk,
					QuestKind.CourtFire,
					QuestKind.CourtGift,
					QuestKind.CourtStars,
					QuestKind.CourtBlessing,
					QuestKind.CourtVerse
				};
				break;
			case Charm.Witty:
				array = new QuestKind[8]
				{
					QuestKind.CourtVerse,
					QuestKind.CourtFire,
					QuestKind.CourtGift,
					QuestKind.CourtWalk,
					QuestKind.CourtStars,
					QuestKind.CourtRival,
					QuestKind.CourtValor,
					QuestKind.CourtBlessing
				};
				break;
			default:
				array = new QuestKind[8]
				{
					QuestKind.CourtStars,
					QuestKind.CourtVerse,
					QuestKind.CourtFire,
					QuestKind.CourtBlessing,
					QuestKind.CourtWalk,
					QuestKind.CourtGift,
					QuestKind.CourtValor,
					QuestKind.CourtRival
				};
				break;
			}
			System.Random random = new System.Random((int)(giver.data.id * 7 + 3));
			for (int num = 4; num > 0; num--)
			{
				int num2 = random.Next(num + 1);
				QuestKind questKind = array[num];
				array[num] = array[num2];
				array[num2] = questKind;
			}
			for (int i = 0; i < 2; i++)
			{
				QuestKind[] array2 = array;
				for (int j = 0; j < array2.Length; j++)
				{
					QuestKind questKind2 = array2[j];
					if ((i != 0 || (s.Kinds & CourtBit(questKind2)) == 0) && (i != 1 || questKind2 != QuestKind.CourtBlessing) && !Retired(questKind2))
					{
						Quest quest = null;
						try
						{
							quest = MakeCourtKind(questKind2, giver, you);
						}
						catch (Exception ex)
						{
							FirstPerson.Log.Error("quests: court " + questKind2.ToString() + ": " + ex.Message);
						}
						if (quest != null)
						{
							return quest;
						}
					}
				}
			}
			return null;
		}

		internal static Quest MakeCourtKind(QuestKind kind, Actor giver, Actor you)
		{
			Quest quest = New(kind, giver, 0, 1, 8, 10);
			switch (kind)
			{
			case QuestKind.CourtGift:
				quest.Thing = ((Romance.Likes(giver) == Charm.Bold) ? "sword_" : "amulet_");
				quest.Need = 1;
				return quest;
			case QuestKind.CourtWalk:
				if (!FindSpot(giver, 10f, 32f, Romance.FavouritePlace(giver), out quest.X, out quest.Y, out quest.Place))
				{
					return null;
				}
				return quest;
			case QuestKind.CourtVerse:
				if (!FindSpot(giver, 18f, 60f, Romance.FavouritePlace(giver), out quest.X, out quest.Y, out quest.Place))
				{
					return null;
				}
				return quest;
			case QuestKind.CourtValor:
			{
				Actor actor2 = BeastNear(giver, you, 50f);
				if (actor2 == null)
				{
					return null;
				}
				quest.Target = actor2.data.id;
				quest.TargetName = actor2.asset.id.Replace('_', ' ');
				quest.Xp = 14;
				quest.Renown = 3;
				return quest;
			}
			case QuestKind.CourtBlessing:
			{
				string role;
				Actor actor = Guardian(giver, you, out role);
				if (actor == null)
				{
					return null;
				}
				quest.Target = actor.data.id;
				quest.TargetName = Dialogue.NameOf(actor);
				quest.Thing = role;
				quest.Place = ((actor.city != null) ? actor.city.name : null);
				return quest;
			}
			default:
				if (!IsCourt2(kind))
				{
					return null;
				}
				return MakeCourt2(kind, giver, you);
			}
		}

		private static Actor BeastNear(Actor giver, Actor you, float range)
		{
			Vector2 current_position = giver.current_position;
			Actor result = null;
			float num = range * range;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && Beasts.Contains(simple.asset.id) && (simple.kingdom == null || giver.kingdom == null || simple.kingdom != giver.kingdom))
				{
					float sqrMagnitude = (simple.current_position - current_position).sqrMagnitude;
					if (!(sqrMagnitude >= num) && Law.Lawful(simple, you) && sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = simple;
					}
				}
			}
			return result;
		}

		private static Actor Guardian(Actor giver, Actor you, out string role)
		{
			role = null;
			long[] array = new long[2]
			{
				giver.data.parent_id_1,
				giver.data.parent_id_2
			};
			foreach (long num in array)
			{
				Actor actor = ((num > 0) ? Chronicle.Find(num) : null);
				if (CanBless(actor) && actor != you)
				{
					role = (Models.IsFemale(actor) ? "mother" : "father");
					return actor;
				}
			}
			try
			{
				Actor actor2 = ((giver.city != null) ? giver.city.leader : null);
				if (CanBless(actor2) && actor2 != giver && actor2 != you)
				{
					role = "town's leader";
					return actor2;
				}
				Actor actor3 = ((giver.kingdom != null && giver.kingdom.isCiv()) ? giver.kingdom.king : null);
				if (CanBless(actor3) && actor3 != giver && actor3 != you)
				{
					role = (Models.IsFemale(actor3) ? "queen" : "king");
					return actor3;
				}
			}
			catch
			{
			}
			return null;
		}

		private static bool CanBless(Actor a)
		{
			try
			{
				return a != null && a.isAlive() && Dialogue.IsPerson(a) && !a.isBaby();
			}
			catch
			{
				return false;
			}
		}

		private static bool FindSpot(Actor giver, float near, float far, string want, out int sx, out int sy, out string name)
		{
			sx = (sy = -1);
			name = null;
			WorldTile tile = World.world.GetTile((int)giver.current_position.x, (int)giver.current_position.y);
			if (tile == null)
			{
				return false;
			}
			System.Random random = new System.Random((int)(giver.data.id * 17 + Chronicle.Year));
			int homeH = ((tile.main_type != null) ? tile.main_type.height_min : 0);
			float num = float.MinValue;
			for (int i = 0; i < 700; i++)
			{
				float f = (float)(random.NextDouble() * Math.PI * 2.0);
				float num2 = near + (float)random.NextDouble() * (far - near);
				WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * num2), tile.y + (int)(Mathf.Sin(f) * num2));
				if (!Walkable(tile2))
				{
					continue;
				}
				try
				{
					if (!tile2.isSameIsland(tile))
					{
						continue;
					}
				}
				catch
				{
				}
				string text = SpotKind(tile2, homeH);
				float num3 = ((text == want) ? 30f : ((text != null) ? 12f : 0f)) - Mathf.Abs(num2 - (near + far) * 0.5f) * 0.2f + (float)random.NextDouble();
				if (!(num3 <= num))
				{
					num = num3;
					sx = tile2.x;
					sy = tile2.y;
					object obj2;
					switch (text)
					{
					default:
						obj2 = "the quiet fields";
						break;
					case "woods":
						obj2 = "the deep woods";
						break;
					case "shore":
						obj2 = "the water's edge";
						break;
					case "hill":
						obj2 = "the hilltop";
						break;
					case "flowers":
						obj2 = "the flower meadow";
						break;
					}
					name = (string)obj2;
				}
			}
			return sx >= 0;
		}

		private static bool Walkable(WorldTile t)
		{
			if (t == null || t.main_type == null || t.main_type.liquid || t.main_type.block)
			{
				return false;
			}
			if (t.zone != null && t.zone.city != null)
			{
				return false;
			}
			if (t.building != null)
			{
				return !AssetStyle.IsSolid(t.building);
			}
			return true;
		}

		private static string SpotKind(WorldTile t, int homeH)
		{
			int num = 0;
			bool flag = false;
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					WorldTile tile = World.world.GetTile(t.x + j, t.y + i);
					if (tile != null && tile.main_type != null)
					{
						if (tile.main_type.liquid && Mathf.Abs(j) <= 1 && Mathf.Abs(i) <= 1)
						{
							flag = true;
						}
						if (tile.building != null && tile.building.asset != null && tile.building.asset.building_type == BuildingType.Building_Tree)
						{
							num++;
						}
					}
				}
			}
			if (flag)
			{
				return "shore";
			}
			try
			{
				if (t.building != null && t.building.asset != null && t.building.asset.id.Contains("flower"))
				{
					return "flowers";
				}
			}
			catch
			{
			}
			if (num >= 7)
			{
				return "woods";
			}
			if (t.main_type.height_min > homeH)
			{
				return "hill";
			}
			return null;
		}

		private static void CheckCourt(Quest q, Actor you, Actor giver, Actor target, Action<string> toast)
		{
			if (IsCourt2(q.Kind))
			{
				CheckCourt2(q, you, giver, target, toast);
				return;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				q.Have = (IsPieceGift(q) ? ((BagPieceLike(you, q.Thing) >= 0) ? 1 : 0) : Civil.BagCount(you, q.Thing));
				if (q.Stage == 0 && q.Have >= q.Need)
				{
					q.Stage = 1;
				}
				else if (q.Stage == 1 && q.Have < q.Need)
				{
					q.Stage = 0;
				}
				break;
			case QuestKind.CourtWalk:
			{
				if (giver == null)
				{
					break;
				}
				Vector2 vector = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
				if ((giver.current_position - vector).magnitude < 5f && (you.current_position - vector).magnitude < 5f)
				{
					if (++q.Have >= 12)
					{
						EndEscort(giver);
						Reward(q, you, toast);
						WorldBoxMod.Instance?.Talk.Remark(giver, Dialogue.Pick("*leans on your shoulder* I'd forgotten how lovely " + q.Place + " is.", "*quietly* Let's stay a little longer.", "*takes your hand* Thank you. I needed this."));
						break;
					}
				}
				else
				{
					q.Have = 0;
				}
				Escort(giver, you);
				break;
			}
			case QuestKind.CourtValor:
				if (q.Stage == 0 && target != null)
				{
					WatchValor(q, you, giver, target);
				}
				if (q.Stage != 0 || target != null)
				{
					break;
				}
				if (!YouKilled(q.Target))
				{
					Fail(q, you, "someone else killed it", toast);
					break;
				}
				q.Need = ValorScore(q, you, giver);
				if (giver != null)
				{
					EndEscort(giver);
				}
				if (q.Need < 35)
				{
					Fail(q, you, (q.Extra == "ran") ? "they saw you run" : "they weren't impressed", toast);
				}
				else
				{
					q.Stage = 1;
				}
				break;
			case QuestKind.CourtBlessing:
				if (q.Stage == 0 && target == null)
				{
					Fail(q, you, q.TargetName + " is gone", toast);
				}
				break;
			case QuestKind.CourtVerse:
				if (q.Stage == 0 && (you.current_position - new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f)).magnitude < 6f)
				{
					q.Stage = 1;
				}
				break;
			}
		}

		private static void Courted(Quest q, Actor you, Action<string> toast)
		{
			Actor actor = Chronicle.Find(q.Giver);
			try
			{
				if (q.Renown > 0)
				{
					you.addRenown(q.Renown);
				}
				if (q.Xp > 0)
				{
					you.addExperience(q.Xp);
				}
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: courted: " + ex.Message);
			}
			Suitor suitor = null;
			if (actor != null)
			{
				try
				{
					actor.changeHappiness("just_received_gift");
				}
				catch
				{
				}
				Chronicle.ChangeOpinion(actor, you, q.Opinion);
				Chronicle.Note(actor, you, 0, 0, 1);
				Romance.TaskDone(actor, you, CourtBit(q.Kind));
				suitor = Romance.Of(actor, you);
				HoldAfter(actor);
				WorldBoxMod.Instance?.Converse.Hearts?.Invoke(actor);
			}
			q.Stage = 2;
			Save(q);
			Drop(q);
			Completed++;
			string text = ((suitor == null) ? "" : ((suitor.Done >= suitor.Wanted) ? (". Ask " + q.GiverName + " to be yours") : $" ({suitor.Done} of {suitor.Wanted})"));
			toast?.Invoke("Courting " + q.GiverName + ": " + CourtWhat(q) + " done" + text);
			FirstPerson.Log.Info("quests: courted " + q.GiverName + " with " + q.Kind.ToString() + ((suitor != null) ? $" ({suitor.Done}/{suitor.Wanted}, affection {suitor.Affection})" : ""));
			WorldBoxMod.Instance?.Audio.PlayRustle();
		}

		private static void CourtLost(Quest q, Actor you)
		{
			ReleaseHold(q.Giver);
			Actor actor = Chronicle.Find(q.Giver);
			if (actor != null && you != null)
			{
				Romance.SetBack(actor, you, 10);
			}
		}

		private static void CourtProgress(Conversation c, Quest q)
		{
			c.LastTopic = "court";
			Actor you = c.You;
			Actor npc = c.With;
			List<Choice> list = new List<Choice>();
			if (CourtProgress2(c, q, list, out var offered))
			{
				if (offered)
				{
					return;
				}
			}
			else if (q.Kind == QuestKind.CourtGift && IsPieceGift(q))
			{
				PieceGiftProgress(c, q, list);
			}
			else
			{
				switch (q.Kind)
				{
				case QuestKind.CourtGift:
				{
					int num = Civil.BagCount(you, q.Thing);
					if (num >= q.Need)
					{
						list.Add(new Choice($"For you: {q.Need} {Res(q)}.", delegate
						{
							if (Civil.TakeFromBag(you, q.Thing, q.Need))
							{
								try
								{
									npc.addToInventory(q.Thing, q.Need);
								}
								catch
								{
								}
								Reward(q, you, c.Toast);
								c.Say(Dialogue.Pick("*holds the " + Res(q) + " close* You remembered.", "*eyes shining* For me? All of it?", "No one's ever paid me this much mind."));
								c.Romantic = true;
							}
							else
							{
								c.Say("Wait... that's not all of it.");
							}
							c.Offer(Dialogue.Topics(c));
						}, ChoiceTone.Romance));
						c.Say("*glances at your bag* Is that... for me?");
					}
					else
					{
						c.Say($"*smiles* I'm still waiting for my {Res(q)}, you know. {q.Need - num} more.");
					}
					break;
				}
				case QuestKind.CourtVerse:
					if (q.Stage == 1)
					{
						Choice choice = new Choice("I wrote you something.", delegate
						{
							Recite(c, q, 0, 0, "");
						}, ChoiceTone.Romance);
						if (Now < q.Wait)
						{
							choice.Off("Give them a little time first.");
						}
						list.Add(choice);
						c.Say("You went to " + q.Place + "? *sits down* Then let me hear it.");
					}
					else
					{
						c.Say("Go and see " + q.Place + " first. Then write me something about it.");
					}
					break;
				case QuestKind.CourtWalk:
					c.Say(Dialogue.Pick("I'm ready when you are. Lead the way.", Upper(q.Place) + "? I'll follow you there."));
					break;
				default:
					if (q.Stage == 1)
					{
						Reward(q, you, c.Toast);
						c.Say((q.Kind != QuestKind.CourtValor) ? Dialogue.Pick("My " + q.Thing + " said yes? *laughs* I never thought they would!", "*hugs you* You spoke to my " + q.Thing + ". That took courage.") : ((q.Need >= 80) ? Dialogue.Pick("*eyes shining* The " + q.TargetName + ", and not a scratch on you!", "*throws arms round you* I watched the whole thing. You were magnificent.") : ((q.Need >= 55) ? Dialogue.Pick("*checks your cuts* You fool. You brave fool. Thank you.", "*throws arms round you* You could have been killed. Thank you.") : Dialogue.Pick("*winces at your wounds* You won. Barely. But you won.", "*quietly* That was too close. But you did it."))));
						c.Romantic = true;
						c.Offer(Dialogue.Topics(c));
						return;
					}
					c.Say((q.Kind == QuestKind.CourtValor) ? ("The " + q.TargetName + " still lives. I'll wait.") : ("Have you spoken to my " + q.Thing + ", " + q.TargetName + "? *bites lip*"));
					break;
				}
			}
			list.Add(new Choice("I can't do it after all.", delegate
			{
				Abandon(q, you);
				c.Say("*looks away* Oh. I see.");
				c.Offer(Dialogue.Topics(c));
			}, ChoiceTone.Risk));
			list.Add(Dialogue.Back(c));
			c.Offer(list);
		}

		private static void Bless(Conversation c, Quest q)
		{
			c.LastTopic = "blessing";
			Actor elder = c.With;
			Actor you = c.You;
			c.Think(1f);
			int num = Chronicle.Opinion(elder, you);
			bool flag = Dialogue.HasTrait(elder, "peaceful") || Dialogue.HasTrait(elder, "content") || Dialogue.HasTrait(elder, "honest");
			Actor actor = Chronicle.Find(q.Giver);
			string text = (((!(q.Thing == "mother") && !(q.Thing == "father")) || actor == null) ? q.GiverName : (Models.IsFemale(actor) ? "my daughter" : "my son"));
			if (num >= 20 || (flag && num >= 0))
			{
				Blessed(c, q, "*looks you up and down, then nods* " + Upper(text) + " could do worse. You have my blessing.");
			}
			else if (num >= -15 && !Dialogue.Enemies(elder, you))
			{
				int price = Dowry(q);
				c.Say("Court " + text + "? *folds arms* What do you have to offer?", $"Show me you can provide. {price} gold, for the household, and you have my blessing.");
				Choice choice = new Choice($"Here: {price} gold.", delegate
				{
					try
					{
						if (you.money < price)
						{
							c.Say("That's not enough.");
							c.Offer(Dialogue.Topics(c));
							return;
						}
						you.spendMoney(price);
						elder.addMoney(price);
					}
					catch
					{
					}
					Blessed(c, q, "*counts it, then clasps your hand* Then you have my blessing. Treat them well.");
				}, ChoiceTone.Romance);
				int num2 = Economy.Money(you);
				if (num2 < price)
				{
					choice.Off($"You have only {num2} gold.");
				}
				c.Offer(new List<Choice>
				{
					choice,
					new Choice("I'll come back.", delegate
					{
						c.Say("Do.");
						c.Offer(Dialogue.Topics(c));
					}, ChoiceTone.Back)
				});
			}
			else
			{
				c.Say("You? Court " + text + "? *scoffs* Earn my trust first. Then we'll talk.");
				c.Offer(Dialogue.Topics(c));
			}
		}

		private static int Dowry(Quest q)
		{
			if (!(q.Thing == "king") && !(q.Thing == "queen"))
			{
				if (!(q.Thing == "town's leader"))
				{
					return 40;
				}
				return 60;
			}
			return 120;
		}

		private static void Blessed(Conversation c, Quest q, string line)
		{
			q.Stage = 1;
			Save(q);
			Chronicle.ChangeOpinion(c.With, c.You, 5);
			c.Say(line);
			c.Toast?.Invoke("Courting " + q.GiverName + ": go back to " + q.GiverName);
			c.Offer(Dialogue.Topics(c));
		}

		private static void Recite(Conversation c, Quest q, int round, int score, string sofar)
		{
			c.LastTopic = "poem";
			Actor with = c.With;
			if (round >= 3)
			{
				Judge(c, q, score, sofar);
				return;
			}
			Charm charm = Romance.Likes(with);
			List<Charm> list = new List<Charm>
			{
				Charm.Sweet,
				Charm.Bold,
				Charm.Witty,
				Charm.Deep
			};
			list.Remove(charm);
			System.Random random = new System.Random((int)(with.data.id * 7 + round * 131 + (long)Now));
			list.RemoveAt(random.Next(list.Count));
			list.Insert(random.Next(list.Count + 1), charm);
			List<Choice> list2 = new List<Choice>();
			foreach (Charm item in list)
			{
				string text = Verses[round][(uint)item][random.Next(2)];
				string line = Fill(text, q, with);
				int worth = ((item == charm) ? 2 : ((item == Romance.Dislikes(with)) ? (-1) : 0)) + (text.Contains("{") ? 1 : 0);
				list2.Add(new Choice(line, delegate
				{
					Recite(c, q, round + 1, score + worth, (sofar.Length == 0) ? line : (sofar + "\n" + line));
				}, ChoiceTone.Romance));
			}
			c.SayAsIs((round == 0) ? "*they wait, chin on hand*" : ("*you read*\n" + sofar));
			c.Offer(list2);
		}

		internal static string Fill(string raw, Quest q, Actor npc)
		{
			return raw.Replace("{place}", q.Place ?? "the hills").Replace("{name}", q.GiverName).Replace("{realm}", Dialogue.KingdomName(npc) ?? "the land")
				.Replace("{town}", Dialogue.CityName(npc) ?? "the land");
		}

		private static void Judge(Conversation c, Quest q, int score, string poem)
		{
			LastVerseScore = score;
			FirstPerson.Log.Info($"quests: poem for {q.GiverName} scored {score} (needs {5})");
			c.Think(1.2f);
			if (score >= 5)
			{
				Reward(q, c.You, c.Toast);
				c.Say((score >= 7) ? "*silent a long moment, then kisses your cheek* No one has ever written me anything." : "*eyes shining* That was lovely. Truly lovely.");
				c.Romantic = true;
				c.Offer(Dialogue.Topics(c));
			}
			else
			{
				q.Wait = Now + 20.0;
				Save(q);
				Suitor suitor = Romance.Of(c.With, c.You);
				c.Say("*smiles kindly* It's sweet. It just isn't... me.", suitor.Knows ? "Think about what I told you I like. Try again another day." : "Ask me what I like, sometime. Then try again.");
				c.Offer(Dialogue.Topics(c));
			}
		}

		private static string CourtWhat(Quest q)
		{
			string text = CourtWhat2(q);
			if (text != null)
			{
				return text;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				if (!IsPieceGift(q))
				{
					return $"{q.Need} {Res(q)}";
				}
				return PieceWord(q, article: true) + " made by you";
			case QuestKind.CourtWalk:
				return "a walk to " + q.Place;
			case QuestKind.CourtValor:
				return "the " + q.TargetName + " slain";
			case QuestKind.CourtBlessing:
				return "the " + q.Thing + "'s blessing";
			case QuestKind.CourtVerse:
				return "a poem about " + q.Place;
			default:
				return "a task";
			}
		}

		private static string CourtTitle(Quest q)
		{
			return "Courting " + q.GiverName + ": " + CourtWhat(q);
		}

		private static string CourtStatus(Quest q)
		{
			string text = CourtStatus2(q);
			if (text != null)
			{
				return text;
			}
			if (q.Kind == QuestKind.CourtGift && IsPieceGift(q))
			{
				if (q.Have <= 0)
				{
					return "Make " + PieceWord(q, article: true) + " at the workbench";
				}
				return "Give " + PieceWord(q, article: false) + " to " + q.GiverName;
			}
			if (q.Stage == 1)
			{
				if (q.Kind != QuestKind.CourtGift)
				{
					if (q.Kind != QuestKind.CourtVerse)
					{
						return "Go back to " + q.GiverName;
					}
					return "Recite your poem to " + q.GiverName;
				}
				return "Bring the " + Res(q) + " to " + q.GiverName;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				return $"{Mathf.Min(q.Have, q.Need)}/{q.Need} {Res(q)} in your bag";
			case QuestKind.CourtWalk:
				if (q.Have <= 0)
				{
					return "Lead " + q.GiverName + " to " + q.Place;
				}
				return "Stay a while at " + q.Place;
			case QuestKind.CourtValor:
				return "Kill the " + q.TargetName + " yourself";
			case QuestKind.CourtBlessing:
				return "Ask " + q.TargetName + " for their blessing";
			case QuestKind.CourtVerse:
				return "Go and see " + q.Place;
			default:
				return "";
			}
		}

		private static bool CourtHeading(Quest q, out Vector2 at)
		{
			if (IsCourt2(q.Kind))
			{
				return CourtHeading2(q, out at);
			}
			at = default(Vector2);
			switch (q.Kind)
			{
			case QuestKind.CourtWalk:
			case QuestKind.CourtVerse:
				if (q.X >= 0)
				{
					at = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
					return true;
				}
				break;
			case QuestKind.CourtValor:
			case QuestKind.CourtBlessing:
			{
				Actor actor = Chronicle.Find(q.Target);
				if (actor != null)
				{
					at = actor.current_position;
					return true;
				}
				break;
			}
			}
			return false;
		}

		private static string[] CourtPitch(Quest q, Actor npc)
		{
			Actor actor = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Host : null);
			Suitor suitor = ((actor != null) ? Romance.Of(npc, actor) : null);
			string text = ((suitor != null && suitor.Done > 0) ? Dialogue.Pick("*laughs softly* You're not done yet.", "*smiles* Still here? Good.") : Dialogue.Pick("*smiles slowly* Court me? Then show me you mean it.", "*blushes* I hoped you'd ask. But words are easy."));
			if (actor != null && Romance.Taken(npc, actor))
			{
				text = text + " " + Dialogue.Pick("*lowers voice* And " + Dialogue.NameOf(npc.lover) + " can't know.", "*glances round* Not where " + Dialogue.NameOf(npc.lover) + " can see.");
			}
			string[] array = CourtPitch2(q, text);
			if (array != null)
			{
				return array;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				if (IsPieceGift(q))
				{
					if (q.Thing == "sword_")
					{
						return new string[3] { text, "Words are cheap. Steel isn't.", "Forge me a sword. Your own work, mind. I'll know if you bought it." };
					}
					return new string[3] { text, "Make me something with your own hands.", "An amulet. I'd wear it every day, and think of you." };
				}
				return new string[3]
				{
					text,
					"I'll tell you a secret: I'd do anything for " + Res(q) + ".",
					$"Bring me {q.Need}. A small thing, but it would mean you listened."
				};
			case QuestKind.CourtWalk:
				return new string[3]
				{
					text,
					"Take me somewhere lovely. " + Upper(q.Place) + ", out past town.",
					"Walk me there, and stay with me a while."
				};
			case QuestKind.CourtValor:
				return new string[3]
				{
					text,
					"I could never love a coward.",
					"There's a " + q.TargetName + " near here. Kill it, with your own hands."
				};
			case QuestKind.CourtBlessing:
				return new string[3]
				{
					text,
					"I won't go behind my " + q.Thing + "'s back.",
					"Ask " + q.TargetName + " for their blessing."
				};
			case QuestKind.CourtVerse:
				return new string[3]
				{
					text,
					"Write me a poem. Go and see " + q.Place + " first, and let it be in the words.",
					"Then come and say it to me."
				};
			default:
				return new string[1] { text };
			}
		}

		private static string CourtPlainly(Quest q)
		{
			string text = "within " + Span(MonthsFor(q));
			string text2 = CourtPlainly2(q, text);
			if (text2 != null)
			{
				return text2;
			}
			if (q.Kind == QuestKind.CourtGift && IsPieceGift(q))
			{
				return "To be clear: " + PieceWord(q, article: true) + " made at your own workbench, given to me " + text + ". The finer, the better.";
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				return $"To be clear: {q.Need} {Res(q)}, in your own bag, given to me {text}.";
			case QuestKind.CourtWalk:
				return "To be clear: I follow you to " + q.Place + ", and we stay there together a while, " + text + ".";
			case QuestKind.CourtValor:
				return "To be clear: the " + q.TargetName + " dead by your hand, not anyone else's, " + text + ". Then come to me.";
			case QuestKind.CourtBlessing:
				return "To be clear: my " + q.Thing + " " + q.TargetName + " gives you their blessing, " + text + ". Then come to me.";
			case QuestKind.CourtVerse:
				return "To be clear: you go and see " + q.Place + ", then come back and recite me a poem I like, " + text + ".";
			default:
				return "Do it " + text + ".";
			}
		}

		private static void CourtQuestions(Quest q, Actor npc, Actor you, Action<string, Func<string>> add)
		{
			if (CourtQuestions2(q, npc, you, add))
			{
				return;
			}
			if (q.Kind == QuestKind.CourtGift && IsPieceGift(q))
			{
				add("Where would I make one?", () => "At a workbench. Yours, if you've a house. Open your bag and look at what you can make.");
				add("Does it matter what it's made of?", () => "*smiles* Anything made by you. But a fine one would mean you really tried.");
				return;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				add("Where do I find " + Res(q) + "?", () => (!(q.Thing == "berries")) ? ((!(q.Thing == "honey")) ? "A town's stores keep it. Ask someone there to trade with you." : "Beehives, out in the wild. Take the honey when it's ready, and mind the bees.") : "Berry bushes out past the fields. Pick them when they're ripe.");
				add("Why " + Res(q) + "?", () => "*shrugs, smiling* I just love it. Isn't that enough?");
				break;
			case QuestKind.CourtWalk:
				add("Where is " + q.Place + "?", () => Where(q, npc, you));
				add("What do we do there?", () => "Nothing. Sit. Look at it. Be with me. That's the point.");
				break;
			case QuestKind.CourtValor:
				add("Where is it?", () => Where(q, npc, you));
				add("How dangerous is it?", () => Danger(q, you));
				add("Why does it matter who kills it?", () => "Because I want to know you'd stand between me and the dark.");
				break;
			case QuestKind.CourtBlessing:
				add("Where do I find " + q.TargetName + "?", () => ((q.Place != null) ? ("In " + q.Place + ". ") : "") + Where(q, npc, you));
				add("What if they say no?", () => "*winces* Then win them over. My " + q.Thing + " likes people who give more than they take.");
				break;
			case QuestKind.CourtVerse:
				add("Where is " + q.Place + "?", () => Where(q, npc, you));
				add("What kind of poem?", () => Dialogue.TasteLine(Romance.Likes(npc), null) + " Write it like that.");
				break;
			}
		}

		private static string CourtProof(Quest q)
		{
			string text = CourtProof2(q);
			if (text != null)
			{
				return text;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				return "Put the " + Res(q) + " in my hands.";
			case QuestKind.CourtWalk:
				return "I'll be there with you. I'll know.";
			case QuestKind.CourtValor:
				return "Word travels. If it's your blade that kills the " + q.TargetName + ", I'll hear of it.";
			case QuestKind.CourtBlessing:
				return "My " + q.Thing + " will tell me. Then come and see me.";
			case QuestKind.CourtVerse:
				return "You'll say it to me, and I'll know if it's true.";
			default:
				return "I'll know.";
			}
		}

		private static string CourtGoal(Quest q)
		{
			string text = CourtGoal2(q);
			if (text != null)
			{
				return text;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				if (!IsPieceGift(q))
				{
					return $"bring me {q.Need} {Res(q)}";
				}
				return "make me " + PieceWord(q, article: true);
			case QuestKind.CourtWalk:
				return "take me walking to " + q.Place;
			case QuestKind.CourtValor:
				return "kill the " + q.TargetName + " yourself";
			case QuestKind.CourtBlessing:
				return "win my " + q.Thing + "'s blessing";
			case QuestKind.CourtVerse:
				return "see " + q.Place + " and write me a poem";
			default:
				return "show me you mean it";
			}
		}

		private static string CourtThen(Quest q)
		{
			string text = CourtThen2(q);
			if (text != null)
			{
				return text;
			}
			switch (q.Kind)
			{
			case QuestKind.CourtGift:
				return "you give them to me";
			case QuestKind.CourtWalk:
				return "we stay there a while";
			case QuestKind.CourtVerse:
				return "you recite it to me";
			default:
				return "you come back to me";
			}
		}

		private static string Payoff(Quest q)
		{
			if (!IsCourt(q.Kind))
			{
				if (!IsTrial(q.Kind))
				{
					return $"you'll have {q.Money} gold for it";
				}
				return TrialPayoff(q);
			}
			return "I'll think a little more of you that way";
		}

		public static bool IsCourt2(QuestKind k)
		{
			if ((int)k >= 25)
			{
				return (int)k <= 30;
			}
			return false;
		}

		internal static bool Retired(QuestKind k)
		{
			if (k != QuestKind.CourtKeepsake)
			{
				return k == QuestKind.CourtFishing;
			}
			return true;
		}

		private static Vector2 SpotOf(Quest q)
		{
			return new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
		}

		private static bool Near(Actor a, Vector2 at, float within)
		{
			try
			{
				return a != null && (a.current_position - at).magnitude < within;
			}
			catch
			{
				return false;
			}
		}

		private static void Court2Frame()
		{
			if (_showerUntil < 0f || Time.unscaledTime < _showerUntil)
			{
				return;
			}
			_showerUntil = -1f;
			try
			{
				if (WorldBoxMod.Instance != null)
				{
					WorldBoxMod.Instance.Renderer.ForceShower = -1f;
				}
			}
			catch
			{
			}
		}

		private static void KeepShower()
		{
			_showerUntil = Time.unscaledTime + 1.5f;
			try
			{
				if (WorldBoxMod.Instance != null)
				{
					WorldBoxMod.Instance.Renderer.ForceShower = 1f;
				}
			}
			catch
			{
			}
		}

		private static void SitBy(Actor who, Vector2 at)
		{
			if (who != null && !RealActs.Sitting(who))
			{
				try
				{
					who.cancelAllBeh();
				}
				catch
				{
				}
				RealActs.Sit(who, at + new Vector2(0.9f, 0.4f), 30f);
			}
		}

		private static void GoTo(Actor who, Vector2 at)
		{
			if (who == null || Time.unscaledTime - _goOrder < 3f)
			{
				return;
			}
			_goOrder = Time.unscaledTime;
			Royal.RegisterTask();
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile == null)
			{
				return;
			}
			try
			{
				who.setTask("fp_royal_march");
				who.beh_tile_target = tile;
			}
			catch
			{
			}
		}

		private static Quest MakeCourt2(QuestKind kind, Actor giver, Actor you)
		{
			Quest quest = New(kind, giver, 0, 1, 8, 10);
			switch (kind)
			{
			case QuestKind.CourtStars:
				if (!FindSpot(giver, 12f, 40f, "hill", out quest.X, out quest.Y, out quest.Place))
				{
					return null;
				}
				return quest;
			case QuestKind.CourtFire:
			{
				WorldTile worldTile = null;
				try
				{
					worldTile = ((giver.city != null) ? giver.city.getTile() : null);
				}
				catch
				{
				}
				if (!Seats.FireNear(giver.current_position, 40f, out var fire) && (worldTile == null || !Seats.FireNear(new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f), 70f, out fire)))
				{
					return null;
				}
				quest.X = Mathf.FloorToInt(fire.x);
				quest.Y = Mathf.FloorToInt(fire.y);
				quest.Place = "the fire";
				quest.Need = 3;
				return quest;
			}
			case QuestKind.CourtRival:
			{
				Actor actor = RivalFor(giver, you);
				if (actor == null)
				{
					return null;
				}
				quest.Target = actor.data.id;
				quest.TargetName = Dialogue.NameOf(actor);
				quest.Xp = 12;
				return quest;
			}
			default:
				return null;
			}
		}

		private static Actor RivalFor(Actor giver, Actor you)
		{
			Actor result = null;
			float num = 3600f;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != giver && simple != you && simple.isAlive() && simple.city != null && simple.city == giver.city && Dialogue.IsPerson(simple) && simple.isAdult() && !simple.hasLover() && !simple.isKing() && !simple.isCityLeader() && simple.isSameSpecies(giver) && !PlayerTalk.IsPlayer(simple))
					{
						float sqrMagnitude = (simple.current_position - giver.current_position).sqrMagnitude;
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							result = simple;
						}
					}
				}
			}
			catch
			{
			}
			return result;
		}

		private static Quest MakeProposal(Actor giver, Actor you)
		{
			Quest quest = New(QuestKind.CourtProposal, giver, 0, 5, 20, 25);
			if (!FindSpot(giver, 8f, 40f, Romance.FavouritePlace(giver), out quest.X, out quest.Y, out quest.Place))
			{
				return null;
			}
			return quest;
		}

		private static void CheckCourt2(Quest q, Actor you, Actor giver, Actor target, Action<string> toast)
		{
			if (Retired(q.Kind))
			{
				q.Stage = 3;
				Save(q);
				Drop(q);
				if (giver != null)
				{
					EndEscort(giver);
				}
				toast?.Invoke("Task withdrawn: " + Title(q));
				FirstPerson.Log.Info("quests: withdrew " + q.Kind.ToString() + " for " + q.GiverName + ": taken out of the game for now");
				return;
			}
			Vector2 vector = SpotOf(q);
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				if (giver == null)
				{
					break;
				}
				if (!Night)
				{
					q.Have = 0;
				}
				else if (Near(giver, vector, 5f) && Near(you, vector, 5f))
				{
					if (q.Have == 0)
					{
						SitBy(giver, vector);
					}
					KeepShower();
					if (q.Have == 6)
					{
						WorldBoxMod.Instance?.Talk.Remark(giver, Dialogue.Pick("*points up* There! Did you see it?", "*whispers* Make a wish. Quick."));
					}
					if (++q.Have >= 24)
					{
						EndEscort(giver);
						Reward(q, you, toast);
						WorldBoxMod.Instance?.Talk.Remark(giver, Dialogue.Pick("*head on your shoulder* I'll never forget tonight.", "*quietly* I wished for you. Don't laugh."));
					}
				}
				else
				{
					q.Have = 0;
					Escort(giver, you);
				}
				break;
			case QuestKind.CourtFire:
			{
				if (giver == null)
				{
					break;
				}
				if (!Evening)
				{
					if (q.Stage == 1)
					{
						q.Stage = 0;
					}
					break;
				}
				if (!Near(giver, vector, 3.5f))
				{
					GoTo(giver, vector);
				}
				else
				{
					SitBy(giver, vector);
				}
				bool flag2 = Near(giver, vector, 5f) && Near(you, vector, 5f);
				if (flag2 && q.Stage == 0)
				{
					q.Stage = 1;
					toast?.Invoke(q.GiverName + " is waiting at the fire");
				}
				else if (!flag2 && q.Stage == 1)
				{
					q.Stage = 0;
				}
				break;
			}
			case QuestKind.CourtRival:
				if (q.Stage != 0)
				{
					break;
				}
				if (target == null)
				{
					Fail(q, you, q.TargetName + " is gone", toast);
				}
				else if (q.Taken == 1 && !Rivals.Brawling(target))
				{
					Rivals.Ending ending = ((Rivals.LastEndedWith == q.Target) ? Rivals.LastEnding : Rivals.Ending.Off);
					switch (ending)
					{
					case Rivals.Ending.Won:
						q.Stage = 1;
						toast?.Invoke(q.TargetName + " won't trouble " + q.GiverName + " again");
						break;
					case Rivals.Ending.Off:
						q.Taken = 0;
						break;
					default:
						Fail(q, you, (ending == Rivals.Ending.Ran) ? "you ran from the fight" : (q.TargetName + " beat you"), toast);
						break;
					}
				}
				break;
			case QuestKind.CourtProposal:
			{
				if (giver == null)
				{
					break;
				}
				bool flag = BagPieceLike(you, "ring_") >= 0;
				q.Have = (flag ? 1 : 0);
				if (!flag)
				{
					q.Stage = 0;
					q.Taken = 0;
				}
				else if (Near(giver, vector, 5f) && Near(you, vector, 5f))
				{
					if (q.Taken == 0)
					{
						q.Taken = 1;
						EndEscort(giver);
						Gather(giver, vector);
						toast?.Invoke("Their family and friends gather. Talk to " + q.GiverName);
					}
					q.Stage = 1;
				}
				else
				{
					q.Stage = 0;
					q.Taken = 0;
					Escort(giver, you);
				}
				break;
			}
			case QuestKind.CourtFishing:
			case QuestKind.CourtKeepsake:
				break;
			}
		}

		private static void WatchValor(Quest q, Actor you, Actor giver, Actor beast)
		{
			if (giver != null)
			{
				if ((giver.current_position - beast.current_position).magnitude > 11f)
				{
					Escort(giver, you);
				}
				else
				{
					EndEscort(giver);
				}
			}
			float magnitude = (you.current_position - beast.current_position).magnitude;
			if (magnitude < 5f)
			{
				q.Have = 1;
			}
			float num = 0f;
			try
			{
				num = you.data.health;
			}
			catch
			{
			}
			if (_valorHealth.TryGetValue(q.Giver, out var value) && num < value - 0.5f && magnitude < 7f)
			{
				q.Taken++;
			}
			_valorHealth[q.Giver] = num;
			if (q.Have == 1 && magnitude > 22f && q.Extra != "ran")
			{
				q.Extra = "ran";
				Save(q);
			}
		}

		private static int ValorScore(Quest q, Actor you, Actor giver)
		{
			int num = 100 - 15 * q.Taken - ((q.Extra == "ran") ? 40 : 0);
			if (giver == null || !Near(giver, you.current_position, 24f))
			{
				num -= 20;
			}
			_valorHealth.Remove(q.Giver);
			return Mathf.Clamp(num, 0, 100);
		}

		private static bool CourtProgress2(Conversation c, Quest q, List<Choice> list, out bool offered)
		{
			offered = false;
			Actor you = c.You;
			Actor npc = c.With;
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				c.Say(Night ? Dialogue.Pick("The stars are out. Take me up there.", Upper(q.Place) + ", tonight. I'll follow you.") : Dialogue.Pick("Not yet. Wait for the dark.", "*smiles* Tonight. When the stars come out."));
				return true;
			case QuestKind.CourtFire:
				if (q.Stage == 1)
				{
					list.Add(new Choice("*sits beside them*", delegate
					{
						FireDate(c, q, 0, 0, NewFireAsks(npc), null);
					}, ChoiceTone.Romance));
					c.Say("*pats the log beside them* Sit with me.");
				}
				else
				{
					c.Say(Evening ? "Meet me at the fire. I'll be there." : "This evening, at the fire. Don't be late.");
				}
				return true;
			case QuestKind.CourtRival:
				if (q.Stage == 1)
				{
					Reward(q, you, c.Toast);
					c.Say(Dialogue.Pick("*laughs* I heard. " + q.TargetName + " won't show their face for a week.", "*touches your bruised cheek* You fought for me. You actually fought for me."));
					c.Romantic = true;
					c.Offer(Dialogue.Topics(c));
					offered = true;
					return true;
				}
				c.Say(q.TargetName + " came by again this morning. *sighs* Well?");
				return true;
			case QuestKind.CourtProposal:
				if (q.Stage == 1)
				{
					list.Insert(0, new Choice("*kneels* Will you be mine?", delegate
					{
						Propose(c, q);
					}, ChoiceTone.Romance));
					c.Say("*everyone goes quiet* What is it?");
				}
				else if (q.Have == 0)
				{
					c.Say("*smiles* I'm waiting. You know what for.");
				}
				else
				{
					c.Say(Upper(q.Place) + "? *takes your arm* Lead the way.");
				}
				return true;
			default:
				return false;
			}
		}

		private static List<FireAsk> NewFireAsks(Actor npc)
		{
			List<FireAsk> list = new List<FireAsk>();
			System.Random rng = new System.Random();
			List<string> list2 = new List<string>();
			foreach (Hope value3 in Enum.GetValues(typeof(Hope)))
			{
				list2.Add(Dialogue.HopeWordsOf(value3));
			}
			list.Add(Make("*pokes the fire* What do you think I want most, out of life?", Dialogue.HopeWordsOf(Romance.HopeOf(npc)), list2));
			List<string> wrong = new List<string> { "flowers", "hill", "shore", "woods" }.ConvertAll<string>(Romance.PlaceWords);
			list.Add(Make("If I went missing one day, where would you look for me?", Romance.PlaceWords(Romance.FavouritePlace(npc)), wrong));
			List<string> wrong2 = new List<string> { "berries", "honey", "bread", "pie", "cider", "jam", "fish", "gems" }.ConvertAll((string g) => Work.ResourceName(g).ToLowerInvariant());
			list.Add(Make("What would you bring me, to make my day?", Work.ResourceName(Romance.Favourite(npc)).ToLowerInvariant(), wrong2));
			int num = Dialogue.SafeHappiness(npc);
			string text = "Happy. Lighter than I've seen you.";
			string text2 = "Tired. Worn down to the bone.";
			string text3 = "Troubled. Something's on your mind.";
			list.Add(Make("*looks at you* How do I seem to you, tonight?", (num > 30) ? text : ((num < -10) ? text3 : text2), new List<string> { text, text2, text3 }));
			list.RemoveAt(rng.Next(list.Count));
			return list;
			FireAsk Make(string question, string right, List<string> list3)
			{
				list3.Remove(right);
				for (int num2 = list3.Count - 1; num2 > 0; num2--)
				{
					int index = rng.Next(num2 + 1);
					string value = list3[num2];
					list3[num2] = list3[index];
					list3[index] = value;
				}
				List<string> list4 = new List<string>
				{
					right,
					list3[0],
					list3[1]
				};
				for (int num3 = list4.Count - 1; num3 > 0; num3--)
				{
					int index2 = rng.Next(num3 + 1);
					string value2 = list4[num3];
					list4[num3] = list4[index2];
					list4[index2] = value2;
				}
				return new FireAsk
				{
					Question = question,
					Answers = list4.ToArray(),
					Right = list4.IndexOf(right)
				};
			}
		}

		private static void FireDate(Conversation c, Quest q, int round, int score, List<FireAsk> asks, string lead)
		{
			c.LastTopic = "court fire " + round;
			if (round >= asks.Count)
			{
				LastFireScore = score;
				if (score >= 2)
				{
					Reward(q, c.You, c.Toast);
					c.Say(lead, Dialogue.Pick("*leans against you* You actually listen. Do you know how rare that is?", "*watches the sparks* Stay a while longer. Please."));
					c.Romantic = true;
					c.Hearts?.Invoke(c.With);
				}
				else
				{
					Fail(q, c.You, "the evening went badly", c.Toast);
					c.Say(lead, "*stands, brushing off ash* I thought you knew me better than that. Goodnight.");
				}
				c.Offer(Dialogue.Topics(c));
				return;
			}
			FireAsk fireAsk = asks[round];
			c.Say(lead, fireAsk.Question);
			List<Choice> list = new List<Choice>();
			for (int i = 0; i < fireAsk.Answers.Length; i++)
			{
				bool right = i == fireAsk.Right;
				list.Add(new Choice(Upper(fireAsk.Answers[i]) + (fireAsk.Answers[i].EndsWith(".") ? "" : "."), delegate
				{
					c.Think(0.4f);
					FireDate(c, q, round + 1, score + (right ? 1 : 0), asks, right ? Dialogue.Pick("*smiles* You remembered.", "*nods slowly* That's right.") : Dialogue.Pick("*frowns* No...", "*quiet* Not quite."));
				}, ChoiceTone.Romance));
			}
			c.Offer(list);
		}

		private static void Gather(Actor giver, Vector2 spot)
		{
			_crowd.Clear();
			List<long> list = new List<long>();
			try
			{
				list.Add(giver.data.parent_id_1);
				list.Add(giver.data.parent_id_2);
				list.Add(giver.data.best_friend_id);
				Record record = Chronicle.Of(giver);
				if (record != null)
				{
					foreach (Kin item in record.Kin)
					{
						if (item.Role == "ch")
						{
							list.Add(item.Id);
						}
					}
					foreach (Kin item2 in Chronicle.SiblingsOf(record))
					{
						list.Add(item2.Id);
					}
				}
			}
			catch
			{
			}
			foreach (long item3 in list)
			{
				if (item3 <= 0 || _crowd.Contains(item3) || _crowd.Count >= 8)
				{
					continue;
				}
				Actor actor = Chronicle.Find(item3);
				if (actor == null || !actor.isAlive() || actor == giver || (actor.current_position - spot).magnitude > 80f || PlayerTalk.IsPlayer(actor))
				{
					continue;
				}
				_crowd.Add(item3);
				Royal.RegisterTask();
				float f = (float)_crowd.Count * 0.9f;
				WorldTile tile = World.world.GetTile((int)(spot.x + Mathf.Cos(f) * 3f), (int)(spot.y + Mathf.Sin(f) * 3f));
				if (tile != null)
				{
					try
					{
						actor.setTask("fp_royal_march");
						actor.beh_tile_target = tile;
					}
					catch
					{
					}
				}
			}
			FirstPerson.Log.Info($"quests: proposal, {_crowd.Count} gather round {Dialogue.NameOf(giver)}");
		}

		private static void Propose(Conversation c, Quest q)
		{
			c.LastTopic = "court propose";
			Actor with = c.With;
			Actor you = c.You;
			int num = BagPieceLike(you, "ring_");
			if (num < 0)
			{
				c.Say("*looks at your empty hands* ...A ring?");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			Rarity rarity = Rarity.R0_Normal;
			try
			{
				Gear.Piece piece = Gear.Of(you)[num];
				rarity = CraftOdds.QualityOf(piece.Asset, piece.Mods);
			}
			catch
			{
			}
			try
			{
				WorldBoxMod.Instance?.Kneel();
			}
			catch
			{
			}
			FlirtOutcome? flirtOutcome = Romance.Blocker(you, with);
			if (flirtOutcome.HasValue)
			{
				c.Say(Dialogue.Flirt(with, you, flirtOutcome.Value));
				c.Offer(Dialogue.Topics(c));
				return;
			}
			GivePiece(you, with, num);
			c.Think(1.2f);
			if (!Married(c, (rarity >= Rarity.R2_Epic) ? "*stares at the ring* Is that... you made this? *laughs, crying* Yes. Yes!" : ((rarity >= Rarity.R1_Rare) ? "*hand over mouth* Yes. Of course yes." : null)))
			{
				c.Say("*something's wrong* I... can't. Not now.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			q.Stage = 2;
			Save(q);
			Drop(q);
			Completed++;
			c.Offer(Dialogue.Topics(c));
			try
			{
				you.addRenown(q.Renown);
				you.addExperience(q.Xp);
			}
			catch
			{
			}
			foreach (long item in _crowd)
			{
				Actor actor = Chronicle.Find(item);
				if (actor != null && actor.isAlive())
				{
					c.Hearts?.Invoke(actor);
					Chronicle.ChangeOpinion(actor, you, 10);
					WorldBoxMod.Instance?.Talk.Remark(actor, Dialogue.Pick("*cheers*", "*claps and whistles*", "About time!", "*wipes an eye*"));
				}
			}
			FirstPerson.Log.Info($"quests: {q.GiverName} said yes to the proposal (ring {rarity}, {_crowd.Count} watching)");
			_crowd.Clear();
		}

		private static bool GivePiece(Actor you, Actor npc, int index)
		{
			if (index < 0)
			{
				return false;
			}
			try
			{
				Gear.Piece piece = Gear.Of(you)[index];
				EquipmentAsset asset = piece.Asset;
				if (asset == null)
				{
					return false;
				}
				ActorEquipmentSlot slot = npc.equipment.getSlot(asset.equipment_type);
				Item item = Gear.Build(piece, npc);
				if (item == null || slot == null)
				{
					return false;
				}
				Item item2 = slot.getItem();
				if (item2 != null)
				{
					slot.takeAwayItem();
					Gear.LetGo(item2);
				}
				slot.setItem(item, npc);
				Gear.Remove(you, index);
				BagOrder.PieceGone(you, index);
				try
				{
					npc.setStatsDirty();
				}
				catch
				{
				}
				return true;
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: give a piece: " + ex.Message);
				return false;
			}
		}

		private static string CourtWhat2(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				return "the stars from " + q.Place;
			case QuestKind.CourtFire:
				return "an evening at the fire";
			case QuestKind.CourtRival:
				return q.TargetName + " seen off";
			case QuestKind.CourtProposal:
				return "the proposal";
			default:
				return null;
			}
		}

		private static string CourtStatus2(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				if (Night)
				{
					if (q.Have <= 0)
					{
						return "Lead " + q.GiverName + " to " + q.Place;
					}
					return "Watch the stars with " + q.GiverName;
				}
				return "Wait for nightfall";
			case QuestKind.CourtFire:
				if (q.Stage != 1)
				{
					if (!Evening)
					{
						return "Wait for the evening";
					}
					return "Meet " + q.GiverName + " at the fire";
				}
				return "Sit with " + q.GiverName + " at the fire";
			case QuestKind.CourtRival:
				if (q.Stage != 1)
				{
					return "Tell " + q.TargetName + " to stay away";
				}
				return "Go back to " + q.GiverName;
			case QuestKind.CourtProposal:
				if (q.Stage != 1)
				{
					if (q.Have != 0)
					{
						return "Lead " + q.GiverName + " to " + q.Place;
					}
					return "Make a ring at the workbench";
				}
				return "Kneel and ask " + q.GiverName;
			default:
				return null;
			}
		}

		private static bool CourtHeading2(Quest q, out Vector2 at)
		{
			at = default(Vector2);
			switch (q.Kind)
			{
			case QuestKind.CourtRival:
			{
				if (q.Stage == 1)
				{
					return false;
				}
				Actor actor = Chronicle.Find(q.Target);
				if (actor != null)
				{
					at = actor.current_position;
					return true;
				}
				return false;
			}
			case QuestKind.CourtProposal:
				if (q.Have == 0)
				{
					return false;
				}
				at = SpotOf(q);
				return true;
			case QuestKind.CourtStars:
			case QuestKind.CourtFire:
				at = SpotOf(q);
				return true;
			default:
				return false;
			}
		}

		private static string[] CourtPitch2(Quest q, string first)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				return new string[3]
				{
					first,
					"Have you ever just lain back and watched the sky?",
					"Take me out to " + q.Place + " one night. Stay with me till the stars fall."
				};
			case QuestKind.CourtFire:
				return new string[3] { first, "Words in the daylight are easy.", "Sit with me at the fire one evening. Let's see how well you know me." };
			case QuestKind.CourtRival:
				return new string[3]
				{
					first,
					"*bites lip* You're not the only one. " + q.TargetName + " has been calling on me too.",
					"I won't choose by words. Show me who'd stand up for me."
				};
			default:
				return null;
			}
		}

		private static string CourtPlainly2(Quest q, string by)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				return "To be clear: one night, I follow you to " + q.Place + ", and we stay till the stars fall, " + by + ".";
			case QuestKind.CourtFire:
				return "To be clear: one evening, we sit at the fire and I ask you about me, " + by + ". Get it right.";
			case QuestKind.CourtRival:
				return "To be clear: " + q.TargetName + " told to stay away, and if it comes to fists, you win, " + by + ". No blades.";
			case QuestKind.CourtProposal:
				return "To be clear: a ring, then ask me at " + q.Place + ", " + by + ".";
			default:
				return null;
			}
		}

		private static bool CourtQuestions2(Quest q, Actor npc, Actor you, Action<string, Func<string>> add)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				add("Where is " + q.Place + "?", () => Where(q, npc, you));
				add("What if no stars fall?", () => "*laughs* They will. They always do, for the right people.");
				return true;
			case QuestKind.CourtFire:
				add("Which fire?", () => Where(q, npc, you));
				add("What will you ask?", () => "*smiles* Things I've told you. And how I am. If you've been paying attention, you'll know.");
				return true;
			case QuestKind.CourtRival:
				add("Who is " + q.TargetName + "?", () => "Someone from town. " + Where(q, npc, you));
				add("What if they won't back off?", () => "Then it's fists. Not blades, mind. I don't want anyone dead.");
				return true;
			default:
				return false;
			}
		}

		private static string CourtProof2(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtRival:
				return "I'll hear of it. " + q.TargetName + " can't keep a bruise quiet.";
			case QuestKind.CourtStars:
			case QuestKind.CourtFire:
				return "I'll be there with you. I'll know.";
			default:
				return null;
			}
		}

		private static string CourtGoal2(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				return "watch the stars with me from " + q.Place;
			case QuestKind.CourtFire:
				return "sit with me at the fire";
			case QuestKind.CourtRival:
				return "see off " + q.TargetName;
			case QuestKind.CourtProposal:
				return "ask me properly";
			default:
				return null;
			}
		}

		private static string CourtThen2(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
				return "we watch till the stars fall";
			case QuestKind.CourtFire:
				return "you answer me true";
			default:
				return null;
			}
		}

		private static int CourtMonths(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.CourtStars:
			case QuestKind.CourtFire:
				return Mathf.Max(300, DaysInMonths(2f) + 12);
			case QuestKind.CourtProposal:
				return 720;
			case QuestKind.CourtGift:
			case QuestKind.CourtRival:
				return 120;
			default:
				return 60;
			}
		}

		private static int DaysInMonths(float days)
		{
			try
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (instance?.Day == null || instance.Settings == null)
				{
					return 0;
				}
				instance.Day.HalfMinutes(instance.Settings, out var day, out var night);
				return Mathf.CeilToInt(days * (day + night) * 60f / 5f);
			}
			catch
			{
				return 0;
			}
		}

		private static bool IsPieceGift(Quest q)
		{
			if (q.Thing != null)
			{
				return q.Thing.EndsWith("_", StringComparison.Ordinal);
			}
			return false;
		}

		private static string PieceWord(Quest q, bool article)
		{
			if (!(q.Thing == "sword_"))
			{
				if (!article)
				{
					return "the amulet";
				}
				return "an amulet";
			}
			if (!article)
			{
				return "the sword";
			}
			return "a sword";
		}

		private static void PieceGiftProgress(Conversation c, Quest q, List<Choice> list)
		{
			Actor you = c.You;
			Actor npc = c.With;
			int num = BagPieceLike(you, q.Thing);
			if (num < 0)
			{
				c.Say("*smiles* I'm still waiting for " + PieceWord(q, article: false) + ", you know.");
				return;
			}
			Gear.Piece piece = Gear.Of(you)[num];
			Rarity quality = Rarity.R0_Normal;
			try
			{
				quality = CraftOdds.QualityOf(piece.Asset, piece.Mods);
			}
			catch
			{
			}
			string text = piece.Name ?? PieceWord(q, article: false);
			list.Add(new Choice("For you: " + text + ".", delegate
			{
				int num2 = BagPieceLike(you, q.Thing);
				if (num2 < 0 || !GivePiece(you, npc, num2))
				{
					c.Say("Wait... where is it?");
					c.Offer(Dialogue.Topics(c));
				}
				else
				{
					q.Opinion += 5 * (int)quality;
					Reward(q, you, c.Toast);
					c.Say((quality >= Rarity.R2_Epic) ? Dialogue.Pick("*turns it in the light, speechless* You made this? It's beautiful.", "*gasps* I've never owned anything so fine.") : ((quality >= Rarity.R1_Rare) ? Dialogue.Pick("*puts it on at once* It's lovely. Really lovely.", "*smiles* You put real work into this.") : Dialogue.Pick("*puts it on* It's a little rough. I love it anyway.", "*laughs softly* Not the finest. But you made it.")));
					c.Romantic = true;
					c.Offer(Dialogue.Topics(c));
				}
			}, ChoiceTone.Romance));
			c.Say("*glances at your bag* Is that... for me?");
		}

		private static void FaceRival(Conversation c, Quest q)
		{
			c.LastTopic = "court rival";
			Actor rival = c.With;
			Actor you = c.You;
			c.Say(Dialogue.Pick("*squares up* " + q.GiverName + "? And what's it to you?", "*laughs* " + q.GiverName + " can choose for themselves.", "Make me."));
			c.Offer(new List<Choice>
			{
				new Choice("Then we settle it. Fists.", delegate
				{
					q.Taken = 1;
					Save(q);
					Rivals.StartBrawl(rival, you, q.Giver, Dialogue.Pick("Fine. Fists it is.", "You'll regret this."), c.Toast);
				}, ChoiceTone.Risk),
				new Choice("Never mind.", delegate
				{
					c.Say("*smirks* Thought so.");
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Back)
			});
		}

		public static void OnKill(Actor victim)
		{
			if (victim != null)
			{
				try
				{
					KilledByYou[victim.data.id] = World.world.getCurWorldTime();
				}
				catch
				{
				}
				Law.OnKill(victim);
				Service.OnKill(victim);
				Hub.OnKill(WorldBoxMod.Instance?.Host, victim);
			}
		}

		public static bool YouKilled(long id)
		{
			return KilledByYou.ContainsKey(id);
		}

		public static void Update(Actor you, float dt, Action<string> toast)
		{
			if (you == null || World.world == null)
			{
				return;
			}
			object map_stats = World.world.map_stats;
			if (map_stats != _world)
			{
				_world = map_stats;
				_logFor = -1L;
				KilledByYou.Clear();
				EndTrialState();
			}
			if (_logFor != you.data.id)
			{
				EndTrialState();
				Rebuild(you);
			}
			TickTrials(you, dt, toast);
			Court2Frame();
			HookRaids();
			Raiders.Update(dt);
			_checkTimer -= dt;
			if (_checkTimer > 0f)
			{
				return;
			}
			_checkTimer = 0.5f;
			for (int num = Log.Count - 1; num >= 0; num--)
			{
				if (num < Log.Count)
				{
					Quest quest = Log[num];
					try
					{
						Check(quest, you, toast);
					}
					catch (Exception ex)
					{
						FirstPerson.Log.Error("quests: check " + quest.Kind.ToString() + ": " + ex.Message);
					}
					if (quest.Stage >= 2)
					{
						Log.Remove(quest);
					}
				}
			}
			_rebuildTimer -= 0.5f;
			if (_rebuildTimer <= 0f)
			{
				_rebuildTimer = 30f;
				Rebuild(you);
			}
		}

		public static void Rebuild(Actor you)
		{
			List<Quest> was = ((_logFor == you.data.id && Log.Count > 0) ? new List<Quest>(Log) : null);
			_logFor = you.data.id;
			Log.Clear();
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive())
					{
						Quest quest = Read(simple.data);
						if (quest != null && quest.Holder == you.data.id && quest.Stage < 2)
						{
							Keep(quest, was);
						}
					}
				}
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive())
					{
						Quest quest2 = Read(item.data, "fp_bq");
						if (quest2 != null && quest2.Holder == you.data.id && quest2.Stage < 2)
						{
							Keep(quest2, was);
						}
						Quest quest3 = Read(item.data, "fp_hq");
						if (quest3 != null && quest3.Holder == you.data.id && quest3.Stage < 2)
						{
							Keep(quest3, was);
						}
						Quest quest4 = Read(item.data, "fp_lq");
						if (quest4 != null && quest4.Holder == you.data.id && quest4.Stage < 2)
						{
							Keep(quest4, was);
						}
					}
				}
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: rebuild: " + ex.Message);
			}
			PassedTrialKept(you);
		}

		private static void PassedTrialKept(Actor you)
		{
			string pResult = null;
			try
			{
				you.data.get("fp_trial_done", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return;
			}
			Quest quest = null;
			try
			{
				quest = Parse(pResult);
			}
			catch
			{
			}
			if (quest == null || quest.Stage != 1 || !IsTrial(quest.Kind))
			{
				try
				{
					you.data.removeString("fp_trial_done");
					return;
				}
				catch
				{
					return;
				}
			}
			foreach (Quest item in Log)
			{
				if (item.Giver == quest.Giver && item.Kind == quest.Kind)
				{
					return;
				}
			}
			if (Chronicle.Find(quest.Giver) != null)
			{
				try
				{
					you.data.removeString("fp_trial_done");
					return;
				}
				catch
				{
					return;
				}
			}
			FirstPerson.Log.Info("quests: a passed trial kept for " + WorldBoxMod.Describe(you) + " (its teacher is gone)");
			TrialPassed(quest, you, (WorldBoxMod.Instance != null) ? new Action<string>(WorldBoxMod.Instance.Toast) : null);
		}

		private static void Keep(Quest q, List<Quest> was)
		{
			if (was != null)
			{
				foreach (Quest wa in was)
				{
					if (wa.Stage < 2 && wa.Giver == q.Giver && wa.Kind == q.Kind && wa.FromBoard == q.FromBoard && (!wa.FromBoard || TownKey(wa) == TownKey(q)) && !Log.Contains(wa))
					{
						Log.Add(wa);
						return;
					}
				}
			}
			Log.Add(q);
		}

		public static Quest Of(Actor giver)
		{
			object obj;
			if (giver != null)
			{
				obj = Find(giver.data.id);
				if (obj == null)
				{
					return Read(giver.data);
				}
			}
			else
			{
				obj = null;
			}
			return (Quest)obj;
		}

		private static Quest Find(long giver)
		{
			foreach (Quest item in Log)
			{
				if (item.Giver == giver && !item.FromBoard)
				{
					return item;
				}
			}
			return null;
		}

		public static bool Accept(Quest q, Actor you, Action<string> toast)
		{
			if (Log.Count >= 5)
			{
				toast?.Invoke("You already have " + 5 + " tasks. Finish one first.");
				return false;
			}
			if ((q.Kind == QuestKind.Hunt || q.Kind == QuestKind.Bounty) && Hunted(q.Target))
			{
				toast?.Invoke("You're already hunting " + q.TargetName + ".");
				return false;
			}
			if ((q.Kind == QuestKind.Hunt || q.Kind == QuestKind.Bounty) && q.Target != 0L && Log.Exists((Quest x) => (x.Kind == QuestKind.Hunt || x.Kind == QuestKind.Bounty) && x.Target == q.Target))
			{
				toast?.Invoke("You're already after the " + q.TargetName + ".");
				return false;
			}
			if (IsTrial(q.Kind) && !PayTrialFee(q, you, toast))
			{
				return false;
			}
			if (LevelOfKind(q.Kind) > LevelOf(you).Level)
			{
				toast?.Invoke("That's work for someone more seasoned (quest level " + LevelOfKind(q.Kind) + ").");
				return false;
			}
			if (q.Kind == QuestKind.Raid && RaidBusy() != null)
			{
				toast?.Invoke(RaidBusy());
				return false;
			}
			q.Holder = you.data.id;
			q.Deadline = ((MonthsFor(q) > 0) ? (Now + (double)(5f * (float)MonthsFor(q))) : 0.0);
			Save(q);
			Log.Add(q);
			toast?.Invoke("New task: " + Title(q));
			FirstPerson.Log.Info("quests: accepted " + q.Kind.ToString() + " from " + q.GiverName + ": " + Status(q));
			if (q.Kind == QuestKind.Raid)
			{
				StartRaid(q, toast);
			}
			return true;
		}

		private static void Drop(Quest q)
		{
			Log.RemoveAll((Quest x) => x == q || (x.Giver == q.Giver && x.Kind == q.Kind && x.FromBoard == q.FromBoard && (!x.FromBoard || TownKey(x) == TownKey(q))));
		}

		public static void Save(Quest q)
		{
			try
			{
				if (q.FromBoard)
				{
					City city = CityById(q.Giver);
					if (city != null)
					{
						string pKey = TownKey(q);
						if (q.Stage >= 2)
						{
							city.data.removeString(pKey);
						}
						else
						{
							city.data.set(pKey, Write(q));
						}
					}
					return;
				}
				Actor actor = Chronicle.Find(q.Giver);
				if (actor != null)
				{
					if (q.Stage >= 2)
					{
						actor.data.removeString("fp_q");
					}
					else
					{
						actor.data.set("fp_q", Write(q));
					}
				}
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: save: " + ex.Message);
			}
		}

		private static bool StillOpen(Quest q, Actor you)
		{
			try
			{
				Quest quest;
				if (q.FromBoard)
				{
					City city = CityById(q.Giver);
					quest = ((city != null) ? Read(city.data, TownKey(q)) : null);
				}
				else
				{
					Actor actor = Chronicle.Find(q.Giver);
					quest = ((actor != null) ? Read(actor.data) : null);
				}
				return quest != null && quest.Kind == q.Kind && quest.Holder == you.data.id && quest.Stage < 2;
			}
			catch
			{
				return true;
			}
		}

		private static string TownKey(Quest q)
		{
			if (q.Kind != QuestKind.Meat)
			{
				if (q.Kind != QuestKind.Undead && q.Kind != QuestKind.Raid)
				{
					return "fp_bq";
				}
				return "fp_lq";
			}
			return "fp_hq";
		}

		public static City CityById(long id)
		{
			try
			{
				return (id > 0) ? World.world.cities.get(id) : null;
			}
			catch
			{
				return null;
			}
		}

		private static Actor GiverOf(Quest q)
		{
			if (!q.FromBoard)
			{
				return Chronicle.Find(q.Giver);
			}
			return null;
		}

		public static void Reward(Quest q, Actor you, Action<string> toast)
		{
			if (!StillOpen(q, you))
			{
				FirstPerson.Log.Info("quests: no reward for " + q.Kind.ToString() + " from " + q.GiverName + ": it's over");
				return;
			}
			if (IsCourt(q.Kind))
			{
				Courted(q, you, toast);
				return;
			}
			if (IsTrial(q.Kind))
			{
				TrialPassed(q, you, toast);
				return;
			}
			Actor actor = GiverOf(q);
			try
			{
				if (actor != null && !q.FromBoard)
				{
					int num = Mathf.Min(actor.money, q.Money / 2);
					if (num > 0)
					{
						actor.spendMoney(num);
					}
				}
				you.addMoney(q.Money);
				if (q.Renown > 0)
				{
					you.addRenown(q.Renown);
				}
				if (q.Xp > 0)
				{
					you.addExperience(q.Xp);
				}
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: reward: " + ex.Message);
			}
			int num2 = (q.Bench ? Workbench.GiveTools(you) : (-1));
			if (actor != null)
			{
				try
				{
					actor.changeHappiness("just_received_gift");
				}
				catch
				{
				}
				Chronicle.ChangeOpinion(actor, you, q.Opinion);
				Chronicle.Note(actor, you, 0, 0, 1);
				if (!q.FromBoard)
				{
					NoteDeal(actor);
				}
				Chronicle.AddOwnMemory(actor, "fp_quest", Short(q));
				try
				{
					if (you.isKing() && actor.kingdom == you.kingdom && actor.city != null)
					{
						Law.LoyaltyBoost(actor.city, 6);
					}
				}
				catch
				{
				}
			}
			q.Stage = 2;
			Save(q);
			Drop(q);
			Completed++;
			Run.Tally(you, "quest");
			List<string> after = new List<string>(3);
			Action<string> toast2 = ((toast == null) ? null : ((Action<string>)delegate(string s)
			{
				after.Add(s);
			}));
			LevelDone(you, q, toast2);
			if (q.Kind == QuestKind.Undead || q.Kind == QuestKind.Raid)
			{
				LevelRep(q, you, actor);
			}
			else
			{
				Hub.OnQuestDone(you, q.FromBoard ? CityById(q.Giver) : actor?.city, q.Renown, q.FromBoard && q.Kind == QuestKind.Bounty);
			}
			LevelPicks(q, you);
			Moves.Rewarded(q, you, toast2);
			toast?.Invoke($"Task done: {Title(q)}   +{q.Money} gold" + ((q.Renown > 0) ? $", +{q.Renown} renown" : "") + ((num2 > 0) ? $", workbench tools (level {num2})" : ((num2 == 0) ? ", workbench tools" : "")));
			foreach (string item in after)
			{
				toast?.Invoke(item);
			}
			FirstPerson.Log.Info("quests: finished " + q.Kind.ToString() + " for " + q.GiverName + " (+" + q.Money + " gold)");
			WorldBoxMod.Instance?.Audio.PlayRustle();
		}

		public static void Fail(Quest q, Actor you, string why, Action<string> toast)
		{
			q.Stage = 3;
			Save(q);
			Drop(q);
			Failed++;
			Actor actor = GiverOf(q);
			if (actor != null && you != null && !IsTrial(q.Kind))
			{
				Chronicle.ChangeOpinion(actor, you, -6);
			}
			if (IsCourt(q.Kind))
			{
				CourtLost(q, you);
			}
			if (IsTrial(q.Kind))
			{
				TrialLost(q, you);
			}
			toast?.Invoke("Task failed: " + Title(q) + ((why != null) ? (" (" + why + ")") : ""));
			FirstPerson.Log.Info("quests: failed " + q.Kind.ToString() + " for " + q.GiverName + ": " + why);
		}

		private static void Withdrawn(Quest q, Actor you, Action<string> toast)
		{
			q.Stage = 3;
			Save(q);
			Drop(q);
			toast?.Invoke("Task withdrawn: " + Title(q) + " (killing " + q.TargetName + " would now be murder)");
			FirstPerson.Log.Info("quests: withdrew " + q.Kind.ToString() + " on " + q.TargetName + ": under the law for you now");
		}

		public static void Abandon(Quest q, Actor you)
		{
			q.Stage = 3;
			Save(q);
			Drop(q);
			Actor actor = GiverOf(q);
			if (actor != null && !IsTrial(q.Kind))
			{
				Chronicle.ChangeOpinion(actor, you, -4);
			}
			if (IsCourt(q.Kind))
			{
				CourtLost(q, you);
				if (actor != null && q.Kind != QuestKind.CourtBlessing && q.Kind != QuestKind.CourtVerse)
				{
					EndEscort(actor);
				}
			}
			if (actor != null && q.Kind == QuestKind.Escort)
			{
				EndEscort(actor);
			}
			if (IsTrial(q.Kind))
			{
				TrialLost(q, you);
			}
		}

		public static string Title(Quest q)
		{
			return Renames.Apply(TitleAsGiven(q));
		}

		private static string TitleAsGiven(Quest q)
		{
			if (IsCourt(q.Kind))
			{
				return CourtTitle(q);
			}
			if (IsTrial(q.Kind))
			{
				return TrialTitle(q);
			}
			if (q.Kind == QuestKind.Meat)
			{
				return MeatTitle(q);
			}
			if (q.Kind == QuestKind.Undead)
			{
				return UndeadTitle(q);
			}
			if (q.Kind == QuestKind.Raid)
			{
				return RaidTitle(q);
			}
			switch (q.Kind)
			{
			case QuestKind.Supply:
				return $"{q.Need} {Work.ResourceName(q.Thing)} for {q.Place ?? q.GiverName}";
			case QuestKind.Hunt:
				return "Hunt the " + q.TargetName;
			case QuestKind.Errand:
				return "Word to " + q.TargetName;
			case QuestKind.Craft:
				return "A new " + ItemWord(q.Thing) + " for " + q.GiverName;
			case QuestKind.Matchmaker:
				return "Matchmaker: " + q.GiverName + " and " + q.TargetName;
			case QuestKind.Escort:
				return "Escort " + q.GiverName + " to " + q.Place;
			case QuestKind.Faith:
				return "Spread " + q.Thing;
			case QuestKind.Pilgrimage:
				return "Pilgrimage to " + q.Place;
			case QuestKind.Investigation:
				return "Who robbed " + q.GiverName + "?";
			case QuestKind.Bounty:
				return "Bounty: " + q.TargetName;
			default:
				return q.Kind.ToString();
			}
		}

		private static string Short(Quest q)
		{
			if (IsCourt(q.Kind))
			{
				return "my heart";
			}
			if (IsTrial(q.Kind))
			{
				return "a lesson";
			}
			switch (q.Kind)
			{
			case QuestKind.Supply:
				return "the stores";
			case QuestKind.Hunt:
				return "the " + q.TargetName;
			case QuestKind.Errand:
				return "word to " + q.TargetName;
			case QuestKind.Craft:
				return "a new " + ItemWord(q.Thing);
			case QuestKind.Matchmaker:
				return q.TargetName;
			case QuestKind.Escort:
				return "the road to " + q.Place;
			case QuestKind.Faith:
				return "our faith";
			case QuestKind.Pilgrimage:
				return "the pilgrimage";
			case QuestKind.Investigation:
				return "the thief";
			case QuestKind.Meat:
				return "the meat";
			case QuestKind.Undead:
				return "the dead";
			case QuestKind.Raid:
				return "the raid";
			default:
				return "a task";
			}
		}

		public static string Status(Quest q)
		{
			return Renames.Apply(StatusAsGiven(q));
		}

		private static string StatusAsGiven(Quest q)
		{
			if (IsCourt(q.Kind))
			{
				return CourtStatus(q);
			}
			if (q.Stage == 1)
			{
				if (q.Kind != QuestKind.Bounty)
				{
					if (q.Kind != QuestKind.Meat || !q.FromBoard)
					{
						if ((q.Kind != QuestKind.Undead && q.Kind != QuestKind.Raid) || !q.FromBoard)
						{
							return "Go back to " + q.GiverName;
						}
						return "Report to " + q.GiverName + ": its leader, a soldier, or its board";
					}
					return "Hand the meat in at the board of " + q.GiverName;
				}
				return "Claim the reward at the board or from a leader";
			}
			if (IsTrial(q.Kind))
			{
				return TrialStatus(q);
			}
			if (q.Kind == QuestKind.Meat)
			{
				return MeatStatus(q);
			}
			if (q.Kind == QuestKind.Undead)
			{
				return UndeadStatus(q);
			}
			if (q.Kind == QuestKind.Raid)
			{
				return RaidStatus(q);
			}
			switch (q.Kind)
			{
			case QuestKind.Supply:
				return $"{Mathf.Min(q.Have, q.Need)}/{q.Need} {Work.ResourceName(q.Thing)} in your bag";
			case QuestKind.Hunt:
			case QuestKind.Bounty:
				return "Kill the " + q.TargetName;
			case QuestKind.Errand:
				return "Find " + q.TargetName + " in " + (q.Place ?? "another town");
			case QuestKind.Craft:
				return "Make a " + ItemWord(q.Thing) + " (Tab) and bring it";
			case QuestKind.Matchmaker:
				return "Talk to " + q.TargetName + " about " + q.GiverName;
			case QuestKind.Escort:
				return "Lead " + q.GiverName + " to " + q.Place;
			case QuestKind.Faith:
				return $"Preach {q.Thing}: {q.Have}/{q.Need} converted";
			case QuestKind.Pilgrimage:
				return "Travel to " + q.Place;
			case QuestKind.Investigation:
				if (q.Target <= 0)
				{
					return "Find the thief";
				}
				return "Find the thief (look for " + q.TargetName + ")";
			default:
				return "";
			}
		}

		public static string ItemWord(string prefix)
		{
			if (!string.IsNullOrEmpty(prefix))
			{
				return prefix.TrimEnd('_').Replace('_', ' ');
			}
			return "tool";
		}

		public static bool Heading(Quest q, out Vector2 at)
		{
			at = default(Vector2);
			try
			{
				if (q.Stage == 1)
				{
					if (q.FromBoard)
					{
						City city = CityById(q.Giver);
						if (city != null)
						{
							WorldTile tile = city.getTile();
							if (tile != null)
							{
								at = new Vector2(tile.posV3.x, tile.posV3.y);
								return true;
							}
						}
						return false;
					}
					Actor actor = Chronicle.Find(q.Giver);
					if (actor != null)
					{
						at = actor.current_position;
						return true;
					}
					return false;
				}
				if (IsCourt(q.Kind))
				{
					return CourtHeading(q, out at);
				}
				if (IsTrial(q.Kind))
				{
					return TrialHeading(q, out at);
				}
				Actor actor2 = Chronicle.Find(q.Target);
				switch (q.Kind)
				{
				case QuestKind.Hunt:
				case QuestKind.Errand:
				case QuestKind.Matchmaker:
				case QuestKind.Investigation:
				case QuestKind.Bounty:
					if (actor2 != null)
					{
						at = actor2.current_position;
						return true;
					}
					break;
				case QuestKind.Escort:
				case QuestKind.Pilgrimage:
					if (q.X >= 0)
					{
						at = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
						return true;
					}
					break;
				case QuestKind.Meat:
					return MeatHeading(q, out at);
				case QuestKind.Undead:
					return UndeadHeading(q, out at);
				case QuestKind.Raid:
					return RaidHeading(q, out at);
				}
			}
			catch
			{
			}
			return false;
		}

		private static string Clean(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return s.Replace(";", " ").Replace("|", " ");
			}
			return "";
		}

		public static string Write(Quest q)
		{
			Text.Length = 0;
			Text.Append((int)q.Kind).Append(';').Append(q.Giver)
				.Append(';')
				.Append(Clean(q.GiverName))
				.Append(';')
				.Append(q.Holder)
				.Append(';')
				.Append(q.Target)
				.Append(';')
				.Append(Clean(q.TargetName))
				.Append(';')
				.Append(q.Target2)
				.Append(';')
				.Append(Clean(q.Target2Name))
				.Append(';')
				.Append(Clean(q.Thing))
				.Append(';')
				.Append(q.Need)
				.Append(';')
				.Append(q.Have)
				.Append(';')
				.Append(q.X)
				.Append(';')
				.Append(q.Y)
				.Append(';')
				.Append(Clean(q.Place))
				.Append(';')
				.Append(q.Money)
				.Append(';')
				.Append(q.Renown)
				.Append(';')
				.Append(q.Xp)
				.Append(';')
				.Append(q.Opinion)
				.Append(';')
				.Append(q.Deadline.ToString("R", CultureInfo.InvariantCulture))
				.Append(';')
				.Append(q.Stage)
				.Append(';')
				.Append(q.FromBoard ? 1 : 0)
				.Append(';')
				.Append(q.Wait.ToString("R", CultureInfo.InvariantCulture))
				.Append(';')
				.Append(q.Bench ? 1 : 0)
				.Append(';')
				.Append(q.Taken)
				.Append(';')
				.Append(q.Posted)
				.Append(';')
				.Append(Clean(q.Extra));
			return Text.ToString();
		}

		public static Quest Read(BaseSystemData data, string key = "fp_q")
		{
			string pResult;
			try
			{
				data.get(key, out pResult, null);
			}
			catch
			{
				return null;
			}
			Quest quest = Parse(pResult);
			if (quest != null && quest.Holder > 0 && quest.Stage < 2 && Stale(quest))
			{
				try
				{
					data.removeString(key);
				}
				catch
				{
				}
				FirstPerson.Log.Info("quests: dropped " + quest.Kind.ToString() + " for " + quest.GiverName + ": its holder is gone or its time ran out");
				return null;
			}
			return quest;
		}

		private static bool Stale(Quest q)
		{
			Actor actor = WorldBoxMod.Instance?.Host;
			try
			{
				if (actor != null && actor.isAlive() && actor.data.id == q.Holder)
				{
					return false;
				}
			}
			catch
			{
			}
			if (Chronicle.Find(q.Holder) == null)
			{
				return true;
			}
			if (q.Deadline > 0.0)
			{
				return Now > q.Deadline;
			}
			return false;
		}

		public static Quest Parse(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return null;
			}
			string[] array = s.Split(';');
			if (array.Length < 21)
			{
				return null;
			}
			try
			{
				CultureInfo invariantCulture = CultureInfo.InvariantCulture;
				return new Quest
				{
					Kind = (QuestKind)int.Parse(array[0]),
					Giver = long.Parse(array[1]),
					GiverName = array[2],
					Holder = long.Parse(array[3]),
					Target = long.Parse(array[4]),
					TargetName = array[5],
					Target2 = long.Parse(array[6]),
					Target2Name = array[7],
					Thing = array[8],
					Need = int.Parse(array[9]),
					Have = int.Parse(array[10]),
					X = int.Parse(array[11]),
					Y = int.Parse(array[12]),
					Place = ((array[13].Length > 0) ? array[13] : null),
					Money = int.Parse(array[14]),
					Renown = int.Parse(array[15]),
					Xp = int.Parse(array[16]),
					Opinion = int.Parse(array[17]),
					Deadline = double.Parse(array[18], invariantCulture),
					Stage = int.Parse(array[19]),
					FromBoard = (array[20] == "1"),
					Wait = ((array.Length > 21 && array[21].Length > 0) ? double.Parse(array[21], invariantCulture) : 0.0),
					Bench = (array.Length > 22 && array[22] == "1"),
					Taken = ((array.Length > 23 && array[23].Length > 0) ? int.Parse(array[23]) : 0),
					Posted = ((array.Length > 24 && array[24].Length > 0) ? int.Parse(array[24]) : (-1)),
					Extra = ((array.Length > 25 && array[25].Length > 0) ? array[25] : null)
				};
			}
			catch
			{
				return null;
			}
		}

		public static List<Quest> Today(City c, Actor you)
		{
			List<Quest> list = TodayAll(c, you);
			int level = LevelOf(you).Level;
			bool flag = false;
			foreach (Quest item in list)
			{
				if (LevelOfKind(item.Kind) > level)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return list;
			}
			List<Quest> list2 = new List<Quest>(list.Count);
			foreach (Quest item2 in list)
			{
				if (LevelOfKind(item2.Kind) <= level)
				{
					list2.Add(item2);
				}
			}
			return list2;
		}

		public static int TodayCount(City c, Actor you)
		{
			List<Quest> list = TodayAll(c, you);
			int level = LevelOf(you).Level;
			int num = 0;
			foreach (Quest item in list)
			{
				if (LevelOfKind(item.Kind) <= level)
				{
					num++;
				}
			}
			return num;
		}

		private static List<Quest> TodayAll(City c, Actor you)
		{
			bool flag = false;
			try
			{
				flag = c != null && c.isAlive() && c.data != null;
			}
			catch
			{
			}
			if (!flag)
			{
				return new List<Quest>();
			}
			long key = 0L;
			string pResult = null;
			try
			{
				key = c.data.id;
				c.data.get("fp_dq", out pResult, null);
			}
			catch
			{
			}
			int billDay = FirstPerson.Towns.Calendar.BillDay;
			if (pResult != null && DailyCache.TryGetValue(key, out var value) && value.Day == billDay && value.Raw == pResult)
			{
				return value.Notices;
			}
			if (!PostedOn(pResult, out var day) || day != billDay)
			{
				pResult = Post(c, you, billDay);
				try
				{
					c.data.set("fp_dq", pResult);
				}
				catch
				{
				}
			}
			List<Quest> list = new List<Quest>();
			string[] array = pResult.Split('|');
			for (int i = 1; i < array.Length; i++)
			{
				Quest quest = Parse(array[i]);
				if (quest != null)
				{
					list.Add(quest);
				}
			}
			if (DailyCache.Count > 64)
			{
				DailyCache.Clear();
			}
			DailyCache[key] = new Posted
			{
				Raw = pResult,
				Day = billDay,
				Notices = list
			};
			return list;
		}

		private static bool PostedOn(string s, out int day)
		{
			day = -1;
			if (string.IsNullOrEmpty(s))
			{
				return false;
			}
			int num = s.IndexOf('|');
			return int.TryParse((num < 0) ? s : s.Substring(0, num), out day);
		}

		private static string Post(City c, Actor you, int day)
		{
			List<Quest> notices = new List<Quest>();
			try
			{
				List<Actor> people = new List<Actor>();
				foreach (Actor item in Civil.PeopleOf(c))
				{
					if (item != null && item != you && item.isAlive() && item.isAdult() && People.IsHumanoid(item))
					{
						Quest quest = Read(item.data);
						if (quest == null || quest.Stage >= 2)
						{
							people.Add(item);
						}
					}
				}
				System.Random random = new System.Random((int)((c.data.id * 131 + day * 7919) & 0x7FFFFFFF));
				for (int num = people.Count - 1; num > 0; num--)
				{
					int index = random.Next(num + 1);
					Actor value = people[num];
					people[num] = people[index];
					people[index] = value;
				}
				HashSet<Actor> used = new HashSet<Actor>();
				Try(MakeMeat, people.Count);
				if (!notices.Exists((Quest n) => n.Kind == QuestKind.Meat) && !Civil.PeopleOf(c).Exists(IsHunter))
				{
					Quest quest2 = Read(c.data, "fp_hq");
					Quest quest3 = ((quest2 != null && quest2.Stage < 2) ? null : MakeTownMeat(c, you));
					if (quest3 != null)
					{
						quest3.Posted = day;
						notices.Add(quest3);
					}
				}
				Try(MakeSupply, 1);
				Try(MakeHunt, 4);
				Try(MakeInvestigation, 12);
				Try(MakeErrand, 8);
				Try(MakeCraft, 10);
				Try(MakeEscort, 8);
				Try(MakeFaith, 6);
				Try(MakeMatch, 8);
				Try(MakeHunt, 4);
				Try(MakeErrand, 8);
				if (TownLevelTask(c) == null)
				{
					Actor actor = people.Find(GivesHigherWork);
					if (actor != null && you != null)
					{
						Quest quest4 = null;
						Quest quest5 = null;
						try
						{
							quest4 = MakeRaid(actor, you);
						}
						catch
						{
						}
						try
						{
							quest5 = MakeUndead(actor, you);
						}
						catch
						{
						}
						if (quest4 != null)
						{
							quest4.Posted = day;
							notices.Insert(0, quest4);
						}
						if (quest5 != null)
						{
							quest5.Posted = day;
							notices.Insert((quest4 != null) ? 1 : 0, quest5);
						}
					}
				}
				void Try(Func<Actor, Actor, Quest> make, int tries)
				{
					if (notices.Count >= 9)
					{
						return;
					}
					int num2 = 0;
					foreach (Actor item2 in people)
					{
						if (!used.Contains(item2))
						{
							if (num2++ >= tries)
							{
								break;
							}
							Quest q = null;
							try
							{
								q = make(item2, you);
							}
							catch
							{
							}
							if (q != null && (q.Target == 0L || !notices.Exists((Quest o) => o.Target == q.Target)))
							{
								q.Posted = day;
								notices.Add(q);
								used.Add(item2);
								break;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: daily board: " + ex.Message);
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(day);
			foreach (Quest item3 in notices)
			{
				stringBuilder.Append('|').Append(Write(item3));
			}
			string arg = "?";
			try
			{
				arg = c.name;
			}
			catch
			{
			}
			FirstPerson.Log.Info($"quests: {arg}'s board, day {day}: " + ((notices.Count == 0) ? "nothing" : string.Join(", ", notices.Select((Quest q) => q.Kind.ToString() + " for " + q.GiverName).ToArray())));
			return stringBuilder.ToString();
		}

		public static bool TakeNotice(City c, Quest q, Actor you, Action<string> toast)
		{
			if (q.FromBoard && (q.Kind == QuestKind.Undead || q.Kind == QuestKind.Raid))
			{
				return TakeLevelNotice(c, q, you, toast);
			}
			if (q.FromBoard)
			{
				return TakeTownNotice(c, q, you, toast);
			}
			Actor actor = Chronicle.Find(q.Giver);
			if (actor == null || !actor.isAlive())
			{
				toast?.Invoke(q.GiverName + " is gone. The notice is out of date.");
				Unpin(c, q);
				return false;
			}
			Quest quest = Read(actor.data);
			if (quest != null && quest.Stage < 2)
			{
				toast?.Invoke("Someone has already taken that one.");
				Unpin(c, q);
				return false;
			}
			if (q.Target != 0L && (q.Kind == QuestKind.Hunt || q.Kind == QuestKind.Errand || q.Kind == QuestKind.Investigation))
			{
				Actor actor2 = Chronicle.Find(q.Target);
				bool flag = false;
				try
				{
					flag = actor2?.isAlive() ?? false;
				}
				catch
				{
				}
				if (!flag)
				{
					toast?.Invoke("That notice is out of date.");
					Unpin(c, q);
					return false;
				}
			}
			if (!Accept(q, you, toast))
			{
				return false;
			}
			Unpin(c, q);
			return true;
		}

		private static void Unpin(City c, Quest q)
		{
			try
			{
				c.data.get("fp_dq", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return;
				}
				List<string> list = pResult.Split('|').ToList();
				for (int i = 1; i < list.Count; i++)
				{
					Quest quest = Parse(list[i]);
					if (quest != null && quest.Giver == q.Giver && quest.Kind == q.Kind)
					{
						list.RemoveAt(i);
						break;
					}
				}
				c.data.set("fp_dq", string.Join("|", list.ToArray()));
			}
			catch
			{
			}
		}

		public static List<Quest> TakenToday(City c)
		{
			List<Quest> list = new List<Quest>();
			int billDay = FirstPerson.Towns.Calendar.BillDay;
			foreach (Quest item in Log)
			{
				if (item.FromBoard && item.Kind == QuestKind.Meat)
				{
					if (c != null && item.Giver == c.data.id)
					{
						list.Add(item);
					}
				}
				else if (item.FromBoard && (item.Kind == QuestKind.Undead || item.Kind == QuestKind.Raid))
				{
					if (c != null && item.Giver == c.data.id)
					{
						list.Add(item);
					}
				}
				else if (item.Posted == billDay)
				{
					Actor actor = Chronicle.Find(item.Giver);
					if (actor != null && actor.city == c)
					{
						list.Add(item);
					}
				}
			}
			return list;
		}

		public static int DealsToday(Actor npc)
		{
			if (npc?.data == null)
			{
				return 0;
			}
			string pResult = null;
			try
			{
				npc.data.get("fp_deals", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return 0;
			}
			int num = pResult.IndexOf(':');
			if (num < 0 || !int.TryParse(pResult.Substring(0, num), out var result) || result != FirstPerson.Towns.Calendar.BillDay)
			{
				return 0;
			}
			if (!int.TryParse(pResult.Substring(num + 1), out var result2))
			{
				return 0;
			}
			return result2;
		}

		public static void NoteDeal(Actor npc)
		{
			if (npc?.data == null)
			{
				return;
			}
			int num = DealsToday(npc) + 1;
			try
			{
				npc.data.set("fp_deals", FirstPerson.Towns.Calendar.BillDay + ":" + num);
			}
			catch
			{
			}
		}

		public static bool DoneForToday(Actor npc)
		{
			return DealsToday(npc) >= 3;
		}

		internal static string EnoughToday()
		{
			return Dialogue.Pick("You've done enough for me today. Come back tomorrow.", "That's plenty for one day. Ask me again tomorrow.", "Nothing more today. Try me tomorrow.");
		}

		public static Quest Offer(Actor giver, Actor you)
		{
			if (giver == null || you == null)
			{
				return null;
			}
			if (ForceKindForTest >= 0)
			{
				return WithTools(MakeForTest((QuestKind)ForceKindForTest, giver, you), giver, you);
			}
			if (IsHunter(giver))
			{
				Quest quest = null;
				try
				{
					quest = MakeMeat(giver, you);
				}
				catch (Exception ex)
				{
					FirstPerson.Log.Error("quests: offer a hunt: " + ex.Message);
				}
				if (quest != null)
				{
					return WithTools(quest, giver, you);
				}
			}
			List<Func<Actor, Actor, Quest>> list = new List<Func<Actor, Actor, Quest>> { MakeInvestigation, MakeHunt, MakeSupply, MakeErrand, MakeCraft, MakeMatch, MakeFaith, MakeEscort };
			System.Random random = new System.Random((int)(giver.data.id * 31 + Chronicle.Year));
			for (int num = list.Count - 1; num > 2; num--)
			{
				int index = 3 + random.Next(num - 2);
				Func<Actor, Actor, Quest> value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			foreach (Func<Actor, Actor, Quest> item in list)
			{
				Quest quest2 = null;
				try
				{
					quest2 = item(giver, you);
				}
				catch (Exception ex2)
				{
					FirstPerson.Log.Error("quests: offer: " + ex2.Message);
				}
				if (quest2 != null)
				{
					return WithTools(quest2, giver, you);
				}
			}
			return null;
		}

		private static Quest WithTools(Quest q, Actor giver, Actor you)
		{
			if (q == null || IsCourt(q.Kind) || q.Kind == QuestKind.Bounty || !Workbench.WantsTools(you))
			{
				return q;
			}
			if (ToolsForTest >= 0)
			{
				q.Bench = ToolsForTest > 0;
				return q;
			}
			q.Bench = q.Kind == QuestKind.Craft || new System.Random((int)((giver.data.id * 131 + Chronicle.Year * 7) & 0x7FFFFFFF)).Next(4) == 0;
			return q;
		}

		internal static Quest MakeForTest(QuestKind kind, Actor giver, Actor you)
		{
			switch (kind)
			{
			case QuestKind.Supply:
				return MakeSupply(giver, you);
			case QuestKind.Hunt:
				return MakeHunt(giver, you);
			case QuestKind.Errand:
				return MakeErrand(giver, you);
			case QuestKind.Craft:
				return MakeCraft(giver, you);
			case QuestKind.Matchmaker:
				return MakeMatch(giver, you);
			case QuestKind.Escort:
				return MakeEscort(giver, you);
			case QuestKind.Faith:
			case QuestKind.Pilgrimage:
				return MakeFaith(giver, you);
			case QuestKind.Investigation:
				return MakeInvestigation(giver, you);
			case QuestKind.Meat:
				return MakeMeat(giver, you);
			case QuestKind.Undead:
				return MakeUndead(giver, you);
			case QuestKind.Raid:
				return MakeRaid(giver, you);
			default:
				if (!IsCourt(kind))
				{
					return null;
				}
				return MakeCourtKind(kind, giver, you);
			}
		}

		private static Quest New(QuestKind kind, Actor giver, int money, int renown, int xp, int opinion)
		{
			return new Quest
			{
				Kind = kind,
				Giver = giver.data.id,
				GiverName = Dialogue.NameOf(giver),
				Money = money,
				Renown = renown,
				Xp = xp,
				Opinion = opinion
			};
		}

		private static Quest MakeSupply(Actor giver, Actor you)
		{
			City city = giver.city;
			if (city == null)
			{
				return null;
			}
			int num = Mathf.Max(1, city.getPopulationPeople());
			string text = null;
			int num2 = 0;
			string[] foodIds = FoodIds;
			foreach (string pResourceID in foodIds)
			{
				num2 += city.getResourcesAmount(pResourceID);
			}
			if (num2 < num * 2)
			{
				text = Dialogue.Pick("berries", "wheat");
			}
			else if (city.getResourcesAmount("wood") < 20 + num)
			{
				text = "wood";
			}
			else if (city.getResourcesAmount("stone") < 12 + num / 2)
			{
				text = "stone";
			}
			if (text == null)
			{
				return null;
			}
			Quest quest = New(QuestKind.Supply, giver, 0, 3, 6, 12);
			quest.Thing = text;
			quest.Need = ((text == "wood" || text == "stone") ? 8 : 6);
			quest.Money = quest.Need * 3;
			quest.Place = city.name;
			return quest;
		}

		internal static bool Hunted(long id)
		{
			if (id <= 0)
			{
				return false;
			}
			foreach (Quest item in Log)
			{
				if (item.Target == id && item.Stage < 2)
				{
					return true;
				}
			}
			return false;
		}

		private static Quest MakeHunt(Actor giver, Actor you)
		{
			Vector2 current_position = giver.current_position;
			Actor actor = null;
			float num = 2025f;
			LevelState levelState = LevelOf(you);
			bool flag = levelState.Level <= 1 && levelState.Done1 < 5;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != you && simple.isAlive() && simple.asset != null && Beasts.Contains(simple.asset.id) && (!flag || !Strong.Contains(simple.asset.id)) && (simple.kingdom == null || giver.kingdom == null || simple.kingdom != giver.kingdom) && Law.Lawful(simple, you) && !Hunted(simple.data.id))
				{
					float sqrMagnitude = (simple.current_position - current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						actor = simple;
					}
				}
			}
			if (actor == null)
			{
				return null;
			}
			int num2 = Mathf.Clamp(actor.getMaxHealth() / 20, 1, 20);
			Quest quest = New(QuestKind.Hunt, giver, 10 + num2 * 4, 4 + num2, 10 + num2 * 2, 15);
			quest.Target = actor.data.id;
			quest.TargetName = actor.asset.id.Replace('_', ' ');
			return quest;
		}

		private static Quest MakeErrand(Actor giver, Actor you)
		{
			if (giver.city == null)
			{
				return null;
			}
			FamilyTree familyTree = Dialogue.TreeOf(giver);
			List<TreeNode> far = new List<TreeNode>();
			TreeNode[] parents = familyTree.Parents;
			for (int i = 0; i < parents.Length; i++)
			{
				Consider(parents[i]);
			}
			foreach (TreeNode child in familyTree.Children)
			{
				Consider(child);
			}
			foreach (TreeNode sibling in familyTree.Siblings)
			{
				Consider(sibling);
			}
			parents = familyTree.Grandparents;
			for (int i = 0; i < parents.Length; i++)
			{
				Consider(parents[i]);
			}
			if (far.Count == 0)
			{
				return null;
			}
			TreeNode treeNode = far[UnityEngine.Random.Range(0, far.Count)];
			float magnitude = (treeNode.Actor.current_position - giver.current_position).magnitude;
			Quest quest = New(QuestKind.Errand, giver, 8 + (int)(magnitude / 6f), 3, 8, 18);
			quest.Target = treeNode.Id;
			quest.TargetName = treeNode.Name;
			quest.Thing = treeNode.Role;
			quest.Place = treeNode.Actor.city.name;
			return quest;
			void Consider(TreeNode n)
			{
				if (n != null && !n.Dead && n.Actor != null && n.Actor.isAlive())
				{
					City city = n.Actor.city;
					if (city != null && city != giver.city && n.Actor != you)
					{
						far.Add(n);
					}
				}
			}
		}

		private static Quest MakeCraft(Actor giver, Actor you)
		{
			try
			{
				if (giver.getWeapon() != null || !giver.isAdult() || giver.equipment == null)
				{
					return null;
				}
			}
			catch
			{
				return null;
			}
			object obj2;
			switch (Dialogue.JobName(giver) ?? "")
			{
			default:
				obj2 = "sword_";
				break;
			case "farmer":
			case "gatherer":
				obj2 = "spear_";
				break;
			case "miner":
				obj2 = "hammer_";
				break;
			case "warrior":
				obj2 = "sword_";
				break;
			case "hunter":
				obj2 = "bow_";
				break;
			case "woodcutter":
				obj2 = "axe_";
				break;
			}
			string text = (string)obj2;
			bool flag = false;
			foreach (EquipmentAsset item in AssetManager.items.list)
			{
				if (item != null && item.id != null && item.id.StartsWith(text, StringComparison.Ordinal))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return null;
			}
			Quest quest = New(QuestKind.Craft, giver, 18, 3, 8, 16);
			quest.Thing = text;
			return quest;
		}

		private static Quest MakeMatch(Actor giver, Actor you)
		{
			if (giver.hasLover() || !giver.isAdult() || giver.city == null || giver == you)
			{
				return null;
			}
			Actor actor = null;
			float num = float.MaxValue;
			foreach (Actor item in Civil.PeopleOf(giver.city))
			{
				if (item != null && item != giver && item != you && item.isAlive() && item.isAdult() && !item.hasLover() && CanLove(giver, item))
				{
					float sqrMagnitude = (item.current_position - giver.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						actor = item;
					}
				}
			}
			if (actor == null)
			{
				return null;
			}
			Quest quest = New(QuestKind.Matchmaker, giver, 15, 2, 6, 25);
			quest.Target = actor.data.id;
			quest.TargetName = Dialogue.NameOf(actor);
			return quest;
		}

		public static bool CanLove(Actor a, Actor b)
		{
			try
			{
				return a.canFallInLoveWith(b);
			}
			catch
			{
				try
				{
					return a.isSameSpecies(b) && Models.IsFemale(a) != Models.IsFemale(b) && a.isAdult() && b.isAdult();
				}
				catch
				{
					return false;
				}
			}
		}

		private static Quest MakeFaith(Actor giver, Actor you)
		{
			Religion religion = giver.religion;
			if (religion == null)
			{
				return null;
			}
			if (you.religion == religion)
			{
				Quest quest = New(QuestKind.Faith, giver, 20, 5, 10, 18);
				quest.Thing = religion.name;
				quest.Need = 2 + UnityEngine.Random.Range(0, 2);
				return quest;
			}
			City city = CityById(religion.data.creator_city_id);
			if (city == null || city == giver.city)
			{
				return null;
			}
			WorldTile tile = city.getTile();
			if (tile == null)
			{
				return null;
			}
			Quest quest2 = New(QuestKind.Pilgrimage, giver, 14, 6, 12, 14);
			quest2.Thing = religion.name;
			quest2.Place = city.name;
			quest2.X = tile.x;
			quest2.Y = tile.y;
			return quest2;
		}

		private static Quest MakeEscort(Actor giver, Actor you)
		{
			if (giver.city == null || giver.isKing() || giver.isCityLeader() || !giver.isAdult())
			{
				return null;
			}
			City city = null;
			float num = float.MaxValue;
			WorldTile worldTile = giver.current_tile;
			try
			{
				foreach (TileZone zone in giver.city.zones)
				{
					if (zone?.centerTile != null)
					{
						worldTile = zone.centerTile;
						break;
					}
				}
			}
			catch
			{
			}
			foreach (City item in World.world.cities.list)
			{
				if (item == null || !item.isAlive() || item == giver.city || (item.kingdom != null && giver.kingdom != null && item.kingdom.isEnemy(giver.kingdom)))
				{
					continue;
				}
				WorldTile tile = item.getTile();
				if (tile != null)
				{
					float sqrMagnitude = (new Vector2(tile.x, tile.y) - giver.current_position).sqrMagnitude;
					if (!(sqrMagnitude < 400f) && sqrMagnitude < num && OnFoot(item, worldTile))
					{
						num = sqrMagnitude;
						city = item;
					}
				}
			}
			if (city == null || num > 19600f)
			{
				return null;
			}
			WorldTile tile2 = city.getTile();
			Quest quest = New(QuestKind.Escort, giver, 12 + (int)(Mathf.Sqrt(num) / 5f), 4, 10, 20);
			quest.Place = city.name;
			quest.X = tile2.x;
			quest.Y = tile2.y;
			return quest;
		}

		internal static bool OnFoot(City c, WorldTile from)
		{
			if (c == null || from == null)
			{
				return false;
			}
			try
			{
				foreach (TileZone zone in c.zones)
				{
					if (zone?.centerTile != null && zone.centerTile.isSameIsland(from))
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

		private static Quest MakeInvestigation(Actor giver, Actor you)
		{
			Record record = Chronicle.Of(giver);
			bool flag = false;
			long num = long.MinValue;
			if (record != null)
			{
				foreach (Memory memory in record.Memories)
				{
					if (memory.Kind == "got_robbed" && Chronicle.Year - memory.Year <= 2)
					{
						flag = true;
						num = Math.Max(num, memory.Stamp);
					}
				}
			}
			if (!flag)
			{
				return null;
			}
			foreach (Memory memory2 in record.Memories)
			{
				if (memory2.Kind == "fp_quest" && memory2.Other == "the thief" && memory2.Stamp >= num)
				{
					return null;
				}
			}
			Actor actor = null;
			float num2 = 4900f;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != giver && simple != you && simple.isAlive() && simple.hasTrait("thief"))
				{
					float sqrMagnitude = (simple.current_position - giver.current_position).sqrMagnitude;
					if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						actor = simple;
					}
				}
			}
			if (actor == null)
			{
				return null;
			}
			Quest quest = New(QuestKind.Investigation, giver, 25, 6, 12, 20);
			quest.Target = actor.data.id;
			quest.TargetName = Dialogue.NameOf(actor);
			return quest;
		}

		private static void Check(Quest q, Actor you, Action<string> toast)
		{
			if (q.Stage >= 2)
			{
				return;
			}
			if (q.Deadline > 0.0 && Now > q.Deadline && q.Stage != 1)
			{
				Fail(q, you, "too late", toast);
				return;
			}
			Actor actor = (q.FromBoard ? null : Chronicle.Find(q.Giver));
			if (!q.FromBoard && actor == null && IsTrial(q.Kind) && q.Stage == 1)
			{
				TrialPassed(q, you, toast);
				return;
			}
			if (!q.FromBoard && actor == null)
			{
				Fail(q, you, q.GiverName + " is gone", toast);
				return;
			}
			Actor actor2 = Chronicle.Find(q.Target);
			int stage = q.Stage;
			switch (q.Kind)
			{
			case QuestKind.Supply:
				q.Have = Civil.BagCount(you, q.Thing);
				break;
			case QuestKind.Hunt:
			case QuestKind.Bounty:
				if (q.Stage == 0 && actor2 == null)
				{
					if (YouKilled(q.Target) || q.Kind == QuestKind.Hunt)
					{
						q.Stage = 1;
					}
					else
					{
						Fail(q, you, "someone else got there first", toast);
					}
				}
				else if (q.Stage == 0 && actor2.isAlive() && !Law.Lawful(actor2, you))
				{
					Withdrawn(q, you, toast);
				}
				break;
			case QuestKind.Errand:
			case QuestKind.Matchmaker:
			case QuestKind.Investigation:
				if (q.Stage == 0 && actor2 == null)
				{
					Fail(q, you, q.TargetName + " is gone", toast);
				}
				if (q.Kind == QuestKind.Matchmaker && actor != null && actor2 != null && actor.lover == actor2)
				{
					q.Stage = 1;
				}
				break;
			case QuestKind.Craft:
			{
				bool flag = HeldItemLike(you, q.Thing) != null || BagPieceLike(you, q.Thing) >= 0;
				if (q.Stage == 0 && flag)
				{
					q.Stage = 1;
				}
				else if (q.Stage == 1 && !flag)
				{
					q.Stage = 0;
				}
				break;
			}
			case QuestKind.Escort:
				if (actor != null)
				{
					if ((actor.current_position - new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f)).magnitude < 9f)
					{
						Reward(q, you, toast);
						EndEscort(actor);
						return;
					}
					Escort(actor, you);
				}
				break;
			case QuestKind.Pilgrimage:
				if (q.Stage == 0 && (you.current_position - new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f)).magnitude < 8f)
				{
					q.Stage = 1;
					try
					{
						you.changeHappiness("just_felt_the_divine");
					}
					catch
					{
					}
				}
				break;
			case QuestKind.Faith:
				if (q.Have >= q.Need)
				{
					q.Stage = 1;
				}
				break;
			case QuestKind.Meat:
				CheckMeat(q, you, toast);
				if (q.Stage >= 2)
				{
					return;
				}
				break;
			case QuestKind.Undead:
				CheckUndead(q, you, toast);
				break;
			case QuestKind.Raid:
				CheckRaid(q, you, toast);
				if (q.Stage >= 2)
				{
					return;
				}
				break;
			default:
				if (IsTrial(q.Kind))
				{
					CheckTrial(q);
				}
				else if (IsCourt(q.Kind))
				{
					CheckCourt(q, you, actor, actor2, toast);
					if (q.Stage >= 2)
					{
						return;
					}
				}
				break;
			}
			if (q.Stage == stage)
			{
				return;
			}
			Save(q);
			if (IsTrial(q.Kind) && q.Stage == 1)
			{
				try
				{
					you.data.set("fp_trial_done", Write(q));
				}
				catch
				{
				}
			}
			if (q.Stage == 1)
			{
				toast?.Invoke(IsCourt(q.Kind) ? (Title(q) + ". " + Status(q)) : (Title(q) + ": done! " + Status(q)));
			}
		}

		private static void Escort(Actor who, Actor you)
		{
			if (Time.unscaledTime - _escortOrder < 1.2f)
			{
				return;
			}
			_escortOrder = Time.unscaledTime;
			if ((who.current_position - you.current_position).magnitude < 2.5f)
			{
				return;
			}
			Royal.RegisterTask();
			WorldTile tile = World.world.GetTile((int)you.current_position.x, (int)you.current_position.y);
			if (tile == null)
			{
				return;
			}
			try
			{
				who.setTask("fp_royal_march");
				who.beh_tile_target = tile;
			}
			catch
			{
			}
		}

		private static void EndEscort(Actor who)
		{
			try
			{
				who.cancelAllBeh();
			}
			catch
			{
			}
		}

		public static Item HeldItemLike(Actor you, string prefix)
		{
			try
			{
				Item item = you.equipment.getSlot(EquipmentType.Weapon).getItem();
				return (item != null && item.asset != null && item.asset.id.StartsWith(prefix, StringComparison.Ordinal)) ? item : null;
			}
			catch
			{
				return null;
			}
		}

		public static int BagPieceLike(Actor you, string prefix)
		{
			if (string.IsNullOrEmpty(prefix))
			{
				return -1;
			}
			return Gear.Of(you).FindIndex((Gear.Piece p) => p.Id != null && p.Id.StartsWith(prefix, StringComparison.Ordinal));
		}

		private static bool GiveFromBag(Actor you, Actor npc, int index)
		{
			if (index < 0)
			{
				return false;
			}
			try
			{
				Gear.Piece p = Gear.Of(you)[index];
				ActorEquipmentSlot slot = npc.equipment.getSlot(EquipmentType.Weapon);
				Item item = Gear.Build(p, npc);
				if (item == null)
				{
					return false;
				}
				Item item2 = slot.getItem();
				if (item2 != null)
				{
					slot.takeAwayItem();
					Gear.LetGo(item2);
				}
				slot.setItem(item, npc);
				Gear.Remove(you, index);
				BagOrder.PieceGone(you, index);
				try
				{
					npc.setStatsDirty();
				}
				catch
				{
				}
				return true;
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: give from the bag: " + ex.Message);
				return false;
			}
		}

		public static void Converted(Actor you, Actor npc)
		{
			foreach (Quest item in Log)
			{
				if (item.Kind == QuestKind.Faith && item.Stage == 0 && npc.religion != null && npc.religion.name == item.Thing)
				{
					item.Have++;
					Save(item);
				}
			}
		}

		public static void Choices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			Quest mine = null;
			foreach (Quest item in Log)
			{
				if (!item.FromBoard && item.Giver == with.data.id)
				{
					mine = item;
				}
			}
			if (mine != null)
			{
				list.Add(new Choice(IsTrial(mine.Kind) ? "About my trial..." : "About what you asked of me...", delegate
				{
					Progress(c, mine);
				}, IsCourt(mine.Kind) ? ChoiceTone.Romance : ChoiceTone.Quest));
			}
			else
			{
				Quest quest = Read(with.data);
				if (quest != null && quest.Stage < 2 && quest.Holder != you.data.id)
				{
					list.Add(new Choice("Any work for me?", delegate
					{
						c.Say("Someone's already helping me, thank you.");
						c.Offer(Dialogue.Topics(c));
					}, ChoiceTone.Quest));
				}
				else
				{
					list.Add(new Choice("Any work for me?", delegate
					{
						Ask(c);
					}, ChoiceTone.Quest));
				}
			}
			LevelReportChoices(c, list);
			PickChoice(c, list);
			foreach (Quest item2 in Log)
			{
				if (item2.Stage != 0 || item2.Target != with.data.id)
				{
					continue;
				}
				Quest qq = item2;
				switch (item2.Kind)
				{
				case QuestKind.Errand:
					list.Add(new Choice("I bring word from " + item2.GiverName + ".", delegate
					{
						Deliver(c, qq);
					}, ChoiceTone.Quest));
					break;
				case QuestKind.Matchmaker:
					list.Add(new Choice(item2.GiverName + " is sweet on you.", delegate
					{
						Match(c, qq);
					}, ChoiceTone.Quest));
					break;
				case QuestKind.Investigation:
					list.Add(new Choice("I know you robbed " + item2.GiverName + ".", delegate
					{
						Confront(c, qq);
					}, ChoiceTone.Risk));
					break;
				case QuestKind.CourtBlessing:
					list.Add(new Choice("I wish to court " + item2.GiverName + ".", delegate
					{
						Bless(c, qq);
					}, ChoiceTone.Romance));
					break;
				case QuestKind.CourtRival:
					if (item2.Taken == 0)
					{
						list.Add(new Choice("Stay away from " + item2.GiverName + ".", delegate
						{
							FaceRival(c, qq);
						}, ChoiceTone.Risk));
					}
					break;
				}
			}
		}

		private static void Ask(Conversation c)
		{
			c.LastTopic = "quest";
			Actor with = c.With;
			Actor you = c.You;
			if (c.Voice.Warmth <= -2)
			{
				c.Say("Work? From me? Not for you.");
				c.Offer(Dialogue.Topics(c));
			}
			else if (DoneForToday(with))
			{
				c.Say(EnoughToday());
				c.Offer(Dialogue.Topics(c));
			}
			else if (Log.Count >= 5)
			{
				c.Say($"You've promised {5} people already. Finish something first, then ask me.");
				c.Offer(Dialogue.Topics(c));
			}
			else if (LevelOf(you).Level >= 2 && GivesHigherWork(with))
			{
				LevelMenu(c);
			}
			else
			{
				Everyday(c);
			}
		}

		private static void LevelMenu(Conversation c)
		{
			int level = LevelOf(c.You).Level;
			c.Say(Dialogue.Pick("What kind of work are you after?", "I've work of more than one kind. What will it be?"));
			List<Choice> list = new List<Choice>();
			if (level >= 3)
			{
				list.Add(new Choice("Defend the town.", delegate
				{
					AskLevel(c, QuestKind.Raid);
				}, ChoiceTone.Quest));
			}
			list.Add(new Choice("I'll hunt the dead.", delegate
			{
				AskLevel(c, QuestKind.Undead);
			}, ChoiceTone.Quest));
			list.Add(new Choice("Everyday work.", delegate
			{
				Everyday(c);
			}, ChoiceTone.Quest));
			list.Add(Dialogue.Back(c));
			c.Offer(list);
		}

		private static void AskLevel(Conversation c, QuestKind kind)
		{
			c.LastTopic = "quest";
			Actor with = c.With;
			Actor you = c.You;
			if (kind == QuestKind.Raid && RaidBusy() != null)
			{
				c.Say(RaidBusy());
				c.Offer(Dialogue.Topics(c));
				return;
			}
			Quest quest = TownLevelTask(with.city);
			if (quest != null)
			{
				c.Say((quest.Holder == you.data.id) ? ("You're already seeing to " + Short(quest) + " for us. One thing at a time.") : "Someone's already seeing to that for us.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			Quest quest2 = null;
			try
			{
				quest2 = ((kind == QuestKind.Raid) ? MakeRaid(with, you) : MakeUndead(with, you));
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: offer " + kind.ToString() + ": " + ex.Message);
			}
			if (quest2 == null)
			{
				c.Think(0.6f);
				c.Say((kind == QuestKind.Raid) ? "No one's marching on us that we know of. Thank the gods." : "The dead lie quiet around here, for now.");
				c.Offer(Dialogue.Topics(c));
			}
			else
			{
				Brief(c, WithTools(quest2, with, you));
			}
		}

		private static void Everyday(Conversation c)
		{
			Actor with = c.With;
			Actor you = c.You;
			Quest quest = Offer(with, you);
			if (quest == null)
			{
				c.Think(0.6f);
				c.Say(Dialogue.Pick("Not right now. Things are fine, for once.", "Hm, nothing comes to mind.", "Ask me another day."));
				c.Offer(Dialogue.Topics(c));
			}
			else
			{
				Brief(c, quest);
			}
		}

		private static string[] Pitch(Quest q, Actor npc)
		{
			if (IsCourt(q.Kind))
			{
				return CourtPitch(q, npc);
			}
			if (IsTrial(q.Kind))
			{
				return TrialPitch(q, npc);
			}
			switch (q.Kind)
			{
			case QuestKind.Errand:
				return new string[2]
				{
					"My " + q.Thing + " " + q.TargetName + " lives in " + q.Place + ". I haven't seen them in ages.",
					"Would you carry word to them? Tell them I'm well."
				};
			case QuestKind.Matchmaker:
				return new string[2]
				{
					"*sighs* I'm so lonely.",
					q.TargetName + "... have you seen how " + q.TargetName + " smiles? Would you tell them about me?"
				};
			case QuestKind.Escort:
				return new string[2]
				{
					"I need to go to " + q.Place + ", but the roads aren't safe.",
					"Walk with me there? I'll follow your lead."
				};
			case QuestKind.Faith:
				return new string[2]
				{
					q.Thing + " needs more faithful.",
					$"Preach to the people, win {q.Need} souls for {q.Thing}."
				};
			case QuestKind.Pilgrimage:
				return new string[2]
				{
					q.Thing + " began in " + q.Place + ".",
					"Go there for me, see the holy place, and tell me of it. I'm too old for the road."
				};
			case QuestKind.Investigation:
				return new string[2] { "Someone robbed me! Took my savings in broad daylight.", "Find who did it. I'll reward you." };
			default:
				return new string[1] { "I need help." };
			}
		}

		private static void Progress(Conversation c, Quest q)
		{
			if (IsCourt(q.Kind))
			{
				CourtProgress(c, q);
				return;
			}
			if (IsTrial(q.Kind))
			{
				TrialProgress(c, q);
				return;
			}
			if (q.Kind == QuestKind.Meat)
			{
				MeatProgress(c, q);
				return;
			}
			if (q.Kind == QuestKind.Undead || q.Kind == QuestKind.Raid)
			{
				LevelProgress(c, q);
				return;
			}
			c.LastTopic = "quest";
			Actor you = c.You;
			Actor npc = c.With;
			List<Choice> list = new List<Choice>();
			if (q.Kind == QuestKind.Supply)
			{
				int num = Civil.BagCount(you, q.Thing);
				if (num >= q.Need)
				{
					list.Add(new Choice($"Here: {q.Need} {Work.ResourceName(q.Thing)}.", delegate
					{
						if (Civil.TakeFromBag(you, q.Thing, q.Need))
						{
							int num2 = 0;
							try
							{
								if (npc.city != null)
								{
									num2 = npc.city.addResourcesToRandomStockpile(q.Thing, q.Need);
								}
							}
							catch
							{
							}
							if (num2 < q.Need)
							{
								try
								{
									npc.addToInventory(q.Thing, q.Need - num2);
								}
								catch
								{
								}
							}
							Reward(q, you, c.Toast);
							c.Say(Dialogue.Pick("Bless you! The town won't go hungry now.", "Just what we needed. Thank you!"));
						}
						else
						{
							c.Say("Wait... that's not enough.");
						}
						c.Offer(Dialogue.Topics(c));
					}, ChoiceTone.Quest));
				}
				c.Say((num >= q.Need) ? "You've got it? Wonderful!" : $"I still need {q.Need - num} more {Work.ResourceName(q.Thing).ToLowerInvariant()}.");
			}
			else if (q.Kind == QuestKind.Craft && q.Stage == 1)
			{
				Item item = HeldItemLike(you, q.Thing);
				list.Add(new Choice("Here's your new " + ItemWord(q.Thing) + ".", delegate
				{
					if (HandOver(you, npc, item) || (item == null && GiveFromBag(you, npc, BagPieceLike(you, q.Thing))))
					{
						Reward(q, you, c.Toast);
						c.Say("*turns it over in their hands* Fine work. Thank you!");
					}
					else
					{
						c.Say("Hm, I can't take that from you right now.");
					}
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Quest));
				c.Say("Is that it? Is that for me?");
			}
			else
			{
				if (q.Stage == 1)
				{
					Reward(q, you, c.Toast);
					c.Say(Thanks(q));
					c.Offer(Dialogue.Topics(c));
					return;
				}
				c.Say(Waiting(q));
			}
			list.Add(new Choice("I can't do it after all.", delegate
			{
				Abandon(q, you);
				c.Say("Oh. I see. Well... thank you for trying.");
				c.Offer(Dialogue.Topics(c));
			}, ChoiceTone.Risk));
			list.Add(Dialogue.Back(c));
			c.Offer(list);
		}

		private static string Thanks(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.Hunt:
				return Dialogue.Pick("The " + q.TargetName + " is dead? We can sleep again!", "You did it! Here, take this.");
			case QuestKind.Errand:
				return "You saw " + q.TargetName + "? Bless you. Here, for your trouble.";
			case QuestKind.Matchmaker:
				return "*glowing* " + q.TargetName + " and I... thank you, thank you!";
			case QuestKind.Faith:
				return "The faith grows, thanks to you.";
			case QuestKind.Pilgrimage:
				return "You stood where " + q.Thing + " began... tell me everything. And take this.";
			case QuestKind.Investigation:
				return "So it was " + q.TargetName + "! I'll keep my purse closer. Thank you.";
			default:
				return "Thank you! Here's what I promised.";
			}
		}

		private static string Waiting(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.Hunt:
				return "The " + q.TargetName + " is still out there.";
			case QuestKind.Errand:
				return "Have you found " + q.TargetName + " yet? " + q.Place + " isn't far.";
			case QuestKind.Craft:
				return "Still waiting on that " + ItemWord(q.Thing) + ". Press Tab to craft one.";
			case QuestKind.Matchmaker:
				return "Have you spoken to " + q.TargetName + "? *fidgets*";
			case QuestKind.Escort:
				return "Lead the way, I'm right behind you.";
			case QuestKind.Faith:
				return $"{q.Have} of {q.Need} so far. Keep preaching.";
			case QuestKind.Pilgrimage:
				return "Have you been to " + q.Place + "?";
			case QuestKind.Investigation:
				return "Any sign of the thief?";
			default:
				return "Not done yet?";
			}
		}

		private static bool HandOver(Actor you, Actor npc, Item item)
		{
			if (item == null)
			{
				return false;
			}
			try
			{
				ActorEquipmentSlot slot = you.equipment.getSlot(EquipmentType.Weapon);
				ActorEquipmentSlot slot2 = npc.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != item)
				{
					return false;
				}
				slot.takeAwayItem();
				slot2.setItem(item, npc);
				try
				{
					you.setStatsDirty();
					npc.setStatsDirty();
				}
				catch
				{
				}
				return true;
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: hand over: " + ex.Message);
				return false;
			}
		}

		private static void Deliver(Conversation c, Quest q)
		{
			c.LastTopic = "quest";
			q.Stage = 1;
			Save(q);
			try
			{
				c.With.changeHappiness("just_received_gift");
			}
			catch
			{
			}
			Chronicle.ChangeOpinion(c.With, c.You, 8);
			c.Say("From " + q.GiverName + "? *eyes light up* Tell them I miss them. Tell them I'm well!");
			c.Toast?.Invoke(Title(q) + ": go back to " + q.GiverName);
			c.Offer(Dialogue.Topics(c));
		}

		private static void Match(Conversation c, Quest q)
		{
			c.LastTopic = "quest";
			Actor actor = Chronicle.Find(q.Giver);
			Actor with = c.With;
			if (actor == null)
			{
				c.Say("Who?");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			bool flag = !with.hasLover() && !actor.hasLover() && CanLove(with, actor);
			float num = ((MatchChanceForTest >= 0f) ? MatchChanceForTest : (0.55f + (float)Chronicle.Opinion(with, c.You) / 300f + (Dialogue.HasTrait(with, "lustful") ? 0.2f : 0f)));
			if (flag && UnityEngine.Random.value < num)
			{
				try
				{
					with.becomeLoversWith(actor);
				}
				catch (Exception ex)
				{
					FirstPerson.Log.Error("quests: match: " + ex.Message);
				}
				if (with.lover == actor)
				{
					q.Stage = 1;
					Save(q);
					c.Say(q.GiverName + "? Really? *blushes* I've always liked " + q.GiverName + "...", "I'll go to them right now!");
					c.Romantic = true;
					c.Hearts?.Invoke(with);
					c.Offer(Dialogue.Topics(c));
					return;
				}
			}
			c.Say(with.hasLover() ? ("I'm already spoken for. Tell " + q.GiverName + " I'm sorry.") : Dialogue.Pick(q.GiverName + "? Hm. Not my type. Sorry.", "I'm flattered, but no."));
			if (with.hasLover() || !flag)
			{
				Fail(q, c.You, q.TargetName + " said no", c.Toast);
			}
			c.Offer(Dialogue.Topics(c));
		}

		private static void Confront(Conversation c, Quest q)
		{
			c.LastTopic = "quest";
			Actor with = c.With;
			Actor you = c.You;
			int num = Chronicle.Opinion(with, you);
			q.Stage = 1;
			Save(q);
			if (num > 10 || Dialogue.HasTrait(with, "honest") || UnityEngine.Random.value < 0.5f)
			{
				int num2 = 0;
				try
				{
					num2 = Mathf.Min(with.money, 20);
					if (num2 > 0)
					{
						with.spendMoney(num2);
						you.addMoney(num2);
					}
				}
				catch
				{
				}
				c.Say("*goes pale* All right, all right! It was me.", (num2 > 0) ? $"Here, take it back. {num2} gold. Don't tell the guards!" : "I've spent it all... I'm sorry.");
			}
			else
			{
				c.Say("*snarls* You can't prove anything!");
				try
				{
					with.addAggro(you);
				}
				catch
				{
				}
				Chronicle.ChangeOpinion(with, you, -25);
			}
			c.Offer(Dialogue.Topics(c));
		}

		public static LevelState LevelOf(Actor a)
		{
			string pResult = null;
			try
			{
				a?.data.get("fp_qlv", out pResult, null);
			}
			catch
			{
			}
			return ParseProgress(pResult);
		}

		private static LevelState ParseProgress(string s)
		{
			LevelState levelState = new LevelState
			{
				Level = 1
			};
			if (string.IsNullOrEmpty(s))
			{
				return levelState;
			}
			if (string.Equals(s, _parsedRaw, StringComparison.Ordinal))
			{
				return _parsed;
			}
			string[] array = s.Split('|');
			try
			{
				if (array.Length != 0)
				{
					levelState.Level = Math.Max(1, Math.Min(3, int.Parse(array[0])));
				}
				if (array.Length > 1)
				{
					levelState.Done1 = Math.Max(0, int.Parse(array[1]));
				}
				if (array.Length > 2)
				{
					levelState.Done2 = Math.Max(0, int.Parse(array[2]));
				}
				if (array.Length > 3)
				{
					levelState.Done3 = Math.Max(0, int.Parse(array[3]));
				}
			}
			catch
			{
			}
			_parsedRaw = s;
			_parsed = levelState;
			return levelState;
		}

		private static string ProgressText(LevelState p)
		{
			return p.Level + "|" + p.Done1 + "|" + p.Done2 + "|" + p.Done3;
		}

		private static void SaveProgress(Actor a, LevelState p)
		{
			try
			{
				a.data.set("fp_qlv", ProgressText(p));
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: level: " + ex.Message);
			}
		}

		public static int LevelOfKind(QuestKind k)
		{
			switch (k)
			{
			default:
				return 1;
			case QuestKind.Raid:
				return 3;
			case QuestKind.Undead:
				return 2;
			}
		}

		private static bool CountsForLevel(QuestKind k)
		{
			if (!IsCourt(k))
			{
				return !IsTrial(k);
			}
			return false;
		}

		private static void LevelDone(Actor you, Quest q, Action<string> toast)
		{
			if (you == null || !CountsForLevel(q.Kind))
			{
				return;
			}
			LevelState p = LevelOf(you);
			int level = p.Level;
			switch (LevelOfKind(q.Kind))
			{
			case 1:
				p.Done1++;
				break;
			case 2:
				p.Done2++;
				break;
			default:
				p.Done3++;
				break;
			}
			if (p.Level < 2 && p.Done1 >= 10)
			{
				p.Level = 2;
			}
			if (p.Level < 3 && p.Done2 >= 15)
			{
				p.Level = 3;
			}
			SaveProgress(you, p);
			if (LevelOfKind(q.Kind) == 3)
			{
				Ability ability = Abilities.LearnFromRaid(you);
				if (ability != null)
				{
					toast?.Invoke("New spell: " + ability.Name + ". Put it on X from the Magic page (Tab).");
				}
			}
			if (p.Level == level)
			{
				return;
			}
			FirstPerson.Log.Info($"quests: {WorldBoxMod.Describe(you)} reached quest level {p.Level} ({ProgressText(p)})");
			toast?.Invoke((p.Level == 2) ? "Quest level 2: a town's guards, its leader and its board now ask you to put the dead to rest." : "Quest level 3: a town's guards, its leader and its board now ask you to hold the town against raids.");
			try
			{
				WorldBoxMod.Instance?.Audio.PlayRustle();
			}
			catch
			{
			}
		}

		private static void LevelRep(Quest q, Actor you, Actor giver)
		{
			City city = ((q.Kind == QuestKind.Raid) ? CityById(q.Target) : null);
			try
			{
				if (city == null)
				{
					city = (q.FromBoard ? CityById(q.Giver) : giver?.city);
				}
			}
			catch
			{
			}
			if (city == null || you == null)
			{
				return;
			}
			int amount = ((q.Kind == QuestKind.Raid) ? (400 + 40 * Math.Max(0, RaidPart(q, 2))) : (120 + 25 * Math.Max(0, q.Need)));
			try
			{
				Rep.Add(you, city, amount, (q.Kind == QuestKind.Raid) ? "raid held" : "the dead put to rest");
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: level reputation: " + ex.Message);
			}
		}

		public static void InheritLevel(string fromRaw, Actor to)
		{
			if (to != null && !string.IsNullOrEmpty(fromRaw))
			{
				LevelState levelState = ParseProgress(fromRaw);
				LevelState levelState2 = LevelOf(to);
				LevelState p = new LevelState
				{
					Level = Math.Max(levelState.Level, levelState2.Level),
					Done1 = Math.Max(levelState.Done1, levelState2.Done1),
					Done2 = Math.Max(levelState.Done2, levelState2.Done2),
					Done3 = Math.Max(levelState.Done3, levelState2.Done3)
				};
				SaveProgress(to, p);
				FirstPerson.Log.Info($"quests: {WorldBoxMod.Describe(to)} keeps quest level {p.Level} ({ProgressText(p)})");
			}
		}

		public static string LevelLine(Actor you)
		{
			LevelState levelState = LevelOf(you);
			if (levelState.Level >= 3)
			{
				return string.Format("Quest level 3: {0} {1} held", levelState.Done3, (levelState.Done3 == 1) ? "raid" : "raids");
			}
			if (levelState.Level == 2)
			{
				return $"Quest level 2: {Math.Min(levelState.Done2, 15)}/{15} hunts of the dead toward level 3";
			}
			return $"Quest level 1: {Math.Min(levelState.Done1, 10)}/{10} tasks toward level 2";
		}

		public static bool GivesHigherWork(Actor a)
		{
			try
			{
				return a != null && a.isAlive() && a.isAdult() && (a.isKing() || a.isCityLeader() || a.isWarrior()) && a.city != null;
			}
			catch
			{
				return false;
			}
		}

		private static Quest ToTown(Quest q, City city)
		{
			if (q == null || city == null)
			{
				return q;
			}
			q.FromBoard = true;
			q.Giver = city.data.id;
			q.GiverName = city.name;
			return q;
		}

		private static Quest TownLevelTask(City c)
		{
			if (c == null)
			{
				return null;
			}
			Quest quest = Read(c.data, "fp_lq");
			if (quest == null || quest.Stage >= 2 || quest.Holder <= 0)
			{
				return null;
			}
			return quest;
		}

		private static void LevelReportChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			if (!GivesHigherWork(with))
			{
				return;
			}
			long id;
			try
			{
				id = with.city.data.id;
			}
			catch
			{
				return;
			}
			foreach (Quest item in Log)
			{
				if ((item.Kind == QuestKind.Undead || item.Kind == QuestKind.Raid) && item.FromBoard && item.Giver == id && item.Stage < 2)
				{
					Quest qq = item;
					list.Add(new Choice((item.Stage != 1) ? ("About " + Title(item).ToLowerInvariant() + "...") : ((item.Kind == QuestKind.Raid) ? (item.Place + " held.") : ("The dead at " + item.Place + " are at rest.")), delegate
					{
						LevelProgress(c, qq);
					}, ChoiceTone.Quest));
				}
			}
		}

		private static void LevelProgress(Conversation c, Quest q)
		{
			c.LastTopic = "quest";
			Actor you = c.You;
			if (q.Stage == 1)
			{
				Reward(q, you, c.Toast);
				c.Say((q.Kind == QuestKind.Raid) ? RaidThanks(q) : UndeadThanks(q));
				OfferPicks(c, delegate
				{
					c.Offer(Dialogue.Topics(c));
				});
				return;
			}
			c.Say((q.Kind == QuestKind.Raid) ? RaidWaiting(q) : UndeadWaiting(q));
			c.Offer(new List<Choice>
			{
				new Choice("I can't do it after all.", delegate
				{
					Abandon(q, you);
					c.Say((q.Kind == QuestKind.Raid) ? "*looks away* Then we'll hold it without you." : "Then someone else will have to go. Thank you for telling me.");
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Risk),
				Dialogue.Back(c)
			});
		}

		private static bool HandInLevel(City c, Quest q, Actor you, Action<string> toast)
		{
			if (c == null || q == null || !q.FromBoard || q.Giver != c.data.id || q.Stage != 1)
			{
				return false;
			}
			Reward(q, you, toast);
			if (PicksOwed(you) > 0)
			{
				toast?.Invoke("Your pick is waiting: talk to a leader or a soldier of " + c.name + ".");
			}
			return true;
		}

		private static bool TakeLevelNotice(City c, Quest q, Actor you, Action<string> toast)
		{
			if (TownLevelTask(c) != null)
			{
				toast?.Invoke("Someone has already taken that one.");
				Unpin(c, q);
				return false;
			}
			if (!Accept(q, you, toast))
			{
				return false;
			}
			Unpin(c, q);
			return true;
		}

		private static string LevelPromise(Quest q)
		{
			if (q.Kind != QuestKind.Raid)
			{
				return q.GiverName + " asked you to destroy the dead at " + q.Place + ", every one. Then report to its leader, a soldier or its board for " + Pay(q) + ".";
			}
			return q.GiverName + " asked you to hold it against the " + (q.Thing?.ToLowerInvariant() ?? "raiders") + ": once the horn sounds, stay in the town until every last one is dead or fled. Then report to its leader, a soldier or its board for " + Pay(q) + ".";
		}

		internal static void LevelDoneForTest(Actor you, QuestKind kind, Action<string> toast)
		{
			LevelDone(you, new Quest
			{
				Kind = kind
			}, toast);
		}

		internal static List<long> SiteIdsForTest(Quest q)
		{
			return new List<long>(SiteIds(q));
		}

		internal static Quest TownLevelTaskForTest(City c)
		{
			return TownLevelTask(c);
		}

		internal static void SetLevelForTest(Actor a, int level, int done1, int done2, int done3)
		{
			SaveProgress(a, new LevelState
			{
				Level = Math.Max(1, Math.Min(3, level)),
				Done1 = done1,
				Done2 = done2,
				Done3 = done3
			});
		}

		internal static HuntPlan PlanOf(Quest q)
		{
			if (q == null || string.IsNullOrEmpty(q.Extra))
			{
				return null;
			}
			if (Plans.TryGetValue(q.Extra, out var value))
			{
				return value;
			}
			HuntPlan huntPlan = ParsePlan(q.Extra);
			if (huntPlan == null)
			{
				return null;
			}
			if (Plans.Count > 16)
			{
				Plans.Clear();
			}
			Plans[q.Extra] = huntPlan;
			return huntPlan;
		}

		private static HuntPlan ParsePlan(string text)
		{
			HuntPlan huntPlan = new HuntPlan();
			try
			{
				string[] array = text.Split('!');
				huntPlan.Big = array[0] == "1";
				for (int i = 1; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(',');
					if (array2.Length < 8)
					{
						continue;
					}
					Ground ground = new Ground
					{
						X = int.Parse(array2[0]),
						Y = int.Parse(array2[1]),
						Prey = array2[2],
						Count = int.Parse(array2[3]),
						Pred = array2[4],
						PredCount = int.Parse(array2[5]),
						Spawned = (array2[6] == "1"),
						Land = array2[7]
					};
					if (array2.Length > 8 && array2[8].Length > 0)
					{
						string[] array3 = array2[8].Split('.');
						for (int j = 0; j < array3.Length; j++)
						{
							if (long.TryParse(array3[j], out var result))
							{
								ground.Ids.Add(result);
							}
						}
					}
					huntPlan.Grounds.Add(ground);
				}
				return huntPlan;
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: hunt plan: " + ex.Message);
				return null;
			}
		}

		private static string PlanText(HuntPlan h)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(h.Big ? '1' : '0');
			foreach (Ground ground in h.Grounds)
			{
				stringBuilder.Append('!').Append(ground.X).Append(',')
					.Append(ground.Y)
					.Append(',')
					.Append(ground.Prey)
					.Append(',')
					.Append(ground.Count)
					.Append(',')
					.Append(ground.Pred ?? "")
					.Append(',')
					.Append(ground.PredCount)
					.Append(',')
					.Append(ground.Spawned ? '1' : '0')
					.Append(',')
					.Append(ground.Land)
					.Append(',');
				for (int i = 0; i < ground.Ids.Count; i++)
				{
					if (i > 0)
					{
						stringBuilder.Append('.');
					}
					stringBuilder.Append(ground.Ids[i]);
				}
			}
			return stringBuilder.ToString();
		}

		private static void SetPlan(Quest q, HuntPlan h)
		{
			q.Extra = PlanText(h);
		}

		public static bool IsBigHunt(Quest q)
		{
			if (q != null && q.Kind == QuestKind.Meat)
			{
				return PlanOf(q)?.Big ?? false;
			}
			return false;
		}

		internal static long HerdTag(Quest q)
		{
			long num = (q.Giver * 1000003 + (long)(q.Deadline * 16.0)) & 0x3FFFFFFFFFFFL;
			if (num == 0L)
			{
				num = 1L;
			}
			if (!q.FromBoard)
			{
				return num;
			}
			return -num;
		}

		public static bool IsHunter(Actor a)
		{
			if (a != null)
			{
				return Dialogue.JobName(a) == "hunter";
			}
			return false;
		}

		private static int FoodIn(City c)
		{
			int num = 0;
			try
			{
				string[] foodIds = FoodIds;
				foreach (string pResourceID in foodIds)
				{
					num += c.getResourcesAmount(pResourceID);
				}
			}
			catch
			{
			}
			return num;
		}

		private static int HuntsDone(Actor you, City c)
		{
			if (you == null || c == null)
			{
				return 0;
			}
			try
			{
				you.data.get("fp_hunts", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return 0;
				}
				string text = c.data.id.ToString();
				string[] array = pResult.Split(',');
				foreach (string text2 in array)
				{
					int num = text2.IndexOf(':');
					if (num > 0 && text2.Substring(0, num) == text && int.TryParse(text2.Substring(num + 1), out var result))
					{
						return result;
					}
				}
			}
			catch
			{
			}
			return 0;
		}

		private static void CountHunt(Actor you, City c)
		{
			if (you == null || c == null)
			{
				return;
			}
			try
			{
				you.data.get("fp_hunts", out var pResult, null);
				string text = c.data.id.ToString();
				List<string> list = new List<string>();
				int result = 0;
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array = pResult.Split(',');
					foreach (string text2 in array)
					{
						int num = text2.IndexOf(':');
						if (num > 0 && text2.Substring(0, num) == text)
						{
							int.TryParse(text2.Substring(num + 1), out result);
						}
						else if (text2.Length > 0)
						{
							list.Add(text2);
						}
					}
				}
				list.Add(text + ":" + (result + 1));
				while (list.Count > 24)
				{
					list.RemoveAt(0);
				}
				you.data.set("fp_hunts", string.Join(",", list.ToArray()));
			}
			catch
			{
			}
		}

		private static Quest MakeMeat(Actor giver, Actor you)
		{
			if (giver == null || giver == you || giver.city == null || !IsHunter(giver))
			{
				return null;
			}
			return MakeMeatFor(giver.city, giver, you);
		}

		private static Quest MakeTownMeat(City c, Actor you)
		{
			if (c != null)
			{
				return MakeMeatFor(c, null, you);
			}
			return null;
		}

		private static Quest MakeMeatFor(City city, Actor giver, Actor you)
		{
			WorldTile worldTile = null;
			try
			{
				worldTile = city.getTile();
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return null;
			}
			int num = Mathf.Max(1, city.getPopulationPeople());
			int num2 = FoodIn(city);
			bool flag = num2 < num * 2;
			bool flag2 = num2 < num;
			bool flag3 = ((BigHuntForTest >= 0) ? (BigHuntForTest > 0) : (flag2 || HuntsDone(you, city) % 4 == 3));
			int num3 = (flag3 ? Mathf.Clamp(16 + num / 4, 16, 30) : Mathf.Clamp(6 + num / 5 + (flag ? 3 : 0), 6, 16));
			System.Random random = new System.Random((int)(((giver?.data.id ?? city.data.id) * 31 + FirstPerson.Towns.Calendar.Day * 7919 + (flag3 ? 1 : 0)) & 0x7FFFFFFF));
			HuntPlan huntPlan = new HuntPlan
			{
				Big = flag3
			};
			PickGrounds(huntPlan, worldTile, flag3 ? 3 : ((num3 <= 9) ? 1 : 2), flag3, random);
			if (huntPlan.Grounds.Count == 0)
			{
				return null;
			}
			int num4 = 0;
			foreach (Ground ground in huntPlan.Grounds)
			{
				if (random.NextDouble() < (flag3 ? 0.5 : 0.3))
				{
					num4 += ArmGround(ground, random);
				}
			}
			if (flag3 && num4 == 0)
			{
				num4 += ArmGround(huntPlan.Grounds[random.Next(huntPlan.Grounds.Count)], random);
			}
			int num5 = (flag3 ? 6 : 3);
			if (flag)
			{
				num5 += ((!flag3) ? 1 : 2);
			}
			Quest quest = new Quest
			{
				Kind = QuestKind.Meat,
				Thing = "meat",
				Need = num3,
				Place = city.name,
				Money = num3 * num5 + (flag3 ? 30 : 0) + num4 * 4,
				Renown = (flag3 ? 8 : 3),
				Xp = (flag3 ? 25 : 10),
				Opinion = (flag3 ? 20 : 12),
				X = huntPlan.Grounds[0].X,
				Y = huntPlan.Grounds[0].Y
			};
			if (giver != null)
			{
				quest.Giver = giver.data.id;
				quest.GiverName = Dialogue.NameOf(giver);
			}
			else
			{
				quest.FromBoard = true;
				quest.Giver = city.data.id;
				quest.GiverName = city.name;
			}
			SetPlan(quest, huntPlan);
			return quest;
		}

		private static int ArmGround(Ground g, System.Random rng)
		{
			if (!string.IsNullOrEmpty(g.Pred))
			{
				return 0;
			}
			string text = Pick(World.world.GetTile(g.X, g.Y), PredatorIds, rng) ?? (AssetManager.actor_library.has("wolf") ? "wolf" : null);
			if (text == null)
			{
				return 0;
			}
			g.Pred = text;
			g.PredCount = ((text == "bear" || text == "crocodile") ? 1 : (2 + rng.Next(2)));
			return g.PredCount;
		}

		private static void PickGrounds(HuntPlan plan, WorldTile home, int count, bool big, System.Random rng)
		{
			float num = (big ? 35f : 30f);
			float num2 = (big ? 80f : 65f);
			for (int i = 0; i < 240; i++)
			{
				if (plan.Grounds.Count >= count)
				{
					break;
				}
				double num3 = rng.NextDouble() * Math.PI * 2.0;
				float num4 = num + (float)rng.NextDouble() * (num2 - num);
				WorldTile tile = World.world.GetTile(home.x + (int)(Math.Cos(num3) * (double)num4), home.y + (int)(Math.Sin(num3) * (double)num4));
				if (!GoodGround(tile, home))
				{
					continue;
				}
				bool flag = true;
				foreach (Ground ground in plan.Grounds)
				{
					if ((ground.X - tile.x) * (ground.X - tile.x) + (ground.Y - tile.y) * (ground.Y - tile.y) < 324)
					{
						flag = false;
					}
				}
				if (!flag)
				{
					continue;
				}
				string text = Pick(tile, PreyIds, rng);
				if (text == null && i < 120)
				{
					continue;
				}
				if (text == null)
				{
					string[] fallbackPrey = FallbackPrey;
					foreach (string text2 in fallbackPrey)
					{
						if (AssetManager.actor_library.has(text2))
						{
							text = text2;
							break;
						}
					}
				}
				if (text == null)
				{
					break;
				}
				plan.Grounds.Add(new Ground
				{
					X = tile.x,
					Y = tile.y,
					Prey = text,
					Land = LandOf(tile)
				});
			}
		}

		private static bool Dry(WorldTile t)
		{
			if (t != null && t.main_type != null && !t.main_type.liquid && !t.main_type.block)
			{
				if (t.Type != null)
				{
					return !t.Type.lava;
				}
				return true;
			}
			return false;
		}

		private static bool GoodGround(WorldTile t, WorldTile home)
		{
			try
			{
				if (!Dry(t) || t.zone_city != null || !t.isSameIsland(home))
				{
					return false;
				}
				int num = 0;
				for (int i = -2; i <= 2; i++)
				{
					for (int j = -2; j <= 2; j++)
					{
						if (Dry(World.world.GetTile(t.x + j, t.y + i)))
						{
							num++;
						}
					}
				}
				return num >= 18;
			}
			catch
			{
				return false;
			}
		}

		private static string Pick(WorldTile t, string[] ids, System.Random rng)
		{
			List<string> list = null;
			try
			{
				list = t?.getBiome()?.pot_units_spawn;
			}
			catch
			{
			}
			if (list == null)
			{
				return null;
			}
			List<string> list2 = new List<string>();
			foreach (string text in ids)
			{
				if (list.Contains(text) && AssetManager.actor_library.has(text))
				{
					list2.Add(text);
				}
			}
			if (list2.Count != 0)
			{
				return list2[rng.Next(list2.Count)];
			}
			return null;
		}

		private static string LandOf(WorldTile t)
		{
			string text = null;
			try
			{
				text = t.getBiome()?.id;
			}
			catch
			{
			}
			switch (text)
			{
			case "biome_grass":
				return "meadows";
			case "biome_savanna":
				return "savanna";
			case "biome_permafrost":
				return "snowfields";
			case "biome_swamp":
				return "swamp";
			case "biome_desert":
				return "desert";
			case "biome_jungle":
				return "jungle";
			case "biome_birch":
				return "birch woods";
			case "biome_maple":
				return "maple woods";
			case "biome_rocklands":
				return "rocky hills";
			case "biome_clover":
				return "clover fields";
			case "biome_mushroom":
				return "mushroom woods";
			case "biome_sand":
				return "dunes";
			case "biome_flower":
				return "flower fields";
			case "biome_enchanted":
				return "enchanted woods";
			default:
				if (string.IsNullOrEmpty(text))
				{
					return "wilds";
				}
				if (!text.StartsWith("biome_", StringComparison.Ordinal))
				{
					return text.Replace('_', ' ');
				}
				return text.Substring(6).Replace('_', ' ');
			}
		}

		private static string Plural(string kind, int n)
		{
			string text = kind.Replace('_', ' ');
			if (n == 1)
			{
				return text;
			}
			switch (text)
			{
			case "sheep":
			case "buffalo":
				return text;
			case "wolf":
				return "wolves";
			case "ostrich":
				return "ostriches";
			default:
				return text + "s";
			}
		}

		private static int MeatOf(Actor a)
		{
			int num = 0;
			try
			{
				foreach (ResourceContainer item in a.getResourcesFromActor())
				{
					if (item.id == "meat")
					{
						num += item.amount;
					}
				}
			}
			catch
			{
			}
			return Mathf.Max(1, num);
		}

		private static void SpawnHerd(Quest q, HuntPlan h, Ground g, Action<string> toast)
		{
			g.Spawned = true;
			WorldTile tile = World.world.GetTile(g.X, g.Y);
			if (tile == null)
			{
				return;
			}
			long num = HerdTag(q);
			int num2 = Mathf.CeilToInt((float)q.Need * 1.4f / (float)Mathf.Max(1, h.Grounds.Count));
			int num3 = 0;
			System.Random random = new System.Random((int)((num * 17 + g.X * 131 + g.Y) & 0x7FFFFFFF));
			g.Count = 0;
			for (int i = 0; i < 14; i++)
			{
				if (num3 >= num2 && g.Count >= 3)
				{
					break;
				}
				Actor actor = SpawnOne(g.Prey, tile, 3, random, num);
				if (actor == null)
				{
					break;
				}
				g.Ids.Add(actor.data.id);
				g.Count++;
				num3 += MeatOf(actor);
			}
			int num4 = 0;
			if (!string.IsNullOrEmpty(g.Pred) && AssetManager.actor_library.has(g.Pred))
			{
				double num5 = random.NextDouble() * Math.PI * 2.0;
				WorldTile worldTile = World.world.GetTile(g.X + (int)(Math.Cos(num5) * 6.0), g.Y + (int)(Math.Sin(num5) * 6.0));
				if (!Dry(worldTile) || !worldTile.isSameIsland(tile))
				{
					worldTile = tile;
				}
				for (int j = 0; j < g.PredCount; j++)
				{
					Actor actor2 = SpawnOne(g.Pred, worldTile, 2, random, num);
					if (actor2 == null)
					{
						break;
					}
					g.Ids.Add(actor2.data.id);
					num4++;
				}
			}
			g.PredCount = num4;
			toast?.Invoke($"Hunting ground ahead: {g.Count} {Plural(g.Prey, g.Count)}" + ((num4 > 0) ? $", and {num4} {Plural(g.Pred, num4)} prowling" : "") + ".");
			FirstPerson.Log.Info($"quests: {q.GiverName}'s hunt: herd at {g.X},{g.Y} ({g.Land}): {g.Count} {g.Prey} (~{num3} meat for a share of {num2})" + ((num4 > 0) ? $", {num4} {g.Pred}" : ""));
		}

		private static Actor SpawnOne(string id, WorldTile center, int spread, System.Random rng, long tag)
		{
			for (int i = 0; i < 12; i++)
			{
				WorldTile tile = World.world.GetTile(center.x + rng.Next(-spread, spread + 1), center.y + rng.Next(-spread, spread + 1));
				if (!Dry(tile) || !tile.isSameIsland(center))
				{
					continue;
				}
				try
				{
					Actor actor = World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					if (actor == null)
					{
						return null;
					}
					actor.data.set("fp_herd", tag.ToString());
					return actor;
				}
				catch (Exception ex)
				{
					FirstPerson.Log.Error("quests: herd " + id + ": " + ex.Message);
					return null;
				}
			}
			return null;
		}

		private static Actor HerdAnimal(long id)
		{
			try
			{
				Actor actor = World.world.units.get(id);
				return (actor != null && actor.isAlive()) ? actor : null;
			}
			catch
			{
				return null;
			}
		}

		private static int HerdAlive(HuntPlan h)
		{
			int num = 0;
			foreach (Ground ground in h.Grounds)
			{
				foreach (long id in ground.Ids)
				{
					if (HerdAnimal(id) != null)
					{
						num++;
					}
				}
			}
			return num;
		}

		public static void OnMeatCut(Actor you, long herd, int meat)
		{
			if (herd == 0L || meat <= 0)
			{
				return;
			}
			foreach (Quest item in Log)
			{
				if (item.Kind == QuestKind.Meat && item.Stage < 2 && HerdTag(item) == herd && IsBigHunt(item))
				{
					item.Have += meat;
					Save(item);
					break;
				}
			}
		}

		private static void CheckMeat(Quest q, Actor you, Action<string> toast)
		{
			HuntPlan huntPlan = (string.IsNullOrEmpty(q.Extra) ? null : ParsePlan(q.Extra));
			if (huntPlan == null)
			{
				Fail(q, you, "the hunt was lost", toast);
				return;
			}
			if (q.FromBoard && CityById(q.Giver) == null)
			{
				Fail(q, you, q.GiverName + " is gone", toast);
				return;
			}
			bool flag = false;
			foreach (Ground ground in huntPlan.Grounds)
			{
				if (!ground.Spawned && !((you.current_position - new Vector2((float)ground.X + 0.5f, (float)ground.Y + 0.5f)).sqrMagnitude > 1600f))
				{
					SpawnHerd(q, huntPlan, ground, toast);
					flag = true;
				}
			}
			if (flag)
			{
				SetPlan(q, huntPlan);
				Save(q);
			}
			int num = Civil.BagCount(you, "meat");
			int have = q.Have;
			q.Have = (huntPlan.Big ? Mathf.Min(q.Have, num) : num);
			if (huntPlan.Big && q.Have != have)
			{
				Save(q);
			}
			if (q.Stage == 0 && q.Have >= q.Need)
			{
				q.Stage = 1;
			}
			else if (q.Stage == 1 && q.Have < q.Need)
			{
				q.Stage = 0;
			}
			if (huntPlan.Big && q.Stage == 0 && huntPlan.Grounds.TrueForAll((Ground g) => g.Spawned) && HerdAlive(huntPlan) == 0 && q.Have + Butchery.HerdMeatLying(HerdTag(q)) < q.Need)
			{
				Fail(q, you, "the herds are gone", toast);
			}
		}

		private static bool MeatHeading(Quest q, out Vector2 at)
		{
			at = default(Vector2);
			HuntPlan huntPlan = PlanOf(q);
			if (huntPlan == null)
			{
				return false;
			}
			Vector2 vector = (WorldBoxMod.Instance?.Host)?.current_position ?? new Vector2(q.X, q.Y);
			float num = float.MaxValue;
			bool flag = false;
			foreach (Ground ground in huntPlan.Grounds)
			{
				foreach (long id in ground.Ids)
				{
					Actor actor = HerdAnimal(id);
					if (actor != null)
					{
						float sqrMagnitude = (actor.current_position - vector).sqrMagnitude;
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							at = actor.current_position;
							flag = true;
						}
					}
				}
			}
			if (flag && num < 8100f)
			{
				return true;
			}
			float num2 = float.MaxValue;
			foreach (Ground ground2 in huntPlan.Grounds)
			{
				if (!ground2.Spawned)
				{
					Vector2 vector2 = new Vector2((float)ground2.X + 0.5f, (float)ground2.Y + 0.5f);
					float sqrMagnitude2 = (vector2 - vector).sqrMagnitude;
					if (sqrMagnitude2 < num2)
					{
						num2 = sqrMagnitude2;
						at = vector2;
						flag = true;
					}
				}
			}
			return flag;
		}

		public static void HuntMarks(List<Vector2> into)
		{
			into.Clear();
			foreach (Quest item in Log)
			{
				if (item.Kind != QuestKind.Meat || item.Stage != 0)
				{
					continue;
				}
				HuntPlan huntPlan = PlanOf(item);
				if (huntPlan == null)
				{
					continue;
				}
				foreach (Ground ground in huntPlan.Grounds)
				{
					if (!ground.Spawned)
					{
						into.Add(new Vector2((float)ground.X + 0.5f, (float)ground.Y + 0.5f));
						continue;
					}
					Vector2 zero = Vector2.zero;
					int num = 0;
					foreach (long id in ground.Ids)
					{
						Actor actor = HerdAnimal(id);
						if (actor != null)
						{
							zero += actor.current_position;
							num++;
						}
					}
					if (num > 0)
					{
						into.Add(zero / num);
					}
				}
			}
		}

		private static void MeatStars(Quest q, List<KeyValuePair<Actor, bool>> into)
		{
			HuntPlan huntPlan = PlanOf(q);
			Actor actor = WorldBoxMod.Instance?.Host;
			if (huntPlan == null || actor == null)
			{
				return;
			}
			int num = 0;
			foreach (Ground ground in huntPlan.Grounds)
			{
				foreach (long id in ground.Ids)
				{
					Actor actor2 = HerdAnimal(id);
					if (actor2 != null && !((actor2.current_position - actor.current_position).sqrMagnitude > 2025f))
					{
						Star(into, actor2, gold: true);
						if (++num >= 16)
						{
							return;
						}
					}
				}
			}
		}

		private static int MeatCounted(Quest q, Actor you)
		{
			int num = Civil.BagCount(you, "meat");
			if (!IsBigHunt(q))
			{
				return num;
			}
			return Mathf.Min(q.Have, num);
		}

		private static bool HandInMeat(Quest q, Actor you, City city, Actor hunter, Action<string> toast)
		{
			if (!StillOpen(q, you))
			{
				toast?.Invoke("That hunt is over.");
				return false;
			}
			if (MeatCounted(q, you) < q.Need || !Civil.TakeFromBag(you, "meat", q.Need))
			{
				return false;
			}
			int num = 0;
			try
			{
				if (city != null && city.hasStorages())
				{
					num = city.addResourcesToRandomStockpile("meat", q.Need);
				}
			}
			catch
			{
			}
			if (num < q.Need && hunter != null)
			{
				try
				{
					hunter.addToInventory("meat", q.Need - num);
				}
				catch
				{
				}
			}
			CountHunt(you, city);
			FirstPerson.Log.Info($"quests: {q.Need} meat handed in for {city?.name ?? q.Place} ({num} into its stores)");
			Reward(q, you, toast);
			return true;
		}

		public static bool HandInAtBoard(City c, Quest q, Actor you, Action<string> toast)
		{
			if (q != null && (q.Kind == QuestKind.Undead || q.Kind == QuestKind.Raid))
			{
				return HandInLevel(c, q, you, toast);
			}
			if (c == null || q == null || !q.FromBoard || q.Kind != QuestKind.Meat || q.Giver != c.data.id)
			{
				return false;
			}
			if (HandInMeat(q, you, c, null, toast))
			{
				return true;
			}
			toast?.Invoke($"The board wants {q.Need} meat" + (IsBigHunt(q) ? " from the herds." : "."));
			return false;
		}

		private static bool TakeTownNotice(City c, Quest q, Actor you, Action<string> toast)
		{
			Quest quest = Read(c.data, "fp_hq");
			if (quest != null && quest.Stage < 2)
			{
				toast?.Invoke("Someone has already taken that one.");
				Unpin(c, q);
				return false;
			}
			if (!Accept(q, you, toast))
			{
				return false;
			}
			Unpin(c, q);
			return true;
		}

		private static void MeatProgress(Conversation c, Quest q)
		{
			c.LastTopic = "quest";
			Actor you = c.You;
			Actor npc = c.With;
			List<Choice> list = new List<Choice>();
			if (MeatCounted(q, you) >= q.Need)
			{
				list.Add(new Choice($"Here: {q.Need} meat.", delegate
				{
					if (HandInMeat(q, you, npc.city, npc, c.Toast))
					{
						c.Say(Dialogue.Pick("Good meat. " + Place(q, npc) + " eats well tonight.", "*weighs it in both hands* That will feed a lot of people. Thank you."));
					}
					else
					{
						c.Say(StillOpen(q, you) ? "Wait... that's not enough." : "Too late. That hunt is over.");
					}
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Quest));
				c.Say("You've got it? Let's see.");
			}
			else
			{
				c.Say(MeatWaiting(q, you));
			}
			list.Add(new Choice("I can't do it after all.", delegate
			{
				Abandon(q, you);
				c.Say("Then I'll go out myself. Thanks for trying.");
				c.Offer(Dialogue.Topics(c));
			}, ChoiceTone.Risk));
			list.Add(Dialogue.Back(c));
			c.Offer(list);
		}

		private static string MeatWaiting(Quest q, Actor you)
		{
			int num = q.Need - MeatCounted(q, you);
			if (IsBigHunt(q))
			{
				return $"I still need {num} more meat, from the herds I sent you to. Meat from the market won't do for this.";
			}
			return $"I still need {num} more meat. Kill, then strike the carcass to cut it up.";
		}

		private static string GroundsInWords(Quest q, Vector2 from, bool all)
		{
			HuntPlan huntPlan = PlanOf(q);
			if (huntPlan == null)
			{
				return "out there";
			}
			List<string> list = new List<string>();
			foreach (Ground ground in huntPlan.Grounds)
			{
				if (all || !ground.Spawned)
				{
					Vector2 d = new Vector2((float)ground.X + 0.5f, (float)ground.Y + 0.5f) - from;
					float magnitude = d.magnitude;
					string text = ((magnitude < 30f) ? "close" : ((magnitude < 60f) ? "a fair walk" : "a long walk"));
					list.Add("the " + ground.Land + " " + Compass(d).ToLowerInvariant() + " (" + text + ", " + Plural(ground.Prey, 2) + ")");
				}
			}
			if (list.Count == 0)
			{
				return "the grounds you've been to";
			}
			if (list.Count == 1)
			{
				return list[0];
			}
			return string.Join(", ", list.GetRange(0, list.Count - 1).ToArray()) + " and " + list[list.Count - 1];
		}

		private static string MeatTitle(Quest q)
		{
			if (!IsBigHunt(q))
			{
				return $"{q.Need} meat for {q.Place}";
			}
			return "Great hunt for " + q.Place;
		}

		private static string MeatStatus(Quest q)
		{
			HuntPlan huntPlan = PlanOf(q);
			int num = 0;
			if (huntPlan != null)
			{
				foreach (Ground ground in huntPlan.Grounds)
				{
					if (!ground.Spawned)
					{
						num++;
					}
				}
			}
			string text = ((num > 0) ? string.Format(" · {0} hunting ground{1} to go", num, (num == 1) ? "" : "s") : "");
			if (IsBigHunt(q))
			{
				return $"{Mathf.Min(q.Have, q.Need)}/{q.Need} meat from the herds" + text;
			}
			return $"{Mathf.Min(q.Have, q.Need)}/{q.Need} meat in your bag" + text;
		}

		private static void MeatQuestions(Quest q, Actor npc, Actor you, Action<string, Func<string>> add)
		{
			add("Where are the hunting grounds?", () => Upper(GroundsInWords(q, you.current_position, all: true)) + ". Your compass shows the way; the herd's there when you arrive.");
			add("How do I get the meat?", () => "Kill the animal, then strike the carcass: each blow cuts a piece, a blade cuts two. Be quick. A carcass doesn't last long.");
			add("Is it dangerous?", delegate
			{
				HuntPlan huntPlan = PlanOf(q);
				List<string> list = new List<string>();
				if (huntPlan != null)
				{
					foreach (Ground ground in huntPlan.Grounds)
					{
						if (!string.IsNullOrEmpty(ground.Pred))
						{
							list.Add(Plural(ground.Pred, 2) + " on the " + ground.Land);
						}
					}
				}
				return (list.Count != 0) ? ("Yes. There are " + string.Join(" and ", list.ToArray()) + ". Kill them first, or they'll take you from behind.") : "Not much. It's grazing game. Watch for anything else out there.";
			});
			if (IsBigHunt(q))
			{
				add("Why only meat from the herds?", () => "Because I know those herds, and what's in them. The market sells whatever's been lying about. The town needs fresh meat.");
			}
		}

		private static string MeatProof(Quest q)
		{
			if (!IsBigHunt(q))
			{
				return "Bring the meat in your bag and hand it to me. I'll count it.";
			}
			return "Bring it to me. I'll know meat from those herds when I see it.";
		}

		private static string MeatGoal(Quest q)
		{
			if (!IsBigHunt(q))
			{
				return $"bring me {q.Need} meat";
			}
			return $"bring me {q.Need} meat from the herds at the three grounds";
		}

		private static string TownHuntPromise(Quest q)
		{
			return $"The board of {q.GiverName} pays {q.Money} gold for {q.Need} meat" + (IsBigHunt(q) ? " from the herds it names" : "") + ". Hand it in at the board.";
		}

		private static List<string> PicksOf(Actor you)
		{
			List<string> list = new List<string>();
			string pResult = null;
			try
			{
				you?.data.get("fp_picks", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return list;
			}
			string[] array = pResult.Split('|');
			foreach (string text in array)
			{
				if (text.Length > 0)
				{
					list.Add(text);
				}
			}
			return list;
		}

		private static void SavePicks(Actor you, List<string> picks)
		{
			try
			{
				if (picks.Count == 0)
				{
					you.data.removeString("fp_picks");
				}
				else
				{
					you.data.set("fp_picks", string.Join("|", picks.ToArray()));
				}
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: picks: " + ex.Message);
			}
		}

		private static void AddPick(Actor you, string token)
		{
			List<string> list = PicksOf(you);
			list.Add(token);
			SavePicks(you, list);
		}

		private static void DonePick(Actor you, string token)
		{
			List<string> list = PicksOf(you);
			list.Remove(token);
			SavePicks(you, list);
		}

		public static int PicksOwed(Actor you)
		{
			return PicksOf(you).Count;
		}

		private static void LevelPicks(Quest q, Actor you)
		{
			LevelState levelState = LevelOf(you);
			if (q.Kind == QuestKind.Undead && levelState.Done2 > 0 && levelState.Done2 % 3 == 0)
			{
				AddPick(you, "d:" + Mathf.Max(20, q.Money / 2));
			}
			if (q.Kind == QuestKind.Raid)
			{
				AddPick(you, "w:" + MaterialFor(levelState.Done3) + ":" + KindYouFightWith(you));
				if (GoodPickForTest || UnityEngine.Random.value < 0.15f)
				{
					AddPick(you, "g:" + Mathf.Max(50, q.Money / 2));
				}
			}
		}

		internal static string MaterialFor(int held)
		{
			if (held < 10)
			{
				if (held < 6)
				{
					if (held < 3)
					{
						return "iron";
					}
					return "steel";
				}
				return "mythril";
			}
			return "adamantine";
		}

		private static string KindYouFightWith(Actor you)
		{
			string text = null;
			try
			{
				text = you.getWeapon()?.asset?.id;
			}
			catch
			{
			}
			string[] weaponKinds = WeaponKinds;
			foreach (string text2 in weaponKinds)
			{
				if (text != null && text.StartsWith(text2 + "_", StringComparison.Ordinal))
				{
					return text2;
				}
			}
			foreach (Gear.Piece item in Gear.Of(you))
			{
				weaponKinds = WeaponKinds;
				foreach (string text3 in weaponKinds)
				{
					if (item.Id != null && item.Id.StartsWith(text3 + "_", StringComparison.Ordinal))
					{
						return text3;
					}
				}
			}
			return "sword";
		}

		private static void PickChoice(Conversation c, List<Choice> list)
		{
			if (PicksOf(c.You).Count == 0 || !GivesHigherWork(c.With))
			{
				return;
			}
			list.Add(new Choice("About the reward I'm owed...", delegate
			{
				c.Say("Ah, yes. Choose, then.");
				OfferPicks(c, delegate
				{
					c.Offer(Dialogue.Topics(c));
				});
			}, ChoiceTone.Quest));
		}

		private static void OfferPicks(Conversation c, Action after)
		{
			List<string> list = PicksOf(c.You);
			if (list.Count == 0)
			{
				after?.Invoke();
				return;
			}
			string text = list[0];
			string[] array = text.Split(':');
			if (array[0] == "w")
			{
				OfferWeapon(c, text, (array.Length > 1) ? array[1] : "iron", (array.Length > 2) ? array[2] : "sword", after);
				return;
			}
			int result;
			int gold = ((array.Length > 1 && int.TryParse(array[1], out result)) ? result : 30);
			OfferTraits(c, text, (array[0] == "g") ? GoodTraits : DecentTraits, array[0] == "g", gold, after);
		}

		internal static List<string> TraitsToPick(Actor you, string[] pool)
		{
			List<string> list = new List<string>();
			foreach (string text in pool)
			{
				bool flag = false;
				try
				{
					flag = you.hasTrait(text) || TraitSwitch.Owns(you, text);
				}
				catch
				{
				}
				if (!flag && AssetManager.traits.get(text) != null)
				{
					list.Add(text);
				}
			}
			System.Random random = new System.Random((int)((you.data.id * 7 + PicksOf(you).Count * 131 + LevelOf(you).Done2 * 17 + LevelOf(you).Done3 * 29) & 0x7FFFFFFF));
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = random.Next(num + 1);
				string value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			if (list.Count > 3)
			{
				list.RemoveRange(3, list.Count - 3);
			}
			return list;
		}

		private static void OfferTraits(Conversation c, string token, string[] pool, bool good, int gold, Action after)
		{
			Actor you = c.You;
			List<string> list = TraitsToPick(you, pool);
			if (list.Count == 0)
			{
				try
				{
					you.addMoney(gold);
				}
				catch
				{
				}
				DonePick(you, token);
				c.Toast?.Invoke($"+{gold} gold: there's nothing on that list you don't have already");
				OfferPicks(c, after);
				return;
			}
			c.Say(good ? "And one more thing. Something rare: choose what you'd be." : "Choose what you'd be. It stays with you.");
			List<Choice> list2 = new List<Choice>();
			foreach (string item in list)
			{
				string id = item;
				list2.Add(new Choice(Teachers.Name(id) + ": you'll be " + Teachers.Word(id), delegate
				{
					bool flag = false;
					try
					{
						ActorTrait actorTrait = AssetManager.traits.get(id);
						flag = actorTrait != null && you.addTrait(actorTrait, pRemoveOpposites: true);
					}
					catch (Exception ex)
					{
						FirstPerson.Log.Error("quests: trait pick: " + ex.Message);
					}
					if (flag)
					{
						TraitSwitch.Earned(you, id);
						DonePick(you, token);
						c.Toast?.Invoke("You are now " + Teachers.Word(id) + ". (Tab: Magic to switch it off)");
						FirstPerson.Log.Info("quests: " + WorldBoxMod.Describe(you) + " picked the trait " + id);
					}
					else
					{
						c.Say("Hm. That didn't take.");
					}
					OfferPicks(c, after);
				}, ChoiceTone.Quest));
			}
			c.Offer(list2);
		}

		private static void OfferWeapon(Conversation c, string token, string metal, string kind, Action after)
		{
			Actor you = c.You;
			string id = kind + "_" + metal;
			if (AssetManager.items.get(id) == null)
			{
				id = "sword_" + metal;
			}
			if (AssetManager.items.get(id) == null)
			{
				id = "sword_iron";
			}
			string what = metal + " " + kind;
			c.Say("The smith made this for whoever held the town: a " + what + ". Choose its power.");
			List<Choice> list = new List<Choice>();
			string[] common = WeaponTraits.Common;
			foreach (string text in common)
			{
				string mod = text;
				list.Add(new Choice(WeaponTraits.Name(mod) + ": " + WeaponTraits.Does(mod), delegate
				{
					Gear.Piece p = new Gear.Piece
					{
						Id = id,
						Mods = new string[1] { mod },
						By = ((c.With != null) ? Dialogue.NameOf(c.With) : null)
					};
					if (!Gear.Add(you, p))
					{
						Gear.AddOver(you, p);
					}
					DonePick(you, token);
					c.Toast?.Invoke("Yours: a " + what + " of " + WeaponTraits.Name(mod).ToLowerInvariant() + " (Tab)");
					FirstPerson.Log.Info("quests: " + WorldBoxMod.Describe(you) + " took a " + id + " with " + mod);
					OfferPicks(c, after);
				}, ChoiceTone.Quest));
			}
			c.Offer(list);
		}

		private static string RaidBusy()
		{
			if (Raiders.Current != null && !Raiders.Current.Over)
			{
				return Raiders.Current.What + " are already on their way to " + Raiders.Current.TownName + ". One fight at a time.";
			}
			foreach (Quest item in Log)
			{
				if (item.Kind == QuestKind.Raid && item.Stage < 2)
				{
					return "You've already promised to hold a town. One fight at a time.";
				}
			}
			return null;
		}

		private static Quest MakeRaid(Actor giver, Actor you)
		{
			City city = giver?.city;
			if (city == null || you == null || RaidBusy() != null || TownLevelTask(city) != null)
			{
				return null;
			}
			WorldTile worldTile = null;
			try
			{
				worldTile = city.getTile();
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return null;
			}
			int strength = RaidStrength(you, city);
			Raiders.Band band;
			string what;
			List<Raiders.Wave> list = Raiders.Plan(strength, out band, out what);
			int num = 0;
			foreach (Raiders.Wave item in list)
			{
				num += item.Units.Length + ((item.Chief != null) ? 1 : 0);
			}
			Quest quest = New(QuestKind.Raid, giver, 120 + 12 * num, 15, 60, 30);
			quest.Target = city.data.id;
			quest.Place = city.name;
			quest.X = worldTile.x;
			quest.Y = worldTile.y;
			quest.Thing = what;
			quest.Need = num;
			quest.Extra = strength + "!0!0";
			return ToTown(quest, city);
		}

		private static int RaidStrength(Actor you, City city)
		{
			int num = 0;
			try
			{
				num = (int)TownTier.Of(city);
			}
			catch
			{
			}
			return LevelOf(you).Done3 + num;
		}

		private static int RaidPart(Quest q, int i)
		{
			if (string.IsNullOrEmpty(q.Extra))
			{
				return 0;
			}
			string[] array = q.Extra.Split('!');
			if (array.Length <= i || !int.TryParse(array[i], out var result))
			{
				return 0;
			}
			return result;
		}

		private static Raiders.Raid RaidOf(Quest q)
		{
			Raiders.Raid current = Raiders.Current;
			if (current == null || current.TownId != q.Target)
			{
				return null;
			}
			return current;
		}

		private static bool StartRaid(Quest q, Action<string> toast)
		{
			City city = CityById(q.Target);
			if (city == null)
			{
				return false;
			}
			Raiders.Band band;
			string what;
			List<Raiders.Wave> waves = Raiders.Plan(RaidPart(q, 0), out band, out what);
			if (Raiders.Start(city, waves, band, what, (RaidWarningForTest > 0f) ? RaidWarningForTest : 60f, (RaidBetweenForTest > 0f) ? RaidBetweenForTest : 60f) == null)
			{
				return false;
			}
			_raidFor = q;
			_raidAway = 0f;
			toast?.Invoke(what + " are coming for " + city.name + ". They'll be here within the minute: get ready.");
			return true;
		}

		private static void CheckRaid(Quest q, Actor you, Action<string> toast)
		{
			if (q.Stage != 0)
			{
				return;
			}
			if (CityById(q.Target) == null)
			{
				Fail(q, you, q.Place + " is gone", toast);
				return;
			}
			Raiders.Raid raid = RaidOf(q);
			if (raid == null)
			{
				if ((you.current_position - new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f)).sqrMagnitude < 900f && (Raiders.Current == null || Raiders.Current.Over))
				{
					StartRaid(q, toast);
				}
				return;
			}
			switch (raid.State)
			{
			case Raiders.Phase.Held:
				q.Extra = RaidPart(q, 0) + "!" + raid.Sent + "!" + raid.Dead;
				q.Stage = 1;
				Raiders.Release(raid);
				break;
			case Raiders.Phase.Lost:
				Raiders.Release(raid);
				Fail(q, you, raid.Why ?? "the raid was lost", toast);
				break;
			case Raiders.Phase.Fleeing:
				_raidAway = 0f;
				break;
			case Raiders.Phase.Fighting:
				if ((you.current_position - new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f)).magnitude > 30f)
				{
					if (_raidAway <= 0f)
					{
						toast?.Invoke("Get back to " + q.Place + "! The raid isn't over.");
					}
					_raidAway += 0.5f;
					if (_raidAway >= 15f)
					{
						Fail(q, you, "you left " + q.Place + " to the raiders", toast);
					}
				}
				else
				{
					_raidAway = 0f;
				}
				break;
			}
		}

		private static void RaidHorn(Raiders.Raid r)
		{
			try
			{
				WorldBoxMod.Instance?.Audio.PlayHorn();
			}
			catch
			{
			}
			Action<string> obj2 = ((WorldBoxMod.Instance != null) ? new Action<string>(WorldBoxMod.Instance.Toast) : null);
			bool flag = r.WavesSent >= r.Waves.Count;
			obj2?.Invoke(flag ? ("The horn! The last wave, and " + (r.ChiefName ?? "their chief") + " with it.") : $"The horn! Wave {r.WavesSent} of {r.Waves.Count}: {r.What.ToLowerInvariant()} at {r.TownName}.");
		}

		private static void HookRaids()
		{
			if (!_raidHooked)
			{
				_raidHooked = true;
				Raiders.Horn = (Action<Raiders.Raid>)Delegate.Combine(Raiders.Horn, new Action<Raiders.Raid>(RaidHorn));
			}
		}

		private static bool RaidHeading(Quest q, out Vector2 at)
		{
			at = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
			Raiders.Raid raid = RaidOf(q);
			Actor actor = WorldBoxMod.Instance?.Host;
			if (raid == null || actor == null || raid.State != Raiders.Phase.Fighting)
			{
				return true;
			}
			float num = 1600f;
			foreach (Actor unit in raid.Units)
			{
				try
				{
					float sqrMagnitude = (unit.current_position - actor.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						at = unit.current_position;
					}
				}
				catch
				{
				}
			}
			return true;
		}

		private static void RaidStars(Quest q, List<KeyValuePair<Actor, bool>> into)
		{
			Raiders.Raid raid = RaidOf(q);
			Actor actor = WorldBoxMod.Instance?.Host;
			if (raid == null || actor == null || raid.State == Raiders.Phase.Warning)
			{
				return;
			}
			foreach (Actor unit in raid.Units)
			{
				try
				{
					if ((unit.current_position - actor.current_position).sqrMagnitude < 3600f)
					{
						Star(into, unit, gold: true);
					}
				}
				catch
				{
				}
			}
		}

		private static string RaidTitle(Quest q)
		{
			return "Hold " + q.Place + " against the raid";
		}

		private static string RaidStatus(Quest q)
		{
			Raiders.Raid raid = RaidOf(q);
			if (raid != null && !raid.Over)
			{
				return raid.Status(Now);
			}
			return "Be in " + q.Place + ": " + q.Thing.ToLowerInvariant() + " come when you're there";
		}

		private static string[] RaidOneBreath(Quest q, Actor npc, string pay)
		{
			string text = q.Thing ?? "Raiders";
			return new string[2]
			{
				$"Scouts saw {text.ToLowerInvariant()} on the road to {q.Place}. About {q.Need} of them, in three waves, and a chief with the last.",
				"Stand with us: when the horn sounds, stay in " + q.Place + " until every last one is dead or running. For that, " + pay + "."
			};
		}

		private static void RaidQuestions(Quest q, Actor npc, Actor you, Action<string, Func<string>> add)
		{
			add("When do they come?", () => "Within the minute of you saying yes. Use it: gather whoever will fight.");
			add("Who fights with me?", () => "The town's soldiers, and anyone you bring. Every raider down counts, whoever drops them.");
			add("What if I leave?", () => "Then we lose the town, and I'll know you ran. Once the horn sounds, don't go far from " + q.Place + ".");
			add("When is it over?", () => "Once half of them are dead after the last wave, the rest break and run. When none are left, it's over.");
		}

		private static string RaidProof(Quest q)
		{
			return "When the last of them is dead or gone, I'll know. Come to me then.";
		}

		private static string RaidThanks(Quest q)
		{
			return Dialogue.Pick(q.Place + " still stands. Because of you. *grips your arm* Take this, and the town's thanks.", "They broke! They ran! You've earned every coin of this.");
		}

		private static string RaidWaiting(Quest q)
		{
			Raiders.Raid raid = RaidOf(q);
			if (raid == null)
			{
				return "Get to " + q.Place + ". They'll come when you're there.";
			}
			if (raid.State != Raiders.Phase.Warning)
			{
				return "Fight! It isn't over yet!";
			}
			return "They're nearly here. Get ready!";
		}

		public static void Starred(List<KeyValuePair<Actor, bool>> into)
		{
			into.Clear();
			foreach (Quest item in Log)
			{
				if (item.Stage >= 2)
				{
					continue;
				}
				Actor a = (item.FromBoard ? null : Chronicle.Find(item.Giver));
				if (item.Stage == 1)
				{
					Star(into, a, gold: true);
					continue;
				}
				switch (item.Kind)
				{
				case QuestKind.Hunt:
				case QuestKind.Errand:
				case QuestKind.Matchmaker:
				case QuestKind.Investigation:
				case QuestKind.Bounty:
				case QuestKind.CourtValor:
				case QuestKind.CourtBlessing:
				case QuestKind.CourtRival:
					Star(into, Chronicle.Find(item.Target), gold: true);
					Star(into, a, gold: false);
					break;
				case QuestKind.Escort:
				case QuestKind.CourtWalk:
				case QuestKind.CourtStars:
				case QuestKind.CourtProposal:
					Star(into, a, gold: true);
					break;
				case QuestKind.Meat:
					MeatStars(item, into);
					Star(into, a, gold: false);
					break;
				case QuestKind.Undead:
					UndeadStars(item, into);
					break;
				case QuestKind.Raid:
					RaidStars(item, into);
					break;
				default:
					Star(into, a, gold: false);
					break;
				}
			}
		}

		private static void Star(List<KeyValuePair<Actor, bool>> into, Actor a, bool gold)
		{
			if (a == null)
			{
				return;
			}
			for (int i = 0; i < into.Count; i++)
			{
				if (into[i].Key == a)
				{
					if (gold && !into[i].Value)
					{
						into[i] = new KeyValuePair<Actor, bool>(a, value: true);
					}
					return;
				}
			}
			into.Add(new KeyValuePair<Actor, bool>(a, gold));
		}

		public static bool IsTrial(QuestKind k)
		{
			if ((int)k >= 15)
			{
				return (int)k <= 21;
			}
			return false;
		}

		internal static QuestKind TrialOf(Teachers.School s)
		{
			switch (s)
			{
			case Teachers.School.Soldier:
				return QuestKind.TrialSpar;
			case Teachers.School.Duelist:
				return QuestKind.TrialParry;
			case Teachers.School.Hunter:
				return QuestKind.TrialRange;
			case Teachers.School.Miner:
				return QuestKind.TrialOre;
			case Teachers.School.Mage:
				return QuestKind.TrialFire;
			case Teachers.School.Priest:
				return QuestKind.TrialVigil;
			default:
				return QuestKind.TrialStory;
			}
		}

		private static int TrialNeed(QuestKind k)
		{
			switch (k)
			{
			case QuestKind.TrialSpar:
				return 3;
			case QuestKind.TrialParry:
				return 3;
			case QuestKind.TrialRange:
				return 5;
			case QuestKind.TrialOre:
				return 20;
			case QuestKind.TrialFire:
				return 3;
			case QuestKind.TrialVigil:
				return 1;
			default:
				return 3;
			}
		}

		private static int TheirHits(Quest q)
		{
			if (q.Kind != QuestKind.TrialSpar)
			{
				return 6;
			}
			return 3;
		}

		private static int TrialMonths(Quest q)
		{
			if (q.Kind != QuestKind.TrialVigil && q.Kind != QuestKind.TrialStory)
			{
				if (q.Kind != QuestKind.TrialRange && q.Kind != QuestKind.TrialOre)
				{
					return 24;
				}
				return 60;
			}
			return 0;
		}

		internal static Quest MakeTrial(Actor teacher, Actor you, string trait, Teachers.School school)
		{
			QuestKind questKind = TrialOf(school);
			Quest quest = new Quest
			{
				Kind = questKind,
				Giver = teacher.data.id,
				GiverName = Dialogue.NameOf(teacher),
				Thing = trait,
				Need = TrialNeed(questKind),
				Money = Teachers.Fee(teacher, you, trait),
				Renown = 5,
				Xp = 40,
				Opinion = 12,
				Target = teacher.data.id,
				TargetName = Dialogue.NameOf(teacher)
			};
			if (questKind == QuestKind.TrialVigil)
			{
				Building building = TempleOf(teacher.city);
				WorldTile worldTile = null;
				try
				{
					worldTile = ((building != null) ? building.current_tile : teacher.city?.getTile());
				}
				catch
				{
				}
				if (worldTile == null)
				{
					try
					{
						worldTile = World.world.GetTile((int)teacher.current_position.x, (int)teacher.current_position.y);
					}
					catch
					{
					}
				}
				if (worldTile != null)
				{
					quest.X = worldTile.x;
					quest.Y = worldTile.y;
				}
				quest.Place = ((building != null) ? "the temple" : ((teacher.city != null) ? "the town's hall" : "this spot"));
			}
			return quest;
		}

		private static Building TempleOf(City c)
		{
			try
			{
				if (c == null)
				{
					return null;
				}
				foreach (Building building in c.buildings)
				{
					if (building != null && building.isAlive() && building.asset != null && building.asset.id.StartsWith("temple", StringComparison.Ordinal))
					{
						return building;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static bool PayTrialFee(Quest q, Actor you, Action<string> toast)
		{
			int num = Economy.Money(you);
			if (num < q.Money)
			{
				toast?.Invoke($"The lesson costs {q.Money} gold; you have {num}.");
				return false;
			}
			Actor actor = Chronicle.Find(q.Giver);
			try
			{
				you.spendMoney(q.Money);
				actor?.addMoney(q.Money);
			}
			catch
			{
			}
			toast?.Invoke($"You pay {q.GiverName} {q.Money} gold for the lesson.");
			return true;
		}

		private static string TrialTitle(Quest q)
		{
			return "Trial: " + Teachers.Name(q.Thing) + " (" + q.GiverName + ")";
		}

		private static string TrialStatus(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.TrialSpar:
				if (!Bout(q))
				{
					return "Go to " + q.GiverName + " to spar";
				}
				return $"Sparring: {q.Have} of {q.Need} hits landed, {q.Taken} taken";
			case QuestKind.TrialParry:
				if (!Bout(q))
				{
					return "Go to " + q.GiverName + " to duel";
				}
				return $"Duel: {q.Have} of {q.Need} blows turned aside, {q.Taken} taken";
			case QuestKind.TrialRange:
				return $"{q.Have} of {q.Need} hits from eight paces or more";
			case QuestKind.TrialOre:
				return $"{Mathf.Min(q.Have, q.Need)} of {q.Need} stone or ore dug";
			case QuestKind.TrialFire:
				if (q.Taken <= 0)
				{
					return "Go to " + q.GiverName + "; they'll light the fire";
				}
				return $"Stand in the fire ({q.Have} of {q.Need} s)";
			case QuestKind.TrialVigil:
				if (q.Taken <= 0)
				{
					return "Be at " + q.Place + " at dusk";
				}
				return "Keep the vigil by " + q.Place + " until dawn";
			default:
				return "Hear " + q.GiverName + "'s story";
			}
		}

		private static string[] TrialPitch(Quest q, Actor npc)
		{
			switch (q.Kind)
			{
			case QuestKind.TrialSpar:
				return new string[2]
				{
					"*sizes you up* That isn't taught with words. You'll spar with me.",
					$"Land {q.Need} clean hits on me before I land {TheirHits(q)} on you. Nobody dies."
				};
			case QuestKind.TrialParry:
				return new string[2]
				{
					"*smiles* A guard is learned with a blade in front of you.",
					$"I'll come at you. Turn {q.Need} of my blows aside before I land {TheirHits(q)} on you."
				};
			case QuestKind.TrialRange:
				return new string[2]
				{
					"So you want my eye. Show me yours first.",
					$"{q.Need} hits, each from eight paces or more. Beast or foe, I don't care which."
				};
			case QuestKind.TrialOre:
				return new string[2]
				{
					"*taps the rock* The pick teaches better than I do.",
					$"Dig {q.Need} stone or ore with your own hands. You'll know the rock by the end of it."
				};
			case QuestKind.TrialFire:
				return new string[2] { "*looks into the distance* Fire is only what you believe it is.", "I'll light one. You'll walk into it and stand there while I count, then come out." };
			case QuestKind.TrialVigil:
				return new string[2]
				{
					"*speaks softly* Blessings aren't bought; they're waited for.",
					"Keep a vigil by " + q.Place + ", from dusk to dawn. Walk away, and it's broken."
				};
			default:
				return new string[2] { "*settles back* Wisdom? Then listen first.", "I'll tell you of my life. Then I'll ask you three things about it. Answer true, and you'll have learned something." };
			}
		}

		private static string TrialPlainly(Quest q)
		{
			return string.Format("To be clear: {0}{1}. The lesson costs {2} gold, paid now. Fail, and it's gone; you may try again after the next dawn. Pass, and you'll be {3}.", TrialGoal(q), (TrialMonths(q) > 0) ? (", within " + Span(TrialMonths(q))) : "", q.Money, Teachers.Word(q.Thing));
		}

		private static string TrialDoubt(Quest q, Actor you)
		{
			int num = Economy.Money(you);
			if (num < q.Money)
			{
				return $"*glances at your purse* You'll need {q.Money} gold for it, and you've {num}.";
			}
			if ((q.Kind == QuestKind.TrialSpar || q.Kind == QuestKind.TrialParry) && you.getHealthRatio() < 0.6f)
			{
				return "*frowns* You're hurt. Rest first, or I'll be sparring with a corpse.";
			}
			if (q.Kind == QuestKind.TrialRange && !Ranged(you))
			{
				return "You'll want a bow for this. Stones thrown by hand won't do it.";
			}
			if (q.Kind == QuestKind.TrialOre && !HasToolFor(you, "stone"))
			{
				return "Bring a pickaxe, or an axe at least. Bare hands won't break rock.";
			}
			return null;
		}

		private static bool Ranged(Actor you)
		{
			try
			{
				return you.hasRangeAttack();
			}
			catch
			{
				return false;
			}
		}

		private static string TrialGoal(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.TrialSpar:
				return $"spar with me and land {q.Need} hits before I land {TheirHits(q)}";
			case QuestKind.TrialParry:
				return $"turn {q.Need} of my blows aside before I land {TheirHits(q)} on you";
			case QuestKind.TrialRange:
				return $"make {q.Need} hits from eight paces or more";
			case QuestKind.TrialOre:
				return $"dig {q.Need} stone or ore";
			case QuestKind.TrialFire:
				return "walk into the fire I light and stand in it while I count three";
			case QuestKind.TrialVigil:
				return "keep a night's vigil by " + q.Place;
			default:
				return "hear my story and answer my questions";
			}
		}

		private static string TrialThen(Quest q)
		{
			if (q.Kind != QuestKind.TrialStory)
			{
				return "you come back to me";
			}
			return "I ask you three things";
		}

		private static string TrialPayoff(Quest q)
		{
			return "you'll be " + Teachers.Word(q.Thing);
		}

		private static string TrialProof(Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.TrialSpar:
			case QuestKind.TrialParry:
			case QuestKind.TrialFire:
				return "I'll be right there watching.";
			case QuestKind.TrialVigil:
				return "The faithful see who stays. Come to me after dawn.";
			case QuestKind.TrialStory:
				return "Your answers will tell me.";
			default:
				return "I'll hear of it. Then come back to me.";
			}
		}

		private static void TrialQuestions(Quest q, Actor npc, Actor you, Action<string, Func<string>> add)
		{
			switch (q.Kind)
			{
			case QuestKind.TrialSpar:
				add("Will it hurt?", () => "*laughs* Yes. But nobody dies in my spars; I stop before that. If you're badly hurt, it's over and you've lost.");
				add("Can I use any weapon?", () => "Whatever you hold. A blade, an axe, your fists. Don't bring friends.");
				break;
			case QuestKind.TrialParry:
				add("How do I turn a blow aside?", () => "Strike just as mine comes, and your blade meets it. Or step out of its way. Watch my shoulders; they move first.");
				break;
			case QuestKind.TrialRange:
				add("Does it have to be a bow?", () => "Anything that flies far enough. A bow is best.");
				add("Eight paces?", () => "About the length of a house and a half. Closer is just stabbing with extra steps.");
				break;
			case QuestKind.TrialOre:
				add("Where do I find ore?", () => "In the rocks and hills, and in a mine if this town has one. Break it with a pickaxe or an axe.");
				break;
			case QuestKind.TrialFire:
				add("Won't I burn?", () => "You'll feel it. Stand there while I count three, then I'll put it out. Don't run off early.");
				break;
			case QuestKind.TrialVigil:
				add("When does it start?", () => "At dusk, when the sun goes. Be by " + q.Place + " before it's dark.");
				add("Can I sleep?", () => "No. Stand, or kneel, but stay close. Until the sun comes back.");
				break;
			default:
				add("What will you ask?", () => "About me. Listen well.");
				break;
			}
		}

		public static void EndTrialState()
		{
			if (_bout != 0L)
			{
				Actor teacher = Chronicle.Find(_bout);
				_bout = 0L;
				Calm(teacher, _boutYou);
			}
			_boutYou = null;
			PutOutFire();
			_vigilFor = 0L;
			_vigilAway = 0f;
			Rivals.End();
		}

		private static bool Bout(Quest q)
		{
			if (_bout != 0L)
			{
				return _bout == q.Giver;
			}
			return false;
		}

		public static bool Sparring(Actor a)
		{
			try
			{
				if (a == null)
				{
					return false;
				}
				if ((_bout != 0L && a.data.id == _bout) || Rivals.Brawling(a))
				{
					return true;
				}
				Actor actor = WorldBoxMod.Instance?.Host;
				if (actor == null || (a.current_position - actor.current_position).sqrMagnitude > 36f)
				{
					return false;
				}
				foreach (Quest item in Log)
				{
					if (item.Stage == 0 && (item.Kind == QuestKind.TrialSpar || item.Kind == QuestKind.TrialParry) && item.Giver == a.data.id)
					{
						return true;
					}
				}
				return false;
			}
			catch
			{
				return false;
			}
		}

		public static void SparBlow(ref AttackData data, BaseSimObject target)
		{
			if (_bout == 0L || target == null || !target.isActor() || data.initiator == null || data.damage <= 0)
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
				bool flag = data.initiator.isActor() && data.initiator.a.data.id == _bout;
				bool flag2 = (data.initiator == host && target.a.data.id == _bout) || (target == host && flag);
				if (flag2 || flag)
				{
					int num = (flag2 ? Mathf.Max(0, (target.a.getHealth() - 1) / Mathf.Max(1, (!data.critical) ? 1 : data.critical_damage_multiplier)) : 0);
					if (data.damage > num)
					{
						object obj = data;
						SparDamage.SetValue(obj, num);
						data = (AttackData)obj;
					}
				}
			}
			catch
			{
			}
		}

		public static void TrialBlow(BaseSimObject by, BaseSimObject target, bool hit, bool projectile)
		{
			if (!hit || by == null || target == null || Log.Count == 0)
			{
				return;
			}
			Actor host = Blocks.Host;
			if (host == null)
			{
				return;
			}
			foreach (Quest item in Log)
			{
				if (item.Stage != 0)
				{
					continue;
				}
				if ((item.Kind == QuestKind.TrialSpar || item.Kind == QuestKind.TrialParry) && Bout(item))
				{
					if (item.Kind == QuestKind.TrialSpar && by == host && target.isActor() && target.a.data.id == item.Giver)
					{
						item.Have++;
						Save(item);
					}
					else if (target == host && by.isActor() && by.a.data.id == item.Giver)
					{
						item.Taken++;
						Save(item);
					}
				}
				else if (item.Kind == QuestKind.TrialRange && projectile && by == host && target.isActor() && target != host && Law.Lawful(target.a, host) && Time.frameCount - _shotFrame >= 20)
				{
					float magnitude = (target.current_position - host.current_position).magnitude;
					if (!(magnitude < 8f))
					{
						_shotFrame = Time.frameCount;
						item.Have++;
						Save(item);
						WorldBoxMod.Instance?.Toast($"A hit from {Mathf.RoundToInt(magnitude)} paces: {item.Have} of {item.Need}.");
					}
				}
			}
		}

		public static void OnGathered(string resource, int amount)
		{
			if (amount <= 0 || Log.Count == 0 || !IsOre(resource))
			{
				return;
			}
			foreach (Quest item in Log)
			{
				if (item.Kind == QuestKind.TrialOre && item.Stage == 0)
				{
					item.Have += amount;
					Save(item);
				}
			}
		}

		private static bool IsOre(string r)
		{
			switch (r)
			{
			default:
				return r == "gems";
			case "stone":
			case "common_metals":
			case "gold":
			case "silver":
			case "mythril":
			case "adamantine":
				return true;
			}
		}

		private static void TickTrials(Actor you, float dt, Action<string> toast)
		{
			Rivals.Update(you, dt, toast);
			bool flag = false;
			for (int num = Log.Count - 1; num >= 0; num--)
			{
				if (num < Log.Count)
				{
					Quest quest = Log[num];
					if (IsTrial(quest.Kind) && quest.Stage == 0)
					{
						Actor actor = Chronicle.Find(quest.Giver);
						if (actor != null && actor.isAlive())
						{
							float magnitude = (actor.current_position - you.current_position).magnitude;
							try
							{
								switch (quest.Kind)
								{
								case QuestKind.TrialSpar:
								case QuestKind.TrialParry:
									if (Duel(quest, actor, you, magnitude, dt, toast))
									{
										flag = true;
									}
									break;
								case QuestKind.TrialFire:
									Fire(quest, actor, you, magnitude, dt, toast);
									break;
								case QuestKind.TrialVigil:
									Vigil(quest, you, dt, toast);
									break;
								case QuestKind.TrialRange:
								case QuestKind.TrialOre:
									break;
								}
							}
							catch (Exception ex)
							{
								FirstPerson.Log.Error("quests: trial " + quest.Kind.ToString() + ": " + ex.Message);
							}
						}
					}
				}
			}
			if (!flag && _bout != 0L)
			{
				Actor teacher = Chronicle.Find(_bout);
				_bout = 0L;
				Calm(teacher, _boutYou ?? you);
			}
		}

		private static bool Duel(Quest q, Actor teacher, Actor you, float dist, float dt, Action<string> toast)
		{
			if (!Bout(q))
			{
				_boutRest -= dt;
				if (dist > 3.5f || _boutRest > 0f || _bout != 0L)
				{
					return false;
				}
				if (you.getHealthRatio() < 0.6f)
				{
					_boutRest = 12f;
					toast?.Invoke(q.GiverName + ": \"You're hurt. Rest, then come back.\"");
					return false;
				}
				_bout = q.Giver;
				_boutYou = you;
				_blocksSeen = Blocks.Newest;
				toast?.Invoke(q.GiverName + " raises their guard. Begin!");
				FirstPerson.Log.Info("quests: trial bout with " + q.GiverName + " begins");
			}
			try
			{
				if (!teacher.has_attack_target || teacher.attack_target != you)
				{
					teacher.addAggro(you);
					teacher.startFightingWith(you);
				}
			}
			catch
			{
			}
			_calmTick -= dt;
			if (_calmTick <= 0f)
			{
				_calmTick = 0.4f;
				CalmOthers(you, teacher);
			}
			if (q.Kind == QuestKind.TrialParry)
			{
				foreach (BlockEvent @event in Blocks.Events)
				{
					if (@event.Serial > _blocksSeen && @event.OnYou && @event.Attacker != null && @event.Attacker.isActor() && @event.Attacker.a == teacher)
					{
						q.Have += @event.Count;
						Save(q);
					}
				}
				_blocksSeen = Blocks.Newest;
			}
			if (q.Have >= q.Need)
			{
				EndBout(q, teacher, you, won: true, toast);
				return false;
			}
			if (q.Taken >= TheirHits(q) || you.getHealthRatio() < 0.3f)
			{
				EndBout(q, teacher, you, won: false, toast);
				return false;
			}
			if (dist > 14f)
			{
				EndBout(q, teacher, you, won: false, toast, "you ran from the fight");
				return false;
			}
			return true;
		}

		private static void EndBout(Quest q, Actor teacher, Actor you, bool won, Action<string> toast, string why = null)
		{
			_bout = 0L;
			_boutRest = 4f;
			Calm(teacher, you);
			if (won)
			{
				q.Stage = 1;
				Save(q);
				toast?.Invoke(q.GiverName + " lowers their guard: \"Well fought.\" Talk to them.");
			}
			else
			{
				Fail(q, you, why ?? "they had the better of you", toast);
			}
		}

		internal static void CalmOthers(Actor you, Actor teacher)
		{
			try
			{
				Kingdom kingdom = teacher?.kingdom;
				if (kingdom == null || Law.BountyOn(kingdom, you) > 0)
				{
					return;
				}
				long id = you.data.id;
				Vector2 current_position = you.current_position;
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					Actor actor = simpleList[i];
					if (actor != null && actor != you && actor != teacher && actor.isAlive() && actor.kingdom == kingdom && !((actor.current_position - current_position).sqrMagnitude > 625f))
					{
						bool num = actor._aggression_targets != null && actor._aggression_targets.Remove(id);
						bool flag = actor.has_attack_target && actor.attack_target == you;
						if (flag)
						{
							actor.clearAttackTarget();
						}
						if ((num || flag) && (actor._aggression_targets == null || actor._aggression_targets.Count == 0))
						{
							actor.finishStatusEffect("angry");
						}
					}
				}
			}
			catch
			{
			}
		}

		internal static void CalmFight(Actor a, Actor you)
		{
			Calm(a, you);
		}

		private static void Calm(Actor teacher, Actor you)
		{
			if (you != null)
			{
				CalmOthers(you, teacher);
			}
			try
			{
				if (you != null && teacher != null)
				{
					you._aggression_targets?.Remove(teacher.data.id);
					if (you._aggression_targets == null || you._aggression_targets.Count == 0)
					{
						you.finishStatusEffect("angry");
					}
				}
			}
			catch
			{
			}
			try
			{
				if (teacher != null && teacher.isAlive())
				{
					if (you != null)
					{
						teacher._aggression_targets?.Remove(you.data.id);
					}
					teacher.finishStatusEffect("angry");
					teacher.clearAttackTarget();
					teacher.cancelAllBeh();
					teacher.makeWait(2f);
				}
				if (you != null && you.has_attack_target && you.attack_target == teacher)
				{
					you.clearAttackTarget();
				}
			}
			catch
			{
			}
		}

		private static void Fire(Quest q, Actor teacher, Actor you, float dist, float dt, Action<string> toast)
		{
			if (q.Taken == 0 || _fire.x < 0)
			{
				if (dist > 5f)
				{
					return;
				}
				if (q.Taken == 0)
				{
					_fireKept = -1f;
				}
				if ((!(_fireKept >= 0f) || !(Time.unscaledTime - _relitAt < 8f)) && LightFire(teacher, you))
				{
					if (_fireKept >= 0f)
					{
						_fireLit = _fireKept;
						_fireKept = -1f;
					}
					q.Taken = 1;
					Save(q);
					toast?.Invoke(q.GiverName + " sets the ground alight. Walk into it.");
				}
				return;
			}
			KeepFireInItsRing();
			bool flag = false;
			try
			{
				int num = Mathf.FloorToInt(you.current_position.x);
				int num2 = Mathf.FloorToInt(you.current_position.y);
				WorldTile tile = World.world.GetTile(num, num2);
				flag = Mathf.Abs(num - _fire.x) <= 1 && Mathf.Abs(num2 - _fire.y) <= 1 && ((tile != null && tile.isOnFire()) || ((BaseSimObject)you).hasStatus("burning"));
			}
			catch
			{
			}
			bool flag2 = false;
			try
			{
				flag2 = Config.paused || World.world.isPaused();
			}
			catch
			{
			}
			if (flag && !flag2)
			{
				_fireLit += dt;
				int num3 = Mathf.Min(q.Need, Mathf.FloorToInt(_fireLit));
				if (num3 != q.Have)
				{
					q.Have = num3;
					Save(q);
					toast?.Invoke((num3 < q.Need) ? $"{q.GiverName} counts: \"{num3}...\"" : $"{q.GiverName}: \"{num3}. Enough.\"");
				}
			}
			else if (Time.unscaledTime - _relitAt > 5f && !AnyFire())
			{
				_relitAt = Time.unscaledTime;
				LightFire(teacher, you, _fire);
			}
			float num4 = 1f;
			try
			{
				num4 = (float)you.getHealth() / (float)Mathf.Max(1, you.getMaxHealth());
			}
			catch
			{
			}
			if (num4 < 0.4f && q.Have < q.Need && (flag || ((BaseSimObject)you).hasStatus("burning")))
			{
				_fireKept = _fireLit;
				PutOutFire();
				try
				{
					you.finishStatusEffect("burning");
				}
				catch
				{
				}
				_relitAt = Time.unscaledTime;
				toast?.Invoke(q.GiverName + " beats out the flames on you. \"Rest. Then again: your count stands.\"");
			}
			else if (q.Have >= q.Need)
			{
				PutOutFire();
				try
				{
					you.finishStatusEffect("burning");
				}
				catch
				{
				}
				q.Stage = 1;
				Save(q);
				toast?.Invoke("The fire dies at " + q.GiverName + "'s word. You came through. Talk to them.");
			}
		}

		private static bool LightFire(Actor teacher, Actor you, Vector2Int? at = null)
		{
			try
			{
				Vector2Int fire = at ?? FireSpot(teacher, you);
				if (fire.x < 0)
				{
					if (Time.unscaledTime - _noFireLogged > 10f)
					{
						_noFireLogged = Time.unscaledTime;
						FirstPerson.Log.Info($"trial fire: no open ground near {Dialogue.NameOf(teacher)} at {teacher.current_position.x:F0},{teacher.current_position.y:F0}");
					}
					return false;
				}
				for (int i = -1; i <= 1; i++)
				{
					for (int j = -1; j <= 1; j++)
					{
						WorldTile tile = World.world.GetTile(fire.x + i, fire.y + j);
						if (tile != null && !tile.is_liquid && tile.building == null && !tile.Type.explodable)
						{
							tile.startFire(pForce: true);
						}
					}
				}
				_fire = fire;
				_fireLit = 0f;
				return true;
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("trial fire: " + ex.Message);
				return false;
			}
		}

		private static Vector2Int FireSpot(Actor teacher, Actor you)
		{
			Vector2 current_position = teacher.current_position;
			Vector2 vector = you.current_position - current_position;
			float num = ((vector.sqrMagnitude > 0.01f) ? Mathf.Atan2(vector.y, vector.x) : 0f);
			for (int i = 0; i < 6; i++)
			{
				for (int j = 0; j < 12; j++)
				{
					float f = num + (float)(((j % 2 == 0) ? 1 : (-1)) * (j / 2)) * 0.52f;
					float num2 = 2.5f + (float)i * 1.5f;
					Vector2Int vector2Int = new Vector2Int(Mathf.FloorToInt(current_position.x + Mathf.Cos(f) * num2), Mathf.FloorToInt(current_position.y + Mathf.Sin(f) * num2));
					if (FireFits(vector2Int))
					{
						return vector2Int;
					}
				}
			}
			return new Vector2Int(-1, -1);
		}

		private static bool FireFits(Vector2Int c)
		{
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					WorldTile tile = World.world.GetTile(c.x + i, c.y + j);
					if (tile == null || tile.is_liquid)
					{
						return false;
					}
					bool flag = Mathf.Abs(i) <= 1 && Mathf.Abs(j) <= 1;
					try
					{
						if (flag && (tile.building != null || tile.Type.explodable))
						{
							return false;
						}
						if (!flag && tile.building != null && !Wild(tile.building))
						{
							return false;
						}
					}
					catch
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool Wild(Building b)
		{
			try
			{
				BuildingType building_type = b.asset.building_type;
				return building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Plant || building_type == BuildingType.Building_Mineral;
			}
			catch
			{
				return false;
			}
		}

		private static void KeepFireInItsRing()
		{
			for (int i = -4; i <= 4; i++)
			{
				for (int j = -4; j <= 4; j++)
				{
					if (Mathf.Abs(i) <= 1 && Mathf.Abs(j) <= 1)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile(_fire.x + i, _fire.y + j);
					try
					{
						if (tile != null && tile.isOnFire())
						{
							tile.stopFire();
						}
					}
					catch
					{
					}
				}
			}
		}

		private static bool AnyFire()
		{
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					WorldTile tile = World.world.GetTile(_fire.x + i, _fire.y + j);
					try
					{
						if (tile != null && tile.isOnFire())
						{
							return true;
						}
					}
					catch
					{
					}
				}
			}
			return false;
		}

		private static void PutOutFire()
		{
			if (_fire.x < 0)
			{
				return;
			}
			for (int i = -4; i <= 4; i++)
			{
				for (int j = -4; j <= 4; j++)
				{
					WorldTile tile = World.world.GetTile(_fire.x + i, _fire.y + j);
					try
					{
						if (tile != null && tile.isOnFire())
						{
							tile.stopFire();
						}
					}
					catch
					{
					}
				}
			}
			_fire = new Vector2Int(-1, -1);
		}

		private static void Vigil(Quest q, Actor you, float dt, Action<string> toast)
		{
			float timeOfDay = FirstPerson.Towns.Calendar.TimeOfDay;
			bool flag = timeOfDay >= 0.75f || timeOfDay < 0.25f;
			bool flag2 = q.X >= 0 && (you.current_position - new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f)).magnitude < 7f;
			if (q.Taken == 0)
			{
				if (!flag2 || !flag)
				{
					return;
				}
				if (timeOfDay >= 0.82f || timeOfDay < 0.25f)
				{
					if (Time.unscaledTime - _vigilLate > 30f)
					{
						_vigilLate = Time.unscaledTime;
						toast?.Invoke("The night's too far gone for tonight's vigil. Be here at dusk.");
					}
				}
				else
				{
					q.Taken = 1;
					_vigilAway = 0f;
					_vigilFor = q.Giver;
					Save(q);
					toast?.Invoke("The vigil begins. Stay close until dawn.");
				}
			}
			else if (_vigilFor != q.Giver)
			{
				q.Taken = 0;
				Save(q);
				toast?.Invoke("Your vigil was broken. Begin again at dusk.");
			}
			else if (!flag)
			{
				q.Have = 1;
				q.Stage = 1;
				Save(q);
				toast?.Invoke("Dawn. Your vigil is kept. Go to " + q.GiverName + ".");
			}
			else
			{
				bool flag3 = _vigilAway <= 0f;
				_vigilAway = (flag2 ? 0f : (_vigilAway + dt));
				if (_vigilAway > 8f)
				{
					q.Taken = 0;
					Save(q);
					toast?.Invoke("You walked away. The vigil is broken; begin again at dusk.");
				}
				else if (!flag2 && flag3)
				{
					toast?.Invoke("Don't stray from the vigil.");
				}
			}
		}

		private static void CheckTrial(Quest q)
		{
			if ((q.Kind == QuestKind.TrialRange || q.Kind == QuestKind.TrialOre) && q.Stage == 0 && q.Have >= q.Need)
			{
				q.Stage = 1;
			}
		}

		private static bool TrialHeading(Quest q, out Vector2 at)
		{
			at = default(Vector2);
			if (q.Kind == QuestKind.TrialRange || q.Kind == QuestKind.TrialOre)
			{
				return false;
			}
			if (q.Kind == QuestKind.TrialVigil)
			{
				at = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
				return q.X >= 0;
			}
			if (q.Kind == QuestKind.TrialFire && q.Taken > 0 && _fire.x >= 0)
			{
				at = new Vector2((float)_fire.x + 0.5f, (float)_fire.y + 0.5f);
				return true;
			}
			Actor actor = Chronicle.Find(q.Giver);
			if (actor == null)
			{
				return false;
			}
			at = actor.current_position;
			return true;
		}

		private static void TrialBegun(Conversation c, Quest q)
		{
			switch (q.Kind)
			{
			case QuestKind.TrialStory:
				Story(c, q);
				return;
			case QuestKind.TrialSpar:
			case QuestKind.TrialParry:
				c.Say("*steps back and rolls their shoulders* Then come at me when you're ready. Close in, and we begin.");
				break;
			case QuestKind.TrialFire:
				c.Say("*closes their eyes* Stay close. I'll light it beside us.");
				break;
			case QuestKind.TrialVigil:
				c.Say("Then be at " + q.Place + " as the sun goes down. Stay close until it comes up again.");
				break;
			case QuestKind.TrialRange:
				c.Say("Go on, then. I'll hear of every hit.");
				break;
			default:
				c.Say("Off you go. Come back with your hands sore.");
				break;
			}
			c.Offer(Dialogue.Topics(c));
		}

		private static void TrialProgress(Conversation c, Quest q)
		{
			c.LastTopic = "trial";
			q = Log.Find((Quest x) => x == q) ?? Log.Find((Quest x) => !x.FromBoard && x.Giver == q.Giver && IsTrial(x.Kind));
			if (q == null)
			{
				c.Say("That's done with.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			if (q.Kind == QuestKind.TrialStory && q.Stage == 0)
			{
				Story(c, q);
				return;
			}
			if (q.Stage == 1)
			{
				c.Say(Dialogue.Pick("*nods* You have it now. It's yours.", "That's it. You've learned what I had to teach."), "Go on. You're " + Teachers.Word(q.Thing) + " now; use it well.");
				Reward(q, c.You, c.Toast);
				c.Offer(Dialogue.Topics(c));
				return;
			}
			c.Say(Dialogue.Pick("Well? Get to it.", "I'm waiting.", "We're not done yet."), "(" + TrialStatus(q) + ".)");
			c.Offer(new List<Choice>
			{
				new Choice("I give up.", delegate
				{
					Abandon(q, c.You);
					c.Say("*shrugs* The fee's gone. Try again after the next dawn, if you like.");
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Risk),
				new Choice("I'm going.", delegate
				{
					c.Say("Go on, then.");
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Back)
			});
		}

		private static void TrialPassed(Quest q, Actor you, Action<string> toast)
		{
			Actor actor = Chronicle.Find(q.Giver);
			bool flag = false;
			try
			{
				ActorTrait actorTrait = AssetManager.traits.get(q.Thing);
				flag = actorTrait != null && !you.hasTrait(q.Thing) && you.addTrait(actorTrait, pRemoveOpposites: true);
				if (flag)
				{
					TraitSwitch.Earned(you, q.Thing);
				}
				if (q.Renown > 0)
				{
					you.addRenown(q.Renown);
				}
				if (q.Xp > 0)
				{
					you.addExperience(q.Xp);
				}
			}
			catch (Exception ex)
			{
				FirstPerson.Log.Error("quests: trial reward: " + ex.Message);
			}
			if (actor != null)
			{
				Chronicle.ChangeOpinion(actor, you, q.Opinion);
				Chronicle.AddOwnMemory(actor, "fp_student", Dialogue.NameOf(you));
			}
			Chronicle.AddOwnMemory(you, "fp_teacher", q.GiverName);
			q.Stage = 2;
			Save(q);
			Drop(q);
			Completed++;
			Run.Tally(you, "trial");
			if (flag)
			{
				Run.Tally(you, "trait");
			}
			toast?.Invoke("Trial passed: you are " + Teachers.Word(q.Thing) + " (" + Teachers.Name(q.Thing) + ").");
			FirstPerson.Log.Info($"quests: trial passed, {q.Thing} from {q.GiverName} (given {flag})");
			try
			{
				you.data.removeString("fp_trial_done");
			}
			catch
			{
			}
			WorldBoxMod.Instance?.Audio.PlayRustle();
		}

		private static void TrialLost(Quest q, Actor you)
		{
			Actor actor = Chronicle.Find(q.Giver);
			try
			{
				if (actor != null)
				{
					actor.data.get("fp_tw", out var pResult, null);
					List<string> list = new List<string>();
					if (!string.IsNullOrEmpty(pResult))
					{
						string[] array = pResult.Split(',');
						foreach (string text in array)
						{
							if (text.Length > 0 && !text.StartsWith(q.Thing + ":", StringComparison.Ordinal))
							{
								list.Add(text);
							}
						}
					}
					list.Add(q.Thing + ":" + FirstPerson.Towns.Calendar.BillDay);
					actor.data.set("fp_tw", string.Join(",", list.ToArray()));
				}
			}
			catch
			{
			}
			if (Bout(q))
			{
				_bout = 0L;
				Calm(actor, you);
			}
			if (q.Kind == QuestKind.TrialFire)
			{
				PutOutFire();
			}
		}

		internal static bool MayTryAgain(Actor teacher, string trait)
		{
			try
			{
				teacher.data.get("fp_tw", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return true;
				}
				string[] array = pResult.Split(',');
				foreach (string text in array)
				{
					int num = text.LastIndexOf(':');
					if (num > 0 && text.Substring(0, num) == trait && int.TryParse(text.Substring(num + 1), out var result) && FirstPerson.Towns.Calendar.BillDay <= result)
					{
						return false;
					}
				}
				return true;
			}
			catch
			{
				return true;
			}
		}

		public static void TeachChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			if (Teachers.CanTeach(with).Count == 0)
			{
				return;
			}
			foreach (Quest item in Log)
			{
				if (!item.FromBoard && item.Giver == with.data.id)
				{
					return;
				}
			}
			list.Add(new Choice("Could you teach me something?", delegate
			{
				Teach(c);
			}, ChoiceTone.Quest));
		}

		private static void Teach(Conversation c)
		{
			c.LastTopic = "teach";
			Actor with = c.With;
			Actor you = c.You;
			if (c.Voice.Warmth <= -2)
			{
				c.Say("*scoffs* Teach you? No.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			Quest quest = Read(with.data);
			if (quest != null && quest.Stage < 2)
			{
				c.Say((quest.Holder == you.data.id) ? "Finish what I asked of you first." : "I'm busy with someone else's business just now. Another time.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			List<KeyValuePair<string, Teachers.School>> source = Teachers.CanTeach(with);
			c.Think(0.7f);
			c.Say(Dialogue.Pick("*thinks* I could teach you a thing or two. For a price.", "Maybe. Depends what you want to learn.", "*looks you over* What would you learn?"));
			List<Choice> list = new List<Choice>();
			foreach (KeyValuePair<string, Teachers.School> item in source.Where((KeyValuePair<string, Teachers.School> kv) => !you.hasTrait(kv.Key)).Concat(source.Where((KeyValuePair<string, Teachers.School> kv) => you.hasTrait(kv.Key))).Take(5))
			{
				string trait = item.Key;
				Teachers.School school = item.Value;
				string label = (you.hasTrait(trait) ? (Teachers.Name(trait) + " (you have it)") : $"{Teachers.Name(trait)} ({Teachers.Fee(with, you, trait)} gold)");
				list.Add(new Choice(label, delegate
				{
					Lesson(c, trait, school);
				}, ChoiceTone.Quest));
			}
			list.Add(new Choice("Never mind.", delegate
			{
				c.Say("Suit yourself.");
				c.Offer(Dialogue.Topics(c));
			}, ChoiceTone.Back));
			c.Offer(list);
		}

		private static void Lesson(Conversation c, string trait, Teachers.School school)
		{
			Actor with = c.With;
			Actor you = c.You;
			string text = Teachers.Refusal(with, you, trait);
			if (text == null && !MayTryAgain(with, trait))
			{
				text = "You failed me once already. Come back after the next dawn.";
			}
			if (text == null && Log.Count >= 5)
			{
				text = $"You've promised {5} people already. Finish something first.";
			}
			Quest quest = ((text == null) ? Log.Find((Quest x) => IsTrial(x.Kind) && x.Stage < 2) : null);
			if (quest != null)
			{
				text = "You're in the middle of a trial with " + quest.GiverName + ". Finish that first.";
			}
			if (text != null)
			{
				c.Say(text);
				c.Offer(Dialogue.Topics(c));
			}
			else
			{
				Brief(c, MakeTrial(with, you, trait, school));
			}
		}

		internal static Quest TrialForTest(Actor teacher, Actor you, string trait, out string refused)
		{
			refused = Teachers.Refusal(teacher, you, trait);
			if (refused == null && !MayTryAgain(teacher, trait))
			{
				refused = "not before the next dawn";
			}
			if (refused != null)
			{
				return null;
			}
			List<KeyValuePair<string, Teachers.School>> list = Teachers.CanTeach(teacher);
			int num = list.FindIndex((KeyValuePair<string, Teachers.School> kv) => kv.Key == trait);
			if (num < 0)
			{
				refused = "they can't teach that";
				return null;
			}
			Quest quest = MakeTrial(teacher, you, trait, list[num].Value);
			if (!Accept(quest, you, null))
			{
				return null;
			}
			return quest;
		}

		internal static List<KeyValuePair<string, string[]>> StoryForTest(Actor elder, out List<string> told)
		{
			return StoryQuestions(elder, out told);
		}

		private static void Story(Conversation c, Quest q)
		{
			List<string> told;
			List<KeyValuePair<string, string[]>> asks = StoryQuestions(c.With, out told);
			c.Think(1.2f);
			told.Add("Now. Let's see if you listened.");
			c.Say(told.ToArray());
			Ask(c, q, asks, 0, 0);
		}

		private static void Ask(Conversation c, Quest q, List<KeyValuePair<string, string[]>> asks, int k, int right)
		{
			if (k >= asks.Count)
			{
				Quest quest = Log.Find((Quest x) => x == q) ?? Log.Find((Quest x) => !x.FromBoard && x.Giver == q.Giver && x.Kind == q.Kind);
				if (quest == null || quest.Stage != 0)
				{
					c.Say("*waves a hand* That's done with.");
					c.Offer(Dialogue.Topics(c));
					return;
				}
				q = quest;
				if (right >= asks.Count)
				{
					c.Say("*smiles* You listened. That's most of wisdom, you know.");
					TrialPassed(q, c.You, c.Toast);
				}
				else
				{
					c.Say($"*sighs* {right} of {asks.Count}. You heard, but you didn't listen. Come back after the next dawn.");
					Fail(q, c.You, "wrong answers", c.Toast);
				}
				c.Offer(Dialogue.Topics(c));
				return;
			}
			string[] value = asks[k].Value;
			int num = (int)((q.Giver + k) % 3);
			c.Say(asks[k].Key);
			List<Choice> list = new List<Choice>();
			for (int num2 = 0; num2 < value.Length; num2++)
			{
				int pick = (num2 + num) % value.Length;
				list.Add(new Choice(value[pick], delegate
				{
					Ask(c, q, asks, k + 1, right + ((pick == 0) ? 1 : 0));
				}));
			}
			c.Offer(list);
		}

		private static List<KeyValuePair<string, string[]>> StoryQuestions(Actor elder, out List<string> told)
		{
			told = new List<string>();
			List<KeyValuePair<string, string[]>> list = new List<KeyValuePair<string, string[]>>();
			int num = 60;
			int num2 = 0;
			string text = null;
			string text2 = null;
			try
			{
				num = elder.getAge();
			}
			catch
			{
			}
			try
			{
				num2 = Enumerable.Count(elder.getChildren(pOnlyCurrentFamily: false));
			}
			catch
			{
			}
			try
			{
				text = elder.city?.name;
			}
			catch
			{
			}
			try
			{
				text2 = Date.getYear(elder.data.created_time).ToString();
			}
			catch
			{
			}
			TreeNode treeNode = null;
			TreeNode treeNode2 = null;
			try
			{
				FamilyTree familyTree = Dialogue.TreeOf(elder);
				treeNode = Array.Find(familyTree.Parents, (TreeNode p) => p != null && !p.Female);
				treeNode2 = Array.Find(familyTree.Parents, (TreeNode p) => p?.Female ?? false);
			}
			catch
			{
			}
			told.Add((text2 != null) ? ($"I was born in the year {text2}. I'm {num} now" + ((text != null) ? (", and I've watched " + text + " grow up round me.") : ", and I've walked a long way.")) : $"I'm {num} years old. Older than most here.");
			TreeNode treeNode3 = treeNode ?? treeNode2;
			told.Add((treeNode3 != null) ? ("My " + ((treeNode3 == treeNode) ? "father" : "mother") + " was " + treeNode3.Name + ". " + (treeNode3.Dead ? "Gone now, but I think of them often." : "Still about, bless them.")) : "I never knew my parents. The town raised me, more or less.");
			told.Add((num2 == 0) ? "I've no children of my own. The town's young are mine, in a way." : string.Format("I've {0} {1}, and they're my pride.", num2, (num2 == 1) ? "child" : "children"));
			list.Add(new KeyValuePair<string, string[]>("How old am I?", new string[3]
			{
				num.ToString(),
				(num + 7).ToString(),
				((num > 27) ? (num - 9) : (num + 13)).ToString()
			}));
			list.Add(new KeyValuePair<string, string[]>("How many children have I?", new string[3]
			{
				num2.ToString(),
				(num2 + 2).ToString(),
				((num2 == 0) ? 1 : (num2 - 1)).ToString()
			}));
			if (treeNode3 != null)
			{
				list.Add(new KeyValuePair<string, string[]>("What was my " + ((treeNode3 == treeNode) ? "father's" : "mother's") + " name?", new string[3]
				{
					treeNode3.Name,
					OtherName(treeNode3.Name, 1),
					OtherName(treeNode3.Name, 2)
				}));
			}
			else
			{
				list.Add(new KeyValuePair<string, string[]>("Who raised me?", new string[3] { "The town", "My grandmother", "A passing trader" }));
			}
			return list;
		}

		private static string OtherName(string name, int k)
		{
			string[] array = new string[10] { "Bram", "Hilda", "Oswin", "Mira", "Tobin", "Edda", "Garet", "Liss", "Corwin", "Sanne" };
			int num = ((name?.Length ?? 3) * 3 + k * 4) % array.Length;
			if (array[num] == name)
			{
				num = (num + 5) % array.Length;
			}
			return array[num];
		}

		private static Quest MakeUndead(Actor giver, Actor you)
		{
			City city = giver?.city;
			if (city == null || you == null || TownLevelTask(city) != null)
			{
				return null;
			}
			WorldTile worldTile = null;
			try
			{
				worldTile = city.getTile();
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return null;
			}
			LevelState levelState = LevelOf(you);
			System.Random random = new System.Random((int)((giver.data.id * 37 + Chronicle.Year * 101 + levelState.Done2 * 7) & 0x7FFFFFFF));
			WorldTile worldTile2 = null;
			string text = null;
			for (int i = 0; i < 240; i++)
			{
				if (worldTile2 != null)
				{
					break;
				}
				double num = random.NextDouble() * Math.PI * 2.0;
				float num2 = 25f + (float)random.NextDouble() * 15f;
				WorldTile tile = World.world.GetTile(worldTile.x + (int)(Math.Cos(num) * (double)num2), worldTile.y + (int)(Math.Sin(num) * (double)num2));
				if (GoodGround(tile, worldTile))
				{
					worldTile2 = tile;
				}
			}
			if (worldTile2 == null)
			{
				return null;
			}
			text = SiteName(worldTile2, city, random);
			int num3 = Mathf.Clamp(4 + levelState.Done2 / 2, 4, 12);
			bool flag = levelState.Done2 % 4 == 3 && AssetManager.actor_library.has("necromancer");
			Quest quest = New(QuestKind.Undead, giver, 40 + 8 * num3 + (flag ? 30 : 0), flag ? 10 : 6, flag ? 40 : 25, 20);
			quest.X = worldTile2.x;
			quest.Y = worldTile2.y;
			quest.Place = text;
			quest.Need = num3 + (flag ? 1 : 0);
			quest.Thing = (flag ? "necro" : "");
			quest.Extra = "0!";
			return ToTown(quest, city);
		}

		private static string SiteName(WorldTile site, City city, System.Random rng)
		{
			string text = ((city != null) ? city.name : "town");
			string text2 = Compass(new Vector2(site.x, site.y) - TownCentre(city)).ToLowerInvariant();
			try
			{
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple == null || !simple.isAlive() || simple.current_tile == null || simple.asset == null)
					{
						continue;
					}
					int num = simple.current_tile.x - site.x;
					int num2 = simple.current_tile.y - site.y;
					if (num * num + num2 * num2 <= 64)
					{
						string text3 = simple.asset.id ?? "";
						if (text3.Contains("tomb") || text3.Contains("grave"))
						{
							return "the graves " + text2 + " of " + text;
						}
						if (text3.StartsWith("ruin", StringComparison.Ordinal) || text3.Contains("_ruin"))
						{
							return "the ruin " + text2 + " of " + text;
						}
					}
				}
			}
			catch
			{
			}
			string[] array = new string[5] { "the old barrow", "the sunken crypt", "the burial mound", "the forgotten graves", "the bone field" };
			return array[rng.Next(array.Length)] + " " + text2 + " of " + text;
		}

		private static Vector2 TownCentre(City c)
		{
			try
			{
				WorldTile worldTile = c?.getTile();
				if (worldTile != null)
				{
					return new Vector2(worldTile.x, worldTile.y);
				}
			}
			catch
			{
			}
			return Vector2.zero;
		}

		private static bool SiteSpawned(Quest q)
		{
			if (!string.IsNullOrEmpty(q.Extra))
			{
				return q.Extra.StartsWith("1", StringComparison.Ordinal);
			}
			return false;
		}

		private static List<long> SiteIds(Quest q)
		{
			UndeadMemo value = Memos.GetValue(q, (Quest _) => new UndeadMemo());
			if (value.Ids != null && string.Equals(value.Extra, q.Extra, StringComparison.Ordinal))
			{
				return value.Ids;
			}
			List<long> list = new List<long>();
			value.Extra = q.Extra;
			value.Ids = list;
			if (string.IsNullOrEmpty(q.Extra))
			{
				return list;
			}
			int num = q.Extra.IndexOf('!');
			if (num < 0 || num + 1 >= q.Extra.Length)
			{
				return list;
			}
			string[] array = q.Extra.Substring(num + 1).Split('.');
			for (int num2 = 0; num2 < array.Length; num2++)
			{
				if (long.TryParse(array[num2], out var result))
				{
					list.Add(result);
				}
			}
			return list;
		}

		private static void SetSite(Quest q, bool spawned, List<long> ids)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(spawned ? '1' : '0').Append('!');
			for (int i = 0; i < ids.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append('.');
				}
				stringBuilder.Append(ids[i]);
			}
			q.Extra = stringBuilder.ToString();
		}

		private static void RaiseSite(Quest q, Action<string> toast)
		{
			WorldTile tile = World.world.GetTile(q.X, q.Y);
			if (tile == null)
			{
				return;
			}
			List<long> list = new List<long>();
			System.Random random = new System.Random((int)((q.Giver * 13 + q.X * 131 + q.Y) & 0x7FFFFFFF));
			try
			{
				EffectsLibrary.spawn("fx_spawn", tile);
			}
			catch
			{
			}
			string text = ZombieOf(q);
			bool flag = q.Thing == "necro";
			int num = (flag ? (q.Need - 1) : q.Need);
			if (flag)
			{
				Actor actor = SpawnDead("necromancer", tile, 2, random);
				if (actor != null)
				{
					list.Add(actor.data.id);
				}
			}
			for (int i = 0; i < num; i++)
			{
				Actor actor2 = SpawnDead((text != null && random.Next(5) < 2) ? text : "skeleton", tile, 4, random);
				if (actor2 != null)
				{
					list.Add(actor2.data.id);
				}
			}
			q.Need = list.Count;
			q.Have = 0;
			SetSite(q, spawned: true, list);
			Save(q);
			toast?.Invoke("The dead rise at " + q.Place + "!" + (flag ? " A necromancer leads them." : ""));
			FirstPerson.Log.Info($"quests: {q.GiverName}'s site at {q.X},{q.Y}: {list.Count} rose" + (flag ? " (a necromancer leads)" : ""));
			try
			{
				WorldBoxMod.Instance?.Audio.PlayEffect("cloth", 0.4f);
			}
			catch
			{
			}
		}

		private static string ZombieOf(Quest q)
		{
			string text = null;
			try
			{
				foreach (Actor item in Civil.PeopleOf(q.FromBoard ? CityById(q.Giver) : Chronicle.Find(q.Giver)?.city))
				{
					text = item?.asset?.id;
					if (text != null)
					{
						break;
					}
				}
			}
			catch
			{
			}
			if (text != null && AssetManager.actor_library.has("zombie_" + text))
			{
				return "zombie_" + text;
			}
			if (!AssetManager.actor_library.has("zombie"))
			{
				return null;
			}
			return "zombie";
		}

		private static Actor SpawnDead(string id, WorldTile center, int spread, System.Random rng)
		{
			if (!AssetManager.actor_library.has(id))
			{
				return null;
			}
			for (int i = 0; i < 12; i++)
			{
				WorldTile tile = World.world.GetTile(center.x + rng.Next(-spread, spread + 1), center.y + rng.Next(-spread, spread + 1));
				if (Dry(tile) && tile.isSameIsland(center))
				{
					try
					{
						return World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					}
					catch (Exception ex)
					{
						FirstPerson.Log.Error("quests: raising " + id + ": " + ex.Message);
						return null;
					}
				}
			}
			return null;
		}

		private static bool IsDead(Actor a)
		{
			string text = null;
			try
			{
				text = a?.asset?.id;
			}
			catch
			{
			}
			switch (text)
			{
			default:
				if (!text.StartsWith("zombie", StringComparison.Ordinal))
				{
					return text == "ghost";
				}
				goto case "skeleton";
			case "skeleton":
			case "necromancer":
				return true;
			case null:
				return false;
			}
		}

		private static List<Actor> SiteAlive(Quest q)
		{
			List<Actor> siteAlive = _siteAlive;
			siteAlive.Clear();
			foreach (long item in SiteIds(q))
			{
				Actor actor = HerdAnimal(item);
				if (actor != null)
				{
					siteAlive.Add(actor);
				}
			}
			return siteAlive;
		}

		private static Actor StragglerSoon(Quest q)
		{
			UndeadMemo value = Memos.GetValue(q, (Quest _) => new UndeadMemo());
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - value.StragglerAt >= 0.5f)
			{
				value.StragglerAt = unscaledTime;
				Actor actor = DeadNearSite(q);
				value.Straggler = ((actor != null && actor.data != null) ? actor.data.id : (-1));
			}
			if (value.Straggler < 0)
			{
				return null;
			}
			return HerdAnimal(value.Straggler);
		}

		private static Actor DeadNearSite(Quest q)
		{
			Vector2 vector = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && IsDead(simple) && (simple.current_position - vector).sqrMagnitude < 196f)
					{
						return simple;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static void CheckUndead(Quest q, Actor you, Action<string> toast)
		{
			if (q.FromBoard && CityById(q.Giver) == null)
			{
				Fail(q, you, q.GiverName + " is gone", toast);
			}
			else
			{
				if (q.Stage != 0)
				{
					return;
				}
				if (!SiteSpawned(q))
				{
					if ((you.current_position - new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f)).sqrMagnitude <= 324f)
					{
						RaiseSite(q, toast);
					}
					return;
				}
				List<Actor> list = SiteAlive(q);
				int have = q.Have;
				q.Have = Mathf.Max(0, q.Need - list.Count);
				if (list.Count == 0 && DeadNearSite(q) == null)
				{
					q.Have = q.Need;
					q.Stage = 1;
				}
				else if (q.Have != have)
				{
					Save(q);
				}
			}
		}

		private static bool UndeadHeading(Quest q, out Vector2 at)
		{
			at = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
			if (!SiteSpawned(q))
			{
				return true;
			}
			Vector2 vector = (WorldBoxMod.Instance?.Host)?.current_position ?? at;
			float num = float.MaxValue;
			foreach (Actor item in SiteAlive(q))
			{
				float sqrMagnitude = (item.current_position - vector).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					at = item.current_position;
				}
			}
			if (num == float.MaxValue)
			{
				Actor actor = StragglerSoon(q);
				if (actor != null)
				{
					at = actor.current_position;
				}
			}
			return true;
		}

		private static void UndeadStars(Quest q, List<KeyValuePair<Actor, bool>> into)
		{
			if (!SiteSpawned(q))
			{
				return;
			}
			foreach (Actor item in SiteAlive(q))
			{
				Star(into, item, gold: true);
			}
		}

		private static string UndeadTitle(Quest q)
		{
			return "Put the dead to rest at " + q.Place;
		}

		private static string UndeadStatus(Quest q)
		{
			if (SiteSpawned(q))
			{
				return $"{q.Have}/{q.Need} of the dead destroyed" + ((q.Thing == "necro") ? " (a necromancer leads them)" : "");
			}
			return "Go to " + q.Place;
		}

		private static string[] UndeadOneBreath(Quest q, Actor npc, string span, string pay)
		{
			string text = ((npc?.city != null) ? npc.city.name : "town");
			string text2 = ((q.Thing == "necro") ? " Something leads them this time: a necromancer, raising more as you fight. Kill him first." : "");
			return new string[2]
			{
				"The dead walk at " + q.Place + ". They'll come for " + text + " if no one stops them." + text2,
				"Destroy every one of them within " + span + ". For that, " + pay + "."
			};
		}

		private static void UndeadQuestions(Quest q, Actor npc, Actor you, Action<string, Func<string>> add)
		{
			add("Where is it?", () => Where(q, npc, you));
			add("How many are there?", () => (!(q.Thing == "necro")) ? $"About {q.Need}. Skeletons, and some of our own dead come back." : $"About {q.Need - 1}, and the necromancer. He'll raise more while he lives.");
			add("Can I bring help?", () => "Bring whoever you like. The dead don't care who puts them down, and neither do I.");
		}

		private static string UndeadProof(Quest q)
		{
			return "When nothing walks there anymore, I'll know. Then come to me.";
		}

		private static string UndeadThanks(Quest q)
		{
			return Dialogue.Pick(q.Place + " is quiet again? *breathes out* Thank the gods. Here.", "The dead sleep again. You've earned this.");
		}

		private static string UndeadWaiting(Quest q)
		{
			if (!SiteSpawned(q))
			{
				return "Have you been to " + q.Place + "? The compass will show you.";
			}
			return $"{q.Need - q.Have} of them still walk.";
		}
	}
}
