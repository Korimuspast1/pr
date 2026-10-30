using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ZombieGearTest : MonoBehaviour
	{
		private sealed class Case
		{
			public string Race;

			public bool Woman;

			public string Helmet;

			public string Chest;

			public string Boots;

			public Actor Living;

			public Actor Dead;

			public Vector2 At;

			public string Label => Race + (Woman ? " woman" : " man") + " in " + (Helmet ?? "-") + "/" + (Chest ?? "-") + "/" + (Boots ?? "-");
		}

		private static readonly string[] Races = new string[4] { "human", "elf", "orc", "dwarf" };

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private readonly StringBuilder _sprites = new StringBuilder();

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			WorldTile home = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					WorldTile current = enumerator.Current;
					home = current;
				}
			}
			if (home == null)
			{
				Log.Error("ZG no site");
				Finish(quit);
				yield break;
			}
			Actor host = null;
			try
			{
				host = World.world.units.spawnNewUnit("human", home, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("ZG host: " + ex.Message);
			}
			if (host == null)
			{
				Log.Error("ZG no host");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			ControllableUnit.setControllableCreature(host);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("ZG first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			WorldTile worldTile = home;
			float num = float.MaxValue;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				float num2 = Roughness(item.x, item.y);
				if (num2 < num)
				{
					num = num2;
					worldTile = item;
				}
			}
			Log.Info($"ZG stage {worldTile.x},{worldTile.y}, roughness {num:F0}, resolution {_mod.Renderer.Width}x{_mod.Renderer.Height}");
			Check("the game's transformation copy is taken over (ZombieGear)", ZombieGear.Installed);
			try
			{
				host.current_position = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y - 12f);
			}
			catch
			{
			}
			Vector2 eye = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f - 3.5f);
			yield return Run("rise", Rise(eye, s));
			s.TimeMode = savedMode;
			Config.paused = false;
			try
			{
				File.WriteAllText(Path.Combine(Log.Folder, "zg_sprites.txt"), _sprites.ToString());
			}
			catch (Exception ex2)
			{
				Log.Error("ZG sprites: " + ex2.Message);
			}
			Log.Info($"ZG {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private void Check(string what, bool ok)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("ZG " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("ZG --- " + name);
			while (true)
			{
				object current;
				try
				{
					if (!part.MoveNext())
					{
						break;
					}
					current = part.Current;
					goto IL_007b;
				}
				catch (Exception ex)
				{
					Log.Error("ZG " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			Config.paused = false;
		}

		private static float Roughness(int cx, int cy)
		{
			int num = int.MaxValue;
			int num2 = int.MinValue;
			int num3 = 0;
			for (int i = cy - 6; i <= cy + 8; i++)
			{
				for (int j = cx - 9; j <= cx + 9; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.main_type == null)
					{
						num3++;
						continue;
					}
					if (tile.main_type.liquid || tile.main_type.block)
					{
						num3++;
					}
					num = Mathf.Min(num, tile.main_type.height_min);
					num2 = Mathf.Max(num2, tile.main_type.height_min);
				}
			}
			return (float)num3 * 100f + (float)(num2 - num);
		}

		private static void ClearBox(int x0, int y0, int x1, int y1)
		{
			for (int i = y0; i <= y1; i++)
			{
				for (int j = x0; j <= x1; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.building != null)
					{
						try
						{
							tile.building.removeBuildingFinal();
						}
						catch
						{
						}
					}
				}
			}
		}

		private Actor Spawn(string id, Vector2 at)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile == null || tile.main_type == null || tile.main_type.liquid)
			{
				return null;
			}
			try
			{
				return World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("ZG spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private static void Put(Actor a, string id)
		{
			if (id == null)
			{
				return;
			}
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null || a.equipment == null)
				{
					Log.Error("ZG no item " + id);
					return;
				}
				ActorEquipmentSlot slot = a.equipment.getSlot(equipmentAsset.equipment_type);
				Item item = World.world.items.generateItem(equipmentAsset, a.kingdom, a.getName(), 1, a, 0, pByPlayer: true);
				if (item == null)
				{
					Log.Error("ZG item not made " + id);
					return;
				}
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				slot.setItem(item, a);
				a.setStatsDirty();
			}
			catch (Exception ex)
			{
				Log.Error("ZG wear " + id + ": " + ex.Message);
			}
		}

		private void Look(Vector2 at, float yaw, float pitch)
		{
			_mod.DebugEye = at;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(pitch);
		}

		private static string Hex(uint c)
		{
			return (c & 0xFFFFFF).ToString("X6");
		}

		private void Dump(string tag, Actor a)
		{
			SpriteImage spriteImage = _mod.SpriteForTest(a);
			if (spriteImage == null || spriteImage.Pixels == null)
			{
				Log.Info("ZG " + tag + ": no sprite");
				return;
			}
			StringBuilder stringBuilder = new StringBuilder($"{tag} {spriteImage.Width} {spriteImage.Height}");
			uint[] pixels = spriteImage.Pixels;
			foreach (uint num in pixels)
			{
				stringBuilder.Append(' ').Append(num.ToString("x8"));
			}
			_sprites.AppendLine(stringBuilder.ToString());
			if (People.PaletteForTest(a, out var p))
			{
				Log.Info($"ZG {tag} colours: skin {Hex(p.Skin)} hair {Hex(p.Hair)} shirt {Hex(p.Shirt)} pants {Hex(p.Pants)} shoes {Hex(p.Shoes)} accent {Hex(p.Accent)}, long hair {p.LongHair}, beard {p.Beard}");
			}
			try
			{
				Texture2D texture2D = new Texture2D(spriteImage.Width * 8, spriteImage.Height * 8, TextureFormat.RGBA32, mipChain: false);
				Color32[] array = new Color32[texture2D.width * texture2D.height];
				for (int j = 0; j < texture2D.height; j++)
				{
					for (int k = 0; k < texture2D.width; k++)
					{
						uint num2 = spriteImage.Pixels[j / 8 * spriteImage.Width + k / 8];
						array[j * texture2D.width + k] = new Color32((byte)num2, (byte)(num2 >> 8), (byte)(num2 >> 16), (byte)(num2 >> 24));
					}
				}
				texture2D.SetPixels32(array);
				texture2D.Apply();
				File.WriteAllBytes(Path.Combine(Log.Folder, "zg_sprite_" + tag + ".png"), texture2D.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D);
			}
			catch (Exception ex)
			{
				Log.Error("ZG png: " + ex.Message);
			}
		}

		private IEnumerator Rise(Vector2 eye, Settings s)
		{
			s.TimeMode = 1;
			_mod.ForceRelight();
			ClearBox((int)eye.x - 9, (int)eye.y - 2, (int)eye.x + 9, (int)eye.y + 10);
			string[] array = new string[8] { "iron", "steel", "leather", "bronze", "silver", "mythril", "copper", "adamantine" };
			List<Case> cases = new List<Case>();
			for (int i = 0; i < 8; i++)
			{
				string text = array[i];
				cases.Add(new Case
				{
					Race = Races[i / 2],
					Woman = ((i & 1) == 1),
					Helmet = "helmet_" + text,
					Chest = "armor_" + text,
					Boots = "boots_" + text
				});
			}
			cases.Add(new Case
			{
				Race = "human",
				Woman = false
			});
			cases.Add(new Case
			{
				Race = "elf",
				Woman = true
			});
			cases.Add(new Case
			{
				Race = "orc",
				Woman = false,
				Helmet = "helmet_iron"
			});
			cases.Add(new Case
			{
				Race = "dwarf",
				Woman = true,
				Chest = "armor_leather",
				Boots = "boots_copper"
			});
			for (int j = 0; j < cases.Count; j++)
			{
				Case obj = cases[j];
				obj.At = eye + new Vector2(((float)j - (float)(cases.Count - 1) * 0.5f) * 1.25f, 5f + (float)(j % 2) * 0.6f);
				obj.Living = Spawn(obj.Race, obj.At);
				if (obj.Living == null)
				{
					Log.Error("ZG could not spawn " + obj.Label);
					continue;
				}
				try
				{
					obj.Living.data.sex = (obj.Woman ? ActorSex.Female : ActorSex.Male);
				}
				catch (Exception ex)
				{
					Log.Error("ZG sex: " + ex.Message);
				}
				Put(obj.Living, obj.Helmet);
				Put(obj.Living, obj.Chest);
				Put(obj.Living, obj.Boots);
			}
			cases.RemoveAll((Case c) => c.Living == null);
			Look(eye + new Vector2(0f, -4f), MathF.PI / 2f, -6f);
			Config.paused = true;
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("zg_living.png");
			foreach (Case item in cases)
			{
				Dump(item.Race + (item.Woman ? "_f" : "_m") + "_living", item.Living);
			}
			HashSet<long> before = new HashSet<long>();
			foreach (Actor unit in World.world.units)
			{
				try
				{
					if (unit.asset != null && unit.asset.unit_zombie)
					{
						before.Add(unit.data.id);
					}
				}
				catch
				{
				}
			}
			int corpses = Corpses.Count;
			int dressed = ZombieGear.Dressed;
			Config.paused = false;
			foreach (Case item2 in cases)
			{
				try
				{
					item2.Living.addTrait("infected");
					((BaseSimObject)item2.Living).getHit((float)item2.Living.getHealth() + 9999f, pFlash: true, AttackType.Other, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
				}
				catch (Exception ex2)
				{
					Log.Error("ZG strike " + item2.Label + ": " + ex2.Message);
				}
				try
				{
					if (item2.Living.isAlive() && !item2.Living.isAlreadyTransformed())
					{
						Log.Info("ZG " + item2.Label + " survived the blow: raised directly");
						ActionLibrary.turnIntoZombie(item2.Living, item2.Living.current_tile);
					}
				}
				catch (Exception ex3)
				{
					Log.Error("ZG raise " + item2.Label + ": " + ex3.Message);
				}
			}
			yield return null;
			Config.paused = true;
			List<Actor> list = new List<Actor>();
			foreach (Actor unit2 in World.world.units)
			{
				try
				{
					if (unit2.isAlive() && unit2.asset != null && unit2.asset.unit_zombie && !before.Contains(unit2.data.id))
					{
						list.Add(unit2);
					}
				}
				catch
				{
				}
			}
			Log.Info($"ZG {list.Count} zombies risen from {cases.Count} villagers, {ZombieGear.Dressed - dressed} dressed by ZombieGear");
			foreach (Case item3 in cases)
			{
				Actor actor = null;
				float num = 1.5f;
				foreach (Actor item4 in list)
				{
					float magnitude = (item4.current_position - item3.At).magnitude;
					if (magnitude < num)
					{
						num = magnitude;
						actor = item4;
					}
				}
				item3.Dead = actor;
				list.Remove(actor);
			}
			Check("every villager rose as a zombie", cases.All((Case c) => c.Dead != null));
			yield return new WaitForSecondsRealtime(1.5f);
			Check($"no corpse fell where they rose ({Corpses.Count - corpses} new)", Corpses.Count <= corpses);
			foreach (Case item5 in cases)
			{
				Actor dead = item5.Dead;
				if (dead != null)
				{
					bool flag = Models.IsFemale(dead);
					string text2 = Models.PersonFamily(dead);
					string text3 = ((item5.Race == "human") ? "person_zombie" : ("person_zombie_" + item5.Race)) + (item5.Woman ? "_fem" : "");
					Check(item5.Label + ": rose as " + dead.asset.id + ", drawn as " + text2, text2 == text3 && flag == item5.Woman && Models.Get(text2) != null);
					ZombieGear.Wore(dead, out var helmet, out var chest, out var boots);
					Check(item5.Label + ": wears " + (helmet ?? "-") + "/" + (chest ?? "-") + "/" + (boots ?? "-"), helmet == item5.Helmet && chest == item5.Chest && boots == item5.Boots);
					bool flag2 = false;
					try
					{
						flag2 = dead.hasEquipment() && dead.equipment.getItems().Any();
					}
					catch
					{
					}
					Check(item5.Label + ": the game gave the zombie no items", !flag2);
					int num2 = Enumerable.Count((from x in new string[3] { item5.Helmet, item5.Chest, item5.Boots }
						where x != null
						select ArmorMeshes.MetalOf(x)).Distinct());
					int num3 = People.ArmorShown(dead);
					bool flag3 = num2 == 1 && item5.Helmet != null && item5.Chest == null && item5.Boots == null && Models.Get(text2)?.Info("hat") == "1";
					Check($"{item5.Label}: drawn in {num3} armour models (wants {num2})", num3 == num2 || flag3);
					Dump(item5.Race + (item5.Woman ? "_f" : "_m") + "_zombie", dead);
				}
			}
			int savedRes = s.Resolution;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("zg_row.png");
			Look(eye + new Vector2(0f, -1.2f), MathF.PI / 2f, -4f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("zg_row_near.png");
			for (int k = 0; k + 1 < cases.Count; k += 2)
			{
				if (cases[k].Dead != null && cases[k + 1].Dead != null)
				{
					Vector2 vector = (cases[k].Dead.current_position + cases[k + 1].Dead.current_position) * 0.5f;
					Look(vector + new Vector2(0.3f, -2f), 1.7207963f, -3f);
					yield return new WaitForSecondsRealtime(0.8f);
					_mod.SaveFrame($"zg_close{k / 2}.png");
				}
			}
			for (int k = 0; k < cases.Count; k++)
			{
				Actor dead2 = cases[k].Dead;
				if (dead2 != null)
				{
					Look(dead2.current_position + new Vector2(0f, -1.15f), MathF.PI / 2f, 2f);
					yield return new WaitForSecondsRealtime(0.5f);
					_mod.SaveFrame(string.Format("zg_face{0}_{1}{2}.png", k, cases[k].Race, cases[k].Woman ? "_f" : "_m"));
				}
			}
			Look(eye + new Vector2(0f, 10f), -MathF.PI / 2f, -5f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("zg_back.png");
			Actor bait = Spawn("sheep", eye + new Vector2(0f, 1.5f));
			Config.paused = false;
			Look(eye + new Vector2(0f, -1.5f), MathF.PI / 2f, -2f);
			for (int k = 0; k < 6; k++)
			{
				yield return new WaitForSecondsRealtime(0.6f);
				_mod.SaveFrame($"zg_walk_{k}.png");
			}
			int num4 = cases.Count((Case c) => c.Dead != null && c.Dead.isAlive() && People.ArmorShown(c.Dead) > 0);
			Log.Info($"ZG walking: {num4} zombies drawn in armour");
			s.Resolution = savedRes;
			Config.paused = true;
			foreach (Case item6 in cases)
			{
				try
				{
					if (item6.Dead != null && item6.Dead.isAlive())
					{
						item6.Dead.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			try
			{
				if (bait != null && bait.isAlive())
				{
					bait.dieSimpleNone();
				}
			}
			catch
			{
			}
			Config.paused = false;
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("ZG DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
