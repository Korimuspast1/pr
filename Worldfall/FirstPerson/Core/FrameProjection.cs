using System;

namespace FirstPerson.Core
{
	public struct FrameProjection
	{
		public ViewState View;

		public int Width;

		public int Height;

		public float Focal;

		public float Horizon;

		public float Turn;

		public float ShiftRow;

		private float _shownYaw;

		private float _shownRow;

		private Lens _shownLens;

		private bool _shownSet;

		public static float MaxUncovered = 0.04f;

		public bool Moves
		{
			get
			{
				if (Turn == 0f)
				{
					return ShiftRow != 0f;
				}
				return true;
			}
		}

		private float Aspect => (float)Height / (float)Math.Max(1, Width);

		public Lens FrameLens => Lens.Of(in View, Aspect);

		public float MostTurn
		{
			get
			{
				if (Focal <= 0f)
				{
					return 0f;
				}
				return (float)(Math.Atan((double)Width * 0.5 / (double)Focal) - Math.Atan((1.0 - 2.0 * (double)Math.Min(0.5f, MaxUncovered)) * (double)Width * 0.5 / (double)Focal));
			}
		}

		private float MostLook
		{
			get
			{
				if (Focal <= 0f)
				{
					return 0f;
				}
				return (float)(Math.Atan((double)Height * 0.5 / (double)Focal) - Math.Atan((1.0 - 2.0 * (double)Math.Min(0.5f, MaxUncovered)) * (double)Height * 0.5 / (double)Focal));
			}
		}

		public float Uncovered
		{
			get
			{
				if (Width <= 0 || Focal <= 0f || Turn == 0f)
				{
					return 0f;
				}
				double num = Math.Atan((double)Width * 0.5 / (double)Focal);
				double num2 = (double)Width * 0.5 - (double)Focal * Math.Tan(Math.Max(-1.5, num - (double)Math.Abs(Turn)));
				return (float)Math.Max(0.0, num2 / (double)Width);
			}
		}

		public bool Project(float x, float y, float z, out float col, out float row, out float depth)
		{
			float along;
			return Project(x, y, z, out col, out row, out depth, out along);
		}

		public bool Project(float x, float y, float z, out float col, out float row, out float depth, out float along)
		{
			along = 0f;
			Lens lens;
			float num;
			float num2;
			if (_shownSet)
			{
				lens = _shownLens;
				num = _shownYaw;
				num2 = _shownRow;
			}
			else
			{
				lens = FrameLens;
				num = View.Yaw;
				num2 = Horizon;
			}
			float num3 = (float)Math.Cos(num);
			float num4 = (float)Math.Sin(num);
			float num5 = x - View.X;
			float num6 = y - View.Y;
			float num7 = z - View.Z;
			depth = num5 * num3 + num6 * num4;
			col = (row = 0f);
			float num8 = depth * lens.Cq + num7 * lens.Sq;
			if (num8 < 0.05f)
			{
				return false;
			}
			along = num8;
			float num9 = Focal / num8;
			col = (float)Width * 0.5f + (num5 * num4 - num6 * num3) * num9;
			row = num2 - (num7 * lens.Cq - depth * lens.Sq) * num9;
			return true;
		}

		public bool Source(float col, float row, out float srcCol, out float srcRow)
		{
			if (!_shownSet || Focal <= 0f)
			{
				srcCol = col;
				srcRow = row;
				return true;
			}
			Lens shownLens = _shownLens;
			Lens frameLens = FrameLens;
			float num = (col - (float)Width * 0.5f) / Focal;
			float num2 = (_shownRow - row) / Focal;
			float num3 = shownLens.Cq - num2 * shownLens.Sq;
			float num4 = shownLens.Sq + num2 * shownLens.Cq;
			float shownYaw = _shownYaw;
			float yaw = View.Yaw;
			float num5 = (float)Math.Cos(shownYaw) * num3 + (float)Math.Sin(shownYaw) * num;
			float num6 = (float)Math.Sin(shownYaw) * num3 - (float)Math.Cos(shownYaw) * num;
			float num7 = num5 * (float)Math.Cos(yaw) + num6 * (float)Math.Sin(yaw);
			float num8 = num5 * (float)Math.Sin(yaw) - num6 * (float)Math.Cos(yaw);
			float num9 = num7 * frameLens.Cq + num4 * frameLens.Sq;
			float num10 = num4 * frameLens.Cq - num7 * frameLens.Sq;
			if (num9 < 0.001f)
			{
				srcCol = ((num8 >= 0f) ? ((float)Width * 4f) : ((float)(-Width) * 3f));
				srcRow = ((num10 >= 0f) ? ((float)(-Height) * 3f) : ((float)Height * 4f));
				return false;
			}
			srcCol = (float)Width * 0.5f + num8 / num9 * Focal;
			srcRow = Horizon - num10 / num9 * Focal;
			return true;
		}

		public bool ShiftTowards(ViewState now)
		{
			float srcCol = (Turn = (ShiftRow = 0f));
			_shownSet = false;
			if (Focal <= 0f)
			{
				return false;
			}
			float num = now.Yaw - View.Yaw;
			while ((double)num > Math.PI)
			{
				num -= MathF.PI * 2f;
			}
			for (; (double)num < -Math.PI; num += MathF.PI * 2f)
			{
			}
			float num2 = now.X - View.X;
			float num3 = now.Y - View.Y;
			float num4 = now.Z - View.Z;
			if (Math.Abs(num) > 0.6f || num2 * num2 + num3 * num3 + num4 * num4 > 4f || Math.Abs(now.Fov - View.Fov) > 0.2f)
			{
				return false;
			}
			float mostTurn = MostTurn;
			float mostLook = MostLook;
			Turn = Math.Max(0f - mostTurn, Math.Min(mostTurn, num));
			float num5 = (float)Math.Atan(View.Pitch);
			float val = (float)Math.Atan(now.Pitch) - num5;
			val = Math.Max(0f - mostLook, Math.Min(mostLook, val));
			if (Turn == 0f && val == 0f)
			{
				return true;
			}
			ViewState view = View;
			view.Yaw = View.Yaw + Turn;
			view.Pitch = (float)Math.Tan(num5 + val);
			_shownLens = Lens.Of(in view, Aspect);
			_shownYaw = view.Yaw;
			_shownRow = (float)Height * 0.5f + _shownLens.Shift * Focal;
			_shownSet = true;
			ShiftRow = ((val == 0f) ? 0f : (Source((float)Width * 0.5f, (float)Height * 0.5f, out srcCol, out var srcRow) ? ((float)Height * 0.5f - srcRow) : 0f));
			if (ShiftRow == 0f && val != 0f)
			{
				ShiftRow = 0.001f;
			}
			return true;
		}
	}
}
