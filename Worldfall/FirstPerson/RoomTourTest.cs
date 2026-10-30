using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RoomTourTest : CivilTest
	{
		protected override string Prefix => "ROOMTOUR";

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
			City city = actor.city;
			Actor you = ((Founded.Count > 1) ? Founded[1] : actor);
			yield return Possess(you);
			if (!Check("first person", InControl(you)))
			{
				yield break;
			}
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Mod.HintUntil = 0f;
			int savedMode = Mod.Settings.TimeMode;
			Mod.Settings.TimeMode = ((!Options.Contains("night")) ? 1 : 3);
			List<(string, string)> list = new List<(string, string)>();
			string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string text in array)
			{
				for (int j = 0; j <= 5; j++)
				{
					string[] array2 = new string[3] { "", "_b", "_c" };
					foreach (string arg in array2)
					{
						list.Add(($"house_{text}_{j}{arg}", $"house_{text}_{j}"));
					}
				}
				list.Add(("tent_" + text, "tent_" + text));
				list.Add(("tent_" + text + "_b", "tent_" + text));
			}
			list.Add(("tent", "tent_human"));
			List<string> only = new List<string>();
			array = Options.Split(' ', '\n', '\r', '\t');
			foreach (string text2 in array)
			{
				if (text2.StartsWith("only"))
				{
					only.Add(text2.Substring(4));
				}
			}
			foreach (var item in list)
			{
				var (model, asset) = item;
				if ((only.Count <= 0 || only.Exists((string o) => model.StartsWith(o))) && Models.Has(model))
				{
					yield return Part(model, Tour(model, asset, city, you));
					Models.FamilyForTest = null;
				}
			}
			Models.FamilyForTest = null;
			Mod.Settings.TimeMode = savedMode;
		}

		private IEnumerator Tour(string model, string asset, City city, Actor you)
		{
			Models.FamilyForTest = model;
			Building b = AddHouse(asset, city);
			if (!Check(model + " built", b != null && HouseInterior.IsEnterable(b)))
			{
				Models.FamilyForTest = null;
				yield break;
			}
			Homes.SetOwner(b, you);
			int num = 0;
			string[] array = Options.Split(' ');
			foreach (string text in array)
			{
				if (text.StartsWith("face") && text.Length == 5)
				{
					num = text[4] - 48;
				}
			}
			if (num != 0)
			{
				Facing.Set(b, (Facing.Next(b.asset, 0) != 0) ? num : 0);
			}
			Put(you, b.current_position + Facing.Turn(new Vector2(0f, -3.5f), Facing.Of(b)));
			HouseWorld.Placement p;
			for (float t = 0f; t < 5f; t += 0.2f)
			{
				if (HouseWorld.Of(b, out p))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(0.3f);
			Vector2 start;
			if (Options.Contains("walkin"))
			{
				int f = Facing.Of(b);
				Vector2 dp;
				bool door = Steering.DoorOf(b, out dp);
				Vector2 vector = Facing.Turn(new Vector2(0f, -1f), f);
				start = (door ? dp : b.current_position) + vector * 1.2f;
				Put(you, start);
				Vector2 vector2 = (door ? dp : b.current_position) - start;
				Mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
				Mod.SetPitchDegrees(-5f);
				yield return CivilTest.Wait(0.8f);
				Mod.SaveFrame($"tour_{model}_f{f}_door.png");
				HouseWorld.Of(b, out var p2);
				Interior interior = ((Mod.DoorRoom.Inside && Mod.DoorRoom.House == b) ? Mod.DoorRoom.Room : null);
				Note(string.Format("{0} face {1}: drawn at scale {2:F3} face {3} mirror {4}; door room {5}", model, f, p2.Scale, p2.Face, p2.Mirror, (interior == null) ? "none" : $"{interior.MaxX - interior.MinX:F2}x{interior.MaxY - interior.MinY:F2} scale {interior.Fit?.Scale:F3}"));
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				for (float t = 0f; t < 4f; t += 0.1f)
				{
					if (Mod.Interior.Inside)
					{
						break;
					}
					yield return CivilTest.Wait(0.1f);
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				bool walkedIn = Mod.Interior.Inside && Mod.Interior.House == b;
				if (!walkedIn)
				{
					HouseWorld.Of(b, out var p3);
					Interior interior2 = ((Mod.DoorRoom.Inside && Mod.DoorRoom.House == b) ? Mod.DoorRoom.Room : null);
					Note(string.Format("{0} face {1}: NOT IN; drawn now at scale {2:F3} at {3:F2},{4:F2}; door room {5}", model, f, p3.Scale, p3.X, p3.Y, (interior2 == null) ? "none" : $"{interior2.MaxX - interior2.MinX:F2}x{interior2.MaxY - interior2.MinY:F2} scale {interior2.Fit?.Scale:F3}"));
				}
				Check(model + $" face {f}: walked in", !HouseInterior.WalksIn(b.asset) || walkedIn, $"door {door} {dp}, from {start}, now at {you.current_position}, inside {Mod.Interior.Inside}, fitted {Mod.Interior.Fitted}, plan face {(HouseWorld.Of(b, out var p4) ? p4.Face : (-1))}");
				yield return CivilTest.Wait(0.5f);
				Mod.SaveFrame($"tour_{model}_f{f}_walkin.png");
				if (walkedIn)
				{
					PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
					yield return CivilTest.Wait(0.6f);
					PossessionHooks.ScriptedInput = Vector2.zero;
					yield return CivilTest.Wait(0.3f);
					Mod.SaveFrame($"tour_{model}_f{f}_inside.png");
					Note($"{model} face {f}: inside at {Mod.Interior.Eye}, room {Mod.Interior.Room.MinX:F1}..{Mod.Interior.Room.MaxX:F1} x {Mod.Interior.Room.MinY:F1}..{Mod.Interior.Room.MaxY:F1}");
					Mod.ExitHouseForTest();
					yield return CivilTest.Wait(0.5f);
				}
				Put(you, b.current_position + Facing.Turn(new Vector2(0f, -3.5f), f));
				yield return CivilTest.Wait(0.3f);
				start = default(Vector2);
			}
			if (Options.Contains("old"))
			{
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-5f);
				yield return CivilTest.Wait(0.6f);
				Mod.SaveFrame("tour_" + model + "_old_street.png");
				Put(you, b.current_position + new Vector2(0f, -1.8f));
				yield return CivilTest.Wait(0.6f);
				Mod.SaveFrame("tour_" + model + "_old_atdoor.png");
				if (Options.Contains("onplot"))
				{
					Put(you, b.current_position + new Vector2(0f, 0.3f));
					yield return CivilTest.Wait(0.8f);
					Note($"{model}: on the plot at {you.current_position}, floor {Mod.FloorForTest:F2} lift {Mod.LiftForTest:F2}");
					Mod.SaveFrame("tour_" + model + "_old_onplot.png");
					PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
					yield return CivilTest.Wait(0.7f);
					PossessionHooks.ScriptedInput = Vector2.zero;
					yield return CivilTest.Wait(0.2f);
					Note($"{model}: walked on the plot to {you.current_position}, floor {Mod.FloorForTest:F2} lift {Mod.LiftForTest:F2}");
					Mod.SaveFrame("tour_" + model + "_old_onplot_walk.png");
					Put(you, b.current_position + new Vector2(0f, -1.8f));
					yield return CivilTest.Wait(0.6f);
				}
			}
			for (int f = 0; f < 4; f++)
			{
				if (Mod.Interior.Inside && Mod.Interior.House == b)
				{
					break;
				}
				Mod.EnterHouseForTest(b);
				for (float t = 0f; t < 1.5f; t += 0.1f)
				{
					if (Mod.Interior.Inside && Mod.Interior.House == b)
					{
						break;
					}
					yield return CivilTest.Wait(0.1f);
				}
			}
			if (Options.Contains("old") && Mod.Interior.Inside && Mod.Interior.House == b && !Mod.Interior.Fitted)
			{
				Interior r = Mod.Interior.Room;
				Note($"{model}: room of old {r.MaxX - r.MinX:F2}x{r.MaxY - r.MinY:F2}, {r.Meshes.Count} models, {r.Windows.Count} windows, eye {Mod.Interior.Eye}, house at {b.current_position}");
				start = Mod.Interior.Eye;
				(string, float)[] array2 = new(string, float)[4]
				{
					("e", 0f),
					("n", 90f),
					("w", 180f),
					("s", 270f)
				};
				for (int f = 0; f < array2.Length; f++)
				{
					var (view, num2) = array2[f];
					Mod.Interior.Eye = start;
					Mod.SetYaw(num2 * (MathF.PI / 180f));
					Mod.SetPitchDegrees(-8f);
					yield return CivilTest.Wait(0.4f);
					Mod.SaveFrame("tour_" + model + "_old_" + view + ".png");
					Note($"{model} {view}: backdrop frames {HouseInterior.BackdropFrames}");
				}
				Mod.Interior.Eye = new Vector2((r.MinX + r.MaxX) * 0.5f, (r.MinY + r.MaxY) * 0.5f);
				Mod.SetYaw(MathF.PI / 2f);
				yield return CivilTest.Wait(0.4f);
				Mod.SaveFrame("tour_" + model + "_old_mid.png");
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return CivilTest.Wait(0.8f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				Note($"{model}: walked to {Mod.Interior.Eye}");
				yield return CivilTest.Wait(0.2f);
				Mod.SaveFrame("tour_" + model + "_old_walk.png");
				PossessionHooks.ScriptedInput = new Vector2(1f, 0f);
				yield return CivilTest.Wait(0.6f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return CivilTest.Wait(0.2f);
				Mod.SaveFrame("tour_" + model + "_old_walk2.png");
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(0.8f);
				Mod.SaveFrame("tour_" + model + "_old_after.png");
				Remove(b);
				yield break;
			}
			if (!HouseInterior.WalksIn(b.asset))
			{
				Check(model + ": in, the room of old", Mod.Interior.Inside && Mod.Interior.House == b && !Mod.Interior.Fitted, string.Format("inside {0}, house {1}, fitted {2}", Mod.Interior.Inside, (Mod.Interior.House == b) ? "this" : "another", Mod.Interior.Fitted));
				Mod.SaveFrame("tour_" + model + "_old_room.png");
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(0.5f);
				Remove(b);
				yield break;
			}
			if (!Check(model + ": in", Mod.Interior.Inside && Mod.Interior.House == b && Mod.Interior.Fitted, string.Format("inside {0}, house {1}, fitted {2}, drawn {3}", Mod.Interior.Inside, (Mod.Interior.House == b) ? "this" : "another", Mod.Interior.Fitted, HouseWorld.Of(b, out p))))
			{
				Remove(b);
				yield break;
			}
			Interior room = Mod.Interior.Room;
			FitInfo fit = room.Fit;
			Note($"{model}: room {room.MaxX - room.MinX:F2}x{room.MaxY - room.MinY:F2}, {fit.StoreyZ.Length} storeys, {room.Meshes.Count} models, drawn as {HouseWorldName(b)}");
			Mod.SetPitchDegrees(-8f);
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("tour_" + model + "_door.png");
			for (int f = 0; f < fit.StoreyZ.Length; f++)
			{
				if (!Middle(room, f, out start))
				{
					Note($"{model}: storey {f} has no open middle");
					continue;
				}
				Mod.Interior.Eye = start;
				if (f == 0)
				{
					RoomFloors.GroundForTest();
				}
				else
				{
					RoomFloors.FeetForTest(fit.StoreyZ[f]);
				}
				(string, float, float)[] array3 = new(string, float, float)[6]
				{
					("e", 0f, -8f),
					("n", 90f, -8f),
					("w", 180f, -8f),
					("s", 270f, -8f),
					("up", 45f, 70f),
					("down", 225f, -60f)
				};
				for (int j = 0; j < array3.Length; j++)
				{
					var (view, num3, pitchDegrees) = array3[j];
					Mod.Interior.Eye = start;
					if (f > 0)
					{
						RoomFloors.FeetForTest(fit.StoreyZ[f]);
					}
					Mod.SetYaw(num3 * (MathF.PI / 180f));
					Mod.SetPitchDegrees(pitchDegrees);
					yield return CivilTest.Wait(0.3f);
					Mod.SaveFrame($"tour_{model}_s{f}_{view}.png");
				}
				start = default(Vector2);
			}
			RoomFloors.GroundForTest();
			Remove(b);
		}

		private static bool Middle(Interior room, int k, out Vector2 at)
		{
			FitInfo fit = room.Fit;
			float num = (room.MinX + room.MaxX) * 0.5f;
			float num2 = (room.MinY + room.MaxY) * 0.5f;
			float num3 = float.MaxValue;
			at = default(Vector2);
			for (float num4 = room.MinY; num4 < room.MaxY; num4 += 0.1f)
			{
				for (float num5 = room.MinX; num5 < room.MaxX; num5 += 0.1f)
				{
					if (!fit.Clear(k, num5, num4, 0.35f))
					{
						continue;
					}
					float[] stairwell = fit.Stairwell;
					if (stairwell != null && num5 > stairwell[0] - 0.3f && num5 < stairwell[2] + 0.3f && num4 > stairwell[1] - 0.3f && num4 < stairwell[3] + 0.3f)
					{
						continue;
					}
					bool flag = false;
					foreach (float[] solid in room.Solids)
					{
						bool num6;
						if (k != 0)
						{
							if (solid.Length < 6)
							{
								continue;
							}
							num6 = Mathf.Abs(solid[4] - fit.StoreyZ[k]) < 0.3f;
						}
						else
						{
							num6 = solid.Length < 6;
						}
						if (num6 && num5 > solid[0] - 0.25f && num5 < solid[2] + 0.25f && num4 > solid[1] - 0.25f && num4 < solid[3] + 0.25f)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						float num7 = (num5 - num) * (num5 - num) + (num4 - num2) * (num4 - num2);
						if (num7 < num3)
						{
							num3 = num7;
							at = new Vector2(num5, num4);
						}
					}
				}
			}
			return num3 < float.MaxValue;
		}

		private static string HouseWorldName(Building b)
		{
			if (!HouseWorld.Of(b, out var p) || p.Model == null)
			{
				return "?";
			}
			return p.Model.Name;
		}

		private void Remove(Building b)
		{
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			try
			{
				b?.removeBuildingFinal();
			}
			catch
			{
			}
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
