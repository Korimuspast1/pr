using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FeatureTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private Actor _rival;

		private Vector2 _townCentre;

		private readonly List<Building> _wheat = new List<Building>();

		private Vector2 _farm;

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
			Log.Info($"FEAT world {MapBox.width}x{MapBox.height}, units {World.world.units.Count}");
			List<WorldTile> list = KingTest.GoodSites();
			WorldTile worldTile = null;
			foreach (WorldTile item in list)
			{
				_king = KingTest.FoundKingdom(item, "human", 14, 2);
				if (_king != null)
				{
					worldTile = item;
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("FEAT could not found a village");
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
				if (!(num < 30f) && !(num > 85f))
				{
					_rival = KingTest.FoundKingdom(item2, "orc", 12, 2);
					if (_rival != null)
					{
						break;
					}
				}
			}
			Log.Info("FEAT rival " + ((_rival != null) ? Royal.NameOf(_rival.kingdom) : "none"));
			BuildVillage(_king.city);
			BuildFarm(_king.city);
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("FEAT first person did not start");
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
			bool only = options.Contains("only");
			if (!only || options.Contains("pathsonly"))
			{
				yield return Run("paths", Paths());
			}
			if (!only || options.Contains("cropsonly"))
			{
				yield return Run("crops", Crops());
			}
			if (!only || options.Contains("bowonly"))
			{
				yield return Run("bow", Bow());
			}
			if (only && options.Contains("soundonly"))
			{
				yield return Run("sound", Sound());
			}
			if (only)
			{
				s.TimeMode = savedMode;
				Finish(quit);
				yield break;
			}
			yield return Run("bombs", Bombs());
			yield return Run("favourites", Favourites());
			if (_rival != null)
			{
				yield return Run("warmap", WarMapTest());
			}
			yield return Run("dragon", Dragon());
			yield return Run("music", Music());
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("FEAT --- " + name);
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
					Log.Error("FEAT " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private void BuildVillage(City city)
		{
			WorldTile tile = city.getTile();
			_townCentre = new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
			Kingdom kingdom = city.kingdom;
			string[] array = new string[12]
			{
				"hall_human_0", "house_human_1", "house_human_2", "house_human_1", "house_human_3", "house_human_0", "house_human_2", "well", "house_human_1", "house_human_2",
				"house_human_0", "house_human_1"
			};
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				float f = (float)i * 2.4f;
				float num2 = ((i == 0) ? 0f : (6f + (float)(i % 3) * 3.5f));
				int pX = (int)((float)tile.x + Mathf.Cos(f) * num2);
				int pY = (int)((float)tile.y + Mathf.Sin(f) * num2);
				WorldTile tile2 = World.world.GetTile(pX, pY);
				if (tile2 == null || tile2.Type == null || tile2.Type.liquid)
				{
					continue;
				}
				try
				{
					if (AssetManager.buildings.get(array[i]) == null)
					{
						Log.Error("FEAT no building " + array[i]);
						continue;
					}
					Building building = World.world.buildings.addBuilding(array[i], tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						building.completeConstruction();
						building.setKingdom(kingdom);
						city.listBuilding(building);
						num++;
					}
				}
				catch (Exception ex)
				{
					Log.Error("FEAT building " + array[i] + ": " + ex.Message);
				}
			}
			Log.Info($"FEAT village {city.name}: {num} buildings placed, {city.buildings.Count} in the city");
		}

		private void BuildFarm(City city)
		{
			WorldTile tile = city.getTile();
			int num = tile.x + 16;
			int num2 = tile.y - 2;
			float num3 = float.MaxValue;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			for (int i = 12; i < 30; i += 2)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j / 16f * MathF.PI * 2f;
					int num4 = (int)((float)tile.x + Mathf.Cos(f) * (float)i) - 4;
					int num5 = (int)((float)tile.y + Mathf.Sin(f) * (float)i) - 2;
					float num6 = float.MaxValue;
					float num7 = float.MinValue;
					bool flag = true;
					for (int k = -1; k <= 8 && flag; k++)
					{
						for (int l = -1; l <= 5 && flag; l++)
						{
							WorldTile tile2 = World.world.GetTile(num4 + k, num5 + l);
							if (tile2 == null || tile2.Type == null || tile2.Type.liquid || (tile2.building != null && tile2.building.isKingdomCiv()))
							{
								flag = false;
								break;
							}
							float b = ((terrainForTest.Width > 0) ? terrainForTest.Ground[tile2.y * terrainForTest.Width + tile2.x] : 0f);
							num6 = Mathf.Min(num6, b);
							num7 = Mathf.Max(num7, b);
						}
					}
					if (flag)
					{
						float num8 = (num7 - num6) * 10f + (float)i * 0.2f;
						if (num8 < num3)
						{
							num3 = num8;
							num = num4;
							num2 = num5;
						}
					}
				}
			}
			_farm = new Vector2((float)num + 4f, (float)num2 + 2.5f);
			int num9 = 0;
			for (int m = 0; m < 8; m++)
			{
				for (int n = 0; n < 5; n++)
				{
					WorldTile tile3 = World.world.GetTile(num + m, num2 + n);
					if (tile3 == null || tile3.Type == null || tile3.Type.liquid)
					{
						continue;
					}
					try
					{
						if (tile3.building != null)
						{
							tile3.building.removeBuildingFinal();
						}
						tile3.setTopTileType(TopTileLibrary.field);
						Building building = World.world.buildings.addBuilding("wheat", tile3, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
						if (building == null)
						{
							continue;
						}
						float num10 = (float)m / 7f;
						Wheat component_wheat = building.component_wheat;
						if (component_wheat != null)
						{
							if (num10 >= 0.99f)
							{
								component_wheat.growFull();
							}
							else
							{
								building.data.grow_time = building.asset.growth_time * (float)Mathf.Max(1, component_wheat._max_level) * num10;
							}
						}
						_wheat.Add(building);
						num9++;
					}
					catch (Exception ex)
					{
						Log.Error("FEAT wheat: " + ex.Message);
					}
				}
			}
			Log.Info($"FEAT farm: {num9} wheat sown at {num},{num2}");
		}

		private IEnumerator Paths()
		{
			yield return new WaitForSecondsRealtime(7f);
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Log.Info($"FEAT paths: {_mod.PathsForTest.TownCount} towns planned, {_mod.PathsForTest.StrokeCount} strokes, {terrainForTest.PathTileList.Count} tiles worn");
			Vector2 hub = _townCentre;
			if (_mod.PathsForTest.HubOf(_king.city.getID(), out var hub2))
			{
				hub = hub2;
			}
			Log.Info($"FEAT town square at {hub}, surface there {terrainForTest.SurfaceAt(hub.x, hub.y)}");
			_mod.DebugEyeSet = true;
			for (int k = 0; k < 3; k++)
			{
				float num = (float)k * 2.1f + 0.4f;
				_mod.DebugEye = hub - new Vector2(Mathf.Cos(num), Mathf.Sin(num)) * 1.5f;
				_mod.SetYaw(num);
				_mod.SetPitchDegrees(-10f);
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame($"feat_paths_street{k}.png");
			}
			_mod.DebugEye = hub + new Vector2(0f, -15f);
			_mod.DebugEyeHeight = 15f;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-42f);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("feat_paths_above.png");
		}

		private IEnumerator Crops()
		{
			foreach (Building item in _wheat)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						item.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			_wheat.Clear();
			BuildFarm(_king.city);
			_mod.DebugEye = _farm + new Vector2(-6f, -4f);
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(Mathf.Atan2(4f, 6f));
			_mod.SetPitchDegrees(-10f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("feat_crops_low.png");
			_mod.DebugEyeHeight = 5f;
			_mod.SetPitchDegrees(-30f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("feat_crops_high.png");
			Log.Info("FEAT crops: " + _mod.SceneForTest.CropReport());
			int num = 0;
			foreach (Building item2 in _wheat)
			{
				if (item2 != null && item2.isAlive())
				{
					num++;
				}
			}
			Log.Info($"FEAT wheat still standing: {num} of {_wheat.Count}");
			int reaped = 0;
			foreach (Building item3 in _wheat)
			{
				if (item3 != null && item3.isAlive() && SceneCollector.CropGrowth(item3) > 0.95f)
				{
					try
					{
						item3.removeBuildingFinal();
						reaped++;
					}
					catch
					{
					}
				}
			}
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.DebugEyeHeight = 0f;
			_mod.SetPitchDegrees(-12f);
			yield return new WaitForSecondsRealtime(0.8f);
			_mod.SaveFrame("feat_crops_reaped.png");
			Log.Info($"FEAT reaped {reaped}: stubble patches {_mod.SceneForTest.StubbleCount}");
		}

		private IEnumerator Bow()
		{
			_mod.DebugEyeSet = false;
			if (!Equip(_king, "bow_wood"))
			{
				Log.Error("FEAT no bow");
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			BowControl bow = _mod.Bow;
			Log.Info($"FEAT bow in hand {bow.InHand}, hold {PossessionHooks.HoldAttack}");
			_mod.SetPitchDegrees(-3f);
			_mod.SaveFrame("feat_bow_carry.png");
			bow.State.Pose(BowState.Phase.Drawing, 0.3f, 0.6f);
			yield return null;
			bow.State.Pose(BowState.Phase.Drawing, 0.3f, 0.6f);
			yield return null;
			_mod.SaveFrame("feat_bow_drawing.png");
			bow.State.Pose(BowState.Phase.Held, 0.4f, 1f);
			for (int i = 0; i < 20; i++)
			{
				bow.State.Pose(BowState.Phase.Held, 0.4f, 1f);
				yield return null;
			}
			_mod.SaveFrame("feat_bow_full.png");
			Vector2 p = _king.current_position;
			float yaw = _mod.ViewYaw;
			WorldTile at = World.world.GetTile((int)(p.x + Mathf.Cos(yaw) * 6f), (int)(p.y + Mathf.Sin(yaw) * 6f));
			Actor sheep = null;
			try
			{
				sheep = World.world.units.spawnNewUnit("sheep", at, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("FEAT sheep: " + ex.Message);
			}
			yield return new WaitForSecondsRealtime(0.5f);
			int attacks = _mod.Feedback.Attacks;
			int projectiles = ((World.world.projectiles != null) ? World.world.projectiles.list.Count : 0);
			bow.LooseForTest(_king);
			for (int i = 0; i < 40; i++)
			{
				if (!PossessionHooks.ReleaseAttack)
				{
					break;
				}
				if (i < 12)
				{
					string text = ((_mod.Work.Target == null) ? "-" : _mod.Work.Target.asset?.id);
					Log.Info($"FEAT bow frame {i}: release {PossessionHooks.ReleaseAttack}, hold {PossessionHooks.HoldAttack}, block {PossessionHooks.BlockAttack}, pressed {ControllableUnit._attack_pressed_button_left}, timer {_king.attack_timer:F2}, ready {_king.isAttackReady()}, work {text}, stamp {_king._possession_attack_happened_frame}, paused {Config.paused}");
				}
				yield return null;
			}
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			int num = ((World.world.projectiles != null) ? World.world.projectiles.list.Count : 0);
			Log.Info($"FEAT bow shot: attacks {attacks} -> {_mod.Feedback.Attacks}, projectiles {projectiles} -> {num}, release flag left {PossessionHooks.ReleaseAttack}");
			_mod.SaveFrame("feat_bow_loosed.png");
			yield return new WaitForSecondsRealtime(0.35f);
			_mod.SaveFrame("feat_bow_reach.png");
			for (int j = 0; j < 4; j++)
			{
				try
				{
					Vector3 pLaunchPosition = new Vector3(p.x - 2f + (float)j, p.y + 1f, 0f);
					Vector3 pTargetPosition = new Vector3(at.posV3.x, at.posV3.y, 0f);
					World.world.projectiles.spawn(_king, sheep, "arrow", pLaunchPosition, pTargetPosition, 0.3f, 0.6f);
				}
				catch (Exception ex2)
				{
					Log.Error("FEAT arrow: " + ex2.Message);
				}
			}
			_mod.DebugEye = p + new Vector2(Mathf.Cos(yaw + 1.57f) * 4f, Mathf.Sin(yaw + 1.57f) * 4f);
			_mod.DebugEyeSet = true;
			_mod.SetYaw(yaw - 1.57f + 0.5f);
			yield return new WaitForSecondsRealtime(0.15f);
			_mod.SaveFrame("feat_arrows_flying.png");
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.SaveFrame("feat_arrows_stuck.png");
			_mod.DebugEyeSet = false;
			_mod.SetYaw(yaw);
			Equip(_king, "sword_iron");
		}

		private IEnumerator Bombs()
		{
			Vector2 current_position = _king.current_position;
			float viewYaw = _mod.ViewYaw;
			_mod.SetPitchDegrees(14f);
			string[] array = new string[6] { "bomb", "atomic_bomb", "napalm_bomb", "tnt", "grenade", "water_bomb" };
			for (int i = 0; i < array.Length; i++)
			{
				WorldTile tile = World.world.GetTile((int)(current_position.x + Mathf.Cos(viewYaw) * (14f + (float)i * 2f) + Mathf.Sin(viewYaw) * ((float)i - 2.5f) * 3f), (int)(current_position.y + Mathf.Sin(viewYaw) * (14f + (float)i * 2f) - Mathf.Cos(viewYaw) * ((float)i - 2.5f) * 3f));
				try
				{
					World.world.drop_manager.spawn(tile, array[i], 8f + (float)i, -1f, -1L);
				}
				catch (Exception ex)
				{
					Log.Error("FEAT drop " + array[i] + ": " + ex.Message);
				}
			}
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("feat_bombs_falling.png");
			int num = 0;
			foreach (MeshInstance mesh in _mod.Renderer.Meshes)
			{
				if (mesh.Model != null && mesh.Model.Name != null && mesh.Model.Name.StartsWith("bomb_"))
				{
					num++;
				}
			}
			Log.Info($"FEAT bombs drawn as models: {num}");
			yield return new WaitForSecondsRealtime(3f);
			_mod.SetPitchDegrees(0f);
		}

		private IEnumerator Favourites()
		{
			List<Actor> starred = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || simple == _king || !simple.isAdult())
				{
					continue;
				}
				if (starred.Count >= 4)
				{
					break;
				}
				try
				{
					if (!simple.isFavorite())
					{
						simple.switchFavorite();
					}
					starred.Add(simple);
				}
				catch
				{
				}
			}
			_mod.Favs.Update(0f, force: true);
			Log.Info($"FEAT favourites: {_mod.Favs.List.Count}");
			_mod.Craft.Open = true;
			yield return new WaitForSecondsRealtime(0.8f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "feat_screen_favourites.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.Craft.Open = false;
			Actor before = _mod.Host;
			if (starred.Count > 0)
			{
				bool ok = Possess.JumpTo(starred[0]);
				yield return new WaitForSecondsRealtime(2.5f);
				Log.Info(string.Format("FEAT leap to a favourite: {0}, now in {1}, first person {2}", ok, (_mod.Host != null) ? WorldBoxMod.Describe(_mod.Host) : "nobody", _mod.IsFirstPerson));
				_mod.SaveFrame("feat_favourite_eyes.png");
				Possess.JumpTo(before);
				yield return new WaitForSecondsRealtime(2.5f);
				Log.Info("FEAT back in " + ((_mod.Host != null) ? WorldBoxMod.Describe(_mod.Host) : "nobody"));
			}
		}

		private IEnumerator WarMapTest()
		{
			Royal royal = _mod.Royal;
			royal.DeclareWar(_king, _rival.kingdom, delegate(string m)
			{
				Log.Info("FEAT toast: " + m);
			}, delegate
			{
			});
			royal.SendArmy(_king, _rival.kingdom, 0.5f, delegate(string m)
			{
				Log.Info("FEAT toast: " + m);
			}, delegate
			{
			});
			yield return new WaitForSecondsRealtime(3f);
			_mod.WarMap.Toggle(_king, delegate(string m)
			{
				Log.Info("FEAT toast: " + m);
			});
			yield return new WaitForSecondsRealtime(1f);
			_mod.WarMap.MarkAll();
			WarFlag flag = _mod.WarMap.Plant(WarMap.GroundFor(World.world.GetTile((int)_king.current_position.x + 10, (int)_king.current_position.y + 6)), _king.kingdom);
			yield return new WaitForSecondsRealtime(0.6f);
			Log.Info($"FEAT war map: wars {_mod.WarMap.Wars.Count}, units {_mod.WarMap.Units.Count}, towns {_mod.WarMap.Towns.Count}, ours {_mod.WarMap.OurWarriors}, theirs {_mod.WarMap.TheirWarriors}, marked {_mod.WarMap.Marked.Count}, region {_mod.WarMap.RegionSize:F0}");
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "feat_screen_warmap.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			List<Actor> warriors = _mod.WarMap.MarkedOwn();
			int cantReach;
			int num = _mod.WarMap.Assign(royal, _king, warriors, flag, out cantReach);
			Log.Info($"FEAT sent {num} marked warriors to hold flag {flag?.Number} ({cantReach} can't reach it)");
			_mod.WarMap.Close();
			yield return new WaitForSecondsRealtime(1f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "feat_screen_marks.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			royal.Recall(null);
			_mod.WarMap.ClearFlags();
			royal.MakePeace(_king, _rival.kingdom, delegate(string m)
			{
				Log.Info("FEAT toast: " + m);
			});
		}

		private IEnumerator Dragon()
		{
			Vector2 current_position = _king.current_position;
			Actor dragon = null;
			try
			{
				dragon = World.world.units.spawnNewUnit("dragon", World.world.GetTile((int)current_position.x + 14, (int)current_position.y + 10), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("FEAT dragon: " + ex.Message);
			}
			int wings = _mod.Audio.DragonWingBeats;
			int roars = _mod.Audio.DragonRoars;
			yield return new WaitForSecondsRealtime(8f);
			Log.Info(string.Format("FEAT dragon {0}: voices {1}, wingbeats heard {2}, roars {3}", (dragon != null) ? "spawned" : "missing", _mod.Audio.DragonVoices, _mod.Audio.DragonWingBeats - wings, _mod.Audio.DragonRoars - roars));
			try
			{
				if (dragon != null && dragon.isAlive())
				{
					dragon.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Music()
		{
			Vector2 current_position = _king.current_position;
			for (int i = 0; i < 5; i++)
			{
				try
				{
					World.world.units.spawnNewUnit("bandit", World.world.GetTile((int)current_position.x + 4 + i % 2, (int)current_position.y + 3 + i / 2), i == 0, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
				}
				catch (Exception ex)
				{
					Log.Error("FEAT bandits: " + ex.Message);
				}
			}
			for (int k = 0; k < 10; k++)
			{
				yield return new WaitForSecondsRealtime(1f);
				Log.Info($"FEAT music {k}: danger {_mod.Audio.Danger:F2}, mood {_mod.Audio.Mood}, battle {_mod.Audio.BattleOn}, calm {_mod.Audio.CalmOn}");
			}
		}

		private IEnumerator Sound()
		{
			FirstPersonAudio a = _mod.Audio;
			Building house = null;
			foreach (Building building in World.world.buildings)
			{
				if (building != null && HouseInterior.IsEnterable(building) && building.asset.id.StartsWith("house_", StringComparison.Ordinal))
				{
					house = building;
					break;
				}
			}
			if (house == null)
			{
				Log.Info("FEAT sound: no house to walk into");
			}
			else
			{
				for (int pass = 0; pass < 2; pass++)
				{
					if (pass == 0)
					{
						_mod.EnterHouseForTest(house);
					}
					else
					{
						_mod.LeaveRoomForTest();
					}
					List<string> trace = new List<string>();
					int between = 0;
					float target = ((pass == 0) ? 1f : 0f);
					for (float t = 0f; t < 4f && (t < 0.8f || a.MuffleLevel != target); t += Time.unscaledDeltaTime)
					{
						yield return null;
						trace.Add($"{a.MuffleLevel:F2}/{a.MuffleHz:F0}/{a.RoomLevel:F2}");
						if (a.MuffleLevel > 0.05f && a.MuffleLevel < 0.95f)
						{
							between++;
						}
					}
					Log.Info("FEAT sound " + ((pass == 0) ? "in" : "out") + " (muffle/cutoff Hz/room each frame): " + string.Join(" ", trace));
					Expect((pass == 0) ? "stepping in, the walls close in over several frames" : "stepping out, they open up over several frames", between >= 3 && ((pass != 0) ? (a.MuffleLevel <= 0f && a.RoomLevel <= 0f) : (a.MuffleLevel > 0.99f && a.RoomLevel > 0.99f)));
					yield return new WaitForSecondsRealtime(0.5f);
				}
			}
			a.CalmNowForTest();
			float waited = 0f;
			while (!a.CalmOn && waited < 45f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
				waited += 0.25f;
			}
			Log.Info(string.Format("FEAT sound: a calm piece ({0}) {1} after {2:F1} s, heard {3:F2}", a.Mood, a.CalmOn ? "on" : "not on", waited, a.Heard(3)));
			yield return new WaitForSecondsRealtime(2f);
			Vector2 current_position = _king.current_position;
			for (int i = 0; i < 5; i++)
			{
				try
				{
					World.world.units.spawnNewUnit("bandit", World.world.GetTile((int)current_position.x + 4 + i % 2, (int)current_position.y + 3 + i / 2), i == 0, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
				}
				catch (Exception ex)
				{
					Log.Error("FEAT bandits: " + ex.Message);
				}
			}
			float calmAtStart = a.Heard(3);
			float calmLow = calmAtStart;
			int calmBetween = 0;
			bool battleSeen = false;
			float[] entered = new float[3] { -1f, -1f, -1f };
			float next = 0f;
			for (float target = 0f; target < 16f; target += Time.unscaledDeltaTime)
			{
				yield return null;
				float num = a.Heard(3);
				if (calmAtStart > 0f && num > 0.05f * calmAtStart && num < 0.9f * calmAtStart && num < calmLow)
				{
					calmBetween++;
				}
				if (num >= 0f)
				{
					calmLow = Mathf.Min(calmLow, num);
				}
				battleSeen |= a.BattleOn;
				for (int j = 0; j < 3; j++)
				{
					if (a.LayerEnteredAt[j] >= 0f && entered[j] < 0f)
					{
						entered[j] = a.LayerEnteredAt[j];
					}
				}
				if (target >= next)
				{
					next += 0.5f;
					Log.Info($"FEAT sound {target:F1}s: danger {a.Danger:F2}, beat {a.BattleBeatNow():F2}, layers {a.LayerLevel(0):F2} {a.LayerLevel(1):F2} {a.LayerLevel(2):F2}, heard {a.Heard(0):F2} {a.Heard(1):F2} {a.Heard(2):F2}, calm {num:F2}");
				}
			}
			if (calmAtStart > 0f)
			{
				Expect($"the calm piece gives way to the fight over a moment ({calmBetween} frames on the way down, from {calmAtStart:F2})", calmBetween >= 2);
			}
			Expect("the battle music came in", battleSeen);
			string[] array = new string[3] { "drums", "strings", "brass" };
			float[] array2 = new float[3] { 2f, 1f, 4f };
			for (int k = 0; k < 3; k++)
			{
				if (entered[k] < 0f)
				{
					Log.Info("FEAT sound: the " + array[k] + " never came in");
					continue;
				}
				float num2 = entered[k] % array2[k];
				float num3 = a.LayerEnteredLate[k];
				Expect($"the {array[k]} came in on their beat (beat {entered[k]:F2} of the loop, {num2:F2} past a {array2[k]:F0}-beat mark, the frame {num3:F2} beats long)", num2 <= num3 + 0.1f || num2 > array2[k] - 0.05f);
			}
			float[] array3 = new float[3]
			{
				a.Heard(0),
				a.Heard(1),
				a.Heard(2)
			};
			float loudest = Mathf.Max(array3[0], Mathf.Max(array3[1], array3[2]));
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			bool fading = false;
			bool stopped = false;
			bool gameBack = false;
			bool gameBetween = false;
			next = 0f;
			for (float target = 0f; target < 5.5f; target += Time.unscaledDeltaTime)
			{
				yield return null;
				float num4 = Mathf.Max(a.Heard(0), Mathf.Max(a.Heard(1), a.Heard(2)));
				if (target > 0.3f && target < 1.2f && num4 > 0f && num4 < loudest * 0.95f)
				{
					fading = true;
				}
				if (target > 2.2f && num4 < 0f)
				{
					stopped = true;
				}
				float num5 = FirstPersonAudio.GameMusicVolumeNow();
				if (num5 > 0.1f && num5 < 0.9f)
				{
					gameBetween = true;
				}
				if (num5 > 0.99f)
				{
					gameBack = true;
				}
				if (target >= next)
				{
					next += 0.25f;
					Log.Info($"FEAT sound left +{target:F2}s: battle heard {a.Heard(0):F2} {a.Heard(1):F2} {a.Heard(2):F2}, calm {a.Heard(3):F2}, game music {num5:F2}");
				}
			}
			if (loudest > 0f)
			{
				Expect($"leaving, the battle music fades rather than stops (from {loudest:F2})", fading);
				Expect("...and is gone a moment later", stopped);
			}
			Expect("the game's own music slides back in", gameBetween && gameBack);
			void Expect(string what, bool ok)
			{
				Log.Info("FEAT sound " + (ok ? "PASS" : "FAIL") + ": " + what);
			}
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
				Log.Error("FEAT equip " + id + ": " + ex.Message);
				return false;
			}
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
			Log.Info("FEAT DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
