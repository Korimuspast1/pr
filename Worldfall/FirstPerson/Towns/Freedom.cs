using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Freedom
	{
		public const RepLevel Needs = RepLevel.Honored;

		public const int RebelLoss = 1000;

		public static City HomeTown(Actor you)
		{
			try
			{
				City city = you?.city;
				return (city != null && city.isAlive() && Rep.Civ(city.kingdom)) ? city : null;
			}
			catch
			{
				return null;
			}
		}

		public static string WhyNot(Actor you, City town)
		{
			if (you == null || town == null)
			{
				return "You have no town";
			}
			Kingdom kingdom;
			try
			{
				kingdom = town.kingdom;
			}
			catch
			{
				return "You have no town";
			}
			if (!Rep.Civ(kingdom))
			{
				return "Your town has no realm";
			}
			if (Royal.IsKing(you))
			{
				return "You already wear a crown";
			}
			try
			{
				if (kingdom.capital == town)
				{
					return "Not the capital";
				}
			}
			catch
			{
			}
			try
			{
				if (kingdom.countCities() < 2)
				{
					return "It is the realm's last town";
				}
			}
			catch
			{
			}
			if (!Rep.AtLeast(you, town, RepLevel.Honored))
			{
				return Rep.Name(RepLevel.Honored) + " in " + (Rep.NameOf(town) ?? "your town");
			}
			return null;
		}

		public static void Choices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			City town = HomeTown(you);
			if (town == null || with == null)
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = with.isKing() && with.kingdom == town.kingdom;
			}
			catch
			{
			}
			if (flag && !Royal.IsKing(you))
			{
				Choice choice = new Choice("I'd buy " + Rep.NameOf(town) + "'s freedom.", delegate
				{
					Ask(c, town);
				}, ChoiceTone.Risk);
				string text = WhyNot(you, town);
				if (text == null && Rep.Standing(you, town.kingdom) <= RepLevel.Outlaw)
				{
					text = "The crown calls you an outlaw";
				}
				if (text != null)
				{
					choice.Off(text);
				}
				list.Add(choice);
			}
		}

		public static int Price(City town, Actor king, Actor you)
		{
			int num = 0;
			int num2 = 0;
			try
			{
				num = town.getPopulationPeople();
			}
			catch
			{
			}
			try
			{
				num2 = town.buildings.Count;
			}
			catch
			{
			}
			Tier tier = TownTier.Of(town);
			float num3 = 600f + (float)num * 8f + (float)num2 * 10f;
			float num4;
			switch (tier)
			{
			default:
				num4 = 1f;
				break;
			case Tier.Town:
				num4 = 1.4f;
				break;
			case Tier.City:
				num4 = 1.8f;
				break;
			}
			float num5 = num3 * num4;
			if (Dialogue.HasTrait(king, "greedy"))
			{
				num5 *= 0.85f;
			}
			if (Dialogue.HasTrait(king, "ambitious"))
			{
				num5 *= 1.25f;
			}
			if (Rep.Level(you, town) >= RepLevel.Champion)
			{
				num5 *= 0.85f;
			}
			try
			{
				num5 *= 1f - 0.06f * (float)Dialogue.VoiceOf(king, you).Warmth;
			}
			catch
			{
			}
			return Mathf.Max(500, Mathf.RoundToInt(num5 / 50f) * 50);
		}

		private static void Ask(Conversation c, City town)
		{
			c.LastTopic = "freedom_ask";
			Actor with = c.With;
			Actor you = c.You;
			if (WhyNot(you, town) != null)
			{
				c.Say("Not now.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			int price = Price(town, with, you);
			string text = Rep.NameOf(town);
			c.Say(Dialogue.HasTrait(with, "greedy") ? Dialogue.Pick($"{text}? Everything has a price. {price} gold, and it's yours.", $"*his eyes narrow* {price} gold. Not a coin less.") : Dialogue.Pick($"You'd take {text} from my realm? They do love you there. {price} gold, and I'll let it go in peace.", $"A crown of your own. {price} gold buys {text}'s freedom, and my word that I won't come for it."));
			int num = Economy.Money(you);
			Choice choice = new Choice($"Pay {price} gold", delegate
			{
				Buy(c, town, price);
			}, ChoiceTone.Risk);
			if (num < price)
			{
				choice.Off($"You have {num} gold");
			}
			c.Offer(new List<Choice>
			{
				choice,
				new Choice("Too rich for me.", delegate
				{
					c.Say(Dialogue.Pick("As you like.", "Then we're done."));
					c.Offer(Dialogue.Topics(c));
				})
			});
		}

		private static void Buy(Conversation c, City town, int price)
		{
			c.LastTopic = "freedom_bought";
			Actor with = c.With;
			Actor you = c.You;
			Kingdom kingdom = null;
			try
			{
				kingdom = town.kingdom;
			}
			catch
			{
			}
			if (WhyNot(you, town) != null || kingdom == null || with == null || !with.isAlive() || kingdom.king != with)
			{
				c.Say("Things have changed. The offer is gone.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			if (Economy.Money(you) < price)
			{
				c.Say("Come back when you have the gold.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			City city = null;
			try
			{
				city = kingdom.capital;
			}
			catch
			{
			}
			Kingdom kingdom2 = MakeRealm(you, town, rebel: false);
			if (kingdom2 == null)
			{
				c.Say("Something's wrong. Keep your gold.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			try
			{
				you.spendMoney(price);
				with.addMoney(price / 2);
				if (city == null || !city.isAlive() || !Economy.Deposit(city, price - price / 2))
				{
					with.addMoney(price - price / 2);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("freedom pay", e);
			}
			try
			{
				World.world.diplomacy.getRelation(kingdom, kingdom2).data.timestamp_last_war_ended = World.world.getCurWorldTime();
			}
			catch
			{
			}
			Chronicle.ChangeOpinion(with, you, 10);
			c.Toast?.Invoke(Rep.NameOf(town) + " is free. You are its king");
			Sounds.PlayAt("event:/SFX/UI/YesPoint", you.current_position);
			c.Say(Dialogue.Pick("*counts the coins* Done. " + Rep.NameOf(town) + " is yours. Rule it well, Your Majesty.", "Then go, king of " + Rep.NameOf(town) + ". May our realms stay friends."));
			c.Offer(Dialogue.Topics(c));
		}

		public static bool Declare(Actor you, Action<string> toast)
		{
			City city = HomeTown(you);
			string text = WhyNot(you, city);
			if (text != null)
			{
				toast?.Invoke(text);
				return false;
			}
			Kingdom kingdom = city.kingdom;
			string text2 = Royal.NameOf(kingdom);
			Kingdom kingdom2 = MakeRealm(you, city, rebel: true);
			if (kingdom2 == null)
			{
				toast?.Invoke("The town would not rise");
				return false;
			}
			try
			{
				try
				{
					World.world.diplomacy.startWar(kingdom, kingdom2, WarTypeLibrary.rebellion, pLog: true);
				}
				catch (NullReferenceException)
				{
					if (!Royal.AtWar(kingdom, kingdom2))
					{
						World.world.diplomacy.startWar(kingdom, kingdom2, WarTypeLibrary.rebellion, pLog: false);
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("independence war", e);
			}
			Rep.AddStanding(you, kingdom, -1000, "declared independence");
			bool flag = Royal.AtWar(kingdom, kingdom2);
			toast?.Invoke(flag ? (Rep.NameOf(city) + " declares independence. " + text2 + " marches on you") : (Rep.NameOf(city) + " declares independence"));
			Sounds.PlayUI("event:/SFX/UI/YesPoint");
			Log.Info("freedom: " + Rep.NameOf(city) + " declared independence from " + text2 + (flag ? ", at war" : ", no war"));
			return true;
		}

		public static Kingdom MakeRealm(Actor you, City town, bool rebel)
		{
			if (you == null || town == null)
			{
				return null;
			}
			Kingdom kingdom = null;
			try
			{
				if (you.city != town)
				{
					you.joinCity(town);
				}
				kingdom = town.makeOwnKingdom(you, rebel, pFellApart: false);
				if (kingdom != null && kingdom.king != you)
				{
					kingdom.setKing(you);
				}
				if (you.city != town)
				{
					you.joinCity(town);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("new realm", e);
			}
			bool flag = false;
			try
			{
				flag = kingdom != null && kingdom.isAlive() && kingdom.king == you && town.kingdom == kingdom;
			}
			catch
			{
			}
			Log.Info("freedom: " + Rep.NameOf(town) + " " + (flag ? ("is now " + Royal.NameOf(kingdom) + ", " + WorldBoxMod.Describe(you) + " its king") : "could not become a realm"));
			if (!flag)
			{
				return null;
			}
			return kingdom;
		}
	}
}
