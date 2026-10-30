using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PosesTest : CivilTest
	{
		private Actor _me;

		private Actor _king;

		protected override string Prefix => "POSE";

		protected override IEnumerator Body()
		{
			_king = Village("human", 12, 3);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _king && item.isAdult())
				{
					_me = item;
					break;
				}
			}
			if (!Check("a villager", _me != null))
			{
				yield break;
			}
			foreach (Actor item2 in Founded)
			{
				try
				{
					((BaseSimObject)item2).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(_me);
			if (Check("first person", InControl(_me)))
			{
				Mod.Settings.PoseView = 1;
				Mod.Settings.ShowBody = 2;
				if (Options.Contains("cpu"))
				{
					Mod.Settings.GpuRender = false;
				}
				Mod.Settings.Resolution = 2;
				Config.paused = true;
				yield return Part("ground", Ground());
				yield return Part("kneel", Kneel());
				yield return Part("rest", Rest());
				yield return Part("rock", Rock());
				yield return Part("step", Step());
				yield return Part("chair", Chair());
				yield return Part("pier", Pier());
				Config.paused = false;
				yield return Part("about town", AboutTown());
			}
		}

		private IEnumerator Ground()
		{
			Mod.SetPitchDegrees(-8f);
			Mod.SetYaw(0.3f);
			yield return CivilTest.Wait(0.8f);
			float stand = Mod.EyeForTest.z;
			WorldBoxMod.PoseTapForTest = true;
			yield return CivilTest.Wait(1.6f);
			Check("P sits you down", Mod.PoseNow == RestStyle.Ground && Mod.PoseAmount >= 0.99f, $"{Mod.PoseNow} {Mod.PoseAmount:F2}");
			float num = ((People.HostBody > 0f) ? People.HostBody : 0.9f);
			Check("the eye down at your head", stand - Mod.EyeForTest.z > 0.3f * num, $"{stand:F2} -> {Mod.EyeForTest.z:F2} (body {num:F2})");
			Check("the view out behind you", Mod.PoseViewOut > 0.99f && (Mod.PoseCameraForTest - Mod.EyeForTest).magnitude > 0.8f * num, $"out {Mod.PoseViewOut:F2}, camera {(Mod.PoseCameraForTest - Mod.EyeForTest).magnitude:F2} from the eye");
			Check("your whole body drawn", Mod.SceneForTest.OwnWhole && WholeBody(), $"whole {Mod.SceneForTest.OwnWhole}");
			Check("no hands before your eyes", Mod.Renderer.OverlayMeshes.Count == 0, Mod.Renderer.OverlayMeshes.Count + " overlay meshes");
			Mod.SaveFrame("pose_ground_third.png");
			WorldBoxMod.HotbarWheelForTest = 1f;
			yield return CivilTest.Wait(0.9f);
			Check("the wheel takes you into your eyes", Mod.PoseViewOut < 0.01f && Mod.Settings.PoseView == 0, $"out {Mod.PoseViewOut:F2}");
			Mod.SetPitchDegrees(-62f);
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("pose_ground_first_down.png");
			Mod.SetPitchDegrees(-8f);
			WorldBoxMod.HotbarWheelForTest = -1f;
			yield return CivilTest.Wait(0.9f);
			Check("and out again", Mod.PoseViewOut > 0.99f && Mod.Settings.PoseView == 1);
			Vector2 at = _me.current_position;
			Config.paused = false;
			yield return CivilTest.Wait(1.5f);
			Check("you stay where you sit", (_me.current_position - at).magnitude < 0.06f && Mod.PoseNow == RestStyle.Ground, $"moved {(_me.current_position - at).magnitude:F3}");
			Config.paused = true;
			WorldBoxMod.PoseWalkForTest = true;
			yield return CivilTest.Wait(0.15f);
			WorldBoxMod.PoseWalkForTest = false;
			yield return CivilTest.Wait(0.9f);
			Check("walking gets you up", Mod.PoseNow == RestStyle.None, Mod.PoseNow.ToString());
			Check("the eye back up", Mathf.Abs(Mod.EyeForTest.z - stand) < 0.08f, $"{Mod.EyeForTest.z:F2} vs {stand:F2}");
			Check("hands back", Mod.Renderer.OverlayMeshes.Count > 0 || Mod.Holstered);
		}

		private IEnumerator Kneel()
		{
			CivilTest.StandBefore(_king, _me, 1.3f);
			Vector2 to = _king.current_position - _me.current_position;
			Mod.SetYaw(Mathf.Atan2(to.y, to.x));
			yield return CivilTest.Wait(0.6f);
			int before = Chronicle.Opinion(_king, _me);
			Mod.PoseForTest(RestStyle.Kneel);
			yield return CivilTest.Wait(1.8f);
			Check("kneeling", Mod.PoseNow == RestStyle.Kneel && Mod.PoseAmount >= 0.99f);
			int after = Chronicle.Opinion(_king, _me);
			Check("the king is pleased", after == before + 3, $"{before} -> {after}");
			Check("and bids you rise", Mod.Talk.Speaker == _king, (Mod.Talk.Speaker != null) ? (WorldBoxMod.Describe(Mod.Talk.Speaker) + ": " + Mod.Talk.Line) : "nobody");
			Mod.SetYaw(Mathf.Atan2(to.y, to.x) + 0.9f);
			yield return CivilTest.Wait(0.4f);
			Mod.SaveFrame("pose_kneel_third.png");
			WorldBoxMod.PoseTapForTest = true;
			yield return CivilTest.Wait(1f);
			Mod.PoseForTest(RestStyle.Kneel);
			yield return CivilTest.Wait(1.6f);
			Check("kneeling again so soon pleases no more", Chronicle.Opinion(_king, _me) == after);
			WorldBoxMod.PoseTapForTest = true;
			yield return CivilTest.Wait(1f);
			RestStyle[] array = new RestStyle[3]
			{
				RestStyle.KneelBoth,
				RestStyle.CrossLegged,
				RestStyle.SitBack
			};
			for (int i = 0; i < array.Length; i++)
			{
				RestStyle s = array[i];
				Mod.PoseForTest(s);
				yield return CivilTest.Wait(1.6f);
				Check(s.ToString() + " from the wheel", Mod.PoseNow == s);
				Mod.SaveFrame("pose_" + s.ToString().ToLowerInvariant() + "_third.png");
				WorldBoxMod.PoseTapForTest = true;
				yield return CivilTest.Wait(1f);
			}
			Check("up again", Mod.PoseNow == RestStyle.None);
			Mod.PoseForTest(RestStyle.Kneel);
			yield return CivilTest.Wait(1.4f);
			Mod.PoseForTest(RestStyle.CrossLegged);
			yield return CivilTest.Wait(0.25f);
			Check("a new pose: up out of the old one first", Mod.PoseNow == RestStyle.Kneel && Mod.PoseAmount < 0.9f, $"{Mod.PoseNow} {Mod.PoseAmount:F2}");
			yield return CivilTest.Wait(1.6f);
			Check("then down into the new one", Mod.PoseNow == RestStyle.CrossLegged && Mod.PoseAmount >= 0.99f, $"{Mod.PoseNow} {Mod.PoseAmount:F2}");
			yield return Up();
		}

		private IEnumerator Rest()
		{
			int most = 0;
			int max = 0;
			try
			{
				most = _me.getMaxStamina();
				_me.data.stamina = Mathf.Max(1, most / 10);
				max = _me.getMaxHealth();
				_me.data.health = Mathf.Max(1, max / 2);
			}
			catch
			{
			}
			int stamina = _me.data.stamina;
			int health = _me.getHealth();
			WorldBoxMod.PoseTapForTest = true;
			yield return CivilTest.Wait(5.5f);
			Check("sitting gets your breath back", _me.data.stamina > stamina + most / 10, $"{stamina} -> {_me.data.stamina} of {most}");
			Check("and heals a little", _me.getHealth() > health, $"{health} -> {_me.getHealth()} of {max}");
			WorldBoxMod.PoseTapForTest = true;
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator Up()
		{
			WorldBoxMod.PoseSitForTest = false;
			if (Mod.PoseNow != RestStyle.None)
			{
				WorldBoxMod.PoseTapForTest = true;
				yield return CivilTest.Wait(1.1f);
			}
		}

		private IEnumerator Rock()
		{
			yield return Up();
			Vector2 current_position = _me.current_position;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _me && item.isAlive())
				{
					item.current_position = current_position + new Vector2(-7f, -7f);
					try
					{
						item.makeWait(20f);
					}
					catch
					{
					}
				}
			}
			Building rock = null;
			Vector2 dir = Vector2.right;
			for (int i = 0; i < 8; i++)
			{
				if (rock != null)
				{
					break;
				}
				dir = new Vector2(Mathf.Cos((float)i * MathF.PI / 4f), Mathf.Sin((float)i * MathF.PI / 4f));
				WorldTile tile = World.world.GetTile((int)(current_position.x + dir.x * 1.6f), (int)(current_position.y + dir.y * 1.6f));
				if (tile != null && tile.building == null && tile.Type != null && tile.Type.ground)
				{
					try
					{
						rock = World.world.buildings.addBuilding("mineral_stone", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					}
					catch
					{
					}
				}
			}
			for (int tries = 0; tries < 3; tries++)
			{
				if (rock == null)
				{
					break;
				}
				Vector2 vector = new Vector2(rock.current_tile.posV3.x, rock.current_tile.posV3.y);
				_me.current_position = vector - dir * 1.15f;
				Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
				Mod.SetPitchDegrees(-30f);
				yield return CivilTest.Wait(0.8f);
				if (Mod.SeatForTest(out var seat) && seat.Kind == RestSeatKind.Rock)
				{
					break;
				}
				Note("this stone isn't one to sit on: " + Seats.Why);
				WorldTile tile2 = World.world.GetTile(rock.current_tile.x + 3, rock.current_tile.y);
				Building building = null;
				if (tile2 != null && tile2.building == null && tile2.Type != null && tile2.Type.ground)
				{
					try
					{
						building = World.world.buildings.addBuilding("mineral_stone", tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					}
					catch
					{
					}
				}
				if (building == null)
				{
					break;
				}
				rock = building;
			}
			if (!Check("a rock", rock != null))
			{
				yield break;
			}
			Vector2 vector2 = new Vector2(rock.current_tile.posV3.x, rock.current_tile.posV3.y);
			_me.current_position = vector2 - dir * 1.15f;
			Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			float[] array = new float[4] { -22f, -32f, -14f, -42f };
			foreach (float pitchDegrees in array)
			{
				Mod.SetPitchDegrees(pitchDegrees);
				yield return CivilTest.Wait(0.7f);
				if (Mod.Picked == rock)
				{
					break;
				}
			}
			Check("the rock under the crosshair", Mod.Picked == rock, (Mod.Picked != null) ? Mod.Picked.ToString() : "nothing");
			RestSeat seat2;
			bool flag = Mod.SeatForTest(out seat2);
			Check("a seat on it", flag && seat2.Kind == RestSeatKind.Rock, flag ? $"{seat2.Kind} {seat2.Height:F2} high" : ("none: " + Seats.Why));
			Check("E:Sit", Mod.Prompt != null && Mod.Prompt.Contains("E:Sit"), Mod.Prompt);
			WorldBoxMod.PoseSitForTest = true;
			yield return CivilTest.Wait(1.6f);
			Check("sat on the rock", Mod.PoseNow == RestStyle.Seat && Mod.PoseSeatNow.Kind == RestSeatKind.Rock, Mod.PoseNow.ToString() + " " + Mod.PoseSeatNow.Kind);
			Check("facing away from it", Mathf.Abs(Mathf.DeltaAngle(Mod.PoseSeatNow.Heading * 57.29578f, Mathf.Atan2(0f - dir.y, 0f - dir.x) * 57.29578f)) < 35f);
			Mod.SetPitchDegrees(-8f);
			Mod.SetYaw(Mathf.Atan2(0f - dir.y, 0f - dir.x) + 2.4f);
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("pose_rock_third.png");
			yield return Up();
		}

		private IEnumerator Step()
		{
			yield return Up();
			Vector2 found = default(Vector2);
			RestSeat seat = default(RestSeat);
			bool any = false;
			string[] array = new string[6] { "stockpile", "house_human_3", "house_elf_2", "house_dwarf_2", "house_orc_2", "house_human_4" };
			foreach (string id in array)
			{
				Building building = AddHouse(id, _king.city);
				if (building == null)
				{
					Note("no " + id + " could be built");
					continue;
				}
				Vector2 mid = new Vector2(building.current_tile.posV3.x, building.current_tile.posV3.y);
				for (float r = 1.2f; r <= 3.6f; r += 0.4f)
				{
					if (any)
					{
						break;
					}
					_me.current_position = mid + new Vector2(0f, 0f - r);
					yield return CivilTest.Wait(0.4f);
					for (int k = 0; k < 16; k++)
					{
						if (any)
						{
							break;
						}
						float num = (float)k * MathF.PI / 8f;
						Vector2 at = mid + new Vector2(Mathf.Cos(num), Mathf.Sin(num)) * r;
						WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
						if (tile != null && tile.Type != null && !tile.Type.liquid)
						{
							_me.current_position = at;
							Mod.SetYaw(num + MathF.PI);
							yield return null;
							if (Mod.SeatForTest(out seat) && seat.Kind == RestSeatKind.Ledge)
							{
								any = true;
								found = at;
								Note($"a ledge by the {id}: {seat.Height:F2} high, {r:F1} out");
							}
						}
					}
				}
				if (any)
				{
					break;
				}
			}
			if (Check("something at knee height to sit on in town", any))
			{
				_me.current_position = found;
				yield return CivilTest.Wait(0.3f);
				WorldBoxMod.PoseSitForTest = true;
				yield return CivilTest.Wait(1.6f);
				Check("sat on its edge", Mod.PoseNow == RestStyle.Seat && Mod.PoseSeatNow.Kind == RestSeatKind.Ledge, Mod.PoseNow.ToString() + " " + Mod.PoseSeatNow.Kind);
				float face = Mod.PoseSeatNow.Heading;
				Mod.SetYaw(face + MathF.PI + 0.7f);
				yield return CivilTest.Wait(0.5f);
				Mod.SaveFrame("pose_step_third.png");
				WorldBoxMod.HotbarWheelForTest = 1f;
				yield return CivilTest.Wait(0.8f);
				Mod.SetYaw(face);
				yield return CivilTest.Wait(0.3f);
				Mod.SaveFrame("pose_step_first.png");
				Mod.Settings.PoseView = 1;
				yield return Up();
			}
		}

		private IEnumerator Pier()
		{
			yield return Up();
			Vector2 current_position = _king.current_position;
			WorldTile spot = null;
			for (int i = 4; i < 120; i += 2)
			{
				if (spot != null)
				{
					break;
				}
				for (int j = 0; j < 64; j++)
				{
					if (spot != null)
					{
						break;
					}
					float f = (float)j / 64f * MathF.PI * 2f;
					int num = (int)(current_position.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(current_position.y + Mathf.Sin(f) * (float)i);
					if (num < 16 || num2 < 16 || num >= MapBox.width - 16 || num2 >= MapBox.height - 16)
					{
						continue;
					}
					bool flag = true;
					for (int k = 0; k <= 4 && flag; k++)
					{
						for (int l = -2; l <= 2 && flag; l++)
						{
							if (!Wet(num + l, num2 + k))
							{
								flag = false;
							}
						}
					}
					if (!flag)
					{
						continue;
					}
					for (int m = 0; m < 8; m++)
					{
						if (spot != null)
						{
							break;
						}
						float f2 = (float)m * MathF.PI / 4f;
						if (!Wet(num + Mathf.RoundToInt(Mathf.Cos(f2) * 4.5f), num2 + 2 + Mathf.RoundToInt(Mathf.Sin(f2) * 4.5f)))
						{
							spot = World.world.GetTile(num, num2);
						}
					}
				}
			}
			if (!Check("a coast for a harbour", spot != null))
			{
				yield break;
			}
			Building building = null;
			try
			{
				building = World.world.buildings.addBuilding("docks_human", spot, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
			}
			catch
			{
			}
			if (!Check("a harbour", building != null))
			{
				yield break;
			}
			_me.current_position = new Vector2((float)spot.x + 0.5f, (float)spot.y + 2.5f);
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (DockDecks.Count != 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				_me.current_position = new Vector2((float)spot.x + 0.5f, (float)spot.y + 2.5f);
			}
			if (!Check("its pier", DockDecks.Count > 0 && DockDecks.PointForTest(0, 0f, -1.2f, out var _, out var heading)))
			{
				yield break;
			}
			bool seated = false;
			float[] array = new float[4] { -1.2f, -0.4f, -2f, 0.4f };
			foreach (float my in array)
			{
				DockDecks.PointForTest(0, 0f, my, out var at2, out var heading2);
				if (DockDecks.HeightAt(at2.x, at2.y, out heading))
				{
					Vector2 vector = new Vector2(Mathf.Cos(heading2 + MathF.PI / 2f), Mathf.Sin(heading2 + MathF.PI / 2f));
					float num3;
					for (num3 = 0f; num3 < 4f && DockDecks.HeightAt(at2.x + vector.x * (num3 + 0.05f), at2.y + vector.y * (num3 + 0.05f), out heading); num3 += 0.05f)
					{
					}
					at2 += vector * Mathf.Max(0f, num3 - 0.35f);
					Note($"pier side {num3:F2} tiles out from its middle");
					_me.current_position = at2;
					Mod.SetYaw(heading2 + MathF.PI / 2f);
					Mod.SetPitchDegrees(-10f);
					yield return CivilTest.Wait(0.6f);
					if (Mod.SeatForTest(out var seat) && seat.Kind == RestSeatKind.Pier)
					{
						Check("the pier's edge before you", ok: true, $"deck {seat.Top:F2}, {(seat.At - at2).magnitude:F2} off");
						WorldBoxMod.PoseSitForTest = true;
						yield return CivilTest.Wait(1.6f);
						seated = Mod.PoseNow == RestStyle.Seat && Mod.PoseSeatNow.Kind == RestSeatKind.Pier && Mod.PoseSeatNow.Hang;
						break;
					}
				}
			}
			if (Check("sat on the pier's edge, legs over the water", seated, Mod.PoseNow.ToString() + " " + Mod.PoseSeatNow.Kind))
			{
				Mod.SetYaw(Mod.PoseSeatNow.Heading + MathF.PI + 0.8f);
				yield return CivilTest.Wait(0.5f);
				Mod.SaveFrame("pose_pier_third.png");
				yield return Up();
			}
		}

		private static bool Wet(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile != null && tile.Type != null && tile.Type.liquid)
			{
				return !tile.Type.lava;
			}
			return false;
		}

		private IEnumerator Chair()
		{
			yield return Up();
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Founded)
			{
				if (item != null && item != _me && item != _king && item.isAlive() && list.Count < 2)
				{
					list.Add(item);
				}
			}
			Mod.EnterRoomForTest(list);
			yield return CivilTest.Wait(1f);
			HouseInterior interior = Mod.Interior;
			if (!Check("in a room", interior.Inside && interior.Room != null))
			{
				yield break;
			}
			int num = -1;
			for (int i = 0; i < interior.Room.Spots.Count; i++)
			{
				float[] array = interior.Room.Spots[i];
				if (array.Length > 3 && array[3] > 0f && !interior.SeatTaken(new Vector2(array[0], array[1])) && (array.Length < 5 || array[4] < 0.1f))
				{
					num = i;
					break;
				}
			}
			if (!Check("a free chair", num >= 0))
			{
				Mod.LeaveRoomForTest();
				yield break;
			}
			float[] seat = interior.Room.Spots[num];
			Vector2 vector = new Vector2(Mathf.Cos(seat[2] * (MathF.PI / 180f)), Mathf.Sin(seat[2] * (MathF.PI / 180f)));
			interior.Eye = new Vector2(seat[0], seat[1]) + vector * 0.6f;
			Mod.SetYaw(Mathf.Atan2(0f - vector.y, 0f - vector.x));
			Mod.SetPitchDegrees(-15f);
			yield return CivilTest.Wait(0.6f);
			WorldBoxMod.PoseTapForTest = true;
			yield return CivilTest.Wait(1.6f);
			Check("P sits you in the chair", Mod.PoseNow == RestStyle.Seat && Mod.PoseSeatNow.Kind == RestSeatKind.Chair, Mod.PoseNow.ToString() + " " + Mod.PoseSeatNow.Kind);
			Check("nobody makes for it", RoomWalkers.YouSeat.HasValue);
			Mod.SetYaw(seat[2] * (MathF.PI / 180f) + 2.5f);
			Mod.SetPitchDegrees(-12f);
			yield return CivilTest.Wait(0.6f);
			Mod.SaveFrame("pose_chair_third.png");
			WorldBoxMod.HotbarWheelForTest = 1f;
			yield return CivilTest.Wait(0.8f);
			Mod.SetYaw(seat[2] * (MathF.PI / 180f));
			yield return CivilTest.Wait(0.3f);
			Mod.SaveFrame("pose_chair_first.png");
			Mod.Settings.PoseView = 1;
			WorldBoxMod.PoseTapForTest = true;
			yield return CivilTest.Wait(1.2f);
			Check("up from the chair", Mod.PoseNow == RestStyle.None && !RoomWalkers.YouSeat.HasValue);
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator AboutTown()
		{
			City city = _king.city;
			Vector2 centre = ((city != null && city.getTile() != null) ? new Vector2((float)city.getTile().x + 0.5f, (float)city.getTile().y + 0.5f) : _king.current_position);
			int num = 0;
			List<Actor> folk = new List<Actor>();
			for (int i = 0; i < 14; i++)
			{
				Actor actor = CivilTest.SpawnNear("human", centre + new Vector2((float)(i % 5) - 2f, (float)(i / 5) - 1f) * 1.6f, 0f, i % 4 != 3);
				if (actor != null)
				{
					folk.Add(actor);
					try
					{
						actor.joinCity(city);
						((BaseSimObject)actor).addStatusEffect("invincible", 900f, pColorEffect: false);
					}
					catch
					{
					}
					num++;
				}
			}
			Note($"{num} townsfolk at {centre}");
			Lounging.SatDownForTest = 0;
			Lounging.LogForTest = true;
			_me.current_position = centre - new Vector2(0f, 9f);
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-12f);
			int most = 0;
			for (int s = 0; s < 70; s++)
			{
				yield return CivilTest.Wait(1f);
				_me.current_position = centre - new Vector2(0f, 9f);
				most = Mathf.Max(most, Lounging.SeatedForTest);
				if (s == 35 || s == 55)
				{
					Mod.SaveFrame($"pose_town_{s}.png");
				}
			}
			Note($"by day: {Lounging.SatDownForTest} sat down, at most {most} at once");
			yield return CloseUp("pose_town_sitter.png", null);
			Check("people sit about town", Lounging.SatDownForTest > 0 && most > 0, $"{Lounging.SatDownForTest} sat down, at most {most} at once");
			WorldTile ft = null;
			Building fire = null;
			for (int j = 0; j < 24; j++)
			{
				if (fire != null)
				{
					break;
				}
				float f = (float)j * 0.9f;
				float num2 = 2f + (float)j * 0.25f;
				ft = World.world.GetTile((int)(centre.x + Mathf.Cos(f) * num2), (int)(centre.y + Mathf.Sin(f) * num2));
				if (ft != null && ft.building == null && ft.Type != null && ft.Type.ground && !ft.Type.liquid)
				{
					try
					{
						fire = World.world.buildings.addBuilding("bonfire", ft, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
						fire?.completeConstruction();
					}
					catch (Exception ex)
					{
						Note("bonfire: " + ex.Message);
					}
				}
			}
			yield return CivilTest.Wait(2.5f);
			Check("a town fire", fire != null && Seats.FireNear(centre, 8f, out var _), (fire == null) ? "none could be built" : $"built at {ft.x},{ft.y}, alive {fire.isAlive()}, building {fire.isUnderConstruction()}");
			int was = Mod.Settings.TimeMode;
			Mod.Settings.TimeMode = 3;
			if (fire != null)
			{
				for (int k = 0; k < folk.Count; k++)
				{
					if (folk[k] != null && folk[k].isAlive())
					{
						float f2 = (float)k * 2.4f;
						float num3 = 2f + (float)(k % 3);
						Vector2 current_position = new Vector2((float)ft.x + 0.5f + Mathf.Cos(f2) * num3, (float)ft.y + 0.5f + Mathf.Sin(f2) * num3);
						WorldTile tile = World.world.GetTile((int)current_position.x, (int)current_position.y);
						if (tile != null && tile.Type != null && !tile.Type.liquid)
						{
							folk[k].current_position = current_position;
							folk[k].dirty_current_tile = true;
						}
					}
				}
			}
			Lounging.SatDownForTest = 0;
			int ring = 0;
			for (int s = 0; s < 45; s++)
			{
				yield return CivilTest.Wait(1f);
				_me.current_position = ((fire != null) ? new Vector2((float)ft.x + 0.5f, (float)ft.y - 6.5f) : (centre - new Vector2(0f, 9f)));
				if (fire != null)
				{
					ring = Mathf.Max(ring, RoundTheFire(new Vector2((float)ft.x + 0.5f, (float)ft.y + 0.5f)));
				}
				if (s == 30)
				{
					Mod.SaveFrame("pose_fire_night.png");
				}
			}
			Note($"at night: {Lounging.SatDownForTest} sat down, at most {ring} round the fire");
			if (fire != null)
			{
				yield return CloseUp("pose_fire_sitter.png", new Vector2((float)ft.x + 0.5f, (float)ft.y + 0.5f));
			}
			Check("some sit round the fire at night", ring > 0, ring + " round it");
			Mod.Settings.TimeMode = was;
		}

		private IEnumerator CloseUp(string name, Vector2? fire)
		{
			Actor sitter = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != _me && Lounging.Sits(simple))
				{
					float num2 = (fire.HasValue ? (Lounging.Spot(simple, simple.current_position) - fire.Value).magnitude : 0f);
					if (num2 < num)
					{
						num = num2;
						sitter = simple;
					}
				}
			}
			if (sitter == null)
			{
				Note("nobody sitting for " + name);
				yield break;
			}
			Vector2 at = Lounging.Spot(sitter, sitter.current_position);
			Vector2 from = at + new Vector2(-1.6f, -1.6f);
			_me.current_position = from;
			Mod.SetYaw(Mathf.Atan2(at.y - from.y, at.x - from.x));
			Mod.SetPitchDegrees(-14f);
			yield return CivilTest.Wait(0.8f);
			_me.current_position = from;
			yield return CivilTest.Wait(0.3f);
			Mod.SaveFrame(name);
			Note($"{name}: {WorldBoxMod.Describe(sitter)} sitting at {at}");
		}

		private static int RoundTheFire(Vector2 fire)
		{
			int num = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && Lounging.Sits(simple) && (Lounging.Spot(simple, simple.current_position) - fire).magnitude < 2.2f)
				{
					num++;
				}
			}
			return num;
		}

		private bool WholeBody()
		{
			foreach (MeshInstance mesh in Mod.SceneForTest.Meshes)
			{
				if (mesh.Tag == 2147483646 && !mesh.ShadowOnly)
				{
					return true;
				}
			}
			return false;
		}
	}
}
