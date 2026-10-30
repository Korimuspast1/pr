using System;
using System.Collections;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BoatFixTest : CivilTest
	{
		private Actor _king;

		private WorldTile _sea;

		private WorldTile _shore;

		private float _shoreYaw;

		protected override string Prefix => "BOATFIX";

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
			if (!Check("a village of ours by the sea", _king != null && _sea != null) || !Check("a shore to stand on", FindShore(out _shore, out _shoreYaw)))
			{
				yield break;
			}
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
				yield return Part("build a boat", BuildBoat());
				yield return Part("aboard: she waits, and her wheel", Aboard());
				yield return Part("taken over riding in a boat", Rider());
			}
		}

		private bool FindShore(out WorldTile shore, out float yaw)
		{
			shore = null;
			yaw = 0f;
			Vector2 current_position = _king.current_position;
			Vector2 b = P(_sea);
			WorldTile worldTile = null;
			for (float num = 0f; num <= 1f; num += 0.02f)
			{
				Vector2 vector = Vector2.Lerp(current_position, b, num);
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y));
				if (Dry(tile))
				{
					worldTile = tile;
				}
				else if (Liquid(tile) && worldTile != null)
				{
					shore = worldTile;
					yaw = Mathf.Atan2(b.y - current_position.y, b.x - current_position.x);
					return true;
				}
			}
			return false;
		}

		private void StandOnShore()
		{
			try
			{
				Vector2 vector = new Vector2(Mathf.Cos(_shoreYaw), Mathf.Sin(_shoreYaw));
				_king.current_position = P(_shore) + vector * 0.3f;
				_king.dirty_current_tile = true;
			}
			catch
			{
			}
			Mod.SetYaw(_shoreYaw);
			Mod.SetPitchDegrees(-10f);
		}

		private IEnumerator BuildBoat()
		{
			StandOnShore();
			int num = Civil.BagCount(_king, "wood");
			if (num < 15)
			{
				try
				{
					_king.addToInventory("wood", 15 - num);
				}
				catch
				{
				}
			}
			int wood = Civil.BagCount(_king, "wood");
			yield return CivilTest.Wait(1f);
			Note("prompt '" + Mod.Prompt + "', picked " + ((Mod.Picked != null) ? Mod.Picked.ToString() : "nothing"));
			Check("facing the sea: a boat can be built", Mod.Prompt != null && Mod.Prompt.Contains("Build a boat"), Mod.Prompt);
			Mod.BuildBoatForTest();
			yield return CivilTest.Wait(2f);
			Actor lastForTest = Raft.LastForTest;
			Check("a boat built from the bag's wood", lastForTest != null && lastForTest.isAlive() && Civil.BagCount(_king, "wood") == wood - 15, string.Format("wood {0} -> {1}", wood, Civil.BagCount(_king, "wood")));
			Check("...of our realm", lastForTest != null && lastForTest.kingdom == _king.kingdom);
			Check("...and you're at her wheel", lastForTest != null && Mod.Hijacking && BoatHelm.Steers(lastForTest), "host " + WorldBoxMod.Describe(Mod.Host));
			Check("at the wheel, a king's war map is still his", Mod.HasMapPage(MapTabs.Page.War));
			Mod.SaveFrame("boatfix_built.png");
			Mod.LeaveShipForTest();
			yield return CivilTest.Wait(1.5f);
			if (Hijack.Active)
			{
				yield return Possess(_king);
				yield return CivilTest.Wait(1f);
			}
		}

		private IEnumerator Aboard()
		{
			Actor boat = null;
			WorldTile tile = World.world.GetTile(_sea.x, _sea.y);
			try
			{
				boat = World.world.units.createNewUnit("boat_trading_human", tile);
				boat.joinKingdom(_king.kingdom);
				if (_king.city != null)
				{
					boat.joinCity(_king.city);
				}
			}
			catch (Exception ex)
			{
				Note("boat: " + ex.Message);
			}
			if (!Check("a boat of ours on the water", boat != null))
			{
				yield break;
			}
			Hijack.KeepHome(boat);
			yield return CivilTest.Wait(0.5f);
			try
			{
				_king.current_position = boat.current_position;
				_king.dirty_current_tile = true;
			}
			catch
			{
			}
			Mod.EnterCabin(boat);
			yield return CivilTest.Wait(1f);
			if (Check("aboard, in her cabin", Mod.Cabin.Inside && Mod.Cabin.Boat == boat))
			{
				Vector2 p0 = boat.current_position;
				float moved = 0f;
				for (int i = 0; i < 24; i++)
				{
					yield return CivilTest.Wait(0.25f);
					moved = Mathf.Max(moved, (boat.current_position - p0).magnitude);
				}
				Check("she waits while you're aboard", moved < 0.6f && Mod.BoatHeldForTest, $"moved {moved:F2}, held {Mod.BoatHeldForTest}");
				Mod.TakeWheelForTest();
				yield return CivilTest.Wait(2f);
				Check("from the cabin to her wheel", Mod.Hijacking && BoatHelm.Steers(boat), "host " + WorldBoxMod.Describe(Mod.Host));
				Mod.SaveFrame("boatfix_wheel.png");
				Mod.LeaveShipForTest();
				yield return CivilTest.Wait(1.5f);
				if (Hijack.Active)
				{
					yield return Possess(_king);
					yield return CivilTest.Wait(1f);
				}
			}
		}

		private IEnumerator Rider()
		{
			Actor boat = null;
			try
			{
				boat = World.world.units.createNewUnit("boat_trading_human", _sea);
				boat.joinKingdom(_king.kingdom);
			}
			catch (Exception ex)
			{
				Note("boat: " + ex.Message);
			}
			if (!Check("a boat with a rider", boat != null))
			{
				yield break;
			}
			Hijack.KeepHome(boat);
			Actor rider = CivilTest.SpawnNear("human", P(_shore), 1f);
			try
			{
				rider.joinCity(_king.city);
				rider.embarkInto(boat.getSimpleComponent<Boat>());
				((BaseSimObject)rider).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch (Exception ex2)
			{
				Note("rider: " + ex2.Message);
			}
			yield return CivilTest.Wait(0.5f);
			if (!Check("...riding in her", rider?.is_inside_boat ?? false))
			{
				yield break;
			}
			yield return Possess(rider);
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (Mod.Cabin.Inside)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("taken over: in her cabin, not in the sea", Mod.Cabin.Inside && Mod.Cabin.Boat == boat && !Mod.Swimming, $"cabin {Mod.Cabin.Inside}, swimming {Mod.Swimming}");
			Mod.SaveFrame("boatfix_rider.png");
			Mod.LeaveCabinForTest();
			yield return CivilTest.Wait(1f);
			bool flag = false;
			try
			{
				flag = boat.getSimpleComponent<Boat>().hasPassenger(rider);
			}
			catch
			{
			}
			Check("...and off her passenger list once off", !flag && !rider.is_inside_boat);
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
	}
}
