using System;
using System.Collections;

namespace FirstPerson
{
	internal sealed class SuccessionTest : CivilTest
	{
		protected override string Prefix => "SUCCESSION";

		protected override IEnumerator Body()
		{
			CrownOffer.AutoAnswer = true;
			try
			{
				yield return Part("asked, and yes", Round(yes: true));
				yield return Part("asked, and no", Round(yes: false));
			}
			finally
			{
				CrownOffer.AutoAnswer = false;
			}
		}

		private IEnumerator Round(bool yes)
		{
			Actor king = Village("human", 6, 0, null, yes ? 0f : 40f);
			if (!Check("a village", king != null && king.city != null))
			{
				yield break;
			}
			Kingdom k = king.kingdom;
			City town = king.city;
			Actor you = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item != king && item.isAlive() && item.isAdult())
				{
					you = item;
					break;
				}
			}
			if (!Check("someone of it to be", you != null))
			{
				yield break;
			}
			yield return Possess(you);
			if (!Check("first person", InControl(you)))
			{
				yield break;
			}
			Check("the guard is in", CrownGuard.Installed);
			try
			{
				if (town.leader != null && town.leader != you)
				{
					town.removeLeader();
				}
				town.setLeader(you, pNew: true);
				k.data.royal_clan_id = -1L;
			}
			catch (Exception ex)
			{
				Note("setup: " + ex.Message);
			}
			Check("you lead the realm's only town", you.isCityLeader() && k.countCities() == 1);
			int asked = CrownGuard.Asked;
			CivilTest.Kill(king);
			bool flag = false;
			bool crown = false;
			for (float t = 0f; t < 25f; t += 0.25f)
			{
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				flag = CrownOffer.PendingForTest(out crown);
				if (you.isKing())
				{
					break;
				}
			}
			Check("the king dies and you're asked, not crowned", flag && crown && !you.isKing() && CrownGuard.Asked > asked, $"pending {flag}, crown {crown}, king {you.isKing()}");
			yield return CivilTest.Wait(3f);
			Check("...the throne waits for your answer", !you.isKing() && !k.hasKing(), "king " + (k.hasKing() ? WorldBoxMod.Describe(k.king) : "none"));
			Actor alt = CrownOffer.PendingAltForTest;
			Note("the herald names " + ((alt != null) ? WorldBoxMod.Describe(alt) : "no one else"));
			CrownOffer.AnswerForTest(yes);
			yield return CivilTest.Wait(3f);
			if (yes)
			{
				Check("yes: you're crowned", you.isKing() && k.king == you, "king " + (k.hasKing() ? WorldBoxMod.Describe(k.king) : "none"));
			}
			else
			{
				yield return CivilTest.Wait(5f);
				Check("no: you're not crowned", !you.isKing(), "king " + (k.hasKing() ? WorldBoxMod.Describe(k.king) : "none"));
				Check("...nor asked again for this throne", !CrownOffer.PendingForTest(out var _));
				if (alt != null)
				{
					for (float t = 0f; t < 20f; t += 0.5f)
					{
						if (k.hasKing())
						{
							break;
						}
						yield return CivilTest.Wait(0.5f);
					}
					Check("...the crown goes to the next in your town, and you keep its seat", k.hasKing() && k.king != you && you.isCityLeader(), string.Format("king {0}, you lead {1}", k.hasKing() ? WorldBoxMod.Describe(k.king) : "none", you.isCityLeader()));
				}
				else
				{
					string text = "";
					try
					{
						foreach (Actor unit in town.units)
						{
							text += $"{WorldBoxMod.Describe(unit)} adult {unit.isAdult()} citizen {unit.is_profession_citizen} clan {unit.hasClan()}; ";
						}
					}
					catch
					{
					}
					Note("no one else to crown: " + text);
					yield return CivilTest.Wait(5f);
					Check("...no one else to take it: the throne stands empty, as the herald said, and you keep the town", !k.hasKing() && you.isCityLeader(), string.Format("king {0}, you lead {1}", k.hasKing() ? WorldBoxMod.Describe(k.king) : "none", you.isCityLeader()));
				}
			}
			Mod.SaveFrame("succession_" + (yes ? "yes" : "no") + ".png");
		}
	}
}
