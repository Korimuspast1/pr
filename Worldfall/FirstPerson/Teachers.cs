using System;
using System.Collections.Generic;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Teachers
	{
		internal enum School
		{
			Soldier,
			Duelist,
			Hunter,
			Miner,
			Mage,
			Priest,
			Elder
		}

		private static readonly Dictionary<School, string[]> Teaches = new Dictionary<School, string[]>
		{
			[School.Soldier] = new string[4] { "strong", "tough", "hard_skin", "boosted_vitality" },
			[School.Duelist] = new string[4] { "block", "dodge", "backstep", "battle_reflexes" },
			[School.Hunter] = new string[4] { "eagle_eyed", "fast", "agile", "dash" },
			[School.Miner] = new string[1] { "miner" },
			[School.Mage] = new string[5] { "heart_of_wizard", "arcane_reflexes", "fire_proof", "freeze_proof", "regeneration" },
			[School.Priest] = new string[5] { "blessed", "sunblessed", "moonchild", "nightchild", "lucky" },
			[School.Elder] = new string[3] { "wise", "long_liver", "strong_minded" }
		};

		internal static readonly HashSet<string> NeverTaught = new HashSet<string> { "veteran", "kingslayer", "mageslayer", "dragonslayer", "immortal" };

		internal static int Worth(string trait)
		{
			if (!Run.Costs.TryGetValue(trait, out var value) || value <= 0)
			{
				return 3;
			}
			return value;
		}

		internal static List<KeyValuePair<string, School>> CanTeach(Actor a)
		{
			List<KeyValuePair<string, School>> list = new List<KeyValuePair<string, School>>();
			if (a == null)
			{
				return list;
			}
			try
			{
				if (!a.isAlive() || !a.isAdult() || !People.IsHumanoid(a))
				{
					return list;
				}
				if (a.isKing())
				{
					return list;
				}
				foreach (School item in SchoolsOf(a))
				{
					string[] array = Teaches[item];
					foreach (string t in array)
					{
						if (a.hasTrait(t) && !list.Exists((KeyValuePair<string, School> kv) => kv.Key == t))
						{
							list.Add(new KeyValuePair<string, School>(t, item));
						}
					}
				}
			}
			catch
			{
			}
			return list;
		}

		internal static string Word(string trait)
		{
			switch (trait)
			{
			case "hard_skin":
				return "hard of skin";
			case "boosted_vitality":
				return "full of vigour";
			case "block":
				return "quick to block";
			case "dodge":
				return "quick to dodge";
			case "backstep":
				return "light on your feet";
			case "battle_reflexes":
				return "quick in battle";
			case "eagle_eyed":
				return "eagle-eyed";
			case "dash":
				return "quick off the mark";
			case "miner":
				return "a miner";
			case "heart_of_wizard":
				return "touched by magic";
			case "arcane_reflexes":
				return "quick with spells";
			case "fire_proof":
				return "proof against fire";
			case "freeze_proof":
				return "proof against frost";
			case "regeneration":
				return "quick to heal";
			case "sunblessed":
				return "sun-blessed";
			case "moonchild":
				return "moon-touched";
			case "nightchild":
				return "a child of the night";
			case "long_liver":
				return "long-lived";
			case "strong_minded":
				return "strong-minded";
			default:
				return Name(trait).ToLowerInvariant();
			}
		}

		internal static string Name(string trait)
		{
			try
			{
				string text = AssetManager.traits.get(trait)?.getTranslatedName();
				if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
				{
					return text;
				}
			}
			catch
			{
			}
			return trait.Replace('_', ' ');
		}

		private static IEnumerable<School> SchoolsOf(Actor a)
		{
			string job = null;
			string id = ((a.asset != null) ? a.asset.id : "");
			try
			{
				job = ((a.citizen_job != null) ? a.citizen_job.id : null);
			}
			catch
			{
			}
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = a.isWarrior();
				flag2 = a.army != null && a.army.getCaptain() == a;
			}
			catch
			{
			}
			if ((flag || flag2 || a.hasTrait("veteran")) && !a.hasTrait("pacifist"))
			{
				yield return School.Soldier;
			}
			if (a.hasTrait("block") || a.hasTrait("dodge") || a.hasTrait("backstep") || a.hasTrait("battle_reflexes"))
			{
				yield return School.Duelist;
			}
			if (job == "hunter" || a.hasTrait("eagle_eyed"))
			{
				yield return School.Hunter;
			}
			if (job == "miner" || job == "miner_deposit" || a.hasTrait("miner"))
			{
				yield return School.Miner;
			}
			if (id.Contains("mage") || id.Contains("druid") || id.Contains("necromancer") || a.hasTrait("heart_of_wizard"))
			{
				yield return School.Mage;
			}
			if (a.religion != null && (a.hasTrait("blessed") || (TempleIn(a.city) && a.getAge() >= 40)))
			{
				yield return School.Priest;
			}
			if (a.hasTrait("wise") || OldestIn(a))
			{
				yield return School.Elder;
			}
		}

		private static bool TempleIn(City c)
		{
			try
			{
				if (c == null)
				{
					return false;
				}
				foreach (Building building in c.buildings)
				{
					if (building != null && building.asset != null && building.asset.id.StartsWith("temple", StringComparison.Ordinal))
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

		private static bool OldestIn(Actor a)
		{
			try
			{
				if (a.city == null)
				{
					return false;
				}
				int age = a.getAge();
				if (age < 50)
				{
					return false;
				}
				foreach (Actor item in Civil.PeopleOf(a.city))
				{
					if (item != null && item != a && item.isAlive() && item.getAge() > age)
					{
						return false;
					}
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		internal static int Fee(Actor teacher, Actor you, string trait)
		{
			float num = 40f * (float)Worth(trait);
			try
			{
				bool num2 = (teacher.family != null && teacher.family == you.family) || you.isParentOf(teacher) || teacher.isParentOf(you) || teacher.lover == you;
				bool flag = teacher.kingdom != you.kingdom;
				if (num2)
				{
					num *= 0.5f;
				}
				else if (flag)
				{
					num *= 2f;
				}
				if (teacher.hasTrait("greedy"))
				{
					num *= 1.4f;
				}
				if (teacher.hasTrait("honest"))
				{
					num *= 0.9f;
				}
			}
			catch
			{
			}
			return Mathf.Max(10, Mathf.RoundToInt(num / 5f) * 5);
		}

		internal static string Refusal(Actor teacher, Actor you, string trait)
		{
			try
			{
				if (NeverTaught.Contains(trait))
				{
					return "That isn't taught. It's done.";
				}
				if (you.hasTrait(trait))
				{
					return "You have that already.";
				}
				if (teacher.kingdom != null && Law.BountyOn(teacher.kingdom, you) > 0)
				{
					return "I don't teach the wanted.";
				}
				if (Chronicle.Opinion(teacher, you) < 0)
				{
					return "I don't know you well enough to teach you anything.";
				}
				int level = you.data.level;
				int num = Worth(trait);
				if (level < num)
				{
					return $"You're not ready. Come back when you're more seasoned (level {num}).";
				}
			}
			catch
			{
			}
			return null;
		}

		internal static string Rumour(Actor npc, Actor you)
		{
			try
			{
				if (npc.city == null)
				{
					return null;
				}
				int day = Calendar.Day;
				System.Random random = new System.Random((int)((npc.data.id * 31 + day) & 0x7FFFFFFF));
				if (random.NextDouble() < 0.4)
				{
					return null;
				}
				List<Actor> list = new List<Actor>();
				foreach (Actor item in Civil.PeopleOf(npc.city))
				{
					if (item != null && item != npc && item != you && item.isAlive() && CanTeach(item).Exists((KeyValuePair<string, School> kv) => !you.hasTrait(kv.Key)))
					{
						list.Add(item);
					}
				}
				if (list.Count == 0)
				{
					return null;
				}
				Actor a = list[random.Next(list.Count)];
				List<KeyValuePair<string, School>> list2 = CanTeach(a).FindAll((KeyValuePair<string, School> kv) => !you.hasTrait(kv.Key));
				KeyValuePair<string, School> keyValuePair = list2[random.Next(list2.Count)];
				string text;
				switch (keyValuePair.Value)
				{
				case School.Soldier:
					text = "the old soldier";
					break;
				case School.Duelist:
					text = "who fights like a dancer";
					break;
				case School.Hunter:
					text = "the hunter";
					break;
				case School.Miner:
					text = "who works the rock";
					break;
				case School.Mage:
					text = "who knows a little magic";
					break;
				case School.Priest:
					text = "who keeps the faith";
					break;
				default:
					text = "our eldest";
					break;
				}
				return "If you want to learn something, " + Dialogue.NameOf(a) + ", " + text + ", could make you " + Word(keyValuePair.Key) + ". For a price, mind.";
			}
			catch
			{
				return null;
			}
		}

		internal static Actor Nearest(string trait, Vector2 at, float within)
		{
			Actor result = null;
			float num = within * within;
			try
			{
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					Actor actor = simpleList[i];
					if (actor != null && actor.isAlive() && actor.hasTrait(trait))
					{
						float sqrMagnitude = (actor.current_position - at).sqrMagnitude;
						if (!(sqrMagnitude >= num) && CanTeach(actor).Exists((KeyValuePair<string, School> kv) => kv.Key == trait))
						{
							result = actor;
							num = sqrMagnitude;
						}
					}
				}
			}
			catch
			{
			}
			return result;
		}
	}
}
