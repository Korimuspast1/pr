using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FarCreaturesTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private readonly List<Actor> _placed = new List<Actor>();

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
			float savedDetail = s.DetailDistance;
			float savedCreatures = s.CreatureDistance;
			float savedDraw = s.DrawDistance;
			int savedTime = s.TimeMode;
			s.TimeMode = 1;
			s.DetailDistance = 80f;
			s.DrawDistance = 1200f;
			Actor king = null;
			foreach (WorldTile item3 in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item3, "human", 6, 0);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("FAR could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(3f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("FAR first person did not start");
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
			int width = MapBox.width;
			int height = MapBox.height;
			Vector2 eye = Vector2.zero;
			Vector2 dir = Vector2.zero;
			int num = -1;
			(int, int)[] array = new(int, int)[4]
			{
				(12, 12),
				(width - 13, 12),
				(12, height - 13),
				(width - 13, height - 13)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(int, int) tuple = array[i];
				int item = tuple.Item1;
				int item2 = tuple.Item2;
				Vector2 vector = new Vector2(item, item2);
				Vector2 normalized = (new Vector2((float)width * 0.5f, (float)height * 0.5f) - vector).normalized;
				int num2 = 0;
				for (int j = 50; j <= 210; j += 5)
				{
					WorldTile tile = World.world.GetTile((int)(vector.x + normalized.x * (float)j), (int)(vector.y + normalized.y * (float)j));
					if (tile != null && tile.main_type != null && !tile.main_type.liquid)
					{
						num2++;
					}
				}
				if (num2 > num)
				{
					num = num2;
					eye = vector;
					dir = normalized;
				}
			}
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 9f;
			_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			_mod.SetPitchDegrees(-3f);
			Log.Info($"FAR eye {eye.x:F0},{eye.y:F0} looking {Mathf.Atan2(dir.y, dir.x) * 57.29578f:F0} deg, {num} of 33 land tiles along it, map {width}x{height}");
			string[] array2 = new string[4] { "orc", "skeleton", "wolf", "sheep" };
			float[] array3 = new float[4] { 60f, 100f, 150f, 200f };
			Vector2 side = new Vector2(0f - dir.y, dir.x);
			float[] array4 = array3;
			foreach (float num3 in array4)
			{
				for (int k = 0; k < array2.Length; k++)
				{
					WorldTile worldTile = LandNear(eye + dir * num3 + side * (((float)k - 1.5f) * 3f));
					if (worldTile == null)
					{
						continue;
					}
					try
					{
						Actor actor = World.world.units.spawnNewUnit(array2[k], worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
						if (actor != null)
						{
							_placed.Add(actor);
						}
					}
					catch (Exception ex)
					{
						Log.Error("FAR spawn " + array2[k] + ": " + ex.Message);
					}
				}
			}
			Log.Info($"FAR placed {_placed.Count} of {array3.Length * array2.Length}");
			yield return new WaitForSecondsRealtime(0.5f);
			Config.paused = true;
			yield return Look("old", s, 80f);
			yield return Look("200", s, 200f);
			yield return Look("whole", s, 1200f);
			Config.paused = false;
			List<Actor> army = new List<Actor>();
			System.Random random = new System.Random(5);
			for (int l = 0; l < 120; l++)
			{
				float num4 = 80f + (float)random.NextDouble() * 120f;
				WorldTile worldTile2 = LandNear(eye + dir * num4 + side * (((float)random.NextDouble() - 0.5f) * num4 * 0.6f));
				if (worldTile2 == null)
				{
					continue;
				}
				try
				{
					Actor actor2 = World.world.units.spawnNewUnit((l % 3 == 0) ? "wolf" : "orc", worldTile2, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
					if (actor2 != null)
					{
						army.Add(actor2);
					}
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Config.paused = true;
			Log.Info($"FAR army of {army.Count}");
			float[] array5 = new float[4] { 80f, 200f, 80f, 200f };
			foreach (float reach in array5)
			{
				yield return Rate(reach, s);
			}
			Config.paused = false;
			s.DetailDistance = savedDetail;
			s.CreatureDistance = savedCreatures;
			s.DrawDistance = savedDraw;
			s.TimeMode = savedTime;
			Finish(quit);
		}

		private IEnumerator Look(string name, Settings s, float reach)
		{
			s.CreatureDistance = reach;
			for (int i = 0; i < 20; i++)
			{
				yield return null;
			}
			_mod.SaveFrame("far_" + name + ".png");
			FrameRenderer renderer = _mod.Renderer;
			int num = 0;
			foreach (Actor item in _placed)
			{
				if (item != null && item.isAlive() && _mod.SceneForTest.Tagged.Contains(item))
				{
					num++;
				}
			}
			Log.Info($"FAR {name}: creature distance {reach:F0}, {renderer.FarActorsDrawn} specks drawn, {num} of {_placed.Count} placed creatures gathered as models or sprites, models {renderer.MeshMs:F1} ms, {renderer.MeshTriangles} triangles");
			FrameProjection shown = _mod.Shown;
			foreach (Actor item2 in _placed)
			{
				if (item2 != null && item2.isAlive())
				{
					Vector2 current_position = item2.current_position;
					float z = _mod.TerrainForTest.SampleHeight(current_position.x, current_position.y) + 0.5f;
					if (shown.Project(current_position.x, current_position.y, z, out var col, out var row, out var depth))
					{
						Log.Info($"FAR at {name} {item2.asset.id} {depth:F0} tiles: col {col:F0} row {row:F0} of {shown.Width}x{shown.Height}");
					}
				}
			}
		}

		private IEnumerator Rate(float reach, Settings s)
		{
			s.CreatureDistance = reach;
			yield return new WaitForSecondsRealtime(1f);
			double frame = 0.0;
			double models = 0.0;
			double mod = 0.0;
			int frames = 0;
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 5f)
			{
				yield return null;
				frame += (double)Time.unscaledDeltaTime * 1000.0;
				models += (double)_mod.Renderer.MeshMs;
				mod += (double)_mod.FrameMs;
				frames++;
			}
			double num = 1.0 / (double)Math.Max(1, frames);
			Log.Info($"FAR rate at creature distance {reach:F0}: {1000.0 / (frame * num):F1} fps, frame {frame * num:F1} ms, models {models * num:F1} ms, mod on the game's thread {mod * num:F1} ms, {_mod.Renderer.MeshTriangles} triangles, {_mod.Renderer.FarActorsDrawn} specks");
		}

		private static WorldTile LandNear(Vector2 p)
		{
			for (int i = 0; i <= 6; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					for (int k = -i; k <= i; k++)
					{
						WorldTile tile = World.world.GetTile((int)p.x + k, (int)p.y + j);
						if (tile != null && tile.main_type != null && !tile.main_type.liquid && tile.building == null)
						{
							return tile;
						}
					}
				}
			}
			return null;
		}

		private void Finish(bool quit)
		{
			Config.paused = false;
			if (_mod != null)
			{
				_mod.DebugEyeSet = false;
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("FAR DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
