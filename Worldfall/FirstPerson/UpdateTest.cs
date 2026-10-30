using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class UpdateTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

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
			Log.Info(string.Format("UPDATE world {0}x{1}, units {2}, models {3}", MapBox.width, MapBox.height, World.world.units.Count, Models.Ready ? "ready" : "missing"));
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 10, 2);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("UPDATE could not found a village");
				Finish(quit);
				yield break;
			}
			BuildHouses(_king.city);
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("UPDATE first person did not start");
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
			yield return new WaitForSecondsRealtime(1f);
			bool rooms = options.Contains("roomsonly");
			bool beasts = options.Contains("beastonly");
			if (options.Contains("doorsonly"))
			{
				yield return Run("doors", Doors());
				s.TimeMode = savedMode;
				Finish(quit);
				yield break;
			}
			if (!rooms && !beasts)
			{
				yield return Run("jump", Jump());
				yield return Run("hills", Hills());
			}
			if (!beasts)
			{
				yield return Run("rooms", Rooms());
			}
			if (!beasts)
			{
				yield return Run("indoors", Indoors());
			}
			if (!rooms && !beasts)
			{
				yield return Run("doors", Doors());
			}
			if (!rooms)
			{
				yield return Run("beasts", Beasts());
			}
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("UPDATE --- " + name);
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
					Log.Error("UPDATE " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			PossessionHooks.ScriptedJump = false;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private void BuildHouses(City city)
		{
			WorldTile tile = city.getTile();
			string[] array = new string[3] { "house_human_0", "house_human_2", "house_human_4" };
			for (int i = 0; i < array.Length; i++)
			{
				float f = 0.8f + (float)i * 2.1f;
				WorldTile tile2 = World.world.GetTile((int)((float)tile.x + Mathf.Cos(f) * 7f), (int)((float)tile.y + Mathf.Sin(f) * 7f));
				if (tile2 == null || tile2.Type == null || tile2.Type.liquid)
				{
					continue;
				}
				try
				{
					Building building = World.world.buildings.addBuilding(array[i], tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						building.completeConstruction();
						building.setKingdom(city.kingdom);
						city.listBuilding(building);
					}
				}
				catch (Exception ex)
				{
					Log.Error("UPDATE building " + array[i] + ": " + ex.Message);
				}
			}
		}

		private WorldTile OpenLand(Actor me, int radius)
		{
			WorldTile current_tile = me.current_tile;
			for (int i = 4; i < radius; i += 2)
			{
				for (int j = 0; j < 16; j++)
				{
					WorldTile tile = World.world.GetTile(current_tile.x + (int)(Mathf.Cos((float)j * 0.39f) * (float)i), current_tile.y + (int)(Mathf.Sin((float)j * 0.39f) * (float)i));
					if (tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block && (tile.building == null || !AssetStyle.IsSolid(tile.building)))
					{
						bool flag = true;
						for (int k = 1; k <= 8 && flag; k++)
						{
							WorldTile tile2 = World.world.GetTile(tile.x + k, tile.y);
							flag = tile2 != null && tile2.Type != null && !tile2.Type.liquid && !tile2.Type.block && (tile2.building == null || !AssetStyle.IsSolid(tile2.building));
						}
						if (flag)
						{
							return tile;
						}
					}
				}
			}
			return null;
		}

		private IEnumerator Jump()
		{
			Actor me = _king;
			WorldTile worldTile = OpenLand(me, 40);
			if (worldTile != null)
			{
				try
				{
					me.setCurrentTilePosition(worldTile);
				}
				catch (Exception ex)
				{
					Log.Error("UPDATE move: " + ex.Message);
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(0f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			PossessionHooks.ScriptedRun = true;
			yield return new WaitForSecondsRealtime(1.2f);
			float pace = PossessionHooks.Pace;
			Vector2 before = me.current_position;
			PossessionHooks.ScriptedJump = true;
			float peak = 0f;
			float t0 = Time.realtimeSinceStartup;
			bool saved = false;
			while (Time.realtimeSinceStartup - t0 < 1.4f)
			{
				yield return null;
				peak = Mathf.Max(peak, me.position_height);
				if (!saved && me.position_height > 0.3f)
				{
					_mod.SaveFrame("upd_jump_air.png");
					saved = true;
				}
			}
			float magnitude = (me.current_position - before).magnitude;
			Log.Info($"UPDATE jump at a sprint (pace {pace:F2}, running {PossessionHooks.Running}): peak height {peak:F2}, covered {magnitude:F1} tiles in 1.4 s, still sprinting {PossessionHooks.Pace:F2}");
			if (peak < 0.1f)
			{
				Log.Error("UPDATE no jump at a sprint");
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
		}

		private IEnumerator Hills()
		{
			Vector2 current_position = _king.current_position;
			WorldTile best = null;
			float num = float.MaxValue;
			for (int i = 8; i < MapBox.height - 8; i += 2)
			{
				for (int j = 12; j < MapBox.width - 8; j += 2)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.Type == null || tile.Type.id == null || !tile.Type.id.Contains("hills"))
					{
						continue;
					}
					WorldTile tile2 = World.world.GetTile(j - 9, i);
					if (tile2 != null && tile2.Type != null && !tile2.Type.liquid && !tile2.Type.id.Contains("hills") && !tile2.Type.block)
					{
						float sqrMagnitude = (new Vector2(j, i) - current_position).sqrMagnitude;
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							best = tile;
						}
					}
				}
			}
			if (best == null)
			{
				Log.Info("UPDATE no hills on this map");
				yield break;
			}
			TerrainCache terrainForTest = _mod.TerrainForTest;
			StringBuilder stringBuilder = new StringBuilder();
			for (int k = -10; k <= 14; k++)
			{
				stringBuilder.Append(terrainForTest.SampleHeight((float)(best.x + k) + 0.5f, (float)best.y + 0.5f).ToString("F1")).Append(' ');
			}
			Log.Info($"UPDATE hills at {best.x},{best.y}: ground west to east {stringBuilder}");
			_mod.DebugEye = new Vector2((float)best.x - 9.5f, (float)best.y + 0.5f);
			_mod.DebugEyeSet = true;
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(2f);
			yield return new WaitForSecondsRealtime(2f);
			_mod.SaveFrame("upd_hills.png");
			_mod.DebugEye = new Vector2((float)best.x - 3.5f, (float)best.y + 0.5f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("upd_hills_near.png");
			_mod.DebugEyeSet = false;
		}

		private IEnumerator Rooms()
		{
			List<Actor> folk = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != _king && simple.isAlive() && People.IsHumanoid(simple) && folk.Count < 2)
				{
					folk.Add(simple);
				}
			}
			string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string race in array)
			{
				int[] array2 = new int[2] { 0, 4 };
				foreach (int tier in array2)
				{
					_mod.EnterRoomForTest(folk, race, tier);
					Interior room = _mod.Interior.Room;
					Log.Info($"UPDATE room {race} {tier}: {room.Meshes.Count} models, {room.Solids.Count} solids, {room.Spots.Count} spots, {room.Lights.Count} lights, {room.Windows.Count} windows, top {room.Top:F2}");
					yield return new WaitForSecondsRealtime(1f);
					_mod.SetPitchDegrees(-6f);
					yield return new WaitForSecondsRealtime(0.4f);
					_mod.SaveFrame($"upd_room_{race}{tier}.png");
					_mod.SetYaw(2.4707963f);
					yield return new WaitForSecondsRealtime(0.4f);
					_mod.SaveFrame($"upd_room_{race}{tier}_left.png");
					_mod.SetYaw(0.6707964f);
					_mod.SetPitchDegrees(18f);
					yield return new WaitForSecondsRealtime(0.4f);
					_mod.SaveFrame($"upd_room_{race}{tier}_up.png");
					_mod.SetYaw(MathF.PI / 2f);
					_mod.SetPitchDegrees(-6f);
					PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
					yield return new WaitForSecondsRealtime(1.2f);
					PossessionHooks.ScriptedInput = Vector2.zero;
					Log.Info($"UPDATE walked in to {_mod.Interior.Eye.x:F2},{_mod.Interior.Eye.y:F2} (room {room.MinX:F1}..{room.MaxX:F1} x {room.MinY:F1}..{room.MaxY:F1}), render {_mod.RenderMs:F1} ms");
					_mod.LeaveRoomForTest();
					yield return new WaitForSecondsRealtime(0.4f);
				}
			}
			Building building = null;
			foreach (Building building2 in World.world.buildings)
			{
				if (building2 != null && HouseInterior.IsEnterable(building2) && building2.asset.id.StartsWith("house_", StringComparison.Ordinal))
				{
					building = building2;
					if (building2.asset.id.EndsWith("4", StringComparison.Ordinal))
					{
						break;
					}
				}
			}
			if (building == null)
			{
				Log.Info("UPDATE no house to walk into");
				yield break;
			}
			_mod.Settings.TimeMode = 3;
			_mod.ForceRelight();
			_mod.EnterHouseForTest(building);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SetPitchDegrees(-5f);
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("upd_room_real_night.png");
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "upd_screen_room.png"));
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.LeaveRoomForTest();
			_mod.Settings.TimeMode = 1;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(1f);
		}

		private IEnumerator Indoors()
		{
			List<Actor> list = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != _king && simple.isAlive() && People.IsHumanoid(simple) && list.Count < 3)
				{
					list.Add(simple);
				}
			}
			_mod.EnterRoomForTest(list, "human", 4);
			_mod.SetPitchDegrees(-4f);
			Dictionary<Actor, Vector2> start = new Dictionary<Actor, Vector2>();
			foreach (RoomWalkers.Walker item in _mod.Interior.WalkersForTest)
			{
				start[item.Who] = item.Pos;
			}
			float topSpeed = 0f;
			float t0 = Time.realtimeSinceStartup;
			int shot = 0;
			Dictionary<Actor, float> lo = new Dictionary<Actor, float>();
			Dictionary<Actor, float> hi = new Dictionary<Actor, float>();
			while (Time.realtimeSinceStartup - t0 < 16f)
			{
				yield return null;
				foreach (RoomWalkers.Walker item2 in _mod.Interior.WalkersForTest)
				{
					topSpeed = Mathf.Max(topSpeed, item2.Speed);
					SpriteImage spriteImage = _mod.SpriteForTest(item2.Who);
					float raw = ((spriteImage != null) ? (spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(item2.Who.current_scale.y)) : 0f);
					float num = Stature.Peek(item2.Who, raw);
					lo[item2.Who] = (lo.TryGetValue(item2.Who, out var value) ? Mathf.Min(value, num) : num);
					hi[item2.Who] = (hi.TryGetValue(item2.Who, out var value2) ? Mathf.Max(value2, num) : num);
					if (item2.Speed > 0.4f && shot < 3 && Time.realtimeSinceStartup - t0 > 2f + (float)shot * 4f)
					{
						_mod.SetYaw(Mathf.Atan2(item2.Pos.y - _mod.Interior.Eye.y, item2.Pos.x - _mod.Interior.Eye.x));
						shot++;
						_mod.SaveFrame($"upd_indoors_walk{shot}.png");
					}
				}
			}
			foreach (RoomWalkers.Walker item3 in _mod.Interior.WalkersForTest)
			{
				Vector2 value3;
				float num2 = (start.TryGetValue(item3.Who, out value3) ? (item3.Pos - value3).magnitude : 0f);
				Log.Info($"UPDATE indoors {WorldBoxMod.Describe(item3.Who)}: at spot {item3.Spot}, {num2:F2} tiles from where it started, drawn {lo[item3.Who]:F2}..{hi[item3.Who]:F2} tall");
			}
			Log.Info($"UPDATE indoors: top pace {topSpeed:F2} tiles/s, {_mod.Interior.WalkersForTest.Count} people");
			_mod.LeaveRoomForTest();
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Doors()
		{
			Vector2 c = ((_king.city != null) ? new Vector2((float)_king.city.getTile().x + 0.5f, (float)_king.city.getTile().y + 0.5f) : _king.current_position);
			int ways0 = Steering.DoorWays;
			int exits0 = Steering.DoorExits;
			int ghosted0 = Steering.Ghosted;
			Steering.Verbose = true;
			int drawnInside = 0;
			int checks = 0;
			float t0 = Time.realtimeSinceStartup;
			int shot = 0;
			while (Time.realtimeSinceStartup - t0 < 30f)
			{
				yield return null;
				_mod.SetYaw((Time.realtimeSinceStartup - t0) * 0.4f);
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple == null || simple == _mod.Host || !simple.isAlive() || simple.is_inside_building || !People.IsHumanoid(simple) || (simple.current_position - _mod.Host.current_position).sqrMagnitude > 400f)
					{
						continue;
					}
					checks++;
					if (Steering.HiddenLastFrame(simple))
					{
						continue;
					}
					string text = Steering.InsideWhatForTest(simple.current_position);
					if (text != null)
					{
						drawnInside++;
						if (drawnInside <= 8)
						{
							Log.Info($"UPDATE drawn inside {text}: {WorldBoxMod.Describe(simple)} at {simple.current_position.x:F1},{simple.current_position.y:F1}, moving {simple.is_moving}");
						}
					}
				}
				if (Steering.DoorWays > ways0 && shot == 0)
				{
					shot++;
					_mod.SaveFrame("upd_door_way.png");
				}
			}
			Steering.Verbose = false;
			Log.Info("UPDATE doors: " + Steering.DoorReportForTest());
			Log.Info($"UPDATE doors: {Steering.DoorWays - ways0} frames of walkers going round to a door, {Steering.DoorExits - exits0} stepped out of a door, " + $"{Steering.Ghosted - ghosted0} let through after getting stuck, drawn inside a wall {drawnInside} of {checks} checks");
			Building house = null;
			Vector2 door = default(Vector2);
			foreach (Building building in World.world.buildings)
			{
				if (building != null && HouseInterior.IsEnterable(building) && Steering.DoorForTest(building, out door) && building.current_tile != null && (new Vector2(building.current_tile.x, building.current_tile.y) - _mod.Host.current_position).sqrMagnitude < 196f)
				{
					house = building;
					break;
				}
			}
			Actor walker = null;
			foreach (Actor simple2 in World.world.units.getSimpleList())
			{
				if (simple2 != null && simple2 != _mod.Host && simple2.isAlive() && !simple2.is_inside_building && People.IsHumanoid(simple2) && simple2.isAdult() && (simple2.current_position - _mod.Host.current_position).sqrMagnitude < 144f)
				{
					walker = simple2;
					break;
				}
			}
			if (house == null || walker == null)
			{
				Log.Info("UPDATE doors: no house or villager to try the door with");
				yield break;
			}
			WorldTile tile = World.world.GetTile(house.current_tile.x, house.current_tile.y + 4);
			if (tile != null)
			{
				try
				{
					walker.setCurrentTilePosition(tile);
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.DebugEye = door + new Vector2(2.5f, -3f);
			_mod.DebugEyeSet = true;
			_mod.SetYaw(Mathf.Atan2((float)house.current_tile.y + 1f - _mod.DebugEye.y, (float)house.current_tile.x - _mod.DebugEye.x));
			_mod.SetPitchDegrees(-6f);
			try
			{
				walker.beh_building_target = house;
				walker.goTo(house.current_tile);
			}
			catch (Exception ex)
			{
				Log.Error("UPDATE goTo: " + ex.Message);
			}
			Vector2 last = walker.current_position;
			float t1 = Time.realtimeSinceStartup;
			bool went = false;
			int frames = 0;
			while (Time.realtimeSinceStartup - t1 < 12f && !walker.is_inside_building)
			{
				yield return null;
				if (Steering.HiddenLastFrame(walker) && !went)
				{
					went = true;
					Log.Info($"UPDATE door: {WorldBoxMod.Describe(walker)} went out of sight {(last - door).magnitude:F2} tiles from the door (from behind the house)");
				}
				if (!went)
				{
					last = walker.current_position;
				}
				int num = frames + 1;
				frames = num;
				if (num % 40 == 0)
				{
					_mod.SaveFrame($"upd_door_in{frames / 40}.png");
				}
			}
			Log.Info($"UPDATE door: inside the house now {walker.is_inside_building}, went out of sight {went}");
			_mod.DebugEyeSet = false;
			_ = c;
		}

		private IEnumerator Beasts()
		{
			Actor king = _king;
			WorldTile land = OpenLand(king, 40) ?? king.current_tile;
			string[] array = new string[4] { "cow", "sheep", "chicken", "wolf" };
			foreach (string id in array)
			{
				Actor beast = null;
				try
				{
					beast = World.world.units.spawnNewUnit(id, World.world.GetTile(land.x + 2, land.y) ?? land, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
				}
				catch (Exception ex)
				{
					Log.Error("UPDATE spawn " + id + ": " + ex.Message);
				}
				if (beast == null)
				{
					continue;
				}
				yield return new WaitForSecondsRealtime(0.5f);
				if (_mod.Host == null || !_mod.LeapInto(beast))
				{
					Possess.JumpTo(beast);
				}
				yield return new WaitForSecondsRealtime(1.8f);
				if (_mod.Host != beast)
				{
					Log.Error("UPDATE could not become the " + id);
					continue;
				}
				_mod.SetYaw(0f);
				_mod.SetPitchDegrees(0f);
				yield return new WaitForSecondsRealtime(0.8f);
				Vector3 eyeForTest = _mod.EyeForTest;
				Vector2 current_position = beast.current_position;
				float num = _mod.TerrainForTest.SampleHeight(current_position.x, current_position.y);
				Log.Info($"UPDATE {id}: beast view {BeastView.Active}, body drawn {BeastView.ShowsBody(beast)}, arms drawn {_mod.Renderer.OverlayMeshes.Count}, " + $"eye {(new Vector2(eyeForTest.x, eyeForTest.y) - current_position).magnitude:F2} tiles ahead of the body at {eyeForTest.z - num:F2} up, body facing {BeastView.BodyYaw * 57.29578f:F0} deg");
				if (_mod.Renderer.OverlayMeshes.Count > 0)
				{
					Log.Error("UPDATE arms drawn for a " + id);
				}
				LogEffectsNear(beast.current_position, 2.5f);
				SceneCollector sceneForTest = _mod.SceneForTest;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				foreach (Billboard billboard in sceneForTest.Billboards)
				{
					if (billboard.Tag == 2147483646)
					{
						num2++;
					}
				}
				foreach (VoxelObject voxel in sceneForTest.Voxels)
				{
					if (voxel.Tag == 2147483646)
					{
						num3++;
					}
				}
				foreach (MeshInstance mesh in sceneForTest.Meshes)
				{
					if (mesh.Tag == 2147483646)
					{
						num4++;
					}
				}
				Log.Info($"UPDATE {id}: your body in the scene as {num4} models, {num3} voxel shapes, {num2} flat sprites");
				_mod.SaveFrame("upd_beast_" + id + ".png");
				_mod.SetPitchDegrees(-45f);
				yield return new WaitForSecondsRealtime(0.6f);
				_mod.SaveFrame("upd_beast_" + id + "_down.png");
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(0.9f);
				_mod.SaveFrame("upd_beast_" + id + "_walk_down.png");
				_mod.SetPitchDegrees(-5f);
				PossessionHooks.ScriptedRun = true;
				yield return new WaitForSecondsRealtime(0.9f);
				_mod.SaveFrame("upd_beast_" + id + "_run.png");
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				_mod.SetYaw(MathF.PI / 2f);
				yield return new WaitForSecondsRealtime(0.15f);
				float lag = Mathf.DeltaAngle(BeastView.BodyYaw * 57.29578f, 90f);
				yield return new WaitForSecondsRealtime(1.5f);
				Log.Info($"UPDATE {id}: turned 90 deg, body {lag:F0} deg behind after 0.15 s, {Mathf.DeltaAngle(BeastView.BodyYaw * 57.29578f, 90f):F0} after 1.6 s");
				Vector2 debugEye = beast.current_position + new Vector2(-1.4f, -1.4f);
				_mod.DebugEye = debugEye;
				_mod.DebugEyeSet = true;
				_mod.SetYaw(MathF.PI / 4f);
				_mod.SetPitchDegrees(-12f);
				yield return new WaitForSecondsRealtime(0.6f);
				_mod.SaveFrame("upd_beast_" + id + "_outside.png");
				_mod.DebugEyeSet = false;
				_mod.SetPitchDegrees(0f);
				yield return new WaitForSecondsRealtime(0.4f);
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "upd_screen_" + id + ".png"));
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (_king != null && _king.isAlive() && _mod.LeapInto(_king))
			{
				yield return new WaitForSecondsRealtime(2f);
				Log.Info($"UPDATE back in the king: beast view {BeastView.Active}, arms drawn {_mod.Renderer.OverlayMeshes.Count}");
			}
		}

		private static void LogEffectsNear(Vector2 p, float radius)
		{
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
						if (!(item2 == null) && item2.active && !(item2.sprite_renderer == null))
						{
							Vector3 position = item2.transform.position;
							float magnitude = (new Vector2(position.x, position.y) - p).magnitude;
							if (!(magnitude > radius))
							{
								SpriteRenderer sprite_renderer = item2.sprite_renderer;
								Log.Info(string.Format("UPDATE effect near you: {0} sprite {1} at {2:F2} tiles, alpha {3:F2}, enabled {4}, scale {5:F2}", item.asset?.id, (sprite_renderer.sprite != null) ? sprite_renderer.sprite.name : "none", magnitude, sprite_renderer.color.a, sprite_renderer.enabled, item2.transform.lossyScale.x));
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("UPDATE effects: " + ex.Message);
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
			Log.Info("UPDATE DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
