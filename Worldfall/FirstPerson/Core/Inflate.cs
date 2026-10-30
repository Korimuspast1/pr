using System;
using System.IO;

namespace FirstPerson.Core
{
	public static class Inflate
	{
		private sealed class Huffman
		{
			public readonly short[] Count = new short[16];

			public readonly short[] Symbol;

			public readonly int[] Fast = new int[1024];

			public Huffman(int symbols)
			{
				Symbol = new short[symbols];
			}

			public bool Build(byte[] lengths, int offset, int n)
			{
				Array.Clear(Count, 0, Count.Length);
				Array.Clear(Fast, 0, Fast.Length);
				for (int i = 0; i < n; i++)
				{
					Count[lengths[offset + i]]++;
				}
				if (Count[0] == n)
				{
					return true;
				}
				int num = 1;
				for (int j = 1; j < 16; j++)
				{
					num <<= 1;
					num -= Count[j];
					if (num < 0)
					{
						return false;
					}
				}
				short[] array = new short[16];
				for (int k = 1; k < 15; k++)
				{
					array[k + 1] = (short)(array[k] + Count[k]);
				}
				for (int l = 0; l < n; l++)
				{
					if (lengths[offset + l] != 0)
					{
						Symbol[array[lengths[offset + l]]++] = (short)l;
					}
				}
				int num2 = 0;
				int num3 = 0;
				for (int m = 1; m <= 10; m++)
				{
					int num4 = 0;
					while (num4 < Count[m])
					{
						int num5 = 0;
						for (int num6 = 0; num6 < m; num6++)
						{
							num5 |= ((num2 >> num6) & 1) << m - 1 - num6;
						}
						int num7 = (Symbol[num3] << 4) | m;
						for (int num8 = num5; num8 < Fast.Length; num8 += 1 << m)
						{
							Fast[num8] = num7;
						}
						num4++;
						num2++;
						num3++;
					}
					num2 <<= 1;
				}
				return true;
			}
		}

		private sealed class State
		{
			public byte[] In;

			public byte[] Out;

			public int Pos;

			public int End;

			public int OutPos;

			private int _buf;

			private int _cnt;

			public int Bits(int need)
			{
				int num = _buf;
				while (_cnt < need)
				{
					if (Pos >= End)
					{
						throw new InvalidDataException("deflate: ran out of input");
					}
					num |= In[Pos++] << _cnt;
					_cnt += 8;
				}
				_buf = num >> need;
				_cnt -= need;
				return num & ((1 << need) - 1);
			}

			public void Stored()
			{
				Pos -= _cnt >> 3;
				_buf = 0;
				_cnt = 0;
				if (Pos + 4 > End)
				{
					throw new InvalidDataException("deflate: stored block cut short");
				}
				int num = In[Pos] | (In[Pos + 1] << 8);
				int num2 = In[Pos + 2] | (In[Pos + 3] << 8);
				Pos += 4;
				if (num != (~num2 & 0xFFFF))
				{
					throw new InvalidDataException("deflate: stored block length");
				}
				if (Pos + num > End || OutPos + num > Out.Length)
				{
					throw new InvalidDataException("deflate: stored block overruns");
				}
				Buffer.BlockCopy(In, Pos, Out, OutPos, num);
				Pos += num;
				OutPos += num;
			}

			public int Decode(Huffman h)
			{
				while (_cnt < 10 && Pos < End)
				{
					_buf |= In[Pos++] << _cnt;
					_cnt += 8;
				}
				int num = h.Fast[_buf & 0x3FF];
				int num2 = num & 0xF;
				if (num != 0 && num2 <= _cnt)
				{
					_buf >>= num2;
					_cnt -= num2;
					return num >> 4;
				}
				return DecodeSlow(h);
			}

			private int DecodeSlow(Huffman h)
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				for (int i = 1; i < 16; i++)
				{
					num |= Bits(1);
					int num4 = h.Count[i];
					if (num - num4 < num2)
					{
						return h.Symbol[num3 + (num - num2)];
					}
					num3 += num4;
					num2 += num4;
					num2 <<= 1;
					num <<= 1;
				}
				throw new InvalidDataException("deflate: bad code");
			}

			public void Codes(Huffman lencode, Huffman distcode)
			{
				byte[] array = Out;
				while (true)
				{
					int num = Decode(lencode);
					if (num < 256)
					{
						if (OutPos >= array.Length)
						{
							throw new InvalidDataException("deflate: output overrun");
						}
						array[OutPos++] = (byte)num;
						continue;
					}
					if (num == 256)
					{
						return;
					}
					num -= 257;
					if (num >= 29)
					{
						throw new InvalidDataException("deflate: bad length symbol");
					}
					int num2 = LengthBase[num] + Bits(LengthExtra[num]);
					int num3 = Decode(distcode);
					if (num3 >= 30)
					{
						throw new InvalidDataException("deflate: bad distance symbol");
					}
					int num4 = DistBase[num3] + Bits(DistExtra[num3]);
					if (num4 > OutPos || OutPos + num2 > array.Length)
					{
						break;
					}
					int num5 = OutPos - num4;
					if (num4 >= num2)
					{
						Buffer.BlockCopy(array, num5, array, OutPos, num2);
						OutPos += num2;
						continue;
					}
					for (int i = 0; i < num2; i++)
					{
						array[OutPos++] = array[num5 + i];
					}
				}
				throw new InvalidDataException("deflate: bad distance");
			}
		}

