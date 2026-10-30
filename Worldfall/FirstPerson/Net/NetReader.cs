using System;
using System.Text;

namespace FirstPerson.Net
{
	internal sealed class NetReader
	{
		private byte[] _buf;

		private int _pos;

		private int _end;

		public int Left => _end - _pos;

		public bool More => _pos < _end;

		public NetReader()
		{
		}

		public NetReader(byte[] b, int off, int len)
		{
			Set(b, off, len);
		}

		public NetReader Set(byte[] b, int off, int len)
		{
			_buf = b;
			_pos = off;
			_end = off + len;
			return this;
		}

		private void Need(int n)
		{
			if (n < 0 || n > _end - _pos)
			{
				throw new FormatException("message too short");
			}
		}

		public int U8()
		{
			Need(1);
			return _buf[_pos++];
		}

		public bool Bool()
		{
			return U8() != 0;
		}

		public int U16()
		{
			Need(2);
			int result = _buf[_pos] | (_buf[_pos + 1] << 8);
			_pos += 2;
			return result;
		}

		public int I16()
		{
			return (short)U16();
		}

		public int I32()
		{
			Need(4);
			int result = _buf[_pos] | (_buf[_pos + 1] << 8) | (_buf[_pos + 2] << 16) | (_buf[_pos + 3] << 24);
			_pos += 4;
			return result;
		}

		public uint U32()
		{
			return (uint)I32();
		}

		public long I64()
		{
			long num = (uint)I32();
			long num2 = I32();
			return num | (num2 << 32);
		}

		public unsafe float F32()
		{
			int num = I32();
			return *(float*)(&num);
		}

		public unsafe double F64()
		{
			long num = I64();
			return *(double*)(&num);
		}

		public ulong VarU()
		{
			ulong num = 0uL;
			for (int i = 0; i < 64; i += 7)
			{
				int num2 = U8();
				num |= (ulong)((long)(num2 & 0x7F) << i);
				if (num2 < 128)
				{
					return num;
				}
			}
			throw new FormatException("bad varint");
		}

		public long VarI()
		{
			ulong num = VarU();
			return (long)((num >> 1) ^ (0L - (num & 1)));
		}

		public int Count(int max = 1048576)
		{
			ulong num = VarU();
			if (num > (ulong)max)
			{
				throw new FormatException("count too large");
			}
			return (int)num;
		}

		public string Str()
		{
			ulong num = VarU();
			if (num == 0L)
			{
				return null;
			}
			if (num - 1 > (ulong)(_end - _pos))
			{
				throw new FormatException("string past the end");
			}
			int num2 = (int)(num - 1);
			string result = Encoding.UTF8.GetString(_buf, _pos, num2);
			_pos += num2;
			return result;
		}

		public byte[] Bytes(int max = 67108864)
		{
			int num = Count(max);
			Need(num);
			byte[] array = new byte[num];
			Buffer.BlockCopy(_buf, _pos, array, 0, num);
			_pos += num;
			return array;
		}

		public byte[] Rest()
		{
			byte[] array = new byte[Left];
			Buffer.BlockCopy(_buf, _pos, array, 0, array.Length);
			_pos = _end;
			return array;
		}

		public float Pos()
		{
			return (float)I32() / 256f;
		}

		public float Angle8()
		{
			return (float)U8() * 1.40625f;
		}
	}
}
