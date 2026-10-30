using System;
using System.Diagnostics;

namespace FirstPerson.Core
{
	public sealed class SnowCover
	{
		public byte[] Depth = new byte[0];

		private ushort[] _fine = new ushort[0];

		private byte[] _rowMost = new byte[0];

		private float[] _rowAt = new float[0];

		private int _w;

		private int _h;

		private int _next;

		private int _rowsWithSnow;

		private float _clock;

		private bool _primed;

		private int _dirtyY0;

		private int _dirtyY1;

		public static bool Hidden;

		public float StepMs;

		public static float StormSeconds = 24f;

		public static float AgeSeconds = 30f;

		public static float MeltSeconds = 100f;

		public bool On
		{
			get
			{
				if (_rowsWithSnow > 0)
				{
					return !Hidden;
				}
				return false;
			}
		}

		public void Allocate(int w, int h)
		{
			_w = w;
			_h = h;
			int num = w * h;
			Depth = new byte[num];
			_fine = new ushort[num];
			_rowMost = new byte[h];
			_rowAt = new float[h];
			_next = 0;
			_rowsWithSnow = 0;
			_primed = false;
			_dirtyY0 = 0;
			_dirtyY1 = h;
		}

		public void Clear()
		{
			Array.Clear(Depth, 0, Depth.Length);
			Array.Clear(_fine, 0, _fine.Length);
			Array.Clear(_rowMost, 0, _rowMost.Length);
			_rowsWithSnow = 0;
			_primed = false;
			_dirtyY0 = 0;
			_dirtyY1 = _h;
		}

		public void Set(byte[] depths)
		{
			Array.Copy(depths, Depth, Math.Min(depths.Length, Depth.Length));
			_rowsWithSnow = 0;
			for (int i = 0; i < _h; i++)
			{
				int num = 0;
				int j = i * _w;
				for (int num2 = j + _w; j < num2; j++)
				{
					_fine[j] = (ushort)(Depth[j] << 8);
					if (Depth[j] > num)
					{
						num = Depth[j];
					}
				}
				_rowMost[i] = (byte)num;
				if (num > 0)
				{
					_rowsWithSnow++;
				}
			}
			_primed = true;
			_dirtyY0 = 0;
			_dirtyY1 = _h;
		}

		public bool TakeRows(out int y0, out int y1)
		{
			y0 = _dirtyY0;
			y1 = _dirtyY1;
			_dirtyY0 = _h;
			_dirtyY1 = 0;
			return y1 > y0;
		}

