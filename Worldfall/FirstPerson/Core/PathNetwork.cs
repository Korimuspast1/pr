using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class PathNetwork
	{
		public delegate float TileCost(int tileIndex);

		private sealed class Region
		{
			private readonly TerrainCache _t;

			public readonly int X0;

			public readonly int Y0;

			public readonly int X1;

			public readonly int Y1;

			public readonly int W;

			public readonly int H;

			private float[] _dist;

			private int[] _from;

			private static readonly int[] DX = new int[8] { 1, -1, 0, 0, 1, 1, -1, -1 };

			private static readonly int[] DY = new int[8] { 0, 0, 1, -1, 1, -1, 1, -1 };

			private static readonly float[] Len = new float[8] { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

			public Region(TerrainCache t, int x0, int y0, int x1, int y1)
			{
				_t = t;
				X0 = Clamp(x0, 0, t.Width - 1);
				Y0 = Clamp(y0, 0, t.Height - 1);
				X1 = Clamp(x1, 0, t.Width - 1);
				Y1 = Clamp(y1, 0, t.Height - 1);
				W = X1 - X0 + 1;
				H = Y1 - Y0 + 1;
			}

			public bool Contains(int x, int y)
			{
				if (x >= X0 && x <= X1 && y >= Y0)
				{
					return y <= Y1;
				}
				return false;
			}

			public List<int> Route(int start, HashSet<int> targets, TileCost cost)
			{
				int num = W * H;
				int width = _t.Width;
				if (_dist == null || _dist.Length < num)
				{
					_dist = new float[num];
					_from = new int[num];
				}
				for (int i = 0; i < num; i++)
				{
					_dist[i] = float.MaxValue;
					_from[i] = -1;
				}
				int num2 = start % width - X0;
				int num3 = start / width - Y0;
				if (num2 < 0 || num3 < 0 || num2 >= W || num3 >= H)
				{
					return null;
				}
				MinHeap minHeap = new MinHeap(256);
				int num4 = num3 * W + num2;
				_dist[num4] = 0f;
				minHeap.Push(num4, 0f);
				int num5 = -1;
				while (minHeap.Count > 0)
				{
					minHeap.Pop(out var item, out var key);
					if (key > _dist[item])
					{
						continue;
					}
					int num6 = item % W;
					int num7 = item / W;
					int num8 = (num7 + Y0) * width + num6 + X0;
					if (item != num4 && targets.Contains(num8))
					{
						num5 = item;
						break;
					}
					float num9 = _t.Ground[num8];
					for (int j = 0; j < 8; j++)
					{
						int num10 = num6 + DX[j];
						int num11 = num7 + DY[j];
						if (num10 < 0 || num11 < 0 || num10 >= W || num11 >= H)
						{
							continue;
						}
						int num12 = (num11 + Y0) * width + num10 + X0;
						float num13 = cost(num12);
						if (!(num13 < 0f) && (j < 4 || (!(cost((num7 + Y0) * width + num10 + X0) < 0f) && !(cost((num11 + Y0) * width + num6 + X0) < 0f))))
						{
							if (targets.Contains(num12))
							{
								num13 *= 0.45f;
							}
							float num14 = Math.Abs(_t.Ground[num12] - num9);
							float num15 = key + Len[j] * (num13 + num14 * 2.5f);
							int num16 = num11 * W + num10;
							if (num15 < _dist[num16])
							{
								_dist[num16] = num15;
								_from[num16] = item;
								minHeap.Push(num16, num15);
							}
						}
					}
				}
				if (num5 < 0)
				{
					return null;
				}
				List<int> list = new List<int>();
				for (int num17 = num5; num17 >= 0; num17 = _from[num17])
				{
					list.Add((num17 / W + Y0) * width + num17 % W + X0);
				}
				list.Reverse();
				return list;
			}
		}

		private sealed class MinHeap
		{
			private int[] _items;

			private float[] _keys;

			public int Count;

			public MinHeap(int capacity)
			{
				_items = new int[capacity];
				_keys = new float[capacity];
			}

			public void Push(int item, float key)
			{
				if (Count == _items.Length)
				{
					Array.Resize(ref _items, Count * 2);
					Array.Resize(ref _keys, Count * 2);
				}
				int num = Count++;
				while (num > 0)
				{
					int num2 = num - 1 >> 1;
					if (_keys[num2] <= key)
					{
						break;
					}
					_items[num] = _items[num2];
					_keys[num] = _keys[num2];
					num = num2;
				}
				_items[num] = item;
				_keys[num] = key;
			}

			public void Pop(out int item, out float key)
			{
				item = _items[0];
				key = _keys[0];
				int num = --Count;
				if (num == 0)
				{
					return;
				}
				int num2 = _items[num];
				float num3 = _keys[num];
				int num4 = 0;
				while (true)
				{
					int num5 = num4 * 2 + 1;
					if (num5 >= num)
					{
						break;
					}
					if (num5 + 1 < num && _keys[num5 + 1] < _keys[num5])
					{
						num5++;
					}
					if (_keys[num5] >= num3)
					{
						break;
					}
					_items[num4] = _items[num5];
					_keys[num4] = _keys[num5];
					num4 = num5;
				}
				_items[num4] = num2;
				_keys[num4] = num3;
			}
		}

		private const float Blocked = -1f;

		private const float JoinDiscount = 0.45f;

		private const int Margin = 7;

		public static void TownRegion(TerrainCache t, TownPlan town, out int x0, out int y0, out int x1, out int y1)
		{
			int num = Clamp((int)town.HubX, 0, t.Width - 1);
			int num2 = Clamp((int)town.HubY, 0, t.Height - 1);
			x0 = num;
			y0 = num2;
			x1 = num;
			y1 = num2;
			foreach (PathPoint door in town.Doors)
			{
				x0 = Math.Min(x0, (int)door.X);
				y0 = Math.Min(y0, (int)door.Y);
				x1 = Math.Max(x1, (int)door.X);
				y1 = Math.Max(y1, (int)door.Y);
			}
			x0 -= 7;
			y0 -= 7;
			x1 += 7;
			y1 += 7;
		}

		public static void TrackRegion(PathPoint a, PathPoint b, out int x0, out int y0, out int x1, out int y1)
		{
			x0 = Math.Min((int)a.X, (int)b.X) - 10;
			y0 = Math.Min((int)a.Y, (int)b.Y) - 10;
			x1 = Math.Max((int)a.X, (int)b.X) + 10;
			y1 = Math.Max((int)a.Y, (int)b.Y) + 10;
		}

		public static List<PathStroke> PlanTown(TerrainCache t, TownPlan town, TileCost cost)
		{
			List<PathStroke> list = new List<PathStroke>();
			if (t == null || t.Width == 0)
			{
				return list;
			}
			int num = Clamp((int)town.HubX, 0, t.Width - 1);
			int num2 = Clamp((int)town.HubY, 0, t.Height - 1);
			TownRegion(t, town, out var x, out var y, out var x2, out var y2);
			Region region = new Region(t, x, y, x2, y2);
			HashSet<int> hashSet = new HashSet<int>();
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			int num3 = (int)Math.Ceiling(town.PlazaRadius);
			for (int i = num2 - num3; i <= num2 + num3; i++)
			{
				for (int j = num - num3; j <= num + num3; j++)
				{
					float num4 = (float)j + 0.5f - town.HubX;
					float num5 = (float)i + 0.5f - town.HubY;
					if (num4 * num4 + num5 * num5 <= town.PlazaRadius * town.PlazaRadius && region.Contains(j, i))
					{
						hashSet.Add(i * t.Width + j);
					}
				}
			}
			hashSet.Add(num2 * t.Width + num);
			list.Add(Plaza(town.HubX, town.HubY, town.PlazaRadius));
			List<PathPoint> list2 = new List<PathPoint>(town.Doors);
			list2.Sort((PathPoint a, PathPoint b) => Dist2(a, town.HubX, town.HubY).CompareTo(Dist2(b, town.HubX, town.HubY)));
			List<List<int>> list3 = new List<List<int>>();
			foreach (PathPoint item in list2)
			{
				int num6 = (int)item.X;
				int num7 = (int)item.Y;
				if (!region.Contains(num6, num7))
				{
					continue;
				}
				List<int> list4 = region.Route(num7 * t.Width + num6, hashSet, cost);
				if (list4 == null || list4.Count < 2)
				{
					continue;
				}
				foreach (int item2 in list4)
				{
					hashSet.Add(item2);
					dictionary.TryGetValue(item2, out var value);
					dictionary[item2] = value + 1;
				}
				list3.Add(list4);
			}
			for (int num8 = 0; num8 < list3.Count; num8++)
			{
				List<int> list5 = list3[num8];
				List<PathPoint> list6 = new List<PathPoint>(list5.Count + 1) { (list2.Count > 0) ? NearestDoor(list2, list5[0], t.Width) : Centre(list5[0], t.Width) };
				for (int num9 = 1; num9 < list5.Count; num9++)
				{
					list6.Add(Centre(list5[num9], t.Width));
				}
				PathPoint[] array = Smooth(list6, (uint)(list5[0] * 2654435761u));
				float[] array2 = new float[array.Length];
				for (int num10 = 0; num10 < array.Length; num10++)
				{
					int key = Clamp((int)array[num10].Y, 0, t.Height - 1) * t.Width + Clamp((int)array[num10].X, 0, t.Width - 1);
					dictionary.TryGetValue(key, out var value2);
					array2[num10] = 0.19f + 0.07f * (float)Math.Min(5, Math.Max(1, value2) - 1);
				}
				list.Add(new PathStroke
				{
					Points = array,
					HalfWidth = array2,
					Strength = byte.MaxValue
				});
			}
			return list;
		}

		public static PathStroke PlanTrack(TerrainCache t, PathPoint from, PathPoint to, TileCost cost)
		{
			int num = (int)from.X;
			int num2 = (int)from.Y;
			int num3 = (int)to.X;
			int num4 = (int)to.Y;
			TrackRegion(from, to, out var x, out var y, out var x2, out var y2);
			Region region = new Region(t, x, y, x2, y2);
			if (!region.Contains(num, num2) || !region.Contains(num3, num4))
			{
				return null;
			}
			HashSet<int> targets = new HashSet<int> { num4 * t.Width + num3 };
			List<int> list = region.Route(num2 * t.Width + num, targets, cost);
			if (list == null || list.Count < 2)
			{
				return null;
			}
			List<PathPoint> list2 = new List<PathPoint>(list.Count);
			foreach (int item in list)
			{
				list2.Add(Centre(item, t.Width));
			}
			PathPoint[] array = Smooth(list2, (uint)(num * 7919 + num4 * 104729));
			float[] array2 = new float[array.Length];
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i] = 0.15f;
			}
			return new PathStroke
			{
				Points = array,
				HalfWidth = array2,
				Strength = 200
			};
		}

		public static void Apply(TerrainCache t, List<PathStroke> strokes)
		{
			ApplyMasks(t, new List<Dictionary<int, byte[]>> { Paint(t, strokes) });
		}

		public static Dictionary<int, byte[]> Paint(TerrainCache t, List<PathStroke> strokes)
		{
			Dictionary<int, byte[]> dictionary = new Dictionary<int, byte[]>();
			foreach (PathStroke stroke in strokes)
			{
				Rasterize(t, stroke, dictionary);
			}
			return dictionary;
		}

		public static void ApplyMasks(TerrainCache t, List<Dictionary<int, byte[]>> parts)
		{
			foreach (int pathTile in t.PathTileList)
			{
				if (pathTile < t.PathSlot.Length)
				{
					t.PathSlot[pathTile] = 0;
				}
			}
			t.PathTileList.Clear();
			Dictionary<int, byte[]> dictionary = new Dictionary<int, byte[]>();
			foreach (Dictionary<int, byte[]> part in parts)
			{
				if (part == null)
				{
					continue;
				}
				foreach (KeyValuePair<int, byte[]> item in part)
				{
					if (item.Key < 0 || item.Key >= t.PathSlot.Length)
					{
						continue;
					}
					if (!dictionary.TryGetValue(item.Key, out var value))
					{
						dictionary[item.Key] = (byte[])item.Value.Clone();
						continue;
					}
					for (int i = 0; i < 64; i++)
					{
						if (item.Value[i] > value[i])
						{
							value[i] = item.Value[i];
						}
					}
				}
			}
			if (t.PathTexels.Length < dictionary.Count * 64)
			{
				t.PathTexels = new byte[Math.Max(64, dictionary.Count * 80) & -64];
			}
			int num = 0;
			foreach (KeyValuePair<int, byte[]> item2 in dictionary)
			{
				Buffer.BlockCopy(item2.Value, 0, t.PathTexels, num * 64, 64);
				t.PathSlot[item2.Key] = num + 1;
				t.PathTileList.Add(item2.Key);
				num++;
			}
		}

		private static PathStroke Plaza(float x, float y, float radius)
		{
			PathPoint[] array = new PathPoint[11];
			float[] array2 = new float[11];
			for (int i = 0; i <= 10; i++)
			{
				float num = (float)i * (MathF.PI / 5f);
				array[i] = new PathPoint(x + (float)Math.Cos(num) * radius * 0.45f, y + (float)Math.Sin(num) * radius * 0.45f);
				array2[i] = radius * 0.62f;
			}
			return new PathStroke
			{
				Points = array,
				HalfWidth = array2,
				Strength = byte.MaxValue
			};
		}

		private static PathPoint[] Smooth(List<PathPoint> pts, uint seed)
		{
			for (int i = 1; i < pts.Count - 1; i++)
			{
				uint num = TerrainModel.Hash((int)(pts[i].X * 3f) + (int)seed, (int)(pts[i].Y * 5f));
				pts[i] = new PathPoint(pts[i].X + ((float)(num & 0xFF) / 255f - 0.5f) * 0.32f, pts[i].Y + ((float)((num >> 8) & 0xFF) / 255f - 0.5f) * 0.32f);
			}
			List<PathPoint> list = pts;
			for (int j = 0; j < 2; j++)
			{
				if (list.Count < 3)
				{
					break;
				}
				List<PathPoint> list2 = new List<PathPoint>(list.Count * 2) { list[0] };
				for (int k = 0; k < list.Count - 1; k++)
				{
					PathPoint pathPoint = list[k];
					PathPoint pathPoint2 = list[k + 1];
					list2.Add(new PathPoint(pathPoint.X * 0.75f + pathPoint2.X * 0.25f, pathPoint.Y * 0.75f + pathPoint2.Y * 0.25f));
					list2.Add(new PathPoint(pathPoint.X * 0.25f + pathPoint2.X * 0.75f, pathPoint.Y * 0.25f + pathPoint2.Y * 0.75f));
				}
				list2.Add(list[list.Count - 1]);
				list = list2;
			}
			return list.ToArray();
		}

		private static void Rasterize(TerrainCache t, PathStroke s, Dictionary<int, byte[]> masks)
		{
			PathPoint[] points = s.Points;
			if (points == null || points.Length == 0)
			{
				return;
			}
			for (int i = 0; i < points.Length; i++)
			{
				PathPoint pathPoint = points[i];
				PathPoint pathPoint2 = ((i + 1 < points.Length) ? points[i + 1] : points[i]);
				float num = s.HalfWidth[i];
				float num2 = s.HalfWidth[Math.Min(i + 1, points.Length - 1)];
				float num3 = Math.Max(num, num2) + 0.1f;
				int num4 = Math.Max(0, (int)(Math.Min(pathPoint.X, pathPoint2.X) - num3));
				int num5 = Math.Min(t.Width - 1, (int)(Math.Max(pathPoint.X, pathPoint2.X) + num3));
				int num6 = Math.Max(0, (int)(Math.Min(pathPoint.Y, pathPoint2.Y) - num3));
				int num7 = Math.Min(t.Height - 1, (int)(Math.Max(pathPoint.Y, pathPoint2.Y) + num3));
				float num8 = pathPoint2.X - pathPoint.X;
				float num9 = pathPoint2.Y - pathPoint.Y;
				float num10 = num8 * num8 + num9 * num9;
				for (int j = num6; j <= num7; j++)
				{
					for (int k = num4; k <= num5; k++)
					{
						int num11 = j * t.Width + k;
						if ((t.Flags[num11] & 0xB) != 0)
						{
							continue;
						}
						byte[] value = null;
						for (int l = 0; l < 8; l++)
						{
							for (int m = 0; m < 8; m++)
							{
								float num12 = (float)k + ((float)m + 0.5f) / 8f;
								float num13 = (float)j + ((float)l + 0.5f) / 8f;
								float num14 = ((num10 > 1E-08f) ? Math.Max(0f, Math.Min(1f, ((num12 - pathPoint.X) * num8 + (num13 - pathPoint.Y) * num9) / num10)) : 0f);
								float num15 = pathPoint.X + num8 * num14 - num12;
								float num16 = pathPoint.Y + num9 * num14 - num13;
								float num17 = (float)Math.Sqrt(num15 * num15 + num16 * num16);
								uint num18 = TerrainModel.Hash(k * 8 + m, j * 8 + l);
								float num19 = num + (num2 - num) * num14 + ((float)(num18 & 0xFF) / 255f - 0.5f) * 0.09f;
								if (num17 >= num19 + 0.06f)
								{
									continue;
								}
								int num20 = (int)(Math.Min(1f, (num19 + 0.06f - num17) / 0.12f) * (float)(int)s.Strength);
								if (num20 > 0)
								{
									if (value == null && !masks.TryGetValue(num11, out value))
									{
										value = (masks[num11] = new byte[64]);
									}
									int num21 = l * 8 + m;
									if (num20 > value[num21])
									{
										value[num21] = (byte)num20;
									}
								}
							}
						}
					}
				}
			}
		}

		private static PathPoint Centre(int tile, int width)
		{
			return new PathPoint((float)(tile % width) + 0.5f, (float)(tile / width) + 0.5f);
		}

		private static PathPoint NearestDoor(List<PathPoint> doors, int tile, int width)
		{
			PathPoint result = Centre(tile, width);
			foreach (PathPoint door in doors)
			{
				if ((int)door.X == tile % width && (int)door.Y == tile / width)
				{
					return door;
				}
			}
			return result;
		}

		private static float Dist2(PathPoint p, float x, float y)
		{
			return (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y);
		}

		private static int Clamp(int v, int lo, int hi)
		{
			if (v >= lo)
			{
				if (v <= hi)
				{
					return v;
				}
				return hi;
			}
			return lo;
		}
	}
}
