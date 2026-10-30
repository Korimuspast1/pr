using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ChronicleTest : CivilTest
	{
		private long _c;

		private long _p1;

		private long _p2;

		private long _g1;

		private long _s;

		private string _g1Name;

		private string _p1Name;

		private string _p2Name;

		private string _sName;

		protected override string Prefix => "CHRON";

		protected override IEnumerator Body()
		{
			yield return Part("family", Family());
			if (!Options.Contains("noreload"))
			{
				yield return Part("save and reload", Reload());
			}
			if (!Options.Contains("nocost"))
			{
				yield return Part("sweep cost", Cost());
			}
		}

		private IEnumerator WaitSweeps(int cycles)
		{
			int start = Chronicle.Cycles;
			for (float t = 0f; t < 20f; t += 0.1f)
			{
				if (Chronicle.Cycles >= start + cycles)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
		}

		private IEnumerator Family()
		{
			WorldTile worldTile = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					worldTile = enumerator.Current;
				}
			}
			if (worldTile == null)
			{
				Check("a site", ok: false);
				yield break;
			}
			Vector2 vector = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			Actor g1 = CivilTest.Spawn("human", vector);
			Actor g2 = CivilTest.Spawn("human", vector + new Vector2(1f, 0f));
			Actor p1 = CivilTest.Spawn("human", vector + new Vector2(0f, 1f));
			Actor p2 = CivilTest.Spawn("human", vector + new Vector2(1f, 1f));
			Actor c = CivilTest.Spawn("human", vector + new Vector2(0f, 2f), adult: false);
			Actor actor = CivilTest.Spawn("human", vector + new Vector2(1f, 2f), adult: false);
			if (g1 == null || g2 == null || p1 == null || p2 == null || c == null || actor == null)
			{
				Check("family spawned", ok: false);
				yield break;
			}
			CivilTest.SetSex(g1, female: false);
			CivilTest.SetSex(g2, female: true);
			CivilTest.SetSex(p1, female: false);
			CivilTest.SetSex(p2, female: true);
			CivilTest.SetSex(c, female: true);
			CivilTest.SetSex(actor, female: false);
			p1.setParent1(g1);
			p1.setParent2(g2);
			c.setParent1(p1);
			c.setParent2(p2);
			actor.setParent1(p1);
			actor.setParent2(p2);
			try
			{
				p1.becomeLoversWith(p2);
			}
			catch (Exception ex)
			{
				Note("lovers: " + ex.Message);
			}
			Actor[] array = new Actor[6] { g1, g2, p1, p2, c, actor };
			foreach (Actor actor2 in array)
			{
				try
				{
					actor2.makeWait(30f);
				}
				catch
				{
				}
			}
			_c = c.data.id;
			_p1 = p1.data.id;
			_p2 = p2.data.id;
			_g1 = g1.data.id;
			_s = actor.data.id;
			_g1Name = Chronicle.SafeName(g1);
			_p1Name = Chronicle.SafeName(p1);
			_p2Name = Chronicle.SafeName(p2);
			_sName = Chronicle.SafeName(actor);
			Note($"family: grandparents {_g1Name} + {Chronicle.SafeName(g2)}, parents {_p1Name} + {_p2Name}, children {Chronicle.SafeName(c)} and {_sName}; {World.world.units.Count} units");
			Check("everyone in it is a person who remembers", Chronicle.IsPerson(g1) && Chronicle.IsPerson(c));
			yield return WaitSweeps(3);
			Record record = Chronicle.Of(c);
			Record record2 = Chronicle.Of(p1);
			Check("the child knows her father", record.Get("p1")?.Name == _p1Name, record.Get("p1")?.Name);
			Check("the child knows her mother", record.Get("p2")?.Name == _p2Name, record.Get("p2")?.Name);
			Check("the child knows her grandfather", record.Get("g1")?.Name == _g1Name, record.Get("g1")?.Name);
			Check("the child knows her grandmother", record.Get("g2")?.Name == Chronicle.SafeName(g2), record.Get("g2")?.Name);
			Check("the father knows both his children", record2.Child(_c) != null && record2.Child(_s) != null, Chronicle.WriteKin(record2.Kin));
			Check("the father knows his lover", record2.Get("lv")?.Id == _p2, record2.Get("lv")?.Name);
			List<Kin> list = Chronicle.SiblingsOf(record);
			Check("the child's brother is found", list.Exists((Kin k) => k.Id == _s), list.Count + " siblings");
			c.data.get("fp_kin", out var pResult, null);
			Check("kin are written into the child's custom data", pResult?.Contains(_g1Name) ?? false, pResult);
			FamilyTree familyTree = Dialogue.TreeOf(c);
			Check("the family tree has 2 grandparents, 2 parents, a brother", familyTree.Grandparents[0] != null && familyTree.Grandparents[1] != null && familyTree.Parents[0] != null && familyTree.Parents[1] != null && familyTree.Siblings.Count >= 1);
			int year = Chronicle.Year;
			CivilTest.Kill(g1);
			yield return CivilTest.Wait(0.5f);
			yield return WaitSweeps(2);
			record2 = Chronicle.Of(p1);
			record = Chronicle.Of(c);
			Kin kin = record2.Get("p1");
			Kin kin2 = record.Get("g1");
			Check("the son remembers his late father", kin != null && kin.Dead && kin.Name == _g1Name, (kin != null) ? (kin.Name + " died " + kin.Died) : "none");
			Check("the granddaughter remembers her late grandfather", kin2 != null && kin2.Dead && kin2.Died >= year - 1, (kin2 != null) ? (kin2.Name + " died " + kin2.Died) : "none");
			try
			{
				p1.changeHappiness("death_family_member");
				p1.changeHappiness("fallen_in_love");
				p2.changeHappiness("just_had_child");
				c.changeHappiness("got_robbed");
				c.changeHappiness("just_ate");
			}
			catch (Exception ex2)
			{
				Note("happiness: " + ex2.Message);
			}
			try
			{
				List<string> list2 = new List<string>();
				if (p1.happiness_change_history != null)
				{
					foreach (HappinessHistory item in p1.happiness_change_history)
					{
						list2.Add(((item.asset != null) ? item.asset.id : ("?" + item.index)) + "@" + item.timestamp.ToString("F2"));
					}
				}
				Note(string.Format("p1 history ({0}): {1}; world time {2:F2}; record stamp {3}", p1.hasHappinessHistory(), string.Join(", ", list2.ToArray()), World.world.getCurWorldTime(), Chronicle.Of(p1).LastStamp));
			}
			catch (Exception ex3)
			{
				Note("history: " + ex3);
			}
			yield return WaitSweeps(2);
			record2 = Chronicle.Of(p1);
			Memory memory = record2.Memories.Find((Memory m) => m.Kind == "death_family_member");
			Memory memory2 = record2.Memories.Find((Memory m) => m.Kind == "fallen_in_love");
			Memory memory3 = Chronicle.Of(p2).Memories.Find((Memory m) => m.Kind == "just_had_child");
			Check("the son remembers the death in the family, and whose", memory != null && memory.Other == _g1Name, (memory != null) ? memory.Other : "no memory");
			Check("he remembers falling in love, and with whom", memory2 != null && memory2.Other == _p2Name, (memory2 != null) ? memory2.Other : "no memory");
			Check("the mother remembers a child born, and which", memory3 != null && memory3.Other != null, memory3?.Other);
			Check("the robbery is remembered", Chronicle.Of(c).Memories.Exists((Memory m) => m.Kind == "got_robbed"));
			Check("eating is not", !Chronicle.Of(c).Memories.Exists((Memory m) => m.Kind == "just_ate"));
			string text = ((memory != null) ? Dialogue.TellMemory(memory) : null);
			Check("a memory is told in the game's own words", !string.IsNullOrEmpty(text) && text.Contains(_g1Name), text);
			Chronicle.ChangeOpinion(c, p2, 17);
			Chronicle.Note(c, p2, 3, 1);
			Regard regard = Chronicle.RegardOf(c, p2, create: false);
			Check("an opinion is kept", regard != null && regard.Talks == 3 && regard.Gifts == 1, (regard != null) ? (regard.Opinion + " opinion") : null);
			CivilTest.Kill(p1);
			yield return CivilTest.Wait(0.5f);
			yield return WaitSweeps(2);
			familyTree = Dialogue.TreeOf(c);
			Check("the child's tree shows her late father", familyTree.Parents[0] != null && familyTree.Parents[0].Dead && familyTree.Parents[0].Name == _p1Name);
			Check("...and her brother alive", familyTree.Siblings.Exists((TreeNode n) => n.Id == _s && !n.Dead));
		}

		private IEnumerator Reload()
		{
			if (_c == 0L)
			{
				Check("a family to reload", ok: false);
				yield break;
			}
			int opinion = Chronicle.RegardOf(Chronicle.Find(_c), Chronicle.Find(_p2), create: false)?.Opinion ?? int.MinValue;
			byte[] array;
			try
			{
				SavedMap savedMap = SaveManager.currentWorldToSavedMap();
				string text = savedMap.toJson();
				Check("the save holds the Chronicle's custom data", text.Contains("fp_kin") && text.Contains(_g1Name), text.Length + " chars");
				array = savedMap.toZip();
			}
			catch (Exception ex)
			{
				Check("the world saved to bytes", ok: false, ex.Message);
				yield break;
			}
			Note($"saved {array.Length} bytes; loading it back");
			Chronicle.Reset();
			try
			{
				SaveManager.loadMapFromBytes(array);
			}
			catch (Exception ex2)
			{
				Check("the world loaded from bytes", ok: false, ex2.Message);
				yield break;
			}
			for (float t = 0f; t < 60f; t += 0.5f)
			{
				yield return CivilTest.Wait(0.5f);
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t > 2f)
				{
					break;
				}
			}
			yield return CivilTest.Wait(1f);
			Actor actor = null;
			try
			{
				actor = World.world.units.get(_c);
			}
			catch
			{
			}
			if (Check("the child is back after the reload", actor?.isAlive() ?? false))
			{
				actor.data.get("fp_kin", out var pResult, null);
				Check("her kin string survived", pResult != null && pResult.Contains(_g1Name) && pResult.Contains(_p1Name), pResult);
				Record record = Chronicle.Of(actor);
				Kin kin = record.Get("p1");
				Kin kin2 = record.Get("g1");
				Check("her late father is still remembered", kin != null && kin.Dead && kin.Name == _p1Name, kin?.Name);
				Check("her late grandfather too", kin2 != null && kin2.Dead && kin2.Name == _g1Name, kin2?.Name);
				Actor actor2 = null;
				try
				{
					actor2 = World.world.units.get(_p2);
				}
				catch
				{
				}
				Check("her mother's memories survived", actor2 != null && Chronicle.Of(actor2).Memories.Exists((Memory m) => m.Kind == "just_had_child"));
				Regard regard = ((actor2 != null) ? Chronicle.RegardOf(actor, actor2, create: false) : null);
				Check("what she thinks of her mother survived", regard != null && regard.Opinion == opinion && regard.Talks == 3, (regard != null) ? (regard.Opinion + "/" + opinion) : "none");
				actor.data.get("fp_memt", out var pResult2, -1L);
				Check("a long value survived (the memory stamp)", pResult2 >= 0, pResult2.ToString());
			}
		}

		private IEnumerator Cost()
		{
			Chronicle.TotalMs = 0.0;
			Chronicle.TotalFrames = 0;
			yield return CivilTest.Wait(4f);
			double a = Chronicle.TotalMs / (double)Math.Max(1, Chronicle.TotalFrames);
			int units = World.world.units.Count;
			WorldTile worldTile = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					worldTile = enumerator.Current;
				}
			}
			List<Actor> crowd = new List<Actor>();
			for (int i = 0; i < 300; i++)
			{
				if (worldTile == null)
				{
					break;
				}
				Actor actor = CivilTest.Spawn("human", new Vector2((float)worldTile.x + (float)(i % 20) * 0.7f, (float)worldTile.y + (float)(i / 20) * 0.7f), i % 3 != 0);
				if (actor != null)
				{
					crowd.Add(actor);
				}
				if (i > 2 && actor != null && crowd.Count > 3)
				{
					actor.setParent1(crowd[i % 3], pIncreaseChildren: false);
				}
			}
			Chronicle.TotalMs = 0.0;
			Chronicle.TotalFrames = 0;
			Chronicle.SweepMsMax = 0.0;
			yield return CivilTest.Wait(6f);
			double num = Chronicle.TotalMs / (double)Math.Max(1, Chronicle.TotalFrames);
			Note($"sweep cost: {a:F3} ms a frame with {units} units, {num:F3} ms with {World.world.units.Count} (worst frame {Chronicle.SweepMsMax:F2} ms, {Chronicle.PerFrame} people a frame)");
			Check("the sweep costs under 0.5 ms a frame on average", num < 0.5, $"{num:F3} ms");
			foreach (Actor item in crowd)
			{
				CivilTest.Kill(item);
			}
		}
	}
}
