using System;

namespace FirstPerson.Core
{
	public sealed class FloorGrid
	{
		private const float C = 0.05f;

		private readonly float _x0;

		private readonly float _y0;

		private readonly int _w;

		private readonly int _h;

		private readonly float[] _edge;

		public FloorGrid(FitInfo fit, int storey, float x0, float y0, float x1, float y1)
		{
			_x0 = x0 - 0.5f;
			_y0 = y0 - 0.5f;
			_w = (int)((x1 - x0 + 1f) / 0.05f) + 1;
			_h = (int)((y1 - y0 + 1f) / 0.05f) + 1;
			_edge = new float[_w * _h];
			for (int i = 0; i < _h; i++)
			{
				for (int j = 0; j < _w; j++)
				{
					_edge[i * _w + j] = (fit.Inside(storey, _x0 + ((float)j + 0.5f) * 0.05f, _y0 + ((float)i + 0.5f) * 0.05f) ? 1000000f : 0f);
				}
			}
			int w = _w;
			int h = _h;
			float gx0 = _x0;
			float gy0 = _y0;
			float[] edge = _edge;
			fit.ForEachWall(storey, delegate(float ax, float ay, float bx, float by)
			{
				float num9 = (float)Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
				int num10 = Math.Max(1, (int)Math.Ceiling(num9 / 0.025f));
				for (int k = 0; k <= num10; k++)
				{
					float num11 = (float)k / (float)num10;
					int num12 = (int)((ax + (bx - ax) * num11 - gx0) / 0.05f);
					int num13 = (int)((ay + (by - ay) * num11 - gy0) / 0.05f);
					if (num12 >= 0 && num13 >= 0 && num12 < w && num13 < h)
					{
						edge[num13 * w + num12] = 0f;
					}
				}
			});
			for (int num = 0; num < _h; num++)
			{
				for (int num2 = 0; num2 < _w; num2++)
				{
					int num3 = num * _w + num2;
					float num4 = _edge[num3];
					if (num4 == 0f)
					{
						continue;
					}
					if (num2 > 0)
					{
						num4 = Math.Min(num4, _edge[num3 - 1] + 0.05f);
					}
					if (num > 0)
					{
						num4 = Math.Min(num4, _edge[num3 - _w] + 0.05f);
						if (num2 > 0)
						{
							num4 = Math.Min(num4, _edge[num3 - _w - 1] + 0.070709996f);
						}
						if (num2 < _w - 1)
						{
							num4 = Math.Min(num4, _edge[num3 - _w + 1] + 0.070709996f);
						}
					}
					if (num2 == 0 || num == 0 || num2 == _w - 1 || num == _h - 1)
					{
						num4 = Math.Min(num4, 0.05f);
					}
					_edge[num3] = num4;
				}
			}
			for (int num5 = _h - 1; num5 >= 0; num5--)
			{
				for (int num6 = _w - 1; num6 >= 0; num6--)
				{
					int num7 = num5 * _w + num6;
					float num8 = _edge[num7];
					if (num8 != 0f)
					{
						if (num6 < _w - 1)
						{
							num8 = Math.Min(num8, _edge[num7 + 1] + 0.05f);
						}
						if (num5 < _h - 1)
						{
							num8 = Math.Min(num8, _edge[num7 + _w] + 0.05f);
							if (num6 < _w - 1)
							{
								num8 = Math.Min(num8, _edge[num7 + _w + 1] + 0.070709996f);
							}
							if (num6 > 0)
							{
								num8 = Math.Min(num8, _edge[num7 + _w - 1] + 0.070709996f);
							}
						}
						_edge[num7] = num8;
					}
				}
			}
		}

		private float At(float x, float y)
		{
			int num = (int)((x - _x0) / 0.05f);
			int num2 = (int)((y - _y0) / 0.05f);
			if (num >= 0 && num2 >= 0 && num < _w && num2 < _h)
			{
				return _edge[num2 * _w + num];
			}
			return 0f;
		}

		public bool Inside(float x, float y)
		{
			return At(x, y) > 0f;
		}

		public bool Clear(float x, float y, float r)
		{
			return At(x, y) >= r;
		}

		public float Edge(float x, float y)
		{
			return At(x, y);
		}
	}
}
