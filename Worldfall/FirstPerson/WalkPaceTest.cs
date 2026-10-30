using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WalkPaceTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private readonly List<Actor> _near = new List<Actor>();

		private readonly List<Actor> _far = new List<Actor>();

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
			Settings s = _mod.Settings;
			float savedPace = s.FolkPace;
			float savedReach = s.CreatureDistance;
			WorldTile spot = FindOpen(MapBox.width / 2, MapBox.height / 2, 0, MapBox.width / 2);
			WorldTile far = ((spot != null) ? FindOpen(spot.x, spot.y, 95, 125) : null);
			if (spot == null || far == null)
			{
				Log.Error($"WALKPACE no open land (near {spot != null}, far {far != null})");
				Finish(quit, s, savedPace, savedReach);
				yield break;
			}
			Actor actor = Spawn("human", spot);
			if (actor == null)
			{
				Finish(quit, s, savedPace, savedReach);
				yield break;
			}
			try
			{
				((BaseSimObject)actor).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			ControllableUnit.setControllableCreature(actor);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("WALKPACE first person did not start");
				Finish(quit, s, savedPace, savedReach);
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
			Check("the walking step is taken over", WalkPace.Installed);
			for (int i = 0; i < 8; i++)
			{
				float f = (float)i * 2.39996f;
				float num = 3f + (float)(i % 4);
				Actor actor2 = Spawn("human", World.world.GetTile(spot.x + (int)(Mathf.Cos(f) * num), spot.y + (int)(Mathf.Sin(f) * num)));
				if (actor2 != null)
				{
					_near.Add(actor2);
				}
				Actor actor3 = Spawn("human", World.world.GetTile(far.x + (int)(Mathf.Cos(f) * num), far.y + (int)(Mathf.Sin(f) * num)));
				if (actor3 != null)
				{
					_far.Add(actor3);
				}
			}
			foreach (Actor item in _near)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			foreach (Actor item2 in _far)
			{
				try
				{
					((BaseSimObject)item2).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			Log.Info($"WALKPACE you at {spot.x},{spot.y}, {_near.Count} walkers round you, {_far.Count} round {far.x},{far.y} " + $"({Vector2.Distance(spot.posV3, far.posV3):F0} tiles off); fixed step {Time.fixedDeltaTime:F4}");
			s.CreatureDistance = 60f;
			System.Random rng = new System.Random(11);
			s.FolkPace = 0.5f;
			yield return new WaitForSecondsRealtime(1.5f);
			float clock = 0f;
			float nearHalf = 0f;
			float farHalf = 0f;
			float nearFull = 0f;
			float speedHalf = 0f;
			float speedFull = 0f;
			yield return Measure("others' pace 50%", 10f, rng, spot, far, delegate(float c, float n, float num2, float v)
			{
				clock = c;
				nearHalf = n;
				farHalf = num2;
				speedHalf = v;
			});
			s.FolkPace = 1f;
			yield return new WaitForSecondsRealtime(1.5f);
			yield return Measure("others' pace 100%", 10f, rng, spot, far, delegate(float c, float n, float num2, float v)
			{
				nearFull = n;
				speedFull = v;
			});
			Check($"near you at 50% they cover 0.4 to 0.6 of their pace ({nearHalf:F2})", nearHalf > 0.4f && nearHalf < 0.6f);
			Check($"far off they keep their pace, 0.8 to 1.2 ({farHalf:F2})", farHalf > 0.8f && farHalf < 1.2f);
			Check($"near you at 100% they keep their pace, 0.8 to 1.2 ({nearFull:F2})", nearFull > 0.8f && nearFull < 1.2f);
			Check($"the world's clock keeps real time while they are slowed, 0.85x to 1.15x ({clock:F2}x)", clock > 0.85f && clock < 1.15f);
			Log.Info($"WALKPACE villagers near you walk {speedHalf:F2} tiles/s ({speedHalf * 1.75f:F1} m/s) at 50%, {speedFull:F2} tiles/s ({speedFull * 1.75f:F1} m/s) at 100%");
			Finish(quit, s, savedPace, savedReach);
		}

		private IEnumerator Measure(string name, float seconds, System.Random rng, WorldTile spot, WorldTile far, Action<float, float, float, float> done)
		{
			Dictionary<Actor, Vector2> last = new Dictionary<Actor, Vector2>();
			double world0 = World.world.getCurWorldTime();
			double worldLast = world0;
			float real = 0f;
			float march = 0f;
			float nearWent = 0f;
			float nearPace = 0f;
			float farWent = 0f;
			float farPace = 0f;
			float nearReal = 0f;
			while (real < seconds)
			{
				if (march <= 0f)
				{
					march = 4f;
					Send(_near, spot, rng);
					Send(_far, far, rng);
				}
				yield return null;
				real += Time.unscaledDeltaTime;
				march -= Time.unscaledDeltaTime;
				double curWorldTime = World.world.getCurWorldTime();
				float worldDt = (float)(curWorldTime - worldLast);
				worldLast = curWorldTime;
				Add(_near, last, worldDt, Time.unscaledDeltaTime, ref nearWent, ref nearPace, ref nearReal);
				float realTime = 0f;
				Add(_far, last, worldDt, Time.unscaledDeltaTime, ref farWent, ref farPace, ref realTime);
			}
			float num = (float)((World.world.getCurWorldTime() - world0) / (double)real);
			float num2 = ((nearPace > 0f) ? (nearWent / nearPace) : 0f);
			float num3 = ((farPace > 0f) ? (farWent / farPace) : 0f);
			float num4 = ((nearReal > 0f) ? (nearWent / nearReal) : 0f);
			Log.Info($"WALKPACE {name}: world {num:F2}x real time; near you they went {nearWent:F1} tiles for {nearPace:F1} at their pace ({num2:F2}), " + $"far off {farWent:F1} for {farPace:F1} ({num3:F2}); near you {num4:F2} tiles a real second");
			done(num, num2, num3, num4);
		}

		private static void Send(List<Actor> walkers, WorldTile around, System.Random rng)
		{
			foreach (Actor walker in walkers)
			{
				if (walker != null && walker.isAlive())
				{
					double num = rng.NextDouble() * Math.PI * 2.0;
					int pX = Mathf.Clamp(around.x + (int)(Math.Cos(num) * 10.0), 2, MapBox.width - 3);
					int pY = Mathf.Clamp(around.y + (int)(Math.Sin(num) * 10.0), 2, MapBox.height - 3);
					try
					{
						walker.goTo(World.world.GetTile(pX, pY));
					}
					catch
					{
					}
				}
			}
		}

		private static void Add(List<Actor> walkers, Dictionary<Actor, Vector2> last, float worldDt, float realDt, ref float went, ref float pace, ref float realTime)
		{
			foreach (Actor walker in walkers)
			{
				if (walker == null || !walker.isAlive())
				{
					continue;
				}
				Vector2 current_position = walker.current_position;
				bool flag = false;
				float num = 0f;
				try
				{
					flag = walker.is_moving && !walker.under_forces && !walker.has_attack_target;
					num = walker._current_combined_movement_speed;
				}
				catch
				{
				}
				if (last.TryGetValue(walker, out var value) && flag && worldDt > 0f)
				{
					float magnitude = (current_position - value).magnitude;
					if (magnitude < 0.5f && magnitude > 0f)
					{
						went += magnitude;
						pace += num * worldDt;
						realTime += realDt;
					}
				}
				last[walker] = current_position;
			}
		}

		private static Actor Spawn(string id, WorldTile t)
		{
			if (t == null)
			{
				return null;
			}
			try
			{
				return World.world.units.spawnNewUnit(id, t, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("WALKPACE spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private static WorldTile FindOpen(int fromX, int fromY, int minR, int maxR)
		{
			int width = MapBox.width;
			int height = MapBox.height;
			for (int i = minR; i <= maxR; i += 4)
			{
				for (int j = 0; j < 32; j++)
				{
					float f = (float)j * MathF.PI / 16f;
					int num = fromX + (int)(Mathf.Cos(f) * (float)i);
					int num2 = fromY + (int)(Mathf.Sin(f) * (float)i);
					if (num < 24 || num2 < 24 || num + 24 > width || num2 + 24 > height)
					{
						continue;
					}
					bool flag = true;
					for (int k = num2 - 12; k < num2 + 12 && flag; k++)
					{
						for (int l = num - 12; l < num + 12 && flag; l++)
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
			Log.Info("WALKPACE " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private void Finish(bool quit, Settings s, float savedPace, float savedReach)
		{
			s.FolkPace = savedPace;
			s.CreatureDistance = savedReach;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"WALKPACE DONE {_pass} passed, {_fail} failed");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
