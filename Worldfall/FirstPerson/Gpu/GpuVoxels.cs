using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuVoxels : IDisposable
	{
		private readonly Dictionary<VoxelShape, Mesh> _meshes = new Dictionary<VoxelShape, Mesh>();

		private readonly List<Vector3> _v = new List<Vector3>();

		private readonly List<Vector3> _n = new List<Vector3>();

		private readonly List<Color32> _c = new List<Color32>();

		private readonly List<Vector4> _uv = new List<Vector4>();

		private readonly List<int> _t = new List<int>();

		public int Drawn;

		internal void ReferenceCollect(GpuModels.RefInstances into, int set, List<VoxelObject> list, ViewState view, float reach, Func<int, bool> isActor, Vector2 shadowCentre = default(Vector2), float shadowReach = 0f)
		{
			bool flag = set == 0;
			bool flag2 = set == 2;
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			foreach (VoxelObject item in list)
			{
				if (item.Shape == null || item.Overlay != flag2 || (flag && (item.NoShadow || item.Emissive)))
				{
					continue;
				}
				VoxelShape shape = item.Shape;
				float num3 = (float)shape.Width * item.SizeU + (float)shape.Height * item.SizeV + (float)shape.Depth * item.SizeN;
				if (flag)
				{
					float num4 = item.Origin.X - shadowCentre.x;
					float num5 = item.Origin.Y - shadowCentre.y;
					if (num4 * num4 + num5 * num5 > (shadowReach + num3) * (shadowReach + num3))
					{
						continue;
					}
				}
				else if (!flag2)
				{
					float num6 = (item.Origin.X - view.X) * num + (item.Origin.Y - view.Y) * num2;
					if (num6 < 0f - num3 || num6 - num3 > reach)
					{
						continue;
					}
				}
				Mesh mesh = Get(shape);
				InstData d = new InstData
				{
					M = GpuModels.Matrix(new Mat3(item.U * item.SizeU, item.V * item.SizeV, item.N * item.SizeN), item.Origin)
				};
				uint flash = item.Flash;
				uint multiply = item.Multiply;
				d.Flash = new Vector4((float)(flash & 0xFF) / 255f, (float)((flash >> 8) & 0xFF) / 255f, (float)((flash >> 16) & 0xFF) / 255f, (float)(flash >> 24) / 255f);
				d.Mult = ((multiply != 0) ? new Vector4((float)(multiply & 0xFF) / 255f, (float)((multiply >> 8) & 0xFF) / 255f, (float)((multiply >> 16) & 0xFF) / 255f, 1f) : Vector4.zero);
				d.Misc = new Vector4(1f, 0f, item.Glow, item.Emissive ? 1f : 0f);
				float y = (flag2 ? 5f : ((isActor != null && isActor(item.Tag)) ? 4f : 3f));
				d.Tag = new Vector4(item.Tag, y, 1f, 1f);
				d.Ground = new Vector4(item.Origin.Z, 1f, 0f, flag2 ? 1f : 0f);
				d.Pal = new Vector4(-1f, 0f, 0f, 0f);
				into.Add(set, mesh, ref d);
			}
		}

		private Mesh Get(VoxelShape s)
		{
			if (_meshes.TryGetValue(s, out var value))
			{
				return value;
			}
			if (_meshes.Count > 4000)
			{
				Clear();
			}
			value = Build(s);
			_meshes[s] = value;
			return value;
		}

		private Mesh Build(VoxelShape s)
		{
			_v.Clear();
			_n.Clear();
			_c.Clear();
			_uv.Clear();
			_t.Clear();
			int w = s.Width;
			int h = s.Height;
			int d = s.Depth;
			int d2;
			if (s.Grid != null)
			{
				d2 = d * 2;
				for (int i = 0; i < h; i++)
				{
					for (int j = 0; j < w; j++)
					{
						for (int k = -d; k < d; k++)
						{
							uint num = Cell(j, i, k);
							if (num >> 24 != 0)
							{
								Color32 col = Col(num, 128);
								if (Cell(j - 1, i, k) >> 24 == 0)
								{
									Face(j, i, k, 0, -1, 0f, 1f, col, 0f);
								}
								if (Cell(j + 1, i, k) >> 24 == 0)
								{
									Face(j, i, k, 0, 1, 0f, 1f, col, 0f);
								}
								if (Cell(j, i - 1, k) >> 24 == 0)
								{
									Face(j, i, k, 1, -1, 0f, 1f, col, 0f);
								}
								if (Cell(j, i + 1, k) >> 24 == 0)
								{
									Face(j, i, k, 1, 1, 0f, 1f, col, 0f);
								}
								if (Cell(j, i, k - 1) >> 24 == 0)
								{
									Face(j, i, k, 2, -1, 0f, 1f, col, 0f);
								}
								if (Cell(j, i, k + 1) >> 24 == 0)
								{
									Face(j, i, k, 2, 1, 0f, 1f, col, 0f);
								}
							}
						}
					}
				}
			}
			else
			{
				for (int l = 0; l < h; l++)
				{
					for (int m = 0; m < w; m++)
					{
						int num2 = l * w + m;
						int num3 = s.Thickness[num2];
						if (num3 != 0)
						{
							int num4 = ((s.Offset != null) ? s.Offset[num2] : 0);
							int speckle = ((s.Speckle != null) ? s.Speckle[num2] : 128);
							Color32 col2 = Col(s.Colors[num2], speckle);
							float flags = ((s.Glow != null && s.Glow[num2] != 0) ? 2f : 0f);
							float leaf = ((s.Speckle != null && s.Leaves != null && s.Leaves[num2] != 0) ? ((float)(int)s.Leaves[num2] + 4f * (float)num2) : 0f);
							Column(s, m, l, num4 - num3, num4 + num3, col2, flags, leaf);
							if (s.Mirror != null && s.Mirror[num2] != 0)
							{
								Column(s, m, l, -num4 - num3, -num4 + num3, col2, flags, leaf);
							}
						}
					}
				}
			}
			Mesh mesh = new Mesh();
			mesh.name = "FirstPersonVoxels";
			mesh.indexFormat = ((_v.Count > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16);
			mesh.SetVertices(_v);
			mesh.SetNormals(_n);
			mesh.SetColors(_c);
			mesh.SetUVs(0, _uv);
			mesh.SetUVs(1, _n);
			mesh.SetTriangles(_t, 0, calculateBounds: false);
			mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
			mesh.UploadMeshData(markNoLongerReadable: true);
			return mesh;
			uint Cell(int x, int y, int z)
			{
				if (x >= 0 && y >= 0 && x < w && y < h && z >= -d && z < d)
				{
					return s.Grid[(y * w + x) * d2 + z + d];
				}
				return 0u;
			}
		}

		private static Color32 Col(uint c, int speckle)
		{
			float num = (float)speckle / 128f;
			return new Color32((byte)Mathf.Min(255f, (float)(c & 0xFF) * num), (byte)Mathf.Min(255f, (float)((c >> 8) & 0xFF) * num), (byte)Mathf.Min(255f, (float)((c >> 16) & 0xFF) * num), byte.MaxValue);
		}

		private void Column(VoxelShape s, int x, int y, int z0, int z1, Color32 col, float flags, float leaf)
		{
			float depth = z1 - z0;
			Face(x, y, z0, 2, -1, leaf, 1f, col, flags);
			Face(x, y, z1 - 1, 2, 1, leaf, 1f, col, flags);
			for (int i = 0; i < 4; i++)
			{
				int x2 = x + ((i == 0) ? (-1) : ((i == 1) ? 1 : 0));
				int y2 = y + ((i == 2) ? (-1) : ((i == 3) ? 1 : 0));
				if (!Covered(s, x2, y2, z0, z1))
				{
					int axis = ((i >= 2) ? 1 : 0);
					int dir = (((i & 1) != 0) ? 1 : (-1));
					Face(x, y, z0, axis, dir, leaf, depth, col, flags);
				}
			}
		}

		private static bool Covered(VoxelShape s, int x, int y, int z0, int z1)
		{
			if (x < 0 || y < 0 || x >= s.Width || y >= s.Height)
			{
				return false;
			}
			int num = y * s.Width + x;
			int num2 = s.Thickness[num];
			if (num2 == 0)
			{
				return false;
			}
			int num3 = ((s.Offset != null) ? s.Offset[num] : 0);
			if (num3 - num2 <= z0 && num3 + num2 >= z1)
			{
				return true;
			}
			if (s.Mirror != null && s.Mirror[num] != 0 && -num3 - num2 <= z0)
			{
				return -num3 + num2 >= z1;
			}
			return false;
		}

		private void Face(int x, int y, int z, int axis, int dir, float leaf, float depth, Color32 col, float flags)
		{
			float num = x;
			float num2 = x + 1;
			float num3 = y;
			float num4 = y + 1;
			float num5 = z;
			float num6 = (float)z + ((axis == 2) ? 1f : depth);
			Vector3 item;
			Vector3 item2;
			Vector3 item3;
			Vector3 item4;
			Vector3 item5;
			switch (axis)
			{
			case 0:
			{
				float x2 = ((dir > 0) ? num2 : num);
				item = new Vector3(x2, num3, num5);
				item2 = new Vector3(x2, num4, num5);
				item3 = new Vector3(x2, num4, num6);
				item4 = new Vector3(x2, num3, num6);
				item5 = new Vector3(dir, 0f, 0f);
				break;
			}
			case 1:
			{
				float y2 = ((dir > 0) ? num4 : num3);
				item = new Vector3(num, y2, num5);
				item2 = new Vector3(num2, y2, num5);
				item3 = new Vector3(num2, y2, num6);
				item4 = new Vector3(num, y2, num6);
				item5 = new Vector3(0f, dir, 0f);
				break;
			}
			default:
			{
				float z2 = ((dir > 0) ? num6 : num5);
				item = new Vector3(num, num3, z2);
				item2 = new Vector3(num2, num3, z2);
				item3 = new Vector3(num2, num4, z2);
				item4 = new Vector3(num, num4, z2);
				item5 = new Vector3(0f, 0f, dir);
				break;
			}
			}
			int count = _v.Count;
			_v.Add(item);
			_v.Add(item2);
			_v.Add(item3);
			_v.Add(item4);
			Vector4 item6 = new Vector4(-1f, axis, flags, leaf);
			for (int i = 0; i < 4; i++)
			{
				_n.Add(item5);
				_c.Add(col);
				_uv.Add(item6);
			}
			_t.Add(count);
			_t.Add(count + 1);
			_t.Add(count + 2);
			_t.Add(count);
			_t.Add(count + 2);
			_t.Add(count + 3);
		}

		public void Collect(GpuInstances into, int set, List<VoxelObject> list, ViewState view, float reach, Func<int, bool> isActor, Vector2 shadowCentre = default(Vector2), float shadowReach = 0f)
		{
			bool flag = set == 0;
			bool flag2 = set == 2;
			if (set == 1)
			{
				Drawn = 0;
			}
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			foreach (VoxelObject item in list)
			{
				if (item.Shape == null || item.Overlay != flag2 || (flag && (item.NoShadow || item.Emissive)))
				{
					continue;
				}
				VoxelShape shape = item.Shape;
				float num3 = (float)shape.Width * item.SizeU + (float)shape.Height * item.SizeV + (float)shape.Depth * item.SizeN;
				if (flag)
				{
					float num4 = item.Origin.X - shadowCentre.x;
					float num5 = item.Origin.Y - shadowCentre.y;
					if (num4 * num4 + num5 * num5 > (shadowReach + num3) * (shadowReach + num3))
					{
						continue;
					}
				}
				else if (!flag2)
				{
					float num6 = (item.Origin.X - view.X) * num + (item.Origin.Y - view.Y) * num2;
					if (num6 < 0f - num3 || num6 - num3 > reach)
					{
						continue;
					}
				}
				Mesh mesh = Get(shape);
				InstData d = new InstData
				{
					M = GpuModels.Matrix(new Mat3(item.U * item.SizeU, item.V * item.SizeV, item.N * item.SizeN), item.Origin)
				};
				uint flash = item.Flash;
				uint multiply = item.Multiply;
				d.Flash = new Vector4((float)(flash & 0xFF) / 255f, (float)((flash >> 8) & 0xFF) / 255f, (float)((flash >> 16) & 0xFF) / 255f, (float)(flash >> 24) / 255f);
				d.Mult = ((multiply != 0) ? new Vector4((float)(multiply & 0xFF) / 255f, (float)((multiply >> 8) & 0xFF) / 255f, (float)((multiply >> 16) & 0xFF) / 255f, 1f) : Vector4.zero);
				d.Misc = new Vector4(1f, 0f, item.Glow, item.Emissive ? 1f : 0f);
				float y = (flag2 ? 5f : ((isActor != null && isActor(item.Tag)) ? 4f : 3f));
				d.Tag = new Vector4(item.Tag, y, 1f, 1f);
				d.Ground = new Vector4(item.Origin.Z, 1f, 0f, flag2 ? 1f : 0f);
				d.Pal = new Vector4(-1f, 0f, 0f, 0f);
				into.Add(set, mesh, ref d);
				if (set == 1)
				{
					Drawn++;
				}
			}
		}

		private void Clear()
		{
			foreach (Mesh value in _meshes.Values)
			{
				if (value != null)
				{
					UnityEngine.Object.Destroy(value);
				}
			}
			_meshes.Clear();
		}

		public void Dispose()
		{
			Clear();
		}
	}
}
