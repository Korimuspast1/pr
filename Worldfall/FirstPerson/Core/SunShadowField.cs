using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FirstPerson.Core
{
	public sealed class SunShadowField
	{
		private readonly struct Lines
		{
			private readonly float[] _heights;

			private readonly float[] _canopy;

			private readonly int _cells;

			private readonly int _lineStride;

			private readonly int _cellStride;

			private readonly int _first;

			private readonly int _dir;

			public Lines(float[] heights, float[] canopy, int cells, int lineStride, int cellStride, int first, int dir)
			{
				_heights = heights;
				_canopy = canopy;
				_cells = cells;
				_lineStride = lineStride;
				_cellStride = cellStride;
				_first = first;
				_dir = dir;
			}

			public float At(int k, float u, out float most)
			{
				if (!(u >= -0.5f) || !(u <= (float)_cells - 0.5f))
				{
					most = 0f;
					return 0f;
				}
				if (u < 0f)
				{
					u = 0f;
				}
				else if (u > (float)(_cells - 1))
				{
					u = _cells - 1;
				}
				int num = (int)u;
				int num2 = ((num + 1 < _cells) ? (num + 1) : num);
				float num3 = u - (float)num;
				int num4 = (_first + k * _dir) * _lineStride;
				num = num4 + num * _cellStride;
				num2 = num4 + num2 * _cellStride;
				float num5 = _heights[num];
				float num6 = _heights[num2];
				float num7 = num5 + (num6 - num5) * num3;
				if (_canopy == null)
				{
					most = ((num5 > num6) ? num5 : num6);
					return num7;
				}
				float num8 = _canopy[num];
				float num9 = _canopy[num2];
				float num10 = ((num8 > 0f) ? (num5 + num8) : num5);
				float num11 = ((num9 > 0f) ? (num6 + num9) : num6);
				most = ((num10 > num11) ? num10 : num11);
				float num12 = ((!(num3 <= 0.5f)) ? ((num9 > 0f) ? num11 : float.NegativeInfinity) : ((num8 > 0f) ? num10 : float.NegativeInfinity));
				if (!(num12 > num7))
				{
					return num7;
				}
				return num12;
			}
		}

		public const float Bias = 0.15f;

		public const float EdgeSoft = 0.35f;

		public const float Spread = 0.035f;

		public const float Open = -1000f;

		private const float Outside = 0f;

		private const float LeastRise = 0.0175f;

		private const float StandGrow = 1f;

		private const float Side1 = 0.5f;

		private const float Side1Full = 2f;

		private const float Side2 = 0.15f;

		private const float Side2Full = 3f;

		private const float StandSoft = 0.5f;

		private const float StandSoftMost = 2f;

		public int Width;

		public int Height;

		public Vec3 Light;

		public float[] SLand = Empty;

		public float[] DLand = Empty;

		public float[] SAll = Empty;

		public float[] DAll = Empty;

		public int Version;

		private static readonly float[] Empty = new float[0];

		private static int _versions;

		internal static bool TwoThreads = true;

		public float Visibility(float x, float y, float z, bool withCanopy)
		{
			int width = Width;
			int height = Height;
			if (!(x >= 0f) || !(y >= 0f) || !(x < (float)width) || !(y < (float)height))
			{
				return 1f;
			}
			float[] array = (withCanopy ? SAll : SLand);
			float[] array2 = (withCanopy ? DAll : DLand);
			if (array.Length < width * height || array2.Length < width * height)
			{
				return 1f;
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? num3 : 0);
			int num8 = ((num3 + 1 >= width) ? (width - 1) : (num3 + 1));
			int num9 = ((num4 >= 0) ? num4 : 0);
			int num10 = ((num4 + 1 >= height) ? (height - 1) : (num4 + 1));
			int num11 = num9 * width + num7;
			int num12 = num9 * width + num8;
			int num13 = num10 * width + num7;
			int num14 = num10 * width + num8;
			float num15 = array[num11] + (array[num12] - array[num11]) * num5;
			float num16 = array[num13] + (array[num14] - array[num13]) * num5;
			float num17 = array2[num11] + (array2[num12] - array2[num11]) * num5;
			float num18 = array2[num13] + (array2[num14] - array2[num13]) * num5;
			return Lit(z - (num15 + (num16 - num15) * num6), num17 + (num18 - num17) * num6);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Lit(float above, float d)
		{
			float num = (above + 0.15f) / (0.35f + 0.035f * d) + 0.5f;
			if (!(num <= 0f))
			{
				if (!(num >= 1f))
				{
					return num;
				}
				return 1f;
			}
			return 0f;
		}

		private void Both(float x, float y, float z, out float land, out float all)
		{
			land = (all = 1f);
			int width = Width;
			int height = Height;
			int num = width * height;
			if (x >= 0f && y >= 0f && x < (float)width && y < (float)height)
			{
				float num2 = x - 0.5f;
				float num3 = y - 0.5f;
				int num4 = FastMath.FloorInt(num2);
				int num5 = FastMath.FloorInt(num3);
				float tx = num2 - (float)num4;
				float ty = num3 - (float)num5;
				int num6 = ((num4 >= 0) ? num4 : 0);
				int num7 = ((num4 + 1 >= width) ? (width - 1) : (num4 + 1));
				int num8 = ((num5 >= 0) ? num5 : 0);
				int num9 = ((num5 + 1 >= height) ? (height - 1) : (num5 + 1));
				int i = num8 * width + num6;
				int i2 = num8 * width + num7;
				int i3 = num9 * width + num6;
				int i4 = num9 * width + num7;
				if (SLand.Length >= num && DLand.Length >= num)
				{
					land = Lit(z - Bilinear(SLand, i, i2, i3, i4, tx, ty), Bilinear(DLand, i, i2, i3, i4, tx, ty));
				}
				if (SAll == SLand)
				{
					all = land;
				}
				else if (SAll.Length >= num && DAll.Length >= num)
				{
					all = Lit(z - Bilinear(SAll, i, i2, i3, i4, tx, ty), Bilinear(DAll, i, i2, i3, i4, tx, ty));
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Bilinear(float[] v, int i00, int i10, int i01, int i11, float tx, float ty)
		{
			float num = v[i00] + (v[i10] - v[i00]) * tx;
			float num2 = v[i01] + (v[i11] - v[i01]) * tx;
			return num + (num2 - num) * ty;
		}

		public static float Visibility(TerrainCache t, float x, float y, float z, bool withCanopy)
		{
			SunShadowField sunField = t.SunField;
			SunShadowField sunFieldPrev = t.SunFieldPrev;
			float sunFieldBlend = t.SunFieldBlend;
			if (sunField == null)
			{
				return 1f;
			}
			if (withCanopy)
			{
				sunField.Both(x, y, z, out var land, out var all);
				if (sunFieldPrev != null && sunFieldBlend < 1f)
				{
					sunFieldPrev.Both(x, y, z, out var land2, out var all2);
					land = land2 + (land - land2) * sunFieldBlend;
					all = all2 + (all - all2) * sunFieldBlend;
				}
				if (!(all < land))
				{
					return land;
				}
				return all;
			}
			float num = sunField.Visibility(x, y, z, withCanopy: false);
			if (sunFieldPrev == null || sunFieldBlend >= 1f)
			{
				return num;
			}
			float num2 = sunFieldPrev.Visibility(x, y, z, withCanopy: false);
			if (!(sunFieldBlend <= 0f))
			{
				return num2 + (num - num2) * sunFieldBlend;
			}
			return num2;
		}

		public static SunShadowField Bake(float[] heights, float[] canopy, int w, int h, Vec3 light, SunShadowField recycle = null, bool oneThread = false)
		{
			int num = w * h;
			SunShadowField f = new SunShadowField
			{
				Width = w,
				Height = h,
				Light = light,
				Version = Interlocked.Increment(ref _versions)
			};
			float[] array = null;
			float[] a = null;
			float[] a2 = null;
			float[] a3 = null;
			if (recycle != null)
			{
				array = recycle.SLand;
				a = recycle.DLand;
				if (recycle.SAll != array)
				{
					a2 = recycle.SAll;
					a3 = recycle.DAll;
				}
				recycle.SLand = (recycle.DLand = (recycle.SAll = (recycle.DAll = Empty)));
				recycle.Width = (recycle.Height = 0);
			}
			if (w <= 0 || h <= 0 || heights == null || heights.Length < num)
			{
				f.Width = (f.Height = 0);
				return f;
			}
			bool flag = canopy != null && canopy.Length >= num;
			f.SLand = Sized(array, num);
			f.DLand = Sized(a, num);
			if (flag)
			{
				f.SAll = Sized(a2, num);
				f.DAll = Sized(a3, num);
			}
			else
			{
				f.SAll = f.SLand;
				f.DAll = f.DLand;
			}
			float num2 = (float)Math.Sqrt(light.X * light.X + light.Y * light.Y);
			if (num2 < 0.0001f)
			{
				Fill(f.SLand, -1000f);
				Fill(f.DLand, 0f);
				if (flag)
				{
					Fill(f.SAll, -1000f);
					Fill(f.DAll, 0f);
				}
				return f;
			}
			float num3 = light.Z / num2;
			if (!(num3 >= 0.0175f))
			{
				num3 = 0.0175f;
			}
			bool flag2 = Math.Abs(light.X) >= Math.Abs(light.Y);
			float num4 = (flag2 ? light.X : light.Y);
			float num5 = (flag2 ? light.Y : light.X);
			float t = num5 / Math.Abs(num4);
			float step = (float)Math.Sqrt(1f + t * t);
			float drop = Math.Min(step * num3, 1000f);
			int lines = (flag2 ? w : h);
			int cells = (flag2 ? h : w);
			int lineStride = (flag2 ? 1 : w);
			int cellStride = ((!flag2) ? 1 : w);
			int first = ((num4 > 0f) ? (lines - 1) : 0);
			int dir = ((!(num4 > 0f)) ? 1 : (-1));
			if (!flag)
			{
				One(null, f.SLand, f.DLand);
			}
			else if (!TwoThreads || oneThread)
			{
				One(null, f.SLand, f.DLand);
				One(canopy, f.SAll, f.DAll);
			}
			else
			{
				Parallel.Invoke(delegate
				{
					One(null, f.SLand, f.DLand);
				}, delegate
				{
					One(canopy, f.SAll, f.DAll);
				});
			}
			if (flag)
			{
				Soften(f.SLand, f.DLand, f.SAll, f.DAll, lines, cells, lineStride, cellStride, first, dir, t, step, drop);
			}
			return f;
			void One(float[] stands, float[] s, float[] d)
			{
				Sweep(heights, stands, s, d, lines, cells, lineStride, cellStride, first, dir, t, step, drop);
			}
		}

		private static void Soften(float[] sLand, float[] dLand, float[] sAll, float[] dAll, int lines, int cells, int lineStride, int cellStride, int first, int dir, float t, float step, float drop)
		{
			float[] array = new float[cells];
			float[] array2 = new float[cells];
			float[] array3 = new float[cells + 4];
			float[] array4 = new float[cells + 4];
			int num = FastMath.FloorInt(t);
			float num2 = t - (float)num;
			float num3 = 1f * step + 2f * drop;
			float num4 = 14.285714f;
			int num5 = Math.Max(0, -num);
			int num6 = Math.Min(cells, cells - 1 - num);
			if (num6 < num5)
			{
				num6 = num5;
			}
			int num7 = 0;
			int num8 = first;
			while (num7 < lines)
			{
				int num9 = num8 * lineStride;
				int num10 = cells;
				int num11 = -1;
				int num12 = 0;
				int num13 = num9;
				while (num12 < cells)
				{
					float num15;
					if (num12 >= num5 && num12 < num6)
					{
						int num14 = num12 + num;
						num15 = array[num14] + (array[num14 + 1] - array[num14]) * num2;
					}
					else
					{
						num15 = AtSide(array, cells, (float)num12 + t);
					}
					float num16 = sAll[num13] - sLand[num13];
					float num17 = num15 + num3;
					num16 = ((num16 > num17) ? num17 : num16);
					if (!(num16 > 0f))
					{
						num16 = 0f;
					}
					else
					{
						if (num10 > num12)
						{
							num10 = num12;
						}
						num11 = num12;
					}
					array2[num12] = num16;
					array3[num12 + 2] = Beside(num16, 0.5f, 2f);
					array4[num12 + 2] = Beside(num16, 0.15f, 3f);
					num12++;
					num13 += cellStride;
				}
				float[] array5 = array;
				array = array2;
				array2 = array5;
				if (num11 >= 0)
				{
					int num18 = Math.Max(0, num10 - 2);
					int num19 = Math.Min(cells - 1, num11 + 2);
					int num20 = num9 + num18 * cellStride;
					while (num18 <= num19)
					{
						float num21 = array[num18];
						float num22 = array3[num18 + 1];
						float num23 = array3[num18 + 3];
						float num24 = array4[num18];
						float num25 = array4[num18 + 4];
						if (num23 > num22)
						{
							num22 = num23;
						}
						if (num25 > num24)
						{
							num24 = num25;
						}
						if (num24 > num22)
						{
							num22 = num24;
						}
						if (num21 > num22)
						{
							num22 = num21;
						}
						sAll[num20] = sLand[num20] + num21;
						dAll[num20] = dLand[num20] + num4 * ((num22 < 2f) ? num22 : 2f);
						num18++;
						num20 += cellStride;
					}
				}
				num7++;
				num8 += dir;
			}
		}

		private static float AtSide(float[] m, int cells, float u)
		{
			if (!(u > -1f) || !(u < (float)cells))
			{
				return 0f;
			}
			if (u < 0f)
			{
				return m[0] * (1f + u);
			}
			if (u > (float)(cells - 1))
			{
				return m[cells - 1] * ((float)cells - u);
			}
			int num = (int)u;
			int num2 = ((num + 1 < cells) ? (num + 1) : num);
			return m[num] + (m[num2] - m[num]) * (u - (float)num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Beside(float r, float share, float full)
		{
			if (!(r < full))
			{
				return r * share;
			}
			return r * r * (share / full);
		}

		private static void Sweep(float[] heights, float[] canopy, float[] sOut, float[] dOut, int lines, int cells, int lineStride, int cellStride, int first, int dir, float t, float step, float drop)
		{
			float[] array = new float[cells];
			float[] array2 = new float[cells];
			float[] array3 = new float[cells];
			float[] array4 = new float[cells];
			int[] array5 = new int[cells];
			int[] array6 = new int[cells];
			float[] array7 = new float[cells];
			float[] array8 = new float[cells];
			for (int i = 0; i < cells; i++)
			{
				array[i] = 0f;
				array5[i] = -1;
			}
			float[] array9 = null;
			float[] array10 = null;
			float[] array11 = null;
			float[] array12 = null;
			if (canopy != null)
			{
				array9 = new float[cells];
				array10 = new float[cells];
				array11 = new float[cells];
				array12 = new float[cells];
				for (int j = 0; j < cells; j++)
				{
					array9[j] = 0f;
				}
			}
			Lines read = new Lines(heights, canopy, cells, lineStride, cellStride, first, dir);
			int num = FastMath.FloorInt(t);
			float num2 = t - (float)num;
			int num3 = Math.Max(0, -num);
			int num4 = Math.Min(cells, cells - 1 - num);
			if (num4 < num3)
			{
				num4 = num3;
			}
			int num5 = 0;
			int num6 = first;
			while (num5 < lines)
			{
				int num7 = num6 * lineStride;
				for (int k = 0; k < num3; k++)
				{
					Side(heights, canopy, sOut, dOut, array, array2, array3, array4, array9, array10, array11, array12, array5, array6, array7, array8, in read, cells, k, num5, num7 + k * cellStride, t, step, drop);
				}
				int num8 = num3;
				int num9 = num7 + num3 * cellStride;
				while (num8 < num4)
				{
					int num10 = num8 + num;
					float num11 = array[num10];
					float num12 = array[num10 + 1];
					float num13 = array2[num10];
					float num14 = num11 + (num12 - num11) * num2 - drop;
					float num15 = step + (num13 + (array2[num10 + 1] - num13) * num2);
					float num16 = heights[num9];
					if (canopy != null)
					{
						num11 = array9[num10];
						num12 = array9[num10 + 1];
					}
					float num17 = ((num11 > num12) ? num11 : num12);
					float num18 = num17 - drop;
					float num19 = float.NegativeInfinity;
					int num20 = num5 - 1;
					int num21 = -1;
					int num22 = array5[num10];
					int num23 = array5[num10 + 1];
					float num24 = array7[num10];
					float num25 = array7[num10 + 1];
					if (num23 == num22)
					{
						if (num25 > num24)
						{
							num24 = num25;
						}
						num23 = -1;
					}
					if (num22 >= 0)
					{
						float num26 = num5 - num22;
						float most = num24;
						float num27 = num26 * drop;
						if (num24 - num27 > num14)
						{
							float num28 = read.At(num22, (float)num8 + t * num26, out most) - num27;
							if (num28 > num19)
							{
								num19 = num28;
								num21 = num22;
							}
						}
						if (most - num27 > num18)
						{
							num18 = most - num27;
							num20 = num22;
							num17 = most;
						}
					}
					if (num23 >= 0)
					{
						float num29 = num5 - num23;
						float most2 = num25;
						float num30 = num29 * drop;
						if (num25 - num30 > num14)
						{
							float num31 = read.At(num23, (float)num8 + t * num29, out most2) - num30;
							if (num31 > num19)
							{
								num19 = num31;
								num21 = num23;
							}
						}
						if (most2 - num30 > num18)
						{
							num20 = num23;
							num17 = most2;
						}
					}
					array6[num8] = num20;
					array8[num8] = num17;
					sOut[num9] = ((num19 > num14) ? num19 : num14);
					dOut[num9] = ((num19 > num14) ? ((float)(num5 - num21) * step) : num15);
					if (canopy == null)
					{
						Carry(num16, num14, num15, array3, array4, num8);
					}
					else
					{
						float num32 = array9[num10];
						float num33 = array10[num10];
						float s = num32 + (array9[num10 + 1] - num32) * num2 - drop;
						float sd = step + (num33 + (array10[num10 + 1] - num33) * num2);
						Carry(num16, s, sd, array3, array4, num8);
						float num34 = canopy[num9];
						Carry((num34 > 0f) ? (num16 + num34) : num16, s, sd, array11, array12, num8);
					}
					num8++;
					num9 += cellStride;
				}
				for (int l = num4; l < cells; l++)
				{
					Side(heights, canopy, sOut, dOut, array, array2, array3, array4, array9, array10, array11, array12, array5, array6, array7, array8, in read, cells, l, num5, num7 + l * cellStride, t, step, drop);
				}
				float[] array13 = array;
				array = array3;
				array3 = array13;
				float[] array14 = array2;
				array2 = array4;
				array4 = array14;
				int[] array15 = array5;
				array5 = array6;
				array6 = array15;
				float[] array16 = array7;
				array7 = array8;
				array8 = array16;
				if (canopy != null)
				{
					float[] array17 = array9;
					array9 = array11;
					array11 = array17;
					float[] array18 = array10;
					array10 = array12;
					array12 = array18;
				}
				num5++;
				num6 += dir;
			}
		}

		private static void Side(float[] heights, float[] canopy, float[] sOut, float[] dOut, float[] prevM, float[] prevD, float[] curM, float[] curD, float[] prevF, float[] prevFD, float[] curF, float[] curFD, int[] prevK, int[] curK, float[] prevMost, float[] curMost, in Lines read, int cells, int j, int k, int i, float t, float step, float drop)
		{
			float num = (float)j + t;
			int num2 = FastMath.FloorInt(num);
			AtSide(prevM, prevD, cells, num, out var om, out var od);
			float num3 = om - drop;
			float num4 = step + od;
			float num5 = heights[i];
			float most = Most((canopy != null) ? prevF : prevM, cells, num);
			curK[j] = Exact(in read, prevK, prevMost, (num2 >= 0 && num2 < cells) ? num2 : (-1), (num2 + 1 >= 0 && num2 + 1 < cells) ? (num2 + 1) : (-1), j, k, t, drop, most, num3, out var s, out var from, out curMost[j]);
			sOut[i] = ((s > num3) ? s : num3);
			dOut[i] = ((s > num3) ? ((float)(k - from) * step) : num4);
			if (canopy == null)
			{
				Carry(num5, num3, num4, curM, curD, j);
				return;
			}
			AtSide(prevF, prevFD, cells, num, out var om2, out var od2);
			om2 -= drop;
			od2 = step + od2;
			Carry(num5, om2, od2, curM, curD, j);
			float num6 = canopy[i];
			Carry((num6 > 0f) ? (num5 + num6) : num5, om2, od2, curF, curFD, j);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Exact(in Lines read, int[] prevK, float[] prevMost, int a, int b, int j, int k, float t, float drop, float most1, float floor, out float s, out int from, out float passMost)
		{
			s = float.NegativeInfinity;
			from = -1;
			passMost = most1;
			if (k == 0)
			{
				return -1;
			}
			float best = most1 - drop;
			int pass = k - 1;
			int num = ((a >= 0) ? prevK[a] : (-1));
			int num2 = ((b >= 0) ? prevK[b] : (-1));
			float num3 = ((a >= 0) ? prevMost[a] : 0f);
			float num4 = ((b >= 0) ? prevMost[b] : 0f);
			if (num2 == num && num >= 0)
			{
				if (num4 > num3)
				{
					num3 = num4;
				}
				num2 = -1;
			}
			if (num >= 0)
			{
				Candidate(in read, num, num3, j, k, t, drop, floor, ref s, ref from, ref best, ref pass, ref passMost);
			}
			if (num2 >= 0)
			{
				Candidate(in read, num2, num4, j, k, t, drop, floor, ref s, ref from, ref best, ref pass, ref passMost);
			}
			return pass;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Candidate(in Lines read, int kc, float carried, int j, int k, float t, float drop, float floor, ref float s, ref int from, ref float best, ref int pass, ref float passMost)
		{
			float num = k - kc;
			float most = carried;
			if (carried - num * drop > floor)
			{
				float num2 = read.At(kc, (float)j + t * num, out most) - num * drop;
				if (num2 > s)
				{
					s = num2;
					from = kc;
				}
			}
			float num3 = most - num * drop;
			if (num3 > best)
			{
				best = num3;
				pass = kc;
				passMost = most;
			}
		}

		private static float Most(float[] m, int cells, float u)
		{
			if (!(u >= -0.5f) || !(u <= (float)cells - 0.5f))
			{
				return 0f;
			}
			if (u < 0f)
			{
				u = 0f;
			}
			else if (u > (float)(cells - 1))
			{
				u = cells - 1;
			}
			int num = (int)u;
			int num2 = ((num + 1 < cells) ? (num + 1) : num);
			if (!(m[num] > m[num2]))
			{
				return m[num2];
			}
			return m[num];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Carry(float top, float s, float sd, float[] m, float[] d, int j)
		{
			if (top >= s)
			{
				m[j] = top;
				d[j] = 0f;
			}
			else
			{
				m[j] = s;
				d[j] = sd;
			}
		}

		private static void AtSide(float[] m, float[] d, int cells, float u, out float om, out float od)
		{
			if (!(u > -1f) || !(u < (float)cells))
			{
				om = 0f;
				od = 0f;
			}
			else if (u < 0f || u > (float)(cells - 1))
			{
				int num = ((!(u < 0f)) ? (cells - 1) : 0);
				float num2 = ((u < 0f) ? (1f + u) : ((float)cells - u));
				om = 0f + (m[num] - 0f) * num2;
				od = d[num] * num2;
			}
			else
			{
				int num3 = (int)u;
				int num4 = ((num3 + 1 < cells) ? (num3 + 1) : num3);
				float num5 = u - (float)num3;
				om = m[num3] + (m[num4] - m[num3]) * num5;
				od = d[num3] + (d[num4] - d[num3]) * num5;
			}
		}

		private static float[] Sized(float[] a, int n)
		{
			if (a == null || a.Length != n)
			{
				return new float[n];
			}
			return a;
		}

		private static void Fill(float[] a, float v)
		{
			for (int i = 0; i < a.Length; i++)
			{
				a[i] = v;
			}
		}
	}
}
