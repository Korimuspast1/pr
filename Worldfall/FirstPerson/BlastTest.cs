using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Gpu;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BlastTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private string _options;

		private bool Wants(string part)
		{
			if (_options.Contains("only"))
			{
				return _options.Contains("only" + part);
			}
			return true;
		}

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			_options = File.ReadAllText(path);
			bool quit = _options.Contains("quit");
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
			string[] array = new string[10] { "fx_mush_cap", "fx_mush_stem", "fx_fireball", "meteor_rock", "meteor_chunk_a", "god_hand", "held_blaster", "nature_volcano", "nature_geyser", "nature_geyser_acid" };
			int num = 0;
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (Models.Get(text) != null)
				{
					num++;
				}
				else
				{
					Log.Error("BLAST missing model " + text);
				}
			}
			Log.Info($"BLAST world {MapBox.width}x{MapBox.height}, models {num} of {array.Length}");
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
				Log.Error("BLAST could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BLAST first person did not start");
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
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			bool savedGpu = s.GpuRender;
			s.TimeMode = 1;
			if (_options.Contains("cpu"))
			{
				s.GpuRender = false;
			}
			GpuSprites.GlowsLastForTest = _options.Contains("oldglows");
			_mod.ForceRelight();
			WorldAgeAsset savedAge = null;
			if (_options.Contains("ashair"))
			{
				try
				{
					savedAge = World.world.era_manager.getCurrentAge();
					World.world.era_manager.setCurrentAge(AssetManager.era_library.get("age_ash"), pOverrideTime: true);
				}
				catch (Exception ex)
				{
					Log.Error("BLAST age: " + ex.Message);
				}
				yield return null;
				yield return null;
				_mod.Day.FinishAgeChange();
				yield return new WaitForSecondsRealtime(1f);
				Log.Info("BLAST in the Age of Ash's air");
			}
			if (Wants("blaster"))
			{
				yield return Run("blaster", Blaster());
			}
			if (Wants("vents"))
			{
				yield return Run("vents", Vents());
			}
			if (Wants("finger"))
			{
				yield return Run("finger", Finger());
			}
			if (Wants("grab"))
			{
				yield return Run("grab", Grab());
			}
			if (Wants("meteor"))
			{
				yield return Run("meteor", Meteor());
			}
			if (Wants("heatray"))
			{
				yield return Run("heatray", HeatRay());
			}
			if (Wants("crab"))
			{
				yield return Run("crab", CrabBomb());
			}
			if (Wants("bowling"))
			{
				yield return Run("bowling", Bowling());
			}
			if (Wants("nuke"))
			{
				yield return Run("nuke", Nuke());
			}
			s.TimeMode = savedMode;
			s.GpuRender = savedGpu;
			GpuSprites.GlowsLastForTest = false;
			if (savedAge != null)
			{
				try
				{
					World.world.era_manager.setCurrentAge(savedAge, pOverrideTime: true);
				}
				catch
				{
				}
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("BLAST DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("BLAST --- " + name);
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
					Log.Error("BLAST " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			_mod.SetPitchDegrees(0f);
		}

		private bool Site(float room, float watch, out Vector2 site, out Vector2 eye)
		{
			site = (eye = Vector2.zero);
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				return false;
			}
			float num = float.MaxValue;
			bool flag = false;
			Vector2 current_position = _king.current_position;
			for (int i = (int)room + 4; (float)i < (float)terrainForTest.Height - room - 4f; i += 4)
			{
				for (int j = (int)room + 4; (float)j < (float)terrainForTest.Width - room - 4f; j += 4)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -(int)room; (float)k <= room; k += 3)
					{
						for (int l = -(int)room; (float)l <= room; l += 3)
						{
							int num5 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num5] & 3) != 0)
							{
								num4++;
								continue;
							}
							num2 = Mathf.Min(num2, terrainForTest.Ground[num5]);
							num3 = Mathf.Max(num3, terrainForTest.Ground[num5]);
						}
					}
					if (num2 > num3 || (new Vector2(j, i) - current_position).magnitude < room + 45f)
					{
						continue;
					}
					for (int m = 0; m < 4; m++)
					{
						float f = (float)m * MathF.PI * 0.5f - MathF.PI / 2f;
						float num6 = (float)j + Mathf.Cos(f) * watch;
						float num7 = (float)i + Mathf.Sin(f) * watch;
						if (num6 < 4f || num7 < 4f || num6 > (float)(terrainForTest.Width - 5) || num7 > (float)(terrainForTest.Height - 5))
						{
							continue;
						}
						int num8 = (int)num7 * terrainForTest.Width + (int)num6;
						if ((terrainForTest.Flags[num8] & 3) == 0)
						{
							float num9 = Mathf.Abs(terrainForTest.Ground[num8] - (num2 + num3) * 0.5f);
							float num10 = (float)num4 * 4f + (num3 - num2) + num9 * 0.6f + (new Vector2(j, i) - current_position).magnitude * 0.01f;
							if (num10 < num)
							{
								num = num10;
								site = new Vector2((float)j + 0.5f, (float)i + 0.5f);
								eye = new Vector2(num6, num7);
								flag = true;
							}
						}
					}
				}
			}
			if (flag)
			{
				Log.Info($"BLAST site {site} watched from {eye} (score {num:F1})");
			}
			return flag;
		}

		private void Watch(Vector2 eye, Vector2 target, float height, float pitch)
		{
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = height;
			_mod.SetYaw(Mathf.Atan2(target.y - eye.y, target.x - eye.x));
			_mod.SetPitchDegrees(pitch);
		}

		private IEnumerator Nuke()
		{
			if (!Site(18f, 85f, out var site, out var eye))
			{
				Log.Error("BLAST no site for the nuke");
				yield break;
			}
			for (int i = -7; i <= 7; i++)
			{
				for (int j = -7; j <= 7; j++)
				{
					Building building = World.world.GetTile((int)eye.x + j, (int)eye.y + i)?.building;
					if (building == null || building.asset == null)
					{
						continue;
					}
					BuildingType building_type = building.asset.building_type;
					if (building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Mineral || building_type == BuildingType.Building_Plant)
					{
						try
						{
							building.removeBuildingFinal();
						}
						catch
						{
						}
					}
				}
			}
			Watch(eye, site, 4f, 14f);
			List<PlacedLights.Light> lights = new List<PlacedLights.Light>();
			if (_options.Contains("lights"))
			{
				Vector2 normalized = (site - eye).normalized;
				Vector2 vector = new Vector2(0f - normalized.y, normalized.x);
				float heading = Mathf.Atan2(0f - normalized.y, 0f - normalized.x);
				lights.Add(PlacedLights.Add(null, PlacedLights.Kind.Torch, eye + normalized * 4f + vector * 1.6f, heading, null));
				lights.Add(PlacedLights.Add(null, PlacedLights.Kind.Torch, eye + normalized * 4f - vector * 1.6f, heading, null));
				lights.Add(PlacedLights.Add(null, PlacedLights.Kind.Brazier, eye + normalized * 6.5f + vector * 3f, heading, null));
			}
			yield return new WaitForSecondsRealtime(2f);
			_mod.SaveFrame("blast_nuke_before.png");
			WorldTile tile = World.world.GetTile((int)site.x, (int)site.y);
			try
			{
				World.world.drop_manager.spawn(tile, "atomic_bomb", 10f, -1f, -1L);
			}
			catch (Exception ex)
			{
				Log.Error("BLAST drop: " + ex.Message);
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("blast_nuke_falling.png");
			float wait = 0f;
			while (SceneCollector.NukeCount == 0 && wait < 15f)
			{
				wait += Time.unscaledDeltaTime;
				yield return null;
			}
			Log.Info($"BLAST nuke went off after {wait:F1} s: {SceneCollector.NukeCount} nukes");
			float[] array = new float[14]
			{
				0.05f, 0.3f, 0.7f, 1.3f, 2.2f, 3.5f, 5f, 8f, 12f, 17f,
				24f, 32f, 42f, 55f
			};
			float start = Time.unscaledTime;
			float[] array2 = array;
			foreach (float k2 in array2)
			{
				while (Time.unscaledTime - start < k2)
				{
					yield return null;
				}
				float num = -1f;
				foreach (NukeFx activeNuke in SceneCollector.ActiveNukes)
				{
					num = activeNuke.Age;
				}
				_mod.SaveFrame($"blast_nuke_{k2:00.0}.png");
				Log.Info($"BLAST nuke t={k2:F1}: age {num:F1}, particles {SceneCollector.BlastParticleCount}, frame {_mod.FrameMs:F1} ms, flash {_mod.Renderer.ScreenFlash >> 24}");
			}
			foreach (PlacedLights.Light item in lights)
			{
				if (item != null)
				{
					PlacedLights.Remove(item);
				}
			}
		}

		private IEnumerator Meteor()
		{
			if (!Site(6f, 40f, out var site, out var eye))
			{
				Log.Error("BLAST no site for the meteor");
				yield break;
			}
			Watch(eye, site, 14f, 14f);
			yield return new WaitForSecondsRealtime(1.5f);
			WorldTile tile = World.world.GetTile((int)site.x, (int)site.y);
			SceneCollector.MeteorFramesDrawn = 0;
			SceneCollector.MeteorHighest = 0f;
			try
			{
				Meteorite.spawnMeteorite(tile);
			}
			catch (Exception ex)
			{
				Log.Error("BLAST meteor: " + ex.Message);
				yield break;
			}
			float start = Time.unscaledTime;
			int shot = 0;
			while (Time.unscaledTime - start < 14f && SceneCollector.ImpactCount == 0)
			{
				if (Time.unscaledTime - start > (float)shot * 0.4f)
				{
					_mod.SaveFrame($"blast_meteor_fall{shot:00}.png");
					shot++;
				}
				yield return null;
			}
			float num = _mod.TerrainForTest.SampleHeight(site.x, site.y);
			Log.Info($"BLAST meteor struck after {Time.unscaledTime - start:F1} s: {SceneCollector.ImpactCount} strikes, drawn falling in {SceneCollector.MeteorFramesDrawn} frames, highest {SceneCollector.MeteorHighest - num:F0} tiles up");
			Log.Info(((SceneCollector.MeteorFramesDrawn >= 20 && SceneCollector.MeteorHighest - num > 60f) ? "BLAST PASS" : "BLAST FAIL") + " meteor seen coming down out of the sky");
			start = Time.unscaledTime;
			float[] array = new float[7] { 0.05f, 0.25f, 0.6f, 1.2f, 2.5f, 5f, 10f };
			foreach (float k in array)
			{
				while (Time.unscaledTime - start < k)
				{
					yield return null;
				}
				_mod.SaveFrame($"blast_meteor_hit_{k:00.0}.png");
			}
			Watch(site + new Vector2(0f, -9f), site, 0f, -10f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("blast_meteor_crater.png");
		}

		private IEnumerator HeatRay()
		{
			if (!Site(12f, 38f, out var site, out var eye))
			{
				Log.Error("BLAST no site for the heat ray");
				yield break;
			}
			Watch(eye, site, 12f, 2f);
			yield return new WaitForSecondsRealtime(1.5f);
			float start = Time.unscaledTime;
			float heatClock = 0f;
			float[] shots = new float[5] { 0.15f, 0.5f, 1.2f, 2.5f, 4f };
			int next = 0;
			bool touched = false;
			while (Time.unscaledTime - start < 5f)
			{
				float num = Time.unscaledTime - start;
				Vector2 vector = site + new Vector2(Mathf.Max(0f, num - 2f) * 2f, 0f);
				WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
				try
				{
					World.world.heat_ray_fx.play((Vector2)tile.pos, 10);
					heatClock += Time.unscaledDeltaTime;
					if (heatClock > 0.25f)
					{
						heatClock = 0f;
						for (int i = 0; i < 12; i++)
						{
							WorldTile tile2 = World.world.GetTile(tile.x + UnityEngine.Random.Range(-6, 7), tile.y + UnityEngine.Random.Range(-6, 7));
							if (tile2 != null)
							{
								World.world.heat.addTile(tile2, 2);
							}
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("BLAST heat ray: " + ex.Message);
					yield break;
				}
				HeatRayFx heatRayForTest = SceneCollector.HeatRayForTest;
				if (heatRayForTest != null && heatRayForTest.Touching)
				{
					touched = true;
				}
				if (next < shots.Length && num >= shots[next])
				{
					_mod.SaveFrame($"blast_heatray_{shots[next]:0.00}.png");
					Log.Info($"BLAST heat ray t={num:F1}: drawn {heatRayForTest != null}, reach {heatRayForTest?.Reach ?? 0f:F2}, on the ground {heatRayForTest?.Touching ?? false}, held {heatRayForTest?.Held ?? 0f:F1} s, particles {SceneCollector.BlastParticleCount}, frame {_mod.FrameMs:F1} ms");
					next++;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SaveFrame("blast_heatray_released.png");
			Watch(site + new Vector2(0f, -16f), site, 9f, -12f);
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("blast_heatray_after.png");
			float wait = 0f;
			while (SceneCollector.HeatRayForTest != null && wait < 8f)
			{
				wait += Time.unscaledDeltaTime;
				yield return null;
			}
			Log.Info((touched ? "BLAST PASS" : "BLAST FAIL") + " heat ray came down onto the ground");
			Log.Info(((SceneCollector.HeatRayForTest == null) ? "BLAST PASS" : "BLAST FAIL") + $" heat ray gone {wait:F1} s after it was let go");
		}

		private IEnumerator CrabBomb()
		{
			if (!Site(14f, 26f, out var site, out var eye))
			{
				Log.Error("BLAST no site for the crab bomb");
				yield break;
			}
			Watch(eye, site, 10f, 0f);
			yield return new WaitForSecondsRealtime(1.5f);
			WorldTile tile = World.world.GetTile((int)site.x, (int)site.y);
			try
			{
				World.world.drop_manager.spawn(tile, "crab_bomb", -1f, -1f, -1L);
			}
			catch (Exception ex)
			{
				Log.Error("BLAST crab bomb: " + ex.Message);
				yield break;
			}
			float start = Time.unscaledTime;
			int shot = 0;
			int mostBlasts = 0;
			int landings = 0;
			while (Time.unscaledTime - start < 20f)
			{
				float num = Time.unscaledTime - start;
				mostBlasts = Mathf.Max(mostBlasts, SceneCollector.CrabBlastCount);
				landings = Mathf.Max(landings, SceneCollector.CrabLandingCount);
				if (num > (float)shot * 0.6f)
				{
					_mod.SaveFrame($"blast_crab_{shot:00}.png");
					Log.Info($"BLAST crab t={num:F1}: drops drawn {_mod.SceneForTest.CrabDropCount}, landed {SceneCollector.CrabLandingCount}, blasts {SceneCollector.CrabBlastCount}, frame {_mod.FrameMs:F1} ms");
					shot++;
				}
				if (mostBlasts > 0 && SceneCollector.CrabBlastCount == 0 && _mod.SceneForTest.CrabDropCount == 0)
				{
					break;
				}
				yield return null;
			}
			Log.Info(((landings > 0) ? "BLAST PASS" : "BLAST FAIL") + " crab bomb came down on its parachutes and landed");
			Log.Info(((mostBlasts > 0) ? "BLAST PASS" : "BLAST FAIL") + $" crab bomb's little crabs blew up ({mostBlasts} at once at most)");
		}

		private IEnumerator Bowling()
		{
			if (!Site(16f, 50f, out var site, out var eye))
			{
				Log.Error("BLAST no site for the bowling ball");
				yield break;
			}
			Watch(eye, site, 13f, 0f);
			yield return new WaitForSecondsRealtime(1.5f);
			try
			{
				Boulder._initial_charge_position = site;
				Boulder boulder = EffectsLibrary.spawnAt("fx_boulder", site, 1f) as Boulder;
				if (boulder == null)
				{
					Log.Error("BLAST no bowling ball");
					yield break;
				}
				boulder.spawnOn(site);
			}
			catch (Exception ex)
			{
				Log.Error("BLAST bowling ball: " + ex.Message);
				yield break;
			}
			float start = Time.unscaledTime;
			int shot = 0;
			int mostImpacts = 0;
			int finals = 0;
			while (Time.unscaledTime - start < 12f)
			{
				float num = Time.unscaledTime - start;
				mostImpacts = Mathf.Max(mostImpacts, SceneCollector.BowlImpactCount);
				Vector2 ballAtForTest = SceneCollector.BallAtForTest;
				if (ballAtForTest != Vector2.zero)
				{
					_mod.SetYaw(Mathf.Atan2(ballAtForTest.y - eye.y, ballAtForTest.x - eye.x));
				}
				finals = Mathf.Max(finals, SceneCollector.BowlFinalCount);
				if (num > (float)shot * 0.4f)
				{
					_mod.SaveFrame($"blast_bowling_{shot:00}.png");
					Log.Info($"BLAST bowling t={num:F1}: balls {_mod.SceneForTest.BallCount}, strikes {SceneCollector.BowlImpactCount} (bursts {SceneCollector.BowlFinalCount}), frame {_mod.FrameMs:F1} ms");
					shot++;
				}
				if (finals > 0 && num > 6f)
				{
					break;
				}
				yield return null;
			}
			Log.Info(((mostImpacts >= 2) ? "BLAST PASS" : "BLAST FAIL") + $" bowling ball struck the ground {mostImpacts} times");
			Log.Info(((finals > 0) ? "BLAST PASS" : "BLAST FAIL") + " bowling ball burst on its last landing");
		}

		private Vector2 ClearView(Vector2 at, float up, float dist, float eyeUp)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 result = at + Vector2.right * dist;
			float num = float.MaxValue;
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector2 vector = at + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * dist;
				if (terrainForTest != null && !(vector.x < 3f) && !(vector.y < 3f) && !(vector.x > (float)(terrainForTest.Width - 4)) && !(vector.y > (float)(terrainForTest.Height - 4)))
				{
					float a = terrainForTest.SampleHeight(vector.x, vector.y) + eyeUp;
					float b = terrainForTest.SampleHeight(at.x, at.y) + up;
					float num2 = 0f;
					for (int j = 1; j < 16; j++)
					{
						float t = (float)j / 16f;
						Vector2 vector2 = Vector2.Lerp(vector, at, t);
						num2 = Mathf.Max(num2, terrainForTest.SampleHeight(vector2.x, vector2.y) - Mathf.Lerp(a, b, t));
					}
					if (num2 < num)
					{
						num = num2;
						result = vector;
					}
				}
			}
			return result;
		}

		private IEnumerator Finger()
		{
			if (!Site(10f, 45f, out var site, out var eye))
			{
				Log.Error("BLAST no site for the finger");
				yield break;
			}
			Watch(eye, site, 2f, 28f);
			Actor finger = null;
			try
			{
				finger = World.world.units.spawnNewUnit("god_finger", World.world.GetTile((int)site.x, (int)site.y), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("BLAST god finger: " + ex.Message);
			}
			if (finger == null)
			{
				Log.Error("BLAST no god finger");
				yield break;
			}
			bool savedGpu = _mod.Settings.GpuRender;
			_mod.Settings.GpuRender = false;
			int seen = 0;
			int clouded = 0;
			float worstCloud = 0f;
			for (int i = 0; i < 14; i++)
			{
				yield return new WaitForSecondsRealtime(1.5f);
				if (finger == null || !finger.isAlive())
				{
					break;
				}
				bool drawing = false;
				try
				{
					GodFinger actorComponent = finger.getActorComponent<GodFinger>();
					drawing = actorComponent != null && actorComponent.is_drawing;
				}
				catch
				{
				}
				if (i == 10)
				{
					for (float waited = 0f; waited < 60f; waited += 0.25f)
					{
						if (drawing)
						{
							break;
						}
						if (finger == null)
						{
							break;
						}
						if (!finger.isAlive())
						{
							break;
						}
						yield return new WaitForSecondsRealtime(0.25f);
						try
						{
							GodFinger actorComponent2 = finger.getActorComponent<GodFinger>();
							drawing = actorComponent2 != null && actorComponent2.is_drawing;
						}
						catch
						{
						}
					}
					if (finger == null || !finger.isAlive())
					{
						break;
					}
				}
				Vector2 p = finger.current_position;
				if (i == 10)
				{
					Watch(ClearView(p, 4f, 16f, 2.2f), p, 2.2f, 22f);
				}
				if (i == 12)
				{
					Watch(ClearView(p, 8f, 6f, 1f), p, 1f, 55f);
				}
				if (i == 13)
				{
					Watch(ClearView(p, 20f, 75f, 2f), p, 2f, 18f);
				}
				_mod.SetYaw(Mathf.Atan2(p.y - _mod.DebugEye.y, p.x - _mod.DebugEye.x));
				yield return null;
				yield return null;
				_mod.SaveFrame($"blast_finger_{i:00}.png");
				int godFingersDrawn = _mod.SceneForTest.GodFingersDrawn;
				int cloudPixels = _mod.Renderer.CloudPixels;
				if (godFingersDrawn > 0)
				{
					seen++;
				}
				if (cloudPixels > 50)
				{
					clouded++;
				}
				worstCloud = Mathf.Max(worstCloud, _mod.Renderer.CloudModelMs);
				Log.Info($"BLAST finger {i}: at {p}, height {finger.position_height:F1}, drawing {drawing}, arms drawn {godFingersDrawn}, cloud {cloudPixels} px in {_mod.Renderer.CloudModelMs:F1} ms (prep {_mod.Renderer.CloudPrepMs:F1}, raster {_mod.Renderer.CloudRasterMs:F1}), frame {_mod.FrameMs:F1} ms");
			}
			Log.Info(((seen >= 10) ? "BLAST PASS" : "BLAST FAIL") + $" the God Finger drawn as an arm of cloud in {seen} of 14 frames");
			Log.Info(((clouded >= 8) ? "BLAST PASS" : "BLAST FAIL") + $" its cloud covered the view in {clouded} frames (slowest cloud pass {worstCloud:F1} ms)");
			_mod.Settings.GpuRender = savedGpu;
			try
			{
				if (finger != null && finger.isAlive())
				{
					finger.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Grab()
		{
			if (!Site(8f, 26f, out var site, out var _))
			{
				Log.Error("BLAST no site for the grab");
				yield break;
			}
			List<Actor> held = new List<Actor>();
			for (int i = 0; i < 3; i++)
			{
				try
				{
					Actor actor = World.world.units.spawnNewUnit("sheep", World.world.GetTile((int)site.x + i - 1, (int)site.y), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					if (actor != null)
					{
						held.Add(actor);
					}
				}
				catch (Exception ex)
				{
					Log.Error("BLAST grab spawn: " + ex.Message);
				}
			}
			if (held.Count == 0)
			{
				Log.Error("BLAST nobody to grab");
				yield break;
			}
			Magnet mg = World.world.magnet;
			foreach (Actor item in held)
			{
				mg.magnet_units.Add(item);
				mg._magnet_units.Add(item);
				item.is_in_magnet = true;
			}
			mg._has_units = true;
			Watch(ClearView(site, 4f, 24f, 7f), site, 7f, 4f);
			int drawn = 0;
			float closed = 0f;
			for (int j = 0; j < 6; j++)
			{
				for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
				{
					foreach (Actor item2 in held)
					{
						if (item2 != null && item2.isAlive())
						{
							item2.position_height = 2.5f;
						}
					}
					yield return null;
				}
				_mod.SaveFrame($"blast_grab_{j:00}.png");
				if (_mod.SceneForTest.GodGrabsDrawn > 0)
				{
					drawn++;
				}
				closed = Mathf.Max(closed, _mod.SceneForTest.GodGrabCurl);
				Log.Info($"BLAST grab {j}: hand drawn {_mod.SceneForTest.GodGrabsDrawn}, closed {_mod.SceneForTest.GodGrabCurl:F2}, cloud {_mod.Renderer.CloudPixels} px in {_mod.Renderer.CloudModelMs:F1} ms, frame {_mod.FrameMs:F1} ms");
			}
			try
			{
				mg.dropPickedUnits();
			}
			catch (Exception ex2)
			{
				Log.Error("BLAST grab drop: " + ex2.Message);
			}
			float openAt = -1f;
			for (int j = 0; j < 3; j++)
			{
				yield return new WaitForSecondsRealtime(0.25f);
				_mod.SaveFrame($"blast_grab_let_{j}.png");
				Log.Info($"BLAST grab let go {j}: hand drawn {_mod.SceneForTest.GodGrabsDrawn}, closed {_mod.SceneForTest.GodGrabCurl:F2}");
				if (openAt < 0f && _mod.SceneForTest.GodGrabCurl < 0.5f)
				{
					openAt = j;
				}
			}
			Log.Info(((drawn >= 5 && closed > 0.9f) ? "BLAST PASS" : "BLAST FAIL") + $" the grab hand held them in {drawn} of 6 frames (closed {closed:F2})");
			Log.Info(((openAt >= 0f) ? "BLAST PASS" : "BLAST FAIL") + " the grab hand opened as it let them go");
			foreach (Actor item3 in held)
			{
				try
				{
					if (item3 != null && item3.isAlive())
					{
						item3.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Vents()
		{
			if (!Site(9f, 22f, out var site, out var eye))
			{
				Log.Error("BLAST no site for the vents");
				yield break;
			}
			List<Building> placed = new List<Building>();
			string[] ids = new string[3] { "volcano", "geyser", "geyser_acid" };
			for (int i = 0; i < ids.Length; i++)
			{
				Vector2 normalized = (site - eye).normalized;
				Vector2 vector = new Vector2(0f - normalized.y, normalized.x);
				Vector2 vector2 = site + vector * ((float)(i - 1) * 11f) + normalized * ((i == 0) ? 4f : 0f);
				try
				{
					Building building = World.world.buildings.addBuilding(ids[i], World.world.GetTile((int)vector2.x, (int)vector2.y), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						placed.Add(building);
					}
					else
					{
						Log.Error("BLAST could not place " + ids[i]);
					}
				}
				catch (Exception ex)
				{
					Log.Error("BLAST place " + ids[i] + ": " + ex.Message);
				}
			}
			Log.Info($"BLAST vents placed: {placed.Count}");
			Watch(eye, site, 1.5f, 6f);
			yield return new WaitForSecondsRealtime(3f);
			_mod.SaveFrame("blast_vents_all.png");
			for (int j = 0; j < placed.Count; j++)
			{
				Building building2 = placed[j];
				if (building2 != null && building2.current_tile != null)
				{
					Vector2 vector3 = new Vector2((float)building2.current_tile.x + 0.5f, (float)building2.current_tile.y + 1.5f);
					Vector2 normalized2 = (vector3 - eye).normalized;
					Watch(vector3 - normalized2 * ((j == 0) ? 20f : 8f), vector3, (j == 0) ? 2.5f : 1.2f, (j == 0) ? 8f : 0f);
					for (int k = 0; k < 4; k++)
					{
						yield return new WaitForSecondsRealtime(3.5f);
						_mod.SaveFrame($"blast_vent_{ids[j]}_{k}.png");
					}
				}
			}
			_mod.Settings.TimeMode = 3;
			_mod.ForceRelight();
			Watch(eye, site, 1.5f, 6f);
			yield return new WaitForSecondsRealtime(4f);
			_mod.SaveFrame("blast_vents_night.png");
			yield return new WaitForSecondsRealtime(5f);
			_mod.SaveFrame("blast_vents_night2.png");
			_mod.Settings.TimeMode = 1;
			_mod.ForceRelight();
			foreach (Building item in placed)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						item.startDestroyBuilding();
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Blaster()
		{
			if (!Equip(_king, "alien_blaster"))
			{
				Log.Error("BLAST no blaster");
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			BlasterControl b = _mod.BlasterForTest;
			Log.Info($"BLAST blaster in hand {b.InHand}");
			_mod.SaveFrame("blast_gun_ready.png");
			b.ShootForTest(3);
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SaveFrame("blast_gun_hot.png");
			b.ShootForTest(4);
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SaveFrame("blast_gun_overheated.png");
			Log.Info($"BLAST blaster heat {b.State.Heat:F2}, overheated {b.State.Overheated}, attack blocked {PossessionHooks.BlockAttack}, prompt '{_mod.Prompt}'");
			b.State.StartVent();
			for (int i = 0; i < 6; i++)
			{
				yield return new WaitForSecondsRealtime(0.22f);
				_mod.SaveFrame($"blast_gun_vent{i}.png");
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info($"BLAST blaster after venting: heat {b.State.Heat:F2}, overheated {b.State.Overheated}, barrel {b.State.BarrelHeat:F2}, blocked {PossessionHooks.BlockAttack}");
			yield return new WaitForSecondsRealtime(3f);
			_mod.SaveFrame("blast_gun_cooling.png");
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "blast_gun_screen.png"));
			yield return new WaitForSecondsRealtime(0.5f);
			Equip(_king, null);
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				if (id != null)
				{
					EquipmentAsset equipmentAsset = AssetManager.items.get(id);
					if (equipmentAsset == null)
					{
						return false;
					}
					Item pItem = World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
					slot.setItem(pItem, unit);
				}
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("BLAST equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
