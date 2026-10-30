using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal sealed class PeekTest : CivilTest
	{
		protected override string Prefix => "PEEK";

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			bool look = s.LookInside;
			int night = s.MapNight;
			s.LookInside = true;
			s.MapNight = 1;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string text in array)
			{
				if (!Options.Contains("only") || Options.Contains("only" + text))
				{
					yield return Part(text, People(text));
				}
			}
			MapPeek.ForceForTest = null;
			s.LookInside = look;
			s.MapNight = night;
			Config.paused = false;
		}

		private IEnumerator People(string race)
		{
			Config.paused = false;
			Actor actor = Village(race, 14, 2);
			if (!Check("a village of " + race, actor != null))
			{
				yield break;
			}
			City city = actor.city;
			Vector2 home = actor.current_position;
			List<Actor> folk = new List<Actor>();
			foreach (Actor item in Founded)
			{
				if (item != null && item != actor && item.isAlive())
				{
					folk.Add(item);
				}
			}
			List<Building> houses = new List<Building>();
			for (int i = 0; i <= 5; i++)
			{
				Building building = AddHouse($"house_{race}_{i}", city);
				if (building != null)
				{
					houses.Add(building);
				}
			}
			Building building2 = AddHouse("tent_" + race, city);
			if (building2 != null)
			{
				houses.Add(building2);
			}
			Note($"{race}: {houses.Count} houses, {folk.Count} people");
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Config.paused = true;
			int num = 0;
			foreach (Building item2 in houses)
			{
				int num2 = 0;
				while (num2 < 3 && num < folk.Count)
				{
					Actor actor2 = folk[num];
					try
					{
						actor2.setHomeBuilding(item2);
						actor2.stayInBuilding(item2);
						if (num2 > 0)
						{
							((BaseSimObject)actor2).addStatusEffect("sleeping", 600f, pColorEffect: false);
						}
					}
					catch (Exception ex)
					{
						Note("in: " + ex.Message);
					}
					num2++;
					num++;
				}
			}
			bool selected = false;
			foreach (Building b in houses)
			{
				IEnumerator one = One(b, selected, delegate(bool s2)
				{
					selected = s2;
				});
				while (true)
				{
					object current3;
					try
					{
						if (!one.MoveNext())
						{
							break;
						}
						current3 = one.Current;
						goto IL_0348;
					}
					catch (Exception ex2)
					{
						Check((b?.asset?.id ?? "a house") + " looked inside without errors", ok: false, ex2.Message);
					}
					break;
					IL_0348:
					yield return current3;
				}
			}
			Building last = ((houses.Count > 0) ? houses[houses.Count - 1] : null);
			MapPeek.ForceForTest = null;
			Look(home + new Vector2(30f, 30f), 8f);
			yield return CivilTest.Wait(0.6f);
			if (last != null)
			{
				Check("the roof back after", !MapPeek.Hides(last) && !Name(last).Contains("_fp_open_roof"), Name(last));
			}
			foreach (Actor item3 in folk)
			{
				try
				{
					item3.exitBuilding();
				}
				catch
				{
				}
			}
			Config.paused = false;
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

		private IEnumerator One(Building b, bool selected, Action<bool> setSelected)
		{
			if (b == null || !b.isAlive() || b.asset == null)
			{
				Note("a house gone before it was looked into");
				yield break;
			}
			MapPeek.ForceForTest = b;
			Look(b.current_position, 8f);
			for (float t = 0f; t < 12f; t += 0.25f)
			{
				if (MapPeek.OpenForTest == b)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			yield return CivilTest.Wait(0.4f);
			yield return new WaitForEndOfFrame();
			bool flag = MapPeek.OpenForTest == b;
			HouseWorld.Placement pose;
			RoomLayout roomLayout = InsideSpots.LayoutOf(b, out pose);
			int num = 0;
			RoomLayout layout;
			foreach (Actor item in HouseOccupancy.Of(b).Inside)
			{
				if (InsideSpots.Where(item, out layout, out var _))
				{
					num++;
				}
			}
			Check(b.asset.id + " opens", flag, "layout " + ((roomLayout != null) ? "made" : "not made"));
			if (!flag)
			{
				yield break;
			}
			if (num > 0)
			{
				Check("...its people drawn", MapPeek.PeopleForTest >= 1 && MapPeek.PeopleForTest <= num, $"{MapPeek.PeopleForTest} drawn of {num} placed");
			}
			if (b.animData?.main != null && b.animData.main.Length == 1 && !b.asset.has_special_animation_state && !b.check_spawn_animation)
			{
				bool hidden = false;
				for (int i = 0; i < 10; i++)
				{
					bool flag2;
					hidden = (flag2 = Name(b).Contains("_fp_open_roof"));
					if (flag2)
					{
						break;
					}
					yield return null;
				}
				Check("...its own sprite hidden under it", hidden, $"{Name(b)}; hides {MapPeek.Hides(b)}, clear {MapPeekDraw.ClearOf(b.animData.main[0]) != null}");
			}
			else
			{
				Note(b.asset.id + ": an animated sprite, left as the game draws it under the plan");
			}
			Mod.Day.TimeOfDay = 0f;
			yield return CivilTest.Wait(0.3f);
			yield return new WaitForEndOfFrame();
			Shot("pk_" + b.asset.id + "_night.png");
			Mod.Day.TimeOfDay = 0.5f;
			yield return CivilTest.Wait(0.3f);
			yield return new WaitForEndOfFrame();
			Shot("pk_" + b.asset.id + "_day.png");
			if (selected)
			{
				yield break;
			}
			foreach (Actor item2 in HouseOccupancy.Of(b).Inside)
			{
				if (InsideSpots.Where(item2, out layout, out var place2))
				{
					Actor actor = MapPeek.SelectAt(InsideSpots.ToWorld(b, place2.MidX, place2.MidY));
					if (actor != null)
					{
						Check("a click on someone inside selects them", SelectedUnit.isSelected(actor), WorldBoxMod.Describe(actor));
						setSelected(obj: true);
						break;
					}
				}
			}
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
				Log.Error("PEEK shot: " + ex.Message);
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
