using System;

namespace FirstPerson
{
	internal static class Belong
	{
		public static string Refusal(Actor npc, Actor you, City city)
		{
			try
			{
				Kingdom kingdom = city.kingdom;
				Kingdom kingdom2 = you.kingdom;
				if (you.isBaby() || !you.isAdult())
				{
					return "Come back when you're grown.";
				}
				string species = city.getSpecies();
				if (!string.IsNullOrEmpty(species) && you.asset != null && species != you.asset.id)
				{
					return Dialogue.Pick(city.name + " is no place for your kind.", "*looks you up and down* We keep to our own here.");
				}
				if (kingdom != null && kingdom2 != null && kingdom2 != kingdom && kingdom2.isCiv() && Royal.AtWar(kingdom2, kingdom))
				{
					return "You're of " + kingdom2.name + ", and we're at war with them. Leave them first, and then we'll talk.";
				}
				if (kingdom != null && Law.BountyOn(kingdom, you) > 0)
				{
					return "There's a price on your head here. Pay your fine first.";
				}
				if (Chronicle.Opinion(npc, you) < -15)
				{
					return Dialogue.Pick("After what you've done? No.", "*crosses arms* I don't trust you. Not here.");
				}
				return null;
			}
			catch (Exception ex)
			{
				Log.Error("belong: refusal: " + ex.Message);
				return "*hesitates* Not today, I think.";
			}
		}

		public static bool Leave(Actor you)
		{
			try
			{
				string text = ((you.city != null) ? you.city.name : null);
				you.removeFromPreviousFaction();
				try
				{
					you.clearHomeBuilding();
				}
				catch
				{
				}
				if (you.isKingdomCiv())
				{
					you.setDefaultKingdom();
				}
				if (text != null)
				{
					Chronicle.AddOwnMemory(you, "fp_left", text);
				}
				Log.Info("belong: " + WorldBoxMod.Describe(you) + " left " + (text ?? "nowhere") + "; now of " + (you.kingdom?.name ?? "no kingdom"));
				return you.city == null;
			}
			catch (Exception ex)
			{
				Log.Error("belong: leave: " + ex);
				return false;
			}
		}

		public static bool Join(Actor you, City city)
		{
			try
			{
				if (city == null || !city.isAlive())
				{
					return false;
				}
				if (you.kingdom != null && you.kingdom == city.kingdom)
				{
					try
					{
						you.stopBeingWarrior();
					}
					catch
					{
					}
				}
				else if (you.city != null || you.isKing())
				{
					you.removeFromPreviousFaction();
				}
				try
				{
					you.clearHomeBuilding();
				}
				catch
				{
				}
				you.joinCity(city);
				Chronicle.AddOwnMemory(you, "fp_joined", city.name);
				Log.Info("belong: " + WorldBoxMod.Describe(you) + " joined " + city.name + " of " + city.kingdom?.name);
				return you.city == city;
			}
			catch (Exception ex)
			{
				Log.Error("belong: join: " + ex);
				return false;
			}
		}
	}
}
