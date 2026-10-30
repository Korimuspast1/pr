using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal sealed class RoofDiveTest : CivilTest
	{
		protected override string Prefix => "ROOFDIVE";

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 10, 0);
			if (!Check("a village founded", actor != null))
			{
				yield break;
			}
			Building house = AddHouse("house_human_2", actor.city);
			Actor sleeper = null;
			Actor sitter = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAlive() && item.isAdult())
				{
					if (sleeper == null)
					{
						sleeper = item;
					}
					else if (sitter == null)
					{
						sitter = item;
					}
				}
			}
			if (!Check("a house and two of its people", house != null && sleeper != null && sitter != null))
			{
				yield break;
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
			sleeper.setHomeBuilding(house);
			sitter.setHomeBuilding(house);
			Config.paused = true;
			sleeper.stayInBuilding(house);
			try
			{
				((BaseSimObject)sleeper).addStatusEffect("sleeping", 600f, pColorEffect: false);
			}
			catch
			{
			}
			sitter.stayInBuilding(house);
			RoomLayout layout;
			for (float t = 0f; t < 15f; t += 0.25f)
			{
				if (InsideSpots.Where(sleeper, out layout, out var place) && InsideSpots.Where(sitter, out layout, out place))
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			if (!Check("both placed inside", InsideSpots.Where(sitter, out layout, out var place2)))
			{
				yield break;
			}
			Note($"the sitter's place: {place2.Use} ({place2.X:F2},{place2.Y:F2}) storey {place2.Storey}");
			Look(house.current_position, 8f);
			MapPeek.ForceForTest = house;
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (MapPeek.OpenForTest == house)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("the house open on the map", MapPeek.OpenForTest == house);
			Actor actor2 = MapPeek.SelectAt(InsideSpots.ToWorld(house, place2.MidX, place2.MidY));
			Check("a click on them selects them", actor2 == sitter, (actor2 != null) ? WorldBoxMod.Describe(actor2) : "nobody");
			MapPeek.ForceForTest = null;
			Config.paused = false;
			yield return Possess(sitter, 4f);
			for (float t = 0f; t < 10f; t += 0.25f)
			{
				if (Mod.Interior.Inside && Mod.Interior.House == house)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("taken: in first person in their house's room", InControl(sitter) && Mod.Interior.Inside && Mod.Interior.House == house);
			if (Mod.Interior.Inside)
			{
				Vector2 eye = Mod.Interior.Eye;
				float num = Vector2.Distance(eye, new Vector2(place2.X, place2.Y));
				float num2 = Vector2.Distance(eye, new Vector2(Mod.Interior.Room.SpawnX, Mod.Interior.Room.SpawnY));
				Note($"woke at {eye} ({num:F2} from their place, {num2:F2} from the door)");
				Check("...where they were, not at the door", num < 0.4f || (num2 > 0.5f && num < num2));
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame("rd_woke.png");
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			float t2 = Time.unscaledTime;
			bool flag = false;
			while (Time.unscaledTime - t2 < 12f && !flag)
			{
				yield return CivilTest.Wait(0.1f);
				flag = MapPeek.OpenForTest == house;
			}
			Check("left: the body stays in the house", sitter.is_inside_building && sitter.inside_building == house, $"inside {sitter.is_inside_building}");
			Check("...the house open on the map as the view rises out", flag && RoofDive.LastForTest == house, $"after {Time.unscaledTime - t2:F1} s");
			if (flag)
			{
				yield return new WaitForEndOfFrame();
				Shot("rd_open.png");
			}
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (MapPeek.OpenForTest != house)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("...then its roof closes", MapPeek.OpenForTest != house);
		}

		private static void Look(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y + 1f, camera.transform.position.z);
				camera.orthographicSize = zoom;
				World.world.move_camera?.setTargetZoom(zoom);
				World.world.setZoomOrthographic(zoom);
			}
			catch
			{
			}
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
				Log.Error("ROOFDIVE shot: " + ex.Message);
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
