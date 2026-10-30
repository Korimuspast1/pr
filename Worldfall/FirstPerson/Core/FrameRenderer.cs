using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FirstPerson.Core
{
	public sealed class FrameRenderer
	{
		private struct Tint3
		{
			public int R;

			public int G;

			public int B;

			public Tint3(float r, float g, float b)
			{
				R = (int)(r * 256f);
				G = (int)(g * 256f);
				B = (int)(b * 256f);
			}
		}

		private struct GroundSample
		{
			public float H;

			public uint Col;

			public uint Texel;

			public byte Flags;

			public byte Mat;

			public int Sun;

			public int Open;

			public bool Inside;

			public int Ti;

			public int I00;

			public int I10;

			public int I01;

			public int I11;

			public float Tx;

			public float Ty;

			public byte Fl;

			public float Coast;
		}

		private struct VoxelPrep
		{
			public bool Visible;

			public int C0;

			public int C1;

			public int R0;

			public int R1;

			public float DepthMin;

			public float Ox;

			public float Oy;

			public float Oz;

			public float NearT;

			public bool Coarse;

			public Tint3 ShadeU;

			public Tint3 ShadeUn;

			public Tint3 ShadeV;

			public Tint3 ShadeVn;

			public Tint3 ShadeN;

			public Tint3 ShadeNn;

			public bool Shadow;

			public int SC0;

			public int SC1;

			public int SR0;

			public int SR1;

			public float Sdx;

			public float Sdy;

			public float Sdz;

			public float SDepthMin;

			public float SDepthMax;

			public ShadowMask Mask;

			public byte Share;
		}

		private sealed class ShadowMask
		{
			public byte[] Cells;

			public int W;

			public int H;

			public float U0;

			public float N0;

			public float Kx;

			public float Kz;

			public float Top;
		}

		private struct MaskKey : IEquatable<MaskKey>
		{
			public VoxelShape Shape;

			public int Kx;

			public int Kz;

			public bool Equals(MaskKey o)
			{
				if (Shape == o.Shape && Kx == o.Kx)
				{
					return Kz == o.Kz;
				}
				return false;
			}

			public override bool Equals(object obj)
			{
				if (obj is MaskKey o)
				{
					return Equals(o);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return (RuntimeHelpers.GetHashCode(Shape) * 397) ^ (Kx * 7919) ^ Kz;
			}
		}

		private struct VoxelHit
		{
			public int Cell;

			public int Face;

			public float T;

			public uint Color;
		}

		private struct MVert
		{
			public float Side;

			public float Up;

			public float Depth;

			public float Wx;

			public float Wy;

			public float Wz;

			public Vec3 Amb;

			public Vec3 Dir;

			public Vec3 Obj;

			public float Sun;

			public float U;

			public float V;
		}

		private struct STri
		{
			public float X0;

			public float Y0;

			public float X1;

			public float Y1;

			public float X2;

			public float Y2;

			public float W0;

			public float W1;

			public float W2;

			public Vec3 A0;

			public Vec3 A1;

			public Vec3 A2;

			public Vec3 D0;

			public Vec3 D1;

			public Vec3 D2;

			public Vec3 O0;

			public Vec3 O1;

			public Vec3 O2;

			public float U0;

			public float V0;

			public float U1;

			public float V1;

			public float U2;

			public float V2;

			public MeshTexture Tex;

			public uint Color;

			public byte Kind;

			public byte Flags;

			public byte Axis;

			public int Inst;

			public int MinX;

			public int MaxX;

			public int MinY;

			public int MaxY;

			public bool Fine;

			public float Gu;

			public float Gv;

			public float Nu;

			public float Nv;

			public float Nd;

			public float Sw;

			public float Sm;

			public float Sk;
		}

		private struct Meteor
		{
			public Vec3 Head;

			public Vec3 Back;

			public Vec3 Side;

			public Vec3 Mid;

			public float ReachCos;

			public float Len;

			public float Width;

			public float Bright;

			public float HeadSize;

			public float Train;

			public uint Color;

			public uint TrainColor;
		}

		private struct LightPrep
		{
			public int C0;

			public int C1;

			public int R0;

			public int R1;

			public float InvR2;

			public float TLo;

			public float THi;
		}

		private struct Cap
		{
			public float X;

			public float Y;

			public float Half;

			public float Age;

			public float Life;

			public int Epoch;

			public float Reach2;
		}

		internal struct TexelAnchor
		{
			private double _x;

			private double _y;

			private double _z;

			private int _iu;

			private int _iv;

			private bool _set;

			public int CentreU;

			public int CentreV;

			public Vec3 Snap(Vec3 p, Vec3 eye, Vec3 ax, Vec3 bx, Vec3 l, float k, float kb)
			{
				if (!_set)
				{
					_x = eye.X;
					_y = eye.Y;
					_z = eye.Z;
					_set = true;
				}
				double num = (double)eye.X - _x;
				double num2 = (double)eye.Y - _y;
				double num3 = (double)eye.Z - _z;
				double num4 = Math.Round((num * (double)ax.X + num2 * (double)ax.Y + num3 * (double)ax.Z) * (double)k);
				double num5 = Math.Round((num * (double)bx.X + num2 * (double)bx.Y + num3 * (double)bx.Z) * (double)kb);
				double num6 = num4 / (double)k;
				double num7 = num5 / (double)kb;
				double num8 = num * (double)l.X + num2 * (double)l.Y + num3 * (double)l.Z;
				_iu += (int)num4;
				_iv += (int)num5;
				_x += (double)ax.X * num6 + (double)bx.X * num7 + (double)l.X * num8;
				_y += (double)ax.Y * num6 + (double)bx.Y * num7 + (double)l.Y * num8;
				_z += (double)ax.Z * num6 + (double)bx.Z * num7 + (double)l.Z * num8;
				double num9 = (double)p.X - _x;
				double num10 = (double)p.Y - _y;
				double num11 = (double)p.Z - _z;
				double num12 = num9 * (double)ax.X + num10 * (double)ax.Y + num11 * (double)ax.Z;
				double num13 = num9 * (double)bx.X + num10 * (double)bx.Y + num11 * (double)bx.Z;
				double num14 = Math.Round(num12 * (double)k);
				double num15 = Math.Round(num13 * (double)kb);
				double num16 = num14 / (double)k - num12;
				double num17 = num15 / (double)kb - num13;
				CentreU = _iu + (int)num14;
				CentreV = _iv + (int)num15;
				return new Vec3((float)((double)p.X + (double)ax.X * num16 + (double)bx.X * num17), (float)((double)p.Y + (double)ax.Y * num16 + (double)bx.Y * num17), (float)((double)p.Z + (double)ax.Z * num16 + (double)bx.Z * num17));
			}
		}

		private sealed class ForJob : WorkerPool.Job
		{
			public Action<int> Body;

			public int N;

			public int Per;

			public override void Run(int chunk, int worker)
			{
				Action<int> body = Body;
				int i = chunk * Per;
				for (int num = Math.Min(N, i + Per); i < num; i++)
				{
					body(i);
				}
			}
		}

		public bool ActorClarity = true;

		public int ActorTagEnd;

		public float ActorHaze = 0.65f;

		public float ActorEdgeNear = 22f;

		public float ActorEdgeFar = 45f;

		public float ActorEdgeStrength = 0.75f;

		public readonly List<FarActor> FarActors = new List<FarActor>();

		public int FarActorsDrawn;

		private byte[] _actorMask = new byte[0];

		private float _aeSpan;

		private Action<int> _actorEdgeRow;

		private readonly List<MeshInstance> _solidMeshes = new List<MeshInstance>();

		private readonly List<MeshInstance> _vaporMeshes = new List<MeshInstance>();

		private bool _vaporPass;

		private const byte VaporBack = 128;

		private float[] _vaporNear = new float[0];

		private float[] _vaporFar = new float[0];

		private float[] _vaporDens = new float[0];

		private float[] _vaporShare = new float[0];

		private float[] _vaporGlow = new float[0];

		private uint[] _vaporColor = new uint[0];

		private byte[] _vaporSeen = new byte[0];

		private float[] _vaporNear2 = new float[0];

		private float[] _vaporSeam = new float[0];

		private uint[] _vaporColor2 = new uint[0];

		private const byte VaporFront = 1;

		private const byte VaporHidden = 2;

		private const byte VaporFar = 4;

		private const byte VaporSecond = 8;

		private float[] _vaporA = new float[0];

		private float[] _vaporDepth = new float[0];

		private bool _vaporLayer;

		private int[] _vaporRows = new int[0];

		private int _vaporRowLo;

		private int _vaporRowHi = -1;

		public int CloudPixels;

		public float CloudModelMs;

		public float CloudPrepMs;

		public float CloudRasterMs;

		public int Width;

		public int Height;

		public uint[] Pixels = new uint[0];

		public float[] Depth = new float[0];

		public float MaxDistance = 120f;

		public float BillboardDistance = 72f;

		public float DetailZoom = 1f;

		private float _lodK = 1f;

		public float WallModelRange;

		public float Time;

		public bool UseParallel = true;

		public SightCut Cut;

		public Atmosphere Air = Atmosphere.Noon();

		public readonly List<PointLight> Lights = new List<PointLight>();

		public float RippleX;

		public float RippleY;

		public float RippleStrength;

		public readonly float[] Wakes = new float[64];

		public int WakeCount;

		private static readonly uint FireColor = TerrainCache.Rgb(255, 128, 24);

		private static readonly uint EmberGlow = TerrainCache.Rgb(255, 176, 60);

		private static readonly uint LavaGlow = TerrainCache.Rgb(255, 214, 64);

		public int PickTag = -1;

		public float PickDepth;

		private ViewState _view;

		private TerrainCache _terrain;

		private float _focal;

		private float _horizon;

		private float _fx;

		private float _fy;

		private float _rx;

		private float _ry;

		private float _maxTerrain;

		private readonly int[] _stripePick = new int[64];

		private readonly float[] _stripePickDepth = new float[64];

		private const int ColumnChunk = 16;

		private Action<int> _renderColumns;

		private Action<int, int, int> _renderObjects;

		private Action<int, int, int> _renderOverlay;

		private Action<int, int, int> _blendedStripe;

		private Action<int> _prepareVoxel;

		public float TerrainMs;

		public float ObjectsMs;

		public float PostMs;

		public float LightsMs;

		public float OverlayMs;

		public float FinishMs;

		public float WeatherMs;

		public float BlobMs;

		public float VoxelShadowMs;

		public float VoxelMs;

		public float SpriteMs;

		public float PrepMs;

		public float ParallelMs;

		public float StripeSumMs;

		public int Stripes;

		public static int StripesPerThread = 2;

		private readonly float[] _stripeTimes = new float[256];

		private static readonly List<Billboard> _noBillboards = new List<Billboard>();

		private static readonly List<VoxelObject> _noVoxels = new List<VoxelObject>();

		private List<Billboard> _billboards;

		private List<VoxelObject> _voxels;

		private float[] _overlayDepth = new float[0];

		private float[] _overlayClear = new float[0];

		private int _ambR;

		private int _ambG;

		private int _ambB;

		private int _litR;

		private int _litG;

		private int _litB;

		private Tint3 _flatTint;

		private Tint3 _shadowTint;

		private Tint3 _waterTint;

		private Tint3 _cloudTint;

		private Vec3 _light;

		private float _hazeDensity;

		private float _fadeStart;

		private float _fadeInv;

		private float _ambLum;

		private const float InvHazeHeight = 0.0625f;

		private static readonly float[] ExpTable = BuildExpTable();

		private const float NoCoast = 9f;

		private static readonly uint PathEarth = TerrainCache.Rgb(146, 116, 80);

		private const float GrassRange = 12f;

		private const float GrassHeight = 0.05f;

		private const float GlowShare = 0.42f;

		private const float MistShare = 0.6f;

		private float _waveRange = 24f;

		private float _waveLift;

		private const float ShoreFoamReach = 0.3f;

		private static readonly uint Shallows = TerrainCache.Rgb(150, 214, 206);

		private const int SinSize = 1024;

		private const float SinScale = 512f / MathF.PI;

		private static readonly float[] SinTable = BuildSinTable();

		private const float W1x = 0.8f;

		private const float W1y = 0.6f;

		private const float W1k = 1.257f;

		private const float W1w = 1.1f;

		private const float W1a = 0.016f;

		private const float W2x = -0.35f;

		private const float W2y = 0.94f;

		private const float W2k = 2.03f;

		private const float W2w = 1.5f;

		private const float W2a = 0.01f;

		private const float W3x = 0.93f;

		private const float W3y = -0.37f;

		private const float W3k = 3.3f;

		private const float W3w = 2.1f;

		private const float W3a = 0.005f;

		private static readonly uint LavaCrust = TerrainCache.Rgb(54, 42, 40);

		private static readonly uint LavaSeam = TerrainCache.Rgb(228, 76, 22);

		private static readonly uint LavaDeep = TerrainCache.Rgb(226, 88, 18);

		private static readonly uint DryGrass = TerrainCache.Rgb(168, 170, 70);

		private static readonly uint LushGrass = TerrainCache.Rgb(34, 104, 64);

		private static readonly uint WarmStone = TerrainCache.Rgb(170, 140, 118);

		private static readonly uint CoolStone = TerrainCache.Rgb(104, 104, 140);

		private readonly List<int> _blended = new List<int>();

		private byte[] _skyCover = new byte[0];

		private readonly Dictionary<MaskKey, ShadowMask> _masks = new Dictionary<MaskKey, ShadowMask>();

		private readonly object _maskLock = new object();

		public float ShadowDistance = 22f;

		public int DebugSkip;

		private static readonly bool NoEmptySkip = Environment.GetEnvironmentVariable("FP_NOSKIP") == "1";

		private VoxelPrep[] _prep = new VoxelPrep[0];

		private Tint3 _eyeLight;

		private static readonly uint WindowLit = TerrainCache.Rgb(255, 196, 110);

		private const float LeafDetailRange = 11f;

		private static readonly int[] LeafTexture = new int[16]
		{
			150, 118, 92, 138, 108, 158, 128, 84, 88, 132,
			150, 114, 140, 86, 110, 146
		};

		private static readonly int[] BarkTexture = new int[4] { 140, 100, 126, 92 };

		[ThreadStatic]
		private static VoxelHit[] _hitsA;

		[ThreadStatic]
		private static VoxelHit[] _hitsB;

		private static readonly bool NoPairs = Environment.GetEnvironmentVariable("FP_NOPAIRS") == "1";

		public readonly List<FarProp> FarProps = new List<FarProp>();

		public int FarPropsDrawn;

		public float FarModelReach;

		private static readonly uint TrunkColor = TerrainCache.Rgb(74, 52, 36);

		public uint ScreenFlash;

		private float _flashK;

		private Action<int> _flashBand;

		public readonly List<MeshInstance> Meshes = new List<MeshInstance>();

		public List<CrowdItem> Crowd = new List<CrowdItem>();

		public bool TerrainShadowsOnModels = true;

		public float MeshMs;

		public int MeshTriangles;

		public float MeshPrepMs;

		public float MeshRasterMs;

		public float MeshBinMs;

		public float ModelShadowMs;

		private MVert[] _mv = new MVert[0];

		private STri[] _st = new STri[0];

		private int[] _vStart = new int[0];

		private int[] _tStart = new int[0];

		private int[] _tCount = new int[0];

		private int[] _binStart = new int[0];

		private int[] _binFill = new int[0];

		private int[] _binItems = new int[0];

		private byte[] _colStripe = new byte[0];

		private int _colStripeW;

		private int _colStripes;

		private ushort[] _triStripes = new ushort[0];

		private readonly int[] _meshPick = new int[64];

		private readonly float[] _meshPickDepth = new float[64];

		private const float MeshNear = 0.04f;

		private static readonly Comparison<MeshInstance> NearestFirst = (MeshInstance a, MeshInstance b) => a.SortDepth.CompareTo(b.SortDepth);

		[ThreadStatic]
		private static Mat3[] _partM;

		[ThreadStatic]
		private static Vec3[] _partT;

		public uint[] Backdrop;

		private byte[] _portalMask = new byte[0];

		private bool _portalPass;

		public int PortalPixels;

		public readonly List<MeshInstance> OverlayMeshes = new List<MeshInstance>();

		private List<MeshInstance> _pass;

		private float[] _passDepth;

		private bool _overlayPass;

		public int MeshesHidden;

		public static bool SkipHidden = true;

		private int _meshesHidden;

		private float[] _tileMax = new float[0];

		private int _tilesX;

		private int _tilesY;

		private Action<int> _prepareMesh;

		private Action<int> _tileMaxBand;

		private Action<int, int, int> _rasterStripe;

		private Action<int, int, int> _rasterCloudStripe;

		[ThreadStatic]
		private static MVert[] _clip;

		public int NightNumber;

		public float ColdSky;

		public float ForceComet = -1f;

		public float ForceShower = -1f;

		public float ForceAurora = -1f;

		private Vec3 _radiant;

		private bool _nightOn;

		private float _nightVis;

		private bool _starTurnOn;

		private Vec3 _st0;

		private Vec3 _st1;

		private Vec3 _st2;

		private static readonly Vec3 MilkyPole = new Vec3(0.62f, -0.7f, 0.34f).Normalized();

		private static readonly Vec3 MilkyA = Vec3.Cross(MilkyPole, new Vec3(0f, 0f, 1f)).Normalized();

		private static readonly Vec3 MilkyB = Vec3.Cross(MilkyPole, MilkyA);

		private static readonly Vec3 MilkyCore = (MilkyA * 0.82f + MilkyB * 0.57f).Normalized();

		private float _cometBright;

		private float _cometReachCos;

		private float _cometLen;

		private float _cometW0;

		private float _cometSpread;

		private float _cometCurve;

		private float _cometCore;

		private float _cometComa;

		private Vec3 _cometHead;

		private Vec3 _cometTail;

		private Vec3 _cometSide;

		private readonly Meteor[] _meteors = new Meteor[8];

		private int _meteorCount;

		private bool _shower;

		private float _auroraVis;

		private float[] _auBase = new float[0];

		private float[] _auHeight = new float[0];

		private float[] _auBright = new float[0];

		private int _snowCheck = -1000;

		private float _snowNear;

		public int PaletteLevels = 18;

		public float Vignette = 0.2f;

		public bool Finish = true;

		public readonly List<LightWindow> Windows = new List<LightWindow>();

		private float[] _accR = new float[0];

		private float[] _accG = new float[0];

		private float[] _accB = new float[0];

		private LightPrep[] _lightPrep = new LightPrep[0];

		private int _plStripeW;

		private int _wbCount;

		private int _finSat;

		private float _plGain;

		private float _shaftQsx;

		private float _shaftQsy;

		private float _shaftSr;

		private float _shaftSg;

		private float _shaftSb;

		private float _wbDay;

		private float _wbSr;

		private float _wbSg;

		private float _wbSb;

		private Vec3 _wbLight;

		private bool _finQuant;

		private Action<int> _pointLightStripe;

		private Action<int> _shaftRow;

		private Action<int> _shaftAddRow;

		private Action<int> _windowBeamRow;

		private Action<int> _finishBand;

		private const int ShaftSamples = 28;

		private float[] _shaftMask = new float[0];

		private float[] _shaft = new float[0];

		private static readonly float[] FlareAlong = new float[5] { 0.38f, 0.72f, 1.18f, 1.45f, 1.9f };

		private static readonly float[] FlareSize = new float[5] { 0.03f, 0.016f, 0.05f, 0.024f, 0.085f };

		private static readonly bool[] FlareRing = new bool[5] { false, false, true, false, true };

		private static readonly float[] FlareWeight = new float[5] { 0.2f, 0.24f, 0.2f, 0.18f, 0.12f };

		private static readonly Vec3[] FlareTint = new Vec3[5]
		{
			new Vec3(1f, 0.85f, 0.55f),
			new Vec3(0.75f, 1f, 0.7f),
			new Vec3(0.7f, 0.8f, 1f),
			new Vec3(1f, 0.6f, 0.8f),
			new Vec3(1f, 0.75f, 0.5f)
		};

		private const float FlareRays = 0.3f;

		private const float FlareRayLong = 0.11f;

		private const float FlareRayShort = 0.055f;

		private const float FlareGather = 0.35f;

		private const int BeamStride = 9;

		private float[] _beamWin = new float[0];

		private float[] _beamCol = new float[0];

		private float[] _beamLen = new float[0];

		private byte[] _vignette = new byte[0];

		private int _vignetteW;

		private int _vignetteH;

		private float _vignetteFor = -1f;

		private readonly byte[] _quant = new byte[4096];

		private int _quantLevels = -1;

		private readonly int[] _toneR = new int[256];

		private readonly int[] _toneG = new int[256];

		private readonly int[] _toneB = new int[256];

		private readonly byte[] _curve = new byte[256];

		private readonly byte[] _curveQuant = new byte[4096];

		private bool _curveQuantStale = true;

		private float _toneNight = -1f;

		public float FadeAt;

		public float HazeScale = 1f;

		public bool SeaWaves = true;

		private float _sea = 1f;

		private byte[] _seaMap;

		private int _seaW;

		private int _seaH;

		private int _seaOutside;

		private const float CapU = 5f;

		private const float CapV = 5f;

		private const float CapPeriod = 5f;

		private const float CapStagger = 2.3f;

		private const float CapRide = 0.8750995f;

		private const float CapFar = 80f;

		private const float CapFade = 45f;

		private const float BreakFrom = 3.6f;

		private const float BreakTo = 0.3f;

		private const float BreakFar = 40f;

		private const float BreakFade = 22f;

		private Cap[] _caps = new Cap[64];

		private int[] _capGrid = new int[0];

		private int[] _capRow = new int[0];

		private int _capU0;

		private int _capRows;

		private const int CapGridMost = 72;

		public float SeeIntoRange = 12f;

		private const float WaterIndex = 1.333f;

		private const float SeenDepth = 7f;

		private const float FloorWobble = 0.35f;

		private const float PatternRange = 10f;

		private const float CausticDepth = 3f;

		private const float CausticShare = 0.4f;

		public float ShadowReach = 30f;

		public int ShadowMapSize = 512;

		public float ShadowStrength = 1f;

		private int ShadowSize = 512;

		private float _shadowFloor;

		private float[] _shadowMap = new float[0];

		private float[] _shadowU = new float[0];

		private float[] _shadowV = new float[0];

		private float[] _shadowD = new float[0];

		private float[] _casterLo = new float[0];

		private float[] _casterHi = new float[0];

		private byte[] _sunlit = new byte[0];

		private bool _shadowReady;

		private Vec3 _sA;

		private Vec3 _sB;

		private Vec3 _sL;

		private Vec3 _sC;

		private float _sScale;

		private float _sScaleB;

		public float ShadowMs;

		public int ShadowRebuilds;

		public string ShadowRebuildInfo = "";

		private long _staticSoft;

		private int _framesSinceStatic;

		public bool ShadowFocusSet;

		public float ShadowFocusX;

		public float ShadowFocusY;

		private float[] _staticMap = new float[0];

		private long _staticKey = long.MinValue;

		private int _dirtyLo;

		private int _dirtyHi = 512;

		private bool _dcStatics;

		private float _dcHalf;

		private float _dcReach2;

		private int _dcAny;

		private float[] _dcMap;

		private float _msReach;

		private float _msHalfW;

		private float _msHalf;

		private float _msInvHalf2;

		private float _msInvReach2;

		private float _msGatherIn2;

		private float _msGatherOut2;

		private float _msCells16;

		private float _msCells8;

		private Action<int> _casterVertices;

		private Action<int> _casterBand;

		private Action<int> _modelShadowRow;

		private const int ShadowBands = 64;

		private uint[] _fogCol = new uint[0];

		private float[] _sunSide = new float[0];

		private float[] _invLen = new float[0];

		private float[] _colDx = new float[0];

		private float[] _colDy = new float[0];

		private uint[] _scrFog = new uint[0];

		private float[] _rowKv = new float[0];

		private float[] _rowCloud = new float[0];

		private float _glitter;

		private uint _glitterColor;

		private Vec3 _sunDir;

		private Vec3 _moonDir;

		private float _sunDiscCos;

		private float _moonDiscCos;

		private float _moonHaloCos;

		private float _moonScale = 1f;

		private float _sunVis;

		private float _moonVis;

		private float _starsVis;

		private uint _sunCol;

		private uint _zenith;

		private float _cloudOffU;

		private float _cloudOffV;

		private float _cloudSunU;

		private float _cloudSunV;

		private bool _skyNatural;

		private float _moonHaloK;

		private float _moonDark;

		private float _moonPu;

		private float _moonPv;

		private float _moonCosTerm;

		private float _moonInvR;

		private Vec3 _moonRight;

		private Vec3 _moonUp;

		private float _sunHaloGain;

		private float _sunHaloBroad;

		private float _sunDiscWhite;

		private uint _sunHaloCol;

		private const float CloudHeight = 70f;

		private const float CloudTexelsPerTile = 1.05f;

		private static readonly float[] Bayer = new float[16]
		{
			0f, 0.5f, 0.125f, 0.625f, 0.75f, 0.25f, 0.875f, 0.375f, 0.1875f, 0.6875f,
			0.0625f, 0.5625f, 0.9375f, 0.4375f, 0.8125f, 0.3125f
		};

		private const float DayMoon = 0.4f;

		private byte[] _snow;

		private static readonly uint SnowLying = TerrainCache.Rgb(238, 243, 250);

		private static readonly uint SnowCold = TerrainCache.Rgb(214, 226, 242);

		private static readonly uint SnowDust = TerrainCache.Rgb(226, 234, 244);

		public const int SnowPeriod = 512;

		private static readonly Lazy<byte[]> SnowThresholds = new Lazy<byte[]>(delegate
		{
			byte[] array = new byte[262144];
			for (int i = 0; i < 512; i++)
			{
				for (int j = 0; j < 512; j++)
				{
					array[i * 512 + j] = (byte)Math.Max(0, Math.Min(255, (int)(SnowThreshold(j, i) * 255f)));
				}
			}
			return array;
		}, LazyThreadSafetyMode.ExecutionAndPublication);

		private static int _snowWarming;

		private byte[] _snowTable;

		public readonly List<Streak> Streaks = new List<Streak>();

		public static float StreakMaxPixels = 2.2f;

		public bool NaturalSun;

		public bool LensFlare;

		public bool ForBackdrop;

		public float ShadowDarkness = 1f;

		private bool _natural;

		private bool _landField;

		private float _sunFloor;

		private Tint3 _landTint;

		private bool _glintMap;

		private float _fDirR;

		private float _fDirG;

		private float _fDirB;

		private bool _nOn;

		private int _nSize;

		private float _nCx;

		private float _nCy;

		private float _nReach;

		private float _nScale;

		private float _nUp;

		private float _nScaleB;

		private float _nRise;

		private float _nInvReach;

		private float _nInvUp;

		private float _nInvScale;

		private float _nInvScaleB;

		private float _nFaceOff;

		private float _nFaceFront;

		private Vec3 _nL;

		private Vec3 _nA;

		private Vec3 _nB;

		private Vec3 _nC;

		private long _nKey;

		private TexelAnchor _nAnchor;

		private float _nUpHeld;

		private int _nOu;

		private int _nOv;

		private bool _nAllGathered;

		private float _nHold;

		private float _nSunX;

		private float _nSunY;

		private float _nCot;

		private float _nLow;

		private const float MapFade = 0.12f;

		private const float StandsTallest = 12f;

		private const float AloftAbove = 0.5f;

		private const float UpRung = 8f;

		private const float CoverInner2 = 0.7744f;

		private const float GatherMargin = 4f;

		public float GatheredRange;

		private float _nGather = 1f;

		private static readonly float[] JitterSteps = MakeJitterSteps();

		private const float FaceGraze = 0.05f;

		private const float FaceKLeast = 16f;

		private Lens _lens;

		private bool _tilted;

		private float[] _rowK = new float[0];

		private float[] _rowD = new float[0];

		private float[] _rowG = new float[0];

		private float[] _scrU = new float[0];

		private float _gMin = 1f;

		private int _cw;

		private uint[] _cPix;

		private float[] _cDep;

		private byte[] _cLit;

		private float[] _colS = new float[0];

		private int[] _colTop = new int[0];

		private int[] _colBot = new int[0];

		private uint[] _stripPix = new uint[0];

		private float[] _stripDep = new float[0];

		private byte[] _stripLit = new byte[0];

		private int[] _stripLut = new int[0];

		private float _lutScale;

		private float _lutU;

		private Action<int> _splatRow;

		private uint[] _veilPix = new uint[0];

		private float[] _veilDep = new float[0];

		private byte[] _veilA = new byte[0];

		private float[] _veilIn = new float[0];

		private bool _veilOn;

		public Weather Weather;

		public float PrecipX = float.NaN;

		public float PrecipY;

		public float PrecipCell = 1f;

		private const int CloudCell = 3;

		private const float CloudReach = 240f;

		private float[] _cloudA = new float[0];

		private float[] _cloudT = new float[0];

		private float[] _stormKeep = new float[0];

		private uint[] _cloudC = new uint[0];

		private int _cloudW;

		private int _cloudH;

		private bool _stormsDrawn;

		public float CloudShadowMs;

		public float CloudMs;

		public float FallMs;

		public float CloudCover;

		public float CloudCentreCover;

		private Weather _cloudWeather;

		private Weather _precipWeather;

		private float[] _cloudKeep;

		private bool _precipStorms;

		private float _precipCell;

		private float _precipTime;

		private int _precipEx;

		private int _precipEy;

		private const int PrecipReach = 16;

		private Action<int> _cloudRow;

		private Action<int> _cloudCompositeRow;

		private Action<int> _precipRow;

		private Weather _shadeWeather;

		private float _shadeKx;

		private float _shadeKy;

		private Tint3 _shadeTint;

		private const int ShadeBlock = 3;

		private Action<int> _cloudShadeRow;

		private static readonly uint RainColor = TerrainCache.Rgb(190, 206, 230);

		private static readonly uint AcidColor = TerrainCache.Rgb(120, 255, 90);

		private static readonly uint LavaDrop = TerrainCache.Rgb(255, 150, 40);

		private static readonly uint RageDrop = TerrainCache.Rgb(255, 60, 50);

		private static readonly uint SnowColor = TerrainCache.Rgb(246, 250, 255);

		private static readonly uint AshColor = TerrainCache.Rgb(150, 142, 132);

		private static readonly uint MagicA = TerrainCache.Rgb(255, 150, 240);

		private static readonly uint MagicB = TerrainCache.Rgb(170, 140, 255);

		private static readonly uint SunMote = TerrainCache.Rgb(255, 236, 170);

		private const int MostStreak = 26;

		private static WorkerPool _forPool;

		private static volatile bool _forPoolTried;

		private static readonly object ForPoolGate = new object();

		private static readonly ForJob _forJob = new ForJob();

		private static bool _forFloatsChecked;

		public static bool UseWorkerPool = true;

		private Action<int, int, int> _stripeBody;

		private int _stripeW;

		private Action<int> _runStripe;

		public float Focal => _focal;

		public float Horizon => _horizon;

		public FrameProjection Projection => new FrameProjection
		{
			View = _view,
			Width = Width,
			Height = Height,
			Focal = _focal,
			Horizon = _horizon
		};

		public float FogDistance => MaxDistance * 1.5f;

		public byte[] PortalMask
		{
			get
			{
				if (!_portalPass)
				{
					return null;
				}
				return _portalMask;
			}
		}

		public float CometTonight => _cometBright;

		public bool ShowerTonight => _shower;

		public float AuroraTonight => _auroraVis;

		public Vec3 CometDirection => _cometHead;

		public Vec3 ShowerRadiant => _radiant;

		public int MeteorsInSky => _meteorCount;

		public float HazeFadeStart
		{
			get
			{
				if (!(FadeAt > 0f))
				{
					return FogDistance * 0.72f;
				}
				return Math.Min(Math.Max(FadeAt, FogDistance * 0.72f), FogDistance * 0.96f);
			}
		}

		public float CasterReach => ShadowReach * 1.25f;

		private float GatherReach => _nGather;

		private float GatherFade => GatherReach * 0.12f;

		public Lens FrameLens => _lens;

		public int LandColumns => _cw;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsActorTag(int tag)
		{
			if (ActorClarity && tag >= 0)
			{
				return tag < ActorTagEnd;
			}
			return false;
		}

		private void BeginActors()
		{
			int num = Width * Height;
			if (_actorMask.Length != num)
			{
				_actorMask = new byte[num];
			}
			else
			{
				Array.Clear(_actorMask, 0, num);
			}
		}

		private void EndActors()
		{
			ActorTagEnd = 0;
			FarActors.Clear();
		}

		private void DrawFarActors()
		{
			FarActorsDrawn = 0;
			if (!ActorClarity || Air.Indoors)
			{
				return;
			}
			Tint3 flatTint = _flatTint;
			for (int i = 0; i < FarActors.Count; i++)
			{
				FarActor farActor = FarActors[i];
				if (!Project(farActor.X, farActor.Y, farActor.Z, out var col, out var row, out var depth, out var scale) || depth > FogDistance)
				{
					continue;
				}
				int num = Math.Max(1, (int)(farActor.Width * 0.6f * scale + 0.5f));
				int num2 = Math.Max(2, (int)(farActor.Height * scale + 0.5f));
				int num3 = (int)(col - (float)num * 0.5f);
				int num4 = (int)row;
				float num5 = Haze(depth, farActor.Z + farActor.Height * 0.5f) * ActorHaze;
				if (num5 > 0.97f)
				{
					continue;
				}
				Tint3 tint = flatTint;
				byte b = 1;
				if (_natural)
				{
					float vis = ThingVisBeside(farActor.X, farActor.Y, farActor.Z + farActor.Height * 0.5f, farActor.Width * 0.5f);
					b = Share(vis);
					if (_landField)
					{
						tint = FlatLight(Lift(vis));
					}
				}
				uint num6 = TerrainCache.Tint(farActor.Color | 0xFF000000u, tint.R, tint.G, tint.B);
				uint num7 = TerrainCache.Lerp(num6, uint.MaxValue, 0.12f);
				FarActorsDrawn++;
				for (int j = num4 - num2; j < num4; j++)
				{
					if (j < 0 || j >= Height)
					{
						continue;
					}
					uint a = ((j == num4 - num2) ? num7 : num6);
					for (int k = num3; k < num3 + num; k++)
					{
						if (k >= 0 && k < Width)
						{
							int num8 = (Height - 1 - j) * Width + k;
							if (!(depth >= Depth[num8]))
							{
								Pixels[num8] = TerrainCache.Lerp(a, _scrFog[k], num5);
								Depth[num8] = depth;
								_sunlit[num8] = b;
								_actorMask[num8] = 1;
							}
						}
					}
				}
			}
		}

		private void ApplyActorEdges()
		{
			if (ActorClarity && !Air.Indoors)
			{
				float aeSpan = 1f / Math.Max(1f, ActorEdgeFar - ActorEdgeNear);
				_aeSpan = aeSpan;
				For(Height, ActorEdgeRow);
			}
		}

		private void ActorEdgeRow(int y)
		{
			int width = Width;
			int height = Height;
			float actorEdgeNear = ActorEdgeNear;
			float aeSpan = _aeSpan;
			float actorEdgeStrength = ActorEdgeStrength;
			byte[] actorMask = _actorMask;
			float[] depth = Depth;
			uint[] pixels = Pixels;
			float z = _view.Z;
			int num = y * width;
			for (int i = 0; i < width; i++)
			{
				int num2 = num + i;
				if (actorMask[num2] != 0)
				{
					continue;
				}
				float num3 = depth[num2] - 0.5f;
				int num4 = -1;
				if (i > 0 && actorMask[num2 - 1] != 0 && depth[num2 - 1] < num3)
				{
					num3 = depth[num2 - 1];
					num4 = num2 - 1;
				}
				if (i < width - 1 && actorMask[num2 + 1] != 0 && depth[num2 + 1] < num3)
				{
					num3 = depth[num2 + 1];
					num4 = num2 + 1;
				}
				if (y > 0 && actorMask[num2 - width] != 0 && depth[num2 - width] < num3)
				{
					num3 = depth[num2 - width];
					num4 = num2 - width;
				}
				if (y < height - 1 && actorMask[num2 + width] != 0 && depth[num2 + width] < num3)
				{
					num3 = depth[num2 + width];
					num4 = num2 + width;
				}
				if (num4 < 0)
				{
					continue;
				}
				float num5 = (num3 - actorEdgeNear) * aeSpan;
				if (!(num5 <= 0f))
				{
					if (num5 > 1f)
					{
						num5 = 1f;
					}
					num5 *= actorEdgeStrength * (1f - Haze(num3, z) * ActorHaze);
					if (!(num5 <= 0.01f))
					{
						uint num6 = pixels[num4];
						int num7 = (int)(num6 & 0xFF);
						int num8 = (int)((num6 >> 8) & 0xFF);
						int num9 = (int)((num6 >> 16) & 0xFF);
						uint b = (uint)((num7 * 3 / 10 + 10) | (num8 * 26 / 100 + 6 << 8) | (num9 * 4 / 10 + 22 << 16) | -16777216);
						pixels[num2] = TerrainCache.Lerp(pixels[num2], b, num5);
					}
				}
			}
		}

		private List<MeshInstance> SplitClouds()
		{
			_vaporMeshes.Clear();
			_vaporLayer = false;
			bool flag = false;
			for (int i = 0; i < Meshes.Count; i++)
			{
				if (Meshes[i].Cloud != null)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return Meshes;
			}
			_solidMeshes.Clear();
			foreach (MeshInstance mesh in Meshes)
			{
				((mesh.Cloud != null) ? _vaporMeshes : _solidMeshes).Add(mesh);
			}
			return _solidMeshes;
		}

		private void RenderCloudMeshes(int stripes, int stripeW)
		{
			_vaporLayer = false;
			CloudPixels = 0;
			CloudModelMs = 0f;
			if (_vaporMeshes.Count == 0)
			{
				return;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = Pixels.Length;
			if (_vaporA.Length != num)
			{
				_vaporA = new float[num];
				_vaporDepth = new float[num];
				_vaporNear = new float[num];
				_vaporFar = new float[num];
				_vaporDens = new float[num];
				_vaporShare = new float[num];
				_vaporGlow = new float[num];
				_vaporColor = new uint[num];
				_vaporSeen = new byte[num];
				_vaporNear2 = new float[num];
				_vaporSeam = new float[num];
				_vaporColor2 = new uint[num];
			}
			else if (_vaporRowHi >= _vaporRowLo)
			{
				int num2 = (Height - 1 - _vaporRowHi) * Width;
				int num3 = (_vaporRowHi - _vaporRowLo + 1) * Width;
				if (num2 >= 0 && num2 + num3 <= num)
				{
					Array.Clear(_vaporA, num2, num3);
					Array.Clear(_vaporSeen, num2, num3);
				}
				else
				{
					Array.Clear(_vaporA, 0, num);
					Array.Clear(_vaporSeen, 0, num);
				}
			}
			if (_vaporRows.Length < stripes * 2)
			{
				_vaporRows = new int[stripes * 2];
			}
			for (int i = 0; i < stripes; i++)
			{
				_vaporRows[i * 2] = Height;
				_vaporRows[i * 2 + 1] = -1;
			}
			_pass = _vaporMeshes;
			_passDepth = Depth;
			_vaporPass = true;
			int meshTriangles = MeshTriangles;
			float meshMs = MeshMs;
			float meshPrepMs = MeshPrepMs;
			float meshRasterMs = MeshRasterMs;
			float meshBinMs = MeshBinMs;
			RenderPass(stripes, stripeW);
			CloudPrepMs = MeshPrepMs;
			CloudRasterMs = MeshRasterMs;
			MeshTriangles = meshTriangles;
			MeshMs = meshMs;
			MeshPrepMs = meshPrepMs;
			MeshRasterMs = meshRasterMs;
			MeshBinMs = meshBinMs;
			_pass = Meshes;
			_vaporPass = false;
			_vaporLayer = true;
			_vaporRowLo = Height;
			_vaporRowHi = -1;
			for (int j = 0; j < stripes; j++)
			{
				_vaporRowLo = Math.Min(_vaporRowLo, _vaporRows[j * 2]);
				_vaporRowHi = Math.Max(_vaporRowHi, _vaporRows[j * 2 + 1]);
			}
			float[] vaporA = _vaporA;
			int num4 = 0;
			if (_vaporRowHi >= _vaporRowLo)
			{
				int k = (Height - 1 - _vaporRowHi) * Width;
				for (int num5 = (Height - _vaporRowLo) * Width; k < num5; k++)
				{
					if (vaporA[k] > 0.004f)
					{
						num4++;
					}
				}
			}
			CloudPixels = num4;
			CloudModelMs = (float)stopwatch.Elapsed.TotalMilliseconds;
		}

		private void CloudVertex(ref MVert mv, CloudLook c, float nx, float ny, float nz, float wx, float wy, float wz, float ox, float oy, float oz)
		{
			float num = _view.X - wx;
			float num2 = _view.Y - wy;
			float num3 = _view.Z - wz;
			float num4 = (float)Math.Sqrt(num * num + num2 * num2 + num3 * num3);
			float num5 = ((num4 > 1E-05f) ? ((nx * num + ny * num2 + nz * num3) / num4) : 1f);
			if (num5 < 0f)
			{
				num5 = 0f;
			}
			float y = 0f;
			if (c.Glow > 0f)
			{
				float num6 = wx - c.GlowAt.X;
				float num7 = wy - c.GlowAt.Y;
				float num8 = wz - c.GlowAt.Z;
				float num9 = (num6 * num6 + num7 * num7 + num8 * num8) / Math.Max(1E-06f, c.GlowRadius * c.GlowRadius);
				y = ((num9 < 8f) ? (c.Glow * ExpNeg(num9)) : 0f);
			}
			float z = 1f;
			if (oz > c.FadeFrom)
			{
				float num10 = (oz - c.FadeFrom) / Math.Max(0.0001f, c.FadeTo - c.FadeFrom);
				z = ((num10 >= 1f) ? 0f : (1f - num10 * num10 * (3f - 2f * num10)));
			}
			mv.Amb.X = nx;
			mv.Amb.Y = ny;
			mv.Amb.Z = nz;
			mv.Dir.X = num5;
			mv.Dir.Y = y;
			mv.Dir.Z = z;
		}

		private void RasterCloudStripe(int stripe, int x0, int x1)
		{
			int width = Width;
			int height = Height;
			uint[] pixels = Pixels;
			uint[] vaporColor = _vaporColor;
			uint[] vaporColor2 = _vaporColor2;
			float[] depth = Depth;
			float[] vaporNear = _vaporNear;
			float[] vaporFar = _vaporFar;
			float[] vaporDens = _vaporDens;
			float[] vaporShare = _vaporShare;
			float[] vaporGlow = _vaporGlow;
			float[] vaporNear2 = _vaporNear2;
			float[] vaporSeam = _vaporSeam;
			byte[] vaporSeen = _vaporSeen;
			int num = height;
			int num2 = -1;
			int num3 = _binStart[stripe];
			int num4 = _binStart[stripe + 1];
			for (int i = num3; i < num4; i++)
			{
				ref STri reference = ref _st[_binItems[i]];
				int num5 = Math.Max(x0, reference.MinX);
				int num6 = Math.Min(x1 - 1, reference.MaxX);
				if (num5 > num6 || !Edges(reference.X0, reference.Y0, reference.X1, reference.Y1, reference.X2, reference.Y2, out var a, out var b, out var c, out var a2, out var b2, out var c2))
				{
					continue;
				}
				bool flag = (reference.Flags & 0x80) != 0;
				float w = reference.W0;
				float w2 = reference.W1;
				float w3 = reference.W2;
				for (int j = reference.MinY; j <= reference.MaxY; j++)
				{
					float num7 = (float)j + 0.5f;
					if (!RowSpan(a, b, c, a2, b2, c2, num7, num5, num6, out var xs, out var xe))
					{
						continue;
					}
					if (j < num)
					{
						num = j;
					}
					if (j > num2)
					{
						num2 = j;
					}
					int num8 = (height - 1 - j) * width;
					float num9 = _rowG[j];
					for (int k = xs; k <= xe; k++)
					{
						float num10 = (float)k + 0.5f;
						float num11 = a * num10 + b * num7 + c;
						float num12 = a2 * num10 + b2 * num7 + c2;
						float num13 = 1f - num11 - num12;
						float num14 = num11 * w + num12 * w2 + num13 * w3;
						if (num14 <= 0f)
						{
							continue;
						}
						float num15 = num9 / num14;
						int num16 = num8 + k;
						byte b3 = vaporSeen[num16];
						float num17 = depth[num16];
						if (flag)
						{
							float num18 = ((num15 < num17) ? num15 : num17);
							if ((b3 & 4) == 0 || num18 > vaporFar[num16])
							{
								vaporFar[num16] = num18;
							}
							vaporSeen[num16] = (byte)(b3 | 4);
						}
						else if (num15 >= num17)
						{
							vaporSeen[num16] = (byte)(b3 | 2);
						}
						else if ((b3 & 1) == 0 || num15 < vaporNear[num16])
						{
							vaporNear[num16] = num15;
							vaporSeen[num16] = (byte)(b3 | 1);
						}
					}
				}
			}
			if (num2 < num)
			{
				return;
			}
			_vaporRows[stripe * 2] = num;
			_vaporRows[stripe * 2 + 1] = num2;
			_ = _horizon;
			float focal = _focal;
			float z = _view.Z;
			float time = Time;
			Atmosphere air = Air;
			float x2 = _light.X;
			float y = _light.Y;
			float z2 = _light.Z;
			float ambR = air.AmbR;
			float ambG = air.AmbG;
			float ambB = air.AmbB;
			float lightR = air.LightR;
			float lightG = air.LightG;
			float lightB = air.LightB;
			MeshInstance meshInstance = null;
			float num19 = 0f;
			float num20 = 0f;
			float num21 = 1f;
			float num22 = 1f;
			float num23 = 0f;
			float num24 = 0f;
			float num25 = 0f;
			float num26 = 0f;
			float num27 = 0f;
			float num28 = 0f;
			float num29 = 0f;
			float num30 = 0f;
			float num31 = 1f;
			float num32 = 0f;
			float num33 = 0f;
			float num34 = 0f;
			float num35 = 0f;
			uint b4 = 0u;
			uint num36 = 0u;
			float num37 = 1f;
			float num38 = 0f;
			float num39 = 0f;
			float num40 = 0f;
			float num41 = 1f;
			float num42 = 0f;
			float num43 = 0f;
			float num44 = 0f;
			float num45 = 1f;
			for (int l = num3; l < num4; l++)
			{
				ref STri reference2 = ref _st[_binItems[l]];
				if ((reference2.Flags & 0x80) != 0)
				{
					continue;
				}
				int num46 = Math.Max(x0, reference2.MinX);
				int num47 = Math.Min(x1 - 1, reference2.MaxX);
				if (num46 > num47 || !Edges(reference2.X0, reference2.Y0, reference2.X1, reference2.Y1, reference2.X2, reference2.Y2, out var a3, out var b5, out var c3, out var a4, out var b6, out var c4))
				{
					continue;
				}
				MeshInstance meshInstance2 = _pass[reference2.Inst];
				if (meshInstance2 != meshInstance)
				{
					meshInstance = meshInstance2;
					CloudLook cloud = meshInstance2.Cloud;
					num19 = cloud.Opacity;
					num20 = cloud.Mottle;
					num27 = cloud.Bump;
					num26 = cloud.Wrap;
					num29 = cloud.Scatter;
					num31 = cloud.Ambient;
					num22 = 4f / Math.Max(0.001f, cloud.Grain);
					num23 = cloud.Churn.X * time;
					num24 = cloud.Churn.Y * time;
					num25 = cloud.Churn.Z * time;
					num36 = meshInstance2.Multiply;
					float selfLight = cloud.SelfLight;
					num32 = (float)(cloud.SelfColor & 0xFF) / 255f * selfLight;
					num33 = (float)((cloud.SelfColor >> 8) & 0xFF) / 255f * selfLight;
					num34 = (float)((cloud.SelfColor >> 16) & 0xFF) / 255f * selfLight;
					b4 = cloud.GlowColor | 0xFF000000u;
					Mat3 basis = meshInstance2.Basis;
					float num48 = (float)Math.Sqrt(Vec3.Dot(basis.C0, basis.C0));
					float num49 = (float)Math.Sqrt(Vec3.Dot(basis.C1, basis.C1));
					float num50 = (float)Math.Sqrt(Vec3.Dot(basis.C2, basis.C2));
					float num51 = 1f / Math.Max(1E-06f, num48);
					float num52 = 1f / Math.Max(1E-06f, num49);
					float num53 = 1f / Math.Max(1E-06f, num50);
					num37 = basis.C0.X * num51;
					num38 = basis.C0.Y * num51;
					num39 = basis.C0.Z * num51;
					num40 = basis.C1.X * num52;
					num41 = basis.C1.Y * num52;
					num42 = basis.C1.Z * num52;
					num43 = basis.C2.X * num53;
					num44 = basis.C2.Y * num53;
					num45 = basis.C2.Z * num53;
					num21 = 0.6931f / Math.Max(0.0001f, cloud.HalfDepth * (num48 + num49 + num50) * (1f / 3f));
					num35 = cloud.Grain * (num48 + num49 + num50) * (1f / 3f) * focal;
					num30 = cloud.Seam * (num48 + num49 + num50) * (1f / 3f);
					float num54 = meshInstance2.Position.X - _view.X;
					float num55 = meshInstance2.Position.Y - _view.Y;
					float num56 = meshInstance2.Position.Z - _view.Z;
					float num57 = (float)Math.Sqrt(num54 * num54 + num55 * num55 + num56 * num56);
					float num58 = ((num57 > 0.001f) ? ((num54 * x2 + num55 * y + num56 * z2) / num57) : 0f);
					num28 = ((num58 > 0f) ? (num58 * num58 * 1.4f) : 0f);
				}
				uint c5 = reference2.Color | 0xFF000000u;
				float w4 = reference2.W0;
				float w5 = reference2.W1;
				float w6 = reference2.W2;
				float x3 = reference2.O0.X;
				float y2 = reference2.O0.Y;
				float z3 = reference2.O0.Z;
				float x4 = reference2.O1.X;
				float y3 = reference2.O1.Y;
				float z4 = reference2.O1.Z;
				float x5 = reference2.O2.X;
				float y4 = reference2.O2.Y;
				float z5 = reference2.O2.Z;
				float x6 = reference2.A0.X;
				float y5 = reference2.A0.Y;
				float z6 = reference2.A0.Z;
				float x7 = reference2.A1.X;
				float y6 = reference2.A1.Y;
				float z7 = reference2.A1.Z;
				float x8 = reference2.A2.X;
				float y7 = reference2.A2.Y;
				float z8 = reference2.A2.Z;
				float x9 = reference2.D0.X;
				float x10 = reference2.D1.X;
				float x11 = reference2.D2.X;
				float y8 = reference2.D0.Y;
				float y9 = reference2.D1.Y;
				float y10 = reference2.D2.Y;
				float z9 = reference2.D0.Z;
				float z10 = reference2.D1.Z;
				float z11 = reference2.D2.Z;
				for (int m = reference2.MinY; m <= reference2.MaxY; m++)
				{
					float num59 = (float)m + 0.5f;
					if (!RowSpan(a3, b5, c3, a4, b6, c4, num59, num46, num47, out var xs2, out var xe2))
					{
						continue;
					}
					int num60 = (height - 1 - m) * width;
					for (int n = xs2; n <= xe2; n++)
					{
						float num61 = (float)n + 0.5f;
						float num62 = a3 * num61 + b5 * num59 + c3;
						float num63 = a4 * num61 + b6 * num59 + c4;
						float num64 = 1f - num62 - num63;
						float num65 = num62 * w4 + num63 * w5 + num64 * w6;
						if (num65 <= 0f)
						{
							continue;
						}
						float num66 = 1f / num65;
						float num67 = _rowG[m] / num65;
						int num68 = num60 + n;
						byte b7 = vaporSeen[num68];
						if ((b7 & 1) == 0 || num67 >= depth[num68])
						{
							continue;
						}
						float num69 = vaporNear[num68];
						bool flag2 = num67 <= num69 * 1.00001f;
						if (flag2 || (!(num67 - num69 > num30) && ((b7 & 8) == 0 || !(num67 >= vaporNear2[num68]))))
						{
							float num70 = (num62 * z9 + num63 * z10 + num64 * z11) * num66;
							float num71 = (num62 * x9 + num63 * x10 + num64 * x11) * num66;
							float num72 = (num62 * y8 + num63 * y9 + num64 * y10) * num66;
							float num73 = (num62 * x3 + num63 * x4 + num64 * x5) * num66;
							float num74 = (num62 * y2 + num63 * y3 + num64 * y4) * num66;
							float num75 = (num62 * z3 + num63 * z4 + num64 * z5) * num66;
							float gx;
							float gy;
							float gz;
							float num76 = CloudLook.Lumps((num73 - num23) * num22, (num74 - num24) * num22, (num75 - num25) * num22, out gx, out gy, out gz);
							float num77 = num35 / num66;
							float num78 = ((num77 < 4f) ? 0f : ((num77 > 12f) ? 1f : ((num77 - 4f) * 0.125f)));
							if (num77 > 11f)
							{
								float gx2;
								float gy2;
								float gz2;
								float num79 = CloudLook.Lumps(num73 * num22 * 2.7f + 11.3f, num74 * num22 * 2.7f + 5.1f, (num75 - num25 * 1.6f) * num22 * 2.7f, out gx2, out gy2, out gz2);
								float num80 = ((num77 > 24f) ? 0.25f : ((num77 - 11f) * (1f / 52f)));
								num76 += (num79 - num76) * num80;
								gx += gx2 * num80 * 1.2f - gx * num80;
								gy += gy2 * num80 * 1.2f - gy * num80;
								gz += gz2 * num80 * 1.2f - gz * num80;
							}
							gx *= num78;
							gy *= num78;
							gz *= num78;
							float num81 = (num62 * x6 + num63 * x7 + num64 * x8) * num66;
							float num82 = (num62 * y5 + num63 * y6 + num64 * y7) * num66;
							float num83 = (num62 * z6 + num63 * z7 + num64 * z8) * num66;
							float num84 = num37 * gx + num40 * gy + num43 * gz;
							float num85 = num38 * gx + num41 * gy + num44 * gz;
							float num86 = num39 * gx + num42 * gy + num45 * gz;
							float num87 = 1f / (float)Math.Sqrt(num81 * num81 + num82 * num82 + num83 * num83 + 1E-12f);
							num81 *= num87;
							num82 *= num87;
							num83 *= num87;
							float num88 = num84 * num81 + num85 * num82 + num86 * num83;
							num81 -= (num84 - num88 * num81) * num27;
							num82 -= (num85 - num88 * num82) * num27;
							num83 -= (num86 - num88 * num83) * num27;
							num87 = 1f / (float)Math.Sqrt(num81 * num81 + num82 * num82 + num83 * num83 + 1E-12f);
							num81 *= num87;
							num82 *= num87;
							num83 *= num87;
							float num89 = (num81 * x2 + num82 * y + num83 * z2 + num26) / (1f + num26);
							if (num89 < num29)
							{
								num89 = num29 + (num89 - num29) * 0.25f;
							}
							if (num89 < 0f)
							{
								num89 = 0f;
							}
							float num90 = 1f - num71;
							num89 += num28 * num90 * num90;
							float num91 = (0.84f + 0.16f * num83) * num31;
							float num92 = (0.9f + 0.2f * num76 * num78) * 256f;
							float num93 = Shoulder(ambR * num91 + lightR * num89 + num32) * num92;
							float num94 = Shoulder(ambG * num91 + lightG * num89 + num33) * num92;
							float num95 = Shoulder(ambB * num91 + lightB * num89 + num34) * num92;
							uint num96 = TerrainCache.Tint(c5, (int)num93, (int)num94, (int)num95);
							if (num72 > 0.01f)
							{
								num96 = TerrainCache.Lerp(num96, b4, (num72 > 1.2f) ? 0.9f : (num72 * 0.75f));
							}
							if (num36 != 0)
							{
								num96 = TerrainCache.Multiply(num96, num36);
							}
							float num97 = Haze(num67, z + _rowK[m] * num67);
							if (num97 > 0.002f)
							{
								num96 = TerrainCache.Lerp(num96, _scrFog[n], num97);
							}
							if (!flag2)
							{
								vaporNear2[num68] = num67;
								vaporColor2[num68] = num96;
								vaporSeen[num68] = (byte)(b7 | 8);
							}
							else
							{
								vaporSeam[num68] = num30;
								vaporColor[num68] = num96;
								vaporDens[num68] = num21 * (1f + num20 * (2f * num76 - 1f));
								vaporShare[num68] = num19 * num70;
								vaporGlow[num68] = num72;
							}
						}
					}
				}
			}
			MeshInstance meshInstance3 = ((_pass.Count > 0) ? _pass[0] : null);
			uint num98 = 0u;
			float num99 = 1f;
			float num100 = 0f;
			if (meshInstance3 != null && meshInstance3.Cloud != null && meshInstance3.Model.SlotColor.Length != 0)
			{
				CloudLook cloud2 = meshInstance3.Cloud;
				float selfLight2 = cloud2.SelfLight;
				float num101 = (ambR + lightR * 0.55f) * 0.5f + (ambG + lightG * 0.55f) * 0.5f;
				num98 = TerrainCache.Scale(meshInstance3.Model.SlotColor[0] | 0xFF000000u, Math.Min(1f, num101 + selfLight2));
				Mat3 basis2 = meshInstance3.Basis;
				num99 = 0.6931f / Math.Max(0.0001f, cloud2.HalfDepth * (float)Math.Sqrt(Vec3.Dot(basis2.C2, basis2.C2)));
				num100 = cloud2.Opacity;
			}
			float[] vaporA = _vaporA;
			float[] vaporDepth = _vaporDepth;
			for (int num102 = num; num102 <= num2; num102++)
			{
				int num103 = (height - 1 - num102) * width;
				for (int num104 = x0; num104 < x1; num104++)
				{
					int num105 = num103 + num104;
					byte b8 = vaporSeen[num105];
					if (b8 == 0)
					{
						continue;
					}
					float num106;
					float num107;
					float num108;
					float num109;
					uint b9;
					if ((b8 & 1) != 0)
					{
						num106 = vaporNear[num105];
						num107 = vaporDens[num105];
						num108 = vaporShare[num105];
						num109 = vaporGlow[num105];
						b9 = vaporColor[num105];
						if ((b8 & 8) != 0)
						{
							float num110 = (vaporNear2[num105] - num106) / Math.Max(0.0001f, vaporSeam[num105]);
							if (num110 < 1f)
							{
								b9 = TerrainCache.Lerp(vaporColor2[num105], b9, 0.5f + 0.5f * num110);
							}
						}
					}
					else
					{
						if ((b8 & 2) != 0 || (b8 & 4) == 0 || num98 == 0)
						{
							continue;
						}
						num106 = 0.04f;
						num107 = num99;
						num108 = num100;
						num109 = 0f;
						b9 = num98;
					}
					float num111 = (((b8 & 4) != 0) ? (vaporFar[num105] - num106) : (0.7f / Math.Max(0.0001f, num107)));
					if (num111 <= 0f)
					{
						continue;
					}
					float num112 = (1f - ExpNeg(num111 * num107)) * num108 + num109 * 0.4f;
					if (!(num112 <= 0.004f))
					{
						if (num112 > 0.97f)
						{
							num112 = 0.97f;
						}
						pixels[num105] = TerrainCache.Lerp(pixels[num105], b9, num112);
						vaporA[num105] = num112;
						vaporDepth[num105] = num106;
					}
				}
			}
		}

		private static float Shoulder(float v)
		{
			if (!(v > 0.85f))
			{
				return v;
			}
			return 0.85f + (v - 0.85f) * 0.45f;
		}

		private float VaporPass(int idx, float depth)
		{
			float num = _vaporA[idx];
			if (!(num > 0f) || !(depth > _vaporDepth[idx]))
			{
				return 1f;
			}
			return 1f - num;
		}

		public void Resize(int w, int h)
		{
			if (w != Width || h != Height)
			{
				Width = w;
				Height = h;
				Pixels = new uint[w * h];
				Depth = new float[w * h];
				_fogCol = new uint[w];
				_sunSide = new float[w];
				_invLen = new float[w];
			}
		}

		public bool Project(float x, float y, float z, out float col, out float row, out float depth)
		{
			float scale;
			return ProjectRel(x - _view.X, y - _view.Y, z - _view.Z, 0.05f, out col, out row, out depth, out scale);
		}

		public bool Project(float x, float y, float z, out float col, out float row, out float depth, out float scale)
		{
			return ProjectRel(x - _view.X, y - _view.Y, z - _view.Z, 0.05f, out col, out row, out depth, out scale);
		}

		public void Render(TerrainCache terrain, ViewState view, List<Billboard> billboards, float maxTerrainHeight)
		{
			Render(terrain, view, billboards, null, maxTerrainHeight);
		}

		public void Render(TerrainCache terrain, ViewState view, List<Billboard> billboards, List<VoxelObject> voxels, float maxTerrainHeight)
		{
			_terrain = terrain;
			BeginSnow(terrain);
			view.Free = false;
			_view = view;
			_maxTerrain = maxTerrainHeight;
			_lodK = ((DetailZoom > 1f) ? (1f / DetailZoom) : 1f);
			_focal = (float)Width * 0.5f / (float)Math.Tan(view.Fov * 0.5f);
			_lens = Lens.Of(in view, (float)Height / (float)Math.Max(1, Width));
			_tilted = _lens.Tilted;
			_horizon = (float)Height * 0.5f + _lens.Shift * _focal;
			_fx = (float)Math.Cos(view.Yaw);
			_fy = (float)Math.Sin(view.Yaw);
			_rx = _fy;
			_ry = 0f - _fx;
			PrepareLight();
			PrepareSun();
			EnsureSunlit();
			PrepareCanvas();
			PrepareVeil();
			PrepareSky();
			PrepareSea(terrain, view.X, view.Y);
			BeginActors();
			Stopwatch stopwatch = Stopwatch.StartNew();
			int cw = _cw;
			int n = (cw + 16 - 1) / 16;
			if (UseParallel)
			{
				For(n, RenderColumns);
			}
			else
			{
				for (int i = 0; i < cw; i++)
				{
					RenderColumn(i);
				}
			}
			if (_tilted)
			{
				ApplyVeil(_cPix, _cDep, cw);
				SplatStrips();
			}
			TerrainMs = (float)stopwatch.Elapsed.TotalMilliseconds;
			stopwatch.Restart();
			PickTag = -1;
			PickDepth = float.MaxValue;
			_billboards = billboards ?? _noBillboards;
			FadeNearParticles(_billboards);
			_voxels = voxels ?? _noVoxels;
			_voxels.Sort((VoxelObject a, VoxelObject b) => a.SortDepth.CompareTo(b.SortDepth));
			bool flag = false;
			foreach (VoxelObject voxel in _voxels)
			{
				flag |= voxel.Overlay;
			}
			PrepareVoxels();
			PrepMs = (float)stopwatch.Elapsed.TotalMilliseconds;
			int num = ((!UseParallel) ? 1 : Math.Min(_stripePick.Length, Math.Max(1, Environment.ProcessorCount * StripesPerThread)));
			int num2 = (Width + num - 1) / num;
			Stripes = num;
			if (_billboards.Count > 0 || _voxels.Count > 0)
			{
				if (UseParallel && num > 1)
				{
					ForStripes(num, num2, RenderObjects);
				}
				else
				{
					RenderObjects(0, 0, Width);
				}
				int num3 = Math.Min(num - 1, Width / 2 / num2);
				PickTag = _stripePick[num3];
				PickDepth = _stripePickDepth[num3];
			}
			ParallelMs = (float)stopwatch.Elapsed.TotalMilliseconds - PrepMs;
			ObjectsMs = (float)stopwatch.Elapsed.TotalMilliseconds;
			BlobMs = (VoxelShadowMs = (VoxelMs = (SpriteMs = (StripeSumMs = 0f))));
			for (int num4 = 0; num4 < Math.Min(num, 64); num4++)
			{
				StripeSumMs += _stripeTimes[num4 * 4] + _stripeTimes[num4 * 4 + 1] + _stripeTimes[num4 * 4 + 2] + _stripeTimes[num4 * 4 + 3];
				BlobMs = Math.Max(BlobMs, _stripeTimes[num4 * 4]);
				VoxelShadowMs = Math.Max(VoxelShadowMs, _stripeTimes[num4 * 4 + 1]);
				VoxelMs = Math.Max(VoxelMs, _stripeTimes[num4 * 4 + 2]);
				SpriteMs = Math.Max(SpriteMs, _stripeTimes[num4 * 4 + 3]);
			}
			stopwatch.Restart();
			RenderMeshes(num, num2);
			DrawFarProps();
			DrawFarActors();
			ApplyVeil(Pixels, Depth, Width);
			double totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
			RenderWeatherShadows();
			double num5 = stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds;
			double totalMilliseconds2 = stopwatch.Elapsed.TotalMilliseconds;
			ApplyPointLights();
			LightsMs = (float)(stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds2);
			ApplyActorEdges();
			totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
			RenderWeather();
			num5 += stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds;
			RenderCloudMeshes(num, num2);
			RenderBlended(num, num2);
			RenderStreaks();
			totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
			RenderWeatherFront();
			WeatherMs = (float)(num5 + stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds);
			if (Windows.Count > 0 && Air.Indoors)
			{
				ApplyWindowBeams();
			}
			if (!Air.Indoors)
			{
				ApplyLightShafts();
			}
			double totalMilliseconds3 = stopwatch.Elapsed.TotalMilliseconds;
			if (flag || OverlayMeshes.Count > 0)
			{
				if (_overlayDepth.Length != Depth.Length)
				{
					_overlayDepth = new float[Depth.Length];
					_overlayClear = new float[Depth.Length];
					for (int num6 = 0; num6 < _overlayClear.Length; num6++)
					{
						_overlayClear[num6] = float.MaxValue;
					}
				}
				Array.Copy(_overlayClear, _overlayDepth, _overlayDepth.Length);
				if (flag)
				{
					if (UseParallel && num > 1)
					{
						ForStripes(num, num2, RenderOverlayStripe);
					}
					else
					{
						RenderOverlay(0, Width);
					}
				}
				RenderOverlayMeshes(num, num2);
			}
			double totalMilliseconds4 = stopwatch.Elapsed.TotalMilliseconds;
			OverlayMs = (float)(totalMilliseconds4 - totalMilliseconds3);
			ApplyScreenFlash();
			ApplyFinish();
			FinishMs = (float)(stopwatch.Elapsed.TotalMilliseconds - totalMilliseconds4);
			EndActors();
			PostMs = (float)stopwatch.Elapsed.TotalMilliseconds;
		}

		private void RenderColumns(int k)
		{
			int i = k * 16;
			for (int num = Math.Min(_cw, i + 16); i < num; i++)
			{
				RenderColumn(i);
			}
		}

		private void RenderOverlayStripe(int stripe, int x0, int x1)
		{
			RenderOverlay(x0, x1);
		}

		private void PrepareLight()
		{
			Atmosphere air = Air;
			_light = air.Light;
			_ambR = (int)(air.AmbR * 256f);
			_ambG = (int)(air.AmbG * 256f);
			_ambB = (int)(air.AmbB * 256f);
			_litR = (int)(air.LightR * 256f);
			_litG = (int)(air.LightG * 256f);
			_litB = (int)(air.LightB * 256f);
			float flatLambert = air.FlatLambert;
			_flatTint = new Tint3(air.AmbR + air.LightR * flatLambert * 0.9f, air.AmbG + air.LightG * flatLambert * 0.9f, air.AmbB + air.LightB * flatLambert * 0.9f);
			_waterTint = new Tint3(air.AmbR + air.LightR * flatLambert, air.AmbG + air.LightG * flatLambert, air.AmbB + air.LightB * flatLambert);
			_shadowTint = new Tint3(air.AmbR / Math.Max(0.05f, air.AmbR + air.LightR * flatLambert), air.AmbG / Math.Max(0.05f, air.AmbG + air.LightG * flatLambert), air.AmbB / Math.Max(0.05f, air.AmbB + air.LightB * flatLambert));
			_cloudTint = _shadowTint;
			if (ShadowStrength < 1f)
			{
				float num = Math.Max(0f, ShadowStrength);
				_shadowTint = new Tint3(1f - num * (1f - air.AmbR / Math.Max(0.05f, air.AmbR + air.LightR * flatLambert)), 1f - num * (1f - air.AmbG / Math.Max(0.05f, air.AmbG + air.LightG * flatLambert)), 1f - num * (1f - air.AmbB / Math.Max(0.05f, air.AmbB + air.LightB * flatLambert)));
			}
			_ambLum = Math.Max(0.12f, (air.AmbR + air.LightR * flatLambert) * 0.3f + (air.AmbG + air.LightG * flatLambert) * 0.59f + (air.AmbB + air.LightB * flatLambert) * 0.11f);
			_hazeDensity = air.HazeDensity * HazeScale;
			_fadeStart = HazeFadeStart;
			_fadeInv = 1f / (FogDistance - _fadeStart);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float Haze(float t, float z)
		{
			float num = (_view.Z + z) * 0.5f;
			if (num < 0f)
			{
				num = 0f;
			}
			float num2 = 1f - ExpNeg(_hazeDensity * t * ExpNeg(num * 0.0625f));
			if (t > _fadeStart)
			{
				float num3 = (t - _fadeStart) * _fadeInv;
				if (num3 > num2)
				{
					num2 = ((num3 > 1f) ? 1f : num3);
				}
			}
			return num2;
		}

		private static float[] BuildExpTable()
		{
			float[] array = new float[1026];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (float)Math.Exp((double)(-i) / 128.0);
			}
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float ExpNeg(float x)
		{
			if (x <= 0f)
			{
				return 1f;
			}
			if (x >= 8f)
			{
				return 0f;
			}
			float num = x * 128f;
			int num2 = (int)num;
			float num3 = num - (float)num2;
			return ExpTable[num2] + (ExpTable[num2 + 1] - ExpTable[num2]) * num3;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint LightSurface(uint albedo, int sun, int open)
		{
			int r = (_ambR * open >> 8) + (_litR * sun >> 8);
			int g = (_ambG * open >> 8) + (_litG * sun >> 8);
			int b = (_ambB * open >> 8) + (_litB * sun >> 8);
			return TerrainCache.Tint(albedo, r, g, b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SampleGround(float px, float py, float t, ref GroundSample g)
		{
			SampleHeight(px, py, ref g);
			if (g.Inside)
			{
				SampleLook(px, py, t, ref g);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SampleHeight(float px, float py, ref GroundSample g)
		{
			TerrainCache terrain = _terrain;
			int width = terrain.Width;
			int height = terrain.Height;
			if (px < 0f || py < 0f || px >= (float)width || py >= (float)height)
			{
				g.Inside = false;
				g.H = terrain.OutsideHeight;
				g.Col = terrain.OutsideColor;
				g.Flags = (terrain.OutsideIsWater ? ((byte)1) : ((byte)0));
				g.Mat = 0;
				g.Texel = 0u;
				g.Sun = (int)(255f * Math.Max(0f, _light.Z));
				g.Open = 255;
				g.Coast = 9f;
				return;
			}
			float[] heights = terrain.Heights;
			float[] array = ((terrain.Ground.Length == heights.Length) ? terrain.Ground : heights);
			byte[] flags = terrain.Flags;
			float num = px - 0.5f;
			float num2 = py - 0.5f;
			int num3 = (int)num;
			int num4 = (int)num2;
			if (num < 0f)
			{
				num3 = -1;
			}
			if (num2 < 0f)
			{
				num4 = -1;
			}
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? num3 : 0);
			int num8 = ((num3 + 1 >= width) ? (width - 1) : (num3 + 1));
			int num9 = ((num4 >= 0) ? num4 : 0);
			int num10 = ((num4 + 1 >= height) ? (height - 1) : (num4 + 1));
			int num11 = num9 * width + num7;
			int num12 = num9 * width + num8;
			int num13 = num10 * width + num7;
			int num14 = num10 * width + num8;
			float num15 = array[num11];
			float num16 = array[num12];
			float num17 = array[num13];
			float num18 = array[num14];
			float num19 = num15 + (num16 - num15) * num5;
			float num20 = num17 + (num18 - num17) * num5;
			float num21 = num19 + (num20 - num19) * num6;
			byte b = flags[num11];
			byte b2 = flags[num12];
			byte b3 = flags[num13];
			byte b4 = flags[num14];
			if (((b | b2 | b3 | b4) & 0x40) != 0)
			{
				num21 = TerrainCache.HollowHeight(array, width, height, num3, num4, num5, num6, num21, b, b2, b3, b4);
			}
			int num22 = (int)px;
			int num23 = (int)py * width + num22;
			byte b5 = flags[num23];
			if ((b5 & 8) != 0)
			{
				float num24 = heights[num23];
				if ((((int)(px * 2f) + (int)(py * 2f)) & 1) == 0 && !WallModelled(px, py))
				{
					num24 += 0.45f;
				}
				if (num24 > num21)
				{
					num21 = num24;
				}
			}
			g.Inside = true;
			g.H = num21;
			g.Fl = b5;
			g.Flags = (byte)(b5 & -17);
			g.Ti = num23;
			g.I00 = num11;
			g.I10 = num12;
			g.I01 = num13;
			g.I11 = num14;
			g.Tx = num5;
			g.Ty = num6;
			g.Coast = 9f;
			if (TerrainCache.IsCoastCell(b, b2, b3, b4))
			{
				CoastSample(px, py, ref g, array, flags);
			}
		}

		private static void CoastSample(float px, float py, ref GroundSample g, float[] ground, byte[] flags)
		{
			byte f = flags[g.I00];
			byte f2 = flags[g.I10];
			byte f3 = flags[g.I01];
			byte f4 = flags[g.I11];
			g.H = TerrainCache.ShoreHeight(g.H, f, f2, f3, f4, ground[g.I00], ground[g.I10], ground[g.I01], ground[g.I11], g.Tx, g.Ty, px, py, out var side);
			g.Coast = side;
			bool flag = side >= 0f;
			if ((g.Fl & 3) != 0 != flag)
			{
				g.Fl = flags[g.Ti = TerrainCache.ShoreTile(flag, f, f2, f3, f4, g.I00, g.I10, g.I01, g.I11, g.Tx, g.Ty)];
				g.Flags = (byte)(g.Fl & -17);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool WallModelled(float px, float py)
		{
			if (WallModelRange <= 0f)
			{
				return false;
			}
			float num = (float)(int)px + 0.5f - _view.X;
			float num2 = (float)(int)py + 0.5f - _view.Y;
			return num * num + num2 * num2 < WallModelRange * WallModelRange;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SampleLook(float px, float py, float t, ref GroundSample g)
		{
			TerrainCache terrain = _terrain;
			int width = terrain.Width;
			int height = terrain.Height;
			byte[] flags = terrain.Flags;
			int ti = g.Ti;
			byte fl = g.Fl;
			uint num = TerrainModel.Hash((int)(px * 8f), (int)(py * 8f));
			int num2 = ti;
			if (t * _lodK < 40f && terrain.DitherEdges)
			{
				float num3 = ((float)((num >> 12) & 7) - 3.5f) * 0.07f;
				float num4 = ((float)((num >> 15) & 7) - 3.5f) * 0.07f;
				int num5 = (int)(px + num3);
				int num6 = (int)(py + num4);
				if (num5 >= 0 && num6 >= 0 && num5 < width && num6 < height)
				{
					int num7 = num6 * width + num5;
					if (((flags[num7] ^ fl) & 0xB) == 0)
					{
						num2 = num7;
					}
				}
			}
			g.Col = (((fl & 1) != 0) ? WaterColor(terrain.Colors, flags, ref g) : terrain.Colors[num2]);
			g.Mat = terrain.Materials[num2];
			g.Texel = num;
			g.Flags = (byte)((fl & -17) | (flags[num2] & 0x10));
			if (_snow != null)
			{
				SnowGround(px, py, t, ref g);
			}
			if (g.Coast < 0f && g.Coast > -0.3f)
			{
				WetGround(ref g, px, py);
			}
			int[] pathSlot = terrain.PathSlot;
			if (ti < pathSlot.Length && pathSlot[ti] != 0 && (fl & 0xB) == 0 && ti == (int)py * width + (int)px)
			{
				int num8 = terrain.PathTexels[(pathSlot[ti] - 1) * 64 + ((int)(py * 8f) & 7) * 8 + ((int)(px * 8f) & 7)];
				if (num8 > 0)
				{
					WornGround(ref g, num8, num);
				}
			}
			if ((fl & 8) != 0)
			{
				g.Sun = terrain.Sun[ti];
				g.Open = terrain.Open[ti];
				return;
			}
			byte[] sun = terrain.Sun;
			byte[] open = terrain.Open;
			int i = g.I00;
			int i2 = g.I10;
			int i3 = g.I01;
			int i4 = g.I11;
			float tx = g.Tx;
			float ty = g.Ty;
			float num9 = (float)(int)sun[i] + (float)(sun[i2] - sun[i]) * tx;
			float num10 = (float)(int)sun[i3] + (float)(sun[i4] - sun[i3]) * tx;
			float num11 = (float)(int)open[i] + (float)(open[i2] - open[i]) * tx;
			float num12 = (float)(int)open[i3] + (float)(open[i4] - open[i3]) * tx;
			g.Sun = (int)(num9 + (num10 - num9) * ty);
			g.Open = (int)(num11 + (num12 - num11) * ty);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint WaterColor(uint[] colors, byte[] flags, ref GroundSample g)
		{
			uint num = colors[g.I00];
			uint num2 = colors[g.I10];
			uint num3 = colors[g.I01];
			uint num4 = colors[g.I11];
			if (num == num2 && num == num3 && num == num4)
			{
				return num;
			}
			float tx = g.Tx;
			float ty = g.Ty;
			float num5 = (((flags[g.I00] & 1) != 0) ? ((1f - tx) * (1f - ty)) : 0f);
			float num6 = (((flags[g.I10] & 1) != 0) ? (tx * (1f - ty)) : 0f);
			float num7 = (((flags[g.I01] & 1) != 0) ? ((1f - tx) * ty) : 0f);
			float num8 = (((flags[g.I11] & 1) != 0) ? (tx * ty) : 0f);
			float num9 = num5 + num6 + num7 + num8;
			if (num9 < 1E-06f)
			{
				return colors[g.Ti];
			}
			float num10 = 1f / num9;
			float num11 = ((float)(num & 0xFF) * num5 + (float)(num2 & 0xFF) * num6 + (float)(num3 & 0xFF) * num7 + (float)(num4 & 0xFF) * num8) * num10;
			float num12 = ((float)((num >> 8) & 0xFF) * num5 + (float)((num2 >> 8) & 0xFF) * num6 + (float)((num3 >> 8) & 0xFF) * num7 + (float)((num4 >> 8) & 0xFF) * num8) * num10;
			float num13 = ((float)((num >> 16) & 0xFF) * num5 + (float)((num2 >> 16) & 0xFF) * num6 + (float)((num3 >> 16) & 0xFF) * num7 + (float)((num4 >> 16) & 0xFF) * num8) * num10;
			return TerrainCache.Rgb((int)(num11 + 0.5f), (int)(num12 + 0.5f), (int)(num13 + 0.5f));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float Swash(float px, float py)
		{
			return 0.05f + 0.07f * (0.5f + 0.5f * FastSin(Time * 1.3f + px * 0.9f + py * 0.7f));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void WetGround(ref GroundSample g, float px, float py)
		{
			if (g.Mat != 6 && g.Mat != 8)
			{
				float num = 0f - g.Coast - Swash(px, py);
				float num2 = ((num <= 0f) ? 1f : (1f - num / 0.12f - (float)((g.Texel >> 24) & 0xFF) / 255f * 0.5f));
				if (num2 > 0f)
				{
					g.Col = TerrainCache.Scale(g.Col, 1f - 0.24f * Math.Min(1f, num2));
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WornGround(ref GroundSample g, int wear, uint texel)
		{
			byte mat = g.Mat;
			if (mat == 6 || mat == 7 || mat == 4)
			{
				return;
			}
			int num = (int)(60 + ((texel >> 21) & 0x7F));
			if (wear >= num)
			{
				uint col = g.Col;
				uint pathEarth = PathEarth;
				float t;
				switch (mat)
				{
				default:
					t = 0.74f;
					break;
				case 5:
					t = 0.45f;
					break;
				case 2:
					t = 0.32f;
					break;
				}
				g.Col = TerrainCache.Lerp(col, pathEarth, t);
				g.Mat = 9;
			}
			else
			{
				g.Col = TerrainCache.Lerp(g.Col, PathEarth, 0.22f * (float)wear / (float)num);
				if (wear * 2 > num && mat == 1)
				{
					g.Mat = 3;
				}
			}
		}

		private void RenderColumn(int c)
		{
			TerrainCache terrain = _terrain;
			float num = _colS[c];
			float num2 = _fx + _rx * num;
			float num3 = _fy + _ry * num;
			float z = _view.Z;
			float focal = _focal;
			float horizon = _horizon;
			float fogDistance = FogDistance;
			bool tilted = _tilted;
			_ = _lens;
			float[] rowK = _rowK;
			float lodK = _lodK;
			float num4 = 0.011f * lodK;
			float num5 = _maxTerrain + 0.05f;
			float waveRange = _waveRange;
			float waveLift = _waveLift;
			byte[] open = ((terrain.SeaOpen.Length == terrain.Flags.Length) ? terrain.SeaOpen : null);
			if (waveLift > 0f)
			{
				num5 = Math.Max(num5, 0f + waveLift + 0.05f);
			}
			bool flag = z < num5;
			int num6 = _colBot[c];
			int num7 = _colTop[c];
			int cw = _cw;
			int height = Height;
			float num8 = 0.06f;
			float[] array = ((terrain.BlockMax.Length != 0) ? terrain.BlockMax : null);
			int blocksX = terrain.BlocksX;
			int blocksY = terrain.BlocksY;
			uint fogCol = _fogCol[c];
			GroundSample g = default(GroundSample);
			GroundSample g2 = default(GroundSample);
			float num9 = 0f;
			float num10 = 0f;
			bool flag2 = false;
			float x = _view.X;
			float y = _view.Y;
			int width = terrain.Width;
			int height2 = terrain.Height;
			float[] heights = terrain.Heights;
			float[] array2 = ((terrain.Ground.Length == heights.Length) ? terrain.Ground : heights);
			byte[] flags = terrain.Flags;
			uint[] cPix = _cPix;
			float[] cDep = _cDep;
			byte[] cLit = _cLit;
			bool natural = _natural;
			float num11 = 1f / focal;
			float num12 = (tilted ? SlopeAtRow((float)num6 - 0.75f) : ((horizon - (float)num6 + 0.75f) * num11));
			float num13 = (tilted ? SlopeAtRow((float)num6 - 0.02f) : ((horizon - (float)num6 + 0.02f) * num11));
			SightCut cut = Cut;
			float num14 = (cut.On ? cut.Target : 0f);
			int yVeil = num6;
			float prevT = 0f;
			float prevH = 0f;
			bool prevOk = false;
			while (num8 < fogDistance)
			{
				float num15 = x + num2 * num8;
				float num16 = y + num3 * num8;
				bool flag3 = !(num15 < 0f) && !(num16 < 0f) && !(num15 >= (float)width) && !(num16 >= (float)height2);
				float num37;
				if (flag3)
				{
					float num17 = num15 - 0.5f;
					float num18 = num16 - 0.5f;
					int num19 = (int)num17;
					int num20 = (int)num18;
					if (num17 < 0f)
					{
						num19 = -1;
					}
					if (num18 < 0f)
					{
						num20 = -1;
					}
					float num21 = num17 - (float)num19;
					float num22 = num18 - (float)num20;
					int num23 = ((num19 >= 0) ? num19 : 0);
					int num24 = ((num19 + 1 >= width) ? (width - 1) : (num19 + 1));
					int num25 = ((num20 >= 0) ? num20 : 0);
					int num26 = ((num20 + 1 >= height2) ? (height2 - 1) : (num20 + 1));
					int num27 = num25 * width + num23;
					int num28 = num25 * width + num24;
					int num29 = num26 * width + num23;
					int num30 = num26 * width + num24;
					float num31 = array2[num27];
					float num32 = array2[num28];
					float num33 = array2[num29];
					float num34 = array2[num30];
					float num35 = num31 + (num32 - num31) * num21;
					float num36 = num33 + (num34 - num33) * num21;
					num37 = num35 + (num36 - num35) * num22;
					byte b = flags[num27];
					byte b2 = flags[num28];
					byte b3 = flags[num29];
					byte b4 = flags[num30];
					if (((b | b2 | b3 | b4) & 0x40) != 0)
					{
						num37 = TerrainCache.HollowHeight(array2, width, height2, num19, num20, num21, num22, num37, b, b2, b3, b4);
					}
					int num38 = (int)num16 * width + (int)num15;
					byte b5 = flags[num38];
					if ((b5 & 8) != 0)
					{
						float num39 = heights[num38];
						if ((((int)(num15 * 2f) + (int)(num16 * 2f)) & 1) == 0 && !WallModelled(num15, num16))
						{
							num39 += 0.45f;
						}
						if (num39 > num37)
						{
							num37 = num39;
						}
					}
					g.Inside = true;
					g.H = num37;
					g.Fl = b5;
					g.Flags = (byte)(b5 & -17);
					g.Ti = num38;
					g.I00 = num27;
					g.I10 = num28;
					g.I01 = num29;
					g.I11 = num30;
					g.Tx = num21;
					g.Ty = num22;
					g.Coast = 9f;
					if (TerrainCache.IsCoastCell(b, b2, b3, b4))
					{
						CoastSample(num15, num16, ref g, array2, flags);
						num37 = g.H;
					}
				}
				else
				{
					SampleHeight(num15, num16, ref g);
					num37 = g.H;
				}
				if (flag3 && num8 * lodK < waveRange && (g.Fl & 0x81) == 1)
				{
					num37 += WaveHeight(num15, num16, OpenOf(open, ref g), g.Coast) * (1f - num8 * lodK / waveRange);
				}
				bool flag4 = false;
				float num40 = num37;
				if (num8 < num14 && num37 > cut.Cap(num8))
				{
					if (num37 - z >= num8 * num12)
					{
						VeilStep(c, num8, num37, flag3, ref g, ref yVeil, prevT, prevH, prevOk, num15, num16, num2, num3, fogCol, num7);
					}
					num37 = (g.H = cut.Land(num8, num37));
					flag4 = true;
				}
				bool flag5 = !flag4 && (g.Flags & 8) != 0;
				bool flag6 = num8 * lodK < 12f;
				if (!((flag6 ? (num37 + 0.05f) : num37) - z < num8 * num12))
				{
					if (flag6 && flag3)
					{
						SampleLook(num15, num16, num8, ref g);
					}
					uint num41 = g.Col;
					if (flag6 && g.Mat == 1 && (g.Texel & 3) != 0 && !flag5)
					{
						float num42 = (float)((g.Texel >> 4) & 7) / 7f * 0.05f * (1f - num8 * lodK / 12f);
						num37 += num42;
						num41 = TerrainCache.Scale(num41, 1f + num42 * 2.2f);
					}
					else if (flag6 && g.Mat == 7 && !flag5)
					{
						int num43 = (int)(num16 * 8f) & 7;
						float num44 = num37;
						float num45;
						switch (num43)
						{
						default:
							num45 = 0.01f;
							break;
						case 0:
						case 3:
							num45 = -0.012f;
							break;
						case 1:
						case 4:
						case 6:
							num45 = 0.03f;
							break;
						}
						num37 = num44 + num45 * (1f - num8 * lodK / 12f);
					}
					float num46 = (tilted ? RowAtSlope((num37 - z) / num8) : (horizon - (num37 - z) * focal / num8));
					int num47 = 0;
					if (num46 > 0f)
					{
						num47 = (int)num46;
						if (num46 > (float)num47)
						{
							num47++;
						}
					}
					if (num47 < num7)
					{
						num47 = num7;
					}
					if (num47 < num6)
					{
						if (!flag6)
						{
							if (flag3)
							{
								SampleLook(num15, num16, num8, ref g);
							}
							num41 = g.Col;
						}
						float num48 = num8 / focal;
						bool flag7 = (g.Flags & 3) != 0;
						byte b6 = (byte)((g.Sun > 24 && (g.Flags & 0xA2) == 0) ? 1u : 0u);
						if (flag5 && (tilted ? (num8 * (SlopeAtRow(num47) - SlopeAtRow(num6))) : ((float)(num6 - num47) * num48)) >= 0.3f)
						{
							DrawWallFace(c, num47, num6, num8, num37, num15, num16, num2, num3, ref g, fogCol);
						}
						else if (flag2 && g.Inside && !flag7 && num6 - num47 > 2 && num37 > num10)
						{
							float num49 = (num37 - num10) / (num8 - num9);
							byte share;
							uint num50 = ShadeSample(num41, g.Sun, g.Open, g.Flags, g.Mat, g.Texel, num15, num16, num8, flag4 ? num40 : num37, c, fogCol, g.Coast, out share);
							if (natural)
							{
								b6 = share;
							}
							float num51 = -1f;
							uint num52 = 0u;
							byte b7 = 0;
							for (int i = num47; i < num6; i++)
							{
								int num53 = (height - 1 - i) * cw + c;
								float num54 = rowK[i];
								float num55 = num54 - num49;
								float num56 = ((num55 < -1E-06f) ? ((num10 - num49 * num9 - z) / num55) : num8);
								if (num56 > num8 || num56 < num9)
								{
									num56 = ((num56 > num8) ? num8 : num9);
								}
								if (num56 > num8 - 0.02f)
								{
									cPix[num53] = num50;
									cLit[num53] = b6;
									cDep[num53] = num8;
									continue;
								}
								if (num51 < 0f || Math.Abs(num56 - num51) > 0.04f + num56 * (0.004f * lodK))
								{
									float px = x + num2 * num56;
									float py = y + num3 * num56;
									SampleGround(px, py, num56, ref g2);
									num52 = ShadeSample(g2.Col, g2.Sun, g2.Open, g2.Flags, g2.Mat, g2.Texel, px, py, num56, flag4 ? num40 : (z + num54 * num56), c, fogCol, g2.Coast, out var share2);
									b7 = (natural ? share2 : ((byte)((g2.Sun > 24 && (g2.Flags & 0x22) == 0) ? 1u : 0u)));
									num51 = num56;
								}
								cPix[num53] = num52;
								cLit[num53] = b7;
								cDep[num53] = num56;
							}
						}
						else
						{
							byte share3;
							uint num57 = ShadeSample(num41, g.Sun, g.Open, g.Flags, g.Mat, g.Texel, num15, num16, num8, flag4 ? num40 : num37, c, fogCol, g.Coast, out share3);
							if (natural)
							{
								b6 = share3;
							}
							int num58 = num47;
							int num59 = (height - 1 - num47) * cw + c;
							while (num58 < num6)
							{
								cPix[num59] = num57;
								cLit[num59] = b6;
								cDep[num59] = num8;
								num58++;
								num59 -= cw;
							}
						}
						num6 = num47;
						if (num47 < yVeil)
						{
							yVeil = num47;
						}
						if (num6 <= num7)
						{
							break;
						}
						num12 = (tilted ? SlopeAtRow((float)num6 - 0.75f) : ((horizon - (float)num6 + 0.75f) * num11));
						num13 = (tilted ? SlopeAtRow((float)num6 - 0.02f) : ((horizon - (float)num6 + 0.02f) * num11));
					}
				}
				num9 = num8;
				num10 = g.H;
				flag2 = true;
				prevT = num8;
				prevH = num40;
				prevOk = true;
				if (flag && num5 - z <= num8 * num13 && (tilted ? RowAtSlope((num5 - z) / num8) : (horizon - (num5 - z) * focal / num8)) >= (float)num6)
				{
					break;
				}
				float num60 = 0.012f + num8 * num4;
				if (array != null && num8 > 2f)
				{
					int num61 = (int)num15 >> 3;
					int num62 = (int)num16 >> 3;
					if (num15 >= 0f && num16 >= 0f && num61 < blocksX && num62 < blocksY)
					{
						float num63 = array[num62 * blocksX + num61] + waveLift;
						if (num63 - z <= num8 * num13 && (tilted ? RowAtSlope((num63 - z) / num8) : (horizon - (num63 - z) * focal / num8)) >= (float)num6)
						{
							float num64 = num61 << 3;
							float num65 = num62 << 3;
							float val = ((num2 > 0f) ? ((num64 + 8f - x) / num2) : ((num2 < 0f) ? ((num64 - x) / num2) : float.MaxValue));
							float val2 = ((num3 > 0f) ? ((num65 + 8f - y) / num3) : ((num3 < 0f) ? ((num65 - y) / num3) : float.MaxValue));
							float num66 = Math.Min(val, val2);
							float num67 = ((num63 > z) ? num8 : num66);
							if (num66 > num8 + num60 && (tilted ? RowAtSlope((num63 - z) / num67) : (horizon - (num63 - z) * focal / num67)) >= (float)num6)
							{
								num8 = num66 + 0.01f;
								flag2 = (prevOk = false);
								continue;
							}
						}
					}
				}
				num8 += num60;
			}
			bool flag8 = !tilted;
			int num68 = num7;
			int num69 = (height - 1 - num7) * cw + c;
			while (num68 < num6)
			{
				cPix[num69] = (flag8 ? SkyPixel(c, num68) : 0u);
				cLit[num69] = 0;
				cDep[num69] = float.MaxValue;
				num68++;
				num69 -= cw;
			}
		}

		private void DrawWallFace(int c, int top, int ybuf, float t, float h, float px, float py, float dx, float dy, ref GroundSample g, uint fogCol)
		{
			int num = (int)px;
			int num2 = (int)py;
			float num3 = ((dx > 0f) ? (((float)num - _view.X) / dx) : ((dx < 0f) ? (((float)(num + 1) - _view.X) / dx) : (-1E+09f)));
			float num4 = ((dy > 0f) ? (((float)num2 - _view.Y) / dy) : ((dy < 0f) ? (((float)(num2 + 1) - _view.Y) / dy) : (-1E+09f)));
			float num5;
			float num6;
			float x;
			float y;
			if (num3 > num4)
			{
				num5 = _view.Y + dy * num3;
				num6 = FaceLambert((dx > 0f) ? (-1f) : 1f, 0f);
				x = ((dx > 0f) ? num : (num + 1));
				y = num5;
			}
			else
			{
				num5 = _view.X + dx * num4;
				num6 = FaceLambert(0f, (dy > 0f) ? (-1f) : 1f);
				x = num5;
				y = ((dy > 0f) ? num2 : (num2 + 1));
			}
			bool landField = _landField;
			if ((g.Flags & 0x20) != 0 && !landField)
			{
				num6 = 0f;
			}
			int open = g.Open * 200 >> 8;
			int num7 = (int)(num6 * 225f) + 30;
			uint c2 = TerrainCache.Scale(g.Col, 0.95f);
			float num8 = t / _focal;
			uint num9 = ShadeSample(g.Col, g.Sun, g.Open, g.Flags, g.Mat, g.Texel, px, py, t, h, c, fogCol, g.Coast);
			uint[] cPix = _cPix;
			float[] cDep = _cDep;
			byte[] cLit = _cLit;
			byte b = (byte)((num7 > 60) ? ((!_natural) ? 1u : 255u) : 0u);
			for (int i = top; i < ybuf; i++)
			{
				int num10 = (Height - 1 - i) * _cw + c;
				float num11 = (_tilted ? (_view.Z + _rowK[i] * t) : (_view.Z + (_horizon - ((float)i + 0.5f)) * num8));
				int sun = num7;
				byte b2 = b;
				if (landField)
				{
					float vis = ThingVis(x, y, num11);
					sun = (int)(num6 * 225f * Lift(vis)) + 30;
					if (b2 != 0)
					{
						b2 = Share(vis);
					}
				}
				uint num12;
				if (h - num11 < 0.1f)
				{
					num12 = num9;
				}
				else
				{
					float num13 = ((h - num11 > 1.5f) ? 1f : (0.82f + 0.12f * (h - num11)));
					num12 = LightSurface(TerrainCache.Scale(c2, (1f + Masonry(num11, num5)) * num13), sun, open);
					num12 = TerrainCache.Lerp(num12, fogCol, Haze(t, num11));
				}
				cPix[num10] = num12;
				cLit[num10] = b2;
				cDep[num10] = t;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float FaceLambert(float nx, float ny)
		{
			float num = (float)Math.Sqrt(nx * nx + ny * ny);
			if (!(num > 1E-05f))
			{
				return 0f;
			}
			return Math.Max(0f, (nx * _light.X + ny * _light.Y) / num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float RockLayers(float z, float px, float py)
		{
			float num = CloudNoise.Sample(px * 2.2f, py * 2.2f) * 1.8f;
			uint num2 = TerrainModel.Hash(FastMath.FloorInt(z * 1.5f + num), 913);
			uint num3 = TerrainModel.Hash((int)(px * 16f) + (int)(z * 9f) * 7, (int)(py * 16f));
			return ((float)(num2 & 0xFF) / 255f - 0.5f) * 0.2f + ((float)(num3 & 0xFF) / 255f - 0.5f) * 0.08f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Masonry(float z, float along)
		{
			float num = z / 0.3f;
			int num2 = FastMath.FloorInt(num);
			float num3 = along / 0.55f + (((num2 & 1) != 0) ? 0.5f : 0f);
			int num4 = FastMath.FloorInt(num3);
			if (num - (float)num2 < 0.13f || num3 - (float)num4 < 0.07f)
			{
				return -0.3f;
			}
			uint num5 = TerrainModel.Hash(num4, num2);
			uint num6 = TerrainModel.Hash((int)(z * 16f), (int)(along * 16f));
			return ((float)(num5 & 0xFF) / 255f - 0.5f) * 0.18f + ((float)(num6 & 0xFF) / 255f - 0.5f) * 0.05f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint ShadeSample(uint col, int sun, int open, byte fl, byte mat, uint texel, float px, float py, float t, float h, int c, uint fogCol, float coast)
		{
			byte share;
			return ShadeSample(col, sun, open, fl, mat, texel, px, py, t, h, c, fogCol, coast, out share);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint ShadeSample(uint col, int sun, int open, byte fl, byte mat, uint texel, float px, float py, float t, float h, int c, uint fogCol, float coast, out byte share)
		{
			float hazeScale = 1f;
			float num = t * _lodK;
			share = 0;
			float num2 = 1f;
			float num3 = 1f;
			if (_natural)
			{
				bool flag = sun > 24 && (fl & 2) == 0;
				if (_landField && (sun > 0 || (fl & 1) != 0))
				{
					num3 = ThingVis(px, py, h);
					num2 = Lift(num3);
					if ((fl & 1) == 0)
					{
						sun = (int)((float)sun * num2);
					}
					if (flag)
					{
						share = Share(num3);
					}
				}
				else if (flag && (fl & 0x20) == 0)
				{
					share = byte.MaxValue;
				}
			}
			if ((fl & 1) != 0 && _terrain != null)
			{
				IceEdge(ref fl, ref col, px, py);
			}
			if ((fl & 0x80) != 0)
			{
				col = IceSurface(col, sun, open, px, py, t, h, fogCol);
				share = 0;
			}
			else if ((fl & 1) != 0)
			{
				WaveSlope(px, py, num, out var gx, out var gy);
				if (Sea.A1 > 0f && num < _waveRange && _terrain != null)
				{
					Sea.StormSlope(px, py, Time, _terrain.OpenAt(px, py), out var gx2, out var gy2);
					float num4 = (1f - num / _waveRange) * Sea.ShoreFade(coast);
					gx += gx2 * num4;
					gy += gy2 * num4;
				}
				float num5 = 1f / (float)Math.Sqrt(gx * gx + gy * gy + 1f);
				float num6 = (0f - gx) * num5;
				float num7 = (0f - gy) * num5;
				float num8 = num5;
				float num9 = num6 * _light.X + num7 * _light.Y + num8 * _light.Z - _light.Z;
				float num10 = 0.96f + num9 * 1.4f;
				num10 = ((num10 < 0.84f) ? 0.84f : ((num10 > 1.1f) ? 1.1f : num10));
				if (_terrain != null && _terrain.HasSeabed)
				{
					col = SeeInto(col, px, py, h, num, gx, gy, num2);
				}
				else if (coast < 0.3f)
				{
					col = TerrainCache.Lerp(col, Shallows, 0.45f * (1f - coast / 0.3f));
				}
				col = TerrainCache.Tint(TerrainCache.Scale(col, num10), _waterTint.R, _waterTint.G, _waterTint.B);
				float num11 = _view.X - px;
				float num12 = _view.Y - py;
				float num13 = _view.Z - h;
				float num14 = 1f / (float)Math.Sqrt(num11 * num11 + num12 * num12 + num13 * num13 + 1E-06f);
				num11 *= num14;
				num12 *= num14;
				num13 *= num14;
				float val = 1f - Math.Min(1f, 3.5f * (_view.Z - h + 0.05f) / Math.Max(0.1f, t));
				float num15 = (float)Math.Sqrt(num11 * num11 + num12 * num12) + 0.0001f;
				float num16 = (0f - (num6 * num11 + num7 * num12)) / num15;
				float num17 = 0.12f + 0.5f * Math.Max(0f, val) + num16 * 0.9f;
				num17 = ((num17 < 0.05f) ? 0.05f : ((num17 > 0.8f) ? 0.8f : num17));
				col = TerrainCache.Lerp(col, TerrainCache.Lerp(fogCol, Air.Zenith, 0.2f), num17);
				float num18 = _glitter * num2;
				bool flag2 = _glintMap;
				if (num18 > 0f && num < 60f)
				{
					float num19 = _light.X + num11;
					float num20 = _light.Y + num12;
					float num21 = _light.Z + num13;
					float num22 = 1f / (float)Math.Sqrt(num19 * num19 + num20 * num20 + num21 * num21 + 1E-06f);
					num19 *= num22;
					num20 *= num22;
					num21 *= num22;
					float cell = ((num < 4.375f) ? 32f : ((num < 8.75f) ? 16f : 8f));
					float x;
					float y;
					float t2;
					float shine;
					uint num23 = GlintSpeck(px, py, cell, 0, out x, out y, out t2, out shine);
					float num24 = Math.Min(1f, num / 12f) * (1f - num / 60f);
					float num25 = 0.18f * num24 * (1f - 0.75f * Air.Night);
					float num26 = num6 * num19 + num7 * num20 + num8 * num21;
					if (flag2 && shine > 0f && num23 < (uint)(Math.Max(num25, (num26 > 0.95f) ? 1f : 0f) * num18 * 255f))
					{
						num18 = _glitter * Lift(GlintVis(px, py, h, t, num3));
						flag2 = false;
					}
					if (shine > 0f && num23 < (uint)(Math.Max(num25, (num26 > 0.95f) ? 1f : 0f) * num18 * 255f))
					{
						WaveSlope(x, y, num, t2, out var gx3, out var gy3);
						float num27 = 1f / (float)Math.Sqrt(gx3 * gx3 + gy3 * gy3 + 1f);
						float num28 = (0f - gx3) * num27;
						float num29 = (0f - gy3) * num27;
						float num30 = num27;
						num26 = num28 * num19 + num29 * num20 + num30 * num21;
						float num31 = 0f;
						if (num26 > 0.97f)
						{
							num31 = Math.Min(1f, (num26 - 0.97f) * 40f) * (0.35f + 0.65f * num24);
						}
						else if (num28 * _light.X + num29 * _light.Y + num30 * _light.Z - _light.Z > 0.04f)
						{
							num31 = num25;
						}
						num31 *= num18;
						if (num23 < (uint)(num31 * 255f))
						{
							col = TerrainCache.Lerp(col, _glitterColor, (0.55f + 0.4f * Math.Min(1f, num31 * 2f)) * shine);
						}
					}
				}
				float num32 = SeaFoam(px, py, t, num, h);
				if (num32 > 0f)
				{
					col = TerrainCache.Lerp(col, LightSurface(4294242536u, sun, 255), num32);
				}
				if (coast < 0.3f)
				{
					float num33 = coast - (0.12f - Swash(px, py)) * 0.6f;
					uint num34 = TerrainModel.Hash((int)(px * 8f), (int)(py * 8f));
					float num35 = 1f - num33 / 0.21000001f - (float)(num34 & 0xFF) / 255f * 0.6f;
					if (num35 > 0f)
					{
						col = TerrainCache.Lerp(col, LightSurface(4294242536u, sun, 255), 0.35f + 0.45f * Math.Min(1f, num35 * 2f));
					}
				}
				float num36 = _sunSide[c];
				if (num36 > 0.9f && num > 5f && num18 > 0f)
				{
					float x2;
					float y2;
					float t3;
					float shine2;
					uint num37 = GlintSpeck(px, py, 9f, 17, out x2, out y2, out t3, out shine2);
					float num38 = (num36 - 0.9f) * 10f;
					num38 = num38 * num38 * num38 * num18 * Math.Min(1f, num / 40f);
					if (flag2 && num37 < (uint)(num38 * 40f))
					{
						num18 = _glitter * Lift(GlintVis(px, py, h, t, num3));
						num38 = (num36 - 0.9f) * 10f;
						num38 = num38 * num38 * num38 * num18 * Math.Min(1f, num / 40f);
					}
					if (num37 < (uint)(num38 * 40f))
					{
						col = TerrainCache.Lerp(col, _glitterColor, (0.5f + 0.5f * num38) * shine2);
					}
				}
				if (RippleStrength > 0f)
				{
					float num39 = px - RippleX;
					float num40 = py - RippleY;
					float num41 = num39 * num39 + num40 * num40;
					if (num41 < 25f)
					{
						float num42 = (float)Math.Sqrt(num41);
						float num43 = (float)(Math.Sin(num42 * 9f - Time * 7f) * Math.Exp((0f - num42) * 0.7f)) * RippleStrength;
						if (num43 > 0f)
						{
							col = TerrainCache.Lerp(col, LightSurface(uint.MaxValue, sun, 255), num43 * 0.4f);
						}
					}
				}
				for (int i = 0; i < WakeCount; i++)
				{
					float num44 = px - Wakes[i * 4];
					float num45 = py - Wakes[i * 4 + 1];
					float num46 = num44 * num44 + num45 * num45;
					if (!(num46 >= 6.25f))
					{
						float num47 = (float)Math.Sqrt(num46);
						float num48 = Wakes[i * 4 + 3];
						float num49 = (float)(Math.Sin(num47 * 11f - Time * 7f) * Math.Exp((0f - num47) * 1.3f)) * Wakes[i * 4 + 2];
						if (num49 > 0f)
						{
							col = TerrainCache.Lerp(col, LightSurface(uint.MaxValue, sun, 255), num49 * 0.4f);
						}
						float num50 = Math.Abs(num47 - num48) / (0.3f * num48 + 0.015f);
						if (num50 < 1f)
						{
							float num51 = (float)Math.Atan2(num45, num44);
							float num52 = 0.55f + 0.45f * (0.5f + 0.5f * FastSin(num51 * 5f + Time * 1.7f + num48 * 31f)) * (0.5f + 0.5f * FastSin(num51 * 11f - Time * 2.3f));
							col = TerrainCache.Lerp(col, LightSurface(4294242536u, sun, 255), (1f - num50) * 0.7f * num52);
						}
					}
				}
				if (num3 < 1f)
				{
					int share2 = Share(1f - num3);
					col = TerrainCache.Tint(col, ShareTint(_landTint.R, share2), ShareTint(_landTint.G, share2), ShareTint(_landTint.B, share2));
				}
			}
			else if ((fl & 2) != 0)
			{
				col = LavaSurface(col, sun, open, px, py, t, h, out hazeScale);
			}
			else
			{
				if (num < 40f)
				{
					float num53 = 1f - num / 40f;
					if (mat == 1 && num < 6f)
					{
						num53 *= 0.4f + 0.6f * num / 6f;
					}
					col = TerrainCache.Scale(col, 1f + MaterialNoise(mat, texel, (int)(px * 8f), (int)(py * 8f)) * num53);
				}
				if (num < 7f && mat != 8 && mat != 6)
				{
					uint num54 = TerrainModel.Hash((int)(px * 26f) + 7, (int)(py * 26f) - 3);
					float num55 = ((mat == 1) ? 0.16f : 0.08f);
					col = TerrainCache.Scale(col, 1f + ((float)(num54 & 0xFF) / 255f - 0.5f) * num55 * (1f - num / 7f));
				}
				if (mat == 4)
				{
					col = TerrainCache.Scale(col, 1f + RockLayers(h, px, py));
				}
				if (mat != 5 && mat != 6 && mat != 8 && _terrain != null)
				{
					bool marked;
					float num56 = Strata.Share(Strata.SlopeAt(_terrain, px, py, out marked), marked);
					if (num56 > 0f)
					{
						col = Strata.Apply(col, num56, h, px, py, num);
					}
				}
				col = MacroTint(col, mat, px, py);
				uint unlit = col;
				col = LightSurface(col, sun, open);
				if (Air.Night > 0.02f)
				{
					col = BiomeGlow(col, unlit, px, py);
				}
				if (Air.DawnMist > 0.02f)
				{
					col = CutMist(col, fogCol, px, py);
				}
			}
			if ((fl & 4) != 0 && (fl & 3) == 0)
			{
				col = TerrainCache.Scale(col, 0.4f);
				uint num57 = TerrainModel.Hash((int)(px * 9f), (int)(py * 9f));
				float num58 = 0.5f + 0.5f * (float)Math.Sin(Time * 4.7f + (float)(num57 & 0xFF) * 0.05f);
				switch ((int)(num57 & 7))
				{
				case 0:
					col = TerrainCache.Lerp(col, EmberGlow, 0.5f + 0.4f * num58);
					break;
				case 1:
					col = TerrainCache.Lerp(col, FireColor, 0.3f * num58);
					break;
				}
				hazeScale = 0.6f;
			}
			return TerrainCache.Lerp(col, fogCol, Haze(t, h) * hazeScale);
		}

		private uint BiomeGlow(uint col, uint unlit, float px, float py)
		{
			TerrainCache terrain = _terrain;
			if (terrain == null || terrain.Marks.Length != terrain.Flags.Length)
			{
				return col;
			}
			int num = (int)px;
			int num2 = (int)py;
			if (num < 0 || num2 < 0 || num >= terrain.Width || num2 >= terrain.Height || (terrain.Marks[num2 * terrain.Width + num] & 8) == 0)
			{
				return col;
			}
			uint num3 = TerrainModel.Hash(num * 31 + 5, num2 * 17 - 9);
			float num4 = 0.75f + 0.25f * (float)Math.Sin(Time * 0.8f + (float)(num3 & 0xFF) * 0.025f);
			uint num5 = TerrainModel.Hash((int)(px * 8f) + 11, (int)(py * 8f) - 7);
			float val = Air.Night * 0.42f * num4 * (((num5 & 0xF) == 0) ? 1.8f : 1f);
			return TerrainCache.Lerp(col, TerrainCache.Scale(unlit, 1.25f), Math.Min(1f, val));
		}

		private uint CutMist(uint col, uint fogCol, float px, float py)
		{
			TerrainCache terrain = _terrain;
			if (terrain == null || terrain.Marks.Length != terrain.Flags.Length)
			{
				return col;
			}
			int num = (int)px;
			int num2 = (int)py;
			int width = terrain.Width;
			if (num < 1 || num2 < 1 || num >= width - 1 || num2 >= terrain.Height - 1)
			{
				return col;
			}
			int num3 = num2 * width + num;
			if ((terrain.Marks[num3] & 1) == 0)
			{
				return col;
			}
			float num4 = Math.Max(Math.Max(terrain.Ground[num3 - 1], terrain.Ground[num3 + 1]), Math.Max(terrain.Ground[num3 - width], terrain.Ground[num3 + width])) - terrain.Ground[num3];
			if (num4 < 0.08f)
			{
				return col;
			}
			float num5 = Math.Min(1f, (num4 - 0.08f) / 0.5f);
			uint num6 = TerrainModel.Hash((int)(px * 3f) + (int)(Time * 0.6f), (int)(py * 3f));
			float t = Air.DawnMist * 0.6f * num5 * (0.8f + 0.2f * ((float)(num6 & 0xFF) / 255f));
			return TerrainCache.Lerp(col, TerrainCache.Scale(fogCol, 1.08f), t);
		}

		private static float[] BuildSinTable()
		{
			float[] array = new float[1024];
			for (int i = 0; i < 1024; i++)
			{
				array[i] = (float)Math.Sin((float)i / (512f / MathF.PI));
			}
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float FastSin(float x)
		{
			return SinTable[(int)(x * (512f / MathF.PI)) & 0x3FF];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float FastCos(float x)
		{
			return SinTable[((int)(x * (512f / MathF.PI)) + 256) & 0x3FF];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float WaveHeight(float px, float py, float open, float coast)
		{
			return Sea.Height(px, py, Time, open, Sea.ShoreFade(coast));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float OpenOf(byte[] open, ref GroundSample g)
		{
			if (open == null)
			{
				return 0f;
			}
			float num = (float)(int)open[g.I00] + (float)(open[g.I10] - open[g.I00]) * g.Tx;
			float num2 = (float)(int)open[g.I01] + (float)(open[g.I11] - open[g.I01]) * g.Tx;
			return (num + (num2 - num) * g.Ty) * 0.003921569f;
		}

		private void WaveSlope(float px, float py, float dist, out float gx, out float gy)
		{
			WaveSlope(px, py, dist, Time, out gx, out gy);
		}

		private void WaveSlope(float px, float py, float dist, float t, out float gx, out float gy)
		{
			float num = 5f * Math.Max(0f, 1f - dist / 90f);
			float num2 = Math.Max(0f, 1f - dist / 40f);
			float num3 = 0.020112f * FastCos(1.257f * (0.8f * px + 0.6f * py) - 1.1f * t);
			float num4 = 0.020299999f * FastCos(2.03f * (-0.35f * px + 0.94f * py) - 1.5f * t + 1.7f);
			float num5 = 0.0165f * FastCos(3.3f * (0.93f * px + -0.37f * py) - 2.1f * t + 4.1f);
			gx = (num3 * 0.8f + num4 * -0.35f + num5 * 0.93f) * num;
			gy = (num3 * 0.6f + num4 * 0.94f + num5 * -0.37f) * num;
			if (num2 > 0f)
			{
				float num6 = 0.13f * FastCos(7f * (0.5f * px - 0.87f * py) - 3.2f * t + 0.6f);
				float num7 = 0.1f * FastCos(10.5f * (-0.9f * px - 0.44f * py) - 4.1f * t + 2.3f);
				gx += (num6 * 0.5f + num7 * -0.9f) * num2;
				gy += (num6 * -0.87f + num7 * -0.44f) * num2;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private uint GlintSpeck(float px, float py, float cell, int seed, out float x0, out float y0, out float t0, out float shine)
		{
			float num = Time * 0.8750995f % 1024f;
			float num2 = 0.8f * num;
			float num3 = 0.6f * num;
			int num4 = FastMath.FloorInt((px - num2) * cell);
			int num5 = FastMath.FloorInt((py - num3) * cell);
			uint num6 = TerrainModel.Hash(num4 * 3 + seed, num5 * 5 - seed);
			float num7 = 0.6f + (float)((num6 >> 8) & 0xFF) * 0.0031372549f;
			float num8 = Time / num7 + (float)((num6 >> 16) & 0xFF) / 255f;
			int num9 = FastMath.FloorInt(num8);
			float num10 = (num8 - (float)num9) * num7;
			shine = Math.Min(1f, (1f - Math.Abs(2f * (num8 - (float)num9) - 1f)) * 3f);
			t0 = Time - num10;
			x0 = ((float)num4 + 0.5f) / cell + num2 - 0.7000796f * num10;
			y0 = ((float)num5 + 0.5f) / cell + num3 - 0.5250597f * num10;
			return TerrainModel.Hash(num4 * 3 + num9 * 7919 + seed, num5 * 5 - num9 * 4111) & 0xFF;
		}

		private uint LavaSurface(uint col, int sun, int open, float px, float py, float t, float h, out float hazeScale)
		{
			float num = 0.5f + 0.5f * (float)Math.Sin(Time * 2.3f + px * 1.3f - py * 1.7f);
			uint num2 = TerrainCache.Lerp(col, LavaGlow, num * 0.45f);
			int num3 = (int)(col & 0xFF);
			int num4 = (int)((col >> 8) & 0xFF);
			int num5 = (int)((col >> 16) & 0xFF);
			float num6 = ((num3 > num4 && num3 > num5) ? ((float)(num4 - num5) / (6f * (float)Math.Max(1, num3 - Math.Min(num4, num5)))) : 0.15f);
			float num7 = (float)Math.Sqrt(Math.Max(0f, Math.Min(1f, (num6 - 0.015f) / 0.13f)));
			float num8 = 0.2f + 0.25f * num7;
			if (h > 0.48000002f)
			{
				num8 = Math.Min(0.8f, num8 + 0.35f);
			}
			float num9 = 0.5f + (num8 - 0.5f) * 0.22f;
			uint num10 = TerrainCache.Lerp(LightSurface(LavaCrust, sun, open), LavaSeam, 0.08f);
			float num11 = (float)Math.Sqrt(num8);
			uint num12 = TerrainCache.Lerp(num10, num2, num11);
			float num13 = 0.35f + 0.65f * (1f - num11);
			float num14 = (t - 30f) / 40f;
			if (num14 >= 1f)
			{
				hazeScale = num13;
				return num12;
			}
			float num15 = Time * 0.05f;
			float num16 = CloudNoise.Sample(px * 11f + num15, py * 11f - num15 * 0.5f) * 0.45f + CloudNoise.Sample(px * 23f + 91f - num15 * 1.5f, py * 23f + 37f) * 0.35f + CloudNoise.Sample(px * 47f + 17f, py * 47f + 203f + num15) * 0.2f;
			uint num18;
			if (num16 < num9 - 0.012f)
			{
				float num17 = Math.Min(1f, (num9 - 0.012f - num16) / 0.06f);
				num18 = TerrainCache.Lerp(num2, TerrainCache.Lerp(col, LavaDeep, 0.45f), num17 * 0.55f);
				hazeScale = 0.35f;
			}
			else if (num16 < num9 + 0.006f)
			{
				num18 = TerrainCache.Lerp(num2, LavaGlow, 0.55f);
				hazeScale = 0.35f;
			}
			else if (Math.Abs(CloudNoise.Sample(px * 14f + 13f + num15, py * 14f + 57f) - 0.5f) < 0.018f)
			{
				num18 = LavaSeam;
				hazeScale = 0.5f;
			}
			else
			{
				uint num19 = TerrainModel.Hash((int)(px * 8f) + 5, (int)(py * 8f) - 11);
				num18 = TerrainCache.Scale(num10, 0.8f + (float)(num19 & 0xFF) / 255f * 0.4f);
				num18 = TerrainCache.Lerp(num18, LavaSeam, Math.Max(0f, 1f - (num16 - num9) / 0.04f) * 0.3f);
				hazeScale = 1f;
			}
			if (num14 > 0f)
			{
				num18 = TerrainCache.Lerp(num18, num12, num14);
				hazeScale += (num13 - hazeScale) * num14;
			}
			return num18;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint MacroTint(uint col, byte mat, float px, float py)
		{
			float num = CloudNoise.Sample(px * 3.1f, py * 3.1f) - 0.5f;
			switch (mat)
			{
			case 1:
				if (!(num > 0f))
				{
					return TerrainCache.Lerp(col, LushGrass, (0f - num) * 0.55f);
				}
				return TerrainCache.Lerp(col, DryGrass, num * 0.5f);
			case 4:
			case 5:
				if (!(num > 0f))
				{
					return TerrainCache.Lerp(col, CoolStone, (0f - num) * 0.3f);
				}
				return TerrainCache.Lerp(col, WarmStone, num * 0.25f);
			case 2:
			case 3:
			case 9:
				if (!(num > 0f))
				{
					return TerrainCache.Scale(col, 1f + num * 0.2f);
				}
				return TerrainCache.Lerp(col, WarmStone, num * 0.2f);
			default:
				return col;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float MaterialNoise(byte mat, uint h, int tx, int ty)
		{
			float num = (float)(h & 0xFF) / 255f - 0.5f;
			switch (mat)
			{
			case 1:
				return num * 0.2f + ((((h >> 8) & 0xF) == 0) ? 0.16f : 0f) - ((((h >> 8) & 0xF) == 1) ? 0.14f : 0f);
			case 2:
				return num * 0.07f + ((((h >> 8) & 7) == 0) ? 0.08f : 0f) + 0.035f * (float)Math.Sin((float)ty * 0.9f + (float)tx * 0.35f);
			case 3:
				return ((float)(TerrainModel.Hash(tx >> 1, ty >> 1) & 0xFF) / 255f - 0.5f) * 0.18f - ((((h >> 8) & 0xF) == 0) ? 0.14f : 0f);
			case 4:
				return ((float)(TerrainModel.Hash(tx >> 2, ty >> 2) & 0xFF) / 255f - 0.5f) * 0.16f + num * 0.06f;
			case 5:
				return num * 0.05f + ((((h >> 8) & 0x1F) == 0) ? 0.14f : 0f);
			case 6:
			{
				int num5 = ty / 3;
				int num6 = num5 & 1;
				if (ty % 3 == 0 || (tx + num6) % 3 == 0)
				{
					return -0.22f;
				}
				return ((float)(TerrainModel.Hash((tx + num6) / 3, num5) & 0xFF) / 255f - 0.5f) * 0.18f;
			}
			case 7:
			{
				float num3;
				switch (ty % 8)
				{
				default:
					num3 = 0.02f;
					break;
				case 0:
				case 3:
					num3 = -0.16f;
					break;
				case 1:
				case 4:
				case 6:
					num3 = 0.1f;
					break;
				}
				uint num4 = TerrainModel.Hash(tx >> 1, ty);
				return num3 + ((float)(num4 & 0xFF) / 255f - 0.5f) * 0.12f + num * 0.04f;
			}
			case 9:
			{
				uint num7 = (h >> 8) & 0x1F;
				float num8 = num * 0.08f;
				float num9;
				switch (num7)
				{
				default:
					num9 = 0f;
					break;
				case 1u:
					num9 = -0.12f;
					break;
				case 0u:
					num9 = 0.16f;
					break;
				}
				return num8 + num9 + ((((h >> 13) & 0x3F) == 0) ? (-0.1f) : 0f);
			}
			case 8:
			{
				int num2 = ty / 3;
				if (ty % 3 == 0)
				{
					return -0.2f;
				}
				if ((tx + num2 * 5) % 11 == 0)
				{
					return -0.14f;
				}
				return num * 0.05f + 0.04f * (float)Math.Sin((float)tx * 0.7f + (float)num2 * 2.1f);
			}
			default:
				return num * 0.07f;
			}
		}

		private void RenderObjects(int stripe, int x0, int x1)
		{
			List<Billboard> billboards = _billboards;
			int pick = -1;
			float pickDepth = float.MaxValue;
			int width = Width;
			int height = Height;
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = Math.Min(stripe, 63) * 4;
			Tint3 shadowTint = _shadowTint;
			bool natural = _natural;
			for (int i = 0; i < billboards.Count; i++)
			{
				Billboard billboard = billboards[i];
				if (!billboard.Shadow)
				{
					continue;
				}
				float num2 = billboard.X - _view.X;
				float num3 = billboard.Y - _view.Y;
				float num4 = num2 * _fx + num3 * _fy;
				if (num4 < 0.3f || num4 > (billboard.Far ? MaxDistance : (BillboardDistance * 0.6f)))
				{
					continue;
				}
				float num5 = billboard.Width * ((billboard.Kind == BillboardKind.Cross) ? 0.3f : 0.38f);
				float col;
				float row;
				float num7;
				float num8;
				if (!_tilted)
				{
					float num6 = _focal / num4;
					col = (float)width * 0.5f + (num2 * _rx + num3 * _ry) * num6;
					row = _horizon - (billboard.GroundZ - _view.Z) * num6;
					num7 = num5 * num6;
					num8 = num7 * Math.Max(0.08f, (_view.Z - billboard.GroundZ) / num4);
				}
				else
				{
					float dz = billboard.GroundZ - _view.Z;
					if (!ProjectRel(num2, num3, dz, 0.1f, out col, out row, out var t, out var scale))
					{
						continue;
					}
					num7 = num5 * scale;
					num8 = num7 * 0.08f;
					if (ProjectRel(num2 - _fx * num5, num3 - _fy * num5, dz, 0.05f, out t, out var row2, out var t2, out var scale2) && ProjectRel(num2 + _fx * num5, num3 + _fy * num5, dz, 0.05f, out scale2, out var row3, out t2, out t))
					{
						row = (row2 + row3) * 0.5f;
						num8 = Math.Max(num8, Math.Abs(row2 - row3) * 0.5f);
					}
				}
				int num9 = Math.Max(x0, (int)(col - num7));
				int num10 = Math.Min(x1 - 1, (int)(col + num7));
				int num11 = Math.Max(0, (int)(row - num8));
				int num12 = Math.Min(height - 1, (int)(row + num8));
				for (int j = num11; j <= num12; j++)
				{
					float num13 = ((float)j + 0.5f - row) / num8;
					for (int k = num9; k <= num10; k++)
					{
						float num14 = ((float)k + 0.5f - col) / num7;
						if (num14 * num14 + num13 * num13 > 1f)
						{
							continue;
						}
						int num15 = (height - 1 - j) * width + k;
						if (Math.Abs(Depth[num15] - num4) > num5 * 2.5f + 0.4f)
						{
							continue;
						}
						if (natural)
						{
							int num16 = _sunlit[num15];
							if (num16 != 0)
							{
								Pixels[num15] = TerrainCache.Tint(Pixels[num15], ShareTint(shadowTint.R + 256 >> 1, num16), ShareTint(shadowTint.G + 256 >> 1, num16), ShareTint(shadowTint.B + 256 >> 1, num16));
								_sunlit[num15] = (byte)(num16 >> 1);
							}
						}
						else
						{
							Pixels[num15] = TerrainCache.Tint(Pixels[num15], shadowTint.R + 256 >> 1, shadowTint.G + 256 >> 1, shadowTint.B + 256 >> 1);
						}
					}
				}
			}
			_stripeTimes[num] = (float)stopwatch.Elapsed.TotalMilliseconds;
			stopwatch.Restart();
			List<VoxelObject> voxels = _voxels;
			if (_litR + _litG + _litB > 40)
			{
				for (int l = 0; l < voxels.Count; l++)
				{
					if (!voxels[l].Overlay)
					{
						DrawVoxelShadow(l, x0, x1);
					}
				}
			}
			_stripeTimes[num + 1] = (float)stopwatch.Elapsed.TotalMilliseconds;
			stopwatch.Restart();
			for (int m = 0; m < voxels.Count; m++)
			{
				if (!voxels[m].Overlay)
				{
					DrawVoxel(m, x0, x1, Depth, fogged: true, ref pick, ref pickDepth);
				}
			}
			_stripeTimes[num + 2] = (float)stopwatch.Elapsed.TotalMilliseconds;
			stopwatch.Restart();
			for (int n = 0; n < billboards.Count; n++)
			{
				Billboard b = billboards[n];
				if (b.Image == null || b.Alpha != 0)
				{
					continue;
				}
				if (b.Kind == BillboardKind.Cross)
				{
					float width2 = b.Width;
					float pivotX = b.Image.PivotX;
					for (int num17 = 0; num17 < 2; num17++)
					{
						float num18 = b.Angle + (float)num17 * (MathF.PI / 2f);
						float num19 = (float)Math.Cos(num18);
						float num20 = (float)Math.Sin(num18);
						float num21 = b.X - num19 * width2 * pivotX;
						float num22 = b.Y - num20 * width2 * pivotX;
						DrawPlane(ref b, num21, num22, num21 + num19 * width2, num22 + num20 * width2, x0, x1, ref pick, ref pickDepth);
					}
				}
				else
				{
					DrawFacing(ref b, x0, x1, ref pick, ref pickDepth);
				}
			}
			_stripeTimes[num + 3] = (float)stopwatch.Elapsed.TotalMilliseconds;
			_stripePick[stripe] = pick;
			_stripePickDepth[stripe] = pickDepth;
		}

		private void FadeNearParticles(List<Billboard> list)
		{
			float x = _view.X;
			float y = _view.Y;
			float z = _view.Z;
			for (int i = 0; i < list.Count; i++)
			{
				Billboard value = list[i];
				if (value.Image == null || value.Kind != BillboardKind.Facing || value.Tag != -1 || value.Shadow || value.Far)
				{
					continue;
				}
				float num = Math.Max(value.Width, value.Height);
				if (num <= 0f)
				{
					continue;
				}
				float num2 = value.X - x;
				float num3 = value.Y - y;
				float num4 = Math.Max(value.Z, Math.Min(value.Z + value.Height, z)) - z;
				float num5 = (float)Math.Sqrt(num2 * num2 + num3 * num3 + num4 * num4) / num;
				if (!(num5 >= 1.2f))
				{
					float num6 = (num5 - 0.6f) / 0.6f;
					if (num6 <= 0f)
					{
						value.Image = null;
						list[i] = value;
					}
					else
					{
						int num7 = ((value.Alpha != 0) ? value.Alpha : byte.MaxValue);
						value.Alpha = (byte)Math.Max(1, (int)((float)num7 * num6));
						list[i] = value;
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void CoverSky(int idx, float w)
		{
			if (Depth[idx] == float.MaxValue)
			{
				int num = (int)(w * 255f);
				if (num > _skyCover[idx])
				{
					_skyCover[idx] = (byte)Math.Min(255, num);
				}
			}
		}

		private void RenderBlended(int stripes, int stripeW)
		{
			if (_skyCover.Length != Width * Height)
			{
				_skyCover = new byte[Width * Height];
			}
			else
			{
				Array.Clear(_skyCover, 0, _skyCover.Length);
			}
			_blended.Clear();
			List<Billboard> list = _billboards;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Alpha != 0 && list[i].Image != null)
				{
					_blended.Add(i);
				}
			}
			if (_blended.Count != 0)
			{
				float ex = _view.X;
				float ey = _view.Y;
				float fx = _fx;
				float fy = _fy;
				_blended.Sort(delegate(int a, int b)
				{
					float value = (list[a].X - ex) * fx + (list[a].Y - ey) * fy;
					return ((list[b].X - ex) * fx + (list[b].Y - ey) * fy).CompareTo(value);
				});
				if (UseParallel && stripes > 1)
				{
					ForStripes(stripes, stripeW, BlendedStripe);
				}
				else
				{
					BlendedStripe(0, 0, Math.Min(Width, stripeW));
				}
			}
		}

		private void BlendedStripe(int stripe, int x0, int x1)
		{
			List<Billboard> billboards = _billboards;
			int pick = -1;
			float pickDepth = float.MaxValue;
			for (int i = 0; i < _blended.Count; i++)
			{
				Billboard b = billboards[_blended[i]];
				DrawFacing(ref b, x0, x1, ref pick, ref pickDepth);
			}
		}

		private void RenderOverlay(int x0, int x1)
		{
			int pick = -1;
			float pickDepth = float.MaxValue;
			for (int i = 0; i < _voxels.Count; i++)
			{
				if (_voxels[i].Overlay)
				{
					DrawVoxel(i, x0, x1, _overlayDepth, fogged: false, ref pick, ref pickDepth);
				}
			}
		}

		private ShadowMask MaskFor(VoxelObject o, float sdx, float sdy, float sdz)
		{
			if (sdy < 0.2f)
			{
				return null;
			}
			VoxelShape shape = o.Shape;
			float num = sdx / sdy;
			float num2 = sdz / sdy;
			MaskKey key = new MaskKey
			{
				Shape = shape,
				Kx = (int)Math.Round(num * 64f),
				Kz = (int)Math.Round(num2 * 64f)
			};
			lock (_maskLock)
			{
				if (_masks.TryGetValue(key, out var value))
				{
					return value;
				}
			}
			int width = shape.Width;
			int height = shape.Height;
			int depth = shape.Depth;
			float num3 = Math.Min(0f, (0f - num) * (float)height) - 1f;
			float num4 = Math.Max(width, (float)width - num * (float)height) + 1f;
			float num5 = Math.Min(-depth, (float)(-depth) - num2 * (float)height) - 1f;
			float num6 = Math.Max(depth, (float)depth - num2 * (float)height) + 1f;
			int num7 = (int)Math.Ceiling(num4 - num3);
			int num8 = (int)Math.Ceiling(num6 - num5);
			if ((long)num7 * (long)num8 > 400000)
			{
				return null;
			}
			ShadowMask shadowMask = new ShadowMask
			{
				Cells = new byte[num7 * num8],
				W = num7,
				H = num8,
				U0 = num3,
				N0 = num5,
				Kx = num,
				Kz = num2,
				Top = height
			};
			byte[] cells = shadowMask.Cells;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < 2; j++)
				{
					float num9 = (float)i + 0.25f + (float)j * 0.5f;
					float num10 = (0f - num) * num9 - num3;
					float num11 = (0f - num2) * num9 - num5;
					for (int k = 0; k < width; k++)
					{
						if (shape.Grid != null)
						{
							int num12 = (i * width + k) * 2 * depth;
							for (int l = -depth; l < depth; l++)
							{
								if (shape.Grid[num12 + l + depth] >> 24 != 0)
								{
									Mark(cells, num7, num8, (float)k + 0.5f + num10, (float)l + 0.5f + num11);
								}
							}
							continue;
						}
						int num13 = i * width + k;
						int num14 = shape.Thickness[num13];
						if (num14 == 0)
						{
							continue;
						}
						int num15 = ((shape.Offset != null) ? shape.Offset[num13] : 0);
						for (int m = num15 - num14; m < num15 + num14; m++)
						{
							Mark(cells, num7, num8, (float)k + 0.5f + num10, (float)m + 0.5f + num11);
						}
						if (shape.Mirror != null && shape.Mirror[num13] != 0)
						{
							for (int n = -num15 - num14; n < -num15 + num14; n++)
							{
								Mark(cells, num7, num8, (float)k + 0.5f + num10, (float)n + 0.5f + num11);
							}
						}
					}
				}
			}
			lock (_maskLock)
			{
				if (_masks.Count > 1500)
				{
					_masks.Clear();
				}
				_masks[key] = shadowMask;
				return shadowMask;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Mark(byte[] cells, int w, int h, float u, float n)
		{
			int num = (int)u;
			int num2 = (int)n;
			if (num >= 0 && num2 >= 0 && num < w && num2 < h)
			{
				cells[num2 * w + num] = 1;
			}
		}

		private void PrepareVoxels()
		{
			_eyeLight = LightAtEye();
			if (_prep.Length < _voxels.Count)
			{
				_prep = new VoxelPrep[_voxels.Count * 2];
			}
			if (UseParallel && _voxels.Count > 8)
			{
				For(_voxels.Count, PrepareVoxel);
				return;
			}
			for (int i = 0; i < _voxels.Count; i++)
			{
				_prep[i] = Prepare(_voxels[i]);
			}
		}

		private void PrepareVoxel(int i)
		{
			_prep[i] = Prepare(_voxels[i]);
		}

		private VoxelPrep Prepare(VoxelObject o)
		{
			VoxelPrep p = default(VoxelPrep);
			VoxelShape shape = o.Shape;
			if (shape == null)
			{
				return p;
			}
			int width = shape.Width;
			int height = shape.Height;
			int depth = shape.Depth;
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MaxValue;
			float num6 = 0f;
			int num7 = 0;
			for (int i = 0; i < 8; i++)
			{
				float num8 = (((i & 1) != 0) ? width : 0);
				float num9 = (((i & 2) != 0) ? height : 0);
				float num10 = (((i & 4) != 0) ? depth : (-depth));
				Vec3 vec = o.Origin + o.U * (num8 * o.SizeU) + o.V * (num9 * o.SizeV) + o.N * (num10 * o.SizeN);
				if (ProjectRel(vec.X - _view.X, vec.Y - _view.Y, vec.Z - _view.Z, 0.02f, out var col, out var row, out var t, out var _))
				{
					num7++;
					if (col < num)
					{
						num = col;
					}
					if (col > num2)
					{
						num2 = col;
					}
					if (row < num3)
					{
						num3 = row;
					}
					if (row > num4)
					{
						num4 = row;
					}
					if (t < num5)
					{
						num5 = t;
					}
					if (t > num6)
					{
						num6 = t;
					}
				}
			}
			if (num7 == 0)
			{
				return p;
			}
			if (num7 < 8)
			{
				num = 0f;
				num2 = Width;
				num3 = 0f;
				num4 = Height;
				num5 = 0.02f;
			}
			if (!o.Overlay && num5 > BillboardDistance)
			{
				return p;
			}
			p.C0 = Math.Max(0, (int)Math.Floor(num));
			p.C1 = Math.Min(Width, (int)Math.Ceiling(num2));
			p.R0 = Math.Max(0, (int)Math.Floor(num3));
			p.R1 = Math.Min(Height, (int)Math.Ceiling(num4));
			if (p.C0 >= p.C1 || p.R0 >= p.R1)
			{
				return p;
			}
			Vec3 a = new Vec3(_view.X, _view.Y, _view.Z) - o.Origin;
			p.Ox = Vec3.Dot(a, o.U) / o.SizeU;
			p.Oy = Vec3.Dot(a, o.V) / o.SizeV;
			p.Oz = Vec3.Dot(a, o.N) / o.SizeN;
			p.NearT = (o.Overlay ? 0.02f : 0.05f);
			p.DepthMin = num5;
			p.Coarse = !o.Overlay && Math.Min(o.SizeU, Math.Min(o.SizeV, o.SizeN)) * _focal / Math.Max(0.05f, num6) >= 2f;
			if (o.Emissive)
			{
				Tint3 shadeNn = new Tint3(1f, 1f, 1f);
				p.ShadeU = (p.ShadeUn = (p.ShadeV = (p.ShadeVn = (p.ShadeN = (p.ShadeNn = shadeNn)))));
			}
			else
			{
				float lift = 1f;
				p.Share = 1;
				if (_natural && !o.Overlay)
				{
					Vec3 vec2 = o.Origin + o.U * ((float)width * 0.5f * o.SizeU) + o.V * ((float)height * 0.5f * o.SizeV);
					float vis = ThingVis(vec2.X, vec2.Y, vec2.Z);
					p.Share = Share(vis);
					if (_landField)
					{
						lift = Lift(vis);
					}
				}
				p.ShadeU = FaceShade(o.U, o.Overlay, lift);
				p.ShadeUn = FaceShade(o.U * -1f, o.Overlay, lift);
				p.ShadeV = FaceShade(o.V, o.Overlay, lift);
				p.ShadeVn = FaceShade(o.V * -1f, o.Overlay, lift);
				p.ShadeN = FaceShade(o.N, o.Overlay, lift);
				p.ShadeNn = FaceShade(o.N * -1f, o.Overlay, lift);
			}
			p.Visible = true;
			if (!o.Overlay && !o.NoShadow && num5 < ShadowDistance)
			{
				PrepareShadow(ref p, o);
			}
			return p;
		}

		private void PrepareShadow(ref VoxelPrep p, VoxelObject o)
		{
			VoxelShape shape = o.Shape;
			Vec3 vec = (_natural ? Air.CastLight : _light);
			float z = o.Origin.Z;
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MaxValue;
			float num6 = 0f;
			for (int i = 0; i < 8; i++)
			{
				float num7 = (((i & 1) != 0) ? shape.Width : 0);
				float num8 = (((i & 2) != 0) ? shape.Height : 0);
				float num9 = (((i & 4) != 0) ? shape.Depth : (-shape.Depth));
				Vec3 vec2 = o.Origin + o.U * (num7 * o.SizeU) + o.V * (num8 * o.SizeV) + o.N * (num9 * o.SizeN);
				for (int j = 0; j < 2; j++)
				{
					Vec3 vec3 = ((j == 0) ? vec2 : (vec2 - vec * ((vec2.Z - z) / vec.Z)));
					float col;
					float row;
					float t;
					float scale;
					bool num10 = ProjectRel(vec3.X - _view.X, vec3.Y - _view.Y, vec3.Z - _view.Z, 0.05f, out col, out row, out t, out scale);
					if (t < num5)
					{
						num5 = t;
					}
					if (t > num6)
					{
						num6 = t;
					}
					if (!num10)
					{
						num = 0f;
						num2 = Width;
						num3 = 0f;
						num4 = Height;
						continue;
					}
					if (col < num)
					{
						num = col;
					}
					if (col > num2)
					{
						num2 = col;
					}
					if (row < num3)
					{
						num3 = row;
					}
					if (row > num4)
					{
						num4 = row;
					}
				}
			}
			p.SC0 = Math.Max(0, (int)Math.Floor(num));
			p.SC1 = Math.Min(Width, (int)Math.Ceiling(num2));
			p.SR0 = Math.Max(0, (int)Math.Floor(num3));
			p.SR1 = Math.Min(Height, (int)Math.Ceiling(num4));
			p.Sdx = Vec3.Dot(vec, o.U) / o.SizeU;
			p.Sdy = Vec3.Dot(vec, o.V) / o.SizeV;
			p.Sdz = Vec3.Dot(vec, o.N) / o.SizeN;
			p.Mask = ((o.V.Z > 0.99f) ? MaskFor(o, p.Sdx, p.Sdy, p.Sdz) : null);
			p.SDepthMin = Math.Max(0f, num5) - 0.5f;
			p.SDepthMax = num6 + 0.5f;
			p.Shadow = p.SC0 < p.SC1 && p.SR0 < p.SR1;
		}

		private void DrawVoxelShadow(int index, int x0, int x1)
		{
			VoxelPrep voxelPrep = _prep[index];
			if (!voxelPrep.Visible || !voxelPrep.Shadow)
			{
				return;
			}
			int num = Math.Max(x0, voxelPrep.SC0);
			int num2 = Math.Min(x1, voxelPrep.SC1);
			if (num >= num2)
			{
				return;
			}
			VoxelObject voxelObject = _voxels[index];
			VoxelShape shape = voxelObject.Shape;
			Tint3 shadowTint = _shadowTint;
			float num3 = 1f / voxelObject.SizeU;
			float num4 = 1f / voxelObject.SizeV;
			float num5 = 1f / voxelObject.SizeN;
			bool tilted = _tilted;
			bool natural = _natural;
			for (int i = num; i < num2; i++)
			{
				float num6 = _scrU[i];
				float num7 = num6;
				float num8 = _fx + _rx * num7;
				float num9 = _fy + _ry * num7;
				float num10 = (num8 * voxelObject.U.X + num9 * voxelObject.U.Y) * num3;
				float num11 = voxelObject.U.Z * num3;
				float num12 = (num8 * voxelObject.V.X + num9 * voxelObject.V.Y) * num4;
				float num13 = voxelObject.V.Z * num4;
				float num14 = (num8 * voxelObject.N.X + num9 * voxelObject.N.Y) * num5;
				float num15 = voxelObject.N.Z * num5;
				for (int j = voxelPrep.SR0; j < voxelPrep.SR1; j++)
				{
					int num16 = (Height - 1 - j) * Width + i;
					float num17 = Depth[num16];
					if (num17 < voxelPrep.SDepthMin || num17 > voxelPrep.SDepthMax)
					{
						continue;
					}
					float num18 = _rowK[j];
					if (tilted)
					{
						num7 = num6 * _rowD[j];
						float num19 = _fx + _rx * num7;
						num9 = _fy + _ry * num7;
						num10 = (num19 * voxelObject.U.X + num9 * voxelObject.U.Y) * num3;
						num12 = (num19 * voxelObject.V.X + num9 * voxelObject.V.Y) * num4;
						num14 = (num19 * voxelObject.N.X + num9 * voxelObject.N.Y) * num5;
					}
					if (natural && _sunlit[num16] == 0)
					{
						continue;
					}
					float num20 = voxelPrep.Ox + (num10 + num11 * num18) * num17;
					float num21 = voxelPrep.Oy + (num12 + num13 * num18) * num17;
					float num22 = voxelPrep.Oz + (num14 + num15 * num18) * num17;
					ShadowMask mask = voxelPrep.Mask;
					float tHit;
					int cell;
					int axis;
					int step;
					if (mask != null)
					{
						if (num21 >= mask.Top)
						{
							continue;
						}
						int num23 = (int)(num20 - mask.Kx * num21 - mask.U0);
						int num24 = (int)(num22 - mask.Kz * num21 - mask.N0);
						if (num23 >= 0 && num24 >= 0 && num23 < mask.W && num24 < mask.H && mask.Cells[num24 * mask.W + num23] != 0)
						{
							if (natural)
							{
								ShadeShare(num16, shadowTint);
							}
							else
							{
								Pixels[num16] = TerrainCache.Tint(Pixels[num16], shadowTint.R, shadowTint.G, shadowTint.B);
							}
						}
					}
					else if (Trace(shape, num20, num21, num22, voxelPrep.Sdx, voxelPrep.Sdy, voxelPrep.Sdz, 0.02f, out tHit, out cell, out axis, out step))
					{
						if (natural)
						{
							ShadeShare(num16, shadowTint);
						}
						else
						{
							Pixels[num16] = TerrainCache.Tint(Pixels[num16], shadowTint.R, shadowTint.G, shadowTint.B);
						}
					}
				}
			}
		}

		private void DrawVoxel(int index, int x0, int x1, float[] depthBuffer, bool fogged, ref int pick, ref float pickDepth)
		{
			VoxelPrep p = _prep[index];
			if (!p.Visible)
			{
				return;
			}
			int num = Math.Max(x0, p.C0);
			int num2 = Math.Min(x1, p.C1);
			if (num >= num2)
			{
				return;
			}
			VoxelObject o = _voxels[index];
			VoxelShape shape = o.Shape;
			if (DebugSkip != 0 && !o.Overlay && (((DebugSkip & 1) != 0 && shape.Speckle != null) || ((DebugSkip & 2) != 0 && shape.Grid != null)))
			{
				return;
			}
			if (!p.Coarse || num2 - num < 3 || NoPairs)
			{
				for (int i = num; i < num2; i++)
				{
					VoxelColumn(ref p, ref o, i, depthBuffer, fogged, null, ref pick, ref pickDepth);
				}
				return;
			}
			int num3 = Math.Max(p.R1 - p.R0, Height);
			if (_hitsA == null || _hitsA.Length < num3)
			{
				_hitsA = new VoxelHit[num3];
				_hitsB = new VoxelHit[num3];
			}
			VoxelHit[] array = null;
			VoxelHit[] array2 = _hitsA;
			int num4 = num;
			for (int j = num; j < num2; j += 2)
			{
				VoxelColumn(ref p, ref o, j, depthBuffer, fogged, array2, ref pick, ref pickDepth);
				if (array != null)
				{
					FillColumn(ref p, ref o, j - 1, depthBuffer, fogged, array, array2, ref pick, ref pickDepth);
				}
				array = array2;
				array2 = ((array2 == _hitsA) ? _hitsB : _hitsA);
				num4 = j;
			}
			if (num4 < num2 - 1)
			{
				VoxelColumn(ref p, ref o, num2 - 1, depthBuffer, fogged, null, ref pick, ref pickDepth);
			}
		}

		private void VoxelColumn(ref VoxelPrep p, ref VoxelObject o, int c, float[] depthBuffer, bool fogged, VoxelHit[] hits, ref int pick, ref float pickDepth)
		{
			for (int i = p.R0; i < p.R1; i++)
			{
				int num = (Height - 1 - i) * Width + c;
				if (depthBuffer[num] <= p.DepthMin)
				{
					if (hits != null)
					{
						hits[i - p.R0].Cell = -2;
					}
					continue;
				}
				VoxelPixel(ref p, ref o, c, i, num, depthBuffer, fogged, out var hit, ref pick, ref pickDepth);
				if (hits != null)
				{
					hits[i - p.R0] = hit;
				}
			}
		}

		private void FillColumn(ref VoxelPrep p, ref VoxelObject o, int c, float[] depthBuffer, bool fogged, VoxelHit[] left, VoxelHit[] right, ref int pick, ref float pickDepth)
		{
			int num = Width / 2;
			int num2 = Height / 2;
			for (int i = p.R0; i < p.R1; i++)
			{
				int num3 = (Height - 1 - i) * Width + c;
				if (depthBuffer[num3] <= p.DepthMin)
				{
					continue;
				}
				VoxelHit voxelHit = left[i - p.R0];
				VoxelHit voxelHit2 = right[i - p.R0];
				if (voxelHit.Cell == -1 && voxelHit2.Cell == -1)
				{
					continue;
				}
				if (voxelHit.Cell >= 0 && voxelHit.Cell == voxelHit2.Cell && voxelHit.Face == voxelHit2.Face)
				{
					float num4 = (voxelHit.T + voxelHit2.T) * 0.5f;
					if (!(num4 >= depthBuffer[num3]))
					{
						Pixels[num3] = voxelHit2.Color;
						depthBuffer[num3] = num4;
						if (fogged && _natural)
						{
							_sunlit[num3] = (byte)((!o.Emissive) ? p.Share : 0);
						}
						if (fogged)
						{
							_actorMask[num3] = (IsActorTag(o.Tag) ? ((byte)1) : ((byte)0));
						}
						if (c == num && i == num2)
						{
							pick = o.Tag;
							pickDepth = num4;
						}
					}
				}
				else
				{
					VoxelPixel(ref p, ref o, c, i, num3, depthBuffer, fogged, out var _, ref pick, ref pickDepth);
				}
			}
		}

		private void VoxelPixel(ref VoxelPrep p, ref VoxelObject o, int c, int r, int idx, float[] depthBuffer, bool fogged, out VoxelHit hit, ref int pick, ref float pickDepth)
		{
			hit = new VoxelHit
			{
				Cell = -1
			};
			VoxelShape shape = o.Shape;
			float num = _scrU[c] * _rowD[r];
			float num2 = _fx + _rx * num;
			float num3 = _fy + _ry * num;
			float num4 = _rowK[r];
			float num5 = (num2 * o.U.X + num3 * o.U.Y + o.U.Z * num4) / o.SizeU;
			float num6 = (num2 * o.V.X + num3 * o.V.Y + o.V.Z * num4) / o.SizeV;
			float num7 = (num2 * o.N.X + num3 * o.N.Y + o.N.Z * num4) / o.SizeN;
			if (!Trace(shape, p.Ox, p.Oy, p.Oz, num5, num6, num7, p.NearT, out var tHit, out var cell, out var axis, out var step))
			{
				return;
			}
			Tint3 tint;
			switch (axis)
			{
			case 0:
				tint = ((step > 0) ? p.ShadeUn : p.ShadeU);
				break;
			case 1:
				tint = ((step > 0) ? p.ShadeVn : p.ShadeV);
				break;
			default:
				tint = ((step > 0) ? p.ShadeNn : p.ShadeN);
				break;
			}
			int c2 = (int)((shape.Grid != null) ? shape.Grid[cell] : shape.Colors[cell]) | -16777216;
			byte[] speckle = shape.Speckle;
			if (speckle != null && shape.Grid == null)
			{
				int num8 = speckle[cell];
				byte b = (byte)((shape.Leaves != null) ? shape.Leaves[cell] : 0);
				if (b != 0 && tHit < 11f)
				{
					float num9 = p.Ox + num5 * tHit;
					float num10 = p.Oy + num6 * tHit;
					float num11 = p.Oz + num7 * tHit;
					float num12 = ((axis == 0) ? num11 : num9);
					float num13 = ((axis == 1) ? num11 : num10);
					int num14 = ((int)Math.Floor(num12 * 4f) + cell * 3) & 3;
					int num15 = ((int)Math.Floor(num13 * 4f) + (cell >> 1)) & 3;
					num8 = num8 * ((b == 1) ? LeafTexture[(num15 << 2) | num14] : BarkTexture[num14]) >> 7;
				}
				if (num8 != 128)
				{
					tint.R = tint.R * num8 >> 7;
					tint.G = tint.G * num8 >> 7;
					tint.B = tint.B * num8 >> 7;
				}
			}
			uint num16 = TerrainCache.Tint((uint)c2, tint.R, tint.G, tint.B);
			if (o.Glow > 0f && shape.Glow != null && shape.Grid == null && shape.Glow[cell] != 0)
			{
				num16 = TerrainCache.Lerp(num16, WindowLit, o.Glow);
			}
			if (o.Multiply != 0)
			{
				num16 = TerrainCache.Multiply(num16, o.Multiply);
			}
			float num17 = (float)(o.Flash >> 24) / 255f;
			if (num17 > 0f)
			{
				num16 = TerrainCache.Lerp(num16, o.Flash | 0xFF000000u, num17);
			}
			if (fogged)
			{
				float num18 = Haze(tHit, _view.Z + num4 * tHit);
				if (o.Emissive)
				{
					num18 *= 0.4f;
				}
				if (IsActorTag(o.Tag))
				{
					num18 *= ActorHaze;
				}
				if (num18 > 0.002f)
				{
					num16 = TerrainCache.Lerp(num16, _scrFog[c], num18);
				}
			}
			hit = new VoxelHit
			{
				Cell = cell,
				Face = axis * 2 + ((step > 0) ? 1 : 0),
				T = tHit,
				Color = num16
			};
			if (!(tHit >= depthBuffer[idx]))
			{
				Pixels[idx] = num16;
				if (fogged)
				{
					_sunlit[idx] = (byte)((!o.Emissive) ? p.Share : 0);
				}
				if (fogged)
				{
					_actorMask[idx] = (IsActorTag(o.Tag) ? ((byte)1) : ((byte)0));
				}
				depthBuffer[idx] = tHit;
				if (c == Width / 2 && r == Height / 2)
				{
					pick = o.Tag;
					pickDepth = tHit;
				}
			}
		}

		private Tint3 FaceShade(Vec3 normal, bool overlay, float lift = 1f)
		{
			Atmosphere air = Air;
			float num = Math.Max(0f, Vec3.Dot(normal, _light)) + (1f - Math.Max(0f, normal.Z)) * 0.12f;
			if (lift != 1f)
			{
				num *= lift;
			}
			float num2 = 0.72f + 0.28f * normal.Z;
			Tint3 result = new Tint3(air.AmbR * num2 + air.LightR * num, air.AmbG * num2 + air.LightG * num, air.AmbB * num2 + air.LightB * num);
			if (overlay)
			{
				result.R += _eyeLight.R;
				result.G += _eyeLight.G;
				result.B += _eyeLight.B;
			}
			return result;
		}

		private static bool Trace(VoxelShape s, float ox, float oy, float oz, float dx, float dy, float dz, float near, out float tHit, out int cell, out int axis, out int step)
		{
			tHit = 0f;
			cell = -1;
			axis = 2;
			step = 1;
			if (s.Grid != null)
			{
				return TraceGrid(s, ox, oy, oz, dx, dy, dz, near, out tHit, out cell, out axis, out step);
			}
			int width = s.Width;
			int height = s.Height;
			int depth = s.Depth;
			float num = ((dx != 0f) ? (1f / dx) : 0f);
			float num2 = ((dy != 0f) ? (1f / dy) : 0f);
			float num3 = ((dz != 0f) ? (1f / dz) : 0f);
			float tmin = near;
			float tmax = 1E+09f;
			int enterAxis = -1;
			if (!Slab(ox, dx, num, 0f, width, 0, ref tmin, ref tmax, ref enterAxis))
			{
				return false;
			}
			if (!Slab(oy, dy, num2, 0f, height, 1, ref tmin, ref tmax, ref enterAxis))
			{
				return false;
			}
			if (!Slab(oz, dz, num3, -depth, depth, 2, ref tmin, ref tmax, ref enterAxis))
			{
				return false;
			}
			float num4 = tmin + 0.0001f;
			int num5 = (int)Math.Floor(ox + dx * num4);
			int num6 = (int)Math.Floor(oy + dy * num4);
			if (num5 < 0)
			{
				num5 = 0;
			}
			else if (num5 >= width)
			{
				num5 = width - 1;
			}
			if (num6 < 0)
			{
				num6 = 0;
			}
			else if (num6 >= height)
			{
				num6 = height - 1;
			}
			int num7 = ((dx > 0f) ? 1 : (-1));
			int num8 = ((dy > 0f) ? 1 : (-1));
			float num9 = ((dx != 0f) ? Math.Abs(num) : float.MaxValue);
			float num10 = ((dy != 0f) ? Math.Abs(num2) : float.MaxValue);
			float num11 = ((dx != 0f) ? (((float)((dx > 0f) ? (num5 + 1) : num5) - ox) * num) : float.MaxValue);
			float num12 = ((dy != 0f) ? (((float)((dy > 0f) ? (num6 + 1) : num6) - oy) * num2) : float.MaxValue);
			int num13 = ((enterAxis < 0) ? 2 : enterAxis);
			int num14;
			switch (num13)
			{
			default:
				num14 = ((dz > 0f) ? 1 : (-1));
				break;
			case 1:
				num14 = num8;
				break;
			case 0:
				num14 = num7;
				break;
			}
			int num15 = num14;
			float num16 = tmin;
			byte[] thickness = s.Thickness;
			sbyte[] offset = s.Offset;
			byte[] mirror = s.Mirror;
			byte[] emptyReach = s.EmptyReach;
			int num17 = width + height + 2;
			for (int i = 0; i < num17; i++)
			{
				float num18 = ((num11 < num12) ? num11 : num12);
				if (num18 > tmax)
				{
					num18 = tmax;
				}
				int num19 = num6 * width + num5;
				int num20 = thickness[num19];
				if (num20 == 0 && emptyReach[num19] >= 3 && !NoEmptySkip)
				{
					int num21 = emptyReach[num19] - 1;
					float num22 = ((dx != 0f) ? (((float)((dx > 0f) ? (num5 + num21 + 1) : (num5 - num21)) - ox) * num) : float.MaxValue);
					float num23 = ((dy != 0f) ? (((float)((dy > 0f) ? (num6 + num21 + 1) : (num6 - num21)) - oy) * num2) : float.MaxValue);
					float num24 = ((num22 < num23) ? num22 : num23);
					if (num24 >= tmax)
					{
						return false;
					}
					if (num24 > num16)
					{
						int num25 = (int)Math.Floor(ox + dx * (num24 + 0.0001f));
						int num26 = (int)Math.Floor(oy + dy * (num24 + 0.0001f));
						if (num25 < 0 || num25 >= width || num26 < 0 || num26 >= height)
						{
							return false;
						}
						num5 = num25;
						num6 = num26;
						num11 = ((dx != 0f) ? (((float)((dx > 0f) ? (num5 + 1) : num5) - ox) * num) : float.MaxValue);
						num12 = ((dy != 0f) ? (((float)((dy > 0f) ? (num6 + 1) : num6) - oy) * num2) : float.MaxValue);
						num16 = num24;
						num13 = ((!(num22 < num23)) ? 1 : 0);
						num15 = ((num13 == 0) ? num7 : num8);
						continue;
					}
				}
				if (num20 > 0)
				{
					int num27 = ((offset != null) ? offset[num19] : 0);
					float t;
					bool zFace;
					bool flag = Band(oz, dz, num3, num27 - num20, num27 + num20, num16, num18, out t, out zFace);
					if (mirror != null && mirror[num19] != 0 && Band(oz, dz, num3, -num27 - num20, -num27 + num20, num16, num18, out var t2, out var zFace2) && (!flag || t2 < t))
					{
						flag = true;
						t = t2;
						zFace = zFace2;
					}
					if (flag)
					{
						tHit = t;
						cell = num19;
						if (zFace)
						{
							axis = 2;
							step = ((dz > 0f) ? 1 : (-1));
						}
						else
						{
							axis = num13;
							step = num15;
						}
						return true;
					}
				}
				if (num18 >= tmax)
				{
					return false;
				}
				if (num11 < num12)
				{
					num5 += num7;
					if (num5 < 0 || num5 >= width)
					{
						return false;
					}
					num16 = num11;
					num11 += num9;
					num13 = 0;
					num15 = num7;
				}
				else
				{
					num6 += num8;
					if (num6 < 0 || num6 >= height)
					{
						return false;
					}
					num16 = num12;
					num12 += num10;
					num13 = 1;
					num15 = num8;
				}
			}
			return false;
		}

		private static bool TraceGrid(VoxelShape s, float ox, float oy, float oz, float dx, float dy, float dz, float near, out float tHit, out int cell, out int axis, out int step)
		{
			tHit = 0f;
			cell = -1;
			axis = 2;
			step = 1;
			int width = s.Width;
			int height = s.Height;
			int depth = s.Depth;
			int num = depth * 2;
			float num2 = ((dx != 0f) ? (1f / dx) : 0f);
			float num3 = ((dy != 0f) ? (1f / dy) : 0f);
			float num4 = ((dz != 0f) ? (1f / dz) : 0f);
			float tmin = near;
			float tmax = 1E+09f;
			int enterAxis = -1;
			if (!Slab(ox, dx, num2, 0f, width, 0, ref tmin, ref tmax, ref enterAxis))
			{
				return false;
			}
			if (!Slab(oy, dy, num3, 0f, height, 1, ref tmin, ref tmax, ref enterAxis))
			{
				return false;
			}
			if (!Slab(oz, dz, num4, -depth, depth, 2, ref tmin, ref tmax, ref enterAxis))
			{
				return false;
			}
			float num5 = tmin + 0.0001f;
			int num6 = (int)Math.Floor(ox + dx * num5);
			int num7 = (int)Math.Floor(oy + dy * num5);
			int num8 = (int)Math.Floor(oz + dz * num5);
			if (num6 < 0)
			{
				num6 = 0;
			}
			else if (num6 >= width)
			{
				num6 = width - 1;
			}
			if (num7 < 0)
			{
				num7 = 0;
			}
			else if (num7 >= height)
			{
				num7 = height - 1;
			}
			if (num8 < -depth)
			{
				num8 = -depth;
			}
			else if (num8 >= depth)
			{
				num8 = depth - 1;
			}
			int num9 = ((dx > 0f) ? 1 : (-1));
			int num10 = ((dy > 0f) ? 1 : (-1));
			int num11 = ((dz > 0f) ? 1 : (-1));
			float num12 = ((dx != 0f) ? Math.Abs(num2) : float.MaxValue);
			float num13 = ((dy != 0f) ? Math.Abs(num3) : float.MaxValue);
			float num14 = ((dz != 0f) ? Math.Abs(num4) : float.MaxValue);
			float num15 = ((dx != 0f) ? (((float)((dx > 0f) ? (num6 + 1) : num6) - ox) * num2) : float.MaxValue);
			float num16 = ((dy != 0f) ? (((float)((dy > 0f) ? (num7 + 1) : num7) - oy) * num3) : float.MaxValue);
			float num17 = ((dz != 0f) ? (((float)((dz > 0f) ? (num8 + 1) : num8) - oz) * num4) : float.MaxValue);
			int num18 = ((enterAxis < 0) ? 2 : enterAxis);
			int num19;
			switch (num18)
			{
			default:
				num19 = num11;
				break;
			case 1:
				num19 = num10;
				break;
			case 0:
				num19 = num9;
				break;
			}
			int num20 = num19;
			float num21 = tmin;
			uint[] grid = s.Grid;
			int num22 = width + height + num + 3;
			for (int i = 0; i < num22; i++)
			{
				int num23 = (num7 * width + num6) * num + num8 + depth;
				if (grid[num23] >> 24 != 0)
				{
					tHit = num21;
					cell = num23;
					axis = num18;
					step = num20;
					return true;
				}
				if (num15 < num16 && num15 < num17)
				{
					num6 += num9;
					if (num6 < 0 || num6 >= width)
					{
						return false;
					}
					num21 = num15;
					num15 += num12;
					num18 = 0;
					num20 = num9;
				}
				else if (num16 < num17)
				{
					num7 += num10;
					if (num7 < 0 || num7 >= height)
					{
						return false;
					}
					num21 = num16;
					num16 += num13;
					num18 = 1;
					num20 = num10;
				}
				else
				{
					num8 += num11;
					if (num8 < -depth || num8 >= depth)
					{
						return false;
					}
					num21 = num17;
					num17 += num14;
					num18 = 2;
					num20 = num11;
				}
				if (num21 > tmax)
				{
					return false;
				}
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Band(float oz, float dz, float iz0, float lo, float hi, float tEntry, float tExit, out float t, out bool zFace)
		{
			float num = oz + dz * tEntry;
			if (num >= lo && num <= hi)
			{
				t = tEntry;
				zFace = false;
				return true;
			}
			zFace = true;
			if (dz != 0f)
			{
				t = (((num < lo) ? lo : hi) - oz) * iz0;
				if (t >= tEntry && t <= tExit)
				{
					return true;
				}
			}
			t = 0f;
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Slab(float o, float d, float invD, float lo, float hi, int axis, ref float tmin, ref float tmax, ref int enterAxis)
		{
			if (d == 0f)
			{
				if (o >= lo)
				{
					return o <= hi;
				}
				return false;
			}
			float num = (lo - o) * invD;
			float num2 = (hi - o) * invD;
			if (num > num2)
			{
				float num3 = num;
				num = num2;
				num2 = num3;
			}
			if (num > tmin)
			{
				tmin = num;
				enterAxis = axis;
			}
			if (num2 < tmax)
			{
				tmax = num2;
			}
			return tmin <= tmax;
		}

		private void DrawFacing(ref Billboard b, int x0, int x1, ref int pick, ref float pickDepth)
		{
			SpriteImage image = b.Image;
			float num = b.X - _view.X;
			float num2 = b.Y - _view.Y;
			float num3 = num * _fx + num2 * _fy;
			if (num3 < 0.2f || num3 > (b.Far ? MaxDistance : BillboardDistance))
			{
				return;
			}
			if (_tilted)
			{
				float num4 = num * _rx + num2 * _ry - b.Width * (b.Flip ? (1f - image.PivotX) : image.PivotX);
				float val = num3 * _lens.Cq + (b.Z + b.Height * 0.5f - _view.Z) * _lens.Sq;
				float num5 = _focal / Math.Max(0.05f, val);
				if (!(b.Width * num5 < 0.35f) && !(b.Height * num5 < 0.35f))
				{
					float num6 = Haze(num3, b.Z + b.Height * 0.5f);
					if (b.Emissive)
					{
						num6 *= 0.4f;
					}
					if (IsActorTag(b.Tag))
					{
						num6 *= ActorHaze;
					}
					DrawQuad(ref b, num4, num3, num4 + b.Width, num3, b.Flip, num6, x0, x1, ref pick, ref pickDepth);
				}
				return;
			}
			float num7 = _focal / num3;
			float num8 = b.Width * num7;
			float num9 = b.Height * num7;
			if (num8 < 0.35f || num9 < 0.35f)
			{
				return;
			}
			float num10 = (float)Width * 0.5f + (num * _rx + num2 * _ry) * num7;
			float num11 = (b.Flip ? (1f - image.PivotX) : image.PivotX);
			float num12 = num10 - num8 * num11;
			float num13 = _horizon - (b.Z - _view.Z) * num7;
			int num14 = Math.Max(x0, (int)Math.Floor(num12));
			int num15 = Math.Min(x1, (int)Math.Ceiling(num12 + num8));
			if (num14 >= num15 || num13 - num9 >= (float)Height || num13 < 0f)
			{
				return;
			}
			float num16 = Haze(num3, b.Z + b.Height * 0.5f);
			if (b.Emissive)
			{
				num16 *= 0.4f;
			}
			if (IsActorTag(b.Tag))
			{
				num16 *= ActorHaze;
			}
			int width = image.Width;
			byte share;
			Tint3 lit = SpriteLight(ref b, out share);
			for (int i = num14; i < num15; i++)
			{
				int num17 = (int)(((float)i + 0.5f - num12) / num8 * (float)width);
				if (num17 >= 0 && num17 < width)
				{
					if (b.Flip)
					{
						num17 = width - 1 - num17;
					}
					DrawColumn(ref b, i, num17, num3, num13, num9, num16, lit, share, ref pick, ref pickDepth);
				}
			}
		}

		private void DrawPlane(ref Billboard b, float ax, float ay, float bx, float by, int x0, int x1, ref int pick, ref float pickDepth)
		{
			float num = (ax - _view.X) * _rx + (ay - _view.Y) * _ry;
			float num2 = (ax - _view.X) * _fx + (ay - _view.Y) * _fy;
			float num3 = (bx - _view.X) * _rx + (by - _view.Y) * _ry;
			float num4 = (bx - _view.X) * _fx + (by - _view.Y) * _fy;
			if ((num2 < 0.12f && num4 < 0.12f) || (num2 > BillboardDistance && num4 > BillboardDistance))
			{
				return;
			}
			if (_tilted)
			{
				DrawQuad(ref b, num, num2, num3, num4, flip: false, -1f, x0, x1, ref pick, ref pickDepth);
				return;
			}
			float num5 = 0f;
			float num6 = 1f;
			if (num2 < 0.12f)
			{
				float num7 = (0.12f - num2) / (num4 - num2);
				num += (num3 - num) * num7;
				num2 = 0.12f;
				num5 = num7;
			}
			else if (num4 < 0.12f)
			{
				float num8 = (0.12f - num4) / (num2 - num4);
				num3 += (num - num3) * num8;
				num4 = 0.12f;
				num6 = 1f - num8;
			}
			float num9 = (float)Width * 0.5f + num / num2 * _focal;
			float num10 = (float)Width * 0.5f + num3 / num4 * _focal;
			if (Math.Abs(num10 - num9) < 0.05f)
			{
				return;
			}
			int num11 = Math.Max(x0, (int)Math.Floor(Math.Min(num9, num10)));
			int num12 = Math.Min(x1, (int)Math.Ceiling(Math.Max(num9, num10)));
			float num13 = 1f / num2;
			float num14 = 1f / num4;
			int width = b.Image.Width;
			float z = b.Z + b.Height * 0.5f;
			if (num11 >= num12)
			{
				return;
			}
			byte share;
			Tint3 lit = SpriteLight(ref b, out share);
			for (int i = num11; i < num12; i++)
			{
				float num15 = ((float)i + 0.5f - num9) / (num10 - num9);
				if (!(num15 < 0f) && !(num15 > 1f))
				{
					float num16 = num13 + (num14 - num13) * num15;
					float num17 = 1f / num16;
					int num18 = (int)((num5 * num13 + (num6 * num14 - num5 * num13) * num15) * num17 * (float)width);
					if (num18 < 0)
					{
						num18 = 0;
					}
					else if (num18 >= width)
					{
						num18 = width - 1;
					}
					float num19 = _focal * num16;
					float bottom = _horizon - (b.Z - _view.Z) * num19;
					DrawColumn(ref b, i, num18, num17, bottom, b.Height * num19, Haze(num17, z), lit, share, ref pick, ref pickDepth);
				}
			}
		}

		private void DrawQuad(ref Billboard b, float aSide, float aDepth, float bSide, float bDepth, bool flip, float fog, int x0, int x1, ref int pick, ref float pickDepth)
		{
			if (aDepth < 0.12f && bDepth < 0.12f)
			{
				return;
			}
			SpriteImage image = b.Image;
			float z = _view.Z;
			float num = b.Z - z;
			float num2 = b.Z + b.Height - z;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			float num5 = float.MaxValue;
			float num6 = float.MinValue;
			bool flag = false;
			for (int i = 0; i < 4; i++)
			{
				if (flag)
				{
					break;
				}
				float num7 = (((i & 1) == 0) ? aSide : bSide);
				float num8 = (((i & 1) == 0) ? aDepth : bDepth);
				float num9 = (((i & 2) == 0) ? num : num2);
				float num10 = num8 * _lens.Cq + num9 * _lens.Sq;
				if (num10 < 0.05f)
				{
					flag = true;
					break;
				}
				float num11 = _focal / num10;
				float num12 = (float)Width * 0.5f + num7 * num11;
				float num13 = _horizon - (num9 * _lens.Cq - num8 * _lens.Sq) * num11;
				if (num12 < num3)
				{
					num3 = num12;
				}
				if (num12 > num4)
				{
					num4 = num12;
				}
				if (num13 < num5)
				{
					num5 = num13;
				}
				if (num13 > num6)
				{
					num6 = num13;
				}
			}
			int num14 = x0;
			int num15 = x1;
			int num16 = 0;
			int num17 = Height;
			if (!flag)
			{
				num14 = Math.Max(x0, (int)Math.Floor(num3));
				num15 = Math.Min(x1, (int)Math.Ceiling(num4) + 1);
				num16 = Math.Max(0, (int)Math.Floor(num5));
				num17 = Math.Min(Height, (int)Math.Ceiling(num6) + 1);
			}
			if (num14 >= num15 || num16 >= num17)
			{
				return;
			}
			float num18 = bSide - aSide;
			float num19 = bDepth - aDepth;
			int width = image.Width;
			int height = image.Height;
			int width2 = Width;
			int height2 = Height;
			float num20 = (float)height / Math.Max(0.0001f, b.Height);
			uint[] pixels = image.Pixels;
			float num21 = (float)(b.Flash >> 24) / 255f;
			int num22 = width2 / 2;
			int num23 = height2 / 2;
			byte share;
			Tint3 tint = SpriteLight(ref b, out share);
			bool emissive = b.Emissive;
			bool flag2 = IsActorTag(b.Tag);
			float num24 = (float)(int)b.Alpha / 255f;
			float z2 = b.Z + b.Height * 0.5f;
			float num25 = (b.Far ? MaxDistance : BillboardDistance);
			for (int j = num14; j < num15; j++)
			{
				float num26 = _scrU[j];
				uint b2 = _scrFog[j];
				for (int k = num16; k < num17; k++)
				{
					float num27 = num26 * _rowD[k];
					float num28 = num18 - num27 * num19;
					if (Math.Abs(num28) < 1E-07f)
					{
						continue;
					}
					float num29 = (num27 * aDepth - aSide) / num28;
					if (num29 < 0f || num29 > 1f)
					{
						continue;
					}
					float num30 = aDepth + num29 * num19;
					if (num30 < 0.12f || num30 > num25)
					{
						continue;
					}
					float num31 = _rowK[k] * num30;
					if (num31 < num || num31 >= num2)
					{
						continue;
					}
					int num32 = (int)(num29 * (float)width);
					if (num32 >= width)
					{
						num32 = width - 1;
					}
					if (flip)
					{
						num32 = width - 1 - num32;
					}
					int num33 = (int)((num31 - num) * num20);
					if (num33 < 0 || num33 >= height)
					{
						continue;
					}
					uint num34 = pixels[num33 * width + num32];
					uint num35 = num34 >> 24;
					if (num35 < 128 && (num24 <= 0f || num35 == 0))
					{
						continue;
					}
					int num36 = (height2 - 1 - k) * width2 + j;
					if (num30 >= Depth[num36])
					{
						continue;
					}
					float num37 = fog;
					if (num37 < 0f)
					{
						num37 = Haze(num30, z2);
						if (emissive)
						{
							num37 *= 0.4f;
						}
						if (flag2)
						{
							num37 *= ActorHaze;
						}
					}
					num34 |= 0xFF000000u;
					if (!emissive)
					{
						num34 = TerrainCache.Tint(num34, tint.R, tint.G, tint.B);
					}
					if (b.Multiply != 0)
					{
						num34 = TerrainCache.Multiply(num34, b.Multiply);
					}
					if (num21 > 0f)
					{
						num34 = TerrainCache.Lerp(num34, b.Flash | 0xFF000000u, num21);
					}
					if (num37 > 0.002f && !b.Additive)
					{
						num34 = TerrainCache.Lerp(num34, b2, num37);
					}
					if (num24 > 0f)
					{
						float num38 = ((num35 == 255) ? num24 : (num24 * (float)num35 * 0.003921569f));
						if (_vaporLayer)
						{
							num38 *= VaporPass(num36, num30);
						}
						if (b.Additive)
						{
							Pixels[num36] = BlastFx.AddScaled(Pixels[num36], num34, num38 * (1f - Math.Min(1f, num37)));
							continue;
						}
						Pixels[num36] = TerrainCache.Lerp(Pixels[num36], num34, num38);
						CoverSky(num36, num38);
					}
					else
					{
						Pixels[num36] = num34;
						_sunlit[num36] = (byte)((!emissive) ? share : 0);
						_actorMask[num36] = (flag2 ? ((byte)1) : ((byte)0));
						Depth[num36] = num30;
						if (j == num22 && k == num23)
						{
							pick = b.Tag;
							pickDepth = num30;
						}
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void DrawColumn(ref Billboard b, int c, int tx, float z, float bottom, float hpx, float fog, Tint3 lit, byte share, ref int pick, ref float pickDepth)
		{
			SpriteImage image = b.Image;
			int width = image.Width;
			int height = image.Height;
			int width2 = Width;
			int height2 = Height;
			int num = Math.Max(0, (int)Math.Floor(bottom - hpx));
			int num2 = Math.Min(height2, (int)Math.Ceiling(bottom));
			uint[] pixels = image.Pixels;
			float num3 = (float)(b.Flash >> 24) / 255f;
			int num4 = width2 / 2;
			int num5 = height2 / 2;
			uint b2 = _scrFog[c];
			bool emissive = b.Emissive;
			float num6 = (float)(int)b.Alpha / 255f;
			for (int i = num; i < num2; i++)
			{
				int num7 = (int)((bottom - ((float)i + 0.5f)) / hpx * (float)height);
				if (num7 < 0 || num7 >= height)
				{
					continue;
				}
				uint num8 = pixels[num7 * width + tx];
				uint num9 = num8 >> 24;
				if (num9 < 128 && (num6 <= 0f || num9 == 0))
				{
					continue;
				}
				int num10 = (height2 - 1 - i) * width2 + c;
				if (z >= Depth[num10])
				{
					continue;
				}
				num8 |= 0xFF000000u;
				if (!emissive)
				{
					num8 = TerrainCache.Tint(num8, lit.R, lit.G, lit.B);
				}
				if (b.Multiply != 0)
				{
					num8 = TerrainCache.Multiply(num8, b.Multiply);
				}
				if (num3 > 0f)
				{
					num8 = TerrainCache.Lerp(num8, b.Flash | 0xFF000000u, num3);
				}
				if (fog > 0.002f && !b.Additive)
				{
					num8 = TerrainCache.Lerp(num8, b2, fog);
				}
				if (num6 > 0f)
				{
					float num11 = ((num9 == 255) ? num6 : (num6 * (float)num9 * 0.003921569f));
					if (_vaporLayer)
					{
						num11 *= VaporPass(num10, z);
					}
					if (b.Additive)
					{
						Pixels[num10] = BlastFx.AddScaled(Pixels[num10], num8, num11 * (1f - Math.Min(1f, Math.Max(0f, fog))));
						continue;
					}
					Pixels[num10] = TerrainCache.Lerp(Pixels[num10], num8, num11);
					CoverSky(num10, num11);
				}
				else
				{
					Pixels[num10] = num8;
					_sunlit[num10] = (byte)((!emissive) ? share : 0);
					_actorMask[num10] = (IsActorTag(b.Tag) ? ((byte)1) : ((byte)0));
					Depth[num10] = z;
					if (c == num4 && i == num5)
					{
						pick = b.Tag;
						pickDepth = z;
					}
				}
			}
		}

		private Tint3 SpriteLight(ref Billboard b, out byte share)
		{
			if (!_natural || b.Alpha > 0)
			{
				share = 1;
				return _flatTint;
			}
			float vis = ThingVis(b.X, b.Y, b.Z + b.Height * 0.5f);
			share = Share(vis);
			if (!_landField)
			{
				return _flatTint;
			}
			return FlatLight(Lift(vis));
		}

		private void DrawFarProps()
		{
			FarPropsDrawn = 0;
			if (FarProps.Count == 0)
			{
				return;
			}
			if (Air.Indoors)
			{
				FarProps.Clear();
				return;
			}
			Tint3 flatTint = _flatTint;
			float fogDistance = FogDistance;
			for (int i = 0; i < FarProps.Count; i++)
			{
				FarProp f = FarProps[i];
				if (!Project(f.X, f.Y, f.Z, out var col, out var row, out var depth, out var scale) || depth > fogDistance)
				{
					continue;
				}
				float num = f.Height * scale;
				if (num < 0.7f)
				{
					continue;
				}
				int num2 = Math.Max(1, (int)(f.Width * 0.7f * scale + 0.5f));
				int num3 = Math.Max(1, (int)(num + 0.5f));
				if (col + (float)num2 < 0f || col - (float)num2 > (float)Width || row < 0f || row - (float)num3 > (float)Height)
				{
					continue;
				}
				float num4 = Haze(depth, f.Z + f.Height * 0.5f);
				if (num4 > 0.97f)
				{
					continue;
				}
				int num5 = (int)row;
				FarPropsDrawn++;
				Tint3 lit = flatTint;
				byte b = 1;
				if (_natural)
				{
					float vis = ThingVisBeside(f.X, f.Y, f.Z + f.Height * 0.5f, f.Width * 0.5f);
					b = Share(vis);
					if (_landField)
					{
						lit = FlatLight(Lift(vis));
					}
				}
				if (f.Look != null)
				{
					DrawFarLook(f, col, num5, num3, scale, depth, num4, lit, b);
					continue;
				}
				uint num6 = TerrainCache.Tint(f.Color | 0xFF000000u, lit.R, lit.G, lit.B);
				bool flag = num3 >= 4;
				int num7 = ((f.Tree && flag) ? Math.Max(1, (num3 + 2) / 5) : 0);
				int num8 = ((!f.Tree && flag) ? Math.Max(1, num3 * 2 / 5) : 0);
				uint num9 = TerrainCache.Tint(TrunkColor | 0xFF000000u, lit.R, lit.G, lit.B);
				for (int j = 0; j < num3; j++)
				{
					int num10 = num5 - num3 + j;
					if (num10 < 0 || num10 >= Height)
					{
						continue;
					}
					int num11 = num2;
					uint a;
					if (j >= num3 - num7)
					{
						num11 = Math.Max(1, num2 / 4);
						a = num9;
					}
					else if (!(f.Tree && flag))
					{
						a = ((j >= num8) ? ((j == 0 && num3 > 1) ? TerrainCache.Lerp(num6, uint.MaxValue, 0.1f) : ((num8 > 0) ? TerrainCache.Scale(num6, 1.1f) : num6)) : TerrainCache.Scale(num6, 0.72f + 0.1f * (float)j / (float)Math.Max(1, num8)));
					}
					else
					{
						float num12 = ((float)j + 0.5f) / (float)(num3 - num7);
						num11 = Math.Max(1, (int)((float)num2 * (0.4f + 0.6f * (float)Math.Sqrt(Math.Sin(Math.PI * (double)num12))) + 0.5f));
						a = TerrainCache.Scale(num6, 1.12f - 0.3f * num12);
					}
					int num13 = (int)(col - (float)num11 * 0.5f);
					for (int k = num13; k < num13 + num11; k++)
					{
						if (k >= 0 && k < Width)
						{
							int num14 = (Height - 1 - num10) * Width + k;
							if (!(depth >= Depth[num14]))
							{
								Pixels[num14] = TerrainCache.Lerp(a, _scrFog[k], num4);
								Depth[num14] = depth;
								_sunlit[num14] = b;
							}
						}
					}
				}
			}
			FarProps.Clear();
		}

		private void DrawFarLook(FarProp f, float col, int r1, int h, float inv, float depth, float haze, Tint3 lit, byte share)
		{
			FarLook look = f.Look;
			int num = look.LevelFor(h);
			int num2 = look.W[num];
			int num3 = look.H[num];
			uint[] array = look.Pixels[num];
			float num4 = col + look.Left * f.Width * inv;
			float num5 = col + look.Right * f.Width * inv;
			if (num5 - num4 < 1f)
			{
				float num6 = (num4 + num5) * 0.5f;
				num4 = num6 - 0.5f;
				num5 = num6 + 0.5f;
			}
			int num7 = Math.Max(0, (int)Math.Floor(num4));
			int num8 = Math.Min(Width, (int)Math.Ceiling(num5));
			float num9 = (float)num2 / (num5 - num4);
			for (int i = 0; i < h; i++)
			{
				int num10 = r1 - h + i;
				if (num10 < 0 || num10 >= Height)
				{
					continue;
				}
				int num11 = Math.Min(num3 - 1, (int)(((float)(h - 1 - i) + 0.5f) * (float)num3 / (float)h));
				for (int j = num7; j < num8; j++)
				{
					int num12 = (int)(((float)j + 0.5f - num4) * num9);
					if (num12 < 0 || num12 >= num2)
					{
						continue;
					}
					uint num13 = array[num11 * num2 + num12];
					if (num13 != 0)
					{
						int num14 = (Height - 1 - num10) * Width + j;
						if (!(depth >= Depth[num14]))
						{
							Pixels[num14] = TerrainCache.Lerp(TerrainCache.Tint(num13, lit.R, lit.G, lit.B), _scrFog[j], haze);
							Depth[num14] = depth;
							_sunlit[num14] = share;
						}
					}
				}
			}
		}

		private void ApplyScreenFlash()
		{
			int num = (int)(ScreenFlash >> 24);
			if (num > 0)
			{
				float flashK = (float)num / 255f;
				_flashK = flashK;
				For((Height + 15) / 16, FlashBand);
			}
		}

		private void FlashBand(int band)
		{
			float flashK = _flashK;
			int num = (int)ScreenFlash | -16777216;
			int num2 = num & 0xFF;
			int num3 = (int)(((uint)num >> 8) & 0xFF);
			int num4 = (int)(((uint)num >> 16) & 0xFF);
			uint[] pixels = Pixels;
			int val = pixels.Length;
			int width = Width;
			int num5 = band * 16 * width;
			int num6 = Math.Min(val, num5 + 16 * width);
			for (int i = num5; i < num6; i++)
			{
				uint num7 = pixels[i];
				int num8 = (int)(num7 & 0xFF);
				int num9 = (int)((num7 >> 8) & 0xFF);
				int num10 = (int)((num7 >> 16) & 0xFF);
				float num11 = (float)(num8 * 77 + num9 * 150 + num10 * 29) * 1.5318628E-05f;
				float num12 = flashK * (0.55f + 0.45f * num11);
				if (num12 > 1f)
				{
					num12 = 1f;
				}
				int num13 = (int)(num12 * 256f);
				int num14 = 256 - num13;
				num8 = num8 * num14 + num2 * num13 >> 8;
				num9 = num9 * num14 + num3 * num13 >> 8;
				num10 = num10 * num14 + num4 * num13 >> 8;
				pixels[i] = (uint)(num8 | (num9 << 8) | (num10 << 16) | -16777216);
			}
		}

		private void RenderMeshes(int stripes, int stripeW)
		{
			_pass = SplitClouds();
			_passDepth = Depth;
			_overlayPass = false;
			BuildTileMax();
			_portalPass = Backdrop != null && Backdrop.Length == Pixels.Length;
			if (_portalPass)
			{
				if (_portalMask.Length != Pixels.Length)
				{
					_portalMask = new byte[Pixels.Length];
				}
				else
				{
					Array.Clear(_portalMask, 0, _portalMask.Length);
				}
			}
			RenderPass(stripes, stripeW);
			PortalPixels = 0;
			if (_portalPass)
			{
				for (int i = 0; i < _portalMask.Length; i++)
				{
					PortalPixels += _portalMask[i];
				}
			}
		}

		private void RenderOverlayMeshes(int stripes, int stripeW)
		{
			if (OverlayMeshes.Count != 0)
			{
				_pass = OverlayMeshes;
				_passDepth = _overlayDepth;
				_overlayPass = true;
				int meshTriangles = MeshTriangles;
				float meshMs = MeshMs;
				float meshPrepMs = MeshPrepMs;
				float meshRasterMs = MeshRasterMs;
				float meshBinMs = MeshBinMs;
				RenderPass(stripes, stripeW);
				MeshTriangles = meshTriangles;
				MeshMs = meshMs;
				MeshPrepMs = meshPrepMs;
				MeshRasterMs = meshRasterMs;
				MeshBinMs = meshBinMs;
				_pass = Meshes;
				_passDepth = Depth;
				_overlayPass = false;
			}
		}

		private void RenderPass(int stripes, int stripeW)
		{
			int count = _pass.Count;
			MeshTriangles = 0;
			if (count == 0)
			{
				if (!_overlayPass && !_vaporPass && _natural)
				{
					_shadowReady = false;
					ShadowMs = 0f;
				}
				return;
			}
			_pass.Sort(NearestFirst);
			Stopwatch stopwatch = Stopwatch.StartNew();
			if (_vStart.Length < count + 1)
			{
				_vStart = new int[count * 2 + 1];
				_tStart = new int[count * 2 + 1];
				_tCount = new int[count * 2 + 1];
			}
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < count; i++)
			{
				_vStart[i] = num;
				_tStart[i] = num2;
				MeshPart[] parts = _pass[i].Model.Parts;
				foreach (MeshPart meshPart in parts)
				{
					num += meshPart.VertexCount;
					num2 += meshPart.TriangleCount * 2;
				}
			}
			_vStart[count] = num;
			if (_mv.Length < num)
			{
				_mv = new MVert[num + num / 2];
			}
			if (_st.Length < num2)
			{
				_st = new STri[num2 + num2 / 2];
			}
			if (UseParallel && count > 4)
			{
				For(count, PrepareMesh);
			}
			else
			{
				for (int k = 0; k < count; k++)
				{
					PrepareMesh(k);
				}
			}
			MeshPrepMs = (float)stopwatch.Elapsed.TotalMilliseconds;
			if (!_overlayPass && !_vaporPass)
			{
				BuildShadowMap();
			}
			float num3 = (float)stopwatch.Elapsed.TotalMilliseconds;
			if (_binStart.Length < stripes + 1)
			{
				_binStart = new int[stripes + 1];
				_binFill = new int[stripes + 1];
			}
			Array.Clear(_binStart, 0, _binStart.Length);
			if (_colStripe.Length != Width || _colStripeW != stripeW || _colStripes != stripes)
			{
				_colStripe = new byte[Width];
				for (int l = 0; l < Width; l++)
				{
					_colStripe[l] = (byte)Math.Min(stripes - 1, l / stripeW);
				}
				_colStripeW = stripeW;
				_colStripes = stripes;
			}
			if (_triStripes.Length < num2)
			{
				_triStripes = new ushort[_st.Length];
			}
			byte[] colStripe = _colStripe;
			ushort[] triStripes = _triStripes;
			int[] binStart = _binStart;
			STri[] st = _st;
			int num4 = 0;
			int num5 = 0;
			for (int m = 0; m < count; m++)
			{
				int n = _tStart[m];
				for (int num6 = n + _tCount[m]; n < num6; n++)
				{
					int num7 = colStripe[st[n].MinX];
					int num8 = colStripe[st[n].MaxX];
					triStripes[n] = (ushort)(num7 | (num8 << 8));
					for (int num9 = num7; num9 <= num8; num9++)
					{
						binStart[num9 + 1]++;
					}
					num4 += num8 - num7 + 1;
					num5++;
				}
			}
			MeshTriangles = num5;
			for (int num10 = 0; num10 < stripes; num10++)
			{
				binStart[num10 + 1] += binStart[num10];
			}
			if (_binItems.Length < num4)
			{
				_binItems = new int[num4 + num4 / 2];
			}
			Array.Copy(binStart, _binFill, stripes + 1);
			int[] binFill = _binFill;
			int[] binItems = _binItems;
			for (int num11 = 0; num11 < count; num11++)
			{
				int num12 = _tStart[num11];
				for (int num13 = num12 + _tCount[num11]; num12 < num13; num12++)
				{
					ushort num14 = triStripes[num12];
					int num15 = num14 >> 8;
					for (int num16 = num14 & 0xFF; num16 <= num15; num16++)
					{
						binItems[binFill[num16]++] = num12;
					}
				}
			}
			MeshBinMs = (float)stopwatch.Elapsed.TotalMilliseconds - num3;
			if (_vaporPass)
			{
				if (UseParallel && stripes > 1)
				{
					ForStripes(stripes, stripeW, RasterCloudStripe);
				}
				else
				{
					for (int num17 = 0; num17 < stripes; num17++)
					{
						RasterCloudStripe(num17, num17 * stripeW, Math.Min(Width, (num17 + 1) * stripeW));
					}
				}
			}
			else if (UseParallel && stripes > 1)
			{
				ForStripes(stripes, stripeW, RasterStripe);
			}
			else
			{
				for (int num18 = 0; num18 < stripes; num18++)
				{
					RasterStripe(num18, num18 * stripeW, Math.Min(Width, (num18 + 1) * stripeW));
				}
			}
			MeshRasterMs = (float)stopwatch.Elapsed.TotalMilliseconds - num3;
			if (_overlayPass || _vaporPass)
			{
				MeshMs = (float)stopwatch.Elapsed.TotalMilliseconds;
				return;
			}
			float num19 = (float)stopwatch.Elapsed.TotalMilliseconds;
			ApplyModelShadows();
			ModelShadowMs = (float)stopwatch.Elapsed.TotalMilliseconds - num19;
			for (int num20 = 0; num20 < stripes; num20++)
			{
				if (_meshPick[num20] >= 0 && _meshPickDepth[num20] < PickDepth)
				{
					PickTag = _meshPick[num20];
					PickDepth = _meshPickDepth[num20];
				}
			}
			MeshMs = (float)stopwatch.Elapsed.TotalMilliseconds;
		}

		private void PrepareMesh(int index)
		{
			MeshInstance meshInstance = _pass[index];
			MeshModel model = meshInstance.Model;
			int num = model.Parts.Length;
			if (_partM == null || _partM.Length < num)
			{
				_partM = new Mat3[Math.Max(num, 32)];
				_partT = new Vec3[Math.Max(num, 32)];
			}
			Mat3[] partM = _partM;
			Vec3[] partT = _partT;
			int num2 = ((meshInstance.PartRotation != null) ? meshInstance.PartRotation.Length : 0);
			int num3 = ((meshInstance.PartScale != null) ? meshInstance.PartScale.Length : 0);
			int num4 = ((meshInstance.PartOffset != null) ? meshInstance.PartOffset.Length : 0);
			for (int i = 0; i < num; i++)
			{
				MeshPart meshPart = model.Parts[i];
				Mat3 a = ((i < num2) ? meshInstance.PartRotation[i] : Mat3.Identity);
				float num5 = ((i < num3) ? meshInstance.PartScale[i] : 1f);
				if (num5 != 1f)
				{
					a *= num5;
				}
				Vec3 vec = meshPart.Pivot - a.Mul(meshPart.Pivot);
				if (i < num4)
				{
					vec += meshInstance.PartOffset[i];
				}
				if (meshPart.Parent >= 0)
				{
					Mat3 a2 = partM[meshPart.Parent];
					partM[i] = a2 * a;
					partT[i] = a2.Mul(vec) + partT[meshPart.Parent];
				}
				else
				{
					partM[i] = a;
					partT[i] = vec;
				}
			}
			Atmosphere air = Air;
			Vec3 light = _light;
			bool emissive = meshInstance.Emissive;
			float x = _view.X;
			float y = _view.Y;
			float z = _view.Z;
			int num6 = _vStart[index];
			TerrainCache terrain = _terrain;
			float num7 = 1f;
			float num8 = 1f;
			float z2 = meshInstance.Position.Z;
			float num9 = 1f;
			bool flag = false;
			bool flag2 = false;
			CloudLook cloud = meshInstance.Cloud;
			bool flag3 = _natural && !emissive && cloud == null && (_nOn || _landField);
			float num10 = 1f;
			float num11 = 1f;
			float num12 = 1f;
			bool flag4 = flag3 && _overlayPass && _landField;
			bool flag5 = false;
			bool flag6 = false;
			bool flag7 = false;
			bool flag8 = false;
			if (TerrainShadowsOnModels && !emissive && !_overlayPass && cloud == null && !meshInstance.ShadowOnly && terrain != null && terrain.CastShadows && terrain.Width > 0)
			{
				float num13 = Math.Max(0.1f, meshInstance.Basis.C2.Z * model.Max.Z);
				num9 = 1f / num13;
				float x2 = meshInstance.Position.X;
				float y2 = meshInstance.Position.Y;
				num8 = terrain.LightAt(x2, y2, z2 + num13);
				float num14 = Math.Max(Length(meshInstance.Basis.C0), Length(meshInstance.Basis.C1)) * Math.Max(Math.Max(0f - model.Min.X, model.Max.X), Math.Max(0f - model.Min.Y, model.Max.Y));
				float num15 = terrain.GroundLight(x2, y2);
				float val = terrain.GroundLight(x2 - num14, y2 - num14);
				float val2 = terrain.GroundLight(x2 + num14, y2 - num14);
				float val3 = terrain.GroundLight(x2 - num14, y2 + num14);
				float val4 = terrain.GroundLight(x2 + num14, y2 + num14);
				float num16 = Math.Min(num15, Math.Min(Math.Min(val, val2), Math.Min(val3, val4)));
				float num17 = Math.Max(num15, Math.Max(Math.Max(val, val2), Math.Max(val3, val4)));
				num7 = num15;
				flag2 = num17 - num16 > 0.01f;
				flag = flag2 || Math.Abs(num8 - num15) > 0.01f;
				if (flag3 && _landField && z2 > terrain.SampleHeight(x2, y2) + 0.5f)
				{
					flag5 = true;
					flag = (flag2 = false);
					num7 = LandVis(x2, y2, z2);
				}
				if (flag3)
				{
					if (_nOn)
					{
						float num18 = x2 - _nC.X;
						float num19 = y2 - _nC.Y;
						float num20 = z2 + num13 * 0.5f - _nC.Z;
						float num21 = (num18 * _nA.X + num19 * _nA.Y + num20 * _nA.Z) / _nReach;
						float num22 = (num18 * _nB.X + num19 * _nB.Y + num20 * _nB.Z) / _nUp;
						float num23 = (float)Math.Sqrt(Math.Max((num18 * num18 + num19 * num19) / (_nReach * _nReach), num21 * num21 + num22 * num22));
						float num24 = Math.Max(num14, num13) / _nUp;
						num10 = CoverAt(num23);
						flag6 = num23 + num24 > 0.88f && num23 - num24 < 1f;
						float num25 = x2 - _view.X;
						float num26 = y2 - _view.Y;
						float num27 = (float)Math.Sqrt(num25 * num25 + num26 * num26);
						float gatherReach = GatherReach;
						if (num27 - num14 >= gatherReach)
						{
							num10 = 0f;
						}
						else
						{
							num10 = Math.Min(num10, Gathered(x2, y2));
							flag6 |= num27 + num14 > gatherReach - GatherFade;
						}
						num10 = MapHeld(num10);
					}
					if (_landField && (num10 < 1f || flag6))
					{
						flag8 = true;
						Sunward(x2, y2, num14, out var sx, out var sy);
						float num28 = Math.Max(z2, terrain.SampleHeight(sx, sy));
						num12 = SunShadowField.Visibility(terrain, sx, sy, Math.Max(z2 + num13, num28), withCanopy: true);
						num11 = SunShadowField.Visibility(terrain, sx, sy, num28, withCanopy: true);
						flag7 = Math.Abs(num12 - num11) > 0.01f;
					}
				}
			}
			float sunFloor = _sunFloor;
			float num29 = 1f - sunFloor;
			float val5 = (flag3 ? _nHold : 0f);
			bool flag9 = flag3 && !(flag || flag7 || flag4 || flag5 || flag6);
			float num30 = 0f;
			float num31 = 0f;
			if (flag9)
			{
				float num32 = ((!flag8) ? num7 : Math.Min(num7, num11));
				float num33 = Math.Min(num7, val5);
				num30 = sunFloor + num29 * (1f - num10) * num32 + num29 * num10 * num33;
				num31 = num29 * num10 * (num7 - num33);
			}
			bool flag10 = meshInstance.Static && meshInstance.PartRotation == null && meshInstance.PartOffset == null;
			bool flag11 = flag10 && meshInstance.CacheModel == model && Same(meshInstance.CachePosition, meshInstance.Position) && Same(meshInstance.CacheBasis.C0, meshInstance.Basis.C0) && Same(meshInstance.CacheBasis.C1, meshInstance.Basis.C1) && Same(meshInstance.CacheBasis.C2, meshInstance.Basis.C2);
			int num34 = _vStart[index + 1] - num6;
			if (flag10 && !flag11)
			{
				if (meshInstance.CachePos == null || meshInstance.CachePos.Length != num34 * 3)
				{
					meshInstance.CachePos = new float[num34 * 3];
					meshInstance.CacheNrm = new float[num34 * 3];
				}
				meshInstance.CacheModel = null;
			}
			float[] cachePos = meshInstance.CachePos;
			float[] cacheNrm = meshInstance.CacheNrm;
			MVert[] mv = _mv;
			bool shadowOnly = meshInstance.ShadowOnly;
			bool overlayPass = _overlayPass;
			float x3 = light.X;
			float y3 = light.Y;
			float z3 = light.Z;
			float ambR = air.AmbR;
			float ambG = air.AmbG;
			float ambB = air.AmbB;
			float lightR = air.LightR;
			float lightG = air.LightG;
			float lightB = air.LightB;
			float rx = _rx;
			float ry = _ry;
			float fx = _fx;
			float fy = _fy;
			float cq = _lens.Cq;
			float sq = _lens.Sq;
			float num35 = (float)_eyeLight.R / 256f;
			float num36 = (float)_eyeLight.G / 256f;
			float num37 = (float)_eyeLight.B / 256f;
			int num38 = 0;
			for (int j = 0; j < num; j++)
			{
				MeshPart obj = model.Parts[j];
				Mat3 mat = meshInstance.Basis * partM[j];
				Vec3 vec2 = meshInstance.Basis.Mul(partT[j]) + meshInstance.Position;
				float x4 = mat.C0.X;
				float x5 = mat.C1.X;
				float x6 = mat.C2.X;
				float y4 = mat.C0.Y;
				float y5 = mat.C1.Y;
				float y6 = mat.C2.Y;
				float z4 = mat.C0.Z;
				float z5 = mat.C1.Z;
				float z6 = mat.C2.Z;
				float x7 = vec2.X;
				float y7 = vec2.Y;
				float z7 = vec2.Z;
				float[] pos = obj.Pos;
				float[] nrm = obj.Nrm;
				float[] uv = obj.Uv;
				int num39 = 0;
				int num40 = obj.VertexCount * 3;
				int num41 = 0;
				while (num39 < num40)
				{
					float num42 = pos[num39];
					float num43 = pos[num39 + 1];
					float num44 = pos[num39 + 2];
					if (uv != null)
					{
						mv[num6].U = uv[num41];
						mv[num6].V = uv[num41 + 1];
					}
					float num45;
					float num46;
					float num47;
					float num48;
					float num49;
					float num50;
					if (flag11)
					{
						num45 = cachePos[num38];
						num46 = cachePos[num38 + 1];
						num47 = cachePos[num38 + 2];
						num48 = cacheNrm[num38];
						num49 = cacheNrm[num38 + 1];
						num50 = cacheNrm[num38 + 2];
					}
					else
					{
						num45 = x4 * num42 + x5 * num43 + x6 * num44 + x7;
						num46 = y4 * num42 + y5 * num43 + y6 * num44 + y7;
						num47 = z4 * num42 + z5 * num43 + z6 * num44 + z7;
						float num51 = nrm[num39];
						float num52 = nrm[num39 + 1];
						float num53 = nrm[num39 + 2];
						num48 = x4 * num51 + x5 * num52 + x6 * num53;
						num49 = y4 * num51 + y5 * num52 + y6 * num53;
						num50 = z4 * num51 + z5 * num52 + z6 * num53;
						float num54 = (float)Math.Sqrt(num48 * num48 + num49 * num49 + num50 * num50);
						if (num54 > 1E-06f)
						{
							num48 /= num54;
							num49 /= num54;
							num50 /= num54;
						}
						if (flag10)
						{
							cachePos[num38] = num45;
							cachePos[num38 + 1] = num46;
							cachePos[num38 + 2] = num47;
							cacheNrm[num38] = num48;
							cacheNrm[num38 + 1] = num49;
							cacheNrm[num38 + 2] = num50;
						}
					}
					ref MVert reference = ref mv[num6];
					reference.Wx = num45;
					reference.Wy = num46;
					reference.Wz = num47;
					if (!shadowOnly)
					{
						float num55 = num45 - x;
						float num56 = num46 - y;
						reference.Side = num55 * rx + num56 * ry;
						float num57 = num55 * fx + num56 * fy;
						float num58 = num47 - z;
						reference.Depth = num57 * cq + num58 * sq;
						reference.Up = num58 * cq - num57 * sq;
						reference.Obj.X = num42;
						reference.Obj.Y = num43;
						reference.Obj.Z = num44;
						if (cloud != null)
						{
							CloudVertex(ref reference, cloud, num48, num49, num50, num45, num46, num47, num42, num43, num44);
						}
						else if (emissive)
						{
							reference.Amb.X = 1f;
							reference.Amb.Y = 1f;
							reference.Amb.Z = 1f;
							reference.Dir.X = 0f;
							reference.Dir.Y = 0f;
							reference.Dir.Z = 0f;
						}
						else
						{
							float num59 = 0.72f + 0.28f * num50;
							float num60 = num48 * x3 + num49 * y3 + num50 * z3;
							float num61 = ((num60 > 0f) ? num60 : 0f) + (1f - ((num50 > 0f) ? num50 : 0f)) * 0.1f;
							if (overlayPass)
							{
								float val6 = 0f - (num48 * fx + num49 * fy);
								num61 = Math.Max(num61, Math.Max(0f, val6) * 0.55f + 0.08f);
								if (num61 > 1f)
								{
									num61 = 1f;
								}
								reference.Amb.X = ambR * num59 + num35;
								reference.Amb.Y = ambG * num59 + num36;
								reference.Amb.Z = ambB * num59 + num37;
							}
							else
							{
								reference.Amb.X = ambR * num59;
								reference.Amb.Y = ambG * num59;
								reference.Amb.Z = ambB * num59;
							}
							if (flag3)
							{
								float num62;
								float num63;
								if (flag9)
								{
									num62 = num61 * num30;
									num63 = num61 * num31;
								}
								else
								{
									float num64 = num7;
									float num65 = num10;
									float num66 = 0f;
									if (flag || flag7)
									{
										num66 = (num47 - z2) * num9;
										num66 = ((num66 < 0f) ? 0f : ((num66 > 1f) ? 1f : num66));
									}
									if (flag)
									{
										float num67 = (flag2 ? terrain.GroundLight(num45, num46) : num7);
										num64 = num67 + (num8 - num67) * num66;
									}
									else if (flag4 || flag5)
									{
										num64 = LandVis(num45, num46, num47);
									}
									float num68 = ((!flag8) ? num64 : Math.Min(num64, flag7 ? (num11 + (num12 - num11) * num66) : num11));
									if (flag6)
									{
										num65 = MapCover(num45, num46, num47);
									}
									float num69 = Math.Min(num64, val5);
									num62 = num61 * (sunFloor + num29 * (1f - num65) * num68 + num29 * num65 * num69);
									num63 = num61 * (num29 * num65 * (num64 - num69));
								}
								reference.Amb.X += lightR * num62;
								reference.Amb.Y += lightG * num62;
								reference.Amb.Z += lightB * num62;
								reference.Dir.X = lightR * num63;
								reference.Dir.Y = lightG * num63;
								reference.Dir.Z = lightB * num63;
								reference.Sun = num61;
							}
							else
							{
								if (flag)
								{
									float num70 = (flag2 ? terrain.GroundLight(num45, num46) : num7);
									float num71 = (num47 - z2) * num9;
									num71 = ((num71 < 0f) ? 0f : ((num71 > 1f) ? 1f : num71));
									num61 *= num70 + (num8 - num70) * num71;
								}
								else if (num7 < 1f)
								{
									num61 *= num7;
								}
								reference.Dir.X = lightR * num61;
								reference.Dir.Y = lightG * num61;
								reference.Dir.Z = lightB * num61;
							}
						}
					}
					num39 += 3;
					num41 += 2;
					num6++;
					num38 += 3;
				}
			}
			if (flag10 && !flag11)
			{
				meshInstance.CacheModel = model;
				meshInstance.CachePosition = meshInstance.Position;
				meshInstance.CacheBasis = meshInstance.Basis;
			}
			if (meshInstance.ShadowOnly)
			{
				_tCount[index] = 0;
				return;
			}
			if (!overlayPass && SkipHidden && Hidden(meshInstance, model, mv, _vStart[index], _vStart[index + 1]))
			{
				_tCount[index] = 0;
				Interlocked.Increment(ref _meshesHidden);
				return;
			}
			int num72 = _tStart[index];
			int num73 = 0;
			num6 = _vStart[index];
			MVert[] array = _clip ?? (_clip = new MVert[4]);
			STri[] st = _st;
			uint[] palette = meshInstance.Palette;
			uint[] slotColor = model.SlotColor;
			byte[] slotKind = model.SlotKind;
			byte[] slotFlags = model.SlotFlags;
			ulong hidden = meshInstance.Hidden;
			float num74 = ((Vec3.Dot(meshInstance.Basis.C0, Vec3.Cross(meshInstance.Basis.C1, meshInstance.Basis.C2)) < 0f) ? (-1f) : 1f);
			bool fine = meshInstance.KeepSmall && !overlayPass;
			float num75 = ((!overlayPass && cloud == null && !emissive) ? meshInstance.Snow : 0f);
			int[] slotTexture = model.SlotTexture;
			MeshTexture[] textures = model.Textures;
			for (int k = 0; k < num; k++)
			{
				MeshPart meshPart2 = model.Parts[k];
				if (k < 64 && (hidden & (ulong)(1L << k)) != 0L)
				{
					num6 += meshPart2.VertexCount;
					continue;
				}
				int[] tri = meshPart2.Tri;
				byte[] slot = meshPart2.Slot;
				byte[] axis = meshPart2.Axis;
				bool flag12 = slotTexture != null && meshPart2.Uv != null && cloud == null;
				int num76 = 0;
				int num77 = 0;
				int triangleCount = meshPart2.TriangleCount;
				while (num76 < triangleCount)
				{
					ref MVert reference2 = ref mv[num6 + tri[num77]];
					ref MVert reference3 = ref mv[num6 + tri[num77 + 1]];
					ref MVert reference4 = ref mv[num6 + tri[num77 + 2]];
					float depth = reference2.Depth;
					float depth2 = reference3.Depth;
					float depth3 = reference4.Depth;
					if (!(depth < 0.04f) || !(depth2 < 0.04f) || !(depth3 < 0.04f))
					{
						float wx = reference2.Wx;
						float wy = reference2.Wy;
						float wz = reference2.Wz;
						float num78 = reference3.Wx - wx;
						float num79 = reference3.Wy - wy;
						float num80 = reference3.Wz - wz;
						float num81 = reference4.Wx - wx;
						float num82 = reference4.Wy - wy;
						float num83 = reference4.Wz - wz;
						float num84 = num79 * num83 - num80 * num82;
						float num85 = num80 * num81 - num78 * num83;
						float num86 = num78 * num82 - num79 * num81;
						bool flag13 = (num84 * (wx - x) + num85 * (wy - y) + num86 * (wz - z)) * num74 >= 0f;
						if (!flag13 || cloud != null)
						{
							byte b = slot[num76];
							uint color = ((palette != null && b < palette.Length && palette[b] != 0) ? palette[b] : slotColor[b]);
							byte kind = slotKind[b];
							byte b2 = slotFlags[b];
							byte axis2 = axis[num76];
							if ((b2 & 0x80) == 0)
							{
								MeshTexture meshTexture = null;
								if (flag12 && b < slotTexture.Length && slotTexture[b] >= 0)
								{
									meshTexture = textures[slotTexture[b]];
									if (meshTexture.Pixels == null)
									{
										meshTexture = null;
									}
								}
								if (num75 > 0.01f && (b2 & 7) == 0 && kind != 12)
								{
									color = SnowFace(color, ref kind, num84 * num74, num85 * num74, num86 * num74, num75, (wx + reference3.Wx + reference4.Wx) * (1f / 3f), (wy + reference3.Wy + reference4.Wy) * (1f / 3f), (wz + reference3.Wz + reference4.Wz) * (1f / 3f));
								}
								if (flag13)
								{
									b2 |= 0x80;
								}
								if (depth >= 0.04f && depth2 >= 0.04f && depth3 >= 0.04f)
								{
									bool flag14 = (b2 & 0x10) != 0;
									if (((overlayPass && (kind == 10 || kind == 12)) || flag14) && (b2 & 1) == 0 && !emissive)
									{
										float num87 = 1f / (float)Math.Sqrt(num84 * num84 + num85 * num85 + num86 * num86 + 1E-20f);
										float num88 = num84 * num87;
										float num89 = num85 * num87;
										float num90 = num86 * num87;
										float num91 = reference2.Wx - x;
										float num92 = reference2.Wy - y;
										float num93 = reference2.Wz - z;
										float num94 = 1f / (float)Math.Sqrt(num91 * num91 + num92 * num92 + num93 * num93 + 1E-20f);
										num91 *= num94;
										num92 *= num94;
										num93 *= num94;
										float num95 = 2f * (num91 * num88 + num92 * num89 + num93 * num90);
										float num96 = num91 - num95 * num88;
										float num97 = num92 - num95 * num89;
										float num98 = num93 - num95 * num90;
										float num99 = 0.22f + 0.42f * Math.Max(0f, num98) + 0.1f * Math.Max(0f, 0f - num98);
										float num100 = Math.Max(0f, num96 * light.X + num97 * light.Y + num98 * light.Z);
										float num101 = num100 * num100;
										float num102 = num101 * num101;
										float num103 = num102 * num102 * 1.3f;
										if (!overlayPass)
										{
											num99 *= 0.55f;
											num103 *= (flag3 ? Math.Min(1f, 2f * (reference2.Sun + reference3.Sun + reference4.Sun) * (1f / 3f)) : Math.Min(1f, 2f * (reference2.Dir.X + reference3.Dir.X + reference4.Dir.X) / Math.Max(0.0001f, 3f * air.LightR)));
										}
										float num104 = 1f;
										float num105 = 0f;
										if (flag9)
										{
											num104 = num30;
											num105 = num31;
										}
										else if (flag3)
										{
											float x8 = (reference2.Wx + reference3.Wx + reference4.Wx) * (1f / 3f);
											float y8 = (reference2.Wy + reference3.Wy + reference4.Wy) * (1f / 3f);
											float num106 = (reference2.Wz + reference3.Wz + reference4.Wz) * (1f / 3f);
											float num107 = num7;
											float num108 = num10;
											float num109 = 0f;
											if (flag || flag7)
											{
												num109 = (num106 - z2) * num9;
												num109 = ((num109 < 0f) ? 0f : ((num109 > 1f) ? 1f : num109));
											}
											if (flag)
											{
												float num110 = (flag2 ? terrain.GroundLight(x8, y8) : num7);
												num107 = num110 + (num8 - num110) * num109;
											}
											else if (flag4 || flag5)
											{
												num107 = LandVis(x8, y8, num106);
											}
											float num111 = ((!flag8) ? num107 : Math.Min(num107, flag7 ? (num11 + (num12 - num11) * num109) : num11));
											if (flag6)
											{
												num108 = MapCover(x8, y8, num106);
											}
											float num112 = Math.Min(num107, val5);
											num104 = sunFloor + num29 * (1f - num108) * num111 + num29 * num108 * num112;
											num105 = num29 * num108 * (num107 - num112);
										}
										float num113 = num103 * num104;
										float num114 = num103 * num105;
										Vec3 vec3 = new Vec3(air.AmbR * num99 + air.LightR * num113, air.AmbG * num99 + air.LightG * num113, air.AmbB * num99 + air.LightB * num113);
										MVert a3 = reference2;
										MVert b3 = reference3;
										MVert c = reference4;
										a3.Amb += vec3;
										b3.Amb += vec3;
										c.Amb += vec3;
										if (num114 > 0f)
										{
											Vec3 vec4 = new Vec3(air.LightR * num114, air.LightG * num114, air.LightB * num114);
											a3.Dir += vec4;
											b3.Dir += vec4;
											c.Dir += vec4;
										}
										if (Emit(ref st[num72 + num73], ref a3, ref b3, ref c, color, kind, b2, axis2, index, fine, meshTexture))
										{
											num73++;
										}
									}
									else if (Emit(ref st[num72 + num73], ref reference2, ref reference3, ref reference4, color, kind, b2, axis2, index, fine, meshTexture))
									{
										num73++;
									}
								}
								else
								{
									int count = 0;
									ClipEdge(array, ref count, ref reference2, ref reference3);
									ClipEdge(array, ref count, ref reference3, ref reference4);
									ClipEdge(array, ref count, ref reference4, ref reference2);
									for (int l = 1; l + 1 < count; l++)
									{
										if (Emit(ref st[num72 + num73], ref array[0], ref array[l], ref array[l + 1], color, kind, b2, axis2, index, fine, meshTexture))
										{
											num73++;
										}
									}
								}
							}
						}
					}
					num76++;
					num77 += 3;
				}
				num6 += meshPart2.VertexCount;
			}
			_tCount[index] = num73;
		}

		private void BuildTileMax()
		{
			MeshesHidden = _meshesHidden;
			_meshesHidden = 0;
			int width = Width;
			int height = Height;
			int num = width + 7 >> 3;
			int num2 = height + 7 >> 3;
			if (_tileMax.Length != num * num2)
			{
				_tileMax = new float[num * num2];
			}
			_tilesX = num;
			_tilesY = num2;
			For(num2, TileMaxBand, UseParallel);
		}

		private void TileMaxBand(int t)
		{
			int width = Width;
			int height = Height;
			int tilesX = _tilesX;
			float[] depth = Depth;
			float[] tileMax = _tileMax;
			for (int i = 0; i < tilesX; i++)
			{
				tileMax[t * tilesX + i] = float.MinValue;
			}
			int j = t * 8;
			for (int num = Math.Min(height, j + 8); j < num; j++)
			{
				int num2 = (height - 1 - j) * width;
				for (int k = 0; k < tilesX; k++)
				{
					float num3 = tileMax[t * tilesX + k];
					int l = k * 8;
					for (int num4 = Math.Min(width, l + 8); l < num4; l++)
					{
						float num5 = depth[num2 + l];
						if (num5 > num3)
						{
							num3 = num5;
						}
					}
					tileMax[t * tilesX + k] = num3;
				}
			}
		}

		private bool Hidden(MeshInstance m, MeshModel model, MVert[] mvs, int v0, int v1)
		{
			float focal = _focal;
			float num = (float)Width * 0.5f;
			float horizon = _horizon;
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			float num4 = float.MaxValue;
			float num5 = float.MinValue;
			float num6 = float.MaxValue;
			if (m.PartRotation == null && m.PartOffset == null && m.PartScale == null)
			{
				Mat3 basis = m.Basis;
				Vec3 min = model.Min;
				Vec3 max = model.Max;
				float x = _view.X;
				float y = _view.Y;
				float z = _view.Z;
				for (int i = 0; i < 8; i++)
				{
					float num7 = (((i & 1) == 0) ? min.X : max.X);
					float num8 = (((i & 2) == 0) ? min.Y : max.Y);
					float num9 = (((i & 4) == 0) ? min.Z : max.Z);
					float num10 = basis.C0.X * num7 + basis.C1.X * num8 + basis.C2.X * num9 + m.Position.X;
					float num11 = basis.C0.Y * num7 + basis.C1.Y * num8 + basis.C2.Y * num9 + m.Position.Y;
					float num12 = basis.C0.Z * num7 + basis.C1.Z * num8 + basis.C2.Z * num9 + m.Position.Z;
					float num13 = num10 - x;
					float num14 = num11 - y;
					float num15 = num13 * _fx + num14 * _fy;
					float num16 = num12 - z;
					float num17 = num15 * _lens.Cq + num16 * _lens.Sq;
					if (num17 < 0.04f)
					{
						return false;
					}
					float num18 = focal / num17;
					float num19 = num + (num13 * _rx + num14 * _ry) * num18;
					float num20 = horizon - (num16 * _lens.Cq - num15 * _lens.Sq) * num18;
					if (num19 < num2)
					{
						num2 = num19;
					}
					if (num19 > num3)
					{
						num3 = num19;
					}
					if (num20 < num4)
					{
						num4 = num20;
					}
					if (num20 > num5)
					{
						num5 = num20;
					}
					if (num17 < num6)
					{
						num6 = num17;
					}
				}
			}
			else
			{
				for (int j = v0; j < v1; j++)
				{
					ref MVert reference = ref mvs[j];
					if (reference.Depth < 0.04f)
					{
						return false;
					}
					float num21 = focal / reference.Depth;
					float num22 = num + reference.Side * num21;
					float num23 = horizon - reference.Up * num21;
					if (num22 < num2)
					{
						num2 = num22;
					}
					if (num22 > num3)
					{
						num3 = num22;
					}
					if (num23 < num4)
					{
						num4 = num23;
					}
					if (num23 > num5)
					{
						num5 = num23;
					}
					if (reference.Depth < num6)
					{
						num6 = reference.Depth;
					}
				}
				if (num6 == float.MaxValue)
				{
					return false;
				}
			}
			int width = Width;
			int height = Height;
			int num24 = FastMath.FloorInt(num2) - 1;
			int num25 = FastMath.FloorInt(num3) + 1;
			int num26 = FastMath.FloorInt(num4) - 1;
			int num27 = FastMath.FloorInt(num5) + 1;
			if (num25 < 0 || num24 >= width || num27 < 0 || num26 >= height)
			{
				return true;
			}
			if (num24 < 0)
			{
				num24 = 0;
			}
			if (num25 > width - 1)
			{
				num25 = width - 1;
			}
			if (num26 < 0)
			{
				num26 = 0;
			}
			if (num27 > height - 1)
			{
				num27 = height - 1;
			}
			float num28 = NearestT(num6) * 0.9999f;
			float[] tileMax = _tileMax;
			int tilesX = _tilesX;
			int k = num26 >> 3;
			for (int num29 = num27 >> 3; k <= num29; k++)
			{
				int l = k * tilesX + (num24 >> 3);
				for (int num30 = k * tilesX + (num25 >> 3); l <= num30; l++)
				{
					if (tileMax[l] >= num28)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool Same(Vec3 a, Vec3 b)
		{
			if (a.X == b.X && a.Y == b.Y)
			{
				return a.Z == b.Z;
			}
			return false;
		}

		private static float Length(Vec3 a)
		{
			return (float)Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
		}

		private static void ClipEdge(MVert[] poly, ref int count, ref MVert a, ref MVert b)
		{
			bool num = a.Depth >= 0.04f;
			bool flag = b.Depth >= 0.04f;
			if (num && count < 4)
			{
				poly[count++] = a;
			}
			if (num != flag && count < 4)
			{
				float num2 = (0.04f - a.Depth) / (b.Depth - a.Depth);
				poly[count++] = new MVert
				{
					Side = a.Side + (b.Side - a.Side) * num2,
					Up = a.Up + (b.Up - a.Up) * num2,
					Depth = 0.04f,
					Wx = a.Wx + (b.Wx - a.Wx) * num2,
					Wy = a.Wy + (b.Wy - a.Wy) * num2,
					Wz = a.Wz + (b.Wz - a.Wz) * num2,
					Amb = a.Amb + (b.Amb - a.Amb) * num2,
					Dir = a.Dir + (b.Dir - a.Dir) * num2,
					Obj = a.Obj + (b.Obj - a.Obj) * num2,
					U = a.U + (b.U - a.U) * num2,
					V = a.V + (b.V - a.V) * num2
				};
			}
		}

		private bool Emit(ref STri s, ref MVert a, ref MVert b, ref MVert c, uint color, byte kind, byte flags, byte axis, int inst, bool fine, MeshTexture tex = null)
		{
			float num = 1f / a.Depth;
			float num2 = 1f / b.Depth;
			float num3 = 1f / c.Depth;
			float num4 = (float)Width * 0.5f;
			float focal = _focal;
			float horizon = _horizon;
			float num5 = num4 + a.Side * num * focal;
			float num6 = horizon - a.Up * num * focal;
			float num7 = num4 + b.Side * num2 * focal;
			float num8 = horizon - b.Up * num2 * focal;
			float num9 = num4 + c.Side * num3 * focal;
			float num10 = horizon - c.Up * num3 * focal;
			float num11 = ((num7 < num9) ? num7 : num9);
			float num12 = ((num7 > num9) ? num7 : num9);
			float num13 = ((num8 < num10) ? num8 : num10);
			float num14 = ((num8 > num10) ? num8 : num10);
			if (num5 < num11)
			{
				num11 = num5;
			}
			if (num5 > num12)
			{
				num12 = num5;
			}
			if (num6 < num13)
			{
				num13 = num6;
			}
			if (num6 > num14)
			{
				num14 = num6;
			}
			int width = Width;
			int height = Height;
			if (num12 < 0f || num11 >= (float)width || num14 < 0f || num13 >= (float)height)
			{
				return false;
			}
			int num15 = (int)num11;
			int num16 = (int)num12;
			int num17 = (int)num13;
			int num18 = (int)num14;
			if (num15 < 0)
			{
				num15 = 0;
			}
			if (num16 > width - 1)
			{
				num16 = width - 1;
			}
			if (num17 < 0)
			{
				num17 = 0;
			}
			if (num18 > height - 1)
			{
				num18 = height - 1;
			}
			if (num15 > num16 || num17 > num18)
			{
				return false;
			}
			if (!Edges(num5, num6, num7, num8, num9, num10, out var a2, out var b2, out var c2, out var a3, out var b3, out var c3))
			{
				return false;
			}
			if (num18 - num17 <= 2)
			{
				bool flag = false;
				for (int i = num17; i <= num18; i++)
				{
					if (flag)
					{
						break;
					}
					flag = RowSpan(a2, b2, c2, a3, b3, c3, (float)i + 0.5f, num15, num16, out var _, out var _);
				}
				if (!flag && !fine)
				{
					return false;
				}
			}
			s.X0 = num5;
			s.Y0 = num6;
			s.X1 = num7;
			s.Y1 = num8;
			s.X2 = num9;
			s.Y2 = num10;
			s.MinX = num15;
			s.MaxX = num16;
			s.MinY = num17;
			s.MaxY = num18;
			s.W0 = num;
			s.W1 = num2;
			s.W2 = num3;
			s.A0.X = a.Amb.X * num;
			s.A0.Y = a.Amb.Y * num;
			s.A0.Z = a.Amb.Z * num;
			s.A1.X = b.Amb.X * num2;
			s.A1.Y = b.Amb.Y * num2;
			s.A1.Z = b.Amb.Z * num2;
			s.A2.X = c.Amb.X * num3;
			s.A2.Y = c.Amb.Y * num3;
			s.A2.Z = c.Amb.Z * num3;
			s.D0.X = a.Dir.X * num;
			s.D0.Y = a.Dir.Y * num;
			s.D0.Z = a.Dir.Z * num;
			s.D1.X = b.Dir.X * num2;
			s.D1.Y = b.Dir.Y * num2;
			s.D1.Z = b.Dir.Z * num2;
			s.D2.X = c.Dir.X * num3;
			s.D2.Y = c.Dir.Y * num3;
			s.D2.Z = c.Dir.Z * num3;
			s.O0.X = a.Obj.X * num;
			s.O0.Y = a.Obj.Y * num;
			s.O0.Z = a.Obj.Z * num;
			s.O1.X = b.Obj.X * num2;
			s.O1.Y = b.Obj.Y * num2;
			s.O1.Z = b.Obj.Z * num2;
			s.O2.X = c.Obj.X * num3;
			s.O2.Y = c.Obj.Y * num3;
			s.O2.Z = c.Obj.Z * num3;
			s.Tex = tex;
			if (tex != null)
			{
				s.U0 = a.U * num;
				s.V0 = a.V * num;
				s.U1 = b.U * num2;
				s.V1 = b.V * num2;
				s.U2 = c.U * num3;
				s.V2 = c.V * num3;
			}
			s.Color = color;
			s.Kind = kind;
			s.Flags = flags;
			s.Axis = axis;
			s.Inst = inst;
			s.Fine = fine;
			s.Gu = (s.Gv = (s.Nu = (s.Nv = (s.Nd = 0f))));
			s.Sw = (s.Sm = 1f);
			s.Sk = _nScale;
			if (_nOn && _overlayPass)
			{
				s.Sw = 0f;
			}
			else if (_nOn)
			{
				float num19 = b.Wx - a.Wx;
				float num20 = b.Wy - a.Wy;
				float num21 = b.Wz - a.Wz;
				float num22 = c.Wx - a.Wx;
				float num23 = c.Wy - a.Wy;
				float num24 = c.Wz - a.Wz;
				float num25 = num20 * num24 - num21 * num23;
				float num26 = num21 * num22 - num19 * num24;
				float num27 = num19 * num23 - num20 * num22;
				float num28 = num25 * num25 + num26 * num26 + num27 * num27;
				if (num28 > 1E-20f)
				{
					float num29 = 1f / (float)Math.Sqrt(num28);
					if (num25 * (_view.X - a.Wx) + num26 * (_view.Y - a.Wy) + num27 * (_view.Z - a.Wz) < 0f)
					{
						num29 = 0f - num29;
					}
					num25 *= num29;
					num26 *= num29;
					num27 *= num29;
					float nScale = _nScale;
					float nScaleB = _nScaleB;
					float num30 = num25 * _nA.X + num26 * _nA.Y + num27 * _nA.Z;
					float num31 = num25 * _nB.X + num26 * _nB.Y + num27 * _nB.Z;
					float num32 = num25 * _nL.X + num26 * _nL.Y + num27 * _nL.Z;
					float num33 = ((num32 >= 0f) ? Math.Max(num32, 0.05f) : Math.Min(num32, -0.05f));
					float num34 = -1f / num33;
					s.Gu = num30 * num34 * _nInvScale;
					s.Gv = num31 * num34 * _nInvScaleB;
					float num35 = (float)Math.Sqrt(Math.Max(0f, 1f - num32 * num32)) * _nFaceOff;
					s.Nu = num30 * num35 * nScale;
					s.Nv = num31 * num35 * nScaleB;
					s.Nd = num32 * num35;
					float num36 = s.Gu * s.Gu + s.Gv * s.Gv;
					float num37 = FaceStipple(num32);
					float nFaceFront = _nFaceFront;
					s.Sw = ((num37 * num37 * num36 <= nFaceFront * nFaceFront) ? num37 : (nFaceFront / (float)Math.Sqrt(num36)));
					s.Sw *= Math.Min(1f, nScale * Math.Abs(num32) * 0.125f);
					s.Sk = nScale * Math.Max(Math.Abs(num32), 0.15f);
					s.Sm = ((kind == 1) ? 1f : FaceMapShare(num32));
				}
			}
			return true;
		}

		private void RasterStripe(int stripe, int x0, int x1)
		{
			int num = -1;
			float num2 = float.MaxValue;
			int num3 = Width / 2;
			int num4 = Height / 2;
			int width = Width;
			int height = Height;
			uint[] pixels = Pixels;
			float[] passDepth = _passDepth;
			byte[] sunlit = _sunlit;
			uint[] scrFog = _scrFog;
			bool overlayPass = _overlayPass;
			bool shadowReady = _shadowReady;
			float horizon = _horizon;
			float focal = _focal;
			float num5 = (float)Width * 0.5f;
			float x2 = _view.X;
			float y = _view.Y;
			float z = _view.Z;
			float fx = _fx;
			float fy = _fy;
			float rx = _rx;
			float ry = _ry;
			bool tilted = _tilted;
			Lens lens = _lens;
			float x3 = _sC.X;
			float y2 = _sC.Y;
			float z2 = _sC.Z;
			float sScale = _sScale;
			float sScaleB = _sScaleB;
			float num6 = (float)ShadowSize * 0.5f;
			float shadowFloor = _shadowFloor;
			int shadowSize = ShadowSize;
			float x4 = _sA.X;
			float y3 = _sA.Y;
			float z3 = _sA.Z;
			float x5 = _sB.X;
			float y4 = _sB.Y;
			float z4 = _sB.Z;
			float x6 = _sL.X;
			float y5 = _sL.Y;
			float z5 = _sL.Z;
			float[] shadowMap = _shadowMap;
			bool natural = _natural;
			float num7 = 0.3f / Math.Min(sScale, 16f);
			float num8 = FrontOfFace(Math.Max(sScale, 16f));
			int nOu = _nOu;
			int nOv = _nOv;
			byte[] array = (_portalPass ? _portalMask : null);
			byte[] actorMask = _actorMask;
			int i = _binStart[stripe];
			for (int num9 = _binStart[stripe + 1]; i < num9; i++)
			{
				ref STri reference = ref _st[_binItems[i]];
				int num10 = Math.Max(x0, reference.MinX);
				int num11 = Math.Min(x1 - 1, reference.MaxX);
				if (num10 > num11 || !Edges(reference.X0, reference.Y0, reference.X1, reference.Y1, reference.X2, reference.Y2, out var a, out var b, out var c, out var a2, out var b2, out var c2))
				{
					continue;
				}
				MeshInstance meshInstance = _pass[reference.Inst];
				float num12 = (float)(meshInstance.Flash >> 24) / 255f;
				bool flag = (reference.Flags & 1) != 0 || meshInstance.Emissive;
				bool flag2 = (reference.Flags & 2) != 0 && meshInstance.Glow > 0f;
				float glow = meshInstance.Glow;
				uint multiply = meshInstance.Multiply;
				uint b3 = meshInstance.Flash | 0xFF000000u;
				float w = reference.W0;
				float w2 = reference.W1;
				float w3 = reference.W2;
				float x7 = reference.O0.X;
				float y6 = reference.O0.Y;
				float z6 = reference.O0.Z;
				float x8 = reference.O1.X;
				float y7 = reference.O1.Y;
				float z7 = reference.O1.Z;
				float x9 = reference.O2.X;
				float y8 = reference.O2.Y;
				float z8 = reference.O2.Z;
				float x10 = reference.A0.X;
				float y9 = reference.A0.Y;
				float z9 = reference.A0.Z;
				float x11 = reference.A1.X;
				float y10 = reference.A1.Y;
				float z10 = reference.A1.Z;
				float x12 = reference.A2.X;
				float y11 = reference.A2.Y;
				float z11 = reference.A2.Z;
				float x13 = reference.D0.X;
				float y12 = reference.D0.Y;
				float z12 = reference.D0.Z;
				float x14 = reference.D1.X;
				float y13 = reference.D1.Y;
				float z13 = reference.D1.Z;
				float x15 = reference.D2.X;
				float y14 = reference.D2.Y;
				float z14 = reference.D2.Z;
				bool flag3 = x13 + x14 + x15 + y12 + y13 + y14 > 0.0001f;
				float gu = reference.Gu;
				float gv = reference.Gv;
				float nu = reference.Nu;
				float nv = reference.Nv;
				float nd = reference.Nd;
				float sw = reference.Sw;
				float sm = reference.Sm;
				float num13 = gu * nu + gv * nv;
				float d = 0f;
				float d2 = 0f;
				if (natural && !overlayPass)
				{
					MapCellDepths(focal, reference.Sk, out d, out d2);
				}
				byte kind = reference.Kind;
				byte axis = reference.Axis;
				float num14 = (((reference.Flags & 8) != 0) ? (meshInstance.Fade * 0.5f) : meshInstance.Fade);
				bool flag4 = num14 < 1f;
				float num15 = ((meshInstance.DissolveScale > 0f) ? (1f / meshInstance.DissolveScale) : 0f);
				bool flag5 = !overlayPass && IsActorTag(meshInstance.Tag);
				uint num16 = reference.Color | 0xFF000000u;
				MeshTexture tex = reference.Tex;
				uint[] array2 = tex?.Pixels;
				int w4 = 0;
				int h = 0;
				bool crisp = false;
				bool flag6 = false;
				float num17 = 0f;
				float num18 = 0f;
				float num19 = 0f;
				float num20 = 0f;
				float num21 = 0f;
				float num22 = 0f;
				if (array2 != null)
				{
					w4 = tex.Width;
					h = tex.Height;
					crisp = tex.Crisp;
					flag6 = tex.Cutout;
					num17 = reference.U0;
					num18 = reference.V0;
					num19 = reference.U1;
					num20 = reference.V1;
					num21 = reference.U2;
					num22 = reference.V2;
				}
				uint[] array3 = (((reference.Flags & 4) != 0 && array != null) ? Backdrop : null);
				bool fine = reference.Fine;
				bool flag7 = false;
				int j = reference.MinY;
				for (int num23 = (fine ? (reference.MaxY + 1) : reference.MaxY); j <= num23; j++)
				{
					int num24 = j;
					float num25 = (float)num24 + 0.5f;
					float mid = -1f;
					int xs;
					int xe;
					if (j > reference.MaxY)
					{
						if (flag7)
						{
							break;
						}
						float num26 = (reference.X0 + reference.X1 + reference.X2) * (1f / 3f);
						float num27 = (reference.Y0 + reference.Y1 + reference.Y2) * (1f / 3f);
						num24 = (int)num27;
						num25 = num27;
						mid = num26;
						xs = (xe = (int)num26);
						if (num27 < 0f || num24 >= height || num26 < 0f || xs < num10 || xs > num11)
						{
							break;
						}
					}
					else if (!RowSpan(a, b, c, a2, b2, c2, num25, num10, num11, out xs, out xe))
					{
						if (!fine)
						{
							continue;
						}
						if (RowSpan(a, b, c, a2, b2, c2, num25, reference.MinX, reference.MaxX, out var _, out var _))
						{
							flag7 = true;
							continue;
						}
						if (!RowMid(a, b, c, a2, b2, c2, num25, reference.MinX, reference.MaxX, out mid))
						{
							continue;
						}
						flag7 = true;
						xs = (xe = (int)mid);
						if (xs < num10 || xs > num11)
						{
							continue;
						}
					}
					flag7 = true;
					float num28 = (horizon - num25) / focal;
					float num29 = 1f;
					float num30 = 1f;
					if (tilted)
					{
						float num31 = num28;
						num29 = lens.Cq - num31 * lens.Sq;
						if (num29 < 0.0001f)
						{
							num29 = 0.0001f;
						}
						num30 = 1f / num29;
						num28 = (lens.Sq + num31 * lens.Cq) * num30;
					}
					int num32 = (height - 1 - num24) * width;
					for (int k = xs; k <= xe; k++)
					{
						float num33 = ((mid >= 0f) ? mid : ((float)k + 0.5f));
						float num34 = a * num33 + b * num25 + c;
						float num35 = a2 * num33 + b2 * num25 + c2;
						float num36 = 1f - num34 - num35;
						float num37 = num34 * w + num35 * w2 + num36 * w3;
						if (num37 <= 0f)
						{
							continue;
						}
						float num38 = 1f / num37;
						float num39 = num38 * num29;
						int num40 = num32 + k;
						if (num39 >= passDepth[num40])
						{
							continue;
						}
						if (flag4)
						{
							if (num15 > 0f)
							{
								float num41 = (num34 * x7 + num35 * x8 + num36 * x9) * num38;
								float num42 = (num34 * y6 + num35 * y7 + num36 * y8) * num38;
								float num43 = (num34 * z6 + num35 * z7 + num36 * z8) * num38;
								if (DissolveNoise(num41 * num15, num42 * num15, num43 * num15) + (BayerAt(k, num24) - 0.5f) * 0.12f >= num14)
								{
									continue;
								}
							}
							else if (BayerAt(k, num24) >= num14)
							{
								continue;
							}
						}
						if (array3 != null)
						{
							pixels[num40] = array3[num40];
							passDepth[num40] = num39;
							array[num40] = 1;
							if (!overlayPass)
							{
								sunlit[num40] = 0;
							}
							continue;
						}
						uint c3 = num16;
						int num45;
						if (array2 != null)
						{
							uint num44 = Texel(array2, w4, h, crisp, (num34 * num17 + num35 * num19 + num36 * num21) * num38, (num34 * num18 + num35 * num20 + num36 * num22) * num38);
							if (flag6 && num44 >> 24 < 128)
							{
								continue;
							}
							c3 = num44 | 0xFF000000u;
							num45 = 128;
						}
						else
						{
							float x16 = (num34 * x7 + num35 * x8 + num36 * x9) * num38;
							float y15 = (num34 * y6 + num35 * y7 + num36 * y8) * num38;
							float z15 = (num34 * z6 + num35 * z7 + num36 * z8) * num38;
							num45 = Pattern(kind, axis, x16, y15, z15);
						}
						uint num46;
						if (flag)
						{
							num46 = TerrainCache.Scale(c3, (float)num45 / 128f);
						}
						else
						{
							float num47 = 1f;
							if (shadowReady && flag3)
							{
								float num48 = (num33 - num5) / focal * num30;
								float num49 = x2 + (fx + rx * num48) * num39;
								float num50 = y + (fy + ry * num48) * num39;
								float num51 = z + num28 * num39;
								float num52 = num49 - x3;
								float num53 = num50 - y2;
								float num54 = num51 - z2;
								float num55 = num6 + (num52 * x4 + num53 * y3 + num54 * z3) * sScale;
								float num56 = num6 + (num52 * x5 + num53 * y4 + num54 * z4) * sScaleB;
								if (natural)
								{
									num55 += nu;
									num56 += nv;
									float num57 = num52 * x6 + num53 * y5 + num54 * z5;
									float num58 = (overlayPass ? (0.08f + num39 * 0.004f) : (num7 + num39 * 0.0008f));
									float num59 = num57 + nd + num58;
									float ju = 0f;
									float jv = 0f;
									num47 = sm;
									if (num47 > 0f && !overlayPass)
									{
										MapJitter(num55, num56, sw, MapCells(num39, d, d2), nOu, nOv, out ju, out jv);
									}
									int num60 = FastMath.FloorInt(num55 + ju);
									int num61 = FastMath.FloorInt(num56 + jv);
									if (num47 > 0f && num60 >= 0 && num61 >= 0 && num60 < shadowSize && num61 < shadowSize)
									{
										float num62 = shadowMap[num61 * shadowSize + num60];
										float num63 = gu * ((float)num60 + 0.5f - num55) + gv * ((float)num61 + 0.5f - num56);
										float num64 = Math.Max(num57 + num8, num57 + num13 + gu * ju + gv * jv + num8);
										if (num62 > num59 + Math.Max(num63, 0f) || (num62 > num64 && Math.Abs(num62 - (num57 + num63 + num13)) > num58))
										{
											num47 = 0f;
										}
									}
								}
								else
								{
									float num65 = num52 * x6 + num53 * y5 + num54 * z5 + (0.1f + num39 * 0.003f);
									float num66 = BayerAt(k, num24) - 0.5f;
									int num67 = (int)(num55 + num66);
									int num68 = (int)(num56 - num66);
									if (num67 >= 0 && num68 >= 0 && num67 < shadowSize && num68 < shadowSize && shadowMap[num68 * shadowSize + num67] > num65)
									{
										num47 = shadowFloor;
									}
								}
							}
							float num69 = (num34 * x10 + num35 * x11 + num36 * x12 + (num34 * x13 + num35 * x14 + num36 * x15) * num47) * num38;
							float num70 = (num34 * y9 + num35 * y10 + num36 * y11 + (num34 * y12 + num35 * y13 + num36 * y14) * num47) * num38;
							float num71 = (num34 * z9 + num35 * z10 + num36 * z11 + (num34 * z12 + num35 * z13 + num36 * z14) * num47) * num38;
							float num72 = (float)num45 * 2f;
							num46 = TerrainCache.Tint(c3, (int)(num69 * num72), (int)(num70 * num72), (int)(num71 * num72));
						}
						if (flag2)
						{
							num46 = TerrainCache.Lerp(num46, WindowLit, glow);
						}
						if (multiply != 0)
						{
							num46 = TerrainCache.Multiply(num46, multiply);
						}
						if (num12 > 0f)
						{
							num46 = TerrainCache.Lerp(num46, b3, num12);
						}
						float num73 = Haze(num39, z + num28 * num39);
						if (flag)
						{
							num73 *= 0.4f;
						}
						if (flag5)
						{
							num73 *= ActorHaze;
						}
						if (num73 > 0.002f)
						{
							num46 = TerrainCache.Lerp(num46, scrFog[k], num73);
						}
						pixels[num40] = num46;
						passDepth[num40] = num39;
						if (array != null)
						{
							array[num40] = 0;
						}
						if (!overlayPass)
						{
							sunlit[num40] = 0;
							actorMask[num40] = (flag5 ? ((byte)1) : ((byte)0));
							if (k == num3 && num24 == num4 && num39 < num2)
							{
								num = meshInstance.Tag;
								num2 = num39;
							}
						}
					}
				}
			}
			_meshPick[stripe] = num;
			_meshPickDepth[stripe] = num2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint Texel(uint[] px, int w, int h, bool crisp, float u, float v)
		{
			float num = (u - (float)FastMath.FloorInt(u)) * (float)w;
			float num2 = (v - (float)FastMath.FloorInt(v)) * (float)h;
			if (crisp)
			{
				int num3 = (int)num;
				int num4 = (int)num2;
				if (num3 >= w)
				{
					num3 = w - 1;
				}
				if (num4 >= h)
				{
					num4 = h - 1;
				}
				return px[num4 * w + num3];
			}
			num -= 0.5f;
			num2 -= 0.5f;
			int num5 = FastMath.FloorInt(num);
			int num6 = FastMath.FloorInt(num2);
			int num7 = (int)((num - (float)num5) * 256f);
			int num8 = (int)((num2 - (float)num6) * 256f);
			int num9 = num5 + 1;
			int num10 = num6 + 1;
			if (num5 < 0)
			{
				num5 += w;
			}
			if (num6 < 0)
			{
				num6 += h;
			}
			if (num9 >= w)
			{
				num9 -= w;
			}
			if (num10 >= h)
			{
				num10 -= h;
			}
			uint num11 = px[num6 * w + num5];
			uint num12 = px[num6 * w + num9];
			uint num13 = px[num10 * w + num5];
			uint num14 = px[num10 * w + num9];
			uint num15 = 0u;
			for (int i = 0; i < 32; i += 8)
			{
				int num16 = (int)((num11 >> i) & 0xFF) * (256 - num7) + (int)((num12 >> i) & 0xFF) * num7;
				int num17 = (int)((num13 >> i) & 0xFF) * (256 - num7) + (int)((num14 >> i) & 0xFF) * num7;
				num15 |= (uint)(num16 * (256 - num8) + num17 * num8 >> 16 << i);
			}
			return num15;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Edges(float x0, float y0, float x1, float y1, float x2, float y2, out float a0, out float b0, out float c0, out float a1, out float b1, out float c1)
		{
			float num = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
			if (Math.Abs(num) < 1E-06f)
			{
				a0 = (b0 = (c0 = (a1 = (b1 = (c1 = 0f)))));
				return false;
			}
			float num2 = 1f / num;
			a0 = (y1 - y2) * num2;
			b0 = (x2 - x1) * num2;
			c0 = (x1 * y2 - x2 * y1) * num2;
			a1 = (y2 - y0) * num2;
			b1 = (x0 - x2) * num2;
			c1 = (x2 * y0 - x0 * y2) * num2;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool RowSpan(float a0, float b0, float c0, float a1, float b1, float c1, float py, int minX, int maxX, out int xs, out int xe)
		{
			xs = 0;
			xe = -1;
			float lo = minX;
			float hi = (float)maxX + 0.999f;
			if (!Span(0f - a0, 0f - (b0 * py + c0), ref lo, ref hi))
			{
				return false;
			}
			if (!Span(0f - a1, 0f - (b1 * py + c1), ref lo, ref hi))
			{
				return false;
			}
			float num = 0f - a0 - a1;
			float num2 = 1f - (b0 * py + c0) - (b1 * py + c1);
			if (!Span(0f - num, 0f - num2, ref lo, ref hi))
			{
				return false;
			}
			xs = Math.Max(minX, FastMath.CeilInt(lo - 0.5f));
			xe = Math.Min(maxX, FastMath.FloorInt(hi - 0.5f));
			return xs <= xe;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool RowMid(float a0, float b0, float c0, float a1, float b1, float c1, float py, int minX, int maxX, out float mid)
		{
			mid = -1f;
			float lo = minX;
			float hi = (float)maxX + 0.999f;
			if (!Span(0f - a0, 0f - (b0 * py + c0), ref lo, ref hi))
			{
				return false;
			}
			if (!Span(0f - a1, 0f - (b1 * py + c1), ref lo, ref hi))
			{
				return false;
			}
			float num = 0f - a0 - a1;
			float num2 = 1f - (b0 * py + c0) - (b1 * py + c1);
			if (!Span(0f - num, 0f - num2, ref lo, ref hi))
			{
				return false;
			}
			mid = (lo + hi) * 0.5f;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Span(float a, float b, ref float lo, ref float hi)
		{
			if (a > 1E-09f)
			{
				float num = (0f - b) / a;
				if (num < hi)
				{
					hi = num;
				}
			}
			else if (a < -1E-09f)
			{
				float num2 = (0f - b) / a;
				if (num2 > lo)
				{
					lo = num2;
				}
			}
			else if (b > 0f)
			{
				return false;
			}
			return lo <= hi;
		}

		private static float DissolveNoise(float x, float y, float z)
		{
			float num = ValueNoise3(x, y, z) * 0.7f + ValueNoise3(x * 2.3f + 17.1f, y * 2.3f + 3.7f, z * 2.3f + 9.3f) * 0.3f;
			num = (num - 0.22f) * 1.7857143f;
			if (!(num < 0f))
			{
				if (!(num > 1f))
				{
					return num;
				}
				return 1f;
			}
			return 0f;
		}

		private static float ValueNoise3(float x, float y, float z)
		{
			int num = FastMath.FloorInt(x);
			int num2 = FastMath.FloorInt(y);
			int num3 = FastMath.FloorInt(z);
			float num4 = x - (float)num;
			float num5 = y - (float)num2;
			float num6 = z - (float)num3;
			num4 = num4 * num4 * (3f - 2f * num4);
			num5 = num5 * num5 * (3f - 2f * num5);
			num6 = num6 * num6 * (3f - 2f * num6);
			float num7 = Lattice(num, num2, num3);
			float num8 = Lattice(num + 1, num2, num3);
			float num9 = Lattice(num, num2 + 1, num3);
			float num10 = Lattice(num + 1, num2 + 1, num3);
			float num11 = Lattice(num, num2, num3 + 1);
			float num12 = Lattice(num + 1, num2, num3 + 1);
			float num13 = Lattice(num, num2 + 1, num3 + 1);
			float num14 = Lattice(num + 1, num2 + 1, num3 + 1);
			float num15 = num7 + (num8 - num7) * num4;
			float num16 = num9 + (num10 - num9) * num4;
			float num17 = num11 + (num12 - num11) * num4;
			float num18 = num13 + (num14 - num13) * num4;
			float num19 = num15 + (num16 - num15) * num5;
			float num20 = num17 + (num18 - num17) * num5;
			return num19 + (num20 - num19) * num6;
		}

		private static float Lattice(int x, int y, int z)
		{
			return (float)(TerrainModel.Hash((x * 73856093) ^ (z * 19349663), y * 83492791 + z * 2971) & 0xFFFF) * 1.5259022E-05f;
		}

		private int Pattern(byte kind, byte axis, float x, float y, float z)
		{
			float num = ((axis == 0) ? y : x);
			float num2 = ((axis == 2) ? y : z);
			switch (kind)
			{
			case 1:
			{
				uint num11 = TerrainModel.Hash(FastMath.FloorInt(num * 12f) + 311, FastMath.FloorInt(num2 * 12f) + axis * 977);
				int num12 = (int)((num11 >> 7) & 0xF);
				if (num12 < 2)
				{
					return 70;
				}
				if (num12 == 15)
				{
					return 166;
				}
				return (int)(98 + (num11 & 0x2F));
			}
			case 2:
			{
				float num46 = ((axis == 2) ? (x + y) : num);
				uint num47 = TerrainModel.Hash(FastMath.FloorInt(num46 * 22f), FastMath.FloorInt(z * 3f));
				return ((FastMath.FloorInt(num46 * 22f) % 3 == 0) ? 92 : 108) + (int)(num47 & 0x1F);
			}
			case 3:
			{
				float num40 = num2 * 8f;
				if (num40 - (float)FastMath.FloorInt(num40) < 0.12f)
				{
					return 94;
				}
				uint num41 = TerrainModel.Hash(FastMath.FloorInt(num * 3f), FastMath.FloorInt(num40));
				return (int)(114 + (num41 & 0x17));
			}
			case 4:
			{
				float num42 = z / 0.16f;
				int num43 = FastMath.FloorInt(num42);
				float num44 = num / 0.28f + (((num43 & 1) != 0) ? 0.5f : 0f);
				int num45 = FastMath.FloorInt(num44);
				if (num42 - (float)num43 < 0.12f || num44 - (float)num45 < 0.08f)
				{
					return 86;
				}
				return (int)(108 + (TerrainModel.Hash(num45, num43) & 0x1F));
			}
			case 5:
			{
				float num33 = z / 0.1f;
				int num34 = FastMath.FloorInt(num33);
				int x2 = FastMath.FloorInt(num / 0.16f + (((num34 & 1) != 0) ? 0.5f : 0f));
				if (num33 - (float)num34 < 0.22f)
				{
					return 90;
				}
				return (int)(112 + (TerrainModel.Hash(x2, num34) & 0x1B));
			}
			case 6:
				return (int)(100 + (TerrainModel.Hash(FastMath.FloorInt(num * 30f), FastMath.FloorInt(z * 4f)) & 0x37));
			case 7:
				return (int)(122 + (TerrainModel.Hash(FastMath.FloorInt(num * 25f), FastMath.FloorInt(num2 * 25f)) & 0xB));
			case 9:
				return (int)(106 + (TerrainModel.Hash(FastMath.FloorInt(num * 40f), FastMath.FloorInt(num2 * 8f)) & 0x27));
			case 11:
			{
				uint num24 = TerrainModel.Hash(FastMath.FloorInt(num * 12f), FastMath.FloorInt((num2 - Time * 1.6f) * 10f));
				return (int)(108 + (num24 & 0x2F));
			}
			case 12:
			{
				float num38 = num * 4f - (float)FastMath.FloorInt(num * 4f);
				float num39 = num2 * 4f - (float)FastMath.FloorInt(num2 * 4f);
				if (!(num38 < 0.12f) && !(num39 < 0.12f))
				{
					return 140;
				}
				return 72;
			}
			case 13:
				return (int)(118 + (TerrainModel.Hash(FastMath.FloorInt(num * 5f), FastMath.FloorInt(num2 * 5f)) & 0xF) + (TerrainModel.Hash(FastMath.FloorInt(num * 30f), FastMath.FloorInt(num2 * 30f)) & 7));
			case 14:
			{
				uint num35 = TerrainModel.Hash(FastMath.FloorInt(num * 7f), FastMath.FloorInt(num2 * 7f));
				uint num36 = TerrainModel.Hash(FastMath.FloorInt(num * 23f) + 97, FastMath.FloorInt(num2 * 23f) + 31);
				int num37 = (int)(100 + (num35 & 0x1F) + (num36 & 0xF));
				if (((num36 >> 8) & 0xF) == 0)
				{
					num37 -= 26;
				}
				else if (((num36 >> 12) & 0x1F) == 0)
				{
					num37 += 22;
				}
				return num37;
			}
			case 10:
				return (int)(120 + (TerrainModel.Hash(FastMath.FloorInt(num * 12f), FastMath.FloorInt(num2 * 12f)) & 0xF));
			case 16:
			{
				float num13 = z / 0.62f;
				int num14 = FastMath.FloorInt(num13);
				uint num15 = TerrainModel.Hash(num14, 913);
				float num16 = num / (0.7f + (float)(num15 & 0xFF) * 0.0023529413f) + (float)((num15 >> 8) & 0xFF) * 0.003921569f;
				int num17 = FastMath.FloorInt(num16);
				if (num13 - (float)num14 < 0.06f || num16 - (float)num17 < 0.045f)
				{
					return 78;
				}
				return (int)(98 + (TerrainModel.Hash(num17, num14) & 0x17) + (TerrainModel.Hash(FastMath.FloorInt(num * 9f), FastMath.FloorInt(num2 * 9f)) & 0xF));
			}
			case 17:
			{
				int num18 = FastMath.FloorInt(num * 2.4f);
				int num19 = FastMath.FloorInt(num2 * 2.4f);
				uint num20 = TerrainModel.Hash(num18 + 71, num19 * 3 + axis);
				float num21 = num * 2.4f - (float)num18;
				float num22 = num2 * 2.4f - (float)num19;
				float num23 = num22 - 0.5f - ((float)(num20 & 0xFF) * 0.0062745097f - 0.8f) * (num21 - 0.5f);
				if (num23 > -0.035f && num23 < 0.035f)
				{
					return 158;
				}
				if (((num20 >> 9) & 0x1F) == 0 && num21 > 0.4f && num21 < 0.55f && num22 > 0.3f && num22 < 0.45f)
				{
					return 176;
				}
				return (int)(112 + ((num20 >> 4) & 0xB));
			}
			case 18:
			{
				float num25 = num / 0.86f;
				float num26 = num2 / 0.68f;
				int num27 = FastMath.FloorInt(num25);
				int num28 = FastMath.FloorInt(num26);
				float num29 = num25 - (float)num27;
				float num30 = num26 - (float)num28;
				if (num29 < 0.035f || num30 < 0.045f)
				{
					return 80;
				}
				float num31 = ((num29 < 0.5f) ? num29 : (1f - num29));
				float num32 = ((num30 < 0.5f) ? num30 : (1f - num30));
				if (num31 > 0.07f && num31 < 0.13f && num32 > 0.08f && num32 < 0.15f)
				{
					return 156;
				}
				return (int)(112 + (TerrainModel.Hash(num27 * 5 + axis, num28 * 7) & 0xF) + (TerrainModel.Hash(FastMath.FloorInt(num * 30f), num28) & 3));
			}
			case 19:
			{
				float num3 = num / 1.3f;
				float num4 = num2 / 0.95f;
				int num5 = FastMath.FloorInt(num3);
				int num6 = FastMath.FloorInt(num4);
				if (num3 - (float)num5 < 0.02f || num4 - (float)num6 < 0.025f)
				{
					return 104;
				}
				uint num7 = TerrainModel.Hash(num5 * 3 + 17, num6 * 11 + axis);
				float num8 = (float)(num7 & 0xFF) * 0.024639215f;
				float num9 = ((float)((num7 >> 8) & 0xFF) * 0.003921569f - 0.5f) * 1.4f;
				float num10 = (float)Math.Sin((num2 - num9 * num) * 7.5f + num8 + 0.6f * (float)Math.Sin(num * 3.1f + num8));
				if (num10 > 0.95f)
				{
					return 110;
				}
				if (num10 > 0.88f)
				{
					return 120;
				}
				return (int)(128 + (TerrainModel.Hash(FastMath.FloorInt(num * 7f), FastMath.FloorInt(num2 * 7f)) & 5));
			}
			default:
				return 128;
			}
		}

		private void PrepareNightSky()
		{
			_nightVis = (Air.Indoors ? 0f : _starsVis);
			_nightOn = _nightVis > 0.01f;
			_meteorCount = 0;
			_cometBright = 0f;
			_auroraVis = 0f;
			if (_nightOn)
			{
				_starTurnOn = StarTurnRows(Air, out _st0, out _st1, out _st2);
				int nightNumber = NightNumber;
				PrepareComet(nightNumber);
				PrepareMeteors(nightNumber);
				PrepareAurora(nightNumber);
			}
		}

		private void PrepareComet(int night)
		{
			int num = FloorDiv(night, 6);
			int num2 = night - num * 6;
			float num3;
			switch (num2)
			{
			default:
				num3 = 0f;
				break;
			case 3:
				num3 = 0.75f;
				break;
			case 1:
			case 2:
				num3 = 1f;
				break;
			case 0:
				num3 = 0.7f;
				break;
			}
			float num4 = num3;
			if (ForceComet >= 0f)
			{
				num4 = ForceComet;
			}
			if (!(num4 <= 0.01f))
			{
				uint num5 = Rand(num, 1013);
				bool flag = num % 3 == 1;
				float num6 = (float)(num5 & 0x3FF) / 1023f * 6.2832f + (float)num2 * 0.07f;
				float num7 = 0.2f + (float)((num5 >> 10) & 0xFF) / 255f * 0.3f + (float)num2 * 0.025f;
				float num8 = (float)Math.Cos(num7);
				_cometHead = new Vec3((float)Math.Cos(num6) * num8, (float)Math.Sin(num6) * num8, (float)Math.Sin(num7));
				if (_starTurnOn)
				{
					_cometHead = AmongStarsBack(_st0, _st1, _st2, _cometHead);
				}
				Vec3 sunDisc = Air.SunDisc;
				Vec3 vec = _cometHead * Vec3.Dot(_cometHead, sunDisc) - sunDisc;
				Vec3 vec2 = (new Vec3(0f, 0f, 1f) - _cometHead * _cometHead.Z).Normalized();
				if (Vec3.Dot(vec, vec) < 0.0001f)
				{
					vec = vec2;
				}
				Vec3 vec3 = Vec3.Cross(_cometHead, vec2) * (((num5 & 0x40000000) != 0) ? 1f : (-1f));
				_cometTail = (vec.Normalized() * 0.75f + vec3 * 0.55f).Normalized();
				_cometSide = Vec3.Cross(_cometHead, _cometTail);
				_cometLen = (flag ? 0.75f : (0.38f + (float)((num5 >> 18) & 0x3F) / 63f * 0.16f)) * (0.75f + 0.25f * num4);
				_cometW0 = 0.008f;
				_cometSpread = (flag ? 0.2f : 0.17f);
				_cometCurve = ((((num5 >> 24) & 1) == 0) ? 1f : (-1f)) * (0.1f + (float)((num5 >> 25) & 7) * 0.02f);
				float num9 = 1f / Math.Max(1f, _focal);
				_cometCore = Math.Max(1.2f * num9, 0.0035f);
				_cometComa = Math.Max(3.5f * num9, flag ? 0.017f : 0.013f);
				_cometReachCos = (float)Math.Cos(Math.Min(1.4f, _cometLen * 1.3f + 0.08f));
				_cometBright = num4;
			}
		}

		private uint Comet(uint sky, int c, int r, float nx, float ny, float nz)
		{
			float num = nx * _cometTail.X + ny * _cometTail.Y + nz * _cometTail.Z;
			float num2 = nx * _cometSide.X + ny * _cometSide.Y + nz * _cometSide.Z;
			float num3 = num * num + num2 * num2;
			float num4 = _cometBright * _nightVis;
			float num5 = 0f;
			float num6 = 0f;
			float num7 = 0f;
			float num8 = _cometComa * _cometComa;
			float num9 = ((num3 < num8 * 9f) ? ExpNeg(num3 / num8) : 0f);
			float num10 = ((num3 < num8 * 40f) ? (ExpNeg(num3 / (num8 * 8f)) * 0.22f) : 0f);
			num5 += 150f * num9 + 120f * num10;
			num6 += 255f * num9 + 170f * num10;
			num7 += 205f * num9 + 170f * num10;
			float num11 = _cometCore * _cometCore;
			if (num3 < num11 * 2.2f && (num3 < num11 || BayerAt(c, r) < 0.5f))
			{
				num5 += 255f;
				num6 += 255f;
				num7 += 240f;
			}
			if (num > 0f)
			{
				float cometLen = _cometLen;
				float num12 = num / cometLen;
				if (num12 < 1f)
				{
					float num13 = _cometCurve * num * num / cometLen;
					float num14 = _cometW0 + _cometSpread * num;
					float num15 = (num2 - num13) / num14;
					if (num15 > -1f && num15 < 1f)
					{
						float num16 = 1f - num15 * num15;
						float num17 = 0.75f + 0.35f * ((num15 * (float)Math.Sign(_cometCurve) > 0f) ? (num15 * (float)Math.Sign(_cometCurve)) : 0f);
						float num18 = (1f - num12) * (float)Math.Sqrt(1f - num12) * Math.Min(1f, num / (_cometComa * 1.5f));
						float num19 = 0.72f + 0.42f * CloudNoise.Sample(num15 * 9f + 40f, num * 30f + 11f);
						float num20 = Dither(num18 * Math.Min(1f, num16 * 1.6f) * num17 * num19 * 1.25f, c, r, 6f);
						num5 += 255f * num20;
						num6 += 232f * num20;
						num7 += 196f * num20;
					}
				}
				float num21 = num / (cometLen * 1.3f);
				if (num21 < 1f)
				{
					float num22 = _cometW0 * 0.6f + _cometSpread * 0.2f * num;
					float num23 = num2 / num22;
					if (num23 > -1f && num23 < 1f)
					{
						float num24 = 1f - num23 * num23;
						float num25 = (1f - num21) * num24 * num24 * (0.55f + 0.5f * CloudNoise.Sample(num23 * 3f + 90f, num * 55f - Time * 0.6f));
						num25 = Dither(num25 * 0.7f * Math.Min(1f, num / (_cometComa * 2f)), c, r + 1, 5f);
						num5 += 110f * num25;
						num6 += 160f * num25;
						num7 += 255f * num25;
					}
				}
			}
			return AddLight(sky, num5 * num4, num6 * num4, num7 * num4);
		}

		private void PrepareMeteors(int night)
		{
			_shower = ((ForceShower >= 0f) ? (ForceShower > 0.5f) : (Rand(night, 4099) % 5 == 0));
			float num = (_shower ? 0.4f : 7f);
			uint num2 = Rand(night, 811);
			float num3 = (float)(num2 & 0x3FF) / 1023f * 6.2832f;
			float num4 = 0.55f + (float)((num2 >> 10) & 0xFF) / 255f * 0.6f;
			Vec3 vec = (_radiant = new Vec3((float)Math.Cos(num3) * (float)Math.Cos(num4), (float)Math.Sin(num3) * (float)Math.Cos(num4), (float)Math.Sin(num4)));
			float num5 = 1f / Math.Max(1f, _focal);
			int num6 = FastMath.FloorInt(Time / num);
			int num7 = (int)(5f / num) + 1;
			for (int i = num6 - num7; i <= num6 && _meteorCount < _meteors.Length; i++)
			{
				uint num8 = Rand(i, 2203 + night * 131);
				if ((num8 & 0xFF) > (uint)(_shower ? 235 : 200))
				{
					continue;
				}
				bool flag = ((num8 >> 8) & 0x1F) == 0;
				float num9 = (float)i * num + (float)((num8 >> 13) & 0xFF) / 255f * num * 0.9f;
				float num10 = (flag ? (1.6f + (float)((num8 >> 21) & 7) * 0.08f) : (0.35f + (float)((num8 >> 21) & 7) * 0.07f));
				float num11 = (flag ? 3.2f : 0.25f);
				float num12 = Time - num9;
				if (num12 < 0f || num12 > num10 + num11)
				{
					continue;
				}
				uint num13 = Rand((int)num8, i + 99);
				Vec3 vec5;
				Vec3 vec6;
				if (_shower && !flag)
				{
					float num14 = (float)(num13 & 0x3FF) / 1023f * 6.2832f;
					float num15 = 0.12f + (float)((num13 >> 10) & 0xFF) / 255f * 0.7f;
					Vec3 vec2 = Vec3.Cross(vec, new Vec3(0f, 0f, 1f)).Normalized();
					Vec3 vec3 = Vec3.Cross(vec, vec2);
					Vec3 vec4 = vec2 * (float)Math.Cos(num14) + vec3 * (float)Math.Sin(num14);
					vec5 = (vec * (float)Math.Cos(num15) + vec4 * (float)Math.Sin(num15)).Normalized();
					vec6 = (vec4 * (float)Math.Cos(num15) - vec * (float)Math.Sin(num15)).Normalized();
				}
				else
				{
					float num16 = (float)(num13 & 0x3FF) / 1023f * 6.2832f;
					float num17 = 0.3f + (float)((num13 >> 10) & 0xFF) / 255f * 0.75f;
					float num18 = (float)Math.Cos(num17);
					vec5 = new Vec3((float)Math.Cos(num16) * num18, (float)Math.Sin(num16) * num18, (float)Math.Sin(num17));
					Vec3 vec7 = Vec3.Cross(new Vec3(0f, 0f, 1f), vec5).Normalized();
					Vec3 vec8 = Vec3.Cross(vec5, vec7);
					float num19 = ((float)((num13 >> 18) & 0xFF) / 255f - 0.5f) * 2.4f;
					vec6 = (vec8 * (0f - (float)Math.Cos(num19)) + vec7 * (float)Math.Sin(num19)).Normalized();
				}
				float num20 = (flag ? (0.24f + (float)((num13 >> 26) & 3) * 0.03f) : (0.6f + (float)((num13 >> 26) & 7) * 0.09f));
				float num21 = Math.Min(num12, num10);
				Vec3 vec9 = (vec5 + vec6 * (num20 * num21)).Normalized();
				Vec3 vec10 = (vec6 * -1f - vec9 * Vec3.Dot(vec6 * -1f, vec9)).Normalized();
				ref Meteor reference = ref _meteors[_meteorCount];
				reference.Head = vec9;
				reference.Back = vec10;
				reference.Side = Vec3.Cross(vec9, vec10);
				float val = Math.Min(num20 * num21, flag ? 0.32f : (0.13f + (float)((num13 >> 6) & 7) * 0.016f));
				reference.Len = Math.Max(val, 2f * num5);
				float num23;
				if (num12 < num10)
				{
					float num22 = num12 / num10;
					num23 = ((num22 < 0.15f) ? (num22 / 0.15f) : (1f - (num22 - 0.15f) / 0.85f * 0.8f));
					if (flag && num22 > 0.6f && num22 < 0.75f)
					{
						num23 += 0.8f;
					}
					reference.Train = (flag ? (0.35f * Math.Min(1f, num22 * 3f)) : 0f);
				}
				else
				{
					num23 = 0f;
					float num24 = 1f - (num12 - num10) / num11;
					reference.Train = (flag ? 0.5f : 0.3f) * num24 * num24;
					if (reference.Train < 0.02f)
					{
						continue;
					}
				}
				reference.Bright = num23;
				reference.Width = (flag ? 1.6f : 0.8f) * num5 * (1f + ((num12 > num10) ? ((num12 - num10) * 0.6f) : 0f));
				reference.HeadSize = (flag ? 2.6f : 1.2f) * num5;
				bool flag2 = ((num13 >> 29) & 3) == 0;
				reference.Color = (flag ? TerrainCache.Rgb(180, 255, 200) : (flag2 ? TerrainCache.Rgb(255, 236, 190) : TerrainCache.Rgb(220, 236, 255)));
				reference.TrainColor = (flag ? TerrainCache.Rgb(255, 150, 70) : TerrainCache.Rgb(170, 190, 255));
				Vec3 mid = (vec9 + vec10 * (reference.Len * 0.5f)).Normalized();
				reference.Mid = mid;
				reference.ReachCos = (float)Math.Cos(reference.Len * 0.5f + reference.Width * 3f + reference.HeadSize * 3f + 0.01f);
				_meteorCount++;
			}
		}

		private uint Meteors(uint sky, int c, int r, float nx, float ny, float nz)
		{
			for (int i = 0; i < _meteorCount; i++)
			{
				ref Meteor reference = ref _meteors[i];
				if (nx * reference.Mid.X + ny * reference.Mid.Y + nz * reference.Mid.Z < reference.ReachCos)
				{
					continue;
				}
				float num = nx * reference.Back.X + ny * reference.Back.Y + nz * reference.Back.Z;
				float num2 = nx * reference.Side.X + ny * reference.Side.Y + nz * reference.Side.Z;
				float num3 = 0f;
				float num4 = 0f;
				if (num > 0f - reference.Width && num < reference.Len)
				{
					float num5 = 1f - Math.Abs(num2) / reference.Width;
					if (num5 > 0f)
					{
						float num6 = 1f - Math.Max(0f, num) / reference.Len;
						num3 += reference.Bright * num6 * num6 * num5;
						num4 += reference.Train * (0.4f + 0.6f * num6) * num5;
					}
				}
				float num7 = num * num + num2 * num2;
				float num8 = reference.HeadSize * reference.HeadSize;
				if (num7 < num8 * 4f)
				{
					num3 += reference.Bright * ExpNeg(num7 / num8) * 1.2f;
				}
				num3 = Dither(Math.Min(1.5f, num3), c, r, 5f) * _nightVis;
				num4 = Dither(num4, c + 1, r, 4f) * _nightVis;
				if (!(num3 <= 0f) || !(num4 <= 0f))
				{
					uint color = reference.Color;
					uint trainColor = reference.TrainColor;
					sky = AddLight(sky, (float)(color & 0xFF) * num3 + (float)(trainColor & 0xFF) * num4, (float)((color >> 8) & 0xFF) * num3 + (float)((trainColor >> 8) & 0xFF) * num4, (float)((color >> 16) & 0xFF) * num3 + (float)((trainColor >> 16) & 0xFF) * num4);
				}
			}
			return sky;
		}

		private void PrepareAurora(int night)
		{
			if (_terrain != null && _snowCheck != FastMath.FloorInt(Time * 2f))
			{
				_snowCheck = FastMath.FloorInt(Time * 2f);
				_snowNear = SnowAround();
			}
			float num = Math.Max(ColdSky, Math.Min(1f, _snowNear * 1.6f));
			uint num2 = Rand(night, 3301);
			float num3 = Math.Max((num2 % 4 == 0) ? (0.55f + (float)((num2 >> 8) & 3) * 0.12f) : 0f, num * 0.95f);
			if (ForceAurora >= 0f)
			{
				num3 = ForceAurora;
			}
			_auroraVis = num3 * _nightVis;
			if (_auroraVis < 0.02f)
			{
				_auroraVis = 0f;
				return;
			}
			if (_auBase.Length < _cw)
			{
				_auBase = new float[_colS.Length];
				_auHeight = new float[_colS.Length];
				_auBright = new float[_colS.Length];
			}
			float num4 = Time + (float)((num2 >> 12) & 0xFF) * 3.1f;
			for (int i = 0; i < _cw; i++)
			{
				float num5 = _invLen[i];
				float x = _colDx[i] * num5;
				float num6 = FastAtan2(_colDy[i] * num5, x);
				float num7 = Math.Abs(num6);
				if (num7 > 1.75f)
				{
					_auBright[i] = 0f;
					continue;
				}
				float num8 = ((num7 < 1.1f) ? 1f : (1f - (num7 - 1.1f) / 0.65f));
				float num9 = CloudNoise.Sample(num6 * 22f + num4 * 0.05f + 30f, num4 * 0.03f + 60f);
				float num10 = CloudNoise.Sample(num6 * 48f - num4 * 0.11f + 150f, 20f + num4 * 0.05f);
				float num11 = 0.6f * Ray(num6 * 120f + num4 * 0.35f) + 0.4f * Ray(num6 * 270f - num4 * 0.8f + 57f);
				num11 *= num11;
				float num12 = (0.32f + 0.68f * Smooth01((num9 - 0.2f) * 2.4f)) * (0.6f + 0.6f * num10) * (0.55f + 0.45f * (float)Math.Cos(num6 * 0.8f));
				_auBright[i] = num12 * (0.45f + 0.85f * num11) * num8;
				_auBase[i] = 0.05f + 0.13f * num10 + 0.05f * (float)Math.Sin(num6 * 3.1f + num4 * 0.07f);
				_auHeight[i] = 0.2f + 0.36f * num9 * (0.7f + 0.3f * num11);
			}
		}

		private uint Aurora(uint sky, int c, int r, float e)
		{
			float num = _auBright[c];
			if (num <= 0.005f)
			{
				return sky;
			}
			float num2 = _auBase[c];
			float num3 = (e - num2) / _auHeight[c];
			float num4 = ((num3 < 0f) ? ((num3 > -0.15f) ? ((1f + num3 / 0.15f) * 0.35f) : 0f) : ((!(num3 < 0.06f)) ? ExpNeg((num3 - 0.06f) * 1.9f) : (0.35f + num3 / 0.06f * 0.65f)));
			if (num4 <= 0.01f)
			{
				return sky;
			}
			num4 = Dither(num4 * num * _auroraVis, c, r, 7f);
			if (num4 <= 0f)
			{
				return sky;
			}
			float num5 = ((num3 < 0f) ? 0f : Math.Min(1f, num3 * 1.3f));
			float num6 = 60f + 170f * num5;
			float num7 = 255f - 190f * num5;
			float num8 = 140f + 80f * num5;
			return AddLight(sky, num6 * num4 * 1.4f, num7 * num4 * 1.4f, num8 * num4 * 1.4f);
		}

		private float SnowAround()
		{
			TerrainCache terrain = _terrain;
			if (terrain == null || terrain.Materials == null || terrain.Materials.Length < terrain.Width * terrain.Height || terrain.Width <= 0)
			{
				return 0f;
			}
			int num = 0;
			int num2 = 0;
			for (int i = -3; i <= 3; i++)
			{
				for (int j = -3; j <= 3; j++)
				{
					int num3 = (int)(_view.X + (float)j * 9f);
					int num4 = (int)(_view.Y + (float)i * 9f);
					if (num3 >= 0 && num4 >= 0 && num3 < terrain.Width && num4 < terrain.Height)
					{
						num2++;
						if (terrain.Materials[num4 * terrain.Width + num3] == 5)
						{
							num++;
						}
					}
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)num / (float)num2;
		}

		private uint MilkyWay(uint sky, int c, int r, float nx, float ny, float nz, float e, out int density)
		{
			density = 7;
			float num = nx * MilkyPole.X + ny * MilkyPole.Y + nz * MilkyPole.Z;
			if (num > 0.3f || num < -0.3f)
			{
				return sky;
			}
			float num2 = nx * MilkyA.X + ny * MilkyA.Y + nz * MilkyA.Z;
			float num3 = nx * MilkyB.X + ny * MilkyB.Y + nz * MilkyB.Z;
			float num4 = CloudNoise.Sample(num2 * 34f + 50f, num3 * 34f + 80f);
			float num5 = CloudNoise.Sample(num2 * 95f + num * 120f + 9f, num3 * 95f - num * 120f + 33f);
			float num6 = ExpNeg(num * num / 0.016f);
			float num7 = num - 0.012f - 0.05f * (num4 - 0.5f);
			float num8 = ExpNeg(num7 * num7 / 0.0006f) * 0.75f;
			float num9 = num6 * (0.25f + 0.85f * num4) * (0.7f + 0.5f * num5) * (1f - num8);
			float num10 = nx * MilkyCore.X + ny * MilkyCore.Y + nz * MilkyCore.Z;
			float num11 = ((num10 > 0f) ? (num10 * num10 * num10 * num10) : 0f);
			num9 *= 0.75f + 0.8f * num11;
			density = 7 + (int)(num9 * 40f);
			float num12 = Dither(num9 * 1f, c, r, 5f) * _nightVis * Math.Min(1f, (e - 0.03f) * 5f);
			if (num12 <= 0f)
			{
				return sky;
			}
			float num13 = 120f + 90f * num11;
			float num14 = 128f + 70f * num11;
			float num15 = 175f + 10f * num11;
			return AddLight(sky, num13 * num12 * 0.5f, num14 * num12 * 0.5f, num15 * num12 * 0.5f);
		}

		private uint NightSky(uint sky, int c, int ac, int r, float nx, float ny, float nz, float e)
		{
			if (_starTurnOn)
			{
				float nx2 = _st0.X * nx + _st0.Y * ny + _st0.Z * nz;
				float ny2 = _st1.X * nx + _st1.Y * ny + _st1.Z * nz;
				float nz2 = _st2.X * nx + _st2.Y * ny + _st2.Z * nz;
				sky = MilkyWay(sky, c, r, nx2, ny2, nz2, e, out var density);
				sky = Stars(sky, nx2, ny2, nz2, e, density);
			}
			else
			{
				sky = MilkyWay(sky, c, r, nx, ny, nz, e, out var density2);
				sky = Stars(sky, nx, ny, nz, e, density2);
			}
			if (_auroraVis > 0f)
			{
				sky = Aurora(sky, ac, r, e);
			}
			if (_cometBright > 0f && nx * _cometHead.X + ny * _cometHead.Y + nz * _cometHead.Z > _cometReachCos)
			{
				sky = Comet(sky, c, r, nx, ny, nz);
			}
			if (_meteorCount > 0)
			{
				sky = Meteors(sky, c, r, nx, ny, nz);
			}
			return sky;
		}

		internal static bool StarTurnRows(Atmosphere a, out Vec3 r0, out Vec3 r1, out Vec3 r2)
		{
			r0 = new Vec3(1f, 0f, 0f);
			r1 = new Vec3(0f, 1f, 0f);
			r2 = new Vec3(0f, 0f, 1f);
			if (!a.Natural)
			{
				return false;
			}
			Vec3 vec = a.StarPole.Normalized();
			double num = Math.PI - (double)a.StarTurn;
			float num2 = (float)Math.Cos(num);
			float num3 = (float)Math.Sin(num);
			float num4 = 1f - num2;
			r0 = new Vec3(num2 + num4 * vec.X * vec.X, num4 * vec.X * vec.Y - num3 * vec.Z, num4 * vec.X * vec.Z + num3 * vec.Y);
			r1 = new Vec3(num4 * vec.Y * vec.X + num3 * vec.Z, num2 + num4 * vec.Y * vec.Y, num4 * vec.Y * vec.Z - num3 * vec.X);
			r2 = new Vec3(num4 * vec.Z * vec.X - num3 * vec.Y, num4 * vec.Z * vec.Y + num3 * vec.X, num2 + num4 * vec.Z * vec.Z);
			return true;
		}

		internal static Vec3 AmongStarsBack(Vec3 r0, Vec3 r1, Vec3 r2, Vec3 d)
		{
			return new Vec3(r0.X * d.X + r1.X * d.Y + r2.X * d.Z, r0.Y * d.X + r1.Y * d.Y + r2.Y * d.Z, r0.Z * d.X + r1.Z * d.Y + r2.Z * d.Z);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint AddLight(uint c, float r, float g, float b)
		{
			int num = (int)(c & 0xFF) + (int)r;
			int num2 = (int)((c >> 8) & 0xFF) + (int)g;
			int num3 = (int)((c >> 16) & 0xFF) + (int)b;
			if (num > 255)
			{
				num = 255;
			}
			if (num2 > 255)
			{
				num2 = 255;
			}
			if (num3 > 255)
			{
				num3 = 255;
			}
			if (num < 0)
			{
				num = 0;
			}
			if (num2 < 0)
			{
				num2 = 0;
			}
			if (num3 < 0)
			{
				num3 = 0;
			}
			return (c & 0xFF000000u) | (uint)num | (uint)(num2 << 8) | (uint)(num3 << 16);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Dither(float k, int c, int r, float levels)
		{
			if (k <= 0f)
			{
				return 0f;
			}
			return (float)(int)(k * levels + BayerAt(c, r)) / levels;
		}

		private static float Smooth01(float x)
		{
			x = ((x < 0f) ? 0f : ((x > 1f) ? 1f : x));
			return x * x * (3f - 2f * x);
		}

		private static uint Rand(int a, int b)
		{
			uint num = TerrainModel.Hash(a, b);
			uint num2 = (num ^ (num >> 15)) * 739982445;
			uint num3 = (num2 ^ (num2 >> 12)) * 695872825;
			return num3 ^ (num3 >> 15);
		}

		private static float Ray(float x)
		{
			int num = FastMath.FloorInt(x);
			float num2 = x - (float)num;
			num2 = num2 * num2 * (3f - 2f * num2);
			float num3 = (float)(TerrainModel.Hash(num, 911) & 0x3FF) / 1023f;
			float num4 = (float)(TerrainModel.Hash(num + 1, 911) & 0x3FF) / 1023f;
			return num3 + (num4 - num3) * num2;
		}

		private static int FloorDiv(int a, int b)
		{
			if (a < 0)
			{
				return -((-a + b - 1) / b);
			}
			return a / b;
		}

		private static float FastAtan2(float y, float x)
		{
			float num = Math.Abs(x);
			float num2 = Math.Abs(y);
			float num3 = Math.Max(num, num2);
			float num4 = Math.Min(num, num2);
			if (num3 < 1E-09f)
			{
				return 0f;
			}
			float num5 = num4 / num3;
			float num6 = num5 * num5;
			float num7 = ((-0.046496473f * num6 + 0.15931422f) * num6 - 0.32762277f) * num6 * num5 + num5;
			if (num2 > num)
			{
				num7 = MathF.PI / 2f - num7;
			}
			if (x < 0f)
			{
				num7 = MathF.PI - num7;
			}
			if (!(y < 0f))
			{
				return num7;
			}
			return 0f - num7;
		}

		private Tint3 LightAtEye()
		{
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			for (int i = 0; i < Lights.Count; i++)
			{
				PointLight pointLight = Lights[i];
				float num4 = pointLight.X - _view.X;
				float num5 = pointLight.Y - _view.Y;
				float num6 = pointLight.Z - (_view.Z - 0.3f);
				float num7 = num4 * num4 + num5 * num5 + num6 * num6;
				float num8 = pointLight.Radius * pointLight.Radius;
				if (!(num7 >= num8))
				{
					float num9 = 1f - num7 / num8;
					num9 *= num9 * 0.8f;
					num += pointLight.R * num9;
					num2 += pointLight.G * num9;
					num3 += pointLight.B * num9;
				}
			}
			float num10 = Math.Max(num, Math.Max(num2, num3));
			if (num10 > 0.0001f)
			{
				float num11 = 1f / (1f + num10 / 0.7f);
				num *= num11;
				num2 *= num11;
				num3 *= num11;
			}
			return new Tint3(num, num2, num3);
		}

		private void ApplyPointLights()
		{
			int count = Lights.Count;
			if (count == 0)
			{
				return;
			}
			int num = Width * Height;
			if (_accR.Length != num)
			{
				_accR = new float[num];
				_accG = new float[num];
				_accB = new float[num];
			}
			if (_lightPrep.Length < count)
			{
				_lightPrep = new LightPrep[count * 2];
			}
			for (int i = 0; i < count; i++)
			{
				PointLight pointLight = Lights[i];
				LightPrep lightPrep = new LightPrep
				{
					InvR2 = 1f / Math.Max(0.01f, pointLight.Radius * pointLight.Radius)
				};
				float num2 = pointLight.X - _view.X;
				float num3 = pointLight.Y - _view.Y;
				float num4 = num2 * _fx + num3 * _fy;
				float num5 = Math.Max(0.1f, pointLight.Radius) * 1.001f + 0.01f;
				lightPrep.TLo = num4 - num5;
				lightPrep.THi = num4 + num5;
				float num6 = pointLight.Z - _view.Z;
				float num7 = (_tilted ? (num4 * _lens.Cq + num6 * _lens.Sq) : num4);
				if (num7 - pointLight.Radius < 0.3f)
				{
					if (num7 + pointLight.Radius < 0f)
					{
						lightPrep.C1 = -1;
						_lightPrep[i] = lightPrep;
						continue;
					}
					lightPrep.C0 = 0;
					lightPrep.C1 = Width;
					lightPrep.R0 = 0;
					lightPrep.R1 = Height;
				}
				else
				{
					float num8 = _focal / (num7 - pointLight.Radius);
					float num9 = (float)Width * 0.5f + (num2 * _rx + num3 * _ry) * (_focal / num7);
					float num10 = _horizon - (_tilted ? (num6 * _lens.Cq - num4 * _lens.Sq) : num6) * (_focal / num7);
					float num11 = pointLight.Radius * num8;
					lightPrep.C0 = Math.Max(0, (int)(num9 - num11));
					lightPrep.C1 = Math.Min(Width, (int)(num9 + num11) + 1);
					lightPrep.R0 = Math.Max(0, (int)(num10 - num11));
					lightPrep.R1 = Math.Min(Height, (int)(num10 + num11) + 1);
				}
				_lightPrep[i] = lightPrep;
			}
			float plGain = 1f / _ambLum;
			int num12 = ((!UseParallel) ? 1 : Math.Max(1, Math.Min(32, Environment.ProcessorCount * 2)));
			int plStripeW = (Width + num12 - 1) / num12;
			_plStripeW = plStripeW;
			_plGain = plGain;
			if (num12 > 1)
			{
				For(num12, PointLightStripe);
			}
			else
			{
				PointLightStripe(0);
			}
			for (int j = 0; j < count; j++)
			{
				PointLight pointLight2 = Lights[j];
				if (pointLight2.Glow <= 0f || !Project(pointLight2.X, pointLight2.Y, pointLight2.Z, out var col, out var row, out var depth, out var scale))
				{
					continue;
				}
				int num13 = (int)col;
				int num14 = (int)row;
				if (num13 < 0 || num14 < 0 || num13 >= Width || num14 >= Height || Depth[(Height - 1 - num14) * Width + num13] < depth - 0.35f)
				{
					continue;
				}
				float num15 = Math.Max(1.2f, pointLight2.Glow * scale);
				float num16 = Math.Max(pointLight2.R, Math.Max(pointLight2.G, pointLight2.B));
				if (num16 <= 0f)
				{
					continue;
				}
				int num17 = (int)Math.Ceiling(num15);
				for (int k = Math.Max(0, num14 - num17); k <= Math.Min(Height - 1, num14 + num17); k++)
				{
					for (int l = Math.Max(0, num13 - num17); l <= Math.Min(Width - 1, num13 + num17); l++)
					{
						float num18 = (float)l + 0.5f - col;
						float num19 = (float)k + 0.5f - row;
						float num20 = 1f - (float)Math.Sqrt(num18 * num18 + num19 * num19) / num15;
						if (!(num20 <= 0f))
						{
							num20 *= num20;
							int num21 = (Height - 1 - k) * Width + l;
							if (Depth[num21] < depth - 0.35f)
							{
								num20 *= 0.3f;
							}
							float num22 = num20 * 150f / num16;
							Pixels[num21] = TerrainCache.Add(Pixels[num21], (int)(pointLight2.R * num22), (int)(pointLight2.G * num22), (int)(pointLight2.B * num22));
						}
					}
				}
			}
		}

		private void PointLightStripe(int s)
		{
			int count = Lights.Count;
			int plStripeW = _plStripeW;
			float plGain = _plGain;
			byte[] array = (_portalPass ? _portalMask : null);
			int width = Width;
			int height = Height;
			float[] depth = Depth;
			float[] accR = _accR;
			float[] accG = _accG;
			float[] accB = _accB;
			float[] rowK = _rowK;
			float[] rowD = _rowD;
			float[] scrU = _scrU;
			float fx = _fx;
			float fy = _fy;
			float rx = _rx;
			float ry = _ry;
			uint[] pixels = Pixels;
			LightPrep[] lightPrep = _lightPrep;
			List<PointLight> lights = Lights;
			float x = _view.X;
			float y = _view.Y;
			float z = _view.Z;
			int num = s * plStripeW;
			int num2 = Math.Min(width, num + plStripeW);
			int num3 = num2;
			int num4 = num;
			int num5 = height;
			int num6 = 0;
			for (int i = 0; i < count; i++)
			{
				LightPrep lightPrep2 = lightPrep[i];
				int num7 = Math.Max(num, lightPrep2.C0);
				int num8 = Math.Min(num2, lightPrep2.C1);
				if (num7 >= num8 || lightPrep2.R0 >= lightPrep2.R1)
				{
					continue;
				}
				if (num7 < num3)
				{
					num3 = num7;
				}
				if (num8 > num4)
				{
					num4 = num8;
				}
				if (lightPrep2.R0 < num5)
				{
					num5 = lightPrep2.R0;
				}
				if (lightPrep2.R1 > num6)
				{
					num6 = lightPrep2.R1;
				}
				PointLight pointLight = lights[i];
				float num9 = x + 0f - pointLight.X;
				float num10 = y - pointLight.Y;
				float num11 = z - pointLight.Z;
				float r = pointLight.R;
				float g = pointLight.G;
				float b = pointLight.B;
				float invR = lightPrep2.InvR2;
				float tLo = lightPrep2.TLo;
				float tHi = lightPrep2.THi;
				for (int j = lightPrep2.R0; j < lightPrep2.R1; j++)
				{
					float num12 = rowK[j];
					float num13 = rowD[j];
					int num14 = (height - 1 - j) * width;
					for (int k = num7; k < num8; k++)
					{
						int num15 = num14 + k;
						float num16 = depth[num15];
						if (!(num16 < tLo) && !(num16 > tHi) && (array == null || array[num15] == 0))
						{
							float num17 = scrU[k] * num13;
							float num18 = num9 + (fx + rx * num17) * num16;
							float num19 = num10 + (fy + ry * num17) * num16;
							float num20 = num11 + num12 * num16;
							float num21 = (num18 * num18 + num19 * num19 + num20 * num20) * invR;
							if (!(num21 >= 1f))
							{
								float num22 = 1f - num21;
								num22 *= num22;
								accR[num15] += r * num22;
								accG[num15] += g * num22;
								accB[num15] += b * num22;
							}
						}
					}
				}
			}
			for (int l = num5; l < num6; l++)
			{
				int num23 = (height - 1 - l) * width;
				int m = num23 + num3;
				for (int num24 = num23 + num4; m < num24; m++)
				{
					float num25 = accR[m];
					float num26 = accG[m];
					float num27 = accB[m];
					if (num25 != 0f || num26 != 0f || num27 != 0f)
					{
						accR[m] = (accG[m] = (accB[m] = 0f));
						if (!(num25 + num26 + num27 < 0.004f))
						{
							pixels[m] = TerrainCache.Tint(pixels[m], 256 + (int)(num25 * plGain * 256f), 256 + (int)(num26 * plGain * 256f), 256 + (int)(num27 * plGain * 256f));
						}
					}
				}
			}
		}

		private void ApplyLightShafts()
		{
			Atmosphere air = Air;
			float num = air.Rays * air.SunVisible;
			if (num < 0.02f)
			{
				return;
			}
			Vec3 sunDisc = air.SunDisc;
			float num2 = sunDisc.X * _fx + sunDisc.Y * _fy;
			float num3 = (_tilted ? (num2 * _lens.Cq + sunDisc.Z * _lens.Sq) : num2);
			if (num3 < 0.1f)
			{
				return;
			}
			float num4 = (float)Width * 0.5f + (sunDisc.X * _rx + sunDisc.Y * _ry) / num3 * _focal;
			float num5 = _horizon - (_tilted ? (sunDisc.Z * _lens.Cq - num2 * _lens.Sq) : sunDisc.Z) / num3 * _focal;
			float val = Math.Max(Math.Max(0f - num4, num4 - (float)Width) / (float)Width, Math.Max(0f - num5, num5 - (float)Height) / (float)Height);
			float num6 = 1f - Math.Max(0f, val) * 1.5f;
			if (num6 <= 0f)
			{
				return;
			}
			num *= num6;
			int num7 = (Width + 3) / 4;
			int num8 = (Height + 3) / 4;
			if (_shaftMask.Length != num7 * num8)
			{
				_shaftMask = new float[num7 * num8];
				_shaft = new float[num7 * num8];
			}
			for (int i = 0; i < num8; i++)
			{
				for (int j = 0; j < num7; j++)
				{
					int num9 = Math.Min(Width - 1, j * 4 + 2);
					int num10 = Math.Min(Height - 1, i * 4 + 2);
					int num11 = (Height - 1 - num10) * Width + num9;
					float num12 = 0f;
					if (Depth[num11] == float.MaxValue)
					{
						uint num13 = Pixels[num11];
						num12 = ((float)(num13 & 0xFF) * 0.3f + (float)((num13 >> 8) & 0xFF) * 0.59f + (float)((num13 >> 16) & 0xFF) * 0.11f) / 255f;
						if (num11 < _skyCover.Length)
						{
							num12 *= 1f - (float)(int)_skyCover[num11] * 0.003921569f;
						}
					}
					_shaftMask[i * num7 + j] = num12;
				}
			}
			float shaftQsx = num4 / 4f;
			float shaftQsy = num5 / 4f;
			_shaftQsx = shaftQsx;
			_shaftQsy = shaftQsy;
			For(num8, ShaftRow);
			uint sunColor = air.SunColor;
			float shaftSr = (float)(sunColor & 0xFF) * num;
			float shaftSg = (float)((sunColor >> 8) & 0xFF) * num;
			float shaftSb = (float)((sunColor >> 16) & 0xFF) * num;
			_shaftSr = shaftSr;
			_shaftSg = shaftSg;
			_shaftSb = shaftSb;
			For(Height, ShaftAddRow);
		}

		private void ShaftRow(int y)
		{
			int num = (Width + 3) / 4;
			int num2 = (Height + 3) / 4;
			float shaftQsx = _shaftQsx;
			float shaftQsy = _shaftQsy;
			for (int i = 0; i < num; i++)
			{
				float num3 = (shaftQsx - (float)i) / 28f;
				float num4 = (shaftQsy - (float)y) / 28f;
				float num5 = i;
				float num6 = y;
				float num7 = 0f;
				float num8 = 1f;
				for (int j = 0; j < 28; j++)
				{
					int num9 = (int)num5;
					int num10 = (int)num6;
					if (num9 >= 0 && num10 >= 0 && num9 < num && num10 < num2)
					{
						num7 += _shaftMask[num10 * num + num9] * num8;
					}
					num8 *= 0.94f;
					num5 += num3;
					num6 += num4;
				}
				float num11 = (float)Math.Sqrt(((float)i - shaftQsx) * ((float)i - shaftQsx) + ((float)y - shaftQsy) * ((float)y - shaftQsy)) / (float)num;
				_shaft[y * num + i] = num7 / 28f * (float)Math.Exp((0f - num11) * 2.2f);
			}
		}

		private void ShaftAddRow(int r)
		{
			int num = (Width + 3) / 4;
			int num2 = (Height + 3) / 4;
			float shaftSr = _shaftSr;
			float shaftSg = _shaftSg;
			float shaftSb = _shaftSb;
			int num3 = Math.Min(num2 - 1, r / 4);
			for (int i = 0; i < Width; i++)
			{
				float num4 = _shaft[num3 * num + Math.Min(num - 1, i / 4)];
				if (!(num4 < 0.01f))
				{
					int num5 = (Height - 1 - r) * Width + i;
					Pixels[num5] = TerrainCache.Add(Pixels[num5], (int)(shaftSr * num4), (int)(shaftSg * num4), (int)(shaftSb * num4));
				}
			}
		}

		private void ApplyLensFlare()
		{
			Atmosphere air = Air;
			if (air.SunVisible <= 0.01f)
			{
				return;
			}
			Vec3 sunDisc = air.SunDisc;
			float num = sunDisc.X * _fx + sunDisc.Y * _fy;
			float num2 = (_tilted ? (num * _lens.Cq + sunDisc.Z * _lens.Sq) : num);
			if (num2 < 0.1f)
			{
				return;
			}
			float num3 = (float)Width * 0.5f + (sunDisc.X * _rx + sunDisc.Y * _ry) / num2 * _focal;
			float num4 = _horizon - (_tilted ? (sunDisc.Z * _lens.Cq - num * _lens.Sq) : sunDisc.Z) / num2 * _focal;
			float num5 = FlareStrength(air, num3, num4, Width, Height, SunCloudCover(_view.X, _view.Y, 1f, 1f));
			if (num5 < 0.01f)
			{
				return;
			}
			float num6 = 1f - Math.Min(1f, Math.Max(0f, sunDisc.Z) * 3f);
			float num7 = (0.026f + 0.016f * num6) * _focal * 0.7f;
			float num8 = 0f;
			for (int i = 0; i < 9; i++)
			{
				float num9 = ((i == 0) ? 0f : ((float)Math.Cos((float)i * (MathF.PI / 4f)) * num7));
				float num10 = ((i == 0) ? 0f : ((float)Math.Sin((float)i * (MathF.PI / 4f)) * num7));
				int num11 = Math.Max(0, Math.Min(Width - 1, (int)Math.Floor(num3 + num9)));
				int num12 = Math.Max(0, Math.Min(Height - 1, (int)Math.Floor(num4 + num10)));
				if (Depth[(Height - 1 - num12) * Width + num11] == float.MaxValue)
				{
					num8 += 1f - StormCloudAt(num11, num12);
				}
			}
			num5 *= num8 / 9f;
			if (num5 < 0.01f)
			{
				return;
			}
			uint sunColor = air.SunColor;
			float num13 = sunColor & 0xFF;
			float num14 = (sunColor >> 8) & 0xFF;
			float num15 = (sunColor >> 16) & 0xFF;
			float num16 = Height;
			float num17 = (float)Width * 0.5f;
			float num18 = (float)Height * 0.5f;
			float num19 = Math.Min(1f, (float)Math.Sqrt((num17 - num3) * (num17 - num3) + (num18 - num4) * (num18 - num4)) / (0.35f * num16));
			float num20 = num19 * num19 * (3f - 2f * num19);
			for (int j = 0; j < FlareAlong.Length; j++)
			{
				if (!(num20 > 0f))
				{
					break;
				}
				float num21 = num3 + (num17 - num3) * FlareAlong[j];
				float num22 = num4 + (num18 - num4) * FlareAlong[j];
				float num23 = FlareSize[j] * num16;
				float num24 = FlareWeight[j] * num5 * num20;
				float num25 = num13 * FlareTint[j].X * num24;
				float num26 = num14 * FlareTint[j].Y * num24;
				float num27 = num15 * FlareTint[j].Z * num24;
				int num28 = Math.Max(0, (int)(num21 - num23) - 1);
				int num29 = Math.Min(Width - 1, (int)(num21 + num23) + 1);
				int num30 = Math.Max(0, (int)(num22 - num23) - 1);
				int num31 = Math.Min(Height - 1, (int)(num22 + num23) + 1);
				for (int k = num30; k <= num31; k++)
				{
					for (int l = num28; l <= num29; l++)
					{
						float num32 = (float)l + 0.5f - num21;
						float num33 = (float)k + 0.5f - num22;
						float num34 = FlareGhostAt(FlareRing[j], (float)Math.Sqrt(num32 * num32 + num33 * num33) / num23, BayerAt(l, k));
						if (!(num34 <= 0f))
						{
							int num35 = (Height - 1 - k) * Width + l;
							Pixels[num35] = TerrainCache.Add(Pixels[num35], (int)(num25 * num34), (int)(num26 * num34), (int)(num27 * num34));
						}
					}
				}
			}
			float num36 = 0.11f * num16;
			int num37 = Math.Max(0, (int)(num3 - num36) - 1);
			int num38 = Math.Min(Width - 1, (int)(num3 + num36) + 1);
			int num39 = Math.Max(0, (int)(num4 - num36) - 1);
			int num40 = Math.Min(Height - 1, (int)(num4 + num36) + 1);
			int r = (int)(num13 * 0.3f * num5);
			int g = (int)(num14 * 0.3f * num5);
			int b = (int)(num15 * 0.3f * num5);
			for (int m = num39; m <= num40; m++)
			{
				for (int n = num37; n <= num38; n++)
				{
					if (FlareRayAt((float)n + 0.5f - num3, (float)m + 0.5f - num4, num16, BayerAt(n, m)))
					{
						int num41 = (Height - 1 - m) * Width + n;
						Pixels[num41] = TerrainCache.Add(Pixels[num41], r, g, b);
					}
				}
			}
		}

		internal static float FlareStrength(Atmosphere a, float sx, float sy, float w, float h, float cloud)
		{
			if (a.Indoors || a.SunVisible <= 0.01f)
			{
				return 0f;
			}
			float val = Math.Max(Math.Max(0f - sx, sx - w) / w, Math.Max(0f - sy, sy - h) / h);
			float num = 1f - Math.Max(0f, val) * 1.5f;
			if (num <= 0f)
			{
				return 0f;
			}
			float num2 = sx / w * 2f - 1f;
			float num3 = sy / h * 2f - 1f;
			float num4 = 1f - Math.Min(1f, (float)Math.Sqrt(num2 * num2 + num3 * num3) * 0.7071f);
			return a.SunVisible * (1f - cloud) * num * (0.45f + 0.55f * num4);
		}

		private static float FlareGhostAt(bool ring, float d, float b)
		{
			if ((ring ? (1f - Math.Abs(d - 0.85f) / 0.15f) : ((1f - d) / 0.18f)) <= b)
			{
				return 0f;
			}
			if (ring || !(d > 0.78f))
			{
				return 1f;
			}
			return 1.35f;
		}

		private static bool FlareRayAt(float dx, float dy, float h, float b)
		{
			float num = Math.Abs(dx);
			float num2 = Math.Abs(dy);
			float num3 = 0.11f * h;
			float num4 = 0.055f * h;
			float num5 = 0f;
			if (num2 < 0.5f && num < num3)
			{
				num5 = 1f - num / num3;
			}
			else if (num < 0.5f && num2 < num3)
			{
				num5 = 1f - num2 / num3;
			}
			else if (Math.Abs(num - num2) < 0.5f && num < num4 * 0.7071f)
			{
				num5 = 1f - num * 1.4142f / num4;
			}
			return num5 * num5 > b;
		}

		private void ApplyWindowBeams()
		{
			Atmosphere air = Air;
			Vec3 vec = new Vec3(0f - _light.X, 0f - _light.Y, 0f - _light.Z);
			float num = 1f - air.Night;
			if (num < 0.05f)
			{
				return;
			}
			uint sunColor = air.SunColor;
			float wbSr = (float)(sunColor & 0xFF) / 255f;
			float wbSg = (float)((sunColor >> 8) & 0xFF) / 255f;
			float wbSb = (float)((sunColor >> 16) & 0xFF) / 255f;
			Vec3 vec2 = new Vec3(_view.X, _view.Y, _view.Z);
			int width = Width;
			if (_beamWin.Length < Windows.Count * 9)
			{
				_beamWin = new float[Windows.Count * 9];
			}
			if (_beamCol.Length < Windows.Count * 2 * width)
			{
				_beamCol = new float[Windows.Count * 2 * width];
			}
			if (_beamLen.Length < width)
			{
				_beamLen = new float[width];
			}
			float[] beamWin = _beamWin;
			float[] beamCol = _beamCol;
			float[] beamLen = _beamLen;
			bool tilted = _tilted;
			float cq = _lens.Cq;
			float sq = _lens.Sq;
			for (int i = 0; i < width; i++)
			{
				float num2 = ((float)i + 0.5f - (float)Width * 0.5f) / _focal;
				float num3 = _fx + _rx * num2;
				float num4 = _fy + _ry * num2;
				beamLen[i] = (tilted ? (1f + num2 * num2) : (num3 * num3 + num4 * num4));
			}
			int num5 = 0;
			for (int j = 0; j < Windows.Count; j++)
			{
				LightWindow lightWindow = Windows[j];
				float num6 = Vec3.Dot(vec, lightWindow.Inward);
				if (!(num6 <= 0.05f))
				{
					Vec3 a = vec2 - lightWindow.Center;
					float num7 = Vec3.Dot(a, lightWindow.Inward) / num6;
					float num8 = Vec3.Dot(vec, lightWindow.Right);
					int num9 = num5 * 9;
					beamWin[num9] = num6;
					beamWin[num9 + 1] = num7;
					beamWin[num9 + 2] = num8;
					beamWin[num9 + 3] = Vec3.Dot(a, lightWindow.Right) - num7 * num8;
					beamWin[num9 + 4] = a.Z - num7 * vec.Z;
					beamWin[num9 + 5] = lightWindow.HalfWidth;
					beamWin[num9 + 6] = lightWindow.HalfHeight;
					beamWin[num9 + 7] = lightWindow.Inward.Z * cq - sq * (_fx * lightWindow.Inward.X + _fy * lightWindow.Inward.Y);
					beamWin[num9 + 8] = lightWindow.Right.Z * cq - sq * (_fx * lightWindow.Right.X + _fy * lightWindow.Right.Y);
					int num10 = num5 * 2 * width;
					for (int k = 0; k < width; k++)
					{
						float num11 = ((float)k + 0.5f - (float)Width * 0.5f) / _focal;
						float num12 = _fx * cq + _rx * num11;
						float num13 = _fy * cq + _ry * num11;
						beamCol[num10 + k] = num12 * lightWindow.Inward.X + num13 * lightWindow.Inward.Y + sq * lightWindow.Inward.Z;
						beamCol[num10 + width + k] = num12 * lightWindow.Right.X + num13 * lightWindow.Right.Y + sq * lightWindow.Right.Z;
					}
					num5++;
				}
			}
			if (num5 != 0)
			{
				_wbCount = num5;
				_wbLight = vec;
				_wbDay = num;
				_wbSr = wbSr;
				_wbSg = wbSg;
				_wbSb = wbSb;
				For(Height, WindowBeamRow);
			}
		}

		private void WindowBeamRow(int r)
		{
			int width = Width;
			int wbCount = _wbCount;
			bool tilted = _tilted;
			bool natural = _natural;
			float[] beamWin = _beamWin;
			float[] beamCol = _beamCol;
			float[] beamLen = _beamLen;
			byte[] array = (_portalPass ? _portalMask : null);
			float[] rowK = _rowK;
			float[] rowD = _rowD;
			Vec3 wbLight = _wbLight;
			float z = wbLight.Z;
			float wbDay = _wbDay;
			float wbSr = _wbSr;
			float wbSg = _wbSg;
			float wbSb = _wbSb;
			float num = (_horizon - ((float)r + 0.5f)) / _focal;
			float num2 = rowK[r];
			float num3 = rowD[r];
			float num4 = num * num;
			for (int i = 0; i < width; i++)
			{
				int num5 = (Height - 1 - r) * width + i;
				float num6 = Depth[num5];
				if (num6 == float.MaxValue)
				{
					num6 = 30f;
				}
				float num7 = (tilted ? ((float)Math.Sqrt(beamLen[i] + num4) * num3) : ((float)Math.Sqrt(beamLen[i] + num4)));
				float num8 = 0f;
				float num9 = 0f;
				for (int j = 0; j < wbCount; j++)
				{
					int num10 = j * 9;
					int num11 = j * 2 * width + i;
					float num12 = (beamCol[num11] + num * beamWin[num10 + 7]) * num3 / beamWin[num10];
					float b = (beamCol[num11 + width] + num * beamWin[num10 + 8]) * num3 - num12 * beamWin[num10 + 2];
					float b2 = num2 - num12 * z;
					float t = 0f;
					float t2 = num6;
					float num13 = beamWin[num10 + 5];
					float num14 = beamWin[num10 + 6];
					if (Clip(beamWin[num10 + 1], num12, 0f, 40f, ref t, ref t2) && Clip(beamWin[num10 + 3], b, 0f - num13, num13, ref t, ref t2) && Clip(beamWin[num10 + 4], b2, 0f - num14, num14, ref t, ref t2))
					{
						num8 += (t2 - t) * num7;
						if (t2 >= num6 - 0.02f)
						{
							num9 = 1f;
						}
					}
				}
				if (array != null && array[num5] != 0)
				{
					num9 = 0f;
				}
				if (num9 > 0f && natural && Depth[num5] != float.MaxValue && !FacesLight(i, r, num6, wbLight))
				{
					num9 = 0f;
				}
				if (!(num8 <= 0f) || !(num9 <= 0f))
				{
					uint c = Pixels[num5];
					if (num9 > 0f)
					{
						c = TerrainCache.Tint(c, 256 + (int)(wbSr * 300f * wbDay), 256 + (int)(wbSg * 290f * wbDay), 256 + (int)(wbSb * 250f * wbDay));
					}
					float num15 = Math.Min(0.6f, num8 * 0.2f) * wbDay;
					uint num16 = TerrainModel.Hash(i * 3 + (int)(Time * 2f), r * 7 - (int)(Time * 3f));
					if (num8 > 0.3f && (num16 & 0xFF) < 3)
					{
						num15 += 0.35f;
					}
					Pixels[num5] = TerrainCache.Add(c, (int)(wbSr * 255f * num15), (int)(wbSg * 240f * num15), (int)(wbSb * 210f * num15));
				}
			}
		}

		private bool FacesLight(int c, int r, float t, Vec3 d)
		{
			int width = Width;
			int height = Height;
			float[] depth = Depth;
			float num = ((c > 0) ? depth[(height - 1 - r) * width + c - 1] : float.MaxValue);
			float num2 = ((c + 1 < width) ? depth[(height - 1 - r) * width + c + 1] : float.MaxValue);
			float num3 = ((r > 0) ? depth[(height - r) * width + c] : float.MaxValue);
			float num4 = ((r + 1 < height) ? depth[(height - 2 - r) * width + c] : float.MaxValue);
			bool flag = Math.Abs(num2 - t) <= Math.Abs(num - t);
			bool flag2 = Math.Abs(num4 - t) <= Math.Abs(num3 - t);
			float num5 = (flag ? num2 : num);
			float num6 = (flag2 ? num4 : num3);
			if (num5 == float.MaxValue || num6 == float.MaxValue)
			{
				return true;
			}
			Vec3 vec = PixelPoint(c, r, t);
			Vec3 vec2 = PixelPoint(flag ? (c + 1) : (c - 1), r, num5);
			Vec3 vec3 = PixelPoint(c, flag2 ? (r + 1) : (r - 1), num6);
			Vec3 vec4 = Vec3.Cross(new Vec3(vec2.X - vec.X, vec2.Y - vec.Y, vec2.Z - vec.Z), new Vec3(vec3.X - vec.X, vec3.Y - vec.Y, vec3.Z - vec.Z));
			float num7 = vec4.X * (_view.X - vec.X) + vec4.Y * (_view.Y - vec.Y) + vec4.Z * (_view.Z - vec.Z);
			return (vec4.X * d.X + vec4.Y * d.Y + vec4.Z * d.Z) * num7 < 0f;
		}

		private Vec3 PixelPoint(int c, int r, float t)
		{
			float num = ((float)c + 0.5f - (float)Width * 0.5f) / _focal * _rowD[r];
			return new Vec3(_view.X + (_fx + _rx * num) * t, _view.Y + (_fy + _ry * num) * t, _view.Z + _rowK[r] * t);
		}

		private static bool Clip(float a, float b, float lo, float hi, ref float t0, ref float t1)
		{
			if (Math.Abs(b) < 1E-06f)
			{
				if (a >= lo)
				{
					return a <= hi;
				}
				return false;
			}
			float num = (lo - a) / b;
			float num2 = (hi - a) / b;
			if (num > num2)
			{
				float num3 = num;
				num = num2;
				num2 = num3;
			}
			if (num > t0)
			{
				t0 = num;
			}
			if (num2 < t1)
			{
				t1 = num2;
			}
			return t0 < t1;
		}

		private void ApplyFinish()
		{
			if (LensFlare && !Air.Indoors && !ForBackdrop)
			{
				ApplyLensFlare();
			}
			if (!Finish)
			{
				return;
			}
			PrepareFinish();
			bool flag = PaletteLevels >= 2;
			int finSat = (int)((Air.Indoors ? 0.98f : (1.07f + 0.05f * Air.Night)) * 256f);
			if (flag && _curveQuantStale)
			{
				for (int i = 0; i < 16; i++)
				{
					for (int j = 0; j < 256; j++)
					{
						_curveQuant[(i << 8) + j] = _quant[(i << 8) + _curve[j]];
					}
				}
				_curveQuantStale = false;
			}
			_finQuant = flag;
			_finSat = finSat;
			For((Height + 7) / 8, FinishBand);
		}

		private void FinishBand(int band)
		{
			int width = Width;
			int height = Height;
			bool finQuant = _finQuant;
			int finSat = _finSat;
			byte[] array = (_portalPass ? _portalMask : null);
			uint[] pixels = Pixels;
			byte[] vignette = _vignette;
			byte[] curve = _curve;
			byte[] curveQuant = _curveQuant;
			int[] toneR = _toneR;
			int[] toneG = _toneG;
			int[] toneB = _toneB;
			int i = band * 8;
			for (int num = Math.Min(height, i + 8); i < num; i++)
			{
				int num2 = i * width;
				int num3 = ((height - 1 - i) & 3) << 2;
				for (int j = 0; j < width; j++)
				{
					int num4 = num2 + j;
					if (array == null || array[num4] == 0)
					{
						uint num5 = pixels[num4];
						int num6 = (int)(num5 & 0xFF);
						int num7 = (int)((num5 >> 8) & 0xFF);
						int num8 = (int)((num5 >> 16) & 0xFF);
						int num9 = num6 * 77 + num7 * 150 + num8 * 29 >> 8;
						num6 = num9 + ((num6 - num9) * finSat >> 8) + toneR[num9];
						num7 = num9 + ((num7 - num9) * finSat >> 8) + toneG[num9];
						num8 = num9 + ((num8 - num9) * finSat >> 8) + toneB[num9];
						int num10 = vignette[num4];
						num6 = num6 * num10 >> 8;
						num7 = num7 * num10 >> 8;
						num8 = num8 * num10 >> 8;
						num6 = ((num6 >= 0) ? ((num6 > 255) ? 255 : num6) : 0);
						num7 = ((num7 >= 0) ? ((num7 > 255) ? 255 : num7) : 0);
						num8 = ((num8 >= 0) ? ((num8 > 255) ? 255 : num8) : 0);
						if (finQuant)
						{
							int num11 = (num3 | (j & 3)) << 8;
							num6 = curveQuant[num11 + num6];
							num7 = curveQuant[num11 + num7];
							num8 = curveQuant[num11 + num8];
						}
						else
						{
							num6 = curve[num6];
							num7 = curve[num7];
							num8 = curve[num8];
						}
						pixels[num4] = (uint)(num6 | (num7 << 8) | (num8 << 16) | -16777216);
					}
				}
			}
		}

		private void PrepareFinish()
		{
			if (_vignetteW != Width || _vignetteH != Height || _vignetteFor != Vignette)
			{
				_vignetteW = Width;
				_vignetteH = Height;
				_vignetteFor = Vignette;
				_vignette = new byte[Width * Height];
				for (int i = 0; i < Height; i++)
				{
					for (int j = 0; j < Width; j++)
					{
						float num = ((float)j + 0.5f) / (float)Width * 2f - 1f;
						float num2 = ((float)i + 0.5f) / (float)Height * 2f - 1f;
						float num3 = (num * num * 0.8f + num2 * num2) * 0.55f;
						_vignette[i * Width + j] = (byte)Math.Max(0, Math.Min(255, (int)(256f * (1f - Vignette * num3 * num3 * 1.6f))));
					}
				}
			}
			if (_quantLevels != PaletteLevels && PaletteLevels >= 2)
			{
				_quantLevels = PaletteLevels;
				_curveQuantStale = true;
				float num4 = 255f / (float)(PaletteLevels - 1);
				for (int k = 0; k < 16; k++)
				{
					float num5 = Bayer[k] - 0.5f;
					for (int l = 0; l < 256; l++)
					{
						float num6 = (float)Math.Floor((float)l / num4 + 0.5f + num5) * num4;
						_quant[(k << 8) + l] = (byte)Math.Max(0, Math.Min(255, (int)(num6 + 0.5f)));
					}
				}
			}
			float night = Air.Night;
			if (Math.Abs(_toneNight - night) > 0.02f)
			{
				_toneNight = night;
				_curveQuantStale = true;
				for (int m = 0; m < 256; m++)
				{
					float num7 = (float)m / 255f;
					float num8 = (1f - num7) * (1f - num7);
					float num9 = num7 * num7;
					_toneR[m] = (int)(num8 * (-4f + 2f * night) + num9 * 12f);
					_toneG[m] = (int)(num8 * (-6f - 2f * night) + num9 * 5f);
					_toneB[m] = (int)(num8 * (16f + 10f * night) + num9 * -8f);
					float num10 = ((num7 < 0.5f) ? (2f * num7 * num7) : (1f - 2f * (1f - num7) * (1f - num7)));
					float num11 = num7 + (num10 - num7) * 0.28f + 0.02f * (1f - num7) * (1f - num7);
					_curve[m] = (byte)Math.Max(0, Math.Min(255, (int)(num11 * 255f + 0.5f)));
				}
			}
		}

		private void PrepareSea(TerrainCache terrain, float x, float y)
		{
			_sea = SeaState(x, y);
			_waveRange = Sea.Range;
			_waveLift = ((Sea.Lift > 0.45f) ? Sea.Lift : 0f);
			_seaW = terrain.Width;
			_seaH = terrain.Height;
			byte[] seaReach = terrain.SeaReach;
			_seaMap = ((seaReach.Length == _seaW * _seaH && seaReach.Length != 0) ? seaReach : null);
			_seaOutside = (terrain.OutsideIsWater ? 255 : 0);
			PrepareCaps();
		}

		public float SeaState(float x, float y)
		{
			Weather weather = Weather;
			if (weather == null)
			{
				return 1f;
			}
			float num = (Wet(weather.Global) ? weather.GlobalStrength : 0f);
			List<StormCloud> storms = weather.Storms;
			for (int i = 0; i < storms.Count; i++)
			{
				StormCloud stormCloud = storms[i];
				if (Wet(stormCloud.Drops) && !(stormCloud.HalfX <= 0f) && !(stormCloud.HalfY <= 0f))
				{
					float num2 = (x - stormCloud.X) / stormCloud.HalfX;
					float num3 = (y - stormCloud.Y) / stormCloud.HalfY;
					num = Math.Max(num, stormCloud.Opacity * Math.Max(0f, Math.Min(1f, 1.6f - num2 * num2 - num3 * num3)));
				}
			}
			return 1f + 0.8f * Math.Max(0f, Math.Min(1f, num));
		}

		private static bool Wet(Precip p)
		{
			if (p != Precip.Rain)
			{
				return p == Precip.Acid;
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SeaAt(int x, int y)
		{
			if ((uint)x >= (uint)_seaW || (uint)y >= (uint)_seaH)
			{
				return _seaOutside;
			}
			byte[] seaMap = _seaMap;
			if (seaMap == null)
			{
				return 0;
			}
			return seaMap[y * _seaW + x];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int SeaClamped(int x, int y)
		{
			x = ((x >= 0) ? ((x >= _seaW) ? (_seaW - 1) : x) : 0);
			y = ((y >= 0) ? ((y >= _seaH) ? (_seaH - 1) : y) : 0);
			byte[] seaMap = _seaMap;
			if (seaMap == null)
			{
				return 0;
			}
			return seaMap[y * _seaW + x];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float SeaStep(float e0, float e1, float x)
		{
			float num = (x - e0) / (e1 - e0);
			num = ((num < 0f) ? 0f : ((num > 1f) ? 1f : num));
			return num * num * (3f - 2f * num);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float SeaRows(float t, float h)
		{
			return t * t / (_focal * Math.Max(0.4f, _view.Z - h));
		}

		private void PrepareCaps()
		{
			_capRows = 0;
			if (!SeaWaves)
			{
				return;
			}
			float num = 80f / _lodK * 1.6f + 5f;
			int num2 = Math.Min(72, (int)(num * 2f / 5f) + 2);
			float num3 = 0.8f * _view.X + 0.6f * _view.Y;
			float num4 = 0.8f * _view.Y - 0.6f * _view.X;
			_capU0 = FastMath.FloorInt(num3 * 0.2f) - num2 / 2;
			if (_capGrid.Length < num2 * num2)
			{
				_capGrid = new int[num2 * num2];
			}
			if (_capRow.Length < num2)
			{
				_capRow = new int[num2];
			}
			int num5 = 0;
			for (int i = 0; i < num2; i++)
			{
				int num6 = _capU0 + i;
				int num7 = FastMath.FloorInt((num4 + (float)num6 * 2.3f) * 0.2f) - num2 / 2;
				_capRow[i] = num7;
				for (int j = 0; j < num2; j++)
				{
					int num8 = 0;
					if (CapIn(num6, num7 + j, out var cap))
					{
						if (num5 == _caps.Length)
						{
							Array.Resize(ref _caps, num5 * 2);
						}
						_caps[num5++] = cap;
						num8 = num5;
					}
					_capGrid[i * num2 + j] = num8;
				}
			}
			_capRows = num2;
		}

		private bool CapIn(int iu, int iv, out Cap cap)
		{
			cap = default(Cap);
			uint num = TerrainModel.Hash(iu * 7 + 11, iv * 13 + 5);
			float num2 = Time + (float)(num & 0x3FF) * 0.0048828125f;
			int num3 = FastMath.FloorInt(num2 * 0.2f);
			float num4 = num2 - (float)num3 * 5f;
			uint num5 = TerrainModel.Hash(iu + num3 * 7919, iv - num3 * 4111);
			float num6 = 2.2f + (float)((num5 >> 8) & 0xFF) * 0.0047058826f;
			if (num4 >= num6)
			{
				return false;
			}
			float num7 = Math.Min(0.9f, 0.5f * _sea);
			if ((num5 & 0xFF) >= (uint)(num7 * 256f))
			{
				return false;
			}
			float num8 = 0.6f + (float)((num5 >> 16) & 0xFF) * 0.0035294117f;
			float num9 = (float)iu * 5f + 0.5f + (float)(num5 >> 24) * 0.005098039f;
			float num10 = (float)iv * 5f - (float)iu * 2.3f + 2.5f + ((float)((num >> 12) & 0xFF) / 255f - 0.5f) * (5f - 2.6f * num8 - 0.2f);
			float num11 = 0.8f * num9 - 0.6f * num10;
			float num12 = 0.6f * num9 + 0.8f * num10;
			float num13 = ((float)SeaAt(FastMath.FloorInt(num11), FastMath.FloorInt(num12)) * 0.125f - 5f) * 0.125f;
			if (num13 <= 0f || (num5 & 0xFF) >= (uint)(Math.Min(1f, num13) * num7 * 256f))
			{
				return false;
			}
			cap.X = num11;
			cap.Y = num12;
			cap.Half = num8;
			cap.Age = num4;
			cap.Life = num6;
			cap.Epoch = num3;
			float num14 = 0.8750995f * num4 + 0.14f + 0.3f * num8;
			float num15 = 1.3f * num8 + 0.05f;
			cap.Reach2 = num14 * num14 + num15 * num15;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float Whitecap(float px, float py, float t, float td, float h)
		{
			float num = 0.8f * px + 0.6f * py;
			float num2 = 0.8f * py - 0.6f * px;
			int num3 = FastMath.FloorInt(num * 0.2f);
			int num4 = FastMath.FloorInt((num2 + (float)num3 * 2.3f) * 0.2f);
			int num5 = num3 - _capU0;
			if ((uint)num5 < (uint)_capRows)
			{
				int num6 = num4 - _capRow[num5];
				if ((uint)num6 < (uint)_capRows)
				{
					int num7 = _capGrid[num5 * _capRows + num6];
					if (num7 != 0)
					{
						return CapFoam(ref _caps[num7 - 1], px, py, t, td, h);
					}
					return 0f;
				}
			}
			return CapOutside(num3, num4, px, py, t, td, h);
		}

		private float CapOutside(int iu, int iv, float px, float py, float t, float td, float h)
		{
			if (!CapIn(iu, iv, out var cap))
			{
				return 0f;
			}
			return CapFoam(ref cap, px, py, t, td, h);
		}

		private float CapFoam(ref Cap cap, float px, float py, float t, float td, float h)
		{
			float num = px - cap.X;
			float num2 = py - cap.Y;
			float num3 = num * num + num2 * num2;
			if (num3 > cap.Reach2 * 4f)
			{
				return 0f;
			}
			float num4 = SeaRows(t, h);
			if (num4 <= 0.6f && num3 > cap.Reach2)
			{
				return 0f;
			}
			if (num4 > 0.6f)
			{
				float num5 = px - _view.X;
				float num6 = py - _view.Y;
				float num7 = 1f / (float)Math.Sqrt(num5 * num5 + num6 * num6 + 1E-06f);
				float num8 = (num * num5 + num2 * num6) * num7 * (1f - 0.6f / Math.Min(num4, 1.2f));
				num -= num5 * num7 * num8;
				num2 -= num6 * num7 * num8;
			}
			float age = cap.Age;
			float half = cap.Half;
			float num9 = 0.8750995f * age;
			float num10 = 0.8f * num + 0.6f * num2 - num9;
			float num11 = 0.8f * num2 - 0.6f * num;
			float num12 = age / cap.Life;
			float num13 = half * Math.Min(1f, num12 * 8.333334f) * (1f + 0.3f * num12);
			if (num13 < 0.001f)
			{
				return 0f;
			}
			float num14 = num11 / num13;
			float num15 = num14 * num14;
			if (num15 >= 1f)
			{
				return 0f;
			}
			float num16 = 1f - num15;
			num10 += 0.3f * half * num15;
			float num17 = 0.06f + 0.08f * num16;
			if (num10 > num17 || num10 < 0f - num9 - num17)
			{
				return 0f;
			}
			float num18;
			if (num10 >= 0f - num17)
			{
				num18 = 1f - SeaStep(0.3f, 0.9f, num12);
			}
			else
			{
				float num19 = (0f - num10 - num17) * 1.1427273f;
				num18 = 0.7f * (1f - num19 * (5f / 12f)) * (1f - SeaStep(0.6f, 1f, num12));
			}
			num18 *= 0.5f + 0.5f * num16;
			return Foam(num18, px, py, td, cap.Epoch, 45f, 80f);
		}

		private float Breakers(float px, float py, float t, float td, float h)
		{
			float num = px - 0.5f;
			float num2 = py - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			float num7 = SeaClamped(num3, num4);
			float num8 = SeaClamped(num3 + 1, num4);
			float num9 = SeaClamped(num3, num4 + 1);
			float num10 = SeaClamped(num3 + 1, num4 + 1);
			float num11 = num7 + (num8 - num7) * num5;
			float num12 = num9 + (num10 - num9) * num5;
			float num13 = (num11 + (num12 - num11) * num6) * 0.125f;
			if (num13 >= 3.6f || num13 <= 0.3f)
			{
				return 0f;
			}
			float num14 = ((num8 - num7) * (1f - num6) + (num10 - num9) * num6) * 0.125f;
			float num15 = ((num9 - num7) * (1f - num5) + (num10 - num8) * num5) * 0.125f;
			float num16 = (float)Math.Sqrt(num14 * num14 + num15 * num15);
			if (num16 < 0.2f)
			{
				return 0f;
			}
			float num17 = 6f / num16;
			float num18 = ((float)SeaAt(FastMath.FloorInt(px + num14 * num17), FastMath.FloorInt(py + num15 * num17)) * 0.125f - 6f) * 0.2f;
			if (num18 <= 0f)
			{
				return 0f;
			}
			float num19 = num13 * (2f / 3f) + Time * 0.28f + (CloudNoise.Sample(px * 1.3f, py * 1.3f) - 0.5f) * 0.5f + (CloudNoise.Sample(px * 4.1f + 31f, py * 4.1f + 17f) - 0.5f) * 0.15f;
			float num20 = num19 - (float)FastMath.FloorInt(num19);
			float num21 = 0.045f * Math.Max(1f, Math.Min(3f, SeaRows(t, h) * 2f));
			float num22 = ((num20 < num21) ? 0.7f : ((num20 < num21 + 0.3f) ? ((1f - (num20 - num21) * 3.3333333f) * 0.35f) : 0f));
			if (num22 <= 0f)
			{
				return 0f;
			}
			float num23 = (CloudNoise.Sample(px * 2.6f + Time * 0.15f, py * 2.6f) - 0.4f) * 5f;
			if (num23 <= 0f)
			{
				return 0f;
			}
			return Foam(num22 * Math.Min(1f, num23) * Math.Min(1f, num18) * SeaStep(3.6f, 2f, num13) * SeaStep(0.3f, 0.8f, num13), px, py, td, 977, 22f, 40f);
		}

		private static float Foam(float cover, float px, float py, float td, int seed, float fade, float far)
		{
			if (cover <= 0f || td >= far)
			{
				return 0f;
			}
			float num = cover * 0.75f;
			float num2 = ((td < 18f) ? 1f : (1f - (td - 18f) * (1f / 22f)));
			if (num2 > 0f)
			{
				float num3 = (((float)(TerrainModel.Hash(FastMath.FloorInt(px * 8f) + seed * 101, FastMath.FloorInt(py * 8f) - seed * 57) & 0xFF) * 0.003921569f < cover) ? 0.85f : 0f);
				num += (num3 - num) * num2;
			}
			if (td > fade)
			{
				num *= 1f - (td - fade) / (far - fade);
			}
			return num;
		}

		internal void SeaFoamForTest(TerrainCache terrain, float time, float[] xs, float[] ys, float[] foam)
		{
			float time2 = Time;
			float focal = _focal;
			float lodK = _lodK;
			ViewState view = _view;
			TerrainCache terrain2 = _terrain;
			Time = time;
			_terrain = terrain;
			_view = new ViewState
			{
				X = xs[0] - 4f,
				Y = ys[0],
				Z = 1.2f
			};
			_focal = 320f;
			_lodK = 1f;
			PrepareSea(terrain, xs[0], ys[0]);
			for (int i = 0; i < xs.Length; i++)
			{
				foam[i] = SeaFoam(xs[i], ys[i], 4f, 4f, 0f);
			}
			Time = time2;
			_focal = focal;
			_lodK = lodK;
			_view = view;
			_terrain = terrain2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float SeaFoam(float px, float py, float t, float td, float h)
		{
			if (td >= 80f || !SeaWaves)
			{
				return 0f;
			}
			float num = Whitecap(px, py, t, td, h);
			if (td < 40f && SeaAt(FastMath.FloorInt(px), FastMath.FloorInt(py)) <= 40)
			{
				num = Math.Max(num, Breakers(px, py, t, td, h));
			}
			return num;
		}

		private uint SeeInto(uint baked, float px, float py, float h, float td, float gx, float gy, float lift)
		{
			float seeIntoRange = SeeIntoRange;
			TerrainCache terrain = _terrain;
			if (td >= seeIntoRange || terrain == null)
			{
				return baked;
			}
			float[] floor = terrain.Floor;
			int width = terrain.Width;
			int height = terrain.Height;
			if (floor.Length != width * height || floor.Length == 0)
			{
				return baked;
			}
			float outside = terrain.OutsideHeight - 25.5f;
			float num = h - FloorAt(floor, width, height, px, py, outside);
			if (num < 0f)
			{
				num = 0f;
			}
			if (num >= 7f)
			{
				return baked;
			}
			float num2 = px - _view.X;
			float num3 = py - _view.Y;
			float num4 = h - _view.Z;
			float num5 = num2 * num2 + num3 * num3;
			float num6 = 1f / (float)Math.Sqrt(num5 + num4 * num4 + 1E-06f);
			float num7 = (float)Math.Sqrt(1f - num5 * num6 * num6 * 0.5627814f);
			float num8 = num * num6 * 0.7501876f / num7;
			float num9 = px + num2 * num8 + gx * num * 0.35f;
			float num10 = py + num3 * num8 + gy * num * 0.35f;
			float num11 = h - FloorAt(floor, width, height, num9, num10, outside);
			if (num11 < num)
			{
				num11 = num;
			}
			int num12 = (int)(num11 * 10f + 0.5f);
			int num13 = ((num12 > 255) ? 255 : num12) * 8 + Seabed.BendOf(1f / num7);
			uint num14 = Seabed.SeenFloor[num13];
			if (num11 > 0.6f)
			{
				float num15 = Seabed.Smooth((CloudNoise.Sample(num9 * 3f + 71f, num10 * 3f - 13f) - 0.6f) * 10f) * (1f - 0.7f * Seabed.SandShare(num11));
				if (num15 > 0f)
				{
					num14 = TerrainCache.Lerp(num14, Seabed.SeenRock[num13], num15);
				}
			}
			if (td < 10f && seeIntoRange >= 10f)
			{
				float num16 = FloorPattern(num9, num10, num11, td, lift, (0f - num4) * num6);
				if (num16 != 0f)
				{
					num14 = TerrainCache.Scale(num14, 1f + num16 * Seabed.SeenShare[num13]);
				}
			}
			float num17 = ((td < seeIntoRange * 0.7f) ? 1f : ((seeIntoRange - td) / (seeIntoRange * 0.3f)));
			if (num > 5.5f)
			{
				num17 *= (7f - num) * (2f / 3f);
			}
			if (!(num17 >= 1f))
			{
				return TerrainCache.Lerp(baked, num14, num17);
			}
			return num14;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float FloorAt(float[] floor, int mw, int mh, float x, float y, float outside)
		{
			if (x < 0f || y < 0f || x >= (float)mw || y >= (float)mh)
			{
				return outside;
			}
			float num = x - 0.5f;
			float num2 = y - 0.5f;
			int num3 = (int)num;
			int num4 = (int)num2;
			if (num < 0f)
			{
				num3 = -1;
			}
			if (num2 < 0f)
			{
				num4 = -1;
			}
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? num3 : 0);
			int num8 = ((num3 + 1 >= mw) ? (mw - 1) : (num3 + 1));
			int num9 = ((num4 >= 0) ? num4 : 0) * mw;
			int num10 = ((num4 + 1 >= mh) ? (mh - 1) : (num4 + 1)) * mw;
			float num11 = floor[num9 + num7] + (floor[num9 + num8] - floor[num9 + num7]) * num5;
			float num12 = floor[num10 + num7] + (floor[num10 + num8] - floor[num10 + num7]) * num5;
			return num11 + (num12 - num11) * num6;
		}

		private uint IceSurface(uint col, int sun, int open, float px, float py, float t, float h, uint fogCol)
		{
			uint a = LightSurface(col, sun, open);
			float val = 1f - Math.Min(1f, 3.5f * (_view.Z - h + 0.05f) / Math.Max(0.1f, t));
			a = TerrainCache.Lerp(a, TerrainCache.Lerp(fogCol, Air.Zenith, 0.2f), 0.06f + 0.3f * Math.Max(0f, val));
			if (!IceCracks(px, py))
			{
				return a;
			}
			return TerrainCache.Scale(a, 1.12f);
		}

		private void IceEdge(ref byte fl, ref uint col, float px, float py)
		{
			TerrainCache terrain = _terrain;
			int width = terrain.Width;
			int height = terrain.Height;
			byte[] flags = terrain.Flags;
			if (flags.Length != width * height || width == 0)
			{
				return;
			}
			float num = px - 0.5f;
			float num2 = py - 0.5f;
			int num3 = FastMath.FloorInt(num);
			int num4 = FastMath.FloorInt(num2);
			float num5 = num - (float)num3;
			float num6 = num2 - (float)num4;
			int num7 = ((num3 >= 0) ? ((num3 >= width) ? (width - 1) : num3) : 0);
			int num8 = ((num3 + 1 >= 0) ? ((num3 + 1 >= width) ? (width - 1) : (num3 + 1)) : 0);
			int num9 = ((num4 >= 0) ? ((num4 >= height) ? (height - 1) : num4) : 0) * width;
			int num10 = ((num4 + 1 >= 0) ? ((num4 + 1 >= height) ? (height - 1) : (num4 + 1)) : 0) * width;
			byte b = flags[num9 + num7];
			byte b2 = flags[num9 + num8];
			byte b3 = flags[num10 + num7];
			byte b4 = flags[num10 + num8];
			if (((b | b2 | b3 | b4) & 0x80) == 0 || (b & b2 & b3 & b4 & 0x80) != 0)
			{
				return;
			}
			float num11 = (1f - num5) * (1f - num6);
			float num12 = num5 * (1f - num6);
			float num13 = (1f - num5) * num6;
			float num14 = num5 * num6;
			bool flag = (((b & 0x80) != 0) ? num11 : 0f) + (((b2 & 0x80) != 0) ? num12 : 0f) + (((b3 & 0x80) != 0) ? num13 : 0f) + (((b4 & 0x80) != 0) ? num14 : 0f) + 0.15f * FastSin(px * 2.3f + FastSin(py * 1.7f) * 2f) > 0.5f;
			if (flag != ((fl & 0x80) != 0))
			{
				fl = (flag ? ((byte)(fl | 0x80)) : ((byte)(fl & -129)));
				int num15 = -1;
				float num16 = -1f;
				if ((b & 0x80) != 0 == flag && num11 > num16)
				{
					num16 = num11;
					num15 = num9 + num7;
				}
				if ((b2 & 0x80) != 0 == flag && num12 > num16)
				{
					num16 = num12;
					num15 = num9 + num8;
				}
				if ((b3 & 0x80) != 0 == flag && num13 > num16)
				{
					num16 = num13;
					num15 = num10 + num7;
				}
				if ((b4 & 0x80) != 0 == flag && num14 > num16)
				{
					num16 = num14;
					num15 = num10 + num8;
				}
				if (num15 >= 0 && num15 < terrain.Colors.Length)
				{
					col = terrain.Colors[num15];
				}
			}
		}

		private static bool IceCracks(float px, float py)
		{
			float num = px * 0.9f + FastSin(py * 1.3f) * 0.6f;
			float num2 = py * 0.8f + FastSin(px * 1.1f + 2f) * 0.6f;
			if (!(Math.Abs(num - (float)Math.Floor(num) - 0.5f) < 0.018f))
			{
				return Math.Abs(num2 - (float)Math.Floor(num2) - 0.5f) < 0.018f;
			}
			return true;
		}

		private float FloorPattern(float qx, float qy, float d, float td, float lift, float down)
		{
			float num = 1f - td * 0.1f;
			float num2 = Seabed.SandShare(d);
			float num3 = ((num2 > 0f) ? (FastSin((qx * 0.8f + qy * 0.6f) * 9f + FastSin(qx * 1.1f - qy * 1.7f) * 2.5f) * 0.05f * num2 * num) : 0f);
			if (d < 3f && lift > 0f && down > 0.04f)
			{
				float num4 = 0.4f * (1f - d * (1f / 3f)) * num * lift * (1f - Air.Night) * Seabed.Smooth(down * 3f);
				if (num4 > 0.01f)
				{
					num3 += Caustic(qx, qy, Time) * num4;
				}
			}
			return num3;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Caustic(float x, float y, float t)
		{
			float num = FastSin(x * 5.2f + t * 1.3f + FastSin(y * 3.1f - t * 0.8f) * 1.3f);
			float num2 = FastSin(y * 5.9f - t * 1.1f + FastSin(x * 3.8f + t * 0.7f) * 1.1f);
			float num3 = 1f - Math.Abs(num + num2) * 3f;
			if (!(num3 <= 0f))
			{
				return num3 * num3;
			}
			return 0f;
		}

		private void EnsureSunlit()
		{
			if (_sunlit.Length != Width * Height)
			{
				_sunlit = new byte[Width * Height];
			}
		}

		private void BuildShadowMap()
		{
			_shadowReady = false;
			ShadowMs = 0f;
			if (Air.Indoors || _pass.Count == 0 || ShadowStrength <= 0f)
			{
				return;
			}
			ShadowSize = Math.Max(64, Math.Min(2048, ShadowMapSize / 64 * 64));
			_shadowFloor = 1f - Math.Min(1f, ShadowStrength);
			if (Math.Max(Air.LightR, Math.Max(Air.LightG, Air.LightB)) < 0.05f)
			{
				return;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			bool natural = _natural;
			float num;
			float num2;
			if (natural)
			{
				if (!_nOn)
				{
					return;
				}
				ShadowSize = _nSize;
				_sL = _nL;
				_sA = _nA;
				_sB = _nB;
				_sC = _nC;
				num = _nCx;
				num2 = _nCy;
			}
			else
			{
				_sL = _light;
				Vec3 a = new Vec3(0f, 0f, 1f);
				_sA = Vec3.Cross(a, _sL).Normalized();
				if (Math.Abs(_sA.X) + Math.Abs(_sA.Y) < 0.0001f)
				{
					_sA = new Vec3(1f, 0f, 0f);
				}
				_sB = Vec3.Cross(_sL, _sA);
				float num3 = (ShadowFocusSet ? ShadowFocusX : _view.X);
				float num4 = (ShadowFocusSet ? ShadowFocusY : _view.Y);
				num = (float)Math.Floor(num3 / 4f) * 4f + 2f;
				num2 = (float)Math.Floor(num4 / 4f) * 4f + 2f;
				_sC = new Vec3(num, num2, 0f);
			}
			_sScale = (float)ShadowSize / (2f * ShadowReach);
			_sScaleB = (natural ? _nScaleB : _sScale);
			int num5 = ShadowSize * ShadowSize;
			if (_shadowMap.Length != num5)
			{
				_shadowMap = new float[num5];
				_staticMap = new float[num5];
				_staticKey = long.MinValue;
				_dirtyLo = 0;
				_dirtyHi = ShadowSize;
			}
			int count = _pass.Count;
			if (_casterLo.Length < count)
			{
				_casterLo = new float[count * 2];
				_casterHi = new float[count * 2];
			}
			int num6 = _vStart[count];
			if (_shadowU.Length < num6)
			{
				_shadowU = new float[num6 + num6 / 2];
				_shadowV = new float[num6 + num6 / 2];
				_shadowD = new float[num6 + num6 / 2];
			}
			long num7 = (natural ? _nKey : (((long)(int)num * 73856093L) ^ ((long)(int)num2 * 19349663L) ^ ((long)(int)Math.Round(_sL.X * 400f) << 20) ^ ((long)(int)Math.Round(_sL.Y * 400f) << 32) ^ ((long)(int)Math.Round(_sL.Z * 400f) << 44)));
			int num8 = 0;
			long num9 = 0L;
			long num10 = 0L;
			float num11 = CasterReach * CasterReach;
			for (int i = 0; i < count; i++)
			{
				MeshInstance meshInstance = _pass[i];
				if (meshInstance.Static && !meshInstance.NoShadow && !meshInstance.Emissive)
				{
					float num12 = meshInstance.Position.X - num;
					float num13 = meshInstance.Position.Y - num2;
					if (!(natural ? (!SunwardCaster(meshInstance, num12, num13, num11)) : (num12 * num12 + num13 * num13 > num11)))
					{
						num8++;
						long num14 = ((long)(int)Math.Floor(meshInstance.Position.X * 2f) * 73856093L) ^ ((long)(int)Math.Floor(meshInstance.Position.Y * 2f) * 19349663L);
						num9 += num14 * 2862933555777941757L;
						num10 += (int)(meshInstance.Basis.C2.Z * 32f) * (num14 | 1);
					}
				}
			}
			num7 ^= (num8 * 2654435761u) ^ num9;
			_framesSinceStatic++;
			if (num7 != _staticKey || (num10 != _staticSoft && _framesSinceStatic > 120))
			{
				ShadowRebuilds++;
				ShadowRebuildInfo = $"statics {num8}, sum {num9}, soft {num10 != _staticSoft}, centre {num},{num2}";
				_staticSoft = num10;
				_framesSinceStatic = 0;
				DrawCasters(_staticMap, statics: true);
				_staticKey = num7;
				_dirtyLo = 0;
				_dirtyHi = ShadowSize;
			}
			if (_dirtyHi > _dirtyLo)
			{
				Array.Copy(_staticMap, _dirtyLo * ShadowSize, _shadowMap, _dirtyLo * ShadowSize, (_dirtyHi - _dirtyLo) * ShadowSize);
			}
			DrawCasters(_shadowMap, statics: false);
			float num15 = float.MaxValue;
			float num16 = float.MinValue;
			for (int j = 0; j < count; j++)
			{
				if (!_pass[j].Static && !(_casterLo[j] > _casterHi[j]))
				{
					if (_casterLo[j] < num15)
					{
						num15 = _casterLo[j];
					}
					if (_casterHi[j] > num16)
					{
						num16 = _casterHi[j];
					}
				}
			}
			if (num15 > num16)
			{
				_dirtyLo = 0;
				_dirtyHi = 0;
			}
			else
			{
				_dirtyLo = Math.Max(0, Math.Min(ShadowSize, FastMath.FloorInt(Math.Max(-1f, num15))));
				_dirtyHi = Math.Max(_dirtyLo, Math.Min(ShadowSize, FastMath.FloorInt(Math.Min((float)ShadowSize + 1f, num16)) + 1));
			}
			_shadowReady = true;
			ShadowMs = (float)stopwatch.Elapsed.TotalMilliseconds;
		}

		private void DrawCasters(float[] map, bool statics)
		{
			int count = _pass.Count;
			float dcHalf = (float)ShadowSize * 0.5f;
			float dcReach = CasterReach * CasterReach;
			if (statics)
			{
				for (int i = 0; i < map.Length; i++)
				{
					map[i] = float.MinValue;
				}
			}
			_dcStatics = statics;
			_dcHalf = dcHalf;
			_dcReach2 = dcReach;
			_dcAny = 0;
			For(count, CasterVertices);
			if (_dcAny != 0)
			{
				_dcMap = map;
				For(64, CasterBand);
				_dcMap = null;
			}
		}

		private void ShadowTriangle(float[] map, int a, int b, int c, int y0, int y1, float slack)
		{
			float num = _shadowU[a];
			float num2 = _shadowV[a];
			float num3 = _shadowU[b];
			float num4 = _shadowV[b];
			float num5 = _shadowU[c];
			float num6 = _shadowV[c];
			int num7 = Math.Max(y0, FastMath.FloorInt(Math.Min(num2, Math.Min(num4, num6))));
			int num8 = Math.Min(y1 - 1, FastMath.FloorInt(Math.Max(num2, Math.Max(num4, num6))));
			if (num7 > num8)
			{
				return;
			}
			float num9 = Math.Min(num, Math.Min(num3, num5));
			float num10 = Math.Max(num, Math.Max(num3, num5));
			if (num10 < 0f || num9 >= (float)ShadowSize)
			{
				return;
			}
			float num11 = (num3 - num) * (num6 - num2) - (num5 - num) * (num4 - num2);
			float num12 = _shadowD[a];
			float num13 = _shadowD[b];
			float num14 = _shadowD[c];
			if (Math.Abs(num11) < 0.0001f)
			{
				if (_natural)
				{
					float val = Math.Min(num2, Math.Min(num4, num6));
					float val2 = Math.Max(num2, Math.Max(num4, num6));
					for (int i = num7; i <= num8; i++)
					{
						float py = Math.Max(val, Math.Min(val2, (float)i + 0.5f));
						float best = float.MinValue;
						float bu = 0f;
						EdgeOnRow(num, num2, num12, num3, num4, num13, py, ref best, ref bu);
						EdgeOnRow(num3, num4, num13, num5, num6, num14, py, ref best, ref bu);
						EdgeOnRow(num5, num6, num14, num, num2, num12, py, ref best, ref bu);
						int num15 = FastMath.FloorInt(bu);
						if (best > float.MinValue && num15 >= 0 && num15 < ShadowSize && best > map[i * ShadowSize + num15])
						{
							map[i * ShadowSize + num15] = best;
						}
					}
					return;
				}
				int num16 = Math.Max(0, Math.Min(ShadowSize - 1, (int)((num9 + num10) * 0.5f)));
				float num17 = Math.Max(num12, Math.Max(num13, num14));
				for (int j = num7; j <= num8; j++)
				{
					if (num17 > map[j * ShadowSize + num16])
					{
						map[j * ShadowSize + num16] = num17;
					}
				}
				return;
			}
			float num18 = 1f / num11;
			float num19 = (num4 - num6) * num18;
			float num20 = (num5 - num3) * num18;
			float num21 = (num3 * num6 - num5 * num4) * num18;
			float num22 = (num6 - num2) * num18;
			float num23 = (num - num5) * num18;
			float num24 = (num5 * num2 - num * num6) * num18;
			for (int k = num7; k <= num8; k++)
			{
				float num25 = (float)k + 0.5f;
				float lo = Math.Max(0f, num9 - 0.5f);
				float hi = Math.Min(ShadowSize, num10 + 0.5f);
				if (!Span(0f - num19, 0f - (num20 * num25 + num21) - slack * Math.Abs(num19), ref lo, ref hi) || !Span(0f - num22, 0f - (num23 * num25 + num24) - slack * Math.Abs(num22), ref lo, ref hi))
				{
					continue;
				}
				float num26 = 0f - num19 - num22;
				float num27 = 1f - (num20 * num25 + num21) - (num23 * num25 + num24);
				if (!Span(0f - num26, 0f - num27 - slack * Math.Abs(num26), ref lo, ref hi))
				{
					continue;
				}
				int num28 = Math.Max(0, FastMath.CeilInt(lo - 0.5f));
				int num29 = Math.Min(ShadowSize - 1, FastMath.FloorInt(hi - 0.5f));
				int num30 = k * ShadowSize;
				for (int l = num28; l <= num29; l++)
				{
					float num31 = (float)l + 0.5f;
					float num32 = num19 * num31 + num20 * num25 + num21;
					float num33 = num22 * num31 + num23 * num25 + num24;
					float num34 = num32 * num12 + num33 * num13 + (1f - num32 - num33) * num14;
					if (num34 > map[num30 + l])
					{
						map[num30 + l] = num34;
					}
				}
			}
		}

		private static void EdgeOnRow(float u0, float v0, float d0, float u1, float v1, float d1, float py, ref float best, ref float bu)
		{
			if (!(py < Math.Min(v0, v1)) && !(py > Math.Max(v0, v1)))
			{
				float num = v1 - v0;
				float num2 = ((Math.Abs(num) > 1E-06f) ? ((py - v0) / num) : ((d1 > d0) ? 1f : 0f));
				float num3 = d0 + (d1 - d0) * num2;
				if (num3 > best)
				{
					best = num3;
					bu = u0 + (u1 - u0) * num2;
				}
			}
		}

		private float ShadowAt(float x, float y, float z, int c, int r, float bias)
		{
			Vec3 a = new Vec3(x - _sC.X, y - _sC.Y, z - _sC.Z);
			float num = (float)ShadowSize * 0.5f;
			float num2 = num + Vec3.Dot(a, _sA) * _sScale;
			float num3 = num + Vec3.Dot(a, _sB) * _sScaleB;
			float num4 = Vec3.Dot(a, _sL) + bias;
			float num5 = BayerAt(c, r) - 0.5f;
			int num6 = (int)(num2 + num5);
			int num7 = (int)(num3 - num5);
			if (num6 < 0 || num7 < 0 || num6 >= ShadowSize || num7 >= ShadowSize)
			{
				return 0f;
			}
			if (!(_shadowMap[num7 * ShadowSize + num6] > num4))
			{
				return 0f;
			}
			return 1f;
		}

		private void ApplyModelShadows()
		{
			if (_shadowReady)
			{
				int width = Width;
				float focal = _focal;
				float msReach = ShadowReach * 2.2f;
				float msHalfW = (float)width * 0.5f;
				float x = _view.X;
				float y = _view.Y;
				float z = _view.Z;
				float x2 = _sC.X;
				float y2 = _sC.Y;
				float z2 = _sC.Z;
				float sScale = _sScale;
				float num = (float)ShadowSize * 0.5f;
				bool natural = _natural;
				float msInvHalf = 0f;
				float msInvReach = 0f;
				float msGatherIn = 0f;
				float msGatherOut = 0f;
				MapCellDepths(focal, sScale, out var d, out var d2);
				if (natural)
				{
					float num2 = Math.Max(0f, GatherReach);
					float num3 = Math.Max(0f, num2 - GatherFade);
					msGatherOut = num2 * num2;
					msGatherIn = num3 * num3;
					float num4 = x - x2;
					float num5 = y - y2;
					float num6 = z - z2;
					msReach = (float)Math.Sqrt(num4 * num4 + num5 * num5 + num6 * num6) + _nReach + _nRise + 1f;
					msInvHalf = 1f / (num * num);
					msInvReach = 1f / (_nReach * _nReach);
				}
				_msReach = msReach;
				_msHalfW = msHalfW;
				_msHalf = num;
				_msInvHalf2 = msInvHalf;
				_msInvReach2 = msInvReach;
				_msGatherIn2 = msGatherIn;
				_msGatherOut2 = msGatherOut;
				_msCells16 = d;
				_msCells8 = d2;
				For(Height, ModelShadowRow);
			}
		}

		private void CasterVertices(int i)
		{
			bool dcStatics = _dcStatics;
			float dcHalf = _dcHalf;
			float dcReach = _dcReach2;
			ref int dcAny = ref _dcAny;
			float x = _sC.X;
			float y = _sC.Y;
			float z = _sC.Z;
			float sScale = _sScale;
			float sScaleB = _sScaleB;
			float x2 = _sA.X;
			float y2 = _sA.Y;
			float z2 = _sA.Z;
			float x3 = _sB.X;
			float y3 = _sB.Y;
			float z3 = _sB.Z;
			float x4 = _sL.X;
			float y4 = _sL.Y;
			float z4 = _sL.Z;
			MVert[] mv = _mv;
			float[] shadowU = _shadowU;
			float[] shadowV = _shadowV;
			float[] shadowD = _shadowD;
			float num = (_natural ? _nCx : x);
			float num2 = (_natural ? _nCy : y);
			MeshInstance meshInstance = _pass[i];
			_casterLo[i] = float.MaxValue;
			_casterHi[i] = float.MinValue;
			float num3 = meshInstance.Position.X - num;
			float num4 = meshInstance.Position.Y - num2;
			if (meshInstance.Static != dcStatics || meshInstance.NoShadow || meshInstance.Emissive || (meshInstance.Fade < 0.5f && !meshInstance.FadeKeepsShadow) || (_natural ? (!SunwardCaster(meshInstance, num3, num4, dcReach)) : (num3 * num3 + num4 * num4 > dcReach)))
			{
				return;
			}
			float num5 = float.MaxValue;
			float num6 = float.MinValue;
			float num7 = float.MaxValue;
			float num8 = float.MinValue;
			int j = _vStart[i];
			for (int num9 = _vStart[i + 1]; j < num9; j++)
			{
				ref MVert reference = ref mv[j];
				float num10 = reference.Wx - x;
				float num11 = reference.Wy - y;
				float num12 = reference.Wz - z;
				float num13 = dcHalf + (num10 * x2 + num11 * y2 + num12 * z2) * sScale;
				float num14 = dcHalf + (num10 * x3 + num11 * y3 + num12 * z3) * sScaleB;
				shadowU[j] = num13;
				shadowV[j] = num14;
				shadowD[j] = num10 * x4 + num11 * y4 + num12 * z4;
				if (num14 < num5)
				{
					num5 = num14;
				}
				if (num14 > num6)
				{
					num6 = num14;
				}
				if (num13 < num7)
				{
					num7 = num13;
				}
				if (num13 > num8)
				{
					num8 = num13;
				}
			}
			if (!(num8 < 0f) && !(num7 >= (float)ShadowSize) && !(num6 < 0f) && !(num5 >= (float)ShadowSize))
			{
				_casterLo[i] = num5;
				_casterHi[i] = num6;
				Interlocked.Increment(ref dcAny);
			}
		}

		private void CasterBand(int band)
		{
			float[] dcMap = _dcMap;
			int count = _pass.Count;
			int num = ShadowSize / 64;
			float slack = (_natural ? 0f : 0.3f);
			int num2 = band * num;
			int num3 = num2 + num;
			for (int i = 0; i < count; i++)
			{
				if (_casterHi[i] < (float)num2 || _casterLo[i] >= (float)num3)
				{
					continue;
				}
				MeshInstance meshInstance = _pass[i];
				MeshModel model = meshInstance.Model;
				int num4 = _vStart[i];
				for (int j = 0; j < model.Parts.Length; j++)
				{
					MeshPart meshPart = model.Parts[j];
					if (j < 64 && (meshInstance.Hidden & (ulong)(1L << j)) != 0L)
					{
						num4 += meshPart.VertexCount;
						continue;
					}
					int[] tri = meshPart.Tri;
					int k = 0;
					for (int triangleCount = meshPart.TriangleCount; k < triangleCount; k++)
					{
						if ((model.SlotFlags[meshPart.Slot[k]] & 0x81) == 0)
						{
							ShadowTriangle(dcMap, num4 + tri[k * 3], num4 + tri[k * 3 + 1], num4 + tri[k * 3 + 2], num2, num3, slack);
						}
					}
					num4 += meshPart.VertexCount;
				}
			}
		}

		private void ModelShadowRow(int r)
		{
			Tint3 shadowTint = _shadowTint;
			int width = Width;
			int height = Height;
			float focal = _focal;
			float msReach = _msReach;
			float msHalfW = _msHalfW;
			float x = _view.X;
			float y = _view.Y;
			float z = _view.Z;
			float fx = _fx;
			float fy = _fy;
			float rx = _rx;
			float ry = _ry;
			float x2 = _sC.X;
			float y2 = _sC.Y;
			float z2 = _sC.Z;
			float sScale = _sScale;
			float sScaleB = _sScaleB;
			float msHalf = _msHalf;
			float x3 = _sA.X;
			float y3 = _sA.Y;
			float z3 = _sA.Z;
			float x4 = _sB.X;
			float y4 = _sB.Y;
			float z4 = _sB.Z;
			float x5 = _sL.X;
			float y5 = _sL.Y;
			float z5 = _sL.Z;
			int shadowSize = ShadowSize;
			byte[] sunlit = _sunlit;
			float[] depth = Depth;
			float[] shadowMap = _shadowMap;
			uint[] pixels = Pixels;
			float[] rowK = _rowK;
			float[] rowD = _rowD;
			bool natural = _natural;
			bool landField = _landField;
			float num = (natural ? _nHold : 0f);
			float msInvHalf = _msInvHalf2;
			float msInvReach = _msInvReach2;
			float msGatherIn = _msGatherIn2;
			float msGatherOut = _msGatherOut2;
			int nOu = _nOu;
			int nOv = _nOv;
			float msCells = _msCells16;
			float msCells2 = _msCells8;
			float num2 = rowK[r];
			float num3 = rowD[r];
			int num4 = (height - 1 - r) * width;
			for (int i = 0; i < width; i++)
			{
				int num5 = num4 + i;
				if (sunlit[num5] == 0)
				{
					continue;
				}
				float num6 = depth[num5];
				if (num6 > msReach)
				{
					continue;
				}
				float num7 = ((float)i + 0.5f - msHalfW) / focal * num3;
				float num8 = x + (fx + rx * num7) * num6;
				float num9 = y + (fy + ry * num7) * num6;
				float num10 = z + num2 * num6;
				float num11 = num8 - x2;
				float num12 = num9 - y2;
				float num13 = num10 - z2;
				float num14 = msHalf + (num11 * x3 + num12 * y3 + num13 * z3) * sScale;
				float num15 = msHalf + (num11 * x4 + num12 * y4 + num13 * z4) * sScaleB;
				float num16 = num11 * x5 + num12 * y5 + num13 * z5 + (0.08f + num6 * 0.004f);
				if (natural)
				{
					float num17 = num14 - msHalf;
					float num18 = num15 - msHalf;
					float num19 = Math.Max((num17 * num17 + num18 * num18) * msInvHalf, (num11 * num11 + num12 * num12) * msInvReach);
					if (num19 >= 1f)
					{
						continue;
					}
					float num20 = num8 - x;
					float num21 = num9 - y;
					float num22 = num20 * num20 + num21 * num21;
					if (num22 >= msGatherOut)
					{
						continue;
					}
					MapJitter(num14, num15, 1f, MapCells(num6, msCells, msCells2), nOu, nOv, out var ju, out var jv);
					int num23 = FastMath.FloorInt(num14 + ju);
					int num24 = FastMath.FloorInt(num15 + jv);
					if (num23 < 0 || num24 < 0 || num23 >= shadowSize || num24 >= shadowSize || !(shadowMap[num24 * shadowSize + num23] > num16))
					{
						continue;
					}
					int num25 = sunlit[num5];
					if (num > 0f)
					{
						int num26 = Share(MapHeld(Math.Min((num19 > 0.7744f) ? CoverAt((float)Math.Sqrt(num19)) : 1f, (num22 > msGatherIn) ? Gathered(num8, num9) : 1f)) * Math.Max(0f, (landField ? LandVis(num8, num9, num10) : 1f) - num));
						if (num26 < num25)
						{
							num25 = num26;
						}
					}
					else if (num19 > 0.7744f || num22 > msGatherIn)
					{
						int num27 = Share(Math.Min((num19 > 0.7744f) ? CoverAt((float)Math.Sqrt(num19)) : 1f, Gathered(num8, num9)) * (landField ? LandVis(num8, num9, num10) : 1f));
						if (num27 < num25)
						{
							num25 = num27;
						}
					}
					if (num25 > 0)
					{
						pixels[num5] = TerrainCache.Tint(pixels[num5], ShareTint(shadowTint.R, num25), ShareTint(shadowTint.G, num25), ShareTint(shadowTint.B, num25));
					}
				}
				else
				{
					float num28 = BayerAt(i, r) - 0.5f;
					int num29 = (int)(num14 + num28);
					int num30 = (int)(num15 - num28);
					if (num29 >= 0 && num30 >= 0 && num29 < shadowSize && num30 < shadowSize && shadowMap[num30 * shadowSize + num29] > num16)
					{
						pixels[num5] = TerrainCache.Tint(pixels[num5], shadowTint.R, shadowTint.G, shadowTint.B);
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float BayerAt(int c, int r)
		{
			return Bayer[((r & 3) << 2) | (c & 3)];
		}

		private void PrepareSky()
		{
			Atmosphere air = Air;
			_sunDir = air.SunDisc;
			_moonDir = air.MoonDisc;
			_sunVis = air.SunVisible;
			_moonVis = air.MoonVisible * air.Night;
			_starsVis = air.Stars;
			_sunCol = air.SunColor;
			_zenith = air.Zenith;
			float num = 1f - Math.Min(1f, Math.Max(0f, air.SunDisc.Z) * 3f);
			_sunDiscCos = (float)Math.Cos(0.026f + 0.016f * num);
			_moonScale = Math.Max(0.5f, air.MoonScale);
			_moonDiscCos = (float)Math.Cos(0.022f * _moonScale);
			_moonHaloCos = (float)Math.Cos(0.245f * Math.Min(2.2f, _moonScale));
			_skyNatural = air.Natural;
			if (_skyNatural)
			{
				NaturalMoon(air, out _moonVis, out _moonHaloK, out _moonDark, out _moonRight, out _moonUp, out _moonPu, out _moonPv, out _moonCosTerm, out _moonInvR);
				SunGlow(air, num, out var _, out _sunHaloGain, out _sunHaloBroad, out _sunDiscWhite, out _sunHaloCol);
			}
			bool moon = air.Night > 0.6f;
			Vec3 vec = (moon ? air.MoonDisc : air.SunDisc);
			float num2 = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
			float gx = ((num2 > 0.0001f) ? (vec.X / num2) : 1f);
			float gy = ((num2 > 0.0001f) ? (vec.Y / num2) : 0f);
			float sideStrength = (moon ? 0.35f : (0.35f + 0.65f * num));
			float glitter = 0f;
			if (_skyNatural)
			{
				SkyGlow(air, num, out moon, out gx, out gy, out sideStrength, out glitter);
			}
			int cw = _cw;
			if (_colDx.Length < cw)
			{
				_colDx = new float[_colS.Length];
				_colDy = new float[_colS.Length];
			}
			if (_fogCol.Length < cw)
			{
				_fogCol = new uint[_colS.Length];
				_sunSide = new float[_colS.Length];
				_invLen = new float[_colS.Length];
			}
			if (_rowCloud.Length != Height)
			{
				_rowCloud = new float[Height];
			}
			_rowKv = _rowK;
			for (int i = 0; i < Height; i++)
			{
				_rowCloud[i] = 70f / _rowK[i];
			}
			for (int j = 0; j < cw; j++)
			{
				float num3 = _colS[j];
				float num4 = _fx + _rx * num3;
				float num5 = _fy + _ry * num3;
				_colDx[j] = num4;
				_colDy[j] = num5;
				float num6 = 1f / (float)Math.Sqrt(num4 * num4 + num5 * num5);
				_invLen[j] = num6;
				float num7 = Math.Max(0f, (num4 * gx + num5 * gy) * num6);
				_sunSide[j] = num7;
				float t = num7 * num7 * sideStrength;
				_fogCol[j] = TerrainCache.Lerp(air.Horizon, air.SunHorizon, t);
			}
			if (!_tilted)
			{
				_scrFog = _fogCol;
			}
			else
			{
				if (_scrFog.Length != Width || _scrFog == _fogCol)
				{
					_scrFog = new uint[Width];
				}
				for (int k = 0; k < Width; k++)
				{
					float num8 = _scrU[k] * _lens.Cq;
					float num9 = _fx + _rx * num8;
					float num10 = _fy + _ry * num8;
					float num11 = Math.Max(0f, (num9 * gx + num10 * gy) / (float)Math.Sqrt(num9 * num9 + num10 * num10));
					_scrFog[k] = TerrainCache.Lerp(air.Horizon, air.SunHorizon, num11 * num11 * sideStrength);
				}
			}
			_glitter = (moon ? (air.MoonVisible * 0.6f) : (air.SunVisible * (0.45f + 0.55f * num)));
			_glitterColor = (moon ? TerrainCache.Rgb(200, 210, 255) : TerrainCache.Lerp(air.SunColor, uint.MaxValue, 0.35f));
			if (_skyNatural)
			{
				_glitter = glitter;
			}
			_cloudOffU = Time * 0.9f;
			_cloudOffV = Time * 0.35f;
			float num12 = (float)Math.Sqrt(air.SunDisc.X * air.SunDisc.X + air.SunDisc.Y * air.SunDisc.Y);
			_cloudSunU = ((num12 > 0.0001f) ? (air.SunDisc.X / num12 * 2.5f) : 0f);
			_cloudSunV = ((num12 > 0.0001f) ? (air.SunDisc.Y / num12 * 2.5f) : 0f);
			PrepareNightSky();
		}

		private uint SkyPixel(int c, int r)
		{
			return SkyRay(c, c, r, _colDx[c], _colDy[c], _invLen[c], _fogCol[c]);
		}

		private uint SkyRay(int c, int ac, int r, float dx, float dy, float il, uint horizon)
		{
			Atmosphere air = Air;
			float num = _rowKv[r];
			float num2 = num * il;
			if (air.Indoors)
			{
				float t = ((num2 <= 0f) ? 0f : Math.Min(1f, num2 * 1.3f));
				return TerrainCache.Lerp(horizon, air.Zenith, t);
			}
			if (num2 <= 0f)
			{
				return horizon;
			}
			float t2 = 1f - ExpNeg(num2 * 2.4f);
			uint num3 = TerrainCache.Lerp(horizon, _zenith, t2);
			float num4 = 1f / (float)Math.Sqrt(dx * dx + dy * dy + num * num);
			float num5 = dx * num4;
			float num6 = dy * num4;
			float num7 = num * num4;
			if (_nightOn && num2 > 0.03f)
			{
				num3 = NightSky(num3, c, ac, r, num5, num6, num7, num2);
			}
			float num8 = num5 * _sunDir.X + num6 * _sunDir.Y + num7 * _sunDir.Z;
			if (_sunVis > 0.01f && num8 > 0.9f)
			{
				float num9 = (num8 - 0.9f) * 10f;
				if (_skyNatural)
				{
					float num10 = (num9 * num9 * num9 * 0.55f * (1f + _sunHaloGain) + num9 * num9 * _sunHaloBroad) * _sunVis;
					num3 = TerrainCache.Lerp(num3, _sunHaloCol, (num10 > 0.85f) ? 0.85f : num10);
				}
				else
				{
					num9 = num9 * num9 * num9;
					num3 = TerrainCache.Lerp(num3, _sunCol, num9 * 0.55f * _sunVis);
				}
				if (num8 > _sunDiscCos)
				{
					float num11 = (num8 - _sunDiscCos) / (1f - _sunDiscCos);
					if (num11 > 0.25f || BayerAt(c, r) < num11 * 4f)
					{
						num3 = TerrainCache.Lerp(num3, TerrainCache.Lerp(_sunCol, uint.MaxValue, _skyNatural ? _sunDiscWhite : 0.5f), _sunVis);
					}
				}
			}
			if (_moonVis > 0.01f)
			{
				float num12 = num5 * _moonDir.X + num6 * _moonDir.Y + num7 * _moonDir.Z;
				if (num12 > _moonHaloCos)
				{
					float num13 = (num12 - _moonHaloCos) / (1f - _moonHaloCos);
					num3 = TerrainCache.Lerp(num3, TerrainCache.Rgb(150, 160, 210), num13 * num13 * 0.35f * (_skyNatural ? _moonHaloK : _moonVis));
					if (num12 > _moonDiscCos)
					{
						num3 = (_skyNatural ? MoonPhase(num3, c, r, num5, num6, num7) : TerrainCache.Lerp(num3, MoonPixel(num5, num6, num7), _moonVis));
					}
				}
			}
			if (air.Clouds > 0.01f && num2 > 0.012f)
			{
				num3 = Clouds(num3, horizon, c, r, dx, dy, num, num2, num8);
			}
			return num3;
		}

		private uint MoonPixel(float nx, float ny, float nz)
		{
			Vec3 moonDir = _moonDir;
			Vec3 a = Vec3.Cross(moonDir, new Vec3(0f, 0f, 1f)).Normalized();
			Vec3 vec = Vec3.Cross(a, moonDir);
			float num = (nx * a.X + ny * a.Y + nz * a.Z) * 90f / _moonScale;
			float num2 = (nx * vec.X + ny * vec.Y + nz * vec.Z) * 90f / _moonScale;
			float s = ((CloudNoise.Sample(num * 1.7f + 40f, num2 * 1.7f + 90f) > 0.62f) ? 0.78f : 0.97f);
			return TerrainCache.Scale(TerrainCache.Rgb(236, 236, 250), s);
		}

		private uint MoonPhase(uint sky, int c, int r, float nx, float ny, float nz)
		{
			float num = (nx * _moonRight.X + ny * _moonRight.Y + nz * _moonRight.Z) * _moonInvR;
			float num2 = (nx * _moonUp.X + ny * _moonUp.Y + nz * _moonUp.Z) * _moonInvR;
			float num3 = 1f - num * num - num2 * num2;
			float num4 = (_moonCosTerm * ((num3 > 0f) ? ((float)Math.Sqrt(num3)) : 0f) + num * _moonPu + num2 * _moonPv) / 0.14f + 0.5f;
			float num5 = ((num4 >= 1f || (num4 > 0f && BayerAt(c, r) < num4)) ? _moonVis : (_moonVis * _moonDark));
			if (!(num5 > 0f))
			{
				return sky;
			}
			return TerrainCache.Lerp(sky, MoonPixel(nx, ny, nz), num5);
		}

		private uint Stars(uint sky, float nx, float ny, float nz, float e, int density = 7)
		{
			float num = Math.Abs(nx);
			float num2 = Math.Abs(ny);
			float num3 = Math.Abs(nz);
			float num4;
			float num5;
			int num6;
			if (num3 >= num && num3 >= num2)
			{
				num4 = nx / num3;
				num5 = ny / num3;
				num6 = 0;
			}
			else if (num >= num2)
			{
				num4 = ny / num;
				num5 = nz / num;
				num6 = ((nx > 0f) ? 1 : 2);
			}
			else
			{
				num4 = nx / num2;
				num5 = nz / num2;
				num6 = ((ny > 0f) ? 3 : 4);
			}
			float num7 = (num4 + 1f) * 130f;
			float num8 = (num5 + 1f) * 130f;
			int num9 = (int)num7;
			int num10 = (int)num8;
			uint num11 = TerrainModel.Hash(num9 + num6 * 1000, num10 - num6 * 777);
			if ((num11 & 0xFF) >= density)
			{
				return sky;
			}
			float num12 = (float)((num11 >> 8) & 0xFF) / 255f * 0.6f + 0.2f;
			float num13 = (float)((num11 >> 16) & 0xFF) / 255f * 0.6f + 0.2f;
			float num14 = num7 - (float)num9 - num12;
			float num15 = num8 - (float)num10 - num13;
			float num16 = 0.24f;
			if (num14 > num16 || num14 < 0f - num16 || num15 > num16 || num15 < 0f - num16)
			{
				return sky;
			}
			float num17 = 0.45f + 0.55f * (float)((num11 >> 24) & 0xFF) / 255f;
			num17 *= 0.75f + 0.25f * (float)Math.Sin(Time * 3.1f + (float)(num11 & 0x3FF));
			num17 *= _starsVis * Math.Min(1f, (e - 0.03f) * 6f);
			uint b = (((num11 & 0x300) == 0) ? TerrainCache.Rgb(255, 236, 200) : TerrainCache.Rgb(220, 232, 255));
			return TerrainCache.Lerp(sky, b, num17);
		}

		private uint Clouds(uint sky, uint horizon, int c, int r, float dx, float dy, float kv, float e, float cs)
		{
			Atmosphere air = Air;
			float num = _rowCloud[r];
			float num2 = _view.X + dx * num;
			float num3 = _view.Y + dy * num;
			float num4 = num2 * 1.05f + _cloudOffU;
			float num5 = num3 * 1.05f + _cloudOffV;
			float num6 = CloudNoise.Sample(num4, num5) * 0.64f + CloudNoise.Sample(num4 * 2.3f + 37f, num5 * 2.3f + 71f) * 0.36f;
			float num7 = (num6 - (1f - air.Clouds)) * 4.5f;
			num7 += (BayerAt(c, r) - 0.5f) * 0.55f;
			if (num7 <= 0f)
			{
				return sky;
			}
			if (num7 > 1f)
			{
				num7 = 1f;
			}
			float num8 = CloudNoise.Sample(num4 + _cloudSunU, num5 + _cloudSunV) * 0.64f + CloudNoise.Sample((num4 + _cloudSunU) * 2.3f + 37f, (num5 + _cloudSunV) * 2.3f + 71f) * 0.36f;
			float num9 = 0.6f + (num6 - num8) * 4f - Math.Max(0f, num6 - (1f - air.Clouds) - 0.25f) * 0.8f;
			num9 = ((num9 < 0f) ? 0f : ((num9 > 1f) ? 1f : num9));
			num9 = (float)(int)(num9 * 4f + BayerAt(c + 2, r + 1)) / 4f;
			uint num10 = TerrainCache.Lerp(air.CloudShade, air.CloudLit, num9);
			if (cs > 0.8f && _sunVis > 0f)
			{
				num10 = TerrainCache.Lerp(num10, _sunCol, (cs - 0.8f) * 5f * 0.45f * _sunVis);
			}
			float num11 = Math.Min(1f, e * 4f);
			num10 = TerrainCache.Lerp(horizon, num10, 0.35f + 0.65f * num11);
			return TerrainCache.Lerp(sky, num10, num7 * (0.45f + 0.55f * num11));
		}

		internal static void NaturalMoon(Atmosphere a, out float disc, out float halo, out float dark, out Vec3 right, out Vec3 up, out float pu, out float pv, out float cosTerm, out float invR)
		{
			float num = a.MoonVisible * MoonAbove(a);
			disc = num * (a.Night + (1f - a.Night) * 0.4f);
			halo = num * a.Night * (0.3f + 0.7f * a.MoonLit);
			dark = 0.14f * a.Night;
			Vec3 moonDisc = a.MoonDisc;
			right = Vec3.Cross(moonDisc, new Vec3(0f, 0f, 1f)).Normalized();
			up = Vec3.Cross(right, moonDisc);
			float num2 = Vec3.Dot(a.SunDisc, right);
			float num3 = Vec3.Dot(a.SunDisc, up);
			float num4 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
			if (num4 < 1E-05f)
			{
				num2 = 1f;
				num3 = 0f;
				num4 = 1f;
			}
			float num5 = Math.Max(0f, Math.Min(1f, a.MoonLit));
			float num6 = 2f * (float)Math.Sqrt(num5 * (1f - num5));
			pu = num2 / num4 * num6;
			pv = num3 / num4 * num6;
			cosTerm = 2f * num5 - 1f;
			invR = 1f / (float)Math.Sin(0.022f * Math.Max(0.5f, a.MoonScale));
		}

		private static float MoonAbove(Atmosphere a)
		{
			return Smooth01((SunPath.Elevation(a.MoonDisc) + 0.04f) / 0.09f);
		}

		internal static void SunGlow(Atmosphere a, float low, out float bloom, out float haloGain, out float haloBroad, out float discWhite, out uint haloCol)
		{
			bloom = 1f + 0.6f * low;
			haloGain = 0.45f * low;
			haloBroad = 0.18f * low;
			discWhite = 0.5f - 0.2f * low;
			haloCol = TerrainCache.Lerp(a.SunColor, TerrainCache.Rgb(255, 128, 64), 0.3f * low);
		}

		internal static void SkyGlow(Atmosphere a, float low, out bool moon, out float gx, out float gy, out float sideStrength, out float glitter)
		{
			float num = MoonAbove(a);
			float num2 = 1f - Smooth01((a.Night - 0.6f) / 0.4f);
			float num3 = num2;
			float num4 = a.MoonVisible * num * a.Night * a.MoonLit * (1f - num2);
			float num5 = num4 / (num3 + num4 + 0.02f);
			moon = num5 > 0.5f;
			float num6 = Math.Abs(2f * num5 - 1f);
			Vec3 vec = (moon ? a.MoonDisc : a.SunDisc);
			float num7 = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
			gx = ((num7 > 0.0001f) ? (vec.X / num7) : 1f);
			gy = ((num7 > 0.0001f) ? (vec.Y / num7) : 0f);
			float num8 = 0.35f + 0.65f * low * num2;
			sideStrength = num6 * (moon ? 0.35f : num8);
			glitter = num6 * (moon ? (a.MoonVisible * num * 0.6f * a.MoonLit) : (a.SunVisible * (0.45f + 0.55f * low)));
		}

		internal float SunCloudCover(float eyeX, float eyeY, float scale, float contrast)
		{
			Atmosphere air = Air;
			Vec3 sunDisc = air.SunDisc;
			if (air.Indoors || air.Clouds <= 0.01f || sunDisc.Z <= 0.0001f)
			{
				return 0f;
			}
			float num = (float)Math.Sqrt(sunDisc.X * sunDisc.X + sunDisc.Y * sunDisc.Y);
			float num2 = ((num > 0.0001f) ? (sunDisc.Z / num) : 10000f);
			if (num2 <= 0.012f)
			{
				return 0f;
			}
			float num3 = 70f / sunDisc.Z;
			float num4 = (eyeX + sunDisc.X * num3) * 1.05f * scale + Time * 0.9f;
			float num5 = (eyeY + sunDisc.Y * num3) * 1.05f * scale + Time * 0.35f;
			float num6 = (CloudNoise.Sample(num4, num5) * 0.64f + CloudNoise.Sample(num4 * 2.3f + 37f, num5 * 2.3f + 71f) * 0.36f - (1f - air.Clouds)) * 4.5f * contrast;
			num6 = ((num6 < 0f) ? 0f : ((num6 > 1f) ? 1f : num6));
			return num6 * (0.45f + 0.55f * Math.Min(1f, num2 * 4f));
		}

		public static float SnowThreshold(int tx, int ty)
		{
			tx &= 0x1FF;
			ty &= 0x1FF;
			float num = ((float)tx + 0.5f) * 0.125f;
			float num2 = ((float)ty + 0.5f) * 0.125f;
			return ((CloudNoise.Sample(num * 8f + 3.1f, num2 * 8f + 7.7f) - 0.5f) * 1.2f + (CloudNoise.Sample(num * 4f + 71f, num2 * 4f + 19f) - 0.5f) * 0.45f + (CloudNoise.Sample(num * 32f + 41f, num2 * 32f + 17f) - 0.5f) * 0.45f + ((float)(TerrainModel.Hash(tx, ty) >> 24) * 0.003921569f - 0.5f) * 0.12f + 0.62f) * 0.8f;
		}

		private void BeginSnow(TerrainCache terrain)
		{
			_snow = (terrain.Snow.On ? terrain.Snow.Depth : null);
			_snowTable = ((_snow != null) ? SnowTableIfReady() : null);
		}

		public static void ReadySnow()
		{
			_ = SnowThresholds.Value;
		}

		public static byte[] SnowTableIfReady()
		{
			if (SnowThresholds.IsValueCreated)
			{
				return SnowThresholds.Value;
			}
			if (Interlocked.Exchange(ref _snowWarming, 1) == 0)
			{
				SlowLane.Run(delegate
				{
					_ = SnowThresholds.Value;
				});
			}
			return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void SnowGround(float px, float py, float t, ref GroundSample g)
		{
			if ((g.Fl & 7) != 0 || g.Mat == 5)
			{
				return;
			}
			byte[] snow = _snow;
			float num = (float)(int)snow[g.I00] + (float)(snow[g.I10] - snow[g.I00]) * g.Tx;
			float num2 = (float)(int)snow[g.I01] + (float)(snow[g.I11] - snow[g.I01]) * g.Tx;
			float num3 = (num + (num2 - num) * g.Ty) * 0.003921569f;
			if (num3 < 0.02f)
			{
				return;
			}
			if (g.Mat == 4)
			{
				num3 *= 0.75f;
			}
			if (t * _lodK > 60f || _snowTable == null)
			{
				float num4 = Math.Max(0f, Math.Min(1f, (num3 - 0.12f) * 1.4f));
				g.Col = TerrainCache.Lerp(g.Col, SnowLying, 0.15f * Math.Min(1f, num3 * 4f) + 0.85f * num4 * num4 * (3f - 2f * num4));
				if (num4 > 0.5f)
				{
					g.Mat = 5;
				}
				return;
			}
			float num5 = (num3 * 255f - (float)(int)_snowTable[((int)(py * 8f) & 0x1FF) * 512 + ((int)(px * 8f) & 0x1FF)]) * 0.004901961f;
			if (num5 > 0f)
			{
				g.Col = TerrainCache.Lerp(SnowLying, SnowCold, Math.Max(0f, 0.45f - num5 * 2f));
				g.Mat = 5;
			}
			else if (num5 > -0.3f && (g.Texel & 0x300) == 0)
			{
				g.Col = SnowDust;
			}
			else
			{
				g.Col = TerrainCache.Lerp(g.Col, SnowDust, Math.Min(0.3f, num3 * 0.4f));
			}
		}

		private void Flake(int gx, int gy, uint h, float time, float cell, bool low)
		{
			float num = (float)((h >> 8) & 0xFF) / 255f;
			float num2 = (float)((h >> 16) & 0xFF) / 255f;
			float num3 = (float)((h >> 24) & 0xFF) / 255f;
			float num4 = 0.75f + 0.5f * (float)((h >> 4) & 0xF) / 15f;
			float num5 = (low ? 2.6f : 9f);
			float num6 = time * num4 / num5 + num3;
			float num7 = num6 - (float)Math.Floor(num6);
			int num8 = (int)Math.Floor(num6);
			uint num9 = TerrainModel.Hash((int)h + num8 * 7919, num8);
			float num10 = ((float)gx + (num + (float)(num9 & 0xFF) / 255f) % 1f) * cell;
			float num11 = ((float)gy + (num2 + (float)((num9 >> 8) & 0xFF) / 255f) % 1f) * cell;
			float num12 = num5 * (1f - num7);
			float num13 = 0.18f + 0.2f * (float)((num9 >> 16) & 0xF) / 15f;
			float num14 = 1.1f + 1.4f * (float)((num9 >> 20) & 0xF) / 15f;
			num10 += (float)Math.Sin(time * num14 + num3 * 6.3f) * num13;
			num11 += (float)Math.Cos(time * num14 * 0.83f + num * 6.3f) * num13 * 0.8f;
			Weather weather = Weather;
			if (weather != null)
			{
				float num15 = Math.Max(-2.5f, Math.Min(2.5f, weather.WindX * 0.35f + weather.SlantX * 4f));
				float num16 = Math.Max(-2.5f, Math.Min(2.5f, weather.WindY * 0.35f + weather.SlantY * 4f));
				num10 -= num15 * num12 / num4;
				num11 -= num16 * num12 / num4;
			}
			float num17 = _terrain.SampleHeight(num10, num11) + num12;
			float num18 = num10 - _view.X;
			float num19 = num11 - _view.Y;
			float num20 = num18 * _fx + num19 * _fy;
			if (!(num20 < 0.2f) && (!Cut.On || !(num17 > Cut.Ceiling(num20))) && ProjectRel(num18, num19, num17 - _view.Z, 0.2f, out var col, out var row, out var _, out var _) && !(col < -3f) && !(col > (float)Width + 3f))
			{
				uint color = TerrainCache.Lerp(TerrainCache.Tint(SnowColor, _flatTint.R, _flatTint.G, _flatTint.B), SnowColor, 0.3f);
				float num21 = ((num20 < 11f) ? 0.92f : Math.Max(0.4f, 0.92f - (num20 - 11f) * 0.045f));
				float fog = Haze(num20, num17);
				int num22 = (int)col;
				int num23 = (int)row;
				if (num20 < 2.2f && (num9 & 3) != 0)
				{
					Plot(num22, num23, num20, color, num21, fog);
					Plot(num22 - 1, num23, num20, color, num21 * 0.5f, fog);
					Plot(num22 + 1, num23, num20, color, num21 * 0.5f, fog);
					Plot(num22, num23 - 1, num20, color, num21 * 0.5f, fog);
					Plot(num22, num23 + 1, num20, color, num21 * 0.5f, fog);
				}
				else if (num20 < 6f)
				{
					Plot(num22, num23, num20, color, num21, fog);
					Plot(num22 + 1, num23, num20, color, num21 * 0.8f, fog);
					Plot(num22, num23 + 1, num20, color, num21 * 0.8f, fog);
					Plot(num22 + 1, num23 + 1, num20, color, num21 * 0.6f, fog);
				}
				else
				{
					Plot(num22, num23, num20, color, num21, fog);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint SnowFace(uint color, ref byte kind, float nx, float ny, float nz, float snow, float cx, float cy, float cz)
		{
			if (nz <= 0f)
			{
				return color;
			}
			float num = (nz / (float)Math.Sqrt(nx * nx + ny * ny + nz * nz + 1E-20f) - 0.3f) * 2.2222223f;
			if (num <= 0f)
			{
				return color;
			}
			if (num > 1f)
			{
				num = 1f;
			}
			float num2 = num * snow * 1.5f - CloudNoise.Sample(cx * 40f + cz * 29f, cy * 40f + cz * 17f);
			if (num2 > 0f)
			{
				kind = 13;
				return TerrainCache.Lerp(color, SnowLying, Math.Min(1f, 0.75f + num2 * 2f)) | 0xFF000000u;
			}
			return TerrainCache.Lerp(color, SnowLying, num * snow * 0.35f) | 0xFF000000u;
		}

		private void RenderStreaks()
		{
			if (Streaks.Count != 0)
			{
				for (int i = 0; i < Streaks.Count; i++)
				{
					DrawStreak(Streaks[i]);
				}
				Streaks.Clear();
			}
		}

		private void DrawStreak(Streak s)
		{
			float num = (s.A.X - _view.X) * _fx + (s.A.Y - _view.Y) * _fy;
			float num2 = (s.B.X - _view.X) * _fx + (s.B.Y - _view.Y) * _fy;
			if (num < 0.12f && num2 < 0.12f)
			{
				return;
			}
			Vec3 a = s.A;
			Vec3 b = s.B;
			float num3 = s.AlphaA;
			float num4 = s.AlphaB;
			if (num < 0.12f)
			{
				float num5 = (0.12f - num) / (num2 - num);
				a += (b - a) * num5;
				num3 += (num4 - num3) * num5;
				num = 0.12f;
			}
			else if (num2 < 0.12f)
			{
				float num6 = (0.12f - num2) / (num - num2);
				b += (a - b) * num6;
				num4 += (num3 - num4) * num6;
				num2 = 0.12f;
			}
			if ((num3 <= 0.004f && num4 <= 0.004f) || !Project(a.X, a.Y, a.Z, out var col, out var row, out var depth) || !Project(b.X, b.Y, b.Z, out var col2, out var row2, out depth))
			{
				return;
			}
			float value = col2 - col;
			float value2 = row2 - row;
			if (Math.Abs(value) > 4000f || Math.Abs(value2) > 4000f)
			{
				return;
			}
			uint num7 = s.Color | 0xFF000000u;
			if (s.Glow < 1f)
			{
				num7 = TerrainCache.Lerp(TerrainCache.Tint(num7, _flatTint.R, _flatTint.G, _flatTint.B), num7, s.Glow);
			}
			float num8 = 1f / num;
			float num9 = 1f / num2;
			bool flag = Math.Abs(value2) > Math.Abs(value);
			float num10 = (flag ? row : col);
			float num11 = (flag ? row2 : col2);
			float num12 = (flag ? col : row);
			float num13 = (flag ? col2 : row2);
			float num14 = num11 - num10;
			float num15 = ((Math.Abs(num14) > 0.0001f) ? ((num13 - num12) / num14) : 0f);
			float num16 = (float)Math.Sqrt(1f + num15 * num15);
			int num17 = (int)Math.Ceiling(Math.Min(num10, num11) - 0.5f);
			int num18 = (int)Math.Ceiling(Math.Max(num10, num11) - 0.5f) - 1;
			if (num17 > num18)
			{
				num18 = num17;
			}
			int num19 = (flag ? Height : Width);
			if (num17 < 0)
			{
				num17 = 0;
			}
			if (num18 > num19 - 1)
			{
				num18 = num19 - 1;
			}
			for (int i = num17; i <= num18; i++)
			{
				float num20 = ((Math.Abs(num14) > 0.0001f) ? Math.Max(0f, Math.Min(1f, ((float)i + 0.5f - num10) / num14)) : 0.5f);
				float num21 = num8 + (num9 - num8) * num20;
				float num22 = 1f / num21;
				float num23 = num20 * num9 / num21;
				float num24 = num3 + (num4 - num3) * num23;
				if (num24 <= 0.004f)
				{
					continue;
				}
				float num25 = s.Width * _focal * num21;
				if (num25 < 1f)
				{
					num24 *= Math.Max(0.8f, num25);
					num25 = 1f;
				}
				else if (num25 > StreakMaxPixels)
				{
					num25 = StreakMaxPixels;
				}
				float num26 = num25 * 0.5f * num16;
				float num27 = num12 + (num13 - num12) * num20;
				float z = a.Z + (b.Z - a.Z) * num23;
				int num28 = (int)Math.Floor(num27 - num26);
				int num29 = (int)Math.Floor(num27 + num26);
				int num30 = (flag ? Width : Height);
				float num31 = Haze(num22, z);
				uint b2 = ((num31 > 0.002f) ? TerrainCache.Lerp(num7, _scrFog[Math.Max(0, Math.Min(Width - 1, flag ? ((int)num27) : i))], num31) : num7);
				float num32 = s.Edge * num24 * (1f - num31);
				for (int j = Math.Max(0, num28 - 1); j <= Math.Min(num30 - 1, num29 + 1); j++)
				{
					float num33 = Math.Min((float)j + 1f, num27 + num26) - Math.Max(j, num27 - num26);
					int num34 = (flag ? j : i);
					int num35 = (flag ? i : j);
					int num36 = (Height - 1 - num35) * Width + num34;
					if (num22 >= Depth[num36])
					{
						continue;
					}
					if (num33 <= 0.02f)
					{
						if (num32 > 0.01f && (j < num28 || j > num29))
						{
							Pixels[num36] = TerrainCache.Lerp(Pixels[num36], TerrainCache.Scale(Pixels[num36], 0.45f), num32);
						}
					}
					else
					{
						Pixels[num36] = TerrainCache.Lerp(Pixels[num36], b2, Math.Min(1f, num24 * Math.Min(1f, num33)));
					}
				}
			}
		}

		private void PrepareSun()
		{
			_natural = NaturalSun;
			TerrainCache terrain = _terrain;
			Atmosphere air = Air;
			_landField = _natural && !air.Indoors && terrain != null && terrain.CastShadows && terrain.SoftLandShadows && terrain.SunField != null;
			_sunFloor = 1f - Math.Max(0f, Math.Min(1f, (ShadowStrength > 0f) ? ShadowStrength : ShadowDarkness));
			_nOn = (_glintMap = false);
			if (_natural)
			{
				float flatLambert = air.FlatLambert;
				float num = 1f - _sunFloor;
				_landTint = new Tint3(1f - num * (1f - air.AmbR / Math.Max(0.05f, air.AmbR + air.LightR * flatLambert)), 1f - num * (1f - air.AmbG / Math.Max(0.05f, air.AmbG + air.LightG * flatLambert)), 1f - num * (1f - air.AmbB / Math.Max(0.05f, air.AmbB + air.LightB * flatLambert)));
				_fDirR = air.LightR * flatLambert * 0.9f;
				_fDirG = air.LightG * flatLambert * 0.9f;
				_fDirB = air.LightB * flatLambert * 0.9f;
				PlaceSunMap();
				_glintMap = _nOn && _shadowReady;
			}
		}

		private void PlaceSunMap()
		{
			_nGather = GatherRange();
			_nHold = 0f;
			Atmosphere air = Air;
			float num = Math.Max(air.LightR, Math.Max(air.LightG, air.LightB));
			if (air.Indoors || ShadowStrength <= 0f || num < 0.05f)
			{
				return;
			}
			bool flag = false;
			for (int i = 0; i < Meshes.Count; i++)
			{
				if (flag)
				{
					break;
				}
				flag = Meshes[i].Cloud == null;
			}
			if (!flag)
			{
				return;
			}
			Vec3 castLight = air.CastLight;
			long num2 = (long)Math.Round(castLight.X * 400f);
			long num3 = (long)Math.Round(castLight.Y * 400f);
			long num4 = (long)Math.Round(castLight.Z * 400f);
			if (num4 <= 0)
			{
				return;
			}
			Vec3 vec = new Vec3(num2, num3, num4).Normalized();
			Vec3 vec2 = Vec3.Cross(new Vec3(0f, 0f, 1f), vec).Normalized();
			if (Math.Abs(vec2.X) + Math.Abs(vec2.Y) < 0.0001f)
			{
				vec2 = new Vec3(1f, 0f, 0f);
			}
			Vec3 vec3 = Vec3.Cross(vec, vec2);
			int num5 = Math.Max(64, Math.Min(2048, ShadowMapSize / 64 * 64));
			float shadowReach = ShadowReach;
			float num6 = (float)num5 / (2f * shadowReach);
			float num7 = (ShadowFocusSet ? ShadowFocusX : _view.X);
			float num8 = (ShadowFocusSet ? ShadowFocusY : _view.Y);
			float num9 = (float)Math.Floor(num7 / 4f) * 4f + 2f;
			float num10 = (float)Math.Floor(num8 / 4f) * 4f + 2f;
			TerrainCache terrain = _terrain;
			int num11;
			float num12;
			if (terrain != null)
			{
				num11 = ((terrain.Width > 0) ? 1 : 0);
				if (num11 != 0)
				{
					num12 = terrain.SampleHeight(num9, num10);
					goto IL_0232;
				}
			}
			else
			{
				num11 = 0;
			}
			num12 = 0f;
			goto IL_0232;
			IL_0232:
			float num13 = num12;
			float num14 = Math.Max(0f, Math.Min(1f, vec.Z));
			float num15 = (float)Math.Sqrt(Math.Max(0f, 1f - num14 * num14));
			float num16 = ((num11 != 0) ? HighestNear(terrain, num9, num10, shadowReach) : num13) + 12f;
			float num17 = Math.Min((num11 != 0) ? terrain.OutsideHeight : num13, num13);
			if (num11 != 0 && GodCamera.LowestGround < -1.5f)
			{
				num17 = Math.Min(num17, GodCamera.LowestGround);
			}
			float num18 = Math.Max(0f, Math.Max(num16 - num13, num13 - num17));
			float num19 = FitRung(ref _nUpHeld, Math.Min(shadowReach, shadowReach * num14 + num18 * num15 + 2f + 0.02f * shadowReach), shadowReach);
			float num20 = (float)num5 / (2f * num19);
			Vec3 p = new Vec3(num9, num10, num13);
			_nC = _nAnchor.Snap(p, new Vec3(num7, num8, num13), vec2, vec3, vec, num6, num20);
			_nOu = _nAnchor.CentreU - num5 / 2;
			_nOv = _nAnchor.CentreV - num5 / 2;
			_nL = vec;
			_nA = vec2;
			_nB = vec3;
			_nCx = num9;
			_nCy = num10;
			float num21 = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
			_nSunX = ((num21 > 0.0001f) ? (vec.X / num21) : 1f);
			_nSunY = ((num21 > 0.0001f) ? (vec.Y / num21) : 0f);
			_nCot = ((num21 > 0.0001f) ? (num21 / vec.Z) : 0f);
			_nLow = num17;
			_nSize = num5;
			_nReach = shadowReach;
			_nScale = num6;
			_nUp = num19;
			_nScaleB = num20;
			_nRise = num18;
			_nInvReach = 1f / shadowReach;
			_nInvUp = 1f / num19;
			_nInvScale = 1f / num6;
			_nInvScaleB = 1f / num20;
			_nFaceOff = 0.5f / Math.Max(num6, 16f);
			_nFaceFront = FrontOfFace(Math.Max(num6, 16f));
			float num22 = _nC.X - _view.X;
			float num23 = _nC.Y - _view.Y;
			_nAllGathered = GatherReach - GatherFade >= (float)Math.Sqrt(num22 * num22 + num23 * num23) + shadowReach;
			_nKey = ((long)(int)num9 * 73856093L) ^ ((long)(int)num10 * 19349663L) ^ ((long)(int)Math.Round(num13 * 16f) * 83492791L) ^ (num2 << 20) ^ (num3 << 32) ^ (num4 << 44) ^ ((int)(shadowReach * 64f) * 2654435761u) ^ ((long)(int)(num19 * 64f) * 40503L) ^ 0x4E61747572616CL;
			_nOn = true;
			float num24 = (num - 0.05f) / 0.1f;
			_nHold = 1f - ((num24 <= 0f) ? 0f : ((num24 >= 1f) ? 1f : num24));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float MapHeld(float v)
		{
			return v + _nHold * (1f - v);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool SunwardCaster(MeshInstance inst, float dx, float dy, float reach2)
		{
			float num = dx * _nSunX + dy * _nSunY;
			float num2 = dx * _nSunY - dy * _nSunX;
			if (num > 0f && _nCot > 0f)
			{
				Mat3 basis = inst.Basis;
				MeshModel model = inst.Model;
				float num3 = inst.Position.Z + Math.Abs(basis.C0.Z) * Math.Max(0f - model.Min.X, model.Max.X) + Math.Abs(basis.C1.Z) * Math.Max(0f - model.Min.Y, model.Max.Y) + Math.Max(basis.C2.Z * model.Max.Z, basis.C2.Z * model.Min.Z);
				num = Math.Max(0f, num - Math.Max(0f, num3 - _nLow) * _nCot);
			}
			return num * num + num2 * num2 <= reach2;
		}

		internal static float FitRung(ref float held, float need, float most)
		{
			float val = (float)Math.Ceiling(Math.Max(need, 1f) / 8f) * 8f;
			if (held <= 0f || need > held || held > most)
			{
				held = Math.Min(most, val);
			}
			else
			{
				float num = Math.Min(most, (float)Math.Ceiling(Math.Max(need, 1f) / 0.85f / 8f) * 8f);
				if (num < held)
				{
					held = num;
				}
			}
			return held;
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
			int num = Math.Max(0, FastMath.FloorInt((x - reach) / 8f));
			int num2 = Math.Min(blocksX - 1, FastMath.FloorInt((x + reach) / 8f));
			int num3 = Math.Max(0, FastMath.FloorInt((y - reach) / 8f));
			int num4 = Math.Min(blocksY - 1, FastMath.FloorInt((y + reach) / 8f));
			float num5 = float.MinValue;
			for (int i = num3; i <= num4; i++)
			{
				for (int j = num; j <= num2; j++)
				{
					float num6 = blockMax[i * blocksX + j];
					num5 = Math.Max(num5, (num6 >= float.MaxValue) ? land.MaxHeight : num6);
				}
			}
			if (!(num5 > float.MinValue))
			{
				return land.OutsideHeight;
			}
			return num5;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float MapCover(float x, float y, float z)
		{
			if (!_nOn)
			{
				return 1f;
			}
			float num = x - _nC.X;
			float num2 = y - _nC.Y;
			float num3 = z - _nC.Z;
			float num4 = (num * _nA.X + num2 * _nA.Y + num3 * _nA.Z) * _nInvReach;
			float num5 = (num * _nB.X + num2 * _nB.Y + num3 * _nB.Z) * _nInvUp;
			float num6 = Math.Max((num * num + num2 * num2) * (_nInvReach * _nInvReach), num4 * num4 + num5 * num5);
			float num7 = ((num6 <= 0.7744f) ? 1f : ((num6 >= 1f) ? 0f : CoverAt((float)Math.Sqrt(num6))));
			return MapHeld((_nAllGathered || num7 <= 0f) ? num7 : Math.Min(num7, Gathered(x, y)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float Gathered(float x, float y)
		{
			float num = x - _view.X;
			float num2 = y - _view.Y;
			float num3 = num * num + num2 * num2;
			float gatherReach = GatherReach;
			float num4 = gatherReach * 0.12f;
			float num5 = gatherReach - num4;
			if (num3 <= num5 * num5)
			{
				return 1f;
			}
			if (num3 >= gatherReach * gatherReach)
			{
				return 0f;
			}
			float num6 = (gatherReach - (float)Math.Sqrt(num3)) / num4;
			if (!(num6 <= 0f))
			{
				if (!(num6 >= 1f))
				{
					return num6;
				}
				return 1f;
			}
			return 0f;
		}

		private float GatherRange()
		{
			float gatheredRange = GatheredRange;
			if (gatheredRange > 0f)
			{
				return Math.Max(1f, gatheredRange - 4f);
			}
			float num = (ShadowFocusSet ? (_view.X - ShadowFocusX) : 0f);
			float num2 = (ShadowFocusSet ? (_view.Y - ShadowFocusY) : 0f);
			return Math.Max(1f, Math.Max(BillboardDistance, (float)Math.Sqrt(num * num + num2 * num2) + 30f) - 4f);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float CoverAt(float r)
		{
			float num = (1f - r) * 8.333334f;
			if (!(num <= 0f))
			{
				if (!(num >= 1f))
				{
					return num;
				}
				return 1f;
			}
			return 0f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float LandVis(float x, float y, float z)
		{
			return SunShadowField.Visibility(_terrain, x, y, z, withCanopy: false);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float Lift(float vis)
		{
			return _sunFloor + (1f - _sunFloor) * vis;
		}

		private float ThingVis(float x, float y, float z)
		{
			if (!_landField)
			{
				return 1f;
			}
			float num = MapCover(x, y, z);
			float num2 = ((num > 0f) ? (SunShadowField.Visibility(_terrain, x, y, z, withCanopy: false) * num) : 0f);
			if (num < 1f)
			{
				num2 += SunShadowField.Visibility(_terrain, x, y, z, withCanopy: true) * (1f - num);
			}
			return num2;
		}

		private float GlintVis(float x, float y, float z, float t, float vis)
		{
			float num = MapCover(x, y, z);
			if (num <= 0f)
			{
				return vis;
			}
			float num2 = x - _sC.X;
			float num3 = y - _sC.Y;
			float num4 = z - _sC.Z;
			float num5 = (float)ShadowSize * 0.5f;
			int num6 = FastMath.FloorInt(num5 + (num2 * _sA.X + num3 * _sA.Y + num4 * _sA.Z) * _sScale);
			int num7 = FastMath.FloorInt(num5 + (num2 * _sB.X + num3 * _sB.Y + num4 * _sB.Z) * _sScaleB);
			if (num6 < 0 || num7 < 0 || num6 >= ShadowSize || num7 >= ShadowSize)
			{
				return vis;
			}
			if (_shadowMap[num7 * ShadowSize + num6] <= num2 * _sL.X + num3 * _sL.Y + num4 * _sL.Z + 0.08f + t * 0.004f)
			{
				return vis;
			}
			float nHold = _nHold;
			if (!_landField)
			{
				return 1f - num * (1f - nHold);
			}
			return Math.Max(0f, vis - num * Math.Max(0f, LandVis(x, y, z) - nHold));
		}

		private float ThingVisBeside(float x, float y, float z, float half)
		{
			if (!_landField)
			{
				return 1f;
			}
			float num = MapCover(x, y, z);
			float num2 = SunShadowField.Visibility(_terrain, x, y, z, withCanopy: false);
			if (num >= 1f)
			{
				return num2;
			}
			Sunward(x, y, half, out var sx, out var sy);
			float num3 = Math.Min(num2, SunShadowField.Visibility(_terrain, sx, sy, Math.Max(z, _terrain.SampleHeight(sx, sy)), withCanopy: true));
			return num2 * num + num3 * (1f - num);
		}

		private void Sunward(float x, float y, float half, out float sx, out float sy)
		{
			Vec3 castLight = Air.CastLight;
			float num = (float)Math.Sqrt(castLight.X * castLight.X + castLight.Y * castLight.Y);
			float num2 = ((num > 0.0001f) ? ((half + 0.5f) / num) : 0f);
			sx = x + castLight.X * num2;
			sy = y + castLight.Y * num2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Tint3 FlatLight(float lift)
		{
			Atmosphere air = Air;
			return new Tint3(air.AmbR + _fDirR * lift, air.AmbG + _fDirG * lift, air.AmbB + _fDirB * lift);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static byte Share(float vis)
		{
			return (byte)((!(vis <= 0f)) ? ((vis >= 1f) ? 255u : ((uint)(int)(vis * 255f + 0.5f))) : 0u);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int ShareTint(int tint, int share)
		{
			return 256 - (256 - tint) * share / 255;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ShadeShare(int idx, Tint3 shadow)
		{
			int num = _sunlit[idx];
			if (num != 0)
			{
				Pixels[idx] = TerrainCache.Tint(Pixels[idx], ShareTint(shadow.R, num), ShareTint(shadow.G, num), ShareTint(shadow.B, num));
				_sunlit[idx] = 0;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void MapJitter(float u, float v, float wide, float cells, int ou, int ov, out float ju, out float jv)
		{
			int num = FastMath.FloorInt(u * cells);
			int num2 = FastMath.FloorInt(v * cells);
			int num3 = ((cells > 8f) ? 4 : ((cells > 4f) ? 3 : 2));
			float num4 = ((cells > 8f) ? 0.0625f : ((cells > 4f) ? 0.125f : 0.25f));
			uint num5 = TerrainModel.Hash((num >> num3) + ou, (num2 >> num3) + ov);
			float num6 = JitterSteps[(((num2 + (int)((num5 >> 2) & 3)) & 3) << 2) | ((num + (int)(num5 & 3)) & 3)];
			float num7 = JitterSteps[(((num + (int)((num5 >> 6) & 3)) & 3) << 2) | ((num2 + (int)((num5 >> 4) & 3)) & 3)];
			ju = (((float)num + 0.5f) * num4 - u + num6) * wide;
			jv = (((float)num2 + 0.5f) * num4 - v + num7) * wide;
		}

		private static float[] MakeJitterSteps()
		{
			int[] array = new int[16]
			{
				0, 8, 2, 10, 12, 4, 14, 6, 3, 11,
				1, 9, 15, 7, 13, 5
			};
			float[] array2 = new float[16];
			for (int i = 0; i < 16; i++)
			{
				array2[i] = ((float)array[i] - 7.5f) / 15f;
			}
			return array2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float MapCells(float t, float d16, float d8)
		{
			float num = ((t > 0.05f) ? t : 0.05f);
			if (!(num < d16))
			{
				if (!(num < d8))
				{
					return 4f;
				}
				return 8f;
			}
			return 16f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void MapCellDepths(float focal, float k, out float d16, out float d8)
		{
			d16 = focal / (24f * k);
			d8 = focal / (12f * k);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float FaceStipple(float nl)
		{
			float num = Math.Abs(nl) * 2f;
			if (!(num >= 0.5f))
			{
				return num;
			}
			return 0.5f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float FaceMapShare(float nl)
		{
			float num = (Math.Abs(nl) - 0.05f) * 9.999999f;
			if (!(num <= 0f))
			{
				if (!(num >= 1f))
				{
					return num * num * (3f - 2f * num);
				}
				return 1f;
			}
			return 0f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float FrontOfFace(float k)
		{
			return 0.15f + 0.5f / k;
		}

		private void PrepareCanvas()
		{
			int width = Width;
			int height = Height;
			if (_rowK.Length != height)
			{
				_rowK = new float[height];
				_rowD = new float[height];
				_rowG = new float[height];
			}
			if (_scrU.Length != width)
			{
				_scrU = new float[width];
			}
			float cq = _lens.Cq;
			float sq = _lens.Sq;
			_gMin = float.MaxValue;
			float num = 0f;
			for (int i = 0; i < height; i++)
			{
				float num2 = (_horizon - ((float)i + 0.5f)) / _focal;
				float num3 = cq - num2 * sq;
				_rowK[i] = (_tilted ? _lens.SlopeOf(num2) : num2);
				_rowG[i] = num3;
				_rowD[i] = (_tilted ? (1f / num3) : 1f);
				if (num3 < _gMin)
				{
					_gMin = num3;
				}
				if (num3 > num)
				{
					num = num3;
				}
			}
			for (int j = 0; j < width; j++)
			{
				_scrU[j] = ((float)j + 0.5f - (float)width * 0.5f) / _focal;
			}
			if (!_tilted)
			{
				_cw = width;
				_cPix = Pixels;
				_cDep = Depth;
				_cLit = _sunlit;
				if (_colS.Length != width)
				{
					_colS = new float[width];
					_colTop = new int[width];
					_colBot = new int[width];
				}
				for (int k = 0; k < width; k++)
				{
					_colS[k] = _scrU[k];
					_colTop[k] = 0;
					_colBot[k] = height;
				}
				return;
			}
			float num4 = (float)width * 0.5f / _focal;
			float num5 = 1f / num;
			float num6 = 1f / _gMin;
			float num7 = num4 * num5;
			float num8 = num5 / _focal;
			float num9 = num4 * num6 * 1.002f + num8;
			int num10 = ((num7 > 0f && num9 > num7) ? ((int)Math.Ceiling((double)(num7 / num8) * Math.Log(num9 / num7))) : 0);
			int num11 = width + 2 * num10;
			if (num11 > width * 8)
			{
				num11 = width * 8;
			}
			num10 = (num11 - width) / 2;
			num11 = width + 2 * num10;
			if (_colS.Length < num11)
			{
				_colS = new float[num11 + num11 / 4];
				_colTop = new int[_colS.Length];
				_colBot = new int[_colS.Length];
			}
			for (int l = 0; l < width; l++)
			{
				_colS[num10 + l] = ((float)l + 0.5f - (float)width * 0.5f) * num8;
			}
			for (int m = 1; m <= num10; m++)
			{
				float num12 = num7 * (float)Math.Exp((float)m * num8 / num7);
				_colS[num10 + width - 1 + m] = num12;
				_colS[num10 - m] = 0f - num12;
			}
			float horizon = _horizon;
			float focal = _focal;
			for (int n = 0; n < num11; n++)
			{
				float num13 = Math.Abs(_colS[n]) * (1f - num8 / num7) - num8;
				int num14 = 0;
				int num15 = height;
				if (num13 > num4 * num5)
				{
					float num16 = cq - num4 / num13;
					if (sq < 0f)
					{
						float num17 = (0f - num16) / (0f - sq);
						num14 = (int)Math.Floor(horizon - num17 * focal) - 1;
						if (num14 < 0)
						{
							num14 = 0;
						}
						if (num14 > height)
						{
							num14 = height;
						}
					}
					else
					{
						float num18 = num16 / sq;
						num15 = (int)Math.Ceiling(horizon - num18 * focal) + 1;
						if (num15 > height)
						{
							num15 = height;
						}
						if (num15 < 0)
						{
							num15 = 0;
						}
					}
				}
				_colTop[n] = num14;
				_colBot[n] = num15;
			}
			_cw = num11;
			int num19 = num11 * height;
			if (_stripPix.Length < num19)
			{
				_stripPix = new uint[num19 + num19 / 4];
				_stripDep = new float[_stripPix.Length];
				_stripLit = new byte[_stripPix.Length];
			}
			_cPix = _stripPix;
			_cDep = _stripDep;
			_cLit = _stripLit;
			_lutU = Math.Abs(_colS[0]) + num8;
			_lutScale = 4f / num8;
			int num20 = (int)(2f * _lutU * _lutScale) + 2;
			if (_stripLut.Length < num20)
			{
				_stripLut = new int[num20 + num20 / 4];
			}
			int num21 = 0;
			for (int num22 = 0; num22 < num20; num22++)
			{
				for (float num23 = ((float)num22 + 0.5f) / _lutScale - _lutU; num21 + 1 < num11 && Math.Abs(_colS[num21 + 1] - num23) <= Math.Abs(_colS[num21] - num23); num21++)
				{
				}
				_stripLut[num22] = num21;
			}
		}

		private void SplatStrips()
		{
			For(Height, SplatRow, UseParallel);
		}

		private void SplatRow(int r)
		{
			int width = Width;
			int height = Height;
			int cw = _cw;
			uint[] pixels = Pixels;
			uint[] stripPix = _stripPix;
			float[] depth = Depth;
			float[] stripDep = _stripDep;
			float[] rowD = _rowD;
			float[] scrU = _scrU;
			byte[] sunlit = _sunlit;
			byte[] stripLit = _stripLit;
			int[] stripLut = _stripLut;
			float lutU = _lutU;
			float lutScale = _lutScale;
			int num = stripLut.Length - 1;
			float fx = _fx;
			float fy = _fy;
			float rx = _rx;
			float ry = _ry;
			uint[] fogCol = _fogCol;
			int num2 = (height - 1 - r) * width;
			int num3 = (height - 1 - r) * cw;
			float num4 = rowD[r];
			for (int i = 0; i < width; i++)
			{
				float num5 = scrU[i] * num4;
				int num6 = (int)((num5 + lutU) * lutScale);
				if (num6 < 0)
				{
					num6 = 0;
				}
				else if (num6 > num)
				{
					num6 = num;
				}
				int num7 = stripLut[num6];
				int num8 = num3 + num7;
				float num9 = (depth[num2 + i] = stripDep[num8]);
				sunlit[num2 + i] = stripLit[num8];
				if (num9 != float.MaxValue)
				{
					pixels[num2 + i] = stripPix[num8];
					continue;
				}
				float num10 = fx + rx * num5;
				float num11 = fy + ry * num5;
				pixels[num2 + i] = SkyRay(i, num7, r, num10, num11, 1f / (float)Math.Sqrt(num10 * num10 + num11 * num11), fogCol[num7]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float SlopeAtRow(float y)
		{
			if (!_tilted)
			{
				return (_horizon - y) / _focal;
			}
			return _lens.SlopeOf((_horizon - y) / _focal);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float RowAtSlope(float k)
		{
			float num = (_tilted ? (_horizon - _lens.RowOfSlope(k) * _focal) : (_horizon - k * _focal));
			if (!(num < -4f))
			{
				if (!(num > (float)Height + 4f))
				{
					return num;
				}
				return (float)Height + 4f;
			}
			return -4f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool ProjectRel(float relx, float rely, float dz, float near, out float col, out float row, out float t, out float scale)
		{
			t = relx * _fx + rely * _fy;
			float num = (_tilted ? (t * _lens.Cq + dz * _lens.Sq) : t);
			if (num < near)
			{
				col = (row = (scale = 0f));
				return false;
			}
			scale = _focal / num;
			col = (float)Width * 0.5f + (relx * _rx + rely * _ry) * scale;
			row = _horizon - (_tilted ? (dz * _lens.Cq - t * _lens.Sq) : dz) * scale;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void PixelRay(float px, float py, out float side, out float rise)
		{
			float num = (px - (float)Width * 0.5f) / _focal;
			if (!_tilted)
			{
				side = num;
				rise = (_horizon - py) / _focal;
				return;
			}
			float num2 = (_horizon - py) / _focal;
			float num3 = _lens.Cq - num2 * _lens.Sq;
			if (num3 < 0.0001f)
			{
				num3 = 0.0001f;
			}
			side = num / num3;
			rise = (_lens.Sq + num2 * _lens.Cq) / num3;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float RowOfPoint(float t, float dz)
		{
			float num = t * _lens.Cq + dz * _lens.Sq;
			if (num < 0.001f)
			{
				if (!(dz > 0f))
				{
					return (float)Height + 4f;
				}
				return -4f;
			}
			float num2 = _horizon - (dz * _lens.Cq - t * _lens.Sq) * _focal / num;
			if (!(num2 < -4f))
			{
				if (!(num2 > (float)Height + 4f))
				{
					return num2;
				}
				return (float)Height + 4f;
			}
			return -4f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float NearestT(float zv)
		{
			if (!_tilted)
			{
				return zv;
			}
			return zv * _gMin;
		}

		private void PrepareVeil()
		{
			_veilOn = Cut.On;
			if (_veilOn)
			{
				int num = _cw * Height;
				if (_veilA.Length < num)
				{
					_veilA = new byte[num + num / 4];
					_veilPix = new uint[_veilA.Length];
					_veilDep = new float[_veilA.Length];
				}
				Array.Clear(_veilA, 0, num);
			}
		}

		private void VeilStep(int c, float t, float h, bool inside, ref GroundSample g, ref int yVeil, float prevT, float prevH, bool prevOk, float px, float py, float dx, float dy, uint fogCol, int topRow)
		{
			float z = _view.Z;
			float num = (_tilted ? RowAtSlope((h - z) / t) : (_horizon - (h - z) * _focal / t));
			int num2 = 0;
			if (num > 0f)
			{
				num2 = (int)num;
				if (num > (float)num2)
				{
					num2++;
				}
			}
			if (num2 < topRow)
			{
				num2 = topRow;
			}
			int num3 = yVeil;
			if (num2 >= num3)
			{
				return;
			}
			yVeil = num2;
			if (inside)
			{
				SampleLook(px, py, t, ref g);
			}
			uint num4 = ShadeSample(g.Col, g.Sun, g.Open, g.Flags, g.Mat, g.Texel, px, py, t, h, c, fogCol, g.Coast);
			bool flag = prevOk && g.Inside && (g.Flags & 3) == 0 && num3 - num2 > 2 && h > prevH;
			SightCut cut = Cut;
			uint[] veilPix = _veilPix;
			float[] veilDep = _veilDep;
			float[] rowK = _rowK;
			byte[] veilA = _veilA;
			int cw = _cw;
			int height = Height;
			float num5 = (flag ? ((h - prevH) / (t - prevT)) : 0f);
			float lodK = _lodK;
			GroundSample g2 = default(GroundSample);
			float num6 = -1f;
			uint num7 = 0u;
			for (int i = num2; i < num3; i++)
			{
				int num8 = (height - 1 - i) * cw + c;
				float num9 = rowK[i];
				float num10 = t;
				uint num11 = num4;
				if (flag)
				{
					float num12 = num9 - num5;
					num10 = ((num12 < -1E-06f) ? ((prevH - num5 * prevT - z) / num12) : t);
					if (num10 > t || num10 < prevT)
					{
						num10 = ((num10 > t) ? t : prevT);
					}
					if (num10 <= t - 0.02f)
					{
						if (num6 < 0f || Math.Abs(num10 - num6) > 0.04f + num10 * (0.004f * lodK))
						{
							float px2 = _view.X + dx * num10;
							float py2 = _view.Y + dy * num10;
							SampleGround(px2, py2, num10, ref g2);
							num7 = ShadeSample(g2.Col, g2.Sun, g2.Open, g2.Flags, g2.Mat, g2.Texel, px2, py2, num10, z + num9 * num10, c, fogCol, g2.Coast);
							num6 = num10;
						}
						num11 = num7;
					}
				}
				veilPix[num8] = num11;
				veilDep[num8] = num10;
				veilA[num8] = (byte)Math.Max(1, Math.Min(255, (int)(cut.Veil(num10, Math.Min(h, z + num9 * num10)) * 255f + 0.5f)));
			}
		}

		private void ApplyVeil(uint[] pix, float[] dep, int w)
		{
			if (!_veilOn)
			{
				return;
			}
			_veilOn = false;
			SightCut cut = Cut;
			TerrainCache terrain = _terrain;
			byte[] veilA = _veilA;
			uint[] veilPix = _veilPix;
			float[] veilDep = _veilDep;
			float[] colS = _colS;
			float[] rowK = _rowK;
			float x = _view.X;
			float y = _view.Y;
			float z = _view.Z;
			float fx = _fx;
			float fy = _fy;
			float rx = _rx;
			float ry = _ry;
			int height = Height;
			int num = w * height;
			if (_veilIn.Length < num)
			{
				_veilIn = new float[num + num / 4];
			}
			float[] veilIn = _veilIn;
			for (int i = 0; i < num; i++)
			{
				veilIn[i] = -1f;
				if (veilA[i] != 0)
				{
					float num2 = dep[i];
					if (!(num2 < veilDep[i] - 0.02f))
					{
						float num3 = colS[i % w];
						veilIn[i] = ((num2 < cut.Target) ? cut.Within(num2, z + rowK[height - 1 - i / w] * num2, terrain.SampleHeight(x + (fx + rx * num3) * num2, y + (fy + ry * num3) * num2)) : 0f);
					}
				}
			}
			int num4 = Math.Max(2, height / 45);
			for (int j = 0; j < w; j++)
			{
				float num5 = 0f;
				int num6 = 0;
				for (int k = 0; k < num4 && k < height; k++)
				{
					float num7 = veilIn[k * w + j];
					if (num7 >= 0f)
					{
						num5 += num7;
						num6++;
					}
				}
				for (int l = 0; l < height; l++)
				{
					if (l + num4 < height)
					{
						float num8 = veilIn[(l + num4) * w + j];
						if (num8 >= 0f)
						{
							num5 += num8;
							num6++;
						}
					}
					if (l - num4 - 1 >= 0)
					{
						float num9 = veilIn[(l - num4 - 1) * w + j];
						if (num9 >= 0f)
						{
							num5 -= num9;
							num6--;
						}
					}
					int num10 = l * w + j;
					if (!(veilIn[num10] < 0f))
					{
						float num11 = (float)(int)veilA[num10] / 255f;
						num11 += (1f - num11) * Math.Max(0f, num5) / (float)num6;
						pix[num10] = ((num11 >= 1f) ? veilPix[num10] : TerrainCache.Lerp(pix[num10], veilPix[num10], num11));
					}
				}
			}
		}

		private void RenderWeatherShadows()
		{
			CloudShadowMs = (CloudMs = (FallMs = 0f));
			CloudCover = (CloudCentreCover = 0f);
			Weather weather = Weather;
			if (weather != null && !Air.Indoors && weather.Storms.Count != 0)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				weather.Bounds();
				ShadeUnderClouds(weather);
				CloudShadowMs = (float)stopwatch.Elapsed.TotalMilliseconds;
			}
		}

		private void RenderWeather()
		{
			_stormsDrawn = false;
			Weather weather = Weather;
			if (weather != null && !Air.Indoors && weather.Storms.Count != 0)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				RenderStormClouds(weather);
				_stormsDrawn = true;
				CloudMs = (float)stopwatch.Elapsed.TotalMilliseconds;
			}
		}

		private float StormCloudAt(int x, int y)
		{
			if (!_stormsDrawn || _cloudA.Length < _cloudW * _cloudH || _cloudW <= 0 || _cloudH <= 0)
			{
				return 0f;
			}
			float num = ((float)x + 0.5f) / 3f - 0.5f;
			float num2 = ((float)y + 0.5f) / 3f - 0.5f;
			int num3 = Math.Max(0, Math.Min(_cloudW - 1, (int)Math.Floor(num)));
			int num4 = Math.Min(_cloudW - 1, num3 + 1);
			int num5 = Math.Max(0, Math.Min(_cloudH - 1, (int)Math.Floor(num2)));
			int num6 = Math.Min(_cloudH - 1, num5 + 1);
			float num7 = Math.Max(0f, Math.Min(1f, num - (float)num3));
			float num8 = Math.Max(0f, Math.Min(1f, num2 - (float)num5));
			int num9 = num5 * _cloudW + num3;
			int num10 = num5 * _cloudW + num4;
			int num11 = num6 * _cloudW + num3;
			int num12 = num6 * _cloudW + num4;
			float num13 = Math.Min(Math.Min(_cloudT[num9], _cloudT[num10]), Math.Min(_cloudT[num11], _cloudT[num12]));
			if (Depth[(Height - 1 - y) * Width + x] < num13)
			{
				return 0f;
			}
			float num14 = (_cloudA[num9] + (_cloudA[num10] - _cloudA[num9]) * num7) * (1f - num8) + (_cloudA[num11] + (_cloudA[num12] - _cloudA[num11]) * num7) * num8;
			if (!(num14 <= 0f))
			{
				if (!(num14 >= 1f))
				{
					return num14;
				}
				return 1f;
			}
			return 0f;
		}

		private void RenderWeatherFront()
		{
			Weather weather = Weather;
			if (weather != null && !Air.Indoors)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				RenderPrecipitation(weather);
				RenderBolts(weather);
				FallMs = (float)stopwatch.Elapsed.TotalMilliseconds;
			}
		}

		private void RenderStormClouds(Weather w)
		{
			_cloudW = (Width + 3 - 1) / 3;
			_cloudH = (Height + 3 - 1) / 3;
			int num = _cloudW * _cloudH;
			if (_cloudA.Length != num)
			{
				_cloudA = new float[num];
				_cloudT = new float[num];
				_cloudC = new uint[num];
			}
			Atmosphere air = Air;
			float[] cloudKeep = null;
			if (Cut.On)
			{
				if (_stormKeep.Length < w.Storms.Count)
				{
					_stormKeep = new float[w.Storms.Count * 2];
				}
				for (int i = 0; i < w.Storms.Count; i++)
				{
					_stormKeep[i] = Cut.Storm(w.Storms[i].X, w.Storms[i].Y, w.Storms[i].HalfX, w.Storms[i].HalfY, w.Base, w.Base + w.Thickness);
				}
				cloudKeep = _stormKeep;
			}
			Math.Max(air.LightR, Math.Max(air.LightG, air.LightB));
			_cloudWeather = w;
			_cloudKeep = cloudKeep;
			For(_cloudH, CloudRow, UseParallel);
			_cloudWeather = null;
			_cloudKeep = null;
			float num2 = 0f;
			float num3 = 0f;
			int num4 = 0;
			for (int j = 0; j < _cloudH; j++)
			{
				for (int k = 0; k < _cloudW; k++)
				{
					float num5 = _cloudA[j * _cloudW + k];
					num2 += num5;
					if (k * 3 >= _cloudW && k * 3 < _cloudW * 2 && j * 3 >= _cloudH && j * 3 < _cloudH * 2)
					{
						num3 += num5;
						num4++;
					}
				}
			}
			CloudCover = num2 / (float)Math.Max(1, num);
			CloudCentreCover = num3 / (float)Math.Max(1, num4);
			For(Height, CloudCompositeRow, UseParallel);
		}

		private void CloudRow(int cy)
		{
			Weather cloudWeather = _cloudWeather;
			float[] cloudKeep = _cloudKeep;
			for (int i = 0; i < _cloudW; i++)
			{
				int num = cy * _cloudW + i;
				_cloudA[num] = 0f;
				float num2 = (float)Math.Min(Width - 1, i * 3 + 1) + 0.5f;
				float py = (float)Math.Min(Height - 1, cy * 3 + 1) + 0.5f;
				PixelRay(num2, py, out var side, out var rise);
				float dx = _fx + _rx * side;
				float dy = _fy + _ry * side;
				MarchCloud(cloudWeather, dx, dy, rise, BayerAt(i, cy), (int)num2, cloudKeep, out _cloudA[num], out _cloudT[num], out _cloudC[num]);
			}
		}

		private void CloudCompositeRow(int y)
		{
			float num = ((float)y + 0.5f) / 3f - 0.5f;
			int num2 = Math.Max(0, Math.Min(_cloudH - 1, (int)Math.Floor(num)));
			int num3 = Math.Min(_cloudH - 1, num2 + 1);
			float num4 = Math.Max(0f, Math.Min(1f, num - (float)num2));
			for (int i = 0; i < Width; i++)
			{
				float num5 = ((float)i + 0.5f) / 3f - 0.5f;
				int num6 = Math.Max(0, Math.Min(_cloudW - 1, (int)Math.Floor(num5)));
				int num7 = Math.Min(_cloudW - 1, num6 + 1);
				float num8 = Math.Max(0f, Math.Min(1f, num5 - (float)num6));
				int num9 = num2 * _cloudW + num6;
				int num10 = num2 * _cloudW + num7;
				int num11 = num3 * _cloudW + num6;
				int num12 = num3 * _cloudW + num7;
				float num13 = _cloudA[num9];
				float num14 = _cloudA[num10];
				float num15 = _cloudA[num11];
				float num16 = _cloudA[num12];
				if (num13 + num14 + num15 + num16 < 0.004f)
				{
					continue;
				}
				float num17 = (num13 + (num14 - num13) * num8) * (1f - num4) + (num15 + (num16 - num15) * num8) * num4;
				num17 += (BayerAt(i, y) - 0.5f) * 0.18f;
				if (!(num17 <= 0.02f))
				{
					if (num17 > 1f)
					{
						num17 = 1f;
					}
					int num18 = (Height - 1 - y) * Width + i;
					float num19 = Math.Min(Math.Min(_cloudT[num9], _cloudT[num10]), Math.Min(_cloudT[num11], _cloudT[num12]));
					if (!(Depth[num18] < num19))
					{
						uint a = TerrainCache.Lerp(_cloudC[num9], _cloudC[num10], num8);
						uint b = TerrainCache.Lerp(_cloudC[num11], _cloudC[num12], num8);
						Pixels[num18] = TerrainCache.Lerp(Pixels[num18], TerrainCache.Lerp(a, b, num4), num17);
					}
				}
			}
		}

		private void MarchCloud(Weather w, float dx, float dy, float kv, float jitter, int column, float[] keep, out float alpha, out float front, out uint color)
		{
			alpha = 0f;
			front = float.MaxValue;
			color = 0u;
			float z = _view.Z;
			float num = w.Base;
			float num2 = w.Base + w.Thickness;
			float num3;
			float num4;
			if (Math.Abs(kv) < 0.0001f)
			{
				if (z < num || z > num2)
				{
					return;
				}
				num3 = 0.1f;
				num4 = 240f;
			}
			else
			{
				float val = (num - z) / kv;
				float val2 = (num2 - z) / kv;
				num3 = Math.Max(0.1f, Math.Min(val, val2));
				num4 = Math.Min(240f, Math.Max(val, val2));
			}
			if (num4 <= num3)
			{
				return;
			}
			float num5 = float.MaxValue;
			float num6 = float.MinValue;
			for (int i = 0; i < w.Storms.Count; i++)
			{
				StormCloud stormCloud = w.Storms[i];
				if (keep != null && keep[i] <= 0.01f)
				{
					continue;
				}
				float t = num3;
				float t2 = num4;
				if (ClipToBox(_view.X, dx, stormCloud.X - stormCloud.HalfX * 1.3f, stormCloud.X + stormCloud.HalfX * 1.3f, ref t, ref t2) && ClipToBox(_view.Y, dy, stormCloud.Y - stormCloud.HalfY * 1.3f, stormCloud.Y + stormCloud.HalfY * 1.3f, ref t, ref t2))
				{
					if (t < num5)
					{
						num5 = t;
					}
					if (t2 > num6)
					{
						num6 = t2;
					}
				}
			}
			if (num6 <= num5)
			{
				return;
			}
			num3 = num5;
			num4 = num6;
			float num7 = (float)Math.Sqrt(dx * dx + dy * dy + kv * kv);
			int num8 = Math.Max(4, Math.Min(12, (int)Math.Ceiling((num4 - num3) * num7 / 2.2f)));
			float num9 = (num4 - num3) / (float)num8;
			float num10 = 1f;
			float num11 = 0f;
			float num12 = 0f;
			float num13 = 0f;
			Atmosphere air = Air;
			SightCut cut = Cut;
			for (int j = 0; j < num8; j++)
			{
				if (!(num10 > 0.03f))
				{
					break;
				}
				float num14 = num3 + ((float)j + jitter) * num9;
				float num15 = _view.X + dx * num14;
				float y = _view.Y + dy * num14;
				float num16 = z + kv * num14;
				float num17 = (cut.On ? cut.CloudNear(num14 * num7) : 1f);
				if (num17 <= 0.01f)
				{
					continue;
				}
				float num18 = (num16 - num) / w.Thickness;
				int which;
				float billow;
				float num19 = w.Density(num15, y, num18, Time, out which, out billow, keep) * num17;
				if (!(num19 <= 0.01f))
				{
					StormCloud stormCloud2 = w.Storms[which];
					float num20 = 1f - (float)Math.Exp((0f - num19) * num9 * num7 * 1.2f);
					bool num21 = stormCloud2.Drops == Precip.Snow;
					float num22 = (num21 ? (0.42f + 0.4f * Math.Min(1f, num18 * 1.3f) + (billow - 0.45f) * 0.7f) : (0.2f + 0.55f * Math.Min(1f, num18 * 1.3f) + (billow - 0.45f) * 1.1f));
					num22 = ((num22 < 0.05f) ? 0.05f : ((num22 > 1f) ? 1f : num22));
					num22 = (float)Math.Floor(num22 * 5f + jitter * 0.9f) / 5f;
					float num23 = (num21 ? 1.5f : 0.9f);
					float num24 = (num21 ? 0.7f : 0.9f);
					float num25 = air.AmbR * num23 + air.LightR * num22 * num24;
					float num26 = air.AmbG * num23 + air.LightG * num22 * num24;
					float num27 = air.AmbB * num23 + air.LightB * num22 * num24;
					uint num28 = TerrainCache.Tint(stormCloud2.Color, (int)(num25 * 256f), (int)(num26 * 256f), (int)(num27 * 256f));
					if (stormCloud2.Glows)
					{
						float val3 = (0.55f - num18 * 0.4f) * (0.85f + 0.15f * (float)Math.Sin(Time * 3.3f + num15 * 0.4f));
						num28 = TerrainCache.Lerp(num28, TerrainCache.Scale(stormCloud2.Color, 1.35f), Math.Max(0f, val3));
					}
					if (w.Flash > 0f)
					{
						num28 = TerrainCache.Lerp(num28, 4294963440u, w.Flash * 0.55f);
					}
					float num29 = Haze(num14, num16) * 0.7f;
					if (num29 > 0.01f)
					{
						num28 = TerrainCache.Lerp(num28, _scrFog[column], num29);
					}
					float num30 = num10 * num20;
					num11 += (float)(num28 & 0xFF) * num30;
					num12 += (float)((num28 >> 8) & 0xFF) * num30;
					num13 += (float)((num28 >> 16) & 0xFF) * num30;
					num10 *= 1f - num20;
					if (front == float.MaxValue && num10 < 0.7f)
					{
						front = num14;
					}
				}
			}
			alpha = 1f - num10;
			if (alpha < 0.004f)
			{
				alpha = 0f;
				return;
			}
			if (front == float.MaxValue)
			{
				front = num4;
			}
			float num31 = 1f / alpha;
			color = TerrainCache.Rgb(Math.Min(255, (int)(num11 * num31)), Math.Min(255, (int)(num12 * num31)), Math.Min(255, (int)(num13 * num31)));
		}

		private static bool ClipToBox(float o, float d, float lo, float hi, ref float t0, ref float t1)
		{
			if (Math.Abs(d) < 1E-06f)
			{
				if (o >= lo)
				{
					return o <= hi;
				}
				return false;
			}
			float num = (lo - o) / d;
			float num2 = (hi - o) / d;
			if (num > num2)
			{
				float num3 = num;
				num = num2;
				num2 = num3;
			}
			if (num > t0)
			{
				t0 = num;
			}
			if (num2 < t1)
			{
				t1 = num2;
			}
			return t1 > t0;
		}

		private void ShadeUnderClouds(Weather w)
		{
			Vec3 light = _light;
			if (!(light.Z < 0.05f) && _litR + _litG + _litB >= 30)
			{
				float shadeKx = light.X / light.Z;
				float shadeKy = light.Y / light.Z;
				_shadeWeather = w;
				_shadeKx = shadeKx;
				_shadeKy = shadeKy;
				_shadeTint = _cloudTint;
				For((Height + 3 - 1) / 3, CloudShadeRow, UseParallel);
				_shadeWeather = null;
			}
		}

		private float CloudShadeAt(float sx, float sy, float t)
		{
			Weather shadeWeather = _shadeWeather;
			float shadeKx = _shadeKx;
			float shadeKy = _shadeKy;
			if (t == float.MaxValue || t > 240f)
			{
				return 0f;
			}
			PixelRay(sx, sy, out var side, out var rise);
			float num = _view.X + (_fx + _rx * side) * t;
			float num2 = _view.Y + (_fy + _ry * side) * t;
			float num3 = _view.Z + rise * t;
			if (num3 > shadeWeather.Base)
			{
				return 0f;
			}
			float num4 = shadeWeather.Base + shadeWeather.Thickness * 0.2f - num3;
			float num5 = num + shadeKx * num4;
			float num6 = num2 + shadeKy * num4;
			if (num5 < shadeWeather.MinX || num5 > shadeWeather.MaxX || num6 < shadeWeather.MinY || num6 > shadeWeather.MaxY)
			{
				return 0f;
			}
			int which;
			float num7 = shadeWeather.Density(num5, num6, 0.2f, Time, out which);
			if (num7 <= 0.05f)
			{
				return 0f;
			}
			float num8 = Math.Min(1f, num7 * 1.4f) * 0.85f;
			if (!_landField)
			{
				return num8;
			}
			return num8 * Lift(LandVis(num, num2, num3));
		}

		private void CloudShade(int idx, float k)
		{
			Tint3 shadeTint = _shadeTint;
			if (!(k <= 0f))
			{
				int r = 256 + (int)((float)(shadeTint.R - 256) * k);
				int g = 256 + (int)((float)(shadeTint.G - 256) * k);
				int b = 256 + (int)((float)(shadeTint.B - 256) * k);
				Pixels[idx] = TerrainCache.Tint(Pixels[idx], r, g, b);
			}
		}

		private void CloudShadeRow(int by)
		{
			int num = by * 3;
			int num2 = Math.Min(Height, num + 3);
			for (int i = 0; i < Width; i += 3)
			{
				int num3 = Math.Min(Width, i + 3);
				float num4 = float.MaxValue;
				float num5 = 0f;
				float num6 = 0f;
				int num7 = 0;
				for (int j = num; j < num2; j++)
				{
					for (int k = i; k < num3; k++)
					{
						float num8 = Depth[(Height - 1 - j) * Width + k];
						if (num8 < num4)
						{
							num4 = num8;
						}
						if (num8 > num5)
						{
							num5 = num8;
						}
						num6 += num8;
						num7++;
					}
				}
				if (num4 > 240f)
				{
					continue;
				}
				if (num5 <= 240f && num5 - num4 < num4 * 0.06f + 0.15f)
				{
					float num9 = CloudShadeAt((float)(i + num3) * 0.5f, (float)(num + num2) * 0.5f, num6 / (float)num7);
					if (num9 <= 0f)
					{
						continue;
					}
					for (int l = num; l < num2; l++)
					{
						for (int m = i; m < num3; m++)
						{
							CloudShade((Height - 1 - l) * Width + m, num9);
						}
					}
					continue;
				}
				for (int n = num; n < num2; n++)
				{
					for (int num10 = i; num10 < num3; num10++)
					{
						int num11 = (Height - 1 - n) * Width + num10;
						CloudShade(num11, CloudShadeAt((float)num10 + 0.5f, (float)n + 0.5f, Depth[num11]));
					}
				}
			}
		}

		private void RenderPrecipitation(Weather w)
		{
			bool flag = w.Storms.Count > 0;
			if (flag || (w.Global != Precip.None && !(w.GlobalStrength <= 0.01f)))
			{
				float num = ((PrecipCell > 1f) ? PrecipCell : 1f);
				float num2 = (float.IsNaN(PrecipX) ? _view.X : PrecipX);
				float num3 = (float.IsNaN(PrecipX) ? _view.Y : PrecipY);
				int precipEx = (int)Math.Floor(num2 / num);
				int precipEy = (int)Math.Floor(num3 / num);
				float time = Time;
				_precipWeather = w;
				_precipStorms = flag;
				_precipCell = num;
				_precipTime = time;
				_precipEx = precipEx;
				_precipEy = precipEy;
				For(33, PrecipRow, UseParallel);
				_precipWeather = null;
			}
		}

		private void PrecipRow(int row)
		{
			Weather precipWeather = _precipWeather;
			bool precipStorms = _precipStorms;
			float precipCell = _precipCell;
			float precipTime = _precipTime;
			int precipEx = _precipEx;
			int precipEy = _precipEy;
			int num = precipEy - 16 + row;
			for (int i = precipEx - 16; i <= precipEx + 16; i++)
			{
				float num2 = ((float)i + 0.5f) * precipCell;
				float num3 = ((float)num + 0.5f) * precipCell;
				float num4 = num2 - _view.X;
				float num5 = num3 - _view.Y;
				if (num4 * _fx + num5 * _fy < -1f)
				{
					continue;
				}
				Precip drops = Precip.None;
				float num6 = 0f;
				if (precipStorms && num2 > precipWeather.MinX && num2 < precipWeather.MaxX && num3 > precipWeather.MinY && num3 < precipWeather.MaxY)
				{
					num6 = Math.Min(1f, precipWeather.Cover(num2, num3, precipTime, out drops) * 1.6f);
					if (drops == Precip.Seeds)
					{
						drops = Precip.None;
						num6 = 0f;
					}
				}
				if ((drops == Precip.None || num6 < 0.1f) && precipWeather.Global != Precip.None)
				{
					drops = precipWeather.Global;
					num6 = precipWeather.GlobalStrength;
				}
				if (drops == Precip.None || num6 < 0.05f)
				{
					continue;
				}
				int num7;
				switch (drops)
				{
				default:
					num7 = 2;
					break;
				case Precip.Sun:
					num7 = 1;
					break;
				case Precip.Ash:
					num7 = 3;
					break;
				case Precip.Snow:
					num7 = 6;
					break;
				case Precip.Rain:
				case Precip.Acid:
				case Precip.Rage:
					num7 = 6;
					break;
				}
				int num8 = num7;
				for (int j = 0; j < num8; j++)
				{
					uint num9 = TerrainModel.Hash(i * 7 + j * 1013, num * 13 - j * 71);
					if (!((float)(num9 & 0xFF) > num6 * 255f))
					{
						if (drops == Precip.Snow)
						{
							Flake(i, num, num9, precipTime, precipCell, low: false);
						}
						else
						{
							Particle(drops, i, num, num9, precipTime, precipCell);
						}
					}
				}
				if (drops == Precip.Snow && precipCell <= 1f && i - precipEx <= 4 && precipEx - i <= 4 && num - precipEy <= 4 && precipEy - num <= 4)
				{
					for (int k = 0; k < 3; k++)
					{
						uint num10 = TerrainModel.Hash(i * 11 + k * 977 + 31, num * 17 - k * 53 + 5);
						if (!((float)(num10 & 0xFF) > num6 * 255f))
						{
							Flake(i, num, num10, precipTime, precipCell, low: true);
						}
					}
				}
				if (!(precipCell <= 1f) || num8 != 6 || i - precipEx > 6 || precipEx - i > 6 || num - precipEy > 6 || precipEy - num > 6)
				{
					continue;
				}
				for (int l = 0; l < 3; l++)
				{
					uint num11 = TerrainModel.Hash(i * 11 + l * 977 + 31, num * 17 - l * 53 + 5);
					if (!((float)(num11 & 0xFF) > num6 * 255f))
					{
						Particle(drops, i, num, num11, precipTime, precipCell, low: true);
					}
				}
			}
		}

		private void Particle(Precip kind, int gx, int gy, uint h, float time, float cell, bool low = false)
		{
			float num = (float)((h >> 8) & 0xFF) / 255f;
			float num2 = (float)((h >> 16) & 0xFF) / 255f;
			float num3 = (float)((h >> 24) & 0xFF) / 255f;
			float num4;
			float num5;
			float num6;
			switch (kind)
			{
			case Precip.Rain:
			case Precip.Acid:
			case Precip.Rage:
				num4 = 15f;
				num5 = 7.5f;
				num6 = 0.55f;
				break;
			case Precip.Lava:
				num4 = 7f;
				num5 = 12f;
				num6 = 0.3f;
				break;
			case Precip.Fire:
				num4 = 4f;
				num5 = 10f;
				num6 = 0f;
				break;
			case Precip.Snow:
				num4 = 1.1f;
				num5 = 9f;
				num6 = 0f;
				break;
			case Precip.Ash:
				num4 = 0.8f;
				num5 = 9f;
				num6 = 0f;
				break;
			case Precip.Sun:
				num4 = 0.15f;
				num5 = 3f;
				num6 = 0f;
				break;
			default:
				num4 = 0.6f;
				num5 = 7f;
				num6 = 0f;
				break;
			}
			if (low)
			{
				num5 = 2f;
			}
			float num7 = num5 / num4;
			float num8 = time / num7 + num3;
			float num9 = num8 - (float)Math.Floor(num8);
			int num10 = (int)Math.Floor(num8);
			uint num11 = TerrainModel.Hash((int)h + num10 * 7919, num10);
			float num12 = ((float)gx + (num + (float)(num11 & 0xFF) / 255f) % 1f) * cell;
			float num13 = ((float)gy + (num2 + (float)((num11 >> 8) & 0xFF) / 255f) % 1f) * cell;
			float num14 = 1f;
			if (num6 > 0f && kind != Precip.Lava)
			{
				num14 = 0.7f + 0.6f * (float)((num11 >> 16) & 0xF) / 15f;
				num6 *= num14;
				if (num9 < 0.12f)
				{
					Landed(kind, gx, gy, h, num, num2, num10 - 1, num9 / 0.12f, cell);
				}
			}
			float num15 = ((kind == Precip.Snow || kind == Precip.Ash || kind == Precip.Magic || kind == Precip.Sun) ? 0.35f : 0f);
			if (num15 > 0f)
			{
				num12 += (float)Math.Sin(time * 0.9f + num3 * 6.3f) * num15;
				num13 += (float)Math.Cos(time * 0.7f + num * 6.3f) * num15;
			}
			float num16 = _terrain.SampleHeight(num12, num13);
			float num17 = ((kind == Precip.Sun) ? (num16 + 0.3f + num9 * num5) : (num16 + num5 * (1f - num9)));
			if (num17 < num16)
			{
				return;
			}
			float num18 = 0f;
			float num19 = 0f;
			if (num6 > 0f && kind != Precip.Lava && Weather != null)
			{
				num18 = Weather.SlantX;
				num19 = Weather.SlantY;
				num12 -= num18 * (num17 - num16);
				num13 -= num19 * (num17 - num16);
			}
			float num20 = num12 - _view.X;
			float num21 = num13 - _view.Y;
			float num22 = num20 * _fx + num21 * _fy;
			if (num22 < 0.25f || (Cut.On && num17 > Cut.Ceiling(num22)) || !ProjectRel(num20, num21, num17 - _view.Z, 0.25f, out var col, out var row, out var t, out var scale) || col < -2f || col > (float)Width + 2f)
			{
				return;
			}
			float num23 = 0.6f;
			bool flag = false;
			uint num24;
			switch (kind)
			{
			case Precip.Rain:
				num24 = RainColor;
				num23 = 0.52f;
				break;
			case Precip.Acid:
				num24 = AcidColor;
				num23 = 0.6f;
				flag = true;
				break;
			case Precip.Rage:
				num24 = RageDrop;
				num23 = 0.6f;
				flag = true;
				break;
			case Precip.Lava:
				num24 = LavaDrop;
				num23 = 0.95f;
				flag = true;
				break;
			case Precip.Fire:
				num24 = (((h & 1) == 0) ? LavaDrop : TerrainCache.Rgb(255, 214, 72));
				num23 = 0.95f;
				flag = true;
				break;
			case Precip.Snow:
				num24 = SnowColor;
				num23 = 0.9f;
				break;
			case Precip.Ash:
				num24 = AshColor;
				num23 = 0.85f;
				break;
			case Precip.Sun:
				num24 = SunMote;
				num23 = 0.55f * (float)Math.Sin((double)num9 * Math.PI);
				flag = true;
				break;
			default:
				num24 = (((h & 2) == 0) ? MagicA : MagicB);
				num23 = 0.6f + 0.4f * (float)Math.Sin(time * 9f + num3 * 20f);
				flag = true;
				break;
			}
			num23 *= num14;
			if (num23 <= 0.03f)
			{
				return;
			}
			if (!flag)
			{
				num24 = ((num6 > 0f) ? TerrainCache.Lerp(TerrainCache.Tint(num24, _flatTint.R, _flatTint.G, _flatTint.B), num24, 0.35f) : TerrainCache.Tint(num24, _flatTint.R, _flatTint.G, _flatTint.B));
			}
			float fog = Haze(num22, num17);
			int num25 = (int)col;
			if (num6 > 0f)
			{
				if ((num18 != 0f || num19 != 0f) && ProjectRel(num20 - num18 * num6, num21 - num19 * num6, num17 + num6 - _view.Z, 0.25f, out var col2, out var row2, out t, out var _))
				{
					float num26 = col - col2;
					float num27 = row - row2;
					int num28 = Math.Min(26, (int)Math.Ceiling(Math.Max(Math.Abs(num26), Math.Abs(num27))));
					for (int i = 0; i <= num28; i++)
					{
						float num29 = ((num28 == 0) ? 1f : ((float)i / (float)num28));
						Plot((int)(col2 + num26 * num29), (int)(row2 + num27 * num29), num22, num24, num23 * (0.5f + 0.5f * num29), fog);
					}
				}
				else
				{
					float val = (_tilted ? RowOfPoint(num22, num17 + num6 - _view.Z) : (_horizon - (num17 + num6 - _view.Z) * scale));
					int num30 = (int)Math.Floor(Math.Min(val, row));
					int num31 = (int)Math.Ceiling(Math.Max(val, row));
					if (num31 - num30 > 26)
					{
						num30 = num31 - 26;
					}
					for (int j = Math.Max(0, num30); j <= Math.Min(Height - 1, num31); j++)
					{
						Plot(num25, j, num22, num24, num23 * (0.5f + 0.5f * (float)(j - num30) / Math.Max(1f, num31 - num30)), fog);
					}
				}
				if (num9 > 0.97f && num22 < 12f && kind != Precip.Lava)
				{
					int r = (int)(_tilted ? RowOfPoint(num22, num16 + 0.03f - _view.Z) : (_horizon - (num16 + 0.03f - _view.Z) * scale));
					Plot(num25 - 1, r, num22, num24, num23, fog);
					Plot(num25 + 1, r, num22, num24, num23, fog);
				}
				if (kind == Precip.Lava && num9 > 0.95f)
				{
					Plot(num25, (int)(_tilted ? RowOfPoint(num22, num16 + 0.05f - _view.Z) : (_horizon - (num16 + 0.05f - _view.Z) * scale)), num22, TerrainCache.Rgb(255, 230, 120), 1f, fog);
				}
				return;
			}
			int num32 = ((!(num22 < 3f)) ? 1 : 2);
			int num33 = (int)row;
			for (int k = 0; k < num32; k++)
			{
				for (int l = 0; l < num32; l++)
				{
					Plot(num25 + l, num33 + k, num22, num24, num23, fog);
				}
			}
		}

		private void Landed(Precip kind, int gx, int gy, uint h, float jx, float jy, int life, float t, float cell)
		{
			uint num = TerrainModel.Hash((int)h + life * 7919, life);
			float num2 = ((float)gx + (jx + (float)(num & 0xFF) / 255f) % 1f) * cell;
			float num3 = ((float)gy + (jy + (float)((num >> 8) & 0xFF) / 255f) % 1f) * cell;
			float num4 = num2 - _view.X;
			float num5 = num3 - _view.Y;
			float num6 = num4 * _fx + num5 * _fy;
			if (num6 < 0.6f || num6 > 14f)
			{
				return;
			}
			int num7 = (int)num2;
			int num8 = (int)num3;
			if (num7 < 0 || num8 < 0 || num7 >= _terrain.Width || num8 >= _terrain.Height)
			{
				return;
			}
			bool num9 = (_terrain.Flags[num8 * _terrain.Width + num7] & 1) != 0;
			uint num10;
			switch (kind)
			{
			default:
				num10 = RainColor;
				break;
			case Precip.Rage:
				num10 = RageDrop;
				break;
			case Precip.Acid:
				num10 = AcidColor;
				break;
			}
			uint num11 = num10;
			if (kind == Precip.Rain)
			{
				num11 = TerrainCache.Tint(num11, _flatTint.R, _flatTint.G, _flatTint.B);
			}
			float fog = Haze(num6, _view.Z);
			float scale;
			float t2;
			if (num9)
			{
				float dz = 0.02f - _view.Z;
				float num12 = 0.05f + 0.3f * t;
				float alpha = 0.7f * (1f - t);
				for (int i = 0; i < 12; i++)
				{
					float num13 = (float)i * 0.5236f;
					if (ProjectRel(num4 + (float)Math.Cos(num13) * num12, num5 + (float)Math.Sin(num13) * num12, dz, 0.25f, out var col, out var row, out scale, out t2))
					{
						Plot((int)col, (int)row, num6, num11, alpha, fog);
					}
				}
			}
			else if (!(t > 0.6f) && !(num6 > 11f))
			{
				float num14 = _terrain.SampleHeight(num2, num3);
				if (ProjectRel(num4, num5, num14 + 0.03f - _view.Z, 0.25f, out var col2, out var row2, out t2, out scale))
				{
					int num15 = 1 + (int)(t * 4f);
					int num16 = 1 + (int)(t * 3f);
					float num17 = 0.85f * (1f - t);
					num11 = TerrainCache.Lerp(num11, uint.MaxValue, 0.3f);
					Plot((int)col2 - num15, (int)row2 - num16, num6, num11, num17, fog);
					Plot((int)col2 + num15, (int)row2 - num16, num6, num11, num17, fog);
					Plot((int)col2, (int)row2 - num16 - 1, num6, num11, num17 * 0.8f, fog);
				}
			}
		}

		private void Plot(int c, int r, float depth, uint color, float alpha, float fog)
		{
			if (c >= 0 && r >= 0 && c < Width && r < Height)
			{
				int num = (Height - 1 - r) * Width + c;
				if (!(Depth[num] <= depth))
				{
					uint num2 = ((fog > 0.01f) ? TerrainCache.Lerp(color, _scrFog[c], fog) : color);
					Pixels[num] = ((alpha >= 0.99f) ? num2 : TerrainCache.Lerp(Pixels[num], num2, alpha));
				}
			}
		}

		private void RenderBolts(Weather w)
		{
			if (!(w.Flash <= 0.01f))
			{
				int num = (int)(w.Flash * 70f);
				for (int i = 0; i < Pixels.Length; i++)
				{
					Pixels[i] = TerrainCache.Add(Pixels[i], num, num, num + num / 3);
				}
			}
		}

		private static WorkerPool ForPool()
		{
			WorkerPool forPool = _forPool;
			if (forPool != null)
			{
				if (!forPool.Started)
				{
					return null;
				}
				return forPool;
			}
			if (_forPoolTried)
			{
				return null;
			}
			lock (ForPoolGate)
			{
				if (!_forPoolTried)
				{
					try
					{
						_forPool = new WorkerPool(Math.Max(1, Environment.ProcessorCount - 1), "FirstPerson frame");
					}
					catch
					{
						_forPool = null;
					}
					_forPoolTried = true;
				}
			}
			return null;
		}

		private static void For(int n, Action<int> body, bool parallel = true)
		{
			if (n <= 0)
			{
				return;
			}
			if (!parallel || n == 1)
			{
				for (int i = 0; i < n; i++)
				{
					body(i);
				}
				return;
			}
			WorkerPool workerPool = (UseWorkerPool ? ForPool() : null);
			if (workerPool != null && !_forFloatsChecked)
			{
				lock (ForPoolGate)
				{
					if (!_forFloatsChecked && _forPool != null && !_forPool.FloatsAgree())
					{
						_forPool.Dispose();
						_forPool = null;
					}
					_forFloatsChecked = true;
				}
				workerPool = _forPool;
			}
			if (workerPool == null || !workerPool.TryBegin())
			{
				Parallel.For(0, n, body);
				return;
			}
			ForJob forJob = _forJob;
			try
			{
				int num = Math.Min(n, (workerPool.Workers + 1) * 4);
				int num2 = (n + num - 1) / num;
				forJob.Body = body;
				forJob.N = n;
				forJob.Per = num2;
				workerPool.Start(forJob, (n + num2 - 1) / num2);
				workerPool.Finish();
			}
			finally
			{
				forJob.Body = null;
				workerPool.End();
			}
		}

		private void ForStripes(int stripes, int stripeW, Action<int, int, int> body)
		{
			_stripeBody = body;
			_stripeW = stripeW;
			try
			{
				For(stripes, RunStripe);
			}
			finally
			{
				_stripeBody = null;
			}
		}

		private void RunStripe(int s)
		{
			_stripeBody(s, s * _stripeW, Math.Min(Width, (s + 1) * _stripeW));
		}
	}
}
