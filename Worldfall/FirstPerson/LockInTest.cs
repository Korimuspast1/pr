using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LockInTest : CivilTest
	{
		protected override string Prefix => "LOCKIN";

		protected override IEnumerator Body()
		{
			string text = Path.Combine(Log.Folder, "runs_test.txt");
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
			Actor king = Village("human", 10, 0);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			_ = king.city;
			List<Actor> people = Founded.Where((Actor u) => u != null && u != king && u.isAdult()).ToList();
			if (!Check("villagers", people.Count >= 4))
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
			Actor actor = CivilTest.SpawnNear("sheep", king.current_position, 4f);
			string why = null;
			Check("a sheep can't be locked into", actor != null && !Run.CanLock(actor, out why), why);
			Actor me = people[0];
			yield return Possess(me);
			if (!Check("first person on a villager", InControl(me)))
			{
				yield break;
			}
			try
			{
				me.addTrait("strong", pRemoveOpposites: true);
			}
			catch
			{
			}
			int num = Run.Budget();
			Check("a first run's budget is 3 points", num == 3, num.ToString());
			Run.Picked.Clear();
			Run.Picked.Add("fast");
			Run.Picked.Add("block");
			bool flag = Run.Lock(me, Run.Picked.ToList(), base.Note);
			Check("locked in", flag && Run.Locked && Run.Body == me);
			Check("the loadout's trait, within the budget", me.hasTrait("fast") && !me.hasTrait("block"), Run.Current?.Spent + " points spent");
			Check("the free gift kept", me.hasTrait("strong"));
			string pResult = null;
			try
			{
				me.data.get("fp_run", out pResult, null);
			}
			catch
			{
			}
			Run.Record record = Run.Record.Parse(pResult);
			Check("the run is in their data", record != null && record.Id == Run.Current.Id && record.Generation == 1, pResult);
			Actor actor2 = people[1];
			bool flag2 = Mod.RunHoldsLeapForTest(actor2);
			Check("= asks before giving the run up", flag2 && Run.Asking == Run.Ask.Abandon && Run.LeapTo == actor2);
			Mod.RunAnswer(abandon: false);
			Check("...kept living: still locked, still you", Run.Asking == Run.Ask.None && Run.Locked && InControl(me));
			Mod.LetGoForPadTest();
			for (float t = 0f; t < 3f; t += 0.1f)
			{
				if (Run.Asking == Run.Ask.LeftBody)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Check("let go: asked to go back or give up", Run.Asking == Run.Ask.LeftBody, Run.Asking.ToString());
			PlayInput.TestPress(KeyCode.Escape);
			yield return null;
			yield return null;
			PlayInput.TestRelease(KeyCode.Escape);
			yield return CivilTest.Wait(1f);
			Check("...Esc: later, not asked again, the run kept", Run.Asking == Run.Ask.None && !Run.WindowOpen && Run.Current != null && Run.Body == me, Run.Asking.ToString());
			yield return Possess(me);
			yield return CivilTest.Wait(0.5f);
			Check("...taken again: still locked, still you", Run.Asking == Run.Ask.None && Run.Locked && InControl(me));
			Actor elder = people.Skip(2).FirstOrDefault((Actor p) => !Run.HasHome(p));
			Actor heir = people.Skip(2).FirstOrDefault((Actor p) => p != elder);
			try
			{
				if (elder != null)
				{
					elder.setParent1(me);
				}
				heir.setParent1(me);
				WorldTile tile = World.world.GetTile((int)heir.current_position.x + 3, (int)heir.current_position.y + 3);
				Building building = World.world.buildings.addBuilding("house_human_1", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building != null)
				{
					heir.setHomeBuilding(building);
				}
			}
			catch (Exception ex)
			{
				Note("family: " + ex.Message);
			}
			Check("one child has a home, the other none", Run.HasHome(heir) && (elder == null || !Run.HasHome(elder)), string.Format("housed {0}, other {1}", Run.HasHome(heir), (elder != null) ? Run.HasHome(elder).ToString() : "none found"));
			try
			{
				me.addToInventory("wood", 7);
			}
			catch
			{
			}
			int woodBefore = Wood(heir);
			CivilTest.Kill(me);
			for (float t = 0f; t < 4f; t += 0.2f)
			{
				if (Mod.ChoosingHeir)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			Check("on the death screen, only an heir with a home", Mod.ChoosingHeir && Mod.Heirs.Count == 1 && Mod.Heirs[0].Actor == heir, string.Join(", ", Mod.Heirs.ConvertAll((TreeNode h) => h.Name + " (" + h.Role + ")").ToArray()));
			if (!Mod.ChoosingHeir)
			{
				yield break;
			}
			bool on = Mod.ContinueAs(0);
			yield return CivilTest.Wait(1.5f);
			Check("you carry on as your heir", on && InControl(heir));
			Check("...the run with you: generation 2", Run.Locked && Run.Body == heir && Run.Current.Generation == 2);
			Check("...and your bag", Wood(heir) >= woodBefore + 7, $"{woodBefore} -> {Wood(heir)}");
			Run.Tally(heir, "quest", 3);
			CivilTest.Kill(heir);
			for (float t = 0f; t < 25f; t += 0.25f)
			{
				if (Run.Current == null)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Run.Legacy lastLegacy = Run.LastLegacy;
			Check("with no heir, the run ends", Run.Current == null && lastLegacy != null && !lastLegacy.Abandoned, (lastLegacy != null) ? lastLegacy.How : "still going");
			Check("...scored (3 quests at least)", lastLegacy != null && lastLegacy.Total >= 30 && lastLegacy.Generations == 2, (lastLegacy != null) ? $"{lastLegacy.Total} points, {lastLegacy.Generations} generations" : null);
			Check("...and in the Hall of Lives with its credits", Run.Hall.Count == 1 && Run.Credits == lastLegacy?.Credits, $"{Run.Hall.Count} lives, {Run.Credits} credits");
			Check("the legacy screen shows", Run.LegacyOpen);
			Run.LegacyOpen = false;
			Actor next = people.FirstOrDefault((Actor p) => p != null && p.isAlive() && p != me && p != heir);
			if (next == null)
			{
				yield break;
			}
			yield return Possess(next);
			Run.Picked.Clear();
			Run.Lock(next, Run.Picked, base.Note);
			Run.Tally(next, "quest", 40);
			int credits = Run.Credits;
			Run.Asking = Run.Ask.Abandon;
			Mod.RunAnswer(abandon: true);
			lastLegacy = Run.LastLegacy;
			Check("given up: the run ends", Run.Current == null && lastLegacy != null && lastLegacy.Abandoned);
			Check("...for half the credits", lastLegacy != null && lastLegacy.Credits == lastLegacy.Total / 200 && Run.Credits == credits + lastLegacy.Credits, (lastLegacy != null) ? $"{lastLegacy.Total} points, {lastLegacy.Credits} credits" : null);
			Check("the shelves open again (sandbox)", !Run.Locked);
			Run.LegacyOpen = false;
			if (king.isAlive())
			{
				try
				{
					king.addMoney(2000);
				}
				catch
				{
				}
				yield return Possess(king);
				Run.Picked.Clear();
				Run.Lock(king, Run.Picked, base.Note);
				Run.Asking = Run.Ask.Abandon;
				Mod.RunAnswer(abandon: true);
				lastLegacy = Run.LastLegacy;
				Check("a king given up at once scores nothing for his crown or purse", lastLegacy != null && lastLegacy.Wealth == 0 && lastLegacy.Credits == 0, (lastLegacy != null) ? $"{lastLegacy.Total} points, wealth {lastLegacy.Wealth}" : null);
				Run.LegacyOpen = false;
			}
			Run.HallFileForTest = null;
		}

		private static int Wood(Actor a)
		{
			try
			{
				return a.inventory.getResource("wood");
			}
			catch
			{
				return 0;
			}
		}
	}
}
