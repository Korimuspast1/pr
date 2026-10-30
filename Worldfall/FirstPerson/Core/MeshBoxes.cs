using System;

namespace FirstPerson.Core
{
	public static class MeshBoxes
	{
		public static MeshModel Boxes(MeshModel src, string name)
		{
			float min = Math.Max(0.05f, src.Max.Z - src.Min.Z) * 0.04f;
			MeshPart[] array = new MeshPart[src.Parts.Length];
			for (int i = 0; i < array.Length; i++)
			{
				MeshPart meshPart = src.Parts[i];
				MeshPart meshPart2 = (array[i] = new MeshPart
				{
					Name = meshPart.Name,
					Parent = meshPart.Parent,
					Pivot = meshPart.Pivot
				});
				int triangleCount = meshPart.TriangleCount;
				if (meshPart.VertexCount == 0 || triangleCount == 0)
				{
					meshPart2.Pos = new float[0];
					meshPart2.Nrm = new float[0];
					meshPart2.Tri = new int[0];
					meshPart2.Slot = new byte[0];
					meshPart2.Axis = new byte[0];
					continue;
				}
				float lo = float.MaxValue;
				float lo2 = float.MaxValue;
				float lo3 = float.MaxValue;
				float hi = float.MinValue;
				float hi2 = float.MinValue;
				float hi3 = float.MinValue;
				float[] pos = meshPart.Pos;
				for (int j = 0; j < pos.Length; j += 3)
				{
					lo = Math.Min(lo, pos[j]);
					hi = Math.Max(hi, pos[j]);
					lo2 = Math.Min(lo2, pos[j + 1]);
					hi2 = Math.Max(hi2, pos[j + 1]);
					lo3 = Math.Min(lo3, pos[j + 2]);
					hi3 = Math.Max(hi3, pos[j + 2]);
				}
				Thicken(ref lo, ref hi, min);
				Thicken(ref lo2, ref hi2, min);
				Thicken(ref lo3, ref hi3, min);
				float[] array2 = new float[256];
				for (int k = 0; k < triangleCount; k++)
				{
					int num = meshPart.Tri[k * 3] * 3;
					int num2 = meshPart.Tri[k * 3 + 1] * 3;
					int num3 = meshPart.Tri[k * 3 + 2] * 3;
					float num4 = pos[num2] - pos[num];
					float num5 = pos[num2 + 1] - pos[num + 1];
					float num6 = pos[num2 + 2] - pos[num + 2];
					float num7 = pos[num3] - pos[num];
					float num8 = pos[num3 + 1] - pos[num + 1];
					float num9 = pos[num3 + 2] - pos[num + 2];
					float num10 = num5 * num9 - num6 * num8;
					float num11 = num6 * num7 - num4 * num9;
					float num12 = num4 * num8 - num5 * num7;
					array2[meshPart.Slot[k]] += (float)Math.Sqrt(num10 * num10 + num11 * num11 + num12 * num12);
				}
				byte b = 0;
				for (int l = 1; l < 256; l++)
				{
					if (array2[l] > array2[b])
					{
						b = (byte)l;
					}
				}
				meshPart2.Pos = new float[24]
				{
					lo, lo2, lo3, hi, lo2, lo3, hi, hi2, lo3, lo,
					hi2, lo3, lo, lo2, hi3, hi, lo2, hi3, hi, hi2,
					hi3, lo, hi2, hi3
				};
				meshPart2.Nrm = new float[24]
				{
					-0.57735026f, -0.57735026f, -0.57735026f, 0.57735026f, -0.57735026f, -0.57735026f, 0.57735026f, 0.57735026f, -0.57735026f, -0.57735026f,
					0.57735026f, -0.57735026f, -0.57735026f, -0.57735026f, 0.57735026f, 0.57735026f, -0.57735026f, 0.57735026f, 0.57735026f, 0.57735026f,
					0.57735026f, -0.57735026f, 0.57735026f, 0.57735026f
				};
				meshPart2.Tri = new int[36]
				{
					0, 3, 2, 0, 2, 1, 4, 5, 6, 4,
					6, 7, 0, 1, 5, 0, 5, 4, 3, 7,
					6, 3, 6, 2, 0, 4, 7, 0, 7, 3,
					1, 2, 6, 1, 6, 5
				};
				meshPart2.Slot = new byte[12];
				for (int m = 0; m < 12; m++)
				{
					meshPart2.Slot[m] = b;
				}
				meshPart2.Axis = new byte[12];
				for (int n = 0; n < 12; n++)
				{
					meshPart2.Axis[n] = MeshLibrary.DominantAxis(meshPart2, n);
				}
			}
			return src.Derive(name, array);
		}

		private static void Thicken(ref float lo, ref float hi, float min)
		{
			if (!(hi - lo >= min))
			{
				float num = (lo + hi) * 0.5f;
				lo = num - min * 0.5f;
				hi = num + min * 0.5f;
			}
		}
	}
}
