using System;
using System.Collections;
using FirstPerson.Towns;

namespace FirstPerson
{
	internal sealed class SettlersTest : CivilTest
	{
		protected override string Prefix => "SETTLERS";

		protected override IEnumerator Body()
		{
			yield return Part("land for every people", Land());
			yield return Part("sent to settle: yes, then no", Rounds());
		}

		private IEnumerator Land()
		{
			int num = 0;
			int num2 = 0;
			foreach (ArchitectureAsset item in AssetManager.architecture_library.list)
			{
				if (item?.building_ids_for_construction != null && item.building_ids_for_construction.ContainsKey("order_house_0"))
				{
					num++;
					if (LandPlot.SiteOf(item) == null)
					{
						num2++;
						Note("no site for " + item.id);
					}
				}
			}
			Check("a house site for every people that builds houses", num > 10 && num2 == 0, $"{num} sets, {num2} without");
			ArchitectureAsset arch = AssetManager.architecture_library.get("civ_cat");
			Check("a cat town's site is its house", LandPlot.SiteOf(arch)?.id == "house_civ_cat_0", LandPlot.SiteOf(arch)?.id);
			ArchitectureAsset arch2 = AssetManager.architecture_library.get("human");
			Check("a human town's site is still a tent", LandPlot.SiteOf(arch2)?.id == "tent_human", LandPlot.SiteOf(arch2)?.id);
			yield break;
		}

		private IEnumerator Rounds()
		{
			Actor actor = Village("human", 8, 0);
			if (!Check("a village", actor != null && actor.city != null))
			{
				yield break;
			}
			Kingdom k = actor.kingdom;
			City home = actor.city;
			Actor you = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAlive() && item.isAdult() && !item.isCityLeader())
				{
					you = item;
					break;
				}
			}
			if (!Check("someone of it to be", you != null))
			{
				yield break;
			}
			Actor actor2 = null;
			float[] array = new float[4] { 30f, 15f, 60f, 100f };
			foreach (float minAway in array)
			{
				if ((actor2 = Village("human", 2, 0, home.getTile(), minAway)) != null)
				{
					break;
				}
			}
			if (!Check("a second village", actor2 != null && actor2.city != null))
			{
				yield break;
			}
			City fresh = actor2.city;
			try
			{
				fresh.joinAnotherKingdom(k);
			}
			catch (Exception ex)
			{
				Note("joinAnotherKingdom: " + ex.Message);
			}
			yield return CivilTest.Wait(0.5f);
			if (Check("both towns of one realm", fresh.kingdom == k, fresh.kingdom?.name))
			{
				yield return Possess(you);
				if (Check("first person", InControl(you)))
				{
					Check("the guard is in", SettlerOffer.Installed);
					yield return Round(you, home, fresh, yes: true);
					yield return Round(you, fresh, home, yes: false);
				}
			}
		}

		private IEnumerator Round(Actor you, City from, City to, bool yes)
		{
			new BehKingCheckNewCityFoundation().moveSomeUnitsToNewCity(to, from);
			City to2;
			bool flag = SettlerOffer.PendingForTest(out to2);
			Check("sent to " + to.name + ": you're asked, not moved", flag && to2 == to && you.city == from, $"pending {flag}, to {to2?.name}, you in {you.city?.name}");
			yield return CivilTest.Wait(2f);
			Check("...and still home while it waits", you.city == from);
			SettlerOffer.AnswerForTest(yes);
			yield return CivilTest.Wait(1f);
			if (yes)
			{
				Check("yes: you settle there", you.city == to, you.city?.name);
			}
			else
			{
				Check("no: you stay home", you.city == from && !SettlerOffer.PendingForTest(out var _), you.city?.name);
			}
		}
	}
}
