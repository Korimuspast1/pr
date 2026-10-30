using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MenuKeysTest : CivilTest
	{
		private Actor _host;

		protected override string Prefix => "MK";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			CrownOffer.AutoAnswer = true;
			try
			{
				Actor actor = Village("human", 8, 0);
				if (!Check("a village", actor != null))
				{
					yield break;
				}
				foreach (Actor item in Founded)
				{
					if (item != null && item.isAlive() && item != actor && item.isAdult() && !Service.IsSoldier(item))
					{
						_host = item;
						break;
					}
				}
				if (_host == null)
				{
					_host = actor;
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
					yield return Part("map window", MapWindow());
					yield return Part("leave body", LeaveBody());
					yield return Part("god view time", GodTime());
				}
			}
			finally
			{
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

		private IEnumerator MapWindow()
		{
			Mod.CloseMapWindow();
			MapTabs.Last = null;
			Mod.MapKeyForTest();
			yield return Shot("mk_map.png");
			Rect mapWindowRectForTest = H.MapWindowRectForTest;
			Check("M opens the map window", Journal.Open && Mod.MapWindowOpen);
			Check("the window sits under the compass", mapWindowRectForTest.height > 100f && mapWindowRectForTest.y >= H.CompassBottomForTest && mapWindowRectForTest.yMax <= (float)Screen.height, $"window {mapWindowRectForTest}, compass bottom {H.CompassBottomForTest:F0}, screen {Screen.width}x{Screen.height}");
			Mod.MapKeyForTest();
			Check("M closes it", !Mod.MapWindowOpen);
		}

		private IEnumerator LeaveBody()
		{
			Mod.SetSettingsMenu(open: true);
			yield return Shot("mk_settings.png");
			Check("the settings offer to leave the body in first person", Mod.SettingsMenu.Open && Mod.CanLeaveBody);
			Mod.AskLeaveBody();
			for (int i = 0; i < 60; i++)
			{
				if (!Mod.IsFirstPerson)
				{
					break;
				}
				yield return null;
			}
			yield return CivilTest.Wait(0.5f);
			bool flag = true;
			try
			{
				flag = ControllableUnit.isControllingUnit();
			}
			catch
			{
			}
			Check("Leave body lets the creature go and closes the settings", !Mod.IsFirstPerson && !flag && !Mod.SettingsMenu.Open, $"first person {Mod.IsFirstPerson}, controlling {flag}, settings {Mod.SettingsMenu.Open}");
			Check("...and is not offered on the map", !Mod.CanLeaveBody);
		}

		private IEnumerator GodTime()
		{
			Settings s = Mod.Settings;
			bool godWas = s.GodView;
			int modeWas = s.TimeMode;
			s.GodView = true;
			ZoomOnly(20f);
			for (int i = 0; i < 200; i++)
			{
				if (Mod.GodShown)
				{
					break;
				}
				yield return null;
			}
			if (!Check("the 3D god view shows", Mod.GodShown))
			{
				s.GodView = godWas;
				yield break;
			}
			s.TimeMode = 0;
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("mk_god_cycle.png");
			Mod.GodTimeKeyForTest();
			string godToastForTest = Mod.GodToastForTest;
			Check("F7 over the 3D view steps the time of day", s.TimeMode == 1 && godToastForTest != null && godToastForTest.StartsWith("Time of day: " + DayCycle.ModeNames[1], StringComparison.Ordinal), godToastForTest);
			Mod.GodTimeKeyForTest();
			Mod.GodTimeKeyForTest();
			Check("...round to always night", s.TimeMode == 3, DayCycle.ModeNames[s.TimeMode]);
			yield return CivilTest.Wait(1f);
			Mod.SaveFrame("mk_god_night.png");
			Mod.GodTimeKeyForTest();
			Check("...and back to day and night", s.TimeMode == 0);
			s.TimeMode = modeWas;
			s.GodView = godWas;
			s.Save();
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
				Log.Error("MK camera: " + ex.Message);
			}
		}
	}
}
