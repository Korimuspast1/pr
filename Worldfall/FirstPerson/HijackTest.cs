using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class HijackTest : CivilTest
	{
		private Actor _king;

		private Actor _theirKing;

		private Kingdom _ours;

		private Kingdom _theirs;

		private WorldTile _sea;

		private readonly List<Actor> _ships = new List<Actor>();

		private readonly Dictionary<Actor, Vector2> _pinned = new Dictionary<Actor, Vector2>();

		private float _afloatAt;

		protected override string Prefix => "HIJACK";

		private void Update()
		{
			foreach (KeyValuePair<Actor, Vector2> item in _pinned)
			{
				Actor key = item.Key;
				try
				{
					if (key != null && key.isAlive() && !ControllableUnit.isControllingUnit(key))
					{
						key.cancelAllBeh();
						key.current_position = item.Value;
						key.dirty_current_tile = true;
					}
				}
				catch
				{
				}
			}
			if (Time.realtimeSinceStartup - _afloatAt < 1f)
			{
				return;
			}
			_afloatAt = Time.realtimeSinceStartup;
			foreach (Actor ship in _ships)
			{
				try
				{
					if (ship != null && ship.isAlive())
					{
						ship.data.set("existence_check", (int)World.world.getCurWorldTime());
					}
				}
				catch
				{
				}
			}
		}

		protected override IEnumerator Body()
		{
			foreach (WorldTile item in KingTest.GoodSites())
			{
				WorldTile worldTile = OpenSea(P(item), 5f, 24f, 2);
				if (worldTile != null)
				{
					_king = KingTest.FoundKingdom(item, "human", 4, 0);
					if (_king != null)
					{
						_sea = worldTile;
						break;
					}
				}
			}
			if (!Check("a village of ours by the sea", _king != null && _sea != null))
			{
				yield break;
			}
			_ours = _king.kingdom;
			_theirKing = Village("human", 4, 0, Tile(_king), 25f) ?? Village("human", 4, 0);
			_theirs = ((_theirKing != null) ? _theirKing.kingdom : null);
			if (!Check("another realm", _theirs != null && _theirs != _ours))
			{
				yield break;
			}
			bool flag = false;
			try
			{
				flag = _ours.isEnemy(_theirs);
			}
			catch
			{
			}
			Check("the two realms are at peace", !flag);
			yield return Possess(_king);
			if (Check("the king in first person", InControl(_king)))
			{
				try
				{
					((BaseSimObject)_king).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Part("ashore", Ashore());
				yield return OffAnyShip();
				yield return Part("at sea", AtSea());
				yield return OffAnyShip();
				yield return Part("sunk", Sunk());
				yield return OffAnyShip();
				yield return Part("leap away", LeapAway());
				yield return OffAnyShip();
				yield return Part("our own", OurOwn());
				yield return OffAnyShip();
				yield return Part("beasts", Beasts());
				yield return Part("war map keys", MapKeys());
			}
		}

		private IEnumerator Ashore()
		{
			Vector2 stand;
			Actor ship = ShipBeside(_theirs, _theirKing.city, out stand);
			if (!Check("their transport by the shore", ship != null))
			{
				yield break;
			}
			List<Actor> riders = Riders(ship, 2, _theirKing.city, stand);
			Actor actor = CivilTest.SpawnNear("human", stand, 1.5f);
			try
			{
				actor.joinCity(_theirKing.city);
				actor.makeWait(30f);
			}
			catch
			{
			}
			yield return StandBy(ship, stand);
			Note($"by her: {Boats.HullDistance(ship, _king.current_position):F2} from her hull, picked {Mod.Picked == ship}, prompt '{Mod.Prompt}'");
			if (Mod.Picked == ship)
			{
				Check("Shift+E is offered by her side", Mod.Prompt != null && Mod.Prompt.Contains("Shift+E:Hijack"), Mod.Prompt);
			}
			Check("she can be taken from here", Hijack.CanTake(_king, ship, _king.current_position));
			int before = Law.BountyOn(_theirs, _king);
			_pinned.Remove(ship);
			Mod.TakeShipForTest(ship);
			yield return CivilTest.Wait(1.5f);
			Boat hold = ship.getSimpleComponent<Boat>();
			Check("you steer her", InControl(ship) && BoatHelm.Steers(ship), "host " + WorldBoxMod.Describe(Mod.Host));
			Check("your body is aboard her", _king.isAlive() && _king.is_inside_boat && Mod.Body == _king && Mod.Hijacking);
			Check("she is our realm's now", ship.kingdom == _ours, (ship.kingdom != null) ? ship.kingdom.name : "none");
			int num = Law.BountyOn(_theirs, _king);
			Check("taking her was seen: wanted in their realm", num >= before + 80, $"bounty {before} -> {num}, witnesses {Law.LastWitnesses}");
			Check("her two riders ride along", hold != null && hold.countPassengers() == 2, (hold != null) ? (hold.countPassengers() + " aboard") : "no hold");
			Check("the prompt offers getting off", Mod.Prompt != null && Mod.Prompt.Contains("Shift+E:"), Mod.Prompt);
			Check("the hull bar follows her", Mod.HijackedShip == ship);
			Note($"toast '{Mod.ToastText}', home dock {ship.getHomeBuilding() != null}");
			Mod.SaveFrame("hijack_helm.png");
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "hijack_helm_hud.png"));
			yield return CivilTest.Wait(0.5f);
			Mod.LeaveShipForTest();
			yield return CivilTest.Wait(1.5f);
			Check("back in your own eyes", InControl(_king) && !Hijack.Active, "host " + WorldBoxMod.Describe(Mod.Host));
			Check("ashore, not in her", !_king.is_inside_boat && !Liquid(Tile(_king)), $"at {_king.current_position}");
			Check("she's no longer steered", !ControllableUnit.isControllingUnit(ship) && BoatHelm.Boat == null);
			int num2 = 0;
			foreach (Actor item in riders)
			{
				try
				{
					if (item.isAlive() && !item.is_inside_boat)
					{
						num2++;
					}
				}
				catch
				{
				}
			}
			Check("her riders came ashore with you", num2 == riders.Count && hold.countPassengers() == 0, $"{num2} of {riders.Count} ashore, {hold.countPassengers()} aboard");
			Check("she stays ours after", ship.kingdom == _ours);
			Note("toast '" + Mod.ToastText + "'");
			Mod.SaveFrame("hijack_ashore.png");
			_pinned.Remove(ship);
		}

		private IEnumerator AtSea()
		{
			Vector2 stand;
			Actor ship = ShipBeside(_theirs, _theirKing.city, out stand, (Vector2 p) => WayOut(p).Count > 0);
			if (!Check("another of theirs by the shore", ship != null))
			{
				yield break;
			}
			List<Actor> riders = Riders(ship, 1, _theirKing.city, stand);
			yield return StandBy(ship, stand);
			_pinned.Remove(ship);
			Mod.TakeShipForTest(ship);
			yield return CivilTest.Wait(1.5f);
			if (!Check("taken", InControl(ship) && Mod.Hijacking))
			{
				yield break;
			}
			Vector2 from = ship.current_position;
			List<Vector2> way = WayOut(from);
			Note(string.Format("a way out over {0} tiles of water to {1}", way.Count, (way.Count > 0) ? way[way.Count - 1].ToString() : "nowhere"));
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 30f && way.Count > 0 && (Boats.CanGoAshore(ship) || (ship.current_position - from).magnitude < 5f))
			{
				Vector2 vector = Ahead(way, ship.current_position, 3f);
				BoatHelm.Heading = Mathf.Atan2(vector.y - ship.current_position.y, vector.x - ship.current_position.x);
				yield return CivilTest.Wait(0.25f);
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			float magnitude = (ship.current_position - from).magnitude;
			Check("she sails where you steer her", magnitude > 2f, $"{magnitude:F1} tiles in {Time.realtimeSinceStartup - t0:F0} s, way {BoatHelm.Way:F2}");
			Check("out of reach of a shore", !Boats.CanGoAshore(ship));
			Mod.SaveFrame("hijack_sailing.png");
			Mod.LeaveShipForTest();
			yield return CivilTest.Wait(1.5f);
			Boat hold = ship.getSimpleComponent<Boat>();
			Check("over the side, still you", InControl(_king) && !_king.is_inside_boat && !Hijack.Active);
			Check("in the sea", Liquid(Tile(_king)) || Mod.Swimming, $"at {_king.current_position}, swimming {Mod.Swimming}");
			string text = "";
			try
			{
				text = ((((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)ship.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)ship.ai).task.id : "none");
			}
			catch
			{
			}
			Check("her rider stays aboard, she takes them home", hold.countPassengers() == riders.Count && text == "boat_transport_go_unload", string.Format("{0} aboard, task {1}, to {2}", hold.countPassengers(), text, (hold.taxi_target != null) ? (hold.taxi_target.x + "," + hold.taxi_target.y) : "nowhere"));
			Note("toast '" + Mod.ToastText + "'");
			Mod.SaveFrame("hijack_overboard.png");
			float t1 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t1 < 60f && hold.countPassengers() > 0 && ship.isAlive())
			{
				yield return CivilTest.Wait(1f);
			}
			int num = 0;
			foreach (Actor item in riders)
			{
				try
				{
					if (item.isAlive() && !item.is_inside_boat)
					{
						num++;
					}
				}
				catch
				{
				}
			}
			Check("she lets her rider off at home", num == riders.Count, $"{num} of {riders.Count} landed after {Time.realtimeSinceStartup - t1:F0} s, she is at {ship.current_position}");
			Put(_king, StandOnLand(P(Tile(_theirKing))) ?? P(Tile(_theirKing)));
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator Sunk()
		{
			Vector2 stand;
			Actor ship = ShipBeside(_theirs, _theirKing.city, out stand);
			if (!Check("a third by the shore", ship != null))
			{
				yield break;
			}
			yield return StandBy(ship, stand);
			_pinned.Remove(ship);
			Mod.TakeShipForTest(ship);
			yield return CivilTest.Wait(1.5f);
			if (Check("taken", InControl(ship) && Mod.Hijacking))
			{
				try
				{
					ship.finishStatusEffect("invincible");
					((BaseSimObject)ship).getHitFullHealth(AttackType.Divine);
				}
				catch (Exception ex)
				{
					Log.Error("HIJACK sinking: " + ex.Message);
				}
				yield return CivilTest.Wait(1.5f);
				Check("she sinks, you're in the water, still you", _king.isAlive() && InControl(_king) && !_king.is_inside_boat && Mod.DeathTimer < 0f, $"alive {_king.isAlive()}, host {WorldBoxMod.Describe(Mod.Host)}, aboard {_king.is_inside_boat}, death {Mod.DeathTimer:F1}");
				Check("nothing left of the hijack", !Hijack.Active && BoatHelm.Boat == null);
				Note("toast '" + Mod.ToastText + "'");
			}
		}

		private IEnumerator LeapAway()
		{
			Put(_king, StandOnLand(_king.current_position) ?? _king.current_position);
			Vector2 stand;
			Actor ship = ShipBeside(_theirs, _theirKing.city, out stand);
			if (Check("a fourth by the shore", ship != null))
			{
				Actor other = CivilTest.SpawnNear("human", stand, 2f);
				try
				{
					other.joinCity(_king.city);
					other.makeWait(30f);
				}
				catch
				{
				}
				yield return StandBy(ship, stand);
				_pinned.Remove(ship);
				Mod.TakeShipForTest(ship);
				yield return CivilTest.Wait(1.5f);
				if (Check("taken", InControl(ship) && Mod.Hijacking))
				{
					yield return Possess(other);
					Check("leapt into someone else", InControl(other));
					Check("your old body was put off her", _king.isAlive() && !_king.is_inside_boat && !Hijack.Active, $"at {_king.current_position}");
					Check("she's let go", !ControllableUnit.isControllingUnit(ship));
					yield return Possess(_king);
					Check("back in the king", InControl(_king));
					_pinned.Remove(ship);
				}
			}
		}

		private IEnumerator OurOwn()
		{
			Put(_king, StandOnLand(_king.current_position) ?? _king.current_position);
			Vector2 stand;
			Actor ship = ShipBeside(_ours, _king.city, out stand);
			if (Check("one of our own by the shore", ship != null))
			{
				yield return StandBy(ship, stand);
				int before = Law.BountyOn(_ours, _king);
				string crime = Law.LastCrime;
				Law.LastCrime = null;
				_pinned.Remove(ship);
				Mod.TakeShipForTest(ship);
				yield return CivilTest.Wait(1.5f);
				Check("taken", InControl(ship) && Mod.Hijacking);
				Check("no crime to take your own realm's", Law.LastCrime == null && Law.BountyOn(_ours, _king) == before, "crime " + (Law.LastCrime ?? "none"));
				Check("she stays ours", ship.kingdom == _ours);
				Note("toast '" + Mod.ToastText + "'");
				Law.LastCrime = crime;
				Mod.LeaveShipForTest();
				yield return CivilTest.Wait(1.5f);
				Check("off her again", InControl(_king) && !_king.is_inside_boat);
				_pinned.Remove(ship);
			}
		}

		private IEnumerator Beasts()
		{
			Vector2 stand;
			Actor actor = ShipBeside(_theirs, _theirKing.city, out stand);
			if (Check("a ship for the sheep", actor != null))
			{
				Actor actor2 = CivilTest.Spawn("sheep", stand);
				if (Check("a sheep by her", actor2 != null))
				{
					actor2.current_position = stand;
					Check("a sheep can't take her", !Hijack.CanTake(actor2, actor, stand));
					Check("a person standing there could", Hijack.CanTake(_king, actor, stand));
					CivilTest.Kill(actor2);
					yield return CivilTest.Wait(0.2f);
				}
			}
		}

		private IEnumerator MapKeys()
		{
			Put(_king, StandOnLand(_king.current_position) ?? _king.current_position);
			yield return CivilTest.Wait(0.5f);
			Check("the keys walk with no window open", PossessionHooks.KeysTaken != null && !PossessionHooks.KeysTaken());
			WarMap map = Mod.WarMap;
			map.Toggle(_king, Mod.Toast);
			yield return CivilTest.Wait(0.6f);
			Check("the war map opens", map.Open);
			Check("with the war map open WASD is the map's, the body stands", PossessionHooks.KeysTaken != null && PossessionHooks.KeysTaken());
			Vector2 at = _king.current_position;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return CivilTest.Wait(1f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			Note($"scripted walk with the map open: {(_king.current_position - at).magnitude:F2} tiles");
			map.Toggle(_king, Mod.Toast);
			yield return CivilTest.Wait(0.6f);
			Check("closed again, the keys walk", !map.Open && !PossessionHooks.KeysTaken());
		}

		private Actor ShipBeside(Kingdom k, City city, out Vector2 stand, Func<Vector2, bool> wanted = null)
		{
			stand = Vector2.zero;
			Vector2 current_position = _king.current_position;
			HashSet<int> hashSet = new HashSet<int>();
			for (float num = 2f; num < 40f; num += 1f)
			{
				for (int i = 0; i < 32; i++)
				{
					float f = (float)i * MathF.PI / 16f;
					int num2 = Mathf.FloorToInt(current_position.x + Mathf.Cos(f) * num);
					int num3 = Mathf.FloorToInt(current_position.y + Mathf.Sin(f) * num);
					if (!hashSet.Add(num2 * 4096 + num3) || !OpenAround(num2, num3, 1) || !LandWithin(num2, num3, 2) || (wanted != null && !wanted(new Vector2((float)num2 + 0.5f, (float)num3 + 0.5f))) || Busy(new Vector2((float)num2 + 0.5f, (float)num3 + 0.5f)))
					{
						continue;
					}
					Actor actor = NewShip("boat_transport_human", World.world.GetTile(num2, num3), k, city);
					if (actor == null)
					{
						continue;
					}
					Vector2 current_position2 = actor.current_position;
					Vector2? vector = null;
					float num4 = float.MaxValue;
					for (float num5 = -3f; num5 <= 3f; num5 += 0.25f)
					{
						for (float num6 = -3f; num6 <= 3f; num6 += 0.25f)
						{
							Vector2 vector2 = new Vector2(current_position2.x + num6, current_position2.y + num5);
							if (Dry(World.world.GetTile((int)vector2.x, (int)vector2.y)))
							{
								float num7 = Boats.HullDistance(actor, vector2);
								if (!(num7 < 0.25f) && !(num7 > 1.1f) && num7 < num4)
								{
									num4 = num7;
									vector = vector2;
								}
							}
						}
					}
					if (!vector.HasValue)
					{
						CivilTest.Kill(actor);
						continue;
					}
					stand = vector.Value;
					_ships.Add(actor);
					_pinned[actor] = current_position2;
					Note($"{WorldBoxMod.Describe(actor)} of {k.name} at {current_position2}, stand {stand} ({num4:F2} from her hull)");
					return actor;
				}
			}
			return null;
		}

		private static List<Vector2> WayOut(Vector2 from)
		{
			List<Vector2> list = new List<Vector2>();
			WorldTile tile = World.world.GetTile((int)from.x, (int)from.y);
			if (tile == null)
			{
				return list;
			}
			Dictionary<WorldTile, WorldTile> dictionary = new Dictionary<WorldTile, WorldTile> { { tile, null } };
			Queue<WorldTile> queue = new Queue<WorldTile>();
			queue.Enqueue(tile);
			WorldTile worldTile = null;
			while (queue.Count > 0 && dictionary.Count < 6000)
			{
				WorldTile worldTile2 = queue.Dequeue();
				if ((P(worldTile2) - from).magnitude >= 8f && OpenAround(worldTile2.x, worldTile2.y, 6))
				{
					worldTile = worldTile2;
					break;
				}
				for (int i = 0; i < 4; i++)
				{
					MapBox world = World.world;
					int x = worldTile2.x;
					int num;
					switch (i)
					{
					default:
						num = 0;
						break;
					case 1:
						num = -1;
						break;
					case 0:
						num = 1;
						break;
					}
					int pX = x + num;
					int y = worldTile2.y;
					int num2;
					switch (i)
					{
					default:
						num2 = 0;
						break;
					case 3:
						num2 = -1;
						break;
					case 2:
						num2 = 1;
						break;
					}
					WorldTile tile2 = world.GetTile(pX, y + num2);
					if (tile2 != null && !dictionary.ContainsKey(tile2) && Liquid(tile2))
					{
						dictionary[tile2] = worldTile2;
						queue.Enqueue(tile2);
					}
				}
			}
			for (WorldTile worldTile3 = worldTile; worldTile3 != null; worldTile3 = dictionary[worldTile3])
			{
				list.Add(P(worldTile3));
			}
			list.Reverse();
			return list;
		}

		private static Vector2 Ahead(List<Vector2> way, Vector2 p, float lead)
		{
			int num = 0;
			float num2 = float.MaxValue;
			for (int i = 0; i < way.Count; i++)
			{
				float sqrMagnitude = (way[i] - p).sqrMagnitude;
				if (sqrMagnitude < num2)
				{
					num2 = sqrMagnitude;
					num = i;
				}
			}
			return way[Mathf.Min(way.Count - 1, num + Mathf.CeilToInt(lead))];
		}

		private IEnumerator OffAnyShip()
		{
			if (Hijack.Active)
			{
				Note("(still aboard a ship from the part before: getting off)");
				Mod.LeaveShipForTest();
				yield return CivilTest.Wait(1.5f);
				if (Hijack.Active)
				{
					yield return Possess(_king);
					yield return CivilTest.Wait(1f);
				}
			}
		}

		private bool Busy(Vector2 p)
		{
			foreach (Actor ship in _ships)
			{
				try
				{
					if ((ship.current_position - p).magnitude < 6f)
					{
						return true;
					}
				}
				catch
				{
				}
			}
			return false;
		}

		private static Actor NewShip(string id, WorldTile at, Kingdom k, City city)
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
				Log.Error("HIJACK ship: " + ex.Message);
				return null;
			}
		}

		private List<Actor> Riders(Actor ship, int n, City city, Vector2 near)
		{
			List<Actor> list = new List<Actor>();
			Boat simpleComponent = ship.getSimpleComponent<Boat>();
			for (int i = 0; i < n; i++)
			{
				try
				{
					Actor actor = CivilTest.SpawnNear("human", near, 1f);
					if (actor != null)
					{
						actor.joinCity(city);
						actor.embarkInto(simpleComponent);
						((BaseSimObject)actor).addStatusEffect("invincible", 900f, pColorEffect: false);
						list.Add(actor);
					}
				}
				catch (Exception ex)
				{
					Log.Error("HIJACK rider: " + ex.Message);
				}
			}
			Note($"{list.Count} aboard her ({simpleComponent.countPassengers()} on her list)");
			return list;
		}

		private IEnumerator StandBy(Actor ship, Vector2 stand)
		{
			Put(_king, stand);
			Vector2 vector = ship.current_position - stand;
			Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			Mod.SetPitchDegrees(-8f);
			yield return CivilTest.Wait(1f);
		}

		private static void Put(Actor a, Vector2 at)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile != null)
			{
				a.spawnOn(tile, 0f);
			}
			a.current_position = at;
			a.dirty_current_tile = true;
		}

		private static Vector2? StandOnLand(Vector2 near)
		{
			for (float num = 0f; num < 20f; num += 1f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = new Vector2(near.x + Mathf.Cos(f) * num, near.y + Mathf.Sin(f) * num);
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (Dry(tile))
					{
						return new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
					}
					if (num == 0f)
					{
						break;
					}
				}
			}
			return null;
		}

		private static bool OpenAround(int x, int y, int open)
		{
			for (int i = -open; i <= open; i++)
			{
				for (int j = -open; j <= open; j++)
				{
					if (!Liquid(World.world.GetTile(x + j, y + i)))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool LandWithin(int x, int y, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					if (Dry(World.world.GetTile(x + j, y + i)))
					{
						return true;
					}
				}
			}
			return false;
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
					if (OpenAround(num2, num3, open))
					{
						return World.world.GetTile(num2, num3);
					}
				}
			}
			return null;
		}

		private static bool Liquid(WorldTile t)
		{
			if (t != null && t.Type != null && t.Type.liquid)
			{
				return !t.Type.lava;
			}
			return false;
		}

		private static bool Dry(WorldTile t)
		{
			if (t != null && t.Type != null && !t.Type.liquid && !t.Type.block)
			{
				return !t.Type.lava;
			}
			return false;
		}

		private static Vector2 P(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static WorldTile Tile(Actor a)
		{
			return World.world.GetTile((int)a.current_position.x, (int)a.current_position.y);
		}
	}
}
