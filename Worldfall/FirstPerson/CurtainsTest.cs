using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class CurtainsTest : CivilTest
	{
		private const int All = -1;

		private Building _house;

		private Actor _you;

		private Actor _a;

		private Actor _b;

		private int _win = -1;

		protected override string Prefix => "CURTAINS";

		protected override IEnumerator Body()
		{
			Settings settings = Mod.Settings;
			settings.GpuRender = true;
			settings.WindowView = 1;
			if (Options.Contains("hd"))
			{
				settings.Resolution = 3;
			}
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			Actor king = Village("human", 9, 0, worldTile);
			if (!Check("a village", king != null && king.city != null))
			{
				yield break;
			}
			_house = AddHouse("house_human_1", king.city);
			if (!Check("a house of the town", _house != null && HouseInterior.IsEnterable(_house)))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Founded)
			{
				if (item != null && item != king && item.isAlive() && !item.isWarrior())
				{
					list.Add(item);
				}
			}
			if (list.Count < 2)
			{
				list.Insert(0, king);
			}
			_you = ((list.Count > 2) ? list[2] : Founded[1]);
			_a = list[0];
			_b = list[1];
			try
			{
				_a.setHomeBuilding(_house);
				_b.setHomeBuilding(_house);
			}
			catch (Exception ex)
			{
				Note("homes: " + ex.Message);
			}
			yield return Possess(_you);
			if (Check("first person", InControl(_you)))
			{
				Check("the house is lived in, and not yours", _house.hasResidents() && Workbench.HomeOf(_you) != _house);
				Mod.Day.TimeOfDay = 0.45f;
				yield return Part("from the street", Street());
				yield return Part("dusk and dawn", DuskAndDawn());
				yield return Part("in the room", Room());
				yield return Part("nobody sees in", Law());
				yield return Part("indoors only the huge get in", Sounds());
				yield return Part("a round of the windows at dusk", Round());
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.5f);
				Check("out again", !Mod.Interior.Inside);
				yield return Part("a ship's cabin", Cabin(king));
				Mod.DebugEyeSet = false;
			}
		}

		private void LookAtHouse(float back)
		{
			Mod.DebugEye = _house.current_position + new Vector2(0.3f, 0f - back);
			Mod.DebugEyeHeight = 0f;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(4f);
		}

		private IEnumerator Street()
		{
			LookAtHouse(4.5f);
			yield return CivilTest.Wait(1f);
			Curtains.SetMaskForTest(_house, -1);
			yield return CivilTest.Wait(2.5f);
			int streetPanelsForTest = Curtains.StreetPanelsForTest;
			Check("drawn curtains show in its windows from the street", streetPanelsForTest > 0, $"{streetPanelsForTest} panels");
			Mod.SaveFrame("curtains_street_drawn.png");
			Curtains.SetMaskForTest(_house, 0);
			yield return CivilTest.Wait(0.4f);
			Mod.SaveFrame("curtains_street_opening.png");
			yield return CivilTest.Wait(1.2f);
			Check("opened, none show", Curtains.StreetPanelsForTest == 0, $"{Curtains.StreetPanelsForTest} panels");
			Mod.SaveFrame("curtains_street_open.png");
			LookAtHouse(2.6f);
			Curtains.SetMaskForTest(_house, -1);
			yield return CivilTest.Wait(1.5f);
			Mod.SaveFrame("curtains_street_close_drawn.png");
			Curtains.SetMaskForTest(_house, 0);
			yield return CivilTest.Wait(1.5f);
			Mod.SaveFrame("curtains_street_close_open.png");
			Mod.Day.TimeOfDay = 0.95f;
			Curtains.SetMaskForTest(_house, -1);
			yield return CivilTest.Wait(2f);
			Mod.SaveFrame("curtains_street_close_night_drawn.png");
			Curtains.SetMaskForTest(_house, 0);
			yield return CivilTest.Wait(1.5f);
			Mod.SaveFrame("curtains_street_close_night_open.png");
			Mod.Day.TimeOfDay = 0.45f;
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator DuskAndDawn()
		{
			LookAtHouse(4.5f);
			Curtains.StampForTest(_house, int.MinValue);
			Mod.Day.TimeOfDay = 0.9f;
			yield return CivilTest.Wait(1.5f);
			Check("at dusk the household draws them", Curtains.MaskForTest(_house) == -1, $"mask {Curtains.MaskForTest(_house)}");
			Mod.SaveFrame("curtains_street_night.png");
			Mod.Day.TimeOfDay = 0.45f;
			yield return CivilTest.Wait(1.5f);
			Check("at dawn they open them", Curtains.MaskForTest(_house) == 0, $"mask {Curtains.MaskForTest(_house)}");
			Building had = null;
			try
			{
				had = _you.getHomeBuilding();
				_you.setHomeBuilding(_house);
			}
			catch
			{
			}
			if (Workbench.HomeOf(_you) == _house)
			{
				Curtains.StampForTest(_house, int.MinValue);
				Mod.Day.TimeOfDay = 0.9f;
				yield return CivilTest.Wait(1.2f);
				Check("your own home's stay as you left them at dusk", Curtains.MaskForTest(_house) == 0, $"mask {Curtains.MaskForTest(_house)}");
				try
				{
					if (had != null)
					{
						_you.setHomeBuilding(had);
					}
					else
					{
						_you.clearHomeBuilding();
					}
				}
				catch
				{
				}
				Mod.Day.TimeOfDay = 0.45f;
				yield return CivilTest.Wait(0.3f);
			}
			else
			{
				Note("couldn't make the house yours: own-home check skipped");
			}
			Curtains.SetMaskForTest(_house, 0);
		}

		private IEnumerator Room()
		{
			Mod.DebugEyeSet = false;
			Mod.EnterHouseForTest(_house);
			yield return CivilTest.Wait(1f);
			if (!Check("in the house", Mod.Interior.Inside && Mod.Interior.House == _house))
			{
				yield break;
			}
			Interior room = Mod.Interior.Room;
			int num = Curtains.WindowsForTest(room);
			Check("a pair of curtains on every window", num > 0 && num == room.Windows.Count, $"{num} of {room.Windows.Count} windows");
			if (num <= 0)
			{
				yield break;
			}
			float num2 = float.MaxValue;
			for (int i = 0; i < room.Windows.Count; i++)
			{
				if (room.Windows[i].Center.Z < num2)
				{
					num2 = room.Windows[i].Center.Z;
					_win = i;
				}
			}
			LightWindow w = room.Windows[_win];
			Vector2 inward = new Vector2(w.Inward.X, w.Inward.Y).normalized;
			Vector2 eye = new Vector2(w.Center.X, w.Center.Y) + inward * 1.1f;
			for (float num3 = 0.8f; num3 <= 1.6f; num3 += 0.1f)
			{
				Vector2 vector = new Vector2(w.Center.X, w.Center.Y) + inward * num3;
				if (room.Fit == null || room.Fit.Clear(0, vector.x, vector.y, 0.25f))
				{
					eye = vector;
					break;
				}
			}
			Mod.Interior.Eye = eye;
			Mod.SetYaw(Mathf.Atan2(0f - inward.y, 0f - inward.x));
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(1.5f);
			int num4 = Curtains.AimedForTest(room, Mod.View);
			Check("looking at the window picks it", num4 == _win, $"aimed {num4}, window {_win}, eye {Mod.View.X:F2},{Mod.View.Y:F2},{Mod.View.Z:F2}");
			Check("open at noon", Curtains.OpenForTest(room, _win) <= 0.01f, $"{Curtains.OpenForTest(room, _win):F2}");
			int from = HouseInterior.BackdropFrames;
			yield return CivilTest.Wait(1f);
			int num5 = HouseInterior.BackdropFrames - from;
			Check("the street shows through it open", num5 > 5, $"{num5} outdoor frames in a second");
			Mod.SaveFrame("curtains_room_open.png");
			Curtains.SetMaskForTest(_house, -1);
			yield return CivilTest.Wait(0.45f);
			float num6 = Curtains.OpenForTest(room, _win);
			Check("they draw across over a moment", num6 > 0.2f && num6 < 0.8f, $"{num6:F2} after 0.45 s");
			Mod.SaveFrame("curtains_room_drawing.png");
			yield return CivilTest.Wait(1f);
			Check("drawn", Curtains.OpenForTest(room, _win) >= 0.99f, $"{Curtains.OpenForTest(room, _win):F2}");
			Check("a drawn window hides the street", Curtains.Covers(room, w));
			Check("all drawn, door shut: no opening left to see the street through", !Mod.Interior.PortalInView(Mod.View), $"door {Mod.Interior.DoorOpen:F2}");
			from = HouseInterior.BackdropFrames;
			yield return CivilTest.Wait(1f);
			int num7 = HouseInterior.BackdropFrames - from;
			Check("all drawn: the street isn't drawn behind them", num7 == 0, $"{num7} outdoor frames in a second");
			Mod.SaveFrame("curtains_room_drawn.png");
			Mod.SetYaw(Mathf.Atan2(0f - inward.y, 0f - inward.x) + 0.7f);
			yield return CivilTest.Wait(0.3f);
			Mod.SaveFrame("curtains_room_drawn_side.png");
			Mod.SetYaw(Mathf.Atan2(0f - inward.y, 0f - inward.x));
			Curtains.SetMaskForTest(_house, 0);
			yield return CivilTest.Wait(1.3f);
			Check("opened again", Curtains.OpenForTest(room, _win) <= 0.01f, $"{Curtains.OpenForTest(room, _win):F2}");
			Mod.SaveFrame("curtains_room_opened.png");
		}

		private IEnumerator Law()
		{
			if (!Mod.Interior.Inside)
			{
				yield break;
			}
			Actor actor = CivilTest.SpawnNear("human", _house.current_position + new Vector2(0f, -3.5f));
			if (!Check("a townsman in the street", actor != null))
			{
				yield break;
			}
			try
			{
				actor.joinCity(_a.city);
				actor.makeWait(30f);
			}
			catch (Exception ex)
			{
				Note("townsman: " + ex.Message);
			}
			Interior room = Mod.Interior.Room;
			Vector2 vector = new Vector2(room.DoorX, room.DoorY);
			float best = 0f;
			for (float num = room.MinY; num < room.MaxY; num += 0.1f)
			{
				for (float num2 = room.MinX; num2 < room.MaxX; num2 += 0.1f)
				{
					float magnitude = (new Vector2(num2, num) - vector).magnitude;
					if (magnitude > best && (room.Fit == null || room.Fit.Clear(0, num2, num, 0.3f)))
					{
						best = magnitude;
						Mod.Interior.Eye = new Vector2(num2, num);
					}
				}
			}
			yield return CivilTest.Wait(2f);
			Note($"standing {best:F2} from the door, door {Mod.Interior.DoorOpen:F2}");
			Vector2 at = _house.current_position;
			Curtains.SetMaskForTest(_house, -1);
			yield return CivilTest.Wait(1.3f);
			List<Actor> list = FirstPerson.Law.Witnesses(_a, _you, at);
			int num3 = OutOfHouse(list);
			Check("all drawn, the door shut: nobody in the street sees in", num3 == 0, $"{num3} saw from outside, {list.Count} in all, door {Mod.Interior.DoorOpen:F2}");
			Curtains.SetMaskForTest(_house, 0);
			yield return CivilTest.Wait(0.3f);
			list = FirstPerson.Law.Witnesses(_a, _you, at);
			int num4 = OutOfHouse(list);
			Check("curtains open: still only the room sees (the street never does)", num4 == 0, $"{num4} saw from outside, {list.Count} in all");
		}

		private int OutOfHouse(List<Actor> saw)
		{
			int num = 0;
			foreach (Actor item in saw)
			{
				bool flag = false;
				try
				{
					flag = item.is_inside_building && item.inside_building == _house;
				}
				catch
				{
				}
				if (!flag)
				{
					num++;
				}
			}
			return num;
		}

		private IEnumerator Sounds()
		{
			if (Mod.Interior.Inside)
			{
				FirstPersonAudio audio = Mod.Audio;
				if (WorldBoxMod.QuietTestCopy)
				{
					WorldBoxMod.QuietLevelForTest = 1E-05f;
				}
				yield return CivilTest.Wait(0.5f);
				Check("indoors: the walls close in", audio.MuffleLevel > 0.99f, $"{audio.MuffleLevel:F2}");
				Vector2 at = _house.current_position;
				MusicBox.playSound("event:/SFX/HIT/HitFlesh", at.x + 7f, at.y);
				MusicBox.playSound("event:/SFX/EXPLOSIONS/ExplosionMiddle", at.x - 7f, at.y + 2f);
				yield return CivilTest.Wait(0.2f);
				float num = audio.KeepForTest("HitFlesh");
				float num2 = audio.KeepForTest("ExplosionMiddle");
				Check("a blow outside isn't heard", num >= 0f && num < 0.01f, $"keep {num:F2}");
				Check("an explosion outside gets in, softer", num2 > 0.4f && num2 < 0.7f, $"keep {num2:F2}");
				yield return CivilTest.Wait(1.5f);
				MusicBox.playSound("event:/SFX/HIT/HitWood", at.x, at.y);
				yield return CivilTest.Wait(0.2f);
				float num3 = audio.KeepForTest("HitWood");
				Check("a sound in the house itself is heard whole", num3 > 0.99f, $"keep {num3:F2}");
				WorldBoxMod.QuietLevelForTest = -1f;
			}
		}

		private IEnumerator Cabin(Actor king)
		{
			Vector2 current_position = _house.current_position;
			Vector2? vector = null;
			for (int i = 4; i < 90; i += 2)
			{
				if (vector.HasValue)
				{
					break;
				}
				for (int j = 0; j < 24; j++)
				{
					if (vector.HasValue)
					{
						break;
					}
					float f = (float)j * MathF.PI / 12f;
					int num = (int)(current_position.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(current_position.y + Mathf.Sin(f) * (float)i);
					bool flag = true;
					for (int k = -2; k <= 2 && flag; k++)
					{
						for (int l = -2; l <= 2 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num + l, num2 + k);
							flag = tile != null && tile.Type != null && tile.Type.liquid && !tile.Type.lava;
						}
					}
					if (flag)
					{
						vector = new Vector2((float)num + 0.5f, (float)num2 + 0.5f);
					}
				}
			}
			if (!vector.HasValue)
			{
				Note("no open water near: cabin skipped");
				yield break;
			}
			Actor ship = null;
			try
			{
				ship = World.world.units.createNewUnit("boat_transport_human", World.world.GetTile((int)vector.Value.x, (int)vector.Value.y));
				if (king.kingdom != null)
				{
					ship?.setKingdom(king.kingdom);
				}
			}
			catch (Exception ex)
			{
				Note("ship: " + ex.Message);
			}
			if (!Check("a galleon at sea", ship != null))
			{
				yield break;
			}
			Mod.EnterCabin(ship);
			yield return CivilTest.Wait(1.5f);
			BoatCabin cabin = Mod.Cabin;
			if (Check("in her cabin", cabin?.Inside ?? false))
			{
				Interior room = cabin.Room;
				int num3 = Curtains.WindowsForTest(room);
				Check("curtains on her cabin's windows", num3 > 0 && num3 == room.Windows.Count, $"{num3} of {room.Windows.Count}");
				if (num3 <= 0)
				{
					Mod.LeaveCabinForTest();
					yield break;
				}
				LightWindow w = room.Windows[0];
				Vector2 normalized = new Vector2(w.Inward.X, w.Inward.Y).normalized;
				cabin.Eye = new Vector2(w.Center.X, w.Center.Y) + normalized * 0.9f;
				Mod.SetYaw(Mathf.Atan2(0f - normalized.y, 0f - normalized.x));
				Mod.SetPitchDegrees(0f);
				yield return CivilTest.Wait(1f);
				int num4 = Curtains.AimedForTest(room, Mod.View);
				Note($"cabin: eye z {Mod.View.Z:F2}, room top {room.Top:F2}, window at z {w.Center.Z:F2}, aboard {Mod.Host != null && Mod.Host.is_inside_boat}, eye height {Mod.Settings.EyeHeight:F2}");
				Check("looking at a cabin window picks it", num4 == 0, $"aimed {num4}");
				Mod.SaveFrame("curtains_cabin_open.png");
				Curtains.SetShipMaskForTest(ship, -1);
				yield return CivilTest.Wait(1.4f);
				Check("her curtains draw", Curtains.OpenForTest(room, 0) >= 0.99f, $"{Curtains.OpenForTest(room, 0):F2}");
				Mod.SaveFrame("curtains_cabin_drawn.png");
				Curtains.SetShipMaskForTest(ship, 0);
				yield return CivilTest.Wait(1.4f);
				Check("and open", Curtains.OpenForTest(room, 0) <= 0.01f, $"{Curtains.OpenForTest(room, 0):F2}");
				Mod.LeaveCabinForTest();
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator Round()
		{
			if (!Mod.Interior.Inside)
			{
				yield break;
			}
			HomeLife.SendHome(_a);
			for (float t = 0f; t < 40f; t += 0.5f)
			{
				if (_a.is_inside_building && _a.inside_building == _house)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(2f);
			bool flag = false;
			using (IEnumerator<Actor> enumerator = Mod.Interior.WalkersAwake.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					_ = enumerator.Current;
					flag = true;
				}
			}
			if (!flag)
			{
				Note("nobody up in the room: round skipped");
				yield break;
			}
			Curtains.StampForTest(_house, Calendar.Day * 2);
			Mod.Day.TimeOfDay = 0.9f;
			yield return CivilTest.Wait(1f);
			Check("at dusk someone goes round the windows", Curtains.ErrandRunningForTest, $"mask {Curtains.MaskForTest(_house)}");
			Mod.SetPitchDegrees(-8f);
			for (float t = 0f; t < 40f; t += 0.5f)
			{
				if (!Curtains.ErrandRunningForTest)
				{
					break;
				}
				if (Mathf.Abs(t - 3f) < 0.01f)
				{
					Mod.SaveFrame("curtains_round.png");
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("every window drawn by the end of the round", Curtains.MaskForTest(_house) == -1, $"mask {Curtains.MaskForTest(_house)}");
			Mod.Day.TimeOfDay = 0.45f;
		}
	}
}
