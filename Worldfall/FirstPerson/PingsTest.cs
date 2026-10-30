using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PingsTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			bool screens = text.Contains("shots");
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
			int savedTime = s.TimeMode;
			int savedRes = s.Resolution;
			bool savedMinimap = s.Minimap;
			float savedReach = s.MinimapReach;
			s.TimeMode = 1;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			s.Minimap = true;
			s.MinimapReach = 40f;
			Actor king = null;
			WorldTile village = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 8, 2);
				if (king != null)
				{
					village = item;
					break;
				}
			}
			if (king == null)
			{
				Log.Error("PING could not found a village");
				Finish(quit, s, savedTime, savedRes, savedMinimap, savedReach);
				yield break;
			}
			yield return new WaitForSecondsRealtime(3f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("PING first person did not start");
				Finish(quit, s, savedTime, savedRes, savedMinimap, savedReach);
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
			Hud hud = _mod.HudForTest;
			TerrainCache land = _mod.TerrainForTest;
			Vector2 home = new Vector2((float)village.x + 0.5f, (float)village.y + 0.5f);
			yield return new WaitForSecondsRealtime(3f);
			int count = World.world.units.getSimpleList().Count;
			Check("the sweep keeps a map for the world's creatures", Explored.Count >= Math.Min(30, count / 2), $"{Explored.Count} maps for {count} creatures");
			Explored.Map km = Explored.Of(king);
			Check("the king's map knows his town", km != null && Explored.SeenAt(km, home.x, home.y), (km != null) ? (km.Cells + " cells") : "none");
			Vector2 far = FindLand(home, 70f, land);
			Actor sheep = Spawn("sheep", far);
			yield return new WaitForSecondsRealtime(0.5f);
			Explored.Map sm = ((sheep != null) ? Explored.Of(sheep) : null);
			Check("a sheep far off knows its own ground", sm != null && Explored.SeenAt(sm, far.x, far.y), (sm != null) ? (sm.Cells + " cells") : "no sheep");
			Check("...not the king's town", sm != null && !Explored.SeenAt(sm, home.x, home.y));
			Check("...and the king doesn't know the sheep's ground", !Explored.SeenAt(km, far.x, far.y));
			if (sheep != null && sm != null)
			{
				Vector2 vector = FindLand(far, 24f, land);
				int before = sm.Cells;
				try
				{
					sheep.setCurrentTilePosition(World.world.GetTile((int)vector.x, (int)vector.y));
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(3f);
				Check("a creature that moves sees more", sm.Cells > before && Explored.SeenAt(sm, sheep.current_position.x, sheep.current_position.y), $"{before} -> {sm.Cells} cells");
			}
			Vector2 me = king.current_position;
			Vector2 dir = OpenDirection(me, land, 85f);
			Vector2 at80 = me + dir * 80f;
			Vector2 at81 = me + dir * 30f;
			Check("80 tiles out not known at first", !Explored.SeenAt(km, at80.x, at80.y));
			_mod.DebugEye = me;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 12f;
			_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			float y = land.SampleHeight(me.x, me.y) + 12f - land.SampleHeight(at80.x, at80.y);
			_mod.SetPitchDegrees((0f - Mathf.Atan2(y, 80f)) * 57.29578f);
			yield return new WaitForSecondsRealtime(1f);
			Check("what you look at is known (30 tiles ahead)", Explored.SeenAt(km, at81.x, at81.y));
			Check("...not yet 80 out", !Explored.SeenAt(km, at80.x, at80.y));
			Binoculars b = _mod.Binoculars;
			b.Press();
			yield return null;
			b.Release();
			yield return new WaitForSecondsRealtime(1.25f);
			Check("through binoculars 80 tiles out is known", Explored.SeenAt(km, at80.x, at80.y), $"range {_mod.BinocularsRange:F0}");
			int count2 = _mod.CompassMarks().Count;
			bool flag2 = _mod.PingForTest(null);
			Ping lastPing = _mod.LastPing;
			float num = ((lastPing != null) ? (new Vector2(lastPing.At.x, lastPing.At.y) - me).magnitude : (-1f));
			float binocularsRange = _mod.BinocularsRange;
			Check("binoculars: the ping lands where they rest, far off", flag2 && lastPing != null && lastPing.Far && num > 20f && Mathf.Abs(num - binocularsRange) < Mathf.Max(3f, binocularsRange * 0.08f), $"{num:F0} tiles, resting on {binocularsRange:F0}, {lastPing?.Kind}");
			Check("the compass shows it", _mod.CompassMarks().Count > count2);
			yield return null;
			yield return null;
			Check("its mark is drawn in the view", hud.PingsInView > 0, $"{hud.PingsInView} in view, {hud.PingsAtEdge} at the edge");
			yield return Screen("binoculars", screens);
			b.Press();
			yield return null;
			b.Release();
			yield return new WaitForSecondsRealtime(0.6f);
			Vector2 pdir;
			Vector2 spot = OpenSpot(home, land, out pdir);
			Vector2 pside = new Vector2(0f - pdir.y, pdir.x);
			Log.Info($"PING open ground at {spot.x:F0},{spot.y:F0}, looking {Mathf.Atan2(pdir.y, pdir.x) * 57.29578f:F0} deg");
			_mod.DebugEye = spot;
			_mod.DebugEyeHeight = 3f;
			Actor goat = Spawn("sheep", spot + pdir * 6f);
			Config.paused = true;
			yield return LookAt(goat, spot);
			flag2 = _mod.PingForTest(null);
			Ping p = _mod.LastPing;
			Check("a tap on a sheep: look, following it", flag2 && p != null && p.Kind == PingKind.Look && p.Actor == goat, (p != null) ? (p.Kind.ToString() + ((p.Actor != null) ? (" on " + WorldBoxMod.Describe(p.Actor)) : " on the ground")) : "none");
			int count3 = _mod.Pings.List.Count;
			flag2 = _mod.PingForTest(null);
			Check("pinging it again takes it away", flag2 && _mod.Pings.List.Count == count3 - 1, $"{count3} -> {_mod.Pings.List.Count}");
			Config.paused = false;
			Vector2 foeAt = spot + pdir * 7f + pside * 1.5f;
			Actor foe = Spawn("bandit", foeAt);
			yield return new WaitForSecondsRealtime(0.6f);
			try
			{
				foe.setCurrentTilePosition(World.world.GetTile((int)foeAt.x, (int)foeAt.y));
			}
			catch
			{
			}
			yield return null;
			Config.paused = true;
			for (int before = 0; before < 10; before++)
			{
				yield return null;
			}
			yield return LookAt(foe, spot);
			flag2 = _mod.PingForTest(null);
			p = _mod.LastPing;
			Check("a tap on a foe: enemy", flag2 && p != null && p.Kind == PingKind.Enemy && p.Actor == foe, (p != null) ? (p.Kind.ToString() + ", " + _mod.LastRallied + " warriors answered") : "none");
			yield return null;
			yield return null;
			Check("the enemy mark is drawn", hud.PingsInView > 0, hud.PingsInView + " in view");
			yield return Screen("enemy", screens);
			_mod.SetYaw(Mathf.Atan2(0f - pdir.y, 0f - pdir.x));
			_mod.SetPitchDegrees(-35f);
			for (int before = 0; before < 6; before++)
			{
				yield return null;
			}
			flag2 = _mod.PingForTest(PingKind.Go);
			p = _mod.LastPing;
			Check("go here on the ground", flag2 && p != null && p.Kind == PingKind.Go && p.Actor == null, (p != null) ? $"{(new Vector2(p.At.x, p.At.y) - spot).magnitude:F1} tiles from the eye" : "none");
			yield return new WaitForSecondsRealtime(1.1f);
			Check("getting there clears it", p != null && _mod.Pings.Update(new Vector2(p.At.x, p.At.y)) && !_mod.Pings.List.Contains(p));
			Check("the wheel: up is look", WorldBoxMod.WheelSegment(new Vector2(0f, 1f)) == 0);
			Check("the wheel: down-right is enemy", WorldBoxMod.WheelSegment(new Vector2(Mathf.Sin(MathF.PI * 2f / 3f), Mathf.Cos(MathF.PI * 2f / 3f))) == 2);
			Check("the wheel: straight down is danger", WorldBoxMod.WheelSegment(new Vector2(0f, -1f)) == 3);
			Check("the wheel: the middle cancels", WorldBoxMod.WheelSegment(new Vector2(0.1f, 0.1f)) == -1);
			_mod.WheelForTest(open: true, 2);
			yield return null;
			yield return null;
			Check("the wheel is drawn", hud.WheelDrawn);
			yield return Screen("wheel", screens);
			_mod.WheelForTest(open: false, -1);
			Config.paused = false;
			_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			_mod.SetPitchDegrees(-5f);
			yield return new WaitForSecondsRealtime(0.6f);
			Minimap mm = _mod.Minimap;
			Check("the minimap is drawn", hud.MinimapDrawn && mm.Land != null && mm.Fog != null, $"land {mm.LandMs:F2} ms x{mm.LandBuilds}, fog {mm.FogMs:F3} ms x{mm.FogBuilds}");
			yield return Screen("minimap", screens);
			s.Minimap = false;
			yield return null;
			yield return null;
			Check("N off: no minimap", !hud.MinimapDrawn);
			s.Minimap = true;
			_mod.BigMapForTest(open: true);
			yield return new WaitForSecondsRealtime(0.3f);
			Check("the whole explored map is drawn", hud.BigMapDrawn && mm.WorldLand != null && mm.WorldFog != null, $"{mm.WorldMs:F1} ms, {Explored.Share(km) * 100f:F1}% explored");
			SaveMap(mm.Land, mm.Fog, mm.TexSize, "ping_minimap_tex.png");
			SaveMap(mm.WorldLand, mm.WorldFog, Mathf.Max(mm.WorldW, mm.WorldH), "ping_worldmap_tex.png");
			yield return Screen("bigmap", screens);
			_mod.BigMapForTest(open: false);
			yield return null;
			Explored.TotalMs = 0.0;
			Explored.TotalFrames = 0;
			Explored.WorstMs = 0.0;
			yield return new WaitForSecondsRealtime(4f);
			double num2 = Explored.TotalMs / (double)Math.Max(1, Explored.TotalFrames);
			Check("the sweep is cheap", num2 < 0.35, $"{num2:F3} ms a frame (worst {Explored.WorstMs:F2}), {Explored.Count} maps");
			_mod.DebugEyeSet = false;
			yield return Reload(king, km, at80, home);
			Log.Info($"PING {_pass} passed, {_fail} failed");
			Finish(quit, s, savedTime, savedRes, savedMinimap, savedReach);
		}

		private IEnumerator Reload(Actor king, Explored.Map km, Vector2 farPoint, Vector2 home)
		{
			bool farKnown = Explored.SeenAt(km, farPoint.x, farPoint.y);
			long id = king.data.id;
			int cells = km.Cells;
			int num = Explored.WriteAll();
			king.data.get("fp_seen", out var pResult, null);
			Check("the maps are written into the creatures' custom data", num > 0 && !string.IsNullOrEmpty(pResult), $"{num} written, the king's {pResult?.Length ?? 0} chars");
			byte[] zip;
			try
			{
				SavedMap savedMap = SaveManager.currentWorldToSavedMap();
				string text = savedMap.toJson();
				Check("the save holds the maps", text.Contains("fp_seen"), text.Length + " chars");
				zip = savedMap.toZip();
			}
			catch (Exception ex)
			{
				Check("the world saved to bytes", ok: false, ex.Message);
				yield break;
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(1f);
			Explored.Reset();
			try
			{
				SaveManager.loadMapFromBytes(zip);
			}
			catch (Exception ex2)
			{
				Check("the world loaded from bytes", ok: false, ex2.Message);
				yield break;
			}
			for (float t = 0f; t < 60f; t += 0.5f)
			{
				yield return new WaitForSecondsRealtime(0.5f);
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t > 2f)
				{
					break;
				}
			}
			yield return new WaitForSecondsRealtime(1f);
			Actor actor = null;
			try
			{
				actor = World.world.units.get(id);
			}
			catch
			{
			}
			if (Check("the king is back after the reload", actor?.isAlive() ?? false))
			{
				int reads = Explored.Reads;
				Explored.Map map = Explored.Of(actor);
				Check("his map came back", map != null && map.Cells >= cells && Explored.SeenAt(map, home.x, home.y) && Explored.SeenAt(map, farPoint.x, farPoint.y) == farKnown, $"{cells} -> {map?.Cells} cells, {Explored.Reads - reads} read");
			}
		}

		private IEnumerator LookAt(Actor a, Vector2 eye)
		{
			if (a != null)
			{
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				Vector3 eyeNow = _mod.EyeForTest;
				TerrainCache land = _mod.TerrainForTest;
				Vector3 head;
				Vector3 aim = (_mod.StarHead(a, out head) ? new Vector3(head.x, head.y, head.z - 0.3f) : new Vector3(a.current_position.x, a.current_position.y, land.SampleHeight(a.current_position.x, a.current_position.y) + 0.35f));
				Vector2 d = new Vector2(aim.x - eyeNow.x, aim.y - eyeNow.y);
				_mod.SetYaw(Mathf.Atan2(d.y, d.x));
				_mod.SetPitchDegrees((0f - Mathf.Atan2(eyeNow.z - aim.z, d.magnitude)) * 57.29578f);
				float wantDeg = (0f - Mathf.Atan2(eyeNow.z - aim.z, d.magnitude)) * 57.29578f;
				for (int i = 0; i < 6; i++)
				{
					yield return null;
				}
				Vector3 eyeForTest = _mod.EyeForTest;
				Log.Info($"PING aim: eye {eyeNow.x:F1},{eyeNow.y:F1},{eyeNow.z:F2} (land {land.SampleHeight(eyeNow.x, eyeNow.y):F2}) -> {eyeForTest.x:F1},{eyeForTest.y:F1},{eyeForTest.z:F2}, aim {aim.x:F1},{aim.y:F1},{aim.z:F2}; pitch wanted {wantDeg:F1}, view {Mathf.Atan(_mod.View.Pitch) * 57.29578f:F1}, shown tilt {_mod.Shown.FrameLens.Tilt * 57.29578f:F1} steep {_mod.Shown.FrameLens.Steep}");
				_mod.SaveFrame("ping_look_" + ((a.asset != null) ? a.asset.id : "it") + ".png");
				Log.Info(string.Format("PING pick depth {0:F1}, {1} at {2:F1},{3:F1}, inside {4}", _mod.Renderer.PickDepth, a.isAlive() ? "alive" : "dead", a.current_position.x, a.current_position.y, a.is_inside_building));
				Log.Info(string.Format("PING looking at {0} {1:F1} tiles off: picked {2}", WorldBoxMod.Describe(a), d.magnitude, (_mod.Picked is Actor a2) ? WorldBoxMod.Describe(a2) : ((_mod.Picked != null) ? _mod.Picked.GetType().Name : "nothing")));
			}
		}

		private static Actor Spawn(string kind, Vector2 at)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile == null)
			{
				return null;
			}
			try
			{
				return World.world.units.spawnNewUnit(kind, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("PING could not spawn " + kind + ": " + ex.Message);
				return null;
			}
		}

		private static Vector2 OpenSpot(Vector2 from, TerrainCache land, out Vector2 dir)
		{
			for (float num = 10f; num <= 40f; num += 3f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					for (int j = 0; j < 8; j++)
					{
						float f2 = (float)j * MathF.PI / 4f;
						Vector2 vector2 = new Vector2(Mathf.Cos(f2), Mathf.Sin(f2));
						Vector2 vector3 = new Vector2(0f - vector2.y, vector2.x);
						bool flag = true;
						for (int k = -1; k <= 10 && flag; k++)
						{
							for (int l = -2; l <= 2 && flag; l++)
							{
								Vector2 vector4 = vector + vector2 * k + vector3 * l;
								WorldTile tile = World.world.GetTile((int)vector4.x, (int)vector4.y);
								if (tile == null || tile.main_type == null || tile.main_type.liquid || (tile.building != null && AssetStyle.IsSolid(tile.building)))
								{
									flag = false;
								}
							}
						}
						if (flag)
						{
							dir = vector2;
							return new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
						}
					}
				}
			}
			dir = Vector2.right;
			return from + new Vector2(14f, 0f);
		}

		private static Vector2 FindLand(Vector2 from, float distance, TerrainCache land)
		{
			for (int i = 0; i < 24; i++)
			{
				float f = (float)i * MathF.PI / 12f;
				Vector2 vector = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * distance;
				if (!(vector.x < 4f) && !(vector.y < 4f) && !(vector.x > (float)(MapBox.width - 5)) && !(vector.y > (float)(MapBox.height - 5)))
				{
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid)
					{
						return new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
					}
				}
			}
			return from;
		}

		private static Vector2 OpenDirection(Vector2 from, TerrainCache land, float reach)
		{
			List<KeyValuePair<Vector2, float>> list = new List<KeyValuePair<Vector2, float>>();
			try
			{
				foreach (Building building in World.world.buildings)
				{
					if (building != null && building.asset != null && building.current_tile != null && building.asset.id.StartsWith("celestial_tree"))
					{
						list.Add(new KeyValuePair<Vector2, float>(new Vector2((float)building.current_tile.x + 0.5f, (float)building.current_tile.y + 0.5f), building.asset.id.Contains("small") ? 8f : 22f));
					}
				}
			}
			catch
			{
			}
			Vector2 result = Vector2.right;
			float num = float.MaxValue;
			float a = land.SampleHeight(from.x, from.y) + 4f;
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
				Vector2 b = from + vector * reach;
				if (b.x < 3f || b.y < 3f || b.x > (float)(MapBox.width - 4) || b.y > (float)(MapBox.height - 4))
				{
					continue;
				}
				float b2 = land.SampleHeight(b.x, b.y);
				float num2 = -99f;
				for (int j = 1; j < 30; j++)
				{
					float t = (float)j / 30f;
					Vector2 vector2 = Vector2.Lerp(from, b, t);
					num2 = Mathf.Max(num2, land.SampleHeight(vector2.x, vector2.y) - Mathf.Lerp(a, b2, t));
				}
				foreach (KeyValuePair<Vector2, float> item in list)
				{
					Vector2 vector3 = item.Key - from;
					float magnitude = (vector3 - vector * Mathf.Clamp(Vector2.Dot(vector3, vector), 0f, reach)).magnitude;
					if (magnitude < item.Value)
					{
						num2 += (item.Value - magnitude) * 4f;
					}
				}
				if (num2 < num)
				{
					num = num2;
					result = vector;
				}
			}
			return result;
		}

		private static void SaveMap(Texture2D land, Texture2D fog, float tiles, string name)
		{
			if (land == null)
			{
				return;
			}
			try
			{
				Color32[] pixels = land.GetPixels32();
				Color32[] array = ((fog != null) ? fog.GetPixels32() : null);
				int width = land.width;
				int num = ((fog != null) ? fog.width : 0);
				int num2 = ((width <= 256) ? 4 : 2);
				int num3 = width * num2;
				Color32[] array2 = new Color32[num3 * num3];
				for (int i = 0; i < num3; i++)
				{
					for (int j = 0; j < num3; j++)
					{
						int num4 = j / num2;
						int num5 = i / num2;
						Color color = pixels[num5 * width + num4];
						if (array != null)
						{
							float num6 = ((float)num4 + 0.5f) / (float)width * tiles / 4f;
							float num7 = ((float)num5 + 0.5f) / (float)width * tiles / 4f;
							int num8 = Mathf.Clamp((int)num6, 0, num - 1);
							int num9 = Mathf.Clamp((int)num7, 0, num - 1);
							Color color2 = array[num9 * num + num8];
							color = Color.Lerp(color, new Color(color2.r, color2.g, color2.b), color2.a * 0.97f);
						}
						color.a = 1f;
						array2[i * num3 + j] = color;
					}
				}
				Texture2D texture2D = new Texture2D(num3, num3, TextureFormat.RGBA32, mipChain: false);
				texture2D.SetPixels32(array2);
				texture2D.Apply(updateMipmaps: false);
				File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D);
				Log.Info("PING saved " + name);
			}
			catch (Exception ex)
			{
				Log.Error("PING could not save " + name + ": " + ex.Message);
			}
		}

		private IEnumerator Screen(string name, bool screens)
		{
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			_mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "ping_" + name + "_hud.png"));
			for (int i = 0; i < 6; i++)
			{
				if (!_mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			if (screens)
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "ping_" + name + "_screen.png"));
				yield return null;
				yield return null;
			}
		}

		private bool Check(string what, bool ok, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("PING " + (ok ? "PASS" : "FAIL") + " " + what + ((detail != null) ? (": " + detail) : ""));
			return ok;
		}

		private void Finish(bool quit, Settings s, int time, int res, bool minimap, float reach)
		{
			Config.paused = false;
			s.TimeMode = time;
			s.Resolution = res;
			s.Minimap = minimap;
			s.MinimapReach = reach;
			if (_mod != null)
			{
				_mod.DebugEyeSet = false;
				_mod.Pings.Clear();
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("PING DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
