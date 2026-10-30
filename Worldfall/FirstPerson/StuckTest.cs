using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class StuckTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _me;

		private int _walks;

		private int _traps;

		private int _slow;

		private bool _done;

		private IEnumerator WatchRelief()
		{
			Relief r = _mod.SamplerForTest.Relief;
			short[] last = ((r.Gen != null) ? ((short[])r.Gen.Clone()) : null);
			int checks = 0;
			int changes = 0;
			string[] types = null;
			Dictionary<string, int> flips = new Dictionary<string, int>();
			while (!_done)
			{
				yield return new WaitForSecondsRealtime(1f);
				try
				{
					WorldTile[] tiles = _mod.SamplerForTest.Tiles;
					if (types == null || types.Length != tiles.Length)
					{
						types = new string[tiles.Length];
						for (int i = 0; i < tiles.Length; i++)
						{
							types[i] = tiles[i].main_type?.id;
						}
					}
					else
					{
						flips.Clear();
						int num = 0;
						for (int j = 0; j < tiles.Length; j++)
						{
							string text = tiles[j].main_type?.id;
							if (!(text == types[j]))
							{
								num++;
								string key = (types[j] ?? "-") + ">" + (text ?? "-");
								flips[key] = ((!flips.TryGetValue(key, out var value)) ? 1 : (value + 1));
								types[j] = text;
							}
						}
						if (num > 0)
						{
							List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(flips);
							list.Sort((KeyValuePair<string, int> x, KeyValuePair<string, int> y) => y.Value.CompareTo(x.Value));
							Log.Info(string.Format("STUCK tiles: {0} changed type: {1}", num, string.Join(", ", list.GetRange(0, Math.Min(5, list.Count)).ConvertAll((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value))));
						}
					}
				}
				catch (Exception ex)
				{
					Log.Info("STUCK tiles: " + ex.Message);
				}
				short[] gen = r.Gen;
				checks++;
				if (gen == null || last == null || gen.Length != last.Length)
				{
					last = ((gen != null) ? ((short[])gen.Clone()) : null);
					continue;
				}
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				float num5 = 0f;
				float num6 = float.MaxValue;
				int width = r.Width;
				Vector2 a = ((_me != null && _me.isAlive()) ? _me.current_position : Vector2.zero);
				List<int> list2 = new List<int>(ReliefGen.Chosen);
				lock (ReliefGen.LastTarns)
				{
					list2.AddRange(ReliefGen.LastTarns);
				}
				for (int num7 = 0; num7 < gen.Length; num7++)
				{
					if (gen[num7] == last[num7])
					{
						continue;
					}
					num2++;
					num5 = Mathf.Max(num5, (float)Mathf.Abs(gen[num7] - last[num7]) * (1f / 64f));
					int num8 = num7 % width;
					int num9 = num7 / width;
					num6 = Mathf.Min(num6, Vector2.Distance(a, new Vector2((float)num8 + 0.5f, (float)num9 + 0.5f)));
					foreach (int item in list2)
					{
						if (Mathf.Abs(item % width - num8) <= 6 && Mathf.Abs(item / width - num9) <= 6)
						{
							num4++;
							break;
						}
					}
					WorldTile tile = World.world.GetTile(num8, num9);
					if (tile != null && tile.zone != null && tile.zone.city != null)
					{
						num3++;
					}
				}
				int num10 = int.MaxValue;
				int num11 = int.MaxValue;
				int num12 = -1;
				int num13 = -1;
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				for (int num14 = 0; num14 < gen.Length; num14++)
				{
					if (gen[num14] != last[num14])
					{
						int num15 = num14 % width;
						int num16 = num14 / width;
						num10 = Math.Min(num10, num15);
						num11 = Math.Min(num11, num16);
						num12 = Math.Max(num12, num15);
						num13 = Math.Max(num13, num16);
						string key2 = World.world.GetTile(num15, num16)?.main_type?.id ?? "-";
						dictionary[key2] = ((!dictionary.TryGetValue(key2, out var value2)) ? 1 : (value2 + 1));
					}
				}
				if (num2 > 0)
				{
					List<KeyValuePair<string, int>> list3 = new List<KeyValuePair<string, int>>(dictionary);
					list3.Sort((KeyValuePair<string, int> x, KeyValuePair<string, int> y) => y.Value.CompareTo(x.Value));
					Log.Info(string.Format("STUCK relief:   in {0},{1}..{2},{3}, on {4}", num10, num11, num12, num13, string.Join(", ", list3.GetRange(0, Math.Min(4, list3.Count)).ConvertAll((KeyValuePair<string, int> kv) => kv.Key + " " + kv.Value))));
					changes++;
					WorldSampler samplerForTest = _mod.SamplerForTest;
					Log.Info($"STUCK relief: {num2} tiles of the generated land moved (most {num5:F2} tiles), {num4} by a tarn site, {num3} in a town's land, nearest {num6:F1} tiles from you; " + $"eased {samplerForTest.EasedForTest}, {samplerForTest.EaseLeftForTest} to go, {samplerForTest.PinnedForTest} held by buildings");
				}
				last = (short[])gen.Clone();
			}
			Log.Info($"STUCK relief: moved in {changes} of {checks} seconds");
		}

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
			Log.Info(string.Format("STUCK world {0}x{1}, models {2}", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing"));
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_me = KingTest.FoundKingdom(item, "human", 4, 0);
				if (_me != null)
				{
					break;
				}
			}
			if (_me == null)
			{
				Log.Error("STUCK could not found a village");
				Finish(quit);
				yield break;
			}
			City city = _me.city;
			WorldTile home = city.getTile() ?? _me.current_tile;
			ControllableUnit.setControllableCreature(_me);
			try
			{
				((BaseSimObject)_me).addStatusEffect("invincible", 9999f, pColorEffect: true);
			}
			catch (Exception ex)
			{
				Log.Info("STUCK no invincible: " + ex.Message);
			}
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("STUCK first person did not start");
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
			if (!options.Contains("flat"))
			{
				ReliefWorld.Choose(2);
				_mod.SamplerForTest.EnsureWorld();
				yield return new WaitForSecondsRealtime(2f);
			}
			Log.Info("STUCK land " + (_mod.SamplerForTest.Relief.Terrainy ? "Terrainy" : "Flat"));
			if (options.Contains("cage") || options.Contains("onlycage"))
			{
				yield return Run("cage", Cage(home));
			}
			if (options.Contains("onlycage"))
			{
				_done = true;
				Finish(quit);
				yield break;
			}
			List<WorldTile> taken = new List<WorldTile>();
			List<(Building b, string what)> built = new List<(Building, string)>();
			(string, bool, int)[] array = new(string, bool, int)[8]
			{
				("statue", false, 0),
				("statue", true, 0),
				("house_human_1", true, 4),
				("house_human_2", true, 5),
				("house_human_0", true, 0),
				("house_human_3", true, 0),
				("house_human_1", false, 0),
				("well", false, 0)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (text, flag2, num) = array[i];
				if (options.Contains("only") && !options.Contains("only" + text.Replace("house_human_", "h")))
				{
					continue;
				}
				Building building = Build(text, home, city, taken);
				if (building == null)
				{
					continue;
				}
				Building building2 = ((num > 0) ? Place(text, World.world.GetTile(building.current_tile.x + num, building.current_tile.y), city) : null);
				if (flag2)
				{
					try
					{
						building.makeRuins();
						building2?.makeRuins();
					}
					catch (Exception ex2)
					{
						Log.Error("STUCK could not ruin " + text + ": " + ex2.Message);
					}
				}
				built.Add((building, (flag2 ? "ruin of " : "") + text + ((building2 != null) ? $" (a twin {num} tiles east)" : "")));
			}
			StartCoroutine(WatchRelief());
			yield return new WaitForSecondsRealtime(2f);
			bool[] array2 = ((!options.Contains("nowalk")) ? new bool[2] { false, true } : new bool[1] { true });
			foreach (bool jumps in array2)
			{
				foreach (var (building3, text2) in built)
				{
					if (building3 == null || !building3.isAlive())
					{
						Log.Error("STUCK " + text2 + " is gone");
					}
					else
					{
						yield return Run((jumps ? "jumping at " : "") + text2, Walks(building3, text2, jumps));
					}
				}
			}
			_done = true;
			Log.Info($"STUCK all: slipped out {_mod.UnstuckForTest} times");
			Log.Info($"STUCK all: {_walks} walks, {_traps} traps, {_slow} spots left only slowly (under 0.3 tiles in 0.4 s every way)");
			if (_traps > 0)
			{
				Log.Error($"STUCK {_traps} traps");
			}
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("STUCK --- " + name);
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
					Log.Error("STUCK " + name + " failed: " + ex);
				}
				break;
				IL_0074:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
		}

		private float Drop(float x, float y, float half)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					float b = terrainForTest.SampleHeight(x + (float)j * half * 0.5f, y + (float)i * half * 0.5f);
					num = Mathf.Min(num, b);
					num2 = Mathf.Max(num2, b);
				}
			}
			return num2 - num;
		}

		private Building Build(string id, WorldTile home, City city, List<WorldTile> taken)
		{
			WorldTile worldTile = null;
			float num = 0f;
			for (int i = 9; i < 64; i += 2)
			{
				for (int j = 0; j < 32; j++)
				{
					float f = (float)j * MathF.PI * 2f / 32f + (float)i * 0.37f;
					WorldTile tile = World.world.GetTile(home.x + (int)(Mathf.Cos(f) * (float)i), home.y + (int)(Mathf.Sin(f) * (float)i));
					if (tile == null || !Clear(tile, 5))
					{
						continue;
					}
					bool flag = true;
					foreach (WorldTile item in taken)
					{
						if (Mathf.Abs(item.x - tile.x) < 14 && Mathf.Abs(item.y - tile.y) < 14)
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						float num2 = Drop((float)tile.x + 0.5f, (float)tile.y + 0.5f, 2.5f);
						if (!(num2 > 2f) && !(num2 <= num))
						{
							worldTile = tile;
							num = num2;
						}
					}
				}
			}
			if (worldTile == null)
			{
				Log.Error("STUCK no room for " + id);
				return null;
			}
			foreach (Building item2 in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item2 == null || !item2.isAlive() || item2.current_tile == null || Mathf.Abs(item2.current_tile.x - worldTile.x) > 7 || Mathf.Abs(item2.current_tile.y - worldTile.y) > 7)
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
				Building building = World.world.buildings.addBuilding(id, worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					Log.Error("STUCK could not put " + id);
					return null;
				}
				building.completeConstruction();
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				taken.Add(worldTile);
				Log.Info($"STUCK {id} at {worldTile.x},{worldTile.y}: the land falls {num:F2} across 5 tiles round it");
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("STUCK " + id + ": " + ex.Message);
				return null;
			}
		}

		private IEnumerator Cage(WorldTile home)
		{
			WorldTile c = null;
			for (int i = 6; i < 40; i += 2)
			{
				if (c != null)
				{
					break;
				}
				for (int j = 0; j < 16; j++)
				{
					if (c != null)
					{
						break;
					}
					WorldTile tile = World.world.GetTile(home.x + (int)(Mathf.Cos((float)j * 0.39f) * (float)i), home.y + (int)(Mathf.Sin((float)j * 0.39f) * (float)i));
					if (tile != null && Clear(tile, 3))
					{
						c = tile;
					}
				}
			}
			if (c == null)
			{
				Log.Error("STUCK no room for the cage");
				yield break;
			}
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item != null && item.isAlive() && item.current_tile != null && Mathf.Abs(item.current_tile.x - c.x) <= 3 && Mathf.Abs(item.current_tile.y - c.y) <= 3)
				{
					try
					{
						item.removeBuildingFinal();
					}
					catch
					{
					}
				}
			}
			Vector2 at = new Vector2((float)c.x + 0.5f, (float)c.y + 0.5f);
			int stones = 0;
			Colliders.ExtraForTest.Clear();
			for (int k = 0; k < 10; k++)
			{
				float f = (float)k * MathF.PI * 2f / 10f;
				Colliders.ExtraForTest.Add(new Collider
				{
					X = (float)c.x + 0.5f + Mathf.Cos(f) * 0.52f,
					Y = (float)c.y + 0.5f + Mathf.Sin(f) * 0.52f,
					Radius = 0.3f
				});
				stones++;
			}
			float nearest = float.MaxValue;
			for (float w = 0f; w < 6f; w += 0.25f)
			{
				if (!(nearest > 0.6f))
				{
					break;
				}
				Put(at);
				yield return new WaitForSecondsRealtime(0.25f);
				nearest = float.MaxValue;
				foreach (Collider item2 in Colliders.Near)
				{
					if (!item2.Mobile)
					{
						nearest = Mathf.Min(nearest, Colliders.Distance(item2, at, out var _));
					}
				}
			}
			Put(at);
			yield return null;
			int before = _mod.UnstuckForTest;
			Log.Info($"STUCK cage: {stones} posts round {c.x},{c.y}, the nearest {nearest:F2} off the middle; {Colliders.DescribeNear(at, 3)}");
			_mod.SetYaw(0.3f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 4f && _mod.UnstuckForTest == before)
			{
				yield return null;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			float num = Time.realtimeSinceStartup - t0;
			float magnitude = (_me.current_position - at).magnitude;
			if (_mod.UnstuckForTest > before)
			{
				Log.Info($"STUCK cage: slipped out after {num:F1} s, {magnitude:F2} tiles from the middle");
			}
			else
			{
				Log.Error($"STUCK cage: still held after {num:F1} s ({magnitude:F2} tiles from the middle)");
			}
			Colliders.ExtraForTest.Clear();
		}

		private static Building Place(string id, WorldTile t, City city)
		{
			if (t == null)
			{
				return null;
			}
			try
			{
				Building building = World.world.buildings.addBuilding(id, t, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					return null;
				}
				building.completeConstruction();
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("STUCK " + id + " twin: " + ex.Message);
				return null;
			}
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

		private void Put(Vector2 p)
		{
			if (_mod.Interior.Inside)
			{
				_mod.LeaveRoomForTest();
			}
			try
			{
				_me.current_position = p;
				_me.dirty_current_tile = true;
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

		private IEnumerator Walks(Building b, string what, bool jumps)
		{
			TerrainCache land = _mod.TerrainForTest;
			Vector2 middle = new Vector2((float)b.current_tile.x + 0.5f, (float)b.current_tile.y + 0.5f);
			Put(middle + new Vector2(0f, -5f));
			yield return new WaitForSecondsRealtime(1f);
			float reach = 2.5f;
			if (Drawn(b, out var found))
			{
				middle = new Vector2(found.X, found.Y);
				reach = found.Shape.Reach * found.Scale;
			}
			else
			{
				Log.Info("STUCK " + what + ": no model outline gathered (a box or a circle stands for it)");
			}
			float ground = land.SampleHeight(middle.x, middle.y);
			Log.Info($"STUCK {what}: reach {reach:F2}, the land falls {Drop(middle.x, middle.y, reach):F2} across it");
			int traps = 0;
			for (int k = 0; k < 16; k++)
			{
				float f = (float)k * MathF.PI / 8f - MathF.PI / 2f;
				Vector2 start = middle + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * (reach + 1.2f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				Put(start);
				Vector2 dir = (middle - start).normalized;
				_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
				_mod.SetPitchDegrees(-6f);
				yield return new WaitForSecondsRealtime(0.35f);
				Vector2 normalized = (middle + new Vector2(0f - dir.y, dir.x) * (k % 3 - 1) * reach * 0.35f - start).normalized;
				_mod.SetYaw(Mathf.Atan2(normalized.y, normalized.x));
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				PossessionHooks.ScriptedRun = jumps;
				float t0 = Time.realtimeSinceStartup;
				float lastJump = -1f;
				while (Time.realtimeSinceStartup - t0 < 2.5f)
				{
					float num = Time.realtimeSinceStartup - t0;
					if (jumps && num > 0.3f && _me.position_height <= 0.01f && num - lastJump > 0.4f)
					{
						PossessionHooks.ScriptedJump = true;
						lastJump = num;
					}
					yield return null;
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				for (float w = 0f; w < 2f; w += 0.05f)
				{
					if (!(_me.position_height > 0.01f))
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.05f);
				}
				yield return new WaitForSecondsRealtime(0.15f);
				if (_mod.Interior.Inside)
				{
					Log.Info($"STUCK {what} from {(float)k * 22.5f:F0} deg: walked in at its door");
					_mod.LeaveRoomForTest();
					continue;
				}
				Vector2 end = _me.current_position;
				_walks++;
				float best = 0f;
				string ways = "";
				for (int i = 0; i < 8; i++)
				{
					Put(end);
					float yaw = (float)i * MathF.PI / 4f;
					_mod.SetYaw(yaw);
					yield return null;
					yield return null;
					PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
					float w = Time.realtimeSinceStartup;
					while (Time.realtimeSinceStartup - w < 0.4f)
					{
						yield return null;
					}
					PossessionHooks.ScriptedInput = Vector2.zero;
					float magnitude = (_me.current_position - end).magnitude;
					best = Mathf.Max(best, magnitude);
					ways = ways + magnitude.ToString("F2") + " ";
				}
				Put(end);
				yield return null;
				yield return null;
				Collider found2;
				Vector2 normal;
				float num2 = (Drawn(b, out found2) ? Colliders.Distance(found2, end, out normal) : 99f);
				string text = $"{what} from {(float)k * 22.5f:F0} deg: walked {(end - start).magnitude:F2} in, ended {(end - middle).magnitude:F2} from the middle, " + $"{num2:F2} off its outline (nearest {Colliders.NearestForTest(end)}), ground {land.SampleHeight(end.x, end.y) - ground:+0.00;-0.00} on the middle's, " + Colliders.BodyForTest + "; away: " + ways;
				if (best < 0.06f)
				{
					traps++;
					_traps++;
					Log.Error("STUCK TRAP " + text);
					_mod.SaveFrame(string.Format("stuck_{0}{1}_{2}.png", jumps ? "j_" : "", what.Replace(' ', '_'), k));
					float w = 0f;
					for (int i = 0; i < 8; i++)
					{
						if (!(w < 0.3f))
						{
							break;
						}
						Put(end);
						_mod.SetYaw((float)i * MathF.PI / 4f);
						yield return null;
						PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
						PossessionHooks.ScriptedJump = true;
						yield return new WaitForSecondsRealtime(0.7f);
						PossessionHooks.ScriptedInput = Vector2.zero;
						w = Mathf.Max(w, (_me.current_position - end).magnitude);
					}
					Log.Info($"STUCK   a jump gets out {w:F2} tiles");
					Put(end);
				}
				else if (best < 0.3f)
				{
					_slow++;
					Log.Info("STUCK slow " + text);
				}
				else
				{
					Log.Info("STUCK " + text);
				}
			}
			Log.Info($"STUCK {what}: {traps} of 16 walks ended in a trap");
			if (!jumps)
			{
				_mod.SaveFrame("stuck_" + what.Replace(' ', '_') + ".png");
			}
		}

		private static void Finish(bool quit)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("STUCK DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
