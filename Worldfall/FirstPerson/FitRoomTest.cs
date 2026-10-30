using System;
using System.Collections;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FitRoomTest : CivilTest
	{
		private static readonly string[] Houses = new string[3] { "house_human_2", "house_human_0", "house_human_1" };

		private Actor _you;

		private City _city;

		private Building _built;

		protected override string Prefix => "FITROOM";

		protected override IEnumerator Body()
		{
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
			foreach (string text in houses)
			{
				if (!Options.Contains("only") || Options.Contains("only" + text))
				{
					yield return Part(text, House(text));
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
			PossessionHooks.ScriptedInput = Vector2.zero;
		}

		private IEnumerator House(string id)
		{
			Building b = (_built = AddHouse(id, _city));
			if (!Check(id + " built", b != null && HouseInterior.IsEnterable(b)))
			{
				yield break;
			}
			Homes.SetOwner(b, _you);
			Vector2 current_position = b.current_position;
			Put(_you, current_position + new Vector2(0f, -4.5f));
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-2f);
			HouseWorld.Placement p = default(HouseWorld.Placement);
			for (float t = 0f; t < 4f; t += 0.2f)
			{
				if (HouseWorld.Of(b, out p))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(0.4f);
			if (!Check(id + ": drawn where it stands", HouseWorld.Of(b, out var place)) || !Check(id + ": its door is known", WorldBoxMod.DoorPointForTest(b, place, out var door)))
			{
				yield break;
			}
			HousePlan plan = HousePlan.Of(place.Model);
			if (!Check(id + ": its model measured", plan != null, (plan == null) ? HousePlan.Why : $"{place.Model.Name}: {plan.Storeys.Count} storeys, floor {plan.Storeys[0].Area:F1} m2, {plan.Windows.Count} windows"))
			{
				yield break;
			}
			FaceOf(place, plan, out var doorFace, out var doorOut);
			Put(_you, doorFace + doorOut * 1.6f);
			Mod.SetYaw(Mathf.Atan2(0f - doorOut.y, 0f - doorOut.x));
			float t2 = Time.unscaledTime;
			while (Time.unscaledTime - t2 < 6f && (!Mod.DoorRoom.Inside || !Mod.DoorRoom.Fitted || Mod.DoorRoom.House != b))
			{
				yield return CivilTest.Wait(0.1f);
			}
			if (!Check(id + ": its room ready at the door", Mod.DoorRoom.Inside && Mod.DoorRoom.Fitted && Mod.DoorRoom.House == b, $"after {Time.unscaledTime - t2:F1} s"))
			{
				yield break;
			}
			Interior room = Mod.DoorRoom.Room;
			Note(string.Format("{0}: room {1:F2}x{2:F2} t, {3} storeys{4}{5}, {6} windows, {7} models, scale {8:F3}{9}", id, room.MaxX - room.MinX, room.MaxY - room.MinY, room.Fit.StoreyZ.Length, float.IsNaN(room.Fit.StairFootX) ? "" : ", a stair", room.Fit.UpperShut ? ", upper floor shut" : "", room.Windows.Count, room.Meshes.Count, place.Scale, place.Mirror ? " mirrored" : ""));
			float num = (place.Model.Max.X - place.Model.Min.X) * place.Scale;
			float num2 = (place.Model.Max.Y - place.Model.Min.Y) * place.Scale;
			Check(id + ": the room fits in the house", room.MaxX - room.MinX <= num + 0.05f && room.MaxY - room.MinY <= num2 + 0.05f, $"room {room.MaxX - room.MinX:F2}x{room.MaxY - room.MinY:F2}, house {num:F2}x{num2:F2}");
			yield return CivilTest.Wait(0.6f);
			Mod.SaveFrame("fitroom_" + id + "_door.png");
			if (Options.Contains("nowalk"))
			{
				yield break;
			}
			int inBefore = WorldBoxMod.DoorStepsIn;
			Vector2 lastOut = _you.current_position;
			float yaw = Mathf.Atan2(0f - doorOut.y, 0f - doorOut.x);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			t2 = Time.unscaledTime;
			float noted = 0f;
			while (Time.unscaledTime - t2 < 8f && !Mod.Interior.Inside)
			{
				lastOut = _you.current_position;
				if (Time.unscaledTime - t2 > noted + 1.5f)
				{
					noted = Time.unscaledTime - t2;
					Vector2 current_position2 = _you.current_position;
					Vector2 p2 = current_position2 - doorOut * 0.15f;
					Note(string.Format("{0}: walking in at {1:F1} s: at {2}, {3:F2} out from the door's face, nearest {4} here, {5} a step on; {6}; door open {7:F2}, to its point {8:F2}, room at {9}", id, noted, current_position2, Vector2.Dot(current_position2 - doorFace, doorOut), Colliders.NearestForTest(current_position2), Colliders.NearestForTest(p2), Colliders.BodyForTest, HouseLife.OpenOf(b), WorldBoxMod.DoorPointForTest(b, place, out var door2) ? (current_position2 - door2).magnitude : (-1f), (Mod.DoorRoom.House == b) ? "this door" : "another"));
				}
				yield return null;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			bool flag = Mod.Interior.Inside && Mod.Interior.House == b;
			if (!Check(id + ": walked in through the door", flag && WorldBoxMod.DoorStepsIn > inBefore, $"inside {Mod.Interior.Inside}, at {_you.current_position}, door {door}"))
			{
				yield break;
			}
			Vector2 vector = Mod.Interior.ToWorld(Mod.Interior.Eye);
			Check(id + ": in the room where you stood", (vector - lastOut).magnitude < 0.75f, $"room eye in the world {vector}, last outside {lastOut}");
			Check(id + ": facing as you were", Mathf.Abs(Mathf.DeltaAngle(Mod.EyeYawForTest * 57.29578f, yaw * 57.29578f)) < 5f, $"yaw {Mod.EyeYawForTest * 57.29578f:F1} in, {yaw * 57.29578f:F1} out");
			yield return CivilTest.Wait(0.3f);
			Mod.SaveFrame("fitroom_" + id + "_in.png");
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return CivilTest.Wait(0.7f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return CivilTest.Wait(0.3f);
			Mod.SaveFrame("fitroom_" + id + "_room.png");
			if (room.Windows.Count > 0)
			{
				LightWindow win = room.Windows[0];
				float num3 = float.MaxValue;
				foreach (LightWindow window in room.Windows)
				{
					if (!(window.Center.Z > room.Fit.StoreyZ[0] + 1.6f))
					{
						float magnitude = new Vector2(window.Center.X - Mod.Interior.Eye.x, window.Center.Y - Mod.Interior.Eye.y).magnitude;
						if (magnitude < num3)
						{
							num3 = magnitude;
							win = window;
						}
					}
				}
				Mod.SetYaw(Mathf.Atan2(win.Center.Y - Mod.Interior.Eye.y, win.Center.X - Mod.Interior.Eye.x));
				int frames = HouseInterior.BackdropFrames;
				yield return CivilTest.Wait(0.8f);
				Note($"{id}: window at ({win.Center.X:F2},{win.Center.Y:F2},{win.Center.Z:F2}), backdrop frames {HouseInterior.BackdropFrames - frames}");
				if (Mod.Settings.WindowView == 1 && Mod.GpuWantedForTest)
				{
					Check(id + ": the town drawn for the window", HouseInterior.BackdropFrames > frames);
				}
				Mod.SaveFrame("fitroom_" + id + "_window.png");
			}
			if (!float.IsNaN(room.Fit.StairFootX))
			{
				Vector2 vector2 = new Vector2(room.Fit.StairFootX, room.Fit.StairFootY);
				Vector2 vector3 = new Vector2(room.Fit.StairHeadX, room.Fit.StairHeadY);
				Vector2 dir = (vector3 - vector2).normalized;
				Mod.Interior.Eye = vector2 - dir * 0.35f;
				Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
				yield return CivilTest.Wait(0.5f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				t2 = Time.unscaledTime;
				float t = 0f;
				while (Time.unscaledTime - t2 < 6f && RoomFloors.Feet < room.Fit.StoreyZ[1] - 0.1f)
				{
					if (Time.unscaledTime - t2 > t + 0.5f)
					{
						t = Time.unscaledTime - t2;
						Note(string.Format("{0}: climbing at {1:F1} s: eye {2}, feet {3:F2}, body {4:F2}, field ready {5}, a step on: {6}", id, t, Mod.Interior.Eye, RoomFloors.Feet, RoomFloors.BodyForTest, RoomFloors.Ready, Mod.Interior.BlockedWhyForTest(Mod.Interior.Eye + dir * 0.05f) ?? "free"));
					}
					yield return null;
				}
				yield return CivilTest.Wait(0.4f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				Check(id + ": up the stair to the upper floor", RoomFloors.Feet > room.Fit.StoreyZ[1] - 0.15f, $"feet {RoomFloors.Feet:F2}, upper floor {room.Fit.StoreyZ[1]:F2}, at {Mod.Interior.Eye}");
				yield return CivilTest.Wait(0.3f);
				Mod.SaveFrame("fitroom_" + id + "_upstairs.png");
				Mod.Interior.Eye = new Vector2(room.SpawnX, room.SpawnY);
				RoomFloors.GroundForTest();
				yield return CivilTest.Wait(0.5f);
			}
			int outBefore = WorldBoxMod.DoorStepsOut;
			Mod.Interior.Eye = new Vector2(room.SpawnX, room.SpawnY);
			Mod.SetYaw(Mathf.Atan2(room.DoorOutY, room.DoorOutX));
			yield return CivilTest.Wait(0.3f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			t2 = Time.unscaledTime;
			float told = 0f;
			Vector2 outWay = new Vector2(room.DoorOutX, room.DoorOutY);
			while (Time.unscaledTime - t2 < 6f && Mod.Interior.Inside)
			{
				if (Time.unscaledTime - t2 > told + 0.75f && Mod.Interior.Inside)
				{
					told = Time.unscaledTime - t2;
					Note(string.Format("{0}: going out at {1:F1} s: eye {2}, door at {3:F2},{4:F2}, a step on: {5}", id, told, Mod.Interior.Eye, room.Fit.DoorWallX, room.Fit.DoorWallY, Mod.Interior.BlockedWhyForTest(Mod.Interior.Eye + outWay * 0.05f) ?? "free"));
				}
				yield return null;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return CivilTest.Wait(0.25f);
			Check(id + ": walked out through the door", !Mod.Interior.Inside && WorldBoxMod.DoorStepsOut > outBefore);
			FaceOf(place, plan, out var face, out var faceOut);
			float num4 = Vector2.Dot(_you.current_position - new Vector2(face.x, face.y), faceOut);
			Check(id + ": in the street before the door", num4 > -0.05f && num4 < 1.2f, $"at {_you.current_position}, {num4:F2} out from the door's face {face}");
			Check(id + ": the house knows you're out", !_you.is_inside_building);
			yield return CivilTest.Wait(0.6f);
			Mod.SaveFrame("fitroom_" + id + "_out.png");
			Mod.EnterHouseForTest(b);
			yield return CivilTest.Wait(0.5f);
			Check(id + ": E at the door goes in", Mod.Interior.Inside && Mod.Interior.House == b && Mod.Interior.Fitted);
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			num4 = Vector2.Dot(_you.current_position - new Vector2(face.x, face.y), faceOut);
			Check(id + ": out again in the street", !Mod.Interior.Inside && num4 > -0.05f && num4 < 1.2f, $"inside {Mod.Interior.Inside}, at {_you.current_position}, {num4:F2} out from the door's face {face}");
		}

		private static void FaceOf(HouseWorld.Placement place, HousePlan plan, out Vector2 face, out Vector2 outward)
		{
			float num = (place.Mirror ? (0f - place.Scale) : place.Scale);
			face = new Vector2(place.X + plan.Door.X * num, place.Y + plan.Door.Y * place.Scale);
			outward = new Vector2(plan.Door.OutX * (place.Mirror ? (-1f) : 1f), plan.Door.OutY).normalized;
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
