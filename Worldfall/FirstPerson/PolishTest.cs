using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PolishTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
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
			Log.Info($"POLISH world {MapBox.width}x{MapBox.height}, units {World.world.units.Count}");
			Actor king = null;
			Actor rivalKing = null;
			List<WorldTile> list = KingTest.GoodSites();
			WorldTile worldTile = null;
			foreach (WorldTile item in list)
			{
				king = KingTest.FoundKingdom(item, "human", 12, 2);
				if (king != null)
				{
					worldTile = item;
					break;
				}
			}
			if (king == null)
			{
				Log.Error("POLISH could not found a village");
				Finish(quit);
				yield break;
			}
			foreach (WorldTile item2 in list)
			{
				if (item2 == worldTile)
				{
					continue;
				}
				float num = Mathf.Sqrt((item2.x - worldTile.x) * (item2.x - worldTile.x) + (item2.y - worldTile.y) * (item2.y - worldTile.y));
				if (!(num < 25f) && !(num > 90f))
				{
					rivalKing = KingTest.FoundKingdom(item2, "orc", 10, 2);
					if (rivalKing != null)
					{
						break;
					}
				}
			}
			Log.Info("POLISH rival " + ((rivalKing != null) ? (WorldBoxMod.Describe(rivalKing) + " of " + Royal.NameOf(rivalKing.kingdom)) : "none"));
			yield return new WaitForSecondsRealtime(2f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("POLISH first person did not start");
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
			s.TimeMode = 1;
			_mod.ForceRelight();
			if (options.Contains("hudonly"))
			{
				yield return Run("hud", Hud(king));
				yield return Run("panels", Panels(king));
				s.TimeMode = savedMode;
				Finish(quit);
				yield break;
			}
			yield return Run("pace", Pace(king));
			yield return Run("collision", Collision(king));
			yield return Run("battle", Battle(king));
			if (rivalKing != null)
			{
				yield return Run("council", Council(king, rivalKing));
			}
			yield return Run("hands", Hands(king));
			yield return Run("hud", Hud(king));
			yield return Run("fire", Fire(king));
			yield return Run("clouds", Clouds(king));
			yield return Run("ages", Ages(king));
			yield return Run("rooms", Rooms(king));
			yield return Run("sounds", SoundCheck());
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("POLISH --- " + name);
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
					Log.Error("POLISH " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			_mod.DebugEyeSet = false;
		}

		private IEnumerator Pace(Actor unit)
		{
			ToOpenLand(unit, clear: true);
			yield return new WaitForSecondsRealtime(0.5f);
			bool[] array = new bool[3] { false, true, false };
			foreach (bool run in array)
			{
				PossessionHooks.ScriptedRun = run;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				_mod.SetYaw(UnityEngine.Random.value * MathF.PI * 2f);
				yield return new WaitForSecondsRealtime(0.6f);
				Vector2 start = unit.current_position;
				int steps = _mod.StepCount;
				float t0 = Time.realtimeSinceStartup;
				List<float> frames = new List<float>();
				while (Time.realtimeSinceStartup - t0 < 2f)
				{
					yield return null;
					frames.Add(Time.unscaledDeltaTime * 1000f);
				}
				float num = Time.realtimeSinceStartup - t0;
				float magnitude = (unit.current_position - start).magnitude;
				frames.Sort();
				float num2 = 0f;
				try
				{
					num2 = unit._current_combined_movement_speed;
				}
				catch
				{
				}
				Log.Info(string.Format("POLISH {0} through the game's own movement: {1:F2} tiles/s, pace {2:F2}, run blend {3:F2}, {4:F2} steps/s, unit speed {5:F2}, frame median {6:F1} ms", run ? "running" : "walking", magnitude / num, PossessionHooks.Pace, PossessionHooks.RunBlend, (float)(_mod.StepCount - steps) / num, num2, frames[frames.Count / 2]));
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				Vector2 before = unit.current_position;
				yield return new WaitForSecondsRealtime(0.4f);
				Log.Info($"POLISH glide after letting go: {(unit.current_position - before).magnitude:F3} tiles");
			}
			_mod.SaveFrame("polish_walk.png");
		}

		private WorldTile ToOpenLand(Actor unit)
		{
			return ToOpenLand(unit, clear: false);
		}

		private WorldTile ToOpenLand(Actor unit, bool clear)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 current_position = unit.current_position;
			for (int i = 0; i < 60; i += 2)
			{
				for (int j = 0; j < 24; j++)
				{
					float f = (float)j / 24f * MathF.PI * 2f;
					int num = (int)(current_position.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(current_position.y + Mathf.Sin(f) * (float)i);
					bool flag = num > 14 && num2 > 14 && num < terrainForTest.Width - 14 && num2 < terrainForTest.Height - 14;
					for (int k = -9; k <= 9 && flag; k += 3)
					{
						for (int l = -12; l <= 12 && flag; l += 3)
						{
							int num3 = (num2 + k) * terrainForTest.Width + num + l;
							flag = (terrainForTest.Flags[num3] & 0xB) == 0 && Mathf.Abs(terrainForTest.Ground[num3] - terrainForTest.Ground[num2 * terrainForTest.Width + num]) < 2.5f;
						}
					}
					if (!flag)
					{
						continue;
					}
					if (clear)
					{
						for (int m = -6; m <= 6 && flag; m++)
						{
							for (int n = -10; n <= 12 && flag; n++)
							{
								WorldTile tile = World.world.GetTile(num + n, num2 + m);
								flag = tile != null && tile.building == null && !tile.Type.liquid;
							}
						}
					}
					if (flag)
					{
						WorldTile tile2 = World.world.GetTile(num, num2);
						try
						{
							unit.setCurrentTilePosition(tile2);
						}
						catch (Exception ex)
						{
							Log.Error("POLISH move: " + ex.Message);
						}
						Log.Info($"POLISH moved to open land at {num},{num2}");
						return tile2;
					}
				}
			}
			return null;
		}

		private IEnumerator Collision(Actor unit)
		{
			ToOpenLand(unit);
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 current_position = unit.current_position;
			float num = 0f;
			_mod.SetYaw(num);
			Vector2 vector = new Vector2(Mathf.Cos(num), Mathf.Sin(num));
			WorldTile tile = World.world.GetTile((int)(current_position.x + vector.x * 6f), (int)(current_position.y + vector.y * 6f));
			Building house = null;
			try
			{
				house = World.world.buildings.addBuilding("house_human_2", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				house?.completeConstruction();
				if (house != null && unit.kingdom != null)
				{
					house.setKingdom(unit.kingdom);
				}
			}
			catch (Exception ex)
			{
				Log.Error("POLISH house: " + ex.Message);
			}
			if (house == null)
			{
				Log.Error("POLISH no house placed");
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			Vector2 hp = new Vector2(house.current_tile.posV3.x, house.current_tile.posV3.y);
			Vector2 vector2 = hp - unit.current_position;
			_mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
			_mod.SetPitchDegrees(0f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			PossessionHooks.ScriptedRun = true;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 4f)
			{
				yield return null;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			int num2 = 0;
			foreach (Collider item in Colliders.Near)
			{
				if (item.Shape != null)
				{
					num2++;
				}
			}
			float magnitude = (unit.current_position - hp).magnitude;
			Log.Info($"POLISH walked at a house: stopped {magnitude:F2} tiles from its middle (it is {house.asset.id}), {num2} model footprints near, eye pushed {(new Vector2(_mod.View.X, _mod.View.Y) - unit.current_position).magnitude:F3}");
			_mod.SaveFrame("polish_house_bump.png");
			PossessionHooks.ScriptedInput = new Vector2(1f, 0.6f);
			yield return new WaitForSecondsRealtime(1.5f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			Log.Info($"POLISH after sliding along it: {(unit.current_position - hp).magnitude:F2} tiles from its middle");
			_mod.SaveFrame("polish_house_slide.png");
		}

		private IEnumerator Hands(Actor unit)
		{
			string[] array = new string[10] { null, "sword_iron", "axe_bronze", "spear_steel", "hammer_iron", "bow_wood", "stick_wood", "flame_sword", "necromancer_staff", "alien_blaster" };
			_mod.SetPitchDegrees(-6f);
			string[] array2 = array;
			foreach (string id in array2)
			{
				if (!Equip(unit, id))
				{
					Log.Error("POLISH could not give " + id);
					continue;
				}
				yield return new WaitForSecondsRealtime(0.9f);
				string tag = id ?? "fists";
				_mod.SaveFrame("polish_hand_" + tag + "_ready.png");
				float[] array3 = new float[3] { 0.05f, 0.13f, 0.24f };
				foreach (float t in array3)
				{
					_mod.Feedback.Attacks++;
					_mod.Feedback.LastAttackMelee = id != "bow_wood" && id != "alien_blaster" && id != "necromancer_staff";
					_mod.Feedback.LastSwing = Swing.Attack;
					_mod.Feedback.SinceAttack = t;
					yield return null;
					_mod.SaveFrame($"polish_hand_{tag}_{(int)(t * 100f):D2}.png");
				}
			}
			Equip(unit, "sword_iron");
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				if (id != null)
				{
					EquipmentAsset equipmentAsset = AssetManager.items.get(id);
					if (equipmentAsset == null)
					{
						return false;
					}
					Item pItem = World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
					slot.setItem(pItem, unit);
				}
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("POLISH equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private IEnumerator Hud(Actor unit)
		{
			_mod.SetPitchDegrees(0f);
			yield return new WaitForSecondsRealtime(0.5f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_hud.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			int hp = unit.getHealth();
			try
			{
				unit.setHealth(Mathf.Max(1, unit.getMaxHealth() / 4));
			}
			catch (Exception ex)
			{
				Log.Error("POLISH health: " + ex.Message);
			}
			yield return new WaitForSecondsRealtime(0.4f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_hurt.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			try
			{
				((BaseSimObject)unit).addStatusEffect("burning", 3f, pColorEffect: true);
			}
			catch (Exception ex2)
			{
				Log.Error("POLISH burning: " + ex2.Message);
			}
			yield return new WaitForSecondsRealtime(0.5f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_burning.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			try
			{
				unit.finishStatusEffect("burning");
				unit.setHealth(hp);
			}
			catch
			{
			}
			Log.Info($"POLISH HUD shots taken; health {unit.getHealth()}/{unit.getMaxHealth()}, nutrition {unit.getNutritionRatio():F2}, level {unit.data?.level}");
		}

		private IEnumerator Panels(Actor unit)
		{
			Work work = _mod.Work;
			work.Bag.Clear();
			work.Bag.Add(new KeyValuePair<string, int>("wood", 6));
			work.Bag.Add(new KeyValuePair<string, int>("stone", 3));
			work.BagCount = 9;
			_mod.Toast("The council has sent bandits against Uh Axes");
			_mod.DebugPrompt = "E:Talk|R:Flirt|T:Recruit";
			yield return new WaitForSecondsRealtime(0.3f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_toast.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.Craft.Toggle();
			yield return new WaitForSecondsRealtime(0.5f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_craft.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.Craft.Open = false;
			_mod.Royal.Toggle(unit, _mod.Toast);
			yield return new WaitForSecondsRealtime(0.5f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_council.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.Royal.Open = false;
			_mod.HintUntil = Time.unscaledTime + 3f;
			yield return new WaitForSecondsRealtime(0.4f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_help.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.HintUntil = 0f;
			_mod.DebugPrompt = null;
			work.Bag.Clear();
			work.BagCount = 0;
			Log.Info("POLISH panel shots taken");
		}

		private IEnumerator Fire(Actor unit)
		{
			ToOpenLand(unit);
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 current_position = unit.current_position;
			float num = MathF.PI / 2f;
			_mod.SetYaw(num);
			_mod.SetPitchDegrees(-3f);
			Vector2 vector = new Vector2(Mathf.Cos(num), Mathf.Sin(num));
			int num2 = 0;
			for (int i = -3; i <= 3; i++)
			{
				for (int j = -3; j <= 3; j++)
				{
					WorldTile tile = World.world.GetTile((int)(current_position.x + vector.x * 9f) + j, (int)(current_position.y + vector.y * 9f) + i);
					if (tile != null && tile.startFire(pForce: true))
					{
						num2++;
					}
				}
			}
			Log.Info($"POLISH fires lit: {num2}");
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("polish_fire_day.png");
			_mod.Settings.TimeMode = 3;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("polish_fire_night.png");
			_mod.Settings.TimeMode = 1;
			_mod.ForceRelight();
			int num3 = 0;
			foreach (Billboard billboard in _mod.SceneForTest.Billboards)
			{
				if (billboard.Emissive)
				{
					num3++;
				}
			}
			Log.Info($"POLISH glowing sprites (flames, embers) in view: {num3}");
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Clouds(Actor unit)
		{
			Vector2 p = unit.current_position;
			string[] array = new string[3] { "cloud_rain", "cloud_acid", "cloud_lava" };
			foreach (string id in array)
			{
				WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
				try
				{
					EffectsLibrary.spawn("fx_cloud", tile, id);
				}
				catch (Exception ex)
				{
					Log.Error("POLISH cloud: " + ex.Message);
				}
				yield return new WaitForSecondsRealtime(4.5f);
				Weather w = _mod.SceneForTest.Weather;
				int mine = -1;
				for (int j = 0; j < w.Storms.Count; j++)
				{
					StormCloud stormCloud = w.Storms[j];
					Log.Info($"POLISH {id}: cloud {j} at {(new Vector2(stormCloud.X, stormCloud.Y) - p).magnitude:F1} tiles, size {stormCloud.HalfX:F1}x{stormCloud.HalfY:F1}, opacity {stormCloud.Opacity:F2}, drops {stormCloud.Drops}, glows {stormCloud.Glows}");
					if (stormCloud.Drops != Precip.Seeds && (mine < 0 || (new Vector2(stormCloud.X, stormCloud.Y) - p).sqrMagnitude < (new Vector2(w.Storms[mine].X, w.Storms[mine].Y) - p).sqrMagnitude))
					{
						mine = j;
					}
				}
				if (mine >= 0)
				{
					_mod.DebugEye = new Vector2(w.Storms[mine].X, w.Storms[mine].Y);
					_mod.DebugEyeSet = true;
					yield return new WaitForSecondsRealtime(1.2f);
				}
				_mod.SetPitchDegrees(30f);
				yield return new WaitForSecondsRealtime(0.4f);
				_mod.SaveFrame("polish_" + id + "_up.png");
				_mod.SetPitchDegrees(2f);
				yield return new WaitForSecondsRealtime(0.4f);
				_mod.SaveFrame("polish_" + id + "_level.png");
				if (mine >= 0)
				{
					StormCloud stormCloud2 = w.Storms[mine];
					_mod.DebugEye = new Vector2(stormCloud2.X, stormCloud2.Y - 40f);
					_mod.DebugEyeSet = true;
					_mod.SetYaw(MathF.PI / 2f);
					_mod.SetPitchDegrees(8f);
					yield return new WaitForSecondsRealtime(0.8f);
					_mod.SaveFrame("polish_" + id + "_far.png");
					_mod.DebugEyeSet = false;
				}
				try
				{
					foreach (BaseEffectController item in World.world.stack_effects.list)
					{
						if (item?._list == null)
						{
							continue;
						}
						foreach (BaseEffect item2 in item._list)
						{
							if (item2 is Cloud cloud && ((BaseEffect)cloud).active)
							{
								cloud.kill();
							}
						}
					}
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			try
			{
				MapBox.spawnLightningBig(World.world.GetTile((int)p.x + 6, (int)p.y + 6));
			}
			catch (Exception ex2)
			{
				Log.Error("POLISH lightning: " + ex2.Message);
			}
			yield return null;
			yield return null;
			_mod.SaveFrame("polish_lightning.png");
			Log.Info($"POLISH lightning: {_mod.SceneForTest.Weather.Bolts.Count} bolts, flash {_mod.SceneForTest.Weather.Flash:F2}");
			yield return new WaitForSecondsRealtime(1f);
		}

		private IEnumerator Ages(Actor unit)
		{
			WorldAgeManager ages = World.world.era_manager;
			WorldAgeAsset before = ages.getCurrentAge();
			_mod.SetPitchDegrees(12f);
			string[] array = new string[9] { "age_sun", "age_tears", "age_chaos", "age_wonders", "age_ice", "age_ash", "age_moon", "age_dark", "age_despair" };
			foreach (string id in array)
			{
				WorldAgeAsset worldAgeAsset = AssetManager.era_library.get(id);
				if (worldAgeAsset == null)
				{
					Log.Error("POLISH no age " + id);
					continue;
				}
				try
				{
					ages.setCurrentAge(worldAgeAsset, pOverrideTime: true);
				}
				catch (Exception ex)
				{
					Log.Error("POLISH age " + id + ": " + ex.Message);
					continue;
				}
				yield return new WaitForSecondsRealtime(6f);
				_mod.SaveFrame("polish_" + id + ".png");
				Log.Info($"POLISH {id}: weather {_mod.SceneForTest.Weather.Global} {_mod.SceneForTest.Weather.GlobalStrength:F2}, night mod {ages.getNightMod():F2}");
			}
			if (before != null)
			{
				try
				{
					ages.setCurrentAge(before, pOverrideTime: true);
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(5f);
		}

		private IEnumerator Council(Actor king, Actor rivalKing)
		{
			Royal royal = _mod.Royal;
			royal.Open = true;
			royal.Selected = rivalKing.kingdom;
			yield return new WaitForSecondsRealtime(0.8f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "polish_screen_council.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			City city = rivalKing.city;
			try
			{
				king.kingdom.data.total_kills += 40L;
			}
			catch
			{
			}
			royal.Unleash(king, city, Royal.Deed.Bandits, delegate(string m)
			{
				Log.Info("POLISH toast: " + m);
			}, delegate
			{
			});
			Log.Info($"POLISH raids under way: {royal.Raids.Count}, raiders {((royal.Raids.Count > 0) ? royal.Raids[0].Units.Count : 0)}");
			royal.Unleash(king, city, Royal.Deed.Meteor, delegate(string m)
			{
				Log.Info("POLISH toast: " + m);
			}, delegate
			{
			});
			royal.Open = false;
			yield return new WaitForSecondsRealtime(4f);
			if (royal.Raids.Count > 0)
			{
				Log.Info("POLISH raid status: " + royal.Raids[0].Status);
			}
			royal.DeclareWar(king, rivalKing.kingdom, delegate(string m)
			{
				Log.Info("POLISH toast: " + m);
			}, delegate
			{
			});
			royal.MakePeace(king, rivalKing.kingdom, delegate(string m)
			{
				Log.Info("POLISH toast: " + m);
			});
			royal.SendGift(king, rivalKing.kingdom, delegate(string m)
			{
				Log.Info("POLISH toast: " + m);
			});
			royal.Levy(king, delegate(string m)
			{
				Log.Info("POLISH toast: " + m);
			});
		}

		private IEnumerator Battle(Actor unit)
		{
			ToOpenLand(unit, clear: true);
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 p = unit.current_position;
			List<Actor> list = new List<Actor>();
			List<Actor> raiders = new List<Actor>();
			int num = 0;
			for (int i = 0; i < 6; i++)
			{
				try
				{
					WorldTile tile = World.world.GetTile((int)p.x + 3 + i % 3, (int)p.y + 4 + i / 3);
					Actor actor = World.world.units.spawnNewUnit("human", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					if (actor != null && unit.city != null)
					{
						actor.joinCity(unit.city);
						unit.city.makeWarrior(actor);
						list.Add(actor);
					}
					WorldTile tile2 = World.world.GetTile((int)p.x + 9 + i % 3, (int)p.y + 4 + i / 3);
					Actor actor2 = World.world.units.spawnNewUnit("bandit", tile2, i == 0, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					if (actor2 != null)
					{
						num++;
						raiders.Add(actor2);
					}
				}
				catch (Exception ex)
				{
					Log.Error("POLISH battle spawn: " + ex.Message);
				}
			}
			Log.Info($"POLISH skirmish: {list.Count} of ours against {num} bandits");
			_mod.DebugEye = p + new Vector2(7f, -3f);
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-6f);
			for (int k = 0; k < 12; k++)
			{
				yield return new WaitForSecondsRealtime(1f);
				if (k == 4)
				{
					foreach (Actor item in raiders)
					{
						try
						{
							if (item != null && item.isAlive())
							{
								item.setHealth(Mathf.Max(1, item.getMaxHealth() / 12));
							}
						}
						catch
						{
						}
					}
				}
				_mod.SaveFrame($"polish_battle_{k}.png");
				int num2 = 0;
				int num3 = 0;
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && (simple.current_position - p).sqrMagnitude < 400f)
					{
						if (simple.isFighting())
						{
							num2++;
						}
						if (simple.has_attack_target)
						{
							num3++;
						}
					}
				}
				Log.Info($"POLISH battle {k}: {num2} fighting, {num3} with a target, {Corpses.Count} bodies on the ground");
			}
			_mod.DebugEyeSet = false;
		}

		private IEnumerator Rooms(Actor unit)
		{
			HashSet<string> hashSet = new HashSet<string>();
			foreach (Building building in World.world.buildings)
			{
				if (building != null && building.asset != null && hashSet.Add(building.asset.id))
				{
					string text = HouseInterior.RoomName(building.asset);
					Log.Info("POLISH building " + building.asset.id + " (" + building.asset.type + "): " + ((text != null) ? ("enter as " + text) : "cannot enter"));
				}
			}
			yield break;
		}

		private IEnumerator SoundCheck()
		{
			for (int i = 0; i < 20; i++)
			{
				if (_mod.Audio.SoundKinds != 0)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Log.Info(string.Format("POLISH sounds: {0} kinds loaded, reverb {1}", _mod.Audio.SoundKinds, _mod.Audio.HasReverb ? "on" : "off"));
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
			Log.Info("POLISH DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
