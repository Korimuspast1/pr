using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MutationsTest : CivilTest
	{
		private static readonly (string kind, bool female, bool adult, UnitProfession role)[] Line = new(string, bool, bool, UnitProfession)[9]
		{
			("human", false, true, UnitProfession.Unit),
			("human", true, true, UnitProfession.Unit),
			("human", false, false, UnitProfession.Unit),
			("human", false, true, UnitProfession.King),
			("human", false, true, UnitProfession.Warrior),
			("orc", true, true, UnitProfession.Leader),
			("orc", false, true, UnitProfession.Unit),
			("civ_cat", true, true, UnitProfession.Unit),
			("sheep", false, true, UnitProfession.Nothing)
		};

		private static readonly string[] EggLayers = new string[18]
		{
			"chicken", "sheep", "human", "cow", "cat", "dog", "fox", "wolf", "rabbit", "rat",
			"monkey", "goat", "buffalo", "penguin", "frog", "turtle", "elf", "orc"
		};

		protected override string Prefix => "MUT";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (worldTile == null)
			{
				Check("a site for the test", ok: false);
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 9, worldTile.y - 4, worldTile.x + 9, worldTile.y + 10);
			Actor host = CivilTest.Spawn("elf", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			CivilTest.SetSex(host, female: false);
			yield return CivilTest.Wait(0.5f);
			yield return Possess(host);
			if (!Check("first person on a host", InControl(host)))
			{
				yield break;
			}
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			Check("the mutation bodies are in the library", Models.Get("person_mut_burger") != null && Models.Get("mut_void") != null && Models.Get("egg_eyeball") != null);
			if (!Options.Contains("noskins"))
			{
				string[] skins = MutationLook.Skins;
				foreach (string text in skins)
				{
					if (!Options.Contains("only") || Options.Contains("only" + text))
					{
						yield return Part(text, Skin(text));
					}
				}
			}
			if (!Options.Contains("noeggs"))
			{
				yield return Part("eggs", Eggs());
			}
			if (Options.Contains("zoo"))
			{
				yield return Part("zoo", Zoo());
			}
		}

		private IEnumerator Zoo()
		{
			List<string> kinds = new List<string>();
			foreach (string item in Models.CreatureIdsForTest())
			{
				if (item.StartsWith("zombie", StringComparison.Ordinal) || kinds.Contains(item))
				{
					continue;
				}
				switch (item)
				{
				case "dragon":
				case "UFO":
				case "living_plants":
				case "living_house":
				case "piranha":
					continue;
				}
				try
				{
					if (AssetManager.actor_library.get(item) == null)
					{
						continue;
					}
				}
				catch
				{
					continue;
				}
				kinds.Add(item);
			}
			StringBuilder list = new StringBuilder();
			Vector2 vector = Mod.Host.current_position + new Vector2(0f, 1.5f);
			CivilTest.ClearBox((int)vector.x - 8, (int)vector.y - 2, (int)vector.x + 8, (int)vector.y + 8);
			Pave((int)vector.x - 8, (int)vector.y - 2, (int)vector.x + 8, (int)vector.y + 8);
			Vector2 at = vector + new Vector2(0f, 4.5f);
			yield return CivilTest.Wait(1f);
			for (int k = 0; k < kinds.Count; k++)
			{
				Actor a = CivilTest.Spawn(kinds[k], at);
				if (a == null)
				{
					Note("zoo: no " + kinds[k]);
					continue;
				}
				try
				{
					a.makeWait(30f);
				}
				catch
				{
				}
				yield return CivilTest.Wait((k == 0) ? 1.2f : 0.5f);
				try
				{
					SpriteImage spriteImage = WorldBoxMod.Instance.SpriteImageForTest(a.getSpriteToRender());
					if (spriteImage != null)
					{
						AssetDump.Save(spriteImage, Path.Combine(Log.Folder, $"zoo_{k:00}_sprite.png"), 8);
					}
				}
				catch (Exception ex)
				{
					Note("zoo sprite: " + ex.Message);
				}
				try
				{
					a.current_position = at;
				}
				catch
				{
				}
				float num = Mathf.Max(0.15f, Creatures.DrawnSize(a));
				float d = Mathf.Clamp(num * 2.4f + 0.5f, 1f, 7f);
				list.AppendLine($"{k} {kinds[k]} {Models.CreatureFamily(a)} {num:F2}");
				Config.paused = true;
				Mod.DebugEyeSet = true;
				Mod.DebugEyeHeight = Mathf.Max(0.12f, num * 0.55f + 0.08f);
				Mod.DebugEye = at + new Vector2(0f, 0f - d);
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-9f);
				yield return CivilTest.Wait(0.9f);
				Mod.SaveFrame($"zoo_{k:00}_side.png");
				Vector2 vector2 = new Vector2(-0.72f, -0.69f) * d;
				Mod.DebugEye = at + vector2;
				Mod.SetYaw(Mathf.Atan2(0f - vector2.y, 0f - vector2.x));
				yield return CivilTest.Wait(0.8f);
				Mod.SaveFrame($"zoo_{k:00}_34.png");
				Config.paused = false;
				CivilTest.Kill(a);
				yield return CivilTest.Wait(0.9f);
				Corpses.Clear();
				Mod.DebugEyeSet = false;
			}
			try
			{
				File.WriteAllText(Path.Combine(Log.Folder, "zoo.txt"), list.ToString());
			}
			catch
			{
			}
			Check("the zoo photographed", kinds.Count > 40, kinds.Count + " kinds");
		}

		private IEnumerator Skin(string skin)
		{
			string trait = "mutation_skin_" + skin;
			Vector2 eye = Mod.Host.current_position + new Vector2(0f, 1.5f);
			CivilTest.ClearBox((int)eye.x - 8, (int)eye.y - 2, (int)eye.x + 8, (int)eye.y + 8);
			Pave((int)eye.x - 8, (int)eye.y - 2, (int)eye.x + 8, (int)eye.y + 8);
			List<Actor> row = new List<Actor>();
			HashSet<Subspecies> subs = new HashSet<Subspecies>();
			for (int i = 0; i < Line.Length; i++)
			{
				(string kind, bool female, bool adult, UnitProfession role) tuple = Line[i];
				string item = tuple.kind;
				bool item2 = tuple.female;
				bool item3 = tuple.adult;
				UnitProfession item4 = tuple.role;
				Actor actor = CivilTest.Spawn(item, eye + new Vector2(((float)i - (float)(Line.Length - 1) * 0.5f) * 1.05f, 4.2f), item3);
				if (actor == null)
				{
					Note(skin + ": no " + item);
					continue;
				}
				CivilTest.SetSex(actor, item2);
				try
				{
					actor.makeWait(30f);
				}
				catch
				{
				}
				try
				{
					if (item4 != UnitProfession.Nothing)
					{
						actor.data.profession = item4;
					}
				}
				catch
				{
				}
				row.Add(actor);
				try
				{
					if (actor.subspecies != null)
					{
						subs.Add(actor.subspecies);
					}
				}
				catch
				{
				}
			}
			foreach (Subspecies item5 in subs)
			{
				try
				{
					item5.addTrait(trait, pRemoveOpposites: true);
					item5.forceRecalcBaseStats();
				}
				catch (Exception ex)
				{
					Note(skin + ": trait not added: " + ex.Message);
				}
			}
			yield return CivilTest.Wait(0.4f);
			Config.paused = true;
			Mod.DebugEye = eye;
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 0.35f;
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(1.8f);
			Mod.SaveFrame("mut_" + skin + ".png");
			bool female;
			ulong hidden;
			foreach (Actor item6 in row)
			{
				if (item6 == null || !item6.isAlive())
				{
					continue;
				}
				string text = ((item6.asset != null) ? item6.asset.id : "?");
				bool num = People.IsHumanoid(item6);
				string text2 = (num ? People.DrawnModel(item6, out female, out hidden) : Creatures.DrawnModel(item6));
				string text3 = (num ? "person_mut_" : "mut_") + skin;
				Check(skin + ": " + text + " " + (Models.IsFemale(item6) ? "woman" : "man") + (item6.isAdult() ? "" : " child") + " drawn as " + (text2 ?? "nothing"), text2?.StartsWith(text3, StringComparison.Ordinal) ?? false, "wanted " + text3);
				try
				{
					SpriteImage spriteImage = WorldBoxMod.Instance.SpriteImageForTest(item6.getSpriteToRender());
					if (spriteImage != null && (text == "human" || text == "sheep"))
					{
						Note($"{skin}: {text} sprite {spriteImage.Width}x{spriteImage.Height} unit {spriteImage.UnitHeight:F2} scale {item6.current_scale.y:F2} frame {spriteImage.VisibleFraction:F2} share {Mutations.Visible(item6, spriteImage):F2} -> {spriteImage.UnitHeight * Mathf.Abs(item6.current_scale.y) * Mutations.Visible(item6, spriteImage):F2} tiles");
					}
				}
				catch
				{
				}
			}
			for (int g = 0; g < 3; g++)
			{
				Mod.DebugEye = eye + new Vector2(((float)(g * 3 + 1) - (float)(Line.Length - 1) * 0.5f) * 1.05f, 2f);
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame($"mut_{skin}_close{g}.png");
			}
			Config.paused = false;
			foreach (Subspecies item7 in subs)
			{
				try
				{
					item7.removeTrait(trait);
					item7.forceRecalcBaseStats();
				}
				catch
				{
				}
			}
			yield return CivilTest.Wait(0.6f);
			Mod.DebugEye = eye;
			Config.paused = true;
			yield return CivilTest.Wait(1f);
			int num2 = 0;
			int num3 = 0;
			foreach (Actor item8 in row)
			{
				if (item8 != null && item8.isAlive())
				{
					string text4 = (People.IsHumanoid(item8) ? People.DrawnModel(item8, out female, out hidden) : Creatures.DrawnModel(item8));
					num3++;
					if (text4 != null && !text4.Contains("mut_"))
					{
						num2++;
					}
				}
			}
			Check(skin + ": everyone back in their own body once the skin is gone", num3 > 0 && num2 == num3, $"{num2} of {num3}");
			Config.paused = false;
			foreach (Actor item9 in row)
			{
				CivilTest.Kill(item9);
			}
			yield return CivilTest.Wait(0.3f);
			Corpses.Clear();
			Mod.DebugEyeSet = false;
			yield return CivilTest.Wait(0.4f);
		}

		internal static void Pave(int x0, int y0, int x1, int y1)
		{
			TileType tileType = null;
			try
			{
				tileType = AssetManager.tiles.get("soil_low");
			}
			catch
			{
			}
			if (tileType == null)
			{
				return;
			}
			for (int i = y0; i <= y1; i++)
			{
				for (int j = x0; j <= x1; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.block)
					{
						try
						{
							tile.top_type = null;
							tile.setTileType(tileType);
						}
						catch
						{
						}
					}
				}
			}
		}

		private IEnumerator Eggs()
		{
			string[] eggs = MutationLook.Eggs;
			Vector2 eye = Mod.Host.current_position + new Vector2(0f, 1.5f);
			for (int g = 0; g < eggs.Length; g += 9)
			{
				CivilTest.ClearBox((int)eye.x - 8, (int)eye.y - 2, (int)eye.x + 8, (int)eye.y + 7);
				Pave((int)eye.x - 8, (int)eye.y - 2, (int)eye.x + 8, (int)eye.y + 7);
				List<KeyValuePair<string, Actor>> row = new List<KeyValuePair<string, Actor>>();
				for (int i = 0; i < 9 && g + i < eggs.Length; i++)
				{
					string text = "egg_" + eggs[g + i];
					string text2 = EggLayers[(g + i) % EggLayers.Length];
					Actor actor = CivilTest.Spawn(text2, eye + new Vector2(((float)i - 4f) * 0.8f, 3.2f), adult: false);
					if (actor == null)
					{
						Note("no " + text2 + " for " + text);
						continue;
					}
					try
					{
						actor.makeWait(30f);
					}
					catch
					{
					}
					try
					{
						Subspecies subspecies = actor.subspecies;
						subspecies.addTrait("reproduction_strategy_oviparity", pRemoveOpposites: true);
						subspecies.addTrait(text, pRemoveOpposites: true);
						subspecies.forceRecalcBaseStats();
						((BaseSimObject)actor).addStatusEffect("egg", 60f, pColorEffect: false);
						actor.calcIsEgg();
					}
					catch (Exception ex)
					{
						Note(text + ": not an egg: " + ex.Message);
					}
					row.Add(new KeyValuePair<string, Actor>(text, actor));
				}
				yield return CivilTest.Wait(0.4f);
				Config.paused = true;
				Mod.DebugEye = eye;
				Mod.DebugEyeSet = true;
				Mod.DebugEyeHeight = 0.4f;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-14f);
				yield return CivilTest.Wait(1.6f);
				Mod.SaveFrame($"mut_eggs_{g / 9}.png");
				foreach (KeyValuePair<string, Actor> item in row)
				{
					Actor value = item.Value;
					if (value == null || !value.isAlive())
					{
						continue;
					}
					bool flag = Mutations.EggNow(value);
					string arg = Models.CreatureFamily(value);
					string text3 = Creatures.DrawnModel(value);
					string text4 = null;
					try
					{
						text4 = value.subspecies.egg_id;
					}
					catch
					{
					}
					Check(item.Key + ": " + value.asset.id + " in its egg (" + text4 + ") drawn as " + (text3 ?? "nothing"), flag && text3 == item.Key, $"egg {flag}, family {arg}");
					try
					{
						SpriteImage spriteImage = WorldBoxMod.Instance.SpriteImageForTest(value.getSpriteToRender());
						if (spriteImage != null)
						{
							Note($"{item.Key}: sprite {spriteImage.Width}x{spriteImage.Height} unit {spriteImage.UnitWidth:F2}x{spriteImage.UnitHeight:F2} visible {spriteImage.VisibleFraction:F2}, scale {value.current_scale.y:F2}, tall {spriteImage.UnitHeight * Mathf.Abs(value.current_scale.y) * spriteImage.VisibleFraction:F2} tiles");
						}
					}
					catch (Exception ex2)
					{
						Note("sprite: " + ex2.Message);
					}
				}
				Config.paused = false;
				foreach (KeyValuePair<string, Actor> item2 in row)
				{
					CivilTest.Kill(item2.Value);
				}
				yield return CivilTest.Wait(0.3f);
				Corpses.Clear();
				Mod.DebugEyeSet = false;
				yield return CivilTest.Wait(0.4f);
			}
		}
	}
}
