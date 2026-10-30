using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public sealed class TerrainCache
	{
		private sealed class RelightJob : WorkerPool.Job
		{
			public const int RowsPerChunk = 4;

			public TerrainCache Cache;

			public int X0;

			public int X1;

			public int Y0;

			public int Y1;

			public Vec3 L;

			public float Sdx;

			public float Sdy;

			public float Rise;

			public bool Soft;

			public override void Run(int chunk, int worker)
			{
				int num = Y0 + chunk * 4;
				int num2 = Math.Min(Y1, num + 4);
				for (int i = num; i < num2; i++)
				{
					Cache.RelightRow(i, X0, X1, L, Sdx, Sdy, Rise, Soft);
				}
			}
		}

		public int Width;

		public int Height;

		public float[] Heights = new float[0];

		public float[] Ground = new float[0];

		public float[] Floor = new float[0];

		public float MaxHeight = 100f;

		public float[] BlockMax = new float[0];

		public int BlocksX;

		public int BlocksY;

		public const int BlockShift = 3;

		public const int BlockSize = 8;

		public uint[] BaseColors = new uint[0];

		public uint[] Colors = new uint[0];

		public bool Seasonal;

		public byte[] Sun = new byte[0];

		public byte[] Open = new byte[0];

		public byte[] Flags = new byte[0];

		public byte[] Materials = new byte[0];

		public byte[] Marks = new byte[0];

		public byte[] SeasonMaterials = new byte[0];

		public readonly SnowCover Snow = new SnowCover();

		public int[] PathSlot = new int[0];

		public byte[] PathTexels = new byte[0];

		public readonly List<int> PathTileList = new List<int>();

		public byte[] SeaReach = new byte[0];

		public const float SeaReachMost = 31.875f;

		public uint OutsideColor = Rgb(42, 94, 176);

		public float OutsideHeight;

		public bool OutsideIsWater = true;

		public bool CastShadows = true;

		public bool SoftLandShadows;

		public SunShadowField SunField;

		public SunShadowField SunFieldPrev;

		public float SunFieldBlend = 1f;

		public bool DitherEdges = true;

		public Vec3 LightDir = new Vec3(-0.45f, 0.55f, 0.7f).Normalized();

		private static readonly uint SlopeRock = Rgb(120, 114, 108);

		public const float CliffDrop = 1.4f;

		private byte[] _seaScratch = new byte[0];

		private int _changedX0;

		private int _changedY0;

		private int _changedX1;

		private int _changedY1;

		private readonly object _changedLock = new object();

		public const int HollowReach = 2;

		public const byte MaskHole = 1;

		public const byte MaskBroken = 2;

		public const byte MaskShaped = 4;

		public const float ShoreWobble = 0.18f;

		private const float ShoreRamp = 0.32f;

		private static WorkerPool _lightPool;

		private static volatile bool _lightPoolTried;

		private static readonly object LightPoolGate = new object();

		private static readonly RelightJob _relightJob = new RelightJob();

		public byte[] SeaOpen = new byte[0];

		public bool HasSeabed;

		private static readonly uint[] BakedSea = BuildBakedSea();

		public void Allocate(int w, int h)
		{
			Width = w;
			Height = h;
			int num = w * h;
			Heights = new float[num];
			Ground = new float[num];
			Floor = new float[num];
			BlocksX = w + 8 - 1 >> 3;
			BlocksY = h + 8 - 1 >> 3;
			BlockMax = new float[BlocksX * BlocksY];
			for (int i = 0; i < BlockMax.Length; i++)
			{
				BlockMax[i] = float.MaxValue;
			}
			BaseColors = new uint[num];
			Colors = new uint[num];
			Sun = new byte[num];
			Open = new byte[num];
			Flags = new byte[num];
			Materials = new byte[num];
			Marks = new byte[num];
			SeaOpen = new byte[num];
			SeasonMaterials = new byte[num];
			PathSlot = new int[num];
			PathTileList.Clear();
			SeaReach = new byte[0];
			Snow.Allocate(w, h);
			SunField = (SunFieldPrev = null);
			SunFieldBlend = 1f;
			MarkChanged(0, 0, w, h);
		}

		public void UpdateSeaReach(byte[] codes = null, byte waterCode = 0)
		{
			int width = Width;
			int height = Height;
			int num = width * height;
			if (num > 0 && (codes == null || codes.Length >= num))
			{
				if (_seaScratch.Length != num)
				{
					_seaScratch = new byte[num];
				}
				byte[] seaScratch = _seaScratch;
				for (int i = 0; i < num; i++)
				{
					seaScratch[i] = (byte)(((codes != null) ? (codes[i] == waterCode) : ((byte)(Flags[i] & 1) != 0)) ? 255u : 0u);
				}
				SeaChamfer(seaScratch, width, height);
				ApplySeaReach(seaScratch);
			}
		}

		public static byte[] SeaReachOf(byte[] codes, byte waterCode, int w, int h)
		{
			int num = w * h;
			byte[] array = new byte[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = (byte)((codes[i] == waterCode) ? 255u : 0u);
			}
			SeaChamfer(array, w, h);
			return array;
		}

		private static void SeaChamfer(byte[] d, int w, int h)
		{
			for (int i = 0; i < h; i++)
			{
				int num = i * w;
				for (int j = 0; j < w; j++)
				{
					int num2 = num + j;
					int num3 = d[num2];
					if (num3 == 0)
					{
						continue;
					}
					if (j > 0)
					{
						num3 = Math.Min(num3, d[num2 - 1] + 8);
					}
					if (i > 0)
					{
						num3 = Math.Min(num3, d[num2 - w] + 8);
						if (j > 0)
						{
							num3 = Math.Min(num3, d[num2 - w - 1] + 11);
						}
						if (j < w - 1)
						{
							num3 = Math.Min(num3, d[num2 - w + 1] + 11);
						}
					}
					d[num2] = (byte)num3;
				}
			}
			for (int num4 = h - 1; num4 >= 0; num4--)
			{
				int num5 = num4 * w;
				for (int num6 = w - 1; num6 >= 0; num6--)
				{
					int num7 = num5 + num6;
					int num8 = d[num7];
					if (num8 != 0)
					{
						if (num6 < w - 1)
						{
							num8 = Math.Min(num8, d[num7 + 1] + 8);
						}
						if (num4 < h - 1)
						{
							num8 = Math.Min(num8, d[num7 + w] + 8);
							if (num6 < w - 1)
							{
								num8 = Math.Min(num8, d[num7 + w + 1] + 11);
							}
							if (num6 > 0)
							{
								num8 = Math.Min(num8, d[num7 + w - 1] + 11);
							}
						}
						d[num7] = (byte)num8;
					}
				}
			}
		}

		public void ApplySeaReach(byte[] d)
		{
			int width = Width;
			int height = Height;
			int num = width * height;
			if (num <= 0 || d.Length != num)
			{
				return;
			}
			if (SeaReach.Length != num)
			{
				SeaReach = (byte[])d.Clone();
				ReopenSea();
				MarkChanged(0, 0, width, height);
				return;
			}
			int num2 = width;
			int num3 = height;
			int num4 = 0;
			int y = 0;
			byte[] seaReach = SeaReach;
			for (int i = 0; i < height; i++)
			{
				int num5 = i * width;
				for (int j = 0; j < width; j++)
				{
					byte b = d[num5 + j];
					if (seaReach[num5 + j] != b)
					{
						seaReach[num5 + j] = b;
						if (num5 + j < SeaOpen.Length)
						{
							SeaOpen[num5 + j] = OpenByte(num5 + j);
						}
						if (j < num2)
						{
							num2 = j;
						}
						if (j >= num4)
						{
							num4 = j + 1;
						}
						if (i < num3)
						{
							num3 = i;
						}
						y = i + 1;
					}
				}
			}
			if (num4 > num2)
			{
				MarkChanged(num2, num3, num4, y);
			}
		}

		private void MarkChanged(int x0, int y0, int x1, int y1)
		{
			lock (_changedLock)
			{
				if (_changedX1 <= _changedX0)
				{
					_changedX0 = x0;
					_changedY0 = y0;
					_changedX1 = x1;
					_changedY1 = y1;
				}
				else
				{
					_changedX0 = Math.Min(_changedX0, x0);
					_changedY0 = Math.Min(_changedY0, y0);
					_changedX1 = Math.Max(_changedX1, x1);
					_changedY1 = Math.Max(_changedY1, y1);
				}
			}
		}

		public bool TakeChanged(out int x0, out int y0, out int x1, out int y1)
		{
			lock (_changedLock)
			{
				x0 = _changedX0;
				y0 = _changedY0;
				x1 = _changedX1;
				y1 = _changedY1;
				_changedX0 = (_changedX1 = 0);
				return x1 > x0 && y1 > y0;
			}
		}

		public int PathWear(float x, float y)
		{
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height || PathSlot.Length == 0)
			{
				return 0;
			}
			int num = PathSlot[(int)y * Width + (int)x];
			if (num == 0)
			{
				return 0;
			}
			return PathTexels[(num - 1) * 64 + ((int)(y * 8f) & 7) * 8 + ((int)(x * 8f) & 7)];
		}

		public byte SurfaceAt(float x, float y)
		{
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height)
			{
				return 0;
			}
			if (PathWear(x, y) > 120)
			{
				return 9;
			}
			if (Snow.On && (Flags[(int)y * Width + (int)x] & 3) == 0 && Snow.At(x, y) > 0.5f)
			{
				return 5;
			}
			return Materials[(int)y * Width + (int)x];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Rgb(int r, int g, int b)
		{
			return (uint)(r | (g << 8) | (b << 16) | -16777216);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float GroundAtClamped(int x, int y)
		{
			if (x < 0)
			{
				x = 0;
			}
			else if (x >= Width)
			{
				x = Width - 1;
			}
			if (y < 0)
			{
				y = 0;
			}
			else if (y >= Height)
			{
				y = Height - 1;
			}
			return Ground[y * Width + x];
		}

		public float SampleHeight(float x, float y)
		{
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height)
			{
				return OutsideHeight;
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? num3 : 0);
			int num8 = ((num3 + 1 >= Width) ? (Width - 1) : (num3 + 1));
			int num9 = ((num4 >= 0) ? num4 : 0);
			int num10 = ((num4 + 1 >= Height) ? (Height - 1) : (num4 + 1));
			int num11 = num9 * Width + num7;
			int num12 = num9 * Width + num8;
			int num13 = num10 * Width + num7;
			int num14 = num10 * Width + num8;
			float num15 = Ground[num11];
			float num16 = Ground[num12];
			float num17 = Ground[num13];
			float num18 = Ground[num14];
			float num19 = num15 + (num16 - num15) * num5;
			float num20 = num17 + (num18 - num17) * num5;
			float num21 = num19 + (num20 - num19) * num6;
			byte b = Flags[num11];
			byte b2 = Flags[num12];
			byte b3 = Flags[num13];
			byte b4 = Flags[num14];
			if (((b | b2 | b3 | b4) & 0x40) != 0)
			{
				num21 = HollowHeight(Ground, Width, Height, num3, num4, num5, num6, num21, b, b2, b3, b4);
			}
			int num22 = (int)x;
			int num23 = (int)y;
			if ((Flags[num23 * Width + num22] & 8) != 0)
			{
				num21 = Math.Max(num21, Heights[num23 * Width + num22]);
			}
			else if (IsCoastCell(b, b2, b3, b4))
			{
				num21 = ShoreHeight(num21, b, b2, b3, b4, num15, num16, num17, num18, num5, num6, x, y, out var _);
			}
			return num21;
		}

		public float LiquidSurface(float x, float y)
		{
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height || Flags.Length == 0)
			{
				return 0f;
			}
			int num = (int)y * Width + (int)x;
			if (num < Marks.Length && (Marks[num] & 4) != 0 && (Flags[num] & 1) != 0)
			{
				return Ground[num];
			}
			if ((Flags[num] & 2) == 0)
			{
				return 0f;
			}
			return Math.Max(0f, SampleHeight(x, y));
		}

		public static bool NearHole(byte[] holes, int w, int h, int x, int y)
		{
			int num = Math.Max(0, x - 2);
			int num2 = Math.Min(w - 1, x + 2);
			int num3 = Math.Max(0, y - 2);
			int num4 = Math.Min(h - 1, y + 2);
			for (int i = num3; i <= num4; i++)
			{
				int num5 = i * w;
				bool flag = i >= y - 1 && i <= y + 1;
				for (int j = num; j <= num2; j++)
				{
					byte b = holes[num5 + j];
					if (b != 0 && ((b & 5) != 0 || (flag && j >= x - 1 && j <= x + 1)))
					{
						return true;
					}
				}
			}
			return false;
		}

		public static float HollowHeight(float[] ground, int w, int h, int x0, int y0, float tx, float ty, float bilinear, byte f00, byte f10, byte f01, byte f11)
		{
			float num = (((f00 & 0x40) != 0) ? 1f : 0f);
			float num2 = (((f10 & 0x40) != 0) ? 1f : 0f);
			float num3 = (((f01 & 0x40) != 0) ? 1f : 0f);
			float num4 = (((f11 & 0x40) != 0) ? 1f : 0f);
			float num5 = num + (num2 - num) * tx;
			float num6 = num3 + (num4 - num3) * tx;
			float num7 = num5 + (num6 - num5) * ty;
			if (num7 <= 0f)
			{
				return bilinear;
			}
			float num8 = tx * tx;
			float num9 = num8 * tx;
			float num10 = 1f - tx;
			float num11 = num10 * num10 * num10 * (1f / 6f);
			float num12 = (3f * num9 - 6f * num8 + 4f) * (1f / 6f);
			float num13 = (-3f * num9 + 3f * num8 + 3f * tx + 1f) * (1f / 6f);
			float num14 = num9 * (1f / 6f);
			float num15 = ty * ty;
			float num16 = num15 * ty;
			float num17 = 1f - ty;
			float num18 = num17 * num17 * num17 * (1f / 6f);
			float num19 = (3f * num16 - 6f * num15 + 4f) * (1f / 6f);
			float num20 = (-3f * num16 + 3f * num15 + 3f * ty + 1f) * (1f / 6f);
			float num21 = num16 * (1f / 6f);
			int num22 = Clamp(x0 - 1, w);
			int num23 = Clamp(x0, w);
			int num24 = Clamp(x0 + 1, w);
			int num25 = Clamp(x0 + 2, w);
			int num26 = Clamp(y0 - 1, h) * w;
			int num27 = Clamp(y0, h) * w;
			int num28 = Clamp(y0 + 1, h) * w;
			int num29 = Clamp(y0 + 2, h) * w;
			float num30 = num18 * (num11 * ground[num26 + num22] + num12 * ground[num26 + num23] + num13 * ground[num26 + num24] + num14 * ground[num26 + num25]) + num19 * (num11 * ground[num27 + num22] + num12 * ground[num27 + num23] + num13 * ground[num27 + num24] + num14 * ground[num27 + num25]) + num20 * (num11 * ground[num28 + num22] + num12 * ground[num28 + num23] + num13 * ground[num28 + num24] + num14 * ground[num28 + num25]) + num21 * (num11 * ground[num29 + num22] + num12 * ground[num29 + num23] + num13 * ground[num29 + num24] + num14 * ground[num29 + num25]);
			return bilinear + (num30 - bilinear) * num7;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int Clamp(int v, int n)
		{
			if (v >= 0)
			{
				if (v < n)
				{
					return v;
				}
				return n - 1;
			}
			return 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsCoastCell(byte f00, byte f10, byte f01, byte f11)
		{
			int num = f00 | (f00 >> 1);
			if ((((num ^ (f10 | (f10 >> 1))) | (num ^ (f01 | (f01 >> 1))) | (num ^ (f11 | (f11 >> 1)))) & 1) != 0)
			{
				return ((f00 | f10 | f01 | f11) & 8) == 0;
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ShoreNoise(float x, float y)
		{
			float num = (CloudNoise.Sample(x * 20f + 37f, y * 20f + 91f) - 0.5f) * 2.4f;
			return ((num < -1f) ? (-1f) : ((num > 1f) ? 1f : num)) * 0.18f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ShoreSide(float waterShare, float x, float y)
		{
			return waterShare - 0.5f + ShoreNoise(x, y);
		}

		public static float ShoreHeight(float h, byte f00, byte f10, byte f01, byte f11, float h00, float h10, float h01, float h11, float tx, float ty, float x, float y, out float side)
		{
			float num = (((f00 & 3) != 0) ? ((1f - tx) * (1f - ty)) : 0f);
			float num2 = (((f10 & 3) != 0) ? (tx * (1f - ty)) : 0f);
			float num3 = (((f01 & 3) != 0) ? ((1f - tx) * ty) : 0f);
			float num4 = (((f11 & 3) != 0) ? (tx * ty) : 0f);
			float num5 = num + num2 + num3 + num4;
			float num6 = ((num5 > 1E-06f) ? ((num * h00 + num2 * h10 + num3 * h01 + num4 * h11) / num5) : 0f);
			side = ShoreSide(num5, x, y);
			if (side >= 0f)
			{
				return num6;
			}
			float num7 = (0f - side) * 3.125f;
			if (num7 >= 1f)
			{
				return h;
			}
			num7 = num7 * num7 * (3f - 2f * num7);
			return num6 + (h - num6) * num7;
		}

		public static int ShoreTile(bool water, byte f00, byte f10, byte f01, byte f11, int i00, int i10, int i01, int i11, float tx, float ty)
		{
			int result = -1;
			float num = -1f;
			if ((f00 & 3) != 0 == water && (1f - tx) * (1f - ty) > num)
			{
				result = i00;
				num = (1f - tx) * (1f - ty);
			}
			if ((f10 & 3) != 0 == water && tx * (1f - ty) > num)
			{
				result = i10;
				num = tx * (1f - ty);
			}
			if ((f01 & 3) != 0 == water && (1f - tx) * ty > num)
			{
				result = i01;
				num = (1f - tx) * ty;
			}
			if ((f11 & 3) != 0 == water && tx * ty > num)
			{
				result = i11;
			}
			return result;
		}

		public void UpdateBlocks(int x0, int y0, int x1, int y1)
		{
			if (BlockMax.Length == 0)
			{
				return;
			}
			int num = Math.Max(0, x0 >> 3);
			int num2 = Math.Max(0, y0 >> 3);
			int num3 = Math.Min(BlocksX - 1, Math.Max(x0, x1 - 1) >> 3);
			int num4 = Math.Min(BlocksY - 1, Math.Max(y0, y1 - 1) >> 3);
			for (int i = num2; i <= num4; i++)
			{
				for (int j = num; j <= num3; j++)
				{
					float num5 = float.MinValue;
					int num6 = j << 3;
					int num7 = i << 3;
					for (int k = Math.Max(0, num7 - 1); k < Math.Min(Height, num7 + 8 + 1); k++)
					{
						for (int l = Math.Max(0, num6 - 1); l < Math.Min(Width, num6 + 8 + 1); l++)
						{
							float num8 = Heights[k * Width + l];
							if (num8 > num5)
							{
								num5 = num8;
							}
						}
					}
					BlockMax[i * BlocksX + j] = num5 + 0.45f + 0.15f;
				}
			}
		}

		public void Relight(int x0, int y0, int x1, int y1)
		{
			Vec3 lightDir = LightDir;
			float num = (float)Math.Sqrt(lightDir.X * lightDir.X + lightDir.Y * lightDir.Y);
			float sdx = ((num > 0.0001f) ? (lightDir.X / num) : 0f);
			float sdy = ((num > 0.0001f) ? (lightDir.Y / num) : 0f);
			float rise = ((num > 0.0001f) ? (lightDir.Z / num) : 1000f);
			x0 = Math.Max(0, x0);
			x1 = Math.Min(Width, x1);
			y0 = Math.Max(0, y0);
			y1 = Math.Min(Height, y1);
			if (x0 >= x1 || y0 >= y1)
			{
				return;
			}
			MarkChanged(x0, y0, x1, y1);
			bool soft = SoftLandShadows && SunField != null;
			WorkerPool workerPool = (((x1 - x0) * (y1 - y0) >= 2048) ? LightPool() : null);
			if (workerPool != null && workerPool.TryBegin())
			{
				RelightJob relightJob = _relightJob;
				try
				{
					relightJob.Cache = this;
					relightJob.X0 = x0;
					relightJob.X1 = x1;
					relightJob.Y0 = y0;
					relightJob.Y1 = y1;
					relightJob.L = lightDir;
					relightJob.Sdx = sdx;
					relightJob.Sdy = sdy;
					relightJob.Rise = rise;
					relightJob.Soft = soft;
					workerPool.Start(relightJob, (y1 - y0 + 4 - 1) / 4);
					workerPool.Finish();
					return;
				}
				finally
				{
					relightJob.Cache = null;
					workerPool.End();
				}
			}
			for (int i = y0; i < y1; i++)
			{
				RelightRow(i, x0, x1, lightDir, sdx, sdy, rise, soft);
			}
		}

		private static WorkerPool LightPool()
		{
			if (_lightPoolTried)
			{
				return _lightPool;
			}
			lock (LightPoolGate)
			{
				if (!_lightPoolTried)
				{
					try
					{
						_lightPool = new WorkerPool(Math.Max(1, Math.Min(3, Environment.ProcessorCount / 2 - 1)), "relight");
					}
					catch
					{
						_lightPool = null;
					}
					_lightPoolTried = true;
				}
			}
			return _lightPool;
		}

		private void RelightRow(int y, int x0, int x1, Vec3 l, float sdx, float sdy, float rise, bool soft)
		{
			int num = y * Width;
			for (int i = x0; i < x1; i++)
			{
				int num2 = num + i;
				byte b = (byte)(Flags[num2] & -33);
				uint num3 = BaseColors[num2];
				if ((b & 3) != 0)
				{
					Colors[num2] = (((b & 1) != 0) ? SeaColour(num2, num3) : num3);
					if (num2 < SeaOpen.Length)
					{
						SeaOpen[num2] = OpenByte(num2);
					}
					Sun[num2] = (byte)(255f * Math.Max(0f, l.Z));
					Open[num2] = byte.MaxValue;
					Flags[num2] = b;
					if (Marks.Length == Flags.Length)
					{
						Marks[num2] = (byte)(Marks[num2] & -3);
					}
					continue;
				}
				float num4 = Ground[num2];
				float num5 = GroundAtClamped(i + 1, y);
				float num6 = GroundAtClamped(i - 1, y);
				float num7 = GroundAtClamped(i, y + 1);
				float num8 = GroundAtClamped(i, y - 1);
				float num9 = (num5 - num6) * 0.5f;
				float num10 = (num7 - num8) * 0.5f;
				if (Marks.Length == Flags.Length)
				{
					float num11 = Math.Max(Math.Max(Math.Abs(num5 - num4), Math.Abs(num6 - num4)), Math.Max(Math.Abs(num7 - num4), Math.Abs(num8 - num4)));
					byte b2 = Marks[num2];
					Marks[num2] = (byte)((num11 > 1.4f && (b & 8) == 0) ? (b2 | 2) : (b2 & -3));
				}
				if ((b & 8) != 0)
				{
					num9 = (num10 = 0f);
				}
				float num12 = num9 * num9 + num10 * num10;
				float num13 = 1f / (float)Math.Sqrt(num12 + 1f);
				float num14 = ((0f - num9) * l.X - num10 * l.Y + l.Z) * num13;
				if (num14 < 0f)
				{
					num14 = 0f;
				}
				if (soft)
				{
					if (CastShadows && SunShadowField.Visibility(this, (float)i + 0.5f, (float)y + 0.5f, Heights[num2], withCanopy: false) < 0.5f)
					{
						b |= 0x20;
					}
				}
				else if (CastShadows && num14 > 0f && InTerrainShadow(i, y, Heights[num2], sdx, sdy, rise))
				{
					num14 = 0f;
					b |= 0x20;
				}
				Sun[num2] = (byte)(Math.Min(1f, num14) * 255f);
				float num15 = (GroundAtClamped(i - 2, y) + GroundAtClamped(i + 2, y) + GroundAtClamped(i, y - 2) + GroundAtClamped(i, y + 2)) * 0.25f;
				float val = 1f - Math.Max(0f, num15 - num4) * 0.2f;
				Open[num2] = (byte)(Math.Max(0.5f, Math.Min(1f, val)) * 255f);
				if (Seasonal && SeasonLook.Stamp != 0)
				{
					num3 = SeasonLook.Ground(num3, (SeasonMaterials.Length == Materials.Length) ? SeasonMaterials[num2] : Materials[num2], TerrainModel.Hash(i, y));
				}
				if (num12 > 1.2f && (b & 8) == 0)
				{
					float num16 = Math.Min(0.85f, ((float)Math.Sqrt(num12) - 1.1f) * 0.45f);
					if (num16 > 0f)
					{
						num3 = Lerp(num3, SlopeRock, num16);
						if (num16 > 0.45f)
						{
							Materials[num2] = 4;
						}
					}
				}
				Colors[num2] = num3;
				Flags[num2] = b;
			}
		}

		public float LightAt(float x, float y, float z)
		{
			if (!CastShadows || Width == 0)
			{
				return 1f;
			}
			if (SoftLandShadows && SunField != null)
			{
				return SunShadowField.Visibility(this, x, y, z, withCanopy: false);
			}
			Vec3 lightDir = LightDir;
			float num = (float)Math.Sqrt(lightDir.X * lightDir.X + lightDir.Y * lightDir.Y);
			if (num < 0.0001f || lightDir.Z <= 0f)
			{
				if (!(lightDir.Z > 0f))
				{
					return 0f;
				}
				return 1f;
			}
			float num2 = lightDir.X / num;
			float num3 = lightDir.Y / num;
			float num4 = lightDir.Z / num;
			for (int i = 1; i <= 64; i++)
			{
				float num5 = z + num4 * (float)i + 0.15f;
				if (num5 > MaxHeight)
				{
					return 1f;
				}
				int num6 = (int)(x + num2 * (float)i);
				int num7 = (int)(y + num3 * (float)i);
				if (num6 < 0 || num7 < 0 || num6 >= Width || num7 >= Height)
				{
					return 1f;
				}
				if (Heights[num7 * Width + num6] > num5)
				{
					return 0f;
				}
			}
			return 1f;
		}

		public float GroundLight(float x, float y)
		{
			if (Width == 0)
			{
				return 1f;
			}
			if (SoftLandShadows && SunField != null)
			{
				if (!CastShadows)
				{
					return 1f;
				}
				return SunShadowField.Visibility(this, x, y, SampleHeight(x, y), withCanopy: false);
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			float num7 = Lit(num3, num4);
			float num8 = Lit(num3 + 1, num4);
			float num9 = Lit(num3, num4 + 1);
			float num10 = Lit(num3 + 1, num4 + 1);
			float num11 = num7 + (num8 - num7) * num5;
			float num12 = num9 + (num10 - num9) * num5;
			return num11 + (num12 - num11) * num6;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float Lit(int x, int y)
		{
			x = ((x >= 0) ? ((x >= Width) ? (Width - 1) : x) : 0);
			y = ((y >= 0) ? ((y >= Height) ? (Height - 1) : y) : 0);
			if ((Flags[y * Width + x] & 0x20) == 0)
			{
				return 1f;
			}
			return 0f;
		}

		private bool InTerrainShadow(int x, int y, float h, float sdx, float sdy, float rise)
		{
			float num = (float)x + 0.5f;
			float num2 = (float)y + 0.5f;
			float[] heights = Heights;
			float[] blockMax = BlockMax;
			int width = Width;
			int height = Height;
			int blocksX = BlocksX;
			float maxHeight = MaxHeight;
			bool flag = rise >= 0f && blockMax.Length == BlocksX * BlocksY && blockMax.Length != 0;
			float num3 = Math.Abs(sdx);
			float num4 = Math.Abs(sdy);
			for (int i = 1; i <= 64; i++)
			{
				float num5 = h + rise * (float)i + 0.15f;
				if (num5 > maxHeight)
				{
					return false;
				}
				float num6 = num + sdx * (float)i;
				float num7 = num2 + sdy * (float)i;
				int num8 = (int)num6;
				int num9 = (int)num7;
				if (num8 < 0 || num9 < 0 || num8 >= width || num9 >= height)
				{
					return false;
				}
				if (heights[num9 * width + num8] > num5)
				{
					return true;
				}
				if (flag && num5 >= blockMax[(num9 >> 3) * blocksX + (num8 >> 3)])
				{
					float val = ((num3 < 1E-06f) ? 1E+09f : (((sdx > 0f) ? ((float)((num8 | 7) + 1) - num6) : (num6 - (float)(num8 & -8))) / num3));
					float val2 = ((num4 < 1E-06f) ? 1E+09f : (((sdy > 0f) ? ((float)((num9 | 7) + 1) - num7) : (num7 - (float)(num9 & -8))) / num4));
					int num10 = (int)Math.Min(val, val2) - 1;
					if (num10 > 0)
					{
						i += num10;
					}
				}
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Scale(uint c, float s)
		{
			int num = (int)((float)(c & 0xFF) * s);
			int num2 = (int)((float)((c >> 8) & 0xFF) * s);
			int num3 = (int)((float)((c >> 16) & 0xFF) * s);
			if (num > 255)
			{
				num = 255;
			}
			if (num2 > 255)
			{
				num2 = 255;
			}
			if (num3 > 255)
			{
				num3 = 255;
			}
			return (uint)(num | (num2 << 8) | (num3 << 16) | -16777216);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Tint(uint c, int r256, int g256, int b256)
		{
			int num = (int)(c & 0xFF) * r256 >> 8;
			int num2 = (int)((c >> 8) & 0xFF) * g256 >> 8;
			int num3 = (int)((c >> 16) & 0xFF) * b256 >> 8;
			if (num > 255)
			{
				num = 255;
			}
			if (num2 > 255)
			{
				num2 = 255;
			}
			if (num3 > 255)
			{
				num3 = 255;
			}
			return (uint)(num | (num2 << 8) | (num3 << 16) | -16777216);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Multiply(uint a, uint b)
		{
			uint num = (a & 0xFF) * (b & 0xFF) / 255;
			uint num2 = ((a >> 8) & 0xFF) * ((b >> 8) & 0xFF) / 255;
			uint num3 = ((a >> 16) & 0xFF) * ((b >> 16) & 0xFF) / 255;
			return num | (num2 << 8) | (num3 << 16) | 0xFF000000u;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Lerp(uint a, uint b, float t)
		{
			if (t <= 0f)
			{
				return a;
			}
			if (t >= 1f)
			{
				return b;
			}
			int num = (int)(t * 256f);
			int num2 = 256 - num;
			uint num3 = (uint)((int)(a & 0xFF) * num2 + (int)(b & 0xFF) * num) >> 8;
			uint num4 = (uint)((int)((a >> 8) & 0xFF) * num2 + (int)((b >> 8) & 0xFF) * num) >> 8;
			uint num5 = (uint)((int)((a >> 16) & 0xFF) * num2 + (int)((b >> 16) & 0xFF) * num) >> 8;
			return num3 | (num4 << 8) | (num5 << 16) | 0xFF000000u;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Add(uint c, int r, int g, int b)
		{
			r += (int)(c & 0xFF);
			g += (int)((c >> 8) & 0xFF);
			b += (int)((c >> 16) & 0xFF);
			if (r > 255)
			{
				r = 255;
			}
			if (g > 255)
			{
				g = 255;
			}
			if (b > 255)
			{
				b = 255;
			}
			if (r < 0)
			{
				r = 0;
			}
			if (g < 0)
			{
				g = 0;
			}
			if (b < 0)
			{
				b = 0;
			}
			return (uint)(r | (g << 8) | (b << 16) | -16777216);
		}

		private byte OpenByte(int i)
		{
			if (i >= Flags.Length || (Flags[i] & 1) == 0 || (Flags[i] & 0x80) != 0)
			{
				return 0;
			}
			float reachTiles = ((i < SeaReach.Length) ? ((float)(int)SeaReach[i] * 0.125f) : 0f);
			return (byte)(Sea.OpenShare(DepthOf(i), reachTiles) * 255f + 0.5f);
		}

		private void ReopenSea()
		{
			byte[] seaOpen = SeaOpen;
			if (seaOpen.Length == Flags.Length)
			{
				for (int i = 0; i < seaOpen.Length; i++)
				{
					seaOpen[i] = OpenByte(i);
				}
			}
		}

		public float OpenAt(float x, float y)
		{
			byte[] seaOpen = SeaOpen;
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height)
			{
				if (!OutsideIsWater)
				{
					return 0f;
				}
				return 1f;
			}
			if (seaOpen.Length != Width * Height)
			{
				return 0f;
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? num3 : 0);
			int num8 = ((num3 + 1 >= Width) ? (Width - 1) : (num3 + 1));
			int num9 = ((num4 >= 0) ? num4 : 0) * Width;
			int num10 = ((num4 + 1 >= Height) ? (Height - 1) : (num4 + 1)) * Width;
			float num11 = (float)(int)seaOpen[num9 + num7] + (float)(seaOpen[num9 + num8] - seaOpen[num9 + num7]) * num5;
			float num12 = (float)(int)seaOpen[num10 + num7] + (float)(seaOpen[num10 + num8] - seaOpen[num10 + num7]) * num5;
			return (num11 + (num12 - num11) * num6) * 0.003921569f;
		}

		private float ShoreSideAt(float x, float y)
		{
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height || Flags.Length == 0)
			{
				return 9f;
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? num3 : 0);
			int num8 = ((num3 + 1 >= Width) ? (Width - 1) : (num3 + 1));
			int num9 = ((num4 >= 0) ? num4 : 0) * Width;
			int num10 = ((num4 + 1 >= Height) ? (Height - 1) : (num4 + 1)) * Width;
			byte b = Flags[num9 + num7];
			byte b2 = Flags[num9 + num8];
			byte b3 = Flags[num10 + num7];
			byte b4 = Flags[num10 + num8];
			if (!IsCoastCell(b, b2, b3, b4))
			{
				return 9f;
			}
			float num11 = (((b & 3) != 0) ? ((1f - num5) * (1f - num6)) : 0f);
			float num12 = (((b2 & 3) != 0) ? (num5 * (1f - num6)) : 0f);
			float num13 = (((b3 & 3) != 0) ? ((1f - num5) * num6) : 0f);
			float num14 = (((b4 & 3) != 0) ? (num5 * num6) : 0f);
			return ShoreSide(num11 + num12 + num13 + num14, x, y);
		}

		public bool IsWaterAt(float x, float y)
		{
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height || Flags.Length == 0)
			{
				return OutsideIsWater;
			}
			return (Flags[(int)y * Width + (int)x] & 1) != 0;
		}

		public float WaveAt(float x, float y)
		{
			float num = Sea.FadeAt(x, y);
			if (x >= 0f && y >= 0f && x < (float)Width && y < (float)Height && Flags.Length != 0 && (Flags[(int)y * Width + (int)x] & 0x80) != 0)
			{
				return 0f;
			}
			if (!(num > 0f))
			{
				return 0f;
			}
			return Sea.Height(x, y, Sea.Now, OpenAt(x, y), Sea.ShoreFade(ShoreSideAt(x, y))) * num;
		}

		public float WaterTop(float x, float y)
		{
			if (!IsWaterAt(x, y))
			{
				return float.NaN;
			}
			return LiquidSurface(x, y) + WaveAt(x, y);
		}

		public float TopAt(float x, float y)
		{
			float num = WaterTop(x, y);
			if (!float.IsNaN(num))
			{
				return num;
			}
			return SampleHeight(x, y);
		}

		public float FloorHeight(float x, float y)
		{
			float[] floor = Floor;
			if (floor.Length != Width * Height || floor.Length == 0)
			{
				return SampleHeight(x, y);
			}
			if (x < 0f || y < 0f || x >= (float)Width || y >= (float)Height)
			{
				return OutsideHeight - ((HasSeabed && OutsideIsWater) ? 25.5f : 0f);
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? num3 : 0);
			int num8 = ((num3 + 1 >= Width) ? (Width - 1) : (num3 + 1));
			int num9 = ((num4 >= 0) ? num4 : 0);
			int num10 = ((num4 + 1 >= Height) ? (Height - 1) : (num4 + 1));
			float num11 = floor[num9 * Width + num7];
			float num12 = floor[num9 * Width + num8];
			float num13 = floor[num10 * Width + num7];
			float num14 = floor[num10 * Width + num8];
			float num15 = num11 + (num12 - num11) * num5;
			float num16 = num13 + (num14 - num13) * num5;
			return num15 + (num16 - num15) * num6;
		}

		public float DepthOf(int i)
		{
			if (i < 0 || i >= Floor.Length || i >= Ground.Length)
			{
				return 0f;
			}
			return Math.Max(0f, Ground[i] - Floor[i]);
		}

		private static uint[] BuildBakedSea()
		{
			uint[] array = new uint[256];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Seabed.Baked((float)i * 0.1f);
			}
			return array;
		}

		private uint SeaColour(int i, uint gameColour)
		{
			if (Floor.Length != Ground.Length || (Flags[i] & 0x80) != 0)
			{
				return gameColour;
			}
			float num = Ground[i] - Floor[i];
			if (num <= 0.001f)
			{
				return gameColour;
			}
			int num2 = (int)(num * 10f + 0.5f);
			return BakedSea[(num2 < 1) ? 1 : ((num2 > 255) ? 255 : num2)];
		}
	}
}
