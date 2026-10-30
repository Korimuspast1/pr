using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class NightFolkTest : MonoBehaviour
	{
		private static readonly string[] Races = new string[4] { "human", "elf", "orc", "dwarf" };

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
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
			WorldTile home = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					WorldTile current = enumerator.Current;
					home = current;
				}
			}
			if (home == null)
			{
				Log.Error("NF no site");
				Finish(quit);
				yield break;
			}
			Actor host = null;
			try
			{
				host = World.world.units.spawnNewUnit("human", home, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("NF host: " + ex.Message);
			}
			if (host == null)
			{
				Log.Error("NF no host");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			ControllableUnit.setControllableCreature(host);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("NF first person did not start");
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
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			Log.Info($"NF site {home.x},{home.y}, resolution {_mod.Renderer.Width}x{_mod.Renderer.Height}");
			WorldTile worldTile = home;
			float num = float.MaxValue;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				float num2 = Roughness(item.x, item.y);
				if (num2 < num)
				{
					num = num2;
					worldTile = item;
				}
			}
			Log.Info($"NF stage {worldTile.x},{worldTile.y}, roughness {num:F0}");
			Vector2 eye = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f - 3.5f);
			yield return Run("women", Women(eye, s));
			yield return Run("zombies", Zombies(eye, s));
			yield return Run("sky", Sky(s));
			s.TimeMode = savedMode;
			FrameRenderer renderer = _mod.Renderer;
			renderer.ForceComet = (renderer.ForceShower = (renderer.ForceAurora = -1f));
			Config.paused = false;
			Log.Info($"NF {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private void Check(string what, bool ok)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("NF " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("NF --- " + name);
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
					Log.Error("NF " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			Config.paused = false;
		}

		private static float Roughness(int cx, int cy)
		{
			int num = int.MaxValue;
			int num2 = int.MinValue;
			int num3 = 0;
			for (int i = cy - 6; i <= cy + 7; i++)
			{
				for (int j = cx - 7; j <= cx + 7; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.main_type == null)
					{
						num3++;
						continue;
					}
					if (tile.main_type.liquid || tile.main_type.block)
					{
						num3++;
					}
					num = Mathf.Min(num, tile.main_type.height_min);
					num2 = Mathf.Max(num2, tile.main_type.height_min);
				}
			}
			return (float)num3 * 100f + (float)(num2 - num);
		}

		private static void ClearBox(int x0, int y0, int x1, int y1)
		{
			for (int i = y0; i <= y1; i++)
			{
				for (int j = x0; j <= x1; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.building != null)
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

		private Actor Spawn(string id, Vector2 at)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile == null || tile.main_type == null || tile.main_type.liquid)
			{
				return null;
			}
			try
			{
				return World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("NF spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private IEnumerator Women(Vector2 eye, Settings s)
		{
			s.TimeMode = 1;
			_mod.ForceRelight();
			ClearBox((int)eye.x - 7, (int)eye.y - 2, (int)eye.x + 7, (int)eye.y + 9);
			List<Actor> people = new List<Actor>();
			for (int i = 0; i < 8; i++)
			{
				Actor actor = Spawn(Races[i / 2], eye + new Vector2(((float)i - 3.5f) * 1.05f, 4.5f));
				if (actor != null)
				{
					try
					{
						actor.data.sex = (((i & 1) == 1) ? ActorSex.Female : ActorSex.Male);
					}
					catch (Exception ex)
					{
						Log.Error("NF sex: " + ex.Message);
					}
					people.Add(actor);
				}
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Config.paused = true;
			foreach (Actor item in people)
			{
				bool flag = false;
				try
				{
					flag = item.isSexFemale();
				}
				catch
				{
				}
				string text = Models.PersonFamily(item);
				Log.Info("NF " + item.asset.id + " " + (flag ? "woman" : "man") + ": " + text);
				Check(item.asset.id + " " + (flag ? "woman" : "man") + " drawn as " + text, flag == text.EndsWith("_fem", StringComparison.Ordinal) && Models.Get(text) != null);
			}
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-4f);
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.SaveFrame("nf_women_row.png");
			for (int pair = 0; pair < people.Count / 2; pair++)
			{
				_ = Vector2.zero;
				Vector2 vector;
				try
				{
					vector = (people[pair * 2].current_position + people[pair * 2 + 1].current_position) * 0.5f;
				}
				catch
				{
					continue;
				}
				_mod.DebugEye = vector + new Vector2(0f, -1.8f);
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame($"nf_women_close{pair}.png");
			}
			int num = 0;
			foreach (Actor item2 in people)
			{
				if (Hitboxes.Seen(item2))
				{
					num++;
				}
			}
			Log.Info($"NF women: {people.Count} spawned, {num} with drawn bodies, eye {eye}, at " + string.Join(" ", people.ConvertAll((Actor p) => p.current_position.ToString("F1"))));
			Config.paused = false;
			foreach (Actor item3 in people)
			{
				try
				{
					if (item3.isAlive())
					{
						item3.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Zombies(Vector2 eye, Settings s)
		{
			s.TimeMode = 1;
			_mod.ForceRelight();
			List<string> list = new List<string>();
			string[] array = new string[5] { "zombie", "zombie_human", "zombie_elf", "zombie_orc", "zombie_dwarf" };
			foreach (string text in array)
			{
				bool flag = false;
				try
				{
					flag = AssetManager.actor_library.get(text) != null;
				}
				catch
				{
				}
				Log.Info("NF asset " + text + ": " + (flag ? "yes" : "no"));
				if (flag)
				{
					list.Add(text);
				}
			}
			List<Actor> dead = new List<Actor>();
			for (int j = 0; j < list.Count; j++)
			{
				Actor actor = Spawn(list[j], eye + new Vector2(-4f + (float)j * 0.6f, 4f + (float)(j % 2) * 1.2f));
				if (actor != null)
				{
					dead.Add(actor);
				}
			}
			Actor bait = Spawn("sheep", eye + new Vector2(9f, 4.5f));
			foreach (Actor item in dead)
			{
				string text2 = Models.PersonFamily(item);
				Log.Info("NF " + item.asset.id + ": " + text2);
				Check(item.asset.id + " drawn as " + text2, text2.StartsWith("person_zombie", StringComparison.Ordinal) && Models.Get(text2) != null);
			}
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-4f);
			Vector2 start = eye + new Vector2(-3f, 4.5f);
			for (int k = 0; k < 10; k++)
			{
				yield return new WaitForSecondsRealtime(0.4f);
				Vector2 zero = Vector2.zero;
				int num = 0;
				foreach (Actor item2 in dead)
				{
					try
					{
						if (item2.isAlive())
						{
							zero += item2.current_position;
							num++;
						}
					}
					catch
					{
					}
				}
				if (num > 0)
				{
					zero /= (float)num;
					_mod.DebugEye = zero + new Vector2(0f, -3f);
				}
				_mod.SaveFrame($"nf_zombies_{k}.png");
			}
			float num2 = 0f;
			foreach (Actor item3 in dead)
			{
				try
				{
					num2 = Mathf.Max(num2, (item3.current_position - start).magnitude);
				}
				catch
				{
				}
			}
			Log.Info($"NF zombies: {dead.Count} spawned, furthest {num2:F1} tiles from the start line");
			foreach (Actor item4 in dead)
			{
				try
				{
					if (item4.isAlive())
					{
						item4.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			try
			{
				if (bait != null && bait.isAlive())
				{
					bait.dieSimpleNone();
				}
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Sky(Settings s)
		{
			FrameRenderer r = _mod.Renderer;
			bool savedGpu = s.GpuRender;
			s.GpuRender = false;
			s.TimeMode = 3;
			_mod.ForceRelight();
			_mod.DebugEyeSet = false;
			yield return new WaitForSecondsRealtime(1.5f);
			Log.Info($"NF night {r.NightNumber}: tonight comet {r.CometTonight:F2}, shower {r.ShowerTonight}, aurora {r.AuroraTonight:F2}, stars {r.Air.Stars:F2}, clouds {r.Air.Clouds:F2}");
			Check("the day cycle counts nights", r.NightNumber > 0);
			r.ForceComet = 1f;
			yield return null;
			yield return null;
			Vec3 cometDirection = r.CometDirection;
			float num = Mathf.Atan2(cometDirection.Y, cometDirection.X);
			float num2 = Mathf.Asin(Mathf.Clamp(cometDirection.Z, -1f, 1f)) * 57.29578f;
			_mod.SetYaw(num);
			_mod.SetPitchDegrees(Mathf.Min(35f, num2 * 0.8f));
			Log.Info($"NF comet at yaw {num * 57.29578f:F0}, elevation {num2:F0}");
			float[] cost = new float[2];
			for (int pass = 0; pass < 2; pass++)
			{
				bool flag = pass == 1;
				r.ForceComet = (flag ? 1f : 0f);
				r.ForceAurora = (flag ? 1f : 0f);
				r.ForceShower = (flag ? 1f : 0f);
				for (int i = 0; i < 10; i++)
				{
					yield return null;
				}
				float sum = 0f;
				for (int i = 0; i < 60; i++)
				{
					yield return null;
					sum += _mod.RenderMs;
				}
				cost[pass] = sum / 60f;
			}
			Log.Info($"NF perf: plain night {cost[0]:F2} ms a frame, with comet + aurora + shower {cost[1]:F2} ms");
			Check($"night sights cost under 2 ms a frame ({cost[1] - cost[0]:F2})", cost[1] - cost[0] < 2f);
			r.ForceAurora = 0f;
			r.ForceShower = 0f;
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SaveFrame("nf_comet.png");
			Check("tonight's comet is up", r.CometTonight > 0.5f);
			r.ForceComet = 0f;
			r.ForceAurora = 1f;
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(8f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("nf_aurora.png");
			yield return new WaitForSecondsRealtime(2f);
			_mod.SaveFrame("nf_aurora2.png");
			Check("the aurora is out", r.AuroraTonight > 0.3f);
			r.ForceAurora = 0f;
			r.ForceShower = 1f;
			yield return null;
			Vec3 rad = r.ShowerRadiant;
			_mod.SetYaw(Mathf.Atan2(rad.Y, rad.X));
			_mod.SetPitchDegrees(Mathf.Min(35f, Mathf.Asin(Mathf.Clamp(rad.Z, -1f, 1f)) * 57.29578f * 0.8f));
			int withMeteors = 0;
			int most = 0;
			for (int pass = 0; pass < 10; pass++)
			{
				yield return new WaitForSecondsRealtime(0.35f);
				withMeteors += ((r.MeteorsInSky > 0) ? 1 : 0);
				most = Mathf.Max(most, r.MeteorsInSky);
				_mod.SaveFrame($"nf_shower_{pass}.png");
			}
			Log.Info($"NF shower: radiant elevation {Mathf.Asin(Mathf.Clamp(rad.Z, -1f, 1f)) * 57.29578f:F0}, shooting stars in the sky in {withMeteors} of 10 frames, at most {most} at once");
			Check("shooting stars fall in a shower", withMeteors >= 5);
			Check("a shower night", r.ShowerTonight);
			r.ForceComet = (r.ForceShower = (r.ForceAurora = -1f));
			_mod.SetPitchDegrees(0f);
			s.GpuRender = savedGpu;
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
			Log.Info("NF DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
