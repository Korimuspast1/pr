using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuTerrain : IDisposable
	{
		public const int ChunkTiles = 64;

		private ComputeBuffer _tiles;

		private uint[] _packed = new uint[0];

		private ComputeBuffer _pathSlots;

		private ComputeBuffer _pathTexels;

		private uint[] _slots = new uint[0];

		private uint[] _texels = new uint[0];

		private float _pathsAt = -10f;

		private int _pathCount = -1;

		private TerrainCache _bound;

		private int _w;

		private int _h;

		private float _maxHeight;

		private static readonly float[] Steps = new float[5] { 0.25f, 0.5f, 1f, 2f, 4f };

		private readonly Mesh[] _grids = new Mesh[Steps.Length];

		private Mesh _outside;

		private int _outsideW;

		private int _outsideH;

		private readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();

		private readonly List<KeyValuePair<float, int>> _order = new List<KeyValuePair<float, int>>();

		public int ChunksDrawn;

		public int RowsUploaded;

		private byte[] _isWall = new byte[0];

		private readonly List<uint> _wallList = new List<uint>();

		private ComputeBuffer _walls;

		private bool _wallsDirty = true;

		private float[] _chunkDeep = new float[0];

		public float MaxHeight => _maxHeight;

		public float Floor { get; private set; }

		public int Walls => _wallList.Count;

		public ComputeBuffer Tiles => _tiles;

		public ComputeBuffer PathSlots => _pathSlots;

		public ComputeBuffer PathTexels => _pathTexels;

		public void SyncPaths(TerrainCache land, bool fresh)
		{
			int num = land.Width * land.Height;
			if (num <= 0 || (!fresh && _pathSlots != null && _pathSlots.count == num && land.PathTileList.Count == _pathCount && !(Time.unscaledTime - _pathsAt > 2f)))
			{
				return;
			}
			_pathsAt = Time.unscaledTime;
			_pathCount = land.PathTileList.Count;
			if (_pathSlots == null || _pathSlots.count != num)
			{
				_pathSlots?.Release();
				_pathSlots = new ComputeBuffer(num, 4, ComputeBufferType.Structured);
				_slots = new uint[num];
			}
			Array.Clear(_slots, 0, _slots.Length);
			int[] pathSlot = land.PathSlot;
			foreach (int pathTile in land.PathTileList)
			{
				if (pathTile >= 0 && pathTile < num && pathTile < pathSlot.Length)
				{
					_slots[pathTile] = (uint)pathSlot[pathTile];
				}
			}
			_pathSlots.SetData(_slots);
			byte[] pathTexels = land.PathTexels;
			int num2 = Math.Max(16, (pathTexels.Length + 3) / 4);
			if (_pathTexels == null || _pathTexels.count < num2)
			{
				_pathTexels?.Release();
				_pathTexels = new ComputeBuffer(Math.Max(num2, 1024), 4, ComputeBufferType.Structured);
				_texels = new uint[_pathTexels.count];
			}
			Buffer.BlockCopy(pathTexels, 0, _texels, 0, Math.Min(pathTexels.Length, _texels.Length * 4));
			_pathTexels.SetData(_texels);
		}

		public void Sync(TerrainCache land)
		{
			RowsUploaded = 0;
			int width = land.Width;
			int height = land.Height;
			if (width <= 0 || height <= 0)
			{
				return;
			}
			bool flag = _bound != land || width != _w || height != _h || _tiles == null;
			int x;
			int x2;
			int y;
			int y2;
			bool flag2 = land.TakeChanged(out x, out y, out x2, out y2);
			if (flag)
			{
				_bound = land;
				_w = width;
				_h = height;
				_tiles?.Release();
				_tiles = new ComputeBuffer(width * height, 16, ComputeBufferType.Structured);
				_packed = new uint[width * height * 4];
				x = 0;
				y = 0;
				x2 = width;
				y2 = height;
				flag2 = true;
			}
			if (!flag2)
			{
				return;
			}
			x = Mathf.Clamp(x, 0, width);
			x2 = Mathf.Clamp(x2, 0, width);
			y = Mathf.Clamp(y, 0, height);
			y2 = Mathf.Clamp(y2, 0, height);
			if (x2 <= x || y2 <= y)
			{
				return;
			}
			Pack(land, x, y, x2, y2);
			NoteWalls(land, x, y, x2, y2);
			NoteDeep(land, x, y, x2, y2);
			_tiles.SetData(_packed, y * width * 4, y * width * 4, (y2 - y) * width * 4);
			RowsUploaded = y2 - y;
			float num = 0f;
			float[] blockMax = land.BlockMax;
			foreach (float num2 in blockMax)
			{
				if (num2 != float.MaxValue && num2 > num)
				{
					num = num2;
				}
			}
			_maxHeight = Mathf.Max(4f, num + 1f);
			float num3 = (flag ? land.OutsideHeight : Floor);
			float[] array = ((land.Ground.Length == land.Heights.Length) ? land.Ground : land.Heights);
			float[] heights = land.Heights;
			for (int j = y; j < y2; j++)
			{
				int k = j * width + x;
				for (int num4 = j * width + x2; k < num4; k++)
				{
					num3 = Math.Min(num3, Math.Min(array[k], heights[k]));
				}
			}
			if (land.HasSeabed && land.Floor.Length == array.Length)
			{
				for (int l = y; l < y2; l++)
				{
					int m = l * width + x;
					for (int num5 = l * width + x2; m < num5; m++)
					{
						num3 = Math.Min(num3, land.Floor[m]);
					}
				}
			}
			Floor = num3;
		}

		private static uint DepthByte(TerrainCache land, int i)
		{
			int num = (int)(land.DepthOf(i) * 10f + 0.5f);
			if (num >= 0)
			{
				if (num <= 255)
				{
					return (uint)num;
				}
				return 255u;
			}
			return 0u;
		}

		private void Pack(TerrainCache land, int x0, int y0, int x1, int y1)
		{
			int width = land.Width;
			float[] array = ((land.Ground.Length == land.Heights.Length) ? land.Ground : land.Heights);
			float[] heights = land.Heights;
			uint[] colors = land.Colors;
			byte[] sun = land.Sun;
			byte[] open = land.Open;
			byte[] flags = land.Flags;
			byte[] materials = land.Materials;
			byte[] array2 = ((land.Marks.Length == materials.Length) ? land.Marks : null);
			byte[] array3 = ((land.SeaReach.Length == colors.Length) ? land.SeaReach : null);
			uint[] packed = _packed;
			for (int i = y0; i < y1; i++)
			{
				for (int j = x0; j < x1; j++)
				{
					int num = i * width + j;
					int num2 = num * 4;
					packed[num2] = FloatBits(array[num]);
					packed[num2 + 1] = FloatBits(heights[num]);
					packed[num2 + 2] = (colors[num] & 0xFFFFFF) | (uint)((array3 != null) ? (array3[num] << 24) : 0);
					uint num3 = (((flags[num] & 1) != 0) ? DepthByte(land, num) : open[num]);
					packed[num2 + 3] = sun[num] | (num3 << 8) | (uint)(flags[num] << 16) | (uint)(((materials[num] & 0xF) | ((array2 != null) ? (array2[num] << 4) : 0)) << 24);
				}
			}
		}

		private unsafe static uint FloatBits(float f)
		{
			return *(uint*)(&f);
		}

		internal float DeepSkirtForTest(int x, int y)
		{
			int num = (_w + 64 - 1) / 64;
			int num2 = y / 64 * num + x / 64;
			if (num2 < 0 || num2 >= _chunkDeep.Length)
			{
				return -1f;
			}
			return _chunkDeep[num2];
		}

		private void NoteDeep(TerrainCache land, int x0, int y0, int x1, int y1)
		{
			int width = land.Width;
			int height = land.Height;
			int num = (width + 64 - 1) / 64;
			int num2 = (height + 64 - 1) / 64;
			if (_chunkDeep.Length != num * num2)
			{
				_chunkDeep = new float[num * num2];
				x0 = 0;
				y0 = 0;
				x1 = width;
				y1 = height;
			}
			float[] array = ((land.Ground.Length == land.Heights.Length) ? land.Ground : land.Heights);
			for (int i = Math.Max(0, (y0 - 1) / 64); i <= Math.Min(num2 - 1, y1 / 64); i++)
			{
				for (int j = Math.Max(0, (x0 - 1) / 64); j <= Math.Min(num - 1, x1 / 64); j++)
				{
					float num3 = 0f;
					float num4 = 0f;
					int num5 = Math.Max(0, i * 64 - 1);
					int num6 = Math.Min(height, (i + 1) * 64 + 1);
					int num7 = Math.Max(0, j * 64 - 1);
					int num8 = Math.Min(width, (j + 1) * 64 + 1);
					for (int k = num5; k < num6; k++)
					{
						int l = k * width + num7;
						for (int num9 = k * width + num8; l < num9; l++)
						{
							float num10 = array[l];
							if (num10 < num3)
							{
								num3 = num10;
							}
							else if (num10 > num4)
							{
								num4 = num10;
							}
						}
					}
					_chunkDeep[i * num + j] = ((num3 < -0.5f) ? Math.Min(num4 - num3, 30f) : 0f);
				}
			}
		}

		private void NoteWalls(TerrainCache land, int x0, int y0, int x1, int y1)
		{
			int width = land.Width;
			int num = width * land.Height;
			if (_isWall.Length != num)
			{
				_isWall = new byte[num];
				_wallsDirty = true;
			}
			byte[] flags = land.Flags;
			for (int i = y0; i < y1; i++)
			{
				for (int j = x0; j < x1; j++)
				{
					int num2 = i * width + j;
					byte b = (((flags[num2] & 8) != 0) ? ((byte)1) : ((byte)0));
					if (b != _isWall[num2])
					{
						_isWall[num2] = b;
						_wallsDirty = true;
					}
				}
			}
		}

		private void SendWalls()
		{
			if (!_wallsDirty && _walls != null)
			{
				return;
			}
			_wallsDirty = false;
			_wallList.Clear();
			for (int i = 0; i < _isWall.Length; i++)
			{
				if (_isWall[i] != 0)
				{
					_wallList.Add((uint)i);
				}
			}
			int num = Math.Max(1, _wallList.Count);
			if (_walls == null || _walls.count < num)
			{
				_walls?.Release();
				_walls = new ComputeBuffer(Math.Max(256, num * 3 / 2), 4, ComputeBufferType.Structured);
			}
			if (_wallList.Count > 0)
			{
				_walls.SetData(_wallList);
			}
		}

		public void Draw(CommandBuffer cmd, Material mat, ViewState view, float focal, int width, int height, float reach, Lens lens)
		{
			ChunksDrawn = 0;
			if (_tiles == null)
			{
				return;
			}
			EnsureMeshes();
			int num = (_w + 64 - 1) / 64;
			int num2 = (_h + 64 - 1) / 64;
			float num3 = Mathf.Cos(view.Yaw);
			float num4 = Mathf.Sin(view.Yaw);
			float num5 = num4;
			float num6 = 0f - num3;
			float num7 = (float)width * 0.5f / focal;
			float b = ((lens.Sq < 0f) ? (view.Z - GodCamera.LowestGround) : ((lens.Sq > 0f) ? (MaxHeight + 2f - view.Z) : 0f));
			float num8 = num7 * lens.Cq;
			float num9 = num7 * Mathf.Abs(lens.Sq) * Mathf.Max(0f, b);
			_order.Clear();
			float num10 = 32f;
			float num11 = num10 * 1.4143f;
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					float num12 = (float)(j * 64) + num10;
					float num13 = (float)(i * 64) + num10;
					float num14 = num12 - view.X;
					float num15 = num13 - view.Y;
					float num16 = num14 * num3 + num15 * num4;
					float f = num14 * num5 + num15 * num6;
					if (!(num16 < 0f - num11) && !(num16 - num11 > reach) && !(Mathf.Abs(f) - num11 > Mathf.Max(0f, num16 + num11) * num8 + num9))
					{
						float key = Mathf.Max(0f, Mathf.Sqrt(num14 * num14 + num15 * num15) - num11);
						_order.Add(new KeyValuePair<float, int>(key, i * num + j));
					}
				}
			}
			_order.Sort((KeyValuePair<float, int> a, KeyValuePair<float, int> keyValuePair) => a.Key.CompareTo(keyValuePair.Key));
			foreach (KeyValuePair<float, int> item in _order)
			{
				int num17 = item.Value % num;
				int num18 = item.Value / num;
				int num19 = ((!(item.Key < 24f)) ? ((item.Key < 56f) ? 1 : ((item.Key < 140f) ? 2 : ((item.Key < 320f) ? 3 : 4))) : 0);
				float num20 = Steps[num19];
				float num21 = 0.6f * num20 + 0.2f;
				if (item.Value < _chunkDeep.Length)
				{
					num21 = Mathf.Max(num21, _chunkDeep[item.Value]);
				}
				_props.SetVector("_Chunk", new Vector4(num17 * 64, num18 * 64, num20, num21));
				cmd.DrawMesh(_grids[num19], Matrix4x4.identity, mat, 0, 0, _props);
				ChunksDrawn++;
			}
			if (_outside == null || _outsideW != _w || _outsideH != _h)
			{
				BuildOutside();
			}
			_props.SetVector("_Chunk", new Vector4(0f, 0f, -1f, 0f));
			cmd.DrawMesh(_outside, Matrix4x4.identity, mat, 0, 0, _props);
			SendWalls();
			if (_wallList.Count > 0)
			{
				cmd.SetGlobalBuffer("_FP_Walls", _walls);
				cmd.DrawProcedural(Matrix4x4.identity, mat, 1, MeshTopology.Triangles, 120, _wallList.Count);
			}
		}

		private void EnsureMeshes()
		{
			for (int i = 0; i < Steps.Length; i++)
			{
				if (_grids[i] == null)
				{
					_grids[i] = BuildGrid(Mathf.RoundToInt(64f / Steps[i]));
				}
			}
		}

		private static Mesh BuildGrid(int n)
		{
			int side = n + 2;
			List<Vector3> verts = new List<Vector3>(side * side + side * 4);
			List<int> tris = new List<int>(side * side * 6 + side * 24);
			for (int i = -1; i <= n; i++)
			{
				for (int j = -1; j <= n; j++)
				{
					verts.Add(new Vector3(j, i, 0f));
				}
			}
			for (int k = 0; k < side - 1; k++)
			{
				for (int l = 0; l < side - 1; l++)
				{
					int num = k * side + l;
					int item = num + 1;
					int num2 = num + side;
					int item2 = num2 + 1;
					tris.Add(num);
					tris.Add(num2);
					tris.Add(item);
					tris.Add(item);
					tris.Add(num2);
					tris.Add(item2);
				}
			}
			Edge(0, 1);
			Edge((side - 1) * side, 1);
			Edge(0, side);
			Edge(side - 1, side);
			Mesh mesh = new Mesh();
			mesh.name = "FirstPersonTerrainGrid" + n;
			mesh.indexFormat = ((verts.Count > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16);
			mesh.SetVertices(verts);
			mesh.SetTriangles(tris, 0, calculateBounds: false);
			mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
			return mesh;
			void Edge(int start, int stride)
			{
				int count = verts.Count;
				for (int m = 0; m < side; m++)
				{
					Vector3 vector = verts[start + m * stride];
					verts.Add(new Vector3(vector.x, vector.y, 1f));
				}
				for (int num3 = 0; num3 < side - 1; num3++)
				{
					int item3 = start + num3 * stride;
					int item4 = start + (num3 + 1) * stride;
					int item5 = count + num3;
					int item6 = count + num3 + 1;
					tris.Add(item3);
					tris.Add(item5);
					tris.Add(item4);
					tris.Add(item4);
					tris.Add(item5);
					tris.Add(item6);
				}
			}
		}

		private void BuildOutside()
		{
			_outsideW = _w;
			_outsideH = _h;
			if (_outside != null)
			{
				UnityEngine.Object.Destroy(_outside);
			}
			float num = _w;
			float num2 = _h;
			List<Vector3> vertices = new List<Vector3>
			{
				new Vector3(-6000f, -6000f, 0f),
				new Vector3(num + 6000f, -6000f, 0f),
				new Vector3(num + 6000f, 0f, 0f),
				new Vector3(-6000f, 0f, 0f),
				new Vector3(-6000f, num2, 0f),
				new Vector3(num + 6000f, num2, 0f),
				new Vector3(num + 6000f, num2 + 6000f, 0f),
				new Vector3(-6000f, num2 + 6000f, 0f),
				new Vector3(-6000f, 0f, 0f),
				new Vector3(0f, 0f, 0f),
				new Vector3(0f, num2, 0f),
				new Vector3(-6000f, num2, 0f),
				new Vector3(num, 0f, 0f),
				new Vector3(num + 6000f, 0f, 0f),
				new Vector3(num + 6000f, num2, 0f),
				new Vector3(num, num2, 0f)
			};
			List<int> list = new List<int>();
			for (int i = 0; i < 4; i++)
			{
				int num3 = i * 4;
				list.Add(num3);
				list.Add(num3 + 1);
				list.Add(num3 + 2);
				list.Add(num3);
				list.Add(num3 + 2);
				list.Add(num3 + 3);
			}
			_outside = new Mesh
			{
				name = "FirstPersonOutsideSea"
			};
			_outside.SetVertices(vertices);
			_outside.SetTriangles(list, 0, calculateBounds: false);
			_outside.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
		}

		public void Dispose()
		{
			_tiles?.Release();
			_tiles = null;
			_walls?.Release();
			_walls = null;
			_wallsDirty = true;
			_pathSlots?.Release();
			_pathTexels?.Release();
			_pathSlots = (_pathTexels = null);
			for (int i = 0; i < _grids.Length; i++)
			{
				if (_grids[i] != null)
				{
					UnityEngine.Object.Destroy(_grids[i]);
					_grids[i] = null;
				}
			}
			if (_outside != null)
			{
				UnityEngine.Object.Destroy(_outside);
			}
			_outside = null;
			_bound = null;
		}
	}
}
