using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SceneTest : CivilTest
	{
		private readonly Queue<Event> _events = new Queue<Event>();

		private readonly List<string> _toasts = new List<string>();

		private string _lastToast;

		private Actor _king;

		private readonly List<Actor> _warriors = new List<Actor>();

		private int _ignored;

		private bool _saidIgnored;

		private Vector2 SceneAtBefore;

		protected override string Prefix => "SCENE";

		private void Update()
		{
			string text = ((Mod != null) ? Mod.ToastText : null);
			if (text != null && text != _lastToast)
			{
				_toasts.Add(text);
			}
			_lastToast = text;
		}

		private bool Said(string part, int since)
		{
			for (int i = since; i < _toasts.Count; i++)
			{
				if (_toasts[i].Contains(part))
				{
					return true;
				}
			}
			return false;
		}

		protected override IEnumerator Body()
		{
			bool gpuWas = Mod.Settings.GpuRender;
			yield return Part("the king's scene", King());
			yield return Part("edge cases", Edges());
			yield return Part("drawn on the CPU", OnCpu());
			yield return Part("a captain and a soldier", Captain());
			Mod.Settings.GpuRender = gpuWas;
		}

		private IEnumerator King()
		{
			_king = Village("human", 17, 0);
			if (!Check("our village", _king != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			WorldTile current_tile = _king.current_tile;
			Actor foe = Village("orc", 10, 0, current_tile, 30f) ?? Village("orc", 10, 0);
			yield return Possess(_king);
			if (!Check("the king in first person", InControl(_king) && Royal.IsKing(_king)))
			{
				yield break;
			}
			if (foe != null)
			{
				try
				{
					Mod.Royal.DeclareWar(_king, foe.kingdom, Mod.Toast, Mod.PlayHorn);
				}
				catch (Exception ex)
				{
					Note("war: " + ex.Message);
				}
				Note("at war with the orcs: " + Royal.AtWar(_king.kingdom, foe.kingdom));
			}
			yield return CivilTest.Wait(2f);
			try
			{
				foreach (Actor unit in _king.kingdom.getUnits())
				{
					if (unit != null && unit != _king && unit.isAlive() && unit.isWarrior())
					{
						_warriors.Add(unit);
					}
				}
			}
			catch
			{
			}
			if (!Check("warriors to command", _warriors.Count >= 4, _warriors.Count + " warriors"))
			{
				yield break;
			}
			foreach (Actor warrior in _warriors)
			{
				try
				{
					warrior.makeWait(30f);
				}
				catch
				{
				}
			}
			WarMap map = Mod.WarMap;
			map.ClearFlags();
			map.Marked.Clear();
			Mod.ShowMapPage(MapTabs.Page.War);
			yield return CivilTest.Wait(1f);
			if (!Check("the war map opens", map.Open))
			{
				yield break;
			}
			Vector2 middle = (map.Centre = Middle(_warriors));
			map.Zoom = Mathf.Clamp(map.Span / 70f, map.ZoomMin, map.ZoomMax);
			yield return null;
			yield return CivilTest.Wait(0.3f);
			Vector2 body0 = _king.current_position;
			Rect sceneButtonRectForTest = Mod.HudForTest.SceneButtonRectForTest;
			Check("the map has a View scene button", sceneButtonRectForTest.width > 0f, sceneButtonRectForTest.ToString());
			yield return PressViewScene(MapTabs.Page.War, middle);
			SceneView sc = Mod.Scene;
			Check("View scene opens the scene", sc.On && sc.Page == MapTabs.Page.War, $"on {sc.On}, page {sc.Page}");
			Check("...over the middle of the map", (sc.At - middle).magnitude < 1.5f, $"at {sc.At}, map middle {middle}");
			int framesBefore = Mod.FramesShown;
			yield return CivilTest.Wait(1.5f);
			Check("its frames are drawn", Mod.FramesShown > framesBefore + 5 && Mod.SceneShownSet, $"{Mod.FramesShown - framesBefore} frames, shown {Mod.SceneShownSet}");
			ViewState view = Mod.Shown.View;
			float num = Mod.GroundAt(sc.At);
			Check("the picture is the scene's, high over the ground", view.Z - num > 10f && Mathf.Abs(view.Pitch + Mathf.Tan(GodTurn.Elevation(sc.Tip))) < 0.05f, $"camera at {view.X:F1},{view.Y:F1},{view.Z:F1}, ground {num:F1}, pitch {view.Pitch:F2}, shear {view.Shear}");
			Check("the war map stays open under it", map.Open);
			SceneTest sceneTest = this;
			bool ok = (_king.current_position - body0).magnitude < 1.5f;
			Vector2 pip = _king.current_position - body0;
			sceneTest.Check("your body stands where it was", ok, $"moved {pip.magnitude:F2}");
			Check("the HUD isn't up (nothing under a crosshair)", Mod.Picked == null);
			int num2 = 0;
			Vector2 pip2;
			foreach (Actor warrior2 in _warriors)
			{
				if (Mod.HudForTest.ScenePipForTest(warrior2, out pip, out pip2))
				{
					num2++;
				}
			}
			Check("the warriors' pips", num2 >= _warriors.Count * 3 / 4, $"{num2} of {_warriors.Count}, {Mod.HudForTest.ScenePipsForTest} pips in all");
			yield return Capture("scene_war.png");
			Vector2 pip4;
			Vector2 body2;
			Actor one = _warriors.Find((Actor w) => Mod.HudForTest.ScenePipForTest(w, out pip4, out body2));
			if (one != null && Mod.HudForTest.ScenePipForTest(one, out pip2, out var oneBody))
			{
				yield return Click(0, oneBody);
				Check("a click on a warrior marks him", map.Marked.Contains(one) && map.Marked.Count == 1, map.Marked.Count + " marked");
				yield return Click(0, oneBody);
				Check("a second click unmarks him", !map.Marked.Contains(one), map.Marked.Count + " marked");
			}
			else
			{
				Check("a warrior to click", ok: false);
			}
			Rect rect = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
			int inBox = 0;
			foreach (Actor warrior3 in _warriors)
			{
				if (Mod.HudForTest.ScenePipForTest(warrior3, out var pip3, out var body1))
				{
					rect = Rect.MinMaxRect(Mathf.Min(rect.xMin, Mathf.Min(pip3.x, body1.x)), Mathf.Min(rect.yMin, Mathf.Min(pip3.y, body1.y)), Mathf.Max(rect.xMax, Mathf.Max(pip3.x, body1.x)), Mathf.Max(rect.yMax, Mathf.Max(pip3.y, body1.y)));
					inBox++;
				}
			}
			if (inBox > 0)
			{
				float num3 = 12f * GameArt.P;
				yield return Drag(0, new Vector2(rect.xMin - num3, rect.yMin - num3), new Vector2(rect.xMax + num3, rect.yMax + num3));
				int count = map.MarkedOwn().Count;
				Check("a box marks the warriors in it", count >= inBox, $"{count} marked, {inBox} in the box");
			}
			else
			{
				Check("warriors to box", ok: false);
			}
			WorldTile spot = Spot(World.world.GetTile((int)middle.x, (int)middle.y), 0f, 9f) ?? Spot(World.world.GetTile((int)middle.x, (int)middle.y), MathF.PI, 9f);
			int sent = map.MarkedOwn().Count;
			WarFlag f1 = null;
			if (Check("open ground near the warriors", spot != null) && Mod.SceneProject(new Vector3((float)spot.x + 0.5f, (float)spot.y + 0.5f, Mod.GroundAt(P(spot))), out var gui))
			{
				yield return Click(1, gui, shift: true);
				f1 = map.FlagNumbered(1);
				Check("a Shift+right click on the ground plants flag 1", f1 != null && (f1.P - P(spot)).magnitude < 3f, (f1 != null) ? $"flag at {f1.Tile.x},{f1.Tile.y}, clicked {spot.x},{spot.y}" : ("no flag; " + Mod.ToastText));
				Check("...and the marked go to hold it", f1 != null && f1.Band.Count == sent && sent > 0, (f1 != null) ? (f1.Band.Count + " of " + sent) : "-");
				Note("said: " + Mod.ToastText);
				yield return Capture("scene_flag.png", 1.5f);
			}
			if (f1 != null && Mod.SceneProject(new Vector3(f1.P.x, f1.P.y, Mod.GroundAt(f1.P) + 0.05f), out var gui2))
			{
				map.Marked.Clear();
				yield return Click(0, gui2 + new Vector2(4f, -14f) * GameArt.P);
				Check("a click on the flag marks its band", map.Marked.Count == f1.Band.Count && map.Marked.SetEquals(f1.Band), $"{map.Marked.Count} marked, band {f1.Band.Count}");
				Mod.HudForTest.SceneHover = null;
				Mod.SceneFollowKey();
				Check("F follows the marked band", sc.FollowMarked, Mod.ToastText);
				yield return CivilTest.Wait(2f);
				Vector2 vector = Middle(new List<Actor>(map.Marked));
				Check("...the view keeps them in the middle", (sc.At - vector).magnitude < 2.5f, $"at {sc.At}, band {vector}");
			}
			else
			{
				Check("a flag in sight to click", ok: false);
			}
			Vector2 before = sc.At;
			sc.TestMove = new Vector2(1f, 0f);
			yield return CivilTest.Wait(0.5f);
			sc.TestMove = null;
			Check("WASD moves the view and lets go", !sc.Following && Vector2.Dot(sc.At - before, sc.Right) > 2f, $"moved {(sc.At - before).magnitude:F1}, following {sc.Following}");
			sc.TestTurn = new Vector2(1f, 0.6f);
			yield return CivilTest.Wait(0.7f);
			sc.TestTurn = null;
			yield return CivilTest.Wait(0.4f);
			Check("the arrows turn and tip it", Mathf.Abs(sc.Turn) > 0.3f && sc.Tip > 0.15f, $"turn {sc.Turn * 57.29578f:F0} deg, tip {sc.Tip:F2}");
			yield return Capture("scene_turned.png", 0.6f);
			Vector2 mouse = new Vector2((float)Screen.width * 0.66f, (float)Screen.height * 0.6f);
			if (Mod.SceneGround(mouse, out var under))
			{
				float zoom0 = sc.Zoom;
				yield return Wheel(-3f, mouse);
				yield return CivilTest.Wait(1f);
				Vector2 point;
				bool flag = Mod.SceneGround(mouse, out point);
				float num4 = (flag ? (point - under).magnitude : (-1f));
				Check("the wheel zooms in", sc.Zoom < zoom0 * 0.9f, $"zoom {zoom0:F1} -> {sc.Zoom:F1}");
				Check("...round the ground under the mouse", flag && num4 < 0.6f, $"{num4:F2} tiles off (turned {sc.Turn * 57.29578f:F0} deg, tipped {sc.Tip:F2})");
			}
			else
			{
				Check("ground under the mouse for the wheel", ok: false);
			}
			SceneAtBefore = sc.At;
			Vector2 from = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.55f);
			Vector2 to = from + new Vector2((float)Screen.width * 0.12f, (float)Screen.height * 0.08f);
			if (Mod.SceneGround(from, out var held))
			{
				yield return Drag(1, from, to);
				yield return CivilTest.Wait(0.6f);
				Vector2 point2;
				bool flag2 = Mod.SceneGround(to, out point2);
				float num5 = (flag2 ? (point2 - held).magnitude : (-1f));
				Check("a right drag carries the ground with the mouse", flag2 && num5 < 0.6f, $"{num5:F2} tiles off ({(to - from).magnitude:F0} px dragged, the view moved {(sc.At - SceneAtBefore).magnitude:F1} tiles)");
				Check("...and plants no flag", map.Flags.Count == 1, map.Flags.Count + " flags");
			}
			else
			{
				Check("ground to drag", ok: false);
			}
			try
			{
				RuntimeManager.StudioSystem.getListenerAttributes(0, out var attributes);
				Vector2 vector2 = new Vector2(attributes.position.x, attributes.position.y);
				Check("the listener is at the scene", (vector2 - sc.At).magnitude < 3f, $"ears {vector2}, scene {sc.At}, body {_king.current_position}");
			}
			catch (Exception ex2)
			{
				Note("listener: " + ex2.Message);
			}
			Vector2 lastAt = sc.At;
			Check("Esc is the scene's", Mod.SceneEscapeForTest());
			yield return null;
			yield return null;
			Check("Esc goes back to the map", !sc.On && map.Open, $"scene {sc.On}, map {map.Open}");
			Check("...centred where the scene looked", (map.Centre - lastAt).magnitude < 1.5f, $"map {map.Centre}, scene {lastAt}");
			yield return CivilTest.Wait(0.5f);
			yield return Capture("scene_back.png");
			if (f1 != null)
			{
				int tab = map.Tab;
				map.Tab = 1;
				yield return null;
				yield return null;
				if (Mod.HudForTest.SceneEyesForTest.TryGetValue("Flag 1 in 3D", out var value))
				{
					yield return Click(0, value.center);
					if (_ignored == 0)
					{
						Check("a flag's eye opens the scene on it", sc.On && (sc.At - f1.P).magnitude < 1.5f, $"at {sc.At}, flag {f1.P}");
					}
					else
					{
						Note("(the eye's click was ignored by the copy's GUI: not judged)");
					}
				}
				else
				{
					Check("the flag row has an eye", ok: false);
				}
				map.Tab = tab;
				if (!sc.On)
				{
					Mod.EnterScene(MapTabs.Page.War, f1.P);
				}
			}
			yield return CivilTest.Wait(0.8f);
			Check("the scene holds the game's action keys", PossessionHooks.SceneHoldsKeys == sc.On && sc.On, $"held {PossessionHooks.SceneHoldsKeys}, scene {sc.On}");
			Mod.MapKeyForTest();
			yield return null;
			yield return null;
			Check("M goes back to your eyes", !sc.On && !Mod.MapWindowOpen && Mod.IsFirstPerson, $"scene {sc.On}, map window {Mod.MapWindowOpen}");
			Check("...and gives the action keys back", !PossessionHooks.SceneHoldsKeys);
			Mod.ShowMapPage(MapTabs.Page.War);
			yield return CivilTest.Wait(0.5f);
			try
			{
				_king.setHealth(_king.getMaxHealth());
			}
			catch
			{
			}
			Mod.EnterScene(MapTabs.Page.War, middle + new Vector2(25f, 0f));
			yield return CivilTest.Wait(1f);
			int max = 100;
			try
			{
				max = _king.getMaxHealth();
				_king.attackedBy = null;
				_king.setHealth(_king.getHealth() - Mathf.Max(1, Mathf.RoundToInt((float)max * 0.04f)));
			}
			catch (Exception ex3)
			{
				Note("starve: " + ex3.Message);
			}
			yield return CivilTest.Wait(0.6f);
			Check("a small loss nobody dealt is let pass", sc.On && Time.unscaledTime - Mod.SceneHurtAt < 1f, $"scene {sc.On}, flashed {Time.unscaledTime - Mod.SceneHurtAt:F1} s ago, health {max}");
			try
			{
				_king.attackedBy = null;
				_king.setHealth(Mathf.Max(1, _king.getHealth() - Mathf.CeilToInt((float)max * 0.15f)));
			}
			catch
			{
			}
			float tb = Time.realtimeSinceStartup;
			while (sc.On && Time.realtimeSinceStartup - tb < 2f)
			{
				yield return null;
			}
			Check("a big one sends you back", !sc.On && Mod.ToastText != null && Mod.ToastText.Contains("You are hurt"), Mod.ToastText);
			try
			{
				_king.setHealth(_king.getMaxHealth());
			}
			catch
			{
			}
			Mod.ShowMapPage(MapTabs.Page.War);
			yield return CivilTest.Wait(0.5f);
			Mod.EnterScene(MapTabs.Page.War, middle + new Vector2(25f, 0f));
			yield return CivilTest.Wait(1f);
			Actor attackedBy = ((foe != null && foe.isAlive()) ? foe : _warriors[0]);
			Mod.ToastText = null;
			try
			{
				_king.attackedBy = attackedBy;
				_king.setHealth(Mathf.Max(1, _king.getHealth() - 4));
			}
			catch (Exception ex4)
			{
				Note("hurt: " + ex4.Message);
			}
			float t0 = Time.realtimeSinceStartup;
			while (sc.On && Time.realtimeSinceStartup - t0 < 2f)
			{
				yield return null;
			}
			Check("a blow puts you back in your body", !sc.On && !Mod.MapWindowOpen, $"scene {sc.On}, map window {Mod.MapWindowOpen}");
			Check("...and says so", Mod.ToastText != null && Mod.ToastText.Contains("You are struck"), Mod.ToastText);
			try
			{
				_king.setHealth(_king.getMaxHealth());
			}
			catch
			{
			}
		}

		private IEnumerator Edges()
		{
			if (_king == null || !InControl(_king))
			{
				Check("still the king", ok: false);
				yield break;
			}
			SceneView sc = Mod.Scene;
			WarMap map = Mod.WarMap;
			Vector2 middle = Middle(_warriors);
			Mod.ShowMapPage(MapTabs.Page.War);
			yield return CivilTest.Wait(0.4f);
			Mod.EnterScene(MapTabs.Page.War, middle);
			yield return CivilTest.Wait(0.6f);
			HotkeyAction hotkeyAction = null;
			try
			{
				hotkeyAction = HotkeyLibrary.cancel?.just_pressed_action;
			}
			catch
			{
			}
			if (hotkeyAction != null && hotkeyAction.Method.Name == "EscapePressed")
			{
				hotkeyAction(HotkeyLibrary.cancel);
				yield return null;
				yield return null;
				Check("the game's Esc goes back to the map", !sc.On && map.Open && !Mod.SettingsMenu.Open, $"scene {sc.On}, map {map.Open}, settings {Mod.SettingsMenu.Open}");
			}
			else
			{
				Note("(the game's Esc isn't the mod's: " + ((hotkeyAction != null) ? hotkeyAction.Method.Name : "none") + ")");
			}
			Mod.EnterScene(MapTabs.Page.War, middle);
			yield return CivilTest.Wait(0.6f);
			Vector2 c = new Vector2((float)Screen.width * 0.45f, (float)Screen.height * 0.5f);
			Mod.HudForTest.ScenePressForTest(0, c, c + new Vector2(120f, 60f));
			Check("a drag going on", Mod.HudForTest.SceneMouseBusyForTest);
			Mod.SceneEscapeForTest();
			yield return null;
			Mod.EnterScene(MapTabs.Page.War, middle);
			yield return CivilTest.Wait(0.4f);
			Check("the scene opens again with no drag left over", sc.On && !Mod.HudForTest.SceneMouseBusyForTest);
			Mod.HudForTest.ScenePressForTest(1, c, c + new Vector2(80f, 0f));
			Mod.SendMessage("OnApplicationFocus", false, SendMessageOptions.DontRequireReceiver);
			Check("losing the focus ends the drag", !Mod.HudForTest.SceneMouseBusyForTest);
			yield return CivilTest.Wait(1f);
			Check("...and holds no ground (no key pressed)", !sc.Pivot(out var _, out var mouse));
			Vector2 before = sc.At;
			sc.TestMove = new Vector2(0f, 1f);
			yield return CivilTest.Wait(0.5f);
			sc.TestMove = null;
			Check("WASD moves the view after it", Vector2.Dot(sc.At - before, sc.Ahead) > 2f, $"moved {(sc.At - before).magnitude:F1}");
			foreach (Actor warrior in _warriors)
			{
				if (warrior != null && warrior.isAlive())
				{
					map.Marked.Add(warrior);
				}
			}
			int flags = map.Flags.Count;
			Rect sceneKeysRectForTest = Mod.HudForTest.SceneKeysRectForTest;
			if (sceneKeysRectForTest.width > 0f)
			{
				yield return Click(1, sceneKeysRectForTest.center);
				Check("a right click on the keys' plate plants no flag", map.Flags.Count == flags && !Mod.HudForTest.SceneMouseBusyForTest, $"{map.Flags.Count} flags, {flags} before");
			}
			else
			{
				Check("the keys' plate drawn", ok: false);
			}
			Mod.HudForTest.SceneMouseForTest(1, c, c + new Vector2(90f, 40f));
			Vector2 jump = sc.At + sc.Ahead * 18f;
			jump = new Vector2(Mathf.Clamp(jump.x, 0f, WarMap.WorldWidth), Mathf.Clamp(jump.y, 0f, WarMap.WorldHeight));
			sc.JumpTo(jump);
			yield return CivilTest.Wait(0.6f);
			Check("a jump just after a drag lands", (sc.At - jump).magnitude < 0.5f, $"at {sc.At}, sent to {jump}");
			sc.JumpTo(Middle(_warriors));
			yield return CivilTest.Wait(0.8f);
			float zoomGoal = sc.ZoomGoal;
			Mod.HudForTest.SceneWheelForTest(0f, c);
			Check("a sideways scroll doesn't zoom", Mathf.Abs(sc.ZoomGoal - zoomGoal) < 0.0001f, $"goal {zoomGoal:F2} -> {sc.ZoomGoal:F2}");
			int count = map.Marked.Count;
			Check("a left press during a right drag is the drag's", Mod.HudForTest.SceneButtonsForTest(c, c + new Vector2(60f, 0f)) && map.Marked.Count == count, $"marked {count} -> {map.Marked.Count}");
			map.Marked.Clear();
			int num = 0;
			foreach (Actor warrior2 in _warriors)
			{
				if (Mod.HudForTest.ScenePipForTest(warrior2, out var pip, out mouse) && pip.x >= 0f && pip.y >= 0f && pip.x <= (float)Screen.width && pip.y <= (float)Screen.height)
				{
					num++;
				}
			}
			Vector2 vector = new Vector2((float)Screen.width * 0.3f, (float)Screen.height * 0.7f);
			Mod.HudForTest.SceneMouseForTest(0, vector, vector, shift: false, 2);
			Check("a double click marks all of yours in sight", map.MarkedOwn().Count >= num && num > 0, $"{map.MarkedOwn().Count} marked, {num} in sight");
			for (int i = 0; i < 30; i++)
			{
				Mod.HudForTest.SceneWheelForTest(-3f, c);
			}
			yield return CivilTest.Wait(1f);
			Check("the zoom stops at its nearest", Mathf.Abs(sc.Zoom - 5f) < 0.05f, $"zoom {sc.Zoom:F2}");
			for (int j = 0; j < 40; j++)
			{
				Mod.HudForTest.SceneWheelForTest(3f, c);
			}
			Mod.HudForTest.SceneWheelForTest(0f, c);
			yield return CivilTest.Wait(1.2f);
			Check("...and at its furthest", Mathf.Abs(sc.Zoom - Mod.SceneZoomFar) < 0.1f, $"zoom {sc.Zoom:F1}, far {Mod.SceneZoomFar:F0}");
			int num2 = 0;
			foreach (Actor item in Founded)
			{
				try
				{
					if (item != null && item.isAlive() && (item.current_position - sc.At).magnitude < 36f)
					{
						num2++;
					}
				}
				catch
				{
				}
			}
			Check("walkers where the scene watches are steered", Steering.Walkers > 0 || num2 == 0, $"{Steering.Walkers} steered last frame, {num2} of the village within reach of the middle, camera {(new Vector2(Mod.Shown.View.X, Mod.Shown.View.Y) - sc.At).magnitude:F0} tiles off");
			yield return Capture("scene_far.png", 0.6f);
			sc.ZoomGoal = 16f;
			yield return CivilTest.Wait(1f);
			List<Vector2> list = new List<Vector2>
			{
				sc.At + new Vector2(30f, 0f),
				sc.At + new Vector2(-50f, 0f),
				sc.At + new Vector2(0f, 80f)
			};
			List<Vector2> list2 = new List<Vector2>();
			Vector2 vector2 = sc.At;
			for (int k = 0; k < 4; k++)
			{
				Vector2? vector3 = Mod.NextSceneBattle(list, vector2, fresh: false);
				if (!vector3.HasValue)
				{
					break;
				}
				list2.Add(vector3.Value);
				vector2 = vector3.Value;
			}
			Check("Next battle goes round them all", list2.Count == 4 && list2[0] == list[0] && list2[1] == list[1] && list2[2] == list[2] && list2[3] != list2[2], string.Join(" / ", list2.ConvertAll((Vector2 v) => v.ToString("F0")).ToArray()));
			Mod.LeaveScene(toMap: false);
			yield return null;
			yield return null;
			yield return null;
			Check("after the scene the renderer is first person's", !Mod.Renderer.Cut.On && float.IsNaN(Mod.Renderer.PrecipX), $"cut {Mod.Renderer.Cut.On}, rain at {Mod.Renderer.PrecipX}");
			Actor actor = Mod.Picked as Actor;
			Check("...and nothing far off under the crosshair", actor == null || (actor.current_position - _king.current_position).magnitude < 40f, (actor != null) ? (WorldBoxMod.Describe(actor) + " " + (actor.current_position - _king.current_position).magnitude.ToString("F0") + " tiles off") : "none");
			Mod.ShowMapPage(MapTabs.Page.War);
			yield return CivilTest.Wait(0.3f);
			Mod.EnterScene(MapTabs.Page.War, middle);
			yield return CivilTest.Wait(0.5f);
			Mod.Royal.Toggle(_king, Mod.Toast);
			yield return null;
			yield return null;
			Check("another window ends the scene", !sc.On && Mod.Royal.Open, $"scene {sc.On}, council {Mod.Royal.Open}");
			Mod.Royal.Open = false;
			Mod.CloseMapWindow();
			yield return CivilTest.Wait(0.3f);
			Building house = null;
			try
			{
				foreach (Building building in _king.city.buildings)
				{
					if (building != null && building.isAlive() && HouseInterior.IsEnterable(building))
					{
						house = building;
						break;
					}
				}
			}
			catch
			{
			}
			if (house == null)
			{
				house = AddHouse("house_human_1", _king.city);
				yield return CivilTest.Wait(1f);
				if (house != null && !HouseInterior.IsEnterable(house))
				{
					house = null;
				}
			}
			if (house != null)
			{
				Put(_king, house.current_position + new Vector2(0f, -3f));
				yield return CivilTest.Wait(1f);
				Mod.EnterHouseForTest(house);
				yield return CivilTest.Wait(0.8f);
				Check("in a house", Mod.Interior.Inside, house.asset.id);
				Mod.ShowMapPage(MapTabs.Page.War);
				yield return CivilTest.Wait(0.3f);
				Mod.ToastText = null;
				bool opened = Mod.EnterScene(MapTabs.Page.War, middle);
				int frames = Mod.FramesShown;
				yield return CivilTest.Wait(1.5f);
				ViewState view = Mod.Shown.View;
				Check("indoors the scene shows", opened && sc.On && Mod.FramesShown > frames + 5 && Mod.SceneShownSet, $"opened {opened}, on {sc.On}, {Mod.FramesShown - frames} frames, toast {Mod.ToastText}");
				Check("...from the scene's camera over the battle", Mathf.Abs(view.Pitch + Mathf.Tan(GodTurn.Elevation(sc.Tip))) < 0.05f && (new Vector2(view.X, view.Y) - sc.At).magnitude < sc.Zoom * 6f, $"pitch {view.Pitch:F2}, camera at {view.X:F0},{view.Y:F0}, looking at {sc.At}");
				Check("...and you are still in the house", Mod.Interior.Inside && Mod.Interior.House == house, $"inside {Mod.Interior.Inside}");
				Vector2 atFirst = sc.At;
				sc.TestMove = new Vector2(1f, 0f);
				yield return CivilTest.Wait(0.6f);
				sc.TestMove = null;
				Check("...and steered from in there", (sc.At - atFirst).magnitude > 1f, $"moved {(sc.At - atFirst).magnitude:F1}");
				yield return Capture("scene_indoors.png");
				Mod.LeaveScene(toMap: false);
				frames = Mod.FramesShown;
				yield return CivilTest.Wait(0.8f);
				Check("back in the room after", !sc.On && Mod.Interior.Inside && Mod.FramesShown > frames + 3 && Mod.Shown.View.Shear < 0.01f, $"scene {sc.On}, inside {Mod.Interior.Inside}, {Mod.FramesShown - frames} frames, shear {Mod.Shown.View.Shear}");
				yield return Capture("scene_indoors_after.png");
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.8f);
			}
			else
			{
				Note("(no house to go into)");
			}
		}

		private IEnumerator OnCpu()
		{
			if (_king == null || !InControl(_king))
			{
				Check("still the king", ok: false);
				yield break;
			}
			Mod.Settings.GpuRender = false;
			bool besideWas = WorldBoxMod.BesideAllowed;
			WorldBoxMod.BesideAllowed = true;
			Mod.ShowMapPage(MapTabs.Page.War);
			yield return CivilTest.Wait(0.5f);
			Mod.EnterScene(MapTabs.Page.War, Middle(_warriors));
			int frames = Mod.FramesShown;
			yield return CivilTest.Wait(2f);
			Check("...drawn beside the game", Mod.DrawingBeside || !Mod.Settings.AsyncRender, $"beside {Mod.DrawingBeside}, async {Mod.Settings.AsyncRender}");
			Check("on the CPU the scene is drawn", Mod.Scene.On && Mod.FramesShown > frames + 5 && Mod.SceneShownSet, $"{Mod.FramesShown - frames} frames, {Mod.Renderer.Width}x{Mod.Renderer.Height}");
			ViewState view = Mod.Shown.View;
			Check("...from the scene's camera", Mathf.Abs(view.Pitch + Mathf.Tan(GodTurn.Elevation(Mod.Scene.Tip))) < 0.05f && view.Shear > 0.99f, $"pitch {view.Pitch:F2}, tip {Mod.Scene.Tip:F2}, shear {view.Shear}");
			int num = 0;
			foreach (Actor warrior in _warriors)
			{
				if (Mod.HudForTest.ScenePipForTest(warrior, out var _, out var _))
				{
					num++;
				}
			}
			Check("...with the warriors' pips", num > 0, num + " pips");
			yield return Capture("scene_cpu.png");
			Mod.LeaveScene(toMap: false);
			yield return CivilTest.Wait(0.5f);
			Check("back in first person on the CPU", Mod.IsFirstPerson && !Mod.Scene.On && Mod.Shown.View.Shear < 0.01f, $"shear {Mod.Shown.View.Shear}");
			WorldBoxMod.BesideAllowed = besideWas;
			Mod.Settings.GpuRender = true;
		}

		private IEnumerator Captain()
		{
			if (_warriors.Count < 2)
			{
				Check("soldiers to try", ok: false);
				yield break;
			}
			Actor captain = _warriors[0];
			City city = captain.city;
			try
			{
				if (!city.hasArmy())
				{
					World.world.armies.newArmy(captain, city);
				}
				if (city.army.getCaptain() != captain)
				{
					city.army.setCaptain(captain);
				}
			}
			catch (Exception ex)
			{
				Note("captain: " + ex.Message);
			}
			Mod.WarMap.ClearFlags();
			yield return Possess(captain);
			if (!Check("the captain in first person", InControl(captain) && Service.IsCaptain(captain)))
			{
				yield break;
			}
			Mod.ShowMapPage(MapTabs.Page.Orders);
			yield return CivilTest.Wait(1.2f);
			SoldierMap sm = Mod.SoldierMap;
			if (!Check("the captain's map opens", sm.Open && sm.Captaincy, $"open {sm.Open}, captaincy {sm.Captaincy}"))
			{
				yield break;
			}
			sm.Centre = captain.current_position + new Vector2(6f, 0f);
			yield return null;
			yield return CivilTest.Wait(0.3f);
			yield return PressViewScene(MapTabs.Page.Orders, sm.Centre);
			Check("View scene on the captain's map", Mod.Scene.On && Mod.Scene.Page == MapTabs.Page.Orders);
			yield return CivilTest.Wait(1.2f);
			WorldTile spot = Spot(captain.current_tile, MathF.PI / 2f, 8f) ?? Spot(captain.current_tile, -MathF.PI / 2f, 8f);
			if (Check("open ground for the captain's mark", spot != null) && Mod.SceneProject(new Vector3((float)spot.x + 0.5f, (float)spot.y + 0.5f, Mod.GroundAt(P(spot))), out var gui))
			{
				Vector2 under;
				bool seen = Mod.SceneGround(gui, out under);
				yield return Click(1, gui);
				WorldTile commandTile = Service.CommandTile;
				Check("a captain's right click sends his soldiers there", Service.Command == Service.CommandKind.March && commandTile != null && seen && Mathf.Abs((float)commandTile.x + 0.5f - under.x) <= 1.5f && Mathf.Abs((float)commandTile.y + 0.5f - under.y) <= 1.5f, string.Format("{0} at {1}, ground under the click {2:F1},{3:F1} (aimed at {4},{5}); {6}", Service.Command, (commandTile != null) ? (commandTile.x + "," + commandTile.y) : "none", under.x, under.y, spot.x, spot.y, Mod.ToastText));
				yield return Capture("scene_captain.png", 1.2f);
			}
			Actor soldier = null;
			try
			{
				foreach (Actor unit in captain.kingdom.getUnits())
				{
					if (unit != null && unit != captain && unit.isAlive() && unit.city == captain.city && Service.IsSoldier(unit) && !Service.IsCaptain(unit))
					{
						soldier = unit;
						break;
					}
				}
			}
			catch
			{
			}
			if (!Check("a plain soldier to take over", soldier != null))
			{
				yield break;
			}
			yield return Possess(soldier);
			Check("another body ends the scene", !Mod.Scene.On);
			if (!Check("a plain soldier in first person", InControl(soldier) && Service.IsSoldier(soldier) && !Service.IsCaptain(soldier), $"in control {InControl(soldier)}, soldier {Service.IsSoldier(soldier)}, captain {Service.IsCaptain(soldier)}"))
			{
				yield break;
			}
			Mod.ShowMapPage(MapTabs.Page.Orders);
			yield return CivilTest.Wait(1f);
			if (Check("the soldier's map opens", sm.Open && !sm.Captaincy))
			{
				yield return null;
				Mod.EnterScene(MapTabs.Page.Orders, soldier.current_position);
				yield return CivilTest.Wait(1f);
				WorldTile before = Service.CommandTile;
				int said = _toasts.Count;
				yield return Click(1, new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.6f));
				Check("a soldier's right click gives no order", Service.CommandTile == before && Said("Only a captain", said), string.Join(" / ", _toasts.GetRange(said, _toasts.Count - said).ToArray()));
				yield return Capture("scene_soldier.png");
				Mod.LeaveScene(toMap: false);
				Mod.ShowMapPage(MapTabs.Page.Orders);
				yield return CivilTest.Wait(0.3f);
				Mod.EnterScene(MapTabs.Page.Orders, soldier.current_position);
				yield return CivilTest.Wait(0.6f);
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch (Exception ex2)
				{
					Note("let go: " + ex2.Message);
				}
				float t0 = Time.realtimeSinceStartup;
				while (Mod.IsFirstPerson && Time.realtimeSinceStartup - t0 < 3f)
				{
					yield return null;
				}
				Check("letting go of the body ends the scene", !Mod.IsFirstPerson && !Mod.Scene.On && !Mod.Renderer.Cut.On, $"first person {Mod.IsFirstPerson}, scene {Mod.Scene.On}");
			}
		}

		private static Vector2 Middle(List<Actor> who)
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			foreach (Actor item in who)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						zero += item.current_position;
						num++;
					}
				}
				catch
				{
				}
			}
			if (num <= 0)
			{
				return Vector2.zero;
			}
			return zero / num;
		}

		private static Vector2 P(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static WorldTile Spot(WorldTile from, float angle, float distance)
		{
			if (from == null)
			{
				return null;
			}
			for (float num = distance; num < distance + 12f; num += 1.5f)
			{
				for (int i = 0; i < 9; i++)
				{
					float f = angle + (float)(((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2)) * 0.17f;
					WorldTile tile = World.world.GetTile(from.x + Mathf.RoundToInt(Mathf.Cos(f) * num), from.y + Mathf.RoundToInt(Mathf.Sin(f) * num));
					if (tile == null || !Royal.Standable(tile))
					{
						continue;
					}
					try
					{
						if (!tile.isSameIsland(from))
						{
							continue;
						}
					}
					catch
					{
						continue;
					}
					bool flag = true;
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							flag = Royal.Standable(World.world.GetTile(tile.x + k, tile.y + j));
						}
					}
					if (flag)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private IEnumerator Click(int button, Vector2 at, bool shift = false)
		{
			return Drag(button, at, at, shift);
		}

		private IEnumerator Drag(int button, Vector2 from, Vector2 to, bool shift = false)
		{
			_ignored = 0;
			EventModifiers modifiers = (shift ? EventModifiers.Shift : EventModifiers.None);
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = button,
				mousePosition = from,
				clickCount = 1,
				modifiers = modifiers
			});
			if (to != from)
			{
				for (int i = 1; i <= 10; i++)
				{
					_events.Enqueue(new Event
					{
						type = EventType.MouseDrag,
						button = button,
						mousePosition = Vector2.Lerp(from, to, (float)i / 10f),
						modifiers = modifiers
					});
				}
			}
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = button,
				mousePosition = to,
				clickCount = 1,
				modifiers = modifiers
			});
			yield return Flush();
			if (_ignored != 0)
			{
				if (!_saidIgnored)
				{
					Note("(the copy's GUI ignores made events: the scene's mouse steps are driven directly)");
				}
				_saidIgnored = true;
				if (Mod.Scene.On)
				{
					Mod.HudForTest.SceneMouseForTest(button, from, to, shift);
					yield return null;
					yield return null;
				}
			}
		}

		private IEnumerator Wheel(float delta, Vector2 at)
		{
			_ignored = 0;
			_events.Enqueue(new Event
			{
				type = EventType.ScrollWheel,
				delta = new Vector2(0f, delta),
				mousePosition = at
			});
			yield return Flush();
			if (_ignored > 0)
			{
				Mod.HudForTest.SceneWheelForTest(delta, at);
			}
		}

		private IEnumerator PressViewScene(MapTabs.Page page, Vector2 centre)
		{
			yield return Click(0, Mod.HudForTest.SceneButtonRectForTest.center);
			if (_ignored > 0 && !Mod.Scene.On)
			{
				Mod.EnterScene(page, centre);
			}
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

		private static void Put(Actor a, Vector2 at)
		{
			try
			{
				a.current_position = at;
				a.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private IEnumerator Capture(string name, float wait = 0.5f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("captured " + name);
		}

		private void OnGUI()
		{
			if (Mod == null || _events.Count == 0)
			{
				return;
			}
			Event current = Event.current;
			try
			{
				Event.current = _events.Dequeue();
				if (Event.current.type == EventType.Ignore)
				{
					_ignored++;
				}
				Draw();
			}
			catch (Exception ex)
			{
				Log.Error("SCENE events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}

		private void Draw()
		{
			if (Mod.Scene.On)
			{
				Mod.HudForTest.DrawScene();
			}
			else if (Mod.WarMap.Open)
			{
				Mod.HudForTest.DrawWarMap();
			}
			else if (Mod.SoldierMap.Open)
			{
				Mod.HudForTest.DrawSoldierMap();
			}
		}
	}
}
