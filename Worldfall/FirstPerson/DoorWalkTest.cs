using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class DoorWalkTest : CivilTest
	{
		private Actor _you;

		private Actor _neighbour;

		private City _city;

		private Building _house;

		private HouseWorld.Placement _place;

		private HousePlan _plan;

		private Vector2 _face;

		private Vector2 _out;

		private int _stairs;

		private int _climbs;

		private int _sealed;

		private int _footsOff;

		private string _footsOffAt = "";

		private string _sealedAt = "";

		private bool[] _reached;

		private int _steps;

		private float _worst;

		private float _eyeJump;

		private string _eyeJumpAt = "";

		private int _downFails;

		private string _downFailsAt = "";

		private string _worstAt = "none";

		private readonly List<float> _withWindow = new List<float>();

		private readonly List<float> _noWindow = new List<float>();

		private readonly List<float> _modWindow = new List<float>();

		private readonly List<float> _modNoWindow = new List<float>();

		private int _backdrops;

		protected override string Prefix => "DOORWALK";

		private Vector2 Along => new Vector2(0f - _out.y, _out.x);

		protected override IEnumerator Body()
		{
			if (Options.Contains("nofix"))
			{
				WorldBoxMod.DoorwayAside = false;
			}
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
			int savedMode = Mod.Settings.TimeMode;
			Mod.Settings.TimeMode = ((!Options.Contains("night")) ? 1 : 3);
			_house = AddHouse(Options.Contains("house0") ? "house_human_0" : "house_human_2", _city);
			if (!Check("a house", _house != null && HouseInterior.IsEnterable(_house)))
			{
				yield break;
			}
			Put(_you, _house.current_position + new Vector2(0f, -4.5f));
			Mod.SetYaw(MathF.PI / 2f);
			for (float t = 0f; t < 4f; t += 0.2f)
			{
				if (HouseWorld.Of(_house, out _place))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(0.4f);
			if (!Check("drawn where it stands", HouseWorld.Of(_house, out _place)))
			{
				yield break;
			}
			_plan = HouseWorld.PlanFor(_place);
			if (!Check("its model measured", _plan != null, HousePlan.Why))
			{
				yield break;
			}
			float num = (_place.Mirror ? (0f - _place.Scale) : _place.Scale);
			_face = new Vector2(_place.X + _plan.Door.X * num, _place.Y + _plan.Door.Y * _place.Scale);
			_out = new Vector2(_plan.Door.OutX * (_place.Mirror ? (-1f) : 1f), _plan.Door.OutY).normalized;
			Note($"house {_place.Model.Name} door face {_face} out {_out}");
			if (Want("talk"))
			{
				yield return Part("someone by the door", TalkAtDoor());
			}
			if (Want("in"))
			{
				yield return Part("walking in", WalkIn());
			}
			if (Want("doorstep"))
			{
				yield return Part("someone on their doorstep, struck", Doorstep());
			}
			if (Want("cross"))
			{
				yield return Part("in and out, the frames after", Crossings());
			}
			if (Want("about"))
			{
				yield return Part("walking about inside", WalkAbout());
			}
			if (Want("door"))
			{
				yield return Part("the door by hand", DoorByHand());
			}
			if (Want("river"))
			{
				yield return Part("a home by the river", River());
			}
			if (Want("flights"))
			{
				if (Mod.Interior.Inside)
				{
					Mod.LeaveRoomForTest();
				}
				try
				{
					_house.removeBuildingFinal();
				}
				catch
				{
				}
				string[] array = new string[3] { "", "_b", "_c" };
				foreach (string v in array)
				{
					for (int tier = 0; tier <= 5; tier++)
					{
						string text = $"house_human_{tier}{v}";
						if (Models.Has(text) && (!Options.Contains("flightsof") || Options.Contains("flightsof" + text + " ")))
						{
							yield return Part(text + " floors", Floors(text, $"house_human_{tier}"));
							Models.FamilyForTest = null;
						}
					}
				}
				Models.FamilyForTest = null;
				Note($"floors: {_climbs} stairs climbed of {_stairs}, {_sealed} storeys with floor out of reach ({_sealedAt})");
				Check("every stair climbed", _climbs == _stairs, $"{_climbs} of {_stairs}");
				Check("every floor reachable", _sealed == 0, _sealedAt);
				Check("every stair's foot walked to from the door", _footsOff == 0, _footsOffAt);
				Check("every stair walked back down", _downFails == 0, _downFailsAt);
				Check("no jump of the view up or down a stair", _eyeJump < 0.45f, $"worst {_eyeJump:F3} at {_eyeJumpAt}");
			}
			if (Want("every"))
			{
				if (Mod.Interior.Inside)
				{
					Mod.LeaveRoomForTest();
				}
				try
				{
					_house.removeBuildingFinal();
				}
				catch
				{
				}
				string[] array = new string[3] { "", "_b", "_c" };
				foreach (string v in array)
				{
					for (int tier = 0; tier <= 5; tier++)
					{
						string model = $"house_human_{tier}{v}";
						if (Models.Has(model))
						{
							yield return Part(model, Threshold(model, $"house_human_{tier}"));
							Models.FamilyForTest = null;
						}
					}
				}
				Models.FamilyForTest = null;
				Note($"thresholds: {_steps} walked in, worst jump {_worst:F3} ({_worstAt})");
				Check("every house: no jump at the threshold", _worst < 0.2f, $"worst {_worst:F3} at {_worstAt}");
			}
			Mod.Settings.TimeMode = savedMode;
			PossessionHooks.ScriptedInput = Vector2.zero;
			PlayInput.TestReleaseAll();
			WorldBoxMod.DoorwayAside = true;
		}

		private IEnumerator River()
		{
			if (Options.Contains("wet"))
			{
				DryBoards.KeepIndoorsDry = false;
			}
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			Building b = AddHouse("house_elf_1", _city);
			if (!Check("an elf's house", b != null && HouseInterior.IsEnterable(b)))
			{
				yield break;
			}
			Put(_you, b.current_position + new Vector2(0f, -3f));
			yield return CivilTest.Wait(1f);
			Mod.EnterHouseForTest(b);
			yield return CivilTest.Wait(0.6f);
			if (!Check("in its room", Mod.Interior.Inside && Mod.Interior.House == b && !Mod.Interior.Fitted, $"inside {Mod.Interior.Inside}, fitted {Mod.Interior.Fitted}"))
			{
				yield break;
			}
			Interior room = Mod.Interior.Room;
			List<(Vector2 Room, Vector2 World, float Far)> spots = new List<(Vector2, Vector2, float)>();
			for (float num = room.MinX + 0.3f; num < room.MaxX - 0.3f; num += 0.25f)
			{
				for (float num2 = room.MinY + 0.3f; num2 < room.MaxY - 0.3f; num2 += 0.25f)
				{
					Vector2 vector = new Vector2(num, num2);
					if (Mod.Interior.BlockedWhyForTest(vector) == null)
					{
						Vector2 vector2 = vector + Mod.Interior.CombatOffset(_you);
						WorldTile tile = World.world.GetTile((int)vector2.x, (int)vector2.y);
						if (tile != null && (tile.building == null || !AssetStyle.IsSolid(tile.building)))
						{
							spots.Add((vector, vector2, (vector2 - b.current_position).magnitude));
						}
					}
				}
			}
			spots.Sort(((Vector2 Room, Vector2 World, float Far) m, (Vector2 Room, Vector2 World, float Far) n) => n.Far.CompareTo(m.Far));
			TileType water = AssetManager.tiles.get("shallow_waters");
			Vector2 best = Mod.Interior.Eye;
			Vector2 bestWorld = default(Vector2);
			bool flag = false;
			for (int i = 0; i < spots.Count && i < 40; i += 3)
			{
				if (flag)
				{
					break;
				}
				best = spots[i].Room;
				bestWorld = spots[i].World;
				for (int num3 = -1; num3 <= 1; num3++)
				{
					for (int num4 = -1; num4 <= 1; num4++)
					{
						WorldTile tile2 = World.world.GetTile((int)bestWorld.x + num4, (int)bestWorld.y + num3);
						if (tile2 != null && water != null && (tile2.building == null || !AssetStyle.IsSolid(tile2.building)))
						{
							try
							{
								tile2.top_type = null;
								tile2.setTileType(water);
							}
							catch (Exception ex)
							{
								Note("water: " + ex.Message);
							}
						}
					}
				}
				for (int f = 0; f < 6; f++)
				{
					Mod.Interior.Eye = best;
					yield return null;
				}
				flag = _you.current_tile != null && _you.current_tile.Type != null && _you.current_tile.Type.liquid;
			}
			Vector2 pond = default(Vector2);
			bool held = false;
			if (!flag)
			{
				float num5 = float.MaxValue;
				Vector2 current_position = b.current_position;
				for (int num6 = -80; num6 <= 80; num6 += 2)
				{
					for (int num7 = -80; num7 <= 80; num7 += 2)
					{
						WorldTile tile3 = World.world.GetTile((int)current_position.x + num7, (int)current_position.y + num6);
						if (tile3?.Type != null && tile3.Type.liquid && !tile3.Type.lava)
						{
							float num8 = num7 * num7 + num6 * num6;
							if (num8 < num5)
							{
								num5 = num8;
								pond = new Vector2((float)tile3.x + 0.5f, (float)tile3.y + 0.5f);
							}
						}
					}
				}
				if (num5 < float.MaxValue)
				{
					held = true;
					RoomCombat.Enabled = false;
					for (int i = 0; i < 6; i++)
					{
						Put(_you, pond);
						yield return null;
					}
					flag = _you.current_tile?.Type != null && _you.current_tile.Type.liquid && _you.is_inside_building;
				}
			}
			if (!Check("your body over water in the room", flag, $"tile {_you.current_tile?.Type?.id}, held over a pond {held}"))
			{
				RoomCombat.Enabled = true;
				Mod.LeaveRoomForTest();
				yield break;
			}
			Mod.Interior.Eye = best;
			yield return CivilTest.Wait(0.5f);
			WorldTile current_tile = _you.current_tile;
			Note($"room spot {best} is {bestWorld} in the world; your body at {_you.current_position}, its tile {current_tile?.Type?.id}, inside {_you.is_inside_building}");
			_you.setStamina(0);
			int h0 = _you.getHealth();
			bool drowning = false;
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				Mod.Interior.Eye = best;
				if (held)
				{
					Put(_you, pond);
				}
				drowning |= ((BaseSimObject)_you).hasStatus("drowning");
				yield return null;
			}
			int health = _you.getHealth();
			Check("indoors over the water nobody drowns", !drowning && health >= h0, $"drowning {drowning}, health {h0} -> {health}, tile {_you.current_tile?.Type?.id}");
			_you.setStamina(_you.getMaxStamina());
			try
			{
				_you.finishStatusEffect("drowning");
			}
			catch
			{
			}
			RoomCombat.Enabled = true;
			Mod.LeaveRoomForTest();
			DryBoards.KeepIndoorsDry = true;
			try
			{
				b.removeBuildingFinal();
			}
			catch
			{
			}
		}

		private IEnumerator Floors(string model, string asset)
		{
			Models.FamilyForTest = model;
			Building b = AddHouse(asset, _city);
			if (!Check(model + " built", b != null && HouseInterior.IsEnterable(b)))
			{
				yield break;
			}
			Put(_you, b.current_position + new Vector2(0f, -4f));
			for (float t = 0f; t < 4f; t += 0.2f)
			{
				if (HouseWorld.Of(b, out var _))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			yield return CivilTest.Wait(0.3f);
			for (int tries = 0; tries < 3; tries++)
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
			if (!Mod.Interior.Inside || Mod.Interior.House != b || !Mod.Interior.Fitted)
			{
				Note(model + ": no fitted room");
				Cleanup(b);
				yield break;
			}
			Interior room = Mod.Interior.Room;
			FitInfo fit = room.Fit;
			for (float t = 0f; t < 3f; t += 0.1f)
			{
				if (RoomFloors.Ready)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Reach(room, 0, Mod.Interior.Eye, out var got, out var lost0, out var lostAt0);
			Mod.SaveFrame("doorwalk_" + model + "_ground.png");
			string line = string.Format("{0}: {1} storeys{2}, ground floor {3:F1} t2 reached, {4:F1} out of reach{5}", model, fit.StoreyZ.Length, fit.UpperShut ? " (upper shut)" : "", got, lost0, (lost0 > 0.3f) ? $" round {lostAt0}" : "");
			if (!float.IsNaN(fit.StairFootX) && fit.StoreyZ.Length > 1 && !fit.UpperShut)
			{
				_stairs++;
				bool flag = false;
				if (_reached != null)
				{
					int num = Mathf.CeilToInt((room.MaxX - room.MinX) / 0.1f) + 1;
					for (int i = -3; i <= 3; i++)
					{
						if (flag)
						{
							break;
						}
						for (int j = -3; j <= 3; j++)
						{
							if (flag)
							{
								break;
							}
							int num2 = Mathf.RoundToInt((fit.StairFootX - room.MinX) / 0.1f) + j;
							int num3 = (Mathf.RoundToInt((fit.StairFootY - room.MinY) / 0.1f) + i) * num + num2;
							if (num2 >= 0 && num2 < num && num3 >= 0 && num3 < _reached.Length && _reached[num3])
							{
								flag = true;
							}
						}
					}
				}
				if (!flag)
				{
					_footsOff++;
					_footsOffAt = _footsOffAt + model + "; ";
				}
				line += (flag ? "; its foot reached from the door" : "; its foot NOT reached from the door");
				Vector2 vector = new Vector2(fit.StairFootX, fit.StairFootY);
				Vector2 head = new Vector2(fit.StairHeadX, fit.StairHeadY);
				Vector2 dir = (head - vector).normalized;
				RoomFloors.GroundForTest();
				Mod.Interior.Eye = vector - dir * 0.35f;
				Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
				Mod.SetPitchDegrees(10f);
				yield return CivilTest.Wait(0.4f);
				Mod.SaveFrame("doorwalk_" + model + "_stair.png");
				string why = Mod.Interior.BlockedWhyForTest(Mod.Interior.Eye + dir * 0.06f) ?? "free";
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				float t = 0f;
				float prevRel = float.NaN;
				float jump = 0f;
				for (float t2 = 0f; t2 < 6f; t2 += Time.unscaledDeltaTime)
				{
					if (!(RoomFloors.Feet < fit.StoreyZ[1] - 0.1f))
					{
						break;
					}
					Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
					t = Mathf.Max(t, RoomFloors.Feet);
					yield return null;
					float num4 = Mod.View.Z - RoomFloors.Shown;
					if (!float.IsNaN(prevRel))
					{
						jump = Mathf.Max(jump, Mathf.Abs(num4 - prevRel));
					}
					prevRel = num4;
				}
				yield return CivilTest.Wait(0.4f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				bool up = RoomFloors.Feet > fit.StoreyZ[1] - 0.15f;
				if (up)
				{
					PossessionHooks.ScriptedInput = new Vector2(0f, -1f);
					prevRel = float.NaN;
					for (float t2 = 0f; t2 < 8f; t2 += Time.unscaledDeltaTime)
					{
						if (!(RoomFloors.Feet > 0.02f))
						{
							break;
						}
						Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
						yield return null;
						float num5 = Mod.View.Z - RoomFloors.Shown;
						if (!float.IsNaN(prevRel))
						{
							jump = Mathf.Max(jump, Mathf.Abs(num5 - prevRel));
						}
						prevRel = num5;
					}
					for (float t2 = 0f; t2 < 0.5f; t2 += Time.unscaledDeltaTime)
					{
						Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
						yield return null;
					}
					PossessionHooks.ScriptedInput = Vector2.zero;
					line += $"; back down to {RoomFloors.Feet:F2}";
					if (RoomFloors.Feet > 0.05f)
					{
						_downFails++;
						_downFailsAt = _downFailsAt + model + "; ";
					}
					RoomFloors.FeetForTest(fit.StoreyZ[1]);
					Mod.Interior.Eye = head + dir * 0.35f;
					yield return CivilTest.Wait(0.3f);
				}
				line += $"; the eye over the feet moved at most {jump:F3} in a frame";
				if (jump > _eyeJump)
				{
					_eyeJump = jump;
					_eyeJumpAt = model;
				}
				if (up)
				{
					_climbs++;
				}
				string text = string.Format("NOT climbed (feet at most {0:F2} of {1:F2}, first step: {2}, now at {3}, a step on: {4})", t, fit.StoreyZ[1], why, Mod.Interior.Eye, Mod.Interior.BlockedWhyForTest(Mod.Interior.Eye + dir * 0.06f) ?? "free");
				line = line + "; stair " + (up ? "climbed" : text);
				Mod.SaveFrame("doorwalk_" + model + "_up.png");
				if (up)
				{
					Reach(room, 1, Mod.Interior.Eye, out var got2, out var lost1, out var lostAt1);
					line += string.Format(", upper floor {0:F1} t2 reached, {1:F1} out of reach{2}", got2, lost1, (lost1 > 0.3f) ? $" round {lostAt1}" : "");
					if (lost1 > 0.6f)
					{
						_sealed++;
						_sealedAt += $"{model} upstairs round {lostAt1}; ";
					}
				}
				RoomFloors.GroundForTest();
			}
			else if (fit.StoreyZ.Length > 1)
			{
				line += $"; no stair (foot {fit.StairFootX:F2}, upper shut {fit.UpperShut})";
			}
			if (lost0 > 0.6f)
			{
				_sealed++;
				_sealedAt += $"{model} ground floor round {lostAt0}; ";
			}
			Note(line);
			Cleanup(b);
			yield return CivilTest.Wait(0.3f);
		}

		private void Cleanup(Building b)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			try
			{
				b.removeBuildingFinal();
			}
			catch
			{
			}
		}

		private void Reach(Interior room, int storey, Vector2 from, out float got, out float lost, out Vector2 lostAt)
		{
			FitInfo fit = room.Fit;
			int num = Mathf.CeilToInt((room.MaxX - room.MinX) / 0.1f) + 1;
			int num2 = Mathf.CeilToInt((room.MaxY - room.MinY) / 0.1f) + 1;
			bool[] array = new bool[num * num2];
			bool[] array2 = new bool[num * num2];
			RoomFloors.FeetForTest(fit.StoreyZ[storey]);
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					Vector2 p = new Vector2(room.MinX + (float)j * 0.1f, room.MinY + (float)i * 0.1f);
					if (fit.Inside(storey, p.x, p.y))
					{
						string text = Mod.Interior.BlockedWhyForTest(p);
						array[i * num + j] = text == null || text == "someone there";
					}
				}
			}
			int num3 = Mathf.Clamp(Mathf.RoundToInt((from.x - room.MinX) / 0.1f), 0, num - 1);
			int num4 = Mathf.Clamp(Mathf.RoundToInt((from.y - room.MinY) / 0.1f), 0, num2 - 1);
			int num5 = -1;
			float num6 = float.MaxValue;
			for (int k = 0; k < num * num2; k++)
			{
				if (array[k])
				{
					float num7 = (k % num - num3) * (k % num - num3) + (k / num - num4) * (k / num - num4);
					if (num7 < num6)
					{
						num6 = num7;
						num5 = k;
					}
				}
			}
			got = 0f;
			lost = 0f;
			lostAt = default(Vector2);
			if (num5 >= 0)
			{
				got = (float)Flood(num5, array, array2, num, num2, out var _, out var _) * 0.1f * 0.1f;
				_reached = (bool[])array2.Clone();
				int num8 = 0;
				for (int l = 0; l < num * num2; l++)
				{
					if (array[l] && !array2[l])
					{
						float cx2;
						float cy2;
						int num9 = Flood(l, array, array2, num, num2, out cx2, out cy2);
						if (num9 > num8)
						{
							num8 = num9;
							lostAt = new Vector2(room.MinX + cx2 * 0.1f, room.MinY + cy2 * 0.1f);
						}
					}
				}
				lost = (float)num8 * 0.1f * 0.1f;
			}
			if (Options.Contains("floormap") && lost > 0.6f)
			{
				StringBuilder stringBuilder = new StringBuilder();
				int num10 = (float.IsNaN(fit.StairFootX) ? (-1) : Mathf.RoundToInt((fit.StairFootX - room.MinX) / 0.1f));
				int num11 = (float.IsNaN(fit.StairFootX) ? (-1) : Mathf.RoundToInt((fit.StairFootY - room.MinY) / 0.1f));
				int num12 = (float.IsNaN(fit.DoorWallX) ? (-1) : Mathf.RoundToInt((fit.DoorWallX - room.MinX) / 0.1f));
				int num13 = (float.IsNaN(fit.DoorWallX) ? (-1) : Mathf.RoundToInt((fit.DoorWallY - room.MinY) / 0.1f));
				RoomFloors.FeetForTest(fit.StoreyZ[storey]);
				for (int num14 = num2 - 1; num14 >= 0; num14--)
				{
					stringBuilder.Append('\n');
					for (int m = 0; m < num; m++)
					{
						int num15 = num14 * num + m;
						Vector2 p2 = new Vector2(room.MinX + (float)m * 0.1f, room.MinY + (float)num14 * 0.1f);
						char value;
						if (m == num10 && num14 == num11)
						{
							value = 'S';
						}
						else if (storey == 0 && m == num12 && num14 == num13)
						{
							value = 'D';
						}
						else if (array[num15])
						{
							value = (Reached(num15) ? '.' : 'o');
						}
						else if (!fit.Inside(storey, p2.x, p2.y))
						{
							value = ' ';
						}
						else
						{
							string text2 = Mod.Interior.BlockedWhyForTest(p2) ?? "";
							value = (text2.StartsWith("solid") ? '#' : (text2.StartsWith("floor") ? 'f' : (text2.StartsWith("walls") ? '|' : '?')));
						}
						stringBuilder.Append(value);
					}
				}
				Note($"floor map of storey {storey} ({room.MinX:F2},{room.MinY:F2})-({room.MaxX:F2},{room.MaxY:F2}), cell {0.1f}:{stringBuilder}");
				if (storey == 0)
				{
					foreach (float[] solid in room.Solids)
					{
						Note(string.Format("  solid {0:F2},{1:F2}-{2:F2},{3:F2}{4}{5}", solid[0], solid[1], solid[2], solid[3], (solid.Length >= 6) ? $" z {solid[4]:F2}" : "", RoomFloors.Climbs(solid) ? " (climbed)" : ""));
					}
				}
			}
			RoomFloors.FeetForTest((storey == 0) ? 0f : fit.StoreyZ[storey]);
			bool Reached(int num16)
			{
				if (_reached != null)
				{
					return _reached[num16];
				}
				return false;
			}
		}

		private static int Flood(int start, bool[] free, bool[] seen, int w, int h, out float cx, out float cy)
		{
			Queue<int> queue = new Queue<int>();
			queue.Enqueue(start);
			seen[start] = true;
			int num = 0;
			float num2 = 0f;
			float num3 = 0f;
			while (queue.Count > 0)
			{
				int num4 = queue.Dequeue();
				num++;
				int num5 = num4 % w;
				int num6 = num4 / w;
				num2 += (float)num5;
				num3 += (float)num6;
				if (num5 > 0 && free[num4 - 1] && !seen[num4 - 1])
				{
					seen[num4 - 1] = true;
					queue.Enqueue(num4 - 1);
				}
				if (num5 < w - 1 && free[num4 + 1] && !seen[num4 + 1])
				{
					seen[num4 + 1] = true;
					queue.Enqueue(num4 + 1);
				}
				if (num6 > 0 && free[num4 - w] && !seen[num4 - w])
				{
					seen[num4 - w] = true;
					queue.Enqueue(num4 - w);
				}
				if (num6 < h - 1 && free[num4 + w] && !seen[num4 + w])
				{
					seen[num4 + w] = true;
					queue.Enqueue(num4 + w);
				}
			}
			cx = num2 / (float)num;
			cy = num3 / (float)num;
			return num;
		}

		private IEnumerator Threshold(string model, string asset)
		{
			Models.FamilyForTest = model;
			Building b = AddHouse(asset, _city);
			if (!Check(model + " built", b != null && HouseInterior.IsEnterable(b)))
			{
				yield break;
			}
			try
			{
				Put(_you, b.current_position + new Vector2(0f, -4.5f));
			}
			catch
			{
			}
			HouseWorld.Placement place = default(HouseWorld.Placement);
			for (float t = 0f; t < 4f; t += 0.2f)
			{
				if (HouseWorld.Of(b, out place))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			HousePlan plan = (HouseWorld.Of(b, out place) ? HouseWorld.PlanFor(place) : null);
			if (plan == null)
			{
				Note(model + ": no plan");
				try
				{
					b.removeBuildingFinal();
					yield break;
				}
				catch
				{
					yield break;
				}
			}
			float num = (place.Mirror ? (0f - place.Scale) : place.Scale);
			Vector2 face = new Vector2(place.X + plan.Door.X * num, place.Y + plan.Door.Y * place.Scale);
			Vector2 o = new Vector2(plan.Door.OutX * (place.Mirror ? (-1f) : 1f), plan.Door.OutY).normalized;
			Put(_you, face + o * 2.4f);
			Mod.SetYaw(Mathf.Atan2(0f - o.y, 0f - o.x));
			Mod.SetPitchDegrees(-3f);
			yield return CivilTest.Wait(1.2f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			float start = Time.unscaledTime;
			float lastOut = float.NaN;
			float firstIn = float.NaN;
			float ground = float.NaN;
			float on = float.NaN;
			float prev = float.NaN;
			float stepMost = 0f;
			int inFrames = -1;
			string mostWhere = "";
			while (Time.unscaledTime - start < 7f && inFrames < 12)
			{
				yield return null;
				ViewState view = Mod.View;
				float num2;
				if (!Mod.Interior.Inside)
				{
					num2 = view.Z;
					lastOut = num2;
					ground = Mod.GroundForTest(_you.current_position);
					on = Mod.FloorForTest;
				}
				else
				{
					if (Mod.Interior.House != b || !Mod.Interior.Fitted)
					{
						break;
					}
					if ((Mod.Interior.ToWorld(new Vector2(view.X, view.Y)) - face).magnitude > 3f)
					{
						continue;
					}
					num2 = Mod.Interior.WorldZ(view.Z);
					if (inFrames < 0)
					{
						firstIn = num2;
					}
					inFrames++;
				}
				if (!float.IsNaN(prev) && (Mod.Interior.Inside || Vector2.Dot(_you.current_position - face, o) < 0.6f) && Mathf.Abs(num2 - prev) > stepMost)
				{
					stepMost = Mathf.Abs(num2 - prev);
					mostWhere = string.Format("{0}, dt {1:F0} ms, on {2:F2}", Mod.Interior.Inside ? $"in, frame {inFrames}" : "out", Time.unscaledDeltaTime * 1000f, Mod.FloorForTest);
				}
				prev = num2;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			float num3 = place.Ground + plan.FloorZ * place.Scale;
			if (float.IsNaN(firstIn))
			{
				Note($"{model}: didn't walk in (inside {Mod.Interior.Inside}, at {Vector2.Dot(_you.current_position - face, o):F2} out)");
			}
			else
			{
				_steps++;
				if (stepMost > _worst)
				{
					_worst = stepMost;
					_worstAt = model;
				}
				Note($"{model}: eye out {lastOut:F3} (ground {ground:F3}, on {on:F3}), first in {firstIn:F3}, settled in {prev:F3}, most in a frame {stepMost:F3} ({mostWhere}), house ground {place.Ground:F3}, threshold {num3:F3}, door sill {plan.Door.Z0 * place.Scale:F3}");
				Mod.SaveFrame("doorwalk_" + model + "_in.png");
			}
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			yield return CivilTest.Wait(0.3f);
			try
			{
				b.removeBuildingFinal();
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
		}

		private bool Want(string part)
		{
			if (Options.Contains("only"))
			{
				return Options.Contains("only" + part);
			}
			return true;
		}

		private IEnumerator TalkAtDoor()
		{
			_neighbour = CivilTest.SpawnNear("human", _face + _out * 2f);
			if (!Check("a neighbour", _neighbour != null))
			{
				yield break;
			}
			Vector2 them = _face + _out * 0.8f + Along * 0.55f;
			Vector2 me = _face + _out * 2.1f - Along * 0.2f;
			if (!OnLand(them) || !OnLand(me) || !OnLand(_face + _out * 1.4f))
			{
				Note($"by the door: SKIPPED, no street before this house's door (at {_face}, out {_out})");
				yield break;
			}
			int[] hits = new int[3];
			int flips = 0;
			int buildings = 0;
			string lastPrompt = null;
			float start = Time.unscaledTime;
			int doorwayBefore = WorldBoxMod.DoorwayFrames;
			while (Time.unscaledTime - start < 4f)
			{
				Hold(_neighbour, them);
				Put(_you, me);
				Vector2 vector = them - me;
				Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
				float num = Mod.GroundForTest(them) + 0.55f;
				Mod.SetPitchDegrees(Mathf.Atan2(num - Mod.View.Z, Mathf.Max(0.3f, vector.magnitude)) * 57.29578f);
				yield return null;
				if (!(Time.unscaledTime - start < 1f))
				{
					hits[2]++;
					if (Mod.Talk.Candidate == _neighbour)
					{
						hits[0]++;
					}
					if (Mod.PickedForTest == _neighbour)
					{
						hits[1]++;
					}
					else if (Mod.PickedForTest is Building)
					{
						buildings++;
					}
					if (Mod.Prompt != lastPrompt)
					{
						flips++;
						lastPrompt = Mod.Prompt;
					}
				}
			}
			int num2 = WorldBoxMod.DoorwayFrames - doorwayBefore;
			Mod.SaveFrame("doorwalk_talk.png");
			Note("neighbour: " + Mod.DescribeForTest(_neighbour) + string.Format(", held at {0}, you at {1}, eye {2:F2},{3:F2},{4:F2}, picked {5}", them, _you.current_position, Mod.View.X, Mod.View.Y, Mod.View.Z, Mod.PickedForTest?.GetType().Name ?? "nothing"));
			Note($"by the door: {hits[2]} frames, candidate {hits[0]}, picked {hits[1]}, a building picked {buildings}, prompt changes {flips}, doorway room frames {num2}, door open {HouseLife.OpenOf(_house):F2}, prompt now '{Mod.Prompt}'");
			Check("the doorway's room drawn meanwhile", num2 > 0 || !Mod.GpuWantedForTest, num2 + " frames");
			Check("they're under the crosshair every frame", hits[2] > 3 && hits[0] == hits[2], $"{hits[0]} of {hits[2]}");
			Check("the prompt holds steady", flips <= 2, flips + " changes");
			Put(_neighbour, _face + _out * 6f + Along * 4f);
			yield return null;
			Vector2 at = _face + _out * 1.5f;
			int doorFrames = 0;
			int onHouse = 0;
			start = Time.unscaledTime;
			while (Time.unscaledTime - start < 2f)
			{
				Put(_you, at);
				Mod.SetYaw(Mathf.Atan2(0f - _out.y, 0f - _out.x));
				Mod.SetPitchDegrees(-6f);
				yield return null;
				if (!(Time.unscaledTime - start < 0.8f))
				{
					doorFrames++;
					if (Mod.PickedForTest == _house)
					{
						onHouse++;
					}
				}
			}
			Mod.SaveFrame("doorwalk_doorway.png");
			Check("looking into the open doorway is looking at the house", doorFrames > 2 && onHouse == doorFrames, $"{onHouse} of {doorFrames}, prompt '{Mod.Prompt}'");
		}

		private IEnumerator WalkIn()
		{
			if (_neighbour != null)
			{
				Put(_neighbour, _face + _out * 8f + Along * 5f);
			}
			Put(_you, _face + _out * 3.2f);
			Mod.SetYaw(Mathf.Atan2(0f - _out.y, 0f - _out.x));
			Mod.SetPitchDegrees(-3f);
			yield return CivilTest.Wait(1.5f);
			int stepsIn = WorldBoxMod.DoorStepsIn;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			float start = Time.unscaledTime;
			int shot = 0;
			int after = -1;
			float lastZ = float.NaN;
			float worstJump = 0f;
			Vector2 lastXY = Vector2.zero;
			while (Time.unscaledTime - start < 10f && after < 10)
			{
				yield return null;
				bool inside = Mod.Interior.Inside;
				ViewState view = Mod.View;
				Vector2 vector = ((inside && Mod.Interior.Fitted) ? Mod.Interior.ToWorld(new Vector2(view.X, view.Y)) : new Vector2(view.X, view.Y));
				float num = ((inside && Mod.Interior.Fitted) ? Mod.Interior.WorldZ(view.Z) : view.Z);
				float num2 = Vector2.Dot(vector - _face, _out);
				float num3 = (float.IsNaN(lastZ) ? 0f : (vector - lastXY).magnitude);
				if (num3 > 5f)
				{
					lastXY = vector;
					continue;
				}
				if (!float.IsNaN(lastZ) && Mathf.Abs(num - lastZ) > Mathf.Abs(worstJump))
				{
					worstJump = num - lastZ;
				}
				lastZ = num;
				lastXY = vector;
				if (inside && after < 0)
				{
					after = 0;
				}
				if (after >= 0)
				{
					after++;
				}
				if (num2 < 1.3f || after >= 0)
				{
					string text = $"doorwalk_in_{shot:D2}.png";
					Mod.SaveFrame(text);
					Note(string.Format("in frame {0:D2}: inside {1}, out {2:F2}, eye z {3:F3}, moved {4:F3}, yaw {5:F1}, pitch {6:F3}, fov {7:F1}, dt {8:F0} ms, frame {9:F1} ms, door {10:F2}/{11:F2}; ground {12:F3}, on {13:F3}, lift {14:F3}, house ground {15:F3}, threshold {16:F3}{17}", shot, inside, num2, num, num3, view.Yaw * 57.29578f, view.Pitch, view.Fov * 57.29578f, Time.unscaledDeltaTime * 1000f, Mod.FrameMs, HouseLife.OpenOf(_house), Mod.Interior.DoorOpen, Mod.GroundForTest(_you.current_position), Mod.FloorForTest, Mod.LiftForTest, _place.Ground, _place.Ground + _plan.FloorZ * _place.Scale, (inside && Mod.Interior.Fitted) ? $", room feet {Mod.Interior.WorldZ(RoomFloors.Shown):F3}, eye cap {Mod.Interior.WorldZ(RoomFloors.EyeLimit(Mod.Interior.Eye)):F3}" : ""));
					shot++;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			Check("walked in through the door", Mod.Interior.Inside && WorldBoxMod.DoorStepsIn > stepsIn);
			Check("the eye doesn't jump at the threshold", Mathf.Abs(worstJump) < 0.12f, $"worst {worstJump:F3} tiles in a frame");
		}

		private IEnumerator Doorstep()
		{
			if (Options.Contains("nodoorstep"))
			{
				FirstPerson.Doorstep.On = false;
			}
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			Actor v = CivilTest.SpawnNear("human", _face + _out * 3f);
			if (!Check("a villager", v != null))
			{
				yield break;
			}
			bool ff = Mod.Settings.FriendlyFire;
			Mod.Settings.FriendlyFire = true;
			int landed = 0;
			int tries = 0;
			for (int round = 0; round < 3; round++)
			{
				Vector2 them = _face + _out * 1.7f + Along * 0.9f;
				Vector2 me = _face + _out * 1.7f - Along * 0.25f;
				for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
				{
					Hold(v, them);
					Put(_you, me);
					Mod.SetYaw(Mathf.Atan2((them - me).y, (them - me).x));
					Mod.SetPitchDegrees(-6f);
					yield return null;
				}
				int before = v.getHealth();
				try
				{
					v.stayInBuilding(_house);
				}
				catch (Exception ex)
				{
					Note("stay: " + ex.Message);
				}
				yield return null;
				yield return null;
				Vector2 at;
				bool drawn = Mod.SceneForTest.DrawnWalkingIn(v, out at);
				Vector2 vector = at - _you.current_position;
				Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
				yield return null;
				PossessionCombat.AttackForTest(_you);
				tries++;
				yield return CivilTest.Wait(0.3f);
				int num = (v.isAlive() ? v.getHealth() : 0);
				if (num < before)
				{
					landed++;
				}
				Note(string.Format("round {0}: drawn on the doorstep {1} at {2}, inside {3}, health {4} -> {5}, stepped out {6}, last blow at {7}", round, drawn, at, v.is_inside_building, before, num, FirstPerson.Doorstep.SteppedOut, (PossessionCombat.LastBlowAt == v) ? "them" : ((PossessionCombat.LastBlowAt == null) ? "nobody" : "someone else")));
				try
				{
					if (v.is_inside_building)
					{
						v.exitBuilding();
					}
					v.restoreHealth(999);
				}
				catch
				{
				}
				if (!v.isAlive())
				{
					break;
				}
				at = default(Vector2);
			}
			Mod.Settings.FriendlyFire = ff;
			FirstPerson.Doorstep.On = true;
			Check("blows land on someone stepping into their house", landed > 0 && landed == tries, $"{landed} of {tries}");
			try
			{
				if (v.isAlive())
				{
					CivilTest.Kill(v);
				}
			}
			catch
			{
			}
		}

		private IEnumerator Crossings()
		{
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			List<float> after = new List<float>();
			float worstIn = 0f;
			float worstOut = 0f;
			for (int i = 0; i < 3; i++)
			{
				Put(_you, _face + _out * 1.8f);
				Mod.SetYaw(Mathf.Atan2(0f - _out.y, 0f - _out.x));
				Mod.SetPitchDegrees(-3f);
				yield return CivilTest.Wait(1.2f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
				{
					if (Mod.Interior.Inside)
					{
						break;
					}
					yield return null;
				}
				for (int f = 0; f < 8; f++)
				{
					yield return null;
					worstIn = Mathf.Max(worstIn, Time.unscaledDeltaTime * 1000f);
					after.Add(Time.unscaledDeltaTime * 1000f);
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				if (!Mod.Interior.Inside)
				{
					Note($"crossing {i}: not in");
					continue;
				}
				yield return CivilTest.Wait(0.8f);
				Interior room = Mod.Interior.Room;
				Mod.SetYaw(Mathf.Atan2(room.DoorOutY, room.DoorOutX));
				yield return CivilTest.Wait(0.3f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
				{
					if (!Mod.Interior.Inside)
					{
						break;
					}
					yield return null;
				}
				for (int f = 0; f < 8; f++)
				{
					yield return null;
					worstOut = Mathf.Max(worstOut, Time.unscaledDeltaTime * 1000f);
					after.Add(Time.unscaledDeltaTime * 1000f);
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				if (Mod.Interior.Inside)
				{
					Note($"crossing {i}: not out");
				}
			}
			Note($"frames after crossing: {Stats(after)}; worst in {worstIn:F0} ms, out {worstOut:F0} ms");
			Check("no hitch stepping in or out", worstIn < 100f && worstOut < 100f, $"in {worstIn:F0} ms, out {worstOut:F0} ms");
		}

		private IEnumerator WalkAbout()
		{
			if (!Mod.Interior.Inside)
			{
				Mod.EnterHouseForTest(_house);
				yield return CivilTest.Wait(0.5f);
			}
			if (Check("in the room", Mod.Interior.Inside && Mod.Interior.Fitted))
			{
				Interior room = Mod.Interior.Room;
				Mod.Interior.Eye = new Vector2(room.SpawnX, room.SpawnY);
				yield return CivilTest.Wait(0.5f);
				List<float> inside = new List<float>();
				List<float> insideSteps = new List<float>();
				_withWindow.Clear();
				_noWindow.Clear();
				_modWindow.Clear();
				_modNoWindow.Clear();
				yield return Measure(inRoom: true, inside, insideSteps);
				Note("inside, the town drawn for the windows: frames " + Stats(_withWindow) + ", mod " + Stats(_modWindow) + "; without: frames " + Stats(_noWindow) + ", mod " + Stats(_modNoWindow));
				Mod.SaveFrame("doorwalk_about.png");
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(0.5f);
				Put(_you, _face + _out * 6f);
				Mod.SetYaw(Mathf.Atan2(_out.y, _out.x));
				yield return CivilTest.Wait(0.8f);
				List<float> outside = new List<float>();
				List<float> outsideSteps = new List<float>();
				yield return Measure(inRoom: false, outside, outsideSteps);
				Note("frames inside: " + Stats(inside) + "; outside: " + Stats(outside));
				Note("speed inside (tiles/s per frame): " + Stats(insideSteps) + "; outside: " + Stats(outsideSteps));
			}
		}

		private IEnumerator Measure(bool inRoom, List<float> frames, List<float> speeds)
		{
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			Vector2 last = Here(inRoom);
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 3f)
			{
				Mod.SetYaw(Mod.View.Yaw + Time.unscaledDeltaTime * 1.4f);
				yield return null;
				if (inRoom != Mod.Interior.Inside)
				{
					break;
				}
				Vector2 vector = Here(inRoom);
				frames.Add(Time.unscaledDeltaTime * 1000f);
				if (inRoom)
				{
					bool num = HouseInterior.BackdropFrames != _backdrops;
					_backdrops = HouseInterior.BackdropFrames;
					(num ? _withWindow : _noWindow).Add(Time.unscaledDeltaTime * 1000f);
					(num ? _modWindow : _modNoWindow).Add(Mod.FrameMs);
					if ((vector - last).magnitude > 0.5f)
					{
						Note($"a jump of {(vector - last).magnitude:F2} in a frame: {last} -> {vector}");
					}
				}
				if (Time.unscaledDeltaTime > 0f)
				{
					speeds.Add((vector - last).magnitude / Time.unscaledDeltaTime);
				}
				last = vector;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
		}

		private Vector2 Here(bool inRoom)
		{
			if (!inRoom)
			{
				return _you.current_position;
			}
			return Mod.Interior.Eye;
		}

		private static string Stats(List<float> v)
		{
			if (v.Count == 0)
			{
				return "none";
			}
			List<float> list = new List<float>(v);
			list.Sort();
			float num = 0f;
			foreach (float item in list)
			{
				num += item;
			}
			return $"n {list.Count}, mean {num / (float)list.Count:F2}, median {list[list.Count / 2]:F2}, p90 {list[list.Count * 9 / 10]:F2}, max {list[list.Count - 1]:F2}";
		}

		private IEnumerator DoorByHand()
		{
			if (!Mod.Interior.Inside)
			{
				Mod.EnterHouseForTest(_house);
				yield return CivilTest.Wait(0.5f);
			}
			if (!Check("in the room", Mod.Interior.Inside && Mod.Interior.Fitted))
			{
				yield break;
			}
			Interior room = Mod.Interior.Room;
			Vector2 door = new Vector2(room.Fit.DoorWallX, room.Fit.DoorWallY);
			Vector2 o = new Vector2(room.DoorOutX, room.DoorOutY);
			Mod.Interior.Eye = door - o * 1.1f;
			Mod.SetYaw(Mathf.Atan2(o.y, o.x));
			Mod.SetPitchDegrees(-5f);
			yield return CivilTest.Wait(2f);
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (Mod.Prompt != null && Mod.Prompt.Contains("door"))
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			for (int i = 0; i < 3; i++)
			{
				Mod.Interior.Eye = door - o * 1.1f;
				bool wasOpen = Mod.Interior.DoorHeldOpen;
				int toggles = HouseInterior.DoorToggles;
				Note($"before press {i}: prompt '{Mod.Prompt}', held open {wasOpen}, drawn {Mod.Interior.DoorOpen:F2}");
				PlayInput.TestPress(KeyCode.E);
				yield return null;
				yield return null;
				bool atOnce = HouseInterior.DoorToggles > toggles;
				PlayInput.TestRelease(KeyCode.E);
				yield return null;
				Check(string.Format("E {0} the door at once ({1})", wasOpen ? "closes" : "opens", i), atOnce && Mod.Interior.DoorHeldOpen != wasOpen, $"toggles {HouseInterior.DoorToggles - toggles}, held open {Mod.Interior.DoorHeldOpen}");
				yield return CivilTest.Wait(1.6f);
				Check($"and it stays so ({i})", Mod.Interior.DoorHeldOpen != wasOpen && (wasOpen ? (Mod.Interior.DoorOpen < 0.05f) : (Mod.Interior.DoorOpen > 0.95f)), $"drawn {Mod.Interior.DoorOpen:F2}");
			}
			if (!Mod.Interior.DoorHeldOpen)
			{
				PlayInput.TestPress(KeyCode.E);
				yield return null;
				yield return null;
				PlayInput.TestRelease(KeyCode.E);
				yield return null;
			}
			PlayInput.TestPress(KeyCode.E);
			yield return null;
			yield return null;
			PlayInput.TestRelease(KeyCode.E);
			yield return null;
			PossessionHooks.ScriptedInput = new Vector2(0f, 0.5f);
			yield return CivilTest.Wait(0.15f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return CivilTest.Wait(0.3f);
			Check("a step toward it just after shutting it leaves it shut", !Mod.Interior.DoorHeldOpen, $"eye {(Mod.Interior.Eye - door).magnitude:F2} from the door");
			Mod.Interior.Eye = door - o * 1.1f;
			Mod.SetYaw(Mathf.Atan2(o.y, o.x));
			yield return null;
			PlayInput.TestPress(KeyCode.E);
			yield return CivilTest.Wait(0.8f);
			PlayInput.TestRelease(KeyCode.E);
			yield return CivilTest.Wait(0.3f);
			Check("held E at the door leaves", !Mod.Interior.Inside);
		}

		private static bool OnLand(Vector2 p)
		{
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile?.Type != null && tile.Type.ground && !tile.Type.liquid)
			{
				return !tile.Type.block;
			}
			return false;
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

		private static void Hold(Actor a, Vector2 at)
		{
			try
			{
				a.current_position = at;
				a.dirty_current_tile = true;
				if (a.isAlive())
				{
					a.makeWait(2f);
				}
			}
			catch
			{
			}
		}
	}
}
