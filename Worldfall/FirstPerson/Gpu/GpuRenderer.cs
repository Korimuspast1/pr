using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using FirstPerson.Core;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuRenderer : IDisposable
	{
		private struct CopyArgs
		{
			public bool Set;

			public bool Shadows;

			public bool Overlay;

			public ViewState Cull;

			public float Reach;

			public float VoxelReach;

			public float ShadowReach;

			public Vector2 ShadowCentre;

			public Matrix4x4 ViewProj;

			public GpuModels.ShadowFall Fall;
		}

		private struct RoomStats
		{
			public bool On;

			public int Lights;

			public int Cubes;

			public int Shadowed;

			public int Face;

			public int Windows;

			public int Bakes;

			public int Copies;

			public int Passes;

			public Vector4 Fill;

			public double BakeMs;
		}

		internal static class RoomLights
		{
			private static readonly Dictionary<string, float> Defaults = new Dictionary<string, float>
			{
				{ "room_fill_day", 0.22f },
				{ "room_fill_night", 0.07f },
				{ "room_bounce", 0.05f },
				{ "room_bounce_most", 0.12f },
				{ "room_light", 2.2f },
				{ "room_falloff", 2f },
				{ "room_wrap", 0.3f },
				{ "room_window", 3f },
				{ "room_beam", 1.7f },
				{ "room_hands", 1.5f },
				{ "room_shadows", 1f },
				{ "room_shadow_size", 256f },
				{ "room_lamp_clear", 0.14f },
				{ "room_bias", 0.03f },
				{ "room_soft", 0.08f },
				{ "room_debug", 0f },
				{ "room_debug_slot", 0f }
			};

			private static readonly Dictionary<string, float> Values = new Dictionary<string, float>(Defaults);

			private static string _path;

			private static DateTime _stamp = DateTime.MaxValue;

			private static float _nextCheck = -1f;

			public static float Get(string key)
			{
				if (!(key == "room_shadows") || !LightMode.NoRoomShadows)
				{
					if (!Values.TryGetValue(key, out var value))
					{
						return 0f;
					}
					return value;
				}
				return 0f;
			}

			public static void Read()
			{
				if (Time.unscaledTime < _nextCheck && _stamp != DateTime.MaxValue)
				{
					return;
				}
				_nextCheck = Time.unscaledTime + 1f;
				if (_path == null)
				{
					_path = Path.Combine(Log.Folder, "gpu_style.txt");
				}
				DateTime dateTime = DateTime.MinValue;
				try
				{
					if (File.Exists(_path))
					{
						dateTime = File.GetLastWriteTimeUtc(_path);
					}
				}
				catch
				{
				}
				if (dateTime == _stamp)
				{
					return;
				}
				_stamp = dateTime;
				foreach (KeyValuePair<string, float> @default in Defaults)
				{
					Values[@default.Key] = @default.Value;
				}
				if (dateTime == DateTime.MinValue)
				{
					return;
				}
				try
				{
					string[] array = File.ReadAllLines(_path);
					for (int i = 0; i < array.Length; i++)
					{
						string text = array[i];
						int num = text.IndexOf('#');
						if (num >= 0)
						{
							text = text.Substring(0, num);
						}
						int num2 = text.IndexOf('=');
						if (num2 > 0)
						{
							string key = text.Substring(0, num2).Trim();
							if (Defaults.ContainsKey(key) && float.TryParse(text.Substring(num2 + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
							{
								Values[key] = result;
							}
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("gpu: room light style: " + ex.Message);
				}
			}
		}

		private struct TexelCount
		{
			private Vec3 _c;

			private Vec3 _a;

			private Vec3 _b;

			private float _k;

			private float _kb;

			private bool _set;

			public int A;

			public int B;

			public void Next(Vec3 eye, Vec3 c, Vec3 a, Vec3 b, float k, float kb)
			{
				if (_set)
				{
					A += (int)Math.Round(Along(eye, _c, _a) * (double)_k - Along(eye, c, a) * (double)k);
					B += (int)Math.Round(Along(eye, _c, _b) * (double)_kb - Along(eye, c, b) * (double)kb);
				}
				_c = c;
				_a = a;
				_b = b;
				_k = k;
				_kb = kb;
				_set = true;
			}

			private static double Along(Vec3 p, Vec3 c, Vec3 a)
			{
				return ((double)p.X - (double)c.X) * (double)a.X + ((double)p.Y - (double)c.Y) * (double)a.Y + ((double)p.Z - (double)c.Z) * (double)a.Z;
			}
		}

		internal static int BenchFrames;

		internal static float BenchMs = -1f;

		private GpuModels.RefInstances _ref;

		private readonly List<(Mesh mesh, int start, int count)> _drawsNew = new List<(Mesh, int, int)>();

		private readonly List<(Mesh mesh, int start, int count)> _drawsOld = new List<(Mesh, int, int)>();

		private readonly RenderTargetIdentifier[] _sceneTargets = new RenderTargetIdentifier[2];

		private readonly RenderTargetIdentifier[] _veilTargets = new RenderTargetIdentifier[2];

		private static readonly string[] BlockNames = new string[4] { "_Blocks1", "_Blocks2", "_Blocks3", "_Blocks4" };

		public int Width;

		public int Height;

		public int Supersample = 2;

		public RenderTexture Output;

		public int PickTag = -1;

		public float PickDepth = float.MaxValue;

		public string Error;

		private readonly GpuTerrain _worldTerrain = new GpuTerrain();

		private readonly GpuTerrain _roomTerrain = new GpuTerrain();

		public bool Room;

		public Texture Backdrop;

		public bool NoPick;

		private readonly GpuSnow _snow = new GpuSnow();

		private readonly GpuModels _models = new GpuModels();

		private readonly GpuInstances _instances = new GpuInstances();

		private readonly GpuVoxels _voxels = new GpuVoxels();

		private readonly GpuSprites _sprites = new GpuSprites();

		private readonly GpuSpecks _specks = new GpuSpecks();

		private readonly GpuWeather _weather = new GpuWeather();

		private readonly GpuNightSky _nightSky = new GpuNightSky();

		private RenderTexture _sceneCol;

		private RenderTexture _sceneData;

		private RenderTexture _aired;

		private RenderTexture _painted;

		private RenderTexture _shadow;

		private RenderTexture _glowA;

		private RenderTexture _glowB;

		private RenderTexture _glowC;

		private RenderTexture _front;

		public static float EyeShadowReach = 64f;

		public float ShadowReach = EyeShadowReach;

		public int ShadowSize = 2048;

		public int ShadowQuality = 2;

		public int ShadowSoftness;

		public float ShadowDarkness = 1f;

		public const float NearShadowReach = 32f;

		private const float SoftShadow = 0.06f;

		private const float SunSpread = 0.03f;

		private bool _cascades;

		private const int MostSceneWidth = 2560;

		private Material _skyMat;

		private Material _terrainMat;

		private Material _styleMat;

		private Material _modelMat;

		private Material _spriteMat;

		private Material _specksMat;

		private Material _weatherMat;

		private Material _modelMatCulled;

		private static readonly int ModelCullId = Shader.PropertyToID("_FP_ModelCull");

		private static readonly int SkyZTestId = Shader.PropertyToID("_FP_SkyZTest");

		internal static CullMode ModelCull = CullMode.Front;

		internal static bool SkyLast = true;

		private int _shaderVersion = -1;

		private CommandBuffer _cmd;

		private Texture2D _cloud;

		private float _flipY = 1f;

		private FrameProjection _projection;

		private FrameRenderer _isActorCfg;

		private Func<int, bool> _isActor;

		private CopyArgs _copyArgs;

		private readonly Vector4[] _sides = new Vector4[4];

		private TerrainCache _land;

		private readonly Vector4[] _wakes = new Vector4[16];

		private int _presentW;

		private int _presentH;

		private RenderTexture _present;

		private bool _presented;

		private bool _pickPending;

		public int FrameNumber;

		public int PickFrame;

		private readonly Stopwatch _clock = Stopwatch.StartNew();

		private double _setupMs;

		private double _collectMs;

		private double _uploadMs;

		private double _spritesMs;

		private double _restMs;

		private double _execMs;

		private int _timed;

		private const float ShadowLift = 150f;

		private const float ShadowNear = 0.1f;

		private const float ShadowDepthRange = 320f;

		private Vector3 _lightAx;

		private Vector3 _lightBx;

		private Vector3 _lightL;

		private Vector3 _shadowEye;

		private Matrix4x4 _nearVP;

		private Matrix4x4 _farVP;

		private const int MaxLights = 64;

		private readonly Vector4[] _lights = new Vector4[128];

		private readonly List<PointLight> _nearLights = new List<PointLight>();

		private Vector3 _lightEye;

		private const int MaxWindows = 16;

		private readonly Vector4[] _windows = new Vector4[48];

		public bool ReadDepth;

		public float[] DepthPixels = new float[0];

		public int DepthFrame = -1;

		public int DepthWidth;

		public int DepthHeight;

		private RenderTexture _depthOut;

		private int _depthPending;

		public const int MostDepthWidth = 640;

		public bool HasEye;

		public EyeCamera Eye;

		public int EyeSupersample;

		public bool Stereo;

		public Vector3 StereoHead;

		public bool ShadowsKept;

		public int StereoPair;

		private int _shadowsDrawnPair = -1;

		internal int ShadowMapsDrawn;

		public const float SteepLeast = 0.05f;

		public float SteepBehind = 4f;

		private static readonly List<(Mesh mesh, int start, int count)> DrawList = new List<(Mesh, int, int)>();

		public bool PixelFar;

		private readonly RenderTexture[] _blocks = new RenderTexture[4];

		private readonly int[] _blockSize = new int[4];

		private int _blockLevels;

		private Vector4 _pixel;

		private Vector4 _pixelGrid;

		private Vector4 _pixelSize;

		public Interior RoomShape;

		public float RoomDaylight = 1f;

		private const int MostShadowed = 16;

		private RenderTexture _roomShadow;

		private int _roomFace;

		private long _roomKey = long.MinValue;

		private long _pendingKey;

		private bool _bakeRoom;

		private readonly List<Vector4> _shadowed = new List<Vector4>();

		private readonly Vector4[] _slots = new Vector4[16];

		private readonly List<bool> _castWas = new List<bool>();

		private int _bakes;

		private int _bakeCopies;

		private double _bakeMs;

		private RoomStats _roomStats;

		internal static bool OldRoomLightForTest;

		private static readonly Vector3[] FaceForward = new Vector3[6]
		{
			Vector3.right,
			Vector3.left,
			Vector3.up,
			Vector3.down,
			Vector3.forward,
			Vector3.back
		};

		private bool _headsetFrame;

		private bool _classicFinish;

		private Vector4 _flare;

		private RenderTexture _flareOpen;

		public bool NaturalSun;

		public bool LensFlare;

		private readonly GpuSunField _sunField = new GpuSunField();

		private const float ShadowFade = 0.12f;

		private const float CascadeBand = 3f;

		private const float GatherMargin = 4f;

		public bool ContactShade = true;

		private const float ContactStrength = 0.4f;

		private const float ContactReach = 0.6f;

		private const float CreaseShade = 0.25f;

		private const float StandsTallest = 12f;

		private float _upHeld;

		private float _up0Held;

		private FrameRenderer.TexelAnchor _wideAnchor;

		private FrameRenderer.TexelAnchor _nearAnchor;

		private TexelCount _wideCount;

		private TexelCount _nearCount;

		private RenderTexture _veilCol;

		private RenderTexture _veilData;

		internal bool CopiesNoted
		{
			get
			{
				if (_copyArgs.Set)
				{
					return _land != null;
				}
				return false;
			}
		}

		private GpuTerrain _terrain
		{
			get
			{
				if (!Room)
				{
					return _worldTerrain;
				}
				return _roomTerrain;
			}
		}

		internal GpuTerrain TerrainForTest => _terrain;

		public GpuTerrain Terrain => _worldTerrain;

		public FrameProjection Projection => _projection;

		public bool Available
		{
			get
			{
				if (GpuShaders.Ready && SystemInfo.supportsComputeShaders)
				{
					return GpuSupport.Missing == null;
				}
				return false;
			}
		}

		public RenderTexture Shown
		{
			get
			{
				if (!_presented || !(_present != null))
				{
					return Output;
				}
				return _present;
			}
		}

		public bool Presented
		{
			get
			{
				if (_presented)
				{
					return _present != null;
				}
				return false;
			}
		}

		public int FarPropsDrawn => _specks.Props;

		public int FarPlainDrawn => _specks.Plain;

		public bool CrowdsReady
		{
			get
			{
				if (Available)
				{
					return GpuModels.ShaderDrawsCrowds();
				}
				return false;
			}
		}

		public string Stats => $"{Width}x{Height} x{Supersample}, chunks {_terrain.ChunksDrawn}, land rows {_terrain.RowsUploaded}, models {_models.Drawn} ({_instances.Instances} copies, {_instances.Draws} draws), crowd {_models.CrowdDrawn}, voxels {_voxels.Drawn}, sprites {_sprites.Drawn}, far {_specks.Props}+{_specks.Actors} ({_specks.Plain} plain, {_specks.Looks} looks), trails {_specks.Trails}";

		public int PixelLevels => _blockLevels;

		public string RoomLightStats
		{
			get
			{
				RoomStats roomStats = _roomStats;
				if (!roomStats.On)
				{
					return null;
				}
				return $"room light: {roomStats.Lights} lights ({roomStats.Cubes} with shadow cubes of {roomStats.Shadowed}, {roomStats.Face} px), {roomStats.Windows} windows, fill {roomStats.Fill.x:F2}/{roomStats.Fill.y:F2}/{roomStats.Fill.z:F2}, " + $"cubes drawn {roomStats.Bakes}x (last {roomStats.BakeMs:F1} ms to record, {roomStats.Copies} casters, model passes {roomStats.Passes})";
			}
		}

		public string SunFieldTimes => $"half floats {_sunField.ConvertMs:F1} ms (worker), sent up {_sunField.UploadMs:F1} ms, {_sunField.Uploads} sent";

		private void BenchAfterFrame(CommandBuffer cmd)
		{
			int benchFrames = BenchFrames;
			if (benchFrames <= 0)
			{
				return;
			}
			BenchFrames = 0;
			BenchMs = -1f;
			RenderTexture renderTexture = ((Output != null) ? Output : _sceneCol);
			if (!(renderTexture == null) && SystemInfo.supportsAsyncGPUReadback)
			{
				AsyncGPUReadback.Request(renderTexture, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
				Stopwatch stopwatch = Stopwatch.StartNew();
				for (int i = 0; i < benchFrames; i++)
				{
					Graphics.ExecuteCommandBuffer(cmd);
				}
				AsyncGPUReadback.Request(renderTexture, 0, 0, 1, 0, 1, 0, 1).WaitForCompletion();
				BenchMs = (float)(stopwatch.Elapsed.TotalMilliseconds / (double)benchFrames);
			}
		}

		private static Func<int, bool> ActorTest(FrameRenderer cfg)
		{
			return (int tag) => cfg.ActorClarity && tag >= 0 && tag < cfg.ActorTagEnd;
		}

		private void RefCollectFrame(FrameRenderer cfg, TerrainCache land, List<VoxelObject> voxels, in CopyArgs a, Func<int, bool> isActor)
		{
			if (_ref == null)
			{
				_ref = new GpuModels.RefInstances();
			}
			_ref.Begin();
			if (a.Shadows && voxels != null)
			{
				_voxels.ReferenceCollect(_ref, 0, voxels, a.Cull, 0f, null, a.ShadowCentre, a.ShadowReach * 1.25f);
			}
			_models.ReferenceCollect(_ref, cfg.Meshes, land, a.Cull, a.Reach, isActor, a.Shadows, a.ShadowCentre, a.ShadowReach * 1.25f);
			if (voxels != null)
			{
				_voxels.ReferenceCollect(_ref, 1, voxels, a.Cull, a.VoxelReach, isActor);
			}
			if (a.Overlay)
			{
				_models.ReferenceCollectOverlay(_ref, cfg.OverlayMeshes, land, null);
				if (voxels != null)
				{
					_voxels.ReferenceCollect(_ref, 2, voxels, a.Cull, 1E+09f, null);
				}
			}
		}

		internal string CompareCopies(FrameRenderer cfg, List<VoxelObject> voxels)
		{
			if (!CopiesNoted)
			{
				return "no frame drawn yet";
			}
			Func<int, bool> isActor = ActorTest(cfg);
			_models.ForgetLightForTest();
			bool skipUnseen = GpuModels.SkipUnseen;
			GpuModels.SkipUnseen = false;
			try
			{
				CollectCopies(cfg, _land, voxels, in _copyArgs, isActor);
			}
			finally
			{
				GpuModels.SkipUnseen = skipUnseen;
			}
			_instances.Lay();
			RefCollectFrame(cfg, _land, voxels, in _copyArgs, isActor);
			_ref.Lay();
			_instances.LaidForTest(out var all, out var count, out var palette, out var entries);
			if (count != _ref.Instances)
			{
				return $"copies {count} vs {_ref.Instances} before";
			}
			if (entries != _ref.Palette.Count)
			{
				return $"palette entries {entries} vs {_ref.Palette.Count} before";
			}
			int word;
			int diffs;
			int num = FirstDiff(all, _ref.All, count, out word, out diffs);
			if (num >= 0)
			{
				return $"{diffs} copies differ, the first at {num} in {Field(word)}";
			}
			for (int i = 0; i < entries; i++)
			{
				if (!SameBits(palette[i], _ref.Pal[i]))
				{
					return $"palette entry {i} differs";
				}
			}
			for (int j = 0; j < 3; j++)
			{
				_drawsNew.Clear();
				_drawsOld.Clear();
				_instances.DrawsForTest(j, _drawsNew);
				_ref.Draws(j, _drawsOld);
				if (_drawsNew.Count != _drawsOld.Count)
				{
					return $"set {j}: {_drawsNew.Count} draws vs {_drawsOld.Count} before";
				}
				for (int k = 0; k < _drawsNew.Count; k++)
				{
					if ((object)_drawsNew[k].mesh != _drawsOld[k].mesh || _drawsNew[k].start != _drawsOld[k].start || _drawsNew[k].count != _drawsOld[k].count)
					{
						return $"set {j}: draw {k} differs ({_drawsNew[k].mesh?.name} {_drawsNew[k].start}+{_drawsNew[k].count} vs {_drawsOld[k].mesh?.name} {_drawsOld[k].start}+{_drawsOld[k].count})";
					}
				}
			}
			return null;
		}

		private static bool SameBits(Vector4 a, Vector4 b)
		{
			if (BitConverter.ToInt32(BitConverter.GetBytes(a.x), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b.x), 0) && BitConverter.ToInt32(BitConverter.GetBytes(a.y), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b.y), 0) && BitConverter.ToInt32(BitConverter.GetBytes(a.z), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b.z), 0))
			{
				return BitConverter.ToInt32(BitConverter.GetBytes(a.w), 0) == BitConverter.ToInt32(BitConverter.GetBytes(b.w), 0);
			}
			return false;
		}

		private unsafe static int FirstDiff(InstData[] a, InstData[] b, int n, out int word, out int diffs)
		{
			int num = -1;
			word = -1;
			diffs = 0;
			fixed (InstData* ptr = a)
			{
				fixed (InstData* ptr2 = b)
				{
					int* ptr3 = (int*)ptr;
					int* ptr4 = (int*)ptr2;
					for (int i = 0; i < n; i++)
					{
						for (int j = 0; j < 40; j++)
						{
							if (ptr3[i * 40 + j] != ptr4[i * 40 + j])
							{
								if (num < 0)
								{
									num = i;
									word = j;
								}
								diffs++;
								break;
							}
						}
					}
				}
			}
			return num;
		}

		private static string Field(int word)
		{
			if (word >= 16)
			{
				if (word >= 20)
				{
					if (word >= 24)
					{
						if (word >= 28)
						{
							if (word >= 32)
							{
								if (word >= 36)
								{
									return "Pal";
								}
								return "Ground";
							}
							return "Tag";
						}
						return "Misc";
					}
					return "Mult";
				}
				return "Flash";
			}
			return "M";
		}

		internal string ProfileCopies(FrameRenderer cfg, List<VoxelObject> voxels, int rounds)
		{
			if (!CopiesNoted)
			{
				return "no frame drawn yet";
			}
			Func<int, bool> isActor = ActorTest(cfg);
			List<double>[] array = new List<double>[11];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new List<double>(rounds);
			}
			double num = 1000.0 / (double)Stopwatch.Frequency;
			bool skipUnseen = GpuModels.SkipUnseen;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			for (int j = 0; j < rounds; j++)
			{
				GpuModels.SkipUnseen = false;
				long timestamp = Stopwatch.GetTimestamp();
				CollectCopies(cfg, _land, voxels, in _copyArgs, isActor);
				_instances.Lay();
				num2 = _instances.Instances;
				GpuModels.SkipUnseen = skipUnseen;
				long timestamp2 = Stopwatch.GetTimestamp();
				CollectCopies(cfg, _land, voxels, in _copyArgs, isActor);
				long timestamp3 = Stopwatch.GetTimestamp();
				_instances.Lay();
				num4 = _instances.Instances;
				long timestamp4 = Stopwatch.GetTimestamp();
				RefCollectFrame(cfg, _land, voxels, in _copyArgs, isActor);
				long timestamp5 = Stopwatch.GetTimestamp();
				_ref.Lay();
				num3 = _ref.Instances;
				long timestamp6 = Stopwatch.GetTimestamp();
				array[0].Add((double)(timestamp3 - timestamp2) * num);
				array[1].Add((double)(timestamp4 - timestamp3) * num);
				array[2].Add((double)(timestamp5 - timestamp4) * num);
				array[3].Add((double)(timestamp6 - timestamp5) * num);
				array[7].Add((double)(timestamp2 - timestamp) * num);
				for (int k = 1; k <= 3; k++)
				{
					_instances.Begin();
					long timestamp7 = Stopwatch.GetTimestamp();
					_models.StagedCollect(k, _instances, cfg.Meshes, _land, _copyArgs.Cull, _copyArgs.Reach, isActor, _copyArgs.Shadows, _copyArgs.ShadowCentre, _copyArgs.ShadowReach * 1.25f, Sides(_copyArgs.ViewProj), _copyArgs.Fall);
					array[7 + k].Add((double)(Stopwatch.GetTimestamp() - timestamp7) * num);
				}
				for (int l = 1; l <= 3; l++)
				{
					_models.RefMode = l;
					_ref.Begin();
					long timestamp8 = Stopwatch.GetTimestamp();
					_models.ReferenceCollect(_ref, cfg.Meshes, _land, _copyArgs.Cull, _copyArgs.Reach, isActor, _copyArgs.Shadows, _copyArgs.ShadowCentre, _copyArgs.ShadowReach * 1.25f);
					array[3 + l].Add((double)(Stopwatch.GetTimestamp() - timestamp8) * num);
					_models.RefMode = 0;
				}
			}
			GpuModels.SkipUnseen = skipUnseen;
			string[] array2 = new string[11]
			{
				"now", "now laid", "old", "old laid", "old cull", "old described", "old posed", "now keeping all (laid too)", "now described", "now posed",
				"now tested"
			};
			StringBuilder stringBuilder = new StringBuilder();
			for (int m = 0; m < array.Length; m++)
			{
				array[m].Sort();
				stringBuilder.Append((m == 0) ? "" : ", ").Append(array2[m]).Append(' ')
					.Append(array[m][array[m].Count / 2].ToString("F2"))
					.Append(" (")
					.Append(array[m][0].ToString("F2"))
					.Append(')');
			}
			stringBuilder.Append($"; {num4} copies now ({num2} keeping all), {num3} before; describing: {_models.ProfileDescribe(_instances, cfg.Meshes, _land)}");
			return stringBuilder.ToString();
		}

		internal byte[] RedrawForTest(FrameRenderer cfg, List<Billboard> billboards, List<VoxelObject> voxels, bool classic, bool skipUnseen)
		{
			if (!CopiesNoted)
			{
				return null;
			}
			bool skipUnseen2 = GpuModels.SkipUnseen;
			float shadowReach = ShadowReach;
			GpuModels.SkipUnseen = skipUnseen;
			ShadowReach = _copyArgs.ShadowReach;
			try
			{
				if (!Render(cfg, _land, _projection.View, Width, Height, billboards, voxels, classic))
				{
					return null;
				}
			}
			finally
			{
				GpuModels.SkipUnseen = skipUnseen2;
				ShadowReach = shadowReach;
			}
			byte[] array = Read(_sceneCol, TextureFormat.RGBAHalf);
			byte[] array2 = Read(_sceneData, TextureFormat.RGBAFloat);
			byte[] array3 = Read(Output, TextureFormat.RGBA32);
			byte[] array4 = new byte[array.Length + array2.Length + array3.Length];
			Buffer.BlockCopy(array, 0, array4, 0, array.Length);
			Buffer.BlockCopy(array2, 0, array4, array.Length, array2.Length);
			Buffer.BlockCopy(array3, 0, array4, array.Length + array2.Length, array3.Length);
			return array4;
		}

		private static byte[] Read(RenderTexture rt, TextureFormat format)
		{
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			Texture2D texture2D = new Texture2D(rt.width, rt.height, format, mipChain: false, linear: true);
			texture2D.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0, recalculateMipMaps: false);
			texture2D.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			byte[] rawTextureData = texture2D.GetRawTextureData();
			UnityEngine.Object.Destroy(texture2D);
			return rawTextureData;
		}

		internal string CopyStats(FrameRenderer cfg)
		{
			if (!CopiesNoted)
			{
				return "no frame drawn yet";
			}
			CopyArgs copyArgs = _copyArgs;
			Matrix4x4 viewProj = copyArgs.ViewProj;
			Vector4 row = viewProj.GetRow(0);
			Vector4 row2 = viewProj.GetRow(1);
			Vector4 row3 = viewProj.GetRow(3);
			Vector4[] array = new Vector4[4]
			{
				row3 + row,
				row3 - row,
				row3 + row2,
				row3 - row2
			};
			for (int i = 0; i < array.Length; i++)
			{
				float magnitude = new Vector3(array[i].x, array[i].y, array[i].z).magnitude;
				if (magnitude > 0f)
				{
					array[i] /= magnitude;
				}
			}
			float num = Mathf.Cos(copyArgs.Cull.Yaw);
			float num2 = Mathf.Sin(copyArgs.Cull.Yaw);
			float num3 = copyArgs.ShadowReach * 1.25f;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			int num8 = 0;
			int num9 = 0;
			int num10 = 0;
			int num11 = 0;
			int num12 = 0;
			int num13 = 0;
			int num14 = 0;
			int num15 = 0;
			int num16 = 0;
			int num17 = 0;
			foreach (MeshInstance mesh in cfg.Meshes)
			{
				if (mesh == null || mesh.Model == null || mesh.Fade <= 0f)
				{
					continue;
				}
				MeshModel model = mesh.Model;
				Mat3 basis = mesh.Basis;
				float num18 = Mathf.Max(Mathf.Max(Len(basis.C0), Len(basis.C1)), Len(basis.C2)) * Mathf.Max(MaxAbs(model.Min), MaxAbs(model.Max)) + 0.5f;
				Vec3 position = mesh.Position;
				bool flag = false;
				bool flag2 = false;
				if (!mesh.ShadowOnly)
				{
					float num19 = (position.X - copyArgs.Cull.X) * num + (position.Y - copyArgs.Cull.Y) * num2;
					flag = num19 >= 0f - num18 && num19 - num18 <= copyArgs.Reach;
				}
				if (copyArgs.Shadows && !mesh.NoShadow && !mesh.Emissive)
				{
					float num20 = position.X - copyArgs.ShadowCentre.x;
					float num21 = position.Y - copyArgs.ShadowCentre.y;
					float num22 = num3 + num18;
					flag2 = num20 * num20 + num21 * num21 <= num22 * num22;
				}
				if (!flag && !flag2)
				{
					continue;
				}
				bool flag3 = mesh.PartRotation == null && mesh.PartOffset == null && mesh.PartScale == null && mesh.Hidden == 0;
				int num23 = (flag3 ? 1 : model.Parts.Length);
				if (flag)
				{
					num4++;
					num8 += num23;
				}
				if (flag2)
				{
					num5++;
					num9 += num23;
				}
				if (mesh.ShadowOnly)
				{
					num16++;
				}
				if (flag3)
				{
					num17++;
				}
				else
				{
					num6++;
					num7 += num23;
				}
				if (Models.IsDistant(model))
				{
					num10++;
				}
				if (mesh.Static)
				{
					num11++;
				}
				float num24 = Mathf.Sqrt(Dot(basis.C0) + Dot(basis.C1) + Dot(basis.C2));
				float num25 = Mathf.Max(Mathf.Abs(model.Min.X), Mathf.Abs(model.Max.X));
				float num26 = Mathf.Max(Mathf.Abs(model.Min.Y), Mathf.Abs(model.Max.Y));
				float num27 = Mathf.Max(Mathf.Abs(model.Min.Z), Mathf.Abs(model.Max.Z));
				float num28 = num24 * Mathf.Sqrt(num25 * num25 + num26 * num26 + num27 * num27) * (flag3 ? 1f : 2f) + 0.1f;
				if (flag)
				{
					Vector4[] array2 = array;
					for (int j = 0; j < array2.Length; j++)
					{
						Vector4 vector = array2[j];
						if (vector.x * position.X + vector.y * position.Y + vector.z * position.Z + vector.w < 0f - num28)
						{
							num12++;
							num13 += num23;
							break;
						}
					}
				}
				if (flag2)
				{
					Vector3 rhs = new Vector3(position.X, position.Y, position.Z) - _shadowEye;
					float f = Vector3.Dot(_lightAx, rhs);
					float f2 = Vector3.Dot(_lightBx, rhs);
					float num29 = Vector3.Dot(_lightL, rhs);
					float num30 = copyArgs.ShadowReach + num28;
					if (Mathf.Abs(f) > num30 || Mathf.Abs(f2) > num30 || num29 > -0.1f + num28 || num29 < -320f - num28)
					{
						num14++;
						num15 += num23;
					}
				}
			}
			return $"{cfg.Meshes.Count} listed: {num4} seen ({num8} copies), {num5} casting ({num9} copies), {num16} shadow only; {num17} at rest, {num6} posed ({num7} parts), " + $"{num10} far boxes, {num11} still; out of the picture {num12} ({num13} copies), out of the shadow map {num14} ({num15} copies); shadow reach {copyArgs.ShadowReach:F0}, reach {copyArgs.Reach:F0}";
		}

		private static float Len(Vec3 v)
		{
			return Mathf.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
		}

		private static float Dot(Vec3 v)
		{
			return v.X * v.X + v.Y * v.Y + v.Z * v.Z;
		}

		private static float MaxAbs(Vec3 v)
		{
			return Mathf.Max(Mathf.Abs(v.X), Mathf.Max(Mathf.Abs(v.Y), Mathf.Abs(v.Z)));
		}

		public void Forget(MeshModel model)
		{
			_models.Forget(model);
		}

		private bool EnsureMaterials()
		{
			if (!GpuShaders.Ready)
			{
				Error = GpuShaders.Error ?? "no shaders";
				return false;
			}
			if (_shaderVersion == GpuShaders.Version && _skyMat != null)
			{
				return true;
			}
			DestroyMaterials();
			Shader shader = GpuShaders.Get("FirstPerson/Sky");
			Shader shader2 = GpuShaders.Get("FirstPerson/Terrain");
			Shader shader3 = GpuShaders.Get("FirstPerson/Style");
			Shader shader4 = GpuShaders.Get("FirstPerson/Model");
			Shader shader5 = GpuShaders.Get("FirstPerson/Sprite");
			Shader shader6 = GpuShaders.Get("FirstPerson/Specks");
			Shader shader7 = GpuShaders.Get("FirstPerson/Weather");
			if (shader == null || shader2 == null || shader3 == null || shader4 == null || shader5 == null || shader6 == null || shader7 == null)
			{
				Error = "a shader is missing from the bundle";
				return false;
			}
			_weatherMat = new Material(shader7)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			_specksMat = new Material(shader6)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			_skyMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			_terrainMat = new Material(shader2)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			_styleMat = new Material(shader3)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			_modelMat = new Material(shader4)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			if (_modelMat.HasProperty(ModelCullId))
			{
				_modelMatCulled = new Material(shader4)
				{
					hideFlags = HideFlags.HideAndDontSave
				};
			}
			_spriteMat = new Material(shader5)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			_shaderVersion = GpuShaders.Version;
			return true;
		}

		private void DestroyMaterials()
		{
			if (_skyMat != null)
			{
				UnityEngine.Object.Destroy(_skyMat);
			}
			if (_terrainMat != null)
			{
				UnityEngine.Object.Destroy(_terrainMat);
			}
			if (_styleMat != null)
			{
				UnityEngine.Object.Destroy(_styleMat);
			}
			if (_modelMat != null)
			{
				UnityEngine.Object.Destroy(_modelMat);
			}
			if (_modelMatCulled != null)
			{
				UnityEngine.Object.Destroy(_modelMatCulled);
			}
			if (_spriteMat != null)
			{
				UnityEngine.Object.Destroy(_spriteMat);
			}
			if (_specksMat != null)
			{
				UnityEngine.Object.Destroy(_specksMat);
			}
			if (_weatherMat != null)
			{
				UnityEngine.Object.Destroy(_weatherMat);
			}
			_skyMat = (_terrainMat = (_styleMat = (_modelMat = (_modelMatCulled = (_spriteMat = (_specksMat = (_weatherMat = null)))))));
		}

		private Material CulledModels()
		{
			if (_modelMatCulled == null || ModelCull == CullMode.Off)
			{
				return null;
			}
			if (_modelMatCulled.GetFloat(ModelCullId) != (float)ModelCull)
			{
				_modelMatCulled.SetFloat(ModelCullId, (float)ModelCull);
			}
			return _modelMatCulled;
		}

		private bool SkyGoesLast()
		{
			if (!_skyMat.HasProperty(SkyZTestId))
			{
				return false;
			}
			float num = (SkyLast ? 4f : 8f);
			if (_skyMat.GetFloat(SkyZTestId) != num)
			{
				_skyMat.SetFloat(SkyZTestId, num);
			}
			return SkyLast;
		}

		private static void Drop(RenderTexture rt)
		{
			if (!(rt == null))
			{
				rt.Release();
				UnityEngine.Object.Destroy(rt);
			}
		}

		private void EnsureTargets(int w, int h)
		{
			int num = w * Supersample;
			int num2 = h * Supersample;
			if (Output == null || Output.width != w || Output.height != h)
			{
				if (Output != null)
				{
					Output.Release();
				}
				Output = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonGpuFrame"
				};
				Output.Create();
			}
			if (_sceneCol == null || _sceneCol.width != num || _sceneCol.height != num2)
			{
				Drop(_sceneCol);
				Drop(_sceneData);
				_sceneCol = new RenderTexture(num, num2, 32, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuScene"
				};
				_sceneCol.Create();
				_sceneData = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGBFloat)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuSceneData"
				};
				_sceneData.Create();
				Drop(_aired);
				Drop(_painted);
				Drop(_front);
				_front = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuFront"
				};
				_front.Create();
				_aired = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuAired"
				};
				_aired.Create();
				_painted = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuPainted"
				};
				_painted.Create();
			}
		}

		private void EnsureCloud()
		{
			if (_cloud != null)
			{
				return;
			}
			_cloud = new Texture2D(256, 256, GpuSupport.FilteredFloat, mipChain: false, linear: true)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear,
				name = "FirstPersonCloudNoise"
			};
			float[] array = new float[65536];
			for (int i = 0; i < 256; i++)
			{
				for (int j = 0; j < 256; j++)
				{
					array[i * 256 + j] = CloudNoise.Sample(j, i);
				}
			}
			if (_cloud.format == TextureFormat.RHalf)
			{
				ushort[] array2 = new ushort[array.Length];
				for (int k = 0; k < array.Length; k++)
				{
					array2[k] = Mathf.FloatToHalf(array[k]);
				}
				_cloud.SetPixelData(array2, 0);
			}
			else
			{
				_cloud.SetPixelData(array, 0);
			}
			_cloud.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		}

		public bool Render(FrameRenderer cfg, TerrainCache land, ViewState view, int width, int height, List<Billboard> billboards, List<VoxelObject> voxels, bool classic = false)
		{
			bool flag = false;
			bool flag2 = (_headsetFrame = HasEye);
			double totalMilliseconds = _clock.Elapsed.TotalMilliseconds;
			try
			{
				FrameNumber++;
				if (!EnsureMaterials())
				{
					return false;
				}
				GpuStyle.Update(classic);
				_classicFinish = classic;
				Supersample = ((HasEye && EyeSupersample > 0) ? EyeSupersample : Mathf.Clamp(2560 / Mathf.Max(1, width), 1, GpuStyle.Supersample));
				EnsureTargets(width, height);
				FilterMode filterMode = (((_presentW <= 0 || _presentW % width != 0 || _presentH % height != 0) && Screen.width < width * 2) ? FilterMode.Bilinear : FilterMode.Point);
				if (Output.filterMode != filterMode)
				{
					Output.filterMode = filterMode;
				}
				_presented = false;
				EnsureCloud();
				Width = width;
				Height = height;
				_land = land;
				_terrain.Sync(land);
				if (_terrain.Tiles == null)
				{
					Error = "no land";
					return false;
				}
				_terrain.SyncPaths(land, fresh: false);
				_snow.Sync(land);
				int num = width * Supersample;
				int num2 = height * Supersample;
				float num3 = (float)num * 0.5f / Mathf.Tan(view.Fov * 0.5f);
				Lens lens = Lens.Of(in view, (float)num2 / (float)Mathf.Max(1, num));
				float num4 = (float)num2 * 0.5f + lens.Shift * num3;
				float fogDistance = cfg.FogDistance;
				PlanPixels(cfg, view, lens, num3, num4, width, height, fogDistance, PixelFar && !flag2);
				Matrix4x4 matrix4x = ViewMatrix(view, lens);
				Matrix4x4 matrix4x2 = LensMatrix(num3, num, num2, lens.Shift, 0.04f, fogDistance * 1.05f + 20f);
				float depthScale = 1f;
				if (!HasEye && lens.Steep)
				{
					flag = true;
					HasEye = true;
					Eye = SteepEye(in view, in lens, width, height, SteepBehind);
					float num5 = lens.Cq - lens.Shift * lens.Sq;
					depthScale = num5 / Mathf.Max(num5, 0.05f);
				}
				ViewState viewState = view;
				ViewState viewState2 = view;
				float cx = (float)num * 0.5f;
				float num6 = num3;
				float num7 = 0f;
				if (HasEye)
				{
					num3 = Eye.Focal * (float)Supersample;
					cx = Eye.Cx * (float)Supersample;
					num4 = Eye.Cy * (float)Supersample;
					lens = new Lens
					{
						Cq = 1f
					};
					matrix4x = EyeViewMatrix(in Eye);
					matrix4x2 = EyeMatrix(num3, cx, num4, num, num2, 0.02f, fogDistance * 1.05f + 20f);
					viewState = EyeAt(view);
					viewState2 = EyeCull(view);
					num6 = (float)num * 0.5f / 12f;
					num7 = Mathf.Max(0f, Eye.Behind);
				}
				Matrix4x4 gPUProjectionMatrix = GL.GetGPUProjectionMatrix(matrix4x2, renderIntoTexture: true);
				_flipY = ((GL.GetGPUProjectionMatrix(Matrix4x4.identity, renderIntoTexture: true).m11 < 0f) ? (-1f) : 1f);
				if (_cmd == null)
				{
					_cmd = new CommandBuffer
					{
						name = "FirstPerson GPU frame"
					};
				}
				CommandBuffer cmd = _cmd;
				cmd.Clear();
				SetGlobals(cmd, cfg, viewState, gPUProjectionMatrix * matrix4x, num3, num4, num, num2, width, height, fogDistance);
				GpuDark.Apply(cmd, cfg);
				GpuClassicNight.Apply(cmd, cfg, _classicFinish);
				SetEyeGlobals(cmd, cx, num4, flag);
				SetPixelGlobals(cmd);
				cmd.SetGlobalVector("_FP_Tilt", new Vector4(lens.Cq, lens.Sq, 0f, 0f));
				cmd.SetGlobalVectorArray("_FP_Style", GpuStyle.Params);
				_nightSky.SetUniforms(cmd, cfg, land, viewState, num3 / (float)Supersample);
				_isActorCfg = cfg;
				if (_isActor == null)
				{
					_isActor = (int tag) => _isActorCfg.ActorClarity && tag >= 0 && tag < _isActorCfg.ActorTagEnd;
				}
				Func<int, bool> isActor = _isActor;
				Vector2 centre;
				bool flag3 = ShadowFrame(cmd, cfg, land, ShadowView(viewState), out centre);
				bool flag4 = CastShadowsNow(flag3);
				bool flag5 = cfg.OverlayMeshes.Count > 0;
				if (voxels != null)
				{
					foreach (VoxelObject voxel in voxels)
					{
						flag5 |= voxel.Overlay;
					}
				}
				double totalMilliseconds2 = _clock.Elapsed.TotalMilliseconds;
				_copyArgs = new CopyArgs
				{
					Set = true,
					Shadows = flag4,
					Overlay = flag5,
					Cull = viewState2,
					Reach = Mathf.Max(cfg.MaxDistance, cfg.FarModelReach) + num7,
					VoxelReach = cfg.BillboardDistance + num7,
					ShadowReach = ShadowReach,
					ShadowCentre = centre,
					ViewProj = matrix4x2 * matrix4x,
					Fall = new GpuModels.ShadowFall
					{
						On = flag4,
						Light = _lightL,
						Margin = 6f * ShadowReach / (float)ShadowSize + 0.05f,
						Floor = _terrain.Floor - 5f
					}
				};
				CollectCopies(cfg, land, voxels, in _copyArgs, isActor);
				GatherRoomCasters(land, viewState2);
				double totalMilliseconds3 = _clock.Elapsed.TotalMilliseconds;
				_instances.Upload(cmd);
				_sprites.Prepare(cmd, billboards, viewState2, num3, cfg.MaxDistance + num7, cfg.BillboardDistance + num7, isActor);
				double totalMilliseconds4 = _clock.Elapsed.TotalMilliseconds;
				if (flag4)
				{
					DrawShadows(cmd);
					ShadowMapsDrawn++;
				}
				else if (flag3)
				{
					cmd.SetGlobalTexture("_FP_Shadow", _shadow);
				}
				RoomLightFrame(cmd, cfg, viewState);
				_weather.SetUniforms(cmd, cfg, viewState);
				RenderTargetIdentifier[] sceneTargets = _sceneTargets;
				sceneTargets[0] = _sceneCol;
				sceneTargets[1] = _sceneData;
				cmd.SetRenderTarget(sceneTargets, _sceneCol.depthBuffer);
				cmd.ClearRenderTarget(clearDepth: true, clearColor: true, Color.clear, 1f);
				bool num8 = SkyGoesLast();
				if (!num8)
				{
					cmd.DrawProcedural(Matrix4x4.identity, _skyMat, 0, MeshTopology.Triangles, 3);
				}
				_terrain.Draw(cmd, _terrainMat, viewState2, num6, num, num2, fogDistance * 1.05f + num7, lens);
				Material culled = CulledModels();
				_instances.Draw(cmd, _modelMat, 1, 0, culled);
				_sprites.DrawSolid(cmd, _spriteMat);
				double totalMilliseconds5 = _clock.Elapsed.TotalMilliseconds;
				_specks.Draw(cmd, _specksMat, cfg, viewState2, num3, Supersample, fogDistance + num7);
				if (num8)
				{
					cmd.DrawProcedural(Matrix4x4.identity, _skyMat, 0, MeshTopology.Triangles, 3);
				}
				DrawVeil(cmd, cfg, viewState2, num6, num, num2, fogDistance * 1.05f + num7, lens);
				cmd.SetRenderTarget(_front, _sceneCol.depthBuffer);
				cmd.ClearRenderTarget(clearDepth: false, clearColor: true, Color.clear);
				cmd.SetGlobalTexture("_SceneData", _sceneData);
				_sprites.DrawBlended(cmd, _spriteMat);
				_specks.DrawTrails(cmd, _specksMat, cfg, viewState);
				_weather.DrawFalls(cmd, _weatherMat);
				FlareOpen(cmd, cfg, view, num3, num4, num, lens, cx);
				cmd.SetRenderTarget(sceneTargets, _sceneCol.depthBuffer);
				if (flag5)
				{
					cmd.ClearRenderTarget(clearDepth: true, clearColor: false, Color.clear, 1f);
					cmd.SetGlobalVector("_FP_EyeLight", EyeLight(cfg, viewState));
					_instances.Draw(cmd, _modelMat, 2, 0, culled);
				}
				_weather.March(cmd, _weatherMat, width, height);
				_lightEye = new Vector3(viewState.X, viewState.Y, viewState.Z);
				SetLights(cmd, cfg);
				SetWindows(cmd, cfg);
				cmd.SetGlobalVectorArray("_FP_Style", GpuStyle.Params);
				cmd.SetGlobalTexture("_SceneCol", _sceneCol);
				cmd.SetGlobalTexture("_SceneData", _sceneData);
				cmd.SetGlobalTexture("_Front", _front);
				cmd.SetRenderTarget(_aired);
				cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 0, MeshTopology.Triangles, 3);
				cmd.SetGlobalTexture("_Aired", _aired);
				cmd.SetRenderTarget(_painted);
				cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 1, MeshTopology.Triangles, 3);
				cmd.SetGlobalTexture("_Painted", _painted);
				DrawPixelBlocks(cmd, width, height);
				Glow(cmd, cfg, view, num3, num4, num, num2, lens, cx);
				cmd.SetRenderTarget(Output);
				cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 2, MeshTopology.Triangles, 3);
				_presented = Present(cmd, width, height, flag2);
				int num9;
				if (ReadDepth)
				{
					num9 = (SystemInfo.supportsAsyncGPUReadback ? 1 : 0);
					if (num9 != 0)
					{
						DepthSizeFor(width, height, out var w, out var h);
						EnsureDepthOut(w, h);
						cmd.Blit(_sceneData, _depthOut);
					}
				}
				else
				{
					num9 = 0;
				}
				double totalMilliseconds6 = _clock.Elapsed.TotalMilliseconds;
				Graphics.ExecuteCommandBuffer(cmd);
				BenchAfterFrame(cmd);
				if (num9 != 0)
				{
					DepthSizeFor(width, height, out var w2, out var h2);
					RequestDepth(w2, h2);
				}
				double totalMilliseconds7 = _clock.Elapsed.TotalMilliseconds;
				_setupMs += totalMilliseconds2 - totalMilliseconds;
				_collectMs += totalMilliseconds3 - totalMilliseconds2;
				_uploadMs += totalMilliseconds4 - totalMilliseconds3;
				_spritesMs += totalMilliseconds5 - totalMilliseconds4;
				_restMs += totalMilliseconds6 - totalMilliseconds5;
				_execMs += totalMilliseconds7 - totalMilliseconds6;
				_timed++;
				_projection = new FrameProjection
				{
					View = view,
					Width = width,
					Height = height,
					Focal = num3 / (float)Supersample,
					Horizon = num4 / (float)Supersample
				};
				if (!NoPick)
				{
					if (HasEye)
					{
						RequestPick(Mathf.Clamp((int)(Eye.PickX * (float)Supersample), 0, num - 1), Mathf.Clamp(num2 - 1 - (int)(Eye.PickY * (float)Supersample), 0, num2 - 1), depthScale);
					}
					else
					{
						RequestPick(num / 2, num2 / 2, 1f);
					}
				}
				Error = null;
				return true;
			}
			catch (Exception ex)
			{
				Error = ex.GetType().Name + ": " + ex.Message;
				Log.Error("gpu: frame failed: " + ex);
				return false;
			}
			finally
			{
				if (flag)
				{
					HasEye = false;
				}
			}
		}

		private void CollectCopies(FrameRenderer cfg, TerrainCache land, List<VoxelObject> voxels, in CopyArgs a, Func<int, bool> isActor)
		{
			_instances.Begin();
			if (a.Shadows && voxels != null)
			{
				_voxels.Collect(_instances, 0, voxels, a.Cull, 0f, null, a.ShadowCentre, a.ShadowReach * 1.25f);
			}
			_models.CollectBoth(_instances, cfg.Meshes, land, a.Cull, a.Reach, isActor, a.Shadows, a.ShadowCentre, a.ShadowReach * 1.25f, Sides(a.ViewProj), a.Fall);
			_models.CollectCrowds(_instances, cfg.Crowd, land, a.Cull, Mathf.Max(a.Reach, cfg.FogDistance), isActor, a.Shadows, a.ShadowCentre, a.ShadowReach * 1.25f, Sides(a.ViewProj), a.Fall);
			if (voxels != null)
			{
				_voxels.Collect(_instances, 1, voxels, a.Cull, a.VoxelReach, isActor);
			}
			if (a.Overlay)
			{
				_models.Collect(_instances, 2, cfg.OverlayMeshes, land, a.Cull, 1E+09f, null);
				if (voxels != null)
				{
					_voxels.Collect(_instances, 2, voxels, a.Cull, 1E+09f, null);
				}
			}
		}

		private Vector4[] Sides(Matrix4x4 vp)
		{
			Vector4 row = vp.GetRow(0);
			Vector4 row2 = vp.GetRow(1);
			Vector4 row3 = vp.GetRow(3);
			_sides[0] = row3 + row;
			_sides[1] = row3 - row;
			_sides[2] = row3 + row2;
			_sides[3] = row3 - row2;
			for (int i = 0; i < 4; i++)
			{
				Vector4 vector = _sides[i];
				float num = Mathf.Sqrt(vector.x * vector.x + vector.y * vector.y + vector.z * vector.z);
				if (!(num > 1E-12f))
				{
					return null;
				}
				_sides[i] = vector / num;
			}
			return _sides;
		}

		private void SetGlobals(CommandBuffer cmd, FrameRenderer cfg, ViewState view, Matrix4x4 vp, float focal, float horizon, int sw, int sh, int w, int h, float fogDistance)
		{
			Atmosphere air = cfg.Air;
			bool flag = Backdrop != null && Backdrop.width == w && Backdrop.height == h;
			cmd.SetGlobalTexture("_FP_Backdrop", flag ? Backdrop : Texture2D.blackTexture);
			cmd.SetGlobalVector("_FP_BackdropOn", new Vector4(flag ? 1f : 0f, 0f, 0f, 0f));
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			cmd.SetGlobalMatrix("_FP_VP", vp);
			cmd.SetGlobalVector("_FP_Eye", new Vector4(view.X, view.Y, view.Z, cfg.Time));
			cmd.SetGlobalVector("_FP_View", new Vector4(num, num2, num2, 0f - num));
			cmd.SetGlobalVector("_FP_Lens", new Vector4(focal, horizon, sw, sh));
			cmd.SetGlobalVector("_FP_Out", new Vector4(w, h, Supersample, _terrain.MaxHeight));
			cmd.SetGlobalVector("_FP_FlipY", new Vector4(_flipY, 0f, 0f, 0f));
			cmd.SetGlobalVector("_FP_LightDir", new Vector4(air.Light.X, air.Light.Y, air.Light.Z, air.FlatLambert));
			cmd.SetGlobalVector("_FP_LightCol", new Vector4(air.LightR, air.LightG, air.LightB, 0f));
			if (_classicFinish)
			{
				cmd.SetGlobalVector("_FP_AmbCol", new Vector4(air.AmbR, air.AmbG, air.AmbB, 0f));
			}
			else
			{
				cmd.SetGlobalVector("_FP_AmbCol", new Vector4(air.AmbR * air.CardFillR, air.AmbG * air.CardFillG, air.AmbB * air.CardFillB, 0f));
			}
			cmd.SetGlobalVector("_FP_Classic", new Vector4(_classicFinish ? 1f : 0f, 0f, 0f, 0f));
			cmd.SetGlobalVector("_FP_Misc", new Vector4((cfg.DetailZoom > 1f) ? (1f / cfg.DetailZoom) : 1f, air.Night, air.Indoors ? 1 : 0, cfg.WallModelRange));
			cmd.SetGlobalVector("_FP_Mist", new Vector4(air.Indoors ? 0f : air.DawnMist, 0f, 0f, 0f));
			cmd.SetGlobalVector("_FP_Sway", new Vector4(TreeSway.PushX, TreeSway.PushY, TreeSway.Gust, TreeSway.Clock));
			cmd.SetGlobalBuffer("_FP_Tiles", _terrain.Tiles);
			if (_terrain.PathSlots != null)
			{
				cmd.SetGlobalBuffer("_FP_PathSlots", _terrain.PathSlots);
				cmd.SetGlobalBuffer("_FP_PathTexels", _terrain.PathTexels);
			}
			_snow.Bind(cmd);
			cmd.SetGlobalTexture("_FP_Cloud", _cloud);
			float num3 = 1f - Mathf.Min(1f, Mathf.Max(0f, air.SunDisc.Z) * 3f);
			bool moon = air.Night > 0.6f;
			Vec3 vec = (moon ? air.MoonDisc : air.SunDisc);
			float num4 = Mathf.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
			float gx = ((num4 > 0.0001f) ? (vec.X / num4) : 1f);
			float gy = ((num4 > 0.0001f) ? (vec.Y / num4) : 0f);
			float sideStrength = (moon ? 0.35f : (0.35f + 0.65f * num3));
			float glitter = 0f;
			if (air.Natural)
			{
				FrameRenderer.SkyGlow(air, num3, out moon, out gx, out gy, out sideStrength, out glitter);
			}
			cmd.SetGlobalVector("_FP_FogDir", new Vector4(gx, gy, sideStrength, 0f));
			cmd.SetGlobalVector("_FP_Fog0", Col(air.Horizon));
			cmd.SetGlobalVector("_FP_Fog1", Col(air.SunHorizon));
			cmd.SetGlobalVector("_FP_Zenith", Col(air.Zenith));
			float w2 = (moon ? (air.MoonVisible * 0.6f) : (air.SunVisible * (0.45f + 0.55f * num3)));
			Color color = (moon ? new Color(40f / 51f, 0.8235294f, 1f) : Color.Lerp(Col(air.SunColor), Color.white, 0.35f));
			if (air.Natural)
			{
				w2 = glitter;
			}
			cmd.SetGlobalVector("_FP_Glitter", new Vector4(color.r, color.g, color.b, w2));
			float num5 = Mathf.Min(cfg.HazeFadeStart, fogDistance * 0.96f);
			cmd.SetGlobalVector("_FP_HazeParams", new Vector4(air.HazeDensity * cfg.HazeScale, num5, 1f / Mathf.Max(0.001f, fogDistance - num5), fogDistance));
			cmd.SetGlobalVector("_FP_Map", new Vector4(_land.Width, _land.Height, _land.OutsideHeight, _land.OutsideIsWater ? 1 : 0));
			cmd.SetGlobalVector("_FP_OutsideCol", Col(_land.OutsideColor));
			float num6 = Mathf.Max(0.5f, air.MoonScale);
			cmd.SetGlobalVector("_FP_Sun", new Vector4(air.SunDisc.X, air.SunDisc.Y, air.SunDisc.Z, air.SunVisible));
			cmd.SetGlobalVector("_FP_Moon", new Vector4(air.MoonDisc.X, air.MoonDisc.Y, air.MoonDisc.Z, air.MoonVisible * air.Night));
			Color color2 = Col(air.SunColor);
			cmd.SetGlobalVector("_FP_SunCol", new Vector4(color2.r, color2.g, color2.b, Mathf.Cos(0.026f + 0.016f * num3)));
			cmd.SetGlobalVector("_FP_SkyMisc", new Vector4(Mathf.Cos(0.022f * num6), Mathf.Cos(0.245f * Mathf.Min(2.2f, num6)), num6, air.Stars));
			SetNaturalSky(cmd, air, num3);
			float num7 = Mathf.Sqrt(air.SunDisc.X * air.SunDisc.X + air.SunDisc.Y * air.SunDisc.Y);
			cmd.SetGlobalVector("_FP_Clouds", new Vector4(air.Clouds, cfg.Time * 0.9f, cfg.Time * 0.35f, air.Indoors ? 1 : 0));
			cmd.SetGlobalVector("_FP_CloudSun", new Vector4((num7 > 0.0001f) ? (air.SunDisc.X / num7 * 2.5f) : 0f, (num7 > 0.0001f) ? (air.SunDisc.Y / num7 * 2.5f) : 0f, 0f, 0f));
			cmd.SetGlobalVector("_FP_CloudLit", Col(air.CloudLit));
			cmd.SetGlobalVector("_FP_CloudShade", Col(air.CloudShade));
			cmd.SetGlobalVector("_FP_Finish", new Vector4(cfg.PaletteLevels, air.Indoors ? 0.98f : (1.07f + 0.05f * air.Night), cfg.Vignette, air.Night));
			bool flag2 = BiomePicture.Frame == Time.frameCount && !air.Indoors;
			cmd.SetGlobalVector("_FP_Biome", flag2 ? new Vector4(BiomePicture.Shimmer, BiomePicture.Mirage, BiomePicture.Warp, cfg.Time) : new Vector4(0f, 0f, 0f, cfg.Time));
			SightCut cut = cfg.Cut;
			cmd.SetGlobalFloat("_FP_VeilPass", 0f);
			cmd.SetGlobalVector("_FP_Cut0", new Vector4(cut.On ? cut.Target : 0f, cut.Bottom, cut.Aim, 0.6f * cut.Strength));
			cmd.SetGlobalVector("_FP_Cut1", new Vector4(cut.X, cut.Y, cut.Z, cut.Strength));
			cmd.SetGlobalVector("_FP_Cut2", new Vector4(cut.Fx, cut.Fy, 0.05f, 0.4f));
			cmd.SetGlobalVector("_FP_Ripple", new Vector4(cfg.RippleX, cfg.RippleY, cfg.RippleStrength, 0f));
			for (int i = 0; i < _wakes.Length; i++)
			{
				_wakes[i] = ((i < cfg.WakeCount) ? new Vector4(cfg.Wakes[i * 4], cfg.Wakes[i * 4 + 1], cfg.Wakes[i * 4 + 2], cfg.Wakes[i * 4 + 3]) : Vector4.zero);
			}
			cmd.SetGlobalVectorArray("_FP_Wakes", _wakes);
			cmd.SetGlobalFloat("_FP_WakeCount", cfg.WakeCount);
			float z = ((_land != null && _land.HasSeabed) ? cfg.SeeIntoRange : 0f);
			cmd.SetGlobalVector("_FP_Sea", new Vector4(cfg.SeaState(view.X, view.Y), cfg.SeaWaves ? 1f : 0f, z, cfg.SeeIntoRange));
			cmd.SetGlobalVector("_FP_Wave", new Vector4(Sea.A1, Sea.A2, Sea.Range, 0f));
			cmd.SetGlobalVector("_FP_WaveDir", new Vector4(Sea.DirX, Sea.DirY, Sea.Dir2X, Sea.Dir2Y));
			cmd.SetGlobalVector("_FP_Scene", new Vector4(sw, sh, 0f, 0f));
		}

		private static Color Col(uint c)
		{
			return new Color((float)(c & 0xFF) / 255f, (float)((c >> 8) & 0xFF) / 255f, (float)((c >> 16) & 0xFF) / 255f, 1f);
		}

		private static Matrix4x4 ViewMatrix(ViewState v, Lens lens)
		{
			float num = Mathf.Cos(v.Yaw);
			float num2 = Mathf.Sin(v.Yaw);
			float num3 = num2;
			float num4 = 0f - num;
			float cq = lens.Cq;
			float sq = lens.Sq;
			Matrix4x4 result = default(Matrix4x4);
			result.SetRow(0, new Vector4(num3, num4, 0f, 0f - (num3 * v.X + num4 * v.Y)));
			result.SetRow(1, new Vector4((0f - num) * sq, (0f - num2) * sq, cq, (num * v.X + num2 * v.Y) * sq - v.Z * cq));
			result.SetRow(2, new Vector4((0f - num) * cq, (0f - num2) * cq, 0f - sq, (num * v.X + num2 * v.Y) * cq + v.Z * sq));
			result.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
			return result;
		}

		private static Matrix4x4 LensMatrix(float focal, int w, int h, float shift, float near, float far)
		{
			return new Matrix4x4
			{
				m00 = 2f * focal / (float)w,
				m11 = 2f * focal / (float)h,
				m12 = 2f * focal / (float)h * shift,
				m22 = (far + near) / (near - far),
				m23 = 2f * far * near / (near - far),
				m32 = -1f
			};
		}

		public void PresentTo(int screenWidth, int screenHeight)
		{
			_presentW = screenWidth;
			_presentH = screenHeight;
		}

		private bool Present(CommandBuffer cmd, int width, int height, bool headset)
		{
			int presentW = _presentW;
			int presentH = _presentH;
			if (headset || presentW <= width || presentH <= height || (presentW % width == 0 && presentH % height == 0))
			{
				return false;
			}
			if (_styleMat.passCount < 8)
			{
				return false;
			}
			if (_present == null || _present.width != presentW || _present.height != presentH)
			{
				if (_present != null)
				{
					_present.Release();
				}
				_present = new RenderTexture(presentW, presentH, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonGpuShown"
				};
				_present.Create();
			}
			cmd.SetGlobalTexture("_Picture", Output);
			cmd.SetGlobalVector("_FP_Present", new Vector4(width, height, presentW, presentH));
			cmd.SetRenderTarget(_present);
			cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 7, MeshTopology.Triangles, 3);
			return true;
		}

		public string TakeTimes()
		{
			int num = Math.Max(1, _timed);
			string result = $"setup {_setupMs / (double)num:F2}, copies {_collectMs / (double)num:F2}, upload {_uploadMs / (double)num:F2}, land+sprites {_spritesMs / (double)num:F2}, rest {_restMs / (double)num:F2}, execute {_execMs / (double)num:F2}";
			_setupMs = (_collectMs = (_uploadMs = (_spritesMs = (_restMs = (_execMs = 0.0)))));
			_timed = 0;
			return result;
		}

		private bool ShadowFrame(CommandBuffer cmd, FrameRenderer cfg, TerrainCache land, ViewState view, out Vector2 centre)
		{
			Atmosphere air = cfg.Air;
			centre = Vector2.zero;
			float num = Mathf.Max(air.LightR, Mathf.Max(air.LightG, air.LightB));
			cmd.SetGlobalVector("_FP_ShadowQ", new Vector4(ShadowSoftness, 1f - Mathf.Clamp01(ShadowDarkness), 0.06f, 0.03f));
			NaturalShadows(cmd, cfg, land, view, air);
			Vec3 light = air.Light;
			if (air.Indoors || num < 0.05f || light.Z < 0.03f || ShadowQuality <= 0 || ShadowDarkness <= 0f)
			{
				cmd.SetGlobalVector("_FP_ShadowL", Vector4.zero);
				HoldMap(cmd, air, map: false);
				return false;
			}
			HoldMap(cmd, air, map: true);
			int size = ((ShadowQuality == 1) ? 1024 : ((ShadowQuality >= 4) ? 4096 : 2048));
			ShadowSize = size;
			_cascades = ShadowQuality >= 3 && !cfg.ShadowFocusSet && ShadowReach > 48f;
			int num2 = (_cascades ? (size * 2) : size);
			if (_shadow == null || _shadow.height != size || _shadow.width < num2)
			{
				if (_shadow != null)
				{
					_shadow.Release();
					UnityEngine.Object.Destroy(_shadow);
				}
				_shadow = new RenderTexture(num2, size, 24, RenderTextureFormat.RFloat)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuShadow"
				};
				_shadow.Create();
			}
			Vector3 l = new Vector3(light.X, light.Y, light.Z).normalized;
			Vector3 ax = Vector3.Cross(Vector3.forward, l);
			if (ax.sqrMagnitude < 1E-06f)
			{
				ax = Vector3.right;
			}
			ax.Normalize();
			Vector3 bx = Vector3.Cross(l, ax);
			float fx = Mathf.Cos(view.Yaw);
			float fy = Mathf.Sin(view.Yaw);
			float num3 = (_cascades ? 32f : ShadowReach);
			float up = ShadowReach;
			float up2 = num3;
			float lift = 150f;
			float depth = 320f;
			Vector3 wideAt = Centre(ShadowReach);
			if (NaturalSun)
			{
				FitShadows(land, wideAt, l, num3, out up, out up2, out lift, out depth);
			}
			Vector3 vector = Snap(wideAt, ShadowReach, up, near: false);
			Vector3 at = (_cascades ? Snap(Centre(32f), 32f, up2, near: true) : vector);
			_nearVP = LightVP(at, num3, up2);
			_farVP = (_cascades ? LightVP(vector, ShadowReach, up) : _nearVP);
			cmd.SetGlobalMatrix("_FP_LightVP", _nearVP);
			cmd.SetGlobalVector("_FP_ShadowA", new Vector4(ax.x, ax.y, ax.z, (float)size / (2f * num3)));
			cmd.SetGlobalVector("_FP_ShadowB", new Vector4(bx.x, bx.y, bx.z, size));
			cmd.SetGlobalVector("_FP_ShadowL", new Vector4(l.x, l.y, l.z, 1f));
			cmd.SetGlobalVector("_FP_ShadowC", new Vector4(at.x, at.y, at.z, num3));
			cmd.SetGlobalVector("_FP_Shadow2", _cascades ? new Vector4(vector.x, vector.y, vector.z, (float)size / (2f * ShadowReach)) : Vector4.zero);
			cmd.SetGlobalVector("_FP_ShadowK", NaturalSun ? new Vector4((float)size / (2f * up2), _cascades ? ((float)size / (2f * up)) : 0f, 0f, 0f) : Vector4.zero);
			cmd.SetGlobalVector("_FP_ShadowO", StippleOrigins());
			_lightAx = ax;
			_lightBx = bx;
			_lightL = l;
			_shadowEye = vector + l * lift;
			centre = new Vector2(vector.x, vector.y);
			return true;
			Vector3 Centre(float reach)
			{
				float num4 = (cfg.ShadowFocusSet ? cfg.ShadowFocusX : (view.X + fx * reach * 0.45f));
				float num5 = (cfg.ShadowFocusSet ? cfg.ShadowFocusY : (view.Y + fy * reach * 0.45f));
				if (NaturalSun)
				{
					return NaturalCentre(land, num4, num5, cfg.ShadowFocusSet, view);
				}
				return new Vector3(Mathf.Floor(num4 / 4f) * 4f + 2f, Mathf.Floor(num5 / 4f) * 4f + 2f, cfg.ShadowFocusSet ? land.SampleHeight(num4, num5) : land.SampleHeight(view.X, view.Y));
			}
			Matrix4x4 LightVP(Vector3 vector2, float reach, float upMap)
			{
				Vector3 rhs = vector2 + l * lift;
				Matrix4x4 matrix4x = default(Matrix4x4);
				matrix4x.SetRow(0, new Vector4(ax.x, ax.y, ax.z, 0f - Vector3.Dot(ax, rhs)));
				matrix4x.SetRow(1, new Vector4(bx.x, bx.y, bx.z, 0f - Vector3.Dot(bx, rhs)));
				matrix4x.SetRow(2, new Vector4(l.x, l.y, l.z, 0f - Vector3.Dot(l, rhs)));
				matrix4x.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
				return GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0f - reach, reach, 0f - upMap, upMap, 0.1f, depth), renderIntoTexture: true) * matrix4x;
			}
			Vector3 Snap(Vector3 p, float reach, float upMap, bool near)
			{
				float num4 = (float)size / (2f * reach);
				float num5 = (float)size / (2f * upMap);
				if (NaturalSun)
				{
					return SnapNatural(p, cfg, view, wideAt.z, ax, bx, l, num4, num5, near);
				}
				float num6 = Vector3.Dot(p, ax);
				float num7 = Vector3.Dot(p, bx);
				return p + ax * (Mathf.Round(num6 * num4) / num4 - num6) + bx * (Mathf.Round(num7 * num5) / num5 - num7);
			}
		}

		private void DrawShadows(CommandBuffer cmd)
		{
			cmd.SetRenderTarget(_shadow);
			cmd.ClearRenderTarget(clearDepth: true, clearColor: true, new Color(-1E+09f, 0f, 0f, 0f), 1f);
			if (_cascades)
			{
				int height = _shadow.height;
				cmd.SetViewport(new Rect(0f, 0f, height, height));
				cmd.SetGlobalMatrix("_FP_LightVP", _nearVP);
				_instances.Draw(cmd, _modelMat, 0, 1);
				cmd.SetViewport(new Rect(height, 0f, height, height));
				cmd.SetGlobalMatrix("_FP_LightVP", _farVP);
				_instances.Draw(cmd, _modelMat, 0, 1);
			}
			else
			{
				if (_shadow.width != _shadow.height)
				{
					cmd.SetViewport(new Rect(0f, 0f, _shadow.height, _shadow.height));
				}
				_instances.Draw(cmd, _modelMat, 0, 1);
			}
			cmd.SetGlobalTexture("_FP_Shadow", _shadow);
		}

		private void Glow(CommandBuffer cmd, FrameRenderer cfg, ViewState view, float focal, float horizon, int sw, int sh, Lens lens, float cx)
		{
			int num = Mathf.Max(1, Width / 4);
			int num2 = Mathf.Max(1, Height / 4);
			if (_glowA == null || _glowA.width != num || _glowA.height != num2)
			{
				Drop(_glowA);
				Drop(_glowB);
				Drop(_glowC);
				_glowC = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuGlowC"
				};
				_glowC.Create();
				_glowA = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuGlowA"
				};
				_glowA.Create();
				_glowB = new RenderTexture(num, num2, 0, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuGlowB"
				};
				_glowB.Create();
			}
			Atmosphere air = cfg.Air;
			float num3 = (air.Indoors ? 0f : (air.Rays * air.SunVisible));
			float num4 = Mathf.Cos(view.Yaw);
			float num5 = Mathf.Sin(view.Yaw);
			Vec3 sunDisc = air.SunDisc;
			float num6 = sunDisc.X * num4 + sunDisc.Y * num5;
			float num7 = num6 * lens.Cq + sunDisc.Z * lens.Sq;
			float z = 0f;
			float w = 0f;
			float col = 0f;
			float row = 0f;
			bool num8 = (HasEye ? EyeDirection(new Vector3(sunDisc.X, sunDisc.Y, sunDisc.Z), focal, cx, horizon, out col, out row) : (num7 >= 0.1f));
			Vector4 flare = _flare;
			if (!num8)
			{
				num3 = 0f;
			}
			else
			{
				float num9 = (HasEye ? col : ((float)sw * 0.5f + (sunDisc.X * num5 + sunDisc.Y * (0f - num4)) / num7 * focal));
				float num10 = (HasEye ? row : (horizon - (sunDisc.Z * lens.Cq - num6 * lens.Sq) / num7 * focal));
				float b = Mathf.Max(Mathf.Max(0f - num9, num9 - (float)sw) / (float)sw, Mathf.Max(0f - num10, num10 - (float)sh) / (float)sh);
				float num11 = 1f - Mathf.Max(0f, b) * 1.5f;
				num3 = ((!(num11 <= 0f)) ? (num3 * num11) : 0f);
				z = num9 / (float)sw * (float)num;
				w = (1f - num10 / (float)sh) * (float)num2;
			}
			Color color = Col(air.SunColor);
			cmd.SetGlobalVector("_FP_Glow", new Vector4(num, num2, z, w));
			cmd.SetGlobalVector("_FP_Shafts", new Vector4(color.r, color.g, color.b, num3));
			cmd.SetGlobalVector("_FP_Flare", flare);
			cmd.SetGlobalVector("_FP_FlareCol", (flare.z > 0f) ? new Vector4(color.r, color.g, color.b, 0f) : Vector4.zero);
			cmd.SetRenderTarget(_glowA);
			cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 3, MeshTopology.Triangles, 3);
			cmd.SetGlobalTexture("_GlowA", _glowA);
			cmd.SetRenderTarget(_glowB);
			cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 4, MeshTopology.Triangles, 3);
			cmd.SetGlobalTexture("_GlowB", _glowB);
			cmd.SetRenderTarget(_glowC);
			cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 5, MeshTopology.Triangles, 3);
			cmd.SetGlobalTexture("_GlowC", _glowC);
		}

		private void SetLights(CommandBuffer cmd, FrameRenderer cfg)
		{
			List<PointLight> list = cfg.Lights;
			if (list.Count > 64)
			{
				_nearLights.Clear();
				_nearLights.AddRange(list);
				_nearLights.Sort((PointLight p, PointLight q) => Reach(p).CompareTo(Reach(q)));
				list = _nearLights;
			}
			int num = Mathf.Min(64, list.Count);
			Atmosphere air = cfg.Air;
			float flatLambert = air.FlatLambert;
			float num2 = (_classicFinish ? 1f : air.CardFillR);
			float num3 = (_classicFinish ? 1f : air.CardFillG);
			float num4 = (_classicFinish ? 1f : air.CardFillB);
			float num5 = Mathf.Max(0.12f, (air.AmbR * num2 + air.LightR * flatLambert) * 0.3f + (air.AmbG * num3 + air.LightG * flatLambert) * 0.59f + (air.AmbB * num4 + air.LightB * flatLambert) * 0.11f);
			for (int num6 = 0; num6 < num; num6++)
			{
				PointLight pointLight = list[num6];
				_lights[num6 * 2] = new Vector4(pointLight.X, pointLight.Y, pointLight.Z, pointLight.Radius);
				_lights[num6 * 2 + 1] = new Vector4(pointLight.R, pointLight.G, pointLight.B, pointLight.Glow);
			}
			cmd.SetGlobalVectorArray("_FP_Lights", _lights);
			uint screenFlash = cfg.ScreenFlash;
			cmd.SetGlobalVector("_FP_ScreenFlash", new Vector4((float)(screenFlash & 0xFF) / 255f, (float)((screenFlash >> 8) & 0xFF) / 255f, (float)((screenFlash >> 16) & 0xFF) / 255f, (float)(screenFlash >> 24) / 255f));
			cmd.SetGlobalVector("_FP_LightCount", new Vector4(num, GpuClassicNight.Gain / num5, 0f, 0f));
		}

		private void SetWindows(CommandBuffer cmd, FrameRenderer cfg)
		{
			Atmosphere air = cfg.Air;
			float num = 1f - air.Night;
			int num2 = (air.Indoors ? Mathf.Min(16, cfg.Windows.Count) : 0);
			int num3 = ((num >= 0.05f) ? num2 : 0);
			for (int i = 0; i < num2; i++)
			{
				LightWindow lightWindow = cfg.Windows[i];
				_windows[i * 3] = new Vector4(lightWindow.Center.X, lightWindow.Center.Y, lightWindow.Center.Z, lightWindow.HalfWidth);
				_windows[i * 3 + 1] = new Vector4(lightWindow.Right.X, lightWindow.Right.Y, lightWindow.Right.Z, lightWindow.HalfHeight);
				_windows[i * 3 + 2] = new Vector4(lightWindow.Inward.X, lightWindow.Inward.Y, lightWindow.Inward.Z, 0f);
			}
			cmd.SetGlobalVectorArray("_FP_Windows", _windows);
			cmd.SetGlobalVector("_FP_WindowCount", new Vector4(num3, num, num2, 0f));
			Color color = Col(air.SunColor);
			cmd.SetGlobalVector("_FP_BeamSun", new Vector4(color.r, color.g, color.b, 0f));
		}

		private float Reach(PointLight l)
		{
			float num = l.X - _lightEye.x;
			float num2 = l.Y - _lightEye.y;
			float num3 = l.Z - _lightEye.z;
			return num * num + num2 * num2 + num3 * num3 - l.Radius * l.Radius;
		}

		private static Vector4 EyeLight(FrameRenderer cfg, ViewState view)
		{
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			foreach (PointLight light in cfg.Lights)
			{
				float num4 = light.X - view.X;
				float num5 = light.Y - view.Y;
				float num6 = light.Z - (view.Z - 0.3f);
				float num7 = num4 * num4 + num5 * num5 + num6 * num6;
				float num8 = light.Radius * light.Radius;
				if (!(num7 >= num8))
				{
					float num9 = 1f - num7 / num8;
					num9 *= num9 * 0.8f;
					num += light.R * num9;
					num2 += light.G * num9;
					num3 += light.B * num9;
				}
			}
			float num10 = Mathf.Max(num, Mathf.Max(num2, num3));
			if (num10 > 0.0001f)
			{
				float num11 = 1f / (1f + num10 / 0.7f);
				num *= num11;
				num2 *= num11;
				num3 *= num11;
			}
			return new Vector4(num, num2, num3, 0f);
		}

		private void RequestPick(int x, int y, float depthScale)
		{
			if (_pickPending || !SystemInfo.supportsAsyncGPUReadback)
			{
				return;
			}
			_pickPending = true;
			int frame = FrameNumber;
			AsyncGPUReadback.Request(_sceneData, 0, x, 1, y, 1, 0, 1, delegate(AsyncGPUReadbackRequest r)
			{
				_pickPending = false;
				if (!r.hasError)
				{
					NativeArray<Vector4> data = r.GetData<Vector4>();
					if (data.Length >= 1)
					{
						Vector4 vector = data[0];
						PickTag = ((vector.y > 0.5f) ? (Mathf.RoundToInt(vector.y) - 1) : (-1));
						PickDepth = ((vector.z > 0.5f) ? (vector.x * depthScale) : float.MaxValue);
						PickFrame = frame;
					}
				}
			});
		}

		public static void DepthSizeFor(int width, int height, out int w, out int h)
		{
			w = Mathf.Min(width, 640);
			h = ((w == width) ? height : Mathf.Max(1, Mathf.RoundToInt((float)(height * w) / (float)Mathf.Max(1, width))));
		}

		private void EnsureDepthOut(int w, int h)
		{
			if (!(_depthOut != null) || _depthOut.width != w || _depthOut.height != h)
			{
				if (_depthOut != null)
				{
					_depthOut.Release();
				}
				_depthOut = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuDepth"
				};
				_depthOut.Create();
			}
		}

		private void RequestDepth(int w, int h)
		{
			if (_depthPending >= 3)
			{
				return;
			}
			_depthPending++;
			int frame = FrameNumber;
			AsyncGPUReadback.Request(_depthOut, 0, TextureFormat.RFloat, delegate(AsyncGPUReadbackRequest r)
			{
				_depthPending--;
				if (!r.hasError && frame > DepthFrame)
				{
					NativeArray<float> data = r.GetData<float>();
					int num = w * h;
					if (data.Length >= num)
					{
						if (DepthPixels.Length != num)
						{
							DepthPixels = new float[num];
						}
						NativeArray<float>.Copy(data, 0, DepthPixels, 0, num);
						DepthFrame = frame;
						DepthWidth = w;
						DepthHeight = h;
					}
				}
			});
		}

		public Texture2D ReadOutput()
		{
			if (Output == null)
			{
				return null;
			}
			Texture2D texture2D = new Texture2D(Output.width, Output.height, TextureFormat.RGBA32, mipChain: false);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = Output;
			texture2D.ReadPixels(new Rect(0f, 0f, Output.width, Output.height), 0, 0);
			texture2D.Apply();
			RenderTexture.active = active;
			return texture2D;
		}

		public void Dispose()
		{
			_worldTerrain.Dispose();
			_roomTerrain.Dispose();
			ReleaseRoomShadows();
			_sunField.Dispose();
			_snow.Dispose();
			_models.Dispose();
			_instances.Dispose();
			_voxels.Dispose();
			_sprites.Dispose();
			_specks.Dispose();
			_weather.Dispose();
			DestroyMaterials();
			if (Output != null)
			{
				Output.Release();
			}
			if (_sceneCol != null)
			{
				_sceneCol.Release();
			}
			if (_sceneData != null)
			{
				_sceneData.Release();
			}
			if (_aired != null)
			{
				_aired.Release();
			}
			if (_painted != null)
			{
				_painted.Release();
			}
			if (_front != null)
			{
				_front.Release();
			}
			if (_present != null)
			{
				_present.Release();
			}
			_present = null;
			if (_shadow != null)
			{
				_shadow.Release();
			}
			if (_glowA != null)
			{
				_glowA.Release();
			}
			if (_glowB != null)
			{
				_glowB.Release();
			}
			if (_glowC != null)
			{
				_glowC.Release();
			}
			ReleaseVeil();
			ReleasePixel();
			ReleaseFlare();
			if (_depthOut != null)
			{
				_depthOut.Release();
			}
			_depthOut = null;
			Output = (_sceneCol = (_sceneData = (_aired = (_painted = (_front = (_shadow = (_glowA = (_glowB = (_glowC = null)))))))));
			if (_cloud != null)
			{
				UnityEngine.Object.Destroy(_cloud);
			}
			_cloud = null;
			_cmd?.Release();
			_cmd = null;
		}

		private static EyeCamera SteepEye(in ViewState v, in Lens lens, int width, int height, float behind)
		{
			float num = Mathf.Cos(v.Yaw);
			float num2 = Mathf.Sin(v.Yaw);
			float num3 = (float)width * 0.5f / Mathf.Tan(v.Fov * 0.5f);
			return new EyeCamera
			{
				Position = new Vector3(v.X, v.Y, v.Z),
				Right = new Vector3(num2, 0f - num, 0f),
				Up = new Vector3((0f - num) * lens.Sq, (0f - num2) * lens.Sq, lens.Cq),
				Forward = new Vector3(num * lens.Cq, num2 * lens.Cq, lens.Sq),
				Focal = num3,
				Cx = (float)width * 0.5f,
				Cy = (float)height * 0.5f + lens.Shift * num3,
				Yaw = v.Yaw,
				Behind = behind,
				PickX = (float)width * 0.5f,
				PickY = (float)height * 0.5f
			};
		}

		private static Matrix4x4 EyeViewMatrix(in EyeCamera e)
		{
			Vector3 position = e.Position;
			Vector3 right = e.Right;
			Vector3 up = e.Up;
			Vector3 forward = e.Forward;
			Matrix4x4 result = default(Matrix4x4);
			result.SetRow(0, new Vector4(right.x, right.y, right.z, 0f - Vector3.Dot(right, position)));
			result.SetRow(1, new Vector4(up.x, up.y, up.z, 0f - Vector3.Dot(up, position)));
			result.SetRow(2, new Vector4(0f - forward.x, 0f - forward.y, 0f - forward.z, Vector3.Dot(forward, position)));
			result.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
			return result;
		}

		private static Matrix4x4 EyeMatrix(float focal, float cx, float cy, int w, int h, float near, float far)
		{
			return new Matrix4x4
			{
				m00 = 2f * focal / (float)w,
				m02 = 1f - 2f * cx / (float)w,
				m11 = 2f * focal / (float)h,
				m12 = 2f * cy / (float)h - 1f,
				m22 = (far + near) / (near - far),
				m23 = 2f * far * near / (near - far),
				m32 = -1f
			};
		}

		private void SetEyeGlobals(CommandBuffer cmd, float cx, float cy, bool steep)
		{
			cmd.SetGlobalVector("_FP_Stereo", new Vector4((HasEye && Stereo) ? 1f : 0f, 0f, 0f, 0f));
			if (!HasEye)
			{
				cmd.SetGlobalVector("_FP_Cam", Vector4.zero);
				return;
			}
			cmd.SetGlobalVector("_FP_Cam", new Vector4(cx, cy, 1f, steep ? 1f : 0f));
			cmd.SetGlobalVector("_FP_CamR", Eye.Right);
			cmd.SetGlobalVector("_FP_CamU", Eye.Up);
			cmd.SetGlobalVector("_FP_CamF", Eye.Forward);
		}

		private bool EyeDirection(Vector3 d, float focal, float cx, float cy, out float col, out float row)
		{
			float num = Vector3.Dot(d, Eye.Forward);
			col = (row = 0f);
			if (num < 0.1f)
			{
				return false;
			}
			col = cx + focal * Vector3.Dot(d, Eye.Right) / num;
			row = cy - focal * Vector3.Dot(d, Eye.Up) / num;
			return true;
		}

		private ViewState EyeAt(ViewState view)
		{
			return new ViewState
			{
				X = Eye.Position.x,
				Y = Eye.Position.y,
				Z = Eye.Position.z,
				Yaw = Eye.Yaw,
				Pitch = 0f,
				Fov = view.Fov,
				Shear = 0f
			};
		}

		private ViewState ShadowView(ViewState at)
		{
			if (!HasEye || !Stereo)
			{
				return at;
			}
			at.X = StereoHead.x;
			at.Y = StereoHead.y;
			at.Z = StereoHead.z;
			return at;
		}

		private bool CastShadowsNow(bool shadows)
		{
			if (!shadows)
			{
				return false;
			}
			bool flag = HasEye && Stereo;
			if (flag && ShadowsKept && _shadowsDrawnPair == StereoPair)
			{
				return false;
			}
			_shadowsDrawnPair = (flag ? StereoPair : (-1));
			return true;
		}

		private ViewState EyeCull(ViewState view)
		{
			float num = Mathf.Cos(Eye.Yaw);
			float num2 = Mathf.Sin(Eye.Yaw);
			return new ViewState
			{
				X = Eye.Position.x - num * Eye.Behind,
				Y = Eye.Position.y - num2 * Eye.Behind,
				Z = Eye.Position.z,
				Yaw = Eye.Yaw,
				Pitch = 0f,
				Fov = 2.9670596f,
				Shear = 0f
			};
		}

		internal string PeopleDrawsForTest()
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int[] array = new int[2] { 0, 1 };
			foreach (int set in array)
			{
				DrawList.Clear();
				_instances.DrawsForTest(set, DrawList);
				foreach (var draw in DrawList)
				{
					Mesh item = draw.mesh;
					string text = ((item != null) ? item.name : "");
					if (!text.StartsWith("FirstPersonModel_person_human", StringComparison.Ordinal) && !text.StartsWith("FirstPersonModel_person_elf", StringComparison.Ordinal) && !text.StartsWith("FirstPersonModel_person_orc", StringComparison.Ordinal) && !text.StartsWith("FirstPersonModel_person_dwarf", StringComparison.Ordinal))
					{
						num5++;
						continue;
					}
					num6++;
					if (text.EndsWith("_x", StringComparison.Ordinal))
					{
						num4++;
					}
					else if (text.EndsWith("_f", StringComparison.Ordinal))
					{
						num3++;
					}
					else if (text.EndsWith("_m", StringComparison.Ordinal))
					{
						num2++;
					}
					else
					{
						num++;
					}
				}
			}
			return $"people's draws {num6} (near {num}, middle {num2}, far {num3}, boxes {num4}), others {num5}";
		}

		internal void ForgetModel(MeshModel model)
		{
			_models.ForgetRetired(model);
		}

		private void PlanPixels(FrameRenderer cfg, ViewState view, Lens lens, float focal, float horizon, int width, int height, float fogDistance, bool on)
		{
			_blockLevels = 0;
			_pixel = Vector4.zero;
			if (on)
			{
				int num = Mathf.Max(1, Mathf.RoundToInt((float)width / 640f));
				if (num > 1)
				{
					_blockSize[0] = num;
					_blockSize[1] = 2 * num;
					_blockSize[2] = 4 * num;
					_blockSize[3] = 8 * num;
					_blockLevels = 4;
				}
				else
				{
					_blockSize[0] = 2;
					_blockSize[1] = 4;
					_blockSize[2] = 8;
					_blockSize[3] = 8;
					_blockLevels = 3;
				}
				int m = _blockSize[_blockLevels - 1];
				float num2 = Mathf.Max(1f, cfg.DetailZoom);
				float num3 = Mathf.Max(8f, Mathf.Min(cfg.BillboardDistance / num2, fogDistance * 0.5f));
				float num4 = Mathf.Max(num3 * 1.5f, num3 + (fogDistance - num3) * GpuStyle.PixelFull);
				_pixel = new Vector4(1f, num3, 3f / Mathf.Log(num4 / num3, 2f), _blockLevels);
				double num5 = (double)focal / (double)Supersample;
				double val = ((double)horizon + (double)focal * (double)lens.Sq / Math.Max(0.02, lens.Cq)) / (double)Supersample;
				val = Math.Max(-1000000.0, Math.Min(1000000.0, val));
				int num6 = PosMod(-(long)Math.Round(num5 * (double)view.Yaw), m);
				int num7 = PosMod((long)Math.Round(val - (double)height), m);
				_pixelGrid = new Vector4(num6, num7, (num > 1) ? 1f : 0f, 1f / num2);
				_pixelSize = new Vector4(_blockSize[0], _blockSize[1], _blockSize[2], _blockSize[3]);
			}
		}

		private static int PosMod(long v, int m)
		{
			return (int)((v % m + m) % m);
		}

		private void SetPixelGlobals(CommandBuffer cmd)
		{
			cmd.SetGlobalVector("_FP_Pixel", _pixel);
			cmd.SetGlobalVector("_FP_PixelGrid", _pixelGrid);
			cmd.SetGlobalVector("_FP_PixelSize", _pixelSize);
		}

		private void DrawPixelBlocks(CommandBuffer cmd, int width, int height)
		{
			if (_blockLevels == 0)
			{
				return;
			}
			int num = _blockSize[_blockLevels - 1];
			for (int i = 0; i < _blockLevels; i++)
			{
				int num2 = _blockSize[i];
				int num3 = (width + num) / num2 + 1;
				int num4 = (height + num) / num2 + 1;
				RenderTexture renderTexture = _blocks[i];
				if (renderTexture == null || renderTexture.width != num3 || renderTexture.height != num4)
				{
					Drop(renderTexture);
					renderTexture = new RenderTexture(num3, num4, 0, RenderTextureFormat.ARGBHalf)
					{
						filterMode = FilterMode.Point,
						wrapMode = TextureWrapMode.Clamp,
						name = "FirstPersonGpuBlocks" + (i + 1)
					};
					renderTexture.Create();
					_blocks[i] = renderTexture;
				}
				cmd.SetGlobalVector("_FP_PixelLevel", new Vector4(i + 1, num2, 0f, 0f));
				cmd.SetRenderTarget(renderTexture);
				cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 6, MeshTopology.Triangles, 3);
			}
			for (int j = 0; j < 4; j++)
			{
				cmd.SetGlobalTexture(BlockNames[j], _blocks[Mathf.Min(j, _blockLevels - 1)]);
			}
		}

		private void ReleasePixel()
		{
			for (int i = 0; i < _blocks.Length; i++)
			{
				if (_blocks[i] != null)
				{
					_blocks[i].Release();
				}
				_blocks[i] = null;
			}
		}

		private void GatherRoomCasters(TerrainCache land, ViewState cull)
		{
			_bakeRoom = false;
			RoomLights.Read();
			Interior roomShape = RoomShape;
			if (!Room || roomShape == null || RoomLights.Get("room_shadows") <= 0f || _modelMat == null || _modelMat.passCount < 3)
			{
				return;
			}
			long num = RoomKey(roomShape);
			if (num == _roomKey && _roomShadow != null && _roomShadow.IsCreated())
			{
				return;
			}
			_pendingKey = num;
			ChooseShadowed(roomShape);
			if (_shadowed.Count == 0)
			{
				_roomKey = num;
				return;
			}
			_castWas.Clear();
			foreach (MeshInstance mesh in roomShape.Meshes)
			{
				_castWas.Add(mesh?.NoShadow ?? false);
				if (mesh != null)
				{
					mesh.NoShadow = false;
				}
			}
			try
			{
				_models.CollectBoth(_instances, roomShape.Meshes, land, cull, -1E+09f, null, shadows: true, new Vector2(cull.X, cull.Y), 100000f, null, default(GpuModels.ShadowFall));
			}
			finally
			{
				for (int i = 0; i < roomShape.Meshes.Count && i < _castWas.Count; i++)
				{
					if (roomShape.Meshes[i] != null)
					{
						roomShape.Meshes[i].NoShadow = _castWas[i];
					}
				}
			}
			_bakeRoom = true;
		}

		private void RoomLightFrame(CommandBuffer cmd, FrameRenderer cfg, ViewState at)
		{
			Atmosphere air = cfg.Air;
			if (!Room || RoomShape == null || air == null || !air.Indoors || OldRoomLightForTest)
			{
				cmd.SetGlobalVector("_FP_RoomOn", Vector4.zero);
				_roomStats = default(RoomStats);
				return;
			}
			if (_bakeRoom)
			{
				BakeRoomShadows(cmd);
			}
			bool flag = _roomShadow != null && _shadowed.Count > 0 && RoomLights.Get("room_shadows") > 0f;
			_lightEye = new Vector3(at.X, at.Y, at.Z);
			SetLights(cmd, cfg);
			SetWindows(cmd, cfg);
			List<PointLight> list = ((cfg.Lights.Count > 64) ? _nearLights : cfg.Lights);
			int num = Mathf.Min(64, list.Count);
			int num2 = 0;
			for (int i = 0; i < 64; i++)
			{
				float value = -1f;
				if (flag && i < num)
				{
					PointLight pointLight = list[i];
					for (int j = 0; j < _shadowed.Count; j++)
					{
						Vector4 vector = _shadowed[j];
						if (Mathf.Abs(vector.x - pointLight.X) < 0.001f && Mathf.Abs(vector.y - pointLight.Y) < 0.001f && Mathf.Abs(vector.z - pointLight.Z) < 0.001f)
						{
							value = j;
							num2++;
							break;
						}
					}
				}
				Vector4 vector2 = _slots[i >> 2];
				vector2[i & 3] = value;
				_slots[i >> 2] = vector2;
			}
			cmd.SetGlobalVectorArray("_FP_LightSlot", _slots);
			cmd.SetGlobalTexture("_FP_RoomShadow", flag ? ((Texture)_roomShadow) : ((Texture)Texture2D.blackTexture));
			float num3 = 1f - air.Night;
			float num4 = Mathf.Lerp(RoomLights.Get("room_fill_night"), RoomLights.Get("room_fill_day"), num3 * Mathf.Clamp01(RoomDaylight));
			float num5 = 0f;
			float num6 = 0f;
			float num7 = 0f;
			for (int k = 0; k < num; k++)
			{
				PointLight pointLight2 = list[k];
				float num8 = Mathf.Min(1f, pointLight2.Radius * pointLight2.Radius / 30f);
				num5 += pointLight2.R * num8;
				num6 += pointLight2.G * num8;
				num7 += pointLight2.B * num8;
			}
			float num9 = RoomLights.Get("room_bounce");
			float a = RoomLights.Get("room_bounce_most");
			Color color = new Color(1f, 0.9f, 0.8f);
			Vector4 vector3 = new Vector4(num4 * color.r + Mathf.Min(a, num5 * num9), num4 * color.g + Mathf.Min(a, num6 * num9), num4 * color.b + Mathf.Min(a, num7 * num9), RoomLights.Get("room_wrap"));
			cmd.SetGlobalVector("_FP_RoomFill", vector3);
			Color color2 = Color.Lerp(new Color(0.16f, 0.19f, 0.3f), new Color(0.68f, 0.74f, 0.84f), num3) * (RoomLights.Get("room_window") * Mathf.Clamp01(RoomDaylight));
			cmd.SetGlobalVector("_FP_RoomSky", new Vector4(color2.r, color2.g, color2.b, cfg.Windows.Count));
			cmd.SetGlobalVector("_FP_RoomK", new Vector4(RoomLights.Get("room_falloff"), RoomLights.Get("room_beam"), RoomLights.Get("room_hands"), RoomLights.Get("room_bias")));
			cmd.SetGlobalVector("_FP_RoomK2", new Vector4(RoomLights.Get("room_soft"), 0f, 0f, 0f));
			cmd.SetGlobalVector("_FP_RoomOn", new Vector4(1f, num3, flag ? ((float)_roomFace) : 0f, RoomLights.Get("room_light")));
			cmd.SetGlobalVector("_FP_RoomDebug", new Vector4(RoomLights.Get("room_debug"), RoomLights.Get("room_debug_slot"), 0f, 0f));
			_roomStats = new RoomStats
			{
				On = true,
				Lights = num,
				Cubes = num2,
				Shadowed = _shadowed.Count,
				Face = _roomFace,
				Windows = cfg.Windows.Count,
				Fill = vector3,
				Bakes = _bakes,
				BakeMs = _bakeMs,
				Copies = _bakeCopies,
				Passes = ((_modelMat != null) ? _modelMat.passCount : (-1))
			};
		}

		private void ChooseShadowed(Interior room)
		{
			_shadowed.Clear();
			List<(Vector4, float)> list = new List<(Vector4, float)>();
			if (room.Lights.Count == 0)
			{
				list.Add((new Vector4(room.HearthX - 0.35f, room.HearthY, 0.35f, 5.5f), 1f));
			}
			foreach (RoomLight light2 in room.Lights)
			{
				PointLight light = light2.Light;
				if (!(light.Radius <= 0.05f))
				{
					list.Add((new Vector4(light.X, light.Y, light.Z, light.Radius), (light.R + light.G + light.B) * light.Radius * light.Radius));
				}
			}
			list.Sort(((Vector4 at, float weight) p, (Vector4 at, float weight) q) => q.weight.CompareTo(p.weight));
			for (int num = 0; num < list.Count; num++)
			{
				if (_shadowed.Count >= 16)
				{
					break;
				}
				bool flag = false;
				foreach (Vector4 item in _shadowed)
				{
					if ((new Vector3(item.x, item.y, item.z) - new Vector3(list[num].Item1.x, list[num].Item1.y, list[num].Item1.z)).sqrMagnitude < 1E-06f)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					_shadowed.Add(list[num].Item1);
				}
			}
		}

		private void BakeRoomShadows(CommandBuffer cmd)
		{
			double totalMilliseconds = _clock.Elapsed.TotalMilliseconds;
			int num = Mathf.Clamp(Mathf.RoundToInt(RoomLights.Get("room_shadow_size")), 64, 512);
			int num2 = Mathf.Max(4, (_shadowed.Count + 3) / 4 * 4);
			if (_roomShadow == null || !_roomShadow.IsCreated() || _roomShadow.width != num * 6 || _roomShadow.height < num * _shadowed.Count)
			{
				if (_roomShadow != null)
				{
					_roomShadow.Release();
				}
				RenderTextureFormat format = (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf) ? RenderTextureFormat.RHalf : RenderTextureFormat.RFloat);
				_roomShadow = new RenderTexture(num * 6, num * num2, 16, format)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonRoomShadows"
				};
				_roomShadow.Create();
			}
			cmd.SetRenderTarget(_roomShadow);
			cmd.ClearRenderTarget(clearDepth: true, clearColor: true, new Color(10000f, 0f, 0f, 0f), 1f);
			float w = RoomLights.Get("room_lamp_clear");
			for (int i = 0; i < _shadowed.Count; i++)
			{
				float y = i * num;
				Vector4 vector = _shadowed[i];
				Vector3 rhs = new Vector3(vector.x, vector.y, vector.z);
				cmd.SetGlobalVector("_FP_RoomLightAt", new Vector4(rhs.x, rhs.y, rhs.z, w));
				Matrix4x4 gPUProjectionMatrix = GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(90f, 1f, 0.02f, vector.w * 1.05f + 0.2f), renderIntoTexture: true);
				for (int j = 0; j < 6; j++)
				{
					Vector3 lhs = FaceForward[j];
					Vector3 vector2 = ((j < 4) ? Vector3.forward : Vector3.up);
					Vector3 lhs2 = Vector3.Cross(lhs, vector2);
					Matrix4x4 matrix4x = default(Matrix4x4);
					matrix4x.SetRow(0, new Vector4(lhs2.x, lhs2.y, lhs2.z, 0f - Vector3.Dot(lhs2, rhs)));
					matrix4x.SetRow(1, new Vector4(vector2.x, vector2.y, vector2.z, 0f - Vector3.Dot(vector2, rhs)));
					matrix4x.SetRow(2, new Vector4(0f - lhs.x, 0f - lhs.y, 0f - lhs.z, Vector3.Dot(lhs, rhs)));
					matrix4x.SetRow(3, new Vector4(0f, 0f, 0f, 1f));
					cmd.SetViewport(new Rect(j * num, y, num, num));
					cmd.SetGlobalMatrix("_FP_RoomVP", gPUProjectionMatrix * matrix4x);
					_instances.Draw(cmd, _modelMat, 0, 2);
				}
			}
			_roomFace = num;
			_bakes++;
			_roomKey = _pendingKey;
			_bakeCopies = _instances.Count(0);
			_bakeMs = _clock.Elapsed.TotalMilliseconds - totalMilliseconds;
		}

		private static long RoomKey(Interior room)
		{
			long num = RuntimeHelpers.GetHashCode(room);
			num = num * 31 + room.Meshes.Count;
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (mesh != null)
				{
					num = num * 31 + ((mesh.Model != null) ? RuntimeHelpers.GetHashCode(mesh.Model) : 0);
					num = num * 31 + (long)Math.Round(mesh.Position.X * 256f);
					num = num * 31 + (long)Math.Round(mesh.Position.Y * 256f);
					num = num * 31 + (long)Math.Round(mesh.Position.Z * 256f);
				}
			}
			foreach (RoomLight light in room.Lights)
			{
				num = (num * 31 + (long)Math.Round(light.Light.X * 256f)) * 31 + (long)Math.Round(light.Light.Y * 256f) + (long)Math.Round(light.Light.Z * 256f) * 7;
			}
			return num * 31 + (long)Math.Round(RoomLights.Get("room_shadow_size"));
		}

		internal string RoomShadowsForTest()
		{
			if (_roomShadow == null || !_roomShadow.IsCreated())
			{
				return "no shadow cubes";
			}
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = _roomShadow;
			Texture2D texture2D = new Texture2D(_roomShadow.width, _roomShadow.height, TextureFormat.RGBAFloat, mipChain: false);
			texture2D.ReadPixels(new Rect(0f, 0f, _roomShadow.width, _roomShadow.height), 0, 0, recalculateMipMaps: false);
			texture2D.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			Color[] pixels = texture2D.GetPixels();
			UnityEngine.Object.Destroy(texture2D);
			int num = Mathf.Max(1, _roomFace);
			int num2 = _roomShadow.height / num;
			StringBuilder stringBuilder = new StringBuilder($"cubes {_roomShadow.width}x{_roomShadow.height} ({num2} rows, {_shadowed.Count} lights):");
			for (int i = 0; i < num2; i++)
			{
				stringBuilder.Append($" row{i}[");
				for (int j = 0; j < 6; j++)
				{
					int num3 = 0;
					float num4 = float.MaxValue;
					for (int k = i * num; k < (i + 1) * num; k++)
					{
						for (int l = j * num; l < (j + 1) * num; l++)
						{
							float r = pixels[k * _roomShadow.width + l].r;
							if (r < 1000f)
							{
								num3++;
								num4 = Mathf.Min(num4, r);
							}
						}
					}
					stringBuilder.Append((num3 == 0) ? " -" : $" {num3 * 100 / (num * num)}%>{num4:F2}");
				}
				stringBuilder.Append(" ]");
			}
			return stringBuilder.ToString();
		}

		private void ReleaseRoomShadows()
		{
			if (_roomShadow != null)
			{
				_roomShadow.Release();
			}
			_roomShadow = null;
			_roomKey = long.MinValue;
		}

		private void SetNaturalSky(CommandBuffer cmd, Atmosphere a, float low)
		{
			if (!a.Natural)
			{
				cmd.SetGlobalVector("_FP_MoonPhase", Vector4.zero);
				cmd.SetGlobalVector("_FP_MoonLook", Vector4.zero);
				cmd.SetGlobalVector("_FP_SunGlow", Vector4.zero);
				cmd.SetGlobalVector("_FP_SunGlowCol", Vector4.zero);
			}
			else
			{
				FrameRenderer.NaturalMoon(a, out var disc, out var halo, out var dark, out var _, out var _, out var pu, out var pv, out var cosTerm, out var invR);
				cmd.SetGlobalVector("_FP_Moon", new Vector4(a.MoonDisc.X, a.MoonDisc.Y, a.MoonDisc.Z, disc));
				cmd.SetGlobalVector("_FP_MoonPhase", new Vector4(pu, pv, cosTerm, invR));
				cmd.SetGlobalVector("_FP_MoonLook", new Vector4(halo, dark, 0f, 0f));
				FrameRenderer.SunGlow(a, low, out var bloom, out var haloGain, out var haloBroad, out var discWhite, out var haloCol);
				cmd.SetGlobalVector("_FP_SunGlow", new Vector4(bloom, haloGain, haloBroad, discWhite));
				cmd.SetGlobalVector("_FP_SunGlowCol", Col(haloCol));
			}
		}

		private Vector4 FlareFor(FrameRenderer cfg, ViewState view, float col, float row, float focal)
		{
			Atmosphere air = cfg.Air;
			if (!LensFlare || air.Indoors || _headsetFrame || cfg.ForBackdrop)
			{
				return Vector4.zero;
			}
			Vector4 vector = GpuStyle.Params[9];
			float cloud = cfg.SunCloudCover(view.X, view.Y, (vector.x > 0f) ? vector.x : 1f, (vector.y > 0f) ? vector.y : 1f);
			float num = FrameRenderer.FlareStrength(air, col, row, Width, Height, cloud);
			if (num < 0.01f)
			{
				return Vector4.zero;
			}
			float num2 = 1f - Mathf.Min(1f, Mathf.Max(0f, air.SunDisc.Z) * 3f);
			return new Vector4(col, row, num, (0.026f + 0.016f * num2) * focal);
		}

		private void FlareOpen(CommandBuffer cmd, FrameRenderer cfg, ViewState view, float focal, float horizon, int sw, Lens lens, float cx)
		{
			_flare = Vector4.zero;
			Vec3 sunDisc = cfg.Air.SunDisc;
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			float num3 = sunDisc.X * num + sunDisc.Y * num2;
			float num4 = num3 * lens.Cq + sunDisc.Z * lens.Sq;
			float col;
			float row;
			if (HasEye)
			{
				if (!EyeDirection(new Vector3(sunDisc.X, sunDisc.Y, sunDisc.Z), focal, cx, horizon, out col, out row))
				{
					return;
				}
			}
			else
			{
				if (num4 < 0.1f)
				{
					return;
				}
				col = (float)sw * 0.5f + (sunDisc.X * num2 + sunDisc.Y * (0f - num)) / num4 * focal;
				row = horizon - (sunDisc.Z * lens.Cq - num3 * lens.Sq) / num4 * focal;
			}
			_flare = FlareFor(cfg, view, col / (float)Supersample, row / (float)Supersample, focal / (float)Supersample);
			if (!(_flare.z <= 0f) && _styleMat.passCount >= 9)
			{
				if (_flareOpen == null)
				{
					_flareOpen = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBHalf)
					{
						filterMode = FilterMode.Point,
						name = "FirstPersonGpuFlareOpen"
					};
					_flareOpen.Create();
				}
				cmd.SetGlobalVector("_FP_Flare", _flare);
				cmd.SetGlobalTexture("_SceneData", _sceneData);
				cmd.SetRenderTarget(_flareOpen);
				cmd.DrawProcedural(Matrix4x4.identity, _styleMat, 8, MeshTopology.Triangles, 3);
				cmd.SetGlobalTexture("_FlareOpen", _flareOpen);
			}
		}

		private void ReleaseFlare()
		{
			if (_flareOpen != null)
			{
				_flareOpen.Release();
			}
			_flareOpen = null;
		}

		private static float Gathered(FrameRenderer cfg, ViewState view)
		{
			float num = (cfg.ShadowFocusSet ? (view.X - cfg.ShadowFocusX) : 0f);
			float num2 = (cfg.ShadowFocusSet ? (view.Y - cfg.ShadowFocusY) : 0f);
			return Mathf.Max(1f, Mathf.Max(cfg.BillboardDistance, Mathf.Sqrt(num * num + num2 * num2) + 30f) - 4f);
		}

		private void NaturalShadows(CommandBuffer cmd, FrameRenderer cfg, TerrainCache land, ViewState view, Atmosphere a)
		{
			bool flag = NaturalSun && !a.Indoors;
			cmd.SetGlobalVector("_FP_ShadowN", flag ? new Vector4(1f, 0.12f, 3f, Gathered(cfg, view)) : Vector4.zero);
			_models.FieldLight = _sunField.Bind(cmd, land, NaturalSun, flag && !Room);
			ContactShadows(cmd, a);
		}

		private void ContactShadows(CommandBuffer cmd, Atmosphere a)
		{
			if (!ContactShade || !NaturalSun || ShadowQuality < 3 || ShadowDarkness <= 0f || a.Indoors || Room)
			{
				cmd.SetGlobalVector("_FP_Contact", Vector4.zero);
				return;
			}
			float num = Mathf.Clamp01(ShadowDarkness);
			cmd.SetGlobalVector("_FP_Contact", new Vector4(1f, 0.4f * num * MapEase(a), 0.6f, 0.25f * num));
		}

		private static float MapEase(Atmosphere a)
		{
			return Mathf.Clamp01((Mathf.Max(a.LightR, Mathf.Max(a.LightG, a.LightB)) - 0.05f) / 0.1f);
		}

		private void HoldMap(CommandBuffer cmd, Atmosphere a, bool map)
		{
			cmd.SetGlobalVector("_FP_ShadowE", new Vector4((map && NaturalSun) ? (1f - MapEase(a)) : 0f, 0f, 0f, 0f));
		}

		private void FitShadows(TerrainCache land, Vector3 centre, Vector3 l, float reach0, out float up, out float up0, out float lift, out float depth)
		{
			float num = Mathf.Clamp01(l.z);
			float num2 = Mathf.Sqrt(Mathf.Max(0f, 1f - num * num));
			float num3 = HighestNear(land, centre.x, centre.y, ShadowReach) + 12f;
			float num4 = Mathf.Min(_terrain.Floor, centre.z);
			float num5 = Mathf.Max(0f, Mathf.Max(num3 - centre.z, centre.z - num4));
			up = FrameRenderer.FitRung(ref _upHeld, Mathf.Min(ShadowReach, ShadowReach * num + num5 * num2 + 2f + 0.02f * ShadowReach), ShadowReach);
			up0 = ((reach0 == ShadowReach) ? up : FrameRenderer.FitRung(ref _up0Held, Mathf.Min(reach0, reach0 * num + num5 * num2 + 2f + 0.02f * reach0), reach0));
			float b = ShadowReach * 1.25f + 40f;
			lift = Mathf.Max(150f, b);
			depth = lift + Mathf.Max(170f, b);
		}

		private Vector4 StippleOrigins()
		{
			if (NaturalSun)
			{
				if (!_cascades)
				{
					return new Vector4(_wideCount.A, _wideCount.B, 0f, 0f);
				}
				return new Vector4(_nearCount.A, _nearCount.B, _wideCount.A, _wideCount.B);
			}
			return Vector4.zero;
		}

		private static Vector3 NaturalCentre(TerrainCache land, float gx, float gy, bool focus, ViewState view)
		{
			float num = (focus ? gx : view.X);
			float num2 = (focus ? gy : view.Y);
			return new Vector3(Mathf.Floor(gx / 4f) * 4f + 2f, Mathf.Floor(gy / 4f) * 4f + 2f, land.SampleHeight(Mathf.Floor(num / 4f) * 4f + 2f, Mathf.Floor(num2 / 4f) * 4f + 2f));
		}

		private Vector3 SnapNatural(Vector3 p, FrameRenderer cfg, ViewState view, float z, Vector3 ax, Vector3 bx, Vector3 l, float k, float kb, bool near)
		{
			Vec3 eye = new Vec3(cfg.ShadowFocusSet ? cfg.ShadowFocusX : view.X, cfg.ShadowFocusSet ? cfg.ShadowFocusY : view.Y, z);
			Vec3 vec = new Vec3(ax.x, ax.y, ax.z);
			Vec3 vec2 = new Vec3(bx.x, bx.y, bx.z);
			Vec3 l2 = new Vec3(l.x, l.y, l.z);
			Vec3 p2 = new Vec3(p.x, p.y, p.z);
			Vec3 c = (near ? _nearAnchor.Snap(p2, eye, vec, vec2, l2, k, kb) : _wideAnchor.Snap(p2, eye, vec, vec2, l2, k, kb));
			if (near)
			{
				_nearCount.Next(eye, c, vec, vec2, k, kb);
			}
			else
			{
				_wideCount.Next(eye, c, vec, vec2, k, kb);
			}
			return new Vector3(c.X, c.Y, c.Z);
		}

		private static float HighestNear(TerrainCache land, float x, float y, float reach)
		{
			float[] blockMax = land.BlockMax;
			int blocksX = land.BlocksX;
			int blocksY = land.BlocksY;
			if (blockMax.Length < blocksX * blocksY || blocksX <= 0 || blocksY <= 0)
			{
				return land.MaxHeight;
			}
			int num = Mathf.Max(0, (int)Mathf.Floor((x - reach) / 8f));
			int num2 = Mathf.Min(blocksX - 1, (int)Mathf.Floor((x + reach) / 8f));
			int num3 = Mathf.Max(0, (int)Mathf.Floor((y - reach) / 8f));
			int num4 = Mathf.Min(blocksY - 1, (int)Mathf.Floor((y + reach) / 8f));
			float num5 = float.MinValue;
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					float num6 = blockMax[i * blocksX + j];
					num5 = Mathf.Max(num5, (num6 >= float.MaxValue) ? land.MaxHeight : num6);
				}
			}
			if (!(num5 > float.MinValue))
			{
				return land.OutsideHeight;
			}
			return num5;
		}

		public void KeepSunField(TerrainCache land)
		{
			_sunField.KeepUp(land, NaturalSun);
		}

		private void DrawVeil(CommandBuffer cmd, FrameRenderer cfg, ViewState cull, float cullFocal, int sw, int sh, float reach, Lens lens)
		{
			bool flag = cfg.Cut.On;
			if (flag)
			{
				EnsureVeil(sw, sh);
				_veilTargets[0] = _veilCol;
				_veilTargets[1] = _veilData;
				cmd.SetRenderTarget(_veilTargets, _veilCol.depthBuffer);
				cmd.ClearRenderTarget(clearDepth: true, clearColor: true, Color.clear, 1f);
				cmd.SetGlobalTexture("_SceneData", _sceneData);
				cmd.SetGlobalFloat("_FP_VeilPass", 1f);
				_terrain.Draw(cmd, _terrainMat, cull, cullFocal, sw, sh, reach, lens);
				cmd.SetGlobalFloat("_FP_VeilPass", 0f);
			}
			else
			{
				ReleaseVeil();
			}
			cmd.SetGlobalVector("_FP_VeilOn", new Vector4(flag ? 1f : 0f, 0f, 0f, 0f));
			cmd.SetGlobalTexture("_VeilCol", flag ? ((Texture)_veilCol) : ((Texture)Texture2D.blackTexture));
			cmd.SetGlobalTexture("_VeilData", flag ? ((Texture)_veilData) : ((Texture)Texture2D.blackTexture));
		}

		private void EnsureVeil(int sw, int sh)
		{
			if (!(_veilCol != null) || _veilCol.width != sw || _veilCol.height != sh)
			{
				ReleaseVeil();
				_veilCol = new RenderTexture(sw, sh, 32, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuVeil"
				};
				_veilCol.Create();
				_veilData = new RenderTexture(sw, sh, 0, RenderTextureFormat.ARGBFloat)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuVeilData"
				};
				_veilData.Create();
			}
		}

		private void ReleaseVeil()
		{
			if (_veilCol != null)
			{
				_veilCol.Release();
			}
			if (_veilData != null)
			{
				_veilData.Release();
			}
			_veilCol = (_veilData = null);
		}
	}
}
