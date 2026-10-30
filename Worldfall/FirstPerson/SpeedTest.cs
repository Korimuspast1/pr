using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class SpeedTest : MonoBehaviour
	{
		private sealed class Stats
		{
			public float Distance;

			public float Time;

			public float Expected;

			public float Game;

			public float Yours;

			public float Fps;

			public float RealTime;

			public float Slide;

			public float Steps;

			public float Stat;

			public float Stamina;

			public int Frames;

			public string Notes = "";

			public void Add(float d, float dt, float expected, float game, float yours, float realTime, float slide, int steps)
			{
				Distance += d;
				Time += dt;
				Expected += expected * dt;
				Game += game;
				Yours += yours;
				Fps += ((dt > 0f) ? (1f / dt) : 0f);
				RealTime += realTime;
				Slide += slide;
				Steps += steps;
				Frames++;
			}

			public override string ToString()
			{
				if (Frames == 0 || Time <= 0f)
				{
					return "no samples";
				}
				float num = Frames;
				float num2 = Distance / Time;
				float num3 = Expected / Time;
				return $"{num2:F2} t/s of {num3:F2} ({num2 / Mathf.Max(0.01f, num3) * 100f:F0}%), game {Game / num:F2} yours {Yours / num:F2}, stat {Stat / num:F1}, stamina {Stamina / num:F2}, fps {Fps / num:F0}, rt {RealTime / num:F2}, slide {Slide / num:F2}, steps/frame {Steps / num:F2} {Notes}";
			}
		}

		private WorldBoxMod _mod;

		private static readonly string[][] Surfaces = new string[36][]
		{
			new string[3] { "soil_low", "soil_low", null },
			new string[3] { "grass_low", "soil_low", "grass_low" },
			new string[3] { "grass_high", "soil_high", "grass_high" },
			new string[3] { "soil_high", "soil_high", null },
			new string[3] { "sand", "sand", null },
			new string[3] { "hills", "hills", null },
			new string[3] { "shallow_waters", "shallow_waters", null },
			new string[3] { "swamp_low", "soil_low", "swamp_low" },
			new string[3] { "swamp_high", "soil_high", "swamp_high" },
			new string[3] { "desert_low", "soil_low", "desert_low" },
			new string[3] { "frozen_low", "soil_low", "frozen_low" },
			new string[3] { "frozen_high", "soil_high", "frozen_high" },
			new string[3] { "permafrost_low", "soil_low", "permafrost_low" },
			new string[3] { "snow_sand", "sand", "snow_sand" },
			new string[3] { "snow_hills", "hills", "snow_hills" },
			new string[3] { "ice", "soil_low", "ice" },
			new string[3] { "jungle_low", "soil_low", "jungle_low" },
			new string[3] { "savanna_low", "soil_low", "savanna_low" },
			new string[3] { "enchanted_low", "soil_low", "enchanted_low" },
			new string[3] { "mushroom_low", "soil_low", "mushroom_low" },
			new string[3] { "corrupted_low", "soil_low", "corrupted_low" },
			new string[3] { "wasteland_low", "soil_low", "wasteland_low" },
			new string[3] { "infernal_low", "soil_low", "infernal_low" },
			new string[3] { "crystal_low", "soil_low", "crystal_low" },
			new string[3] { "candy_low", "soil_low", "candy_low" },
			new string[3] { "lemon_low", "soil_low", "lemon_low" },
			new string[3] { "birch_low", "soil_low", "birch_low" },
			new string[3] { "maple_low", "soil_low", "maple_low" },
			new string[3] { "rocklands_low", "soil_low", "rocklands_low" },
			new string[3] { "garlic_low", "soil_low", "garlic_low" },
			new string[3] { "flower_low", "soil_low", "flower_low" },
			new string[3] { "clover_low", "soil_low", "clover_low" },
			new string[3] { "tumor_low", "soil_low", "tumor_low" },
			new string[3] { "biomass_low", "soil_low", "biomass_low" },
			new string[3] { "road", "soil_low", "road" },
			new string[3] { "field", "soil_low", "field" }
		};

		private const int Length = 30;

		private const int Width = 3;

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
			bool flag2 = false;
			try
			{
				flag2 = WorldLawLibrary.world_law_entanglewood.isEnabled();
			}
			catch
			{
			}
			float num = 1f;
			try
			{
				num = Config.time_scale_asset.multiplier;
			}
			catch
			{
			}
			Log.Info($"SPEED fixed step {Time.fixedDeltaTime:F4}, time scale {num}, fps lock 30 {Config.fps_lock_30}, entanglewood law {flag2}");
			WorldTile strip = FindStrip();
			if (strip == null)
			{
				Log.Error("SPEED no open strip of land");
				Finish(quit);
				yield break;
			}
			Log.Info($"SPEED strip at {strip.x},{strip.y}");
			Actor me = null;
			try
			{
				me = World.world.units.spawnNewUnit("human", strip, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("SPEED spawn: " + ex.Message);
			}
			if (me == null)
			{
				Finish(quit);
				yield break;
			}
			try
			{
				((BaseSimObject)me).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			ControllableUnit.setControllableCreature(me);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SPEED first person did not start");
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
			if (options.Contains("rain"))
			{
				yield return Rain(me, strip);
			}
			string[][] surfaces = Surfaces;
			foreach (string[] s in surfaces)
			{
				if (options.Contains("rainonly"))
				{
					break;
				}
				if (!options.Contains("only:") || options.Contains(" " + s[0] + " ") || options.Contains(":" + s[0] + " "))
				{
					try
					{
						me.restoreStaminaPercent(1f);
					}
					catch
					{
					}
					if (!Paint(strip, s[1], s[2]))
					{
						Log.Error("SPEED could not paint " + s[0]);
						continue;
					}
					yield return new WaitForSecondsRealtime(0.3f);
					Stats walk = new Stats();
					Stats run = new Stats();
					yield return Pass(me, strip, running: false, walk);
					yield return Pass(me, strip, running: true, run);
					Log.Info($"SPEED {s[0],-15} walk {walk}  |  run {run}");
				}
			}
			if (!options.Contains("only:") && !options.Contains("rainonly"))
			{
				Dictionary<string, Stats> byGround = new Dictionary<string, Stats>();
				yield return Across(me, byGround);
				foreach (KeyValuePair<string, Stats> item in byGround)
				{
					if (item.Value.Time > 0.5f)
					{
						Log.Info($"SPEED across {item.Key,-24} {item.Value}");
					}
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			Finish(quit);
		}

		private IEnumerator Rain(Actor me, WorldTile strip)
		{
			Place(me, strip);
			_mod.SetYaw(0.6f);
			_mod.SetPitchDegrees(8f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return new WaitForSecondsRealtime(2f);
			yield return Frames("clear", 8f);
			Vector2 p = me.current_position;
			for (int i = 0; i < 3; i++)
			{
				WorldTile tile = World.world.GetTile((int)p.x + (i - 1) * 6, (int)p.y + ((i == 1) ? 4 : (-2)));
				try
				{
					EffectsLibrary.spawn("fx_cloud", tile, "cloud_rain");
				}
				catch (Exception ex)
				{
					Log.Error("SPEED cloud: " + ex.Message);
				}
			}
			yield return new WaitForSecondsRealtime(4f);
			yield return Frames("rain", 12f);
			TornadoEffect twister = null;
			try
			{
				twister = EffectsLibrary.spawnAtTile("fx_tornado", World.world.GetTile((int)(p.x + Mathf.Cos(0.6f) * 11f), (int)(p.y + Mathf.Sin(0.6f) * 11f)), 0.0833f) as TornadoEffect;
				if (twister != null)
				{
					twister.resizeTornado(0.5f);
				}
			}
			catch (Exception ex2)
			{
				Log.Error("SPEED tornado: " + ex2.Message);
			}
			yield return new WaitForSecondsRealtime(2f);
			yield return Frames("rain and tornado", 10f);
			if (twister != null && twister.gameObject.activeSelf)
			{
				Vector2 vector = (Vector2)twister.transform.position - me.current_position;
				Log.Info($"SPEED tornado {vector.magnitude:F1} tiles away");
				_mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
				_mod.SetPitchDegrees(-4f);
				yield return new WaitForSecondsRealtime(0.4f);
			}
			_mod.SaveFrame("speed_rain_tornado.png");
			try
			{
				if (twister != null && ((BaseEffect)twister).active)
				{
					twister.kill();
				}
			}
			catch (Exception ex3)
			{
				Log.Error("SPEED tornado kill: " + ex3.Message);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Place(me, strip);
			_mod.SetYaw(0.6f);
			_mod.SetPitchDegrees(8f);
			yield return new WaitForSecondsRealtime(1f);
			Vector2 at = me.current_position + new Vector2(Mathf.Cos(0.6f), Mathf.Sin(0.6f)) * 2.5f;
			string[] array = new string[8] { "fx_change_happiness_positive", "fx_change_happiness_negative", "fx_hmm", "fx_hearts", "fx_slash", "fx_hit", "fx_money_got_money", "fx_explosion_small" };
			foreach (string id in array)
			{
				yield return new WaitForSecondsRealtime(1.2f);
				try
				{
					EffectsLibrary.spawnAt(id, new Vector3(at.x, at.y, 0f), 1f);
				}
				catch (Exception ex4)
				{
					Log.Error("SPEED effect " + id + ": " + ex4.Message);
				}
				SceneCollector.EffectTrace = new List<string>();
				yield return new WaitForSecondsRealtime(0.15f);
				_mod.SaveFrame("speed_" + id + ".png");
				Log.Info("SPEED effect " + id + ": " + string.Join(", ", SceneCollector.EffectTrace.Distinct().Take(12)));
				SceneCollector.EffectTrace = null;
			}
			Vector2 current_position = me.current_position;
			for (int k = 1; k <= 3; k++)
			{
				WorldTile tile2 = World.world.GetTile((int)(current_position.x + Mathf.Cos(0.6f) * (float)k * 0.8f), (int)(current_position.y + Mathf.Sin(0.6f) * (float)k * 0.8f));
				try
				{
					if (tile2 != null)
					{
						World.world.drop_manager.spawn(tile2, "fire", 2f, -1f, -1L);
					}
				}
				catch (Exception ex5)
				{
					Log.Error("SPEED fire: " + ex5.Message);
				}
			}
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.SaveFrame("speed_fire_near.png");
		}

		private IEnumerator Frames(string label, float seconds)
		{
			double frame = 0.0;
			double render = 0.0;
			double weather = 0.0;
			double terrain = 0.0;
			double objects = 0.0;
			double post = 0.0;
			double models = 0.0;
			double shade = 0.0;
			double cloud = 0.0;
			double fall = 0.0;
			int n = 0;
			int drops = 0;
			int storms = 0;
			int billboards = 0;
			float worst = 0f;
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				yield return null;
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				FrameRenderer renderer = _mod.Renderer;
				frame += (double)unscaledDeltaTime * 1000.0;
				render += (double)_mod.RenderMs;
				weather += (double)renderer.WeatherMs;
				shade += (double)renderer.CloudShadowMs;
				cloud += (double)renderer.CloudMs;
				fall += (double)renderer.FallMs;
				terrain += (double)renderer.TerrainMs;
				objects += (double)renderer.ObjectsMs;
				post += (double)renderer.PostMs;
				models += (double)renderer.MeshMs;
				worst = Mathf.Max(worst, unscaledDeltaTime * 1000f);
				try
				{
					drops += World.world.drop_manager._drops.Count;
				}
				catch
				{
				}
				storms += _mod.SceneForTest.Weather.Storms.Count;
				billboards += _mod.BillboardCount;
				n++;
			}
			if (n != 0)
			{
				double num = 1.0 / (double)n;
				Log.Info($"SPEED rain {label}: {1000.0 / (frame * num):F0} fps (frame {frame * num:F1} ms, worst {worst:F0}), mod render {render * num:F1} ms (terrain {terrain * num:F1}, sprites {objects * num:F1}, models {models * num:F1}, " + $"light and finish {(post - models) * num:F1} of which weather {weather * num:F1}: cloud shadows {shade * num:F1}, clouds {cloud * num:F1}, rain {fall * num:F1}), game drops {(double)drops * num:F0}, clouds {(double)storms * num:F1}, billboards {(double)billboards * num:F0}");
			}
		}

		private IEnumerator Pass(Actor me, WorldTile strip, bool running, Stats stats)
		{
			Place(me, strip);
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(0f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return new WaitForSecondsRealtime(0.5f);
			PossessionHooks.ScriptedRun = running;
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			Vector2 last = me.current_position;
			float t = 0f;
			float end = (float)(strip.x + 30) - 2f;
			while (t < 9f && me.current_position.x < end)
			{
				PossessionHooks.YourStepsForTest = 0;
				yield return null;
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				t += unscaledDeltaTime;
				Vector2 current_position = me.current_position;
				float magnitude = (current_position - last).magnitude;
				last = current_position;
				if (t < 1.1f || magnitude > 2f)
				{
					continue;
				}
				stats.Add(magnitude, unscaledDeltaTime, Expected(me, running), me._current_combined_movement_speed, PossessionHooks.SpeedForTest(me), PossessionHooks.RealTimeForTest, PossessionHooks.SlideForTest, PossessionHooks.YourStepsForTest);
				try
				{
					stats.Stat += ((BaseSimObject)me).stats["speed"];
					stats.Stamina += me.getStaminaRatio();
				}
				catch
				{
				}
				if (stats.Frames != 5)
				{
					continue;
				}
				try
				{
					WorldTile current_tile = me.current_tile;
					TileTypeBase type = current_tile.Type;
					string[] obj2 = new string[8]
					{
						string.Format("[{0} walk {1} tag '{2}' liquid {3} in liquid {4} task {5}", type.id, type.walk_multiplier, type.ignore_walk_multiplier_if_tag, type.liquid, me._is_in_liquid, (me.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task != null) ? (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task.id + " x" + ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task.speed_multiplier) : "none"),
						" building ",
						null,
						null,
						null,
						null,
						null,
						null
					};
					object obj3;
					if (current_tile.building == null)
					{
						obj3 = "none";
					}
					else
					{
						string id = current_tile.building.asset.id;
						int flora_type = (int)current_tile.building.asset.flora_type;
						obj3 = id + " flora " + flora_type;
					}
					obj2[2] = (string)obj3;
					obj2[3] = " target ";
					obj2[4] = ((me.tile_target != null) ? (me.current_position - new Vector2(me.tile_target.posV3.x, me.tile_target.posV3.y)).magnitude.ToString("F1") : "none");
					obj2[5] = " status ";
					obj2[6] = Statuses(me);
					obj2[7] = "]";
					stats.Notes = string.Concat(obj2);
				}
				catch (Exception ex)
				{
					stats.Notes = "[" + ex.Message + "]";
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
		}

		private IEnumerator Across(Actor me, Dictionary<string, Stats> byGround)
		{
			bool[] array = new bool[2] { false, true };
			foreach (bool running in array)
			{
				_mod.SetYaw(running ? MathF.PI : 0f);
				PossessionHooks.ScriptedRun = running;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				Vector2 last = me.current_position;
				float t = 0f;
				while (t < 10f)
				{
					PossessionHooks.YourStepsForTest = 0;
					yield return null;
					float unscaledDeltaTime = Time.unscaledDeltaTime;
					t += unscaledDeltaTime;
					Vector2 current_position = me.current_position;
					float magnitude = (current_position - last).magnitude;
					last = current_position;
					if (t < 1.1f || magnitude > 2f)
					{
						continue;
					}
					string text = "?";
					try
					{
						WorldTile current_tile = me.current_tile;
						text = ((current_tile.Type != null) ? current_tile.Type.id : "?") + ((current_tile.building != null) ? ("+" + current_tile.building.asset.id) : "");
						if (text.Length > 40)
						{
							text = text.Substring(0, 40);
						}
					}
					catch
					{
					}
					string key = (running ? "run " : "walk ") + text;
					if (!byGround.TryGetValue(key, out var value))
					{
						value = (byGround[key] = new Stats());
					}
					value.Add(magnitude, unscaledDeltaTime, Expected(me, running), me._current_combined_movement_speed, PossessionHooks.SpeedForTest(me), PossessionHooks.RealTimeForTest, PossessionHooks.SlideForTest, PossessionHooks.YourStepsForTest);
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(0.4f);
			}
			PossessionHooks.ScriptedRun = false;
		}

		private static float Expected(Actor me, bool running)
		{
			float num = 1f;
			try
			{
				num = Config.time_scale_asset.multiplier;
			}
			catch
			{
			}
			float num2 = (running ? 1.35f : PossessionHooks.RunFactor);
			return PossessionHooks.SpeedForTest(me) * num * num2;
		}

		private static string Statuses(Actor me)
		{
			List<string> list = new List<string>();
			try
			{
				foreach (StatusAsset item in AssetManager.status.list)
				{
					if (item != null && ((BaseSimObject)me).hasStatus(item.id))
					{
						list.Add(item.id);
					}
				}
			}
			catch
			{
			}
			return string.Join(",", list);
		}

		private static void Place(Actor me, WorldTile strip)
		{
			try
			{
				me.cancelAllBeh();
				me.current_position = new Vector2((float)strip.x + 1.5f, (float)strip.y + 1.5f);
				me.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private static bool Paint(WorldTile strip, string main, string top)
		{
			TileType tileType = AssetManager.tiles.get(main);
			TopTileType topTileType = ((top != null) ? AssetManager.top_tiles.get(top) : null);
			if (tileType == null || (top != null && topTileType == null))
			{
				return false;
			}
			for (int i = strip.y; i < strip.y + 3; i++)
			{
				for (int j = strip.x; j < strip.x + 30; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null)
					{
						continue;
					}
					try
					{
						if (tile.building != null)
						{
							tile.building.startDestroyBuilding();
						}
						tile.setTileTypes(tileType, topTileType);
					}
					catch (Exception ex)
					{
						Log.Error("SPEED paint: " + ex.Message);
						return false;
					}
				}
			}
			return true;
		}

		private static WorldTile FindStrip()
		{
			int width = MapBox.width;
			int height = MapBox.height;
			for (int i = 0; i < Mathf.Max(width, height) / 2; i += 4)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j * MathF.PI / 8f;
					int num = width / 2 + (int)(Mathf.Cos(f) * (float)i) - 15;
					int num2 = height / 2 + (int)(Mathf.Sin(f) * (float)i);
					if (num < 4 || num2 < 4 || num + 30 + 4 > width || num2 + 3 + 4 > height)
					{
						continue;
					}
					bool flag = true;
					for (int k = num2 - 1; k < num2 + 3 + 1 && flag; k++)
					{
						for (int l = num; l < num + 30 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(l, k);
							flag = tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block && tile.zone != null && tile.zone.city == null;
						}
					}
					if (flag)
					{
						return World.world.GetTile(num, num2);
					}
				}
			}
			return null;
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
			Log.Info("SPEED DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
