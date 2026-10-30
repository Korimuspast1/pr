using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LookThroughTest : CivilTest
	{
		private static readonly string[] Houses = new string[5] { "house_human_1", "house_human_2", "house_human_3", "house_human_4", "house_human_5" };

		private Actor _you;

		private City _city;

		private Building _built;

		protected override string Prefix => "LOOKTHROUGH";

		protected override IEnumerator Body()
		{
			Mod.Settings.GpuRender = true;
			Mod.Settings.WindowView = 1;
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
			_city = actor.city;
			_you = ((Founded.Count > 1) ? Founded[1] : actor);
			yield return Possess(_you);
			if (!Check("first person", InControl(_you)))
			{
				yield break;
			}
			string[] houses = Houses;
			foreach (string id in houses)
			{
				if (Options.Contains("only") && !Options.Contains("only" + id))
				{
					continue;
				}
				string[] array = ((!Options.Contains("shapes")) ? new string[1] { "?" } : new string[3] { "", "_b", "_c" });
				foreach (string text in array)
				{
					string text2 = ((text == "?") ? id : (id + text));
					if (!(text != "?") || Models.Has(text2))
					{
						Models.FamilyForTest = ((text == "?") ? null : text2);
						yield return Part(text2, House(id, text2));
						Models.FamilyForTest = null;
						if (Mod.Interior.Inside)
						{
							Mod.LeaveRoomForTest();
						}
						try
						{
							_built?.removeBuildingFinal();
						}
						catch
						{
						}
						_built = null;
						yield return CivilTest.Wait(0.5f);
					}
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
		}

		private IEnumerator House(string asset, string id)
		{
			Building b = (_built = AddHouse(asset, _city));
			if (!Check(id + " built", b != null && HouseInterior.IsEnterable(b)))
			{
				yield break;
			}
			Homes.SetOwner(b, _you);
			Vector2 current_position = b.current_position;
			Put(_you, current_position + new Vector2(0f, -4.5f));
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-2f);
			for (float t = 0f; t < 4f; t += 0.2f)
			{
				if (HouseWorld.Of(b, out var _))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(0.4f);
			if (!Check(id + ": drawn", HouseWorld.Of(b, out var place)))
			{
				yield break;
			}
			HousePlan housePlan = HousePlan.Of(place.Model);
			if (!Check(id + ": measured", housePlan != null))
			{
				yield break;
			}
			float num = (place.Mirror ? (0f - place.Scale) : place.Scale);
			Vector2 vector = new Vector2(place.X + housePlan.Door.X * num, place.Y + housePlan.Door.Y * place.Scale);
			Vector2 normalized = new Vector2(housePlan.Door.OutX * (place.Mirror ? (-1f) : 1f), housePlan.Door.OutY).normalized;
			Put(_you, vector + normalized * 1.6f);
			Mod.SetYaw(Mathf.Atan2(0f - normalized.y, 0f - normalized.x));
			float t2 = Time.unscaledTime;
			while (Time.unscaledTime - t2 < 6f && (!Mod.DoorRoom.Inside || !Mod.DoorRoom.Fitted || Mod.DoorRoom.House != b))
			{
				yield return CivilTest.Wait(0.1f);
			}
			if (!Check(id + ": room ready at the door", Mod.DoorRoom.Inside && Mod.DoorRoom.House == b))
			{
				yield break;
			}
			yield return CivilTest.Wait(0.5f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			t2 = Time.unscaledTime;
			while (Time.unscaledTime - t2 < 8f && !Mod.Interior.Inside)
			{
				yield return null;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			if (!Check(id + ": walked in", Mod.Interior.Inside && Mod.Interior.House == b))
			{
				yield break;
			}
			Interior room = Mod.Interior.Room;
			if (!Mod.Interior.DoorHeldOpen)
			{
				Mod.Interior.ToggleDoor();
			}
			Note(string.Format("{0}: model {1}, room {2:F2}..{3:F2} x {4:F2}..{5:F2}, door ({6:F2},{7:F2}) out ({8:F2},{9:F2}), {10} windows, storeys {11}, place ({12:F2},{13:F2}) scale {14:F3}{15}", id, place.Model.Name, room.MinX, room.MaxX, room.MinY, room.MaxY, room.DoorX, room.DoorY, room.DoorOutX, room.DoorOutY, room.Windows.Count, room.Fit.StoreyZ.Length, place.X, place.Y, place.Scale, place.Mirror ? " mirrored" : ""));
			for (int i = 0; i < room.Windows.Count; i++)
			{
				LightWindow lightWindow = room.Windows[i];
				Note($"{id}: window {i} at ({lightWindow.Center.X:F2},{lightWindow.Center.Y:F2},{lightWindow.Center.Z:F2}) inward ({lightWindow.Inward.X:F2},{lightWindow.Inward.Y:F2})");
			}
			Vector2 door = new Vector2(room.DoorX, room.DoorY);
			List<Vector2> spots = new List<Vector2> { Mod.Interior.Eye };
			Vector2 vector2 = Nearest(room, new Vector2((room.MinX + room.MaxX) * 0.5f, (room.MinY + room.MaxY) * 0.5f));
			spots.Add(vector2);
			Vector2 item = vector2;
			float num2 = 0f;
			for (float num3 = room.MinY; num3 < room.MaxY; num3 += 0.1f)
			{
				for (float num4 = room.MinX; num4 < room.MaxX; num4 += 0.1f)
				{
					float magnitude = (new Vector2(num4, num3) - door).magnitude;
					if (magnitude > num2 && room.Fit.Clear(0, num4, num3, 0.3f))
					{
						num2 = magnitude;
						item = new Vector2(num4, num3);
					}
				}
			}
			spots.Add(item);
			for (int s = 0; s < spots.Count; s++)
			{
				Mod.Interior.Eye = spots[s];
				for (int k = 0; k < 8; k++)
				{
					float yaw = (float)k * 45f * (MathF.PI / 180f);
					Mod.SetYaw(yaw);
					Mod.SetPitchDegrees(-4f);
					int frames = HouseInterior.BackdropFrames;
					yield return CivilTest.Wait(0.5f);
					Note($"{id}: spot {s} ({spots[s].x:F2},{spots[s].y:F2}) yaw {k * 45}: door open {Mod.Interior.DoorOpen:F2}, opening in view {Mod.WindowInViewForTest}, town frames {HouseInterior.BackdropFrames - frames}");
					Mod.SaveFrame($"lookthrough_{id}_s{s}_y{k * 45}.png");
				}
				Mod.SetYaw(Mathf.Atan2(door.y - spots[s].y, door.x - spots[s].x));
				yield return CivilTest.Wait(0.5f);
				Mod.SaveFrame($"lookthrough_{id}_s{s}_door.png");
			}
		}

		private static Vector2 Nearest(Interior room, Vector2 p)
		{
			Vector2 result = p;
			float num = float.MaxValue;
			for (float num2 = room.MinY; num2 < room.MaxY; num2 += 0.1f)
			{
				for (float num3 = room.MinX; num3 < room.MaxX; num3 += 0.1f)
				{
					float sqrMagnitude = (new Vector2(num3, num2) - p).sqrMagnitude;
					if (sqrMagnitude < num && room.Fit.Clear(0, num3, num2, 0.3f))
					{
						num = sqrMagnitude;
						result = new Vector2(num3, num2);
					}
				}
			}
			return result;
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
	}
}
