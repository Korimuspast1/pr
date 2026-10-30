using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GodTurnTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

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
			_mod = WorldBoxMod.Instance;
			Settings s = _mod.Settings;
			bool savedGod = s.GodView;
			int savedTime = s.TimeMode;
			s.TimeMode = 1;
			WorldTile home = null;
			foreach (WorldTile item2 in KingTest.GoodSites())
			{
				if (KingTest.FoundKingdom(item2, "human", 12, 4) != null)
				{
					home = item2;
					break;
				}
			}
			if (home == null)
			{
				Log.Error("GT could not found a village");
				Finish(quit, s, savedGod, savedTime);
				yield break;
			}
			Log.Info($"GT village at {home.x},{home.y}");
			yield return new WaitForSecondsRealtime(5f);
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Vector2 at = new Vector2((float)home.x + 0.5f, (float)home.y + 0.5f);
			Camera cam = World.world.camera;
			int mask = cam.cullingMask;
			s.GodView = true;
			yield return Zoom(at, 25f, 1.5f);
			Check("the view is up, north up at 45 degrees", _mod.GodShown && _mod.GodAlpha >= 1f && _mod.GodTurnForTest == 0f && _mod.GodTipForTest == 0f, $"shown {_mod.GodShown}, cover {_mod.GodAlpha:F2}, turn {_mod.GodTurnForTest:F3}, tip {_mod.GodTipForTest:F2}, keys taken {GodView.KeysHooked}");
			_mod.SaveFrame("gt_north.png");
			List<float> speeds = new List<float>();
			float was = _mod.GodTurnForTest;
			float step = Time.unscaledDeltaTime;
			GodView.TestTurn = new Vector2(1f, 0f);
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
			{
				Hold(at, 25f);
				yield return null;
				float godTurnForTest = _mod.GodTurnForTest;
				speeds.Add(DivePath.Wrap(godTurnForTest - was) / Mathf.Max(0.0001f, step));
				was = godTurnForTest;
				step = Time.unscaledDeltaTime;
			}
			GodView.TestTurn = null;
			float letGo = _mod.GodTurnForTest;
			yield return Zoom(at, 25f, 0.7f);
			float t2 = _mod.GodTurnForTest;
			float num = 0f;
			foreach (float item3 in speeds)
			{
				num = Mathf.Max(num, item3);
			}
			Log.Info($"GT turning: speeds {Describe(speeds)} rad/s; let go at {letGo:F3}, settled at {t2:F3}");
			Check("left turns the view left (anticlockwise seen from above)", t2 > 1f && t2 < 2.2f, $"turned {t2 * 57.29578f:F0} degrees in a second");
			Check("it gathers speed rather than jumping to it", speeds.Count > 4 && speeds[0] < speeds[3] && speeds[0] < 1.2f && num <= 1.785f, $"first {((speeds.Count > 0) ? speeds[0] : 0f):F2}, fourth {((speeds.Count > 3) ? speeds[3] : 0f):F2}, fastest {num:F2}");
			Check("and settles when let go (no snap)", t2 - letGo > 0.05f && t2 - letGo < 0.45f, $"coasted {(t2 - letGo) * 57.29578f:F1} degrees");
			ViewState godShownForTest = _mod.GodShownForTest;
			Check("the frame is drawn turned so", Mathf.Abs(DivePath.Wrap(godShownForTest.Yaw - (MathF.PI / 2f + t2))) < 0.01f, $"yaw {godShownForTest.Yaw:F3}");
			Check("still looking at the same point", Middle(godShownForTest, at, out var detail), detail);
			_mod.SaveFrame("gt_turned.png");
			GodView.TestMouse = new Vector2((float)Screen.width * 0.75f, (float)Screen.height * 0.5f);
			yield return null;
			yield return null;
			GodView.Resolve();
			Vector2 to = new Vector2(Mathf.Cos(t2), Mathf.Sin(t2));
			Vector2 vector = GodView.FlatPoint - GodView.ViewAt;
			Vector2 vector2 = GodView.LandPoint - at;
			float num2 = Vector2.Angle(vector, to);
			float num3 = Vector2.Angle(vector2, to);
			Check("a point right of the middle is on the ground to the view's right", GodView.FlatValid && GodView.MouseValid && num2 < 1f && num3 < 20f, $"flat point {num2:F2} degrees off, the drawn point {num3:F1} degrees off ({vector2.magnitude:F1} tiles out)");
			GodView.TestMouse = null;
			MoveCamera mover = MoveCamera.instance;
			HotkeyAsset pAsset = AssetManager.hotkey_library.get("up");
			mover._move_velocity = Vector2.zero;
			GodView.TestPanKey = true;
			MoveCamera.move(pAsset);
			GodView.TestPanKey = false;
			Vector2 move_velocity = mover._move_velocity;
			Vector2 to2 = new Vector2(0f - Mathf.Sin(t2), Mathf.Cos(t2));
			Check("W pans the way the view faces", move_velocity.magnitude > 0f && Vector2.Angle(move_velocity, to2) < 1f, $"velocity {move_velocity.x:F2},{move_velocity.y:F2}, {Vector2.Angle(move_velocity, to2):F2} degrees off ahead");
			mover._move_velocity = Vector2.zero;
			MoveCamera.move(pAsset);
			Check("an arrow alone doesn't pan while it turns the view", GodView.ArrowsTurn && mover._move_velocity == Vector2.zero, $"arrows turn {GodView.ArrowsTurn}, velocity {mover._move_velocity}");
			mover._move_velocity = Vector2.zero;
			yield return Zoom(at, 25f, 0.3f);
			GodView.TestMouse = new Vector2((float)Screen.width * 0.9f, (float)Screen.height * 0.8f);
			GodView.TestTurn = new Vector2(1f, 0f);
			float worstNew = 0f;
			float worstOld = 0f;
			for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
			{
				Hold(at, 25f);
				yield return null;
				if (!(t < 0.3f))
				{
					GodView.Resolve();
					if (GodView.FlatValid && Expected(at, out var point))
					{
						Vector2 vector3 = GodView.DragPointForTest(mover);
						GodView.StaleDragForTest = true;
						Vector2 vector4 = GodView.DragPointForTest(mover);
						GodView.StaleDragForTest = false;
						worstNew = Mathf.Max(worstNew, (vector3 - point).magnitude);
						worstOld = Mathf.Max(worstOld, (vector4 - point).magnitude);
					}
				}
			}
			GodView.TestTurn = null;
			GodView.TestMouse = null;
			Check("dragging while it turns holds the ground the turning view shows", worstNew < 0.25f && worstNew < worstOld, $"worst {worstNew:F2} tiles off (the point from the frame as it stands: {worstOld:F2})");
			yield return Zoom(at, 25f, 0.6f);
			GodView.TestTurn = new Vector2(0f, 1f);
			yield return Zoom(at, 25f, 1.6f);
			GodView.TestTurn = null;
			yield return Zoom(at, 25f, 0.4f);
			godShownForTest = _mod.GodShownForTest;
			float num4 = Mathf.Atan(0f - godShownForTest.Pitch) * 57.29578f;
			Check("up tips it towards the horizon, as far as 28 degrees", _mod.GodTipForTest >= 0.999f && Mathf.Abs(num4 - 28f) < 0.3f, $"tip {_mod.GodTipForTest:F3}, looking {num4:F1} degrees down");
			Check("tipped, still looking at the same point", Middle(godShownForTest, at, out detail), detail);
			_mod.SaveFrame("gt_tipped.png");
			Log.Info($"GT tipped: frame {_mod.FrameMs:F1} ms on the game's thread");
			GodView.TestTurn = new Vector2(0f, -1f);
			yield return Zoom(at, 25f, 0.35f);
			GodView.TestTurn = new Vector2(1f, 0f);
			yield return Zoom(at, 25f, 0.25f);
			GodView.TestTurn = null;
			Check("down tips it back", _mod.GodTipForTest < 0.9f && _mod.GodTipForTest > 0.05f, $"tip {_mod.GodTipForTest:F2}");
			yield return Leave(at, 25f, delegate
			{
				_mod.SetGodView(on: false);
			}, "V");
			Check("the flat map, the map camera's own drawing back", !_mod.GodShown && !GodView.Active && cam.cullingMask == mask, $"mask {cam.cullingMask:X} (was {mask:X})");
			_mod.SetGodView(on: true);
			List<float> alphas = new List<float>();
			for (float t = 0f; t < 0.7f; t += Time.unscaledDeltaTime)
			{
				Hold(at, 25f);
				yield return null;
				alphas.Add(_mod.GodAlpha);
			}
			bool flag2 = true;
			for (int num5 = 1; num5 < alphas.Count; num5++)
			{
				flag2 &= alphas[num5] >= alphas[num5 - 1];
			}
			Check("V fades the view back in, north up", alphas.Count > 3 && alphas[0] < 0.5f && flag2 && _mod.GodAlpha >= 1f && _mod.GodTurnForTest == 0f, "cover " + Describe(alphas));
			GodView.TestTurn = new Vector2(-1f, 0f);
			yield return Zoom(at, 25f, 0.9f);
			GodView.TestTurn = null;
			yield return Leave(at, 70f, null, "zoom 70");
			yield return Zoom(at, 25f, 1f);
			Check("zoomed back in, it is north up", _mod.GodShown && _mod.GodTurnForTest == 0f, $"turn {_mod.GodTurnForTest:F3}");
			GodView.TestTurn = new Vector2(1f, 0.6f);
			yield return Zoom(at, 20f, 0.7f);
			GodView.TestTurn = null;
			yield return Zoom(at, 20f, 0.6f);
			float t3 = _mod.GodTurnForTest;
			float tip3 = _mod.GodTipForTest;
			Actor actor = null;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.asset != null && simple.asset.id == "human" && !simple.is_inside_building && Vector2.Distance(simple.current_position, at) < 12f)
					{
						actor = simple;
						break;
					}
				}
			}
			catch
			{
			}
			if (actor == null)
			{
				Fail("a villager to dive into", "none near the village");
			}
			else
			{
				ViewState before = _mod.GodShownForTest;
				_mod.SaveFrame("gt_dive_before.png");
				_mod.NoDive = false;
				ControllableUnit.setControllableCreature(actor);
				List<float> yaws = new List<float>();
				List<float> pitches = new List<float>();
				List<float> steps = new List<float>();
				float t = Time.unscaledDeltaTime;
				for (float t4 = 0f; t4 < 3f; t4 += Time.unscaledDeltaTime)
				{
					if (_mod.IsFirstPerson && !_mod.Diving)
					{
						break;
					}
					yield return null;
					float item = t;
					t = Time.unscaledDeltaTime;
					if (_mod.Diving)
					{
						ViewState eyeNowForTest = _mod.EyeNowForTest;
						yaws.Add(eyeNowForTest.Yaw);
						pitches.Add(eyeNowForTest.Pitch);
						steps.Add(item);
						if (yaws.Count == 1)
						{
							_mod.SaveFrame("gt_dive0.png");
						}
						if (yaws.Count == 6)
						{
							_mod.SaveFrame("gt_dive5.png");
						}
					}
				}
				float num6 = ((yaws.Count > 0) ? Mathf.Abs(DivePath.Wrap(yaws[0] - before.Yaw)) : 9f);
				float num7 = ((pitches.Count > 0) ? Mathf.Abs(pitches[0] - before.Pitch) : 9f);
				float num8 = 0f;
				List<float> list = new List<float>();
				for (int num9 = 1; num9 < yaws.Count; num9++)
				{
					list.Add(DivePath.Wrap(yaws[num9] - yaws[num9 - 1]) / Mathf.Max(0.001f, steps[num9]));
					num8 = Mathf.Max(num8, Mathf.Abs(list[list.Count - 1]));
				}
				Log.Info("GT the dive's turn, rad/s each frame: " + string.Join(" ", list.ConvertAll((float r) => r.ToString("F2"))));
				Log.Info($"GT dive from turn {t3:F2} tip {tip3:F2}: the view's yaw {before.Yaw:F3} pitch {before.Pitch:F3}; the dive's first {((yaws.Count > 0) ? yaws[0] : 0f):F3}/{((pitches.Count > 0) ? pitches[0] : 0f):F3}, {yaws.Count} frames");
				Check("the dive starts as the turned view was", yaws.Count > 3 && num6 < 0.06f && num7 < 0.1f, $"yaw {num6:F3} rad off, pitch {num7:F3} off");
				Check("and turns smoothly on the way down", num8 < 10f, $"at most {num8:F2} rad/s");
				yield return new WaitForSecondsRealtime(1.5f);
				Check("dived into the villager", _mod.IsFirstPerson && !_mod.Diving, $"first person {_mod.IsFirstPerson}, diving {_mod.Diving}");
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(2.5f);
				Check("letting go rises back into the view, turned as it was, at 45 degrees", !_mod.IsFirstPerson && _mod.GodShown && Mathf.Abs(DivePath.Wrap(_mod.GodTurnForTest - t3)) < 0.0001f && _mod.GodTipForTest == 0f && Mathf.Abs(DivePath.Wrap(_mod.GodShownForTest.Yaw - (MathF.PI / 2f + t3))) < 0.01f, $"turn {_mod.GodTurnForTest:F3} (was {t3:F3}), tip {_mod.GodTipForTest:F2}, yaw {_mod.GodShownForTest.Yaw:F3}");
				_mod.SaveFrame("gt_after_rise.png");
				_mod.NoDive = true;
			}
			yield return Zoom(at, 25f, 0.5f);
			Plate(at);
			yield return Hover(at);
			s.GodView = savedGod;
			Finish(quit, s, savedGod, savedTime);
		}

		private IEnumerator Leave(Vector2 at, float zoom, Action leave, string how)
		{
			float turn0 = _mod.GodTurnForTest;
			float tip0 = _mod.GodTipForTest;
			leave?.Invoke();
			float clock = 0f;
			float homeAt = -1f;
			float fadeAt = -1f;
			float goneAt = -1f;
			float fastest = 0f;
			float was = turn0;
			float step = Time.unscaledDeltaTime;
			bool coveredTurning = true;
			bool homeFirst = true;
			int n = 0;
			for (; clock < 3f; clock += Time.unscaledDeltaTime)
			{
				Hold(at, zoom);
				yield return null;
				float godTurnForTest = _mod.GodTurnForTest;
				float godTipForTest = _mod.GodTipForTest;
				float godAlpha = _mod.GodAlpha;
				bool flag = godTurnForTest == 0f && godTipForTest == 0f && !_mod.GodHomingForTest;
				fastest = Mathf.Max(fastest, Mathf.Abs(DivePath.Wrap(godTurnForTest - was)) / Mathf.Max(0.0001f, step));
				was = godTurnForTest;
				step = Time.unscaledDeltaTime;
				if (!flag && godAlpha < 1f)
				{
					coveredTurning = false;
				}
				if (flag && homeAt < 0f)
				{
					homeAt = clock;
				}
				if (godAlpha < 1f && fadeAt < 0f)
				{
					fadeAt = clock;
					homeFirst = flag;
				}
				if (!_mod.GodShown)
				{
					goneAt = clock;
					break;
				}
				if (n++ % 4 == 0 && n < 30)
				{
					_mod.SaveFrame($"gt_leave_{how.Replace(' ', '_')}_{n:00}.png");
				}
			}
			Log.Info($"GT leaving by {how} from turn {turn0:F2} tip {tip0:F2}: north up at {homeAt:F2} s, fading from {fadeAt:F2} s, gone at {goneAt:F2} s, fastest turn {fastest:F2} rad/s");
			Check(how + ": the view stays while it turns back", coveredTurning && homeAt > 0.2f, $"north up after {homeAt:F2} s");
			Check(how + ": it turns back smoothly", fastest < 9f, $"fastest {fastest:F2} rad/s");
			Check(how + ": north up at 45 degrees before it fades", homeFirst && fadeAt >= homeAt, $"fading from {fadeAt:F2} s");
			Check(how + ": then fades to the flat map", goneAt > 0f && goneAt - homeAt < 0.7f, $"gone {goneAt - homeAt:F2} s after");
		}

		private static bool Middle(ViewState shown, Vector2 at, out string detail)
		{
			int width = GodView.Width;
			int height = GodView.Height;
			if (!GodTurn.Project(shown, width, height, new Vec3(at.x, at.y, GodView.FlatZ), out var column, out var row))
			{
				detail = "behind the camera";
				return false;
			}
			float num = Mathf.Sqrt((column - (float)width * 0.5f) * (column - (float)width * 0.5f) + (row - (float)height * 0.5f) * (row - (float)height * 0.5f));
			detail = $"{num:F2} px from the middle of {width}x{height}";
			return num < (float)width * 0.01f;
		}

		private bool Expected(Vector2 at, out Vector2 point)
		{
			point = default(Vector2);
			int width = GodView.Width;
			int height = GodView.Height;
			Camera camera = World.world.camera;
			Vector2 vector = camera.transform.position;
			float zoom = Mathf.Max(10f, camera.orthographicSize);
			ViewState camera2 = GodTurn.Tip(GodCamera.Camera(vector.x, vector.y, GodView.FlatZ, zoom, GodView.Turn, width, height), new Vec3(vector.x, vector.y, GodView.FlatZ), _mod.GodTipForTest);
			Vector2 value = GodView.TestMouse.Value;
			Vec3 vec = GodCamera.Ray(camera2, width, height, value.x / (float)Screen.width * (float)width, ((float)Screen.height - value.y) / (float)Screen.height * (float)height);
			if (vec.Z > -0.0001f)
			{
				return false;
			}
			float num = (camera2.Z - GodView.FlatZ) / (0f - vec.Z);
			point = new Vector2(camera2.X + vec.X * num, camera2.Y + vec.Y * num);
			return true;
		}

		private void Plate(Vector2 at)
		{
			NameplateText nameplateText = null;
			try
			{
				NameplateText[] array = Resources.FindObjectsOfTypeAll<NameplateText>();
				foreach (NameplateText nameplateText2 in array)
				{
					if (nameplateText2 != null)
					{
						nameplateText = nameplateText2;
						break;
					}
				}
			}
			catch
			{
			}
			if (nameplateText == null)
			{
				Log.Info("GT no name plate in the scene to ask (skipped)");
				return;
			}
			Vector2 vector = (Vector2)typeof(NameplateText).GetMethod("transformPosition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(nameplateText, new object[1]
			{
				new Vector3(at.x, at.y, 0f)
			});
			float column;
			float row;
			bool flag = GodTurn.Project(GodView.Shown, Screen.width, Screen.height, new Vec3(at.x, at.y, Mathf.Max(GodView.Terrain.SampleHeight(at.x, at.y), 0f)), out column, out row);
			Vector2 vector2 = new Vector2(column, (float)Screen.height - row);
			Vector2 vector3 = World.world.camera.WorldToScreenPoint(new Vector3(at.x, at.y, 0f));
			Check("a name plate goes where the turned view shows its place", flag && (vector - vector2).magnitude < 1f, $"at {vector.x:F0},{vector.y:F0}; the view shows it at {vector2.x:F0},{vector2.y:F0}; the flat map would put it at {vector3.x:F0},{vector3.y:F0}");
		}

		private IEnumerator Hover(Vector2 at)
		{
			NameplateManager manager = World.world.nameplate_manager;
			NameplateText showing = null;
			try
			{
				foreach (NameplateText item in manager._active)
				{
					if (item != null && item.isShowing())
					{
						showing = item;
						break;
					}
				}
			}
			catch
			{
			}
			if (showing == null)
			{
				Log.Info("GT no name plate showing to point at (skipped)");
				yield break;
			}
			Vector2 where = showing.getLastScreenPosition();
			GodView.TestMouse = where;
			for (int k = 0; k < 4; k++)
			{
				Hold(at, 25f);
				yield return null;
			}
			NameplateText cursor_over_text = manager.cursor_over_text;
			GodView.TestMouse = null;
			Check("pointing at a name plate over the turned view picks it", cursor_over_text == showing, string.Format("pointed at {0:F0},{1:F0} ({2}); the game has {3} under the mouse", where.x, where.y, showing.nano_object, (!(cursor_over_text != null)) ? "none" : (cursor_over_text.nano_object?.ToString() ?? "a plate")));
		}

		private static string Describe(List<float> values)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < values.Count; i += Math.Max(1, values.Count / 12))
			{
				list.Add(values[i].ToString("F2"));
			}
			return string.Join(" ", list);
		}

		private IEnumerator Zoom(Vector2 at, float zoom, float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Hold(at, zoom);
				yield return null;
			}
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
				Log.Error("GT camera: " + ex.Message);
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
			Log.Info("GT " + (ok ? "PASS" : "FAIL") + " " + what + ": " + detail);
		}

		private void Fail(string what, string detail)
		{
			Check(what, ok: false, detail);
		}

		private void Finish(bool quit, Settings s, bool god, int time)
		{
			GodView.TestTurn = null;
			GodView.TestMouse = null;
			GodView.TestPanKey = false;
			GodView.StaleDragForTest = false;
			s.GodView = god;
			s.TimeMode = time;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"GT {_pass} passed, {_fail} failed");
			Log.Info("GT DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
