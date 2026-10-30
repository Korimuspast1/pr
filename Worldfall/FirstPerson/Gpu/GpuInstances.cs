using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuInstances : IDisposable
	{
		internal sealed class Group
		{
			public Mesh Mesh;

			public InstData[] Items = new InstData[16];

			public int Count;

			public int Start;

			public int Kept;

			public int Ensured;

			public Texture Poses;

			public volatile bool Mirrored;
		}

		internal struct Mark
		{
			public int Palettes;

			public int Pool;

			public int Order0;

			public int Order1;

			public int Order2;

			public int Order(int set)
			{
				switch (set)
				{
				default:
					return Order2;
				case 1:
					return Order1;
				case 0:
					return Order0;
				}
			}
		}

		private sealed class LayJob : WorkerPool.Job
		{
			public GpuInstances Into;

			public override void Run(int chunk, int worker)
			{
				Into.LayRunOf(chunk);
			}
		}

		public const int Shadow = 0;

		public const int Scene = 1;

		public const int Overlay = 2;

		private readonly Dictionary<Mesh, Group>[] _groups = new Dictionary<Mesh, Group>[4]
		{
			new Dictionary<Mesh, Group>(),
			new Dictionary<Mesh, Group>(),
			new Dictionary<Mesh, Group>(),
			new Dictionary<Mesh, Group>()
		};

		private readonly List<Group>[] _order = new List<Group>[3]
		{
			new List<Group>(),
			new List<Group>(),
			new List<Group>()
		};

		private readonly List<Group> _pool = new List<Group>();

		private int _poolUsed;

		private Vector4[] _pal = new Vector4[4096];

		private int _palCount;

		private InstData[] _all = new InstData[0];

		private ComputeBuffer _inst;

		private ComputeBuffer _palBuf;

		private readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();

		private static readonly int InstBaseId = Shader.PropertyToID("_InstBase");

		private static readonly int CrowdId = Shader.PropertyToID("_FP_Crowd");

		private static readonly int CrowdPosesId = Shader.PropertyToID("_FP_CrowdPoses");

		private static readonly int ModelTexId = Shader.PropertyToID("_FP_ModelTex");

		private static readonly int ModelTexSizeId = Shader.PropertyToID("_FP_ModelTexSize");

		private static readonly int ModelTexOnId = Shader.PropertyToID("_FP_ModelTexOn");

		public int Instances;

		public int Draws;

		public int Frame;

		internal static bool LayOnWorkers = true;

		private const int LayRun = 8192;

		private int _run = 8192;

		private readonly List<Group> _laid = new List<Group>();

		private LayJob _layJob;

		internal static bool VerifyLay;

		internal static int LayChecks;

		internal static int LaySplit;

		internal static int LayDiffers;

		internal static string LayFirst;

		private InstData[] _layCheck = new InstData[0];

		private readonly List<int> _startCheck = new List<int>();

		internal Vector4[] PaletteArray => _pal;

		internal int PaletteCount => _palCount;

		public void Begin()
		{
			Frame++;
			Dictionary<Mesh, Group>[] groups = _groups;
			for (int i = 0; i < groups.Length; i++)
			{
				groups[i].Clear();
			}
			List<Group>[] order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				order[i].Clear();
			}
			_poolUsed = 0;
			_palCount = 0;
			Instances = (Draws = 0);
		}

		public int AddPalette(Vector4[] entries, int count)
		{
			Vector4[] pal;
			int num = ReservePalette(count, out pal);
			Array.Copy(entries, 0, pal, num, count);
			return num;
		}

		public int ReservePalette(int count, out Vector4[] pal)
		{
			int palCount = _palCount;
			if (palCount + count > _pal.Length)
			{
				Array.Resize(ref _pal, Math.Max(_pal.Length * 2, palCount + count));
			}
			_palCount = palCount + count;
			pal = _pal;
			return palCount;
		}

		public Group For(int set, Mesh mesh)
		{
			Dictionary<Mesh, Group> dictionary = _groups[set];
			if (!dictionary.TryGetValue(mesh, out var value))
			{
				if (_poolUsed == _pool.Count)
				{
					_pool.Add(new Group());
				}
				value = _pool[_poolUsed++];
				value.Mesh = mesh;
				value.Count = 0;
				value.Poses = null;
				dictionary[mesh] = value;
				_order[set].Add(value);
			}
			return value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref InstData Next(Group g)
		{
			if (g.Count == g.Items.Length)
			{
				Array.Resize(ref g.Items, g.Count * 2);
			}
			return ref g.Items[g.Count++];
		}

		public void Add(int set, Mesh mesh, ref InstData d)
		{
			Next(For(set, mesh)) = d;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Reserve(Group g)
		{
			if (g.Count == g.Items.Length)
			{
				Array.Resize(ref g.Items, g.Count * 2);
			}
			return g.Count++;
		}

		public static int ReserveWithin(Group g, int stamp, int most)
		{
			if (g.Ensured != stamp)
			{
				g.Ensured = stamp;
				int num = g.Count + most;
				if (g.Items.Length < num)
				{
					Array.Resize(ref g.Items, Math.Max(g.Items.Length * 2, num));
				}
			}
			if (g.Count == g.Items.Length)
			{
				throw new InvalidOperationException("gpu copies: more copies of " + g.Mesh?.name + " than made room for");
			}
			return g.Count++;
		}

		internal void EnsurePalettes(int count)
		{
			if (_pal.Length < count)
			{
				Array.Resize(ref _pal, Math.Max(_pal.Length * 2, count));
			}
		}

		internal Mark MarkForTest()
		{
			List<Group>[] order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item in order[i])
				{
					item.Kept = item.Count;
				}
			}
			return new Mark
			{
				Palettes = _palCount,
				Pool = _poolUsed,
				Order0 = _order[0].Count,
				Order1 = _order[1].Count,
				Order2 = _order[2].Count
			};
		}

		internal void RollBackForTest(Mark m)
		{
			for (int i = 0; i < _order.Length; i++)
			{
				List<Group> list = _order[i];
				int num = m.Order(i);
				for (int j = num; j < list.Count; j++)
				{
					_groups[i].Remove(list[j].Mesh);
				}
				list.RemoveRange(num, list.Count - num);
				foreach (Group item in list)
				{
					item.Count = item.Kept;
				}
			}
			_poolUsed = m.Pool;
			_palCount = m.Palettes;
		}

		internal List<Group> OrderForTest(int set)
		{
			return _order[set];
		}

		public void Upload(CommandBuffer cmd)
		{
			Lay();
			int instances = Instances;
			if (_inst == null || _inst.count < _all.Length)
			{
				_inst?.Release();
				_inst = new ComputeBuffer(_all.Length, 160, ComputeBufferType.Structured);
			}
			_inst.SetData(_all, 0, 0, Math.Max(1, instances));
			int num = Math.Max(1, _palCount);
			if (_palBuf == null || _palBuf.count < num)
			{
				_palBuf?.Release();
				_palBuf = new ComputeBuffer(Math.Max(4096, num * 3 / 2), 16, ComputeBufferType.Structured);
			}
			_palBuf.SetData(_pal, 0, 0, num);
			cmd.SetGlobalBuffer("_FP_Inst", _inst);
			cmd.SetGlobalBuffer("_FP_Palette", _palBuf);
		}

		internal void Lay()
		{
			if (!LayOnWorkers)
			{
				LayOld();
			}
			else if (VerifyLay)
			{
				LayVerified();
			}
			else
			{
				LayNew(8192);
			}
		}

		private void LayNew(int run)
		{
			_run = run;
			int num = 0;
			List<Group>[] order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item in order[i])
				{
					num += item.Count;
				}
			}
			Instances = num;
			if (_all.Length < Math.Max(1, num))
			{
				_all = new InstData[Math.Max(64, num * 3 / 2)];
			}
			int num2 = 0;
			_laid.Clear();
			order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item2 in order[i])
				{
					item2.Start = num2;
					item2.Mirrored = false;
					num2 += item2.Count;
					if (item2.Count > 0)
					{
						_laid.Add(item2);
					}
				}
			}
			int num3 = (num + run - 1) / run;
			WorkerPool workerPool = ((num3 >= 2) ? GpuModels.Pool : null);
			if (workerPool == null || workerPool.Workers == 0 || !workerPool.TryBegin())
			{
				for (int j = 0; j < num3; j++)
				{
					LayRunOf(j);
				}
				return;
			}
			if (VerifyLay)
			{
				LaySplit++;
			}
			try
			{
				LayJob layJob = _layJob;
				if (layJob == null)
				{
					LayJob obj = new LayJob
					{
						Into = this
					};
					LayJob layJob2 = obj;
					_layJob = obj;
					layJob = layJob2;
				}
				workerPool.Start(layJob, num3);
				try
				{
					workerPool.Finish();
				}
				catch
				{
					workerPool.Drop();
					throw;
				}
			}
			finally
			{
				workerPool.End();
			}
		}

		private void LayRunOf(int r)
		{
			int run = _run;
			int num = r * run;
			int num2 = Math.Min(Instances, num + run);
			int num3 = 0;
			int num4 = _laid.Count - 1;
			while (num3 < num4)
			{
				int num5 = num3 + num4 + 1 >> 1;
				if (_laid[num5].Start <= num)
				{
					num3 = num5;
				}
				else
				{
					num4 = num5 - 1;
				}
			}
			for (int i = num3; i < _laid.Count; i++)
			{
				if (num >= num2)
				{
					break;
				}
				Group obj = _laid[i];
				int num6 = Math.Max(num, obj.Start);
				int num7 = Math.Min(num2, obj.Start + obj.Count);
				if (num7 > num6)
				{
					Array.Copy(obj.Items, num6 - obj.Start, _all, num6, num7 - num6);
					if (!obj.Mirrored && AnyMirrored(_all, num6, num7))
					{
						obj.Mirrored = true;
					}
				}
				if (obj.Start + obj.Count >= num2)
				{
					break;
				}
			}
		}

		private unsafe void LayVerified()
		{
			int num = 0;
			List<Group>[] order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item in order[i])
				{
					num += item.Count;
				}
			}
			Array.Clear(_all, 0, Math.Min(num, _all.Length));
			LayNew(((LayChecks & 1) == 0) ? 8192 : 509);
			if (_layCheck.Length < num)
			{
				_layCheck = new InstData[num];
			}
			Array.Copy(_all, _layCheck, num);
			_startCheck.Clear();
			order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item2 in order[i])
				{
					_startCheck.Add(item2.Start);
				}
			}
			LayOld();
			LayChecks++;
			string text = null;
			if (Instances != num)
			{
				text = $"{num} copies vs {Instances} by the old lay";
			}
			int num2 = 0;
			order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item3 in order[i])
				{
					if (text == null && _startCheck[num2] != item3.Start)
					{
						text = $"group {num2} starts at {_startCheck[num2]} vs {item3.Start} by the old lay";
					}
					num2++;
				}
			}
			if (text == null && num > 0)
			{
				fixed (InstData* layCheck = _layCheck)
				{
					fixed (InstData* all = _all)
					{
						int* ptr = (int*)layCheck;
						int* ptr2 = (int*)all;
						for (int j = 0; j < num * 40; j++)
						{
							if (text != null)
							{
								break;
							}
							if (ptr[j] != ptr2[j])
							{
								text = $"copy {j / 40} of {num} differs in word {j % 40}";
							}
						}
					}
				}
			}
			if (text != null)
			{
				LayDiffers++;
				if (LayFirst == null)
				{
					LayFirst = text;
				}
			}
		}

		private void LayOld()
		{
			int num = 0;
			List<Group>[] order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item in order[i])
				{
					num += item.Count;
				}
			}
			Instances = num;
			if (_all.Length < Math.Max(1, num))
			{
				_all = new InstData[Math.Max(64, num * 3 / 2)];
			}
			int num2 = 0;
			order = _order;
			for (int i = 0; i < order.Length; i++)
			{
				foreach (Group item2 in order[i])
				{
					item2.Start = num2;
					Array.Copy(item2.Items, 0, _all, num2, item2.Count);
					item2.Mirrored = AnyMirrored(_all, num2, num2 + item2.Count);
					num2 += item2.Count;
				}
			}
		}

		private static bool AnyMirrored(InstData[] all, int a, int b)
		{
			for (int i = a; i < b; i++)
			{
				ref Matrix4x4 m = ref all[i].M;
				if (m.m00 * (m.m11 * m.m22 - m.m12 * m.m21) - m.m01 * (m.m10 * m.m22 - m.m12 * m.m20) + m.m02 * (m.m10 * m.m21 - m.m11 * m.m20) < 0f)
				{
					return true;
				}
			}
			return false;
		}

		internal void LaidForTest(out InstData[] all, out int count, out Vector4[] palette, out int entries)
		{
			all = _all;
			count = Instances;
			palette = _pal;
			entries = _palCount;
		}

		internal void DrawsForTest(int set, List<(Mesh mesh, int start, int count)> into)
		{
			foreach (Group item in _order[set])
			{
				if (item.Count > 0)
				{
					into.Add((item.Mesh, item.Start, item.Count));
				}
			}
		}

		public void Draw(CommandBuffer cmd, Material mat, int set, int pass, Material culled = null)
		{
			foreach (Group item in _order[set])
			{
				if (item.Count == 0)
				{
					continue;
				}
				bool flag = item.Poses != null;
				if (!flag || pass < 2)
				{
					_props.SetFloat(InstBaseId, item.Start);
					_props.SetFloat(CrowdId, flag ? 1f : 0f);
					if (flag)
					{
						_props.SetTexture(CrowdPosesId, item.Poses);
					}
					Texture2D value;
					Texture2D texture2D = ((GpuModels.ImageOf.Count > 0 && GpuModels.ImageOf.TryGetValue(item.Mesh, out value)) ? value : null);
					_props.SetFloat(ModelTexOnId, (texture2D != null) ? 1f : 0f);
					if (texture2D == null)
					{
						texture2D = GpuModels.White;
					}
					_props.SetTexture(ModelTexId, texture2D);
					_props.SetVector(ModelTexSizeId, new Vector4(texture2D.width, texture2D.height, 1f / (float)texture2D.width, 1f / (float)texture2D.height));
					cmd.DrawMeshInstancedProcedural(item.Mesh, 0, (culled != null && !flag && !item.Mirrored) ? culled : mat, pass, flag ? (item.Count / 2) : item.Count, _props);
					Draws++;
				}
			}
		}

		public int Count(int set)
		{
			int num = 0;
			foreach (Group item in _order[set])
			{
				num += item.Count;
			}
			return num;
		}

		public void Dispose()
		{
			_inst?.Release();
			_palBuf?.Release();
			_inst = (_palBuf = null);
		}
	}
}
