using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MenusTest : CivilTest
	{
		private Actor _host;

		private Actor _king;

		private Actor _other;

		protected override string Prefix => "MENU";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			CrownOffer.AutoAnswer = true;
			_king = Village("human", 8, 0);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _king && item.isAdult() && !Service.IsSoldier(item))
				{
					if (_host == null)
					{
						_host = item;
					}
					else if (_other == null)
					{
						_other = item;
						break;
					}
				}
			}
			if (_host == null)
			{
				_host = CivilTest.SpawnNear("human", _king.current_position, 3f);
			}
			if (_other == null)
			{
				_other = CivilTest.SpawnNear("human", _king.current_position, 3f);
			}
			yield return CivilTest.Wait(0.5f);
			if (!Check("two villagers", _host != null && _other != null && _host != _other, $"founded {Founded.Count}"))
			{
				yield break;
			}
			yield return Possess(_host);
			if (Check("first person in a villager", InControl(_host)))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Part("old keys", OldKeys());
				yield return Part("map pages", MapPages());
				yield return Part("action wheel", ActionWheel());
				yield return Part("tool wheel", ToolWheel());
				yield return Part("hotbar put away", HotbarStow());
				yield return Part("help", HelpWindow());
				yield return Part("settings", SettingsTabs());
				yield return Part("run button", RunButton());
				yield return Part("council page", CouncilPage());
				PlayInput.TestReleaseAll();
				CrownOffer.AutoAnswer = false;
			}
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.3f);
			H.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!H.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}

		private IEnumerator Tap(KeyCode k)
		{
			PlayInput.TestPress(k);
			yield return null;
			yield return null;
			PlayInput.TestRelease(k);
			yield return null;
			yield return null;
		}

		private IEnumerator OldKeys()
		{
			Settings s = Mod.Settings;
			int res = s.Resolution;
			int pal = s.Palette;
			int dark = s.DarkFantasy;
			float eye = s.EyeHeight;
			float draw = s.DrawDistance;
			KeyCode[] array = new KeyCode[11]
			{
				KeyCode.F5,
				KeyCode.F6,
				KeyCode.F8,
				KeyCode.F10,
				KeyCode.PageUp,
				KeyCode.K,
				KeyCode.U,
				KeyCode.H,
				KeyCode.L,
				KeyCode.O,
				KeyCode.J
			};
			foreach (KeyCode k in array)
			{
				yield return Tap(k);
			}
			yield return CivilTest.Wait(0.3f);
			Check("F5 F6 F8 F10 PgUp change nothing", s.Resolution == res && s.Palette == pal && s.DarkFantasy == dark && Mathf.Approximately(s.EyeHeight, eye) && Mathf.Approximately(s.DrawDistance, draw), $"res {s.Resolution}/{res}, palette {s.Palette}/{pal}, dark {s.DarkFantasy}/{dark}, eye {s.EyeHeight}/{eye}, draw {s.DrawDistance}/{draw}");
			Check("K U H L O J open or change nothing", !Mod.MapWindowOpen && !Mod.Royal.Open && !WorldNews.Open && !Mod.Holstered && !Mod.Compass.Out && !Torches.Out && Mod.Host == _host, $"map {Mod.MapWindowOpen}, council {Mod.Royal.Open}, news {WorldNews.Open}, holstered {Mod.Holstered}, compass {Mod.Compass.Out}, torch {Torches.Out}");
		}

		private void Aim()
		{
			Vector2 vector = _other.current_position - _host.current_position;
			if (vector.sqrMagnitude > 0.01f)
			{
				Mod.LookForTest(Mathf.Atan2(vector.y, vector.x), -6f);
			}
		}

		private void NoTalk()
		{
			if (Mod.Converse.Open)
			{
				Note("a talk was open: " + Mod.Converse.With);
				Mod.EndConversation();
			}
		}

		private IEnumerator MapPages()
		{
			NoTalk();
			Mod.CloseMapWindow();
			MapTabs.Last = null;
			Check("a villager has News but no Council page", Mod.HasMapPage(MapTabs.Page.News) && !Mod.HasMapPage(MapTabs.Page.Council));
			Mod.MapKeyForTest();
			yield return null;
			Check("M opens the Map page", Journal.Open && Mod.MapPageShown == MapTabs.Page.Map);
			Mod.WantMapPage(MapTabs.Page.News);
			yield return CivilTest.Wait(0.2f);
			yield return Shot("menu_news.png");
			Check("the News tab shows the news in the same window", WorldNews.Open && !Journal.Open && Mod.MapPageShown == MapTabs.Page.News && H.MapWindowRectForTest.height > 100f, $"news {WorldNews.Open}, journal {Journal.Open}, shown {Mod.MapPageShown}");
			Mod.MapKeyForTest();
			Check("M closes it, news and all", !Mod.MapWindowOpen && !WorldNews.Open);
			Mod.MapKeyForTest();
			Check("M opens again on the News page", WorldNews.Open, Mod.MapPageShown.ToString());
			Mod.WantMapPage(MapTabs.Page.Family);
			yield return CivilTest.Wait(0.2f);
			Check("...and Family takes its place", Mod.FamilyMap.Open && !WorldNews.Open);
			Mod.CloseMapWindow();
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator ActionWheel()
		{
			NoTalk();
			Mod.CloseMapWindow();
			CivilTest.StandBefore(_other, _host, 1.4f);
			string raw = null;
			string shown = null;
			for (int i = 0; i < 120; i++)
			{
				yield return null;
				CivilTest.StandBefore(_other, _host, 1.4f);
				Aim();
				if (Mod.Talk.Candidate == _other && Mod.Prompt != null && Mod.Prompt.Contains("E:"))
				{
					raw = Mod.Prompt;
					shown = Mod.ShownPrompt(raw);
					break;
				}
			}
			string text = "picked " + ((Mod.Picked != null) ? (Mod.Picked.GetType().Name + " " + ((Mod.Picked is Actor a) ? WorldBoxMod.Describe(a) : Mod.Picked.ToString())) : "nothing") + ", candidate " + ((Mod.Talk.Candidate != null) ? WorldBoxMod.Describe(Mod.Talk.Candidate) : "none") + ", " + $"apart {(_other.current_position - _host.current_position).magnitude:F2}, prompt '{Mod.Prompt}', busy {Mod.TalkBusy}, menu {Mod.AnyMenuOpen}, alive {_other.isAlive()}";
			if (!Check("someone to talk to under the crosshair", Mod.Talk.Candidate == _other, raw ?? text))
			{
				yield break;
			}
			Check("the prompt shows E and Hold E:More, not F, Q or =", shown != null && shown.StartsWith("E:", StringComparison.Ordinal) && shown.Contains("Hold E:More") && !shown.Contains("F:") && !shown.Contains("Q:") && !shown.Contains("=:"), "raw '" + raw + "', shown '" + shown + "'");
			int num = Mod.ActionsForTest();
			string text2 = string.Join(", ", Mod.ActionWheel.Labels, 0, Mod.ActionWheel.Count);
			Check("the wheel holds talk, insult, steal and star", num >= 4 && text2.Contains("Insult") && text2.Contains("Steal") && (text2.Contains("Star") || text2.Contains("Unstar")), text2);
			yield return Tap(KeyCode.E);
			for (int i = 0; i < 30; i++)
			{
				if (Mod.Converse.Open)
				{
					break;
				}
				yield return null;
			}
			Check("a tap of E talks", Mod.Converse.Open);
			if (Mod.Converse.Open)
			{
				Mod.EndConversation();
			}
			yield return CivilTest.Wait(0.6f);
			for (int i = 0; i < 60; i++)
			{
				if (Mod.Talk.Candidate == _other && !Mod.TalkBusy)
				{
					break;
				}
				CivilTest.StandBefore(_other, _host, 1.4f);
				Aim();
				yield return null;
			}
			int insults = Mod.Talk.Insults;
			CivilTest.StandBefore(_other, _host, 1.4f);
			Aim();
			yield return null;
			PlayInput.TestPress(KeyCode.E);
			yield return CivilTest.Wait(0.45f);
			Check("held, E brings up the wheel", Mod.ActionWheelOpen && !Mod.Converse.Open);
			yield return Shot("menu_actionwheel.png");
			int slice = Array.IndexOf(Mod.ActionWheel.Labels, "Insult");
			Mod.ActionWheel.PointForTest(slice);
			PlayInput.TestRelease(KeyCode.E);
			yield return CivilTest.Wait(0.3f);
			Check("letting go takes Insult, and no talk opens", Mod.Talk.Insults == insults + 1 && !Mod.Converse.Open && !Mod.ActionWheelOpen, $"insults {Mod.Talk.Insults - insults}, talk {Mod.Converse.Open}");
			yield return CivilTest.Wait(1.4f);
			bool was = false;
			try
			{
				was = _other.isFavorite();
			}
			catch
			{
			}
			CivilTest.StandBefore(_other, _host, 1.4f);
			Aim();
			yield return null;
			PlayInput.TestPress(KeyCode.E);
			yield return CivilTest.Wait(0.45f);
			int slice2 = Array.FindIndex(Mod.ActionWheel.Labels, (string l) => l == "Star" || l == "Unstar");
			Mod.ActionWheel.PointForTest(slice2);
			PlayInput.TestRelease(KeyCode.E);
			yield return CivilTest.Wait(0.3f);
			bool flag = was;
			try
			{
				flag = _other.isFavorite();
			}
			catch
			{
			}
			Check("Star on the wheel stars them", flag != was);
			if (flag != was)
			{
				Mod.StarCreature(_other);
			}
			insults = Mod.Talk.Insults;
			CivilTest.StandBefore(_other, _host, 1.4f);
			Aim();
			yield return null;
			PlayInput.TestPress(KeyCode.E);
			yield return CivilTest.Wait(0.45f);
			Mod.ActionWheel.PointForTest(-1);
			PlayInput.TestRelease(KeyCode.E);
			yield return CivilTest.Wait(0.3f);
			Check("let go on Cancel, nothing happens", !Mod.Converse.Open && Mod.Talk.Insults == insults && !Mod.ActionWheelOpen);
		}

		private IEnumerator ToolWheel()
		{
			NoTalk();
			_other.current_position += new Vector2(6f, 6f);
			yield return CivilTest.Wait(0.3f);
			Mod.LastTool = null;
			yield return Tap(KeyCode.Q);
			Check("a tap of Q with no tool yet says to hold it", (Mod.ToastText ?? "").Contains("Hold Q"), Mod.ToastText);
			PlayInput.TestPress(KeyCode.Q);
			yield return CivilTest.Wait(0.45f);
			string text = string.Join(", ", Mod.ToolWheel.Labels, 0, Mod.ToolWheel.Count);
			Check("held, Q brings up the tools", Mod.ToolWheelOpen && text.Contains("Torch") && text.Contains("ompass") && text.Contains("Eat") && text.Contains("Bandage"), text);
			yield return Shot("menu_toolwheel.png");
			int slice = Array.FindIndex(Mod.ToolWheel.Labels, (string l) => l?.Contains("ompass") ?? false);
			Mod.ToolWheel.PointForTest(slice);
			PlayInput.TestRelease(KeyCode.Q);
			yield return CivilTest.Wait(0.6f);
			Check("Compass on the wheel takes it out", Mod.Compass.Out && Mod.LastTool == WorldBoxMod.Tool.Compass, $"out {Mod.Compass.Out}, last {Mod.LastTool}");
			yield return Tap(KeyCode.Q);
			yield return CivilTest.Wait(0.6f);
			Check("a tap of Q puts the last tool away again", !Mod.Compass.Out);
			yield return Tap(KeyCode.Q);
			yield return CivilTest.Wait(0.6f);
			Check("...and another takes it out", Mod.Compass.Out);
			yield return Tap(KeyCode.Q);
			yield return CivilTest.Wait(0.4f);
		}

		private IEnumerator HotbarStow()
		{
			int slot = Hotbar.Chosen(_host);
			Mod.Holstered = false;
			if (string.IsNullOrEmpty(Hotbar.InHand(_host)))
			{
				WorldBoxMod.HotbarKeyForTest = slot;
				WorldBoxMod.HotbarKeyFrameForTest = Time.frameCount + 1;
				yield return null;
				yield return null;
				Check("an empty hand's number puts nothing away", !Mod.Holstered);
				try
				{
					_host.addToInventory("wood", 3);
				}
				catch (Exception ex)
				{
					Note("wood: " + ex.Message);
				}
				yield return CivilTest.Wait(0.3f);
				for (int i = 0; i < Hotbar.Size(_host); i++)
				{
					Hotbar.Choose(_host, i, wheel: false);
					if (!string.IsNullOrEmpty(Hotbar.InHand(_host)))
					{
						slot = i;
						break;
					}
				}
				yield return null;
			}
			if (Check("something in hand", !string.IsNullOrEmpty(Hotbar.InHand(_host)), Hotbar.InHand(_host)))
			{
				WorldBoxMod.HotbarKeyForTest = slot;
				WorldBoxMod.HotbarKeyFrameForTest = Time.frameCount + 1;
				yield return null;
				yield return null;
				Check("the held slot's number puts it away", Mod.Holstered);
				WorldBoxMod.HotbarKeyForTest = slot;
				WorldBoxMod.HotbarKeyFrameForTest = Time.frameCount + 1;
				yield return null;
				yield return null;
				Check("...and again takes it up", !Mod.Holstered);
				WorldBoxMod.HotbarKeyForTest = -1;
			}
		}

		private IEnumerator HelpWindow()
		{
			NoTalk();
			yield return Tap(KeyCode.F1);
			Check("F1 opens the help window", Help.Open && Mod.AnyMenuOpen);
			Help.Page = 0;
			Help.Section[0] = 0;
			yield return Shot("menu_help_keys.png");
			Check("it is drawn", H.HelpRectForTest.width > 100f, H.HelpRectForTest.ToString());
			Help.Page = 1;
			Help.Section[1] = Array.FindIndex(Help.Guides, (Help.Part g) => g.Name == "Your own village");
			yield return Shot("menu_help_village.png");
			Help.Page = 0;
			int num = 0;
			Help.Part[] keys = Help.Keys;
			foreach (Help.Part part in keys)
			{
				num += part.Rows.Length;
			}
			Check("every section has rows or lines", Array.TrueForAll(Help.Keys, (Help.Part p) => p.Rows != null && p.Rows.Length != 0) && Array.TrueForAll(Help.Guides, (Help.Part g) => g.Lines != null && g.Lines.Length != 0), num + " keys");
			Esc();
			yield return null;
			Check("Esc closes it", !Help.Open);
			Mod.SetSettingsMenu(open: true);
			Mod.AskHelp();
			yield return CivilTest.Wait(0.2f);
			Check("Help under the settings window opens it in their place", Help.Open && !Mod.SettingsMenu.Open);
			yield return Tap(KeyCode.F1);
			Check("F1 closes it", !Help.Open);
		}

		private IEnumerator SettingsTabs()
		{
			Settings s = Mod.Settings;
			Check("eight tabs, Graphics first, no Keys", SettingsMenu.Tabs.Length == 8 && SettingsMenu.Tabs[0] == "Graphics" && Array.IndexOf(SettingsMenu.Tabs, "Keys") < 0, string.Join(",", SettingsMenu.Tabs));
			Mod.SetSettingsMenu(open: true);
			SettingsMenu menu = Mod.SettingsMenu;
			menu.Tab = 0;
			menu.Scroll = Vector2.zero;
			yield return Shot("menu_settings_graphics.png");
			SettingsMenu.Row row = menu.RowsOf(0).Find((SettingsMenu.Row r) => r.Name == "Quality");
			Check("Graphics starts with Quality, with the frame rate rows on it", row != null && menu.RowsOf(0).Exists((SettingsMenu.Row r) => r.Name == "Frame rate cap") && menu.RowsOf(0).Exists((SettingsMenu.Row r) => r.Name == "Show frame rate"));
			Check("the Map rows are on Game now", menu.RowsOf(2).Exists((SettingsMenu.Row r) => r.Name == "Minimap") && menu.RowsOf(1).Exists((SettingsMenu.Row r) => r.Name == "Mouse sensitivity"));
			float drawDistance = s.DrawDistance;
			float detailDistance = s.DetailDistance;
			float creatureDistance = s.CreatureDistance;
			float shadowDistance = s.ShadowDistance;
			float biomeParticles = s.BiomeParticles;
			int shadows = s.Shadows;
			int pixelDistance = s.PixelDistance;
			int windowView = s.WindowView;
			int farModels = s.FarModels;
			bool lensFlare = s.LensFlare;
			if (row != null)
			{
				menu.Pick(row, s, 0);
				Check("Low keeps far trees and buildings as sprites", SettingsMenu.QualityOf(s) == 0 && s.FarModels == 0, $"quality {SettingsMenu.QualityOf(s)}, far models {s.FarModels}");
				menu.Pick(row, s, 2);
				Check("High sets the distances and shadows", SettingsMenu.QualityOf(s) == 2 && s.Shadows == 3 && Mathf.Approximately(s.DrawDistance, 390f) && s.FarModels == 1, $"quality {SettingsMenu.QualityOf(s)}, shadows {s.Shadows}, draw {s.DrawDistance}, far models {s.FarModels}");
				s.DrawDistance = 500f;
				Check("changing one row makes it Custom", SettingsMenu.QualityOf(s) == 4);
				menu.Pick(row, s, 1);
				Check("Medium is a new player's defaults", SettingsMenu.QualityOf(new Settings()) == 1 && SettingsMenu.QualityOf(s) == 1);
			}
			s.DrawDistance = drawDistance;
			s.DetailDistance = detailDistance;
			s.CreatureDistance = creatureDistance;
			s.ShadowDistance = shadowDistance;
			s.BiomeParticles = biomeParticles;
			s.Shadows = shadows;
			s.PixelDistance = pixelDistance;
			s.WindowView = windowView;
			s.LensFlare = lensFlare;
			s.FarModels = farModels;
			bool fpsWas = s.ShowFps;
			s.ShowFps = true;
			Mod.SetSettingsMenu(open: false);
			yield return Shot("menu_fps.png");
			s.ShowFps = fpsWas;
			s.Save();
		}

		private IEnumerator RunButton()
		{
			Check("the settings offer a run in first person", Mod.CanRunButton && Mod.RunButtonName == "Start a run", Mod.RunButtonName);
			Mod.SetSettingsMenu(open: true);
			yield return Shot("menu_settings_buttons.png");
			Mod.AskRun();
			for (int i = 0; i < 30; i++)
			{
				if (Run.LockInOpen)
				{
					break;
				}
				yield return null;
			}
			Check("Start a run closes the settings and opens the run's window", Run.LockInOpen && !Mod.SettingsMenu.Open, $"lock-in {Run.LockInOpen}, settings {Mod.SettingsMenu.Open}");
			Run.LockInOpen = false;
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator CouncilPage()
		{
			yield return Possess(_king);
			if (Check("first person in the king", InControl(_king)))
			{
				Check("a king has the Council page", Mod.HasMapPage(MapTabs.Page.Council) && Mod.HasMapPage(MapTabs.Page.War));
				Mod.CloseMapWindow();
				Mod.ShowMapPage(MapTabs.Page.Council);
				yield return CivilTest.Wait(0.3f);
				yield return Shot("menu_council.png");
				Check("the council is a page of the M window", Mod.Royal.Open && Mod.MapWindowOpen && Mod.MapPageShown == MapTabs.Page.Council);
				Rect warMapLinkRectForTest = H.WarMapLinkRectForTest;
				Check("its War tab is drawn", warMapLinkRectForTest.width > 0f, warMapLinkRectForTest.ToString());
				Mod.WantMapPage(MapTabs.Page.War);
				yield return CivilTest.Wait(0.3f);
				Check("the War tab takes its place", Mod.WarMap.Open && !Mod.Royal.Open);
				Mod.CloseMapWindow();
				Check("closing the window closes them all", !Mod.MapWindowOpen && !Mod.Royal.Open);
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
				Log.Error("MENU Esc failed: " + ex);
			}
		}
	}
}
