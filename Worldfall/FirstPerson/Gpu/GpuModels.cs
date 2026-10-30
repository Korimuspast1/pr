using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuModels : IDisposable
	{
		private sealed class CrowdBuilt
		{
			public Built B;

			public Mesh Mesh;

			public Texture2D Poses;

			public MeshModel PosesOf;

			public bool OwnPoses;
		}

		private struct CrowdSlot
		{
			public CrowdBuilt C;

			public Matrix4x4 M;

			public InstData E;

			public float ShadowFade;

			public bool Scene;

			public bool Caster;

			public bool Own;

			public int SceneAt;

			public int ShadowAt;

			public int PalAt;

			public GpuInstances.Group G;

			public GpuInstances.Group GS;
		}

		private sealed class CrowdJob : WorkerPool.Job
		{
			public GpuModels Models;

			public override void Run(int chunk, int worker)
			{
				if (Models._crowdStep == 1)
				{
					Models.CrowdCull(chunk);
				}
				else
				{
					Models.CrowdWrite(chunk);
				}
			}
		}

		private sealed class Built
		{
			public Mesh Rest;

			public Mesh[] Parts;

			public Vector4[] Palette;

			public Atlas Atlas;

			public int Slots;

			public int PaletteFrame = -1;

			public int PaletteAt;

			public int[] Versions;

			public bool Live;

			public ulong Merged;

			public ulong Owners;

			public int Seen;

			public Vector4 RestSphere;

			public Vector4[] PartSphere;

			public int GroupFrame = -1;

			public GpuInstances.Group RestScene;

			public GpuInstances.Group RestShadow;

			public GpuInstances.Group[] PartScene;

			public GpuInstances.Group[] PartShadow;

			public MeshModel TwinOf;

			public bool TwinFits;

			public int CallStamp;

			public int CallItems;

			public void ForgetGroups(int frame)
			{
				GroupFrame = frame;
				RestScene = (RestShadow = null);
				Array.Clear(PartScene, 0, PartScene.Length);
				Array.Clear(PartShadow, 0, PartShadow.Length);
			}
		}

		private sealed class Lit
		{
			public Vec3 Pos;

			public float Base;

			public float Top;

			public float InvRise;

			public float Varies;

			public int Frame;
		}

		internal struct ShadowFall
		{
			public bool On;

			public Vector3 Light;

			public float Margin;

			public float Floor;
		}

		internal sealed class RefInstances
		{
			public sealed class Group
			{
				public Mesh Mesh;

				public readonly List<InstData> Items = new List<InstData>();

				public int Start;
			}

			public readonly Dictionary<Mesh, Group>[] Groups = new Dictionary<Mesh, Group>[3]
			{
				new Dictionary<Mesh, Group>(),
				new Dictionary<Mesh, Group>(),
				new Dictionary<Mesh, Group>()
			};

			private readonly List<Group> _pool = new List<Group>();

			private int _poolUsed;

			public readonly List<Vector4> Palette = new List<Vector4>(4096);

			public InstData[] All = new InstData[0];

			public Vector4[] Pal = new Vector4[0];

			public int Frame;

			public int Instances;

			public void Begin()
			{
				Frame++;
				Dictionary<Mesh, Group>[] groups = Groups;
				for (int i = 0; i < groups.Length; i++)
				{
					groups[i].Clear();
				}
				_poolUsed = 0;
				Palette.Clear();
				Instances = 0;
			}

			public int AddPalette(Vector4[] entries, int count)
			{
				int count2 = Palette.Count;
				for (int i = 0; i < count; i++)
				{
					Palette.Add(entries[i]);
				}
				return count2;
			}

			public void Add(int set, Mesh mesh, ref InstData d)
			{
				Dictionary<Mesh, Group> dictionary = Groups[set];
				if (!dictionary.TryGetValue(mesh, out var value))
				{
					if (_poolUsed == _pool.Count)
					{
						_pool.Add(new Group());
					}
					value = _pool[_poolUsed++];
					value.Mesh = mesh;
					value.Items.Clear();
					dictionary[mesh] = value;
				}
				value.Items.Add(d);
			}

			public void Lay()
			{
				int num = 0;
				Dictionary<Mesh, Group>[] groups = Groups;
				for (int i = 0; i < groups.Length; i++)
				{
					foreach (Group value in groups[i].Values)
					{
						num += value.Items.Count;
					}
				}
				Instances = num;
				if (All.Length < Math.Max(1, num))
				{
					All = new InstData[Math.Max(64, num * 3 / 2)];
				}
				int num2 = 0;
				groups = Groups;
				for (int i = 0; i < groups.Length; i++)
				{
					foreach (Group value2 in groups[i].Values)
					{
						value2.Start = num2;
						value2.Items.CopyTo(All, num2);
						num2 += value2.Items.Count;
					}
				}
				int num3 = Math.Max(1, Palette.Count);
				if (Pal.Length < num3)
				{
					Pal = new Vector4[Math.Max(256, num3 * 3 / 2)];
				}
				Palette.CopyTo(Pal, 0);
			}

			public void Draws(int set, List<(Mesh mesh, int start, int count)> into)
			{
				foreach (Group value in Groups[set].Values)
				{
					if (value.Items.Count > 0)
					{
						into.Add((value.Mesh, value.Start, value.Items.Count));
					}
				}
			}

			public int Count(int set)
			{
				int num = 0;
				foreach (Group value in Groups[set].Values)
				{
					num += value.Items.Count;
				}
				return num;
			}
		}

		private sealed class Atlas
		{
			public Texture2D Tex;

			public Vector4[] SlotRect;
		}

		private struct Item
		{
			public MeshInstance M;

			public Built B;

			public int First;

			public int Twin;

			public int Fill;

			public int SceneAt;

			public int ShadowAt;

			public float ShadowFade;

			public bool Scene;

			public bool Caster;

			public bool Rest;

			public bool Any;

			public bool AnyCast;

			public bool Copies;
		}

		private sealed class Scratch
		{
			public Mat3[] Pm = new Mat3[32];

			public Vec3[] Pt = new Vec3[32];

			public int PoseOf = -1;
		}

		private struct PaletteFill
		{
			public int At;

			public int Slots;

			public Vector4[] Base;

			public uint[] Colours;

			public MeshModel Model;
		}

		private sealed class WriteJob : WorkerPool.Job
		{
			public GpuModels Models;

			public override void Run(int chunk, int worker)
			{
				Models.WriteChunk(chunk, worker);
			}
		}

		private sealed class CullJob : WorkerPool.Job
		{
			public GpuModels Models;

			public override void Run(int chunk, int worker)
			{
				Models.CullRunOf(chunk, here: false);
			}
		}

		private sealed class StepsJob : WorkerPool.Job
		{
			public GpuModels Models;

			public override void Run(int chunk, int worker)
			{
				int chunks = Models._chunks;
				if (chunk < chunks)
				{
					Models.PoseChunk(chunk, worker);
				}
				else
				{
					Models.WriteChunk(chunk - chunks, worker);
				}
			}
		}

		private sealed class Added
		{
			private readonly List<(int set, Mesh mesh, bool fresh, int count)> _runs = new List<(int, Mesh, bool, int)>();

			private InstData[] _items = new InstData[1024];

			private Vector4[] _pal = new Vector4[256];

			private int _palCount;

			private int _drawn;

			public int Count;

			public void Take(GpuInstances into, GpuInstances.Mark mark, int drawn)
			{
				_runs.Clear();
				Count = 0;
				_drawn = drawn;
				for (int i = 0; i < 3; i++)
				{
					List<GpuInstances.Group> list = into.OrderForTest(i);
					int num = mark.Order(i);
					for (int j = 0; j < list.Count; j++)
					{
						GpuInstances.Group obj = list[j];
						int num2 = ((j < num) ? obj.Kept : 0);
						int num3 = obj.Count - num2;
						if (num3 > 0 || j >= num)
						{
							_runs.Add((i, obj.Mesh, j >= num, num3));
							if (Count + num3 > _items.Length)
							{
								Array.Resize(ref _items, Math.Max(_items.Length * 2, Count + num3));
							}
							Array.Copy(obj.Items, num2, _items, Count, num3);
							Count += num3;
						}
					}
				}
				_palCount = into.PaletteCount - mark.Palettes;
				if (_pal.Length < _palCount)
				{
					_pal = new Vector4[_palCount * 2];
				}
				Array.Copy(into.PaletteArray, mark.Palettes, _pal, 0, _palCount);
			}

			public unsafe string Differs(Added o)
			{
				if (_drawn != o._drawn)
				{
					return $"drawn {_drawn} vs {o._drawn} by the one pass";
				}
				if (_runs.Count != o._runs.Count)
				{
					return $"{_runs.Count} groups added to vs {o._runs.Count} by the one pass";
				}
				for (int i = 0; i < _runs.Count; i++)
				{
					(int, Mesh, bool, int) tuple = _runs[i];
					(int, Mesh, bool, int) tuple2 = o._runs[i];
					if (tuple.Item1 != tuple2.Item1 || (object)tuple.Item2 != tuple2.Item2 || tuple.Item3 != tuple2.Item3 || tuple.Item4 != tuple2.Item4)
					{
						return string.Format("group {0}: set {1} {2} {3} +{4} vs set {5} {6} {7} +{8} by the one pass", i, tuple.Item1, tuple.Item2?.name, tuple.Item3 ? "new" : "kept", tuple.Item4, tuple2.Item1, tuple2.Item2?.name, tuple2.Item3 ? "new" : "kept", tuple2.Item4);
					}
				}
				if (Count != o.Count)
				{
					return $"{Count} copies vs {o.Count}";
				}
				fixed (InstData* items = _items)
				{
					fixed (InstData* items2 = o._items)
					{
						int* ptr = (int*)items;
						int* ptr2 = (int*)items2;
						for (int j = 0; j < Count; j++)
						{
							for (int k = 0; k < 40; k++)
							{
								if (ptr[j * 40 + k] != ptr2[j * 40 + k])
								{
									return $"copy {j} of {Count} differs in word {k} (0x{ptr[j * 40 + k]:X8} vs 0x{ptr2[j * 40 + k]:X8} by the one pass)";
								}
							}
						}
					}
				}
				if (_palCount != o._palCount)
				{
					return $"palette entries {_palCount} vs {o._palCount}";
				}
				fixed (Vector4* pal = _pal)
				{
					fixed (Vector4* pal2 = o._pal)
					{
						int* ptr3 = (int*)pal;
						int* ptr4 = (int*)pal2;
						for (int l = 0; l < _palCount * 4; l++)
						{
							if (ptr3[l] != ptr4[l])
							{
								return $"palette entry {l / 4} differs";
							}
						}
					}
				}
				return null;
			}
		}

		private readonly Dictionary<MeshModel, CrowdBuilt> _crowdBuilt = new Dictionary<MeshModel, CrowdBuilt>();

		private static int _crowdVersion = int.MinValue;

		private static bool _crowdShader;

		public int CrowdDrawn;

		internal static bool CrowdsPhased = true;

		private const int CrowdRun = 96;

		private CrowdSlot[] _crowd = new CrowdSlot[256];

		private List<CrowdItem> _crowdList;

		private TerrainCache _crowdLand;

		private Func<int, bool> _crowdIsActor;

		private Vector4[] _crowdSides;

		private Vector4[] _crowdPal;

		private bool _crowdShadows;

		private bool _crowdSkip;

		private bool _crowdShadowCull;

		private ShadowFall _crowdFalls;

		private float _crowdReach;

		private float _crowdShadowReach;

		private float _crowdEx;

		private float _crowdEy;

		private float _crowdFx;

		private float _crowdFy;

		private float _crowdScx;

		private float _crowdScy;

		private int _crowdStep;

		private CrowdJob _crowdJob;

		internal static bool SkipUnseen = true;

		private readonly Dictionary<MeshInstance, Lit> _lit = new Dictionary<MeshInstance, Lit>();

		private int _frame;

		internal bool FieldLight;

		private readonly Dictionary<MeshModel, Built> _built = new Dictionary<MeshModel, Built>();

		private Mat3[] _pm = new Mat3[32];

		private Vec3[] _pt = new Vec3[32];

		public int Drawn;

		private Vector3[] _fillPos = new Vector3[0];

		private Vector3[] _fillNrm = new Vector3[0];

		private Vector3[] _fillFace = new Vector3[0];

		private Vector4[] _fillUv = new Vector4[0];

		private Vector3[] _fillTuv = new Vector3[0];

		private Vector4[] _fillRect = new Vector4[0];

		private Atlas _atlas;

		private Matrix4x4[] _mm = new Matrix4x4[32];

		private bool[] _seen = new bool[32];

		private bool[] _casts = new bool[32];

		internal static bool MergeLooks = true;

		private static int _piecesVersion = int.MinValue;

		private static bool _piecesShader;

		private readonly Dictionary<MeshInstance, Lit> _refLit = new Dictionary<MeshInstance, Lit>();

		private readonly Vector4[] _palette = new Vector4[64];

		private readonly Dictionary<Built, int> _refPalFrame = new Dictionary<Built, int>();

		private readonly Dictionary<Built, int> _refPalAt = new Dictionary<Built, int>();

		private int _refFrame;

		internal int RefMode;

		internal static readonly Dictionary<Mesh, Texture2D> ImageOf = new Dictionary<Mesh, Texture2D>();

		private static Texture2D _white;

		private const int AtlasPad = 2;

		private const int AtlasMax = 4096;

		internal static bool KeepAtlasReadable;

		private static bool _texturedShader;

		private static int _texturedVersion = -1;

		internal static bool Phased = true;

		internal static bool PoseTwins = true;

		internal static bool Verify;

		internal static int VerifyRuns;

		internal static int VerifyDiffers;

		internal static int VerifyCopies;

		internal static int VerifyTwins;

		internal static string VerifyFirst;

		internal static readonly int[] VerifyMixes = new int[8];

		private Item[] _items = new Item[256];

		private InstData[] _desc = new InstData[256];

		private Matrix4x4[] _mats = new Matrix4x4[1024];

		private byte[] _flags = new byte[1024];

		private int[] _sceneAt = new int[1024];

		private int[] _shadowAt = new int[1024];

		private int[] _chunkStart = new int[64];

		private int _itemCount;

		private int _chunks;

		private int _twinItems;

		private Vector4[] _jobSides;

		private bool _jobShadowCull;

		private ShadowFall _jobFalls;

		private Vector4[] _jobPalettes;

		private Scratch[] _scratch;

		private PaletteFill[] _fills = new PaletteFill[64];

		private int _fillCount;

		private bool _fillLater;

		private WriteJob _writeJob;

		private const int ChunkParts = 160;

		private static WorkerPool _pool;

		private static bool _poolTried;

		private static WorkerPool _pending;

		private static int _pendingLooks;

		private static long _pendingNext;

		private const int PendingLooks = 20;

		private static readonly bool TimeSteps = Environment.GetEnvironmentVariable("FIRSTPERSON_GPU_STEPS") == "1";

		private static long _stepsSince;

		private static double _step1;

		private static double _step23;

		private static double _step3;

		private static double _step4;

		private static double _stepCalls;

		private int _itemsHigh;

		private int _foundHigh;

		private int _fillsHigh;

		private List<MeshInstance> _cullList;

		private float _cullEx;

		private float _cullEy;

		private float _cullFx;

		private float _cullFy;

		private float _cullReach;

		private float _cullScx;

		private float _cullScy;

		private float _cullShadowReach;

		private bool _cullShadows;

		private bool _cullSkip;

		private byte[] _cull = new byte[1024];

		private Built[] _found = new Built[1024];

		private const int CullRun = 256;

		private static int _stamp;

		private int _jobStamp;

		internal static bool WriteBehind = true;

		internal static bool CullOnWorkers = true;

		private const int WriteStayers = 2;

		private CullJob _cullJob;

		private StepsJob _stepsJob;

		private static Mat3[] _checkPm = new Mat3[8];

		private static Mat3[] _refPm = new Mat3[8];

		private static Vec3[] _checkPt = new Vec3[8];

		private static Vec3[] _refPt = new Vec3[8];

		private Added _verifyNew;

		private Added _verifyOld;

		internal static Texture2D White
		{
			get
			{
				if (_white == null)
				{
					_white = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
					{
						name = "FirstPersonWhite",
						filterMode = FilterMode.Point
					};
					_white.SetPixel(0, 0, Color.white);
					_white.Apply(updateMipmaps: false, makeNoLongerReadable: true);
				}
				return _white;
			}
		}

		internal static WorkerPool Pool
		{
			get
			{
				if (_pending != null)
				{
					LookAtPending();
				}
				if (_poolTried)
				{
					return _pool;
				}
				_poolTried = true;
				int val = Math.Min(5, Environment.ProcessorCount / 2 - 1);
				try
				{
					string environmentVariable = Environment.GetEnvironmentVariable("FIRSTPERSON_GPU_WORKERS");
					if (!string.IsNullOrEmpty(environmentVariable) && int.TryParse(environmentVariable, out var result))
					{
						val = Math.Max(0, Math.Min(16, result));
					}
				}
				catch
				{
				}
				val = Math.Max(0, val);
				try
				{
					WorkerPool workerPool = new WorkerPool(val, "FirstPerson copies");
					if (!workerPool.WaitStarted(500))
					{
						Log.Info($"gpu copies: {val} worker threads not started within 0.5 s ({Environment.ProcessorCount} hardware threads): copies gathered on the game thread until they are");
						_pending = workerPool;
						_pendingLooks = 0;
						_pendingNext = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
						_pool = new WorkerPool(0, "FirstPerson copies");
					}
					else
					{
						_pool = Agreed(workerPool);
					}
				}
				catch (Exception ex)
				{
					Log.Error("gpu copies: no worker threads: " + ex.Message);
					_pool = null;
				}
				return _pool;
			}
		}

		internal static int WorkerCount => Pool?.Workers ?? 0;

		internal static bool ShaderDrawsCrowds()
		{
			if (_crowdVersion != GpuShaders.Version)
			{
				_crowdVersion = GpuShaders.Version;
				Shader shader = GpuShaders.Get("FirstPerson/Model");
				_crowdShader = shader != null && shader.FindPropertyIndex("_FP_Crowds") >= 0;
				Log.Info("gpu: far crowds " + (_crowdShader ? "can be posed on the card" : "posed on the CPU (the shader loaded has no crowd poses)"));
			}
			return _crowdShader;
		}

		private CrowdBuilt GetCrowd(MeshModel model, MeshModel skeleton = null)
		{
			if (_crowdBuilt.TryGetValue(model, out var value))
			{
				value.B = Get(model);
				if (skeleton != null && skeleton != model && value.PosesOf != skeleton)
				{
					value.Poses = GetCrowd(skeleton).Poses;
					value.PosesOf = skeleton;
				}
				return value;
			}
			value = new CrowdBuilt
			{
				B = Get(model)
			};
			_atlas = value.B.Atlas;
			Fill(model, 0, model.Parts.Length, out var pos, out var nrm, out var uv, out var face);
			int num = 0;
			for (int i = 0; i < model.Parts.Length; i++)
			{
				int num2 = model.Parts[i].TriangleCount * 3;
				while (num2 > 0)
				{
					uv[num].w = i + 1;
					num2--;
					num++;
				}
			}
			int[] array = new int[pos.Length];
			for (int j = 0; j < array.Length; j++)
			{
				array[j] = j;
			}
			Mesh mesh = new Mesh
			{
				name = "FirstPersonCrowd_" + model.Name,
				indexFormat = ((array.Length > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16)
			};
			mesh.vertices = pos;
			mesh.normals = nrm;
			mesh.SetUVs(0, uv);
			mesh.SetUVs(1, face);
			if (value.B.Atlas != null)
			{
				mesh.SetUVs(2, _fillTuv);
				mesh.SetUVs(3, _fillRect);
				ImageOf[mesh] = value.B.Atlas.Tex;
			}
			mesh.SetIndices(array, MeshTopology.Triangles, 0, calculateBounds: false);
			mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
			mesh.UploadMeshData(markNoLongerReadable: true);
			value.Mesh = mesh;
			_crowdBuilt[model] = value;
			if (skeleton != null && skeleton != model)
			{
				value.Poses = GetCrowd(skeleton).Poses;
				value.PosesOf = skeleton;
				return value;
			}
			float[] array2 = CrowdPoses.For(model, Models.Rig(model));
			ushort[] array3 = new ushort[array2.Length];
			for (int k = 0; k < array2.Length; k++)
			{
				array3[k] = Mathf.FloatToHalf(array2[k]);
			}
			Texture2D texture2D = new Texture2D(CrowdPoses.Width(model), CrowdPoses.Rows, TextureFormat.RGBAHalf, mipChain: false, linear: true)
			{
				name = "FirstPersonCrowdPoses_" + model.Name,
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			texture2D.SetPixelData(array3, 0);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			value.Poses = texture2D;
			value.OwnPoses = true;
			value.PosesOf = model;
			return value;
		}

		public void CollectCrowds(GpuInstances into, List<CrowdItem> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach, Vector4[] sides, ShadowFall falls)
		{
			CrowdDrawn = 0;
			if (list == null || list.Count == 0)
			{
				return;
			}
			if (CrowdsPhased && list.Count >= 192)
			{
				CollectCrowdsPhased(into, list, land, view, reach, isActor, shadows, shadowCentre, shadowReach, sides, falls);
				return;
			}
			bool skipUnseen = SkipUnseen;
			if (!skipUnseen)
			{
				sides = null;
			}
			bool flag = sides != null && shadows && falls.On && falls.Light.z > 0.001f;
			_ = into.Frame;
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			float x = view.X;
			float y = view.Y;
			float x2 = shadowCentre.x;
			float y2 = shadowCentre.y;
			InstData d = default(InstData);
			InstData instData = default(InstData);
			int i = 0;
			for (int count = list.Count; i < count; i++)
			{
				CrowdItem crowdItem = list[i];
				MeshInstance body = crowdItem.Body;
				if (body == null || body.Model == null || body.Fade <= 0f)
				{
					continue;
				}
				MeshModel model = body.Model;
				CrowdBuilt crowd = GetCrowd(model, crowdItem.Skeleton);
				Built b = crowd.B;
				b.Seen = _frame;
				Mat3 basis = body.Basis;
				Vec3 pos = body.Position;
				Placed(b.RestSphere, ref basis, ref pos, out var x3, out var y3, out var z, out var r);
				r *= 1.5f;
				float num3 = (x3 - x) * num + (y3 - y) * num2;
				bool flag2 = num3 >= 0f - r && num3 - r <= reach && (sides == null || !Beyond(sides, x3, y3, z, r));
				float num4 = (body.FadeKeepsShadow ? 1f : body.Fade);
				bool flag3 = false;
				if (shadows && !body.NoShadow && !body.Emissive && (!skipUnseen || !(num4 < 0.5f)))
				{
					float num5 = pos.X - x2;
					float num6 = pos.Y - y2;
					float num7 = shadowReach + r;
					flag3 = num5 * num5 + num6 * num6 <= num7 * num7 && (!flag || !Unlit(sides, x3, y3, z, r, ref falls));
				}
				if (flag2 || flag3)
				{
					Matrix4x4 mm = Matrix(basis, pos);
					Pieces(ref mm, body.Hidden);
					instData = new InstData
					{
						M = 
						{
							m00 = crowdItem.A,
							m01 = crowdItem.B,
							m02 = crowdItem.W
						}
					};
					if (flag2)
					{
						Describe(ref d, body, model, b, land, overlay: false, isActor, into);
						d.M = mm;
						GpuInstances.Group obj = into.For(1, crowd.Mesh);
						obj.Poses = crowd.Poses;
						GpuInstances.Next(obj) = d;
						GpuInstances.Next(obj) = instData;
						CrowdDrawn++;
						Drawn++;
					}
					if (flag3)
					{
						GpuInstances.Group obj2 = into.For(0, crowd.Mesh);
						obj2.Poses = crowd.Poses;
						ShadowCopy(ref GpuInstances.Next(obj2), ref mm, new Vector4(num4, 0f, 0f, 0f));
						GpuInstances.Next(obj2) = instData;
					}
				}
			}
		}

		private void DisposeCrowds()
		{
			foreach (CrowdBuilt value in _crowdBuilt.Values)
			{
				if (value.Mesh != null)
				{
					Unregister(value.Mesh);
					UnityEngine.Object.Destroy(value.Mesh);
				}
				if (value.Poses != null && value.OwnPoses)
				{
					UnityEngine.Object.Destroy(value.Poses);
				}
			}
			_crowdBuilt.Clear();
		}

		private void CollectCrowdsPhased(GpuInstances into, List<CrowdItem> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach, Vector4[] sides, ShadowFall falls)
		{
			int count = list.Count;
			if (_crowd.Length < count)
			{
				_crowd = new CrowdSlot[Math.Max(count, _crowd.Length * 2)];
			}
			_crowdList = list;
			_crowdLand = land;
			_crowdIsActor = isActor;
			_crowdSkip = SkipUnseen;
			_crowdSides = (_crowdSkip ? sides : null);
			_crowdShadows = shadows;
			_crowdShadowCull = _crowdSides != null && shadows && falls.On && falls.Light.z > 0.001f;
			_crowdFalls = falls;
			_crowdReach = reach;
			_crowdShadowReach = shadowReach;
			_crowdEx = view.X;
			_crowdEy = view.Y;
			_crowdFx = Mathf.Cos(view.Yaw);
			_crowdFy = Mathf.Sin(view.Yaw);
			_crowdScx = shadowCentre.x;
			_crowdScy = shadowCentre.y;
			for (int i = 0; i < count; i++)
			{
				CrowdItem crowdItem = list[i];
				MeshInstance body = crowdItem.Body;
				if (body == null || body.Model == null || body.Fade <= 0f)
				{
					_crowd[i].C = null;
					continue;
				}
				CrowdBuilt crowd = GetCrowd(body.Model, crowdItem.Skeleton);
				crowd.B.Seen = _frame;
				_crowd[i].C = crowd;
			}
			int runs = (count + 96 - 1) / 96;
			RunCrowdStep(1, runs);
			int frame = into.Frame;
			for (int j = 0; j < count; j++)
			{
				ref CrowdSlot reference = ref _crowd[j];
				if (reference.C == null || (!reference.Scene && !reference.Caster))
				{
					continue;
				}
				if (reference.Scene)
				{
					MeshInstance body2 = list[j].Body;
					Built b = reference.C.B;
					int slots = b.Slots;
					bool flag = true;
					if (body2.Palette != null)
					{
						for (int k = 0; k < body2.Palette.Length && k < slots; k++)
						{
							if (body2.Palette[k] != 0)
							{
								flag = false;
								break;
							}
						}
					}
					reference.Own = flag;
					if (flag)
					{
						if (b.PaletteFrame != frame)
						{
							b.PaletteAt = into.AddPalette(b.Palette, slots);
							b.PaletteFrame = frame;
						}
						reference.PalAt = b.PaletteAt;
					}
					else
					{
						reference.PalAt = into.ReservePalette(slots, out var _);
					}
					GpuInstances.Group obj = into.For(1, reference.C.Mesh);
					obj.Poses = reference.C.Poses;
					reference.G = obj;
					reference.SceneAt = GpuInstances.Reserve(obj);
					GpuInstances.Reserve(obj);
					CrowdDrawn++;
					Drawn++;
				}
				if (reference.Caster)
				{
					GpuInstances.Group obj2 = into.For(0, reference.C.Mesh);
					obj2.Poses = reference.C.Poses;
					reference.GS = obj2;
					reference.ShadowAt = GpuInstances.Reserve(obj2);
					GpuInstances.Reserve(obj2);
				}
			}
			into.ReservePalette(0, out _crowdPal);
			RunCrowdStep(2, runs);
			_crowdList = null;
			_crowdLand = null;
			_crowdIsActor = null;
			_crowdPal = null;
		}

		private void RunCrowdStep(int step, int runs)
		{
			_crowdStep = step;
			WorkerPool pool = Pool;
			if (pool != null && runs > 1 && pool.TryBegin())
			{
				try
				{
					CrowdJob crowdJob = _crowdJob;
					if (crowdJob == null)
					{
						CrowdJob obj = new CrowdJob
						{
							Models = this
						};
						CrowdJob crowdJob2 = obj;
						_crowdJob = obj;
						crowdJob = crowdJob2;
					}
					pool.Start(crowdJob, runs);
					pool.Finish();
					return;
				}
				finally
				{
					pool.End();
				}
			}
			for (int i = 0; i < runs; i++)
			{
				if (step == 1)
				{
					CrowdCull(i);
				}
				else
				{
					CrowdWrite(i);
				}
			}
		}

		private void CrowdCull(int run)
		{
			List<CrowdItem> crowdList = _crowdList;
			int num = Math.Min(crowdList.Count, (run + 1) * 96);
			Vector4[] crowdSides = _crowdSides;
			bool crowdSkip = _crowdSkip;
			bool crowdShadows = _crowdShadows;
			bool crowdShadowCull = _crowdShadowCull;
			ShadowFall falls = _crowdFalls;
			float crowdEx = _crowdEx;
			float crowdEy = _crowdEy;
			float crowdFx = _crowdFx;
			float crowdFy = _crowdFy;
			float crowdScx = _crowdScx;
			float crowdScy = _crowdScy;
			float crowdReach = _crowdReach;
			float crowdShadowReach = _crowdShadowReach;
			for (int i = run * 96; i < num; i++)
			{
				ref CrowdSlot reference = ref _crowd[i];
				reference.Scene = (reference.Caster = false);
				if (reference.C != null)
				{
					CrowdItem crowdItem = crowdList[i];
					MeshInstance body = crowdItem.Body;
					Built b = reference.C.B;
					Mat3 basis = body.Basis;
					Vec3 pos = body.Position;
					Placed(b.RestSphere, ref basis, ref pos, out var x, out var y, out var z, out var r);
					r *= 1.5f;
					float num2 = (x - crowdEx) * crowdFx + (y - crowdEy) * crowdFy;
					bool flag = num2 >= 0f - r && num2 - r <= crowdReach && (crowdSides == null || !Beyond(crowdSides, x, y, z, r));
					float num3 = (body.FadeKeepsShadow ? 1f : body.Fade);
					bool flag2 = false;
					if (crowdShadows && !body.NoShadow && !body.Emissive && (!crowdSkip || !(num3 < 0.5f)))
					{
						float num4 = pos.X - crowdScx;
						float num5 = pos.Y - crowdScy;
						float num6 = crowdShadowReach + r;
						flag2 = num4 * num4 + num5 * num5 <= num6 * num6 && (!crowdShadowCull || !Unlit(crowdSides, x, y, z, r, ref falls));
					}
					if (flag || flag2)
					{
						Matrix4x4 mm = Matrix(basis, pos);
						Pieces(ref mm, body.Hidden);
						InstData e = new InstData
						{
							M = 
							{
								m00 = crowdItem.A,
								m01 = crowdItem.B,
								m02 = crowdItem.W
							}
						};
						reference.M = mm;
						reference.E = e;
						reference.ShadowFade = num3;
						reference.Scene = flag;
						reference.Caster = flag2;
					}
				}
			}
		}

		private void CrowdWrite(int run)
		{
			List<CrowdItem> crowdList = _crowdList;
			int num = Math.Min(crowdList.Count, (run + 1) * 96);
			Vector4[] crowdPal = _crowdPal;
			InstData d = default(InstData);
			for (int i = run * 96; i < num; i++)
			{
				ref CrowdSlot reference = ref _crowd[i];
				if (reference.C == null)
				{
					continue;
				}
				if (reference.Scene)
				{
					MeshInstance body = crowdList[i].Body;
					Built b = reference.C.B;
					if (!reference.Own)
					{
						int palAt = reference.PalAt;
						int slots = b.Slots;
						MeshModel model = body.Model;
						Array.Copy(b.Palette, 0, crowdPal, palAt, slots);
						uint[] palette = body.Palette;
						for (int j = 0; j < palette.Length && j < slots; j++)
						{
							if (palette[j] != 0)
							{
								crowdPal[palAt + j] = Colour(palette[j], model.SlotKind[j], model.SlotFlags[j]);
							}
						}
					}
					DescribeCrowd(ref d, body, body.Model, reference.PalAt, _crowdLand, _crowdIsActor);
					d.M = reference.M;
					reference.G.Items[reference.SceneAt] = d;
					reference.G.Items[reference.SceneAt + 1] = reference.E;
				}
				if (reference.Caster)
				{
					ShadowCopy(ref reference.GS.Items[reference.ShadowAt], ref reference.M, new Vector4(reference.ShadowFade, 0f, 0f, 0f));
					reference.GS.Items[reference.ShadowAt + 1] = reference.E;
				}
			}
		}

		private void DescribeCrowd(ref InstData d, MeshInstance m, MeshModel model, int palAt, TerrainCache land, Func<int, bool> isActor)
		{
			d.Pal = new Vector4(palAt, m.Snow, m.Sway, m.SwayInvTop);
			uint flash = m.Flash;
			d.Flash = new Vector4((float)(flash & 0xFF) / 255f, (float)((flash >> 8) & 0xFF) / 255f, (float)((flash >> 16) & 0xFF) / 255f, (float)(flash >> 24) / 255f);
			uint multiply = m.Multiply;
			d.Mult = ((multiply != 0) ? new Vector4((float)(multiply & 0xFF) / 255f, (float)((multiply >> 8) & 0xFF) / 255f, (float)((multiply >> 16) & 0xFF) / 255f, 1f) : Vector4.zero);
			d.Misc = new Vector4(Mathf.Clamp01(m.Fade), (m.DissolveScale > 0f) ? (1f / m.DissolveScale) : 0f, m.Glow, m.Emissive ? 1f : 0f);
			if (m.Cloud != null)
			{
				d.Misc = new Vector4(Mathf.Clamp01(m.Cloud.Opacity), 0f, 0f, 0f);
				d.Flash = new Vector4(0.95f, 0.95f, 1f, Mathf.Clamp01(0.3f + m.Cloud.SelfLight));
			}
			float y = ((isActor != null && isActor(m.Tag)) ? 4f : 3f);
			float x = 1f;
			float y2 = 1f;
			float minusReach = 1f;
			float z = 0f;
			if (FieldLight)
			{
				if (!m.Emissive && m.Cloud == null)
				{
					BesideLight(m, model, out x, out y2, out minusReach);
				}
			}
			else if (!m.Emissive && m.Cloud == null && land != null && land.CastShadows && land.Width > 0)
			{
				float num = Mathf.Max(0.1f, m.Basis.C2.Z * model.Max.Z);
				minusReach = 1f / num;
				float x2 = m.Position.X;
				float y3 = m.Position.Y;
				y2 = land.LightAt(x2, y3, m.Position.Z + num);
				float num2 = Mathf.Max(Len(m.Basis.C0), Len(m.Basis.C1)) * Mathf.Max(Mathf.Max(0f - model.Min.X, model.Max.X), Mathf.Max(0f - model.Min.Y, model.Max.Y));
				float num3 = land.GroundLight(x2, y3);
				float a = land.GroundLight(x2 - num2, y3 - num2);
				float b = land.GroundLight(x2 + num2, y3 - num2);
				float a2 = land.GroundLight(x2 - num2, y3 + num2);
				float b2 = land.GroundLight(x2 + num2, y3 + num2);
				float num4 = Mathf.Min(num3, Mathf.Min(Mathf.Min(a, b), Mathf.Min(a2, b2)));
				float num5 = Mathf.Max(num3, Mathf.Max(Mathf.Max(a, b), Mathf.Max(a2, b2)));
				x = num3;
				z = ((num5 - num4 > 0.01f) ? 1f : 0f);
			}
			d.Tag = new Vector4(m.Tag, y, x, y2);
			d.Ground = new Vector4(m.Position.Z, minusReach, z, 0f);
		}

		private Built Get(MeshModel model)
		{
			if (_built.TryGetValue(model, out var value))
			{
				return Touch(value, model);
			}
			int num = Math.Min(64, model.SlotColor.Length);
			value = new Built
			{
				Parts = new Mesh[model.Parts.Length],
				Palette = new Vector4[Math.Max(1, num)],
				Slots = num,
				Versions = new int[model.Parts.Length],
				Seen = _frame
			};
			value.Atlas = BuildAtlas(model);
			_atlas = value.Atlas;
			for (int i = 0; i < model.Parts.Length; i++)
			{
				value.Versions[i] = model.Parts[i].Version;
				value.Live |= value.Versions[i] != 0;
				value.Parts[i] = BuildMesh(model, i, i + 1, value.Versions[i] != 0);
				Register(value.Parts[i], value);
			}
			value.Rest = ((model.Parts.Length == 1 && !value.Live) ? value.Parts[0] : (value.Live ? null : BuildMesh(model, 0, model.Parts.Length, live: false)));
			Register(value.Rest, value);
			for (int j = 0; j < num; j++)
			{
				value.Palette[j] = Colour(model.SlotColor[j], model.SlotKind[j], model.SlotFlags[j]);
			}
			value.RestSphere = Sphere(model, 0, model.Parts.Length);
			value.PartSphere = new Vector4[model.Parts.Length];
			for (int k = 0; k < model.Parts.Length; k++)
			{
				value.PartSphere[k] = Sphere(model, k, k + 1);
			}
			MergePieces(value, model);
			value.PartScene = new GpuInstances.Group[model.Parts.Length];
			value.PartShadow = new GpuInstances.Group[model.Parts.Length];
			_built[model] = value;
			return value;
		}

		private Built Touch(Built b, MeshModel model)
		{
			if (b.Seen != _frame)
			{
				b.Seen = _frame;
				for (int i = 0; i < model.Parts.Length; i++)
				{
					if (model.Parts[i].Version != b.Versions[i])
					{
						Refresh(b, model, i);
					}
				}
			}
			return b;
		}

		private static Vector4 Sphere(MeshModel model, int p0, int p1)
		{
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MinValue;
			float num6 = float.MinValue;
			for (int i = p0; i < p1; i++)
			{
				float[] pos = model.Parts[i].Pos;
				for (int j = 0; j + 2 < pos.Length; j += 3)
				{
					num = Math.Min(num, pos[j]);
					num4 = Math.Max(num4, pos[j]);
					num2 = Math.Min(num2, pos[j + 1]);
					num5 = Math.Max(num5, pos[j + 1]);
					num3 = Math.Min(num3, pos[j + 2]);
					num6 = Math.Max(num6, pos[j + 2]);
				}
			}
			if (num > num4)
			{
				return new Vector4(0f, 0f, 0f, 0f);
			}
			double num7 = ((double)num + (double)num4) * 0.5;
			double num8 = ((double)num2 + (double)num5) * 0.5;
			double num9 = ((double)num3 + (double)num6) * 0.5;
			double num10 = 0.0;
			for (int k = p0; k < p1; k++)
			{
				float[] pos2 = model.Parts[k].Pos;
				for (int l = 0; l + 2 < pos2.Length; l += 3)
				{
					double num11 = (double)pos2[l] - num7;
					double num12 = (double)pos2[l + 1] - num8;
					double num13 = (double)pos2[l + 2] - num9;
					num10 = Math.Max(num10, num11 * num11 + num12 * num12 + num13 * num13);
				}
			}
			return new Vector4((float)num7, (float)num8, (float)num9, (float)(Math.Sqrt(num10) * 1.0001 + 0.0001));
		}

		private void Refresh(Built b, MeshModel model, int p)
		{
			b.Versions[p] = model.Parts[p].Version;
			b.PartSphere[p] = Sphere(model, p, p + 1);
			b.GroupFrame = -1;
			Mesh mesh = b.Parts[p];
			_atlas = b.Atlas;
			if (mesh != null && mesh.isReadable && mesh.vertexCount == model.Parts[p].TriangleCount * 3)
			{
				Fill(model, p, p + 1, out var pos, out var nrm, out var uv, out var face);
				mesh.vertices = pos;
				mesh.normals = nrm;
				mesh.SetUVs(0, uv);
				mesh.SetUVs(1, face);
				if (b.Atlas != null)
				{
					mesh.SetUVs(2, _fillTuv);
					mesh.SetUVs(3, _fillRect);
				}
			}
			else
			{
				if (mesh != null && mesh != b.Rest)
				{
					Unregister(mesh);
					UnityEngine.Object.Destroy(mesh);
				}
				b.Parts[p] = BuildMesh(model, p, p + 1, live: true);
				Register(b.Parts[p], b);
			}
			if (!b.Live)
			{
				b.Live = true;
				if (b.Rest != null && Array.IndexOf(b.Parts, b.Rest) < 0)
				{
					UnityEngine.Object.Destroy(b.Rest);
				}
				b.Rest = null;
			}
		}

		private void ForgetLive()
		{
			List<MeshModel> list = null;
			foreach (KeyValuePair<MeshModel, Built> item in _built)
			{
				if (item.Value.Live && _frame - item.Value.Seen > 600)
				{
					(list ?? (list = new List<MeshModel>())).Add(item.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (MeshModel item2 in list)
			{
				Built built = _built[item2];
				Mesh[] parts = built.Parts;
				foreach (Mesh mesh in parts)
				{
					if (mesh != null)
					{
						Unregister(mesh);
						UnityEngine.Object.Destroy(mesh);
					}
				}
				if (built.Rest != null && Array.IndexOf(built.Parts, built.Rest) < 0)
				{
					Unregister(built.Rest);
					UnityEngine.Object.Destroy(built.Rest);
				}
				DestroyAtlas(built);
				_built.Remove(item2);
			}
		}

		public void Forget(MeshModel model)
		{
			if (model == null || !_built.TryGetValue(model, out var value))
			{
				return;
			}
			Mesh[] parts = value.Parts;
			foreach (Mesh mesh in parts)
			{
				if (mesh != null)
				{
					Unregister(mesh);
					UnityEngine.Object.Destroy(mesh);
				}
			}
			if (value.Rest != null && Array.IndexOf(value.Parts, value.Rest) < 0)
			{
				Unregister(value.Rest);
				UnityEngine.Object.Destroy(value.Rest);
			}
			DestroyAtlas(value);
			_built.Remove(model);
		}

		private static Vector4 Colour(uint c, byte kind, byte flags)
		{
			return new Vector4((float)(c & 0xFF) / 255f, (float)((c >> 8) & 0xFF) / 255f, (float)((c >> 16) & 0xFF) / 255f, (float)(int)kind + (float)(int)flags * 32f);
		}

		private void Fill(MeshModel model, int p0, int p1, out Vector3[] pos, out Vector3[] nrm, out Vector4[] uv, out Vector3[] face)
		{
			int num = 0;
			for (int i = p0; i < p1; i++)
			{
				num += model.Parts[i].TriangleCount;
			}
			int num2 = num * 3;
			if (_fillPos.Length != num2)
			{
				_fillPos = new Vector3[num2];
				_fillNrm = new Vector3[num2];
				_fillFace = new Vector3[num2];
				_fillUv = new Vector4[num2];
			}
			pos = _fillPos;
			nrm = _fillNrm;
			uv = _fillUv;
			face = _fillFace;
			Atlas atlas = _atlas;
			Vector3[] array = null;
			Vector4[] array2 = null;
			if (atlas != null)
			{
				if (_fillTuv.Length != num2)
				{
					_fillTuv = new Vector3[num2];
					_fillRect = new Vector4[num2];
				}
				array = _fillTuv;
				array2 = _fillRect;
			}
			int num3 = 0;
			for (int j = p0; j < p1; j++)
			{
				MeshPart meshPart = model.Parts[j];
				float[] pos2 = meshPart.Pos;
				float[] nrm2 = meshPart.Nrm;
				float[] uv2 = meshPart.Uv;
				for (int k = 0; k < meshPart.TriangleCount; k++)
				{
					int num4 = meshPart.Tri[k * 3] * 3;
					int num5 = meshPart.Tri[k * 3 + 1] * 3;
					int num6 = meshPart.Tri[k * 3 + 2] * 3;
					Vector3 vector = new Vector3(pos2[num4], pos2[num4 + 1], pos2[num4 + 2]);
					Vector3 vector2 = Vector3.Cross(new Vector3(pos2[num5], pos2[num5 + 1], pos2[num5 + 2]) - vector, new Vector3(pos2[num6], pos2[num6 + 1], pos2[num6 + 2]) - vector);
					float magnitude = vector2.magnitude;
					vector2 = ((magnitude > 1E-12f) ? (vector2 / magnitude) : Vector3.zero);
					int num7 = meshPart.Slot[k];
					Vector4 vector3 = new Vector4(Math.Min(63, num7), (int)meshPart.Axis[k], (num7 < model.SlotFlags.Length) ? model.SlotFlags[num7] : 0, 0f);
					Vector4 vector4 = ((atlas != null && uv2 != null && num7 < atlas.SlotRect.Length) ? atlas.SlotRect[num7] : Vector4.zero);
					for (int l = 0; l < 3; l++)
					{
						int num8 = meshPart.Tri[k * 3 + l];
						int num9 = num8 * 3;
						pos[num3] = new Vector3(pos2[num9], pos2[num9 + 1], pos2[num9 + 2]);
						nrm[num3] = new Vector3(nrm2[num9], nrm2[num9 + 1], nrm2[num9 + 2]);
						uv[num3] = vector3;
						face[num3] = vector2;
						if (array != null)
						{
							array[num3] = ((uv2 != null) ? new Vector3(uv2[num8 * 2], uv2[num8 * 2 + 1], j) : new Vector3(0f, 0f, j));
							array2[num3] = vector4;
						}
						num3++;
					}
				}
			}
		}

		private Mesh BuildMesh(MeshModel model, int p0, int p1, bool live)
		{
			Fill(model, p0, p1, out var pos, out var nrm, out var uv, out var face);
			int num = pos.Length;
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = i;
			}
			Mesh mesh = new Mesh
			{
				name = "FirstPersonModel_" + model.Name,
				indexFormat = ((num > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16)
			};
			if (live)
			{
				mesh.MarkDynamic();
			}
			mesh.vertices = pos;
			mesh.normals = nrm;
			mesh.SetUVs(0, uv);
			mesh.SetUVs(1, face);
			if (_atlas != null)
			{
				mesh.SetUVs(2, _fillTuv);
				mesh.SetUVs(3, _fillRect);
			}
			mesh.SetIndices(array, MeshTopology.Triangles, 0, calculateBounds: false);
			mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
			mesh.UploadMeshData(!live);
			return mesh;
		}

		public void Collect(GpuInstances into, int set, List<MeshInstance> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, Vector2 shadowCentre = default(Vector2), float shadowReach = 0f)
		{
			switch (set)
			{
			case 1:
				CollectBoth(into, list, land, view, reach, isActor, shadows: false, shadowCentre, shadowReach, null, default(ShadowFall));
				break;
			case 2:
				CollectOne(into, list, land, view, isActor);
				break;
			}
		}

		public void CollectBoth(GpuInstances into, List<MeshInstance> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach, Vector4[] sides, ShadowFall falls)
		{
			Drawn = 0;
			_frame++;
			if (_lit.Count > Math.Max(20000, list.Count * 2))
			{
				_lit.Clear();
			}
			if (_frame % 600 == 0)
			{
				ForgetLive();
			}
			if (Phased)
			{
				CollectPhased(into, list, land, view, reach, isActor, shadows, shadowCentre, shadowReach, sides, falls);
			}
			else
			{
				CollectBothOld(into, list, land, view, reach, isActor, shadows, shadowCentre, shadowReach, sides, falls);
			}
		}

		private void CollectBothOld(GpuInstances into, List<MeshInstance> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach, Vector4[] sides, ShadowFall falls)
		{
			bool skipUnseen = SkipUnseen;
			if (!skipUnseen)
			{
				sides = null;
			}
			bool flag = sides != null && shadows && falls.On && falls.Light.z > 0.001f;
			int frame = into.Frame;
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			float x = view.X;
			float y = view.Y;
			float x2 = shadowCentre.x;
			float y2 = shadowCentre.y;
			InstData d = default(InstData);
			int i = 0;
			for (int count = list.Count; i < count; i++)
			{
				MeshInstance meshInstance = list[i];
				if (meshInstance == null || meshInstance.Model == null || meshInstance.Fade <= 0f)
				{
					continue;
				}
				MeshModel model = meshInstance.Model;
				Mat3 basis = meshInstance.Basis;
				float num3 = Math.Max(Math.Max(Len(basis.C0), Len(basis.C1)), Len(basis.C2)) * Math.Max(MaxAbs(model.Min), MaxAbs(model.Max)) + 0.5f;
				Vec3 pos = meshInstance.Position;
				bool flag2 = false;
				bool flag3 = false;
				if (!meshInstance.ShadowOnly)
				{
					float num4 = (pos.X - x) * num + (pos.Y - y) * num2;
					flag2 = num4 >= 0f - num3 && num4 - num3 <= reach;
				}
				float num5 = (meshInstance.FadeKeepsShadow ? 1f : meshInstance.Fade);
				if (shadows && !meshInstance.NoShadow && !meshInstance.Emissive && (!skipUnseen || !(num5 < 0.5f)))
				{
					float num6 = pos.X - x2;
					float num7 = pos.Y - y2;
					float num8 = shadowReach + num3;
					flag3 = num6 * num6 + num7 * num7 <= num8 * num8;
				}
				if (!flag2 && !flag3)
				{
					continue;
				}
				Built built = Get(model);
				if (built.GroupFrame != frame)
				{
					built.ForgetGroups(frame);
				}
				Vector4 misc = new Vector4(num5, 0f, 0f, 0f);
				if (built.Rest != null && meshInstance.PartRotation == null && meshInstance.PartOffset == null && meshInstance.PartScale == null && meshInstance.Hidden == 0)
				{
					if (sides != null)
					{
						Placed(built.RestSphere, ref basis, ref pos, out var x3, out var y3, out var z, out var r);
						if (flag2 && Beyond(sides, x3, y3, z, r))
						{
							flag2 = false;
						}
						if (flag3 && flag && Unlit(sides, x3, y3, z, r, ref falls))
						{
							flag3 = false;
						}
						if (!flag2 && !flag3)
						{
							continue;
						}
					}
					Matrix4x4 mm = Matrix(basis, pos);
					if (flag2)
					{
						Describe(ref d, meshInstance, model, built, land, overlay: false, isActor, into);
						ref InstData reference = ref GpuInstances.Next(built.RestScene ?? (built.RestScene = into.For(1, built.Rest)));
						reference = d;
						reference.M = mm;
						Drawn++;
					}
					if (flag3)
					{
						ShadowCopy(ref GpuInstances.Next(built.RestShadow ?? (built.RestShadow = into.For(0, built.Rest))), ref mm, misc);
					}
					continue;
				}
				PoseParts(meshInstance, model);
				int num9 = model.Parts.Length;
				ulong num10 = Skip(meshInstance, built);
				if (_mm.Length < num9)
				{
					_mm = new Matrix4x4[num9 * 2];
					_seen = new bool[num9 * 2];
					_casts = new bool[num9 * 2];
				}
				Matrix4x4[] mm2 = _mm;
				bool[] seen = _seen;
				bool[] casts = _casts;
				bool flag4 = false;
				bool flag5 = false;
				bool flag6 = sides != null;
				if (flag6)
				{
					Placed(built.RestSphere, ref basis, ref pos, out var x4, out var y4, out var z2, out var r2);
					flag6 = !Within(sides, x4, y4, z2, r2 * 2f);
				}
				for (int j = 0; j < num9; j++)
				{
					if (j < 64 && (num10 & (ulong)(1L << j)) != 0L)
					{
						continue;
					}
					PartMatrix(ref basis, ref pos, j, ref mm2[j]);
					bool flag7 = flag2;
					bool flag8 = flag3;
					if (flag6)
					{
						Placed(built.PartSphere[j], ref mm2[j], out var x5, out var y5, out var z3, out var r3);
						if (flag7 && Beyond(sides, x5, y5, z3, r3))
						{
							flag7 = false;
						}
						if (flag8 && flag && Unlit(sides, x5, y5, z3, r3, ref falls))
						{
							flag8 = false;
						}
					}
					seen[j] = flag7;
					casts[j] = flag8;
					flag4 = flag4 || flag7;
					flag5 = flag5 || flag8;
				}
				bool flag9 = ((sides == null) ? flag2 : flag4);
				if (!flag9 && !flag5)
				{
					continue;
				}
				if (flag9)
				{
					Describe(ref d, meshInstance, model, built, land, overlay: false, isActor, into);
					Drawn++;
				}
				GpuInstances.Group[] partScene = built.PartScene;
				GpuInstances.Group[] partShadow = built.PartShadow;
				Mesh[] parts = built.Parts;
				for (int k = 0; k < num9; k++)
				{
					if (k < 64 && (num10 & (ulong)(1L << k)) != 0L)
					{
						continue;
					}
					if (seen[k])
					{
						ref InstData reference2 = ref GpuInstances.Next(partScene[k] ?? (partScene[k] = into.For(1, parts[k])));
						reference2 = d;
						reference2.M = mm2[k];
						if (built.Merged != 0L)
						{
							Pieces(ref reference2.M, meshInstance.Hidden);
						}
					}
					if (casts[k])
					{
						if (built.Merged != 0L)
						{
							ShadowCopy(ref GpuInstances.Next(partShadow[k] ?? (partShadow[k] = into.For(0, parts[k]))), mm2[k], misc, meshInstance.Hidden);
						}
						else
						{
							ShadowCopy(ref GpuInstances.Next(partShadow[k] ?? (partShadow[k] = into.For(0, parts[k]))), ref mm2[k], misc);
						}
					}
				}
			}
		}

		private static void ShadowCopy(ref InstData c, ref Matrix4x4 mm, Vector4 misc)
		{
			c = default(InstData);
			c.M = mm;
			c.Misc = misc;
		}

		private void CollectOne(GpuInstances into, List<MeshInstance> list, TerrainCache land, ViewState view, Func<int, bool> isActor, int set = 2)
		{
			foreach (MeshInstance item in list)
			{
				if (item == null || item.Model == null || item.Fade <= 0f || item.ShadowOnly)
				{
					continue;
				}
				MeshModel model = item.Model;
				Built built = Get(model);
				InstData d = default(InstData);
				Describe(ref d, item, model, built, land, set == 2, isActor, into);
				if (built.Rest != null && item.PartRotation == null && item.PartOffset == null && item.PartScale == null && item.Hidden == 0)
				{
					d.M = Matrix(item.Basis, item.Position);
					into.Add(set, built.Rest, ref d);
					continue;
				}
				PoseParts(item, model);
				Mat3 basis = item.Basis;
				Vec3 pos = item.Position;
				ulong num = Skip(item, built);
				for (int i = 0; i < model.Parts.Length; i++)
				{
					if (i >= 64 || (num & (ulong)(1L << i)) == 0L)
					{
						PartMatrix(ref basis, ref pos, i, ref d.M);
						if (built.Merged != 0L)
						{
							Pieces(ref d.M, item.Hidden);
						}
						into.Add(set, built.Parts[i], ref d);
					}
				}
			}
		}

		private void PoseParts(MeshInstance m, MeshModel model)
		{
			int num = model.Parts.Length;
			if (_pm.Length < num)
			{
				_pm = new Mat3[num * 2];
				_pt = new Vec3[num * 2];
			}
			PoseParts(m, model, _pm, _pt);
		}

		private static void PoseParts(MeshInstance m, MeshModel model, Mat3[] pm, Vec3[] pt)
		{
			MeshPart[] parts = model.Parts;
			int num = parts.Length;
			Mat3[] partRotation = m.PartRotation;
			Vec3[] partOffset = m.PartOffset;
			float[] partScale = m.PartScale;
			int num2 = ((partRotation != null) ? Math.Min(partRotation.Length, num) : 0);
			int num3 = ((partOffset != null) ? Math.Min(partOffset.Length, num) : 0);
			int num4 = ((partScale != null) ? Math.Min(partScale.Length, num) : 0);
			for (int i = 0; i < num; i++)
			{
				MeshPart obj = parts[i];
				float num5 = 1f;
				float num6 = 0f;
				float num7 = 0f;
				float num8 = 0f;
				float num9 = 1f;
				float num10 = 0f;
				float num11 = 0f;
				float num12 = 0f;
				float num13 = 1f;
				if (i < num2)
				{
					ref Mat3 reference = ref partRotation[i];
					num5 = reference.C0.X;
					num6 = reference.C0.Y;
					num7 = reference.C0.Z;
					num8 = reference.C1.X;
					num9 = reference.C1.Y;
					num10 = reference.C1.Z;
					num11 = reference.C2.X;
					num12 = reference.C2.Y;
					num13 = reference.C2.Z;
				}
				float num14 = ((i < num4) ? partScale[i] : 1f);
				if (num14 != 1f)
				{
					num5 *= num14;
					num6 *= num14;
					num7 *= num14;
					num8 *= num14;
					num9 *= num14;
					num10 *= num14;
					num11 *= num14;
					num12 *= num14;
					num13 *= num14;
				}
				float x = obj.Pivot.X;
				float y = obj.Pivot.Y;
				float z = obj.Pivot.Z;
				float num15 = x - (num5 * x + num8 * y + num11 * z);
				float num16 = y - (num6 * x + num9 * y + num12 * z);
				float num17 = z - (num7 * x + num10 * y + num13 * z);
				if (i < num3)
				{
					ref Vec3 reference2 = ref partOffset[i];
					num15 += reference2.X;
					num16 += reference2.Y;
					num17 += reference2.Z;
				}
				ref Mat3 reference3 = ref pm[i];
				ref Vec3 reference4 = ref pt[i];
				int parent = obj.Parent;
				if (parent >= 0)
				{
					ref Mat3 reference5 = ref pm[parent];
					float x2 = reference5.C0.X;
					float y2 = reference5.C0.Y;
					float z2 = reference5.C0.Z;
					float x3 = reference5.C1.X;
					float y3 = reference5.C1.Y;
					float z3 = reference5.C1.Z;
					float x4 = reference5.C2.X;
					float y4 = reference5.C2.Y;
					float z4 = reference5.C2.Z;
					ref Vec3 reference6 = ref pt[parent];
					float x5 = reference6.X;
					float y5 = reference6.Y;
					float z5 = reference6.Z;
					reference3.C0.X = x2 * num5 + x3 * num6 + x4 * num7;
					reference3.C0.Y = y2 * num5 + y3 * num6 + y4 * num7;
					reference3.C0.Z = z2 * num5 + z3 * num6 + z4 * num7;
					reference3.C1.X = x2 * num8 + x3 * num9 + x4 * num10;
					reference3.C1.Y = y2 * num8 + y3 * num9 + y4 * num10;
					reference3.C1.Z = z2 * num8 + z3 * num9 + z4 * num10;
					reference3.C2.X = x2 * num11 + x3 * num12 + x4 * num13;
					reference3.C2.Y = y2 * num11 + y3 * num12 + y4 * num13;
					reference3.C2.Z = z2 * num11 + z3 * num12 + z4 * num13;
					reference4.X = x2 * num15 + x3 * num16 + x4 * num17 + x5;
					reference4.Y = y2 * num15 + y3 * num16 + y4 * num17 + y5;
					reference4.Z = z2 * num15 + z3 * num16 + z4 * num17 + z5;
				}
				else
				{
					reference3.C0.X = num5;
					reference3.C0.Y = num6;
					reference3.C0.Z = num7;
					reference3.C1.X = num8;
					reference3.C1.Y = num9;
					reference3.C1.Z = num10;
					reference3.C2.X = num11;
					reference3.C2.Y = num12;
					reference3.C2.Z = num13;
					reference4.X = num15;
					reference4.Y = num16;
					reference4.Z = num17;
				}
			}
		}

		private void PartMatrix(ref Mat3 basis, ref Vec3 pos, int p, ref Matrix4x4 mm)
		{
			PartMatrix(_pm, _pt, ref basis, ref pos, p, ref mm);
		}

		private static void PartMatrix(Mat3[] pm, Vec3[] pt, ref Mat3 basis, ref Vec3 pos, int p, ref Matrix4x4 mm)
		{
			float x = basis.C0.X;
			float y = basis.C0.Y;
			float z = basis.C0.Z;
			float x2 = basis.C1.X;
			float y2 = basis.C1.Y;
			float z2 = basis.C1.Z;
			float x3 = basis.C2.X;
			float y3 = basis.C2.Y;
			float z3 = basis.C2.Z;
			ref Mat3 reference = ref pm[p];
			float x4 = reference.C0.X;
			float y4 = reference.C0.Y;
			float z4 = reference.C0.Z;
			mm.m00 = x * x4 + x2 * y4 + x3 * z4;
			mm.m10 = y * x4 + y2 * y4 + y3 * z4;
			mm.m20 = z * x4 + z2 * y4 + z3 * z4;
			x4 = reference.C1.X;
			y4 = reference.C1.Y;
			z4 = reference.C1.Z;
			mm.m01 = x * x4 + x2 * y4 + x3 * z4;
			mm.m11 = y * x4 + y2 * y4 + y3 * z4;
			mm.m21 = z * x4 + z2 * y4 + z3 * z4;
			x4 = reference.C2.X;
			y4 = reference.C2.Y;
			z4 = reference.C2.Z;
			mm.m02 = x * x4 + x2 * y4 + x3 * z4;
			mm.m12 = y * x4 + y2 * y4 + y3 * z4;
			mm.m22 = z * x4 + z2 * y4 + z3 * z4;
			ref Vec3 reference2 = ref pt[p];
			x4 = reference2.X;
			y4 = reference2.Y;
			z4 = reference2.Z;
			mm.m03 = x * x4 + x2 * y4 + x3 * z4 + pos.X;
			mm.m13 = y * x4 + y2 * y4 + y3 * z4 + pos.Y;
			mm.m23 = z * x4 + z2 * y4 + z3 * z4 + pos.Z;
			mm.m30 = 0f;
			mm.m31 = 0f;
			mm.m32 = 0f;
			mm.m33 = 1f;
		}

		private static void Placed(Vector4 sphere, ref Mat3 basis, ref Vec3 pos, out float x, out float y, out float z, out float r)
		{
			x = basis.C0.X * sphere.x + basis.C1.X * sphere.y + basis.C2.X * sphere.z + pos.X;
			y = basis.C0.Y * sphere.x + basis.C1.Y * sphere.y + basis.C2.Y * sphere.z + pos.Y;
			z = basis.C0.Z * sphere.x + basis.C1.Z * sphere.y + basis.C2.Z * sphere.z + pos.Z;
			r = sphere.w * (float)Math.Sqrt(Dot(basis.C0) + Dot(basis.C1) + Dot(basis.C2));
			r = r * 1.001f + 0.02f;
		}

		private static void Placed(Vector4 sphere, ref Matrix4x4 m, out float x, out float y, out float z, out float r)
		{
			x = m.m00 * sphere.x + m.m01 * sphere.y + m.m02 * sphere.z + m.m03;
			y = m.m10 * sphere.x + m.m11 * sphere.y + m.m12 * sphere.z + m.m13;
			z = m.m20 * sphere.x + m.m21 * sphere.y + m.m22 * sphere.z + m.m23;
			r = sphere.w * (float)Math.Sqrt(m.m00 * m.m00 + m.m10 * m.m10 + m.m20 * m.m20 + m.m01 * m.m01 + m.m11 * m.m11 + m.m21 * m.m21 + m.m02 * m.m02 + m.m12 * m.m12 + m.m22 * m.m22);
			r = r * 1.001f + 0.02f;
		}

		private static bool Within(Vector4[] sides, float x, float y, float z, float r)
		{
			for (int i = 0; i < sides.Length; i++)
			{
				Vector4 vector = sides[i];
				if (vector.x * x + vector.y * y + vector.z * z + vector.w < r)
				{
					return false;
				}
			}
			return true;
		}

		private static bool Beyond(Vector4[] sides, float x, float y, float z, float r)
		{
			for (int i = 0; i < sides.Length; i++)
			{
				Vector4 vector = sides[i];
				if (vector.x * x + vector.y * y + vector.z * z + vector.w < 0f - r)
				{
					return true;
				}
			}
			return false;
		}

		private static bool Unlit(Vector4[] sides, float x, float y, float z, float r, ref ShadowFall falls)
		{
			r += falls.Margin;
			Vector3 light = falls.Light;
			float num = 0f;
			float num2 = Math.Max(0f, (z + r - falls.Floor) / light.z);
			for (int i = 0; i < sides.Length; i++)
			{
				Vector4 vector = sides[i];
				float num3 = vector.x * x + vector.y * y + vector.z * z + vector.w + r;
				float num4 = 0f - (vector.x * light.x + vector.y * light.y + vector.z * light.z);
				if (num4 > 0f)
				{
					num = Math.Max(num, (0f - num3) / num4);
				}
				else if (num4 < 0f)
				{
					num2 = Math.Min(num2, (0f - num3) / num4);
				}
				else if (num3 < 0f)
				{
					return true;
				}
				if (num > num2)
				{
					return true;
				}
			}
			return false;
		}

		private static float Dot(Vec3 v)
		{
			return v.X * v.X + v.Y * v.Y + v.Z * v.Z;
		}

		private void Describe(ref InstData d, MeshInstance m, MeshModel model, Built b, TerrainCache land, bool overlay, Func<int, bool> isActor, GpuInstances into)
		{
			int slots = b.Slots;
			bool flag = true;
			if (m.Palette != null)
			{
				for (int i = 0; i < m.Palette.Length && i < slots; i++)
				{
					if (m.Palette[i] != 0)
					{
						flag = false;
						break;
					}
				}
			}
			if (flag)
			{
				if (b.PaletteFrame != into.Frame)
				{
					b.PaletteAt = into.AddPalette(b.Palette, slots);
					b.PaletteFrame = into.Frame;
				}
				d.Pal = new Vector4(b.PaletteAt, m.Snow, m.Sway, m.SwayInvTop);
			}
			else
			{
				Vector4[] pal;
				int num = into.ReservePalette(slots, out pal);
				if (_fillLater)
				{
					FillLater(num, slots, b.Palette, model, m.Palette);
				}
				else
				{
					Array.Copy(b.Palette, 0, pal, num, slots);
					uint[] palette = m.Palette;
					for (int j = 0; j < palette.Length && j < slots; j++)
					{
						if (palette[j] != 0)
						{
							pal[num + j] = Colour(palette[j], model.SlotKind[j], model.SlotFlags[j]);
						}
					}
				}
				d.Pal = new Vector4(num, m.Snow, m.Sway, m.SwayInvTop);
			}
			uint flash = m.Flash;
			d.Flash = new Vector4((float)(flash & 0xFF) / 255f, (float)((flash >> 8) & 0xFF) / 255f, (float)((flash >> 16) & 0xFF) / 255f, (float)(flash >> 24) / 255f);
			uint multiply = m.Multiply;
			d.Mult = ((multiply != 0) ? new Vector4((float)(multiply & 0xFF) / 255f, (float)((multiply >> 8) & 0xFF) / 255f, (float)((multiply >> 16) & 0xFF) / 255f, 1f) : Vector4.zero);
			d.Misc = new Vector4(Mathf.Clamp01(m.Fade), (m.DissolveScale > 0f) ? (1f / m.DissolveScale) : 0f, m.Glow, m.Emissive ? 1f : 0f);
			if (m.Cloud != null)
			{
				d.Misc = new Vector4(Mathf.Clamp01(m.Cloud.Opacity), 0f, 0f, 0f);
				d.Flash = new Vector4(0.95f, 0.95f, 1f, Mathf.Clamp01(0.3f + m.Cloud.SelfLight));
			}
			float y = (overlay ? 5f : ((isActor != null && isActor(m.Tag)) ? 4f : 3f));
			float x = 1f;
			float y2 = 1f;
			float minusReach = 1f;
			float num2 = 0f;
			Lit value = null;
			bool flag2 = m.Static && m.PartRotation == null && m.PartOffset == null;
			if (FieldLight)
			{
				if (!overlay && !m.Emissive && m.Cloud == null)
				{
					BesideLight(m, model, out x, out y2, out minusReach);
				}
			}
			else if (flag2 && _lit.TryGetValue(m, out value) && value.Pos.X == m.Position.X && value.Pos.Y == m.Position.Y && value.Pos.Z == m.Position.Z && _frame - value.Frame < 40 + (m.GetHashCode() & 0xF))
			{
				x = value.Base;
				y2 = value.Top;
				minusReach = value.InvRise;
				num2 = value.Varies;
			}
			else if (!overlay && !m.Emissive && m.Cloud == null && land != null && land.CastShadows && land.Width > 0)
			{
				float num3 = Mathf.Max(0.1f, m.Basis.C2.Z * model.Max.Z);
				minusReach = 1f / num3;
				float x2 = m.Position.X;
				float y3 = m.Position.Y;
				y2 = land.LightAt(x2, y3, m.Position.Z + num3);
				float num4 = Mathf.Max(Len(m.Basis.C0), Len(m.Basis.C1)) * Mathf.Max(Mathf.Max(0f - model.Min.X, model.Max.X), Mathf.Max(0f - model.Min.Y, model.Max.Y));
				float num5 = land.GroundLight(x2, y3);
				float a = land.GroundLight(x2 - num4, y3 - num4);
				float b2 = land.GroundLight(x2 + num4, y3 - num4);
				float a2 = land.GroundLight(x2 - num4, y3 + num4);
				float b3 = land.GroundLight(x2 + num4, y3 + num4);
				float num6 = Mathf.Min(num5, Mathf.Min(Mathf.Min(a, b2), Mathf.Min(a2, b3)));
				float num7 = Mathf.Max(num5, Mathf.Max(Mathf.Max(a, b2), Mathf.Max(a2, b3)));
				x = num5;
				num2 = ((num7 - num6 > 0.01f) ? 1f : 0f);
				if (flag2)
				{
					if (value == null)
					{
						value = (_lit[m] = new Lit());
					}
					value.Pos = m.Position;
					value.Base = x;
					value.Top = y2;
					value.InvRise = minusReach;
					value.Varies = num2;
					value.Frame = _frame;
				}
			}
			d.Tag = new Vector4(m.Tag, y, x, y2);
			d.Ground = new Vector4(m.Position.Z, minusReach, num2, overlay ? 1f : 0f);
		}

		private static float Len(Vec3 v)
		{
			return (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
		}

		private static void BesideLight(MeshInstance m, MeshModel model, out float x, out float y, out float minusReach)
		{
			x = m.Position.X;
			y = m.Position.Y;
			float b = Mathf.Max(Len(m.Basis.C0), Len(m.Basis.C1)) * Mathf.Max(Mathf.Max(0f - model.Min.X, model.Max.X), Mathf.Max(0f - model.Min.Y, model.Max.Y));
			minusReach = 0f - Mathf.Max(0.01f, b);
		}

		private static float MaxAbs(Vec3 v)
		{
			return Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));
		}

		internal static Matrix4x4 Matrix(Mat3 m, Vec3 t)
		{
			return new Matrix4x4
			{
				m00 = m.C0.X,
				m10 = m.C0.Y,
				m20 = m.C0.Z,
				m01 = m.C1.X,
				m11 = m.C1.Y,
				m21 = m.C1.Z,
				m02 = m.C2.X,
				m12 = m.C2.Y,
				m22 = m.C2.Z,
				m03 = t.X,
				m13 = t.Y,
				m23 = t.Z,
				m33 = 1f
			};
		}

		public void Dispose()
		{
			foreach (Built value in _built.Values)
			{
				Mesh[] parts = value.Parts;
				foreach (Mesh mesh in parts)
				{
					if (mesh != null && mesh != value.Rest)
					{
						Unregister(mesh);
						UnityEngine.Object.Destroy(mesh);
					}
				}
				if (value.Rest != null)
				{
					Unregister(value.Rest);
					UnityEngine.Object.Destroy(value.Rest);
				}
				DestroyAtlas(value);
			}
			_built.Clear();
			DisposeCrowds();
		}

		internal void ForgetRetired(MeshModel model)
		{
			if (model == null || !_built.TryGetValue(model, out var value))
			{
				return;
			}
			Mesh[] parts = value.Parts;
			foreach (Mesh mesh in parts)
			{
				if (mesh != null)
				{
					Unregister(mesh);
					UnityEngine.Object.Destroy(mesh);
				}
			}
			if (value.Rest != null && Array.IndexOf(value.Parts, value.Rest) < 0)
			{
				Unregister(value.Rest);
				UnityEngine.Object.Destroy(value.Rest);
			}
			DestroyAtlas(value);
			_built.Remove(model);
		}

		private static bool ShaderMasksPieces()
		{
			if (_piecesVersion != GpuShaders.Version)
			{
				_piecesVersion = GpuShaders.Version;
				Shader shader = GpuShaders.Get("FirstPerson/Model");
				_piecesShader = shader != null && shader.FindPropertyIndex("_FP_Pieces") >= 0;
				Log.Info("gpu: looks pieces " + (_piecesShader ? "merged (the shader masks them)" : "drawn part by part (the shader loaded has no piece mask)"));
			}
			return _piecesShader;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ulong Skip(MeshInstance m, Built b)
		{
			if (b.Merged != 0L)
			{
				return (m.Hidden & ~b.Owners) | b.Merged;
			}
			return m.Hidden;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Pieces(ref Matrix4x4 mm, ulong hidden)
		{
			mm.m30 = hidden & 0xFFFFFF;
			mm.m31 = (hidden >> 24) & 0xFFFFFF;
			mm.m32 = hidden >> 48;
		}

		private static void ShadowCopy(ref InstData c, Matrix4x4 mm, Vector4 misc, ulong hidden)
		{
			Pieces(ref mm, hidden);
			ShadowCopy(ref c, ref mm, misc);
		}

		private void MergePieces(Built b, MeshModel model)
		{
			if (b.Live || !MergeLooks || !ShaderMasksPieces())
			{
				return;
			}
			Wardrobe.Parts parts = Wardrobe.PartsOf(model);
			if (parts == null || !parts.Has || parts.Managed == 0L)
			{
				return;
			}
			int num = Math.Min(64, model.Parts.Length);
			int[] array = new int[num];
			Dictionary<int, List<int>> dictionary = new Dictionary<int, List<int>>();
			for (int i = 0; i < num; i++)
			{
				int num2 = i;
				int num3 = 0;
				while (num2 >= 0 && num2 < num && (parts.Managed & (ulong)(1L << num2)) != 0L && num3++ < 64)
				{
					num2 = model.Parts[num2].Parent;
				}
				if (num2 < 0 || num2 >= num || (parts.Managed & (ulong)(1L << num2)) != 0L)
				{
					array[i] = i;
					continue;
				}
				array[i] = num2;
				if (num2 != i)
				{
					if (!dictionary.TryGetValue(num2, out var value))
					{
						int key = num2;
						List<int> obj = new List<int> { num2 };
						value = obj;
						dictionary[key] = obj;
					}
					value.Add(i);
				}
			}
			foreach (KeyValuePair<int, List<int>> item in dictionary)
			{
				int key2 = item.Key;
				if (b.Parts[key2] != null && b.Parts[key2] != b.Rest)
				{
					UnityEngine.Object.Destroy(b.Parts[key2]);
				}
				b.Parts[key2] = BuildMerged(model, item.Value);
				b.PartSphere[key2] = Sphere(model, item.Value);
				b.Owners |= (ulong)(1L << key2);
				foreach (int item2 in item.Value)
				{
					if (item2 != key2)
					{
						if (b.Parts[item2] != null && b.Parts[item2] != b.Rest)
						{
							UnityEngine.Object.Destroy(b.Parts[item2]);
						}
						b.Parts[item2] = null;
						b.Merged |= (ulong)(1L << item2);
					}
				}
			}
		}

		private Mesh BuildMerged(MeshModel model, List<int> parts)
		{
			int num = 0;
			foreach (int part in parts)
			{
				num += model.Parts[part].TriangleCount;
			}
			int num2 = num * 3;
			Vector3[] array = new Vector3[num2];
			Vector3[] array2 = new Vector3[num2];
			Vector4[] array3 = new Vector4[num2];
			Vector3[] array4 = new Vector3[num2];
			Built value;
			Atlas atlas = (_atlas = (_built.TryGetValue(model, out value) ? value.Atlas : null));
			Vector3[] array5 = ((atlas != null) ? new Vector3[num2] : null);
			Vector4[] array6 = ((atlas != null) ? new Vector4[num2] : null);
			int num3 = 0;
			foreach (int part2 in parts)
			{
				Fill(model, part2, part2 + 1, out var pos, out var nrm, out var uv, out var face);
				int num4 = model.Parts[part2].TriangleCount * 3;
				Array.Copy(pos, 0, array, num3, num4);
				Array.Copy(nrm, 0, array2, num3, num4);
				Array.Copy(face, 0, array4, num3, num4);
				if (array5 != null)
				{
					Array.Copy(_fillTuv, 0, array5, num3, num4);
					Array.Copy(_fillRect, 0, array6, num3, num4);
				}
				for (int i = 0; i < num4; i++)
				{
					Vector4 vector = uv[i];
					vector.w = part2 + 1;
					array3[num3 + i] = vector;
				}
				num3 += num4;
			}
			int[] array7 = new int[num2];
			for (int j = 0; j < num2; j++)
			{
				array7[j] = j;
			}
			Mesh mesh = new Mesh
			{
				name = "FirstPersonModel_" + model.Name,
				indexFormat = ((num2 > 65000) ? IndexFormat.UInt32 : IndexFormat.UInt16)
			};
			mesh.vertices = array;
			mesh.normals = array2;
			mesh.SetUVs(0, array3);
			mesh.SetUVs(1, array4);
			if (array5 != null)
			{
				mesh.SetUVs(2, array5);
				mesh.SetUVs(3, array6);
			}
			mesh.SetIndices(array7, MeshTopology.Triangles, 0, calculateBounds: false);
			mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
			mesh.UploadMeshData(markNoLongerReadable: true);
			if (atlas != null)
			{
				ImageOf[mesh] = atlas.Tex;
			}
			return mesh;
		}

		private static Vector4 Sphere(MeshModel model, List<int> parts)
		{
			float num = float.MaxValue;
			float num2 = float.MaxValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MinValue;
			float num6 = float.MinValue;
			foreach (int part in parts)
			{
				float[] pos = model.Parts[part].Pos;
				for (int i = 0; i + 2 < pos.Length; i += 3)
				{
					num = Math.Min(num, pos[i]);
					num4 = Math.Max(num4, pos[i]);
					num2 = Math.Min(num2, pos[i + 1]);
					num5 = Math.Max(num5, pos[i + 1]);
					num3 = Math.Min(num3, pos[i + 2]);
					num6 = Math.Max(num6, pos[i + 2]);
				}
			}
			if (num > num4)
			{
				return new Vector4(0f, 0f, 0f, 0f);
			}
			double num7 = ((double)num + (double)num4) * 0.5;
			double num8 = ((double)num2 + (double)num5) * 0.5;
			double num9 = ((double)num3 + (double)num6) * 0.5;
			double num10 = 0.0;
			foreach (int part2 in parts)
			{
				float[] pos2 = model.Parts[part2].Pos;
				for (int j = 0; j + 2 < pos2.Length; j += 3)
				{
					double num11 = (double)pos2[j] - num7;
					double num12 = (double)pos2[j + 1] - num8;
					double num13 = (double)pos2[j + 2] - num9;
					num10 = Math.Max(num10, num11 * num11 + num12 * num12 + num13 * num13);
				}
			}
			return new Vector4((float)num7, (float)num8, (float)num9, (float)(Math.Sqrt(num10) * 1.0001 + 0.0001));
		}

		internal void ForgetLightForTest()
		{
			_lit.Clear();
			_refLit.Clear();
		}

		internal void ReferenceCollect(RefInstances into, List<MeshInstance> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach)
		{
			_refFrame++;
			if (_refLit.Count > 20000)
			{
				_refLit.Clear();
			}
			int refMode = RefMode;
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			float x = view.X;
			float y = view.Y;
			float x2 = shadowCentre.x;
			float y2 = shadowCentre.y;
			int i = 0;
			for (int count = list.Count; i < count; i++)
			{
				MeshInstance meshInstance = list[i];
				if (meshInstance == null || meshInstance.Model == null || meshInstance.Fade <= 0f)
				{
					continue;
				}
				MeshModel model = meshInstance.Model;
				Mat3 a = meshInstance.Basis;
				float num3 = Math.Max(Math.Max(Len(a.C0), Len(a.C1)), Len(a.C2)) * Math.Max(MaxAbs(model.Min), MaxAbs(model.Max)) + 0.5f;
				Vec3 position = meshInstance.Position;
				bool flag = false;
				bool flag2 = false;
				if (!meshInstance.ShadowOnly)
				{
					float num4 = (position.X - x) * num + (position.Y - y) * num2;
					flag = num4 >= 0f - num3 && num4 - num3 <= reach;
				}
				if (shadows && !meshInstance.NoShadow && !meshInstance.Emissive)
				{
					float num5 = position.X - x2;
					float num6 = position.Y - y2;
					float num7 = shadowReach + num3;
					flag2 = num5 * num5 + num6 * num6 <= num7 * num7;
				}
				if ((!flag && !flag2) || refMode == 1)
				{
					continue;
				}
				Built built = Get(model);
				InstData d = default(InstData);
				InstData d2 = new InstData
				{
					Misc = new Vector4(meshInstance.FadeKeepsShadow ? 1f : meshInstance.Fade, 0f, 0f, 0f)
				};
				if (flag)
				{
					RefDescribe(ref d, meshInstance, model, built, land, overlay: false, isActor, into);
				}
				if (refMode == 2)
				{
					continue;
				}
				if (built.Rest != null && meshInstance.PartRotation == null && meshInstance.PartOffset == null && meshInstance.PartScale == null && meshInstance.Hidden == 0)
				{
					Matrix4x4 m = Matrix(a, position);
					if (refMode != 3)
					{
						if (flag)
						{
							d.M = m;
							into.Add(1, built.Rest, ref d);
						}
						if (flag2)
						{
							d2.M = m;
							into.Add(0, built.Rest, ref d2);
						}
					}
					continue;
				}
				PoseParts(meshInstance, model);
				ulong num8 = Skip(meshInstance, built);
				for (int j = 0; j < model.Parts.Length; j++)
				{
					if (j < 64 && (num8 & (ulong)(1L << j)) != 0L)
					{
						continue;
					}
					Matrix4x4 mm = Matrix(a * _pm[j], a.Mul(_pt[j]) + position);
					if (built.Merged != 0L)
					{
						Pieces(ref mm, meshInstance.Hidden);
					}
					if (refMode != 3)
					{
						if (flag)
						{
							d.M = mm;
							into.Add(1, built.Parts[j], ref d);
						}
						if (flag2)
						{
							d2.M = mm;
							into.Add(0, built.Parts[j], ref d2);
						}
					}
				}
			}
		}

		internal void StagedCollect(int stage, GpuInstances into, List<MeshInstance> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach, Vector4[] sides, ShadowFall falls)
		{
			_frame++;
			bool flag = sides != null && shadows && falls.On && falls.Light.z > 0.001f;
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			InstData d = default(InstData);
			int i = 0;
			for (int count = list.Count; i < count; i++)
			{
				MeshInstance meshInstance = list[i];
				if (meshInstance == null || meshInstance.Model == null || meshInstance.Fade <= 0f)
				{
					continue;
				}
				MeshModel model = meshInstance.Model;
				Mat3 basis = meshInstance.Basis;
				float num3 = Math.Max(Math.Max(Len(basis.C0), Len(basis.C1)), Len(basis.C2)) * Math.Max(MaxAbs(model.Min), MaxAbs(model.Max)) + 0.5f;
				Vec3 pos = meshInstance.Position;
				bool flag2 = false;
				bool flag3 = false;
				if (!meshInstance.ShadowOnly)
				{
					float num4 = (pos.X - view.X) * num + (pos.Y - view.Y) * num2;
					flag2 = num4 >= 0f - num3 && num4 - num3 <= reach;
				}
				if (shadows && !meshInstance.NoShadow && !meshInstance.Emissive)
				{
					float num5 = pos.X - shadowCentre.x;
					float num6 = pos.Y - shadowCentre.y;
					float num7 = shadowReach + num3;
					flag3 = num5 * num5 + num6 * num6 <= num7 * num7;
				}
				if (!flag2 && !flag3)
				{
					continue;
				}
				Built built = Get(model);
				if (flag2)
				{
					Describe(ref d, meshInstance, model, built, land, overlay: false, isActor, into);
				}
				if (stage < 2 || (built.Rest != null && meshInstance.PartRotation == null && meshInstance.PartOffset == null && meshInstance.PartScale == null && meshInstance.Hidden == 0))
				{
					continue;
				}
				PoseParts(meshInstance, model);
				int num8 = model.Parts.Length;
				if (_mm.Length < num8)
				{
					_mm = new Matrix4x4[num8 * 2];
					_seen = new bool[num8 * 2];
					_casts = new bool[num8 * 2];
				}
				ulong num9 = Skip(meshInstance, built);
				for (int j = 0; j < num8; j++)
				{
					if (j >= 64 || (num9 & (ulong)(1L << j)) == 0L)
					{
						PartMatrix(ref basis, ref pos, j, ref _mm[j]);
						if (stage >= 3 && sides != null)
						{
							Placed(built.PartSphere[j], ref _mm[j], out var x, out var y, out var z, out var r);
							_seen[j] = flag2 && !Beyond(sides, x, y, z, r);
							_casts[j] = flag3 && flag && !Unlit(sides, x, y, z, r, ref falls);
						}
					}
				}
			}
		}

		internal string ProfileDescribe(GpuInstances into, List<MeshInstance> list, TerrainCache land)
		{
			double num = 1000.0 / (double)Stopwatch.Frequency;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			float num7 = 0f;
			long timestamp = Stopwatch.GetTimestamp();
			foreach (MeshInstance item in list)
			{
				if (item != null && item.Model != null && item.Fade > 0f)
				{
					Get(item.Model);
					num2++;
				}
			}
			long timestamp2 = Stopwatch.GetTimestamp();
			foreach (MeshInstance item2 in list)
			{
				if (item2 != null && item2.Model != null && !(item2.Fade <= 0f) && !item2.ShadowOnly && !item2.Emissive && item2.Cloud == null && (!item2.Static || item2.PartRotation != null || item2.PartOffset != null))
				{
					num3++;
					MeshModel model = item2.Model;
					float num8 = Mathf.Max(0.1f, item2.Basis.C2.Z * model.Max.Z);
					float x = item2.Position.X;
					float y = item2.Position.Y;
					num7 += land.LightAt(x, y, item2.Position.Z + num8);
				}
			}
			long timestamp3 = Stopwatch.GetTimestamp();
			foreach (MeshInstance item3 in list)
			{
				if (item3 != null && item3.Model != null && !(item3.Fade <= 0f) && !item3.ShadowOnly && !item3.Emissive && item3.Cloud == null && (!item3.Static || item3.PartRotation != null || item3.PartOffset != null))
				{
					MeshModel model2 = item3.Model;
					float x2 = item3.Position.X;
					float y2 = item3.Position.Y;
					float num9 = Mathf.Max(Len(item3.Basis.C0), Len(item3.Basis.C1)) * Mathf.Max(Mathf.Max(0f - model2.Min.X, model2.Max.X), Mathf.Max(0f - model2.Min.Y, model2.Max.Y));
					num7 += land.GroundLight(x2, y2) + land.GroundLight(x2 - num9, y2 - num9) + land.GroundLight(x2 + num9, y2 - num9) + land.GroundLight(x2 - num9, y2 + num9) + land.GroundLight(x2 + num9, y2 + num9);
				}
			}
			long timestamp4 = Stopwatch.GetTimestamp();
			into.Begin();
			foreach (MeshInstance item4 in list)
			{
				if (item4 == null || item4.Model == null || item4.Fade <= 0f || item4.ShadowOnly)
				{
					continue;
				}
				Built built = Get(item4.Model);
				int slots = built.Slots;
				bool flag = true;
				if (item4.Palette != null)
				{
					for (int i = 0; i < item4.Palette.Length && i < slots; i++)
					{
						if (item4.Palette[i] != 0)
						{
							flag = false;
							break;
						}
					}
				}
				if (flag)
				{
					num4++;
					continue;
				}
				num5++;
				Vector4[] pal;
				int num10 = into.ReservePalette(slots, out pal);
				Array.Copy(built.Palette, 0, pal, num10, slots);
				uint[] palette = item4.Palette;
				for (int j = 0; j < palette.Length && j < slots; j++)
				{
					if (palette[j] != 0)
					{
						pal[num10 + j] = Colour(palette[j], item4.Model.SlotKind[j], item4.Model.SlotFlags[j]);
						num6++;
					}
				}
			}
			long timestamp5 = Stopwatch.GetTimestamp();
			return string.Format("lookups {0:F2} ({1}), land light at the top {2:F2} ({3} not still), ground light {4:F2}, palettes {5:F2} ({6} own, {7} coloured, {8} slots){9}", (double)(timestamp2 - timestamp) * num, num2, (double)(timestamp3 - timestamp2) * num, num3, (double)(timestamp4 - timestamp3) * num, (double)(timestamp5 - timestamp4) * num, num4, num5, num6, (num7 < -1f) ? "!" : "");
		}

		internal void ReferenceCollectOverlay(RefInstances into, List<MeshInstance> list, TerrainCache land, Func<int, bool> isActor)
		{
			foreach (MeshInstance item in list)
			{
				if (item == null || item.Model == null || item.Fade <= 0f || item.ShadowOnly)
				{
					continue;
				}
				MeshModel model = item.Model;
				Built built = Get(model);
				InstData d = default(InstData);
				RefDescribe(ref d, item, model, built, land, overlay: true, isActor, into);
				if (built.Rest != null && item.PartRotation == null && item.PartOffset == null && item.PartScale == null && item.Hidden == 0)
				{
					d.M = Matrix(item.Basis, item.Position);
					into.Add(2, built.Rest, ref d);
					continue;
				}
				PoseParts(item, model);
				ulong num = Skip(item, built);
				for (int i = 0; i < model.Parts.Length; i++)
				{
					if (i >= 64 || (num & (ulong)(1L << i)) == 0L)
					{
						d.M = Matrix(item.Basis * _pm[i], item.Basis.Mul(_pt[i]) + item.Position);
						if (built.Merged != 0L)
						{
							Pieces(ref d.M, item.Hidden);
						}
						into.Add(2, built.Parts[i], ref d);
					}
				}
			}
		}

		private void RefDescribe(ref InstData d, MeshInstance m, MeshModel model, Built b, TerrainCache land, bool overlay, Func<int, bool> isActor, RefInstances into)
		{
			int slots = b.Slots;
			bool flag = true;
			if (m.Palette != null)
			{
				for (int i = 0; i < m.Palette.Length && i < slots; i++)
				{
					if (m.Palette[i] != 0)
					{
						flag = false;
						break;
					}
				}
			}
			if (flag)
			{
				if (!_refPalFrame.TryGetValue(b, out var value) || value != into.Frame)
				{
					_refPalAt[b] = into.AddPalette(b.Palette, slots);
					_refPalFrame[b] = into.Frame;
				}
				d.Pal = new Vector4(_refPalAt[b], m.Snow, m.Sway, m.SwayInvTop);
			}
			else
			{
				Array.Copy(b.Palette, _palette, slots);
				for (int j = 0; j < m.Palette.Length && j < slots; j++)
				{
					if (m.Palette[j] != 0)
					{
						_palette[j] = Colour(m.Palette[j], model.SlotKind[j], model.SlotFlags[j]);
					}
				}
				d.Pal = new Vector4(into.AddPalette(_palette, slots), m.Snow, m.Sway, m.SwayInvTop);
			}
			uint flash = m.Flash;
			d.Flash = new Vector4((float)(flash & 0xFF) / 255f, (float)((flash >> 8) & 0xFF) / 255f, (float)((flash >> 16) & 0xFF) / 255f, (float)(flash >> 24) / 255f);
			uint multiply = m.Multiply;
			d.Mult = ((multiply != 0) ? new Vector4((float)(multiply & 0xFF) / 255f, (float)((multiply >> 8) & 0xFF) / 255f, (float)((multiply >> 16) & 0xFF) / 255f, 1f) : Vector4.zero);
			d.Misc = new Vector4(Mathf.Clamp01(m.Fade), (m.DissolveScale > 0f) ? (1f / m.DissolveScale) : 0f, m.Glow, m.Emissive ? 1f : 0f);
			if (m.Cloud != null)
			{
				d.Misc = new Vector4(Mathf.Clamp01(m.Cloud.Opacity), 0f, 0f, 0f);
				d.Flash = new Vector4(0.95f, 0.95f, 1f, Mathf.Clamp01(0.3f + m.Cloud.SelfLight));
			}
			float y = (overlay ? 5f : ((isActor != null && isActor(m.Tag)) ? 4f : 3f));
			float x = 1f;
			float y2 = 1f;
			float minusReach = 1f;
			float num = 0f;
			Lit value2 = null;
			bool flag2 = m.Static && m.PartRotation == null && m.PartOffset == null;
			if (FieldLight)
			{
				if (!overlay && !m.Emissive && m.Cloud == null)
				{
					BesideLight(m, model, out x, out y2, out minusReach);
				}
			}
			else if (flag2 && _refLit.TryGetValue(m, out value2) && value2.Pos.X == m.Position.X && value2.Pos.Y == m.Position.Y && value2.Pos.Z == m.Position.Z && _refFrame - value2.Frame < 40 + (m.GetHashCode() & 0xF))
			{
				x = value2.Base;
				y2 = value2.Top;
				minusReach = value2.InvRise;
				num = value2.Varies;
			}
			else if (!overlay && !m.Emissive && m.Cloud == null && land != null && land.CastShadows && land.Width > 0)
			{
				float num2 = Mathf.Max(0.1f, m.Basis.C2.Z * model.Max.Z);
				minusReach = 1f / num2;
				float x2 = m.Position.X;
				float y3 = m.Position.Y;
				y2 = land.LightAt(x2, y3, m.Position.Z + num2);
				float num3 = Mathf.Max(Len(m.Basis.C0), Len(m.Basis.C1)) * Mathf.Max(Mathf.Max(0f - model.Min.X, model.Max.X), Mathf.Max(0f - model.Min.Y, model.Max.Y));
				float num4 = land.GroundLight(x2, y3);
				float a = land.GroundLight(x2 - num3, y3 - num3);
				float b2 = land.GroundLight(x2 + num3, y3 - num3);
				float a2 = land.GroundLight(x2 - num3, y3 + num3);
				float b3 = land.GroundLight(x2 + num3, y3 + num3);
				float num5 = Mathf.Min(num4, Mathf.Min(Mathf.Min(a, b2), Mathf.Min(a2, b3)));
				float num6 = Mathf.Max(num4, Mathf.Max(Mathf.Max(a, b2), Mathf.Max(a2, b3)));
				x = num4;
				num = ((num6 - num5 > 0.01f) ? 1f : 0f);
				if (flag2)
				{
					if (value2 == null)
					{
						value2 = (_refLit[m] = new Lit());
					}
					value2.Pos = m.Position;
					value2.Base = x;
					value2.Top = y2;
					value2.InvRise = minusReach;
					value2.Varies = num;
					value2.Frame = _refFrame;
				}
			}
			d.Tag = new Vector4(m.Tag, y, x, y2);
			d.Ground = new Vector4(m.Position.Z, minusReach, num, overlay ? 1f : 0f);
		}

		private static Atlas BuildAtlas(MeshModel model)
		{
			if (!model.Textured)
			{
				return null;
			}
			MeshTexture[] tex = model.Textures;
			int num = tex.Length;
			Rect[] array = new Rect[num];
			bool flag = false;
			List<int> list = new List<int>();
			for (int i = 0; i < num; i++)
			{
				if (tex[i].Pixels != null)
				{
					list.Add(i);
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			list.Sort((int a, int b) => tex[b].Height.CompareTo(tex[a].Height));
			int num2 = 256;
			while (true)
			{
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				bool flag2 = true;
				foreach (int item in list)
				{
					int num7 = tex[item].Width + 4;
					int val = tex[item].Height + 4;
					if (num7 > num2)
					{
						flag2 = false;
						break;
					}
					if (num3 + num7 > num2)
					{
						num3 = 0;
						num4 += num5;
						num5 = 0;
					}
					array[item] = new Rect(num3 + 2, num4 + 2, tex[item].Width, tex[item].Height);
					num3 += num7;
					num5 = Math.Max(num5, val);
					num6 = Math.Max(num6, num4 + num5);
				}
				if (flag2 && num6 <= num2)
				{
					flag = true;
					break;
				}
				if (num2 >= 4096)
				{
					break;
				}
				num2 *= 2;
			}
			if (!flag)
			{
				Log.Info("gpu: WARNING " + model.Name + "'s images don't fit an atlas of " + 4096 + " px: drawn flat");
				return null;
			}
			int num8 = 1;
			foreach (int item2 in list)
			{
				num8 = Math.Max(num8, (int)array[item2].yMax + 2);
			}
			num8 = Mathf.NextPowerOfTwo(num8);
			Color32[] array2 = new Color32[num2 * num8];
			foreach (int item3 in list)
			{
				MeshTexture meshTexture = tex[item3];
				int num9 = (int)array[item3].x;
				int num10 = (int)array[item3].y;
				int width = meshTexture.Width;
				int height = meshTexture.Height;
				for (int num11 = -2; num11 < height + 2; num11++)
				{
					int num12 = ((num11 >= 0) ? ((num11 >= height) ? (height - 1) : num11) : 0);
					int num13 = (num10 + num11) * num2;
					for (int num14 = -2; num14 < width + 2; num14++)
					{
						int num15 = ((num14 >= 0) ? ((num14 >= width) ? (width - 1) : num14) : 0);
						uint num16 = meshTexture.Pixels[num12 * width + num15];
						array2[num13 + num9 + num14] = new Color32((byte)(num16 & 0xFF), (byte)((num16 >> 8) & 0xFF), (byte)((num16 >> 16) & 0xFF), (byte)(num16 >> 24));
					}
				}
			}
			Texture2D texture2D = new Texture2D(num2, num8, TextureFormat.RGBA32, mipChain: false, linear: true)
			{
				name = "FirstPersonAtlas_" + model.Name,
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp
			};
			texture2D.SetPixels32(array2);
			texture2D.Apply(updateMipmaps: false, !KeepAtlasReadable);
			Atlas atlas = new Atlas
			{
				Tex = texture2D,
				SlotRect = new Vector4[model.SlotColor.Length]
			};
			for (int num17 = 0; num17 < atlas.SlotRect.Length; num17++)
			{
				int num18 = ((num17 < model.SlotTexture.Length) ? model.SlotTexture[num17] : (-1));
				if (num18 >= 0 && tex[num18].Pixels != null)
				{
					Rect rect = array[num18];
					float num19 = rect.width / (float)num2;
					float w = rect.height / (float)num8;
					atlas.SlotRect[num17] = new Vector4(rect.x / (float)num2, rect.y / (float)num8, tex[num18].Crisp ? (0f - num19) : num19, w);
				}
			}
			return atlas;
		}

		internal static Texture2D AtlasForTest(MeshModel model, out Vector4 rect)
		{
			Atlas atlas = BuildAtlas(model);
			rect = ((atlas != null && atlas.SlotRect.Length != 0) ? atlas.SlotRect[0] : Vector4.zero);
			return atlas?.Tex;
		}

		private static void DestroyAtlas(Built b)
		{
			if (b.Atlas != null)
			{
				if (b.Atlas.Tex != null)
				{
					UnityEngine.Object.Destroy(b.Atlas.Tex);
				}
				b.Atlas = null;
			}
		}

		private static void Register(Mesh mesh, Built b)
		{
			if (!(mesh == null))
			{
				if (b.Atlas != null)
				{
					ImageOf[mesh] = b.Atlas.Tex;
				}
				else
				{
					ImageOf.Remove(mesh);
				}
			}
		}

		private static void Unregister(Mesh mesh)
		{
			if (mesh != null)
			{
				ImageOf.Remove(mesh);
			}
		}

		internal static bool ShaderTextured()
		{
			if (_texturedVersion != GpuShaders.Version)
			{
				_texturedVersion = GpuShaders.Version;
				Shader shader = GpuShaders.Get("FirstPerson/Model");
				_texturedShader = shader != null && shader.FindPropertyIndex("_FP_Textured") >= 0;
				Log.Info("gpu: pack textures " + (_texturedShader ? "drawn" : "NOT drawn (the shader loaded has no texture sampler: an old bundle)"));
			}
			return _texturedShader;
		}

		private static WorkerPool Agreed(WorkerPool pool)
		{
			if (!pool.FloatsAgree())
			{
				Log.Error("gpu copies: the worker threads' float arithmetic differs from the game thread's: copies gathered on the game thread alone");
				pool.Dispose();
				pool = new WorkerPool(0, "FirstPerson copies");
			}
			Log.Info($"gpu copies: {pool.Workers} worker threads ({Environment.ProcessorCount} hardware threads)");
			return pool;
		}

		private static void LookAtPending()
		{
			long timestamp = Stopwatch.GetTimestamp();
			if (timestamp < _pendingNext)
			{
				return;
			}
			WorkerPool pending = _pending;
			try
			{
				if (pending.WaitStarted(0))
				{
					_pending = null;
					_pool = Agreed(pending);
				}
				else if (++_pendingLooks >= 20)
				{
					_pending = null;
					pending.Dispose();
					Log.Error($"gpu copies: the worker threads never started ({20.5:F1} s): copies gathered on the game thread alone");
				}
				else
				{
					_pendingNext = timestamp + Stopwatch.Frequency;
				}
			}
			catch (Exception ex)
			{
				_pending = null;
				Log.Error("gpu copies: no worker threads: " + ex.Message);
			}
		}

		private void CollectPhased(GpuInstances into, List<MeshInstance> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach, Vector4[] sides, ShadowFall falls)
		{
			if (!Verify)
			{
				CollectSteps(into, list, land, view, reach, isActor, shadows, shadowCentre, shadowReach, sides, falls);
				return;
			}
			GpuInstances.Mark mark = into.MarkForTest();
			int num = VerifyRuns & 7;
			bool poseTwins = PoseTwins;
			bool writeBehind = WriteBehind;
			bool cullOnWorkers = CullOnWorkers;
			PoseTwins = (num & 1) == 0;
			WriteBehind = (num & 2) == 0;
			CullOnWorkers = (num & 4) == 0;
			try
			{
				CollectSteps(into, list, land, view, reach, isActor, shadows, shadowCentre, shadowReach, sides, falls);
			}
			finally
			{
				PoseTwins = poseTwins;
				WriteBehind = writeBehind;
				CullOnWorkers = cullOnWorkers;
			}
			VerifyMixes[num]++;
			int drawn = Drawn;
			(_verifyNew ?? (_verifyNew = new Added())).Take(into, mark, drawn);
			into.RollBackForTest(mark);
			for (int i = 0; i < _itemCount; i++)
			{
				Built b = _items[i].B;
				b.GroupFrame = -1;
				if (b.PaletteFrame == into.Frame && b.PaletteAt >= mark.Palettes)
				{
					b.PaletteFrame = -1;
				}
			}
			Drawn = 0;
			CollectBothOld(into, list, land, view, reach, isActor, shadows, shadowCentre, shadowReach, sides, falls);
			(_verifyOld ?? (_verifyOld = new Added())).Take(into, mark, Drawn);
			VerifyRuns++;
			VerifyCopies += _verifyNew.Count;
			VerifyTwins += _twinItems;
			string text = _verifyNew.Differs(_verifyOld);
			if (text != null)
			{
				VerifyDiffers++;
				if (VerifyFirst == null)
				{
					VerifyFirst = text;
				}
			}
		}

		private void CollectSteps(GpuInstances into, List<MeshInstance> list, TerrainCache land, ViewState view, float reach, Func<int, bool> isActor, bool shadows, Vector2 shadowCentre, float shadowReach, Vector4[] sides, ShadowFall falls)
		{
			long t = (TimeSteps ? Stopwatch.GetTimestamp() : 0);
			bool skipUnseen = SkipUnseen;
			if (!skipUnseen)
			{
				sides = null;
			}
			bool jobShadowCull = sides != null && shadows && falls.On && falls.Light.z > 0.001f;
			int frame = into.Frame;
			float cullFx = Mathf.Cos(view.Yaw);
			float cullFy = Mathf.Sin(view.Yaw);
			int num = ((++_stamp != 0) ? _stamp : (++_stamp));
			bool poseTwins = PoseTwins;
			int count = list.Count;
			WorkerPool workerPool = Pool;
			if (workerPool != null && (workerPool.Workers == 0 || !workerPool.TryBegin()))
			{
				workerPool = null;
			}
			try
			{
				if (_scratch == null || _scratch.Length < (workerPool?.Workers ?? 0) + 1)
				{
					_scratch = new Scratch[(workerPool?.Workers ?? 0) + 1];
					for (int i = 0; i < _scratch.Length; i++)
					{
						_scratch[i] = new Scratch();
					}
				}
				if (_cull.Length < count)
				{
					_cull = new byte[Math.Max(count, _cull.Length * 2)];
					_found = new Built[_cull.Length];
				}
				_cullList = list;
				_cullEx = view.X;
				_cullEy = view.Y;
				_cullFx = cullFx;
				_cullFy = cullFy;
				_cullReach = reach;
				_cullScx = shadowCentre.x;
				_cullScy = shadowCentre.y;
				_cullShadowReach = shadowReach;
				_cullShadows = shadows;
				_cullSkip = skipUnseen;
				int num2 = (count + 256 - 1) / 256;
				bool flag = workerPool == null || !CullOnWorkers || num2 < 2;
				bool flag2 = !flag;
				if (flag2)
				{
					WorkerPool workerPool2 = workerPool;
					CullJob cullJob = _cullJob;
					if (cullJob == null)
					{
						CullJob obj = new CullJob
						{
							Models = this
						};
						CullJob cullJob2 = obj;
						_cullJob = obj;
						cullJob = cullJob2;
					}
					workerPool2.Start(cullJob, num2);
				}
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				int num7 = 0;
				int num8 = -1;
				int num9 = 0;
				int num10 = 0;
				_chunkStart[0] = 0;
				try
				{
					for (int j = 0; j < num2; j++)
					{
						if (flag)
						{
							CullRunOf(j, here: true);
						}
						else if (flag2)
						{
							while (!workerPool.IsDone(j))
							{
								if (workerPool.TryTake(out var chunk))
								{
									workerPool.RunHere(chunk);
								}
								else
								{
									workerPool.Await(j);
								}
							}
							if (workerPool.Failed)
							{
								break;
							}
						}
						int k = j * 256;
						for (int num11 = Math.Min(count, k + 256); k < num11; k++)
						{
							byte b = _cull[k];
							if (b == 0)
							{
								continue;
							}
							MeshInstance meshInstance = list[k];
							MeshModel model = meshInstance.Model;
							Built built = _found[k];
							if (built != null)
							{
								built = Touch(built, model);
							}
							else
							{
								if (flag2)
								{
									workerPool.Finish();
									flag2 = false;
								}
								built = Get(model);
							}
							if (built.GroupFrame != frame)
							{
								built.ForgetGroups(frame);
							}
							if (built.CallStamp != num)
							{
								built.CallStamp = num;
								built.CallItems = 0;
								if (built.PaletteFrame != frame)
								{
									num10 += built.Slots;
								}
							}
							built.CallItems++;
							if ((b & 1) != 0 && OwnColours(meshInstance, built.Slots))
							{
								num10 += built.Slots;
							}
							bool flag3 = built.Rest != null && meshInstance.PartRotation == null && meshInstance.PartOffset == null && meshInstance.PartScale == null && meshInstance.Hidden == 0;
							int num12 = (flag3 ? 1 : model.Parts.Length);
							int num13 = ((!flag3 && poseTwins && num8 >= num7 && Twins(_items[num8].M, meshInstance, built)) ? num8 : (-1));
							if (num6 >= 160 && num13 < 0)
							{
								if (num5 + 2 > _chunkStart.Length)
								{
									Array.Resize(ref _chunkStart, _chunkStart.Length * 2);
								}
								_chunkStart[++num5] = num3;
								num7 = num3;
								num6 = 0;
							}
							if (!flag3 && num13 < 0)
							{
								num8 = num3;
							}
							if (num13 >= 0)
							{
								num9++;
							}
							if (num3 == _items.Length)
							{
								Array.Resize(ref _items, num3 * 2);
								Array.Resize(ref _desc, num3 * 2);
							}
							if (num4 + num12 > _mats.Length)
							{
								int newSize = Math.Max(_mats.Length * 2, num4 + num12);
								Array.Resize(ref _mats, newSize);
								Array.Resize(ref _flags, newSize);
								Array.Resize(ref _sceneAt, newSize);
								Array.Resize(ref _shadowAt, newSize);
							}
							ref Item reference = ref _items[num3++];
							reference.M = meshInstance;
							reference.B = built;
							reference.First = num4;
							reference.Twin = num13;
							reference.Fill = -1;
							reference.SceneAt = (reference.ShadowAt = -1);
							reference.ShadowFade = (meshInstance.FadeKeepsShadow ? 1f : meshInstance.Fade);
							reference.Scene = (b & 1) != 0;
							reference.Caster = (b & 2) != 0;
							reference.Rest = flag3;
							reference.Any = (reference.AnyCast = (reference.Copies = false));
							num4 += num12;
							num6 += num12;
						}
					}
					if (flag2)
					{
						workerPool.Finish();
					}
					flag2 = false;
				}
				catch
				{
					if (flag2)
					{
						workerPool.Drop();
					}
					throw;
				}
				finally
				{
					_cullList = null;
				}
				if (num3 > 0)
				{
					num5++;
				}
				if (num5 >= _chunkStart.Length)
				{
					Array.Resize(ref _chunkStart, _chunkStart.Length * 2);
				}
				_chunkStart[num5] = num3;
				_itemCount = num3;
				_chunks = num5;
				_twinItems = num9;
				if (num3 == 0)
				{
					return;
				}
				long t2 = (TimeSteps ? Stopwatch.GetTimestamp() : 0);
				_jobSides = sides;
				_jobShadowCull = jobShadowCull;
				_jobFalls = falls;
				_jobStamp = num;
				_fillCount = 0;
				into.EnsurePalettes(into.PaletteCount + num10);
				if (_fills.Length < num3)
				{
					_fills = new PaletteFill[Math.Max(num3, _fills.Length * 2)];
				}
				Vector4[] array = (_jobPalettes = into.PaletteArray);
				InstData d = default(InstData);
				long t3;
				if (workerPool == null || num5 < 2)
				{
					for (int l = 0; l < num5; l++)
					{
						PoseChunk(l, 0);
						DescribeChunk(l, into, land, isActor, sides, ref d);
					}
					t3 = (TimeSteps ? Stopwatch.GetTimestamp() : 0);
					for (int m = 0; m < num5; m++)
					{
						WriteChunk(m, 0);
					}
				}
				else
				{
					bool writeBehind = WriteBehind;
					StepsJob stepsJob = _stepsJob;
					if (stepsJob == null)
					{
						StepsJob obj3 = new StepsJob
						{
							Models = this
						};
						StepsJob stepsJob2 = obj3;
						_stepsJob = obj3;
						stepsJob = stepsJob2;
					}
					StepsJob job = stepsJob;
					workerPool.Start(job, writeBehind ? (2 * num5) : num5, int.MaxValue, num5, (writeBehind && workerPool.Workers > 2) ? 2 : 0);
					try
					{
						int num14 = 0;
						while (num14 < num5)
						{
							int chunk2;
							if (workerPool.IsDone(num14))
							{
								if (workerPool.Failed)
								{
									break;
								}
								DescribeChunk(num14, into, land, isActor, sides, ref d);
								num14++;
								if (writeBehind)
								{
									workerPool.Raise(num5 + num14);
								}
							}
							else if (workerPool.TryTake(out chunk2))
							{
								workerPool.RunHere(chunk2);
							}
							else
							{
								workerPool.Await(num14);
							}
						}
						t3 = (TimeSteps ? Stopwatch.GetTimestamp() : 0);
						workerPool.Finish();
					}
					catch
					{
						workerPool.Drop();
						throw;
					}
					if (!writeBehind)
					{
						if (num5 < 2)
						{
							WriteChunk(0, 0);
						}
						else
						{
							WorkerPool workerPool3 = workerPool;
							WriteJob writeJob = _writeJob;
							if (writeJob == null)
							{
								WriteJob obj5 = new WriteJob
								{
									Models = this
								};
								WriteJob writeJob2 = obj5;
								_writeJob = obj5;
								writeJob = writeJob2;
							}
							workerPool3.Start(writeJob, num5);
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
					}
				}
				if (into.PaletteArray != array)
				{
					_jobPalettes = into.PaletteArray;
					for (int n = 0; n < num3; n++)
					{
						if (_items[n].Fill >= 0)
						{
							Fill(ref _fills[_items[n].Fill], _jobPalettes);
						}
					}
				}
				if (TimeSteps)
				{
					NoteSteps(t, t2, t3, Stopwatch.GetTimestamp());
				}
			}
			finally
			{
				workerPool?.End();
				_jobSides = null;
				_jobPalettes = null;
				TrimTails(count);
			}
		}

		private void TrimTails(int listed)
		{
			int num = Math.Min(_itemCount, _items.Length);
			int num2 = Math.Min(listed, _found.Length);
			int num3 = Math.Min(_fillCount, _fills.Length);
			if (_itemsHigh > num)
			{
				Array.Clear(_items, num, Math.Min(_itemsHigh, _items.Length) - num);
			}
			if (_foundHigh > num2)
			{
				Array.Clear(_found, num2, Math.Min(_foundHigh, _found.Length) - num2);
			}
			if (_fillsHigh > num3)
			{
				Array.Clear(_fills, num3, Math.Min(_fillsHigh, _fills.Length) - num3);
			}
			_itemsHigh = num;
			_foundHigh = num2;
			_fillsHigh = num3;
		}

		private void CullRunOf(int r, bool here)
		{
			List<MeshInstance> cullList = _cullList;
			float cullEx = _cullEx;
			float cullEy = _cullEy;
			float cullFx = _cullFx;
			float cullFy = _cullFy;
			float cullReach = _cullReach;
			float cullScx = _cullScx;
			float cullScy = _cullScy;
			float cullShadowReach = _cullShadowReach;
			bool cullShadows = _cullShadows;
			bool cullSkip = _cullSkip;
			int i = r * 256;
			for (int num = Math.Min(cullList.Count, i + 256); i < num; i++)
			{
				MeshInstance meshInstance = cullList[i];
				byte b = 0;
				Built value = null;
				if (meshInstance != null && meshInstance.Model != null && !(meshInstance.Fade <= 0f))
				{
					MeshModel model = meshInstance.Model;
					Mat3 basis = meshInstance.Basis;
					float num2 = Math.Max(Math.Max(Len(basis.C0), Len(basis.C1)), Len(basis.C2)) * Math.Max(MaxAbs(model.Min), MaxAbs(model.Max)) + 0.5f;
					Vec3 position = meshInstance.Position;
					bool flag = false;
					bool flag2 = false;
					if (!meshInstance.ShadowOnly)
					{
						float num3 = (position.X - cullEx) * cullFx + (position.Y - cullEy) * cullFy;
						flag = num3 >= 0f - num2 && num3 - num2 <= cullReach;
					}
					float num4 = (meshInstance.FadeKeepsShadow ? 1f : meshInstance.Fade);
					if (cullShadows && !meshInstance.NoShadow && !meshInstance.Emissive && (!cullSkip || !(num4 < 0.5f)))
					{
						float num5 = position.X - cullScx;
						float num6 = position.Y - cullScy;
						float num7 = cullShadowReach + num2;
						flag2 = num5 * num5 + num6 * num6 <= num7 * num7;
					}
					if (flag || flag2)
					{
						b = (byte)((flag ? 1u : 0u) | (uint)(flag2 ? 2 : 0));
						if (!here)
						{
							_built.TryGetValue(model, out value);
						}
					}
				}
				_cull[i] = b;
				_found[i] = value;
			}
		}

		private void NoteSteps(long t0, long t1, long t2, long t3)
		{
			double num = 1000.0 / (double)Stopwatch.Frequency;
			_step1 += (double)(t1 - t0) * num;
			_step23 += (double)(t2 - t1) * num;
			_step4 += (double)(t3 - t2) * num;
			_stepCalls += 1.0;
			if (_stepsSince == 0L)
			{
				_stepsSince = t3;
			}
			if (!((double)(t3 - _stepsSince) * num < 10000.0))
			{
				double num2 = Math.Max(1.0, _stepCalls);
				Log.Info($"gpu copies steps: step 1 {_step1 / num2:F2}, steps 2+3 {_step23 / num2:F2} (describing {_step3 / num2:F2}), step 4 {_step4 / num2:F2} ms a gathering; {_stepCalls:F0} gatherings, {WorkerCount} workers, " + $"last {_itemCount} instances in {_chunks} runs, {_twinItems} twins");
				_step1 = (_step23 = (_step3 = (_step4 = (_stepCalls = 0.0))));
				_stepsSince = t3;
			}
		}

		private static bool Twins(MeshInstance src, MeshInstance m, Built b)
		{
			if (m.PartRotation != src.PartRotation || m.PartOffset != src.PartOffset || m.PartScale != src.PartScale)
			{
				return false;
			}
			if (!Same(ref m.Basis, ref src.Basis) || !Same(ref m.Position, ref src.Position))
			{
				return false;
			}
			MeshModel model = src.Model;
			if (b.TwinOf != model)
			{
				b.TwinOf = model;
				b.TwinFits = SameJoints(model, m.Model);
			}
			return b.TwinFits;
		}

		private static bool SameJoints(MeshModel a, MeshModel b)
		{
			if (a == b)
			{
				return true;
			}
			MeshPart[] parts = a.Parts;
			MeshPart[] parts2 = b.Parts;
			if (parts.Length != parts2.Length)
			{
				return false;
			}
			for (int i = 0; i < parts.Length; i++)
			{
				if (parts[i].Parent != parts2[i].Parent || !Same(ref parts[i].Pivot, ref parts2[i].Pivot))
				{
					return false;
				}
			}
			return true;
		}

		private unsafe static bool Same(ref Vec3 a, ref Vec3 b)
		{
			fixed (Vec3* ptr = &a)
			{
				fixed (Vec3* ptr2 = &b)
				{
					int* ptr3 = (int*)ptr;
					int* ptr4 = (int*)ptr2;
					if (*ptr3 == *ptr4 && ptr3[1] == ptr4[1])
					{
						return ptr3[2] == ptr4[2];
					}
					return false;
				}
			}
		}

		private unsafe static bool Same(ref Mat3 a, ref Mat3 b)
		{
			fixed (Mat3* ptr = &a)
			{
				fixed (Mat3* ptr2 = &b)
				{
					int* ptr3 = (int*)ptr;
					int* ptr4 = (int*)ptr2;
					for (int i = 0; i < 9; i++)
					{
						if (ptr3[i] != ptr4[i])
						{
							return false;
						}
					}
					return true;
				}
			}
		}

		private void PoseChunk(int chunk, int worker)
		{
			Scratch scratch = _scratch[worker];
			Vector4[] jobSides = _jobSides;
			bool jobShadowCull = _jobShadowCull;
			Item[] items = _items;
			Matrix4x4[] mats = _mats;
			byte[] flags = _flags;
			scratch.PoseOf = -1;
			int i = _chunkStart[chunk];
			for (int num = _chunkStart[chunk + 1]; i < num; i++)
			{
				ref Item reference = ref items[i];
				MeshInstance m = reference.M;
				Built b = reference.B;
				MeshModel model = m.Model;
				Mat3 basis = m.Basis;
				Vec3 pos = m.Position;
				bool flag = reference.Scene;
				bool flag2 = reference.Caster;
				if (reference.Rest)
				{
					if (jobSides != null)
					{
						Placed(b.RestSphere, ref basis, ref pos, out var x, out var y, out var z, out var r);
						if (flag && Beyond(jobSides, x, y, z, r))
						{
							flag = false;
						}
						if (flag2 && jobShadowCull && Unlit(jobSides, x, y, z, r, ref _jobFalls))
						{
							flag2 = false;
						}
						reference.Scene = flag;
						reference.Caster = flag2;
					}
					if (flag || flag2)
					{
						mats[reference.First] = Matrix(basis, pos);
					}
					continue;
				}
				int num2 = model.Parts.Length;
				int twin = reference.Twin;
				if (twin < 0 || scratch.PoseOf != twin)
				{
					if (scratch.Pm.Length < num2)
					{
						scratch.Pm = new Mat3[num2 * 2];
						scratch.Pt = new Vec3[num2 * 2];
					}
					PoseParts(m, model, scratch.Pm, scratch.Pt);
					scratch.PoseOf = ((twin >= 0) ? twin : i);
				}
				ulong num3 = Skip(m, b);
				ulong num4 = ulong.MaxValue;
				int num5 = 0;
				if (twin >= 0)
				{
					num4 = Skip(items[twin].M, items[twin].B);
					num5 = items[twin].First;
				}
				bool flag3 = false;
				bool flag4 = false;
				bool flag5 = jobSides != null;
				if (flag5)
				{
					Placed(b.RestSphere, ref basis, ref pos, out var x2, out var y2, out var z2, out var r2);
					flag5 = !Within(jobSides, x2, y2, z2, r2 * 2f);
				}
				int first = reference.First;
				for (int j = 0; j < num2; j++)
				{
					if (j < 64 && (num3 & (ulong)(1L << j)) != 0L)
					{
						continue;
					}
					int num6 = first + j;
					if (twin >= 0 && j < 64 && (num4 & (ulong)(1L << j)) == 0L)
					{
						mats[num6] = mats[num5 + j];
					}
					else
					{
						PartMatrix(scratch.Pm, scratch.Pt, ref basis, ref pos, j, ref mats[num6]);
					}
					bool flag6 = flag;
					bool flag7 = flag2;
					if (flag5)
					{
						Placed(b.PartSphere[j], ref mats[num6], out var x3, out var y3, out var z3, out var r3);
						if (flag6 && Beyond(jobSides, x3, y3, z3, r3))
						{
							flag6 = false;
						}
						if (flag7 && jobShadowCull && Unlit(jobSides, x3, y3, z3, r3, ref _jobFalls))
						{
							flag7 = false;
						}
					}
					flags[num6] = (byte)((flag6 ? 1u : 0u) | (uint)(flag7 ? 2 : 0));
					flag3 = flag3 || flag6;
					flag4 = flag4 || flag7;
				}
				reference.Any = flag3;
				reference.AnyCast = flag4;
			}
		}

		private void DescribeChunk(int chunk, GpuInstances into, TerrainCache land, Func<int, bool> isActor, Vector4[] sides, ref InstData d)
		{
			Item[] items = _items;
			byte[] flags = _flags;
			int[] sceneAt = _sceneAt;
			int[] shadowAt = _shadowAt;
			int jobStamp = _jobStamp;
			long num = (TimeSteps ? Stopwatch.GetTimestamp() : 0);
			_fillLater = true;
			try
			{
				int i = _chunkStart[chunk];
				for (int num2 = _chunkStart[chunk + 1]; i < num2; i++)
				{
					ref Item reference = ref items[i];
					MeshInstance m = reference.M;
					Built b = reference.B;
					MeshModel model = m.Model;
					int callItems = b.CallItems;
					if (reference.Rest)
					{
						if (!reference.Scene && !reference.Caster)
						{
							continue;
						}
						reference.Copies = true;
						if (reference.Scene)
						{
							int fillCount = _fillCount;
							Describe(ref d, m, model, b, land, overlay: false, isActor, into);
							if (_fillCount > fillCount)
							{
								reference.Fill = fillCount;
							}
							_desc[i] = d;
							reference.SceneAt = GpuInstances.ReserveWithin(b.RestScene ?? (b.RestScene = into.For(1, b.Rest)), jobStamp, callItems);
							Drawn++;
						}
						if (reference.Caster)
						{
							reference.ShadowAt = GpuInstances.ReserveWithin(b.RestShadow ?? (b.RestShadow = into.For(0, b.Rest)), jobStamp, callItems);
						}
						continue;
					}
					bool flag = ((sides == null) ? reference.Scene : reference.Any);
					if (!flag && !reference.AnyCast)
					{
						continue;
					}
					reference.Copies = true;
					if (flag)
					{
						int fillCount2 = _fillCount;
						Describe(ref d, m, model, b, land, overlay: false, isActor, into);
						if (_fillCount > fillCount2)
						{
							reference.Fill = fillCount2;
						}
						_desc[i] = d;
						Drawn++;
					}
					GpuInstances.Group[] partScene = b.PartScene;
					GpuInstances.Group[] partShadow = b.PartShadow;
					Mesh[] parts = b.Parts;
					int num3 = model.Parts.Length;
					int first = reference.First;
					ulong num4 = Skip(m, b);
					for (int j = 0; j < num3; j++)
					{
						if (j >= 64 || (num4 & (ulong)(1L << j)) == 0L)
						{
							int num5 = first + j;
							byte num6 = flags[num5];
							int num7 = -1;
							if ((num6 & 1) != 0)
							{
								GpuInstances.Group obj = partScene[j] ?? (partScene[j] = into.For(1, parts[j]));
								num7 = ((obj.Ensured == jobStamp && obj.Count < obj.Items.Length) ? obj.Count++ : GpuInstances.ReserveWithin(obj, jobStamp, callItems));
							}
							sceneAt[num5] = num7;
							num7 = -1;
							if ((num6 & 2) != 0)
							{
								GpuInstances.Group obj2 = partShadow[j] ?? (partShadow[j] = into.For(0, parts[j]));
								num7 = ((obj2.Ensured == jobStamp && obj2.Count < obj2.Items.Length) ? obj2.Count++ : GpuInstances.ReserveWithin(obj2, jobStamp, callItems));
							}
							shadowAt[num5] = num7;
						}
					}
				}
			}
			finally
			{
				_fillLater = false;
				if (TimeSteps)
				{
					_step3 += (double)(Stopwatch.GetTimestamp() - num) * 1000.0 / (double)Stopwatch.Frequency;
				}
			}
		}

		private void FillLater(int at, int slots, Vector4[] palette, MeshModel model, uint[] colours)
		{
			if (_fillCount == _fills.Length)
			{
				throw new InvalidOperationException("gpu copies: more palette fills than made room for");
			}
			ref PaletteFill reference = ref _fills[_fillCount++];
			reference.At = at;
			reference.Slots = slots;
			reference.Base = palette;
			reference.Colours = colours;
			reference.Model = model;
		}

		private static bool OwnColours(MeshInstance m, int slots)
		{
			uint[] palette = m.Palette;
			if (palette == null)
			{
				return false;
			}
			for (int i = 0; i < palette.Length && i < slots; i++)
			{
				if (palette[i] != 0)
				{
					return true;
				}
			}
			return false;
		}

		private static void Fill(ref PaletteFill f, Vector4[] pal)
		{
			int at = f.At;
			int slots = f.Slots;
			Array.Copy(f.Base, 0, pal, at, slots);
			uint[] colours = f.Colours;
			MeshModel model = f.Model;
			for (int i = 0; i < colours.Length && i < slots; i++)
			{
				if (colours[i] != 0)
				{
					pal[at + i] = Colour(colours[i], model.SlotKind[i], model.SlotFlags[i]);
				}
			}
		}

		private void WriteChunk(int chunk, int worker)
		{
			Item[] items = _items;
			Matrix4x4[] mats = _mats;
			int[] sceneAt = _sceneAt;
			int[] shadowAt = _shadowAt;
			Vector4[] jobPalettes = _jobPalettes;
			int i = _chunkStart[chunk];
			for (int num = _chunkStart[chunk + 1]; i < num; i++)
			{
				ref Item reference = ref items[i];
				if (!reference.Copies)
				{
					continue;
				}
				if (reference.Fill >= 0)
				{
					Fill(ref _fills[reference.Fill], jobPalettes);
				}
				Built b = reference.B;
				Vector4 misc = new Vector4(reference.ShadowFade, 0f, 0f, 0f);
				if (reference.Rest)
				{
					if (reference.SceneAt >= 0)
					{
						ref InstData reference2 = ref b.RestScene.Items[reference.SceneAt];
						reference2 = _desc[i];
						reference2.M = mats[reference.First];
					}
					if (reference.ShadowAt >= 0)
					{
						ShadowCopy(ref b.RestShadow.Items[reference.ShadowAt], ref mats[reference.First], misc);
					}
					continue;
				}
				GpuInstances.Group[] partScene = b.PartScene;
				GpuInstances.Group[] partShadow = b.PartShadow;
				int num2 = reference.M.Model.Parts.Length;
				int first = reference.First;
				ulong num3 = Skip(reference.M, b);
				ulong hidden = ((b.Merged != 0L) ? reference.M.Hidden : 0);
				for (int j = 0; j < num2; j++)
				{
					if (j < 64 && (num3 & (ulong)(1L << j)) != 0L)
					{
						continue;
					}
					int num4 = first + j;
					int num5 = sceneAt[num4];
					if (num5 >= 0)
					{
						ref InstData reference3 = ref partScene[j].Items[num5];
						reference3 = _desc[i];
						reference3.M = mats[num4];
						if (b.Merged != 0L)
						{
							Pieces(ref reference3.M, hidden);
						}
					}
					num5 = shadowAt[num4];
					if (num5 >= 0)
					{
						if (b.Merged != 0L)
						{
							ShadowCopy(ref partShadow[j].Items[num5], mats[num4], misc, hidden);
						}
						else
						{
							ShadowCopy(ref partShadow[j].Items[num5], ref mats[num4], misc);
						}
					}
				}
			}
		}

		internal static string PosingDiffersForTest(MeshInstance m, MeshModel model, Mat3 basis, Vec3 pos)
		{
			int num = model.Parts.Length;
			if (_checkPm.Length < num)
			{
				_checkPm = new Mat3[num];
				_checkPt = new Vec3[num];
				_refPm = new Mat3[num];
				_refPt = new Vec3[num];
			}
			PoseParts(m, model, _checkPm, _checkPt);
			int num2 = ((m.PartRotation != null) ? m.PartRotation.Length : 0);
			int num3 = ((m.PartScale != null) ? m.PartScale.Length : 0);
			int num4 = ((m.PartOffset != null) ? m.PartOffset.Length : 0);
			for (int i = 0; i < num; i++)
			{
				MeshPart meshPart = model.Parts[i];
				Mat3 a = ((i < num2) ? m.PartRotation[i] : Mat3.Identity);
				float num5 = ((i < num3) ? m.PartScale[i] : 1f);
				if (num5 != 1f)
				{
					a *= num5;
				}
				Vec3 vec = meshPart.Pivot - a.Mul(meshPart.Pivot);
				if (i < num4)
				{
					vec += m.PartOffset[i];
				}
				if (meshPart.Parent >= 0)
				{
					Mat3 a2 = _refPm[meshPart.Parent];
					_refPm[i] = a2 * a;
					_refPt[i] = a2.Mul(vec) + _refPt[meshPart.Parent];
				}
				else
				{
					_refPm[i] = a;
					_refPt[i] = vec;
				}
			}
			for (int j = 0; j < num; j++)
			{
				Matrix4x4 mm = default(Matrix4x4);
				PartMatrix(_checkPm, _checkPt, ref basis, ref pos, j, ref mm);
				Matrix4x4 matrix4x = Matrix(basis * _refPm[j], basis.Mul(_refPt[j]) + pos);
				for (int k = 0; k < 16; k++)
				{
					float num6 = mm[k];
					float num7 = matrix4x[k];
					if (Bits(num6) != Bits(num7))
					{
						return $"part {j} element {k}: {num6:R} vs {num7:R} by the operators";
					}
				}
			}
			return null;
		}

		private unsafe static int Bits(float f)
		{
			return *(int*)(&f);
		}
	}
}
