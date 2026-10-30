using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal abstract class CivilTest : MonoBehaviour
	{
		protected WorldBoxMod Mod;

		protected int Pass;

		protected int Fail;

		protected string Options = "";

		private bool _quit;

		protected readonly List<Actor> Founded = new List<Actor>();

		protected abstract string Prefix { get; }

		protected abstract IEnumerator Body();

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			Options = (File.Exists(path) ? File.ReadAllText(path) : "");
			_quit = Options.Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			Mod = WorldBoxMod.Instance;
			Log.Info($"{Prefix} start: {World.world.units.Count} units, options \"{Options.Trim()}\"");
			IEnumerator body = Body();
			while (true)
			{
				object current;
				try
				{
					if (!body.MoveNext())
					{
						break;
					}
					current = body.Current;
					goto IL_01bf;
				}
				catch (Exception ex)
				{
					Log.Error(Prefix + " test failed: " + ex);
					Fail++;
				}
				break;
				IL_01bf:
				yield return current;
			}
			Config.paused = false;
			Log.Info($"{Prefix} {Pass} passed, {Fail} failed");
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info(Prefix + " DONE");
			if (_quit)
			{
				Application.Quit();
			}
		}

		protected bool Check(string what, bool ok, string detail = null)
		{
			if (ok)
			{
				Pass++;
			}
			else
			{
				Fail++;
			}
			Log.Info(Prefix + " " + (ok ? "PASS" : "FAIL") + " " + what + ((detail != null) ? ("  (" + detail + ")") : ""));
			return ok;
		}

		protected void Note(string what)
		{
			Log.Info(Prefix + " " + what);
		}

		protected IEnumerator Part(string name, IEnumerator part)
		{
			Log.Info(Prefix + " --- " + name);
			while (true)
			{
				object current;
				try
				{
					if (!part.MoveNext())
					{
						break;
					}
					current = part.Current;
					goto IL_0084;
				}
				catch (Exception arg)
				{
					Log.Error($"{Prefix} {name} failed: {arg}");
					Fail++;
				}
				break;
				IL_0084:
				yield return current;
			}
			Config.paused = false;
			if (Mod != null)
			{
				Mod.DebugEyeSet = false;
				Mod.DebugPrompt = null;
			}
		}

		protected static WaitForSecondsRealtime Wait(float s)
		{
			return new WaitForSecondsRealtime(s);
		}

		protected Actor Village(string species, int adults, int children, WorldTile near = null, float minAway = 0f, WorldTile avoid = null)
		{
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if (avoid != null && (float)((item.x - avoid.x) * (item.x - avoid.x) + (item.y - avoid.y) * (item.y - avoid.y)) < 576f)
				{
					continue;
				}
				if (near != null)
				{
					float num = Mathf.Sqrt((item.x - near.x) * (item.x - near.x) + (item.y - near.y) * (item.y - near.y));
					if (num < minAway || num > minAway + 70f)
					{
						continue;
					}
					try
					{
						if (!item.isSameIsland(near))
						{
							continue;
						}
					}
					catch
					{
					}
				}
				Actor actor = Found(item, species, adults, children);
				if (actor != null)
				{
					return actor;
				}
			}
			return null;
		}

		private Actor Found(WorldTile center, string species, int adults, int children)
		{
			Founded.Clear();
			for (int i = 0; i < adults + children; i++)
			{
				WorldTile worldTile = World.world.GetTile(center.x + i % 5 * 2 - 4, center.y + i / 5 * 2 - 3) ?? center;
				if (worldTile.main_type == null || worldTile.main_type.liquid)
				{
					worldTile = center;
				}
				try
				{
					Actor actor = World.world.units.spawnNewUnit(species, worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, i < adults);
					if (actor != null)
					{
						Founded.Add(actor);
					}
				}
				catch (Exception ex)
				{
					Log.Error(Prefix + " spawn failed: " + ex.Message);
				}
			}
			if (Founded.Count == 0)
			{
				return null;
			}
			Actor actor2 = Founded[0];
			try
			{
				if (!actor2.buildCityAndStartCivilization())
				{
					foreach (Actor item in Founded)
					{
						if (item != null && item.isAlive())
						{
							item.dieSimpleNone();
						}
					}
					Founded.Clear();
					return null;
				}
				City city = actor2.city;
				foreach (Actor item2 in Founded)
				{
					if (item2 != actor2 && item2.city != city)
					{
						item2.joinCity(city);
					}
				}
				Kingdom kingdom = actor2.kingdom;
				if (kingdom != null && !kingdom.hasKing())
				{
					kingdom.setKing(actor2);
				}
				for (int j = 1; j < Founded.Count && j < adults; j++)
				{
					if (j % 4 != 0 && Founded[j].isAdult())
					{
						city.makeWarrior(Founded[j]);
					}
				}
				Note($"village {city.name} of {kingdom?.name}: {Founded.Count} people, king {WorldBoxMod.Describe(actor2)}");
				return actor2;
			}
			catch (Exception ex2)
			{
				Log.Error(Prefix + " founding failed: " + ex2);
				return null;
			}
		}

		protected static WorldTile Stage()
		{
			WorldTile result = null;
			float num = float.MaxValue;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				int num2 = int.MaxValue;
				int num3 = int.MinValue;
				int num4 = 0;
				for (int i = item.y - 6; i <= item.y + 8; i++)
				{
					for (int j = item.x - 8; j <= item.x + 8; j++)
					{
						WorldTile tile = World.world.GetTile(j, i);
						if (tile == null || tile.main_type == null)
						{
							num4++;
							continue;
						}
						if (tile.main_type.liquid || tile.main_type.block)
						{
							num4++;
						}
						num2 = Mathf.Min(num2, tile.main_type.height_min);
						num3 = Mathf.Max(num3, tile.main_type.height_min);
					}
				}
				float num5 = (float)num4 * 100f + (float)(num3 - num2);
				if (num5 < num)
				{
					num = num5;
					result = item;
				}
			}
			return result;
		}

		protected static Actor SpawnNear(string id, Vector2 at, float from = 0f, bool adult = true)
		{
			for (float num = from; num < from + 14f; num += 1.5f)
			{
				for (int i = 0; i < 12; i++)
				{
					float f = (float)i * MathF.PI / 6f + num;
					Vector2 vector = at + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.block)
					{
						Actor actor = Spawn(id, new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f), adult);
						if (actor != null)
						{
							return actor;
						}
					}
				}
			}
			return null;
		}

		protected static Actor Spawn(string id, Vector2 at, bool adult = true)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile == null || tile.main_type == null || tile.main_type.liquid)
			{
				return null;
			}
			try
			{
				Actor actor = World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, adult);
				if (actor != null)
				{
					actor.current_position = new Vector2(at.x, at.y);
				}
				return actor;
			}
			catch (Exception ex)
			{
				Log.Error("spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		protected static void SetSex(Actor a, bool female)
		{
			try
			{
				a.data.sex = (female ? ActorSex.Female : ActorSex.Male);
			}
			catch
			{
			}
		}

		protected IEnumerator Possess(Actor a, float wait = 2f)
		{
			if (a == null || !a.isAlive())
			{
				Note("(can't take control of someone dead)");
				yield break;
			}
			try
			{
				if (ControllableUnit.isControllingUnit())
				{
					FirstPerson.Possess.Swap(a);
				}
				else
				{
					ControllableUnit.setControllableCreature(a);
				}
			}
			catch (Exception ex)
			{
				Log.Error(Prefix + " possess: " + ex.Message);
			}
			for (float t = 0f; t < wait + 3f; t += 0.25f)
			{
				yield return Wait(0.25f);
				if (t >= wait && Mod.IsFirstPerson && Mod.Host == a)
				{
					break;
				}
			}
			Mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
		}

		protected bool InControl(Actor a)
		{
			if (Mod.IsFirstPerson)
			{
				return Mod.Host == a;
			}
			return false;
		}

		protected static void StandBefore(Actor npc, Actor you, float distance)
		{
			float f = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.ViewYaw : 0f);
			Vector2 current_position = you.current_position + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * distance;
			npc.current_position = current_position;
			try
			{
				npc.makeWait(6f);
				npc.cancelAllBeh();
				npc.makeWait(6f);
			}
			catch
			{
			}
		}

		protected static void Kill(Actor a)
		{
			if (a == null)
			{
				return;
			}
			try
			{
				a.dieSimpleNone();
			}
			catch (Exception ex)
			{
				Log.Error("kill: " + ex.Message);
			}
		}

		protected static int Money(Actor a)
		{
			try
			{
				return a.money;
			}
			catch
			{
				return -1;
			}
		}

		protected Building AddHouse(string id, City city)
		{
			try
			{
				WorldTile tile = city.getTile();
				for (int i = 0; i < 300; i++)
				{
					if (tile == null)
					{
						break;
					}
					float f = (float)i * 0.61f;
					float num = 5f + (float)i * 0.2f;
					WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * num), tile.y + (int)(Mathf.Sin(f) * num));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || (tile2.building != null && AssetStyle.IsSolid(tile2.building)))
					{
						continue;
					}
					bool flag = true;
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							WorldTile tile3 = World.world.GetTile(tile2.x + k, tile2.y + j);
							if (tile3 == null || tile3.Type == null || !tile3.Type.ground || tile3.Type.block || (tile3.building != null && AssetStyle.IsSolid(tile3.building)))
							{
								flag = false;
							}
						}
					}
					if (!flag)
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding(id, tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building == null)
					{
						continue;
					}
					try
					{
						if (tile2.zone != null)
						{
							List<TileZone> list = new List<TileZone> { tile2.zone };
							TileZone[] neighbours_all = tile2.zone.neighbours_all;
							foreach (TileZone tileZone in neighbours_all)
							{
								if (tileZone != null)
								{
									list.Add(tileZone);
								}
							}
							foreach (TileZone item in list)
							{
								if (item.city == null)
								{
									city.addZone(item);
								}
							}
						}
					}
					catch (Exception ex)
					{
						Note("land: " + ex.Message);
					}
					try
					{
						building.completeConstruction();
						building.setKingdom(city.kingdom);
					}
					catch
					{
					}
					if (building.city != city)
					{
						city.listBuilding(building);
					}
					return building;
				}
			}
			catch (Exception ex2)
			{
				Log.Error(Prefix + " house: " + ex2.Message);
			}
			return null;
		}

		protected bool GiveStockpile(City c)
		{
			if (c == null)
			{
				return false;
			}
			try
			{
				if (c.hasStorages())
				{
					return true;
				}
				WorldTile tile = c.getTile();
				List<WorldTile> list = new List<WorldTile>();
				if (tile != null)
				{
					ClearBox(tile.x - 12, tile.y - 12, tile.x + 12, tile.y + 12);
					for (int i = 2; i <= 12; i++)
					{
						for (int j = -i; j <= i; j++)
						{
							for (int k = -i; k <= i; k++)
							{
								if (Mathf.Max(Mathf.Abs(j), Mathf.Abs(k)) == i)
								{
									WorldTile tile2 = World.world.GetTile(tile.x + j, tile.y + k);
									if (tile2 != null)
									{
										list.Add(tile2);
									}
								}
							}
						}
					}
				}
				foreach (TileZone item in new List<TileZone>(c.zones))
				{
					list.AddRange(item.tiles);
				}
				BuildingAsset pNewBuildingAsset = AssetManager.buildings.get("stockpile");
				foreach (WorldTile item2 in list)
				{
					if (item2 == null || item2.Type == null || !item2.Type.ground || item2.Type.block || (item2.building != null && AssetStyle.IsSolid(item2.building)) || !World.world.buildings.canBuildFrom(item2, pNewBuildingAsset, (City)null, BuildPlacingType.New, pFloraGrowth: false))
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding("stockpile", item2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						if (building.city != c)
						{
							c.listBuilding(building);
						}
						if (c.hasStorages())
						{
							Note($"a stockpile put up at {item2.x},{item2.y}");
							return true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Note("stockpile: " + ex.Message);
			}
			if (!c.hasStorages())
			{
				Note($"no stockpile could be put up for {c.name} ({c.zones.Count} zones, {c.buildings.Count} buildings)");
			}
			return c.hasStorages();
		}

		protected static void ClearBox(int x0, int y0, int x1, int y1)
		{
			for (int i = y0; i <= y1; i++)
			{
				for (int j = x0; j <= x1; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.building == null || tile.building.asset == null)
					{
						continue;
					}
					BuildingType building_type = tile.building.asset.building_type;
					if (building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Mineral || building_type == BuildingType.Building_Plant)
					{
						try
						{
							tile.building.removeBuildingFinal();
						}
						catch
						{
						}
					}
				}
			}
		}
	}
}
