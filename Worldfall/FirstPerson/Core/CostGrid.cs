using System;

namespace FirstPerson.Core
{
	public sealed class CostGrid
	{
		public int X0;

		public int Y0;

		public int W;

		public int H;

		public int MapWidth;

		public float[] Cost;

		public static CostGrid Take(TerrainCache t, int x0, int y0, int x1, int y1, Func<int, float> cost)
		{
			x0 = Math.Max(0, x0);
			y0 = Math.Max(0, y0);
			x1 = Math.Min(t.Width - 1, x1);
			y1 = Math.Min(t.Height - 1, y1);
			CostGrid costGrid = new CostGrid
			{
				X0 = x0,
				Y0 = y0,
				W = Math.Max(1, x1 - x0 + 1),
				H = Math.Max(1, y1 - y0 + 1),
				MapWidth = t.Width
			};
			costGrid.Cost = new float[costGrid.W * costGrid.H];
			for (int i = 0; i < costGrid.H; i++)
			{
				for (int j = 0; j < costGrid.W; j++)
				{
					costGrid.Cost[i * costGrid.W + j] = cost((i + y0) * t.Width + j + x0);
				}
			}
			return costGrid;
		}

		public float At(int tile)
		{
			int num = tile % MapWidth - X0;
			int num2 = tile / MapWidth - Y0;
			if (num < 0 || num2 < 0 || num >= W || num2 >= H)
			{
				return -1f;
			}
			return Cost[num2 * W + num];
		}
	}
}
