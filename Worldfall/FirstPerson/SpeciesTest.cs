using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SpeciesTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private static readonly string[] Civic = new string[6] { "hall_civ_{0}_0", "windmill_civ_{0}_0", "watch_tower_civ_{0}", "temple_civ_{0}", "library_civ_{0}", "barracks_civ_{0}" };

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
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
			_mod = WorldBoxMod.Instance;
			List<string> sets = new List<string>(Models.SpeciesPacks);
			sets.Sort();
			Log.Info(string.Format("SPECIES world {0}x{1}, models {2}, packs: {3}", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing", string.Join(", ", sets)));
			foreach (string item in sets)
			{
				double ms;
				Dictionary<string, MeshModel> dictionary = Models.LoadSpeciesNow(item, out ms);
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				if (dictionary != null)
				{
					foreach (string key in dictionary.Keys)
					{
						if (key.StartsWith("ruin_", StringComparison.Ordinal))
						{
							num3++;
						}
						else if (key.EndsWith("_m", StringComparison.Ordinal) || key.EndsWith("_f", StringComparison.Ordinal))
						{
							num2++;
						}
						else
						{
							num++;
						}
					}
				}
				Log.Info($"SPECIES pack {item}: {dictionary?.Count ?? 0} models ({num} near, {num2} distance versions, {num3} ruins) loaded in {ms:F0} ms");
			}
			foreach (WorldTile item2 in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item2, "human", 4, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("SPECIES could not found a kingdom");
				Application.Quit();
				yield break;
			}
			try
			{
				((BaseSimObject)_king).addStatusEffect("invincible", 3600f, pColorEffect: false);
			}
			catch
			{
			}
			long kingId = _king.data.id;
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SPECIES first person did not start");
				Application.Quit();
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			List<Vector2> taken = new List<Vector2>();
			Vector2? shared = null;
			List<string> towns = Models.TownsPacks();
			foreach (string item3 in sets)
			{
				if (towns.Contains(item3) || (options.Contains("only") && !options.Contains("only" + item3)))
				{
					continue;
				}
				bool flag2 = false;
				try
				{
					flag2 = _king.isAlive() && _king.data != null && _king.data.id == kingId && _king.city != null;
				}
				catch
				{
				}
				if (!flag2)
				{
					Log.Error("SPECIES the king died or lost his town before " + item3 + ": no town to build for");
					break;
				}
				Vector2? vector3;
				if (!options.Contains("spread"))
				{
					Vector2? vector = shared;
					if (!vector.HasValue)
					{
						Vector2? vector2;
						shared = (vector2 = Site(taken));
						vector3 = vector2;
					}
					else
					{
						vector3 = vector;
					}
				}
				else
				{
					vector3 = Site(taken);
				}
				Vector2? vector4 = vector3;
				if (!vector4.HasValue)
				{
					Log.Error("SPECIES no land left for " + item3);
					continue;
				}
				taken.Add(vector4.Value);
				yield return Run(item3, Town(item3, _king, vector4.Value, !options.Contains("noruin"), !options.Contains("norooms"), !options.Contains("noboats")));
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("SPECIES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("SPECIES --- " + name);
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
					goto IL_007b;
				}
				catch (Exception ex)
				{
					Log.Error("SPECIES " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			if (_mod.Interior.Inside)
			{
				_mod.LeaveRoomForTest();
			}
		}

		private Vector2? Site(List<Vector2> taken)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				Log.Error("SPECIES no terrain yet");
				return null;
			}
			Vector2? result = null;
			float num = float.MaxValue;
			for (int i = 14; i < terrainForTest.Height - 34; i += 2)
			{
				for (int j = 10; j < terrainForTest.Width - 50; j += 2)
				{
					bool flag = false;
					foreach (Vector2 item in taken)
					{
						if (Mathf.Abs(item.x - (float)j) < 58f && Mathf.Abs(item.y - (float)i) < 46f)
						{
							flag = true;
						}
					}
					if (flag)
					{
						continue;
					}
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					int num5 = 0;
					for (int k = -10; k <= 30; k += 2)
					{
						for (int l = -6; l <= 46; l += 2)
						{
							int num6 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num6] & 0xB) != 0)
							{
								num4++;
								continue;
							}
							float num7 = terrainForTest.Ground[num6];
							if (Mathf.Abs(num7 - terrainForTest.Ground[num6 + 2]) > 0.6f || Mathf.Abs(num7 - terrainForTest.Ground[num6 + 2 * terrainForTest.Width]) > 0.6f)
							{
								num5++;
							}
							num2 = Mathf.Min(num2, num7);
							num3 = Mathf.Max(num3, num7);
						}
					}
					if (!(num2 > num3))
					{
						float num8 = (float)num4 * 10f + (float)num5 * 3f + Mathf.Max(0f, num3 - num2 - 2f) * 4f;
						if (num8 < num)
						{
							num = num8;
							result = new Vector2(j, i);
						}
					}
				}
			}
			if (result.HasValue)
			{
				Log.Info($"SPECIES site {result.Value}: score {num:F1}");
			}
			return result;
		}

		private static void Clear(Vector2 site)
		{
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				float num = (float)item.current_tile.x - site.x;
				float num2 = (float)item.current_tile.y - site.y;
				if (num < -6f || num > 48f || num2 < -10f || num2 > 32f)
				{
					continue;
				}
				try
				{
					if (!item.isKingdomCiv())
					{
						item.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
		}

		private static Building Put(City city, string id, int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block)
			{
				return null;
			}
			if (city == null || !city.isAlive())
			{
				return null;
			}
			try
			{
				if (tile.building != null)
				{
					tile.building.startDestroyBuilding();
				}
				Building building = World.world.buildings.addBuilding(id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					return null;
				}
				building.completeConstruction();
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("SPECIES building " + id + ": " + ex.Message);
				return null;
			}
		}

		private static Vector2Int Near(int x, int y, string sp, int want)
		{
			for (int i = 0; i <= 6; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					int[] array = new int[3] { 0, -1, 1 };
					foreach (int num in array)
					{
						if (Models.ShapeAt(x + j, y + num, sp, 3) == want)
						{
							return new Vector2Int(x + j, y + num);
						}
					}
				}
			}
			return new Vector2Int(x, y);
		}

		private static WorldTile Shore(Vector2 from)
		{
			WorldTile result = null;
			float num = 1600f;
			for (int i = (int)from.y - 40; i <= (int)from.y + 40; i++)
			{
				for (int j = (int)from.x - 40; j <= (int)from.x + 40; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.building != null)
					{
						continue;
					}
					float num2 = ((float)j - from.x) * ((float)j - from.x) + ((float)i - from.y) * ((float)i - from.y);
					if (num2 >= num)
					{
						continue;
					}
					int num3 = 0;
					for (int k = 1; k <= 4; k++)
					{
						WorldTile tile2 = World.world.GetTile(j, i - k);
						if (tile2 != null && tile2.Type != null && tile2.Type.liquid && tile2.Type.ocean)
						{
							num3++;
						}
					}
					if (num3 >= 3)
					{
						result = tile;
						num = num2;
					}
				}
			}
			return result;
		}

		private static bool Wet(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile != null && tile.Type != null && tile.Type.liquid)
			{
				return tile.Type.ocean;
			}
			return false;
		}

		private static Vector2? Water(Vector2 p, Vector2? clearOf)
		{
			for (float num = 2f; num < 14f; num += 0.5f)
			{
				int num2 = Mathf.Max(8, (int)(num * 6f));
				for (int i = 0; i < num2; i++)
				{
					float f = (float)i * 2f * MathF.PI / (float)num2;
					Vector2 vector = p + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					bool flag = !DockDecks.On(vector) && (!clearOf.HasValue || (clearOf.Value - vector).magnitude > 6f);
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							if (!Wet((int)vector.x + k, (int)vector.y + j))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return vector;
					}
				}
			}
			return null;
		}

		private IEnumerator Boats(string sp, Actor king, Vector2 shore)
		{
			Vector2? a = Water(shore, null);
			Vector2? b = (a.HasValue ? Water(shore, a) : ((Vector2?)null));
			if (!a.HasValue)
			{
				Log.Error("SPECIES " + sp + ": no open water for the boats");
				yield break;
			}
			Actor transport = null;
			Actor trading = null;
			try
			{
				transport = World.world.units.createNewUnit("boat_transport_" + sp, World.world.GetTile((int)a.Value.x, (int)a.Value.y));
				if (b.HasValue)
				{
					trading = World.world.units.createNewUnit("boat_trading_" + sp, World.world.GetTile((int)b.Value.x, (int)b.Value.y));
				}
				if (king.kingdom != null)
				{
					transport?.setKingdom(king.kingdom);
					trading?.setKingdom(king.kingdom);
				}
			}
			catch (Exception ex)
			{
				Log.Error("SPECIES " + sp + " boats: " + ex.Message);
			}
			if (transport == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info("SPECIES " + sp + " boats: transport " + Models.BoatFamily(transport) + ((trading != null) ? (", trading " + Models.BoatFamily(trading)) : "") + ", cabin " + Models.CabinFor(Models.BoatFamily(transport)));
			Vector2 vector = (b.HasValue ? ((a.Value + b.Value) * 0.5f) : a.Value);
			Vector2 debugEye = shore + (shore - vector).normalized * 2f;
			_mod.DebugEyeSet = true;
			_mod.DebugEye = debugEye;
			_mod.SetYaw(Mathf.Atan2(vector.y - debugEye.y, vector.x - debugEye.x));
			_mod.SetPitchDegrees(-4f);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("sp_" + sp + "_boats.png");
			if (transport.isAlive())
			{
				_mod.DebugEyeSet = false;
				Possess.Swap(transport);
				yield return new WaitForSecondsRealtime(2f);
				Log.Info($"SPECIES {sp} at the helm: steering {BoatHelm.Steers(transport)}, hands {_mod.Renderer.OverlayMeshes.Count} models, prompt '{_mod.Prompt}'");
				_mod.SaveFrame("sp_" + sp + "_helm.png");
				Possess.Swap(king);
				yield return new WaitForSecondsRealtime(3f);
			}
			try
			{
				if (transport.isAlive())
				{
					transport.dieSimpleNone();
				}
				if (trading != null && trading.isAlive())
				{
					trading.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Town(string sp, Actor king, Vector2 site, bool ruin, bool rooms, bool boats)
		{
			City city = king.city;
			Clear(site);
			int sx = (int)site.x;
			int street = (int)site.y;
			List<(Building b, string family)> placed = new List<(Building, string)>();
			for (int i = 0; i < 8; i++)
			{
				Vector2Int vector2Int = Near(sx + 3 + i * 5, street + 5 + i % 2 * 7, sp, i % 3);
				Building building = Put(city, "house_civ_" + sp + "_0", vector2Int.x, vector2Int.y);
				if (building != null)
				{
					placed.Add((building, Models.BuildingFamily(building)));
				}
			}
			for (int j = 0; j < Civic.Length; j++)
			{
				Building building2 = Put(city, string.Format(Civic[j], sp), sx + 5 + j * 7, street - 7);
				if (building2 != null)
				{
					placed.Add((building2, Models.BuildingFamily(building2)));
				}
			}
			WorldTile shore = Shore(new Vector2(sx + 20, street));
			Building docks = ((shore != null) ? Put(city, "docks_civ_" + sp, shore.x, shore.y) : null);
			if (docks != null)
			{
				placed.Add((docks, Models.BuildingFamily(docks)));
			}
			Log.Info(string.Format("SPECIES {0} at {1}: placed {2}: {3}", sp, site, placed.Count, string.Join(", ", placed.ConvertAll(((Building b, string family) p) => p.b.asset.id + "=" + p.family))));
			int num = 0;
			foreach (var item3 in placed)
			{
				if (Species.FromFamily(item3.family) == Species.Alias(sp))
				{
					num++;
				}
			}
			Log.Info(string.Format("SPECIES {0}: {1} of {2} drawn by the set's own models{3}", sp, num, placed.Count, (docks == null) ? " (no shore for the docks)" : ""));
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			try
			{
				king.setCurrentTilePosition(World.world.GetTile(sx - 3, street));
			}
			catch
			{
			}
			_mod.DebugEyeSet = true;
			_mod.DebugEye = new Vector2((float)sx - 4.5f, (float)street + 0.5f);
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(3f);
			_mod.SaveFrame("sp_" + sp + "_street.png");
			_mod.DebugEye = new Vector2((float)sx - 5f, (float)street - 2f);
			_mod.SetYaw(-0.12f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("sp_" + sp + "_civic.png");
			_mod.SceneForTest.DumpNear(new Vector2((float)sx + 22f, street), 40f);
			Log.Info($"SPECIES {sp} street render {_mod.RenderMs:F1} ms");
			foreach (var (building3, family) in placed)
			{
				if (building3 != null && building3.isAlive())
				{
					Vector2 vector = new Vector2((float)building3.current_tile.x + 0.5f, (float)building3.current_tile.y + 0.5f);
					bool flag = family.StartsWith("house", StringComparison.Ordinal);
					float num2 = (flag ? 6.5f : 10f);
					Vector2 debugEye = vector + new Vector2(num2 * 0.5f, 0f - num2);
					if (building3 == docks)
					{
						debugEye = vector + new Vector2(num2 * 0.9f, num2 * 0.2f);
					}
					_mod.DebugEye = debugEye;
					_mod.SetYaw(Mathf.Atan2(vector.y + 1f - debugEye.y, vector.x - debugEye.x));
					_mod.SetPitchDegrees(flag ? (-6f) : 4f);
					yield return new WaitForSecondsRealtime(0.8f);
					_mod.SaveFrame("sp_" + sp + "_" + family + ".png");
				}
			}
			SceneCollector.AllLit = true;
			s.TimeMode = 3;
			_mod.ForceRelight();
			_mod.DebugEye = new Vector2((float)sx - 4.5f, (float)street + 0.5f);
			_mod.SetYaw(0.05f);
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("sp_" + sp + "_night.png");
			_mod.DebugEye = new Vector2((float)sx - 5f, (float)street - 2f);
			_mod.SetYaw(-0.12f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("sp_" + sp + "_civic_night.png");
			SceneCollector.AllLit = false;
			s.TimeMode = 1;
			_mod.ForceRelight();
			if (boats && shore != null)
			{
				yield return Boats(sp, king, new Vector2((float)shore.x + 0.5f, (float)shore.y + 0.5f));
			}
			_mod.DebugEyeSet = true;
			if (rooms)
			{
				foreach (var (building4, family) in placed)
				{
					if (building4 != null && building4.isAlive() && family.StartsWith("house", StringComparison.Ordinal))
					{
						_mod.DebugEyeSet = false;
						_mod.EnterHouseForTest(building4);
						yield return new WaitForSecondsRealtime(1.2f);
						if (!_mod.Interior.Inside)
						{
							Log.Error("SPECIES could not enter " + family);
							break;
						}
						Interior room = _mod.Interior.Room;
						Log.Info($"SPECIES room in {family}: {room.Meshes.Count} models, {room.Lights.Count} lights, {room.MinX:F1}..{room.MaxX:F1} x {room.MinY:F1}..{room.MaxY:F1}");
						_mod.SetPitchDegrees(-4f);
						yield return new WaitForSecondsRealtime(0.5f);
						_mod.SaveFrame("sp_" + sp + "_room.png");
						_mod.LeaveRoomForTest();
						yield return new WaitForSecondsRealtime(0.6f);
						_mod.DebugEyeSet = true;
						break;
					}
				}
			}
			if (ruin)
			{
				int num3 = 0;
				foreach (var item4 in placed)
				{
					Building item = item4.b;
					try
					{
						if (item != null && item.isAlive())
						{
							item.makeRuins();
							num3++;
						}
					}
					catch (Exception ex)
					{
						Log.Error("SPECIES could not ruin " + item.asset.id + ": " + ex.Message);
					}
				}
				Log.Info($"SPECIES {sp}: {num3} ruined");
				_mod.DebugEye = new Vector2((float)sx - 4.5f, (float)street + 0.5f);
				_mod.SetYaw(0.05f);
				_mod.SetPitchDegrees(-2f);
				yield return new WaitForSecondsRealtime(2.5f);
				_mod.SaveFrame("sp_" + sp + "_ruins.png");
				_mod.DebugEye = new Vector2((float)sx - 5f, (float)street - 2f);
				_mod.SetYaw(-0.12f);
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame("sp_" + sp + "_civic_ruins.png");
				_mod.SceneForTest.DumpNear(new Vector2((float)sx + 22f, street), 40f);
			}
			_mod.DebugEyeSet = false;
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			foreach (var item5 in placed)
			{
				Building item2 = item5.b;
				try
				{
					if (item2 != null && item2.isAlive())
					{
						item2.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			try
			{
				World.world.cities.setDirtyBuildings(city);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(1f);
		}
	}
}