		public float At(float x, float y)
		{
			if (_rowsWithSnow == 0 || _w == 0)
			{
				return 0f;
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = (int)Math.Floor(num);
			int num4 = (int)Math.Floor(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = Clamp(num3, 0, _w - 1);
			int num8 = Clamp(num3 + 1, 0, _w - 1);
			int num9 = Clamp(num4, 0, _h - 1);
			int num10 = Clamp(num4 + 1, 0, _h - 1);
			byte[] depth = Depth;
			float num11 = (float)(int)depth[num9 * _w + num7] + (float)(depth[num9 * _w + num8] - depth[num9 * _w + num7]) * num5;
			float num12 = (float)(int)depth[num10 * _w + num7] + (float)(depth[num10 * _w + num8] - depth[num10 * _w + num7]) * num5;
			return (num11 + (num12 - num11) * num6) * 0.003921569f;
		}

		private static int Clamp(int v, int lo, int hi)
		{
			if (v >= lo)
			{
				if (v <= hi)
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		public void Step(TerrainCache land, Weather w, float dt, float time, float night, float winter)
		{
			if (land == null || land.Width != _w || land.Height != _h || _w == 0)
			{
				return;
			}
			if (dt < 0f)
			{
				dt = 0f;
			}
			long timestamp = Stopwatch.GetTimestamp();
			_clock += dt;
			bool flag = w != null && w.Global == Precip.Snow && w.GlobalStrength > 0.01f;
			float globalRate = (flag ? (w.GlobalStrength / AgeSeconds) : 0f);
			int num = 0;
			float num2 = float.MaxValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MinValue;
			if (w != null)
			{
				foreach (StormCloud storm in w.Storms)
				{
					if (storm.Drops == Precip.Snow && !(storm.Opacity < 0.05f))
					{
						num++;
						num2 = Math.Min(num2, storm.X - storm.HalfX * 1.3f);
						num4 = Math.Max(num4, storm.X + storm.HalfX * 1.3f);
						num3 = Math.Min(num3, storm.Y - storm.HalfY * 1.3f);
						num5 = Math.Max(num5, storm.Y + storm.HalfY * 1.3f);
					}
				}
			}
			float melt = (1f - 0.6f * night) * (1f - 0.9f * Clamp01(winter)) / MeltSeconds;
			int num6;
			if (!_primed)
			{
				num6 = _h;
			}
			else
			{
				float num7 = Math.Max(0.5f, (float)(_w * _h) / 250000f);
				num6 = Math.Max(1, (int)Math.Ceiling((float)_h * Math.Min(dt, 0.25f) / num7));
			}
			byte[] flags = land.Flags;
			byte[] materials = land.Materials;
			for (int i = 0; i < num6; i++)
			{
				int next = _next;
				_next = (_next + 1) % _h;
				float dt2 = (_primed ? Math.Min(2f, _clock - _rowAt[next]) : 0f);
				_rowAt[next] = _clock;
				bool flag2 = num > 0 && (float)next + 0.5f > num3 && (float)next + 0.5f < num5;
				if (_rowMost[next] != 0 || flag || flag2 || !_primed || RowHasField(materials, flags, next))
				{
					StepRow(next, dt2, w, time, flag2 ? num2 : float.MaxValue, flag2 ? num4 : float.MinValue, globalRate, melt, flags, materials);
				}
			}
			_primed = true;
			StepMs = (float)((double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency);
		}

		private bool RowHasField(byte[] mats, byte[] flags, int y)
		{
			int i = y * _w;
			for (int num = i + _w; i < num; i++)
			{
				if (mats[i] == 5 && (flags[i] & 7) == 0)
				{
					return true;
				}
			}
			return false;
		}

		private void StepRow(int y, float dt, Weather w, float time, float sx0, float sx1, float globalRate, float melt, byte[] flags, byte[] mats)
		{
			int num = y * _w;
			int num2 = 0;
			bool flag = false;
			float y2 = (float)y + 0.5f;
			for (int i = 0; i < _w; i++)
			{
				int num3 = num + i;
				int num4 = _fine[num3];
				int num5 = num4;
				if ((flags[num3] & 7) != 0)
				{
					num5 = 0;
				}
				else
				{
					float num6 = globalRate;
					float num7 = (float)i + 0.5f;
					if (num7 > sx0 && num7 < sx1)
					{
						Precip drops;
						float num8 = w.Cover(num7, y2, time, out drops);
						if (drops == Precip.Snow)
						{
							num6 += Math.Min(1f, num8 * 1.6f) / StormSeconds;
						}
					}
					bool flag2 = mats[num3] == 5;
					if (!_primed)
					{
						if (flag2)
						{
							num5 = 65535;
						}
					}
					else if (num6 > 0f)
					{
						num5 = num4 + (int)(num6 * dt * 65535f);
					}
					else if (flag2)
					{
						num5 = num4 + (int)(dt * 65535f / 4f);
					}
					else if (num4 > 0)
					{
						float num9 = ((mats[num3] == 2) ? 2.5f : ((mats[num3] == 4) ? 1.5f : 1f));
						num5 = num4 - (int)(melt * num9 * dt * 65535f) - 1;
					}
					if (num5 > 65535)
					{
						num5 = 65535;
					}
					if (num5 < 0)
					{
						num5 = 0;
					}
				}
				if (num5 != num4)
				{
					_fine[num3] = (ushort)num5;
					byte b = (byte)(num5 >> 8);
					if (b != Depth[num3])
					{
						Depth[num3] = b;
						flag = true;
					}
				}
				if (Depth[num3] > num2)
				{
					num2 = Depth[num3];
				}
			}
			if (_rowMost[y] == 0 != (num2 == 0))
			{
				_rowsWithSnow += ((num2 != 0) ? 1 : (-1));
			}
			_rowMost[y] = (byte)num2;
			if (flag)
			{
				if (y < _dirtyY0)
				{
					_dirtyY0 = y;
				}
				if (y + 1 > _dirtyY1)
				{
					_dirtyY1 = y + 1;
				}
			}
		}

		private static float Clamp01(float v)
		{
			if (!(v < 0f))
			{
				if (!(v > 1f))
				{
					return v;
				}
				return 1f;
			}
			return 0f;
		}
	}
}
