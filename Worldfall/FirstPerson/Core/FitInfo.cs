using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class FitInfo
	{
		public HousePlan Plan;

		public float Scale;

		public float OffsetX;

		public float OffsetY;

		public float FloorUp;

		public bool Mirror;

		public float[] StoreyZ;

		public float[] Stairwell;

		public float StairFootX = float.NaN;

		public float StairFootY;

		public float StairHeadX;

		public float StairHeadY;

		public float StairWidth;

		public float DoorWallX = float.NaN;

		public float DoorWallY;

		public float DoorHalf;

		public bool Tent;

		public bool UpperShut;

		public float StairRun;

		public float StairOpenX;

		public float StairOpenY;

		public int StairSteps;

		public const float SlabThick = 0.12f;

		public bool StairFree;

		public float StairWellBack;

		private float[][][] _loops;

		private float Sx
		{
			get
			{
				if (!Mirror)
				{
					return Scale;
				}
				return 0f - Scale;
			}
		}

		public float ModelX(float rx)
		{
			return (rx - OffsetX) / Sx;
		}

		public float ModelY(float ry)
		{
			return (ry - OffsetY) / Scale;
		}

		public float RoomX(float mx)
		{
			return mx * Sx + OffsetX;
		}

		public float RoomY(float my)
		{
			return my * Scale + OffsetY;
		}

		public int StoreyAt(float z)
		{
			int result = 0;
			for (int i = 1; i < StoreyZ.Length; i++)
			{
				if (z >= StoreyZ[i] - 0.35f)
				{
					result = i;
				}
			}
			return result;
		}

		public void ForEachWall(int storey, Action<float, float, float, float> line)
		{
			Inside(storey, 0f, 0f);
			if (storey < 0 || storey >= _loops.Length)
			{
				return;
			}
			float[][] array = _loops[storey];
			foreach (float[] array2 in array)
			{
				int num = array2.Length / 2;
				int num2 = 0;
				int num3 = num - 1;
				while (num2 < num)
				{
					line(array2[num3 * 2], array2[num3 * 2 + 1], array2[num2 * 2], array2[num2 * 2 + 1]);
					num3 = num2++;
				}
			}
		}

		public bool Inside(int storey, float rx, float ry)
		{
			if (_loops == null)
			{
				float[][][] array = new float[Plan.Storeys.Count][][];
				for (int i = 0; i < array.Length; i++)
				{
					List<List<HousePlan.P2>> loops = Plan.Storeys[i].Loops;
					array[i] = new float[loops.Count][];
					for (int j = 0; j < loops.Count; j++)
					{
						float[] array2 = new float[loops[j].Count * 2];
						for (int k = 0; k < loops[j].Count; k++)
						{
							array2[k * 2] = RoomX(loops[j][k].X);
							array2[k * 2 + 1] = RoomY(loops[j][k].Y);
						}
						array[i][j] = array2;
					}
				}
				_loops = array;
			}
			if (storey < 0 || storey >= _loops.Length)
			{
				return false;
			}
			bool flag = false;
			float[][] array3 = _loops[storey];
			foreach (float[] array4 in array3)
			{
				int num = array4.Length / 2;
				int num2 = 0;
				int num3 = num - 1;
				while (num2 < num)
				{
					float num4 = array4[num2 * 2];
					float num5 = array4[num2 * 2 + 1];
					float num6 = array4[num3 * 2];
					float num7 = array4[num3 * 2 + 1];
					if (num5 > ry != num7 > ry && rx < (num6 - num4) * (ry - num5) / (num7 - num5) + num4)
					{
						flag = !flag;
					}
					num3 = num2++;
				}
			}
			return flag;
		}

		public bool Clear(int storey, float rx, float ry, float radius)
		{
			if (!Inside(storey, rx, ry))
			{
				return false;
			}
			for (int i = 0; i < 8; i++)
			{
				float num = (float)i * MathF.PI / 4f;
				if (!Inside(storey, rx + (float)Math.Cos(num) * radius, ry + (float)Math.Sin(num) * radius))
				{
					return false;
				}
			}
			return true;
		}

		public float CeilingAt(int storey, float rx, float ry)
		{
			HousePlan.Storey storey2 = Plan.Storeys[storey];
			float num = Plan.CeilingAt(storey, ModelX(rx), ModelY(ry));
			if (float.IsNaN(num))
			{
				for (float num2 = 0.06f; num2 < 0.5f; num2 += 0.06f)
				{
					if (!float.IsNaN(num))
					{
						break;
					}
					for (int i = 0; i < 8; i++)
					{
						if (!float.IsNaN(num))
						{
							break;
						}
						float num3 = (float)i * MathF.PI / 4f;
						num = Plan.CeilingAt(storey, ModelX(rx + (float)Math.Cos(num3) * num2), ModelY(ry + (float)Math.Sin(num3) * num2));
					}
				}
				if (float.IsNaN(num))
				{
					num = storey2.Top;
				}
			}
			float num4 = (num - Plan.FloorZ) * Scale;
			if (storey + 1 < StoreyZ.Length && Inside(storey + 1, rx, ry))
			{
				num4 = Math.Min(num4, StoreyZ[storey + 1] - 0.12f);
			}
			return num4;
		}
	}
}
