using System;
using System.Collections.Generic;
using UnityEngine;
using ai;
using life.taxi;

namespace FirstPerson
{
	internal static class Hijack
	{
		public static Actor Rider;

		public static Actor Ship;

		private static readonly List<Actor> Homeless = new List<Actor>();

		private static readonly List<long> HomelessIds = new List<long>();

		private static float _homelessAt;

		private static object _homelessWorld;

		public static bool Active
		{
			get
			{
				if (Ship != null)
				{
					return Rider != null;
				}
				return false;
			}
		}

		public static bool CanTake(Actor you, Actor ship, Vector2 from)
		{
			try
			{
				if (Active || you == null || ship == null || you == ship || !you.isAlive() || !ship.isAlive())
				{
					return false;
				}
				if (you.asset == null || you.asset.is_boat || !Dialogue.IsPerson(you))
				{
					return false;
				}
				if (you.is_inside_boat || you.is_inside_building || you.position_height > 0.3f)
				{
					return false;
				}
				if (!BoatHelm.IsBoat(ship) || BoatOf(ship) == null || !ship.canBePossessed())
				{
					return false;
				}
				if (ControllableUnit.isControllingUnit(ship) || ControllableUnit.isControllingCrabzilla())
				{
					return false;
				}
				return Boats.HullDistance(ship, from) < 1.4f;
			}
			catch
			{
				return false;
			}
		}

		public static bool Take(Actor you, Actor ship, Action<string> toast, out string realm, out int riders)
		{
			realm = null;
			riders = 0;
			if (!Boats.Board(you, ship))
			{
				return false;
			}
			Rider = you;
			Ship = ship;
			try
			{
				Boat boat = BoatOf(ship);
				riders = boat.countPassengers();
				if (boat.taxi_request != null)
				{
					TaxiManager.finish(boat.taxi_request);
					boat.taxi_request = null;
				}
				boat.taxi_target = null;
			}
			catch (Exception ex)
			{
				Log.Error("hijack: her ferrying: " + ex.Message);
			}
			try
			{
				Law.TookShip(ship, you, toast);
			}
			catch (Exception ex2)
			{
				Log.Error("hijack: the law: " + ex2.Message);
			}
			realm = Capture(ship, you);
			Log.Info(string.Format("hijack: took {0}{1}, {2} aboard", WorldBoxMod.Describe(ship), (realm != null) ? (" for " + realm) : "", riders));
			return true;
		}

		private static string Capture(Actor ship, Actor you)
		{
			try
			{
				Kingdom kingdom = you.kingdom;
				if (kingdom == null || !kingdom.isCiv())
				{
					return null;
				}
				if (ship.kingdom == kingdom)
				{
					return null;
				}
				ship.clearHomeBuilding();
				if (you.city != null && you.city.kingdom == kingdom)
				{
					ship.joinCity(you.city);
				}
				else
				{
					ship.joinKingdom(kingdom);
				}
				ActorTool.checkHomeDocks(ship);
				if (ship.getHomeBuilding() == null)
				{
					object obj = World.world?.map_stats;
					if (obj != _homelessWorld)
					{
						Homeless.Clear();
						HomelessIds.Clear();
						_homelessWorld = obj;
					}
					if (!Homeless.Contains(ship))
					{
						Homeless.Add(ship);
						HomelessIds.Add(ship.data.id);
					}
					KeepAfloat(ship);
				}
				Log.Info("hijack: " + WorldBoxMod.Describe(ship) + " now of " + kingdom.name + ", home " + ((ship.getHomeBuilding() != null) ? ("a dock of " + ((ship.city != null) ? ship.city.name : "the realm")) : "none"));
				return kingdom.name;
			}
			catch (Exception ex)
			{
				Log.Error("hijack: capturing her: " + ex.Message);
				return null;
			}
		}

