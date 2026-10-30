using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Personas
	{
		private static readonly string[] TemperTraits = new string[24]
		{
			"psychopath", "evil", "bloodlust", "hotheaded", "savage", "deceitful", "thief", "greedy", "paranoid", "lustful",
			"ambitious", "blessed", "miracle_born", "scar_of_divinity", "peaceful", "pacifist", "honest", "genius", "wise", "stupid",
			"veteran", "kingslayer", "dragonslayer", "content"
		};

		private static readonly Voices.Temper[] TemperOfTrait = new Voices.Temper[24]
		{
			Voices.Temper.Cruel,
			Voices.Temper.Cruel,
			Voices.Temper.Cruel,
			Voices.Temper.Hothead,
			Voices.Temper.Hothead,
			Voices.Temper.Sly,
			Voices.Temper.Sly,
			Voices.Temper.Greedy,
			Voices.Temper.Wary,
			Voices.Temper.Lustful,
			Voices.Temper.Ambitious,
			Voices.Temper.Pious,
			Voices.Temper.Pious,
			Voices.Temper.Pious,
			Voices.Temper.Gentle,
			Voices.Temper.Gentle,
			Voices.Temper.Honest,
			Voices.Temper.Clever,
			Voices.Temper.Wise,
			Voices.Temper.Dull,
			Voices.Temper.Hardened,
			Voices.Temper.Hardened,
			Voices.Temper.Hardened,
			Voices.Temper.Easy
		};

		private static readonly string[][] BeastWords = new string[6][]
		{
			new string[10] { "cat", "fox", "alien", "crystal", "cold_one", "gentleman", "candy", "unicorn", "snowman", "lynx" },
			new string[18]
			{
				"chicken", "bird", "crow", "penguin", "duck", "goose", "parrot", "owl", "ostrich", "turkey",
				"pigeon", "cow", "buffalo", "alpaca", "sheep", "goat", "llama", "flamingo"
			},
			new string[11]
			{
				"bee", "ant", "beetle", "wasp", "fly", "mosquito", "termite", "scorpion", "spider", "worm",
				"bug"
			},
			new string[11]
			{
				"wolf", "dog", "hyena", "jackal", "coyote", "bear", "lion", "tiger", "rhino", "boar",
				"monkey"
			},
			new string[11]
			{
				"rabbit", "rat", "mouse", "mole", "hamster", "capybara", "armadillo", "squirrel", "hedgehog", "badger",
				"beaver"
			},
			new string[12]
			{
				"frog", "toad", "crocodile", "alligator", "turtle", "crab", "fish", "piranha", "snake", "lizard",
				"newt", "angle"
			}
		};

		private static readonly Voices.Accent[] BeastAccents = new Voices.Accent[6]
		{
			Voices.Accent.Aloof,
			Voices.Accent.Flock,
			Voices.Accent.Hive,
			Voices.Accent.Pack,
			Voices.Accent.Burrow,
			Voices.Accent.Swamp
		};

		private static readonly string[] SickTraits = new string[5] { "plague", "infected", "tumor_infection", "mush_spores", "contagious" };

		public static Voices.Persona Of(Actor npc, Actor you, Voice v)
		{
			Voices.Persona p = default(Voices.Persona);
			if (npc == null)
			{
				return p;
			}
			try
			{
				p.Id = npc.data.id;
			}
			catch
			{
			}
			Tempers(npc, ref p);
			p.Age = AgeOf(npc);
			p.Accent = AccentOf(npc);
			p.Female = Models.IsFemale(npc);
			p.Job = JobOf(npc);
			p.Warmth = v.Warmth;
			p.Tie = TieOf(npc, you, v);
			p.Plight = PlightOf(npc, you);
			p.Mood = MoodOf(npc, in p);
			Regard regard = ((you != null) ? Chronicle.RegardOf(npc, you, create: false) : null);
			p.Met = regard != null && regard.Talks >= 2;
			p.Bond = Bonds.Of(npc, you, p.Tie);
			return p;
		}

		private static void Tempers(Actor npc, ref Voices.Persona p)
		{
			bool flag = false;
			for (int i = 0; i < TemperTraits.Length; i++)
			{
				bool flag2 = false;
				try
				{
					flag2 = npc.hasTrait(TemperTraits[i]);
				}
				catch
				{
				}
				if (flag2)
				{
					Voices.Temper temper = TemperOfTrait[i];
					if (!flag)
					{
						p.First = (p.Second = temper);
						flag = true;
					}
					else if (temper != p.First)
					{
						p.Second = temper;
						break;
					}
				}
			}
		}

		public static Voices.AgeBand AgeOf(Actor npc)
		{
			try
			{
				if (!npc.isAdult())
				{
					return Voices.AgeBand.Child;
				}
				float ageRatio = npc.getAgeRatio();
				if (ageRatio > 0.75f)
				{
					return Voices.AgeBand.Elder;
				}
				if (ageRatio < 0.3f)
				{
					return Voices.AgeBand.Young;
				}
			}
			catch
			{
			}
			return Voices.AgeBand.Grown;
		}

		public static Voices.Accent AccentOf(Actor npc)
		{
			string text = null;
			try
			{
				text = ((npc.asset != null) ? npc.asset.id : null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text))
			{
				return Voices.Accent.Human;
			}
			switch (text)
			{
			case "human":
				return Voices.Accent.Human;
			case "elf":
				return Voices.Accent.Elf;
			case "orc":
				return Voices.Accent.Orc;
			case "dwarf":
				return Voices.Accent.Dwarf;
			default:
			{
				for (int i = 0; i < BeastWords.Length; i++)
				{
					string[] array = BeastWords[i];
					foreach (string value in array)
					{
						if (text.Contains(value))
						{
							return BeastAccents[i];
						}
					}
				}
				return Voices.Accent.Human;
			}
			}
		}

		public static string JobOf(Actor npc)
		{
			string text = Dialogue.JobName(npc);
			switch (text)
			{
			case "queen":
				return "king";
			case "miner":
			case "smith":
			case "fisher":
			case "hunter":
			case "farmer":
			case "leader":
			case "builder":
			case "warrior":
			case "woodcutter":
			case "gatherer":
			case "king":
				return text;
			default:
				return null;
			}
		}

		private static Voices.Tie TieOf(Actor npc, Actor you, Voice v)
		{
			if (you == null)
			{
				return Voices.Tie.None;
			}
			try
			{
				if (npc.lover == you)
				{
					return Voices.Tie.Lover;
				}
				if (v.Sovereign)
				{
					return Voices.Tie.Sovereign;
				}
				if (npc.isParentOf(you) || you.isParentOf(npc))
				{
					return Voices.Tie.Kin;
				}
				long parent_id_ = you.data.parent_id_1;
				long parent_id_2 = you.data.parent_id_2;
				long parent_id_3 = npc.data.parent_id_1;
				long parent_id_4 = npc.data.parent_id_2;
				if ((parent_id_ > 0 && (parent_id_ == parent_id_3 || parent_id_ == parent_id_4)) || (parent_id_2 > 0 && (parent_id_2 == parent_id_3 || parent_id_2 == parent_id_4)))
				{
					return Voices.Tie.Kin;
				}
				if (npc.family != null && npc.family == you.family)
				{
					return Voices.Tie.Kin;
				}
				if (Dialogue.Enemies(npc, you))
				{
					return Voices.Tie.Enemy;
				}
				if (!Dialogue.SameTribe(npc, you) && npc.kingdom != you.kingdom)
				{
					return Voices.Tie.Foreign;
				}
			}
			catch
			{
			}
			return Voices.Tie.None;
		}

		private static Voices.Plight PlightOf(Actor npc, Actor you)
		{
			Voices.Plight plight = Voices.Plight.None;
			try
			{
				if (npc.isStarving() || npc.isHungry())
				{
					plight |= Voices.Plight.Hungry;
				}
			}
			catch
			{
			}
			try
			{
				int maxHealth = npc.getMaxHealth();
				if (maxHealth > 0 && npc.getHealth() < maxHealth / 2)
				{
					plight |= Voices.Plight.Hurt;
				}
			}
			catch
			{
			}
			string[] sickTraits = SickTraits;
			foreach (string id in sickTraits)
			{
				if (Dialogue.HasTrait(npc, id))
				{
					plight |= Voices.Plight.Sick;
					break;
				}
			}
			Record record = Chronicle.Of(npc);
			if (record != null)
			{
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
						plight |= Voices.Plight.Bereaved;
					}
					else if (memory.Kind == "fallen_in_love")
					{
						plight |= Voices.Plight.InLove;
					}
				}
			}
			try
			{
				if (npc.lover == null || npc.lover == you)
				{
					plight &= ~Voices.Plight.InLove;
				}
				if (npc.isAdult())
				{
					int money = npc.money;
					if (money >= 150 && !npc.isKing())
					{
						plight |= Voices.Plight.Rich;
					}
					else if (money <= 0 && !npc.isKing() && !npc.isCityLeader())
					{
						plight |= Voices.Plight.Poor;
					}
				}
			}
			catch
			{
			}
			return plight;
		}

		private static Voices.Mood MoodOf(Actor npc, in Voices.Persona p)
		{
			int num = Dialogue.SafeHappiness(npc);
			if (num >= 50)
			{
				return Voices.Mood.Happy;
			}
			if (num <= -60)
			{
				return Voices.Mood.Dark;
			}
			if (num <= -20)
			{
				if (!p.Has(Voices.Temper.Hothead) && !p.Has(Voices.Temper.Cruel))
				{
					return Voices.Mood.Sad;
				}
				return Voices.Mood.Angry;
			}
			return Voices.Mood.Normal;
		}

		public static Voices.Scene SceneNow(Actor you)
		{
			Voices.Scene result = new Voices.Scene
			{
				Season = -1
			};
			float num = 0.5f;
			try
			{
				num = Calendar.TimeOfDay;
			}
			catch
			{
			}
			num -= Mathf.Floor(num);
			result.Hour = ((num < 0.2f || num >= 0.88f) ? Voices.Hour.Night : ((!(num < 0.32f)) ? ((num < 0.75f) ? Voices.Hour.Day : Voices.Hour.Dusk) : Voices.Hour.Dawn));
			try
			{
				result.Season = (int)Calendar.Season;
			}
			catch
			{
			}
			result.Sky = SkyOver(you);
			return result;
		}

		private static Voices.Sky SkyOver(Actor you)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || you == null)
			{
				return Voices.Sky.Clear;
			}
			try
			{
				Weather skyWeather = instance.SkyWeather;
				if (skyWeather == null)
				{
					return Voices.Sky.Clear;
				}
				Vector2 current_position = you.current_position;
				Precip drops = Precip.None;
				if (((skyWeather.Storms.Count > 0) ? skyWeather.Cover(current_position.x, current_position.y, instance.SkyTime, out drops) : 0f) > 0.3f)
				{
					switch (drops)
					{
					case Precip.Snow:
						return Voices.Sky.Snow;
					case Precip.Rain:
					case Precip.Acid:
						return (skyWeather.Bolts.Count <= 0) ? Voices.Sky.Rain : Voices.Sky.Storm;
					}
				}
				if (skyWeather.GlobalStrength > 0.3f)
				{
					if (skyWeather.Global == Precip.Snow)
					{
						return Voices.Sky.Snow;
					}
					if (skyWeather.Global == Precip.Rain)
					{
						return Voices.Sky.Rain;
					}
				}
			}
			catch
			{
			}
			return Voices.Sky.Clear;
		}

		public static Voices.Slots SlotsFor(Actor npc, Actor you, Voice v)
		{
			Voices.Slots slots = new Voices.Slots
			{
				You = ((you != null) ? Dialogue.NameOf(you) : null),
				Name = ((npc != null) ? Dialogue.NameOf(npc) : null),
				Town = ((npc != null) ? Dialogue.CityName(npc) : null),
				Realm = ((npc != null) ? Dialogue.KingdomName(npc) : null)
			};
			try
			{
				if (npc != null && npc.lover != null && npc.lover != you)
				{
					slots.Lover = Dialogue.NameOf(npc.lover);
				}
			}
			catch
			{
			}
			if (v.Sovereign && you != null)
			{
				slots.Title = Dialogue.Majesty(you);
			}
			return slots;
		}
	}
}
