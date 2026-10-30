using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FirstPerson
{
	internal sealed class GreatWarTest : CivilTest
	{
		protected override string Prefix => "GW";

		protected override IEnumerator Body()
		{
			string text = Path.Combine(Log.Folder, "runs_gwtest.txt");
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
			Run.HallFileForTest = text;
			Run.Forget();
			Actor me = Village("human", 8, 0);
			if (!Check("your realm", me != null))
			{
				yield break;
			}
			Kingdom mine = me.kingdom;
			WorldTile home = me.city.getTile();
			Actor small = Village("human", 3, 0, home, 40f);
			Kingdom little = small?.kingdom;
			Actor big = Village("human", 34, 0, home, 40f, small?.city?.getTile());
			Kingdom rival = big?.kingdom;
			if (!Check("a small realm and a big one", little != null && rival != null && little != mine && rival != mine && little != rival))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			try
			{
				((BaseSimObject)me).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(me);
			if (!Check("first person, as the king", InControl(me) && me.isKing()))
			{
				yield break;
			}
			Run.Picked.Clear();
			Check("locked in", Run.Lock(me, Run.Picked, base.Note));
			try
			{
				World.world.diplomacy.startWar(mine, little, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			try
			{
				World.world.diplomacy.startWar(mine, rival, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex2)
			{
				Note("war: " + ex2.Message);
			}
			yield return CivilTest.Wait(2.5f);
			List<GreatWar.Rival> rivalsForTest = GreatWar.RivalsForTest;
			Note("noted: " + string.Join(", ", rivalsForTest.Select((GreatWar.Rival r) => $"{r.Name} ({r.Towns} towns, {r.People} people, real {r.Real})").ToArray()));
			GreatWar.Rival rival2 = rivalsForTest.Find((GreatWar.Rival r) => r.Kingdom == little.data.id);
			GreatWar.Rival rival3 = rivalsForTest.Find((GreatWar.Rival r) => r.Kingdom == rival.data.id);
			Check("both wars noted at their start", rival2 != null && rival3 != null);
			Check("the small realm is no real rival, the big one is", rival2 != null && !rival2.Real && rival3 != null && rival3.Real);
			string text2 = GreatWar.Card(me);
			Check("the war's goal on the HUD", text2?.Contains(rival.name) ?? false, text2);
			int towns = Run.Current.TownsTaken;
			int kingdoms = Run.Current.Kingdoms;
			try
			{
				small.city.joinAnotherKingdom(mine, pCaptured: true);
			}
			catch (Exception ex3)
			{
				Note("capture: " + ex3.Message);
			}
			yield return CivilTest.Wait(2.5f);
			Check("its town taken: a town for the run", Run.Current.TownsTaken >= towns + 1, $"{towns} -> {Run.Current.TownsTaken}");
			Check("a small realm destroyed is no great war", !me.hasTrait("immortal"));
			yield return AllyAndRuins(me, mine, home, big.city.getTile());
			try
			{
				big.city.joinAnotherKingdom(mine, pCaptured: true);
			}
			catch (Exception ex4)
			{
				Note("capture: " + ex4.Message);
			}
			for (float t = 0f; t < 6f; t += 0.5f)
			{
				if (GreatWar.Card(me) == null)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(1.2f);
			Check("the real rival destroyed: immortal", me.hasTrait("immortal"));
			Check("...a kingdom and the war for the run", Run.Current.Kingdoms >= kingdoms + 2 && Run.Current.Immortal, $"kingdoms {kingdoms} -> {Run.Current.Kingdoms}, immortal {Run.Current.Immortal}");
			Check("...the war's card gone", GreatWar.Card(me) == null);
			Run.Asking = Run.Ask.Abandon;
			Mod.RunAnswer(abandon: true);
			Run.LegacyOpen = false;
			Run.HallFileForTest = null;
		}

		private IEnumerator AllyAndRuins(Actor me, Kingdom mine, WorldTile home, WorldTile avoid)
		{
			Actor actor = Village("human", 3, 0, home, 40f, avoid);
			Kingdom allyK = actor?.kingdom;
			Actor second = Village("human", 6, 0, home, 40f, actor?.city?.getTile());
			Kingdom second_k = second?.kingdom;
			if (!Check("an ally and a second rival", allyK != null && second_k != null && allyK != mine && second_k != mine && allyK != second_k))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			try
			{
				World.world.alliances.newAlliance(mine, allyK);
			}
			catch (Exception ex)
			{
				Note("alliance: " + ex.Message);
			}
			try
			{
				World.world.diplomacy.startWar(mine, second_k, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex2)
			{
				Note("war: " + ex2.Message);
			}
			yield return CivilTest.Wait(2.5f);
			GreatWar.Rival rival = GreatWar.RivalsForTest.Find((GreatWar.Rival r) => r.Kingdom == second_k.data.id);
			if (!Check("the second war noted", rival != null))
			{
				yield break;
			}
			rival.Real = true;
			int wins = GreatWar.WinsForTest;
			City theirs = second.city;
			try
			{
				theirs.joinAnotherKingdom(allyK, pCaptured: true);
			}
			catch (Exception ex3)
			{
				Note("ally capture: " + ex3.Message);
			}
			for (float t = 0f; t < 6f; t += 0.5f)
			{
				if (GreatWar.WinsForTest != wins)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("a real rival's only town taken by your ally: the great war is won", GreatWar.WinsForTest > wins, $"allied {Royal.Allied(mine, allyK)}");
			Check("...the ally's town of that rival is yours to ask for", GreatWar.AllyHolds(me, allyK).Contains(theirs));
			string said = null;
			try
			{
				Mod.Royal.SendGift(me, allyK, delegate(string s)
				{
					said = s;
				});
			}
			catch (Exception ex4)
			{
				Note("gift: " + ex4.Message);
			}
			Note("gift: " + said);
			yield return CivilTest.Wait(0.5f);
			try
			{
				Mod.Royal.AskForTown(me, allyK, theirs, delegate(string s)
				{
					said = s;
				});
			}
			catch (Exception ex5)
			{
				Note("ask: " + ex5.Message);
			}
			yield return CivilTest.Wait(1f);
			Check("asked, the ally hands the town over", theirs.isAlive() && theirs.kingdom == mine, said);
			Actor third = Village("human", 4, 0, home, 40f, theirs.getTile());
			Kingdom third_k = third?.kingdom;
			if (!Check("a third rival", third_k != null && third_k != mine && third_k != allyK))
			{
				yield break;
			}
			try
			{
				World.world.diplomacy.startWar(mine, third_k, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex6)
			{
				Note("war: " + ex6.Message);
			}
			yield return CivilTest.Wait(2.5f);
			GreatWar.Rival rival2 = GreatWar.RivalsForTest.Find((GreatWar.Rival r) => r.Kingdom == third_k.data.id);
			if (!Check("the third war noted", rival2 != null))
			{
				yield break;
			}
			rival2.Real = true;
			wins = GreatWar.WinsForTest;
			City gone = third.city;
			long goneId = gone.data.id;
			foreach (Actor item2 in new List<Actor>(gone.getUnits()))
			{
				CivilTest.Kill(item2);
			}
			foreach (Actor item3 in Founded)
			{
				CivilTest.Kill(item3);
			}
			foreach (Building item4 in new List<Building>(gone.buildings))
			{
				try
				{
					if (item4 != null && item4.isAlive())
					{
						item4.makeRuins();
					}
				}
				catch
				{
				}
			}
			for (float t = 0f; t < 10f; t += 0.5f)
			{
				if (!gone.isAlive())
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			if (gone.isAlive())
			{
				Note("the town still stood: taken away as the game does when nothing of it is left");
				try
				{
					World.world.cities.removeObject(gone);
				}
				catch (Exception ex7)
				{
					Note("remove: " + ex7.Message);
				}
			}
			for (float t = 0f; t < 6f; t += 0.5f)
			{
				if (GreatWar.WinsForTest != wins)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("a real rival razed, its town taken by nobody: the great war is won", GreatWar.WinsForTest > wins, $"town alive {gone.isAlive()}");
			List<GreatWar.Ruin> list = GreatWar.Ruins(me);
			GreatWar.Ruin ruin = list.Find((GreatWar.Ruin r) => r.City == goneId);
			if (Check("...its ruins on the Council's list to claim", ruin.Of != null, string.Join(", ", list.Select((GreatWar.Ruin r) => r.Name).ToArray())))
			{
				City city = GreatWar.ClaimRuin(me, ruin, delegate(string s)
				{
					said = s;
				});
				Check("claimed: settlers raise a town of your realm on its land", city != null && city.kingdom == mine, said);
				Check("...and the ruins come off the list", !GreatWar.Ruins(me).Exists((GreatWar.Ruin r) => r.City == goneId));
			}
		}
	}
}
