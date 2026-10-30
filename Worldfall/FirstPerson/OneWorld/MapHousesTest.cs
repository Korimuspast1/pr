using System;
using System.Collections;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal sealed class MapHousesTest : CivilTest
	{
		private Building _up;

		private Building _asleep;

		private Building _empty;

		private Building _walk;

		private Building _other;

		private Actor _upOne;

		private Actor _sleeper;

		private Actor _walker;

		protected override string Prefix => "MAPHOUSES";

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			int houses = s.MapHouses;
			int night = s.MapNight;
			s.MapHouses = 1;
			s.MapNight = 2;
			Actor actor = Village("human", 12, 2);
			if (!Check("a village founded", actor != null))
			{
				yield break;
			}
			City city = actor.city;
			_up = AddHouse("house_human_1", city);
			_asleep = AddHouse("house_human_1", city);
			_empty = AddHouse("house_human_2", city);
			_walk = AddHouse("house_human_3", city);
			if (!Check("four houses", _up != null && _asleep != null && _empty != null && _walk != null))
			{
				yield break;
			}
			int num = 0;
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAlive() && item.isAdult())
				{
					if (_upOne == null)
					{
						_upOne = item;
					}
					else if (_sleeper == null)
					{
						_sleeper = item;
					}
					else if (_walker == null)
					{
						_walker = item;
					}
					if (++num >= 3)
					{
						break;
					}
				}
			}
			if (Check("three villagers", _upOne != null && _sleeper != null && _walker != null))
			{
				Building[] array = new Building[4] { _up, _asleep, _empty, _walk };
				foreach (Building b in array)
				{
					Live(b, city);
				}
				Actor actor2 = Village("human", 6, 0, null, 40f, actor.current_tile);
				if (actor2 != null && actor2.city != null)
				{
					_other = AddHouse("house_human_1", actor2.city);
				}
				if (_other != null)
				{
					Live(_other, actor2.city);
				}
				Note($"a second kingdom's house: {_other != null} ({actor2?.kingdom?.name} vs {actor.kingdom?.name})");
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
				Look(_up.current_position, 12f);
				Config.paused = true;
				In(_upOne, _up, sleep: false);
				In(_sleeper, _asleep, sleep: true);
				Mod.Day.TimeOfDay = 0f;
				yield return CivilTest.Wait(2.5f);
				yield return Part("lit where someone is up", Lights());
				yield return Part("smoke while someone is in", Smoke());
				yield return Part("the door open as someone goes in", Door());
				yield return Part("curtains by the hour", Curtained());
				yield return Part("off: the game's own", Off());
				s.MapHouses = houses;
				s.MapNight = night;
				Config.paused = false;
			}
		}

		private void Live(Building b, City city)
		{
			int num = 0;
			try
			{
				foreach (Actor item in Founded)
				{
					if (item != null && item.isAlive() && item.city == city && item.getHomeBuilding() == null)
					{
						item.setHomeBuilding(b);
						if (++num >= 2)
						{
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Note("homes: " + ex.Message);
			}
		}

		private void In(Actor a, Building b, bool sleep)
		{
			try
			{
				a.stayInBuilding(b);
				if (sleep)
				{
					((BaseSimObject)a).addStatusEffect("sleeping", 600f, pColorEffect: false);
				}
			}
			catch (Exception ex)
			{
				Note("in: " + ex.Message);
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
			catch
			{
			}
		}

		private IEnumerator Lights()
		{
			yield return new WaitForEndOfFrame();
			Sprite light;
			float num = MapHouses.Glow(_up, out light);
			Sprite light2;
			float num2 = MapHouses.Glow(_asleep, out light2);
			float num3 = MapHouses.Glow(_empty, out light2);
			Note(string.Format("glow: up {0:0.00} (light {1}), asleep {2:0.00}, empty {3:0.00}; in: {4}/{5}/{6}, awake {7}/{8}", num, (light != null) ? light.name : "none", num2, num3, HouseOccupancy.Count(_up), HouseOccupancy.Count(_asleep), HouseOccupancy.Count(_empty), HouseOccupancy.Of(_up).Awake, HouseOccupancy.Of(_asleep).Awake));
			Check("someone up inside: lit", num > 0f);
			Check("...dimmed, its curtains drawn at night", num < 0.5f, $"{num:0.00}");
			Check("everyone inside asleep: dark", num2 == 0f);
			Check("nobody in: dark", num3 == 0f);
			Note($"windows drawn: lit {MapLights.LitForTest}, dimmed {MapLights.DimForTest}, dark {MapLights.DarkForTest}");
			Check("the map's window lights drawn by the new rule", MapLights.DimForTest + MapLights.LitForTest >= 1 && MapLights.DarkForTest >= 2);
			Shot("mh_night.png");
		}

		private IEnumerator Smoke()
		{
			Config.paused = false;
			int before = MapSmoke.PuffsForTest;
			yield return CivilTest.Wait(6f);
			int num = MapSmoke.PuffsForTest - before;
			Note($"smoking houses {MapSmoke.SmokingForTest}, puffs {num} in 6 s");
			Check("smoke from the houses someone is in", MapSmoke.SmokingForTest >= 2 && num >= 3);
			Config.paused = true;
			In(_upOne, _up, sleep: false);
			In(_sleeper, _asleep, sleep: true);
		}

		private IEnumerator Door()
		{
			MapSprites.Info info = Info(_walk);
			In(_walker, _walk, sleep: false);
			float t0 = Time.unscaledTime;
			bool flag = false;
			for (int i = 0; i < 40; i++)
			{
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.05f);
				flag = Name(_walk).Contains("_fp_open");
			}
			float num = Time.unscaledTime - t0;
			if (info == null || info.Door.width == 0)
			{
				Note($"{_walk.asset.id}'s doorway isn't known yet (assets/map_sprites): open shown {flag}");
				yield break;
			}
			Check("the door open soon after someone went in", flag, $"{num:0.0} s");
			yield return CivilTest.Wait(3.5f);
			Check("...and shut again", !Name(_walk).Contains("_fp_open"), Name(_walk));
		}

		private static MapSprites.Info Info(Building b)
		{
			Sprite sprite = ((b.animData?.main != null && b.animData.main.Length != 0) ? b.animData.main[0] : null);
			if (!(sprite != null))
			{
				return null;
			}
			return MapSprites.Of(b.asset.id, (int)sprite.textureRect.width, (int)sprite.textureRect.height);
		}

		private static string Name(Building b)
		{
			try
			{
				return b.calculateMainSprite()?.name ?? "";
			}
			catch
			{
				return "";
			}
		}

		private IEnumerator Curtained()
		{
			Mod.Day.TimeOfDay = 0f;
			Name(_up);
			if (_other != null)
			{
				Name(_other);
			}
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			string text = Name(_up);
			string text2 = ((_other != null) ? Name(_other) : "");
			Check("midnight: curtains drawn", Curtains.DrawnOnMap(_up, Calendar.Day, Mod.Day.Hour, null));
			Check("...the curtained picture shown", text.Contains("curtained"), text);
			if (_other != null)
			{
				Sprite sprite = _up.checkSpriteToRender();
				Sprite sprite2 = _other.checkSpriteToRender();
				Check("the second kingdom's curtained too", text2.Contains("curtained"), text2);
				Check("each in its kingdom's colours", sprite != null && sprite2 != null && (object)sprite != sprite2 && _up.kingdom != _other.kingdom, sprite?.name + " / " + sprite2?.name);
			}
			Mod.Day.TimeOfDay = 0.5f;
			yield return CivilTest.Wait(0.5f);
			yield return new WaitForEndOfFrame();
			string text3 = Name(_up);
			Check("noon: curtains open", !Curtains.DrawnOnMap(_up, Calendar.Day, Mod.Day.Hour, null) && !text3.Contains("_fp_curtained"), text3);
			Shot("mh_day.png");
			Mod.Day.TimeOfDay = 0f;
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Off()
		{
			Mod.Settings.MapHouses = 0;
			yield return CivilTest.Wait(0.3f);
			Check("off: the game's own picture", !Name(_up).Contains("_fp_"), Name(_up));
			Mod.Settings.MapHouses = 1;
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
				File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D);
			}
			catch (Exception ex)
			{
				Log.Error("MAPHOUSES shot: " + ex.Message);
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
