using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class StairsTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _me;

		private int _pass;

		private int _fail;

		private static readonly string[] Houses = new string[7] { "house_human_2", "house_human_4", "house_elf_2", "house_dwarf_2", "house_orc_3", "house_elf_4", "house_human_1" };

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
			Log.Info(string.Format("STAIRS world {0}x{1}, models {2}", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing"));
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_me = KingTest.FoundKingdom(item, "human", 6, 0);
				if (_me != null)
				{
					break;
				}
			}
			if (_me == null)
			{
				Log.Error("STAIRS could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(_me);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("STAIRS first person did not start");
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
			if (!options.Contains("roomsonly"))
			{
				yield return Run("steps", Steps());
				yield return Run("crates", Crates());
			}
			if (!options.Contains("outonly"))
			{
				yield return Run("rooms", Rooms());
			}
			Log.Info($"STAIRS result: {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private void Check(bool ok, string what)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			if (ok)
			{
				Log.Info("STAIRS PASS " + what);
			}
			else
			{
				Log.Error("STAIRS FAIL " + what);
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("STAIRS --- " + name);
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
					goto IL_008c;
				}
				catch (Exception ex)
				{
					Log.Error("STAIRS " + name + " failed: " + ex);
					_fail++;
				}
				break;
				IL_008c:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			PossessionHooks.ScriptedJump = false;
		}

		private float Ground(Vector2 p)
		{
			return _mod.SamplerForTest.Cache.SampleHeight(p.x, p.y);
		}

		private static bool Dry(int x0, int y0, int w, int h)
		{
			for (int i = y0; i < y0 + h; i++)
			{
				for (int j = x0; j < x0 + w; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava)
					{
						return false;
					}
					if (tile.building != null && !Clearable(tile.building))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool Clearable(Building b)
		{
			BuildingType buildingType = ((b.asset == null) ? BuildingType.Building_Tree : b.asset.building_type);
			if (buildingType != BuildingType.Building_Tree && buildingType != BuildingType.Building_Mineral)
			{
				return buildingType == BuildingType.Building_Plant;
			}
			return true;
		}

		private static void ClearBox(int x0, int y0, int w, int h)
		{
			for (int i = y0; i < y0 + h; i++)
			{
				for (int j = x0; j < x0 + w; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.building != null && Clearable(tile.building))
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

		private IEnumerator Steps()
		{
			WorldTile worldTile = ((_me.city != null) ? _me.city.getTile() : _me.current_tile);
			List<Building> list = new List<Building>();
			List<Vector2Int> list2 = new List<Vector2Int>();
			string[] houses = Houses;
			foreach (string text in houses)
			{
				Building building = null;
				for (int j = 12; j < 60; j += 4)
				{
					if (building != null)
					{
						break;
					}
					for (int k = 0; k < 24; k++)
					{
						if (building != null)
						{
							break;
						}
						int num = worldTile.x + (int)(Mathf.Cos((float)k * 0.2618f) * (float)j);
						int num2 = worldTile.y + (int)(Mathf.Sin((float)k * 0.2618f) * (float)j);
						bool flag = true;
						foreach (Vector2Int item in list2)
						{
							if (Mathf.Abs(item.x - num) < 9 && Mathf.Abs(item.y - num2) < 11)
							{
								flag = false;
								break;
							}
						}
						if (!flag || !Dry(num - 3, num2 - 6, 7, 9))
						{
							continue;
						}
						ClearBox(num - 3, num2 - 6, 7, 9);
						try
						{
							building = World.world.buildings.addBuilding(text, World.world.GetTile(num, num2), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
							if (building != null)
							{
								building.completeConstruction();
								list2.Add(new Vector2Int(num, num2));
							}
						}
						catch (Exception ex)
						{
							Log.Info("STAIRS place " + text + ": " + ex.Message);
							building = null;
						}
					}
				}
				if (building == null)
				{
					Log.Info("STAIRS no room for " + text);
				}
				else
				{
					list.Add(building);
				}
			}
			foreach (Building b in list)
			{
				if (b == null || !b.isAlive())
				{
					continue;
				}
				Vector2 current_position = b.current_position;
				Put(current_position + new Vector2(0f, -4f));
				Vector2 from = Vector2.zero;
				Vector2 door = Vector2.zero;
				float up = 0f;
				bool flag2 = false;
				for (float t = 0f; t < 4f; t += 0.25f)
				{
					if (flag2)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
					flag2 = BuildingFloors.DoorClimbForTest(b, PossessionHooks.BodyRadius, RoomFloor.StepOf(1f), out up, out from, out door);
				}
				MeshModel meshModel = BuildingFloors.ModelOf(b);
				string name = ((meshModel != null) ? meshModel.Name : b.asset.id);
				if (meshModel != null && !Steering.DoorOf(meshModel, out var at))
				{
					Log.Info("STAIRS " + name + ": no door to walk up to");
					continue;
				}
				if (!flag2)
				{
					Check(ok: false, name + ": its floors were never ready to stand on");
					continue;
				}
				BuildingFloors.WorldOf(b, 0f, 0f, out at, out var baseZ, out var _);
				Put(from);
				_mod.SetYaw(Mathf.Atan2(door.y - from.y, door.x - from.x));
				_mod.SetPitchDegrees(-12f);
				for (float t = 0f; t < 2f; t += 0.1f)
				{
					yield return new WaitForSecondsRealtime(0.1f);
					if (!_mod.Swimming && (Colliders.BodyForTest.Contains("body 0.9") || Colliders.BodyForTest.Contains("body 1.")))
					{
						break;
					}
				}
				float best = float.NegativeInfinity;
				float stuck = 0f;
				Vector2 last = _me.current_position;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				bool saved = false;
				bool steppedIn = false;
				StringBuilder trace = new StringBuilder();
				float nextTrace = 0f;
				byte what;
				for (float t = 0f; t < 5f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					float floorForTest = _mod.FloorForTest;
					if (!float.IsNaN(floorForTest))
					{
						best = Mathf.Max(best, floorForTest);
					}
					Vector2 current_position2 = _me.current_position;
					if (t >= nextTrace)
					{
						nextTrace += 0.2f;
						float z;
						bool flag3 = BuildingFloors.StandOn(current_position2.x, current_position2.y, PossessionHooks.BodyRadius, Ground(current_position2) + 2f, out z, out what);
						trace.Append(string.Format("[{0:F2} off: ground {1:F2} floor {2} top {3}] ", (current_position2 - door).magnitude, Ground(current_position2) - baseZ, float.IsNaN(floorForTest) ? "-" : (floorForTest - baseZ).ToString("F2"), flag3 ? (z - baseZ).ToString("F2") : "-"));
					}
					stuck = (((current_position2 - last).magnitude < 0.002f) ? (stuck + Time.unscaledDeltaTime) : 0f);
					last = current_position2;
					if (!saved && best > baseZ + up - 0.03f && up > 0.05f)
					{
						_mod.SaveFrame("stairs_" + name + ".png");
						saved = true;
					}
					if (stuck > 0.8f || (current_position2 - door).magnitude < 0.12f)
					{
						break;
					}
					if (_mod.Interior.Inside && _mod.Interior.House == b)
					{
						steppedIn = true;
						break;
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				Vector2 current_position3 = _me.current_position;
				float num3 = (steppedIn ? 0f : (door - current_position3).magnitude);
				float num4 = Ground(door) - baseZ;
				float num5 = (float.IsNegativeInfinity(best) ? 0f : (best - baseZ));
				float num6 = Mathf.Max(Ground(current_position3), float.IsNaN(_mod.FloorForTest) ? Ground(current_position3) : _mod.FloorForTest) - baseZ;
				Vector2 vector = current_position3 + (door - current_position3).normalized * 0.3f;
				float num7 = 0f;
				for (float num8 = 0.05f; num8 < 1.2f; num8 += 0.05f)
				{
					if (num7 != 0f)
					{
						break;
					}
					if (BuildingFloors.StandOn(vector.x, vector.y, PossessionHooks.BodyRadius, Ground(current_position3) + num8, out var z2, out what) && z2 > Ground(current_position3) + 0.02f)
					{
						num7 = z2 - Ground(current_position3);
					}
				}
				Log.Info($"STAIRS {name}: steps go up {up:F2} over its base, you got up {num5:F2}, feet {num6:F2} at the end (the ground at its door {num4:F2} over the base), ended {num3:F2} from the door, the next step {num7:F2} over the ground there, {Colliders.BodyForTest}");
				Log.Info("STAIRS   " + trace);
				bool num9 = num3 > 0.4f && (num7 > RoomFloor.StepOf(1f) + 0.01f || Ground(current_position3) - baseZ > up + 0.1f);
				bool flag4 = !num9 && num3 > 1f && float.IsNegativeInfinity(best) && num7 == 0f;
				if (num9)
				{
					Log.Info($"STAIRS {name}: on its slope (the ground {Ground(current_position3) - baseZ:F2} over its base where you stopped, its next step {num7:F2} over that): not a walk up its steps");
				}
				else if (flag4)
				{
					Log.Info($"STAIRS {name}: a wall or fence across the way to its door, {num3:F2} from it: not a walk up its steps");
				}
				if (!num9 && !flag4)
				{
					Check(num6 > Mathf.Min(up, up + num4) - 0.05f || num4 > up - 0.05f, $"{name}: up its steps to its door ({num6:F2} of {up:F2})");
					Check(num3 < 0.45f, $"{name}: walked right up to its door ({num3:F2} tiles off)");
				}
				if (!saved)
				{
					_mod.SaveFrame("stairs_" + name + ".png");
				}
				_mod.SetYaw(Mathf.Atan2(from.y - door.y, from.x - door.x));
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(1.6f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(0.3f);
				Check(!_mod.OnFloor, $"{name}: back down on the ground (floor {_mod.FloorForTest:F2})");
			}
		}

		private void Put(Vector2 at)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			_me.current_position = at;
			_me.dirty_current_tile = true;
		}

		private IEnumerator Crates()
		{
			Building stock = BuildStockpile(_me.city);
			if (stock == null)
			{
				Check(ok: false, "no stockpile");
				yield break;
			}
			Put(stock.current_position + new Vector2(0f, -4f));
			Vector2 at = Vector2.zero;
			float z = 0f;
			bool found = false;
			byte what;
			for (float t = 0f; t < 4f; t += 0.25f)
			{
				if (found)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
				Vector2 current_position = stock.current_position;
				for (float num = current_position.y - 3f; num < current_position.y + 3f; num += 0.05f)
				{
					if (found)
					{
						break;
					}
					for (float num2 = current_position.x - 3f; num2 < current_position.x + 3f; num2 += 0.1f)
					{
						if (found)
						{
							break;
						}
						Vector2 vector = new Vector2(num2, num);
						if (!BuildingFloors.StandOn(vector.x, vector.y, PossessionHooks.BodyRadius, Ground(vector) + 0.6f, out var z2, out what) || z2 - Ground(vector) < 0.3f || !BuildingFloors.StandOn(vector.x, vector.y, PossessionHooks.BodyRadius, z2 + 0.05f, out var z3, out what) || Mathf.Abs(z3 - z2) > 0.02f)
						{
							continue;
						}
						bool flag = true;
						for (float num3 = 0.4f; num3 < 1.8f && flag; num3 += 0.2f)
						{
							if (BuildingFloors.StandOn(vector.x, vector.y - num3, PossessionHooks.BodyRadius, Ground(vector) + 1.5f, out var z4, out what) && z4 > Ground(vector) + 0.1f)
							{
								flag = false;
							}
						}
						if (flag)
						{
							at = vector;
							z = z2;
							found = true;
						}
					}
				}
			}
			if (!found)
			{
				Log.Info("STAIRS stockpile: no crate top in a jump's reach (0.25-0.6 tiles)");
				yield break;
			}
			float ground = Ground(at);
			float z5;
			bool flag2 = BuildingFloors.StandOn(at.x, at.y, PossessionHooks.BodyRadius, z + 0.05f, out z5, out what);
			float z6;
			bool flag3 = BuildingFloors.StandOn(at.x, at.y, PossessionHooks.BodyRadius, ground + 0.6f, out z6, out what);
			Log.Info(string.Format("STAIRS stockpile crate top {0:F2} over the ground at {1} (standing there: {2}, reaching 0.6: {3}, r {4:F2}); {5}", z - ground, at, flag2 ? (z5 - ground).ToString("F2") : "nothing", flag3 ? (z6 - ground).ToString("F2") : "nothing", PossessionHooks.BodyRadius, BuildingFloors.DescribeForTest(stock, at)));
			float[] runUps = new float[5] { 0.9f, 1f, 0.8f, 1.1f, 0.7f };
			bool onIt = false;
			bool stayed = false;
			for (int attempt = 0; attempt < runUps.Length; attempt++)
			{
				if (stayed)
				{
					break;
				}
				Vector2 at2 = at + new Vector2(0f, 0f - runUps[attempt]);
				Put(at2);
				_mod.SetYaw(MathF.PI / 2f);
				_mod.SetPitchDegrees(-20f);
				yield return new WaitForSecondsRealtime(0.5f);
				PossessionHooks.ScriptedJump = true;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				float t = float.NaN;
				StringBuilder frames = new StringBuilder();
				for (float t2 = 0f; t2 < 1.2f; t2 += Time.unscaledDeltaTime)
				{
					yield return null;
					float floorForTest = _mod.FloorForTest;
					if (t2 > 0.7f || !float.IsNaN(floorForTest))
					{
						PossessionHooks.ScriptedInput = Vector2.zero;
					}
					if (!float.IsNaN(floorForTest) && (float.IsNaN(t) || floorForTest > t))
					{
						t = floorForTest;
					}
					Vector2 current_position2 = _me.current_position;
					frames.Append(string.Format("[{0:F2} {1:F2} h {2:F2} fl {3}] ", t2, current_position2.y - at.y, _me.position_height, float.IsNaN(floorForTest) ? "-" : (floorForTest - ground).ToString("F2")));
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				bool flag4 = !float.IsNaN(t) && Mathf.Abs(t - z) < 0.05f;
				Log.Info(string.Format("STAIRS jump from {0:F2} in front: highest floor {1} (the crate {2:F2}), at {3}", runUps[attempt], float.IsNaN(t) ? "none" : (t - ground).ToString("F2"), z - ground, _me.current_position - at));
				if (!flag4)
				{
					Log.Info("STAIRS   frames: " + frames);
					continue;
				}
				onIt = true;
				_mod.SaveFrame("stairs_on_crates.png");
				yield return new WaitForSecondsRealtime(1.2f);
				stayed = _mod.OnFloor && Mathf.Abs(_mod.FloorForTest - z) < 0.05f;
				Log.Info(string.Format("STAIRS   a second on: floor {0}, lift {1:F2}, at {2}", float.IsNaN(_mod.FloorForTest) ? "-" : (_mod.FloorForTest - ground).ToString("F2"), _mod.LiftForTest, _me.current_position - at));
			}
			Check(onIt, "jumped up onto a stockpile's crate");
			if (onIt)
			{
				Check(stayed, "stays up on the crate");
			}
		}

		private Building BuildStockpile(City city)
		{
			if (city == null)
			{
				return null;
			}
			WorldTile tile = city.getTile();
			int num = 0;
			int num2 = 0;
			float num3 = float.MaxValue;
			for (int i = 8; i < 70; i += 3)
			{
				for (int j = 0; j < 24; j++)
				{
					int num4 = tile.x + (int)(Mathf.Cos((float)j * 0.2618f + 0.4f) * (float)i);
					int num5 = tile.y + (int)(Mathf.Sin((float)j * 0.2618f + 0.4f) * (float)i);
					if (!Dry(num4 - 4, num5 - 5, 9, 9))
					{
						continue;
					}
					float num6 = float.MaxValue;
					float num7 = float.MinValue;
					for (int k = -4; k <= 3; k++)
					{
						for (int l = -3; l <= 3; l++)
						{
							float b = Ground(new Vector2((float)num4 + 0.5f + (float)l, (float)num5 + 0.5f + (float)k));
							num6 = Mathf.Min(num6, b);
							num7 = Mathf.Max(num7, b);
						}
					}
					if (num7 - num6 < num3)
					{
						num3 = num7 - num6;
						num = num4;
						num2 = num5;
					}
				}
			}
			if (num3 == float.MaxValue)
			{
				return null;
			}
			ClearBox(num - 4, num2 - 5, 9, 9);
			try
			{
				Building building = World.world.buildings.addBuilding("stockpile", World.world.GetTile(num, num2), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					return null;
				}
				building.completeConstruction();
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				Log.Info($"STAIRS stockpile at {num},{num2} (the ground within {num3:F2})");
				return building;
			}
			catch (Exception ex)
			{
				Log.Info("STAIRS stockpile: " + ex.Message);
			}
			return null;
		}

		private IEnumerator Rooms()
		{
			yield return Room("human_stone", "human", 3);
			yield return Room("human_timber", "human", 2);
			yield return Room("dwarf_stump", "dwarf", 4);
		}

		private IEnumerator Room(string style, string race, int tier)
		{
			_mod.EnterRoomForTest(new List<Actor>(), race, tier, style);
			for (float t = 0f; t < 4f; t += 0.1f)
			{
				if (RoomFloors.Ready)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			Interior room = _mod.Interior.Room;
			Check(RoomFloors.Ready, $"{style}: its floors are ready ({room?.Meshes.Count ?? 0} models)");
			if (!RoomFloors.Ready || room == null)
			{
				_mod.LeaveRoomForTest();
				yield break;
			}
			if (style == "human_stone")
			{
				yield return Stair(room);
			}
			else if (style == "human_timber")
			{
				yield return Ladder(room);
			}
			else
			{
				yield return Spiral(room);
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.LeaveRoomForTest();
			yield return new WaitForSecondsRealtime(1f);
		}

		private static MeshInstance Piece(Interior room, string name)
		{
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (mesh.Model != null && mesh.Model.Name == name)
				{
					return mesh;
				}
			}
			return null;
		}

		private static Vector2 At(MeshInstance inst, float mx, float my)
		{
			Vec3 vec = inst.Position + inst.Basis.Mul(new Vec3(mx, my, 0f));
			return new Vector2(vec.X, vec.Y);
		}

		private IEnumerator Walk(float yaw, float seconds, float[] best)
		{
			_mod.SetYaw(yaw);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				yield return null;
				best[0] = Mathf.Max(best[0], RoomFloors.Feet);
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
		}

		private IEnumerator Stair(Interior room)
		{
			MeshInstance stair = Piece(room, "int_v_stair");
			if (stair == null)
			{
				Check(ok: false, "human_stone: no stair in the room");
				yield break;
			}
			float s = stair.Basis.C0.X;
			_mod.Interior.Eye = At(stair, -1.3f - 0.4f / s * 0.6f, 0f);
			_mod.SetPitchDegrees(5f);
			yield return new WaitForSecondsRealtime(0.3f);
			float[] best = new float[1];
			yield return Walk(0f, 3.5f, best);
			float num = stair.Position.Z + 2.75f * s;
			Log.Info($"STAIRS stair: top step {num:F2}, feet got to {best[0]:F2}, eye {_mod.EyeForTest.z:F2}, ceiling {room.Top:F2}");
			Check(Mathf.Abs(best[0] - num) < 0.05f, $"human_stone: up the stair to the top step ({best[0]:F2} of {num:F2})");
			Check(_mod.EyeForTest.z > room.Top + 0.3f, $"human_stone: head up in the stairwell ({_mod.EyeForTest.z:F2} over the ceiling at {room.Top:F2})");
			_mod.SetPitchDegrees(-40f);
			_mod.SetYaw(MathF.PI);
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SaveFrame("stairs_room_stair_top.png");
			_mod.SetPitchDegrees(0f);
			float[] best2 = new float[1];
			yield return Walk(MathF.PI, 3.5f, best2);
			Check(RoomFloors.Feet < 0.05f, $"human_stone: back down the stair (feet {RoomFloors.Feet:F2})");
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(35f);
			_mod.Interior.Eye = new Vector2(stair.Position.X, stair.Position.Y - 1.3f);
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("stairs_room_stair_from_below.png");
		}

		private IEnumerator Ladder(Interior room)
		{
			MeshInstance loft = Piece(room, "int_v_loft");
			if (loft == null)
			{
				Check(ok: false, "human_timber: no loft in the room");
				yield break;
			}
			float y = loft.Basis.C1.Y;
			float edge = At(loft, 0f, -0.95f).y;
			float deck = loft.Position.Z + 1.9f * y;
			_mod.Interior.Eye = At(loft, 0.85f, -1.79f - 0.35f / y * 0.57f);
			_mod.SetPitchDegrees(10f);
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SetYaw(MathF.PI / 2f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			StringBuilder trace = new StringBuilder();
			float nextTrace = 0f;
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				if (!(_mod.Interior.Eye.y < edge + 0.2f))
				{
					break;
				}
				yield return null;
				if (t >= nextTrace)
				{
					nextTrace += 0.25f;
					trace.Append($"[{_mod.Interior.Eye.y:F2} feet {RoomFloors.Feet:F2}] ");
				}
			}
			Log.Info($"STAIRS   ladder: {trace}{Colliders.BodyForTest}");
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return new WaitForSecondsRealtime(0.3f);
			Log.Info($"STAIRS loft: deck {deck:F2}, feet {RoomFloors.Feet:F2}, at {_mod.Interior.Eye}, edge {edge:F2}");
			Check(RoomFloors.Feet > deck - 0.05f && RoomFloors.Feet < deck + 0.25f, $"human_timber: up the ladder onto the loft ({RoomFloors.Feet:F2} of {deck:F2}, more on its chest or bed)");
			_mod.SetYaw(-MathF.PI / 2f);
			_mod.SetPitchDegrees(-35f);
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SaveFrame("stairs_room_loft.png");
			_mod.Interior.Eye = At(loft, 0.85f, -0.75f);
			_mod.SetPitchDegrees(-10f);
			float[] best = new float[1];
			yield return Walk(-MathF.PI / 2f, 3f, best);
			Check(RoomFloors.Feet < 0.05f, $"human_timber: back down the ladder (feet {RoomFloors.Feet:F2})");
		}

		private IEnumerator Spiral(Interior room)
		{
			MeshInstance sp = Piece(room, "int_v_spiral");
			if (sp == null)
			{
				Check(ok: false, "dwarf_stump: no spiral stair in the room");
				yield break;
			}
			float cx = sp.Position.X;
			float cy = sp.Position.Y;
			float s = sp.Basis.C1.Y;
			Vec3 vec = sp.Basis.Mul(new Vec3(0f, -1f, 0f));
			Vec3 vec2 = sp.Basis.Mul(new Vec3(Mathf.Cos(-1.2707963f), Mathf.Sin(-1.2707963f), 0f));
			float num = Mathf.Atan2(vec.Y, vec.X);
			float turn = Mathf.Sign(Mathf.DeltaAngle(num * 57.29578f, Mathf.Atan2(vec2.Y, vec2.X) * 57.29578f));
			float f = num - turn * 0.5f;
			_mod.Interior.Eye = new Vector2(cx + Mathf.Cos(f) * 0.3f, cy + Mathf.Sin(f) * 0.3f);
			_mod.SetPitchDegrees(10f);
			yield return new WaitForSecondsRealtime(0.3f);
			float best = 0f;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (float t = 0f; t < 9f; t += Time.unscaledDeltaTime)
			{
				Vector2 eye = _mod.Interior.Eye;
				float f2 = Mathf.Atan2(eye.y - cy, eye.x - cx) + turn * 0.35f;
				_mod.SetYaw(Mathf.Atan2(cy + Mathf.Sin(f2) * 0.3f - eye.y, cx + Mathf.Cos(f2) * 0.3f - eye.x));
				yield return null;
				best = Mathf.Max(best, RoomFloors.Feet);
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			float num2 = sp.Position.Z + 2.7f * s;
			Log.Info($"STAIRS spiral: top {num2:F2}, best feet {best:F2}, now {RoomFloors.Feet:F2}, eye {_mod.EyeForTest.z:F2}");
			Check(best > num2 - 0.08f, $"dwarf_stump: round the spiral stair to the top ({best:F2} of {num2:F2})");
			Check(RoomFloors.Feet > num2 - 0.08f, $"dwarf_stump: stopped at the top by its rail ({RoomFloors.Feet:F2})");
			_mod.SetPitchDegrees(-45f);
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SaveFrame("stairs_room_spiral_top.png");
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
			Log.Info("STAIRS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
