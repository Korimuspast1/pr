using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HallStoreTest : CivilTest
	{
		private Actor _king;

		private Actor _you;

		private City _town;

		protected override string Prefix => "HALLSTORE";

		protected override IEnumerator Body()
		{
			_king = Village("human", 6, 0);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			_town = _king.city;
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
			foreach (Actor item2 in Founded)
			{
				if (item2 != null && item2 != _king && item2.isAdult())
				{
					_you = item2;
					break;
				}
			}
			if (!Check("a villager to be", _you != null))
			{
				yield break;
			}
			ClaimLand(_town);
			Building hall = AddBuilding("hall_human_0", 0);
			Building pile = AddBuilding("stockpile", 4);
			yield return CivilTest.Wait(1f);
			if (!Check("a town hall", hall?.isAlive() ?? false))
			{
				yield break;
			}
			Check("the hall keeps the town's goods", hall.asset.storage || hall.asset.is_stockpile, hall.asset.id);
			for (float t = 0f; t < 10f; t += 0.5f)
			{
				if (HouseInterior.IsEnterable(hall))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			if (Check("the hall can be gone into", HouseInterior.IsEnterable(hall)))
			{
				yield return Possess(_you);
				if (Check("first person on a villager", InControl(_you)))
				{
					yield return Part("the hall's door", HallDoor(hall));
					yield return Part("a stockpile", Stockpile(pile));
				}
			}
		}

		private IEnumerator HallDoor(Building hall)
		{
			Bag.Put(_you, "wood", 6, spill: false);
			yield return CivilTest.Wait(0.3f);
			if (!Check("wood in the bag", Carried("wood") == 6, Carried("wood").ToString()))
			{
				yield break;
			}
			bool there = false;
			yield return StandAt(hall, 2.2f, delegate(bool ok)
			{
				there = ok;
			});
			if (!Check("at the hall's door, looking at it", there, $"picked {Describe(Mod.Picked)}, {Away(hall):F2} from it"))
			{
				yield break;
			}
			Note("prompt: " + Mod.Prompt);
			Check("E enters, Shift+E stores", Mod.Prompt != null && Mod.Prompt.StartsWith("E:Enter") && Mod.Prompt.Contains("Shift+E:Store 6 resources"), Mod.Prompt);
			int stock = _town.getResourcesAmount("wood");
			TouchControls.PulseForTest(KeyCode.E);
			for (int i = 0; i < 20; i++)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				yield return null;
			}
			yield return CivilTest.Wait(0.3f);
			Check("E at the door: in the hall", Mod.Interior.Inside && Mod.Interior.House == hall, $"inside {Mod.Interior.Inside}");
			Check("...and the bag is as it was", Carried("wood") == 6, Carried("wood") + " wood");
			Check("...and nothing went into the town's stores", _town.getResourcesAmount("wood") == stock, string.Format("{0} -> {1}", stock, _town.getResourcesAmount("wood")));
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			if (!Check("out again", !Mod.Interior.Inside))
			{
				yield break;
			}
			yield return StandAt(hall, 2.2f, delegate(bool ok)
			{
				there = ok;
			});
			if (!Check("back at the door", there))
			{
				yield break;
			}
			PlayInput.TestPress(KeyCode.E);
			for (float t = 0f; t < 3f; t += 0.1f)
			{
				if (Mod.ActionWheelOpen)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			int num = Array.FindIndex(Mod.ActionWheel.Labels, 0, Mod.ActionWheel.Count, (string l) => l?.StartsWith("Store") ?? false);
			Check("held, E brings up the wheel with Store", Mod.ActionWheelOpen && num >= 0, string.Join(", ", Mod.ActionWheel.Labels, 0, Mod.ActionWheel.Count));
			Mod.ActionWheel.PointForTest(num);
			PlayInput.TestRelease(KeyCode.E);
			for (int i = 0; i < 20; i++)
			{
				if (Carried("wood") <= 0)
				{
					break;
				}
				yield return null;
			}
			yield return CivilTest.Wait(0.3f);
			Check("Store on the wheel at the door stores the bag", Carried("wood") == 0, Carried("wood") + " wood left");
			Check("...into the town's stores", _town.getResourcesAmount("wood") >= stock + 6, string.Format("{0} -> {1}", stock, _town.getResourcesAmount("wood")));
			Check("...without going in", !Mod.Interior.Inside);
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator Stockpile(Building pile)
		{
			if (!Check("a stockpile", pile?.isAlive() ?? false))
			{
				yield break;
			}
			Check("a stockpile isn't gone into", !HouseInterior.IsEnterable(pile));
			Bag.Put(_you, "stone", 4, spill: false);
			yield return CivilTest.Wait(0.3f);
			bool there = false;
			yield return StandAt(pile, 2.9f, delegate(bool ok)
			{
				there = ok;
			});
			if (!Check("by the stockpile, looking at it", there, $"picked {Describe(Mod.Picked)}, {Away(pile):F2} from it"))
			{
				yield break;
			}
			Note("prompt: " + Mod.Prompt);
			Check("E stores, as before", Mod.Prompt != null && Mod.Prompt.StartsWith("E:Store 4 resources"), Mod.Prompt);
			TouchControls.PulseForTest(KeyCode.E);
			for (int i = 0; i < 20; i++)
			{
				if (Carried("stone") <= 0)
				{
					break;
				}
				yield return null;
			}
			yield return CivilTest.Wait(0.3f);
			Check("E at a stockpile stores the bag", Carried("stone") == 0, Carried("stone") + " stone left");
		}

		private IEnumerator StandAt(Building b, float reach, Action<bool> done)
		{
			Vector2 c = new Vector2(b.current_tile.posV3.x, b.current_tile.posV3.y);
			for (int n = 0; n < 16; n++)
			{
				float a = -MathF.PI / 2f + (((n & 1) == 0) ? 1f : (-1f)) * (float)((n + 1) / 2) * MathF.PI / 8f;
				float[] array = new float[3] { 1.4f, 1.8f, 2.1f };
				foreach (float num in array)
				{
					if (num >= reach)
					{
						continue;
					}
					Vector2 vector = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * num;
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (tile != null && tile.Type != null && tile.Type.ground && !tile.Type.block)
					{
						Put(_you, vector);
						Vector2 vector2 = c - vector;
						Mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
						Mod.SetPitchDegrees(-6f);
						yield return CivilTest.Wait(0.4f);
						if (Mod.Picked == b && Away(b) < reach)
						{
							done(obj: true);
							yield break;
						}
					}
				}
			}
			done(obj: false);
		}

		private float Away(Building b)
		{
			return (new Vector2(b.current_tile.posV3.x, b.current_tile.posV3.y) - _you.current_position).magnitude;
		}

		private int Carried(string res)
		{
			try
			{
				Dictionary<string, ResourceContainer> dictionary = _you.inventory?.getResources();
				ResourceContainer value;
				return (dictionary != null && dictionary.TryGetValue(res, out value)) ? value.amount : 0;
			}
			catch
			{
				return 0;
			}
		}

		private static string Describe(BaseSimObject o)
		{
			if (o is Building building)
			{
				if (building.asset == null)
				{
					return "a building";
				}
				return building.asset.id;
			}
			if (o is Actor actor)
			{
				if (actor.asset == null)
				{
					return "someone";
				}
				return actor.asset.id;
			}
			return "nothing";
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

		private void ClaimLand(City c)
		{
			try
			{
				TileZone tileZone = c.getTile()?.zone;
				if (tileZone == null)
				{
					return;
				}
				HashSet<TileZone> hashSet = new HashSet<TileZone> { tileZone };
				for (int i = 0; i < 2; i++)
				{
					foreach (TileZone item in new List<TileZone>(hashSet))
					{
						TileZone[] neighbours_all = item.neighbours_all;
						foreach (TileZone tileZone2 in neighbours_all)
						{
							if (tileZone2 != null)
							{
								hashSet.Add(tileZone2);
							}
						}
					}
				}
				foreach (TileZone item2 in hashSet)
				{
					if (item2.city == null)
					{
						c.addZone(item2);
					}
				}
			}
			catch (Exception ex)
			{
				Note("land: " + ex.Message);
			}
		}

		private Building AddBuilding(string id, int ring)
		{
			try
			{
				WorldTile tile = _town.getTile();
				if (tile == null)
				{
					return null;
				}
				for (int i = 0; i < 200; i++)
				{
					float f = (float)ring * 2.4f + (float)i * 0.61f;
					float num = 5f + (float)ring * 2.5f + (float)i * 0.15f;
					WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * num), tile.y + (int)(Mathf.Sin(f) * num));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || (tile2.building != null && AssetStyle.IsSolid(tile2.building)) || tile2.zone == null || tile2.zone.city != _town)
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding(id, tile2, pCheckForBuild: true, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						try
						{
							building.completeConstruction();
							building.setKingdom(_town.kingdom);
						}
						catch
						{
						}
						if (building.city != _town)
						{
							_town.listBuilding(building);
						}
						return building;
					}
				}
			}
			catch (Exception ex)
			{
				Note("place " + id + ": " + ex.Message);
			}
			return null;
		}
	}
}
