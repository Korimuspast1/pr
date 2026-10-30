using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TearTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private bool _drawHalves;

		private bool _sweepHalves;

		private FrameProjection _drawnFrom;

		private double _rectsMs;

		private double _meshMs;

		private bool _savedGpu;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
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
			WorldBoxMod.BesideAllowed = true;
			ScreenSync.Allowed = true;
			_mod = WorldBoxMod.Instance;
			Settings s = _mod.Settings;
			bool savedBeside = s.AsyncRender;
			bool savedSync = s.VSync;
			int savedTime = s.TimeMode;
			_savedGpu = s.GpuRender;
			s.AsyncRender = true;
			s.GpuRender = false;
			s.VSync = true;
			s.TimeMode = 1;
			bool option = false;
			bool locked = false;
			try
			{
				option = PlayerConfig.optionBoolEnabled("vsync");
			}
			catch (Exception ex)
			{
				Log.Error("TEAR vsync option: " + ex.Message);
			}
			try
			{
				locked = Config.fps_lock_30;
			}
			catch
			{
			}
			double hz = Screen.currentResolution.refreshRateRatio.value;
			int want = ((!locked) ? 1 : Mathf.Clamp((int)(hz / 30.0), 1, 4));
			Log.Info(string.Format("TEAR screen {0}x{1} at {2:F1} Hz, {3}; the game's vsync option {4}, ", Screen.width, Screen.height, hz, Screen.fullScreenMode, option ? "on" : "off") + string.Format("30 fps lock {0}; on the map vSyncCount {1}, target {2} fps, god view shown {3}", locked ? "on" : "off", QualitySettings.vSyncCount, Application.targetFrameRate, _mod.GodShown));
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 10, 3);
				if (king != null)
				{
					Log.Info($"TEAR village at {item.x},{item.y}");
					break;
				}
			}
			if (king == null)
			{
				Log.Error("TEAR could not found a village");
				yield return Finish(quit, s, savedBeside, savedSync, savedTime, want);
				yield break;
			}
			yield return new WaitForSecondsRealtime(3f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("TEAR first person did not start");
				yield return Finish(quit, s, savedBeside, savedSync, savedTime, want);
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
			Log.Info($"TEAR {_mod.Renderer.Width}x{_mod.Renderer.Height} frame, fov {_mod.Settings.FieldOfView:F0}");
			Vector2 current_position = king.current_position;
			Building building = null;
			try
			{
				building = World.world.buildings.addBuilding("house_human_1", World.world.GetTile((int)current_position.x + 5, (int)current_position.y + 5), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
			}
			catch (Exception ex2)
			{
				Log.Error("TEAR house: " + ex2.Message);
			}
			Vector2 vector = ((building != null) ? new Vector2(building.current_tile.posV3.x, building.current_tile.posV3.y) : current_position);
			_mod.DebugEye = vector + new Vector2(0.3f, -4.5f);
			_mod.DebugEyeSet = true;
			float yaw = Mathf.Atan2(4.5f, -0.3f);
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(1f);
			for (int i = 0; i < 20; i++)
			{
				yield return null;
			}
			Check("first person waits for the screen's refresh", QualitySettings.vSyncCount == want, $"vSyncCount {QualitySettings.vSyncCount}, want {want}");
			List<float> on = new List<float>();
			List<float> off = new List<float>();
			for (int i = 0; i < 2; i++)
			{
				s.VSync = true;
				yield return Rate("vsync on", on);
				s.VSync = false;
				for (int j = 0; j < 3; j++)
				{
					yield return null;
				}
				if (i == 0)
				{
					CheckGameOwn("setting off: the game's own sync", option, locked, hz);
				}
				yield return Rate("vsync off", off);
			}
			s.VSync = true;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			Check("setting on again", QualitySettings.vSyncCount == want, $"vSyncCount {QualitySettings.vSyncCount}");
			float num = Mean(on);
			float num2 = Mean(off);
			Check("synced frames cost little", num >= num2 * 0.8f, $"{num:F1} fps synced, {num2:F1} unsynced");
			Config.paused = true;
			float uncover = FrameProjection.MaxUncovered;
			FrameProjection.MaxUncovered = 1f;
			Check("a turned frame can be drawn as one mesh", _mod.HudForTest.FrameMeshForTest, "shader Hidden/Internal-GUITexture");
			yield return Seams("turn 7 left", delegate
			{
				_mod.SetYaw(yaw + 0.12217305f);
			}, delegate
			{
				_mod.SetYaw(yaw);
			}, steps: true);
			yield return Seams("turn 7 right", delegate
			{
				_mod.SetYaw(yaw - 0.12217305f);
			}, delegate
			{
				_mod.SetYaw(yaw);
			}, steps: true);
			yield return Seams("turn 3 left", delegate
			{
				_mod.SetYaw(yaw + MathF.PI / 60f);
			}, delegate
			{
				_mod.SetYaw(yaw);
			}, steps: false);
			yield return Seams("turn 6 and look up", delegate
			{
				_mod.SetYaw(yaw + MathF.PI / 30f);
				_mod.SetPitchDegrees(6f);
			}, delegate
			{
				_mod.SetYaw(yaw);
				_mod.SetPitchDegrees(1f);
			}, steps: true);
			FrameProjection.MaxUncovered = uncover;
			yield return Sweep("slow turn", 20f, 0.25f, 1.5f, yaw);
			yield return Sweep("fast left and right", 40f, 2f, 3f, yaw);
			Config.paused = false;
			_mod.DebugEyeSet = false;
			Log.Info($"TEAR {_pass} passed, {_fail} failed");
			yield return Finish(quit, s, savedBeside, savedSync, savedTime, want);
		}

		private void CheckGameOwn(string what, bool option, bool locked, double hz)
		{
			int num = (option ? ((hz < 61.0) ? 1 : ((hz < 121.0) ? 2 : ((hz < 181.0) ? 3 : 4))) : 0);
			int num2 = (locked ? 30 : 60);
			bool ok = QualitySettings.vSyncCount == num && (option || Application.targetFrameRate == num2);
			Check(what, ok, string.Format("vSyncCount {0}, target {1} fps (the game's: {2}{3})", QualitySettings.vSyncCount, Application.targetFrameRate, num, option ? "" : (", " + num2 + " fps")));
		}

		private IEnumerator Rate(string name, List<float> into)
		{
			yield return new WaitForSecondsRealtime(0.5f);
			List<float> times = new List<float>();
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 4f)
			{
				yield return null;
				if (!_mod.IsFirstPerson)
				{
					Log.Error("TEAR first person ended");
					yield break;
				}
				times.Add(Time.unscaledDeltaTime * 1000f);
			}
			if (times.Count == 0)
			{
				yield break;
			}
			float num = 0f;
			foreach (float item in times)
			{
				num += item;
			}
			times.Sort();
			float num2 = 1000f * (float)times.Count / num;
			into.Add(num2);
			Log.Info($"TEAR rate {name}: {num2:F1} fps, frame median {times[times.Count / 2]:F1} ms, 95% under {times[(int)((float)times.Count * 0.95f)]:F1} ms, " + $"vSyncCount {QualitySettings.vSyncCount}, mod on the game's thread {_mod.FrameMs:F1} ms");
		}

		private static float Mean(List<float> v)
		{
			if (v.Count == 0)
			{
				return 0f;
			}
			float num = 0f;
			foreach (float item in v)
			{
				num += item;
			}
			return num / (float)v.Count;
		}

		private IEnumerator Seams(string name, Action turn, Action back, bool steps)
		{
			for (int i = 0; i < 15; i++)
			{
				yield return null;
			}
			yield return null;
			turn();
			_drawnFrom = default(FrameProjection);
			_drawHalves = true;
			yield return new WaitForEndOfFrame();
			_drawHalves = false;
			Texture2D texture2D = ScreenCapture.CaptureScreenshotAsTexture();
			Texture2D frameTexture = _mod.FrameTexture;
			FrameProjection drawnFrom = _drawnFrom;
			if (texture2D == null || frameTexture == null || !drawnFrom.Moves)
			{
				Fail(name + ": drawn", $"capture {texture2D != null}, frame {frameTexture != null}, moved {drawnFrom.Moves}");
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
				back();
				yield break;
			}
			Color32[] pixels = texture2D.GetPixels32();
			Color32[] pixels2 = frameTexture.GetPixels32();
			int width = texture2D.width;
			int height = texture2D.height;
			Rect view = new Rect(0f, 0f, Mathf.Floor((float)width * 0.5f), Mathf.Floor((float)height * 0.5f));
			Rect view2 = new Rect(view.width, 0f, view.width, view.height);
			float worstColumn;
			float num = Mismatch(pixels, width, height, view, drawnFrom, pixels2, frameTexture.width, frameTexture.height, out worstColumn);
			float worstColumn2;
			float num2 = Mismatch(pixels, width, height, view2, drawnFrom, pixels2, frameTexture.width, frameTexture.height, out worstColumn2);
			string detail = $"turned {drawnFrom.Turn * 57.29578f:F1} deg, horizon moved {drawnFrom.ShiftRow:F1} rows; off the worked-out picture: " + $"mesh {num2 * 100f:F2}% of pixels (worst column {worstColumn2 * 100f:F1}%, drawn in {_meshMs:F2} ms), rectangles {num * 100f:F2}% (worst column {worstColumn * 100f:F1}%, {_rectsMs:F2} ms)";
			Check(name + ": the mesh shows the turned picture", num2 < 0.003f && worstColumn2 < 0.05f, detail);
			if (steps)
			{
				Check(name + ": the rectangles stepped where the mesh does not", num > num2 * 3f + 0.001f, detail);
			}
			File.WriteAllBytes(Path.Combine(Log.Folder, "tear_" + name.Replace(' ', '_') + ".png"), texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
			back();
			for (int i = 0; i < 12; i++)
			{
				yield return null;
			}
		}

		private IEnumerator Sweep(string name, float degrees, float hz, float seconds, float yaw)
		{
			_mod.SetYaw(yaw);
			for (int i = 0; i < 12; i++)
			{
				yield return null;
			}
			yield return new WaitForEndOfFrame();
			float start = Time.unscaledTime;
			float mostNeeded = 0f;
			float mostTurn = 0f;
			float mostUncovered = 0f;
			float mostBehind = 0f;
			float behindWithin = 0f;
			float cap = 0f;
			int frames = 0;
			int capped = 0;
			int inStep = 0;
			bool shot = false;
			while (Time.unscaledTime - start < seconds)
			{
				float num = Time.unscaledTime - start;
				_mod.SetYaw(yaw + degrees * (MathF.PI / 180f) * Mathf.Sin(MathF.PI * 2f * hz * num));
				bool quickest = (_sweepHalves = !shot && hz > 1f && num > 0.4f && Mathf.Abs(Mathf.Cos(MathF.PI * 2f * hz * num)) > 0.95f);
				yield return new WaitForEndOfFrame();
				_sweepHalves = false;
				if (!_mod.IsFirstPerson)
				{
					Fail(name + ": first person", "ended");
					yield break;
				}
				FrameProjection shown = _mod.Shown;
				if (!_mod.DrawingBeside)
				{
					inStep++;
					continue;
				}
				float viewYaw = _mod.ViewYaw;
				float num2 = Mathf.Abs(Mathf.DeltaAngle(shown.View.Yaw * 57.29578f, viewYaw * 57.29578f));
				float num3 = Mathf.Abs(Mathf.DeltaAngle((shown.View.Yaw + shown.Turn) * 57.29578f, viewYaw * 57.29578f));
				cap = shown.MostTurn * 57.29578f;
				frames++;
				if (num2 > cap + 0.01f)
				{
					capped++;
				}
				else
				{
					behindWithin = Mathf.Max(behindWithin, num3);
				}
				mostNeeded = Mathf.Max(mostNeeded, num2);
				mostTurn = Mathf.Max(mostTurn, Mathf.Abs(shown.Turn) * 57.29578f);
				mostUncovered = Mathf.Max(mostUncovered, shown.Uncovered);
				mostBehind = Mathf.Max(mostBehind, num3);
				if (quickest)
				{
					shot = true;
					float maxUncovered = FrameProjection.MaxUncovered;
					FrameProjection.MaxUncovered = 1f;
					float uncovered = _mod.Shown.Uncovered;
					FrameProjection.MaxUncovered = maxUncovered;
					Log.Info($"TEAR {name}: a quickest frame turned {num2:F1} deg since drawn; followed whole it would uncover {uncovered * 100f:F1}% of the width, " + $"now {shown.Uncovered * 100f:F1}% (turned {shown.Turn * 57.29578f:F2} deg, {num3:F1} deg behind the eye)");
					Texture2D texture2D = ScreenCapture.CaptureScreenshotAsTexture();
					if (texture2D != null)
					{
						File.WriteAllBytes(Path.Combine(Log.Folder, "tear_sweep.png"), texture2D.EncodeToPNG());
						UnityEngine.Object.Destroy(texture2D);
					}
				}
			}
			string detail = $"{frames} frames beside the game ({inStep} in step), up to {mostNeeded:F1} deg turned since a frame was drawn ({capped} past {cap:F2}); " + $"picture turned at most {mostTurn:F2} deg, uncovered at most {mostUncovered * 100f:F1}% of the width, at most {mostBehind:F2} deg behind the eye " + $"({behindWithin:F3} on frames within the cap)";
			if (hz > 1f)
			{
				Check($"{name}: at most {FrameProjection.MaxUncovered * 100f:F0}% of an edge uncovered", frames > 20 && capped > 0 && mostUncovered <= FrameProjection.MaxUncovered + 0.002f, detail);
			}
			else
			{
				Check(name + ": followed at once", frames > 20 && capped <= frames / 5 && behindWithin < 0.01f, detail);
			}
			_mod.SetYaw(yaw);
			for (int i = 0; i < 12; i++)
			{
				yield return null;
			}
		}

		private void OnGUI()
		{
			GUI.depth = -2000;
			if (_sweepHalves && Event.current.type == EventType.Repaint && _mod != null && _mod.FrameTexture != null)
			{
				float num = Mathf.Floor((float)Screen.width * 0.5f);
				float height = Mathf.Floor((float)Screen.height * 0.5f);
				float maxUncovered = FrameProjection.MaxUncovered;
				FrameProjection.MaxUncovered = 1f;
				_mod.HudForTest.DrawFrameForTest(new Rect(0f, 0f, num, height), seamless: true);
				FrameProjection.MaxUncovered = maxUncovered;
				_mod.HudForTest.DrawFrameForTest(new Rect(num, 0f, num, height), seamless: true);
			}
			else if (_drawHalves && Event.current.type == EventType.Repaint && !(_mod == null) && !(_mod.FrameTexture == null))
			{
				_drawnFrom = _mod.Shown;
				float num2 = Screen.width;
				float num3 = Screen.height;
				Rect view = new Rect(0f, 0f, Mathf.Floor(num2 * 0.5f), Mathf.Floor(num3 * 0.5f));
				long timestamp = Stopwatch.GetTimestamp();
				_mod.HudForTest.DrawFrameForTest(view, seamless: false);
				long timestamp2 = Stopwatch.GetTimestamp();
				_mod.HudForTest.DrawFrameForTest(new Rect(view.width, 0f, view.width, view.height), seamless: true);
				long timestamp3 = Stopwatch.GetTimestamp();
				_rectsMs = (double)(timestamp2 - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
				_meshMs = (double)(timestamp3 - timestamp2) * 1000.0 / (double)Stopwatch.Frequency;
			}
		}

		private static float Mismatch(Color32[] shot, int sw, int sh, Rect view, FrameProjection p, Color32[] frame, int w, int h, out float worstColumn)
		{
			float num = Mathf.Round(view.y + view.height * 0.2f);
			float num2 = Mathf.Round(view.y + view.height * 0.8f);
			int num3 = 0;
			int num4 = 0;
			worstColumn = 0f;
			int num5 = Mathf.CeilToInt(view.x + view.width * 4f / 16f);
			int num6 = Mathf.FloorToInt(view.x + view.width * 12f / 16f);
			for (int i = num5; i < num6; i++)
			{
				float col = ((float)i + 0.5f - view.x) / view.width * (float)w;
				int num7 = 0;
				int num8 = 0;
				for (int j = (int)num; j < (int)num2; j++)
				{
					float num9 = (float)j + 0.5f;
					p.Source(col, (num9 - view.y) / view.height * (float)h, out var srcCol, out var srcRow);
					Color32 got = shot[(sh - 1 - j) * sw + i];
					num8++;
					if (!Near(frame, w, h, srcCol, srcRow, got))
					{
						num7++;
					}
				}
				num3 += num7;
				num4 += num8;
				if (num8 > 0)
				{
					worstColumn = Mathf.Max(worstColumn, (float)num7 / (float)num8);
				}
			}
			if (num4 <= 0)
			{
				return 1f;
			}
			return (float)num3 / (float)num4;
		}

		private static bool Near(Color32[] frame, int w, int h, float col, float row, Color32 got)
		{
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					int num = Mathf.Clamp(Mathf.FloorToInt(col + (float)i * 0.33f), 0, w - 1);
					int num2 = Mathf.Clamp(Mathf.FloorToInt((float)h - (row + (float)j * 0.33f)), 0, h - 1);
					Color32 color = frame[num2 * w + num];
					if (Math.Abs(color.r - got.r) <= 3 && Math.Abs(color.g - got.g) <= 3 && Math.Abs(color.b - got.b) <= 3)
					{
						return true;
					}
				}
			}
			return false;
		}

		private void Check(string what, bool ok, string detail)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("TEAR " + (ok ? "PASS" : "FAIL") + " " + what + ": " + detail);
		}

		private void Fail(string what, string detail)
		{
			Check(what, ok: false, detail);
		}

		private IEnumerator Finish(bool quit, Settings s, bool beside, bool sync, int time, int want)
		{
			Config.paused = false;
			s.AsyncRender = beside;
			s.GpuRender = _savedGpu;
			s.TimeMode = time;
			s.VSync = true;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(4f);
			bool option = false;
			bool locked = false;
			try
			{
				option = PlayerConfig.optionBoolEnabled("vsync");
			}
			catch
			{
			}
			try
			{
				locked = Config.fps_lock_30;
			}
			catch
			{
			}
			if (_mod != null && !_mod.IsFirstPerson)
			{
				if (_mod.GodShown)
				{
					Check("on the map in the 3D god view: still synced", QualitySettings.vSyncCount == want, $"vSyncCount {QualitySettings.vSyncCount}");
				}
				else
				{
					CheckGameOwn("on the map: the game's own sync back", option, locked, Screen.currentResolution.refreshRateRatio.value);
				}
			}
			s.VSync = sync;
			Log.Info($"TEAR {_pass} passed, {_fail} failed (with the map's)");
			Log.Info("TEAR DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
