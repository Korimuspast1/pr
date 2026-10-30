using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LookTest : MonoBehaviour
	{
		private static readonly string[] ModeTags = new string[4] { "cycle", "day", "golden", "night" };

		private static readonly (string id, float x, float y)[] TownPlan = new(string, float, float)[33]
		{
			("hall_human_0", 0f, 9f),
			("temple_human", -12f, 9f),
			("library_human", 12f, 9f),
			("house_human_0", -15f, 1f),
			("house_human_1", -10f, 1f),
			("house_human_2", -5f, 1f),
			("well", 0f, 1f),
			("house_human_3", 5f, 1f),
			("house_human_4", 11f, 1f),
			("market_human", 19f, 2f),
			("house_elf_2", -14f, -6f),
			("house_orc_3", -8f, -6f),
			("tent_human", -3f, -7f),
			("house_dwarf_3", 7f, -6f),
			("house_human_5", 13f, -6f),
			("windmill_human_0", -22f, 6f),
			("stockpile", -22f, -4f),
			("watch_tower_human", 23f, -6f),
			("barracks_human", 22f, 12f),
			("training_dummy", 17f, -10f),
			("house_elf_4", -8f, 16f),
			("house_orc_5", 6f, 17f),
			("house_dwarf_5", 16f, 20f),
			("statue", 4f, -11f),
			("mine", -20f, 16f),
			("beehive", -17f, -11f),
			("geyser", -22f, -20f),
			("monolith", -14f, -20f),
			("flame_tower", -6f, -21f),
			("golden_brain", 3f, -21f),
			("super_pumpkin", 10f, -19f),
			("waypoint_computer", 17f, -21f),
			("biomass", 23f, -19f)
		};

		private static Vector2? DocksAt;

		private static float DocksYaw;

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			bool onlyTown = text.Contains("townonly");
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
				if (!flag && t >= 4f && World.world.units.Count > 0)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			Log.Info($"LOOK world {MapBox.width}x{MapBox.height}, units {World.world.units.Count}, buildings {World.world.buildings.Count}");
			Actor unit = null;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.canBePossessed() && People.IsHumanoid(simple) && simple.isKingdomCiv() && !SceneCollector.IsSwimming(simple))
				{
					unit = simple;
					break;
				}
			}
			if (unit == null)
			{
				foreach (WorldTile item in KingTest.GoodSites())
				{
					unit = KingTest.FoundKingdom(item, "human", 10, 3);
					if (unit != null)
					{
						break;
					}
				}
				Log.Info("LOOK founded a village: " + ((unit != null) ? WorldBoxMod.Describe(unit) : "failed"));
				yield return new WaitForSecondsRealtime(3f);
			}
			if (unit == null)
			{
				unit = SelfTest.PickUnit() ?? SelfTest.SpawnSome();
			}
			if (unit == null)
			{
				Log.Error("LOOK no creature");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(unit);
			yield return new WaitForSecondsRealtime(2f);
			WorldBoxMod mod = WorldBoxMod.Instance;
			if (mod == null || !mod.IsFirstPerson)
			{
				Log.Error("LOOK first person did not start");
				Finish(quit);
				yield break;
			}
			mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Settings s = mod.Settings;
			int savedMode = s.TimeMode;
			Log.Info($"LOOK host {WorldBoxMod.Describe(unit)}, fov {s.FieldOfView}, eye {s.EyeHeight}, walk {s.RunSpeed}, palette {s.Palette}, draw distance {s.DrawDistance}, detail distance {s.DetailDistance}");
			SceneCollector.AllLit = true;
			Vector2? square = BuildTown(mod, unit);
			int[] array;
			if (square.HasValue)
			{
				array = new int[3] { 1, 2, 3 };
				for (int i = 0; i < array.Length; i++)
				{
					int m = (s.TimeMode = array[i]);
					mod.ForceRelight();
					yield return new WaitForSecondsRealtime(1f);
					Vector2 c = square.Value;
					yield return Spot(mod, c + new Vector2(0f, -14f), MathF.PI / 2f, 0f, "look_" + ModeTags[m] + "_town0.png");
					yield return Spot(mod, c + new Vector2(-12f, -9f), MathF.PI / 5f, 0f, "look_" + ModeTags[m] + "_town1.png");
					yield return Spot(mod, c + new Vector2(26f, -14f), 2.6389377f, 0f, "look_" + ModeTags[m] + "_town2.png");
					yield return Spot(mod, c + new Vector2(-3f, -4.5f), 1.3194689f, 0f, "look_" + ModeTags[m] + "_town3.png");
					yield return Spot(mod, c + new Vector2(0f, -30f), MathF.PI / 2f, 6f, "look_" + ModeTags[m] + "_town4.png");
					yield return Spot(mod, c + new Vector2(0f, -32f), MathF.PI / 2f, 0f, "look_" + ModeTags[m] + "_wonders0.png");
					yield return Spot(mod, c + new Vector2(-16f, -29f), 0.9424779f, 0f, "look_" + ModeTags[m] + "_wonders1.png");
					if (DocksAt.HasValue)
					{
						yield return Spot(mod, DocksAt.Value - new Vector2(Mathf.Cos(DocksYaw), Mathf.Sin(DocksYaw)) * 9f + new Vector2(0f - Mathf.Sin(DocksYaw), Mathf.Cos(DocksYaw)) * 4f, DocksYaw - 0.35f, 0f, "look_" + ModeTags[m] + "_docks.png");
					}
					if (m == 1)
					{
						mod.DebugEye = c + new Vector2(0f, -14f);
						mod.DebugEyeSet = true;
						mod.SetYaw(MathF.PI / 2f);
						yield return Measure(mod, "day town");
						yield return Measure(mod, "day town turning", 0.04f);
						mod.DebugEyeSet = false;
					}
				}
			}
			SceneCollector.AllLit = false;
			if (onlyTown)
			{
				s.TimeMode = savedMode;
				mod.ForceRelight();
				Finish(quit);
				yield break;
			}
			yield return Pace(mod, unit, run: false);
			yield return Pace(mod, unit, run: true);
			array = new int[3] { 1, 2, 3 };
			for (int i = 0; i < array.Length; i++)
			{
				int m = (s.TimeMode = array[i]);
				mod.ForceRelight();
				yield return new WaitForSecondsRealtime(1.2f);
				for (int k = 0; k < 3; k++)
				{
					mod.SetYaw((float)k * MathF.PI * 2f / 3f + 0.4f);
					mod.SetPitchDegrees(3f);
					yield return new WaitForSecondsRealtime(0.6f);
					mod.SaveFrame($"look_{ModeTags[m]}_around{k}.png");
				}
				yield return Measure(mod, ModeTags[m] + " around");
			}
			Vector2? village = SelfTest.DensestCluster(flora: false);
			float edgeYaw;
			Vector2? edge = ScenicSpot(mod.TerrainForTest, high: false, out edgeYaw);
			float riseYaw;
			Vector2? rise = ScenicSpot(mod.TerrainForTest, high: true, out riseYaw);
			Log.Info($"LOOK spots: village {village}, forest edge {edge}, rise {rise}");
			array = new int[3] { 2, 3, 1 };
			for (int i = 0; i < array.Length; i++)
			{
				int m = (s.TimeMode = array[i]);
				mod.ForceRelight();
				yield return new WaitForSecondsRealtime(0.8f);
				if (village.HasValue)
				{
					yield return Spot(mod, village.Value + new Vector2(-7f, -7f), MathF.PI / 4f, 0f, "look_" + ModeTags[m] + "_village.png");
				}
				if (edge.HasValue)
				{
					yield return Spot(mod, edge.Value, edgeYaw, 0f, "look_" + ModeTags[m] + "_forest.png");
				}
				if (rise.HasValue)
				{
					yield return Spot(mod, rise.Value, riseYaw, 0f, "look_" + ModeTags[m] + "_vista.png");
				}
				if (m == 3 && village.HasValue)
				{
					yield return Measure(mod, "night village");
				}
			}
			TerrainCache terrainForTest = mod.TerrainForTest;
			if (terrainForTest != null && terrainForTest.Width > 0)
			{
				int num = 0;
				for (int j = 0; j < terrainForTest.Ground.Length; j++)
				{
					if (terrainForTest.Ground[j] > terrainForTest.Ground[num])
					{
						num = j;
					}
				}
				float num2 = (float)(num % terrainForTest.Width) + 0.5f;
				float num3 = (float)(num / terrainForTest.Width) + 0.5f;
				Log.Info($"LOOK highest ground {terrainForTest.Ground[num]:F1} at {num2},{num3}");
				square = null;
				for (int l = 30; l < 90; l += 6)
				{
					if (square.HasValue)
					{
						break;
					}
					for (int n = 0; n < 16; n++)
					{
						if (square.HasValue)
						{
							break;
						}
						float f = (float)n * MathF.PI / 8f;
						float num4 = num2 + Mathf.Cos(f) * (float)l;
						float num5 = num3 + Mathf.Sin(f) * (float)l;
						if (!(num4 < 2f) && !(num5 < 2f) && !(num4 >= (float)(terrainForTest.Width - 2)) && !(num5 >= (float)(terrainForTest.Height - 2)))
						{
							int num6 = (int)num5 * terrainForTest.Width + (int)num4;
							if ((terrainForTest.Flags[num6] & 1) == 0 && terrainForTest.Ground[num6] < 6f)
							{
								square = new Vector2(num4, num5);
							}
						}
					}
				}
				if (square.HasValue)
				{
					float t = Mathf.Atan2(num3 - square.Value.y, num2 - square.Value.x);
					array = new int[3] { 1, 2, 3 };
					for (int i = 0; i < array.Length; i++)
					{
						int m = (s.TimeMode = array[i]);
						mod.ForceRelight();
						yield return new WaitForSecondsRealtime(0.8f);
						yield return Spot(mod, square.Value, t, 8f, "look_" + ModeTags[m] + "_mountain.png");
					}
				}
			}
			array = new int[2] { 1, 3 };
			for (int i = 0; i < array.Length; i++)
			{
				int m = (s.TimeMode = array[i]);
				mod.EnterRoomForTest(new List<Actor>());
				yield return new WaitForSecondsRealtime(1f);
				mod.SetPitchDegrees(-4f);
				yield return new WaitForSecondsRealtime(0.5f);
				mod.SaveFrame("look_" + ModeTags[m] + "_house.png");
				mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(0.5f);
			}
			s.TimeMode = savedMode;
			mod.ForceRelight();
			Finish(quit);
		}

		private static IEnumerator Pace(WorldBoxMod mod, Actor unit, bool run)
		{
			PossessionHooks.ScriptedRun = run;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			Vector2 start = unit.current_position;
			float t0 = Time.realtimeSinceStartup;
			float measuredFrom = -1f;
			while (Time.realtimeSinceStartup - t0 < 2.5f)
			{
				ControllableUnit.updateMovementVectorKeyboard();
				Vector2 movement_vector = ControllableUnit._movement_vector;
				if (movement_vector != Vector2.zero && World.world.elapsed > 0f)
				{
					unit.updatePossessedMovementTowards(World.world.elapsed, unit.current_position + movement_vector);
				}
				if (measuredFrom < 0f && Time.realtimeSinceStartup - t0 > 0.5f)
				{
					measuredFrom = Time.realtimeSinceStartup;
					start = unit.current_position;
				}
				yield return null;
			}
			float num = Time.realtimeSinceStartup - measuredFrom;
			float magnitude = (unit.current_position - start).magnitude;
			Log.Info($"LOOK pace test: elapsed {World.world.elapsed:F4}, speed {unit._current_combined_movement_speed:F2} tiles/s, paused {Config.paused}");
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			float num2 = 1f;
			SpriteImage spriteImage = mod.SpriteForTest(unit);
			if (spriteImage != null)
			{
				num2 = spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(unit.current_scale.y);
			}
			Log.Info(string.Format("LOOK {0}: {1:F2} tiles/s = {2:F2} body heights/s (body {3:F2} tiles), pace {4:F2}", run ? "running" : "walking", magnitude / num, magnitude / num / Mathf.Max(0.1f, num2), num2, PossessionHooks.Pace));
			yield return new WaitForSecondsRealtime(0.6f);
		}

		private static Vector2? ScenicSpot(TerrainCache cache, bool high, out float yaw)
		{
			yaw = 0f;
			if (cache == null || cache.Width == 0)
			{
				return null;
			}
			List<Vector2> list = new List<Vector2>();
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && simple.current_tile != null && AssetStyle.Classify(simple.asset) == BuildingLook.Tree)
				{
					list.Add(new Vector2((float)simple.current_tile.x + 0.5f, (float)simple.current_tile.y + 0.5f));
				}
			}
			Vector2? result = null;
			float num = float.MinValue;
			for (int i = 12; i < cache.Height - 12; i += 5)
			{
				for (int j = 12; j < cache.Width - 12; j += 5)
				{
					int num2 = i * cache.Width + j;
					if ((cache.Flags[num2] & 3) != 0 || cache.Ground[num2] > 14f)
					{
						continue;
					}
					Vector2 vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
					int num3 = 0;
					int num4 = 0;
					Vector2 zero = Vector2.zero;
					foreach (Vector2 item in list)
					{
						float sqrMagnitude = (item - vector).sqrMagnitude;
						if (sqrMagnitude < 36f)
						{
							num3++;
						}
						else if (sqrMagnitude < 900f)
						{
							num4++;
							zero += item - vector;
						}
					}
					if (num3 <= 1)
					{
						float num5 = (high ? (cache.Ground[num2] * 10f + (float)Mathf.Min(num4, 30)) : ((float)Mathf.Min(num4, 120)));
						if (num5 > num)
						{
							num = num5;
							result = vector;
							Vector2 vector2 = ((num4 > 0) ? (zero / num4) : new Vector2(1f, 0f));
							yaw = Mathf.Atan2(vector2.y, vector2.x) + (high ? MathF.PI : 0f);
						}
					}
				}
			}
			return result;
		}

		private static Vector2? BuildTown(WorldBoxMod mod, Actor host)
		{
			TerrainCache terrainForTest = mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				return null;
			}
			Vector2 current_position = host.current_position;
			Vector2? vector = null;
			float num = float.MaxValue;
			for (int i = 30; i < terrainForTest.Height - 28; i += 3)
			{
				for (int j = 30; j < terrainForTest.Width - 30; j += 3)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					bool flag = true;
					for (int k = -24; k <= 24 && flag; k += 2)
					{
						for (int l = -26; l <= 26 && flag; l += 2)
						{
							int num4 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num4] & 1) != 0)
							{
								flag = false;
							}
							num2 = Math.Min(num2, terrainForTest.Ground[num4]);
							num3 = Math.Max(num3, terrainForTest.Ground[num4]);
						}
					}
					if (flag && !(num3 - num2 > 12f))
					{
						float num5 = (num3 - num2) * 8f + (new Vector2(j, i) - current_position).magnitude;
						if (num5 < num)
						{
							num = num5;
							vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						}
					}
				}
			}
			if (!vector.HasValue)
			{
				Log.Error("LOOK town: no flat open land");
				return null;
			}
			Vector2 value = vector.Value;
			int num6 = 0;
			int num7 = 0;
			foreach (Building item4 in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item4 == null || !item4.isAlive() || item4.current_tile == null)
				{
					continue;
				}
				Vector2 vector2 = new Vector2(item4.current_tile.x, item4.current_tile.y) - value;
				if (vector2.x < -30f || vector2.x > 30f || vector2.y < -16f || vector2.y > 26f)
				{
					continue;
				}
				try
				{
					if (!item4.isKingdomCiv())
					{
						item4.removeBuildingFinal();
						num6++;
					}
				}
				catch
				{
				}
			}
			Kingdom kingdom = host.kingdom;
			(string, float, float)[] townPlan = TownPlan;
			for (int m = 0; m < townPlan.Length; m++)
			{
				(string, float, float) tuple = townPlan[m];
				string item = tuple.Item1;
				float item2 = tuple.Item2;
				float item3 = tuple.Item3;
				WorldTile tile = World.world.GetTile((int)(value.x + item2), (int)(value.y + item3));
				if (tile == null)
				{
					continue;
				}
				try
				{
					Building building = World.world.buildings.addBuilding(item, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building == null)
					{
						Log.Error("LOOK town: could not place " + item);
						continue;
					}
					building.completeConstruction();
					if (kingdom != null)
					{
						building.setKingdom(kingdom);
					}
					num7++;
				}
				catch (Exception ex)
				{
					Log.Error("LOOK town: " + item + " failed: " + ex.Message);
				}
			}
			Log.Info($"LOOK town at {value}: {num7} buildings placed, {num6} cleared, ground {terrainForTest.SampleHeight(value.x, value.y):F1}");
			PlaceDocks(terrainForTest, value, kingdom);
			return value;
		}

		private static void Nearby(WorldBoxMod mod, Vector2 eye)
		{
			mod.SceneForTest.DumpNear(eye, 16f);
			foreach (Billboard billboard in mod.SceneForTest.Billboards)
			{
				float magnitude = (new Vector2(billboard.X, billboard.Y) - eye).magnitude;
				if (magnitude < 6f)
				{
					Log.Info($"LOOK near sprite at {magnitude:F1}: {billboard.Image?.Width}x{billboard.Image?.Height} size {billboard.Width:F2}x{billboard.Height:F2} z {billboard.Z:F2} kind {billboard.Kind} emissive {billboard.Emissive} tag {billboard.Tag}");
				}
			}
			try
			{
				foreach (BaseEffectController item in World.world.stack_effects.list)
				{
					if (item == null || item._list == null)
					{
						continue;
					}
					foreach (BaseEffect item2 in item._list)
					{
						if (!(item2 == null) && item2.active)
						{
							Vector3 position = item2.transform.position;
							float magnitude2 = (new Vector2(position.x, position.y) - eye).magnitude;
							if (magnitude2 < 6f)
							{
								Log.Info($"LOOK near effect at {magnitude2:F1}: {item.asset?.id} sprite {item2.sprite_renderer?.sprite?.name} alpha {item2.sprite_renderer?.color.a:F2} scale {item2.transform.lossyScale.x:F2}");
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("LOOK effects: " + ex.Message);
			}
			foreach (PointLight light in mod.Renderer.Lights)
			{
				float magnitude3 = (new Vector2(light.X, light.Y) - eye).magnitude;
				if (magnitude3 < 12f)
				{
					Log.Info($"LOOK near light at {magnitude3:F1}: radius {light.Radius:F1} rgb {light.R:F2},{light.G:F2},{light.B:F2} glow {light.Glow:F2} z {light.Z:F2}");
				}
			}
			foreach (VoxelObject voxel in mod.SceneForTest.Voxels)
			{
				float magnitude4 = (new Vector2(voxel.Origin.X, voxel.Origin.Y) - eye).magnitude;
				if (magnitude4 < 25f)
				{
					List<BaseSimObject> tagged = mod.SceneForTest.Tagged;
					BaseSimObject baseSimObject = ((voxel.Tag >= 0 && voxel.Tag < tagged.Count) ? tagged[voxel.Tag] : null);
					string arg = ((baseSimObject is Building building) ? ("building " + building.asset?.id + (building.isUnderConstruction() ? " (building)" : "") + (building.isRuin() ? " (ruin)" : "")) : ((baseSimObject is Actor actor) ? ("creature " + actor.asset?.id) : ("tag " + voxel.Tag)));
					Log.Info($"LOOK near voxel at {magnitude4:F1}: {arg}");
				}
			}
			foreach (MeshInstance mesh in mod.SceneForTest.Meshes)
			{
				float magnitude5 = (new Vector2(mesh.Position.X, mesh.Position.Y) - eye).magnitude;
				if (magnitude5 < 6f)
				{
					Log.Info($"LOOK near model at {magnitude5:F1}: {mesh.Model.Name} shadow only {mesh.ShadowOnly}");
				}
			}
		}

		private static void PlaceDocks(TerrainCache cache, Vector2 c, Kingdom kingdom)
		{
			DocksAt = null;
			int w = cache.Width;
			List<(Vector2, float)> list = new List<(Vector2, float)>();
			for (int i = 10; i < 80; i += 2)
			{
				if (list.Count >= 2)
				{
					break;
				}
				for (int j = 0; j < 48; j++)
				{
					if (list.Count >= 2)
					{
						break;
					}
					float f = (float)j / 48f * MathF.PI * 2f;
					int num = (int)(c.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(c.y + Mathf.Sin(f) * (float)i);
					if (num < 8 || num2 < 8 || num >= w - 8 || num2 >= cache.Height - 8 || Wet(num, num2))
					{
						continue;
					}
					float num3 = 0f;
					int num4 = 0;
					for (int k = 0; k < 8; k++)
					{
						float num5 = (float)k * MathF.PI / 4f;
						int num6 = 0;
						for (int l = 1; l <= 8; l++)
						{
							if (Wet(num + Mathf.RoundToInt(Mathf.Cos(num5) * (float)l), num2 + Mathf.RoundToInt(Mathf.Sin(num5) * (float)l)))
							{
								num6++;
							}
						}
						if (num6 > num4)
						{
							num4 = num6;
							num3 = num5;
						}
					}
					if (num4 >= 6 && Wet(num + Mathf.RoundToInt(Mathf.Cos(num3) * 2f), num2 + Mathf.RoundToInt(Mathf.Sin(num3) * 2f)))
					{
						Vector2 vector = new Vector2(num, num2);
						if (list.Count <= 0 || !((list[0].Item1 - vector).sqrMagnitude < 144f))
						{
							list.Add((vector, num3));
						}
					}
				}
			}
			string[] array = new string[2] { "docks_human", "fishing_docks_human" };
			for (int m = 0; m < list.Count; m++)
			{
				try
				{
					WorldTile tile = World.world.GetTile((int)list[m].Item1.x, (int)list[m].Item1.y);
					Building building = World.world.buildings.addBuilding(array[m], tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						building.completeConstruction();
						if (kingdom != null)
						{
							building.setKingdom(kingdom);
						}
						if (m == 0)
						{
							DocksAt = list[m].Item1;
							DocksYaw = list[m].Item2;
						}
						Log.Info($"LOOK {array[m]} at {list[m].Item1}, water towards {list[m].Item2 * 57.29578f:F0} degrees");
					}
				}
				catch (Exception ex)
				{
					Log.Error("LOOK docks: " + ex.Message);
				}
			}
			bool Wet(int x, int y)
			{
				if (x >= 0 && y >= 0 && x < w && y < cache.Height)
				{
					return (cache.Flags[y * w + x] & 1) != 0;
				}
				return false;
			}
		}

		private static IEnumerator Spot(WorldBoxMod mod, Vector2 eye, float yaw, float height, string name)
		{
			mod.DebugEye = eye;
			mod.DebugEyeSet = true;
			mod.DebugEyeHeight = height;
			mod.SetYaw(yaw);
			mod.SetPitchDegrees((height > 1f) ? 2f : 4f);
			yield return new WaitForSecondsRealtime(0.8f);
			mod.SaveFrame(name);
			if (name.Contains("night_town0") || name.Contains("night_village") || name.Contains("day_docks") || name.Contains("day_wonders1"))
			{
				Nearby(mod, eye);
			}
			mod.DebugEyeSet = false;
			mod.DebugEyeHeight = 0f;
		}

		private static IEnumerator Measure(WorldBoxMod mod, string label, float turn = 0f)
		{
			List<float> frames = new List<float>();
			List<float> render = new List<float>();
			List<float> post = new List<float>();
			List<float> shadow = new List<float>();
			int rebuilds = mod.Renderer.ShadowRebuilds;
			for (int k = 0; k < 30; k++)
			{
				if (turn != 0f)
				{
					mod.SetYaw(mod.ViewYaw + turn);
				}
				yield return null;
				shadow.Add(mod.Renderer.ShadowMs);
				frames.Add(Time.unscaledDeltaTime * 1000f);
				render.Add(mod.RenderMs);
				post.Add(mod.Renderer.PostMs);
			}
			frames.Sort();
			render.Sort();
			post.Sort();
			shadow.Sort();
			Log.Info($"LOOK shadows {label}: map median {shadow[15]:F1} ms, worst {shadow[29]:F1} ms, cached part redrawn {mod.Renderer.ShadowRebuilds - rebuilds} times in 30 frames");
			FrameRenderer renderer = mod.Renderer;
			Log.Info($"LOOK perf {label}: models {renderer.Meshes.Count} ({renderer.MeshTriangles} triangles): prep {renderer.MeshPrepMs:F1}, shadow map {renderer.ShadowMs:F1}, raster {renderer.MeshRasterMs:F1}, all {renderer.MeshMs:F1} ms; game frame {frames[15]:F1} ms ({1000f / Mathf.Max(1f, frames[15]):F0} fps), render {render[15]:F1} ms (terrain {renderer.TerrainMs:F1}, objects {renderer.ObjectsMs:F1} [prep {renderer.PrepMs:F1}, blobs {renderer.BlobMs:F1}, shadows {renderer.VoxelShadowMs:F1}, voxels {renderer.VoxelMs:F1}, sprites {renderer.SpriteMs:F1}, {renderer.Stripes} stripes], light and finish {post[15]:F1}), {renderer.Lights.Count} lights, {mod.VoxelCount} voxels, {mod.BillboardCount} sprites, detail {mod.SceneForTest.Detail:F2}");
			yield return Breakdown(mod, label);
		}

		private static IEnumerator Breakdown(WorldBoxMod mod, string label)
		{
			float[] result = new float[3];
			for (int skip = 0; skip < 3; skip++)
			{
				mod.Renderer.DebugSkip = skip;
				List<float> voxels = new List<float>();
				for (int k = 0; k < 20; k++)
				{
					yield return null;
					voxels.Add(mod.Renderer.VoxelMs + mod.Renderer.VoxelShadowMs);
				}
				voxels.Sort();
				result[skip] = voxels[10];
			}
			mod.Renderer.DebugSkip = 0;
			Log.Info($"LOOK breakdown {label}: voxels and shadows {result[0]:F1} ms, without trees {result[1]:F1} ms, without people {result[2]:F1} ms");
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
			Log.Info("LOOK DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
