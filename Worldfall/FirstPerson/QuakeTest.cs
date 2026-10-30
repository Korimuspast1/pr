using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class QuakeTest : MonoBehaviour
	{
		private struct Blob
		{
			public int Size;

			public Vector2 Centre;

			public int X0;

			public int Y0;

			public int X1;

			public int Y1;
		}

		private WorldBoxMod _mod;

		private Actor _king;

		private string _options;

		private readonly List<string> _views = new List<string>();

		private readonly List<Vector2> _used = new List<Vector2>();

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
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if (!Mountain(item, 30))
				{
					_king = KingTest.FoundKingdom(item, "human", 3, 0);
					if (_king != null)
					{
						break;
					}
				}
			}
			if (_king == null)
			{
				Log.Error("QUAKE could not found a kingdom");
				Done(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("QUAKE first person did not start");
				Done(quit);
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
			s.TimeMode = 1;
			_mod.ForceRelight();
			Log.Info($"QUAKE world {MapBox.width}x{MapBox.height}, king at {_king.current_position}, fov {s.FieldOfView}");
			if (Wants("made"))
			{
				yield return Run("made", Made());
			}
			Dump("quake_before.land");
			if (Wants("lower"))
			{
				yield return Run("lower", Quake(EarthquakeType.BigDecrease));
			}
			if (Wants("raise"))
			{
				yield return Run("raise", Quake(EarthquakeType.BigIncrease));
			}
			if (Wants("meteors"))
			{
				yield return Run("meteors", Meteors());
			}
			if (Wants("lava"))
			{
				yield return Run("lava", Lava());
			}
			if (Wants("nuke"))
			{
				yield return Run("nuke", Nuke());
			}
			Dump("quake_after.land");
			try
			{
				File.WriteAllLines(Path.Combine(Log.Folder, "quake_views.txt"), _views.ToArray());
			}
			catch (Exception ex)
			{
				Log.Error("QUAKE views: " + ex.Message);
			}
			s.TimeMode = savedMode;
			Done(quit);
		}

		private void Done(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("QUAKE DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("QUAKE --- " + name);
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
					Log.Error("QUAKE " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			_mod.SetPitchDegrees(0f);
		}

		private void Dump(string name)
		{
			try
			{
				_mod.SamplerForTest.DumpForLab(Path.Combine(Log.Folder, name));
			}
			catch (Exception ex)
			{
				Log.Error("QUAKE dump: " + ex.Message);
			}
		}

		private static int Code(WorldTile t)
		{
			if (t == null || t.main_type == null || t.main_type.liquid)
			{
				return 0;
			}
			int height_min = t.main_type.height_min;
			if (height_min < 210)
			{
				if (height_min < 199)
				{
					return 1;
				}
				return 2;
			}
			return 3;
		}

		private static bool Mountain(WorldTile c, int r)
		{
			for (int i = -r; i <= r; i += 3)
			{
				for (int j = -r; j <= r; j += 3)
				{
					if (Code(World.world.GetTile(c.x + j, c.y + i)) == 3)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static List<Blob> Blobs(int minCode)
		{
			int width = MapBox.width;
			int height = MapBox.height;
			bool[] array = new bool[width * height];
			List<Blob> list = new List<Blob>();
			Stack<int> stack = new Stack<int>();
			for (int i = 0; i < width * height; i++)
			{
				if (array[i] || Code(World.world.GetTile(i % width, i / width)) < minCode)
				{
					continue;
				}
				Blob item = new Blob
				{
					X0 = int.MaxValue,
					Y0 = int.MaxValue,
					X1 = int.MinValue,
					Y1 = int.MinValue
				};
				float num = 0f;
				float num2 = 0f;
				array[i] = true;
				stack.Push(i);
				while (stack.Count > 0)
				{
					int num3 = stack.Pop();
					int num4 = num3 % width;
					int num5 = num3 / width;
					item.Size++;
					num += (float)num4 + 0.5f;
					num2 += (float)num5 + 0.5f;
					item.X0 = Math.Min(item.X0, num4);
					item.Y0 = Math.Min(item.Y0, num5);
					item.X1 = Math.Max(item.X1, num4);
					item.Y1 = Math.Max(item.Y1, num5);
					for (int j = -1; j <= 1; j++)
					{
						for (int k = -1; k <= 1; k++)
						{
							int num6 = num4 + k;
							int num7 = num5 + j;
							if (num6 >= 0 && num7 >= 0 && num6 < width && num7 < height)
							{
								int num8 = num7 * width + num6;
								if (!array[num8] && Code(World.world.GetTile(num6, num7)) >= minCode)
								{
									array[num8] = true;
									stack.Push(num8);
								}
							}
						}
					}
				}
				item.Centre = new Vector2(num / (float)item.Size, num2 / (float)item.Size);
				list.Add(item);
			}
			return list;
		}

		private IEnumerator Made()
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			List<Blob> list = Blobs(3);
			int[] array = new int[7] { 1, 2, 5, 13, 41, 151, 2147483647 };
			int[] array2 = new int[array.Length - 1];
			foreach (Blob item5 in list)
			{
				for (int i = 0; i < array2.Length; i++)
				{
					if (item5.Size >= array[i] && item5.Size < array[i + 1])
					{
						array2[i]++;
					}
				}
			}
			Log.Info($"QUAKE mountain knots: {list.Count} (1 tile {array2[0]}, 2-4 {array2[1]}, 5-12 {array2[2]}, 13-40 {array2[3]}, 41-150 {array2[4]}, more {array2[5]})");
			foreach (Blob item6 in list)
			{
				if (item6.Size < 2)
				{
					continue;
				}
				float num = 0f;
				float num2 = float.MaxValue;
				for (int j = item6.Y0 - 1; j <= item6.Y1 + 1; j++)
				{
					for (int k = item6.X0 - 1; k <= item6.X1 + 1; k++)
					{
						if (k >= 0 && j >= 0 && k < terrainForTest.Width && j < terrainForTest.Height)
						{
							float b = terrainForTest.Ground[j * terrainForTest.Width + k];
							if (Code(World.world.GetTile(k, j)) == 3)
							{
								num = Mathf.Max(num, b);
							}
							else
							{
								num2 = Mathf.Min(num2, b);
							}
						}
					}
				}
				if (item6.Size <= 60)
				{
					Log.Info($"QUAKE knot of {item6.Size} at {item6.Centre.x:F0},{item6.Centre.y:F0} ({item6.X1 - item6.X0 + 1}x{item6.Y1 - item6.Y0 + 1}): top {num:F1}, foot {num2:F1}");
				}
			}
			List<(string, Blob, float)> list2 = new List<(string, Blob, float)>();
			Vector2 mid = new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f);
			list.Sort((Blob p, Blob q) => (p.Centre - mid).sqrMagnitude.CompareTo((q.Centre - mid).sqrMagnitude));
			int num3 = 0;
			int num4 = 0;
			Blob item = default(Blob);
			foreach (Blob item7 in list)
			{
				if (item7.Size > item.Size)
				{
					item = item7;
				}
				if (item7.Size >= 3 && item7.Size <= 12 && num3 < 2)
				{
					list2.Add(($"small{++num3}", item7, 9f));
				}
				else if (item7.Size >= 13 && item7.Size <= 60 && num4 < 2)
				{
					list2.Add(($"middling{++num4}", item7, 15f));
				}
			}
			if (item.Size > 60)
			{
				list2.Add(("range", item, 30f));
			}
			foreach (var item8 in list2)
			{
				string item2 = item8.Item1;
				Blob item3 = item8.Item2;
				float item4 = item8.Item3;
				Vector2 target = item3.Centre;
				if (item2 == "range")
				{
					target = EdgeOf(item3);
				}
				if (!Look(target, item4, 1.6f, out var eye, out var yaw, out var pitch))
				{
					Log.Info("QUAKE no spot to see " + item2 + " from");
				}
				else
				{
					yield return Shot("made_" + item2, eye, 1.6f, yaw, pitch);
				}
			}
			Blob b2 = default(Blob);
			foreach (Blob item9 in Blobs(2))
			{
				if (item9.Size > b2.Size)
				{
					b2 = item9;
				}
			}
			if (b2.Size > 30 && Look(EdgeOf(b2), 18f, 1.6f, out var eye2, out var yaw2, out var pitch2))
			{
				yield return Shot("made_hills", eye2, 1.6f, yaw2, pitch2);
			}
		}

		private static Vector2 EdgeOf(Blob b)
		{
			Vector2 vector = (new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f) - b.Centre).normalized;
			if (vector.sqrMagnitude < 0.5f)
			{
				vector = Vector2.right;
			}
			int num = Code(World.world.GetTile((int)b.Centre.x, (int)b.Centre.y));
			if (num < 2)
			{
				num = 2;
			}
			Vector2 vector2 = b.Centre;
			for (int i = 0; i < 200; i++)
			{
				Vector2 vector3 = vector2 + vector;
				if (Code(World.world.GetTile((int)vector3.x, (int)vector3.y)) < num)
				{
					break;
				}
				vector2 = vector3;
			}
			return vector2;
		}

		private bool Look(Vector2 target, float dist, float height, out Vector2 eye, out float yaw, out float pitch)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			eye = Vector2.zero;
			yaw = (pitch = 0f);
			float num = float.MaxValue;
			float num2 = terrainForTest.SampleHeight(target.x, target.y);
			for (int i = 0; i < 24; i++)
			{
				for (float num3 = 0.8f; num3 <= 1.21f; num3 += 0.2f)
				{
					float f = (float)i * MathF.PI * 2f / 24f;
					float num4 = dist * num3;
					Vector2 vector = target + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num4;
					if (vector.x < 3f || vector.y < 3f || vector.x > (float)(terrainForTest.Width - 4) || vector.y > (float)(terrainForTest.Height - 4))
					{
						continue;
					}
					int num5 = (int)vector.y * terrainForTest.Width + (int)vector.x;
					if ((terrainForTest.Flags[num5] & 0xB) != 0 || (vector - _king.current_position).magnitude < 8f)
					{
						continue;
					}
					float num6 = terrainForTest.SampleHeight(vector.x, vector.y) + height;
					int num7 = 0;
					for (float num8 = 1f; num8 < num4 - 1.5f; num8 += 0.5f)
					{
						Vector2 vector2 = Vector2.Lerp(vector, target, num8 / num4);
						if (terrainForTest.SampleHeight(vector2.x, vector2.y) > num6 + (num2 + 0.5f - num6) * (num8 / num4))
						{
							num7++;
						}
					}
					float num9 = (float)num7 * 5f + Mathf.Abs(num6 - num2) * 0.3f + Mathf.Abs(num3 - 1f) * 3f;
					if (num9 < num)
					{
						num = num9;
						eye = vector;
						yaw = Mathf.Atan2(target.y - vector.y, target.x - vector.x);
						pitch = Mathf.Clamp(Mathf.Atan2(num2 + 1.5f - num6, num4) * 57.29578f, -30f, 35f);
					}
				}
			}
			return num < float.MaxValue;
		}

		private IEnumerator Shot(string name, Vector2 eye, float height, float yaw, float pitch)
		{
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = height;
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(pitch);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("quake_" + name + ".png");
			Vector3 eyeForTest = _mod.EyeForTest;
			_views.Add(string.Format(CultureInfo.InvariantCulture, "{0} {1:F2} {2:F2} {3:F2} {4:F1} {5:F1} {6:F0}", name, eye.x, eye.y, height, yaw * 57.29578f, pitch, _mod.Settings.FieldOfView));
			Log.Info($"QUAKE shot {name}: eye {eye.x:F1},{eye.y:F1} at {eyeForTest.z:F1}, yaw {yaw * 57.29578f:F0}, pitch {pitch:F0}");
		}

		private IEnumerator Settle()
		{
			yield return new WaitForSecondsRealtime(1f);
			_mod.SamplerForTest.ScanLandformsForTest();
			yield return new WaitForSecondsRealtime(1.5f);
		}

		private bool SiteOf(int code, int minSize, out Vector2 site, float apart = 45f)
		{
			site = Vector2.zero;
			float num = -1f;
			foreach (Blob item in Blobs(code))
			{
				if (item.Size < minSize)
				{
					continue;
				}
				for (int i = item.Y0; i <= item.Y1; i++)
				{
					for (int j = item.X0; j <= item.X1; j++)
					{
						if (Code(World.world.GetTile(j, i)) < code)
						{
							continue;
						}
						Vector2 vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						if ((vector - _king.current_position).magnitude < 60f)
						{
							continue;
						}
						bool flag = true;
						foreach (Vector2 item2 in _used)
						{
							if ((vector - item2).magnitude < apart)
							{
								flag = false;
							}
						}
						if (!flag)
						{
							continue;
						}
						int k;
						for (k = 1; k < 12; k++)
						{
							bool flag2 = false;
							for (int l = 0; l < 16; l++)
							{
								if (flag2)
								{
									break;
								}
								float f = (float)l * MathF.PI / 8f;
								flag2 = Code(World.world.GetTile((int)(vector.x + Mathf.Cos(f) * (float)k), (int)(vector.y + Mathf.Sin(f) * (float)k))) < code;
							}
							if (flag2)
							{
								break;
							}
						}
						if ((float)k > num)
						{
							num = k;
							site = vector;
						}
					}
				}
			}
			if (num > 0f)
			{
				_used.Add(site);
			}
			return num > 0f;
		}

		private IEnumerator Quake(EarthquakeType type)
		{
			bool flag = type == EarthquakeType.BigDecrease;
			string name = (flag ? "lower" : "raise");
			if (!(flag ? SiteOf(3, 40, out var site) : (SiteOf(2, 30, out site) || SiteOf(1, 200, out site))))
			{
				Log.Error("QUAKE no site to " + name);
				yield break;
			}
			List<(Vector2 eye, float h, float yaw, float pitch)> eyes = new List<(Vector2, float, float, float)>();
			if (Look(site, 26f, 6f, out var eye, out var yaw, out var pitch))
			{
				eyes.Add((eye, 6f, yaw, pitch));
			}
			if (Look(site, 12f, 1.6f, out var eye2, out var yaw2, out var pitch2))
			{
				eyes.Add((eye2, 1.6f, yaw2, pitch2));
			}
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"{name}{k}_before", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
			Dictionary<int, TileTypeBase> before = Snapshot(site, 40);
			Earthquake.startQuake(World.world.GetTile((int)site.x, (int)site.y), type);
			float start = Time.unscaledTime;
			yield return new WaitForSecondsRealtime(1f);
			while (Earthquake.isQuakeActive() && Time.unscaledTime - start < 45f)
			{
				yield return null;
			}
			int num = Changed(before, site, 40);
			Log.Info($"QUAKE earthquake ({name}) at {site} over after {Time.unscaledTime - start:F1} s: {num} tiles changed type");
			yield return Settle();
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"{name}{k}_after", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
		}

		private IEnumerator Meteors()
		{
			if (!SiteOf(3, 12, out var range, 30f) && !SiteOf(2, 12, out range, 30f))
			{
				Log.Error("QUAKE no range for the meteors");
				yield break;
			}
			List<(Vector2 eye, float h, float yaw, float pitch)> eyes = new List<(Vector2, float, float, float)>();
			if (Look(range, 24f, 5f, out var eye, out var yaw, out var pitch))
			{
				eyes.Add((eye, 5f, yaw, pitch));
			}
			if (Look(range, 11f, 1.6f, out var eye2, out var yaw2, out var pitch2))
			{
				eyes.Add((eye2, 1.6f, yaw2, pitch2));
			}
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"meteors{k}_before", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
			Dictionary<int, TileTypeBase> before = Snapshot(range, 20);
			System.Random rng = new System.Random(5);
			for (int k = 0; k < 5; k++)
			{
				WorldTile tile = World.world.GetTile((int)range.x + rng.Next(-7, 8), (int)range.y + rng.Next(-7, 8));
				if (tile != null)
				{
					Meteorite.spawnMeteorite(tile);
				}
				yield return new WaitForSecondsRealtime(0.6f);
			}
			yield return new WaitForSecondsRealtime(9f);
			Log.Info($"QUAKE meteors at {range}: {Changed(before, range, 20)} tiles changed type, {SceneCollector.ImpactCount} strikes seen");
			yield return Settle();
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"meteors{k}_after", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
		}

		private IEnumerator Lava()
		{
			if (!SiteOf(2, 30, out var site))
			{
				Log.Error("QUAKE no hills for the lava");
				yield break;
			}
			Blob b = default(Blob);
			foreach (Blob item in Blobs(2))
			{
				if (site.x >= (float)item.X0 && site.x <= (float)(item.X1 + 1) && site.y >= (float)item.Y0 && site.y <= (float)(item.Y1 + 1) && item.Size > b.Size)
				{
					b = item;
				}
			}
			Vector2 foot = ((b.Size > 0) ? EdgeOf(b) : site);
			List<(Vector2 eye, float h, float yaw, float pitch)> eyes = new List<(Vector2, float, float, float)>();
			if (Look(foot, 16f, 3f, out var eye, out var yaw, out var pitch))
			{
				eyes.Add((eye, 3f, yaw, pitch));
			}
			if (Look(foot, 8f, 1.6f, out var eye2, out var yaw2, out var pitch2))
			{
				eyes.Add((eye2, 1.6f, yaw2, pitch2));
			}
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"lava{k}_before", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					if (j * j + i * i > 5)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile((int)foot.x + j, (int)foot.y + i);
					if (tile != null)
					{
						try
						{
							World.world.drop_manager.spawn(tile, "lava", 3f, -1f, -1L);
						}
						catch (Exception ex)
						{
							Log.Error("QUAKE lava drop: " + ex.Message);
							yield break;
						}
					}
				}
			}
			yield return new WaitForSecondsRealtime(12f);
			int num = 0;
			for (int l = -12; l <= 12; l++)
			{
				for (int m = -12; m <= 12; m++)
				{
					WorldTile tile2 = World.world.GetTile((int)foot.x + m, (int)foot.y + l);
					if (tile2 != null && tile2.main_type != null && tile2.main_type.lava)
					{
						num++;
					}
				}
			}
			Log.Info($"QUAKE lava poured at {foot}: {num} lava tiles round it after 12 s");
			yield return Settle();
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"lava{k}_after", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
		}

		private IEnumerator Nuke()
		{
			if (!SiteOf(2, 30, out var site) && !SiteOf(1, 300, out site))
			{
				Log.Error("QUAKE no site for the bomb");
				yield break;
			}
			List<(Vector2 eye, float h, float yaw, float pitch)> eyes = new List<(Vector2, float, float, float)>();
			if (Look(site, 40f, 8f, out var eye, out var yaw, out var pitch))
			{
				eyes.Add((eye, 8f, yaw, pitch));
			}
			if (Look(site, 18f, 1.6f, out var eye2, out var yaw2, out var pitch2))
			{
				eyes.Add((eye2, 1.6f, yaw2, pitch2));
			}
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"nuke{k}_before", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
			Dictionary<int, TileTypeBase> before = Snapshot(site, 30);
			try
			{
				World.world.drop_manager.spawn(World.world.GetTile((int)site.x, (int)site.y), "atomic_bomb", 10f, -1f, -1L);
			}
			catch (Exception ex)
			{
				Log.Error("QUAKE bomb: " + ex.Message);
				yield break;
			}
			yield return new WaitForSecondsRealtime(70f);
			Log.Info($"QUAKE bomb at {site}: {Changed(before, site, 30)} tiles changed type");
			yield return Settle();
			for (int k = 0; k < eyes.Count; k++)
			{
				yield return Shot($"nuke{k}_after", eyes[k].eye, eyes[k].h, eyes[k].yaw, eyes[k].pitch);
			}
		}

		private static Dictionary<int, TileTypeBase> Snapshot(Vector2 c, int r)
		{
			Dictionary<int, TileTypeBase> dictionary = new Dictionary<int, TileTypeBase>();
			for (int i = (int)c.y - r; (float)i <= c.y + (float)r; i++)
			{
				for (int j = (int)c.x - r; (float)j <= c.x + (float)r; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null)
					{
						dictionary[i * MapBox.width + j] = tile.main_type;
					}
				}
			}
			return dictionary;
		}

		private static int Changed(Dictionary<int, TileTypeBase> before, Vector2 c, int r)
		{
			int num = 0;
			foreach (KeyValuePair<int, TileTypeBase> item in before)
			{
				WorldTile tile = World.world.GetTile(item.Key % MapBox.width, item.Key / MapBox.width);
				if (tile != null && tile.main_type != item.Value)
				{
					num++;
				}
			}
			return num;
		}
	}
}
