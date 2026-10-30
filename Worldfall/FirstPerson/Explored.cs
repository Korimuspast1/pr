using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace FirstPerson
{
	internal static class Explored
	{
		internal sealed class Map
		{
			public long Id;

			public readonly Dictionary<int, ulong> Blocks = new Dictionary<int, ulong>();

			public int Cells;

			public int Version;

			public int LastCell = -1;

			public int SeenCycle;

			public Rect SeenBounds;

			public int BoundsVersion = -1;

			public bool Dirty;

			public float DirtySince;
		}

		public const int Cell = 4;

		public const int BlockCells = 8;

		public const string Key = "fp_seen";

		public const float Sight = 10f;

		private const float TownZone = 6.5f;

		public static int PerFrame = 160;

		public static double BudgetMs = 0.2;

		private const float WriteAfter = 30f;

		private static readonly Dictionary<long, Map> Maps = new Dictionary<long, Map>();

		private static readonly List<Actor> Batch = new List<Actor>(256);

		private static readonly List<long> Gone = new List<long>();

		private static object _world;

		private static int _cursor;

		private static int _cycle;

		private static int _w;

		private static int _h;

		public static double TotalMs;

		public static double WorstMs;

		public static int TotalFrames;

		public static int Writes;

		public static int Reads;

		public static int Seeded;

		private static double _logMs;

		private static int _logFrames;

		public static int Count => Maps.Count;

		public static int CellsWide => (_w + 4 - 1) / 4;

		public static int CellsHigh => (_h + 4 - 1) / 4;

		public static int WorldCells => Math.Max(1, CellsWide * CellsHigh);

		public static void Tick()
		{
			if (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				return;
			}
			long timestamp = Stopwatch.GetTimestamp();
			CheckWorld();
			List<Actor> simpleList = World.world.units.getSimpleList();
			int count = simpleList.Count;
			if (count == 0)
			{
				return;
			}
			if (_cursor >= count)
			{
				_cursor = 0;
				_cycle++;
				Prune();
			}
			int num = Math.Min(count, _cursor + PerFrame);
			Batch.Clear();
			for (int i = _cursor; i < num; i++)
			{
				Batch.Add(simpleList[i]);
			}
			long num2 = (long)(BudgetMs * (double)Stopwatch.Frequency / 1000.0);
			float unscaledTime = Time.unscaledTime;
			int num3 = 0;
			foreach (Actor item in Batch)
			{
				num3++;
				try
				{
					Visit(item, unscaledTime);
				}
				catch (Exception ex)
				{
					Log.Error("explored: " + ex.Message);
				}
				if (Stopwatch.GetTimestamp() - timestamp > num2)
				{
					break;
				}
			}
			_cursor += num3;
			double num4 = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			TotalMs += num4;
			TotalFrames++;
			WorstMs = Math.Max(WorstMs, num4);
			_logMs += num4;
			if (++_logFrames >= 3600)
			{
				Log.Info($"explored: {_logMs / (double)_logFrames:F3} ms a frame (worst {WorstMs:F2}), {Maps.Count} maps kept, {Reads} read, {Seeded} new, {Writes} written, {_cycle} sweeps");
				_logFrames = 0;
				_logMs = 0.0;
			}
		}

		private static void Visit(Actor a, float now)
		{
			if (!Tracked(a))
			{
				return;
			}
			Map map = Of(a);
			if (map != null)
			{
				map.SeenCycle = _cycle;
				Vector2 current_position = a.current_position;
				int num = CellIndex(current_position.x, current_position.y);
				if (num != map.LastCell)
				{
					map.LastCell = num;
					Reveal(map, current_position, 10f);
				}
				if (map.Dirty && now - map.DirtySince > 30f)
				{
					Write(a, map);
				}
			}
		}

		public static bool Tracked(Actor a)
		{
			if (a == null || !a.isAlive() || a.asset == null)
			{
				return false;
			}
			return !a.asset.is_boat;
		}

		private static void CheckWorld()
		{
			object map_stats = World.world.map_stats;
			int num = 0;
			int num2 = 0;
			try
			{
				num = MapBox.width;
				num2 = MapBox.height;
			}
			catch
			{
			}
			if (map_stats != _world || num != _w || num2 != _h)
			{
				_world = map_stats;
				_w = num;
				_h = num2;
				Maps.Clear();
				_cursor = 0;
			}
		}

		private static void CheckWorldSafe()
		{
			try
			{
				if (World.world != null)
				{
					CheckWorld();
				}
			}
			catch
			{
			}
		}

		public static void Reset()
		{
			_world = null;
			Maps.Clear();
			_cursor = 0;
		}

		private static void Prune()
		{
			Gone.Clear();
			foreach (KeyValuePair<long, Map> map in Maps)
			{
				if (_cycle - map.Value.SeenCycle > 2)
				{
					Gone.Add(map.Key);
				}
			}
			foreach (long item in Gone)
			{
				Maps.Remove(item);
			}
		}

		public static Map Of(Actor a)
		{
			if (a == null)
			{
				return null;
			}
			CheckWorldSafe();
			long id;
			try
			{
				id = a.data.id;
			}
			catch
			{
				return null;
			}
			if (Maps.TryGetValue(id, out var value))
			{
				return value;
			}
			value = new Map
			{
				Id = id,
				SeenCycle = _cycle
			};
			Maps[id] = value;
			string pResult = null;
			try
			{
				a.data.get("fp_seen", out pResult, null);
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(pResult) && Parse(pResult, value))
			{
				Reads++;
			}
			else
			{
				Seed(a, value);
				Seeded++;
			}
			return value;
		}

		public static bool Has(Actor a)
		{
			try
			{
				return a != null && Maps.ContainsKey(a.data.id);
			}
			catch
			{
				return false;
			}
		}

		private static void Seed(Actor a, Map m)
		{
			try
			{
				Vector2 current_position = a.current_position;
				m.LastCell = CellIndex(current_position.x, current_position.y);
				Reveal(m, current_position, 10f);
				City city = a.city;
				if (city != null && city.isAlive() && city.zones != null)
				{
					int num = 0;
					foreach (TileZone zone in city.zones)
					{
						if (zone != null && zone.centerTile != null)
						{
							Reveal(m, new Vector2((float)zone.centerTile.x + 0.5f, (float)zone.centerTile.y + 0.5f), 6.5f);
							if (++num >= 160)
							{
								break;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("explored: could not begin a map: " + ex.Message);
			}
			m.Dirty = false;
		}

		private static int CellIndex(float x, float y)
		{
			int num = Mathf.FloorToInt(x / 4f);
			return Mathf.FloorToInt(y / 4f) * 65536 + num;
		}

		private static int BlockKey(int bx, int by)
		{
			return (by << 16) | (bx & 0xFFFF);
		}

		public static bool Seen(Map m, int cx, int cy)
		{
			if (m == null || cx < 0 || cy < 0)
			{
				return false;
			}
			if (!m.Blocks.TryGetValue(BlockKey(cx / 8, cy / 8), out var value))
			{
				return false;
			}
			return (value & (ulong)(1L << cy % 8 * 8 + cx % 8)) != 0;
		}

		public static bool SeenAt(Map m, float x, float y)
		{
			return Seen(m, Mathf.FloorToInt(x / 4f), Mathf.FloorToInt(y / 4f));
		}

		private static bool Mark(Map m, int cx, int cy)
		{
			if (cx < 0 || cy < 0 || cx >= CellsWide || cy >= CellsHigh)
			{
				return false;
			}
			int key = BlockKey(cx / 8, cy / 8);
			m.Blocks.TryGetValue(key, out var value);
			ulong num = (ulong)(1L << cy % 8 * 8 + cx % 8);
			if ((value & num) != 0L)
			{
				return false;
			}
			m.Blocks[key] = value | num;
			m.Cells++;
			return true;
		}

		private static void Changed(Map m, int added)
		{
			if (added > 0)
			{
				m.Version++;
				if (!m.Dirty)
				{
					m.Dirty = true;
					m.DirtySince = Time.unscaledTime;
				}
			}
		}

		public static int Reveal(Map m, Vector2 at, float radius)
		{
			if (m == null || radius <= 0f)
			{
				return 0;
			}
			float num = radius + 2f;
			float num2 = num * num;
			int num3 = Mathf.FloorToInt((at.x - num) / 4f);
			int num4 = Mathf.FloorToInt((at.x + num) / 4f);
			int num5 = Mathf.FloorToInt((at.y - num) / 4f);
			int num6 = Mathf.FloorToInt((at.y + num) / 4f);
			int num7 = 0;
			for (int i = num5; i <= num6; i++)
			{
				float num8 = ((float)i + 0.5f) * 4f - at.y;
				for (int j = num3; j <= num4; j++)
				{
					float num9 = ((float)j + 0.5f) * 4f - at.x;
					if (num9 * num9 + num8 * num8 <= num2 && Mark(m, j, i))
					{
						num7++;
					}
				}
			}
			Changed(m, num7);
			return num7;
		}

		public static int RevealView(Map m, Vector2 eye, float yaw, float halfAngle, float far)
		{
			if (m == null || far <= 0f)
			{
				return 0;
			}
			halfAngle = Mathf.Clamp(halfAngle, 0.01f, 1.4f);
			Vector2 vector = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
			float num = Mathf.Tan(halfAngle);
			int num2 = 0;
			for (float num3 = 2f; num3 <= far; num3 += 2f)
			{
				float num4 = num3 * num;
				for (float num5 = 0f - num4; num5 <= num4 + 0.01f; num5 += 2f)
				{
					Vector2 vector3 = eye + vector * num3 + vector2 * num5;
					if (Mark(m, Mathf.FloorToInt(vector3.x / 4f), Mathf.FloorToInt(vector3.y / 4f)))
					{
						num2++;
					}
				}
			}
			Changed(m, num2);
			return num2;
		}

		public static bool Bounds(Map m, out Rect r)
		{
			r = default(Rect);
			if (m == null || m.Blocks.Count == 0)
			{
				return false;
			}
			if (m.BoundsVersion == m.Version)
			{
				r = m.SeenBounds;
				return true;
			}
			int num = int.MaxValue;
			int num2 = int.MaxValue;
			int num3 = int.MinValue;
			int num4 = int.MinValue;
			foreach (KeyValuePair<int, ulong> block in m.Blocks)
			{
				int num5 = block.Key & 0xFFFF;
				int num6 = (block.Key >> 16) & 0xFFFF;
				ulong value = block.Value;
				for (int i = 0; i < 64; i++)
				{
					if ((value & (ulong)(1L << i)) != 0L)
					{
						int num7 = num5 * 8 + i % 8;
						int num8 = num6 * 8 + i / 8;
						if (num7 < num)
						{
							num = num7;
						}
						if (num7 > num3)
						{
							num3 = num7;
						}
						if (num8 < num2)
						{
							num2 = num8;
						}
						if (num8 > num4)
						{
							num4 = num8;
						}
					}
				}
			}
			if (num > num3)
			{
				return false;
			}
			m.SeenBounds = (r = Rect.MinMaxRect(num * 4, num2 * 4, (num3 + 1) * 4, (num4 + 1) * 4));
			m.BoundsVersion = m.Version;
			return true;
		}

		public static float Share(Map m)
		{
			if (m != null)
			{
				return Mathf.Clamp01((float)m.Cells / (float)WorldCells);
			}
			return 0f;
		}

		public static void Write(Actor a, Map m)
		{
			if (a == null || m == null)
			{
				return;
			}
			try
			{
				a.data.set("fp_seen", Text(m));
				m.Dirty = false;
				Writes++;
			}
			catch (Exception ex)
			{
				Log.Error("explored: could not write a map: " + ex.Message);
				m.DirtySince = Time.unscaledTime;
			}
		}

		public static int WriteAll()
		{
			if (World.world == null || World.world.units == null)
			{
				return 0;
			}
			int num = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				try
				{
					if (simple != null && simple.isAlive() && Maps.TryGetValue(simple.data.id, out var value) && value.Dirty)
					{
						Write(simple, value);
						num++;
					}
				}
				catch
				{
				}
			}
			return num;
		}

		public static string Text(Map m)
		{
			byte[] array = new byte[1 + m.Blocks.Count * 12];
			array[0] = 1;
			int num = 1;
			foreach (KeyValuePair<int, ulong> block in m.Blocks)
			{
				int num2 = block.Key & 0xFFFF;
				int num3 = (block.Key >> 16) & 0xFFFF;
				array[num++] = (byte)num2;
				array[num++] = (byte)(num2 >> 8);
				array[num++] = (byte)num3;
				array[num++] = (byte)(num3 >> 8);
				ulong value = block.Value;
				for (int i = 0; i < 8; i++)
				{
					array[num++] = (byte)(value >> i * 8);
				}
			}
			return Convert.ToBase64String(array);
		}

		private static bool Parse(string text, Map m)
		{
			try
			{
				byte[] array = Convert.FromBase64String(text);
				if (array.Length < 1 || array[0] != 1 || (array.Length - 1) % 12 != 0)
				{
					return false;
				}
				m.Blocks.Clear();
				m.Cells = 0;
				for (int i = 1; i + 12 <= array.Length; i += 12)
				{
					int bx = array[i] | (array[i + 1] << 8);
					int num = array[i + 2] | (array[i + 3] << 8);
					ulong num2 = 0uL;
					for (int j = 0; j < 8; j++)
					{
						num2 |= (ulong)array[i + 4 + j] << j * 8;
					}
					if (num2 != 0L)
					{
						m.Blocks[BlockKey(bx, num)] = num2;
						m.Cells += PopCount(num2);
					}
				}
				m.Version++;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static int PopCount(ulong v)
		{
			v -= (v >> 1) & 0x5555555555555555L;
			v = (v & 0x3333333333333333L) + ((v >> 2) & 0x3333333333333333L);
			v = (v + (v >> 4)) & 0xF0F0F0F0F0F0F0FL;
			return (int)(v * 72340172838076673L >> 56);
		}
	}
}
