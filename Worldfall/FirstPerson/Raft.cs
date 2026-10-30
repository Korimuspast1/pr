using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class Raft
	{
		public const int Wood = 15;

		private const string BoatId = "boat_fishing";

		internal static Actor LastForTest;

		public static WorldTile WaterAhead(Vector2 at, float yaw)
		{
			try
			{
				Vector2 vector = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
				for (float num = 0.8f; num <= 1.6f; num += 0.4f)
				{
					Vector2 vector2 = at + vector * num;
					WorldTile tile = World.world.GetTile(Mathf.FloorToInt(vector2.x), Mathf.FloorToInt(vector2.y));
					if (tile == null || tile.Type == null || !tile.Type.liquid || tile.Type.lava || DockDecks.On(new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f)))
					{
						continue;
					}
					int num2 = 0;
					for (int i = -4; i <= 4; i++)
					{
						for (int j = -4; j <= 4; j++)
						{
							WorldTile tile2 = World.world.GetTile(tile.x + j, tile.y + i);
							if (tile2 != null && tile2.Type != null && tile2.Type.liquid && !tile2.Type.lava)
							{
								num2++;
							}
						}
					}
					return (num2 >= 30) ? tile : null;
				}
			}
			catch
			{
			}
			return null;
		}

		public static bool BoatNear(Actor you, Vector2 at, float reach = 8f)
		{
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.asset != null && simple.asset.is_boat && simple.isAlive() && simple.kingdom == you.kingdom && (simple.current_position - at).sqrMagnitude < reach * reach)
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		public static Actor Build(Actor you, WorldTile water, Action<string> toast)
		{
			if (Civil.BagCount(you, "wood") < 15)
			{
				toast?.Invoke(string.Format("You need {0} wood to build a boat (you have {1})", 15, Civil.BagCount(you, "wood")));
				return null;
			}
			Actor actor = null;
			try
			{
				actor = World.world.units.createNewUnit("boat_fishing", water);
			}
			catch (Exception ex)
			{
				Log.Error("raft: " + ex.Message);
			}
			if (actor == null)
			{
				toast?.Invoke("A boat can't be built here");
				return null;
			}
			if (!Civil.TakeFromBag(you, "wood", 15))
			{
				try
				{
					actor.dieSimpleNone();
				}
				catch
				{
				}
				return null;
			}
			try
			{
				if (you.city != null && you.city.kingdom == you.kingdom)
				{
					actor.joinCity(you.city);
				}
				else if (you.kingdom != null && you.kingdom.isCiv())
				{
					actor.joinKingdom(you.kingdom);
				}
			}
			catch
			{
			}
			Hijack.KeepHome(actor);
			LastForTest = actor;
			Log.Info($"raft: {WorldBoxMod.Describe(you)} built {WorldBoxMod.Describe(actor)} at {water.x},{water.y} from {15} wood");
			return actor;
		}
	}
}
