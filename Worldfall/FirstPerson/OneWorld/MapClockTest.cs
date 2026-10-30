using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal sealed class MapClockTest : CivilTest
	{
		private Actor _body;

		private Vector2 _village;

		protected override string Prefix => "MAPCLOCK";

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			int timeMode = s.TimeMode;
			int mapNight = s.MapNight;
			s.TimeMode = 0;
			s.MapNight = 1;
			Actor actor = Village("human", 10, 3);
			if (!Check("a village founded", actor != null))
			{
				yield break;
			}
			_village = actor.current_position;
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAlive() && item.isAdult() && _body == null)
				{
					_body = item;
				}
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1f);
			Check("on the plain map", !Mod.IsFirstPerson && !Mod.GodShown);
			Look(_village, 14f);
			yield return Part("the hour on the map", Hours());
			yield return Part("the map's night", Night());
			yield return Part("bills count one dawn away", Bills());
			if (_body != null)
			{
				yield return Part("first person keeps its day", FirstPerson());
			}
			yield return Part("map night off", OffAgain());
			s.TimeMode = timeMode;
			s.MapNight = mapNight;
			try
			{
				Config.setWorldSpeed("x1", pUpdateDebug: false);
			}
			catch
			{
			}
		}

		private static void Look(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				World.world.move_camera?.setTargetZoom(zoom);
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("MAPCLOCK camera: " + ex.Message);
			}
		}

		private IEnumerator Measure(float seconds, float[] into)
		{
			DayCycle day = Mod.Day;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			day.TimeOfDay = 0.4f;
			yield return null;
			into[3] = day.Rate(Mod.Settings);
			float h0 = day.TimeOfDay;
			float r0 = Time.unscaledTime;
			double a0 = WorldPace.ActionNow;
			yield return CivilTest.Wait(seconds);
			float num = day.TimeOfDay - h0;
			if (num < 0f)
			{
				num += 1f;
			}
			into[0] = num;
			into[1] = (float)(WorldPace.ActionNow - a0);
			into[2] = Time.unscaledTime - r0;
			if (into[1] <= 0f)
			{
				Note($"the world stood: paused {Config.paused}, window {ScrollWindow.isWindowActive()}, isPaused {World.world.isPaused()}");
			}
		}

		private IEnumerator Hours()
		{
			float[] m = new float[4];
			Config.paused = false;
			Config.setWorldSpeed("x1", pUpdateDebug: false);
			yield return CivilTest.Wait(1f);
			yield return Measure(6f, m);
			float num = m[3];
			float worldX1 = m[1] / Mathf.Max(0.01f, m[2]);
			Note($"x1: the hour moved {m[0]:0.00000} in {m[2]:0.0} s, world {m[1]:0.00} s; by the rate {m[1] * num:0.00000}");
			Check("at x1 the hour runs on the map", m[0] > 0f);
			float num2 = m[0] / Mathf.Max(0.01f, m[2]);
			Check("...as fast as a day at x1 always ran: real time", Mathf.Abs(num2 - num) < 0.15f * num, $"{num2 / num:0.00} of the rate a real second");
			Config.setWorldSpeed("x5", pUpdateDebug: false);
			yield return CivilTest.Wait(1f);
			yield return Measure(6f, m);
			num = m[3];
			float num3 = m[1] / Mathf.Max(0.01f, m[2]);
			Note($"x5: the hour moved {m[0]:0.00000} in {m[2]:0.0} s, world {m[1]:0.00} s ({num3 / Mathf.Max(0.001f, worldX1):0.00}x of x1's)");
			Check("at x5 the world runs about five times as fast", num3 > worldX1 * 3.5f, $"{num3:0.00} vs {worldX1:0.00} world s a second");
			num2 = m[0] / Mathf.Max(0.01f, m[2]);
			Check("at x5 the day runs five times as fast", Mathf.Abs(num2 - 5f * num) < 0.75f * num, $"{num2 / num:0.00} of the rate a real second");
			Config.setWorldSpeed("x1", pUpdateDebug: false);
			Config.paused = true;
			yield return CivilTest.Wait(0.5f);
			float h = Mod.Day.TimeOfDay;
			yield return CivilTest.Wait(2f);
			Check("paused, the hour stands", Mathf.Abs(Mod.Day.TimeOfDay - h) < 1E-06f, $"{Mod.Day.TimeOfDay - h:0.000000}");
			Config.paused = false;
		}

		private IEnumerator Night()
		{
			WorldAgeManager ages = World.world.era_manager;
			WorldAgeEffects fx = WorldAgeEffects.instance;
			bool nightBefore = ages.isNight();
			float ageNight = ages._night_multiplier;
			Note($"age {ages.getCurrentAge()?.id}, its night {ageNight:0.00}, isNight {nightBefore}");
			Config.paused = true;
			Mod.Day.TimeOfDay = 0.5f;
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			Check("noon: no night on the map", MapNight.ValueForTest <= ageNight + 0.001f && (ageNight > 0f || !fx.override_night), $"value {MapNight.ValueForTest:0.00}, override {fx.override_night}");
			Shot("mc_noon.png");
			Mod.Day.TimeOfDay = 0.758f;
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			float valueForTest = MapNight.ValueForTest;
			Check("dusk: part of the night", valueForTest > 0.02f && valueForTest < 0.29f, $"{valueForTest:0.000}");
			Shot("mc_dusk.png");
			Mod.Day.TimeOfDay = 0f;
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			Check("midnight, Light: the game's night value 0.3", fx.override_night && Mathf.Abs(ages.getNightMod() - Mathf.Max(0.3f, ageNight)) < 0.01f, $"override {fx.override_night}, getNightMod {ages.getNightMod():0.00}");
			Note($"midnight: lights shown {ages.shouldShowLights()}, night layer {MapNight.LayerForTest:0.00}");
			Check("only the night layer shows (the age's other layers as the game has them)", Layers(fx, out var seen), seen);
			Shot("mc_midnight_light.png");
			Mod.Settings.MapNight = 2;
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			Check("midnight, Full: 0.6", Mathf.Abs(ages.getNightMod() - 0.6f) < 0.01f, $"{ages.getNightMod():0.00}");
			Check("the map's lights shown at night", ages.shouldShowLights());
			Shot("mc_midnight_full.png");
			Check("the gameplay night is the age's alone", ages.isNight() == nightBefore, $"isNight {ages.isNight()}");
			Mod.Day.Update(Mod.Settings, 0.016f);
			Check("the 3D side reads the age's own night", Mathf.Abs(Mod.Day.WorldNight - Mathf.Clamp01(ageNight / 0.6f)) < 0.02f, $"WorldNight {Mod.Day.WorldNight:0.00}");
			Mod.Settings.MapNight = 1;
			Config.paused = false;
		}

		private static bool Layers(WorldAgeEffects fx, out string seen)
		{
			seen = "";
			bool flag = true;
			WorldAgeAsset world_era = World.world_era;
			foreach (KeyValuePair<string, SpriteRenderer> dict_effect in fx.dict_effects)
			{
				SpriteRenderer value = dict_effect.Value;
				if (!(value == null))
				{
					seen += string.Format("{0} {1} {2:0.00}; ", dict_effect.Key, value.enabled ? "on" : "off", value.color.a);
					if (dict_effect.Key == "night")
					{
						flag &= value.enabled && Mathf.Abs(value.color.a - Mathf.Max(MapNight.LayerForTest, 0f)) < 0.01f && value.color.a > 0.05f;
					}
					else if (value.enabled && value.color.a > 0.02f)
					{
						flag &= world_era != null && world_era.era_effect_overlay_alpha > 0f;
					}
				}
			}
			return flag;
		}

		private IEnumerator Dawn()
		{
			int day = Calendar.Day;
			Mod.Day.TimeOfDay = 0.2497f;
			for (float t = 0f; t < 8f; t += 0.1f)
			{
				if (Calendar.Day != day)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator Bills()
		{
			Config.paused = false;
			if (_body != null)
			{
				yield return Possess(_body);
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				yield return CivilTest.Wait(1f);
			}
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Config.setWorldSpeed("x5", pUpdateDebug: false);
			int d0 = Calendar.Day;
			int b0 = Calendar.BillDay;
			for (int i = 0; i < 3; i++)
			{
				yield return Dawn();
			}
			Note($"away: days {d0} -> {Calendar.Day}, bill days {b0} -> {Calendar.BillDay}");
			Check("three dawns on the map: three days", Calendar.Day == d0 + 3);
			Check("...and one bill day", Calendar.BillDay == b0 + 1);
			Config.setWorldSpeed("x1", pUpdateDebug: false);
			if (_body != null)
			{
				yield return Possess(_body);
				if (Check("in a body", InControl(_body)))
				{
					int d1 = Calendar.Day;
					int b1 = Calendar.BillDay;
					yield return Dawn();
					Check("a dawn in a body counts both", Calendar.Day == d1 + 1 && Calendar.BillDay == b1 + 1, $"days +{Calendar.Day - d1}, bill days +{Calendar.BillDay - b1}");
				}
			}
		}

		private IEnumerator FirstPerson()
		{
			if (!InControl(_body))
			{
				yield return Possess(_body);
			}
			if (Check("first person", InControl(_body)))
			{
				float[] m = new float[4];
				yield return Measure(6f, m);
				float num = m[3];
				float num2 = m[0] / Mathf.Max(0.01f, m[2]);
				Note($"first person: the hour moved {m[0]:0.00000} in {m[2]:0.0} s ({num2 / Mathf.Max(1E-09f, num):0.00} of the rate a real second), world {m[1]:0.00} s");
				Check("the day runs on real time as before", num2 > num * 0.75f && num2 < num * 1.25f, $"{num2:0.000000} vs {num:0.000000}");
				Config.paused = true;
				Mod.Day.TimeOfDay = 0f;
				yield return CivilTest.Wait(0.5f);
				Check("no map night while in first person", !WorldAgeEffects.instance.override_night || World.world.era_manager._night_multiplier > 0f, $"override {WorldAgeEffects.instance.override_night}");
				Config.paused = false;
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				yield return CivilTest.Wait(2f);
			}
		}

		private IEnumerator OffAgain()
		{
			Config.paused = true;
			Mod.Day.TimeOfDay = 0f;
			Mod.Settings.MapNight = 1;
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			bool on = WorldAgeEffects.instance.override_night;
			Mod.Settings.MapNight = 0;
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			float night_multiplier = World.world.era_manager._night_multiplier;
			Check("Off: the game's own night again", on && !WorldAgeEffects.instance.override_night && Mathf.Abs(World.world.era_manager.getNightMod() - night_multiplier) < 0.001f, $"was on {on}, override {WorldAgeEffects.instance.override_night}, getNightMod {World.world.era_manager.getNightMod():0.00}");
			Shot("mc_midnight_off.png");
			Config.paused = false;
		}

		private void Shot(string name)
		{
			Camera camera = World.world.camera;
			RenderTexture renderTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			RenderTexture targetTexture = camera.targetTexture;
			RenderTexture active = RenderTexture.active;
			try
			{
				camera.targetTexture = renderTexture;
				camera.Render();
				RenderTexture.active = renderTexture;
				Texture2D texture2D = new Texture2D(1280, 720, TextureFormat.RGB24, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
				texture2D.Apply();
				Color32[] pixels = texture2D.GetPixels32();
				double num = 0.0;
				for (int i = 0; i < pixels.Length; i += 7)
				{
					num += (double)(pixels[i].r + pixels[i].g + pixels[i].b);
				}
				Note($"{name}: mean brightness {num / ((double)pixels.Length / 7.0) / 3.0:0.0}");
				File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D);
			}
			catch (Exception ex)
			{
				Log.Error("MAPCLOCK shot: " + ex.Message);
			}
			finally
			{
				camera.targetTexture = targetTexture;
				RenderTexture.active = active;
				renderTexture.Release();
			}
		}
	}
}
