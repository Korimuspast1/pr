using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class KingTest : MonoBehaviour
	{
		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
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
			Config.autosaves = false;
			List<WorldTile> list = GoodSites();
			Log.Info($"KINGTEST {list.Count} zones good for a city");
			Actor kingA = null;
			Actor kingB = null;
			WorldTile a0 = null;
			WorldTile b0 = null;
			foreach (WorldTile item in list)
			{
				kingA = FoundKingdom(item, "human", 14, 3);
				if (kingA != null)
				{
					a0 = item;
					break;
				}
			}
			if (kingA != null)
			{
				foreach (WorldTile item2 in list)
				{
					float num = Mathf.Sqrt((item2.x - a0.x) * (item2.x - a0.x) + (item2.y - a0.y) * (item2.y - a0.y));
					if (num < 28f || num > 90f)
					{
						continue;
					}
					try
					{
						if (!item2.isSameIsland(a0))
						{
							continue;
						}
					}
					catch
					{
					}
					kingB = FoundKingdom(item2, "human", 10, 2);
					if (kingB != null)
					{
						b0 = item2;
						break;
					}
				}
			}
			if (kingA == null || kingB == null)
			{
				Log.Error("KINGTEST could not found two kingdoms");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Log.Info(string.Format("KINGTEST founded {0} at {1},{2} and {3} at {4},{5}; {6} kingdoms", (kingA != null) ? Royal.NameOf(kingA.kingdom) : "-", a0.x, a0.y, (kingB != null) ? Royal.NameOf(kingB.kingdom) : "-", b0.x, b0.y, CountCivs()));
			Actor king = kingA;
			if (king == null)
			{
				foreach (Kingdom item3 in World.world.kingdoms.list)
				{
					if (item3 != null && item3.isAlive() && item3.isCiv() && item3.hasKing() && item3.king != null && item3.king.isAlive() && item3.king.canBePossessed())
					{
						king = item3.king;
						break;
					}
				}
			}
			if (king == null)
			{
				Log.Error("KINGTEST no king to possess");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2f);
			WorldBoxMod mod = WorldBoxMod.Instance;
			if (mod == null || !mod.IsFirstPerson)
			{
				Log.Error("KINGTEST first person did not start");
				Finish(quit);
				yield break;
			}
			mod.HintUntil = 0f;
			try
			{
				Log.Info(string.Format("KINGTEST window open: {0} ({1})", ScrollWindow.isWindowActive(), (ScrollWindow._current_window != null) ? ScrollWindow._current_window.name : "none"));
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch (Exception ex)
			{
				Log.Error("KINGTEST closing windows: " + ex.Message);
			}
			Log.Info("KINGTEST king " + WorldBoxMod.Describe(king) + " of " + Royal.NameOf(king.kingdom) + $", {Royal.Warriors(king.kingdom)} warriors, {Royal.CityCount(king.kingdom)} cities");
			for (int k = 0; k < 4; k++)
			{
				mod.SetYaw((float)k * MathF.PI * 0.5f);
				mod.SetPitchDegrees(2f);
				yield return new WaitForSecondsRealtime(0.8f);
				yield return Capture("kingtest_view" + k + ".png");
			}
			Actor child = null;
			Actor adult = null;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != king && People.IsHumanoid(simple))
				{
					if (child == null && !simple.isAdult())
					{
						child = simple;
					}
					if (adult == null && simple.isAdult() && child != null && (simple.current_position - child.current_position).sqrMagnitude < 100f)
					{
						adult = simple;
					}
					if (child != null && adult != null)
					{
						break;
					}
				}
			}
			if (child != null)
			{
				child.makeWait(12f);
				adult?.makeWait(12f);
				for (int k = 0; k < 8; k++)
				{
					float num2 = (float)k * MathF.PI * 0.25f + 4.712389f;
					mod.DebugEye = child.current_position + new Vector2(Mathf.Cos(num2), Mathf.Sin(num2)) * 1.8f;
					mod.DebugEyeSet = true;
					mod.SetYaw(num2 + MathF.PI);
					mod.SetPitchDegrees(-12f);
					yield return null;
					yield return null;
					yield return null;
					if (mod.Picked == child)
					{
						break;
					}
				}
				mod.Talk.Talk(king, child);
				yield return new WaitForSecondsRealtime(1.5f);
				yield return Capture("kingtest_child.png");
				Log.Info("KINGTEST child " + WorldBoxMod.Describe(child) + ((adult != null) ? (", adult nearby " + WorldBoxMod.Describe(adult)) : ""));
				DumpSprite(mod, child, "kingtest_sprite_child.png");
				if (adult != null)
				{
					DumpSprite(mod, adult, "kingtest_sprite_adult.png");
				}
				mod.DebugEyeSet = false;
			}
			else
			{
				Log.Info("KINGTEST no child found");
			}
			yield return Measure(mod, "village ground");
			mod.Royal.Toggle(king, mod.Toast);
			yield return new WaitForSecondsRealtime(1f);
			yield return Capture("kingtest_council.png");
			List<Kingdom> list2 = Royal.Rivals(king.kingdom);
			Log.Info("KINGTEST rivals: " + string.Join(", ", list2.ConvertAll((Kingdom r) => $"{Royal.NameOf(r)} ({Royal.CityCount(r)} cities, war {Royal.AtWar(king.kingdom, r)}, ally {Royal.Allied(king.kingdom, r)})").ToArray()));
			Kingdom target = list2.Find((Kingdom r) => !Royal.Allied(king.kingdom, r));
			if (target != null)
			{
				if (!Royal.AtWar(king.kingdom, target))
				{
					mod.Royal.DeclareWar(king, target, mod.Toast, mod.PlayHorn);
				}
				yield return new WaitForSecondsRealtime(0.5f);
				Log.Info($"KINGTEST at war with {Royal.NameOf(target)}: {Royal.AtWar(king.kingdom, target)}; toast '{mod.ToastText}'");
				mod.Royal.SendArmy(king, target, 0.5f, mod.Toast, mod.PlayHorn);
				yield return new WaitForSecondsRealtime(0.8f);
				yield return Capture("kingtest_council_war.png");
				Log.Info("KINGTEST sent: toast '" + mod.ToastText + "', campaign " + ((mod.Royal.Active != null) ? (mod.Royal.Active.Troops.Count + " troops -> " + mod.Royal.Active.Objective?.name) : "none"));
				mod.Royal.Open = false;
				if (mod.Royal.Active != null)
				{
					Royal.Campaign c = mod.Royal.Active;
					Dictionary<Actor, float> start = new Dictionary<Actor, float>();
					Vector2 goal = ((c.Objective != null && c.Objective.getTile() != null) ? ((Vector2)c.Objective.getTile().posV3) : Vector2.zero);
					foreach (Actor troop in c.Troops)
					{
						start[troop] = (troop.current_position - goal).magnitude;
					}
					for (int k = 0; k < 4; k++)
					{
						yield return new WaitForSecondsRealtime(4f);
						if (mod.Royal.Active == null)
						{
							Log.Info("KINGTEST campaign ended: " + mod.ToastText);
							break;
						}
						int num3 = 0;
						int num4 = 0;
						float num5 = 0f;
						foreach (Actor troop2 in c.Troops)
						{
							if (troop2 == null || !troop2.isAlive())
							{
								continue;
							}
							float magnitude = (troop2.current_position - goal).magnitude;
							num5 += magnitude;
							if (start.TryGetValue(troop2, out var value) && magnitude < value - 0.5f)
							{
								num3++;
							}
							try
							{
								if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)troop2.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)troop2.ai).task.id == "fp_royal_march")
								{
									num4++;
								}
							}
							catch
							{
							}
						}
						Log.Info($"KINGTEST march t+{(k + 1) * 4}s: {c.Troops.Count} troops, {num4} on the march task, {num3} closer than at the start, mean distance {num5 / (float)Mathf.Max(1, c.Troops.Count):F1}; status '{c.Status}'");
						if (c.Troops.Count > 0)
						{
							Actor actor = c.Troops[0];
							string text2 = "?";
							try
							{
								text2 = ((((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor.ai).action != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor.ai).action.GetType().Name : "none");
							}
							catch
							{
							}
							Log.Info(string.Format("KINGTEST troop0 pos {0} moving {1} path {2} target {3} action {4} wait {5:F1}; paused {6}, world {7}, speed {8}; king at {9}", actor.current_position, actor.is_moving, (actor.current_path != null) ? actor.current_path.Count : (-1), (actor.beh_tile_target != null) ? (actor.beh_tile_target.x + "," + actor.beh_tile_target.y) : "null", text2, actor.timer_action, Config.paused, World.world.isPaused(), (Config.time_scale_asset != null) ? Config.time_scale_asset.id : "?", king.current_position));
						}
					}
					Actor actor2 = ((c.Troops.Count > 0) ? c.Troops[0] : null);
					if (actor2 != null)
					{
						mod.DebugEye = actor2.current_position - new Vector2(Mathf.Cos(0.3f), Mathf.Sin(0.3f)) * 4f;
						mod.DebugEyeSet = true;
						Vector2 vector = goal - mod.DebugEye;
						mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
						mod.SetPitchDegrees(0f);
						yield return new WaitForSecondsRealtime(1f);
						yield return Capture("kingtest_army.png");
						mod.DebugEyeSet = false;
					}
					mod.Royal.Recall(mod.Toast);
				}
			}
			TopTileType topTileType = null;
			foreach (TopTileType item4 in AssetManager.top_tiles.list)
			{
				if (item4 != null && item4.wall)
				{
					topTileType = item4;
					break;
				}
			}
			if (topTileType != null && king.city != null)
			{
				WorldTile c2 = king.city.getTile() ?? king.current_tile;
				int num6 = 0;
				for (int num7 = -7; num7 <= 7; num7++)
				{
					for (int num8 = -7; num8 <= 7; num8++)
					{
						if (Mathf.Max(Mathf.Abs(num8), Mathf.Abs(num7)) != 7 || (num7 == -7 && Mathf.Abs(num8) <= 1))
						{
							continue;
						}
						WorldTile tile = World.world.GetTile(c2.x + num8, c2.y + num7);
						if (tile != null && tile.main_type != null && !tile.main_type.liquid && tile.building == null)
						{
							try
							{
								tile.setTopTileType(topTileType);
								num6++;
							}
							catch
							{
							}
						}
					}
				}
				Log.Info($"KINGTEST walls: {num6} tiles of {topTileType.id} around {c2.x},{c2.y}");
				yield return new WaitForSecondsRealtime(1.5f);
				mod.DebugEye = new Vector2((float)c2.x + 0.5f, (float)c2.y + 0.5f - 12f);
				mod.DebugEyeSet = true;
				mod.SetYaw(MathF.PI / 2f);
				mod.SetPitchDegrees(6f);
				yield return new WaitForSecondsRealtime(1.2f);
				yield return Capture("kingtest_walls_outside.png");
				mod.DebugEye = new Vector2((float)c2.x + 0.5f, (float)c2.y + 0.5f - 3f);
				mod.SetYaw(MathF.PI * 3f / 4f);
				mod.SetPitchDegrees(8f);
				yield return new WaitForSecondsRealtime(1.2f);
				yield return Capture("kingtest_walls_inside.png");
				mod.DebugEyeSet = false;
			}
			else
			{
				Log.Info("KINGTEST no wall tile type or no city");
			}
			WorldTile spot = FindOpenLand(king.current_position, 25);
			if (spot != null)
			{
				Building building = null;
				try
				{
					building = World.world.buildings.addBuilding("volcano", spot, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				}
				catch (Exception ex2)
				{
					Log.Error("KINGTEST volcano failed: " + ex2.Message);
				}
				Log.Info("KINGTEST volcano " + ((building != null) ? ("at " + spot.x + "," + spot.y) : "not placed"));
				yield return new WaitForSecondsRealtime(9f);
				mod.DebugEye = new Vector2((float)spot.x + 0.5f, (float)spot.y + 0.5f - 34f);
				mod.DebugEyeSet = true;
				mod.DebugEyeHeight = 9f;
				mod.SetYaw(MathF.PI / 2f);
				mod.SetPitchDegrees(4f);
				yield return new WaitForSecondsRealtime(2.5f);
				yield return Capture("kingtest_volcano.png");
				yield return Measure(mod, "volcano aerial");
				mod.DebugEye = new Vector2((float)spot.x + 0.5f - 7f, (float)spot.y + 0.5f - 14f);
				mod.DebugEyeHeight = 3f;
				mod.SetYaw(Mathf.Atan2(14f, 7f));
				mod.SetPitchDegrees(18f);
				yield return new WaitForSecondsRealtime(1.5f);
				yield return Capture("kingtest_volcano_near.png");
				mod.DebugEyeHeight = 0f;
				Log.Info($"KINGTEST volcano fx: {mod.CraterCount} craters, {mod.SceneForTest.PuffCount} smoke puffs");
				mod.DebugEyeSet = false;
			}
			Vector2? peak = HighestPoint();
			if (peak.HasValue)
			{
				mod.DebugEye = peak.Value + new Vector2(0f, -45f);
				mod.DebugEyeSet = true;
				mod.DebugEyeHeight = 6f;
				mod.SetYaw(MathF.PI / 2f);
				mod.SetPitchDegrees(6f);
				yield return new WaitForSecondsRealtime(1.5f);
				yield return Capture("kingtest_mountains.png");
				yield return Measure(mod, "mountains aerial");
				mod.DebugEyeHeight = 0f;
				mod.DebugEye = peak.Value + new Vector2(0f, -45f);
				yield return new WaitForSecondsRealtime(1.5f);
				yield return Capture("kingtest_mountains_ground.png");
				yield return Measure(mod, "mountains ground");
				Log.Info($"KINGTEST mountains: terrain {mod.Renderer.TerrainMs:F1} ms, objects {mod.Renderer.ObjectsMs:F1} ms (blobs {mod.Renderer.BlobMs:F1}, voxel shadows {mod.Renderer.VoxelShadowMs:F1}, voxels {mod.Renderer.VoxelMs:F1}, sprites {mod.Renderer.SpriteMs:F1}; prep {mod.Renderer.PrepMs:F1}, parallel {mod.Renderer.ParallelMs:F1}, stripe sum {mod.Renderer.StripeSumMs:F1} over {mod.Renderer.Stripes} stripes), frame {mod.RenderMs:F1} ms, {mod.VoxelCount} voxels, {mod.BillboardCount} billboards");
				mod.DebugEyeSet = false;
			}
			Log.Info($"KINGTEST render {mod.RenderMs:F1} ms");
			Finish(quit);
		}

		internal static Actor FoundKingdom(WorldTile center, string species, int adults, int children)
		{
			List<Actor> list = new List<Actor>();
			for (int i = 0; i < adults + children; i++)
			{
				WorldTile worldTile = World.world.GetTile(center.x + i % 5 * 2 - 4, center.y + i / 5 * 2 - 3) ?? center;
				if (worldTile.main_type == null || worldTile.main_type.liquid)
				{
					worldTile = center;
				}
				try
				{
					Actor actor = World.world.units.spawnNewUnit(species, worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, i < adults);
					if (actor != null)
					{
						list.Add(actor);
					}
				}
				catch (Exception ex)
				{
					Log.Error("KINGTEST spawn failed: " + ex.Message);
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			Actor actor2 = list[0];
			try
			{
				if (!actor2.buildCityAndStartCivilization())
				{
					foreach (Actor item in list)
					{
						if (item != null && item.isAlive())
						{
							item.dieSimpleNone();
						}
					}
					return null;
				}
				City city = actor2.city;
				foreach (Actor item2 in list)
				{
					if (item2 != actor2 && item2.city != city)
					{
						item2.joinCity(city);
					}
				}
				Kingdom kingdom = actor2.kingdom;
				if (kingdom != null && !kingdom.hasKing())
				{
					kingdom.setKing(actor2);
				}
				for (int j = 1; j < list.Count && j < adults; j++)
				{
					if (j % 4 != 0 && list[j].isAdult())
					{
						city.makeWarrior(list[j]);
					}
				}
				Log.Info($"KINGTEST city {city.name} of {Royal.NameOf(kingdom)}: {list.Count} people, king {WorldBoxMod.Describe(actor2)}, {Royal.Warriors(kingdom)} warriors");
				return actor2;
			}
			catch (Exception ex2)
			{
				Log.Error("KINGTEST founding failed: " + ex2);
				return null;
			}
		}

		internal static List<WorldTile> GoodSites()
		{
			List<WorldTile> list = new List<WorldTile>();
			try
			{
				foreach (TileZone zone in World.world.zone_calculator.zones)
				{
					if (zone != null && zone.centerTile != null && zone.isGoodForNewCity() && Open(zone.centerTile))
					{
						list.Add(zone.centerTile);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("KINGTEST zone scan failed: " + ex.Message);
			}
			System.Random random = new System.Random(7);
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = random.Next(num + 1);
				WorldTile value = list[num];
				list[num] = list[index];
				list[index] = value;
			}
			List<WorldTile> droppers = new List<WorldTile>();
			try
			{
				foreach (Building building in World.world.buildings)
				{
					if (building != null && building.asset != null && building.asset.spawn_drops && building.current_tile != null)
					{
						droppers.Add(building.current_tile);
					}
				}
			}
			catch
			{
			}
			List<WorldTile> safe = list.FindAll((WorldTile t) => !Hazard(t, droppers));
			if (safe.Count == 0)
			{
				return list;
			}
			Dictionary<WorldTile, int> woods = new Dictionary<WorldTile, int>();
			foreach (WorldTile item in safe)
			{
				woods[item] = Woods(item) / 12;
			}
			List<WorldTile> list2 = new List<WorldTile>(safe);
			list2.Sort((WorldTile a, WorldTile b) => (woods[a] == woods[b]) ? safe.IndexOf(a).CompareTo(safe.IndexOf(b)) : woods[a].CompareTo(woods[b]));
			return list2;
		}

		private static int Woods(WorldTile t)
		{
			int num = 0;
			try
			{
				for (int i = t.y - 8; i <= t.y + 8; i++)
				{
					for (int j = t.x - 8; j <= t.x + 8; j++)
					{
						WorldTile tile = World.world.GetTile(j, i);
						if (tile != null && tile.building != null && AssetStyle.IsSolid(tile.building))
						{
							num++;
						}
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static bool Hazard(WorldTile t, List<WorldTile> droppers)
		{
			try
			{
				foreach (WorldTile dropper in droppers)
				{
					if ((dropper.x - t.x) * (dropper.x - t.x) + (dropper.y - t.y) * (dropper.y - t.y) < 900)
					{
						return true;
					}
				}
				for (int i = t.y - 12; i <= t.y + 12; i++)
				{
					for (int j = t.x - 12; j <= t.x + 12; j++)
					{
						WorldTile tile = World.world.GetTile(j, i);
						if (tile != null && ((tile.Type != null && tile.Type.lava) || tile.isOnFire()))
						{
							return true;
						}
					}
				}
			}
			catch
			{
			}
			return false;
		}

		private static void FindTwoSites(out WorldTile a, out WorldTile b)
		{
			a = (b = null);
			List<WorldTile> list = new List<WorldTile>();
			for (int i = 16; i < MapBox.height - 16; i += 8)
			{
				for (int j = 16; j < MapBox.width - 16; j += 8)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (Open(tile))
					{
						bool flag = true;
						for (int k = -3; k <= 3 && flag; k += 3)
						{
							flag = Open(World.world.GetTile(j + k, i)) && Open(World.world.GetTile(j, i + k));
						}
						if (flag)
						{
							list.Add(tile);
						}
					}
				}
			}
			Log.Info($"KINGTEST {list.Count} candidate sites");
			foreach (WorldTile item in list)
			{
				foreach (WorldTile item2 in list)
				{
					float num = Mathf.Sqrt((item.x - item2.x) * (item.x - item2.x) + (item.y - item2.y) * (item.y - item2.y));
					if (num < 30f || num > 90f)
					{
						continue;
					}
					try
					{
						if (!item.isSameIsland(item2))
						{
							continue;
						}
					}
					catch
					{
					}
					a = item;
					b = item2;
					return;
				}
			}
		}

		private static bool Open(WorldTile t)
		{
			if (t != null && t.main_type != null && !t.main_type.liquid && !t.main_type.lava && t.main_type.height_min < 199)
			{
				if (t.zone != null)
				{
					return t.zone.city == null;
				}
				return true;
			}
			return false;
		}

		private static void DumpSprite(WorldBoxMod mod, Actor a, string name)
		{
			SpriteImage spriteImage = mod.SpriteForTest(a);
			if (spriteImage == null)
			{
				Log.Info("KINGTEST no sprite for " + name);
				return;
			}
			Texture2D texture2D = new Texture2D(spriteImage.Width * 8, spriteImage.Height * 8, TextureFormat.RGBA32, mipChain: false);
			Color32[] array = new Color32[spriteImage.Width * 8 * spriteImage.Height * 8];
			for (int i = 0; i < spriteImage.Height * 8; i++)
			{
				for (int j = 0; j < spriteImage.Width * 8; j++)
				{
					uint num = spriteImage.Pixels[i / 8 * spriteImage.Width + j / 8];
					array[i * spriteImage.Width * 8 + j] = new Color32((byte)(num & 0xFF), (byte)((num >> 8) & 0xFF), (byte)((num >> 16) & 0xFF), (byte)(num >> 24));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
			Humanoid.Palette palette = Humanoid.Extract(spriteImage);
			Log.Info($"KINGTEST sprite {name}: {spriteImage.Width}x{spriteImage.Height} rows {spriteImage.OpaqueBottom}-{spriteImage.OpaqueTop}, unit height {spriteImage.UnitHeight:F3}, scale {a.current_scale.y:F2}; skin {palette.Skin:X8} hair {palette.Hair:X8} shirt {palette.Shirt:X8} pants {palette.Pants:X8}");
		}

		private static IEnumerator Measure(WorldBoxMod mod, string label)
		{
			List<float> terrain = new List<float>();
			List<float> objects = new List<float>();
			List<float> frames = new List<float>();
			List<float> render = new List<float>();
			for (int k = 0; k < 30; k++)
			{
				yield return null;
				terrain.Add(mod.Renderer.TerrainMs);
				objects.Add(mod.Renderer.ObjectsMs);
				frames.Add(Time.unscaledDeltaTime * 1000f);
				render.Add(mod.RenderMs);
			}
			terrain.Sort();
			objects.Sort();
			frames.Sort();
			render.Sort();
			Log.Info($"KINGTEST perf {label}: terrain {terrain[15]:F1} ms, objects {objects[15]:F1} ms, game frame {frames[15]:F1} ms ({1000f / Mathf.Max(1f, frames[15]):F0} fps), detail {mod.SceneForTest.Detail:F2}, {mod.VoxelCount} voxels, {mod.BillboardCount} billboards");
		}

		private static int CountCivs()
		{
			int num = 0;
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item.isAlive() && item.isCiv())
					{
						num++;
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static WorldTile FindOpenLand(Vector2 near, int distance)
		{
			for (int i = distance; i < distance + 30; i += 2)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j / 16f * MathF.PI * 2f;
					WorldTile tile = World.world.GetTile((int)(near.x + Mathf.Cos(f) * (float)i), (int)(near.y + Mathf.Sin(f) * (float)i));
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && tile.building == null && tile.zone != null && tile.zone.city == null)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static Vector2? HighestPoint()
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			TerrainCache terrainCache = ((instance != null) ? instance.TerrainForTest : null);
			if (terrainCache == null || terrainCache.Heights.Length == 0)
			{
				return null;
			}
			int num = 0;
			for (int i = 1; i < terrainCache.Ground.Length; i++)
			{
				if (terrainCache.Ground[i] > terrainCache.Ground[num])
				{
					num = i;
				}
			}
			Log.Info($"KINGTEST highest ground {terrainCache.Ground[num]:F1} at {num % terrainCache.Width},{num / terrainCache.Width}");
			return new Vector2((float)(num % terrainCache.Width) + 0.5f, (float)(num / terrainCache.Width) + 0.5f);
		}

		private static IEnumerator Capture(string name)
		{
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info("KINGTEST captured " + name);
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
			Log.Info("KINGTEST DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
