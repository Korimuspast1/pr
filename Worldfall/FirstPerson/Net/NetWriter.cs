using System;
using System.Text;

namespace FirstPerson.Net
{
	internal sealed class NetWriter
	{
		private byte[] _buf;

		private int _len;

		public byte[] Buffer => _buf;

		public int Length => _len;

		public NetWriter(int capacity = 256)
		{
			_buf = new byte[capacity];
		}

		public NetWriter Reset()
		{
			_len = 0;
			return this;
		}

		public byte[] ToArray()
		{
			byte[] array = new byte[_len];
			System.Buffer.BlockCopy(_buf, 0, array, 0, _len);
			return array;
		}

		private void Need(int n)
		{
			if (_len + n > _buf.Length)
			{
				int num;
				for (num = _buf.Length * 2; num < _len + n; num *= 2)
				{
				}
				Array.Resize(ref _buf, num);
			}
		}

		public void U8(int v)
		{
			Need(1);
			_buf[_len++] = (byte)v;
		}

		public void Bool(bool v)
		{
			U8(v ? 1 : 0);
		}

		public void U16(int v)
		{
			Need(2);
			_buf[_len++] = (byte)v;
			_buf[_len++] = (byte)(v >> 8);
		}

		public void I16(int v)
		{
			U16((ushort)(short)v);
		}

		public void I32(int v)
		{
			Need(4);
			_buf[_len++] = (byte)v;
			_buf[_len++] = (byte)(v >> 8);
			_buf[_len++] = (byte)(v >> 16);
			_buf[_len++] = (byte)(v >> 24);
		}

		public void U32(uint v)
		{
			I32((int)v);
		}

		public void I64(long v)
		{
			I32((int)v);
			I32((int)(v >> 32));
		}

		public unsafe void F32(float v)
		{
			I32(*(int*)(&v));
		}

		public unsafe void F64(double v)
		{
			I64(*(long*)(&v));
		}

		public void VarU(ulong v)
		{
			Need(10);
			while (v >= 128)
			{
				_buf[_len++] = (byte)(v | 0x80);
				v >>= 7;
			}
			_buf[_len++] = (byte)v;
		}

		public void VarI(long v)
		{
			VarU((ulong)((v << 1) ^ (v >> 63)));
		}

		public void Str(string s)
		{
			if (s == null)
			{
				VarU(0uL);
				return;
			}
			int byteCount = Encoding.UTF8.GetByteCount(s);
			VarU((ulong)byteCount + 1uL);
			Need(byteCount);
			Encoding.UTF8.GetBytes(s, 0, s.Length, _buf, _len);
			_len += byteCount;
		}

		public void Bytes(byte[] b, int off, int n)
		{
			VarU((ulong)n);
			Raw(b, off, n);
		}

		public void Raw(byte[] b, int off, int n)
		{
			Need(n);
			System.Buffer.BlockCopy(b, off, _buf, _len, n);
			_len += n;
		}

		public void Pos(float x)
		{
			I32((int)Math.Round(x * 256f));
		}

		public void Angle8(float deg)
		{
			U8((int)Math.Round((deg % 360f + 360f) % 360f / 360f * 256f) & 0xFF);
		}
	}
}
