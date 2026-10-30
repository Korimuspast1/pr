using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class ReliefGen
	{
		public const float RollLess = 0.5f;

		public static float Upland = 1.7f;

		public static float UplandVary = 0.45f;

		public static float BankRun = 1.6f;

		public static float WallRun = 3.2f;

		public static float BankRise = 0.18f;

		public static float SeaRun = 7f;

		public static float GullyDepth = 1.4f;

		public static float GullyStart = 30f;

		public static float GullyFull = 1500f;

		public static float GullyWide = 1.6f;

		public static float GullyWider = 3.4f;

		public static float DrainNoise = 0.9f;

		public static float RidgeHeight = 0.55f;

		public static float CliffLow = 1.1f;

		public static float CliffHigh = 2.5f;

		public static float CliffReach = 3.5f;

		public static int OpenReach = 28;

		public static float TarnRange = 14f;

		public static float TarnRadius = 3.1f;

		public static float TarnDepth = 2.2f;

		public static float TarnSpacing = 14f;

		public static float MostDown = 2.5f;

		public static float MostUp = 2.6f;

		public static readonly List<int> LastTarns = new List<int>();

		private const float Far = 1000000f;

		public static float TarnCut = 11f;

		public static float TarnSteep = 8.5f;

		public static readonly List<int> Chosen = new List<int>();

		public static int TarnsCarved;

		public static int TarnsSteep;

		public static short[] Build(int w, int h, byte[] codes, Landforms forms, byte[] sea, int seed, List<int> tarns = null)
		{
			int num = w * h;
			short[] array = new short[num];
			if (num == 0 || codes == null || codes.Length < num)
			{
				return array;
			}
			bool flag = forms != null && forms.Form.Length == num;
			bool flag2 = sea != null && sea.Length == num;
			float num2 = (float)(seed & 0xFFF) * 0.37f;
			float num3 = (float)((seed >> 12) & 0xFFF) * 0.29f;
			float[] array2 = new float[num];
			for (int i = 0; i < num; i++)
			{
				array2[i] = ((codes[i] == 0 && (!flag2 || sea[i] > OpenReach)) ? 0f : 1000000f);
			}
			Chamfer(array2, w, h);
			float[] array3 = new float[num];
			float[] array4 = new float[num];
			float[] array5 = new float[num];
			for (int j = 0; j < num; j++)
			{
				bool flag3 = codes[j] == 0;
				bool flag4 = flag3 && array2[j] > 4f;
				array3[j] = (flag4 ? 0f : 1000000f);
				array4[j] = ((flag3 && !flag4) ? 0f : 1000000f);
				array5[j] = ((codes[j] >= 2) ? 0f : 1000000f);
			}
			Chamfer(array3, w, h);
			Chamfer(array4, w, h);
			Chamfer(array5, w, h);
			float[] array6 = new float[num];
			for (int k = 0; k < h; k++)
			{
				for (int l = 0; l < w; l++)
				{
					int num4 = k * w + l;
					byte b = codes[num4];
					if (b != 0 && b < 3)
					{
						float num5 = array3[num4];
						float num6 = array4[num4];
						float val = ((num5 <= BankRun) ? (BankRise * num5 / BankRun / ((Upland > 0f) ? Upland : 1f)) : Lerp(BankRise / Math.Max(0.01f, Upland), 1f, Smooth(BankRun, BankRun + WallRun, num5)));
						float val2 = 1f - (float)Math.Exp((0f - Math.Max(0f, num6 - 0.5f)) / SeaRun);
						float num7 = 1f + UplandVary * (2f * Noise((float)l / 23f + num2, (float)k / 23f + num3) - 1f);
						float num8 = Upland * num7 * Math.Min(val, val2);
						float num9 = Smooth(BankRun + WallRun, BankRun + WallRun + 6f, Math.Min(num5, num6 + 3f));
						num8 += RidgeHeight * num9 * Ridge((float)l / 11f + num2 * 1.7f, (float)k / 11f - num3 * 1.3f);
						if (b == 2)
						{
							num8 *= 0.5f;
						}
						array6[num4] = num8;
					}
				}
			}
			float[] array7 = Gullies(w, h, codes, array6, array3, array4, seed, num2, num3);
			float[] array8 = new float[num];
			for (int m = 0; m < num; m++)
			{
				array8[m] = array6[m] - array7[m];
			}
			if (CliffHigh > 0f)
			{
				Cliffs(w, h, codes, array4, array5, array8, num2, num3);
			}
			tarns?.Clear();
			if (flag && TarnDepth > 0f)
			{
				Tarns(w, h, codes, forms, array8, seed, tarns);
			}
			for (int n = 0; n < num; n++)
			{
				if (codes[n] != 0)
				{
					float num10 = array8[n];
					num10 = ((num10 < 0f - MostDown) ? (0f - MostDown) : ((num10 > MostUp) ? MostUp : num10));
					if (num10 < 0f && Math.Min(array3[n], array4[n]) < 2.5f)
					{
						num10 = 0f;
					}
					array[n] = Relief.ToSteps(num10);
				}
			}
			return array;
		}

		private static float[] Gullies(int w, int h, byte[] codes, float[] up, float[] river, float[] shoreOf, int seed, float ox, float oy)
		{
			int num = w * h;
			float[] array = new float[num];
			if (GullyDepth <= 0f)
			{
				return array;
			}
			float[] array2 = new float[num];
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					int num4 = i * w + j;
					if (codes[num4] == 0)
					{
						array2[num4] = -1f;
						continue;
					}
					float num5 = up[num4] + DrainNoise * Noise((float)j / 19f + ox * 1.3f, (float)i / 19f - oy * 0.7f) + (float)(TerrainModel.Hash(num4 ^ seed, 77) & 0x3FF) * 2.9325513E-05f;
					if (codes[num4] >= 3)
					{
						num5 += 4f;
					}
					array2[num4] = num5;
					if (num5 < num2)
					{
						num2 = num5;
					}
					if (num5 > num3)
					{
						num3 = num5;
					}
				}
			}
			if (num2 > num3)
			{
				return array;
			}
			num3 = FillHollows(array2, w, h, num2, num3);
			int[] array3 = new int[4097];
			float num6 = 4095f / Math.Max(0.0001f, num3 - num2);
			for (int k = 0; k < num; k++)
			{
				if (array2[k] >= 0f)
				{
					array3[(int)((num3 - array2[k]) * num6) + 1]++;
				}
			}
			for (int l = 1; l <= 4096; l++)
			{
				array3[l] += array3[l - 1];
			}
			int[] array4 = new int[array3[4096]];
			int[] array5 = (int[])array3.Clone();
			for (int m = 0; m < num; m++)
			{
				if (array2[m] >= 0f)
				{
					array4[array5[(int)((num3 - array2[m]) * num6)]++] = m;
				}
			}
			int[] array6 = new int[num];
			float[] array7 = new float[num];
			foreach (int num7 in array4)
			{
				int num8 = num7 % w;
				int num9 = num7 / w;
				array7[num7] += 1f;
				float num10 = array2[num7];
				int num11 = -1;
				for (int num12 = -1; num12 <= 1; num12++)
				{
					int num13 = num9 + num12;
					if (num13 < 0 || num13 >= h)
					{
						continue;
					}
					for (int num14 = -1; num14 <= 1; num14++)
					{
						int num15 = num8 + num14;
						if ((num14 | num12) != 0 && num15 >= 0 && num15 < w)
						{
							int num16 = num13 * w + num15;
							float num17 = ((array2[num16] < 0f) ? (-1f) : array2[num16]);
							float num18 = (array2[num7] - num17) * (((num14 & num12) != 0) ? 0.7071f : 1f);
							if (array2[num7] - num18 < num10 - 1E-06f || (num11 < 0 && num17 < num10))
							{
								num10 = array2[num7] - num18;
								num11 = num16;
							}
						}
					}
				}
				array6[num7] = num11;
				if (num11 >= 0 && array2[num11] >= 0f)
				{
					array7[num11] += array7[num7];
				}
			}
			float[] array8 = new float[num];
			float[] array9 = new float[num];
			float a = (float)Math.Log(GullyStart);
			float b = (float)Math.Log(GullyFull);
			for (int num19 = 0; num19 < num; num19++)
			{
				if (!(array2[num19] < 0f) && codes[num19] < 3 && !(array7[num19] < GullyStart))
				{
					float num20 = Smooth(a, b, (float)Math.Log(array7[num19]));
					array8[num19] = GullyDepth * (0.35f + 0.65f * num20);
					array9[num19] = GullyWide + GullyWider * num20;
				}
			}
			foreach (int num22 in array4)
			{
				int num23 = array6[num22];
				if (num23 >= 0 && !(array9[num22] <= 0f) && !(array9[num23] <= 0f))
				{
					float num24 = up[num22] - array8[num22];
					if (up[num23] - array8[num23] > num24)
					{
						array8[num23] = Math.Min(GullyDepth * 1.6f, up[num23] - num24);
					}
				}
			}
			float[] array10 = new float[num];
			int[] array11 = new int[num];
			for (int num25 = 0; num25 < num; num25++)
			{
				bool flag = array9[num25] > 0f;
				array10[num25] = (flag ? 0f : 1000000f);
				array11[num25] = (flag ? num25 : (-1));
			}
			NearestChamfer(array10, array11, w, h);
			for (int num26 = 0; num26 < num; num26++)
			{
				int num27 = array11[num26];
				if (num27 >= 0 && !(array2[num26] < 0f) && codes[num26] < 3)
				{
					float num28 = array10[num26] / array9[num27];
					if (!(num28 >= 1f))
					{
						float num29 = 1f - num28 * num28;
						float num30 = Smooth(BankRun * 0.5f, BankRun + 1.5f, Math.Min(river[num26], shoreOf[num26] + 1f));
						array[num26] = array8[num27] * num29 * num29 * num30;
					}
				}
			}
			return array;
		}

		private static float FillHollows(float[] e, int w, int h, float lo, float hi)
		{
			int num = w * h;
			float num2 = Math.Max(0.001f, hi - lo + 1f);
			float scale = 8191f / num2;
			List<int>[] queue = new List<int>[8192];
			bool[] array = new bool[num];
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					int num3 = i * w + j;
					if (e[num3] < 0f)
					{
						array[num3] = true;
						continue;
					}
					bool flag = j == 0 || i == 0 || j == w - 1 || i == h - 1;
					if (!flag)
					{
						for (int k = -1; k <= 1; k++)
						{
							if (flag)
							{
								break;
							}
							for (int l = -1; l <= 1; l++)
							{
								if (e[(i + k) * w + j + l] < 0f)
								{
									flag = true;
									break;
								}
							}
						}
					}
					if (flag)
					{
						array[num3] = true;
						Push(num3);
					}
				}
			}
			float num4 = hi;
			for (int m = 0; m < 8192; m++)
			{
				List<int> list = queue[m];
				if (list == null)
				{
					continue;
				}
				for (int n = 0; n < list.Count; n++)
				{
					int num5 = list[n];
					int num6 = num5 % w;
					int num7 = num5 / w;
					float num8 = e[num5];
					for (int num9 = -1; num9 <= 1; num9++)
					{
						int num10 = num7 + num9;
						if (num10 < 0 || num10 >= h)
						{
							continue;
						}
						for (int num11 = -1; num11 <= 1; num11++)
						{
							int num12 = num6 + num11;
							if ((num11 | num9) == 0 || num12 < 0 || num12 >= w)
							{
								continue;
							}
							int num13 = num10 * w + num12;
							if (!array[num13])
							{
								array[num13] = true;
								if (e[num13] <= num8 + 0.0001f)
								{
									e[num13] = num8 + 0.0001f;
								}
								if (e[num13] > num4)
								{
									num4 = e[num13];
								}
								int num14 = Bucket(e[num13]);
								if (num14 <= m)
								{
									list.Add(num13);
								}
								else
								{
									(queue[num14] ?? (queue[num14] = new List<int>())).Add(num13);
								}
							}
						}
					}
				}
				queue[m] = null;
			}
			return num4;
			int Bucket(float v)
			{
				return Math.Max(0, Math.Min(8191, (int)((v - lo) * scale)));
			}
			void Push(int num16)
			{
				int num15 = Bucket(e[num16]);
				(queue[num15] ?? (queue[num15] = new List<int>())).Add(num16);
			}
		}

		private static void NearestChamfer(float[] d, int[] src, int w, int h)
		{
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					int num = i * w + j;
					if (d[num] == 0f)
					{
						continue;
					}
					if (j > 0)
					{
						Take(num, num - 1, 1f);
					}
					if (i > 0)
					{
						Take(num, num - w, 1f);
						if (j > 0)
						{
							Take(num, num - w - 1, 1.4142f);
						}
						if (j < w - 1)
						{
							Take(num, num - w + 1, 1.4142f);
						}
					}
				}
			}
			for (int num2 = h - 1; num2 >= 0; num2--)
			{
				for (int num3 = w - 1; num3 >= 0; num3--)
				{
					int num4 = num2 * w + num3;
					if (d[num4] != 0f)
					{
						if (num3 < w - 1)
						{
							Take(num4, num4 + 1, 1f);
						}
						if (num2 < h - 1)
						{
							Take(num4, num4 + w, 1f);
							if (num3 < w - 1)
							{
								Take(num4, num4 + w + 1, 1.4142f);
							}
							if (num3 > 0)
							{
								Take(num4, num4 + w - 1, 1.4142f);
							}
						}
					}
				}
			}
			void Take(int num7, int num6, float step)
			{
				float num5 = d[num6] + step;
				if (num5 < d[num7])
				{
					d[num7] = num5;
					src[num7] = src[num6];
				}
			}
		}

		private static void Cliffs(int w, int h, byte[] codes, float[] shore, float[] high, float[] shape, float ox, float oy)
		{
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					int num = i * w + j;
					byte b = codes[num];
					if (b == 0 || b >= 3)
					{
						continue;
					}
					float num2 = shore[num];
					if (num2 > 6f)
					{
						continue;
					}
					float num3 = ((b >= 2) ? 1f : (1f - Smooth(CliffReach - 1.5f, CliffReach, high[num])));
					if (num3 <= 0f)
					{
						continue;
					}
					int num4 = 0;
					int num5 = 0;
					for (int k = -3; k <= 3; k++)
					{
						int num6 = i + k;
						if (num6 < 0 || num6 >= h)
						{
							continue;
						}
						for (int l = -3; l <= 3; l++)
						{
							int num7 = j + l;
							if (num7 >= 0 && num7 < w)
							{
								num5++;
								if (shore[num6 * w + num7] == 0f)
								{
									num4++;
								}
							}
						}
					}
					float v = ((num5 > 0) ? ((float)num4 / (float)num5) : 0f);
					float num8 = Smooth(0.15f, 0.55f, v);
					float num9 = Lerp(CliffLow, CliffHigh, Noise((float)j / 9f - ox, (float)i / 9f + oy)) * (0.45f + 0.75f * num8);
					float num10 = num3 * (1f - Smooth(3f, 6f, num2));
					float num11 = num9 * num10;
					if (num11 > shape[num])
					{
						shape[num] = num11;
					}
				}
			}
		}

		private static void Tarns(int w, int h, byte[] codes, Landforms forms, float[] shape, int seed, List<int> tarns)
		{
			List<int> list = new List<int>();
			for (int i = 2; i < h - 2; i++)
			{
				for (int j = 2; j < w - 2; j++)
				{
					int num = i * w + j;
					if (codes[num] < 3 || forms.Form[num] < 3 || forms.RangeSize[num] < TarnRange)
					{
						continue;
					}
					float num2 = forms.MountainDepth[num];
					if (num2 < 2.5f || num2 > 9f || (TerrainModel.Hash(j * 31 + seed, i * 17 - seed) & 0x3FF) > 80)
					{
						continue;
					}
					bool flag = true;
					foreach (int item in list)
					{
						float num3 = item % w - j;
						float num4 = item / w - i;
						if (num3 * num3 + num4 * num4 < 25f)
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						list.Add(num);
					}
				}
			}
			foreach (int item2 in list)
			{
				tarns?.Add(item2);
			}
			lock (LastTarns)
			{
				LastTarns.Clear();
				LastTarns.AddRange(list);
			}
		}

		public static List<int> CarveTarns(short[] gen, IList<int> sites, int w, int h, Func<int, float> height, Func<int, bool> rock)
		{
			List<int> list = new List<int>();
			TarnsCarved = (TarnsSteep = 0);
			if (gen == null || sites == null)
			{
				return list;
			}
			int num = (int)Math.Ceiling(TarnRadius + 1.5f);
			List<(float, int, float)> list2 = new List<(float, int, float)>();
			foreach (int site in sites)
			{
				int num2 = site % w;
				int num3 = site / w;
				float num4 = float.MaxValue;
				for (int i = Math.Max(0, num3 - num); i <= Math.Min(h - 1, num3 + num); i++)
				{
					for (int j = Math.Max(0, num2 - num); j <= Math.Min(w - 1, num2 + num); j++)
					{
						float num5 = (float)Math.Sqrt((j - num2) * (j - num2) + (i - num3) * (i - num3));
						if (num5 >= TarnRadius && num5 <= TarnRadius + 1.5f)
						{
							num4 = Math.Min(num4, height(i * w + j));
						}
					}
				}
				if (num4 != float.MaxValue)
				{
					list2.Add((height(site) - num4, site, num4));
				}
			}
			list2.Sort(((float, int, float) a, (float, int, float) b) => a.Item1.CompareTo(b.Item1));
			int num6 = Math.Max(1, list2.Count / 8);
			List<(int, float)> list3 = new List<(int, float)>();
			foreach (var (num7, num8, item) in list2)
			{
				if (list3.Count >= num6)
				{
					break;
				}
				if (num7 > TarnSteep)
				{
					TarnsSteep++;
					continue;
				}
				bool flag = true;
				foreach (var item6 in list3)
				{
					int item2 = item6.Item1;
					float num9 = item2 % w - num8 % w;
					float num10 = item2 / w - num8 / w;
					if (num9 * num9 + num10 * num10 < TarnSpacing * TarnSpacing)
					{
						flag = false;
						break;
					}
				}
				if (flag)
				{
					list3.Add((num8, item));
				}
			}
			Chosen.Clear();
			foreach (var item7 in list3)
			{
				int item3 = item7.Item1;
				Chosen.Add(item3);
			}
			foreach (var item8 in list3)
			{
				int item4 = item8.Item1;
				float item5 = item8.Item2;
				int num11 = item4 % w;
				int num12 = item4 / w;
				float num13 = item5;
				TarnsCarved++;
				for (int num14 = Math.Max(0, num12 - num); num14 <= Math.Min(h - 1, num12 + num); num14++)
				{
					for (int num15 = Math.Max(0, num11 - num); num15 <= Math.Min(w - 1, num11 + num); num15++)
					{
						int num16 = num14 * w + num15;
						float num17 = (float)Math.Sqrt((num15 - num11) * (num15 - num11) + (num14 - num12) * (num14 - num12)) / TarnRadius;
						if (!(num17 >= 1f) && rock(num16))
						{
							float num18 = 1f - num17 * num17;
							float num19 = num13 - 0.25f - TarnDepth * num18;
							float num20 = height(num16);
							if (!(num20 <= num19))
							{
								float tiles = Math.Min(TarnCut, num20 - num19);
								int val = gen[num16] - Relief.ToSteps(tiles);
								gen[num16] = (short)Math.Max(-32767, val);
								list.Add(num16);
							}
						}
					}
				}
			}
			return list;
		}

		private static void Chamfer(float[] d, int w, int h)
		{
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					int num = i * w + j;
					float num2 = d[num];
					if (num2 == 0f)
					{
						continue;
					}
					if (j > 0)
					{
						num2 = Math.Min(num2, d[num - 1] + 1f);
					}
					if (i > 0)
					{
						num2 = Math.Min(num2, d[num - w] + 1f);
						if (j > 0)
						{
							num2 = Math.Min(num2, d[num - w - 1] + 1.4142f);
						}
						if (j < w - 1)
						{
							num2 = Math.Min(num2, d[num - w + 1] + 1.4142f);
						}
					}
					d[num] = num2;
				}
			}
			for (int num3 = h - 1; num3 >= 0; num3--)
			{
				for (int num4 = w - 1; num4 >= 0; num4--)
				{
					int num5 = num3 * w + num4;
					float num6 = d[num5];
					if (num6 != 0f)
					{
						if (num4 < w - 1)
						{
							num6 = Math.Min(num6, d[num5 + 1] + 1f);
						}
						if (num3 < h - 1)
						{
							num6 = Math.Min(num6, d[num5 + w] + 1f);
							if (num4 < w - 1)
							{
								num6 = Math.Min(num6, d[num5 + w + 1] + 1.4142f);
							}
							if (num4 > 0)
							{
								num6 = Math.Min(num6, d[num5 + w - 1] + 1.4142f);
							}
						}
						d[num5] = num6;
					}
				}
			}
		}

		private static float Noise(float x, float y)
		{
			return TerrainModel.ValueNoise(x, y) * 0.65f + TerrainModel.ValueNoise(x * 2.3f + 5.1f, y * 2.3f - 3.7f) * 0.35f;
		}

		private static float Ridge(float x, float y)
		{
			float num = 1f - Math.Abs(2f * TerrainModel.ValueNoise(x, y) - 1f);
			return num * num;
		}

		private static float Smooth(float a, float b, float v)
		{
			float num = (v - a) / (b - a);
			num = ((num < 0f) ? 0f : ((num > 1f) ? 1f : num));
			return num * num * (3f - 2f * num);
		}

		private static float Lerp(float a, float b, float t)
		{
			return a + (b - a) * t;
		}
	}
}
