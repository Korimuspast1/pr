using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BirthsTest : CivilTest
	{
		private Actor _mother;

		private Actor _father;

		protected override string Prefix => "BIRTHS";

		protected override IEnumerator Body()
		{
			Births.AnyPlaceForTest = true;
			WorldTile worldTile = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					worldTile = enumerator.Current;
				}
			}
			if (!Check("a site", worldTile != null))
			{
				yield break;
			}
			Vector2 vector = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			_mother = CivilTest.Spawn("human", vector);
			_father = CivilTest.Spawn("human", vector + new Vector2(1.4f, 0f));
			if (Check("a couple spawned", _mother != null && _father != null))
			{
				CivilTest.SetSex(_mother, female: true);
				CivilTest.SetSex(_father, female: false);
				try
				{
					_mother.becomeLoversWith(_father);
				}
				catch (Exception ex)
				{
					Note("lovers: " + ex.Message);
				}
				Check("they are lovers", _mother.lover == _father && _father.lover == _mother);
				yield return Possess(_mother);
				if (Check("first person as the mother", InControl(_mother)))
				{
					yield return Part("a child of your own", Conceive());
					yield return Part("the belly", Belly());
					yield return Part("the birth and the name", Birth());
					yield return Part("adoption", Adoption());
					yield return Part("alone", Alone());
					yield return Part("your heir", Heir());
					yield return Part("another subspecies", OtherKind());
					Births.AnyPlaceForTest = false;
				}
			}
		}

		private IEnumerator Conceive()
		{
			Conversation c = Mod.Converse;
			CivilTest.StandBefore(_father, _mother, 1.3f);
			yield return CivilTest.Wait(0.3f);
			bool adopt;
			string text = Births.WhyNot(_mother, _father, out adopt);
			Check("nothing stands in the way", text == null, text);
			Mod.StartConversationForTest(_father);
			Choice choice = c.Choices.Find((Choice ch) => ch.Label.StartsWith("Let's have a child"));
			Check("your lover's talk offers a child", choice?.Enabled ?? false, choice?.Note);
			if (choice == null)
			{
				yield break;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			Check("the screen fades to black", Births.FadeLeft > 0f);
			Note("result: " + Births.LastResult + ", said: " + c.Line);
			yield return CivilTest.Wait(3.5f);
			float fadeBy = Time.realtimeSinceStartup + 3.2f;
			while (Births.FadeLeft > 0f && Time.realtimeSinceStartup < fadeBy)
			{
				yield return null;
			}
			Check("the fade passes", Births.FadeLeft <= 0f, Births.FadeLeft.ToString("F2"));
			bool flag = Births.IsExpecting(_mother);
			Check("you're expecting (the game's own pregnant status)", flag, Births.LastResult);
			if (!flag)
			{
				for (int num = 0; num < 3; num++)
				{
					if (Births.IsExpecting(_mother))
					{
						break;
					}
					Births.Conceive(_mother);
				}
				Check("...on a second try", Births.IsExpecting(_mother));
			}
			string text2 = Births.ExpectingLabel(_mother);
			float num2 = Births.Progress(_mother);
			Check("\"Expecting, about N months\"", text2?.StartsWith("Expecting") ?? false, text2);
			Check("the pregnancy has only just begun", num2 >= 0f && num2 < 0.2f, num2.ToString("F2"));
			Mod.EndConversation();
			Mod.StartConversationForTest(_father);
			Note("he says: " + c.Line + " / " + ((c.Pages.Count > 1) ? c.Pages[1] : ""));
			Check("the father speaks of the baby", string.Join(" ", c.Pages.ToArray()).ToLowerInvariant().Contains("bab") || string.Join(" ", c.Pages.ToArray()).Contains("little one"));
			Mod.EndConversation();
			Check("the nameplate shows it", Births.ExpectingLabel(_mother) != null);
		}

		private IEnumerator OtherKind()
		{
			Actor actor = CivilTest.Spawn("human", _mother.current_position + new Vector2(2f, 1f));
			Actor actor2 = CivilTest.Spawn("human", _mother.current_position + new Vector2(3f, 1f));
			if (Check("two of one species", actor != null && actor2 != null))
			{
				CivilTest.SetSex(actor, female: true);
				CivilTest.SetSex(actor2, female: false);
				try
				{
					actor2.setSubspecies(World.world.subspecies.newSpecies(actor2.asset, actor2.current_tile, pMutation: true));
				}
				catch (Exception ex)
				{
					Note("subspecies: " + ex.Message);
				}
				Check("...of two subspecies", actor.subspecies != null && actor2.subspecies != null && actor.subspecies != actor2.subspecies);
				Check("they may flirt", Romance.Blocker(actor, actor2) != FlirtOutcome.Incompatible, Romance.Blocker(actor, actor2)?.ToString());
				try
				{
					actor.becomeLoversWith(actor2);
				}
				catch (Exception ex2)
				{
					Note("lovers: " + ex2.Message);
				}
				bool adopt;
				string text = Births.WhyNot(actor, actor2, out adopt);
				Check("...and have a child: not refused as another kind", text != "You're not the same kind.", text ?? "nothing in the way");
				yield return null;
			}
		}

		private static Status Pregnancy(Actor a)
		{
			try
			{
				Status value;
				return a.getStatusesDict().TryGetValue("pregnant", out value) ? value : null;
			}
			catch
			{
				return null;
			}
		}

		private IEnumerator Belly()
		{
			yield return Possess(_father, 1.2f);
			CivilTest.StandBefore(_mother, _father, 1.6f);
			try
			{
				_mother.makeWait(20f);
			}
			catch
			{
			}
			Mod.SetPitchDegrees(-6f);
			Mod.StartConversationForTest(_mother);
			int before = Births.BelliesDrawn;
			yield return CivilTest.Wait(1.2f);
			Check("her belly is drawn", Births.BelliesDrawn > before);
			Mod.SaveFrame("births_belly_early.png");
			Vector2 current_position = _mother.current_position;
			Vector2 current_position2 = _father.current_position;
			Vector2 normalized = new Vector2(0f - (current_position - current_position2).y, (current_position - current_position2).x).normalized;
			Mod.DebugEye = current_position + normalized * 1.5f;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(0f - normalized.y, 0f - normalized.x));
			Note($"belly photos: her at {current_position}, eye at {Mod.DebugEye}, yaw {Mathf.Atan2(0f - normalized.y, 0f - normalized.x) * 57.29578f:F0}");
			yield return CivilTest.Wait(0.8f);
			Mod.SaveFrame("births_belly_early_side.png");
			Status status = Pregnancy(_mother);
			if (status != null)
			{
				double curWorldTime = World.world.getCurWorldTime();
				status._end_time = curWorldTime + (double)(status.duration * 0.15f);
				yield return CivilTest.Wait(0.8f);
				float num = Births.Progress(_mother);
				Check("late in the pregnancy", num > 0.8f, num.ToString("F2"));
				Mod.SaveFrame("births_belly_late_side.png");
				Mod.DebugEyeSet = false;
				Vector2 vector = _mother.current_position - _father.current_position;
				Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
				yield return CivilTest.Wait(0.6f);
				Mod.SaveFrame("births_belly_late.png");
			}
			Mod.DebugEyeSet = false;
			Mod.EndConversation();
			yield return Possess(_mother, 1.2f);
			yield return CivilTest.Wait(0.6f);
			Check("late on, you can't sprint as fast", PossessionHooks.SprintCap < 1.25f, PossessionHooks.SprintCap.ToString("F2"));
		}

		private IEnumerator Birth()
		{
			try
			{
				_ = _mother.current_children_count;
			}
			catch
			{
			}
			Status status = Pregnancy(_mother);
			if (!Check("still expecting before the birth", status != null))
			{
				yield break;
			}
			status.setDuration(1.5f);
			Actor baby = null;
			for (float t = 0f; t < 20f; t += 0.5f)
			{
				if (baby != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if ((simple != null && simple.isAlive() && simple.data.parent_id_1 == _mother.data.id) || (simple != null && simple.isAlive() && simple.data.parent_id_2 == _mother.data.id))
					{
						baby = simple;
						break;
					}
				}
			}
			if (!Check("a baby is born (the game's own birth)", baby != null))
			{
				yield break;
			}
			long parent_id_ = baby.data.parent_id_1;
			long parent_id_2 = baby.data.parent_id_2;
			bool ok = (parent_id_ == _mother.data.id && parent_id_2 == _father.data.id) || (parent_id_2 == _mother.data.id && parent_id_ == _father.data.id);
			Check("the baby has both of you as parents", ok, $"{parent_id_}, {parent_id_2}");
			Check("the pregnancy is over", !Births.IsExpecting(_mother));
			yield return CivilTest.Wait(0.3f);
			Check("your sprint is back", PossessionHooks.SprintCap >= 1.34f || Births.IsExpecting(_mother), PossessionHooks.SprintCap.ToString("F2"));
			for (float t = 0f; t < 3f; t += 0.25f)
			{
				if (Births.Naming != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("the name box opens", Births.Naming == baby, (Births.Naming != null) ? Dialogue.NameOf(Births.Naming) : "none");
			Check("the keys are kept for typing", Mod.GameKeysMuted);
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "births_name.png"));
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Births.NameText = "Testa";
			Births.FinishNaming(_mother, keep: false);
			Check("the baby has the name you gave", Dialogue.NameOf(baby) == "Testa", Dialogue.NameOf(baby));
			Check("the name box is gone", Births.Naming == null && !Mod.GameKeysMuted);
			yield return CivilTest.Wait(0.6f);
			Record record = Chronicle.Of(_mother);
			Check("you remember naming her", record.Memories.Exists((Memory m) => m.Kind == "fp_named" && m.Other == "Testa"));
			Check("the child is on your family tree", Dialogue.TreeOf(_mother).Children.Exists((TreeNode n) => n.Id == baby.data.id));
		}

		private IEnumerator Adoption()
		{
			Actor her = CivilTest.Spawn("human", _mother.current_position);
			CivilTest.SetSex(her, female: true);
			bool adopt;
			string detail = Births.WhyNot(_mother, her, out adopt);
			Check("a same-sex couple is offered adoption", her != null && adopt, detail);
			Actor orphan = CivilTest.Spawn("human", _mother.current_position, adult: false);
			if (Check("an orphan spawned", orphan != null))
			{
				yield return CivilTest.Wait(0.2f);
				Actor actor = Births.FindOrphan(_mother, 30f);
				Check("the orphan is found", actor != null, (actor != null) ? Dialogue.NameOf(actor) : null);
				bool flag = Births.Adopt(_mother, _father, orphan);
				Check("adopted: you and your lover are the parents", flag && orphan.data.parent_id_1 == _mother.data.id && orphan.data.parent_id_2 == _father.data.id);
				yield return CivilTest.Wait(0.3f);
				Check("the adopted child is on your family tree", Dialogue.TreeOf(_mother).Children.Exists((TreeNode n) => n.Id == orphan.data.id));
				CivilTest.Kill(her);
			}
		}

		private IEnumerator Heir()
		{
			if (!InControl(_mother))
			{
				yield return Possess(_mother, 1.2f);
			}
			try
			{
				_mother.addMoney(40);
			}
			catch
			{
			}
			int purse = CivilTest.Money(_mother);
			CivilTest.Kill(_mother);
			for (float t = 0f; t < 3f; t += 0.2f)
			{
				if (Mod.ChoosingHeir)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			Check("on the death screen, heirs are offered", Mod.ChoosingHeir, string.Join(", ", Mod.Heirs.ConvertAll((TreeNode h) => h.Name + " (" + h.Role + ")").ToArray()));
			if (Mod.ChoosingHeir)
			{
				yield return CivilTest.Wait(1.6f);
				try
				{
					ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "births_heir.png"));
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.3f);
				Check("your children come first", Mod.Heirs[0].Role == "son" || Mod.Heirs[0].Role == "daughter", Mod.Heirs[0].Role);
				Actor heir = Mod.Heirs[0].Actor;
				int theirs = CivilTest.Money(heir);
				bool ok = Mod.ContinueAs(0);
				yield return CivilTest.Wait(1.5f);
				Check("you carry on as your heir", ok && InControl(heir), (heir != null) ? Dialogue.NameOf(heir) : null);
				Check("...with what was in your purse", CivilTest.Money(heir) >= theirs + purse, $"{theirs} -> {CivilTest.Money(heir)} (purse {purse})");
			}
		}

		private IEnumerator Alone()
		{
			Actor lone = null;
			string[] array = new string[10] { "bioblob", "acid_blob", "fire_elemental_blob", "tumor_monster_animal", "mush_animal", "civ_crystal_golem", "civ_acid_gentleman", "civ_liliar", "lemon_snail", "worm" };
			foreach (string text in array)
			{
				Actor actor = CivilTest.Spawn(text, _mother.current_position + new Vector2(3f, -3f));
				if (actor != null)
				{
					if (Births.ReproducesAlone(actor))
					{
						lone = actor;
						Note("reproduces alone: " + text);
						break;
					}
					CivilTest.Kill(actor);
				}
			}
			if (lone == null)
			{
				Note("(no kind that reproduces alone could be spawned: skipped)");
				yield break;
			}
			int before = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset == lone.asset)
				{
					before++;
				}
			}
			Actor baby = Births.BringForth(lone);
			yield return CivilTest.Wait(0.5f);
			int num = 0;
			foreach (Actor simple2 in World.world.units.getSimpleList())
			{
				if (simple2 != null && simple2.isAlive() && simple2.asset == lone.asset)
				{
					num++;
				}
			}
			Check("a " + lone.asset.id + " brings forth young alone", baby != null || num > before || Births.IsExpecting(lone), Births.LastResult);
		}
	}
}
