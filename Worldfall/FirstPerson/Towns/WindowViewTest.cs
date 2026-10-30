using System;
using System.Collections;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class WindowViewTest : CivilTest
	{
		private int _counted;

		protected override string Prefix => "WINDOWVIEW";

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			s.GpuRender = true;
			s.WindowView = 1;
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			Actor actor = Village("human", 6, 0, worldTile);
			if (!Check("a village", actor != null && actor.city != null))
			{
				yield break;
			}
			Building house = AddHouse("house_human_1", actor.city);
			if (!Check("a house of the town", house != null && HouseInterior.IsEnterable(house)))
			{
				yield break;
			}
			Actor you = Founded[Founded.Count - 1];
			yield return Possess(you);
			if (!Check("first person", InControl(you)))
			{
				yield break;
			}
			Mod.DebugEye = house.current_position + new Vector2(0f, -4f);
			Mod.DebugEyeHeight = 0f;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(MathF.PI / 2f);
			yield return CivilTest.Wait(1.5f);
			Mod.SaveFrame("windowview_outside.png");
			Mod.DebugEyeSet = false;
			Mod.EnterHouseForTest(house);
			yield return CivilTest.Wait(1f);
			if (!Check("in the house", Mod.Interior.Inside && Mod.Interior.House == house))
			{
				yield break;
			}
			Interior room = Mod.Interior.Room;
			Check("the room's door has a hinged leaf", room.DoorLeafPart >= 0, $"part {room.DoorLeafPart}, portal {room.DoorPortal}");
			Check("the room has windows", room.Windows.Count > 0, $"{room.Windows.Count}");
			Mod.SetPitchDegrees(-4f);
			Mod.SetYaw(-MathF.PI / 2f);
			yield return CivilTest.Wait(1.2f);
			Check("the door swings open as you stand at it", Mod.Interior.DoorOpen > 0.9f, $"open {Mod.Interior.DoorOpen:F2}");
			yield return Count(1f);
			int counted = _counted;
			Check("the world outside is drawn behind the open door", counted > 5, $"{counted} frames in a second");
			Mod.SaveFrame("windowview_door_open.png");
			Mod.Interior.Eye = new Vector2((room.MinX + room.MaxX) * 0.5f, (room.MinY + room.MaxY) * 0.5f);
			if (room.Fit != null)
			{
				Vector2 vector = new Vector2(room.DoorX, room.DoorY);
				float num = 0f;
				for (float num2 = room.MinY; num2 < room.MaxY; num2 += 0.1f)
				{
					for (float num3 = room.MinX; num3 < room.MaxX; num3 += 0.1f)
					{
						float magnitude = (new Vector2(num3, num2) - vector).magnitude;
						if (magnitude > num && room.Fit.Clear(0, num3, num2, 0.3f))
						{
							num = magnitude;
							Mod.Interior.Eye = new Vector2(num3, num2);
						}
					}
				}
				Note($"standing {num:F2} from the door");
			}
			yield return CivilTest.Wait(1.5f);
			Check("the door shuts once you've gone from it", Mod.Interior.DoorOpen < 0.1f, $"open {Mod.Interior.DoorOpen:F2}");
			float fov = s.FieldOfView * (MathF.PI / 180f);
			if (room.Windows.Count > 0)
			{
				LightWindow lightWindow = room.Windows[0];
				Mod.SetYaw(Mathf.Atan2(lightWindow.Center.Y - Mod.Interior.Eye.y, lightWindow.Center.X - Mod.Interior.Eye.x));
				yield return CivilTest.Wait(0.5f);
				yield return Count(1f);
				counted = _counted;
				Check("the world is drawn behind a window", counted > 5, $"{counted} frames in a second");
				Mod.SaveFrame("windowview_window.png");
			}
			float blank = float.NaN;
			for (int i = 0; i < 36; i++)
			{
				float num4 = (float)i * 10f * (MathF.PI / 180f);
				if (!Mod.Interior.PortalInView(Mod.Interior.Eye.x, Mod.Interior.Eye.y, num4, fov))
				{
					blank = num4;
					break;
				}
			}
			if (float.IsNaN(blank))
			{
				Note("every way faces an opening in this room: no blank-wall check");
			}
			else
			{
				Mod.SetYaw(blank);
				yield return CivilTest.Wait(0.5f);
				yield return Count(1f);
				counted = _counted;
				Check("no outdoor frame while no opening is on screen", counted == 0, $"{counted} frames in a second facing {blank * 57.29578f:F0} deg");
				Mod.SaveFrame("windowview_wall.png");
			}
			Mod.SetPitchDegrees(-80f);
			yield return CivilTest.Wait(0.5f);
			yield return Count(1f);
			Check("no outdoor frame looking at the floor", _counted == 0, $"{_counted} frames in a second");
			Mod.SaveFrame("windowview_floor.png");
			Mod.SetPitchDegrees(-4f);
			Mod.SetYaw(-MathF.PI / 2f);
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("windowview_door_shut.png");
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			Check("out again", !Mod.Interior.Inside);
		}

		private IEnumerator Count(float seconds)
		{
			int from = HouseInterior.BackdropFrames;
			yield return CivilTest.Wait(seconds);
			_counted = HouseInterior.BackdropFrames - from;
		}
	}
}
