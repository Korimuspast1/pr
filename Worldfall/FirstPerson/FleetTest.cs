using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FleetTest : CivilTest
	{
		private readonly Queue<Event> _events = new Queue<Event>();

		private readonly List<Actor> _afloat = new List<Actor>();

		private float _afloatAt;

		private Actor _pinned;

		private Vector2 _pinAt;

		protected override string Prefix => "FLEET";

		private void Update()
		{
			try
			{
				if (_pinned != null && _pinned.isAlive())
				{
					_pinned.current_position = _pinAt;
				}
			}
			catch
			{
			}
			if (Time.realtimeSinceStartup - _afloatAt < 1f || _afloat.Count == 0)
			{
				return;
			}
			_afloatAt = Time.realtimeSinceStartup;
			int num = 0;
			try
			{
				num = (int)World.world.getCurWorldTime();
			}
			catch
			{
				return;
			}
			foreach (Actor item in _afloat)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						item.data.set("existence_check", num);
					}
				}
				catch
				{
				}
			}
		}

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 10, 0);
			if (!Check("our village", king != null))
			{
				yield break;
			}
			Kingdom home = king.kingdom;
			City ourTown = king.city;
			Actor rival = null;
			WorldTile sea = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if ((new Vector2(item.x, item.y) - Royal.Position(ourTown)).magnitude < 45f)
				{
					continue;
				}
				WorldTile worldTile = OpenSea(new Vector2((float)item.x + 0.5f, (float)item.y + 0.5f), 9f, 18f, 2);
				if (worldTile != null)
				{
					rival = KingTest.FoundKingdom(item, "orc", 8, 0);
					if (rival != null)
					{
						sea = worldTile;
						break;
					}
				}
			}
			if (!Check("a rival town on a coast", rival != null && sea != null && rival.kingdom != home))
			{
				yield break;
			}
			City town = rival.city;
			Vector2 townAt = Royal.Position(town);
			Note($"rival {town.name} at {townAt}, open sea at {sea.x},{sea.y} ({(P(sea) - townAt).magnitude:F0} tiles off)");
			yield return Possess(king);
			if (!Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield break;
			}
			try
			{
				Mod.Royal.DeclareWar(king, rival.kingdom, Mod.Toast, delegate
				{
				});
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			Check("at war with the rival", Royal.AtWar(home, rival.kingdom));
			WorldTile start = OpenSea(townAt, 22f, 34f, 2) ?? sea;
			List<Actor> ships = new List<Actor>();
			for (int num = 0; num < 2; num++)
			{
				WorldTile at = OpenSea(P(start) + new Vector2((float)num * 3f, 0f), 0f, 5f, 1) ?? start;
				Actor actor = Ship("boat_transport_human", at, home, ourTown);
				if (actor != null)
				{
					ships.Add(actor);
					_afloat.Add(actor);
				}
			}
			if (!Check("two ships of ours at sea", ships.Count == 2 && WarMap.IsShip(ships[0]) && WarMap.Armed(ships[0]), $"{ships.Count} ships, armed {ships.Count > 0 && WarMap.Armed(ships[0])}, reach {((ships.Count > 0) ? ships[0].getAttackRange() : 0f):F1}"))
			{
				yield break;
			}
			WarMap map = Mod.WarMap;
			map.ClearFleet();
			map.Toggle(king, Mod.Toast);
			yield return CivilTest.Wait(1f);
			if (!Check("the war map opens", map.Open))
			{
				yield break;
			}
			yield return View(map, (P(start) + townAt) * 0.5f, Mathf.Max(60f, (P(start) - townAt).magnitude + 30f));
			Check("both sides' ships are on the map", map.OurShips >= 2, $"ours {map.OurShips}, theirs {map.TheirShips}");
			map.MarkShips(add: false);
			Check("Mark ships marks them", map.MarkedShips().Count == ships.Count, map.MarkedShips().Count + " marked");
			WorldTile dest = OpenSea(P(start), 9f, 14f, 1, straight: true, unclaimed: true);
			if (dest != null)
			{
				yield return Click(1, Screen(map, P(dest)), ctrl: false);
				int num2 = 0;
				int num3 = 0;
				foreach (Actor item2 in ships)
				{
					ShipOrder shipOrder = map.OrderOf(item2);
					if (shipOrder != null && shipOrder.What == ShipOrder.Kind.Sail)
					{
						num2++;
					}
					if (Royal.IsSailing(item2))
					{
						num3++;
					}
				}
				Check("a right-click on water sails them", num2 == ships.Count, num2 + " ordered, " + num3 + " under way already (the rest within a second)");
				float t0 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t0 < 30f && !AllNear(ships, P(dest), 6f))
				{
					yield return CivilTest.Wait(0.5f);
				}
				Check("they get there", AllNear(ships, P(dest), 6f), $"after {Time.realtimeSinceStartup - t0:F0} s: {Dist(ships, P(dest))}");
				map.MarkShips(add: false);
			}
			else
			{
				Note("(no open water a dozen tiles off: the sail check is skipped)");
			}
			int people0 = Count(town);
			int buildings0 = Buildings(town);
			float hull0 = TownHealth(town);
			yield return View(map, (ships[0].current_position + townAt) * 0.5f, Mathf.Max(60f, (ships[0].current_position - townAt).magnitude + 30f));
			map.MarkShips(add: false);
			yield return Click(1, Screen(map, TownClick(town)), ctrl: false);
			int num4 = 0;
			foreach (Actor item3 in ships)
			{
				if (map.OrderOf(item3)?.Town == town)
				{
					num4++;
				}
			}
			Check("a right-click on the town has them fire on it", num4 == ships.Count, num4 + " ordered");
			float t1 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t1 < 50f && Shots(map, ships) < 4)
			{
				yield return CivilTest.Wait(0.5f);
			}
			Check("they close in and fire", Shots(map, ships) >= 4, $"after {Time.realtimeSinceStartup - t1:F0} s: {Shots(map, ships)} shots, {Dist(ships, townAt)} from the town");
			yield return Capture("fleet_bombard.png");
			while (Time.realtimeSinceStartup - t1 < 40f && Shots(map, ships) < 10 && town.isAlive())
			{
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(2f);
			Check("the town is hit", Count(town) < people0 || Buildings(town) < buildings0 || TownHealth(town) < hull0 || !town.isAlive(), $"{Shots(map, ships)} shots: people {people0} -> {Count(town)}, buildings {buildings0} -> {Buildings(town)}, their health {hull0:F0} -> {TownHealth(town):F0}");
			WorldTile worldTile2 = OpenSea(ships[0].current_position, 7f, 11f, 1);
			Actor foe = ((worldTile2 != null) ? Ship("boat_transport_orc", worldTile2, rival.kingdom, town.isAlive() ? town : null) : null);
			if (foe != null)
			{
				_afloat.Add(foe);
				yield return CivilTest.Wait(0.5f);
				yield return View(map, (ships[0].current_position + foe.current_position) * 0.5f, 40f);
				map.MarkShips(add: false);
				float t0 = Health(foe);
				yield return Click(1, Screen(map, foe.current_position), ctrl: false);
				Check("a right-click on a rival ship fires on her", map.OrderOf(ships[0])?.Target == foe, "target " + (map.OrderOf(ships[0])?.TargetName ?? "none"));
				float t2 = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - t2 < 30f && foe.isAlive() && Health(foe) >= t0)
				{
					yield return CivilTest.Wait(0.5f);
				}
				Check("she is hit", !foe.isAlive() || Health(foe) < t0, string.Format("after {0:F0} s: hull {1:F0} -> {2}", Time.realtimeSinceStartup - t2, t0, foe.isAlive() ? Health(foe).ToString("F0") : "sunk"));
			}
			else
			{
				Note("(no water for a rival ship: that check is skipped)");
			}
			yield return View(map, Royal.Position(ourTown), 40f);
			map.MarkShips(add: false);
			_ = map.ShipOrders.Count;
			ShipOrder shipOrder2 = map.OrderOf(ships[0]);
			if (shipOrder2 == null)
			{
				_ = Vector2.zero;
			}
			else
			{
				_ = shipOrder2.Aim;
			}
			yield return Click(1, Screen(map, TownClick(ourTown)), ctrl: true);
			ShipOrder shipOrder3 = map.OrderOf(ships[0]);
			Check("they won't fire on our own town", shipOrder3 == null || (shipOrder3.Town != ourTown && (shipOrder3.Aim - Royal.Position(ourTown)).magnitude > 6f), (shipOrder3 != null) ? ("order on " + shipOrder3.TargetName) : "no order");
			Actor lead = ships.Find((Actor a) => a?.isAlive() ?? false);
			Note(ships.FindAll((Actor a) => a?.isAlive() ?? false).Count + " of our ships afloat");
			if (lead == null)
			{
				yield break;
			}
			WorldTile shore = Shore(lead.current_position, 5f, 11f);
			if (shore != null)
			{
				Actor ourMan = CivilTest.SpawnNear("human", P(shore));
				try
				{
					ourMan.joinKingdom(home);
					((BaseSimObject)ourMan).addStatusEffect("invincible", 600f, pColorEffect: false);
					ourMan.makeWait(30f);
				}
				catch
				{
				}
				if (ourMan != null)
				{
					_pinned = ourMan;
					_pinAt = ourMan.current_position;
				}
				yield return View(map, P(shore), 40f);
				map.MarkShips(add: false);
				yield return Click(1, Screen(map, ourMan?.current_position ?? P(shore)), ctrl: true);
				yield return CivilTest.Wait(6f);
				ShipOrder o = map.OrderOf(lead);
				Check("they hold fire on a spot among our own", o != null && o.Shots == 0 && o.Holding == "your own are there", (o != null) ? $"shots {o.Shots}, holding \"{o.Holding}\", {(ourMan.current_position - o.Aim).magnitude:F1} from our man" : "no order");
				_pinned = null;
				CivilTest.Kill(ourMan);
				yield return CivilTest.Wait(3f);
				o = map.OrderOf(lead);
				Check("then bombard the bare land", o != null && o.Shots > 0, (o != null) ? $"shots {o.Shots}" : "no order");
				yield return Capture("fleet_spot.png");
				map.Marked.Clear();
				if (o != null)
				{
					yield return Click(1, Screen(map, o.Mark), ctrl: false);
					Check("a right-click on the reticle calls them off", map.OrderOf(lead) == null, map.ShipOrders.Count + " orders left");
				}
			}
			else
			{
				Note("(no shore near: the spot checks are skipped)");
			}
			map.MarkShips(add: false);
			if (town.isAlive())
			{
				yield return Click(1, Screen(map, TownClick(town)), ctrl: false);
			}
			map.ClearFleet();
			yield return CivilTest.Wait(0.5f);
			int num5 = 0;
			foreach (Actor item4 in ships)
			{
				if (item4.isAlive() && Royal.IsSailing(item4))
				{
					num5++;
				}
			}
			Check("cease fire lets them go", map.ShipOrders.Count == 0 && num5 == 0, $"{map.ShipOrders.Count} orders, {num5} still on the task");
			map.Close();
		}

		private static Vector2 P(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static bool Liquid(int x, int y)
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

		private static bool Unclaimed(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			try
			{
				return tile != null && (tile.zone == null || tile.zone.city == null);
			}
			catch
			{
				return false;
			}
		}

		private static WorldTile OpenSea(Vector2 from, float min, float max, int open, bool straight = false, bool unclaimed = false)
		{
			for (float num = Mathf.Max(0f, min); num <= max; num += 1f)
			{
				for (int i = 0; i < 32; i++)
				{
					float f = (float)i * MathF.PI / 16f;
					int num2 = Mathf.FloorToInt(from.x + Mathf.Cos(f) * num);
					int num3 = Mathf.FloorToInt(from.y + Mathf.Sin(f) * num);
					bool flag = true;
					for (int j = -open; j <= open && flag; j++)
					{
						for (int k = -open; k <= open && flag; k++)
						{
							flag = Liquid(num2 + k, num3 + j) && (!unclaimed || Unclaimed(num2 + k, num3 + j));
						}
					}
					float num4 = 0f;
					while (flag && straight && num4 <= num)
					{
						flag = Liquid(Mathf.FloorToInt(from.x + Mathf.Cos(f) * num4), Mathf.FloorToInt(from.y + Mathf.Sin(f) * num4));
						num4 += 0.5f;
					}
					if (flag)
					{
						return World.world.GetTile(num2, num3);
					}
					if (num == 0f)
					{
						break;
					}
				}
			}
			return null;
		}

		private static WorldTile Shore(Vector2 from, float min, float max)
		{
			for (float num = min; num <= max; num += 1f)
			{
				for (int i = 0; i < 32; i++)
				{
					float f = (float)i * MathF.PI / 16f;
					WorldTile tile = World.world.GetTile(Mathf.FloorToInt(from.x + Mathf.Cos(f) * num), Mathf.FloorToInt(from.y + Mathf.Sin(f) * num));
					if (!Royal.Standable(tile))
					{
						continue;
					}
					try
					{
						if (tile.zone != null && tile.zone.city != null)
						{
							continue;
						}
					}
					catch
					{
					}
					return tile;
				}
			}
			return null;
		}

		private static Vector2 TownClick(City c)
		{
			try
			{
				foreach (Building building in c.buildings)
				{
					if (building != null && building.isAlive() && building.current_tile != null)
					{
						return P(building.current_tile);
					}
				}
			}
			catch
			{
			}
			return Royal.Position(c);
		}

		private static Actor Ship(string id, WorldTile at, Kingdom k, City city)
		{
			try
			{
				if (!AssetManager.actor_library.has(id))
				{
					id = "boat_transport_human";
				}
				Actor actor = World.world.units.createNewUnit(id, at);
				if (actor == null)
				{
					return null;
				}
				actor.joinKingdom(k);
				if (city != null)
				{
					actor.joinCity(city);
				}
				return actor;
			}
			catch (Exception ex)
			{
				Log.Error("FLEET ship: " + ex.Message);
				return null;
			}
		}

		private static bool AllNear(List<Actor> ships, Vector2 p, float r)
		{
			foreach (Actor ship in ships)
			{
				if (ship.isAlive() && (ship.current_position - p).magnitude > r)
				{
					return false;
				}
			}
			return true;
		}

		private static string Dist(List<Actor> ships, Vector2 p)
		{
			List<string> list = new List<string>();
			foreach (Actor ship in ships)
			{
				list.Add(ship.isAlive() ? (ship.current_position - p).magnitude.ToString("F1") : "sunk");
			}
			return string.Join(", ", list) + " tiles";
		}

		private static int Shots(WarMap map, List<Actor> ships)
		{
			int num = 0;
			foreach (Actor ship in ships)
			{
				num += map.OrderOf(ship)?.Shots ?? 0;
			}
			return num;
		}

		private static float Health(Actor a)
		{
			try
			{
				return a.getHealth();
			}
			catch
			{
				return 0f;
			}
		}

		private static float TownHealth(City c)
		{
			float num = 0f;
			try
			{
				if (c != null && c.isAlive() && c.buildings != null)
				{
					foreach (Building building in c.buildings)
					{
						if (building != null && building.isAlive())
						{
							num += (float)building.getHealth();
						}
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static int Count(City c)
		{
			try
			{
				return (c != null && c.isAlive()) ? c.countUnits() : 0;
			}
			catch
			{
				return 0;
			}
		}

		private static int Buildings(City c)
		{
			try
			{
				return (c != null && c.isAlive() && c.buildings != null) ? c.buildings.Count : 0;
			}
			catch
			{
				return 0;
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

		private IEnumerator Click(int button, Vector2 at, bool ctrl)
		{
			EventModifiers modifiers = (ctrl ? EventModifiers.Control : EventModifiers.None);
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = button,
				mousePosition = at,
				clickCount = 1,
				modifiers = modifiers
			});
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = button,
				mousePosition = at,
				clickCount = 1,
				modifiers = modifiers
			});
			while (_events.Count > 0)
			{
				yield return null;
			}
			yield return null;
			yield return null;
			Note(string.Format("{0}click {1} at {2}: \"{3}\"", ctrl ? "ctrl+" : "", button, at, Mod.ToastText));
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
				Log.Error("FLEET events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}
	}
}
