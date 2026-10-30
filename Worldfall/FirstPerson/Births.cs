using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Births
	{
		public static readonly string[] PregnantStatuses = new string[2] { "pregnant", "pregnant_parthenogenesis" };

		public static float FadeLeft;

		public const float FadeSeconds = 3.2f;

		public static Actor Naming;

		public static string NameText = "";

		public static bool NameFocus;

		private static readonly HashSet<long> KnownChildren = new HashSet<long>();

		private static long _knownFor;

		private static object _knownWorld;

		private static float _scanTimer;

		private static readonly List<Actor> Scratch = new List<Actor>();

		public static string LastResult;

		public static bool AnyPlaceForTest;

		public static int BelliesDrawn;

		private static readonly string[] AloneTraits = new string[5] { "reproduction_budding", "reproduction_fission", "reproduction_vegetative", "reproduction_parthenogenesis", "reproduction_spores" };

		private static long _namingId = -1L;

		private static Actor _namingObj;

		public static bool IsExpecting(Actor a)
		{
			if (a == null)
			{
				return false;
			}
			try
			{
				if (!((BaseSimObject)a).hasAnyStatusEffect())
				{
					return false;
				}
				string[] pregnantStatuses = PregnantStatuses;
				foreach (string pID in pregnantStatuses)
				{
					if (((BaseSimObject)a).hasStatus(pID))
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

		private static Status PregnancyOf(Actor a)
		{
			try
			{
				if (!((BaseSimObject)a).hasAnyStatusEffect())
				{
					return null;
				}
				IReadOnlyDictionary<string, Status> statusesDict = a.getStatusesDict();
				string[] pregnantStatuses = PregnantStatuses;
				foreach (string key in pregnantStatuses)
				{
					if (statusesDict.TryGetValue(key, out var value) && value != null && !value.is_finished)
					{
						return value;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		public static float Progress(Actor a)
		{
			Status status = PregnancyOf(a);
			if (status == null)
			{
				return -1f;
			}
			try
			{
				float num = Mathf.Max(0.1f, status.duration);
				float num2 = (float)status.getRemainingTime();
				return Mathf.Clamp01(1f - num2 / num);
			}
			catch
			{
				return 0.5f;
			}
		}

		public static float MonthsLeft(Actor a)
		{
			Status status = PregnancyOf(a);
			if (status == null)
			{
				return -1f;
			}
			try
			{
				return Mathf.Max(0f, (float)status.getRemainingTime() / 5f);
			}
			catch
			{
				return -1f;
			}
		}

		public static string ExpectingLabel(Actor a)
		{
			float num = MonthsLeft(a);
			if (num < 0f)
			{
				return null;
			}
			int num2 = Mathf.Max(0, Mathf.RoundToInt(num));
			if (num2 >= 15)
			{
				int num3 = Mathf.RoundToInt(num / 12f);
				if (num3 > 1)
				{
					return $"Expecting, about {num3} years to go";
				}
				return "Expecting, about a year to go";
			}
			if (num2 > 0)
			{
				if (num2 != 1)
				{
					return $"Expecting, about {num2} months";
				}
				return "Expecting, about a month";
			}
			return "Expecting, any day now";
		}

		public static string ExpectingGreeting(Actor npc, Actor you)
		{
			string text = When(MonthsLeft(npc));
			bool flag = false;
			try
			{
				flag = npc.lover == you;
			}
			catch
			{
			}
			if (flag)
			{
				return Dialogue.Pick("*puts your hand on the belly* Feel that? Our little one comes " + text + ".", "Our baby comes " + text + "! I can hardly wait.");
			}
			return Dialogue.Pick("*rests a hand on the belly* The baby comes " + text + ".", "I'm expecting! " + Cap(text) + ".", "Another little mouth to feed, " + text + ".");
		}

		public static string PartnerGreeting(Actor you)
		{
			string text = When(MonthsLeft(you));
			return Dialogue.Pick("How are you feeling? Our baby comes " + text + ".", "*puts a hand on your belly* Our little one comes " + text + ". I can hardly wait.", "Sit, sit! You and the baby need your rest.");
		}

		private static string When(float months)
		{
			if (months < 0.6f)
			{
				return "any day now";
			}
			if (months < 1.5f)
			{
				return "in about a month";
			}
			if (months < 15f)
			{
				return $"in about {Mathf.RoundToInt(months)} months";
			}
			int num = Mathf.RoundToInt(months / 12f);
			if (num > 1)
			{
				return $"in about {num} years";
			}
			return "in about a year";
		}

		private static string Cap(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return char.ToUpper(s[0]) + s.Substring(1);
			}
			return s;
		}

		public static void Choices(Conversation c, List<Choice> list)
		{
			Actor you = c.You;
			Actor with = c.With;
			bool flag = false;
			try
			{
				flag = with.lover == you && you.lover == with;
			}
			catch
			{
			}
			if (flag)
			{
				bool adopt;
				string text = WhyNot(you, with, out adopt);
				Choice choice = new Choice("Let's have a child.", delegate
				{
					MakeChild(c);
				}, ChoiceTone.Romance);
				if (text != null)
				{
					choice.Off(text);
				}
				list.Add(choice);
				if (adopt || text != null)
				{
					Choice choice2 = new Choice("Let's take in an orphan.", delegate
					{
						Adopt(c);
					}, ChoiceTone.Romance);
					if (FindOrphan(you, 80f) == null)
					{
						choice2.Off("There are no orphans nearby.");
					}
					list.Add(choice2);
				}
			}
			if (!ReproducesAlone(you))
			{
				return;
			}
			Choice choice3 = new Choice("I'm ready to bring a new one into the world.", delegate
			{
				Alone(c);
			}, ChoiceTone.Romance);
			string text2 = null;
			try
			{
				if (!you.isAdult())
				{
					text2 = "You are too young.";
				}
				else if (!BabyHelper.canMakeBabies(you))
				{
					text2 = "You can't bring forth new life right now.";
				}
				else if (BabyHelper.isMetaLimitsReached(you))
				{
					text2 = "There is no room for another (the town or the world is full).";
				}
			}
			catch
			{
				text2 = "Not now.";
			}
			if (text2 != null)
			{
				choice3.Off(text2);
			}
			list.Add(choice3);
		}

		public static string WhyNot(Actor you, Actor lover, out bool adopt)
		{
			adopt = false;
			try
			{
				if (lover == null)
				{
					return "You need a lover for that.";
				}
				Actor actor = (Models.IsFemale(you) ? you : (Models.IsFemale(lover) ? lover : null));
				bool flag = false;
				try
				{
					flag = you.subspecies != null && (you.subspecies.needOppositeSexTypeForReproduction() || you.subspecies.hasTraitReproductionSexual());
				}
				catch
				{
				}
				if (flag && Models.IsFemale(you) == Models.IsFemale(lover))
				{
					adopt = true;
					return "The two of you can't have a child together.";
				}
				if (actor == null)
				{
					actor = you;
				}
				if (you.hasTrait("infertile") || lover.hasTrait("infertile"))
				{
					adopt = true;
					return "One of you can't have children.";
				}
				if (IsExpecting(actor))
				{
					return "A baby is already on the way.";
				}
				if (!you.isAdult() || !lover.isAdult())
				{
					return "Not until you're both grown.";
				}
				if (!you.isSameSpecies(lover))
				{
					adopt = true;
					return "You're not the same kind.";
				}
				if (!BabyHelper.canMakeBabies(actor))
				{
					return actor.hasReachedOffspringLimit() ? "You already have all the children you can." : "Not now: you're too hungry, or not ready.";
				}
				if (BabyHelper.isMetaLimitsReached(actor))
				{
					return "There's no room for another: your town needs a free house.";
				}
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (instance != null && !instance.AtHomeAtNight && !AnyPlaceForTest)
				{
					return "Only at home, at night.";
				}
			}
			catch (Exception ex)
			{
				return "Not now (" + ex.GetType().Name + ").";
			}
			return null;
		}

		private static void MakeChild(Conversation c)
		{
			c.LastTopic = "child";
			Actor you = c.You;
			Actor with = c.With;
			bool adopt;
			string text = WhyNot(you, with, out adopt);
			if (text != null)
			{
				c.Say(text);
				c.Offer(Dialogue.Topics(c));
				return;
			}
			FadeLeft = 3.2f;
			int num = CountChildren(you);
			bool flag = Conceive(you);
			Actor actor = (Models.IsFemale(you) ? you : with);
			if (IsExpecting(actor))
			{
				LastResult = "pregnant";
				c.Say((actor == you) ? "*the night passes* ...You feel something new stirring inside you." : "*the night passes* Later, a quiet whisper: \"I think... we're going to have a baby.\"");
				c.Romantic = true;
				c.Hearts?.Invoke(with);
				Chronicle.ChangeOpinion(with, you, 10);
			}
			else if (CountChildren(you) > num)
			{
				LastResult = "born";
				c.Say("*the night passes* By morning, there's a little one with you!");
				c.Romantic = true;
			}
			else
			{
				LastResult = (flag ? "nothing" : "refused");
				c.Say("*the night passes* ...Not this time. There's always tomorrow.");
			}
			c.Offer(Dialogue.Topics(c));
		}

		public static bool Conceive(Actor you)
		{
			try
			{
				new BehCheckForBabiesFromSexualReproduction().execute(you);
				Log.Info(string.Format("births: lovers {0} + {1}: expecting {2}", WorldBoxMod.Describe(you), (you.lover != null) ? WorldBoxMod.Describe(you.lover) : "-", IsExpecting(you) || IsExpecting(you.lover)));
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("births: could not run the lovers' behaviour: " + ex);
				return false;
			}
		}

		private static int CountChildren(Actor a)
		{
			try
			{
				return a.current_children_count;
			}
			catch
			{
				return 0;
			}
		}

		public static Actor FindOrphan(Actor you, float range)
		{
			Actor result = null;
			float num = range * range;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple == null || simple == you || !simple.isAlive() || simple.isAdult() || simple.asset != you.asset)
					{
						continue;
					}
					long parent_id_ = simple.data.parent_id_1;
					long parent_id_2 = simple.data.parent_id_2;
					if (Chronicle.Find(parent_id_) == null && Chronicle.Find(parent_id_2) == null)
					{
						float sqrMagnitude = (simple.current_position - you.current_position).sqrMagnitude;
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

		private static void Adopt(Conversation c)
		{
			c.LastTopic = "adopt";
			Actor you = c.You;
			Actor with = c.With;
			Actor actor = FindOrphan(you, 80f);
			if (actor == null)
			{
				c.Say("I asked around. There are no orphans near here.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			if (Adopt(you, with, actor))
			{
				c.Say(Dialogue.NameOf(actor) + " needs a home. From today, " + (Models.IsFemale(actor) ? "she" : "he") + "'s ours.");
				c.Romantic = true;
				c.Hearts?.Invoke(with);
				c.Toast?.Invoke("You took in " + Dialogue.NameOf(actor));
			}
			else
			{
				c.Say("It didn't work out. Maybe another time.");
			}
			c.Offer(Dialogue.Topics(c));
		}

		public static bool Adopt(Actor you, Actor lover, Actor child)
		{
			try
			{
				child.setParent1(you);
				if (lover != null)
				{
					child.setParent2(lover);
				}
				if (you.family != null)
				{
					child.setFamily(you.family);
				}
				try
				{
					child.changeHappiness("just_found_house");
				}
				catch
				{
				}
				Chronicle.AddOwnMemory(you, "fp_adopted", Dialogue.NameOf(child));
				if (lover != null)
				{
					Chronicle.AddOwnMemory(lover, "fp_adopted", Dialogue.NameOf(child));
				}
				Chronicle.Capture(child);
				Log.Info("births: " + WorldBoxMod.Describe(child) + " adopted by " + WorldBoxMod.Describe(you) + ((lover != null) ? (" and " + WorldBoxMod.Describe(lover)) : ""));
				LastResult = "adopted";
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("births: adoption failed: " + ex.Message);
				return false;
			}
		}

		public static bool ReproducesAlone(Actor a)
		{
			try
			{
				string[] aloneTraits = AloneTraits;
				foreach (string pTraitID in aloneTraits)
				{
					if (a.hasSubspeciesTrait(pTraitID))
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

		private static void Alone(Conversation c)
		{
			c.LastTopic = "bud";
			Actor you = c.You;
			Actor actor = BringForth(you);
			if (actor != null)
			{
				c.Say(Dialogue.Pick("*watches, wide-eyed* A new little one! Look at it!", "*gasps* It's... beautiful? I think?"));
				c.Toast?.Invoke("A new life: " + Dialogue.NameOf(actor));
			}
			else
			{
				c.Say(IsExpecting(you) ? "*nods* It has begun. It will take a while." : "Nothing happened. Perhaps you're not ready.");
			}
			c.Offer(Dialogue.Topics(c));
		}

		public static Actor BringForth(Actor you)
		{
			try
			{
				Actor actor = null;
				if (you.hasSubspeciesTrait("reproduction_budding"))
				{
					actor = BabyMaker.makeBabyViaBudding(you);
				}
				else if (you.hasSubspeciesTrait("reproduction_fission"))
				{
					actor = BabyMaker.makeBabyViaFission(you);
				}
				else if (you.hasSubspeciesTrait("reproduction_vegetative"))
				{
					actor = BabyMaker.makeBabyViaVegetative(you);
				}
				else if (you.hasSubspeciesTrait("reproduction_parthenogenesis"))
				{
					BabyMaker.makeBabyViaParthenogenesis(you);
				}
				else if (you.hasSubspeciesTrait("reproduction_spores"))
				{
					BabyMaker.spawnSporesFor(you);
				}
				LastResult = ((actor != null) ? "alone" : (IsExpecting(you) ? "pregnant" : "nothing"));
				Log.Info("births: " + WorldBoxMod.Describe(you) + " alone -> " + LastResult);
				return actor;
			}
			catch (Exception ex)
			{
				Log.Error("births: alone: " + ex.Message);
				return null;
			}
		}

		public static void Update(Actor you, float dt, Action<string> toast)
		{
			if (FadeLeft > 0f)
			{
				FadeLeft = Mathf.Max(0f, FadeLeft - dt);
			}
			float num = ((you != null) ? Progress(you) : (-1f));
			PossessionHooks.SprintCap = ((num > 0.55f) ? Mathf.Lerp(1.35f, 0.95f, Mathf.InverseLerp(0.55f, 1f, num)) : 1.35f);
			if (you == null)
			{
				return;
			}
			if (Naming != null && Naming != _namingObj)
			{
				_namingObj = Naming;
				try
				{
					_namingId = Naming.data.id;
				}
				catch
				{
				}
			}
			if (Naming != null && (!Naming.isAlive() || Naming.data == null || Naming.data.id != _namingId))
			{
				Naming = null;
			}
			_scanTimer -= dt;
			if (_scanTimer > 0f)
			{
				return;
			}
			_scanTimer = 0.5f;
			long id = you.data.id;
			object obj2 = null;
			try
			{
				obj2 = World.world?.map_stats;
			}
			catch
			{
			}
			if (_knownFor != id || obj2 != _knownWorld)
			{
				_knownFor = id;
				_knownWorld = obj2;
				KnownChildren.Clear();
				{
					foreach (Actor item in ChildrenOf(you))
					{
						KnownChildren.Add(item.data.id);
					}
					return;
				}
			}
			foreach (Actor item2 in ChildrenOf(you))
			{
				if (!KnownChildren.Contains(item2.data.id))
				{
					bool flag = false;
					try
					{
						flag = item2.isBaby() && item2.getAge() <= 1;
					}
					catch
					{
					}
					if (!flag)
					{
						KnownChildren.Add(item2.data.id);
					}
					else if (Naming == null)
					{
						KnownChildren.Add(item2.data.id);
						Naming = item2;
						_namingObj = item2;
						_namingId = item2.data.id;
						NameText = Dialogue.NameOf(item2);
						NameFocus = true;
						toast("A child is born to you! Give " + (Models.IsFemale(item2) ? "her" : "him") + " a name");
						Log.Info("births: newborn " + WorldBoxMod.Describe(item2) + " of " + WorldBoxMod.Describe(you));
					}
				}
			}
		}

		private static List<Actor> ChildrenOf(Actor a)
		{
			Scratch.Clear();
			try
			{
				long id = a.data.id;
				Actor lover = a.lover;
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && (simple.data.parent_id_1 == id || simple.data.parent_id_2 == id) && (simple.asset == a.asset || (lover != null && simple.asset == lover.asset)))
					{
						Scratch.Add(simple);
					}
				}
			}
			catch
			{
			}
			return Scratch;
		}

		public static void FinishNaming(Actor you, bool keep)
		{
			Actor naming = Naming;
			Naming = null;
			NameFocus = false;
			if (naming == null || !naming.isAlive())
			{
				return;
			}
			string text = (NameText ?? "").Trim();
			if (!keep && text.Length > 0 && text.Length <= 24)
			{
				try
				{
					naming.setName(text);
					naming.data.custom_name = true;
				}
				catch (Exception ex)
				{
					Log.Error("births: could not name the baby: " + ex.Message);
				}
			}
			string text2 = Dialogue.NameOf(naming);
			Chronicle.AddOwnMemory(you, "fp_named", text2);
			try
			{
				if (you.lover != null && naming.isChildOf(you.lover))
				{
					Chronicle.AddOwnMemory(you.lover, "fp_named", text2);
				}
			}
			catch
			{
			}
			Chronicle.Capture(naming);
			WorldBoxMod.Instance?.Toast("Welcome to the world, " + text2 + "!");
			Log.Info("births: named " + text2);
		}
	}
}
