using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GodWallsTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private readonly List<WorldTile> _walls = new List<WorldTile>();

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
			int savedMode = s.TimeMode;
			bool savedGod = s.GodView;
			s.TimeMode = 1;
			s.GodView = true;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			WorldTile worldTile = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					worldTile = enumerator.Current;
				}
			}
			if (worldTile == null)
			{
				Log.Error("GW no open site");
				Finish(quit);
				yield break;
			}
			Vector2 at = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			Build(worldTile.x, worldTile.y);
			yield return Zoom(at, 30f, 3f);
			float[] array = new float[5] { 10f, 15f, 20f, 30f, 40f };
			for (int i = 0; i < array.Length; i++)
			{
				float t = array[i];
				yield return Zoom(at, t, 1.5f);
				Count(out var pieces, out var boxes, out var inView);
				float wallModelRange = _mod.Renderer.WallModelRange;
				float maxDistance = _mod.Renderer.MaxDistance;
				Log.Info($"GW zoom {t:0}: {pieces} wall pieces ({boxes} boxes) for {inView} of {_walls.Count} wall tiles in sight, renderer wall range {wallModelRange:F0}, reach {maxDistance:F0}");
				Check($"zoom {t:0}: the renderer's wall range reaches as far as the frame ({wallModelRange:F0} >= {maxDistance:F0})", wallModelRange >= maxDistance - 0.5f);
				Check($"zoom {t:0}: every wall tile in sight is a piece ({pieces} pieces, {inView} in sight)", inView > 0 && pieces >= inView);
				if (t >= 40f)
				{
					Check($"zoom {t:0}: the farthest are boxes ({boxes})", boxes > 0);
				}
				yield return Timed($"zoom {t:0} pieces", t, at);
				_mod.SaveFrame($"gw_z{t:0}_pieces.png");
				SceneCollector.WallModelsOffForTest = true;
				yield return Zoom(at, t, 1f);
				yield return Timed($"zoom {t:0} plain", t, at);
				_mod.SaveFrame($"gw_z{t:0}_plain.png");
				SceneCollector.WallModelsOffForTest = false;
			}
			Clear();
			s.GodView = savedGod;
			s.TimeMode = savedMode;
			Log.Info($"GW {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private void Build(int cx, int cy)
		{
			Ring("wall_order", cx - 15, cy - 11, cx + 15, cy + 11, 3);
			Ring("wall_evil", cx + 20, cy - 6, cx + 30, cy + 2, 0);
			Ring("wall_green", cx - 28, cy + 2, cx - 20, cy + 8, 2);
			Log.Info($"GW {_walls.Count} wall tiles round {cx},{cy}");
			void Ring(string type, int x0, int y0, int x1, int y1, int gate)
			{
				TopTileType topTileType = AssetManager.top_tiles.get(type);
				if (topTileType == null)
				{
					Log.Error("GW no wall type " + type);
				}
				else
				{
					for (int i = x0; i <= x1; i++)
					{
						int[] array = new int[2] { y0, y1 };
						foreach (int y2 in array)
						{
							if (gate <= 0 || Math.Abs(i - (x0 + x1) / 2) > gate / 2)
							{
								Place(i, y2, topTileType);
							}
						}
					}
					for (int k = y0 + 1; k < y1; k++)
					{
						Place(x0, k, topTileType);
						Place(x1, k, topTileType);
					}
				}
			}
		}

		private void Place(int x, int y, TopTileType wall)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null || tile.main_type == null || tile.main_type.liquid)
			{
				return;
			}
			try
			{
				tile.setTopTileType(wall);
				_walls.Add(tile);
			}
			catch
			{
			}
		}

		private void Clear()
		{
			TopTileType topTileType = AssetManager.top_tiles.get("grass_low");
			foreach (WorldTile wall in _walls)
			{
				try
				{
					if (topTileType != null)
					{
						wall.setTopTileType(topTileType);
					}
				}
				catch
				{
				}
			}
		}

		private void Count(out int pieces, out int boxes, out int inView)
		{
			pieces = (boxes = (inView = 0));
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (!mesh.ShadowOnly && mesh.Model != null && mesh.Model.Name != null && mesh.Model.Name.StartsWith("wall_", StringComparison.Ordinal))
				{
					pieces++;
					if (mesh.Model.Name.EndsWith("_x", StringComparison.Ordinal))
					{
						boxes++;
					}
				}
			}
			ViewState view = GodView.View;
			int width = GodView.Width;
			int height = GodView.Height;
			if (width <= 0 || height <= 0)
			{
				return;
			}
			float num = (float)width * 0.5f / Mathf.Tan(view.Fov * 0.5f);
			float num2 = (float)height * 0.5f + view.Pitch * num;
			float num3 = Mathf.Cos(view.Yaw);
			float num4 = Mathf.Sin(view.Yaw);
			TerrainCache terrainForTest = _mod.TerrainForTest;
			foreach (WorldTile wall in _walls)
			{
				float num5 = (float)wall.x + 0.5f - view.X;
				float num6 = (float)wall.y + 0.5f - view.Y;
				float num7 = num5 * num3 + num6 * num4;
				float num8 = num5 * num4 - num6 * num3;
				float maxDistance = _mod.Renderer.MaxDistance;
				if (!(num7 < 1f) && !(num5 * num5 + num6 * num6 >= maxDistance * maxDistance))
				{
					float num9 = terrainForTest.Heights[wall.y * terrainForTest.Width + wall.x];
					float num10 = (float)width * 0.5f + num8 / num7 * num;
					float num11 = num2 - (num9 - view.Z) / num7 * num;
					if (num10 >= 0f && num10 < (float)width && num11 >= 0f && num11 < (float)height)
					{
						inView++;
					}
				}
			}
		}

		private IEnumerator Timed(string what, float zoom, Vector2 at)
		{
			float models = 0f;
			float frame = 0f;
			int frames = 0;
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
			{
				Hold(at, zoom);
				yield return null;
				models += _mod.Renderer.MeshMs;
				frame += _mod.FrameMs;
				frames++;
			}
			Log.Info($"GW {what}: models {models / (float)Mathf.Max(1, frames):F2} ms, mod frame {frame / (float)Mathf.Max(1, frames):F2} ms, {frames} frames");
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
			Log.Info("GW " + (ok ? "PASS" : "FAIL") + " " + what);
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
				Log.Error("GW camera: " + ex.Message);
			}
		}

		private void Finish(bool quit)
		{
			SceneCollector.WallModelsOffForTest = false;
			Log.Info("GW DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