		public static Vector2 PutOff(out bool wet, out int landed, out int aboard)
		{
			Actor ship = Ship;
			Actor rider = Rider;
			Ship = (Rider = null);
			landed = (aboard = 0);
			wet = false;
			Vector2 result = rider?.current_position ?? Vector2.zero;
			if (rider == null)
			{
				return result;
			}
			result = Boats.Disembark(rider, ship, out wet);
			if (!wet)
			{
				WorldTile tileAt = Toolbox.getTileAt(result.x, result.y);
				wet = tileAt != null && tileAt.Type != null && tileAt.Type.liquid && !DockDecks.On(result);
			}
			try
			{
				Boat boat = ((ship != null && ship.isAlive()) ? BoatOf(ship) : null);
				if (boat != null && boat.hasPassengers())
				{
					if (wet)
					{
						aboard = boat.countPassengers();
					}
					else
					{
						landed = boat.countPassengers();
						boat.unloadPassengers(DryTileNear(result) ?? Toolbox.getTileAt(result.x, result.y) ?? rider.current_tile, pRandomForce: false);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("hijack: putting the riders ashore: " + ex.Message);
			}
			Log.Info(string.Format("hijack: off {0} {1} at {2}, {3} landed with you, {4} left aboard", WorldBoxMod.Describe(ship), wet ? "into the sea" : "ashore", result, landed, aboard));
			return result;
		}

		public static void SendHome(Actor ship)
		{
			try
			{
				Boat boat = ((ship != null && ship.isAlive()) ? BoatOf(ship) : null);
				if (boat == null || !boat.hasPassengers())
				{
					return;
				}
				WorldTile worldTile = null;
				City city = null;
				foreach (Actor passenger in boat.getPassengers())
				{
					if (passenger != null && passenger.isAlive() && passenger.kingdom != ship.kingdom && passenger.city != null && passenger.city.isAlive())
					{
						city = passenger.city;
						break;
					}
				}
				if (city != null)
				{
					worldTile = city.getTile();
				}
				Building homeBuilding = ship.getHomeBuilding();
				if (worldTile == null && homeBuilding != null)
				{
					worldTile = homeBuilding.current_tile;
				}
				if (worldTile == null && ship.city != null)
				{
					worldTile = ship.city.getTile();
				}
				if (worldTile != null)
				{
					boat.taxi_target = worldTile;
					ship.setTask("boat_transport_go_unload");
					Log.Info($"hijack: {WorldBoxMod.Describe(ship)} takes {boat.countPassengers()} home to {worldTile.x},{worldTile.y}");
				}
			}
			catch (Exception ex)
			{
				Log.Error("hijack: sending her home: " + ex.Message);
			}
		}

		public static void Forget()
		{
			Ship = (Rider = null);
		}

		public static void KeepHomeless()
		{
			if (Homeless.Count == 0 || Time.unscaledTime < _homelessAt)
			{
				return;
			}
			_homelessAt = Time.unscaledTime + 2f;
			if (WorldBoxMod.WorldLoading)
			{
				return;
			}
			if (World.world?.map_stats != _homelessWorld)
			{
				Homeless.Clear();
				HomelessIds.Clear();
				return;
			}
			for (int num = Homeless.Count - 1; num >= 0; num--)
			{
				Actor actor = Homeless[num];
				try
				{
					if (actor == null || !actor.isAlive() || actor.data == null || actor.data.id != HomelessIds[num] || actor.getHomeBuilding() != null)
					{
						Homeless.RemoveAt(num);
						HomelessIds.RemoveAt(num);
					}
					else
					{
						KeepAfloat(actor);
					}
				}
				catch
				{
					Homeless.RemoveAt(num);
					HomelessIds.RemoveAt(num);
				}
			}
		}

		private static void KeepAfloat(Actor ship)
		{
			try
			{
				ship.data.set("existence_check", (int)World.world.getCurWorldTime() + 1000000);
			}
			catch
			{
			}
		}

		internal static void KeepHome(Actor ship)
		{
			try
			{
				ActorTool.checkHomeDocks(ship);
				if (ship.getHomeBuilding() == null)
				{
					object obj = World.world?.map_stats;
					if (obj != _homelessWorld)
					{
						Homeless.Clear();
						HomelessIds.Clear();
						_homelessWorld = obj;
					}
					if (!Homeless.Contains(ship))
					{
						Homeless.Add(ship);
						HomelessIds.Add(ship.data.id);
					}
					KeepAfloat(ship);
				}
			}
			catch (Exception ex)
			{
				Log.Error("hijack: keeping her: " + ex.Message);
			}
		}

		private static Boat BoatOf(Actor ship)
		{
			try
			{
				return ship?.getSimpleComponent<Boat>();
			}
			catch
			{
				return null;
			}
		}

		private static WorldTile DryTileNear(Vector2 at)
		{
			WorldTile result = null;
			float num = float.MaxValue;
			int num2 = Mathf.FloorToInt(at.x);
			int num3 = Mathf.FloorToInt(at.y);
			for (int i = -4; i <= 4; i++)
			{
				for (int j = -4; j <= 4; j++)
				{
					WorldTile tile = World.world.GetTile(num2 + j, num3 + i);
					if (tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block && !tile.Type.lava)
					{
						float num4 = j * j + i * i;
						if (num4 < num)
						{
							num = num4;
							result = tile;
						}
					}
				}
			}
			return result;
		}
	}
}
