using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PerfTest : MonoBehaviour
	{
		private const float Stretch = 12f;

		private WorldBoxMod _mod;

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			bool both = text.Contains("async");
			bool cull = text.Contains("cull");
			bool pool = text.Contains("pool");
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
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 12, 4);
				if (king != null)
				{
					Log.Info($"PERF village at {item.x},{item.y}");
					break;
				}
			}
			if (king == null)
			{
				Log.Error("PERF could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(4f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("PERF first person did not start");
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
			bool savedBeside = s.AsyncRender;
			Log.Info($"PERF {_mod.Renderer.Width}x{_mod.Renderer.Height}, view distance {_mod.Renderer.MaxDistance}, {SystemInfo.processorType} ({SystemInfo.processorCount} threads)");
			(string, int, bool)[] array = new(string, int, bool)[4]
			{
				("day", 1, false),
				("golden", 2, false),
				("night", 3, true),
				("cycle", 0, false)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (name, mode, lantern) = array[i];
				if (pool)
				{
					for (int round = 1; round <= 2; round++)
					{
						FrameRenderer.UseWorkerPool = false;
						yield return Scenario(name + " Parallel.For " + round, mode, lantern, s);
						FrameRenderer.UseWorkerPool = true;
						yield return Scenario(name + " worker pool " + round, mode, lantern, s);
					}
				}
				else if (cull)
				{
					FrameRenderer.SkipHidden = false;
					yield return Scenario(name + " drawing hidden models", mode, lantern, s);
					FrameRenderer.SkipHidden = true;
					yield return Scenario(name + " skipping hidden models", mode, lantern, s);
				}
				else if (!both)
				{
					yield return Scenario(name, mode, lantern, s);
				}
				else
				{
					s.AsyncRender = false;
					yield return Scenario(name + " in step", mode, lantern, s);
					s.AsyncRender = true;
					yield return Scenario(name + " beside", mode, lantern, s);
				}
			}
			s.TimeMode = savedMode;
			s.AsyncRender = savedBeside;
			Finish(quit);
		}

		private IEnumerator Scenario(string name, int timeMode, bool lantern, Settings s)
		{
			s.TimeMode = timeMode;
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.ResetPerf();
			double frame = 0.0;
			double mod = 0.0;
			double terrain = 0.0;
			double objects = 0.0;
			double models = 0.0;
			double post = 0.0;
			float worst = 0f;
			int frames = 0;
			int gc0 = GC.CollectionCount(0);
			float start = Time.unscaledTime;
			float yaw = 0f;
			while (Time.unscaledTime - start < 12f)
			{
				float num = Mathf.Floor((Time.unscaledTime - start) / 3f) * MathF.PI * 0.5f;
				if (num != yaw)
				{
					yaw = num;
					_mod.SetYaw(yaw);
				}
				yield return null;
				if (!_mod.IsFirstPerson)
				{
					Log.Error("PERF first person ended");
					yield break;
				}
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				frame += (double)unscaledDeltaTime * 1000.0;
				worst = Mathf.Max(worst, unscaledDeltaTime * 1000f);
				mod += (double)_mod.FrameMs;
				FrameRenderer renderer = _mod.Renderer;
				terrain += (double)renderer.TerrainMs;
				objects += (double)renderer.ObjectsMs;
				models += (double)renderer.MeshMs;
				post += (double)(renderer.PostMs - renderer.MeshMs);
				frames++;
			}
			double num2 = 1.0 / (double)Math.Max(1, frames);
			Log.Info($"PERF {name}: {1000.0 / (frame * num2):F1} fps, frame {frame * num2:F1} ms (worst {worst:F0}), mod {mod * num2:F1} ms; " + $"terrain {terrain * num2:F2}, sprites {objects * num2:F2}, models {models * num2:F2}, light and finish {post * num2:F2}; {frames} frames, {GC.CollectionCount(0) - gc0} collections");
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
			Log.Info("PERF DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
