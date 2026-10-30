using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GodSkyTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private bool _gpu;

		private string Card
		{
			get
			{
				if (!_gpu)
				{
					return "";
				}
				return "_gpu";
			}
		}

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
			_gpu = File.ReadAllText(path).Contains("oncard");
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
			int savedMode = s.TimeMode;
			bool savedGod = s.GodView;
			bool savedGpu = s.GpuRender;
			s.TimeMode = 1;
			s.GodView = true;
			s.GpuRender = _gpu;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			WorldTile site = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					WorldTile current = enumerator.Current;
					site = current;
				}
			}
			if (site == null)
			{
				Log.Error("GS no open site");
				Finish(quit);
				yield break;
			}
			Vector2 at = new Vector2((float)site.x + 0.5f, (float)site.y + 0.5f);
			yield return Zoom(at, 30f, 2f);
			yield return Mountains();
			if (!_gpu)
			{
				yield return Clouds(site);
				yield return Falling(site);
			}
			_mod.GodCutOffForTest = false;
			s.GodView = savedGod;
			s.GpuRender = savedGpu;
			s.TimeMode = savedMode;
			Log.Info($"GS {_pass} passed, {_fail} failed");
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
			Log.Info("GS " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Zoom(Vector2 at, float zoom, float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Hold(at, zoom);
				yield return null;
			}
		}

		private static void Hold(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				if (World.world.move_camera != null)
				{
					World.world.move_camera.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("GS camera: " + ex.Message);
			}
		}

		private static float CentreOff(Vector2 from, out Vec3 shown)
		{
			shown = default(Vec3);
			List<KeyValuePair<float, Vec3>> list = new List<KeyValuePair<float, Vec3>>();
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					float column = (float)GodView.Width * 0.5f + (float)j * 3f;
					float row = (float)GodView.Height * 0.5f + (float)i * 3f;
					if (GodCamera.DrawnPoint(GodView.View, GodView.Width, GodView.Height, GodView.Depth, column, row, out var point))
					{
						list.Add(new KeyValuePair<float, Vec3>(Mathf.Sqrt((point.X - from.x) * (point.X - from.x) + (point.Y - from.y) * (point.Y - from.y)), point));
					}
				}
			}
			if (list.Count == 0)
			{
				return float.MaxValue;
			}
			list.Sort((KeyValuePair<float, Vec3> a, KeyValuePair<float, Vec3> b) => a.Key.CompareTo(b.Key));
			shown = list[list.Count / 2].Value;
			return list[list.Count / 2].Key;
		}

		private IEnumerator Mountains()
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				Log.Error("GS no land read");
				yield break;
			}
			int bx = -1;
			int by = -1;
			float num = 0f;
			float peak = 0f;
			for (int i = 6; i < terrainForTest.Height - 30; i += 2)
			{
				for (int j = 6; j < terrainForTest.Width - 6; j += 2)
				{
					float num2 = terrainForTest.Heights[i * terrainForTest.Width + j];
					if (!(num2 < 6f))
					{
						float num3 = Mathf.Max(terrainForTest.SampleHeight((float)j + 0.5f, (float)i + 15.5f), 0f);
						if (num2 - num3 > num)
						{
							num = num2 - num3;
							peak = num2;
							bx = j;
							by = i;
						}
					}
				}
			}
			Log.Info(string.Format("GS tallest rise over the land north of it: {0}, highest land {1:F1}", (bx < 0) ? "none" : $"{bx},{by}, {peak:F1} high, {num:F1} over the land 15 tiles north", terrainForTest.MaxHeight));
			if (bx < 0 || num < 4f)
			{
				Log.Error("GS no mountain to look past");
				yield break;
			}
			Vector2[] array = new Vector2[3]
			{
				new Vector2(12f, 1.23f),
				new Vector2(20f, 1.23f),
				new Vector2(40f, 0.75f)
			};
			for (int k = 0; k < array.Length; k++)
			{
				Vector2 vector = array[k];
				float zoom = vector.x;
				Vector2 look = new Vector2((float)bx + 0.5f, (float)by + 0.5f + vector.y * zoom);
				_mod.GodCutOffForTest = true;
				yield return Zoom(look, zoom, 1.2f);
				Vec3 without;
				float offWithout = CentreOff(look, out without);
				_mod.SaveFrame($"gs_mountain_z{zoom:0}_off{Card}.png");
				_mod.GodCutOffForTest = false;
				yield return Zoom(look, zoom, 1.2f);
				Vec3 shown;
				float num4 = CentreOff(look, out shown);
				_mod.SaveFrame($"gs_mountain_z{zoom:0}_on{Card}.png");
				SightCut godCutForTest = _mod.GodCutForTest;
				Log.Info($"GS mountain zoom {zoom}: looking at {look.x:F1},{look.y:F1}; the centre shows {without.X:F1},{without.Y:F1},{without.Z:F1} ({offWithout:F1} off) without the cut, " + string.Format("{0:F1},{1:F1},{2:F1} ({3:F1} off) with it; cut {4}, frame {5:F1} ms", shown.X, shown.Y, shown.Z, num4, godCutForTest.On ? $"to {godCutForTest.Target:F0} tiles ahead" : "off", _mod.FrameMs));
				Check($"zoom {zoom}: with the cut the view's centre shows the land it looks at, past the mountain", godCutForTest.On && (num4 < 6f || (shown.Y > (float)by + 4f && shown.Z < peak * 0.5f && num4 < 15f)));
			}
		}

		private IEnumerator Clouds(WorldTile site)
		{
			Vector2 at = new Vector2((float)site.x + 0.5f, (float)site.y + 0.5f);
			try
			{
				EffectsLibrary.spawn("fx_cloud", site, "cloud_rain");
			}
			catch (Exception ex)
			{
				Log.Error("GS cloud: " + ex.Message);
				yield break;
			}
			Vector2 cloud = at;
			Vector2 half = new Vector2(6f, 6f);
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				Hold(at, 14f);
				yield return null;
				Track(ref cloud, ref half);
			}
			Weather w = _mod.SceneForTest.Weather;
			float behind = w.Base + w.Thickness * 0.5f - Mathf.Max(_mod.TerrainForTest.SampleHeight(cloud.x, cloud.y), 0f);
			Log.Info($"GS cloud at {cloud.x:F1},{cloud.y:F1} (spawned over {at.x:F1},{at.y:F1}), {half.x:F1} by {half.y:F1} tiles; it covers the ground {behind:F1} tiles north of it");
			float[] array = new float[2] { 14f, 30f };
			for (int i = 0; i < array.Length; i++)
			{
				float t = array[i];
				bool[] array2 = new bool[2] { false, true };
				foreach (bool aside in array2)
				{
					Vector2 shift = new Vector2(0f, aside ? Aside(cloud, half.y, t) : behind);
					float off = 0f;
					float on = 0f;
					float kept = 1f;
					for (int pass = 0; pass < 2; pass++)
					{
						_mod.GodCutOffForTest = pass == 0;
						float most = 0f;
						for (float t2 = 0f; t2 < 1.4f; t2 += Time.unscaledDeltaTime)
						{
							Track(ref cloud, ref half);
							Hold(cloud + shift, t);
							yield return null;
							if (t2 > 0.6f)
							{
								most = Mathf.Max(most, aside ? _mod.Renderer.CloudCover : _mod.Renderer.CloudCentreCover);
							}
						}
						if (pass == 0)
						{
							off = most;
						}
						else
						{
							on = most;
						}
						if (pass == 1)
						{
							kept = _mod.GodCutForTest.Storm(cloud.x, cloud.y, half.x, half.y, w.Base, w.Base + w.Thickness);
						}
						_mod.SaveFrame(string.Format("gs_cloud_z{0:0}_{1}_{2}.png", t, aside ? "aside" : "inline", (pass == 0) ? "off" : "on"));
					}
					_mod.GodCutOffForTest = false;
					Log.Info(string.Format("GS cloud {0} the line of sight, zoom {1}: covers {2:F3} of the {3} without the cut, {4:F3} with it (the cut keeps {5:F2} of it), looking {6:F1} tiles north of it", aside ? "clear of" : "in", t, off, aside ? "frame" : "middle", on, kept, shift.y));
					if (!aside)
					{
						Check($"zoom {t}: a rain cloud in the line of sight is cut away", off > 0.1f && on < off * 0.15f);
					}
					else if (t >= 30f)
					{
						Check($"zoom {t}: a rain cloud clear of the line of sight still shows", off > 0.005f && on > off * 0.7f);
					}
				}
			}
		}

		private float Aside(Vector2 cloud, float halfY, float zoom)
		{
			Weather weather = _mod.SceneForTest.Weather;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = 0f;
			float num2 = zoom * 0.35f;
			for (int i = 0; i < 3; i++)
			{
				float num3 = 0f;
				for (int j = -2; j <= 2; j++)
				{
					for (int k = -2; k <= 2; k++)
					{
						num3 += Mathf.Max(terrainForTest.SampleHeight(cloud.x + (float)k * num2 * 0.5f, cloud.y + num + (float)j * num2 * 0.5f), 0f);
					}
				}
				num = weather.Base - num3 / 25f - (1.9f * halfY + 1f);
			}
			return num;
		}

		private void Track(ref Vector2 cloud, ref Vector2 half)
		{
			float num = float.MaxValue;
			foreach (StormCloud storm in _mod.SceneForTest.Weather.Storms)
			{
				float num2 = Vector2.Distance(new Vector2(storm.X, storm.Y), cloud);
				if (num2 < num && num2 < 12f)
				{
					num = num2;
					cloud = new Vector2(storm.X, storm.Y);
					half = new Vector2(storm.HalfX, storm.HalfY);
				}
			}
		}

		private IEnumerator Falling(WorldTile site)
		{
			Vector2 at = new Vector2((float)site.x + 0.5f, (float)site.y + 0.5f);
			yield return Zoom(at, 40f, 1.5f);
			WorldTile bt = World.world.GetTile(site.x + 3, site.y + 2);
			try
			{
				World.world.drop_manager.spawn(bt, "bomb", 10f, -1f, -1L);
			}
			catch (Exception ex)
			{
				Log.Error("GS bomb: " + ex.Message);
				yield break;
			}
			int seen = 0;
			int falling = 0;
			int shots = 0;
			float most = 0f;
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				Hold(at, 40f);
				yield return null;
				foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
				{
					if (mesh.Model != null && mesh.Model.Name != null && mesh.Model.Name.StartsWith("bomb_") && !(Mathf.Abs(mesh.Position.X - ((float)bt.x + 0.5f)) > 3f) && !(Mathf.Abs(mesh.Position.Y - ((float)bt.y + 0.5f)) > 3f))
					{
						seen++;
						float num = _mod.TerrainForTest.SampleHeight(mesh.Position.X, mesh.Position.Y);
						if (mesh.Position.Z > num + 0.5f)
						{
							falling++;
						}
						most = Mathf.Max(most, Pixels(mesh.Position.X, mesh.Position.Y, Mathf.Abs(mesh.Basis.C2.Z) * (mesh.Model.Max.Z - mesh.Model.Min.Z)));
						break;
					}
				}
				if (shots < 4 && t > (float)shots * 0.35f)
				{
					_mod.SaveFrame($"gs_bomb{shots++}.png");
				}
			}
			Log.Info($"GS bomb from zoom {40f}: drawn {seen} frames ({falling} of them falling), at most {most:F1} pixels tall");
			Check("a bomb dropped from the sky is drawn on its way down", falling > 0);
			Check("and big enough to see from up there (5+ pixels)", most >= 5f);
			List<Vector2> spots = new List<Vector2>();
			try
			{
				for (int i = 0; i < 8; i++)
				{
					WorldTile tile = World.world.GetTile(site.x - 4 + i, site.y - 3 + (i & 1) * 2);
					if (tile != null)
					{
						World.world.drop_manager.spawn(tile, "acid", 10f, -1f, -1L);
						spots.Add(new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f));
					}
				}
			}
			catch (Exception ex2)
			{
				Log.Error("GS acid: " + ex2.Message);
			}
			int falling2 = 0;
			int drawn = 0;
			float smallest = float.MaxValue;
			for (int k = 0; k < 4; k++)
			{
				Hold(at, 40f);
				yield return null;
			}
			try
			{
				foreach (Drop drop in World.world.drop_manager._drops)
				{
					if (drop == null || !drop.active || drop._asset == null || drop._asset.id != "acid")
					{
						continue;
					}
					falling2++;
					Vector2 current_position = drop.current_position;
					foreach (Billboard billboard in _mod.SceneForTest.Billboards)
					{
						if (billboard.X == current_position.x && billboard.Y == current_position.y)
						{
							drawn++;
							smallest = Mathf.Min(smallest, Pixels(billboard.X, billboard.Y, Mathf.Max(billboard.Width, billboard.Height)));
							break;
						}
					}
				}
			}
			catch (Exception ex3)
			{
				Log.Error("GS acid drops: " + ex3.Message);
			}
			_mod.SaveFrame("gs_acid.png");
			Log.Info($"GS acid: {spots.Count} dropped, {falling2} in the air, {drawn} of them drawn, the smallest {((drawn > 0) ? smallest : 0f):F1} pixels");
			Check("a power's drops are drawn falling from the sky, a few pixels big", falling2 > 0 && drawn == falling2 && smallest >= 1.9f);
			int before = SceneCollector.ImpactCount;
			WorldTile mt = World.world.GetTile(site.x - 6, site.y + 5);
			try
			{
				Meteorite.spawnMeteorite(mt);
			}
			catch (Exception ex4)
			{
				Log.Error("GS meteor: " + ex4.Message);
				yield break;
			}
			float wait = 0f;
			int mshots = 0;
			int sideSmoke = 0;
			while (SceneCollector.ImpactCount <= before && wait < 15f)
			{
				Hold(at, 40f);
				wait += Time.unscaledDeltaTime;
				yield return null;
				if (mshots < 8 && wait > (float)mshots * 0.4f)
				{
					_mod.SaveFrame($"gs_meteor{mshots++}.png");
				}
				sideSmoke = Mathf.Max(sideSmoke, SideSmoke(mt));
			}
			Log.Info($"GS meteor: {sideSmoke} puffs of the game's slide smoke at most");
			Check("no smoke trail laid in from the meteor's side", sideSmoke <= 2);
			yield return Zoom(at, 40f, 0.5f);
			_mod.SaveFrame("gs_meteor_strike.png");
			Log.Info($"GS meteor: struck after {wait:F1} s ({SceneCollector.ImpactCount} strikes playing)");
			Check("a meteor's strike is seen from the sky at map zoom 40", SceneCollector.ImpactCount > before);
		}

		private static int SideSmoke(WorldTile target)
		{
			int num = 0;
			BaseEffectController baseEffectController = World.world.stack_effects.get("fx_fire_smoke");
			if (baseEffectController?._list == null || target == null)
			{
				return 0;
			}
			for (int i = 0; i < Math.Min(baseEffectController._active_index, baseEffectController._list.Count); i++)
			{
				BaseEffect baseEffect = baseEffectController._list[i];
				if (baseEffect != null && baseEffect.active && ((Vector2)baseEffect.transform.position - new Vector2(target.x, target.y)).sqrMagnitude > 36f)
				{
					num++;
				}
			}
			return num;
		}

		private static float Pixels(float x, float y, float size)
		{
			ViewState view = GodView.View;
			if (GodView.Width <= 0)
			{
				return 0f;
			}
			float num = (x - view.X) * Mathf.Cos(view.Yaw) + (y - view.Y) * Mathf.Sin(view.Yaw);
			float num2 = (float)GodView.Width * 0.5f / Mathf.Tan(view.Fov * 0.5f);
			if (!(num > 0.5f))
			{
				return 0f;
			}
			return size * num2 / num;
		}

		private void Finish(bool quit)
		{
			if (_mod != null)
			{
				_mod.GodCutOffForTest = false;
			}
			Log.Info("GS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
