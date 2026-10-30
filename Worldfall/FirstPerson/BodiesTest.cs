using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BodiesTest : CivilTest
	{
		private static readonly string[] Kinds = new string[52]
		{
			"human", "elf", "orc", "dwarf", "bandit", "druid", "evil_mage", "white_mage", "necromancer", "demon",
			"cold_one", "alien", "plague_doctor", "zombie", "zombie_elf", "zombie_orc", "zombie_dwarf", "civ_cat", "civ_dog", "civ_fox",
			"civ_wolf", "civ_bear", "civ_rabbit", "civ_rat", "civ_monkey", "civ_cow", "civ_buffalo", "civ_goat", "civ_sheep", "civ_alpaca",
			"civ_unicorn", "civ_rhino", "civ_armadillo", "civ_chicken", "civ_penguin", "civ_crocodile", "civ_snake", "civ_frog", "civ_turtle", "civ_beetle",
			"civ_scorpion", "civ_crab", "civ_piranha", "civ_seal", "civ_hyena", "civ_capybara", "civ_liliar", "civ_garlic_man", "civ_lemon_man", "civ_crystal_golem",
			"civ_acid_gentleman", "civ_candy_man"
		};

		private static readonly HashSet<string> OwnWoman = new HashSet<string>
		{
			"human", "elf", "orc", "dwarf", "bandit", "druid", "evil_mage", "white_mage", "necromancer", "demon",
			"cold_one", "alien", "zombie", "zombie_elf", "zombie_orc", "zombie_dwarf"
		};

		protected override string Prefix => "BODIES";

		protected override IEnumerator Body()
		{
			WorldTile home = CivilTest.Stage();
			if (home == null)
			{
				Check("a site for the test", ok: false);
				yield break;
			}
			CivilTest.ClearBox(home.x - 8, home.y - 4, home.x + 8, home.y + 9);
			Actor host = CivilTest.Spawn("human", new Vector2((float)home.x + 0.5f, (float)home.y + 0.5f));
			CivilTest.SetSex(host, female: false);
			yield return CivilTest.Wait(0.5f);
			yield return Possess(host);
			if (!Check("first person on a host", InControl(host)))
			{
				yield break;
			}
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			List<string> kinds = new List<string>();
			string[] kinds2 = Kinds;
			foreach (string text in kinds2)
			{
				if (!Options.Contains("only") || Options.Contains("only" + text.Replace("civ_", "")))
				{
					bool flag = false;
					try
					{
						flag = AssetManager.actor_library.get(text) != null;
					}
					catch
					{
					}
					if (flag)
					{
						kinds.Add(text);
					}
					else
					{
						Note("no such kind in this game: " + text);
					}
				}
			}
			yield return Part("models and beards", Models_(kinds));
			yield return Part("drawn", Drawn(kinds, home));
			yield return Part("sex change", SexChange(home));
			if (!Options.Contains("nohands"))
			{
				yield return Part("arms", Arms(kinds, home));
			}
		}

		private IEnumerator Models_(List<string> kinds)
		{
			Vector2 at = Mod.Host.current_position + new Vector2(0f, 12f);
			foreach (string kind in kinds)
			{
				Actor actor = CivilTest.Spawn(kind, at);
				Actor actor2 = CivilTest.Spawn(kind, at + new Vector2(1f, 0f));
				if (actor == null || actor2 == null)
				{
					Check(kind + " spawned", ok: false);
					continue;
				}
				CivilTest.SetSex(actor, female: false);
				CivilTest.SetSex(actor2, female: true);
				string text = Models.PersonFamily(actor);
				string text2 = Models.PersonFamily(actor2);
				MeshModel meshModel = Models.Get(text);
				MeshModel meshModel2 = Models.Get(text2);
				PersonRig personRig = ((meshModel != null) ? Models.Rig(meshModel) : null);
				PersonRig personRig2 = ((meshModel2 != null) ? Models.Rig(meshModel2) : null);
				bool num = OwnWoman.Contains(kind);
				bool flag = personRig2 != null && personRig2.FemaleParts != 0;
				if (num)
				{
					Check(kind + " woman has her own body", text2 == text + "_fem" && meshModel2 != null, text + " / " + text2);
				}
				else if (kind.StartsWith("civ_") && text.StartsWith("person_beast_"))
				{
					Check(kind + " woman wears the beast-folk skirt and bow", text2 == text && flag, $"{text2} parts {personRig2?.FemaleParts:X}");
				}
				else
				{
					Note(kind + ": shared body " + text + " (no woman's sprite of its own)");
				}
				Humanoid.Palette p = new Humanoid.Palette
				{
					Skin = 4290805856u,
					Hair = 4281348144u,
					Beard = true,
					LongHair = false
				};
				if (personRig2 != null)
				{
					MeshInstance meshInstance = new MeshInstance
					{
						Model = meshModel2
					};
					personRig2.Dress(meshInstance, p, beardAllowed: true, female: true);
					int num2 = meshModel2.PartIndex("beard");
					Check(kind + " woman has no beard", num2 < 0 || (meshInstance.Hidden & (ulong)(1L << num2)) != 0, personRig2.AlwaysBearded ? "beard=always model" : null);
					if (personRig2.FemaleParts != 0L)
					{
						Check(kind + " woman shows her own parts", (meshInstance.Hidden & personRig2.FemaleParts) == 0);
					}
				}
				if (personRig != null)
				{
					MeshInstance meshInstance2 = new MeshInstance
					{
						Model = meshModel
					};
					personRig.Dress(meshInstance2, p, beardAllowed: true);
					int num3 = meshModel.PartIndex("beard");
					if (personRig.AlwaysBearded && num3 >= 0)
					{
						Check(kind + " man keeps his beard", (meshInstance2.Hidden & (ulong)(1L << num3)) == 0);
					}
					if (personRig.FemaleParts != 0L)
					{
						Check(kind + " man hides the women's parts", (meshInstance2.Hidden & personRig.FemaleParts) == personRig.FemaleParts);
					}
				}
				ArmLook armLook = WorldBoxMod.ArmLookOf(actor2);
				Check(kind + " woman's arms are a woman's", armLook != ArmLook.Man, armLook.ToString());
				Check(kind + " man's arms are a man's", WorldBoxMod.ArmLookOf(actor) == ArmLook.Man);
				CivilTest.Kill(actor);
				CivilTest.Kill(actor2);
				yield return null;
			}
		}

		private IEnumerator Drawn(List<string> kinds, WorldTile home)
		{
			Vector2 eye = Mod.Host.current_position + new Vector2(0f, 2f);
			for (int g = 0; g < kinds.Count; g += 4)
			{
				CivilTest.ClearBox((int)eye.x - 6, (int)eye.y + 1, (int)eye.x + 6, (int)eye.y + 7);
				List<KeyValuePair<string, Actor>> row = new List<KeyValuePair<string, Actor>>();
				for (int i = 0; i < 8 && g + i / 2 < kinds.Count; i++)
				{
					string text = kinds[g + i / 2];
					Actor actor = CivilTest.Spawn(text, eye + new Vector2(((float)i - 3.5f) * 0.95f, 3.6f));
					if (actor != null)
					{
						CivilTest.SetSex(actor, (i & 1) == 1);
						try
						{
							actor.makeWait(20f);
						}
						catch
						{
						}
						row.Add(new KeyValuePair<string, Actor>(text, actor));
					}
				}
				yield return CivilTest.Wait(0.3f);
				Config.paused = true;
				Mod.DebugEye = eye;
				Mod.DebugEyeSet = true;
				Mod.DebugEyeHeight = 0f;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-3f);
				yield return CivilTest.Wait(1.6f);
				Mod.SaveFrame($"bodies_row_{g / 4}.png");
				foreach (KeyValuePair<string, Actor> item in row)
				{
					bool female;
					ulong hidden;
					string text2 = People.DrawnModel(item.Value, out female, out hidden);
					bool flag = Models.IsFemale(item.Value);
					string text3 = Models.PersonFamily(item.Value);
					bool flag2 = text2 != null && text2.StartsWith(text3, StringComparison.Ordinal) && female == flag;
					if (flag2 && !flag && text2.Contains("_fem"))
					{
						flag2 = false;
					}
					Check(item.Key + " " + (flag ? "woman" : "man") + " drawn as " + (text2 ?? "nothing"), flag2, "wanted " + text3);
				}
				Config.paused = false;
				foreach (KeyValuePair<string, Actor> item2 in row)
				{
					CivilTest.Kill(item2.Value);
				}
				Mod.DebugEyeSet = false;
				yield return CivilTest.Wait(0.4f);
			}
		}

		private IEnumerator SexChange(WorldTile home)
		{
			Vector2 vector = Mod.Host.current_position + new Vector2(0f, 2f);
			Actor a = CivilTest.Spawn("human", vector + new Vector2(0f, 3f));
			if (a == null)
			{
				Check("spawned for the sex change", ok: false);
				yield break;
			}
			CivilTest.SetSex(a, female: false);
			try
			{
				a.makeWait(20f);
			}
			catch
			{
			}
			Mod.DebugEye = vector;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(MathF.PI / 2f);
			yield return CivilTest.Wait(1f);
			bool w0;
			ulong hidden;
			string before = People.DrawnModel(a, out w0, out hidden);
			CivilTest.SetSex(a, female: true);
			yield return CivilTest.Wait(0.6f);
			bool female;
			string text = People.DrawnModel(a, out female, out hidden);
			Check("a man is drawn as a man", before != null && !before.Contains("_fem") && !w0, before);
			Check("the same person drawn as a woman after the change", (text?.Contains("_fem") ?? false) && female, text);
			CivilTest.Kill(a);
			Mod.DebugEyeSet = false;
		}

		private IEnumerator Arms(List<string> kinds, WorldTile home)
		{
			Actor first = Mod.Host;
			Vector2 at = first.current_position + new Vector2(0f, -2f);
			int n = 0;
			foreach (string kind in kinds)
			{
				if (!OwnWoman.Contains(kind) && !(kind == "civ_cat") && !(kind == "civ_frog"))
				{
					continue;
				}
				Actor w = CivilTest.Spawn(kind, at);
				if (w == null)
				{
					continue;
				}
				CivilTest.SetSex(w, female: true);
				yield return CivilTest.Wait(0.2f);
				yield return Possess(w, 1.2f);
				if (!Check("in control of a " + kind + " woman", InControl(w)))
				{
					CivilTest.Kill(w);
					continue;
				}
				ArmLook armLook = WorldBoxMod.ArmLookOf(w);
				Check($"{kind} woman's first-person arms: {armLook}", armLook != ArmLook.Man);
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-20f);
				yield return CivilTest.Wait(0.6f);
				if (n++ < 10)
				{
					Mod.SaveFrame("bodies_hands_" + kind + ".png");
				}
				Mod.SetPitchDegrees(0f);
			}
			yield return Possess(first, 1f);
		}
	}
}
