using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class Seabed
	{
		public const byte NotSea = 0;

		public const byte Shallow = 1;

		public const byte Close = 2;

		public const byte Deep = 3;

		public const byte BandBits = 3;

		public const byte Dry = 4;

		public const byte WasWater = 8;

		public const byte Own = 16;

		public const float Step = 0.1f;

		public const float MostDepth = 25.5f;

		private const float ShallowTop = 0.1f;

		private const float ShallowRise = 0.9f;

		private const float ShallowRun = 5f;

		private const float CloseTop = 1.5f;

		private const float CloseRise = 2.5f;

		private const float CloseRun = 8f;

		private const float DeepTop = 10f;

		private const float DeepRise = 15f;

		private const float DeepRun = 14f;

		public const float SigmaR = 0.6f;

		public const float SigmaG = 0.13f;

		public const float SigmaB = 0.065f;

		public const float BodyR = 0.04f;

		public const float BodyG = 0.24f;

		public const float BodyB = 0.42f;

		public const float SandR = 0.86f;

		public const float SandG = 0.82f;

		public const float SandB = 0.7f;

		public const float SiltR = 0.3f;

		public const float SiltG = 0.34f;

		public const float SiltB = 0.3f;

		public const float RockR = 0.42f;

		public const float RockG = 0.42f;

		public const float RockB = 0.4f;

		public const float BakedPath = 2.4f;

		public const int Bends = 8;

		private const float MostBend = 0.512f;

		public static readonly uint[] SeenFloor = BuildSeen(rock: false, out SeenShare);

		public static readonly uint[] SeenRock = BuildSeen(rock: true, out var _);

		public static readonly float[] SeenShare;

		public static uint OpenSea => Baked(25.5f);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte BandOf(int heightMin)
		{
			if (heightMin < 70)
			{
				if (heightMin < 30)
				{
					return 3;
				}
				return 2;
			}
			return 1;
		}

		public static byte[] Depths(byte[] bands, int w, int h)
		{
			int num = w * h;
			byte[] array = new byte[num];
			if (num <= 0 || bands.Length < num)
			{
				return array;
			}
			bool[] array2 = SeaMask(bands, w, h);
			ushort[] array3 = new ushort[num];
			ushort[] array4 = new ushort[num];
			ushort[] array5 = new ushort[num];
			for (int i = 0; i < num; i++)
			{
				int num2 = bands[i] & 3;
				array3[i] = (ushort)(array2[i] ? ushort.MaxValue : 0);
				array4[i] = (ushort)((array2[i] && num2 >= 2) ? ushort.MaxValue : 0);
				array5[i] = (ushort)((array2[i] && num2 == 3) ? ushort.MaxValue : 0);
			}
			Chamfer(array3, w, h);
			Chamfer(array4, w, h);
			Chamfer(array5, w, h);
			for (int j = 0; j < h; j++)
			{
				for (int k = 0; k < w; k++)
				{
					int num3 = j * w + k;
					if (array2[num3])
					{
						int num4 = (int)(DepthOf(bands[num3] & 3, array3[num3], array4[num3], array5[num3], k, j) / 0.1f + 0.5f);
						array[num3] = (byte)((num4 < 1) ? 1u : ((num4 > 255) ? 255u : ((uint)num4)));
					}
				}
			}
			return array;
		}

		public static float DepthOf(int band, int toLand, int toNotClose, int toNotDeep, int x, int y)
		{
			float num = CloudNoise.Sample((float)x * 1.6f + 41f, (float)y * 1.6f - 23f) - 0.5f;
			switch (band)
			{
			case 1:
			{
				float num4 = Math.Max(0f, (float)toLand * 0.125f - 0.5f);
				float num5 = 0.1f + 0.9f * Smooth(num4 / 5f) + num * 0.24f;
				if (!(num5 < 0.05f))
				{
					return num5;
				}
				return 0.05f;
			}
			case 2:
			{
				float num3 = Math.Max(0f, (float)toNotClose * 0.125f - 0.5f);
				return (1.5f + 2.5f * Smooth(num3 / 8f)) * (1f + num * 0.3f);
			}
			case 3:
			{
				float num2 = Math.Max(0f, (float)toNotDeep * 0.125f - 0.5f);
				return Math.Min(25.5f, (10f + 15f * (1f - (float)Math.Exp((0f - num2) / 14f))) * (1f + num * 0.25f));
			}
			default:
				return 0f;
			}
		}

		private static bool[] SeaMask(byte[] bands, int w, int h)
		{
			int num = w * h;
			bool[] array = new bool[num];
			Queue<int> queue = new Queue<int>();
			for (int i = 0; i < num; i++)
			{
				byte b = bands[i];
				if ((b & 3) != 0 && (b & 0x10) == 0 && ((b & 4) == 0 || (b & 8) != 0))
				{
					array[i] = true;
				}
			}
			for (int j = 0; j < h; j++)
			{
				for (int k = 0; k < w; k++)
				{
					int num2 = j * w + k;
					if (!array[num2] && IsBed(bands[num2]) && (k == 0 || j == 0 || k == w - 1 || j == h - 1 || Wet(bands, array, num2 - 1) || Wet(bands, array, num2 + 1) || Wet(bands, array, num2 - w) || Wet(bands, array, num2 + w)))
					{
						array[num2] = true;
						queue.Enqueue(num2);
					}
				}
			}
			while (queue.Count > 0)
			{
				int num3 = queue.Dequeue();
				int num4 = num3 % w;
				int num5 = num3 / w;
				if (num4 > 0)
				{
					Spread(bands, array, queue, num3 - 1);
				}
				if (num4 < w - 1)
				{
					Spread(bands, array, queue, num3 + 1);
				}
				if (num5 > 0)
				{
					Spread(bands, array, queue, num3 - w);
				}
				if (num5 < h - 1)
				{
					Spread(bands, array, queue, num3 + w);
				}
			}
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsBed(byte b)
		{
			if ((b & 3) != 0 && (b & 4) != 0)
			{
				return (b & 0x10) == 0;
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Wet(byte[] bands, bool[] sea, int i)
		{
			if (i >= 0 && i < bands.Length && sea[i])
			{
				return (bands[i] & 4) == 0;
			}
			return false;
		}

		private static void Spread(byte[] bands, bool[] sea, Queue<int> queue, int i)
		{
			if (!sea[i] && IsBed(bands[i]))
			{
				sea[i] = true;
				queue.Enqueue(i);
			}
		}

		private static void Chamfer(ushort[] d, int w, int h)
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
					d[num2] = (ushort)Math.Min(num3, 65535);
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
						d[num7] = (ushort)Math.Min(num8, 65535);
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float SandShare(float depth)
		{
			return 1f - Smooth((depth - 0.8f) * 0.45454544f);
		}

		public static uint Baked(float depth)
		{
			float num = depth * 2.4f;
			float num2 = SandShare(depth);
			float num3 = (float)Math.Exp(-0.6f * num);
			float num4 = (float)Math.Exp(-0.13f * num);
			float num5 = (float)Math.Exp(-0.065f * num);
			float num6 = 0.3f + 0.56f * num2;
			float num7 = 0.34f + 0.48f * num2;
			float num8 = 0.3f + 0.39999998f * num2;
			return Rgb01(num6 * num3 + 0.04f * (1f - num3), num7 * num4 + 0.24f * (1f - num4), num8 * num5 + 0.42f * (1f - num5));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int BendOf(float invCos)
		{
			int num = (int)((invCos - 1f) * 13.671874f + 0.5f);
			if (num >= 0)
			{
				if (num < 8)
				{
					return num;
				}
				return 7;
			}
			return 0;
		}

		private static uint[] BuildSeen(bool rock, out float[] share)
		{
			uint[] array = new uint[2048];
			share = new float[2048];
			for (int i = 0; i < 256; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					float num = (float)i * 0.1f;
					float num2 = num * (2f + (float)j * 0.512f / 7f);
					float num3 = SandShare(num);
					float num4 = (float)Math.Exp(-0.6f * num2);
					float num5 = (float)Math.Exp(-0.13f * num2);
					float num6 = (float)Math.Exp(-0.065f * num2);
					float num7 = (rock ? 0.42f : (0.3f + 0.56f * num3));
					float num8 = (rock ? 0.42f : (0.34f + 0.48f * num3));
					float num9 = (rock ? 0.4f : (0.3f + 0.39999998f * num3));
					array[i * 8 + j] = Rgb01(num7 * num4 + 0.04f * (1f - num4), num8 * num5 + 0.24f * (1f - num5), num9 * num6 + 0.42f * (1f - num6));
					share[i * 8 + j] = num5;
				}
			}
			return array;
		}

		public static float RangeFor(float detailDistance, bool light)
		{
			if (light || detailDistance < 65f)
			{
				return 8f;
			}
			if (detailDistance < 100f)
			{
				return 12f;
			}
			if (detailDistance < 160f)
			{
				return 20f;
			}
			return 30f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Smooth(float x)
		{
			x = ((x < 0f) ? 0f : ((x > 1f) ? 1f : x));
			return x * x * (3f - 2f * x);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Rgb01(float r, float g, float b)
		{
			int num = (int)(r * 255f + 0.5f);
			int num2 = (int)(g * 255f + 0.5f);
			int num3 = (int)(b * 255f + 0.5f);
			return TerrainCache.Rgb((num >= 0) ? ((num > 255) ? 255 : num) : 0, (num2 >= 0) ? ((num2 > 255) ? 255 : num2) : 0, (num3 >= 0) ? ((num3 > 255) ? 255 : num3) : 0);
		}
	}
}
