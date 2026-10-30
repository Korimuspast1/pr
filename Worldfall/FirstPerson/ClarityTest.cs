using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ClarityTest : MonoBehaviour
	{
		private static readonly float[] Distances = new float[6] { 12f, 25f, 40f, 60f, 85f, 110f };

		private static readonly string[] Kinds = new string[3] { "human", "sheep", "wolf" };

		private WorldBoxMod _mod;

		private const float VillageDistance = 75f;

		private float _eyeHeight = 2.2f;

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
			Vector2 eye = Vector2.zero;
			Vector2 fwd = Vector2.right;
			float num = float.MinValue;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
					Vector2 vector2 = new Vector2((float)item.x + 0.5f, (float)item.y + 0.5f) - vector * 75f;
					float num2 = Corridor(vector2, vector, clear: false);
					if (num2 > num)
					{
						num = num2;
						home = item;
						eye = vector2;
						fwd = vector;
					}
				}
			}
			if (home == null)
			{
				Log.Error("CLAR no site");
				Finish(quit);
				yield break;
			}
			Log.Info($"CLAR site {home.x},{home.y}, eye {eye}, corridor score {num:F0}");
			Actor king = KingTest.FoundKingdom(home, "human", 10, 4);
			if (king == null)
			{
				Log.Error("CLAR could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("CLAR first person did not start");
				Finish(quit);
				yield break;
			}
			Vector2 vector3 = new Vector2((float)home.x + 0.5f, (float)home.y + 0.5f);
			int num3 = -1;
			float[] array = new float[3] { 2.2f, 4f, 6f };
			foreach (float num4 in array)
			{
				for (int k = 0; k < 16; k++)
				{
					for (float num5 = 60f; num5 <= 90f; num5 += 5f)
					{
						float f2 = (float)k * MathF.PI / 8f;
						Vector2 vector4 = new Vector2(Mathf.Cos(f2), Mathf.Sin(f2));
						Vector2 vector5 = vector3 - vector4 * num5;
						if (!(Corridor(vector5, vector4, clear: false) < 300f))
						{
							int num6 = Seen(vector5, vector4, num4);
							if (num6 > num3)
							{
								num3 = num6;
								eye = vector5;
								fwd = vector4;
								_eyeHeight = num4;
							}
						}
					}
				}
				if (num3 >= Distances.Length * 2 - 1)
				{
					break;
				}
			}
			Log.Info($"CLAR eye {eye} at {_eyeHeight} up, sees {num3} of {Distances.Length * 2} line points, village {(vector3 - eye).magnitude:F0} tiles off");
			Log.Info($"CLAR cleared {Corridor(eye, fwd, clear: true):F0} trees and rocks from the view");
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
			Log.Info($"CLAR view distance {_mod.Renderer.MaxDistance}, resolution {_mod.Renderer.Width}x{_mod.Renderer.Height}");
			yield return Run("line", Line(eye, fwd, s));
			yield return Run("village", Village(eye, fwd, s));
			s.TimeMode = savedMode;
			_mod.Renderer.ActorClarity = true;
			Config.paused = false;
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("CLAR --- " + name);
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
					Log.Error("CLAR " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator Line(Vector2 eye, Vector2 fwd, Settings s)
		{
			float bestYaw = Mathf.Atan2(fwd.y, fwd.x);
			Vector2 vector = new Vector2(0f - fwd.y, fwd.x);
			List<Actor> spawned = new List<Actor>();
			float[] distances = Distances;
			foreach (float num in distances)
			{
				for (int j = 0; j < Kinds.Length; j++)
				{
					Vector2 vector2 = eye + fwd * num + vector * ((float)(j - 1) * Mathf.Max(1.2f, num * 0.025f));
					WorldTile tile = World.world.GetTile((int)vector2.x, (int)vector2.y);
					if (tile == null || tile.main_type == null || tile.main_type.liquid)
					{
						continue;
					}
					try
					{
						Actor actor = World.world.units.spawnNewUnit(Kinds[j], tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
						if (actor != null)
						{
							spawned.Add(actor);
						}
					}
					catch (Exception ex)
					{
						Log.Error("CLAR spawn " + Kinds[j] + ": " + ex.Message);
					}
				}
			}
			Log.Info($"CLAR spawned {spawned.Count} of {Distances.Length * Kinds.Length}");
			yield return new WaitForSecondsRealtime(0.5f);
			Config.paused = true;
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = _eyeHeight;
			_mod.SetYaw(bestYaw);
			_mod.SetPitchDegrees(-1f);
			string[] times = new string[3] { "noon", "golden", "night" };
			int[] modes = new int[3] { 1, 2, 3 };
			for (int m = 0; m < modes.Length; m++)
			{
				s.TimeMode = modes[m];
				_mod.ForceRelight();
				yield return new WaitForSecondsRealtime((m == 0) ? 3f : 1.5f);
				yield return Pair("line_" + times[m]);
			}
			Config.paused = false;
			foreach (Actor item in spawned)
			{
				try
				{
					if (item.isAlive())
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Village(Vector2 eye, Vector2 fwd, Settings s)
		{
			s.TimeMode = 1;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(4f);
			Config.paused = true;
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = _eyeHeight;
			_mod.SetYaw(Mathf.Atan2(fwd.y, fwd.x));
			_mod.SetPitchDegrees(-1f);
			yield return new WaitForSecondsRealtime(3f);
			yield return Pair("village");
			Config.paused = false;
		}

		private int Seen(Vector2 eye, Vector2 fwd, float eyeUp)
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			float num = sceneTerrain.SampleHeight(eye.x, eye.y) + eyeUp;
			int num2 = 0;
			float[] distances = Distances;
			foreach (float num3 in distances)
			{
				float[] array = new float[2] { 0.3f, 1f };
				foreach (float num4 in array)
				{
					Vector2 vector = eye + fwd * num3;
					float num5 = sceneTerrain.SampleHeight(vector.x, vector.y) + num4;
					bool flag = true;
					for (float num6 = 1f; num6 < num3 && flag; num6 += 0.5f)
					{
						Vector2 vector2 = eye + fwd * num6;
						if (sceneTerrain.SampleHeight(vector2.x, vector2.y) > num + (num5 - num) * (num6 / num3))
						{
							flag = false;
						}
					}
					if (flag)
					{
						num2++;
					}
				}
			}
			return num2;
		}

		private static float Corridor(Vector2 eye, Vector2 fwd, bool clear)
		{
			Vector2 vector = new Vector2(0f - fwd.y, fwd.x);
			int num = 0;
			int num2 = 0;
			int num3 = int.MaxValue;
			int num4 = int.MinValue;
			int num5 = 0;
			for (float num6 = 0f; num6 <= 116f; num6 += 1f)
			{
				for (float num7 = -2f; num7 <= 2f; num7 += 1f)
				{
					Vector2 vector2 = eye + fwd * num6 + vector * num7;
					WorldTile tile = World.world.GetTile((int)vector2.x, (int)vector2.y);
					if (tile == null || tile.main_type == null)
					{
						if (!clear)
						{
							return -1000f;
						}
						continue;
					}
					if (num7 == 0f)
					{
						if (!tile.main_type.liquid && !tile.main_type.block)
						{
							num++;
						}
						num3 = Mathf.Min(num3, tile.main_type.height_min);
						num4 = Mathf.Max(num4, tile.main_type.height_min);
					}
					if (tile.building == null || !AssetStyle.IsSolid(tile.building))
					{
						continue;
					}
					if (clear)
					{
						if (num6 < 67f)
						{
							try
							{
								tile.building.removeBuildingFinal();
								num5++;
							}
							catch
							{
							}
						}
					}
					else
					{
						num2++;
					}
				}
			}
			if (!clear)
			{
				return (float)num * 4f - (float)num2 - (float)(num4 - num3) * 0.5f;
			}
			return num5;
		}

		private IEnumerator Pair(string name)
		{
			FrameRenderer r = _mod.Renderer;
			bool[] array = new bool[2] { false, true };
			foreach (bool on in array)
			{
				r.ActorClarity = on;
				for (int j = 0; j < 4; j++)
				{
					yield return null;
				}
				float post = 0f;
				float mesh = 0f;
				for (int j = 0; j < 20; j++)
				{
					yield return null;
					post += r.PostMs;
					mesh += r.MeshMs;
				}
				_mod.SaveFrame("clar_" + name + "_" + (on ? "on" : "off") + ".png");
				Log.Info(string.Format("CLAR {0} {1}: post {2:F2} ms, models {3:F2} ms, far specks {4}", name, on ? "on " : "off", post / 20f, mesh / 20f, r.FarActorsDrawn));
			}
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
			Log.Info("CLAR DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
