using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BoatTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private Actor _galleon;

		private Actor _fishing;

		private Vector2 _galleonAt;

		private Vector2 _fishingAt;

		private bool _pin;

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			bool dryOnly = text.Contains("boatsdry");
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
			int num = 0;
			string[] array = new string[5] { "galleon", "cog", "barge", "raft", "fishing" };
			foreach (string text2 in array)
			{
				if (Models.Get("cabin_" + text2) != null)
				{
					num++;
				}
			}
			Log.Info(string.Format("BOATS world {0}x{1}, models {2}, {3} of 5 cabins, ", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing", num) + string.Format("wheel on the galleon {0}, docks walkable {1}", Models.Get("boat_galleon")?.PartIndex("wheel") ?? (-1), WalkSurface.Of(Models.Get("docks")) != null));
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 3, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("BOATS could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BOATS first person did not start");
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
			StartCoroutine(Pin());
			yield return Run("harbour", Harbour());
			if (DockDecks.Count > 0 && dryOnly)
			{
				yield return Run("dry", Dry());
			}
			else if (DockDecks.Count > 0)
			{
				yield return Run("pier", Pier());
				yield return Run("dry", Dry());
				yield return Run("moorings", Moorings());
				yield return Run("aboard", Aboard());
				yield return Run("helm", Helm());
				yield return Run("sink", Sink());
			}
			if (!dryOnly)
			{
				yield return Run("mine", Mine());
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("BOATS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("BOATS --- " + name);
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
					goto IL_0074;
				}
				catch (Exception ex)
				{
					Log.Error("BOATS " + name + " failed: " + ex);
				}
				break;
				IL_0074:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
		}

		private IEnumerator Pin()
		{
			while (true)
			{
				if (_pin)
				{
					Hold(_galleon, _galleonAt);
					Hold(_fishing, _fishingAt);
				}
				yield return null;
			}
		}

		private static void Hold(Actor boat, Vector2 at)
		{
			try
			{
				if (boat != null && boat.isAlive() && !BoatHelm.Steers(boat))
				{
					boat.cancelAllBeh();
					boat.current_position = at;
					boat.dirty_current_tile = true;
				}
			}
			catch
			{
			}
		}

		private static WorldTile Tile(Vector2 p)
		{
			return World.world.GetTile((int)p.x, (int)p.y);
		}

		private static bool Wet(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile != null && tile.Type != null && tile.Type.liquid)
			{
				return !tile.Type.lava;
			}
			return false;
		}

		private static void Put(Actor a, Vector2 at)
		{
			WorldTile worldTile = Tile(at);
			if (worldTile != null)
			{
				a.spawnOn(worldTile, 0f);
			}
			a.current_position = at;
			a.dirty_current_tile = true;
		}

		private IEnumerator Harbour()
		{
			Vector2 current_position = _king.current_position;
			WorldTile worldTile = null;
			for (int i = 4; i < 120; i += 2)
			{
				if (worldTile != null)
				{
					break;
				}
				for (int j = 0; j < 64; j++)
				{
					if (worldTile != null)
					{
						break;
					}
					float f = (float)j / 64f * MathF.PI * 2f;
					int num = (int)(current_position.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(current_position.y + Mathf.Sin(f) * (float)i);
					if (num < 16 || num2 < 16 || num >= MapBox.width - 16 || num2 >= MapBox.height - 16)
					{
						continue;
					}
					bool flag = true;
					for (int k = 0; k <= 4 && flag; k++)
					{
						for (int l = -2; l <= 2 && flag; l++)
						{
							if (!Wet(num + l, num2 + k))
							{
								flag = false;
							}
						}
					}
					if (!flag)
					{
						continue;
					}
					for (int m = 0; m < 8; m++)
					{
						if (worldTile != null)
						{
							break;
						}
						float f2 = (float)m * MathF.PI / 4f;
						int x = num + Mathf.RoundToInt(Mathf.Cos(f2) * 4.5f);
						int y = num2 + 2 + Mathf.RoundToInt(Mathf.Sin(f2) * 4.5f);
						if (Wet(x, y))
						{
							continue;
						}
						bool flag2 = true;
						for (int n = 4; n <= 14 && flag2; n++)
						{
							if (!Wet(num + Mathf.RoundToInt((0f - Mathf.Cos(f2)) * (float)n), num2 + 2 + Mathf.RoundToInt((0f - Mathf.Sin(f2)) * (float)n)))
							{
								flag2 = false;
							}
						}
						if (flag2)
						{
							worldTile = World.world.GetTile(num, num2);
						}
					}
				}
			}
			if (worldTile == null)
			{
				Log.Error("BOATS no coast for a harbour");
				yield break;
			}
			Building dock = World.world.buildings.addBuilding("docks_human", worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			if (dock == null)
			{
				Log.Error("BOATS the harbour could not be built");
				yield break;
			}
			dock.completeConstruction();
			try
			{
				if (_king.kingdom != null)
				{
					dock.setKingdom(_king.kingdom);
				}
			}
			catch
			{
			}
			Log.Info($"BOATS harbour at {worldTile.x},{worldTile.y}");
			Vector2 shore = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			for (int num3 = 1; num3 < 12; num3++)
			{
				bool flag3 = false;
				for (int num4 = 0; num4 < 16; num4++)
				{
					if (flag3)
					{
						break;
					}
					float f3 = (float)num4 / 16f * MathF.PI * 2f;
					Vector2 vector = new Vector2((float)worldTile.x + 0.5f + Mathf.Cos(f3) * (float)num3, (float)worldTile.y + 2.5f + Mathf.Sin(f3) * (float)num3);
					WorldTile worldTile2 = Tile(vector);
					if (worldTile2 != null && worldTile2.Type != null && !worldTile2.Type.liquid && !worldTile2.Type.block)
					{
						shore = vector;
						flag3 = true;
					}
				}
				if (flag3)
				{
					break;
				}
			}
			Put(_king, shore);
			for (float t = 0f; t < 4f; t += 0.25f)
			{
				if (DockDecks.Count != 0)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			Log.Info($"BOATS king on the shore at {shore}, docks registered {DockDecks.Count}, harbour alive {dock.isAlive()}");
		}

		private IEnumerator Pier()
		{
			DockDecks.PointForTest(0, 0f, 1.8f, out var landing, out var pierHeading);
			DockDecks.PointForTest(0, 0f, -2f, out var mid, out var heading);
			Put(_king, mid);
			_mod.SetYaw(pierHeading);
			_mod.SetPitchDegrees(-4f);
			yield return new WaitForSecondsRealtime(1.2f);
			DockDecks.HeightAt(mid.x, mid.y, out var z);
			Log.Info($"BOATS on the pier at {_king.current_position}: deck {z:F2}, eye {_mod.EyeForTest.z:F2} (eye over deck {_mod.EyeForTest.z - z:F2}), " + $"in the water (game) {_king.isAffectedByLiquid()}, swimming {_mod.Swimming}, on deck {DockDecks.On(_king.current_position)}");
			_mod.SaveFrame("boats_pier");
			Vector2 start = _king.current_position;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (int k = 0; k < 6; k++)
			{
				yield return new WaitForSecondsRealtime(0.5f);
				Log.Info($"BOATS walking: {(_king.current_position - start).magnitude:F2} tiles, swimming {_mod.Swimming}, wet {_king.isAffectedByLiquid()}, on deck {DockDecks.On(_king.current_position)}, eye {_mod.EyeForTest.z:F2}");
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.SaveFrame("boats_pier_end");
			DockDecks.PointForTest(0, -2.6f, -5.1f, out var at, out heading);
			Actor npc = null;
			try
			{
				npc = World.world.units.spawnNewUnit("human", Tile(at), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("BOATS spawn: " + ex.Message);
			}
			if (npc != null)
			{
				Put(npc, at);
			}
			_mod.SetYaw(Mathf.Atan2(at.y - _king.current_position.y, at.x - _king.current_position.x));
			yield return new WaitForSecondsRealtime(1.2f);
			if (npc != null)
			{
				Log.Info($"BOATS someone on the T-head: in the water (game) {npc.isAffectedByLiquid()}, drawn {_mod.SceneForTest.Tagged.Contains(npc)}, at {npc.current_position}");
				_mod.SaveFrame("boats_pier_npc");
			}
			_mod.SetYaw(pierHeading + MathF.PI);
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.SaveFrame("boats_pier_back");
			Log.Info($"BOATS landing at {landing}, pier towards {pierHeading * 57.29578f:F0} degrees");
		}

		private IEnumerator Dry()
		{
			Log.Info($"BOATS dry: tile step taken over {DryBoards.Installed}");
			DockDecks.PointForTest(0, 0f, -2f, out var mid, out var heading);
			DockDecks.PointForTest(0, -2.6f, -5.1f, out var head, out heading);
			yield return Soak("you on the pier", _king, mid, dry: true);
			DryBoards.Enabled = false;
			yield return Soak("you on the pier, the game's step", _king, mid, dry: false);
			DryBoards.Enabled = true;
			Vector2? vector = Water(mid, null);
			if (vector.HasValue)
			{
				yield return Soak("you in the water beside it", _king, vector.Value, dry: false);
			}
			else
			{
				Log.Error("BOATS dry: no open water beside the pier");
			}
			Actor actor = null;
			try
			{
				actor = World.world.units.spawnNewUnit("human", Tile(head), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("BOATS spawn: " + ex.Message);
			}
			if (actor != null)
			{
				yield return Soak("someone on the T-head", actor, head, dry: true);
			}
			Put(_king, mid);
			Refresh(_king);
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Soak(string label, Actor a, Vector2 at, bool dry)
		{
			Put(a, at);
			Refresh(a);
			yield return new WaitForSecondsRealtime(0.3f);
			bool deck = DockDecks.On(a.current_position);
			int steps0 = DryBoards.StepsForTest;
			int s0 = a.getStamina();
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				Stand(a, at);
				yield return null;
			}
			int s1 = a.getStamina();
			a.setStamina(0);
			int h0 = a.getHealth();
			bool drowning = false;
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				Stand(a, at);
				drowning |= ((BaseSimObject)a).hasStatus("drowning");
				yield return null;
			}
			int health = a.getHealth();
			bool flag = s1 >= s0 && !drowning && health >= h0;
			bool flag2 = s1 < s0 && drowning;
			bool flag3 = (dry ? flag : flag2);
			Log.Info(string.Format("BOATS dry {0} {1}: on the boards {2}, stamina {3} -> {4} in 2 s, ", flag3 ? "PASS" : "FAIL", label, deck, s0, s1) + string.Format("with none left drowning {0}, health {1} -> {2} in 3 s, board steps {3} (expected {4})", drowning, h0, health, DryBoards.StepsForTest - steps0, dry ? "dry" : "swimming"));
			Refresh(a);
		}

		private static void Stand(Actor a, Vector2 at)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					if (a != ControllableUnit._unit_main)
					{
						a.cancelAllBeh();
					}
					a.current_position = at;
					a.dirty_current_tile = true;
				}
			}
			catch
			{
			}
		}

		private static void Refresh(Actor a)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					if (((BaseSimObject)a).hasStatus("drowning"))
					{
						a.finishStatusEffect("drowning");
					}
					a.restoreHealth(a.getMaxHealth());
					a.setStamina(a.getMaxStamina());
				}
			}
			catch
			{
			}
		}

		private IEnumerator Moorings()
		{
			Building building = DockDecks.BuildingForTest(0);
			WorldTile worldTile = null;
			try
			{
				building.component_docks.recalculateOceanTiles();
				foreach (ref WorldTile item in building.component_docks.tiles_ocean)
				{
					WorldTile current = item;
					if (DockDecks.On(new Vector2(current.posV3.x, current.posV3.y)) || worldTile == null)
					{
						worldTile = current;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("BOATS berths: " + ex.Message);
			}
			if (worldTile == null)
			{
				Log.Error("BOATS the harbour has no berths");
				yield break;
			}
			Actor cog = World.world.units.createNewUnit("boat_trading_human", worldTile);
			if (cog == null)
			{
				Log.Error("BOATS no cog");
				yield break;
			}
			Vector2 at = cog.current_position;
			Log.Info($"BOATS berth at {at}: under the boards {DockDecks.On(at)}");
			if (DockDecks.PointForTest(0, 0f, -3f, out var at2, out var _))
			{
				Put(_king, at2);
			}
			_mod.SetYaw(Mathf.Atan2(at.y - _king.current_position.y, at.x - _king.current_position.x));
			_mod.SetPitchDegrees(-8f);
			for (int k = 0; k < 30; k++)
			{
				Hold(cog, at);
				yield return new WaitForSecondsRealtime(0.1f);
			}
			Log.Info($"BOATS tied up: drawn {Creatures.MooringOffset(cog).magnitude:F2} tiles off her berth, clear of the boards {Creatures.ClearForTest(cog)}");
			_mod.SaveFrame("boats_moored");
			try
			{
				cog.dieSimpleNone();
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(0.3f);
		}

		private IEnumerator Aboard()
		{
			DockDecks.PointForTest(0, 3.2f, -8.6f, out var at, out var _);
			Vector2? vector = Water(at, null);
			if (!vector.HasValue)
			{
				Log.Error("BOATS no open water off the pier's end");
				yield break;
			}
			_galleonAt = vector.Value;
			try
			{
				_galleon = World.world.units.createNewUnit("boat_transport_human", Tile(_galleonAt));
				if (_king.kingdom != null)
				{
					_galleon?.setKingdom(_king.kingdom);
				}
			}
			catch (Exception ex)
			{
				Log.Error("BOATS boats: " + ex.Message);
			}
			if (_galleon == null)
			{
				Log.Error("BOATS no galleon");
				yield break;
			}
			_pin = true;
			Log.Info($"BOATS galleon at {_galleonAt} ({(_galleonAt - at).magnitude:F1} off the pier's end), on water {Wet((int)_galleonAt.x, (int)_galleonAt.y)}");
			Boat hold = _galleon.getSimpleComponent<Boat>();
			for (int i = 0; i < 2; i++)
			{
				try
				{
					Actor actor = World.world.units.spawnNewUnit("human", _king.current_tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					if (actor != null && hold != null)
					{
						actor.embarkInto(hold);
					}
				}
				catch (Exception ex2)
				{
					Log.Error("BOATS passenger: " + ex2.Message);
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 at2 = _galleonAt;
			float num = float.MaxValue;
			for (float num2 = -4.4f; num2 <= 4.4f; num2 += 0.4f)
			{
				for (float num3 = -8.4f; num3 <= 2.4f; num3 += 0.4f)
				{
					if (DockDecks.PointForTest(0, num2, num3, out var at3, out var _) && DockDecks.On(at3))
					{
						float num4 = Boats.HullDistance(_galleon, at3);
						if (num4 > 0.3f && num4 < num)
						{
							num = num4;
							at2 = at3;
						}
					}
				}
			}
			Put(_king, at2);
			_mod.SetYaw(Mathf.Atan2(_galleonAt.y - at2.y, _galleonAt.x - at2.x));
			_mod.SetPitchDegrees(-6f);
			yield return new WaitForSecondsRealtime(1.5f);
			float num5 = Boats.HullDistance(_galleon, _king.current_position);
			Log.Info($"BOATS galleon at {_galleon.current_position} ({Models.BoatFamily(_galleon)}), {num5:F2} tiles from her hull, can board {Boats.CanBoard(_king, _galleon, _king.current_position)}, " + $"under the crosshair {_mod.Picked == _galleon}, prompt '{_mod.Prompt}', passengers {hold?.countPassengers()}");
			_mod.SaveFrame("boats_galleon_alongside");
			_mod.EnterCabin(_galleon);
			yield return new WaitForSecondsRealtime(1.5f);
			BoatCabin cab = _mod.Cabin;
			Log.Info($"BOATS moored off the dock by {Creatures.MooringOffset(_galleon).magnitude:F2} tiles, could step ashore {Boats.CanGoAshore(_galleon)}");
			Log.Info($"BOATS aboard: cabin {cab.Inside} ({cab.Name}), king inside the boat {_king.is_inside_boat}, frozen {PossessionHooks.Frozen}, " + $"windows showing {_mod.Renderer.PortalPixels} pixels, people aboard drawn {cab.WalkersForTest.Count}, prompt '{_mod.Prompt}'");
			_mod.SaveFrame("boats_cabin_door");
			(string, float)[] array = new(string, float)[3]
			{
				("port", 0f),
				("starboard", 180f),
				("stern", 90f)
			};
			for (int j = 0; j < array.Length; j++)
			{
				var (tag, num6) = array[j];
				_mod.SetYaw(num6 * (MathF.PI / 180f));
				_mod.SetPitchDegrees(0f);
				yield return new WaitForSecondsRealtime(0.8f);
				Log.Info($"BOATS cabin looking {tag}: windows showing {_mod.Renderer.PortalPixels} pixels, render {_mod.RenderMs:F1} ms");
				_mod.SaveFrame("boats_cabin_" + tag);
			}
			Vector2 was = cab.Eye;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(1f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			Log.Info($"BOATS walked {(cab.Eye - was).magnitude:F2} tiles across the cabin to {cab.Eye}");
			_mod.SetYaw(0f);
			float heading3 = cab.Heading;
			Vector2 from = _galleonAt;
			for (int j = 0; j < 30; j++)
			{
				_galleonAt = from + new Vector2(Mathf.Cos(heading3), Mathf.Sin(heading3)) * ((float)j * 0.05f);
				if (!Wet((int)_galleonAt.x, (int)_galleonAt.y))
				{
					break;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Log.Info($"BOATS under way: moved {(_galleon.current_position - from).magnitude:F2} tiles, king with her {_king.current_tile == _galleon.current_tile}");
			_mod.SaveFrame("boats_cabin_underway");
			_galleonAt = from;
			yield return new WaitForSecondsRealtime(0.5f);
			cab.Eye = new Vector2(cab.Room.DoorX, cab.Room.DoorY);
			yield return new WaitForSecondsRealtime(0.4f);
			Log.Info("BOATS at the door: prompt '" + _mod.Prompt + "'");
			_mod.LeaveCabinForTest();
			yield return new WaitForSecondsRealtime(1.2f);
			Log.Info($"BOATS ashore: cabin {cab.Inside}, king inside the boat {_king.is_inside_boat}, at {_king.current_position}, on a dock {DockDecks.On(_king.current_position)}, " + $"swimming {_mod.Swimming}, frozen {PossessionHooks.Frozen}, {Boats.HullDistance(_galleon, _king.current_position):F2} from her hull");
			_mod.SaveFrame("boats_ashore");
		}

		private static Vector2? Water(Vector2 p, Actor clearOf)
		{
			for (float num = 0f; num < 9f; num += 0.5f)
			{
				int num2 = ((num == 0f) ? 1 : Mathf.Max(8, (int)(num * 6f)));
				for (int i = 0; i < num2; i++)
				{
					float f = (float)i * 2f * MathF.PI / (float)num2;
					Vector2 vector = p + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					bool flag = !DockDecks.On(vector) && (clearOf == null || (clearOf.current_position - vector).magnitude > 4f);
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							if (!Wet((int)vector.x + k, (int)vector.y + j) || DockDecks.On(vector + new Vector2(k, j)))
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

		private IEnumerator Helm()
		{
			if (_galleon == null || !_galleon.isAlive())
			{
				yield break;
			}
			Possess.Swap(_galleon);
			yield return new WaitForSecondsRealtime(2f);
			Vector3 eyeForTest = _mod.EyeForTest;
			Log.Info($"BOATS at the helm: steering {BoatHelm.Steers(_galleon)}, host is the galleon {_mod.Host == _galleon}, eye {eyeForTest} over the water {eyeForTest.z - 0f:F2}, " + $"{(new Vector2(eyeForTest.x, eyeForTest.y) - _galleon.current_position).magnitude:F2} from her middle, hands {_mod.Renderer.OverlayMeshes.Count} models, prompt '{_mod.Prompt}'");
			_mod.SaveFrame("boats_helm");
			Vector2 current_position = _galleon.current_position;
			float num = BoatHelm.Heading;
			float num2 = 0f;
			for (int i = 0; i < 32; i++)
			{
				float num3 = (float)i * MathF.PI / 16f;
				float num4 = 0f;
				for (float num5 = 1f; num5 < 30f; num5 += 0.5f)
				{
					Vector2 p = current_position + new Vector2(Mathf.Cos(num3), Mathf.Sin(num3)) * num5;
					if (!Wet((int)p.x, (int)p.y) || DockDecks.On(p))
					{
						break;
					}
					num4 = num5;
				}
				if (num4 > num2)
				{
					num2 = num4;
					num = num3;
				}
			}
			BoatHelm.Heading = num;
			Log.Info($"BOATS open water towards {num * 57.29578f:F0} degrees for {num2:F1} tiles");
			yield return new WaitForSecondsRealtime(0.3f);
			Vector2 start = _galleon.current_position;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (int k = 0; k < 6; k++)
			{
				yield return new WaitForSecondsRealtime(1f);
				Vector2 vector = _galleon.current_position - start;
				Log.Info($"BOATS sailing: sails {BoatHelm.Sails:F2}, way {BoatHelm.Way:F2}, made {vector.magnitude:F2} tiles towards {Mathf.Atan2(vector.y, vector.x) * 57.29578f:F0} (heading {BoatHelm.Heading * 57.29578f:F0}), bump {BoatHelm.Bump:F2}");
			}
			_mod.SaveFrame("boats_helm_sailing");
			float before = BoatHelm.Heading;
			PossessionHooks.ScriptedInput = new Vector2(1f, 0f);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("boats_helm_turning");
			yield return new WaitForSecondsRealtime(1.3f);
			Log.Info($"BOATS turning: wheel {BoatHelm.Wheel:F2}, heading {before * 57.29578f:F0} -> {BoatHelm.Heading * 57.29578f:F0}, way {BoatHelm.Way:F2}");
			PossessionHooks.ScriptedInput = Vector2.zero;
			Vector2 current_position2 = _galleon.current_position;
			float heading = BoatHelm.Heading;
			for (int j = 0; j < 32; j++)
			{
				float num6 = (float)j * MathF.PI / 16f;
				bool flag = false;
				for (float num7 = 2f; num7 < 20f; num7 += 1f)
				{
					if (flag)
					{
						break;
					}
					if (!Wet((int)(current_position2.x + Mathf.Cos(num6) * num7), (int)(current_position2.y + Mathf.Sin(num6) * num7)))
					{
						flag = true;
						heading = num6;
					}
				}
				if (flag)
				{
					break;
				}
			}
			BoatHelm.Heading = heading;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			PossessionHooks.ScriptedRun = true;
			float maxWay = 0f;
			for (int k = 0; k < 16; k++)
			{
				yield return new WaitForSecondsRealtime(0.5f);
				maxWay = Mathf.Max(maxWay, BoatHelm.Way);
				WorldTile current_tile = _galleon.current_tile;
				if (k % 4 == 3)
				{
					Log.Info($"BOATS at the shore: way {BoatHelm.Way:F2} (most {maxWay:F2}), bump {BoatHelm.Bump:F2}, on water {current_tile?.Type.liquid ?? false}, at {_galleon.current_position}");
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			_mod.SaveFrame("boats_helm_shore");
			float h = BoatHelm.Heading;
			_mod.SetYaw(h + MathF.PI);
			yield return new WaitForSecondsRealtime(0.6f);
			Log.Info($"BOATS looking aft: hands {_mod.Renderer.OverlayMeshes.Count} models");
			_mod.SaveFrame("boats_helm_aft");
			_mod.SetYaw(h + MathF.PI / 2f);
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.SaveFrame("boats_helm_port");
			_galleonAt = _galleon.current_position;
		}

		private IEnumerator Mine()
		{
			if (_king == null || !_king.isAlive())
			{
				yield break;
			}
			if (_mod.Host != _king)
			{
				Possess.Swap(_king);
				yield return new WaitForSecondsRealtime(1.5f);
			}
			Vector2 current_position = _king.current_position;
			WorldTile spot = null;
			for (int i = 6; i < 60; i += 2)
			{
				if (spot != null)
				{
					break;
				}
				for (int j = 0; j < 32; j++)
				{
					if (spot != null)
					{
						break;
					}
					float f = (float)j / 32f * MathF.PI * 2f;
					int num = (int)(current_position.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(current_position.y + Mathf.Sin(f) * (float)i);
					bool flag = true;
					for (int k = -4; k <= 5 && flag; k++)
					{
						for (int l = -4; l <= 4 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num + l, num2 + k);
							if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava || (tile.building != null && tile.building.isKingdomCiv()))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						spot = World.world.GetTile(num, num2);
					}
				}
			}
			if (spot == null)
			{
				Log.Error("BOATS no ground for a mine");
				yield break;
			}
			int num3 = 0;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int num4 = item.current_tile.x - spot.x;
				int num5 = item.current_tile.y - spot.y;
				if (num4 < -5 || num4 > 5 || num5 < -7 || num5 > 6)
				{
					continue;
				}
				try
				{
					if (!item.isKingdomCiv())
					{
						item.removeBuildingFinal();
						num3++;
					}
				}
				catch
				{
				}
			}
			Log.Info($"BOATS cleared {num3} plants and rocks for the mine");
			Building mine = World.world.buildings.addBuilding("mine", spot, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			if (mine == null)
			{
				Log.Error("BOATS the mine could not be built");
				yield break;
			}
			mine.completeConstruction();
			Put(_king, new Vector2((float)spot.x + 0.5f, (float)spot.y - 5.5f));
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(4f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("boats_mine_far");
			if (MineInterior.Mouth(mine, out var mouth))
			{
				Put(_king, mouth + new Vector2(0f, -1.4f));
			}
			_mod.SetPitchDegrees(2f);
			yield return new WaitForSecondsRealtime(1f);
			Log.Info($"BOATS mine at {spot.x},{spot.y}: enterable {MineInterior.IsEnterable(mine)}, under the crosshair {_mod.Picked == mine}, {MineInterior.MouthDistance(mine, _king.current_position):F2} from its mouth, prompt '{_mod.Prompt}'");
			_mod.SaveFrame("boats_mine_outside");
			Actor miner = null;
			try
			{
				miner = World.world.units.spawnNewUnit("human", spot, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
				if (miner != null)
				{
					miner.is_inside_building = true;
					miner.inside_building = mine;
				}
			}
			catch (Exception ex)
			{
				Log.Error("BOATS miner: " + ex.Message);
			}
			_mod.EnterMine(mine);
			yield return new WaitForSecondsRealtime(1.5f);
			MineInterior inside = _mod.Mine;
			Log.Info($"BOATS in the mine: inside {inside.Inside}, frozen {PossessionHooks.Frozen}, miners {inside.WalkersForTest.Count}, day through the mouth {_mod.Renderer.PortalPixels} pixels, prompt '{_mod.Prompt}'");
			_mod.SaveFrame("boats_mine_in");
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return new WaitForSecondsRealtime(2.5f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.SaveFrame("boats_mine_deep");
			_mod.SetYaw(-MathF.PI / 2f);
			yield return new WaitForSecondsRealtime(0.8f);
			Log.Info($"BOATS looking back at the mouth from {inside.Eye}: day {_mod.Renderer.PortalPixels} pixels, render {_mod.RenderMs:F1} ms");
			_mod.SaveFrame("boats_mine_back");
			inside.Eye = new Vector2(inside.Room.DoorX, inside.Room.DoorY);
			yield return new WaitForSecondsRealtime(0.4f);
			Log.Info("BOATS at the mouth: prompt '" + _mod.Prompt + "'");
			_mod.LeaveMineForTest();
			yield return new WaitForSecondsRealtime(1f);
			Log.Info($"BOATS out of the mine: inside {inside.Inside}, frozen {PossessionHooks.Frozen}");
			try
			{
				if (miner != null)
				{
					miner.is_inside_building = false;
					miner.inside_building = null;
				}
			}
			catch
			{
			}
			_mod.SetYaw(MathF.PI / 2f);
			try
			{
				mine.makeRuins();
			}
			catch (Exception ex2)
			{
				Log.Error("BOATS could not ruin the mine: " + ex2.Message);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Log.Info($"BOATS the mine fell in: ruin {mine.isRuin()}, enterable {MineInterior.IsEnterable(mine)}");
			_mod.SaveFrame("boats_mine_ruin");
		}

		private IEnumerator Sink()
		{
			if (_king == null || !_king.isAlive())
			{
				yield break;
			}
			Possess.Swap(_king);
			yield return new WaitForSecondsRealtime(1.5f);
			Log.Info($"BOATS back in the king: steering {BoatHelm.Boat != null}, host the king {_mod.Host == _king}");
			DockDecks.PointForTest(0, -3.2f, -8.6f, out var at, out var _);
			Vector2? vector = Water(at, _galleon);
			if (!vector.HasValue)
			{
				Log.Error("BOATS no water for the fishing boat");
				yield break;
			}
			_fishingAt = vector.Value;
			_fishing = World.world.units.createNewUnit("boat_fishing", Tile(_fishingAt));
			if (_fishing == null)
			{
				Log.Error("BOATS no fishing boat");
				yield break;
			}
			Put(_king, _fishingAt + (at - _fishingAt).normalized * Mathf.Min(1.2f, (at - _fishingAt).magnitude));
			yield return new WaitForSecondsRealtime(1f);
			Log.Info($"BOATS fishing boat at {_fishingAt}, the king {Boats.HullDistance(_fishing, _king.current_position):F2} from her hull");
			_mod.EnterCabin(_fishing);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SetYaw(0f);
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info($"BOATS in the cuddy: cabin {_mod.Cabin.Inside} ({_mod.Cabin.Name}), windows showing {_mod.Renderer.PortalPixels} pixels");
			_mod.SaveFrame("boats_cuddy");
			try
			{
				((BaseSimObject)_fishing).getHitFullHealth(AttackType.Divine);
			}
			catch (Exception ex)
			{
				Log.Error("BOATS sinking: " + ex.Message);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Log.Info($"BOATS she sank: king alive {_king.isAlive()}, inside a boat {_king.is_inside_boat}, cabin {_mod.Cabin.Inside}, swimming {_mod.Swimming}, at {_king.current_position}");
			_mod.SaveFrame("boats_sunk");
			_pin = false;
		}
	}
}
