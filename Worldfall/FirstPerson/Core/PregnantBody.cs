using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class PregnantBody
	{
		public const int Stages = 6;

		private static readonly Dictionary<MeshModel, MeshModel[]> Cache = new Dictionary<MeshModel, MeshModel[]>();

		public static int BuildsPerFrame = 1;

		private static int _built;

		public static int StageOf(float progress)
		{
			if (progress < 0f)
			{
				return 0;
			}
			int num = 1 + (int)(progress * 6f);
			if (num <= 6)
			{
				return num;
			}
			return 6;
		}

		public static float Size(int stage)
		{
			if (stage > 0)
			{
				return (float)Math.Pow(((float)stage - 0.5f) / 6f, 0.85);
			}
			return 0f;
		}

		public static MeshModel Morph(MeshModel model, int stage)
		{
			if (model == null || stage <= 0)
			{
				return model;
			}
			if (stage > 6)
			{
				stage = 6;
			}
			if (!Cache.TryGetValue(model, out var value))
			{
				value = (Cache[model] = new MeshModel[7]);
			}
			if (value[stage] != null)
			{
				return value[stage];
			}
			if (_built >= BuildsPerFrame)
			{
				for (int num = stage - 1; num >= 1; num--)
				{
					if (value[num] != null)
					{
						return value[num];
					}
				}
				return model;
			}
			_built++;
			return value[stage] = Build(model, Size(stage)) ?? model;
		}

		public static void NewFrame()
		{
			_built = 0;
		}

		private static MeshModel Build(MeshModel src, float k)
		{
			int num = src.PartIndex("torso");
			int num2 = src.PartIndex("hips");
			if (num < 0 || num2 < 0)
			{
				return null;
			}
			MeshPart meshPart = src.Parts[num];
			MeshPart meshPart2 = src.Parts[num2];
			float z = meshPart.Pivot.Z;
			float z2 = meshPart2.Pivot.Z;
			float num3 = z + 0.015f;
			float axisY = 0f;
			float num4 = 0f;
			float num5 = 0f;
			int num6 = 0;
			MeshPart[] array = new MeshPart[2] { meshPart, meshPart2 };
			foreach (MeshPart meshPart3 in array)
			{
				for (int j = 0; j < meshPart3.Pos.Length; j += 3)
				{
					float value = meshPart3.Pos[j];
					float num7 = meshPart3.Pos[j + 1];
					if (!(Math.Abs(meshPart3.Pos[j + 2] - num3) > 0.05f))
					{
						axisY += num7;
						num6++;
						if (Math.Abs(value) < 0.05f)
						{
							num4 = Math.Min(num4, num7);
						}
						num5 = Math.Max(num5, Math.Abs(value));
					}
				}
			}
			if (num6 == 0)
			{
				return null;
			}
			axisY /= num6;
			float num8 = Math.Max(0.06f, axisY - num4);
			float num9 = float.MaxValue;
			for (int l = 2; l < meshPart2.Pos.Length; l += 3)
			{
				num9 = Math.Min(num9, meshPart2.Pos[l]);
			}
			int num10 = src.PartIndex("fem_skirt");
			bool flag = num9 < z2 - 0.22f || num10 >= 0;
			float num11 = (0.11f + 0.7f * num8) * k;
			float num12 = 0.2f;
			float num13 = 0.15f;
			float num14 = num4 - num11 * 0.94f;
			MeshPart[] array2 = (MeshPart[])src.Parts.Clone();
			int[] array3 = new int[3] { num, num2, num10 };
			foreach (int num15 in array3)
			{
				if (num15 < 0)
				{
					continue;
				}
				float zTop = num3 + num12 + 0.04f;
				float zLow = (flag ? (num9 - 0.01f) : (num3 - num13 - 0.04f));
				float wide = num5 * 1.4f + 0.05f;
				MeshPart meshPart4 = Subdivide(Subdivide(src.Parts[num15], Near), Near);
				float[] array4 = (float[])meshPart4.Pos.Clone();
				bool[] array5 = new bool[meshPart4.VertexCount];
				bool flag2 = false;
				for (int m = 0; m < meshPart4.VertexCount; m++)
				{
					float num16 = array4[m * 3];
					float num17 = array4[m * 3 + 1];
					float num18 = array4[m * 3 + 2];
					float num19 = axisY - num17;
					float num20 = (float)Math.Sqrt(num16 * num16 + num19 * num19);
					if (num20 < 0.0001f || num19 <= 0f)
					{
						continue;
					}
					float num21 = (float)Math.Pow(num19 / num20, 1.5);
					float num22 = num18 - num3;
					float num23 = ((num22 >= 0f) ? (num22 / num12) : ((0f - num22) / num13));
					float num24 = ((num23 < 1f) ? ((float)Math.Pow(1f - num23 * num23, 0.7)) : 0f);
					float num25 = num11 * num24 * num21;
					if (flag && num22 < 0f)
					{
						float num26 = num14 - 0.08f * (0f - num22);
						float num27 = Math.Max(0f, num17 - num26) * num21 * num21;
						if (num27 > num25)
						{
							num25 = num27;
						}
					}
					if (!(num25 < 1E-05f))
					{
						array4[m * 3 + 1] = num17 - num25;
						array4[m * 3] = num16 * (1f + 0.6f * num25);
						if (num22 > 0.06f)
						{
							array4[m * 3 + 2] = num18 + num25 * 0.1f;
						}
						array5[m] = true;
						flag2 = true;
					}
				}
				if (flag2)
				{
					MeshPart meshPart5 = new MeshPart
					{
						Name = meshPart4.Name,
						Parent = meshPart4.Parent,
						Pivot = meshPart4.Pivot,
						Pos = array4,
						Nrm = (float[])meshPart4.Nrm.Clone(),
						Tri = meshPart4.Tri,
						Slot = meshPart4.Slot,
						Axis = new byte[meshPart4.TriangleCount],
						Uv = meshPart4.Uv
					};
					Renormal(meshPart5, array5);
					for (int n = 0; n < meshPart5.TriangleCount; n++)
					{
						meshPart5.Axis[n] = MeshLibrary.DominantAxis(meshPart5, n);
					}
					array2[num15] = meshPart5;
				}
				bool Near(float x0, float y0, float num28)
				{
					if (num28 > zLow && num28 < zTop && Math.Abs(x0) < wide)
					{
						return y0 < axisY + 0.03f;
					}
					return false;
				}
			}
			MeshModel meshModel = src.Derive(src.Name + "~preg" + (int)(k * 100f), array2);
			meshModel.Min = new Vec3(src.Min.X * (1f + num11), src.Min.Y - num11, src.Min.Z);
			meshModel.Max = new Vec3(src.Max.X * (1f + num11), src.Max.Y, src.Max.Z);
			return meshModel;
		}

		private static MeshPart Subdivide(MeshPart p, Func<float, float, float, bool> region)
		{
			List<float> pos = new List<float>(p.Pos);
			List<float> nrm = new List<float>(p.Nrm);
			List<int> list = new List<int>(p.Tri.Length * 2);
			List<byte> list2 = new List<byte>(p.Slot.Length * 2);
			Dictionary<long, int> mid = new Dictionary<long, int>();
			bool flag = false;
			for (int i = 0; i < p.TriangleCount; i++)
			{
				int num = p.Tri[i * 3];
				int num2 = p.Tri[i * 3 + 1];
				int num3 = p.Tri[i * 3 + 2];
				byte item = p.Slot[i];
				if (!In(num) && !In(num2) && !In(num3))
				{
					list.Add(num);
					list.Add(num2);
					list.Add(num3);
					list2.Add(item);
					continue;
				}
				flag = true;
				int num4 = Mid(num, num2);
				int num5 = Mid(num2, num3);
				int num6 = Mid(num3, num);
				list.AddRange(new int[12]
				{
					num, num4, num6, num4, num2, num5, num6, num5, num3, num4,
					num5, num6
				});
				list2.Add(item);
				list2.Add(item);
				list2.Add(item);
				list2.Add(item);
			}
			if (!flag)
			{
				return p;
			}
			MeshPart meshPart = new MeshPart
			{
				Name = p.Name,
				Parent = p.Parent,
				Pivot = p.Pivot,
				Pos = pos.ToArray(),
				Nrm = nrm.ToArray(),
				Tri = list.ToArray(),
				Slot = list2.ToArray()
			};
			meshPart.Axis = new byte[meshPart.TriangleCount];
			for (int j = 0; j < meshPart.TriangleCount; j++)
			{
				meshPart.Axis[j] = MeshLibrary.DominantAxis(meshPart, j);
			}
			return meshPart;
			bool In(int v)
			{
				return region(p.Pos[v * 3], p.Pos[v * 3 + 1], p.Pos[v * 3 + 2]);
			}
			int Mid(int a, int b)
			{
				long key = ((a < b) ? (((long)a << 32) | (uint)b) : (((long)b << 32) | (uint)a));
				if (mid.TryGetValue(key, out var value))
				{
					return value;
				}
				value = pos.Count / 3;
				for (int k = 0; k < 3; k++)
				{
					pos.Add((p.Pos[a * 3 + k] + p.Pos[b * 3 + k]) * 0.5f);
				}
				float num7 = p.Nrm[a * 3] + p.Nrm[b * 3];
				float num8 = p.Nrm[a * 3 + 1] + p.Nrm[b * 3 + 1];
				float num9 = p.Nrm[a * 3 + 2] + p.Nrm[b * 3 + 2];
				float num10 = (float)Math.Sqrt(num7 * num7 + num8 * num8 + num9 * num9);
				if (num10 < 1E-06f)
				{
					num7 = p.Nrm[a * 3];
					num8 = p.Nrm[a * 3 + 1];
					num9 = p.Nrm[a * 3 + 2];
					num10 = 1f;
				}
				nrm.Add(num7 / num10);
				nrm.Add(num8 / num10);
				nrm.Add(num9 / num10);
				mid[key] = value;
				return value;
			}
		}

		private static void Renormal(MeshPart p, bool[] moved)
		{
			float[] array = new float[p.Pos.Length];
			for (int i = 0; i < p.TriangleCount; i++)
			{
				int num = p.Tri[i * 3];
				int num2 = p.Tri[i * 3 + 1];
				int num3 = p.Tri[i * 3 + 2];
				if (moved[num] || moved[num2] || moved[num3])
				{
					float num4 = p.Pos[num2 * 3] - p.Pos[num * 3];
					float num5 = p.Pos[num2 * 3 + 1] - p.Pos[num * 3 + 1];
					float num6 = p.Pos[num2 * 3 + 2] - p.Pos[num * 3 + 2];
					float num7 = p.Pos[num3 * 3] - p.Pos[num * 3];
					float num8 = p.Pos[num3 * 3 + 1] - p.Pos[num * 3 + 1];
					float num9 = p.Pos[num3 * 3 + 2] - p.Pos[num * 3 + 2];
					float num10 = num5 * num9 - num6 * num8;
					float num11 = num6 * num7 - num4 * num9;
					float num12 = num4 * num8 - num5 * num7;
					int[] array2 = new int[3] { num, num2, num3 };
					foreach (int num13 in array2)
					{
						array[num13 * 3] += num10;
						array[num13 * 3 + 1] += num11;
						array[num13 * 3 + 2] += num12;
					}
				}
			}
			for (int k = 0; k < moved.Length; k++)
			{
				if (moved[k])
				{
					float num14 = array[k * 3];
					float num15 = array[k * 3 + 1];
					float num16 = array[k * 3 + 2];
					float num17 = (float)Math.Sqrt(num14 * num14 + num15 * num15 + num16 * num16);
					if (!(num17 < 1E-08f))
					{
						float num18 = p.Nrm[k * 3];
						float num19 = p.Nrm[k * 3 + 1];
						float num20 = p.Nrm[k * 3 + 2];
						float num21 = ((num14 * num18 + num15 * num19 + num16 * num20 < 0f) ? (-1f / num17) : (1f / num17));
						p.Nrm[k * 3] = num14 * num21;
						p.Nrm[k * 3 + 1] = num15 * num21;
						p.Nrm[k * 3 + 2] = num16 * num21;
					}
				}
			}
		}
	}
}
