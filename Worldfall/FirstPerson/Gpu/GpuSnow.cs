using System;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuSnow : IDisposable
	{
		private ComputeBuffer _buf;

		private uint[] _words = new uint[0];

		private SnowCover _bound;

		private int _n;

		private bool _on;

		private Texture2D _pattern;

		public void Sync(TerrainCache land)
		{
			SnowCover snow = land.Snow;
			int num = land.Width * land.Height;
			_on = snow.On && num > 0 && snow.Depth.Length == num;
			if (_on && _pattern == null)
			{
				byte[] array = FrameRenderer.SnowTableIfReady();
				if (array != null)
				{
					_pattern = new Texture2D(512, 512, TextureFormat.R8, mipChain: false, linear: true)
					{
						filterMode = FilterMode.Point,
						wrapMode = TextureWrapMode.Repeat,
						name = "FirstPersonSnowPattern"
					};
					_pattern.LoadRawTextureData(array);
					_pattern.Apply(updateMipmaps: false, makeNoLongerReadable: true);
				}
			}
			if (num <= 0 || snow.Depth.Length != num)
			{
				return;
			}
			int num2 = Math.Max(4, num + 3 >> 2);
			int num3;
			if (_buf != null && _bound == snow)
			{
				num3 = ((_n != num) ? 1 : 0);
				if (num3 == 0)
				{
					goto IL_011b;
				}
			}
			else
			{
				num3 = 1;
			}
			_buf?.Release();
			_buf = new ComputeBuffer(num2, 4, ComputeBufferType.Structured);
			_words = new uint[num2];
			_bound = snow;
			_n = num;
			goto IL_011b;
			IL_011b:
			int y;
			int y2;
			bool flag = snow.TakeRows(out y, out y2);
			if (num3 != 0)
			{
				y = 0;
				y2 = land.Height;
			}
			else if (!flag)
			{
				return;
			}
			int width = land.Width;
			int num4 = y * width;
			int num5 = Math.Min(num, y2 * width);
			int num6 = num4 >> 2;
			int num7 = num5 + 3 >> 2;
			Buffer.BlockCopy(snow.Depth, num6 * 4, _words, num6 * 4, Math.Min(num, num7 * 4) - num6 * 4);
			_buf.SetData(_words, num6, num6, num7 - num6);
		}

		public void Bind(CommandBuffer cmd)
		{
			if (_buf == null)
			{
				_buf = new ComputeBuffer(4, 4, ComputeBufferType.Structured);
				_buf.SetData(new uint[4]);
				_bound = null;
				_n = -1;
			}
			cmd.SetGlobalBuffer("_FP_Snow", _buf);
			cmd.SetGlobalTexture("_FP_SnowPattern", (_pattern != null) ? _pattern : Texture2D.blackTexture);
			cmd.SetGlobalVector("_FP_SnowOn", new Vector4((_on && _buf != null) ? 1f : 0f, (_pattern != null) ? 1f : 0f, 0f, 0f));
		}

		public void Dispose()
		{
			_buf?.Release();
			_buf = null;
			_bound = null;
			if (_pattern != null)
			{
				UnityEngine.Object.Destroy(_pattern);
			}
			_pattern = null;
		}
	}
}
