using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HopsTest : MonoBehaviour
	{
		private sealed class Walked
		{
			public int Shivers;

			public int Pushes;

			public float Covered;

			public float WorstPush;

			public float Heading;

			public Vector2 End;
		}

		private WorldBoxMod _mod;

		private Actor _king;

		private static readonly string[] Jumpers = new string[16]
		{
			"human", "elf", "orc", "dwarf", "civ_rat", "civ_frog", "civ_cat", "fox", "cat", "rabbit",
			"chicken", "dog", "sheep", "cow", "bear", "crocodile"
		};

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
			Log.Info(string.Format("HOPS world {0}x{1}, models {2}", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing"));
			Building stock = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 10, 2);
				if (_king != null)
				{
					stock = BuildStockpile(_king.city);
					if (stock != null)
					{
						break;
					}
				}
			}
			if (_king == null || stock == null)
			{
				Log.Error("HOPS could not found a village with a stockpile");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("HOPS first person did not start");
				Finish(quit);
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
			if (!options.Contains("jumpsonly"))
			{
				yield return Run("stockpile", Stockpile(stock));
			}
			if (!options.Contains("walkonly"))
			{
				yield return Run("jumps", Jumping());
			}
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("HOPS --- " + name);
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
					Log.Error("HOPS " + name + " failed: " + ex);
				}
				break;
				IL_0074:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			PossessionHooks.ScriptedJump = false;
		}

		private static Building BuildStockpile(City city)
		{
			WorldTile tile = city.getTile();
			for (int i = 0; i < 2; i++)
			{
				for (int j = 0; j < 48; j++)
				{
					float f = (float)j * 0.39f;
					float num = 9f + (float)(j / 16) * 6f;
					WorldTile tile2 = World.world.GetTile((int)((float)tile.x + Mathf.Cos(f) * num), (int)((float)tile.y + Mathf.Sin(f) * num));
					if (tile2 == null || tile2.Type == null || tile2.Type.liquid || tile2.Type.block || (i == 0 && !OpenLand(tile2)))
					{
						continue;
					}
					try
					{
						Building building = World.world.buildings.addBuilding("stockpile", tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
						if (building == null)
						{
							continue;
						}
						building.completeConstruction();
						building.setKingdom(city.kingdom);
						city.listBuilding(building);
						Log.Info($"HOPS stockpile at {tile2.x},{tile2.y}");
						return building;
					}
					catch (Exception ex)
					{
						Log.Error("HOPS stockpile: " + ex.Message);
					}
				}
			}
			return null;
			bool OpenLand(WorldTile mid)
			{
				for (int k = mid.y - 8; k <= mid.y + 10; k++)
				{
					for (int l = mid.x - 8; l <= mid.x + 8; l++)
					{
						WorldTile tile3 = World.world.GetTile(l, k);
						if (tile3 == null || tile3.Type == null || tile3.Type.liquid || tile3.Type.block || !tile3.Type.ground)
						{
							return false;
						}
					}
				}
				return true;
			}
		}

		private IEnumerator Stockpile(Building stock)
		{
			Actor me = _king;
			Vector2 at = ((stock.current_tile != null) ? new Vector2((float)stock.current_tile.x + 0.5f, (float)stock.current_tile.y + 0.5f) : stock.current_position);
			me.current_position = at + new Vector2(0f, -5f);
			me.dirty_current_tile = true;
			yield return new WaitForSecondsRealtime(1f);
			Vector2 centre = at;
			float num = float.MaxValue;
			foreach (Collider item in Colliders.Near)
			{
				if (item.Shape != null)
				{
					float sqrMagnitude = (new Vector2(item.X, item.Y) - at).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						centre = new Vector2(item.X, item.Y);
					}
				}
			}
			if (num == float.MaxValue)
			{
				Log.Error("HOPS no model outline for the stockpile");
				yield break;
			}
			Log.Info($"HOPS stockpile drawn at {centre} (tile {at})");
			int totalShivers = 0;
			int totalPushes = 0;
			float worstPush = 0f;
			Walked result;
			for (int k = 0; k < 12; k++)
			{
				float a = (float)k * MathF.PI * 2f / 12f;
				for (int run = 0; run < 2; run++)
				{
					Vector2 vector = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5f;
					result = new Walked();
					yield return Walk(me, vector, centre, run == 1, 2.6f, result);
					totalShivers += result.Shivers;
					totalPushes += result.Pushes;
					worstPush = Mathf.Max(worstPush, result.WorstPush);
					Log.Info(string.Format("HOPS stockpile from {0} deg {1}: covered {2:F2} tiles, ended {3:F2} from the middle ", k * 30, (run == 1) ? "running" : "walking", result.Covered, (result.End - centre).magnitude) + $"(at {result.End - centre}, heading {result.Heading:F0} deg), shivers {result.Shivers}, pushed back {result.Pushes} times (worst {result.WorstPush:F3})");
				}
			}
			result = new Walked();
			yield return Walk(me, centre + new Vector2(0f, -4.5f), centre + new Vector2(0f, 3f), run: false, 4f, result);
			Log.Info($"HOPS stockpile in at the front: reached {(result.End - centre).y:F2} north of the middle, shivers {result.Shivers}, pushed back {result.Pushes} times");
			totalShivers += result.Shivers;
			totalPushes += result.Pushes;
			_mod.SaveFrame("hops_stockpile_inside.png");
			Walked along = new Walked();
			yield return Walk(me, me.current_position, me.current_position + new Vector2(4f, 0.6f), run: true, 2.5f, along);
			Log.Info($"HOPS stockpile along the back inside: covered {along.Covered:F2}, shivers {along.Shivers}, pushed back {along.Pushes} times");
			totalShivers += along.Shivers;
			Log.Info(string.Format(arg1: totalPushes + along.Pushes, format: "HOPS stockpile total: shivers {0}, push-backs {1} (worst {2:F3} tiles)", arg0: totalShivers, arg2: worstPush));
			if (totalShivers > 30)
			{
				Log.Error($"HOPS stockpile walking shivers ({totalShivers} reversals)");
			}
		}

		private IEnumerator Walk(Actor me, Vector2 from, Vector2 towards, bool run, float seconds, Walked result)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			me.current_position = from;
			me.dirty_current_tile = true;
			Vector2 d = towards - from;
			_mod.SetYaw(Mathf.Atan2(d.y, d.x));
			_mod.SetPitchDegrees(-5f);
			yield return new WaitForSecondsRealtime(0.3f);
			PossessionHooks.ScriptedRun = run;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			Vector2 last = me.current_position;
			Vector2 prevStep = Vector2.zero;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < seconds)
			{
				yield return null;
				Vector2 current_position = me.current_position;
				Vector2 vector = current_position - last;
				float num = Mathf.Max(0.004f, PossessionHooks.SpeedForTest(me) * Time.unscaledDeltaTime);
				if (prevStep.magnitude > 0.05f * num && vector.magnitude > 0.05f * num && Vector2.Dot(vector, prevStep) < -0.3f * vector.magnitude * prevStep.magnitude)
				{
					result.Shivers++;
				}
				float num2 = 0f - Vector2.Dot(vector, d.normalized);
				if (num2 > num * 0.5f)
				{
					result.Pushes++;
					result.WorstPush = Mathf.Max(result.WorstPush, num2);
				}
				result.Covered += vector.magnitude;
				prevStep = vector;
				last = current_position;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			result.End = me.current_position;
			result.Heading = PossessionHooks.Yaw * 57.29578f;
			yield return new WaitForSecondsRealtime(0.3f);
		}

		private IEnumerator Jumping()
		{
			List<WorldTile> spots = new List<WorldTile>();
			WorldTile current_tile = _king.current_tile;
			for (int i = 20; i < 120; i += 3)
			{
				if (spots.Count >= Jumpers.Length)
				{
					break;
				}
				for (int j = 0; j < 48; j++)
				{
					if (spots.Count >= Jumpers.Length)
					{
						break;
					}
					WorldTile tile = World.world.GetTile(current_tile.x + (int)(Mathf.Cos((float)j * 0.1309f) * (float)i), current_tile.y + (int)(Mathf.Sin((float)j * 0.1309f) * (float)i));
					if (tile == null || !Open(tile))
					{
						continue;
					}
					bool flag = true;
					foreach (WorldTile item in spots)
					{
						if (Mathf.Abs(item.x - tile.x) + Mathf.Abs(item.y - tile.y) < 8)
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						spots.Add(tile);
					}
				}
			}
			if (spots.Count < Jumpers.Length)
			{
				Log.Error("HOPS no open land to jump on");
				yield break;
			}
			for (int k = 0; k < Jumpers.Length; k++)
			{
				string id = Jumpers[k];
				WorldTile spot = spots[k];
				Actor body = null;
				if (id == "human")
				{
					body = _king;
				}
				else
				{
					try
					{
						body = World.world.units.spawnNewUnit(id, spot, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					}
					catch (Exception ex)
					{
						Log.Error("HOPS spawn " + id + ": " + ex.Message);
					}
				}
				if (body == null)
				{
					continue;
				}
				yield return new WaitForSecondsRealtime(0.4f);
				if (_mod.Host != body && !Possess.JumpTo(body))
				{
					Log.Error("HOPS could not become the " + id);
					continue;
				}
				yield return new WaitForSecondsRealtime(1.6f);
				if (_mod.Host != body)
				{
					Log.Error("HOPS could not become the " + id);
					continue;
				}
				try
				{
					body.current_position = new Vector2((float)spot.x + 0.5f, (float)spot.y + 0.5f);
					body.dirty_current_tile = true;
				}
				catch
				{
				}
				for (float w = 0f; w < 3f; w += 0.1f)
				{
					if (!body.under_forces && !(body.position_height > 0f))
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.1f);
				}
				yield return new WaitForSecondsRealtime(0.6f);
				Jumps.LastShare = -1f;
				float mass = Mathf.Max(0.01f, ((BaseSimObject)body).stats["mass"]);
				float gameForce = Mathf.Clamp(SimGlobals.m.gravity * 0.5f / Mathf.Max(0.001f, ((BaseSimObject)body).stats["mass_2"] * body.actor_scale), 1.5f, 2.5f);
				int steps;
				float gamePeak = Jumps.Peak(gameForce, mass, out steps);
				float measured = 0f;
				float airborne = 0f;
				_ = body.current_position;
				PossessionHooks.ScriptedJump = true;
				float t0 = Time.realtimeSinceStartup;
				bool saved = false;
				while (Time.realtimeSinceStartup - t0 < 2.2f)
				{
					yield return null;
					float position_height = body.position_height;
					if (position_height > 0.01f)
					{
						airborne += Time.unscaledDeltaTime;
					}
					measured = Mathf.Max(measured, position_height);
					if (!saved && measured > 0.2f && position_height < measured - 0.01f)
					{
						_mod.SaveFrame("hops_jump_" + id + ".png");
						saved = true;
					}
				}
				float num = Stature.Peek(body, 0f);
				Log.Info($"HOPS jump {id}: share {Jumps.LastShare:F2} of a human's, force {Jumps.LastForce:F2} (game {gameForce:F2}), " + $"expected {Jumps.LastPeak:F2} tiles, measured {measured:F2} (game alone {gamePeak:F2}), {airborne:F2} s in the air, " + string.Format("body {0:F2} tiles, mass {1:F1}, mass_2 {2:F1}, scale {3:F3}", num, mass, ((BaseSimObject)body).stats["mass_2"], body.actor_scale));
				if (Jumps.LastShare < 0f)
				{
					Log.Error("HOPS the jump as a " + id + " didn't go through the mod (" + (body.isAlive() ? "alive" : "dead") + ")");
				}
				else
				{
					if (measured < 0.05f && Jumps.LastPeak > 0f)
					{
						Log.Error("HOPS no jump as a " + id);
					}
					if (body.asset.is_humanoid && Jumps.LastShare > 1.35f)
					{
						Log.Error($"HOPS {id} jumps {Jumps.LastShare:F2} times as high as a human");
					}
				}
			}
			if (_king != null && _king.isAlive() && (_mod.Host == _king || Possess.JumpTo(_king)))
			{
				yield return new WaitForSecondsRealtime(1.6f);
				_mod.SetYaw(0f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				PossessionHooks.ScriptedRun = true;
				yield return new WaitForSecondsRealtime(1.2f);
				PossessionHooks.ScriptedJump = true;
				float t0 = 0f;
				float airborne = Time.realtimeSinceStartup;
				while (Time.realtimeSinceStartup - airborne < 1.4f)
				{
					yield return null;
					t0 = Mathf.Max(t0, _king.position_height);
				}
				Log.Info($"HOPS human jump at a sprint: peak {t0:F2}");
			}
		}

		private static WorldTile OpenLand(Actor me, int radius, int first = 4)
		{
			WorldTile current_tile = me.current_tile;
			for (int i = first; i < radius; i += 2)
			{
				for (int j = 0; j < 16; j++)
				{
					WorldTile tile = World.world.GetTile(current_tile.x + (int)(Mathf.Cos((float)j * 0.39f) * (float)i), current_tile.y + (int)(Mathf.Sin((float)j * 0.39f) * (float)i));
					if (tile != null && Open(tile))
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static bool Open(WorldTile t)
		{
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					WorldTile tile = World.world.GetTile(t.x + j, t.y + i);
					if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || (tile.building != null && AssetStyle.IsSolid(tile.building)))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("HOPS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
