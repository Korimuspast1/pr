using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Gallery : MonoBehaviour
	{
		private const int ThumbW = 240;

		private const int ThumbH = 135;

		private const int RowsPerSheet = 12;

		private static readonly float[] Angles = new float[3] { 90f, 0f, 45f };

		private static readonly string[] ZooSpecies = new string[20]
		{
			"human", "elf", "orc", "dwarf", "sheep", "cow", "chicken", "wolf", "bear", "cat",
			"dog", "rabbit", "fox", "crab", "rat", "monkey", "penguin", "turtle", "snake", "frog"
		};

		private static readonly string[] AllSpecies = new string[80]
		{
			"human", "fox", "dog", "wolf", "hyena", "cat", "raccoon", "bear", "cow", "buffalo",
			"rhino", "sheep", "goat", "alpaca", "unicorn", "capybara", "rabbit", "rat", "armadillo", "monkey",
			"garl", "chicken", "penguin", "ostrich", "crocodile", "turtle", "snake", "lemon_snail", "frog", "seal",
			"crab", "scorpion", "ant_red", "sand_spider", "beetle", "bee", "fly", "butterfly", "grasshopper", "acid_blob",
			"fire_elemental_blob", "ghost", "jumpy_skull", "fire_skull", "snowman", "fire_elemental", "fire_elemental_horse", "fire_elemental_slug", "fire_elemental_snake", "crystal_sword",
			"smore", "flower_bud", "angle", "fairy", "greg", "mush_unit", "mush_animal", "lil_pumpkin", "assimilator", "printer",
			"tumor_monster_unit", "living_plants", "plague_doctor", "zombie_animal_cow", "dragon", "UFO", "civ_cat", "civ_dog", "civ_crab", "civ_frog",
			"civ_chicken", "civ_bear", "civ_rhino", "civ_unicorn", "piranha", "boat_fishing", "boat_trading_human", "boat_transport_human", "boat_trading_orc", "boat_transport_orc"
		};

		private IEnumerator Start()
		{
			string flag = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(flag);
			bool quit = options.Contains("quit");
			bool sheets = !options.Contains("nosheets");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				if (World.world.units.Count != 0)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			yield return new WaitForSecondsRealtime(4f);
			Actor host = null;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.canBePossessed() && Dialogue.IsPerson(simple) && simple.city != null)
				{
					host = simple;
					break;
				}
			}
			if (host == null)
			{
				foreach (Actor simple2 in World.world.units.getSimpleList())
				{
					if (simple2 != null && simple2.isAlive() && simple2.canBePossessed())
					{
						host = simple2;
						break;
					}
				}
			}
			if (host == null)
			{
				Log.Error("GALLERY no creature to possess");
				yield break;
			}
			ControllableUnit.setControllableCreature(host);
			yield return new WaitForSecondsRealtime(1.5f);
			WorldBoxMod mod = WorldBoxMod.Instance;
			if (mod == null || !mod.IsFirstPerson)
			{
				Log.Error("GALLERY first person did not start");
				yield break;
			}
			mod.HintUntil = 0f;
			bool gpu = mod.Settings.GpuRender;
			mod.Settings.GpuRender = false;
			bool zoo = File.ReadAllText(flag).Contains("zoo");
			List<Actor> spawned = new List<Actor>();
			if (zoo)
			{
				spawned = SpawnZoo(host, options.Contains("allzoo") ? AllSpecies : ZooSpecies);
				if (spawned.Count > 0)
				{
					LogFlatPlants(spawned[0].current_position);
				}
				Log.Info($"GALLERY spawned {spawned.Count} test creatures");
				yield return new WaitForSecondsRealtime(2f);
			}
			List<KeyValuePair<string, BaseSimObject>> list = new List<KeyValuePair<string, BaseSimObject>>();
			HashSet<string> hashSet = new HashSet<string>();
			if (zoo)
			{
				foreach (Actor item in spawned)
				{
					if (item != null && item.isAlive() && hashSet.Add("u:" + item.asset.id))
					{
						list.Add(new KeyValuePair<string, BaseSimObject>("unit " + item.asset.id, item));
					}
				}
			}
			else
			{
				foreach (Building simple3 in World.world.buildings.getSimpleList())
				{
					if (simple3 != null && simple3.isAlive() && simple3.asset != null && simple3.current_tile != null && hashSet.Add("b:" + simple3.asset.id))
					{
						list.Add(new KeyValuePair<string, BaseSimObject>(simple3.asset.building_type.ToString() + " " + simple3.asset.id, simple3));
					}
				}
				foreach (Actor simple4 in World.world.units.getSimpleList())
				{
					if (simple4 != null && simple4.isAlive() && simple4 != host && simple4.asset != null && hashSet.Add("u:" + simple4.asset.id))
					{
						list.Add(new KeyValuePair<string, BaseSimObject>("unit " + simple4.asset.id, simple4));
					}
				}
			}
			if (!sheets)
			{
				list.Clear();
			}
			Log.Info($"GALLERY {list.Count} kinds");
			List<string> labels = new List<string>();
			Color32[] sheet = null;
			int sheetIndex = 0;
			int row = 0;
			foreach (KeyValuePair<string, BaseSimObject> subject in list)
			{
				if (subject.Value == null || !subject.Value.isAlive())
				{
					Log.Info("GALLERY " + subject.Key + ": gone before its photo");
					continue;
				}
				if (sheet == null)
				{
					sheet = new Color32[240 * Angles.Length * 135 * 12];
				}
				Vector2 at;
				float num;
				if (subject.Value is Building building)
				{
					at = new Vector2(building.current_tile.posV3.x, building.current_tile.posV3.y);
					num = Mathf.Max(1f, (building.asset.fundament != null) ? ((float)Mathf.Max(building.asset.fundament.width, building.asset.fundament.height)) : 1f);
				}
				else
				{
					at = ((Actor)subject.Value).current_position;
					Actor actor = (Actor)subject.Value;
					num = Mathf.Max(0.6f, Mathf.Abs(actor.current_scale.y) * 12f);
					try
					{
						SpriteImage spriteImage = mod.SpriteImageForTest(actor.getSpriteToRender());
						if (spriteImage != null)
						{
							num = Mathf.Max(0.35f, spriteImage.UnitWidth * Mathf.Abs(actor.current_scale.x) * Models.OpaqueWidth(spriteImage)) * 0.9f;
						}
					}
					catch
					{
					}
				}
				float t = ((subject.Value is Actor) ? (0.9f + num * 1.5f) : (1.6f + num * 1.4f));
				if (subject.Value is Actor actor2 && actor2.asset != null && (actor2.asset.is_boat || actor2.asset.id == "dragon" || actor2.asset.id == "UFO" || actor2.asset.id == "greg" || actor2.asset.id.StartsWith("living_", StringComparison.Ordinal)))
				{
					t = (actor2.asset.is_boat ? 10f : 7f);
				}
				for (int k = 0; k < Angles.Length; k++)
				{
					float num2 = Angles[k] * (MathF.PI / 180f);
					Vector2 vector = new Vector2(Mathf.Cos(num2), Mathf.Sin(num2));
					if (subject.Value is Actor actor3)
					{
						if (!actor3.isAlive())
						{
							break;
						}
						at = actor3.current_position;
					}
					mod.DebugEye = at - vector * t;
					mod.DebugEyeSet = true;
					mod.SetYaw(num2);
					mod.SetPitchDegrees(-8f);
					for (int f = 0; f < 4; f++)
					{
						yield return null;
					}
					Blit(mod, sheet, k, row);
					if (k == 0 && subject.Value is Actor actor4)
					{
						Log.Info("GALLERY " + subject.Key + ": " + mod.DescribeForTest(actor4));
						mod.SaveFrame("zoo_" + actor4.asset.id + ".png");
					}
				}
				labels.Add($"sheet {sheetIndex} row {row}: {subject.Key}");
				row++;
				if (row == 12)
				{
					Save(sheet, sheetIndex++, row);
					sheet = null;
					row = 0;
				}
			}
			if (sheet != null && row > 0)
			{
				Save(sheet, sheetIndex, row);
			}
			File.WriteAllLines(Path.Combine(Log.Folder, "gallery.txt"), labels.ToArray());
			if (zoo)
			{
				List<Actor> people = spawned.FindAll((Actor a) => a != null && a.isAlive() && Dialogue.IsPerson(a));
				Log.Info($"GALLERY {people.Count} people among the test creatures");
				if (people.Count > 0)
				{
					Actor actor5 = people[0];
					Vector2 current_position = actor5.current_position;
					mod.DebugEye = current_position + new Vector2(0f, -1.6f);
					mod.DebugEyeSet = true;
					mod.SetYaw(MathF.PI / 2f);
					mod.SetPitchDegrees(0f);
					mod.Talk.Talk(host, actor5);
					yield return new WaitForSecondsRealtime(1.6f);
					yield return Capture("gallery_talk.png");
					Log.Info("GALLERY talk line: " + mod.Talk.Line);
				}
				mod.DebugEye = ((spawned.Count > 0 && spawned[0] != null) ? spawned[0].current_position : host.current_position) + new Vector2(2f, -2f);
				mod.DebugEyeSet = true;
				mod.SetPitchDegrees(-32f);
				yield return new WaitForSecondsRealtime(0.6f);
				yield return Capture("gallery_ground.png");
				mod.DebugEyeSet = false;
				mod.SetPitchDegrees(-5f);
				mod.EnterRoomForTest(people.GetRange(0, Math.Min(2, people.Count)));
				yield return new WaitForSecondsRealtime(1.2f);
				yield return Capture("gallery_room.png");
				if (people.Count > 0)
				{
					mod.SetYaw(Mathf.Atan2(mod.Interior.Room.Spots[0][1] - mod.Interior.Eye.y, mod.Interior.Room.Spots[0][0] - mod.Interior.Eye.x));
					mod.Talk.Talk(host, people[0]);
					yield return new WaitForSecondsRealtime(1.6f);
					yield return Capture("gallery_room_talk.png");
				}
				mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(0.5f);
			}
			mod.DebugEyeSet = false;
			if (options.Contains("work"))
			{
				Actor worker = (Dialogue.IsPerson(host) ? host : spawned.Find((Actor a) => a != null && a.isAlive() && Dialogue.IsPerson(a)));
				if (worker != null && worker != host)
				{
					ControllableUnit.setControllableCreature(worker);
					yield return new WaitForSecondsRealtime(1.5f);
				}
				yield return WorkTest(mod, worker ?? host, spawned);
			}
			ControllableUnit.clear(pCallKill: false);
			int num3 = 0;
			foreach (Actor item2 in spawned)
			{
				if (item2 != null && item2.isAlive())
				{
					try
					{
						item2.dieSimpleNone();
						num3++;
					}
					catch (Exception ex)
					{
						Log.Error("GALLERY could not remove test creature: " + ex.Message);
					}
				}
			}
			if (spawned.Count > 0)
			{
				Log.Info($"GALLERY removed {num3} test creatures");
			}
			mod.Settings.GpuRender = gpu;
			Log.Info("GALLERY DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator WorkTest(WorldBoxMod mod, Actor host, List<Actor> spawned)
		{
			Building tree = null;
			float num = float.MaxValue;
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || simple.asset == null || simple.chopped || simple.current_tile == null || simple.asset.building_type != BuildingType.Building_Tree)
				{
					continue;
				}
				WorldTile tile = World.world.GetTile(simple.current_tile.x, simple.current_tile.y - 2);
				if (tile != null && tile.building == null && tile.main_type != null && !tile.main_type.liquid)
				{
					float sqrMagnitude = (new Vector2(simple.current_tile.x, simple.current_tile.y) - host.current_position).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						tree = simple;
					}
				}
			}
			if (tree == null)
			{
				Log.Error("GALLERY WORK no tree to chop");
			}
			else
			{
				host.cancelAllBeh();
				host.setCurrentTilePosition(World.world.GetTile(tree.current_tile.x, tree.current_tile.y - 2));
				yield return new WaitForSecondsRealtime(0.6f);
				Vector2 vector = new Vector2(tree.current_tile.posV3.x, tree.current_tile.posV3.y);
				Vector2 to = vector - host.current_position;
				mod.SetYaw(Mathf.Atan2(to.y, to.x));
				mod.SetPitchDegrees(-6f);
				mod.Holstered = true;
				yield return new WaitForSecondsRealtime(0.6f);
				Log.Info(string.Format("GALLERY WORK tree {0} at {1:F2} tiles, picked {2}, target {3}, prompt '{4}'", tree.asset.id, to.magnitude, (mod.Picked is Building building) ? building.asset.id : (mod.Picked?.ToString() ?? "nothing"), mod.Work.Target != null, mod.Prompt));
				mod.Work.ForceSwing = true;
				bool shot = false;
				for (float t = 0f; t < 10f; t += Time.unscaledDeltaTime)
				{
					if (tree.chopped)
					{
						break;
					}
					if (!tree.isAlive())
					{
						break;
					}
					if (!shot && mod.Work.Progress > 0.3f && mod.Feedback.SinceAttack < 0.15f)
					{
						shot = true;
						yield return Capture("gallery_chop.png");
					}
					yield return null;
				}
				mod.Work.ForceSwing = false;
				mod.Holstered = false;
				yield return null;
				yield return null;
				Log.Info($"GALLERY WORK chopped {tree.chopped} alive {tree.isAlive()}, bag {mod.Work.BagCount}: " + string.Join(", ", mod.Work.Bag.ConvertAll((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value).ToArray()));
				yield return new WaitForSecondsRealtime(0.45f);
				yield return Capture("gallery_fall.png");
				yield return new WaitForSecondsRealtime(1f);
				yield return Capture("gallery_fallen.png");
			}
			host.addToInventory("wood", 8);
			host.addToInventory("stone", 6);
			host.addToInventory("common_metals", 4);
			yield return new WaitForSecondsRealtime(0.3f);
			if (!mod.Craft.Open)
			{
				mod.Craft.Toggle();
			}
			yield return new WaitForSecondsRealtime(1.2f);
			yield return Capture("gallery_craft.png");
			foreach (Crafting.Recipe recipe in mod.Craft.Recipes)
			{
				Log.Info($"GALLERY RECIPE {recipe.Slot} {recipe.Id} '{recipe.Name}' costs {recipe.Cost} afford {Crafting.CanAfford(host, recipe)}");
			}
			Crafting.Recipe pick = mod.Craft.Recipes.Find((Crafting.Recipe r) => r.Asset != null && r.Slot == EquipmentType.Weapon && Crafting.CanAfford(host, r) && r.Res1 != "wood") ?? mod.Craft.Recipes.Find((Crafting.Recipe r) => r.Asset != null && Crafting.CanAfford(host, r));
			if (pick != null)
			{
				string before = host.getWeaponAsset()?.id ?? "none";
				mod.Craft.Start(host, pick, mod.Toast);
				yield return new WaitForSecondsRealtime(4.6f);
				Log.Info("GALLERY CRAFT made " + pick.Id + ": weapon " + before + " -> " + (host.getWeaponAsset()?.id ?? "none") + ", toast '" + mod.ToastText + "'");
			}
			else
			{
				Log.Error("GALLERY CRAFT nothing affordable");
			}
			mod.Craft.Open = false;
			mod.SetPitchDegrees(-10f);
			yield return new WaitForSecondsRealtime(0.8f);
			yield return Capture("gallery_crafted.png");
			Actor armed = spawned.Find((Actor a) => a != null && a != host && a.isAlive() && People.IsHumanoid(a));
			if (armed != null)
			{
				try
				{
					EquipmentAsset pItemAsset = AssetManager.items.get("sword_iron");
					Item pItem = World.world.items.generateItem(pItemAsset, armed.kingdom, armed.getName(), 1, armed);
					armed.equipment.getSlot(EquipmentType.Weapon).setItem(pItem, armed);
					armed.setStatsDirty();
				}
				catch (Exception ex)
				{
					Log.Error("GALLERY could not arm a villager: " + ex.Message);
				}
				armed.makeWait(20f);
				yield return new WaitForSecondsRealtime(0.5f);
				mod.DebugEye = armed.current_position + new Vector2(0f, -1.3f);
				mod.DebugEyeSet = true;
				mod.SetYaw(MathF.PI / 2f);
				mod.SetPitchDegrees(-6f);
				mod.Talk.Talk(host, armed);
				yield return new WaitForSecondsRealtime(1.5f);
				yield return Capture("gallery_armed.png");
				mod.DebugEyeSet = false;
			}
			try
			{
				RuntimeManager.StudioSystem.getListenerAttributes(0, out var attributes);
				Log.Info($"GALLERY AUDIO listener at ({attributes.position.x:F1},{attributes.position.y:F1},{attributes.position.z:F2}) forward ({attributes.forward.x:F2},{attributes.forward.y:F2},{attributes.forward.z:F2}) up ({attributes.up.x:F1},{attributes.up.y:F1},{attributes.up.z:F1}); host at {host.current_position}, yaw {mod.View.Yaw:F2}; camera zoom {World.world.move_camera.getTargetZoom():F1}");
			}
			catch (Exception ex2)
			{
				Log.Error("GALLERY AUDIO " + ex2.Message);
			}
		}

		private static IEnumerator Capture(string name)
		{
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info("GALLERY captured " + name);
		}

		private static void LogFlatPlants(Vector2 at)
		{
			HashSet<string> hashSet = new HashSet<string>();
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || simple.asset == null || simple.current_tile == null || (new Vector2(simple.current_tile.x, simple.current_tile.y) - at).sqrMagnitude > 625f)
				{
					continue;
				}
				try
				{
					SpriteImage spriteImage = WorldBoxMod.Instance.SpriteImageForTest(simple.checkSpriteToRender());
					string item = simple.asset.id + ((spriteImage != null) ? (spriteImage.Width + "x" + spriteImage.Height) : "-");
					if (!hashSet.Add(item) || spriteImage == null)
					{
						continue;
					}
					int num = 0;
					uint[] pixels = spriteImage.Pixels;
					for (int i = 0; i < pixels.Length; i++)
					{
						if (pixels[i] >> 24 >= 128)
						{
							num++;
						}
					}
					Log.Info($"GALLERY nature {simple.asset.id} ({AssetStyle.Classify(simple.asset)}): sprite {spriteImage.Width}x{spriteImage.Height}, units {spriteImage.UnitWidth:F2}x{spriteImage.UnitHeight:F2}, scale {simple.current_scale.x:F2}, opaque {num * 100 / Math.Max(1, spriteImage.Pixels.Length)}%");
				}
				catch
				{
				}
			}
		}

		private static bool Wet(string id)
		{
			if (!id.StartsWith("boat_", StringComparison.Ordinal))
			{
				return id == "piranha";
			}
			return true;
		}

		private static List<Actor> SpawnZoo(Actor near, string[] species)
		{
			List<Actor> list = new List<Actor>();
			MapBox world = World.world;
			int num = (int)near.current_position.x;
			int num2 = (int)near.current_position.y;
			int num3 = -1;
			for (int i = 8; i < MapBox.height - 8; i += 4)
			{
				for (int j = 8; j < MapBox.width - 8; j += 4)
				{
					if (!Plain(world.GetTile(j, i)))
					{
						continue;
					}
					int num4 = 0;
					for (int k = -8; k <= 8; k += 4)
					{
						for (int l = -8; l <= 8; l += 4)
						{
							if (Plain(world.GetTile(j + l, i + k)))
							{
								num4++;
							}
						}
					}
					if (num4 > num3)
					{
						num3 = num4;
						num = j;
						num2 = i;
					}
				}
			}
			Log.Info($"GALLERY zoo centre {num},{num2} (open ground score {num3})");
			List<WorldTile> list2 = new List<WorldTile>();
			for (int m = 3; m < 90; m += 2)
			{
				if (list2.Count >= 12)
				{
					break;
				}
				for (int n = 0; n < 24; n++)
				{
					if (list2.Count >= 12)
					{
						break;
					}
					float f = (float)n / 24f * MathF.PI * 2f;
					WorldTile tile = world.GetTile(num + Mathf.RoundToInt(Mathf.Cos(f) * (float)m), num2 + Mathf.RoundToInt(Mathf.Sin(f) * (float)m));
					if (tile == null || tile.main_type == null || !tile.main_type.liquid || tile.main_type.lava)
					{
						continue;
					}
					bool flag = true;
					foreach (WorldTile item in list2)
					{
						if (Mathf.Abs(item.x - tile.x) + Mathf.Abs(item.y - tile.y) < 12)
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						list2.Add(tile);
					}
				}
			}
			List<WorldTile> list3 = new List<WorldTile>();
			for (int num5 = 4; num5 < 60; num5++)
			{
				if (list3.Count >= species.Length)
				{
					break;
				}
				for (int num6 = 0; num6 < 16; num6++)
				{
					if (list3.Count >= species.Length)
					{
						break;
					}
					float f2 = (float)num6 / 16f * MathF.PI * 2f;
					int num7 = num + Mathf.RoundToInt(Mathf.Cos(f2) * (float)num5);
					int num8 = num2 + Mathf.RoundToInt(Mathf.Sin(f2) * (float)num5);
					if (num7 < 0 || num8 < 0 || num7 >= MapBox.width || num8 >= MapBox.height)
					{
						continue;
					}
					WorldTile tile2 = world.GetTile(num7, num8);
					if (!Plain(tile2) || tile2.building != null)
					{
						continue;
					}
					bool flag2 = true;
					foreach (WorldTile item2 in list3)
					{
						if (Mathf.Abs(item2.x - num7) + Mathf.Abs(item2.y - num8) < 5)
						{
							flag2 = false;
							break;
						}
					}
					if (flag2)
					{
						list3.Add(tile2);
					}
				}
			}
			int num9 = 0;
			int num10 = 0;
			for (int num11 = 0; num11 < species.Length; num11++)
			{
				WorldTile worldTile = ((!Wet(species[num11])) ? ((num9 < list3.Count) ? list3[num9++] : null) : ((num10 < list2.Count) ? list2[num10++] : null));
				if (worldTile == null)
				{
					continue;
				}
				try
				{
					Actor actor = world.units.spawnNewUnit(species[num11], worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					if (actor == null)
					{
						Log.Info("GALLERY spawning " + species[num11] + " returned nothing");
						continue;
					}
					actor.makeWait(60f);
					list.Add(actor);
				}
				catch (Exception ex)
				{
					Log.Info("GALLERY no " + species[num11] + ": " + ex.Message);
				}
			}
			return list;
		}

		private static bool Plain(WorldTile t)
		{
			if (t != null && t.main_type != null && !t.main_type.liquid && !t.main_type.lava && t.main_type.height_min >= 98)
			{
				return t.main_type.height_min < 199;
			}
			return false;
		}

		private static void Blit(WorldBoxMod mod, Color32[] sheet, int column, int row)
		{
			FrameRenderer renderer = mod.Renderer;
			int num = 240 * Angles.Length;
			int num2 = 1620;
			for (int i = 0; i < 135; i++)
			{
				int num3 = num2 - 1 - (row * 135 + (134 - i));
				int num4 = i * renderer.Height / 135;
				for (int j = 0; j < 240; j++)
				{
					uint num5 = renderer.Pixels[num4 * renderer.Width + j * renderer.Width / 240];
					sheet[num3 * num + column * 240 + j] = new Color32((byte)(num5 & 0xFF), (byte)((num5 >> 8) & 0xFF), (byte)((num5 >> 16) & 0xFF), byte.MaxValue);
				}
			}
		}

		private static void Save(Color32[] sheet, int index, int rows)
		{
			int width = 240 * Angles.Length;
			int height = 1620;
			Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
			texture2D.SetPixels32(sheet);
			texture2D.Apply(updateMipmaps: false);
			string text = Path.Combine(Log.Folder, "gallery_" + index + ".png");
			File.WriteAllBytes(text, texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
			Log.Info("GALLERY wrote " + text + " (" + rows + " rows)");
		}
	}
}
