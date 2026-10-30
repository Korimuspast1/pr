using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal sealed class InsideTest : CivilTest
	{
		private Building _house;

		private Building _tent;

		private Actor _mum;

		private Actor _dad;

		private Actor _kid;

		private Actor _lodger;

		private Actor _camper;

		private Actor _visitor;

		private readonly Dictionary<long, int> _beds = new Dictionary<long, int>();

		protected override string Prefix => "INSIDE";

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 12, 3);
			if (!Check("a village founded", actor != null))
			{
				yield break;
			}
			City city = actor.city;
			_house = AddHouse("house_human_2", city);
			_tent = AddHouse("tent_human", city);
			if (!Check("a house and a tent", _house != null && _tent != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item == null || item == actor || !item.isAlive())
				{
					continue;
				}
				if (item.isAdult())
				{
					if (_mum == null)
					{
						_mum = item;
					}
					else if (_dad == null)
					{
						_dad = item;
					}
					else if (_lodger == null)
					{
						_lodger = item;
					}
					else if (_camper == null)
					{
						_camper = item;
					}
					else if (_visitor == null)
					{
						_visitor = item;
					}
				}
				else if (_kid == null)
				{
					_kid = item;
				}
			}
			if (Check("a household", _mum != null && _dad != null && _kid != null && _lodger != null && _camper != null && _visitor != null))
			{
				try
				{
					_mum.becomeLoversWith(_dad);
				}
				catch (Exception ex)
				{
					Note("lovers: " + ex.Message);
				}
				Actor[] array = new Actor[4] { _mum, _dad, _kid, _lodger };
				for (int i = 0; i < array.Length; i++)
				{
					array[i].setHomeBuilding(_house);
				}
				_camper.setHomeBuilding(_tent);
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				try
				{
					ScrollWindow.hideAllEvent(pWithAnimation: false);
				}
				catch
				{
				}
				Config.paused = true;
				yield return Part("places in the house", Places());
				yield return Part("out at the door, the same beds the next night", Nights());
				yield return Part("a tent's room of old", Tent());
				yield return Part("the 3D room agrees", Room());
				yield return Part("an upgrade with people in", Upgrade());
				yield return Part("a death inside", Death());
				yield return Part("a house ruined with people in", Ruin());
				Config.paused = false;
			}
		}

		private void In(Actor a, Building b, bool sleep)
		{
			try
			{
				a.stayInBuilding(b);
				if (sleep)
				{
					((BaseSimObject)a).addStatusEffect("sleeping", 600f, pColorEffect: false);
				}
			}
			catch (Exception ex)
			{
				Note("in: " + ex.Message);
			}
		}

		private void Out(Actor a)
		{
			try
			{
				a.finishStatusEffect("sleeping");
			}
			catch
			{
			}
			try
			{
				a.exitBuilding();
			}
			catch
			{
			}
		}

		private static bool InFootprint(Building b, Vector2 p, WorldTile t)
		{
			BuildingFundament fundament = b.asset.fundament;
			WorldTile current_tile = b.current_tile;
			int num = current_tile.x - fundament.left;
			int num2 = current_tile.x + fundament.right;
			int num3 = current_tile.y - fundament.bottom;
			int num4 = current_tile.y + fundament.top;
			if (p.x >= (float)num && p.x < (float)(num2 + 1) && p.y >= (float)num3 && p.y < (float)(num4 + 1) && t != null && t.x >= num && t.x <= num2 && t.y >= num3)
			{
				return t.y <= num4;
			}
			return false;
		}

		private IEnumerator Settle(params Actor[] who)
		{
			for (float t = 0f; t < 15f; t += 0.25f)
			{
				bool flag = true;
				foreach (Actor a in who)
				{
					flag &= InsideSpots.Where(a, out var _, out var _);
				}
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
		}

		private IEnumerator Places()
		{
			In(_mum, _house, sleep: true);
			In(_dad, _house, sleep: true);
			In(_kid, _house, sleep: true);
			In(_lodger, _house, sleep: false);
			yield return Settle(_mum, _dad, _kid, _lodger);
			HouseWorld.Placement pose;
			RoomLayout roomLayout = InsideSpots.LayoutOf(_house, out pose);
			Check("the house's layout made", roomLayout != null, $"{InsideSpots.LayoutsForTest} made");
			if (roomLayout == null)
			{
				yield break;
			}
			Note($"layout: {roomLayout.Places.Count} places, beds {Count(roomLayout, RoomLayout.Use.Bed)} sleeping {Sleeps(roomLayout)}, seats {Count(roomLayout, RoomLayout.Use.HearthSeat) + Count(roomLayout, RoomLayout.Use.TableSeat)}, fitted {roomLayout.Fitted}");
			bool flag = false;
			Actor[] array = new Actor[4] { _mum, _dad, _kid, _lodger };
			RoomLayout layout;
			foreach (Actor actor in array)
			{
				RoomLayout.Place place;
				bool flag2 = InsideSpots.Where(actor, out layout, out place);
				Note(string.Format("{0}: {1}, body at {2} tile {3},{4} (front tile {5},{6})", WorldBoxMod.Describe(actor), flag2 ? $"{place.Use} at ({place.MidX:F2},{place.MidY:F2}) storey {place.Storey}" : "no place", actor.current_position, actor.current_tile?.x, actor.current_tile?.y, _house.current_tile.x, _house.current_tile.y));
				Check(WorldBoxMod.Describe(actor) + " has a place", flag2);
				if (flag2)
				{
					Check("...on the house's own tiles", InFootprint(_house, actor.current_position, actor.current_tile));
					flag |= actor.current_tile != _house.current_tile;
					if (actor != _lodger)
					{
						Check("...a sleeper in a bed", place.Use == RoomLayout.Use.Bed || Sleeps(roomLayout) < 4, place.Use.ToString());
						_beds[actor.data.id] = Index(roomLayout, place);
					}
					else
					{
						Check("...the one up not in a bed", place.Use != RoomLayout.Use.Bed, place.Use.ToString());
					}
				}
			}
			Check("bodies spread over the house (not all on its front tile)", flag);
			if (InsideSpots.Where(_mum, out layout, out var place2) && InsideSpots.Where(_dad, out layout, out var place3) && place2.Sleeps >= 2)
			{
				Check("the couple share their bed", place2 == place3);
			}
		}

		private static int Count(RoomLayout L, RoomLayout.Use u)
		{
			int num = 0;
			foreach (RoomLayout.Place place in L.Places)
			{
				if (place.Use == u)
				{
					num++;
				}
			}
			return num;
		}

		private static int Sleeps(RoomLayout L)
		{
			int num = 0;
			foreach (RoomLayout.Place place in L.Places)
			{
				if (place.Use == RoomLayout.Use.Bed)
				{
					num += place.Sleeps;
				}
			}
			return num;
		}

		private static int Index(RoomLayout L, RoomLayout.Place p)
		{
			return L.Places.IndexOf(p);
		}

		private IEnumerator Nights()
		{
			WorldTile door = _house.door_tile;
			Actor[] array = new Actor[4] { _mum, _dad, _kid, _lodger };
			foreach (Actor a in array)
			{
				Out(a);
			}
			yield return CivilTest.Wait(0.3f);
			bool flag = true;
			string text = "";
			array = new Actor[4] { _mum, _dad, _kid, _lodger };
			foreach (Actor actor in array)
			{
				flag &= actor.current_tile != null && door != null && Mathf.Max(Mathf.Abs(actor.current_tile.x - door.x), Mathf.Abs(actor.current_tile.y - door.y)) <= 1;
				text += $"{WorldBoxMod.Describe(actor)} {actor.current_tile?.x},{actor.current_tile?.y} inside {actor.is_inside_building}; ";
			}
			Check("out: on the door tile", flag, $"door {door?.x},{door?.y}: {text}");
			Check("...their places given up", !InsideSpots.Where(_mum, out var layout, out var _));
			In(_kid, _house, sleep: true);
			In(_dad, _house, sleep: true);
			In(_mum, _house, sleep: true);
			yield return Settle(_mum, _dad, _kid);
			HouseWorld.Placement pose;
			RoomLayout roomLayout = InsideSpots.LayoutOf(_house, out pose);
			bool flag2 = roomLayout != null;
			array = new Actor[3] { _mum, _dad, _kid };
			foreach (Actor actor2 in array)
			{
				if (_beds.TryGetValue(actor2.data.id, out var value))
				{
					flag2 &= InsideSpots.Where(actor2, out layout, out var place2) && Index(roomLayout, place2) == value;
				}
			}
			Check("the same beds the next night", flag2);
		}

		private IEnumerator Tent()
		{
			In(_camper, _tent, sleep: true);
			yield return Settle(_camper);
			RoomLayout layout;
			RoomLayout.Place place;
			bool flag = InsideSpots.Where(_camper, out layout, out place);
			Check("the tent's sleeper has a place", flag);
			if (flag)
			{
				Note($"tent: {place.Use}, room of old {!layout.Fitted}, body at {_camper.current_position} tile {_camper.current_tile?.x},{_camper.current_tile?.y}");
				Check("...in the tent's footprint", InFootprint(_tent, _camper.current_position, _camper.current_tile));
			}
		}

		private IEnumerator Room()
		{
			In(_lodger, _house, sleep: false);
			yield return Settle(_lodger);
			yield return Possess(_visitor);
			if (!Check("in a body", InControl(_visitor)))
			{
				yield break;
			}
			try
			{
				_visitor.setHomeBuilding(_house);
			}
			catch
			{
			}
			Mod.EnterHouseForTest(_house);
			yield return CivilTest.Wait(2f);
			RoomWalkers roomWalkers = ((Mod.Interior != null) ? Mod.Interior.Walkers : null);
			if (!Check("in the house's room", roomWalkers != null && Mod.Interior.House == _house))
			{
				yield break;
			}
			int num = 0;
			int num2 = 0;
			foreach (RoomWalkers.Walker item in roomWalkers.All)
			{
				if (InsideSpots.Where(item.Who, out var _, out var place))
				{
					num2++;
					Vector2 a = ((item.Bed >= 0) ? item.Pos : item.Pos);
					float num3 = ((place.Use == RoomLayout.Use.Bed) ? Vector2.Distance(a, new Vector2(place.X, place.Y)) : Vector2.Distance(a, new Vector2(place.X, place.Y)));
					Note(string.Format("room: {0} {1} at {2}, the map's {3} ({4:F2},{5:F2}), {6:F2} off", WorldBoxMod.Describe(item.Who), (item.Bed >= 0) ? ("in bed " + item.Bed) : ("at spot " + item.Spot), item.Pos, place.Use, place.X, place.Y, num3));
					if (num3 < 0.35f)
					{
						num++;
					}
				}
			}
			Check("the room's people where the map has them", num2 > 0 && num == num2, $"{num} of {num2}");
			Mod.ExitHouseForTest();
			yield return CivilTest.Wait(1f);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1f);
		}

		private IEnumerator Upgrade()
		{
			In(_lodger, _house, sleep: false);
			yield return Settle(_mum, _dad, _kid, _lodger);
			bool flag = false;
			try
			{
				flag = _house.canBeUpgraded() && _house.upgradeBuilding();
			}
			catch (Exception ex)
			{
				Note("upgrade: " + ex.Message);
			}
			Note($"upgraded {flag}: now {_house.asset.id}");
			yield return CivilTest.Wait(3f);
			yield return Settle(_mum, _dad, _kid, _lodger);
			bool ok = _mum.is_inside_building && _mum.inside_building == _house;
			Check("still inside after the upgrade", ok);
			Check("...placed again in the bigger house", InsideSpots.Where(_mum, out var _, out var _) && InFootprint(_house, _mum.current_position, _mum.current_tile));
		}

		private IEnumerator Death()
		{
			Actor dead = _lodger;
			_ = dead.current_position;
			CivilTest.Kill(dead);
			yield return CivilTest.Wait(1.5f);
			Check("a death inside: no place kept", !InsideSpots.Where(dead, out var _, out var _));
		}

		private IEnumerator Ruin()
		{
			Actor a = _mum;
			try
			{
				_house.startDestroyBuilding();
			}
			catch (Exception ex)
			{
				Note("ruin: " + ex.Message);
			}
			Config.paused = false;
			yield return CivilTest.Wait(2f);
			Config.paused = true;
			Check("the house gone: its people out", !a.is_inside_building, $"inside {a.is_inside_building}");
			Check("...no place kept", !InsideSpots.Where(a, out var _, out var _));
		}
	}
}
