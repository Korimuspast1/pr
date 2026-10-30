using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class DragTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private readonly Queue<Event> _events = new Queue<Event>();

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
			Actor king = null;
			WorldTile home = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 12, 4);
				if (king != null)
				{
					home = item;
					break;
				}
			}
			if (king == null)
			{
				Log.Error("DRAG could not found a village");
				Finish(quit, s, savedGod, savedTime);
				yield break;
			}
			Log.Info($"DRAG village at {home.x},{home.y}");
			yield return new WaitForSecondsRealtime(4f);
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			s.GodView = true;
			Vector2 at = new Vector2((float)home.x + 0.5f, (float)home.y - 4.5f);
			for (int i = 0; i < 60; i++)
			{
				Hold(at, 25f);
				yield return null;
			}
			MoveCamera mover = World.world.move_camera;
			Camera camera = World.world.camera;
			Log.Info($"DRAG god view shown {_mod.GodShown}, mouse hooked {GodView.Active}; the mover's transform is the camera's: {mover != null && camera != null && mover.transform == camera.transform}");
			if (!_mod.GodShown || !GodView.Active || mover == null)
			{
				Fail("the god view is up for the drag", $"shown {_mod.GodShown}, hooked {GodView.Active}, mover {mover != null}");
			}
			else
			{
				float[] stale = new float[4];
				float[] fresh = new float[4];
				yield return GodDrag("as before (the last frame's point as it stands)", stale: true, at, mover, stale);
				yield return GodDrag("now", stale: false, at, mover, fresh);
				Check("the god view: the map stops with the mouse", fresh[0] < 0.1f, $"moved {fresh[0]:F2} tiles in the last 25 frames after the mouse stopped (before: {stale[0]:F2})");
				Check("the god view: the ground the drag began on stays under the mouse", fresh[1] < 0.35f, $"{fresh[1]:F2} tiles off (before: {stale[1]:F2})");
				Check("the god view: the map moves steadily while dragged", fresh[3] <= 2f, $"{fresh[3]:F0} of the sweep's frames stood still or crept (before: {stale[3]:F0})");
				Check("the god view: the old drag stuttered and wobbled (the test sees what was wrong)", stale[0] > fresh[0] * 5f + 0.2f && stale[3] > fresh[3] + 4f, $"before {stale[0]:F2} tiles of wobble, worst swing back {stale[2]:F2}, {stale[3]:F0} stalled frames; now {fresh[0]:F2}, {fresh[2]:F2}, {fresh[3]:F0}");
			}
			GodView.TestMouse = null;
			GodView.StaleDragForTest = false;
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Fail("first person for the war map", "did not start");
				Finish(quit, s, savedGod, savedTime);
				yield break;
			}
			_mod.HintUntil = 0f;
			WarMap map = _mod.WarMap;
			if (map.Open)
			{
				map.Close();
			}
			map.Toggle(_mod.Host, _mod.Toast);
			yield return new WaitForSecondsRealtime(1f);
			if (!map.Open)
			{
				Fail("the war map opens", "not open");
				Finish(quit, s, savedGod, savedTime);
				yield break;
			}
			yield return WarMapDrags(map);
			map.Close();
			Log.Info($"DRAG {_pass} passed, {_fail} failed");
			Finish(quit, s, savedGod, savedTime);
		}

		private IEnumerator GodDrag(string name, bool stale, Vector2 at, MoveCamera mover, float[] result)
		{
			GodView.StaleDragForTest = stale;
			for (int i = 0; i < 30; i++)
			{
				Hold(at, 25f);
				yield return null;
			}
			Vector2 mouse0 = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.45f);
			GodView.TestMouse = mouse0;
			for (int i = 0; i < 4; i++)
			{
				Hold(at, 25f);
				yield return null;
			}
			yield return null;
			Vector3 origin = GodView.DragPointForTest(mover);
			List<Vector2> path = new List<Vector2>();
			for (int i = 0; i < 70; i++)
			{
				float num = Mathf.Min(1f, (float)(i + 1) / 32f);
				GodView.TestMouse = mouse0 + new Vector2(300f, 120f) * num;
				yield return null;
				Vector3 position = mover.transform.position;
				position.z = 0f;
				Vector3 vector = GodView.DragPointForTest(mover) - position;
				Vector3 vector2 = origin - vector;
				vector2.z = 0f;
				mover.transform.position = vector2;
				path.Add(vector2);
			}
			float still = 0f;
			float swing = 0f;
			float stalled = 0f;
			for (int j = 45; j < path.Count; j++)
			{
				still += (path[j] - path[j - 1]).magnitude;
			}
			float num2 = (path[30] - path[2]).magnitude / 28f;
			for (int k = 3; k <= 30; k++)
			{
				if ((path[k] - path[k - 1]).magnitude < num2 * 0.25f)
				{
					stalled += 1f;
				}
			}
			for (int l = 2; l < path.Count; l++)
			{
				Vector2 lhs = path[l - 1] - path[l - 2];
				Vector2 rhs = path[l] - path[l - 1];
				if (Vector2.Dot(lhs, rhs) < 0f)
				{
					swing = Mathf.Max(swing, rhs.magnitude);
				}
			}
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			GodView.Resolve();
			float num3 = (GodView.FlatValid ? (GodView.FlatPoint - (Vector2)origin).magnitude : float.MaxValue);
			result[0] = still;
			result[1] = num3;
			result[2] = swing;
			result[3] = stalled;
			StringBuilder stringBuilder = new StringBuilder();
			for (int m = 0; m < path.Count; m += 5)
			{
				stringBuilder.Append($" {path[m].x - at.x:F1},{path[m].y - at.y:F1}");
			}
			Log.Info($"DRAG god view {name}: still-phase motion {still:F2} tiles, ground off {num3:F2}, worst swing back {swing:F2}, {stalled:F0} stalled frames of 28; camera every 5 frames:{stringBuilder}");
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
				Log.Error("DRAG camera: " + ex.Message);
			}
		}

		private IEnumerator WarMapDrags(WarMap map)
		{
			float span0 = map.Span;
			float zoom0 = map.Zoom;
			Vector2 c0 = map.Centre;
			yield return new WaitForSecondsRealtime(3f);
			Check("the war map holds still while open", map.Span == span0 && map.Zoom == zoom0 && (map.Centre - c0).magnitude < 0.01f, $"span {span0:F1} -> {map.Span:F1}, zoom {zoom0:F2} -> {map.Zoom:F2}, centre moved {(map.Centre - c0).magnitude:F2}; region now {map.RegionSize:F1}, bounds {map.Bounds}");
			Rect r = _mod.HudForTest.WarMapRectForTest;
			if (r.width <= 0f)
			{
				Fail("the war map is drawn", "no map rect");
				yield break;
			}
			Vector2 mid = r.center;
			float view = map.Span / map.Zoom;
			c0 = map.Centre;
			yield return Drag(2, mid, mid + new Vector2(r.width * 0.2f, 0f));
			float num = c0.x - map.Centre.x;
			Check("middle-drag moves the map at full zoom-out", num > view * 0.08f, $"centre moved {num:F2} tiles west for a drag of a fifth of the map east (view {view:F1} across)");
			for (int i = 0; i < 6; i++)
			{
				_events.Enqueue(new Event
				{
					type = EventType.ScrollWheel,
					mousePosition = mid,
					delta = new Vector2(0f, -1f)
				});
			}
			yield return Flush();
			view = map.Span / map.Zoom;
			c0 = map.Centre;
			Vector2 d = new Vector2((0f - r.width) * 0.15f, r.height * 0.1f);
			int flags = map.Flags.Count;
			yield return Drag(1, mid, mid + d);
			Vector2 vector = new Vector2((0f - d.x) / r.width * view, d.y / r.width * view);
			Vector2 vector2 = map.Centre - c0;
			Check("right-drag moves the map with the mouse", (vector2 - vector).magnitude < view * 0.01f && map.Flags.Count == flags, $"zoom {map.Zoom:F2}: centre moved {vector2.x:F2},{vector2.y:F2}, the mouse's travel {vector.x:F2},{vector.y:F2}; flags {flags} -> {map.Flags.Count}");
			map.Marked.Clear();
			yield return Drag(1, mid + new Vector2(30f, 30f), mid + new Vector2(30f, 30f));
			int planted = map.Flags.Count;
			yield return Drag(2, mid - new Vector2(60f, 40f), mid - new Vector2(60f, 40f));
			Check("a right click with nobody marked plants no flag, nor a middle click", planted == flags && map.Flags.Count == planted, $"flags {flags}, after the right click {planted}, after the middle click {map.Flags.Count}");
			map.ClearFlags();
		}

		private IEnumerator Drag(int button, Vector2 from, Vector2 to)
		{
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = button,
				mousePosition = from,
				clickCount = 1
			});
			if (to != from)
			{
				for (int i = 1; i <= 10; i++)
				{
					_events.Enqueue(new Event
					{
						type = EventType.MouseDrag,
						button = button,
						mousePosition = Vector2.Lerp(from, to, (float)i / 10f)
					});
				}
			}
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = button,
				mousePosition = to,
				clickCount = 1
			});
			yield return Flush();
		}

		private IEnumerator Flush()
		{
			while (_events.Count > 0)
			{
				yield return null;
			}
			yield return null;
			yield return null;
		}

		private void OnGUI()
		{
			if (_events.Count == 0 || _mod == null || !_mod.WarMap.Open)
			{
				return;
			}
			Event current = Event.current;
			try
			{
				while (_events.Count > 0)
				{
					Event.current = _events.Dequeue();
					_mod.HudForTest.DrawWarMap();
				}
				Event.current = new Event
				{
					type = EventType.Layout,
					mousePosition = current.mousePosition
				};
				_mod.HudForTest.DrawWarMap();
			}
			catch (Exception ex)
			{
				Log.Error("DRAG events: " + ex);
			}
			finally
			{
				Event.current = current;
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
			Log.Info("DRAG " + (ok ? "PASS" : "FAIL") + " " + what + ": " + detail);
		}

		private void Fail(string what, string detail)
		{
			Check(what, ok: false, detail);
		}

		private void Finish(bool quit, Settings s, bool god, int time)
		{
			GodView.TestMouse = null;
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
			Log.Info("DRAG DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
