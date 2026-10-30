using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FMOD;
using FMODUnity;
using FirstPerson.Core;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SettingsTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private readonly Queue<Event> _events = new Queue<Event>();

		private int _ignored;

		private int _lost;

		private bool _direct;

		private bool _eventsOn;

		private bool _layoutTurn;

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
			string file = Path.Combine(Log.Folder, "settings.txt");
			string players = (File.Exists(file) ? File.ReadAllText(file) : null);
			SettingsMenu menu = _mod.SettingsMenu;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			_mod.SetSettingsMenu(open: true);
			yield return new WaitForSecondsRealtime(0.14f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "set_map_unrolling.png"));
			yield return null;
			yield return new WaitForSecondsRealtime(0.5f);
			Check("map: the scroll has unrolled", menu.Unroll >= 1f);
			Check("map: the menu opens", menu.Open);
			Check("map: Esc is the menu's", EscIsOurs());
			Transform cover = _mod.transform.Find("FirstPersonSettingsBlocker");
			Check("map: a sheet covers the map", cover != null && cover.gameObject.activeInHierarchy);
			bool flag2 = false;
			try
			{
				flag2 = World.world.isOverUI();
			}
			catch
			{
			}
			Log.Info($"SET map: the game takes the mouse to be over its UI: {flag2} (mouse {Input.mousePosition}, screen {Screen.width}x{Screen.height})");
			for (int tab = 0; tab < SettingsMenu.Tabs.Length; tab++)
			{
				menu.Tab = tab;
				yield return new WaitForSecondsRealtime(0.4f);
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "set_map_" + SettingsMenu.Tabs[tab].ToLowerInvariant() + ".png"));
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.4f);
			Esc();
			yield return null;
			Check("map: Esc closes the menu", !menu.Open);
			bool flag3 = false;
			try
			{
				flag3 = ScrollWindow.isWindowActive();
			}
			catch
			{
			}
			Check("map: ...and opens no window of the game's", !flag3);
			Check("map: the sheet is lifted", cover == null || !cover.gameObject.activeInHierarchy);
			yield return new WaitForSecondsRealtime(0.4f);
			Check("map: the scroll has rolled up", menu.Unroll <= 0f);
			_mod.SetSettingsMenu(open: true);
			Settings s = _mod.Settings;
			Settings fresh = new Settings();
			for (int tab = 0; tab < SettingsMenu.Tabs.Length; tab++)
			{
				menu.Tab = tab;
				foreach (SettingsMenu.Row item in menu.RowsOf(tab))
				{
					if (item.Fixed)
					{
						int num = item.Chosen(s);
						menu.Pick(item, s, (num + 1) % item.Choices.Length);
						Check(item.Name + ": shown, not chosen (stays " + item.Choices[num] + ")", item.Chosen(s) == num);
						continue;
					}
					if (item.IsChoice)
					{
						int num2 = (item.Chosen(s) + 1) % item.Choices.Length;
						menu.Pick(item, s, num2);
						Check(item.Name + ": picks " + item.Choices[num2], item.Chosen(s) == num2);
						continue;
					}
					float num3 = item.Value(s);
					float num4 = item.FromSlider((item.ToSlider(num3) < 0.5f) ? 0.9f : 0.1f);
					menu.Slide(item, s, num4);
					Check(item.Name + ": slides " + item.Show(num3) + " to " + item.Show(num4), Mathf.Abs(item.Value(s) - num4) < 0.0001f);
				}
				if (menu.RowsOf(tab).Count > 0)
				{
					int count = menu.RowsOf(tab).FindAll((SettingsMenu.Row r) => string.IsNullOrEmpty(r.Help)).Count;
					int count2 = menu.RowsOf(tab).FindAll((SettingsMenu.Row r) => r.Heading != null).Count;
					Check($"{SettingsMenu.Tabs[tab]}: every row has an info text, in {count2} sections", count == 0 && count2 >= 1 && menu.RowsOf(tab)[0].Heading != null, $"{count} without");
				}
				yield return null;
				menu.Defaults(s);
				foreach (SettingsMenu.Row item2 in menu.RowsOf(tab))
				{
					bool ok = (item2.IsChoice ? (item2.Chosen(s) == item2.Chosen(fresh)) : (Mathf.Abs(item2.Value(s) - item2.Value(fresh)) < 0.0001f));
					Check(item2.Name + ": Defaults puts it back to " + (item2.IsChoice ? item2.Choices[item2.Chosen(fresh)] : item2.Show(item2.Value(fresh))), ok);
				}
				yield return null;
			}
			menu.Slide(Row("Field of view"), s, 90f);
			menu.Pick(Row("Palette filter"), s, 0);
			menu.Slide(Row("Music"), s, 0.5f);
			menu.Slide(Row("Length of a day"), s, 45f);
			_mod.SetSettingsMenu(open: false);
			Settings settings = Settings.Load();
			Check("saved: field of view 90", Mathf.Abs(settings.FieldOfView - 90f) < 0.01f);
			Check("saved: palette filter off", settings.Palette == 0);
			Check("saved: music at half", Mathf.Abs(settings.MusicVolume - 0.5f) < 0.01f);
			Check("saved: a 45-minute day", Mathf.Abs(settings.DayMinutes - 45f) < 0.01f);
			Check("saved: the menu has been seen (no more reminder)", settings.MenuSeen);
			s.FieldOfView = fresh.FieldOfView;
			s.Palette = fresh.Palette;
			s.MusicVolume = fresh.MusicVolume;
			s.DayMinutes = fresh.DayMinutes;
			Actor actor = null;
			foreach (WorldTile item3 in KingTest.GoodSites())
			{
				actor = KingTest.FoundKingdom(item3, "human", 8, 0);
				if (actor != null)
				{
					break;
				}
			}
			if (actor == null)
			{
				Log.Error("SET no village to take a villager from");
				Finish(quit, file, players);
				yield break;
			}
			ControllableUnit.setControllableCreature(actor);
			yield return new WaitForSecondsRealtime(2.5f);
			Check("fp: first person starts", _mod.IsFirstPerson);
			_mod.HintUntil = 0f;
			s.TimeMode = 1;
			_mod.ForceRelight();
			_mod.Craft.Open = true;
			_mod.SetSettingsMenu(open: true);
			Check("fp: opening it closes the crafting menu", !_mod.Craft.Open);
			yield return null;
			yield return null;
			Check("fp: the cursor is free", Cursor.lockState == CursorLockMode.None && Cursor.visible);
			Check("fp: no swings while it's open", PossessionHooks.BlockAttack);
			yield return new WaitForSecondsRealtime(0.4f);
			for (int tab = 0; tab < SettingsMenu.Tabs.Length; tab++)
			{
				menu.Tab = tab;
				menu.HelpRow = ((menu.RowsOf(tab).Count > 0) ? menu.RowsOf(tab)[0] : null);
				yield return new WaitForSecondsRealtime(0.4f);
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "set_fp_" + SettingsMenu.Tabs[tab].ToLowerInvariant() + ".png"));
				yield return null;
			}
			menu.HelpRow = null;
			yield return Dropdown(menu, s);
			yield return new WaitForSecondsRealtime(0.4f);
			Esc();
			yield return null;
			yield return null;
			Check("fp: Esc closes the menu", !menu.Open);
			Check("fp: ...and keeps the creature", ControllableUnit.isControllingUnit() && _mod.IsFirstPerson);
			Check("fp: the cursor is held again", Cursor.lockState == CursorLockMode.Locked);
			yield return Reach(s);
			Esc();
			yield return null;
			yield return null;
			Check("fp: Esc with the menu shut opens it", menu.Open && ControllableUnit.isControllingUnit() && _mod.IsFirstPerson);
			Esc();
			yield return null;
			yield return null;
			Check("fp: ...and Esc closes it again", !menu.Open && ControllableUnit.isControllingUnit());
			LetGo();
			yield return new WaitForSecondsRealtime(1f);
			Check("fp: G lets go of the creature", !ControllableUnit.isControllingUnit());
			Check("sound: the master volume is put back on leaving first person", MasterVolume() > 0.99f);
			s.MasterVolume = 1f;
			Finish(quit, file, players);
		}

		private IEnumerator Reach(Settings s)
		{
			float savedDraw = s.DrawDistance;
			float savedDetail = s.DetailDistance;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float across = Mathf.Sqrt((float)terrainForTest.Width * (float)terrainForTest.Width + (float)terrainForTest.Height * (float)terrainForTest.Height) + 40f;
			_mod.SetPitchDegrees(4f);
			float savedYaw = _mod.View.Yaw;
			int farThere = FaceFarThings();
			float[] array = new float[2] { 270f, 1200f };
			for (int i = 0; i < array.Length; i++)
			{
				float draw = (s.DrawDistance = array[i]);
				yield return new WaitForSecondsRealtime(0.5f);
				List<float> ms = new List<float>();
				for (int j = 0; j < 40; j++)
				{
					yield return null;
					ms.Add(_mod.FrameMs);
				}
				ms.Sort();
				string text = ((draw >= 1200f) ? "whole" : draw.ToString("0"));
				Log.Info($"SET draw distance {text}: land to {_mod.Renderer.FogDistance:F0} tiles (map across {across:F0}), objects to {_mod.Renderer.MaxDistance:F0}, {_mod.Renderer.FarPropsDrawn} far trees and buildings, {_mod.Renderer.FarActorsDrawn} far creatures, frame median {ms[ms.Count / 2]:F1} ms, worst {ms[ms.Count - 1]:F1} ms");
				_mod.SaveFrame("set_far_" + text + ".png");
			}
			Check("draw distance: the whole map reaches across it", Mathf.Abs(_mod.Renderer.FogDistance - across) < 1f);
			s.DrawDistance = 270f;
			array = new float[2] { 40f, 150f };
			for (int i = 0; i < array.Length; i++)
			{
				float draw = (s.DetailDistance = array[i]);
				yield return new WaitForSecondsRealtime(0.5f);
				List<float> ms = new List<float>();
				for (int j = 0; j < 40; j++)
				{
					yield return null;
					ms.Add(_mod.FrameMs);
				}
				ms.Sort();
				int farPropsDrawn = _mod.Renderer.FarPropsDrawn;
				int num = GpuFarProps();
				int farModelsDrawn = _mod.SceneForTest.FarModelsDrawn;
				Log.Info($"SET detail distance {draw:F0}: gathered to {_mod.Renderer.BillboardDistance:F0}, {farPropsDrawn} far trees and buildings as specks on the CPU, {num} on the GPU, {farModelsDrawn} as far models (far_models {s.FarModels}, light mode {LightMode.On}), frame median {ms[ms.Count / 2]:F1} ms, worst {ms[ms.Count - 1]:F1} ms");
				_mod.SaveFrame($"set_detail_{draw:F0}.png");
				Check($"detail distance {draw:F0}: things are gathered out to it", Mathf.Abs(_mod.Renderer.BillboardDistance - Mathf.Min(draw, _mod.Renderer.MaxDistance)) < 0.01f);
				if (draw < 100f && farThere > 0)
				{
					Check("detail distance: past it, trees and buildings are specks or far models", farPropsDrawn + num + farModelsDrawn > 0);
				}
				else if (draw < 100f)
				{
					Log.Info("SET detail distance: no tree or building 60-200 tiles off anywhere: specks not checked");
				}
			}
			s.MasterVolume = 0.5f;
			yield return new WaitForSecondsRealtime(0.3f);
			Check("sound: the master volume turns everything down", Mathf.Abs(MasterVolume() - 0.5f) < 0.01f);
			s.DrawDistance = savedDraw;
			s.DetailDistance = savedDetail;
			_mod.SetPitchDegrees(0f);
			_mod.SetYaw(savedYaw);
		}

		private int FaceFarThings()
		{
			int[] array = new int[12];
			ViewState view = _mod.View;
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple == null || simple.asset == null || simple.chopped || !simple.isAlive() || simple.current_tile == null)
				{
					continue;
				}
				float num = simple.current_tile.posV3.x - view.X;
				float num2 = simple.current_tile.posV3.y - view.Y;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				if (!(num3 < 60f) && !(num3 > 200f))
				{
					BuildingLook buildingLook;
					try
					{
						buildingLook = AssetStyle.Classify(simple.asset);
					}
					catch
					{
						continue;
					}
					if (buildingLook == BuildingLook.Tree || buildingLook == BuildingLook.House)
					{
						array[(int)(Mathf.Repeat(Mathf.Atan2(num2, num), MathF.PI * 2f) / (MathF.PI / 6f)) % 12]++;
					}
				}
			}
			int num4 = 0;
			for (int i = 1; i < 12; i++)
			{
				if (array[i] > array[num4])
				{
					num4 = i;
				}
			}
			if (array[num4] > 0)
			{
				_mod.SetYaw(((float)num4 + 0.5f) * MathF.PI / 6f);
			}
			Log.Info($"SET facing {array[num4]} trees and buildings 60-200 tiles off (sector {num4 * 30}-{num4 * 30 + 30} deg; was facing {view.Yaw * 57.29578f:F0} deg)");
			return array[num4];
		}

		private IEnumerator Dropdown(SettingsMenu menu, Settings s)
		{
			Hud hud = _mod.HudForTest;
			_eventsOn = true;
			SettingsMenu.Row row = Row("Language");
			int n = row.Choices.Length;
			Check("dropdown: Language is a dropdown of " + n + " (" + string.Join(", ", row.Choices) + ")", row.Dropdown && n >= 2);
			List<string> list = new List<string>();
			bool flag = true;
			for (int i = 0; i < n; i++)
			{
				if (row.Codes == null || row.Codes[i] == null)
				{
					continue;
				}
				bool bold;
				Font font = Lang.FaceFor(row.Choices[i], row.Codes[i], GameArt.Font, out bold);
				bool flag2 = font != null;
				if (font != null)
				{
					string text = row.Choices[i];
					foreach (char c in text)
					{
						if (!char.IsWhiteSpace(c) && !font.HasCharacter(c))
						{
							flag2 = false;
						}
					}
				}
				flag = flag && flag2;
				list.Add(row.Choices[i] + ": " + ((font != null) ? font.name : "none") + (flag2 ? "" : " (lacks letters)"));
			}
			Check("dropdown: each language's name has a font with its letters (" + string.Join("; ", list) + ")", flag);
			for (int k = 0; k < SettingsMenu.Tabs.Length; k++)
			{
				if (menu.RowsOf(k).Contains(row))
				{
					menu.Tab = k;
				}
			}
			menu.Scroll = Vector2.zero;
			yield return new WaitForSecondsRealtime(0.4f);
			if (!hud.DropButtonForTest(row.Name, out var button))
			{
				Check("dropdown: its button is drawn", ok: false);
				_eventsOn = false;
				yield break;
			}
			string lang = s.Language;
			int was = row.Chosen(s);
			yield return Press(button.center);
			if (menu.Drop != row && _lost > 0)
			{
				_direct = true;
				Log.Info($"SET dropdown: this copy's window can't take made events ({_lost} lost: no room on the screen while minimised): the window's own steps for them are driven directly from here on (Hud.DropEventForTest)");
				yield return Press(button.center);
			}
			else
			{
				Log.Info($"SET dropdown: made events reach the window ({_ignored} put right)");
			}
			Check("dropdown: a click opens its list", menu.Drop == row && menu.Open);
			yield return new WaitForSecondsRealtime(0.3f);
			int num = 0;
			for (int l = 0; l < n; l++)
			{
				if (hud.DropItemForTest(l, out var _))
				{
					num++;
				}
			}
			hud.DropItemForTest(0, out var first);
			Log.Info($"SET dropdown: button {button}, first choice {first}, {num} of {n} showing, lit {menu.DropLit}");
			Check($"dropdown: the list shows its choices ({num} of {n})", num == n && (first.y >= button.yMax - 1f || first.yMax <= button.y + 1f));
			hud.CaptureHudForTest(Path.Combine(Log.Folder, "set_dropdown_open.png"));
			for (int m = 0; m < 30; m++)
			{
				if (!hud.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
			Esc();
			yield return null;
			yield return null;
			Check("dropdown: Esc shuts the list and leaves F2 open", menu.Drop == null && menu.Open);
			if (!menu.Open)
			{
				_mod.SetSettingsMenu(open: true);
				yield return new WaitForSecondsRealtime(0.5f);
			}
			yield return Press(button.center);
			float page = menu.Scroll.y;
			yield return Send(new Event
			{
				type = EventType.ScrollWheel,
				mousePosition = first.center,
				delta = new Vector2(0f, 3f)
			});
			Check("dropdown: the wheel over its list leaves the page where it was", menu.Drop == row && Mathf.Abs(menu.Scroll.y - page) < 0.5f, $"page {page:F0} -> {menu.Scroll.y:F0}");
			yield return Press(new Vector2(button.x - button.width * 0.6f, button.center.y));
			Check("dropdown: a click beside it shuts the list unchanged", menu.Drop == null && menu.Open && row.Chosen(s) == was);
			yield return Press(button.center);
			yield return new WaitForSecondsRealtime(0.2f);
			int pick = ((was == n - 1) ? (n - 2) : (n - 1));
			List<string> before = new List<string>();
			foreach (SettingsMenu.Row item in menu.RowsOf(menu.Tab))
			{
				if (item != row)
				{
					before.Add(item.Name + "=" + (item.IsChoice ? item.Chosen(s).ToString() : item.Value(s).ToString("0.###")));
				}
			}
			if (hud.DropItemForTest(pick, out var r2))
			{
				yield return Press(r2.center);
				Check("dropdown: a click on " + row.Choices[pick] + " picks it and shuts the list", row.Chosen(s) == pick && menu.Drop == null, "chosen " + row.Choices[row.Chosen(s)] + ", list " + ((menu.Drop != null) ? "open" : "shut"));
				List<string> list2 = new List<string>();
				foreach (SettingsMenu.Row item2 in menu.RowsOf(menu.Tab))
				{
					if (item2 != row)
					{
						list2.Add(item2.Name + "=" + (item2.IsChoice ? item2.Chosen(s).ToString() : item2.Value(s).ToString("0.###")));
					}
				}
				Check("dropdown: ...and presses nothing under the list", string.Join(",", before) == string.Join(",", list2) && menu.Open);
			}
			else
			{
				Check("dropdown: choice " + pick + " shows", ok: false);
			}
			yield return new WaitForSecondsRealtime(0.3f);
			yield return Press(button.center);
			yield return new WaitForSecondsRealtime(0.2f);
			hud.CaptureHudForTest(Path.Combine(Log.Folder, "set_dropdown_" + s.Language + ".png"));
			for (int m = 0; m < 30; m++)
			{
				if (!hud.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
			int lit = menu.DropLit;
			int want = Mathf.Max(0, lit - 1);
			yield return Send(new Event
			{
				type = EventType.KeyDown,
				keyCode = KeyCode.UpArrow
			});
			Check("dropdown: the up arrow lights the choice above", menu.Drop == row && lit > 0 && menu.DropLit == want, string.Format("list {0}, lit {1} -> {2}", (menu.Drop != null) ? "open" : "shut", lit, menu.DropLit));
			yield return Send(new Event
			{
				type = EventType.KeyDown,
				keyCode = KeyCode.Return
			});
			Check("dropdown: Enter picks the lit choice and shuts the list", row.Chosen(s) == want && menu.Drop == null, $"chosen {row.Chosen(s)}");
			yield return Press(button.center);
			bool opened = menu.Drop == row;
			_mod.SetSettingsMenu(open: false);
			yield return null;
			_mod.SetSettingsMenu(open: true);
			Check("dropdown: closing F2 shuts the list (not open again with it)", opened && menu.Drop == null);
			_eventsOn = false;
			s.Language = lang;
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Press(Vector2 at)
		{
			yield return Send(new Event
			{
				type = EventType.MouseDown,
				button = 0,
				mousePosition = at,
				clickCount = 1
			});
			yield return Send(new Event
			{
				type = EventType.MouseUp,
				button = 0,
				mousePosition = at,
				clickCount = 1
			});
		}

		private IEnumerator Send(Event e)
		{
			if (_direct)
			{
				_mod.HudForTest.DropEventForTest(_mod.SettingsMenu, _mod.Settings, e);
				yield return null;
				yield return null;
			}
			else
			{
				_events.Enqueue(e);
				yield return Flush();
			}
		}

		private IEnumerator Flush()
		{
			for (int i = 0; i < 300; i++)
			{
				if (_events.Count <= 0)
				{
					break;
				}
				yield return null;
			}
			yield return null;
			yield return null;
		}

		private void OnGUI()
		{
			if (_mod == null || !_eventsOn || !_mod.SettingsMenu.Open || PacksUi.Open)
			{
				return;
			}
			Event current = Event.current;
			if (current.type != EventType.Layout)
			{
				return;
			}
			_layoutTurn = !_layoutTurn;
			if (_layoutTurn || _events.Count == 0)
			{
				_mod.HudForTest.DrawSettings(_mod.SettingsMenu, _mod.Settings);
				_layoutTurn = true;
				return;
			}
			try
			{
				Event obj = _events.Dequeue();
				EventType type = obj.type;
				Vector2 mousePosition = obj.mousePosition;
				Event.current = obj;
				if (Event.current.type != type)
				{
					_ignored++;
					Event.current.type = type;
				}
				if ((Event.current.mousePosition - mousePosition).sqrMagnitude > 1f)
				{
					_ignored++;
					Event.current.mousePosition = mousePosition;
				}
				if (Event.current.type != type || (Event.current.mousePosition - mousePosition).sqrMagnitude > 1f)
				{
					_lost++;
					return;
				}
				_mod.HudForTest.DrawSettings(_mod.SettingsMenu, _mod.Settings);
				if (type != EventType.KeyDown && (obj.mousePosition - mousePosition).sqrMagnitude > 1f)
				{
					_lost++;
					if (_lost == 1)
					{
						Log.Info($"SET dropdown: a made {type} at {mousePosition} reached the window at {obj.mousePosition}");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("SET events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}

		private static float MasterVolume()
		{
			try
			{
				if (RuntimeManager.CoreSystem.getMasterChannelGroup(out var channelgroup) == RESULT.OK && channelgroup.getVolume(out var volume) == RESULT.OK)
				{
					return volume;
				}
			}
			catch
			{
			}
			return -1f;
		}

		private SettingsMenu.Row Row(string name)
		{
			for (int i = 0; i < SettingsMenu.Tabs.Length; i++)
			{
				foreach (SettingsMenu.Row item in _mod.SettingsMenu.RowsOf(i))
				{
					if (item.Name == name)
					{
						return item;
					}
				}
			}
			throw new KeyNotFoundException(name);
		}

		private static bool EscIsOurs()
		{
			try
			{
				return HotkeyLibrary.cancel.just_pressed_action.Method.Name == "EscapePressed";
			}
			catch
			{
				return false;
			}
		}

		private static void Esc()
		{
			try
			{
				HotkeyLibrary.cancel.just_pressed_action(HotkeyLibrary.cancel);
			}
			catch (Exception ex)
			{
				Log.Error("SET Esc failed: " + ex);
			}
		}

		private static void LetGo()
		{
			try
			{
				HotkeyLibrary.control_unit.just_pressed_action(HotkeyLibrary.control_unit);
			}
			catch (Exception ex)
			{
				Log.Error("SET G failed: " + ex);
			}
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
			Log.Info("SET " + (ok ? "PASS " : "FAIL ") + what);
		}

		private void Check(string what, bool ok, string detail)
		{
			Check(ok ? what : (what + " (" + detail + ")"), ok);
		}

		private void Finish(bool quit, string file, string players)
		{
			try
			{
				_mod.SetSettingsMenu(open: false);
				if (players != null)
				{
					File.WriteAllText(file, players);
				}
				else if (File.Exists(file))
				{
					File.Delete(file);
				}
				_mod.Settings = Settings.Load();
			}
			catch (Exception ex)
			{
				Log.Error("SET could not put the settings back: " + ex.Message);
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"SET {_pass} passed, {_fail} failed");
			Log.Info("SET DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private int GpuFarProps()
		{
			try
			{
				GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
				if (gpuRendererForTest == null)
				{
					return 0;
				}
				FieldInfo field = typeof(GpuRenderer).GetField("_specks", BindingFlags.Instance | BindingFlags.NonPublic);
				return (field != null && field.GetValue(gpuRendererForTest) is GpuSpecks gpuSpecks) ? gpuSpecks.Props : 0;
			}
			catch
			{
				return 0;
			}
		}
	}
}