		private static readonly short[] LengthBase = new short[29]
		{
			3, 4, 5, 6, 7, 8, 9, 10, 11, 13,
			15, 17, 19, 23, 27, 31, 35, 43, 51, 59,
			67, 83, 99, 115, 131, 163, 195, 227, 258
		};

		private static readonly short[] LengthExtra = new short[29]
		{
			0, 0, 0, 0, 0, 0, 0, 0, 1, 1,
			1, 1, 2, 2, 2, 2, 3, 3, 3, 3,
			4, 4, 4, 4, 5, 5, 5, 5, 0
		};

		private static readonly short[] DistBase = new short[30]
		{
			1, 2, 3, 4, 5, 7, 9, 13, 17, 25,
			33, 49, 65, 97, 129, 193, 257, 385, 513, 769,
			1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577
		};

		private static readonly short[] DistExtra = new short[30]
		{
			0, 0, 0, 0, 1, 1, 2, 2, 3, 3,
			4, 4, 5, 5, 6, 6, 7, 7, 8, 8,
			9, 9, 10, 10, 11, 11, 12, 12, 13, 13
		};

		private static readonly byte[] CodeOrder = new byte[19]
		{
			16, 17, 18, 0, 8, 7, 9, 6, 10, 5,
			11, 4, 12, 3, 13, 2, 14, 1, 15
		};

		private const int FastBits = 10;

		private const int FastMask = 1023;

		private static Huffman _fixedLen;

		private static Huffman _fixedDist;

		private static void Fixed(out Huffman len, out Huffman dist)
		{
			if (_fixedLen == null)
			{
				byte[] array = new byte[288];
				for (int i = 0; i < 144; i++)
				{
					array[i] = 8;
				}
				for (int j = 144; j < 256; j++)
				{
					array[j] = 9;
				}
				for (int k = 256; k < 280; k++)
				{
					array[k] = 7;
				}
				for (int l = 280; l < 288; l++)
				{
					array[l] = 8;
				}
				Huffman huffman = new Huffman(288);
				huffman.Build(array, 0, 288);
				Huffman huffman2 = new Huffman(30);
				byte[] array2 = new byte[30];
				for (int m = 0; m < 30; m++)
				{
					array2[m] = 5;
				}
				huffman2.Build(array2, 0, 30);
				_fixedDist = huffman2;
				_fixedLen = huffman;
			}
			len = _fixedLen;
			dist = _fixedDist;
		}

		private static void Dynamic(State s)
		{
			int num = s.Bits(5) + 257;
			int num2 = s.Bits(5) + 1;
			int num3 = s.Bits(4) + 4;
			if (num > 286 || num2 > 30)
			{
				throw new InvalidDataException("deflate: bad counts");
			}
			byte[] array = new byte[320];
			for (int i = 0; i < num3; i++)
			{
				array[CodeOrder[i]] = (byte)s.Bits(3);
			}
			Huffman huffman = new Huffman(286);
			if (!huffman.Build(array, 0, 19))
			{
				throw new InvalidDataException("deflate: bad code lengths");
			}
			int num4 = 0;
			Array.Clear(array, 0, array.Length);
			while (num4 < num + num2)
			{
				int num5 = s.Decode(huffman);
				if (num5 < 16)
				{
					array[num4++] = (byte)num5;
					continue;
				}
				int num6 = 0;
				int num7;
				switch (num5)
				{
				case 16:
					if (num4 == 0)
					{
						throw new InvalidDataException("deflate: repeat with no first length");
					}
					num6 = array[num4 - 1];
					num7 = 3 + s.Bits(2);
					break;
				case 17:
					num7 = 3 + s.Bits(3);
					break;
				default:
					num7 = 11 + s.Bits(7);
					break;
				}
				if (num4 + num7 > num + num2)
				{
					throw new InvalidDataException("deflate: too many lengths");
				}
				while (num7-- > 0)
				{
					array[num4++] = (byte)num6;
				}
			}
			if (array[256] == 0)
			{
				throw new InvalidDataException("deflate: no end-of-block code");
			}
			if (!huffman.Build(array, 0, num))
			{
				throw new InvalidDataException("deflate: bad literal/length lengths");
			}
			byte[] array2 = new byte[30];
			Buffer.BlockCopy(array, num, array2, 0, num2);
			Huffman huffman2 = new Huffman(30);
			if (!huffman2.Build(array2, 0, num2))
			{
				throw new InvalidDataException("deflate: bad distance lengths");
			}
			s.Codes(huffman, huffman2);
		}

		public static byte[] Raw(byte[] src, int offset, int length, int outLength)
		{
			return Raw(src, offset, length, new byte[outLength], outLength);
		}

		public static byte[] Raw(byte[] src, int offset, int length, byte[] into, int outLength)
		{
			if (into.Length < outLength)
			{
				throw new ArgumentException("deflate: output buffer too small");
			}
			State state = new State
			{
				In = src,
				Pos = offset,
				End = offset + length,
				Out = into
			};
			int num;
			do
			{
				num = state.Bits(1);
				switch (state.Bits(2))
				{
				case 0:
					state.Stored();
					break;
				case 1:
				{
					Fixed(out var len, out var dist);
					state.Codes(len, dist);
					break;
				}
				case 2:
					Dynamic(state);
					break;
				default:
					throw new InvalidDataException("deflate: bad block type");
				}
			}
			while (num == 0);
			if (state.OutPos != outLength)
			{
				throw new InvalidDataException($"deflate: {state.OutPos} bytes out, {outLength} expected");
			}
			return state.Out;
		}
	}
}
