using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class Basins
	{
		public const float MinDepth = 0.18f;

		public static string Why;

		public static Basin Pour(float[] bed, Func<int, bool> away, int w, int h, int seed, int most)
		{
			Why = null;
			if (seed < 0 || seed >= w * h || away(seed))
			{
				Why = "runs away at once";
				return null;
			}
			HashSet<int> hashSet = new HashSet<int> { seed };
			SortedSet<(float, int)> frontier = new SortedSet<(float, int)>();
			HashSet<int> queued = new HashSet<int> { seed };
			List<int> list = new List<int> { seed };
			float num = bed[seed];
			int spill = seed;
			Push(seed);
			while (frontier.Count > 0)
			{
				(float, int) min = frontier.Min;
				frontier.Remove(min);
				int item = min.Item2;
				if (item >= 0 && hashSet.Contains(item))
				{
					continue;
				}
				bool flag = item >= 0 && min.Item1 != float.MinValue && bed[item] < num - 0.0001f;
				if (flag)
				{
					List<int> list2 = Below(bed, away, w, h, item, num, hashSet, most - list.Count);
					if (list2 != null)
					{
						foreach (int item4 in list2)
						{
							hashSet.Add(item4);
							list.Add(item4);
							queued.Add(item4);
						}
						foreach (int item5 in list2)
						{
							Push(item5);
						}
						continue;
					}
				}
				if (item < 0 || min.Item1 == float.MinValue || flag)
				{
					Basin basin = new Basin
					{
						Rim = num,
						Floor = bed[seed],
						Spill = spill,
						Out = item
					};
					if (basin.Depth < 0.18f)
					{
						Why = $"only {basin.Depth:F2} deep before it runs out";
						return null;
					}
					List<(float, int)> list3 = new List<(float, int)>();
					foreach (int item6 in list)
					{
						if (bed[item6] < num - 0.0001f)
						{
							list3.Add((bed[item6], item6));
						}
					}
					list3.Sort();
					foreach (var (item2, item3) in list3)
					{
						basin.Tiles.Add(item3);
						basin.Beds.Add(item2);
					}
					if (basin.Tiles.Count > 0)
					{
						basin.Floor = basin.Beds[0];
					}
					if (basin.Tiles.Count <= 0)
					{
						return null;
					}
					return basin;
				}
				if (bed[item] > num)
				{
					num = bed[item];
					spill = item;
				}
				hashSet.Add(item);
				list.Add(item);
				if (list.Count > most)
				{
					Why = $"over {most} tiles";
					return null;
				}
				Push(item);
			}
			return null;
			void Push(int i)
			{
				int num2 = i % w;
				int num3 = i / w;
				for (int j = -1; j <= 1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						if ((k | j) != 0 && (k == 0 || j == 0))
						{
							int num4 = num2 + k;
							int num5 = num3 + j;
							if (num4 < 0 || num5 < 0 || num4 >= w || num5 >= h)
							{
								queued.Add(-1);
								frontier.Add((float.MinValue, -1));
							}
							else
							{
								int num6 = num5 * w + num4;
								if (queued.Add(num6))
								{
									frontier.Add((away(num6) ? float.MinValue : bed[num6], num6));
								}
							}
						}
					}
				}
			}
		}

		private static List<int> Below(float[] bed, Func<int, bool> away, int w, int h, int from, float level, HashSet<int> taken, int most)
		{
			List<int> list = new List<int> { from };
			HashSet<int> hashSet = new HashSet<int> { from };
			for (int i = 0; i < list.Count; i++)
			{
				int num = list[i];
				int num2 = num % w;
				int num3 = num / w;
				if (away(num))
				{
					return null;
				}
				for (int j = 0; j < 4; j++)
				{
					int num4;
					switch (j)
					{
					default:
						num4 = 0;
						break;
					case 1:
						num4 = -1;
						break;
					case 0:
						num4 = 1;
						break;
					}
					int num5 = num2 + num4;
					int num6;
					switch (j)
					{
					default:
						num6 = 0;
						break;
					case 3:
						num6 = -1;
						break;
					case 2:
						num6 = 1;
						break;
					}
					int num7 = num3 + num6;
					if (num5 < 0 || num7 < 0 || num5 >= w || num7 >= h)
					{
						return null;
					}
					int num8 = num7 * w + num5;
					if (!taken.Contains(num8) && hashSet.Add(num8))
					{
						if (away(num8))
						{
							return null;
						}
						if (bed[num8] < level - 0.0001f)
						{
							list.Add(num8);
						}
					}
				}
				if (list.Count > most)
				{
					return null;
				}
			}
			return list;
		}

		public static List<int> Run(float[] ground, Func<int, bool> away, int w, int h, int from, int most)
		{
			List<int> list = new List<int>();
			int num = from;
			HashSet<int> hashSet = new HashSet<int>();
			while (num >= 0 && list.Count < most && hashSet.Add(num))
			{
				list.Add(num);
				if (away(num))
				{
					break;
				}
				int num2 = num % w;
				int num3 = num / w;
				int num4 = -1;
				float num5 = ground[num] + 0.04f;
				for (int i = 0; i < 4; i++)
				{
					int num6;
					switch (i)
					{
					default:
						num6 = 0;
						break;
					case 1:
						num6 = -1;
						break;
					case 0:
						num6 = 1;
						break;
					}
					int num7 = num2 + num6;
					int num8;
					switch (i)
					{
					default:
						num8 = 0;
						break;
					case 3:
						num8 = -1;
						break;
					case 2:
						num8 = 1;
						break;
					}
					int num9 = num3 + num8;
					if (num7 < 0 || num9 < 0 || num7 >= w || num9 >= h)
					{
						continue;
					}
					int num10 = num9 * w + num7;
					if (!hashSet.Contains(num10))
					{
						float num11 = (away(num10) ? float.MinValue : ground[num10]);
						if (num11 < num5)
						{
							num5 = num11;
							num4 = num10;
						}
					}
				}
				num = num4;
			}
			return list;
		}
	}
}
