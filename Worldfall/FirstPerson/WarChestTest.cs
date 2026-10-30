using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WarChestTest : CivilTest
	{
		protected override string Prefix => "WCHEST";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 12, 2);
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
			Kingdom home = king.kingdom;
			Actor rival = Village("orc", 12, 2, king.current_tile, 55f) ?? Village("orc", 12, 2);
			if (!Check("a rival village", rival != null && rival.kingdom != null && rival.kingdom != home))
			{
				yield break;
			}
			City town = rival.city;
			float magnitude = (Royal.Position(town) - Royal.Position(king.city)).magnitude;
			Note($"rival town {town.name} of {Royal.NameOf(rival.kingdom)}, {magnitude:F0} tiles from home");
			Actor second = null;
			for (int i = 0; i < 4; i++)
			{
				if (second != null)
				{
					break;
				}
				Actor actor = Village("elf", 8, 0, king.current_tile, 50f + (float)i * 15f);
				if (actor != null && ((Royal.Position(actor.city) - Royal.Position(town)).magnitude > 40f || i == 3))
				{
					second = actor;
				}
			}
			City nukeTown = second?.city;
			if (nukeTown != null)
			{
				Note($"second rival town {nukeTown.name} of {Royal.NameOf(second.kingdom)}, {(Royal.Position(nukeTown) - Royal.Position(king.city)).magnitude:F0} tiles from home, {(Royal.Position(nukeTown) - Royal.Position(town)).magnitude:F0} from the first");
			}
			yield return Possess(king);
			if (!Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield break;
			}
			Royal royal = Mod.Royal;
			string said = null;
			Action<string> toast = delegate(string m)
			{
				said = m;
				Log.Info("WCHEST toast: " + m);
			};
			long num = Royal.KillsSpent(home);
			long num2 = Royal.KillsToSpend(home);
			royal.Unleash(king, town, Royal.Deed.Meteor, toast, delegate
			{
			});
			Check("a new kingdom can't pay for a meteor", Royal.KillsSpent(home) == num && said != null && said.Contains("costs"), $"chest {num2}, spent {num} -> {Royal.KillsSpent(home)}; \"{said}\"");
			home.data.total_kills += 900L;
			royal.DeclareWar(king, rival.kingdom, toast, delegate
			{
			});
			if (second != null && second.kingdom != null)
			{
				royal.DeclareWar(king, second.kingdom, toast, delegate
				{
				});
			}
			long num3 = Royal.KillsToSpend(home);
			Check("kills fill the war chest", num3 >= 900, num3 + " to spend");
			royal.Open = true;
			royal.Selected = rival.kingdom;
			yield return CivilTest.Wait(0.8f);
			yield return Capture("wchest_council.png");
			long num4 = Royal.KillsToSpend(home);
			royal.Unleash(king, town, Royal.Deed.Bandits, toast, delegate
			{
			});
			home.data.get("fp_kills_spent", out var pResult, -1L);
			Check("bandits take their price", num4 - Royal.KillsToSpend(home) == Royal.PriceOf(home, Royal.Deed.Bandits) && royal.Raids.Count > 0 && pResult == Royal.KillsSpent(home), $"{num4} -> {Royal.KillsToSpend(home)}, raids {royal.Raids.Count}, saved {pResult}");
			int people0 = Count(town);
			int buildings0 = Buildings(town);
			long earned0 = Royal.KillsEarned(home);
			Royal.Deed[] array = new Royal.Deed[3]
			{
				Royal.Deed.BombStorm,
				Royal.Deed.Napalm,
				Royal.Deed.CrabBombs
			};
			foreach (Royal.Deed deed in array)
			{
				num4 = Royal.KillsToSpend(home);
				royal.Unleash(king, town, deed, toast, delegate
				{
				});
				Check(Royal.Label(deed) + " takes its price and starts", num4 - Royal.KillsToSpend(home) == Royal.PriceOf(home, deed) && royal.Storms.Count > 0, string.Format("{0} -> {1} (price {2}), storms {3}: {4}", num4, Royal.KillsToSpend(home), Royal.PriceOf(home, deed), royal.Storms.Count, (royal.Storms.Count > 0) ? (royal.Storms[0].Status ?? Royal.Label(royal.Storms[0].Kind)) : "none"));
			}
			yield return CivilTest.Wait(1.5f);
			yield return Capture("wchest_storms.png");
			royal.Open = false;
			float t0 = Time.realtimeSinceStartup;
			while (royal.Storms.Count > 0 && Time.realtimeSinceStartup - t0 < 25f)
			{
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(3f);
			int num6 = Count(town);
			int num7 = Buildings(town);
			long num8 = Royal.KillsEarned(home);
			Check("all the bombs fell", royal.Storms.Count == 0, $"after {Time.realtimeSinceStartup - t0:F0} s, {royal.Storms.Count} still falling");
			Check("the town was hit", num7 < buildings0 || num6 < people0 || !town.isAlive(), $"buildings {buildings0} -> {num7}, people {people0} -> {num6}");
			Check("the strikes earned no kills", num8 - earned0 <= Mathf.Max(3, (people0 - num6) / 3), $"our kills {earned0} -> {num8} while {people0 - num6} of theirs died");
			num4 = Royal.KillsToSpend(home);
			if (town.isAlive())
			{
				royal.CallStorm(king, town, Royal.Storm.Meteors, toast);
				Check("the obelisk's meteor storm costs 60", num4 - Royal.KillsToSpend(home) == 60, $"{num4} -> {Royal.KillsToSpend(home)}");
				Building stone = null;
				try
				{
					stone = World.world.buildings.addBuilding("monolith", World.world.GetTile((int)king.current_position.x + 2, (int)king.current_position.y), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				}
				catch (Exception ex)
				{
					Note("monolith: " + ex.Message);
				}
				Mod.OpenObeliskForTest(stone);
				yield return CivilTest.Wait(0.6f);
				yield return Capture("wchest_obelisk.png");
				royal.ObeliskOpen = false;
			}
			else
			{
				Note("(the town fell to the war machines: the obelisk check is skipped)");
			}
			t0 = Time.realtimeSinceStartup;
			while (royal.Storms.Count > 0 && Time.realtimeSinceStartup - t0 < 25f)
			{
				yield return CivilTest.Wait(0.5f);
			}
			City city = ((nukeTown != null && nukeTown.isAlive()) ? nukeTown : town);
			if (city != null && city.isAlive())
			{
				if ((Royal.Position(city) - Royal.Position(king.city)).magnitude < Royal.BlastRadius(Royal.Deed.CzarBomba) + 6f || (Royal.Position(city) - king.current_position).magnitude < Royal.BlastRadius(Royal.Deed.CzarBomba) + 4f)
				{
					num4 = Royal.KillsToSpend(home);
					said = null;
					royal.Unleash(king, city, Royal.Deed.CzarBomba, toast, delegate
					{
					});
					Check("the Czar Bomba warns before it would hit home", Royal.KillsToSpend(home) == num4 && said != null && said.Contains("again"), $"{num4} -> {Royal.KillsToSpend(home)}; \"{said}\"");
				}
				else
				{
					Note("(the Czar Bomba's blast wouldn't reach home from here: its warning isn't tried, and it isn't dropped)");
				}
				Vector2 ground = Royal.Position(city);
				int alive0 = AliveNear(ground, 25f);
				int standing0 = StandingNear(ground, 25f);
				num4 = Royal.KillsToSpend(home);
				royal.Unleash(king, city, Royal.Deed.Nuke, toast, delegate
				{
				});
				if (Royal.KillsToSpend(home) == num4)
				{
					royal.Unleash(king, city, Royal.Deed.Nuke, toast, delegate
					{
					});
				}
				Check("a nuke takes its price", num4 - Royal.KillsToSpend(home) == Royal.PriceOf(home, Royal.Deed.Nuke), $"{num4} -> {Royal.KillsToSpend(home)}");
				t0 = Time.realtimeSinceStartup;
				while ((AliveNear(ground, 25f) > 0 || (float)StandingNear(ground, 25f) > (float)standing0 * 0.6f) && Time.realtimeSinceStartup - t0 < 25f)
				{
					yield return CivilTest.Wait(0.5f);
				}
				yield return CivilTest.Wait(2f);
				int num9 = AliveNear(ground, 25f);
				int num10 = StandingNear(ground, 25f);
				Check("the nuke kills all round it and flattens much of it", num9 <= Mathf.Max(1, alive0 / 4) && (float)num10 <= (float)standing0 * 0.6f, $"after {Time.realtimeSinceStartup - t0:F0} s within 25 tiles: buildings and trees {standing0} -> {num10}, people and beasts {alive0} -> {num9}");
			}
			else
			{
				Note("(no rival town left for the bombs)");
			}
			Note($"war chest at the end: {Royal.KillsToSpend(home)} to spend, {Royal.KillsEarned(home)} earned, {Royal.KillsSpent(home)} spent");
		}

		private static int AliveNear(Vector2 at, float radius)
		{
			int num = 0;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && (simple.current_position - at).sqrMagnitude < radius * radius)
					{
						num++;
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static int StandingNear(Vector2 at, float radius)
		{
			int num = 0;
			for (int i = Mathf.FloorToInt(at.y - radius); (float)i <= at.y + radius; i++)
			{
				for (int j = Mathf.FloorToInt(at.x - radius); (float)j <= at.x + radius; j++)
				{
					if (((float)j + 0.5f - at.x) * ((float)j + 0.5f - at.x) + ((float)i + 0.5f - at.y) * ((float)i + 0.5f - at.y) > radius * radius)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile(j, i);
					try
					{
						if (tile != null && tile.building != null && tile.building.isAlive() && tile.building.current_tile == tile)
						{
							num++;
						}
					}
					catch
					{
					}
				}
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

		private IEnumerator Capture(string name, float wait = 0.4f)
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
	}
}
