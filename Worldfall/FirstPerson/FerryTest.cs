using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class FerryTest : CivilTest
	{
		private struct Started
		{
			public float Real;

			public double Game;
		}

		private readonly Queue<Event> _events = new Queue<Event>();

		private readonly Queue<EventType> _meant = new Queue<EventType>();

		private readonly Queue<Vector2?> _aimAt = new Queue<Vector2?>();

		private Actor _ship;

		private float _afloatAt;

		private Vector2? _clickWorld;

		private bool _lost;

		protected override string Prefix => "FERRY";

		private void Update()
		{
			if (_ship == null || Time.realtimeSinceStartup - _afloatAt < 1f)
			{
				return;
			}
			_afloatAt = Time.realtimeSinceStartup;
			try
			{
				if (_ship.isAlive())
				{
					_ship.data.set("existence_check", (int)World.world.getCurWorldTime());
				}
			}
			catch
			{
			}
		}

		protected override IEnumerator Body()
		{
			Actor king = null;
			WorldTile sea = null;
			WorldTile far = null;
			bool raised = false;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if (LavaNear(item, 14))
				{
					continue;
				}
				WorldTile worldTile = OpenSea(P(item), 6f, 22f, 2);
				if (worldTile == null)
				{
					continue;
				}
				WorldTile worldTile2 = FarLand(item, worldTile, 16f, 75f);
				if (worldTile2 != null)
				{
					king = KingTest.FoundKingdom(item, "human", 14, 0);
					if (king != null)
					{
						sea = worldTile;
						far = worldTile2;
						break;
					}
				}
			}
			if (king == null)
			{
				foreach (WorldTile item2 in KingTest.GoodSites())
				{
					if (LavaNear(item2, 14))
					{
						continue;
					}
					WorldTile worldTile3 = OpenSea(P(item2), 6f, 22f, 2);
					WorldTile worldTile4 = ((worldTile3 != null) ? OpenSea(P(item2), 26f, 45f, 7) : null);
					if (worldTile4 != null)
					{
						king = KingTest.FoundKingdom(item2, "human", 14, 0);
						if (king != null)
						{
							RaiseIsland(worldTile4, 4.6f);
							sea = worldTile3;
							far = worldTile4;
							raised = true;
							break;
						}
					}
				}
				yield return CivilTest.Wait(2f);
			}
			if (!Check("a coastal village with land across the sea", king != null && far != null && sea != null))
			{
				yield break;
			}
			Kingdom home = king.kingdom;
			City town = king.city;
			Note(string.Format("village {0} at {1}, sea {2},{3}, far land {4},{5} ({6:F0} tiles off{7})", town.name, Royal.Position(town), sea.x, sea.y, far.x, far.y, (P(far) - Royal.Position(town)).magnitude, raised ? ", raised here" : ""));
			Check("the far land is other land", !SameIsland(far, Tile(king)), "same island: " + SameIsland(far, Tile(king)));
			yield return Possess(king);
			if (!Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			List<Actor> list = new List<Actor>();
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != king && simple.isAlive() && simple.kingdom == home && simple.isWarrior())
					{
						list.Add(simple);
					}
				}
			}
			catch
			{
			}
			foreach (Actor item3 in list)
			{
				try
				{
					((BaseSimObject)item3).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			try
			{
				((BaseSimObject)king).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			if (!Check("warriors to carry", list.Count >= 6, list.Count + " warriors"))
			{
				yield break;
			}
			List<Actor> bandA = list.GetRange(0, list.Count / 2);
			List<Actor> bandB = list.GetRange(list.Count / 2, list.Count - list.Count / 2);
			_ship = Ship("boat_transport_human", sea, home, town);
			if (!Check("a transport of ours at sea", _ship != null && WarMap.Transport(_ship), (_ship != null) ? WorldBoxMod.Describe(_ship) : "none"))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_ship).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			WarMap map = Mod.WarMap;
			map.ClearFleet();
			map.ClearFlags();
			map.Toggle(king, Mod.Toast);
			yield return CivilTest.Wait(1f);
			if (!Check("the war map opens", map.Open))
			{
				yield break;
			}
			yield return View(map, (_ship.current_position + king.current_position) * 0.5f, 60f);
			Mark(map, bandA);
			yield return Click(1, Screen(map, _ship.current_position));
			if (_lost)
			{
				yield break;
			}
			Lift lift = map.LiftOf(_ship);
			int num = 0;
			foreach (Actor item4 in bandA)
			{
				if (Royal.IsBoarding(item4))
				{
					num++;
				}
			}
			Check("a right-click on her sends the marked aboard", lift != null && lift.Boarding.Count == bandA.Count && num == bandA.Count, string.Format("lift {0}, {1} on the task", (lift != null) ? (lift.Now.ToString() + ", " + lift.Boarding.Count + " boarding") : "none", num));
			Check("the ship is marked after", map.Marked.Contains(_ship) && map.MarkedOwn().Count == 0, map.Marked.Count + " marked");
			yield return Capture("ferry_board.png", 2f);
			Started t0 = Start();
			while (Within(t0, 100f) && WarMap.AboardCount(_ship) < bandA.Count && map.LiftOf(_ship) != null && map.LiftOf(_ship).Boarding.Count > 0)
			{
				yield return CivilTest.Wait(1f);
			}
			int aboard = WarMap.AboardCount(_ship);
			Note($"after {Since(t0):F0} s: {aboard} aboard, ship at {_ship.current_position}, {Favorites.Doing(_ship)}");
			Check("they go aboard", aboard >= Mathf.CeilToInt((float)bandA.Count * 0.75f), aboard + " of " + bandA.Count);
			yield return CivilTest.Wait(1f);
			int num2 = 0;
			foreach (WarMap.Unit unit in map.Units)
			{
				if (bandA.Contains(unit.Actor) && unit.Actor.is_inside_boat)
				{
					num2++;
				}
			}
			Check("those aboard are off the map (her count instead)", num2 == 0, num2 + " still drawn");
			lift = map.LiftOf(_ship);
			Check("she waits with them aboard", lift != null && lift.Now == Lift.Stage.Aboard, (lift != null) ? lift.Now.ToString() : "no lift");
			Vector2 rode = _ship.current_position;
			yield return CivilTest.Wait(10f);
			string text = "";
			try
			{
				text = ((((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_ship.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_ship.ai).task.id : "none");
			}
			catch
			{
			}
			Check("ten seconds on she still rides there with them", WarMap.AboardCount(_ship) == aboard && (_ship.current_position - rode).magnitude < 4f && text == "fp_royal_sail", $"{WarMap.AboardCount(_ship)} aboard, moved {(_ship.current_position - rode).magnitude:F1}, task {text}");
			yield return View(map, (_ship.current_position + P(far)) * 0.5f, Mathf.Max(50f, (_ship.current_position - P(far)).magnitude + 30f));
			map.Marked.Clear();
			map.Marked.Add(_ship);
			yield return Click(1, Screen(map, P(far)));
			if (_lost)
			{
				yield break;
			}
			WarFlag f1 = map.FlagNumbered(1);
			lift = map.LiftOf(_ship);
			Check("a right-click on the far land plants flag 1 and she sails to land them", f1 != null && lift != null && lift.Flag == f1 && lift.Now == Lift.Stage.Landing, "flag " + ((f1 != null) ? (f1.Tile.x + "," + f1.Tile.y) : "none") + ", lift " + ((lift != null) ? (lift.Now.ToString() + " to " + ((lift.Flag != null) ? ("flag " + lift.Flag.Number) : "nothing")) : "none"));
			if (f1 == null)
			{
				yield break;
			}
			yield return Capture("ferry_landing.png", 3f);
			Started t1 = Start();
			while (Within(t1, 120f) && (WarMap.AboardCount(_ship) > 0 || f1.Band.Count < aboard))
			{
				yield return CivilTest.Wait(1f);
			}
			int num3 = 0;
			foreach (Actor item5 in f1.Band)
			{
				if (SameIsland(Tile(item5), far))
				{
					num3++;
				}
			}
			Lift lift2 = map.LiftOf(_ship);
			Note($"after {Since(t1):F0} s: {WarMap.AboardCount(_ship)} aboard, flag 1 band {f1.Band.Count}, {num3} on the far land; ship at {_ship.current_position}, " + Favorites.Doing(_ship) + ((lift2 != null && lift2.Drop != null) ? (", making for " + lift2.Drop.x + "," + lift2.Drop.y) : ""));
			Check("they land and join flag 1 on the far land", WarMap.AboardCount(_ship) == 0 && num3 >= Mathf.CeilToInt((float)aboard * 0.75f), $"{num3} of {aboard}");
			yield return CivilTest.Wait(12f);
			Check("they hold flag 1", Holding(f1) >= Mathf.CeilToInt((float)aboard * 0.75f), Holding(f1) + " about it of " + f1.Band.Count);
			WorldTile far2 = OtherSpot(far, 5f, 9f) ?? far;
			yield return View(map, (P(far2) + king.current_position) * 0.5f, Mathf.Max(60f, (P(far2) - king.current_position).magnitude + 30f));
			Mark(map, bandB);
			bool hadOrders = map.OrderOf(_ship) != null;
			yield return Click(1, Screen(map, P(far2)));
			if (_lost)
			{
				yield break;
			}
			WarFlag f2 = map.FlagNumbered(2) ?? ((far2 == far) ? f1 : null);
			lift = map.LiftOf(_ship);
			Check("warriors sent across the water go by ship", f2 != null && lift != null && lift.Flag == f2 && lift.Boarding.Count == bandB.Count, "flag " + ((f2 != null) ? f2.Number.ToString() : "none") + ", lift " + ((lift != null) ? (lift.Now.ToString() + ", " + lift.Boarding.Count + " boarding") : "none") + ", toast \"" + Mod.ToastText + "\"");
			if (f2 == null)
			{
				yield break;
			}
			yield return Capture("ferry_auto.png", 3f);
			Started t2 = Start();
			float traced = 0f;
			int there = 0;
			while (Within(t2, 200f))
			{
				there = 0;
				foreach (Actor item6 in bandB)
				{
					if (f2.Band.Contains(item6) && SameIsland(Tile(item6), far))
					{
						there++;
					}
				}
				if (there >= bandB.Count || (map.LiftOf(_ship) == null && WarMap.AboardCount(_ship) == 0))
				{
					break;
				}
				if (Since(t2) - traced >= 10f)
				{
					traced = Since(t2);
					Trace(map);
				}
				yield return CivilTest.Wait(1f);
			}
			Note($"after {Since(t2):F0} s: {there} of the second band at flag {f2.Number}, {WarMap.AboardCount(_ship)} aboard, ship {Favorites.Doing(_ship)}");
			Check("the second band is carried over and holds its flag", there >= Mathf.CeilToInt((float)bandB.Count * 0.75f), there + " of " + bandB.Count);
			Actor done1 = _ship;
			yield return Until(() => map.LiftOf(done1) == null && (hadOrders || map.OrderOf(done1) == null), 5f);
			if (hadOrders)
			{
				Check("a ship already under orders rides on under them after the ferry", map.OrderOf(_ship) != null && map.LiftOf(_ship) == null, "order " + (map.OrderOf(_ship) != null) + ", " + Favorites.Doing(_ship));
			}
			else
			{
				Check("a ship the ferry took by itself is let go after the landing", map.OrderOf(_ship) == null && !Royal.IsSailing(_ship) && map.LiftOf(_ship) == null, "order " + (map.OrderOf(_ship) != null) + ", " + Favorites.Doing(_ship));
			}
			if (Options.Contains("short"))
			{
				map.ClearFleet();
				map.ClearFlags();
				map.Close();
			}
			else
			{
				yield return Part("edges", Edges(map, king, home, town, bandA, bandB, f1, f2));
				map.ClearFleet();
				map.ClearFlags();
				map.Close();
			}
		}

		private IEnumerator Edges(WarMap map, Actor king, Kingdom home, City town, List<Actor> bandA, List<Actor> bandB, WarFlag f1, WarFlag f2)
		{
			yield return View(map, (_ship.current_position + f1.P) * 0.5f, Mathf.Max(50f, (_ship.current_position - f1.P).magnitude + 30f));
			Mark(map, bandA);
			yield return Click(1, Screen(map, _ship.current_position));
			if (_lost)
			{
				yield break;
			}
			Started t0 = Start();
			while (Within(t0, 90f) && map.LiftOf(_ship) != null && map.LiftOf(_ship).Boarding.Count > 0)
			{
				yield return CivilTest.Wait(1f);
			}
			int aboard = WarMap.AboardCount(_ship);
			Check("band A goes aboard again from the far land", aboard >= Mathf.CeilToInt((float)bandA.Count * 0.75f) && f1.Band.Count == 0, $"{aboard} aboard, flag 1 band {f1.Band.Count}, flags {map.Flags.Count}");
			int kept;
			int num = map.CeaseFire(new List<Actor> { _ship }, out kept);
			Lift lift = map.LiftOf(_ship);
			Check("cease fire keeps the soldiers aboard and her under orders", num == 1 && kept == 1 && lift != null && lift.Now == Lift.Stage.Aboard && map.OrderOf(_ship) != null, string.Format("ceased {0}, kept {1}, lift {2}, {3}", num, kept, (lift != null) ? lift.Now.ToString() : "none", WarMap.ShipDoing(_ship)));
			Vector2 rode = _ship.current_position;
			yield return CivilTest.Wait(8f);
			string text = "";
			try
			{
				text = ((((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_ship.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_ship.ai).task.id : "none");
			}
			catch
			{
			}
			Check("eight seconds on they're still aboard where she stopped", WarMap.AboardCount(_ship) == aboard && (_ship.current_position - rode).magnitude < 4f && text == "fp_royal_sail", $"{WarMap.AboardCount(_ship)} aboard, moved {(_ship.current_position - rode).magnitude:F1}, task {text}");
			Vector2 vector = Royal.Position(town);
			WorldTile worldTile = WarMap.GroundFor(World.world.GetTile(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y)));
			WorldTile homeLand = ((worldTile != null) ? (OtherSpot(worldTile, 3f, 10f) ?? worldTile) : Tile(king));
			Note("home land for the landing: " + ((homeLand != null) ? (homeLand.x + "," + homeLand.y + " " + ((homeLand.Type != null) ? homeLand.Type.id : "?")) : "none"));
			yield return View(map, (_ship.current_position + P(homeLand)) * 0.5f, Mathf.Max(60f, (_ship.current_position - P(homeLand)).magnitude + 30f));
			map.Marked.Clear();
			map.Marked.Add(_ship);
			yield return Click(1, Screen(map, P(homeLand)));
			if (_lost)
			{
				yield break;
			}
			lift = map.LiftOf(_ship);
			WarFlag f3 = lift?.Flag;
			Check("right-clicked onto home land, she sails to land them", f3 != null && lift.Now == Lift.Stage.Landing, (lift != null) ? (lift.Now.ToString() + ", flag " + ((f3 != null) ? f3.Number.ToString() : "none")) : "no lift");
			yield return CivilTest.Wait(3f);
			if (f3 != null)
			{
				map.PullUp(f3);
			}
			yield return Until(() => map.LiftOf(_ship) != null && map.LiftOf(_ship).Now == Lift.Stage.Aboard, 6f);
			lift = map.LiftOf(_ship);
			Check("the flag pulled up on the way, they wait aboard", lift != null && lift.Now == Lift.Stage.Aboard && WarMap.AboardCount(_ship) == aboard, string.Format("lift {0}, {1} aboard, toast \"{2}\"", (lift != null) ? lift.Now.ToString() : "none", WarMap.AboardCount(_ship), Mod.ToastText));
			yield return View(map, (_ship.current_position + f2.P) * 0.5f, Mathf.Max(50f, (_ship.current_position - f2.P).magnitude + 30f));
			Mark(map, bandB);
			yield return Click(1, Screen(map, _ship.current_position));
			if (_lost)
			{
				yield break;
			}
			lift = map.LiftOf(_ship);
			int num2 = lift?.Boarding.Count ?? 0;
			Check("band B told aboard the loaded ship", lift != null && lift.Now == Lift.Stage.Boarding && num2 > 0, $"{num2} coming, {WarMap.AboardCount(_ship)} aboard");
			yield return CivilTest.Wait(1f);
			WorldTile worldTile2 = OpenSea(_ship.current_position, 6f, 12f, 1);
			if (worldTile2 != null)
			{
				map.Marked.Clear();
				map.Marked.Add(_ship);
				yield return Click(1, Screen(map, P(worldTile2)));
				if (_lost)
				{
					yield break;
				}
				yield return Until(() => map.LiftOf(_ship) != null && map.LiftOf(_ship).Now == Lift.Stage.Aboard, 6f);
				lift = map.LiftOf(_ship);
				int num3 = 0;
				foreach (Actor item in bandB)
				{
					if (Royal.IsBoarding(item))
					{
						num3++;
					}
				}
				Check("sailed off mid-boarding, the rest stay ashore and those aboard stay aboard", lift != null && lift.Now == Lift.Stage.Aboard && lift.Boarding.Count == 0 && num3 == 0 && WarMap.AboardCount(_ship) >= aboard, string.Format("lift {0}, {1} still on the task, {2} aboard", (lift != null) ? (lift.Now.ToString() + ", " + lift.Boarding.Count + " boarding") : "none", num3, WarMap.AboardCount(_ship)));
			}
			else
			{
				Note("(no open water near her: the sail-off check is skipped)");
			}
			int down = WarMap.AboardCount(_ship);
			try
			{
				_ship.finishStatusEffect("invincible");
			}
			catch
			{
			}
			CivilTest.Kill(_ship);
			Actor sunk = _ship;
			yield return Until(() => map.LiftOf(sunk) == null && map.Lifts.Count == 0, 6f);
			int num4 = 0;
			foreach (Actor bandum in bandA)
			{
				try
				{
					if (bandum.isAlive() && bandum.is_inside_boat)
					{
						num4++;
					}
				}
				catch
				{
				}
			}
			foreach (Actor item2 in bandB)
			{
				try
				{
					if (item2.isAlive() && item2.is_inside_boat)
					{
						num4++;
					}
				}
				catch
				{
				}
			}
			Check("a ship sunk with soldiers aboard ends her lift and leaves nobody inside her", map.Lifts.Count == 0 && map.LiftOf(_ship) == null && num4 == 0, $"{down} were aboard, {num4} still inside, lifts {map.Lifts.Count}, toast \"{Mod.ToastText}\"");
			_ship = null;
			WorldTile worldTile3 = OpenSea(Royal.Position(town), 6f, 24f, 2);
			Actor ship2 = ((worldTile3 != null) ? Ship("boat_transport_human", worldTile3, home, town) : null);
			if (ship2 == null)
			{
				Note("(no water for a second ship: the seat check is skipped)");
				yield break;
			}
			_ship = ship2;
			List<Actor> crowd = new List<Actor>();
			for (int num5 = 0; num5 < 55; num5++)
			{
				Actor actor = CivilTest.SpawnNear("human", Royal.Position(town) + new Vector2(num5 % 8 - 4, num5 / 8 - 3));
				if (actor == null)
				{
					continue;
				}
				try
				{
					actor.joinKingdom(home);
					if (actor.city != town)
					{
						actor.joinCity(town);
					}
				}
				catch
				{
				}
				crowd.Add(actor);
			}
			yield return CivilTest.Wait(1f);
			yield return View(map, (ship2.current_position + Royal.Position(town)) * 0.5f, 60f);
			Mark(map, crowd);
			yield return Click(1, Screen(map, ship2.current_position));
			if (_lost)
			{
				yield break;
			}
			lift = map.LiftOf(ship2);
			int num6 = 0;
			foreach (Actor item3 in crowd)
			{
				if (item3.isAlive() && item3.kingdom == home)
				{
					num6++;
				}
			}
			int num7 = ((lift != null) ? (lift.Boarding.Count + WarMap.AboardCount(ship2)) : 0);
			Check("fifty seats: fifty go aboard, the rest find no room", num7 == 50 && num6 > 50 && Mod.ToastText.Contains("no room"), string.Format("{0} of ours marked, lift {1}, toast \"{2}\"", num6, (lift != null) ? (lift.Boarding.Count + " boarding, " + WarMap.AboardCount(ship2) + " aboard") : "none", Mod.ToastText));
			map.CeaseFire(new List<Actor> { ship2 });
			yield return CivilTest.Wait(0.5f);
			int num8 = 0;
			foreach (Actor item4 in crowd)
			{
				if (Royal.IsBoarding(item4))
				{
					num8++;
				}
			}
			Check("cease fire lets all those still coming go", num8 == 0 && (map.LiftOf(ship2) == null || map.LiftOf(ship2).Boarding.Count == 0), string.Format("{0} still going aboard, lift {1}, {2} aboard", num8, (map.LiftOf(ship2) != null) ? map.LiftOf(ship2).Now.ToString() : "none", WarMap.AboardCount(ship2)));
			foreach (Actor item5 in crowd)
			{
				CivilTest.Kill(item5);
			}
		}

		private static Started Start()
		{
			double game = 0.0;
			try
			{
				game = World.world.getCurWorldTime();
			}
			catch
			{
			}
			return new Started
			{
				Real = Time.realtimeSinceStartup,
				Game = game
			};
		}

		private static bool Within(Started t, float seconds)
		{
			double num = t.Game;
			try
			{
				num = World.world.getCurWorldTime();
			}
			catch
			{
			}
			if (num - t.Game < (double)seconds)
			{
				return Time.realtimeSinceStartup - t.Real < seconds * 5f;
			}
			return false;
		}

		private static float Since(Started t)
		{
			return Time.realtimeSinceStartup - t.Real;
		}

		private void Trace(WarMap map)
		{
			Lift lift = map.LiftOf(_ship);
			string text = "";
			try
			{
				text = ((((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_ship.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_ship.ai).task.id : "none");
			}
			catch
			{
			}
			WorldTile worldTile = ((lift == null) ? null : ((lift.Now == Lift.Stage.Boarding) ? lift.Pickup : lift.Drop));
			string text2 = "";
			try
			{
				text2 = ((_ship.beh_tile_target != null) ? (_ship.beh_tile_target.x + "," + _ship.beh_tile_target.y) : "none");
			}
			catch
			{
			}
			Note(string.Format("ship at {0} moving {1}, task {2} to {3}; lift {4}", _ship.current_position, _ship._is_moving, text, text2, (lift != null) ? (lift.Now.ToString() + ", " + lift.Boarding.Count + " coming, " + lift.Aboard + " aboard") : "none") + ((worldTile != null) ? $", for {worldTile.x},{worldTile.y} ({(_ship.current_position - new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f)).magnitude:F1} off)" : ""));
		}

		private static IEnumerator Until(Func<bool> done, float seconds)
		{
			Started t0 = Start();
			while (Since(t0) < seconds)
			{
				bool flag = false;
				try
				{
					flag = done();
				}
				catch
				{
				}
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
		}

		private static Vector2 P(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static WorldTile Tile(Actor a)
		{
			try
			{
				return World.world.GetTile(Mathf.FloorToInt(a.current_position.x), Mathf.FloorToInt(a.current_position.y));
			}
			catch
			{
				return null;
			}
		}

		private static bool SameIsland(WorldTile a, WorldTile b)
		{
			try
			{
				return a != null && b != null && a.isSameIsland(b);
			}
			catch
			{
				return false;
			}
		}

		private static bool Liquid(WorldTile t)
		{
			try
			{
				return t != null && t.is_liquid && t.Type != null && !t.Type.lava;
			}
			catch
			{
				return false;
			}
		}

		private static WorldTile OpenSea(Vector2 from, float min, float max, int open)
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
							flag = Liquid(World.world.GetTile(num2 + k, num3 + j));
						}
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

		private static WorldTile FarLand(WorldTile home, WorldTile sea, float min, float max)
		{
			HashSet<WorldTile> hashSet = new HashSet<WorldTile> { sea };
			Queue<WorldTile> queue = new Queue<WorldTile>();
			queue.Enqueue(sea);
			WorldTile result = null;
			float num = float.MaxValue;
			Vector2 vector = P(home);
			int num2 = 0;
			while (queue.Count > 0 && num2++ < 60000)
			{
				WorldTile[] neighboursAll = queue.Dequeue().neighboursAll;
				foreach (WorldTile worldTile in neighboursAll)
				{
					if (worldTile == null || !hashSet.Add(worldTile))
					{
						continue;
					}
					float magnitude = (P(worldTile) - vector).magnitude;
					if (Liquid(worldTile))
					{
						if (magnitude <= max + 12f)
						{
							queue.Enqueue(worldTile);
						}
					}
					else if (!(magnitude < min) && !(magnitude > max) && !(magnitude >= num) && Royal.Standable(worldTile) && !SameIsland(worldTile, home) && Room(worldTile) >= 16 && !LavaNear(worldTile, 12))
					{
						result = worldTile;
						num = magnitude;
					}
				}
			}
			return result;
		}

		private static bool LavaNear(WorldTile t, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					WorldTile tile = World.world.GetTile(t.x + j, t.y + i);
					try
					{
						if (tile != null && ((tile.Type != null && tile.Type.lava) || tile.isOnFire()))
						{
							return true;
						}
					}
					catch
					{
					}
				}
			}
			return false;
		}

		private static int Room(WorldTile t)
		{
			int num = 0;
			for (int i = -3; i <= 3; i++)
			{
				for (int j = -3; j <= 3; j++)
				{
					WorldTile tile = World.world.GetTile(t.x + j, t.y + i);
					if (Royal.Standable(tile) && SameIsland(tile, t))
					{
						num++;
					}
				}
			}
			return num;
		}

		private static WorldTile OtherSpot(WorldTile t, float min, float max)
		{
			for (float num = min; num <= max; num += 1f)
			{
				for (int i = 0; i <= 12; i++)
				{
					float f = MathF.PI + (float)i * MathF.PI / 12f;
					WorldTile tile = World.world.GetTile(Mathf.FloorToInt((float)t.x + 0.5f + Mathf.Cos(f) * num), Mathf.FloorToInt((float)t.y + 0.5f + Mathf.Sin(f) * num));
					if (Royal.Standable(tile) && SameIsland(tile, t) && Room(tile) >= 14)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static void RaiseIsland(WorldTile c, float r)
		{
			TileType tileType = AssetManager.tiles.get("sand");
			if (tileType == null)
			{
				return;
			}
			int num = Mathf.CeilToInt(r);
			for (int i = -num; i <= num; i++)
			{
				for (int j = -num; j <= num; j++)
				{
					if (!((float)(j * j + i * i) > r * r))
					{
						WorldTile tile = World.world.GetTile(c.x + j, c.y + i);
						try
						{
							tile?.setTileType(tileType);
						}
						catch (Exception ex)
						{
							Log.Error("FERRY island: " + ex.Message);
							return;
						}
					}
				}
			}
		}

		private static Actor Ship(string id, WorldTile at, Kingdom k, City city)
		{
			try
			{
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
				Log.Error("FERRY ship: " + ex.Message);
				return null;
			}
		}

		private static int Holding(WarFlag f)
		{
			int num = 0;
			float num2 = WarMap.Spread(f.Band.Count) + 3f;
			foreach (Actor item in f.Band)
			{
				try
				{
					if (item.isAlive() && (item.current_position - f.P).magnitude <= num2)
					{
						num++;
					}
				}
				catch
				{
				}
			}
			return num;
		}

		private static void Mark(WarMap map, List<Actor> who)
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

		private Vector2 Screen(WarMap map, Vector2 world)
		{
			_clickWorld = world;
			return WorldBoxMod.Instance.HudForTest.WarMapToScreenForTest?.Invoke(world) ?? Vector2.zero;
		}

		private IEnumerator Click(int button, Vector2 at)
		{
			if (_lost || Mod.Host == null || !Mod.IsFirstPerson)
			{
				if (!_lost)
				{
					Check("still the king in first person", ok: false, "first person ended: the test stops here");
				}
				_lost = true;
				yield break;
			}
			WarMap map = Mod.WarMap;
			if (!map.Open && Mod.Host != null)
			{
				Note("the war map had been shut: opened again for the click");
				map.Toggle(Mod.Host, Mod.Toast);
				yield return null;
				yield return null;
				yield return CivilTest.Wait(0.3f);
				at = (_clickWorld.HasValue ? Screen(map, _clickWorld.Value) : at);
			}
			Vector2? clickWorld = _clickWorld;
			_clickWorld = null;
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = button,
				mousePosition = at,
				clickCount = 1
			});
			_meant.Enqueue(EventType.MouseDown);
			_aimAt.Enqueue(clickWorld);
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = button,
				mousePosition = at,
				clickCount = 1
			});
			_meant.Enqueue(EventType.MouseUp);
			_aimAt.Enqueue(clickWorld);
			float t0 = Time.realtimeSinceStartup;
			while (_events.Count > 0 && Time.realtimeSinceStartup - t0 < 8f)
			{
				yield return null;
			}
			if (_events.Count > 0)
			{
				Note($"the click was never taken (map open {map.Open}): dropped");
				_events.Clear();
				_meant.Clear();
				_aimAt.Clear();
			}
			yield return null;
			yield return null;
			Note($"click {button} at {at}: \"{Mod.ToastText}\"");
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
					Event obj = _events.Dequeue();
					EventType eventType = ((_meant.Count > 0) ? _meant.Dequeue() : obj.type);
					Vector2? vector = ((_aimAt.Count > 0) ? _aimAt.Dequeue() : ((Vector2?)null));
					if (vector.HasValue)
					{
						Event.current = new Event
						{
							type = EventType.Layout,
							mousePosition = obj.mousePosition
						};
						Mod.HudForTest.DrawWarMap();
						Func<Vector2, Vector2> warMapToScreenForTest = Mod.HudForTest.WarMapToScreenForTest;
						if (warMapToScreenForTest != null)
						{
							obj.mousePosition = warMapToScreenForTest(vector.Value);
						}
					}
					Vector2 mousePosition = obj.mousePosition;
					EventType type = obj.type;
					Event.current = obj;
					if (Event.current.type != eventType)
					{
						Event.current.type = eventType;
					}
					bool flag = Event.current == obj;
					Vector2 mousePosition2 = Event.current.mousePosition;
					EventType type2 = Event.current.type;
					Matrix4x4 matrix = GUI.matrix;
					Mod.HudForTest.DrawWarMap();
					if (type != eventType || obj.type != EventType.Used)
					{
						Note(string.Format("event {0} (read {1}, now {2}, focused {3}) at {4}: current {5}, seen at {6}, matrix {7:F2},{8:F2} +{9:F0},{10:F0}, ", eventType, type, type2, Application.isFocused, mousePosition, flag ? "set" : "NOT set", mousePosition2, matrix.m00, matrix.m11, matrix.m03, matrix.m13) + $"after {obj.type}, map rect {Mod.HudForTest.WarMapRectForTest}, open {Mod.WarMap.Open}, screen {UnityEngine.Screen.width}x{UnityEngine.Screen.height}");
					}
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
				Log.Error("FERRY events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}
	}
}
