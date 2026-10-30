using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SceneCollector
	{
		private struct MeteorSlide
		{
			public Vector2 Target;

			public Vector2 Dir;

			public float Length;

			public float Until;
		}

		private struct ClashLight
		{
			public Vec3 At;

			public float Age;

			public float Strength;
		}

		private sealed class BurntLook
		{
			public string For;

			public string Family;

			public string Middle;

			public MeshModel EmbersNear;

			public MeshModel EmbersMiddle;

			public SpriteImage PaletteImage;

			public uint[] Palette;

			public int Frame = -100;

			public float SmoulderFrom = -1f;

			public MeshInstance Embers;
		}

		private sealed class Crumbling
		{
			public MeshInstance Mesh;

			public float X;

			public float Y;

			public float Ground;

			public float Heading;

			public float Across;

			public float Scale;

			public float Tall;

			public float Age;

			public uint Seed;
		}

		private sealed class CannonShot
		{
			public CannonballFx Ball;

			public Actor Ship;

			public BaseSimObject Target;

			public Vector2 Start;

			public Vector2 LastGame;

			public Vector2 Aim;

			public bool Steered;

			public bool AimWet;

			public Vec3 First;

			public Vec3 Offset;

			public Vec3 LastDrawn;

			public float Total;

			public float Size;

			public int Seen;

			public bool Started;

			public bool Ended;

			public bool IsBall;

			public uint Seed;
		}

		private sealed class GunFiring
		{
			public MuzzleBlastFx Fx;

			public Actor Ship;

			public Vec3 Port;

			public int Side;

			public Vector2 Toward;

			public float Lift;
		}

		private sealed class Recoil
		{
			public readonly float[] At = new float[3] { -99f, -99f, -99f };

			public readonly int[] Side = new int[3];

			public float Strength = 1f;

			public int Next;

			public float Last;
		}

		private struct KeptImage
		{
			public SpriteImage Image;

			public int Frame;
		}

		private struct OutlineLook
		{
			public FarLook Look;

			public float Grow;

			public float Lift;

			public float Width;

			public float Height;

			public uint Color;
		}

		private enum ModelRoute
		{
			Plain,
			Building,
			Special
		}

		private sealed class NearBuilding
		{
			public Building Building;

			public SpriteImage Image;

			public BuildingAsset ImageAsset;

			public float Width;

			public float Height;

			public BuildingLook Look;

			public BuildingAsset LookAsset;

			public WorldTile LookTile;

			public float Angle;

			public float DepthTiles = 1f;

			public float CenterOffset;

			public float Radius;

			public bool Solid;

			public string MeshFamily;

			public bool FamilyChecked;

			public BuildingAsset FamilyAsset;

			public int FamilyGen;

			public MeshInstance Mesh;

			public SpriteImage PaletteImage;

			public uint PaletteGround;

			public uint[] SeasonBase;

			public int SeasonStamp;

			public ModelRoute Route;

			public KeptTree Kept;

			public float DockHeading = float.NaN;

			public Vector2 LastPos;

			public int Wobbling;

			public float ModelScale;

			public BuildingAsset ScaleAsset;

			public bool ScaleRuin;

			public int ScaleSettling;

			public float StandingWidth;

			public uint KingdomColor;

			public bool WasStanding;

			public float RuinedAt = -1f;

			public Footprint YourShape;

			public MeshModel YourModel;

			public BurntLook Burnt;

			public BuildingMemo Memo;
		}

		private sealed class FireModel
		{
			public MeshModel Bare;

			public float[] Fires;
		}

		internal struct FireSpot
		{
			public Vector2 At;

			public float Ground;

			public float Radius;

			public float Bottom;

			public float Top;
		}

		internal struct TreeSeen
		{
			public string Id;

			public float Radius;

			public float Scale;

			public float Angle;

			public float Ground;

			public MeshModel Model;

			public Vector2 At;
		}

		private sealed class Puff
		{
			public Vec3 P;

			public Vec3 V;

			public float Age;

			public float Life;

			public float Size;

			public float Grow;

			public bool Ember;
		}

		private sealed class Falling
		{
			public SpriteImage Image;

			public float X;

			public float Y;

			public float Ground;

			public float Bottom;

			public float Width;

			public float Height;

			public float Angle;

			public float Age;

			public Vec3 Axis;

			public bool Landed;

			public MeshInstance Mesh;

			public float MeshScale;
		}

		private sealed class Chip
		{
			public Vec3 P;

			public Vec3 V;

			public float Age;

			public float Life;

			public float Size;

			public float Spin;

			public VoxelShape Shape;

			public bool Crumble;
		}

		private struct Firefly
		{
			public float X;

			public float Y;

			public float Z;

			public float Phase;

			public float Speed;
		}

		private sealed class FarModel
		{
			public NearBuilding Nb;

			public float X;

			public float Y;

			public bool Ok;

			public int Season = -1;

			public BuildingAsset Asset;

			public float Windows;

			public int Ring;
		}

		private struct FarKind
		{
			public bool Shows;

			public bool Tree;

			public bool Deciduous;

			public bool House;
		}

		private struct FarSprite
		{
			public float Width;

			public float Height;

			public uint Color;

			public FarLook Look;

			public bool Shows;
		}

		private sealed class GodArm
		{
			public GodHandFx Fx;

			public Vector2 LastPos;

			public int Seen;

			public bool Finger;

			public MeshModel Model;

			public Vec3 Tip;

			public Vec3 Up;

			public Vec3 Palm;

			public float Ground;
		}

		internal sealed class HallFlagState
		{
			public Building B;

			public MeshInstance Shaft;

			public MeshInstance Finial;

			public MeshInstance Cloth;

			public MeshInstance Banner;

			public MeshInstance RodL;

			public MeshInstance RodR;

			public MeshInstance Bracket;

			public MeshInstance Stand;

			public readonly MeshInstance[] Cloths = new MeshInstance[3];

			public FlagLook Shown;

			public FlagLook Next;

			public int SwapPhase;

			public byte Front;

			public Vec3 FootWorld;

			public MeshModel ClothModel;

			public MeshModel BannerModel;

			public int Frame = -100000;

			public float Dist;

			public float Height01 = 1f;

			internal FlagLook Base;

			internal bool Swapping;

			internal float SwapAt;

			internal int ClothLod = -1;

			internal int BannerLod = -1;

			internal float Seed;

			internal int Torn;

			internal readonly uint[][] ClothPal = new uint[3][];

			internal readonly MeshModel[] PaintedModel = new MeshModel[3];

			internal readonly FlagLook[] PaintedLook = new FlagLook[3];

			internal readonly int[] PaintedMode = new int[3];

			internal readonly uint[] PaintedRoof = new uint[3];

			internal readonly float[] ClothLength = new float[3];

			internal readonly MeshModel[] LengthOf = new MeshModel[3];

			internal uint[] ShaftPal;

			internal uint[] BannerPal;

			internal MeshModel ShaftPainted;

			internal MeshModel BannerPainted;

			internal int ShaftStyle = -9;

			internal FlagLook BannerLook;

			internal MeshModel BannerSized;

			internal float BannerW = 28f;

			internal float BannerH = 38f;

			internal MeshModel GhostOf;

			internal float Ghostly;

			internal MeshInstance[] Fixtures;
		}

		internal sealed class KeptTree
		{
			public int Until;

			public BuildingAsset Asset;

			public SpriteImage Image;

			public float X;

			public float Y;

			public float Ground;

			public string Family;
		}

		private sealed class MonolithPulse
		{
			public float X;

			public float Y;

			public float Seen;

			public uint Color;
		}

		private sealed class Heading
		{
			public readonly BodyMotion Body = new BodyMotion();

			public Vector2 Last;

			public Vector2 Drawn;

			public float Angle;

			public float TurnRate;

			public bool Looking;

			public float Speed;

			public float Size = 1f;

			public float Dt;

			public int SeenFrame = -1;

			public float Owed;

			public Func<float, float, bool> Blocked;

			public int Inside;

			public bool AtDoor;

			public float InsideFor;

			public bool Moving => Speed > 0.25f;
		}

		private sealed class CrabDrop
		{
			public MeshInstance[] Pool = new MeshInstance[3];

			public MeshInstance Crab = new MeshInstance();

			public float X;

			public float Y;

			public float Facing;

			public int Seen;

			public bool Shrapnel;
		}

		private sealed class Ball
		{
			public BowlingBallFx Fx;

			public float X;

			public float Y;

			public float Height;

			public int Bounces;

			public int Seen;

			public double Stamp;
		}

		private sealed class MeteorWay
		{
			public float Start;

			public Vec3 Dir;
		}

		private sealed class CropSeen
		{
			public Building Building;

			public WorldTile Tile;

			public float Growth;

			public bool Reaped;
		}

		private sealed class Stubble
		{
			public int X;

			public int Y;

			public float Age;

			public MeshInstance Mesh;
		}

		private sealed class Flight
		{
			public Vec3 Last;

			public Vec3 Dir;

			public bool HaveDir;

			public int Seen;

			public MeshInstance Mesh;

			public bool Arrow;

			public bool Struck;

			public bool Drawn;

			public Vector2 Start;

			public Vec3 Point;

			public Vector3 Game;

			public float Dist;
		}

		private sealed class Falling3D
		{
			public MeshInstance Mesh;

			public float Spin;

			public float Tumble;

			public int Seen;
		}

		private sealed class Debris
		{
			public MeshInstance Mesh;

			public Mat3 Basis;

			public Vec3 Pivot;

			public Vec3 At;

			public Vec3 Vel;

			public Vec3 Axis;

			public float Spin;

			public float Turned;

			public float Age;

			public float Life;

			public float Ground;

			public float Rest;

			public bool Still;

			public bool Rubble;
		}

		private sealed class RockLand
		{
			public object Top;

			public string Family;

			public string Look;

			public string Biome;
		}

		private sealed class BoulderLook
		{
			public uint[] From;

			public uint[] Colours;

			public object Top;
		}

		private sealed class PelletSeen
		{
			public Vec3 Last;

			public Vec3 Start;

			public int Seen;
		}

		private sealed class BuildingFx
		{
			public int[] Flames;

			public int[] Floats;

			public int[] Wheels;

			public int[] Spins;

			public float Ghostly;

			public float Hover;

			public Vec3[] Smoke;

			public Vec3[] LightAt;

			public uint[] LightColor;

			public float[] LightRadius;

			public uint Mist;

			public bool Animated;

			public bool Any;
		}

		internal struct UnitMemo
		{
			public Sprite Sprite;

			public SpriteImage Image;

			public int Epoch;

			public string Id;

			public bool Ufo;

			public int Kind;
		}

		internal struct BuildingMemo
		{
			public Sprite Sprite;

			public SpriteImage Image;

			public int Epoch;

			public InfoMemo Width;

			public InfoMemo Footing;

			public InfoMemo Height;

			public string RuinOf;

			public string Ruin;

			public string RuinBaseOf;

			public string RuinBase;

			public string SiteOf;

			public string Site;

			public string SiteBaseOf;

			public string SiteBase;
		}

		internal struct InfoMemo
		{
			public string Raw;

			public bool Ok;

			public float Value;
		}

		private sealed class TwisterState
		{
			public double Stamp;

			public Vector2 Foot;

			public Vector2 Top;

			public Vector2 Storm;

			public float Age;

			public float Scale;

			public float Form;

			public float DeathScale;

			public float Presence;

			public float Ground;

			public int Seen;

			public uint Seed;

			public uint Dust;

			public bool Dying;

			public bool Water;
		}

		private sealed class UfoState
		{
			public UfoFx Fx;

			public long Id;

			public readonly MeshInstance Mesh = new MeshInstance();

			public Vector2 Last;

			public bool HasLast;

			public Vec3 Vel;

			public float Ground;

			public float Diameter;

			public float LastHit = 99f;

			public int Seen = -1;

			public int FallFrames;

			public float FallFrom;

			public float FallTime;

			public float LastHeight = 99f;

			public float LeftAt;

			public bool Drawn;

			public bool Dropped;
		}

		private sealed class VentState
		{
			public float Clock;

			public float NextSpurt;

			public float NextBurst;

			public float Burst = -1f;

			public uint Seed;
		}

		internal struct WonderSpot
		{
			public string Family;

			public Vector3 At;
		}

		private sealed class WonderPulse
		{
			public float X;

			public float Y;

			public float At;

			public bool Heard;
		}

		public static int BiomeFxLevel = 2;

		public static float BiomeParticleAmount = 1f;

		public static float Chill;

		private readonly BiomeMap _biomeMap = new BiomeMap();

		public readonly BiomeMix BiomeMix = new BiomeMix();

		private readonly BiomeParticles _biomeParts = new BiomeParticles(Environment.TickCount ^ 0x3C3C);

		private readonly Dictionary<TileTypeBase, BiomeKind> _biomeOfType = new Dictionary<TileTypeBase, BiomeKind>();

		private Func<int, int, BiomeKind> _readBiome;

		private float _biomeTime;

		private static readonly List<NukeFx> Nukes = new List<NukeFx>();

		private static readonly List<ImpactFx> Impacts = new List<ImpactFx>();

		private static readonly FxParticles BlastParticles = new FxParticles();

		private readonly Dictionary<BaseEffect, MeteorFx> _meteors = new Dictionary<BaseEffect, MeteorFx>();

		private readonly Dictionary<BaseEffect, Vector2> _meteorTargets = new Dictionary<BaseEffect, Vector2>();

		private readonly HashSet<BaseEffect> _flashesSeen = new HashSet<BaseEffect>();

		private readonly HashSet<BaseEffect> _activeFlashes = new HashSet<BaseEffect>();

		private readonly HashSet<BaseEffect> _seenMeteors = new HashSet<BaseEffect>();

		private readonly List<BaseEffect> _goneMeteors = new List<BaseEffect>();

		private readonly FxSink _blastSink = new FxSink();

		private float _lastBlastTime = -1f;

		public uint ScreenFlash;

		public float BlastShake;

		internal static int MeteorFramesDrawn;

		internal static float MeteorHighest;

		private readonly Dictionary<BaseEffect, MeteorSlide> _meteorSlides = new Dictionary<BaseEffect, MeteorSlide>();

		private readonly List<BaseEffect> _oldSlides = new List<BaseEffect>();

		private readonly Dictionary<Drop, Vec3> _nukeDrops = new Dictionary<Drop, Vec3>();

		private float _dropTrail;

		private readonly List<ClashLight> _clashLights = new List<ClashLight>();

		private int _blocksSeen = -1;

		internal static int StopsDrawn;

		private const float BoardRange = 70f;

		private const float PosterRange = 26f;

		private readonly List<Law.Board> _boardsScratch = new List<Law.Board>();

		private static MeshModel _boardModel;

		private static MeshModel _notesModel;

		private readonly Dictionary<City, MeshInstance> _boards = new Dictionary<City, MeshInstance>();

		private readonly Dictionary<City, MeshInstance> _boardNotes = new Dictionary<City, MeshInstance>();

		private readonly List<Poster> _posterScratch = new List<Poster>();

		public const float BoardWidth = 1.5f;

		public const float BoardBottom = 0.62f;

		public const float BoardTop = 1.42f;

		internal static bool BuildClockOn;

		internal static double BcRescan;

		internal static double BcSlice;

		internal static double BcHouses;

		internal static double BcTrees;

		internal static double BcOther;

		internal static double BcTotal;

		internal static long BcHousesN;

		internal static long BcTreesN;

		internal static long BcOtherN;

		internal static long BcFrames;

		private static readonly double BcMs = 1000.0 / (double)Stopwatch.Frequency;

		private const int BCell = 16;

		private const float CellNear = 56f;

		private const float CellMargin = 16f;

		internal static bool BuildingCellsOff;

		internal static long CellsSkipped;

		private int[] _cellAt = new int[0];

		private int[] _cellEnd = new int[0];

		private float[] _cellMinX = new float[0];

		private float[] _cellMinY = new float[0];

		private float[] _cellMaxX = new float[0];

		private float[] _cellMaxY = new float[0];

		private int[] _cellCounts = new int[0];

		private int[] _cellOf = new int[0];

		private NearBuilding[] _cellScratch = new NearBuilding[0];

		private const float EmberShare = 0.55f;

		private const float EmberRange = 40f;

		private readonly List<Crumbling> _crumbles = new List<Crumbling>();

		private const float CrumbleSeconds = 0.9f;

		private const float AshSeconds = 2.2f;

		private static readonly Dictionary<TileTypeBase, string> BushLooks = new Dictionary<TileTypeBase, string>();

		private readonly Dictionary<Projectile, CannonShot> _cannonShots = new Dictionary<Projectile, CannonShot>();

		private readonly List<Projectile> _cannonGone = new List<Projectile>();

		private static readonly List<GunFiring> Firings = new List<GunFiring>();

		private static readonly List<CannonImpactFx> CannonImpacts = new List<CannonImpactFx>();

		private static readonly Dictionary<Actor, int> GunsFired = new Dictionary<Actor, int>();

		private static readonly Dictionary<Actor, Recoil> Recoils = new Dictionary<Actor, Recoil>();

		private static readonly List<Actor> RecoilsDone = new List<Actor>();

		internal static int CannonMuzzles;

		internal static int CannonBallFrames;

		internal static int CannonStrikes;

		internal static int CannonSpritesHidden;

		internal static string LastCannonStrike = "";

		internal static bool CannonTrace;

		internal static Vec3 LastMuzzle;

		internal static int LastMuzzleSide;

		internal static float LastMuzzleGap;

		internal static bool ChunkWalkOn;

		internal static bool ChunkWalkForTest;

		internal static bool ChunkWalkOff;

		internal static long ChunksSkipped;

		internal static long ChunkUnitsVisited;

		internal static bool ChunkCheck;

		private static readonly HashSet<Actor> _chunkCheckLogged = new HashSet<Actor>();

		private static readonly Dictionary<Actor, float> _chunkCheckMissedAt = new Dictionary<Actor, float>();

		private readonly List<Actor> _chunkUnits = new List<Actor>(4096);

		private List<ActorAsset> _boatKinds;

		internal static int ClodsThrown;

		private const float MaxCoastSpeed = 10f;

		private static bool _coastBadLogged;

		private static bool _drawnBadLogged;

		private const float CrabzillaRange = 220f;

		private int _crabFrame = -1;

		private List<Heading> _walkersNow = new List<Heading>(128);

		private List<Heading> _walkersLast = new List<Heading>(128);

		private const float DrawnTouch = 0.3f;

		private const float DrawnAhead = 0.6f;

		internal static bool CrowdFast = true;

		internal static bool CrowdVerify;

		internal static long CrowdVerified;

		internal static long CrowdMismatched;

		internal static string CrowdFirstMismatch;

		private double[] _sepX = new double[256];

		private double[] _sepY = new double[256];

		private double[] _sepS = new double[256];

		private bool[] _sepMoving = new bool[256];

		private double _sepMoved;

		private double _sepRound;

		private float[] _sepCheck = new float[1024];

		public readonly List<CrowdItem> Crowd = new List<CrowdItem>();

		internal float CrowdLine;

		internal float CrowdPixel;

		internal bool CrowdGear;

		internal float CrowdReach;

		private readonly Dictionary<Actor, KeptImage> _cardImages = new Dictionary<Actor, KeptImage>();

		private const int CardImageFrames = 20;

		private readonly List<FarProp> _farOutlines = new List<FarProp>();

		private readonly Dictionary<ActorAsset, OutlineLook> _outlineLooks = new Dictionary<ActorAsset, OutlineLook>();

		private const float OutlineLeastPixels = 7f;

		internal static long FarOutlinesDrawn;

		internal static bool FarOutlinesOffForTest;

		internal static bool FarOutlines;

		internal static bool SkipUnitsForTest;

		internal static bool SkipBuildingsForTest;

		private const float ColliderRange = 5f;

		private const float HitFlashSeconds = 0.3f;

		private const float EffectRange = 40f;

		private const float CreatureVoxelRange = 26f;

		private const float CreatureModelRange = 70f;

		public float CreatureRange;

		private const float TreeVoxelRange = 18f;

		public const float TreeScale = 2.25f;

		public const float TreeWidthScale = 1.8f;

		private const float PlantScale = 1.25f;

		private const float BuildingVoxelRange = 30f;

		public float Detail = 1f;

		public bool Tufts = true;

		internal Vector2? SteerAt;

		private float _steerX;

		private float _steerY;

		private const float SwimSink = 0.4f;

		public readonly List<Billboard> Billboards = new List<Billboard>(1024);

		public readonly List<VoxelObject> Voxels = new List<VoxelObject>(512);

		public readonly List<MeshInstance> Meshes = new List<MeshInstance>(512);

		public readonly List<BaseSimObject> Tagged = new List<BaseSimObject>(1024);

		public readonly Dictionary<Actor, float> RecentHits = new Dictionary<Actor, float>();

		private readonly List<NearBuilding> _buildings = new List<NearBuilding>(2048);

		private readonly Dictionary<Actor, Heading> _headings = new Dictionary<Actor, Heading>();

		private readonly List<Actor> _staleHeadings = new List<Actor>();

		private readonly HashSet<BaseSimObject> _broken = new HashSet<BaseSimObject>();

		private readonly HashSet<string> _reportedAssets = new HashSet<string>();

		private Vector2 _lastScanAt = new Vector2(float.MinValue, float.MinValue);

		private int _framesSinceScan;

		private int _spriteCursor;

		private int _frame;

		public bool ShowHost;

		public Actor Skip;

		public Building SkipBuilding;

		public const int HostTag = 2147483646;

		public readonly Dictionary<Actor, Vector2> LookAt = new Dictionary<Actor, Vector2>();

		private float _eyeX;

		private float _eyeY;

		private float _eyeZ;

		private float _vfx;

		private float _vfy;

		private float _cullTan;

		private float _cullSpan;

		private float _cullWide;

		private float _cullBack;

		public static float AllAround;

		private bool _wantCollider;

		private bool _colliderAdded;

		private bool _wantSolid;

		private Collider _placed;

		private Collider _yours;

		private const float ShadowKeep = 41f;

		private const float PostsLo = 0.45f;

		private const float PostsHi = 0.85f;

		private static readonly Dictionary<MeshModel, FireModel> _fireModels = new Dictionary<MeshModel, FireModel>();

		private readonly List<FireSpot> _bonfiresNext = new List<FireSpot>();

		internal FireSpot[] Bonfires = new FireSpot[0];

		internal static List<TreeSeen> TreesForTest;

		internal static bool AllLit;

		private const float PlantModelRange = 55f;

		private readonly List<Puff> _puffs = new List<Puff>();

		private readonly Dictionary<int, float> _craterTimers = new Dictionary<int, float>();

		private const float VolcanoFxRange = 140f;

		private static readonly uint EmberHot = TerrainCache.Rgb(255, 196, 64);

		private static readonly uint EmberCool = TerrainCache.Rgb(230, 80, 24);

		private const float ShakeSeconds = 0.3f;

		private const float FallSeconds = 1f;

		private const float FallenSeconds = 2.6f;

		private readonly List<Falling> _falling = new List<Falling>();

		private readonly List<Chip> _chips = new List<Chip>();

		private readonly Dictionary<uint, VoxelShape> _chipShapes = new Dictionary<uint, VoxelShape>();

		private readonly System.Random _chipRandom = new System.Random();

		private Building _shaking;

		private float _shakeAge = 99f;

		private TerrainCache _terrain;

		private static readonly HashSet<string> MapIcons = new HashSet<string>
		{
			"fx_change_happiness_positive", "fx_change_happiness_negative", "fx_hmm", "fx_money_got_loot", "fx_money_got_money", "fx_money_paid_tax", "fx_money_paid_tribute", "fx_money_spend_money", "fx_conversion_religion", "fx_conversion_culture",
			"fx_conversion_language", "fx_experience_gain", "fx_plot_progress", "fx_infinity_coin", "fx_new_border", "fx_zone_highlight", "fx_bad_place", "fx_debug_tile", "fx_move"
		};

		private static readonly Dictionary<string, float> SmallEffects = new Dictionary<string, float>
		{
			{ "fx_slash", 0.6f },
			{ "fx_hit", 0.45f },
			{ "fx_hit_critical", 0.6f },
			{ "fx_miss", 0.45f },
			{ "fx_jump", 0.6f },
			{ "fx_walk", 0.4f },
			{ "fx_dodge", 0.6f },
			{ "fx_block", 0.5f },
			{ "fx_shield_hit", 0.8f },
			{ "fx_spark", 0.35f },
			{ "fx_status_particle", 0.35f },
			{ "fx_weapon_particle", 0.35f },
			{ "fx_drowning", 0.5f },
			{ "fx_water_splash", 0.8f },
			{ "fx_hearts", 0.4f },
			{ "fx_fishnet", 1f },
			{ "fx_spawn", 1.2f },
			{ "fx_create_skeleton", 1.2f },
			{ "fx_teleport_blue", 1.4f },
			{ "fx_teleport_red", 1.4f },
			{ "fx_cast_ground_blue", 1.6f },
			{ "fx_cast_top_blue", 1.4f },
			{ "fx_cast_ground_red", 1.6f },
			{ "fx_cast_top_red", 1.4f },
			{ "fx_cast_ground_green", 1.6f },
			{ "fx_cast_ground_purple", 1.6f },
			{ "fx_cast_top_green", 1.4f },
			{ "fx_explosion_tiny", 1.2f },
			{ "fx_explosion_small", 2.5f },
			{ "fx_explosion_middle", 4f },
			{ "fx_explosion_crab_bomb", 3f },
			{ "fx_explosion_ufo", 4f },
			{ "fx_explosion_wave", 6f },
			{ "fx_explosion_huge", 7f },
			{ "fx_explosion_meteorite", 8f },
			{ "fx_explosion_nuke_atomic", 24f }
		};

		internal static List<string> EffectTrace;

		public readonly List<PointLight> Lights = new List<PointLight>(96);

		private float _night;

		private float _clock;

		private float _frameDt;

		private const float LightRange = 48f;

		private const int MaxLights = 40;

		private static readonly Color32 FireLight = new Color32(byte.MaxValue, 150, 60, byte.MaxValue);

		private readonly Dictionary<SpriteImage, uint> _glowColors = new Dictionary<SpriteImage, uint>();

		private const float BlazeRange = 48f;

		private readonly Firefly[] _flies = new Firefly[28];

		private bool _fliesPlaced;

		private readonly Dictionary<Building, NearBuilding> _previousNear = new Dictionary<Building, NearBuilding>();

		private int _dragonFrame = -1;

		public float FarActorRange;

		public int UnitTagEnd;

		public readonly List<FarActor> FarActors = new List<FarActor>(256);

		private readonly Dictionary<SpriteImage, uint> _averageColor = new Dictionary<SpriteImage, uint>();

		public bool FarModels;

		public float FarModelReach;

		public int FarModelsDrawn;

		private readonly Dictionary<Building, FarModel> _farModels = new Dictionary<Building, FarModel>();

		private List<FarModel> _farRingModel = new List<FarModel>(8192);

		private List<FarModel> _farNextModel = new List<FarModel>(8192);

		private bool _farRingModels;

		private bool _farNextModels;

		private int _farRings;

		private int _farLate;

		private int _farRemake;

		private bool _farBuild;

		private float _farWindows;

		private bool _farMade;

		private readonly List<Building> _farGone = new List<Building>();

		private const double FarMakeAtOnceMs = 4.0;

		private const int FarRemakes = 8;

		public float FarPropRange;

		public readonly List<FarProp> FarProps = new List<FarProp>(4096);

		private readonly Dictionary<BuildingAsset, FarKind> _farKinds = new Dictionary<BuildingAsset, FarKind>();

		private readonly Dictionary<(BuildingAsset, SpriteImage), FarSprite> _farSprites = new Dictionary<(BuildingAsset, SpriteImage), FarSprite>();

		private static readonly Dictionary<SpriteImage, FarLook> FarLooks = new Dictionary<SpriteImage, FarLook>();

		private readonly Dictionary<Building, SpriteImage> _farImage = new Dictionary<Building, SpriteImage>();

		private List<FarProp> _farRing = new List<FarProp>(8192);

		private List<Building> _farRingOf = new List<Building>(8192);

		private List<FarProp> _farNext = new List<FarProp>(8192);

		private List<Building> _farNextOf = new List<Building>(8192);

		private int _farNextAt = -1;

		private Vector2 _farNextEye;

		private float _farNextNear;

		private float _farNextFar;

		private const double FarScanBudgetMs = 1.5;

		private readonly Dictionary<(FarLook, bool, bool), FarLook> _farSeasoned = new Dictionary<(FarLook, bool, bool), FarLook>();

		private int _farSeasonStamp;

		private Vector2 _farScanAt = new Vector2(-1000000f, -1000000f);

		private float _farScanNear = -1f;

		private float _farScanFar = -1f;

		private int _farScanFrame = -1000;

		private int _farCursor;

		private bool _farLooksOff;

		public float FarScanMs;

		public float FarScanWorstMs;

		public int FarScanCount;

		private float _farScanSum;

		private float _farScanWorst;

		private readonly Stopwatch _farClock = new Stopwatch();

		private const float GrowFullTo = 6.5f;

		private const float GiantTall = 15.5f;

		private const float MapSizeFrom = 20f;

		public int PictureWidth = 1920;

		internal static bool GlideOff;

		private const float GlideFrom = 60f;

		private float _glideFocal;

		internal ViewState GodFrameCamera;

		internal int GodFrameWidth;

		internal int GodFrameHeight;

		internal static bool GodFrameUnits = true;

		internal static bool GodFrameBuildings = true;

		internal static long GodFrameSkipped;

		internal const float GodFrameMargin = 16f;

		private int _gfFrame = -1;

		private bool _gfOn;

		private float _gfFocal;

		private float _gfHorizon;

		private float _gfHalfW;

		private float _gfFx;

		private float _gfFy;

		private const int GodCell = 16;

		private byte[] _gcOut = new byte[0];

		private int _gcW;

		private int _gcH;

		private int _gcFrame = -1;

		private bool _gcOn;

		internal static bool GodCellsOff;

		private const float BuildingCellTall = 30f;

		private const float BuildingCellWide = 12f;

		private byte[] _gcbOut = new byte[0];

		private int _gcbFrame = -1;

		private bool _gcbOn;

		private readonly Dictionary<object, GodArm> _godArms = new Dictionary<object, GodArm>();

		private readonly List<object> _godStale = new List<object>();

		internal int GodFingersDrawn;

		internal int GodGrabsDrawn;

		internal float GodGrabCurl;

		internal static bool HallFlagsOn = true;

		internal static int HallFlagsDrawn;

		internal static double HallFlagsMs;

		private readonly Dictionary<long, HallFlagState> _hallFlags = new Dictionary<long, HallFlagState>();

		private readonly List<long> _hallFlagsGone = new List<long>();

		private object _hfWorld;

		private int _hfFrame = -1;

		private int _hfCity;

		private int _hfDrawnFrame;

		private float _hfClock;

		private float _hfWindX = 1f;

		private float _hfWindY;

		private float _hfStrength = 0.5f;

		private readonly Stopwatch _hfWatch = new Stopwatch();

		private const int KeepFrames = 8;

		private const float KeepFromEye = 30f;

		internal static bool KeptTreesOff;

		internal static long TreesKept;

		private readonly List<MonolithPulse> _monolithPulses = new List<MonolithPulse>();

		internal static int MonolithFlashSeen;

		internal static int MonolithSpritesTaken;

		private const float MonolithFade = 0.6f;

		private const float InsideSeconds = 5f;

		private const int Gone = 1000;

		private static readonly Func<float, float, bool> SteeredRound = (float x, float y) => Steering.Solid(new Vector2(x, y));

		private static readonly Func<float, float, bool> OffGround = (float x, float y) => !OnGround(x, y);

		private static readonly Func<float, float, bool> OffGroundSteered = (float x, float y) => !OnGround(x, y) || Steering.Solid(new Vector2(x, y));

		internal static bool GroundMemo = true;

		internal static bool GroundVerify;

		internal static long GroundVerified;

		internal static long GroundMismatched;

		internal static string GroundFirstMismatch;

		private const int GroundSlots = 512;

		private static readonly int[] _groundTile = new int[512];

		private static readonly int[] _groundAsked = new int[512];

		private static readonly bool[] _groundOn = new bool[512];

		private static int _groundRound = 1;

		private static int _groundW;

		private static int _groundH;

		private double _worldTime = -1.0;

		private float _worldStep;

		private int _worldStepFrame = -1;

		internal static Action<Actor, Vector2, float, bool, float> Watch;

		private readonly List<KeyValuePair<int, Vector2>> _netGods = new List<KeyValuePair<int, Vector2>>();

		internal static bool OffscreenCheap;

		internal static long OffscreenSkipped;

		private const float OffscreenNear = 40f;

		public int OwnBody;

		public bool OwnWhole;

		private const float LegsFrom = 42f;

		private const float LegsWhole = 56f;

		private const float LegsAhead = 0.05f;

		private readonly List<MeshInstance> _ownLegs = new List<MeshInstance>();

		private readonly List<MeshInstance> _ownShadowed = new List<MeshInstance>();

		private readonly Dictionary<MeshModel, ulong> _legsHide = new Dictionary<MeshModel, ulong>();

		private float _ownHeading = float.NaN;

		private float _ownTurn;

		internal int OwnShadowsForTest;

		internal int OwnLegsForTest;

		private readonly Dictionary<MeshModel, ulong> _thighsHide = new Dictionary<MeshModel, ulong>();

		private HeatRayFx _heatRay;

		internal static HeatRayFx HeatRayForTest;

		private readonly Dictionary<Drop, CrabDrop> _crabDrops = new Dictionary<Drop, CrabDrop>();

		private readonly List<Drop> _crabGone = new List<Drop>();

		private static readonly List<CrabLandFx> CrabLandings = new List<CrabLandFx>();

		private static readonly List<CrabBlastFx> CrabBlasts = new List<CrabBlastFx>();

		private readonly Dictionary<BaseEffect, double> _powerFxSeen = new Dictionary<BaseEffect, double>();

		private readonly Dictionary<Boulder, Ball> _balls = new Dictionary<Boulder, Ball>();

		private readonly List<Boulder> _ballsGone = new List<Boulder>();

		private static readonly List<BowlImpactFx> BowlImpacts = new List<BowlImpactFx>();

		internal static Vector2 BallAtForTest;

		private const float MeteorFall = 3.4f;

		private const float MeteorHeight = 150f;

		private const float MeteorAway = 30f;

		private readonly Dictionary<BaseEffect, MeteorWay> _meteorWays = new Dictionary<BaseEffect, MeteorWay>();

		private static readonly float[] CropNearTo = new float[5] { 5f, 5f, 5f, 5f, 5f };

		private static readonly float[] CropMiddleTo = new float[5] { 11f, 13f, 14f, 14f, 11f };

		private static readonly float[] CropDrawTo = new float[5] { 11f, 24f, 42f, 42f, 11f };

		private const float CropRange = 44f;

		private const float CropSwayTo = 14f;

		private const float StubbleSeconds = 75f;

		private readonly Dictionary<int, CropSeen> _crops = new Dictionary<int, CropSeen>();

		private readonly List<Stubble> _stubble = new List<Stubble>();

		private readonly Dictionary<int, uint[]> _cropPalettes = new Dictionary<int, uint[]>();

		private readonly List<int> _cropScratch = new List<int>();

		private float _cropCheck;

		private static readonly uint CropGreen = TerrainCache.Rgb(92, 142, 52);

		private static readonly uint CropGreenDark = TerrainCache.Rgb(58, 100, 40);

		private static readonly uint CropStem = TerrainCache.Rgb(118, 152, 64);

		private static readonly uint EarGreen = TerrainCache.Rgb(150, 172, 78);

		private static readonly uint EarGold = TerrainCache.Rgb(222, 184, 92);

		private static readonly uint EarPale = TerrainCache.Rgb(246, 216, 128);

		private static readonly uint StemGold = TerrainCache.Rgb(196, 162, 86);

		private static readonly uint Straw = TerrainCache.Rgb(198, 170, 106);

		private readonly Dictionary<Projectile, Flight> _flights = new Dictionary<Projectile, Flight>();

		private readonly List<Projectile> _flightsStale = new List<Projectile>();

		private const float ArrowStrikeRange = 48f;

		private readonly Dictionary<Drop, Falling3D> _bombs = new Dictionary<Drop, Falling3D>();

		private readonly List<Drop> _bombsStale = new List<Drop>();

		private readonly List<Debris> _debris = new List<Debris>();

		private readonly Dictionary<Building, int> _rockStage = new Dictionary<Building, int>();

		private const float RubbleSeconds = 7f;

		private const int DebrisKept = 24;

		private const float RockFloorRange = 6f;

		private const float RockFooting = 0.14f;

		private const float JaggedFooting = 0.24f;

		private readonly Dictionary<NearBuilding, RockLand> _rockLands = new Dictionary<NearBuilding, RockLand>();

		private readonly Dictionary<NearBuilding, BoulderLook> _boulderLooks = new Dictionary<NearBuilding, BoulderLook>();

		private const float SmoulderSeconds = 80f;

		private static readonly uint[] OldRoofs = new uint[4]
		{
			TerrainCache.Rgb(150, 70, 56),
			TerrainCache.Rgb(96, 100, 112),
			TerrainCache.Rgb(120, 92, 66),
			TerrainCache.Rgb(128, 60, 58)
		};

		private static readonly uint Weathered = TerrainCache.Rgb(216, 210, 202);

		private static readonly MuzzleFlashes ShotgunFlashes = new MuzzleFlashes();

		private static readonly ShellCasings Casings = new ShellCasings();

		private readonly Dictionary<Projectile, PelletSeen> _pellets = new Dictionary<Projectile, PelletSeen>();

		private readonly List<Projectile> _pelletsGone = new List<Projectile>();

		private readonly Dictionary<BaseSimObject, int> _volleyFrame = new Dictionary<BaseSimObject, int>();

		private static uint _shotgunSeed = 1u;

		internal static bool Trace;

		private static int _traced;

		private int _pelletsDrawn;

		internal static int ShellsLanded;

		internal static Vec3 ShellLandedAt;

		internal static int OthersVolleys;

		internal static int OthersPellets;

		internal static int OthersPelletsTotal;

		internal static int ShellsEjected;

		internal static SceneCollector SimView;

		internal float SimFocusX;

		internal float SimFocusY;

		private float _simFocal;

		private TerrainCache _simTerrain;

		internal float SkyReach;

		internal float SkyPixel;

		internal Vector2 SkyFocus;

		private static readonly SpriteImage NoSprite = new SpriteImage
		{
			Width = 1,
			Height = 1,
			Pixels = new uint[1]
		};

		private SnowCover _snowCover;

		private static readonly Dictionary<MeshModel, BuildingFx> FxCache = new Dictionary<MeshModel, BuildingFx>();

		internal static bool SpriteMemo = false;

		internal static bool UnitChecks = true;

		internal static bool BuildingInfo = true;

		private const int KindRead = 1;

		private const int KindCrab = 2;

		private const int KindDragon = 4;

		private const int KindUfo = 8;

		private const int KindFinger = 16;

		private UnitMemo[] _unitMemo = new UnitMemo[1024];

		private static SpriteCache _watched;

		private static Dictionary<Sprite, SpriteImage> _watchedImages;

		private static Func<Dictionary<Sprite, SpriteImage>, int> _versionOf;

		private static int _baseCount;

		private static int _baseVersion;

		private static int _spriteEpoch = 1;

		private static bool _watchFailed;

		private static readonly string[][] CropNames = new string[4][]
		{
			new string[3] { "crop_wheat_0", "crop_wheat_0_m", "crop_wheat_0_f" },
			new string[3] { "crop_wheat_1", "crop_wheat_1_m", "crop_wheat_1_f" },
			new string[3] { "crop_wheat_2", "crop_wheat_2_m", "crop_wheat_2_f" },
			new string[3] { "crop_wheat_3", "crop_wheat_3_m", "crop_wheat_3_f" }
		};

		internal static bool VerifySprites;

		internal static bool VerifyKinds;

		internal static bool VerifyInfo;

		internal static int SpriteCompared;

		internal static int SpriteDiffered;

		internal static int KindCompared;

		internal static int KindDiffered;

		internal static int InfoCompared;

		internal static int InfoDiffered;

		internal static string SpriteFirst;

		internal static string KindFirst;

		internal static string InfoFirst;

		private readonly Dictionary<TornadoEffect, TwisterState> _twisters = new Dictionary<TornadoEffect, TwisterState>();

		private readonly List<TwisterState> _twistersGone = new List<TwisterState>();

		private readonly List<TornadoEffect> _twistersStale = new List<TornadoEffect>();

		private Atmosphere _air;

		private float _twisterTime;

		private float _flashIn = 4f;

		private float _flashGap;

		private float _flashDist;

		private int _flashPulses;

		private Vector2 _flashAt;

		private bool _baseLifted;

		private const float CalmBase = 15f;

		private const float CalmThickness = 7f;

		private const float TwisterReach = 200f;

		internal int TwisterStormsForTest;

		internal int TwisterPiecesForTest;

		internal float TwisterShakeForTest;

		private readonly Dictionary<Actor, UfoState> _ufos = new Dictionary<Actor, UfoState>();

		private readonly List<Actor> _ufosGone = new List<Actor>();

		private static readonly List<UfoCrashFx> UfoCrashes = new List<UfoCrashFx>();

		private const float UfoFallSeconds = 1.6f;

		private const float UfoDetailScale = 4f;

		internal static readonly List<Vector3> UfoSpots = new List<Vector3>();

		public readonly List<Streak> Streaks = new List<Streak>(256);

		internal static UfoFx UfoForTest;

		internal static int UfoFramesDrawn;

		internal static int UfoBeamFrames;

		internal static int UfoFallingFrames;

		private readonly Dictionary<Building, VentState> _vents = new Dictionary<Building, VentState>();

		private int _ventsSweep;

		private static readonly Vec3[] LavaFeet = new Vec3[3]
		{
			new Vec3(-0.63f, -4.47f, 0f),
			new Vec3(2.22f, -3.85f, 0f),
			new Vec3(-0.77f, 4.39f, 0f)
		};

		internal const float WallModelRange = 46f;

		internal static bool WallModelsOffForTest;

		internal float WallReach;

		internal float WallLens = 1f;

		private static readonly Func<string, MeshModel> FindModel = Models.Get;

		private readonly Dictionary<int, MeshInstance> _wallMeshes = new Dictionary<int, MeshInstance>();

		private readonly Dictionary<int, int> _wallSeen = new Dictionary<int, int>();

		private readonly List<int> _staleWalls = new List<int>();

		public readonly Weather Weather = new Weather();

		private readonly Dictionary<BaseEffect, double> _boltsSeen = new Dictionary<BaseEffect, double>();

		private readonly List<BaseEffect> _boltsStale = new List<BaseEffect>();

		private Func<float, float, float> _groundAt;

		private TerrainCache _groundOf;

		private static readonly HashSet<string> WeatherDrops = new HashSet<string> { "rain", "snow", "acid", "ash", "magic_rain", "rage" };

		public readonly Gusts Gusts = new Gusts(Environment.TickCount);

		private readonly WindFx _wind = new WindFx();

		private float _windTime;

		public static bool WindGusts = true;

		private const float WindTreeRange = 14f;

		private readonly AmbientFx _ambient = new AmbientFx(Environment.TickCount ^ 0x5A5A);

		private int _habitatFrame = -1000;

		private bool _nearSea;

		private bool _onSnow;

		private float _green;

		private static readonly Dictionary<MeshModel, bool> _glows = new Dictionary<MeshModel, bool>();

		private const int MostBent = 120;

		private int _bent;

		private int _bentFrame;

		internal static readonly List<WonderSpot> WonderSpots = new List<WonderSpot>();

		internal static readonly List<WonderSpot> WonderPulses = new List<WonderSpot>();

		internal static int WonderPulsesSeen;

		internal static int WondersPosed;

		private readonly List<WonderPulse> _wonderPulses = new List<WonderPulse>();

		private readonly Dictionary<Building, WonderLook.State> _wonderStates = new Dictionary<Building, WonderLook.State>();

		private readonly List<PointLight> _wonderLights = new List<PointLight>(8);

		internal static int WonderSpotFrame = -10;

		private readonly TownScene _townScene = new TownScene();

		private readonly TownBuilding _townBuilding = new TownBuilding();

		internal BiomeParticles BiomeForTest => _biomeParts;

		internal BiomeMap BiomeMapForTest => _biomeMap;

		public static float BiomeLevel
		{
			get
			{
				if (BiomeFxLevel > 0)
				{
					if (BiomeFxLevel != 1)
					{
						return 1f;
					}
					return 0.5f;
				}
				return 0f;
			}
		}

		internal static FxParticles FxPool => BlastParticles;

		internal static int NukeCount => Nukes.Count;

		internal static int ImpactCount => Impacts.Count;

		internal static int BlastParticleCount => BlastParticles.List.Count;

		internal static IEnumerable<NukeFx> ActiveNukes => Nukes;

		internal int CrumblesForTest => _crumbles.Count;

		internal static int CannonFiringCount => Firings.Count;

		internal static int CannonImpactCount => CannonImpacts.Count;

		internal int PuffCount => _puffs.Count;

		internal int FarModelsKept => _farModels.Count;

		internal bool FarRingSettledForTest
		{
			get
			{
				if (_farNextAt < 0 && _farRingModels == FarModels && _farLate == 0)
				{
					return _farScanNear >= 0f;
				}
				return false;
			}
		}

		internal bool FarLooksOffForTest
		{
			get
			{
				return _farLooksOff;
			}
			set
			{
				_farLooksOff = value;
				_farScanNear = -1f;
				_farNextAt = -1;
			}
		}

		internal float HallFlagClockForTest => _hfClock;

		internal Vector3 HallFlagWindForTest => new Vector3(_hfWindX, _hfWindY, _hfStrength);

		internal static int CrabBlastCount => CrabBlasts.Count;

		internal static int CrabLandingCount => CrabLandings.Count;

		internal int CrabDropCount => _crabDrops.Count;

		internal int BallCount => _balls.Count;

		internal static int BowlImpactCount => BowlImpacts.Count;

		internal static int BowlFinalCount
		{
			get
			{
				int num = 0;
				foreach (BowlImpactFx bowlImpact in BowlImpacts)
				{
					if (bowlImpact.Final)
					{
						num++;
					}
				}
				return num;
			}
		}

		internal int CropCount => _crops.Count;

		internal int StubbleCount => _stubble.Count;

		internal static int CasingCount => Casings.Count;

		private bool FromSky => SkyReach > 0f;

		internal int TwistersForTest => _twisters.Count;

		internal int UfoCount => _ufos.Count;

		internal static int UfoCrashCount => UfoCrashes.Count;

		internal static int UfoWrecks
		{
			get
			{
				int num = 0;
				foreach (UfoCrashFx ufoCrash in UfoCrashes)
				{
					if (ufoCrash.HasWreck)
					{
						num++;
					}
				}
				return num;
			}
		}

		internal static bool WallsAsModels
		{
			get
			{
				if (!WallModelsOffForTest && Models.Ready)
				{
					return Models.Get("wall_order_straight") != null;
				}
				return false;
			}
		}

		internal float WallRange => Mathf.Max(46f, WallReach);

		internal WindFx WindForTest => _wind;

		internal AmbientFx AmbientForTest => _ambient;

		private void StepBiomes(ViewState view, TerrainCache t, float dt)
		{
			float biomeLevel = BiomeLevel;
			if (FromSky || biomeLevel <= 0f || t == null || t.Width == 0 || World.world == null)
			{
				if (_biomeParts.Pools > 0)
				{
					_biomeParts.Clear();
				}
				BiomeMix.Reset();
				return;
			}
			try
			{
				_biomeMap.Allocate(t.Width, t.Height);
				_biomeMap.Sweep(view.X, view.Y, ReadBiome, 6);
			}
			catch (Exception ex)
			{
				if (_reportedAssets.Add("biomes"))
				{
					Log.Error("biome map: " + ex.Message);
				}
				return;
			}
			BiomeMix.Step(_biomeMap, view.X, view.Y, dt);
			float num = (GameClock.EffectsPaused ? 0f : dt);
			_biomeTime += num;
			float sunUp = Mathf.Clamp01(1f - _night * 1.6f);
			float golden = ((_night > 0.02f && _night < 0.6f) ? Mathf.Clamp01(1f - Mathf.Abs(_night - 0.22f) / 0.3f) : 0f);
			float windX = Weather.WindX * 0.6f + Gusts.DirX * Gusts.Speed * Gusts.Strength;
			float windY = Weather.WindY * 0.6f + Gusts.DirY * Gusts.Speed * Gusts.Strength;
			BiomeFrame f = new BiomeFrame
			{
				Sink = _blastSink,
				Dt = num,
				Time = _biomeTime,
				Fov = view.Fov,
				SunUp = sunUp,
				Night = _night,
				Golden = golden,
				Level = biomeLevel,
				Amount = BiomeParticleAmount,
				WindX = windX,
				WindY = windY,
				Chill = Chill
			};
			_biomeParts.Step(in f, _biomeMap, BiomeMix);
		}

		private BiomeKind ReadBiome(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null)
			{
				return BiomeKind.None;
			}
			TileTypeBase tileTypeBase = (TileTypeBase)(((object)tile.top_type) ?? ((object)tile.main_type));
			if (tileTypeBase == null)
			{
				return BiomeKind.None;
			}
			if (!_biomeOfType.TryGetValue(tileTypeBase, out var value))
			{
				value = BiomeMap.FromTileId(tileTypeBase.id);
				_biomeOfType[tileTypeBase] = value;
			}
			return value;
		}

		private static bool IsBlastSprite(string id)
		{
			switch (id)
			{
			default:
				if (!IsPowerSprite(id))
				{
					return id == "fx_explosion_ufo";
				}
				break;
			case "fx_explosion_nuke_atomic":
			case "fx_explosion_huge":
			case "fx_explosion_meteorite":
			case "fx_nuke_flash":
				break;
			}
			return true;
		}

		private static bool BlastNear(float x, float y, float radius, float maxAge)
		{
			foreach (NukeFx nuke in Nukes)
			{
				if (nuke.Age < maxAge && (nuke.X - x) * (nuke.X - x) + (nuke.Y - y) * (nuke.Y - y) < radius * radius)
				{
					return true;
				}
			}
			foreach (ImpactFx impact in Impacts)
			{
				if (impact.Age < maxAge && (impact.X - x) * (impact.X - x) + (impact.Y - y) * (impact.Y - y) < radius * radius)
				{
					return true;
				}
			}
			return false;
		}

		private uint LandColor(TerrainCache terrain, float x, float y)
		{
			if (terrain == null || terrain.Colors.Length == 0)
			{
				return TerrainCache.Rgb(120, 140, 80);
			}
			int num = Mathf.Clamp((int)x, 0, terrain.Width - 1);
			int num2 = Mathf.Clamp((int)y, 0, terrain.Height - 1);
			return terrain.Colors[num2 * terrain.Width + num];
		}

		private void StartNuke(float x, float y, float scale, TerrainCache terrain, float age = 0f)
		{
			if (!BlastNear(x, y, 12f, 20f))
			{
				NukeFx nukeFx = new NukeFx(x, y, terrain.SampleHeight(x, y), scale, (uint)(x * 7919f + y * 104729f) ^ (uint)(Nukes.Count * -1640531535), LandColor(terrain, x, y));
				nukeFx.Age = age;
				Nukes.Add(nukeFx);
				if (age < 1f)
				{
					FirstPersonAudio.QueueRumble(Mathf.Sqrt((x - _eyeX) * (x - _eyeX) + (y - _eyeY) * (y - _eyeY)));
				}
				Log.Info($"BLAST nuke at {x:F0},{y:F0} scale {scale:F1}");
			}
		}

		private void StartImpact(float x, float y, TerrainCache terrain)
		{
			if (!BlastNear(x, y, 3f, 2f))
			{
				Impacts.Add(new ImpactFx(x, y, terrain.SampleHeight(x, y), 1f, (uint)(x * 104729f + y * 7919f) ^ (uint)(Impacts.Count * 40503), LandColor(terrain, x, y)));
				Log.Info($"BLAST meteor strike at {x:F0},{y:F0}");
			}
		}

		private bool TakeBlastEffect(string id, BaseEffect effect, TerrainCache terrain)
		{
			if (id == null)
			{
				return false;
			}
			if (IsPowerSprite(id))
			{
				return TakePowerEffect(id, effect, terrain);
			}
			switch (id)
			{
			case "fx_explosion_ufo":
				return TakeUfoExplosion(effect, terrain);
			case "fx_nuke_flash":
				_activeFlashes.Add(effect);
				if (_flashesSeen.Add(effect))
				{
					Vector3 position5 = effect.transform.position;
					string text = null;
					try
					{
						text = ((effect is NukeFlash nukeFlash && nukeFlash._terraform_asset != null) ? nukeFlash._terraform_asset.id : null);
					}
					catch
					{
					}
					StartNuke(position5.x, position5.y, (text == "czar_bomba") ? 1.8f : 1f, terrain);
				}
				return true;
			case "fx_explosion_nuke_atomic":
			case "fx_explosion_huge":
			{
				Vector3 position2 = effect.transform.position;
				if (!BlastNear(position2.x, position2.y, 14f, 30f))
				{
					StartNuke(position2.x, position2.y, (id == "fx_explosion_huge") ? 1.8f : 1f, terrain, 0.35f);
				}
				return true;
			}
			case "fx_explosion_meteorite":
			{
				Vector3 position4 = effect.transform.position;
				if (!BlastNear(position4.x, position4.y + 2f, 5f, 3f))
				{
					StartImpact(position4.x, position4.y + 2f, terrain);
				}
				return true;
			}
			case "fx_explosion_wave":
			{
				Vector3 position3 = effect.transform.position;
				return BlastNear(position3.x, position3.y, 8f, 4f);
			}
			case "fx_fire_smoke":
			{
				if (_meteorSlides.Count <= 0)
				{
					break;
				}
				Vector3 position = effect.transform.position;
				float unscaledTime = Time.unscaledTime;
				foreach (MeteorSlide value in _meteorSlides.Values)
				{
					if (!(unscaledTime > value.Until))
					{
						float num = position.x - value.Target.x;
						float num2 = position.y - value.Target.y;
						float num3 = num * value.Dir.x + num2 * value.Dir.y;
						float num4 = Mathf.Abs(num * value.Dir.y - num2 * value.Dir.x);
						if (num3 > -4f && num3 < value.Length + 8f && num4 < 5f)
						{
							return true;
						}
					}
				}
				break;
			}
			}
			return false;
		}

		private void NoteMeteorSlide(Meteorite m, Vector2 target)
		{
			Vector2 current_position = m.current_position;
			float magnitude = current_position.magnitude;
			float unscaledTime = Time.unscaledTime;
			_meteorSlides.TryGetValue(m, out var value);
			if (magnitude > 0.5f && (value.Until < unscaledTime || magnitude > value.Length || value.Target != target))
			{
				if (value.Until < unscaledTime || value.Target != target)
				{
					value.Length = 0f;
				}
				value.Target = target;
				value.Dir = current_position / magnitude;
				value.Length = Mathf.Max(value.Length, magnitude);
			}
			value.Until = unscaledTime + 8f;
			_meteorSlides[m] = value;
			try
			{
				m._timer_smoke = 99f;
			}
			catch
			{
			}
			if (_meteorSlides.Count <= 8)
			{
				return;
			}
			_oldSlides.Clear();
			foreach (KeyValuePair<BaseEffect, MeteorSlide> meteorSlide in _meteorSlides)
			{
				if (meteorSlide.Value.Until < unscaledTime)
				{
					_oldSlides.Add(meteorSlide.Key);
				}
			}
			foreach (BaseEffect oldSlide in _oldSlides)
			{
				_meteorSlides.Remove(oldSlide);
			}
		}

		private void DrawMeteorFx(Meteorite m, Vec3 head, Vec3 back, float dt)
		{
			MeteorFramesDrawn++;
			MeteorHighest = Mathf.Max(MeteorHighest, head.Z);
			_seenMeteors.Add(m);
			_meteorTargets[m] = m.transform.position;
			if (!_meteors.TryGetValue(m, out var value))
			{
				value = (_meteors[m] = new MeteorFx((uint)m.GetHashCode()));
			}
			float num = Mathf.Sqrt((head.X - _eyeX) * (head.X - _eyeX) + (head.Y - _eyeY) * (head.Y - _eyeY) + (head.Z - _blastSink.EyeZ) * (head.Z - _blastSink.EyeZ));
			value.Draw(_blastSink, BlastParticles, head, back, 2.2f * Mathf.Clamp(num / 70f, 1f, 2.4f), dt, _clock);
		}

		private void BeginBlasts(ViewState view, TerrainCache terrain)
		{
			float unscaledTime = Time.unscaledTime;
			if (_lastBlastTime >= 0f && unscaledTime - _lastBlastTime > 5f)
			{
				Nukes.Clear();
				Impacts.Clear();
				BlastParticles.List.Clear();
				_meteors.Clear();
				_meteorTargets.Clear();
				ClearPowers();
				ClearUfos();
				ClearShotgun();
			}
			_lastBlastTime = unscaledTime;
			_activeFlashes.Clear();
			_seenMeteors.Clear();
			GodFingersDrawn = 0;
			Streaks.Clear();
			FxSink blastSink = _blastSink;
			blastSink.Streaks = Streaks;
			blastSink.Billboards = Billboards;
			blastSink.Lights = Lights;
			blastSink.Meshes = Meshes;
			blastSink.EyeX = view.X;
			blastSink.EyeY = view.Y;
			blastSink.EyeZ = view.Z;
			blastSink.Yaw = view.Yaw;
			blastSink.Model = (Models.Ready ? new Func<string, MeshModel>(Models.Get) : null);
			blastSink.Ground = terrain.SampleHeight;
			blastSink.Day = 1f - _night;
			blastSink.Begin();
		}

		private void StepBlasts(MapBox world, TerrainCache terrain, float dt)
		{
			if (GameClock.EffectsPaused)
			{
				dt = 0f;
			}
			_goneMeteors.Clear();
			foreach (KeyValuePair<BaseEffect, MeteorFx> meteor in _meteors)
			{
				if (!_seenMeteors.Contains(meteor.Key))
				{
					_goneMeteors.Add(meteor.Key);
				}
			}
			foreach (BaseEffect goneMeteor in _goneMeteors)
			{
				if (goneMeteor is Meteorite m)
				{
					RestoreMeteor(m);
				}
				_meteorWays.Remove(goneMeteor);
				if (_meteorTargets.TryGetValue(goneMeteor, out var value))
				{
					StartImpact(value.x, value.y, terrain);
				}
				_meteors.Remove(goneMeteor);
				_meteorTargets.Remove(goneMeteor);
			}
			if (_flashesSeen.Count > _activeFlashes.Count)
			{
				_flashesSeen.IntersectWith(_activeFlashes);
			}
			FxSink blastSink = _blastSink;
			for (int num = Nukes.Count - 1; num >= 0; num--)
			{
				Nukes[num].Step(dt, blastSink, BlastParticles);
				if (Nukes[num].Finished)
				{
					Nukes.RemoveAt(num);
				}
			}
			for (int num2 = Impacts.Count - 1; num2 >= 0; num2--)
			{
				Impacts[num2].Step(dt, blastSink, BlastParticles);
				if (Impacts[num2].Finished)
				{
					Impacts.RemoveAt(num2);
				}
			}
			StepPowers(world, terrain, dt);
			StepShotgun(world, terrain, dt);
			StepBlocks(dt);
			StepLightning();
			BlastParticles.Update(dt, blastSink.Ground);
			BlastParticles.Draw(blastSink);
			ScreenFlash = ((blastSink.Flash > 0.004f) ? ((blastSink.FlashColor & 0xFFFFFF) | ((uint)(Mathf.Min(1f, blastSink.Flash) * 255f) << 24)) : 0u);
			BlastShake = Mathf.Max(BlastShake, blastSink.Shake);
		}

		private static bool IsNukeDrop(string id)
		{
			if (!(id == "atomic_bomb"))
			{
				return id == "czar_bomba";
			}
			return true;
		}

		private void NukeDropFx(Drop d, string id, float x, float y, float z, float ground)
		{
			FxSink blastSink = _blastSink;
			float num = ((id == "czar_bomba") ? 1.8f : 1f);
			float num2 = Mathf.Clamp01(Mathf.Max(0f, z - ground) / Mathf.Max(4f, Weather.Base - ground));
			float num3 = 0.55f + 0.45f * Mathf.Sin(_clock * (7f + 9f * (1f - num2)));
			float num4 = num * (5f + 9f * num2);
			int num5 = 44;
			for (int i = 0; i < num5; i++)
			{
				float num6 = (float)i / (float)num5 * MathF.PI * 2f + _clock * 0.4f;
				float num7 = x + Mathf.Cos(num6) * num4;
				float num8 = y + Mathf.Sin(num6) * num4;
				float num9 = blastSink.Ground(num7, num8);
				blastSink.Glow(num7, num8, num9 + 0.15f, 0.9f * num, TerrainCache.Rgb(255, 40, 30), 0.65f * num3);
				if ((i & 1) == 0)
				{
					float num10 = num4 * 0.55f;
					float num11 = x + Mathf.Cos((0f - num6) * 1.3f) * num10;
					float num12 = y + Mathf.Sin((0f - num6) * 1.3f) * num10;
					blastSink.Glow(num11, num12, blastSink.Ground(num11, num12) + 0.12f, 0.6f * num, TerrainCache.Rgb(255, 70, 40), 0.4f * num3);
				}
			}
			blastSink.Glow(x, y, ground + 0.2f, 2.2f * num, TerrainCache.Rgb(255, 50, 40), 0.7f * num3);
			blastSink.Light(x, y, ground + 1f, 6f + num4, TerrainCache.Rgb(255, 60, 40), 0.6f * num3);
			Vec3 vec = new Vec3(x, y, z + 0.6f * num);
			if (_nukeDrops.TryGetValue(d, out var value) && BlastParticles != null)
			{
				Vec3 vec2 = vec - value;
				float num13 = Mathf.Sqrt(Vec3.Dot(vec2, vec2));
				if (num13 < 30f)
				{
					_dropTrail += num13;
					while (_dropTrail > 0.35f)
					{
						_dropTrail -= 0.35f;
						float num14 = ((num13 > 0.0001f) ? (_dropTrail / num13) : 0f);
						BlastParticles.Add(new FxParticles.P
						{
							Pos = vec - vec2 * num14,
							Vel = new Vec3(0f, 0f, 0.3f),
							Life = 1.1f,
							Size = 1.1f * num,
							EndSize = 1.8f * num,
							Color = BlastFx.Heat(0.9f),
							EndColor = BlastFx.Heat(0.3f),
							Alpha = 170,
							Kind = FxParticles.Kind.Trail,
							Drag = 0.6f
						});
						if (_chipRandom.NextDouble() < 0.35)
						{
							BlastParticles.Spark(vec - vec2 * num14, new Vec3((float)(_chipRandom.NextDouble() - 0.5) * 3f, (float)(_chipRandom.NextDouble() - 0.5) * 3f, 1f), 0.8f, 0.12f, BlastFx.Heat(1f), BlastFx.Heat(0.4f), 2f, 1f);
						}
					}
				}
			}
			_nukeDrops[d] = vec;
			if (_nukeDrops.Count > 16)
			{
				_nukeDrops.Clear();
			}
			blastSink.Glow(vec.X, vec.Y, vec.Z, 2.4f * num, BlastFx.Heat(0.95f), 0.6f);
		}

		public float TakeBlastShake()
		{
			float blastShake = BlastShake;
			BlastShake = 0f;
			return blastShake;
		}

		private static bool BlockSprite(string id)
		{
			if (id == "fx_block" || id == "fx_dodge")
			{
				return Blocks.Watching;
			}
			return false;
		}

		private void StepBlocks(float dt)
		{
			FxSink blastSink = _blastSink;
			if (_blocksSeen < 0)
			{
				_blocksSeen = Blocks.Newest;
			}
			if (Blocks.Newest != _blocksSeen)
			{
				List<BlockEvent> events = Blocks.Events;
				for (int i = 0; i < events.Count; i++)
				{
					BlockEvent blockEvent = events[i];
					if (blockEvent.Serial <= _blocksSeen)
					{
						continue;
					}
					_blocksSeen = blockEvent.Serial;
					if (blockEvent.Age > 0.4f)
					{
						continue;
					}
					Vec3 toward = new Vec3(blockEvent.Toward.x, blockEvent.Toward.y, 0f);
					uint seed = (uint)(blockEvent.Serial * 7919 + 13);
					if (blockEvent.Kind == Stop.Dodged)
					{
						if (blockEvent.OnYou || blockEvent.Defender == null)
						{
							continue;
						}
						Vector2 current_position = blockEvent.Defender.current_position;
						float z = ((blastSink.Ground != null) ? blastSink.Ground(current_position.x, current_position.y) : (blockEvent.At.Z - 0.55f));
						BlockFx.Dodged(BlastParticles, new Vec3(current_position.x, current_position.y, z), toward, seed);
					}
					else
					{
						BlockFx.Stopped(BlastParticles, blockEvent.At, toward, blockEvent.Material, blockEvent.Weight, blockEvent.Shot, seed, blockEvent.OnYou ? 0.4f : 1f);
						if (blockEvent.Material == Clash.Metal && _clashLights.Count < 8)
						{
							_clashLights.Add(new ClashLight
							{
								At = blockEvent.At,
								Strength = (blockEvent.Shot ? 0.45f : 0.75f) + 0.35f * blockEvent.Weight
							});
						}
					}
					StopsDrawn++;
				}
			}
			for (int num = _clashLights.Count - 1; num >= 0; num--)
			{
				ClashLight value = _clashLights[num];
				value.Age += dt;
				if (value.Age >= 0.13f)
				{
					_clashLights.RemoveAt(num);
				}
				else
				{
					float num2 = 1f - value.Age / 0.13f;
					blastSink.Light(value.At.X, value.At.Y, value.At.Z, 1.6f, TerrainCache.Rgb(255, 236, 200), value.Strength * num2 * num2, 0.3f * num2);
					_clashLights[num] = value;
				}
			}
		}

		private static MeshModel BoardModel()
		{
			if (_boardModel != null)
			{
				return _boardModel;
			}
			MeshBuilder meshBuilder = new MeshBuilder("bounty_board");
			int slot = meshBuilder.Slot("wood", 3, TerrainCache.Rgb(118, 82, 50), 0);
			int slot2 = meshBuilder.Slot("wood_dark", 3, TerrainCache.Rgb(78, 52, 32), 0);
			int slot3 = meshBuilder.Slot("roof", 3, TerrainCache.Rgb(150, 70, 48), 0);
			int slot4 = meshBuilder.Slot("paper", 7, TerrainCache.Rgb(232, 218, 182), 0);
			int slot5 = meshBuilder.Slot("ink", 0, TerrainCache.Rgb(120, 34, 26), 0);
			meshBuilder.Part("board", -1, new Vec3(0f, 0f, 0f));
			float num = 0.75f;
			meshBuilder.Box(slot2, new Vec3(0f - num - 0.04f, 0f, 0.8f), 0.05f, 0.05f, 0.8f);
			meshBuilder.Box(slot2, new Vec3(num + 0.04f, 0f, 0.8f), 0.05f, 0.05f, 0.8f);
			meshBuilder.Box(slot, new Vec3(0f, 0f, 1.02f), num, 0.035f, 0.39999998f);
			meshBuilder.Box(slot2, new Vec3(0f, -0.04f, 1.4399999f), num + 0.04f, 0.02f, 0.025f);
			meshBuilder.Box(slot2, new Vec3(0f, -0.04f, 0.6f), num + 0.04f, 0.02f, 0.025f);
			meshBuilder.Box(slot3, new Vec3(0f, -0.12f, 1.62f), num + 0.16f, 0.2f, 0.025f);
			meshBuilder.Box(slot3, new Vec3(0f, 0.12f, 1.62f), num + 0.16f, 0.2f, 0.025f);
			meshBuilder.Box(slot4, new Vec3(0f, -0.04f, 1.3499999f), num * 0.55f, 0.008f, 0.045f);
			meshBuilder.Box(slot5, new Vec3(0f, -0.05f, 1.3499999f), num * 0.42f, 0.004f, 0.012f);
			for (int i = 0; i < 4; i++)
			{
				Vector2 vector = PosterCentre(i);
				meshBuilder.Box(slot4, new Vec3(vector.x, -0.042f, vector.y), 0.16f, 0.008f, 0.17f);
				meshBuilder.Box(slot5, new Vec3(vector.x, -0.052f, vector.y + 0.13f), 0.1f, 0.004f, 0.018f);
				meshBuilder.Box(slot5, new Vec3(vector.x, -0.052f, vector.y - 0.135f), 0.07f, 0.004f, 0.012f);
			}
			_boardModel = meshBuilder.Build();
			return _boardModel;
		}

		private static MeshModel NotesModel()
		{
			if (_notesModel != null)
			{
				return _notesModel;
			}
			MeshBuilder meshBuilder = new MeshBuilder("board_notices");
			int slot = meshBuilder.Slot("paper", 7, TerrainCache.Rgb(240, 232, 204), 0);
			int slot2 = meshBuilder.Slot("ink", 0, TerrainCache.Rgb(46, 44, 58), 0);
			int slot3 = meshBuilder.Slot("pin", 0, TerrainCache.Rgb(200, 60, 40), 0);
			for (int i = 0; i < 3; i++)
			{
				meshBuilder.Part("notice" + i, -1, new Vec3(0f, 0f, 0f));
				float num = 1.22f - (float)i * 0.23f;
				meshBuilder.Box(slot, new Vec3(0.635f, -0.044f, num), 0.075f, 0.006f, 0.09f);
				meshBuilder.Box(slot2, new Vec3(0.635f, -0.051f, num + 0.04f), 0.05f, 0.003f, 0.008f);
				meshBuilder.Box(slot2, new Vec3(0.625f, -0.051f, num + 0.005f), 0.045f, 0.003f, 0.006f);
				meshBuilder.Box(slot2, new Vec3(0.635f, -0.051f, num - 0.03f), 0.05f, 0.003f, 0.006f);
				meshBuilder.Box(slot3, new Vec3(0.635f, -0.054f, num + 0.075f), 0.012f, 0.006f, 0.012f);
			}
			_notesModel = meshBuilder.Build();
			return _notesModel;
		}

		private static Vector2 PosterCentre(int i)
		{
			return new Vector2((i % 2 == 0) ? (-0.36f) : 0.36f, (i < 2) ? 1.06f : 0.79f);
		}

		private void CollectBoards(ViewState view, TerrainCache terrain, SpriteCache sprites)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = ((instance != null) ? instance.Host : null);
			if (actor != null && (!actor.isAlive() || actor.data == null))
			{
				actor = null;
			}
			List<Law.Board> list = Law.BoardsNear(new Vector2(view.X, view.Y), 70f, _boardsScratch);
			if (list.Count == 0)
			{
				return;
			}
			MeshModel model = BoardModel();
			foreach (Law.Board item2 in list)
			{
				float num = item2.At.x - view.X;
				float num2 = item2.At.y - view.Y;
				float num3 = num * num + num2 * num2;
				if (num3 > 9f && num * _vfx + num2 * _vfy < -2f)
				{
					continue;
				}
				if (!_boards.TryGetValue(item2.City, out var value))
				{
					Dictionary<City, MeshInstance> boards = _boards;
					City city = item2.City;
					MeshInstance obj = new MeshInstance
					{
						Model = model,
						Static = true
					};
					value = obj;
					boards[city] = obj;
				}
				float num4 = (item2.Ground = terrain.SampleHeight(item2.At.x, item2.At.y));
				value.Place(item2.At.x, item2.At.y, num4, item2.Yaw, 1f, 1f, 1f);
				Meshes.Add(value);
				if (num3 > 676f || actor == null)
				{
					continue;
				}
				int num5 = Mathf.Min(3, Quests.TodayCount(item2.City, actor));
				if (num5 > 0)
				{
					if (!_boardNotes.TryGetValue(item2.City, out var value2))
					{
						Dictionary<City, MeshInstance> boardNotes = _boardNotes;
						City city2 = item2.City;
						MeshInstance obj2 = new MeshInstance
						{
							Model = NotesModel(),
							Static = true,
							NoShadow = true
						};
						value2 = obj2;
						boardNotes[city2] = obj2;
					}
					value2.Hidden = 0uL;
					for (int i = num5; i < 3; i++)
					{
						value2.Hidden |= (ulong)(1L << i);
					}
					value2.Place(item2.At.x, item2.At.y, num4, item2.Yaw, 1f, 1f, 1f);
					Meshes.Add(value2);
				}
				List<Poster> list2 = Law.Posters(item2, actor);
				float num6 = Mathf.Cos(item2.Yaw);
				float num7 = Mathf.Sin(item2.Yaw);
				Vec3 vec = new Vec3(0f - num7, num6, 0f);
				Vec3 v = new Vec3(0f, 0f, 1f);
				Vec3 vec2 = new Vec3(num6, num7, 0f);
				for (int j = 0; j < list2.Count && j < 4; j++)
				{
					Poster poster = list2[j];
					if (poster.Target != null && poster.Target.isAlive())
					{
						SpriteImage spriteImage = null;
						try
						{
							spriteImage = sprites.Get(poster.Target.getSpriteToRender());
						}
						catch
						{
						}
						if (spriteImage != null && spriteImage.Width >= 2)
						{
							Vector2 vector = PosterCentre(j);
							float num8 = 0.24f / (float)Mathf.Max(spriteImage.Width, spriteImage.Height);
							Vec3 vec3 = new Vec3(item2.At.x, item2.At.y, num4 + vector.y) + vec * vector.x + vec2 * 0.06f;
							VoxelObject item = new VoxelObject
							{
								Shape = spriteImage.SlabShape(1),
								Tag = -1,
								NoShadow = true
							};
							item.U = vec;
							item.V = v;
							item.N = Vec3.Cross(item.U, item.V);
							item.SizeU = (item.SizeV = num8);
							item.SizeN = 0.01f;
							item.Origin = vec3 - item.U * ((float)spriteImage.Width * 0.5f * num8) - item.V * ((float)spriteImage.Height * 0.42f * num8);
							Voxels.Add(item);
						}
					}
				}
			}
		}

		private static long BcNow()
		{
			if (!BuildClockOn)
			{
				return 0L;
			}
			return Stopwatch.GetTimestamp();
		}

		private static void BcAdd(ref double into, long from)
		{
			if (from != 0L)
			{
				into += (double)(Stopwatch.GetTimestamp() - from) * BcMs;
			}
		}

		internal static string TakeBuildClock()
		{
			double num = 1.0 / (double)Math.Max(1L, BcFrames);
			string result = $"buildings {BcTotal * num:F2} ms a frame: rescan {BcRescan * num:F2}, slice {BcSlice * num:F2}, houses {BcHouses * num:F2} ({(double)BcHousesN * num:F0}), " + $"trees {BcTrees * num:F2} ({(double)BcTreesN * num:F0}), other models {BcOther * num:F2} ({(double)BcOtherN * num:F0}), the loop's own {(BcTotal - BcRescan - BcSlice - BcHouses - BcTrees - BcOther) * num:F2}";
			BcRescan = (BcSlice = (BcHouses = (BcTrees = (BcOther = (BcTotal = 0.0)))));
			BcHousesN = (BcTreesN = (BcOtherN = (BcFrames = 0L)));
			return result;
		}

		private void IndexBuildingCells()
		{
			int count = _buildings.Count;
			int num = Math.Max(1, (MapBox.width + 16 - 1) / 16);
			int num2 = Math.Max(1, (MapBox.height + 16 - 1) / 16);
			int num3 = num * num2;
			if (_cellCounts.Length < num3 + 1)
			{
				_cellCounts = new int[num3 + 1];
				_cellEnd = new int[num3];
				_cellMinX = new float[num3];
				_cellMinY = new float[num3];
				_cellMaxX = new float[num3];
				_cellMaxY = new float[num3];
			}
			else
			{
				Array.Clear(_cellCounts, 0, num3 + 1);
			}
			if (_cellOf.Length < count)
			{
				_cellOf = new int[Math.Max(count, _cellOf.Length * 2)];
			}
			if (_cellAt.Length < count)
			{
				_cellAt = new int[Math.Max(count, _cellAt.Length * 2)];
			}
			if (_cellScratch.Length < count)
			{
				_cellScratch = new NearBuilding[Math.Max(count, _cellScratch.Length * 2)];
			}
			for (int i = 0; i < num3; i++)
			{
				_cellMinX[i] = (_cellMinY[i] = float.MaxValue);
				_cellMaxX[i] = (_cellMaxY[i] = float.MinValue);
			}
			for (int j = 0; j < count; j++)
			{
				NearBuilding nearBuilding = _buildings[j];
				Vector2 vector = ((nearBuilding.Building != null) ? Position(nearBuilding.Building) : Vector2.zero);
				int num4 = Mathf.Clamp((int)(vector.x / 16f), 0, num - 1);
				int num5 = Mathf.Clamp((int)(vector.y / 16f), 0, num2 - 1) * num + num4;
				_cellOf[j] = num5;
				_cellCounts[num5 + 1]++;
				if (vector.x < _cellMinX[num5])
				{
					_cellMinX[num5] = vector.x;
				}
				if (vector.y < _cellMinY[num5])
				{
					_cellMinY[num5] = vector.y;
				}
				if (vector.x > _cellMaxX[num5])
				{
					_cellMaxX[num5] = vector.x;
				}
				if (vector.y > _cellMaxY[num5])
				{
					_cellMaxY[num5] = vector.y;
				}
			}
			for (int k = 0; k < num3; k++)
			{
				_cellCounts[k + 1] += _cellCounts[k];
			}
			for (int l = 0; l < num3; l++)
			{
				_cellEnd[l] = _cellCounts[l + 1];
			}
			for (int m = 0; m < count; m++)
			{
				_cellAt[m] = -1;
			}
			for (int n = 0; n < count; n++)
			{
				int num6 = _cellOf[n];
				int num7 = _cellCounts[num6]++;
				_cellScratch[num7] = _buildings[n];
			}
			for (int num8 = 0; num8 < num3; num8++)
			{
				int num9 = ((num8 != 0) ? _cellEnd[num8 - 1] : 0);
				if (_cellEnd[num8] > num9)
				{
					_cellAt[num9] = num8;
				}
			}
			_buildings.Clear();
			for (int num10 = 0; num10 < count; num10++)
			{
				_buildings.Add(_cellScratch[num10]);
			}
			Array.Clear(_cellScratch, 0, count);
		}

		private bool CellOutOfSight(int cell)
		{
			if (BuildingCellsOff || FromSky || cell < 0 || cell >= _cellEnd.Length)
			{
				return false;
			}
			float num = _cellMinX[cell];
			float num2 = _cellMinY[cell];
			float num3 = _cellMaxX[cell];
			float num4 = _cellMaxY[cell];
			if (num > num3)
			{
				return false;
			}
			float num5 = Mathf.Clamp(_eyeX, num, num3) - _eyeX;
			float num6 = Mathf.Clamp(_eyeY, num2, num4) - _eyeY;
			if (num5 * num5 + num6 * num6 < 3136f)
			{
				return false;
			}
			float num7 = (num + num3) * 0.5f - _eyeX;
			float num8 = (num2 + num4) * 0.5f - _eyeY;
			float num9 = Mathf.Sqrt((num3 - num) * (num3 - num) + (num4 - num2) * (num4 - num2)) * 0.5f;
			if (!(num7 * _vfx + num8 * _vfy < 0f - (num9 + 16f + _cullBack)) && InView(num7, num8, num9 + 16f))
			{
				return false;
			}
			CellsSkipped++;
			return true;
		}

		private bool Burnt(NearBuilding nb, Building b, ref string family)
		{
			bool flag;
			try
			{
				flag = b.isRuin();
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				nb.WasStanding = true;
				return false;
			}
			BurntLook burntLook = nb.Burnt ?? (nb.Burnt = new BurntLook());
			if (burntLook.For != family)
			{
				burntLook.For = family;
				string text = BurntTrees.FamilyOf(family);
				burntLook.Family = ((Models.Get(text) != null) ? text : ((Models.Get("burnt_tree_round") != null) ? "burnt_tree_round" : null));
				burntLook.Middle = ((burntLook.Family != null) ? (burntLook.Family + "_m") : null);
				burntLook.EmbersNear = ((burntLook.Family != null) ? Models.Get(burntLook.Family + "_embers") : null);
				burntLook.EmbersMiddle = ((burntLook.Family != null) ? Models.Get(burntLook.Family + "_embers_m") : null);
				burntLook.Palette = null;
			}
			if (burntLook.Family == null)
			{
				return false;
			}
			if (nb.WasStanding)
			{
				nb.WasStanding = false;
				burntLook.SmoulderFrom = (IsBurning(b) ? _clock : (-1f));
			}
			family = burntLook.Family;
			burntLook.Frame = _frame;
			return true;
		}

		private void BurntColours(NearBuilding nb, MeshInstance inst, MeshModel model)
		{
			BurntLook burnt = nb.Burnt;
			if (burnt.Palette == null || burnt.PaletteImage != nb.Image || burnt.Palette.Length != model.SlotNames.Length)
			{
				burnt.Palette = BurntTrees.Colors(model, nb.Image);
				burnt.PaletteImage = nb.Image;
			}
			inst.Palette = burnt.Palette;
		}

		private string DrawnFamily(NearBuilding nb)
		{
			if (nb.Burnt == null || nb.Burnt.Family == null || _frame - nb.Burnt.Frame > 3)
			{
				return nb.MeshFamily;
			}
			return nb.Burnt.Family;
		}

		private void BurntSmoulder(NearBuilding nb, Building b, MeshInstance inst, Vector2 p, float ground, float dist)
		{
			BurntLook burnt = nb.Burnt;
			if (burnt.SmoulderFrom < 0f)
			{
				return;
			}
			bool flag = IsBurning(b);
			if (flag)
			{
				burnt.SmoulderFrom = _clock;
			}
			float num = _clock - burnt.SmoulderFrom;
			if (num < 0f || num > 80f)
			{
				burnt.SmoulderFrom = -1f;
			}
			else
			{
				if (dist > 48f || inst.ShadowOnly)
				{
					return;
				}
				float num2 = 1f - num / 80f;
				float num3 = nb.Height * nb.Image.VisibleFraction;
				if (!flag)
				{
					int puffs = Mathf.Clamp(Mathf.RoundToInt(((dist < 30f) ? 4f : 2f) * num2 + 0.5f), 1, 4);
					FireField.AddPlume(Billboards, p.x, p.y, ground + num3 * 0.55f, Mathf.Clamp(nb.Width * 0.2f, 0.3f, 1.6f), puffs, (uint)b.GetHashCode(), _clock, 0.55f + 0.45f * num2);
				}
				float num4 = Mathf.Clamp01(1f - num / 44f);
				if (num4 <= 0f)
				{
					return;
				}
				float num5 = Flicker(p.x * 1.3f + p.y);
				MeshModel meshModel = ((inst.Model.Name == burnt.Family) ? burnt.EmbersNear : ((inst.Model.Name == burnt.Middle) ? burnt.EmbersMiddle : null));
				if (meshModel != null && dist < 40f)
				{
					MeshInstance meshInstance = burnt.Embers;
					if (meshInstance == null)
					{
						MeshInstance obj = new MeshInstance
						{
							NoShadow = true
						};
						MeshInstance meshInstance2 = obj;
						burnt.Embers = obj;
						meshInstance = meshInstance2;
					}
					MeshInstance meshInstance3 = meshInstance;
					meshInstance3.Model = meshModel;
					meshInstance3.Position = inst.Position;
					meshInstance3.Basis = inst.Basis;
					meshInstance3.Palette = BurntTrees.Embers(meshModel, num4 * (0.88f + 0.12f * num5));
					meshInstance3.Fade = inst.Fade;
					meshInstance3.Tag = inst.Tag;
					meshInstance3.SortDepth = inst.SortDepth;
					Meshes.Add(meshInstance3);
				}
				if (_night > 0.2f && dist < 48f)
				{
					float num6 = num4 * num5 * _night * 0.5f;
					AddLight(p.x, p.y, ground + num3 * 0.25f, Mathf.Clamp(nb.Width * 0.5f, 2f, 4.5f), 1f * num6, 0.42f * num6, 0.12f * num6, 0.12f);
				}
			}
		}

		internal string ModelNameForTest(Building b)
		{
			return Find(b)?.Mesh?.Model?.Name;
		}

		internal float SwayForTest(Building b)
		{
			return Find(b)?.Mesh?.Sway ?? (-1f);
		}

		internal bool SmoulderingForTest(Building b)
		{
			BurntLook burntLook = Find(b)?.Burnt;
			if (burntLook != null)
			{
				return burntLook.SmoulderFrom >= 0f;
			}
			return false;
		}

		private bool BurntGone(NearBuilding nb, Building b)
		{
			bool flag;
			try
			{
				flag = b == null || !b.isAlive() || b.isOnRemove();
			}
			catch
			{
				flag = true;
			}
			if (!flag)
			{
				return false;
			}
			BurntLook burnt = nb.Burnt;
			if (_frame - burnt.Frame <= 3 && nb.Mesh != null && nb.Mesh.Model != null && !nb.Mesh.ShadowOnly)
			{
				Crumble(nb, burnt);
			}
			burnt.Frame = -100;
			return true;
		}

		private void Crumble(NearBuilding nb, BurntLook bl)
		{
			MeshInstance mesh = nb.Mesh;
			float z = mesh.Basis.C2.Z;
			float across = Mathf.Sqrt(mesh.Basis.C0.X * mesh.Basis.C0.X + mesh.Basis.C0.Y * mesh.Basis.C0.Y);
			Crumbling crumbling = new Crumbling
			{
				Mesh = new MeshInstance
				{
					Model = mesh.Model,
					Palette = mesh.Palette,
					Snow = mesh.Snow
				},
				X = mesh.Position.X,
				Y = mesh.Position.Y,
				Ground = mesh.Position.Z,
				Heading = nb.Angle,
				Across = across,
				Scale = z,
				Tall = nb.Height * ((nb.Image != null) ? nb.Image.VisibleFraction : 0.8f),
				Seed = (uint)nb.GetHashCode()
			};
			_crumbles.Add(crumbling);
			if (_crumbles.Count > 12)
			{
				_crumbles.RemoveAt(0);
			}
			if (nb.Image == null)
			{
				return;
			}
			for (int i = 0; i < 12; i++)
			{
				if (_chips.Count >= 80)
				{
					break;
				}
				uint num = RandomColor(nb.Image, nb.Image.OpaqueBottom, nb.Image.OpaqueTop);
				if (num != 0)
				{
					float f = (float)_chipRandom.NextDouble() * 6.283f;
					float num2 = (float)_chipRandom.NextDouble();
					_chips.Add(new Chip
					{
						P = new Vec3(crumbling.X + Mathf.Cos(f) * num2 * nb.Width * 0.3f, crumbling.Y + Mathf.Sin(f) * num2 * nb.Width * 0.3f, crumbling.Ground + crumbling.Tall * (0.2f + 0.6f * (float)_chipRandom.NextDouble())),
						V = new Vec3(Mathf.Cos(f) * (0.6f + num2), Mathf.Sin(f) * (0.6f + num2), 0.4f + (float)_chipRandom.NextDouble() * 1.2f),
						Life = 0.8f + (float)_chipRandom.NextDouble() * 0.6f,
						Size = 0.035f + (float)_chipRandom.NextDouble() * 0.04f,
						Spin = (float)_chipRandom.NextDouble() * 6f,
						Shape = ChipShape(num)
					});
				}
			}
		}

		private void StepCrumbles(ViewState view, float dt)
		{
			for (int num = _crumbles.Count - 1; num >= 0; num--)
			{
				Crumbling crumbling = _crumbles[num];
				crumbling.Age += dt;
				if (crumbling.Age > 2.2f)
				{
					_crumbles.RemoveAt(num);
				}
				else
				{
					float num2 = crumbling.X - view.X;
					float num3 = crumbling.Y - view.Y;
					float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
					if (crumbling.Age < 0.9f)
					{
						float num5 = crumbling.Age / 0.9f;
						float num6 = num5 * num5;
						crumbling.Mesh.Place(crumbling.X, crumbling.Y, crumbling.Ground - crumbling.Tall * 0.06f * num6, crumbling.Heading, crumbling.Across * (1f + 0.3f * num6), crumbling.Across * (1f + 0.3f * num6), crumbling.Scale * Mathf.Max(0.04f, 1f - 0.92f * num6));
						crumbling.Mesh.Fade = ((num5 < 0.55f) ? 1f : Mathf.Clamp01(1f - (num5 - 0.55f) / 0.45f));
						crumbling.Mesh.SortDepth = num4;
						Meshes.Add(crumbling.Mesh);
					}
					if (num4 < 48f)
					{
						AshPuffs(crumbling);
					}
				}
			}
		}

		private void AshPuffs(Crumbling c)
		{
			float num = Mathf.Clamp(c.Tall * 0.35f, 0.5f, 3f);
			for (int i = 0; i < 7; i++)
			{
				uint num2 = TerrainModel.Hash((int)c.Seed + i * 977, i * 31 + 5);
				float num3 = (float)((num2 >> 3) & 0xFF) / 255f * 0.45f;
				float num4 = c.Age - num3;
				if (!(num4 <= 0f))
				{
					float num5 = num4 / (2.2f - num3);
					if (!(num5 >= 1f))
					{
						float num6 = c.Tall * (0.1f + 0.6f * ((float)((num2 >> 11) & 0xFF) / 255f));
						float num7 = (float)((num2 >> 12) & 0xFF) / 255f - 0.5f;
						float num8 = (float)((num2 >> 20) & 0xFF) / 255f - 0.5f;
						float num9 = Mathf.Clamp(c.Tall * 0.2f, 0.4f, 2.4f) * (0.6f + 1.3f * num5);
						float x = c.X + num7 * num * (0.4f + num5);
						float y = c.Y + num8 * num * (0.4f + num5);
						float num10 = c.Ground + num6 * (1f - 0.45f * num5) + 0.2f * num5;
						float num11 = ((num5 < 0.12f) ? (num5 / 0.12f) : Mathf.Pow(1f - (num5 - 0.12f) / 0.88f, 1.2f));
						Billboards.Add(new Billboard
						{
							X = x,
							Y = y,
							Z = num10 - num9 * 0.5f,
							GroundZ = num10 - num9,
							Width = num9,
							Height = num9,
							Image = FireField.SoftSmoke(5 - (i & 1), (int)(num2 & 3)),
							Kind = BillboardKind.Facing,
							Flip = ((num2 & 4) != 0),
							Tag = -1,
							Alpha = (byte)Mathf.Max(1, (int)(150f * num11))
						});
					}
				}
			}
		}

		private static string BiomeBush(NearBuilding nb, Building b, string family)
		{
			if (family != "bush_fruit" && family != "bush")
			{
				return family;
			}
			WorldTile current_tile = b.current_tile;
			if (current_tile == null)
			{
				return family;
			}
			TileTypeBase tileTypeBase = (TileTypeBase)(((object)current_tile.top_type) ?? ((object)current_tile.main_type));
			if (tileTypeBase == null)
			{
				return family;
			}
			if (!BushLooks.TryGetValue(tileTypeBase, out var value))
			{
				value = "bush_" + BushLookOf(tileTypeBase.id ?? "");
				if (Models.Get(value) == null)
				{
					value = ((Models.Get("bush_grass") != null) ? "bush_grass" : null);
				}
				BushLooks[tileTypeBase] = value;
			}
			if (value == null)
			{
				return family;
			}
			if (nb.Mesh?.Model != null && !nb.Mesh.Model.Name.StartsWith(value, StringComparison.Ordinal))
			{
				nb.PaletteImage = null;
			}
			return value;
		}

		private static string BushLookOf(string id)
		{
			string text = id;
			if (text.EndsWith("_low", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - 4);
			}
			else if (text.EndsWith("_high", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - 5);
			}
			switch (text)
			{
			case "grass":
			case "lemon":
			case "birch":
			case "maple":
			case "swamp":
			case "candy":
			case "flower":
			case "clover":
			case "garlic":
			case "jungle":
			case "desert":
			case "enchanted":
			case "celestial":
			case "wasteland":
			case "corrupted":
			case "rocklands":
			case "paradox":
			case "savanna":
			case "crystal":
			case "infernal":
			case "mushroom":
			case "permafrost":
			case "singularity":
				return text;
			case "sand":
				return "desert";
			case "hills":
			case "mountains":
				return "rocklands";
			case "tumor":
				return "corrupted";
			case "biomass":
				return "swamp";
			case "pumpkin":
				return "maple";
			case "cybertile":
				return "singularity";
			default:
				if (text.Contains("snow") || text.Contains("ice") || text.Contains("frozen"))
				{
					return "permafrost";
				}
				if (text.Contains("lava") || text.Contains("ash"))
				{
					return "infernal";
				}
				if (text.Contains("sand"))
				{
					return "desert";
				}
				return "grass";
			}
		}

		private static float BushScale(string family, float s)
		{
			if (!family.StartsWith("bush_", StringComparison.Ordinal) || family == "bush_fruit")
			{
				return s;
			}
			MeshModel meshModel = Models.Get(family);
			if (meshModel == null)
			{
				return s;
			}
			float num = meshModel.InfoFloat("tiles", 0f);
			float num2 = meshModel.InfoFloat("height", 0f);
			if (!(num > 0f) || !(num2 > 0f))
			{
				return s;
			}
			return num / num2;
		}

		private static bool IsBiomeBush(BuildingAsset asset)
		{
			if (asset == null || Models.Get("bush_grass") == null || AssetStyle.Classify(asset) != BuildingLook.Lump)
			{
				return false;
			}
			string text = Models.LumpFamily(asset.id);
			if (!(text == "bush_fruit"))
			{
				return text == "bush";
			}
			return true;
		}

		internal static float ShipHeel(Actor a)
		{
			if (a == null || Recoils.Count == 0 || !Recoils.TryGetValue(a, out var value))
			{
				return 0f;
			}
			float time = Time.time;
			float num = 0f;
			for (int i = 0; i < 3; i++)
			{
				num += CannonFx.Heel(time - value.At[i], value.Side[i], value.Strength);
			}
			return Mathf.Clamp(num, -0.14f, 0.14f);
		}

		private static float CannonDetail(float dist)
		{
			return Mathf.Clamp(28f / Mathf.Max(1f, dist), 0.35f, 1f);
		}

		private static bool Wet(TerrainCache terrain, float x, float y)
		{
			if (terrain == null || terrain.Flags.Length == 0)
			{
				return false;
			}
			int num = Mathf.Clamp((int)x, 0, terrain.Width - 1);
			int num2 = Mathf.Clamp((int)y, 0, terrain.Height - 1);
			if ((terrain.Flags[num2 * terrain.Width + num] & 1) != 0)
			{
				return true;
			}
			try
			{
				string id = World.world.GetTile(num, num2).Type.id;
				return id.StartsWith("pit_", StringComparison.Ordinal) && (id.EndsWith("ocean", StringComparison.Ordinal) || id.EndsWith("waters", StringComparison.Ordinal));
			}
			catch
			{
				return false;
			}
		}

		private bool DrawCannonShot(Projectile p, Vector3 pos, float baseZ, float dist, TerrainCache terrain)
		{
			string id = p.asset.id;
			bool flag = id == "cannonball";
			if (!flag && id != "fireball")
			{
				return false;
			}
			Vector2 startVector = p.getStartVector();
			if (!_cannonShots.TryGetValue(p, out var value) || value.Start != startVector)
			{
				if (value != null && !value.Ended)
				{
					EndShot(value, terrain);
				}
				Actor actor = null;
				try
				{
					actor = p.by_who as Actor;
				}
				catch
				{
				}
				bool flag2 = actor != null && BoatHelm.IsBoat(actor);
				if (!flag && !flag2)
				{
					_cannonShots.Remove(p);
					return false;
				}
				value = new CannonShot
				{
					Start = startVector,
					Ship = (flag2 ? actor : null),
					IsBall = flag,
					Seed = (uint)((p.GetHashCode() * -1640531535) ^ _frame)
				};
				try
				{
					value.Target = p._main_target;
				}
				catch
				{
				}
				value.Aim = p.getTargetVector();
				value.AimWet = Wet(terrain, value.Aim.x, value.Aim.y);
				value.Total = Mathf.Max(1f, (value.Aim - startVector).magnitude);
				value.Size = Mathf.Max(1f, SkyLeast(pos.x, pos.y, 8f) / 2.4f);
				if (flag)
				{
					value.Ball = new CannonballFx(value.Seed);
				}
				_cannonShots[p] = value;
			}
			value.Seen = _frame;
			if (value.Ended)
			{
				return value.IsBall;
			}
			float dt = (GameClock.EffectsPaused ? 0f : _frameDt);
			float num = baseZ;
			value.Steered = FirstPersonShots.Steered(p);
			if (!value.Steered && Wet(terrain, pos.x, pos.y))
			{
				num = Mathf.Max(baseZ, terrain.LiquidSurface(pos.x, pos.y));
			}
			Vec3 vec = new Vec3(pos.x, pos.y, num + pos.z);
			if (!value.Started)
			{
				value.Started = true;
				value.First = vec;
				if (value.Ship != null && FireGun(value, p, vec, terrain, out var muzzle))
				{
					value.Offset = muzzle - vec;
				}
			}
			value.LastGame = new Vector2(pos.x, pos.y);
			bool flag3 = false;
			try
			{
				flag3 = p.isDeadAnimation() || p.isTargetReached();
			}
			catch
			{
			}
			if (flag3)
			{
				if (CannonTrace)
				{
					string arg = "?";
					try
					{
						arg = $"{p._state}, reached {p.isTargetReached()}, height {pos.z:F2}, from {startVector.x:F1},{startVector.y:F1}";
					}
					catch
					{
					}
					Log.Info($"CANNON ball ended at {pos.x:F1},{pos.y:F1}: {arg}");
				}
				EndShot(value, terrain, seen: true);
				return value.IsBall;
			}
			float num2 = Mathf.Sqrt((pos.x - value.First.X) * (pos.x - value.First.X) + (pos.y - value.First.Y) * (pos.y - value.First.Y));
			float num3 = 1f - BlastFx.Smooth(num2 / value.Total / 0.55f);
			Vec3 at = (value.LastDrawn = vec + value.Offset * num3);
			if (!value.IsBall)
			{
				return false;
			}
			value.Ball.Draw(_blastSink, BlastParticles, at, value.Size, dt, CannonDetail(dist));
			CannonBallFrames++;
			return true;
		}

		private bool FireGun(CannonShot s, Projectile p, Vec3 from, TerrainCache terrain, out Vec3 muzzle)
		{
			muzzle = from;
			Actor ship = s.Ship;
			if (!Hull(ship, out var model, out var pos, out var basis, out var scale))
			{
				return false;
			}
			Vector2 targetVector = p.getTargetVector();
			Vector2 vector = new Vector2(basis.C0.X, basis.C0.Y);
			int num = (((targetVector.x - pos.X) * vector.x + (targetVector.y - pos.Y) * vector.y >= 0f) ? 1 : (-1));
			GunsFired.TryGetValue(ship, out var value);
			GunsFired[ship] = value + 1;
			if (GunsFired.Count > 128)
			{
				GunsFired.Clear();
			}
			Vec3 vec = CannonFx.Port(model, num, value);
			muzzle = pos + basis.Mul(vec);
			float lift = 0.1f;
			try
			{
				Vector3 velocity = p._velocity;
				float num2 = Mathf.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y);
				if (num2 > 0.001f)
				{
					lift = Mathf.Clamp(velocity.z / num2, 0.04f, 0.35f);
				}
			}
			catch
			{
			}
			Vector2 toward = new Vector2(targetVector.x - muzzle.X, targetVector.y - muzzle.Y);
			Vec3 dir = GunDir(basis, num, toward, lift);
			Vector2 normalized = new Vector2(dir.X, dir.Y).normalized;
			bool flag = Wet(terrain, muzzle.X + normalized.x * 1.2f, muzzle.Y + normalized.y * 1.2f);
			float dist = Mathf.Sqrt((muzzle.X - _eyeX) * (muzzle.X - _eyeX) + (muzzle.Y - _eyeY) * (muzzle.Y - _eyeY));
			float scale2 = Mathf.Clamp(scale / 0.55f, 0.75f, 1.4f) * 1.25f * Mathf.Max(1f, SkyLeast(muzzle.X, muzzle.Y, 10f) / 3f);
			MuzzleBlastFx fx = new MuzzleBlastFx(muzzle, dir, scale2, s.Seed, flag ? 0f : float.NaN, p.asset.id == "fireball")
			{
				Detail = CannonDetail(dist)
			};
			Firings.Add(new GunFiring
			{
				Fx = fx,
				Ship = ship,
				Port = vec,
				Side = num,
				Toward = toward,
				Lift = lift
			});
			if (Firings.Count > 40)
			{
				Firings.RemoveAt(0);
			}
			if (!Recoils.TryGetValue(ship, out var value2))
			{
				value2 = (Recoils[ship] = new Recoil());
			}
			value2.At[value2.Next] = Time.time;
			value2.Side[value2.Next] = num;
			value2.Next = (value2.Next + 1) % 3;
			value2.Strength = Mathf.Clamp(0.55f / Mathf.Max(0.3f, scale), 0.8f, 1.3f);
			value2.Last = Time.time;
			CannonMuzzles++;
			LastMuzzle = muzzle;
			LastMuzzleSide = num;
			LastMuzzleGap = Mathf.Sqrt((muzzle.X - from.X) * (muzzle.X - from.X) + (muzzle.Y - from.Y) * (muzzle.Y - from.Y));
			return true;
		}

		private static Vec3 GunDir(Mat3 basis, int side, Vector2 toward, float lift)
		{
			Vector2 vector = new Vector2(basis.C0.X, basis.C0.Y).normalized * side;
			Vector2 vector2 = ((toward.sqrMagnitude > 0.0001f) ? toward.normalized : vector);
			Vector2 normalized = (vector * 0.4f + vector2 * 0.6f).normalized;
			if (Vector2.Dot(normalized, vector) < 0.35f)
			{
				normalized = (vector * 0.7f + vector2 * 0.3f).normalized;
			}
			return new Vec3(normalized.x, normalized.y, lift).Normalized();
		}

		private static bool Hull(Actor ship, out MeshModel model, out Vec3 pos, out Mat3 basis, out float scale, bool sunk = false)
		{
			model = null;
			pos = default(Vec3);
			basis = Mat3.Identity;
			scale = 0.55f;
			try
			{
				if (ship == null || (!sunk && !ship.isAlive()))
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			string text = Models.BoatFamily(ship);
			model = ((text != null) ? Models.Get(text) : null);
			if (model == null)
			{
				return false;
			}
			MeshInstance meshInstance = Creatures.BoatPlaced(ship);
			if (meshInstance != null)
			{
				pos = meshInstance.Position;
				basis = meshInstance.Basis;
				scale = (float)Math.Sqrt(Vec3.Dot(basis.C2, basis.C2));
				return true;
			}
			float heading2;
			float heading = (Creatures.BoatHeading(ship, out heading2) ? heading2 : (ship.flip ? 0f : MathF.PI));
			Vector2 current_position = ship.current_position;
			Creatures.BoatPose(ship, current_position.x, current_position.y, heading, out pos, out basis, out scale);
			return true;
		}

		private void EndShot(CannonShot s, TerrainCache terrain, bool seen = false)
		{
			if (s.Ended)
			{
				return;
			}
			s.Ended = true;
			if (!s.IsBall || !s.Started)
			{
				return;
			}
			Vector2 obj = ((seen || s.Steered) ? s.LastGame : s.Aim);
			float num = obj.x;
			float num2 = obj.y;
			bool num3;
			if (!((obj - s.Aim).sqrMagnitude < 6.25f))
			{
				num3 = Wet(terrain, num, num2);
			}
			else
			{
				if (s.AimWet)
				{
					goto IL_0085;
				}
				num3 = Wet(terrain, num, num2);
			}
			if (num3)
			{
				goto IL_0085;
			}
			int num4 = 0;
			goto IL_0086;
			IL_0086:
			CannonImpactFx.Kind kind = (CannonImpactFx.Kind)num4;
			float num5 = terrain.LiquidSurface(num, num2) + terrain.WaveAt(num, num2);
			float z = ((kind == CannonImpactFx.Kind.Water) ? num5 : terrain.SampleHeight(num, num2));
			Actor actor = s.Target as Actor;
			if (actor == null || actor == s.Ship || !BoatHelm.IsBoat(actor))
			{
				actor = ((kind == CannonImpactFx.Kind.Water) ? Creatures.BoatNear(num, num2, 7f, s.Ship) : null);
			}
			if (kind == CannonImpactFx.Kind.Water && actor != null && Hull(actor, out var _, out var pos, out var _, out var scale, sunk: true))
			{
				float num6 = Boats.HullDistance(actor, new Vector2(num, num2));
				if (CannonTrace)
				{
					Log.Info(string.Format("CANNON strike near a ship {0}, {1:F2} tiles from her hull", (actor == s.Target) ? "aimed at" : "nearby", num6));
				}
				if (num6 < 1f)
				{
					kind = CannonImpactFx.Kind.Hull;
					Vector2 vector = new Vector2(s.First.X - pos.X, s.First.Y - pos.Y);
					vector = ((vector.sqrMagnitude > 0.0001f) ? vector.normalized : Vector2.zero);
					num = pos.X + vector.x * 1.6f * scale;
					num2 = pos.Y + vector.y * 1.6f * scale;
					z = pos.Z + 0.9f * scale;
				}
			}
			float dist = Mathf.Sqrt((num - _eyeX) * (num - _eyeX) + (num2 - _eyeY) * (num2 - _eyeY));
			CannonImpactFx item = new CannonImpactFx(num, num2, z, s.Size, s.Seed ^ 0x5BD1E995, kind, LandColor(terrain, num, num2), num5)
			{
				Detail = CannonDetail(dist)
			};
			CannonImpacts.Add(item);
			if (CannonImpacts.Count > 32)
			{
				CannonImpacts.RemoveAt(0);
			}
			CannonStrikes++;
			LastCannonStrike = $"{kind} at {num:F1},{num2:F1}";
			if (CannonTrace)
			{
				int num7 = Mathf.Clamp((int)num, 0, terrain.Width - 1);
				int num8 = Mathf.Clamp((int)num2, 0, terrain.Height - 1);
				string text = "?";
				try
				{
					text = World.world.GetTile(num7, num8).Type.id;
				}
				catch
				{
				}
				Log.Info(string.Format("CANNON strike {0} at {1:F1},{2:F1} ({3}, last seen {4:F1},{5:F1}, aimed {6:F1},{7:F1}): ", kind, num, num2, seen ? "seen" : "gone", s.LastGame.x, s.LastGame.y, s.Aim.x, s.Aim.y) + $"flags {terrain.Flags[num8 * terrain.Width + num7]}, tile {text}, cache {terrain.Width}x{terrain.Height}");
			}
			return;
			IL_0085:
			num4 = 1;
			goto IL_0086;
		}

		private bool CannonTakes(BaseEffect effect, bool slash)
		{
			if (Firings.Count == 0 && CannonImpacts.Count == 0 && _cannonShots.Count == 0)
			{
				return false;
			}
			Vector3 position = effect.transform.position;
			if (slash)
			{
				foreach (GunFiring firing in Firings)
				{
					if (!(firing.Fx.Age > 0.6f) && firing.Ship != null)
					{
						Vector2 current_position = firing.Ship.current_position;
						if ((current_position.x - position.x) * (current_position.x - position.x) + (current_position.y - position.y) * (current_position.y - position.y) < 12.25f)
						{
							CannonSpritesHidden++;
							return true;
						}
					}
				}
				return false;
			}
			foreach (CannonImpactFx cannonImpact in CannonImpacts)
			{
				if (cannonImpact.Age < 1f && (cannonImpact.X - position.x) * (cannonImpact.X - position.x) + (cannonImpact.Y - position.y) * (cannonImpact.Y - position.y) < 9f)
				{
					CannonSpritesHidden++;
					return true;
				}
			}
			foreach (CannonShot value in _cannonShots.Values)
			{
				if (value.IsBall && value.Started && value.Seen >= _frame - 2 && (value.LastGame.x - position.x) * (value.LastGame.x - position.x) + (value.LastGame.y - position.y) * (value.LastGame.y - position.y) < 9f)
				{
					CannonSpritesHidden++;
					return true;
				}
			}
			return false;
		}

		private void StepCannons(TerrainCache terrain, float dt)
		{
			if (_cannonShots.Count > 0)
			{
				_cannonGone.Clear();
				foreach (KeyValuePair<Projectile, CannonShot> cannonShot in _cannonShots)
				{
					CannonShot value = cannonShot.Value;
					if (!value.Ended && value.Seen != _frame)
					{
						EndShot(value, terrain);
					}
					if (_frame - value.Seen > 30)
					{
						_cannonGone.Add(cannonShot.Key);
					}
				}
				foreach (Projectile item in _cannonGone)
				{
					_cannonShots.Remove(item);
				}
			}
			FxSink blastSink = _blastSink;
			for (int num = Firings.Count - 1; num >= 0; num--)
			{
				GunFiring gunFiring = Firings[num];
				if (gunFiring.Fx.Age < 0.35f && Hull(gunFiring.Ship, out var _, out var pos, out var basis, out var _))
				{
					gunFiring.Fx.Muzzle = pos + basis.Mul(gunFiring.Port);
					gunFiring.Fx.Dir = GunDir(basis, gunFiring.Side, gunFiring.Toward, gunFiring.Lift);
				}
				gunFiring.Fx.Step(dt, blastSink, BlastParticles);
				if (gunFiring.Fx.Finished)
				{
					Firings.RemoveAt(num);
				}
			}
			for (int num2 = CannonImpacts.Count - 1; num2 >= 0; num2--)
			{
				CannonImpacts[num2].Step(dt, blastSink, BlastParticles);
				if (CannonImpacts[num2].Finished)
				{
					CannonImpacts.RemoveAt(num2);
				}
			}
			if (_frame % 120 != 0 || Recoils.Count <= 0)
			{
				return;
			}
			RecoilsDone.Clear();
			float time = Time.time;
			foreach (KeyValuePair<Actor, Recoil> recoil in Recoils)
			{
				if (time - recoil.Value.Last > 5f || recoil.Key == null)
				{
					RecoilsDone.Add(recoil.Key);
				}
			}
			foreach (Actor item2 in RecoilsDone)
			{
				Recoils.Remove(item2);
			}
		}

		private void ClearCannons()
		{
			_cannonShots.Clear();
			Firings.Clear();
			CannonImpacts.Clear();
			Recoils.Clear();
		}

		private List<Actor> UnitsToVisit(MapBox world, float unitRange, float shipRange, Actor host)
		{
			List<Actor> simpleList = world.units.getSimpleList();
			if (ChunkWalkOff || !ChunkWalkOn || FromSky || AllAround > 0f)
			{
				return simpleList;
			}
			MapChunkManager map_chunk_manager = world.map_chunk_manager;
			MapChunk[,] array = map_chunk_manager?._map;
			if (array == null)
			{
				return simpleList;
			}
			int get_amount_x = map_chunk_manager._get_amount_x;
			int amount_y = map_chunk_manager._amount_y;
			if (get_amount_x <= 0 || amount_y <= 0 || array.GetLength(0) < get_amount_x || array.GetLength(1) < amount_y)
			{
				return simpleList;
			}
			try
			{
				float num = Mathf.Max(unitRange, Mathf.Max(shipRange, Mathf.Max(FarActorRange, CrowdReach))) + 11.3144f;
				float num2 = num * num;
				float num3 = Mathf.Max(40f, 48f) + 2f;
				float num4 = num3 * num3;
				float num5 = 1444f;
				bool offscreenCheap = OffscreenCheap;
				float num6 = 11.3144f * (1f + _cullTan) + 2f;
				List<Actor> chunkUnits = _chunkUnits;
				chunkUnits.Clear();
				if (host != null)
				{
					chunkUnits.Add(host);
				}
				int num7 = Math.Max(0, (int)((_eyeX - num) / 16f));
				int num8 = Math.Min(get_amount_x - 1, (int)((_eyeX + num) / 16f));
				int num9 = Math.Max(0, (int)((_eyeY - num) / 16f));
				int num10 = Math.Min(amount_y - 1, (int)((_eyeY + num) / 16f));
				long num11 = 0L;
				for (int i = 0; i < amount_y; i++)
				{
					for (int j = 0; j < get_amount_x; j++)
					{
						MapChunk mapChunk = array[j, i];
						if (mapChunk == null)
						{
							continue;
						}
						List<Actor> units_all = mapChunk.objects.units_all;
						int count = units_all.Count;
						if (count == 0)
						{
							continue;
						}
						num11 += count;
						if (j < num7 || j > num8 || i < num9 || i > num10)
						{
							ChunksSkipped++;
							continue;
						}
						float num12 = (float)j * 16f + 8f;
						float num13 = (float)i * 16f + 8f;
						float num14 = num12 - _eyeX;
						float num15 = num13 - _eyeY;
						float num16 = Math.Max(0f, Math.Abs(num14) - 8f);
						float num17 = Math.Max(0f, Math.Abs(num15) - 8f);
						float num18 = num16 * num16 + num17 * num17;
						if (num18 > num2)
						{
							ChunksSkipped++;
							continue;
						}
						if (offscreenCheap && num18 > num4)
						{
							float num19 = Math.Max(0f, Math.Abs(num12 - _steerX) - 8f);
							float num20 = Math.Max(0f, Math.Abs(num13 - _steerY) - 8f);
							if (num19 * num19 + num20 * num20 > num5)
							{
								bool num21 = num14 * _vfx + num15 * _vfy + 11.3144f < -8f;
								float num22 = Mathf.Sqrt(num14 * num14 + num15 * num15);
								bool flag = !num21 && !InView(num14, num15, 4f + 0.3f * (num22 + 11.3144f) + num6);
								if (num21 || flag)
								{
									ChunksSkipped++;
									continue;
								}
							}
						}
						for (int k = 0; k < count; k++)
						{
							Actor actor = units_all[k];
							if (actor != null && actor != host && (actor.asset == null || !actor.asset.is_boat))
							{
								chunkUnits.Add(actor);
							}
						}
					}
				}
				if (num11 == 0L && simpleList.Count > 0)
				{
					return simpleList;
				}
				AddBoats(chunkUnits, host, shipRange * shipRange);
				ChunkUnitsVisited += chunkUnits.Count;
				if (ChunkCheck)
				{
					CheckAgainst(simpleList, chunkUnits);
				}
				return chunkUnits;
			}
			catch (Exception ex)
			{
				ChunkWalkOff = true;
				Log.Error("chunk walk: " + ex);
				return simpleList;
			}
		}

		private void CheckAgainst(List<Actor> all, List<Actor> list)
		{
			for (int i = 0; i < all.Count; i++)
			{
				Actor actor = all[i];
				if (actor == null || !actor.isAlive())
				{
					continue;
				}
				if (list.Contains(actor))
				{
					if (_chunkCheckMissedAt.TryGetValue(actor, out var value))
					{
						Log.Info($"chunk walk found {WorldBoxMod.Describe(actor)} after {Time.realtimeSinceStartup - value:F2} s");
						_chunkCheckMissedAt.Remove(actor);
					}
				}
				else
				{
					if (_chunkCheckLogged.Contains(actor))
					{
						continue;
					}
					Vector2 current_position = actor.current_position;
					float num = current_position.x - _eyeX;
					float num2 = current_position.y - _eyeY;
					if (num * num + num2 * num2 > 1600f)
					{
						continue;
					}
					_chunkCheckLogged.Add(actor);
					_chunkCheckMissedAt[actor] = Time.realtimeSinceStartup;
					string text = "in no chunk list";
					string text2 = "";
					try
					{
						text2 = $", zone timer {World.world.sim_object_zones._timer:F3} (delta_time {World.world.delta_time:F4}, elapsed {World.world.elapsed:F4}), units {World.world.units.Count}";
					}
					catch
					{
					}
					try
					{
						MapChunk[,] map = World.world.map_chunk_manager._map;
						for (int j = 0; j < map.GetLength(1); j++)
						{
							if (!text.StartsWith("in no"))
							{
								break;
							}
							for (int k = 0; k < map.GetLength(0); k++)
							{
								if (map[k, j] != null && map[k, j].objects.units_all.Contains(actor))
								{
									text = $"in chunk {k},{j}'s list";
									break;
								}
							}
						}
					}
					catch
					{
					}
					Log.Info(string.Format("chunk walk missed {0} at {1:F1},{2:F1} ({3:F1} tiles from the eye at {4:F1},{5:F1}, tile {6}, chunk {7}): {8}; eye chunk {9},{10}, inside {11}, moving {12}{13}", WorldBoxMod.Describe(actor), current_position.x, current_position.y, Mathf.Sqrt(num * num + num2 * num2), _eyeX, _eyeY, (actor.current_tile != null) ? (actor.current_tile.x + "," + actor.current_tile.y) : "none", (actor.current_tile?.chunk != null) ? (actor.current_tile.chunk.x + "," + actor.current_tile.chunk.y) : "none", text, (int)(_eyeX / 16f), (int)(_eyeY / 16f), actor.is_inside_building, actor.is_moving, text2));
				}
			}
		}

		private void AddBoats(List<Actor> list, Actor host, float reach2)
		{
			if (_boatKinds == null)
			{
				_boatKinds = new List<ActorAsset>();
				List<ActorAsset> list2 = AssetManager.actor_library?.list;
				if (list2 != null)
				{
					for (int i = 0; i < list2.Count; i++)
					{
						if (list2[i] != null && list2[i].is_boat)
						{
							_boatKinds.Add(list2[i]);
						}
					}
				}
			}
			for (int j = 0; j < _boatKinds.Count; j++)
			{
				HashSet<Actor> units = _boatKinds[j].units;
				if (units == null || units.Count == 0)
				{
					continue;
				}
				foreach (Actor item in units)
				{
					if (item != null && item != host)
					{
						Vector2 current_position = item.current_position;
						float num = current_position.x - _eyeX;
						float num2 = current_position.y - _eyeY;
						if (num * num + num2 * num2 <= reach2)
						{
							list.Add(item);
						}
					}
				}
			}
		}

		public void Clods(Vec3 at, Vector2 you, uint color, int count, bool fill)
		{
			ClodsThrown++;
			if (_terrain == null || new Vector2(at.X - _eyeX, at.Y - _eyeY).sqrMagnitude > 1600f)
			{
				return;
			}
			Vector2 vector = new Vector2(at.X - you.x, at.Y - you.y);
			vector = ((vector.sqrMagnitude > 1E-06f) ? vector.normalized : new Vector2(0f, 1f));
			Vec3 vec = new Vec3(vector.x, vector.y, 0f);
			Vec3 vec2 = new Vec3(vector.y, 0f - vector.x, 0f);
			for (int i = 0; i < count; i++)
			{
				if (_chips.Count >= 80)
				{
					break;
				}
				float num = (float)_chipRandom.NextDouble();
				float num2 = (float)_chipRandom.NextDouble();
				float num3 = (float)_chipRandom.NextDouble();
				Vec3 p;
				Vec3 v;
				if (fill)
				{
					p = at - vec * (0.25f + 0.15f * num) + vec2 * ((num2 - 0.5f) * 0.3f) + new Vec3(0f, 0f, 0.45f + 0.2f * num3);
					v = vec * (0.4f + 0.4f * num2) + new Vec3(0f, 0f, 0.2f * num);
				}
				else
				{
					p = at + new Vec3(0f, 0f, 0.12f) + vec * ((num3 - 0.5f) * 0.2f);
					v = vec2 * (0.8f + 0.7f * num) + vec * ((num2 - 0.3f) * 0.6f) + new Vec3(0f, 0f, 1.7f + 0.8f * num3);
				}
				_chips.Add(new Chip
				{
					P = p,
					V = v,
					Life = 2.2f + 0.6f * num2,
					Size = 0.04f + 0.035f * num,
					Spin = (float)_chipRandom.NextDouble() * 5f,
					Shape = ChipShape(Vary(color)),
					Crumble = true
				});
			}
		}

		private void Crumbs(Chip c, float ground)
		{
			int num = 3 + _chipRandom.Next(2);
			for (int i = 0; i < num; i++)
			{
				if (_chips.Count >= 90)
				{
					break;
				}
				float f = (float)_chipRandom.NextDouble() * 6.283f;
				float num2 = 0.3f + 0.5f * (float)_chipRandom.NextDouble();
				_chips.Add(new Chip
				{
					P = new Vec3(c.P.X, c.P.Y, ground + 0.01f),
					V = new Vec3(Mathf.Cos(f) * num2 + c.V.X * 0.2f, Mathf.Sin(f) * num2 + c.V.Y * 0.2f, 0.5f + 0.6f * (float)_chipRandom.NextDouble()),
					Life = 0.7f + 0.5f * (float)_chipRandom.NextDouble(),
					Size = c.Size * (0.35f + 0.2f * (float)_chipRandom.NextDouble()),
					Spin = (float)_chipRandom.NextDouble() * 6f,
					Shape = c.Shape
				});
			}
		}

		private uint Vary(uint color)
		{
			if (color == 0)
			{
				color = 0xFF000000u | TerrainCache.Rgb(112, 82, 56);
			}
			float num = 0.82f + 0.24f * (float)_chipRandom.NextDouble();
			int num2 = Mathf.Clamp((int)((float)(color & 0xFF) * num), 0, 255);
			int num3 = Mathf.Clamp((int)((float)((color >> 8) & 0xFF) * num), 0, 255);
			int num4 = Mathf.Clamp((int)((float)((color >> 16) & 0xFF) * num), 0, 255);
			return (uint)(-16777216 | ((num2 & 0xF8) | ((num3 & 0xF8) << 8) | ((num4 & 0xF8) << 16)));
		}

		private bool DrawnOk(Actor a, Heading track)
		{
			if (!float.IsNaN(track.Drawn.x) && !float.IsNaN(track.Drawn.y) && !float.IsInfinity(track.Drawn.x) && !float.IsInfinity(track.Drawn.y))
			{
				return true;
			}
			_headings.Remove(a);
			if (!_drawnBadLogged)
			{
				_drawnBadLogged = true;
				string arg = "?";
				try
				{
					arg = ((a.asset != null) ? a.asset.id : "?");
				}
				catch
				{
				}
				Log.Info($"creatures: a {arg}'s smoothing gave no place (speed {track.Speed}, facing {track.Angle}); started again");
			}
			return false;
		}

		private bool CoastUnit(Actor a, Actor host, TerrainCache terrain, float dt)
		{
			if (CrowdLine <= 0f || !FromSky || a == host || a == Skip)
			{
				return false;
			}
			if (!_headings.TryGetValue(a, out var value) || value.SeenFrame != _frame - 1 || value.Inside > 0 || value.Looking)
			{
				return false;
			}
			if (RecentHits.ContainsKey(a))
			{
				return false;
			}
			float num = WorldStep(dt);
			Vector2 drawn = value.Drawn;
			float num2 = Mathf.Min(value.Speed, 10f);
			if (num2 > 0f && num > 0f)
			{
				drawn += new Vector2(Mathf.Cos(value.Angle), Mathf.Sin(value.Angle)) * (num2 * Mathf.Min(num, 0.1f));
			}
			if (!(drawn.x >= 0f) || !(drawn.y >= 0f) || !(drawn.x < (float)terrain.Width) || !(drawn.y < (float)terrain.Height) || !(num2 >= 0f) || float.IsNaN(value.Angle))
			{
				if (!_coastBadLogged)
				{
					_coastBadLogged = true;
					Log.Info($"coast: left alone, its smoothing was off ({value.Drawn.x},{value.Drawn.y} at {value.Speed} a second, facing {value.Angle}, step {num})");
				}
				return false;
			}
			if (!People.Coast(a, drawn.x, drawn.y, terrain, value.Angle, num2, num, out var inst, out var z, out var body))
			{
				return false;
			}
			value.Drawn = drawn;
			value.SeenFrame = _frame;
			value.Dt = num;
			value.Body.Reset(drawn.x, drawn.y, value.Angle);
			inst.Tag = Tagged.Count;
			Tagged.Add(a);
			Hitboxes.RecordSprite(a, drawn.x, drawn.y, z, body * 0.35f, body, value.Angle);
			return true;
		}

		private void AddCrabzilla(Actor a, ViewState view, Actor host, TerrainCache terrain, float dt)
		{
			if (_crabFrame != _frame)
			{
				_crabFrame = _frame;
				CrabzillaBeam.Begin();
				Crabzillas.Tick();
			}
			bool flag = a == host;
			float num = a.current_position.x - view.X;
			float num2 = a.current_position.y - view.Y;
			if (flag || (!(num * num + num2 * num2 > 48400f) && InView(num, num2, 20f)))
			{
				int tag = (flag ? 2147483646 : Tagged.Count);
				if (Crabzillas.Add(Meshes, Billboards, Lights, a, view, terrain, dt, flag, tag, _clock) && !flag)
				{
					Tagged.Add(a);
				}
			}
		}

		private void NoteDrawn(Heading h)
		{
			if (h.Size < 1.6f && _walkersNow.Count < 256)
			{
				_walkersNow.Add(h);
			}
		}

		private void SeparateDrawn(float dt)
		{
			List<Heading> walkersNow = _walkersNow;
			_walkersNow = _walkersLast;
			_walkersNow.Clear();
			_walkersLast = walkersNow;
			if (!(dt <= 0f) && walkersNow.Count >= 2)
			{
				if (GroundMemo)
				{
					NewGroundRound();
				}
				if (!CrowdFast)
				{
					SeparateOld(walkersNow, dt);
				}
				else if (CrowdVerify)
				{
					SeparateVerified(walkersNow, dt);
				}
				else
				{
					SeparateFast(walkersNow, dt);
				}
			}
		}

		private void SeparateOld(List<Heading> list, float dt)
		{
			for (int i = 0; i < list.Count; i++)
			{
				Heading heading = list[i];
				for (int j = i + 1; j < list.Count; j++)
				{
					Heading heading2 = list[j];
					if (!heading.Moving && !heading2.Moving)
					{
						continue;
					}
					float num = 0.3f * Mathf.Max(1f, 0.5f * (heading.Size + heading2.Size));
					float num2 = heading2.Body.X - heading.Body.X;
					float num3 = heading2.Body.Y - heading.Body.Y;
					float num4 = num2 * num2 + num3 * num3;
					float num5 = num + (heading.Speed + heading2.Speed) * 0.6f;
					if (num4 > num5 * num5)
					{
						continue;
					}
					float num6 = Mathf.Sqrt(num4);
					float num7 = ((!heading.Moving) ? 0f : ((!heading2.Moving) ? 1f : 0.5f));
					float num8 = 1f - num7;
					if (num6 < num)
					{
						float num9;
						float num10;
						if (num6 > 0.0001f)
						{
							num9 = num2 / num6;
							num10 = num3 / num6;
						}
						else
						{
							num9 = 0f - Mathf.Sin(heading.Body.Facing);
							num10 = Mathf.Cos(heading.Body.Facing);
						}
						float num11 = Mathf.Min(num - num6, 2.5f * dt);
						Nudge(heading, (0f - num9) * num11 * num7, (0f - num10) * num11 * num7);
						Nudge(heading2, num9 * num11 * num8, num10 * num11 * num8);
						continue;
					}
					float num12 = Mathf.Cos(heading.Body.Facing) * heading.Speed;
					float num13 = Mathf.Sin(heading.Body.Facing) * heading.Speed;
					float num14 = Mathf.Cos(heading2.Body.Facing) * heading2.Speed;
					float num15 = Mathf.Sin(heading2.Body.Facing) * heading2.Speed;
					float num16 = num14 - num12;
					float num17 = num15 - num13;
					float num18 = num16 * num16 + num17 * num17;
					if (num18 < 0.0001f)
					{
						continue;
					}
					float num19 = (0f - (num2 * num16 + num3 * num17)) / num18;
					if (num19 <= 0f || num19 > 0.6f)
					{
						continue;
					}
					float num20 = num2 + num16 * num19;
					float num21 = num3 + num17 * num19;
					float num22 = Mathf.Sqrt(num20 * num20 + num21 * num21);
					if (!(num22 >= num))
					{
						float num23 = 1f - num19 / 0.6f;
						float k = Mathf.Min(0.05f, 1.6f * dt) * (0.4f + num23) * (1f - num22 / num);
						if (heading.Moving)
						{
							Aside(heading, num20, num21, k);
						}
						if (heading2.Moving)
						{
							Aside(heading2, 0f - num20, 0f - num21, k);
						}
					}
				}
			}
		}

		private void SeparateFast(List<Heading> list, float dt)
		{
			int count = list.Count;
			if (_sepX.Length < count)
			{
				_sepX = new double[count * 2];
				_sepY = new double[count * 2];
				_sepS = new double[count * 2];
				_sepMoving = new bool[count * 2];
			}
			double num = 0.0;
			double num2 = 1.0;
			double num3 = 0.0;
			for (int i = 0; i < count; i++)
			{
				Heading heading = list[i];
				float x = heading.Body.X;
				float y = heading.Body.Y;
				float speed = heading.Speed;
				float size = heading.Size;
				if (!Plain(x) || !Plain(y) || !Plain(size) || !Plain(heading.Body.Facing) || !Plain(speed) || speed < 0f)
				{
					SeparateOld(list, dt);
					return;
				}
				_sepX[i] = x;
				_sepY[i] = y;
				_sepS[i] = (double)speed * 0.6000000238418579;
				_sepMoving[i] = heading.Moving;
				num = Math.Max(num, Math.Max(Math.Abs((double)x), Math.Abs((double)y)));
				num2 = Math.Max(num2, size);
				num3 = Math.Max(num3, _sepS[i]);
			}
			if (num > 1000000.0 || num3 > 1000000.0)
			{
				SeparateOld(list, dt);
				return;
			}
			_sepMoved = 0.0;
			_sepRound = 2E-07 * (num + 1.0);
			double num4 = 0.30000001192092896 * num2 + 0.01 + 1E-06 * num;
			for (int j = 0; j < count; j++)
			{
				bool flag = _sepMoving[j];
				double num5 = _sepX[j];
				double num6 = _sepY[j];
				double num7 = num4 + _sepS[j];
				double num8 = -1.0;
				double num9 = 0.0;
				Heading heading2 = list[j];
				for (int k = j + 1; k < count; k++)
				{
					if (num8 != _sepMoved)
					{
						num8 = _sepMoved;
						num9 = num7 + num3 + 2.0 * num8;
					}
					double num10 = _sepX[k] - num5;
					if (num10 > num9 || 0.0 - num10 > num9)
					{
						continue;
					}
					bool flag2 = _sepMoving[k];
					if (!flag && !flag2)
					{
						continue;
					}
					double num11 = num7 + _sepS[k] + 2.0 * num8;
					double num12 = _sepY[k] - num6;
					if (num10 > num11 || 0.0 - num10 > num11 || num12 > num11 || 0.0 - num12 > num11)
					{
						continue;
					}
					Heading heading3 = list[k];
					float num13 = 0.3f * Mathf.Max(1f, 0.5f * (heading2.Size + heading3.Size));
					float num14 = heading3.Body.X - heading2.Body.X;
					float num15 = heading3.Body.Y - heading2.Body.Y;
					float num16 = num14 * num14 + num15 * num15;
					float num17 = num13 + (heading2.Speed + heading3.Speed) * 0.6f;
					if (num16 > num17 * num17)
					{
						continue;
					}
					float num18 = Mathf.Sqrt(num16);
					float num19 = ((!flag) ? 0f : ((!flag2) ? 1f : 0.5f));
					float num20 = 1f - num19;
					if (num18 < num13)
					{
						float num21;
						float num22;
						if (num18 > 0.0001f)
						{
							num21 = num14 / num18;
							num22 = num15 / num18;
						}
						else
						{
							num21 = 0f - Mathf.Sin(heading2.Body.Facing);
							num22 = Mathf.Cos(heading2.Body.Facing);
						}
						float num23 = Mathf.Min(num13 - num18, 2.5f * dt);
						NudgeCounted(heading2, (0f - num21) * num23 * num19, (0f - num22) * num23 * num19);
						NudgeCounted(heading3, num21 * num23 * num20, num22 * num23 * num20);
						continue;
					}
					float num24 = Mathf.Cos(heading2.Body.Facing) * heading2.Speed;
					float num25 = Mathf.Sin(heading2.Body.Facing) * heading2.Speed;
					float num26 = Mathf.Cos(heading3.Body.Facing) * heading3.Speed;
					float num27 = Mathf.Sin(heading3.Body.Facing) * heading3.Speed;
					float num28 = num26 - num24;
					float num29 = num27 - num25;
					float num30 = num28 * num28 + num29 * num29;
					if (num30 < 0.0001f)
					{
						continue;
					}
					float num31 = (0f - (num14 * num28 + num15 * num29)) / num30;
					if (num31 <= 0f || num31 > 0.6f)
					{
						continue;
					}
					float num32 = num14 + num28 * num31;
					float num33 = num15 + num29 * num31;
					float num34 = Mathf.Sqrt(num32 * num32 + num33 * num33);
					if (!(num34 >= num13))
					{
						float num35 = 1f - num31 / 0.6f;
						float k2 = Mathf.Min(0.05f, 1.6f * dt) * (0.4f + num35) * (1f - num34 / num13);
						if (flag)
						{
							AsideCounted(heading2, num32, num33, k2);
						}
						if (flag2)
						{
							AsideCounted(heading3, 0f - num32, 0f - num33, k2);
						}
					}
				}
			}
		}

		private static bool Plain(float v)
		{
			if (!float.IsNaN(v))
			{
				return !float.IsInfinity(v);
			}
			return false;
		}

		private void AsideCounted(Heading h, float towardX, float towardY, float k)
		{
			float num = Mathf.Sin(h.Body.Facing);
			float num2 = 0f - Mathf.Cos(h.Body.Facing);
			float num3 = num * towardX + num2 * towardY;
			float num4 = ((Mathf.Abs(num3) < 0.03f) ? 1f : ((num3 > 0f) ? (-1f) : 1f));
			NudgeCounted(h, num * num4 * k, num2 * num4 * k);
		}

		private void NudgeCounted(Heading h, float dx, float dy)
		{
			_sepMoved += Math.Abs((double)dx) + Math.Abs((double)dy) + _sepRound;
			if (dx == 0f && dy == 0f)
			{
				return;
			}
			float num = h.Body.X + dx;
			float num2 = h.Body.Y + dy;
			try
			{
				if (h.Blocked != null && h.Blocked(num, num2) && !h.Blocked(h.Body.X, h.Body.Y))
				{
					return;
				}
			}
			catch
			{
				return;
			}
			h.Body.X = num;
			h.Body.Y = num2;
		}

		private void SeparateVerified(List<Heading> list, float dt)
		{
			int count = list.Count;
			if (_sepCheck.Length < count * 4)
			{
				_sepCheck = new float[count * 8];
			}
			float[] sepCheck = _sepCheck;
			for (int i = 0; i < count; i++)
			{
				sepCheck[i * 4] = list[i].Body.X;
				sepCheck[i * 4 + 1] = list[i].Body.Y;
			}
			SeparateFast(list, dt);
			for (int j = 0; j < count; j++)
			{
				sepCheck[j * 4 + 2] = list[j].Body.X;
				sepCheck[j * 4 + 3] = list[j].Body.Y;
			}
			for (int num = count - 1; num >= 0; num--)
			{
				list[num].Body.X = sepCheck[num * 4];
				list[num].Body.Y = sepCheck[num * 4 + 1];
			}
			SeparateOld(list, dt);
			CrowdVerified++;
			for (int k = 0; k < count; k++)
			{
				Heading heading = list[k];
				if (NpcTune.Bits(heading.Body.X) != NpcTune.Bits(sepCheck[k * 4 + 2]) || NpcTune.Bits(heading.Body.Y) != NpcTune.Bits(sepCheck[k * 4 + 3]))
				{
					CrowdMismatched++;
					if (CrowdFirstMismatch == null)
					{
						CrowdFirstMismatch = $"walker {k} of {count}: the quick way put it at {sepCheck[k * 4 + 2]:R},{sepCheck[k * 4 + 3]:R}, the old way at {heading.Body.X:R},{heading.Body.Y:R} (from {sepCheck[k * 4]:R},{sepCheck[k * 4 + 1]:R}, dt {dt:R})";
					}
					break;
				}
			}
		}

		private static void Aside(Heading h, float towardX, float towardY, float k)
		{
			float num = Mathf.Sin(h.Body.Facing);
			float num2 = 0f - Mathf.Cos(h.Body.Facing);
			float num3 = num * towardX + num2 * towardY;
			float num4 = ((Mathf.Abs(num3) < 0.03f) ? 1f : ((num3 > 0f) ? (-1f) : 1f));
			Nudge(h, num * num4 * k, num2 * num4 * k);
		}

		private static void Nudge(Heading h, float dx, float dy)
		{
			if (dx == 0f && dy == 0f)
			{
				return;
			}
			float num = h.Body.X + dx;
			float num2 = h.Body.Y + dy;
			try
			{
				if (h.Blocked != null && h.Blocked(num, num2) && !h.Blocked(h.Body.X, h.Body.Y))
				{
					return;
				}
			}
			catch
			{
				return;
			}
			h.Body.X = num;
			h.Body.Y = num2;
		}

		private bool CrowdBeyond(Actor a, float dist2)
		{
			if (CrowdLine > 0f && CrowdReach > 0f && dist2 < CrowdReach * CrowdReach && Models.Ready)
			{
				return People.IsHumanoid(a);
			}
			return false;
		}

		private Heading TrackLite(Actor a, Vector2 p, float dt)
		{
			if (!_headings.TryGetValue(a, out var value) || value.SeenFrame < _frame - 2 || value.Inside > 0 || a.is_inside_building || Steering.Hidden(a))
			{
				return Track(a, p, dt, smooth: true);
			}
			if (value.SeenFrame == _frame)
			{
				return value;
			}
			float num = WorldStep(dt);
			Vector2 drawn = value.Drawn;
			value.Drawn = drawn + (p - drawn) * (1f - Mathf.Exp((0f - num) * 10f));
			Vector2 vector = p - value.Drawn;
			if (vector.sqrMagnitude > 1f)
			{
				value.Drawn = p - vector.normalized;
			}
			Vector2 vector2 = value.Drawn - drawn;
			value.Speed = ((num > 1E-05f) ? (vector2.magnitude / num) : 0f);
			if (vector2.sqrMagnitude > 1E-06f && num > 0f)
			{
				value.Angle = Turn.Damp(value.Angle, ref value.TurnRate, Mathf.Atan2(vector2.y, vector2.x), num, 0.2f, MathF.PI * 2f);
			}
			value.Body.Reset(value.Drawn.x, value.Drawn.y, value.Angle);
			value.Last = p;
			value.SeenFrame = _frame;
			value.Dt = num;
			value.Looking = false;
			value.Blocked = null;
			return value;
		}

		private SpriteImage CardImage(Actor a)
		{
			if (!_cardImages.TryGetValue(a, out var value) || _frame - value.Frame >= 20)
			{
				return null;
			}
			return value.Image;
		}

		private void KeepCardImage(Actor a, SpriteImage image)
		{
			if (_cardImages.Count > 20000)
			{
				_cardImages.Clear();
			}
			_cardImages[a] = new KeptImage
			{
				Image = image,
				Frame = _frame + (a.GetHashCode() & 7) - 7
			};
		}

		private void FarCreature(Actor a, Vector2 p, float dist2, TerrainCache terrain, SpriteCache sprites)
		{
			if (!FarOutlines || FarOutlinesOffForTest || CrowdReach <= 0f || !FromSky || dist2 >= CrowdReach * CrowdReach || a.asset == null || a.asset.is_boat)
			{
				return;
			}
			float num = terrain.SampleHeight(p.x, p.y);
			float num2 = 0f;
			try
			{
				num2 = Mathf.Max(0f, a.position_height);
			}
			catch
			{
			}
			if (OutsideGodFrame(p.x, p.y, num, 3f + num2, 16f))
			{
				return;
			}
			if (!_outlineLooks.TryGetValue(a.asset, out var value))
			{
				SpriteImage spriteImage;
				try
				{
					spriteImage = sprites.Get(a.getSpriteToRender());
				}
				catch
				{
					return;
				}
				if (spriteImage == null)
				{
					return;
				}
				float grow;
				float lift;
				FarLook look = FarOutline.Of(spriteImage, out grow, out lift);
				value = new OutlineLook
				{
					Look = look,
					Grow = grow,
					Lift = lift,
					Width = spriteImage.UnitWidth,
					Height = spriteImage.UnitHeight * spriteImage.VisibleFraction,
					Color = AverageColor(spriteImage)
				};
				if (_outlineLooks.Count > 400)
				{
					_outlineLooks.Clear();
				}
				_outlineLooks[a.asset] = value;
			}
			if (value.Look != null)
			{
				Vector3 current_scale;
				try
				{
					current_scale = a.current_scale;
				}
				catch
				{
					return;
				}
				float num3 = value.Height * Mathf.Abs(current_scale.y);
				float num4 = value.Width * Mathf.Abs(current_scale.x);
				float num5 = SkyLeast(p.x, p.y, 7f);
				if (num3 * value.Grow < num5 && num3 > 0.0001f)
				{
					float num6 = num5 / (num3 * value.Grow);
					num4 *= num6;
					num3 *= num6;
				}
				float num7 = (IsSwimming(a) ? terrain.LiquidSurface(p.x, p.y) : (num + num2));
				_farOutlines.Add(new FarProp
				{
					X = p.x,
					Y = p.y,
					Z = num7 + value.Lift * num3,
					Width = num4,
					Height = num3 * value.Grow,
					Color = value.Color,
					Tree = false,
					Look = value.Look
				});
				FarOutlinesDrawn++;
			}
		}

		private void EndCrowd()
		{
			if (_farOutlines.Count > 0)
			{
				FarProps.AddRange(_farOutlines);
			}
			_farOutlines.Clear();
		}

		private void BeginCrowd()
		{
			Crowd.Clear();
			_farOutlines.Clear();
			People.BeginCrowd(Crowd, CrowdLine, CrowdGear, CrowdPixel, _eyeX, _eyeY, _vfx, _vfy);
		}

		public void Reset()
		{
			Corpses.Clear();
			StuckArrows.Clear();
			_buildings.Clear();
			_headings.Clear();
			Colliders.Near.Clear();
			Steering.Reset();
			Hitboxes.Clear();
			_lastScanAt = new Vector2(float.MinValue, float.MinValue);
			ForgetFarRing();
		}

		public void Collect(ViewState view, float range, Actor host, WorldSampler sampler, SpriteCache sprites, float dt, Atmosphere air)
		{
			SceneClock.Begin();
			Billboards.Clear();
			Voxels.Clear();
			Meshes.Clear();
			Tagged.Clear();
			Lights.Clear();
			FarActors.Clear();
			UnitTagEnd = 0;
			_eyeX = view.X;
			_eyeY = view.Y;
			_eyeZ = view.Z;
			_vfx = Mathf.Cos(view.Yaw);
			_vfy = Mathf.Sin(view.Yaw);
			float aspect = (float)Screen.height / (float)Mathf.Max(1, Screen.width);
			Lens lens = Lens.Of(in view, aspect);
			float num = Mathf.Tan(view.Fov * 0.5f) * 1.08f;
			_cullTan = num * lens.Cq;
			float b = ((lens.Sq < 0f) ? (view.Z - GodCamera.LowestGround) : ((lens.Sq > 0f) ? (sampler.MaxHeight + 12f - view.Z) : 0f));
			_cullSpan = num * Mathf.Abs(lens.Sq) * Mathf.Max(0f, b);
			_cullWide = lens.WidestTan(view.Fov, aspect) * 1.08f;
			_cullBack = AllAround;
			if (AllAround > 0f)
			{
				_cullTan = 12f;
				_cullSpan = Mathf.Max(_cullSpan, AllAround);
				_cullWide = 12f;
			}
			Colliders.Near.Clear();
			Colliders.Near.AddRange(Colliders.ExtraForTest);
			Colliders.BeginFrame();
			BeginGlide(view);
			BeginSimLod(view, sampler.Cache);
			TreesForTest?.Clear();
			Vector2 vector = SteerAt ?? new Vector2(view.X, view.Y);
			_steerX = vector.x;
			_steerY = vector.y;
			Steering.Begin(vector.x, vector.y, PossessionHooks.Active ? host : null, PossessionHooks.BodyRadius);
			_frame++;
			SceneClock.Stage(SceneClock.B.Crowd);
			SeparateDrawn(WorldStep(dt));
			_night = air?.Night ?? 0f;
			_air = air;
			_clock += dt;
			SceneClock.Stage(SceneClock.B.Ticks);
			People.Tick();
			Creatures.Tick();
			MapBox world = World.world;
			if (world == null)
			{
				return;
			}
			SceneClock.Stage(SceneClock.B.Begin);
			BeginWeather(dt);
			BeginTowns(view, sampler.Cache, sprites, host, dt);
			AddWallColliders(view, sampler.Cache);
			CollectWalls(view, sampler.Cache, world);
			CollectHits(world);
			_frameDt = dt;
			BeginBlasts(view, sampler.Cache);
			SceneClock.Stage(SceneClock.B.Units);
			BeginCrowd();
			if (!SkipUnitsForTest)
			{
				CollectUnits(world, view, range, host, sampler.Cache, sprites, dt);
			}
			SceneClock.UnitsDone(FarActors.Count);
			UnitTagEnd = Tagged.Count;
			AddDragonFire(sampler.Cache);
			StepDragonDust(view, sampler.Cache, dt);
			StepUfos(view, range, sampler.Cache, dt);
			SceneClock.Stage(SceneClock.B.Buildings);
			if (!SkipBuildingsForTest)
			{
				CollectBuildings(view, range, sampler.Cache, sprites);
			}
			SceneClock.Stage(SceneClock.B.FarProps);
			CollectFarProps(view, range, sampler.Cache, sprites);
			SceneClock.Stage(SceneClock.B.Fx);
			StepWind(view, sampler.Cache, dt);
			StepBiomes(view, sampler.Cache, dt);
			if (Tufts)
			{
				GrassTufts.Add(Billboards, sampler.Cache, view.X, view.Y, _vfx, _vfy, _cullWide);
			}
			if (FromSky)
			{
				FireField.Add(Billboards, sampler.Cache, SkyFocus.x, SkyFocus.y, 0f, 0f, 1f, _clock, (Detail > 0.7f) ? 480 : 260, Mathf.Clamp(SkyReach * 0.6f, 40f, 160f));
			}
			else
			{
				FireField.Add(Billboards, sampler.Cache, view.X, view.Y, _vfx, _vfy, _cullWide, _clock, (Detail > 0.7f) ? 480 : 260);
			}
			SceneClock.Stage(SceneClock.B.Rest);
			CollectProjectiles(world, view, range, sampler.Cache, sprites);
			CollectEffects(world, view, sampler.Cache, sprites);
			FinishGodHands(world, view, sampler.Cache, dt);
			StepBlasts(world, sampler.Cache, dt);
			try
			{
				CollectDrops(world, view, sampler.Cache, sprites);
			}
			catch (Exception ex)
			{
				if (_reportedAssets.Add("drops"))
				{
					Log.Error("drops skipped: " + ex.Message);
				}
			}
			CollectWorkEffects(view, sampler.Cache, dt);
			StepCrumbles(view, dt);
			CollectCrops(view, dt);
			CollectBoards(view, sampler.Cache, sprites);
			CollectTowns();
			ForgetFlights();
			ForgetBombs();
			Corpses.Draw(Meshes, dt, view.X, view.Y);
			CollectVolcanoes(view, sampler, dt);
			CollectTileLights(view, sampler.Cache);
			CollectFireflies(view, sampler.Cache, dt);
			TrimLights(view);
			StepSnow(sampler.Cache, dt);
			EndCrowd();
			if (_frame % 120 == 0)
			{
				_staleHeadings.Clear();
				foreach (KeyValuePair<Actor, Heading> heading in _headings)
				{
					if (_frame - heading.Value.SeenFrame > 120)
					{
						_staleHeadings.Add(heading.Key);
					}
				}
				foreach (Actor staleHeading in _staleHeadings)
				{
					_headings.Remove(staleHeading);
				}
			}
			SceneClock.End();
		}

		private void AddDragonFire(TerrainCache terrain)
		{
			for (int i = 0; i < Creatures.Breaths.Count; i++)
			{
				Creatures.Fire fire = Creatures.Breaths[i];
				DragonBreath.Emit(Billboards, Lights, fire.Mouth, fire.Dir, fire.Reach, fire.Width, fire.Strength, _clock, fire.Seed, (float x, float y) => terrain.SampleHeight(x, y));
			}
			Creatures.Breaths.Clear();
		}

		private static void AddWallColliders(ViewState view, TerrainCache terrain)
		{
			int num = (int)view.X;
			int num2 = (int)view.Y;
			int num3 = 5;
			for (int i = num2 - num3; i <= num2 + num3; i++)
			{
				for (int j = num - num3; j <= num + num3; j++)
				{
					if (j >= 0 && i >= 0 && j < terrain.Width && i < terrain.Height && (terrain.Flags[i * terrain.Width + j] & 8) != 0)
					{
						Colliders.Near.Add(new Collider
						{
							Box = true,
							X = (float)j + 0.5f,
							Y = (float)i + 0.5f,
							HalfW = 0.5f,
							HalfD = 0.5f
						});
					}
				}
			}
		}

		public static bool IsSwimming(Actor a)
		{
			try
			{
				if (!a.isAffectedByLiquid() || (a.asset != null && a.asset.is_boat))
				{
					return false;
				}
				if (a.position_height < 0.3f && DockDecks.On(a.current_position))
				{
					return false;
				}
				return !Wading.Stands(a);
			}
			catch
			{
				return false;
			}
		}

		private void CollectHits(MapBox world)
		{
			RecentHits.Clear();
			List<ActorDamageEffectData> actor_effect_hit = world.stack_effects.actor_effect_hit;
			for (int i = 0; i < actor_effect_hit.Count; i++)
			{
				ActorDamageEffectData actorDamageEffectData = actor_effect_hit[i];
				if (actorDamageEffectData.actor != null)
				{
					float num = world.getRealTimeElapsedSince(actorDamageEffectData.timestamp);
					if (!(num < 0f) && !(num > 0.3f) && (!RecentHits.TryGetValue(actorDamageEffectData.actor, out var value) || num < value))
					{
						RecentHits[actorDamageEffectData.actor] = num;
					}
				}
			}
		}

		private void CollectUnits(MapBox world, ViewState view, float range, Actor host, TerrainCache terrain, SpriteCache sprites, float dt)
		{
			ResetOwnBody();
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			bool flag = num2 < 0f;
			float num3 = Mathf.Max(range, CreatureRange);
			float num4 = num3 * num3;
			float num5 = Mathf.Max(num3, Mathf.Max(FarPropRange, FarActorRange));
			bool flag2 = PossessionHooks.Active && !world.isPaused();
			List<Actor> list = UnitsToVisit(world, num3, num5, host);
			for (int i = 0; i < list.Count; i++)
			{
				Actor actor = list[i];
				SceneClock.Unit();
				bool flag3 = actor == host && !ShowHost && OwnBodyOf(actor);
				if (actor == null || (actor == host && !ShowHost && !BoatHelm.Steers(actor) && !flag3) || actor == Skip || !actor.isAlive() || (actor.is_inside_building && !WalkingIn(actor)) || actor.is_inside_boat || (RoomCombat.Active && actor != host && RoomCombat.In(actor)) || UnitInOffFrameCell(actor, i, host, terrain) || CoastUnit(actor, host, terrain, dt))
				{
					continue;
				}
				if (UnitChecks)
				{
					if (SpecialUnit(actor, i, host, view, range, terrain, sprites, dt))
					{
						continue;
					}
				}
				else
				{
					if (Crabzillas.Is(actor))
					{
						if (Models.Ready)
						{
							AddCrabzilla(actor, view, host, terrain, dt);
						}
						continue;
					}
					if (actor != host && Dragons.Is(actor) && Models.Ready && Dragons.Ready)
					{
						AddDragon(actor, view, terrain, dt);
						continue;
					}
					if (actor != host && IsUfo(actor) && AddUfo(actor, view, range, terrain, sprites, dt))
					{
						continue;
					}
					if (IsGodFinger(actor) && Models.Get("god_hand") != null)
					{
						AddGodFinger(actor, view, terrain, dt);
						continue;
					}
				}
				Vector2 p = actor.current_position;
				float num6 = p.x - view.X;
				float num7 = p.y - view.Y;
				float num8 = num6 * num6 + num7 * num7;
				if (num8 > num4 && (actor.asset == null || !actor.asset.is_boat || !(num8 < num5 * num5)) && !CrowdBeyond(actor, num8))
				{
					SceneClock.To(SceneClock.B.Far);
					if (num8 < FarActorRange * FarActorRange && actor != host)
					{
						AddFarActor(actor, p, num6, num7, terrain, sprites);
					}
					else if (actor != host)
					{
						FarCreature(actor, p, num8, terrain, sprites);
					}
				}
				else
				{
					if ((actor != host && Offscreen(actor, p, num6, num7, num, num2, num8)) || (actor != host && UnitOutsideGodFrame(actor, p, terrain)) || GlideUnit(actor, host, p, num6, num7, num8, dt, terrain))
					{
						continue;
					}
					float num9 = p.x - _steerX;
					float num10 = p.y - _steerY;
					bool flag4 = flag2 && actor != host && num9 * num9 + num10 * num10 < 1296f && Walks(actor) && !actor.is_inside_building;
					SceneClock.Steer(flag4);
					if (flag4 && Steering.Steer(actor, dt))
					{
						p = actor.current_position;
						num6 = p.x - view.X;
						num7 = p.y - view.Y;
						num8 = num6 * num6 + num7 * num7;
					}
					SceneClock.Track();
					if (actor != host)
					{
						p = Drill.Spot(actor, p);
					}
					if (actor != host)
					{
						p = Lounging.Spot(actor, p);
					}
					bool flag5 = actor != host && !flag4 && People.OnCard(actor);
					Heading heading = (flag5 ? TrackLite(actor, p, dt) : Track(actor, p, dt, actor != host, flag4));
					if (actor != host && !DrawnOk(actor, heading))
					{
						continue;
					}
					if (actor != host)
					{
						p = heading.Drawn;
						num6 = p.x - view.X;
						num7 = p.y - view.Y;
						num8 = num6 * num6 + num7 * num7;
					}
					if ((actor != host && (Steering.Hidden(actor) || actor.is_inside_building) && !StillOutside(heading)) || (num6 * num + num7 * num2 < -2f && (actor != host || !BoatHelm.Steers(actor))) || _broken.Contains(actor))
					{
						continue;
					}
					SceneClock.Sprite();
					bool flag6 = flag5 || (actor != host && _glideFocal > 0f && num8 > 3600f);
					SpriteImage spriteImage = (flag6 ? CardImage(actor) : null);
					if (spriteImage == null)
					{
						Sprite spriteToRender;
						try
						{
							spriteToRender = actor.getSpriteToRender();
						}
						catch (Exception e)
						{
							MarkBroken(actor, (actor.asset != null) ? actor.asset.id : "", e);
							continue;
						}
						spriteImage = (SpriteMemo ? UnitImage(i, spriteToRender, sprites) : sprites.Get(spriteToRender));
						if (spriteImage == null)
						{
							continue;
						}
						if (flag6)
						{
							KeepCardImage(actor, spriteImage);
						}
					}
					SceneClock.To(SceneClock.B.Ground);
					Vector3 current_scale = actor.current_scale;
					float num11 = spriteImage.UnitWidth * Mathf.Abs(current_scale.x);
					float num12 = spriteImage.UnitHeight * Mathf.Abs(current_scale.y);
					if (actor != host)
					{
						Steering.SetRadius(actor, num11 * 0.25f);
					}
					heading.Size = num11;
					float num13 = p.x + actor.move_jump_offset.x;
					float y = p.y;
					float num14 = terrain.SampleHeight(num13, y);
					bool flag7 = IsSwimming(actor);
					if (!flag7 && Wading.DrawnFloor(terrain, actor, num13, y, out var floor))
					{
						num14 = floor;
					}
					if (actor.position_height < 0.3f && (actor.asset == null || !actor.asset.is_boat) && DockDecks.HeightAt(num13, y, out var z) && z > num14 - 0.05f)
					{
						num14 = z;
						flag7 = false;
					}
					float num15 = actor.position_height + (Hovers(actor) ? actor.move_jump_offset.y : 0f);
					float num16 = (flag7 ? (terrain.LiquidSurface(num13, y) + terrain.WaveAt(num13, y) - num12 * spriteImage.VisibleFraction * 0.4f - spriteImage.BottomPadding * num12) : (num14 + num15 - spriteImage.BottomPadding * num12));
					if (!flag3 && terrain.IsWaterAt(num13, y) && (actor.asset == null || !actor.asset.is_boat))
					{
						num16 = Wading.EasedBottom(actor, num16, heading.Dt);
						if (actor.position_height < 0.3f)
						{
							Wading.Note(actor, num13, y, heading.Speed, num12, flag7);
						}
					}
					if (num8 < 2304f && !flag3)
					{
						UnitLight(actor, num13, y, num16 + num12 * 0.5f);
					}
					if (num8 < 2304f && IsBurning(actor) && !flag7 && !flag3)
					{
						float num17 = num12 * spriteImage.VisibleFraction;
						float num18 = num16 + spriteImage.BottomPadding * num12;
						FireField.AddBlaze(Billboards, num13, y, num11 * 0.18f, num11 * 0.18f, num18 + num17 * 0.1f, num18 + num17 * 0.75f, (num8 < 100f) ? 4 : 2, Mathf.Clamp(num17 * 0.55f, 0.25f, 2f), (uint)actor.GetHashCode(), _clock);
					}
					uint num19 = 0u;
					uint multiply = 0u;
					float value;
					bool flag8 = RecentHits.TryGetValue(actor, out value);
					num19 = ((!flag8) ? Blocks.FlashOf(actor) : (((uint)(220f * (1f - value / 0.3f)) << 24) | 0xFFFFFF));
					Color color = actor.color;
					if (color.r < 0.98f || color.g < 0.98f || color.b < 0.98f)
					{
						multiply = TerrainCache.Rgb((int)(Mathf.Clamp01(color.r) * 255f), (int)(Mathf.Clamp01(color.g) * 255f), (int)(Mathf.Clamp01(color.b) * 255f));
					}
					bool flag9 = actor == host;
					bool flag10 = flag9 && BoatHelm.Steers(actor);
					int tag = (flag10 ? (-1) : (flag9 ? 2147483646 : Tagged.Count));
					if (!flag9)
					{
						Tagged.Add(actor);
					}
					bool flag11 = actor.asset != null && actor.asset.is_boat;
					if (!flag9 && !flag11 && num8 < 25f && actor.position_height < 0.3f && !Creatures.Airborne(actor))
					{
						Colliders.Near.Add(new Collider
						{
							X = num13,
							Y = y,
							Radius = BumpRadius(actor, num11),
							Mobile = true
						});
					}
					SceneClock.To(SceneClock.B.People);
					if (Models.Ready && People.IsHumanoid(actor) && !Mutations.EggNow(actor))
					{
						if (flag3)
						{
							AddOwnBody(actor, spriteImage, sprites, num13, y, num16 + spriteImage.BottomPadding * num12, Stature.Of(actor, num12 * Mutations.Visible(actor, spriteImage), dt), heading, view, dt, new VoxelObject
							{
								Tag = 2147483646,
								Flash = num19,
								Multiply = multiply,
								SortDepth = 0.2f
							}, UnitChecks ? HitAge(actor, flag8, value) : (RecentHits.TryGetValue(actor, out var value2) ? value2 : (-1f)));
						}
						else if (InView(num13 - _eyeX, y - _eyeY, 1f))
						{
							float angle = heading.Angle;
							float speed = heading.Speed;
							VoxelObject template = new VoxelObject
							{
								Tag = tag,
								Flash = num19,
								Multiply = multiply,
								NoShadow = flag7,
								SortDepth = Mathf.Sqrt(num8)
							};
							float body = Stature.Of(actor, num12 * Mutations.Visible(actor, spriteImage), dt);
							int count = Meshes.Count;
							int count2 = Voxels.Count;
							People.AddMesh(Meshes, Voxels, actor, spriteImage, sprites, num13, y, num16 + spriteImage.BottomPadding * num12, body, angle, speed, Mathf.Sqrt(num8), dt, template, flag7, UnitChecks ? HitAge(actor, flag8, value) : (RecentHits.TryGetValue(actor, out var value3) ? value3 : (-1f)), heading.Dt);
							People.NoteGlide(actor, Meshes, count, Voxels.Count == count2 && !flag7, num14, body);
						}
						continue;
					}
					SceneClock.To(SceneClock.B.Creatures);
					float num20 = ((actor.asset != null && actor.asset.id == "dragon") ? num3 : ((actor.asset != null && actor.asset.is_boat) ? num5 : Mathf.Max(70f, CreatureRange)));
					if (Models.Ready && num8 < num20 * num20)
					{
						float num21 = Mathf.Sqrt(num8);
						bool flag12 = actor.asset != null && actor.asset.is_boat;
						if (flag12 || Models.CreatureFamily(actor) != null)
						{
							if (flag12)
							{
								float heading2 = (flag10 ? BoatHelm.Heading : heading.Angle);
								bool flag13 = flag10 || InView(num13 - _eyeX, y - _eyeY, Mathf.Max(num11, num12) * 2f);
								if (Creatures.AddBoat(flag13 ? Meshes : null, actor, spriteImage, num13, y, num11, heading2, num21, tag, num19, _night, num21 < 16f && !flag9) || !flag13)
								{
									continue;
								}
							}
							if (!InView(num13 - _eyeX, y - _eyeY, Mathf.Max(num11, num12)))
							{
								continue;
							}
							if (!flag12)
							{
								SceneClock.Creature();
							}
							float heading3 = heading.Angle;
							if (flag9 && BeastView.Active)
							{
								heading3 = BeastView.BodyYaw;
							}
							float speed2 = heading.Speed;
							if (!flag12 && Creatures.Add(Meshes, actor, spriteImage, num13, y, num16 + spriteImage.BottomPadding * num12, num11, num12, heading3, speed2, num21, dt, tag, num19, multiply, flag7, heading.Dt))
							{
								continue;
							}
						}
					}
					SceneClock.To(SceneClock.B.Other);
					float num22 = 26f * Detail;
					if (num8 < num22 * num22)
					{
						float angle2 = heading.Angle;
						bool moving = heading.Moving;
						if (People.IsHumanoid(actor))
						{
							VoxelObject template2 = new VoxelObject
							{
								Tag = tag,
								Flash = num19,
								Multiply = multiply,
								NoShadow = flag7,
								SortDepth = Mathf.Sqrt(num8)
							};
							float num23 = Stature.Of(actor, num12 * Mutations.Visible(actor, spriteImage), dt);
							People.Add(Voxels, actor, spriteImage, sprites, num13, y, num16 + spriteImage.BottomPadding * num12, num23, angle2, People.StepFrame(actor, moving && !flag7), template2);
							if (!flag9)
							{
								Hitboxes.RecordSprite(actor, num13, y, num16 + spriteImage.BottomPadding * num12, num11 * 0.7f, num23, angle2);
							}
						}
						else
						{
							VoxelObject item = VoxelObject.Standing(spriteImage.CreatureShape, num13, y, num16, num11, num12, spriteImage.PivotX, 0f - Mathf.Cos(angle2), 0f - Mathf.Sin(angle2));
							item.Tag = tag;
							item.Flash = num19;
							item.Multiply = multiply;
							item.NoShadow = flag7;
							item.SortDepth = Mathf.Sqrt(num8);
							Voxels.Add(item);
							if (!flag9)
							{
								Hitboxes.RecordSprite(actor, num13, y, num16 + spriteImage.BottomPadding * num12, num11, num12 * spriteImage.VisibleFraction, angle2);
							}
						}
					}
					else
					{
						if (!flag9)
						{
							Hitboxes.RecordSprite(actor, num13, y, num16 + spriteImage.BottomPadding * num12, num11, num12 * spriteImage.VisibleFraction, actor.flip ? 0f : MathF.PI);
						}
						if (!flag7)
						{
							Billboards.Add(ShadowOnly(num13, y, num14, num11));
						}
						Billboards.Add(new Billboard
						{
							X = num13,
							Y = y,
							Z = num16,
							GroundZ = num14,
							Width = num11,
							Height = num12,
							Image = spriteImage,
							Kind = BillboardKind.Facing,
							Flip = (actor.flip ^ flag),
							Flash = num19,
							Multiply = multiply,
							Tag = tag
						});
					}
				}
			}
		}

		private static float BumpRadius(Actor a, float spriteWidth)
		{
			if (Hitboxes.Seen(a) && Hitboxes.Get(a, out var box))
			{
				return Mathf.Clamp((box.HalfA + box.HalfB) * 0.5f, 0.06f, 0.8f);
			}
			return Mathf.Clamp(spriteWidth * 0.25f, 0.06f, 0.8f);
		}

		private static bool Walks(Actor a)
		{
			try
			{
				return a.position_height < 0.3f && a.asset != null && !a.asset.is_boat && !a.ignoresBlocks() && !Creatures.Airborne(a) && !IsSwimming(a);
			}
			catch
			{
				return false;
			}
		}

		private static bool Hovers(Actor a)
		{
			try
			{
				return (a.asset != null && a.asset.hovering) || a.isHovering();
			}
			catch
			{
				return false;
			}
		}

		private static Billboard ShadowOnly(float x, float y, float ground, float width)
		{
			return new Billboard
			{
				X = x,
				Y = y,
				GroundZ = ground,
				Width = width,
				Shadow = true,
				Tag = -1
			};
		}

		private void CollectBuildings(ViewState view, float range, TerrainCache terrain, SpriteCache sprites)
		{
			_terrain = terrain;
			Bonfires = _bonfiresNext.ToArray();
			_bonfiresNext.Clear();
			Vector2 vector = new Vector2(view.X, view.Y);
			_framesSinceScan++;
			long num = BcNow();
			long num2 = num;
			if ((vector - _lastScanAt).sqrMagnitude > 16f || _framesSinceScan > 45)
			{
				Rescan(vector, range + 8f);
			}
			BcAdd(ref BcRescan, num2);
			num2 = BcNow();
			int num3 = Math.Min(_buildings.Count, 200);
			for (int i = 0; i < num3; i++)
			{
				_spriteCursor = (_spriteCursor + 1) % _buildings.Count;
				UpdateBuildingSprite(_buildings[_spriteCursor], sprites);
			}
			BcAdd(ref BcSlice, num2);
			float num4 = Mathf.Cos(view.Yaw);
			float num5 = Mathf.Sin(view.Yaw);
			float num6 = range * range;
			for (int j = 0; j < _buildings.Count; j++)
			{
				if (j < _cellAt.Length && _cellAt[j] >= 0 && CellOutOfSight(_cellAt[j]))
				{
					j = _cellEnd[_cellAt[j]] - 1;
					continue;
				}
				NearBuilding nearBuilding = _buildings[j];
				Building building = nearBuilding.Building;
				if ((nearBuilding.Burnt != null && BurntGone(nearBuilding, building)) || building == null || !building.isAlive() || building.chopped || nearBuilding.Look == BuildingLook.Landform)
				{
					continue;
				}
				if (nearBuilding.Image == null || nearBuilding.ImageAsset != building.asset)
				{
					UpdateBuildingSprite(nearBuilding, sprites);
				}
				if (nearBuilding.Image == null)
				{
					continue;
				}
				Vector2 p = Position(building);
				if (building == _shaking && _shakeAge < 0.3f)
				{
					float num7 = 1f - _shakeAge / 0.3f;
					p += new Vector2(Mathf.Sin(_shakeAge * 70f), Mathf.Cos(_shakeAge * 53f)) * (0.045f * num7);
				}
				else
				{
					p += Worked(building, nearBuilding.Look);
				}
				float num8 = p.x - view.X;
				float num9 = p.y - view.Y;
				float num10 = num8 * num8 + num9 * num9;
				bool flag = num8 * num4 + num9 * num5 < 0f - nearBuilding.Width;
				if (num10 > num6 || (flag && num10 > 1681f) || BuildingInOffFrameCell(nearBuilding, p, terrain) || (FromSky && !GodCellsOff && nearBuilding.Look == BuildingLook.Plant && Models.Ready && num10 > 3025f))
				{
					continue;
				}
				float num11 = terrain.SampleHeight(p.x, p.y);
				if (BuildingOutsideGodFrame(nearBuilding, p, num11))
				{
					continue;
				}
				if (building.asset != null && building.asset.draw_light_area && num10 < 2304f)
				{
					BuildingLight(nearBuilding, building, p, num11);
				}
				if (num10 < 2304f && IsBurning(building))
				{
					BuildingBlaze(nearBuilding, building, p, num11, num10);
				}
				float z = num11 - nearBuilding.Image.BottomPadding * nearBuilding.Height;
				int count = Tagged.Count;
				Tagged.Add(building);
				_wantCollider = nearBuilding.Solid && num10 < (5f + nearBuilding.Width) * (5f + nearBuilding.Width);
				float num12 = p.x - _steerX;
				float num13 = p.y - _steerY;
				_wantSolid = nearBuilding.Solid && num12 * num12 + num13 * num13 < (36f + nearBuilding.Width) * (36f + nearBuilding.Width);
				_colliderAdded = false;
				if (flag && nearBuilding.Look == BuildingLook.Plant)
				{
					continue;
				}
				if (nearBuilding.Look == BuildingLook.Plant)
				{
					if (!Models.Ready || (!(num10 > 3025f) && !DrawPlantModel(nearBuilding, building, p, num11, num10, count)))
					{
						Billboards.Add(new Billboard
						{
							X = p.x,
							Y = p.y,
							Z = z,
							GroundZ = num11,
							Width = nearBuilding.Width,
							Height = nearBuilding.Height,
							Image = nearBuilding.Image,
							Kind = BillboardKind.Cross,
							Angle = nearBuilding.Angle,
							Tag = count
						});
					}
					continue;
				}
				num2 = BcNow();
				bool num14 = Models.Ready && ((!_wantSolid && KeptTreeDrawn(nearBuilding, building, p, num11, num10, count)) || DrawAsModel(nearBuilding, building, p, num11, num10, count));
				if (num2 != 0L)
				{
					if (nearBuilding.Route == ModelRoute.Building)
					{
						BcAdd(ref BcHouses, num2);
						BcHousesN++;
					}
					else if (nearBuilding.Look == BuildingLook.Tree)
					{
						BcAdd(ref BcTrees, num2);
						BcTreesN++;
					}
					else
					{
						BcAdd(ref BcOther, num2);
						BcOtherN++;
					}
				}
				if (_wantSolid)
				{
					if (!_colliderAdded)
					{
						_placed = (_yours = ((nearBuilding.Look == BuildingLook.House) ? new Collider
						{
							Box = true,
							X = p.x,
							Y = p.y + nearBuilding.CenterOffset,
							HalfW = nearBuilding.Width * 0.47f,
							HalfD = nearBuilding.DepthTiles * 0.47f
						} : new Collider
						{
							X = p.x,
							Y = p.y,
							Radius = nearBuilding.Radius
						}));
					}
					_yours.Owner = building;
					if (_wantCollider)
					{
						Colliders.Near.Add(_yours);
					}
					Steering.Add(_placed, building);
				}
				if (num14 || flag)
				{
					continue;
				}
				float num15 = ((nearBuilding.Look == BuildingLook.Tree) ? 18f : 30f) * Math.Max(0.55f, Detail);
				if (num10 < num15 * num15)
				{
					VoxelObject item;
					if (nearBuilding.Look == BuildingLook.House)
					{
						float b = nearBuilding.Width / (float)Math.Max(1, nearBuilding.Image.Width);
						int halfDepth = Mathf.Clamp(Mathf.RoundToInt(nearBuilding.DepthTiles * 0.5f / Mathf.Max(0.01f, b)), 2, 48);
						float y = p.y + nearBuilding.CenterOffset;
						item = VoxelObject.Standing(nearBuilding.Image.HouseShape(halfDepth), p.x, y, z, nearBuilding.Width, nearBuilding.Height, nearBuilding.Image.PivotX, 1f, 0f);
					}
					else
					{
						item = VoxelObject.Standing((nearBuilding.Look == BuildingLook.Tree) ? nearBuilding.Image.Tree : nearBuilding.Image.Inflated, p.x, p.y, z, nearBuilding.Width, nearBuilding.Height, nearBuilding.Image.PivotX, Mathf.Cos(nearBuilding.Angle), Mathf.Sin(nearBuilding.Angle));
					}
					item.Tag = count;
					item.SortDepth = Mathf.Sqrt(num10);
					Voxels.Add(item);
				}
				else
				{
					Billboards.Add(new Billboard
					{
						X = p.x,
						Y = p.y + ((nearBuilding.Look == BuildingLook.House) ? nearBuilding.CenterOffset : 0f),
						Z = z,
						GroundZ = num11,
						Width = nearBuilding.Width,
						Height = nearBuilding.Height,
						Image = nearBuilding.Image,
						Kind = ((nearBuilding.Look == BuildingLook.Tree) ? BillboardKind.Cross : BillboardKind.Facing),
						Angle = nearBuilding.Angle,
						Tag = count
					});
				}
			}
			if (num != 0L)
			{
				BcAdd(ref BcTotal, num);
				BcFrames++;
			}
		}

		private void Solid(Collider c)
		{
			_placed = (_yours = c);
			_colliderAdded = true;
		}

		private void SolidModel(NearBuilding nb, MeshModel model, float x, float y, float heading, float s, float baseZ, bool mirror = false, float deck = 0f, MeshModel yourModel = null)
		{
			float lo;
			float hi;
			if (deck > 0f)
			{
				lo = deck;
				hi = 1.6f;
			}
			else
			{
				Colliders.WalkerBand(s, out lo, out hi);
			}
			if (!Colliders.TryFromModel(model, x, y, heading, s, lo, hi, mirror, out var c))
			{
				if (nb.YourShape != null && nb.YourModel == (yourModel ?? model))
				{
					Solid(Colliders.Place(nb.YourShape, x, y, heading, s, mirror));
				}
				return;
			}
			Solid(c);
			if (!_wantCollider)
			{
				return;
			}
			Colliders.YourBand(baseZ, s, out var lo2, out var hi2);
			if (deck > 0f && lo2 < deck)
			{
				lo2 = deck;
				hi2 = Mathf.Max(hi2, deck + 0.3f);
			}
			MeshModel meshModel = yourModel ?? model;
			if (lo2 != lo || hi2 != hi || meshModel != model)
			{
				if (Colliders.TryFromModel(meshModel, x, y, heading, s, lo2, hi2, mirror, out var c2))
				{
					_yours = c2;
				}
				else if (nb.YourShape != null && nb.YourModel == meshModel)
				{
					_yours.Shape = nb.YourShape;
				}
			}
			nb.YourShape = _yours.Shape;
			nb.YourModel = meshModel;
			float num = Mathf.Max(nb.Width, nb.DepthTiles) * 0.6f + 1.5f;
			if ((x - _eyeX) * (x - _eyeX) + (y - _eyeY) * (y - _eyeY) < num * num)
			{
				Colliders.PrefetchYours(model, baseZ, s);
			}
		}

		private bool InView(float dx, float dy, float radius)
		{
			float num = dx * _vfx + dy * _vfy;
			if (num < 0f - radius - _cullBack)
			{
				return false;
			}
			return Math.Abs(dx * _vfy - dy * _vfx) <= Math.Max(0f, num) * _cullTan + _cullSpan + radius + 0.5f;
		}

		private float CrownFade(MeshModel model, Vector2 p, float ground, float across, float tall, float dist)
		{
			float num = across * Mathf.Max(Mathf.Max(Mathf.Abs(model.Min.X), Mathf.Abs(model.Max.X)), Mathf.Max(Mathf.Abs(model.Min.Y), Mathf.Abs(model.Max.Y)));
			float num2 = ground + tall * Mathf.Max(model.Min.Z, model.Max.Z * 0.3f);
			float num3 = ground + tall * model.Max.Z;
			if (_eyeZ < num2 - 0.35f || _eyeZ > num3 + 0.35f || dist > num + 0.35f)
			{
				return 1f;
			}
			return Mathf.Lerp(0.18f, 1f, Mathf.Clamp01((dist - num * 0.35f) / Mathf.Max(0.1f, num * 0.65f + 0.35f)));
		}

		private bool DrawAsModel(NearBuilding nb, Building b, Vector2 p, float ground, float dist2, int tag)
		{
			if (!nb.FamilyChecked || nb.FamilyAsset != b.asset || nb.FamilyGen != TownModels.Generation)
			{
				string meshFamily = nb.MeshFamily;
				nb.FamilyChecked = true;
				nb.FamilyAsset = b.asset;
				nb.FamilyGen = TownModels.Generation;
				string text = b.asset.id ?? "";
				WorldTile current_tile = b.current_tile;
				int variant = (int)((current_tile != null) ? (TerrainModel.Hash(current_tile.x * 7, current_tile.y * 13) >> 4) : 0);
				string text2 = Models.SpecialFamily(text);
				string text3 = Models.BuildingFamily(b);
				if (text2 != null)
				{
					nb.MeshFamily = text2;
					nb.Route = ModelRoute.Special;
				}
				else if (text3 != null)
				{
					nb.MeshFamily = text3;
					nb.Route = ModelRoute.Building;
				}
				else if (nb.Look == BuildingLook.Tree)
				{
					nb.MeshFamily = Models.TreeFamily(text, variant);
				}
				else if (b.asset.type == "type_bonfire" || text == "bonfire")
				{
					nb.MeshFamily = "bonfire";
				}
				else if (nb.Look == BuildingLook.Lump)
				{
					nb.MeshFamily = Models.LumpFamily(text);
				}
				if (meshFamily != null && meshFamily != nb.MeshFamily)
				{
					nb.ModelScale = 0f;
				}
			}
			string family = nb.MeshFamily;
			if (family == null)
			{
				return false;
			}
			if (nb.Route == ModelRoute.Building)
			{
				return DrawBuildingModel(nb, b, p, dist2, tag);
			}
			if (nb.Route == ModelRoute.Special)
			{
				return DrawSpecialModel(nb, b, p, dist2, tag);
			}
			bool flag = nb.Look == BuildingLook.Tree && Burnt(nb, b, ref family);
			if (nb.Look == BuildingLook.Lump)
			{
				family = BiomeBush(nb, b, family);
			}
			if (nb.Look == BuildingLook.Lump)
			{
				family = RockLook(nb, b, family);
			}
			float num = Mathf.Sqrt(dist2);
			bool flag2 = _farBuild || InView(p.x - _eyeX, p.y - _eyeY, Mathf.Max(nb.Width, nb.Height) * 1.3f);
			if (!flag2 && num > 41f)
			{
				return true;
			}
			MeshModel meshModel = (flag2 ? Models.Detailed(family, DetailDistance(nb, num), nb.Mesh?.Model) : Models.Detailed(family, 1000f));
			if (meshModel == null)
			{
				return false;
			}
			MeshInstance meshInstance = nb.Mesh;
			if (meshInstance == null)
			{
				MeshInstance obj = new MeshInstance
				{
					Static = true
				};
				MeshInstance meshInstance2 = obj;
				nb.Mesh = obj;
				meshInstance = meshInstance2;
			}
			MeshInstance meshInstance3 = meshInstance;
			meshInstance3.Model = meshModel;
			meshInstance3.ShadowOnly = !flag2;
			meshInstance3.Static = Steady(nb, p) && (b != _shaking || _shakeAge >= 0.3f);
			bool flag3 = family == "bonfire";
			bool flag4 = NaturePalette.Handles(family);
			if (flag)
			{
				BurntColours(nb, meshInstance3, meshModel);
			}
			else if (flag4 && !Flora.Handles(meshModel))
			{
				NaturalColours(nb, meshInstance3, meshModel, p);
			}
			else if (Flora.Handles(meshModel))
			{
				FloraColours(nb, meshInstance3, meshModel, p);
			}
			else
			{
				meshInstance3.Palette = (flag3 ? null : SharedTreeColours(meshModel, nb.Image, p));
			}
			float num2 = nb.Height * nb.Image.VisibleFraction;
			float num3 = (flag3 ? (nb.Width / 2.1f) : ((nb.Look == BuildingLook.Tree) ? (num2 / Mathf.Max(0.5f, (flag ? (Models.Get(family) ?? meshModel) : meshModel).Max.Z)) : ((!flag4) ? (num2 * 1.35f / Mathf.Max(0.3f, meshModel.Max.Z)) : (num2 * NaturePalette.LumpHeight(family) / Mathf.Max(0.3f, meshModel.Max.Z)))));
			if (nb.Look == BuildingLook.Lump)
			{
				num3 = BushScale(family, num3);
			}
			WorldTile current_tile2 = b.current_tile;
			if (!flag3 && current_tile2 != null)
			{
				num3 *= 0.9f + 0.2f * (float)((TerrainModel.Hash(current_tile2.x, current_tile2.y) >> 12) & 0xFF) / 255f;
			}
			bool flag5 = flag4 && family.StartsWith("rock_", StringComparison.Ordinal);
			if (flag5)
			{
				num3 = RockScale(b, family, meshModel, num2);
			}
			bool flag6 = Giant(nb);
			float num4 = ((nb.Look == BuildingLook.Tree && !flag6) ? (num3 * 0.79999995f) : num3);
			meshInstance3.Place(p.x, p.y, ground, nb.Angle, num4, num4, num3);
			bool flag7 = flag4 && family.StartsWith("bush", StringComparison.Ordinal);
			meshInstance3.Sway = ((nb.Look == BuildingLook.Tree) ? TreeSway.Tree(meshModel.Max.Z * num3, flag6) : (flag7 ? TreeSway.Bush(meshModel.Max.Z * num3) : 0f));
			meshInstance3.SwayInvTop = ((meshInstance3.Sway > 0f) ? (1f / Mathf.Max(0.1f, meshModel.Max.Z)) : 0f);
			if (flag)
			{
				meshInstance3.Sway = 0f;
				meshInstance3.SwayInvTop = 0f;
			}
			if (flag5)
			{
				RockStage(b, meshInstance3, meshModel, ground);
			}
			else if (flag7)
			{
				BushFruit(b, meshInstance3, meshModel);
			}
			if (!_farBuild)
			{
				if (nb.Look == BuildingLook.Tree && !flag6 && !flag)
				{
					WindTree(meshInstance3, meshModel, p, num);
				}
				else if (flag6 && !flag)
				{
					GiantCrown(meshInstance3, meshModel, p, num);
				}
			}
			meshInstance3.Fade = ((nb.Look == BuildingLook.Tree) ? CrownFade(meshModel, p, ground, num4, num3, num) : 1f);
			meshInstance3.FadeKeepsShadow = nb.Look == BuildingLook.Tree;
			if (TreesForTest != null && nb.Look == BuildingLook.Tree && _wantCollider)
			{
				TreesForTest.Add(new TreeSeen
				{
					Id = b.asset.id,
					Radius = nb.Radius,
					Model = (Models.Get(family) ?? meshModel),
					Scale = num3,
					Angle = nb.Angle,
					At = p,
					Ground = ground
				});
			}
			if (_wantSolid && nb.Look != BuildingLook.Tree)
			{
				MeshModel meshModel2 = Models.Get(family) ?? meshModel;
				if (flag5)
				{
					SolidModel(nb, meshModel2, p.x, p.y, nb.Angle, num3, ground);
				}
				else
				{
					Solid(Colliders.FromModel(meshModel2, p.x, p.y, nb.Angle, num3, 0.02f, Mathf.Max(0.3f, Mathf.Min(meshModel2.Max.Z * 0.8f, 1.2f / Mathf.Max(0.05f, num3)))));
				}
				if (flag5 && _wantCollider && num < 6f)
				{
					BuildingFloors.Register(b, meshModel2, p.x, p.y, ground, nb.Angle, num3, mirror: false, (RockForms.StyleOf(family) == 'j') ? 0.24f : 0.14f);
				}
				if (flag3)
				{
					_yours = Colliders.FromModel(Unflamed(meshModel2), p.x, p.y, nb.Angle, num3, 0.45f, 0.85f);
				}
			}
			else if (_wantSolid && flag6)
			{
				SolidModel(nb, Models.Get(family) ?? meshModel, p.x, p.y, nb.Angle, num3, ground);
			}
			meshInstance3.Tag = tag;
			meshInstance3.SortDepth = num;
			if (flag3)
			{
				int num5 = meshModel.PartIndex("flames");
				meshInstance3.EnsurePose();
				meshInstance3.ResetPose();
				bool flag8 = false;
				try
				{
					flag8 = b.isUnderConstruction();
				}
				catch
				{
				}
				if (!flag8 && !_farBuild)
				{
					AddFires(Models.Get(family) ?? meshModel, p.x, p.y, ground, nb.Angle, num3, mirror: false);
				}
				if (num5 >= 0)
				{
					float time = Time.time;
					meshInstance3.PartScale[num5] = (flag8 ? 0f : (0.92f + 0.1f * Mathf.Sin(time * 9.3f) + 0.05f * Mathf.Sin(time * 14.1f + 1f)));
					meshInstance3.PartRotation[num5] = Mat3.RotZ(time * 0.7f) * Mat3.RotX(0.05f * Mathf.Sin(time * 5.1f));
				}
			}
			AddModel(Snowed(meshInstance3));
			if (flag && !_farBuild)
			{
				BurntSmoulder(nb, b, meshInstance3, p, ground, num);
			}
			KeepTree(nb, b, p, ground, family, flag3 || flag || flag6 || _wantSolid);
			return true;
		}

		private static FireModel FireModelOf(MeshModel full)
		{
			lock (_fireModels)
			{
				if (_fireModels.TryGetValue(full, out var value))
				{
					return value;
				}
				List<MeshPart> list = new List<MeshPart>();
				List<float> list2 = new List<float>();
				int[] array = new int[full.Parts.Length];
				for (int i = 0; i < full.Parts.Length; i++)
				{
					MeshPart meshPart = full.Parts[i];
					array[i] = -1;
					if (meshPart.Name == null || !meshPart.Name.StartsWith("flames", StringComparison.Ordinal))
					{
						array[i] = list.Count;
						list.Add(meshPart);
						continue;
					}
					int vertexCount = meshPart.VertexCount;
					if (vertexCount != 0)
					{
						float num = 0f;
						float num2 = 0f;
						float num3 = float.MinValue;
						float num4 = float.MaxValue;
						for (int j = 0; j < vertexCount; j++)
						{
							num += meshPart.Pos[j * 3];
							num2 += meshPart.Pos[j * 3 + 1];
							num3 = Math.Max(num3, meshPart.Pos[j * 3 + 2]);
							num4 = Math.Min(num4, meshPart.Pos[j * 3 + 2]);
						}
						num /= (float)vertexCount;
						num2 /= (float)vertexCount;
						float num5 = 0f;
						for (int k = 0; k < vertexCount; k++)
						{
							float num6 = meshPart.Pos[k * 3] - num;
							float num7 = meshPart.Pos[k * 3 + 1] - num2;
							num5 = Math.Max(num5, num6 * num6 + num7 * num7);
						}
						list2.Add(num);
						list2.Add(num2);
						list2.Add((float)Math.Sqrt(num5));
						list2.Add(num4);
						list2.Add(num3);
					}
				}
				for (int l = 0; l < list.Count; l++)
				{
					MeshPart meshPart2 = list[l];
					int num8 = ((meshPart2.Parent >= 0 && meshPart2.Parent < array.Length) ? array[meshPart2.Parent] : (-1));
					if (num8 != meshPart2.Parent)
					{
						list[l] = new MeshPart
						{
							Name = meshPart2.Name,
							Parent = num8,
							Pivot = meshPart2.Pivot,
							Pos = meshPart2.Pos,
							Nrm = meshPart2.Nrm,
							Tri = meshPart2.Tri,
							Slot = meshPart2.Slot,
							Axis = meshPart2.Axis,
							Uv = meshPart2.Uv
						};
					}
				}
				value = new FireModel
				{
					Bare = ((list.Count == full.Parts.Length) ? full : full.Derive(full.Name + "_unlit", list.ToArray())),
					Fires = list2.ToArray()
				};
				_fireModels[full] = value;
				return value;
			}
		}

		private static MeshModel Unflamed(MeshModel full)
		{
			return FireModelOf(full).Bare;
		}

		private void AddFires(MeshModel full, float x, float y, float ground, float heading, float s, bool mirror)
		{
			float[] fires = FireModelOf(full).Fires;
			float num = Mathf.Sin(heading);
			float num2 = Mathf.Cos(heading);
			for (int i = 0; i + 4 < fires.Length; i += 5)
			{
				float num3 = (mirror ? (0f - fires[i]) : fires[i]);
				float num4 = fires[i + 1];
				Vector2 at = new Vector2(x + ((0f - num3) * num - num4 * num2) * s, y + (num3 * num2 - num4 * num) * s);
				_bonfiresNext.Add(new FireSpot
				{
					At = at,
					Ground = ground,
					Radius = fires[i + 2] * s,
					Bottom = ground + fires[i + 3] * s,
					Top = ground + fires[i + 4] * s
				});
			}
		}

		private bool DrawBuildingModel(NearBuilding nb, Building b, Vector2 p, float dist2, int tag)
		{
			bool flag;
			bool flag2;
			bool flag3;
			try
			{
				if (b.isUnderConstruction())
				{
					return DrawSite(nb, b, p, dist2, tag);
				}
				flag = b.isRuin();
				flag2 = !flag && b.isAbandoned();
				flag3 = !flag && b.isUsable() && !flag2 && (!b.asset.hasHousingSlots() || b.hasResidents() || AllLit);
			}
			catch
			{
				return false;
			}
			MeshModel meshModel = (flag ? RuinModel(nb) : null) ?? Models.Get(nb.MeshFamily);
			if (meshModel == null)
			{
				return false;
			}
			bool flag4 = flag && meshModel.Info("ruin") == "1";
			if (flag && nb.WasStanding && nb.RuinedAt < 0f)
			{
				nb.RuinedAt = _clock;
			}
			nb.WasStanding = !flag;
			float num = ((flag4 && nb.StandingWidth > 0f) ? nb.StandingWidth : nb.Width);
			float x = p.x;
			float num2 = p.y + nb.CenterOffset;
			float num3 = Mathf.Sqrt(dist2);
			bool flag5 = _farBuild || InView(x - _eyeX, num2 - _eyeY, Mathf.Max(nb.Width, nb.Height) * 1.2f);
			if (!flag5 && num3 > 41f)
			{
				return true;
			}
			MeshModel meshModel2 = ((flag4 || flag) ? meshModel : (flag5 ? Models.Detailed(nb.MeshFamily, num3, nb.Mesh?.Model) : Models.Detailed(nb.MeshFamily, 1000f)));
			if (meshModel2 == null)
			{
				meshModel2 = meshModel;
			}
			MeshInstance meshInstance = nb.Mesh;
			if (meshInstance == null)
			{
				MeshInstance obj2 = new MeshInstance
				{
					Static = true
				};
				MeshInstance meshInstance2 = obj2;
				nb.Mesh = obj2;
				meshInstance = meshInstance2;
			}
			MeshInstance meshInstance3 = meshInstance;
			meshInstance3.Model = meshModel2;
			meshInstance3.ShadowOnly = !flag5;
			meshInstance3.Palette = (flag4 ? Models.RuinColors(meshModel2, RoofColour(nb, b)) : Models.BuildingColors(meshModel2, nb.MeshFamily, RoofColour(nb, b)));
			if (nb.MeshFamily != null && nb.MeshFamily.Contains("mine"))
			{
				meshInstance3.Palette = Boulders(nb, b, meshModel2, meshInstance3.Palette);
			}
			if (nb.MeshFamily != null && nb.MeshFamily.StartsWith("statue", StringComparison.Ordinal))
			{
				num = meshModel.InfoFloat("tiles", num);
			}
			float num4 = KeptScale(nb, b, flag, num / Mathf.Max(0.5f, BuildingInfo ? InfoFloat(ref nb.Memo.Width, meshModel2, "width", meshModel2.Max.X - meshModel2.Min.X) : meshModel2.InfoFloat("width", meshModel2.Max.X - meshModel2.Min.X)));
			float num5 = num * 0.4f;
			float num6 = nb.DepthTiles * 0.4f;
			float num7 = _terrain.SampleHeight(x, num2);
			float num8 = Mathf.Min(num7, Mathf.Min(Mathf.Min(_terrain.SampleHeight(x - num5, num2 - num6), _terrain.SampleHeight(x + num5, num2 - num6)), Mathf.Min(_terrain.SampleHeight(x - num5, num2 + num6), _terrain.SampleHeight(x + num5, num2 + num6))));
			float num9 = (BuildingInfo ? InfoFloat(ref nb.Memo.Footing, meshModel, "footing", 0f) : meshModel.InfoFloat("footing", 0f));
			string text = (BuildingInfo ? meshModel2.Info("kind") : null);
			int num10 = Facing.Of(b);
			if (num9 > 0f)
			{
				Vector2 vector = new Vector2(x, num2) + Facing.Turn(new Vector2(0f, 0f - num6), num10);
				num8 = (((BuildingInfo ? text : meshModel2.Info("kind")) == "tent") ? BuildingGround.Pitch(num8, num7) : BuildingGround.Base(num8, _terrain.SampleHeight(vector.x, vector.y), num7, num9, num4));
			}
			float heading = Facing.Heading(num10);
			if ((BuildingInfo ? text : meshModel2.Info("kind")) == "docks")
			{
				if (float.IsNaN(nb.DockHeading))
				{
					nb.DockHeading = DockFacing.Heading(b, x, num2);
				}
				heading = nb.DockHeading;
				num8 = DockFacing.Ground(_terrain, x, num2);
			}
			WorldTile current_tile = b.current_tile;
			bool flag6 = flag4 && current_tile != null && (BuildingInfo ? text : meshModel2.Info("kind")) != "docks" && ((TerrainModel.Hash(current_tile.x * 5 + 1, current_tile.y * 7 + 3) >> 9) & 1) != 0;
			if (!flag && num9 > 0f)
			{
				flag6 = Models.Mirrored(b);
			}
			meshInstance3.Place(x, num2, num8 - 0.05f, heading, flag6 ? (0f - num4) : num4, num4, num4);
			bool flag7 = (BuildingInfo ? text : meshModel2.Info("kind")) == "docks";
			if (!_farBuild)
			{
				if (flag7 && !flag4)
				{
					DockDecks.Register(b, meshModel, x, num2, num8 - 0.05f, heading, num4);
				}
				else if (!flag && (BuildingInfo ? text : meshModel2.Info("kind")) == "mine")
				{
					MineInterior.Register(b, x, num2, num8 - 0.05f, num4);
				}
			}
			if (_wantSolid)
			{
				bool flag8 = b.asset.type == "type_bonfire";
				SolidModel(nb, meshModel, x, num2, heading, num4, num8 - 0.05f, flag6, (flag7 && !flag4) ? 0.8f : 0f, flag8 ? Unflamed(meshModel) : null);
				if (_wantCollider && (!flag7 || flag4))
				{
					BuildingFloors.Register(b, flag8 ? Unflamed(meshModel) : meshModel, x, num2, num8 - 0.05f, heading, num4, flag6);
				}
				string text2 = (BuildingInfo ? text : meshModel2.Info("kind"));
				switch (text2)
				{
				case "house":
				case "hall":
				case "tent":
				{
					if (Steering.DoorOf(meshModel, out var at))
					{
						Steering.SetDoor(b, new Vector2(x, num2) + Facing.Turn(new Vector2((flag6 ? (0f - at.x) : at.x) * num4, Mathf.Min(at.y, meshModel.Min.Y) * num4 - 0.3f), num10));
					}
					break;
				}
				}
				if (text2 == "house" || text2 == "tent")
				{
					HouseWorld.Note(b, meshModel, x, num2, num8 - 0.05f, num4, flag6, num10);
				}
			}
			meshInstance3.Tag = tag;
			meshInstance3.SortDepth = num3;
			bool flag9 = b.asset.hasHousingSlots();
			_farWindows = ((!flag3) ? 0f : ((!flag9 || AllLit) ? 1f : (HouseOccupancy.Lively(b) ? 1f : 0.3f)));
			meshInstance3.Glow = _farWindows * Mathf.Clamp01((_night - 0.05f) * 2.2f);
			meshInstance3.Multiply = ((flag && !flag4) ? TerrainCache.Rgb(128, 120, 112) : (flag2 ? Weathered : 0u));
			if (flag4 && !_farBuild)
			{
				Smoulder(nb, b, x, num2, num8, dist2);
			}
			int num11 = meshModel2.PartIndex("sails");
			meshInstance3.Static = num11 < 0 && Steady(nb, p);
			if (!_farBuild && DrillYard(nb, b, meshInstance3))
			{
				meshInstance3.Static = false;
			}
			if (num11 >= 0)
			{
				meshInstance3.EnsurePose();
				meshInstance3.PartRotation[num11] = (flag3 ? Mat3.RotY(_clock * 0.9f + nb.Angle) : Mat3.RotY(nb.Angle));
			}
			BuildingEffects(nb, b, meshInstance3, meshModel2, flag3, flag, flag5 && !_farBuild, dist2);
			if (_farBuild)
			{
				AddModel(Snowed(meshInstance3));
				return true;
			}
			if (flag3 && b.asset.type == "type_bonfire")
			{
				AddFires(meshModel, x, num2, num8 - 0.05f, heading, num4, flag6);
			}
			TownsBuilding(b, meshInstance3, meshModel, meshModel2, nb.MeshFamily, x, num2, num8 - 0.05f, heading, num4, flag6, flag3, flag, flag5, num3);
			HallFlagsAt(nb, b, meshInstance3, meshModel, x, num2, heading, num4, flag6, flag, flag4, flag2, flag5, num3);
			if (b != SkipBuilding)
			{
				Meshes.Add(Snowed(meshInstance3));
			}
			return true;
		}

		private static float KeptScale(NearBuilding nb, Building b, bool ruin, float measured, bool settle = false)
		{
			if (nb.ModelScale <= 0f || nb.ScaleAsset != b.asset || nb.ScaleRuin != ruin)
			{
				nb.ModelScale = measured;
				nb.ScaleAsset = b.asset;
				nb.ScaleRuin = ruin;
				nb.ScaleSettling = (settle ? 60 : 0);
			}
			else if (nb.ScaleSettling > 0)
			{
				nb.ScaleSettling--;
				if (measured > nb.ModelScale)
				{
					nb.ModelScale = measured;
				}
			}
			return nb.ModelScale;
		}

		private static bool Steady(NearBuilding nb, Vector2 p)
		{
			if ((p - nb.LastPos).sqrMagnitude > 1E-08f)
			{
				nb.Wobbling = 20;
			}
			else if (nb.Wobbling > 0)
			{
				nb.Wobbling--;
			}
			nb.LastPos = p;
			return nb.Wobbling == 0;
		}

		private bool DrawSpecialModel(NearBuilding nb, Building b, Vector2 p, float dist2, int tag)
		{
			if (IsVentFamily(nb.MeshFamily))
			{
				return DrawVent(nb, b, p, dist2, tag);
			}
			MeshModel meshModel = Models.Detailed(nb.MeshFamily, Mathf.Sqrt(dist2) * 0.45f, nb.Mesh?.Model);
			if (meshModel == null)
			{
				return false;
			}
			float x = p.x;
			float num = p.y + nb.CenterOffset;
			float num2 = Mathf.Sqrt(dist2);
			bool flag = _farBuild || InView(x - _eyeX, num - _eyeY, Mathf.Max(nb.Width, nb.Height) * 1.2f);
			if (!flag && num2 > 41f)
			{
				return true;
			}
			MeshInstance meshInstance = nb.Mesh;
			if (meshInstance == null)
			{
				MeshInstance obj = new MeshInstance
				{
					Static = true
				};
				MeshInstance meshInstance2 = obj;
				nb.Mesh = obj;
				meshInstance = meshInstance2;
			}
			MeshInstance meshInstance3 = meshInstance;
			if (meshInstance3.Palette == null || nb.PaletteImage != nb.Image || meshInstance3.Palette.Length != meshModel.SlotNames.Length)
			{
				meshInstance3.Palette = Models.CreaturePalette(meshModel, nb.Image);
				nb.PaletteImage = nb.Image;
			}
			meshInstance3.Model = meshModel;
			meshInstance3.ShadowOnly = !flag;
			float num3 = (BuildingInfo ? KeptScale(nb, b, ruin: false, Mathf.Min(nb.Height * nb.Image.VisibleFraction / Mathf.Max(0.2f, InfoFloat(ref nb.Memo.Height, meshModel, "height", meshModel.Max.Z)), nb.Width * Models.OpaqueWidth(nb.Image) / Mathf.Max(0.2f, InfoFloat(ref nb.Memo.Width, meshModel, "width", meshModel.Max.X - meshModel.Min.X))), settle: true) : KeptScale(nb, b, ruin: false, Mathf.Min(nb.Height * nb.Image.VisibleFraction / Mathf.Max(0.2f, meshModel.InfoFloat("height", meshModel.Max.Z)), nb.Width * Models.OpaqueWidth(nb.Image) / Mathf.Max(0.2f, meshModel.InfoFloat("width", meshModel.Max.X - meshModel.Min.X))), settle: true));
			float num4 = nb.Width * 0.3f;
			float num5 = nb.DepthTiles * 0.3f;
			float num6 = Mathf.Min(_terrain.SampleHeight(x, num), Mathf.Min(Mathf.Min(_terrain.SampleHeight(x - num4, num - num5), _terrain.SampleHeight(x + num4, num - num5)), Mathf.Min(_terrain.SampleHeight(x - num4, num + num5), _terrain.SampleHeight(x + num4, num + num5))));
			meshInstance3.Static = Steady(nb, p);
			meshInstance3.Place(x, num, num6 - 0.05f, -MathF.PI / 2f, num3, num3, num3);
			if (!_farBuild)
			{
				if (nb.MeshFamily == "nature_monolith")
				{
					MonolithGlow(meshInstance3, x, num, num6, meshModel.Max.Z * num3);
				}
				else
				{
					WonderEffects(nb, b, meshInstance3, meshModel, dist2, flag, num6);
				}
			}
			if (_wantSolid)
			{
				SolidModel(nb, meshModel, x, num, -MathF.PI / 2f, num3, num6 - 0.05f);
			}
			meshInstance3.Tag = tag;
			meshInstance3.SortDepth = num2;
			AddModel(Snowed(meshInstance3));
			return true;
		}

		internal void DumpNear(Vector2 at, float radius)
		{
			foreach (NearBuilding building2 in _buildings)
			{
				Building building = building2.Building;
				if (building == null || !building.isAlive() || building.asset == null)
				{
					continue;
				}
				Vector2 vector = Position(building);
				if (!((vector - at).sqrMagnitude > radius * radius))
				{
					string text = "";
					try
					{
						text = (building.isRuin() ? " ruin" : "") + (building.isUnderConstruction() ? " building" : "");
					}
					catch
					{
					}
					Log.Info(string.Format("LOOK building {0} at {1:F1}: look {2}, family {3}, route {4}, checked {5}, size {6:F1}x{7:F1}{8}, mesh {9}, scale {10:F2}, seen {11:F2}", building.asset.id, (vector - at).magnitude, building2.Look, building2.MeshFamily ?? "-", building2.Route, building2.FamilyChecked, building2.Width, building2.Height, text, (building2.Mesh != null && building2.Mesh.Model != null) ? building2.Mesh.Model.Name : "-", building2.ModelScale, (building2.Image != null) ? building2.Image.VisibleFraction : 0f));
				}
			}
		}

		private void NaturalColours(NearBuilding nb, MeshInstance inst, MeshModel model, Vector2 p)
		{
			TerrainCache terrain = _terrain;
			uint num = 0u;
			if (terrain != null && terrain.Width > 0)
			{
				int num2 = Mathf.Clamp((int)p.x, 0, terrain.Width - 1);
				int num3 = Mathf.Clamp((int)p.y, 0, terrain.Height - 1);
				num = terrain.Colors[num3 * terrain.Width + num2];
			}
			if (inst.Palette == null || nb.PaletteImage != nb.Image || nb.PaletteGround != num || inst.Palette.Length != model.SlotNames.Length)
			{
				inst.Palette = ((model.Name != null && model.Name.StartsWith("plant_", StringComparison.Ordinal)) ? NaturePalette.Plant(model, nb.Image, num) : ((model.Name != null && model.Name.StartsWith("rock_", StringComparison.Ordinal)) ? NaturePalette.Rock(model, nb.Image, num, RockBiome(nb)) : NaturePalette.Lump(model, nb.Image, num)));
				nb.PaletteImage = nb.Image;
				nb.PaletteGround = num;
			}
		}

		private bool DrawPlantModel(NearBuilding nb, Building b, Vector2 p, float ground, float dist2, int tag)
		{
			if (IsCrop(b) && Models.Get("crop_wheat_0") != null)
			{
				return DrawCrop(nb, b, p, dist2, tag);
			}
			if (!nb.FamilyChecked || nb.FamilyAsset != b.asset)
			{
				nb.FamilyChecked = true;
				nb.FamilyAsset = b.asset;
				nb.MeshFamily = Models.PlantFamily(b.asset);
			}
			if (nb.MeshFamily == null)
			{
				return false;
			}
			float num = Mathf.Sqrt(dist2);
			if (!InView(p.x - _eyeX, p.y - _eyeY, 1f))
			{
				return true;
			}
			MeshModel meshModel = Models.Detailed(nb.MeshFamily, num, nb.Mesh?.Model);
			if (meshModel == null)
			{
				return false;
			}
			MeshInstance meshInstance = nb.Mesh;
			if (meshInstance == null)
			{
				MeshInstance obj = new MeshInstance
				{
					Static = true,
					NoShadow = true
				};
				MeshInstance meshInstance2 = obj;
				nb.Mesh = obj;
				meshInstance = meshInstance2;
			}
			MeshInstance meshInstance3 = meshInstance;
			if (NaturePalette.Handles(nb.MeshFamily))
			{
				NaturalColours(nb, meshInstance3, meshModel, p);
			}
			else if (Flora.Handles(meshModel))
			{
				FloraColours(nb, meshInstance3, meshModel, p);
			}
			else if (meshInstance3.Palette == null || nb.PaletteImage != nb.Image || meshInstance3.Palette.Length != meshModel.SlotNames.Length)
			{
				meshInstance3.Palette = Models.CreaturePalette(meshModel, nb.Image);
				nb.PaletteImage = nb.Image;
			}
			meshInstance3.Model = meshModel;
			WorldTile current_tile = b.current_tile;
			uint num2 = ((current_tile != null) ? TerrainModel.Hash(current_tile.x * 5 + 3, current_tile.y * 7 - 1) : 0u);
			float num3 = ((float)(num2 & 0xFF) / 255f - 0.5f) * 0.5f;
			float num4 = ((float)((num2 >> 8) & 0xFF) / 255f - 0.5f) * 0.5f;
			float num5 = 0.625f * (0.8f + 0.4f * (float)((num2 >> 16) & 0xFF) / 255f);
			float x = p.x + num3;
			float y = p.y + num4;
			meshInstance3.Place(x, y, _terrain.SampleHeight(x, y) - 0.01f, nb.Angle, num5, num5, num5);
			meshInstance3.Sway = TreeSway.Plant(meshModel.Max.Z * num5);
			meshInstance3.SwayInvTop = 1f / Mathf.Max(0.05f, meshModel.Max.Z);
			meshInstance3.Tag = tag;
			meshInstance3.SortDepth = num;
			Meshes.Add(Snowed(meshInstance3));
			return true;
		}

		private void CollectVolcanoes(ViewState view, WorldSampler sampler, float dt)
		{
			List<Vector3> craters = sampler.Craters;
			for (int i = 0; i < craters.Count; i++)
			{
				Vector3 vector = craters[i];
				float num = vector.x - view.X;
				float num2 = vector.y - view.Y;
				if (num * num + num2 * num2 > 19600f)
				{
					continue;
				}
				AddLight(vector.x, vector.y, vector.z + 2.5f, 18f, 1.25f, 0.5f, 0.14f, 0f);
				_craterTimers.TryGetValue(i, out var value);
				value += dt;
				while (value > 0.22f && _puffs.Count < 220)
				{
					value -= 0.22f;
					float num3 = (float)(_chipRandom.NextDouble() - 0.5) * 1.6f;
					float num4 = (float)(_chipRandom.NextDouble() - 0.5) * 1.6f;
					_puffs.Add(new Puff
					{
						P = new Vec3(vector.x + num3, vector.y + num4, vector.z + 0.6f),
						V = new Vec3(0.35f + num3 * 0.1f, 0.12f + num4 * 0.1f, 1.3f + (float)_chipRandom.NextDouble() * 0.6f),
						Life = 7f + (float)_chipRandom.NextDouble() * 3f,
						Size = 1.1f + (float)_chipRandom.NextDouble() * 0.8f,
						Grow = 0.55f + (float)_chipRandom.NextDouble() * 0.35f
					});
					if (_chipRandom.NextDouble() < 0.28)
					{
						float f = (float)(_chipRandom.NextDouble() * Math.PI * 2.0);
						float num5 = 2.5f + (float)_chipRandom.NextDouble() * 3f;
						_puffs.Add(new Puff
						{
							P = new Vec3(vector.x, vector.y, vector.z + 0.4f),
							V = new Vec3(Mathf.Cos(f) * num5, Mathf.Sin(f) * num5, 6f + (float)_chipRandom.NextDouble() * 4f),
							Life = 2.2f,
							Size = 0.25f,
							Ember = true
						});
					}
				}
				_craterTimers[i] = value;
			}
			for (int num6 = _puffs.Count - 1; num6 >= 0; num6--)
			{
				Puff puff = _puffs[num6];
				puff.Age += dt;
				if (puff.Age > puff.Life)
				{
					_puffs.RemoveAt(num6);
					continue;
				}
				if (puff.Ember)
				{
					puff.V += new Vec3(0f, 0f, -9f * dt);
					puff.P += puff.V * dt;
					if (puff.P.Z < sampler.Cache.SampleHeight(puff.P.X, puff.P.Y))
					{
						_puffs.RemoveAt(num6);
						continue;
					}
				}
				else
				{
					puff.V = new Vec3(puff.V.X, puff.V.Y, puff.V.Z * (1f - 0.12f * dt));
					puff.P += puff.V * dt;
				}
				float num7 = puff.Age / puff.Life;
				float num8 = puff.P.X - view.X;
				float num9 = puff.P.Y - view.Y;
				float num10 = Mathf.Sqrt(num8 * num8 + num9 * num9);
				if (!puff.Ember)
				{
					float num11 = puff.Size + puff.Grow * puff.Age;
					int shade = Math.Min(5, (int)(num7 * 6f));
					float num12 = ((num7 < 0.08f) ? (num7 / 0.08f) : Mathf.Pow(1f - (num7 - 0.08f) / 0.92f, 1.2f));
					Billboards.Add(new Billboard
					{
						X = puff.P.X,
						Y = puff.P.Y,
						Z = puff.P.Z - num11 * 0.5f,
						GroundZ = puff.P.Z - num11,
						Width = num11,
						Height = num11,
						Image = FireField.SoftSmoke(shade, num6 & 3),
						Kind = BillboardKind.Facing,
						Flip = ((num6 & 4) != 0),
						Tag = -1,
						Alpha = (byte)Mathf.Clamp(200f * num12, 1f, 255f)
					});
					continue;
				}
				uint color = ((num7 < 0.5f) ? EmberHot : EmberCool);
				float f2 = puff.Age * 6f + (float)num6;
				VoxelObject item = new VoxelObject
				{
					Shape = ChipShape(color),
					Tag = -1,
					NoShadow = true,
					Emissive = true
				};
				item.U = new Vec3(Mathf.Cos(f2), Mathf.Sin(f2), 0f);
				item.V = new Vec3(0f, 0f, 1f);
				item.N = Vec3.Cross(item.U, item.V);
				item.SizeU = (item.SizeV = puff.Size);
				item.SizeN = puff.Size * 0.5f;
				item.Origin = puff.P - item.U * (puff.Size * 0.5f) - new Vec3(0f, 0f, puff.Size * 0.5f);
				item.Flash = 1883295999u;
				if (num10 < 40f)
				{
					AddLight(puff.P.X, puff.P.Y, puff.P.Z, 3f, 1f, 0.5f, 0.15f, 0.06f);
				}
				item.SortDepth = num10;
				Voxels.Add(item);
			}
		}

		public void Shake(Building b)
		{
			_shaking = b;
			_shakeAge = 0f;
		}

		private NearBuilding Find(Building b)
		{
			for (int i = 0; i < _buildings.Count; i++)
			{
				if (_buildings[i].Building == b)
				{
					return _buildings[i];
				}
			}
			return null;
		}

		public void Chips(Building b, Vector2 from, float atHeight, int count)
		{
			NearBuilding nearBuilding = Find(b);
			if (nearBuilding == null || nearBuilding.Image == null || _terrain == null)
			{
				return;
			}
			Vector2 vector = Position(b);
			Vector2 normalized = (from - vector).normalized;
			float num = _terrain.SampleHeight(vector.x, vector.y);
			float num2 = Mathf.Max(0.08f, nearBuilding.Radius);
			Vec3 p = new Vec3(vector.x + normalized.x * num2, vector.y + normalized.y * num2, num + atHeight);
			SpriteImage image = nearBuilding.Image;
			int y = ((nearBuilding.Look == BuildingLook.Tree) ? (image.OpaqueBottom + Math.Max(1, (image.OpaqueTop - image.OpaqueBottom) / 3)) : image.OpaqueTop);
			Vec3 vec = new Vec3(0f - normalized.y, normalized.x, 0f);
			for (int i = 0; i < count; i++)
			{
				if (_chips.Count >= 80)
				{
					break;
				}
				uint num3 = RandomColor(image, image.OpaqueBottom, y);
				if (num3 != 0)
				{
					float num4 = (float)(_chipRandom.NextDouble() - 0.5) * 1.6f;
					Vec3 v = new Vec3(normalized.x, normalized.y, 0f) * (0.8f + (float)_chipRandom.NextDouble() * 0.9f) + vec * num4 + new Vec3(0f, 0f, 1.2f + (float)_chipRandom.NextDouble() * 1.4f);
					_chips.Add(new Chip
					{
						P = p,
						V = v,
						Life = 0.9f + (float)_chipRandom.NextDouble() * 0.6f,
						Size = 0.03f + (float)_chipRandom.NextDouble() * 0.035f,
						Spin = (float)_chipRandom.NextDouble() * 6f,
						Shape = ChipShape(num3)
					});
				}
			}
		}

		private uint RandomColor(SpriteImage img, int y0, int y1)
		{
			int num = Math.Max(0, y0);
			int maxValue = Math.Max(num + 1, Math.Min(img.Height, y1));
			for (int i = 0; i < 24; i++)
			{
				int num2 = _chipRandom.Next(img.Width);
				int num3 = _chipRandom.Next(num, maxValue);
				uint num4 = img.Pixels[num3 * img.Width + num2];
				if (num4 >> 24 >= 128)
				{
					return num4 | 0xFF000000u;
				}
			}
			return 0u;
		}

		private VoxelShape ChipShape(uint color)
		{
			if (_chipShapes.TryGetValue(color, out var value))
			{
				return value;
			}
			if (_chipShapes.Count > 256)
			{
				_chipShapes.Clear();
			}
			value = VoxelShape.NewGrid(1, 1, 1);
			value.Box(0, 0, -1, 1, 1, 1, color);
			_chipShapes[color] = value;
			return value;
		}

		public void Fell(Building b, Vector2 from)
		{
			NearBuilding nearBuilding = Find(b);
			if (nearBuilding != null && nearBuilding.Image != null && _terrain != null)
			{
				Vector2 vector = Position(b);
				Vector2 vector2 = vector - from;
				vector2 = ((vector2.sqrMagnitude > 0.0001f) ? vector2.normalized : new Vector2(1f, 0f));
				float num = _terrain.SampleHeight(vector.x, vector.y);
				_falling.Add(new Falling
				{
					Image = nearBuilding.Image,
					X = vector.x,
					Y = vector.y,
					Ground = num,
					Bottom = num - nearBuilding.Image.BottomPadding * nearBuilding.Height,
					Width = nearBuilding.Width,
					Height = nearBuilding.Height,
					Angle = nearBuilding.Angle,
					Axis = new Vec3(0f - vector2.y, vector2.x, 0f)
				});
				if (nearBuilding.Mesh != null && nearBuilding.MeshFamily != null)
				{
					Falling falling = _falling[_falling.Count - 1];
					falling.Mesh = new MeshInstance
					{
						Model = (Models.Get(DrawnFamily(nearBuilding)) ?? nearBuilding.Mesh.Model),
						Palette = nearBuilding.Mesh.Palette
					};
					falling.MeshScale = nearBuilding.Mesh.Basis.C2.Z;
				}
				if (_falling.Count > 6)
				{
					_falling.RemoveAt(0);
				}
			}
		}

		private void CollectWorkEffects(ViewState view, TerrainCache terrain, float dt)
		{
			_terrain = terrain;
			_shakeAge += dt;
			StepDebris(view, dt);
			for (int num = _falling.Count - 1; num >= 0; num--)
			{
				Falling falling = _falling[num];
				falling.Age += dt;
				if (falling.Age > 3.3999999f)
				{
					_falling.RemoveAt(num);
				}
				else
				{
					float num2 = Mathf.Clamp01(falling.Age / 1f);
					float num3 = num2 * num2 * 88f;
					if (falling.Age > 1f)
					{
						float num4 = falling.Age - 1f;
						num3 = 88f - 6f * Mathf.Exp((0f - num4) * 6f) * Mathf.Abs(Mathf.Sin(num4 * 14f));
						if (!falling.Landed)
						{
							falling.Landed = true;
							LandingDust(falling, 12);
						}
					}
					float num5 = ((falling.Age > 2.6f) ? ((falling.Age - 2.6f) * falling.Width * 0.6f) : 0f);
					if (falling.Mesh != null)
					{
						float angle = num3 * (MathF.PI / 180f);
						Mat3 a = Mat3.Rotation(falling.Axis, angle);
						falling.Mesh.Place(falling.X, falling.Y, falling.Ground, falling.Angle, falling.MeshScale, falling.MeshScale, falling.MeshScale);
						falling.Mesh.Basis = a * falling.Mesh.Basis;
						falling.Mesh.Position = new Vec3(falling.X, falling.Y, falling.Ground - num5);
						float num6 = falling.X - view.X;
						float num7 = falling.Y - view.Y;
						falling.Mesh.SortDepth = Mathf.Sqrt(num6 * num6 + num7 * num7);
						Meshes.Add(falling.Mesh);
					}
					else
					{
						VoxelObject item = VoxelObject.Standing(falling.Image.Tree, falling.X, falling.Y, falling.Bottom, falling.Width, falling.Height, falling.Image.PivotX, Mathf.Cos(falling.Angle), Mathf.Sin(falling.Angle));
						float radians = num3 * (MathF.PI / 180f);
						Vec3 vec = new Vec3(falling.X, falling.Y, falling.Ground);
						item.U = Vec3.Rotate(item.U, falling.Axis, radians);
						item.V = Vec3.Rotate(item.V, falling.Axis, radians);
						item.N = Vec3.Rotate(item.N, falling.Axis, radians);
						item.Origin = vec + Vec3.Rotate(item.Origin - vec, falling.Axis, radians) - new Vec3(0f, 0f, num5);
						float num8 = falling.X - view.X;
						float num9 = falling.Y - view.Y;
						item.SortDepth = Mathf.Sqrt(num8 * num8 + num9 * num9);
						Voxels.Add(item);
					}
				}
			}
			for (int num10 = _chips.Count - 1; num10 >= 0; num10--)
			{
				Chip chip = _chips[num10];
				chip.Age += dt;
				if (chip.Age > chip.Life)
				{
					_chips.RemoveAt(num10);
				}
				else
				{
					chip.V += new Vec3(0f, 0f, -7f * dt);
					chip.P += chip.V * dt;
					float num11 = terrain.SampleHeight(chip.P.X, chip.P.Y);
					if (chip.P.Z < num11 && chip.Crumble)
					{
						Crumbs(chip, num11);
						_chips.RemoveAt(num10);
					}
					else
					{
						if (chip.P.Z < num11)
						{
							chip.P = new Vec3(chip.P.X, chip.P.Y, num11);
							chip.V = new Vec3(chip.V.X * 0.4f, chip.V.Y * 0.4f, (0f - chip.V.Z) * 0.25f);
							chip.Spin *= 0.5f;
						}
						float num12 = chip.Age * chip.Spin * 6f;
						VoxelObject item2 = new VoxelObject
						{
							Shape = chip.Shape,
							Tag = -1,
							NoShadow = true
						};
						item2.U = new Vec3(Mathf.Cos(num12), Mathf.Sin(num12), 0f);
						item2.V = Vec3.Rotate(new Vec3(0f, 0f, 1f), item2.U, num12 * 0.7f);
						item2.N = Vec3.Cross(item2.U, item2.V);
						item2.SizeU = (item2.SizeV = chip.Size);
						item2.SizeN = chip.Size * 0.5f;
						item2.Origin = chip.P - item2.U * (chip.Size * 0.5f);
						float num13 = chip.P.X - view.X;
						float num14 = chip.P.Y - view.Y;
						item2.SortDepth = Mathf.Sqrt(num13 * num13 + num14 * num14);
						Voxels.Add(item2);
					}
				}
			}
		}

		private void LandingDust(Falling f, int count)
		{
			Vec3 vec = Vec3.Cross(f.Axis, new Vec3(0f, 0f, 1f));
			Vec3 vec2 = new Vec3(f.X, f.Y, f.Ground) + vec * (f.Height * 0.7f);
			for (int i = 0; i < count; i++)
			{
				if (_chips.Count >= 80)
				{
					break;
				}
				uint num = RandomColor(f.Image, f.Image.OpaqueBottom, f.Image.OpaqueTop);
				if (num != 0)
				{
					_chips.Add(new Chip
					{
						P = vec2 + new Vec3((float)(_chipRandom.NextDouble() - 0.5) * f.Width, (float)(_chipRandom.NextDouble() - 0.5) * f.Width, 0.05f),
						V = new Vec3((float)(_chipRandom.NextDouble() - 0.5) * 1.5f, (float)(_chipRandom.NextDouble() - 0.5) * 1.5f, 1f + (float)_chipRandom.NextDouble() * 1.5f),
						Life = 0.8f + (float)_chipRandom.NextDouble() * 0.6f,
						Size = 0.04f + (float)_chipRandom.NextDouble() * 0.04f,
						Spin = (float)_chipRandom.NextDouble() * 6f,
						Shape = ChipShape(num)
					});
				}
			}
		}

		private void CollectProjectiles(MapBox world, ViewState view, float range, TerrainCache terrain, SpriteCache sprites)
		{
			if (world.projectiles == null)
			{
				return;
			}
			float num = range * range;
			List<Projectile> list = world.projectiles.list;
			for (int i = 0; i < list.Count; i++)
			{
				Projectile projectile = list[i];
				if (projectile == null || projectile.asset == null || projectile.asset.frames == null || projectile.asset.frames.Length == 0)
				{
					continue;
				}
				Vector3 current_position_3d = projectile._current_position_3d;
				float num2 = current_position_3d.x - view.X;
				float num3 = current_position_3d.y - view.Y;
				if (num2 * num2 + num3 * num3 > num)
				{
					continue;
				}
				float num4 = terrain.SampleHeight(current_position_3d.x, current_position_3d.y);
				float num5 = FirstPersonShots.BaseZ(projectile, num4);
				if (DrawCannonShot(projectile, current_position_3d, num5, Mathf.Sqrt(num2 * num2 + num3 * num3), terrain) || (projectile.asset.id == "shotgun_bullet" && DrawPellet(projectile, current_position_3d, num5, Mathf.Sqrt(num2 * num2 + num3 * num3))) || (projectile.asset.id == "arrow" && DrawArrow(projectile, current_position_3d, num5, Mathf.Sqrt(num2 * num2 + num3 * num3))) || DrawThrown(projectile, current_position_3d, num5, Mathf.Sqrt(num2 * num2 + num3 * num3)))
				{
					continue;
				}
				Sprite sprite = (projectile.asset.animated ? AnimationHelper.getSpriteFromList(projectile.GetHashCode(), projectile.asset.frames, projectile.asset.animation_speed) : projectile.asset.frames[0]);
				SpriteImage spriteImage = sprites.Get(sprite);
				if (spriteImage != null)
				{
					float num6 = Mathf.Max(0.05f, projectile.getCurrentScale());
					num6 *= Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(Mathf.Sqrt(num2 * num2 + num3 * num3) / 4f));
					float num7 = spriteImage.UnitHeight * num6;
					float width = spriteImage.UnitWidth * num6;
					bool draw_light_area = projectile.asset.draw_light_area;
					Billboards.Add(new Billboard
					{
						X = current_position_3d.x,
						Y = current_position_3d.y,
						Z = num5 + current_position_3d.z - num7 * 0.5f,
						GroundZ = num4,
						Width = width,
						Height = num7,
						Image = spriteImage,
						Kind = BillboardKind.Facing,
						Tag = -1,
						Emissive = draw_light_area
					});
					if (draw_light_area)
					{
						uint num8 = GlowColor(spriteImage);
						float num9 = Mathf.Clamp(projectile.asset.draw_light_size, 0.3f, 3f);
						AddLight(current_position_3d.x, current_position_3d.y, num5 + current_position_3d.z, 2.5f + num9 * 2f, (float)(num8 & 0xFF) / 255f, (float)((num8 >> 8) & 0xFF) / 255f, (float)((num8 >> 16) & 0xFF) / 255f, 0.12f);
					}
				}
			}
		}

		private void CollectEffects(MapBox world, ViewState view, TerrainCache terrain, SpriteCache sprites)
		{
			StackEffects stack_effects = world.stack_effects;
			if (stack_effects == null || stack_effects.list == null)
			{
				return;
			}
			float num = SkyRange(40f) * SkyRange(40f);
			foreach (BaseEffectController item in stack_effects.list)
			{
				if (item == null || item._list == null || (item.asset != null && item.asset.id == "fx_tile_effect"))
				{
					continue;
				}
				bool flag = item == stack_effects.controller_slash_effects;
				EffectAsset asset = item.asset;
				string text = asset?.id;
				if (text != null && MapIcons.Contains(text))
				{
					continue;
				}
				float value;
				float num2 = ((text != null && SmallEffects.TryGetValue(text, out value)) ? value : float.MaxValue);
				bool flag2 = asset?.draw_light_area ?? false;
				bool flag3 = asset != null && asset.id != null && asset.id.Contains("lightning");
				bool flag4 = text != null && (IsBlastSprite(text) || text == "fx_explosion_wave" || text == "fx_fire_smoke");
				foreach (BaseEffect item2 in item._list)
				{
					if (item2 == null || !item2.active || ((flag || text == "fx_firebomb_explosion") && CannonTakes(item2, flag)) || (flag && Drill.TakesSlash(item2.transform.position)) || (flag4 && TakeBlastEffect(text, item2, terrain)) || BlockSprite(text))
					{
						continue;
					}
					if (MonolithSprite(text))
					{
						NoteMonolithFlash(item2, sprites);
					}
					else if (WaypointSprite(text))
					{
						NoteWaypointCall(item2);
					}
					else if (item2 is Meteorite m)
					{
						AddMeteor(m, view, terrain);
					}
					else if (item2 is Boulder b)
					{
						AddBoulder(b, terrain);
					}
					else
					{
						if (item2.sprite_renderer == null)
						{
							continue;
						}
						SpriteRenderer sprite_renderer = item2.sprite_renderer;
						if (item2 is Cloud cloud)
						{
							AddStorm(cloud, sprite_renderer);
						}
						else if (item2 is TornadoEffect t)
						{
							AddTornado(t, view, terrain);
						}
						else
						{
							if (!sprite_renderer.enabled || sprite_renderer.color.a < 0.2f)
							{
								continue;
							}
							Vector3 position = item2.transform.position;
							float num3 = position.x - view.X;
							float num4 = position.y - view.Y;
							if (flag3)
							{
								AddBolt(item2, text, position, terrain, num3 * num3 + num4 * num4);
							}
							else
							{
								if (num3 * num3 + num4 * num4 > num)
								{
									continue;
								}
								SpriteImage spriteImage = sprites.Get(sprite_renderer.sprite);
								if (spriteImage == null)
								{
									continue;
								}
								Vector3 lossyScale = item2.transform.lossyScale;
								float num5 = spriteImage.UnitWidth * Mathf.Abs(lossyScale.x);
								float num6 = spriteImage.UnitHeight * Mathf.Abs(lossyScale.y);
								float num7 = Mathf.Sqrt(num3 * num3 + num4 * num4);
								if (num2 < float.MaxValue)
								{
									if (num7 < 1.2f)
									{
										if (flag)
										{
											continue;
										}
										switch (text)
										{
										case "fx_walk":
										case "fx_jump":
										case "fx_slash":
											continue;
										}
									}
									float num8 = Mathf.Max(num5, num6);
									if (num8 > num2)
									{
										num5 *= num2 / num8;
										num6 *= num2 / num8;
									}
								}
								else
								{
									float num9 = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(num7 / 4f));
									num5 *= num9;
									num6 *= num9;
								}
								if (EffectTrace != null && num7 < 8f)
								{
									EffectTrace.Add($"{text} {num5:F2}x{num6:F2} at {num7:F1}");
								}
								float num10 = terrain.SampleHeight(position.x, position.y);
								Billboards.Add(new Billboard
								{
									X = position.x,
									Y = position.y,
									Z = (flag ? (num10 + 0.5f - num6 * 0.5f) : (num10 - spriteImage.BottomPadding * num6)),
									GroundZ = num10,
									Width = num5,
									Height = num6,
									Image = spriteImage,
									Kind = BillboardKind.Facing,
									Flip = sprite_renderer.flipX,
									Tag = -1,
									Emissive = flag2
								});
								if (flag2)
								{
									uint num11 = GlowColor(spriteImage);
									float num12 = Mathf.Clamp(asset.draw_light_size, 0.3f, 3f);
									AddLight(position.x + asset.draw_light_area_offset_x, position.y + asset.draw_light_area_offset_y, num10 + num6 * 0.4f, 2.5f + num12 * 2.5f, (float)(num11 & 0xFF) / 255f, (float)((num11 >> 8) & 0xFF) / 255f, (float)((num11 >> 16) & 0xFF) / 255f, 0.15f);
								}
							}
						}
					}
				}
			}
		}

		private void AddMeteor(Meteorite m, ViewState view, TerrainCache terrain)
		{
			Vector3 position = m.transform.position;
			float ground = terrain.SampleHeight(position.x, position.y);
			MeteorPath(m, position, ground, out var head, out var back);
			NoteMeteorSlide(m, position);
			float num = position.x - view.X;
			float num2 = position.y - view.Y;
			float num3 = SkyRange(160f);
			if (!(num * num + num2 * num2 > num3 * num3))
			{
				DrawMeteorFx(m, head, back, _frameDt);
			}
		}

		private void AddLight(float x, float y, float z, float radius, float r, float g, float b, float glow)
		{
			Lights.Add(new PointLight
			{
				X = x,
				Y = y,
				Z = z,
				Radius = radius,
				R = r,
				G = g,
				B = b,
				Glow = glow
			});
		}

		private float Flicker(float seed)
		{
			return 0.84f + 0.16f * Mathf.Sin(_clock * 11f + seed) * Mathf.Sin(_clock * 7.3f + seed * 2.1f);
		}

		private uint GlowColor(SpriteImage img)
		{
			if (_glowColors.TryGetValue(img, out var value))
			{
				return value;
			}
			if (_glowColors.Count > 512)
			{
				_glowColors.Clear();
			}
			int num = 0;
			uint[] pixels = img.Pixels;
			foreach (uint num2 in pixels)
			{
				if (num2 >> 24 >= 128)
				{
					num = Math.Max(num, (int)((num2 & 0xFF) + ((num2 >> 8) & 0xFF) + ((num2 >> 16) & 0xFF)));
				}
			}
			long num3 = 0L;
			long num4 = 0L;
			long num5 = 0L;
			long num6 = 0L;
			pixels = img.Pixels;
			foreach (uint num7 in pixels)
			{
				if (num7 >> 24 >= 128 && !((float)(int)((num7 & 0xFF) + ((num7 >> 8) & 0xFF) + ((num7 >> 16) & 0xFF)) < (float)num * 0.8f))
				{
					num3 += num7 & 0xFF;
					num4 += (num7 >> 8) & 0xFF;
					num5 += (num7 >> 16) & 0xFF;
					num6++;
				}
			}
			if (num6 == 0L)
			{
				value = TerrainCache.Rgb(FireLight.r, FireLight.g, FireLight.b);
			}
			else
			{
				float num8 = Math.Max(1f, (float)Math.Max(num3, Math.Max(num4, num5)) / (float)num6);
				value = TerrainCache.Rgb((int)((float)(num3 / num6) * 255f / num8), (int)((float)(num4 / num6) * 255f / num8), (int)((float)(num5 / num6) * 255f / num8));
			}
			_glowColors[img] = value;
			return value;
		}

		private void BuildingLight(NearBuilding nb, Building b, Vector2 p, float ground)
		{
			BuildingAsset asset = b.asset;
			bool flag = asset.id != null && asset.id.Contains("bonfire");
			if (!flag && _night < 0.04f)
			{
				return;
			}
			try
			{
				if (!b.isUsable() || b.isAbandoned() || (asset.hasHousingSlots() && !b.hasResidents()))
				{
					return;
				}
			}
			catch
			{
				return;
			}
			float num = Mathf.Clamp(asset.draw_light_size, 0.3f, 3f);
			float num2 = (0.2f + 0.8f * _night) * Flicker(p.x * 3.1f + p.y);
			if (flag)
			{
				num2 = (0.45f + 0.55f * _night) * Flicker(p.x + p.y * 1.7f);
				AddLight(p.x, p.y, ground + 0.45f, 7f, 1.15f * num2, 0.66f * num2, 0.28f * num2, 0.3f);
			}
			else if (nb.Look == BuildingLook.House)
			{
				float y = p.y + nb.CenterOffset - nb.DepthTiles * 0.5f - 0.3f;
				bool flag2 = nb.Mesh != null && nb.Mesh.Model != null && FxOf(nb.Mesh.Model).Ghostly > 0f;
				AddLight(p.x, y, ground + Mathf.Min(1.1f, nb.Height * 0.35f), Mathf.Clamp(3.5f + num * 2.5f, 3.5f, 8f), (flag2 ? 0.45f : 1f) * num2, (flag2 ? 0.9f : 0.66f) * num2, (flag2 ? 0.8f : 0.34f) * num2, 0.1f);
			}
			else
			{
				uint num3 = GlowColor(nb.Image);
				AddLight(p.x, p.y, ground + nb.Height * 0.5f, Mathf.Clamp(2.5f + num * 2f, 2.5f, 7f), (float)(num3 & 0xFF) / 255f * num2 * 0.8f, (float)((num3 >> 8) & 0xFF) / 255f * num2 * 0.8f, (float)((num3 >> 16) & 0xFF) / 255f * num2 * 0.8f, 0f);
			}
		}

		private static bool IsBurning(BaseSimObject o)
		{
			try
			{
				return o.hasAnyStatusEffect() && o.hasStatus("burning");
			}
			catch
			{
				return false;
			}
		}

		private void BuildingBlaze(NearBuilding nb, Building b, Vector2 p, float ground, float dist2)
		{
			if (nb.Image != null)
			{
				float num = nb.Height * nb.Image.VisibleFraction;
				bool flag = nb.Look == BuildingLook.Tree;
				float x = p.x;
				float num2 = p.y + ((nb.Look == BuildingLook.House) ? nb.CenterOffset : 0f);
				float num3 = nb.Width * (flag ? 0.28f : 0.36f);
				float num4 = ((nb.Look == BuildingLook.House) ? nb.DepthTiles : nb.Width) * (flag ? 0.28f : 0.36f);
				float num5 = _eyeX - x;
				float num6 = _eyeY - num2;
				float num7 = Mathf.Sqrt(dist2) + 0.001f;
				float num8 = Mathf.Min(num3, num4) * 0.6f;
				x += num5 / num7 * num8;
				num2 += num6 / num7 * num8;
				float z = ground + (flag ? (num * 0.35f) : (num * 0.15f));
				float z2 = ground + num * (flag ? 0.92f : 0.8f);
				int count = Mathf.Clamp((int)(nb.Width * num * ((dist2 < 400f) ? 1.6f : 0.8f)), 3, (dist2 < 400f) ? 18 : 8);
				uint hashCode = (uint)b.GetHashCode();
				FireField.AddBlaze(Billboards, x, num2, num3, num4, z, z2, count, Mathf.Clamp(0.45f + num * 0.22f, 0.6f, 2.4f), hashCode, _clock);
				FireField.AddPlume(Billboards, p.x, p.y, ground + num * 0.95f, Mathf.Clamp(nb.Width * 0.5f, 0.5f, 3f), (dist2 < 900f) ? 5 : 3, hashCode, _clock, 1f);
				if (dist2 < 2304f)
				{
					float num9 = Flicker(p.x * 1.7f + p.y) * (0.4f + 0.6f * _night);
					AddLight(p.x, p.y, ground + num * 0.6f, Mathf.Clamp(4f + nb.Width * 1.5f, 5f, 12f), 1.05f * num9, 0.5f * num9, 0.16f * num9, 0.25f);
				}
			}
		}

		private void UnitLight(Actor a, float x, float y, float z)
		{
			try
			{
				if (a.has_tag_generate_light || Mutations.Glows(a))
				{
					AddLight(x, y, z, 3.2f, 0.82f, 0.86f, 0.72f, 0.1f);
				}
				else
				{
					if (!((BaseSimObject)a).hasAnyStatusEffect())
					{
						return;
					}
					{
						foreach (Status status in a.getStatuses())
						{
							if (status != null && status.asset != null && status.asset.draw_light_area)
							{
								float num = Flicker(x * 2.3f + y);
								float num2 = Mathf.Clamp(status.asset.draw_light_size, 0.3f, 3f);
								AddLight(x, y, z, 2.5f + num2 * 2f, 1.15f * num, 0.6f * num, 0.22f * num, 0.12f);
								break;
							}
						}
						return;
					}
				}
			}
			catch
			{
			}
		}

		private void CollectTileLights(ViewState view, TerrainCache t)
		{
			int num = (int)view.X;
			int num2 = (int)view.Y;
			for (int i = (num2 - 28) / 4; i <= (num2 + 28) / 4; i++)
			{
				for (int j = (num - 28) / 4; j <= (num + 28) / 4; j++)
				{
					CellLight(t, j * 4, i * 4, 4, 4, 1);
				}
			}
			for (int k = (num2 - 40) / 8; k <= (num2 + 40) / 8; k++)
			{
				for (int l = (num - 40) / 8; l <= (num + 40) / 8; l++)
				{
					CellLight(t, l * 8, k * 8, 8, 2, 4);
				}
			}
		}

		private void CellLight(TerrainCache t, int x0, int y0, int size, byte flag, int minCount)
		{
			if (x0 + size <= 0 || y0 + size <= 0 || x0 >= t.Width || y0 >= t.Height)
			{
				return;
			}
			float num = 0f;
			float num2 = 0f;
			int num3 = 0;
			for (int i = Math.Max(0, y0); i < Math.Min(t.Height, y0 + size); i++)
			{
				for (int j = Math.Max(0, x0); j < Math.Min(t.Width, x0 + size); j++)
				{
					if ((t.Flags[i * t.Width + j] & flag) != 0)
					{
						num += (float)j + 0.5f;
						num2 += (float)i + 0.5f;
						num3++;
					}
				}
			}
			if (num3 >= minCount)
			{
				float num4 = num / (float)num3;
				float num5 = num2 / (float)num3;
				float num6 = t.SampleHeight(num4, num5);
				if (flag == 4)
				{
					float num7 = Flicker(num4 * 1.3f + num5) * (0.3f + 0.7f * _night);
					AddLight(num4, num5, num6 + 0.7f, Mathf.Min(9f, 3.5f + (float)num3 * 0.4f), 1f * num7, 0.46f * num7, 0.15f * num7, 0.15f);
				}
				else
				{
					AddLight(num4, num5, num6 + 1f, Mathf.Min(12f, 5f + (float)num3 * 0.12f), 1.1f, 0.42f, 0.12f, 0f);
				}
			}
		}

		private void CollectFireflies(ViewState view, TerrainCache t, float dt)
		{
			if (_night < 0.45f || t.Width == 0 || Chill > 0.7f)
			{
				_fliesPlaced = false;
				return;
			}
			float num = (_night - 0.45f) / 0.55f * (1f - Chill / 0.7f);
			for (int i = 0; i < _flies.Length; i++)
			{
				ref Firefly reference = ref _flies[i];
				float num2 = reference.X - view.X;
				float num3 = reference.Y - view.Y;
				if (!_fliesPlaced || num2 * num2 + num3 * num3 > 256f)
				{
					float f = (float)_chipRandom.NextDouble() * MathF.PI * 2f;
					float num4 = 3f + (float)_chipRandom.NextDouble() * 12f;
					reference.X = view.X + Mathf.Cos(f) * num4;
					reference.Y = view.Y + Mathf.Sin(f) * num4;
					reference.Z = 0.3f + (float)_chipRandom.NextDouble() * 1.4f;
					reference.Phase = (float)_chipRandom.NextDouble() * 20f;
					reference.Speed = 0.3f + (float)_chipRandom.NextDouble() * 0.4f;
				}
				reference.Phase += dt;
				reference.X += Mathf.Sin(reference.Phase * reference.Speed * 1.3f + (float)i) * dt * 0.5f;
				reference.Y += Mathf.Cos(reference.Phase * reference.Speed + (float)i * 2.1f) * dt * 0.5f;
				int num5 = (int)reference.X;
				int num6 = (int)reference.Y;
				if (num5 >= 0 && num6 >= 0 && num5 < t.Width && num6 < t.Height && t.Materials[num6 * t.Width + num5] == 1 && (t.Flags[num6 * t.Width + num5] & 1) == 0)
				{
					float num7 = Mathf.Sin(reference.Phase * 1.7f + (float)i);
					if (!(num7 < 0.2f))
					{
						float num8 = (num7 - 0.2f) / 0.8f * num;
						float num9 = t.SampleHeight(reference.X, reference.Y);
						AddLight(reference.X, reference.Y, num9 + reference.Z + Mathf.Sin(reference.Phase * 0.9f) * 0.15f, 1.2f, 0.5f * num8, 0.8f * num8, 0.25f * num8, 0.035f);
					}
				}
			}
			_fliesPlaced = true;
		}

		private void TrimLights(ViewState view)
		{
			if (Lights.Count > 40)
			{
				float ex = view.X;
				float ey = view.Y;
				Lights.Sort(delegate(PointLight a, PointLight b)
				{
					float num = (a.X - ex) * (a.X - ex) + (a.Y - ey) * (a.Y - ey) - a.Radius * a.Radius;
					float value = (b.X - ex) * (b.X - ex) + (b.Y - ey) * (b.Y - ey) - b.Radius * b.Radius;
					return num.CompareTo(value);
				});
				Lights.RemoveRange(40, Lights.Count - 40);
			}
		}

		private void Rescan(Vector2 eye, float range)
		{
			_lastScanAt = eye;
			_framesSinceScan = 0;
			Dictionary<Building, NearBuilding> previousNear = _previousNear;
			previousNear.Clear();
			foreach (NearBuilding building2 in _buildings)
			{
				if (building2.Building != null)
				{
					previousNear[building2.Building] = building2;
				}
			}
			_buildings.Clear();
			float num = range * range;
			List<Building> simpleList = World.world.buildings.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Building building = simpleList[i];
				if (building == null)
				{
					continue;
				}
				WorldTile current_tile = building.current_tile;
				if (current_tile == null)
				{
					continue;
				}
				float num2 = current_tile.posV3.x - eye.x;
				float num3 = current_tile.posV3.y - eye.y;
				if (!(num2 * num2 + num3 * num3 > num) && building.isAlive())
				{
					if (previousNear.TryGetValue(building, out var value) && (value.LookTile != building.current_tile || (value.LookAsset != building.asset && AssetStyle.Classify(building.asset) != value.Look)))
					{
						value = null;
					}
					if (value != null)
					{
						value.LookAsset = building.asset;
					}
					else
					{
						value = NewNear(building);
					}
					_buildings.Add(value);
				}
			}
			previousNear.Clear();
			IndexBuildingCells();
		}

		private static NearBuilding NewNear(Building b)
		{
			WorldTile current_tile = b.current_tile;
			NearBuilding nearBuilding = new NearBuilding
			{
				Building = b,
				Look = AssetStyle.Classify(b.asset),
				LookAsset = b.asset,
				LookTile = current_tile,
				Angle = (float)(TerrainModel.Hash(current_tile.x, current_tile.y) & 0x3FF) / 1023f * MathF.PI * 2f
			};
			BuildingFundament buildingFundament = ((b.asset != null) ? b.asset.fundament : null);
			if (buildingFundament != null)
			{
				nearBuilding.DepthTiles = Mathf.Max(1f, buildingFundament.top + buildingFundament.bottom + 1);
				nearBuilding.CenterOffset = (float)(buildingFundament.top - buildingFundament.bottom) * 0.5f;
			}
			return nearBuilding;
		}

		public void Perches(Vector2 at, float range, List<Vector3> into)
		{
			into.Clear();
			float num = range * range;
			for (int i = 0; i < _buildings.Count; i++)
			{
				NearBuilding nearBuilding = _buildings[i];
				if (nearBuilding.Look != BuildingLook.Tree)
				{
					continue;
				}
				Building building = nearBuilding.Building;
				if (building == null || !building.isAlive() || building.current_tile == null || building.isRuin())
				{
					continue;
				}
				string text = ((building.asset != null) ? (building.asset.id ?? "") : "");
				if (!text.Contains("dead") && !text.Contains("burn") && !text.Contains("corrupt") && !text.Contains("infernal") && !text.Contains("wasteland") && !text.Contains("singularity") && !text.Contains("paradox") && !text.Contains("tumor") && !text.Contains("biomass") && !text.Contains("cyber"))
				{
					Vector2 vector = new Vector2(building.current_tile.posV3.x, building.current_tile.posV3.y);
					if (!((vector - at).sqrMagnitude > num))
					{
						into.Add(new Vector3(vector.x, vector.y, Mathf.Max(1f, nearBuilding.Height * 0.7f)));
					}
				}
			}
		}

		private static Vector2 Position(Building b)
		{
			WorldTile current_tile = b.current_tile;
			if (current_tile != null)
			{
				return new Vector2(current_tile.posV3.x, current_tile.posV3.y);
			}
			Vector3 cur_transform_position = ((BaseSimObject)b).cur_transform_position;
			return new Vector2(cur_transform_position.x - b._shake_offset.x, cur_transform_position.y - b._shake_offset.y);
		}

		private static Vector2 Worked(Building b, BuildingLook look)
		{
			if (look == BuildingLook.House || look == BuildingLook.Landform)
			{
				return Vector2.zero;
			}
			float shake_timer = b._shake_timer;
			if (shake_timer <= 0f)
			{
				return Vector2.zero;
			}
			float num = Mathf.Clamp01(shake_timer / 0.3f);
			float num2 = (1f - num) * 0.3f;
			return new Vector2(Mathf.Sin(num2 * 38f), Mathf.Cos(num2 * 29f)) * (0.025f * num);
		}

		private void UpdateBuildingSprite(NearBuilding nb, SpriteCache sprites)
		{
			Building building = nb.Building;
			if (building == null || !building.isAlive() || _broken.Contains(building))
			{
				return;
			}
			try
			{
				SpriteImage spriteImage = (SpriteMemo ? MemoImage(ref nb.Memo.Sprite, ref nb.Memo.Image, ref nb.Memo.Epoch, building.checkSpriteToRender(), sprites) : sprites.Get(building.checkSpriteToRender()));
				if (spriteImage == null)
				{
					if (nb.ImageAsset != building.asset)
					{
						nb.Image = null;
					}
					return;
				}
				Vector3 vector = ((building.asset != null) ? building.asset.scale_base : building.current_scale);
				bool num = spriteImage != nb.Image;
				nb.Image = spriteImage;
				nb.ImageAsset = building.asset;
				float mapHeight = spriteImage.UnitHeight * Mathf.Abs(vector.y);
				float num2 = ((nb.Look == BuildingLook.Tree) ? TreeGrow(mapHeight) : ((nb.Look == BuildingLook.Plant) ? 1.25f : 1f));
				nb.Width = spriteImage.UnitWidth * Mathf.Abs(vector.x) * ((nb.Look == BuildingLook.Tree) ? TreeWiden(mapHeight) : num2);
				nb.Height = spriteImage.UnitHeight * Mathf.Abs(vector.y) * num2;
				if (num && nb.Look == BuildingLook.House)
				{
					nb.StandingWidth = ((building.isRuin() || building.isUnderConstruction()) ? StandingWidth(building, sprites, nb.Width) : nb.Width);
				}
				nb.Solid = AssetStyle.IsSolid(building);
				if (num && nb.Solid && nb.Look != BuildingLook.House)
				{
					nb.Radius = ((nb.Look == BuildingLook.Tree) ? Mathf.Clamp(Colliders.BaseRadius(spriteImage, nb.Width, 0.25f) * 0.7f, 0.08f, 0.32f) : Mathf.Clamp(Colliders.BaseRadius(spriteImage, nb.Width, 0.4f) * 0.85f, 0.1f, 1.5f));
				}
			}
			catch (Exception e)
			{
				MarkBroken(building, (building.asset != null) ? building.asset.id : "", e);
			}
		}

		private void MarkBroken(BaseSimObject obj, string assetId, Exception e)
		{
			if (_broken.Count > 5000)
			{
				_broken.Clear();
			}
			_broken.Add(obj);
			if (_reportedAssets.Add(assetId))
			{
				Log.Error("skipping a " + assetId + " whose sprite failed (other objects of this kind still show): " + e.Message);
			}
		}

		private void AddDragon(Actor a, ViewState view, TerrainCache terrain, float dt)
		{
			BeginDragons();
			float num = a.current_position.x - view.X;
			float num2 = a.current_position.y - view.Y;
			if (!(num * num + num2 * num2 > 67600f))
			{
				bool draw = InView(num, num2, 34f);
				int count = Tagged.Count;
				uint flash = 0u;
				if (RecentHits.TryGetValue(a, out var value))
				{
					flash = ((uint)(220f * (1f - value / 0.3f)) << 24) | 0xFFFFFF;
				}
				if (Dragons.Add(eye: new Vec3(view.X, view.Y, view.Z), meshes: Meshes, bills: Billboards, lights: Lights, a: a, terrain: terrain, step: WorldStep(dt), draw: draw, tag: count, flash: flash, clock: _clock, night: _night))
				{
					Tagged.Add(a);
				}
			}
		}

		private void BeginDragons()
		{
			if (_dragonFrame != _frame)
			{
				_dragonFrame = _frame;
				Dragons.Tick();
			}
		}

		private void StepDragonDust(ViewState view, TerrainCache terrain, float dt)
		{
			BeginDragons();
			Vec3 eye = new Vec3(view.X, view.Y, view.Z);
			if (Models.Ready && Dragons.Ready)
			{
				Dragons.AddFallen(Meshes, Billboards, Lights, eye, terrain, WorldStep(dt), _clock, _night);
			}
			Dragons.DrawMotes(Billboards, eye, terrain, WorldStep(dt));
		}

		private bool DrillYard(NearBuilding nb, Building b, MeshInstance inst)
		{
			double now;
			FirstPerson.Core.Drill.Yard yard = Drill.Drawn(b, inst, out now);
			if (yard == null)
			{
				return false;
			}
			for (int i = 0; i < yard.Struck.Count; i++)
			{
				var (post, vec) = yard.Struck[i];
				StrawChips(nb, yard.Straw(post), new Vector2(vec.X, vec.Y), 3);
			}
			yard.Struck.Clear();
			bool num = yard.Pose(inst);
			yard.DrawShafts(Meshes, Models.Get("arrow"), now, inst.Tag, inst.SortDepth);
			if (!num)
			{
				return yard.Shafts.Count > 0;
			}
			return true;
		}

		private void StrawChips(NearBuilding nb, Vec3 at, Vector2 from, int count)
		{
			SpriteImage image = nb.Image;
			if (image == null || new Vector2(at.X - _eyeX, at.Y - _eyeY).sqrMagnitude > 1600f)
			{
				return;
			}
			Vector2 vector = new Vector2(from.x - at.X, from.y - at.Y);
			vector = ((vector.sqrMagnitude > 1E-06f) ? vector.normalized : new Vector2(0f, -1f));
			Vec3 vec = new Vec3(0f - vector.y, vector.x, 0f);
			for (int i = 0; i < count; i++)
			{
				if (_chips.Count >= 80)
				{
					break;
				}
				uint num = 0u;
				for (int j = 0; j < 6; j++)
				{
					if (num != 0)
					{
						break;
					}
					uint num2 = RandomColor(image, image.OpaqueBottom, image.OpaqueTop);
					int num3 = (int)(num2 & 0xFF);
					int num4 = (int)((num2 >> 8) & 0xFF);
					int num5 = (int)((num2 >> 16) & 0xFF);
					if (num2 != 0 && num3 > num5 + 50 && num4 > num5 + 20)
					{
						num = num2;
					}
				}
				if (num != 0)
				{
					float num6 = (float)(_chipRandom.NextDouble() - 0.5) * 1.4f;
					Vec3 v = new Vec3(vector.x, vector.y, 0f) * (0.5f + (float)_chipRandom.NextDouble() * 0.7f) + vec * num6 + new Vec3(0f, 0f, 0.6f + (float)_chipRandom.NextDouble() * 1f);
					_chips.Add(new Chip
					{
						P = at + new Vec3(vector.x, vector.y, 0f) * 0.12f,
						V = v,
						Life = 0.7f + (float)_chipRandom.NextDouble() * 0.5f,
						Size = 0.025f + (float)_chipRandom.NextDouble() * 0.025f,
						Spin = (float)_chipRandom.NextDouble() * 6f,
						Shape = ChipShape(num)
					});
				}
			}
		}

		private void AddFarActor(Actor a, Vector2 p, float dx, float dy, TerrainCache terrain, SpriteCache sprites)
		{
			if (InView(dx, dy, 2f))
			{
				SpriteImage spriteImage;
				try
				{
					spriteImage = sprites.Get(a.getSpriteToRender());
				}
				catch
				{
					return;
				}
				if (spriteImage != null)
				{
					Vector3 current_scale = a.current_scale;
					float width = spriteImage.UnitWidth * Mathf.Abs(current_scale.x);
					float num = spriteImage.UnitHeight * Mathf.Abs(current_scale.y) * spriteImage.VisibleFraction;
					float floor;
					float z = (IsSwimming(a) ? (terrain.LiquidSurface(p.x, p.y) + terrain.WaveAt(p.x, p.y) - num * 0.4f) : ((Wading.DrawnFloor(terrain, a, p.x, p.y, out floor) ? floor : terrain.SampleHeight(p.x, p.y)) + a.position_height));
					FarActors.Add(new FarActor
					{
						X = p.x,
						Y = p.y,
						Z = z,
						Width = width,
						Height = num,
						Color = AverageColor(spriteImage)
					});
				}
			}
		}

		private uint AverageColor(SpriteImage image)
		{
			if (_averageColor.TryGetValue(image, out var value))
			{
				return value;
			}
			long num = 0L;
			long num2 = 0L;
			long num3 = 0L;
			long num4 = 0L;
			uint[] pixels = image.Pixels;
			foreach (uint num5 in pixels)
			{
				if (num5 >> 24 >= 128)
				{
					num += num5 & 0xFF;
					num2 += (num5 >> 8) & 0xFF;
					num3 += (num5 >> 16) & 0xFF;
					num4++;
				}
			}
			value = ((num4 == 0L) ? TerrainCache.Rgb(128, 128, 128) : TerrainCache.Rgb((int)(num / num4), (int)(num2 / num4), (int)(num3 / num4)));
			if (_averageColor.Count > 4000)
			{
				_averageColor.Clear();
			}
			_averageColor[image] = value;
			return value;
		}

		private void AddModel(MeshInstance inst)
		{
			if (!_farBuild)
			{
				Meshes.Add(inst);
			}
		}

		private FarModel FarModelFor(Building b, Vector2 p, SpriteCache sprites, bool make)
		{
			if (!_farModels.TryGetValue(b, out var value) || value.Nb.LookTile != b.current_tile || (value.Nb.LookAsset != b.asset && AssetStyle.Classify(b.asset) != value.Nb.Look))
			{
				value = new FarModel
				{
					Nb = NewNear(b)
				};
				_farModels[b] = value;
			}
			value.Nb.LookAsset = b.asset;
			value.X = p.x;
			value.Y = p.y;
			value.Ring = _farRings + 1;
			if (value.Season != SeasonLook.Stamp || value.Asset != b.asset)
			{
				if (make)
				{
					MakeFarModel(value, b, sprites);
				}
				else
				{
					_farLate++;
				}
			}
			if (!value.Ok)
			{
				return null;
			}
			return value;
		}

		private void MakeFarModel(FarModel m, Building b, SpriteCache sprites)
		{
			NearBuilding nb = m.Nb;
			_farMade = true;
			m.Season = SeasonLook.Stamp;
			m.Asset = b.asset;
			m.Ok = false;
			m.Windows = 0f;
			if (!Models.Ready || b.asset == null || _terrain == null)
			{
				return;
			}
			UpdateBuildingSprite(nb, sprites);
			if (nb.Image == null || nb.ImageAsset != b.asset)
			{
				return;
			}
			Vector2 p = (nb.LastPos = new Vector2(m.X, m.Y));
			nb.Wobbling = 0;
			float num = p.x - _eyeX;
			float num2 = p.y - _eyeY;
			_wantCollider = (_wantSolid = (_colliderAdded = false));
			_farWindows = 0f;
			_farBuild = true;
			try
			{
				m.Ok = DrawAsModel(nb, b, p, _terrain.SampleHeight(p.x, p.y), num * num + num2 * num2, -1) && nb.Mesh != null && nb.Mesh.Model != null;
			}
			catch (Exception ex)
			{
				m.Ok = false;
				if (_reportedAssets.Add("far " + b.asset.id))
				{
					Log.Error("far model of a " + b.asset.id + " skipped (its sprite instead): " + ex.Message);
				}
			}
			finally
			{
				_farBuild = false;
			}
			if (m.Ok)
			{
				nb.Mesh.ShadowOnly = false;
				m.Windows = (nb.Mesh.Emissive ? 0f : _farWindows);
			}
		}

		private void DrawFarModel(FarModel m, float night)
		{
			MeshInstance mesh = m.Nb.Mesh;
			if (m.Windows > 0f)
			{
				mesh.Glow = m.Windows * night;
			}
			Meshes.Add(mesh);
			FarModelsDrawn++;
		}

		private void DropFarModel(int i)
		{
			if (_farRingModels && i < _farRingModel.Count && _farRingModel[i] != null)
			{
				_farRingModel[i] = null;
				FarProp value = _farRing[i];
				value.Height = 0f;
				_farRing[i] = value;
			}
		}

		private void FarSpriteChanged(int i, Building b, SpriteCache sprites)
		{
			if (_farRingModels && i < _farRingModel.Count)
			{
				FarModel value = _farRingModel[i];
				if (value != null || _farModels.TryGetValue(b, out value))
				{
					MakeFarModel(value, b, sprites);
					_farRingModel[i] = (value.Ok ? value : null);
				}
			}
		}

		private void RemakeFarModels(SpriteCache sprites)
		{
			if (!_farRingModels || !FarModels)
			{
				return;
			}
			int count = _farRingModel.Count;
			int num = Mathf.Min(count, 8);
			for (int i = 0; i < num; i++)
			{
				_farRemake = (_farRemake + 1) % count;
				FarModel farModel = _farRingModel[_farRemake];
				if (farModel == null)
				{
					continue;
				}
				Building building = _farRingOf[_farRemake];
				if (building == null || building.asset == null || !building.isAlive() || building.chopped)
				{
					DropFarModel(_farRemake);
					continue;
				}
				MakeFarModel(farModel, building, sprites);
				if (!farModel.Ok)
				{
					_farRingModel[_farRemake] = null;
				}
			}
		}

		private void PruneFarModels()
		{
			if (_farModels.Count < _farRingModel.Count + 4096)
			{
				return;
			}
			_farGone.Clear();
			foreach (KeyValuePair<Building, FarModel> farModel in _farModels)
			{
				if (farModel.Value.Ring < _farRings - 1)
				{
					_farGone.Add(farModel.Key);
				}
			}
			foreach (Building item in _farGone)
			{
				_farModels.Remove(item);
			}
			_farGone.Clear();
		}

		internal void FarModelsForTest(List<(Building B, MeshInstance Mesh, float Dist)> into)
		{
			into.Clear();
			if (!_farRingModels || !FarModels)
			{
				return;
			}
			for (int i = 0; i < _farRingModel.Count; i++)
			{
				FarModel farModel = _farRingModel[i];
				if (farModel != null && farModel.Ok)
				{
					into.Add((_farRingOf[i], farModel.Nb.Mesh, Mathf.Sqrt((farModel.X - _eyeX) * (farModel.X - _eyeX) + (farModel.Y - _eyeY) * (farModel.Y - _eyeY))));
				}
			}
		}

		internal MeshInstance NearMeshForTest(Building b)
		{
			foreach (NearBuilding building in _buildings)
			{
				if (building.Building == b)
				{
					return (building.Mesh != null && Meshes.Contains(building.Mesh)) ? building.Mesh : null;
				}
			}
			return null;
		}

		private void ForgetFarRing()
		{
			_farRing.Clear();
			_farRingOf.Clear();
			_farRingModel.Clear();
			_farNext.Clear();
			_farNextOf.Clear();
			_farNextModel.Clear();
			_farRingModels = (_farNextModels = false);
			_farNextAt = -1;
			_farScanNear = -1f;
			_farLate = 0;
			FarProps.Clear();
		}

		private void CollectFarProps(ViewState view, float near, TerrainCache terrain, SpriteCache sprites)
		{
			FarProps.Clear();
			FarModelReach = 0f;
			FarModelsDrawn = 0;
			float farPropRange = FarPropRange;
			if (farPropRange <= near)
			{
				return;
			}
			Vector2 vector = new Vector2(view.X, view.Y);
			bool flag = _farScanNear < 0f;
			if (!flag && (!(near >= _farScanNear - 2f) || !(farPropRange <= _farScanFar + 2f)))
			{
				_farNextAt = -1;
				ScanFarRing(vector, near, farPropRange, terrain, sprites);
			}
			else if (_farNextAt >= 0)
			{
				if (ScanFarStep(terrain, sprites, 1.5))
				{
					SwapFarRing();
				}
			}
			else if (flag)
			{
				BeginFarScan(vector, near, farPropRange);
			}
			else if ((vector - _farScanAt).sqrMagnitude > 64f || _frame - _farScanFrame > 120 || SeasonLook.Stamp != _farSeasonStamp || FarModels != _farRingModels || (_farLate > 0 && _frame - _farScanFrame > 2))
			{
				BeginFarScan(vector, near, farPropRange);
			}
			else
			{
				RefreshFarSlice(sprites);
				RemakeFarModels(sprites);
			}
			float num = near * near;
			float num2 = farPropRange * farPropRange;
			bool flag2 = _farRingModels && FarModels;
			float night = Mathf.Clamp01((_night - 0.05f) * 2.2f);
			for (int i = 0; i < _farRing.Count; i++)
			{
				FarProp item = _farRing[i];
				FarModel farModel = (flag2 ? _farRingModel[i] : null);
				if (farModel != null)
				{
					float num3 = farModel.X - view.X;
					float num4 = farModel.Y - view.Y;
					float num5 = num3 * num3 + num4 * num4;
					if (num5 >= num && num5 <= num2 && InView(num3, num4, Mathf.Max(item.Width, item.Height) + 1f))
					{
						DrawFarModel(farModel, night);
					}
				}
				else
				{
					float num6 = item.X - view.X;
					float num7 = item.Y - view.Y;
					float num8 = num6 * num6 + num7 * num7;
					if (!(num8 < num) && !(num8 > num2) && InView(num6, num7, item.Width + 1f))
					{
						FarProps.Add(item);
					}
				}
			}
			if (FarModelsDrawn > 0)
			{
				FarModelReach = farPropRange;
			}
		}

		private void ScanFarRing(Vector2 eye, float near, float far, TerrainCache terrain, SpriteCache sprites)
		{
			BeginFarScan(eye, near, far);
			ScanFarStep(terrain, sprites, double.MaxValue);
			SwapFarRing();
		}

		private void BeginFarScan(Vector2 eye, float near, float far)
		{
			_farNextAt = 0;
			_farNextEye = eye;
			_farNextNear = near;
			_farNextFar = far;
			_farNext.Clear();
			_farNextOf.Clear();
			_farNextModel.Clear();
			_farNextModels = FarModels && _farScanNear >= 0f;
			_farLate = 0;
			_farScanSum = (_farScanWorst = 0f);
			if (_farImage.Count > 60000)
			{
				_farImage.Clear();
			}
			if (SeasonLook.Stamp != _farSeasonStamp)
			{
				_farSeasoned.Clear();
				_farSeasonStamp = SeasonLook.Stamp;
			}
		}

		private bool ScanFarStep(TerrainCache terrain, SpriteCache sprites, double budgetMs)
		{
			_farClock.Restart();
			Vector2 farNextEye = _farNextEye;
			float num = Mathf.Max(0f, _farNextNear - 10f);
			float num2 = _farNextFar + 10f;
			float num3 = num * num;
			float num4 = num2 * num2;
			List<Building> simpleList = World.world.buildings.getSimpleList();
			int i;
			for (i = _farNextAt; i < simpleList.Count && (((i & 0x1F) != 31 && !_farMade) || !(_farClock.Elapsed.TotalMilliseconds > budgetMs)); i++)
			{
				_farMade = false;
				Building building = simpleList[i];
				if (building == null || building.asset == null || building.chopped || !building.isAlive() || building.current_tile == null)
				{
					continue;
				}
				Vector2 vector = Position(building);
				float sqrMagnitude = (vector - farNextEye).sqrMagnitude;
				if (sqrMagnitude < num3 || sqrMagnitude > num4)
				{
					continue;
				}
				if (!_farKinds.TryGetValue(building.asset, out var value))
				{
					value = FarKindOf(building);
					_farKinds[building.asset] = value;
				}
				if (!value.Shows)
				{
					continue;
				}
				if (!_farImage.TryGetValue(building, out var value2))
				{
					value2 = (_farImage[building] = FarImage(building, sprites));
				}
				FarSprite farSprite = FarSpriteOf(building.asset, value2, value);
				if (farSprite.Shows)
				{
					float y = vector.y;
					float height = farSprite.Height;
					if (value.House)
					{
						HouseStand(building, farSprite.Width, ref y, ref height);
					}
					_farNext.Add(new FarProp
					{
						X = vector.x,
						Y = y,
						Z = terrain.SampleHeight(vector.x, y),
						Width = farSprite.Width,
						Height = height,
						Color = farSprite.Color,
						Tree = value.Tree,
						Look = (_farLooksOff ? null : Seasoned(farSprite.Look, value, vector))
					});
					_farNextOf.Add(building);
					if (_farNextModels)
					{
						_farNextModel.Add(FarModelFor(building, vector, sprites, budgetMs < double.MaxValue || _farClock.Elapsed.TotalMilliseconds < 4.0));
					}
				}
			}
			_farNextAt = i;
			float num5 = (float)_farClock.Elapsed.TotalMilliseconds;
			_farScanSum += num5;
			_farScanWorst = Mathf.Max(_farScanWorst, num5);
			return i >= simpleList.Count;
		}

		private void SwapFarRing()
		{
			List<FarProp> farNext = _farNext;
			List<FarProp> farRing = _farRing;
			_farRing = farNext;
			_farNext = farRing;
			List<Building> farNextOf = _farNextOf;
			List<Building> farRingOf = _farRingOf;
			_farRingOf = farNextOf;
			_farNextOf = farRingOf;
			List<FarModel> farNextModel = _farNextModel;
			List<FarModel> farRingModel = _farRingModel;
			_farRingModel = farNextModel;
			_farNextModel = farRingModel;
			_farRingModels = _farNextModels;
			_farRings++;
			if (_farRingModels)
			{
				PruneFarModels();
			}
			_farScanAt = _farNextEye;
			_farScanNear = _farNextNear;
			_farScanFar = _farNextFar;
			_farScanFrame = _frame;
			_farNextAt = -1;
			_farCursor = 0;
			FarScanMs = _farScanSum;
			FarScanWorstMs = _farScanWorst;
			FarScanCount = _farRing.Count;
		}

		private void RefreshFarSlice(SpriteCache sprites)
		{
			int num = Mathf.Min(_farRing.Count, 150);
			for (int i = 0; i < num; i++)
			{
				_farCursor = (_farCursor + 1) % _farRing.Count;
				Building building = _farRingOf[_farCursor];
				if (building == null || building.asset == null || !building.isAlive() || building.chopped)
				{
					DropFarModel(_farCursor);
					continue;
				}
				SpriteImage spriteImage = FarImage(building, sprites);
				if (!_farImage.TryGetValue(building, out var value) || value != spriteImage)
				{
					_farImage[building] = spriteImage;
					FarKind value2;
					FarKind kind = (_farKinds.TryGetValue(building.asset, out value2) ? value2 : FarKindOf(building));
					FarSprite farSprite = FarSpriteOf(building.asset, spriteImage, kind);
					FarProp value3 = _farRing[_farCursor];
					float height = farSprite.Height;
					float y = 0f;
					if (kind.House)
					{
						HouseStand(building, farSprite.Width, ref y, ref height);
					}
					value3.Width = farSprite.Width;
					value3.Height = (farSprite.Shows ? height : 0f);
					value3.Color = farSprite.Color;
					value3.Look = Seasoned(farSprite.Look, kind, new Vector2(value3.X, value3.Y));
					_farRing[_farCursor] = value3;
					FarSpriteChanged(_farCursor, building, sprites);
				}
			}
		}

		private static void HouseStand(Building b, float width, ref float y, ref float height)
		{
			BuildingFundament fundament = b.asset.fundament;
			if (fundament != null)
			{
				y += (float)(fundament.top - fundament.bottom) * 0.5f;
			}
			string text = (Models.Ready ? Models.BuildingFamily(b) : null);
			MeshModel meshModel = ((text != null) ? Models.Detailed(text, 1000f) : null);
			if (meshModel != null && !(meshModel.Max.Z <= 0f))
			{
				float num = width / Mathf.Max(0.5f, meshModel.InfoFloat("width", meshModel.Max.X - meshModel.Min.X));
				height = num * meshModel.Max.Z;
			}
		}

		private SpriteImage FarImage(Building b, SpriteCache sprites)
		{
			if (_broken.Contains(b))
			{
				return null;
			}
			try
			{
				return sprites.Get(b.checkSpriteToRender());
			}
			catch
			{
				return null;
			}
		}

		private static FarKind FarKindOf(Building b)
		{
			FarKind result = default(FarKind);
			try
			{
				BuildingLook buildingLook = AssetStyle.Classify(b.asset);
				result.Shows = buildingLook == BuildingLook.Tree || buildingLook == BuildingLook.House || buildingLook == BuildingLook.Lump;
				result.Tree = buildingLook == BuildingLook.Tree;
				result.House = buildingLook == BuildingLook.House;
				result.Deciduous = result.Tree && SeasonLook.Deciduous(Models.TreeFamily(b.asset.id, 0));
			}
			catch
			{
			}
			return result;
		}

		private FarLook Seasoned(FarLook look, FarKind kind, Vector2 p)
		{
			if (look == null || SeasonLook.Stamp == 0 || (!kind.Deciduous && SeasonLook.Winter <= 0f))
			{
				return look;
			}
			bool flag = ((TerrainModel.Hash((int)p.x * 3 + 1, (int)p.y * 5 + 2) >> 9) & 1) != 0;
			if (_farSeasoned.TryGetValue((look, kind.Tree, flag), out var value))
			{
				return value;
			}
			if (_farSeasoned.Count > 4000)
			{
				_farSeasoned.Clear();
			}
			bool tree = kind.Tree;
			bool deciduous = kind.Deciduous;
			uint hash = (flag ? 3u : 0u);
			value = look.Recoloured((uint c, bool top) => SeasonLook.Far(c, tree, deciduous, top, hash));
			_farSeasoned[(look, kind.Tree, flag)] = value;
			return value;
		}

		private FarSprite FarSpriteOf(BuildingAsset asset, SpriteImage image, FarKind kind)
		{
			if (image == null)
			{
				return default(FarSprite);
			}
			if (_farSprites.TryGetValue((asset, image), out var value))
			{
				return value;
			}
			Vector3 scale_base = asset.scale_base;
			float mapHeight = image.UnitHeight * Mathf.Abs(scale_base.y);
			float num = (kind.Tree ? TreeGrow(mapHeight) : 1f);
			value.Width = image.UnitWidth * Mathf.Abs(scale_base.x) * (kind.Tree ? TreeWiden(mapHeight) : 1f);
			value.Height = image.UnitHeight * Mathf.Abs(scale_base.y) * num * image.VisibleFraction;
			value.Color = AverageColor(image);
			value.Shows = value.Height >= 0.6f && !IsBiomeBush(asset);
			if (!FarLooks.TryGetValue(image, out var value2))
			{
				if (FarLooks.Count > 3000)
				{
					FarLooks.Clear();
				}
				value2 = (FarLooks[image] = FarLook.From(image));
			}
			value.Look = value2;
			if (_farSprites.Count > 6000)
			{
				_farSprites.Clear();
			}
			_farSprites[(asset, image)] = value;
			return value;
		}

		private void FloraColours(NearBuilding nb, MeshInstance inst, MeshModel model, Vector2 p)
		{
			TerrainCache terrain = _terrain;
			uint num = 0u;
			uint num2 = 0u;
			if (terrain != null && terrain.Width > 0)
			{
				int num3 = Mathf.Clamp((int)p.x, 0, terrain.Width - 1);
				int num4 = Mathf.Clamp((int)p.y, 0, terrain.Height - 1);
				num = terrain.Colors[num4 * terrain.Width + num3];
				num2 = ((terrain.BaseColors.Length == terrain.Colors.Length) ? terrain.BaseColors[num4 * terrain.Width + num3] : num);
			}
			if (inst.Palette != null && nb.PaletteImage == nb.Image && nb.PaletteGround == num2 && inst.Palette.Length == model.SlotNames.Length)
			{
				if (nb.SeasonStamp != SeasonLook.Stamp)
				{
					Seasoned(nb, inst, model, p);
				}
			}
			else
			{
				nb.SeasonBase = Flora.Colors(model, nb.Image, num);
				nb.PaletteImage = nb.Image;
				nb.PaletteGround = num2;
				Seasoned(nb, inst, model, p);
			}
		}

		private static void Seasoned(NearBuilding nb, MeshInstance inst, MeshModel model, Vector2 p)
		{
			nb.SeasonStamp = SeasonLook.Stamp;
			inst.Palette = ((nb.SeasonBase != null) ? SeasonLook.Tree(model, nb.SeasonBase, TerrainModel.Hash((int)p.x * 3 + 1, (int)p.y * 5 + 2)) : inst.Palette);
		}

		private static uint[] SharedTreeColours(MeshModel model, SpriteImage img, Vector2 p)
		{
			return SeasonLook.Tree(model, Models.PaletteFor(model, img), TerrainModel.Hash((int)p.x * 3 + 1, (int)p.y * 5 + 2));
		}

		private static float TreeGrow(float mapHeight)
		{
			if (!(mapHeight > 20f))
			{
				if (!(mapHeight <= 6.5f))
				{
					return (14.625f + (mapHeight - 6.5f)) / mapHeight;
				}
				return 2.25f;
			}
			return 1f;
		}

		private static float TreeWiden(float mapHeight)
		{
			if (!(mapHeight > 20f))
			{
				return TreeGrow(mapHeight) * 0.79999995f;
			}
			return 1f;
		}

		private static bool Giant(NearBuilding nb)
		{
			if (nb.Look == BuildingLook.Tree)
			{
				return nb.Height > 15.5f;
			}
			return false;
		}

		private static float DetailDistance(NearBuilding nb, float dist)
		{
			if (!Giant(nb))
			{
				return dist;
			}
			return dist * 15.5f / nb.Height;
		}

		private void BeginGlide(ViewState view)
		{
			_glideFocal = ((FromSky || GlideOff || People.GlideOff || PictureWidth <= 0) ? 0f : ((float)PictureWidth * 0.5f / Mathf.Max(0.05f, Mathf.Tan(view.Fov * 0.5f))));
		}

		private static int GlideEvery(float tall)
		{
			if (!(tall < 4f))
			{
				if (!(tall < 8f))
				{
					if (!(tall < 16f))
					{
						return 1;
					}
					return 2;
				}
				return 4;
			}
			return 6;
		}

		private bool GlideUnit(Actor a, Actor host, Vector2 p, float dx, float dy, float dist2, float dt, TerrainCache terrain)
		{
			if (_glideFocal <= 0f || a == host || dist2 < 3600f)
			{
				return false;
			}
			float num = dx * _vfx + dy * _vfy;
			if (num <= 1f)
			{
				return false;
			}
			if (!_headings.TryGetValue(a, out var value) || value.SeenFrame != _frame - 1 || value.Inside > 0 || value.Looking)
			{
				return false;
			}
			float lift;
			float body;
			List<MeshInstance> parts;
			List<Vec3> offsets;
			MeshInstance meshInstance = People.GlideBody(a, out lift, out body, out parts, out offsets);
			if (meshInstance == null || !(body > 0f))
			{
				return false;
			}
			int num2 = GlideEvery(_glideFocal * body / num);
			if (num2 <= 1 || (_frame + (a.GetHashCode() & 7)) % num2 == 0)
			{
				return false;
			}
			if (RecentHits.ContainsKey(a) || LookAt.ContainsKey(a))
			{
				return false;
			}
			try
			{
				if (a.has_attack_target || a.under_forces || a.is_inside_building)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			float num3 = WorldStep(dt);
			Vector2 drawn = value.Drawn;
			float num4 = Mathf.Min(value.Speed, 10f);
			if (num4 > 0f && num3 > 0f)
			{
				drawn += new Vector2(Mathf.Cos(value.Angle), Mathf.Sin(value.Angle)) * (num4 * Mathf.Min(num3, 0.1f));
			}
			if (!(drawn.x >= 0f) || !(drawn.y >= 0f) || !(drawn.x < (float)terrain.Width) || !(drawn.y < (float)terrain.Height) || !(num4 >= 0f) || float.IsNaN(value.Angle))
			{
				return false;
			}
			float num5 = drawn.x - _eyeX;
			float num6 = drawn.y - _eyeY;
			if (num5 * _vfx + num6 * _vfy < -2f || !InView(num5, num6, 1f))
			{
				return false;
			}
			value.Drawn = drawn;
			value.Last = p;
			value.SeenFrame = _frame;
			value.Dt = num3;
			value.Owed += num3;
			float num7 = terrain.SampleHeight(drawn.x, drawn.y);
			meshInstance.Position = new Vec3(drawn.x, drawn.y, num7 + lift);
			int count = Tagged.Count;
			Tagged.Add(a);
			meshInstance.Tag = count;
			meshInstance.SortDepth = Mathf.Sqrt(num5 * num5 + num6 * num6);
			Meshes.Add(meshInstance);
			if (parts != null)
			{
				for (int i = 0; i < parts.Count; i++)
				{
					MeshInstance meshInstance2 = parts[i];
					meshInstance2.Position = meshInstance.Position + offsets[i];
					meshInstance2.Tag = count;
					meshInstance2.SortDepth = meshInstance.SortDepth;
					Meshes.Add(meshInstance2);
				}
			}
			Hitboxes.Record(a, meshInstance, person: true);
			People.Glid(a);
			return true;
		}

		private bool GodFrameReady()
		{
			if (_gfFrame == _frame)
			{
				return _gfOn;
			}
			_gfFrame = _frame;
			_gfOn = GodFrameWidth > 0 && GodFrameHeight > 0 && FromSky && GodFrameCamera.Shear > 0.999f;
			if (!_gfOn)
			{
				return false;
			}
			ViewState godFrameCamera = GodFrameCamera;
			_gfFocal = (float)GodFrameWidth * 0.5f / (float)Math.Tan(godFrameCamera.Fov * 0.5f);
			_gfHorizon = (float)GodFrameHeight * 0.5f + godFrameCamera.Pitch * _gfFocal;
			_gfHalfW = (float)GodFrameWidth * 0.5f;
			_gfFx = (float)Math.Cos(godFrameCamera.Yaw);
			_gfFy = (float)Math.Sin(godFrameCamera.Yaw);
			return true;
		}

		private bool OutsideGodFrame(float x, float y, float z, float tall, float margin)
		{
			if (!GodFrameReady())
			{
				return false;
			}
			ViewState godFrameCamera = GodFrameCamera;
			float num = x - godFrameCamera.X;
			float num2 = y - godFrameCamera.Y;
			float num3 = num * _gfFx + num2 * _gfFy;
			float num4 = margin * _gfFocal;
			if (num3 * _gfHalfW + num4 <= 0f)
			{
				return true;
			}
			if (Math.Abs(num * _gfFy - num2 * _gfFx) * _gfFocal > _gfHalfW * num3 + num4)
			{
				return true;
			}
			float num5 = _gfHorizon * num3;
			if (num5 - (z + tall - godFrameCamera.Z) * _gfFocal > (float)GodFrameHeight * num3 + num4)
			{
				return true;
			}
			if (num5 - (z - godFrameCamera.Z) * _gfFocal < 0f - num4)
			{
				return true;
			}
			return false;
		}

		private bool GodCellsReady(TerrainCache terrain)
		{
			if (_gcFrame == _frame)
			{
				return _gcOn;
			}
			_gcFrame = _frame;
			_gcOn = !GodCellsOff && GodFrameUnits && GodFrameReady() && terrain != null && terrain.Width > 0;
			if (!_gcOn)
			{
				return false;
			}
			_gcW = (terrain.Width + 16 - 1) / 16;
			_gcH = (terrain.Height + 16 - 1) / 16;
			if (_gcOut.Length < _gcW * _gcH)
			{
				_gcOut = new byte[_gcW * _gcH];
			}
			ViewState godFrameCamera = GodFrameCamera;
			float num = 17f * _gfFocal;
			float num2 = Mathf.Min(GodCamera.LowestGround, 0f) - 1f;
			float num3 = terrain.MaxHeight + 1f + 3f;
			for (int i = 0; i < _gcH; i++)
			{
				for (int j = 0; j < _gcW; j++)
				{
					bool flag = true;
					bool flag2 = true;
					bool flag3 = true;
					bool flag4 = true;
					bool flag5 = true;
					for (int k = 0; k < 4; k++)
					{
						float num4 = (j + (k & 1)) * 16;
						float num5 = (i + (k >> 1)) * 16;
						float num6 = num4 - godFrameCamera.X;
						float num7 = num5 - godFrameCamera.Y;
						float num8 = num6 * _gfFx + num7 * _gfFy;
						float num9 = (num6 * _gfFy - num7 * _gfFx) * _gfFocal;
						float num10 = _gfHorizon * num8;
						flag &= num8 * _gfHalfW + num < 0f;
						flag2 &= num9 > _gfHalfW * num8 + num;
						flag3 &= 0f - num9 > _gfHalfW * num8 + num;
						flag4 &= num10 - (num3 - godFrameCamera.Z) * _gfFocal > (float)GodFrameHeight * num8 + num;
						flag5 &= num10 - (num2 - godFrameCamera.Z) * _gfFocal < 0f - num;
					}
					_gcOut[i * _gcW + j] = (byte)((flag || flag2 || flag3 || flag4 || flag5) ? 1u : 0u);
				}
			}
			return true;
		}

		private bool UnitInOffFrameCell(Actor a, int index, Actor host, TerrainCache terrain)
		{
			if (a == host || a == Skip || !GodCellsReady(terrain))
			{
				return false;
			}
			Vector2 current_position = a.current_position;
			int num = (int)(current_position.x / 16f);
			int num2 = (int)(current_position.y / 16f);
			if (!(current_position.x >= 0f) || !(current_position.y >= 0f) || num >= _gcW || num2 >= _gcH || _gcOut[num2 * _gcW + num] == 0)
			{
				return false;
			}
			try
			{
				if (a.position_height > 0.01f)
				{
					return false;
				}
				ActorAsset asset = a.asset;
				if (asset == null || asset.is_boat)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			int num3 = UnitSlot(index);
			if ((KindOf(a, ref _unitMemo[num3]) & 0x1E) != 0)
			{
				return false;
			}
			GodFrameSkipped++;
			return true;
		}

		private bool BuildingCellsReady(TerrainCache terrain)
		{
			if (_gcbFrame == _frame)
			{
				return _gcbOn;
			}
			_gcbFrame = _frame;
			_gcbOn = !GodCellsOff && GodFrameBuildings && GodFrameReady() && terrain != null && terrain.Width > 0;
			if (!_gcbOn)
			{
				return false;
			}
			int num = (terrain.Width + 16 - 1) / 16;
			int num2 = (terrain.Height + 16 - 1) / 16;
			if (_gcbOut.Length < num * num2)
			{
				_gcbOut = new byte[num * num2];
			}
			ViewState godFrameCamera = GodFrameCamera;
			float num3 = 29f * _gfFocal;
			float num4 = Mathf.Min(GodCamera.LowestGround, 0f) - 1f;
			float num5 = terrain.MaxHeight + 1f + 30f;
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					bool flag = true;
					bool flag2 = true;
					bool flag3 = true;
					bool flag4 = true;
					bool flag5 = true;
					for (int k = 0; k < 4; k++)
					{
						float num6 = (j + (k & 1)) * 16;
						float num7 = (i + (k >> 1)) * 16;
						float num8 = num6 - godFrameCamera.X;
						float num9 = num7 - godFrameCamera.Y;
						float num10 = num8 * _gfFx + num9 * _gfFy;
						float num11 = (num8 * _gfFy - num9 * _gfFx) * _gfFocal;
						float num12 = _gfHorizon * num10;
						flag &= num10 * _gfHalfW + num3 < 0f;
						flag2 &= num11 > _gfHalfW * num10 + num3;
						flag3 &= 0f - num11 > _gfHalfW * num10 + num3;
						flag4 &= num12 - (num5 - godFrameCamera.Z) * _gfFocal > (float)GodFrameHeight * num10 + num3;
						flag5 &= num12 - (num4 - godFrameCamera.Z) * _gfFocal < 0f - num3;
					}
					_gcbOut[i * num + j] = (byte)((flag || flag2 || flag3 || flag4 || flag5) ? 1u : 0u);
				}
			}
			return true;
		}

		private bool BuildingInOffFrameCell(NearBuilding nb, Vector2 p, TerrainCache terrain)
		{
			if (nb.Height + 1f > 30f || nb.Width > 12f || !BuildingCellsReady(terrain))
			{
				return false;
			}
			int num = (int)(p.x / 16f);
			int num2 = (int)(p.y / 16f);
			int num3 = (terrain.Width + 16 - 1) / 16;
			int num4 = (terrain.Height + 16 - 1) / 16;
			if (!(p.x >= 0f) || !(p.y >= 0f) || num >= num3 || num2 >= num4 || _gcbOut[num2 * num3 + num] == 0)
			{
				return false;
			}
			NoteOffFrame(nb);
			return true;
		}

		private void NoteOffFrame(NearBuilding nb)
		{
			if (nb.Route == ModelRoute.Building)
			{
				bool flag;
				try
				{
					flag = nb.Building.isRuin();
				}
				catch
				{
					flag = false;
				}
				if (flag && nb.WasStanding && nb.RuinedAt < 0f)
				{
					nb.RuinedAt = _clock;
				}
				nb.WasStanding = !flag;
			}
			GodFrameSkipped++;
		}

		private bool UnitOutsideGodFrame(Actor a, Vector2 p, TerrainCache terrain)
		{
			if (!GodFrameUnits || !GodFrameReady())
			{
				return false;
			}
			if (a.asset != null && a.asset.is_boat)
			{
				return false;
			}
			float num = 0f;
			try
			{
				num = Mathf.Max(0f, a.position_height);
			}
			catch
			{
			}
			if (!OutsideGodFrame(p.x, p.y, terrain.SampleHeight(p.x, p.y), 3f + num, 16f))
			{
				return false;
			}
			GodFrameSkipped++;
			return true;
		}

		private bool BuildingOutsideGodFrame(NearBuilding nb, Vector2 p, float ground)
		{
			if (!GodFrameBuildings || !GodFrameReady())
			{
				return false;
			}
			if (!OutsideGodFrame(p.x, p.y, ground, nb.Height + 1f, 16f + nb.Width))
			{
				return false;
			}
			NoteOffFrame(nb);
			return true;
		}

		private static bool IsGodFinger(Actor a)
		{
			if (a.asset != null)
			{
				return a.asset.id == "god_finger";
			}
			return false;
		}

		private void AddGodFinger(Actor a, ViewState view, TerrainCache terrain, float dt)
		{
			Vector2 current_position = a.current_position;
			float num = current_position.x - view.X;
			float num2 = current_position.y - view.Y;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 > 240f)
			{
				return;
			}
			MeshModel meshModel = ((num3 > 90f) ? (Models.Get("god_hand_f") ?? Models.Get("god_hand")) : ((num3 > 32f) ? (Models.Get("god_hand_m") ?? Models.Get("god_hand")) : Models.Get("god_hand")));
			if (meshModel != null)
			{
				if (!_godArms.TryGetValue(a, out var value))
				{
					Dictionary<object, GodArm> godArms = _godArms;
					GodArm obj = new GodArm
					{
						Fx = new GodHandFx((uint)a.GetHashCode())
						{
							Presence = 0f
						},
						LastPos = current_position,
						Finger = true
					};
					value = obj;
					godArms[a] = obj;
				}
				value.Seen = _frame;
				value.Fx.Presence = Mathf.MoveTowards(value.Fx.Presence, 1f, dt * 1.4f);
				bool drawing = false;
				try
				{
					GodFinger actorComponent = a.getActorComponent<GodFinger>();
					drawing = actorComponent != null && actorComponent.is_drawing;
				}
				catch
				{
				}
				Vector2 vector = ((dt > 0f) ? ((current_position - value.LastPos) / Mathf.Max(dt, 0.001f)) : Vector2.zero);
				value.LastPos = current_position;
				if (vector.sqrMagnitude > 400f)
				{
					vector = Vector2.zero;
				}
				Vec3 up = value.Fx.Steer(vector.x, vector.y, dt, _clock);
				float num4 = terrain.SampleHeight(current_position.x, current_position.y);
				float num5 = Mathf.Max(0f, a.position_height - 0.3f);
				Vec3 tip = new Vec3(current_position.x, current_position.y, num4 + 0.05f + num5 * 2.6f);
				Vec3 palm = ((vector.sqrMagnitude > 0.5f) ? new Vec3(vector.x, vector.y, 0f).Normalized() : new Vec3(view.X - current_position.x, view.Y - current_position.y, 0f).Normalized());
				value.Fx.Finger(_blastSink, BlastParticles, meshModel, tip, up, palm, drawing, num4, _clock, dt, _night);
				value.Model = meshModel;
				value.Tip = tip;
				value.Up = up;
				value.Palm = palm;
				value.Ground = num4;
				GodFingersDrawn++;
			}
		}

		private void AddGodGrab(MapBox world, ViewState view, TerrainCache terrain, float dt)
		{
			Magnet magnet = world.magnet;
			MeshModel meshModel = Models.Get("god_hand");
			if (magnet == null || meshModel == null)
			{
				return;
			}
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			int num4 = 0;
			try
			{
				if (magnet.hasUnits())
				{
					foreach (Actor magnet_unit in magnet.magnet_units)
					{
						if (magnet_unit != null && magnet_unit.isAlive())
						{
							num += magnet_unit.current_position.x;
							num2 += magnet_unit.current_position.y;
							num3 += magnet_unit.position_height;
							num4++;
						}
					}
				}
			}
			catch
			{
				num4 = 0;
			}
			if (!_godArms.TryGetValue(magnet, out var value))
			{
				Dictionary<object, GodArm> godArms = _godArms;
				GodArm obj2 = new GodArm
				{
					Fx = new GodHandFx(77u)
					{
						Presence = 0f
					}
				};
				value = obj2;
				godArms[magnet] = obj2;
			}
			value.Fx.Curl = Mathf.MoveTowards(value.Fx.Curl, (num4 > 0) ? 1f : 0f, dt * 2.5f);
			bool flag = num4 > 0 || value.Fx.Curl > 0.35f;
			value.Fx.Presence = Mathf.MoveTowards(value.Fx.Presence, flag ? 1f : 0f, dt * (flag ? 2f : 1.1f));
			if (!(value.Fx.Presence <= 0f))
			{
				value.Seen = _frame;
				if (num4 > 0)
				{
					value.LastPos = new Vector2(num / (float)num4, num2 / (float)num4);
				}
				Vector2 lastPos = value.LastPos;
				if (!((lastPos - new Vector2(view.X, view.Y)).sqrMagnitude > SkyRange(200f) * SkyRange(200f)))
				{
					float num5 = terrain.SampleHeight(lastPos.x, lastPos.y);
					float num6 = ((num4 > 0) ? (num3 / (float)num4) : 0f);
					Vec3 held = new Vec3(lastPos.x, lastPos.y, num5 + 0.6f + num6 * 1.5f + (1f - value.Fx.Curl) * 3f);
					value.Fx.Grabbing(_blastSink, meshModel, held, new Vec3(0.25f, 0.3f, 1f).Normalized(), new Vec3(view.X - lastPos.x, view.Y - lastPos.y, 0f).Normalized(), _clock, _night, dt);
					GodGrabsDrawn++;
					GodGrabCurl = value.Fx.Curl;
				}
			}
		}

		private void FinishGodHands(MapBox world, ViewState view, TerrainCache terrain, float dt)
		{
			GodGrabsDrawn = 0;
			if (Models.Ready)
			{
				AddGodGrab(world, view, terrain, dt);
			}
			if (Models.Ready && NetSession.Active)
			{
				AddPlayerHands(view, terrain, dt);
			}
			foreach (GodArm value in _godArms.Values)
			{
				if (value.Finger && value.Seen != _frame && !(value.Fx.Presence <= 0f) && value.Model != null)
				{
					value.Fx.Presence = Mathf.MoveTowards(value.Fx.Presence, 0f, dt * 1.1f);
					float num = value.Tip.X - view.X;
					float num2 = value.Tip.Y - view.Y;
					if (value.Fx.Presence > 0f && num * num + num2 * num2 < 57600f)
					{
						value.Fx.Finger(_blastSink, BlastParticles, value.Model, value.Tip, value.Up, value.Palm, drawing: false, value.Ground, _clock, dt, _night);
					}
				}
			}
			if (_frame % 90 != 0)
			{
				return;
			}
			_godStale.Clear();
			foreach (KeyValuePair<object, GodArm> godArm in _godArms)
			{
				if (_frame - godArm.Value.Seen > 90 && godArm.Value.Fx.Presence <= 0f)
				{
					_godStale.Add(godArm.Key);
				}
			}
			foreach (object item in _godStale)
			{
				_godArms.Remove(item);
			}
		}

		internal bool DrawnAt(Actor a, out Vector2 drawn)
		{
			if (a != null && _headings.TryGetValue(a, out var value) && _frame - value.SeenFrame <= 2)
			{
				drawn = value.Drawn;
				return true;
			}
			drawn = default(Vector2);
			return false;
		}

		internal HallFlagState HallFlagForTest(Building b)
		{
			if (b == null)
			{
				return null;
			}
			try
			{
				HallFlagState value;
				return (_hallFlags.TryGetValue(b.data.id, out value) && value.B == b) ? value : null;
			}
			catch
			{
				return null;
			}
		}

		private void HallFlagsFrame()
		{
			if (_hfFrame == _frame)
			{
				return;
			}
			_hfFrame = _frame;
			object obj = null;
			try
			{
				obj = ((World.world != null) ? World.world.map_stats : null);
			}
			catch
			{
			}
			if (obj != _hfWorld)
			{
				_hfWorld = obj;
				_hallFlags.Clear();
				HallFlagLook.Reset();
			}
			float num = WorldStep(_frameDt);
			_hfClock += num;
			float num2 = (FromSky ? Weather.WindX : Gusts.DirX);
			float num3 = (FromSky ? Weather.WindY : Gusts.DirY);
			float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
			if (num4 > 0.0001f)
			{
				float num5 = Mathf.Min(1f, 0.8f * num);
				_hfWindX += (num2 / num4 - _hfWindX) * num5;
				_hfWindY += (num3 / num4 - _hfWindY) * num5;
				float num6 = Mathf.Sqrt(_hfWindX * _hfWindX + _hfWindY * _hfWindY);
				if (num6 > 0.0001f)
				{
					_hfWindX /= num6;
					_hfWindY /= num6;
				}
				else
				{
					_hfWindX = num2 / num4;
					_hfWindY = num3 / num4;
				}
			}
			_hfStrength = (FromSky ? 0.45f : Mathf.Clamp01(0.35f + 0.65f * Gusts.Strength));
			HallFlagModels.NewFrame(_frame);
			try
			{
				List<City> list = World.world?.cities?.list;
				if (list != null && list.Count > 0)
				{
					if (_hfCity >= list.Count)
					{
						_hfCity = 0;
					}
					HallFlagLook.Watch(list[_hfCity++]);
				}
			}
			catch
			{
			}
			if (_frame % 120 != 0 || _hallFlags.Count <= 0)
			{
				return;
			}
			_hallFlagsGone.Clear();
			foreach (KeyValuePair<long, HallFlagState> hallFlag in _hallFlags)
			{
				if (_frame - hallFlag.Value.Frame > 600)
				{
					_hallFlagsGone.Add(hallFlag.Key);
				}
			}
			foreach (long item in _hallFlagsGone)
			{
				_hallFlags.Remove(item);
			}
		}

		private void HallFlagsAt(NearBuilding nb, Building b, MeshInstance hall, MeshModel full, float x, float y, float heading, float scale, bool mirror, bool ruin, bool ruined, bool abandoned, bool seen, float dist)
		{
			if (!HallFlagsOn || WorldBoxMod.WorldLoading || b == null || full == null || hall == null)
			{
				return;
			}
			long id;
			Kingdom kingdom;
			try
			{
				if (b.asset == null || b.asset.type != "type_hall")
				{
					return;
				}
				id = b.data.id;
				kingdom = b.kingdom;
			}
			catch
			{
				return;
			}
			if (_hfDrawnFrame != _frame)
			{
				_hfDrawnFrame = _frame;
				HallFlagsMs = 0.0;
				HallFlagsDrawn = 0;
			}
			_hfWatch.Restart();
			try
			{
				HallFlag(nb, b, id, kingdom, hall, full, scale, mirror, ruin, ruined, abandoned, seen, dist);
			}
			catch (Exception ex)
			{
				if (_reportedAssets.Add("hallflags"))
				{
					Log.Error("hall flags: " + ex);
				}
			}
			_hfWatch.Stop();
			HallFlagsMs += _hfWatch.Elapsed.TotalMilliseconds;
		}

		private void HallFlag(NearBuilding nb, Building b, long id, Kingdom k, MeshInstance hall, MeshModel full, float scale, bool mirror, bool ruin, bool ruined, bool abandoned, bool seen, float dist)
		{
			HallFlagsFrame();
			SpriteCache sprites = _townScene.Sprites;
			bool flag = false;
			if (!_hallFlags.TryGetValue(id, out var value) || value.B != b)
			{
				value = new HallFlagState
				{
					B = b,
					Seed = (float)((ulong)(id * 2654435761u) % 6283uL) / 1000f,
					Torn = (int)((id % 4 + 4) % 4)
				};
				_hallFlags[id] = value;
				flag = true;
			}
			FlagLook look = default(FlagLook);
			bool flag2 = !ruin && HallFlagLook.Of(k, sprites, out look);
			FlagLook l;
			if (flag)
			{
				if (flag2)
				{
					value.Base = look;
				}
				else
				{
					HallFlagLook.Recall(b, out value.Base);
				}
				value.Next = value.Base;
			}
			else if (flag2)
			{
				Follow(value, in look);
			}
			else if (_frame - value.Frame > 120 && HallFlagLook.Recall(b, out l) && !HallFlagLook.Same(in l, in value.Base))
			{
				value.Base = (value.Next = l);
				value.Swapping = false;
			}
			if (flag2)
			{
				HallFlagLook.Remember(b, in look);
			}
			if (!value.Base.GreysKnown)
			{
				HallFlagLook.FillGreys(ref value.Base, sprites);
			}
			if (value.Swapping && !value.Next.GreysKnown)
			{
				HallFlagLook.FillGreys(ref value.Next, sprites);
			}
			float fold = 0f;
			value.Height01 = 1f;
			if (value.Swapping)
			{
				float num = _hfClock - value.SwapAt;
				FlagWave.Swap(num, out value.Height01, out fold, out var showNew);
				value.SwapPhase = ((num < 2.4f) ? 1 : ((num < 2.9f) ? 2 : ((num < 5.3f) ? 3 : 0)));
				value.Shown = (showNew ? value.Next : value.Base);
				if (num >= 5.3f)
				{
					value.Base = value.Next;
					value.Swapping = false;
				}
			}
			else
			{
				value.SwapPhase = 0;
				value.Shown = (value.Next = value.Base);
			}
			value.Shaft = (value.Finial = (value.Cloth = (value.Banner = (value.RodL = (value.RodR = (value.Bracket = (value.Stand = null)))))));
			value.Front = 0;
			float num2 = dist / Mathf.Max(0.05f, Models.LodZoom);
			if (!FlagSpots.TryGet(full, out var spot) || spot.Pole == null)
			{
				value.Frame = _frame;
				value.Dist = dist;
				return;
			}
			FlagLook look2 = value.Shown;
			sbyte b2 = (sbyte)(look2.Known ? look2.Style : (-1));
			FlagShape flagShape = FlagCloth.ShapeOf(b2);
			FlagTop top = FlagCloth.TopOf(b2);
			bool flag3 = false;
			if (!ruin)
			{
				try
				{
					flag3 = TownTier.IsCapital(b.city);
				}
				catch
				{
				}
			}
			int num3 = (int)flagShape;
			float num4 = spot.Rig * (flag3 ? 1.25f : 1f);
			float num5 = scale * num4;
			float num6 = (flag3 ? spot.PoleCapital[num3] : spot.Pole[num3]) * scale;
			float num7 = (flag3 ? spot.ClothScaleCapital[num3] : spot.ClothScale[num3]);
			Vec3 vec = (value.FootWorld = hall.Position + hall.Basis.Mul(spot.Foot));
			int num8 = (value.ClothLod = Lod(num2, value.ClothLod, 30f, 90f));
			if (value.GhostOf != full)
			{
				value.GhostOf = full;
				value.Ghostly = full.InfoFloat("ghostly", 0f);
			}
			uint multiply = (abandoned ? Weathered : 0u);
			float num9 = (ruined ? spot.Lean : 0f);
			float num10 = (ruined ? (0.55f * num6) : num6);
			float num11 = Mathf.Cos(num9);
			float num12 = Mathf.Sin(Mathf.Abs(num9));
			float x = hall.Basis.C0.X;
			float y = hall.Basis.C0.Y;
			float num13 = Mathf.Sqrt(x * x + y * y);
			if (num13 > 1E-06f)
			{
				x /= num13;
				y /= num13;
			}
			else
			{
				x = 1f;
				y = 0f;
			}
			if (num9 < 0f)
			{
				x = 0f - x;
				y = 0f - y;
			}
			Vec3 vec2 = new Vec3(num12 * x, num12 * y, num11);
			Vec3 vec3 = vec + vec2 * num10;
			int piece = (ruin ? 2 : ((b2 == 0 || b2 == 2) ? 1 : 0));
			MeshInstance meshInstance = (value.Shaft = Fixture(value, 0, HallFlagModels.Hardware(piece, num8)));
			if (meshInstance != null)
			{
				if (num9 == 0f)
				{
					meshInstance.Place(vec.X, vec.Y, vec.Z, -MathF.PI / 2f, num5, num5, num10);
				}
				else
				{
					meshInstance.Position = vec;
					meshInstance.Basis = new Mat3(new Vec3(num5 * num11 * x, num5 * num11 * y, (0f - num5) * num12), new Vec3((0f - num5) * y, num5 * x, 0f), vec2 * num10);
				}
				if (value.ShaftPainted != meshInstance.Model || value.ShaftStyle != b2 || value.ShaftPal == null)
				{
					int num14 = HallFlagModels.ShaftSlot(meshInstance.Model);
					value.ShaftPal = ((num14 >= 0) ? new uint[meshInstance.Model.SlotNames.Length] : null);
					if (num14 >= 0)
					{
						value.ShaftPal[num14] = ((!ruin) ? ShaftColour(b2) : 0u);
					}
					value.ShaftPainted = meshInstance.Model;
					value.ShaftStyle = b2;
				}
				meshInstance.Palette = value.ShaftPal;
				Dress(meshInstance, hall, seen, num2, dist, multiply, value.Ghostly, noShadow: false);
				Meshes.Add(meshInstance);
			}
			if (!ruin)
			{
				MeshInstance meshInstance2 = (value.Finial = Fixture(value, 1, HallFlagModels.Hardware(HallFlagModels.FinialOf(top), num8)));
				if (meshInstance2 != null)
				{
					meshInstance2.Place(vec3.X, vec3.Y, vec3.Z, -MathF.PI / 2f, num5, num5, num5);
					Dress(meshInstance2, hall, seen, num2, dist, multiply, value.Ghostly, noShadow: false);
					Meshes.Add(meshInstance2);
				}
			}
			float num15 = num5 * num7;
			float num16 = 0.12f * num4 * scale;
			Vec3 vec4 = new Vec3(vec3.X, vec3.Y, vec3.Z - num16);
			if (value.Swapping)
			{
				float num17 = vec.Z + (0.35f + 1.26f * num7) * num4 * scale;
				vec4.Z = num17 + (vec4.Z - num17) * value.Height01;
			}
			int torn = (ruin ? value.Torn : (-1));
			MeshModel meshModel = (look2.Known ? HallFlagModels.Cloth(in look2, flagShape, num8, torn, sprites) : null);
			if (meshModel == null)
			{
				meshModel = HallFlagModels.Plain(flagShape, num8, torn);
			}
			object obj2 = value.Cloths[num8];
			if (obj2 == null)
			{
				MeshInstance[] cloths = value.Cloths;
				MeshInstance obj3 = new MeshInstance
				{
					Static = (num8 == 2),
					NoShadow = (num8 == 2)
				};
				MeshInstance meshInstance3 = obj3;
				cloths[num8] = obj3;
				obj2 = meshInstance3;
			}
			MeshInstance meshInstance4 = (MeshInstance)obj2;
			meshInstance4.Model = meshModel;
			int num18 = (look2.Known ? (ruin ? 1 : 0) : 2);
			uint num19 = ((num18 == 2) ? RoofColour(nb, b) : 0u);
			if (value.PaintedModel[num8] != meshModel || value.PaintedMode[num8] != num18 || value.PaintedRoof[num8] != num19 || !HallFlagLook.SamePaint(in value.PaintedLook[num8], in look2) || value.ClothPal[num8] == null)
			{
				uint[] array = value.ClothPal[num8];
				if (array == null || array.Length != meshModel.SlotNames.Length)
				{
					array = (value.ClothPal[num8] = new uint[meshModel.SlotNames.Length]);
				}
				if (num18 == 2)
				{
					HallFlagLook.PaintPlain(array, num19, ruin);
				}
				else
				{
					HallFlagLook.Paint(array, meshModel, in look2, ruin);
				}
				value.PaintedModel[num8] = meshModel;
				value.PaintedMode[num8] = num18;
				value.PaintedRoof[num8] = num19;
				value.PaintedLook[num8] = look2;
			}
			meshInstance4.Palette = value.ClothPal[num8];
			if (value.LengthOf[num8] != meshModel)
			{
				value.LengthOf[num8] = meshModel;
				value.ClothLength[num8] = meshModel.InfoFloat("length", meshModel.Max.X);
			}
			float wind = (FromSky ? (0.45f + 0.1f * Mathf.Sin(0.13f * _hfClock + value.Seed)) : _hfStrength);
			float num20 = Mathf.Atan2(_hfWindY, _hfWindX) - MathF.PI / 2f;
			if (num8 == 2)
			{
				num20 += FlagWave.FarYaw(wind, _hfClock, value.Seed);
			}
			float num21 = 1f - 0.9f * fold;
			float num22 = 1f - 0.5f * fold;
			meshInstance4.Place(vec4.X, vec4.Y, vec4.Z, num20, num15 * num21, num15, num15 * num22);
			if (num8 < 2)
			{
				FlagWave.Pose(meshInstance4, meshModel.Parts.Length, value.ClothLength[num8], wind, _hfClock, value.Seed, ruin);
			}
			Dress(meshInstance4, hall, seen, num2, dist, multiply, value.Ghostly, num8 == 2 || num2 > 60f);
			Meshes.Add(meshInstance4);
			value.Cloth = meshInstance4;
			value.ClothModel = meshModel;
			if (!ruin && look2.Known && spot.Front != 0 && num2 < 110f)
			{
				FrontBanner(value, hall, in spot, in look2, mirror, scale, seen, num2, dist, multiply, sprites);
			}
			value.Frame = _frame;
			value.Dist = dist;
			HallFlagsDrawn++;
		}

		private static MeshInstance Fixture(HallFlagState st, int which, MeshModel m)
		{
			if (m == null)
			{
				return null;
			}
			MeshInstance[] array = st.Fixtures ?? (st.Fixtures = new MeshInstance[7]);
			object obj = array[which];
			if (obj == null)
			{
				MeshInstance obj2 = new MeshInstance
				{
					Static = true
				};
				MeshInstance meshInstance = obj2;
				array[which] = obj2;
				obj = meshInstance;
			}
			((MeshInstance)obj).Model = m;
			return (MeshInstance)obj;
		}

		private void FrontBanner(HallFlagState st, MeshInstance hall, in FlagSpot spot, in FlagLook look, bool mirror, float scale, bool seen, float d, float dist, uint multiply, SpriteCache sprites)
		{
			float num = (mirror ? (0f - spot.BannerYaw) : spot.BannerYaw);
			float heading = -MathF.PI / 2f + num;
			float num2 = spot.BannerPx * scale;
			Vec3 vec = default(Vec3);
			if (spot.Front == 4)
			{
				vec = hall.Position + hall.Basis.Mul(spot.Stand);
				vec.Z = Mathf.Max(vec.Z, _terrain.SampleHeight(vec.X, vec.Y));
				MeshInstance meshInstance = (st.Stand = Fixture(st, 6, HallFlagModels.Hardware(9, Lod(d, st.BannerLod, 30f, 70f))));
				if (meshInstance != null)
				{
					meshInstance.Place(vec.X, vec.Y, vec.Z, heading, num2, num2, num2);
					Dress(meshInstance, hall, seen, d, dist, multiply, st.Ghostly, noShadow: false);
					Meshes.Add(meshInstance);
				}
			}
			if (!seen)
			{
				return;
			}
			int lod = (st.BannerLod = Lod(d, st.BannerLod, 30f, 70f));
			MeshModel meshModel = HallFlagModels.Banner(in look, lod, sprites);
			if (meshModel == null)
			{
				return;
			}
			if (st.BannerSized != meshModel)
			{
				st.BannerSized = meshModel;
				st.BannerW = Mathf.Max(1f, meshModel.InfoFloat("width", 28f));
				st.BannerH = Mathf.Max(1f, meshModel.InfoFloat("height", 38f));
			}
			float num3 = num2 * Mathf.Min(1f, Mathf.Min(28f / st.BannerW, 38f / st.BannerH));
			Vec3 vec2 = ((spot.Front == 4) ? new Vec3(vec.X, vec.Y, vec.Z + 59f * num2) : (hall.Position + hall.Basis.Mul(spot.BannerTop)));
			MeshInstance meshInstance2 = (st.Banner = Fixture(st, 2, meshModel));
			meshInstance2.Place(vec2.X, vec2.Y, vec2.Z, heading, num3, num3, num3);
			if (st.BannerPainted != meshModel || !HallFlagLook.SamePaint(in st.BannerLook, in look) || st.BannerPal == null)
			{
				if (st.BannerPal == null || st.BannerPal.Length != meshModel.SlotNames.Length)
				{
					st.BannerPal = new uint[meshModel.SlotNames.Length];
				}
				HallFlagLook.Paint(st.BannerPal, meshModel, in look, faded: false);
				st.BannerPainted = meshModel;
				st.BannerLook = look;
			}
			meshInstance2.Palette = st.BannerPal;
			Dress(meshInstance2, hall, seen: true, d, dist, multiply, st.Ghostly, noShadow: true);
			Meshes.Add(meshInstance2);
			st.BannerModel = meshModel;
			st.Front = spot.Front;
			if (spot.Front == 1 || spot.Front == 2)
			{
				float num4 = Mathf.Cos(num);
				float num5 = Mathf.Sin(num);
				float num6 = (0.5f * st.BannerW + 1.5f) * num3;
				float num7 = 0.6f * num3;
				MeshModel m = HallFlagModels.Hardware(7, lod);
				MeshInstance meshInstance3 = (st.RodL = Fixture(st, 3, m));
				MeshInstance meshInstance4 = (st.RodR = Fixture(st, 4, m));
				if (meshInstance3 != null && meshInstance4 != null)
				{
					meshInstance3.Place(vec2.X - num4 * num6, vec2.Y - num5 * num6, vec2.Z + num7, heading, num3, num3, num3);
					meshInstance4.Place(vec2.X + num4 * num6, vec2.Y + num5 * num6, vec2.Z + num7, heading, 0f - num3, num3, num3);
					Dress(meshInstance3, hall, seen: true, d, dist, multiply, st.Ghostly, noShadow: true);
					Dress(meshInstance4, hall, seen: true, d, dist, multiply, st.Ghostly, noShadow: true);
					Meshes.Add(meshInstance3);
					Meshes.Add(meshInstance4);
				}
			}
			else if (spot.Front == 3)
			{
				Vec3 vec3 = hall.Position + hall.Basis.Mul(spot.Stand);
				MeshInstance meshInstance5 = (st.Bracket = Fixture(st, 5, HallFlagModels.Hardware(8, lod)));
				if (meshInstance5 != null)
				{
					meshInstance5.Place(vec3.X, vec3.Y, vec3.Z, heading, scale, scale, scale);
					Dress(meshInstance5, hall, seen: true, d, dist, multiply, st.Ghostly, noShadow: true);
					Meshes.Add(meshInstance5);
				}
			}
		}

		private void Dress(MeshInstance i, MeshInstance hall, bool seen, float d, float dist, uint multiply, float ghostly, bool noShadow)
		{
			i.ShadowOnly = !seen;
			i.NoShadow = noShadow;
			i.KeepSmall = d > 20f;
			i.Tag = hall.Tag;
			i.SortDepth = dist;
			i.Multiply = multiply;
			i.Fade = 1f;
			i.Emissive = false;
			if (ghostly > 0f)
			{
				GhostLook.Apply(i, ghostly, _night);
			}
		}

		private static int Lod(float d, int current, float near, float mid)
		{
			if (current == 0 && d < near + 3f)
			{
				return 0;
			}
			if (current == 1 && d > near - 3f && d < mid + 3f)
			{
				return 1;
			}
			if (current == 2 && d > mid - 3f)
			{
				return 2;
			}
			if (!(d < near))
			{
				if (!(d < mid))
				{
					return 2;
				}
				return 1;
			}
			return 0;
		}

		private static uint ShaftColour(int style)
		{
			switch (style)
			{
			default:
				return TerrainCache.Rgb(96, 66, 40);
			case 2:
				return TerrainCache.Rgb(200, 204, 210);
			case 1:
				return TerrainCache.Rgb(74, 50, 34);
			case 0:
				return TerrainCache.Rgb(58, 60, 66);
			}
		}

		private void Follow(HallFlagState st, in FlagLook now)
		{
			FlagLook a = (st.Swapping ? st.Next : st.Base);
			if (a.Known && (now.KingdomId != a.KingdomId || now.StyleSerial != a.StyleSerial))
			{
				if (_frame - st.Frame > 120 || !(st.Dist < 150f))
				{
					st.Base = (st.Next = now);
					st.Swapping = false;
					return;
				}
				if (st.Swapping)
				{
					FlagWave.Swap(_hfClock - st.SwapAt, out var height, out var _, out var showNew);
					if (showNew)
					{
						st.Base = st.Next;
						st.SwapAt = _hfClock - FlagWave.LowerFrom(height);
					}
				}
				else
				{
					st.Swapping = true;
					st.SwapAt = _hfClock;
				}
				st.Next = now;
			}
			else if (HallFlagLook.Same(in a, in now))
			{
				if (now.GreysKnown && !a.GreysKnown)
				{
					if (st.Swapping)
					{
						st.Next = now;
					}
					else
					{
						st.Base = now;
					}
				}
			}
			else if (st.Swapping)
			{
				st.Next = now;
			}
			else
			{
				st.Base = now;
			}
		}

		private bool KeepsTrees(float dist2)
		{
			if (!FromSky)
			{
				return dist2 >= 900f;
			}
			return true;
		}

		private void KeepTree(NearBuilding nb, Building b, Vector2 p, float ground, string family, bool moving)
		{
			float num = p.x - _eyeX;
			float num2 = p.y - _eyeY;
			if (KeptTreesOff || _farBuild || moving || !KeepsTrees(num * num + num2 * num2))
			{
				nb.Kept = null;
				return;
			}
			KeptTree keptTree = nb.Kept;
			if (keptTree == null)
			{
				KeptTree obj = new KeptTree
				{
					Until = _frame + 1 + (int)((uint)b.GetHashCode() % 8u)
				};
				KeptTree keptTree2 = obj;
				nb.Kept = obj;
				keptTree = keptTree2;
			}
			KeptTree keptTree3 = keptTree;
			if (keptTree3.Until <= _frame)
			{
				keptTree3.Until = _frame + 8;
			}
			keptTree3.Asset = b.asset;
			keptTree3.Image = nb.Image;
			keptTree3.X = p.x;
			keptTree3.Y = p.y;
			keptTree3.Ground = ground;
			keptTree3.Family = family;
		}

		private bool KeptTreeDrawn(NearBuilding nb, Building b, Vector2 p, float ground, float dist2, int tag)
		{
			KeptTree kept = nb.Kept;
			if (kept == null || KeptTreesOff || _farBuild || _frame >= kept.Until || !KeepsTrees(dist2))
			{
				return false;
			}
			MeshInstance mesh = nb.Mesh;
			if (mesh == null || mesh.Model == null || b == _shaking || nb.Burnt != null)
			{
				return false;
			}
			if (kept.Asset != b.asset || kept.Image != nb.Image || kept.X != p.x || kept.Y != p.y || kept.Ground != ground || nb.FamilyAsset != b.asset)
			{
				return false;
			}
			float num = Mathf.Sqrt(dist2);
			bool flag = InView(p.x - _eyeX, p.y - _eyeY, Mathf.Max(nb.Width, nb.Height) * 1.3f);
			if (flag == mesh.ShadowOnly)
			{
				return false;
			}
			if (!flag && num > 41f)
			{
				return true;
			}
			if (flag && Models.Detailed(kept.Family, DetailDistance(nb, num), mesh.Model) != mesh.Model)
			{
				return false;
			}
			mesh.Tag = tag;
			mesh.SortDepth = num;
			AddModel(Snowed(mesh));
			TreesKept++;
			return true;
		}

		private static bool MonolithSprite(string id)
		{
			return id?.StartsWith("fx_monolith_", StringComparison.Ordinal) ?? false;
		}

		private void NoteMonolithFlash(BaseEffect effect, SpriteCache sprites)
		{
			MonolithSpritesTaken++;
			Vector3 position = effect.transform.position;
			SpriteRenderer sprite_renderer = effect.sprite_renderer;
			SpriteImage spriteImage = ((sprite_renderer != null && sprite_renderer.sprite != null) ? sprites.Get(sprite_renderer.sprite) : null);
			uint color = ((spriteImage != null) ? GlowColor(spriteImage) : TerrainCache.Rgb(150, 220, 255));
			foreach (MonolithPulse monolithPulse in _monolithPulses)
			{
				if (!((monolithPulse.X - position.x) * (monolithPulse.X - position.x) + (monolithPulse.Y - position.y) * (monolithPulse.Y - position.y) > 9f))
				{
					if (_clock - monolithPulse.Seen > 0.6f || spriteImage != null)
					{
						monolithPulse.Color = color;
					}
					monolithPulse.Seen = _clock;
					return;
				}
			}
			if (EffectTrace != null)
			{
				Log.Info($"MONO flash at {position.x:F1},{position.y:F1}");
			}
			_monolithPulses.Add(new MonolithPulse
			{
				X = position.x,
				Y = position.y,
				Seen = _clock,
				Color = color
			});
		}

		private void MonolithGlow(MeshInstance inst, float x, float y, float ground, float height)
		{
			inst.Flash = 0u;
			float wake = 0f;
			uint wakeColor = 0u;
			for (int num = _monolithPulses.Count - 1; num >= 0; num--)
			{
				MonolithPulse monolithPulse = _monolithPulses[num];
				float num2 = _clock - monolithPulse.Seen;
				if (num2 > 0.6f || num2 < -1f)
				{
					_monolithPulses.RemoveAt(num);
				}
				else if (!((monolithPulse.X - x) * (monolithPulse.X - x) + (monolithPulse.Y - y) * (monolithPulse.Y - y) > 9f))
				{
					float num3 = 1f - Mathf.Clamp01(num2 / 0.6f);
					num3 *= 0.85f + 0.15f * Mathf.Sin(_clock * 18f);
					inst.Flash = (monolithPulse.Color & 0xFFFFFF) | ((uint)(num3 * 30f) << 24);
					MonolithFlashSeen = Mathf.Max(MonolithFlashSeen, (int)(num3 * 30f));
					inst.Static = false;
					float num4 = 0.35f * num3;
					AddLight(x, y, ground + height + 0.8f, 3f + height * 1.3f, (float)(monolithPulse.Color & 0xFF) / 255f * num4, (float)((monolithPulse.Color >> 8) & 0xFF) / 255f * num4, (float)((monolithPulse.Color >> 16) & 0xFF) / 255f * num4, 0.1f * num3);
					wake = num3;
					wakeColor = monolithPulse.Color;
					break;
				}
			}
			TerrainCache terrain = _terrain;
			uint ground2 = 0u;
			if (terrain != null && terrain.Width > 0)
			{
				ground2 = terrain.Colors[Mathf.Clamp((int)y, 0, terrain.Height - 1) * terrain.Width + Mathf.Clamp((int)x, 0, terrain.Width - 1)];
			}
			MonolithLook.Apply(inst.Model, inst.Palette, _night, wake, wakeColor, ground2);
		}

		private bool WalkingIn(Actor a)
		{
			if (_headings.TryGetValue(a, out var value) && value.SeenFrame >= _frame - 1)
			{
				return value.Inside < 1000;
			}
			return false;
		}

		internal bool DrawnWalkingIn(Actor a, out Vector2 at)
		{
			at = default(Vector2);
			if (a == null || !_headings.TryGetValue(a, out var value) || value.SeenFrame < _frame - 2 || value.Inside <= 0 || value.Inside >= 1000)
			{
				return false;
			}
			at = value.Drawn;
			return true;
		}

		private bool StillOutside(Heading h)
		{
			if (h.Inside >= 1000)
			{
				return false;
			}
			h.InsideFor += h.Dt;
			if (Steering.InsideAt(h.Drawn) || h.InsideFor > 5f)
			{
				h.Inside = 1000;
				return false;
			}
			h.Inside++;
			return true;
		}

		private static Vector2 ToTheDoor(Actor a, Heading h, Vector2 p)
		{
			Building inside_building;
			try
			{
				inside_building = a.inside_building;
			}
			catch
			{
				return p;
			}
			if (inside_building == null || !Steering.DoorOf(inside_building, out var door))
			{
				return p;
			}
			float magnitude = (door - h.Drawn).magnitude;
			if (magnitude > 9f)
			{
				h.Inside = 1000;
				return p;
			}
			Vector2 vector = p - door;
			Vector2 vector2 = ((vector.sqrMagnitude > 0.0001f) ? (vector.normalized * 0.5f) : Vector2.zero);
			if (magnitude < 0.9f)
			{
				Steering.NoteCrossing(inside_building);
			}
			if (magnitude < 0.25f)
			{
				h.AtDoor = true;
			}
			if (!h.AtDoor)
			{
				return door;
			}
			return door + vector2;
		}

		private static void NewGroundRound()
		{
			int width = MapBox.width;
			int height = MapBox.height;
			_groundW = ((width > 0 && height > 0) ? width : 0);
			_groundH = ((width > 0 && height > 0) ? height : 0);
			if (++_groundRound == int.MaxValue)
			{
				Array.Clear(_groundAsked, 0, 512);
				_groundRound = 1;
			}
		}

		private static bool OnGround(float x, float y)
		{
			if (!GroundMemo)
			{
				return OnGroundOld(x, y);
			}
			int num = (int)x;
			int num2 = (int)y;
			if (x < (float)num)
			{
				num--;
			}
			if (y < (float)num2)
			{
				num2--;
			}
			if ((uint)num >= (uint)_groundW || (uint)num2 >= (uint)_groundH)
			{
				return OnGroundOld(x, y);
			}
			int num3 = num2 * _groundW + num;
			int num4 = ((num2 << 5) ^ num) & 0x1FF;
			if (_groundAsked[num4] == _groundRound && _groundTile[num4] == num3)
			{
				bool flag = _groundOn[num4];
				if (GroundVerify)
				{
					CheckGround(x, y, flag);
				}
				return flag;
			}
			bool flag2 = OnGroundOld(x, y);
			_groundTile[num4] = num3;
			_groundAsked[num4] = _groundRound;
			_groundOn[num4] = flag2;
			return flag2;
		}

		private static void CheckGround(float x, float y, bool known)
		{
			bool flag = OnGroundOld(x, y);
			GroundVerified++;
			if (flag != known)
			{
				GroundMismatched++;
				if (GroundFirstMismatch == null)
				{
					GroundFirstMismatch = $"tile {Mathf.FloorToInt(x)},{Mathf.FloorToInt(y)} (at {x:R},{y:R}): remembered {known}, the game says {flag}";
				}
			}
		}

		private static bool OnGroundOld(float x, float y)
		{
			try
			{
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(x), Mathf.FloorToInt(y));
				return tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.lava && !tile.Type.block;
			}
			catch
			{
				return true;
			}
		}

		private float WorldStep(float dt)
		{
			if (_worldStepFrame == _frame)
			{
				return _worldStep;
			}
			_worldStepFrame = _frame;
			double num = ((World.world != null) ? WorldPace.ActionNow : (-1.0));
			double num2 = ((_worldTime >= 0.0) ? (num - _worldTime) : ((double)dt));
			_worldTime = num;
			_worldStep = ((num2 >= 0.0 && num2 < 0.5) ? ((float)num2) : dt);
			return _worldStep;
		}

		private Heading Track(Actor a, Vector2 p, float dt, bool smooth, bool steered = false)
		{
			if (!_headings.TryGetValue(a, out var value))
			{
				value = new Heading
				{
					Last = p,
					Drawn = p,
					Angle = (a.flip ? 0f : MathF.PI)
				};
				value.Body.Reset(p.x, p.y, value.Angle);
				_headings[a] = value;
			}
			else
			{
				if (value.SeenFrame == _frame)
				{
					return value;
				}
				if (value.SeenFrame < _frame - 2)
				{
					value.Body.Reset(p.x, p.y, OffscreenCheap ? Onward(a, p, value.Body.Facing) : value.Body.Facing);
					value.Last = p;
					value.Inside = 0;
					value.InsideFor = 0f;
				}
			}
			SceneClock.Sub(SceneClock.B.Route);
			if (GroundMemo)
			{
				NewGroundRound();
			}
			FromDoorway(a, value);
			if (!a.is_inside_building && !Steering.Hidden(a))
			{
				value.Inside = 0;
				value.InsideFor = 0f;
				value.AtDoor = false;
			}
			else if (a.is_inside_building && smooth)
			{
				p = ToTheDoor(a, value, p);
			}
			float num = WorldStep(dt);
			if (value.Owed > 0f)
			{
				num += value.Owed;
				value.Owed = 0f;
			}
			value.Last = p;
			value.SeenFrame = _frame;
			value.Dt = num;
			bool flag = false;
			bool brisk = false;
			try
			{
				flag = a.under_forces;
				brisk = a.has_attack_target;
			}
			catch
			{
			}
			if (smooth)
			{
				Watch?.Invoke(a, p, dt, flag, value.Size);
			}
			bool flag2 = smooth && !flag;
			Func<float, float, bool> func = (steered ? SteeredRound : null);
			Func<float, float, bool> blocked = func;
			int num2;
			if (flag2 && (steered || Walks(a)))
			{
				num2 = (OnGround(p.x, p.y) ? 1 : 0);
				if (num2 != 0)
				{
					blocked = (steered ? OffGroundSteered : OffGround);
				}
			}
			else
			{
				num2 = 0;
			}
			value.Body.BeginAhead(p.x, p.y);
			if (flag2)
			{
				RouteAhead(a, value.Body, value.Body.AheadWanted(value.Size), func);
			}
			SceneClock.Sub(SceneClock.B.Step);
			value.Body.Step(p.x, p.y, num, value.Size, flag2, !flag, blocked, brisk);
			SceneClock.Sub(SceneClock.B.Face);
			value.Drawn = new Vector2(value.Body.X, value.Body.Y);
			value.Speed = value.Body.Speed;
			value.Blocked = blocked;
			if (num2 != 0)
			{
				NoteDrawn(value);
			}
			float f = Mathf.Max(0.2f, value.Size);
			Vector2 face;
			if (LookAt.TryGetValue(a, out var value2) && (value2 - value.Drawn).sqrMagnitude > 0.0001f)
			{
				value.Angle = Turn.Damp(value.Angle, ref value.TurnRate, Mathf.Atan2(value2.y - value.Drawn.y, value2.x - value.Drawn.x), dt, 0.3f, 200f / Mathf.Sqrt(f) * (MathF.PI / 180f));
				value.Looking = true;
			}
			else if (smooth && value.Body.RealSpeed < 0.2f && Attention.FaceTo(a, out face) && (face - value.Drawn).sqrMagnitude > 0.0001f)
			{
				value.Angle = Turn.Damp(value.Angle, ref value.TurnRate, Mathf.Atan2(face.y - value.Drawn.y, face.x - value.Drawn.x), dt, 0.45f, 150f / Mathf.Sqrt(f) * (MathF.PI / 180f));
				value.Looking = true;
			}
			else if (value.Looking)
			{
				value.Angle = Turn.Damp(value.Angle, ref value.TurnRate, value.Body.Facing, dt, 0.3f, 200f / Mathf.Sqrt(f) * (MathF.PI / 180f));
				if (Mathf.Abs(Mathf.DeltaAngle(value.Angle * 57.29578f, value.Body.Facing * 57.29578f)) < 2f)
				{
					value.Looking = false;
				}
			}
			else
			{
				value.Angle = value.Body.Facing;
				value.TurnRate = 0f;
			}
			return value;
		}

		private static void FromDoorway(Actor a, Heading h)
		{
			if (Steering.TakeExit(a, out var doorway, out var outward))
			{
				h.Body.Reset(doorway.x, doorway.y, outward);
				h.Angle = outward;
				h.TurnRate = 0f;
				h.Drawn = doorway;
			}
		}

		internal bool MotionForTest(Actor a, out Vector2 drawn, out Vector2 real, out float angle, out float speed, out int frame, out float dt)
		{
			Heading value;
			bool flag = _headings.TryGetValue(a, out value);
			dt = (flag ? value.Dt : 0f);
			drawn = (flag ? value.Drawn : default(Vector2));
			real = (flag ? value.Last : default(Vector2));
			angle = (flag ? value.Angle : 0f);
			speed = (flag ? value.Speed : 0f);
			frame = (flag ? value.SeenFrame : (-1));
			return flag;
		}

		private static void RouteAhead(Actor a, BodyMotion body, float enough, Func<float, float, bool> solid)
		{
			try
			{
				if (!a.is_moving)
				{
					return;
				}
				Vector2 next_step_position = a.next_step_position;
				if (!body.AddAhead(next_step_position.x, next_step_position.y, enough, solid))
				{
					return;
				}
				List<WorldTile> current_path = a.current_path;
				if (current_path == null)
				{
					return;
				}
				for (int i = Mathf.Max(0, a.current_path_index); i < current_path.Count; i++)
				{
					WorldTile worldTile = current_path[i];
					if (worldTile == null || !body.AddAhead(worldTile.posV3.x, worldTile.posV3.y, enough, solid))
					{
						break;
					}
				}
			}
			catch
			{
			}
		}

		private void AddPlayerHands(ViewState view, TerrainCache terrain, float dt)
		{
			WorldSync.GodPlayers(_netGods);
			foreach (KeyValuePair<int, Vector2> netGod in _netGods)
			{
				Vector2 value = netGod.Value;
				float num = value.x - view.X;
				float num2 = value.y - view.Y;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				if (num3 > 240f)
				{
					continue;
				}
				MeshModel meshModel = ((num3 > 90f) ? (Models.Get("god_hand_f") ?? Models.Get("god_hand")) : ((num3 > 32f) ? (Models.Get("god_hand_m") ?? Models.Get("god_hand")) : Models.Get("god_hand")));
				if (meshModel != null)
				{
					object key = "net_god_" + netGod.Key;
					if (!_godArms.TryGetValue(key, out var value2))
					{
						Dictionary<object, GodArm> godArms = _godArms;
						GodArm obj = new GodArm
						{
							Fx = new GodHandFx((uint)(9000 + netGod.Key))
							{
								Presence = 0f
							},
							LastPos = value,
							Finger = true
						};
						value2 = obj;
						godArms[key] = obj;
					}
					value2.Seen = _frame;
					value2.Fx.Presence = Mathf.MoveTowards(value2.Fx.Presence, 1f, dt * 1.2f);
					Vector2 vector = Vector2.Lerp(value2.LastPos, value, Mathf.Clamp01(dt * 3f));
					Vector2 vector2 = ((dt > 0f) ? ((vector - value2.LastPos) / Mathf.Max(dt, 0.001f)) : Vector2.zero);
					value2.LastPos = vector;
					Vec3 up = value2.Fx.Steer(vector2.x, vector2.y, dt, _clock);
					float num4 = terrain.SampleHeight(vector.x, vector.y);
					Vec3 tip = new Vec3(vector.x, vector.y, num4 + 12f);
					Vec3 palm = new Vec3(view.X - vector.x, view.Y - vector.y, 0f).Normalized();
					value2.Fx.Finger(_blastSink, BlastParticles, meshModel, tip, up, palm, drawing: false, num4, _clock, dt, _night);
					value2.Model = meshModel;
					value2.Tip = tip;
					value2.Up = up;
					value2.Palm = palm;
					value2.Ground = num4;
				}
			}
		}

		private bool Offscreen(Actor a, Vector2 p, float dx, float dy, float fx, float fy, float dist2)
		{
			if (!OffscreenCheap || dist2 < 1600f)
			{
				return false;
			}
			if (a.asset != null && a.asset.is_boat)
			{
				return false;
			}
			float num = Mathf.Sqrt(dist2);
			bool num2 = dx * fx + dy * fy < -8f;
			bool flag = num > 48f && !InView(p.x - _eyeX, p.y - _eyeY, 4f + 0.3f * num);
			if (!num2 && !flag)
			{
				return false;
			}
			OffscreenSkipped++;
			return true;
		}

		private static float Onward(Actor a, Vector2 p, float facing)
		{
			try
			{
				if (!a.is_moving)
				{
					return facing;
				}
				Vector2 vector = a.next_step_position - p;
				return (vector.sqrMagnitude > 0.0001f) ? Mathf.Atan2(vector.y, vector.x) : facing;
			}
			catch
			{
				return facing;
			}
		}

		private void ResetOwnBody()
		{
			for (int i = 0; i < _ownShadowed.Count; i++)
			{
				if (_ownShadowed[i] != null)
				{
					_ownShadowed[i].ShadowOnly = false;
				}
			}
			_ownShadowed.Clear();
			OwnShadowsForTest = (OwnLegsForTest = 0);
			People.HostShown = null;
		}

		private bool OwnBodyOf(Actor a)
		{
			try
			{
				return OwnBody > 0 && Models.Ready && People.IsHumanoid(a) && !IsSwimming(a) && !BoatHelm.Steers(a) && !Crabzillas.Is(a);
			}
			catch
			{
				return false;
			}
		}

		private void AddOwnBody(Actor a, SpriteImage image, SpriteCache sprites, float x, float y, float z, float body, Heading track, ViewState view, float dt, VoxelObject template, float hitAge)
		{
			float num = view.Yaw;
			Vector2 velocity = PossessionHooks.Velocity;
			if (velocity.sqrMagnitude > 0.0004f)
			{
				float num2 = Mathf.DeltaAngle(view.Yaw * 57.29578f, Mathf.Atan2(velocity.y, velocity.x) * 57.29578f);
				if (Mathf.Abs(num2) <= 100f)
				{
					num = view.Yaw + Mathf.Clamp(num2, -60f, 60f) * (MathF.PI / 180f);
				}
			}
			if (float.IsNaN(_ownHeading))
			{
				_ownHeading = num;
				_ownTurn = 0f;
			}
			_ownHeading = Turn.Damp(_ownHeading, ref _ownTurn, num, dt, 0.08f, 15.707963f);
			bool flag = false;
			if (People.HostRest.On && WorldBoxMod.Instance != null && WorldBoxMod.Instance.PoseBodyAt(new Vector2(x, y), z, out var at, out var z2))
			{
				flag = true;
				x = at.x;
				y = at.y;
				z = z2;
				_ownHeading = People.HostRest.Heading;
				_ownTurn = 0f;
			}
			int count = Meshes.Count;
			int count2 = Voxels.Count;
			People.HostShown = a;
			People._asHost = true;
			try
			{
				People.AddMesh(Meshes, Voxels, a, image, sprites, x, y, z, body, _ownHeading, track.Speed, 0.5f, dt, template, swimming: false, hitAge);
			}
			finally
			{
				People._asHost = false;
			}
			if (Voxels.Count > count2)
			{
				Voxels.RemoveRange(count2, Voxels.Count - count2);
			}
			float num3 = (0f - Mathf.Atan(view.Pitch)) * 57.29578f;
			float num4 = ((OwnBody >= 2 && !OwnWhole) ? Mathf.SmoothStep(0f, 1f, (num3 - 42f) / 14f) : 0f);
			float num5 = (flag ? 0f : (0.05f * body));
			float num6 = Mathf.Cos(view.Yaw) * num5;
			float num7 = Mathf.Sin(view.Yaw) * num5;
			int count3 = Meshes.Count;
			int num8 = 0;
			OwnShadowsForTest = count3 - count;
			for (int i = count; i < count3; i++)
			{
				MeshInstance meshInstance = Meshes[i];
				meshInstance.ShadowOnly = !OwnWhole;
				_ownShadowed.Add(meshInstance);
				meshInstance.Fade = 1f;
				meshInstance.Tag = 2147483646;
				if (num4 <= 0.01f || meshInstance.Model == null)
				{
					continue;
				}
				ulong num9 = LegsHide(meshInstance.Model, flag);
				if (num9 != ulong.MaxValue)
				{
					if (num8 >= _ownLegs.Count)
					{
						_ownLegs.Add(new MeshInstance());
					}
					MeshInstance meshInstance2 = _ownLegs[num8++];
					CopyPose(meshInstance, meshInstance2);
					meshInstance2.Hidden = meshInstance.Hidden | num9;
					meshInstance2.Position = new Vec3(meshInstance.Position.X + num6, meshInstance.Position.Y + num7, meshInstance.Position.Z);
					meshInstance2.Fade = num4;
					Meshes.Add(meshInstance2);
				}
			}
			OwnLegsForTest = num8;
		}

		private ulong LegsHide(MeshModel model, bool thighs = false)
		{
			Dictionary<MeshModel, ulong> dictionary = (thighs ? _thighsHide : _legsHide);
			if (dictionary.TryGetValue(model, out var value))
			{
				return value;
			}
			value = 0uL;
			bool flag = false;
			for (int i = 0; i < model.Parts.Length && i < 64; i++)
			{
				string text = model.Parts[i].Name ?? "";
				if (text == "lower_leg_L" || text == "lower_leg_R" || (thighs && (text == "upper_leg_L" || text == "upper_leg_R")))
				{
					flag = true;
				}
				else
				{
					value |= (ulong)(1L << i);
				}
			}
			if (!flag)
			{
				value = ulong.MaxValue;
			}
			dictionary[model] = value;
			return value;
		}

		private static void CopyPose(MeshInstance src, MeshInstance dst)
		{
			dst.Model = src.Model;
			dst.Position = src.Position;
			dst.Basis = src.Basis;
			dst.PartRotation = src.PartRotation;
			dst.PartOffset = src.PartOffset;
			dst.PartScale = src.PartScale;
			dst.Palette = src.Palette;
			dst.Flash = src.Flash;
			dst.Multiply = src.Multiply;
			dst.Tag = 2147483646;
			dst.Emissive = false;
			dst.NoShadow = true;
			dst.Static = false;
			dst.ShadowOnly = false;
			dst.Glow = 0f;
			dst.Snow = 0f;
			dst.SortDepth = 0.2f;
		}

		private void StepHeatRay(MapBox world, TerrainCache terrain, float dt)
		{
			HeatRayEffect heatRayEffect = null;
			try
			{
				heatRayEffect = world.heat_ray_fx;
			}
			catch
			{
			}
			bool flag = heatRayEffect != null && heatRayEffect.gameObject.activeSelf && (heatRayEffect.active || heatRayEffect.rayScaleY > 0f);
			if (!flag && _heatRay == null)
			{
				return;
			}
			if (flag)
			{
				Vector3 position = heatRayEffect.transform.position;
				float reach = Mathf.Clamp01(heatRayEffect.rayScaleY / 2000f);
				if (_heatRay == null)
				{
					_heatRay = new HeatRayFx((uint)(Time.frameCount * -1640531535));
					Log.Info($"POWER heat ray at {position.x:F0},{position.y:F0}");
				}
				float num = position.x - _eyeX;
				float num2 = position.y - _eyeY;
				float num3 = SkyRange(260f);
				if (num * num + num2 * num2 > num3 * num3)
				{
					reach = 0f;
				}
				_heatRay.Aim(position.x + 0.5f, position.y + 0.5f, reach, heatRayEffect.touchedGround);
			}
			else
			{
				_heatRay.Aim(_heatRay.X, _heatRay.Y, Mathf.Max(0f, _heatRay.Reach - dt * 2f), touching: false);
			}
			_heatRay.Step(dt, _blastSink, BlastParticles, _clock, LandColor(terrain, _heatRay.X, _heatRay.Y));
			HeatRayForTest = _heatRay;
			if (!flag && _heatRay.Done)
			{
				_heatRay = null;
				HeatRayForTest = null;
			}
		}

		private bool DrawPowerDrop(Drop d, string id, float x, float y, float ground, float near)
		{
			bool flag = id == "crab_bomb";
			bool flag2 = id == "crab_bomb_shrapnel";
			if (!flag && !flag2)
			{
				return false;
			}
			MeshModel meshModel = (Models.Ready ? Models.Get(flag ? "crab_bomb" : "crab_bomb_crab") : null);
			if (meshModel == null)
			{
				return false;
			}
			if (!_crabDrops.TryGetValue(d, out var value) || value.Shrapnel != flag2)
			{
				value = new CrabDrop
				{
					Shrapnel = flag2,
					Facing = (float)(d.GetHashCode() % 628) * 0.01f
				};
				_crabDrops[d] = value;
			}
			value.Seen = _frame;
			value.X = x;
			value.Y = y;
			float size = Mathf.Max(1f, SkyLeast(x, y, 6f) / 1.5f);
			if (flag)
			{
				float num = Mathf.Max(6f, Weather.Base + 4f - ground);
				float z = ground + Mathf.Clamp01(d._currentHeightZ / 65f) * num;
				CrabBombFx.DrawFalling(_blastSink, meshModel, value.Pool, x, y, z, value.Facing, _clock, (uint)d.GetHashCode(), size);
				if (FromSky)
				{
					SkyFallCue(x, y, z, ground, size);
				}
			}
			else
			{
				float z2 = ground + 0.35f + Mathf.Max(0f, d._currentHeightZ) * 0.22f;
				float tumble = d.transform.eulerAngles.z * (MathF.PI / 180f);
				Vector2 vector = d._targetPosition - d._startPosition;
				float heading = ((vector.sqrMagnitude > 0.0001f) ? Mathf.Atan2(vector.y, vector.x) : value.Facing);
				CrabBombFx.DrawShrapnel(_blastSink, BlastParticles, meshModel, value.Crab, new Vec3(x, y, z2), tumble, heading, _clock, (uint)d.GetHashCode(), _frameDt, size);
			}
			return true;
		}

		private void StepCrabBombs(TerrainCache terrain, float dt)
		{
			_crabGone.Clear();
			foreach (KeyValuePair<Drop, CrabDrop> crabDrop2 in _crabDrops)
			{
				if (_frame - crabDrop2.Value.Seen >= 2)
				{
					_crabGone.Add(crabDrop2.Key);
				}
			}
			foreach (Drop item in _crabGone)
			{
				CrabDrop crabDrop = _crabDrops[item];
				if (!crabDrop.Shrapnel && _frame - crabDrop.Seen <= 3)
				{
					CrabLandings.Add(new CrabLandFx(crabDrop.X, crabDrop.Y, crabDrop.Facing, (uint)item.GetHashCode()));
				}
				_crabDrops.Remove(item);
			}
			MeshModel model = (Models.Ready ? Models.Get("crab_bomb") : null);
			for (int num = CrabLandings.Count - 1; num >= 0; num--)
			{
				CrabLandings[num].Step(dt, _blastSink, BlastParticles, model);
				if (CrabLandings[num].Finished)
				{
					CrabLandings.RemoveAt(num);
				}
			}
			for (int num2 = CrabBlasts.Count - 1; num2 >= 0; num2--)
			{
				CrabBlasts[num2].Step(dt, _blastSink, BlastParticles);
				if (CrabBlasts[num2].Finished)
				{
					CrabBlasts.RemoveAt(num2);
				}
			}
		}

		private void AddBoulder(Boulder b, TerrainCache terrain)
		{
			if (!(b.current_position == Vector2.zero))
			{
				double timestamp_spawned = ((BaseEffect)b)._timestamp_spawned;
				if (!_balls.TryGetValue(b, out var value) || value.Stamp != timestamp_spawned)
				{
					value = new Ball
					{
						Fx = new BowlingBallFx((uint)b.GetHashCode() ^ (uint)(timestamp_spawned * 1000.0)),
						Bounces = b._bounces_left,
						Stamp = timestamp_spawned
					};
					_balls[b] = value;
					Log.Info($"POWER bowling ball at {b.current_position.x:F0},{b.current_position.y:F0}");
				}
				Vector2 current_position = b.current_position;
				value.Seen = _frame;
				if (b._bounces_left < value.Bounces)
				{
					BallStrike(value.X, value.Y, terrain, final: false, (uint)(b.GetHashCode() + b._bounces_left * 7919));
				}
				value.Bounces = b._bounces_left;
				value.X = current_position.x;
				value.Y = current_position.y;
				value.Height = b.position_height;
				BallAtForTest = current_position;
				float num = current_position.x - _eyeX;
				float num2 = current_position.y - _eyeY;
				float num3 = SkyRange(160f);
				if (!(num * num + num2 * num2 > num3 * num3))
				{
					value.Fx.Draw(_blastSink, BlastParticles, Models.Ready ? Models.Get("bowling_ball") : null, current_position.x, current_position.y, Mathf.Max(0f, b.position_height), _frameDt, _clock);
				}
			}
		}

		private void BallStrike(float x, float y, TerrainCache terrain, bool final, uint seed)
		{
			int num = Mathf.Clamp((int)x, 0, terrain.Width - 1);
			int num2 = Mathf.Clamp((int)y, 0, terrain.Height - 1);
			bool flag = terrain.Flags.Length != 0 && (terrain.Flags[num2 * terrain.Width + num] & 1) != 0;
			BowlImpacts.Add(new BowlImpactFx(x, y, terrain.SampleHeight(x, y), 1f, seed, LandColor(terrain, x, y), flag, final));
			float distance = Mathf.Sqrt((x - _eyeX) * (x - _eyeX) + (y - _eyeY) * (y - _eyeY));
			if (final)
			{
				FirstPersonAudio.QueueRumble(distance);
			}
			Log.Info(string.Format("POWER bowling ball {0} at {1:F0},{2:F0}{3}", final ? "bursts" : "strikes", x, y, flag ? " (water)" : ""));
		}

		private void StepBowling(TerrainCache terrain, float dt)
		{
			_ballsGone.Clear();
			foreach (KeyValuePair<Boulder, Ball> ball2 in _balls)
			{
				if (ball2.Value.Seen != _frame)
				{
					_ballsGone.Add(ball2.Key);
				}
			}
			foreach (Boulder item in _ballsGone)
			{
				Ball ball = _balls[item];
				if (_frame - ball.Seen <= 2)
				{
					BallStrike(ball.X, ball.Y, terrain, final: true, (uint)(item.GetHashCode() ^ 0xB0B0));
				}
				_balls.Remove(item);
			}
			for (int num = BowlImpacts.Count - 1; num >= 0; num--)
			{
				BowlImpacts[num].Step(dt, _blastSink, BlastParticles);
				if (BowlImpacts[num].Finished)
				{
					BowlImpacts.RemoveAt(num);
				}
			}
		}

		private bool BallNear(float x, float y, float radius)
		{
			foreach (Ball value in _balls.Values)
			{
				if ((value.X - x) * (value.X - x) + (value.Y - y) * (value.Y - y) < radius * radius)
				{
					return true;
				}
			}
			foreach (BowlImpactFx bowlImpact in BowlImpacts)
			{
				if (bowlImpact.Age < 2f && (bowlImpact.X - x) * (bowlImpact.X - x) + (bowlImpact.Y - y) * (bowlImpact.Y - y) < radius * radius)
				{
					return true;
				}
			}
			return false;
		}

		private static bool IsPowerSprite(string id)
		{
			switch (id)
			{
			default:
				return id == "fx_explosion_small";
			case "fx_explosion_crab_bomb":
			case "fx_boulder_impact":
			case "fx_boulder_impact_water":
				return true;
			}
		}

		private bool TakePowerEffect(string id, BaseEffect effect, TerrainCache terrain)
		{
			Vector3 position = effect.transform.position;
			switch (id)
			{
			case "fx_explosion_crab_bomb":
			{
				double timestamp_spawned = effect._timestamp_spawned;
				if (!_powerFxSeen.TryGetValue(effect, out var value) || value != timestamp_spawned)
				{
					_powerFxSeen[effect] = timestamp_spawned;
					if (_powerFxSeen.Count > 64)
					{
						_powerFxSeen.Clear();
					}
					CrabBlasts.Add(new CrabBlastFx(position.x, position.y, terrain.SampleHeight(position.x, position.y), 1f, (uint)(timestamp_spawned * 1000.0) ^ (uint)effect.GetHashCode(), LandColor(terrain, position.x, position.y)));
					if (CrabBlasts.Count > 40)
					{
						CrabBlasts.RemoveAt(0);
					}
				}
				return true;
			}
			case "fx_boulder_impact":
			case "fx_boulder_impact_water":
				return BallNear(position.x, position.y, 12f);
			case "fx_explosion_small":
				return BallNear(position.x, position.y, 8f);
			default:
				return false;
			}
		}

		private void MeteorPath(Meteorite m, Vector3 target, float ground, out Vec3 head, out Vec3 back)
		{
			Vector2 current_position = m.current_position;
			float magnitude = current_position.magnitude;
			if (!_meteorWays.TryGetValue(m, out var value) || magnitude > value.Start + 1f)
			{
				value = new MeteorWay
				{
					Start = Mathf.Max(magnitude, 1f)
				};
				float num = target.x - _eyeX;
				float num2 = target.y - _eyeY;
				float f = ((Mathf.Sqrt(num * num + num2 * num2) > 2f) ? Mathf.Atan2(num2, num) : (MathF.PI / 2f));
				value.Dir = new Vec3(Mathf.Cos(f) * 30f, Mathf.Sin(f) * 30f, 150f);
				_meteorWays[m] = value;
				try
				{
					m._falling_speed = Mathf.Min(m._falling_speed, value.Start / 3.4f);
				}
				catch
				{
				}
			}
			back = value.Dir.Normalized();
			head = new Vec3(target.x, target.y, ground + 0.6f) + value.Dir * Mathf.Clamp01(magnitude / value.Start);
		}

		private static void RestoreMeteor(Meteorite m)
		{
			try
			{
				m._falling_speed = 200f;
			}
			catch
			{
			}
		}

		private void StepPowers(MapBox world, TerrainCache terrain, float dt)
		{
			StepHeatRay(world, terrain, dt);
			StepCrabBombs(terrain, dt);
			StepBowling(terrain, dt);
			StepCannons(terrain, dt);
		}

		private void ClearPowers()
		{
			_heatRay = null;
			HeatRayForTest = null;
			_crabDrops.Clear();
			CrabLandings.Clear();
			CrabBlasts.Clear();
			_balls.Clear();
			BowlImpacts.Clear();
			ClearCannons();
			foreach (BaseEffect key in _meteorWays.Keys)
			{
				if (key is Meteorite meteorite && meteorite != null)
				{
					RestoreMeteor(meteorite);
				}
			}
			_meteorWays.Clear();
		}

		internal static float CropGrowth(Building b)
		{
			try
			{
				Wheat component_wheat = b.component_wheat;
				if (component_wheat == null || b.asset == null)
				{
					return 1f;
				}
				if (component_wheat.isMaxLevel())
				{
					return 1f;
				}
				float num = Mathf.Max(0.01f, b.asset.growth_time);
				int num2 = Mathf.Max(1, component_wheat._max_level);
				return Mathf.Clamp01(b.data.grow_time / (num * (float)num2));
			}
			catch
			{
				return 1f;
			}
		}

		private static bool IsCrop(Building b)
		{
			if (b.asset != null)
			{
				return b.asset.building_type == BuildingType.Building_Wheat;
			}
			return false;
		}

		private bool DrawCrop(NearBuilding nb, Building b, Vector2 p, float dist2, int tag)
		{
			WorldTile current_tile = b.current_tile;
			if (current_tile == null)
			{
				return false;
			}
			float num = CropGrowth(b);
			int key = current_tile.y * 100003 + current_tile.x;
			if (!_crops.TryGetValue(key, out var value) || value.Building != b)
			{
				Dictionary<int, CropSeen> crops = _crops;
				CropSeen obj = new CropSeen
				{
					Building = b,
					Tile = current_tile
				};
				value = obj;
				crops[key] = obj;
			}
			value.Growth = num;
			float num2 = Mathf.Sqrt(dist2);
			if (num2 > 44f)
			{
				return true;
			}
			float num3 = (float)current_tile.x + 0.5f;
			float num4 = (float)current_tile.y + 0.5f;
			if (!InView(num3 - _eyeX, num4 - _eyeY, 1.2f))
			{
				return true;
			}
			int num5 = ((!(num < 0.2f)) ? ((num < 0.45f) ? 1 : ((num < 0.85f) ? 2 : 3)) : 0);
			if (num2 > CropDrawTo[num5])
			{
				return true;
			}
			float num6;
			switch (num5)
			{
			default:
				num6 = 1f;
				break;
			case 2:
				num6 = (num - 0.45f) / 0.4f;
				break;
			case 1:
				num6 = (num - 0.2f) / 0.25f;
				break;
			case 0:
				num6 = num / 0.2f;
				break;
			}
			float value2 = num6;
			MeshModel meshModel = (BuildingInfo ? CropModelNamed(num5, num2) : CropModel("crop_wheat_" + num5, num2, num5));
			if (meshModel == null)
			{
				return false;
			}
			MeshInstance meshInstance = nb.Mesh;
			if (meshInstance == null)
			{
				MeshInstance obj2 = new MeshInstance
				{
					NoShadow = true
				};
				MeshInstance meshInstance2 = obj2;
				nb.Mesh = obj2;
				meshInstance = meshInstance2;
			}
			MeshInstance meshInstance3 = meshInstance;
			meshInstance3.Model = meshModel;
			meshInstance3.Palette = CropPalette(meshModel, num, GroundColor(current_tile));
			float num7 = Mathf.Min(_terrain.SampleHeight(num3, num4), Mathf.Min(_terrain.SampleHeight(num3 - 0.45f, num4 - 0.45f), _terrain.SampleHeight(num3 + 0.45f, num4 + 0.45f)));
			float sz = 0.5f * (0.7f + 0.3f * Mathf.Clamp01(value2)) * (0.92f + 0.16f * (float)((TerrainModel.Hash(current_tile.x, current_tile.y * 3) >> 8) & 0xFF) / 255f);
			meshInstance3.Place(num3, num4, num7 - 0.01f, -MathF.PI / 2f, 0.5f, 0.5f, sz);
			bool flag = num2 < 14f;
			if (flag)
			{
				Sway(meshInstance3, num3, num4, (num5 >= 2) ? 0.09f : ((num5 == 1) ? 0.05f : 0.015f));
			}
			meshInstance3.Static = !flag;
			meshInstance3.Tag = tag;
			meshInstance3.SortDepth = num2;
			meshInstance3.Multiply = 0u;
			Meshes.Add(meshInstance3);
			return true;
		}

		private static MeshModel CropModel(string family, float dist, int stage)
		{
			return Models.Get((dist < CropNearTo[stage]) ? family : ((dist < CropMiddleTo[stage]) ? (family + "_m") : (family + "_f"))) ?? Models.Get(family);
		}

		private void Sway(MeshInstance inst, float x, float y, float amount)
		{
			float clock = _clock;
			float num = Mathf.Sin(clock * 1.6f + x * 0.55f + y * 0.35f) * 0.7f + Mathf.Sin(clock * 2.7f + x * 1.3f - y * 0.9f) * 0.3f;
			float num2 = 0.6f + 0.4f * Mathf.Sin(clock * 0.37f + x * 0.05f);
			float num3 = num * num2 * amount;
			Vec3 c = inst.Basis.C2;
			inst.Basis = new Mat3(inst.Basis.C0, inst.Basis.C1, new Vec3(c.X + num3 * 0.8f, c.Y + num3 * 0.45f, c.Z));
		}

		private uint GroundColor(WorldTile t)
		{
			if (_terrain == null || _terrain.Width == 0)
			{
				return TerrainCache.Rgb(110, 84, 58);
			}
			int num = Mathf.Clamp(t.x, 0, _terrain.Width - 1);
			int num2 = Mathf.Clamp(t.y, 0, _terrain.Height - 1);
			return _terrain.Colors[num2 * _terrain.Width + num];
		}

		private uint[] CropPalette(MeshModel model, float growth, uint soil)
		{
			int key = Mathf.Clamp((int)(growth * 31f), 0, 31) * 31 + (int)((soil & 0xF0F0F0) * 37) + model.SlotNames.Length;
			if (_cropPalettes.TryGetValue(key, out var value))
			{
				return value;
			}
			if (_cropPalettes.Count > 512)
			{
				_cropPalettes.Clear();
			}
			float num = Mathf.Clamp01((growth - 0.5f) / 0.45f);
			value = new uint[model.SlotNames.Length];
			uint num2 = TerrainCache.Scale(TerrainCache.Lerp(soil, TerrainCache.Rgb(104, 78, 54), 0.4f), 0.85f);
			for (int i = 0; i < value.Length; i++)
			{
				switch (model.SlotNames[i])
				{
				case "stalk":
					value[i] = TerrainCache.Lerp(CropStem, StemGold, num);
					break;
				case "leaf":
					value[i] = TerrainCache.Lerp(CropGreen, TerrainCache.Rgb(170, 158, 82), num * 0.8f);
					break;
				case "leaf_dark":
					value[i] = TerrainCache.Lerp(CropGreenDark, TerrainCache.Rgb(130, 116, 62), num * 0.8f);
					break;
				case "ear":
					value[i] = TerrainCache.Lerp(EarGreen, EarGold, num);
					break;
				case "ear_light":
					value[i] = TerrainCache.Lerp(TerrainCache.Lerp(EarGreen, EarPale, 0.4f), EarPale, num);
					break;
				case "soil":
					value[i] = num2;
					break;
				case "straw":
					value[i] = Straw;
					break;
				}
			}
			_cropPalettes[key] = value;
			return value;
		}

		private void CollectCrops(ViewState view, float dt)
		{
			_cropCheck -= dt;
			if (_cropCheck <= 0f)
			{
				_cropCheck = 0.25f;
				_cropScratch.Clear();
				foreach (KeyValuePair<int, CropSeen> crop in _crops)
				{
					CropSeen value = crop.Value;
					bool flag;
					try
					{
						flag = value.Building == null || !value.Building.isAlive() || value.Tile.building != value.Building;
					}
					catch
					{
						flag = true;
					}
					float num = (float)value.Tile.x - view.X;
					float num2 = (float)value.Tile.y - view.Y;
					if (flag)
					{
						if (value.Growth > 0.6f && num * num + num2 * num2 < 1936f)
						{
							Reaped(value);
						}
						_cropScratch.Add(crop.Key);
					}
					else if (num * num + num2 * num2 > 4900f)
					{
						_cropScratch.Add(crop.Key);
					}
				}
				foreach (int item in _cropScratch)
				{
					_crops.Remove(item);
				}
			}
			for (int num3 = _stubble.Count - 1; num3 >= 0; num3--)
			{
				Stubble stubble = _stubble[num3];
				stubble.Age += dt;
				WorldTile worldTile = null;
				try
				{
					worldTile = World.world.GetTile(stubble.X, stubble.Y);
				}
				catch
				{
				}
				bool flag2 = worldTile != null && worldTile.building != null && worldTile.building.asset != null && worldTile.building.asset.building_type == BuildingType.Building_Wheat;
				if (stubble.Age > 75f || flag2)
				{
					_stubble.RemoveAt(num3);
				}
				else
				{
					float num4 = (float)stubble.X + 0.5f;
					float num5 = (float)stubble.Y + 0.5f;
					float num6 = num4 - view.X;
					float num7 = num5 - view.Y;
					float num8 = Mathf.Sqrt(num6 * num6 + num7 * num7);
					if (!(num8 > CropDrawTo[4]) && InView(num6, num7, 1.2f))
					{
						MeshModel meshModel = CropModel("crop_stubble", num8, 4);
						if (meshModel != null)
						{
							MeshInstance meshInstance = stubble.Mesh;
							if (meshInstance == null)
							{
								MeshInstance obj3 = new MeshInstance
								{
									NoShadow = true
								};
								MeshInstance meshInstance2 = obj3;
								stubble.Mesh = obj3;
								meshInstance = meshInstance2;
							}
							MeshInstance meshInstance3 = meshInstance;
							meshInstance3.Model = meshModel;
							meshInstance3.Palette = CropPalette(meshModel, 1f, (worldTile != null) ? GroundColor(worldTile) : TerrainCache.Rgb(110, 84, 58));
							meshInstance3.Place(num4, num5, _terrain.SampleHeight(num4, num5) - 0.01f, -MathF.PI / 2f, 0.5f, 0.5f, 0.5f);
							meshInstance3.Fade = Mathf.Clamp01((75f - stubble.Age) / 10f);
							meshInstance3.SortDepth = num8;
							Meshes.Add(meshInstance3);
						}
					}
				}
			}
		}

		private void Reaped(CropSeen c)
		{
			if (_stubble.Exists((Stubble s) => s.X == c.Tile.x && s.Y == c.Tile.y))
			{
				return;
			}
			_stubble.Add(new Stubble
			{
				X = c.Tile.x,
				Y = c.Tile.y
			});
			if (_stubble.Count > 160)
			{
				_stubble.RemoveAt(0);
			}
			if (_terrain == null)
			{
				return;
			}
			float num = (float)c.Tile.x + 0.5f;
			float num2 = (float)c.Tile.y + 0.5f;
			float num3 = _terrain.SampleHeight(num, num2);
			for (int num4 = 0; num4 < 10; num4++)
			{
				if (_chips.Count >= 80)
				{
					break;
				}
				uint color = (((num4 & 1) == 0) ? EarGold : Straw);
				_chips.Add(new Chip
				{
					P = new Vec3(num + (float)(_chipRandom.NextDouble() - 0.5) * 0.8f, num2 + (float)(_chipRandom.NextDouble() - 0.5) * 0.8f, num3 + 0.3f),
					V = new Vec3((float)(_chipRandom.NextDouble() - 0.5) * 1.2f, (float)(_chipRandom.NextDouble() - 0.5) * 1.2f, 1f + (float)_chipRandom.NextDouble() * 1.4f),
					Life = 0.9f + (float)_chipRandom.NextDouble() * 0.7f,
					Size = 0.025f + (float)_chipRandom.NextDouble() * 0.03f,
					Spin = (float)_chipRandom.NextDouble() * 6f,
					Shape = ChipShape(color)
				});
			}
		}

		internal string CropReport()
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (NearBuilding building2 in _buildings)
			{
				Building building = building2.Building;
				if (building != null && building.asset != null && IsCrop(building))
				{
					num++;
					if (!building.isAlive())
					{
						num2++;
					}
					if (building2.Image == null)
					{
						num3++;
					}
					if (building2.MeshFamily != null)
					{
						num4++;
					}
				}
			}
			return $"crops near {num}, dead {num2}, without a sprite {num3}, with a plant family {num4}, drawn {_crops.Count}";
		}

		private bool DrawArrow(Projectile p, Vector3 pos, float ground, float dist)
		{
			MeshModel meshModel = Models.Get("arrow");
			if (meshModel == null)
			{
				return false;
			}
			Vector2 startVector = p.getStartVector();
			if (FirstPersonShots.IsHandled(p))
			{
				if (_flights.TryGetValue(p, out var value))
				{
					value.Struck = true;
					value.Seen = _frame;
				}
				return true;
			}
			if (_flights.TryGetValue(p, out var value2) && value2.Arrow && value2.Start != startVector)
			{
				Ended(value2);
				value2 = null;
			}
			if (value2 == null)
			{
				value2 = new Flight
				{
					Mesh = new MeshInstance
					{
						NoShadow = true
					},
					Arrow = true,
					Start = startVector
				};
				_flights[p] = value2;
				value2.Last = new Vec3(pos.x, pos.y, ground + pos.z);
			}
			value2.Seen = _frame;
			if (value2.Struck)
			{
				return true;
			}
			Vec3 vec = new Vec3(pos.x, pos.y, ground + pos.z);
			bool flag = false;
			try
			{
				flag = p.isDeadAnimation();
			}
			catch
			{
			}
			if (!flag)
			{
				Vec3 vec2 = vec - value2.Last;
				if (Vec3.Dot(vec2, vec2) > 1E-06f)
				{
					value2.Dir = vec2.Normalized();
					value2.HaveDir = true;
				}
				else if (!value2.HaveDir)
				{
					Vector3 velocity = p._velocity;
					Vec3 vec3 = new Vec3(velocity.x, velocity.y, velocity.z);
					if (Vec3.Dot(vec3, vec3) > 1E-06f)
					{
						value2.Dir = vec3.Normalized();
						value2.HaveDir = true;
					}
				}
			}
			value2.Last = vec;
			if (!value2.HaveDir)
			{
				value2.Dir = new Vec3(1f, 0f, 0f);
			}
			Vec3 vec4 = value2.Dir;
			Vec3 vec5 = new Vec3(pos.x, pos.y, ground + pos.z);
			if (value2.Drawn && dist < 48f && Strikes(value2.Point, vec5, out var at))
			{
				StuckArrows.Stick(at + vec4 * 0.0968f, vec4, ground: false);
				value2.Struck = true;
				return true;
			}
			if (flag)
			{
				if (vec4.Z > -0.25f)
				{
					vec4 = new Vec3(vec4.X, vec4.Y, -0.35f).Normalized();
				}
				StuckArrows.Stick(new Vec3(pos.x, pos.y, ground) + vec4 * 0.132f, vec4, ground: true);
				value2.Struck = true;
				return true;
			}
			value2.Point = vec5;
			value2.Game = pos;
			value2.Dist = dist;
			value2.Drawn = true;
			MeshInstance mesh = value2.Mesh;
			mesh.Model = meshModel;
			mesh.Palette = null;
			Archer.PlaceArrow(mesh, vec5 - vec4 * 0.5f, vec4, 0.5f);
			mesh.Fade = 1f;
			mesh.SortDepth = dist;
			mesh.Tag = -1;
			mesh.Static = false;
			Meshes.Add(mesh);
			return true;
		}

		internal bool Strikes(Vec3 a, Vec3 b, out Vec3 at)
		{
			at = b;
			float num = 1f;
			bool flag = false;
			TerrainCache terrain = _terrain;
			if (terrain != null && terrain.Width > 0)
			{
				float num2 = b.X - a.X;
				float num3 = b.Y - a.Y;
				int num4 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(num2 * num2 + num3 * num3) / 0.08f), 1, 64);
				for (int i = 1; i <= num4; i++)
				{
					float num5 = (float)i / (float)num4;
					float num6 = a.X + num2 * num5;
					float num7 = a.Y + num3 * num5;
					float num8 = a.Z + (b.Z - a.Z) * num5;
					int num9 = (int)num6;
					int num10 = (int)num7;
					if (num9 < 0 || num10 < 0 || num9 >= terrain.Width || num10 >= terrain.Height)
					{
						break;
					}
					int num11 = num10 * terrain.Width + num9;
					if ((terrain.Flags[num11] & 8) != 0 && !(num8 >= terrain.Heights[num11]))
					{
						num = num5;
						flag = true;
						break;
					}
				}
			}
			for (int j = 0; j < _buildings.Count; j++)
			{
				NearBuilding nearBuilding = _buildings[j];
				MeshInstance mesh = nearBuilding.Mesh;
				if (mesh != null && mesh.Model != null && nearBuilding.Solid && nearBuilding.Look != BuildingLook.Plant)
				{
					float num12 = mesh.Position.X - a.X;
					float num13 = mesh.Position.Y - a.Y;
					float num14 = nearBuilding.Width + nearBuilding.Height + 1f;
					if (!(num12 * num12 + num13 * num13 > num14 * num14) && MeshRay.Segment(mesh, a, a + (b - a) * num, out var hit))
					{
						num *= hit;
						flag = true;
					}
				}
			}
			if (flag)
			{
				at = a + (b - a) * num;
			}
			return flag;
		}

		internal static MeshModel ThrownModel(string id, out float size, out bool tumbles)
		{
			tumbles = true;
			switch (id)
			{
			case "firebomb":
				size = 0.3f;
				return Models.Get("bomb_round");
			case "rock":
				size = 0.22f;
				return Models.Get("rock_plain_f") ?? Models.Get("rock_plain");
			case "bone":
				size = 0.32f;
				return ThrownMeshes.Get("thrown_bone");
			case "snowball":
				size = 0.2f;
				tumbles = false;
				return ThrownMeshes.Get("snowball");
			default:
				size = 0f;
				return null;
			}
		}

		private bool DrawThrown(Projectile p, Vector3 pos, float ground, float dist)
		{
			float size;
			bool tumbles;
			MeshModel meshModel = ThrownModel(p.asset.id, out size, out tumbles);
			if (meshModel == null)
			{
				return false;
			}
			if (!_flights.TryGetValue(p, out var value))
			{
				value = new Flight
				{
					Mesh = new MeshInstance()
				};
				_flights[p] = value;
				value.Last = new Vec3(pos.x, pos.y, ground + pos.z);
			}
			value.Seen = _frame;
			Vec3 vec = new Vec3(pos.x, pos.y, ground + pos.z);
			Vec3 vec2 = vec - value.Last;
			if (Vec3.Dot(vec2, vec2) > 1E-06f)
			{
				value.Dir = vec2.Normalized();
				value.HaveDir = true;
			}
			value.Last = vec;
			Vec3 vec3 = (value.HaveDir ? value.Dir : new Vec3(1f, 0f, 0f));
			float num = Mathf.Max(0.01f, Mathf.Max(meshModel.Max.Z - meshModel.Min.Z, Mathf.Max(meshModel.Max.X - meshModel.Min.X, meshModel.Max.Y - meshModel.Min.Y)));
			float num2 = size / num;
			uint hashCode = (uint)p.GetHashCode();
			float angle = (tumbles ? (_clock * 9f + (float)(hashCode & 0xFF) * 0.1f) : 0f);
			Vec3 vec4 = Vec3.Cross(new Vec3(vec3.X, vec3.Y, 0f), new Vec3(0f, 0f, 1f));
			vec4 = ((Vec3.Dot(vec4, vec4) > 1E-06f) ? vec4.Normalized() : new Vec3(1f, 0f, 0f));
			Mat3 a = Mat3.Rotation(vec4, angle) * Mat3.RotZ(Mathf.Atan2(vec3.Y, vec3.X) + (float)(hashCode & 7) * 0.7f);
			Vec3 v = (meshModel.Min + meshModel.Max) * 0.5f;
			Vec3 vec5 = new Vec3(pos.x, pos.y, ground + pos.z + size * 0.5f);
			MeshInstance mesh = value.Mesh;
			mesh.Model = meshModel;
			mesh.Palette = null;
			mesh.Basis = a * num2;
			mesh.Position = vec5 - mesh.Basis.Mul(v);
			mesh.Fade = 1f;
			mesh.NoShadow = false;
			mesh.Static = false;
			mesh.SortDepth = dist;
			mesh.Tag = -1;
			Meshes.Add(mesh);
			string text = meshModel.Info("fuse");
			if (text != null && dist < 40f)
			{
				string[] array = text.Split(',');
				if (array.Length == 3 && float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) && float.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
				{
					Vec3 vec6 = mesh.Position + mesh.Basis.Mul(new Vec3(result, result2, result3));
					for (int i = 0; i < 3; i++)
					{
						float f = (float)_chipRandom.NextDouble() * 6.28f;
						float num3 = 0.02f + (float)_chipRandom.NextDouble() * 0.05f;
						VoxelObject item = new VoxelObject
						{
							Shape = ChipShape((i == 0) ? EmberHot : TerrainCache.Rgb(255, 240, 180)),
							Tag = -1,
							NoShadow = true,
							Emissive = true
						};
						item.U = new Vec3(1f, 0f, 0f);
						item.V = new Vec3(0f, 0f, 1f);
						item.N = new Vec3(0f, 1f, 0f);
						item.SizeU = (item.SizeV = (item.SizeN = 0.022f));
						item.Origin = vec6 + new Vec3(Mathf.Cos(f) * num3, Mathf.Sin(f) * num3, (float)_chipRandom.NextDouble() * 0.06f);
						item.Flash = 1883295999u;
						item.SortDepth = dist;
						Voxels.Add(item);
					}
					AddLight(vec6.X, vec6.Y, vec6.Z, 2f, 1.1f * Flicker(vec6.X * 5f), 0.6f, 0.2f, 0.05f);
				}
			}
			return true;
		}

		private void ForgetFlights()
		{
			foreach (KeyValuePair<Projectile, Flight> flight in _flights)
			{
				if (flight.Value.Seen == _frame - 1 && !FirstPersonShots.IsHandled(flight.Key))
				{
					Ended(flight.Value);
				}
			}
			StuckArrows.DrawWorld(Meshes, _eyeX, _eyeY, 48f);
			Bundles.DrawWorld(Meshes, _eyeX, _eyeY, 60f);
			Shipyard.DrawWorld(Meshes, _eyeX, _eyeY, 90f);
			if (_frame % 60 != 0 || _flights.Count == 0)
			{
				return;
			}
			_flightsStale.Clear();
			foreach (KeyValuePair<Projectile, Flight> flight2 in _flights)
			{
				if (_frame - flight2.Value.Seen > 30)
				{
					_flightsStale.Add(flight2.Key);
				}
			}
			foreach (Projectile item in _flightsStale)
			{
				_flights.Remove(item);
			}
		}

		private static void Ended(Flight f)
		{
			if (f.Arrow && !f.Struck && f.Drawn)
			{
				f.Struck = true;
				if (f.Dist < 48f)
				{
					StuckArrows.Hit(f.Game, f.Point, f.Dir);
				}
			}
		}

		private static string BombModel(string id)
		{
			switch (id)
			{
			case "bomb":
				return "bomb_aerial";
			case "napalm_bomb":
				return "bomb_napalm";
			case "atomic_bomb":
				return "bomb_atomic";
			case "antimatter_bomb":
				return "bomb_antimatter";
			case "czar_bomba":
				return "bomb_czar";
			case "grenade":
				return "bomb_grenade";
			case "tnt":
				return "bomb_tnt";
			case "tnt_timed":
				return "bomb_tnt_timed";
			case "water_bomb":
				return "bomb_water";
			case "landmine":
				return "bomb_landmine";
			case "santa_bomb":
				return "bomb_gift";
			case "crab_bomb":
				return "bomb_round";
			default:
				return null;
			}
		}

		private bool DrawBomb(Drop d, string id, float x, float y, float z, float ground, float spriteH, float dist)
		{
			string text = BombModel(id);
			if (text == null)
			{
				return false;
			}
			MeshModel meshModel = Models.Get(text);
			if (meshModel == null)
			{
				return false;
			}
			if (!_bombs.TryGetValue(d, out var value))
			{
				value = new Falling3D
				{
					Mesh = new MeshInstance(),
					Spin = (float)_chipRandom.NextDouble() * 6.28f,
					Tumble = (float)_chipRandom.NextDouble() * 6.28f
				};
				_bombs[d] = value;
			}
			value.Seen = _frame;
			float num = Mathf.Min(0.05f, Time.unscaledDeltaTime);
			bool flag = z - ground < 0.05f;
			string text2 = meshModel.Info("kind") ?? "bomb";
			bool flag2 = meshModel.Info("spin") == "1";
			if (!flag)
			{
				value.Spin += num * (flag2 ? 2.2f : 0.8f);
				value.Tumble += num * (flag2 ? 0f : 3.5f);
			}
			float num2 = meshModel.InfoFloat("height", 1f);
			float a = Mathf.Clamp(spriteH * 0.9f, 0.45f, 1.1f) * num2;
			a = Mathf.Max(a, SkyLeast(x, y, 6f));
			float num3 = a / Mathf.Max(0.2f, meshModel.Max.Z - meshModel.Min.Z);
			MeshInstance mesh = value.Mesh;
			mesh.Model = meshModel;
			mesh.Palette = null;
			Mat3 a2 = Mat3.RotZ(value.Spin);
			if (!flag2 && text2 != "mine")
			{
				a2 *= Mat3.RotX(flag ? 1.5708f : (Mathf.Sin(value.Tumble) * 0.9f + value.Tumble * 0.15f));
			}
			float num4;
			if (!(text2 == "mine"))
			{
				if (!flag)
				{
					num4 = 0f;
				}
				else
				{
					switch (text2)
					{
					default:
						num4 = (0f - meshModel.Min.Z) * num3;
						break;
					case "tnt":
					case "canister":
					case "grenade":
						num4 = 0.5f * a * 0.35f;
						break;
					}
				}
			}
			else
			{
				num4 = (0f - meshModel.Min.Z) * num3;
			}
			float num5 = num4;
			mesh.Position = new Vec3(x, y, (flag ? ground : z) + num5);
			mesh.Basis = a2 * num3;
			mesh.NoShadow = false;
			mesh.Static = false;
			mesh.SortDepth = dist;
			mesh.Tag = -1;
			Meshes.Add(mesh);
			if (IsNukeDrop(id))
			{
				NukeDropFx(d, id, x, y, flag ? ground : z, ground);
			}
			else if (!flag)
			{
				SkyFallCue(x, y, z, ground, a);
			}
			string text3 = meshModel.Info("fuse");
			if (text3 != null)
			{
				string[] array = text3.Split(',');
				if (array.Length == 3 && float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) && float.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
				{
					Vec3 vec = mesh.Position + mesh.Basis.Mul(new Vec3(result, result2, result3));
					for (int i = 0; i < 3; i++)
					{
						float f = (float)_chipRandom.NextDouble() * 6.28f;
						float num6 = 0.02f + (float)_chipRandom.NextDouble() * 0.06f;
						VoxelObject item = new VoxelObject
						{
							Shape = ChipShape((i == 0) ? EmberHot : TerrainCache.Rgb(255, 240, 180)),
							Tag = -1,
							NoShadow = true,
							Emissive = true
						};
						item.U = new Vec3(1f, 0f, 0f);
						item.V = new Vec3(0f, 0f, 1f);
						item.N = new Vec3(0f, 1f, 0f);
						item.SizeU = (item.SizeV = (item.SizeN = 0.025f));
						item.Origin = vec + new Vec3(Mathf.Cos(f) * num6, Mathf.Sin(f) * num6, (float)_chipRandom.NextDouble() * 0.08f);
						item.Flash = 1883295999u;
						item.SortDepth = dist;
						Voxels.Add(item);
					}
					if (dist < 40f)
					{
						AddLight(vec.X, vec.Y, vec.Z, 2f, 1.1f * Flicker(vec.X * 5f), 0.6f, 0.2f, 0.05f);
					}
				}
			}
			if (text == "bomb_antimatter" && dist < 50f)
			{
				AddLight(x, y, mesh.Position.Z, 4f, 0.6f, 0.45f, 1.2f, 0.1f);
			}
			return true;
		}

		private void ForgetBombs()
		{
			if (_frame % 60 != 0 || _bombs.Count == 0)
			{
				return;
			}
			_bombsStale.Clear();
			foreach (KeyValuePair<Drop, Falling3D> bomb in _bombs)
			{
				if (_frame - bomb.Value.Seen > 30)
				{
					_bombsStale.Add(bomb.Key);
				}
			}
			foreach (Drop item in _bombsStale)
			{
				_bombs.Remove(item);
			}
		}

		private string RockLook(NearBuilding nb, Building b, string family)
		{
			if (family == null || !family.StartsWith("rock_", StringComparison.Ordinal))
			{
				return family;
			}
			WorldTile current_tile = b.current_tile;
			if (current_tile == null)
			{
				return family;
			}
			object obj = current_tile.top_type ?? current_tile.Type;
			if (_rockLands.TryGetValue(nb, out var value) && value.Top == obj && value.Family == family)
			{
				return value.Look;
			}
			string biome = BiomeOf(current_tile);
			bool high = false;
			bool flag = false;
			try
			{
				string text = ((current_tile.Type != null) ? (current_tile.Type.id ?? "") : "");
				high = text.Contains("hills") || text.Contains("mountain") || text.Contains("summit");
				for (int i = -2; i <= 2; i++)
				{
					if (flag)
					{
						break;
					}
					for (int j = -2; j <= 2; j++)
					{
						if (flag)
						{
							break;
						}
						WorldTile tile = World.world.GetTile(current_tile.x + j, current_tile.y + i);
						flag = tile != null && tile.Type != null && tile.Type.liquid && !tile.Type.lava;
					}
				}
			}
			catch
			{
			}
			char style = RockForms.StyleFor(biome, flag, high);
			string text2 = RockForms.Pick(family, style, current_tile.x, current_tile.y, (string n) => Models.Get(n) != null);
			if (_rockLands.Count > 4096)
			{
				_rockLands.Clear();
			}
			_rockLands[nb] = new RockLand
			{
				Top = obj,
				Family = family,
				Look = text2,
				Biome = biome
			};
			return text2;
		}

		private static string BiomeOf(WorldTile t)
		{
			try
			{
				if (t.top_type != null && t.top_type.is_biome && t.top_type.biome_asset != null)
				{
					return t.top_type.biome_asset.id;
				}
				return (t.Type != null) ? t.Type.id : null;
			}
			catch
			{
				return null;
			}
		}

		private uint[] Boulders(NearBuilding nb, Building b, MeshModel model, uint[] pal)
		{
			if (pal == null || (model.PartIndex("mine") < 0 && model.PartIndex("ruin") < 0))
			{
				return pal;
			}
			WorldTile current_tile = b.current_tile;
			if (current_tile == null)
			{
				return pal;
			}
			object obj = current_tile.top_type ?? current_tile.Type;
			if (_boulderLooks.TryGetValue(nb, out var value) && value.From == pal && value.Top == obj)
			{
				return value.Colours ?? pal;
			}
			uint[] array = NaturePalette.Boulders(model, pal, BiomeOf(current_tile));
			if (_boulderLooks.Count > 256)
			{
				_boulderLooks.Clear();
			}
			_boulderLooks[nb] = new BoulderLook
			{
				From = pal,
				Colours = array,
				Top = obj
			};
			return array ?? pal;
		}

		private string RockBiome(NearBuilding nb)
		{
			if (!_rockLands.TryGetValue(nb, out var value))
			{
				return null;
			}
			return value.Biome;
		}

		private float RockScale(Building b, string look, MeshModel model, float visible)
		{
			MeshModel model2 = Models.Get(look) ?? model;
			WorldTile current_tile = b.current_tile;
			return RockForms.Scale(model2, visible, NaturePalette.LumpHeight(look), look, current_tile?.x ?? 0, current_tile?.y ?? 0);
		}

		private static void BushFruit(Building b, MeshInstance inst, MeshModel model)
		{
			int num = model.PartIndex("berries");
			if (num < 0 || num >= 64)
			{
				inst.Hidden = 0uL;
				return;
			}
			bool flag = true;
			try
			{
				flag = b.hasResourcesToCollect();
			}
			catch
			{
			}
			inst.Hidden = (ulong)(flag ? 0 : (1L << num));
		}

		private void RockStage(Building b, MeshInstance inst, MeshModel model, float ground)
		{
			float num = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Work : null)?.ProgressOf(b) ?? 0f;
			int num2 = ((num >= 0.66f) ? 2 : ((num >= 0.33f) ? 1 : 0));
			int num3 = model.PartIndex("cracks_1");
			int num4 = model.PartIndex("cracks_2");
			if (num3 >= 0 || num4 >= 0)
			{
				ulong num5 = 0uL;
				if (num3 >= 0 && num3 < 64 && num2 < 1)
				{
					num5 |= (ulong)(1L << num3);
				}
				if (num4 >= 0 && num4 < 64 && num2 < 2)
				{
					num5 |= (ulong)(1L << num4);
				}
				inst.Hidden = num5;
				return;
			}
			_rockStage.TryGetValue(b, out var value);
			if (num2 < value)
			{
				value = num2;
			}
			int num6 = model.PartIndex("chunk_a");
			int num7 = model.PartIndex("chunk_b");
			ulong num8 = 0uL;
			if (num2 >= 1 && num6 >= 0 && num6 < 64)
			{
				num8 |= (ulong)(1L << num6);
			}
			if (num2 >= 2 && num7 >= 0 && num7 < 64)
			{
				num8 |= (ulong)(1L << num7);
			}
			inst.Hidden = num8;
			while (value < num2)
			{
				value++;
				BreakOff(inst, model, (value == 1) ? num6 : num7, ground, crumble: false);
			}
			if (num2 == 0)
			{
				_rockStage.Remove(b);
				return;
			}
			if (_rockStage.Count > 32)
			{
				_rockStage.Clear();
			}
			_rockStage[b] = value;
		}

		private void BreakOff(MeshInstance inst, MeshModel model, int part, float ground, bool crumble, float throwSpeed = 1f)
		{
			if (part < 0 || part >= model.Parts.Length || part >= 64)
			{
				return;
			}
			ulong hidden = (ulong)(~(1L << part));
			MeshInstance mesh = new MeshInstance
			{
				Model = model,
				Palette = inst.Palette,
				Basis = inst.Basis,
				Position = inst.Position,
				Hidden = hidden,
				Snow = inst.Snow
			};
			MeshPart meshPart = model.Parts[part];
			Vec3 pivot = meshPart.Pivot;
			Vec3 vec = inst.Position + inst.Basis.Mul(pivot);
			Vec3 vec2 = vec - inst.Position;
			float num = Mathf.Sqrt(vec2.X * vec2.X + vec2.Y * vec2.Y);
			Vec3 vec3 = ((num > 0.001f) ? new Vec3(vec2.X / num, vec2.Y / num, 0f) : new Vec3(1f, 0f, 0f));
			float z = inst.Basis.C2.Z;
			float num2 = pivot.Z;
			if (meshPart.Pos != null)
			{
				for (int i = 2; i < meshPart.VertexCount * 3; i += 3)
				{
					num2 = Mathf.Min(num2, meshPart.Pos[i]);
				}
			}
			float num3 = (float)_chipRandom.NextDouble();
			Debris item = new Debris
			{
				Mesh = mesh,
				Basis = inst.Basis,
				Pivot = pivot,
				At = vec,
				Ground = ground,
				Rest = Mathf.Max(0.15f, (pivot.Z - num2) * 0.6f),
				Vel = (crumble ? new Vec3(0f, 0f, 0f) : ((vec3 * (0.9f + 0.5f * num3) + new Vec3(0f, 0f, 1.4f)) * (Mathf.Max(0.5f, z) * throwSpeed))),
				Axis = new Vec3(0f - vec3.Y, vec3.X, 0f),
				Spin = (crumble ? 0f : ((3.5f + 2f * num3) * Mathf.Min(1f, throwSpeed + 0.3f))),
				Life = (crumble ? 0.6f : 2.4f),
				Still = crumble
			};
			_debris.Add(item);
			if (_debris.Count > 24)
			{
				_debris.RemoveAt(0);
			}
		}

		public void MinedOut(Building b, Vector2 from)
		{
			NearBuilding nearBuilding = Find(b);
			if (nearBuilding == null || nearBuilding.Mesh == null || nearBuilding.Mesh.Model == null || _terrain == null)
			{
				return;
			}
			MeshInstance mesh = nearBuilding.Mesh;
			Vector2 vector = Position(b);
			float num = _terrain.SampleHeight(vector.x, vector.y);
			MeshModel model = mesh.Model;
			bool flag = false;
			for (int i = 0; i < model.Parts.Length && i < 64; i++)
			{
				if (model.Parts[i].Name != null && model.Parts[i].Name.StartsWith("frag_", StringComparison.Ordinal))
				{
					BreakOff(mesh, model, i, num, crumble: false, 0.55f + 0.35f * (float)_chipRandom.NextDouble());
					flag = true;
				}
			}
			BreakOff(mesh, model, model.PartIndex("rock"), num, crumble: true);
			if (flag)
			{
				mesh.Hidden = ulong.MaxValue;
			}
			MeshModel meshModel = Models.Get("rock_rubble");
			if (meshModel != null)
			{
				float z = mesh.Basis.C2.Z;
				MeshInstance meshInstance = new MeshInstance
				{
					Model = meshModel,
					Snow = mesh.Snow
				};
				if (nearBuilding.Image != null)
				{
					meshInstance.Palette = NaturePalette.Rock(meshModel, nearBuilding.Image, _terrain.Colors[Mathf.Clamp((int)vector.y, 0, _terrain.Height - 1) * _terrain.Width + Mathf.Clamp((int)vector.x, 0, _terrain.Width - 1)], RockBiome(nearBuilding));
				}
				meshInstance.Place(vector.x, vector.y, num - 0.02f, nearBuilding.Angle, z * 1.1f, z * 1.1f, z * 1.1f);
				_debris.Add(new Debris
				{
					Mesh = meshInstance,
					Basis = meshInstance.Basis,
					At = meshInstance.Position,
					Ground = num,
					Life = 7f,
					Still = true,
					Rubble = true
				});
			}
			Chips(b, from, 0.35f, 12);
		}

		private void StepDebris(ViewState view, float dt)
		{
			for (int num = _debris.Count - 1; num >= 0; num--)
			{
				Debris debris = _debris[num];
				debris.Age += dt;
				if (debris.Age >= debris.Life)
				{
					_debris.RemoveAt(num);
				}
				else
				{
					MeshInstance mesh = debris.Mesh;
					if (debris.Rubble)
					{
						float num2 = Mathf.Max(0f, debris.Age - (debris.Life - 2f)) * 0.25f * mesh.Basis.C2.Z;
						mesh.Position = new Vec3(debris.At.X, debris.At.Y, debris.At.Z - num2);
					}
					else if (debris.Still)
					{
						mesh.DissolveScale = 0.35f;
						mesh.Fade = 1f - debris.Age / debris.Life;
					}
					else
					{
						if (dt > 0f)
						{
							debris.Vel = new Vec3(debris.Vel.X, debris.Vel.Y, debris.Vel.Z - 9f * dt);
							debris.At += debris.Vel * dt;
							float num3 = debris.Ground + debris.Rest * debris.Basis.C2.Z;
							if (debris.At.Z < num3)
							{
								debris.At = new Vec3(debris.At.X, debris.At.Y, num3);
								debris.Vel = new Vec3(debris.Vel.X * 0.45f, debris.Vel.Y * 0.45f, Mathf.Abs(debris.Vel.Z) * 0.3f);
								debris.Spin *= 0.5f;
							}
							debris.Turned += debris.Spin * dt;
						}
						mesh.Basis = Mat3.Rotation(debris.Axis, debris.Turned) * debris.Basis;
						mesh.Position = debris.At - mesh.Basis.Mul(debris.Pivot);
						if (debris.Age > debris.Life - 0.8f)
						{
							mesh.DissolveScale = 0.3f;
							mesh.Fade = (debris.Life - debris.Age) / 0.8f;
						}
					}
					float num4 = mesh.Position.X - view.X;
					float num5 = mesh.Position.Y - view.Y;
					mesh.SortDepth = Mathf.Sqrt(num4 * num4 + num5 * num5);
					Meshes.Add(mesh);
				}
			}
		}

		private static MeshModel RuinModel(NearBuilding nb)
		{
			object obj;
			if (nb.MeshFamily != null)
			{
				if (!BuildingInfo)
				{
					obj = Models.Get("ruin_" + nb.MeshFamily);
					if (obj == null)
					{
						return Models.Get("ruin_" + TownModels.Base(nb.MeshFamily));
					}
				}
				else
				{
					obj = Models.Get(RuinName(ref nb.Memo.RuinOf, ref nb.Memo.Ruin, nb.MeshFamily));
					if (obj == null)
					{
						return Models.Get(RuinName(ref nb.Memo.RuinBaseOf, ref nb.Memo.RuinBase, TownModels.Base(nb.MeshFamily)));
					}
				}
			}
			else
			{
				obj = null;
			}
			return (MeshModel)obj;
		}

		private static float StandingWidth(Building b, SpriteCache sprites, float fallback)
		{
			try
			{
				BuildingAnimationData buildingAnimationData = b.animData;
				if ((buildingAnimationData == null || buildingAnimationData.main == null || buildingAnimationData.main.Length == 0) && b.asset.building_sprites != null && b.asset.building_sprites.animation_data != null && b.asset.building_sprites.animation_data.Count > 0)
				{
					buildingAnimationData = b.asset.building_sprites.animation_data[0];
				}
				if (buildingAnimationData == null || buildingAnimationData.main == null || buildingAnimationData.main.Length == 0 || buildingAnimationData.main[0] == null)
				{
					return fallback;
				}
				SpriteImage spriteImage = sprites.Get(buildingAnimationData.main[0]);
				return (spriteImage != null) ? (spriteImage.UnitWidth * Mathf.Abs(b.asset.scale_base.x)) : fallback;
			}
			catch
			{
				return fallback;
			}
		}

		private static uint RoofColour(NearBuilding nb, Building b)
		{
			Kingdom kingdom = b.kingdom;
			bool flag;
			try
			{
				flag = kingdom?.isCiv() ?? false;
			}
			catch
			{
				flag = false;
			}
			if (flag)
			{
				Color color = Royal.ColorOf(kingdom);
				nb.KingdomColor = TerrainCache.Rgb((int)(color.r * 255f), (int)(color.g * 255f), (int)(color.b * 255f));
				return nb.KingdomColor;
			}
			if (nb.KingdomColor != 0)
			{
				return nb.KingdomColor;
			}
			WorldTile current_tile = b.current_tile;
			return OldRoofs[(current_tile != null) ? ((int)(TerrainModel.Hash(current_tile.x * 3, current_tile.y * 5) >> 7) % OldRoofs.Length) : 0];
		}

		private void Smoulder(NearBuilding nb, Building b, float cx, float cy, float ground, float dist2)
		{
			if (nb.RuinedAt < 0f)
			{
				return;
			}
			float num = _clock - nb.RuinedAt;
			if (num > 80f || num < 0f)
			{
				nb.RuinedAt = -1f;
			}
			else if (!(dist2 > 2304f))
			{
				float num2 = 1f - num / 80f;
				int puffs = Mathf.Clamp(Mathf.RoundToInt(((dist2 < 900f) ? 5f : 3f) * num2 + 0.5f), 1, 5);
				FireField.AddPlume(Billboards, cx, cy, ground + 0.4f, Mathf.Clamp(nb.Width * 0.3f, 0.4f, 2f), puffs, (uint)b.GetHashCode(), _clock, 0.6f + 0.4f * num2);
				float num3 = Mathf.Clamp01(1f - num / 36f);
				if (num3 > 0f && _night > 0.2f && dist2 < 2304f)
				{
					float num4 = num3 * Flicker(cx * 1.3f + cy) * _night * 0.55f;
					AddLight(cx, cy, ground + 0.35f, Mathf.Clamp(nb.Width * 0.6f, 2.5f, 5f), 1f * num4, 0.42f * num4, 0.12f * num4, 0.12f);
				}
			}
		}

		internal static void OwnShot(Vec3 muzzle, Vec3 dir, float scale)
		{
			_shotgunSeed = _shotgunSeed * 1664525 + 1013904223;
			ShotgunFlashes.Add(muzzle, dir, scale, own: true, (int)(_shotgunSeed % 3));
			ShotgunFx.MuzzleSmoke(BlastParticles, muzzle, dir, scale, _shotgunSeed);
		}

		internal static void EjectShell(Vec3 port, Vec3 velocity, Vec3 axis, float scale, Vec3 portDir)
		{
			_shotgunSeed = _shotgunSeed * 1664525 + 1013904223;
			Casings.Eject(port, velocity, axis, scale, _shotgunSeed);
			ShellsEjected++;
			ShotgunFx.Wisp(BlastParticles, port, portDir, scale, _shotgunSeed, 110);
		}

		internal static void PelletImpact(Vec3 at, Vec3 back, ShotgunFx.Surface surface, uint color)
		{
			_shotgunSeed = _shotgunSeed * 1664525 + 1013904223;
			ShotgunFx.Impact(BlastParticles, at, back, surface, color, _shotgunSeed);
		}

		private bool DrawPellet(Projectile p, Vector3 pos, float baseZ, float dist)
		{
			if (FirstPersonShots.Steered(p) || FirstPersonShots.IsHandled(p))
			{
				return true;
			}
			if (p._state != ProjectileState.Active)
			{
				return true;
			}
			if (PossessionHooks.Active && p.by_who != null && p.by_who == ControllableUnit._unit_main)
			{
				return true;
			}
			Vec3 vec = new Vec3(pos.x, pos.y, baseZ + pos.z);
			if (!_pellets.TryGetValue(p, out var value))
			{
				if (_pellets.Count > 400)
				{
					_pellets.Clear();
				}
				value = new PelletSeen
				{
					Last = vec,
					Start = vec
				};
				_pellets[p] = value;
				BaseSimObject by_who = p.by_who;
				int value2;
				bool flag = by_who != null && dist < 80f && (!_volleyFrame.TryGetValue(by_who, out value2) || _frame - value2 > 10);
				if (Trace && _traced++ < 40)
				{
					Log.Info(string.Format("SGW pellet by {0} at {1:F1} tiles, state {2}, frame {3}, last volley frame {4}, fresh {5}", (by_who == null) ? "nobody" : ((by_who is Actor actor) ? (actor.asset?.id + "#" + actor.GetHashCode()) : by_who.GetType().Name), dist, p._state, _frame, (by_who != null && _volleyFrame.TryGetValue(by_who, out var value3)) ? value3 : (-1), flag));
				}
				if (flag)
				{
					OthersVolley(by_who, vec);
				}
				if (by_who != null)
				{
					_volleyFrame[by_who] = _frame;
				}
			}
			value.Seen = _frame;
			_pelletsDrawn++;
			OthersPelletsTotal++;
			Vec3 vec2 = value.Last;
			Vec3 vec3 = vec - vec2;
			float num = Mathf.Sqrt(Vec3.Dot(vec3, vec3));
			if (num > 3f)
			{
				vec2 = vec - vec3 * (3f / num);
			}
			if (num > 0.001f)
			{
				Streaks.Add(new Streak
				{
					A = vec2,
					B = vec,
					Color = ShotgunFx.PelletHead,
					AlphaA = 0.15f,
					AlphaB = 0.95f,
					Width = 0.016f,
					Glow = 1f,
					Edge = 0.15f
				});
			}
			value.Last = vec;
			return true;
		}

		private void OthersVolley(BaseSimObject by, Vec3 firstPellet)
		{
			OthersVolleys++;
			float scale = 1f;
			if (!(by is Actor a) || !People.GunMuzzle(a, out var muzzle, out var dir, out scale))
			{
				Vector2 current_position = by.current_position;
				float num = ((_blastSink.Ground != null) ? _blastSink.Ground(current_position.x, current_position.y) : 0f);
				muzzle = new Vec3(current_position.x, current_position.y, num + 0.6f);
				dir = (firstPellet - muzzle).Normalized();
				muzzle += dir * 0.4f;
			}
			_shotgunSeed = _shotgunSeed * 1664525 + 1013904223;
			ShotgunFlashes.Add(muzzle, dir, scale, own: false, (int)(_shotgunSeed % 3));
			ShotgunFx.MuzzleSmoke(BlastParticles, muzzle, dir, scale, _shotgunSeed, 0.8f);
			if (by is Actor a2)
			{
				People.ShotgunFired(a2);
			}
		}

		private void PelletsFromLiveSet(MapBox world, TerrainCache terrain)
		{
			HashSet<Projectile> hashSet = null;
			try
			{
				hashSet = ((world.projectiles != null) ? ((CoreSystemManager<Projectile, ProjectileData>)world.projectiles)._hashset : null);
			}
			catch
			{
			}
			if (hashSet == null)
			{
				return;
			}
			foreach (Projectile item in hashSet)
			{
				if (item != null && item.asset != null && !(item.asset.id != "shotgun_bullet") && item._state == ProjectileState.Active && (!_pellets.TryGetValue(item, out var value) || value.Seen != _frame))
				{
					Vector3 current_position_3d = item._current_position_3d;
					float num = current_position_3d.x - _eyeX;
					float num2 = current_position_3d.y - _eyeY;
					float num3 = Mathf.Sqrt(num * num + num2 * num2);
					if (!(num3 > 90f))
					{
						DrawPellet(item, current_position_3d, FirstPersonShots.BaseZ(item, terrain.SampleHeight(current_position_3d.x, current_position_3d.y)), num3);
					}
				}
			}
		}

		private void StepShotgun(MapBox world, TerrainCache terrain, float dt)
		{
			PelletsFromLiveSet(world, terrain);
			FxSink blastSink = _blastSink;
			OthersPellets = _pelletsDrawn;
			_pelletsDrawn = 0;
			ShotgunFlashes.Step(dt);
			ShotgunFlashes.Draw(blastSink);
			Casings.Step(dt, terrain.SampleHeight);
			if (Casings.Landed > 0)
			{
				ShellsLanded += Casings.Landed;
				ShellLandedAt = Casings.LandedAt;
			}
			Casings.Draw(Meshes, Models.Get("shotgun_shell"));
			if (_pellets.Count == 0)
			{
				return;
			}
			_pelletsGone.Clear();
			foreach (KeyValuePair<Projectile, PelletSeen> pellet in _pellets)
			{
				if (pellet.Value.Seen != _frame)
				{
					_pelletsGone.Add(pellet.Key);
				}
			}
			foreach (Projectile item in _pelletsGone)
			{
				PelletSeen pelletSeen = _pellets[item];
				_pellets.Remove(item);
				Vec3 last = pelletSeen.Last;
				float num = terrain.SampleHeight(last.X, last.Y);
				if (!(last.Z - num > 0.45f) && IsNear(last, 60f))
				{
					Vec3 back = (pelletSeen.Start - last).Normalized();
					bool flag = terrain.IsWaterAt(last.X, last.Y);
					_shotgunSeed = _shotgunSeed * 1664525 + 1013904223;
					ShotgunFx.Impact(BlastParticles, new Vec3(last.X, last.Y, terrain.TopAt(last.X, last.Y)), back, flag ? ShotgunFx.Surface.Water : ShotgunFx.Surface.Ground, LandColor(terrain, last.X, last.Y), _shotgunSeed);
				}
			}
			if (_volleyFrame.Count > 64)
			{
				_volleyFrame.Clear();
			}
		}

		private bool IsNear(Vec3 at, float range)
		{
			return (at.X - _eyeX) * (at.X - _eyeX) + (at.Y - _eyeY) * (at.Y - _eyeY) < range * range;
		}

		private static void ClearShotgun()
		{
			ShotgunFlashes.Clear();
			Casings.Clear();
		}

		private void BeginSimLod(ViewState view, TerrainCache terrain)
		{
			SimView = this;
			_simTerrain = terrain;
			if (FromSky)
			{
				SimFocusX = SkyFocus.x;
				SimFocusY = SkyFocus.y;
			}
			else
			{
				SimFocusX = view.X;
				SimFocusY = view.Y;
			}
			_simFocal = (float)Screen.width * 0.5f / Mathf.Max(0.05f, Mathf.Tan(view.Fov * 0.5f));
		}

		internal bool SimSees(float x, float y, float tall, out float px)
		{
			px = 0f;
			try
			{
				if (FromSky && GodFrameReady())
				{
					ViewState godFrameCamera = GodFrameCamera;
					float num = ((_simTerrain != null) ? _simTerrain.SampleHeight(x, y) : 0f);
					if (OutsideGodFrame(x, y, num, tall, 16f))
					{
						return false;
					}
					float num2 = x - godFrameCamera.X;
					float num3 = y - godFrameCamera.Y;
					float num4 = num - godFrameCamera.Z;
					px = _gfFocal * tall / Mathf.Max(1f, Mathf.Sqrt(num2 * num2 + num3 * num3 + num4 * num4));
					return true;
				}
				float num5 = x - _eyeX;
				float num6 = y - _eyeY;
				if (!InView(num5, num6, tall + 2f))
				{
					return false;
				}
				px = _simFocal * tall / Mathf.Max(1f, num5 * _vfx + num6 * _vfy);
				return true;
			}
			catch
			{
				return true;
			}
		}

		private bool DrawSite(NearBuilding nb, Building b, Vector2 p, float dist2, int tag)
		{
			if (nb.MeshFamily == null)
			{
				return false;
			}
			MeshModel meshModel = Models.Get(nb.MeshFamily);
			MeshModel meshModel2 = ((meshModel == null) ? null : (BuildingInfo ? (Models.Get(SiteName(ref nb.Memo.SiteOf, ref nb.Memo.Site, nb.MeshFamily)) ?? Models.Get(SiteName(ref nb.Memo.SiteBaseOf, ref nb.Memo.SiteBase, TownModels.Base(nb.MeshFamily)))) : (Models.Get("site_" + nb.MeshFamily) ?? Models.Get("site_" + TownModels.Base(nb.MeshFamily)))));
			if (meshModel2 == null)
			{
				return false;
			}
			int stage = BuildSite.StageOf(b.getConstructionProgress(), b.asset.construction_progress_needed);
			float num = ((nb.StandingWidth > 0f) ? nb.StandingWidth : nb.Width);
			float x = p.x;
			float num2 = p.y + nb.CenterOffset;
			float num3 = Mathf.Sqrt(dist2);
			bool flag = _farBuild || InView(x - _eyeX, num2 - _eyeY, Mathf.Max(nb.Width, nb.Height) * 1.2f);
			if (!flag && num3 > 41f)
			{
				return true;
			}
			bool far = !flag || num3 / Models.LodZoom >= 30f;
			MeshModel meshModel3 = BuildSite.At(meshModel2, stage, far);
			MeshInstance meshInstance = nb.Mesh;
			if (meshInstance == null)
			{
				MeshInstance obj = new MeshInstance
				{
					Static = true
				};
				MeshInstance meshInstance2 = obj;
				nb.Mesh = obj;
				meshInstance = meshInstance2;
			}
			MeshInstance meshInstance3 = meshInstance;
			if (meshInstance3.PartRotation != null && meshInstance3.PartRotation.Length != meshModel3.Parts.Length)
			{
				meshInstance3.PartRotation = null;
				meshInstance3.PartOffset = null;
				meshInstance3.PartScale = null;
			}
			meshInstance3.Hidden = 0uL;
			meshInstance3.Model = meshModel3;
			meshInstance3.ShadowOnly = !flag;
			meshInstance3.Palette = Models.BuildingColors(meshModel3, nb.MeshFamily, RoofColour(nb, b));
			float num4 = KeptScale(nb, b, ruin: false, num / Mathf.Max(0.5f, meshModel.InfoFloat("width", meshModel.Max.X - meshModel.Min.X)));
			float num5 = num * 0.4f;
			float num6 = nb.DepthTiles * 0.4f;
			float num7 = _terrain.SampleHeight(x, num2);
			float num8 = Mathf.Min(num7, Mathf.Min(Mathf.Min(_terrain.SampleHeight(x - num5, num2 - num6), _terrain.SampleHeight(x + num5, num2 - num6)), Mathf.Min(_terrain.SampleHeight(x - num5, num2 + num6), _terrain.SampleHeight(x + num5, num2 + num6))));
			float num9 = meshModel.InfoFloat("footing", 0f);
			int turns = Facing.Of(b);
			if (num9 > 0f)
			{
				Vector2 vector = new Vector2(x, num2) + Facing.Turn(new Vector2(0f, 0f - num6), turns);
				num8 = ((meshModel.Info("kind") == "tent") ? BuildingGround.Pitch(num8, num7) : BuildingGround.Base(num8, _terrain.SampleHeight(vector.x, vector.y), num7, num9, num4));
			}
			float heading = Facing.Heading(turns);
			bool flag2 = meshModel.Info("kind") == "docks";
			if (flag2)
			{
				if (float.IsNaN(nb.DockHeading))
				{
					nb.DockHeading = DockFacing.Heading(b, x, num2);
				}
				heading = nb.DockHeading;
				num8 = DockFacing.Ground(_terrain, x, num2);
			}
			bool flag3 = num9 > 0f && Models.Mirrored(b);
			meshInstance3.Place(x, num2, num8 - 0.05f, heading, flag3 ? (0f - num4) : num4, num4, num4);
			if (_wantSolid)
			{
				SolidModel(nb, BuildSite.At(meshModel2, stage), x, num2, heading, num4, num8 - 0.05f, flag3, flag2 ? 0.8f : 0f);
			}
			meshInstance3.Tag = tag;
			meshInstance3.SortDepth = num3;
			meshInstance3.Glow = 0f;
			meshInstance3.Multiply = 0u;
			meshInstance3.Static = Steady(nb, p);
			BuildingEffects(nb, b, meshInstance3, meshModel3, lit: false, ruin: false, flag && !_farBuild, dist2);
			AddModel(meshInstance3);
			return true;
		}

		private float SkyRange(float firstPerson)
		{
			return Mathf.Max(firstPerson, SkyReach);
		}

		private void SkyFallCue(float x, float y, float z, float ground, float size)
		{
			if (FromSky && !(z - ground < 0.3f))
			{
				float num = SkyLeast(x, y, 1f);
				for (int i = 1; i <= 4; i++)
				{
					_blastSink.Glow(x, y, z + size * 0.5f + (float)i * Mathf.Max(size * 0.45f, num * 2.5f), Mathf.Max(size * 0.7f, num * 3f), TerrainCache.Rgb(255, 244, 214), 0.42f - (float)i * 0.08f);
				}
				float num2 = 1f - Mathf.Clamp01((z - ground) / Mathf.Max(4f, Weather.Base - ground));
				Billboards.Add(new Billboard
				{
					X = x,
					Y = y,
					Z = ground,
					GroundZ = ground,
					Width = Mathf.Max(size, num * 4f) * (1.6f + 0.8f * num2),
					Height = 0.01f,
					Image = NoSprite,
					Kind = BillboardKind.Facing,
					Shadow = true,
					Tag = -1,
					Far = true
				});
			}
		}

		private float SkyLeast(float x, float y, float pixels)
		{
			if (!FromSky)
			{
				return 0f;
			}
			float b = (x - _eyeX) * _vfx + (y - _eyeY) * _vfy;
			return pixels * SkyPixel * Mathf.Max(1f, b);
		}

		private void StepSnow(TerrainCache land, float dt)
		{
			_snowCover = land.Snow;
			try
			{
				land.Snow.Step(land, Weather, WorldStep(dt), Time.unscaledTime, _night, SeasonLook.Winter);
			}
			catch (Exception ex)
			{
				if (_reportedAssets.Add("snow"))
				{
					Log.Error("snow skipped: " + ex.Message);
				}
			}
		}

		private MeshInstance Snowed(MeshInstance inst)
		{
			SnowCover snowCover = _snowCover;
			inst.Snow = ((snowCover != null && snowCover.On) ? snowCover.At(inst.Position.X, inst.Position.Y) : 0f);
			return inst;
		}

		private static BuildingFx FxOf(MeshModel m)
		{
			if (FxCache.TryGetValue(m, out var value))
			{
				return value;
			}
			value = new BuildingFx();
			List<int> list = new List<int>();
			List<int> list2 = new List<int>();
			List<int> list3 = new List<int>();
			List<int> list4 = new List<int>();
			for (int i = 0; i < m.Parts.Length; i++)
			{
				string text = m.Parts[i].Name ?? "";
				if (text.StartsWith("flames", StringComparison.Ordinal))
				{
					list.Add(i);
				}
				else if (text.StartsWith("float", StringComparison.Ordinal))
				{
					list2.Add(i);
				}
				else if (text.StartsWith("wheel", StringComparison.Ordinal) && m.Info("kind") != "boat")
				{
					list3.Add(i);
				}
				else if (text.StartsWith("spin", StringComparison.Ordinal))
				{
					list4.Add(i);
				}
			}
			value.Flames = list.ToArray();
			value.Floats = list2.ToArray();
			value.Wheels = list3.ToArray();
			value.Spins = list4.ToArray();
			value.Ghostly = m.InfoFloat("ghostly", 0f);
			value.Hover = m.InfoFloat("hover", 0f);
			value.Smoke = Points(m.Info("smoke"));
			List<Vec3> list5 = new List<Vec3>();
			List<uint> list6 = new List<uint>();
			List<float> list7 = new List<float>();
			string text2 = m.Info("light");
			if (!string.IsNullOrEmpty(text2))
			{
				string[] array = text2.Split('/');
				for (int j = 0; j < array.Length; j++)
				{
					string[] array2 = array[j].Split(',');
					if (array2.Length >= 5)
					{
						list5.Add(new Vec3(F(array2[0]), F(array2[1]), F(array2[2])));
						list6.Add(Hex(array2[3]));
						list7.Add(F(array2[4]));
					}
				}
			}
			int[] flames = value.Flames;
			foreach (int num in flames)
			{
				list5.Add(m.Parts[num].Pivot + new Vec3(0f, 0f, 0.3f));
				list6.Add(TerrainCache.Rgb(255, 150, 60));
				list7.Add(4f);
			}
			value.LightAt = list5.ToArray();
			value.LightColor = list6.ToArray();
			value.LightRadius = list7.ToArray();
			string text3 = m.Info("mist");
			value.Mist = ((!string.IsNullOrEmpty(text3)) ? Hex(text3) : 0u);
			value.Animated = value.Flames.Length + value.Floats.Length + value.Wheels.Length + value.Spins.Length > 0 || value.Ghostly > 0f || value.Hover > 0f;
			value.Any = value.Animated || value.Smoke.Length != 0 || value.LightAt.Length != 0 || value.Mist != 0;
			if (FxCache.Count > 600)
			{
				FxCache.Clear();
			}
			FxCache[m] = value;
			return value;
		}

		private static float F(string s)
		{
			if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return 0f;
			}
			return result;
		}

		private static uint Hex(string s)
		{
			s = (s ?? "").TrimStart('#');
			if (s.Length < 6 || !uint.TryParse(s.Substring(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
			{
				return TerrainCache.Rgb(255, 255, 255);
			}
			return TerrainCache.Rgb((int)((result >> 16) & 0xFF), (int)((result >> 8) & 0xFF), (int)(result & 0xFF));
		}

		private static Vec3[] Points(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return new Vec3[0];
			}
			List<Vec3> list = new List<Vec3>();
			string[] array = s.Split('/');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length >= 3)
				{
					list.Add(new Vec3(F(array2[0]), F(array2[1]), F(array2[2])));
				}
			}
			return list.ToArray();
		}

		private void BuildingEffects(NearBuilding nb, Building b, MeshInstance inst, MeshModel model, bool lit, bool ruin, bool seen, float dist2)
		{
			BuildingFx buildingFx = FxOf(model);
			inst.Fade = 1f;
			inst.NoShadow = false;
			inst.Emissive = false;
			if (!buildingFx.Any)
			{
				return;
			}
			float clock = _clock;
			float num = (float)(b.GetHashCode() & 0x3FF) * 0.37f;
			Vec3 position = inst.Position;
			float num2 = Mathf.Sqrt(inst.Basis.C2.Z * inst.Basis.C2.Z + inst.Basis.C2.X * inst.Basis.C2.X + inst.Basis.C2.Y * inst.Basis.C2.Y);
			if (buildingFx.Animated)
			{
				inst.Static = false;
				inst.EnsurePose();
				for (int i = 0; i < buildingFx.Flames.Length; i++)
				{
					int num3 = buildingFx.Flames[i];
					float num4 = num + (float)i * 1.7f;
					inst.PartScale[num3] = (ruin ? 0f : (0.9f + 0.1f * Mathf.Sin(clock * 9.3f + num4) + 0.06f * Mathf.Sin(clock * 14.1f + num4 * 2f)));
					inst.PartRotation[num3] = Mat3.RotZ(clock * 0.7f + num4) * Mat3.RotX(0.05f * Mathf.Sin(clock * 5.1f + num4));
				}
				for (int j = 0; j < buildingFx.Floats.Length; j++)
				{
					inst.PartOffset[buildingFx.Floats[j]] = new Vec3(0f, 0f, 0.14f * Mathf.Sin(clock * 1.1f + num + (float)j * 2.1f));
				}
				for (int k = 0; k < buildingFx.Wheels.Length; k++)
				{
					inst.PartRotation[buildingFx.Wheels[k]] = Mat3.RotX(lit ? (clock * 0.8f + num) : num);
				}
				for (int l = 0; l < buildingFx.Spins.Length; l++)
				{
					inst.PartRotation[buildingFx.Spins[l]] = Mat3.RotZ(clock * 0.45f + num + (float)l);
				}
				if (buildingFx.Ghostly > 0f)
				{
					GhostLook.Apply(inst, buildingFx.Ghostly, _night);
				}
				if (buildingFx.Hover > 0f)
				{
					float num5 = (buildingFx.Hover + 0.06f * Mathf.Sin(clock * 0.9f + num)) * num2;
					inst.Position = new Vec3(position.X, position.Y, position.Z + num5);
					position = inst.Position;
				}
			}
			if (!seen || (ruin && buildingFx.Mist == 0))
			{
				return;
			}
			float x = position.X;
			float y = position.Y;
			if (_night > 0.05f && dist2 < 2304f && !ruin)
			{
				for (int m = 0; m < buildingFx.LightAt.Length; m++)
				{
					Vec3 vec = inst.Basis.Mul(buildingFx.LightAt[m]) + position;
					uint num6 = buildingFx.LightColor[m];
					float num7 = (0.25f + 0.75f * _night) * Flicker(vec.X * 2.3f + vec.Y + (float)m);
					AddLight(vec.X, vec.Y, vec.Z, Mathf.Clamp(buildingFx.LightRadius[m] * num2, 1.5f, 9f), (float)(num6 & 0xFF) / 255f * num7, (float)((num6 >> 8) & 0xFF) / 255f * num7, (float)((num6 >> 16) & 0xFF) / 255f * num7, 0.08f);
				}
			}
			if (dist2 > 2500f)
			{
				return;
			}
			uint hashCode = (uint)b.GetHashCode();
			if (!ruin && lit && (!b.asset.hasHousingSlots() || AllLit || HouseOccupancy.Count(b) > 0))
			{
				for (int n = 0; n < buildingFx.Smoke.Length; n++)
				{
					Vec3 vec2 = inst.Basis.Mul(buildingFx.Smoke[n]) + position;
					ChimneySmoke(vec2.X, vec2.Y, vec2.Z, hashCode + (uint)(n * 7919), Mathf.Clamp(num2 * 1.2f, 0.4f, 1.4f), 0.85f);
				}
			}
			if (!ruin)
			{
				for (int num8 = 0; num8 < buildingFx.Flames.Length; num8++)
				{
					Vec3 vec3 = inst.Basis.Mul(model.Parts[buildingFx.Flames[num8]].Pivot + new Vec3(0f, 0f, 0.9f)) + position;
					ChimneySmoke(vec3.X, vec3.Y, vec3.Z, hashCode + 31 + (uint)(num8 * 104729), Mathf.Clamp(num2 * 1.1f, 0.4f, 1.3f), 0.35f);
				}
			}
			if (buildingFx.Mist != 0)
			{
				Mist(x, y, _terrain.SampleHeight(x, y), Mathf.Max(nb.Width, 1.5f), buildingFx.Mist, hashCode);
			}
		}

		private void ChimneySmoke(float cx, float cy, float z, uint seed, float size, float pale)
		{
			for (int i = 0; i < 5; i++)
			{
				uint num = TerrainModel.Hash((int)seed + i * 613, i * 29 + 1);
				float num2 = 7f + (float)((num >> 24) & 0xF) * 0.2f;
				float num3 = Frac(_clock / num2 + (float)(num & 0x3FF) / 1023f) * num2;
				float num4 = num3 / num2;
				float num5 = num3 * 0.32f * size;
				float num6 = num5 * num5 * 0.25f;
				float num7 = Mathf.Sin(num3 * 1.3f + (float)(num & 0x3F)) * 0.08f * size;
				float num8 = size * (0.22f + 0.75f * num4);
				float num9 = ((num4 < 0.1f) ? (num4 / 0.1f) : Mathf.Pow(1f - (num4 - 0.1f) / 0.9f, 1.4f));
				int shade = Mathf.Clamp((int)(pale * 5f + num4 * 1.5f), 0, 5);
				Billboards.Add(new Billboard
				{
					X = cx + num6 + num7,
					Y = cy + num6 * 0.4f,
					Z = z + num5 - num8 * 0.5f,
					GroundZ = z - num8,
					Width = num8,
					Height = num8,
					Image = FireField.SoftSmoke(shade, (int)(num & 3)),
					Kind = BillboardKind.Facing,
					Flip = ((num & 4) != 0),
					Tag = -1,
					Alpha = (byte)Mathf.Max(1, (int)(105f * num9))
				});
			}
		}

		private void Mist(float cx, float cy, float ground, float width, uint tint, uint seed)
		{
			int num = 4;
			float night = _night;
			for (int i = 0; i < num; i++)
			{
				uint num2 = TerrainModel.Hash((int)seed + i * 4099, i * 13 + 7);
				float f = (float)i * (MathF.PI * 2f / (float)num) + (float)(num2 & 0xFF) / 255f * 1.2f + _clock * (0.03f + (float)((num2 >> 8) & 0xF) * 0.004f);
				float num3 = width * (0.45f + 0.35f * (float)((num2 >> 12) & 0xFF) / 255f);
				float num4 = 0.75f + 0.25f * Mathf.Sin(_clock * 0.37f + (float)i * 1.9f);
				float x = cx + Mathf.Cos(f) * num3;
				float y = cy + Mathf.Sin(f) * num3;
				float num5 = width * (0.9f + 0.5f * (float)((num2 >> 20) & 0xFF) / 255f);
				float num6 = num5 * 0.32f;
				float num7 = Mathf.Min(ground, _terrain.SampleHeight(x, y));
				Billboards.Add(new Billboard
				{
					X = x,
					Y = y,
					Z = num7 - num6 * 0.25f,
					GroundZ = num7,
					Width = num5,
					Height = num6,
					Image = FireField.SoftSmoke(5, (int)(num2 & 3)),
					Kind = BillboardKind.Facing,
					Flip = ((num2 & 8) != 0),
					Tag = -1,
					Multiply = tint,
					Emissive = (night > 0.5f),
					Alpha = (byte)Mathf.Clamp((int)((22f + 58f * night) * num4), 1, 255)
				});
			}
		}

		private static float Frac(float v)
		{
			return v - Mathf.Floor(v);
		}

		private int UnitSlot(int index)
		{
			if (index >= _unitMemo.Length)
			{
				int num;
				for (num = _unitMemo.Length; num <= index; num *= 2)
				{
				}
				Array.Resize(ref _unitMemo, num);
			}
			return index;
		}

		private bool SpecialUnit(Actor a, int index, Actor host, ViewState view, float range, TerrainCache terrain, SpriteCache sprites, float dt)
		{
			int num = UnitSlot(index);
			int num2 = KindOf(a, ref _unitMemo[num]);
			if ((num2 & 0x1E) != 0)
			{
				SceneClock.To(SceneClock.B.Checks);
			}
			if ((num2 & 2) != 0)
			{
				if (Models.Ready)
				{
					AddCrabzilla(a, view, host, terrain, dt);
				}
				return true;
			}
			if (a != host && (num2 & 4) != 0 && Models.Ready && Dragons.Ready)
			{
				AddDragon(a, view, terrain, dt);
				return true;
			}
			if (a != host && (num2 & 8) != 0 && AddUfo(a, view, range, terrain, sprites, dt))
			{
				return true;
			}
			if ((num2 & 0x10) != 0 && Models.Get("god_hand") != null)
			{
				AddGodFinger(a, view, terrain, dt);
				return true;
			}
			return false;
		}

		private static int KindOf(Actor a, ref UnitMemo m)
		{
			ActorAsset asset = a.asset;
			if (asset == null)
			{
				return 1;
			}
			string id = asset.id;
			bool flag_ufo = asset.flag_ufo;
			if (m.Kind != 0 && (object)m.Id == id && m.Ufo == flag_ufo)
			{
				if (VerifyKinds)
				{
					CompareKinds(a, m.Kind);
				}
				return m.Kind;
			}
			int num = 1;
			if (id == "crabzilla")
			{
				num |= 2;
			}
			if (id == "dragon" || id == "zombie_dragon")
			{
				num |= 4;
			}
			if (flag_ufo)
			{
				num |= 8;
			}
			if (id == "god_finger")
			{
				num |= 0x10;
			}
			m.Id = id;
			m.Ufo = flag_ufo;
			m.Kind = num;
			if (VerifyKinds)
			{
				CompareKinds(a, num);
			}
			return num;
		}

		private float HitAge(Actor a, bool hit, float age)
		{
			float num = (hit ? age : (-1f));
			if (VerifyKinds)
			{
				float value;
				float f = (RecentHits.TryGetValue(a, out value) ? value : (-1f));
				KindCompared++;
				if (NpcTune.Bits(f) != NpcTune.Bits(num))
				{
					KindDiffered++;
					if (KindFirst == null)
					{
						KindFirst = "hit age " + NpcTune.Show(num) + " vs " + NpcTune.Show(f);
					}
				}
			}
			return num;
		}

		private SpriteImage UnitImage(int index, Sprite sprite, SpriteCache sprites)
		{
			int num = UnitSlot(index);
			ref UnitMemo reference = ref _unitMemo[num];
			return MemoImage(ref reference.Sprite, ref reference.Image, ref reference.Epoch, sprite, sprites);
		}

		internal static SpriteImage MemoImage(ref Sprite memoSprite, ref SpriteImage memoImage, ref int memoEpoch, Sprite sprite, SpriteCache sprites)
		{
			if (!SpriteWatch(sprites))
			{
				return sprites.Get(sprite);
			}
			if (memoEpoch == _spriteEpoch && (object)sprite != null && (object)memoSprite == sprite)
			{
				if (VerifySprites)
				{
					CompareSprite(sprite, memoImage, sprites);
				}
				return memoImage;
			}
			SpriteImage spriteImage = sprites.Get(sprite);
			if (spriteImage != null)
			{
				memoSprite = sprite;
				memoImage = spriteImage;
				memoEpoch = _spriteEpoch;
			}
			return spriteImage;
		}

		private static bool SpriteWatch(SpriteCache sprites)
		{
			if (sprites != _watched)
			{
				_watched = sprites;
				_watchedImages = null;
				_spriteEpoch++;
				if (sprites == null || _watchFailed)
				{
					return false;
				}
				try
				{
					_watchedImages = typeof(SpriteCache).GetField("_images", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(sprites) as Dictionary<Sprite, SpriteImage>;
					if (_versionOf == null && _watchedImages != null)
					{
						_versionOf = VersionReader();
					}
				}
				catch (Exception ex)
				{
					Log.Error("sprite memo: the sprite cache can't be watched (" + ex.Message + "): looked up every time");
					_watchFailed = true;
				}
				if (_watchedImages == null || _versionOf == null)
				{
					if (!_watchFailed)
					{
						Log.Info("sprite memo: the sprite cache can't be watched: looked up every time");
					}
					_watchFailed = true;
					_watchedImages = null;
					return false;
				}
				_baseCount = _watchedImages.Count;
				_baseVersion = _versionOf(_watchedImages);
				return true;
			}
			if (_watchedImages == null)
			{
				return false;
			}
			int count = _watchedImages.Count;
			int num = _versionOf(_watchedImages);
			if (num - _baseVersion != count - _baseCount)
			{
				_spriteEpoch++;
				_baseCount = count;
				_baseVersion = num;
			}
			return true;
		}

		private static Func<Dictionary<Sprite, SpriteImage>, int> VersionReader()
		{
			Type typeFromHandle = typeof(Dictionary<Sprite, SpriteImage>);
			FieldInfo f = typeFromHandle.GetField("version", BindingFlags.Instance | BindingFlags.NonPublic) ?? typeFromHandle.GetField("_version", BindingFlags.Instance | BindingFlags.NonPublic);
			if (f == null || f.FieldType != typeof(int))
			{
				return null;
			}
			try
			{
				DynamicMethod dynamicMethod = new DynamicMethod("FirstPerson_SpriteCacheVersion", typeof(int), new Type[1] { typeFromHandle }, typeof(SceneCollector).Module, skipVisibility: true);
				ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
				iLGenerator.Emit(OpCodes.Ldarg_0);
				iLGenerator.Emit(OpCodes.Ldfld, f);
				iLGenerator.Emit(OpCodes.Ret);
				Func<Dictionary<Sprite, SpriteImage>, int> func = (Func<Dictionary<Sprite, SpriteImage>, int>)dynamicMethod.CreateDelegate(typeof(Func<Dictionary<Sprite, SpriteImage>, int>));
				Dictionary<Sprite, SpriteImage> dictionary = new Dictionary<Sprite, SpriteImage>();
				if (func(dictionary) == (int)f.GetValue(dictionary))
				{
					return func;
				}
			}
			catch (Exception ex)
			{
				Log.Info("sprite memo: reflection read of the version (" + ex.Message + ")");
			}
			return (Dictionary<Sprite, SpriteImage> d) => (int)f.GetValue(d);
		}

		internal static float InfoFloat(ref InfoMemo m, MeshModel model, string key, float fallback)
		{
			string text = model.Info(key);
			if ((object)text != m.Raw)
			{
				m.Ok = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out m.Value);
				m.Raw = text;
			}
			float num = (m.Ok ? m.Value : fallback);
			if (VerifyInfo)
			{
				CompareInfo(model, key, fallback, num);
			}
			return num;
		}

		internal static string RuinName(ref string of, ref string name, string family)
		{
			if (name == null || (object)of != family)
			{
				of = family;
				name = "ruin_" + family;
			}
			else if (VerifyInfo)
			{
				CompareName(name, "ruin_" + family);
			}
			return name;
		}

		internal static string SiteName(ref string of, ref string name, string family)
		{
			if (name == null || (object)of != family)
			{
				of = family;
				name = "site_" + family;
			}
			else if (VerifyInfo)
			{
				CompareName(name, "site_" + family);
			}
			return name;
		}

		internal static MeshModel CropModelNamed(int stage, float dist)
		{
			string[] array = CropNames[stage];
			return Models.Get((dist < CropNearTo[stage]) ? array[0] : ((dist < CropMiddleTo[stage]) ? array[1] : array[2])) ?? Models.Get(array[0]);
		}

		internal static int KindOldForTest(Actor a)
		{
			return 1 | (Crabzillas.Is(a) ? 2 : 0) | (Dragons.Is(a) ? 4 : 0) | (IsUfo(a) ? 8 : 0) | (IsGodFinger(a) ? 16 : 0);
		}

		internal static int KindNewForTest(Actor a, UnitMemo[] memos, int slot)
		{
			return KindOf(a, ref memos[slot]);
		}

		internal static MeshModel CropModelOldForTest(int stage, float dist)
		{
			return CropModel("crop_wheat_" + stage, dist, stage);
		}

		private static void CompareSprite(Sprite sprite, SpriteImage memo, SpriteCache sprites)
		{
			SpriteImage spriteImage = sprites.Get(sprite);
			SpriteCompared++;
			if (spriteImage != memo)
			{
				SpriteDiffered++;
				if (SpriteFirst == null)
				{
					SpriteFirst = "sprite " + Name(sprite) + ": memo " + ((memo != null) ? memo.GetHashCode().ToString() : "null") + " vs the cache's " + ((spriteImage != null) ? spriteImage.GetHashCode().ToString() : "null");
				}
			}
		}

		private static string Name(Sprite s)
		{
			try
			{
				return (s != null) ? s.name : "(none)";
			}
			catch
			{
				return "?";
			}
		}

		private static void CompareKinds(Actor a, int kind)
		{
			KindCompared++;
			bool flag = Crabzillas.Is(a);
			bool flag2 = Dragons.Is(a);
			bool flag3 = IsUfo(a);
			bool flag4 = IsGodFinger(a);
			if (flag != ((kind & 2) != 0) || flag2 != ((kind & 4) != 0) || flag3 != ((kind & 8) != 0) || flag4 != ((kind & 0x10) != 0))
			{
				KindDiffered++;
				if (KindFirst == null)
				{
					KindFirst = "unit " + ((a.asset != null) ? a.asset.id : "?") + ": memo " + kind + " vs crab " + flag + ", dragon " + flag2 + ", ufo " + flag3 + ", finger " + flag4;
				}
			}
		}

		private static void CompareName(string memo, string old)
		{
			InfoCompared++;
			if (!string.Equals(memo, old, StringComparison.Ordinal))
			{
				InfoDiffered++;
				if (InfoFirst == null)
				{
					InfoFirst = "name \"" + memo + "\" vs \"" + old + "\"";
				}
			}
		}

		private static void CompareInfo(MeshModel model, string key, float fallback, float memo)
		{
			float f = model.InfoFloat(key, fallback);
			InfoCompared++;
			if (NpcTune.Bits(f) != NpcTune.Bits(memo))
			{
				InfoDiffered++;
				if (InfoFirst == null)
				{
					InfoFirst = model.Name + " " + key + ": memo " + NpcTune.Show(memo) + " vs " + NpcTune.Show(f);
				}
			}
		}

		private void StepTwisters(ViewState view, TerrainCache terrain, float step)
		{
			_twisterTime += step;
			_wind.Vortices.Clear();
			TwisterStormsForTest = 0;
			TwisterShakeForTest = 0f;
			if (_twisters.Count == 0 && _twistersGone.Count == 0)
			{
				if (_baseLifted)
				{
					Weather.Base = 15f;
					Weather.Thickness = 7f;
					_baseLifted = false;
				}
				return;
			}
			_twistersStale.Clear();
			foreach (KeyValuePair<TornadoEffect, TwisterState> twister in _twisters)
			{
				if (twister.Value.Seen < _frame - 1)
				{
					_twistersStale.Add(twister.Key);
				}
			}
			foreach (TornadoEffect item in _twistersStale)
			{
				_twistersGone.Add(_twisters[item]);
				_twisters.Remove(item);
			}
			float lift = 0f;
			float baseTo = 15f;
			float thickTo = 7f;
			foreach (TwisterState value in _twisters.Values)
			{
				StepTwisterStorm(value, view, step, gone: false, ref lift, ref baseTo, ref thickTo);
			}
			for (int num = _twistersGone.Count - 1; num >= 0; num--)
			{
				TwisterState twisterState = _twistersGone[num];
				StepTwisterStorm(twisterState, view, step, gone: true, ref lift, ref baseTo, ref thickTo);
				if (twisterState.Presence <= 0.01f)
				{
					_twistersGone.RemoveAt(num);
				}
			}
			float num2 = BlastFx.Smooth(lift);
			Weather.Base = 15f + (baseTo - 15f) * num2;
			Weather.Thickness = 7f + (thickTo - 7f) * num2;
			_baseLifted = true;
			StepTwisterLightning(view, step);
		}

		private void StepTwisterStorm(TwisterState st, ViewState view, float step, bool gone, ref float lift, ref float baseTo, ref float thickTo)
		{
			float num = (gone ? 0f : (st.Dying ? (0.35f + 0.65f * st.Form) : Mathf.Clamp01(0.25f + st.Age / 4f)));
			st.Presence += (num - st.Presence) * (1f - Mathf.Exp((0f - step) / (gone ? 2f : 1.2f)));
			if (gone)
			{
				st.Storm += new Vector2(Weather.WindX, Weather.WindY) * step;
			}
			if (!(st.Presence <= 0.01f))
			{
				float s = Mathf.Clamp(st.Scale, 0.1f, 5f);
				lift = Mathf.Max(lift, st.Presence);
				baseTo = Mathf.Max(baseTo, st.Ground + TornadoFx.StormBase(s));
				thickTo = Mathf.Max(thickTo, TornadoFx.StormThickness(s));
				float num2 = TornadoFx.StormHalf(s);
				Vector2 vector = new Vector2(Weather.WindX, Weather.WindY);
				if (vector.sqrMagnitude > 0.0001f)
				{
					vector.Normalize();
				}
				Vector2 vector2 = st.Storm + vector * num2 * 0.25f;
				Weather.Storms.Add(new StormCloud
				{
					X = vector2.x,
					Y = vector2.y,
					HalfX = num2,
					HalfY = num2 * 0.85f,
					Color = TornadoFx.StormColor,
					Opacity = st.Presence * 1.35f * (FromSky ? 0.6f : 1f),
					Drops = Precip.None,
					Seed = (float)(st.Seed & 0x3FF) / 97f
				});
				TwisterStormsForTest++;
				if (!gone && !FromSky)
				{
					Twister t = Shape(st);
					_wind.Vortices.Add(TornadoFx.Wind(in t));
					float num3 = Vector2.Distance(st.Foot, new Vector2(view.X, view.Y));
					float num4 = TornadoFx.DustRadius(s);
					float b = 0.26f * (1f - BlastFx.Smooth((num3 - num4) / (num4 * 3f))) * BlastFx.Smooth(st.Form * 1.5f);
					TwisterShakeForTest = Mathf.Max(TwisterShakeForTest, b);
					BlastShake = Mathf.Max(BlastShake, b);
				}
			}
		}

		private void StepTwisterLightning(ViewState view, float step)
		{
			if (step <= 0f)
			{
				return;
			}
			if (_flashPulses > 0)
			{
				_flashGap -= step;
				if (!(_flashGap > 0f))
				{
					float num = Mathf.Clamp01(1.15f - _flashDist / 200f) * (0.22f + 0.18f * UnityEngine.Random.value);
					Weather.Flash = Mathf.Max(Weather.Flash, num);
					AddLight(_flashAt.x, _flashAt.y, Weather.Base - 2f, 45f, 1.5f * num * 2.5f, 1.6f * num * 2.5f, 2f * num * 2.5f, 0f);
					_flashPulses--;
					_flashGap = 0.06f + 0.14f * UnityEngine.Random.value;
					if (_flashPulses == 0)
					{
						FirstPersonAudio.QueueRumble(_flashDist + 70f);
					}
				}
				return;
			}
			_flashIn -= step;
			if (_flashIn > 0f)
			{
				return;
			}
			_flashIn = 5f + 9f * UnityEngine.Random.value;
			TwisterState twisterState = null;
			float num2 = 230f;
			foreach (TwisterState value in _twisters.Values)
			{
				float num3 = Vector2.Distance(value.Storm, new Vector2(view.X, view.Y));
				if (num3 < num2 && value.Presence > 0.5f)
				{
					twisterState = value;
					num2 = num3;
				}
			}
			if (twisterState != null)
			{
				float num4 = TornadoFx.StormHalf(twisterState.Scale);
				_flashAt = twisterState.Storm + UnityEngine.Random.insideUnitCircle * num4 * 0.5f;
				_flashDist = Vector2.Distance(_flashAt, new Vector2(view.X, view.Y));
				_flashPulses = 1 + UnityEngine.Random.Range(0, 3);
				_flashGap = 0f;
			}
		}

		private void AddTornado(TornadoEffect t, ViewState view, TerrainCache terrain)
		{
			Vector3 position = t.transform.position;
			float num = WorldStep(_frameDt);
			if (!_twisters.TryGetValue(t, out var value) || value.Stamp != ((BaseEffect)t)._timestamp_spawned)
			{
				value = new TwisterState
				{
					Stamp = ((BaseEffect)t)._timestamp_spawned,
					Foot = position,
					Top = position,
					Storm = position,
					Seed = ((uint)RuntimeHelpers.GetHashCode(t) ^ (uint)(((BaseEffect)t)._timestamp_spawned * 1000.0))
				};
				_twisters[t] = value;
			}
			value.Seen = _frame;
			value.Age += num;
			value.Foot = Vector2.Lerp(value.Foot, position, 1f - Mathf.Exp((0f - num) / 0.35f));
			value.Top = Vector2.Lerp(value.Top, value.Foot, 1f - Mathf.Exp((0f - num) / 2.5f));
			value.Storm = Vector2.Lerp(value.Storm, value.Foot, 1f - Mathf.Exp((0f - num) / 8f));
			value.Scale = Mathf.Clamp((((BaseEffect)t).scale > 0f) ? ((BaseEffect)t).scale : Mathf.Abs(t.transform.localScale.x), 0.02f, 5f);
			bool flag = ((BaseEffect)t).state == 2;
			if (flag && !value.Dying)
			{
				value.DeathScale = Mathf.Max(0.05f, value.Scale);
			}
			value.Dying = flag;
			value.Form = (flag ? Mathf.Clamp01(value.Scale / value.DeathScale) : BlastFx.Smooth(Mathf.Clamp01(value.Age / 3.5f)));
			value.Ground = terrain.SampleHeight(value.Foot.x, value.Foot.y);
			int num2 = Mathf.Clamp((int)value.Foot.x, 0, terrain.Width - 1);
			int num3 = Mathf.Clamp((int)value.Foot.y, 0, terrain.Height - 1) * terrain.Width + num2;
			if (num3 < terrain.Colors.Length)
			{
				value.Dust = terrain.Colors[num3];
				value.Water = (terrain.Flags[num3] & 1) != 0;
			}
			float num4 = value.Foot.x - view.X;
			float num5 = value.Foot.y - view.Y;
			float num6 = SkyRange(200f);
			if (!(num4 * num4 + num5 * num5 > num6 * num6))
			{
				Twister t2 = Shape(value);
				float b = Mathf.Sqrt(num4 * num4 + num5 * num5);
				float detail = Mathf.Clamp(30f / Mathf.Max(1f, b), 0.3f, 1f) * Mathf.Max(0.55f, Detail);
				Vec3 light = ((_air != null) ? _air.Light : new Vec3(0.3f, 0.3f, 0.9f));
				float sunny = ((_air != null) ? (_air.SunVisible * 0.45f) : 0f);
				int count = Billboards.Count;
				TornadoFx.Draw(Billboards, in t2, _twisterTime, view.X, view.Y, light, sunny, detail);
				TwisterPiecesForTest = Billboards.Count - count;
			}
		}

		private Twister Shape(TwisterState st)
		{
			return new Twister
			{
				X = st.Foot.x,
				Y = st.Foot.y,
				Z = st.Ground,
				TopX = st.Top.x,
				TopY = st.Top.y,
				Base = Weather.Base,
				Scale = st.Scale,
				Form = st.Form,
				Dying = st.Dying,
				Seed = st.Seed,
				Dust = st.Dust,
				Water = st.Water
			};
		}

		private void PullGusts(ViewState view)
		{
			if (_wind.Vortices.Count == 0)
			{
				return;
			}
			float num = 0f;
			float num2 = 0f;
			foreach (Vortex vortice in _wind.Vortices)
			{
				vortice.Flow(view.X, view.Y, out var vx, out var vy, out var _);
				num += vx;
				num2 += vy;
			}
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 > 0.05f)
			{
				Gusts.Pull(Mathf.Clamp01(num3 / 9f), num / num3, num2 / num3);
			}
		}

		private static bool IsUfo(Actor a)
		{
			try
			{
				return a.asset != null && a.asset.flag_ufo;
			}
			catch
			{
				return false;
			}
		}

		private bool AddUfo(Actor a, ViewState view, float range, TerrainCache terrain, SpriteCache sprites, float dt)
		{
			MeshModel meshModel = (Models.Ready ? Models.Get("monster_ufo") : null);
			if (meshModel == null)
			{
				return false;
			}
			UfoState ufoState = UfoStateOf(a);
			if (ufoState == null)
			{
				return false;
			}
			ufoState.Seen = _frame;
			Vector2 current_position = a.current_position;
			float num = WorldStep(dt);
			if (ufoState.HasLast && num > 0.0001f)
			{
				Vec3 vec = new Vec3((current_position.x - ufoState.Last.x) / num, (current_position.y - ufoState.Last.y) / num, 0f);
				if (vec.X * vec.X + vec.Y * vec.Y > 3600f)
				{
					vec = new Vec3(0f, 0f, 0f);
				}
				ufoState.Vel += (vec - ufoState.Vel) * Mathf.Clamp01(num * 6f);
			}
			ufoState.Last = current_position;
			ufoState.HasLast = true;
			ufoState.Ground = terrain.SampleHeight(current_position.x, current_position.y);
			try
			{
				SpriteImage spriteImage = sprites.Get(a.getSpriteToRender());
				if (spriteImage != null)
				{
					float num2 = spriteImage.UnitWidth * Mathf.Abs(a.current_scale.x) * Models.OpaqueWidth(spriteImage) * 0.95f;
					ufoState.Diameter = ((ufoState.Diameter <= 0f) ? num2 : Mathf.Lerp(ufoState.Diameter, num2, Mathf.Clamp01(dt * 2f)));
				}
			}
			catch
			{
			}
			if (ufoState.Diameter <= 0f)
			{
				ufoState.Diameter = 4.8f;
			}
			if (!RecentHits.TryGetValue(a, out var value))
			{
				value = 99f;
			}
			bool hitNow = value < ufoState.LastHit - 0.0001f;
			ufoState.LastHit = value;
			int num3 = -1;
			try
			{
				UFO actorComponent = a.getActorComponent<UFO>();
				if (actorComponent != null && actorComponent.beamAnim != null && actorComponent.beamAnim.isOn)
				{
					num3 = actorComponent.beamAnim.currentFrameIndex;
				}
			}
			catch
			{
			}
			float health = 1f;
			try
			{
				health = Mathf.Clamp01(a.getHealthRatio());
			}
			catch
			{
			}
			UfoFx fx = ufoState.Fx;
			fx.Diameter = ufoState.Diameter;
			fx.Land = LandColor(terrain, current_position.x, current_position.y);
			fx.Update(num, new Vec3(current_position.x, current_position.y, ufoState.Ground + Mathf.Max(0f, a.position_height)), ufoState.Vel, health, hitNow, num3);
			UfoForTest = fx;
			DrawUfo(a, ufoState, meshModel, view, range, tagged: true);
			if (num3 >= 0)
			{
				UfoBeamFrames++;
			}
			return true;
		}

		private UfoState UfoStateOf(Actor a)
		{
			long id;
			try
			{
				id = a.data.id;
			}
			catch
			{
				return null;
			}
			if (!_ufos.TryGetValue(a, out var value) || value.Id != id)
			{
				value = new UfoState
				{
					Id = id,
					Fx = new UfoFx((uint)(((int)id * -1640531535) ^ 0x5EED))
				};
				try
				{
					double num = WorldPace.Since(a.data.created_time);
					value.Fx.Arriving = num >= 0.0 && num < 1.0;
				}
				catch
				{
				}
				_ufos[a] = value;
				Log.Info(string.Format("UFO followed at {0:F0},{1:F0}{2}", a.current_position.x, a.current_position.y, value.Fx.Arriving ? " (arriving)" : ""));
			}
			return value;
		}

		private void DrawUfo(Actor a, UfoState st, MeshModel model, ViewState view, float range, bool tagged)
		{
			UfoFx fx = st.Fx;
			float num = fx.At.X - view.X;
			float num2 = fx.At.Y - view.Y;
			float num3 = SkyRange(Mathf.Max(range, 150f));
			st.Drawn = false;
			if (!(num * num + num2 * num2 > num3 * num3) && InView(num, num2, fx.Diameter + 14f))
			{
				float num4 = Mathf.Sqrt(num * num + num2 * num2);
				MeshInstance mesh = st.Mesh;
				mesh.Model = Models.Detailed("monster_ufo", num4 / 4f, mesh.Model) ?? model;
				fx.Pose(mesh);
				fx.Colour(mesh, mesh.Model, _clock, 1f - _night);
				if (tagged && RecentHits.TryGetValue(a, out var value) && 170f * (1f - value / 0.3f) > (float)(mesh.Flash >> 24))
				{
					mesh.Flash = ((uint)(170f * (1f - value / 0.3f)) << 24) | 0xFFFFFF;
				}
				mesh.SortDepth = num4;
				mesh.KeepSmall = num4 > 20f;
				mesh.Emissive = false;
				mesh.NoShadow = false;
				mesh.Hidden = 0uL;
				float warpFade = fx.WarpFade;
				if (!tagged || warpFade < 1f)
				{
					mesh.Fade = warpFade;
					mesh.DissolveScale = ((warpFade < 1f) ? 0.6f : 0f);
					_blastSink.AddMesh(mesh);
				}
				else
				{
					mesh.Tag = Tagged.Count;
					Tagged.Add(a);
					mesh.Fade = 1f;
					mesh.DissolveScale = 0f;
					Meshes.Add(mesh);
					Hitboxes.Record(a, mesh, person: false);
				}
				fx.Draw(_blastSink, BlastParticles, mesh, WorldStep(_frameDt), _clock);
				st.Drawn = true;
				UfoFramesDrawn++;
			}
		}

		private void StepUfos(ViewState view, float range, TerrainCache terrain, float dt)
		{
			float num = WorldStep(dt);
			_ufosGone.Clear();
			foreach (KeyValuePair<Actor, UfoState> ufo in _ufos)
			{
				if (ufo.Value.Seen == _frame)
				{
					continue;
				}
				Actor key = ufo.Key;
				UfoState value = ufo.Value;
				bool flag = false;
				bool flag2 = false;
				float num2 = 0f;
				try
				{
					flag = key != null && key.exists && key.data != null && key.data.id == value.Id;
					if (flag)
					{
						flag2 = key.isAlive();
						num2 = key.position_height;
					}
				}
				catch
				{
					flag = false;
				}
				if (flag && flag2)
				{
					if (_frame - value.Seen > 600)
					{
						_ufosGone.Add(key);
					}
					continue;
				}
				UfoFx fx = value.Fx;
				if (flag && num2 > 0.02f)
				{
					if (!fx.Dead)
					{
						fx.Die();
						value.FallFrom = num2;
						value.FallTime = 0f;
						Log.Info($"UFO shot down at {key.current_position.x:F0},{key.current_position.y:F0}, {num2:F1} up");
					}
					if (value.FallFrames > 0 && num2 < value.LeftAt - 0.001f)
					{
						value.Dropped = true;
					}
					value.FallTime += num;
					float num3 = Mathf.Clamp01(value.FallTime / 1.6f);
					float num4 = value.FallFrom * (1f - num3 * num3);
					if (num2 < num4 - 0.01f)
					{
						try
						{
							key.position_height = num4;
							num2 = num4;
						}
						catch
						{
						}
					}
					value.FallFrames++;
					value.LastHeight = (value.LeftAt = num2);
					Vector2 current_position = key.current_position;
					value.Ground = terrain.SampleHeight(current_position.x, current_position.y);
					fx.Update(num, new Vec3(current_position.x, current_position.y, value.Ground + num2), value.Vel * 0.3f, 0f, hitNow: false, -1);
					MeshModel meshModel = (Models.Ready ? Models.Get("monster_ufo") : null);
					if (meshModel != null)
					{
						DrawUfo(key, value, meshModel, view, range, tagged: false);
					}
					UfoFallingFrames++;
					value.Seen = _frame;
					continue;
				}
				_ufosGone.Add(key);
				if (value.Dropped || (fx.Dead && value.LastHeight < 1.5f))
				{
					if (!fx.Dead)
					{
						fx.Die();
					}
					UfoCrash(fx, value, terrain);
				}
				else if (fx.Dead)
				{
					Log.Info($"UFO gone at {fx.At.X:F0},{fx.At.Y:F0} without coming down");
				}
			}
			foreach (Actor item in _ufosGone)
			{
				_ufos.Remove(item);
			}
			UfoSpots.Clear();
			foreach (UfoState value2 in _ufos.Values)
			{
				if (!value2.Fx.Dead && value2.Seen == _frame)
				{
					float num5 = value2.Fx.At.X - _eyeX;
					float num6 = value2.Fx.At.Y - _eyeY;
					if (num5 * num5 + num6 * num6 < 3600f)
					{
						UfoSpots.Add(new Vector3(value2.Fx.At.X, value2.Fx.At.Y, value2.Fx.At.Z - value2.Ground + value2.Diameter * 0.3f));
					}
				}
			}
			for (int num7 = UfoCrashes.Count - 1; num7 >= 0; num7--)
			{
				UfoCrashes[num7].Step(num, _blastSink, BlastParticles, SkyRange(160f));
				if (UfoCrashes[num7].Finished)
				{
					UfoCrashes.RemoveAt(num7);
				}
			}
		}

		private void UfoCrash(UfoFx fx, UfoState st, TerrainCache terrain)
		{
			float x = fx.At.X;
			float y = fx.At.Y;
			UfoCrashes.Add(fx.Crash(terrain.SampleHeight(x, y), (st.Mesh.Model != null) ? st.Mesh : null));
			if (UfoCrashes.Count > 8)
			{
				UfoCrashes.RemoveAt(0);
			}
			FirstPersonAudio.QueueRumble(Mathf.Sqrt((x - _eyeX) * (x - _eyeX) + (y - _eyeY) * (y - _eyeY)));
			Log.Info($"UFO crashed at {x:F0},{y:F0}");
		}

		private bool TakeUfoExplosion(BaseEffect effect, TerrainCache terrain)
		{
			Vector3 position = effect.transform.position;
			foreach (UfoCrashFx ufoCrash in UfoCrashes)
			{
				if (ufoCrash.Age < 3f && (ufoCrash.X - position.x) * (ufoCrash.X - position.x) + (ufoCrash.Y - position.y) * (ufoCrash.Y - position.y) < 64f)
				{
					return true;
				}
			}
			double timestamp_spawned = effect._timestamp_spawned;
			if (!_powerFxSeen.TryGetValue(effect, out var value) || value != timestamp_spawned)
			{
				_powerFxSeen[effect] = timestamp_spawned;
				UfoCrashes.Add(new UfoCrashFx(position.x, position.y, terrain.SampleHeight(position.x, position.y), 4.8f, (uint)(timestamp_spawned * 1000.0) ^ 0xA11E, LandColor(terrain, position.x, position.y)));
			}
			return true;
		}

		private void ClearUfos()
		{
			_ufos.Clear();
			UfoCrashes.Clear();
			UfoSpots.Clear();
			UfoForTest = null;
		}

		private static bool IsVentFamily(string family)
		{
			if (!(family == "nature_volcano") && !(family == "nature_geyser"))
			{
				return family == "nature_geyser_acid";
			}
			return true;
		}

		private static Vec3 VentOffset(MeshModel m)
		{
			string text = m?.Info("vent");
			if (text != null)
			{
				string[] array = text.Split(',');
				CultureInfo invariantCulture = CultureInfo.InvariantCulture;
				if (array.Length == 3 && float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2) && float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3))
				{
					return new Vec3(result, result2, result3);
				}
			}
			return new Vec3(0f, 0f, (m != null) ? (m.Max.Z * 0.8f) : 1f);
		}

		internal static Vector3 VolcanoVent(float x, float y, TerrainCache cache)
		{
			Vec3 vec = VentOffset(Models.Get("nature_volcano"));
			float num = y + 1f;
			float num2 = cache.SampleHeight(x, num);
			return new Vector3(x + vec.X, num + vec.Y, num2 - 0.1f + vec.Z);
		}

		private bool DrawVent(NearBuilding nb, Building b, Vector2 p, float dist2, int tag)
		{
			float num = Mathf.Sqrt(dist2);
			MeshModel meshModel = Models.Detailed(nb.MeshFamily, num * 0.45f, nb.Mesh?.Model);
			if (meshModel == null)
			{
				return false;
			}
			float x = p.x;
			float num2 = p.y + nb.CenterOffset;
			float num3 = meshModel.InfoFloat("radius", Mathf.Max(meshModel.Max.X, meshModel.Max.Y));
			bool flag = _farBuild || InView(x - _eyeX, num2 - _eyeY, num3 * 1.3f + meshModel.Max.Z * 0.5f);
			float num4 = num3 * 0.55f;
			float num5 = Mathf.Min(_terrain.SampleHeight(x, num2), Mathf.Min(Mathf.Min(_terrain.SampleHeight(x - num4, num2 - num4), _terrain.SampleHeight(x + num4, num2 - num4)), Mathf.Min(_terrain.SampleHeight(x - num4, num2 + num4), _terrain.SampleHeight(x + num4, num2 + num4))));
			if (flag || num < 41f)
			{
				MeshInstance meshInstance = nb.Mesh;
				if (meshInstance == null)
				{
					MeshInstance obj = new MeshInstance
					{
						Static = true
					};
					MeshInstance meshInstance2 = obj;
					nb.Mesh = obj;
					meshInstance = meshInstance2;
				}
				MeshInstance meshInstance3 = meshInstance;
				meshInstance3.Model = meshModel;
				meshInstance3.Palette = null;
				meshInstance3.ShadowOnly = !flag;
				meshInstance3.Static = Steady(nb, p);
				meshInstance3.Place(x, num2, num5 - 0.1f, -MathF.PI / 2f, 1f, 1f, 1f);
				meshInstance3.Tag = tag;
				meshInstance3.SortDepth = num;
				AddModel(meshInstance3);
			}
			if (_wantSolid)
			{
				Solid(Colliders.FromModel(meshModel, x, num2, -MathF.PI / 2f, 1f, 0.05f, 1f));
			}
			if (num < 120f && !_farBuild)
			{
				VentFx(nb.MeshFamily, b, meshModel, new Vec3(x, num2, num5 - 0.1f), num);
			}
			return true;
		}

		private void VentFx(string family, Building b, MeshModel model, Vec3 at, float dist)
		{
			if (!_vents.TryGetValue(b, out var value))
			{
				value = new VentState
				{
					Seed = (uint)b.GetHashCode()
				};
				value.NextBurst = 3f + BlastFx.Rand(value.Seed, 1) * 6f;
				_vents[b] = value;
			}
			float num = (GameClock.EffectsPaused ? 0f : _frameDt);
			value.Clock += num;
			Vec3 vec = at + VentOffset(model);
			FxParticles blastParticles = BlastParticles;
			FxSink blastSink = _blastSink;
			float num2 = Mathf.Clamp01(1.4f - dist / 90f);
			uint seed = value.Seed;
			int num3 = blastParticles.List.Count + (int)(value.Clock * 997f);
			if (family == "nature_volcano")
			{
				float num4 = 0.85f + 0.15f * Mathf.Sin(value.Clock * 9f + (float)seed) * Mathf.Sin(value.Clock * 5.3f);
				blastSink.Glow(vec.X, vec.Y, vec.Z + 0.6f, 6f, TerrainCache.Rgb(255, 130, 50), 0.35f * num4);
				for (int i = 0; i < LavaFeet.Length; i++)
				{
					Vec3 vec2 = at + LavaFeet[i];
					if (dist < 70f)
					{
						blastSink.Light(vec2.X, vec2.Y, vec2.Z + 0.6f, 5.5f, TerrainCache.Rgb(255, 110, 40), 0.9f * num4);
					}
					if (num2 > 0f && BlastFx.Rand(seed, num3 + 500 + i) < num * 2.5f)
					{
						FxParticles.P p = new FxParticles.P
						{
							Pos = vec2 + new Vec3(0f, 0f, 0.2f),
							Vel = new Vec3(0f, 0f, 0.9f),
							Life = 2.5f,
							Size = 0.5f,
							EndSize = 2f,
							Color = TerrainCache.Rgb(150, 140, 138),
							EndColor = TerrainCache.Rgb(200, 196, 196),
							Alpha = 100,
							Variant = ((num3 + i) & 7),
							Drag = 0.6f,
							Gravity = -0.3f,
							Kind = FxParticles.Kind.Steam
						};
						blastParticles.Add(in p);
					}
				}
				if (value.Clock >= value.NextSpurt)
				{
					value.NextSpurt = value.Clock + 0.25f + BlastFx.Rand(seed, num3) * 0.45f;
					int num5 = 1 + (int)(BlastFx.Rand(seed, num3 + 1) * 3f * num2);
					for (int j = 0; j < num5; j++)
					{
						LavaBlob(blastParticles, vec, seed, num3 + j * 11, 4f, 8f);
					}
				}
				if (value.Burst < 0f && value.Clock >= value.NextBurst)
				{
					value.Burst = 0f;
					if (dist < 35f)
					{
						BlastShake = Mathf.Max(BlastShake, 0.35f * (1f - dist / 35f));
					}
				}
				if (!(value.Burst >= 0f))
				{
					return;
				}
				value.Burst += num;
				float burst = value.Burst;
				if (burst < 2.2f)
				{
					int num6 = (int)(num * 42f * num2 + BlastFx.Rand(seed, num3 + 2));
					for (int k = 0; k < num6; k++)
					{
						LavaBlob(blastParticles, vec, seed, num3 + 100 + k * 13, 9f, 15f);
					}
					blastSink.Glow(vec.X, vec.Y, vec.Z + 3f, 14f, TerrainCache.Rgb(255, 150, 60), 0.5f * (1f - burst / 2.2f));
					blastSink.Light(vec.X, vec.Y, vec.Z + 4f, 30f, TerrainCache.Rgb(255, 120, 50), 1.6f * (1f - burst / 2.2f));
					if (burst < 0.6f && num2 > 0f)
					{
						for (int l = 0; l < 2; l++)
						{
							blastParticles.Smoke(vec + new Vec3(0f, 0f, 1f), new Vec3((BlastFx.Rand(seed, num3 + l) - 0.5f) * 3f, (BlastFx.Rand(seed, num3 + l + 7) - 0.5f) * 3f, 6f + BlastFx.Rand(seed, num3 + l + 9) * 4f), 7f, 2.5f, 8f, TerrainCache.Rgb(54, 46, 46), TerrainCache.Rgb(110, 102, 100), 220, (num3 + l) & 7, 0.5f);
						}
					}
				}
				else
				{
					value.Burst = -1f;
					value.NextBurst = value.Clock + 7f + BlastFx.Rand(seed, num3 + 3) * 6f;
				}
				return;
			}
			bool flag = family == "nature_geyser_acid";
			uint num7 = (flag ? TerrainCache.Rgb(170, 250, 60) : TerrainCache.Rgb(200, 236, 255));
			uint num8 = (flag ? TerrainCache.Rgb(196, 236, 150) : TerrainCache.Rgb(236, 240, 246));
			if (flag)
			{
				float num9 = 0.8f + 0.2f * Mathf.Sin(value.Clock * 3.1f + (float)seed);
				blastSink.Glow(vec.X, vec.Y, vec.Z + 0.3f, 3.5f, TerrainCache.Rgb(150, 255, 60), 0.3f * num9);
				if (dist < 40f)
				{
					blastSink.Light(vec.X, vec.Y, vec.Z + 0.8f, 7f, TerrainCache.Rgb(130, 255, 60), 0.7f * num9);
				}
			}
			if (value.Clock >= value.NextSpurt)
			{
				value.NextSpurt = value.Clock + (flag ? 0.18f : 0.3f) + BlastFx.Rand(seed, num3) * 0.25f;
				if (num2 > 0f)
				{
					FxParticles.P p = new FxParticles.P
					{
						Pos = vec + new Vec3((BlastFx.Rand(seed, num3 + 1) - 0.5f) * 0.8f, (BlastFx.Rand(seed, num3 + 2) - 0.5f) * 0.8f, 0.1f),
						Vel = new Vec3(0f, 0f, 0.8f + BlastFx.Rand(seed, num3 + 3) * 0.6f),
						Life = 3f,
						Size = 0.5f,
						EndSize = 2.2f,
						Color = num8,
						EndColor = num8,
						Alpha = (byte)(flag ? 90u : 110u),
						Variant = (num3 & 7),
						Drag = 0.6f,
						Gravity = -0.3f,
						Kind = FxParticles.Kind.Steam
					};
					blastParticles.Add(in p);
				}
				if (flag && num2 > 0f)
				{
					for (int m = 0; m < 2; m++)
					{
						blastParticles.Add(new FxParticles.P
						{
							Pos = vec + new Vec3((BlastFx.Rand(seed, num3 + 20 + m) - 0.5f) * 1.2f, (BlastFx.Rand(seed, num3 + 30 + m) - 0.5f) * 1.2f, 0f),
							Vel = new Vec3((BlastFx.Rand(seed, num3 + 40 + m) - 0.5f) * 1.2f, (BlastFx.Rand(seed, num3 + 50 + m) - 0.5f) * 1.2f, 2f + BlastFx.Rand(seed, num3 + 60 + m) * 2.5f),
							Life = 1.2f,
							Size = 0.14f,
							EndSize = 0.1f,
							Color = num7,
							EndColor = TerrainCache.Rgb(90, 190, 30),
							Kind = FxParticles.Kind.Drop,
							Gravity = 9.8f,
							Drag = 0.3f
						});
					}
				}
			}
			if (value.Burst < 0f && value.Clock >= value.NextBurst)
			{
				value.Burst = 0f;
			}
			if (value.Burst >= 0f)
			{
				value.Burst += num;
				float burst2 = value.Burst;
				float num10 = ((burst2 < 0.6f) ? (burst2 / 0.6f) : ((burst2 < 4f) ? 1f : (1f - (burst2 - 0.6f - 3.4f) / 1.2f)));
				if (num10 <= 0f)
				{
					value.Burst = -1f;
					value.NextBurst = value.Clock + (flag ? 9f : 11f) + BlastFx.Rand(seed, num3 + 5) * 7f;
				}
				else if (num2 > 0f)
				{
					float num11 = (flag ? 7f : 10f);
					int num12 = (int)(num * 90f * num10 * num2 + BlastFx.Rand(seed, num3 + 6));
					for (int n = 0; n < num12; n++)
					{
						int num13 = num3 + 200 + n * 17;
						float z = Mathf.Sqrt(19.6f * num11) * (0.75f + 0.3f * BlastFx.Rand(seed, num13)) * (0.6f + 0.4f * num10);
						float f = BlastFx.Rand(seed, num13 + 1) * 6.283f;
						float num14 = BlastFx.Rand(seed, num13 + 2) * 1.1f;
						blastParticles.Add(new FxParticles.P
						{
							Pos = vec + new Vec3(Mathf.Cos(f) * 0.15f, Mathf.Sin(f) * 0.15f, 0.1f),
							Vel = new Vec3(Mathf.Cos(f) * num14, Mathf.Sin(f) * num14, z),
							Life = 2.6f,
							Size = 0.16f + 0.1f * BlastFx.Rand(seed, num13 + 3),
							EndSize = 0.12f,
							Color = num7,
							EndColor = num7,
							Kind = FxParticles.Kind.Drop,
							Gravity = 9.8f,
							Drag = 0.25f,
							Matte = !flag
						});
					}
					if ((int)(burst2 * 12f) != (int)((burst2 - num) * 12f))
					{
						float z2 = BlastFx.Rand(seed, num3 + 7) * num11 * num10;
						FxParticles.P p = new FxParticles.P
						{
							Pos = vec + new Vec3(0f, 0f, z2),
							Vel = new Vec3((BlastFx.Rand(seed, num3 + 8) - 0.5f) * 1.5f, (BlastFx.Rand(seed, num3 + 9) - 0.5f) * 1.5f, 1.5f),
							Life = 4f,
							Size = 1.4f,
							EndSize = 4.5f,
							Color = num8,
							EndColor = num8,
							Alpha = 150,
							Variant = (num3 & 7),
							Drag = 0.5f,
							Gravity = -0.2f,
							Kind = FxParticles.Kind.Steam
						};
						blastParticles.Add(in p);
					}
					blastSink.Light(vec.X, vec.Y, vec.Z + num11 * 0.4f, 10f, flag ? TerrainCache.Rgb(120, 255, 60) : TerrainCache.Rgb(180, 220, 255), flag ? (1.1f * num10) : (0.25f * num10));
				}
			}
			if (++_ventsSweep <= 600)
			{
				return;
			}
			_ventsSweep = 0;
			List<Building> list = new List<Building>();
			foreach (KeyValuePair<Building, VentState> vent in _vents)
			{
				if (vent.Key == null || !vent.Key.isAlive())
				{
					list.Add(vent.Key);
				}
			}
			foreach (Building item in list)
			{
				_vents.Remove(item);
			}
		}

		private static void LavaBlob(FxParticles parts, Vec3 vent, uint seed, int i, float upLo, float upHi)
		{
			float f = BlastFx.Rand(seed, i) * 6.283f;
			float num = 0.6f + BlastFx.Rand(seed, i + 1) * 2.4f;
			float z = upLo + (upHi - upLo) * BlastFx.Rand(seed, i + 2);
			float num2 = 0.22f + 0.24f * BlastFx.Rand(seed, i + 3);
			FxParticles.P p = new FxParticles.P
			{
				Pos = vent + new Vec3(Mathf.Cos(f) * 0.3f, Mathf.Sin(f) * 0.3f, 0.2f),
				Vel = new Vec3(Mathf.Cos(f) * num, Mathf.Sin(f) * num, z),
				Life = 3.2f,
				Size = num2,
				EndSize = num2 * 0.8f,
				Color = BlastFx.Heat(1.02f),
				EndColor = BlastFx.Heat(0.42f),
				Kind = FxParticles.Kind.Drop,
				Gravity = 9.8f,
				Drag = 0.1f,
				Trail = (BlastFx.Rand(seed, i + 4) < 0.4f)
			};
			parts.Add(in p);
		}

		private void CollectWalls(ViewState view, TerrainCache terrain, MapBox world)
		{
			if (!WallsAsModels || terrain == null || terrain.Width == 0)
			{
				return;
			}
			int width = terrain.Width;
			int height = terrain.Height;
			float wallRange = WallRange;
			bool flag = WallReach > 46f;
			int num = (int)view.X;
			int num2 = (int)view.Y;
			float num3 = wallRange * wallRange;
			int num4 = (int)wallRange + 1;
			int num5 = 42;
			int num6 = num - num5;
			int num7 = num + num5;
			int num8 = num2 - num5;
			int num9 = num2 + num5;
			float num10 = Mathf.Atan(_cullWide);
			for (int i = -1; i <= 1; i++)
			{
				float f = view.Yaw + (float)i * num10;
				float num11 = view.X + Mathf.Cos(f) * wallRange;
				float num12 = view.Y + Mathf.Sin(f) * wallRange;
				num6 = Mathf.Min(num6, (int)num11 - 4);
				num7 = Mathf.Max(num7, (int)num11 + 4);
				num8 = Mathf.Min(num8, (int)num12 - 4);
				num9 = Mathf.Max(num9, (int)num12 + 4);
			}
			for (int j = 0; j < 4; j++)
			{
				if (!(Mathf.Abs(Mathf.DeltaAngle(view.Yaw * 57.29578f, (float)j * 90f) * (MathF.PI / 180f)) > num10))
				{
					switch (j)
					{
					case 0:
						num7 = num + num4;
						break;
					case 1:
						num9 = num2 + num4;
						break;
					case 2:
						num6 = num - num4;
						break;
					default:
						num8 = num2 - num4;
						break;
					}
				}
			}
			num6 = Mathf.Max(Mathf.Max(0, num6), num - num4);
			num7 = Mathf.Min(Mathf.Min(width - 1, num7), num + num4);
			num8 = Mathf.Max(Mathf.Max(0, num8), num2 - num4);
			num9 = Mathf.Min(Mathf.Min(height - 1, num9), num2 + num4);
			byte[] flags = terrain.Flags;
			for (int k = num8; k <= num9; k++)
			{
				for (int l = num6; l <= num7; l++)
				{
					int num13 = k * width + l;
					if ((flags[num13] & 8) == 0)
					{
						continue;
					}
					float num14 = (float)l + 0.5f - view.X;
					float num15 = (float)k + 0.5f - view.Y;
					float num16 = num14 * num14 + num15 * num15;
					if (num16 >= num3)
					{
						continue;
					}
					float num17 = Mathf.Sqrt(num16);
					bool flag2 = InView(num14, num15, 2.5f);
					if (!flag2 && num17 > 41f)
					{
						continue;
					}
					WorldTile tile = world.GetTile(l, k);
					string text = ((tile != null && tile.top_type != null) ? tile.top_type.id : null);
					if (text != null && text.StartsWith("wall_", StringComparison.Ordinal))
					{
						int num18 = 0;
						if (k + 1 < height && (flags[num13 + width] & 8) != 0)
						{
							num18 |= 1;
						}
						if (l + 1 < width && (flags[num13 + 1] & 8) != 0)
						{
							num18 |= 2;
						}
						if (k > 0 && (flags[num13 - width] & 8) != 0)
						{
							num18 |= 4;
						}
						if (l > 0 && (flags[num13 - 1] & 8) != 0)
						{
							num18 |= 8;
						}
						int quarterTurns;
						string shape = WallPieces.Shape(num18, out quarterTurns);
						string text2 = WallPieces.Family(text, shape, l, k);
						if (Models.Get(text2) == null)
						{
							text2 = WallPieces.Family(text, shape);
						}
						if (!_wallMeshes.TryGetValue(num13, out var value))
						{
							value = new MeshInstance
							{
								Static = true
							};
							_wallMeshes[num13] = value;
						}
						MeshModel meshModel;
						if (!flag2)
						{
							meshModel = Models.Get(text2 + "_f") ?? Models.Get(text2);
						}
						else if (flag)
						{
							float num19 = terrain.Ground[num13] - view.Z;
							meshModel = WallPieces.Pick(FindModel, text2, Mathf.Sqrt(num16 + num19 * num19) * WallLens, value.Model);
						}
						else
						{
							meshModel = Models.Get(WallPieces.Detailed(text2, num17, (value.Model != null) ? value.Model.Name : null)) ?? Models.Get(text2);
						}
						if (meshModel != null)
						{
							value.Model = meshModel;
							value.Static = true;
							value.ShadowOnly = !flag2;
							value.KeepSmall = flag && meshModel.Name.EndsWith("_x", StringComparison.Ordinal);
							value.Place((float)l + 0.5f, (float)k + 0.5f, terrain.Ground[num13], WallPieces.Heading(quarterTurns), 0.5f, 0.5f, 0.5f);
							value.SortDepth = num17;
							value.Tag = -1;
							_wallSeen[num13] = _frame;
							Meshes.Add(value);
						}
					}
				}
			}
			if (_frame % 240 != 0)
			{
				return;
			}
			_staleWalls.Clear();
			foreach (KeyValuePair<int, int> item in _wallSeen)
			{
				if (_frame - item.Value > 240)
				{
					_staleWalls.Add(item.Key);
				}
			}
			foreach (int staleWall in _staleWalls)
			{
				_wallMeshes.Remove(staleWall);
				_wallSeen.Remove(staleWall);
			}
		}

		private void BeginWeather(float dt)
		{
			Weather.Storms.Clear();
			for (int num = Weather.Bolts.Count - 1; num >= 0; num--)
			{
				Bolt value = Weather.Bolts[num];
				value.Age += dt;
				if (value.Age > value.Life)
				{
					Weather.Bolts.RemoveAt(num);
				}
				else
				{
					Weather.Bolts[num] = value;
				}
			}
			Weather.Flash = Mathf.Max(0f, Weather.Flash - dt * 3.5f);
			if (_frame % 300 != 0 || _boltsSeen.Count <= 0)
			{
				return;
			}
			_boltsStale.Clear();
			foreach (KeyValuePair<BaseEffect, double> item in _boltsSeen)
			{
				if (item.Key == null || !item.Key.active)
				{
					_boltsStale.Add(item.Key);
				}
			}
			foreach (BaseEffect item2 in _boltsStale)
			{
				_boltsSeen.Remove(item2);
			}
		}

		public void GatherCloudsOnly()
		{
			Weather.Storms.Clear();
			StackEffects stackEffects = ((World.world != null) ? World.world.stack_effects : null);
			if (stackEffects == null || stackEffects.list == null)
			{
				return;
			}
			try
			{
				foreach (BaseEffectController item in stackEffects.list)
				{
					if (item == null || item._list == null)
					{
						continue;
					}
					foreach (BaseEffect item2 in item._list)
					{
						if (item2 is Cloud cloud && ((BaseEffect)cloud).active && ((BaseEffect)cloud).sprite_renderer != null)
						{
							AddStorm(cloud, ((BaseEffect)cloud).sprite_renderer);
						}
					}
				}
			}
			catch
			{
			}
		}

		private void AddStorm(Cloud cloud, SpriteRenderer renderer)
		{
			try
			{
				CloudAsset asset = cloud.asset;
				if (asset == null)
				{
					return;
				}
				Vector3 localPosition = cloud.transform.localPosition;
				float num = ((cloud.spriteShadow != null) ? cloud.spriteShadow.offset.y : 0f);
				float num2 = Mathf.Max(3f, cloud.effect_texture_width);
				float num3 = Mathf.Max(2f, cloud.effect_texture_height);
				if (renderer.sprite != null)
				{
					Vector3 extents = renderer.bounds.extents;
					num2 = Mathf.Max(num2, extents.x * 0.65f);
					num3 = Mathf.Max(num3, extents.y * 0.55f);
				}
				Color color = asset.color;
				Precip precip = DropsOf(asset);
				uint num4 = (asset.normal_cloud ? TerrainCache.Rgb(250, 250, 255) : ((precip == Precip.Snow) ? Weather.SnowCloud : TerrainCache.Rgb((int)(color.r * 255f), (int)(color.g * 255f), (int)(color.b * 255f))));
				float num5 = ((asset.max_alpha > 0f) ? asset.max_alpha : 1f);
				float b = num5;
				try
				{
					Camera camera = World.world.camera;
					if (camera != null && camera.orthographicSize > 0f)
					{
						b = Mathf.Min(num5, num5 * camera.orthographicSize / 100f);
					}
				}
				catch
				{
				}
				float num6 = Mathf.Clamp01(((BaseEffect)cloud).alpha / Mathf.Max(0.01f, b));
				if (asset.normal_cloud)
				{
					num6 *= 0.62f;
				}
				if (!(num6 < 0.02f))
				{
					Weather.Storms.Add(new StormCloud
					{
						X = localPosition.x,
						Y = localPosition.y + num,
						HalfX = num2,
						HalfY = num3,
						Color = num4,
						Opacity = num6,
						Drops = precip,
						Glows = asset.draw_light_area,
						Seed = (float)(RuntimeHelpers.GetHashCode(cloud) & 0x3FF) / 97f
					});
					if (asset.draw_light_area)
					{
						float num7 = num6 * (0.35f + 0.65f * _night);
						uint num8 = num4;
						AddLight(localPosition.x, localPosition.y + num, Weather.Base - 1f, Mathf.Clamp(num2 * 1.6f, 8f, 26f), (float)(num8 & 0xFF) / 255f * num7, (float)((num8 >> 8) & 0xFF) / 255f * num7, (float)((num8 >> 16) & 0xFF) / 255f * num7, 0f);
					}
				}
			}
			catch (Exception ex)
			{
				if (_reportedAssets.Add("cloud"))
				{
					Log.Error("cloud skipped: " + ex.Message);
				}
			}
		}

		private static Precip DropsOf(CloudAsset asset)
		{
			switch (asset.drop_id)
			{
			case "rain":
				return Precip.Rain;
			case "acid":
				return Precip.Acid;
			case "lava":
				return Precip.Lava;
			case "fire":
				return Precip.Fire;
			case "snow":
				return Precip.Snow;
			case "ash":
				return Precip.Ash;
			case "magic_rain":
				return Precip.Magic;
			case "rage":
				return Precip.Rage;
			default:
				return Precip.Seeds;
			}
		}

		private void AddBolt(BaseEffect effect, string id, Vector3 pos, TerrainCache terrain, float dist2)
		{
			float num = SkyRange(300f);
			if (!(dist2 > num * num))
			{
				double timestamp_spawned = effect._timestamp_spawned;
				if (!_boltsSeen.TryGetValue(effect, out var value) || value != timestamp_spawned)
				{
					_boltsSeen[effect] = timestamp_spawned;
					float ground = terrain.SampleHeight(pos.x, pos.y);
					float size = (id.Contains("small") ? 0.55f : (id.Contains("medium") ? 0.8f : 1f));
					Weather.Bolts.Add(new Bolt
					{
						X = pos.x,
						Y = pos.y,
						Ground = ground,
						Life = 1.5f,
						Size = size,
						Seed = ((uint)(timestamp_spawned * 1000.0) ^ (uint)effect.GetHashCode())
					});
				}
			}
		}

		private void StepLightning()
		{
			float top = Weather.Base + 1f;
			for (int i = 0; i < Weather.Bolts.Count; i++)
			{
				Bolt value = Weather.Bolts[i];
				bool flag = !value.Struck && value.Age >= 0.09f;
				float num = LightningFx.Draw(_blastSink, BlastParticles, value.X, value.Y, value.Ground, top, value.Size, value.Seed, value.Age, flag);
				float num2 = _blastSink.Distance(value.X, value.Y);
				Weather.Flash = Mathf.Max(Weather.Flash, num * Mathf.Clamp01(1.15f - num2 / 220f));
				if (flag)
				{
					value.Struck = true;
					Weather.Bolts[i] = value;
				}
			}
		}

		private void CollectDrops(MapBox world, ViewState view, TerrainCache terrain, SpriteCache sprites)
		{
			if (!FromSky && Weather.Storms.Count > 0)
			{
				if (_groundOf != terrain)
				{
					_groundOf = terrain;
					_groundAt = terrain.SampleHeight;
				}
				RainFx.Veils(Billboards, Weather, view.X, view.Y, _vfx, _vfy, _clock, _groundAt);
			}
			DropManager drop_manager = world.drop_manager;
			if (drop_manager == null || drop_manager._drops == null)
			{
				return;
			}
			List<Drop> drops = drop_manager._drops;
			for (int i = 0; i < drops.Count; i++)
			{
				Drop drop = drops[i];
				if (drop == null || !drop.active || drop._sprite_renderer == null || (!FromSky && drop._asset != null && drop._asset.id == "snow"))
				{
					continue;
				}
				Vector2 current_position = drop.current_position;
				float num = current_position.x - view.X;
				float num2 = current_position.y - view.Y;
				float num3 = SkyRange((drop._asset != null && IsNukeDrop(drop._asset.id)) ? 170f : 50f);
				if (num * num + num2 * num2 > num3 * num3)
				{
					continue;
				}
				SpriteImage spriteImage;
				try
				{
					spriteImage = sprites.Get(drop._sprite_renderer.sprite);
				}
				catch
				{
					spriteImage = null;
				}
				if (spriteImage == null)
				{
					continue;
				}
				float num4 = terrain.SampleHeight(current_position.x, current_position.y);
				float num5 = Mathf.Max(4f, Weather.Base - num4);
				float z = num4 + Mathf.Clamp01(drop._currentHeightZ / 10f) * num5;
				float num6 = Mathf.Max(0.3f, drop._scale);
				float num7 = spriteImage.UnitWidth * num6 * 1.4f;
				float num8 = spriteImage.UnitHeight * num6 * 1.4f;
				float num9 = Mathf.Sqrt(num * num + num2 * num2);
				if ((drop._asset != null && DrawPowerDrop(drop, drop._asset.id, current_position.x, current_position.y, num4, num9)) || (drop._asset != null && DrawBomb(drop, drop._asset.id, current_position.x, current_position.y, z, num4, num8, num9)))
				{
					continue;
				}
				bool flag = drop._asset != null && WeatherDrops.Contains(drop._asset.id);
				float num10 = Mathf.Max(num7, num8);
				if (FromSky)
				{
					float num11 = SkyLeast(current_position.x, current_position.y, flag ? 2f : 3f);
					float num12 = Mathf.Clamp(num10, num11, flag ? Mathf.Max(num11, 0.3f) : float.MaxValue);
					if (num10 > 0f)
					{
						num7 *= num12 / num10;
						num8 *= num12 / num10;
					}
				}
				else
				{
					if (num9 < 0.8f)
					{
						continue;
					}
					float num13 = (flag ? 0.1f : 0.35f);
					if (num10 > num13)
					{
						num7 *= num13 / num10;
						num8 *= num13 / num10;
					}
				}
				bool emissive = drop._asset != null && (drop._asset.id.Contains("lava") || drop._asset.id.Contains("fire") || drop._asset.id.Contains("napalm") || drop._asset.id.Contains("bomb"));
				Billboards.Add(new Billboard
				{
					X = current_position.x,
					Y = current_position.y,
					Z = z,
					GroundZ = num4,
					Width = num7,
					Height = num8,
					Image = spriteImage,
					Kind = BillboardKind.Facing,
					Emissive = emissive,
					Tag = -1
				});
			}
		}

		private void StepWind(ViewState view, TerrainCache terrain, float dt)
		{
			float num = WorldStep(dt);
			StepTwisters(view, terrain, num);
			float num2 = 0f;
			float num3 = 0f;
			try
			{
				if (Weather.Storms.Count > 0)
				{
					Precip drops;
					float num4 = Weather.Cover(view.X, view.Y, _clock, out drops);
					if (drops != Precip.Seeds && drops != Precip.Sun)
					{
						num2 = Mathf.Clamp01(num4 * 1.4f);
					}
					if (drops != Precip.None && drops != Precip.Seeds && drops != Precip.Sun)
					{
						num3 = Mathf.Clamp01(num4 * 1.6f);
					}
				}
				if (Weather.Global == Precip.Rain || Weather.Global == Precip.Snow || Weather.Global == Precip.Ash)
				{
					num2 = Mathf.Max(num2, 0.35f * Weather.GlobalStrength);
					num3 = Mathf.Max(num3, Weather.GlobalStrength);
				}
			}
			catch
			{
			}
			bool flag = WindGusts || Gusts.Force >= 0f;
			if (flag)
			{
				Gusts.Step(num, Weather.WindX, Weather.WindY, num2);
			}
			else
			{
				Gusts.Strength = 0f;
				Gusts.Fury = 0f;
			}
			TreeSway.Step(GameClock.EffectsPaused ? 0f : num, Weather.WindX, Weather.WindY, Gusts.DirX, Gusts.DirY, Gusts.Strength);
			bool flag2 = _wind.Vortices.Count > 0 && !FromSky;
			if (flag2)
			{
				PullGusts(view);
			}
			_windTime += num;
			bool num5 = !FromSky;
			Weather.SlantX = Gusts.DirX * Gusts.Speed * Gusts.Strength / 15f;
			Weather.SlantY = Gusts.DirY * Gusts.Speed * Gusts.Strength / 15f;
			GrassTufts.WindX = Gusts.DirX;
			GrassTufts.WindY = Gusts.DirY;
			GrassTufts.WindStrength = (num5 ? Gusts.Strength : 0f);
			GrassTufts.WindTime = _windTime;
			int count = _wind.Crowns.Count;
			if (!num5)
			{
				_wind.Clear();
				_wind.Crowns.Clear();
				_ambient.Clear();
				return;
			}
			_wind.LeafAmount = BiomeParticleAmount;
			if (flag || flag2 || BiomeParticleAmount > 0f)
			{
				_wind.Step(num, _clock, Gusts, _blastSink, terrain, view.Fov, _night);
			}
			else
			{
				_wind.Clear();
				_wind.Crowns.Clear();
			}
			StepAmbient(view, terrain, num, num3, count);
		}

		internal void FlockNowForTest(bool gulls)
		{
			_ambient.FlockNow(_blastSink, MathF.PI / 2f, gulls);
		}

		private void StepAmbient(ViewState view, TerrainCache t, float step, float wet, int crowns)
		{
			if (_frame - _habitatFrame > 60 && t != null && t.Width > 0)
			{
				_habitatFrame = _frame;
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				int num5 = (int)view.X;
				int num6 = (int)view.Y;
				for (int i = num6 - 24; i <= num6 + 24; i += 4)
				{
					for (int j = num5 - 24; j <= num5 + 24; j += 4)
					{
						if (j >= 0 && i >= 0 && j < t.Width && i < t.Height)
						{
							int num7 = i * t.Width + j;
							num4++;
							if ((t.Flags[num7] & 1) != 0)
							{
								num++;
							}
							else if (t.Materials[num7] == 1 || t.Materials[num7] == 7)
							{
								num2++;
							}
							else if (t.Materials[num7] == 5)
							{
								num3++;
							}
						}
					}
				}
				_nearSea = num4 > 0 && num > num4 / 6;
				_onSnow = num4 > 0 && num3 > num4 / 3;
				_green = ((num4 > 0) ? Mathf.Clamp01((float)num2 / (float)num4 * 1.3f) : 0f);
			}
			float green = Mathf.Clamp01(_green + (float)crowns / 30f);
			float sunUp = Mathf.Clamp01(1f - _night * 1.6f);
			float golden = ((_night > 0.02f && _night < 0.6f) ? Mathf.Clamp01(1f - Mathf.Abs(_night - 0.22f) / 0.3f) : 0f);
			_ambient.Step(step, _clock, _blastSink, Gusts, view.Fov, sunUp, golden, wet, _onSnow, _nearSea, green);
		}

		private void WindTree(MeshInstance inst, MeshModel model, Vector2 p, float dist)
		{
			if (FromSky || dist > 14f || model == null)
			{
				return;
			}
			bool flag = _wind.Vortices.Count > 0;
			float z = inst.Basis.C2.Z;
			float num = model.Max.Z * z;
			if (!(num < 0.8f))
			{
				if (_wind.Crowns.Count < 48)
				{
					float radius = Mathf.Max(0.4f, (model.Max.X - model.Min.X) * z * 0.35f);
					_wind.Crowns.Add(new WindFx.Crown
					{
						X = p.x,
						Y = p.y,
						Z = inst.Position.Z + num * 0.72f,
						Radius = radius,
						Leaf = LeafColour(inst, model),
						Deciduous = SeasonLook.Deciduous(model.Name),
						Glow = Glows(model),
						Snowy = (inst.Snow > 0.25f)
					});
				}
				if (flag && _bentFrame != _frame)
				{
					_bentFrame = _frame;
					_bent = 0;
				}
				if (flag && _bent < 120 && _wind.TwisterWind(p.x, p.y, out var dirX, out var dirY, out var strength))
				{
					_bent++;
					WindFx.Bend(inst, p.x, p.y, dirX, dirY, Mathf.Max(strength, WindGusts ? Gusts.Strength : 0f), _windTime, _clock);
				}
				else if (WindGusts)
				{
					WindFx.Bend(inst, p.x, p.y, Gusts, _windTime, _clock);
				}
			}
		}

		private void GiantCrown(MeshInstance inst, MeshModel model, Vector2 p, float dist)
		{
			if (!FromSky && !(dist > 48f) && model != null && _wind.Crowns.Count < 48)
			{
				float z = inst.Basis.C2.Z;
				float num = model.Max.Z * z;
				float num2 = Mathf.Max(1f, (model.Max.X - model.Min.X) * Mathf.Sqrt(Vec3.Dot(inst.Basis.C0, inst.Basis.C0)) * 0.3f);
				uint leaf = LeafColour(inst, model);
				bool glow = Glows(model);
				bool snowy = inst.Snow > 0.25f;
				for (int i = 0; i < 4; i++)
				{
					float f = (float)i * 1.571f + 0.4f;
					_wind.Crowns.Add(new WindFx.Crown
					{
						X = p.x + Mathf.Cos(f) * num2 * 0.6f,
						Y = p.y + Mathf.Sin(f) * num2 * 0.6f,
						Z = inst.Position.Z + num * 0.62f,
						Radius = num2 * 0.7f,
						Leaf = leaf,
						Deciduous = false,
						Glow = glow,
						Snowy = snowy
					});
				}
			}
		}

		private static bool Glows(MeshModel model)
		{
			if (_glows.TryGetValue(model, out var value))
			{
				return value;
			}
			int num = model.SlotIndex("leaf_mid");
			value = num >= 0 && num < model.SlotFlags.Length && (model.SlotFlags[num] & 0x20) != 0;
			if (_glows.Count > 512)
			{
				_glows.Clear();
			}
			_glows[model] = value;
			return value;
		}

		private static uint LeafColour(MeshInstance inst, MeshModel model)
		{
			int num = model.SlotIndex("leaf_mid");
			if (num < 0)
			{
				num = model.SlotIndex("leaf");
			}
			if (num < 0)
			{
				num = model.SlotIndex("leaves");
			}
			if (num < 0)
			{
				return TerrainCache.Rgb(86, 140, 60);
			}
			uint num2 = ((inst.Palette != null && num < inst.Palette.Length) ? inst.Palette[num] : 0u);
			if (num2 == 0 && model.SlotColor != null && num < model.SlotColor.Length)
			{
				num2 = model.SlotColor[num];
			}
			if (num2 == 0)
			{
				return TerrainCache.Rgb(86, 140, 60);
			}
			return num2;
		}

		private static bool WaypointSprite(string id)
		{
			return id?.StartsWith("fx_waypoint_", StringComparison.Ordinal) ?? false;
		}

		private void NoteWaypointCall(BaseEffect effect)
		{
			Vector3 position = effect.transform.position;
			foreach (WonderPulse wonderPulse in _wonderPulses)
			{
				if ((wonderPulse.X - position.x) * (wonderPulse.X - position.x) + (wonderPulse.Y - position.y) * (wonderPulse.Y - position.y) < 16f && _clock - wonderPulse.At < 3f)
				{
					return;
				}
			}
			_wonderPulses.Add(new WonderPulse
			{
				X = position.x,
				Y = position.y,
				At = _clock
			});
			WonderPulsesSeen++;
			if (_wonderPulses.Count > 16)
			{
				_wonderPulses.RemoveAt(0);
			}
		}

		private void WonderEffects(NearBuilding nb, Building b, MeshInstance inst, MeshModel model, float dist2, bool seen, float ground)
		{
			WonderLook.Fx fx = WonderLook.Of(model);
			if (fx == null || !fx.Any)
			{
				return;
			}
			if (!_wonderStates.TryGetValue(b, out var value))
			{
				if (_wonderStates.Count > 48)
				{
					_wonderStates.Clear();
				}
				value = new WonderLook.State
				{
					Seed = (uint)b.GetHashCode()
				};
				_wonderStates[b] = value;
			}
			float x = inst.Position.X;
			float y = inst.Position.Y;
			for (int num = _wonderPulses.Count - 1; num >= 0; num--)
			{
				WonderPulse wonderPulse = _wonderPulses[num];
				if (_clock - wonderPulse.At > 3.2f)
				{
					_wonderPulses.RemoveAt(num);
				}
				else
				{
					float num2 = Mathf.Max(3f, nb.Width * 0.75f);
					if (!((wonderPulse.X - x) * (wonderPulse.X - x) + (wonderPulse.Y - y) * (wonderPulse.Y - y) > num2 * num2) && wonderPulse.At > value.PulseAt + 0.5f)
					{
						value.PulseAt = wonderPulse.At;
						if (!wonderPulse.Heard && dist2 < 3600f)
						{
							WonderPulses.Add(new WonderSpot
							{
								Family = nb.MeshFamily,
								At = new Vector3(x, y, model.Max.Z * WonderLook.ScaleOf(inst) * 0.5f)
							});
							if (WonderPulses.Count > 8)
							{
								WonderPulses.RemoveAt(0);
							}
						}
						wonderPulse.Heard = true;
					}
				}
			}
			if (dist2 < 1600f)
			{
				if (WonderSpotFrame != Time.frameCount)
				{
					WonderSpots.Clear();
					WonderSpotFrame = Time.frameCount;
				}
				if (WonderSpots.Count < 16)
				{
					WonderSpots.Add(new WonderSpot
					{
						Family = nb.MeshFamily,
						At = new Vector3(x, y, model.Max.Z * WonderLook.ScaleOf(inst) * 0.45f)
					});
				}
			}
			if (!seen)
			{
				return;
			}
			if (fx.Posed || fx.Coloured || fx.HasBurst)
			{
				inst.Static = false;
			}
			WonderLook.Apply(inst, value, _clock, _night, new Vec3(_eyeX, _eyeY, _eyeZ));
			WondersPosed++;
			_wonderLights.Clear();
			WonderLook.Emit(inst, value, _clock, _night, ground, dist2 < 2500f, Billboards, _wonderLights);
			if (!(dist2 < 2304f) && !(_clock - value.PulseAt < 2.2f))
			{
				return;
			}
			foreach (PointLight wonderLight in _wonderLights)
			{
				AddLight(wonderLight.X, wonderLight.Y, wonderLight.Z, wonderLight.Radius, wonderLight.R, wonderLight.G, wonderLight.B, wonderLight.Glow);
			}
		}

		internal bool TownsInView(float dx, float dy, float radius)
		{
			return InView(dx, dy, radius);
		}

		private void BeginTowns(ViewState view, TerrainCache terrain, SpriteCache sprites, Actor host, float dt)
		{
			TownScene townScene = _townScene;
			townScene.Collector = this;
			townScene.View = view;
			townScene.Terrain = terrain;
			townScene.Sprites = sprites;
			townScene.Host = host;
			townScene.Dt = dt;
			townScene.Clock = _clock;
			townScene.Night = _night;
			townScene.Frame = _frame;
			townScene.FromSky = FromSky;
		}

		private void CollectTowns()
		{
			Hub.Collect(_townScene);
		}

		private void TownsBuilding(Building b, MeshInstance inst, MeshModel full, MeshModel model, string family, float x, float y, float ground, float heading, float scale, bool mirrored, bool lit, bool ruin, bool seen, float dist)
		{
			TownBuilding townBuilding = _townBuilding;
			townBuilding.Scene = _townScene;
			townBuilding.B = b;
			townBuilding.Inst = inst;
			townBuilding.Full = full;
			townBuilding.Model = model;
			townBuilding.Family = family;
			townBuilding.X = x;
			townBuilding.Y = y;
			townBuilding.Ground = ground;
			townBuilding.Heading = heading;
			townBuilding.Scale = scale;
			townBuilding.Mirrored = mirrored;
			townBuilding.Lit = lit;
			townBuilding.Ruin = ruin;
			townBuilding.Seen = seen;
			townBuilding.Dist = dist;
			Hub.Building(townBuilding);
		}

		internal float TwisterNear(float x, float y, float reach)
		{
			float num = reach;
			foreach (KeyValuePair<TornadoEffect, TwisterState> twister in _twisters)
			{
				float num2 = Vector2.Distance(twister.Value.Foot, new Vector2(x, y));
				if (num2 < num && !twister.Value.Dying)
				{
					num = num2;
				}
			}
			return num;
		}
	}
}
