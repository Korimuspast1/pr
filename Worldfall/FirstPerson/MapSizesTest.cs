using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace FirstPerson
{
	internal sealed class MapSizesTest : MonoBehaviour
	{
		private struct MemoryStatus
		{
			public uint Length;

			public uint Load;

			public ulong TotalPhys;

			public ulong AvailPhys;

			public ulong TotalPageFile;

			public ulong AvailPageFile;

			public ulong TotalVirtual;

			public ulong AvailVirtual;

			public ulong AvailExtendedVirtual;
		}

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private double _bytesPerTile;

		private long _basePriv;

		private long _baseTiles;

		private long _maxTiles;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			yield return Settle(60f);
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			GC.Collect();
			_basePriv = PrivateBytes();
			_baseTiles = (_maxTiles = (long)MapBox.width * (long)MapBox.height);
			MapSizes.Tick();
			string[] sizes = MapSizeLibrary.getSizes();
			Log.Info("MS sizes: " + string.Join(", ", sizes));
			int num = Array.IndexOf(sizes, "iceberg");
			Check("the game's own sizes come first, iceberg the last of them", num == 7, "at " + num);
			int num2 = 9;
			for (int i = 0; i < MapSizes.Bigger.Count; i++)
			{
				MapSizes.Size size = MapSizes.Bigger[i];
				MapSizeAsset mapSizeAsset = AssetManager.map_sizes.get(size.Id);
				Check(size.Name + " follows iceberg in order", mapSizeAsset != null && Array.IndexOf(sizes, size.Id) == num + 1 + i && size.Blocks > num2, $"at {Array.IndexOf(sizes, size.Id)}, {size.Blocks} blocks");
				num2 = size.Blocks;
				Sprite sprite = null;
				try
				{
					sprite = mapSizeAsset?.getIconSprite();
				}
				catch
				{
				}
				Check(size.Name + " has an icon", sprite != null, size.Icon);
				string text = null;
				try
				{
					text = LocalizedTextManager.getText("map_size_" + size.Id);
				}
				catch
				{
				}
				Check(size.Name + " has its name in the window", text == Lang.T(size.Name), text);
			}
			LogIcons();
			List<int> list = new List<int>();
			int startIndex = options.IndexOf("mapsizes", StringComparison.Ordinal) + "mapsizes".Length;
			string[] array = options.Substring(startIndex).Split(new char[4] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			for (int j = 0; j < array.Length; j++)
			{
				if (int.TryParse(array[j], out var result) && result >= 2 && result <= 64)
				{
					list.Add(result);
				}
			}
			foreach (int item in list)
			{
				bool go = true;
				yield return Measure(item, delegate(bool ok)
				{
					go = ok;
				});
				if (!go)
				{
					break;
				}
			}
			Done(quit);
		}

		private IEnumerator Measure(int blocks, Action<bool> carryOn)
		{
			int tiles = blocks * 64;
			string id = MapSizes.EnsureForTest(blocks);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return Wait(1f);
			GC.Collect();
			long num = AvailableBytes();
			if (_bytesPerTile > 0.0 && num > 0)
			{
				double num2 = _bytesPerTile * (double)Math.Max(0L, (long)tiles * (long)tiles - _maxTiles) * 1.25;
				if ((double)num - num2 < 2500000000.0)
				{
					Log.Info($"MS {tiles}: skipped (and the bigger ones): about {num2 / 1000000000.0:F1} GB more needed, {(double)num / 1000000000.0:F1} GB free");
					carryOn(obj: false);
					yield break;
				}
			}
			PrivateBytes();
			Stopwatch sw = Stopwatch.StartNew();
			Config.customMapSize = id;
			World.world.generateNewMap();
			yield return Settle(900f);
			double genS = sw.Elapsed.TotalSeconds;
			Config.autosaves = false;
			Check($"{tiles}: the world is {tiles} tiles a side", MapBox.width == tiles && MapBox.height == tiles, $"{MapBox.width}x{MapBox.height}");
			yield return Wait(1f);
			Mem($"{tiles} made in {genS:F1} s", out var privMade);
			sw.Restart();
			int want = Mathf.Clamp(blocks * blocks / 4, 4, 256);
			int made = 0;
			int people = 0;
			string[] kinds = new string[4] { "human", "elf", "orc", "dwarf" };
			List<WorldTile> sites = KingTest.GoodSites();
			for (int i = 0; i < sites.Count; i++)
			{
				if (made >= want)
				{
					break;
				}
				Actor actor = null;
				try
				{
					actor = KingTest.FoundKingdom(sites[i], kinds[made % kinds.Length], 6, 2);
				}
				catch
				{
				}
				if (actor != null)
				{
					made++;
					if (made % 16 == 0)
					{
						yield return null;
					}
				}
			}
			try
			{
				people = World.world.units.Count;
			}
			catch
			{
			}
			Log.Info($"MS {tiles}: {made} villages founded of {sites.Count} sites in {sw.Elapsed.TotalSeconds:F1} s, {people} creatures");
			Config.paused = false;
			yield return Wait(8f);
			yield return Frames($"{tiles} map", 240, turn: false);
			Actor host = Middle();
			if (host != null)
			{
				ControllableUnit.setControllableCreature(host);
				yield return Wait(4f);
			}
			if (host == null || !_mod.IsFirstPerson)
			{
				Check($"{tiles}: first person started", ok: false, (host == null) ? "no one" : "not in first person");
			}
			else
			{
				_mod.HintUntil = 0f;
				try
				{
					ScrollWindow.hideAllEvent(pWithAnimation: false);
				}
				catch
				{
				}
				Settings s = _mod.Settings;
				s.GpuRender = true;
				s.GpuLook = 1;
				s.PixelDistance = 1;
				s.Resolution = 2;
				s.DrawDistance = 1200f;
				s.DetailDistance = 240f;
				s.CreatureDistance = 1200f;
				yield return Wait(4f);
				_mod.SaveFrame($"ms_{tiles}_whole.png");
				yield return Frames($"{tiles} fp whole", 240, turn: true);
				s.DrawDistance = 270f;
				s.DetailDistance = 80f;
				s.CreatureDistance = 200f;
				yield return Wait(3f);
				_mod.SaveFrame($"ms_{tiles}_default.png");
				yield return Frames($"{tiles} fp default", 240, turn: true);
				Mem($"{tiles} in first person", out var _);
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				yield return Wait(2f);
			}
			Config.paused = true;
			byte[] zip = null;
			sw.Restart();
			try
			{
				zip = SaveManager.currentWorldToSavedMap().toZip();
			}
			catch (Exception ex)
			{
				Check($"{tiles}: saved", ok: false, ex.Message);
			}
			double saveS = sw.Elapsed.TotalSeconds;
			double loadS = 0.0;
			if (zip != null)
			{
				sw.Restart();
				bool flag = false;
				try
				{
					SaveManager.loadMapFromBytes(zip);
					flag = true;
				}
				catch (Exception ex2)
				{
					Check($"{tiles}: loaded back", ok: false, ex2.Message);
				}
				if (flag)
				{
					yield return Settle(900f);
					loadS = sw.Elapsed.TotalSeconds;
					Config.autosaves = false;
					Check($"{tiles}: loaded back at its size", MapBox.width == tiles && MapBox.height == tiles, $"{MapBox.width}x{MapBox.height}");
				}
			}
			Config.paused = false;
			yield return Wait(2f);
			Mem($"{tiles} loaded back", out var priv2);
			Log.Info($"MS {tiles}: save {(double)((zip != null) ? zip.Length : 0) / 1000000.0:F1} MB in {saveS:F1} s, load {loadS:F1} s");
			long num3 = Math.Max(privMade, priv2) - _basePriv;
			long num4 = (long)tiles * (long)tiles;
			if (num3 > 0 && num4 > _baseTiles)
			{
				_bytesPerTile = Math.Max(_bytesPerTile, (double)num3 / (double)(num4 - _baseTiles));
			}
			_maxTiles = Math.Max(_maxTiles, num4);
			Log.Info($"MS {tiles}: about {_bytesPerTile:F0} bytes of process memory per tile over the start world");
			carryOn(obj: true);
		}

		private IEnumerator Frames(string what, int count, bool turn)
		{
			List<float> frame = new List<float>(count);
			List<float> mod = new List<float>(count);
			float yaw0 = _mod.ViewYaw;
			for (int i = 0; i < count; i++)
			{
				if (turn && i % (count / 4) == 0)
				{
					_mod.SetYaw(yaw0 + MathF.PI / 2f * (float)(i / (count / 4)));
				}
				float t0 = Time.realtimeSinceStartup;
				yield return null;
				frame.Add((Time.realtimeSinceStartup - t0) * 1000f);
				mod.Add(_mod.FrameMs);
			}
			frame.Sort();
			mod.Sort();
			string text = (turn ? $"; mod median {mod[count / 2]:F1} ms, 90% {mod[count * 9 / 10]:F1}" : "");
			Log.Info($"MS perf {what}: frame median {frame[count / 2]:F1} ms, 90% {frame[count * 9 / 10]:F1}, worst {frame[count - 1]:F1}{text}");
		}

		private void Mem(string what, out long priv)
		{
			long totalMemory = GC.GetTotalMemory(forceFullCollection: true);
			priv = PrivateBytes();
			long num = 0L;
			long num2 = 0L;
			long num3 = 0L;
			try
			{
				num = Profiler.GetMonoHeapSizeLong();
				num2 = Profiler.GetTotalReservedMemoryLong();
				num3 = Profiler.GetAllocatedMemoryForGraphicsDriver();
			}
			catch
			{
			}
			Log.Info($"MS mem {what}: managed {(double)totalMemory / 1000000.0:F0} MB (mono heap {(double)num / 1000000.0:F0}), process {(double)priv / 1000000.0:F0} MB, unity reserved {(double)num2 / 1000000.0:F0} MB, graphics {(double)num3 / 1000000.0:F0} MB, free {(double)AvailableBytes() / 1000000000.0:F1} GB");
		}

		private static long PrivateBytes()
		{
			try
			{
				using (Process process = Process.GetCurrentProcess())
				{
					return process.PrivateMemorySize64;
				}
			}
			catch
			{
				return 0L;
			}
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

		private static long AvailableBytes()
		{
			try
			{
				MemoryStatus status = new MemoryStatus
				{
					Length = (uint)Marshal.SizeOf(typeof(MemoryStatus))
				};
				return (long)(GlobalMemoryStatusEx(ref status) ? status.AvailPhys : 0);
			}
			catch
			{
				return 0L;
			}
		}

		private static Actor Middle()
		{
			Actor result = null;
			float num = float.MaxValue;
			Vector2 vector = new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f);
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.city != null && simple.isAdult())
				{
					float sqrMagnitude = (simple.current_position - vector).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = simple;
					}
				}
			}
			return result;
		}

		private static void LogIcons()
		{
			try
			{
				List<string> list = new List<string>();
				Sprite[] array = Resources.LoadAll<Sprite>("ui/Icons");
				foreach (Sprite sprite in array)
				{
					string text = sprite.name.ToLowerInvariant();
					string[] array2 = new string[20]
					{
						"world", "map", "planet", "globe", "ice", "earth", "continent", "island", "titan", "ocean",
						"size", "galax", "star", "moon", "sun", "snow", "frozen", "glacier", "big", "giant"
					};
					foreach (string value in array2)
					{
						if (text.Contains(value))
						{
							list.Add(sprite.name);
							break;
						}
					}
				}
				list.Sort(StringComparer.Ordinal);
				Log.Info("MS icons: " + string.Join(" ", list));
			}
			catch (Exception ex)
			{
				Log.Info("MS icons: " + ex.Message);
			}
		}

		private static IEnumerator Settle(float limit)
		{
			for (float t = 0f; t < limit; t += 0.25f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t >= 2f)
				{
					break;
				}
			}
		}

		private static IEnumerator Wait(float s)
		{
			yield return new WaitForSecondsRealtime(s);
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
			Log.Info("MS " + (ok ? "PASS" : "FAIL") + " " + what + (string.IsNullOrEmpty(detail) ? "" : (" (" + detail + ")")));
			return ok;
		}

		private void Done(bool quit)
		{
			Log.Info($"MS {_pass} passed, {_fail} failed");
			Log.Info("MS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
