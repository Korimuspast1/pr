using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BoatStackTest : CivilTest
	{
		private Actor _king;

		private City _town;

		private readonly List<Actor> _boats = new List<Actor>();

		private readonly List<Building> _docks = new List<Building>();

		private static readonly string[] Kinds = new string[3] { "boat_transport_human", "boat_trading_human", "boat_fishing" };

		private static readonly float[] Taper = new float[5] { 0.55f, 0.9f, 1f, 0.9f, 0.6f };

		protected override string Prefix => "BSTACK";

		protected override IEnumerator Body()
		{
			foreach (WorldTile item in KingTest.GoodSites())
			{
				Actor actor = KingTest.FoundKingdom(item, "human", 4, 0);
				if (actor == null)
				{
					continue;
				}
				Building building = Harbour(actor.city, Royal.Position(actor.city), 4f, 120f);
				if (building != null)
				{
					_king = actor;
					_town = actor.city;
					_docks.Add(building);
					break;
				}
				try
				{
					foreach (Actor item2 in new List<Actor>(actor.city.units))
					{
						item2.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			if (!Check("a town by the sea with a harbour", _king != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_king).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_king);
			if (!Check("the king in first person", InControl(_king)))
			{
				yield break;
			}
			Look(DockAt(_docks[0]));
			for (float t = 0f; t < 15f; t += 0.25f)
			{
				if (DockDecks.Count != 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("the harbour is drawn", DockDecks.Count > 0, $"{DockDecks.Count} docks");
			yield return Part("one harbour, its three boats", Crowd(0, 40f, "one"));
			Building building2 = Harbour(_town, DockAt(_docks[0]), 4f, 12f);
			if (building2 != null)
			{
				_docks.Add(building2);
				Look((DockAt(_docks[0]) + DockAt(building2)) * 0.5f);
				yield return CivilTest.Wait(2f);
				yield return Part("a second harbour beside it, three more", Crowd(1, 45f, "two"));
			}
			else
			{
				Note("no room for a second harbour beside the first: skipped");
			}
			Look(DockAt(_docks[_docks.Count - 1]));
			yield return Part("at the helm of one, sailing in among them", Helm());
			foreach (Actor boat in _boats)
			{
				try
				{
					boat.dieSimpleNone();
				}
				catch
				{
				}
			}
		}

		private IEnumerator Helm()
		{
			Actor ship = _boats.Find(delegate(Actor b)
			{
				try
				{
					return b.isAlive() && b.asset.id.StartsWith("boat_transport", StringComparison.Ordinal);
				}
				catch
				{
					return false;
				}
			});
			if (!Check("a ship of ours to take", ship != null))
			{
				yield break;
			}
			yield return Possess(ship);
			if (!Check("at her helm", BoatHelm.Steers(ship), "host " + WorldBoxMod.Describe(Mod.Host)))
			{
				yield break;
			}
			Actor actor = null;
			float num = float.MaxValue;
			foreach (Actor boat in _boats)
			{
				if (boat == ship)
				{
					continue;
				}
				try
				{
					if (!boat.isAlive())
					{
						continue;
					}
				}
				catch
				{
					continue;
				}
				float sqrMagnitude = (boat.current_position - ship.current_position).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					actor = boat;
				}
			}
			if (actor != null)
			{
				Vector2 vector = actor.current_position - ship.current_position;
				BoatHelm.Heading = Mathf.Atan2(vector.y, vector.x);
				BoatHelm.Sails = 0.6f;
				BoatHelm.Way = Mathf.Max(BoatHelm.Way, 0.4f);
				Note($"sailing #{_boats.IndexOf(ship)} at #{_boats.IndexOf(actor)}, {Mathf.Sqrt(num):F1} tiles off");
			}
			Look(ship.current_position);
			yield return Watch(18f, "helm");
			BoatHelm.Sails = 0f;
		}

		private IEnumerator Crowd(int dock, float seconds, string label)
		{
			Building building = _docks[dock];
			List<WorldTile> list = new List<WorldTile>();
			try
			{
				building.component_docks.recalculateOceanTiles();
				foreach (ref WorldTile item in building.component_docks.tiles_ocean)
				{
					WorldTile current = item;
					if (current != null)
					{
						list.Add(current);
					}
				}
			}
			catch (Exception ex)
			{
				Note("sea tiles: " + ex.Message);
			}
			Note($"harbour {dock} at {DockAt(building)}: {list.Count} sea tiles for its boats");
			if (!Check($"harbour {dock} has sea tiles for boats", list.Count > 0))
			{
				yield break;
			}
			System.Random random = new System.Random(17 + dock);
			string[] kinds = Kinds;
			foreach (string text in kinds)
			{
				Actor actor = null;
				WorldTile worldTile = list[random.Next(list.Count)];
				try
				{
					actor = World.world.units.createNewUnit(text, worldTile);
					if (actor != null)
					{
						building.component_docks.addBoatToDock(actor);
						actor.joinKingdom(_town.kingdom);
						if (actor.city != _town)
						{
							actor.joinCity(_town);
						}
					}
				}
				catch (Exception ex2)
				{
					Note(text + ": " + ex2.Message);
				}
				if (actor != null)
				{
					_boats.Remove(actor);
					_boats.Add(actor);
					Note($"{text} made at {worldTile.x},{worldTile.y}");
				}
			}
			Check($"harbour {dock}'s boats made", _boats.Count >= 3 * (dock + 1) - 1, $"{_boats.Count} boats in all");
			yield return Watch(seconds, label);
		}

		private IEnumerator Watch(float seconds, string label)
		{
			float worstShare = -9f;
			float worstUp = -9f;
			float worstGap = 9f;
			float worstRaised = -9f;
			float longest = 0f;
			float longestStill = 0f;
			string worstUpAt = "";
			string worstGapAt = "";
			string longestAt = "";
			string longestStillAt = "";
			int frames = 0;
			int overlapFrames = 0;
			int upFrames = 0;
			int sameTileFrames = 0;
			bool savedStack = false;
			bool savedUp = false;
			Dictionary<int, float> since = new Dictionary<int, float>();
			Dictionary<int, float> sinceStill = new Dictionary<int, float>();
			float t0 = Time.realtimeSinceStartup;
			float nextLine = 0f;
			Frame("bstack_" + label + "_start");
			while (Time.realtimeSinceStartup - t0 < seconds)
			{
				yield return null;
				float num = Time.realtimeSinceStartup - t0;
				List<Actor> list = _boats.FindAll(delegate(Actor b)
				{
					try
					{
						return b?.isAlive() ?? false;
					}
					catch
					{
						return false;
					}
				});
				frames++;
				bool flag = false;
				StringBuilder stringBuilder = new StringBuilder();
				for (int num2 = 0; num2 < list.Count; num2++)
				{
					for (int num3 = num2 + 1; num3 < list.Count; num3++)
					{
						WorldTile current_tile = list[num2].current_tile;
						WorldTile current_tile2 = list[num3].current_tile;
						if (current_tile != null && current_tile == current_tile2)
						{
							flag = true;
							stringBuilder.Append($" {_boats.IndexOf(list[num2])}-{_boats.IndexOf(list[num3])}@{current_tile.x},{current_tile.y}");
						}
					}
				}
				if (flag)
				{
					sameTileFrames++;
				}
				float num4 = 9f;
				string text = "";
				HashSet<int> hashSet = new HashSet<int>();
				HashSet<int> hashSet2 = new HashSet<int>();
				for (int num5 = 0; num5 < list.Count; num5++)
				{
					for (int num6 = num5 + 1; num6 < list.Count; num6++)
					{
						float num7 = Gap(list[num5], list[num6]);
						if (num7 < -8f)
						{
							continue;
						}
						int num8 = _boats.IndexOf(list[num5]);
						int num9 = _boats.IndexOf(list[num6]);
						int num10 = num8 * 64 + num9;
						if (num7 < num4)
						{
							num4 = num7;
							text = $"{num8}-{num9}";
						}
						if (num7 >= -0.3f)
						{
							continue;
						}
						hashSet.Add(num10);
						bool flag2 = false;
						try
						{
							flag2 = !list[num5].is_moving && !list[num6].is_moving;
						}
						catch
						{
						}
						if (flag2)
						{
							hashSet2.Add(num10);
							if (!sinceStill.TryGetValue(num10, out var value))
							{
								value = (sinceStill[num10] = num);
							}
							if (num - value > longestStill)
							{
								longestStill = num - value;
								longestStillAt = $"{num8}-{num9} from {value:F1}s, gap now {num7:F2}";
							}
						}
						if (!since.TryGetValue(num10, out var value2))
						{
							value2 = (since[num10] = num);
						}
						if (num - value2 > longest)
						{
							longest = num - value2;
							longestAt = $"{num8}-{num9} from {value2:F1}s, gap now {num7:F2}";
						}
						if (num - value2 > 2f && num - value2 - Time.unscaledDeltaTime <= 2f)
						{
							Note($"{label} {num8}-{num9} one in another 2 s, gap {num7:F2}: #{num8} {Describe(list[num5])}; #{num9} {Describe(list[num6])}");
						}
					}
				}
				foreach (int item in new List<int>(since.Keys))
				{
					if (!hashSet.Contains(item))
					{
						since.Remove(item);
					}
				}
				foreach (int item2 in new List<int>(sinceStill.Keys))
				{
					if (!hashSet2.Contains(item2))
					{
						sinceStill.Remove(item2);
					}
				}
				if (num4 < -0.3f)
				{
					overlapFrames++;
				}
				if (num4 < worstGap)
				{
					worstGap = num4;
					worstGapAt = $"{text} at {num:F1}s";
				}
				float num13 = -9f;
				StringBuilder stringBuilder2 = new StringBuilder();
				foreach (Actor item3 in list)
				{
					int num14 = _boats.IndexOf(item3);
					if (!Creatures.RideForTest(item3, out var z, out var water, out var keel))
					{
						stringBuilder2.Append($" #{num14} unseen");
						continue;
					}
					float num15 = z - water;
					float num16 = num15 / Mathf.Max(0.1f, keel);
					num13 = Mathf.Max(num13, num16);
					worstRaised = Mathf.Max(worstRaised, z - keel - water);
					worstUp = Mathf.Max(worstUp, num15);
					if (num16 > worstShare)
					{
						worstShare = num16;
						worstUpAt = $"#{num14} at {num:F1}s, up {num15:F2}, keel {keel:F2}, {Describe(item3)}";
					}
					stringBuilder2.Append($" #{num14} {Describe(item3)} up {num15:F2} keel {keel:F2}");
				}
				if (num13 > 0.5f)
				{
					upFrames++;
				}
				if (!savedStack && num4 < -0.6f && num > 3f)
				{
					savedStack = true;
					Frame("bstack_" + label + "_stacked");
					Note($"stacked frame at {num:F1}s: {text} gap {num4:F2};{stringBuilder2}");
				}
				else if (!savedUp && num13 > 0.5f && num > 3f)
				{
					savedUp = true;
					Frame("bstack_" + label + "_floating");
					Note($"floating frame at {num:F1}s: up {num13:F2} of a keel;{stringBuilder2}");
				}
				if (num >= nextLine)
				{
					nextLine = num + 2f;
					Note(string.Format("{0} {1:F0}s:{2} | closest {3} {4:F2}{5}", label, num, stringBuilder2, text, num4, flag ? (" | one tile:" + stringBuilder) : ""));
				}
			}
			Frame("bstack_" + label + "_end");
			yield return CivilTest.Wait(0.4f);
			Vector2 mid = Vector2.zero;
			int seen = 0;
			foreach (Actor boat in _boats)
			{
				try
				{
					if (boat.isAlive())
					{
						mid += boat.current_position;
						seen++;
					}
				}
				catch
				{
				}
			}
			if (seen > 0)
			{
				Side(mid / seen);
				yield return CivilTest.Wait(0.6f);
				Frame("bstack_" + label + "_side");
				yield return CivilTest.Wait(0.3f);
				Look(mid / seen);
			}
			Note($"{label} summary: {frames} frames, two on one tile in {sameTileFrames}, hulls one in another in {overlapFrames} (worst {worstGap:F2}, {worstGapAt}; longest {longest:F1}s, {longestAt}; both lying still {longestStill:F1}s, {longestStillAt}), more than half a keel above the water in {upFrames} (worst {worstShare:F2} of a keel: {worstUpAt}), keel bottom above the water at worst {worstRaised:F2}");
			Check(label + ": no hull drawn out of the water (her middle never half her draught over it)", worstShare < 0.5f, $"worst {worstShare:F2} of her draught, {worstUp:F2} tiles; {upFrames} of {frames} frames over");
			Check(label + ": two ships lying still are never drawn one in another (pushed apart within 2.5 s)", longestStill < 2.5f, $"longest {longestStill:F1}s ({longestStillAt})");
			Check(label + ": two passing under way are drawn one in another only a moment (within 6 s)", longest < 6f, $"longest {longest:F1}s ({longestAt}); worst {worstGap:F2}; {overlapFrames} of {frames} frames with some overlap");
		}

		private void Frame(string name)
		{
			Mod.SaveFrame(name + ".png");
			try
			{
				if (!Mod.HudForTest.CapturePending)
				{
					Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name + "_hd.png"));
				}
			}
			catch
			{
			}
		}

		private string Describe(Actor b)
		{
			try
			{
				WorldTile current_tile = b.current_tile;
				Vector2 mid;
				float heading;
				float half;
				float beam;
				Vector2 offset;
				string text = (Creatures.HullForTest(b, out mid, out heading, out half, out beam, out offset) ? $"drawn {mid.x:F1},{mid.y:F1} (off {offset.magnitude:F1}, {Vector2.Distance(mid, Mod.DebugEye):F0} from the eye)" : "not drawn");
				return string.Format("{0} tile {1},{2}{3} {4}", b.asset.id.Replace("boat_", ""), current_tile?.x, current_tile?.y, b.is_moving ? " moving" : "", text);
			}
			catch
			{
				return "?";
			}
		}

		private static float Gap(Actor a, Actor b)
		{
			if (!Creatures.HullForTest(a, out var mid, out var heading, out var half, out var beam, out var offset))
			{
				return -9f;
			}
			if (!Creatures.HullForTest(b, out var mid2, out var heading2, out var half2, out var beam2, out offset))
			{
				return -9f;
			}
			Vector2 vector = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
			Vector2 vector2 = new Vector2(Mathf.Cos(heading2), Mathf.Sin(heading2));
			float num = float.MaxValue;
			for (int i = 0; i < 5; i++)
			{
				for (int j = 0; j < 5; j++)
				{
					Vector2 a2 = mid + vector * ((-0.85f + (float)i * 0.425f) * half);
					Vector2 b2 = mid2 + vector2 * ((-0.85f + (float)j * 0.425f) * half2);
					num = Mathf.Min(num, Vector2.Distance(a2, b2) - beam * Taper[i] - beam2 * Taper[j]);
				}
			}
			return num;
		}

		private static Vector2 DockAt(Building d)
		{
			if (d == null || d.current_tile == null)
			{
				return Vector2.zero;
			}
			return new Vector2((float)d.current_tile.x + 0.5f, (float)d.current_tile.y + 0.5f);
		}

		private static bool Wet(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			try
			{
				return tile != null && tile.is_liquid && tile.Type != null && !tile.Type.lava;
			}
			catch
			{
				return false;
			}
		}

		private Building Harbour(City town, Vector2 c, float min, float max)
		{
			WorldTile worldTile = null;
			for (float num = min; num < max; num += 1f)
			{
				if (worldTile != null)
				{
					break;
				}
				for (int i = 0; i < 64; i++)
				{
					if (worldTile != null)
					{
						break;
					}
					float f = (float)i / 64f * MathF.PI * 2f;
					int num2 = (int)(c.x + Mathf.Cos(f) * num);
					int num3 = (int)(c.y + Mathf.Sin(f) * num);
					if (num2 < 16 || num3 < 16 || num2 >= MapBox.width - 16 || num3 >= MapBox.height - 16)
					{
						continue;
					}
					bool flag = true;
					for (int j = 0; j <= 4 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							if (!Wet(num2 + k, num3 + j))
							{
								flag = false;
							}
						}
					}
					if (!flag)
					{
						continue;
					}
					foreach (Building dock in _docks)
					{
						if (dock.current_tile != null && Mathf.Abs(dock.current_tile.x - num2) < 4 && Mathf.Abs(dock.current_tile.y - num3) < 4)
						{
							flag = false;
						}
					}
					if (!flag)
					{
						continue;
					}
					for (int l = 0; l < 8; l++)
					{
						if (worldTile != null)
						{
							break;
						}
						float f2 = (float)l * MathF.PI / 4f;
						int x = num2 + Mathf.RoundToInt(Mathf.Cos(f2) * 4.5f);
						int y = num3 + 2 + Mathf.RoundToInt(Mathf.Sin(f2) * 4.5f);
						if (Wet(x, y))
						{
							continue;
						}
						bool flag2 = true;
						for (int m = 4; m <= 14 && flag2; m++)
						{
							if (!Wet(num2 + Mathf.RoundToInt((0f - Mathf.Cos(f2)) * (float)m), num3 + 2 + Mathf.RoundToInt((0f - Mathf.Sin(f2)) * (float)m)))
							{
								flag2 = false;
							}
						}
						if (flag2)
						{
							worldTile = World.world.GetTile(num2, num3);
						}
					}
				}
			}
			if (worldTile == null)
			{
				return null;
			}
			try
			{
				for (int n = -1; n <= 6; n++)
				{
					for (int num4 = -3; num4 <= 3; num4++)
					{
						WorldTile tile = World.world.GetTile(worldTile.x + num4, worldTile.y + n);
						if (tile != null && tile.zone != null && tile.zone.city == null)
						{
							town.addZone(tile.zone);
						}
					}
				}
				Building building = World.world.buildings.addBuilding("docks_human", worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					return null;
				}
				building.completeConstruction();
				try
				{
					building.setKingdom(town.kingdom);
				}
				catch
				{
				}
				List<Building> buildingListOfType = town.getBuildingListOfType("type_docks");
				if (buildingListOfType == null || !buildingListOfType.Contains(building))
				{
					town.listBuilding(building);
				}
				Note($"harbour at {worldTile.x},{worldTile.y}, {Vector2.Distance(c, new Vector2(worldTile.x, worldTile.y)):F0} tiles from {c}");
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("BSTACK harbour: " + ex);
				return null;
			}
		}

		private void Look(Vector2 p)
		{
			Eye(p, 8f, 24f, -62f);
		}

		private void Side(Vector2 p)
		{
			Eye(p, 16f, 1.6f, -3f);
		}

		private void Eye(Vector2 p, float least, float height, float pitch)
		{
			TerrainCache terrainForTest = Mod.TerrainForTest;
			Vector2 debugEye = p + new Vector2(least, 0f);
			int num = -1;
			for (float num2 = least; num2 <= least + 6f; num2 += 3f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = p + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num2;
					if (!(vector.x < 1f) && !(vector.y < 1f) && !(vector.x >= (float)(terrainForTest.Width - 1)) && !(vector.y >= (float)(terrainForTest.Height - 1)))
					{
						int num3 = (int)vector.y * terrainForTest.Width + (int)vector.x;
						int num4 = (((terrainForTest.Flags[num3] & 1) != 0) ? terrainForTest.SeaReach[num3] : (-1));
						if (num4 > num)
						{
							num = num4;
							debugEye = vector;
						}
					}
				}
			}
			Mod.DebugEye = debugEye;
			Mod.DebugEyeHeight = height;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(p.y - debugEye.y, p.x - debugEye.x));
			Mod.SetPitchDegrees(pitch);
		}
	}
}
