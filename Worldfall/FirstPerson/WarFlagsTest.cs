using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WarFlagsTest : CivilTest
	{
		private readonly Queue<Event> _events = new Queue<Event>();

		protected override string Prefix => "WFLAG";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 17, 0);
			if (!Check("our village", king != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(king);
			if (!Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			List<Actor> warriors = new List<Actor>();
			try
			{
				foreach (Actor unit in king.kingdom.getUnits())
				{
					if (unit != null && unit != king && unit.isAlive() && unit.isWarrior())
					{
						warriors.Add(unit);
					}
				}
			}
			catch
			{
			}
			if (!Check("warriors to send", warriors.Count >= 6, warriors.Count + " warriors"))
			{
				yield break;
			}
			List<Actor> bandA = warriors.GetRange(0, warriors.Count / 2);
			List<Actor> bandB = warriors.GetRange(warriors.Count / 2, warriors.Count - warriors.Count / 2);
			WorldTile home = king.current_tile;
			WorldTile east = Spot(home, 0f, 16f);
			WorldTile west = Spot(home, MathF.PI, 16f);
			if (east == null || west == null)
			{
				WorldTile worldTile;
				west = (worldTile = null);
				east = worldTile;
			}
			float[] array = new float[3] { 16f, 12f, 9f };
			foreach (float num in array)
			{
				for (int j = 0; j < 16; j++)
				{
					if (east != null && west != null)
					{
						break;
					}
					float num2 = (float)j * MathF.PI / 8f;
					WorldTile worldTile2 = Spot(home, num2, num);
					if (worldTile2 == null)
					{
						continue;
					}
					for (int k = 4; k <= 12; k++)
					{
						if (east != null && west != null)
						{
							break;
						}
						WorldTile worldTile3 = Spot(home, num2 + (float)k * MathF.PI / 8f, num);
						if (worldTile3 != null && (P(worldTile3) - P(worldTile2)).magnitude > num)
						{
							east = worldTile2;
							west = worldTile3;
						}
					}
				}
			}
			if (!Check("open ground on two sides of the town", east != null && west != null, "one " + ((east != null) ? (east.x + "," + east.y) : "none") + ", two " + ((west != null) ? (west.x + "," + west.y) : "none")))
			{
				yield break;
			}
			WarMap map = Mod.WarMap;
			map.ClearFlags();
			map.Toggle(king, Mod.Toast);
			yield return CivilTest.Wait(1f);
			if (!Check("the war map opens", map.Open))
			{
				yield break;
			}
			yield return View(map, king.current_position, 70f);
			Mark(map, bandA);
			yield return Click(1, Cloth(map, P(east), onFlag: false), shift: true);
			WarFlag f1 = map.FlagNumbered(1);
			Check("a Shift+right click plants flag 1 with the marked", f1 != null && f1.Band.Count == bandA.Count, string.Format("flag 1 {0}, marked {1}", (f1 != null) ? (f1.Tile.x + "," + f1.Tile.y + ", band " + f1.Band.Count) : "none", bandA.Count));
			Mark(map, bandB);
			yield return Click(1, Cloth(map, P(west), onFlag: false), shift: true);
			WarFlag f2 = map.FlagNumbered(2);
			Check("a second Shift+right click plants flag 2 with the others", f2 != null && f2.Band.Count == bandB.Count && map.Flags.Count == 2, string.Format("flag 2 {0}, flags {1}", (f2 != null) ? (f2.Tile.x + "," + f2.Tile.y + ", band " + f2.Band.Count) : "none", map.Flags.Count));
			if (f1 == null || f2 == null)
			{
				yield break;
			}
			int num3 = 0;
			foreach (Actor item2 in warriors)
			{
				if (Royal.IsHolding(item2))
				{
					num3++;
				}
			}
			Check("every one sent is on the hold task", num3 == warriors.Count, num3 + " of " + warriors.Count);
			yield return CivilTest.Wait(1.2f);
			yield return Capture("wflag_map_sent.png");
			map.Close();
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 45f && (At(f1) < 0.75f || At(f2) < 0.75f))
			{
				yield return CivilTest.Wait(1f);
			}
			Note($"arrived after {Time.realtimeSinceStartup - t0:F0} s: flag 1 {Report(f1)}; flag 2 {Report(f2)}");
			Check("band A reaches flag 1", At(f1) >= 0.75f, Report(f1));
			Check("band B reaches flag 2", At(f2) >= 0.75f, Report(f2));
			yield return CivilTest.Wait(20f);
			Check("band A still holds flag 1 twenty seconds on", At(f1) >= 0.75f && f1.Band.Count == bandA.Count, Report(f1));
			Check("band B still holds flag 2 twenty seconds on", At(f2) >= 0.75f && f2.Band.Count == bandB.Count, Report(f2));
			string text = Favorites.Doing(bandA[0]);
			Note("one of band A is " + text);
			Vector2 current_position = king.current_position;
			Mod.SetYaw(Mathf.Atan2(f1.P.y - current_position.y, f1.P.x - current_position.x));
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(1.5f);
			yield return Capture("wflag_fp.png");
			map.Toggle(king, Mod.Toast);
			yield return CivilTest.Wait(0.6f);
			WorldTile east2 = Spot(f1.Tile, 0f, 10f) ?? Spot(f1.Tile, 0.6f, 10f);
			if (east2 != null)
			{
				yield return View(map, (f1.P + P(east2)) * 0.5f, 50f);
				Vector2 vector = Cloth(map, f1.P, onFlag: true);
				Vector2 to = vector + (Screen(map, P(east2)) - Screen(map, f1.P));
				yield return Drag(0, vector, to);
				float magnitude = (f1.P - P(east2)).magnitude;
				Check("dragging flag 1 moves it", magnitude < 2.5f, $"flag 1 at {f1.Tile.x},{f1.Tile.y}, wanted {east2.x},{east2.y} ({magnitude:F1} off)");
				yield return Capture("wflag_map_moved.png", 0.8f);
				map.Close();
				t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 35f && At(f1) < 0.75f)
				{
					yield return CivilTest.Wait(1f);
				}
				Check("band A follows its flag", At(f1) >= 0.75f, $"after {Time.realtimeSinceStartup - t0:F0} s: {Report(f1)}");
				map.Toggle(king, Mod.Toast);
				yield return CivilTest.Wait(0.6f);
			}
			else
			{
				Note("(no ground ten tiles on from flag 1: the drag is skipped)");
			}
			yield return View(map, (f1.P + f2.P) * 0.5f, Mathf.Max(50f, (f1.P - f2.P).magnitude + 30f));
			map.Marked.Clear();
			_events.Enqueue(new Event
			{
				type = EventType.KeyDown,
				keyCode = KeyCode.Alpha1
			});
			yield return Flush();
			Check("1 marks flag 1's band", map.Marked.Count == f1.Band.Count && map.Marked.SetEquals(f1.Band), $"marked {map.Marked.Count}, band {f1.Band.Count}");
			Check("...and the map goes to them", (map.Centre - f1.P).magnitude < 12f, $"centre {map.Centre}, flag 1 {f1.P}");
			yield return View(map, (f1.P + f2.P) * 0.5f, Mathf.Max(50f, (f1.P - f2.P).magnitude + 30f));
			yield return Click(1, Cloth(map, f2.P, onFlag: true));
			Check("band A joins flag 2, flag 1 comes down", map.FlagNumbered(1) == null && f2.Band.Count == warriors.Count, $"flags {map.Flags.Count}, flag 2's band {f2.Band.Count} of {warriors.Count}");
			yield return CivilTest.Wait(0.8f);
			yield return Capture("wflag_map_joined.png");
			map.Marked.Clear();
			yield return Click(1, Cloth(map, f2.P, onFlag: true));
			Check("a right click with nobody marked pulls flag 2 up", map.Flags.Count == 0, "flags " + map.Flags.Count);
			yield return CivilTest.Wait(1.5f);
			num3 = 0;
			foreach (Actor item3 in warriors)
			{
				if (Royal.IsHolding(item3) || map.FlagOf(item3) != null)
				{
					num3++;
				}
			}
			Check("nobody holds a flag any more", num3 == 0, num3 + " still at it");
			WorldTile sea = Sea(home, 60);
			if (sea != null)
			{
				yield return View(map, P(sea), 40f);
				yield return Click(1, Screen(map, P(sea)), shift: true);
				WarFlag warFlag = map.FlagNumbered(1);
				bool ok = warFlag == null || Royal.Standable(warFlag.Tile);
				Check("a right click on the sea plants no flag in it", ok, (warFlag == null) ? "no flag (open sea)" : string.Format("flag on {0},{1}, {2}, {3:F1} from the click", warFlag.Tile.x, warFlag.Tile.y, (warFlag.Tile.Type != null) ? warFlag.Tile.Type.id : "?", (P(warFlag.Tile) - P(sea)).magnitude));
				map.ClearFlags();
			}
			else
			{
				Note("(no sea within 60 tiles: the shore check is skipped)");
			}
			map.Close();
		}

		private static Vector2 P(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static WorldTile Spot(WorldTile from, float angle, float distance)
		{
			for (float num = distance; num < distance + 12f; num += 1.5f)
			{
				for (int i = 0; i < 9; i++)
				{
					float f = angle + (float)(((i % 2 == 0) ? 1 : (-1)) * ((i + 1) / 2)) * 0.17f;
					WorldTile tile = World.world.GetTile(from.x + Mathf.RoundToInt(Mathf.Cos(f) * num), from.y + Mathf.RoundToInt(Mathf.Sin(f) * num));
					if (tile == null || !Royal.Standable(tile))
					{
						continue;
					}
					try
					{
						if (!tile.isSameIsland(from))
						{
							continue;
						}
					}
					catch
					{
						continue;
					}
					bool flag = true;
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							flag = Royal.Standable(World.world.GetTile(tile.x + k, tile.y + j));
						}
					}
					if (flag)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static WorldTile Sea(WorldTile from, int reach)
		{
			for (int i = 4; i < reach; i += 2)
			{
				for (int j = 0; j < 24; j++)
				{
					float f = (float)j * MathF.PI / 12f;
					WorldTile tile = World.world.GetTile(from.x + Mathf.RoundToInt(Mathf.Cos(f) * (float)i), from.y + Mathf.RoundToInt(Mathf.Sin(f) * (float)i));
					if (tile != null && tile.is_liquid && WarMap.GroundFor(tile) != null)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static float At(WarFlag f)
		{
			if (f.Band.Count == 0)
			{
				return 0f;
			}
			float num = WarMap.Spread(f.Band.Count) + 3f;
			int num2 = 0;
			foreach (Actor item in f.Band)
			{
				if ((item.current_position - f.P).magnitude <= num)
				{
					num2++;
				}
			}
			return (float)num2 / (float)f.Band.Count;
		}

		private static string Report(WarFlag f)
		{
			float num = 0f;
			float num2 = 0f;
			foreach (Actor item in f.Band)
			{
				float magnitude = (item.current_position - f.P).magnitude;
				num += magnitude;
				num2 = Mathf.Max(num2, magnitude);
			}
			int num3 = 0;
			foreach (Actor item2 in f.Band)
			{
				if (Royal.IsHolding(item2))
				{
					num3++;
				}
			}
			return $"{At(f) * 100f:F0}% of {f.Band.Count} about it (mean {((f.Band.Count > 0) ? (num / (float)f.Band.Count) : 0f):F1}, worst {num2:F1} tiles), " + $"{num3} on the hold task, counted {f.Holding} holding / {f.Coming} coming / {f.Fighting} fighting / {f.Eating} eating";
		}

		private void Mark(WarMap map, List<Actor> who)
		{
			map.Marked.Clear();
			foreach (Actor item in who)
			{
				map.Marked.Add(item);
			}
		}

		private IEnumerator View(WarMap map, Vector2 at, float across)
		{
			map.Centre = at;
			map.Zoom = Mathf.Clamp(map.Span / across, map.ZoomMin, map.ZoomMax);
			yield return null;
			yield return null;
			yield return CivilTest.Wait(0.3f);
		}

		private static Vector2 Screen(WarMap map, Vector2 world)
		{
			return WorldBoxMod.Instance.HudForTest.WarMapToScreenForTest?.Invoke(world) ?? Vector2.zero;
		}

		private static Vector2 Cloth(WarMap map, Vector2 world, bool onFlag)
		{
			return Screen(map, world) + (onFlag ? (new Vector2(4f, -14f) * GameArt.P) : Vector2.zero);
		}

		private IEnumerator Click(int button, Vector2 at, bool shift = false)
		{
			return Drag(button, at, at, shift);
		}

		private IEnumerator Drag(int button, Vector2 from, Vector2 to, bool shift = false)
		{
			EventModifiers modifiers = (shift ? EventModifiers.Shift : EventModifiers.None);
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = button,
				mousePosition = from,
				clickCount = 1,
				modifiers = modifiers
			});
			if (to != from)
			{
				for (int i = 1; i <= 10; i++)
				{
					_events.Enqueue(new Event
					{
						type = EventType.MouseDrag,
						button = button,
						mousePosition = Vector2.Lerp(from, to, (float)i / 10f),
						modifiers = modifiers
					});
				}
			}
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = button,
				mousePosition = to,
				clickCount = 1,
				modifiers = modifiers
			});
			yield return Flush();
		}

		private IEnumerator Flush()
		{
			while (_events.Count > 0)
			{
				yield return null;
			}
			yield return null;
			yield return null;
		}

		private IEnumerator Capture(string name, float wait = 0.5f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("captured " + name);
		}

		private void OnGUI()
		{
			if (_events.Count == 0 || Mod == null || !Mod.WarMap.Open)
			{
				return;
			}
			Event current = Event.current;
			try
			{
				while (_events.Count > 0)
				{
					Event.current = _events.Dequeue();
					Mod.HudForTest.DrawWarMap();
				}
				Event.current = new Event
				{
					type = EventType.Layout,
					mousePosition = current.mousePosition
				};
				Mod.HudForTest.DrawWarMap();
			}
			catch (Exception ex)
			{
				Log.Error("WFLAG events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}
	}
}
