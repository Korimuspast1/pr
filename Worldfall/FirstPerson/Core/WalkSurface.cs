using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class WalkSurface
	{
		public float X0;

		public float Y0;

		public float Cell;

		public int W;

		public int H;

		public float[] Top;

		public float Reach;

		private static readonly Dictionary<MeshModel, WalkSurface> Cache = new Dictionary<MeshModel, WalkSurface>();

		public static WalkSurface Of(MeshModel model, string slot = "deck")
		{
			if (model == null)
			{
				return null;
			}
			lock (Cache)
			{
				if (Cache.TryGetValue(model, out var value))
				{
					return value;
				}
				value = Build(model, slot);
				Cache[model] = value;
				return value;
			}
		}

		public bool HeightAt(float x, float y, out float z)
		{
			z = 0f;
			int num = (int)Math.Floor((x - X0) / Cell);
			int num2 = (int)Math.Floor((y - Y0) / Cell);
			if (num < 0 || num2 < 0 || num >= W || num2 >= H)
			{
				return false;
			}
			z = Top[num2 * W + num];
			return !float.IsNaN(z);
		}

		private static WalkSurface Build(MeshModel model, string slotName)
		{
			int num = model.SlotIndex(slotName);
			if (num < 0)
			{
				return null;
			}
			float num2 = model.Min.X - 0.1f;
			float num3 = model.Min.Y - 0.1f;
			int num4 = (int)Math.Ceiling((model.Max.X - num2) / 0.1f) + 2;
			int num5 = (int)Math.Ceiling((model.Max.Y - num3) / 0.1f) + 2;
			float[] array = new float[num4 * num5];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = float.NaN;
			}
			bool flag = false;
			float num6 = 0f;
			MeshPart[] parts = model.Parts;
			foreach (MeshPart meshPart in parts)
			{
				float[] pos = meshPart.Pos;
				int[] tri = meshPart.Tri;
				for (int k = 0; k < meshPart.TriangleCount; k++)
				{
					if (meshPart.Slot[k] != num)
					{
						continue;
					}
					int num7 = tri[k * 3] * 3;
					int num8 = tri[k * 3 + 1] * 3;
					int num9 = tri[k * 3 + 2] * 3;
					float num10 = pos[num7];
					float num11 = pos[num7 + 1];
					float num12 = pos[num7 + 2];
					float num13 = pos[num8];
					float num14 = pos[num8 + 1];
					float num15 = pos[num8 + 2];
					float num16 = pos[num9];
					float num17 = pos[num9 + 1];
					float num18 = pos[num9 + 2];
					float num19 = num13 - num10;
					float num20 = num14 - num11;
					float num21 = num15 - num12;
					float num22 = num16 - num10;
					float num23 = num17 - num11;
					float num24 = num18 - num12;
					float num25 = num20 * num24 - num21 * num23;
					float num26 = num21 * num22 - num19 * num24;
					float num27 = num19 * num23 - num20 * num22;
					float num28 = (float)Math.Sqrt(num25 * num25 + num26 * num26 + num27 * num27);
					if (num28 < 1E-09f || num27 / num28 < 0.75f)
					{
						continue;
					}
					float num29 = num19 * num23 - num20 * num22;
					if (Math.Abs(num29) < 1E-09f)
					{
						continue;
					}
					int num30 = Math.Max(0, (int)Math.Floor((Math.Min(num10, Math.Min(num13, num16)) - num2) / 0.1f));
					int num31 = Math.Min(num4 - 1, (int)Math.Floor((Math.Max(num10, Math.Max(num13, num16)) - num2) / 0.1f));
					int num32 = Math.Max(0, (int)Math.Floor((Math.Min(num11, Math.Min(num14, num17)) - num3) / 0.1f));
					int num33 = Math.Min(num5 - 1, (int)Math.Floor((Math.Max(num11, Math.Max(num14, num17)) - num3) / 0.1f));
					for (int l = num32; l <= num33; l++)
					{
						for (int m = num30; m <= num31; m++)
						{
							float num34 = num2 + ((float)m + 0.5f) * 0.1f;
							float num35 = num3 + ((float)l + 0.5f) * 0.1f;
							float num36 = ((num34 - num10) * num23 - (num35 - num11) * num22) / num29;
							float num37 = (num19 * (num35 - num11) - num20 * (num34 - num10)) / num29;
							if (!(num36 < -0.02f) && !(num37 < -0.02f) && !(num36 + num37 > 1.02f))
							{
								float num38 = num12 + num21 * num36 + num24 * num37;
								int num39 = l * num4 + m;
								if (float.IsNaN(array[num39]) || num38 > array[num39])
								{
									array[num39] = num38;
								}
								flag = true;
								num6 = Math.Max(num6, (float)Math.Sqrt(num34 * num34 + num35 * num35));
							}
						}
					}
				}
			}
			if (!flag)
			{
				return null;
			}
			return new WalkSurface
			{
				X0 = num2,
				Y0 = num3,
				Cell = 0.1f,
				W = num4,
				H = num5,
				Top = array,
				Reach = num6 + 0.1f
			};
		}
	}
}
