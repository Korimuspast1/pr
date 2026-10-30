using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RoomPerfTest : MonoBehaviour
	{
		private const float Stretch = 8f;

		private WorldBoxMod _mod;

		private Actor _king;

		private Actor _galleon;

		private Vector2 _home;

		private Vector2 _galleonAt;

		private bool _pin;

		private readonly List<string> _summary = new List<string>();

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			bool cpuOnly = options.Contains("cpuonly");
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
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if (OpenWater(new Vector2(item.x, item.y), 70).HasValue)
				{
					_king = KingTest.FoundKingdom(item, "human", 10, 2);
					if (_king != null)
					{
						Log.Info($"ROOMFPS village at {item.x},{item.y}");
						break;
					}
				}
			}
			if (_king == null)
			{
				Log.Error("ROOMFPS could not found a village by the sea");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(3f);
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(3f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("ROOMFPS first person did not start");
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
			_home = _king.current_position;
			StartCoroutine(Pin());
			Building house = Build("house_human_2", Dry(_home + new Vector2(4f, 4f), 2, 40));
			WorldTile mineSpot = Dry(_home + new Vector2(-8f, 6f), 5, 60);
			ClearAround(mineSpot);
			Building mine = Build("mine", mineSpot);
			Log.Info(string.Format("ROOMFPS house {0}, mine {1}, water for the galleon {2}", (house != null) ? house.asset.id : "none", mine != null, OpenWater(_home, 70)));
			Settings s = _mod.Settings;
			bool savedGpu = s.GpuRender;
			int savedRate = Application.targetFrameRate;
			int savedSync = QualitySettings.vSyncCount;
			if (!options.Contains("capped"))
			{
				Application.targetFrameRate = 1000;
				QualitySettings.vSyncCount = 0;
			}
			Log.Info($"ROOMFPS {_mod.Renderer.Width}x{_mod.Renderer.Height}, fov {s.FieldOfView}, draw distance {s.DrawDistance}, drawn beside the game {s.AsyncRender && WorldBoxMod.BesideAllowed}, " + $"{SystemInfo.processorType.Trim()} ({SystemInfo.processorCount} threads), {SystemInfo.graphicsDeviceName}");
			bool[] array = (cpuOnly ? new bool[1] : new bool[2] { true, false });
			bool[] array2 = array;
			foreach (bool gpu in array2)
			{
				s.GpuRender = gpu;
				string r = (gpu ? "gpu" : "cpu");
				Put(_king, _home);
				yield return Measure("outdoors " + r, "roomfps_outdoors_" + r, () => !_mod.Interior.Inside && !_mod.Mine.Inside && !_mod.Cabin.Inside);
				if (house != null && house.isAlive())
				{
					_mod.EnterHouseForTest(house);
					yield return Measure("house " + r, "roomfps_house_" + r, () => _mod.Interior.Inside, gpu);
					_mod.LeaveRoomForTest();
					yield return new WaitForSecondsRealtime(0.5f);
				}
				if (mine != null && mine.isAlive())
				{
					Put(_king, new Vector2((float)mineSpot.x + 0.5f, (float)mineSpot.y - 4.5f));
					_mod.SetYaw(MathF.PI / 2f);
					_mod.SetPitchDegrees(2f);
					yield return new WaitForSecondsRealtime(1f);
					_mod.EnterMine(mine);
					yield return new WaitForSecondsRealtime(0.3f);
					if (_mod.Mine.Inside)
					{
						_mod.Mine.Eye += new Vector2(0f, 1.5f);
						yield return Measure("mine " + r, "roomfps_mine_" + r, () => _mod.Mine.Inside, gpu);
						_mod.SetYaw(-MathF.PI / 2f);
						yield return new WaitForSecondsRealtime(0.5f);
						_mod.SaveFrame("roomfps_mine_mouth_" + r + ".png");
						Log.Info($"ROOMFPS mine {r}: looking back at the mouth, {_mod.Renderer.PortalPixels} pixels of the world through it");
						_mod.LeaveMineForTest();
					}
					else
					{
						Log.Error("ROOMFPS could not get into the mine (enterable " + MineInterior.IsEnterable(mine) + ")");
					}
					yield return new WaitForSecondsRealtime(0.5f);
				}
				Put(_king, _home);
				yield return new WaitForSecondsRealtime(0.5f);
				Vector2? vector = OpenWater(_home, 70);
				if (vector.HasValue)
				{
					_galleonAt = vector.Value;
					try
					{
						_galleon = World.world.units.createNewUnit("boat_transport_human", World.world.GetTile((int)_galleonAt.x, (int)_galleonAt.y));
						if (_galleon != null && _king.kingdom != null)
						{
							_galleon.setKingdom(_king.kingdom);
						}
					}
					catch (Exception ex)
					{
						Log.Error("ROOMFPS galleon: " + ex.Message);
					}
					_pin = true;
					yield return new WaitForSecondsRealtime(0.5f);
					if (_galleon != null && _galleon.isAlive())
					{
						_mod.EnterCabin(_galleon);
						yield return new WaitForSecondsRealtime(0.3f);
						if (_mod.Cabin.Inside)
						{
							yield return Measure("cabin " + r, "roomfps_cabin_" + r, () => _mod.Cabin.Inside, gpu);
							if (gpu)
							{
								_mod.SetPitchDegrees(-5f);
								for (int k = 0; k < 4; k++)
								{
									_mod.SetYaw(_mod.ViewYaw + MathF.PI / 2f);
									yield return new WaitForSecondsRealtime(0.4f);
									_mod.SaveFrame($"roomfps_cabin_window{k}.png");
								}
								_mod.SetPitchDegrees(80f);
								yield return new WaitForSecondsRealtime(0.4f);
								_mod.SaveFrame("roomfps_cabin_up.png");
								Log.Info($"ROOMFPS cabin look up: pitch {Mathf.Atan(_mod.View.Pitch) * 57.29578f:F0} deg, free {_mod.View.Free}, on the card {_mod.ShownFrame != _mod.Frame}");
								_mod.SetPitchDegrees(0f);
							}
							_mod.LeaveCabinForTest();
						}
						else
						{
							Log.Error("ROOMFPS could not get aboard");
						}
					}
					_pin = false;
					try
					{
						if (_galleon != null && _galleon.isAlive())
						{
							_galleon.dieSimpleNone();
						}
					}
					catch
					{
					}
					_galleon = null;
					yield return new WaitForSecondsRealtime(0.5f);
				}
				else
				{
					Log.Error("ROOMFPS no open water for the galleon");
				}
			}
			if (!cpuOnly && house != null && house.isAlive())
			{
				int i = s.TimeMode;
				s.TimeMode = 1;
				Put(_king, _home);
				yield return new WaitForSecondsRealtime(0.5f);
				_mod.EnterHouseForTest(house);
				yield return new WaitForSecondsRealtime(1f);
				if (_mod.Interior.Inside)
				{
					float t = _mod.ViewYaw;
					_mod.SetPitchDegrees(-10f);
					for (int k2 = 0; k2 < 4; k2++)
					{
						array2 = new bool[2] { false, true };
						foreach (bool gpu in array2)
						{
							s.GpuRender = gpu;
							_mod.SetYaw(t + (float)k2 * MathF.PI * 0.5f);
							yield return new WaitForSecondsRealtime(0.6f);
							_mod.SaveFrame(string.Format("roomfps_look{0}_{1}.png", k2, gpu ? "gpu" : "cpu"));
						}
					}
					Log.Info($"ROOMFPS look: {_mod.Renderer.Windows.Count} windows, {_mod.Renderer.Lights.Count} lights, night {_mod.Day.Air.Night:F2}");
					s.GpuRender = true;
					float[] array3 = new float[2] { 85f, -85f };
					foreach (float pitch in array3)
					{
						_mod.SetPitchDegrees(pitch);
						yield return new WaitForSecondsRealtime(0.6f);
						_mod.SaveFrame("roomfps_look_" + ((pitch > 0f) ? "up" : "down") + ".png");
						Log.Info(string.Format("ROOMFPS look {0}: pitch {1:F0} deg, free {2}, on the card {3}", (pitch > 0f) ? "up" : "down", Mathf.Atan(_mod.View.Pitch) * 57.29578f, _mod.View.Free, _mod.ShownFrame != _mod.Frame));
					}
					_mod.SetPitchDegrees(0f);
					s.TimeMode = 3;
					int k = s.GpuLook;
					_mod.SetYaw(t);
					string[] array4 = new string[3] { "cpu", "gpu", "classic" };
					foreach (string r in array4)
					{
						s.GpuRender = r != "cpu";
						s.GpuLook = ((!(r == "classic")) ? 1 : 0);
						yield return new WaitForSecondsRealtime(0.8f);
						_mod.SaveFrame("roomfps_night_" + r + ".png");
					}
					s.GpuLook = k;
					_mod.LeaveRoomForTest();
				}
				else
				{
					Log.Error("ROOMFPS look: could not get into the house");
				}
				s.TimeMode = i;
				array2 = new bool[2] { true, false };
				foreach (bool gpu in array2)
				{
					s.GpuRender = gpu;
					yield return new WaitForSecondsRealtime(1f);
					_mod.EnterHouseForTest(house);
					List<string> times = new List<string>();
					float t = 0f;
					for (int k2 = 0; k2 < 12; k2++)
					{
						yield return null;
						float b = Time.unscaledDeltaTime * 1000f;
						t = Mathf.Max(t, b);
						times.Add(b.ToString("F0"));
					}
					Log.Info(string.Format("ROOMFPS entry {0}: first frames {1} ms (worst {2:F0}), inside {3}", gpu ? "gpu" : "cpu", string.Join(" ", times), t, _mod.Interior.Inside));
					_mod.LeaveRoomForTest();
				}
			}
			s.GpuRender = savedGpu;
			Application.targetFrameRate = savedRate;
			QualitySettings.vSyncCount = savedSync;
			foreach (string item2 in _summary)
			{
				Log.Info("ROOMFPS summary " + item2);
			}
			Finish(quit);
		}

		private IEnumerator Measure(string name, string frame, Func<bool> where, bool card = false)
		{
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.ResetPerf();
			double ms = 0.0;
			double mod = 0.0;
			double terrain = 0.0;
			double sprites = 0.0;
			double models = 0.0;
			double prep = 0.0;
			double shadowMap = 0.0;
			double raster = 0.0;
			double lights = 0.0;
			double overlay = 0.0;
			double finish = 0.0;
			double post = 0.0;
			double tris = 0.0;
			float worst = 0f;
			int frames = 0;
			int gpuFrames = 0;
			float start = Time.unscaledTime;
			float yaw = float.NaN;
			float baseYaw = _mod.ViewYaw;
			bool saved = false;
			while (Time.unscaledTime - start < 8f)
			{
				float num = baseYaw + Mathf.Floor((Time.unscaledTime - start) / 2f) * MathF.PI * 0.5f;
				if (num != yaw)
				{
					yaw = num;
					_mod.SetYaw(yaw);
				}
				yield return null;
				if (!_mod.IsFirstPerson || _mod.Host != _king || !where())
				{
					Log.Error($"ROOMFPS {name}: cut short after {frames} frames (first person {_mod.IsFirstPerson}, still the king {_mod.Host == _king}, still there {where()}): " + KingState());
					yield break;
				}
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				ms += (double)unscaledDeltaTime * 1000.0;
				worst = Mathf.Max(worst, unscaledDeltaTime * 1000f);
				mod += (double)_mod.FrameMs;
				FrameRenderer renderer = _mod.Renderer;
				terrain += (double)renderer.TerrainMs;
				sprites += (double)renderer.ObjectsMs;
				models += (double)renderer.MeshMs;
				prep += (double)renderer.MeshPrepMs;
				shadowMap += (double)renderer.ShadowMs;
				raster += (double)renderer.MeshRasterMs;
				lights += (double)renderer.LightsMs;
				overlay += (double)renderer.OverlayMs;
				finish += (double)renderer.FinishMs;
				post += (double)(renderer.PostMs - renderer.MeshMs);
				tris += (double)renderer.MeshTriangles;
				if (_mod.ShownFrame != _mod.Frame)
				{
					gpuFrames++;
				}
				frames++;
				if (!saved && Time.unscaledTime - start > 0.5f)
				{
					_mod.SaveFrame(frame + ".png");
					saved = true;
				}
			}
			double num2 = 1.0 / (double)Math.Max(1, frames);
			string text = $"{name}: {1000.0 / (ms * num2):F1} fps, frame {ms * num2:F1} ms (worst {worst:F0}), mod {mod * num2:F1} ms on the game's thread, " + $"{gpuFrames} of {frames} frames on the graphics card, render {_mod.RenderMs:F1} ms";
			Log.Info("ROOMFPS " + text + "; " + KingState());
			if (card && (float)gpuFrames < (float)frames * 0.9f)
			{
				Log.Error($"ROOMFPS {name}: only {gpuFrames} of {frames} frames drawn on the graphics card");
			}
			Log.Info($"ROOMFPS {name} drawing: terrain {terrain * num2:F1}, sprites {sprites * num2:F1}, models {models * num2:F1} (prep {prep * num2:F1}, shadow map {shadowMap * num2:F1}, raster {raster * num2:F1}, " + $"{tris * num2:F0} triangles, {_mod.Renderer.Meshes.Count} models), light and finish {post * num2:F1} (lights {lights * num2:F1}: {_mod.Renderer.Lights.Count}, hands {overlay * num2:F1}, palette {finish * num2:F1}), " + $"{_mod.Renderer.Windows.Count} windows");
			_summary.Add(text);
		}

		private string KingState()
		{
			try
			{
				return $"king alive {_king.isAlive()}, health {_king.getHealth()}, nutrition {_king.getNutrition()}, at {_king.current_position}";
			}
			catch (Exception ex)
			{
				return "king: " + ex.Message;
			}
		}

		private IEnumerator Pin()
		{
			while (true)
			{
				if (_pin && _galleon != null)
				{
					try
					{
						if (_galleon.isAlive())
						{
							_galleon.cancelAllBeh();
							_galleon.current_position = _galleonAt;
							_galleon.dirty_current_tile = true;
						}
					}
					catch
					{
					}
				}
				yield return null;
			}
		}

		private static Building Build(string id, WorldTile at)
		{
			if (at == null)
			{
				return null;
			}
			try
			{
				Building building = World.world.buildings.addBuilding(id, at, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("ROOMFPS could not build " + id + ": " + ex.Message);
				return null;
			}
		}

		private static WorldTile Dry(Vector2 c, int r, int reach)
		{
			for (int i = 0; i < reach; i += 2)
			{
				for (int j = 0; j < 32; j++)
				{
					float f = (float)j / 32f * MathF.PI * 2f;
					int num = (int)(c.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(c.y + Mathf.Sin(f) * (float)i);
					bool flag = true;
					for (int k = -r; k <= r + 1 && flag; k++)
					{
						for (int l = -r; l <= r && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num + l, num2 + k);
							if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava || (tile.building != null && tile.building.isKingdomCiv()))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return World.world.GetTile(num, num2);
					}
					if (i == 0)
					{
						break;
					}
				}
			}
			return null;
		}

		private static void ClearAround(WorldTile spot)
		{
			if (spot == null)
			{
				return;
			}
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int num = item.current_tile.x - spot.x;
				int num2 = item.current_tile.y - spot.y;
				if (num < -5 || num > 5 || num2 < -7 || num2 > 6)
				{
					continue;
				}
				try
				{
					if (!item.isKingdomCiv())
					{
						item.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
		}

		private static Vector2? OpenWater(Vector2 c, int reach)
		{
			for (int i = 4; i < reach; i += 2)
			{
				for (int j = 0; j < 48; j++)
				{
					float f = (float)j / 48f * MathF.PI * 2f;
					int num = (int)(c.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(c.y + Mathf.Sin(f) * (float)i);
					if (num < 16 || num2 < 16 || num >= MapBox.width - 16 || num2 >= MapBox.height - 16)
					{
						continue;
					}
					bool flag = true;
					for (int k = -2; k <= 2 && flag; k++)
					{
						for (int l = -2; l <= 2 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num + l, num2 + k);
							if (tile == null || tile.Type == null || !tile.Type.liquid || tile.Type.lava)
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return new Vector2((float)num + 0.5f, (float)num2 + 0.5f);
					}
				}
			}
			return null;
		}

		private static void Put(Actor a, Vector2 at)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile != null)
			{
				a.spawnOn(tile, 0f);
			}
			a.current_position = at;
			a.dirty_current_tile = true;
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
			Log.Info("ROOMFPS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
