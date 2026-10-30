using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Gpu;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FarSightTest : MonoBehaviour
	{
		private struct View
		{
			public string Name;

			public Vector2 Eye;

			public float Height;

			public float Yaw;

			public float Pitch;
		}

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private readonly List<View> _views = new List<View>();

		private readonly Dictionary<string, float> _perfFrame = new Dictionary<string, float>();

		private readonly Dictionary<string, float> _perfMod = new Dictionary<string, float>();

		private readonly List<(Building B, MeshInstance Mesh, float Dist)> _farSeen = new List<(Building, MeshInstance, float)>();

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			bool stay = options.Contains("stay");
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
			Settings s = _mod.Settings;
			s.TimeMode = 1;
			s.GpuRender = true;
			s.GpuLook = 1;
			s.PixelDistance = 1;
			s.Resolution = 2;
			s.FarModels = 0;
			_mod.ForceRelight();
			int slot = SaveSlot(options);
			if (slot > 0)
			{
				string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
				bool flag2 = false;
				try
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
					flag2 = true;
				}
				catch (Exception ex)
				{
					Log.Error("FS could not load " + text + ": " + ex.Message);
				}
				if (flag2)
				{
					for (float t = 0f; t < 90f; t += 0.5f)
					{
						yield return new WaitForSecondsRealtime(0.5f);
						bool flag3 = true;
						try
						{
							flag3 = SmoothLoader.isLoading();
						}
						catch
						{
						}
						if (!flag3 && t > 3f)
						{
							break;
						}
					}
					Config.autosaves = false;
					yield return new WaitForSecondsRealtime(2f);
					Log.Info($"FS loaded save {slot}: {MapBox.width}x{MapBox.height}");
				}
			}
			Vector2 town;
			Actor host = AnyGrownUp(out town);
			if (host == null)
			{
				foreach (WorldTile item in KingTest.GoodSites())
				{
					host = KingTest.FoundKingdom(item, "human", 8, 2);
					if (host != null)
					{
						town = new Vector2((float)item.x + 0.5f, (float)item.y + 0.5f);
						break;
					}
				}
			}
			if (host == null)
			{
				Log.Error("FS no one to look through");
				Done(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(3f);
			ControllableUnit.setControllableCreature(host);
			yield return new WaitForSecondsRealtime(4f);
			for (int tries = 0; tries < 8; tries++)
			{
				if (!_mod.IsFirstPerson)
				{
					break;
				}
				if (!_mod.IndoorsForDrops)
				{
					break;
				}
				Actor actor = OutdoorsOf(host);
				if (actor == null)
				{
					break;
				}
				host = actor;
				ControllableUnit.setControllableCreature(host);
				yield return new WaitForSecondsRealtime(3f);
			}
			if (!_mod.IsFirstPerson)
			{
				Log.Error("FS first person did not start");
				Done(quit);
				yield break;
			}
			if (_mod.IndoorsForDrops)
			{
				Log.Error("FS first person began indoors: the far land is not seen from a room");
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			PlanViews(town);
			Config.paused = true;
			for (int tries = 0; tries < 10; tries++)
			{
				yield return null;
			}
			s.DrawDistance = 1200f;
			s.DetailDistance = 240f;
			s.CreatureDistance = 1200f;
			yield return Shots("whole");
			s.DrawDistance = 1200f;
			s.DetailDistance = 80f;
			s.CreatureDistance = 200f;
			yield return Shots("whole80");
			s.DrawDistance = 270f;
			s.DetailDistance = 80f;
			s.CreatureDistance = 200f;
			yield return Shots("default");
			MeshModel meshModel = Models.DetailedCreature("boat_galleon", 200f, null);
			Check("a ship far off keeps its own model", meshModel != null && !Models.IsDistant(meshModel), meshModel?.Name);
			s.DrawDistance = 1200f;
			s.DetailDistance = 80f;
			yield return Perf("check");
			yield return FarModelChecks();
			if (stay)
			{
				string cmdFile = Path.Combine(Log.Folder, "farsight.cmd");
				Log.Info("FS staying: commands in " + cmdFile);
				while (true)
				{
					yield return new WaitForSecondsRealtime(0.5f);
					if (!File.Exists(cmdFile))
					{
						continue;
					}
					string[] array;
					try
					{
						array = File.ReadAllLines(cmdFile);
						File.Delete(cmdFile);
					}
					catch
					{
						continue;
					}
					bool done = false;
					string[] array2 = array;
					for (int tries = 0; tries < array2.Length; tries++)
					{
						string text2 = array2[tries].Trim();
						if (text2.Length == 0)
						{
							continue;
						}
						Log.Info("FS command: " + text2);
						string[] w = text2.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
						if (w[0] == "quit")
						{
							done = true;
							break;
						}
						if (w[0] == "perf")
						{
							yield return Perf((w.Length > 1) ? w[1] : "x");
							Log.Info("FS perf done");
						}
						if (w[0] == "looks" && w.Length > 1)
						{
							_mod.SceneForTest.FarLooksOffForTest = w[1] == "0";
							Log.Info("FS far looks " + ((w[1] == "0") ? "off" : "on"));
						}
						if (w[0] == "shots")
						{
							for (int i = 0; i < 3; i++)
							{
								yield return null;
							}
							yield return Shots((w.Length > 1) ? w[1] : "x");
							Log.Info("FS shots done");
						}
						if (w[0] == "set" && w.Length > 2)
						{
							Set(s, w[1], w[2]);
						}
						if (w[0] == "season" && w.Length > 1)
						{
							yield return SetSeason(w[1]);
						}
						if (w[0] == "models" && w.Length > 1)
						{
							s.FarModels = ((w[1] == "1") ? 1 : 0);
							Log.Info("FS far models " + s.FarModels);
						}
					}
					if (done)
					{
						break;
					}
				}
			}
			Config.paused = false;
			Done(quit);
		}

		private void Set(Settings s, string key, string value)
		{
			float num = ((value == "whole") ? 1200f : float.Parse(value, CultureInfo.InvariantCulture));
			switch (key)
			{
			case "draw":
				s.DrawDistance = num;
				break;
			case "detail":
				s.DetailDistance = num;
				break;
			case "creatures":
				s.CreatureDistance = num;
				break;
			case "pixel":
				s.PixelDistance = (int)num;
				break;
			case "look":
				s.GpuLook = (int)num;
				break;
			case "res":
				s.Resolution = (int)num;
				break;
			case "time":
				s.TimeMode = (int)num;
				_mod.ForceRelight();
				break;
			case "gpu":
				s.GpuRender = num > 0.5f;
				break;
			}
			Log.Info("FS set " + key + " = " + value);
		}

		private IEnumerator SetSeason(string name)
		{
			Season want;
			try
			{
				want = (Season)Enum.Parse(typeof(Season), name, ignoreCase: true);
			}
			catch
			{
				Log.Error("FS no season " + name);
				yield break;
			}
			int num = -1;
			for (int i = 0; i < 8; i++)
			{
				if (num >= 0)
				{
					break;
				}
				if (FirstPerson.Towns.Calendar.SeasonOf(i, out var dayIn, out var _) == want && dayIn == 0)
				{
					num = i;
				}
			}
			if (num < 0)
			{
				Log.Info("FS no " + want.ToString() + " in this age");
				yield break;
			}
			FirstPerson.Towns.Calendar.SetDayForTest(num + 8);
			yield return new WaitForSecondsRealtime(6f);
			_mod.SamplerForTest.RelightAll();
			for (int j = 0; j < 10; j++)
			{
				yield return null;
			}
			Log.Info($"FS season {want}: spring {SeasonLook.Spring:F2} summer {SeasonLook.Summer:F2} autumn {SeasonLook.Autumn:F2} winter {SeasonLook.Winter:F2}");
		}

		private static int SaveSlot(string options)
		{
			int num = options.IndexOf("map");
			if (num < 0)
			{
				return 0;
			}
			int num2 = 0;
			int num3 = num + 3;
			while (num3 < options.Length && char.IsDigit(options[num3]))
			{
				num2 = num2 * 10 + (options[num3++] - 48);
			}
			return num2;
		}

		private static Actor OutdoorsOf(Actor was)
		{
			List<Actor> list = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != was && simple.isAlive() && simple.city == was.city && !simple.is_inside_building && !simple.is_inside_boat && HouseInterior.HouseAt(simple) == null)
				{
					list.Add(simple);
				}
			}
			if (list.Count <= 0)
			{
				return null;
			}
			return list[UnityEngine.Random.Range(0, list.Count)];
		}

		private static Actor AnyGrownUp(out Vector2 town)
		{
			town = Vector2.zero;
			Actor result = null;
			int num = -1;
			foreach (City item in World.world.cities.list)
			{
				if (item == null || !item.isAlive())
				{
					continue;
				}
				WorldTile worldTile = null;
				try
				{
					worldTile = item.getTile();
				}
				catch
				{
				}
				if (worldTile == null)
				{
					continue;
				}
				Actor actor = null;
				int num2 = 0;
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.city == item)
					{
						num2++;
						if (actor == null && !simple.is_inside_building && !simple.is_inside_boat && HouseInterior.HouseAt(simple) == null)
						{
							actor = simple;
						}
					}
				}
				if (actor != null && num2 > num)
				{
					num = num2;
					result = actor;
					town = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
				}
			}
			return result;
		}

		private void PlanViews(Vector2 town)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			int width = terrainForTest.Width;
			int height = terrainForTest.Height;
			_views.Clear();
			Vector2 eye = Vector2.zero;
			Vector2 vector = Vector2.right;
			int num = -1;
			(int, int)[] array = new(int, int)[4]
			{
				(10, 10),
				(width - 11, 10),
				(10, height - 11),
				(width - 11, height - 11)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(int, int) tuple = array[i];
				int item = tuple.Item1;
				int item2 = tuple.Item2;
				Vector2 vector2 = new Vector2(item, item2);
				Vector2 normalized = (new Vector2((float)width * 0.5f, (float)height * 0.5f) - vector2).normalized;
				int num2 = 0;
				for (float num3 = 20f; num3 < Mathf.Sqrt(width * width + height * height) - 30f; num3 += 6f)
				{
					Vector2 vector3 = vector2 + normalized * num3;
					if (vector3.x < 0f || vector3.y < 0f || vector3.x >= (float)width || vector3.y >= (float)height)
					{
						break;
					}
					if ((terrainForTest.Flags[(int)vector3.y * width + (int)vector3.x] & 1) == 0)
					{
						num2++;
					}
				}
				if (num2 > num)
				{
					num = num2;
					eye = vector2;
					vector = normalized;
				}
			}
			_views.Add(new View
			{
				Name = "corner",
				Eye = eye,
				Height = 5f,
				Yaw = Mathf.Atan2(vector.y, vector.x),
				Pitch = -2f
			});
			Vector2 eye2 = new Vector2((float)width * 0.5f, (float)height * 0.5f);
			float num4 = -99f;
			for (int j = 8; j < height - 8; j += 4)
			{
				for (int k = 8; k < width - 8; k += 4)
				{
					float num5 = terrainForTest.SampleHeight((float)k + 0.5f, (float)j + 0.5f) - 0.02f * (Mathf.Abs((float)k - (float)width * 0.5f) + Mathf.Abs((float)j - (float)height * 0.5f));
					if (num5 > num4)
					{
						num4 = num5;
						eye2 = new Vector2((float)k + 0.5f, (float)j + 0.5f);
					}
				}
			}
			_views.Add(LandView("peak", eye2, 3f, 60f, Mathf.Max(width, height), -2f));
			Vector2 normalized2 = (new Vector2((!(town.x < (float)width * 0.5f)) ? 1 : (width - 1), (!(town.y < (float)height * 0.5f)) ? 1 : (height - 1)) - town).normalized;
			_views.Add(new View
			{
				Name = "street",
				Eye = town - normalized2 * 3f,
				Height = 0f,
				Yaw = Mathf.Atan2(normalized2.y, normalized2.x),
				Pitch = 1f
			});
			foreach (View view in _views)
			{
				Log.Info($"FS view {view.Name}: eye {view.Eye.x:F0},{view.Eye.y:F0} +{view.Height:F1} yaw {view.Yaw * 57.29578f:F0} pitch {view.Pitch:F1}, map {width}x{height}");
			}
		}

		private View LandView(string name, Vector2 eye, float height, float near, float far, float pitch)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = -1f;
			float yaw = 0f;
			for (int i = 0; i < 32; i++)
			{
				float num2 = (float)i * MathF.PI / 16f;
				Vector2 vector = new Vector2(Mathf.Cos(num2), Mathf.Sin(num2));
				float num3 = 0f;
				for (float num4 = near; num4 <= far; num4 += 6f)
				{
					Vector2 vector2 = eye + vector * num4;
					if (vector2.x < 0f || vector2.y < 0f || vector2.x >= (float)terrainForTest.Width || vector2.y >= (float)terrainForTest.Height)
					{
						break;
					}
					if ((terrainForTest.Flags[(int)vector2.y * terrainForTest.Width + (int)vector2.x] & 1) == 0)
					{
						num3 += 1f;
					}
				}
				if (num3 > num)
				{
					num = num3;
					yaw = num2;
				}
			}
			return new View
			{
				Name = name,
				Eye = eye,
				Height = height,
				Yaw = yaw,
				Pitch = pitch
			};
		}

		private IEnumerator Shots(string tag)
		{
			Settings s = _mod.Settings;
			foreach (View v in _views)
			{
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = v.Height;
				_mod.SetYaw(v.Yaw);
				_mod.SetPitchDegrees(v.Pitch);
				for (int i = 0; i < 30; i++)
				{
					yield return null;
				}
				float t0 = Time.realtimeSinceStartup;
				for (int i = 0; i < 20; i++)
				{
					yield return null;
				}
				float num = (Time.realtimeSinceStartup - t0) * 1000f / 20f;
				_mod.SaveFrame("fs_" + v.Name + "_" + tag + ".png");
				GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
				FrameRenderer renderer = _mod.Renderer;
				TerrainCache terrainForTest = _mod.TerrainForTest;
				float num2 = Mathf.Max(v.Eye.x, (float)terrainForTest.Width - v.Eye.x);
				float num3 = Mathf.Max(v.Eye.y, (float)terrainForTest.Height - v.Eye.y);
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				if (tag.StartsWith("whole"))
				{
					Check("whole map (" + v.Name + "): no land in the last melt into the haze", renderer.HazeFadeStart >= num4 - 2f, $"fade from {renderer.HazeFadeStart:F0}, farthest corner {num4:F0}");
					Check("whole map (" + v.Name + "): the air thinner than at the default reach", renderer.HazeScale < 0.9f, renderer.HazeScale.ToString("F2"));
				}
				if (tag == "default")
				{
					Check("default reach (" + v.Name + "): the air as it always was", renderer.HazeScale == 1f && Mathf.Abs(renderer.HazeFadeStart - renderer.FogDistance * 0.72f) < 0.5f, $"x{renderer.HazeScale:F2}, fade {renderer.HazeFadeStart:F0}");
				}
				if (tag == "whole80" && gpuRendererForTest != null)
				{
					Check("far trees and buildings in their own looks (" + v.Name + ")", gpuRendererForTest.FarPropsDrawn > 0 && gpuRendererForTest.FarPlainDrawn == 0, $"{gpuRendererForTest.FarPropsDrawn} drawn, {gpuRendererForTest.FarPlainDrawn} as blocks");
				}
				Log.Info($"FS {v.Name} {tag}: draw {s.DrawDistance:F0} detail {s.DetailDistance:F0} pixel {s.PixelDistance} look {s.GpuLook}, " + string.Format("fog {0:F0}, fade {1:F0}, haze x{2:F2}, far ring {3} in {4:F1} ms (worst frame {5:F1}), {6:F1} ms a frame, mod {7:F1} ms, {8}", _mod.Renderer.FogDistance, _mod.Renderer.HazeFadeStart, _mod.Renderer.HazeScale, _mod.SceneForTest.FarScanCount, _mod.SceneForTest.FarScanMs, _mod.SceneForTest.FarScanWorstMs, num, _mod.FrameMs, (gpuRendererForTest != null) ? gpuRendererForTest.Stats : "no gpu"));
			}
		}

		private IEnumerator Perf(string tag)
		{
			foreach (View v in _views)
			{
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = v.Height;
				_mod.SetYaw(v.Yaw);
				_mod.SetPitchDegrees(v.Pitch);
				yield return Settled();
				List<float> frame = new List<float>();
				List<float> mod = new List<float>();
				for (int i = 0; i < 120; i++)
				{
					float t0 = Time.realtimeSinceStartup;
					yield return null;
					frame.Add((Time.realtimeSinceStartup - t0) * 1000f);
					mod.Add(_mod.FrameMs);
				}
				frame.Sort();
				mod.Sort();
				_perfFrame[tag + " " + v.Name] = frame[60];
				_perfMod[tag + " " + v.Name] = mod[60];
				if (tag == "check")
				{
					Check("the far ring gathered a share a frame (" + v.Name + ")", _mod.SceneForTest.FarScanWorstMs < 5f, $"{_mod.SceneForTest.FarScanCount} in it, worst frame {_mod.SceneForTest.FarScanWorstMs:F1} ms");
				}
				Log.Info(string.Format("FS perf {0} {1}: frame median {2:F1} ms, 90% {3:F1}, worst {4:F1}; mod median {5:F1} ms; far ring {6}, worst frame {7:F1} ms; {8} far models, {9} far sprites; {10}", v.Name, tag, frame[60], frame[108], frame[119], mod[60], _mod.SceneForTest.FarScanCount, _mod.SceneForTest.FarScanWorstMs, _mod.SceneForTest.FarModelsDrawn, _mod.SceneForTest.FarProps.Count, (_mod.GpuRendererForTest != null) ? _mod.GpuRendererForTest.Stats : "no gpu"));
			}
		}

		private void Check(string what, bool ok, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info((ok ? "PASS " : "FAIL ") + "FS " + what + ((detail != null) ? (" (" + detail + ")") : ""));
		}

		private void Done(bool quit)
		{
			if (_mod != null)
			{
				_mod.DebugEyeSet = false;
			}
			Log.Info($"FS {_pass} passed, {_fail} failed");
			Log.Info("FS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator FarModelChecks()
		{
			Settings s = _mod.Settings;
			SceneCollector scene = _mod.SceneForTest;
			int res = s.Resolution;
			s.Resolution = 3;
			s.DrawDistance = 1200f;
			s.DetailDistance = 80f;
			s.CreatureDistance = 1200f;
			s.FarModels = 0;
			yield return Shots("sprites80");
			s.FarModels = 1;
			yield return ModelShots("models80");
			s.DetailDistance = 240f;
			yield return ModelShots("models240");
			s.Resolution = res;
			s.DetailDistance = 80f;
			yield return HandOff(_views[1]);
			yield return HandOff(_views[2]);
			s.FarModels = 0;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Check("far models off: their sprites at once", scene.FarModelsDrawn == 0 && scene.FarProps.Count > 0, $"{scene.FarModelsDrawn} models, {scene.FarProps.Count} sprites");
			s.FarModels = 1;
			yield return Settled();
			Check("far models on again: models within a ring", scene.FarModelsDrawn > 0, $"{scene.FarModelsDrawn} models, {scene.FarProps.Count} sprites");
			s.DetailDistance = 80f;
			s.CreatureDistance = 200f;
			for (int i = 1; i <= 2; i++)
			{
				s.FarModels = 0;
				yield return Perf("sprites" + i);
				s.FarModels = 1;
				yield return Perf("models" + i);
			}
			foreach (View view in _views)
			{
				float num = Best(_perfFrame, "sprites", view);
				float num2 = Best(_perfFrame, "models", view);
				float num3 = Best(_perfMod, "sprites", view);
				float num4 = Best(_perfMod, "models", view);
				if (!(num <= 0f) && !(num2 <= 0f))
				{
					Check("far models cost little (" + view.Name + ")", num2 <= Mathf.Max(num * 1.25f, num + 3f), $"frame median {num:F1} -> {num2:F1} ms, mod {num3:F1} -> {num4:F1} ms (the better of two)");
				}
			}
		}

		private static float Best(Dictionary<string, float> times, string setup, View v)
		{
			float value;
			bool flag = times.TryGetValue(setup + "1 " + v.Name, out value);
			float value2;
			bool flag2 = times.TryGetValue(setup + "2 " + v.Name, out value2);
			if (!(flag && flag2))
			{
				if (!flag)
				{
					if (!flag2)
					{
						return 0f;
					}
					return value2;
				}
				return value;
			}
			return Mathf.Min(value, value2);
		}

		private IEnumerator Settled()
		{
			SceneCollector scene = _mod.SceneForTest;
			for (int i = 0; i < 40; i++)
			{
				yield return null;
			}
			for (int i = 0; i < 900; i++)
			{
				if (scene.FarRingSettledForTest)
				{
					break;
				}
				yield return null;
			}
			for (int i = 0; i < 5; i++)
			{
				yield return null;
			}
		}

		private IEnumerator ModelShots(string tag)
		{
			Settings s = _mod.Settings;
			SceneCollector scene = _mod.SceneForTest;
			foreach (View v in _views)
			{
				Look(v);
				yield return Settled();
				float t0 = Time.realtimeSinceStartup;
				for (int i = 0; i < 20; i++)
				{
					yield return null;
				}
				float num = (Time.realtimeSinceStartup - t0) * 1000f / 20f;
				_mod.SaveFrame("fs_" + v.Name + "_" + tag + ".png");
				GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
				int farModelsDrawn = scene.FarModelsDrawn;
				int count = scene.FarProps.Count;
				if (farModelsDrawn + count == 0)
				{
					Log.Info("FS " + v.Name + " " + tag + ": no tree or building past the detail distance in view: far models not checked here");
				}
				else
				{
					Check("far trees and buildings as models (" + v.Name + ", " + tag + ")", farModelsDrawn > 0 && count * 10 < farModelsDrawn + 10, $"{farModelsDrawn} models, {count} sprites, {scene.FarModelsKept} kept");
					Check("far models drawn as far as the land (" + v.Name + ", " + tag + ")", _mod.Renderer.FarModelReach >= _mod.Renderer.FogDistance - 0.5f, $"reach {_mod.Renderer.FarModelReach:F0}, land {_mod.Renderer.FogDistance:F0}, objects {_mod.Renderer.MaxDistance:F0}");
				}
				Check("the far ring with models gathered a share a frame (" + v.Name + ", " + tag + ")", scene.FarScanWorstMs < 5f, $"{scene.FarScanCount} in it, {scene.FarScanMs:F0} ms in all, worst frame {scene.FarScanWorstMs:F1} ms");
				Log.Info($"FS {v.Name} {tag}: draw {s.DrawDistance:F0} detail {s.DetailDistance:F0}, {farModelsDrawn} far models, {count} far sprites, {scene.Meshes.Count} models in all, " + string.Format("{0:F1} ms a frame, mod {1:F1} ms, {2}; buildings still flat sprites nearer: {3}", num, _mod.FrameMs, (gpuRendererForTest != null) ? gpuRendererForTest.Stats : "no gpu", FlatBuildings(scene)));
			}
		}

		private IEnumerator HandOff(View v)
		{
			Settings s = _mod.Settings;
			SceneCollector scene = _mod.SceneForTest;
			Look(v);
			yield return Settled();
			scene.FarModelsForTest(_farSeen);
			List<(Building B, string Model, Vec3 Pos, Mat3 Basis, uint[] Pal)> picked = new List<(Building, string, Vec3, Mat3, uint[])>();
			foreach (var item in _farSeen)
			{
				if (item.Dist > 82f && item.Dist < 110f && item.B != null && picked.Count < 600)
				{
					picked.Add((item.B, item.Mesh.Model.Name, item.Mesh.Position, item.Mesh.Basis, (item.Mesh.Palette != null) ? ((uint[])item.Mesh.Palette.Clone()) : null));
				}
			}
			s.DetailDistance = 120f;
			for (int i = 0; i < 60; i++)
			{
				yield return null;
			}
			int num = 0;
			int num2 = 0;
			string text = null;
			foreach (var item2 in picked)
			{
				MeshInstance meshInstance = scene.NearMeshForTest(item2.B);
				if (meshInstance != null && meshInstance.Model != null)
				{
					num++;
					if (meshInstance.Model.Name == item2.Model && Close(meshInstance.Position, item2.Pos) && Close(meshInstance.Basis.C0, item2.Basis.C0) && Close(meshInstance.Basis.C1, item2.Basis.C1) && Close(meshInstance.Basis.C2, item2.Basis.C2) && SamePalette(meshInstance.Palette, item2.Pal))
					{
						num2++;
					}
					else if (text == null)
					{
						text = $"{item2.B.asset?.id}: {item2.Model} -> {meshInstance.Model.Name}, at {item2.Pos.X:F3},{item2.Pos.Y:F3},{item2.Pos.Z:F3} -> {meshInstance.Position.X:F3},{meshInstance.Position.Y:F3},{meshInstance.Position.Z:F3}, " + string.Format("size {0:F3} -> {1:F3}, colours {2}", item2.Basis.C2.Z, meshInstance.Basis.C2.Z, SamePalette(meshInstance.Palette, item2.Pal) ? "same" : "differ");
					}
				}
			}
			Check("far models are the near ones across the detail distance (" + v.Name + ")", num >= 10 && (float)num2 >= (float)num * 0.97f, string.Format("{0} of {1} the same, of {2} far ones 82-110 tiles off{3}", num2, num, picked.Count, (text != null) ? ("; first differing " + text) : ""));
			s.DetailDistance = 80f;
		}

		private static string FlatBuildings(SceneCollector scene)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (Billboard billboard in scene.Billboards)
			{
				if (billboard.Tag >= 0 && billboard.Tag < scene.Tagged.Count && scene.Tagged[billboard.Tag] is Building building && building.asset != null)
				{
					dictionary.TryGetValue(building.asset.id, out var value);
					dictionary[building.asset.id] = value + 1;
				}
			}
			if (dictionary.Count == 0)
			{
				return "none";
			}
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, int> item in dictionary)
			{
				list.Add(item.Key + " " + item.Value);
			}
			return string.Join(", ", list);
		}

		private void Look(View v)
		{
			_mod.DebugEye = v.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = v.Height;
			_mod.SetYaw(v.Yaw);
			_mod.SetPitchDegrees(v.Pitch);
		}

		private static bool Close(Vec3 a, Vec3 b)
		{
			if (Mathf.Abs(a.X - b.X) < 0.001f && Mathf.Abs(a.Y - b.Y) < 0.001f)
			{
				return Mathf.Abs(a.Z - b.Z) < 0.001f;
			}
			return false;
		}

		private static bool SamePalette(uint[] a, uint[] b)
		{
			if (a == null || b == null)
			{
				return a == b;
			}
			if (a.Length != b.Length)
			{
				return false;
			}
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i] != b[i])
				{
					return false;
				}
			}
			return true;
		}
	}
}
