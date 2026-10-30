using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SnowTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private readonly List<Cloud> _clouds = new List<Cloud>();

		private readonly List<Vector2> _cloudAt = new List<Vector2>();

		private int _pass;

		private int _fail;

		private string _cloudKind = "cloud_snow";

		private Vector2 _town;

		private Vector2 _eye;

		private float _meanMs;

		private float _worstMs;

		private float _stepMs;

		private string _split;

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
			Log.Info((ok ? "SNOW PASS " : "SNOW FAIL ") + what);
		}

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
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 3, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("SNOW could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SNOW first person did not start");
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
			bool savedGpu = s.GpuRender;
			bool savedGod = s.GodView;
			s.TimeMode = 1;
			WorldAgeAsset savedAge = null;
			try
			{
				savedAge = World.world.era_manager.getCurrentAge();
				WorldAgeAsset worldAgeAsset = AssetManager.era_library.get("age_hope");
				if (worldAgeAsset != null && worldAgeAsset != savedAge)
				{
					World.world.era_manager.setCurrentAge(worldAgeAsset, pOverrideTime: true);
				}
			}
			catch (Exception ex)
			{
				Log.Error("SNOW age: " + ex.Message);
			}
			yield return null;
			yield return null;
			for (float t = 0f; t < 20f; t += 0.25f)
			{
				float num = 0f;
				try
				{
					num = World.world.era_manager.getNightMod();
				}
				catch
				{
				}
				if (num <= 0.01f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			_mod.Day.FinishAgeChange();
			_mod.ForceRelight();
			IEnumerator run = Run();
			while (true)
			{
				object current2;
				try
				{
					if (!run.MoveNext())
					{
						break;
					}
					current2 = run.Current;
					goto IL_03b9;
				}
				catch (Exception ex2)
				{
					Log.Error("SNOW failed: " + ex2);
				}
				break;
				IL_03b9:
				yield return current2;
			}
			SnowCover.Hidden = false;
			KillClouds();
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			_mod.SetPitchDegrees(0f);
			s.TimeMode = savedMode;
			s.GpuRender = savedGpu;
			s.GodView = savedGod;
			if (savedAge != null)
			{
				try
				{
					World.world.era_manager.setCurrentAge(savedAge, pOverrideTime: true);
				}
				catch
				{
				}
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Finish(quit);
		}

		private void Finish(bool quit)
		{
			Log.Info($"SNOW {_pass} passed, {_fail} failed");
			Log.Info("SNOW DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private void KillClouds()
		{
			foreach (Cloud cloud in _clouds)
			{
				try
				{
					if (cloud != null && ((BaseEffect)cloud).active)
					{
						cloud.kill();
					}
				}
				catch
				{
				}
			}
			_clouds.Clear();
			_cloudAt.Clear();
		}

		private void Pin()
		{
			for (int i = 0; i < _clouds.Count; i++)
			{
				Cloud cloud = _clouds[i];
				try
				{
					if (!(cloud == null) && ((BaseEffect)cloud).active)
					{
						goto IL_00a0;
					}
					cloud = (_clouds[i] = EffectsLibrary.spawn("fx_cloud", World.world.GetTile((int)_cloudAt[i].x, (int)_cloudAt[i].y), _cloudKind) as Cloud);
					if (cloud == null)
					{
						continue;
					}
					cloud.setLifespan(600f);
					goto IL_00a0;
					IL_00a0:
					Vector3 localPosition = cloud.transform.localPosition;
					float num = ((cloud.spriteShadow != null) ? cloud.spriteShadow.offset.y : 0f);
					cloud.transform.localPosition = new Vector3(_cloudAt[i].x, _cloudAt[i].y - num, localPosition.z);
				}
				catch (Exception ex)
				{
					Log.Error("SNOW cloud: " + ex.Message);
				}
			}
		}

		private void Look(Vector2 eye, Vector2 at, float pitch, float height = 0f)
		{
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = height;
			_mod.SetYaw(Mathf.Atan2(at.y - eye.y, at.x - eye.x));
			_mod.SetPitchDegrees(pitch);
		}

		private IEnumerator Hold(float seconds, Vector2 eye, Vector2 at, float pitch, float height = 0f)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Pin();
				Look(eye, at, pitch, height);
				yield return null;
			}
		}

		private IEnumerator Clock(float seconds, Vector2 eye, Vector2 at, float pitch)
		{
			float sum = 0f;
			float worst = 0f;
			float steps = 0f;
			float render = 0f;
			float land = 0f;
			float models = 0f;
			float post = 0f;
			float clouds = 0f;
			float shade = 0f;
			float falls = 0f;
			float drops = 0f;
			int n = 0;
			FrameRenderer r = _mod.Renderer;
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Pin();
				Look(eye, at, pitch);
				if (t > 0.6f)
				{
					sum += _mod.FrameMs;
					worst = Mathf.Max(worst, _mod.FrameMs);
					steps += _mod.TerrainForTest.Snow.StepMs;
					n++;
					render += _mod.RenderMs;
					land += r.TerrainMs;
					models += r.MeshMs;
					post += r.PostMs;
					clouds += r.CloudMs;
					shade += r.CloudShadowMs;
					falls += r.FallMs;
					try
					{
						drops += (float)World.world.drop_manager._drops.Count;
					}
					catch
					{
					}
				}
				yield return null;
			}
			float num = 1f / (float)Mathf.Max(1, n);
			_meanMs = sum * num;
			_worstMs = worst;
			_stepMs = steps * num;
			_split = $"drawing {render * num:F1} (land {land * num:F1}, models {models * num:F1}, finish {post * num:F1}: clouds {clouds * num:F1}, their shadows {shade * num:F1}, falling {falls * num:F2}), the rest {(sum - render) * num:F1}, {drops * num:F0} game drops";
		}

		private Vector2 Stand(Vector2 at, float back)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = float.MaxValue;
			Vector2 vector = at + new Vector2(back, 0f);
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector2 vector2 = at + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * back;
				if (vector2.x < 3f || vector2.y < 3f || vector2.x > (float)terrainForTest.Width - 4f || vector2.y > (float)terrainForTest.Height - 4f)
				{
					continue;
				}
				float num2 = terrainForTest.SampleHeight(vector2.x, vector2.y) + 1f;
				float num3 = terrainForTest.SampleHeight(at.x, at.y) + 1.5f;
				float num4 = 0f;
				int num5 = Mathf.CeilToInt(back);
				for (int j = 1; j < num5; j++)
				{
					float num6 = (float)j / (float)num5;
					Vector2 vector3 = Vector2.Lerp(vector2, at, num6);
					float num7 = num2 + (num3 - num2) * num6;
					float num8 = terrainForTest.SampleHeight(vector3.x, vector3.y);
					if (num8 > num7)
					{
						num4 += 3f + (num8 - num7);
					}
					WorldTile tile = World.world.GetTile((int)vector3.x, (int)vector3.y);
					if (tile != null && tile.building != null && AssetStyle.IsSolid(tile.building) && num8 + 3f > num7)
					{
						num4 += ((num6 < 0.3f) ? 2f : 1f);
					}
				}
				int num9 = (int)vector2.y * terrainForTest.Width + (int)vector2.x;
				if ((terrainForTest.Flags[num9] & 3) != 0)
				{
					num4 += 6f;
				}
				if (num4 < num)
				{
					num = num4;
					vector = vector2;
				}
			}
			Log.Info($"SNOW standing {back:F0} tiles from {at} at {vector} ({num:F0} in the way)");
			return vector;
		}

		private void Houses(Vector2 at)
		{
			Vector2[] obj = new Vector2[4]
			{
				new Vector2(-4f, 3f),
				new Vector2(3f, 4f),
				new Vector2(5f, -3f),
				new Vector2(-5f, -4f)
			};
			int num = 0;
			Vector2[] array = obj;
			for (int i = 0; i < array.Length; i++)
			{
				Vector2 vector = array[i];
				WorldTile tile = World.world.GetTile((int)(at.x + vector.x), (int)(at.y + vector.y));
				if (tile == null || tile.main_type == null || tile.main_type.liquid)
				{
					continue;
				}
				try
				{
					if (tile.building != null)
					{
						tile.building.startDestroyBuilding();
					}
					if (World.world.buildings.addBuilding((num % 2 == 0) ? "house_human_1" : "house_human_2", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New) != null)
					{
						num++;
					}
				}
				catch (Exception ex)
				{
					Log.Error("SNOW house: " + ex.Message);
				}
			}
			Log.Info($"SNOW {num} houses put up");
		}

		private float LyingSnow(Vector2 at, int r)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = 0f;
			for (int i = (int)at.y - r; i <= (int)at.y + r; i++)
			{
				for (int j = (int)at.x - r; j <= (int)at.x + r; j++)
				{
					if (j >= 0 && i >= 0 && j < terrainForTest.Width && i < terrainForTest.Height)
					{
						int num2 = i * terrainForTest.Width + j;
						if (terrainForTest.Materials[num2] != 5)
						{
							num += (float)(int)terrainForTest.Snow.Depth[num2];
						}
					}
				}
			}
			return num;
		}

		private int SnowyModels(out int all)
		{
			int num = 0;
			all = 0;
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (mesh != null)
				{
					all++;
					if (mesh.Snow > 0.2f)
					{
						num++;
					}
				}
			}
			return num;
		}

		private IEnumerator Run()
		{
			Settings s = _mod.Settings;
			SceneCollector scene = _mod.SceneForTest;
			TerrainCache cache = _mod.TerrainForTest;
			_town = _king.current_position;
			Houses(_town);
			yield return new WaitForSecondsRealtime(1.5f);
			_eye = Stand(_town, 10f);
			float before = LyingSnow(_town, 14);
			Log.Info($"SNOW before: snow lying anywhere {cache.Snow.On}, at the eye {cache.Snow.At(_eye.x, _eye.y):F2}, round the town {before:F0}");
			Dictionary<bool, float> clearMs = new Dictionary<bool, float>();
			Dictionary<bool, Vector2> clearTone = new Dictionary<bool, Vector2>();
			Dictionary<bool, Vector2> handsTone = new Dictionary<bool, Vector2>();
			bool[] array = new bool[2] { true, false };
			foreach (bool gpu in array)
			{
				s.GpuRender = gpu;
				yield return Clock(6f, _eye, _town, -6f);
				clearMs[gpu] = _meanMs;
				clearTone[gpu] = _mod.ShownToneForTest(0f, 0.35f);
				handsTone[gpu] = _mod.ShownToneForTest(0f, 0.18f);
				_mod.SaveFrame("snow_clear_" + (gpu ? "gpu" : "cpu") + ".png");
				Log.Info(string.Format("SNOW clear ({0}): frame {1:F1} ms (worst {2:F1}), ground {3}; {4}", gpu ? "gpu" : "cpu", _meanMs, _worstMs, clearTone[gpu], _split));
			}
			Check(Mathf.Abs(handsTone[true].x - handsTone[false].x) < 60f, $"models on the card aren't washed white (your hands as bright as the CPU's): gpu {handsTone[true].x:F0} vs cpu {handsTone[false].x:F0}");
			Vector2[] spots = new Vector2[3]
			{
				new Vector2(0f, 2f),
				new Vector2(-9f, -4f),
				new Vector2(9f, -3f)
			};
			Dictionary<bool, float> rainMs = new Dictionary<bool, float>();
			_cloudKind = "cloud_rain";
			Vector2[] array2 = spots;
			foreach (Vector2 vector in array2)
			{
				_cloudAt.Add(_town + vector);
				_clouds.Add(null);
			}
			array = new bool[2] { true, false };
			foreach (bool gpu in array)
			{
				s.GpuRender = gpu;
				yield return Hold(1.5f, _eye, _town, -6f);
				yield return Clock(6f, _eye, _town, -6f);
				rainMs[gpu] = _meanMs;
				Log.Info(string.Format("SNOW raining instead ({0}): frame {1:F1} ms (worst {2:F1}) vs {3:F1} clear; {4}", gpu ? "gpu" : "cpu", _meanMs, _worstMs, clearMs[gpu], _split));
			}
			KillClouds();
			yield return Hold(4f, _eye, _town, -6f);
			_cloudKind = "cloud_snow";
			array2 = spots;
			foreach (Vector2 vector2 in array2)
			{
				_cloudAt.Add(_town + vector2);
				_clouds.Add(null);
			}
			s.GpuRender = true;
			yield return Hold(3f, _eye, _town, -6f);
			bool ok = false;
			bool flag = false;
			foreach (StormCloud storm in scene.Weather.Storms)
			{
				if (storm.Drops == Precip.Snow)
				{
					ok = true;
					flag |= storm.Color == Weather.SnowCloud;
				}
			}
			Check(ok, "the snow clouds are in the sky");
			Check(flag, "drawn in their own grey-white, not the map's mint");
			Check(_mod.Snowfall > 0.3f, $"under them the air fills with snow ({_mod.Snowfall:F2})");
			_mod.SaveFrame("snow_under_gpu.png");
			yield return Hold(0.5f, _eye, _town, 30f);
			_mod.SaveFrame("snow_under_up_gpu.png");
			List<string> depths = new List<string>();
			for (int i = 1; i <= 6; i++)
			{
				yield return Hold(5f, _eye, _town, -6f);
				depths.Add($"{cache.Snow.At(_eye.x, _eye.y):F2}");
				if (i == 2 || i == 6)
				{
					_mod.SaveFrame($"snow_build_{i * 5}_gpu.png");
				}
			}
			float num = LyingSnow(_town, 14);
			float num2 = cache.Snow.At(_eye.x, _eye.y);
			int all;
			int num3 = SnowyModels(out all);
			Log.Info(string.Format("SNOW building up at the eye: {0}; round the town {1:F0} -> {2:F0}; {3} of {4} models snowed on", string.Join(" ", depths), before, num, num3, all));
			Check(num2 > 0.35f, $"the snow builds up where it falls ({num2:F2} deep at the eye)");
			Check(num > before + 2000f, "and all round the town");
			Check(num3 >= 5, $"it lies on the trees and roofs ({num3} models)");
			Vector2 vector3 = _mod.ShownToneForTest(0f, 0.35f);
			Check(vector3.y < clearTone[true].y * 0.75f, $"the ground has gone white on the card (colour {clearTone[true].y:F0} -> {vector3.y:F0}, light {clearTone[true].x:F0} -> {vector3.x:F0})");
			Dictionary<bool, float> snowMs = new Dictionary<bool, float>();
			array = new bool[2] { true, false };
			foreach (bool gpu in array)
			{
				s.GpuRender = gpu;
				yield return Clock(6f, _eye, _town, -6f);
				snowMs[gpu] = _meanMs;
				string text = (gpu ? "gpu" : "cpu");
				_mod.SaveFrame("snow_under_" + text + "_late.png");
				Log.Info($"SNOW snowing ({text}): frame {_meanMs:F1} ms (worst {_worstMs:F1}) vs {clearMs[gpu]:F1} clear; flakes {_mod.Renderer.FallMs:F2} ms, clouds {_mod.Renderer.CloudMs:F2} ms, the snow lying moved on in {_stepMs:F3} ms a frame; {_split}");
				if (!gpu)
				{
					Vector2 vector4 = _mod.ShownToneForTest(0f, 0.35f);
					Check(vector4.y < clearTone[false].y * 0.75f, $"and on the CPU (colour {clearTone[false].y:F0} -> {vector4.y:F0})");
				}
			}
			array = new bool[2] { true, false };
			foreach (bool gpu in array)
			{
				s.GpuRender = gpu;
				SnowCover.Hidden = true;
				yield return Clock(6f, _eye, _town, -6f);
				SnowCover.Hidden = false;
				Log.Info(string.Format("SNOW snowing, the snow lying hidden ({0}): frame {1:F1} ms vs {2:F1} with it; {3}", gpu ? "gpu" : "cpu", _meanMs, snowMs[gpu], _split));
			}
			Check(snowMs[true] < Mathf.Max(rainMs[true], clearMs[true]) * 1.15f + 1f, $"snow costs the card no more than rain does (clear {clearMs[true]:F1}, rain {rainMs[true]:F1}, snow {snowMs[true]:F1} ms)");
			Check(snowMs[false] < Mathf.Max(rainMs[false], clearMs[false]) * 1.15f + 2f, $"nor the CPU, with the snow lying (clear {clearMs[false]:F1}, rain {rainMs[false]:F1}, snow {snowMs[false]:F1} ms)");
			Vector2 far = Stand(_town, 45f);
			array = new bool[2] { true, false };
			foreach (bool gpu in array)
			{
				s.GpuRender = gpu;
				yield return Hold(2.5f, far, _town, 6f);
				_mod.SaveFrame("snow_outside_" + (gpu ? "gpu" : "cpu") + ".png");
			}
			Check(RainFx.SnowVeils >= 1, $"seen from afar, veils of snow hang under them ({RainFx.SnowVeils})");
			s.GpuRender = true;
			s.GodView = true;
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				Pin();
				try
				{
					Camera camera = World.world.camera;
					Vector2 vector5 = _town + new Vector2(0f, -8f);
					camera.transform.position = new Vector3(vector5.x, vector5.y, camera.transform.position.z);
					camera.orthographicSize = 30f;
					if (World.world.move_camera != null)
					{
						World.world.move_camera.setTargetZoom(30f);
					}
					World.world.setZoomOrthographic(30f);
				}
				catch (Exception ex)
				{
					Log.Error("SNOW camera: " + ex.Message);
					break;
				}
				yield return null;
			}
			_mod.SaveFrame("snow_god_gpu.png");
			s.GodView = false;
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			KillClouds();
			yield return Hold(2f, _eye, _town, -6f);
			Dictionary<int, byte> then = new Dictionary<int, byte>();
			for (int k = (int)_town.y - 14; k <= (int)_town.y + 14; k++)
			{
				for (int l = (int)_town.x - 14; l <= (int)_town.x + 14; l++)
				{
					if (l >= 0 && k >= 0 && l < cache.Width && k < cache.Height)
					{
						int num4 = k * cache.Width + l;
						if (cache.Materials[num4] != 5 && cache.Snow.Depth[num4] > 0)
						{
							then[num4] = cache.Snow.Depth[num4];
						}
					}
				}
			}
			yield return Hold(15f, _eye, _town, -6f);
			float num5 = 0f;
			float num6 = 0f;
			foreach (KeyValuePair<int, byte> item in then)
			{
				if (cache.Materials[item.Key] != 5)
				{
					num5 += (float)(int)item.Value;
					num6 += (float)(int)cache.Snow.Depth[item.Key];
				}
			}
			Log.Info($"SNOW melting: {num5:F0} -> {num6:F0} on {then.Count} tiles round the town; snowfall {_mod.Snowfall:F2}");
			Check(num6 < num5, "once the clouds have gone it melts");
			Check(_mod.Snowfall < 0.15f, $"and the air clears ({_mod.Snowfall:F2})");
			_mod.SaveFrame("snow_melting_gpu.png");
		}
	}
}
