using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GodViewTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private bool _gpu;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			_gpu = options.Contains("oncard");
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
			int savedMode = s.TimeMode;
			bool savedGod = s.GodView;
			bool savedGpu = s.GpuRender;
			s.TimeMode = 1;
			s.GpuRender = _gpu;
			Log.Info("GV drawn on the " + (_gpu ? "graphics card" : "CPU"));
			WorldTile home = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					WorldTile current = enumerator.Current;
					home = current;
				}
			}
			if (home == null)
			{
				Log.Error("GV no site");
				Finish(quit);
				yield break;
			}
			Actor actor = KingTest.FoundKingdom(home, "human", 14, 4);
			Log.Info(string.Format("GV site {0},{1}, leader {2}", home.x, home.y, (actor != null) ? actor.name : "none"));
			yield return new WaitForSecondsRealtime(6f);
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Vector2 at = new Vector2((float)home.x + 0.5f, (float)home.y + 0.5f);
			s.GodView = true;
			float[] array = new float[6] { 10f, 20f, 30f, 40f, 50f, 70f };
			for (int i = 0; i < array.Length; i++)
			{
				float t = array[i];
				yield return Zoom(at, t, 1.5f);
				List<float> ms = new List<float>();
				for (int k = 0; k < 40; k++)
				{
					Hold(at, t);
					yield return null;
					if (_mod.GodShown)
					{
						ms.Add(_mod.FrameMs);
					}
				}
				ms.Sort();
				string text = ((ms.Count > 0) ? $"median {ms[ms.Count / 2]:F1} ms, best {ms[0]:F1}, worst {ms[ms.Count - 1]:F1}" : "not drawn");
				string text2 = (_gpu ? ("gpu " + _mod.GpuRendererForTest?.Stats) : (_mod.Renderer.MeshTriangles + " tris"));
				Log.Info($"GV zoom {t}: cover {_mod.GodAlpha:F2}, shown {_mod.GodShown}, mouse hooked {GodView.Active}, {text}, {text2}, {_mod.SceneForTest.Meshes.Count} meshes, {_mod.BillboardCount} billboards, {Time.unscaledDeltaTime * 1000f:F0} ms a game frame");
				if (_gpu && _mod.GodShown)
				{
					Check($"zoom {t}: drawn on the graphics card", OnCard());
				}
				if (_mod.GodShown)
				{
					_mod.SaveFrame($"gv_zoom{t:0}.png");
				}
				Check($"zoom {t}: covers the map as it should", (t <= s.GodViewZoom) ? (_mod.GodShown && _mod.GodAlpha >= 1f) : (!(t >= s.GodViewZoom * 1.5f) || !_mod.GodShown));
			}
			if (_gpu)
			{
				array = new float[2] { 15f, 30f };
				for (int i = 0; i < array.Length; i++)
				{
					float t = array[i];
					yield return Zoom(at, t, 1f);
					s.GpuRender = false;
					for (int k = 0; k < 12; k++)
					{
						Hold(at, t);
						yield return null;
					}
					_mod.SaveFrame($"gv_cmp_z{t:0}_cpu.png");
					s.GpuRender = true;
					for (int k = 0; k < 12; k++)
					{
						Hold(at, t);
						yield return null;
					}
					_mod.SaveFrame($"gv_cmp_z{t:0}_gpu.png");
					Check($"zoom {t}: back on the card after the CPU's frame", OnCard());
				}
			}
			yield return Zoom(at, 20f, 1f);
			CameraShot("gv_camera.png");
			yield return Zoom(at, 25f, 1f);
			MouseMapping(at);
			yield return Pointing(at);
			s.TimeMode = 3;
			yield return Zoom(at, 25f, 1.5f);
			_mod.SaveFrame("gv_night.png");
			s.TimeMode = 1;
			if (_gpu && options.Contains("dark"))
			{
				int[] array2 = new int[2] { 1, 3 };
				for (int i = 0; i < array2.Length; i++)
				{
					int k = (s.TimeMode = array2[i]);
					for (int on = 0; on < 2; on++)
					{
						s.DarkFantasy = on;
						yield return Zoom(at, 25f, 1.5f);
						_mod.SaveFrame("gv_dark_" + ((k == 3) ? "night" : "day") + "_" + ((on == 1) ? "on" : "off") + ".png");
					}
				}
				s.DarkFantasy = 0;
				s.TimeMode = 1;
				Check("the dark fantasy look drawn on the card in the 3D view", OnCard());
			}
			Actor host = null;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.asset != null && simple.asset.id == "human" && Vector2.Distance(simple.current_position, at) < 12f)
					{
						host = simple;
						break;
					}
				}
			}
			catch
			{
			}
			if (host != null)
			{
				yield return Zoom(at, 20f, 1f);
				_mod.SaveFrame("gv_dive_before.png");
				_mod.NoDive = false;
				ControllableUnit.setControllableCreature(host);
				for (int i = 0; i < 6; i++)
				{
					yield return new WaitForSecondsRealtime(0.25f);
					_mod.SaveFrame($"gv_dive_in{i}.png");
				}
				yield return new WaitForSecondsRealtime(2f);
				Check("dived into the villager", _mod.IsFirstPerson && !_mod.Diving);
				if (_gpu)
				{
					Check("first person on the card", OnCard());
				}
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				bool risingOnCard = true;
				for (int i = 0; i < 6; i++)
				{
					yield return new WaitForSecondsRealtime(0.25f);
					_mod.SaveFrame($"gv_dive_out{i}.png");
					if (i < 3)
					{
						risingOnCard &= OnCard();
					}
				}
				if (_gpu)
				{
					Check("the rise back out drawn on the card", risingOnCard);
				}
				yield return new WaitForSecondsRealtime(2f);
				_mod.SaveFrame("gv_dive_after.png");
				Check("back in the 3D view after letting go", !_mod.IsFirstPerson && _mod.GodShown);
				_mod.NoDive = true;
			}
			else
			{
				Log.Error("GV no villager to dive into");
			}
			yield return Magnet(at);
			Camera cam = World.world.camera;
			s.GodView = false;
			yield return null;
			yield return null;
			Check("flat map again when turned off", !_mod.GodShown && !GodView.Active && cam.cullingMask != 0);
			s.GodView = savedGod;
			s.TimeMode = savedMode;
			s.GpuRender = savedGpu;
			Log.Info($"GV {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private bool OnCard()
		{
			if (_mod.GpuRendererForTest != null && _mod.ShownFrame == _mod.GpuRendererForTest.Shown)
			{
				return _mod.GpuRendererForTest.Error == null;
			}
			return false;
		}

		private void Check(string what, bool ok)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("GV " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Zoom(Vector2 at, float zoom, float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Hold(at, zoom);
				yield return null;
			}
		}

		private static Color PatchMean(Texture2D t, float u, float v, float half)
		{
			int num = Mathf.Max(1, Mathf.RoundToInt(half * (float)t.width));
			int num2 = (int)(u * (float)t.width);
			int num3 = (int)(v * (float)t.height);
			int num4 = Mathf.Clamp(num2 - num, 0, t.width - 1);
			int num5 = Mathf.Clamp(num3 - num, 0, t.height - 1);
			int blockWidth = Mathf.Min(2 * num + 1, t.width - num4);
			int blockHeight = Mathf.Min(2 * num + 1, t.height - num5);
			Color[] pixels = t.GetPixels(num4, num5, blockWidth, blockHeight);
			float num6 = 0f;
			float num7 = 0f;
			float num8 = 0f;
			Color[] array = pixels;
			for (int i = 0; i < array.Length; i++)
			{
				Color color = array[i];
				num6 += color.r;
				num7 += color.g;
				num8 += color.b;
			}
			return new Color(num6 / (float)pixels.Length, num7 / (float)pixels.Length, num8 / (float)pixels.Length);
		}

		private Vector2 Drawn(Actor a)
		{
			if (!_mod.SceneForTest.DrawnAt(a, out var drawn))
			{
				return a.current_position;
			}
			return drawn;
		}

		private static void ZoomOnly(float zoom)
		{
			try
			{
				World.world.camera.orthographicSize = zoom;
				if (World.world.move_camera != null)
				{
					World.world.move_camera.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("GV camera: " + ex.Message);
			}
		}

		private IEnumerator Magnet(Vector2 at)
		{
			Actor who = null;
			float num = float.MaxValue;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.asset != null && !(simple.asset.id != "human") && !simple.is_inside_building)
					{
						float num2 = Vector2.Distance(simple.current_position, at);
						if (num2 < num)
						{
							num = num2;
							who = simple;
						}
					}
				}
			}
			catch
			{
			}
			if (who == null)
			{
				Log.Error("GV no villager for the magnet");
				yield break;
			}
			Camera cam = World.world.camera;
			Vector2 start = who.current_position + new Vector2(1.6f, -1.2f);
			yield return Zoom(start, 20f, 1f);
			Log.Info($"GV magnet: villager {WorldBoxMod.Describe(who)} at {who.current_position}, camera at {start}");
			float[] array = new float[6] { 18f, 16f, 14.5f, 13f, 11.5f, 10f };
			for (int i = 0; i < array.Length; i++)
			{
				float z = array[i];
				ZoomOnly(z);
				GodView.TestWheel = 1f;
				for (int k = 0; k < 8; k++)
				{
					ZoomOnly(z);
					yield return null;
				}
			}
			yield return new WaitForSecondsRealtime(1.2f);
			Actor godLockForTest = _mod.GodLockForTest;
			if (godLockForTest != null && godLockForTest != who)
			{
				Log.Info($"GV magnet took {WorldBoxMod.Describe(godLockForTest)}, {Vector2.Distance(godLockForTest.current_position, start):F1} tiles from the middle (the pick was {Vector2.Distance(who.current_position, start):F1})");
				who = godLockForTest;
			}
			float num3 = Vector2.Distance(cam.transform.position, Drawn(who));
			Log.Info(string.Format("GV magnet held {0}, pull {1:F2}, camera {2:F2} tiles off it", (godLockForTest != null) ? WorldBoxMod.Describe(godLockForTest) : "nothing", _mod.GodPullForTest, num3));
			Check("the magnet takes hold of a creature by the middle of the view", godLockForTest != null && Vector2.Distance(godLockForTest.current_position, start) < 7f);
			Check("and centres the view on it", num3 < 0.8f);
			_mod.SaveFrame("gv_magnet.png");
			for (int i = 1; i <= 16; i++)
			{
				if (!(_mod.GodCloseTargetForTest > 0.9009f))
				{
					break;
				}
				GodView.TestWheel = 1f;
				yield return null;
				if (_mod.GodCloseTargetForTest <= 0.9009f)
				{
					break;
				}
				for (int k = 0; k < 7; k++)
				{
					yield return null;
				}
				if (i % 3 == 0)
				{
					_mod.SaveFrame($"gv_close{i}.png");
				}
			}
			GodView.TestWheel = 1f;
			yield return null;
			yield return null;
			Check("a quick extra turn at the closest doesn't snap in", !_mod.IsFirstPerson && ControllableUnit._unit_main != who);
			yield return new WaitForSecondsRealtime(0.6f);
			Log.Info($"GV close zoom {_mod.GodCloseForTest:F2}, map zoom {cam.orthographicSize:F1}");
			Check("the wheel brings the view down to a close-up", _mod.GodCloseForTest < 1f && Mathf.Abs(cam.orthographicSize - 10f) < 0.1f);
			float worst = 0f;
			for (float z = 0f; z < 3f; z += Time.unscaledDeltaTime)
			{
				yield return null;
				if (!who.isAlive())
				{
					break;
				}
				worst = Mathf.Max(worst, Vector2.Distance(cam.transform.position, Drawn(who)));
			}
			Log.Info($"GV following: the view's centre at most {worst:F2} tiles off the villager over 3 s");
			Check("the close-up follows the villager", worst < 0.7f && _mod.GodLockForTest == who);
			_mod.SaveFrame("gv_close_last.png");
			_mod.NoDive = false;
			GodView.TestWheel = 1f;
			yield return null;
			for (int i = 0; i < 8; i++)
			{
				_mod.SaveFrame($"gv_snap{i}.png");
				yield return new WaitForSecondsRealtime(0.12f);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Check("one more turn steps into the villager's eyes", _mod.IsFirstPerson && _mod.Host == who && !_mod.Diving);
			_mod.SaveFrame("gv_snap_eyes.png");
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.NoDive = true;
			Check("letting go rises back into the view, still following the body", !_mod.IsFirstPerson && _mod.GodShown && _mod.GodLockForTest == who);
			_mod.SaveFrame("gv_after_snap.png");
			cam.transform.position += new Vector3(4f, 0f, 0f);
			yield return null;
			yield return null;
			Check("panning lets the villager go", _mod.GodLockForTest == null);
		}

		private static void Hold(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				if (World.world.move_camera != null)
				{
					World.world.move_camera.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("GV camera: " + ex.Message);
			}
		}

		private void MouseMapping(Vector2 at)
		{
			ViewState view = GodView.View;
			TerrainCache terrain = GodView.Terrain;
			int width = GodView.Width;
			int height = GodView.Height;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			float num4 = 0f;
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -3; j <= 3; j++)
				{
					float num5 = at.x + (float)j * 6f;
					float num6 = at.y + (float)i * 5f;
					float z = Mathf.Max(terrain.SampleHeight(num5, num6), 0f);
					if (!Project(view, width, height, num5, num6, z, out var col, out var row) || col < 0f || row < 0f || col >= (float)width || row >= (float)height)
					{
						continue;
					}
					if (!GodCamera.GroundPoint(terrain, view, width, height, col, row, out var point))
					{
						num2++;
						continue;
					}
					float num7 = Mathf.Cos(view.Yaw);
					float num8 = Mathf.Sin(view.Yaw);
					if ((point.X - view.X) * num7 + (point.Y - view.Y) * num8 < (num5 - view.X) * num7 + (num6 - view.Y) * num8 - 0.5f)
					{
						num3++;
						continue;
					}
					num2++;
					float num9 = Mathf.Sqrt((point.X - num5) * (point.X - num5) + (point.Y - num6) * (point.Y - num6));
					num4 = Mathf.Max(num4, num9);
					if (num9 < 0.5f)
					{
						num++;
					}
				}
			}
			Log.Info($"GV mouse mapping: {num}/{num2} ground points in sight come back within half a tile (worst {num4:F2}), {num3} behind hills");
			Check("the view's pixels map back to the ground they show (bar those hidden behind hills)", num2 > 10 && (float)num >= (float)num2 * 0.9f);
			int num10 = 0;
			int num11 = 0;
			int num12 = 0;
			int num13 = 0;
			for (int k = 0; k < 9; k++)
			{
				for (int l = 0; l < 12; l++)
				{
					float column = (float)width * (0.1f + 0.8f * (float)l / 11f);
					float row2 = (float)height * (0.15f + 0.8f * (float)k / 8f);
					Vec3 point3;
					if (!GodCamera.DrawnPoint(view, width, height, GodView.Depth, column, row2, out var point2))
					{
						num12++;
					}
					else if (point2.Z > Mathf.Max(terrain.SampleHeight(point2.X, point2.Y), 0f) + 0.6f)
					{
						num13++;
					}
					else if (GodCamera.GroundPoint(terrain, view, width, height, column, row2, out point3))
					{
						num11++;
						float num14 = point2.X - point3.X;
						float num15 = point2.Y - point3.Y;
						if (num14 * num14 + num15 * num15 < 1f)
						{
							num10++;
						}
					}
				}
			}
			Log.Info($"GV depths: {num10}/{num11} pixels' depths put them on the ground their ray meets, {num13} on trees or roofs, {num12} sky (depths {((GodView.Depth != null) ? GodView.Depth.Length : 0)} for {width}x{height})");
			Check("the frame's depths show the land where it is", num11 > 15 && (float)num10 >= (float)num11 * 0.5f);
			if (GodCamera.DrawnPoint(view, width, height, GodView.Depth, (float)width * 0.5f, (float)height * 0.5f, out var point4))
			{
				Log.Info($"GV centre pixel shows {point4.X:F1},{point4.Y:F1},{point4.Z:F1}; the map camera looks at {at.x:F1},{at.y:F1}");
			}
			try
			{
				Vector2 mousePos = World.world.getMousePos();
				WorldTile mouseTilePos = World.world.getMouseTilePos();
				Log.Info(string.Format("GV game mouse at {0:F0},{1:F0}: point {2:F1},{3:F1}, tile {4}, hooks {5}, our point {6}", Input.mousePosition.x, Input.mousePosition.y, mousePos.x, mousePos.y, (mouseTilePos != null) ? (mouseTilePos.x + "," + mouseTilePos.y) : "none", GodView.Installed, GodView.MouseValid ? GodView.MousePoint.ToString() : "none"));
				Check("the game's mouse goes through the view", GodView.Installed && (!GodView.MouseValid || (mouseTilePos != null && mouseTilePos.x == GodView.MouseTile.x && mouseTilePos.y == GodView.MouseTile.y)));
			}
			catch (Exception ex)
			{
				Log.Error("GV game mouse: " + ex);
			}
		}

		private void CameraShot(string name)
		{
			Camera camera = World.world.camera;
			RenderTexture renderTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			RenderTexture targetTexture = camera.targetTexture;
			RenderTexture active = RenderTexture.active;
			try
			{
				camera.targetTexture = renderTexture;
				camera.Render();
				RenderTexture.active = renderTexture;
				Texture2D texture2D = new Texture2D(960, 540, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, 960f, 540f), 0, 0);
				texture2D.Apply();
				File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
				bool flag = OnCard();
				Texture2D texture2D2 = (flag ? _mod.GpuRendererForTest.ReadOutput() : _mod.FrameTexture);
				int num = 0;
				int num2 = 0;
				for (int i = 0; i < 5; i++)
				{
					for (int j = 0; j < 7; j++)
					{
						float u = 0.3f + 0.4f * ((float)j + 0.5f) / 7f;
						float v = 0.3f + 0.4f * ((float)i + 0.5f) / 5f;
						Color color = PatchMean(texture2D, u, v, 0.005f);
						Color color2 = PatchMean(texture2D2, u, v, 0.005f);
						num2++;
						if (Mathf.Abs(color.r - color2.r) + Mathf.Abs(color.g - color2.g) + Mathf.Abs(color.b - color2.b) < 0.2f)
						{
							num++;
						}
					}
				}
				Log.Info($"GV camera shot {name}: {num}/{num2} spots match the frame, camera aspect {camera.aspect:F2}, screen {Screen.width}x{Screen.height}");
				_mod.SaveFrame(name.Replace(".png", "_frame.png"));
				Log.Info(string.Format("GV camera rect {0}, scale {1}, size {2}, pixel {3}x{4}, layer 30 '{5}', components {6}", camera.rect, camera.transform.lossyScale, camera.orthographicSize, camera.pixelWidth, camera.pixelHeight, LayerMask.LayerToName(30), string.Join(", ", Array.ConvertAll(camera.GetComponents<Component>(), (Component c) => c.GetType().Name))));
				Check("the map camera shows the view's frame", (float)num >= (float)num2 * 0.6f);
				UnityEngine.Object.Destroy(texture2D);
				if (flag)
				{
					UnityEngine.Object.Destroy(texture2D2);
				}
			}
			catch (Exception ex)
			{
				Log.Error("GV camera shot: " + ex);
			}
			finally
			{
				camera.targetTexture = targetTexture;
				RenderTexture.active = active;
				renderTexture.Release();
			}
		}

		private IEnumerator Pointing(Vector2 at)
		{
			int w = GodView.Width;
			int h = GodView.Height;
			float col = Mathf.Floor((float)w * 0.58f) + 0.5f;
			float row = Mathf.Floor((float)h * 0.62f) + 0.5f;
			if (!GodCamera.GroundPoint(GodView.Terrain, GodView.View, w, h, col, row, GodView.Cut, out var point))
			{
				Check("the pixel to point at shows the world", ok: false);
				yield break;
			}
			Vector2 spot = new Vector2(point.X, point.Y);
			GodView.TestCursor = true;
			Vector2 point2 = Vector2.zero;
			WorldTile tile = null;
			Actor near = null;
			yield return PointAt(at, () => new Vector2(col, row), delegate(WorldTile t, Vector2 p, Actor a)
			{
				tile = t;
				point2 = p;
				near = a;
			});
			_mod.SaveFrame("gv_pointing.png");
			Actor mouseCreature = GodView.MouseCreature;
			Log.Info(string.Format("GV pointing at {0:F1},{1:F1}: the game says tile {2}, point {3:F1},{4:F1}, over {5}", spot.x, spot.y, (tile != null) ? (tile.x + "," + tile.y) : "none", point2.x, point2.y, (mouseCreature != null) ? mouseCreature.asset.id : "no creature"));
			bool ok = tile != null && Mathf.Abs((float)tile.x + 0.5f - spot.x) <= 1.1f && Mathf.Abs((float)tile.y + 0.5f - spot.y) <= 1.1f;
			Check("the game's tile under the mouse is the land the view shows there", ok);
			Check("the game's point under the mouse is the land the view shows there (or a creature standing on it)", (mouseCreature == null) ? (Vector2.Distance(point2, spot) < 0.8f) : (Vector2.Distance(point2, mouseCreature.current_position) < 0.4f));
			Check("map dragging has a point to hold on to", GodView.FlatValid);
			ViewState v = GodView.View;
			float num = Mathf.Cos(v.Yaw);
			float num2 = Mathf.Sin(v.Yaw);
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			int num8 = 0;
			float num9 = 0f;
			float num10 = float.MaxValue;
			float tc = 0f;
			float tr = 0f;
			Vec3 drawnAt = default(Vec3);
			Vec3 landAt = default(Vec3);
			float perTile = 3f * Mathf.Tan(v.Fov * 0.5f) / ((float)Screen.width * 0.5f);
			Vec3 eye = new Vec3(v.X, v.Y, v.Z);
			Vector2 nudge;
			for (int num11 = 0; num11 < 24; num11++)
			{
				for (int num12 = 0; num12 < 32; num12++)
				{
					float num13 = Mathf.Floor((float)w * (0.1f + 0.8f * (float)num12 / 31f)) + 0.5f;
					float num14 = Mathf.Floor((float)h * (0.12f + 0.8f * (float)num11 / 23f)) + 0.5f;
					if (!GodCamera.DrawnPoint(v, w, h, GodView.Depth, num13, num14, out var point3))
					{
						num6++;
					}
					else
					{
						if (!GodCamera.GroundPoint(GodView.Terrain, v, w, h, num13, num14, GodView.Cut, out var point4))
						{
							continue;
						}
						float num15 = (point3.X - v.X) * num + (point3.Y - v.Y) * num2;
						float num16 = (point4.X - v.X) * num + (point4.Y - v.Y) * num2;
						float num17 = (num15 - num16) / num16;
						if (GodView.Terrain.SampleHeight(point4.X, point4.Y) < 0f)
						{
							num7++;
							if (Mathf.Abs(num17) <= 0.012f)
							{
								num8++;
							}
						}
						else if (num17 < -0.012f)
						{
							num5++;
						}
						else if (num17 > 0.015f)
						{
							num4++;
							num9 = Mathf.Max(num9, num15 - num16);
							if (num4 <= 4)
							{
								TerrainCache terrain = GodView.Terrain;
								bool flag = (terrain.Flags[Mathf.Clamp((int)point4.Y, 0, terrain.Height - 1) * terrain.Width + Mathf.Clamp((int)point4.X, 0, terrain.Width - 1)] & 8) != 0;
								Log.Info(string.Format("GV past the mouse's land at pixel {0:F0},{1:F0}: the mouse meets {2:F1},{3:F1},{4:F2} (the cut-away caps it at {5:F2}{6}), the frame shows {7:F1},{8:F1},{9:F2} ({10:F2} further)", num13, num14, point4.X, point4.Y, point4.Z, GodView.Cut.Cap(num16), flag ? ", a wall" : "", point3.X, point3.Y, point3.Z, num15 - num16));
							}
						}
						else
						{
							num3++;
						}
						float num18 = point3.Z - Mathf.Max(GodView.Terrain.SampleHeight(point3.X, point3.Y), 0f);
						float num19 = Mathf.Sqrt((point3.X - point4.X) * (point3.X - point4.X) + (point3.Y - point4.Y) * (point3.Y - point4.Y));
						if (!(num18 < 1.2f) && !(num19 < 2f) && GodView.CreatureOnRay(eye, point4, perTile * 2f, out nudge) == null)
						{
							float num20 = Mathf.Abs(num13 - (float)w * 0.5f) + Mathf.Abs(num14 - (float)h * 0.5f);
							if (num20 < num10)
							{
								num10 = num20;
								tc = num13;
								tr = num14;
								drawnAt = point3;
								landAt = point4;
							}
						}
					}
				}
			}
			Log.Info($"GV land under the mouse vs the depths: {num3} the same, {num5} with something in front, {num4} past the land the mouse finds (worst {num9:F2} tiles), {num6} sky; sea {num7} ({num8} at the surface)");
			Check("the land the mouse finds is the land the frame shows", num3 > 100 && (float)num4 <= (float)(num3 + num4) * 0.04f);
			if (num10 == float.MaxValue)
			{
				Log.Info("GV pointing over a tree: nothing standing up in view to point at (skipped)");
			}
			else
			{
				yield return PointAt(at, () => new Vector2(tc, tr), delegate(WorldTile t, Vector2 p, Actor a)
				{
					tile = t;
					point2 = p;
					near = a;
				});
				_mod.SaveFrame("gv_pointing_tree.png");
				Vector2 b = new Vector2(landAt.X, landAt.Y);
				Vector2 b2 = new Vector2(drawnAt.X, drawnAt.Y);
				Log.Info(string.Format("GV pointing over something {0:F1} up at {1:F1},{2:F1}, the land behind at {3:F1},{4:F1}: the game says point {5:F1},{6:F1}, tile {7}, over {8}", drawnAt.Z - GodView.Terrain.SampleHeight(b2.x, b2.y), b2.x, b2.y, b.x, b.y, point2.x, point2.y, (tile != null) ? (tile.x + "," + tile.y) : "none", (GodView.MouseCreature != null) ? GodView.MouseCreature.asset.id : "no creature"));
				bool flag2 = tile != null && Mathf.Abs((float)tile.x + 0.5f - b.x) <= 1.1f && Mathf.Abs((float)tile.y + 0.5f - b.y) <= 1.1f;
				Check("a tree or roof under the mouse doesn't catch it: the tile and the point are the land behind", flag2 && Vector2.Distance(point2, b) < 0.8f && Vector2.Distance(point2, b2) > 1.2f);
			}
			Actor who = null;
			float num21 = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && Hitboxes.Seen(simple) && Hitboxes.Get(simple, out var box) && !(box.HalfUp < 0.2f) && HeadPixel(simple, out var pixel) && GodCamera.GroundPoint(GodView.Terrain, v, w, h, pixel.x, pixel.y, GodView.Cut, out var point5) && GodView.CreatureOnRay(eye, point5, perTile, out nudge) == simple)
				{
					float num22 = Mathf.Abs(pixel.x - (float)w * 0.5f) + Mathf.Abs(pixel.y - (float)h * 0.5f);
					if (num22 < num21)
					{
						num21 = num22;
						who = simple;
					}
				}
			}
			if (who == null)
			{
				Log.Info("GV pointing at a creature: none in view to point at (skipped)");
			}
			else
			{
				Actor target = who;
				Vector2 pixel3;
				yield return PointAt(at, () => (!HeadPixel(target, out pixel3)) ? ((Vector2?)null) : new Vector2?(pixel3), delegate(WorldTile t, Vector2 p, Actor a)
				{
					tile = t;
					point2 = p;
					near = a;
				});
				_mod.SaveFrame("gv_pointing_creature.png");
				Vector2 landPoint = GodView.LandPoint;
				Log.Info(string.Format("GV pointing at the head of {0} at {1:F1},{2:F1}: the game picks {3}, point {4:F1},{5:F1}, the land behind at {6:F1},{7:F1}, tile {8}", who.asset.id, who.current_position.x, who.current_position.y, (near != null) ? (near.asset.id + " at " + near.current_position.x.ToString("F1") + "," + near.current_position.y.ToString("F1")) : "nobody", point2.x, point2.y, landPoint.x, landPoint.y, (tile != null) ? (tile.x + "," + tile.y) : "none"));
				Check("a click on a creature's head picks that creature", near == who);
				Check("and the tile there is still the land", tile != null && tile.x == Mathf.FloorToInt(landPoint.x) && tile.y == Mathf.FloorToInt(landPoint.y));
				if (HeadPixel(who, out var pixel2) && GodCamera.GroundPoint(GodView.Terrain, GodView.View, w, h, pixel2.x, pixel2.y, GodView.Cut, out var point6))
				{
					Vec3 vec = new Vec3(GodView.View.X, GodView.View.Y, GodView.View.Z);
					Actor actor = GodView.CreatureOnRay(vec, point6, perTile, out nudge);
					HashSet<Actor> magnet_units = World.world.magnet._magnet_units;
					magnet_units.Add(who);
					Actor actor2;
					try
					{
						actor2 = GodView.CreatureOnRay(vec, point6, perTile, out nudge);
					}
					finally
					{
						magnet_units.Remove(who);
					}
					Log.Info("GV the creature on the ray: " + ((actor != null) ? actor.asset.id : "none") + ", while the magnet carries it " + ((actor2 != null) ? actor2.asset.id : "none"));
					Check("the mouse looks through a creature the magnet carries", actor == who && actor2 != who);
				}
			}
			GodView.TestMouse = null;
			GodView.TestCursor = false;
		}

		private static bool HeadPixel(Actor a, out Vector2 pixel)
		{
			pixel = default(Vector2);
			if (!Hitboxes.Get(a, out var box))
			{
				return false;
			}
			if (!Project(GodView.View, GodView.Width, GodView.Height, box.Centre.X, box.Centre.Y, box.Centre.Z + box.HalfUp * 0.6f, out var col, out var row))
			{
				return false;
			}
			if (col < 2f || row < 2f || col >= (float)(GodView.Width - 2) || row >= (float)(GodView.Height - 2))
			{
				return false;
			}
			pixel = new Vector2(col, row);
			return true;
		}

		private IEnumerator PointAt(Vector2 at, Func<Vector2?> pixel, Action<WorldTile, Vector2, Actor> said)
		{
			for (int k = 0; k < 20; k++)
			{
				Vector2? vector = pixel();
				if (vector.HasValue)
				{
					GodView.TestMouse = new Vector2(vector.Value.x / (float)GodView.Width * (float)Screen.width, (float)Screen.height - vector.Value.y / (float)GodView.Height * (float)Screen.height);
				}
				Hold(at, 25f);
				yield return null;
			}
			try
			{
				said(World.world.getMouseTilePos(), World.world.getMousePos(), World.world.getActorNearCursor());
			}
			catch (Exception ex)
			{
				Log.Error("GV pointing: " + ex.Message);
			}
		}

		private static bool Project(ViewState v, int w, int h, float x, float y, float z, out float col, out float row)
		{
			float num = (float)w * 0.5f / Mathf.Tan(v.Fov * 0.5f);
			float num2 = (float)h * 0.5f + v.Pitch * num;
			float num3 = Mathf.Cos(v.Yaw);
			float num4 = Mathf.Sin(v.Yaw);
			float num5 = x - v.X;
			float num6 = y - v.Y;
			float num7 = num5 * num3 + num6 * num4;
			col = (row = 0f);
			if (num7 < 0.05f)
			{
				return false;
			}
			col = (float)w * 0.5f + (num5 * num4 - num6 * num3) / num7 * num;
			row = num2 - (z - v.Z) / num7 * num;
			return true;
		}

		private static void Finish(bool quit)
		{
			GodView.TestMouse = null;
			GodView.TestCursor = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("GV DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
