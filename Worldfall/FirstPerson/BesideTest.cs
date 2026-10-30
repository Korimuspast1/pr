using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BesideTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

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
			_mod = WorldBoxMod.Instance;
			Settings s = _mod.Settings;
			bool savedBeside = s.AsyncRender;
			int savedTime = s.TimeMode;
			int savedRes = s.Resolution;
			_savedGpu = s.GpuRender;
			s.AsyncRender = true;
			s.GpuRender = false;
			s.TimeMode = 1;
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 10, 3);
				if (king != null)
				{
					Log.Info($"BESIDE village at {item.x},{item.y}");
					break;
				}
			}
			if (king == null)
			{
				Log.Error("BESIDE could not found a village");
				Finish(quit, s, savedBeside, savedTime, savedRes);
				yield break;
			}
			yield return new WaitForSecondsRealtime(3f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BESIDE first person did not start");
				Finish(quit, s, savedBeside, savedTime, savedRes);
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
			Log.Info($"BESIDE {_mod.Renderer.Width}x{_mod.Renderer.Height} on a {Screen.width}x{Screen.height} screen, {SystemInfo.processorCount} threads");
			int frames = 0;
			int beside = 0;
			int shown0 = _mod.FramesShown;
			for (int i = 0; i < 90; i++)
			{
				yield return null;
				frames++;
				if (_mod.DrawingBeside)
				{
					beside++;
				}
			}
			int num = _mod.FramesShown - shown0;
			Check("frames drawn beside the game", (float)beside >= (float)frames * 0.9f && (float)num >= (float)frames * 0.9f, $"{beside} of {frames} frames in the drawing beside the game, {num} frames shown");
			Vector2 p = king.current_position;
			Building house = null;
			WorldTile tile = World.world.GetTile((int)p.x + 5, (int)p.y + 5);
			try
			{
				house = World.world.buildings.addBuilding("house_human_1", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				house?.completeConstruction();
			}
			catch (Exception ex)
			{
				Log.Error("BESIDE house: " + ex.Message);
			}
			Config.paused = true;
			bool tried = false;
			foreach (Actor v in World.world.units.getSimpleList())
			{
				if (tried)
				{
					break;
				}
				if (v == null || v == king || !v.isAlive() || v.is_inside_building || v.kingdom != king.kingdom || !People.IsHumanoid(v) || (v.current_position - p).magnitude > 20f)
				{
					continue;
				}
				for (int i = 0; i < 4; i++)
				{
					if (tried)
					{
						break;
					}
					float num2 = (float)i * MathF.PI * 0.5f;
					Vector2 vector = new Vector2(Mathf.Cos(num2), Mathf.Sin(num2)) * 3f;
					_mod.DebugEye = v.current_position + vector;
					_mod.DebugEyeSet = true;
					_mod.SetYaw(num2 + MathF.PI);
					s.AsyncRender = false;
					for (int j = 0; j < 3; j++)
					{
						yield return null;
					}
					float num3 = _mod.TerrainForTest.SampleHeight(v.current_position.x, v.current_position.y) + 0.45f;
					_mod.SetPitchDegrees(Mathf.Atan2(num3 - _mod.EyeForTest.z, 3f) * 57.29578f);
					int inStep = 0;
					for (int j = 0; j < 6; j++)
					{
						yield return null;
						if (_mod.Picked == v)
						{
							inStep++;
						}
					}
					if (inStep < 6)
					{
						continue;
					}
					tried = true;
					s.AsyncRender = true;
					for (int j = 0; j < 3; j++)
					{
						yield return null;
					}
					int besidePicks = 0;
					for (int j = 0; j < 10; j++)
					{
						yield return null;
						if (_mod.Picked == v)
						{
							besidePicks++;
						}
					}
					_mod.SaveFrame("beside_pick.png");
					Check("crosshair picks a villager, drawn beside as in step", besidePicks == 10, $"{WorldBoxMod.Describe(v)} picked in {inStep} of 6 frames in step, {besidePicks} of 10 beside (last: {Name(_mod.Picked)})");
				}
			}
			s.AsyncRender = true;
			if (!tried)
			{
				Fail("crosshair picks a villager", "no spot saw a villager in step");
			}
			if (house != null)
			{
				Vector2 vector2 = new Vector2(house.current_tile.posV3.x, house.current_tile.posV3.y);
				_mod.DebugEye = vector2 + new Vector2(0.3f, -4.5f);
				_mod.DebugEyeSet = true;
				_mod.SetYaw(Mathf.Atan2(4.5f, -0.3f));
				_mod.SetPitchDegrees(1f);
				for (int i = 0; i < 10; i++)
				{
					yield return null;
				}
			}
			float yaw = _mod.ViewYaw;
			yield return Shift("turn", delegate
			{
				_mod.SetYaw(yaw + MathF.PI / 90f);
			}, delegate
			{
				_mod.SetYaw(yaw);
			});
			yield return Shift("look up", delegate
			{
				_mod.SetPitchDegrees(2.5f);
			}, delegate
			{
				_mod.SetPitchDegrees(1f);
			});
			yield return Shift("turn right", delegate
			{
				_mod.SetYaw(yaw - MathF.PI / 90f);
			}, delegate
			{
				_mod.SetYaw(yaw);
			});
			if (house != null && HouseInterior.IsEnterable(house))
			{
				_mod.DebugEyeSet = false;
				_mod.EnterHouseForTest(house);
				int i = _mod.FramesShown;
				yield return new WaitForSecondsRealtime(1f);
				Check("a room is drawn beside the game", _mod.Interior.Inside && _mod.DrawingBeside && _mod.FramesShown - i > 10, $"inside {_mod.Interior.Inside}, beside {_mod.DrawingBeside}, {_mod.FramesShown - i} frames shown");
				_mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(1f);
				Check("outdoors again, drawn beside", !_mod.Interior.Inside && _mod.DrawingBeside, $"inside {_mod.Interior.Inside}, beside {_mod.DrawingBeside}");
			}
			else
			{
				Log.Info("BESIDE (no enterable house: the room check skipped)");
			}
			int res = s.Resolution;
			for (int i = 1; i <= WorldBoxMod.ResolutionWidths.Length; i++)
			{
				s.Resolution = (res + i) % WorldBoxMod.ResolutionWidths.Length;
				yield return new WaitForSecondsRealtime(0.6f);
				int num4 = Mathf.Min(WorldBoxMod.ResolutionWidths[s.Resolution], 640);
				Check($"render width {num4}", _mod.IsFirstPerson && _mod.FrameTexture != null && _mod.FrameTexture.width == num4 && _mod.Shown.Width == num4, $"frame {((_mod.FrameTexture != null) ? _mod.FrameTexture.width : 0)} wide, shown {_mod.Shown.Width}");
			}
			s.AsyncRender = false;
			yield return new WaitForSecondsRealtime(0.5f);
			Check("setting off: drawn in step", !_mod.DrawingBeside, $"beside {_mod.DrawingBeside}");
			s.AsyncRender = true;
			yield return new WaitForSecondsRealtime(0.5f);
			Check("setting on: drawn beside", _mod.DrawingBeside, $"beside {_mod.DrawingBeside}");
			Config.paused = false;
			_mod.DebugEyeSet = false;
			for (int i = 0; i < 2; i++)
			{
				yield return Rate("in step", beside: false, s);
				yield return Rate("beside", beside: true, s);
			}
			Log.Info($"BESIDE {_pass} passed, {_fail} failed");
			Finish(quit, s, savedBeside, savedTime, savedRes);
		}

		private IEnumerator Shift(string name, Action turn, Action back)
		{
			for (int i = 0; i < 12; i++)
			{
				yield return null;
			}
			yield return new WaitForEndOfFrame();
			Texture2D before = ScreenCapture.CaptureScreenshotAsTexture();
			yield return null;
			turn();
			yield return new WaitForEndOfFrame();
			Texture2D moved = ScreenCapture.CaptureScreenshotAsTexture();
			FrameProjection shown = _mod.Shown;
			for (int i = 0; i < 12; i++)
			{
				yield return null;
			}
			yield return new WaitForEndOfFrame();
			Texture2D texture2D = ScreenCapture.CaptureScreenshotAsTexture();
			float num = Difference(before, texture2D);
			float num2 = Difference(moved, texture2D);
			Check("the picture keeps up with a " + name, shown.Moves && num2 < num * 0.5f, $"turned {shown.Turn * 57.29578f:F1} deg, horizon moved {shown.ShiftRow:F1} rows; difference to the new frame: unmoved {num:F1}, moved {num2:F1}");
			string text = name.Replace(' ', '_');
			File.WriteAllBytes(Path.Combine(Log.Folder, "beside_" + text + "_0.png"), before.EncodeToPNG());
			File.WriteAllBytes(Path.Combine(Log.Folder, "beside_" + text + "_1.png"), moved.EncodeToPNG());
			File.WriteAllBytes(Path.Combine(Log.Folder, "beside_" + text + "_2.png"), texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(before);
			UnityEngine.Object.Destroy(moved);
			UnityEngine.Object.Destroy(texture2D);
			back();
			for (int i = 0; i < 12; i++)
			{
				yield return null;
			}
		}

		private static float Difference(Texture2D a, Texture2D b)
		{
			if (a == null || b == null || a.width != b.width || a.height != b.height)
			{
				return float.MaxValue;
			}
			Color32[] pixels = a.GetPixels32();
			Color32[] pixels2 = b.GetPixels32();
			int width = a.width;
			int height = a.height;
			long num = 0L;
			long num2 = 0L;
			for (int i = (int)((float)height * 0.4f); i < (int)((float)height * 0.75f); i += 2)
			{
				for (int j = (int)((float)width * 0.08f); j < (int)((float)width * 0.92f); j += 2)
				{
					if (!((float)j > (float)width * 0.38f) || !((float)j < (float)width * 0.62f))
					{
						Color32 color = pixels[i * width + j];
						Color32 color2 = pixels2[i * width + j];
						num += Math.Abs(color.r - color2.r) + Math.Abs(color.g - color2.g) + Math.Abs(color.b - color2.b);
						num2 += 3;
					}
				}
			}
			if (num2 <= 0)
			{
				return float.MaxValue;
			}
			return (float)num / (float)num2;
		}

		private IEnumerator Rate(string name, bool beside, Settings s)
		{
			s.AsyncRender = beside;
			yield return new WaitForSecondsRealtime(1f);
			double frame = 0.0;
			double mod = 0.0;
			int frames = 0;
			int drawnBeside = 0;
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 6f)
			{
				yield return null;
				if (!_mod.IsFirstPerson)
				{
					Log.Error("BESIDE first person ended");
					yield break;
				}
				frame += (double)Time.unscaledDeltaTime * 1000.0;
				mod += (double)_mod.FrameMs;
				if (_mod.DrawingBeside)
				{
					drawnBeside++;
				}
				frames++;
			}
			double num = 1.0 / (double)Math.Max(1, frames);
			Log.Info($"BESIDE rate {name}: {1000.0 / (frame * num):F1} fps, frame {frame * num:F1} ms, mod on the game's thread {mod * num:F1} ms, {drawnBeside} of {frames} frames beside");
		}

		private static string Name(BaseSimObject o)
		{
			if (o == null)
			{
				return "nothing";
			}
			try
			{
				return (o is Building building) ? ("building " + building.asset.id) : ((o is Actor a) ? WorldBoxMod.Describe(a) : o.ToString());
			}
			catch
			{
				return "?";
			}
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
			Log.Info("BESIDE " + (ok ? "PASS" : "FAIL") + " " + what + ": " + detail);
		}

		private void Fail(string what, string detail)
		{
			Check(what, ok: false, detail);
		}

		private void Finish(bool quit, Settings s, bool beside, int time, int res)
		{
			Config.paused = false;
			s.AsyncRender = beside;
			s.GpuRender = _savedGpu;
			s.TimeMode = time;
			s.Resolution = res;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("BESIDE DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
