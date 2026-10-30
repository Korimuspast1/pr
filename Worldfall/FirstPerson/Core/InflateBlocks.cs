using System;
using System.IO;
using System.Text;

namespace FirstPerson.Core
{
	public sealed class InflateBlocks : Stream
	{
		public const string Magic = "FPZ1";

		private readonly Stream _from;

		private readonly BinaryReader _reader;

		private readonly long _length;

		private readonly int _blockLength;

		private readonly int _blocks;

		private byte[] _packed = new byte[0];

		private readonly byte[] _block;

		private int _next;

		private int _blockFill;

		private int _blockPos;

		private long _position;

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => _length;

		public override long Position
		{
			get
			{
				return _position;
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		public InflateBlocks(Stream from)
		{
			_from = from;
			_reader = new BinaryReader(from);
			if (Encoding.ASCII.GetString(_reader.ReadBytes(4)) != "FPZ1")
			{
				throw new InvalidDataException("not a block-deflated file");
			}
			_length = _reader.ReadInt32();
			_blockLength = _reader.ReadInt32();
			_blocks = _reader.ReadInt32();
			if (_length < 0 || _blockLength <= 0 || _blocks < 0 || (long)_blockLength * (long)_blocks < _length)
			{
				throw new InvalidDataException("block-deflated file: bad header");
			}
			_block = new byte[_blockLength];
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_blockPos >= _blockFill)
			{
				if (_next >= _blocks)
				{
					return 0;
				}
				int num = _reader.ReadInt32();
				if (num < 0)
				{
					throw new InvalidDataException("block-deflated file: bad block");
				}
				if (_packed.Length < num)
				{
					_packed = new byte[num];
				}
				int num2;
				for (int i = 0; i < num; i += num2)
				{
					num2 = _from.Read(_packed, i, num - i);
					if (num2 <= 0)
					{
						throw new EndOfStreamException("block-deflated file cut short");
					}
				}
				_blockFill = (int)Math.Min(_blockLength, _length - (long)_next * (long)_blockLength);
				Inflate.Raw(_packed, 0, num, _block, _blockFill);
				_blockPos = 0;
				_next++;
			}
			int num3 = Math.Min(count, _blockFill - _blockPos);
			Buffer.BlockCopy(_block, _blockPos, buffer, offset, num3);
			_blockPos += num3;
			_position += num3;
			return num3;
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_from.Dispose();
			}
			base.Dispose(disposing);
		}
	}
}
