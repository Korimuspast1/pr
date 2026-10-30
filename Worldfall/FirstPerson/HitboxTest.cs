using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HitboxTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

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
			Log.Info(string.Format("HITS world {0}x{1}, models {2}", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing"));
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 6, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("HITS could not found a village");
				Finish(quit);
				yield break;
			}
			City city = _king.city;
			WorldTile home = city.getTile() ?? _king.current_tile;
			List<Building> built = new List<Building>();
			List<WorldTile> taken = new List<WorldTile>();
			string[] array = new string[8] { "mine", "market_human", "stockpile", "house_human_0", "house_human_0", "house_human_0", "house_human_1", "house_human_2" };
			for (int i = 0; i < array.Length; i++)
			{
				Building building = Build(array[i], home, city, taken);
				if (building != null)
				{
					built.Add(building);
				}
			}
			Log.Info(string.Format("HITS built {0}: {1}", built.Count, string.Join(", ", built.ConvertAll((Building b) => Models.BuildingFamily(b) ?? b.asset.id))));
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("HITS first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			_mod.Day.TimeOfDay = 0.3f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			HashSet<string> families = new HashSet<string>();
			foreach (Building item2 in built)
			{
				string text2 = Models.BuildingFamily(item2);
				if (text2 != null && families.Add(text2))
				{
					yield return Run(text2, Approach(item2, text2));
				}
			}
			foreach (Building item3 in built)
			{
				if (item3.asset.type == "type_mine")
				{
					yield return Run("mine mouth", MineMouth(item3));
					break;
				}
			}
			int jumped = 0;
			foreach (Building item4 in built)
			{
				if (item4.asset.type == "type_house" && jumped++ < 3)
				{
					yield return Run("jumps " + Models.BuildingFamily(item4), Jumps(item4));
				}
			}
			yield return Run("trees", Trees());
			Log.Info($"HITS footprints worked out: {Colliders.BuiltCount} at once in {Colliders.BuiltMs:F0} ms (at most {Colliders.WorstFrameMs:F1} ms in a frame), " + $"{Colliders.BuiltAside} on workers in {Colliders.BuiltAsideMs:F0} ms; render {_mod.RenderMs:F1} ms");
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("HITS --- " + name);
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
					goto IL_0074;
				}
				catch (Exception ex)
				{
					Log.Error("HITS " + name + " failed: " + ex);
				}
				break;
				IL_0074:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			PossessionHooks.ScriptedJump = false;
		}

		private static Building Build(string id, WorldTile home, City city, List<WorldTile> taken)
		{
			for (int i = 9; i < 34; i += 3)
			{
				for (int j = 0; j < 24; j++)
				{
					float f = (float)j * MathF.PI * 2f / 24f + (float)i * 0.37f;
					WorldTile tile = World.world.GetTile(home.x + (int)(Mathf.Cos(f) * (float)i), home.y + (int)(Mathf.Sin(f) * (float)i));
					if (tile == null || !Clear(tile, 6))
					{
						continue;
					}
					bool flag = true;
					foreach (WorldTile item in taken)
					{
						if (Mathf.Abs(item.x - tile.x) < 15 && Mathf.Abs(item.y - tile.y) < 15)
						{
							flag = false;
							break;
						}
					}
					if (!flag)
					{
						continue;
					}
					foreach (Building item2 in new List<Building>(World.world.buildings.getSimpleList()))
					{
						if (item2 == null || !item2.isAlive() || item2.current_tile == null || Mathf.Abs(item2.current_tile.x - tile.x) > 8 || Mathf.Abs(item2.current_tile.y - tile.y) > 8)
						{
							continue;
						}
						try
						{
							if (!item2.isKingdomCiv())
							{
								item2.removeBuildingFinal();
							}
						}
						catch
						{
						}
					}
					try
					{
						Building building = World.world.buildings.addBuilding(id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
						if (building == null)
						{
							continue;
						}
						building.completeConstruction();
						building.setKingdom(city.kingdom);
						city.listBuilding(building);
						taken.Add(tile);
						Log.Info($"HITS {id} at {tile.x},{tile.y} ({Models.BuildingFamily(building)})");
						return building;
					}
					catch (Exception ex)
					{
						Log.Error("HITS " + id + ": " + ex.Message);
					}
				}
			}
			Log.Error("HITS no room for " + id);
			return null;
		}

		private static bool Clear(WorldTile t, int half)
		{
			for (int i = -half; i <= half; i++)
			{
				for (int j = -half; j <= half; j++)
				{
					WorldTile tile = World.world.GetTile(t.x + j, t.y + i);
					if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava)
					{
						return false;
					}
					if (tile.building != null && tile.building.isKingdomCiv())
					{
						return false;
					}
				}
			}
			return true;
		}

		private static void Put(Actor a, Vector2 p)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null && instance.Interior.Inside)
			{
				instance.LeaveRoomForTest();
			}
			try
			{
				a.current_position = p;
				a.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private static bool Drawn(Building b, out Collider found)
		{
			found = default(Collider);
			foreach (Collider item in Colliders.Near)
			{
				if (item.Shape != null && item.Owner == b)
				{
					found = item;
					return true;
				}
			}
			return false;
		}

		private static Vector2 FirstTouch(Collider c, Vector2 from, Vector2 to, float radius)
		{
			Vector2 vector = to - from;
			float magnitude = vector.magnitude;
			for (float num = 0f; num <= magnitude; num += 0.02f)
			{
				Vector2 vector2 = from + vector * (num / magnitude);
				if (Colliders.Distance(c, vector2, out var _) < radius)
				{
					return vector2;
				}
			}
			return to;
		}

		private IEnumerator Approach(Building b, string family)
		{
			Actor me = _king;
			Put(me, new Vector2((float)b.current_tile.x + 0.5f, (float)b.current_tile.y - 4.5f));
			yield return new WaitForSecondsRealtime(1f);
			if (!Drawn(b, out var now))
			{
				Log.Error("HITS no model outline for " + family);
				yield break;
			}
			TerrainCache land = _mod.TerrainForTest;
			MeshModel meshModel = Models.Get(family);
			Collider old = Colliders.WithBand(now, meshModel, (meshModel != null && meshModel.Info("kind") == "docks") ? 0.8f : 0.06f, 1.6f);
			Vector2 centre = new Vector2(now.X, now.Y);
			float reach = now.Shape.Reach * now.Scale;
			float gainedSum = 0f;
			int shortStops = 0;
			for (int k = 0; k < 8; k++)
			{
				float f = (float)k * MathF.PI / 4f - MathF.PI / 2f;
				Vector2 start = centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * (reach + 1.5f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				Put(me, start);
				Vector2 dir = (centre - start).normalized;
				_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
				_mod.SetPitchDegrees(-6f);
				yield return new WaitForSecondsRealtime(0.4f);
				if (Drawn(b, out var found))
				{
					now = found;
				}
				float bodyRadius = PossessionHooks.BodyRadius;
				Vector2 stopNew = FirstTouch(now, start, centre, bodyRadius);
				Vector2 stopOld = FirstTouch(old, start, centre, bodyRadius);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				int inBefore = WorldBoxMod.DoorStepsIn;
				float t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 3.5f)
				{
					yield return null;
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				if (WorldBoxMod.DoorStepsIn > inBefore)
				{
					Log.Info($"HITS {family} from {k * 45} deg: walked in at its door");
					continue;
				}
				Vector2 current_position = me.current_position;
				float num = Vector2.Dot(current_position - start, dir);
				float magnitude = (stopNew - start).magnitude;
				float magnitude2 = (stopOld - start).magnitude;
				gainedSum += magnitude - magnitude2;
				if (num < magnitude - 0.3f)
				{
					shortStops++;
				}
				Collider found2;
				bool flag = Drawn(b, out found2);
				string text = $"HITS {family} from {k * 45} deg: walked {num:F2} in (its footprint stops you at {magnitude:F2}, the old one at {magnitude2:F2}: {magnitude - magnitude2:+0.00;-0.00} closer), ";
				Vector2 normal = current_position - centre;
				Log.Info(text + $"ended {normal.magnitude:F2} from the middle, {(flag ? Colliders.Distance(found2, current_position, out normal) : (-1f)):F2} off its outline; " + $"ground {land.SampleHeight(current_position.x, current_position.y) - land.SampleHeight(centre.x, centre.y):+0.00;-0.00} on the middle's, {Colliders.BodyForTest}");
				if (k == 0)
				{
					_mod.SaveFrame("hits_" + family + "_front.png");
				}
			}
			Log.Info($"HITS {family}: on average {gainedSum / 8f:F2} tiles closer than before, {shortStops} of 8 walks stopped short of the outline (something else in the way)");
		}

		private IEnumerator MineMouth(Building mine)
		{
			Actor me = _king;
			Put(me, new Vector2((float)mine.current_tile.x + 0.5f, (float)mine.current_tile.y - 5f));
			yield return new WaitForSecondsRealtime(1f);
			if (!MineInterior.Mouth(mine, out var mouth) || !Drawn(mine, out var now))
			{
				Log.Error("HITS the mine isn't drawn");
				yield break;
			}
			MeshModel model = Models.Get("mine");
			Collider old = Colliders.WithBand(now, model, 0.06f, 1.6f);
			float best = float.MaxValue;
			string prompt = null;
			float[] array = new float[3] { 0f, -1.1f, 1.1f };
			foreach (float side in array)
			{
				Vector2 start = mouth + new Vector2(side, -4.5f);
				Put(me, start);
				Vector2 normalized = (mouth - start).normalized;
				_mod.SetYaw(Mathf.Atan2(normalized.y, normalized.x));
				_mod.SetPitchDegrees(-4f);
				yield return new WaitForSecondsRealtime(0.5f);
				if (Drawn(mine, out var found))
				{
					now = found;
				}
				float bodyRadius = PossessionHooks.BodyRadius;
				float oldGap = MineInterior.MouthDistance(mine, FirstTouch(old, start, mouth, bodyRadius));
				float newGap = MineInterior.MouthDistance(mine, FirstTouch(now, start, mouth, bodyRadius));
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(4f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				_mod.SetYaw(MathF.PI / 2f);
				yield return new WaitForSecondsRealtime(0.4f);
				float num = MineInterior.MouthDistance(mine, me.current_position);
				Log.Info($"HITS mine mouth from {side:+0.0;-0.0} across: walked up to {num:F2} from it (its footprint lets you to {newGap:F2}, the old one stopped you {oldGap:F2} off), prompt '{_mod.Prompt}'");
				if (num < best)
				{
					best = num;
					prompt = _mod.Prompt;
				}
				if (side == 0f)
				{
					_mod.SaveFrame("hits_mine_mouth.png");
				}
			}
			if (best > 1.2f)
			{
				Log.Error($"HITS the mine's mouth is still out of reach ({best:F2} tiles)");
			}
			if (prompt == null || !prompt.Contains("mine"))
			{
				Log.Error("HITS no way into the mine at its mouth (prompt '" + prompt + "')");
			}
		}

		private IEnumerator Jumps(Building house)
		{
			Actor me = _king;
			string family = Models.BuildingFamily(house);
			Put(me, new Vector2((float)house.current_tile.x + 0.5f, (float)house.current_tile.y - 4.5f));
			yield return new WaitForSecondsRealtime(1f);
			if (!Drawn(house, out var walls))
			{
				Log.Error("HITS no outline for " + family);
				yield break;
			}
			MeshModel model = Models.Get(family);
			Colliders.WalkerBand(walls.Scale, out var lo, out var hi);
			walls = Colliders.WithBand(walls, model, lo, hi);
			Vector2 centre = new Vector2(walls.X, walls.Y);
			float reach = walls.Shape.Reach * walls.Scale;
			int inside = 0;
			int frames = 0;
			int eyeInside = 0;
			int walkerInside = 0;
			float deepest = 0f;
			float eyeDeepest = 0f;
			float peak = 0f;
			bool saved = false;
			for (int k = 0; k < 4; k++)
			{
				float f = (float)k * MathF.PI * 0.5f - MathF.PI / 2f;
				Vector2 vector = centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * (reach + 1.6f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				Put(me, vector);
				Vector2 normalized = (centre - vector).normalized;
				_mod.SetYaw(Mathf.Atan2(normalized.y, normalized.x));
				_mod.SetPitchDegrees(8f);
				for (float w = 0f; w < 2f; w += 0.1f)
				{
					if (!(me.position_height > 0f))
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.1f);
				}
				yield return new WaitForSecondsRealtime(0.4f);
				PossessionHooks.ScriptedRun = true;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				float t0 = Time.realtimeSinceStartup;
				float lastJump = -1f;
				WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
				while (Time.realtimeSinceStartup - t0 < 4f)
				{
					yield return endOfFrame;
					float num = Time.realtimeSinceStartup - t0;
					if (me.position_height <= 0.01f && num - lastJump > 0.35f)
					{
						PossessionHooks.ScriptedJump = true;
						lastJump = num;
					}
					if (!_mod.Interior.Inside)
					{
						frames++;
						peak = Mathf.Max(peak, me.position_height);
						Collider found;
						Collider c = (Drawn(house, out found) ? found : walls);
						Vector2 normal;
						float num2 = Colliders.Distance(c, me.current_position, out normal);
						if (num2 < -0.12f)
						{
							inside++;
						}
						deepest = Mathf.Min(deepest, num2);
						if (Colliders.Distance(walls, me.current_position, out normal) < -0.03f)
						{
							walkerInside++;
						}
						Vector3 eyeForTest = _mod.EyeForTest;
						float num3 = Colliders.Distance(c, new Vector2(eyeForTest.x, eyeForTest.y), out normal);
						if (num3 < -0.15f)
						{
							eyeInside++;
						}
						eyeDeepest = Mathf.Min(eyeDeepest, num3);
						if (!saved && k == 0 && me.position_height > 0.45f && num2 < PossessionHooks.BodyRadius + 0.3f)
						{
							_mod.SaveFrame("hits_jump_" + family + ".png");
							saved = true;
						}
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
			}
			Log.Info($"HITS jumps at {family}: {frames} frames, peak {peak:F2}, body inside the walls {inside} frames (deepest {deepest:F2}), eye inside {eyeInside} (deepest {eyeDeepest:F2}); " + $"inside a walker's outline {walkerInside} frames");
			if (inside > 0)
			{
				Log.Error($"HITS jumping at {family} put the body inside its walls ({inside} frames, {deepest:F2} deep)");
			}
			if (eyeInside > 0)
			{
				Log.Error($"HITS jumping at {family} put the eye inside its walls ({eyeInside} frames)");
			}
			if (!saved)
			{
				_mod.SaveFrame("hits_jump_" + family + ".png");
			}
		}

		private IEnumerator Trees()
		{
			Actor me = _king;
			WorldTile home = me.current_tile;
			Dictionary<string, string> seen = new Dictionary<string, string>();
			int over = 0;
			int under = 0;
			int count = 0;
			for (int r = 12; r < 90; r += 7)
			{
				if (count >= 40)
				{
					break;
				}
				for (int k = 0; k < 12; k++)
				{
					if (count >= 40)
					{
						break;
					}
					WorldTile tile = World.world.GetTile(home.x + (int)(Mathf.Cos((float)k * 0.52f + (float)r) * (float)r), home.y + (int)(Mathf.Sin((float)k * 0.52f + (float)r) * (float)r));
					if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block)
					{
						continue;
					}
					Put(me, new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f));
					SceneCollector.TreesForTest = new List<SceneCollector.TreeSeen>();
					yield return null;
					yield return null;
					List<SceneCollector.TreeSeen> treesForTest = SceneCollector.TreesForTest;
					SceneCollector.TreesForTest = null;
					foreach (SceneCollector.TreeSeen item in treesForTest)
					{
						string key = item.Id + "/" + item.Model.Name;
						if (seen.ContainsKey(key) || item.Model == null)
						{
							continue;
						}
						Colliders.WalkerBand(item.Scale, out var lo, out var hi);
						Collider c = Colliders.FromModel(item.Model, item.At.x, item.At.y, item.Angle, item.Scale, lo, hi);
						float num = 0f;
						for (int i = 0; i < 16; i++)
						{
							Vector2 vector = new Vector2(Mathf.Cos((float)i * 0.3927f), Mathf.Sin((float)i * 0.3927f));
							float num2 = 0f;
							for (float num3 = 0f; num3 < 3f; num3 += 0.01f)
							{
								if (Colliders.Distance(c, item.At + vector * num3, out var _) < 0f)
								{
									num2 = num3;
								}
							}
							num += num2 / 16f;
						}
						seen[key] = $"{item.Id} ({item.Model.Name}, {item.Scale:F2} tiles/unit): blocks at {item.Radius:F2}, drawn trunk {num:F2}";
						if (item.Radius > num + 0.08f)
						{
							over++;
						}
						if (item.Radius < num - 0.08f)
						{
							under++;
						}
						count++;
					}
				}
			}
			foreach (string value in seen.Values)
			{
				Log.Info("HITS tree " + value);
			}
			Log.Info($"HITS trees: {count} kinds, {over} block wider than their trunk, {under} narrower");
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
			Log.Info("HITS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
