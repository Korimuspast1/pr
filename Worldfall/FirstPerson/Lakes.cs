using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class Lakes
	{
		internal sealed class Lake
		{
			public Basin Basin;

			public float Volume;

			public float Level;

			public bool Tarn;

			public List<int> Stream;

			public float SpillAt;

			public int Key
			{
				get
				{
					if (Basin.Tiles.Count <= 0)
					{
						return -1;
					}
					return Basin.Tiles[0];
				}
			}

			public float Capacity => Basin.VolumeAt(Basin.Rim);
		}

		public static float RainPerDay = 0.9f;

		public static float SpringPerDay = 1.6f;

		public static float DryPerDay = 0.08f;

		public const int Most = 900;

		private const float Wet = 0.03f;

		private static readonly List<Lake> All = new List<Lake>();

		private static int _built = -1;

		private static int _editSeen = int.MinValue;

		private static float _editAt = -1f;

		private static float _lastHour = -1f;

		private static float _days;

		private static float _applyAt;

		private static readonly HashSet<int> _tarns = new HashSet<int>();

		internal static int Flooded;

		internal static int Drained;

		internal static int Found;

		private static TileType _shallow;

		private static TileType _close;

		private static TerraformOptions _quiet;

		internal static IReadOnlyList<Lake> Now => All;

		public static void Tick(DayCycle day, Weather weather)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || sampler.Cache.Width == 0 || WorldBoxMod.WorldLoading)
			{
				return;
			}
			Relief relief = sampler.Relief;
			if (ReliefWorld.Builds != _built)
			{
				_built = ReliefWorld.Builds;
				All.Clear();
				_tarns.Clear();
				lock (ReliefGen.LastTarns)
				{
					if (relief.Terrainy)
					{
						foreach (int item in ReliefGen.Chosen)
						{
							_tarns.Add(item);
						}
					}
				}
				_editSeen = int.MinValue;
				_editAt = -1f;
				_lastHour = -1f;
			}
			if (NetSession.Joined)
			{
				return;
			}
			WaterFields.Tick(sampler.Cache);
			float unscaledTime = Time.unscaledTime;
			if (relief.EditVersion != _editSeen)
			{
				if (_editAt < 0f)
				{
					_editAt = unscaledTime + ((_editSeen == int.MinValue) ? 0f : 1f);
				}
				else if (unscaledTime >= _editAt)
				{
					_editSeen = relief.EditVersion;
					_editAt = -1f;
					FindBasins(sampler);
				}
			}
			if (day != null)
			{
				float timeOfDay = day.TimeOfDay;
				if (_lastHour >= 0f)
				{
					float num = timeOfDay - _lastHour;
					if (num < 0f)
					{
						num += 1f;
					}
					if (num < 0.25f)
					{
						_days += num;
					}
				}
				_lastHour = timeOfDay;
			}
			if (_days > 0.004f && unscaledTime >= _applyAt)
			{
				_applyAt = unscaledTime + 0.5f;
				Flow(sampler, _days, weather);
				_days = 0f;
			}
		}

		private static bool Away(TerrainCache c, int i)
		{
			if ((c.Flags[i] & 1) == 0 || (c.Marks[i] & 4) != 0)
			{
				return (c.Flags[i] & 2) != 0;
			}
			return true;
		}

		private static void FindBasins(WorldSampler s)
		{
			TerrainCache c = s.Cache;
			Relief relief = s.Relief;
			int width = c.Width;
			int height = c.Height;
			int num = width * height;
			float[] bed = (float[])c.Floor.Clone();
			if (bed.Length != num)
			{
				return;
			}
			bool[] held = new bool[num];
			short[] edit = relief.Edit;
			for (int i = 0; i < num; i++)
			{
				if ((edit == null || edit[i] == 0) && !relief.IsLake(i))
				{
					continue;
				}
				int num2 = i % width;
				int num3 = i / width;
				if (!relief.IsLake(i) && Occupied(num2, num3))
				{
					continue;
				}
				for (int j = -1; j <= 1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						int num4 = num2 + k;
						int num5 = num3 + j;
						if (num4 >= 0 && num5 >= 0 && num4 < width && num5 < height && !Occupied(num4, num5))
						{
							held[num5 * width + num4] = true;
						}
					}
				}
			}
			int num6 = (int)Math.Ceiling(ReliefGen.TarnRadius + 1.5f);
			foreach (int tarn in _tarns)
			{
				int num7 = tarn % width;
				int num8 = tarn / width;
				for (int l = Math.Max(0, num8 - num6); l <= Math.Min(height - 1, num8 + num6); l++)
				{
					for (int m = Math.Max(0, num7 - num6); m <= Math.Min(width - 1, num7 + num6); m++)
					{
						if ((m - num7) * (m - num7) + (l - num8) * (l - num8) <= num6 * num6)
						{
							held[l * width + m] = true;
						}
					}
				}
			}
			List<int> list = new List<int>();
			for (int n = 0; n < num; n++)
			{
				bool num9 = relief.IsLake(n);
				bool flag = edit != null && edit[n] != 0;
				if ((num9 || flag || _tarns.Contains(n)) && !Out(n))
				{
					int num10 = Downhill(bed, width, height, n, Out);
					if (num10 >= 0 && !list.Contains(num10))
					{
						list.Add(num10);
					}
				}
			}
			list.Sort((int a, int b) => bed[a].CompareTo(bed[b]));
			HashSet<int> hashSet = new HashSet<int>();
			List<Lake> list2 = new List<Lake>();
			foreach (int item in list)
			{
				if (hashSet.Contains(item))
				{
					continue;
				}
				Basin basin = Basins.Pour(bed, Out, width, height, item, 900);
				if (basin == null)
				{
					continue;
				}
				bool flag2 = false;
				foreach (int tile in basin.Tiles)
				{
					if (hashSet.Contains(tile))
					{
						flag2 = true;
						break;
					}
				}
				if (flag2)
				{
					continue;
				}
				foreach (int tile2 in basin.Tiles)
				{
					hashSet.Add(tile2);
				}
				Lake lake = new Lake
				{
					Basin = basin
				};
				foreach (int tile3 in basin.Tiles)
				{
					if (_tarns.Contains(tile3))
					{
						lake.Tarn = true;
					}
					if (relief.Lake(tile3, out var level, out var _, out var _))
					{
						lake.Volume += Math.Max(0f, level - bed[tile3]);
					}
				}
				if (lake.Tarn && lake.Volume <= 0f)
				{
					lake.Volume = lake.Capacity;
				}
				lake.Volume = Math.Min(lake.Volume, lake.Capacity);
				lake.Level = basin.LevelFor(lake.Volume);
				list2.Add(lake);
			}
			Found = list2.Count;
			foreach (Lake item2 in list2)
			{
				foreach (Lake item3 in All)
				{
					if (item3.Key == item2.Key && item3.Stream != null)
					{
						item2.Stream = item3.Stream;
						item2.SpillAt = item3.SpillAt;
						break;
					}
				}
			}
			for (int num11 = 0; num11 < num; num11++)
			{
				if (relief.IsLake(num11) && !hashSet.Contains(num11))
				{
					Drain(relief, num11);
				}
			}
			All.Clear();
			All.AddRange(list2);
			foreach (Lake item4 in All)
			{
				Apply(relief, c, item4);
			}
			if (list2.Count > 0)
			{
				Log.Info($"lakes: {list2.Count} basins ({Count(list2, tarns: true)} tarns), {relief.LakeTileCount()} tiles under water");
			}
			bool Out(int num12)
			{
				if (held[num12])
				{
					return Away(c, num12);
				}
				return true;
			}
		}

		private static bool Occupied(int x, int y)
		{
			try
			{
				Building building = World.world.GetTile(x, y)?.building;
				if (building == null || !AssetStyle.IsSolid(building))
				{
					return false;
				}
				BuildingType building_type = building.asset.building_type;
				return building_type != BuildingType.Building_Tree && building_type != BuildingType.Building_Mineral;
			}
			catch
			{
				return false;
			}
		}

		private static int Count(List<Lake> lakes, bool tarns)
		{
			int num = 0;
			foreach (Lake lake in lakes)
			{
				if (lake.Tarn == tarns)
				{
					num++;
				}
			}
			return num;
		}

		private static int Downhill(float[] bed, int w, int h, int i, Func<int, bool> away)
		{
			for (int j = 0; j < 64; j++)
			{
				int num = i % w;
				int num2 = i / w;
				int num3 = i;
				float num4 = bed[i];
				if (num > 0 && bed[i - 1] < num4)
				{
					num3 = i - 1;
					num4 = bed[i - 1];
				}
				if (num < w - 1 && bed[i + 1] < num4)
				{
					num3 = i + 1;
					num4 = bed[i + 1];
				}
				if (num2 > 0 && bed[i - w] < num4)
				{
					num3 = i - w;
					num4 = bed[i - w];
				}
				if (num2 < h - 1 && bed[i + w] < num4)
				{
					num3 = i + w;
					num4 = bed[i + w];
				}
				if (num3 == i)
				{
					return i;
				}
				if (away(num3))
				{
					return -1;
				}
				i = num3;
			}
			return i;
		}

		private static void Flow(WorldSampler s, float days, Weather weather, float summer = -1f)
		{
			Relief relief = s.Relief;
			if (summer < 0f)
			{
				summer = SeasonLook.Summer;
			}
			foreach (Lake item in All)
			{
				int num = item.Basin.Tiles[0];
				float x = (float)(num % relief.Width) + 0.5f;
				float y = (float)(num / relief.Width) + 0.5f;
				float num2 = RainAt(weather, x, y);
				float num3 = RainPerDay * num2 + (item.Tarn ? SpringPerDay : 0f) - DryPerDay * summer * ((num2 > 0.05f) ? 0f : 1f);
				float capacity = item.Capacity;
				item.Volume = Mathf.Clamp(item.Volume + num3 * (float)item.Basin.Count * days, 0f, capacity);
				bool flag = num3 > 0f && item.Volume >= capacity * 0.995f;
				if (flag)
				{
					item.SpillAt = Time.unscaledTime;
				}
				bool flag2 = item.Stream != null;
				if (flag && item.Stream == null)
				{
					item.Stream = Basins.Run(s.Cache.Floor, (int i) => Away(s.Cache, i), relief.Width, relief.Height, (item.Basin.Out >= 0) ? item.Basin.Out : item.Basin.Spill, 240);
				}
				else if (!flag && item.Stream != null && Time.unscaledTime - item.SpillAt > 20f)
				{
					item.Stream = null;
				}
				if (flag2 != (item.Stream != null))
				{
					Streams.Changed = true;
				}
				float num4 = item.Basin.LevelFor(item.Volume);
				if (!(Mathf.Abs(num4 - item.Level) < 3f / 128f))
				{
					item.Level = num4;
					Apply(relief, s.Cache, item);
				}
			}
		}

		private static float RainAt(Weather weather, float x, float y)
		{
			if (weather == null)
			{
				return 0f;
			}
			float num = ((weather.Global == Precip.Rain) ? weather.GlobalStrength : 0f);
			foreach (StormCloud storm in weather.Storms)
			{
				if (storm.Drops == Precip.Rain && Mathf.Abs(x - storm.X) < storm.HalfX && Mathf.Abs(y - storm.Y) < storm.HalfY)
				{
					num = Mathf.Max(num, storm.Opacity);
				}
			}
			return Mathf.Clamp01(num);
		}

		private static void Apply(Relief r, TerrainCache c, Lake l)
		{
			Basin basin = l.Basin;
			for (int i = 0; i < basin.Tiles.Count; i++)
			{
				int i2 = basin.Tiles[i];
				float num = basin.Beds[i];
				bool flag = num < l.Level - 0.03f;
				float level;
				int main;
				int top;
				bool flag2 = r.Lake(i2, out level, out main, out top);
				if (flag && !flag2)
				{
					Flood(r, i2, l.Level, l.Level - num);
				}
				else if (flag)
				{
					r.SetLake(i2, l.Level, main, top);
				}
				else if (flag2)
				{
					Drain(r, i2);
				}
			}
		}

		private static void Flood(Relief r, int i, float level, float depth)
		{
			WorldTile tile;
			try
			{
				tile = World.world.GetTile(i % r.Width, i / r.Width);
			}
			catch
			{
				return;
			}
			if (tile?.main_type == null || tile.main_type.liquid || tile.main_type.lava)
			{
				return;
			}
			try
			{
				Building building = tile.building;
				if (building != null && building.isAlive() && building.asset != null && AssetStyle.IsSolid(building))
				{
					return;
				}
			}
			catch
			{
			}
			Ensure();
			TileType tileType = ((depth < 0.6f || _close == null) ? _shallow : _close);
			if (tileType != null)
			{
				int main = tile.main_type.index_id + 1;
				int top = ((tile.top_type != null) ? (tile.top_type.index_id + 1) : 0);
				try
				{
					MapAction.terraformTile(tile, tileType, null, _quiet);
				}
				catch (Exception ex)
				{
					Log.Error("lakes: flooding a tile: " + ex.Message);
					return;
				}
				if (tile.main_type != null && tile.main_type.liquid)
				{
					r.SetLake(i, level, main, top);
					Flooded++;
				}
			}
		}

		private static void Drain(Relief r, int i)
		{
			r.Lake(i, out var _, out var main, out var top);
			r.SetLake(i, 0f, 0, 0);
			WorldTile tile;
			try
			{
				tile = World.world.GetTile(i % r.Width, i / r.Width);
			}
			catch
			{
				return;
			}
			if (tile?.main_type == null || !tile.main_type.liquid)
			{
				return;
			}
			TileType tileType = WorldSampler.TypeOfId(main) as TileType;
			TopTileType pTopType = WorldSampler.TypeOfId(top) as TopTileType;
			if (tileType != null)
			{
				Ensure();
				try
				{
					MapAction.terraformTile(tile, tileType, pTopType, _quiet);
				}
				catch (Exception ex)
				{
					Log.Error("lakes: draining a tile: " + ex.Message);
					return;
				}
				Drained++;
			}
		}

		private static void Ensure()
		{
			if (_shallow != null)
			{
				return;
			}
			try
			{
				_shallow = AssetManager.tiles.get("shallow_waters");
				_close = AssetManager.tiles.get("close_ocean");
				_quiet = AssetManager.terraform.get("nothing") ?? TerraformLibrary.flash;
			}
			catch (Exception ex)
			{
				Log.Error("lakes: the game's water: " + ex.Message);
			}
		}

		internal static void FindNowForTest()
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler != null && sampler.Ready)
			{
				FindBasins(sampler);
			}
		}

		internal static void FlowForTest(float days, float rain, float summer = 0f)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler != null && sampler.Ready)
			{
				Weather weather = new Weather
				{
					Global = ((rain > 0f) ? Precip.Rain : Precip.None),
					GlobalStrength = rain
				};
				Flow(sampler, days, weather, summer);
			}
		}
	}
}
