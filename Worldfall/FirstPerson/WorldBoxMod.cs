using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FMOD;
using FMODUnity;
using FirstPerson.Core;
using FirstPerson.Gpu;
using FirstPerson.Net;
using FirstPerson.OneWorld;
using FirstPerson.Pad;
using FirstPerson.Towns;
using FirstPerson.Vr;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace FirstPerson
{
	public class WorldBoxMod : MonoBehaviour
	{
		private enum Act
		{
			E,
			ShiftE,
			Insult,
			Steal,
			Star,
			TakeOver,
			GuardKill,
			GuardExecute
		}

		internal struct CompassMark
		{
			public string Key;

			public Vector2 At;

			public Actor Actor;

			public string Name;

			public Color Color;

			public float Distance;
		}

		private enum Dive
		{
			None,
			In,
			Out
		}

		private struct GodGpuFrame
		{
			public int Number;

			public ViewState Camera;

			public int Width;

			public int Height;

			public float Ground;

			public Vector2 At;

			public SightCut Cut;
		}

		private struct SceneFrame
		{
			public bool Set;

			public ViewState Camera;

			public Vector2 At;

			public SightCut Cut;
		}

		internal enum Tool
		{
			Torch,
			Compass,
			Eat,
			Bandage,
			PutAway
		}

		private struct VrGodRoom
		{
			public bool Valid;

			public Vector3 Seat;

			public Vector3 SeatFromMap;

			public float Yaw;

			public float Scale;

			public float Tilt;

			public float Behind;
		}

		private struct VrPin
		{
			public bool Set;

			public int Kind;

			public float Yaw;

			public float Pitch;

			public Xr.Vec3 From;

			public float AwaySince;

			public float LastAt;

			public bool Gliding;

			public bool Lift;
		}

		private struct VrPanelPlace
		{
			public bool Up;

			public Xr.Pose Pose;

			public float Wide;

			public float High;

			public bool Solid;

			public bool OnHead;
		}

		private sealed class SwingState
		{
			public HandStrike Was;

			public HandStrike Now;

			public bool On;

			public float Slow;

			public readonly HashSet<object> Hit = new HashSet<object>();
		}

		private readonly PromptAsk _townsAsk = new PromptAsk();

		private bool _townsReady;

		private const KeyCode AbilityKey = KeyCode.X;

		internal Ability Ability;

		internal bool AbilityAiming;

		internal float SinceAbility = 99f;

		internal Ability LastAbility;

		internal Vector2 AbilityAimAt;

		private bool _abilityDown;

		private bool _abilityUp;

		private bool _abilityHeldForTest;

		private Ability _abilityPending;

		private bool _abilityCooling;

		private Actor _abilityFor;

		private int _abilityChoice;

		private float _abilityRecheck;

		private int _abilityUses;

		private static readonly HashSet<string> AbilityTold = new HashSet<string>();

		private static readonly Dictionary<uint, VoxelShape> MarkShapes = new Dictionary<uint, VoxelShape>();

		private static bool _zoneKeysGuarded;

		internal readonly RadialWheel ActionWheel = new RadialWheel();

		private Actor _guardTarget;

		private readonly Act[] _acts = new Act[8];

		private float _actDownAt = -1f;

		private int _actDownFrame;

		private Actor _actTarget;

		private bool _actSwallow;

		private const float ActHold = 0.25f;

		private bool _actPicked;

		internal static bool BesideAllowed = true;

		private Task _beside;

		private Exception _besideLandError;

		private bool _besideLandErrorLogged;

		private int _besideFailures;

		private float _besideRetryAt;

		private double _besideMainMs;

		private ViewState _besideCamera;

		private int _besideWidth;

		private int _besideHeight;

		private bool _besideRoom;

		private bool _besideScene;

		private bool _besideHoles;

		private List<BaseSimObject> _besideTagged;

		private Actor _besideHost;

		private float _besideBody;

		private float _besideFeet;

		private double _besideCollectMs;

		private double _besideLandMs;

		private double _besideDrawMs;

		private readonly List<Billboard> _besideBillboards = new List<Billboard>(1024);

		private readonly List<VoxelObject> _besideVoxels = new List<VoxelObject>(1024);

		private FrameProjection _shown;

		private ViewState _eyeNow;

		internal int FramesShown;

		private bool _wasInRoom;

		private const float BedrollSafe = 14f;

		internal static int BedrollNights;

		private const KeyCode BinocularsKey = KeyCode.Z;

		internal readonly Binoculars Binoculars = new Binoculars();

		internal float BinocularsRange = -1f;

		private int _binocularsLoweredFrame = -10;

		private Actor _binocularsHost;

		private readonly BlasterControl _blaster = new BlasterControl();

		internal readonly BoatCabin Cabin = new BoatCabin();

		private float _helmLastHeading;

		private float _helmBody = 0.6f;

		private bool _helmFresh = true;

		private float _ashoreCheckAt;

		private bool _ashoreNear;

		private bool _bumped;

		private float _raftCheckAt;

		private bool _raftWater;

		private float _holdAt;

		internal bool BoatHeldForTest;

		private float _inBonfire;

		private const float CatchAfter = 0.12f;

		internal readonly Conversation Converse = new Conversation();

		private bool _frozeForTalk;

		private int _convClosedFrame = -10;

		internal static float TalkWheelForTest;

		private float _talkWait;

		private bool _civilReady;

		private bool _audience;

		private int _boardOpenedFrame = -10;

		internal readonly List<TreeNode> Heirs = new List<TreeNode>();

		private long _heirsOf;

		private int _deadMoney;

		private Actor _deadBody;

		private readonly Dictionary<HotkeyAsset, HotkeyAction> _pressWraps = new Dictionary<HotkeyAsset, HotkeyAction>();

		private readonly Dictionary<HotkeyAsset, HotkeyAction> _holdWraps = new Dictionary<HotkeyAsset, HotkeyAction>();

		private int _muteChecked = -100;

		private bool _muteFailed;

		private const float ClickGraceSeconds = 0.5f;

		private bool _clickUiWasOpen;

		private bool _clickHeldOver;

		private float _clickUiClosedAt = -10f;

		internal readonly CompassState Compass = new CompassState();

		private readonly List<CompassMark> _compassMarks = new List<CompassMark>();

		private string _compassPick;

		private string _compassAuto;

		private float _compassGatherAt = -10f;

		private int _compassShutFrame = -10;

		private Actor _compassHost;

		internal static bool CompassKeyForTest;

		internal static bool CompassClickForTest;

		private float _guardLoadedAt = -1f;

		private bool _guardStartDone;

		private bool _gpuRoomsNoted;

		private int _gpuRoomFrame = -1000;

		private static readonly float[] CrowdLines = new float[4] { 0f, 6f, 12f, 24f };

		internal static float CrowdLineForTest = -1f;

		internal const float GodLine = 8f;

		internal static bool GodLineOff;

		internal const int CpuMostWidth = 640;

		internal static readonly float[] DrawPresets = new float[5] { 180f, 270f, 390f, 600f, 1200f };

		private static readonly string[] PaletteNames = new string[2] { "Palette filter off", "Palette filter on" };

		private const float DeathScreenSeconds = 3.2f;

		private readonly WorldSampler _sampler = new WorldSampler();

		private Func<float, float, float> _cacheHeight;

		private readonly SpriteCache _sprites = new SpriteCache
		{
			AsyncReads = true
		};

		private readonly SceneCollector _scene = new SceneCollector();

		private readonly Hands _hands = new Hands();

		private readonly Stopwatch _frameClock = new Stopwatch();

		internal readonly FrameRenderer Renderer = new FrameRenderer();

		internal readonly CombatFeedback Feedback = new CombatFeedback();

		internal readonly Interactions Talk = new Interactions();

		internal readonly HouseInterior Interior = new HouseInterior();

		internal readonly Work Work = new Work();

		internal readonly Crafting Craft = new Crafting();

		internal readonly Royal Royal = new Royal();

		private readonly FirstPersonAudio _audio = new FirstPersonAudio();

		private readonly Habitat _habitat = new Habitat();

		internal Settings Settings;

		internal string Prompt;

		internal string DebugPrompt;

		private Hud _hud;

		private readonly List<VoxelObject> _roomVoxels = new List<VoxelObject>(64);

		private static readonly List<Billboard> NoBillboards = new List<Billboard>();

		private readonly DayCycle _day = new DayCycle();

		private bool _relightNow;

		private float _fovKick;

		private float _landDip;

		private float _landVel;

		private float _groundZ;

		private float _groundVel;

		internal float Lean;

		private float _lastPace;

		private float _plantNod;

		private bool _planted;

		private float _crouch;

		private float _standTry = -1f;

		private Vector2 _eyeOffset;

		private Vector2 _headOffset;

		private readonly Gait _gait = new Gait();

		private readonly PerfLog _perf = new PerfLog();

		private bool _wasAirborne;

		private int _landingsSeen;

		private Interior _litRoom;

		internal Texture2D Frame;

		internal SpriteImage HostImage;

		internal float BobPhase;

		internal float Stride;

		internal bool Moving;

		internal bool Swimming;

		internal bool ForceSwim;

		internal bool DebugEyeSet;

		internal Vector2 DebugEye;

		internal float DebugEyeHeight;

		internal float HeldTime = -1f;

		internal bool NoFirefliesForTest;

		internal float SwimPhase;

		internal float HintUntil;

		internal bool Holstered;

		private int _unholsterFrame = -10;

		internal string ToastText;

		private float _roomWeatherAt;

		private Vector2 _wakeFrom;

		private Vector2 _talkFrom;

		private Action<Vector2, float> _wakeSound;

		private Func<Actor, float> _talkDistance;

		internal float ToastUntil;

		internal float DeathTimer = -1f;

		internal string DeathName;

		private bool _active;

		private bool _viewEnabled = true;

		private Actor _host;

		private ViewState _view;

		private float _pitchDegrees;

		private float _swimSurface;

		private float _eyeZ;

		private Vector2 _lastPos;

		private float _renderMs;

		private BaseSimObject _picked;

		private bool _settingsDirty;

		internal float FrameMs;

		private float _outsideYaw;

		private Actor _wakeAboard;

		private Building _wakeIn;

		private float _wakeSince;

		private const float ObeliskReach = 3f;

		private Building _obelisk;

		private int _obeliskClosedFrame = -1;

		private int _savedCullingMask = -1;

		private bool _menuWasOpen;

		private float _overcast;

		private float _rain;

		private uint _overcastColor = TerrainCache.Rgb(110, 120, 140);

		private readonly List<KeyValuePair<float, Color>> _marks = new List<KeyValuePair<float, Color>>();

		private readonly Queue<string> _toastNext = new Queue<string>();

		private int _toastFrame = -1;

		private Dive _dive;

		private float _diveT;

		private float _diveLength;

		private float _diveZoom;

		private Vector2 _diveMapFrom;

		private float _diveTurn;

		private bool _diveHands;

		private bool _divePrewarm;

		private float _diveClock;

		private ViewState _diveEye;

		private Vector2 _diveFrom;

		private float _diveGround;

		private Actor _diveBody;

		private bool _noDive;

		private bool _diveFromDeath;

		private float _diveViewAlpha = 1f;

		private float _diveHudAlpha = 1f;

		private const float WallsSeenFromHigh = 8f;

		internal readonly HouseInterior DoorRoom = new HouseInterior();

		private Building _doorHouse;

		private Task<Interior> _doorBuild;

		private Building _doorBuildFor;

		private HouseWorld.Placement _doorPlace;

		private SpriteImage _doorSprite;

		private Building _doorFailed;

		private const float DoorReady = 4f;

		internal static int DoorStepsIn;

		internal static int DoorStepsOut;

		private float _doorRebuiltAt = -1f;

		private Building _doorwayNear;

		internal Func<Building, Actor, bool> MayWalkInAt;

		private float _doorPress;

		private float _doorPressOff;

		private RenderTexture _doorwayPicture;

		private MeshModel _doorwayPane;

		private readonly MeshInstance _doorwayPaneAt = new MeshInstance
		{
			NoShadow = true,
			Tag = -1
		};

		private readonly List<VoxelObject> _doorVoxels = new List<VoxelObject>(16);

		private readonly List<MeshInstance> _keptMeshes = new List<MeshInstance>(512);

		private readonly List<MeshInstance> _keptOverlay = new List<MeshInstance>(32);

		private readonly List<PointLight> _keptLights = new List<PointLight>(32);

		private readonly List<LightWindow> _keptWindows = new List<LightWindow>(8);

		internal static int DoorwayFrames;

		internal readonly FamilyMap FamilyMap = new FamilyMap();

		private readonly TownPaths _paths = new TownPaths();

		private readonly BowControl _bow = new BowControl();

		private readonly MeleeControl _melee = new MeleeControl();

		private readonly GuardControl _guard = new GuardControl();

		internal readonly WarMap WarMap = new WarMap();

		internal readonly Favorites Favs = new Favorites();

		internal Actor FavoriteTarget;

		private float _danger;

		private float _dangerTarget;

		private float _dangerTimer;

		private bool _wasDead;

		private readonly Stopwatch _featClock = new Stopwatch();

		private float _featLog;

		private int _featFrames;

		private double _featSum;

		private double _featPathsMax;

		private int _hitsSeen = -1;

		private bool _foughtRecently;

		internal int FlameSpritesForTest;

		internal int FlameOthersForTest;

		internal int FlameLightsForTest;

		private float _standLift;

		private float _floorZ = float.NaN;

		private float _takeoffZ = float.NaN;

		private bool _landedOn;

		private byte _floorWhat;

		private float _lastFeet = float.NaN;

		private Vector2 _lastFeetAt;

		private bool _lastFeetAir = true;

		internal int FallsForTest;

		private const float BenchReach = 1.5f;

		private const float BenchOver = 0.6f;

		private const float BenchFacing = 40f;

		private const float ForgeRest = 1.2f;

		private float _forgeRestUntil;

		private int _forgeSwings;

		private bool _forgeHands;

		internal static bool BusyForTest;

		private const float FlyBaseSpeed = 10f;

		private const float FlyBoost = 3f;

		private const float FlyRadius = 0.25f;

		private const float FlyClear = 0.35f;

		private const float FlyHold = 0.5f;

		private const float FlyEaseSeconds = 0.4f;

		private const float FlyLeaveSeconds = 0.5f;

		private bool _fly;

		private Vector3 _flyPos;

		private float _flyYaw;

		private float _flyPitch;

		private Vector3 _flyVel;

		private float _flyEase;

		private float _flyShearFrom;

		private float _flyFovFrom;

		private float _flyAlpha;

		private bool _flyCursor;

		private bool _flyLeaving;

		private float _flyLeaveClock;

		private ViewState _flyLeaveFrom;

		private ViewState _flyLeaveTo;

		private Vector2 _flyLeaveAt;

		private float _flyLeaveZoom;

		private float _flyLeaveGround;

		private float _flyLeaveTurn;

		private float _fHeld = -1f;

		private bool _flyLift;

		private float _flyLiftAt;

		private bool _diveFromFly;

		private bool _flyDirty;

		private float _flySpeedUntil;

		private float _flyHintUntil;

		private float _flyDotsTimer;

		private Vector2 _flyMapAt;

		private bool _flyMapSet;

		internal readonly List<KeyValuePair<Vector2, Color>> FlyDots = new List<KeyValuePair<Vector2, Color>>(64);

		internal Vector3? FlyKeysForTest;

		internal Vector2 FlyLookForTest;

		private HotkeyAction _gameFollow;

		private HotkeyAction _follow;

		private bool _followFailed;

		private readonly GodGpuFrame[] _godGpuFrames = new GodGpuFrame[8];

		private int _godGpuApplied = -1;

		private double _gpSubmit;

		internal static long GodCardCycles;

		private const float GodTurnSpeed = 1.75f;

		private const float GodTipSpeed = 1.1f;

		private const float GodTurnRamp = 0.14f;

		private const float GodFadeSeconds = 0.3f;

		private float _godTurn;

		private float _godTurnRate;

		private float _godTip;

		private float _godTipRate;

		private bool _godHoming;

		private float _godHomeClock;

		private float _godHomeSeconds;

		private float _godHomeTurn;

		private float _godHomeTurnRate;

		private float _godHomeTip;

		private float _godHomeTipRate;

		private float _godFade;

		private float _godHeld;

		private bool _godWasOn;

		private bool _godSwitched;

		private const int GodLayer = 30;

		internal static long GodSceneCycles;

		internal static float GodTimeForTest = -1f;

		private GameObject _godScreen;

		private Material _godMaterial;

		private int _godSavedMask = -1;

		private Camera _godCam;

		private float _godGround = float.NaN;

		private float _godAlpha;

		private bool _godProbed;

		private float _godRetryAt;

		private int _godFailures;

		private string _godToast;

		private float _godToastUntil;

		private Task _godTask;

		private ViewState _godTaskCamera;

		private int _godTaskWidth;

		private int _godTaskHeight;

		private float _godTaskGround;

		private float _godTaskZoom;

		private Vector2 _godTaskAt;

		private bool _godTaskGpu;

		private double _godTaskMs;

		private double _godTaskLandMs;

		private Exception _godLandError;

		private bool _godLandErrorLogged;

		private float[] _godDepth = new float[0];

		private double _gpFrame;

		private double _gpLand;

		private double _gpLandBeside;

		private double _gpScene;

		private double _gpWait;

		private double _gpUpload;

		private double _gpDraw;

		private double _gpTerrain;

		private double _gpObjects;

		private double _gpModels;

		private double _gpPost;

		private double _gpTris;

		private int _gpFrames;

		private float _gpSince;

		private SightCut _godCut;

		private bool _diveFromGod;

		private bool _godTaskRoomForCursor;

		private float _godTaskBehind = 2f;

		private Vector2 _diveGodFrom;

		private float _diveGodGround;

		internal bool GodCutOffForTest;

		private readonly List<KeyValuePair<Camera, int>> _godMuted = new List<KeyValuePair<Camera, int>>();

		private Mesh _godScreenMesh;

		private bool _diveGodOffsetSet;

		private Vector3 _diveGodOffset;

		private Vector3 _diveGodLook;

		private float _diveGodK0;

		private float _diveCutFrom;

		private const float MagnetZoom = 16f;

		private const float CloseZoomMin = 0.9f;

		private const float WheelStep = 0.8f;

		private Actor _godLock;

		private long _godLockId;

		private float _godPull;

		private float _godLockTime = -10f;

		private Vector2 _godFollow;

		private Vector2 _godFollowVel;

		private float _godClose = 10f;

		private float _godCloseTarget = 10f;

		private float _godZoomedInAt = -10f;

		private Vector3 _godCamWas;

		private bool _godCamSet;

		private float _godEyeZ;

		private float _godLockGround;

		private ViewState _godShown;

		private float _godSnapAt = -10f;

		private float _godAtMinSince = -1f;

		private float _godLift;

		private Vector3 _godLook;

		private GpuRenderer _gpu;

		private bool _gpuShown;

		private float _gpuRetryAt;

		private string _gpuLastError;

		private const int PickRing = 15;

		private readonly List<BaseSimObject>[] _gpuTagged = NewPickRing();

		private int _gpuFails;

		private int _gpuProven;

		private bool _frameHoles;

		private float _gpuPerfSince;

		private double _gpuCollect;

		private double _gpuDraw;

		private int _gpuFrames;

		internal static bool GpuRooms = true;

		internal static bool DoorwayAside = true;

		private Interior _gpuRoom;

		private Actor _takeShip;

		private bool _leaveShip;

		private float _shipShoreAt;

		private bool _shipShore;

		private Actor _builtShip;

		private Actor _builtShipBy;

		private float _builtShipFor;

		private bool _homeDoorsWired;

		private float _roomDoorKey = -1f;

		private int _roomDoorFrame = -10;

		private bool _roomDoorPropped;

		private string _hotbarFood;

		internal static int HotbarKeyForTest = -1;

		internal static int HotbarKeyFrameForTest = -1;

		internal static int HotbarClickForTest = -1;

		internal static float HotbarWheelForTest;

		private float _eatAt = -100f;

		private Carried _eaten;

		internal readonly CharacterView YouView = new CharacterView();

		internal static int PressForTest = -1;

		private const float InventoryHold = 0.25f;

		private float _tabDownAt;

		private int _tabDownFrame;

		private bool _tabHeld;

		internal static bool HoldForTest;

		private float _strandedSince = -1f;

		private float _strayFrozenSince = -1f;

		private const int KeyTalk = 0;

		private const int KeyBoard = 1;

		private const int KeyNaming = 2;

		private const int KeyTowns = 3;

		private readonly int[] _keysShownFrame = new int[4] { -100, -100, -100, -100 };

		private float _keysHeldSince = -1f;

		private float _fieldSince = -1f;

		private float _heldBackSince = -1f;

		private float _heldBack2Since = -1f;

		private float _escAt = -10f;

		private int _escCount;

		private string _escHolders;

		private bool _dropKeyboardControl;

		private float _unfocusedSince = -1f;

		private bool _unfocusedTold;

		private int _unfocusedLogs;

		private int _modifierEscLogs;

		internal static bool LandBesideOff;

		private Task _landBeside;

		private Exception _landBesideError;

		private bool _landBesideErrorLogged;

		private bool _landBesideDone;

		private double _landBesideMs;

		private double _landBesideWaitMs;

		internal static bool LandBesideForTest;

		private bool _leaping;

		private float _leapT;

		private float _leapSeconds;

		private ViewState _leapFrom;

		private const float LootLookDown = -20f;

		private readonly Dictionary<WorldTimeScaleAsset, bool> _renderSkipWas = new Dictionary<WorldTimeScaleAsset, bool>();

		private bool _mapHeld;

		internal static bool MapHoldOff;

		internal static bool MapHoldFirstPersonOff;

		private MapTabs.Page? _mapPageWanted;

		private MapTabs.Page? _lastPageShown;

		internal readonly MineInterior Mine = new MineInterior();

		private bool _packSteamGone;

		private PadControls.Mode _padMode;

		private int _padScroll;

		private bool _padHidCursor;

		private bool _padLogged;

		private Vector2Int _padHopLater;

		private float _padHopUntil;

		private Texture2D _padArrow;

		private static readonly string[] PadArrowRows = new string[16]
		{
			"#..........", "##.........", "#o#........", "#oo#.......", "#ooo#......", "#oooo#.....", "#ooooo#....", "#oooooo#...", "#ooooooo#..", "#oooooooo#.",
			"#oooo#####.", "#oo#o#.....", "#o#.#o#....", "##..#o#....", "#....#o#...", ".....###..."
		};

		private bool _phoneChecked;

		private bool _touchScheme;

		private bool _schemeChosen;

		private float _lastTouchAt = -99f;

		private bool _landscapeHeld;

		private bool _touchToldOnce;

		private bool _keysToldOnce;

		private bool _gameTouchHidden;

		private bool _touchBlocksMap;

		private const KeyCode MinimapKey = KeyCode.N;

		private const KeyCode PingKey = KeyCode.C;

		private const float HoldSeconds = 0.25f;

		internal readonly Pings Pings = new Pings();

		internal readonly Minimap Minimap = new Minimap();

		internal Explored.Map HostMap;

		private float _mapKeyAt = -1f;

		private bool _bigMap;

		private bool _pingDown;

		private float _pingDownAt;

		private bool _wheel;

		private Vector2 _wheelAim;

		private int _wheelChoice = -1;

		private float _wheelOpenedAt;

		private bool _aimOk;

		private bool _aimFar;

		private Vector3 _aimAt;

		private Actor _aimActor;

		private BaseSimObject _aimOn;

		internal readonly List<KeyValuePair<Vector2, Color>> MinimapDots = new List<KeyValuePair<Vector2, Color>>(64);

		private float _dotsTimer;

		private float _revealTimer;

		private Actor _pingHost;

		internal Ping LastPing;

		internal int LastRallied;

		private bool _pingSwallow;

		private int _pingsFrame = -10;

		private const float FavoritePingLife = 600f;

		private const KeyCode PoseKey = KeyCode.P;

		internal static readonly RestStyle[] PoseWheel = new RestStyle[5]
		{
			RestStyle.Ground,
			RestStyle.CrossLegged,
			RestStyle.SitBack,
			RestStyle.KneelBoth,
			RestStyle.Kneel
		};

		private RestStyle _pose;

		private float _poseAmount;

		private bool _poseUp;

		private RestSeat _poseSeat;

		private bool _poseIndoors;

		private float _poseFrom;

		private float _poseSince;

		private float _poseView;

		private bool _poseThird;

		private float _poseRested;

		private float _poseTogether;

		private Actor _poseBeside;

		private bool _poseNoticed;

		private bool _poseHolstered;

		private Actor _poseHost;

		private int _poseHurts;

		private RestStyle _poseNext;

		private RestSeat _poseNextSeat;

		private float _poseKeyAt = -1f;

		private bool _poseWheel;

		private bool _poseSwallow;

		private Vector2 _poseWheelAim;

		private int _poseWheelChoice = -1;

		private float _poseWheelOpenedAt;

		private static readonly Dictionary<long, float> _knelt = new Dictionary<long, float>();

		private static readonly Dictionary<long, float> _satWith = new Dictionary<long, float>();

		internal static bool PoseTapForTest;

		internal static bool PoseWalkForTest;

		internal static bool PoseSitForTest;

		internal Vector3 PoseCameraForTest;

		private static PersonRig _fallbackRig;

		internal bool HoldDetailForTest;

		private const float HeadRoom = 0.05f;

		internal float RoomBodyForTest;

		private float _roomFall;

		private const float RoomFallPull = 9f;

		internal static float WindowReachTiles = 64f;

		internal static int WindowEvery = 1;

		private int _windowSkipped;

		private int _windowLastFrame = -10;

		private float _windowDt;

		private uint[] _backdrop;

		private uint[] _clearBackdrop;

		private readonly List<LightWindow> _roomWindows = new List<LightWindow>();

		private readonly List<PointLight> _roomLights = new List<PointLight>();

		private readonly List<MeshInstance> _roomMeshes = new List<MeshInstance>();

		private readonly List<MeshInstance> _roomHands = new List<MeshInstance>();

		private readonly List<Billboard> _worldBillboards = new List<Billboard>(1024);

		private readonly List<VoxelObject> _worldVoxels = new List<VoxelObject>(1024);

		private bool _roomTalk;

		private RenderTexture _cabinWorld;

		private Building _royalVisited;

		private float _royalVisitAt = -999f;

		internal int RoyalWelcomes;

		private float _celebrateUntil = -1f;

		private bool _runAsked;

		private bool _runLater;

		private bool _runBodyHeld;

		private static bool _splitScreenLogged;

		private static int _splitWindows;

		private static long _glided0;

		private static long _offscreen0;

		private static long _kept0;

		private static long _cells0;

		private static long _coasted0;

		internal readonly SceneView Scene = new SceneView();

		private SceneFrame _sceneDrawn;

		private SceneFrame _sceneShown;

		private int _sceneEndedFrame = -1;

		private Actor _sceneHost;

		private int _sceneHurts;

		private float _sceneHitAge;

		private float _sceneHealth;

		internal float SceneHurtAt = -10f;

		private const float SceneBigHurt = 0.1f;

		private const float SceneLowHealth = 0.25f;

		private bool _sceneWasOn;

		private float _scenePerfSince;

		private double _sceneMs;

		private int _sceneFrames;

		private const float SceneEar = 3f;

		internal Vector2 SceneEarForTest;

		private readonly List<Vector2> _battlesSeen = new List<Vector2>();

		internal float SeaStormForTest = -1f;

		private float _seaStorm;

		private const float SeaBuild = 25f;

		internal readonly SoldierMap SoldierMap = new SoldierMap();

		internal Order Orders;

		internal Commission Charge;

		private Actor _serviceFor;

		private float _serviceErrorAt = -99f;

		private static readonly Color CaptainBlue = new Color(0.45f, 0.75f, 1f);

		private readonly SettingsMenu _settingsMenu = new SettingsMenu();

		private GameObject _menuBlocker;

		private HotkeyAction _gameEscape;

		private HotkeyAction _menuEscape;

		private bool _escapeFailed;

		private bool _menuHinted;

		private float _menuHintAt = -1f;

		private bool? _selfTesting;

		private bool _leaveBodyAsked;

		private bool _helpAsked;

		private HotkeyAction _gameLetGo;

		private HotkeyAction _letGo;

		private bool _letGoFailed;

		private const float DefaultFadeStart = 194.40001f;

		private float _creatureReach;

		private float _creatureDetail = 1f;

		private const string MenuHint = "First Person mod: F2 opens its settings";

		private int _sunWas = -1;

		internal bool NoLandFieldForTest;

		internal const int SharpResolution = 2;

		private static readonly int[] _resolutionWidths = new int[4] { 480, 640, 1280, 1920 };

		private static readonly Regex SharedGpu = new Regex("^(?!.*arc.*\\b[ab]\\d{3}\\b).*intel|radeon(\\(tm\\))? (graphics|vega|\\d{3}m\\b)|custom gpu|apple|basic render|llvmpipe|softpipe|swiftshader|vmware|virtualbox|parallels", RegexOptions.IgnoreCase);

		internal const float TrialSeconds = 60f;

		internal const float TrialSlowestMs = 25f;

		internal const float ProbeSeconds = 3f;

		internal const float ProbeGain = 0.75f;

		private readonly List<float> _trialMs = new List<float>();

		private float _trialTime;

		private float _trialSharpMs;

		private bool _trialProbing;

		internal float TrialFrameMsForTest = -1f;

		internal float TrialProbeMsForTest = -1f;

		internal float TrialSecondsForTest = -1f;

		private readonly ShotgunControl _shotgun = new ShotgunControl();

		private float _snowfall;

		private int _voiceKey;

		private Mumble.Speaker _voiceWho;

		private bool _steppedAside;

		private const float TalkHeadDefault = 0.4f;

		private Actor _talkHeld;

		private float _talkHeldAt = -9f;

		private const float TalkHoldFor = 0.35f;

		private float _widenToastAt = -9f;

		internal static float QuietLevelForTest = -1f;

		private float _quietCheck;

		private int _quietWidth = 960;

		private int _quietHeight = 540;

		private float _crossEyeZ = float.NaN;

		private bool _crossIn;

		private float _roomEyeEase;

		internal const KeyCode ToolKey = KeyCode.Q;

		internal readonly RadialWheel ToolWheel = new RadialWheel();
		internal readonly RadialWheel EmoteWheel = new RadialWheel();

		private static readonly Tool[] ToolOrder = new Tool[5]
		{
			Tool.Torch,
			Tool.Compass,
			Tool.Eat,
			Tool.Bandage,
			Tool.PutAway
		};

		private readonly Tool[] _tools = new Tool[8];

		private float _toolDownAt = -1f;
		private float _emoteDownAt = -1f;
		private int _emoteDownFrame = -1;
		private bool _emotePicked;
		private bool _emoteSwallow;

		private int _toolDownFrame;

		private bool _toolSwallow;

		internal Tool? LastTool;

		private bool _compassWanted;

		private bool _toolPicked;

		private float _torchHold;

		private bool _torchReach;

		private bool _torchBlocks;

		private float _torchSputter;

		private float _torchCatch;

		private float _torchAgainst;

		private float _torchLookedAt = -99f;

		private float _torchHintAt = -99f;

		private bool _torchFireNear;

		private bool _torchRight;

		private int _torchAttacks = -1;

		private float _torchBlowAt = -1f;

		internal int TorchSpritesForTest;

		internal int TorchOthersForTest;

		internal int TorchLightsForTest;

		internal string TorchLastForTest;

		internal bool TorchReachForTest;

		internal bool TorchWaterForTest;

		internal float TorchRainForTest = -1f;

		private const float TorchHoldOut = 0.25f;

		private bool _torchPress;

		public const float TownSeconds = 5.5f;

		private const float TownAwaySeconds = 20f;

		internal City TownShown;

		internal float TownUntil;

		private City _townHere;

		private City _townLastShown;

		private float _townLeftAt = -1000f;

		private float _heldFor;

		private float _heldLogged = -100f;

		private Vector2 _heldAt;

		internal int UnstuckForTest;

		private const float HeldFor = 1.2f;

		internal static readonly int[] VrEyeWidths = new int[5] { 640, 800, 1024, 1280, 1600 };

		private const float VrMonoFov = 105f;

		private const float VrEyeMetres = 1.6f;

		private const float VrHudMetres = 1.4f;

		private VrSystem _vr;

		private static readonly string VrByEnvironment;

		internal static bool VrSimulate;

		private float _vrYaw;

		private float _vrLastYaw = float.NaN;

		private bool _vrHeadSet;

		private bool _vrStereoPlan;

		private float _vrScale = 0.5f;

		private bool _vrScaleSet;

		private Vector3 _vrAnchor;

		private Xr.Vec3 _vrHeadLocal;

		private Xr.Vec3 _vrHeadShift;

		private readonly RenderTexture[] _vrEyes = new RenderTexture[2];

		private readonly IntPtr[] _vrEyePtr = new IntPtr[2];

		private RenderTexture _vrMono;

		private RenderTexture _vrHud;

		private RenderTexture _vrPanel;

		private RenderTexture _vrScreen;

		private IntPtr _vrMonoPtr;

		private IntPtr _vrPanelPtr;

		private IntPtr _vrScreenPtr;

		private readonly EyeCamera[] _vrEyeCam = new EyeCamera[2];

		private readonly Xr.Pose[] _vrEyePose = new Xr.Pose[2];

		private readonly Xr.Fov[] _vrEyeFov = new Xr.Fov[2];

		private readonly int[] _vrEyeW = new int[2];

		private readonly int[] _vrEyeH = new int[2];

		private readonly VrSystem.Layers _vrLayers = new VrSystem.Layers();

		private bool _vrLoop;

		private bool _vrStereoDrawn;

		private bool _vrHudDrawn;

		private float _vrFade;

		private int _vrShowing;

		private RenderTexture _vrFadePicture;

		private IntPtr _vrFadePtr;

		private readonly List<FarProp> _vrFarProps = new List<FarProp>();

		private readonly List<FarActor> _vrFarActors = new List<FarActor>();

		private readonly List<Streak> _vrStreaks = new List<Streak>();

		private int _vrSavedVsync = -1;

		private int _vrSavedFps;

		private bool _vrSavedBackground;

		private Texture2D _vrCursor;

		private double _vrDrawMs;

		private int _vrFrames;

		private float _vrPerfSince;

		internal int VrStereoFrames;

		internal int VrFadesForTest;

		internal double VrDrawMsForTest;

		internal bool MouseLookOff;

		private int _vrHits;

		private int _vrHurts;

		private int _vrAttacks;

		private const float VrHudBoost = 1.35f;

		private RenderTexture _vrWin;

		private RenderTexture _vrWinPanel;

		private IntPtr _vrWinPanelPtr;

		private bool _vrHudPass;

		private bool _vrWinDrawn;

		private const float VrGodMetres = 3f;

		private VrGodRoom _godTaskRoom;

		private VrGodRoom _vrGodRoom;

		private bool _godTaskVr;

		private bool _vrGodDrawn;

		private float _vrGodHeadYaw = float.NaN;

		private float _vrGodHeadPitch;

		private float _vrZoomWheel;

		private bool _vrPanning;

		private bool _vrSnapArmed = true;

		private bool _vrStickClickWas;

		private bool _vrCovered;

		private Camera _vrCoveredCam;

		private CameraClearFlags _vrSavedClear;

		private Color _vrSavedClearColor;

		private readonly List<Behaviour> _vrPostOff = new List<Behaviour>();

		private Material _vrUiMaterial;

		internal int VrGodFrames;

		private float _godTaskDetail;

		private Xr.Vec3 _vrGodPivot;

		private bool _vrGodPivotSet;

		private float _vrGodPivotAt;

		private bool _vrMirrorFailed;

		private float _vrTurnSinceGather;

		private static readonly uint BeamBright;

		private static readonly uint BeamDim;

		private bool _vrHandsOn;

		private bool _vrNockWas;

		private float _vrDrawTick;

		private static readonly uint AimDotColor;

		private bool _vrNavOn;

		private bool _vrNavPress;

		private Vector2 _vrNavGui;

		private Vector3 _vrNavHandWay;

		private int _vrNavHand = -1;

		private int _vrNavClick;

		private Vector2Int _vrNavLater;

		private float _vrNavLaterUntil;

		internal const int PinWindows = 1;

		internal const int PinGodControls = 2;

		internal const int PinScreen = 3;

		private VrPin _vrPin;

		private bool _vrPinRecentre;

		private bool _vrRecentredNow;

		private VrPanelPlace _vrAimPanel;

		private bool _vrWindowUp;

		private bool _vrPullLeft;

		private bool _vrPullRight;

		private bool _vrPullGrip;

		private bool _vrPressOnPanel;

		private bool _vrPressOnLand;

		private float _vrScroll;

		private Vector3 _vrLaserDirWas;

		private PointerEventData _vrPointerData;

		private EventSystem _vrPointerSystem;

		private readonly List<RaycastResult> _vrUiHits = new List<RaycastResult>();

		private readonly List<Event> _vrGuiEvents = new List<Event>();

		private static readonly FieldInfo EventFocus;

		private bool _vrWasAny;

		private bool _vrWasGrip;

		private bool _vrGripRight;

		private bool _vrWasTrigger;

		private bool _vrTakeOverPress;

		private bool _vrTakeOverWanted;

		private int _vrAimHand = -1;

		private float _vrBeamMetres = -1f;

		private int _vrActiveHand = 1;

		private readonly bool[] _vrTrig = new bool[2];

		private readonly bool[] _vrGrip = new bool[2];

		private readonly float[] _vrHandBusy = new float[2];

		private readonly Vector3[] _vrHandWay = new Vector3[2];

		private Vector2 _vrPanelPixel;

		private int _vrOtherHand = -1;

		private bool _vrOtherOnPanel;

		private Vector2 _vrOtherPixel;

		private readonly float[] _vrPullWas = new float[2];

		private readonly bool[] _vrPullStarts = new bool[2];

		private readonly Vector3[] _vrRay = new Vector3[2];

		private readonly Vector3[] _vrRayOld = new Vector3[2];

		private readonly float[] _vrRayAt = new float[2] { -10f, -10f };

		private int _vrHoldHand = -1;

		private Vector3 _vrHoldRay;

		private float _vrHoldSince;

		private Selectable _vrHoverButton;

		internal int VrTakeOversForTest;

		private readonly Xr.Pose[] _vrHandLast = new Xr.Pose[2];

		private readonly float[] _vrHandSeen = new float[2] { -10f, -10f };

		private int _vrPressHand = -1;

		private bool _vrRoomStereo;

		internal static int VrRoomFrames;

		private bool _vrDoorReady;

		private readonly List<MeshInstance> _vrDoorMeshes = new List<MeshInstance>(256);

		private readonly List<PointLight> _vrDoorLights = new List<PointLight>(16);

		private readonly List<LightWindow> _vrDoorWindows = new List<LightWindow>(8);

		private Atmosphere _vrDoorAir;

		private Vector3 _vrDoorShift;

		private const float SwingStart = 1.6f;

		private const float PunchStart = 1.4f;

		private const float SwingKeep = 0.9f;

		private const float HandFastest = 14f;

		private readonly SwingState[] _swing = new SwingState[2]
		{
			new SwingState(),
			new SwingState()
		};

		private Vector3 _strikeAnchorWas;

		private Vector3 _strikeAnchorNow;

		private float _strikeYawWas;

		private float _strikeYawNow;

		private float _strikeAtWas;

		private float _strikeAtNow;

		private readonly List<Actor> _swingNear = new List<Actor>();

		private readonly HashSet<Building> _swingSeen = new HashSet<Building>();

		internal int VrSwingHitsForTest;

		internal int VrSwingWorkForTest;

		internal int VrSwingCutsForTest;

		internal float VrSwingSpeedForTest;

		private const int SweepSteps = 4;

		private float _vrViewAcross = 1.19f;

		private float _vrViewUp = 0.97f;

		private const int VrWristPixels = 1100;

		private const float VrWristMetres = 0.33f;

		private const float VrWristUnits = 239f;

		private RenderTexture _vrWrist;

		private RenderTexture _vrWristPanel;

		private IntPtr _vrWristPanelPtr;

		private bool _vrWristUp;

		private bool _vrWristDrawn;

		private Xr.Pose _vrWristPose;

		private float _vrWristWide;

		private float _vrWristHigh;

		private bool _vrWristShown;

		private bool _vrWristTrigWas;

		private float _vrWristSince = -1f;

		private int _vrWristW;

		private int _vrWristRows;

		internal int WeaponGlowsForTest;

		internal int WeaponGlowOthersForTest;

		private const float GlowOthersRange = 16f;

		private const int GlowOthersMost = 4;

		private readonly List<People.HeldGlow> _glowNear = new List<People.HeldGlow>();

		private readonly List<float> _glowNearD2 = new List<float>();

		private readonly WeaponAura _weaponAura = new WeaponAura();

		internal bool TownsKeysFreeForTest => Hub.KeysFree;

		internal float SinceBeastAbility
		{
			get
			{
				if (LastAbility == null || (LastAbility.Motion != AbilityMotion.Bite && LastAbility.Motion != AbilityMotion.Leap && LastAbility.Motion != AbilityMotion.Shout))
				{
					return 99f;
				}
				return SinceAbility;
			}
		}

		internal bool ActionWheelOpen => ActionWheel.Open;

		internal FrameProjection Shown
		{
			get
			{
				FrameProjection shown = _shown;
				shown.ShiftTowards(_eyeNow);
				return shown;
			}
		}

		internal Hud HudForTest => _hud;

		internal WorldSampler SamplerForTest => _sampler;

		internal bool DrawingBeside => _beside != null;

		private bool CanDrawRoomBeside
		{
			get
			{
				if (Settings.AsyncRender && BesideAllowed && Time.unscaledTime >= _besideRetryAt)
				{
					return _wasInRoom;
				}
				return false;
			}
		}

		private bool BinocularsBlock
		{
			get
			{
				if (!Binoculars.Active)
				{
					return Time.frameCount - _binocularsLoweredFrame < 2;
				}
				return true;
			}
		}

		private float BinocularSteady => 1f / Binoculars.Magnification;

		private bool CanUseBinoculars
		{
			get
			{
				if (_active && DeathTimer < 0f && _dive == Dive.None && !_leaping && !Interior.Inside && !Cabin.Inside)
				{
					return !Mine.Inside;
				}
				return false;
			}
		}

		internal BlasterControl BlasterForTest => _blaster;

		internal Bundles.Bundle BundleOfferedForTest
		{
			get
			{
				if (_host == null)
				{
					return null;
				}
				return Bundles.Near(_host.current_position, _view.Yaw, 1.3f);
			}
		}

		internal float SceneDetailForTest => _scene.Detail;

		internal float CreatureDetailForTest => _creatureDetail;

		internal float CreatureReachForTest => _creatureReach;

		internal bool TalkBusy
		{
			get
			{
				if (!Converse.Open && !Law.BoardOpen && Births.Naming == null)
				{
					return Time.frameCount - _convClosedFrame < 2;
				}
				return true;
			}
		}

		internal bool AtHomeAtNight
		{
			get
			{
				if (Interior.Inside && !Interior.Virtual)
				{
					return _day.Air.Night > 0.45f;
				}
				return false;
			}
		}

		internal bool ChoosingHeir
		{
			get
			{
				if (DeathTimer >= 0f)
				{
					return Heirs.Count > 0;
				}
				return false;
			}
		}

		private float HeirWait
		{
			get
			{
				if (Heirs.Count <= 0)
				{
					return 0f;
				}
				return 12f;
			}
		}

		internal bool KeyboardTaken
		{
			get
			{
				if (!Converse.Open && !Law.BoardOpen && Births.Naming == null && !ChoosingHeir && !Hub.AnyWindowOpen && !MpUi.Typing)
				{
					return Time.frameCount - Hud.FieldFocusFrame <= 1;
				}
				return true;
			}
		}

		internal bool GameKeysMuted
		{
			get
			{
				if (!KeyboardTaken)
				{
					return _fly;
				}
				return true;
			}
		}

		internal TerrainCache SceneTerrain => _sampler.Cache;

		internal bool ClickGrace
		{
			get
			{
				if (!_clickHeldOver)
				{
					return Time.unscaledTime - _clickUiClosedAt < 0.5f;
				}
				return true;
			}
		}

		internal CompassMark? CompassOn { get; private set; }

		internal int CompassIndex { get; private set; }

		internal int CompassCount => _compassMarks.Count;

		private bool CompassBlock
		{
			get
			{
				if (!Compass.Out && !Compass.Active)
				{
					return Time.frameCount - _compassShutFrame < 2;
				}
				return true;
			}
		}

		private bool CanUseCompass
		{
			get
			{
				if (_active && DeathTimer < 0f && _dive == Dive.None && !_leaping && !Swimming && !Interior.Inside && !Cabin.Inside && !Mine.Inside)
				{
					if (_host != null)
					{
						return !BoatHelm.Steers(_host);
					}
					return true;
				}
				return false;
			}
		}

		internal static bool SafeStart => CrashGuard.Suspect("start");

		private bool GpuRoomsSafe
		{
			get
			{
				if (!CrashGuard.Suspect("gpurooms"))
				{
					return true;
				}
				if (!_gpuRoomsNoted)
				{
					_gpuRoomsNoted = true;
					Log.Info("crash guard: rooms drawn on the CPU (a room on the GPU crashed the game last time)");
				}
				return false;
			}
		}

		private bool FreeLook
		{
			get
			{
				if (GpuWanted && !VrRunning)
				{
					if (DeathTimer < 0f && !GpuRooms)
					{
						if (!Cabin.Inside && !Mine.Inside)
						{
							return !Interior.Inside;
						}
						return false;
					}
					return true;
				}
				return false;
			}
		}

		private float LookLimit
		{
			get
			{
				if (!FreeLook)
				{
					return Mathf.Max(45f, Lens.FullTiltDegrees(Settings.FieldOfView * (MathF.PI / 180f), (float)Screen.height / (float)Mathf.Max(1, Screen.width)));
				}
				return 88f;
			}
		}

		internal float LookLimitForTest => LookLimit;

		private Func<float, float, float> CacheHeight => (float x, float y) => _sampler.Cache.SampleHeight(x, y);

		internal FirstPersonAudio Audio => _audio;

		internal DayCycle Day => _day;

		internal float CrouchForTest => _crouch;

		internal float LandDipForTest => _landDip;

		internal float FovKickForTest => _fovKick;

		internal int StepCount => _gait.Steps;

		public static WorldBoxMod Instance { get; private set; }

		public bool IsFirstPerson => _active;

		internal static bool WorldLoading
		{
			get
			{
				try
				{
					return !Config.game_loaded || SmoothLoader.isLoading();
				}
				catch
				{
					return false;
				}
			}
		}

		public Actor Host => _host;

		public ViewState View => _view;

		public float RenderMs => _renderMs;

		internal Vector3 EyeForTest => new Vector3(_view.X, _view.Y, _view.Z);

		public BaseSimObject Picked => _picked;

		public int BillboardCount => _scene.Billboards.Count;

		public int VoxelCount => _scene.Voxels.Count;

		public Texture2D FrameTexture => Frame;

		internal TerrainCache TerrainForTest => _sampler.Cache;

		internal SceneCollector SceneForTest => _scene;

		internal int CraterCount => _sampler.Craters.Count;

		public float ViewYaw => _view.Yaw;

		internal bool IsTypingNow => IsTyping();

		internal bool DivingOutForTest => _dive == Dive.Out;

		internal bool Diving => _dive != Dive.None;

		internal bool LeavingDead
		{
			get
			{
				if (_dive == Dive.Out)
				{
					return _diveFromDeath;
				}
				return false;
			}
		}

		internal float EyeYawForTest => _view.Yaw;

		internal bool DiveDone => _dive == Dive.None;

		internal bool GpuWantedForTest => GpuWanted;

		internal BowControl Bow => _bow;

		internal MeleeControl Melee => _melee;

		internal GuardControl Guard => _guard;

		internal TownPaths PathsForTest => _paths;

		internal bool AnyMenuOpen => AnyMenu;

		private bool AnyMenu
		{
			get
			{
				if (!Help.Open && !Craft.Open && !Royal.Open && !Royal.ObeliskOpen && !WarMap.Open && !SoldierMap.Open && !FamilyMap.Open && !_settingsMenu.Open && !Converse.Open && !WorldNews.Open && !Journal.Open && !Law.BoardOpen && Births.Naming == null && !ChoosingHeir && !Hub.AnyWindowOpen && !Run.WindowOpen && !ReliefWorld.CardOpen)
				{
					return ModSync.PromptShown;
				}
				return true;
			}
		}

		internal bool OnFloor => !float.IsNaN(_floorZ);

		internal float FloorForTest => _floorZ;

		internal float LiftForTest => _standLift;

		private byte FloorSurface
		{
			get
			{
				if (_floorWhat != 3 && _floorWhat != 2 && _floorWhat != 6 && _floorWhat != 18 && _floorWhat != 7)
				{
					return 6;
				}
				return 8;
			}
		}

		internal bool ForgeHandsForTest => _forgeHands;

		internal bool Flying => _fly;

		internal bool FlyLeaving => _flyLeaving;

		internal Vector3 FlyPosition => _flyPos;

		internal float FlyYaw => _flyYaw;

		internal float FlyPitchDegrees => _flyPitch;

		internal bool FlyCursorFree => _flyCursor;

		internal float FlyHintUntil => _flyHintUntil;

		internal float FlySpeedShownUntil => _flySpeedUntil;

		internal float FlyTopForTest => Settings.GodViewZoom * FlyZoomHeight;

		internal ViewState FlyCameraForTest => FlyCamera();

		private float FlyFov => Settings.FieldOfView * (MathF.PI / 180f);

		private static float FlyZoomHeight => (float)Screen.width / (float)Mathf.Max(1, Screen.height) / Mathf.Tan(MathF.PI / 6f);

		private float FlyLookLimit
		{
			get
			{
				if (!GpuWanted || VrRunning)
				{
					return Mathf.Max(45f, Lens.FullTiltDegrees(FlyFov, (float)Screen.height / (float)Mathf.Max(1, Screen.width)));
				}
				return 88f;
			}
		}

		internal float FlyMinimapReach => Mathf.Clamp(Settings.MinimapReach * 1.5f, 24f, 160f);

		internal float GodTurnForTest => _godTurn;

		internal float GodTipForTest => _godTip;

		internal bool GodHomingForTest => _godHoming;

		internal ViewState GodShownForTest => _godShown;

		internal ViewState EyeNowForTest => _eyeNow;

		private bool GodTurnHome
		{
			get
			{
				if (!_godHoming && _godTurn == 0f)
				{
					return _godTip == 0f;
				}
				return false;
			}
		}

		internal bool GodShown => _godSavedMask != -1;

		internal float GodAlpha => _godAlpha;

		internal bool NoDive
		{
			set
			{
				_noDive = value;
			}
		}

		internal SightCut GodCutForTest => _godCut;

		internal string GodToastForTest
		{
			get
			{
				if (!(Time.unscaledTime < _godToastUntil))
				{
					return null;
				}
				return _godToast;
			}
		}

		private float GodCloseness
		{
			get
			{
				if (!(_godClose < 9.999f))
				{
					return 0f;
				}
				return DivePath.ClosenessAt(_godClose, 10f);
			}
		}

		internal Actor GodLockForTest => _godLock;

		internal float GodCloseForTest => _godClose;

		internal float GodCloseTargetForTest => _godCloseTarget;

		internal float GodPullForTest => _godPull;

		internal GpuRenderer GpuRendererForTest => _gpu;

		internal bool GpuWanted
		{
			get
			{
				if (Settings != null && Settings.GpuRender && Time.unscaledTime >= _gpuRetryAt && (_gpu == null || _gpu.Available))
				{
					return !CrashGuard.Suspect("gpu");
				}
				return false;
			}
		}

		internal Texture ShownFrame
		{
			get
			{
				if (!_gpuShown || _gpu == null || !(_gpu.Output != null))
				{
					return Frame;
				}
				return _gpu.Shown;
			}
		}

		internal Texture FrameBehind
		{
			get
			{
				if (!_frameHoles || _gpuShown || _gpu == null || !(_gpu.Output != null))
				{
					return null;
				}
				return _gpu.Output;
			}
		}

		internal BaseSimObject PickedForTest => _picked;

		private bool GpuRoomsWanted
		{
			get
			{
				if (GpuRooms && GpuWanted && !VrRunning)
				{
					return GpuRoomsSafe;
				}
				return false;
			}
		}

		internal string GpuRoomLightForTest
		{
			get
			{
				if (_gpu == null || !_gpuShown)
				{
					return null;
				}
				return _gpu.RoomLightStats;
			}
		}

		public Actor Body
		{
			get
			{
				if (!Hijacking)
				{
					return _host;
				}
				return Hijack.Rider;
			}
		}

		internal bool Mounted => Hijacking;

		internal bool Hijacking
		{
			get
			{
				if (Hijack.Active && _host != null)
				{
					return _host == Hijack.Ship;
				}
				return false;
			}
		}

		internal Actor HijackedShip
		{
			get
			{
				if (!Hijacking)
				{
					return null;
				}
				return Hijack.Ship;
			}
		}

		private static bool ShiftHeld
		{
			get
			{
				if (!PlayInput.GetKey(KeyCode.LeftShift))
				{
					return PlayInput.GetKey(KeyCode.RightShift);
				}
				return true;
			}
		}

		internal string HotbarFoodForTest => _hotbarFood;

		internal float SinceEaten => Time.unscaledTime - _eatAt;

		private bool SettingsTakeTab
		{
			get
			{
				if (_settingsMenu.Open)
				{
					if (!PacksUi.Open)
					{
						return _settingsMenu.Tab == 7;
					}
					return true;
				}
				return false;
			}
		}

		internal bool QuickInventory { get; private set; }

		private bool MoveKeysTaken
		{
			get
			{
				if (!AnyMenu || QuickInventory)
				{
					return PoseLocks;
				}
				return true;
			}
		}

		internal string KeyHoldersForTest => KeyHolders();

		private bool LandBesideOn
		{
			get
			{
				if (!LandBesideOff && GpuWanted)
				{
					if (!BesideAllowed)
					{
						return LandBesideForTest;
					}
					return true;
				}
				return false;
			}
		}

		internal bool CanLeap
		{
			get
			{
				if (_active && DeathTimer < 0f && !Interior.Inside && !Cabin.Inside && !Mine.Inside)
				{
					return _dive == Dive.None;
				}
				return false;
			}
		}

		internal bool IndoorsForDrops
		{
			get
			{
				if (!Interior.Inside && !Cabin.Inside)
				{
					return Mine.Inside;
				}
				return true;
			}
		}

		internal bool WorldStepsWanted
		{
			get
			{
				if (Settings != null && Settings.WorldSteps == 1 && _godAlpha >= 1f && !_active && _dive == Dive.None)
				{
					return !WorldSync.Mirroring;
				}
				return false;
			}
		}

		internal bool MapWindowOpen
		{
			get
			{
				if (!Journal.Open && !WarMap.Open && !SoldierMap.Open && !FamilyMap.Open && !WorldNews.Open)
				{
					return Royal.Open;
				}
				return true;
			}
		}

		internal MapTabs.Page MapPageShown
		{
			get
			{
				if (!WarMap.Open)
				{
					if (!SoldierMap.Open)
					{
						if (!FamilyMap.Open)
						{
							if (!WorldNews.Open)
							{
								if (!Royal.Open)
								{
									return MapTabs.Page.Map;
								}
								return MapTabs.Page.Council;
							}
							return MapTabs.Page.News;
						}
						return MapTabs.Page.Family;
					}
					return MapTabs.Page.Orders;
				}
				return MapTabs.Page.War;
			}
		}

		internal static bool PadCursorOn
		{
			get
			{
				if (PadControls.Active && PadControls.Driving)
				{
					return PadControls.Shown;
				}
				return false;
			}
		}

		internal float YawForPadTest => _view.Yaw;

		internal float PitchForPadTest => _pitchDegrees;

		internal bool CompassOutForPadTest => Compass.Out;

		internal bool BinocularsUpForPadTest => Binoculars.Up;

		internal PadControls.Mode PadModeForTest => _padMode;

		internal bool LandscapeHeldForTest => _landscapeHeld;

		internal TerrainCache Land => _sampler.Cache;

		internal bool BigMapOpen => _bigMap;

		internal bool PingWheelOpen => _wheel;

		internal int PingWheelChoice => _wheelChoice;

		internal Vector2 PingWheelAim => _wheelAim;

		internal float PingWheelOpenedAt => _wheelOpenedAt;

		internal bool PingAimOk => _aimOk;

		internal BaseSimObject PingAimOn => _aimOn;

		internal float PingAimDistance
		{
			get
			{
				if (!_aimOk || _host == null)
				{
					return -1f;
				}
				return (new Vector2(_aimAt.x, _aimAt.y) - _host.current_position).magnitude;
			}
		}

		internal float MinimapReach => Mathf.Clamp(Settings.MinimapReach, 16f, 160f);

		internal bool PoseWheelOpen => _poseWheel;

		internal int PoseWheelChoice => _poseWheelChoice;

		internal float PoseWheelOpenedAt => _poseWheelOpenedAt;

		internal bool PoseLocks
		{
			get
			{
				if (_pose != RestStyle.None)
				{
					if (_poseUp)
					{
						return !(_poseAmount < 0.35f);
					}
					return true;
				}
				return false;
			}
		}

		internal RestStyle PoseNow => _pose;

		internal float PoseAmount => _poseAmount;

		internal float PoseViewOut => _poseView;

		internal RestSeat PoseSeatNow => _poseSeat;

		private bool PoseShowsBody => _poseView > 0.12f;

		private float BodyHeightNow
		{
			get
			{
				if (!(People.HostBody > 0.1f))
				{
					return 0.9f;
				}
				return People.HostBody;
			}
		}

		private bool RoomAirborne => _standLift > 0.05f;

		internal bool WindowInViewForTest
		{
			get
			{
				if (Interior.Inside)
				{
					return Interior.PortalInView(_view);
				}
				return false;
			}
		}

		internal bool CanRunButton
		{
			get
			{
				if (_active && _host != null)
				{
					return DeathTimer < 0f;
				}
				return false;
			}
		}

		internal string RunButtonName
		{
			get
			{
				if (!Run.Locked)
				{
					return "Start a run";
				}
				return "Give up the run";
			}
		}

		internal float SceneZoomFar => Mathf.Max(24f, Settings.GodViewZoom);

		internal Vector2 SceneShownAt
		{
			get
			{
				if (!_sceneShown.Set)
				{
					return Scene.At;
				}
				return _sceneShown.At;
			}
		}

		internal bool SceneShownSet => _sceneShown.Set;

		internal float SeaNow
		{
			get
			{
				if (!(HeldTime >= 0f))
				{
					return Time.unscaledTime + WorldSync.SeaOffset;
				}
				return HeldTime;
			}
		}

		internal SettingsMenu SettingsMenu => _settingsMenu;

		internal bool CanLeaveBody
		{
			get
			{
				if (_active && _host != null)
				{
					return DeathTimer < 0f;
				}
				return false;
			}
		}

		private bool EscapeTaken
		{
			get
			{
				try
				{
					return _menuEscape != null && HotkeyLibrary.cancel?.just_pressed_action == _menuEscape;
				}
				catch
				{
					return false;
				}
			}
		}

		internal float ViewMaxDistance => ViewReach() / 1.5f;

		internal bool NaturalSun
		{
			get
			{
				if (Settings != null)
				{
					if (Settings.SunStyle != 2)
					{
						if (Settings.SunStyle == 0 && (Settings.GpuRender || VrRunning) && (_gpu == null || _gpu.Available))
						{
							return !CrashGuard.Suspect("gpu");
						}
						return false;
					}
					return true;
				}
				return false;
			}
		}

		internal static int[] ResolutionWidths
		{
			get
			{
				_resolutionWidths[2] = SharpWidth;
				return _resolutionWidths;
			}
		}

		internal static int SharpWidth => Mathf.Max(160, Screen.width / 2);

		private int DrawnResolution
		{
			get
			{
				if (!_trialProbing || !_active)
				{
					return Settings.Resolution;
				}
				return 0;
			}
		}

		internal ShotgunControl Shotgun => _shotgun;

		internal Hands HandsForTest => _hands;

		public float Snowfall => _snowfall;

		internal static bool QuietTestCopy { get; private set; }

		private bool WheelClick
		{
			get
			{
				if (!ActionWheel.Open && !_actSwallow && !ToolWheel.Open)
				{
					return _toolSwallow;
				}
				return true;
			}
		}

		internal bool ToolWheelOpen => ToolWheel.Open;
		internal bool EmoteWheelOpen => EmoteWheel.Open;

		private bool TorchClickFree
		{
			get
			{
				if (!ToolWheel.Open && !_toolSwallow && !ActionWheel.Open && !_actSwallow && !_wheel && !_pingSwallow && !_poseWheel && !_poseSwallow && _pose == RestStyle.None && Work.Target == null && !Earthworks.Busy && !Holstered && !BinocularsBlock && !CompassBlock && !Bandages.Wrapping)
				{
					return !Hub.AnyModeActive;
				}
				return false;
			}
		}

		internal Weather SkyWeather
		{
			get
			{
				if (_scene == null)
				{
					return null;
				}
				return _scene.Weather;
			}
		}

		internal float SkyTime
		{
			get
			{
				if (Renderer == null)
				{
					return 0f;
				}
				return Renderer.Time;
			}
		}

		internal VrSystem VrForTest => _vr;

		internal bool VrRunning
		{
			get
			{
				if (_vr != null)
				{
					return _vr.Running;
				}
				return false;
			}
		}

		private bool VrWanted
		{
			get
			{
				if (Settings != null)
				{
					if (!Settings.Vr)
					{
						return VrByEnvironment != null;
					}
					return true;
				}
				return false;
			}
		}

		internal RenderTexture VrHudForTest => _vrHud;

		internal RenderTexture VrWindowForTest
		{
			get
			{
				if (!_vrWindowUp)
				{
					return null;
				}
				return _vrWin;
			}
		}

		internal RenderTexture VrMonoForTest => _vrMono;

		internal float VrFadeForTest => _vrFade;

		internal bool VrWindowUpForTest => _vrWindowUp;

		internal float VrScaleForTest => _vrScale;

		internal float VrYawForTest => _vrYaw;

		private bool VrFades
		{
			get
			{
				if (VrRunning)
				{
					return Settings.VrDive == 0;
				}
				return false;
			}
		}

		private bool VrSquare
		{
			get
			{
				if (_vrHeadSet)
				{
					return !_vrStereoPlan;
				}
				return false;
			}
		}

		private bool VrHudWanted
		{
			get
			{
				if (VrRunning && _vr.Waited && _vrHeadSet && _active && ShownFrame != null && _dive != Dive.In)
				{
					return !_leaping;
				}
				return false;
			}
		}

		internal bool VrGodDrawnForTest => _vrGodDrawn;

		internal Vector3 VrGodSeatForTest => _vrGodRoom.Seat;

		internal float VrGodScaleForTest => _vrGodRoom.Scale;

		internal float VrGodYawForTest => _vrGodRoom.Yaw;

		internal RenderTexture VrScreenForTest => _vrScreen;

		private Xr.Vec3 VrGodPivot
		{
			get
			{
				if (!_vrGodPivotSet)
				{
					return _vr.Head.Position;
				}
				return _vrGodPivot;
			}
		}

		internal bool VrAimDotShown { get; private set; }

		internal Vec3 VrAimDotForTest { get; private set; }

		private bool VrHandsTracked
		{
			get
			{
				if (VrRunning && _vrHeadSet && _vrStereoPlan)
				{
					return Settings.VrHands == 0;
				}
				return false;
			}
		}

		internal bool VrHandsOnForTest => _vrHandsOn;

		internal bool VrNavOnForTest => _vrNavOn;

		internal Vector2 VrNavAtForTest => _vrNavGui;

		internal bool VrPinGlidingForTest => _vrPin.Gliding;

		internal float VrPinYawForTest => _vrPin.Yaw;

		internal bool VrAimPanelUpForTest => _vrAimPanel.Up;

		internal Xr.Pose VrAimPanelPoseForTest => PanelInRoom(_vrAimPanel);

		internal Vector2 VrAimPanelSizeForTest => new Vector2(_vrAimPanel.Wide, _vrAimPanel.High);

		internal bool VrRayHeldForTest => _vrHoldHand >= 0;

		internal float VrBeamMetresForTest => _vrBeamMetres;

		internal int VrActiveHandForTest => _vrActiveHand;

		internal bool VrOtherOnPanelForTest => _vrOtherOnPanel;

		private float HeadBobNow
		{
			get
			{
				if (!VrRunning)
				{
					return Settings.HeadBob;
				}
				return 0f;
			}
		}

		internal bool VrSwingOnForTest => _swing[1].On;

		internal bool AnyMenuForTest => AnyMenu;

		private float VrHudDegrees
		{
			get
			{
				VrViewReach();
				float num = (float)Screen.width / (float)Mathf.Max(1, Screen.height);
				float vrViewAcross = _vrViewAcross;
				float vrViewUp = _vrViewUp;
				float num2 = 1f / Mathf.Sqrt(1f / (vrViewAcross * vrViewAcross) + 1f / (num * num * vrViewUp * vrViewUp));
				num2 *= Mathf.Clamp(Settings.VrHudView, 0.5f, 1f);
				return 2f * Mathf.Atan(num2) * 57.29578f;
			}
		}

		internal float VrHudDegreesForTest => VrHudDegrees;

		internal bool VrWristUpForTest => _vrWristUp;

		internal RenderTexture VrWristForTest => _vrWrist;

		internal int VrWristHoverForTest => _hud.VrWristHover;

		internal string VrWristWhyForTest { get; private set; } = "";

		internal Xr.Pose VrWristPoseForTest => _vrWristPose;

		internal Vector2 VrWristSizeForTest => new Vector2(_vrWristWide, _vrWristHigh);

		internal Vector2 VrWristPixelsForTest => new Vector2(_vrWristW, _vrWristRows);

		private float WristSide
		{
			get
			{
				if (_vr == null || _vr.Off != 1)
				{
					return -1f;
				}
				return 1f;
			}
		}

		private void WakeAtPlace(Building b)
		{
			if (!Interior.Inside || Interior.Room == null || !InsideSpots.WokeAt(_host, b, out var _, out var place))
			{
				return;
			}
			Interior room = Interior.Room;
			bool flag = place.Use == RoomLayout.Use.Bed;
			float num = (flag ? place.SideX : place.X);
			float num2 = (flag ? place.SideY : place.Y);
			if (!(num < room.MinX) && !(num > room.MaxX) && !(num2 < room.MinY) && !(num2 > room.MaxY))
			{
				Interior.Eye = new Vector2(num, num2);
				if (room.Fit != null && place.Storey > 0 && place.Storey < room.Fit.StoreyZ.Length)
				{
					RoomFloors.FeetForTest(room.Fit.StoreyZ[place.Storey]);
				}
				_view.Yaw = (flag ? Mathf.Atan2(place.MidY - num2, place.MidX - num) : place.Yaw);
				Log.Info($"through the roof: woke at {place.Use} ({num:F2},{num2:F2}) storey {place.Storey} of {b.asset.id}");
			}
		}

		private bool LeavingIndoors(out Building house, out Vector2 at, out float z)
		{
			house = null;
			at = default(Vector2);
			z = 0f;
			if (!Interior.Inside || Interior.Virtual || Interior.House == null || !HouseInterior.IsEnterable(Interior.House))
			{
				return false;
			}
			house = Interior.House;
			if (Interior.Fitted && Interior.Room?.Fit != null)
			{
				at = Interior.ToWorld(Interior.Eye);
				z = Interior.WorldZ(_view.Z);
			}
			else
			{
				at = house.current_position;
				z = ((_sampler.Cache != null) ? (_sampler.Cache.SampleHeight(at.x, at.y) + _view.Z) : _view.Z);
			}
			return true;
		}

		private void TownsUpdate(Actor host, float dt)
		{
			if (!_townsReady)
			{
				_townsReady = true;
				RepHud.Install();
			}
			CrownOffer.Update(Body, dt);
			SettlerOffer.Update(Body, dt);
			Hub.InRoom = Interior.Inside && !Interior.Virtual;
			Hub.KeysFree = !IsTyping() && !TalkBusy && DeathTimer < 0f && !ChoosingHeir && (!AnyMenu || Hub.AnyWindowOpen);
			Hub.Update(host, dt, Toast);
		}

		private bool TownsPrompt(Actor host, Vector2 pos)
		{
			PromptAsk townsAsk = _townsAsk;
			townsAsk.Host = host;
			townsAsk.Pos = pos;
			townsAsk.Yaw = _view.Yaw;
			townsAsk.Picked = _picked;
			townsAsk.Room = null;
			townsAsk.House = null;
			townsAsk.Eye = pos;
			townsAsk.Prompt = null;
			if (!Hub.Prompt(townsAsk))
			{
				return false;
			}
			Prompt = townsAsk.Prompt;
			return true;
		}

		private bool TownsRoomPrompt(Actor host)
		{
			PromptAsk townsAsk = _townsAsk;
			townsAsk.Host = host;
			townsAsk.Pos = Interior.Eye;
			townsAsk.Yaw = _view.Yaw;
			townsAsk.Picked = null;
			townsAsk.Room = Interior;
			townsAsk.House = Interior.House;
			townsAsk.Eye = Interior.Eye;
			townsAsk.Prompt = null;
			if (!Hub.RoomPrompt(townsAsk))
			{
				return false;
			}
			Prompt = townsAsk.Prompt;
			return true;
		}

		private void DrawTowns()
		{
			if (!(DeathTimer >= 0f))
			{
				Hub.DrawWindows();
			}
		}

		private void AbilityKeys()
		{
			if (PlayInput.GetKeyDown(KeyCode.X))
			{
				_abilityDown = true;
			}
			if (PlayInput.GetKeyUp(KeyCode.X))
			{
				_abilityUp = true;
			}
		}

		private void UpdateAbility(Actor host, bool dead, float dt)
		{
			if (!_zoneKeysGuarded)
			{
				GuardZoneKeys();
			}
			SinceAbility += dt;
			try
			{
				Abilities.Update();
			}
			catch (Exception ex)
			{
				Log.Error("ability rush: " + ex.Message);
				Abilities.Reset();
			}
			try
			{
				if (!dead)
				{
					Abilities.UpdateCreatures(host, dt);
				}
			}
			catch (Exception ex2)
			{
				Log.Error("creature abilities: " + ex2.Message);
			}
			HearCreatureAbilities(host);
			_abilityRecheck -= dt;
			bool flag = _abilityRecheck <= 0f;
			if (flag)
			{
				_abilityRecheck = 3f;
			}
			Ability ability = ((!(host != _abilityFor || _abilityChoice != Abilities.ChoiceVersion || flag)) ? Ability : ((host != null) ? Abilities.Chosen(host) : null));
			if (host == _abilityFor && ability != Ability && !AbilityAiming && _abilityPending == null)
			{
				Ability = ability;
				_abilityChoice = Abilities.ChoiceVersion;
				if (Ability != null && AbilityTold.Add(Ability.Name))
				{
					Toast("X: " + Ability.Name + " - " + Ability.Blurb);
				}
			}
			_abilityChoice = Abilities.ChoiceVersion;
			if (host != _abilityFor)
			{
				_abilityFor = host;
				Ability = ability;
				AbilityAiming = false;
				_abilityPending = null;
				LastAbility = null;
				SinceAbility = 99f;
				_abilityCooling = false;
				if (Ability != null && AbilityTold.Add(Ability.Name))
				{
					Toast("X: " + Ability.Name + " - " + Ability.Blurb);
				}
			}
			bool abilityDown = _abilityDown;
			bool abilityUp = _abilityUp;
			_abilityDown = (_abilityUp = false);
			if (dead || host == null || Ability == null || !host.isAlive())
			{
				AbilityAiming = false;
				_abilityPending = null;
				_audio.AbilityHold(null, dt);
				return;
			}
			bool flag2 = Abilities.CooldownLeft(host) > 0f;
			if (_abilityCooling && !flag2 && Ability.Cooldown >= 6f)
			{
				_audio.AbilityReady();
			}
			_abilityCooling = flag2;
			bool flag3 = AnyMenu || (Interior.Inside && !RoomCombat.Active) || _dive != Dive.None || DeathTimer >= 0f || IsTyping();
			try
			{
				flag3 |= (WorldSync.Mirroring ? WorldSync.HostPaused : World.world.isPaused());
			}
			catch
			{
			}
			Actor target;
			if (AbilityAiming)
			{
				AbilityAimAt = AbilityAim(host, Ability, out target);
				if (flag3)
				{
					AbilityAiming = false;
				}
				else if (abilityUp || (!PlayInput.GetKey(KeyCode.X) && !_abilityHeldForTest))
				{
					UseAbility(host);
				}
			}
			else if (abilityDown && !flag3 && AbilityReady(host, say: true))
			{
				if (Ability.Aimed)
				{
					AbilityAiming = true;
					AbilityAimAt = AbilityAim(host, Ability, out target);
					if (Ability.Motion == AbilityMotion.Throw)
					{
						_audio.PlayEffect("cloth", 0.5f);
					}
				}
				else
				{
					UseAbility(host);
				}
			}
			if (_abilityPending != null && SinceAbility >= _abilityPending.Delay)
			{
				Ability abilityPending = _abilityPending;
				_abilityPending = null;
				ReleaseAbility(host, abilityPending);
			}
			_audio.AbilityHold(AbilityAiming ? Ability : null, dt);
		}

		private void HearCreatureAbilities(Actor host)
		{
			List<KeyValuePair<Ability, Actor>> creatureUsed = Abilities.CreatureUsed;
			if (creatureUsed.Count == 0)
			{
				return;
			}
			float num = Mathf.Sin(_view.Yaw);
			float num2 = 0f - Mathf.Cos(_view.Yaw);
			foreach (KeyValuePair<Ability, Actor> item in creatureUsed)
			{
				if (host != null && item.Value != null)
				{
					Vector2 vector = item.Value.current_position - host.current_position;
					float magnitude = vector.magnitude;
					float pan = ((magnitude > 0.01f) ? ((vector.x * num + vector.y * num2) / magnitude) : 0f);
					_audio.AbilityHeard(item.Key, item.Value, magnitude, pan);
				}
			}
			creatureUsed.Clear();
		}

		private bool AbilityReady(Actor host, bool say)
		{
			float num = Abilities.CooldownLeft(host);
			if (num > 0f)
			{
				if (say)
				{
					Toast(Ability.Name + " is ready in " + Mathf.CeilToInt(num) + " s");
					_audio.AbilityDenied();
				}
				return false;
			}
			if (!Abilities.CanPay(host, Ability, out var why))
			{
				if (say)
				{
					Toast(why + " for " + Ability.Name);
					_audio.AbilityDenied();
				}
				return false;
			}
			return true;
		}

		private void UseAbility(Actor host)
		{
			AbilityAiming = false;
			if (!AbilityReady(host, say: true))
			{
				return;
			}
			Ability ability = Ability;
			if (!WorldSync.Mirroring || Abilities.MovesBody(ability))
			{
				Abilities.Pay(host, ability);
			}
			Abilities.StartCooldown(host, ability);
			LastAbility = ability;
			SinceAbility = 0f;
			_abilityPending = ability;
			_abilityUses++;
			_audio.AbilityUsed(ability, host, People.IsHumanoid(host));
			if (ability.Motion == AbilityMotion.Shout)
			{
				Feedback.Shake = Mathf.Max(Feedback.Shake, 0.35f);
			}
			try
			{
				if ((ability.Motion == AbilityMotion.Shout || ability.Motion == AbilityMotion.Bite) && host.asset != null && host.asset.has_sound_attack)
				{
					Sounds.PlayAt(host.asset.sound_attack, host.current_position);
				}
			}
			catch
			{
			}
			Feedback.Kick = Mathf.Max(Feedback.Kick, (ability.Motion == AbilityMotion.Leap) ? 1f : 0.5f);
			Log.Info("ability: " + Describe(host) + " uses " + ability.Name);
		}

		private void ReleaseAbility(Actor host, Ability ab)
		{
			if (host == null || !host.isAlive())
			{
				return;
			}
			Actor target;
			Vector2 at = AbilityAim(host, ab, out target);
			float x = Mathf.Cos(_view.Yaw);
			float y = Mathf.Sin(_view.Yaw);
			AbilityUse arg = new AbilityUse
			{
				Self = host,
				At = at,
				Target = target,
				Dir = new Vector2(x, y),
				Toast = Toast
			};
			if (!Abilities.MovesBody(ab) && WorldSync.ForwardAbility(host, ab, at, target, arg.Dir))
			{
				_audio.AbilityLanded(ab, host, 0);
				if (ab.Motion == AbilityMotion.Slam)
				{
					Feedback.Shake = Mathf.Max(Feedback.Shake, 0.8f);
				}
				return;
			}
			Abilities.Answered = 0;
			Abilities.Caster = host;
			bool flag;
			try
			{
				flag = ab.Effect(arg);
			}
			catch (Exception ex)
			{
				Log.Error("ability " + ab.Name + " failed: " + ex);
				flag = false;
			}
			finally
			{
				Abilities.Caster = null;
			}
			if (!flag)
			{
				Abilities.Refund(host, ab);
				return;
			}
			_audio.AbilityLanded(ab, host, Abilities.Answered);
			if (ab.Motion == AbilityMotion.Slam)
			{
				Feedback.Shake = Mathf.Max(Feedback.Shake, 0.8f);
			}
		}

		private Vector2 AbilityAim(Actor host, Ability ab, out Actor target)
		{
			target = null;
			Vector2 current_position = host.current_position;
			bool handAim = FirstPersonAim.HandAim;
			Actor actor = (handAim ? null : (_picked as Actor));
			if (actor == null && FirstPersonAim.Ready(host) && FirstPersonAim.ThrowPoint(out var _, out var creature))
			{
				actor = creature;
			}
			if (actor != null && actor != host && actor.isAlive() && (actor.current_position - current_position).sqrMagnitude <= (ab.Range + 0.5f) * (ab.Range + 0.5f))
			{
				target = actor;
				if (ab.Range > 0f)
				{
					return actor.current_position;
				}
			}
			if (ab.Range <= 0f)
			{
				return current_position;
			}
			Vec3 vec = (handAim ? FirstPersonAim.FreeFrom : new Vec3(_view.X, _view.Y, _view.Z));
			float f = (handAim ? FirstPersonAim.ThrowYaw : _view.Yaw);
			float num = (handAim ? FirstPersonAim.ThrowSlope : _view.Pitch);
			float num2 = Mathf.Cos(f);
			float num3 = Mathf.Sin(f);
			bool room = RoomCombat.Active && RoomCombat.Room != null;
			TerrainCache terrainCache = (room ? RoomCombat.Room.Floor : _sampler.Cache);
			float num4 = 0.15f;
			for (float num5 = 0.3f; num5 <= ab.Range; num5 += num4)
			{
				float x = vec.X + num2 * num5;
				float y = vec.Y + num3 * num5;
				float num6 = vec.Z + num * num5;
				if (terrainCache.SampleHeight(x, y) >= num6 || (room && !RoomCombat.InRoomSpace(new Vec3(x, y, num6))))
				{
					return ClampReach(current_position, Map(x, y), ab.Range);
				}
			}
			return ClampReach(current_position, Map(vec.X + num2 * ab.Range, vec.Y + num3 * ab.Range), ab.Range);
			Vector2 Map(float x2, float y2)
			{
				if (!room)
				{
					return new Vector2(x2, y2);
				}
				return RoomCombat.ToWorld(new Vector2(x2, y2));
			}
		}

		private static Vector2 ClampReach(Vector2 from, Vector2 to, float range)
		{
			Vector2 vector = to - from;
			if (!(vector.sqrMagnitude > range * range))
			{
				return to;
			}
			return from + vector.normalized * range;
		}

		private void AbilityHands(ref HandsInput input)
		{
			Ability ability = (AbilityAiming ? Ability : LastAbility);
			if (ability != null)
			{
				input.Gesture = ((ability.Motion == AbilityMotion.Throw) ? Gesture.Throw : ((ability.Motion == AbilityMotion.Cast) ? Gesture.Cast : ((ability.Motion == AbilityMotion.Shout) ? Gesture.Shout : ((ability.Motion == AbilityMotion.Slam) ? Gesture.Slam : Gesture.None))));
				input.GestureHeld = AbilityAiming;
				input.SinceGesture = (AbilityAiming ? 0f : SinceAbility);
				input.GestureRelease = ability.Delay;
				input.GestureProp = ((ability.Prop == null) ? null : (Models.Get(ability.Prop) ?? ThrownMeshes.Get(ability.Prop)));
				Color glow = ability.Glow;
				input.GestureGlow = ((ability.Motion == AbilityMotion.Cast || (ability.Motion == AbilityMotion.Slam && glow != Color.white)) ? TerrainCache.Rgb((int)(glow.r * 255f), (int)(glow.g * 255f), (int)(glow.b * 255f)) : 0u);
			}
		}

		private void AddAbilityMarker(List<VoxelObject> voxels)
		{
			if (!AbilityAiming || Ability == null || Ability.Range <= 0f)
			{
				return;
			}
			TerrainCache cache = _sampler.Cache;
			float num = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
			uint num2 = ((Ability.Motion == AbilityMotion.Cast) ? TerrainCache.Rgb((int)(Ability.Glow.r * 255f), (int)(Ability.Glow.g * 255f), (int)(Ability.Glow.b * 255f)) : TerrainCache.Rgb(255, 214, 120));
			if (!MarkShapes.TryGetValue(num2, out var value))
			{
				value = VoxelShape.NewGrid(1, 1, 1);
				value.Box(0, 0, -1, 1, 1, 1, num2);
				MarkShapes[num2] = value;
			}
			float num3 = Mathf.Max(0.45f, Ability.Radius);
			int num4 = Mathf.Clamp(Mathf.RoundToInt(num3 * 14f), 12, 36);
			float num5 = Time.unscaledTime * 0.6f;
			float num6 = 0.05f + 0.02f * num;
			for (int i = 0; i <= num4; i++)
			{
				Vector2 abilityAimAt = AbilityAimAt;
				if (i < num4)
				{
					float f = num5 + (float)i * MathF.PI * 2f / (float)num4;
					abilityAimAt += new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num3;
				}
				float z = cache.SampleHeight(abilityAimAt.x, abilityAimAt.y) + 0.04f;
				float num7 = abilityAimAt.x - _view.X;
				float num8 = abilityAimAt.y - _view.Y;
				voxels.Add(new VoxelObject
				{
					Shape = value,
					Emissive = true,
					NoShadow = true,
					Tag = -1,
					U = new Vec3(1f, 0f, 0f),
					V = new Vec3(0f, 0f, 1f),
					N = new Vec3(0f, 1f, 0f),
					SizeU = ((i < num4) ? num6 : (num6 * 1.6f)),
					SizeV = num6 * 0.6f,
					SizeN = ((i < num4) ? num6 : (num6 * 1.6f)),
					Origin = new Vec3(abilityAimAt.x - num6 * 0.5f, abilityAimAt.y, z),
					SortDepth = Mathf.Sqrt(num7 * num7 + num8 * num8)
				});
			}
		}

		private static void GuardZoneKeys()
		{
			_zoneKeysGuarded = true;
			try
			{
				foreach (HotkeyAsset item in AssetManager.hotkey_library.list)
				{
					if (item == null || item.id == null || !item.id.StartsWith("zone_type", StringComparison.Ordinal) || item.just_pressed_action == null)
					{
						continue;
					}
					HotkeyAction original = item.just_pressed_action;
					item.just_pressed_action = delegate(HotkeyAsset asset)
					{
						WorldBoxMod instance = Instance;
						if (instance == null || !instance.IsFirstPerson)
						{
							original(asset);
						}
					};
				}
			}
			catch (Exception ex)
			{
				Log.Error("could not keep the zone keys off X in first person: " + ex.Message);
			}
		}

		internal void PressAbilityForTest(bool down)
		{
			_abilityHeldForTest = down;
			if (down)
			{
				_abilityDown = true;
			}
			else
			{
				_abilityUp = true;
			}
		}

		private void StopAbility()
		{
			_audio.AbilityHold(null, 0f);
			AbilityAiming = false;
			_abilityPending = null;
			_abilityFor = null;
			Ability = null;
			LastAbility = null;
			Abilities.Reset();
		}

		private static bool IsGuardAct(Act a)
		{
			if (a != Act.GuardKill)
			{
				return a == Act.GuardExecute;
			}
			return true;
		}

		private static bool FromPrompt(Act a)
		{
			if (a != Act.E && a != Act.ShiftE && a != Act.Insult)
			{
				return a == Act.Steal;
			}
			return true;
		}

		internal int ActionsForTest()
		{
			return ReadActions(Prompt, _picked as Actor);
		}

		private int ReadActions(string prompt, Actor picked)
		{
			RadialWheel actionWheel = ActionWheel;
			actionWheel.Clear();
			actionWheel.Tip = "Let go of E to choose";
			if (prompt != null && prompt.Contains("Hold E"))
			{
				return 0;
			}
			Actor actor = ((Talk.Candidate != null && Dialogue.IsPerson(Talk.Candidate)) ? Talk.Candidate : null);
			_guardTarget = actor ?? picked;
			string text = KingGuards.Refusal(_host, _guardTarget);
			bool flag = text == null || text.Length > 0;
			if (string.IsNullOrEmpty(prompt) && !flag)
			{
				return 0;
			}
			prompt = prompt ?? "";
			bool flag2 = false;
			string[] array = prompt.Split('|');
			foreach (string text2 in array)
			{
				int num = text2.IndexOf(':');
				if (num <= 0)
				{
					continue;
				}
				string text3 = text2.Substring(0, num);
				string label = text2.Substring(num + 1);
				switch (text3)
				{
				case "E":
					Push(Act.E, label);
					break;
				case "Shift+E":
					Push(Act.ShiftE, label);
					break;
				case "F":
					if (actor != null)
					{
						Push(Act.Insult, "Insult");
					}
					break;
				case "Q":
					if (actor != null)
					{
						bool flag3 = Asleep(actor);
						bool flag4 = Run.Locked && !flag3;
						Push(Act.Steal, "Steal", flag4, flag4 ? "In a run you steal only from the sleeping" : null);
					}
					break;
				case "=":
					flag2 = true;
					break;
				}
			}
			if (actionWheel.Count == 0 && !flag)
			{
				return 0;
			}
			if (actor != null)
			{
				bool flag5 = false;
				try
				{
					flag5 = actor.isFavorite();
				}
				catch
				{
				}
				Push(Act.Star, flag5 ? "Unstar" : "Star");
			}
			if (flag2 && picked != null)
			{
				Push(Act.TakeOver, "Take over");
			}
			if (flag)
			{
				bool dim = text != null;
				Push(Act.GuardKill, "Guards: kill", dim, text);
				if (KingGuards.CanExecute(_guardTarget))
				{
					Push(Act.GuardExecute, "Guards: execute", dim, text);
				}
			}
			return actionWheel.Count;
		}

		private void Push(Act act, string label, bool dim = false, string note = null)
		{
			if (ActionWheel.Count < 8)
			{
				_acts[ActionWheel.Count] = act;
				ActionWheel.Add(label, dim, note);
			}
		}

		private static bool Asleep(Actor a)
		{
			try
			{
				return ((BaseSimObject)a).hasStatus("sleeping");
			}
			catch
			{
				return false;
			}
		}

		private void ActionWheelKeys(bool menu)
		{
			float unscaledTime = Time.unscaledTime;
			bool flag = !menu && DeathTimer < 0f && !IsTyping() && !TalkBusy && !Hub.AnyModeActive && _dive == Dive.None && !ToolWheel.Open;
			if (_actSwallow && !PlayInput.GetMouseButton(0) && !PlayInput.GetMouseButton(1))
			{
				_actSwallow = false;
			}
			if (_actSwallow)
			{
				PossessionHooks.BlockAttack = true;
			}
			if (_actDownAt < 0f)
			{
				if (flag && PlayInput.RawKeyDown(KeyCode.E))
				{
					int num = ReadActions(Prompt, _picked as Actor);
					if (num >= 2 || (num == 1 && (_acts[0] == Act.ShiftE || IsGuardAct(_acts[0]))))
					{
						_actDownAt = unscaledTime;
						_actDownFrame = Time.frameCount;
						_actTarget = Talk.Candidate ?? (_picked as Actor);
						PlayInput.HeldBack = KeyCode.E;
					}
				}
				return;
			}
			if (!flag)
			{
				if (ActionWheel.Open)
				{
					ActionWheel.Close();
				}
				EndActionHold();
				return;
			}
			if (!PlayInput.RawKey(KeyCode.E))
			{
				if (_actPicked)
				{
					EndActionHold();
					return;
				}
				bool open = ActionWheel.Open;
				int num2 = (open ? ActionWheel.Close() : 0);
				EndActionHold();
				if (open || FromPrompt(_acts[0]))
				{
					if (num2 >= 0)
					{
						DoAction(num2);
					}
					else
					{
						Sounds.PlayUI("event:/SFX/UI/WindowClose");
					}
				}
				return;
			}
			if (!ActionWheel.Open && !_actPicked && unscaledTime - _actDownAt >= 0.25f && Time.frameCount - _actDownFrame > 2)
			{
				int num3 = ReadActions(Prompt, _actTarget);
				if (num3 < 2 && (num3 != 1 || !IsGuardAct(_acts[0])))
				{
					return;
				}
				ActionWheel.Show();
			}
			if (!ActionWheel.Open)
			{
				return;
			}
			PossessionHooks.BlockAttack = true;
			if (PlayInput.GetMouseButtonDown(0) || PlayInput.GetMouseButtonDown(1))
			{
				int num4 = ActionWheel.Close();
				_actSwallow = true;
				_actPicked = true;
				if (num4 >= 0)
				{
					DoAction(num4);
				}
			}
		}

		private void ResetWheels()
		{
			if (ActionWheel.Open)
			{
				ActionWheel.Close();
			}
			if (ToolWheel.Open)
			{
				ToolWheel.Close();
			}
			EndActionHold();
			EndToolHold();
			_actSwallow = (_toolSwallow = false);
			_actTarget = null;
			_torchPress = false;
		}

		private void EndActionHold()
		{
			_actPicked = false;
			_actDownAt = -1f;
			if (PlayInput.HeldBack == KeyCode.E)
			{
				PlayInput.HeldBack = KeyCode.None;
			}
		}

		private void DoAction(int i)
		{
			Actor host = _host;
			Actor actTarget = _actTarget;
			switch (_acts[i])
			{
			case Act.E:
				PlayInput.Inject(KeyCode.E, shift: false);
				break;
			case Act.ShiftE:
				PlayInput.Inject(KeyCode.E, shift: true);
				break;
			case Act.Insult:
				if (actTarget != null && actTarget.isAlive() && host != null)
				{
					Talk.Insult(host, actTarget);
				}
				break;
			case Act.Steal:
				StealFrom(actTarget);
				break;
			case Act.Star:
				if (actTarget != null && actTarget.isAlive())
				{
					StarCreature(actTarget);
				}
				break;
			case Act.TakeOver:
				if (actTarget != null)
				{
					TryLeapInto(actTarget);
				}
				break;
			case Act.GuardKill:
			case Act.GuardExecute:
				if (host != null && _guardTarget != null)
				{
					KingGuards.Give(host, _guardTarget, (_acts[i] != Act.GuardKill) ? KingGuards.Kind.Execute : KingGuards.Kind.Kill, Toast);
				}
				break;
			}
		}

		private void StealFrom(Actor who)
		{
			if (who != null && who.isAlive())
			{
				if (Run.Locked && !Asleep(who))
				{
					Toast("In a run you steal only from the sleeping");
					return;
				}
				Law.WatchTheft(who);
				PossessionHooks.ScriptedSteal = true;
			}
		}

		internal static void MenuTool(Tool t)
		{
			Instance?.UseTool(t);
		}

		internal static void MenuTakeOver()
		{
			Instance?.TryLeap();
		}

		internal static void MenuLetGo()
		{
			WorldBoxMod instance = Instance;
			if (instance != null && instance.CanLeaveBody)
			{
				instance.TouchLetGo();
			}
		}

		internal static void MenuInsult()
		{
			WorldBoxMod instance = Instance;
			Actor actor = instance?.Talk.Candidate;
			if (!(instance == null) && instance._host != null)
			{
				if (actor == null || !Dialogue.IsPerson(actor) || instance.Talk.Indoors || instance.Talk.Blocked)
				{
					instance.Toast("Look at someone near to insult them");
				}
				else
				{
					instance.Talk.Insult(instance._host, actor);
				}
			}
		}

		internal static void MenuSteal()
		{
			WorldBoxMod instance = Instance;
			Actor actor = instance?.Talk.Candidate;
			if (!(instance == null) && instance._host != null)
			{
				if (actor == null || !Dialogue.IsPerson(actor) || instance.Talk.Blocked)
				{
					instance.Toast("Look at someone near to steal from them");
				}
				else
				{
					instance.StealFrom(actor);
				}
			}
		}

		internal void ActionForTest(int i)
		{
			_actTarget = Talk.Candidate ?? (_picked as Actor);
			if (ReadActions(Prompt, _actTarget) > i)
			{
				DoAction(i);
			}
		}

		internal string ShownPrompt(string raw)
		{
			if (ActionWheel.Open)
			{
				return null;
			}
			bool flag = false;
			if (raw == null || !raw.Contains("Hold E"))
			{
				Actor t = ((Talk.Candidate != null && Dialogue.IsPerson(Talk.Candidate)) ? Talk.Candidate : (_picked as Actor));
				flag = KingGuards.Refusal(_host, t) == null;
			}
			if (string.IsNullOrEmpty(raw))
			{
				if (!flag)
				{
					return raw;
				}
				return "Hold E:Guards";
			}
			if (raw.Contains("Hold E"))
			{
				return raw;
			}
			string[] array = raw.Split('|');
			string text = null;
			string text2 = null;
			int num = 0;
			bool flag2 = Talk.Candidate != null && Dialogue.IsPerson(Talk.Candidate);
			bool flag3 = false;
			StringBuilder stringBuilder = new StringBuilder();
			string[] array2 = array;
			foreach (string text3 in array2)
			{
				int num2 = text3.IndexOf(':');
				string text4 = ((num2 > 0) ? text3.Substring(0, num2) : null);
				string text5 = ((num2 > 0) ? text3.Substring(num2 + 1) : text3);
				switch (text4)
				{
				case "E":
				case "Shift+E":
					if (text == null)
					{
						text = text5;
						break;
					}
					num++;
					text2 = text5;
					break;
				case "F":
				case "Q":
					if (flag2)
					{
						num++;
						text2 = ((text4 == "F") ? "Insult" : "Steal");
					}
					break;
				case "=":
					flag3 = true;
					break;
				default:
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('|');
					}
					stringBuilder.Append(text3);
					break;
				}
			}
			if (text == null)
			{
				string text6 = ((!flag3) ? stringBuilder.ToString() : ((stringBuilder.Length > 0) ? ("MMB:Take over|" + stringBuilder) : "MMB:Take over"));
				if (!flag)
				{
					return text6;
				}
				if (text6.Length <= 0)
				{
					return "Hold E:Guards";
				}
				return text6 + "|Hold E:Guards";
			}
			if (flag2)
			{
				num++;
			}
			if (flag3)
			{
				num++;
			}
			if (flag)
			{
				num += 2;
			}
			string text7 = "E:" + text;
			if (num == 1)
			{
				text7 = text7 + "|Hold E:" + ((flag2 && text2 == null) ? "Star" : (text2 ?? "Take over"));
			}
			else if (num > 1)
			{
				text7 += "|Hold E:More";
			}
			if (stringBuilder.Length <= 0)
			{
				return text7;
			}
			return text7 + "|" + stringBuilder;
		}

		private bool CanDrawBeside(bool dead)
		{
			if (Settings.AsyncRender && BesideAllowed && !dead && _dive != Dive.In && !_leaping && Time.unscaledTime >= _besideRetryAt && !GpuWanted && !_vrStereoPlan)
			{
				return Time.frameCount != _sceneEndedFrame;
			}
			return false;
		}

		private void StartBesideRender(ViewState camera, int width, int height, Actor host, float body, float feet, Vector2 eye, double collectMs)
		{
			_besideBillboards.Clear();
			_besideBillboards.AddRange(_scene.Billboards);
			_besideVoxels.Clear();
			_besideVoxels.AddRange(_scene.Voxels);
			_besideCamera = camera;
			_besideWidth = width;
			_besideHeight = height;
			_besideHost = host;
			_besideBody = body;
			_besideFeet = feet;
			_besideCollectMs = collectMs;
			_besideRoom = false;
			_besideScene = false;
			_besideHoles = false;
			_besideTagged = _scene.Tagged;
			_eyeNow = camera;
			FrameRenderer renderer = Renderer;
			WorldSampler sampler = _sampler;
			TerrainCache cache = sampler.Cache;
			List<Billboard> billboards = _besideBillboards;
			List<VoxelObject> voxels = _besideVoxels;
			float top = sampler.MaxHeight + 0.45f;
			float ex = eye.x;
			float ey = eye.y;
			_beside = Task.Run(delegate
			{
				long timestamp = Stopwatch.GetTimestamp();
				try
				{
					sampler.TickTiles(ex, ey, eye: true);
				}
				catch (Exception besideLandError)
				{
					_besideLandError = besideLandError;
				}
				long timestamp2 = Stopwatch.GetTimestamp();
				renderer.Render(cache, camera, billboards, voxels, top);
				long timestamp3 = Stopwatch.GetTimestamp();
				_besideLandMs = (double)(timestamp2 - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
				_besideDrawMs = (double)(timestamp3 - timestamp2) * 1000.0 / (double)Stopwatch.Frequency;
			});
		}

		private void StartBesideRoom(Action draw, ViewState view, int width, int height, List<BaseSimObject> tagged, bool holes)
		{
			_besideRoom = true;
			_besideScene = false;
			_besideHoles = holes;
			_besideTagged = tagged;
			_besideWidth = width;
			_besideHeight = height;
			_besideHost = null;
			_eyeNow = view;
			_beside = Task.Run(delegate
			{
				long timestamp = Stopwatch.GetTimestamp();
				draw();
				_besideLandMs = 0.0;
				_besideDrawMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			});
		}

		private void FinishBesideRender()
		{
			_besideMainMs = 0.0;
			if (_beside == null)
			{
				return;
			}
			Task beside = _beside;
			_beside = null;
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				beside.Wait();
			}
			catch (AggregateException ex)
			{
				BesideFailed(ex.InnerException ?? ex);
				return;
			}
			double waitMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			if (_besideLandError != null)
			{
				if (!_besideLandErrorLogged)
				{
					Log.Error("land refresh beside the game failed (it carries on next frame): " + _besideLandError);
				}
				_besideLandErrorLogged = true;
				_besideLandError = null;
			}
			_besideFailures = 0;
			if (!_active || Renderer.Width != _besideWidth || Renderer.Height != _besideHeight)
			{
				return;
			}
			List<BaseSimObject> list = _besideTagged ?? _scene.Tagged;
			_picked = ((!_besideScene && Renderer.PickTag >= 0 && Renderer.PickTag < list.Count) ? list[Renderer.PickTag] : null);
			Actor besideHost = _besideHost;
			if (besideHost != null && besideHost == _host && DeathTimer < 0f && besideHost.isAlive())
			{
				if (Hitboxes.SpriteOf == null)
				{
					Hitboxes.SpriteOf = (Actor a) => _sprites.Get(a.getSpriteToRender());
					Hitboxes.GroundAt = (float x, float y) => _sampler.Cache.SampleHeight(x, y);
				}
				FirstPersonAim.Set(besideHost, _besideCamera, _besideBody, _besideFeet, _picked, Renderer.PickDepth, _sampler.Cache, Renderer.MaxDistance);
			}
			long timestamp2 = Stopwatch.GetTimestamp();
			UploadFrame(_besideWidth, _besideHeight, _besideHoles);
			double uploadMs = (double)(Stopwatch.GetTimestamp() - timestamp2) * 1000.0 / (double)Stopwatch.Frequency;
			if (_besideRoom || _besideScene)
			{
				_besideMainMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
				return;
			}
			AdaptDetail();
			_besideMainMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			_perf.AddLate(_besideMainMs);
			_perf.AddLand(_sampler.TakeTimes());
			_perf.AddBeside(Time.unscaledDeltaTime, _besideCollectMs, waitMs, _besideLandMs, _besideDrawMs, uploadMs, Renderer);
		}

		private void BesideFailed(Exception e)
		{
			_besideFailures++;
			if (_besideFailures >= 3)
			{
				_besideRetryAt = float.MaxValue;
				Log.Error("drawing beside the game failed again; drawing in step from now on: " + e);
				Toast("Drawing while the game runs failed: drawing in step until the game restarts");
			}
			else
			{
				_besideRetryAt = Time.unscaledTime + 5f;
				Log.Error("drawing beside the game failed (drawing in step for a few seconds): " + e);
			}
		}

		private void SettleBesideRender()
		{
			if (_beside != null)
			{
				try
				{
					_beside.Wait();
				}
				catch
				{
				}
				_beside = null;
			}
		}

		internal void Rustle()
		{
			_audio.PlayRustle();
		}

		private static int MissingHealth(Actor a)
		{
			try
			{
				return (a != null) ? Mathf.Max(0, a.getMaxHealth() - a.getHealth()) : 0;
			}
			catch
			{
				return 0;
			}
		}

		private void BandageHands(Actor host, ref HandsInput input)
		{
			input.Wrapped = Bandages.Healing(host);
			if (Bandages.Wrapping && Bandages.WrapBy == host)
			{
				input.Bandage = Mathf.Max(0.001f, Bandages.WrapProgress);
				input.BandageOther = Bandages.WrapOther;
				input.BandageOver = Bandages.WrapOver;
			}
			if (input.Bandage > 0f)
			{
				input.BandageRoll = Models.Get("held_bandage_roll");
			}
		}

		internal string SleepOnBedrollForTest(Actor host)
		{
			return SleepOnBedroll(host);
		}

		private string SleepOnBedroll(Actor host)
		{
			if (host == null || HomeLife.Sleeping)
			{
				return "asleep already";
			}
			string text = null;
			if (Interior.Inside || Cabin.Inside || Mine.Inside)
			{
				text = "Sleep in a bed in here";
			}
			else if (Swimming)
			{
				text = "Not in the water";
			}
			else
			{
				Actor actor = EnemyNear(host, 14f);
				if (actor != null || Economy.InAFight(host))
				{
					text = "Not with enemies near";
					Log.Info("bedroll: " + ((actor != null) ? (Describe(actor) + " near") : "in a fight"));
				}
			}
			if (text != null)
			{
				Toast(text);
				return text;
			}
			BedrollNights++;
			Vector2 current_position = host.current_position;
			Log.Info("bedroll: sleeping out at " + current_position.ToString());
			Toast("You lay out your bedroll");
			HomeLife.StartSleep();
			try
			{
				int num = Hotbar.Chosen(host);
				int num2 = Hotbar.Size(host);
				int num3 = BagOrder.Of(host).IndexOf("h");
				int num4 = ((num3 >= 0 && num3 < num2) ? num3 : (-1));
				int num5 = 0;
				while (num4 < 0 && num5 < num2)
				{
					if (num5 != num)
					{
						num4 = num5;
					}
					num5++;
				}
				if (num4 >= 0 && num4 != num)
				{
					Hotbar.Choose(host, num4, wheel: false);
				}
			}
			catch (Exception ex)
			{
				Log.Error("bedroll: the slot after: " + ex.Message);
			}
			return null;
		}

		private static Actor EnemyNear(Actor host, float reach)
		{
			try
			{
				Vector2 current_position = host.current_position;
				float num = reach * reach;
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != host && simple.isAlive() && !((simple.current_position - current_position).sqrMagnitude > num))
					{
						if (simple.attack_target == host)
						{
							return simple;
						}
						if (simple.kingdom != null && host.kingdom != null && simple.kingdom != host.kingdom && simple.kingdom.isEnemy(host.kingdom))
						{
							return simple;
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private void BinocularKeys(bool menu)
		{
			if (PlayInput.GetKeyDown(KeyCode.Z) && !menu && !IsTyping() && CanUseBinoculars)
			{
				bool up = Binoculars.Up;
				Binoculars.Press();
				if (Binoculars.Up && !up)
				{
					_audio.PlayRustle();
				}
			}
			if (PlayInput.GetKeyUp(KeyCode.Z))
			{
				Binoculars.Release();
			}
			if (Binoculars.Up)
			{
				if (menu || !CanUseBinoculars)
				{
					Binoculars.Lower();
				}
				else if (PlayInput.GetMouseButtonDown(0) || PlayInput.GetMouseButtonDown(1) || PlayInput.GetKeyDown(KeyCode.X))
				{
					Binoculars.Lower();
					_binocularsLoweredFrame = Time.frameCount;
				}
			}
		}

		private bool BinocularWheel(float scroll)
		{
			if (!Binoculars.Up)
			{
				return false;
			}
			if (Binoculars.Step((scroll > 0f) ? 1 : (-1)))
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			return true;
		}

		private void UpdateBinoculars(float dt)
		{
			if (!CanUseBinoculars || _host != _binocularsHost)
			{
				if (Binoculars.Active)
				{
					Binoculars.Drop();
				}
				_binocularsHost = _host;
			}
			Binoculars.Update(dt);
			float magnification = Binoculars.Magnification;
			Renderer.DetailZoom = magnification;
			Models.LodZoom = magnification;
			BinocularsRange = (Binoculars.Active ? CentreDepth() : (-1f));
		}

		private void DropBinoculars()
		{
			Binoculars.Drop();
			Renderer.DetailZoom = 1f;
			Models.LodZoom = 1f;
			BinocularsRange = -1f;
		}

		private float CentreDepth()
		{
			if (_gpuShown && _gpu != null)
			{
				float pickDepth = _gpu.PickDepth;
				if (!(pickDepth > 0f) || !(pickDepth < Renderer.FogDistance))
				{
					return -1f;
				}
				return pickDepth;
			}
			int width = Renderer.Width;
			int height = Renderer.Height;
			float[] depth = Renderer.Depth;
			if (width <= 0 || height <= 0 || depth.Length < width * height)
			{
				return -1f;
			}
			float num = depth[(height - 1 - height / 2) * width + width / 2];
			if (!(num > 0f) || !(num < Renderer.FogDistance))
			{
				return -1f;
			}
			return num;
		}

		private void ApplyBiomeAir()
		{
			float biomeLevel = SceneCollector.BiomeLevel;
			if (!(biomeLevel <= 0f) && Renderer.Air != null && !Renderer.Air.Indoors)
			{
				Renderer.Air = _scene.BiomeMix.Apply(Renderer.Air, biomeLevel, Time.unscaledTime);
				BiomePicture.Set(_scene.BiomeMix, Time.frameCount);
			}
		}

		private void UpdateBlaster(Actor host, string weapon, bool canShoot, float dt)
		{
			bool keysFree = canShoot && !IsTyping() && !Hub.AnyModeActive;
			_blaster.Update(host, weapon, Feedback.Attacks, Feedback.LastAttackMelee, keysFree, dt, _audio);
		}

		private bool BoatPrompt(Actor host, Vector2 pos)
		{
			if (!(_picked is Actor actor) || !BoatHelm.IsBoat(actor))
			{
				return false;
			}
			bool flag = Boats.CanBoard(host, actor, pos);
			bool flag2 = Hijack.CanTake(host, actor, pos);
			if (!flag && !flag2)
			{
				return false;
			}
			Prompt = ((flag && flag2) ? "E:Go aboard|Shift+E:Hijack" : (flag ? "E:Go aboard" : "Shift+E:Hijack"));
			if (PlayInput.GetKeyDown(KeyCode.E) && !IsTyping())
			{
				if (ShiftHeld)
				{
					if (flag2)
					{
						_takeShip = actor;
					}
				}
				else if (flag)
				{
					EnterCabin(actor);
				}
			}
			return true;
		}

		internal void BuildBoatForTest()
		{
			Actor host = _host;
			WorldTile worldTile = ((host != null) ? Raft.WaterAhead(host.current_position, _view.Yaw) : null);
			Actor actor = ((worldTile != null) ? Raft.Build(host, worldTile, Toast) : null);
			if (actor != null)
			{
				_takeShip = actor;
			}
			else
			{
				Log.Info("raft: no water ahead to build on" + ((worldTile == null) ? "" : ", or no boat made"));
			}
		}

		private bool RaftPrompt(Actor host, Vector2 pos)
		{
			if (Hijacking || Swimming || host == null || !Dialogue.IsPerson(host) || _picked is Actor || _picked is Building)
			{
				return false;
			}
			if (Time.unscaledTime >= _raftCheckAt)
			{
				_raftCheckAt = Time.unscaledTime + 0.3f;
				_raftWater = Raft.WaterAhead(pos, _view.Yaw) != null && !Raft.BoatNear(host, pos);
			}
			if (!_raftWater)
			{
				return false;
			}
			int num = Civil.BagCount(host, "wood");
			Prompt = ((num >= 15) ? $"E:Build a boat ({15} wood)" : $"Build a boat: {15} wood ({num} in your bag)");
			if (PlayInput.GetKeyDown(KeyCode.E) && !IsTyping())
			{
				WorldTile worldTile = Raft.WaterAhead(pos, _view.Yaw);
				Actor actor = ((worldTile != null) ? Raft.Build(host, worldTile, Toast) : null);
				if (actor != null)
				{
					_takeShip = actor;
					_builtShip = actor;
					_builtShipBy = host;
					_builtShipFor = 1f;
					_audio.PlayDoor();
					Toast("Boat built. W/S sails, A/D the wheel, Shift+E to step off");
				}
			}
			return true;
		}

		internal void EnterCabin(Actor boat)
		{
			Actor host = _host;
			if (host != null && boat != null)
			{
				ExitHouse();
				if (!Cabin.Enter(boat, host))
				{
					Toast("There's no room aboard");
					return;
				}
				if (!Boats.Board(host, boat))
				{
					Cabin.Exit();
					Toast("You can't get aboard");
					return;
				}
				Talk.Reset(_scene.LookAt);
				Work.Reset();
				_audio.PlayDoor();
				_outsideYaw = _view.Yaw;
				_view.Yaw = Cabin.Room.SpawnYaw;
				PossessionHooks.Frozen = true;
				_ashoreCheckAt = 0f;
				Toast("Aboard, in the " + Cabin.Name + "   (E at the door to go ashore)");
			}
		}

		private void HoldBoat(Actor boat)
		{
			if (Time.unscaledTime < _holdAt)
			{
				return;
			}
			_holdAt = Time.unscaledTime + 0.25f;
			BoatHeldForTest = false;
			try
			{
				if (WarMap.Ordered(boat, out var _))
				{
					return;
				}
				Boat simpleComponent = boat.getSimpleComponent<Boat>();
				if (simpleComponent != null)
				{
					int num = 0;
					foreach (Actor passenger in simpleComponent.getPassengers())
					{
						if (passenger != null && passenger != _host && passenger.isAlive())
						{
							num++;
						}
					}
					if (num > 0)
					{
						return;
					}
				}
				boat.stopMovement();
				boat.makeWait(1f);
				BoatHeldForTest = true;
			}
			catch
			{
			}
		}

		private void TakeWheelFromCabin(Actor boat)
		{
			Actor host = _host;
			if (host == null || boat == null)
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = !Hijack.Active && boat.isAlive() && BoatHelm.IsBoat(boat) && boat.canBePossessed() && !ControllableUnit.isControllingUnit(boat) && !ControllableUnit.isControllingCrabzilla();
			}
			catch
			{
			}
			if (!flag)
			{
				Toast("Her wheel can't be taken now");
				Log.Info("boats: wheel refused from the cabin of " + Describe(boat));
				return;
			}
			Talk.Reset(_scene.LookAt);
			Cabin.Exit();
			PossessionHooks.Frozen = false;
			Boats.Forget(host);
			try
			{
				if (host.is_inside_boat)
				{
					host.exitBoat();
				}
			}
			catch
			{
			}
			try
			{
				host.current_position = boat.current_position;
				host.dirty_current_tile = true;
			}
			catch
			{
			}
			_takeShip = boat;
			Log.Info("boats: up from the cabin to the wheel of " + Describe(boat));
		}

		internal void TakeWheelForTest()
		{
			if (Cabin.Inside)
			{
				TakeWheelFromCabin(Cabin.Boat);
			}
		}

		internal void LeaveCabinForTest()
		{
			LeaveCabin();
		}

		private bool AtSea()
		{
			try
			{
				if (Cabin.Boat == null || !Cabin.Boat.isAlive())
				{
					return false;
				}
				Boats.Landing(Cabin.Boat, out var wet);
				return wet;
			}
			catch
			{
				return false;
			}
		}

		private void LeaveCabin(string why = null)
		{
			if (Cabin.Inside)
			{
				Actor boat = Cabin.Boat;
				float heading = Cabin.Heading;
				Actor host = _host;
				Talk.Reset(_scene.LookAt);
				Cabin.Exit();
				PossessionHooks.Frozen = false;
				bool wet = false;
				if (host != null && host.isAlive() && host.is_inside_boat)
				{
					Vector2 vector = Boats.Disembark(host, boat, out wet);
					Vector2 vector2 = vector - (boat?.current_position ?? vector);
					_view.Yaw = ((vector2.sqrMagnitude > 0.0001f) ? Mathf.Atan2(vector2.y, vector2.x) : heading);
					_lastPos = vector;
				}
				_eyeZ = 0f;
				_eyeOffset = Vector2.zero;
				if (wet)
				{
					_audio.PlayEffect("splash", 0.7f);
				}
				else
				{
					_audio.PlayDoor();
				}
				if (why != null)
				{
					Toast(why);
				}
				else if (wet)
				{
					Toast("Over the side, into the sea");
				}
			}
		}

		private void RenderCabin(float dt)
		{
			Actor host = _host;
			Actor boat = Cabin.Boat;
			bool flag = false;
			try
			{
				flag = boat?.isAlive() ?? false;
			}
			catch
			{
			}
			if (!flag || !Boats.IsAboard(host, boat))
			{
				LeaveCabin(flag ? null : "The boat is going down!");
				return;
			}
			HoldBoat(boat);
			RenderRoom(Cabin, 8, 0.25f, dt);
			Prompt = null;
			if (Talk.Candidate != null)
			{
				Prompt = TalkPrompt(host, Talk.Candidate);
			}
			else if ((!(_pitchDegrees < -20f) && Cabin.NearDoor) || !RoomLootPrompt(host))
			{
				string prompt;
				if ((prompt = Curtains.CabinPrompt(Cabin)) != null)
				{
					Prompt = prompt;
				}
				else if (Cabin.NearDoor && Cabin.Inside)
				{
					if (Time.unscaledTime >= _ashoreCheckAt)
					{
						_ashoreNear = Boats.CanGoAshore(boat);
						_ashoreCheckAt = Time.unscaledTime + 0.6f;
					}
					bool flag2 = false;
					try
					{
						flag2 = boat.kingdom == null || !boat.kingdom.isCiv() || boat.kingdom == host.kingdom;
					}
					catch
					{
					}
					Prompt = (_ashoreNear ? "E:Go ashore" : "E:Jump overboard") + (flag2 ? "|Shift+E:Take the wheel" : "");
					if (PlayInput.GetKeyDown(KeyCode.E) && !IsTyping())
					{
						if (flag2 && ShiftHeld)
						{
							TakeWheelFromCabin(boat);
						}
						else
						{
							LeaveCabin();
						}
					}
				}
			}
			if (DebugPrompt != null)
			{
				Prompt = DebugPrompt;
			}
		}

		private static Vec3 HelmEye(Actor boat, float heading, out Vec3 pos, out Mat3 basis, out float scale, out float eyeAbove)
		{
			MeshModel meshModel = Models.Get(Models.BoatFamily(boat));
			float[] array = CabinBuilder.Floats(meshModel?.Info("helm"));
			Vector2 current_position = boat.current_position;
			Creatures.BoatPose(boat, current_position.x + boat.move_jump_offset.x, current_position.y, heading, out pos, out basis, out scale);
			Vec3 v = ((array != null && array.Length >= 4) ? new Vec3(array[0], array[1], array[2] + array[3]) : new Vec3(0f, (meshModel?.Max.Y ?? 2f) * 0.6f, 2f));
			eyeAbove = ((array != null && array.Length >= 4) ? array[3] : 1f) * scale;
			return basis.Mul(v) + pos;
		}

		private void HelmView(Actor boat, float dt)
		{
			BoatHelm.Update(dt, !AnyMenu && !IsTyping());
			if (_helmFresh)
			{
				_helmLastHeading = BoatHelm.Heading;
				_view.Yaw = BoatHelm.Heading;
				_helmFresh = false;
			}
			_view.Yaw += Mathf.DeltaAngle(_helmLastHeading * 57.29578f, BoatHelm.Heading * 57.29578f) * (MathF.PI / 180f);
			_helmLastHeading = BoatHelm.Heading;
			Vec3 pos;
			Mat3 basis;
			float scale;
			float eyeAbove;
			Vec3 vec = HelmEye(boat, BoatHelm.Heading, out pos, out basis, out scale, out eyeAbove);
			_view.X = vec.X;
			_view.Y = vec.Y;
			_view.Z = vec.Z;
			_eyeZ = vec.Z;
			_helmBody = eyeAbove / 0.9f;
			Vec3 c = basis.C1;
			Vec3 c2 = basis.C0;
			float num = Mathf.Asin(Mathf.Clamp((0f - c.Z) / Mathf.Max(0.0001f, Mathf.Sqrt(Vec3.Dot(c, c))), -0.5f, 0.5f));
			float num2 = Mathf.Asin(Mathf.Clamp(c2.Z / Mathf.Max(0.0001f, Mathf.Sqrt(Vec3.Dot(c2, c2))), -0.5f, 0.5f));
			float f = _view.Yaw - BoatHelm.Heading;
			float num3 = num * Mathf.Cos(f) + num2 * Mathf.Sin(f);
			float num4 = (Settings.ScreenShake ? (Feedback.Shake + BoatHelm.Bump * 0.8f) : 0f);
			float num5 = ((num4 > 0f) ? ((UnityEngine.Random.value - 0.5f) * num4 * 1.2f) : 0f);
			_view.Pitch = LookSlope(_pitchDegrees + Feedback.Kick * 1.5f + num5 + num3 * 57.29578f);
			_view.Fov = (Settings.FieldOfView + _fovKick + (PhotoMode.Active ? PhotoMode.FovOffset : 0f)) * (MathF.PI / 180f);
			Swimming = false;
			Moving = false;
			float num6 = BoatHelm.HalfLength();
			Vector2 vector = boat.current_position + Creatures.MooringOffset(boat);
			Renderer.RippleX = vector.x + Mathf.Cos(BoatHelm.Heading) * num6 * 0.8f;
			Renderer.RippleY = vector.y + Mathf.Sin(BoatHelm.Heading) * num6 * 0.8f;
			Renderer.RippleStrength = Mathf.Clamp01(Mathf.Abs(BoatHelm.Way)) * 0.7f;
			if (BoatHelm.Bump > 0.6f && !_bumped)
			{
				_bumped = true;
				_audio.PlayEffect("hit_heavy", 0.6f);
				Toast("She runs up against it and stops");
			}
			else if (BoatHelm.Bump < 0.1f)
			{
				_bumped = false;
			}
		}

		private void BuildHelmHands(Actor boat, float dt)
		{
			float[] array = CabinBuilder.Floats(Models.Get(Models.BoatFamily(boat))?.Info("wheel"));
			if (array == null || array.Length < 5)
			{
				Renderer.OverlayMeshes.Clear();
				return;
			}
			HelmEye(boat, BoatHelm.Heading, out var pos, out var basis, out var scale, out var _);
			int num = Mathf.Max(3, (int)array[4]);
			HelmGrip g = new HelmGrip
			{
				Hub = basis.Mul(new Vec3(array[0], array[1], array[2])) + pos,
				Left = basis.Mul(new Vec3(1f, 0f, 0f)).Normalized(),
				Up = basis.Mul(new Vec3(0f, 0f, 1f)).Normalized(),
				Radius = array[3] * scale,
				Turn = BoatHelm.Wheel * 2.4f,
				Spokes = num,
				First = MathF.PI / 2f + MathF.PI / (float)num
			};
			HelmsmanColours(boat, out var skin, out var sleeve);
			if (Hijacking)
			{
				uint num2 = People.GameSkin(Hijack.Rider);
				if (num2 != 0)
				{
					skin = num2;
				}
			}
			if (!_hands.BuildHelm(Renderer.OverlayMeshes, _view, in g, skin, sleeve, _helmBody, dt))
			{
				Renderer.OverlayMeshes.Clear();
			}
		}

		private static void HelmsmanColours(Actor boat, out uint skin, out uint sleeve)
		{
			string text = ((boat.asset != null) ? (boat.asset.id ?? "") : "");
			skin = (text.EndsWith("_orc", StringComparison.Ordinal) ? TerrainCache.Rgb(118, 150, 84) : (text.EndsWith("_elf", StringComparison.Ordinal) ? TerrainCache.Rgb(238, 206, 170) : (text.EndsWith("_dwarf", StringComparison.Ordinal) ? TerrainCache.Rgb(214, 158, 120) : TerrainCache.Rgb(226, 178, 136))));
			sleeve = TerrainCache.Rgb(96, 74, 56);
			try
			{
				if (boat.kingdom != null)
				{
					Color color = Royal.ColorOf(boat.kingdom);
					sleeve = TerrainCache.Lerp(TerrainCache.Rgb((int)(color.r * 255f), (int)(color.g * 255f), (int)(color.b * 255f)), TerrainCache.Rgb(70, 54, 40), 0.35f);
				}
			}
			catch
			{
			}
		}

		private void HelmFor(Actor unit)
		{
			if (BoatHelm.IsBoat(unit))
			{
				BoatHelm.Begin(unit);
				_helmFresh = true;
				_pitchDegrees = -10f;
				Toast("At the helm   W/S sails  A/D wheel  Shift a gust" + (WarMap.Armed(unit) ? "  LMB fire" : ""));
			}
			else
			{
				BoatHelm.End();
			}
		}

		private void KeepBodyClear(Actor host)
		{
			if (host == null || (PossessionHooks.Frozen && _dive != Dive.In))
			{
				return;
			}
			Vector2 current_position = host.current_position;
			Vector2 vector = KeepOffBlocks(host, current_position);
			if (host.position_height < 0.3f || !PossessionHooks.Flies(host))
			{
				float radius = PossessionHooks.BodyRadius * 0.9f;
				vector = Colliders.PushOut(vector, radius);
				if (host.position_height < 0.3f && !TalkBusy)
				{
					Vector2 vector2 = Colliders.OutOfCreatures(vector, radius);
					float num = 2.5f * Time.unscaledDeltaTime;
					if (vector2.sqrMagnitude > num * num)
					{
						vector2 = vector2.normalized * num;
					}
					vector += vector2;
				}
			}
			Vector2 vector3 = vector - current_position;
			if (!(vector3.sqrMagnitude < 1E-08f) && !(vector3.sqrMagnitude > 25f))
			{
				try
				{
					host.current_position = vector;
					host.dirty_current_tile = true;
				}
				catch
				{
					return;
				}
				_eyeOffset -= vector3;
			}
		}

		private static Vector2 KeepOffBlocks(Actor host, Vector2 p)
		{
			try
			{
				if (host.position_height > 0.01f || host.ignoresBlocks())
				{
					return p;
				}
				WorldTile tileAt = Toolbox.getTileAt(p.x, p.y);
				if (tileAt == null || !Climbing.Stops(host, tileAt))
				{
					return p;
				}
				float num = float.MaxValue;
				Vector2 result = p;
				for (int i = 1; i <= 4; i++)
				{
					if (num != float.MaxValue)
					{
						break;
					}
					for (int j = -i; j <= i; j++)
					{
						for (int k = -i; k <= i; k++)
						{
							if (Mathf.Max(Mathf.Abs(k), Mathf.Abs(j)) != i)
							{
								continue;
							}
							WorldTile tileAt2 = Toolbox.getTileAt((float)(tileAt.x + k) + 0.5f, (float)(tileAt.y + j) + 0.5f);
							if (tileAt2 != null && !Climbing.Stops(host, tileAt2))
							{
								Vector2 vector = new Vector2(Mathf.Clamp(p.x, (float)tileAt2.x + 0.22f, (float)tileAt2.x + 1f - 0.22f), Mathf.Clamp(p.y, (float)tileAt2.y + 0.22f, (float)tileAt2.y + 1f - 0.22f));
								float num2 = (vector - p).sqrMagnitude;
								if (tileAt2.Type != null && (tileAt2.Type.liquid || tileAt2.Type.lava))
								{
									num2 += 4f;
								}
								if (num2 < num)
								{
									num = num2;
									result = vector;
								}
							}
						}
					}
				}
				return result;
			}
			catch
			{
				return p;
			}
		}

		private void BonfireBurn(Actor host, Vector2 pos, float feet, float head, float bodyRadius, float dt)
		{
			bool flag = false;
			SceneCollector.FireSpot[] bonfires = _scene.Bonfires;
			for (int i = 0; i < bonfires.Length; i++)
			{
				SceneCollector.FireSpot fireSpot = bonfires[i];
				float num = fireSpot.Radius + bodyRadius * 0.5f;
				if (!((pos - fireSpot.At).sqrMagnitude > num * num) && !(feet > fireSpot.Top - 0.1f) && !(head < fireSpot.Bottom))
				{
					flag = true;
					break;
				}
			}
			_inBonfire = (flag ? (_inBonfire + dt) : 0f);
			if (_inBonfire < 0.12f || Torches.Burning(host))
			{
				return;
			}
			try
			{
				((BaseSimObject)host).addStatusEffect("burning", 0f, pColorEffect: true);
			}
			catch
			{
			}
		}

		private bool BundlePrompt(Actor host, Vector2 pos, bool lookingDown)
		{
			if (Bundles.List.Count == 0 || AnyMenu)
			{
				return false;
			}
			bool num;
			if (!lookingDown)
			{
				if (Prompt == null)
				{
					goto IL_0047;
				}
				num = Prompt.Contains("E:");
			}
			else
			{
				num = _pitchDegrees > -20f;
			}
			if (num)
			{
				return false;
			}
			goto IL_0047;
			IL_0047:
			Bundles.Bundle bundle = Bundles.Near(pos, _view.Yaw, 1.3f);
			if (bundle == null)
			{
				return false;
			}
			string text = "E:Pick up " + Bundles.NameOf(bundle);
			Prompt = ((Prompt == null) ? text : (text + "|" + Prompt));
			if (PlayInput.GetKeyDown(KeyCode.E) && !IsTyping())
			{
				int count = Bundles.List.Count;
				Bundles.PickUp(Body ?? host, bundle, Toast);
				if (Bundles.List.Count != count || bundle.Count > 0)
				{
					_audio.PlayRustle();
				}
			}
			return true;
		}

		internal bool DropSpot(out Vector2 at)
		{
			at = Vector2.zero;
			if (_host == null || !_active)
			{
				return false;
			}
			if (Interior.Inside || Cabin.Inside || Mine.Inside)
			{
				Toast("Nothing can be dropped in here");
				return false;
			}
			at = _host.current_position + new Vector2(Mathf.Cos(_view.Yaw), Mathf.Sin(_view.Yaw)) * 0.8f;
			if (!Bundles.Dry(at))
			{
				Toast("It would sink");
				return false;
			}
			return true;
		}

		internal bool DropResource(string id, int count)
		{
			if (count <= 0 || !DropSpot(out var at))
			{
				return false;
			}
			try
			{
				if (!Supplies.Is(id))
				{
					_host.takeFromInventory(id, count);
				}
				else if (!Supplies.Take(_host, id, count))
				{
					return false;
				}
			}
			catch (Exception ex)
			{
				Log.Error("bundles: dropping " + id + ": " + ex.Message);
				return false;
			}
			Bundles.Drop(at, GroundAt, id, count, null);
			_audio.PlayRustle();
			Toast("You drop " + Work.Amount(id, count));
			return true;
		}

		internal bool DropPiece(int index)
		{
			if (!DropSpot(out var at))
			{
				return false;
			}
			Gear.Piece piece = Gear.Remove(_host, index);
			if (piece == null)
			{
				return false;
			}
			BagOrder.PieceGone(_host, index);
			Bundles.Drop(at, GroundAt, null, 0, piece);
			_audio.PlayRustle();
			Toast("You drop " + Gear.NameOf(piece));
			return true;
		}

		internal bool DropWorn(EquipmentType slot)
		{
			if (!DropSpot(out var at))
			{
				return false;
			}
			Gear.Piece piece = Gear.Strip(_host, slot, Toast);
			if (piece == null)
			{
				return false;
			}
			Bundles.Drop(at, GroundAt, null, 0, piece);
			_audio.PlayRustle();
			Toast("You drop " + Gear.NameOf(piece));
			return true;
		}

		internal bool PickUpForTest(Bundles.Bundle b)
		{
			return Bundles.PickUp(_host, b, Toast);
		}

		internal void GpuSumsForTest(out double scene, out double submit, out int frames)
		{
			scene = _gpuCollect;
			submit = _gpuDraw;
			frames = _gpuFrames;
		}

		internal void RestartPerfWindowsForTest()
		{
			_gpuPerfSince = Time.unscaledTime;
			_gpuCollect = (_gpuDraw = 0.0);
			_gpuFrames = 0;
			SceneClock.ResetWindow();
		}

		internal void PinDetailForTest(float detail, float creatures)
		{
			_scene.Detail = detail;
			Renderer.ShadowDistance = 22f * detail;
			_creatureDetail = creatures;
		}

		internal bool CollectSceneForTest(float dt)
		{
			if (!_active || _host == null || !_host.isAlive() || DeathTimer >= 0f || _dive != Dive.None || _leaping)
			{
				return false;
			}
			if (Interior.Inside || Mine.Inside || Cabin.Inside || SceneShows())
			{
				return false;
			}
			CollectScene(_view, _view, _host, 0f, _day.Air, dt);
			return true;
		}

		private void CivilSetup()
		{
			if (!_civilReady)
			{
				_civilReady = true;
				Talk.Converse = (Actor h, Actor n) => PlayerTalk.TryAsk(n) || StartConversation(h, n, hello: true);
				Talk.Closed = CivilStop;
				Converse.Ended = EndConversation;
				Converse.Toast = Toast;
				Converse.Hearts = delegate(Actor a)
				{
					Talk.HeartsOver(a, HeadOfAny(a));
				};
			}
		}

		private Vector3 HeadOfAny(Actor a)
		{
			if (!Interior.Inside)
			{
				return HeadOfWorld(a);
			}
			return Interior.HeadOf(a, _sprites);
		}

		internal bool StarHead(Actor a, out Vector3 head)
		{
			head = default(Vector3);
			try
			{
				if (a == null || !a.isAlive() || Cabin.Inside || Mine.Inside)
				{
					return false;
				}
				if (Interior.Inside)
				{
					if (Interior.DistanceTo(a) == float.MaxValue)
					{
						return false;
					}
					head = Interior.HeadOf(a, _sprites);
					return true;
				}
				if (Hitboxes.Seen(a) && Hitboxes.Get(a, out var box))
				{
					head = new Vector3(box.Centre.X, box.Centre.Y, box.Top + 0.04f);
					return true;
				}
				head = HeadOfWorld(a);
				return true;
			}
			catch
			{
				return false;
			}
		}

		internal static ArmLook ArmLookOf(Actor host)
		{
			if (!Models.IsFemale(host))
			{
				return ArmLook.Man;
			}
			switch (Models.ArmStyle(Models.PersonFamily(host)))
			{
			case "bell":
				return ArmLook.WomanBell;
			case "bare":
				return ArmLook.WomanBare;
			case "bracer":
				return ArmLook.WomanBracer;
			default:
				return ArmLook.Woman;
			}
		}

		private string TalkPrompt(Actor host, Actor a)
		{
			if (!PlayerTalk.IsPlayer(a))
			{
				if (!Dialogue.IsPerson(a) || PeopleMet.Knows(host, a))
				{
					return "E:Talk";
				}
				return "E:Say hello";
			}
			return "E:Ask to talk";
		}

		internal bool StartConversationForTest(Actor npc)
		{
			if (_host != null)
			{
				return StartConversation(_host, npc, hello: false);
			}
			return false;
		}

		internal bool HelloForTest(Actor npc)
		{
			if (_host != null)
			{
				return StartConversation(_host, npc, hello: true);
			}
			return false;
		}

		internal bool StartAudience(Actor npc)
		{
			if (_host == null || !StartConversation(_host, npc, hello: false))
			{
				return false;
			}
			Converse.DistanceTo = (Actor a) => 0f;
			_audience = true;
			return true;
		}

		internal bool StartConfrontation(Actor npc)
		{
			if (_host != null)
			{
				return StartConversation(_host, npc, hello: false);
			}
			return false;
		}

		private bool StartConversation(Actor host, Actor npc, bool hello)
		{
			CivilSetup();
			if (!Dialogue.IsPerson(npc))
			{
				return false;
			}
			bool flag = false;
			try
			{
				flag = npc.isBaby();
			}
			catch
			{
			}
			if (flag)
			{
				return false;
			}
			if (Converse.Open)
			{
				EndConversation();
			}
			Converse.DistanceTo = (Actor a) => (!Interior.Inside) ? (a.current_position - host.current_position).magnitude : Interior.DistanceTo(a);
			_audience = false;
			Converse.Start(host, npc, hello);
			if (!PossessionHooks.Frozen)
			{
				PossessionHooks.Frozen = true;
				_frozeForTalk = true;
			}
			_talkWait = 0f;
			KeepTalking(host, npc, 0f);
			_audio.PlayRustle();
			Log.Info("talk: with " + Describe(npc) + " (" + Converse.Relation + ")");
			return true;
		}

		internal void EndConversation()
		{
			if (!Converse.Open)
			{
				Unfreeze();
				return;
			}
			Actor with = Converse.With;
			if (with != null)
			{
				_scene.LookAt.Remove(with);
			}
			Converse.Close();
			Unfreeze();
			_convClosedFrame = Time.frameCount;
		}

		private void Unfreeze()
		{
			if (_frozeForTalk && !Interior.Inside)
			{
				PossessionHooks.Frozen = false;
			}
			_frozeForTalk = false;
		}

		private void CivilStop()
		{
			EndConversation();
			Journal.Open = false;
			WorldNews.Away();
			SoldierMap.Close();
			FamilyMap.Close();
			Law.BoardOpen = false;
			Hub.CloseAll();
			HomeCard.Close();
			if (Births.Naming != null && _host != null)
			{
				Births.FinishNaming(_host, keep: true);
			}
			Births.Naming = null;
		}

		internal bool CivilEscape()
		{
			if (Births.Naming != null)
			{
				Births.FinishNaming(_host, keep: true);
				return true;
			}
			if (Law.BoardOpen)
			{
				Law.BoardOpen = false;
				_convClosedFrame = Time.frameCount;
				return true;
			}
			if (Converse.Open)
			{
				EndConversation();
				return true;
			}
			if (PlayerTalk.Escape())
			{
				return true;
			}
			if (Hub.Escape())
			{
				return true;
			}
			if (ServiceEscape())
			{
				return true;
			}
			if (FamilyMapEscape())
			{
				return true;
			}
			if (WorldNews.Open)
			{
				WorldNews.Toggle();
				return true;
			}
			if (Journal.Open)
			{
				Journal.Open = false;
				return true;
			}
			return false;
		}

		private void KeepTalking(Actor host, Actor npc, float dt)
		{
			_talkWait -= dt;
			if (_talkWait <= 0f && !_audience)
			{
				_talkWait = 2f;
				try
				{
					npc.makeWait(3f);
					npc.lookTowardsPosition(host.current_position);
				}
				catch
				{
				}
			}
			_scene.LookAt[npc] = (Interior.Inside ? Interior.Eye : host.current_position);
			if (dt > 0f && !DebugEyeSet && !_audience && !_roomTalk)
			{
				FaceTalker(npc, dt);
			}
		}

		private void CivilKeys()
		{
			CivilSetup();
			if (_frozeForTalk && !Converse.Open)
			{
				Unfreeze();
			}
			MuteGameKeys();
			MapWindowKeys();
			if (ChoosingHeir)
			{
				for (int i = 0; i < Heirs.Count; i++)
				{
					if (PlayInput.GetKeyDown((KeyCode)(49 + i)) || PlayInput.GetKeyDown((KeyCode)(257 + i)))
					{
						ContinueAs(i);
						break;
					}
				}
			}
			else if (Births.Naming != null)
			{
				if (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter))
				{
					Births.FinishNaming(_host, keep: false);
				}
			}
			else if (Law.BoardOpen)
			{
				if (PlayInput.GetKeyDown(KeyCode.E) && Time.frameCount != _boardOpenedFrame)
				{
					Law.BoardOpen = false;
					_convClosedFrame = Time.frameCount;
				}
			}
			else
			{
				if (!Converse.Open)
				{
					return;
				}
				if (PlayInput.GetKeyDown(KeyCode.B) && Converse.With != null)
				{
					StarCreature(Converse.With);
				}
				if (PlayInput.GetKeyDown(KeyCode.W) || PlayInput.GetKeyDown(KeyCode.UpArrow))
				{
					Converse.Move(-1);
				}
				if (PlayInput.GetKeyDown(KeyCode.S) || PlayInput.GetKeyDown(KeyCode.DownArrow))
				{
					Converse.Move(1);
				}
				float num = Input.mouseScrollDelta.y + TalkWheelForTest;
				TalkWheelForTest = 0f;
				if (num != 0f && !_settingsMenu.Open)
				{
					Converse.Move((!(num > 0f)) ? 1 : (-1), wrap: false);
				}
				if (PlayInput.GetKeyDown(KeyCode.E) || PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter) || PlayInput.GetKeyDown(KeyCode.Space))
				{
					if (Converse.EndAfter && Converse.Typed)
					{
						EndConversation();
					}
					else
					{
						Converse.Pick(Converse.Selected);
					}
				}
				for (int j = 0; j < 9; j++)
				{
					if (PlayInput.GetKeyDown((KeyCode)(49 + j)) || PlayInput.GetKeyDown((KeyCode)(257 + j)))
					{
						Converse.Selected = j;
						Converse.Pick(j);
						break;
					}
				}
				if (PlayInput.GetKeyDown(KeyCode.Backspace) && Converse.Open && !Converse.EndAfter)
				{
					Dialogue.Back(Converse).Act();
				}
			}
		}

		private void CivilUpdate(Actor host, float dt)
		{
			CivilSetup();
			try
			{
				Births.Update(host, dt, Toast);
			}
			catch (Exception ex)
			{
				Log.Error("births: " + ex.Message);
			}
			try
			{
				Quests.Update(host, dt, Toast);
			}
			catch (Exception ex2)
			{
				Log.Error("quests: " + ex2.Message);
			}
			try
			{
				Law.Update(host, dt, Toast);
			}
			catch (Exception ex3)
			{
				Log.Error("law: " + ex3.Message);
			}
			try
			{
				Economy.Update(host, dt, Toast);
			}
			catch (Exception ex4)
			{
				Log.Error("economy: " + ex4.Message);
			}
			TownsUpdate(host, dt);
			try { Fishing.Update(host, dt, Toast); } catch {}
			try { TavernBoard.Tick(host, dt, Toast); } catch {}
			try { TavernBoard.Tick(host, dt, Toast); } catch {}
			UpdateService(host, dt);
			UpdateFamilyMap(host, dt);
			PeopleMet.KeepFamily(host, dt);
			if (!Converse.Open)
			{
				return;
			}
			Actor with = Converse.With;
			if (with == null || !with.isAlive() || host == null || Converse.You != host)
			{
				EndConversation();
				return;
			}
			if (((Converse.DistanceTo != null) ? Converse.DistanceTo(with) : 0f) > 7.5f)
			{
				EndConversation();
				Toast(PeopleMet.Label(host, with) + " walked away");
				return;
			}
			KeepTalking(host, with, dt);
			Converse.Update();
			if (Converse.EndAfter && Converse.Typed && Converse.Age > (float)Converse.Line.Length / 55f + 1.8f)
			{
				EndConversation();
			}
		}

		private void UpdateNews(float dt)
		{
			Actor host = _host;
			if (host == null || DeathTimer >= 0f)
			{
				return;
			}
			try
			{
				if (host.isAlive())
				{
					switch (WorldNews.Update(host, dt, Settings.NewsAlerts, AnyMenu))
					{
					case 2:
						_audio.PlayHorn();
						break;
					case 1:
						_audio.PlayRustle();
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("news: " + ex.Message);
			}
		}

		private bool BoardPrompt(Actor host, Vector2 pos)
		{
			Law.Board board = Law.Facing(pos, _view.Yaw);
			if (board == null)
			{
				return false;
			}
			Prompt = "E:Read the bounty board";
			if (PlayInput.GetKeyDown(KeyCode.E))
			{
				Law.OpenBoard = board;
				Law.BoardOpen = true;
				_boardOpenedFrame = Time.frameCount;
				_audio.PlayRustle();
			}
			return true;
		}

		private void AddCivilMarks(List<KeyValuePair<float, Color>> marks, Vector2 p)
		{
			foreach (Quest item in Quests.Log)
			{
				if (Quests.Heading(item, out var at))
				{
					Vector2 vector = at - p;
					if (vector.sqrMagnitude > 9f)
					{
						marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector.y, vector.x), (item.Stage == 1) ? new Color(1f, 0.95f, 0.55f) : new Color(0.95f, 0.74f, 0.2f)));
					}
				}
			}
			Actor marked = Converse.Marked;
			if (marked != null)
			{
				if (!marked.isAlive())
				{
					Converse.Marked = null;
				}
				else
				{
					Vector2 vector2 = marked.current_position - p;
					if (vector2.sqrMagnitude > 9f)
					{
						marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector2.y, vector2.x), new Color(1f, 0.55f, 0.75f)));
					}
				}
			}
			WorldNews.AddMarks(marks, p);
		}

		private void DrawCivil()
		{
			DropKeyboardControlIfAsked();
			if (ChoosingHeir)
			{
				_hud.DrawHeirs(this);
			}
			else if (!(DeathTimer >= 0f))
			{
				DrawService();
				DrawFamilyMap();
				if (Converse.Open)
				{
					_hud.DrawTalk(Converse);
					KeysShown(0);
				}
				else if (PlayerTalk.CardOpen)
				{
					_hud.DrawTalk(PlayerTalk.Card, PlayerTalk.Hint, Cursor.lockState != CursorLockMode.Locked);
				}
				if (WorldNews.Open)
				{
					_hud.DrawWorldNews();
				}
				if (Journal.Open)
				{
					_hud.DrawJournal();
				}
				DrawTowns();
				if (Hub.AnyWindowOpen)
				{
					KeysShown(3);
				}
				if (Law.BoardOpen && Law.OpenBoard != null)
				{
					_hud.DrawBoard(Law.OpenBoard, _host);
					KeysShown(1);
				}
				if (Births.Naming != null)
				{
					_hud.DrawNaming(_host);
					KeysShown(2);
				}
				if (Event.current.type == EventType.Repaint && Births.FadeLeft > 0f)
				{
					_hud.DrawFade(Births.FadeLeft / 3.2f);
				}
			}
		}

		private void FindHeirs(Actor dead)
		{
			Heirs.Clear();
			if (dead == null)
			{
				return;
			}
			_heirsOf = dead.data.id;
			_deadBody = dead;
			Hub.OnBodyDying(dead);
			try
			{
				_deadMoney = dead.money;
			}
			catch
			{
				_deadMoney = 0;
			}
			try
			{
				FamilyTree familyTree = Dialogue.TreeOf(dead);
				List<TreeNode> list = new List<TreeNode>(familyTree.Children);
				list.Sort((TreeNode a, TreeNode b) => a.Born.CompareTo(b.Born));
				foreach (TreeNode item in list)
				{
					Add(item);
				}
				Actor actor = null;
				try
				{
					actor = dead.lover;
				}
				catch
				{
				}
				foreach (TreeNode partner in familyTree.Partners)
				{
					bool flag = false;
					try
					{
						flag = partner.Actor != null && (partner.Actor.lover == dead || (actor != null && actor == partner.Actor));
					}
					catch
					{
					}
					if (flag)
					{
						Add(partner);
					}
				}
				foreach (TreeNode sibling in familyTree.Siblings)
				{
					Add(sibling);
				}
			}
			catch (Exception ex)
			{
				Log.Error("heirs: " + ex.Message);
			}
			if (Heirs.Count > 0)
			{
				Log.Info("heirs of " + Describe(dead) + ": " + string.Join(", ", Heirs.ConvertAll((TreeNode h) => h.Name + " (" + h.Role + ")").ToArray()));
			}
			void Add(TreeNode n)
			{
				if (n != null && !n.Dead && n.Actor != null && n.Actor.isAlive() && Heirs.Count < 4)
				{
					try
					{
						if (n.Actor.is_inside_boat || !n.Actor.canBePossessed())
						{
							return;
						}
					}
					catch
					{
						return;
					}
					foreach (TreeNode heir in Heirs)
					{
						if (heir.Id == n.Id)
						{
							return;
						}
					}
					Heirs.Add(n);
				}
			}
		}

		internal bool ContinueAs(int index)
		{
			if (index < 0 || index >= Heirs.Count)
			{
				return false;
			}
			TreeNode treeNode = Heirs[index];
			Actor actor = treeNode.Actor;
			bool flag = false;
			try
			{
				flag = actor != null && actor.isAlive() && actor.data != null && actor.data.id == treeNode.Id;
			}
			catch
			{
			}
			if (!flag)
			{
				Heirs.RemoveAt(index);
				return false;
			}
			if (!Possess.Swap(actor))
			{
				return false;
			}
			Run.PassTo(_deadBody, actor);
			try
			{
				if (_deadMoney > 0)
				{
					actor.addMoney(_deadMoney);
				}
			}
			catch
			{
			}
			Hub.OnBodyDied(_deadBody, actor);
			Chronicle.AddOwnMemory(actor, "fp_heir", DeathName);
			Toast("You carry on as " + treeNode.Name + ", your " + treeNode.Role + ((_deadMoney > 0) ? $". You inherit {_deadMoney} gold." : "."));
			Log.Info("heir: carrying on as " + Describe(actor));
			Heirs.Clear();
			return true;
		}

		private void MuteGameKeys()
		{
			if (_muteFailed || Time.frameCount - _muteChecked < 30)
			{
				return;
			}
			_muteChecked = Time.frameCount;
			try
			{
				foreach (HotkeyAsset item in AssetManager.hotkey_library.list)
				{
					if (item == null || item == HotkeyLibrary.cancel || item == HotkeyLibrary.control_unit || item == HotkeyLibrary.follow_unit)
					{
						continue;
					}
					if (item.just_pressed_action != null && (!_pressWraps.TryGetValue(item, out var value) || item.just_pressed_action != value))
					{
						HotkeyAction original = item.just_pressed_action;
						HotkeyAction hotkeyAction = delegate(HotkeyAsset a)
						{
							if (!GameKeysMuted)
							{
								original(a);
							}
						};
						_pressWraps[item] = hotkeyAction;
						item.just_pressed_action = hotkeyAction;
					}
					if (item.holding_action == null || (_holdWraps.TryGetValue(item, out var value2) && !(item.holding_action != value2)))
					{
						continue;
					}
					HotkeyAction original2 = item.holding_action;
					HotkeyAction hotkeyAction2 = delegate(HotkeyAsset a)
					{
						if (!GameKeysMuted)
						{
							original2(a);
						}
					};
					_holdWraps[item] = hotkeyAction2;
					item.holding_action = hotkeyAction2;
				}
			}
			catch (Exception ex)
			{
				_muteFailed = true;
				Log.Error("talk: could not hold back the game's hotkeys: " + ex.Message);
			}
		}

		private void UpdateClickGrace()
		{
			bool flag = Converse.Open || Law.BoardOpen || Births.Naming != null || ChoosingHeir;
			if (_clickUiWasOpen && !flag)
			{
				_clickUiClosedAt = Time.unscaledTime;
			}
			_clickUiWasOpen = flag;
			if (!PlayInput.GetMouseButton(0) && !PlayInput.GetMouseButtonDown(0))
			{
				_clickHeldOver = false;
			}
			else if (flag || Time.unscaledTime - _clickUiClosedAt < 0.5f)
			{
				_clickHeldOver = true;
			}
		}

		private void CompassKeys(bool menu)
		{
			bool num = _compassWanted || CompassKeyForTest;
			bool compassClickForTest = CompassClickForTest;
			_compassWanted = false;
			CompassKeyForTest = (CompassClickForTest = false);
			if (num && !menu && !IsTyping() && CanUseCompass)
			{
				if (!Compass.Out && Binoculars.Up)
				{
					Binoculars.Lower();
				}
				Compass.Toggle();
				if (Compass.Out)
				{
					_compassGatherAt = -10f;
				}
			}
			if (Compass.Out)
			{
				if (menu || !CanUseCompass || Binoculars.Up || Bandages.Wrapping)
				{
					Compass.PutAway();
				}
				else if (compassClickForTest || (!WheelClick && (PlayInput.GetMouseButtonDown(0) || PlayInput.GetMouseButtonDown(1))) || PlayInput.GetKeyDown(KeyCode.X))
				{
					Compass.PutAway();
					_compassShutFrame = Time.frameCount;
				}
			}
		}

		private bool CompassWheel(float scroll)
		{
			if (!Compass.Out)
			{
				return false;
			}
			GatherCompassMarks(now: true);
			int count = _compassMarks.Count;
			if (count < 2)
			{
				return true;
			}
			int num = (CompassIndex + ((scroll > 0f) ? 1 : (-1)) + count) % count;
			_compassPick = ((num == 0) ? null : _compassMarks[num].Key);
			_compassAuto = _compassMarks[0].Key;
			PickCompassMark();
			_audio.PlayEffect("compass_tick", 0.35f);
			return true;
		}

		private void BookmarkKeys(bool menu)
		{
			if (menu || IsTyping() || _host == null || !CanUseCompass)
				return;
			bool shift = PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift);
			bool ctrl = PlayInput.GetKey(KeyCode.LeftControl) || PlayInput.GetKey(KeyCode.RightControl);
			if (PlayInput.GetKeyDown(KeyCode.K))
			{
				if (ctrl && shift)
				{
					UserWaypoints.Clear();
					Toast("All bookmarks cleared");
					_compassGatherAt = -10f;
				}
				else if (shift)
				{
					if (UserWaypoints.RemoveNearest(_host.current_position, 12f))
					{
						Toast("Bookmark removed");
						_compassGatherAt = -10f;
					}
					else
					{
						Toast("No bookmark nearby");
					}
				}
				else
				{
					Vector2 at = _host.current_position;
					try
					{
						if (Picked is Actor a && a.isAlive())
							at = a.current_position;
						else if (Picked is Building b)
						{
							var tile = b.getTile();
							if (tile != null) at = new Vector2(tile.posV3.x, tile.posV3.y);
						}
					}
					catch { }
					var bm = UserWaypoints.Add(at);
					Toast($"Bookmark \"{bm.Name}\" added (K to add, Shift+K remove)");
					_compassGatherAt = -10f;
				}
			}
		}

		private void HelmetKeys(bool menu)
		{
			if (menu || IsTyping() || _host == null)
				return;
			if (PlayInput.GetKeyDown(KeyCode.L))
			{
				HelmetLight.Toggle(_host, Toast);
			}
		}

		private void PhotoKeys(bool menu)
		{
			if (IsTyping()) return;
			if (PlayInput.GetKeyDown(KeyCode.F10))
			{
				PhotoMode.Toggle(Toast);
			}
			PhotoMode.Update(menu);
		}

		private void UpdateCompass(float dt)
		{
			if (_host != _compassHost)
			{
				Compass.Drop();
				_compassHost = _host;
				_compassPick = (_compassAuto = null);
				try { HelmetLight.SetForActor(_host); } catch {}
			}
			else if (!CanUseCompass && Compass.Active)
			{
				Compass.Drop();
			}
			Compass.Update(dt);
			if (Compass.Popped)
			{
				_audio.PlayEffect("compass_open", 0.5f);
			}
			if (Compass.Shut)
			{
				_audio.PlayEffect("compass_shut", 0.55f);
			}
			if (!Compass.Active || _host == null)
			{
				Compass.Target = float.NaN;
				return;
			}
			GatherCompassMarks(now: false);
			Vector2 current_position = _host.current_position;
			CompassMark? compassOn = CompassOn;
			if (compassOn.HasValue)
			{
				CompassMark valueOrDefault = compassOn.GetValueOrDefault();
				if (valueOrDefault.Actor != null)
				{
					try
					{
						if (valueOrDefault.Actor.isAlive())
						{
							valueOrDefault.At = valueOrDefault.Actor.current_position;
						}
					}
					catch
					{
					}
				}
				Vector2 vector = valueOrDefault.At - current_position;
				Compass.Target = ((vector.sqrMagnitude > 0.04f) ? Mathf.Atan2(vector.y, vector.x) : Compass.Target);
				valueOrDefault.Distance = vector.magnitude;
				CompassOn = valueOrDefault;
			}
			else
			{
				Compass.Target = float.NaN;
			}
		}

		private void DropCompass()
		{
			Compass.Drop();
			CompassOn = null;
		}

		private void GatherCompassMarks(bool now)
		{
			if (!now && Time.unscaledTime - _compassGatherAt < 0.25f)
			{
				return;
			}
			_compassGatherAt = Time.unscaledTime;
			_compassMarks.Clear();
			if (_host == null)
			{
				CompassOn = null;
				return;
			}
			Vector2 me = _host.current_position;
			try
			{
				foreach (Quest item in Quests.Log)
				{
					if (Quests.Heading(item, out var at))
					{
						bool flag = item.Stage == 1;
						Actor actor = Chronicle.Find(flag ? item.Giver : item.Target);
						string text = item.Giver.ToString();
						int kind = (int)item.Kind;
						Add("q" + text + "/" + kind, at, actor, Quests.Status(item), flag ? new Color(1f, 0.95f, 0.55f) : new Color(0.95f, 0.74f, 0.2f));
					}
				}
			}
			catch
			{
			}
			try
			{
				Order orders = Orders;
				if (orders != null && orders.HasWhere)
				{
					Add("o" + orders.Kind, orders.Where, null, orders.Title, (orders.Kind == OrderKind.Patrol) ? new Color(1f, 0.78f, 0.6f) : new Color(1f, 0.4f, 0.3f));
				}
			}
			catch
			{
			}
			try
			{
				foreach (JournalEntry item2 in Hub.JournalEntries())
				{
					if (item2 != null && item2.HasWay)
					{
						Add("e" + item2.Key, item2.Way, null, (item2.Ready ? "Done  ·  " : "") + item2.Title, item2.Ready ? new Color(1f, 0.95f, 0.55f) : new Color(0.95f, 0.74f, 0.2f));
					}
				}
			}
			catch
			{
			}
			for (int i = 0; i < Pings.List.Count; i++)
			{
				Ping ping = Pings.List[i];
				if (ping.Kind == PingKind.Go)
				{
					Vector3 vector = Pings.Where(ping);
					Add("g" + ping.Born.ToString("R"), new Vector2(vector.x, vector.y), null, Pings.Name(PingKind.Go), Pings.ColorOf(PingKind.Go));
				}
			}
			try
			{
				UserWaypoints.EnsureLoaded();
				foreach (var bm in UserWaypoints.All)
				{
					Add("b" + bm.Id, new Vector2(bm.X, bm.Y), null, bm.Name, UserWaypoints.ColorOf(bm));
				}
			}
			catch
			{
			}
			Actor marked = Converse.Marked;
			try
			{
				if (marked != null && marked.isAlive())
				{
					Add("m" + marked.data.id, marked.current_position, marked, Converse.MarkedName ?? Describe(marked), new Color(1f, 0.55f, 0.75f));
				}
			}
			catch
			{
			}
			if (_compassMarks.Count == 0)
			{
				try
				{
					City city = _host.city;
					WorldTile worldTile = city?.getTile();
					if (worldTile != null)
					{
						Add("h" + city.data.id, new Vector2(worldTile.posV3.x, worldTile.posV3.y), null, Renames.Apply(city.name), new Color(0.45f, 1f, 0.45f));
					}
				}
				catch
				{
				}
			}
			_compassMarks.Sort((CompassMark a, CompassMark b) => a.Distance.CompareTo(b.Distance));
			PickCompassMark();
			void Add(string key, Vector2 vector2, Actor actor2, string name, Color color)
			{
				if (actor2 != null)
				{
					try
					{
						if (!actor2.isAlive() || (actor2.current_position - vector2).sqrMagnitude > 1f)
						{
							actor2 = null;
						}
					}
					catch
					{
						actor2 = null;
					}
				}
				_compassMarks.Add(new CompassMark
				{
					Key = key,
					At = vector2,
					Actor = actor2,
					Name = name,
					Color = color,
					Distance = (vector2 - me).magnitude
				});
			}
		}

		private void PickCompassMark()
		{
			if (_compassMarks.Count == 0)
			{
				CompassOn = null;
				CompassIndex = 0;
				return;
			}
			int num = -1;
			if (_compassPick != null)
			{
				num = _compassMarks.FindIndex((CompassMark m) => m.Key == _compassPick);
			}
			if (num < 0)
			{
				_compassPick = null;
				num = 0;
				int num2 = ((_compassAuto != null) ? _compassMarks.FindIndex((CompassMark m) => m.Key == _compassAuto) : (-1));
				if (num2 > 0 && _compassMarks[num2].Distance < _compassMarks[0].Distance * 1.15f + 3f)
				{
					num = num2;
				}
				_compassAuto = _compassMarks[num].Key;
			}
			CompassIndex = num;
			CompassOn = _compassMarks[num];
		}

		private void GuardStartBegin()
		{
			if (!SafeStart)
			{
				CrashGuard.Enter("start");
			}
		}

		private void OnApplicationPause(bool paused)
		{
			if (paused)
			{
				CrashGuard.LeaveAll();
				Detour.End();
			}
		}

		private void GuardStartTick()
		{
			if (_guardStartDone)
			{
				return;
			}
			if (!Config.game_loaded || World.world == null || WorldLoading)
			{
				_guardLoadedAt = -1f;
				return;
			}
			if (_guardLoadedAt < 0f)
			{
				_guardLoadedAt = Time.unscaledTime;
			}
			if (Time.unscaledTime - _guardLoadedAt < 10f)
			{
				return;
			}
			_guardStartDone = true;
			if (!SafeStart)
			{
				CrashGuard.Leave("start");
			}
			string text = CrashGuard.Notice();
			if (text != null)
			{
				Log.Info("crash guard: told the player \"" + text + "\"");
				if (_active)
				{
					ToastText = text;
					ToastUntil = Time.unscaledTime + 8f;
				}
				else
				{
					_godToast = text;
					_godToastUntil = Time.unscaledTime + 8f;
				}
			}
		}

		private void GpuMake()
		{
			if (_gpu == null)
			{
				CrashGuard.Enter("gpu");
				_gpu = new GpuRenderer();
			}
		}

		private void GpuProven(bool room)
		{
			if (_gpuProven < 120 && ++_gpuProven == 120)
			{
				CrashGuard.Leave("gpu");
			}
			if (!room && Time.frameCount - _gpuRoomFrame > 30)
			{
				CrashGuard.Leave("gpurooms");
			}
		}

		private void GpuRoomMarked()
		{
			_gpuRoomFrame = Time.frameCount;
			CrashGuard.Enter("gpurooms");
		}

		private bool CrowdGearNow()
		{
			if (Settings == null || Settings.CrowdDetail < 1 || Settings.CrowdDetail > 2)
			{
				return CrowdLineForTest >= 0f;
			}
			return true;
		}

		private float CrowdPixelFor(ViewState camera)
		{
			if (Renderer.Width <= 0 || Renderer.Height <= 0)
			{
				return 0f;
			}
			return Mathf.Tan(camera.Fov * 0.5f) / ((float)Renderer.Width * 0.5f) * (float)Renderer.Height / 1080f;
		}

		private float CrowdLineNow(bool godView)
		{
			if (CrowdLineForTest >= 0f)
			{
				if (!GpuWanted || _gpu == null || !_gpu.CrowdsReady)
				{
					return 0f;
				}
				return CrowdLineForTest;
			}
			if (!GpuWanted || _gpu == null || !_gpu.CrowdsReady)
			{
				return 0f;
			}
			int num = ((Settings != null) ? Settings.CrowdDetail : 0);
			float num2 = ((num > 0 && num < CrowdLines.Length) ? CrowdLines[num] : 0f);
			if (godView && !GodLineOff)
			{
				num2 = Mathf.Max(num2, 8f);
			}
			return num2;
		}

		internal static string ResolutionName(int width)
		{
			return width + "x" + Mathf.Max(90, Mathf.RoundToInt((float)(width * Screen.height) / (float)Mathf.Max(1, Screen.width)));
		}

		private string ResolutionToast(bool onCpu)
		{
			int num = ResolutionWidths[Mathf.Clamp(Settings.Resolution, 0, ResolutionWidths.Length - 1)];
			string text = "Resolution " + ResolutionName(num);
			if (!onCpu || num <= 640)
			{
				return text;
			}
			return text + " (CPU " + ResolutionName(640) + ")";
		}

		private int RenderWidth(bool onCard)
		{
			int num = ResolutionWidths[Mathf.Clamp(DrawnResolution, 0, ResolutionWidths.Length - 1)];
			if (!onCard)
			{
				return Mathf.Min(num, 640);
			}
			return num;
		}

		private Vector2 PaceCentre()
		{
			if (Cabin.Inside || Mine.Inside || Interior.Inside)
			{
				try
				{
					if (_host != null && _host.isAlive())
					{
						return _host.current_position;
					}
				}
				catch
				{
				}
			}
			return new Vector2(_view.X, _view.Y);
		}

		private void SteepAround(ViewState camera, int width, int height, float ground)
		{
			if (camera.Free && Lens.Of(in camera, (float)height / (float)Mathf.Max(1, width)).Steep)
			{
				SceneCollector.AllAround = Mathf.Max(SceneCollector.AllAround, Mathf.Min(24f, 2f + Mathf.Max(0f, camera.Z - ground) * 4f));
			}
		}

		private static float LookSlope(float degrees)
		{
			return Mathf.Tan(Mathf.Clamp(degrees, -89f, 89f) * (MathF.PI / 180f));
		}

		internal void ViewForTest(bool on)
		{
			_viewEnabled = on;
		}

		internal void ResetPerf()
		{
			_perf.Reset();
		}

		internal SpriteImage SpriteForTest(Actor a)
		{
			return SpriteOf(a);
		}

		internal SpriteImage SpriteOf(Actor a)
		{
			try
			{
				return _sprites.Get(a.getSpriteToRender());
			}
			catch
			{
				return null;
			}
		}

		public void SetYaw(float radians)
		{
			_view.Yaw = radians;
		}

		public void SetPitchDegrees(float degrees)
		{
			_pitchDegrees = degrees;
		}

		private void Awake()
		{
			if (StayOffOnOldGame() || StandAsideForNml())
			{
				return;
			}
			Instance = this;
			SteamGlue.FindSteamLibrary();
			StartQuietTestCopy();
			Settings = Settings.Load();
			if (Settings.Fresh && !File.Exists(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest")))
			{
				ChooseFirstResolution();
			}
			_hud = new Hud(this);
			if (Settings.Upgraded)
			{
				Settings.Save();
			}
			Log.Info("loaded, version " + typeof(WorldBoxMod).Assembly.GetName().Version);
			CrashGuard.Report();
			GuardStartBegin();
			FrameGuard.Install();
			base.gameObject.AddComponent<HitchClock>();
			base.gameObject.AddComponent<WorldStepClock>();
			Hitch.RaisePoolFloor();
			if (!SafeStart)
			{
				Hitch.InstallLoopMarkers();
			}
			if (Detour.Platform == Detour.Kind.WindowsX64 || Detour.Hooker != null)
			{
				try
				{
					ZombieGear.Install();
				}
				catch (Exception ex)
				{
					Log.Error("could not take over a creature's transformation, zombies rise without the armour they wore: " + ex.Message);
				}
			}
			Models.Preload();
			FirstPersonAudio.Prepare();
			PacksStart();
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			if (File.Exists(path))
			{
				string text = File.ReadAllText(path);
				if (text.Contains("noinsidemove"))
				{
					InsideSpots.MoveBodies = false;
				}
				if (text.Contains("nokept"))
				{
					RoomWalkers.KeepInsideSpots = false;
				}
				if (text.Contains("nolooks"))
				{
					People.LooksOn = false;
				}
				if (text.Contains("timelooks"))
				{
					People.TimeLooks = true;
				}
				if (text.Contains("nomerge"))
				{
					GpuModels.MergeLooks = false;
				}
				if (text.Contains("nofight"))
				{
					RoomCombat.Enabled = false;
				}
				if (text.Contains("nosimlod"))
				{
					SimLod.Off = true;
				}
				if (text.Contains("nochunks"))
				{
					SceneCollector.ChunkWalkOff = true;
				}
				if (text.Contains("chunkwalk"))
				{
					SceneCollector.ChunkWalkForTest = true;
				}
				if (text.Contains("chunkcheck"))
				{
					SceneCollector.ChunkCheck = true;
				}
				_noDive = true;
				Patrons.Off = !text.Contains("freedom");
				ReliefWorld.AutoAnswer = !text.Contains("landcard");
				CrownOffer.AutoAnswer = !text.Contains("crown");
				BesideAllowed = text.Contains("async");
				ScreenSync.Allowed = text.Contains("vsync");
				if (text.Contains("netplay"))
				{
					base.gameObject.AddComponent<NetTest>();
				}
				else if (text.Contains("assets"))
				{
					base.gameObject.AddComponent<AssetDump>();
				}
				else if (text.Contains("packs"))
				{
					base.gameObject.AddComponent<PacksTest>();
				}
				else if (text.Contains("burnt"))
				{
					base.gameObject.AddComponent<BurntTest>();
				}
				else if (text.Contains("trees"))
				{
					base.gameObject.AddComponent<TreesTest>();
				}
				else if (text.Contains("bushes"))
				{
					base.gameObject.AddComponent<BushesTest>();
				}
				else if (text.Contains("stones"))
				{
					base.gameObject.AddComponent<RocksTest>();
				}
				else if (text.Contains("roomfps"))
				{
					base.gameObject.AddComponent<RoomPerfTest>();
				}
				else if (text.Contains("godperf"))
				{
					base.gameObject.AddComponent<GodPerfTest>();
				}
				else if (text.Contains("houseperf"))
				{
					base.gameObject.AddComponent<HousePerfTest>();
				}
				else if (text.Contains("roomlight"))
				{
					base.gameObject.AddComponent<RoomLightTest>();
				}
				else if (text.Contains("shadowtune"))
				{
					base.gameObject.AddComponent<ShadowTest>();
				}
				else if (text.Contains("sunpath"))
				{
					base.gameObject.AddComponent<SunPathTest>();
				}
				else if (text.Contains("homeroom"))
				{
					base.gameObject.AddComponent<HomeRoomTest>();
				}
				else if (text.Contains("inventory"))
				{
					base.gameObject.AddComponent<InventoryTest>();
				}
				else if (text.Contains("storage"))
				{
					base.gameObject.AddComponent<StorageTest>();
				}
				else if (text.Contains("forging"))
				{
					base.gameObject.AddComponent<ForgeTest>();
				}
				else if (text.Contains("workbench"))
				{
					base.gameObject.AddComponent<WorkbenchTest>();
				}
				else if (text.Contains("stress"))
				{
					base.gameObject.AddComponent<StressTest>();
				}
				else if (text.Contains("cityperf"))
				{
					base.gameObject.AddComponent<CityPerfTest>();
				}
				else if (text.Contains("gpu"))
				{
					base.gameObject.AddComponent<GpuTest>();
				}
				else if (text.Contains("ufofx"))
				{
					base.gameObject.AddComponent<UfoTest>();
				}
				else if (text.Contains("tornado"))
				{
					base.gameObject.AddComponent<TornadoTest>();
				}
				else if (text.Contains("snow"))
				{
					base.gameObject.AddComponent<SnowTest>();
				}
				else if (text.Contains("seawaves"))
				{
					base.gameObject.AddComponent<SeaWavesTest>();
				}
				else if (text.Contains("seabed"))
				{
					base.gameObject.AddComponent<SeabedTest>();
				}
				else if (text.Contains("swell"))
				{
					base.gameObject.AddComponent<SwellTest>();
				}
				else if (text.Contains("boatstack"))
				{
					base.gameObject.AddComponent<BoatStackTest>();
				}
				else if (text.Contains("wade"))
				{
					base.gameObject.AddComponent<WadeTest>();
				}
				else if (text.Contains("glitter"))
				{
					base.gameObject.AddComponent<GlitterTest>();
				}
				else if (text.Contains("ports"))
				{
					base.gameObject.AddComponent<PortsTest>();
				}
				else if (text.Contains("biomefx"))
				{
					base.gameObject.AddComponent<BiomeFxTest>();
				}
				else if (text.Contains("cannons"))
				{
					base.gameObject.AddComponent<CannonTest>();
				}
				else if (text.Contains("drill"))
				{
					base.gameObject.AddComponent<DrillTest>();
				}
				else if (text.Contains("showreel"))
				{
					base.gameObject.AddComponent<ShowreelTest>();
				}
				else if (text.Contains("showcase"))
				{
					base.gameObject.AddComponent<ShowcaseTest>();
				}
				else if (text.Contains("skirmishreel"))
				{
					base.gameObject.AddComponent<WarReelTest>();
				}
				else if (text.Contains("playreel"))
				{
					base.gameObject.AddComponent<PlayReelTest>();
				}
				else if (text.Contains("beside"))
				{
					base.gameObject.AddComponent<BesideTest>();
				}
				else if (text.Contains("faraway"))
				{
					base.gameObject.AddComponent<FarCreaturesTest>();
				}
				else if (text.Contains("mapsizes"))
				{
					base.gameObject.AddComponent<MapSizesTest>();
				}
				else if (text.Contains("farsight"))
				{
					base.gameObject.AddComponent<FarSightTest>();
				}
				else if (text.Contains("binoculars"))
				{
					base.gameObject.AddComponent<BinocularsTest>();
				}
				else if (text.Contains("compass"))
				{
					base.gameObject.AddComponent<CompassTest>();
				}
				else if (text.Contains("pings"))
				{
					base.gameObject.AddComponent<PingsTest>();
				}
				else if (text.Contains("hudart"))
				{
					base.gameObject.AddComponent<HudArtTest>();
				}
				else if (text.Contains("popups"))
				{
					base.gameObject.AddComponent<PopupsTest>();
				}
				else if (text.Contains("monolith"))
				{
					base.gameObject.AddComponent<MonolithTest>();
				}
				else if (text.Contains("wonders"))
				{
					base.gameObject.AddComponent<WondersTest>();
				}
				else if (text.Contains("kingorders"))
				{
					base.gameObject.AddComponent<KingOrdersTest>();
				}
				else if (text.Contains("kingui"))
				{
					base.gameObject.AddComponent<KingUiTest>();
				}
				else if (text.Contains("warflags"))
				{
					base.gameObject.AddComponent<WarFlagsTest>();
				}
				else if (text.Contains("warchest"))
				{
					base.gameObject.AddComponent<WarChestTest>();
				}
				else if (text.Contains("fleet"))
				{
					base.gameObject.AddComponent<FleetTest>();
				}
				else if (text.Contains("warview"))
				{
					base.gameObject.AddComponent<WarViewTest>();
				}
				else if (text.Contains("sceneview"))
				{
					base.gameObject.AddComponent<SceneTest>();
				}
				else if (text.Contains("rtsorders"))
				{
					base.gameObject.AddComponent<RtsOrdersTest>();
				}
				else if (text.Contains("hijack"))
				{
					base.gameObject.AddComponent<HijackTest>();
				}
				else if (text.Contains("ferry"))
				{
					base.gameObject.AddComponent<FerryTest>();
				}
				else if (text.Contains("calendar"))
				{
					base.gameObject.AddComponent<CalendarTest>();
				}
				else if (text.Contains("land"))
				{
					base.gameObject.AddComponent<LandTest>();
				}
				else if (text.Contains("quakes"))
				{
					base.gameObject.AddComponent<QuakeTest>();
				}
				else if (text.Contains("relief"))
				{
					base.gameObject.AddComponent<ReliefTest>();
				}
				else if (text.Contains("shovel"))
				{
					base.gameObject.AddComponent<ShovelTest>();
				}
				else if (text.Contains("basins"))
				{
					base.gameObject.AddComponent<BasinsTest>();
				}
				else if (text.Contains("species"))
				{
					base.gameObject.AddComponent<SpeciesTest>();
				}
				else if (text.Contains("sites"))
				{
					base.gameObject.AddComponent<SiteTest>();
				}
				else if (text.Contains("handlook"))
				{
					base.gameObject.AddComponent<HandLookTest>();
				}
				else if (text.Contains("handskin"))
				{
					base.gameObject.AddComponent<HandSkinTest>();
				}
				else if (text.Contains("mutations"))
				{
					base.gameObject.AddComponent<MutationsTest>();
				}
				else if (text.Contains("fauna"))
				{
					base.gameObject.AddComponent<FaunaTest>();
				}
				else if (text.Contains("heldloads"))
				{
					base.gameObject.AddComponent<HeldLoadsTest>();
				}
				else if (text.Contains("bodies"))
				{
					base.gameObject.AddComponent<BodiesTest>();
				}
				else if (text.Contains("chronicle"))
				{
					base.gameObject.AddComponent<ChronicleTest>();
				}
				else if (text.Contains("talks"))
				{
					base.gameObject.AddComponent<TalksTest>();
				}
				else if (text.Contains("voices"))
				{
					base.gameObject.AddComponent<VoicesTest>();
				}
				else if (text.Contains("slowyears"))
				{
					base.gameObject.AddComponent<PaceTest>();
				}
				else if (text.Contains("mapclock"))
				{
					base.gameObject.AddComponent<MapClockTest>();
				}
				else if (text.Contains("maphouses"))
				{
					base.gameObject.AddComponent<MapHousesTest>();
				}
				else if (text.Contains("insidespots"))
				{
					base.gameObject.AddComponent<InsideTest>();
				}
				else if (text.Contains("peek"))
				{
					base.gameObject.AddComponent<PeekTest>();
				}
				else if (text.Contains("realacts"))
				{
					base.gameObject.AddComponent<RealActsTest>();
				}
				else if (text.Contains("roofdive"))
				{
					base.gameObject.AddComponent<RoofDiveTest>();
				}
				else if (text.Contains("owperf"))
				{
					base.gameObject.AddComponent<OneWorldPerfTest>();
				}
				else if (text.Contains("finding"))
				{
					base.gameObject.AddComponent<FindingTest>();
				}
				else if (text.Contains("favping"))
				{
					base.gameObject.AddComponent<FavPingTest>();
				}
				else if (text.Contains("hello"))
				{
					base.gameObject.AddComponent<HelloTest>();
				}
				else if (text.Contains("births"))
				{
					base.gameObject.AddComponent<BirthsTest>();
				}
				else if (text.Contains("quests"))
				{
					base.gameObject.AddComponent<QuestsTest>();
				}
				else if (text.Contains("bounty"))
				{
					base.gameObject.AddComponent<BountyTest>();
				}
				else if (text.Contains("qlevels"))
				{
					base.gameObject.AddComponent<QuestLevelsTest>();
				}
				else if (text.Contains("magicpage"))
				{
					base.gameObject.AddComponent<MagicPageTest>();
				}
				else if (text.Contains("economy"))
				{
					base.gameObject.AddComponent<EconomyTest>();
				}
				else if (text.Contains("hallstore"))
				{
					base.gameObject.AddComponent<HallStoreTest>();
				}
				else if (text.Contains("belong"))
				{
					base.gameObject.AddComponent<BelongTest>();
				}
				else if (text.Contains("spatial"))
				{
					base.gameObject.AddComponent<SpatialTest>();
				}
				else if (text.Contains("mumble"))
				{
					base.gameObject.AddComponent<MumbleTest>();
				}
				else if (text.Contains("news"))
				{
					base.gameObject.AddComponent<NewsTest>();
				}
				else if (text.Contains("renames"))
				{
					base.gameObject.AddComponent<RenamesTest>();
				}
				else if (text.Contains("service"))
				{
					base.gameObject.AddComponent<ServiceTest>();
				}
				else if (text.Contains("brief"))
				{
					base.gameObject.AddComponent<BriefTest>();
				}
				else if (text.Contains("romance"))
				{
					base.gameObject.AddComponent<RomanceTest>();
				}
				else if (text.Contains("rivals"))
				{
					base.gameObject.AddComponent<RivalsTest>();
				}
				else if (text.Contains("slice"))
				{
					base.gameObject.AddComponent<SliceTest>();
				}
				else if (text.Contains("torch"))
				{
					base.gameObject.AddComponent<TorchTest>();
				}
				else if (text.Contains("lamps"))
				{
					base.gameObject.AddComponent<LampsTest>();
				}
				else if (text.Contains("flamesword"))
				{
					base.gameObject.AddComponent<FlameSwordTest>();
				}
				else if (text.Contains("weapons"))
				{
					base.gameObject.AddComponent<WeaponsTest>();
				}
				else if (text.Contains("journal"))
				{
					base.gameObject.AddComponent<JournalTest>();
				}
				else if (text.Contains("menus"))
				{
					base.gameObject.AddComponent<MenusTest>();
				}
				else if (text.Contains("freezes") || text.Contains("keysfree"))
				{
					base.gameObject.AddComponent<FreezeTest>();
				}
				else if (text.Contains("letgo"))
				{
					base.gameObject.AddComponent<LetGoTest>();
				}
				else if (text.Contains("menukeys"))
				{
					base.gameObject.AddComponent<MenuKeysTest>();
				}
				else if (text.Contains("civilhud"))
				{
					base.gameObject.AddComponent<CivilHudTest>();
				}
				else if (text.Contains("lang"))
				{
					base.gameObject.AddComponent<LangTest>();
				}
				else if (text.Contains("freefly"))
				{
					base.gameObject.AddComponent<FreeFlyTest>();
				}
				else if (text.Contains("godsky"))
				{
					base.gameObject.AddComponent<GodSkyTest>();
				}
				else if (text.Contains("godwalls"))
				{
					base.gameObject.AddComponent<GodWallsTest>();
				}
				else if (text.Contains("godview"))
				{
					base.gameObject.AddComponent<GodViewTest>();
				}
				else if (text.Contains("godturn"))
				{
					base.gameObject.AddComponent<GodTurnTest>();
				}
				else if (text.Contains("looks"))
				{
					base.gameObject.AddComponent<LooksTest>();
				}
				else if (text.Contains("look"))
				{
					base.gameObject.AddComponent<LookTest>();
				}
				else if (text.Contains("succession"))
				{
					base.gameObject.AddComponent<SuccessionTest>();
				}
				else if (text.Contains("settlers"))
				{
					base.gameObject.AddComponent<SettlersTest>();
				}
				else if (text.Contains("boatfix"))
				{
					base.gameObject.AddComponent<BoatFixTest>();
				}
				else if (text.Contains("king"))
				{
					base.gameObject.AddComponent<KingTest>();
				}
				else if (text.Contains("gallery"))
				{
					base.gameObject.AddComponent<Gallery>();
				}
				else if (text.Contains("polish"))
				{
					base.gameObject.AddComponent<PolishTest>();
				}
				else if (text.Contains("npc"))
				{
					base.gameObject.AddComponent<NpcTest>();
				}
				else if (text.Contains("abilities"))
				{
					base.gameObject.AddComponent<AbilityTest>();
				}
				else if (text.Contains("speed"))
				{
					base.gameObject.AddComponent<SpeedTest>();
				}
				else if (text.Contains("feel"))
				{
					base.gameObject.AddComponent<FeelTest>();
				}
				else if (text.Contains("weight"))
				{
					base.gameObject.AddComponent<FeelTest>();
				}
				else if (text.Contains("measure"))
				{
					base.gameObject.AddComponent<SizesTest>();
				}
				else if (text.Contains("features"))
				{
					base.gameObject.AddComponent<FeatureTest>();
				}
				else if (text.Contains("update"))
				{
					base.gameObject.AddComponent<UpdateTest>();
				}
				else if (text.Contains("ruins"))
				{
					base.gameObject.AddComponent<RuinTest>();
				}
				else if (text.Contains("archery"))
				{
					base.gameObject.AddComponent<ArcheryTest>();
				}
				else if (text.Contains("shots"))
				{
					base.gameObject.AddComponent<ShotsTest>();
				}
				else if (text.Contains("character"))
				{
					base.gameObject.AddComponent<CharacterTest>();
				}
				else if (text.Contains("familybugs"))
				{
					base.gameObject.AddComponent<FamilyTest>();
				}
				else if (text.Contains("friendly"))
				{
					base.gameObject.AddComponent<FriendlyFireTest>();
				}
				else if (text.Contains("monsterlaw"))
				{
					base.gameObject.AddComponent<MonsterLawTest>();
				}
				else if (text.Contains("hurtfrom"))
				{
					base.gameObject.AddComponent<HurtFromTest>();
				}
				else if (text.Contains("blocks"))
				{
					base.gameObject.AddComponent<BlocksTest>();
				}
				else if (text.Contains("guard"))
				{
					base.gameObject.AddComponent<GuardTest>();
				}
				else if (text.Contains("headshot"))
				{
					base.gameObject.AddComponent<HeadshotTest>();
				}
				else if (text.Contains("toolwork"))
				{
					base.gameObject.AddComponent<ToolWorkTest>();
				}
				else if (text.Contains("clarity"))
				{
					base.gameObject.AddComponent<ClarityTest>();
				}
				else if (text.Contains("variants"))
				{
					base.gameObject.AddComponent<VariantTest>();
				}
				else if (text.Contains("boats"))
				{
					base.gameObject.AddComponent<BoatTest>();
				}
				else if (text.Contains("perf"))
				{
					base.gameObject.AddComponent<PerfTest>();
				}
				else if (text.Contains("biomes"))
				{
					base.gameObject.AddComponent<BiomeTest>();
				}
				else if (text.Contains("crabzilla"))
				{
					base.gameObject.AddComponent<CrabzillaTest>();
				}
				else if (text.Contains("nightfolk"))
				{
					base.gameObject.AddComponent<NightFolkTest>();
				}
				else if (text.Contains("zombiegear"))
				{
					base.gameObject.AddComponent<ZombieGearTest>();
				}
				else if (text.Contains("armorsets"))
				{
					base.gameObject.AddComponent<ArmorTest>();
				}
				else if (text.Contains("regalia"))
				{
					base.gameObject.AddComponent<RegaliaTest>();
				}
				else if (text.Contains("hallflags"))
				{
					base.gameObject.AddComponent<HallFlagsTest>();
				}
				else if (text.Contains("blasts"))
				{
					base.gameObject.AddComponent<BlastTest>();
				}
				else if (text.Contains("lockin"))
				{
					base.gameObject.AddComponent<LockInTest>();
				}
				else if (text.Contains("teachers"))
				{
					base.gameObject.AddComponent<TeachersTest>();
				}
				else if (text.Contains("greatwar"))
				{
					base.gameObject.AddComponent<GreatWarTest>();
				}
				else if (text.Contains("bag"))
				{
					base.gameObject.AddComponent<BagTest>();
				}
				else if (text.Contains("bandages"))
				{
					base.gameObject.AddComponent<BandagesTest>();
				}
				else if (text.Contains("motion"))
				{
					base.gameObject.AddComponent<MotionTest>();
				}
				else if (text.Contains("settings"))
				{
					base.gameObject.AddComponent<SettingsTest>();
				}
				else if (text.Contains("hops"))
				{
					base.gameObject.AddComponent<HopsTest>();
				}
				else if (text.Contains("climb"))
				{
					base.gameObject.AddComponent<ClimbTest>();
				}
				else if (text.Contains("stairs"))
				{
					base.gameObject.AddComponent<StairsTest>();
				}
				else if (text.Contains("hitboxes"))
				{
					base.gameObject.AddComponent<HitboxTest>();
				}
				else if (text.Contains("stuckhunt"))
				{
					base.gameObject.AddComponent<StuckTest>();
				}
				else if (text.Contains("herostatues"))
				{
					base.gameObject.AddComponent<StatueLookTest>();
				}
				else if (text.Contains("tunes"))
				{
					base.gameObject.AddComponent<MusicTest>();
				}
				else if (text.Contains("tearing"))
				{
					base.gameObject.AddComponent<TearTest>();
				}
				else if (text.Contains("drags"))
				{
					base.gameObject.AddComponent<DragTest>();
				}
				else if (text.Contains("walkpace"))
				{
					base.gameObject.AddComponent<WalkPaceTest>();
				}
				else if (text.Contains("vrhands"))
				{
					base.gameObject.AddComponent<VrHandsTest>();
				}
				else if (text.Contains("vrui"))
				{
					Log.Error("SELFTEST: VR UI test was not recovered from the decompiled source and is disabled");
				}
				else if (text.Contains("vrsim"))
				{
					base.gameObject.AddComponent<VrTest>();
				}
				else if (text.Contains("tilt"))
				{
					base.gameObject.AddComponent<TiltTest>();
				}
				else if (text.Contains("hunger"))
				{
					base.gameObject.AddComponent<HungerTest>();
				}
				else if (text.Contains("ownmind"))
				{
					base.gameObject.AddComponent<OwnMindTest>();
				}
				else if (text.Contains("drift"))
				{
					base.gameObject.AddComponent<DriftTest>();
				}
				else if (text.Contains("shotwalls"))
				{
					base.gameObject.AddComponent<ShotWallsTest>();
				}
				else if (text.Contains("shelter"))
				{
					base.gameObject.AddComponent<ShelterTest>();
				}
				else if (text.Contains("roomjump"))
				{
					base.gameObject.AddComponent<RoomJumpTest>();
				}
				else if (text.Contains("roomfight"))
				{
					base.gameObject.AddComponent<RoomFightTest>();
				}
				else if (text.Contains("dragons"))
				{
					base.gameObject.AddComponent<DragonTest>();
				}
				else if (text.Contains("gamepad"))
				{
					base.gameObject.AddComponent<PadTest>();
				}
				else if (text.Contains("lightmode"))
				{
					base.gameObject.AddComponent<LightModeTest>();
				}
				else if (text.Contains("phone"))
				{
					base.gameObject.AddComponent<PhoneTest>();
				}
				else if (text.Contains("feedback"))
				{
					base.gameObject.AddComponent<FeedbackTest>();
				}
				else if (text.Contains("towns"))
				{
					base.gameObject.AddComponent<TownsTest>();
				}
				else if (text.Contains("townart"))
				{
					base.gameObject.AddComponent<TownArtTest>();
				}
				else if (text.Contains("houselife"))
				{
					base.gameObject.AddComponent<HouseLifeTest>();
				}
				else if (text.Contains("windowview"))
				{
					base.gameObject.AddComponent<WindowViewTest>();
				}
				else if (text.Contains("bonfire"))
				{
					base.gameObject.AddComponent<BonfireTest>();
				}
				else if (text.Contains("curtains"))
				{
					base.gameObject.AddComponent<CurtainsTest>();
				}
				else if (text.Contains("roomtour"))
				{
					base.gameObject.AddComponent<RoomTourTest>();
				}
				else if (text.Contains("doorwalk"))
				{
					base.gameObject.AddComponent<DoorWalkTest>();
				}
				else if (text.Contains("doorpeek"))
				{
					base.gameObject.AddComponent<LookThroughTest>();
				}
				else if (text.Contains("fitroom"))
				{
					base.gameObject.AddComponent<FitRoomTest>();
				}
				else if (text.Contains("homedoors"))
				{
					base.gameObject.AddComponent<HomeDoorsTest>();
				}
				else if (text.Contains("planning"))
				{
					base.gameObject.AddComponent<PlanningTest>();
				}
				else if (text.Contains("village"))
				{
					base.gameObject.AddComponent<VillageTest>();
				}
				else if (text.Contains("taxes"))
				{
					base.gameObject.AddComponent<TaxesTest>();
				}
				else if (text.Contains("tentpeg"))
				{
					base.gameObject.AddComponent<TentLookTest>();
				}
				else if (text.Contains("fixpass"))
				{
					base.gameObject.AddComponent<FixPassTest>();
				}
				else if (text.Contains("court"))
				{
					base.gameObject.AddComponent<CourtTest>();
				}
				else if (text.Contains("freedom"))
				{
					base.gameObject.AddComponent<FreedomTest>();
				}
				else if (text.Contains("peace"))
				{
					base.gameObject.AddComponent<PeaceTest>();
				}
				else if (text.Contains("butcher"))
				{
					base.gameObject.AddComponent<ButcherTest>();
				}
				else if (text.Contains("poses"))
				{
					base.gameObject.AddComponent<PosesTest>();
				}
				else
				{
					base.gameObject.AddComponent<SelfTest>();
				}
			}
		}

		private void Update()
		{
			long timestamp = Stopwatch.GetTimestamp();
			Hitch.UpdateStart();
			try
			{
				UpdateMod();
			}
			catch (Exception ex)
			{
				Log.Error("update failed: " + ex);
			}
			finally
			{
				Hitch.UpdateEnd("u:rest", Stopwatch.GetTimestamp() - timestamp);
			}
		}

		private void UpdateMod()
		{
			ToastTick();
			Lang.Tick(Settings?.Language);
			WorldPace.Tick(this);
			VrUpdate();
			PadUpdate();
			Hitch.Lap("u:lang, vr, pad");
			KeepQuiet();
			GuardStartTick();
			if (!SafeStart)
			{
				MapSizes.Tick();
			}
			_audio.Idle(Time.unscaledDeltaTime);
			Hitch.Lap("u:quiet, sizes, audio");
			NetSession.Tick();
			Hitch.Lap("u:net");
			if (!Config.game_loaded || World.world == null)
			{
				return;
			}
			ReleaseStrandedKeys();
			try
			{
				PhoneUpkeep();
			}
			catch (Exception ex)
			{
				Log.Error("phone upkeep: " + ex.Message);
			}
			LightUpkeep();
			if (PlayInput.GetKeyDown(KeyCode.V) && !IsTyping() && ControllableUnit.isControllingUnit())
			{
				if (_viewEnabled && Cabin.Inside && AtSea())
				{
					Toast("Not out at sea. Leave the cabin first (E)");
				}
				else
				{
					_viewEnabled = !_viewEnabled;
					Toast(_viewEnabled ? "First-person view" : "Map view (press V to return)");
				}
			}
			Hitch.Lap("u:phone, v");
			GodViewKeys();
			FlyKeys();
			SettingsMenuKeys();
			RunKeys();
			Hitch.Lap("u:god, settings, run, dark keys");
			Bandages.Tick();
			Hitch.Lap("u:bandages");
			if (!_active)
			{
				return;
			}
			ActionWheelKeys(AnyMenu);
			ToolWheelKeys(AnyMenu);
			EmoteWheelKeys(AnyMenu);
			CivilKeys();
			Hitch.Lap("u:civil keys");
			UpdateClickGrace();
			if (PlayInput.GetKeyDown(KeyCode.F7))
			{
				Settings.TimeMode = (Settings.TimeMode + 1) % DayCycle.ModeNames.Length;
				_settingsDirty = true;
				_relightNow = true;
				Toast("Time of day: " + _day.Describe(Settings));
			}
			if (PlayInput.GetKeyDown(KeyCode.F9))
			{
				SaveScreenshot();
			}
			Hitch.Lap("u:f keys");
			InventoryKeys();
			InventoryQoLKeys(AnyMenu);
			HotbarKeys();
			TorchKeys(AnyMenu);
			HelmetKeys(AnyMenu);
			PhotoKeys(AnyMenu);
			Hitch.Lap("u:inventory, hotbar, torch, h, k, eat, leap");
			FeatureKeys();
			if (Royal.ObeliskOpen && PlayInput.GetKeyDown(KeyCode.E) && !IsTyping())
			{
				Royal.ObeliskOpen = false;
				_obeliskClosedFrame = Time.frameCount;
			}
			bool anyMenu = AnyMenu;
			if (anyMenu != _menuWasOpen)
			{
				Sounds.PlayUI(anyMenu ? "event:/SFX/UI/WindowWhoosh" : "event:/SFX/UI/WindowClose");
			}
			_menuWasOpen = anyMenu;
			if (Holstered && !anyMenu && !ClickGrace && !WheelClick && Work.Target == null && PlayInput.GetMouseButtonDown(0))
			{
				Holstered = false;
				_unholsterFrame = Time.frameCount;
			}
			Hitch.Lap("u:features, menus");
			BinocularKeys(anyMenu);
			CompassKeys(anyMenu);
			PossessionHooks.BlockAttack = anyMenu || ClickGrace || WheelClick || Work.Target != null || Earthworks.Busy || _dive == Dive.In || Holstered || Time.frameCount - _unholsterFrame < 2 || _blaster.Blocks || _shotgun.Blocks || BinocularsBlock || CompassBlock || (_hotbarFood != null && !AimsAtCarcass(_host)) || Bandages.Wrapping || _torchBlocks || Hub.AnyModeActive;
			PoseKeys(anyMenu);
			PingKeys(anyMenu);
			BookmarkKeys(anyMenu);
			if (anyMenu)
			{
				if (Cursor.lockState != CursorLockMode.None)
				{
					Cursor.lockState = CursorLockMode.None;
				}
				Cursor.visible = !PadCursorOn;
				return;
			}
			Hitch.Lap("u:binoculars, compass, poses, pings");
			if (!PingWheelSteals() && !PoseWheelSteals() && !ActionWheel.Steal(DebugEyeSet) && !ToolWheel.Steal(DebugEyeSet) && !EmoteWheel.Steal(DebugEyeSet))
			{
				float num = Input.mouseScrollDelta.y + HotbarWheelForTest;
				HotbarWheelForTest = 0f;
				int num2 = PadControls.TakeStep();
				if (num2 != 0)
				{
					num = ((Binoculars.Up || Compass.Out) ? num2 : (-num2));
				}
				DigKeys(_host);
				if (num != 0f && !Hub.AnyModeActive && !PoseScroll(num) && !BinocularWheel(num) && !CompassWheel(num) && (num2 != 0 || !DigWheel(_host, num)))
				{
					HotbarWheel(num);
				}
				float num3 = 0f;
				float num4 = 0f;
				try
				{
					num3 = Input.GetAxisRaw("Mouse X");
					num4 = Input.GetAxisRaw("Mouse Y");
				}
				catch (ArgumentException)
				{
				}
				if (DebugEyeSet || MouseLookOff)
				{
					num3 = (num4 = 0f);
				}
				Vector2 vector = PadControls.TakeLook();
				if (!MouseLookOff)
				{
					num3 += vector.x / Settings.MouseSensitivity;
					num4 += vector.y / Settings.MouseSensitivity;
				}
				if (TouchControls.Shown)
				{
					Vector2 look = TouchControls.Look;
					num3 = look.x / Settings.MouseSensitivity;
					num4 = look.y / Settings.MouseSensitivity;
				}
				float num5 = ((_view.Fov > 0.01f) ? Mathf.Min(1f, Mathf.Tan(_view.Fov * 0.5f) / Mathf.Tan(Mathf.Max(10f, Settings.FieldOfView) * 0.5f * (MathF.PI / 180f))) : 1f);
				num3 *= num5;
				num4 *= num5;
				_view.Yaw -= num3 * Settings.MouseSensitivity * (MathF.PI / 180f);
				float lookLimit = LookLimit;
				_pitchDegrees = Mathf.Clamp(_pitchDegrees + num4 * Settings.MouseSensitivity, 0f - lookLimit, lookLimit);
				CursorLockMode cursorLockMode = ((!TouchControls.Shown) ? CursorLockMode.Locked : CursorLockMode.None);
				if (Cursor.lockState != cursorLockMode)
				{
					Cursor.lockState = cursorLockMode;
				}
				Cursor.visible = false;
			}
		}

		private void LateUpdate()
		{
			Hitch.LateStart();
			FrameGuard.AfterGameFrame();
			try
			{
				VrBeginFrame();
				try
				{
					FinishGodRender();
				}
				catch (Exception e)
				{
					GodViewFailed(e);
				}
				Hitch.Lap("start");
				FinishBesideRender();
				FinishLandBeside();
				UpdateSea(Time.unscaledDeltaTime);
				Hitch.Lap("beside wait");
				PacksLate();
				if (Config.game_loaded && World.world != null && !WorldSync.Mirroring)
				{
					try
					{
						Royal.Update(Time.unscaledDeltaTime, Toast);
					}
					catch (Exception ex)
					{
						Log.Error("campaign update failed: " + ex.Message);
						Royal.Active = null;
					}
					if (!_active)
					{
						try
						{
							CrownOffer.Update(null, Time.unscaledDeltaTime);
						}
						catch (Exception ex2)
						{
							Log.Error("crown offer: " + ex2.Message);
						}
					}
					Hitch.Lap("campaign");
					try
					{
						FirstPersonSave.Update(this);
					}
					catch (Exception ex3)
					{
						Log.Error("save: " + ex3.Message);
					}
					Actor you = (ControllableUnit.isControllingUnit() ? ControllableUnit._unit_main : null);
					try
					{
						WarMap.UpkeepFlags(you, Time.unscaledDeltaTime, Toast);
					}
					catch (Exception ex4)
					{
						Log.Error("war flags: " + ex4.Message);
					}
					try
					{
						WarMap.UpkeepFleet(you, Time.unscaledDeltaTime, Toast);
					}
					catch (Exception ex5)
					{
						Log.Error("fleet: " + ex5.Message);
					}
					Hitch.Lap("save, flags, fleet");
					if (ControllableUnit.isControllingUnit())
					{
						Guards.Update(Time.unscaledDeltaTime, ControllableUnit._unit_main, Toast);
					}
					try
					{
						KingGuards.Update(ControllableUnit.isControllingUnit() ? ControllableUnit._unit_main : null, Toast, _audio);
					}
					catch (Exception ex6)
					{
						Log.Error("king's guards: " + ex6.Message);
					}
					try
					{
						Shipyard.Update(Toast);
					}
					catch (Exception ex7)
					{
						Log.Error("shipyard: " + ex7.Message);
					}
					FamilyFollow.Update(ControllableUnit.isControllingUnit() ? ControllableUnit._unit_main : null, Settings.FamilyFollows, Toast);
					if (ControllableUnit.isControllingUnit() && !ControllableUnit.isControllingCrabzilla())
					{
						Possess.Keep(ControllableUnit._unit_main);
					}
					if (ControllableUnit.isControllingUnit())
					{
						PossessionCombat.ShakeOffStuns(ControllableUnit._unit_main);
					}
					Hitch.Lap("guards, family");
					Chronicle.Tick();
					Hitch.Lap("chronicle");
					Explored.Tick();
					Hitch.Lap("explored");
					DockDecks.Upkeep(Time.unscaledDeltaTime);
					BuildingFloors.Upkeep();
				}
				else if (WorldSync.Mirroring)
				{
					if (ControllableUnit.isControllingUnit() && !ControllableUnit.isControllingCrabzilla())
					{
						Possess.Keep(ControllableUnit._unit_main);
					}
					if (ControllableUnit.isControllingUnit())
					{
						PossessionCombat.ShakeOffStuns(ControllableUnit._unit_main);
					}
					DockDecks.Upkeep(Time.unscaledDeltaTime);
					BuildingFloors.Upkeep();
					if (Config.game_loaded && World.world != null)
					{
						try
						{
							Regalia.Update(Time.unscaledDeltaTime);
						}
						catch (Exception ex8)
						{
							Log.Error("regalia: " + ex8.Message);
						}
					}
				}
				Hitch.Lap("upkeep");
				UpdateActivation();
				Hitch.Lap("activation");
				if (_active)
				{
					long timestamp = Stopwatch.GetTimestamp();
					RenderFrame();
					if (_dive == Dive.In)
					{
						UpdateDiveIn();
					}
					if (!Scene.On)
					{
						_perf.AddLate((double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency);
					}
				}
				else if (_dive == Dive.Out)
				{
					RenderDiveOut();
				}
				Hitch.Lap("f:civil, prompts");
				try
				{
					UpdateGodView();
				}
				catch (Exception e2)
				{
					GodViewFailed(e2);
				}
				Hitch.Lap("god view");
				try
				{
					MapClock.Tick(this, !_active && _dive == Dive.None && !GodShown);
				}
				catch (Exception e3)
				{
					MapClock.Failed(e3);
				}
				Hitch.Lap("one world");
				GameClock.Update(_active || _dive != Dive.None || GodShown);
				WalkPace.Update(_active, Settings.FolkPace, PaceCentre(), Settings.CreatureDistance, Time.unscaledDeltaTime);
				ScreenSync.Update(_active || _dive != Dive.None || GodShown, Settings.VSync && !VrRunning);
				GameConsole.Update(_active || _dive != Dive.None || GodShown);
				if (!VrRunning)
				{
					FrameCaps.Update(_active || _dive != Dive.None || GodShown, Settings.FrameCap);
				}
				VrPacing();
				FirstPersonShots.ReleaseIfIdle();
				WorldSync.LateTick();
				Hitch.LateEnd();
			}
			catch (Exception ex9)
			{
				Log.Error("frame failed, leaving first person: " + ex9);
				Deactivate();
				EndDive();
				_viewEnabled = false;
			}
		}

		private void UpdateActivation()
		{
			UpkeepHijack();
			Actor actor = null;
			if (Config.game_loaded && World.world != null && ControllableUnit.isControllingUnit())
			{
				actor = ControllableUnit.getControllableUnit();
			}
			if (actor != null && !actor.isAlive())
			{
				actor = null;
			}
			if (DeathTimer >= 0f)
			{
				DeathTimer += Time.unscaledDeltaTime;
				if (actor != null && actor != _host && _viewEnabled)
				{
					DeathTimer = -1f;
					SwitchHost(actor);
				}
				else if (DeathTimer >= 3.2f + HeirWait)
				{
					if (!WorldSync.Mirroring)
					{
						RunDeathOver();
					}
					DeathTimer = -1f;
					LeaveFirstPerson();
				}
			}
			else if (_active && _host != null && !_host.isAlive())
			{
				if (_dive == Dive.In)
				{
					EndDive();
				}
				ExitHouse();
				LeaveRooms();
				BoatHelm.End();
				Craft.Open = false;
				Royal.Open = false;
				Royal.ObeliskOpen = false;
				Craft.Working = null;
				CivilStop();
				if (_host.data == null || WorldLoading)
				{
					Log.Info("first person off: the world was cleared");
					PossessionHooks.Active = false;
					Deactivate();
					EndDive();
					return;
				}
				if (WorldSync.Mirroring)
				{
					Heirs.Clear();
					_deadBody = null;
				}
				else
				{
					FindHeirs(_host);
					RunOnDeath(_host);
				}
				DeathTimer = 0f;
				DeathName = Describe(_host);
				try
				{
					var dAt = _host != null ? _host.current_position : new UnityEngine.Vector2(_view.X, _view.Y);
					var bm = UserWaypoints.Add(dAt, "☠ " + DeathName);
					bm.ColorHex = "#ff3b30";
					UserWaypoints.Save();
					Toast("Death marked on compass — press K to manage bookmarks");
					_compassGatherAt = -10f;
				} catch {}
				PossessionHooks.Active = false;
				Log.Info("died: " + DeathName + DeathCause.Of(_host));
			}
			else
			{
				if (actor == null)
				{
					_viewEnabled = true;
				}
				bool flag = actor != null && _viewEnabled;
				if (flag && !_active)
				{
					Activate(actor);
				}
				else if (!flag && _active)
				{
					LeaveFirstPerson();
				}
				else if (flag && actor != _host)
				{
					SwitchHost(actor);
				}
			}
		}

		private void Activate(Actor unit)
		{
			if (!_sampler.EnsureWorld())
			{
				return;
			}
			PossessionHooks.Install();
			bool flag = _dive == Dive.Out;
			_active = true;
			if (!flag)
			{
				_pitchDegrees = 0f;
				_view.Yaw = (unit.flip ? MathF.PI : 0f);
			}
			_scene.Reset();
			SwitchHost(unit);
			BeginDiveIn(flag);
			if (!Settings.TipsShown)
			{
				HintUntil = Time.unscaledTime + 15f;
				Settings.TipsShown = true;
				_settingsDirty = true;
			}
			else
			{
				HintUntil = 0f;
			}
			Holstered = false;
			ResetTown();
			_perf.Reset();
			_audio.Begin();
			try
			{
				if (ScrollWindow.isWindowActive())
				{
					ScrollWindow.hideAllEvent(pWithAnimation: false);
				}
			}
			catch
			{
			}
			HideMapView(_dive == Dive.None);
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
			if (!PossessionHooks.Installed)
			{
				Toast("Movement hook unavailable: WASD moves along map axes");
			}
			Log.Info("first person on: " + Describe(unit));
		}

		private void SwitchHost(Actor unit)
		{
			if (unit != _host)
			{
				LeaveRooms();
				CivilStop();
			}
			_wakeIn = HouseInterior.HouseAt(unit);
			_wakeAboard = null;
			try
			{
				if (unit.is_inside_boat && unit.inside_boat != null && (unit.asset == null || !unit.asset.is_boat))
				{
					_wakeAboard = ((ActorSimpleComponent)unit.inside_boat).actor;
				}
			}
			catch
			{
			}
			HelmFor(unit);
			_host = unit;
			_eyeZ = 0f;
			_gait.Reset();
			_crouch = 0f;
			_standTry = -1f;
			_eyeOffset = Vector2.zero;
			_landDip = (_landVel = 0f);
			HostImage = null;
			_lastPos = unit.current_position;
			Feedback.Reset(unit);
			Work.Reset();
			Craft.Working = null;
			PossessionHooks.Yaw = _view.Yaw;
			PossessionHooks.AimPoint = unit.current_position;
			PossessionHooks.KeysTaken = () => MoveKeysTaken;
			PossessionHooks.Active = PossessionHooks.Installed;
		}

		private void Deactivate()
		{
			SettleBesideRender();
			SceneOff();
			if (_active)
			{
				Log.Info("first person off");
			}
			ExitHouse();
			LeaveRooms();
			BoatHelm.End();
			Talk.Reset(_scene.LookAt);
			Prompt = null;
			ResetWheels();
			_active = false;
			_host = null;
			DropBinoculars();
			DropCompass();
			DropPose();
			_picked = null;
			_leaping = false;
			DeathTimer = -1f;
			PossessionHooks.Active = false;
			PossessionHooks.BlockAttack = false;
			HideMapView(hide: false);
			Craft.Open = false;
			Royal.Open = false;
			Royal.ObeliskOpen = false;
			Craft.Working = null;
			Work.Reset();
			Quests.EndTrialState();
			StopFeatures();
			Crabzillas.Release();
			_audio.End();
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
			if (_settingsDirty)
			{
				Settings.Save();
				_settingsDirty = false;
			}
		}

		private void RenderFrame()
		{
			if (!_sampler.EnsureWorld())
			{
				Deactivate();
				return;
			}
			VrLook();
			_view.Free = FreeLook;
			UpdateBinoculars(Time.unscaledDeltaTime);
			UpdateCompass(Time.unscaledDeltaTime);
			UpdatePose(Time.unscaledDeltaTime);
			_day.Update(Settings, Time.unscaledDeltaTime);
			Calendar.UpdateLook(Time.unscaledDeltaTime);
			Renderer.NightNumber = _day.Night;
			Renderer.ColdSky = _day.Cold;
			Renderer.PaletteLevels = ((Settings.Palette >= 1) ? 18 : 0);
			ApplyShadows();
			PossessionHooks.RunFactor = Settings.RunSpeed * (1f - 0.45f * Binoculars.Lift);
			_audio.StepVolume = Settings.StepVolume;
			_audio.AmbientVolume = Settings.AmbientVolume;
			_audio.MasterVolume = Settings.MasterVolume;
			_audio.EffectsVolume = Settings.EffectsVolume;
			_audio.SpatialWanted = Settings.SpatialSound;
			_audio.VoicesVolume = Settings.VoicesVolume;
			_audio.VoiceStyle = Settings.VoiceStyle;
			UpdateTalkVoice();
			SceneCollector.WindGusts = Settings.WindGusts;
			SceneCollector.BiomeFxLevel = Settings.BiomeFx;
			SceneCollector.BiomeParticleAmount = Settings.BiomeParticles;
			SceneCollector.Chill = (NoFirefliesForTest ? 1f : Mathf.Max(SeasonLook.Winter, _day.Cold));
			Hitch.Lap("f:look, day, shadows");
			UpdateNews(Time.unscaledDeltaTime);
			if (SceneShows())
			{
				KeepRoomUnderScene(Time.unscaledDeltaTime);
				RenderScene(Time.unscaledDeltaTime);
				return;
			}
			if (Cabin.Inside && DeathTimer < 0f)
			{
				RenderCabin(Time.unscaledDeltaTime);
				return;
			}
			if (Mine.Inside && DeathTimer < 0f)
			{
				RenderMine(Time.unscaledDeltaTime);
				return;
			}
			WakeIndoors();
			if (Interior.Inside && DeathTimer < 0f)
			{
				RenderInterior(Time.unscaledDeltaTime);
				return;
			}
			Hitch.Lap("f:news, rooms");
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			bool flag = DeathTimer >= 0f;
			_wasInRoom = false;
			Atmosphere air = _day.Air;
			Renderer.Air = air;
			Renderer.Windows.Clear();
			FollowLight(air);
			Actor host = _host;
			bool flag2 = !flag && BoatHelm.Steers(host);
			bool flag3 = !flag && Crabzillas.Is(host);
			if (!flag && !DebugEyeSet && !flag2 && !flag3)
			{
				KeepBodyClear(host);
				Unstick(host, unscaledDeltaTime);
			}
			Vector2 vector = (flag ? _lastPos : (DebugEyeSet ? DebugEye : host.current_position));
			float num = _sampler.Cache.SampleHeight(vector.x, vector.y);
			if (!flag2 && DockDecks.HeightAt(vector.x, vector.y, out var z) && z > num)
			{
				num = z;
			}
			if (!flag && !DebugEyeSet)
			{
				Climbing.Feel(vector, _sampler.Cache, unscaledDeltaTime);
			}
			float num2 = 1f;
			float num3;
			if (flag)
			{
				num3 = num + 0.18f;
				_pitchDegrees = Mathf.Lerp(_pitchDegrees, 28f, 1f - Mathf.Exp(-2f * unscaledDeltaTime));
			}
			else
			{
				try
				{
					SpriteImage spriteImage = _sprites.Get(host.getSpriteToRender());
					if (spriteImage != null)
					{
						HostImage = spriteImage;
						num2 = Stature.Of(host, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(host.current_scale.y), unscaledDeltaTime);
					}
				}
				catch
				{
				}
				num2 = Mathf.Clamp(num2, 0.3f, 8f);
				num = StandOn(host, vector, num, num2, flag2 || flag3 || DebugEyeSet);
				Swimming = !flag3 && (ForceSwim || SceneCollector.IsSwimming(host));
				if (Wading.YourFloor(host, vector, Wading.DryBody(host, num2, Swimming), out var floor) && !Swimming && !flag2 && !flag3 && !DebugEyeSet)
				{
					num = floor;
					num2 = Wading.DryBody(host, num2, Swimming);
				}
				float up;
				bool flag4 = BeastView.Update(host, _view.Yaw, _pitchDegrees, num2, Swimming, ControllableUnit.getMovementVector() != Vector2.zero, Mathf.Min(Feedback.SinceAttack, SinceBeastAbility), Feedback.LastAttackMelee || SinceBeastAbility < Feedback.SinceAttack, unscaledDeltaTime, out _headOffset, out up);
				if (flag3 && Crabzillas.Eye(host, _view.Yaw, ControllableUnit.getMovementVector() != Vector2.zero, unscaledDeltaTime, out _headOffset, out up))
				{
					num2 = Crabzillas.EyeHeight;
				}
				if (Swimming)
				{
					float num4 = _sampler.Cache.WaveAt(vector.x, vector.y);
					_swimSurface = _sampler.Cache.LiquidSurface(vector.x, vector.y);
					num3 = _swimSurface + (flag4 ? up : (num2 * 0.28f)) * Settings.EyeHeight + num4;
				}
				else
				{
					num3 = num + (DebugEyeSet ? 0f : _standLift) + (flag4 ? up : (num2 * Mathf.Lerp(0.9f, 0.55f, _crouch))) * Settings.EyeHeight;
					if (DebugEyeSet && DebugEyeHeight > 0f)
					{
						num3 = num + DebugEyeHeight;
					}
					num3 = PoseEyeZ(num3, num, num2);
				}
				_lastPos = vector;
			}
			if (flag)
			{
				_eyeZ = ((_eyeZ == 0f) ? num3 : Mathf.Lerp(_eyeZ, num3, 1f - Mathf.Exp(-3f * unscaledDeltaTime)));
				_groundZ = num;
			}
			else if (Swimming)
			{
				_eyeZ = ((_eyeZ == 0f) ? num3 : Mathf.Lerp(_eyeZ, num3, 1f - Mathf.Exp(-8f * unscaledDeltaTime)));
				_groundZ = num;
				_groundVel = 0f;
			}
			else
			{
				if (_eyeZ == 0f || Mathf.Abs(_groundZ - num) > 3f)
				{
					_groundZ = num;
					_groundVel = 0f;
					StreetEyeEase(num3, num);
				}
				else if (_standLift > 0.05f)
				{
					_groundZ = num;
					_groundVel = 0f;
				}
				else
				{
					_groundZ = Mathf.SmoothDamp(_groundZ, num, ref _groundVel, (num > _groundZ) ? 0.11f : 0.06f, 60f, Mathf.Max(0.0001f, unscaledDeltaTime));
				}
				_groundZ = Mathf.Max(_groundZ, num - 0.3f * num2);
				_eyeZ = num3 - num + _groundZ;
			}
			if (!flag && !Swimming && !DebugEyeSet && !flag2 && !flag3 && host != null)
			{
				FallHurt.Track(host, vector, num, unscaledDeltaTime);
			}
			else
			{
				FallHurt.Reset();
			}
			bool flag5 = !flag && ControllableUnit.getMovementVector() != Vector2.zero;
			bool flag6 = !flag && _standLift > 0.05f;
			Moving = flag5 && _standLift <= 0.01f;
			float num5 = (PossessionHooks.Installed ? PossessionHooks.RunBlend : 0f);
			float num6 = 0f;
			float value = ((host != null) ? Weight.Of(host) : 1f);
			float a = (PossessionHooks.Installed ? PossessionHooks.WalkBlend : 0f);
			CrouchStep(host, vector, num2, flag || Swimming || flag3 || flag2, unscaledDeltaTime);
			a = Mathf.Max(a, _crouch);
			_gait.Update(vector, unscaledDeltaTime, !flag && !Swimming && !flag6 && !flag2 && !flag3, flag5 && !flag2, num5, a, num2);
			if (Swimming && !flag)
			{
				SwimPhase += unscaledDeltaTime * (flag5 ? 5.5f : 2f);
				num6 = Mathf.Sin(Time.unscaledTime * 1.7f) * 0.8f;
			}
			BobPhase = _gait.Phase;
			Stride = _gait.Stride;
			if (_wasAirborne && !flag6 && !Swimming)
			{
				Leap jump = PossessionHooks.Jump;
				float num7 = ((jump.Landings != _landingsSeen) ? jump.LandSpeed : 4.6f);
				_landVel = (0f - num2) * Mathf.Clamp(0.3f * num7, 0.35f, 3.2f) * Mathf.Sqrt(Mathf.Clamp(value, 0.3f, 2.5f));
			}
			_landingsSeen = PossessionHooks.Jump.Landings;
			_wasAirborne = flag6;
			float num8 = (PossessionHooks.Installed ? PossessionHooks.Pace : 0f);
			int num9;
			if (PossessionHooks.Installed)
			{
				num9 = (PossessionHooks.KeysHeld ? 1 : 0);
				if (num9 != 0)
				{
					goto IL_0ad2;
				}
			}
			else
			{
				num9 = 0;
			}
			if (!flag6 && !Swimming && !flag && !_planted && _lastPace > PossessionHooks.RunFactor * 1.15f && num8 < _lastPace - 0.005f)
			{
				_planted = true;
				float num10 = Mathf.Clamp01((_lastPace - PossessionHooks.RunFactor) / Mathf.Max(0.05f, 1.35f - PossessionHooks.RunFactor));
				_landVel -= num2 * 1f * num10 * Settings.HeadBob;
				_plantNod += 1.2f * num10 * Settings.HeadBob;
			}
			goto IL_0ad2;
			IL_0ad2:
			if (num9 != 0 || num8 < 0.05f)
			{
				_planted = false;
			}
			_lastPace = num8;
			_plantNod = Mathf.Lerp(_plantNod, 0f, 1f - Mathf.Exp(-5f * unscaledDeltaTime));
			float num11 = Mathf.Min(unscaledDeltaTime, 0.04f);
			float num12 = 140f / Mathf.Clamp(value, 0.5f, 2.5f);
			_landVel += ((0f - _landDip) * num12 - _landVel * 2f * 0.76f * Mathf.Sqrt(num12)) * num11;
			_landDip += _landVel * num11;
			float num13 = ((!flag) ? (7f * num5 * _gait.Stride - 7f * _bow.Draw) : 0f);
			_fovKick = Mathf.Lerp(_fovKick, num13, 1f - Mathf.Exp((0f - ((num13 > _fovKick) ? 10f : 5f)) * unscaledDeltaTime));
			float b = 0f;
			if (!flag && !Swimming && !flag3 && !flag2 && !PoseLocks && !VrRunning && PossessionHooks.Installed)
			{
				Vector2 velocity = PossessionHooks.Velocity;
				float num14 = Mathf.Cos(_view.Yaw);
				float num15 = Mathf.Sin(_view.Yaw);
				b = (0f - Mathf.Clamp((velocity.x * num15 - velocity.y * num14) / Mathf.Max(0.05f, PossessionHooks.RunFactor), -1f, 1f)) * Mathf.Lerp(1.8f, 2.3f, num5) * Settings.HeadBob * BinocularSteady;
			}
			Lean = Mathf.Lerp(Lean, b, 1f - Mathf.Exp(-8f * unscaledDeltaTime));
			if (flag3)
			{
				Feedback.Shake = Mathf.Max(Feedback.Shake, Crabzillas.Shake);
				Crabzillas.Shake = 0f;
			}
			Feedback.Shake = Mathf.Max(Feedback.Shake, _scene.TakeBlastShake());
			Feedback.Shake = Mathf.Max(Feedback.Shake, Dragons.Shake);
			Dragons.Shake = 0f;
			float num16 = (Settings.ScreenShake ? Feedback.Shake : 0f);
			float num17 = ((num16 > 0f) ? ((UnityEngine.Random.value - 0.5f) * num16 * 1.2f) : 0f);
			float num18 = Mathf.Cos(_view.Yaw);
			float num19 = Mathf.Sin(_view.Yaw);
			if (flag)
			{
				_headOffset = Vector2.zero;
			}
			Vector2 vector2 = vector + _headOffset + PoseShift(vector);
			Vector2 vector3 = ((flag || flag3 || PoseLocks) ? vector2 : Colliders.PushOut(vector2, PossessionHooks.BodyRadius * 0.7f));
			_eyeOffset = Vector2.Lerp(_eyeOffset, vector3 - vector2, 1f - Mathf.Exp(-20f * unscaledDeltaTime));
			float num20 = (flag3 ? 0f : (Settings.HeadBob * BinocularSteady * (1f - 0.7f * _bow.Steady)));
			float num21 = (Swimming ? 0f : (_gait.Lateral * num20));
			_view.X = vector2.x + _eyeOffset.x + num19 * num21;
			_view.Y = vector2.y + _eyeOffset.y - num18 * num21;
			_view.Z = _eyeZ + (Swimming ? 0f : (_gait.Vertical * num20)) + _landDip;
			if (!flag && !Swimming && _headOffset != Vector2.zero)
			{
				_view.Z = Mathf.Max(_view.Z, _sampler.Cache.SampleHeight(_view.X, _view.Y) + 0.1f);
			}
			_view.Pitch = LookSlope(_pitchDegrees + (Feedback.Kick * 1.5f + num17 + num6 + _melee.ViewPitch + _shotgun.ViewKick) * BinocularSteady - (Swimming ? 0f : (_gait.Nod * num20 + _plantNod * BinocularSteady)));
			_view.Fov = (Settings.FieldOfView + _fovKick + (flag ? 0f : BeastView.Widen) + (PhotoMode.Active ? PhotoMode.FovOffset : 0f)) * (MathF.PI / 180f);
			PossessionHooks.BodyRadius = Mathf.Clamp(0.17f * num2, 0.1f, 0.6f);
			float num22 = (Swimming ? (_swimSurface - num2 * 0.3f) : num);
			float num23 = ((flag || Swimming) ? 0f : _standLift);
			if (flag || flag3 || DebugEyeSet || (num23 > 0.3f && PossessionHooks.Flies(host)))
			{
				Colliders.ForgetBody();
			}
			else
			{
				Colliders.SetBody(num22, num23, num2 * Mathf.Lerp(1f, 0.62f, _crouch), _eyeZ - num22 - num23);
			}
			if (!flag && !flag3 && !Swimming && host != null)
			{
				BonfireBurn(host, vector, num22 + num23, num22 + num23 + num2, PossessionHooks.BodyRadius, unscaledDeltaTime);
			}
			Renderer.RippleX = vector.x;
			Renderer.RippleY = vector.y;
			_wakeFrom = vector;
			Renderer.WakeCount = Wading.Wakes(Renderer.Wakes, vector, Time.unscaledTime, delegate(Vector2 at, float k)
			{
				_audio?.PlayFrom("wade", 0.2f + 0.25f * k, at, _wakeFrom, 12f);
			});
			Renderer.RippleStrength = ((!Swimming || flag) ? ((Wading.YouWade && !flag && !Wading.Frozen(host?.current_tile) && !DebugEyeSet) ? (flag5 ? 0.7f : 0.25f) : 0f) : (flag5 ? 1f : 0.45f));
			if (flag2)
			{
				HelmView(host, unscaledDeltaTime);
			}
			_view.Fov = Binoculars.Fov(_view.Fov, Binoculars.Magnification);
			VrHead(vector2, num23, flag, unscaledDeltaTime);
			Hitch.Lap("f:body, eye");
			bool flag7 = CanDrawBeside(flag);
			_frameClock.Restart();
			if (flag7 || TakeLandBeside())
			{
				_sampler.TickLandforms(unscaledDeltaTime);
			}
			else
			{
				_sampler.Tick(vector.x, vector.y);
			}
			double totalMilliseconds = _frameClock.Elapsed.TotalMilliseconds;
			Hitch.Lap("f:land");
			UpdateFeatures(flag ? null : host, vector, flag, unscaledDeltaTime);
			Doorways(host, vector, flag);
			_perf.AddWorld(totalMilliseconds, _frameClock.Elapsed.TotalMilliseconds - totalMilliseconds);
			Hitch.Lap("f:features, doors");
			_sprites.BeginFrame();
			int num24 = RenderWidth(GpuWanted);
			int num25 = Mathf.Max(90, Mathf.RoundToInt((float)(num24 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
			if (VrSquare)
			{
				num25 = num24;
			}
			Renderer.Resize(num24, num25);
			_scene.PictureWidth = num24;
			ApplyViewDistance();
			Renderer.Time = SeaNow;
			ViewState viewState = _view;
			float bodyFade = 0f;
			bool flag8 = _dive == Dive.In && !flag;
			bool prewarm = false;
			bool handsUp = !Binoculars.Active || Binoculars.HandsShown;
			if (flag8)
			{
				viewState = DiveCamera(_view, Swimming ? Mathf.Max(num, _swimSurface) : num, num2, unscaledDeltaTime, num24, num25, out bodyFade, out prewarm);
			}
			else if (_leaping && !flag)
			{
				viewState = LeapCamera(_view, num2, unscaledDeltaTime, out bodyFade, out handsUp);
			}
			else if (_poseView > 0.001f && !flag)
			{
				viewState = PoseCamera(_view, num2, out handsUp);
			}
			SteepAround(viewState, num24, num25, num);
			Renderer.ShadowFocusSet = flag8;
			Renderer.ShadowFocusX = vector.x;
			Renderer.ShadowFocusY = vector.y;
			Hitch.Lap("f:camera");
			if (!flag)
			{
				FirstPersonShots.Update(_sampler.Cache, _scene);
			}
			if (!flag)
			{
				ShotWalls.Update(_sampler.Cache, _scene, host);
			}
			if (!flag8 || _diveViewAlpha > 0f || prewarm)
			{
				double totalMilliseconds2 = _frameClock.Elapsed.TotalMilliseconds;
				Hitch.Lap("f:shots");
				CollectScene(viewState, _view, flag ? null : host, bodyFade, air, unscaledDeltaTime);
				Hitch.Lap("f:scene");
				ApplyWeather(air, vector, unscaledDeltaTime);
				ApplyBiomeAir();
				Renderer.Meshes.Clear();
				Renderer.Meshes.AddRange(_scene.Meshes);
				Renderer.Lights.Clear();
				Renderer.Lights.AddRange(_scene.Lights);
				if (!flag)
				{
					FirstPersonShots.EmitTrails(Renderer.Streaks, unscaledDeltaTime);
				}
				Dragons.EmitTrails(Renderer.Streaks);
				Renderer.Streaks.AddRange(_scene.Streaks);
				ApplyDiveCut(flag8 ? DiveCut(viewState, num24, num25, vector.x, vector.y, Swimming ? Mathf.Max(num, _swimSurface) : num, DivePath.Ease(_diveT)) : SightCut.Off);
				if (!flag && (!flag8 || _diveHands) && handsUp && !BeastView.Active)
				{
					BuildHands(_scene.Voxels, host, num2, num5, unscaledDeltaTime);
				}
				else
				{
					Renderer.OverlayMeshes.Clear();
				}
				if (!flag && !flag8)
				{
					AddAbilityMarker(_scene.Voxels);
				}
				bool num26 = !flag8 && !flag && DoorwayView(viewState, num24, num25, unscaledDeltaTime);
				double totalMilliseconds3 = _frameClock.Elapsed.TotalMilliseconds;
				Hitch.Lap("f:weather, hands, doorway");
				if (!VrStereo(viewState, flag8, flag, host, num2, num, totalMilliseconds3 - totalMilliseconds2))
				{
					if (GpuFrame(viewState, num24, num25, flag8, flag, host, num2, num, totalMilliseconds3 - totalMilliseconds2))
					{
						if (!flag8 && !flag && LandBesideOn)
						{
							StartLandBeside(vector);
						}
					}
					else if (flag7)
					{
						float feet = (Swimming ? (_swimSurface - num2 * 0.3f) : (num + _standLift));
						StartBesideRender(viewState, num24, num25, host, num2, feet, vector, totalMilliseconds3 - totalMilliseconds2);
					}
					else
					{
						if (num24 > 640)
						{
							num24 = 640;
							num25 = Mathf.Max(90, Mathf.RoundToInt((float)(num24 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
							if (VrSquare)
							{
								num25 = num24;
							}
							Renderer.Resize(num24, num25);
						}
						Renderer.Render(_sampler.Cache, viewState, _scene.Billboards, _scene.Voxels, _sampler.MaxHeight + 0.45f);
						double totalMilliseconds4 = _frameClock.Elapsed.TotalMilliseconds;
						_picked = ((!flag8 && Renderer.PickTag >= 0 && Renderer.PickTag < _scene.Tagged.Count) ? _scene.Tagged[Renderer.PickTag] : null);
						if (!flag8 && !flag)
						{
							AimFrom(viewState, host, num2, num);
						}
						UploadFrame(num24, num25);
						if (!flag8)
						{
							_perf.AddLand(_sampler.TakeTimes());
							_perf.Add(unscaledDeltaTime, totalMilliseconds3 - totalMilliseconds2, totalMilliseconds4 - totalMilliseconds3, _frameClock.Elapsed.TotalMilliseconds - totalMilliseconds4, Renderer);
						}
					}
				}
				if (num26)
				{
					DoorwayViewDone();
				}
			}
			Hitch.Lap("f:draw");
			FrameMs = (float)(_frameClock.Elapsed.TotalMilliseconds + _besideMainMs);
			_renderMs = Mathf.Lerp(_renderMs, FrameMs, 0.1f);
			if (!flag8 && !flag7)
			{
				AdaptDetail();
			}
			TerrainCache cache = _sampler.Cache;
			Feedback.Update(flag ? null : host, unscaledDeltaTime, _scene.Tagged, CacheHeight);
			Hitch.Lap("f:adapt, feedback");
			_habitat.Update(vector, _scene, cache, unscaledDeltaTime);
			Hitch.Lap("f:habitat");
			if (!flag)
			{
				UpdateTown(host);
			}
			Hitch.Lap("f:town");
			_audio.Update(OutdoorSound(vector, num, flag, flag5), unscaledDeltaTime);
			Hitch.Lap("f:audio");
			if (flag)
			{
				return;
			}
			float x = Mathf.Cos(_view.Yaw);
			float y = Mathf.Sin(_view.Yaw);
			Vector2 vector4 = vector + new Vector2(x, y) * (flag2 ? 7f : 1.5f);
			if (_picked is Actor actor && actor.isAlive())
			{
				vector4 = actor.current_position;
			}
			else if (_picked is Building building && building.current_tile != null)
			{
				vector4 = new Vector2(building.current_tile.posV3.x, building.current_tile.posV3.y);
			}
			PossessionHooks.AimPoint = vector4;
			PossessionHooks.Yaw = _view.Yaw;
			Vector2 vector5 = vector;
			Talk.Blocked = TalkBusy || Hub.AnyModeActive || Mounted || AnyMenu;
			Talk.Indoors = false;
			Hitch.Lap("f:aim");
			BaseSimObject baseSimObject = (flag3 ? _picked : TalkPick(_picked, (Actor actor3) => actor3.current_position, vector5));
			_talkFrom = vector5;
			if (!flag3)
			{
				Talk.Update(host, baseSimObject, (Actor actor3) => (actor3.current_position - _talkFrom).magnitude, HeadOfWorld, _scene.LookAt, vector5, Toast, unscaledDeltaTime);
			}
			if (!flag3)
			{
				Work.Update(host, TorchWorkPick(host), vector, unscaledDeltaTime, !Craft.Open && !AnyMenu && !ClickGrace && !BinocularsBlock && !CompassBlock && !Hub.AnyModeActive, _scene, Feedback, Toast, CacheHeight, _eyeZ, _view.Yaw, _view.Pitch, Holstered);
			}
			Hitch.Lap("f:talk, work");
			ForgeUpdate(Body ?? host, unscaledDeltaTime);
			CivilUpdate(Body ?? host, unscaledDeltaTime);
			Prompt = null;
			if (!TalkBusy && !AnyMenu)
			{
				if (Hub.AnyModeActive)
				{
					Prompt = Hub.ModePrompt;
				}
				else if (flag3)
				{
					Prompt = "LMB:Left claw laser|RMB:Right claw laser";
				}
				else if (!flag2)
				{
					if (Talk.Candidate != null)
					{
						Prompt = (Dialogue.IsPerson(Talk.Candidate) ? (TalkPrompt(host, Talk.Candidate) + "|F:Insult|Q:Steal") : "E:Talk");
					}
					else if (!BundlePrompt(host, vector, lookingDown: true) && (DoorAhead(vector) || !LootPrompt(host, vector)) && !TownsPrompt(host, vector))
					{
						if (Work.Storage != null && (!HouseInterior.IsEnterable(Work.Storage) || !(DistanceToBuilding(Work.Storage, vector) < 2.2f)))
						{
							Prompt = "E:Store " + Work.BagCount + " resources";
							if (PlayInput.GetKeyDown(KeyCode.E))
							{
								Work.Store(host, Toast);
							}
						}
						else if (Work.Target != null)
						{
							Prompt = "LMB:" + ((Work.Verb == "Collect honey from") ? "Collect honey" : Work.Verb) + (Work.TargetFull ? " (bag full)" : "");
						}
						else if (Work.Needs != null)
						{
							Prompt = Work.Needs;
						}
						else if (_picked is Building building2 && Royal.IsObelisk(building2) && DistanceToBuilding(building2, vector) < 3f)
						{
							Prompt = (Royal.IsKing(host) ? "E:Wake the obelisk" : "E:Touch the obelisk");
							if (PlayInput.GetKeyDown(KeyCode.E) && Time.frameCount != _obeliskClosedFrame)
							{
								Royal.TouchObelisk(host, Toast);
								if (Royal.ObeliskOpen)
								{
									_obelisk = building2;
								}
							}
						}
						else if (!BoatPrompt(host, vector) && !MinePrompt(host, vector) && !BoardPrompt(host, vector) && !RaftPrompt(host, vector) && (!(_picked is Building building3) || !HouseInterior.IsEnterable(building3) || !(DistanceToBuilding(building3, vector) < 2.2f) || !HouseDoorPrompt(host, building3)))
						{
							if (_picked is Building building4 && HouseInterior.IsEnterable(building4) && DistanceToBuilding(building4, vector) < 2.2f)
							{
								bool flag9 = Work.Storage == building4;
								Prompt = "E:Enter" + (flag9 ? ("|Shift+E:Store " + Work.BagCount + " resources") : "");
								if (PlayInput.GetKeyDown(KeyCode.E))
								{
									if (flag9 && (PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift)))
									{
										Work.Store(host, Toast);
									}
									else
									{
										string text = HouseInterior.Barred(building4, Body ?? host);
										if (text != null)
										{
											Toast(text);
											_audio.PlayDoor();
										}
										else
										{
											EnterHouse(building4);
										}
									}
								}
							}
							else if (!flag3 && !flag2)
							{
								SeatPrompt(host);
							}
						}
					}
				}
			}
			bool anyModeActive = Hub.AnyModeActive;
			if (!flag3 && !flag2 && !TalkBusy && !anyModeActive && Prompt != null && !Prompt.Contains("E:"))
			{
				SeatPrompt(host, append: true);
			}
			if (!TalkBusy && !AnyMenu && !flag3 && !anyModeActive)
			{
				BundlePrompt(host, vector, lookingDown: false);
			}
			if (!TalkBusy && !AnyMenu && !flag3 && !anyModeActive)
			{
				FounderPrompt(host);
			}
			if (baseSimObject is Actor actor2 && actor2 != host && Possess.CanJumpTo(actor2))
			{
				if (Prompt == null)
				{
					Prompt = "=:Possess";
				}
				else if (Talk.Candidate == actor2)
				{
					Prompt += "|=:Possess";
				}
			}
			HijackPrompt(flag2);
			if (Prompt == null && PoseLocks)
			{
				Prompt = "P:Get up";
			}
			if (flag2 && Prompt == null)
			{
				Prompt = "W/S:Sails|A/D:Wheel" + (WarMap.Armed(BoatHelm.Boat) ? "|LMB:Fire" : "");
			}
			if (Prompt == null && _blaster.InHand && _blaster.State.Overheated)
			{
				Prompt = "R:Turn the knob to vent the blaster";
			}
			if (Prompt == null && _shotgun.InHand && _shotgun.State.Loaded <= 2 && !_shotgun.State.Reloading)
			{
				Prompt = "R:Reload";
			}
			if (!TalkBusy && Eating.Hungry(Body) && !BoatHelm.IsBoat(Body))
			{
				Prompt = ((Prompt == null) ? "Hold Q:Eat" : (Prompt + "|Hold Q:Eat"));
			}
			if (Hub.AnyModeActive)
			{
				Prompt = Hub.ModePrompt;
			}
			if (DebugPrompt != null)
			{
				Prompt = DebugPrompt;
			}
			if (Royal.ObeliskOpen && (_obelisk == null || !Royal.IsObelisk(_obelisk) || DistanceToBuilding(_obelisk, vector) > 5f))
			{
				Royal.ObeliskOpen = false;
			}
			Camera camera = World.world.camera;
			if (camera != null)
			{
				Vector2 vector6 = (PossessionHooks.Installed ? vector : vector4);
				camera.transform.position = new Vector3(vector6.x, vector6.y, camera.transform.position.z);
			}
		}

		private void AimFrom(ViewState camera, Actor host, float body, float ground)
		{
			if (Hitboxes.SpriteOf == null)
			{
				Hitboxes.SpriteOf = (Actor a) => _sprites.Get(a.getSpriteToRender());
				Hitboxes.GroundAt = (float x, float y) => _sampler.Cache.SampleHeight(x, y);
				FirstPersonAudio.GroundAt = Hitboxes.GroundAt;
			}
			float feet = (Swimming ? (_swimSurface - body * 0.3f) : (ground + _standLift));
			FirstPersonAim.Set(host, camera, body, feet, _picked, Renderer.PickDepth, _sampler.Cache, Renderer.MaxDistance);
		}

		private void EnterHouse(Building house)
		{
			if (DoorRoom.Inside && DoorRoom.House == house && DoorRoom.Fitted)
			{
				Talk.Reset(_scene.LookAt);
				NoteCrossing(_view.Z, stepIn: true);
				Interior.TakeOver(DoorRoom, _host);
				Interior.Eye = new Vector2(Interior.Room.SpawnX, Interior.Room.SpawnY);
				_outsideYaw = _view.Yaw;
				_view.Yaw = Interior.Room.SpawnYaw;
				PossessionHooks.Frozen = true;
				_doorHouse = null;
				if (Interior.BenchNote != null)
				{
					Toast("Home. " + Interior.BenchNote);
				}
				RoyalVisit(house);
				return;
			}
			SpriteImage spriteImage;
			try
			{
				spriteImage = _sprites.Get(house.checkSpriteToRender());
			}
			catch
			{
				spriteImage = null;
			}
			BuildingFundament fundament = house.asset.fundament;
			float num = ((spriteImage != null) ? (spriteImage.UnitWidth * Mathf.Abs(house.asset.scale_base.x)) : ((fundament != null) ? ((float)(fundament.left + fundament.right + 1)) : 2f));
			float depthTiles = ((fundament != null) ? Mathf.Max(1f, fundament.top + fundament.bottom + 1) : num);
			Talk.Reset(_scene.LookAt);
			Interior.Enter(house, spriteImage, num, depthTiles, _host);
			_audio.PlayDoor();
			_outsideYaw = _view.Yaw;
			_view.Yaw = Interior.Room.SpawnYaw;
			PossessionHooks.Frozen = true;
			Toast(((Interior.BenchNote != null) ? ("Home. " + Interior.BenchNote) : ("Inside the " + Interior.Name)) + "   (E at the door to leave)");
			RoyalVisit(house);
		}

		private void WakeAboard()
		{
			Actor wakeAboard = _wakeAboard;
			if (_host == null || Cabin.Inside || DeathTimer >= 0f || wakeAboard == null)
			{
				_wakeAboard = null;
			}
			else
			{
				if (_dive != Dive.None)
				{
					return;
				}
				_wakeAboard = null;
				bool flag = false;
				try
				{
					flag = wakeAboard.isAlive() && _host.is_inside_boat && _host.inside_boat != null && ((ActorSimpleComponent)_host.inside_boat).actor == wakeAboard;
				}
				catch
				{
				}
				if (flag)
				{
					Boats.Adopt(_host, wakeAboard);
					ExitHouse();
					if (Cabin.Enter(wakeAboard, _host))
					{
						Talk.Reset(_scene.LookAt);
						Work.Reset();
						_outsideYaw = _view.Yaw;
						_view.Yaw = Cabin.Room.SpawnYaw;
						PossessionHooks.Frozen = true;
						_ashoreCheckAt = 0f;
						Log.Info("first person aboard: into her " + Cabin.Name);
						Toast("Aboard, in the " + Cabin.Name + "   (E at the door to go ashore)");
					}
					else
					{
						bool wet;
						Vector2 lastPos = Boats.Disembark(_host, wakeAboard, out wet);
						_lastPos = lastPos;
						Log.Info("first person aboard a boat with no cabin: put " + (wet ? "over the side" : "ashore"));
					}
				}
			}
		}

		private void WakeIndoors()
		{
			if (_wakeAboard != null)
			{
				WakeAboard();
			}
			if (_wakeIn == null)
			{
				return;
			}
			if (_host == null || Interior.Inside || Cabin.Inside || Mine.Inside || DeathTimer >= 0f)
			{
				_wakeIn = null;
				return;
			}
			if (_dive != Dive.None)
			{
				_wakeSince = Time.unscaledTime;
				return;
			}
			Building wakeIn = _wakeIn;
			if (HouseInterior.HouseAt(_host) != wakeIn)
			{
				_wakeIn = null;
			}
			else if (!(Time.unscaledTime - _wakeSince < 0.1f))
			{
				_wakeIn = null;
				string text = null;
				try
				{
					text = HouseInterior.Barred(wakeIn, Body ?? _host);
				}
				catch
				{
				}
				if (text != null)
				{
					Log.Info("first person on a house's tile: its door is barred to you, staying out");
					return;
				}
				Log.Info("first person in a house: into its room");
				EnterHouse(wakeIn);
				WakeAtPlace(wakeIn);
			}
		}

		internal void EnterRoomForTest(List<Actor> occupants, string race = "human", int tier = 2, string style = null)
		{
			Talk.Reset(_scene.LookAt);
			Interior.EnterVirtual(TerrainCache.Rgb(214, 196, 160), TerrainCache.Rgb(168, 62, 50), occupants, race, tier, style);
			_outsideYaw = _view.Yaw;
			_view.Yaw = Interior.Room.SpawnYaw;
			PossessionHooks.Frozen = true;
		}

		internal void LeaveRoomForTest()
		{
			ExitHouse();
		}

		internal void EnterHouseForTest(Building house)
		{
			EnterHouse(house);
		}

		internal void ExitHouseForTest()
		{
			ExitHouse();
		}

		internal string DescribeForTest(Actor a)
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append($"pos {a.current_position} h {a.position_height:F2} alive {a.isAlive()} insideBuilding {a.is_inside_building} insideBoat {a.is_inside_boat}");
			stringBuilder.Append($" collected {_scene.Tagged.Contains(a)} eye {_view.X:F1},{_view.Y:F1}");
			try
			{
				Sprite spriteToRender = a.getSpriteToRender();
				stringBuilder.Append((spriteToRender == null) ? " sprite null" : $" sprite {spriteToRender.name} tex {spriteToRender.texture?.name} readable {spriteToRender.texture?.isReadable} rect {spriteToRender.rect}");
				SpriteImage spriteImage = _sprites.Get(spriteToRender);
				if (spriteImage == null)
				{
					stringBuilder.Append(" image null");
				}
				else
				{
					int num = 0;
					uint[] pixels = spriteImage.Pixels;
					for (int i = 0; i < pixels.Length; i++)
					{
						if (pixels[i] >> 24 >= 128)
						{
							num++;
						}
					}
					stringBuilder.Append($" image {spriteImage.Width}x{spriteImage.Height} opaque {num} rows {spriteImage.OpaqueBottom}-{spriteImage.OpaqueTop} depth {spriteImage.CreatureShape.Depth}");
				}
			}
			catch (Exception ex)
			{
				stringBuilder.Append(" error " + ex.GetType().Name + ": " + ex.Message);
			}
			return stringBuilder.ToString();
		}

		private void ExitHouse()
		{
			if (Interior.Inside)
			{
				Talk.Reset(_scene.LookAt);
				if (Interior.Fitted && !Interior.Virtual && Interior.House != null && Interior.Room.Fit != null && !float.IsNaN(Interior.Room.Fit.DoorWallX))
				{
					Interior room = Interior.Room;
					Vector2 at = Interior.ToWorld(new Vector2(room.Fit.DoorWallX + room.DoorOutX * 0.35f, room.Fit.DoorWallY + room.DoorOutY * 0.35f));
					NoteCrossing(Interior.WorldZ(_view.Z), stepIn: false);
					Interior.Exit();
					_audio.PlayDoor();
					PossessionHooks.Frozen = false;
					PlaceBody(_host, at);
					_view.Yaw = Mathf.Atan2(room.DoorOutY, room.DoorOutX);
				}
				else
				{
					Interior.Exit();
					_audio.PlayDoor();
					PossessionHooks.Frozen = false;
					_view.Yaw = _outsideYaw + MathF.PI;
					_eyeZ = 0f;
				}
			}
		}

		private void RenderInterior(float dt)
		{
			RoomClock.Begin();
			Actor host = _host;
			if (!Interior.Virtual && !HouseInterior.IsEnterable(Interior.House))
			{
				ExitHouse();
				Toast("The house is gone!");
				return;
			}
			Interior.KeepIndoors();
			Vector2 vector = (MoveKeysTaken ? Vector2.zero : PossessionHooks.MoveKeys()) + PossessionHooks.ScriptedInput;
			RoomFloors.Begin(Interior.Room);
			FightRoomCarried(Interior, host);
			Interior.Move(vector.y, vector.x, _view.Yaw, dt, IndoorPace(host, vector));
			if (Interior.OutOfDoor(Interior.Eye, WalkWay(vector)))
			{
				StepOutOfDoor();
				return;
			}
			Moving = vector != Vector2.zero;
			Swimming = false;
			float num = 1f;
			try
			{
				SpriteImage spriteImage = _sprites.Get(host.getSpriteToRender());
				if (spriteImage != null)
				{
					HostImage = spriteImage;
					num = Mathf.Clamp(Stature.Of(host, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(host.current_scale.y), dt), 0.3f, 2f);
				}
			}
			catch
			{
			}
			RoomFloors.SetBody(num);
			Interior.Eye = RoomFloors.Settle(Interior.Eye, dt);
			FightRoomBegin(Interior, host);
			float num2 = RoomJumpLift(host, num, RoomFloors.Shown + num * 0.9f * Settings.EyeHeight, dt);
			_gait.Update(Interior.Eye, dt, !RoomAirborne, Moving && !RoomAirborne, 0f, PossessionHooks.Walking ? 1f : 0f, num);
			BobPhase = _gait.Phase;
			Stride = _gait.Stride;
			float num3 = Mathf.Cos(_view.Yaw);
			float num4 = Mathf.Sin(_view.Yaw);
			Vector2 vector2 = PoseShift(Interior.Eye);
			_view.X = Interior.Eye.x + vector2.x + num4 * _gait.Lateral * HeadBobNow;
			_view.Y = Interior.Eye.y + vector2.y - num3 * _gait.Lateral * HeadBobNow;
			Vector2 offset;
			float up;
			bool flag = BeastView.Update(host, _view.Yaw, _pitchDegrees, num, swimming: false, Moving, Feedback.SinceAttack, Feedback.LastAttackMelee, dt, out offset, out up);
			if (flag)
			{
				_view.X += offset.x;
				_view.Y += offset.y;
			}
			_view.Z = RoomFloors.Shown + num2 + (flag ? up : (num * 0.9f)) * Settings.EyeHeight + _gait.Vertical * HeadBobNow;
			_view.Z = PoseEyeZ(_view.Z, RoomFloors.Shown, num);
			_view.Z = RoomFloors.EyeUnder(_view.Z, Interior.Eye, dt);
			_view.Z = RoomEyeEase(_view.Z, RoomFloors.Shown, dt);
			_view.Pitch = LookSlope(_pitchDegrees + Feedback.Kick * 1.5f - _gait.Nod * HeadBobNow);
			_view.Fov = (VrSquare ? 105f : Settings.FieldOfView) * (MathF.PI / 180f);
			_fovKick = 0f;
			VrRoomHead(ref _view, RoomFloors.Shown, dt);
			WeaponsIndoors(host, dt);
			_frameClock.Restart();
			_sprites.BeginFrame();
			bool gpuRoomsWanted = GpuRoomsWanted;
			int num5 = RenderWidth(gpuRoomsWanted);
			int num6 = Mathf.Max(90, Mathf.RoundToInt((float)(num5 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
			if (VrSquare)
			{
				num6 = num5;
			}
			Renderer.Resize(num5, num6);
			Renderer.Time = SeaNow;
			Interior.UpdateDoor(dt);
			RoomClock.Lap(RoomClock.P.Move);
			bool flag2 = false;
			bool num7 = gpuRoomsWanted && Settings.WindowView == 1 && !Interior.Virtual && Interior.House != null && !VrRunning;
			ViewState viewState = _view;
			if (num7)
			{
				viewState = Interior.OutsideView(_view, CacheHeight);
				FollowLight(_day.Air);
				if (_gpu != null)
				{
					_gpu.KeepSunField(_sampler.Cache);
				}
				_sampler.Tick(viewState.X, viewState.Y);
			}
			int num8;
			if (num7)
			{
				num8 = (Interior.PortalInView(_view) ? 1 : 0);
				if (num8 != 0)
				{
					goto IL_0640;
				}
			}
			else
			{
				num8 = 0;
			}
			if (Time.unscaledTime >= _roomWeatherAt)
			{
				_roomWeatherAt = Time.unscaledTime + 0.5f;
				_scene.GatherCloudsOnly();
				Vector2 pos = ((Interior.House != null) ? new Vector2(Interior.House.current_position.x, Interior.House.current_position.y) : new Vector2(viewState.X, viewState.Y));
				RainOver(pos, 0.5f);
			}
			goto IL_0640;
			IL_0640:
			if (num8 != 0)
			{
				Renderer.Air = _day.Air;
				ApplyViewDistance();
				WindowReach();
				if (WindowGatherNow(dt, out var gatherDt))
				{
					_scene.SkipBuilding = Interior.House;
					Interior.OutdoorsGathered = true;
					try
					{
						CollectScene(viewState, viewState, host, 0f, _day.Air, gatherDt);
					}
					finally
					{
						_scene.SkipBuilding = null;
					}
				}
				ApplyWeather(_day.Air, new Vector2(viewState.X, viewState.Y), dt);
				Renderer.Meshes.Clear();
				Renderer.Meshes.AddRange(_scene.Meshes);
				Renderer.Lights.Clear();
				Renderer.Lights.AddRange(_scene.Lights);
				Renderer.Streaks.AddRange(_scene.Streaks);
				RoomClock.Lap(RoomClock.P.Scene);
				flag2 = GpuBackdrop(viewState, num5, num6);
				if (!flag2)
				{
					Renderer.Streaks.Clear();
				}
				if (flag2)
				{
					KeepCabinWorld();
					HouseInterior.BackdropFrames++;
				}
				RoomClock.Lap(RoomClock.P.Backdrop);
			}
			RoomClock.Lap(RoomClock.P.Scene);
			bool flag3 = !gpuRoomsWanted && CanDrawRoomBeside;
			_wasInRoom = !gpuRoomsWanted;
			Renderer.MaxDistance = 60f;
			Renderer.BillboardDistance = 40f;
			Renderer.FadeAt = 0f;
			Renderer.HazeScale = 1f;
			Renderer.RippleStrength = 0f;
			Renderer.OverlayMeshes.Clear();
			Interior room = Interior.Room;
			Atmosphere atmosphere = Atmosphere.Indoor(room.CeilingTop, room.CeilingBottom, _day.Air).SunThroughWindows(_day.Air);
			Renderer.Air = atmosphere;
			if (_litRoom != room || Vec3.Dot(room.Floor.LightDir, atmosphere.Light) < 0.999f)
			{
				room.Floor.LightDir = atmosphere.Light;
				room.Floor.Relight(0, 0, room.Floor.Width, room.Floor.Height);
				_litRoom = room;
			}
			Renderer.Windows.Clear();
			Renderer.Windows.AddRange(room.Windows);
			Renderer.Lights.Clear();
			float num9 = RoomFlicker(0, fire: true);
			if (room.Lights.Count == 0)
			{
				Renderer.Lights.Add(new PointLight
				{
					X = room.HearthX - 0.35f,
					Y = room.HearthY,
					Z = 0.35f,
					Radius = 5.5f,
					R = 0.95f * num9,
					G = 0.64f * num9,
					B = 0.36f * num9,
					Glow = 0.12f
				});
			}
			for (int i = 0; i < room.Lights.Count; i++)
			{
				RoomLight roomLight = room.Lights[i];
				PointLight light = roomLight.Light;
				if (roomLight.Fire)
				{
					float num10 = RoomFlicker(i, fire: true);
					light.R *= num10;
					light.G *= num10;
					light.B *= num10;
				}
				Renderer.Lights.Add(light);
			}
			room.Daylight(1f - _day.Air.Night);
			Renderer.Meshes.Clear();
			_roomVoxels.Clear();
			RoomCombat.Tick(dt);
			if (RoomCombat.Active)
			{
				FirstPersonShots.Update(room.Floor, null);
			}
			Interior.Collect(_roomVoxels, Renderer.Meshes, _sprites, _scene.LookAt, dt);
			Curtains.AddRoom(Renderer.Meshes, Interior, dt);
			RoomCombat.Blaze(Renderer.Meshes, Renderer.Lights, dt);
			RoomCombat.DrawShots(Renderer.Meshes, Interior.Eye);
			AddPosedBodyIndoors(Renderer.Meshes, _roomVoxels, host, num, dt);
			if (flag || PoseShowsBody)
			{
				Renderer.OverlayMeshes.Clear();
			}
			else
			{
				BuildHands(_roomVoxels, host, num, 0f, dt);
			}
			Renderer.Backdrop = null;
			FrameRenderer renderer = Renderer;
			TerrainCache floor = room.Floor;
			ViewState eye = _view;
			if (_poseView > 0.001f && !_vrRoomStereo)
			{
				eye = PoseCamera(_view, num, out var _);
			}
			List<VoxelObject> voxels = _roomVoxels;
			float top = Mathf.Max(room.Top, _view.Z) + 0.5f;
			RoomClock.Lap(RoomClock.P.Collect);
			bool flag4 = false;
			if (_vrRoomStereo)
			{
				flag4 = VrRoomStereo(room, eye, voxels, Interior.Tagged, 1f);
			}
			else if (gpuRoomsWanted)
			{
				if (_gpu != null)
				{
					_gpu.Backdrop = (flag2 ? _cabinWorld : null);
				}
				try
				{
					flag4 = GpuRoomFrame(room, eye, num5, num6, voxels, Interior.Tagged);
				}
				finally
				{
					if (_gpu != null)
					{
						_gpu.Backdrop = null;
					}
				}
			}
			if (!flag4)
			{
				if (flag3)
				{
					StartBesideRoom(delegate
					{
						renderer.Render(floor, eye, NoBillboards, voxels, top);
					}, eye, num5, num6, Interior.Tagged, holes: false);
				}
				else
				{
					if (num5 > 640)
					{
						num5 = 640;
						num6 = Mathf.Max(90, Mathf.RoundToInt((float)(num5 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
						if (VrSquare)
						{
							num6 = num5;
						}
						Renderer.Resize(num5, num6);
					}
					Renderer.Render(floor, eye, NoBillboards, voxels, top);
					_picked = ((Renderer.PickTag >= 0 && Renderer.PickTag < Interior.Tagged.Count) ? Interior.Tagged[Renderer.PickTag] : null);
					UploadFrame(num5, num6);
				}
			}
			RoomClock.Lap(RoomClock.P.Draw);
			FrameMs = (float)(_frameClock.Elapsed.TotalMilliseconds + _besideMainMs);
			_renderMs = Mathf.Lerp(_renderMs, FrameMs, 0.1f);
			AimIndoors(host, num, RoomFloors.Shown, room.Floor);
			Feedback.Update(host, dt, Interior.Tagged, (float x, float y) => 0f);
			ForgeUpdate(host, dt);
			Work.Target = null;
			_audio.Update(new FirstPersonAudio.State
			{
				Position = host.current_position,
				EyeAboveGround = _view.Z,
				Yaw = _view.Yaw,
				Pitch = _view.Pitch,
				Indoors = true,
				Moving = Moving,
				Ground = 8,
				Steps = _gait.Steps,
				Stride = _gait.Stride,
				Health = HealthFraction(host),
				Attacks = Feedback.Attacks,
				Swing = Feedback.LastSwing,
				Melee = Feedback.LastAttackMelee,
				WeaponId = (Holstered ? null : WeaponOf(host)),
				Hits = Feedback.Hits,
				Kills = Feedback.Kills,
				Hurts = Feedback.Hurts,
				HearthDistance = new Vector2(Interior.Room.HearthX - Interior.Eye.x, Interior.Room.HearthY - Interior.Eye.y).magnitude,
				Rain = _rain,
				Wind = 0.12f + 0.3f * _overcast + 0.8f * _scene.Gusts.Strength,
				Hour = _day.Hour,
				Night = _day.Air.Night,
				Habitat = _habitat
			}, dt);
			if (Interior.House != null)
			{
				FirstPersonAudio.InsideFor(FirstPersonAudio.Footprint(Interior.House));
			}
			PossessionHooks.AimPoint = host.current_position;
			Talk.Blocked = TalkBusy || Hub.AnyModeActive || AnyMenu;
			Talk.Indoors = true;
			Talk.Update(host, TalkPick(_picked, Interior.PositionOf, Interior.Eye), Interior.DistanceTo, (Actor a) => Interior.HeadOf(a, _sprites), _scene.LookAt, Interior.Eye, Toast, dt);
			CivilUpdate(host, dt);
			Prompt = null;
			if (!TalkBusy)
			{
				if (Hub.AnyModeActive)
				{
					Prompt = Hub.ModePrompt;
				}
				else if (Talk.Candidate != null)
				{
					string text = Court.TalkHint(host);
					Prompt = TalkPrompt(host, Talk.Candidate) + (Dialogue.IsPerson(Talk.Candidate) ? "|Q:Steal" : "") + text;
					if (text.Length > 0 && PlayInput.GetKeyDown(KeyCode.E) && (PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift)))
					{
						Court.OpenCouncil(null);
					}
				}
				else if ((!(_pitchDegrees < -20f) || !RoomLootPrompt(host)) && !RoomDoorPrompt(host) && !RoomLootPrompt(host) && !BenchPrompt(host) && !TownsRoomPrompt(host))
				{
					ChairPrompt(host);
				}
			}
			RoomClock.Lap(RoomClock.P.Rest);
			RoomPerfEnd(flag2);
		}

		private void HideMapView(bool hide)
		{
			try
			{
				HideGodScreen();
				Camera camera = ((World.world != null) ? World.world.camera : null);
				if (!(camera == null))
				{
					if (hide && _savedCullingMask == -1)
					{
						_savedCullingMask = camera.cullingMask;
						camera.cullingMask = 0;
					}
					else if (!hide && _savedCullingMask != -1)
					{
						camera.cullingMask = _savedCullingMask;
						_savedCullingMask = -1;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("map camera: " + ex.Message);
			}
		}

		private void FollowLight(Atmosphere air)
		{
			Vec3 a = (_sampler.Cache.LightDir = air.CastLight);
			float num = Mathf.Max(air.LightR, Mathf.Max(air.LightG, air.LightB));
			bool relightNow = _relightNow;
			if (_relightNow || (Vec3.Dot(a, _sampler.SweepLight) < 0.9f && num > 0.15f))
			{
				if (!_sampler.RelightWithNextField())
				{
					_sampler.RelightAll();
				}
				_relightNow = false;
			}
			Settings settings = Settings;
			bool mapDrawn = settings != null && settings.Shadows > 0 && settings.ShadowDarkness > 0f && num >= 0.05f;
			_sampler.TickSunField(relightNow, num, mapDrawn);
		}

		private void ApplyWeather(Atmosphere air, Vector2 pos, float dt)
		{
			Weather weather = _scene.Weather;
			weather.Global = _day.AgeFalls;
			weather.GlobalStrength = _day.AgeFallStrength;
			Renderer.Weather = weather;
			float num = 0f;
			Precip precip = Precip.None;
			if (weather.Storms.Count > 0)
			{
				num = weather.Density(pos.x, pos.y, 0.15f, Renderer.Time, out var which);
				if (which >= 0)
				{
					StormCloud stormCloud = weather.Storms[which];
					if (stormCloud.Drops == Precip.Seeds)
					{
						num *= 0.35f;
					}
					else
					{
						precip = stormCloud.Drops;
					}
					_overcastColor = TerrainCache.Lerp(_overcastColor, stormCloud.Color, 1f - Mathf.Exp(-2f * dt));
				}
			}
			_overcast = Mathf.Lerp(_overcast, Mathf.Clamp01(num * 1.5f), 1f - Mathf.Exp(-1.2f * dt));
			if (_overcast > 0.01f)
			{
				Renderer.Air = air.Overcast(_overcast, _overcastColor);
			}
			ApplySnowfall(weather, precip, num, dt);
			float num2 = ((precip == Precip.Rain || precip == Precip.Acid || precip == Precip.Rage) ? Mathf.Clamp01(num * 1.6f) : 0f);
			if (weather.Global == Precip.Rain)
			{
				num2 = Mathf.Max(num2, weather.GlobalStrength);
			}
			_rain = Mathf.Lerp(_rain, num2, 1f - Mathf.Exp(-1.5f * dt));
		}

		private void RainOver(Vector2 pos, float dt)
		{
			Weather weather = _scene.Weather;
			float num = 0f;
			Precip precip = Precip.None;
			if (weather.Storms.Count > 0)
			{
				num = weather.Density(pos.x, pos.y, 0.15f, Renderer.Time, out var which);
				if (which >= 0 && weather.Storms[which].Drops != Precip.Seeds)
				{
					precip = weather.Storms[which].Drops;
				}
			}
			float num2 = ((precip == Precip.Rain || precip == Precip.Acid || precip == Precip.Rage) ? Mathf.Clamp01(num * 1.6f) : 0f);
			if (_day.AgeFalls == Precip.Rain)
			{
				num2 = Mathf.Max(num2, _day.AgeFallStrength);
			}
			_rain = Mathf.Lerp(_rain, num2, 1f - Mathf.Exp(-1.5f * dt));
		}

		private void AdaptDetail()
		{
			if (HoldDetailForTest)
			{
				return;
			}
			if (_gpuShown)
			{
				float num = ((FrameMs > 28f) ? (-0.02f) : ((FrameMs < 16f) ? 0.01f : 0f));
				_scene.Detail = Mathf.Clamp(_scene.Detail + num, 0.45f, 1f);
				Renderer.ShadowDistance = 22f * _scene.Detail;
				_creatureDetail = Mathf.Clamp(_creatureDetail + num, 0.4f, 1f);
				return;
			}
			float objectsMs = Renderer.ObjectsMs;
			float num2 = _scene.Detail;
			if (objectsMs > 9f)
			{
				num2 -= 0.04f;
			}
			else if (objectsMs < 5f)
			{
				num2 += 0.01f;
			}
			_scene.Detail = Mathf.Clamp(num2, 0.45f, 1f);
			Renderer.ShadowDistance = 22f * _scene.Detail;
			float meshMs = Renderer.MeshMs;
			if (meshMs > 14f)
			{
				_creatureDetail -= 0.03f;
			}
			else if (meshMs < 9f)
			{
				_creatureDetail += 0.01f;
			}
			_creatureDetail = Mathf.Clamp(_creatureDetail, 0.4f, 1f);
		}

		private FirstPersonAudio.State OutdoorSound(Vector2 pos, float ground, bool dead, bool wantsToMove)
		{
			TerrainCache cache = _sampler.Cache;
			int num = Mathf.Clamp((int)pos.x, 0, cache.Width - 1);
			int num2 = Mathf.Clamp((int)pos.y, 0, cache.Height - 1) * cache.Width + num;
			bool flag = cache.Flags.Length > num2 && (cache.Flags[num2] & 0x81) == 1;
			float num3 = (Swimming ? _swimSurface : ground);
			Actor host = _host;
			return new FirstPersonAudio.State
			{
				Position = pos,
				EyeAboveGround = Mathf.Max(0.2f, _eyeZ - num3),
				Yaw = _view.Yaw,
				Pitch = _view.Pitch,
				Swimming = Swimming,
				Moving = (Swimming ? wantsToMove : Moving),
				Airborne = (!dead && host != null && _standLift > 0.05f),
				Dead = dead,
				Ground = (byte)((OnFloor && !dead) ? FloorSurface : ((DockDecks.On(pos) || BoatHelm.Steers(host)) ? 8 : ((cache.Materials.Length <= num2) ? 1 : cache.SurfaceAt(pos.x, pos.y)))),
				Shallows = (flag && !Swimming && !DockDecks.On(pos) && !BoatHelm.Steers(host)),
				Steps = _gait.Steps,
				Stride = _gait.Stride,
				Run = PossessionHooks.RunBlend,
				Crouch = _crouch,
				SwimPhase = SwimPhase,
				Health = ((dead || host == null) ? 1f : HealthFraction(host)),
				Attacks = Feedback.Attacks,
				Swing = Feedback.LastSwing,
				Melee = Feedback.LastAttackMelee,
				WeaponId = ((dead || host == null || Holstered) ? null : WeaponOf(host)),
				Hits = Feedback.Hits,
				Kills = Feedback.Kills,
				Hurts = Feedback.Hurts,
				Wind = 0.12f + 0.88f * Mathf.Clamp01((ground - 2.5f) / 6f) + 0.3f * _overcast + 0.8f * _scene.Gusts.Strength,
				Rain = _rain,
				HearthDistance = -1f,
				Hour = _day.Hour,
				Night = _day.Air.Night,
				Habitat = _habitat,
				Heavy = ((host != null) ? Weight.Of(host) : 1f),
				Armor = FirstPersonAudio.ArmorOf(host),
				Woman = Models.IsFemale(host),
				Voice = FirstPersonAudio.VoiceOf(host),
				Velocity = PossessionHooks.Velocity * PossessionHooks.FullSpeed(host),
				Full = PossessionHooks.FullSpeed(host),
				Jumps = PossessionHooks.Jump.Starts,
				Landings = PossessionHooks.Jump.Landings,
				LandSpeed = PossessionHooks.Jump.LandSpeed
			};
		}

		private static string WeaponOf(Actor a)
		{
			try
			{
				return (a.getWeapon() == null) ? null : a.getWeaponAsset()?.id;
			}
			catch
			{
				return null;
			}
		}

		private static float HealthFraction(Actor a)
		{
			try
			{
				return (float)a.getHealth() / (float)Mathf.Max(1, a.getMaxHealth());
			}
			catch
			{
				return 1f;
			}
		}

		private static float DistanceToBuilding(Building b, Vector2 from)
		{
			return (new Vector2(b.current_tile.posV3.x, b.current_tile.posV3.y) - from).magnitude;
		}

		private Vector3 HeadOfWorld(Actor a)
		{
			Vector2 current_position = a.current_position;
			float num = _sampler.Cache.SampleHeight(current_position.x, current_position.y);
			float num2 = 1f;
			try
			{
				SpriteImage spriteImage = _sprites.Get(a.getSpriteToRender());
				if (spriteImage != null)
				{
					num2 = Stature.Peek(a, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(a.current_scale.y));
				}
			}
			catch
			{
			}
			return new Vector3(current_position.x, current_position.y, num + num2 + 0.15f);
		}

		internal Vector3 SpeakerHead()
		{
			if (Talk.Speaker != null)
			{
				if (!Interior.Inside)
				{
					return HeadOfWorld(Talk.Speaker);
				}
				return Interior.HeadOf(Talk.Speaker, _sprites);
			}
			return Vector3.zero;
		}

		private SpriteImage HeldItem(Actor host)
		{
			try
			{
				if (host.getWeapon() == null && host.isCarryingResources())
				{
					return null;
				}
			}
			catch
			{
			}
			return People.HeldItem(host, _sprites);
		}

		private void CrouchStep(Actor host, Vector2 pos, float body, bool never, float dt)
		{
			bool flag = PossessionHooks.Installed && PossessionHooks.Crouched && !never;
			if (never && PossessionHooks.Crouched)
			{
				PossessionHooks.Crouched = false;
			}
			if (!flag && _crouch > 0.3f && host != null)
			{
				if (_standTry < 0f)
				{
					_standTry = Time.unscaledTime;
				}
				if (Time.unscaledTime - _standTry < 0.35f)
				{
					Colliders.SetBody(_sampler.Cache.SampleHeight(pos.x, pos.y), 0f, body, body * 0.9f * Settings.EyeHeight);
					if ((Colliders.PushOut(pos, PossessionHooks.BodyRadius * 0.7f) - pos).sqrMagnitude > 0.0009f)
					{
						PossessionHooks.Crouched = true;
						flag = true;
						_standTry = -1f;
					}
				}
			}
			else
			{
				_standTry = -1f;
			}
			_crouch = Mathf.Lerp(_crouch, flag ? 1f : 0f, 1f - Mathf.Exp((0f - dt) / 0.07f));
			if (_crouch < 0.002f)
			{
				_crouch = 0f;
			}
			else if (_crouch > 0.998f)
			{
				_crouch = 1f;
			}
		}

		private void BuildHands(List<VoxelObject> voxels, Actor host, float body, float run, float dt)
		{
			string text = null;
			_hands.Aspect = (float)Renderer.Height / (float)Mathf.Max(1, Renderer.Width);
			People.HostCarried = null;
			if (BoatHelm.Steers(host))
			{
				BuildHelmHands(host, dt);
				return;
			}
			try
			{
				text = ((host.getWeapon() == null) ? null : host.getWeaponAsset()?.id);
			}
			catch
			{
			}
			HandsInput input = new HandsInput
			{
				Body = HostImage,
				Owner = host,
				Look = People.AgeOf(host),
				Humanoid = People.IsHumanoid(host),
				Species = ((host.asset != null) ? host.asset.id : null),
				ItemId = (Holstered ? null : text),
				Icon = ((!Holstered && text != null) ? HeldItem(host) : null),
				SinceAttack = ((Feedback.LastSwing == Swing.Attack) ? _melee.AttackClock(Feedback.SinceAttack) : Feedback.SinceAttack),
				Charge = _melee.Charge,
				Melee = Feedback.LastAttackMelee,
				Attacks = Feedback.Attacks,
				Swing = Feedback.LastSwing,
				Guard = _guard.Raise,
				Hurt = Feedback.Hurt,
				Swimming = Swimming,
				SwimPhase = SwimPhase,
				Stride = _gait.Stride,
				GaitPhase = _gait.Phase,
				Run = run,
				BodyHeight = body,
				Dt = dt,
				Bow = (_bow.InHand ? _bow.State : null),
				ArrowModel = Models.Get("arrow"),
				Time = Time.unscaledTime,
				GunModel = (_blaster.InHand ? Models.Get("held_blaster") : null),
				Blaster = (_blaster.InHand ? _blaster.State : null),
				Shotgun = (_shotgun.InHand ? _shotgun.State : null),
				ShotgunModel = (_shotgun.InHand ? Models.Get("held_shotgun") : null),
				ShellModel = (_shotgun.InHand ? Models.Get("shotgun_shell") : null),
				ArmLook = ArmLookOf(host),
				Armor = People.ChestArmor(host),
				Skin = People.GameSkin(host),
				Sleeve = People.LookSleeve(host),
				Binoculars = (Binoculars.HandsShown ? Binoculars.Lift : 0f),
				Compass = Compass,
				StoppedAt = Feedback.StoppedAt,
				StoppedWeight = Feedback.StoppedWeight,
				StoppedRings = Feedback.StoppedRings,
				ClashAge = Feedback.SinceClash,
				ClashWeight = ((Feedback.SinceClash < 0.6f) ? Feedback.ClashWeight : 0f),
				ClashRings = Feedback.ClashRings
			};
			AbilityHands(ref input);
			BandageHands(host, ref input);
			MeshModel meshModel = ((Holstered || _bow.InHand || _blaster.InHand || _shotgun.InHand) ? null : Held.Model(host));
			Carried item = ((meshModel != null) ? Held.Item(host) : null);
			if (meshModel == null && EatingNow(out var eaten))
			{
				meshModel = eaten.Model(0);
				item = eaten;
			}
			if (meshModel != null)
			{
				input.HeldModel = meshModel;
				input.HeldKind = Held.Kind(host);
			}
			ForgeHands(host, ref input);
			DigHands(host, ref input);
			TorchHands(host, ref input);
			HeldItemHands(item, meshModel, ref input);
			VrHandsInput(ref input, voxels == _scene.Voxels || _vrRoomStereo);
			_hands.Build(Renderer.OverlayMeshes, voxels, VrHandsView(_view), in input);
			VrHandsAfter(voxels, host);
			_blaster.Emit(_hands, SceneCollector.FxPool, dt);
			_shotgun.Emit(_hands, dt);
			FlameSwordFx(voxels, dt);
			TorchFx(voxels, host, dt);
			WeaponGlowFx();
		}

		private void UploadFrame(int width, int height, bool holes = false)
		{
			if (Frame == null || Frame.width != width || Frame.height != height)
			{
				if (Frame != null)
				{
					UnityEngine.Object.Destroy(Frame);
				}
				Frame = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonFrame"
				};
			}
			Frame.SetPixelData(Renderer.Pixels, 0);
			Frame.Apply(updateMipmaps: false);
			_gpuShown = false;
			_frameHoles = holes;
			FramesShown++;
			_shown = Renderer.Projection;
			_eyeNow = _shown.View;
		}

		private void OnGUIBody()
		{
			if (!_active && _dive != Dive.Out)
			{
				DrawGodGui();
			}
			if (!_active && GodShown && Event.current.type == EventType.Repaint)
			{
				_hud.DrawFps();
			}
			if ((!_active && _dive != Dive.Out) || ShownFrame == null)
			{
				DrawRun();
				DrawHelpWindow();
				DrawSettingsMenu();
				DrawPad();
				return;
			}
			long timestamp = Stopwatch.GetTimestamp();
			GUI.depth = -1000;
			if (_active && Scene.On && DeathTimer < 0f)
			{
				_hud.DrawScene();
				DrawHelpWindow();
				DrawSettingsMenu();
				return;
			}
			if (Event.current.type == EventType.Repaint)
			{
				_hud.Draw(_diveViewAlpha, _diveHudAlpha);
			}
			VrWindowsLayer();
			if (_active)
			{
				if (Craft.Open && DeathTimer < 0f)
				{
					_hud.DrawCraftPanel();
				}
				if (Royal.Open && DeathTimer < 0f)
				{
					_hud.DrawCouncil();
				}
				if (Royal.ObeliskOpen && DeathTimer < 0f)
				{
					_hud.DrawObelisk();
				}
				if (HomeCard.Open && DeathTimer < 0f)
				{
					_hud.DrawHomeCard();
				}
				if (Chest.Open && DeathTimer < 0f)
				{
					_hud.DrawStorage();
				}
				if (Ledger.Open && DeathTimer < 0f)
				{
					_hud.DrawLedger();
				}
				DrawFeatures();
				DrawCivil();
				DrawRun();
			}
			if (_active)
			{
				_hud.DrawFps();
			}
			DrawHelpWindow();
			DrawSettingsMenu();
			DrawPad();
			_perf.AddGui((double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency);
		}

		public string SaveScreenshot()
		{
			byte[] array = ShownPng();
			if (array == null)
			{
				return null;
			}
			string text = Path.Combine(Log.Folder, "fp_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
			File.WriteAllBytes(text, array);
			Toast("Saved " + Path.GetFileName(text));
			Log.Info("screenshot " + text);
			return text;
		}

		internal void PlayHorn()
		{
			_audio.PlayHorn();
		}

		internal List<KeyValuePair<float, Color>> CompassMarks()
		{
			_marks.Clear();
			if (_host == null)
			{
				return _marks;
			}
			Vector2 p = _host.current_position;
			try
			{
				if (_host.city != null)
				{
					Mark(_host.city.getTile(), new Color(0.45f, 1f, 0.45f));
				}
			}
			catch
			{
			}
			try
			{
				if (Royal.Active != null && Royal.Active.Objective != null)
				{
					Mark(Royal.Active.Objective.getTile(), new Color(1f, 0.3f, 0.25f));
				}
			}
			catch
			{
			}
			try
			{
				if (Royal.Raid != null && Royal.Raid.Target != null)
				{
					Mark(Royal.Raid.Target.getTile(), new Color(1f, 0.6f, 0.2f));
				}
			}
			catch
			{
			}
			AddFeatureMarks(_marks, p);
			AddCivilMarks(_marks, p);
			AddServiceMarks(_marks, p);
			AddPingMarks(_marks, p);
			return _marks;
			void Mark(WorldTile t, Color c)
			{
				if (t != null)
				{
					Vector2 vector = new Vector2(t.posV3.x, t.posV3.y) - p;
					if (vector.sqrMagnitude > 9f)
					{
						_marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector.y, vector.x), c));
					}
				}
			}
		}

		internal void ForceRelight()
		{
			_relightNow = true;
		}

		internal SpriteImage SpriteImageForTest(Sprite s)
		{
			return _sprites.Get(s);
		}

		internal void SaveFrame(string name)
		{
			byte[] array = ShownPng();
			if (array != null)
			{
				File.WriteAllBytes(Path.Combine(Log.Folder, name), array);
				Log.Info("frame saved " + name);
			}
		}

		internal void Toast(string message)
		{
			message = Renames.Apply(message);
			if (ToastText != null && message != ToastText && _toastFrame == Time.frameCount && Time.unscaledTime < ToastUntil)
			{
				if (_toastNext.Count < 3 && !_toastNext.Contains(message))
				{
					_toastNext.Enqueue(message);
				}
			}
			else
			{
				ToastText = message;
				ToastUntil = Time.unscaledTime + 2.5f;
				_toastFrame = Time.frameCount;
			}
		}

		private void ToastTick()
		{
			if (_toastNext.Count != 0 && (ToastText == null || !(Time.unscaledTime < ToastUntil)))
			{
				ToastText = _toastNext.Dequeue();
				ToastUntil = Time.unscaledTime + 2.5f;
			}
		}

		internal static string Describe(Actor a)
		{
			string text = ((a.asset != null) ? a.asset.id.Replace('_', ' ') : "creature");
			string text2 = People.AgeLabel(a);
			if (text2 != null)
			{
				text = text + " " + text2;
			}
			string text3 = null;
			try
			{
				text3 = a.getName();
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(text3))
			{
				return text3 + " (" + text + ")";
			}
			return text;
		}

		private static bool IsTyping()
		{
			if (Instance != null && Instance.KeyboardTaken)
			{
				return true;
			}
			GameObject gameObject = ((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject : null);
			if (gameObject == null)
			{
				return false;
			}
			InputField component = gameObject.GetComponent<InputField>();
			if (component != null)
			{
				return component.isFocused;
			}
			return false;
		}

		private void OnDestroy()
		{
			if (!_steppedAside)
			{
				FrameGuard.Uninstall();
				SettleBesideRender();
				GameClock.Restore();
				MapNight.Off();
				ScreenSync.Restore();
				FrameCaps.Restore();
				Deactivate();
				EndDive();
				if (Frame != null)
				{
					UnityEngine.Object.Destroy(Frame);
				}
				DisposeGpu();
				DisposeVr();
			}
		}

		internal float DarkGloom()
		{
			return Mathf.Max(_active ? Mathf.Clamp01((_overcast - 0.1f) / 0.6f) : 0f, AgeGloom(_day.Age));
		}

		private static float AgeGloom(string age)
		{
			switch (age)
			{
			case "age_dark":
				return 1f;
			case "age_despair":
				return 0.9f;
			case "age_chaos":
			case "age_tears":
			case "age_ash":
				return 0.75f;
			case "age_moon":
				return 0.3f;
			case "age_ice":
				return 0.2f;
			default:
				return 0f;
			}
		}

		private void BeginDiveIn(bool returning)
		{
			Camera camera = ((World.world != null) ? World.world.camera : null);
			if (Settings.DiveSpeed <= 0f || _noDive || VrFades || camera == null)
			{
				if (returning)
				{
					EndDive();
				}
				return;
			}
			if (!returning)
			{
				_diveT = 0f;
				_diveZoom = Mathf.Max(10f, camera.orthographicSize);
				_divePrewarm = true;
			}
			_diveMapFrom = camera.transform.position;
			if (!returning)
			{
				NoteDiveFromGod(_diveMapFrom);
				if (_diveFromGod)
				{
					_divePrewarm = false;
				}
			}
			_dive = Dive.In;
			_diveLength = DivePath.Seconds(_diveZoom);
			_diveTurn = DivePath.Wrap(MathF.PI / 2f + GodDiveTurn() - _view.Yaw);
			_diveHands = false;
			_diveHudAlpha = 0f;
			float closeness = DivePath.Ease(_diveT);
			_diveViewAlpha = (_diveFromGod ? 1f : DivePath.ViewAlpha(closeness, DivePath.Zoom(closeness, _diveZoom)));
			PossessionHooks.Frozen = true;
		}

		private void LeaveFirstPerson()
		{
			CrashGuard.Leave("gpurooms");
			Building house;
			Vector2 at;
			float z;
			bool flag = LeavingIndoors(out house, out at, out z);
			bool flag2 = Settings.DiveSpeed > 0f && !_noDive && !VrFades && ShownFrame != null && _host != null && (!Interior.Inside || flag) && World.world != null && World.world.camera != null && _sampler.Cache != null && _sampler.Cache.Width > 0;
			bool flag3 = _dive == Dive.In;
			Actor host = _host;
			bool flag4 = host != null && (DeathTimer >= 0f || !host.isAlive());
			ViewState view = _view;
			Vector2 vector = _lastPos;
			if (flag)
			{
				view.X = at.x;
				view.Y = at.y;
				view.Z = z;
				vector = at;
			}
			Deactivate();
			if (flag && !flag4)
			{
				RoofDive.LeftIndoors(host, house, at, flag2 ? DivePath.Seconds((_diveZoom < 10f) ? 30f : _diveZoom) : 0f);
			}
			_diveFromFly = false;
			if (FlyFromEye(view))
			{
				if (flag3)
				{
					EndDive();
				}
				return;
			}
			if (!flag2)
			{
				if (flag3)
				{
					EndDive();
				}
				if (Settings.DiveSpeed > 0f)
				{
					Log.Info($"left first person without the rise out (no dive {_noDive}, a picture {ShownFrame != null}, body {host != null}, in a room {Interior.Inside})");
				}
				GodRiseTurn(Settings.GodView && GodCover(_diveZoom) >= 1f);
				return;
			}
			if (!flag3)
			{
				_diveT = 1f;
				_diveMapFrom = vector;
				if (_diveZoom < 10f)
				{
					_diveZoom = 30f;
				}
			}
			_dive = Dive.Out;
			_diveLength = DivePath.Seconds(_diveZoom) * 0.85f;
			_diveClock = 0f;
			_diveBody = (flag4 ? null : host);
			_diveFromDeath = flag4;
			_diveEye = view;
			_diveFrom = vector;
			_diveGround = _sampler.Cache.SampleHeight(vector.x, vector.y);
			_diveFromGod = Settings.GodView && GodCover(_diveZoom) >= 1f;
			_diveTurn = DivePath.Wrap(MathF.PI / 2f + GodRiseTurn(_diveFromGod) - view.Yaw);
			_diveGodFrom = vector;
			_diveGodGround = _diveGround;
			_host = host;
			HideMapView(hide: false);
		}

		private void EndDive()
		{
			Dive dive = _dive;
			_dive = Dive.None;
			_diveViewAlpha = (_diveHudAlpha = 1f);
			_scene.ShowHost = false;
			Renderer.ShadowFocusSet = false;
			switch (dive)
			{
			case Dive.In:
				PossessionHooks.Frozen = Interior.Inside;
				if (_active)
				{
					HideMapView(hide: true);
				}
				break;
			case Dive.Out:
				try
				{
					Camera camera = ((World.world != null) ? World.world.camera : null);
					MoveCamera moveCamera = ((World.world != null) ? World.world.move_camera : null);
					if (camera != null && _diveZoom >= 10f)
					{
						camera.orthographicSize = _diveZoom;
						if (moveCamera != null)
						{
							moveCamera.setTargetZoom(_diveZoom);
						}
						World.world.setZoomOrthographic(_diveZoom);
					}
				}
				catch (Exception ex)
				{
					Log.Error("map zoom: " + ex.Message);
				}
				if (!_active)
				{
					_host = null;
					HideMapView(hide: false);
				}
				if (_diveFromGod)
				{
					_godGround = _diveGodGround;
					GodFollowAfterDive(_diveBody);
				}
				_diveBody = null;
				break;
			}
		}

		private ViewState DiveCamera(ViewState eye, float aimZ, float body, float dt, int width, int height, out float bodyFade, out bool prewarm)
		{
			_diveT = Mathf.Min(1f, _diveT + dt * Settings.DiveSpeed / _diveLength);
			float num = DivePath.Ease(_diveT);
			_diveViewAlpha = (_diveFromGod ? 1f : DivePath.ViewAlpha(num, DivePath.Zoom(num, _diveZoom)));
			prewarm = _divePrewarm && _diveViewAlpha <= 0f;
			_divePrewarm = false;
			if (prewarm)
			{
				num = DivePath.ClosenessAt(16.199999f, _diveZoom);
			}
			ViewState viewState = GodDiveCamera(DivePath.Camera(eye, aimZ, num, _diveZoom, _diveTurn, width, height), new Vector2(eye.X, eye.Y), aimZ, num);
			_diveHudAlpha = ((_diveViewAlpha >= 1f) ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, num)) : 0f);
			float num2 = DivePath.Distance(viewState, eye);
			bodyFade = BodyFade(num2, body);
			if (!_diveHands && num2 < 0.4f * body)
			{
				_diveHands = true;
				_hands.Lower();
			}
			return viewState;
		}

		private static float BodyFade(float distance, float body)
		{
			return Mathf.Clamp01((distance - 0.45f * body) / (1.4f * body));
		}

		private void CollectScene(ViewState camera, ViewState eye, Actor host, float bodyFade, Atmosphere air, float dt)
		{
			bool flag = BeastView.ShowsBody(host) || Crabzillas.Is(host);
			BeastView.Inside = ((flag && bodyFade < 0.6f) ? host : null);
			_scene.ShowHost = (bodyFade > 0f || flag) && host != null;
			_scene.OwnBody = ((!flag && !VrControls.Active && host != null) ? Settings.ShowBody : 0);
			_scene.OwnWhole = !flag && host != null && PoseShowsBody;
			if (_scene.OwnWhole)
			{
				_scene.OwnBody = Mathf.Max(1, _scene.OwnBody);
			}
			float num = camera.X - eye.X;
			float num2 = camera.Y - eye.Y;
			float range = Mathf.Max(Renderer.BillboardDistance, Mathf.Sqrt(num * num + num2 * num2) + 30f);
			TerrainCache cache = _sampler.Cache;
			bool flag2 = _scene.SkyReach > 0f || (cache != null && cache.Width > 0 && camera.Z - cache.SampleHeight(camera.X, camera.Y) > 8f);
			_scene.WallReach = (flag2 ? Renderer.MaxDistance : 0f);
			_scene.WallLens = (flag2 ? (Mathf.Tan(camera.Fov * 0.5f) / Mathf.Tan(Settings.FieldOfView * 0.5f * (MathF.PI / 180f))) : 1f);
			Renderer.WallModelRange = (SceneCollector.WallsAsModels ? _scene.WallRange : 0f);
			_scene.FarActorRange = (Renderer.ActorClarity ? Renderer.MaxDistance : 0f);
			_scene.CreatureRange = _creatureReach;
			_scene.FarPropRange = Renderer.FogDistance;
			_scene.FarModels = Settings.FarModels == 1 && GpuWanted;
			_scene.CrowdLine = CrowdLineNow(_scene.SkyReach > 0f);
			_scene.CrowdGear = CrowdGearNow();
			_scene.CrowdPixel = CrowdPixelFor(camera);
			_scene.CrowdReach = ((_scene.SkyReach > 0f) ? Renderer.FogDistance : 0f);
			Renderer.Crowd = _scene.Crowd;
			_scene.Collect(camera, range, host, _sampler, _sprites, dt, air);
			_scene.FarPropRange = 0f;
			Renderer.FarProps.Clear();
			Renderer.FarProps.AddRange(_scene.FarProps);
			Renderer.FarModelReach = _scene.FarModelReach;
			Renderer.ScreenFlash = _scene.ScreenFlash;
			Renderer.ActorTagEnd = _scene.UnitTagEnd;
			Renderer.FarActors.Clear();
			Renderer.FarActors.AddRange(_scene.FarActors);
			List<MeshInstance> meshes = _scene.Meshes;
			for (int i = 0; i < meshes.Count; i++)
			{
				if (meshes[i].Tag != -2 && (meshes[i].Tag != 2147483646 || _scene.ShowHost))
				{
					meshes[i].Fade = ((meshes[i].Tag == 2147483646 && !flag) ? bodyFade : 1f);
				}
			}
			Crabzillas.ClearView(meshes);
			Dragons.ClearView(meshes);
			if (_scene.ShowHost && bodyFade < 0.5f)
			{
				_scene.Voxels.RemoveAll((VoxelObject v) => v.Tag == 2147483646);
				_scene.Billboards.RemoveAll((Billboard b) => b.Tag == 2147483646);
			}
			_scene.ShowHost = false;
		}

		private void DiveMapCamera(Vector2 target)
		{
			try
			{
				Camera camera = ((World.world != null) ? World.world.camera : null);
				if (camera == null)
				{
					return;
				}
				float num = DivePath.Ease(_diveT);
				float a = DivePath.Zoom(num, _diveZoom);
				Vector2 vector = Vector2.Lerp(t: DivePath.Ease(num / Mathf.Max(0.15f, DivePath.ClosenessAt(18f, _diveZoom))), a: _diveMapFrom, b: target);
				camera.transform.position = new Vector3(vector.x, vector.y, camera.transform.position.z);
				float a2 = (camera.orthographicSize = Mathf.Max(a, 6.2999997f));
				float num3 = Mathf.Max(a2, 10f);
				MoveCamera move_camera = World.world.move_camera;
				if (move_camera != null)
				{
					move_camera.setTargetZoom(num3);
				}
				World.world.setZoomOrthographic(num3);
			}
			catch (Exception ex)
			{
				Log.Error("map camera: " + ex.Message);
			}
			HideMapView(_diveViewAlpha >= 1f);
		}

		private void UpdateDiveIn()
		{
			DiveMapCamera(_lastPos);
			if (_diveT >= 1f)
			{
				EndDive();
			}
		}

		private void RenderDiveOut()
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			if (World.world == null || !_sampler.EnsureWorld())
			{
				EndDive();
				return;
			}
			_diveT -= unscaledDeltaTime * Mathf.Max(0.05f, Settings.DiveSpeed) / Mathf.Max(0.1f, _diveLength);
			_diveClock += unscaledDeltaTime;
			if (_diveT <= 0f)
			{
				_diveT = 0f;
				DiveMapCamera(_diveFrom);
				EndDive();
				return;
			}
			float closeness = DivePath.Ease(_diveT);
			Actor actor = ((_diveBody != null && _diveBody.isAlive()) ? _diveBody : null);
			TerrainCache cache = _sampler.Cache;
			Vector2 vector = actor?.current_position ?? _diveFrom;
			float num = cache.SampleHeight(vector.x, vector.y);
			ViewState diveEye = _diveEye;
			diveEye.X += vector.x - _diveFrom.x;
			diveEye.Y += vector.y - _diveFrom.y;
			diveEye.Z += num - _diveGround;
			int num2 = Mathf.Clamp((int)vector.x, 0, cache.Width - 1);
			int num3 = Mathf.Clamp((int)vector.y, 0, cache.Height - 1);
			float num4 = (((cache.Flags[num3 * cache.Width + num2] & 1) != 0) ? Mathf.Max(num, 0f) : num);
			_day.Update(Settings, unscaledDeltaTime);
			Atmosphere air = _day.Air;
			Renderer.Air = air;
			Renderer.Windows.Clear();
			Renderer.PaletteLevels = ((Settings.Palette >= 1) ? 18 : 0);
			ApplyShadows();
			FollowLight(air);
			int num5 = RenderWidth(GpuWanted);
			int num6 = Mathf.Max(90, Mathf.RoundToInt((float)(num5 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
			ViewState viewState = DivePath.Camera(diveEye, num4, closeness, _diveZoom, _diveTurn, num5, num6);
			_diveViewAlpha = (_diveFromGod ? 1f : DivePath.ViewAlpha(closeness, DivePath.Zoom(closeness, _diveZoom)));
			_diveGodGround = num4;
			bool flag = _host != null && _host.isAlive();
			_diveHudAlpha = (_diveFromDeath ? Mathf.Clamp01(1f - _diveClock / 0.7f) : (flag ? Mathf.Clamp01(1f - _diveClock / 0.3f) : 0f));
			if (_diveViewAlpha > 0f)
			{
				_sampler.Tick(vector.x, vector.y);
				_sprites.BeginFrame();
				Renderer.Resize(num5, num6);
				ApplyViewDistance();
				Renderer.Time = SeaNow;
				Renderer.RippleStrength = 0f;
				Renderer.ShadowFocusSet = true;
				Renderer.ShadowFocusX = vector.x;
				Renderer.ShadowFocusY = vector.y;
				float body = ((HostImage != null) ? Mathf.Clamp(HostImage.UnitHeight * HostImage.VisibleFraction, 0.3f, 8f) : 1f);
				SteepAround(viewState, num5, num6, num);
				if (_gpu != null)
				{
					_gpu.SteepBehind = Mathf.Max(2f, SceneCollector.AllAround);
				}
				CollectScene(viewState, diveEye, actor, BodyFade(DivePath.Distance(viewState, diveEye), body), air, unscaledDeltaTime);
				ApplyWeather(air, vector, unscaledDeltaTime);
				Renderer.Meshes.Clear();
				Renderer.Meshes.AddRange(_scene.Meshes);
				Renderer.Lights.Clear();
				Renderer.Lights.AddRange(_scene.Lights);
				Renderer.Streaks.AddRange(_scene.Streaks);
				Renderer.OverlayMeshes.Clear();
				ApplyDiveCut(DiveCut(viewState, num5, num6, vector.x, vector.y, num4, closeness));
				if (!GpuPlainFrame(viewState, num5, num6, "the rise"))
				{
					if (num5 > 640)
					{
						num5 = 640;
						num6 = Mathf.Max(90, Mathf.RoundToInt((float)(num5 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
						Renderer.Resize(num5, num6);
					}
					Renderer.Render(cache, viewState, _scene.Billboards, _scene.Voxels, _sampler.MaxHeight + 0.45f);
					UploadFrame(num5, num6);
				}
				Renderer.Cut = SightCut.Off;
			}
			DiveMapCamera(vector);
		}

		internal static bool DoorPointForTest(Building b, HouseWorld.Placement place, out Vector2 door)
		{
			return DoorPoint(b, place, out door);
		}

		private void Doorways(Actor host, Vector2 pos, bool dead)
		{
			_doorwayNear = null;
			WireHomeDoors();
			if (dead || host == null || !HouseInterior.UseFitted || !Models.Ready || Interior.Inside || Cabin.Inside || Mine.Inside || _dive != Dive.None || _leaping || Mounted || BoatHelm.Steers(host) || Crabzillas.Is(host))
			{
				DropDoorRoom();
				return;
			}
			Building building = NearDoor(pos, host);
			if (building != _doorHouse && _doorHouse != null && KeepDoor(_doorHouse, building, pos))
			{
				building = _doorHouse;
			}
			if (building != _doorHouse)
			{
				DropDoorRoom();
				_doorHouse = building;
			}
			if (_doorHouse == null || _doorHouse == _doorFailed)
			{
				return;
			}
			if (DoorRoom.Inside && DoorRoom.Fitted && DoorRoom.House == _doorHouse && Time.unscaledTime - _doorRebuiltAt > 0.5f && Moved(_doorHouse, DoorRoom.Place))
			{
				Log.Info("room: " + HouseName(_doorHouse) + " drawn anew, its room built again");
				_doorRebuiltAt = Time.unscaledTime;
				DoorRoom.Exit();
			}
			if (!DoorRoom.Inside)
			{
				ReadyDoorRoom(host);
			}
			else if (DoorRoom.Fitted)
			{
				if (DoorRoom.DoorwayInWorld(out var middle, out var _, out var _, out var _) && (pos - new Vector2(middle.x, middle.y)).sqrMagnitude < 2.25f)
				{
					_doorwayNear = _doorHouse;
				}
				Vector2 at;
				if (WalkingIn(host, pos))
				{
					StepIn(host, pos);
				}
				else if (InHouse(pos, out at))
				{
					StepIn(host, pos);
					Interior.Eye = at;
				}
			}
		}

		private bool InHouse(Vector2 pos, out Vector2 at)
		{
			at = default(Vector2);
			if (!DoorRoom.Fitted)
			{
				return false;
			}
			at = DoorRoom.ToRoom(pos);
			return DoorRoom.Room.Fit.Clear(0, at.x, at.y, 0.18f);
		}

		private Building NearDoor(Vector2 pos, Actor host)
		{
			Building result = null;
			float num = 16f;
			Vector2 rhs = new Vector2(Mathf.Cos(_view.Yaw), Mathf.Sin(_view.Yaw));
			foreach (KeyValuePair<Building, HouseWorld.Placement> item in HouseWorld.Near())
			{
				Building key = item.Key;
				if (!DoorPoint(key, item.Value, out var door))
				{
					continue;
				}
				float num2 = (door - pos).sqrMagnitude;
				if (!(num2 >= 16f))
				{
					if (num2 > 0.25f && Vector2.Dot((door - pos).normalized, rhs) < 0.3f)
					{
						num2 *= 4f;
					}
					if (!(num2 >= num) && HouseInterior.WalksIn(key.asset) && HouseInterior.IsEnterable(key) && !Rooms.CanEnter(key) && HouseInterior.RoomName(key.asset) != null && HouseInterior.Barred(key, Body ?? host) == null && MayWalkIn(key, host))
					{
						result = key;
						num = num2;
					}
				}
			}
			return result;
		}

		private bool KeepDoor(Building cur, Building other, Vector2 pos)
		{
			try
			{
				if (!cur.isAlive() || !HouseWorld.Of(cur, out var p) || !DoorPoint(cur, p, out var door))
				{
					return false;
				}
				if (!HouseInterior.IsEnterable(cur) || HouseInterior.Barred(cur, Body ?? _host) != null || !MayWalkIn(cur, _host))
				{
					return false;
				}
				float sqrMagnitude = (door - pos).sqrMagnitude;
				float num = 4.75f;
				if (sqrMagnitude >= num * num)
				{
					return false;
				}
				if (other == null || !HouseWorld.Of(other, out var p2) || !DoorPoint(other, p2, out var door2))
				{
					return true;
				}
				return (door2 - pos).sqrMagnitude > sqrMagnitude * 0.5f;
			}
			catch
			{
				return false;
			}
		}

		private static bool DoorPoint(Building b, HouseWorld.Placement place, out Vector2 door)
		{
			if (Steering.DoorOf(b, out door))
			{
				return true;
			}
			HousePlan housePlan = HouseWorld.PlanFor(place);
			if (housePlan == null)
			{
				return false;
			}
			HousePlan.Opening door2 = housePlan.Door;
			float num = (place.Mirror ? (0f - place.Scale) : place.Scale);
			door = new Vector2(place.X + door2.X * num + door2.OutX * (place.Mirror ? (-1f) : 1f) * 0.3f, place.Y + door2.Y * place.Scale + door2.OutY * 0.3f);
			Steering.SetDoor(b, door);
			return true;
		}

		private bool MayWalkIn(Building b, Actor host)
		{
			if (MayWalkInAt != null)
			{
				return MayWalkInAt(b, host);
			}
			return true;
		}

		private void ReadyDoorRoom(Actor host)
		{
			Building doorHouse = _doorHouse;
			if (_doorBuild == null)
			{
				if (HouseWorld.Of(doorHouse, out var place))
				{
					SpriteImage spriteImage;
					try
					{
						spriteImage = _sprites.Get(doorHouse.checkSpriteToRender());
					}
					catch
					{
						spriteImage = null;
					}
					HouseInterior.LookOf(doorHouse, spriteImage, out var wall, out var roof, out var seed, out var race, out var tier, out var style);
					_doorPlace = place;
					_doorSprite = spriteImage;
					_doorBuildFor = doorHouse;
					_ = place;
					_doorBuild = Task.Run(delegate
					{
						HousePlan housePlan = HouseWorld.PlanFor(place);
						return (housePlan != null) ? InteriorBuilder.BuildFitted(housePlan, place.Scale, place.Mirror, wall, roof, seed, race, tier, Models.Get, style) : null;
					});
				}
			}
			else
			{
				if (!_doorBuild.IsCompleted)
				{
					return;
				}
				Interior interior = null;
				if (_doorBuild.IsFaulted)
				{
					Log.Error("room: building " + HouseName(doorHouse) + "'s failed: " + _doorBuild.Exception?.GetBaseException().Message);
				}
				else
				{
					interior = _doorBuild.Result;
				}
				_doorBuild = null;
				if (_doorBuildFor != doorHouse || Moved(doorHouse, _doorPlace))
				{
					return;
				}
				if (interior != null)
				{
					try
					{
						DoorRoom.Enter(doorHouse, _doorSprite, 2f, 2f, host, preview: true, interior, _doorPlace);
						Log.Info($"room: {DoorRoom.Name} ready at its door ({interior.Meshes.Count} models, {interior.Fit.StoreyZ.Length} storeys)");
						return;
					}
					catch (Exception ex)
					{
						Log.Error("room: readying " + HouseName(doorHouse) + "'s: " + ex.Message);
						DoorRoom.Exit();
						_doorFailed = doorHouse;
						return;
					}
				}
				_doorFailed = doorHouse;
				Log.Info("room: no fitted room for " + HouseName(doorHouse) + " (" + HousePlan.Why + "): E at its door");
			}
		}

		private static bool Moved(Building b, HouseWorld.Placement was)
		{
			if (!HouseWorld.Of(b, out var p))
			{
				return false;
			}
			if (p.Model == was.Model && p.Face == was.Face && p.Mirror == was.Mirror && !(Mathf.Abs(p.Scale - was.Scale) > 0.005f) && !(Mathf.Abs(p.X - was.X) > 0.02f) && !(Mathf.Abs(p.Y - was.Y) > 0.02f))
			{
				return Mathf.Abs(p.Ground - was.Ground) > 0.05f;
			}
			return true;
		}

		private void DropDoorRoom()
		{
			if (DoorRoom.Inside)
			{
				DoorRoom.Exit();
			}
			_doorHouse = null;
		}

		private bool WalkingIn(Actor host, Vector2 pos)
		{
			if (!DoorRoom.DoorwayInWorld(out var middle, out var halfWidth, out var _, out var outward))
			{
				return false;
			}
			Vector2 lhs = pos - new Vector2(middle.x, middle.y);
			float num = Vector2.Dot(lhs, outward);
			float f = lhs.x * (0f - outward.y) + lhs.y * outward.x;
			Vector2 keys = (MoveKeysTaken ? Vector2.zero : PossessionHooks.MoveKeys()) + PossessionHooks.ScriptedInput;
			bool flag = keys.y > 0.1f && Vector2.Dot(WalkWay(keys), -outward) > 0.55f;
			bool flag2 = Mathf.Abs(f) <= halfWidth + 0.05f;
			if (!flag2 && DoorRoom.DoorFaceInWorld(out var middle2, out var halfWidth2, out var _, out var outward2))
			{
				Vector2 lhs2 = pos - new Vector2(middle2.x, middle2.y);
				float num2 = Vector2.Dot(lhs2, outward2);
				if (Mathf.Abs(lhs2.x * (0f - outward2.y) + lhs2.y * outward2.x) <= halfWidth2 + 0.05f && num2 < 0.62f && num2 > -0.6f)
				{
					flag2 = true;
					num = Mathf.Min(num, num2);
				}
			}
			if (!flag || num > 0.62f || num < -0.6f || !flag2)
			{
				_doorPress = 0f;
				return false;
			}
			float num3 = HouseLife.OpenOf(_doorHouse);
			if (num3 >= 0f && num3 < 0.5f && _doorwayNear != _doorHouse)
			{
				_doorPress = 0f;
				return false;
			}
			if (num < 0.12f)
			{
				return true;
			}
			if (_doorPress <= 0f || num < _doorPressOff - 0.02f)
			{
				_doorPress = Time.unscaledTime;
				_doorPressOff = num;
				return false;
			}
			return Time.unscaledTime - _doorPress > 0.2f;
		}

		private Vector2 WalkWay(Vector2 keys)
		{
			if (keys == Vector2.zero)
			{
				return Vector2.zero;
			}
			float num = Mathf.Cos(_view.Yaw);
			float num2 = Mathf.Sin(_view.Yaw);
			return new Vector2(num * keys.y + num2 * keys.x, num2 * keys.y - num * keys.x).normalized;
		}

		private void StepIn(Actor host, Vector2 pos)
		{
			Talk.Reset(_scene.LookAt);
			NoteCrossing(_view.Z, stepIn: true);
			Interior.TakeOver(DoorRoom, host);
			Interior.ComeInAt(pos);
			_outsideYaw = _view.Yaw;
			PossessionHooks.Frozen = true;
			_doorHouse = null;
			DoorStepsIn++;
			Log.Info("stepped in at the door of " + Interior.Name + " (" + HouseName(Interior.House) + ")");
			RoyalVisit(Interior.House);
		}

		private void StepOutOfDoor()
		{
			Actor host = _host;
			Building house = Interior.House;
			Vector2 at = Interior.ToWorld(Interior.Eye);
			NoteCrossing(Interior.WorldZ(_view.Z), stepIn: false);
			Talk.Reset(_scene.LookAt);
			DoorRoom.TakeOver(Interior, host, preview: true);
			PossessionHooks.Frozen = false;
			PlaceBody(host, at);
			_doorHouse = house;
			DoorStepsOut++;
			Log.Info("stepped out of the door of " + HouseName(house));
		}

		private static string HouseName(Building b)
		{
			try
			{
				return (b == null) ? "nothing" : (b.asset.id + " " + b.getID() + " at " + b.current_tile.x + "," + b.current_tile.y);
			}
			catch
			{
				return "a building";
			}
		}

		private void PlaceBody(Actor host, Vector2 at)
		{
			try
			{
				if (host != null && host.isAlive())
				{
					host.current_position = at;
					host.dirty_current_tile = true;
					_lastPos = at;
					_eyeZ = 0f;
				}
			}
			catch (Exception ex)
			{
				Log.Error("could not step out: " + ex.Message);
			}
		}

		private static ulong LeafParts(MeshModel model, int leaf)
		{
			ulong num = 0uL;
			MeshPart[] array = model?.Parts;
			if (array == null)
			{
				return (ulong)(1L << leaf);
			}
			for (int i = 0; i < array.Length && i < 64; i++)
			{
				int num2 = i;
				int num3 = 0;
				while (num2 >= 0 && num3 < 64)
				{
					if (num2 == leaf)
					{
						num |= (ulong)(1L << i);
						break;
					}
					num2 = array[num2].Parent;
					num3++;
				}
			}
			return num;
		}

		private bool DoorwayView(ViewState camera, int width, int height, float dt)
		{
			bool vrStereoPlan = _vrStereoPlan;
			_vrDoorReady = false;
			if ((!GpuRoomsWanted && (!vrStereoPlan || !GpuRooms || !GpuRoomsSafe)) || !DoorRoom.Inside || !DoorRoom.Fitted || DoorRoom.House == null)
			{
				return false;
			}
			if (vrStereoPlan)
			{
				GpuMake();
			}
			if (_gpu == null)
			{
				return false;
			}
			float num = HouseLife.OpenOf(DoorRoom.House);
			if (num >= 0f && num < 0.06f)
			{
				return false;
			}
			if (!DoorRoom.DoorFaceInWorld(out var middle, out var halfWidth, out var height2, out var outward))
			{
				return false;
			}
			Vector2 vector = new Vector2(middle.x - camera.X, middle.y - camera.Y);
			if (Vector2.Dot(-vector, outward) < 0.02f || vector.sqrMagnitude > 81f)
			{
				return false;
			}
			if (Mathf.Abs(Mathf.DeltaAngle(camera.Yaw * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f)) > camera.Fov * 57.29578f * 0.5f + 30f && vector.sqrMagnitude > 1f)
			{
				return false;
			}
			_keptMeshes.Clear();
			_keptMeshes.AddRange(Renderer.Meshes);
			_keptOverlay.Clear();
			_keptOverlay.AddRange(Renderer.OverlayMeshes);
			_keptLights.Clear();
			_keptLights.AddRange(Renderer.Lights);
			_keptWindows.Clear();
			_keptWindows.AddRange(Renderer.Windows);
			Atmosphere air = Renderer.Air;
			float maxDistance = Renderer.MaxDistance;
			float billboardDistance = Renderer.BillboardDistance;
			float fadeAt = Renderer.FadeAt;
			float hazeScale = Renderer.HazeScale;
			float rippleStrength = Renderer.RippleStrength;
			bool flag = false;
			try
			{
				ViewState inRoom = DoorRoom.RoomView(camera);
				DoorRoom.Eye = new Vector2(inRoom.X, inRoom.Y);
				DoorRoom.UpdateDoor(dt);
				Interior room = DoorRoom.Room;
				Atmosphere atmosphere = Atmosphere.Indoor(room.CeilingTop, room.CeilingBottom, _day.Air).SunThroughWindows(_day.Air);
				Renderer.Air = atmosphere;
				Renderer.Windows.Clear();
				Renderer.Windows.AddRange(room.Windows);
				Renderer.Lights.Clear();
				for (int i = 0; i < room.Lights.Count; i++)
				{
					RoomLight roomLight = room.Lights[i];
					PointLight light = roomLight.Light;
					if (roomLight.Fire)
					{
						float num2 = RoomFlicker(i, fire: true);
						light.R *= num2;
						light.G *= num2;
						light.B *= num2;
					}
					Renderer.Lights.Add(light);
				}
				room.Daylight(1f - _day.Air.Night);
				Renderer.MaxDistance = 60f;
				Renderer.BillboardDistance = 40f;
				Renderer.FadeAt = 0f;
				Renderer.HazeScale = 1f;
				Renderer.RippleStrength = 0f;
				Renderer.OverlayMeshes.Clear();
				Renderer.Meshes.Clear();
				_doorVoxels.Clear();
				DoorRoom.OutdoorsGathered = true;
				DoorRoom.Collect(_doorVoxels, Renderer.Meshes, _sprites, _scene.LookAt, dt);
				Curtains.AddRoom(Renderer.Meshes, DoorRoom, dt);
				_gpu.Backdrop = null;
				if (vrStereoPlan)
				{
					_vrDoorMeshes.Clear();
					_vrDoorMeshes.AddRange(Renderer.Meshes);
					_vrDoorLights.Clear();
					_vrDoorLights.AddRange(Renderer.Lights);
					_vrDoorWindows.Clear();
					_vrDoorWindows.AddRange(Renderer.Windows);
					_vrDoorAir = atmosphere;
					_vrDoorShift = new Vector3(inRoom.X - camera.X, inRoom.Y - camera.Y, inRoom.Z - camera.Z);
					flag = (_vrDoorReady = true);
				}
				else
				{
					flag = DoorwayRoomFrame(room, inRoom, width, height);
				}
			}
			catch (Exception ex)
			{
				Log.Error("doorway view: " + ex.Message);
				flag = false;
			}
			finally
			{
				Renderer.Meshes.Clear();
				Renderer.Meshes.AddRange(_keptMeshes);
				Renderer.OverlayMeshes.Clear();
				Renderer.OverlayMeshes.AddRange(_keptOverlay);
				Renderer.Lights.Clear();
				Renderer.Lights.AddRange(_keptLights);
				Renderer.Windows.Clear();
				Renderer.Windows.AddRange(_keptWindows);
				Renderer.Air = air;
				Renderer.MaxDistance = maxDistance;
				Renderer.BillboardDistance = billboardDistance;
				Renderer.FadeAt = fadeAt;
				Renderer.HazeScale = hazeScale;
				Renderer.RippleStrength = rippleStrength;
			}
			if (!flag)
			{
				return false;
			}
			if (_doorwayPane == null)
			{
				_doorwayPane = DoorwayPane();
			}
			_doorwayPaneAt.Model = _doorwayPane;
			Vector3 vector2 = middle + new Vector3(outward.x, outward.y, 0f) * 0.012f;
			_doorwayPaneAt.Place(vector2.x, vector2.y, vector2.z, Mathf.Atan2(outward.y, outward.x), halfWidth * 2f, 1f, height2);
			_doorwayPaneAt.SortDepth = vector.magnitude;
			_doorwayPaneAt.Tag = _scene.Tagged.IndexOf(DoorRoom.House);
			Renderer.Meshes.Add(_doorwayPaneAt);
			if (!vrStereoPlan)
			{
				_gpu.Backdrop = _doorwayPicture;
			}
			DoorwayFrames++;
			return true;
		}

		private bool DoorwayRoomFrame(Interior room, ViewState inRoom, int width, int height)
		{
			MeshInstance shell = room.Shell;
			ulong hidden = shell?.Hidden ?? 0;
			if (shell != null && room.DoorLeafPart >= 0 && room.DoorLeafPart < 64)
			{
				shell.Hidden |= LeafParts(shell.Model, room.DoorLeafPart);
			}
			if (shell != null && room.DoorBarPart >= 0 && room.DoorBarPart < 64)
			{
				shell.Hidden |= (ulong)(1L << room.DoorBarPart);
			}
			bool flag;
			try
			{
				flag = GpuRoomFrame(room, inRoom, width, height, _doorVoxels, DoorRoom.Tagged, 1f, DoorwayAside);
			}
			finally
			{
				if (shell != null)
				{
					shell.Hidden = hidden;
				}
			}
			if (flag)
			{
				KeepDoorwayPicture();
			}
			return flag;
		}

		private void DoorwayViewDone()
		{
			if (_gpu != null && _gpu.Backdrop == _doorwayPicture)
			{
				_gpu.Backdrop = null;
			}
		}

		private void KeepDoorwayPicture()
		{
			RenderTexture renderTexture = ((_gpu != null) ? _gpu.Output : null);
			if (renderTexture == null)
			{
				return;
			}
			if (_doorwayPicture == null || _doorwayPicture.width != renderTexture.width || _doorwayPicture.height != renderTexture.height)
			{
				if (_doorwayPicture != null)
				{
					_doorwayPicture.Release();
					UnityEngine.Object.Destroy(_doorwayPicture);
				}
				_doorwayPicture = new RenderTexture(renderTexture.width, renderTexture.height, 0, renderTexture.format)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonDoorway"
				};
				_doorwayPicture.Create();
			}
			if (SystemInfo.copyTextureSupport != CopyTextureSupport.None)
			{
				Graphics.CopyTexture(renderTexture, _doorwayPicture);
			}
			else
			{
				Graphics.Blit(renderTexture, _doorwayPicture);
			}
		}

		private static MeshModel DoorwayPane()
		{
			MeshBuilder meshBuilder = new MeshBuilder("doorway_pane");
			int slot = meshBuilder.Slot("doorway", 0, TerrainCache.Rgb(26, 22, 20), 5);
			meshBuilder.Part("pane", -1, new Vec3(0f, 0f, 0f));
			meshBuilder.Face(slot, new Vec3(-0.5f, 0f, 0f), new Vec3(0.5f, 0f, 0f), new Vec3(0.5f, 0f, 1f), new Vec3(-0.5f, 0f, 1f));
			return meshBuilder.Build();
		}

		internal void FamilyMapKeyForTest()
		{
			if (FamilyMap.Open)
			{
				CloseMapWindow();
			}
			else
			{
				ShowMapPage(MapTabs.Page.Family);
			}
		}

		private void UpdateFamilyMap(Actor host, float dt)
		{
			if (!FamilyMap.Open)
			{
				return;
			}
			if (host == null || Craft.Open || Royal.Open || Royal.ObeliskOpen || WarMap.Open || SoldierMap.Open || _settingsMenu.Open || Converse.Open)
			{
				FamilyMap.Close();
				return;
			}
			try
			{
				FamilyMap.Update(host, dt);
				Minimap.UpdateWorld(Land, HostMap, dt);
			}
			catch (Exception ex)
			{
				Log.Error("family map: " + ex.Message);
			}
		}

		private void DrawFamilyMap()
		{
			if (!(DeathTimer >= 0f) && FamilyMap.Open)
			{
				_hud.DrawFamilyMap();
			}
		}

		private bool FamilyMapEscape()
		{
			if (!FamilyMap.Open)
			{
				return false;
			}
			FamilyMap.Close();
			return true;
		}

		internal Vector3 HeadOf(Actor a)
		{
			return HeadOfWorld(a);
		}

		internal float GroundAt(Vector2 p)
		{
			return _sampler.Cache.SampleHeight(p.x, p.y);
		}

		private void FeatureKeys()
		{
			if (IsTyping() || DeathTimer >= 0f)
			{
				return;
			}
			AbilityKeys();
			if (InventoryPressed())
			{
				WarMap.Close();
			}
			if (InventoryPressed())
			{
				FavoriteTarget = _picked as Actor;
				Favs.Update(0f, force: true);
			}
			if (PlayInput.GetKeyDown(KeyCode.M) && !TalkBusy)
			{
				Craft.Open = false;
				Royal.Open = false;
				Royal.ObeliskOpen = false;
				MapKey();
			}
			if (AnyMenu)
			{
				return;
			}
			if (PlayInput.GetKeyDown(KeyCode.B))
			{
				Actor actor = _picked as Actor;
				if (actor == null || !actor.isAlive())
				{
					actor = _host;
				}
				StarCreature(actor);
			}
			bool keyDown = PlayInput.GetKeyDown(KeyCode.LeftBracket);
			bool keyDown2 = PlayInput.GetKeyDown(KeyCode.RightBracket);
			if (keyDown || keyDown2)
			{
				Favs.Update(0f, force: true);
				Actor actor2 = Favs.Step(_host, keyDown2 ? 1 : (-1));
				if (actor2 == null)
				{
					Toast((Favs.List.Count == 0) ? "No favourites yet. B stars whoever you look at" : "No other favourite to leap to");
				}
				else if (!Possess.JumpTo(actor2))
				{
					Toast(Describe(actor2) + " can't be taken over right now");
				}
			}
		}

		internal bool StarCreature(Actor who)
		{
			if (who == null)
			{
				return false;
			}
			bool flag = Favs.Toggle(who);
			string text = ((who == _host) ? "yourself" : (Dialogue.IsPerson(who) ? PeopleMet.Label(_host, who) : Describe(who)));
			Toast(flag ? ("★ Starred " + text + "   (Tab to see your favourites, [ ] to leap between them)") : ("Unstarred " + text));
			_audio.PlayRustle();
			return flag;
		}

		private void UpdateFeatures(Actor host, Vector2 pos, bool dead, float dt)
		{
			_featClock.Restart();
			RoomCombat.End();
			RoomCombat.Watch(host, _scene.Tagged);
			try
			{
				_paths.Update(_sampler.Cache, pos, dt);
			}
			catch (Exception ex)
			{
				Log.Error("paths: " + ex.Message);
			}
			double totalMilliseconds = _featClock.Elapsed.TotalMilliseconds;
			string text = ((dead || Holstered || host == null) ? null : WeaponOf(host));
			bool flag = !dead && !AnyMenu && !ClickGrace && Work.Target == null && !Earthworks.Busy && !Swimming && !Interior.Inside && _dive != Dive.In && DeathTimer < 0f && !Binoculars.Active && !Bandages.Wrapping && !CompassBlock;
			bool flag2 = _torchBlocks || _hotbarFood != null || Hub.AnyModeActive;
			_bow.Update(host, text, flag && !flag2, PossessionHooks.RunBlend, dt, _audio, _hands);
			_melee.Update(host, text, !BeastView.Active, flag, Settings.HeadBob, dt, Feedback, _audio);
			_guard.Update(host, (text != null) ? ViewMeshes.KindOf(text) : WeaponKind.None, !BeastView.Active, flag && !flag2 && !_wheel && !_pingSwallow, dt, _melee, _bow, Settings.ParryTiming, _audio);
			UpdateWindUps(host, !dead && Settings.EnemyWindups, _scene.Tagged);
			UpdateBlaster(host, text, flag, dt);
			UpdateShotgun(host, text, flag, dt);
			Crabzillas.Control(host, flag, dt);
			UpdateAbility(host, dead, dt);
			WarMap.SetObjective((Royal.Active != null) ? Royal.Active.Objective : null);
			if (host != null)
			{
				WarMap.Update(Body ?? host, _sampler.Cache, dt);
			}
			if (Craft.Open)
			{
				Favs.Update(dt, force: false);
			}
			UpdateMusicEar(host, pos, dead, dt);
			double totalMilliseconds2 = _featClock.Elapsed.TotalMilliseconds;
			_featFrames++;
			_featSum += totalMilliseconds2;
			_featPathsMax = Math.Max(_featPathsMax, totalMilliseconds);
			_featLog += dt;
			if (_featLog >= 10f)
			{
				Log.Info($"features: {_featSum / (double)Math.Max(1, _featFrames):F2} ms a frame (paths at worst {_featPathsMax:F1} ms), {_sampler.Cache.PathTileList.Count} tiles of paths, {_scene.CropCount} crops in sight");
				_featLog = 0f;
				_featFrames = 0;
				_featSum = 0.0;
				_featPathsMax = 0.0;
			}
		}

		private void WeaponsIndoors(Actor host, float dt)
		{
			if (RoomCombat.Active)
			{
				CombatIndoors(host, dt);
				return;
			}
			string weaponId = ((Holstered || host == null) ? null : WeaponOf(host));
			_bow.Update(host, weaponId, canShoot: false, 0f, dt, _audio, _hands);
			_melee.Update(host, weaponId, !BeastView.Active, canStrike: false, Settings.HeadBob, dt, Feedback, _audio);
			_guard.Update(host, WeaponKind.None, !BeastView.Active, canGuard: false, dt, _melee, _bow, Settings.ParryTiming, _audio);
			WindUps.Update(host, null, on: false);
		}

		private void UpdateWindUps(Actor host, bool on, List<BaseSimObject> nearby)
		{
			WindUps.Update(host, nearby, on);
			Actor startedBy = WindUps.StartedBy;
			if (startedBy == null)
			{
				return;
			}
			try
			{
				_audio.PlayWindUp(HeadOfWorld(startedBy), WindUps.StartedKind, WindUps.StartedBeast);
			}
			catch
			{
			}
		}

		internal void GuardBroken(string why)
		{
			Feedback.GuardWord = "GUARD BROKEN";
			Feedback.GuardWordAge = 0f;
			Feedback.GuardWordBroken = true;
			Feedback.Shake = Mathf.Max(Feedback.Shake, 0.6f);
			_audio.PlayGuardBroken();
			Log.Info("guard: broken (" + why + ")");
		}

		private void StopFeatures()
		{
			RoomCombat.End();
			_bow.Stop();
			_melee.Stop();
			_guard.Stop();
			WindUps.Clear();
			_blaster.Stop();
			_shotgun.Stop();
			StopAbility();
			WarMap.Close();
			_danger = (_dangerTarget = 0f);
			_wasDead = false;
		}

		private void UpdateMusicEar(Actor host, Vector2 pos, bool dead, float dt)
		{
			NoteFighting();
			_audio.MusicVolume = Settings.MusicVolume;
			bool flag = _day.Air.Night > 0.55f || _rain > 0.35f;
			_audio.Mood = (flag ? "night" : ((_townHere != null) ? "town" : "day"));
			_dangerTimer -= dt;
			if (_dangerTimer <= 0f)
			{
				_dangerTimer = 0.3f;
				try
				{
					_dangerTarget = ((dead || host == null) ? 0f : MeasureDanger(host, pos));
				}
				catch
				{
					_dangerTarget = 0f;
				}
			}
			_danger = ((_dangerTarget > _danger) ? Mathf.MoveTowards(_danger, _dangerTarget, dt * 2.5f) : Mathf.MoveTowards(_danger, _dangerTarget, dt * 0.055f));
			_audio.Danger = _danger;
			_audio.DangerNow = (dead ? 0f : _dangerTarget);
			if (dead && !_wasDead)
			{
				_audio.MusicDeath();
			}
			_wasDead = dead;
		}

		private float MeasureDanger(Actor host, Vector2 pos)
		{
			float num = 0f;
			if (Feedback.HitFromAge < 6f || _foughtRecently)
			{
				num = 0.95f;
			}
			int num2 = 0;
			int num3 = 0;
			bool flag = false;
			Kingdom kingdom = host.kingdom;
			List<Actor> simpleList = World.world.units.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor == null || actor == host)
				{
					continue;
				}
				float num4 = actor.current_position.x - pos.x;
				float num5 = actor.current_position.y - pos.y;
				float num6 = num4 * num4 + num5 * num5;
				if (!(num6 > 784f) && actor.isAlive())
				{
					if (actor.isFighting())
					{
						num2++;
					}
					if (actor.attack_target == host)
					{
						flag = true;
					}
					if (num6 < 225f && kingdom != null && actor.kingdom != null && actor.kingdom != kingdom && actor.kingdom.isEnemy(kingdom))
					{
						num3++;
					}
					if (actor.asset != null && actor.asset.id == "dragon" && num6 < 676f)
					{
						num = Mathf.Max(num, 0.72f);
					}
				}
			}
			if (flag)
			{
				num = Mathf.Max(num, 1f);
			}
			if (num2 >= 2)
			{
				num = Mathf.Max(num, Mathf.Min(0.85f, 0.32f + (float)num2 * 0.05f));
			}
			if (num3 >= 3)
			{
				num = Mathf.Max(num, 0.3f);
			}
			return num;
		}

		private void NoteFighting()
		{
			int num = Feedback.Hits + Feedback.Kills;
			if (_hitsSeen < 0)
			{
				_hitsSeen = num;
			}
			if (num != _hitsSeen)
			{
				_hitsSeen = num;
				_foughtRecently = true;
			}
			if (Feedback.SinceAttack > 6f && Feedback.HitFromAge > 6f)
			{
				_foughtRecently = false;
			}
		}

		private void DrawFeatures()
		{
			if (!(DeathTimer >= 0f))
			{
				if (Event.current.type == EventType.Repaint && !AnyMenu)
				{
					_hud.DrawWarMarks(Screen.width, Screen.height);
				}
				if (WarMap.Open)
				{
					_hud.DrawWarMap();
				}
				if (Craft.Open)
				{
					_hud.DrawFavoritesBeside();
				}
			}
		}

		private void AddFeatureMarks(List<KeyValuePair<float, Color>> marks, Vector2 p)
		{
			if (WarMap.Marked.Count > 0)
			{
				Vector2 zero = Vector2.zero;
				int num = 0;
				foreach (Actor item in WarMap.Marked)
				{
					try
					{
						if (item != null && item.isAlive())
						{
							zero += item.current_position;
							num++;
						}
					}
					catch
					{
					}
				}
				if (num > 0)
				{
					Vector2 vector = zero / num - p;
					if (vector.sqrMagnitude > 9f)
					{
						marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector.y, vector.x), new Color(1f, 0.84f, 0.3f)));
					}
				}
			}
			if (_host != null && _host.kingdom == WarMap.FlagsKingdom)
			{
				foreach (WarFlag flag2 in WarMap.Flags)
				{
					Vector2 vector2 = flag2.P - p;
					if (vector2.sqrMagnitude > 9f)
					{
						marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector2.y, vector2.x), flag2.Color));
					}
				}
			}
			if (_host == null || _host.kingdom != WarMap.FleetKingdom)
			{
				return;
			}
			for (int i = 0; i < WarMap.ShipOrders.Count; i++)
			{
				ShipOrder shipOrder = WarMap.ShipOrders[i];
				if (shipOrder.What != ShipOrder.Kind.Bombard)
				{
					continue;
				}
				bool flag = true;
				for (int j = 0; j < i && flag; j++)
				{
					if (WarMap.ShipOrders[j].SameTarget(shipOrder))
					{
						flag = false;
					}
				}
				Vector2 vector3 = shipOrder.Mark - p;
				if (flag && vector3.sqrMagnitude > 9f)
				{
					marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector3.y, vector3.x), new Color(1f, 0.32f, 0.22f)));
				}
			}
		}

		internal bool PingFound(Actor a)
		{
			if (_host == null || a == null || a == _host)
			{
				return false;
			}
			try
			{
				if (!a.isAlive())
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			Ping ping = Pings.On(a);
			if (ping != null)
			{
				Pings.Remove(ping);
			}
			Vector2 current_position = a.current_position;
			float z = 0f;
			try
			{
				z = GroundAt(current_position);
			}
			catch
			{
			}
			LastPing = Pings.Add(PingKind.Look, new Vector3(current_position.x, current_position.y, z), a, a, far: false, 600f);
			LastRallied = 0;
			_audio.PlayEffect("ping_look", 0.75f);
			Log.Info($"finding: ping on {Describe(a)} at {current_position.x:F1},{current_position.y:F1} ({(current_position - _host.current_position).magnitude:F0} tiles)");
			return true;
		}

		private void FlameSwordFx(List<VoxelObject> voxels, float dt)
		{
			bool effectsPaused = GameClock.EffectsPaused;
			float unscaledTime = Time.unscaledTime;
			Vec3 eye = (People.FlameEye = new Vec3(_view.X, _view.Y, _view.Z));
			FxParticles fxPool = SceneCollector.FxPool;
			int count = voxels.Count;
			int count2 = _scene.Billboards.Count;
			int count3 = Renderer.Lights.Count;
			float num = 0.7f * Mathf.Clamp01((Renderer.Air.Night - 0.15f) / 0.45f);
			if (_hands.FlameShown)
			{
				_hands.Flame.AddFlames(voxels, eye, unscaledTime, GpuWanted);
				if (!effectsPaused)
				{
					_hands.Flame.Emit(fxPool, dt);
				}
				if (num > 0f)
				{
					_hands.Flame.Light(Renderer.Lights, unscaledTime, num);
				}
			}
			int num2 = 0;
			foreach (FlameSword item in People.FlamesDrawn)
			{
				if (!item.Shown || unscaledTime - item.PosedAt > 0.1f)
				{
					continue;
				}
				float num3 = item.Base.X - _view.X;
				float num4 = item.Base.Y - _view.Y;
				float num5 = num3 * num3 + num4 * num4;
				if (num5 > 1600f)
				{
					continue;
				}
				item.AddFlames(_scene.Billboards, unscaledTime, num5 < 196f);
				if (!(num5 > 784f))
				{
					if (!effectsPaused)
					{
						item.Emit(fxPool, dt, (num5 < 100f) ? 0.6f : 0.3f);
					}
					if (num > 0f && num2++ < 4)
					{
						item.Light(Renderer.Lights, unscaledTime, 0.8f * num);
					}
				}
			}
			FlameSpritesForTest = voxels.Count - count;
			FlameOthersForTest = _scene.Billboards.Count - count2;
			FlameLightsForTest = Renderer.Lights.Count - count3;
		}

		private float StandOn(Actor host, Vector2 pos, float ground, float body, bool skip)
		{
			float num = StandOnCore(host, pos, ground, body, skip);
			PossessionHooks.GroundNow = ground;
			bool flag = _standLift > 0.01f;
			if (!skip && !flag && !Swimming && host != null && !PossessionHooks.Flies(host) && !_lastFeetAir && !float.IsNaN(_lastFeet))
			{
				float num2 = _lastFeet - num;
				float num3 = RoomFloor.StepOf(body);
				if (num2 > num3 + 0.03f && num2 < 4f && (pos - _lastFeetAt).sqrMagnitude < 1f)
				{
					PossessionHooks.StartFall(host, _lastFeet);
					if (PossessionHooks.Leaping)
					{
						float standLift = 0f;
						try
						{
							standLift = host.position_height;
						}
						catch
						{
						}
						_standLift = standLift;
						_takeoffZ = _lastFeet;
						_floorZ = float.NaN;
						_landedOn = false;
						flag = true;
						num = ground;
						FallsForTest++;
					}
				}
			}
			if (!flag)
			{
				_lastFeet = num;
				_lastFeetAt = pos;
				PossessionHooks.FeetZ = num;
			}
			_lastFeetAir = flag || skip || Swimming;
			return num;
		}

		private float StandOnCore(Actor host, Vector2 pos, float ground, float body, bool skip)
		{
			float num = 0f;
			try
			{
				num = host.position_height;
			}
			catch
			{
			}
			_standLift = num;
			bool flag = num > 0.01f;
			if (!flag)
			{
				_landedOn = false;
			}
			if (skip || BuildingFloors.Count == 0 || (flag && PossessionHooks.Flies(host)))
			{
				_floorZ = (_takeoffZ = float.NaN);
				_landedOn = false;
				return ground;
			}
			float num2 = RoomFloor.StepOf(body);
			float num3 = ((flag && !float.IsNaN(_takeoffZ)) ? Mathf.Max(ground, _takeoffZ) : ground);
			if (_landedOn)
			{
				_standLift = 0f;
				if (BuildingFloors.StandOn(pos.x, pos.y, PossessionHooks.BodyRadius, _floorZ + num2, _floorZ, out var z, out _floorWhat) && z > ground + 0.01f)
				{
					_floorZ = z;
					return z;
				}
				_floorZ = float.NaN;
				return ground;
			}
			float num4 = (flag ? (num3 + num) : (float.IsNaN(_floorZ) ? ground : Mathf.Max(ground, _floorZ)));
			if (!BuildingFloors.StandOn(pos.x, pos.y, PossessionHooks.BodyRadius, num4 + num2, flag ? float.NaN : _floorZ, out var z2, out var what) || z2 <= ground + 0.01f)
			{
				_floorZ = float.NaN;
				if (!flag)
				{
					_takeoffZ = float.NaN;
				}
				if (!flag)
				{
					return ground;
				}
				return num3;
			}
			_floorWhat = what;
			if (flag)
			{
				if (z2 < num3 + num)
				{
					_floorZ = float.NaN;
					return num3;
				}
				_landedOn = true;
				_standLift = 0f;
				_floorZ = (_takeoffZ = z2);
				try
				{
					host.stopForce();
				}
				catch
				{
				}
				return z2;
			}
			_floorZ = (_takeoffZ = z2);
			return z2;
		}

		private void ForgeUpdate(Actor host, float dt)
		{
			if (!AnyMenu && (PlayInput.GetMouseButton(0) || PlayInput.GetMouseButton(1)))
			{
				_forgeRestUntil = Time.unscaledTime + 1.2f;
			}
			bool busy = Moving || Swimming || Time.unscaledTime < _forgeRestUntil || BusyForTest;
			Craft.Update(host, dt, Toast, busy);
			if (Craft.Swings != _forgeSwings && Craft.AtBench && !Craft.Open)
			{
				Feedback.WorkSwing(Swing.Mine);
			}
			_forgeSwings = Craft.Swings;
		}

		private void ForgeHands(Actor host, ref HandsInput input)
		{
			_forgeHands = false;
			if (Craft.AtBench && !Craft.Open && !Swimming && !BoatHelm.Steers(host) && !(input.Binoculars > 0f) && !CompassBlock && !(input.Bandage > 0f))
			{
				MeshModel meshModel = Models.Get(BenchHammer(Workbench.Level(host)));
				if (meshModel != null)
				{
					input.HeldModel = meshModel;
					input.HeldKind = WeaponKind.Hammer;
					input.Bow = null;
					input.GunModel = null;
					input.Blaster = null;
					input.Shotgun = null;
					input.Guard = 0f;
					input.Charge = 0f;
					_forgeHands = true;
				}
			}
		}

		private static string BenchHammer(int level)
		{
			switch (level)
			{
			case 0:
			case 1:
				return "held_hammer_stone";
			case 2:
				return "held_hammer_bronze";
			case 3:
				return "held_hammer_iron";
			case 4:
				return "held_hammer_mythril";
			default:
				return "held_hammer_adamantine";
			}
		}

		private bool BenchPrompt(Actor host)
		{
			if (!Interior.Inside || !Interior.HasBench || host == null)
			{
				return false;
			}
			Building building = Workbench.HomeOf(host);
			if (building == null || Interior.House != building)
			{
				return false;
			}
			float num = Interior.BenchX - Interior.Eye.x;
			float num2 = Interior.BenchY - Interior.Eye.y;
			float num3 = num * num + num2 * num2;
			if (num3 > 2.25f)
			{
				return false;
			}
			float num4 = Mathf.Abs(Mathf.DeltaAngle(_view.Yaw * 57.29578f, Mathf.Atan2(num2, num) * 57.29578f));
			if (num3 > 0.36f && num4 > 40f)
			{
				return false;
			}
			if (Chest.Yours(building, host) && Chest.Faced(Interior.Room, Interior.Eye, _view.Yaw, out var d) && (num4 > 40f || d < num3))
			{
				return false;
			}
			Prompt = "E:Workbench";
			if (PlayInput.GetKeyDown(KeyCode.E) && !Craft.Open)
			{
				OpenCrafting();
			}
			return true;
		}

		internal void OpenCrafting()
		{
			Royal.Open = false;
			Royal.ObeliskOpen = false;
			if (!Craft.Open)
			{
				Craft.Toggle();
			}
			Craft.Tab = 0;
			QuickInventory = false;
			_tabHeld = false;
			_audio.PlayRustle();
			Log.Info("crafting: opened at the workbench");
		}

		internal void StartFlyForTest()
		{
			StartFlyFromMap();
		}

		internal void FlyTakeOverForTest()
		{
			FlyTakeOver();
		}

		internal void LiftOutForTest()
		{
			LiftOutToFly();
		}

		internal bool FlyEscapeForTest()
		{
			return FlyEscape();
		}

		internal void LeaveFlyForTest()
		{
			BeginLeaveFly("test");
		}

		internal static bool FlyBlockedForTest(Vector3 p)
		{
			return FlyBlocked(p);
		}

		internal void PlaceFlyForTest(Vector3 p, float yaw, float pitchDegrees)
		{
			_flyPos = KeepFlyOut(p);
			_flyYaw = yaw;
			_flyPitch = pitchDegrees;
			_flyVel = Vector3.zero;
			_flyEase = 1f;
		}

		private void FlyKeys()
		{
			WrapFollowKey();
			if (IsTyping() || VrRunning || World.world == null)
			{
				_fHeld = -1f;
				return;
			}
			if (_active)
			{
				if (AnyMenu)
				{
					_fHeld = -1f;
					return;
				}
				if (PlayInput.GetKeyDown(KeyCode.F))
				{
					_fHeld = 0f;
				}
				if (_fHeld >= 0f && PlayInput.GetKey(KeyCode.F))
				{
					_fHeld += Time.unscaledDeltaTime;
					if (_fHeld >= 0.5f)
					{
						_fHeld = -1f;
						LiftOutToFly();
					}
				}
				else
				{
					_fHeld = -1f;
				}
				return;
			}
			_fHeld = -1f;
			if (_dive != Dive.None || ControllableUnit.isControllingUnit() || _settingsMenu.Open || Help.Open)
			{
				return;
			}
			bool keyDown = PlayInput.GetKeyDown(KeyCode.F);
			if (_fly)
			{
				if ((keyDown || PlayInput.GetKeyDown(KeyCode.V)) && !_flyLeaving)
				{
					BeginLeaveFly("key");
				}
			}
			else if (keyDown)
			{
				StartFlyFromMap();
			}
		}

		private void WrapFollowKey()
		{
			if (_followFailed)
			{
				return;
			}
			try
			{
				HotkeyAsset follow_unit = HotkeyLibrary.follow_unit;
				HotkeyAction hotkeyAction = follow_unit?.just_pressed_action;
				if (hotkeyAction != null && (_follow == null || hotkeyAction != _follow))
				{
					if (_follow == null)
					{
						_follow = FollowPressed;
					}
					_gameFollow = hotkeyAction;
					follow_unit.just_pressed_action = _follow;
				}
			}
			catch (Exception ex)
			{
				Log.Error("free flight: could not take F from the game's follow: " + ex.Message);
				_followFailed = true;
			}
		}

		private void FollowPressed(HotkeyAsset asset)
		{
			if (!_active && !_fly && !_flyLift && (VrRunning || _dive != Dive.None || ControllableUnit.isControllingUnit() || _settingsMenu.Open || Help.Open) && !GameKeysMuted)
			{
				_gameFollow?.Invoke(asset);
			}
		}

		private void StartFlyFromMap()
		{
			Camera camera = ((World.world != null) ? World.world.camera : null);
			if (camera == null || !_sampler.EnsureWorld())
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = SmoothLoader.isLoading();
			}
			catch
			{
			}
			if (!flag)
			{
				bool flag2 = GodShown && _godAlpha >= 1f && _godShown.Fov > 0f;
				ViewState viewState;
				if (flag2)
				{
					viewState = _godShown;
				}
				else
				{
					Vector2 vector = camera.transform.position;
					float zoom = Mathf.Clamp(camera.orthographicSize, 10f, Settings.GodViewZoom);
					float groundZ = GodGroundUnder(_sampler.Cache, vector.x, vector.y, zoom);
					viewState = GodCamera.Camera(vector.x, vector.y, groundZ, zoom, 0f, Screen.width, Screen.height);
				}
				StartFly(viewState, flag2 ? 1f : 0f);
			}
		}

		private void LiftOutToFly()
		{
			if (Diving || DeathTimer >= 0f || _host == null || Scene.On || GameKeysMuted || _settingsMenu.Open)
			{
				return;
			}
			if (Interior.Inside || Cabin.Inside || Mine.Inside)
			{
				Toast("Go outside to fly");
				return;
			}
			_flyLift = true;
			_flyLiftAt = Time.unscaledTime;
			try
			{
				ControllableUnit.clear();
			}
			catch (Exception ex)
			{
				_flyLift = false;
				Log.Error("free flight: letting go: " + ex.Message);
			}
		}

		private bool FlyFromEye(ViewState eye)
		{
			bool num = _flyLift && Time.unscaledTime - _flyLiftAt < 1f;
			_flyLift = false;
			if (!num || World.world == null || !_sampler.EnsureWorld())
			{
				return false;
			}
			eye.Shear = 0f;
			eye.Fov = Settings.FieldOfView * (MathF.PI / 180f);
			StartFly(eye, 1f);
			return true;
		}

		private void StartFly(ViewState from, float alpha)
		{
			_fly = true;
			_flyLeaving = false;
			_flyPos = new Vector3(from.X, from.Y, from.Z);
			_flyYaw = from.Yaw;
			_flyPitch = Mathf.Atan(from.Pitch) * 57.29578f;
			_flyShearFrom = from.Shear;
			_flyFovFrom = ((from.Fov > 0.01f) ? from.Fov : FlyFov);
			_flyEase = ((from.Shear <= 0f && Mathf.Abs(_flyFovFrom - FlyFov) < 0.001f) ? 1f : 0f);
			_flyVel = Vector3.zero;
			_flyCursor = false;
			_flyAlpha = alpha;
			_flyMapSet = false;
			_flyDotsTimer = 0f;
			_flyPos = KeepFlyOut(_flyPos);
			try
			{
				if (MoveCamera.hasFocusUnit() && MoveCamera.instance != null)
				{
					MoveCamera.instance.clearFocusUnitAndUnselect();
				}
			}
			catch (Exception ex)
			{
				Log.Error("free flight: letting go of the game's follow: " + ex.Message);
			}
			ResetGodZoom();
			GodView.Flying = true;
			_flyHintUntil = Time.unscaledTime + 6f;
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
			Log.Info($"free flight on at ({_flyPos.x:F1}, {_flyPos.y:F1}, {_flyPos.z:F1})");
		}

		private void EndFly()
		{
			if (_fly)
			{
				_fly = false;
				_flyLeaving = false;
				GodView.Flying = false;
				SaveFlySpeed();
				if (!_active)
				{
					Cursor.lockState = CursorLockMode.None;
					Cursor.visible = true;
				}
				Log.Info("free flight off");
			}
		}

		private void SaveFlySpeed()
		{
			if (_flyDirty)
			{
				_flyDirty = false;
				Settings.Save();
			}
		}

		private bool FlyEscape()
		{
			if (!_fly)
			{
				return false;
			}
			bool flag = false;
			try
			{
				flag = ScrollWindow.isWindowActive();
			}
			catch
			{
			}
			if (flag)
			{
				return false;
			}
			if (!_flyLeaving)
			{
				BeginLeaveFly("Esc");
			}
			return true;
		}

		private ViewState FlyCamera()
		{
			if (_flyLeaving)
			{
				return BlendFlyCamera(_flyLeaveFrom, _flyLeaveTo, DivePath.Ease(_flyLeaveClock / 0.5f));
			}
			float num = DivePath.Ease(_flyEase);
			return new ViewState
			{
				X = _flyPos.x,
				Y = _flyPos.y,
				Z = _flyPos.z,
				Yaw = _flyYaw,
				Pitch = Mathf.Tan(Mathf.Clamp(_flyPitch, -89f, 89f) * (MathF.PI / 180f)),
				Fov = Mathf.Lerp(_flyFovFrom, FlyFov, num),
				Shear = _flyShearFrom * (1f - num),
				Free = (GpuWanted && !VrRunning)
			};
		}

		private static ViewState BlendFlyCamera(ViewState a, ViewState b, float t)
		{
			float a2 = Mathf.Atan(a.Pitch);
			float b2 = Mathf.Atan(b.Pitch);
			return new ViewState
			{
				X = Mathf.Lerp(a.X, b.X, t),
				Y = Mathf.Lerp(a.Y, b.Y, t),
				Z = Mathf.Lerp(a.Z, b.Z, t),
				Yaw = a.Yaw + DivePath.Wrap(b.Yaw - a.Yaw) * t,
				Pitch = Mathf.Tan(Mathf.Lerp(a2, b2, t)),
				Fov = Mathf.Lerp(a.Fov, b.Fov, t),
				Shear = Mathf.Lerp(a.Shear, b.Shear, t),
				Free = (a.Free && t < 0.5f)
			};
		}

		private void UpdateFly(Camera cam, float dt)
		{
			MuteGameKeys();
			GodView.Flying = true;
			GodView.ArrowsTurn = false;
			bool flag = false;
			try
			{
				flag = ScrollWindow.isWindowActive();
			}
			catch
			{
			}
			bool flag2 = _settingsMenu.Open || flag || WorldNews.Open || Run.WindowOpen || Help.Open;
			bool flag3 = IsTyping();
			if (!flag2 && !flag3 && PlayInput.GetKeyDown(KeyCode.Tab))
			{
				_flyCursor = !_flyCursor;
			}
			bool flag4 = _flyCursor || flag2;
			CursorLockMode cursorLockMode = ((!flag4) ? CursorLockMode.Locked : CursorLockMode.None);
			if (Cursor.lockState != cursorLockMode)
			{
				Cursor.lockState = cursorLockMode;
			}
			Cursor.visible = flag4;
			_flyEase = Mathf.MoveTowards(_flyEase, 1f, dt / 0.4f);
			TerrainCache cache = _sampler.Cache;
			if (_flyLeaving)
			{
				_flyLeaveClock += dt;
				HoldFlyMap(cam, _flyLeaveAt, _flyLeaveZoom, 1f);
				if (_flyLeaveClock >= 0.5f)
				{
					FinishLeaveFly(cam);
				}
				return;
			}
			if (!flag4)
			{
				float num = 0f;
				float num2 = 0f;
				try
				{
					num = Input.GetAxisRaw("Mouse X");
					num2 = Input.GetAxisRaw("Mouse Y");
				}
				catch (ArgumentException)
				{
				}
				if (DebugEyeSet || MouseLookOff)
				{
					num = (num2 = 0f);
				}
				Vector2 vector = PadControls.TakeLook();
				num += vector.x / Settings.MouseSensitivity;
				num2 += vector.y / Settings.MouseSensitivity;
				float num3 = Mathf.Tan(FlyCamera().Fov * 0.5f) / Mathf.Tan(FlyFov * 0.5f);
				_flyYaw -= num * Settings.MouseSensitivity * num3 * (MathF.PI / 180f);
				_flyPitch += num2 * Settings.MouseSensitivity * num3;
			}
			_flyYaw = DivePath.Wrap(_flyYaw + FlyLookForTest.x * (MathF.PI / 180f));
			_flyPitch += FlyLookForTest.y;
			FlyLookForTest = Vector2.zero;
			float flyLookLimit = FlyLookLimit;
			_flyPitch = Mathf.Clamp(_flyPitch, 0f - flyLookLimit, flyLookLimit);
			bool flag5 = false;
			try
			{
				flag5 = flag4 && World.world.isOverUI();
			}
			catch
			{
			}
			float num4 = ((flag2 || flag3 || flag5) ? 0f : Input.mouseScrollDelta.y);
			if (num4 != 0f)
			{
				Settings.FlySpeed = Mathf.Clamp(Settings.FlySpeed * Mathf.Pow(1.25f, num4), 0.1f, 20f);
				_flyDirty = true;
				_flySpeedUntil = Time.unscaledTime + 1.5f;
			}
			Vector3 vector2 = Vector3.zero;
			if (FlyKeysForTest.HasValue)
			{
				vector2 = FlyKeysForTest.Value;
			}
			else if (!flag2 && !flag3)
			{
				if (PlayInput.GetKey(KeyCode.W) || PlayInput.GetKey(KeyCode.UpArrow))
				{
					vector2.x += 1f;
				}
				if (PlayInput.GetKey(KeyCode.S) || PlayInput.GetKey(KeyCode.DownArrow))
				{
					vector2.x -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.D) || PlayInput.GetKey(KeyCode.RightArrow))
				{
					vector2.y += 1f;
				}
				if (PlayInput.GetKey(KeyCode.A) || PlayInput.GetKey(KeyCode.LeftArrow))
				{
					vector2.y -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.Space))
				{
					vector2.z += 1f;
				}
				if (PlayInput.GetKey(KeyCode.LeftControl) || PlayInput.GetKey(KeyCode.RightControl) || PlayInput.GetKey(KeyCode.C))
				{
					vector2.z -= 1f;
				}
			}
			float num5 = Mathf.Cos(_flyPitch * (MathF.PI / 180f));
			float z = Mathf.Sin(_flyPitch * (MathF.PI / 180f));
			float num6 = Mathf.Cos(_flyYaw);
			float num7 = Mathf.Sin(_flyYaw);
			Vector3 vector3 = new Vector3(num6 * num5, num7 * num5, z);
			Vector3 vector4 = new Vector3(num7, 0f - num6, 0f);
			Vector3 vector5 = vector3 * vector2.x + vector4 * vector2.y + new Vector3(0f, 0f, vector2.z);
			if (vector5.sqrMagnitude > 1f)
			{
				vector5.Normalize();
			}
			bool flag6 = !flag2 && !flag3 && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
			vector5 *= 10f * Settings.FlySpeed * (flag6 ? 3f : 1f);
			_flyVel = Vector3.Lerp(_flyVel, vector5, 1f - Mathf.Exp(-9f * dt));
			if (vector5 == Vector3.zero && _flyVel.sqrMagnitude < 0.0001f)
			{
				_flyVel = Vector3.zero;
			}
			Vector3 flyPos = MoveFly(_flyPos, _flyVel * dt);
			float num8 = FlyGround(cache, flyPos.x, flyPos.y);
			float num9 = Settings.GodViewZoom * FlyZoomHeight;
			if (flyPos.z - num8 > num9 && _flyVel.z > 0.5f)
			{
				_flyPos = flyPos;
				BeginLeaveFly("up past the god view");
				return;
			}
			flyPos.z = Mathf.Min(flyPos.z, num8 + num9 * 1.05f);
			_flyPos = flyPos;
			ViewState camera = FlyCamera();
			Vector2 vector6 = (flag4 ? ((Vector2)Input.mousePosition) : new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f));
			if (GodView.TestMouse.HasValue)
			{
				vector6 = GodView.TestMouse.Value;
			}
			GodView.FlyFrom = new Vec3(camera.X, camera.Y, camera.Z);
			GodView.FlyDir = GodCamera.LensRay(camera, Screen.width, Screen.height, vector6.x, (float)Screen.height - vector6.y);
			float num10 = Mathf.Max(1f, _flyPos.z - num8);
			Vector2 at = new Vector2(_flyPos.x + num6 * Mathf.Min(num10, 60f), _flyPos.y + num7 * Mathf.Min(num10, 60f));
			if (GodView.MouseValid && !flag4 && (GodView.LandPoint - (Vector2)_flyPos).sqrMagnitude < (num10 * 3f + 20f) * (num10 * 3f + 20f))
			{
				at = GodView.LandPoint;
			}
			HoldFlyMap(cam, at, Mathf.Clamp(num10 / FlyZoomHeight, 10f, Settings.GodViewZoom), 1f - Mathf.Exp(-6f * dt));
			if (!flag2 && !flag3 && !flag5 && TakeOverPressed())
			{
				FlyTakeOver();
			}
			FlyMinimap(dt);
		}

		private void HoldFlyMap(Camera cam, Vector2 at, float zoom, float ease)
		{
			if (!(cam == null))
			{
				if (!_flyMapSet)
				{
					_flyMapAt = cam.transform.position;
					_flyMapSet = true;
				}
				_flyMapAt = Vector2.Lerp(_flyMapAt, at, ease);
				if (World.world != null)
				{
					_flyMapAt = new Vector2(Mathf.Clamp(_flyMapAt.x, 0f, MapBox.width), Mathf.Clamp(_flyMapAt.y, 0f, MapBox.height));
				}
				cam.transform.position = new Vector3(_flyMapAt.x, _flyMapAt.y, cam.transform.position.z);
				HoldMapZoom(cam, zoom);
			}
		}

		private static float FlyGround(TerrainCache cache, float x, float y)
		{
			float num = Mathf.Max(cache.SampleHeight(x, y), 0f);
			for (int i = 0; i < 4; i++)
			{
				float f = (float)i * MathF.PI * 0.5f;
				num = Mathf.Max(num, cache.SampleHeight(x + Mathf.Cos(f) * 0.25f, y + Mathf.Sin(f) * 0.25f));
			}
			return num;
		}

		private Vector3 KeepFlyOut(Vector3 p)
		{
			TerrainCache cache = _sampler.Cache;
			if (cache == null || cache.Width <= 0)
			{
				return p;
			}
			float num = Settings.GodViewZoom * FlyZoomHeight + 20f;
			p.x = Mathf.Clamp(p.x, 0f - num, (float)cache.Width + num);
			p.y = Mathf.Clamp(p.y, 0f - num, (float)cache.Height + num);
			p.z = Mathf.Max(p.z, FlyGround(cache, p.x, p.y) + 0.35f);
			return p;
		}

		private static bool FlyBlocked(Vector3 p)
		{
			return BuildingFloors.Blocked(p.x, p.y, 0.25f, p.z - 0.25f, p.z + 0.25f);
		}

		private Vector3 MoveFly(Vector3 p, Vector3 move)
		{
			float magnitude = move.magnitude;
			if (magnitude < 1E-06f)
			{
				return KeepFlyOut(p);
			}
			int num = Mathf.Clamp(Mathf.CeilToInt(magnitude / 0.12f), 1, 400);
			Vector3 vector = move / num;
			bool flag = FlyBlocked(p);
			for (int i = 0; i < num; i++)
			{
				Vector3 vector2 = KeepFlyOut(p + vector);
				if (flag || !FlyBlocked(vector2))
				{
					p = vector2;
					continue;
				}
				bool flag2 = false;
				for (int j = 0; j < 3; j++)
				{
					Vector3 zero = Vector3.zero;
					zero[j] = vector[j];
					if (!(Mathf.Abs(zero[j]) < 1E-07f))
					{
						Vector3 vector3 = KeepFlyOut(p + zero);
						if (!FlyBlocked(vector3))
						{
							p = vector3;
							flag2 = true;
						}
					}
				}
				if (!flag2)
				{
					break;
				}
			}
			return p;
		}

		private void BeginLeaveFly(string why)
		{
			TerrainCache cache = _sampler.Cache;
			if (cache == null || cache.Width <= 0)
			{
				EndFly();
				return;
			}
			float flyZoomHeight = FlyZoomHeight;
			float num = FlyGround(cache, _flyPos.x, _flyPos.y);
			float num2 = Mathf.Clamp((_flyPos.z - num) / flyZoomHeight, 10f, Settings.GodViewZoom);
			float num3 = num2 * flyZoomHeight;
			Vector2 flyLeaveAt = new Vector2(_flyPos.x + Mathf.Cos(_flyYaw) * num3, _flyPos.y + Mathf.Sin(_flyYaw) * num3);
			flyLeaveAt.x = Mathf.Clamp(flyLeaveAt.x, 0f, cache.Width);
			flyLeaveAt.y = Mathf.Clamp(flyLeaveAt.y, 0f, cache.Height);
			float num4 = DivePath.Wrap(_flyYaw - MathF.PI / 2f);
			float num5 = GodGroundUnder(cache, flyLeaveAt.x, flyLeaveAt.y, num2);
			int num6 = RenderWidth(GpuWanted);
			int height = Mathf.Max(90, Mathf.RoundToInt((float)(num6 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
			_flyLeaveFrom = FlyCamera();
			_flyLeaveTo = GodCamera.Camera(flyLeaveAt.x, flyLeaveAt.y, num5, num2, num4, num6, height);
			_flyLeaveTo.Free = _flyLeaveFrom.Free;
			_flyLeaveAt = flyLeaveAt;
			_flyLeaveZoom = num2;
			_flyLeaveGround = num5;
			_flyLeaveTurn = num4;
			_flyLeaveClock = 0f;
			_flyLeaving = true;
			_flyVel = Vector3.zero;
			Log.Info($"free flight: back into the god view ({why}), zoom {num2:F0}");
		}

		private void FinishLeaveFly(Camera cam)
		{
			EndFly();
			_godTurn = _flyLeaveTurn;
			_godTurnRate = (_godTip = (_godTipRate = 0f));
			_godHoming = false;
			if (!Settings.GodView)
			{
				Settings.GodView = true;
				Settings.Save();
			}
			_godFade = 1f;
			_godWasOn = true;
			_godSwitched = false;
			_godHeld = 1f;
			_godGround = _flyLeaveGround;
			if (cam != null)
			{
				cam.transform.position = new Vector3(_flyLeaveAt.x, _flyLeaveAt.y, cam.transform.position.z);
				HoldMapZoom(cam, _flyLeaveZoom);
			}
		}

		private void FlyTakeOver()
		{
			Actor actor = (GodView.MouseValid ? GodView.MouseCreature : null);
			if (actor == null && GodView.MouseValid)
			{
				actor = NearestCreature(GodView.LandPoint, 1.2f);
			}
			if (actor == null)
			{
				GodToast("Nobody there to take over");
				return;
			}
			if (!Possess.CanJumpTo(actor))
			{
				GodToast("You can't take over " + Describe(actor));
				return;
			}
			Log.Info("free flight: taking over " + Describe(actor));
			if (!Possess.Swap(actor))
			{
				GodToast("You can't take over " + Describe(actor));
			}
		}

		private void FlyIntoDive()
		{
			_diveFromFly = true;
			Actor actor = (ControllableUnit.isControllingUnit() ? ControllableUnit._unit_main : null);
			float num = 30f;
			if (actor != null)
			{
				Vector2 current_position = actor.current_position;
				float num2 = Mathf.Max(_sampler.Cache.SampleHeight(current_position.x, current_position.y), 0f) + 0.9f;
				num = new Vector3(_flyPos.x - current_position.x, _flyPos.y - current_position.y, _flyPos.z - num2).magnitude;
			}
			float num3 = num / (Mathf.Sqrt(2f) * FlyZoomHeight);
			_godTurn = DivePath.Wrap(_flyYaw - MathF.PI / 2f);
			_godTip = (_godTipRate = (_godTurnRate = 0f));
			_view.Yaw = _flyYaw;
			_pitchDegrees = 0f;
			if (num3 >= 10f)
			{
				_diveZoom = Mathf.Min(num3, Mathf.Max(Settings.GodViewZoom, 10f));
				_diveT = 0f;
			}
			else
			{
				_diveZoom = 10f;
				_diveT = DivePath.InverseEase(DivePath.ClosenessAt(Mathf.Max(0.05f, num3), 10f));
			}
			EndFly();
		}

		private void FlyMinimap(float dt)
		{
			if (!Settings.Minimap || Land == null)
			{
				return;
			}
			Vector2 vector = _flyPos;
			try
			{
				Minimap.Update(Land, null, vector, FlyMinimapReach, dt);
			}
			catch (Exception ex)
			{
				Log.Error("free flight: minimap: " + ex.Message);
			}
			_flyDotsTimer -= dt;
			if (_flyDotsTimer > 0f)
			{
				return;
			}
			_flyDotsTimer = 0.25f;
			FlyDots.Clear();
			try
			{
				float flyMinimapReach = FlyMinimapReach;
				float num = flyMinimapReach * flyMinimapReach;
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					if (FlyDots.Count >= 60)
					{
						break;
					}
					Actor actor = simpleList[i];
					if (actor != null && actor.isAlive() && !actor.is_inside_building)
					{
						Vector2 current_position = actor.current_position;
						if (!((current_position - vector).sqrMagnitude > num))
						{
							Color value = ((actor.kingdom != null && actor.kingdom.isCiv()) ? new Color(0.86f, 0.86f, 0.8f) : new Color(1f, 0.55f, 0.35f));
							FlyDots.Add(new KeyValuePair<Vector2, Color>(current_position, value));
						}
					}
				}
			}
			catch (Exception ex2)
			{
				Log.Error("free flight: minimap creatures: " + ex2.Message);
			}
		}

		private void DrawFlyGui()
		{
			if (!_fly || !GodShown)
			{
				return;
			}
			string text = null;
			float health = -1f;
			if (!_flyLeaving && GodView.MouseValid)
			{
				Actor mouseCreature = GodView.MouseCreature;
				if (mouseCreature != null && mouseCreature.isAlive())
				{
					text = Describe(mouseCreature);
					try
					{
						health = (float)mouseCreature.getHealth() / (float)Mathf.Max(1, mouseCreature.getMaxHealth());
					}
					catch
					{
					}
				}
				else
				{
					try
					{
						Building building = World.world.GetTile(GodView.MouseTile.x, GodView.MouseTile.y)?.building;
						if (building != null && building.asset != null && AssetStyle.IsSolid(building) && !building.isRuin())
						{
							text = building.asset.id.Replace('_', ' ');
							if (building.city != null)
							{
								text = text + ", " + building.city.name;
							}
						}
					}
					catch
					{
					}
				}
			}
			bool crosshair = !_flyCursor && !_flyLeaving && Cursor.lockState == CursorLockMode.Locked;
			_hud.DrawFly(crosshair, text, health, _flyPos, _flyYaw, Settings.FlySpeed, _flySpeedUntil, _flyHintUntil, _flyCursor);
		}

		private void FinishGodGpu(double waited)
		{
			int godTaskWidth = _godTaskWidth;
			int godTaskHeight = _godTaskHeight;
			long timestamp = Stopwatch.GetTimestamp();
			long cyclesNow = Hitch.CyclesNow;
			try
			{
				FinishGodGpuFrame(waited, godTaskWidth, godTaskHeight, timestamp);
			}
			finally
			{
				GodCardCycles += Hitch.CyclesNow - cyclesNow;
			}
		}

		private void FinishGodGpuFrame(double waited, int width, int height, long started)
		{
			if (_godTaskVr && VrGodEyes())
			{
				GodGpuPerf(waited, (double)(Stopwatch.GetTimestamp() - started) * 1000.0 / (double)Stopwatch.Frequency);
				return;
			}
			GpuMake();
			_gpu.ShadowReach = Mathf.Clamp(_godTaskZoom * 2.2f, GpuRenderer.EyeShadowReach, 200f);
			_gpu.ReadDepth = true;
			_gpu.SteepBehind = _godTaskBehind;
			bool flag;
			try
			{
				flag = GpuPlainFrame(_godTaskCamera, width, height, "the god view");
			}
			finally
			{
				_gpu.ReadDepth = false;
				_gpu.ShadowReach = GpuRenderer.EyeShadowReach;
			}
			if (!flag)
			{
				return;
			}
			_godShown = _godTaskCamera;
			GpuRenderer.DepthSizeFor(width, height, out var w, out var h);
			_godGpuFrames[_gpu.FrameNumber & 7] = new GodGpuFrame
			{
				Number = _gpu.FrameNumber,
				Camera = _godTaskCamera,
				Width = w,
				Height = h,
				Ground = _godTaskGround,
				At = _godTaskAt,
				Cut = _godCut
			};
			if (SystemInfo.supportsAsyncGPUReadback)
			{
				ApplyGodGpuDepth();
			}
			else
			{
				if (_godDepth.Length != 0)
				{
					_godDepth = new float[0];
				}
				SetGodMouseFrame(_godTaskCamera, width, height, _godTaskGround, _godTaskAt, _godCut);
			}
			GodGpuPerf(waited, (double)(Stopwatch.GetTimestamp() - started) * 1000.0 / (double)Stopwatch.Frequency);
		}

		private bool GpuPlainFrame(ViewState camera, int width, int height, string what)
		{
			if (!GpuWanted)
			{
				return false;
			}
			GpuMake();
			_gpuTagged[(_gpu.FrameNumber + 1) & 0xF].Clear();
			_gpu.PixelFar = false;
			PresentGpu();
			if (!_gpu.Available || !_gpu.Render(Renderer, _sampler.Cache, camera, width, height, _scene.Billboards, _scene.Voxels, Settings.GpuLook == 0))
			{
				string text = (_gpu.Available ? _gpu.Error : (GpuShaders.Error ?? GpuSupport.Missing ?? "compute buffers not supported"));
				if (text != _gpuLastError)
				{
					Log.Error("gpu: " + what + " drawn on the CPU for now: " + text);
					_gpuLastError = text;
				}
				GpuRetryLater();
				return false;
			}
			_gpuShown = true;
			_gpuFails = 0;
			GpuProven(room: false);
			FramesShown++;
			_shown = _gpu.Projection;
			_eyeNow = _shown.View;
			return true;
		}

		private void ApplyGodGpuDepth()
		{
			GpuRenderer gpu = _gpu;
			if (gpu == null || gpu.DepthFrame < 0 || gpu.DepthFrame == _godGpuApplied)
			{
				return;
			}
			GodGpuFrame godGpuFrame = _godGpuFrames[gpu.DepthFrame & 7];
			if (godGpuFrame.Number == gpu.DepthFrame && godGpuFrame.Width == gpu.DepthWidth && godGpuFrame.Height == gpu.DepthHeight)
			{
				_godGpuApplied = gpu.DepthFrame;
				int num = godGpuFrame.Width * godGpuFrame.Height;
				if (_godDepth.Length != num)
				{
					_godDepth = new float[num];
				}
				Array.Copy(gpu.DepthPixels, _godDepth, num);
				SetGodMouseFrame(godGpuFrame.Camera, godGpuFrame.Width, godGpuFrame.Height, godGpuFrame.Ground, godGpuFrame.At, godGpuFrame.Cut);
			}
		}

		private void SetGodMouseFrame(ViewState camera, int width, int height, float ground, Vector2 at, SightCut cut)
		{
			GodView.View = camera;
			GodView.Width = width;
			GodView.Height = height;
			GodView.Depth = _godDepth;
			GodView.Terrain = _sampler.Cache;
			GodView.Cut = cut;
			GodView.FlatZ = ground;
			GodView.ViewAt = at;
		}

		private void GodGpuPerf(double waited, double submit)
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			_gpFrame += (double)unscaledDeltaTime * 1000.0;
			_gpWait += waited;
			_gpSubmit += submit;
			_gpLandBeside += _godTaskLandMs;
			_gpFrames++;
			_gpSince += unscaledDeltaTime;
			if (!(_gpSince < 10f))
			{
				double num = 1.0 / (double)_gpFrames;
				double num2 = (_gpLand + _gpScene + _gpWait + _gpSubmit) * num;
				float num3 = ((World.world != null && World.world.camera != null) ? World.world.camera.orthographicSize : 0f);
				Log.Info($"god view perf (gpu): {1000.0 / (_gpFrame * num):F0} fps (frame {_gpFrame * num:F1} ms); on the game's thread {num2:F1} ms: landforms {_gpLand * num:F1}, " + $"scene {_gpScene * num:F1}, waiting for the land {_gpWait * num:F1}, drawing on the card {_gpSubmit * num:F1} ({_gpu.TakeTimes()}); beside the game: land {_gpLandBeside * num:F1} ms; " + $"{_gpu.Stats}, zoom {num3:F0}, {(double)SceneCollector.GodFrameSkipped * num:F0} off the frame");
				GodPerfReset();
			}
		}

		private void GodPerfReset()
		{
			_gpFrame = (_gpLand = (_gpLandBeside = (_gpScene = (_gpWait = (_gpUpload = (_gpDraw = (_gpTerrain = (_gpObjects = (_gpModels = (_gpPost = (_gpTris = (_gpSubmit = 0.0))))))))))));
			_gpFrames = 0;
			_gpSince = 0f;
			SceneCollector.GodFrameSkipped = 0L;
		}

		internal void GodPerfResetForTest()
		{
			GodPerfReset();
			_gpu?.TakeTimes();
			GodSceneCycles = (GodCardCycles = 0L);
		}

		internal string GodPerfReadForTest()
		{
			if (_gpFrames == 0 || _gpu == null)
			{
				return "no frames on the card";
			}
			double num = 1.0 / (double)_gpFrames;
			return $"frame {_gpFrame * num:F1} ms, game thread {(_gpLand + _gpScene + _gpWait + _gpSubmit) * num:F1} (scene {_gpScene * num:F1}, waiting {_gpWait * num:F1}, card {_gpSubmit * num:F1} [{_gpu.TakeTimes()}]), " + $"land beside {_gpLandBeside * num:F1}; {_gpu.Stats}; {(double)SceneCollector.GodFrameSkipped * num:F0} off the frame";
		}

		internal Color32[] GodPixelsForTest(out int width, out int height)
		{
			width = (height = 0);
			if (!_gpuShown || _gpu == null || _gpu.Output == null)
			{
				return null;
			}
			Texture2D texture2D = _gpu.ReadOutput();
			if (texture2D == null)
			{
				return null;
			}
			width = texture2D.width;
			height = texture2D.height;
			Color32[] pixels = texture2D.GetPixels32();
			UnityEngine.Object.Destroy(texture2D);
			return pixels;
		}

		private float GodShowing(float cover, float dt)
		{
			bool godView = Settings.GodView;
			if (godView != _godWasOn && !_godSwitched)
			{
				_godFade = (godView ? 1f : 0f);
				if (!godView)
				{
					GodTurnReset();
				}
			}
			_godWasOn = godView;
			_godSwitched = false;
			bool flag = !godView || cover < 1f;
			if (flag && _godAlpha <= 0f)
			{
				GodTurnReset();
			}
			UpdateGodTurn(dt, !flag && _godFade >= 1f, flag);
			bool godTurnHome = GodTurnHome;
			_godFade = Mathf.MoveTowards(_godFade, godView ? 1f : (godTurnHome ? 0f : _godFade), dt / 0.3f);
			if (!godTurnHome)
			{
				_godHeld = Mathf.Max(_godHeld, cover);
			}
			else
			{
				_godHeld = ((_godHeld > cover) ? Mathf.MoveTowards(_godHeld, cover, dt / 0.3f) : cover);
			}
			GodView.Turn = _godTurn;
			return Mathf.Min(_godHeld, _godFade);
		}

		private void GodTurnIdle(bool keepTurn)
		{
			_godFade = (Settings.GodView ? 1f : 0f);
			_godWasOn = Settings.GodView;
			_godSwitched = false;
			_godHeld = 0f;
			GodView.ArrowsTurn = false;
			GodView.Turn = 0f;
			if (!keepTurn || !Settings.GodView)
			{
				GodTurnReset();
			}
		}

		private void GodTurnReset()
		{
			_godTurn = (_godTurnRate = (_godTip = (_godTipRate = 0f)));
			_godHoming = false;
		}

		private void UpdateGodTurn(float dt, bool turnable, bool flat)
		{
			bool num = (GodView.ArrowsTurn = turnable && GodView.KeysHooked && GodTurnKeysFree());
			float num2 = 0f;
			float num3 = 0f;
			if (num)
			{
				if (PlayInput.GetKey(KeyCode.LeftArrow))
				{
					num2 += 1f;
				}
				if (PlayInput.GetKey(KeyCode.RightArrow))
				{
					num2 -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.UpArrow))
				{
					num3 += 1f;
				}
				if (PlayInput.GetKey(KeyCode.DownArrow))
				{
					num3 -= 1f;
				}
				if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
				{
					num2 *= 2f;
				}
			}
			if (turnable && GodView.TestTurn.HasValue)
			{
				num2 = GodView.TestTurn.Value.x;
				num3 = GodView.TestTurn.Value.y;
			}
			if (_godHoming && turnable && (num2 != 0f || num3 != 0f))
			{
				_godHoming = false;
			}
			if (flat && VrRunning && !GodTurnHome)
			{
				GodTurnReset();
			}
			if (flat && !_godHoming && !GodTurnHome)
			{
				StartGodHome();
			}
			if (_godHoming)
			{
				_godHomeClock += dt;
				_godTurn = GodTurn.Home(_godHomeTurn, _godHomeTurnRate, _godHomeSeconds, _godHomeClock, out _godTurnRate);
				_godTip = Mathf.Clamp01(GodTurn.Home(_godHomeTip, _godHomeTipRate, _godHomeSeconds, _godHomeClock, out _godTipRate));
				if (_godHomeClock >= _godHomeSeconds)
				{
					GodTurnReset();
				}
				return;
			}
			float t = 1f - Mathf.Exp((0f - dt) / 0.14f);
			_godTurnRate = Mathf.Lerp(_godTurnRate, num2 * 1.75f, t);
			float num4 = num3 * 1.1f;
			if (num4 > 0f)
			{
				num4 *= Mathf.SmoothStep(0.15f, 1f, (1f - _godTip) / 0.25f);
			}
			else if (num4 < 0f)
			{
				num4 *= Mathf.SmoothStep(0.15f, 1f, _godTip / 0.25f);
			}
			_godTipRate = Mathf.Lerp(_godTipRate, num4, t);
			if (num2 == 0f && Mathf.Abs(_godTurnRate) < 0.001f)
			{
				_godTurnRate = 0f;
			}
			if (num3 == 0f && Mathf.Abs(_godTipRate) < 0.001f)
			{
				_godTipRate = 0f;
			}
			_godTurn = DivePath.Wrap(_godTurn + _godTurnRate * dt);
			_godTip = Mathf.Clamp01(_godTip + _godTipRate * dt);
			if ((_godTip <= 0f && _godTipRate < 0f) || (_godTip >= 1f && _godTipRate > 0f))
			{
				_godTipRate = 0f;
			}
		}

		private void StartGodHome()
		{
			float num = _godTurn + _godTurnRate * 0.14f;
			float num2 = MathF.PI * 2f * Mathf.Round(num / (MathF.PI * 2f));
			_godHomeTurn = _godTurn - num2;
			_godHomeTurnRate = _godTurnRate;
			_godHomeTip = _godTip;
			_godHomeTipRate = _godTipRate;
			_godHomeSeconds = GodTurn.HomeSeconds(_godHomeTurn, _godTip);
			_godHomeClock = 0f;
			_godHoming = true;
		}

		private bool GodTurnKeysFree()
		{
			if (IsTyping() || _settingsMenu.Open || WorldNews.Open)
			{
				return false;
			}
			if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
			{
				return false;
			}
			try
			{
				return !ScrollWindow.isWindowActive();
			}
			catch
			{
				return true;
			}
		}

		private float GodDiveTurn()
		{
			if (!_diveFromGod)
			{
				return 0f;
			}
			return _godTurn;
		}

		private float GodRiseTurn(bool intoView)
		{
			_godTip = (_godTipRate = (_godTurnRate = 0f));
			_godHoming = false;
			if (!intoView)
			{
				_godTurn = 0f;
			}
			return _godTurn;
		}

		private void GodViewKeys()
		{
			if (!_active && !IsTyping())
			{
				if ((Input.GetKeyDown(KeyCode.V) || VrControls.Down(KeyCode.V) || PadControls.Down(KeyCode.V)) && !ControllableUnit.isControllingUnit() && !_fly)
				{
					SetGodView(!Settings.GodView);
				}
				if (Input.GetKeyDown(KeyCode.F7) && GodShown)
				{
					GodTimeKey();
				}
			}
		}

		private void GodTimeKey()
		{
			Settings.TimeMode = (Settings.TimeMode + 1) % DayCycle.ModeNames.Length;
			Settings.Save();
			_relightNow = true;
			GodToast("Time of day: " + _day.Describe(Settings));
		}

		internal void GodTimeKeyForTest()
		{
			GodTimeKey();
		}

		internal void SetGodView(bool on)
		{
			if (on != Settings.GodView)
			{
				_godSwitched = true;
			}
			Settings.GodView = on;
			Settings.Save();
			if (VrRunning)
			{
				GodToast(on ? "3D world view   (Y for the flat map)" : "Flat map   (Y for the 3D world)");
			}
			else
			{
				GodToast(on ? "3D world view   (arrow keys turn it, V for the flat map)" : "Flat map   (V for the 3D world)");
			}
			Log.Info("god view " + (on ? "on" : "off"));
		}

		private void GodToast(string text)
		{
			_godToast = text;
			_godToastUntil = Time.unscaledTime + 2.5f;
		}

		private float GodCover(float zoom)
		{
			float godViewZoom = Settings.GodViewZoom;
			float num = Mathf.InverseLerp(Mathf.Log(godViewZoom * 1.5f), Mathf.Log(godViewZoom), Mathf.Log(Mathf.Max(1f, zoom)));
			return num * num * (3f - 2f * num);
		}

		private void UpdateGodView()
		{
			Camera camera = ((Config.game_loaded && World.world != null) ? World.world.camera : null);
			bool flag = false;
			try
			{
				flag = SmoothLoader.isLoading();
			}
			catch
			{
			}
			if (_fly && (_active || _dive != Dive.None || camera == null || flag))
			{
				EndFly();
			}
			bool flag2 = (Settings.GodView || _godFade > 0f || _fly) && !_active && _dive == Dive.None && camera != null && !flag && Time.unscaledTime >= _godRetryAt && _sampler.EnsureWorld();
			if (_fly)
			{
				_flyAlpha = Mathf.MoveTowards(_flyAlpha, 1f, Time.unscaledDeltaTime / 0.3f);
			}
			_godAlpha = ((!flag2) ? 0f : (_fly ? _flyAlpha : GodShowing(GodCover(camera.orthographicSize), Time.unscaledDeltaTime)));
			HoldMapDrawing(_godAlpha >= 1f || (_active && _dive == Dive.None && !MapHoldFirstPersonOff));
			if (!_fly)
			{
				GodView.Flying = false;
			}
			if (!flag2)
			{
				GodTurnIdle(_active || _dive != Dive.None);
			}
			if (_godAlpha <= 0f)
			{
				HideGodScreen();
				GodView.Active = false;
				ResetGodZoom();
				if (!flag2)
				{
					_godGround = float.NaN;
				}
				return;
			}
			if (!_godProbed)
			{
				ProbeGodLayers(camera);
			}
			GodView.Install();
			if (_fly)
			{
				UpdateFly(camera, Time.unscaledDeltaTime);
				_godAlpha = (_fly ? _flyAlpha : 1f);
			}
			else
			{
				UpdateGodZoom(camera, Time.unscaledDeltaTime);
			}
			RenderGodFrame(camera, Time.unscaledDeltaTime);
			GodView.Shown = _godTaskCamera;
			ShowGodScreen(camera, _godAlpha);
			GodView.Active = _godAlpha >= 0.5f;
			_godFailures = 0;
		}

		private void GodViewFailed(Exception e)
		{
			HideGodScreen();
			GodView.Active = false;
			EndFly();
			_godGround = float.NaN;
			_godFailures++;
			_godRetryAt = Time.unscaledTime + 3f;
			if (_godFailures >= 5)
			{
				Settings.GodView = false;
				Log.Error("god view failed again, back to the flat map: " + e);
				GodToast("The 3D world view failed; back to the flat map (V to try again)");
			}
			else
			{
				Log.Error("god view frame failed (trying again shortly): " + e.Message);
			}
		}

		private void RenderGodFrame(Camera cam, float dt)
		{
			if (GodTimeForTest >= 0f)
			{
				dt = 1E-05f;
			}
			TerrainCache cache = _sampler.Cache;
			Vector2 vector = cam.transform.position;
			float num = Mathf.Max(10f, cam.orthographicSize);
			float num2 = GodGroundTarget(cache, vector, num);
			_godGround = (float.IsNaN(_godGround) ? num2 : Mathf.Lerp(_godGround, num2, 1f - Mathf.Exp((0f - Mathf.Lerp(2.5f, 7f, _godPull)) * dt)));
			_day.Update(Settings, dt, fromAbove: true);
			Atmosphere air = _day.Air;
			Renderer.Air = air;
			Renderer.NightNumber = _day.Night;
			Renderer.ColdSky = _day.Cold;
			Calendar.UpdateLook(Time.unscaledDeltaTime);
			Renderer.PaletteLevels = ((Settings.Palette >= 1) ? 18 : 0);
			ApplyShadows();
			Renderer.Windows.Clear();
			FollowLight(air);
			int num3 = RenderWidth(GpuWanted);
			int num4 = Mathf.Max(90, Mathf.RoundToInt((float)(num3 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
			bool fly = _fly;
			ViewState camera = (fly ? FlyCamera() : GodViewCamera(vector, num, num3, num4));
			float reach = (fly ? ViewMaxDistance : GodCamera.Reach(camera, num3, num4));
			bool flag = GodCloseness > 0f || fly;
			if (flag)
			{
				reach = Mathf.Min(reach, ViewMaxDistance);
			}
			bool flag2 = !fly && VrGodGather(camera, vector, ref reach);
			_godCut = ((GodCutOffForTest || flag2 || fly) ? SightCut.Off : SightCut.Make(camera, num3, num4, _godLook.x, _godLook.y, _godLook.z, 1f));
			if (fly)
			{
				SteepAround(camera, num3, num4, Mathf.Max(cache.SampleHeight(camera.X, camera.Y), 0f));
			}
			_godTaskBehind = Mathf.Max(2f, SceneCollector.AllAround);
			GodView.Resolve();
			bool working = (Input.GetMouseButton(0) || Input.GetMouseButton(1)) && GodView.Active && GodView.MouseValid;
			Vector2 focus = (working ? GodView.LandPoint : vector);
			_frameClock.Restart();
			_sampler.TickLandforms(dt);
			double totalMilliseconds = _frameClock.Elapsed.TotalMilliseconds;
			_sprites.BeginFrame();
			Renderer.Resize(num3, num4);
			Renderer.MaxDistance = reach;
			Renderer.FarModelReach = 0f;
			Renderer.FadeAt = 0f;
			Renderer.HazeScale = Mathf.Min(1f, 194.40001f / Mathf.Max(1f, Renderer.HazeFadeStart));
			Renderer.BillboardDistance = (flag ? Mathf.Min(Settings.DetailDistance, reach) : reach);
			if (flag2)
			{
				Renderer.BillboardDistance = Mathf.Min(Renderer.BillboardDistance, _godTaskDetail);
			}
			Renderer.Time = ((GodTimeForTest >= 0f) ? GodTimeForTest : SeaNow);
			Renderer.RippleStrength = 0f;
			Renderer.ShadowFocusSet = true;
			Renderer.ShadowFocusX = vector.x;
			Renderer.ShadowFocusY = vector.y;
			if (fly)
			{
				float num5 = Mathf.Min(Mathf.Max(0f, camera.Z - Mathf.Max(cache.SampleHeight(camera.X, camera.Y), 0f)) + 4f, 25f);
				Renderer.ShadowFocusX = camera.X + Mathf.Cos(camera.Yaw) * num5;
				Renderer.ShadowFocusY = camera.Y + Mathf.Sin(camera.Yaw) * num5;
			}
			_creatureReach = (flag2 ? Renderer.BillboardDistance : 0f);
			if (fly)
			{
				_creatureReach = Mathf.Min(reach, Mathf.Max(Renderer.BillboardDistance, Settings.CreatureDistance * _creatureDetail));
			}
			_scene.SkyReach = reach;
			_scene.SkyPixel = 1f / ((float)num3 * 0.5f / Mathf.Tan(camera.Fov * 0.5f));
			_scene.SkyFocus = vector;
			ViewState viewState = (flag2 ? VrGodGatherView() : camera);
			if (flag2)
			{
				SceneCollector.AllAround = _godTaskRoom.Behind;
			}
			_scene.GodFrameCamera = camera;
			_scene.GodFrameWidth = ((!(flag || flag2)) ? num3 : 0);
			_scene.GodFrameHeight = num4;
			long cyclesNow = Hitch.CyclesNow;
			try
			{
				CollectScene(viewState, viewState, null, 0f, air, dt);
			}
			finally
			{
				_scene.GodFrameWidth = 0;
				GodSceneCycles += Hitch.CyclesNow - cyclesNow;
				_scene.SkyReach = 0f;
				if (flag2)
				{
					SceneCollector.AllAround = 0f;
				}
			}
			ClearGodLineOfSight(_scene.Meshes, viewState);
			_godCut.Prune(cache, _scene.Meshes, _scene.UnitTagEnd);
			ApplyWeather(air, vector, dt);
			Renderer.Meshes.Clear();
			Renderer.Meshes.AddRange(_scene.Meshes);
			Renderer.Lights.Clear();
			Renderer.Lights.AddRange(_scene.Lights);
			Renderer.Streaks.AddRange(_scene.Streaks);
			Renderer.OverlayMeshes.Clear();
			Renderer.Cut = _godCut;
			Renderer.PrecipX = (fly ? camera.X : vector.x);
			Renderer.PrecipY = (fly ? camera.Y : vector.y);
			Renderer.PrecipCell = Mathf.Max(1f, num / 12f);
			_godTaskRoomForCursor = flag2;
			AddGodCursor(_scene.Voxels, camera, cache);
			AddGodLockRing(_scene.Voxels, camera, cache);
			_gpLand += totalMilliseconds;
			_gpScene += _frameClock.Elapsed.TotalMilliseconds - totalMilliseconds;
			_godTaskCamera = camera;
			_godTaskWidth = num3;
			_godTaskHeight = num4;
			_godTaskGround = _godGround;
			_godTaskAt = vector;
			_godTaskZoom = num;
			_godTaskVr = flag2;
			bool gpu = GpuWanted;
			_godTaskGpu = gpu;
			List<Billboard> billboards = _scene.Billboards;
			List<VoxelObject> voxels = _scene.Voxels;
			float top = _sampler.MaxHeight + 0.45f;
			FrameRenderer renderer = Renderer;
			WorldSampler sampler = _sampler;
			_godTask = Task.Run(delegate
			{
				long timestamp = Stopwatch.GetTimestamp();
				try
				{
					sampler.TickTiles(focus.x, focus.y, working);
				}
				catch (Exception godLandError)
				{
					_godLandError = godLandError;
				}
				long timestamp2 = Stopwatch.GetTimestamp();
				if (!gpu)
				{
					renderer.Render(cache, camera, billboards, voxels, top);
				}
				long timestamp3 = Stopwatch.GetTimestamp();
				_godTaskLandMs = (double)(timestamp2 - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
				_godTaskMs = (double)(timestamp3 - timestamp2) * 1000.0 / (double)Stopwatch.Frequency;
			});
			FrameMs = (float)_frameClock.Elapsed.TotalMilliseconds;
			_renderMs = Mathf.Lerp(_renderMs, FrameMs, 0.1f);
		}

		private void FinishGodRender()
		{
			if (_godTask == null)
			{
				return;
			}
			Task godTask = _godTask;
			_godTask = null;
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				godTask.Wait();
			}
			catch (AggregateException ex)
			{
				ResetGodRenderer();
				GodViewFailed(ex.InnerException ?? ex);
				return;
			}
			double num = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			if (_godLandError != null)
			{
				if (!_godLandErrorLogged)
				{
					Log.Error("god view: land refresh failed (it carries on next frame): " + _godLandError);
				}
				_godLandErrorLogged = true;
				_godLandError = null;
			}
			if (_godTaskGpu)
			{
				try
				{
					FinishGodGpu(num);
					return;
				}
				finally
				{
					ResetGodRenderer();
				}
			}
			ResetGodRenderer();
			int godTaskWidth = _godTaskWidth;
			int godTaskHeight = _godTaskHeight;
			int num2 = godTaskWidth * godTaskHeight;
			if (Renderer.Width == godTaskWidth && Renderer.Height == godTaskHeight)
			{
				UploadFrame(godTaskWidth, godTaskHeight);
				if (_godDepth.Length != num2)
				{
					_godDepth = new float[num2];
				}
				Array.Copy(Renderer.Depth, _godDepth, num2);
				GodView.View = _godTaskCamera;
				_godShown = _godTaskCamera;
				GodView.Width = godTaskWidth;
				GodView.Height = godTaskHeight;
				GodView.Depth = _godDepth;
				GodView.Terrain = _sampler.Cache;
				GodView.Cut = _godCut;
				GodView.FlatZ = _godTaskGround;
				GodView.ViewAt = _godTaskAt;
				double num3 = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
				GodPerf(num, num3 - num);
			}
		}

		private void ResetGodRenderer()
		{
			Renderer.Cut = SightCut.Off;
			Renderer.PrecipX = float.NaN;
			Renderer.PrecipCell = 1f;
		}

		private float GodDrawnGround(TerrainCache cache, float x, float y)
		{
			float num = Mathf.Max(cache.SampleHeight(x, y), 0f);
			if (!_godCut.On)
			{
				return num;
			}
			return _godCut.Land(_godCut.Depth(x, y), num);
		}

		private void GodPerf(double waited, double upload)
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			_gpFrame += (double)unscaledDeltaTime * 1000.0;
			_gpWait += waited;
			_gpUpload += upload;
			_gpDraw += _godTaskMs;
			_gpLandBeside += _godTaskLandMs;
			_gpTerrain += Renderer.TerrainMs;
			_gpObjects += Renderer.ObjectsMs;
			_gpModels += Renderer.MeshMs;
			_gpPost += Renderer.PostMs - Renderer.MeshMs;
			_gpTris += Renderer.MeshTriangles;
			_gpFrames++;
			_gpSince += unscaledDeltaTime;
			if (!(_gpSince < 10f))
			{
				double num = 1.0 / (double)_gpFrames;
				double num2 = (_gpLand + _gpScene + _gpWait + _gpUpload) * num;
				float num3 = ((World.world != null && World.world.camera != null) ? World.world.camera.orthographicSize : 0f);
				Log.Info($"god view perf: {1000.0 / (_gpFrame * num):F0} fps (frame {_gpFrame * num:F1} ms); on the game's thread {num2:F1} ms: landforms {_gpLand * num:F1}, " + $"scene {_gpScene * num:F1}, waiting for the drawing {_gpWait * num:F1}, upload {_gpUpload * num:F1}; beside the game: land {_gpLandBeside * num:F1} ms, drawing {_gpDraw * num:F1} ms " + $"(terrain {_gpTerrain * num:F1}, sprites {_gpObjects * num:F1}, models {_gpModels * num:F1}, light and finish {_gpPost * num:F1}), " + $"{_gpTris * num:F0} triangles, {Renderer.Width}x{Renderer.Height}, zoom {num3:F0}");
				_gpFrame = (_gpLand = (_gpLandBeside = (_gpScene = (_gpWait = (_gpUpload = (_gpDraw = (_gpTerrain = (_gpObjects = (_gpModels = (_gpPost = (_gpTris = 0.0)))))))))));
				_gpFrames = 0;
				_gpSince = 0f;
			}
		}

		private static float GodGroundUnder(TerrainCache cache, float x, float y, float zoom)
		{
			float num = zoom * 0.35f;
			float num2 = 0f;
			int num3 = 0;
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					float a = cache.SampleHeight(x + (float)j * num * 0.5f, y + (float)i * num * 0.5f);
					num2 += Mathf.Max(a, 0f);
					num3++;
				}
			}
			return num2 / (float)num3;
		}

		private void AddGodCursor(List<VoxelObject> voxels, ViewState camera, TerrainCache cache)
		{
			if (!GodView.Active || !GodView.MouseValid)
			{
				return;
			}
			MapBox world = World.world;
			try
			{
				if (!GodView.TestCursor && (world.isOverUI() || string.IsNullOrEmpty(world.getSelectedPowerID())))
				{
					return;
				}
			}
			catch
			{
				return;
			}
			float num = 0.6f;
			try
			{
				BrushData current_brush_data = Config.current_brush_data;
				if (current_brush_data != null)
				{
					num = Mathf.Max(0.6f, (float)current_brush_data.size + 0.5f);
				}
			}
			catch
			{
			}
			uint num2 = TerrainCache.Rgb(255, 236, 160);
			if (!MarkShapes.TryGetValue(num2, out var value))
			{
				value = VoxelShape.NewGrid(1, 1, 1);
				value.Box(0, 0, -1, 1, 1, 1, num2);
				MarkShapes[num2] = value;
			}
			Vector2 landPoint = GodView.LandPoint;
			int num3 = Mathf.Clamp(Mathf.RoundToInt(num * 10f), 12, 72);
			float b = (float)Renderer.Width * 0.5f / Mathf.Tan(camera.Fov * 0.5f);
			float num4 = Mathf.Cos(camera.Yaw);
			float num5 = Mathf.Sin(camera.Yaw);
			float num6 = Time.unscaledTime * 0.5f;
			for (int i = 0; i < num3; i++)
			{
				float f = num6 + (float)i * MathF.PI * 2f / (float)num3;
				float num7 = landPoint.x + Mathf.Cos(f) * num;
				float num8 = landPoint.y + Mathf.Sin(f) * num;
				float num9 = GodDrawnGround(cache, num7, num8) + 0.05f;
				float num10 = num7 - camera.X;
				float num11 = num8 - camera.Y;
				float num12 = 2.2f * Mathf.Max(1f, num10 * num4 + num11 * num5) / Mathf.Max(1f, b);
				if (_godTaskRoomForCursor)
				{
					num12 = 0.005f * Mathf.Sqrt(num10 * num10 + num11 * num11 + (num9 - camera.Z) * (num9 - camera.Z));
				}
				voxels.Add(new VoxelObject
				{
					Shape = value,
					Emissive = true,
					NoShadow = true,
					Tag = -1,
					Overlay = true,
					U = new Vec3(1f, 0f, 0f),
					V = new Vec3(0f, 0f, 1f),
					N = new Vec3(0f, 1f, 0f),
					SizeU = num12,
					SizeV = num12 * 0.6f,
					SizeN = num12,
					Origin = new Vec3(num7 - num12 * 0.5f, num8, num9),
					SortDepth = Mathf.Sqrt(num10 * num10 + num11 * num11)
				});
			}
		}

		private void ShowGodScreen(Camera cam, float alpha)
		{
			GodView.InHeadset = _vrGodDrawn;
			if (_vrGodDrawn)
			{
				VrGodCover(cam);
				return;
			}
			VrGodUncover();
			Texture shownFrame = ShownFrame;
			if (shownFrame == null)
			{
				return;
			}
			if (_godScreen == null || _godCam != cam)
			{
				BuildGodScreen(cam);
			}
			if (!(_godScreen == null))
			{
				if (_godMaterial.mainTexture != shownFrame)
				{
					_godMaterial.mainTexture = shownFrame;
				}
				_godMaterial.color = new Color(1f, 1f, 1f, alpha);
				float num = cam.orthographicSize * 2f;
				float x = num * cam.aspect;
				float nearClipPlane = cam.nearClipPlane;
				float farClipPlane = cam.farClipPlane;
				_godScreen.transform.localPosition = new Vector3(0f, 0f, nearClipPlane + Mathf.Min(1f, (farClipPlane - nearClipPlane) * 0.05f));
				_godScreen.transform.localRotation = Quaternion.identity;
				_godScreen.transform.localScale = new Vector3(x, num, 1f);
				if (!_godScreen.activeSelf)
				{
					_godScreen.SetActive(value: true);
				}
				if (_godSavedMask == -1)
				{
					_godSavedMask = cam.cullingMask;
				}
				int num2 = ((alpha >= 1f) ? 1073741824 : (_godSavedMask | 0x40000000));
				if (cam.cullingMask != num2)
				{
					cam.cullingMask = num2;
				}
				MuteOtherCameras(alpha >= 1f, cam);
			}
		}

		private void MuteOtherCameras(bool mute, Camera main)
		{
			if (mute && _godMuted.Count == 0)
			{
				Camera[] allCameras = Camera.allCameras;
				foreach (Camera camera in allCameras)
				{
					if (camera != main && camera.enabled && camera.targetTexture == null && camera.cullingMask != 0)
					{
						_godMuted.Add(new KeyValuePair<Camera, int>(camera, camera.cullingMask));
						camera.cullingMask = 0;
					}
				}
			}
			else
			{
				if (mute || _godMuted.Count <= 0)
				{
					return;
				}
				foreach (KeyValuePair<Camera, int> item in _godMuted)
				{
					if (item.Key != null)
					{
						item.Key.cullingMask = item.Value;
					}
				}
				_godMuted.Clear();
			}
		}

		private void HideGodScreen()
		{
			GodView.InHeadset = false;
			VrGodUncover();
			if (_godScreen != null && _godScreen.activeSelf)
			{
				_godScreen.SetActive(value: false);
			}
			if (_godSavedMask != -1)
			{
				try
				{
					if (_godCam != null)
					{
						_godCam.cullingMask = _godSavedMask;
					}
				}
				catch (Exception ex)
				{
					Log.Error("god view: map camera: " + ex.Message);
				}
				_godSavedMask = -1;
			}
			try
			{
				MuteOtherCameras(mute: false, null);
			}
			catch (Exception ex2)
			{
				Log.Error("god view: cameras: " + ex2.Message);
			}
		}

		private void BuildGodScreen(Camera cam)
		{
			HideGodScreen();
			if (_godScreen != null)
			{
				UnityEngine.Object.Destroy(_godScreen);
			}
			_godCam = cam;
			Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("UI/Default");
			if (shader == null)
			{
				Log.Error("god view: no shader to show the view with");
				Settings.GodView = false;
				return;
			}
			if (_godMaterial == null)
			{
				_godMaterial = new Material(shader)
				{
					name = "FirstPersonGodView"
				};
			}
			_godMaterial.renderQueue = 4000;
			if (_godScreenMesh != null)
			{
				UnityEngine.Object.Destroy(_godScreenMesh);
			}
			Mesh mesh = (_godScreenMesh = new Mesh
			{
				name = "FirstPersonGodScreen"
			});
			mesh.vertices = new Vector3[4]
			{
				new Vector3(-0.5f, -0.5f, 0f),
				new Vector3(-0.5f, 0.5f, 0f),
				new Vector3(0.5f, 0.5f, 0f),
				new Vector3(0.5f, -0.5f, 0f)
			};
			mesh.uv = new Vector2[4]
			{
				new Vector2(0f, 0f),
				new Vector2(0f, 1f),
				new Vector2(1f, 1f),
				new Vector2(1f, 0f)
			};
			mesh.colors32 = new Color32[4]
			{
				new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
				new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
				new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue),
				new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue)
			};
			mesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
			mesh.RecalculateBounds();
			mesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 1000f));
			_godScreen = new GameObject("FirstPersonGodScreen")
			{
				layer = 30
			};
			_godScreen.transform.SetParent(cam.transform, worldPositionStays: false);
			_godScreen.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer meshRenderer = _godScreen.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = _godMaterial;
			meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
			meshRenderer.receiveShadows = false;
			meshRenderer.sortingOrder = 32767;
			_godScreen.SetActive(value: false);
		}

		private void ProbeGodLayers(Camera cam)
		{
			_godProbed = true;
			try
			{
				Camera[] allCameras = Camera.allCameras;
				foreach (Camera camera in allCameras)
				{
					Log.Info($"god view: camera {camera.name} depth {camera.depth} mask 0x{camera.cullingMask:X} ortho {camera.orthographic} near {camera.nearClipPlane} far {camera.farClipPlane} main {camera == cam}");
				}
				Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
				foreach (Canvas canvas in array)
				{
					if (canvas.isRootCanvas)
					{
						Log.Info(string.Format("god view: canvas {0} {1} layer {2} order {3} camera {4} active {5}", canvas.name, canvas.renderMode, canvas.gameObject.layer, canvas.sortingOrder, (canvas.worldCamera != null) ? canvas.worldCamera.name : "-", canvas.isActiveAndEnabled));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("god view probe: " + ex.Message);
			}
		}

		private void DrawGodGui()
		{
			if (GodShown)
			{
				DrawGodLockGui();
			}
			if (_fly && Event.current.type == EventType.Repaint)
			{
				DrawFlyGui();
			}
			if (_godToast != null && !(Time.unscaledTime >= _godToastUntil) && Event.current.type == EventType.Repaint)
			{
				_hud.DrawGodToast(_godToast, _godToastUntil);
			}
		}

		private void NoteDiveFromGod(Vector2 mapFrom)
		{
			_diveFromGod = (Settings.GodView || _fly) && _godAlpha >= 1f && !float.IsNaN(_godGround);
			_diveFromFly = false;
			_diveGodFrom = mapFrom;
			_diveGodGround = _godGround;
			_diveGodOffsetSet = false;
			_diveCutFrom = (_diveFromGod ? GodCloseness : 0f);
			float godCloseness = GodCloseness;
			if (_diveFromGod && godCloseness > 0f)
			{
				_diveZoom = 10f;
				_diveT = DivePath.InverseEase(godCloseness);
				_view.Yaw = MathF.PI / 2f + _godTurn;
				_pitchDegrees = 0f;
			}
			if (_fly)
			{
				if (_diveFromGod)
				{
					FlyIntoDive();
				}
				else
				{
					EndFly();
				}
			}
			ResetGodZoom();
			HideGodScreen();
			GodView.Active = false;
		}

		private ViewState GodDiveCamera(ViewState camera, Vector2 target, float aimZ, float closeness)
		{
			if (!_diveFromGod)
			{
				return camera;
			}
			if (!_diveGodOffsetSet)
			{
				_diveGodOffset = new Vector3(_godShown.X - camera.X, _godShown.Y - camera.Y, _godShown.Z - camera.Z);
				_diveGodLook = new Vector3(DivePath.Wrap(_godShown.Yaw - camera.Yaw), _godShown.Pitch - camera.Pitch, _godShown.Shear - camera.Shear);
				_diveGodK0 = closeness;
				_diveGodOffsetSet = true;
			}
			float num = ((_diveGodK0 <= 0.001f) ? Mathf.Max(0.15f, DivePath.ClosenessAt(18f, _diveZoom)) : Mathf.Max(0.05f, (1f - _diveGodK0) * 0.6f));
			float num2 = 1f - DivePath.Ease((closeness - _diveGodK0) / num);
			camera.X += _diveGodOffset.x * num2;
			camera.Y += _diveGodOffset.y * num2;
			camera.Z += _diveGodOffset.z * num2;
			camera.Yaw += _diveGodLook.x * num2;
			camera.Pitch += _diveGodLook.y * num2;
			camera.Shear += _diveGodLook.z * num2;
			return camera;
		}

		private SightCut DiveCut(ViewState camera, int width, int height, float x, float y, float z, float closeness)
		{
			if (!_diveFromGod || _diveFromFly)
			{
				return SightCut.Off;
			}
			if (Lens.Of(in camera, (float)height / (float)Mathf.Max(1, width)).Steep)
			{
				return SightCut.Off;
			}
			float num = ((_dive == Dive.In) ? _diveCutFrom : 0f);
			float strength = 1f - DivePath.Ease((closeness - num) / Mathf.Max(0.05f, (1f - num) * 0.45f));
			return SightCut.Make(camera, width, height, x, y, z, strength);
		}

		private void ApplyDiveCut(SightCut cut)
		{
			Renderer.Cut = cut;
			cut.Prune(_sampler.Cache, Renderer.Meshes, _scene.UnitTagEnd);
		}

		private void GodFollowAfterDive(Actor body)
		{
			if (body != null && body.isAlive() && Settings.GodView)
			{
				_godLock = body;
				_godLockId = body.data.id;
				_godPull = 1f;
				_godLockTime = -10f;
				_godFollow = CreaturePlace(body);
				_godFollowVel = Vector2.zero;
				_godCamSet = false;
				_godZoomedInAt = -10f;
				CreatureEye(body, 0f, out _godLockGround, out _godEyeZ);
			}
		}

		private void UpdateGodZoom(Camera cam, float dt)
		{
			MapBox world = World.world;
			float unscaledTime = Time.unscaledTime;
			float num = cam.orthographicSize;
			GodView.Resolve();
			bool flag = IsTyping();
			bool flag2 = false;
			try
			{
				flag2 = world.isOverUI();
			}
			catch
			{
			}
			bool flag3 = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
			float num2 = GodView.TestWheel ?? ((flag || flag2 || flag3) ? 0f : Input.mouseScrollDelta.y);
			GodView.TestWheel = null;
			if (!flag && !flag3)
			{
				if (Input.GetKey(KeyCode.Q))
				{
					num2 += 3f * dt;
				}
				if (Input.GetKey(KeyCode.E))
				{
					num2 -= 3f * dt;
				}
			}
			num2 += _vrZoomWheel;
			_vrZoomWheel = 0f;
			if (num2 > 0f)
			{
				_godZoomedInAt = unscaledTime;
			}
			bool flag4 = !GodView.ArrowsTurn && (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow));
			bool flag5 = !flag && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) || flag4);
			flag5 |= MoveCamera.camera_drag_run || _vrPanning;
			_vrPanning = false;
			bool flag6 = _godCamSet && Vector2.Distance(cam.transform.position, _godCamWas) > (VrRunning ? 3f : 0.05f);
			bool flag7 = false;
			try
			{
				flag7 = MoveCamera.hasFocusUnit();
			}
			catch
			{
			}
			if (_godLock != null && (flag5 || flag6 || flag7))
			{
				LetGo(byHand: true);
			}
			bool flag8 = num <= 10.05f;
			bool flag9 = _godCloseTarget < 9.999f || _godClose < 9.99f;
			if (_godLock != null && (!_godLock.isAlive() || _godLock.data == null || _godLock.data.id != _godLockId || (num > 20.8f && !flag9)))
			{
				LetGo(byHand: false);
			}
			if (_godLock == null && unscaledTime - _godZoomedInAt < 0.6f && num < 16f && _godAlpha >= 1f && !PowerIsAimed())
			{
				_godLock = MagnetTarget(cam, num, !flag2);
				if (_godLock != null)
				{
					_godLockTime = unscaledTime;
					_godLockId = _godLock.data.id;
					_godFollow = CreaturePlace(_godLock);
					_godFollowVel = Vector2.zero;
					Log.Info("god view: drawn to " + Describe(_godLock));
				}
			}
			if (_godLock != null && num2 > 0f && flag8)
			{
				if (_godAtMinSince >= 0f && unscaledTime - _godAtMinSince > 0.35f && _godClose <= 0.94499993f && _godPull > 0.95f && unscaledTime - _godSnapAt > 1f)
				{
					SnapInto(_godLock);
				}
				else
				{
					_godCloseTarget = Mathf.Max(0.9f, _godCloseTarget * Mathf.Pow(0.8f, num2));
				}
			}
			else if (num2 < 0f && flag9)
			{
				_godCloseTarget = Mathf.Min(10f, _godCloseTarget / Mathf.Pow(0.8f, 0f - num2));
			}
			if (_godLock == null)
			{
				_godCloseTarget = 10f;
			}
			if (!(_godCloseTarget <= 0.9009f))
			{
				_godAtMinSince = -1f;
			}
			else if (_godAtMinSince < 0f)
			{
				_godAtMinSince = unscaledTime;
			}
			float t = 1f - Mathf.Exp((0f - ((_godLock != null) ? 9f : 4f)) * dt);
			_godClose = Mathf.Exp(Mathf.Lerp(Mathf.Log(_godClose), Mathf.Log(_godCloseTarget), t));
			if (Mathf.Abs(_godClose - _godCloseTarget) < 0.002f)
			{
				_godClose = _godCloseTarget;
			}
			flag9 = _godCloseTarget < 9.999f || _godClose < 9.99f;
			if (flag9)
			{
				HoldMapZoom(cam, 10f);
				num = 10f;
			}
			float num3 = 0f;
			if (_godLock != null)
			{
				float num4 = Mathf.InverseLerp(16f, 11f, num);
				num3 = (flag9 ? 1f : (num4 * num4 * (3f - 2f * num4)));
			}
			_godPull = ((_godLock == null) ? 0f : Mathf.MoveTowards(_godPull, num3, dt * ((num3 > _godPull) ? 2.5f : 0.4f)));
			if (_godLock != null)
			{
				Vector2 target = CreaturePlace(_godLock);
				_godFollow = Vector2.SmoothDamp(_godFollow, target, ref _godFollowVel, 0.09f, 200f, Mathf.Max(0.0001f, dt));
				float num5 = Mathf.Lerp(1.6f, 30f, _godPull * _godPull);
				Vector2 vector = Vector2.Lerp(cam.transform.position, _godFollow, 1f - Mathf.Exp((0f - num5) * dt));
				cam.transform.position = new Vector3(vector.x, vector.y, cam.transform.position.z);
				CreatureEye(_godLock, dt, out _godLockGround, out _godEyeZ);
			}
			_godCamWas = cam.transform.position;
			_godCamSet = true;
		}

		private void LetGo(bool byHand)
		{
			if (_godLock != null)
			{
				Log.Info("god view: let go of " + Describe(_godLock) + (byHand ? " (panned)" : ""));
			}
			_godLock = null;
			_godPull = 0f;
			if (byHand)
			{
				_godZoomedInAt = -10f;
			}
		}

		private void ResetGodZoom()
		{
			_godLock = null;
			_godPull = 0f;
			_godClose = (_godCloseTarget = 10f);
			_godAtMinSince = -1f;
			_godLift = 0f;
			_godCamSet = false;
		}

		private static void HoldMapZoom(Camera cam, float zoom)
		{
			try
			{
				cam.orthographicSize = zoom;
				if (MoveCamera.instance != null)
				{
					MoveCamera.instance.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("god view: map zoom: " + ex.Message);
			}
		}

		private static bool PowerIsAimed()
		{
			try
			{
				string selectedPowerID = World.world.getSelectedPowerID();
				return !string.IsNullOrEmpty(selectedPowerID) && !selectedPowerID.Contains("inspect") && !selectedPowerID.Contains("select") && !selectedPowerID.Contains("follow");
			}
			catch
			{
				return false;
			}
		}

		private Actor MagnetTarget(Camera cam, float mapZoom, bool mouseOnWorld)
		{
			Vector2 vector = cam.transform.position;
			float num = Mathf.Max(2.5f, ((_godCloseTarget < 10f) ? _godClose : mapZoom) * 0.45f);
			Actor actor = null;
			float num2 = float.MaxValue;
			List<Actor> simpleList;
			try
			{
				simpleList = World.world.units.getSimpleList();
			}
			catch
			{
				return null;
			}
			for (int i = ((!mouseOnWorld || !GodView.MouseValid || !(Vector2.Distance(GodView.MousePoint, vector) < mapZoom * 1.9f)) ? 1 : 0); i < 2; i++)
			{
				if (actor != null)
				{
					break;
				}
				Vector2 vector2 = ((i == 0) ? GodView.MousePoint : vector);
				float num3 = ((i == 0) ? Mathf.Max(1.5f, mapZoom * 0.18f) : num);
				for (int j = 0; j < simpleList.Count; j++)
				{
					Actor actor2 = simpleList[j];
					if (actor2 == null || !actor2.isAlive() || actor2.asset == null || actor2.is_inside_building || actor2.is_inside_boat || (NetSession.Current != null && WorldSync.IsOtherPlayersBody(actor2)))
					{
						continue;
					}
					Vector2 current_position = actor2.current_position;
					float num4 = current_position.x - vector2.x;
					float num5 = current_position.y - vector2.y;
					if (Mathf.Abs(num4) > num3 || Mathf.Abs(num5) > num3)
					{
						continue;
					}
					float num6 = Mathf.Sqrt(num4 * num4 + num5 * num5);
					if (!(num6 > num3))
					{
						bool flag = false;
						try
						{
							flag = actor2.isSapient();
						}
						catch
						{
						}
						float num7 = (flag ? (num6 / 1.6f) : num6);
						if (num7 < num2)
						{
							num2 = num7;
							actor = actor2;
						}
					}
				}
			}
			return actor;
		}

		private Vector2 CreaturePlace(Actor a)
		{
			if (_scene.DrawnAt(a, out var drawn))
			{
				return drawn;
			}
			return a.current_position;
		}

		private void CreatureEye(Actor a, float dt, out float ground, out float eyeZ)
		{
			Vector2 current_position = a.current_position;
			TerrainCache cache = _sampler.Cache;
			ground = cache.SampleHeight(current_position.x, current_position.y);
			if (SceneCollector.IsSwimming(a))
			{
				ground = Mathf.Max(ground, 0f);
			}
			float value = 1f;
			try
			{
				SpriteImage spriteImage = _sprites.Get(a.getSpriteToRender());
				if (spriteImage != null)
				{
					value = Stature.Of(a, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(a.current_scale.y), dt);
				}
			}
			catch
			{
			}
			value = Mathf.Clamp(value, 0.3f, 8f);
			float num = 0f;
			try
			{
				num = a.position_height;
			}
			catch
			{
			}
			eyeZ = ground + num + value * 0.9f * Settings.EyeHeight;
		}

		private ViewState GodViewCamera(Vector2 at, float mapZoom, int width, int height)
		{
			float godCloseness = GodCloseness;
			_godLook = new Vector3(at.x, at.y, _godGround);
			if (godCloseness <= 0f)
			{
				return GodTurn.Tip(GodCamera.Camera(at.x, at.y, _godGround, mapZoom, _godTurn, width, height), new Vec3(at.x, at.y, _godGround), _godTip);
			}
			float z = ((_godLock != null) ? _godEyeZ : (Mathf.Max(_sampler.Cache.SampleHeight(at.x, at.y), 0f) + 0.9f));
			ViewState eye = new ViewState
			{
				X = at.x,
				Y = at.y,
				Z = z,
				Yaw = MathF.PI / 2f + _godTurn,
				Pitch = 0f,
				Fov = Settings.FieldOfView * (MathF.PI / 180f)
			};
			ViewState camera = DivePath.Camera(eye, _godGround, godCloseness, 10f, 0f, width, height);
			float num = DivePath.Swoop(DivePath.Zoom(godCloseness, 10f));
			Vec3 look = new Vec3(eye.X, eye.Y, eye.Z + (_godGround - eye.Z) * num);
			_godLook = new Vector3(look.X, look.Y, look.Z);
			camera = GodTurn.Tip(camera, look, _godTip);
			TerrainCache cache = _sampler.Cache;
			float num2 = 0f;
			for (int i = 0; i <= 14; i++)
			{
				float num3 = (float)i / 14f * 0.92f;
				float x = camera.X + (look.X - camera.X) * num3;
				float y = camera.Y + (look.Y - camera.Y) * num3;
				float num4 = Mathf.Max(cache.SampleHeight(x, y), 0f) + ((i == 0) ? 0.8f : 0.35f);
				float num5 = camera.Z + (look.Z - camera.Z) * num3;
				if (num5 < num4)
				{
					num2 = Mathf.Max(num2, (num4 - num5) / (1f - num3));
				}
			}
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			_godLift = Mathf.Lerp(_godLift, num2, 1f - Mathf.Exp((0f - ((num2 > _godLift) ? 9f : 2f)) * unscaledDeltaTime));
			if (_godLift > 0.01f)
			{
				camera.Z += _godLift;
				float b = Mathf.Sqrt((look.X - camera.X) * (look.X - camera.X) + (look.Y - camera.Y) * (look.Y - camera.Y));
				camera.Pitch = (look.Z - camera.Z) / Mathf.Max(0.5f, b);
			}
			camera.Shear = 1f;
			return camera;
		}

		private void ClearGodLineOfSight(List<MeshInstance> meshes, ViewState camera)
		{
			if (_godLock == null || _godPull < 0.15f)
			{
				return;
			}
			float b = Mathf.Lerp(1f, 0.18f, Mathf.Clamp01((_godPull - 0.15f) / 0.6f));
			Vec3 vec = new Vec3(camera.X, camera.Y, camera.Z);
			Vec3 vec2 = new Vec3(_godFollow.x, _godFollow.y, Mathf.Lerp(_godLockGround, _godEyeZ, 0.55f));
			Vec3 vec3 = vec2 - vec;
			float num = vec3.X * vec3.X + vec3.Y * vec3.Y;
			float num2 = Mathf.Min(vec.X, vec2.X) - 8f;
			float num3 = Mathf.Max(vec.X, vec2.X) + 8f;
			float num4 = Mathf.Min(vec.Y, vec2.Y) - 8f;
			float num5 = Mathf.Max(vec.Y, vec2.Y) + 8f;
			for (int i = 0; i < meshes.Count; i++)
			{
				MeshInstance meshInstance = meshes[i];
				if (meshInstance.Model == null || !meshInstance.Static || meshInstance.ShadowOnly || meshInstance.Tag == -2)
				{
					continue;
				}
				Vec3 position = meshInstance.Position;
				if (position.X < num2 || position.X > num3 || position.Y < num4 || position.Y > num5)
				{
					continue;
				}
				float num6 = Mathf.Sqrt(meshInstance.Basis.C0.X * meshInstance.Basis.C0.X + meshInstance.Basis.C0.Y * meshInstance.Basis.C0.Y);
				float num7 = Mathf.Sqrt(meshInstance.Basis.C1.X * meshInstance.Basis.C1.X + meshInstance.Basis.C1.Y * meshInstance.Basis.C1.Y);
				float num8 = Mathf.Abs(meshInstance.Basis.C2.Z);
				Vec3 min = meshInstance.Model.Min;
				Vec3 max = meshInstance.Model.Max;
				float num9 = Mathf.Max(Mathf.Max(Mathf.Abs(min.X), Mathf.Abs(max.X)) * num6, Mathf.Max(Mathf.Abs(min.Y), Mathf.Abs(max.Y)) * num7) * 0.85f;
				if (num9 < 0.15f)
				{
					continue;
				}
				float num10 = position.Z + min.Z * num8;
				float num11 = position.Z + max.Z * num8;
				float num12 = vec.X - position.X;
				float num13 = vec.Y - position.Y;
				float num14 = num9 * 1.15f;
				float num15 = num;
				float num16 = 2f * (num12 * vec3.X + num13 * vec3.Y);
				float num17 = num12 * num12 + num13 * num13 - num14 * num14;
				float num18 = 0f;
				float num19 = 1f;
				if (num15 > 1E-06f)
				{
					float num20 = num16 * num16 - 4f * num15 * num17;
					if (num20 <= 0f)
					{
						continue;
					}
					float num21 = Mathf.Sqrt(num20);
					num18 = Mathf.Max(0f, (0f - num16 - num21) / (2f * num15));
					num19 = Mathf.Min(1f, (0f - num16 + num21) / (2f * num15));
					if (num18 >= num19)
					{
						continue;
					}
				}
				else if (num17 > 0f)
				{
					continue;
				}
				float a = vec.Z + vec3.Z * num18;
				float b2 = vec.Z + vec3.Z * num19;
				if (!(Mathf.Max(a, b2) < num10) && !(Mathf.Min(a, b2) > num11))
				{
					float num22 = ((num15 > 1E-06f) ? Mathf.Clamp((0f - (num12 * vec3.X + num13 * vec3.Y)) / num15, num18, num19) : 0f);
					float num23 = num12 + vec3.X * num22;
					float num24 = num13 + vec3.Y * num22;
					float value = Mathf.Sqrt(num23 * num23 + num24 * num24);
					float num25 = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f * num9, 1.15f * num9, value));
					if (!(num25 <= 0f))
					{
						meshInstance.Fade = Mathf.Min(meshInstance.Fade, Mathf.Lerp(1f, b, num25));
					}
				}
			}
		}

		private float GodGroundTarget(TerrainCache cache, Vector2 at, float mapZoom)
		{
			float num = GodGroundUnder(cache, at.x, at.y, mapZoom);
			if (_godLock == null)
			{
				float godCloseness = GodCloseness;
				if (!(godCloseness > 0f))
				{
					return num;
				}
				return Mathf.Lerp(num, Mathf.Max(cache.SampleHeight(at.x, at.y), 0f), Mathf.Min(1f, godCloseness * 4f));
			}
			return Mathf.Lerp(num, _godLockGround, _godPull);
		}

		private void SnapInto(Actor a)
		{
			_godSnapAt = Time.unscaledTime;
			if (!Possess.CanJumpTo(a))
			{
				GodToast("You can't take over " + Describe(a));
				return;
			}
			Log.Info("god view: zoomed into " + Describe(a));
			if (!Possess.JumpTo(a))
			{
				GodToast("You can't take over " + Describe(a));
			}
		}

		private void AddGodLockRing(List<VoxelObject> voxels, ViewState camera, TerrainCache cache)
		{
			float num = Time.unscaledTime - _godLockTime;
			if (_godLock == null || num > 0.9f || num < 0f)
			{
				return;
			}
			float num2 = num / 0.9f;
			uint num3 = TerrainCache.Rgb(255, 244, 196);
			if (!MarkShapes.TryGetValue(num3, out var value))
			{
				value = VoxelShape.NewGrid(1, 1, 1);
				value.Box(0, 0, -1, 1, 1, 1, num3);
				MarkShapes[num3] = value;
			}
			Vector2 godFollow = _godFollow;
			float num4 = Mathf.Lerp(1.6f, 0.55f, 1f - (1f - num2) * (1f - num2));
			float b = (float)Renderer.Width * 0.5f / Mathf.Tan(camera.Fov * 0.5f);
			float num5 = Mathf.Cos(camera.Yaw);
			float num6 = Mathf.Sin(camera.Yaw);
			int num7 = 28;
			for (int i = 0; i < num7; i++)
			{
				if ((i & 1) != 1 || !(num2 > 0.5f))
				{
					float f = (float)i * MathF.PI * 2f / (float)num7 + num2 * 1.2f;
					float num8 = godFollow.x + Mathf.Cos(f) * num4;
					float num9 = godFollow.y + Mathf.Sin(f) * num4;
					float z = GodDrawnGround(cache, num8, num9) + 0.04f;
					float num10 = num8 - camera.X;
					float num11 = num9 - camera.Y;
					float num12 = (1f - num2) * 2f * Mathf.Max(1f, num10 * num5 + num11 * num6) / Mathf.Max(1f, b);
					if (!(num12 <= 0.01f))
					{
						voxels.Add(new VoxelObject
						{
							Shape = value,
							Emissive = true,
							NoShadow = true,
							Tag = -1,
							U = new Vec3(1f, 0f, 0f),
							V = new Vec3(0f, 0f, 1f),
							N = new Vec3(0f, 1f, 0f),
							SizeU = num12,
							SizeV = num12 * 0.6f,
							SizeN = num12,
							Origin = new Vec3(num8 - num12 * 0.5f, num9, z),
							SortDepth = Mathf.Sqrt(num10 * num10 + num11 * num11)
						});
					}
				}
			}
		}

		private void DrawGodLockGui()
		{
			if (_godLock != null && !(_godPull < 0.5f) && Event.current.type == EventType.Repaint)
			{
				string text;
				try
				{
					text = _godLock.getName();
				}
				catch
				{
					text = null;
				}
				string text2 = (string.IsNullOrEmpty(text) ? Describe(_godLock) : (text + ",  " + Describe(_godLock)));
				string text3 = ((_godCloseTarget <= 0.9009f && _godClose <= 0.99f && Possess.CanJumpTo(_godLock)) ? (text2 + (VrRunning ? "   ·   right stick up to step into their eyes" : "   ·   scroll in to step into their eyes")) : text2);
				_hud.DrawGodLabel(text3, Mathf.Clamp01((_godPull - 0.5f) * 2f));
			}
		}

		private static List<BaseSimObject>[] NewPickRing()
		{
			List<BaseSimObject>[] array = new List<BaseSimObject>[16];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new List<BaseSimObject>();
			}
			return array;
		}

		private void GpuRetryLater()
		{
			_gpuFails++;
			float num = Mathf.Min(60f, 5f * (float)(1 << Mathf.Min(_gpuFails - 1, 4)));
			_gpuRetryAt = Time.unscaledTime + num;
			if (_gpuFails > 1)
			{
				Log.Info($"gpu: failed {_gpuFails} times in a row, the CPU draws for {num:F0} s before the next try");
			}
		}

		private bool GpuBackdrop(ViewState outside, int width, int height)
		{
			if (!GpuWanted)
			{
				return false;
			}
			GpuMake();
			_gpuTagged[(_gpu.FrameNumber + 1) & 0xF].Clear();
			_gpu.PixelFar = Settings.PixelDistance >= 1;
			_gpu.PresentTo(0, 0);
			_gpu.NoPick = true;
			bool flag;
			try
			{
				flag = _gpu.Available && _gpu.Render(Renderer, _sampler.Cache, outside, width, height, _scene.Billboards, _scene.Voxels, Settings.GpuLook == 0);
			}
			finally
			{
				_gpu.NoPick = false;
			}
			if (!flag)
			{
				string text = (_gpu.Available ? _gpu.Error : (GpuShaders.Error ?? GpuSupport.Missing ?? "compute buffers not supported"));
				if (text != _gpuLastError)
				{
					Log.Error("gpu: drawing on the CPU for now: " + text);
					_gpuLastError = text;
				}
				GpuRetryLater();
				return false;
			}
			return true;
		}

		private bool GpuFrame(ViewState camera, int width, int height, bool diving, bool dead, Actor host, float body, float ground, double collectMs)
		{
			if (!GpuWanted)
			{
				_gpuShown = false;
				return false;
			}
			GpuMake();
			double totalMilliseconds = _frameClock.Elapsed.TotalMilliseconds;
			int num = _gpu.FrameNumber + 1;
			List<BaseSimObject> obj = _gpuTagged[num & 0xF];
			obj.Clear();
			obj.AddRange(_scene.Tagged);
			_gpu.SteepBehind = Mathf.Max(2f, SceneCollector.AllAround);
			_gpu.PixelFar = Settings.PixelDistance >= 1 && !diving;
			PresentGpu();
			if (!_gpu.Available || !_gpu.Render(Renderer, _sampler.Cache, camera, width, height, _scene.Billboards, _scene.Voxels, Settings.GpuLook == 0))
			{
				string text = (_gpu.Available ? _gpu.Error : (GpuShaders.Error ?? GpuSupport.Missing ?? "compute buffers not supported"));
				if (text != _gpuLastError)
				{
					Log.Error("gpu: drawing on the CPU for now: " + text);
					_gpuLastError = text;
				}
				GpuRetryLater();
				_gpuShown = false;
				return false;
			}
			if (GpuSupport.Watch(_gpu.Output, _gpu.FrameNumber))
			{
				Toast("GPU renderer failed on this computer. Using the CPU renderer");
				CrashGuard.Leave("gpu");
				_gpuShown = false;
				return false;
			}
			_gpuShown = true;
			_gpuFails = 0;
			GpuProven(room: false);
			if (!diving)
			{
				ResolutionTrialFrame();
			}
			BaseSimObject picked = null;
			List<BaseSimObject> list = _gpuTagged[_gpu.PickFrame & 0xF];
			if (!diving && _gpu.PickTag >= 0 && _gpu.PickTag < list.Count && _gpu.FrameNumber - _gpu.PickFrame <= 15)
			{
				picked = list[_gpu.PickTag];
			}
			_picked = picked;
			Renderer.PickTag = -1;
			Renderer.PickDepth = _gpu.PickDepth;
			if (!diving && !dead)
			{
				AimFrom(camera, host, body, ground);
			}
			FramesShown++;
			_shown = _gpu.Projection;
			_eyeNow = _shown.View;
			double drawMs = _frameClock.Elapsed.TotalMilliseconds - totalMilliseconds;
			if (!diving)
			{
				_perf.AddLand(_sampler.TakeTimes());
			}
			GpuPerf(collectMs, drawMs);
			return true;
		}

		private void PresentGpu()
		{
			_gpu.PresentTo((!VrRunning) ? Screen.width : 0, (!VrRunning) ? Screen.height : 0);
		}

		private void GpuPerf(double collectMs, double drawMs)
		{
			_gpuCollect += collectMs;
			_gpuDraw += drawMs;
			_gpuFrames++;
			SceneClock.Frame(Time.unscaledDeltaTime * 1000f);
			if (!(Time.unscaledTime - _gpuPerfSince < 10f))
			{
				bool flag = Time.unscaledTime - _gpuPerfSince > 20f;
				if (_gpuFrames > 0 && _gpuPerfSince > 0f && !flag)
				{
					Log.Info($"gpu perf: {(float)_gpuFrames / (Time.unscaledTime - _gpuPerfSince):F1} fps, scene {_gpuCollect / (double)_gpuFrames:F2} ms, submit {_gpuDraw / (double)_gpuFrames:F2} ms ({_gpu.TakeTimes()}), {_gpu.Stats}");
				}
				SceneSplitLog(_gpuFrames > 0 && _gpuPerfSince > 0f && !flag);
				_gpuPerfSince = Time.unscaledTime;
				_gpuCollect = (_gpuDraw = 0.0);
				_gpuFrames = 0;
			}
		}

		internal byte[] ShownPngForTest()
		{
			return ShownPng();
		}

		private byte[] ShownPng()
		{
			if (FrameBehind != null && Frame != null)
			{
				Texture2D texture2D = _gpu.ReadOutput();
				if (texture2D != null && texture2D.width == Frame.width && texture2D.height == Frame.height)
				{
					Color32[] pixels = texture2D.GetPixels32();
					Color32[] pixels2 = Frame.GetPixels32();
					for (int i = 0; i < pixels2.Length; i++)
					{
						if (pixels2[i].a == 0)
						{
							pixels2[i] = pixels[i];
						}
					}
					Texture2D texture2D2 = new Texture2D(Frame.width, Frame.height, TextureFormat.RGBA32, mipChain: false);
					texture2D2.SetPixels32(pixels2);
					texture2D2.Apply(updateMipmaps: false);
					byte[] result = texture2D2.EncodeToPNG();
					UnityEngine.Object.Destroy(texture2D2);
					UnityEngine.Object.Destroy(texture2D);
					return result;
				}
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
			}
			if (_gpuShown && _gpu != null && _gpu.Output != null)
			{
				Texture2D texture2D3 = _gpu.ReadOutput();
				if (texture2D3 == null)
				{
					return null;
				}
				byte[] result2 = texture2D3.EncodeToPNG();
				UnityEngine.Object.Destroy(texture2D3);
				return result2;
			}
			if (!(Frame != null))
			{
				return null;
			}
			return Frame.EncodeToPNG();
		}

		private void DisposeGpu()
		{
			_gpu?.Dispose();
			_gpu = null;
			_gpuShown = false;
		}

		internal float GroundForTest(Vector2 p)
		{
			return CacheHeight(p.x, p.y);
		}

		private bool GpuRoomFrame(Interior room, ViewState eye, int width, int height, List<VoxelObject> voxels, List<BaseSimObject> tagged, float daylight = 1f, bool aside = false)
		{
			GpuMake();
			GpuRoomMarked();
			_gpu.RoomDaylight = daylight;
			int num = _gpu.FrameNumber + 1;
			List<BaseSimObject> obj = _gpuTagged[num & 0xF];
			obj.Clear();
			obj.AddRange(tagged);
			_gpu.SteepBehind = 2f;
			_gpu.PixelFar = false;
			PresentGpu();
			_gpu.Room = true;
			_gpu.RoomShape = room;
			if (aside)
			{
				_gpu.NoPick = true;
			}
			bool flag;
			try
			{
				flag = _gpu.Available && _gpu.Render(Renderer, room.Floor, eye, width, height, NoBillboards, voxels, Settings.GpuLook == 0);
			}
			finally
			{
				_gpu.Room = false;
				_gpu.RoomShape = null;
				if (aside)
				{
					_gpu.NoPick = false;
				}
			}
			if (!flag)
			{
				string text = (_gpu.Available ? _gpu.Error : (GpuShaders.Error ?? "compute buffers not supported"));
				if (text != _gpuLastError)
				{
					Log.Error("gpu: drawing the room on the CPU for now: " + text);
					_gpuLastError = text;
				}
				GpuRetryLater();
				if (!aside)
				{
					_gpuShown = false;
				}
				return false;
			}
			if (_gpuRoom != room)
			{
				if (_gpuRoom != null)
				{
					ForgetRoom(_gpuRoom);
				}
				_gpuRoom = room;
			}
			GpuProven(room: true);
			if (aside)
			{
				return true;
			}
			_gpuShown = true;
			_gpuFails = 0;
			BaseSimObject picked = null;
			List<BaseSimObject> list = _gpuTagged[_gpu.PickFrame & 0xF];
			if (_gpu.PickTag >= 0 && _gpu.PickTag < list.Count && _gpu.FrameNumber - _gpu.PickFrame <= 15)
			{
				picked = list[_gpu.PickTag];
			}
			_picked = picked;
			Renderer.PickTag = -1;
			Renderer.PickDepth = _gpu.PickDepth;
			FramesShown++;
			_shown = _gpu.Projection;
			_eyeNow = _shown.View;
			return true;
		}

		private static float RoomFlicker(int i, bool fire)
		{
			float unscaledTime = Time.unscaledTime;
			if (!fire)
			{
				return 0.985f + 0.015f * Mathf.Sin(unscaledTime * 1.3f + (float)i * 2.1f);
			}
			float num = 0.55f * Mathf.Sin(unscaledTime * 2.1f + (float)i * 1.7f) + 0.3f * Mathf.Sin(unscaledTime * 3.4f + (float)i * 2.9f + 1f) + 0.15f * Mathf.Sin(unscaledTime * 6.1f + (float)i * 0.9f + 2f);
			return 0.94f + 0.06f * num;
		}

		internal string GpuRoomShadowsForTest()
		{
			if (_gpu == null)
			{
				return "no card";
			}
			return _gpu.RoomShadowsForTest();
		}

		private void ForgetRoom(Interior room)
		{
			foreach (MeshInstance mesh in room.Meshes)
			{
				MeshModel model = mesh.Model;
				if (model != null && (model.Name == null || Models.Get(model.Name) != model))
				{
					_gpu.Forget(model);
				}
			}
		}

		internal void TakeShipForTest(Actor ship)
		{
			_takeShip = ship;
		}

		internal void LeaveShipForTest()
		{
			_leaveShip = true;
		}

		private void HijackPrompt(bool helm)
		{
			if (helm && Hijacking)
			{
				if (Time.unscaledTime >= _shipShoreAt)
				{
					_shipShore = Boats.CanGoAshore(Hijack.Ship);
					_shipShoreAt = Time.unscaledTime + 0.6f;
				}
				string text = "Shift+E:" + (_shipShore ? "Go ashore" : "Jump overboard");
				Prompt = ((Prompt == null) ? ("W/S:Sails|A/D:Wheel|" + (WarMap.Armed(Hijack.Ship) ? "LMB:Fire|" : "") + text) : (Prompt + "|" + text));
				if (PlayInput.GetKeyDown(KeyCode.E) && ShiftHeld && !IsTyping() && !AnyMenu)
				{
					_leaveShip = true;
				}
			}
		}

		private void UpkeepHijack()
		{
			Hijack.KeepHomeless();
			Actor takeShip = _takeShip;
			bool leaveShip = _leaveShip;
			_takeShip = null;
			_leaveShip = false;
			if (takeShip != null && _active && DeathTimer < 0f)
			{
				TakeShip(takeShip);
				return;
			}
			if (_builtShip != null)
			{
				BuiltShipUpkeep();
			}
			if (!Hijack.Active)
			{
				return;
			}
			Actor ship = Hijack.Ship;
			Actor rider = Hijack.Rider;
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = ship.isAlive();
			}
			catch
			{
			}
			try
			{
				flag2 = rider.isAlive();
			}
			catch
			{
			}
			int landed2;
			if (!flag2)
			{
				Hijack.Forget();
				Log.Info("hijack: your body died aboard");
			}
			else if (!flag)
			{
				Hijack.PutOff(out var _, out var _, out landed2);
				if (!Possess.Swap(rider))
				{
					Log.Error("hijack: could not go back to your body after she sank");
					return;
				}
				Log.Info("hijack: the ship sank under you");
				if (_active)
				{
					DeathTimer = -1f;
					SwitchHost(rider);
					_pitchDegrees = 0f;
					_audio.PlayEffect("splash", 0.8f);
					Toast("She's sunk. You're in the sea");
				}
			}
			else if ((ControllableUnit.isControllingUnit() ? ControllableUnit._unit_main : null) != ship)
			{
				Hijack.PutOff(out var wet2, out landed2, out var aboard);
				if (aboard > 0)
				{
					Hijack.SendHome(ship);
				}
				Log.Info("hijack: left her another way, body put off " + (wet2 ? "into the sea" : "ashore"));
			}
			else if (leaveShip && _active && Hijacking)
			{
				LeaveShip();
			}
		}

		private void BuiltShipUpkeep()
		{
			Actor builtShip = _builtShip;
			if (Hijack.Active && Hijack.Ship == builtShip)
			{
				_builtShip = null;
				return;
			}
			_builtShipFor -= Time.unscaledDeltaTime;
			bool flag = false;
			try
			{
				flag = builtShip.isAlive();
			}
			catch
			{
			}
			if (flag && _builtShipFor > 0f && _active && _host == _builtShipBy)
			{
				_takeShip = builtShip;
				return;
			}
			_builtShip = null;
			try
			{
				if (flag)
				{
					builtShip.dieSimpleNone();
				}
			}
			catch
			{
			}
			try
			{
				if (_builtShipBy != null && _builtShipBy.isAlive())
				{
					_builtShipBy.addToInventory("wood", 15);
				}
			}
			catch
			{
			}
			Log.Info("raft: could not board the boat built; wood given back");
			if (_active)
			{
				Toast("You couldn't get aboard. The wood is back in your bag");
			}
		}

		private void TakeShip(Actor ship)
		{
			Actor host = _host;
			Vector2 vector = host?.current_position ?? Vector2.zero;
			if (ship == _builtShip)
			{
				try
				{
					vector = ship.current_position;
				}
				catch
				{
				}
			}
			if (!CanLeap || host == null || !Hijack.CanTake(host, ship, vector))
			{
				return;
			}
			ViewState view = _view;
			string said = null;
			if (!Hijack.Take(host, ship, delegate(string m)
			{
				said = m;
			}, out var realm, out var riders))
			{
				Toast("You can't get aboard");
				return;
			}
			if (!Possess.Swap(ship))
			{
				Hijack.PutOff(out var _, out var _, out var _);
				Toast("You can't take her");
				return;
			}
			Work.Reset();
			Talk.Reset(_scene.LookAt);
			Craft.Open = false;
			Royal.Open = false;
			Royal.ObeliskOpen = false;
			Holstered = false;
			SwitchHost(ship);
			_leapFrom = view;
			_leapT = 0f;
			_leapSeconds = 0.5f;
			_leaping = Settings.DiveSpeed > 0f;
			_audio.PlayDoor();
			string text = said ?? ((realm != null) ? ("You take the ship. She is " + realm + "'s now") : "You take the helm");
			if (riders > 0)
			{
				text = text + ". " + riders + ((riders == 1) ? " rides" : " ride") + " along";
			}
			Toast(text + " (Shift+E to get off)");
		}

		private void LeaveShip()
		{
			Actor ship = Hijack.Ship;
			Actor rider = Hijack.Rider;
			ViewState view = _view;
			bool wet;
			int landed;
			int aboard;
			Vector2 lastPos = Hijack.PutOff(out wet, out landed, out aboard);
			if (!Possess.Swap(rider))
			{
				Log.Error("hijack: could not go back to your body");
				return;
			}
			if (aboard > 0)
			{
				Hijack.SendHome(ship);
			}
			SwitchHost(rider);
			_pitchDegrees = 0f;
			_lastPos = lastPos;
			_leapFrom = view;
			_leapT = 0f;
			_leapSeconds = 0.45f;
			_leaping = Settings.DiveSpeed > 0f;
			if (wet)
			{
				_audio.PlayEffect("splash", 0.7f);
			}
			else
			{
				_audio.PlayDoor();
			}
			if (wet)
			{
				Toast((aboard > 0) ? ("Over the side. She takes " + aboard + " home") : "Over the side, into the sea");
			}
			else
			{
				Toast((landed > 0) ? ("Ashore with " + landed + " from the ship") : "Ashore");
			}
		}

		private void WireHomeDoors()
		{
			if (!_homeDoorsWired)
			{
				_homeDoorsWired = true;
				MayWalkInAt = HouseDoors.MayWalkIn;
				HouseLife.OpensFor = (Building b, Actor a) => HouseInterior.WalksIn(b.asset) && HouseDoors.MayWalkIn(b, a) && HouseInterior.Barred(b, Body ?? a) == null;
				HouseLife.HeldOpen = (Building b) => b == _doorwayNear || HouseInterior.Propped(b);
			}
		}

		private bool HouseDoorPrompt(Actor host, Building house)
		{
			WireHomeDoors();
			if (Rooms.CanEnter(house) || HouseInterior.RoomName(house.asset) == null)
			{
				return false;
			}
			bool flag = HouseInterior.WalksIn(house.asset) && HouseWorld.PlanOf(house) != null;
			Actor you = Body ?? host;
			bool flag2 = HomeCard.Yours(house, you) || Workbench.HomeOf(you) == house;
			if (flag && HouseInterior.Propped(house) && (flag2 || HouseDoors.MayWalkIn(house, you)))
			{
				bool flag3 = PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift);
				Prompt = "E:Close the door" + (flag2 ? "|Shift+E:Home" : "");
				if (PlayInput.GetKeyDown(KeyCode.E))
				{
					if (flag2 && flag3)
					{
						HomeCard.Show(house, you);
					}
					else
					{
						HouseInterior.Unprop(house);
						_audio.PlayEffect("door", 0.5f);
					}
				}
				return true;
			}
			if (flag2)
			{
				bool flag4 = PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift);
				Prompt = (flag ? "E:Home" : "E:Enter|Shift+E:Home");
				if (PlayInput.GetKeyDown(KeyCode.E))
				{
					if (HomeCard.Open)
					{
						HomeCard.Close();
					}
					else if (flag || flag4)
					{
						HomeCard.Show(house, you);
					}
					else
					{
						EnterHouse(house);
					}
				}
				return true;
			}
			if (HouseDoors.MayWalkIn(house, you))
			{
				Prompt = "E:Enter";
				if (PlayInput.GetKeyDown(KeyCode.E))
				{
					string text = HouseInterior.Barred(house, you);
					if (text != null)
					{
						Toast(text);
						_audio.PlayDoor();
					}
					else
					{
						EnterHouse(house);
					}
				}
				return true;
			}
			Prompt = "Locked";
			return true;
		}

		private bool RoomDoorPrompt(Actor host)
		{
			if (_roomDoorFrame != Time.frameCount - 1)
			{
				_roomDoorKey = -1f;
			}
			_roomDoorFrame = Time.frameCount;
			if (_roomDoorKey >= 0f)
			{
				if (!PlayInput.GetKey(KeyCode.E) || !Interior.Inside)
				{
					_roomDoorKey = -1f;
				}
				else if (Time.unscaledTime - _roomDoorKey >= 0.45f)
				{
					_roomDoorKey = -1f;
					if (HouseInterior.Propped(Interior.House) != _roomDoorPropped)
					{
						Interior.ToggleDoor();
					}
					ExitHouse();
					return true;
				}
			}
			if (!Interior.DoorAimed(_view))
			{
				if (_roomDoorKey >= 0f && Interior.Fitted)
				{
					return KeepDoorPrompt();
				}
				return false;
			}
			Building house = Interior.House;
			Actor you = Body ?? host;
			if (!Interior.Fitted)
			{
				Prompt = "E:Leave";
				if (PlayInput.GetKeyDown(KeyCode.E))
				{
					ExitHouse();
				}
				return true;
			}
			bool flag = house != null && (HomeCard.Yours(house, you) || Workbench.HomeOf(you) == house);
			bool flag2 = PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift);
			Prompt = (Interior.DoorHeldOpen ? "E:Close the door" : "E:Open the door") + "|Hold E:Leave" + (flag ? "|Shift+E:Home" : "");
			if (PlayInput.GetKeyDown(KeyCode.E))
			{
				if (flag && flag2)
				{
					HomeCard.Show(house, you);
					return true;
				}
				_roomDoorPropped = HouseInterior.Propped(house);
				_roomDoorKey = Time.unscaledTime;
				Interior.ToggleDoor();
				_audio.PlayEffect("door", 0.5f);
			}
			return true;
		}

		private bool KeepDoorPrompt()
		{
			Prompt = "Hold E:Leave";
			return true;
		}

		private void HotbarKeys()
		{
			_hotbarFood = null;
			Actor host = _host;
			bool flag = false;
			try
			{
				flag = host?.isAlive() ?? false;
			}
			catch
			{
			}
			if (!flag || Mounted || DeathTimer >= 0f)
			{
				return;
			}
			Supplies.Own(host);
			bool flag2 = !AnyMenu && !IsTyping() && !Hub.AnyModeActive && !PlayerTalk.CardOpen && !WheelClick;
			if (flag2)
			{
				int num = Hotbar.Size(host);
				for (int i = 0; i < num; i++)
				{
					if (PlayInput.GetKeyDown((KeyCode)(49 + i)) || PlayInput.GetKeyDown((KeyCode)(257 + i)) || (HotbarKeyForTest == i && HotbarKeyFrameForTest == Time.frameCount))
					{
						ChooseSlot(host, i, wheel: false);
						break;
					}
				}
			}
			try
			{
				Hotbar.Sync(host, Toast);
			}
			catch (Exception ex)
			{
				Log.Error("hotbar: " + ex.Message);
			}
			_hotbarFood = Hotbar.Food(host);
			bool flag3 = PlayInput.GetMouseButtonDown(0) || HotbarClickForTest == Time.frameCount;
			Supplies.Kind kind = Hotbar.Supply(host);
			if (kind != null && kind.Id == "tw_item_bedroll")
			{
				if (flag2 && flag3 && !ClickGrace && Work.Target == null)
				{
					SleepOnBedroll(host);
				}
				return;
			}
			if (!Supplies.Heals(kind))
			{
				kind = null;
			}
			Actor actor = ((kind != null) ? Bandages.Target(host) : null);
			if (kind != null && Bandages.Pending(actor) < MissingHealth(actor))
			{
				_hotbarFood = kind.Id;
				if (flag2 && flag3 && !ClickGrace && Work.Target == null)
				{
					Bandages.Use(host, kind, actor, Toast);
				}
			}
			else if (_hotbarFood != null && flag2 && flag3 && !ClickGrace && Work.Target == null && !AimsAtCarcass(host))
			{
				Carried carried = Held.Item(host);
				int num2 = Hotbar.Chosen(host);
				int j = BagOrder.TakeFrom(host, num2, 1, Bag.Contents(host));
				if (!Eating.EatFromBag(host, _hotbarFood, Toast))
				{
					BagOrder.Swap(host, num2, j);
				}
				else if (carried != null && carried.Edible)
				{
					_eatAt = Time.unscaledTime;
					_eaten = carried;
				}
			}
		}

		private static bool AimsAtCarcass(Actor host)
		{
			if (host == null || RoomCombat.Active || !Butchery.CanCut(host))
			{
				return false;
			}
			Vec3 eye = FirstPersonAim.Eye;
			return Butchery.Aimed(new Vector2(eye.X, eye.Y), FirstPersonAim.Flat, 2.3f) != null;
		}

		private bool EatingNow(out Carried eaten)
		{
			eaten = _eaten;
			if (_eaten != null && Time.unscaledTime - _eatAt < 1.15f)
			{
				return !Holstered;
			}
			return false;
		}

		private void HeldItemHands(Carried item, MeshModel model, ref HandsInput input)
		{
			int num;
			object hostCarried;
			if (item != null && model != null)
			{
				num = ((input.HeldModel == model) ? 1 : 0);
				if (num != 0)
				{
					hostCarried = item;
					goto IL_0019;
				}
			}
			else
			{
				num = 0;
			}
			hostCarried = null;
			goto IL_0019;
			IL_0019:
			People.HostCarried = (Carried)hostCarried;
			if (num != 0)
			{
				input.HeldItem = true;
				input.HeldPalm = item.Palm;
				input.HeldDrink = item.Drink;
				input.HeldMouth = item.Mouth;
				input.EatAge = ((_eaten == item && Time.unscaledTime - _eatAt < 1.15f) ? (Time.unscaledTime - _eatAt) : (-1f));
			}
		}

		private void HotbarWheel(float scroll)
		{
			if (_host != null && !Mounted && !(DeathTimer >= 0f) && Hotbar.Size(_host) > 1)
			{
				int num = Hotbar.Chosen(_host);
				Hotbar.Step(_host, (scroll < 0f) ? 1 : (-1));
				if (Hotbar.Chosen(_host) != num)
				{
					Holstered = false;
				}
			}
		}

		private void ChooseSlot(Actor host, int i, bool wheel)
		{
			if (Hotbar.Chosen(host) != i)
			{
				Holstered = false;
			}
			else if (!wheel && (Holstered || !string.IsNullOrEmpty(Hotbar.InHand(host))))
			{
				Holstered = !Holstered;
				if (Holstered)
				{
					Toast("You put away what you were holding   (" + (i + 1) + " takes it up)");
				}
			}
			Hotbar.Choose(host, i, wheel);
		}

		internal void OpenObeliskForTest(Building stone)
		{
			_obelisk = stone;
			Royal.ObeliskOpen = stone != null;
		}

		internal static bool InventoryPressed()
		{
			if (!PlayInput.GetKeyDown(KeyCode.Tab))
			{
				return PressForTest == Time.frameCount;
			}
			return true;
		}

		private static bool InventoryHeld()
		{
			if (!PlayInput.GetKey(KeyCode.Tab))
			{
				return HoldForTest;
			}
			return true;
		}

		private void InventoryQoLKeys(bool menu)
		{
			if (menu || IsTyping() || _host == null) return;
			if (PlayInput.GetKeyDown(KeyCode.H))
			{
				InventoryQoL.TryQuickHeal(_host, Toast);
			}
			if (PlayInput.GetKeyDown(KeyCode.R) && !Craft.Open && !AnyMenu)
			{
				InventoryQoL.SortBag(_host, Toast);
			}
		}

		private void InventoryKeys()
		{
			float unscaledTime = Time.unscaledTime;
			if (Mounted)
			{
				if (Craft.Open)
				{
					Craft.Open = false;
				}
				return;
			}
			if (InventoryPressed() && !IsTyping() && DeathTimer < 0f && !SettingsTakeTab)
			{
				if (Craft.Open && !QuickInventory)
				{
					Craft.Open = false;
					_audio.PlayRustle();
				}
				else if (!Craft.Open)
				{
					Royal.Open = false;
					Royal.ObeliskOpen = false;
					Craft.Toggle();
					QuickInventory = true;
					_tabDownAt = unscaledTime;
					_tabDownFrame = Time.frameCount;
					_tabHeld = true;
					_audio.PlayRustle();
				}
			}
			if (_tabHeld && !InventoryHeld() && PressForTest != Time.frameCount)
			{
				_tabHeld = false;
				if (Craft.Open && QuickInventory)
				{
					if (unscaledTime - _tabDownAt < 0.25f || Time.frameCount - _tabDownFrame <= 2)
					{
						QuickInventory = false;
					}
					else
					{
						Craft.Open = false;
					}
				}
			}
			if (!Craft.Open)
			{
				QuickInventory = false;
				_tabHeld = false;
			}
		}

		internal void LookForTest(float yaw, float pitchDegrees)
		{
			_view.Yaw = yaw;
			_pitchDegrees = pitchDegrees;
		}

		internal bool DrawYou(int w, int h)
		{
			return YouView.Draw(_host, _sprites, _view.Yaw, w, h, Settings != null && Settings.Palette >= 1);
		}

		internal bool PingFromJournal(PingKind kind)
		{
			if (!CaptureAim())
			{
				Toast("Nothing there to ping");
				return false;
			}
			return PlacePing(kind);
		}

		internal void MarkFromMap(Vector2 at, Ping already)
		{
			if (_host == null)
			{
				return;
			}
			if (already != null)
			{
				Pings.Remove(already);
				Toast(Pings.Name(already.Kind) + " ping taken off");
				return;
			}
			float z = 0f;
			try
			{
				z = GroundAt(at);
			}
			catch
			{
			}
			Pings.Add(PingKind.Go, new Vector3(at.x, at.y, z), null, null, far: true);
			Sounds.PlayUI("event:/SFX/Ui/Click");
			Toast("Marked on your map and compass: " + Service.Heading(_host.current_position, at));
		}

		internal void AbandonFromJournal(Quest q)
		{
			if (q != null && _host != null)
			{
				Quests.Abandon(q, _host);
				Journal.ConfirmGiver = -1L;
				Toast("You gave up: " + Quests.Title(q));
				Log.Info("journal: gave up " + q.Kind.ToString() + " for " + q.GiverName);
			}
		}

		private void ReleaseStrandedKeys()
		{
			float unscaledTime = Time.unscaledTime;
			if (_active || (!Converse.Open && !Law.BoardOpen && Births.Naming == null && !Hub.AnyWindowOpen))
			{
				_strandedSince = -1f;
			}
			else if (_strandedSince < 0f)
			{
				_strandedSince = unscaledTime;
			}
			else if (unscaledTime - _strandedSince > 0.5f)
			{
				_strandedSince = -1f;
				Log.Info("keys: closed a window left open outside first person (talk " + Converse.Open + ", board " + Law.BoardOpen + ", naming " + (Births.Naming != null) + ", towns " + Hub.AnyWindowOpen + ")");
				if (Converse.Open)
				{
					EndConversation();
				}
				Law.BoardOpen = false;
				Births.Naming = null;
				Hub.CloseAll();
				HomeCard.Close();
			}
			if (!_active || _dive != Dive.None || !(DeathTimer < 0f) || !PossessionHooks.Frozen || Interior.Inside || Cabin.Inside || Mine.Inside || Converse.Open || _frozeForTalk)
			{
				_strayFrozenSince = -1f;
			}
			else if (_strayFrozenSince < 0f)
			{
				_strayFrozenSince = unscaledTime;
			}
			else if (unscaledTime - _strayFrozenSince > 1.5f)
			{
				_strayFrozenSince = -1f;
				PossessionHooks.Frozen = false;
				Log.Info("keys: body held still with nothing holding it, let go");
			}
			try
			{
				if (Application.isFocused && !World.world.has_focus)
				{
					World.world.has_focus = true;
					Log.Info("keys: the game's focus flag was off with the window focused, set back");
				}
			}
			catch
			{
			}
			WatchKeys();
		}

		private void KeysShown(int which)
		{
			_keysShownFrame[which] = Time.frameCount;
		}

		private bool Unshown(bool open, int which)
		{
			if (open)
			{
				return Time.frameCount - _keysShownFrame[which] > 2;
			}
			return false;
		}

		internal string KeyHolders()
		{
			StringBuilder b = new StringBuilder();
			try
			{
				Add(Converse.Open, "a talk");
				Add(Law.BoardOpen, (Law.OpenBoard == null) ? "a board (none to show)" : "a board");
				Add(Births.Naming != null, "the name box");
				Add(ChoosingHeir, "the heirs");
				Add(Planning.Open, "planning");
				Add(Chest.Open, "a chest");
				Add(HomeCard.Open, "a home card");
				Add(Ledger.Open, "a ledger");
				Add(MpUi.Typing, "the multiplayer chat");
				Add(Time.frameCount - Hud.FieldFocusFrame <= 1, "a text box of ours");
				InputField inputField = FocusedField();
				Add(inputField != null, "the game's text box " + PathOf((inputField != null) ? inputField.transform : null));
			}
			catch (Exception ex)
			{
				Add(on: true, "? " + ex.GetType().Name);
			}
			return b.ToString();
			void Add(bool on, string what)
			{
				if (on)
				{
					b.Append((b.Length > 0) ? ", " : "").Append(what);
				}
			}
		}

		private static InputField FocusedField()
		{
			GameObject gameObject = ((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject : null);
			if (gameObject == null)
			{
				return null;
			}
			InputField component = gameObject.GetComponent<InputField>();
			if (!(component != null) || !component.isFocused)
			{
				return null;
			}
			return component;
		}

		private static string PathOf(Transform t)
		{
			string text = "";
			int num = 0;
			while (t != null && num < 4)
			{
				text = t.name + ((text.Length > 0) ? ("/" + text) : "");
				num++;
				t = t.parent;
			}
			return text;
		}

		internal void ReleaseKeyHolds(string why)
		{
			string text = KeyHolders();
			Log.Error("keys: " + why + ": let go of " + ((text.Length > 0) ? text : "nothing held") + $" (first person {_active}, scene {Scene.On}, death {DeathTimer >= 0f}, picture {ShownFrame != null}, cursor {Cursor.lockState})");
			try
			{
				if (Converse.Open)
				{
					EndConversation();
				}
			}
			catch (Exception ex)
			{
				Log.Error("keys: the talk: " + ex.Message);
			}
			Law.BoardOpen = false;
			try
			{
				if (Births.Naming != null)
				{
					if (_host != null)
					{
						Births.FinishNaming(_host, keep: true);
					}
					else
					{
						Births.Naming = null;
					}
				}
			}
			catch
			{
				Births.Naming = null;
			}
			try
			{
				Hub.CloseAll();
			}
			catch (Exception ex2)
			{
				Log.Error("keys: the Towns windows: " + ex2.Message);
			}
			HomeCard.Close();
			Planning.Open = false;
			Chest.Open = false;
			Ledger.Open = false;
			MpUi.ChatOpen = false;
			MpUi.Typing = false;
			Hud.FieldFocusFrame = -10;
			DropGameField();
			_dropKeyboardControl = true;
			PlayInput.HeldBack = (PlayInput.HeldBack2 = KeyCode.None);
			try
			{
				if (Application.isFocused && World.world != null)
				{
					World.world.has_focus = true;
				}
			}
			catch
			{
			}
			_keysHeldSince = (_fieldSince = -1f);
			_escCount = 0;
		}

		private static void DropGameField()
		{
			try
			{
				InputField inputField = FocusedField();
				if (inputField != null)
				{
					inputField.DeactivateInputField();
				}
				if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
				{
					EventSystem.current.SetSelectedGameObject(null);
				}
			}
			catch (Exception ex)
			{
				Log.Error("keys: the game's text box: " + ex.Message);
			}
		}

		private void DropKeyboardControlIfAsked()
		{
			if (_dropKeyboardControl)
			{
				_dropKeyboardControl = false;
				GUIUtility.keyboardControl = 0;
			}
		}

		private void WatchKeys()
		{
			float unscaledTime = Time.unscaledTime;
			if (!_active)
			{
				_keysHeldSince = (_fieldSince = (_heldBackSince = (_heldBack2Since = -1f)));
				_escCount = 0;
				return;
			}
			if (PlayInput.RawKeyDown(KeyCode.Escape) && !PadControls.Down(KeyCode.Escape) && !VrControls.Down(KeyCode.Escape))
			{
				bool flag = FocusedField() != null;
				try
				{
					flag |= Application.isFocused && !World.world.has_focus;
				}
				catch
				{
				}
				bool flag2 = false;
				try
				{
					flag2 = HotkeyAsset.isHoldingAnyModKey();
				}
				catch
				{
				}
				if (flag || flag2)
				{
					if (flag || _modifierEscLogs++ < 3)
					{
						Log.Error("keys: Esc pressed while the game's own keys are deaf (focus flag " + World.world.has_focus + ", a modifier held " + flag2 + ", " + KeyHolders() + "): taken by the mod");
					}
					DropGameField();
					try
					{
						if (Application.isFocused)
						{
							World.world.has_focus = true;
						}
					}
					catch
					{
					}
					try
					{
						EscapePressed(HotkeyLibrary.cancel);
					}
					catch (Exception ex)
					{
						Log.Error("keys: Esc: " + ex.Message);
					}
				}
				string escHolders = KeyHolders();
				if (unscaledTime - _escAt > 1.5f)
				{
					_escCount = 0;
				}
				_escAt = unscaledTime;
				_escCount++;
				if (_escCount == 1)
				{
					_escHolders = escHolders;
				}
			}
			else if (_escCount > 0 && unscaledTime - _escAt > 0.15f && unscaledTime - _escAt < 1.5f)
			{
				if (_escCount >= 2 && IsTyping() && KeyHolders() == _escHolders)
				{
					ReleaseKeyHolds("Esc twice, and still held");
				}
				if (_escCount >= 2)
				{
					_escCount = 0;
				}
			}
			if (!Unshown(Converse.Open, 0) && !Unshown(Law.BoardOpen, 1) && !Unshown(Births.Naming != null, 2) && !Unshown(Hub.AnyWindowOpen, 3))
			{
				_keysHeldSince = -1f;
			}
			else if (_keysHeldSince < 0f)
			{
				_keysHeldSince = unscaledTime;
			}
			else if (unscaledTime - _keysHeldSince > 2f)
			{
				ReleaseKeyHolds("held 2 s with nothing on screen to type in or close");
			}
			if (FocusedField() == null)
			{
				_fieldSince = -1f;
			}
			else if (_fieldSince < 0f)
			{
				_fieldSince = unscaledTime;
			}
			else if (unscaledTime - _fieldSince > 1f)
			{
				ReleaseKeyHolds("a text box of the game's had the keyboard under the first-person view");
			}
			if (Application.isFocused)
			{
				_unfocusedSince = -1f;
			}
			else if (_unfocusedSince < 0f)
			{
				_unfocusedSince = unscaledTime;
			}
			else if (unscaledTime - _unfocusedSince > 2f && !_unfocusedTold && _unfocusedLogs < 3)
			{
				_unfocusedTold = true;
				_unfocusedLogs++;
				Log.Error("keys: the game's window has lost the keyboard to another window for 2 s in first person (cursor " + Cursor.lockState.ToString() + "): no key reaches the game until it is clicked or Alt+Tabbed back");
			}
			if (Application.isFocused)
			{
				_unfocusedTold = false;
			}
			WatchHeldBack(ref PlayInput.HeldBack, ref _heldBackSince, unscaledTime);
			WatchHeldBack(ref PlayInput.HeldBack2, ref _heldBack2Since, unscaledTime);
		}

		private static void WatchHeldBack(ref KeyCode key, ref float since, float now)
		{
			if (key == KeyCode.None || PlayInput.RawKey(key))
			{
				since = -1f;
			}
			else if (since < 0f)
			{
				since = now;
			}
			else if (!(now - since < 1f))
			{
				Log.Error("keys: " + key.ToString() + " held back by a wheel 1 s after it came up: given back");
				key = KeyCode.None;
				since = -1f;
			}
		}

		private bool TakeLandBeside()
		{
			bool landBesideDone = _landBesideDone;
			_landBesideDone = false;
			return landBesideDone;
		}

		private void StartLandBeside(Vector2 eye)
		{
			if (_landBeside != null)
			{
				return;
			}
			WorldSampler sampler = _sampler;
			float ex = eye.x;
			float ey = eye.y;
			_landBeside = Task.Run(delegate
			{
				long timestamp = Stopwatch.GetTimestamp();
				try
				{
					sampler.TickTiles(ex, ey, eye: true);
				}
				catch (Exception landBesideError)
				{
					_landBesideError = landBesideError;
				}
				_landBesideMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			});
		}

		private void FinishLandBeside()
		{
			if (_landBeside == null)
			{
				return;
			}
			Task landBeside = _landBeside;
			_landBeside = null;
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				landBeside.Wait();
			}
			catch (Exception landBesideError)
			{
				_landBesideError = landBesideError;
			}
			_landBesideWaitMs = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			_landBesideDone = _landBesideError == null;
			if (_landBesideError != null)
			{
				if (!_landBesideErrorLogged)
				{
					Log.Error("land: the refresh beside the game failed (it carries on in step): " + _landBesideError);
				}
				_landBesideErrorLogged = true;
				_landBesideError = null;
				LandBesideOff = true;
			}
		}

		internal static bool TakeOverPressed()
		{
			if (!PlayInput.GetMouseButtonDown(2) && !PlayInput.GetKeyDown(KeyCode.Equals))
			{
				return PlayInput.GetKeyDown(KeyCode.KeypadEquals);
			}
			return true;
		}

		private void TryLeap()
		{
			if (Interior.Inside)
			{
				Toast("Step outside first");
			}
			else if (!(_picked is Actor actor) || !actor.isAlive())
			{
				Toast("Look at a creature and middle click or press = to take it over");
			}
			else
			{
				TryLeapInto(actor);
			}
		}

		private void TryLeapInto(Actor target)
		{
			if (Interior.Inside)
			{
				Toast("Step outside first");
			}
			else if (target != null && target.isAlive())
			{
				if (!Possess.CanJumpTo(target))
				{
					Toast("You can't take over " + Describe(target));
				}
				else if (!RunHoldsLeap(target))
				{
					Possess.JumpTo(target);
				}
			}
		}

		internal bool LeapInto(Actor target)
		{
			if (!CanLeap)
			{
				return false;
			}
			ViewState view = _view;
			Vector2 vector = ((_host != null) ? _host.current_position : new Vector2(view.X, view.Y));
			if (!Possess.Swap(target))
			{
				return false;
			}
			Work.Reset();
			Talk.Reset(_scene.LookAt);
			Craft.Open = false;
			Royal.Open = false;
			Royal.ObeliskOpen = false;
			Holstered = false;
			SwitchHost(target);
			float magnitude = (target.current_position - vector).magnitude;
			_leapFrom = view;
			_leapT = 0f;
			_leapSeconds = Mathf.Clamp(0.3f + magnitude * 0.018f, 0.35f, 0.9f);
			_leaping = Settings.DiveSpeed > 0f;
			_hands.Lower();
			_audio.PlayLeap();
			Toast("You take over " + Describe(target));
			Log.Info("leapt into: " + Describe(target) + $" ({magnitude:F1} tiles)");
			return true;
		}

		private ViewState LeapCamera(ViewState eye, float body, float dt, out float bodyFade, out bool handsUp)
		{
			bodyFade = 0f;
			handsUp = true;
			if (!_leaping)
			{
				return eye;
			}
			_leapT += dt / Mathf.Max(0.1f, _leapSeconds);
			if (_leapT >= 1f)
			{
				_leaping = false;
				return eye;
			}
			float num = Mathf.SmoothStep(0f, 1f, _leapT);
			ViewState result = eye;
			float num2 = eye.X - _leapFrom.X;
			float num3 = eye.Y - _leapFrom.Y;
			float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
			result.X = _leapFrom.X + num2 * num;
			result.Y = _leapFrom.Y + num3 * num;
			result.Z = Mathf.Lerp(_leapFrom.Z, eye.Z, num) + Mathf.Sin(num * MathF.PI) * Mathf.Min(6f, 0.6f + num4 * 0.25f);
			result.Pitch = Mathf.Tan(Mathf.Lerp(Mathf.Atan(_leapFrom.Pitch), Mathf.Atan(eye.Pitch), num));
			result.Fov = eye.Fov + Mathf.Sin(num * MathF.PI) * 18f * (MathF.PI / 180f);
			float num5 = Mathf.Sqrt((eye.X - result.X) * (eye.X - result.X) + (eye.Y - result.Y) * (eye.Y - result.Y) + (eye.Z - result.Z) * (eye.Z - result.Z));
			bodyFade = BodyFade(num5, body);
			handsUp = num5 < 0.4f * body;
			return result;
		}

		private void LightUpkeep()
		{
			LightMode.Tick(this);
			if (LightMode.On && !Settings.LightTold && Device.OnPhone && _active && _dive == Dive.None && !AnyMenu && !(Time.unscaledTime < ToastUntil))
			{
				Settings.LightTold = true;
				_settingsDirty = true;
				Toast("Light mode is on for this device. F2 > Graphics to change");
				ToastUntil = Time.unscaledTime + 6f;
			}
		}

		internal Texture2D PortraitOf(Actor a)
		{
			return Portraits.For(a, _sprites, Settings != null && Settings.Palette >= 1);
		}

		private bool DoorAhead(Vector2 pos)
		{
			if (_pitchDegrees > -20f && _picked is Building b && HouseInterior.IsEnterable(b))
			{
				return DistanceToBuilding(b, pos) < 2.2f;
			}
			return false;
		}

		private bool LootPrompt(Actor host, Vector2 pos)
		{
			Loot.Fallen fallen = Loot.Near(pos, _view.Yaw, 2.2f);
			if (fallen == null)
			{
				return CarcassPrompt(Body ?? host, pos);
			}
			Prompt = "E:Search " + fallen.Name + "'s body";
			if (PlayInput.GetKeyDown(KeyCode.E))
			{
				Loot.Take(Body ?? host, fallen, Toast);
			}
			return true;
		}

		private bool CarcassPrompt(Actor you, Vector2 pos)
		{
			if (!Butchery.CanCut(you))
			{
				return false;
			}
			Butchery.Carcass carcass = Butchery.Aimed(pos, new Vector2(Mathf.Cos(_view.Yaw), Mathf.Sin(_view.Yaw)), 2.3f);
			if (carcass == null)
			{
				return false;
			}
			bool flag = false;
			try
			{
				flag = you.hasMeleeAttack();
			}
			catch
			{
			}
			Prompt = (flag ? Butchery.PromptFor(carcass) : ("LMB:Strike the " + carcass.Name + " to butcher it (not with a bow)"));
			return true;
		}

		private void HoldMapDrawing(bool covered)
		{
			MapBox mapBox = (Config.game_loaded ? World.world : null);
			WorldTimeScaleAsset time_scale_asset = Config.time_scale_asset;
			if (covered && !MapHoldOff && mapBox != null && time_scale_asset != null)
			{
				try
				{
					if (!_renderSkipWas.ContainsKey(time_scale_asset))
					{
						_renderSkipWas[time_scale_asset] = time_scale_asset.render_skip;
					}
					time_scale_asset.render_skip = true;
					if (mapBox._render_skip >= 1)
					{
						mapBox.calculateVisibleObjects();
					}
					mapBox._render_skip = 0;
					_mapHeld = true;
					return;
				}
				catch (Exception ex)
				{
					Log.Error("map hold: let go: " + ex.Message);
					MapHoldOff = true;
					LetGoOfMap();
					return;
				}
			}
			if (_mapHeld)
			{
				LetGoOfMap();
			}
		}

		private void LetGoOfMap()
		{
			foreach (KeyValuePair<WorldTimeScaleAsset, bool> renderSkipWa in _renderSkipWas)
			{
				if (renderSkipWa.Key != null)
				{
					renderSkipWa.Key.render_skip = renderSkipWa.Value;
				}
			}
			_renderSkipWas.Clear();
			_mapHeld = false;
		}

		internal bool HasMapPage(MapTabs.Page page)
		{
			Actor actor = Body ?? _host;
			if (actor == null)
			{
				return false;
			}
			switch (page)
			{
			case MapTabs.Page.War:
				return Royal.IsKing(actor);
			case MapTabs.Page.Orders:
				if (!Service.IsSoldier(actor))
				{
					return Service.CommissionOf(actor) != null;
				}
				return true;
			case MapTabs.Page.Council:
				return Royal.IsKing(actor);
			default:
				return true;
			}
		}

		private int MapPagesHad()
		{
			int num = 0;
			MapTabs.Page[] all = MapTabs.All;
			foreach (MapTabs.Page page in all)
			{
				if (HasMapPage(page))
				{
					num |= 1 << (int)page;
				}
			}
			return num;
		}

		private void MapKey()
		{
			if (MapWindowOpen)
			{
				CloseMapWindow();
				return;
			}
			if (WorldNews.Banner != null && Time.unscaledTime < WorldNews.BannerUntil)
			{
				ShowMapPage(MapTabs.Page.News);
				return;
			}
			MapTabs.Page? last = MapTabs.Last;
			if (last.HasValue && MapTabs.LastAmong == MapPagesHad() && HasMapPage(last.Value))
			{
				ShowMapPage(last.Value);
			}
			else
			{
				ShowMapPage(HasMapPage(MapTabs.Page.War) ? MapTabs.Page.War : (HasMapPage(MapTabs.Page.Orders) ? MapTabs.Page.Orders : MapTabs.Page.Map));
			}
		}

		internal void MapKeyForTest()
		{
			MapKey();
		}

		internal void CloseMapWindow()
		{
			Journal.Open = false;
			WarMap.Close();
			SoldierMap.Close();
			FamilyMap.Close();
			if (WorldNews.Open)
			{
				WorldNews.Toggle();
			}
			Royal.Open = false;
			_mapPageWanted = null;
		}

		internal void WantMapPage(MapTabs.Page page)
		{
			_mapPageWanted = page;
		}

		internal void ShowMapPage(MapTabs.Page page)
		{
			if (_host == null || !HasMapPage(page))
			{
				return;
			}
			bool mapWindowOpen = MapWindowOpen;
			if (page != MapTabs.Page.Map)
			{
				Journal.Open = false;
			}
			if (page != MapTabs.Page.War)
			{
				WarMap.Close();
			}
			if (page != MapTabs.Page.Orders)
			{
				SoldierMap.Close();
			}
			if (page != MapTabs.Page.Family)
			{
				FamilyMap.Close();
			}
			if (page != MapTabs.Page.News && WorldNews.Open)
			{
				WorldNews.Toggle();
			}
			if (page != MapTabs.Page.Council)
			{
				Royal.Open = false;
			}
			switch (page)
			{
			case MapTabs.Page.Map:
				if (!Journal.Open)
				{
					Journal.Toggle();
					Journal.Fit = true;
				}
				break;
			case MapTabs.Page.War:
				if (!WarMap.Open)
				{
					WarMap.Toggle(Body ?? _host, Toast);
				}
				break;
			case MapTabs.Page.Orders:
				if (!SoldierMap.Open)
				{
					SoldierMap.Toggle(Body ?? _host, Toast);
				}
				break;
			case MapTabs.Page.Family:
				if (!FamilyMap.Open)
				{
					FamilyMap.Toggle(Body ?? _host);
				}
				break;
			case MapTabs.Page.News:
				if (!WorldNews.Open)
				{
					WorldNews.Toggle();
				}
				break;
			case MapTabs.Page.Council:
				if (!Royal.Open)
				{
					Royal.Toggle(Body ?? _host, Toast);
				}
				break;
			}
			if (MapWindowOpen)
			{
				MapTabs.Last = page;
				MapTabs.LastAmong = MapPagesHad();
				Craft.Open = false;
				Royal.ObeliskOpen = false;
				Law.BoardOpen = false;
				if (!mapWindowOpen)
				{
					_audio.PlayRustle();
				}
			}
		}

		private void MapWindowKeys()
		{
			MapTabs.Page mapPageShown = MapPageShown;
			if (_lastPageShown.HasValue && MapWindowOpen && mapPageShown != _lastPageShown.Value && PagesOpen() > 1)
			{
				ShowMapPage(mapPageShown);
			}
			_lastPageShown = (MapWindowOpen ? new MapTabs.Page?(MapPageShown) : ((MapTabs.Page?)null));
			if (_mapPageWanted.HasValue)
			{
				MapTabs.Page value = _mapPageWanted.Value;
				_mapPageWanted = null;
				if (MapWindowOpen)
				{
					ShowMapPage(value);
				}
			}
			if ((Journal.Open || WorldNews.Open) && (Craft.Open || Royal.ObeliskOpen || _settingsMenu.Open || Converse.Open || Law.BoardOpen || ChoosingHeir || DeathTimer >= 0f))
			{
				Journal.Open = false;
				if (WorldNews.Open)
				{
					WorldNews.Toggle();
				}
			}
		}

		private int PagesOpen()
		{
			return (Journal.Open ? 1 : 0) + (WarMap.Open ? 1 : 0) + (SoldierMap.Open ? 1 : 0) + (FamilyMap.Open ? 1 : 0) + (WorldNews.Open ? 1 : 0) + (Royal.Open ? 1 : 0);
		}

		private bool MinePrompt(Actor host, Vector2 pos)
		{
			if (!(_picked is Building building) || !MineInterior.IsEnterable(building) || MineInterior.MouthDistance(building, pos) > 2.2f)
			{
				return false;
			}
			Prompt = "E:Go into the mine";
			if (PlayInput.GetKeyDown(KeyCode.E) && !IsTyping())
			{
				EnterMine(building);
			}
			return true;
		}

		internal void EnterMine(Building mine)
		{
			if (_host != null && mine != null)
			{
				ExitHouse();
				if (Mine.Enter(mine, _host))
				{
					Talk.Reset(_scene.LookAt);
					Work.Reset();
					_audio.PlayDoor();
					_outsideYaw = _view.Yaw;
					_view.Yaw = Mine.Room.SpawnYaw;
					PossessionHooks.Frozen = true;
					Toast("Into the mine   (E at the mouth to go back out)");
				}
			}
		}

		private void LeaveMine(string why = null)
		{
			if (Mine.Inside)
			{
				Talk.Reset(_scene.LookAt);
				Mine.Exit();
				PossessionHooks.Frozen = false;
				_view.Yaw = -MathF.PI / 2f;
				_eyeZ = 0f;
				_audio.PlayDoor();
				if (why != null)
				{
					Toast(why);
				}
			}
		}

		internal void LeaveMineForTest()
		{
			LeaveMine();
		}

		private void LeaveRooms()
		{
			LeaveCabin();
			LeaveMine();
		}

		private void RenderMine(float dt)
		{
			if (!Mine.Standing())
			{
				LeaveMine("The mine has fallen in!");
				return;
			}
			Mine.KeepIndoors();
			Actor host = _host;
			RenderRoom(Mine, 4, 0.05f, dt);
			Prompt = null;
			if (Talk.Candidate != null)
			{
				Prompt = TalkPrompt(host, Talk.Candidate);
			}
			else if (((!(_pitchDegrees < -20f) && Mine.NearDoor) || !RoomLootPrompt(host)) && Mine.NearDoor && Mine.Inside)
			{
				Prompt = "E:Go back out";
				if (PlayInput.GetKeyDown(KeyCode.E) && !IsTyping())
				{
					LeaveMine();
				}
			}
			if (DebugPrompt != null)
			{
				Prompt = DebugPrompt;
			}
		}

		private bool StayOffOnOldGame()
		{
			string current = GameVersion.Current;
			if (!GameVersion.TooOld(current))
			{
				return false;
			}
			string text = OldGameNotice.Text(current);
			UnityEngine.Debug.LogWarning("[Worldfall] " + text);
			Log.Info("stays off: " + text);
			base.gameObject.AddComponent<OldGameNotice>().Message = text;
			base.enabled = false;
			UnityEngine.Object.Destroy(this);
			return true;
		}

		private void PacksStart()
		{
			try
			{
				Packs.ForgetDrawn = delegate(List<MeshModel> retired)
				{
					if (_gpu == null)
					{
						return;
					}
					foreach (MeshModel item in retired)
					{
						_gpu.ForgetModel(item);
					}
				};
				Packs.CacheOwners = () => new object[1] { _scene };
				PackModelNames.SoundSets = () => _audio.SoundSetNames;
				Packs.Start(Settings);
			}
			catch (Exception ex)
			{
				Log.Error("packs: could not start: " + ex);
			}
		}

		private void PacksLate()
		{
			try
			{
				Packs.Follow(Settings);
				Packs.ApplyPending();
				_audio.KeepPackSounds();
			}
			catch (Exception ex)
			{
				Log.Error("packs: " + ex.Message);
			}
			if (_packSteamGone)
			{
				return;
			}
			try
			{
				PackSteam.Tick();
			}
			catch (Exception ex2)
			{
				_packSteamGone = true;
				Log.Info("packs: the Workshop is out of reach here (" + ex2.GetType().Name + ": " + ex2.Message + ")");
			}
			try
			{
				PacksUi.Tick();
			}
			catch (Exception ex3)
			{
				Log.Error("packs: " + ex3.Message);
			}
		}

		private void PadUpdate()
		{
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			PadControls.Mode mode = PadControls.Mode.Off;
			if (Config.game_loaded && World.world != null && !VrRunning)
			{
				if (_active)
				{
					mode = ((!AnyMenu && !TalkBusy) ? PadControls.Mode.Play : PadControls.Mode.Menu);
				}
				else if (_dive == Dive.None)
				{
					mode = ((_settingsMenu.Open || Run.WindowOpen) ? PadControls.Mode.Menu : PadControls.Mode.Map);
				}
			}
			if (mode != _padMode && mode == PadControls.Mode.Play)
			{
				PadControls.Driving = false;
			}
			_padMode = mode;
			PadControls.Update(Settings, mode, _active && Converse.Open, unscaledDeltaTime);
			bool flag = PadCursorOn && mode != PadControls.Mode.Play;
			if (flag != _padHidCursor)
			{
				_padHidCursor = flag;
				Cursor.visible = !flag;
			}
			if (!PadControls.Active)
			{
				return;
			}
			if (!_padLogged)
			{
				_padLogged = true;
				Log.Info("pad: " + PadControls.Describe());
			}
			if (VrMouse.Aim == null)
			{
				VrMouse.Aim = PointerAim;
			}
			if (mode != PadControls.Mode.Play && PadControls.Driving && !VrMouse.Patched)
			{
				VrMouse.Install();
			}
			switch (mode)
			{
			case PadControls.Mode.Play:
			{
				int num = PadControls.TakeTool();
				if (num != 0)
				{
					PadTool(num);
				}
				PadControls.LookScale = (PadAimSlows() ? Settings.PadAimSlow : 1f);
				break;
			}
			case PadControls.Mode.Menu:
				PadWindowKeys();
				break;
			case PadControls.Mode.Map:
				PadMap(unscaledDeltaTime);
				break;
			}
		}

		private void PadWindowKeys()
		{
			Vector2 vector = PadGui(PadControls.Driving ? PadControls.Cursor : new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f));
			Vector2Int vector2Int = PadControls.TakeNav();
			if (vector2Int != Vector2Int.zero && PadNav.Count == 0)
			{
				_padHopLater = vector2Int;
				_padHopUntil = Time.unscaledTime + 0.4f;
				vector2Int = Vector2Int.zero;
			}
			else if (vector2Int == Vector2Int.zero && _padHopLater != Vector2Int.zero && PadNav.Count > 0 && Time.unscaledTime < _padHopUntil)
			{
				vector2Int = _padHopLater;
			}
			if (PadNav.Count > 0 || Time.unscaledTime >= _padHopUntil)
			{
				_padHopLater = Vector2Int.zero;
			}
			if (vector2Int != Vector2Int.zero && _settingsMenu.Open && _settingsMenu.Drop != null)
			{
				if (vector2Int.y != 0)
				{
					_settingsMenu.DropMove(vector2Int.y);
					Sounds.PlayUI("event:/SFX/Ui/Click");
				}
				vector2Int = Vector2Int.zero;
			}
			if (vector2Int.x != 0 && vector2Int.y == 0 && PadControls.Driving && PadNav.StepSlider(vector, vector2Int.x))
			{
				vector2Int = Vector2Int.zero;
			}
			if (vector2Int != Vector2Int.zero && !Converse.Open && PadNav.Hop(vector, vector2Int, out var to))
			{
				PadControls.Place(PadPixel(to));
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			int num = PadControls.TakeStep();
			if (num != 0)
			{
				if (PadNav.TabStep(vector, num, out var to2))
				{
					PadControls.Place(PadPixel(to2));
					PadControls.Click();
				}
				else
				{
					_padScroll -= num;
				}
			}
		}

		private void PadMap(float dt)
		{
			int num = PadControls.TakeWheel();
			if (GodShown)
			{
				_padScroll += num;
			}
			else if (num != 0 && !_settingsMenu.Open)
			{
				try
				{
					for (int i = 0; i < Mathf.Abs(num); i++)
					{
						if (num > 0)
						{
							MoveCamera.zoomInWheel(null);
						}
						else
						{
							MoveCamera.zoomOutWheel(null);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("pad: zoom: " + ex.Message);
				}
			}
			MoveCamera instance = MoveCamera.instance;
			Vector2 leftStick = PadControls.LeftStick;
			if (instance != null && leftStick.sqrMagnitude > 0f && !IsTyping())
			{
				float num2 = dt * 55f * instance._target_zoom * instance.camera_move_speed;
				float num3 = leftStick.x * num2;
				float num4 = leftStick.y * num2;
				if (GodShown && GodView.Turn != 0f)
				{
					float num5 = Mathf.Cos(GodView.Turn);
					float num6 = Mathf.Sin(GodView.Turn);
					float num7 = num3 * num5 - num4 * num6;
					float num8 = num3 * num6 + num4 * num5;
					num3 = num7;
					num4 = num8;
				}
				instance._move_velocity.x += num3;
				instance._move_velocity.y += num4;
				instance.clampVelocity();
				instance._mouse_controls_used_last = false;
			}
			if (!PadControls.Down(KeyCode.Escape) || _settingsMenu.Open)
			{
				return;
			}
			try
			{
				if (ScrollWindow.isWindowActive())
				{
					ScrollWindow.hideAllEvent(pWithAnimation: false);
				}
				else if (!NoPowerPicked())
				{
					PowerButtonSelector.instance.unselectAll();
				}
			}
			catch (Exception ex2)
			{
				Log.Error("pad: B on the map: " + ex2.Message);
			}
		}

		private void PadTool(int step)
		{
			Actor host = _host;
			if (host == null || DeathTimer >= 0f || AnyMenu || IsTyping())
			{
				return;
			}
			int num = (Compass.Out ? 1 : (Torches.Out ? 2 : (Binoculars.Up ? 3 : 0)));
			int num2 = num;
			for (int i = 0; i < 4; i++)
			{
				num2 = (num2 + Math.Sign(step) + 4) % 4;
				if (Can(num2))
				{
					break;
				}
			}
			if (num2 != num)
			{
				if (num != 0)
				{
					Use(num);
				}
				if (num2 != 0)
				{
					Use(num2);
				}
			}
			bool Can(int t)
			{
				if (t != 0 && (t != 1 || !CanUseCompass) && (t != 2 || Torches.Count(host) <= (Torches.InMain(host) ? 1 : 0)))
				{
					if (t == 3)
					{
						return CanUseBinoculars;
					}
					return false;
				}
				return true;
			}
			void Use(int t)
			{
				switch (t)
				{
				case 1:
					UseTool(Tool.Compass);
					break;
				case 2:
					UseTool(Tool.Torch);
					break;
				default:
					PadControls.Tap(KeyCode.Z);
					break;
				}
			}
		}

		private bool PadAimSlows()
		{
			if (Settings.PadAimSlow >= 0.999f || !(_picked is Actor actor) || actor == _host)
			{
				return false;
			}
			bool flag = _bow.InHand || _shotgun.InHand || _blaster.InHand;
			if (!flag && _host != null)
			{
				string text = WeaponOf(_host);
				WeaponKind weaponKind = ((!string.IsNullOrEmpty(text)) ? ViewMeshes.KindOf(text) : WeaponKind.None);
				flag = weaponKind == WeaponKind.Bow || weaponKind == WeaponKind.Gun || weaponKind == WeaponKind.Shotgun;
			}
			if (!flag)
			{
				return false;
			}
			try
			{
				return actor.isAlive();
			}
			catch
			{
				return false;
			}
		}

		private void PointerAim(ref VrMouse.Laser laser)
		{
			VrAim(ref laser);
			VrNavAim(ref laser);
			if (!laser.InPlay && PadCursorOn && _padMode != PadControls.Mode.Play && _padMode != PadControls.Mode.Off)
			{
				laser.InPlay = true;
				laser.OnScreen = true;
				laser.Pixel = PadControls.Cursor;
				laser.Moved = PadControls.CursorMoved;
				laser.Left = PadControls.CursorLeft;
				laser.Right = PadControls.CursorRight;
				laser.Scroll = _padScroll;
				_padScroll = 0;
				laser.Gui = _active || _settingsMenu.Open || Run.WindowOpen;
			}
		}

		internal void DrawPadForCapture()
		{
			DrawPad();
		}

		internal void LetGoForPadTest()
		{
			TouchLetGo();
		}

		private static Vector2 PadGui(Vector2 pixel)
		{
			return new Vector2(pixel.x, (float)Screen.height - pixel.y);
		}

		private static Vector2 PadPixel(Vector2 gui)
		{
			return new Vector2(gui.x, (float)Screen.height - gui.y);
		}

		private void DrawPad()
		{
			DrawVrNav();
			if (!PadControls.Active)
			{
				return;
			}
			if (PadMenu.Open && _active)
			{
				_hud.DrawPadMenu(Screen.width, Screen.height);
			}
			if (PadCursorOn && _padMode != PadControls.Mode.Play && Event.current.type == EventType.Repaint)
			{
				GameArt.Ensure();
				Vector2 at = PadGui(PadControls.Cursor);
				if (_padMode == PadControls.Mode.Menu && PadNav.Under(at, out var r))
				{
					FocusFrame(r);
				}
				if (_padArrow == null)
				{
					_padArrow = PadArrowTexture();
				}
				float num = Mathf.Round(Mathf.Max(16f, (float)Screen.height / 36f));
				GUI.DrawTexture(new Rect(Mathf.Round(at.x), Mathf.Round(at.y), num * 0.7f, num), _padArrow);
			}
		}

		private static Texture2D PadArrowTexture()
		{
			int length = PadArrowRows[0].Length;
			int num = PadArrowRows.Length;
			Texture2D texture2D = new Texture2D(length, num, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "PadArrow"
			};
			Color32[] array = new Color32[length * num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < length; j++)
				{
					char c = PadArrowRows[i][j];
					int num2 = (num - 1 - i) * length + j;
					Color32 color;
					switch (c)
					{
					default:
						color = new Color32(0, 0, 0, 0);
						break;
					case 'o':
						color = new Color32(byte.MaxValue, 248, 230, byte.MaxValue);
						break;
					case '#':
						color = new Color32(20, 16, 12, byte.MaxValue);
						break;
					}
					array[num2] = color;
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private void PhoneUpkeep()
		{
			if (!_phoneChecked)
			{
				_phoneChecked = true;
				if (Device.Is != Device.Kind.Pc)
				{
					TunePhone();
				}
			}
			KeepLandscape();
			HideGameTouchButtons();
			ChooseScheme();
			int touchControls = Settings.TouchControls;
			bool flag = (touchControls == 1 || (touchControls == 0 && _touchScheme)) && _active && DeathTimer < 0f;
			if (flag && !TouchControls.Shown)
			{
				Log.Info("touch controls up (" + (Device.TouchSeen ? "touches" : "the mouse as a finger") + ")");
			}
			TouchControls.Shown = flag;
			TouchControls.PointerMode = !Device.TouchSeen;
			TouchControls.MenuMode = AnyMenu;
			TouchControls.LookSpeed = Settings.TouchLook;
			TouchControls.CanUse = !string.IsNullOrEmpty(Prompt);
			TouchControls.HasAbility = Ability != null;
			TouchControls.RAction = (_shotgun.InHand ? "Reload" : (_blaster.InHand ? "Vent" : null));
			TouchControls.Back = TouchBack;
			TouchControls.LetGo = TouchLetGo;
			try
			{
				TouchControls.TopRightFloor = _hud.MinimapBottom(Screen.width, Screen.height);
			}
			catch
			{
			}
			if (flag)
			{
				if (_menuBlocker == null || !_menuBlocker.activeSelf)
				{
					BlockMap(block: true);
				}
			}
			else if (_touchBlocksMap)
			{
				BlockMap(_settingsMenu.Open);
			}
			_touchBlocksMap = flag;
			if (flag)
			{
				HintUntil = 0f;
				TouchControls.RunPending();
				if (!_touchToldOnce && _dive == Dive.None)
				{
					_touchToldOnce = true;
					Toast("Touch controls: left thumb walks, drag on the right to look; more keys top left");
				}
			}
		}

		private void ChooseScheme()
		{
			float unscaledTime = Time.unscaledTime;
			bool num = Input.touchCount > 0 || TouchControls.ScriptedForTest;
			if (!_schemeChosen)
			{
				_schemeChosen = true;
				_touchScheme = Device.OnPhone;
			}
			if (num)
			{
				if (!Device.TouchSeen)
				{
					Device.TouchSeen = true;
					Log.Info("touch: first touch seen (touch supported " + Input.touchSupported + ", " + Input.touchCount + " fingers, mouse present " + Input.mousePresent + ")");
				}
				_lastTouchAt = unscaledTime;
				_touchScheme = true;
			}
			else
			{
				if (!_touchScheme)
				{
					return;
				}
				bool flag = Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2) && !Input.GetKeyDown(KeyCode.Escape);
				bool flag2 = false;
				if (!flag && !Device.OnPhone && Device.TouchSeen && unscaledTime - _lastTouchAt > 0.5f)
				{
					try
					{
						flag2 = Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.5f;
					}
					catch
					{
					}
				}
				if (flag || flag2)
				{
					_touchScheme = false;
					if (_active && Settings.TouchControls == 0 && !_keysToldOnce)
					{
						_keysToldOnce = true;
						Toast((flag ? "Keyboard" : "Mouse") + " in use: touch controls hidden (F2, View: Touch controls On keeps them)");
					}
				}
			}
		}

		private void KeepLandscape()
		{
			bool flag = Device.Native && (_active || _dive != Dive.None);
			if (flag == _landscapeHeld)
			{
				return;
			}
			_landscapeHeld = flag;
			try
			{
				if (flag)
				{
					if (Screen.height > Screen.width || GameScreenUpright())
					{
						GameScreenSideways();
						Log.Info("phone: turned sideways for first person");
					}
				}
				else
				{
					GameScreenBack();
					Log.Info("phone: screen back as the player had it");
				}
			}
			catch (Exception ex)
			{
				Log.Error("phone: could not turn the screen: " + ex.Message);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool GameScreenUpright()
		{
			if (!PlayerConfig.optionBoolEnabled("portrait"))
			{
				return PlayerConfig.optionBoolEnabled("autorotation");
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void GameScreenSideways()
		{
			Config.setPortrait(pValue: false);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void GameScreenBack()
		{
			if (PlayerConfig.optionBoolEnabled("autorotation"))
			{
				Config.setAutorotation(pValue: true);
			}
			else
			{
				Config.setPortrait(PlayerConfig.optionBoolEnabled("portrait"));
			}
		}

		private void HideGameTouchButtons()
		{
			if (!Device.Native)
			{
				return;
			}
			try
			{
				TouchPossessionController instance = TouchPossessionController.instance;
				if (instance == null)
				{
					return;
				}
				if (_active && instance.gameObject.activeSelf)
				{
					instance.gameObject.SetActive(value: false);
					_gameTouchHidden = true;
				}
				else if (!_active && _gameTouchHidden)
				{
					_gameTouchHidden = false;
					if (ControllableUnit.isControllingUnit())
					{
						instance.gameObject.SetActive(value: true);
					}
				}
			}
			catch
			{
			}
		}

		private void TunePhone()
		{
			if (!Settings.PhoneTuned)
			{
				Settings.PhoneTuned = true;
				Settings.Resolution = 0;
				Settings.DrawDistance = Mathf.Min(Settings.DrawDistance, 180f);
				Settings.DetailDistance = Mathf.Min(Settings.DetailDistance, 60f);
				Settings.CreatureDistance = Mathf.Min(Settings.CreatureDistance, 120f);
				Settings.FarModels = 0;
				float a = Screen.width;
				float b = Screen.height;
				float num = Mathf.Max(a, b) / Mathf.Max(1f, Mathf.Min(a, b));
				if (num > 1.85f)
				{
					float num2 = Mathf.Atan(Mathf.Tan(Settings.FieldOfView * 0.5f * (MathF.PI / 180f)) * num / 1.7777778f);
					Settings.FieldOfView = Mathf.Clamp(Mathf.Round(num2 * 2f * 57.29578f), 50f, 110f);
				}
				Settings.Save();
				Log.Info($"phone: settings made lighter once (render width {ResolutionWidths[0]}, draw {Settings.DrawDistance:0}, detail {Settings.DetailDistance:0}, creatures {Settings.CreatureDistance:0} tiles, field of view {Settings.FieldOfView:0} for a {num:0.00}:1 screen)");
			}
		}

		private void TouchBack()
		{
			if (_settingsMenu.Open)
			{
				if (!_settingsMenu.EscapeDrop())
				{
					SetSettingsMenu(open: false);
				}
			}
			else
			{
				if (CivilEscape())
				{
					return;
				}
				if (WarMap.Open)
				{
					WarMap.Close();
					return;
				}
				if (SoldierMap.Open)
				{
					SoldierMap.Open = false;
					return;
				}
				if (Royal.ObeliskOpen)
				{
					Royal.ObeliskOpen = false;
					_obeliskClosedFrame = Time.frameCount;
				}
				Craft.Open = false;
				Royal.Open = false;
			}
		}

		internal void RechooseSchemeForTest()
		{
			_schemeChosen = false;
		}

		internal void TunePhoneForTest()
		{
			TunePhone();
		}

		private void TouchLetGo()
		{
			try
			{
				ControllableUnit.clear();
			}
			catch (Exception ex)
			{
				Log.Error("touch controls: let go: " + ex.Message);
			}
		}

		private void PingKeys(bool menu)
		{
			float unscaledTime = Time.unscaledTime;
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			if (Time.frameCount - _pingsFrame > 2)
			{
				StopPings();
			}
			_pingsFrame = Time.frameCount;
			bool flag = IsTyping();
			bool flag2 = !menu && DeathTimer < 0f && _dive != Dive.In;
			if (PlayInput.GetKeyDown(KeyCode.N) && !flag && flag2)
			{
				_mapKeyAt = unscaledTime;
			}
			if (_mapKeyAt >= 0f)
			{
				if (!PlayInput.GetKey(KeyCode.N) || !flag2)
				{
					if (!_bigMap && flag2 && unscaledTime - _mapKeyAt < 0.25f)
					{
						Settings.Minimap = !Settings.Minimap;
						_settingsDirty = true;
						Sounds.PlayUI("event:/SFX/Ui/Click");
						Toast(Settings.Minimap ? "Minimap on   (N hides it; hold N for the whole map)" : "Minimap off   (N shows it)");
					}
					if (_bigMap)
					{
						Sounds.PlayUI("event:/SFX/UI/WindowClose");
					}
					_bigMap = false;
					_mapKeyAt = -1f;
				}
				else if (!_bigMap && unscaledTime - _mapKeyAt >= 0.25f)
				{
					_bigMap = true;
					Sounds.PlayUI("event:/SFX/UI/WindowWhoosh");
				}
			}
			bool num = PlayInput.GetKeyDown(KeyCode.C) && !flag && flag2;
			bool key = PlayInput.GetKey(KeyCode.C);
			if (TakeOverPressed() && !flag && flag2 && !_pingDown)
			{
				TryLeap();
			}
			if (num && !_pingDown)
			{
				_pingDown = true;
				_pingDownAt = unscaledTime;
				CaptureAim();
			}
			if (_pingDown)
			{
				if (!flag2)
				{
					CloseWheel();
				}
				else if (!key)
				{
					if (_wheel)
					{
						if (_wheelChoice >= 0)
						{
							PlacePing(Pings.Wheel[_wheelChoice]);
						}
						else
						{
							Sounds.PlayUI("event:/SFX/UI/WindowClose");
						}
					}
					else
					{
						PlacePing(null);
					}
					CloseWheel();
				}
				else if (!_wheel && unscaledTime - _pingDownAt >= 0.25f)
				{
					_wheel = true;
					_wheelAim = Vector2.zero;
					_wheelChoice = -1;
					_wheelOpenedAt = unscaledTime;
					Sounds.PlayUI("event:/SFX/UI/WindowWhoosh");
				}
				if (_wheel && (PlayInput.GetMouseButtonDown(0) || PlayInput.GetMouseButtonDown(1)))
				{
					if (_wheelChoice >= 0)
					{
						PlacePing(Pings.Wheel[_wheelChoice]);
					}
					CloseWheel();
					_pingDown = false;
					_pingSwallow = true;
				}
			}
			if (_wheel || _pingSwallow)
			{
				PossessionHooks.BlockAttack = true;
			}
			if (_pingSwallow && !PlayInput.GetMouseButton(0) && !PlayInput.GetMouseButton(1))
			{
				_pingSwallow = false;
			}
			UpdatePings(unscaledDeltaTime);
		}

		private void CloseWheel()
		{
			_wheel = false;
			_pingDown = false;
			_wheelChoice = -1;
		}

		private bool PingWheelSteals()
		{
			if (!_wheel)
			{
				return false;
			}
			float x = 0f;
			float y = 0f;
			try
			{
				x = Input.GetAxisRaw("Mouse X");
				y = Input.GetAxisRaw("Mouse Y");
			}
			catch (ArgumentException)
			{
			}
			if (DebugEyeSet)
			{
				x = (y = 0f);
			}
			_wheelAim += new Vector2(x, y) * 0.12f;
			Vector2 vector = PadControls.TakeLook();
			if (vector.sqrMagnitude > 0f)
			{
				_wheelAim += vector * 0.02f;
			}
			if (_wheelAim.magnitude > 1f)
			{
				_wheelAim = _wheelAim.normalized;
			}
			int wheelChoice = _wheelChoice;
			_wheelChoice = WheelSegment(_wheelAim);
			if (_wheelChoice != wheelChoice && _wheelChoice >= 0)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			if (Cursor.lockState != CursorLockMode.Locked)
			{
				Cursor.lockState = CursorLockMode.Locked;
			}
			Cursor.visible = false;
			return true;
		}

		internal static int WheelSegment(Vector2 aim)
		{
			if (aim.magnitude < 0.3f)
			{
				return -1;
			}
			int num = Pings.Wheel.Length;
			return Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(aim.x, aim.y) * 57.29578f, 360f) / (360f / (float)num)) % num;
		}

		internal bool PingForTest(PingKind? kind)
		{
			if (!CaptureAim())
			{
				return false;
			}
			return PlacePing(kind);
		}

		internal void WheelForTest(bool open, int choice)
		{
			_wheel = open;
			_wheelChoice = (open ? choice : (-1));
			_wheelOpenedAt = Time.unscaledTime - 1f;
			_wheelAim = ((choice >= 0) ? new Vector2(Mathf.Sin((float)choice * MathF.PI * 2f / (float)Pings.Wheel.Length), Mathf.Cos((float)choice * MathF.PI * 2f / (float)Pings.Wheel.Length)) : Vector2.zero);
		}

		internal void BigMapForTest(bool open)
		{
			_bigMap = open;
		}

		private bool CaptureAim()
		{
			_aimOk = false;
			_aimActor = null;
			_aimOn = null;
			if (_host == null || !FirstPersonAim.Ready(_host))
			{
				return false;
			}
			if (!FirstPersonAim.Point(out var point, out var creature))
			{
				return false;
			}
			_aimAt = new Vector3(point.X, point.Y, point.Z);
			_aimActor = ((creature != null && creature != _host) ? creature : null);
			_aimOn = ((_aimActor != null) ? ((BaseSimObject)_aimActor) : ((BaseSimObject)(_picked as Building)));
			_aimFar = Binoculars.Active;
			_aimOk = true;
			return true;
		}

		private bool PlacePing(PingKind? chosen)
		{
			if (!_aimOk || _host == null)
			{
				Toast(Binoculars.Active ? "Nothing in range to ping" : "Nothing there to ping");
				_audio.PlayEffect("ping_off", 0.5f);
				return false;
			}
			PingKind pingKind = (PingKind)(((int?)chosen) ?? ((_aimActor != null && IsFoe(_host, _aimActor)) ? 2 : 0));
			float magnitude = (new Vector2(_aimAt.x, _aimAt.y) - _host.current_position).magnitude;
			Ping ping = Pings.Near(_aimAt, _aimActor, Mathf.Max(1.5f, magnitude * 0.035f));
			if (ping != null)
			{
				Pings.Remove(ping);
				if (!chosen.HasValue || ping.Kind == pingKind)
				{
					_audio.PlayEffect("ping_off", 0.6f);
					LastPing = null;
					return true;
				}
			}
			Ping lastPing = Pings.Add(pingKind, _aimAt, _aimActor, _aimOn, _aimFar);
			LastPing = lastPing;
			LastRallied = 0;
			_audio.PlayEffect("ping_" + pingKind.ToString().ToLowerInvariant(), 0.75f);
			if (pingKind == PingKind.Enemy && _aimActor != null && Settings.PingAllies)
			{
				LastRallied = Pings.Rally(_host, _aimActor, 5);
				if (LastRallied > 0)
				{
					Toast((LastRallied == 1) ? ("A warrior of yours goes for " + Describe(_aimActor)) : (LastRallied + " of your warriors go for " + Describe(_aimActor)));
				}
			}
			CaptainPing(pingKind, _aimAt);
			Log.Info(string.Format("ping {0} at {1:F1},{2:F1} ({3:F0} tiles{4}){5}{6}", pingKind, _aimAt.x, _aimAt.y, magnitude, _aimFar ? ", binoculars" : "", (_aimActor != null) ? (" on " + Describe(_aimActor)) : "", (LastRallied > 0) ? (", " + LastRallied + " rallied") : ""));
			return true;
		}

		internal bool PingFavorite(Actor a)
		{
			if (_host == null || a == null || a == _host)
			{
				return false;
			}
			try
			{
				if (!a.isAlive())
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			Ping ping = Pings.On(a);
			if (ping != null)
			{
				Pings.Remove(ping);
				if (LastPing == ping)
				{
					LastPing = null;
				}
				_audio.PlayEffect("ping_off", 0.6f);
				return false;
			}
			Vector2 current_position = a.current_position;
			float z = 0f;
			try
			{
				z = GroundAt(current_position);
			}
			catch
			{
			}
			LastPing = Pings.Add(PingKind.Look, new Vector3(current_position.x, current_position.y, z), a, a, far: false, 600f);
			LastRallied = 0;
			_audio.PlayEffect("ping_look", 0.75f);
			Log.Info($"ping Look on favorite {Describe(a)} at {current_position.x:F1},{current_position.y:F1} ({(current_position - _host.current_position).magnitude:F0} tiles)");
			return true;
		}

		private static bool IsFoe(Actor self, Actor a)
		{
			try
			{
				if (a.has_attack_target && a.attack_target == self)
				{
					return true;
				}
				if (self.has_attack_target && self.attack_target == a)
				{
					return true;
				}
				return self.areFoes(a);
			}
			catch
			{
				return false;
			}
		}

		private void UpdatePings(float dt)
		{
			if (_host == null)
			{
				return;
			}
			if (_host != _pingHost)
			{
				_pingHost = _host;
				HostMap = null;
				_revealTimer = 0f;
				if (_bigMap)
				{
					_bigMap = false;
				}
			}
			Vector2 current_position = _host.current_position;
			if (Pings.Update(current_position))
			{
				_audio.PlayEffect("ping_go", 0.5f);
				Toast("You are there");
			}
			try
			{
				RevealForHost(dt, current_position);
			}
			catch (Exception ex)
			{
				Log.Error("explored (you): " + ex.Message);
			}
			if (!Settings.Minimap && !_bigMap && !Journal.Open)
			{
				return;
			}
			try
			{
				if (Settings.Minimap)
				{
					Minimap.Update(Land, HostMap, current_position, MinimapReach, dt);
				}
				if (_bigMap || Journal.Open)
				{
					Minimap.UpdateWorld(Land, HostMap, dt);
				}
			}
			catch (Exception ex2)
			{
				Log.Error("minimap: " + ex2.Message);
			}
			_dotsTimer -= dt;
			if (!(_dotsTimer <= 0f))
			{
				return;
			}
			_dotsTimer = 0.25f;
			try
			{
				GatherDots(current_position);
			}
			catch (Exception ex3)
			{
				Log.Error("minimap dots: " + ex3.Message);
			}
		}

		private void RevealForHost(float dt, Vector2 me)
		{
			_revealTimer -= dt;
			if (HostMap != null && _revealTimer > 0f)
			{
				return;
			}
			_revealTimer = 0.2f;
			HostMap = Explored.Of(_host);
			if (HostMap == null)
			{
				return;
			}
			float num = 0f;
			try
			{
				num = Land.SampleHeight(me.x, me.y);
			}
			catch
			{
			}
			float num2 = Mathf.Clamp(num * 2f, 0f, 24f);
			Explored.Reveal(HostMap, me, 16f + num2 * 0.5f);
			if (!Interior.Inside && !Cabin.Inside && !Mine.Inside)
			{
				float a = 40f + num2;
				float halfAngle = Mathf.Clamp((_view.Fov > 0.01f) ? (_view.Fov * 0.5f) : (Settings.FieldOfView * 0.5f * (MathF.PI / 180f)), 0.03f, 1.2f);
				if (Binoculars.Active)
				{
					a = Mathf.Max(a, Mathf.Max(BinocularsRange + 6f, 30f * Binoculars.Magnification));
				}
				a = Mathf.Min(a, Mathf.Max(40f, Renderer.MaxDistance));
				Explored.RevealView(HostMap, me, _view.Yaw, halfAngle, a);
			}
		}

		private void GatherDots(Vector2 me)
		{
			MinimapDots.Clear();
			float num = Mathf.Min(MinimapReach, 30f);
			float num2 = num * num;
			Kingdom kingdom = _host.kingdom;
			List<Actor> simpleList = World.world.units.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				if (MinimapDots.Count >= 60)
				{
					break;
				}
				Actor actor = simpleList[i];
				if (actor != null && actor != _host && actor.isAlive())
				{
					Vector2 current_position = actor.current_position;
					if (!((current_position - me).sqrMagnitude > num2) && !actor.is_inside_building)
					{
						Color value = (IsFoe(_host, actor) ? new Color(1f, 0.3f, 0.25f) : ((kingdom != null && actor.kingdom == kingdom) ? new Color(0.5f, 1f, 0.45f) : new Color(0.86f, 0.86f, 0.8f)));
						MinimapDots.Add(new KeyValuePair<Vector2, Color>(current_position, value));
					}
				}
			}
		}

		private void AddPingMarks(List<KeyValuePair<float, Color>> marks, Vector2 p)
		{
			foreach (Ping item in Pings.List)
			{
				Vector3 vector = Pings.Where(item);
				Vector2 vector2 = new Vector2(vector.x, vector.y) - p;
				if (vector2.sqrMagnitude > 4f)
				{
					marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector2.y, vector2.x), Pings.ColorOf(item.Kind)));
				}
			}
		}

		private void StopPings()
		{
			CloseWheel();
			_bigMap = false;
			_mapKeyAt = -1f;
			_pingSwallow = false;
		}

		internal static string PoseName(RestStyle s)
		{
			switch (s)
			{
			default:
				return "Stand";
			case RestStyle.Seat:
				return "Sit";
			case RestStyle.KneelBoth:
				return "Both knees";
			case RestStyle.Kneel:
				return "Kneel";
			case RestStyle.SitBack:
				return "Lean back";
			case RestStyle.CrossLegged:
				return "Cross-legged";
			case RestStyle.Ground:
				return "Sit";
			}
		}

		internal void PoseForTest(RestStyle style)
		{
			StartPose(style, chosen: true);
		}

		internal void Kneel()
		{
			StartPose(RestStyle.Kneel, chosen: true);
		}

		internal bool SeatForTest(out RestSeat seat)
		{
			seat = default(RestSeat);
			if (_host != null)
			{
				return FindSeat(_host, out seat);
			}
			return false;
		}

		private void PoseKeys(bool menu)
		{
			float unscaledTime = Time.unscaledTime;
			bool flag = !menu && DeathTimer < 0f && _dive != Dive.In && !IsTyping();
			bool flag2 = PoseTapForTest;
			PoseTapForTest = false;
			if (PlayInput.GetKeyDown(KeyCode.P) && flag)
			{
				_poseKeyAt = unscaledTime;
			}
			if (_poseKeyAt >= 0f)
			{
				if (!flag)
				{
					_poseKeyAt = -1f;
					_poseWheel = false;
				}
				else if (!PlayInput.GetKey(KeyCode.P))
				{
					if (_poseWheel)
					{
						if (_poseWheelChoice >= 0)
						{
							StartPose(PoseWheel[_poseWheelChoice], chosen: true);
						}
						else
						{
							Sounds.PlayUI("event:/SFX/UI/WindowClose");
						}
					}
					else
					{
						flag2 = true;
					}
					_poseWheel = false;
					_poseKeyAt = -1f;
				}
				else if (!_poseWheel && unscaledTime - _poseKeyAt >= 0.25f)
				{
					_poseWheel = true;
					_poseWheelAim = Vector2.zero;
					_poseWheelChoice = -1;
					_poseWheelOpenedAt = unscaledTime;
					Sounds.PlayUI("event:/SFX/UI/WindowWhoosh");
				}
				if (_poseWheel && (PlayInput.GetMouseButtonDown(0) || PlayInput.GetMouseButtonDown(1)))
				{
					if (_poseWheelChoice >= 0)
					{
						StartPose(PoseWheel[_poseWheelChoice], chosen: true);
					}
					_poseWheel = false;
					_poseKeyAt = -1f;
					_poseSwallow = true;
				}
			}
			if (flag2 && flag)
			{
				if (_pose != RestStyle.None && !_poseUp)
				{
					StandUp();
				}
				else if (_pose == RestStyle.None)
				{
					StartPose(RestStyle.Ground, chosen: false);
				}
			}
			if (_pose != RestStyle.None && !_poseUp && flag && _poseAmount > 0.3f)
			{
				bool num = PossessionHooks.MoveKeys().sqrMagnitude > 0.09f || PoseWalkForTest;
				bool flag3 = PlayInput.GetMouseButtonDown(0) && !_poseWheel && !ClickGrace && !WheelClick;
				if (num || PlayInput.GetKeyDown(KeyCode.Space) || flag3)
				{
					StandUp();
					if (flag3)
					{
						_poseSwallow = true;
					}
				}
			}
			if (_poseWheel || _poseSwallow || _pose != RestStyle.None)
			{
				PossessionHooks.BlockAttack = true;
			}
			if (_poseSwallow && !PlayInput.GetMouseButton(0) && !PlayInput.GetMouseButton(1))
			{
				_poseSwallow = false;
			}
		}

		private bool PoseWheelSteals()
		{
			if (!_poseWheel)
			{
				return false;
			}
			float x = 0f;
			float y = 0f;
			try
			{
				x = Input.GetAxisRaw("Mouse X");
				y = Input.GetAxisRaw("Mouse Y");
			}
			catch (ArgumentException)
			{
			}
			if (DebugEyeSet)
			{
				x = (y = 0f);
			}
			_poseWheelAim += new Vector2(x, y) * 0.12f;
			Vector2 vector = PadControls.TakeLook();
			if (vector.sqrMagnitude > 0f)
			{
				_poseWheelAim += vector * 0.02f;
			}
			if (_poseWheelAim.magnitude > 1f)
			{
				_poseWheelAim = _poseWheelAim.normalized;
			}
			int poseWheelChoice = _poseWheelChoice;
			_poseWheelChoice = WheelSegment(_poseWheelAim, PoseWheel.Length);
			if (_poseWheelChoice != poseWheelChoice && _poseWheelChoice >= 0)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			if (Cursor.lockState != CursorLockMode.Locked)
			{
				Cursor.lockState = CursorLockMode.Locked;
			}
			Cursor.visible = false;
			return true;
		}

		internal static int WheelSegment(Vector2 aim, int n)
		{
			if (aim.magnitude < 0.3f || n <= 0)
			{
				return -1;
			}
			return Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(aim.x, aim.y) * 57.29578f, 360f) / (360f / (float)n)) % n;
		}

		private bool PoseScroll(float scroll)
		{
			if (_pose == RestStyle.None || _poseUp || scroll == 0f)
			{
				return false;
			}
			bool flag = scroll < 0f;
			if (flag != _poseThird)
			{
				_poseThird = flag;
				Settings.PoseView = (flag ? 1 : 0);
				_settingsDirty = true;
			}
			return true;
		}

		private bool CanPose(Actor host)
		{
			if (host == null || DeathTimer >= 0f || Swimming || Cabin.Inside || Mine.Inside)
			{
				return false;
			}
			try
			{
				if (!People.IsHumanoid(host) || BoatHelm.Steers(host) || Crabzillas.Is(host) || host.is_inside_boat)
				{
					return false;
				}
				if (host.position_height > 0.1f || Mounted)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			return true;
		}

		private void StartPose(RestStyle style, bool chosen)
		{
			Actor host = _host;
			if (!CanPose(host))
			{
				Toast("You can't sit here");
				return;
			}
			if (style == RestStyle.Ground && FindSeat(host, out var seat))
			{
				SitIn(seat);
				return;
			}
			seat = new RestSeat
			{
				Kind = RestSeatKind.Ground,
				Heading = _view.Yaw
			};
			if (Interior.Inside)
			{
				seat.At = Interior.Eye;
				seat.Floor = (seat.Top = RoomFloors.Shown);
			}
			else
			{
				seat.At = host.current_position;
				seat.Floor = (seat.Top = GroundUnder(seat.At));
				if (Seats.FireNear(seat.At, 4f, out var fire) && style != RestStyle.Kneel && style != RestStyle.KneelBoth)
				{
					seat.Heading = Mathf.Atan2(fire.y - seat.At.y, fire.x - seat.At.x);
				}
			}
			Begin(style, seat);
		}

		private void SitIn(RestSeat seat)
		{
			Begin(RestStyle.Seat, seat);
		}

		private void Begin(RestStyle style, RestSeat seat)
		{
			if (_pose != RestStyle.None && _poseAmount > 0.05f)
			{
				_poseNext = style;
				_poseNextSeat = seat;
				_poseUp = true;
				return;
			}
			bool num = _pose != RestStyle.None;
			_pose = style;
			_poseSeat = seat;
			_poseIndoors = Interior.Inside;
			_poseUp = false;
			_poseSince = 0f;
			_poseNoticed = false;
			_poseTogether = 0f;
			_poseBeside = null;
			_poseHost = _host;
			if (!num)
			{
				_poseAmount = 0f;
				_poseFrom = _view.Yaw;
				_poseThird = Settings.PoseView == 1;
			}
			_poseRested = 1f;
			_poseHurts = Feedback.Hurts;
			_poseNext = RestStyle.None;
			if (!num)
			{
				_poseHolstered = Holstered;
			}
			Holstered = true;
			Log.Info(string.Format("pose: {0} on {1} at {2:F2},{3:F2} floor {4:F2} top {5:F2}{6}{7}", style, seat.Kind, seat.At.x, seat.At.y, seat.Floor, seat.Top, seat.Hang ? " legs hanging" : "", _poseIndoors ? " (room)" : ""));
		}

		private void StandUp()
		{
			if (_pose != RestStyle.None)
			{
				_poseUp = true;
			}
		}

		private void DropPose()
		{
			_pose = RestStyle.None;
			_poseNext = RestStyle.None;
			_poseAmount = 0f;
			_poseUp = false;
			_poseView = 0f;
			_poseWheel = false;
			_poseKeyAt = -1f;
			People.HostRest = default(RestPose);
			RoomWalkers.YouSeat = null;
		}

		private void UpdatePose(float dt)
		{
			Actor host = _host;
			if (_pose == RestStyle.None)
			{
				People.HostRest = default(RestPose);
				RoomWalkers.YouSeat = null;
				return;
			}
			if (host != _poseHost || !CanPose(host) || Interior.Inside != _poseIndoors)
			{
				DropPose();
				return;
			}
			float num = (_poseUp ? 2f : 1.3333334f);
			_poseAmount = Mathf.MoveTowards(_poseAmount, _poseUp ? 0f : 1f, dt * num);
			_poseView = Mathf.MoveTowards(_poseView, (_poseThird && !_poseUp && !VrControls.Active) ? 1f : 0f, dt / 0.45f);
			if (_poseUp && _poseAmount <= 0f)
			{
				if (_poseNext != RestStyle.None)
				{
					RestStyle poseNext = _poseNext;
					bool poseHolstered = _poseHolstered;
					_pose = RestStyle.None;
					Begin(poseNext, _poseNextSeat);
					_poseHolstered = poseHolstered;
				}
				else
				{
					DropPose();
					if (!_poseHolstered)
					{
						Holstered = false;
					}
				}
				return;
			}
			if (Feedback.Hurts != _poseHurts && !_poseUp)
			{
				_poseUp = true;
				_poseNext = RestStyle.None;
			}
			_poseSince += dt;
			if (!_poseUp && _poseAmount >= 0.95f)
			{
				if (!_poseNoticed)
				{
					_poseNoticed = true;
					Noticed(host);
				}
				Rest(host, dt);
			}
			Mathf.SmoothStep(0f, 1f, _poseAmount);
			float heading = Mathf.LerpAngle(_poseFrom * 57.29578f, _poseSeat.Heading * 57.29578f, Mathf.Clamp01(_poseAmount * 1.6f)) * (MathF.PI / 180f);
			People.HostRest = RestPose.Of(_pose, _poseAmount, heading, _poseSeat.Knee ? (-1f) : _poseSeat.Height, _poseSeat.Hang);
			RoomWalkers.YouSeat = ((_poseIndoors && _pose == RestStyle.Seat) ? new Vector2?(_poseSeat.At) : ((Vector2?)null));
		}

		internal bool PoseBodyAt(Vector2 standing, float standingZ, out Vector2 at, out float z)
		{
			at = standing;
			z = standingZ;
			if (_pose == RestStyle.None)
			{
				return false;
			}
			float t = Mathf.SmoothStep(0f, 1f, _poseAmount);
			at = Vector2.Lerp(standing, _poseSeat.At, t);
			float b = (_poseSeat.Hang ? _poseSeat.Top : _poseSeat.Floor);
			z = Mathf.Lerp(standingZ, b, t);
			return true;
		}

		private float PoseEyeZ(float standingEye, float ground, float body)
		{
			if (_pose == RestStyle.None || _poseAmount <= 0f)
			{
				return standingEye;
			}
			PersonRig personRig = People.HostRig ?? FallbackRig();
			if (personRig == null)
			{
				return standingEye;
			}
			float num = body / Mathf.Max(0.1f, personRig.Height);
			float seat = ((_poseSeat.Hang || _poseSeat.Knee) ? personRig.KneeHeight : (_poseSeat.Height / Mathf.Max(0.0001f, num)));
			float num2 = (personRig.LegShare * personRig.Height - personRig.RestHip(_pose, seat)) * num;
			float b = (_poseSeat.Hang ? (_poseSeat.Top - personRig.KneeHeight * num) : _poseSeat.Floor) + (standingEye - ground) - num2;
			return Mathf.Lerp(standingEye, b, Mathf.SmoothStep(0f, 1f, _poseAmount));
		}

		private Vector2 PoseShift(Vector2 standing)
		{
			if (_pose == RestStyle.None)
			{
				return Vector2.zero;
			}
			return (_poseSeat.At - standing) * Mathf.SmoothStep(0f, 1f, _poseAmount);
		}

		private static PersonRig FallbackRig()
		{
			if (_fallbackRig != null)
			{
				return _fallbackRig;
			}
			MeshModel meshModel = Models.Get("person_human");
			if (meshModel == null)
			{
				return null;
			}
			return _fallbackRig = Models.Rig(meshModel);
		}

		private float GroundUnder(Vector2 p)
		{
			float num = _sampler.Cache.SampleHeight(p.x, p.y);
			if (DockDecks.HeightAt(p.x, p.y, out var z) && z > num)
			{
				num = z;
			}
			if (BuildingFloors.StandOn(p.x, p.y, 0.05f, num + 0.6f, out var z2, out var _) && z2 > num)
			{
				num = z2;
			}
			return num;
		}

		private bool FindSeat(Actor host, out RestSeat seat)
		{
			seat = default(RestSeat);
			if (Interior.Inside)
			{
				return Seats.Chair(Interior, Interior.Eye, _view.Yaw, out seat);
			}
			Vector2 current_position = host.current_position;
			float bodyHeightNow = BodyHeightNow;
			if (Seats.PierEdge(current_position, _view.Yaw, bodyHeightNow, _sampler.Cache, out seat))
			{
				return true;
			}
			if (_picked is Building building && (building.current_tile == null || (new Vector2(building.current_tile.posV3.x, building.current_tile.posV3.y) - current_position).magnitude < 2.2f))
			{
				string text = null;
				Seats.Why = null;
				List<MeshInstance> meshes = _scene.Meshes;
				for (int i = 0; i < meshes.Count; i++)
				{
					MeshInstance meshInstance = meshes[i];
					if (!meshInstance.ShadowOnly && meshInstance.Tag >= 0 && meshInstance.Tag < _scene.Tagged.Count && _scene.Tagged[meshInstance.Tag] == building)
					{
						if (Seats.Rock(building, meshInstance, current_position, bodyHeightNow, _sampler.Cache, out seat))
						{
							return true;
						}
						text = ((text != null) ? (text + "," + meshInstance.Model?.Name) : meshInstance.Model?.Name);
					}
				}
				if (text != null)
				{
					Seats.Why = Seats.Why + " [" + text + "]";
				}
			}
			return Seats.Ledge(current_position, GroundUnder(current_position), bodyHeightNow, _view.Yaw, out seat);
		}

		private bool SeatPrompt(Actor host, bool append = false)
		{
			if (_pose != RestStyle.None || !CanPose(host) || !FindSeat(host, out var seat))
			{
				return false;
			}
			string text = ((seat.Kind == RestSeatKind.Pier) ? "E:Sit on the edge" : "E:Sit");
			Prompt = ((append && Prompt != null) ? (Prompt + "|" + text) : text);
			if ((PlayInput.GetKeyDown(KeyCode.E) && !IsTyping()) || PoseSitForTest)
			{
				PoseSitForTest = false;
				SitIn(seat);
			}
			return true;
		}

		private ViewState PoseCamera(ViewState eye, float body, out bool handsUp)
		{
			float num = Mathf.SmoothStep(0f, 1f, _poseView);
			handsUp = num < 0.08f;
			float f = Mathf.Atan(eye.Pitch);
			float num2 = (_poseIndoors ? 1.25f : 1.9f) * body;
			Vector3 vector = new Vector3((0f - Mathf.Cos(eye.Yaw)) * Mathf.Cos(f), (0f - Mathf.Sin(eye.Yaw)) * Mathf.Cos(f), 0f - Mathf.Sin(f));
			Vector3 vector2 = new Vector3(Mathf.Sin(eye.Yaw), 0f - Mathf.Cos(eye.Yaw), 0f);
			Vector3 vector3 = new Vector3(eye.X, eye.Y, eye.Z + 0.12f * body) + vector2 * 0.18f * body;
			float num3 = num2;
			for (float num4 = 0.2f; num4 <= num2; num4 += 0.05f)
			{
				Vector3 q = vector3 + vector * num4;
				if (Blocked(q, body))
				{
					num3 = Mathf.Max(0.15f, num4 - 0.15f);
					break;
				}
			}
			if (!_poseIndoors)
			{
				num3 = Mathf.Min(num3, ClearOfModels(vector3, vector, num3));
			}
			Vector3 vector4 = (PoseCameraForTest = vector3 + vector * num3 * num);
			ViewState result = eye;
			result.X = Mathf.Lerp(eye.X, vector4.x, num);
			result.Y = Mathf.Lerp(eye.Y, vector4.y, num);
			result.Z = Mathf.Lerp(eye.Z, vector4.z, num);
			return result;
		}

		private float ClearOfModels(Vector3 from, Vector3 back, float reach)
		{
			List<MeshInstance> meshes = _scene.Meshes;
			List<BaseSimObject> tagged = _scene.Tagged;
			Vec3 a = new Vec3(from.x, from.y, from.z);
			Vector3 vector = from + back * (reach + 0.12f);
			Vec3 b = new Vec3(vector.x, vector.y, vector.z);
			float num = reach;
			for (int i = 0; i < meshes.Count; i++)
			{
				MeshInstance meshInstance = meshes[i];
				if (meshInstance != null && meshInstance.Model != null && !meshInstance.ShadowOnly && meshInstance.Tag != 2147483646)
				{
					float num2 = meshInstance.Position.X - from.x;
					float num3 = meshInstance.Position.Y - from.y;
					if (!(num2 * num2 + num3 * num3 > 16f) && (meshInstance.Tag < 0 || meshInstance.Tag >= tagged.Count || (!(tagged[meshInstance.Tag] is Actor) && (!(tagged[meshInstance.Tag] is Building b2) || AssetStyle.IsSolid(b2)))) && MeshRay.Segment(meshInstance, a, b, out var hit))
					{
						num = Mathf.Min(num, Mathf.Max(0.15f, hit * (reach + 0.12f) - 0.12f));
					}
				}
			}
			return num;
		}

		private bool Blocked(Vector3 q, float body)
		{
			if (_poseIndoors)
			{
				if (Interior.CameraFits(new Vector2(q.x, q.y), 0.12f) && !(q.z > (float)Interior.Room.CeilingBottom - 0.08f))
				{
					return q.z < RoomFloors.Shown + 0.1f;
				}
				return true;
			}
			TerrainCache cache = _sampler.Cache;
			if (q.z < cache.SampleHeight(q.x, q.y) + 0.15f)
			{
				return true;
			}
			if (BuildingFloors.StandOn(q.x, q.y, 0.06f, 999f, out var z, out var _) && z > q.z - 0.15f)
			{
				return true;
			}
			return false;
		}

		private void Rest(Actor host, float dt)
		{
			if (!RestPose.Sitting(_pose) || (_poseRested -= dt) > 0f)
			{
				return;
			}
			_poseRested = 1f;
			Vector2 fire;
			bool flag = (!_poseIndoors && Seats.FireNear(host.current_position, 4f, out fire)) || (_poseIndoors && Interior.Room != null && new Vector2(Interior.Room.HearthX - Interior.Eye.x, Interior.Room.HearthY - Interior.Eye.y).magnitude < 2.5f);
			try
			{
				int maxStamina = host.getMaxStamina();
				host.data.stamina = Mathf.Min(maxStamina, host.data.stamina + Mathf.Max(1, Mathf.RoundToInt((float)maxStamina * (flag ? 0.12f : 0.06f))));
				int maxHealth = host.getMaxHealth();
				if ((float)host.getHealth() < (float)maxHealth * (flag ? 0.85f : 0.7f))
				{
					host.restoreHealth(Mathf.Max(1, Mathf.RoundToInt((float)maxHealth * (flag ? 0.02f : 0.01f))));
				}
			}
			catch
			{
			}
			Beside(host, 1f);
		}

		private void Noticed(Actor host)
		{
			Vector2 current_position = host.current_position;
			Vector2 rhs = new Vector2(Mathf.Cos(_view.Yaw), Mathf.Sin(_view.Yaw));
			Actor actor = null;
			float num = float.MaxValue;
			int num2 = 0;
			try
			{
				foreach (Actor item in Finder.getUnitsFromChunk(host.current_tile, 1, 8f))
				{
					if (item == null || item == host || !item.isAlive() || !People.IsHumanoid(item))
					{
						continue;
					}
					Vector2 vector = item.current_position - current_position;
					float magnitude = vector.magnitude;
					if (!(magnitude > 8f))
					{
						if (num2 < 6 && UnityEngine.Random.value < 0.55f)
						{
							Attention.Watch(item, current_position, 1.5f + 2.5f * UnityEngine.Random.value);
							num2++;
						}
						if ((_pose == RestStyle.Kneel || _pose == RestStyle.KneelBoth) && magnitude < 4.5f && magnitude < num && (magnitude < 0.8f || Vector2.Dot(vector / magnitude, rhs) > 0.5f) && Lords(item, host))
						{
							actor = item;
							num = magnitude;
						}
					}
				}
			}
			catch
			{
			}
			if (actor != null)
			{
				KneltTo(host, actor);
			}
		}

		private static bool Lords(Actor a, Actor you)
		{
			try
			{
				if (a.isKing() && a.kingdom != null && a.kingdom == you.kingdom)
				{
					return true;
				}
				if (a.isCityLeader() && a.city != null && a.city == you.city)
				{
					return true;
				}
			}
			catch
			{
			}
			return false;
		}

		private void KneltTo(Actor host, Actor lord)
		{
			long key = 0L;
			try
			{
				key = lord.data.id;
			}
			catch
			{
			}
			float unscaledTime = Time.unscaledTime;
			if (!_knelt.TryGetValue(key, out var value) || !(unscaledTime - value < 120f))
			{
				_knelt[key] = unscaledTime;
				Chronicle.ChangeOpinion(lord, host, 3);
				bool flag = false;
				try
				{
					flag = lord.isKing();
				}
				catch
				{
				}
				string line = (flag ? Dialogue.Pick("Rise.", "Rise. Your loyalty is seen.", "On your feet.") : Dialogue.Pick("Up you get.", "No need for that.", "Rise, friend."));
				Talk.Aside(lord, line);
				Attention.Watch(lord, host.current_position, 4f);
				Log.Info("pose: knelt before " + Describe(lord) + " (+3)");
			}
		}

		private void Beside(Actor host, float dt)
		{
			Actor actor = null;
			float num = 1.8f;
			Vector2 current_position = host.current_position;
			try
			{
				foreach (Actor item in Finder.getUnitsFromChunk(host.current_tile, 1, 2f))
				{
					if (item != null && item != host && item.isAlive() && (Lounging.Sits(item) || (_poseIndoors && Interior.Seated(item))))
					{
						float magnitude = (_poseIndoors ? (Interior.PositionOf(item) - _poseSeat.At) : (item.current_position - current_position)).magnitude;
						if (magnitude < num)
						{
							num = magnitude;
							actor = item;
						}
					}
				}
			}
			catch
			{
			}
			if (actor != _poseBeside)
			{
				_poseBeside = actor;
				_poseTogether = 0f;
			}
			if (actor == null)
			{
				return;
			}
			_poseTogether += dt;
			if (!(_poseTogether < 20f) && PeopleMet.Knows(host, actor))
			{
				long key = 0L;
				try
				{
					key = actor.data.id;
				}
				catch
				{
				}
				float unscaledTime = Time.unscaledTime;
				if (!_satWith.TryGetValue(key, out var value) || !(unscaledTime - value < 180f))
				{
					_satWith[key] = unscaledTime;
					_poseTogether = 0f;
					Chronicle.Note(actor, host, 1);
					Chronicle.ChangeOpinion(actor, host, 1);
					Log.Info("pose: sat a while with " + Describe(actor) + " (a talk)");
				}
			}
		}

		private bool ChairPrompt(Actor host)
		{
			return SeatPrompt(host);
		}

		private void AddPosedBodyIndoors(List<MeshInstance> meshes, List<VoxelObject> voxels, Actor host, float body, float dt)
		{
			if (!PoseShowsBody || host == null || HostImage == null)
			{
				return;
			}
			PoseBodyAt(Interior.Eye, RoomFloors.Shown, out var at, out var z);
			People.HostShown = host;
			People._asHost = true;
			int count = voxels.Count;
			int count2 = meshes.Count;
			try
			{
				People.AddMesh(meshes, voxels, host, HostImage, _sprites, at.x, at.y, z, body, People.HostRest.Heading, 0f, 0.5f, dt, new VoxelObject
				{
					Tag = 2147483646,
					NoShadow = true,
					SortDepth = 0.2f
				}, swimming: false);
			}
			finally
			{
				People._asHost = false;
			}
			if (voxels.Count > count)
			{
				voxels.RemoveRange(count, voxels.Count - count);
			}
			for (int i = count2; i < meshes.Count; i++)
			{
				if (meshes[i] != null)
				{
					meshes[i].ShadowOnly = false;
				}
			}
		}

		internal void FixDetailForTest(bool on)
		{
			HoldDetailForTest = on;
			if (on)
			{
				_scene.Detail = 1f;
				Renderer.ShadowDistance = 22f * _scene.Detail;
				_creatureDetail = 1f;
			}
		}

		private void FightRoomBegin(HouseInterior room, Actor host)
		{
			if (room == null || !room.Inside || room.Preview || host == null)
			{
				RoomCombat.End();
			}
			else
			{
				RoomCombat.Begin((room.House != null) ? ((object)room.House) : ((object)room), room.House, room.Room, room.Walkers, room.Tagged, host, room.Eye, room.CombatOffset(host));
			}
		}

		private void FightRoomBegin(ModelRoom room, Actor host)
		{
			if (room == null || !room.Inside || host == null || room.CombatKey == null)
			{
				RoomCombat.End();
			}
			else
			{
				RoomCombat.Begin(room.CombatKey, room.CombatBuilding, room.Room, room.CombatWalkers, room.Tagged, host, room.Eye, room.CombatOffset);
			}
		}

		private void FightRoomCarried(HouseInterior room, Actor host)
		{
			if (room != null && room.Inside && !room.Preview)
			{
				CarriedInRoom(host, room.Eye, (Vector2 p) => room.TryPut(p));
			}
		}

		private void FightRoomCarried(ModelRoom room, Actor host)
		{
			if (room != null && room.Inside)
			{
				CarriedInRoom(host, room.Eye, (Vector2 p) => room.TryPut(p));
			}
		}

		private static void CarriedInRoom(Actor host, Vector2 eye, Func<Vector2, bool> put)
		{
			if (!RoomCombat.Active || RoomCombat.You != host || RoomCombat.Building == null)
			{
				return;
			}
			Vector2 current_position;
			try
			{
				current_position = host.current_position;
			}
			catch
			{
				return;
			}
			Vector2 vector = RoomCombat.ToWorld(RoomCombat.Eye);
			Vector2 vector2 = current_position - vector;
			if (vector2.sqrMagnitude < 0.0004f || vector2.sqrMagnitude > 144f)
			{
				return;
			}
			int num = Mathf.Clamp(Mathf.CeilToInt(vector2.magnitude / 0.05f), 1, 240);
			Vector2 vector3 = eye;
			for (int i = 0; i < num; i++)
			{
				Vector2 vector4 = vector3 + vector2 / num;
				if (put(vector4))
				{
					vector3 = vector4;
					continue;
				}
				break;
			}
		}

		private void CombatIndoors(Actor host, float dt)
		{
			string text = ((Holstered || host == null) ? null : WeaponOf(host));
			bool flag = host != null && !AnyMenu && !ClickGrace && DeathTimer < 0f && !Binoculars.Active && !Bandages.Wrapping && !CompassBlock && !Hub.AnyModeActive && !TalkBusy;
			_bow.Update(host, text, flag, 0f, dt, _audio, _hands);
			_melee.Update(host, text, !BeastView.Active, flag, Settings.HeadBob, dt, Feedback, _audio);
			_guard.Update(host, (text != null) ? ViewMeshes.KindOf(text) : WeaponKind.None, !BeastView.Active, flag && !_wheel && !_pingSwallow, dt, _melee, _bow, Settings.ParryTiming, _audio);
			UpdateWindUps(host, Settings.EnemyWindups, RoomCombat.Tagged);
			UpdateBlaster(host, text, flag, dt);
			UpdateShotgun(host, text, flag, dt);
			UpdateAbility(host, dead: false, dt);
		}

		private void AimIndoors(Actor host, float body, float feet, TerrainCache floor)
		{
			if (RoomCombat.Active && host != null && floor != null)
			{
				BaseSimObject baseSimObject = _picked;
				if (baseSimObject != null && !RoomCombat.In(baseSimObject))
				{
					baseSimObject = null;
				}
				FirstPersonAim.Set(host, _view, body, feet, baseSimObject, Renderer.PickDepth, floor, Renderer.MaxDistance);
			}
		}

		private bool RoomLootPrompt(Actor host)
		{
			if (!RoomCombat.LootPrompt(this, Body ?? host, _view.Yaw, out var prompt))
			{
				return false;
			}
			Prompt = prompt;
			return true;
		}

		private float RoomJumpLift(Actor host, float body, float standingEye, float dt)
		{
			RoomBodyForTest = body;
			float num = 0f;
			try
			{
				if (host != null && host.isAlive())
				{
					num = Mathf.Max(0f, host.position_height);
				}
			}
			catch
			{
			}
			float num2 = Mathf.Max(0f, RoomCeiling() - 0.05f * body - standingEye);
			if (num > num2 && PossessionHooks.Leaping)
			{
				PossessionHooks.Jump.Cancel();
				num = num2;
				_roomFall = 0f;
				try
				{
					host.position_height = Mathf.Max(0.0001f, num2);
				}
				catch
				{
				}
			}
			else if (num > 0f && !PossessionHooks.Leaping)
			{
				float num3 = Mathf.Min(dt, 0.1f);
				_roomFall += 9f * num3;
				num = Mathf.Max(0f, num - _roomFall * num3);
				try
				{
					host.position_height = num;
					if (num <= 0f)
					{
						host.stopForce();
					}
				}
				catch
				{
				}
			}
			else
			{
				_roomFall = 0f;
			}
			_standLift = num;
			bool flag = num > 0.05f;
			if (_wasAirborne && !flag)
			{
				Leap jump = PossessionHooks.Jump;
				float num4 = ((jump.Landings != _landingsSeen) ? jump.LandSpeed : 4.6f);
				float value = Weight.Of(host);
				_landVel = (0f - body) * Mathf.Clamp(0.3f * num4, 0.35f, 3.2f) * Mathf.Sqrt(Mathf.Clamp(value, 0.3f, 2.5f));
			}
			_landingsSeen = PossessionHooks.Jump.Landings;
			_wasAirborne = flag;
			float num5 = Mathf.Min(dt, 0.04f);
			float num6 = 140f / Mathf.Clamp((host != null) ? Weight.Of(host) : 1f, 0.5f, 2.5f);
			_landVel += ((0f - _landDip) * num6 - _landVel * 2f * 0.76f * Mathf.Sqrt(num6)) * num5;
			_landDip += _landVel * num5;
			return Mathf.Min(num, num2) + _landDip;
		}

		internal float RoomCeiling()
		{
			Interior room = Interior.Room;
			if (room == null)
			{
				return float.PositiveInfinity;
			}
			try
			{
				if (room.Fit != null)
				{
					return Mathf.Min(RoomFloor.FittedCeiling(room.Fit, Interior.Eye.x, Interior.Eye.y, RoomFloors.Feet), RoomFloors.EyeLimit(Interior.Eye) + 0.05f);
				}
			}
			catch
			{
			}
			return room.Top - 0.38f + 0.05f;
		}

		private void RoomPerfEnd(bool backdrop)
		{
			if (RoomClock.End(backdrop))
			{
				Log.Info(RoomClock.Line((_gpu != null && _gpuShown) ? ("card: " + _gpu.TakeTimes() + ", " + _gpu.Stats) : "drawn by the CPU"));
			}
		}

		private void WindowReach()
		{
			if (!(WindowReachTiles <= 0f))
			{
				Renderer.BillboardDistance = Mathf.Min(Renderer.BillboardDistance, WindowReachTiles);
				_creatureReach = Mathf.Min(_creatureReach, Mathf.Max(Renderer.BillboardDistance, WindowReachTiles));
			}
		}

		private bool WindowGatherNow(float dt, out float gatherDt)
		{
			int frameCount = Time.frameCount;
			bool flag = frameCount - _windowLastFrame > 1;
			_windowLastFrame = frameCount;
			_windowDt += dt;
			bool flag2 = Interior.DoorOpen > 0.01f;
			if (!flag && !flag2 && ++_windowSkipped < WindowEvery)
			{
				gatherDt = 0f;
				return false;
			}
			gatherDt = (flag ? dt : _windowDt);
			_windowDt = 0f;
			_windowSkipped = 0;
			return true;
		}

		private void RenderRoom(ModelRoom r, byte ground, float wind, float dt)
		{
			Actor host = _host;
			Vector2 vector = PossessionHooks.MoveKeys() + PossessionHooks.ScriptedInput;
			if (AnyMenu)
			{
				vector = Vector2.zero;
			}
			FightRoomCarried(r, host);
			r.Move(vector.y, vector.x, _view.Yaw, dt);
			Moving = vector != Vector2.zero;
			Swimming = false;
			float num = 1f;
			try
			{
				if (host.is_inside_boat)
				{
					host._is_in_liquid = false;
				}
				SpriteImage spriteImage = _sprites.Get(host.getSpriteToRender());
				if (spriteImage != null)
				{
					HostImage = spriteImage;
					num = Mathf.Clamp(Stature.Of(host, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(host.current_scale.y), dt), 0.3f, 2f);
				}
			}
			catch
			{
			}
			_gait.Update(r.Eye, dt, grounded: true, Moving, 0f, PossessionHooks.Walking ? 1f : 0f, num);
			BobPhase = _gait.Phase;
			Stride = _gait.Stride;
			r.Tick(dt);
			float num2 = Mathf.Cos(_view.Yaw);
			float num3 = Mathf.Sin(_view.Yaw);
			_view.X = r.Eye.x + num3 * _gait.Lateral * HeadBobNow;
			_view.Y = r.Eye.y - num2 * _gait.Lateral * HeadBobNow;
			Vector2 offset;
			float up;
			bool flag = BeastView.Update(host, _view.Yaw, _pitchDegrees, num, swimming: false, Moving, Feedback.SinceAttack, Feedback.LastAttackMelee, dt, out offset, out up);
			if (flag)
			{
				_view.X += offset.x;
				_view.Y += offset.y;
			}
			float a = (flag ? up : (num * 0.9f)) * Settings.EyeHeight;
			_view.Z = Mathf.Min(a, r.Room.Top - 0.38f) + _gait.Vertical * HeadBobNow;
			_view.Pitch = LookSlope(_pitchDegrees + Feedback.Kick * 1.5f - _gait.Nod * HeadBobNow);
			_view.Fov = (VrSquare ? 105f : Settings.FieldOfView) * (MathF.PI / 180f);
			_fovKick = 0f;
			VrRoomHead(ref _view, 0f, dt);
			FightRoomBegin(r, host);
			WeaponsIndoors(host, dt);
			_frameClock.Restart();
			_sprites.BeginFrame();
			bool flag2 = GpuRoomsWanted;
			int num4 = RenderWidth(flag2);
			int num5 = Mathf.Max(90, Mathf.RoundToInt((float)(num4 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
			if (VrSquare)
			{
				num5 = num4;
			}
			Renderer.Resize(num4, num5);
			Renderer.Time = SeaNow;
			Atmosphere air = _day.Air;
			Atmosphere outside = air;
			Renderer.Windows.Clear();
			Renderer.OverlayMeshes.Clear();
			Renderer.Backdrop = null;
			Renderer.RippleStrength = 0f;
			Renderer.ShadowFocusSet = false;
			bool flag3 = r.SeesOutside && !_vrRoomStereo;
			bool gpuWorld = false;
			ViewState outside2 = default(ViewState);
			if (flag3)
			{
				outside2 = r.OutsideView(_view);
				Renderer.Air = air;
				FollowLight(air);
				_sampler.Tick(outside2.X, outside2.Y);
				ApplyViewDistance();
				_scene.Skip = r.Hidden;
				CollectScene(outside2, outside2, host, 0f, air, dt);
				_scene.Skip = null;
				ApplyWeather(air, r.Where, dt);
				outside = Renderer.Air;
				Renderer.Meshes.Clear();
				Renderer.Meshes.AddRange(_scene.Meshes);
				Renderer.Lights.Clear();
				Renderer.Lights.AddRange(_scene.Lights);
				Renderer.Streaks.AddRange(_scene.Streaks);
				gpuWorld = GpuBackdrop(outside2, num4, num5);
				if (!gpuWorld)
				{
					Renderer.Streaks.Clear();
				}
				if (gpuWorld && flag2)
				{
					KeepCabinWorld();
				}
				else if (flag2)
				{
					flag2 = false;
					num4 = RenderWidth(onCard: false);
					num5 = Mathf.Max(90, Mathf.RoundToInt((float)(num4 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
					if (VrSquare)
					{
						num5 = num4;
					}
					Renderer.Resize(num4, num5);
				}
			}
			bool cpuWorld = flag3 && !gpuWorld;
			bool flag4 = !flag2 && CanDrawRoomBeside;
			_wasInRoom = !flag2;
			Interior room = r.Room;
			Atmosphere inside = Atmosphere.Indoor(room.CeilingTop, room.CeilingBottom, outside);
			Vec3 world = (air.Natural ? Atmosphere.DaylightFrom(air) : air.Light);
			if (world.Z > 0.08f)
			{
				inside.Light = r.LightInside(world);
				if (air.Natural)
				{
					inside.ShadowLight = inside.Light;
				}
			}
			float daylight = r.Daylight;
			inside.AmbR *= daylight;
			inside.AmbG *= daylight;
			inside.AmbB *= daylight;
			inside.LightR *= daylight;
			inside.LightG *= daylight;
			inside.LightB *= daylight;
			if (Vec3.Dot(room.Floor.LightDir, inside.Light) < 0.999f || _litRoom != room)
			{
				room.Floor.LightDir = inside.Light;
				room.Floor.Relight(0, 0, room.Floor.Width, room.Floor.Height);
				_litRoom = room;
			}
			_roomWindows.Clear();
			_roomWindows.AddRange(room.Windows);
			_roomLights.Clear();
			for (int i = 0; i < room.Lights.Count; i++)
			{
				RoomLight roomLight = room.Lights[i];
				PointLight light = roomLight.Light;
				float num6 = RoomFlicker(i, roomLight.Fire);
				light.R *= num6;
				light.G *= num6;
				light.B *= num6;
				_roomLights.Add(light);
			}
			room.Daylight(1f - air.Night);
			_roomMeshes.Clear();
			_roomVoxels.Clear();
			RoomCombat.Tick(dt);
			if (RoomCombat.Active)
			{
				FirstPersonShots.Update(room.Floor, null);
			}
			r.Collect(_roomVoxels, _roomMeshes, _sprites, _scene.LookAt, dt);
			if (r is BoatCabin cabin)
			{
				Curtains.AddCabin(_roomMeshes, cabin, dt);
			}
			RoomCombat.Blaze(_roomMeshes, _roomLights, dt);
			RoomCombat.DrawShots(_roomMeshes, r.Eye);
			if (!flag)
			{
				BuildHands(_roomVoxels, host, num, 0f, dt);
			}
			_roomHands.Clear();
			_roomHands.AddRange(Renderer.OverlayMeshes);
			Renderer.OverlayMeshes.Clear();
			FrameRenderer renderer = Renderer;
			TerrainCache land = _sampler.Cache;
			float worldTop = _sampler.MaxHeight + 0.45f;
			ViewState eye = _view;
			List<VoxelObject> roomVoxels = _roomVoxels;
			uint[] backdrop = null;
			if (cpuWorld)
			{
				if (_backdrop == null || _backdrop.Length != Renderer.Pixels.Length)
				{
					_backdrop = new uint[Renderer.Pixels.Length];
				}
				backdrop = _backdrop;
				_worldBillboards.Clear();
				_worldBillboards.AddRange(_scene.Billboards);
				_worldVoxels.Clear();
				_worldVoxels.AddRange(_scene.Voxels);
			}
			else if (gpuWorld)
			{
				if (_clearBackdrop == null || _clearBackdrop.Length != Renderer.Pixels.Length)
				{
					_clearBackdrop = new uint[Renderer.Pixels.Length];
				}
				backdrop = _clearBackdrop;
			}
			Action setRoom = delegate
			{
				renderer.Air = inside;
				renderer.Windows.Clear();
				renderer.Windows.AddRange(_roomWindows);
				renderer.Lights.Clear();
				renderer.Lights.AddRange(_roomLights);
				renderer.Meshes.Clear();
				renderer.Meshes.AddRange(_roomMeshes);
				renderer.OverlayMeshes.Clear();
				renderer.OverlayMeshes.AddRange(_roomHands);
				renderer.MaxDistance = 60f;
				renderer.BillboardDistance = 40f;
			};
			Action action = delegate
			{
				if (cpuWorld)
				{
					renderer.Render(land, outside2, _worldBillboards, _worldVoxels, worldTop);
					Array.Copy(renderer.Pixels, backdrop, backdrop.Length);
				}
				setRoom();
				renderer.Backdrop = backdrop;
				renderer.Render(room.Floor, eye, NoBillboards, roomVoxels, room.Top + 0.5f);
				renderer.Backdrop = null;
				if (gpuWorld)
				{
					SeeThroughWindows(renderer);
				}
			};
			bool flag5 = false;
			if (_vrRoomStereo)
			{
				setRoom();
				flag5 = VrRoomStereo(room, eye, roomVoxels, r.Tagged, r.Daylight);
			}
			else if (flag2)
			{
				setRoom();
				_gpu.Backdrop = (gpuWorld ? _cabinWorld : null);
				try
				{
					flag5 = GpuRoomFrame(room, eye, num4, num5, roomVoxels, r.Tagged, r.Daylight);
				}
				finally
				{
					_gpu.Backdrop = null;
				}
				if (!flag5 && num4 > 640)
				{
					num4 = 640;
					num5 = Mathf.Max(90, Mathf.RoundToInt((float)(num4 * Screen.height) / (float)Mathf.Max(1, Screen.width)));
					if (VrSquare)
					{
						num5 = num4;
					}
					Renderer.Resize(num4, num5);
				}
			}
			if (!flag5)
			{
				if (flag4)
				{
					StartBesideRoom(action, eye, num4, num5, r.Tagged, gpuWorld);
				}
				else
				{
					action();
					_picked = ((Renderer.PickTag >= 0 && Renderer.PickTag < r.Tagged.Count) ? r.Tagged[Renderer.PickTag] : null);
					UploadFrame(num4, num5, gpuWorld);
				}
			}
			FrameMs = (float)(_frameClock.Elapsed.TotalMilliseconds + _besideMainMs);
			_renderMs = Mathf.Lerp(_renderMs, FrameMs, 0.1f);
			AimIndoors(host, num, 0f, room.Floor);
			Feedback.Update(host, dt, r.Tagged, (float x, float y) => 0f);
			ForgeUpdate(host, dt);
			Work.Target = null;
			_audio.Update(new FirstPersonAudio.State
			{
				Position = r.Where,
				EyeAboveGround = _view.Z,
				Yaw = _view.Yaw,
				Pitch = _view.Pitch,
				Indoors = true,
				Moving = Moving,
				Ground = ground,
				Steps = _gait.Steps,
				Stride = _gait.Stride,
				Health = HealthFraction(host),
				Attacks = Feedback.Attacks,
				Swing = Feedback.LastSwing,
				Melee = Feedback.LastAttackMelee,
				WeaponId = (Holstered ? null : WeaponOf(host)),
				Hits = Feedback.Hits,
				Kills = Feedback.Kills,
				Hurts = Feedback.Hurts,
				HearthDistance = ((room.HearthX > 0f) ? new Vector2(room.HearthX - r.Eye.x, room.HearthY - r.Eye.y).magnitude : (-1f)),
				Wind = (flag3 ? (wind + 0.3f * _overcast + 0.3f * _scene.Gusts.Strength) : 0f),
				Rain = (flag3 ? _rain : 0f),
				Hour = _day.Hour,
				Night = air.Night,
				Habitat = _habitat
			}, dt);
			if (r is MineInterior mineInterior && mineInterior.Mine != null)
			{
				FirstPersonAudio.InsideFor(FirstPersonAudio.Footprint(mineInterior.Mine));
			}
			else
			{
				FirstPersonAudio.InsideFor(new Rect(r.Where.x - 2f, r.Where.y - 2f, 4f, 4f));
			}
			PossessionHooks.AimPoint = host.current_position;
			Talk.Blocked = TalkBusy || Hub.AnyModeActive || AnyMenu;
			Talk.Indoors = true;
			Talk.Update(host, _picked, r.DistanceTo, (Actor a2) => r.HeadOf(a2, _sprites), _scene.LookAt, r.Eye, Toast, dt);
			_roomTalk = true;
			try
			{
				CivilUpdate(host, dt);
			}
			finally
			{
				_roomTalk = false;
			}
		}

		private void KeepCabinWorld()
		{
			RenderTexture output = _gpu.Output;
			if (output == null)
			{
				return;
			}
			if (_cabinWorld == null || _cabinWorld.width != output.width || _cabinWorld.height != output.height)
			{
				if (_cabinWorld != null)
				{
					_cabinWorld.Release();
					UnityEngine.Object.Destroy(_cabinWorld);
				}
				_cabinWorld = new RenderTexture(output.width, output.height, 0, output.format)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonCabinWorld"
				};
				_cabinWorld.Create();
			}
			if (SystemInfo.copyTextureSupport != CopyTextureSupport.None)
			{
				Graphics.CopyTexture(output, _cabinWorld);
			}
			else
			{
				Graphics.Blit(output, _cabinWorld);
			}
		}

		private static void SeeThroughWindows(FrameRenderer r)
		{
			byte[] portalMask = r.PortalMask;
			uint[] pixels = r.Pixels;
			if (portalMask != null && portalMask.Length == pixels.Length)
			{
				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = ((portalMask[i] == 0) ? (pixels[i] | 0xFF000000u) : 0u);
				}
			}
		}

		private float IndoorPace(Actor host, Vector2 keys)
		{
			float num = ((host != null) ? PossessionHooks.FullSpeed(host) : 3f);
			float num2 = (((PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift)) && keys.y > 0.1f) ? 1f : (PossessionHooks.Walking ? 0.35f : PossessionHooks.RunFactor));
			Vector2 vector = ((keys.sqrMagnitude > 0f) ? keys.normalized : Vector2.up);
			num2 *= ((vector.y >= 0f) ? Mathf.Lerp(0.85f, 1f, vector.y) : Mathf.Lerp(0.85f, 0.7f, 0f - vector.y));
			return Mathf.Clamp(num * num2, 1.2f, 4f);
		}

		private void RoyalVisit(Building b)
		{
			try
			{
				Actor host = _host;
				if (b == null || host == null || !Rep.Reigns(host, b.kingdom) || Homes.OwnedBy(b, host) || Workbench.HomeOf(host) == b || (b == _royalVisited && Time.unscaledTime - _royalVisitAt < 120f))
				{
					return;
				}
				HouseOccupancy.Scan();
				foreach (Actor item in HouseOccupancy.Of(b).Inside)
				{
					if (item != null && item != host && item.isAlive() && !HouseOccupancy.Asleep(item) && Dialogue.IsPerson(item) && !HomeLock.IsFamily(host, item))
					{
						Talk.Aside(item, Dialogue.Speak(Dialogue.VoiceOf(item, host), WelcomeLine(item, host)));
						_royalVisited = b;
						_royalVisitAt = Time.unscaledTime;
						RoyalWelcomes++;
						Log.Info("royal visit: " + Describe(item) + " welcomes you to " + HouseName(b));
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("royal visit: " + ex.Message);
			}
		}

		private static string WelcomeLine(Actor a, Actor you)
		{
			string text = Dialogue.Majesty(you);
			string text2 = char.ToUpper(text[0]) + text.Substring(1);
			bool flag = false;
			try
			{
				flag = !a.isAdult();
			}
			catch
			{
			}
			if (flag)
			{
				return Dialogue.Pick("*gasps and bows* " + text2 + "!", "*runs to bow* " + text2 + "!");
			}
			return Dialogue.Pick("*bows* " + text2 + "! You honour our home.", "*rises and bows* " + text2 + ". Welcome.", "*bows low* " + text2 + "! Had we known, we'd have swept.", text2 + "! Please, come in.");
		}

		internal void Celebrate(float seconds)
		{
			_celebrateUntil = Time.unscaledTime + seconds;
		}

		private void CelebrationSky()
		{
			if (!(_celebrateUntil < 0f))
			{
				if (Time.unscaledTime < _celebrateUntil)
				{
					Renderer.ForceAurora = 0.9f;
					Renderer.ForceShower = 1f;
				}
				else
				{
					Renderer.ForceAurora = (Renderer.ForceShower = -1f);
					_celebrateUntil = -1f;
				}
			}
		}

		private void RunKeys()
		{
			if (_active && _host != null && DeathTimer < 0f)
			{
				Run.Tick(_host, Time.unscaledDeltaTime);
			}
			if (_active && _host != null && DeathTimer < 0f && Run.LockedOn(_host))
			{
				GreatWar.Tick(_host, Time.unscaledDeltaTime, Toast);
			}
			Founding.Update(Time.unscaledDeltaTime);
			TerrainTick();
			CelebrationSky();
			Run.CheckWorld();
			if (!_active && DeathTimer < 0f && Run.Current != null && Run.Body != null && !Run.WindowOpen && !WorldLoading)
			{
				bool flag = false;
				bool flag2 = false;
				try
				{
					flag = Run.Body.isAlive();
					flag2 = ControllableUnit.isControllingUnit() && ControllableUnit._unit_main == Run.Body;
				}
				catch
				{
				}
				if (flag && !flag2)
				{
					if (!_runLater)
					{
						Run.Asking = Run.Ask.LeftBody;
					}
				}
				else if (flag2)
				{
					_runLater = false;
				}
				else if (!flag && _runBodyHeld && !WorldSync.Mirroring)
				{
					RunDiedFromAbove();
				}
				else if (!flag)
				{
					Run.End("died while you were away", abandoned: false);
				}
				if (flag)
				{
					_runBodyHeld = flag2;
				}
			}
			else if (_active && Run.Body != null && Run.Body == _host)
			{
				_runBodyHeld = true;
				_runLater = false;
			}
			if (Run.Current == null)
			{
				_runLater = false;
			}
			if (_runAsked)
			{
				_runAsked = false;
				RunButtonPressed();
			}
			if (!IsTyping() && Run.WindowOpen && PlayInput.GetKeyDown(KeyCode.Escape))
			{
				if (Run.LegacyOpen)
				{
					Run.LegacyOpen = false;
				}
				else if (Run.Asking == Run.Ask.Abandon)
				{
					Run.Asking = Run.Ask.None;
				}
				else if (Run.Asking == Run.Ask.LeftBody)
				{
					RunLater();
				}
				Run.LockInOpen = false;
			}
		}

		internal void RunLater()
		{
			Run.Asking = Run.Ask.None;
			_runLater = true;
			Toast("The run waits. Take " + ((Run.Body != null) ? Run.SafeName(Run.Body) : "its person") + " again to go on");
		}

		internal void AskRun()
		{
			_runAsked = true;
		}

		private void RunButtonPressed()
		{
			if (!Run.WindowOpen && _active && _host != null && !(DeathTimer >= 0f))
			{
				string why;
				if (Run.Locked)
				{
					Run.Asking = Run.Ask.Abandon;
					Run.LeapTo = null;
				}
				else if (NetSession.Active && NetSession.Current.Rules.Runs == 1)
				{
					Toast("The host turned lock-in runs off in this world");
				}
				else if (Run.CanLock(_host, out why))
				{
					Run.LockInOpen = true;
					Run.Tab = 0;
					Run.Picked.Clear();
				}
				else
				{
					Toast(why);
				}
			}
		}

		internal void RunButtonForTest()
		{
			RunButtonPressed();
		}

		private bool RunHoldsLeap(Actor target)
		{
			if (!Run.Locked)
			{
				return false;
			}
			Run.Asking = Run.Ask.Abandon;
			Run.LeapTo = target;
			return true;
		}

		internal bool RunHoldsLeapForTest(Actor target)
		{
			return RunHoldsLeap(target);
		}

		private void RunOnDeath(Actor dead)
		{
			if (Run.LockedOn(dead))
			{
				Run.OnDeath(dead);
				int count = Heirs.Count;
				Heirs.RemoveAll((TreeNode h) => !Run.HasHome(h.Actor));
				if (Heirs.Count < count)
				{
					Log.Info($"run: {count - Heirs.Count} of {count} heirs have no home and can't carry the run on");
				}
			}
		}

		private void RunDiedFromAbove()
		{
			_runBodyHeld = false;
			Actor body = Run.Body;
			try
			{
				FindHeirs(body);
				RunOnDeath(body);
			}
			catch (Exception ex)
			{
				Log.Error("run: heirs from above: " + ex.Message);
				Heirs.Clear();
			}
			if (Heirs.Count > 0 && ContinueAs(0))
			{
				Log.Info("run: died while watching from above; carried on by the eldest heir with a home");
				return;
			}
			Heirs.Clear();
			Run.End("died", abandoned: false);
		}

		private void RunDeathOver()
		{
			if (Run.Current != null && Run.Body == _host)
			{
				Run.End("died", abandoned: false);
			}
		}

		internal void RunAnswer(bool abandon)
		{
			Run.Ask asking = Run.Asking;
			Actor leapTo = Run.LeapTo;
			Actor body = Run.Body;
			Run.Asking = Run.Ask.None;
			Run.LeapTo = null;
			if (!abandon)
			{
				if (asking == Run.Ask.LeftBody && body != null && body.isAlive() && !Possess.Swap(body))
				{
					Toast("Couldn't go back into " + Run.SafeName(body));
				}
				return;
			}
			Run.End((asking == Run.Ask.LeftBody) ? "let go" : ((leapTo != null) ? "left for another body" : "gave up"), abandoned: true);
			if (leapTo != null && leapTo.isAlive())
			{
				Possess.JumpTo(leapTo);
			}
		}

		private void DrawRun()
		{
			DrawTerrainCard();
			if (Run.WindowOpen || (_active && Run.Locked))
			{
				_hud.DrawRun(this);
			}
		}

		private void SceneSplitLog(bool report)
		{
			if (report && ++_splitWindows % 6 != 0)
			{
				return;
			}
			if (!report)
			{
				_splitWindows = 0;
				Hitch.TakeAllocRate(1f);
				Hitch.TakeGarbageSplit(1f);
				Hitch.TakeSpikes(1f);
			}
			if (report)
			{
				SceneClock.Split split = SceneClock.Snapshot();
				if (split.Passes > 0)
				{
					Log.Info($"scene split: crowd {split[SceneClock.B.Crowd]:F2}, ticks {split[SceneClock.B.Ticks]:F2}, begin {split[SceneClock.B.Begin]:F2}, units {split[SceneClock.B.Units]:F2} (checks {split[SceneClock.B.Checks]:F2}, steer {split[SceneClock.B.Steer]:F2}, track {split[SceneClock.B.Track]:F2} [route {split[SceneClock.B.Route]:F2}, step {split[SceneClock.B.Step]:F2}, face {split[SceneClock.B.Face]:F2}], sprites {split[SceneClock.B.Sprites]:F2}, ground {split[SceneClock.B.Ground]:F2}, " + $"people {split[SceneClock.B.People]:F2} [figure {split[SceneClock.B.Figure]:F2}, pose {split[SceneClock.B.Pose]:F2}, extras {split[SceneClock.B.Extras]:F2}, place {split[SceneClock.B.Place]:F2}, head {split[SceneClock.B.Head]:F2}, gear {split[SceneClock.B.Gear]:F2}], " + $"creatures {split[SceneClock.B.Creatures]:F2}, other {split[SceneClock.B.Other]:F2}, far {split[SceneClock.B.Far]:F2}), buildings {split[SceneClock.B.Buildings]:F2}, far props {split[SceneClock.B.FarProps]:F2}, fx {split[SceneClock.B.Fx]:F2}, rest {split[SceneClock.B.Rest]:F2} ms; " + $"units {split[SceneClock.N.Visited]:F0} visited, {split[SceneClock.N.Tracked]:F0} tracked, {split[SceneClock.N.Steered]:F0} steered, {split[SceneClock.N.Behind]:F0} behind, people {split.People:F0} (<30 {split[SceneClock.N.Near]:F0}, 30-60 {split[SceneClock.N.Mid]:F0}, >60 {split[SceneClock.N.Distant]:F0}), " + $"creatures {split[SceneClock.N.Creatures]:F0}, far {split[SceneClock.N.Far]:F0}; steering {split[SceneClock.N.Walkers]:F0} walkers {split.SteerMs:F2} ms ({split.Passes} passes, {split.Sampled} timed)");
				}
				if (split.Passes > 0)
				{
					Log.Info($"scene savings a frame: {(float)(People.Glided - _glided0) / (float)split.Passes:F0} people glided, {(float)(SceneCollector.OffscreenSkipped - _offscreen0) / (float)split.Passes:F0} off-screen creatures skipped, " + $"{(float)(SceneCollector.TreesKept - _kept0) / (float)split.Passes:F0} trees kept, {(float)(SceneCollector.CellsSkipped - _cells0) / (float)split.Passes:F0} building cells skipped, {(float)(People.Coasted - _coasted0) / (float)split.Passes:F0} coasting on the card");
				}
				if (split.Frames > 0)
				{
					Log.Info($"frames: p50 {split.P50:F1}, p95 {split.P95:F1}, p99 {split.P99:F1}, worst {split.Worst:F1} ms, {split.Over16} of {split.Frames} over 16.7 ms, {split.Over33} over 33 ms; gc0 +{split.Gc0}, heap {split.HeapMb:F0} MB, garbage {Hitch.TakeAllocRate(split.Seconds):F1} MB/s");
				}
				string text = Hitch.TakeGarbageSplit(split.Seconds);
				if (text != null && split.Frames > 0)
				{
					Log.Info("garbage by part (MB/s): " + text);
				}
				string text2 = Hitch.TakeSpikes(split.Seconds);
				if (text2 != null && split.Frames > 0)
				{
					Log.Info("spikes: " + text2);
				}
				if (!_splitScreenLogged)
				{
					_splitScreenLogged = true;
					string text3 = "?";
					try
					{
						text3 = Screen.currentResolution.refreshRateRatio.value.ToString("F1");
					}
					catch
					{
					}
					Log.Info($"screen: refresh {text3} Hz, vsync {QualitySettings.vSyncCount}, target fps {Application.targetFrameRate}, {Screen.width}x{Screen.height}");
				}
			}
			SceneClock.ResetWindow();
			_glided0 = People.Glided;
			_offscreen0 = SceneCollector.OffscreenSkipped;
			_kept0 = SceneCollector.TreesKept;
			_cells0 = SceneCollector.CellsSkipped;
			_coasted0 = People.Coasted;
		}

		internal string SceneRefusal()
		{
			if (!_active || _host == null || DeathTimer >= 0f)
			{
				return "Only while you are in a body";
			}
			if (VrRunning)
			{
				return "The scene can't be viewed in a headset";
			}
			if (_dive != Dive.None || _leaping)
			{
				return "Not while leaping";
			}
			return null;
		}

		internal bool EnterScene(MapTabs.Page page, Vector2 at)
		{
			string text = SceneRefusal();
			if (text != null)
			{
				Toast(text);
				return false;
			}
			Scene.Open(page, at);
			_sceneHost = _host;
			_sceneHurts = Feedback.Hurts;
			_sceneHitAge = Feedback.HitFromAge;
			_sceneHealth = HealthFraction(_host);
			_sceneDrawn = (_sceneShown = default(SceneFrame));
			_scenePerfSince = 0f;
			_sceneMs = 0.0;
			_sceneFrames = 0;
			DropBinoculars();
			DropCompass();
			_hud.SceneReset();
			Log.Info($"scene view: from the {page} page at {at.x:F0},{at.y:F0}, zoom {Scene.Zoom:F0}");
			return true;
		}

		internal void LeaveScene(bool toMap)
		{
			if (!Scene.On)
			{
				return;
			}
			if (toMap)
			{
				if (Scene.Page == MapTabs.Page.War)
				{
					WarMap.Centre = Scene.At;
				}
				else
				{
					SoldierMap.Centre = Scene.At;
				}
			}
			else
			{
				CloseMapWindow();
			}
			EndScene(toMap ? "back to the map" : "back to the eyes");
		}

		private void EndScene(string why)
		{
			if (!Scene.On)
			{
				return;
			}
			Scene.On = false;
			Scene.StopFollow();
			Scene.TestMove = (Scene.TestTurn = null);
			_sceneWasOn = true;
			PossessionHooks.SceneHoldsKeys = false;
			try
			{
				Camera camera = ((World.world != null) ? World.world.camera : null);
				if (camera != null)
				{
					camera.transform.position = new Vector3(_lastPos.x, _lastPos.y, camera.transform.position.z);
				}
			}
			catch
			{
			}
			Log.Info("scene view: " + why);
		}

		private bool SceneEscape()
		{
			if (!Scene.On)
			{
				return false;
			}
			LeaveScene(toMap: true);
			return true;
		}

		internal bool SceneEscapeForTest()
		{
			return SceneEscape();
		}

		private bool SceneShows()
		{
			if (Scene.On)
			{
				bool flag = ((Scene.Page == MapTabs.Page.War) ? WarMap.Open : (SoldierMap.Open && !SoldierMap.Founder));
				bool flag2 = Craft.Open || Royal.Open || Royal.ObeliskOpen || _settingsMenu.Open || Converse.Open || WorldNews.Open || Journal.Open || FamilyMap.Open || Law.BoardOpen || Births.Naming != null || ChoosingHeir || Hub.AnyWindowOpen || Run.WindowOpen;
				if (!flag || flag2 || SceneRefusal() != null || _host != _sceneHost)
				{
					EndScene((!flag) ? "its page closed" : (flag2 ? "another window opened" : "it can't show now"));
				}
				else
				{
					if (Feedback.Hurts == _sceneHurts || !SceneHurtSendsBack(out var struck))
					{
						_sceneHitAge = Feedback.HitFromAge;
						_sceneHealth = Mathf.Max(_sceneHealth, HealthFraction(_host));
						PossessionHooks.SceneHoldsKeys = true;
						return true;
					}
					CloseMapWindow();
					EndScene(struck ? "your body was struck" : "your body was hurt");
					Toast(struck ? "You are struck. Back in your body" : "You are hurt. Back in your body");
				}
			}
			PossessionHooks.SceneHoldsKeys = false;
			if (_sceneWasOn)
			{
				ResetGodRenderer();
				_sceneWasOn = false;
				_picked = null;
				_sceneEndedFrame = Time.frameCount;
			}
			return false;
		}

		private void KeepRoomUnderScene(float dt)
		{
			if (DeathTimer >= 0f)
			{
				return;
			}
			if (Cabin.Inside)
			{
				Actor boat = Cabin.Boat;
				bool flag = false;
				try
				{
					flag = boat?.isAlive() ?? false;
				}
				catch
				{
				}
				if (!flag || !Boats.IsAboard(_host, boat))
				{
					LeaveCabin(flag ? null : "The boat is going down!");
					return;
				}
				HoldBoat(boat);
			}
			else if (Mine.Inside)
			{
				if (!Mine.Standing())
				{
					LeaveMine("The mine has fallen in!");
					return;
				}
				Mine.KeepIndoors();
			}
			else
			{
				if (!Interior.Inside)
				{
					return;
				}
				if (!Interior.Virtual && !HouseInterior.IsEnterable(Interior.House))
				{
					ExitHouse();
					Toast("The house is gone!");
					return;
				}
				Interior.KeepIndoors();
			}
			RoomCombat.Tick(dt);
		}

		private bool SceneHurtSendsBack(out bool struck)
		{
			float num = HealthFraction(_host);
			struck = Feedback.HitFromAge < _sceneHitAge - 0.0001f;
			bool flag = _sceneHealth - num >= 0.1f || num < 0.25f;
			_sceneHurts = Feedback.Hurts;
			_sceneHealth = num;
			if (struck || flag)
			{
				return true;
			}
			SceneHurtAt = Time.unscaledTime;
			return false;
		}

		private void SceneOff()
		{
			EndScene("first person ended");
			PossessionHooks.SceneHoldsKeys = false;
			if (_sceneWasOn)
			{
				ResetGodRenderer();
			}
			_sceneWasOn = false;
		}

		private void OnApplicationFocus(bool focus)
		{
			if (!focus && _hud != null)
			{
				_hud.SceneMouseLost();
			}
		}

		internal void SceneFollowKey()
		{
			if (Scene.Following)
			{
				Scene.StopFollow();
				Toast("Follow off (F)");
				return;
			}
			Actor sceneHover = _hud.SceneHover;
			if (sceneHover != null)
			{
				Scene.StopFollow();
				Scene.Follow = sceneHover;
				Toast("Following " + Describe(sceneHover) + " (F)");
				return;
			}
			Vector2 middle;
			int num = ((Scene.Page == MapTabs.Page.War) ? MarkedMiddle(out middle) : 0);
			if (num > 0)
			{
				Scene.StopFollow();
				Scene.FollowMarked = true;
				Toast("Following " + num + ((num == 1) ? " marked warrior (F)" : " marked warriors (F)"));
			}
			else
			{
				Toast((Scene.Page == MapTabs.Page.War) ? "Point at a warrior or mark a band to follow" : "Point at a warrior to follow");
			}
		}

		private int MarkedMiddle(out Vector2 middle)
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			foreach (Actor item in WarMap.Marked)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						zero += item.current_position;
						num++;
					}
				}
				catch
				{
				}
			}
			middle = ((num > 0) ? (zero / num) : default(Vector2));
			return num;
		}

		private void SteerScene(float dt)
		{
			bool num = !IsTyping() && !_settingsMenu.Open;
			Vector2 zero = Vector2.zero;
			float num2 = 0f;
			float num3 = 0f;
			if (num)
			{
				if (PlayInput.GetKey(KeyCode.W))
				{
					zero.y += 1f;
				}
				if (PlayInput.GetKey(KeyCode.S))
				{
					zero.y -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.D))
				{
					zero.x += 1f;
				}
				if (PlayInput.GetKey(KeyCode.A))
				{
					zero.x -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.LeftArrow))
				{
					num2 += 1f;
				}
				if (PlayInput.GetKey(KeyCode.RightArrow))
				{
					num2 -= 1f;
				}
				if (PlayInput.GetKey(KeyCode.UpArrow))
				{
					num3 += 1f;
				}
				if (PlayInput.GetKey(KeyCode.DownArrow))
				{
					num3 -= 1f;
				}
				if (PlayInput.GetKeyDown(KeyCode.F))
				{
					SceneFollowKey();
				}
			}
			bool fast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
			Vector2? band = null;
			if (Scene.FollowMarked && MarkedMiddle(out var middle) > 0)
			{
				band = middle;
			}
			Scene.Update(dt, zero, num2, num3, fast, band, SceneZoomFar);
		}

		private void RenderScene(float dt)
		{
			Actor host = _host;
			TerrainCache cache = _sampler.Cache;
			NoteSceneShown();
			int num = RenderWidth(GpuWanted);
			int num2 = Mathf.Max(90, Mathf.RoundToInt((float)(num * Screen.height) / (float)Mathf.Max(1, Screen.width)));
			SteerScene(dt);
			Vector2 at = Scene.At;
			float num3 = GodGroundUnder(cache, at.x, at.y, Scene.Zoom);
			Scene.Ground = (float.IsNaN(Scene.Ground) ? num3 : Mathf.Lerp(Scene.Ground, num3, 1f - Mathf.Exp(-4f * dt)));
			KeepPivot(cache, num, num2);
			at = Scene.At;
			if (host != null)
			{
				Vector2 vector = host.current_position - _lastPos;
				if (vector.sqrMagnitude < 25f)
				{
					_view.X += vector.x;
					_view.Y += vector.y;
				}
				_lastPos = host.current_position;
			}
			Atmosphere air = _day.Air;
			Renderer.Air = air;
			Renderer.Windows.Clear();
			FollowLight(air);
			_wasInRoom = false;
			ViewState viewState = Scene.Camera(num, num2);
			float num4 = GodCamera.Reach(viewState, num, num2);
			SightCut cut = SightCut.Make(viewState, num, num2, at.x, at.y, Scene.Ground, 1f);
			bool flag = CanDrawBeside(dead: false);
			_frameClock.Restart();
			if (flag)
			{
				_sampler.TickLandforms(dt);
			}
			else
			{
				_sampler.Tick(at.x, at.y);
			}
			UpdateFeatures(host, at, dead: false, dt);
			_sprites.BeginFrame();
			Renderer.Resize(num, num2);
			Renderer.MaxDistance = num4;
			Renderer.BillboardDistance = num4;
			Renderer.FarModelReach = 0f;
			Renderer.FadeAt = 0f;
			Renderer.HazeScale = 1f;
			Renderer.Time = SeaNow;
			Renderer.RippleStrength = 0f;
			Renderer.ShadowFocusSet = true;
			Renderer.ShadowFocusX = at.x;
			Renderer.ShadowFocusY = at.y;
			_creatureReach = 0f;
			_scene.SkyReach = num4;
			_scene.SkyPixel = 1f / ((float)num * 0.5f / Mathf.Tan(viewState.Fov * 0.5f));
			_scene.SkyFocus = at;
			double totalMilliseconds = _frameClock.Elapsed.TotalMilliseconds;
			_scene.SteerAt = at;
			try
			{
				CollectScene(viewState, viewState, host, 1f, air, dt);
			}
			finally
			{
				_scene.SkyReach = 0f;
				_scene.SteerAt = null;
			}
			ApplyWeather(air, at, dt);
			Renderer.Meshes.Clear();
			Renderer.Meshes.AddRange(_scene.Meshes);
			Renderer.Lights.Clear();
			Renderer.Lights.AddRange(_scene.Lights);
			Dragons.EmitTrails(Renderer.Streaks);
			Renderer.Streaks.AddRange(_scene.Streaks);
			ApplyDiveCut(cut);
			Renderer.OverlayMeshes.Clear();
			Renderer.PrecipX = at.x;
			Renderer.PrecipY = at.y;
			Renderer.PrecipCell = Mathf.Max(1f, Scene.Zoom / 12f);
			double collectMs = _frameClock.Elapsed.TotalMilliseconds - totalMilliseconds;
			_sceneDrawn = new SceneFrame
			{
				Set = true,
				Camera = viewState,
				At = at,
				Cut = cut
			};
			if (SceneGpuFrame(viewState, num, num2))
			{
				NoteSceneShown();
			}
			else if (flag)
			{
				StartBesideRender(viewState, num, num2, null, 1f, Scene.Ground, at, collectMs);
				_besideScene = true;
				_eyeNow = _shown.View;
			}
			else
			{
				if (num > 640)
				{
					num = 640;
					num2 = Mathf.Max(90, Mathf.RoundToInt((float)(num * Screen.height) / (float)Mathf.Max(1, Screen.width)));
					Renderer.Resize(num, num2);
					viewState = Scene.Camera(num, num2);
					_sceneDrawn.Camera = viewState;
				}
				Renderer.Render(cache, viewState, _scene.Billboards, _scene.Voxels, _sampler.MaxHeight + 0.45f);
				UploadFrame(num, num2);
				NoteSceneShown();
			}
			_picked = null;
			Prompt = null;
			FrameMs = (float)(_frameClock.Elapsed.TotalMilliseconds + _besideMainMs);
			_renderMs = Mathf.Lerp(_renderMs, FrameMs, 0.1f);
			ScenePerf(dt);
			Feedback.Update(host, dt, _scene.Tagged, (float x, float y) => cache.SampleHeight(x, y));
			_habitat.Update(at, _scene, cache, dt);
			UpdateTown(host);
			FirstPersonAudio.State s = OutdoorSound(at, Scene.Ground, dead: false, wantsToMove: false);
			s.EyeAboveGround = 3f;
			s.Yaw = viewState.Yaw;
			s.Pitch = 0f;
			s.Moving = (s.Swimming = (s.Shallows = (s.Airborne = false)));
			_audio.Update(s, dt);
			SceneEarForTest = at;
			Craft.Update(host, dt, Toast);
			CivilUpdate(host, dt);
			Camera camera = World.world.camera;
			if (camera != null)
			{
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
			}
		}

		private void KeepPivot(TerrainCache cache, int width, int height)
		{
			if (!Scene.Pivot(out var ground, out var mouse) || cache == null || cache.Width <= 0)
			{
				return;
			}
			float column = mouse.x / (float)Mathf.Max(1, Screen.width) * (float)width;
			float row = mouse.y / (float)Mathf.Max(1, Screen.height) * (float)height;
			for (int i = 0; i < 2; i++)
			{
				ViewState camera = Scene.Camera(width, height);
				SightCut cut = SightCut.Make(camera, width, height, Scene.At.x, Scene.At.y, Scene.Ground, 1f);
				if (!GodCamera.GroundPoint(cache, camera, width, height, column, row, cut, out var point))
				{
					break;
				}
				Vector2 vector = ground - new Vector2(point.X, point.Y);
				if (vector.sqrMagnitude > 900f)
				{
					break;
				}
				Scene.At = new Vector2(Mathf.Clamp(Scene.At.x + vector.x, 0f, WarMap.WorldWidth), Mathf.Clamp(Scene.At.y + vector.y, 0f, WarMap.WorldHeight));
			}
		}

		private bool SceneGpuFrame(ViewState camera, int width, int height)
		{
			if (!GpuWanted)
			{
				return false;
			}
			if (_gpu == null)
			{
				_gpu = new GpuRenderer();
			}
			_gpu.ShadowReach = Mathf.Clamp(Scene.Zoom * 2.2f, GpuRenderer.EyeShadowReach, 200f);
			try
			{
				return GpuPlainFrame(camera, width, height, "the scene view");
			}
			finally
			{
				_gpu.ShadowReach = GpuRenderer.EyeShadowReach;
			}
		}

		private void NoteSceneShown()
		{
			if (_sceneDrawn.Set)
			{
				ViewState view = _shown.View;
				ViewState camera = _sceneDrawn.Camera;
				if (Mathf.Abs(view.X - camera.X) < 0.001f && Mathf.Abs(view.Y - camera.Y) < 0.001f && Mathf.Abs(view.Z - camera.Z) < 0.001f && Mathf.Abs(view.Yaw - camera.Yaw) < 0.0001f && Mathf.Abs(view.Pitch - camera.Pitch) < 0.0001f)
				{
					_sceneShown = _sceneDrawn;
				}
			}
		}

		internal bool SceneProject(Vector3 p, out Vector2 gui)
		{
			gui = default(Vector2);
			FrameProjection shown = _shown;
			if (!_sceneShown.Set || shown.Width <= 0 || shown.Height <= 0)
			{
				return false;
			}
			if (!GodTurn.Project(_sceneShown.Camera, shown.Width, shown.Height, new Vec3(p.x, p.y, p.z), out var column, out var row))
			{
				return false;
			}
			gui = new Vector2(column / (float)shown.Width * (float)Screen.width, row / (float)shown.Height * (float)Screen.height);
			return true;
		}

		internal bool SceneGround(Vector2 gui, out Vector2 point)
		{
			point = default(Vector2);
			FrameProjection shown = _shown;
			TerrainCache cache = _sampler.Cache;
			if (!_sceneShown.Set || shown.Width <= 0 || shown.Height <= 0 || cache == null || cache.Width <= 0)
			{
				return false;
			}
			float column = gui.x / (float)Mathf.Max(1, Screen.width) * (float)shown.Width;
			float row = gui.y / (float)Mathf.Max(1, Screen.height) * (float)shown.Height;
			if (!GodCamera.GroundPoint(cache, _sceneShown.Camera, shown.Width, shown.Height, column, row, _sceneShown.Cut, out var point2))
			{
				return false;
			}
			point = new Vector2(point2.X, point2.Y);
			return true;
		}

		internal float SceneGroundZ(Vector2 p)
		{
			return Mathf.Max(_sampler.Cache.SampleHeight(p.x, p.y), 0f);
		}

		private void ScenePerf(float dt)
		{
			_sceneMs += FrameMs;
			_sceneFrames++;
			if (_scenePerfSince <= 0f)
			{
				_scenePerfSince = Time.unscaledTime;
			}
			if (!(Time.unscaledTime - _scenePerfSince < 10f))
			{
				float b = Time.unscaledTime - _scenePerfSince;
				Log.Info(string.Format("scene view perf: {0:F0} fps, {1:F1} ms of the game's frame, zoom {2:F0}, {3}x{4} on the {5}", (float)_sceneFrames / Mathf.Max(0.01f, b), _sceneMs / (double)Mathf.Max(1, _sceneFrames), Scene.Zoom, Renderer.Width, Renderer.Height, GpuWanted ? "card" : "CPU"));
				_scenePerfSince = Time.unscaledTime;
				_sceneMs = 0.0;
				_sceneFrames = 0;
			}
		}

		internal Vector2? NextSceneBattle(List<Vector2> battles, Vector2 from, bool fresh)
		{
			if (battles == null || battles.Count == 0)
			{
				return null;
			}
			if (fresh)
			{
				_battlesSeen.Clear();
			}
			for (int i = 0; i < 2; i++)
			{
				Vector2? result = null;
				float num = float.MaxValue;
				foreach (Vector2 battle in battles)
				{
					bool flag = !fresh && (battle - from).sqrMagnitude < 144f;
					foreach (Vector2 item in _battlesSeen)
					{
						if ((battle - item).sqrMagnitude < 144f)
						{
							flag = true;
						}
					}
					if (!flag)
					{
						float sqrMagnitude = (battle - from).sqrMagnitude;
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							result = battle;
						}
					}
				}
				if (result.HasValue)
				{
					_battlesSeen.Add(result.Value);
					return result;
				}
				_battlesSeen.Clear();
				_battlesSeen.Add(from);
			}
			return battles[0];
		}

		private void UpdateSea(float dt)
		{
			Sea.Now = SeaNow;
			Sea.EyeX = _view.X;
			Sea.EyeY = _view.Y;
			float num = 0f;
			if (Renderer != null)
			{
				num = Mathf.Clamp01((Renderer.SeaState(_view.X, _view.Y) - 1f) / 0.8f);
			}
			float num2 = _scene.TwisterNear(_view.X, _view.Y, 100f);
			if (num2 < 100f)
			{
				num = Mathf.Max(num, 1f - num2 / 140f);
			}
			if (SeaStormForTest >= 0f)
			{
				_seaStorm = SeaStormForTest;
			}
			else
			{
				_seaStorm += (num - _seaStorm) * (1f - Mathf.Exp((0f - Mathf.Max(0f, dt)) / 25f));
			}
			Weather weather = Renderer?.Weather;
			Sea.Set(_seaStorm, weather?.WindX ?? 1f, weather?.WindY ?? 0f);
		}

		private void UpdateService(Actor host, float dt)
		{
			if (host != _serviceFor)
			{
				_serviceFor = host;
				Service.Reset();
				SoldierMap.Close();
			}
			try
			{
				Service.Update(host, Royal, WarMap, dt, Toast, PlayHorn);
				Orders = Service.OrderFor(host, Royal, WarMap);
				Charge = Service.CommissionOf(host);
			}
			catch (Exception ex)
			{
				if (Time.unscaledTime - _serviceErrorAt > 30f)
				{
					_serviceErrorAt = Time.unscaledTime;
					Log.Error("service: " + ex);
				}
			}
			if (!SoldierMap.Open)
			{
				return;
			}
			if (Craft.Open || Royal.Open || Royal.ObeliskOpen || WarMap.Open || _settingsMenu.Open || Converse.Open)
			{
				SoldierMap.Close();
				return;
			}
			SoldierMap.Update(host, Royal, WarMap, dt);
			try
			{
				Minimap.UpdateWorld(Land, HostMap, dt);
			}
			catch (Exception ex2)
			{
				Log.Error("soldier map land: " + ex2.Message);
			}
		}

		private bool FounderPrompt(Actor host)
		{
			if (Charge == null || AnyMenu || (Prompt != null && Prompt.Contains("E:")) || Service.FoundableHere(host) == null)
			{
				return false;
			}
			Prompt = ((Prompt == null) ? "E:Found a village here" : ("E:Found a village here|" + Prompt));
			if (PlayInput.GetKeyDown(KeyCode.E) && !IsTyping() && Service.Found(host, Toast) != null)
			{
				PlayHorn();
				Charge = null;
			}
			return true;
		}

		private void AddServiceMarks(List<KeyValuePair<float, Color>> marks, Vector2 p)
		{
			Order orders = Orders;
			if (orders != null)
			{
				if (orders.HasWhere)
				{
					Mark(orders.Where, (orders.Kind == OrderKind.Patrol) ? new Color(1f, 0.78f, 0.6f) : ((orders.Kind == OrderKind.Muster) ? new Color(1f, 0.84f, 0.3f) : new Color(1f, 0.32f, 0.24f)), (orders.Kind == OrderKind.Patrol) ? 1.5f : 3f);
				}
				try
				{
					if (orders.Captain != null && !orders.YouLead && orders.Captain.isAlive())
					{
						Mark(orders.Captain.current_position, CaptainBlue);
					}
				}
				catch
				{
				}
				if (orders.YouLead && Service.ArmyMiddle(_host, out var at, out var _) && (at - p).sqrMagnitude > 100f)
				{
					Mark(at, CaptainBlue);
				}
			}
			if (Charge != null)
			{
				Mark(Charge.Site, new Color(0.45f, 1f, 0.4f));
			}
			void Mark(Vector2 vector2, Color c, float near = 3f)
			{
				Vector2 vector = vector2 - p;
				if (vector.sqrMagnitude > near * near)
				{
					marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector.y, vector.x), c));
				}
			}
		}

		private void DrawService()
		{
			if (!(DeathTimer >= 0f) && SoldierMap.Open)
			{
				_hud.DrawSoldierMap();
			}
		}

		private bool ServiceEscape()
		{
			if (!SoldierMap.Open)
			{
				return false;
			}
			SoldierMap.Close();
			return true;
		}

		private bool CaptainPing(PingKind kind, Vector3 at)
		{
			if ((kind != PingKind.Go && kind != PingKind.Defend) || !Service.IsCaptain(_host))
			{
				return false;
			}
			WorldTile worldTile = null;
			try
			{
				worldTile = World.world.GetTile((int)at.x, (int)at.y);
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return false;
			}
			bool flag = kind == PingKind.Defend;
			int num = Service.CommandArmy(_host, Service.CommandKind.March, worldTile, flag ? "Defending your ping" : "Holding your ping");
			if (num <= 0)
			{
				return false;
			}
			Toast(flag ? $"Your soldiers ({num}) march to defend it." : $"Your soldiers ({num}) march there and hold it.");
			PlayHorn();
			return true;
		}

		internal bool CaptainPingForTest(PingKind kind, Vector3 at)
		{
			return CaptainPing(kind, at);
		}

		private void SettingsMenuKeys()
		{
			WrapEscape();
			WrapLetGo();
			bool num = _active && !_settingsMenu.Open && !Run.WindowOpen && (VrControls.Down(KeyCode.Escape) || PadControls.Down(KeyCode.Escape));
			if (PlayInput.GetKeyDown(KeyCode.F2) && !IsTyping())
			{
				SetHelp(open: false);
				SetSettingsMenu(!_settingsMenu.Open);
			}
			if (PlayInput.GetKeyDown(KeyCode.F1) && !IsTyping() && Config.game_loaded)
			{
				SetSettingsMenu(open: false);
				SetHelp(!Help.Open);
			}
			if (_settingsMenu.Open && _active && !IsTyping() && !SettingsTakeTab && (InventoryPressed() || PlayInput.GetKeyDown(KeyCode.M)))
			{
				SetSettingsMenu(open: false);
			}
			if (Help.Open && _active && !IsTyping() && (InventoryPressed() || PlayInput.GetKeyDown(KeyCode.M)))
			{
				SetHelp(open: false);
			}
			if (_settingsMenu.Open && (!EscapeTaken || VrControls.Down(KeyCode.Escape) || PadControls.Down(KeyCode.Escape)) && PlayInput.GetKeyDown(KeyCode.Escape) && !_settingsMenu.EscapeDrop())
			{
				SetSettingsMenu(open: false);
			}
			if (num)
			{
				try
				{
					EscapePressed(HotkeyLibrary.cancel);
				}
				catch (Exception ex)
				{
					Log.Error("vr: B as Esc: " + ex.Message);
				}
			}
			if (_runAsked)
			{
				SetSettingsMenu(open: false);
			}
			if (_helpAsked)
			{
				_helpAsked = false;
				SetSettingsMenu(open: false);
				SetHelp(open: true);
			}
			if (_leaveBodyAsked)
			{
				_leaveBodyAsked = false;
				SetSettingsMenu(open: false);
				if (CanLeaveBody)
				{
					TouchLetGo();
				}
			}
			BlockMap(_settingsMenu.Open || Help.Open || (!_active && Run.WindowOpen));
			RemindOfMenu();
		}

		internal void AskHelp()
		{
			_helpAsked = true;
		}

		internal void AskLeaveBody()
		{
			_leaveBodyAsked = true;
		}

		internal void SetSettingsMenu(bool open)
		{
			if (open == _settingsMenu.Open)
			{
				return;
			}
			_settingsMenu.Drop = null;
			if (open)
			{
				_settingsMenu.Build(this);
				Craft.Open = false;
				Royal.Open = false;
				Royal.ObeliskOpen = false;
				WarMap.Close();
				_settingsMenu.Open = true;
				if (!Settings.MenuSeen)
				{
					Settings.MenuSeen = true;
					_settingsMenu.Changed = true;
				}
				if (_godToast != null && _godToast.StartsWith("First Person mod: F2 opens its settings"))
				{
					_godToastUntil = 0f;
				}
			}
			else
			{
				_settingsMenu.Open = false;
				if (_settingsMenu.Changed)
				{
					Settings.Save();
					_settingsMenu.Changed = false;
					_settingsDirty = false;
				}
			}
			if (!_active)
			{
				Sounds.PlayUI(open ? "event:/SFX/UI/WindowWhoosh" : "event:/SFX/UI/WindowClose");
			}
			BlockMap(open);
		}

		internal void SetHelp(bool open)
		{
			if (open == Help.Open)
			{
				return;
			}
			Help.Open = open;
			if (open)
			{
				int num = -1;
				if (PadControls.Active && PadControls.Shown)
				{
					num = Array.FindIndex(Help.Keys, (Help.Part p) => p.Name == "Controller");
				}
				else if (VrControls.Active)
				{
					num = Array.FindIndex(Help.Keys, (Help.Part p) => p.Name == "VR");
				}
				if (num >= 0)
				{
					Help.Page = 0;
					Help.Section[0] = num;
				}
				Help.Scroll = Vector2.zero;
			}
			if (!_active)
			{
				Sounds.PlayUI(open ? "event:/SFX/UI/WindowWhoosh" : "event:/SFX/UI/WindowClose");
			}
		}

		private void DrawHelpWindow()
		{
			if (Help.Open && Config.game_loaded && !(World.world == null) && !_settingsMenu.Open)
			{
				GUI.depth = -1000;
				if (_hud.DrawHelpWindow())
				{
					SetHelp(open: false);
				}
			}
		}

		private void DrawSettingsMenu()
		{
			if (Config.game_loaded)
			{
				_hud.DrawNet(_active && ShownFrame != null);
			}
			if ((!_settingsMenu.Open && _settingsMenu.Unroll <= 0f) || !Config.game_loaded || World.world == null)
			{
				_hud.DrawNetOver();
				return;
			}
			_settingsMenu.Build(this);
			GUI.depth = -1000;
			if (!_settingsMenu.Open)
			{
				PacksUi.Open = false;
			}
			bool num = (PacksUi.Open ? _hud.DrawPacksWindow() : _hud.DrawSettings(_settingsMenu, Settings));
			if (_settingsMenu.Changed)
			{
				_settingsDirty = true;
			}
			if (num)
			{
				SetSettingsMenu(open: false);
			}
			_hud.DrawNetOver();
		}

		private void WrapEscape()
		{
			if (_escapeFailed)
			{
				return;
			}
			try
			{
				HotkeyAsset cancel = HotkeyLibrary.cancel;
				HotkeyAction hotkeyAction = cancel?.just_pressed_action;
				if (hotkeyAction != null && (_menuEscape == null || hotkeyAction != _menuEscape))
				{
					if (_menuEscape == null)
					{
						_menuEscape = EscapePressed;
					}
					_gameEscape = hotkeyAction;
					cancel.just_pressed_action = _menuEscape;
				}
			}
			catch (Exception ex)
			{
				Log.Error("settings menu: could not take Esc (it closes the menu and lets go of the creature): " + ex.Message);
				_escapeFailed = true;
			}
		}

		private void WrapLetGo()
		{
			if (_letGoFailed)
			{
				return;
			}
			try
			{
				HotkeyAsset control_unit = HotkeyLibrary.control_unit;
				HotkeyAction hotkeyAction = control_unit?.just_pressed_action;
				if (hotkeyAction != null && (_letGo == null || hotkeyAction != _letGo))
				{
					if (_letGo == null)
					{
						_letGo = LetGoPressed;
					}
					_gameLetGo = hotkeyAction;
					control_unit.just_pressed_action = _letGo;
				}
			}
			catch (Exception ex)
			{
				Log.Error("let go: could not take G: " + ex.Message);
				_letGoFailed = true;
			}
		}

		private void LetGoPressed(HotkeyAsset asset)
		{
			if (GameKeysMuted)
			{
				return;
			}
			if (_active && ControllableUnit.isControllingUnit())
			{
				try
				{
					ControllableUnit.clear();
					return;
				}
				catch (Exception ex)
				{
					Log.Error("let go: " + ex.Message);
					return;
				}
			}
			_gameLetGo?.Invoke(asset);
		}

		private void EscapePressed(HotkeyAsset asset)
		{
			if (MpUi.ChatOpen)
			{
				return;
			}
			if (_settingsMenu.Open)
			{
				if (!_settingsMenu.EscapeDrop())
				{
					SetSettingsMenu(open: false);
				}
			}
			else if (Help.Open)
			{
				SetHelp(open: false);
			}
			else if ((!_active || !SceneEscape()) && (_active || !FlyEscape()) && (!_active || !CivilEscape()) && (!_active || !SettingsEscape()))
			{
				_gameEscape?.Invoke(asset);
			}
		}

		private bool SettingsEscape()
		{
			if (Craft.Open)
			{
				Craft.Open = false;
				return true;
			}
			if (HomeCard.Open)
			{
				HomeCard.Open = false;
				return true;
			}
			if (Royal.Open)
			{
				Royal.Open = false;
				return true;
			}
			if (Royal.ObeliskOpen)
			{
				Royal.ObeliskOpen = false;
				return true;
			}
			if (WarMap.Open)
			{
				WarMap.Close();
				return true;
			}
			if (DeathTimer >= 0f)
			{
				return false;
			}
			bool flag = false;
			try
			{
				flag = ScrollWindow.isWindowActive();
			}
			catch
			{
			}
			if (flag)
			{
				return false;
			}
			SetSettingsMenu(open: true);
			return true;
		}

		private void BlockMap(bool block)
		{
			if (_menuBlocker == null)
			{
				if (!block)
				{
					return;
				}
				try
				{
					_menuBlocker = new GameObject("FirstPersonSettingsBlocker");
					_menuBlocker.transform.SetParent(base.transform, worldPositionStays: false);
					Canvas canvas = _menuBlocker.AddComponent<Canvas>();
					canvas.renderMode = RenderMode.ScreenSpaceOverlay;
					canvas.sortingOrder = 32000;
					_menuBlocker.AddComponent<GraphicRaycaster>();
					GameObject obj = new GameObject("Sheet");
					obj.transform.SetParent(_menuBlocker.transform, worldPositionStays: false);
					Image image = obj.AddComponent<Image>();
					image.color = new Color(0f, 0f, 0f, 0f);
					image.raycastTarget = true;
					RectTransform rectTransform = image.rectTransform;
					rectTransform.anchorMin = Vector2.zero;
					rectTransform.anchorMax = Vector2.one;
					Vector2 offsetMin = (rectTransform.offsetMax = Vector2.zero);
					rectTransform.offsetMin = offsetMin;
				}
				catch (Exception ex)
				{
					Log.Error("settings menu: could not cover the map: " + ex.Message);
					if (_menuBlocker != null)
					{
						UnityEngine.Object.Destroy(_menuBlocker);
					}
					_menuBlocker = null;
					return;
				}
			}
			if (_menuBlocker.activeSelf != block)
			{
				_menuBlocker.SetActive(block);
			}
		}

		private float ViewReach()
		{
			TerrainCache cache = _sampler.Cache;
			float num = ((cache != null && cache.Width > 0) ? Mathf.Sqrt((float)cache.Width * (float)cache.Width + (float)cache.Height * (float)cache.Height) : 400f) + 40f;
			if (!(Settings.DrawDistance >= 1200f))
			{
				return Mathf.Min(Settings.DrawDistance, num);
			}
			return num;
		}

		private void ApplyViewDistance()
		{
			Renderer.MaxDistance = ViewMaxDistance;
			TerrainCache cache = _sampler.Cache;
			Renderer.FadeAt = ((Settings.DrawDistance >= 1200f && cache != null && cache.Width > 0) ? (FarthestCorner(cache, _view.X, _view.Y) + 4f) : 0f);
			Renderer.HazeScale = Mathf.Min(1f, 194.40001f / Mathf.Max(1f, Renderer.HazeFadeStart));
			float magnification = Binoculars.Magnification;
			Renderer.BillboardDistance = Mathf.Min(Settings.DetailDistance * magnification, Renderer.MaxDistance);
			Renderer.SeeIntoRange = Seabed.RangeFor(Settings.DetailDistance, LightMode.On);
			_creatureReach = Mathf.Min(Renderer.MaxDistance, Mathf.Max(Renderer.BillboardDistance, Settings.CreatureDistance * _creatureDetail * magnification));
			SceneCollector.OffscreenCheap = Settings.OffscreenMotion == 1;
			People.FarPoses = Settings.FarPoses == 1;
			SimLod.Budgeted = Settings.FarCreatures == 1;
			SceneCollector.ChunkWalkOn = Settings.ChunkWalk == 1 || SceneCollector.ChunkWalkForTest;
		}

		private static float FarthestCorner(TerrainCache land, float x, float y)
		{
			float num = Mathf.Max(x, (float)land.Width - x);
			float num2 = Mathf.Max(y, (float)land.Height - y);
			return Mathf.Sqrt(num * num + num2 * num2);
		}

		internal static float NextDrawPreset(float now)
		{
			float[] drawPresets = DrawPresets;
			foreach (float num in drawPresets)
			{
				if (num > now + 1f)
				{
					return num;
				}
			}
			return DrawPresets[0];
		}

		internal static string DrawDistanceName(float distance)
		{
			if (!(distance >= 1200f))
			{
				return distance.ToString("0") + " tiles";
			}
			return "the whole map";
		}

		private void RemindOfMenu()
		{
			if (_menuHinted || Settings.MenuSeen || _active || _dive != Dive.None)
			{
				return;
			}
			if (!_selfTesting.HasValue)
			{
				_selfTesting = File.Exists(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			}
			if (_selfTesting == true)
			{
				_menuHinted = true;
				return;
			}
			bool flag = false;
			try
			{
				flag = SmoothLoader.isLoading();
			}
			catch
			{
			}
			if (flag)
			{
				_menuHintAt = -1f;
				return;
			}
			if (_menuHintAt < 0f)
			{
				_menuHintAt = Time.unscaledTime + 4f;
			}
			if (!(Time.unscaledTime < _menuHintAt))
			{
				_menuHinted = true;
				_godToast = "First Person mod: F2 opens its settings";
				_godToastUntil = Time.unscaledTime + 8f;
			}
		}

		private void ApplyShadows()
		{
			Settings settings = Settings;
			if (settings == null)
			{
				return;
			}
			int num = Mathf.Clamp(settings.Shadows, 0, 4);
			float num2 = Mathf.Clamp(settings.ShadowDistance, 24f, 192f);
			FrameRenderer renderer = Renderer;
			int shadowMapSize;
			if (num > 1)
			{
				switch (num)
				{
				default:
					shadowMapSize = 1024;
					break;
				case 3:
					shadowMapSize = 768;
					break;
				case 2:
					shadowMapSize = 512;
					break;
				}
			}
			else
			{
				shadowMapSize = 384;
			}
			renderer.ShadowMapSize = shadowMapSize;
			Renderer.ShadowReach = 30f * num2 / 64f;
			Renderer.ShadowStrength = ((num <= 0) ? 0f : Mathf.Clamp01(settings.ShadowDarkness));
			Renderer.ShadowDarkness = Mathf.Clamp01(settings.ShadowDarkness);
			GpuRenderer.EyeShadowReach = num2;
			bool naturalSun = NaturalSun;
			if (_sunWas >= 0 && _sunWas != (naturalSun ? 1 : 0))
			{
				ForceRelight();
			}
			_sunWas = (naturalSun ? 1 : 0);
			bool lensFlare = naturalSun && settings.LensFlare && !VrRunning;
			Renderer.NaturalSun = naturalSun;
			Renderer.LensFlare = lensFlare;
			_sampler.Cache.SoftLandShadows = naturalSun && !NoLandFieldForTest;
			if (_gpu != null)
			{
				_gpu.NaturalSun = naturalSun;
				_gpu.LensFlare = lensFlare;
				_gpu.ShadowQuality = num;
				_gpu.ShadowSoftness = Mathf.Clamp(settings.ShadowSoftness, 0, 2);
				_gpu.ShadowDarkness = Mathf.Clamp01(settings.ShadowDarkness);
				_gpu.ShadowReach = num2;
			}
		}

		internal static bool StrongCard(string name, int memoryMb, bool compute)
		{
			if (compute && memoryMb >= 3800)
			{
				return !SharedGpu.IsMatch(name ?? "");
			}
			return false;
		}

		internal static bool AppleSilicon(string name)
		{
			if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
			{
				return (name ?? "").StartsWith("Apple M", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		private void ChooseFirstResolution()
		{
			string graphicsDeviceName = SystemInfo.graphicsDeviceName;
			int graphicsMemorySize = SystemInfo.graphicsMemorySize;
			bool flag = !Device.OnPhone && (StrongCard(graphicsDeviceName, graphicsMemorySize, SystemInfo.supportsComputeShaders) || AppleSilicon(graphicsDeviceName));
			Settings.Resolution = (flag ? 2 : 0);
			Settings.ResolutionTrial = flag;
			_settingsDirty = true;
			Log.Info($"resolution for a new player: {ResolutionName(ResolutionWidths[Settings.Resolution])} ('{graphicsDeviceName}', {graphicsMemorySize} MB" + (flag ? "; checked over the first minute in first person)" : ")"));
		}

		internal void ChooseFirstResolutionForTest()
		{
			ChooseFirstResolution();
		}

		private void ResolutionTrialFrame()
		{
			if (!Settings.ResolutionTrial)
			{
				return;
			}
			if (Settings.Resolution != 2)
			{
				EndResolutionTrial("the resolution was changed");
				return;
			}
			float unscaledDeltaTime = Time.unscaledDeltaTime;
			if (unscaledDeltaTime > 0.5f)
			{
				return;
			}
			float num = (_trialProbing ? TrialProbeMsForTest : TrialFrameMsForTest);
			_trialMs.Add((num >= 0f) ? num : (unscaledDeltaTime * 1000f));
			_trialTime += unscaledDeltaTime;
			float num2 = ((TrialSecondsForTest > 0f) ? TrialSecondsForTest : (_trialProbing ? 3f : 60f));
			if (_trialTime < num2)
			{
				return;
			}
			_trialMs.Sort();
			float num3 = _trialMs[_trialMs.Count / 2];
			if (!_trialProbing)
			{
				if (num3 <= 25f)
				{
					Log.Info($"resolution trial: the middle frame of {_trialMs.Count} took {num3:F1} ms ({1000f / Mathf.Max(0.1f, num3):F0} fps): kept");
					EndResolutionTrial(null);
					return;
				}
				Log.Info($"resolution trial: the middle frame of {_trialMs.Count} took {num3:F1} ms ({1000f / Mathf.Max(0.1f, num3):F0} fps): trying 480x270 a moment");
				_trialSharpMs = num3;
				_trialProbing = true;
				_trialMs.Clear();
				_trialTime = 0f;
			}
			else
			{
				bool flag = num3 <= _trialSharpMs * 0.75f;
				Log.Info($"resolution trial: at 480x270 the middle frame took {num3:F1} ms, against {_trialSharpMs:F1}: " + (flag ? "the card is what is slow, 480x270 kept" : "the game itself is slow, the 2x picture kept"));
				if (flag)
				{
					Settings.Resolution = 0;
					Toast("Resolution lowered to " + ResolutionName(ResolutionWidths[0]) + " for a steadier frame rate (F2 > Graphics)");
				}
				EndResolutionTrial(null);
			}
		}

		private void EndResolutionTrial(string why)
		{
			if (why != null)
			{
				Log.Info("resolution trial ended: " + why);
			}
			Settings.ResolutionTrial = false;
			_settingsDirty = true;
			_trialMs.Clear();
			_trialTime = 0f;
			_trialProbing = false;
		}

		private void UpdateShotgun(Actor host, string weapon, bool canShoot, float dt)
		{
			bool keysFree = canShoot && !IsTyping() && !Hub.AnyModeActive;
			_shotgun.Update(host, weapon, Feedback.Attacks, Feedback.LastAttackMelee, canShoot, keysFree, dt, _audio, _hands, _melee, Feedback, new Vector3(_view.X, _view.Y, _view.Z));
		}

		internal Vector2 ShownToneForTest(float y0, float y1)
		{
			Texture2D texture2D = ((_gpuShown && _gpu != null && _gpu.Output != null) ? _gpu.ReadOutput() : Frame);
			if (texture2D == null)
			{
				return Vector2.zero;
			}
			Color32[] pixels = texture2D.GetPixels32();
			int width = texture2D.width;
			int height = texture2D.height;
			int num = Mathf.Clamp((int)(y0 * (float)height), 0, height - 1);
			int num2 = Mathf.Clamp((int)(y1 * (float)height), num + 1, height);
			double num3 = 0.0;
			double num4 = 0.0;
			for (int i = num; i < num2; i++)
			{
				for (int j = 0; j < width; j++)
				{
					Color32 color = pixels[i * width + j];
					num3 += 0.299 * (double)(int)color.r + 0.587 * (double)(int)color.g + 0.114 * (double)(int)color.b;
					num4 += (double)(Mathf.Max(color.r, Mathf.Max(color.g, color.b)) - Mathf.Min(color.r, Mathf.Min(color.g, color.b)));
				}
			}
			if (texture2D != Frame)
			{
				UnityEngine.Object.Destroy(texture2D);
			}
			float num5 = Mathf.Max(1, (num2 - num) * width);
			return new Vector2((float)(num3 / (double)num5), (float)(num4 / (double)num5));
		}

		private void ApplySnowfall(Weather w, Precip drops, float cover, float dt)
		{
			float num = ((drops == Precip.Snow) ? Mathf.Clamp01(cover * 1.6f) : 0f);
			if (w.Global == Precip.Snow)
			{
				num = Mathf.Max(num, w.GlobalStrength * 0.6f);
			}
			_snowfall = Mathf.Lerp(_snowfall, num, 1f - Mathf.Exp(-1.2f * dt));
			if (_snowfall > 0.01f && Renderer.Air != null && !Renderer.Air.Indoors)
			{
				Renderer.Air = Renderer.Air.Snowfall(_snowfall);
			}
		}

		private void UpdateTalkVoice()
		{
			Conversation converse = Converse;
			FirstPersonAudio.TalkLine line = new FirstPersonAudio.TalkLine
			{
				Open = converse.Open
			};
			if (converse.Open && converse.With != null && converse.Line != null)
			{
				try
				{
					Actor with = converse.With;
					long num = (line.With = with.data.id);
					line.Voiced = !converse.LineIsYours && !converse.AsIs && !converse.Voice.Mute;
					int num2 = ((int)(num ^ (num >> 32)) * 397 + converse.Said) * 31 + converse.Page;
					line.Key = ((num2 == 0) ? 1 : num2);
					string text = VoiceSets.KindOf((with.asset != null) ? with.asset.sound_idle : null);
					line.Kind = ((text != null && VoiceSets.Of(text) != null) ? text : null);
					if (line.Key != _voiceKey)
					{
						_voiceKey = line.Key;
						_voiceWho = Speakers.Of(converse);
					}
					line.Who = _voiceWho;
					line.Text = converse.Line;
					line.Seconds = (float)converse.Line.Length / 55f;
					line.Age = converse.Age;
					line.Typed = converse.Typed;
					line.Mouth = ((Interior.Inside || Cabin.Inside || Mine.Inside) ? Vector3.zero : FirstPersonAudio.MouthOf(with));
				}
				catch
				{
					line.Voiced = false;
				}
			}
			_audio.Talk(line);
		}

		private bool StandAsideForNml()
		{
			if (Detour.Hooker != null || Detour.Platform != Detour.Kind.MacArm64)
			{
				return false;
			}
			string text = NmlWorldfallPackage();
			if (text == null)
			{
				return false;
			}
			UnityEngine.Debug.LogWarning("[Worldfall] Worldfall.dll in the game's mods folder can't patch the game on Apple Silicon; the NeoModLoader copy in " + text + " runs instead. Remove Worldfall.dll from StreamingAssets/mods to skip this step.");
			_steppedAside = true;
			base.enabled = false;
			UnityEngine.Object.Destroy(this);
			return true;
		}

		private static string NmlWorldfallPackage()
		{
			try
			{
				string path = Path.Combine(Application.streamingAssetsPath, "mods");
				if (!File.Exists(Path.Combine(path, "NeoModLoader.dll")) && !File.Exists(Path.Combine(path, "NeoModLoader_memload.dll")))
				{
					return null;
				}
				string path2 = ((Application.platform == RuntimePlatform.OSXPlayer) ? Path.Combine(Application.streamingAssetsPath, "../../../../..") : Path.Combine(Application.streamingAssetsPath, "../.."));
				string[] array = new string[2]
				{
					Path.Combine(path2, "Mods"),
					Path.Combine(path2, "../../workshop/content/1206560")
				};
				foreach (string path3 in array)
				{
					if (!Directory.Exists(path3))
					{
						continue;
					}
					string[] directories = Directory.GetDirectories(path3);
					foreach (string text in directories)
					{
						string path4 = Path.Combine(text, "mod.json");
						if (File.Exists(path4) && File.ReadAllText(path4).IndexOf("s3cond2.worldfall", StringComparison.OrdinalIgnoreCase) >= 0)
						{
							return Path.GetFullPath(text);
						}
					}
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogWarning("[Worldfall] could not look for a NeoModLoader install: " + ex.Message);
			}
			return null;
		}

		private void FaceTalker(Actor npc, float dt)
		{
			if (!TalkHead(npc, out var head))
			{
				return;
			}
			float num = head.x - _view.X;
			float num2 = head.y - _view.Y;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 < 0.05f)
			{
				return;
			}
			float num4 = Mathf.DeltaAngle(_view.Yaw * 57.29578f, Mathf.Atan2(num2, num) * 57.29578f) * (MathF.PI / 180f);
			if (VrRunning)
			{
				if (Converse.Age < 1.2f || Mathf.Abs(num4) > 0.6f)
				{
					_view.Yaw += num4 * (1f - Mathf.Exp(-6f * dt));
				}
			}
			else
			{
				_view.Yaw += num4 * (1f - Mathf.Exp((0f - ((Converse.Age < 1.2f) ? 6f : 3f)) * dt));
				float num5 = Mathf.Atan2(head.z - _view.Z, num3) * 57.29578f;
				float num6 = Mathf.Min(LookLimit, 60f);
				_pitchDegrees = Mathf.Lerp(_pitchDegrees, Mathf.Clamp(num5 - TalkHeadAbove(), 0f - num6, num6), 1f - Mathf.Exp(-4f * dt));
			}
		}

		private float TalkHeadAbove()
		{
			float talkCardTop = Hud.TalkCardTop;
			float num = ((talkCardTop > 0f) ? Mathf.Clamp(talkCardTop - 0.12f, 0.3f, 0.42f) : 0.4f);
			return Mathf.Atan((0.5f - num) * 2f * TanHalfHeight()) * 57.29578f;
		}

		private float TanHalfHeight()
		{
			return Mathf.Tan(_view.Fov * 0.5f) * (float)Screen.height / Mathf.Max(1f, Screen.width);
		}

		private bool TalkHead(Actor npc, out Vector3 head)
		{
			head = default(Vector3);
			try
			{
				if (npc == null || !npc.isAlive())
				{
					return false;
				}
				Vector2 vector = (Interior.Inside ? Interior.PositionOf(npc) : npc.current_position);
				if (Hitboxes.Seen(npc) && Hitboxes.GetHead(npc, out var head2) && (new Vector2(head2.Centre.X, head2.Centre.Y) - vector).sqrMagnitude < 1f)
				{
					head = new Vector3(head2.Centre.X, head2.Centre.Y, head2.Centre.Z);
					return true;
				}
				if (Interior.Inside)
				{
					Vector3 vector2 = Interior.HeadOf(npc, _sprites);
					head = new Vector3(vector2.x, vector2.y, (vector2.z - 0.15f) * 0.88f);
					return true;
				}
				if (Hitboxes.Get(npc, out var box))
				{
					head = new Vector3(box.Centre.X, box.Centre.Y, box.Top - 0.12f * (box.Top - box.Bottom));
					return true;
				}
				head = HeadOfWorld(npc) - new Vector3(0f, 0f, 0.27f);
				return true;
			}
			catch
			{
				return false;
			}
		}

		internal bool TalkHeadOnScreenForTest(Actor npc, out float x, out float y, out float card)
		{
			x = (y = 0f);
			card = Hud.TalkCardTop;
			if (!TalkHead(npc, out var head))
			{
				return false;
			}
			float num = head.x - _view.X;
			float num2 = head.y - _view.Y;
			float x2 = Mathf.Sqrt(num * num + num2 * num2);
			float f = Mathf.DeltaAngle(_view.Yaw * 57.29578f, Mathf.Atan2(num2, num) * 57.29578f) * (MathF.PI / 180f);
			float f2 = Mathf.Atan2(head.z - _view.Z, x2) - Mathf.Atan(_view.Pitch);
			x = 0.5f - Mathf.Tan(f) / Mathf.Tan(_view.Fov * 0.5f) * 0.5f;
			y = 0.5f - Mathf.Tan(f2) / TanHalfHeight() * 0.5f;
			return true;
		}

		private BaseSimObject TalkPick(BaseSimObject picked, Func<Actor, Vector2> posOf, Vector2 eye)
		{
			float unscaledTime = Time.unscaledTime;
			if (picked is Actor actor && actor != _host)
			{
				_talkHeld = actor;
				_talkHeldAt = unscaledTime;
				return picked;
			}
			Actor talkHeld = _talkHeld;
			if (talkHeld == null || unscaledTime - _talkHeldAt > 0.35f)
			{
				_talkHeld = null;
				return picked;
			}
			try
			{
				if (!talkHeld.isAlive())
				{
					_talkHeld = null;
					return picked;
				}
				Vector2 vector = posOf(talkHeld) - eye;
				float magnitude = vector.magnitude;
				if (magnitude > 3f || magnitude < 0.05f)
				{
					return picked;
				}
				if (Mathf.Abs(Mathf.DeltaAngle(_view.Yaw * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f)) * (MathF.PI / 180f) > Mathf.Atan2(0.45f, magnitude))
				{
					return picked;
				}
				return talkHeld;
			}
			catch
			{
				_talkHeld = null;
				return picked;
			}
		}

		private void TerrainTick()
		{
			ReliefWorld.Tick(_active, _day);
			Lakes.Tick(_day, SkyWeather);
			if (!WorldSync.Mirroring)
			{
				try
				{
					SiteLevel.Tick(Time.unscaledDeltaTime);
				}
				catch (Exception e)
				{
					Hub.Fail("site level", e);
				}
			}
			if (ReliefWorld.CardOpen && !IsTyping() && PlayInput.GetKeyDown(KeyCode.Escape))
			{
				ReliefWorld.Postpone();
			}
		}

		private void DrawTerrainCard()
		{
			if (_active && ReliefWorld.CardOpen)
			{
				_hud.DrawWorldLand(this);
			}
		}

		private void DigHands(Actor host, ref HandsInput input)
		{
			if (Holstered || _bow.InHand || _blaster.InHand || _shotgun.InHand || input.HeldModel != null)
			{
				return;
			}
			string text = Earthworks.ToolOf(host);
			if (text == null)
			{
				return;
			}
			string tool = ((text == "tw_item_shovel") ? "shovel" : "pickaxe");
			MeshModel meshModel = CarryLook.Tool(tool)?.Model(0) ?? ToolMeshes.Tool(tool);
			if (meshModel != null)
			{
				input.HeldModel = meshModel;
				input.HeldKind = WeaponKind.Axe;
				if (!(text != "tw_item_shovel"))
				{
					input.HeldKind = WeaponKind.Spear;
					input.Dig = true;
					input.DigRate = Earthworks.SwingTime(1) / Earthworks.SwingTime(Earthworks.LevelOf(host));
					input.DigFill = Earthworks.LastFilled;
				}
			}
		}

		private void DigKeys(Actor host)
		{
			if (host != null && !Holstered && !IsTyping() && !Hub.AnyModeActive && Earthworks.ToolOf(host) != null)
			{
				if (!Settings.DigSeen)
				{
					Settings.DigSeen = true;
					Settings.Save();
					Toast("Hold LMB to dig. R switches to fill, water too. Hold RMB to level to your feet. The wheel widens the patch as you learn.");
				}
				if (PlayInput.GetKeyDown(KeyCode.R))
				{
					Earthworks.SwitchMove(Toast);
				}
			}
		}

		private bool DigWheel(Actor host, float scroll)
		{
			if (host == null || Holstered || Earthworks.ToolOf(host) == null)
			{
				return false;
			}
			if (!Earthworks.Widen(host, (scroll > 0f) ? 1 : (-1)) && Time.unscaledTime - _widenToastAt > 3f)
			{
				_widenToastAt = Time.unscaledTime;
				int num = Earthworks.MaxSize(host);
				Toast((num >= 4) ? ("The widest patch: " + num + " tiles") : ("Dig more to work a wider patch (" + num + " now)"));
			}
			return true;
		}

		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr window, int command);

		[DllImport("user32.dll")]
		private static extern bool IsIconic(IntPtr window);

		private void StartQuietTestCopy()
		{
			QuietTestCopy = Environment.GetEnvironmentVariable("FIRSTPERSON_QUIET") == "1";
			if (QuietTestCopy)
			{
				string[] array = (Environment.GetEnvironmentVariable("FIRSTPERSON_QUIET_SIZE") ?? "").Split('x');
				if (array.Length == 2 && int.TryParse(array[0], out var result) && int.TryParse(array[1], out var result2) && result >= 320 && result2 >= 240)
				{
					_quietWidth = result;
					_quietHeight = result2;
				}
				Application.runInBackground = true;
				Log.Info("quiet test copy: runs in the background, muted, files in " + Log.Folder);
			}
		}

		private void KeepQuiet()
		{
			if (!QuietTestCopy || Time.unscaledTime < _quietCheck)
			{
				return;
			}
			_quietCheck = Time.unscaledTime + 0.5f;
			Application.runInBackground = true;
			try
			{
				if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width > _quietWidth)
				{
					Screen.SetResolution(_quietWidth, _quietHeight, FullScreenMode.Windowed);
				}
				IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
				if (mainWindowHandle != IntPtr.Zero && !IsIconic(mainWindowHandle))
				{
					ShowWindow(mainWindowHandle, 7);
				}
			}
			catch
			{
			}
			try
			{
				if (RuntimeManager.CoreSystem.getMasterChannelGroup(out var channelgroup) == RESULT.OK)
				{
					channelgroup.setMute(QuietLevelForTest < 0f);
					if (QuietLevelForTest >= 0f)
					{
						channelgroup.setVolume(QuietLevelForTest);
					}
				}
			}
			catch
			{
			}
		}

		private void NoteCrossing(float worldEyeZ, bool stepIn)
		{
			_crossEyeZ = worldEyeZ;
			_crossIn = stepIn;
		}

		private float RoomEyeEase(float roomEyeZ, float floorZ, float dt)
		{
			if (_crossIn && !float.IsNaN(_crossEyeZ))
			{
				_roomEyeEase = (Interior.Fitted ? Mathf.Clamp(_crossEyeZ - Interior.WorldZ(roomEyeZ), -1.5f, 0.5f) : 0f);
				_crossEyeZ = float.NaN;
			}
			if (Mathf.Abs(_roomEyeEase) < 0.002f)
			{
				_roomEyeEase = 0f;
				return roomEyeZ;
			}
			float result = Mathf.Min(Mathf.Max(roomEyeZ + _roomEyeEase, floorZ + 0.3f), RoomFloors.EyeLimit(Interior.Eye));
			_roomEyeEase *= Mathf.Exp(-9f * Mathf.Min(dt, 0.1f));
			return result;
		}

		private void StreetEyeEase(float targetZ, float ground)
		{
			if (!_crossIn && !float.IsNaN(_crossEyeZ))
			{
				float num = Mathf.Clamp(_crossEyeZ - targetZ, -0.5f, 0.5f);
				_crossEyeZ = float.NaN;
				_groundZ = ground + num;
				_groundVel = 0f;
				_eyeZ = targetZ + num;
			}
		}

		internal void ToolForTest(Tool t)
		{
			UseTool(t);
		}

		private void ReadTools()
		{
			RadialWheel toolWheel = ToolWheel;
			toolWheel.Clear();
			toolWheel.Tip = "Tap Q for the last one";
			Actor host = _host;
			Tool[] toolOrder = ToolOrder;
			foreach (Tool tool in toolOrder)
			{
				_tools[toolWheel.Count] = tool;
				switch (tool)
				{
				case Tool.Torch:
				{
					bool flag = host != null && (Torches.Count(host) > 0 || Torches.Out || Torches.InMain(host));
					toolWheel.Add((Torches.Out || (Torches.InMain(host) && !Holstered)) ? "Put away torch" : "Torch", !flag, "No torch in your bag");
					break;
				}
				case Tool.Compass:
					toolWheel.Add(Compass.Out ? "Put away compass" : "Compass", !CanUseCompass, "No compass here");
					break;
				case Tool.Eat:
					toolWheel.Add("Eat");
					break;
				case Tool.Bandage:
					toolWheel.Add("Bandage");
					break;
				case Tool.PutAway:
					toolWheel.Add(Holstered ? "Take up" : "Put away");
					break;
				}
			}
		}

		private void EmoteWheelKeys(bool menu)
		{
			float unscaledTime = Time.unscaledTime;
			bool flag = !menu && DeathTimer < 0f && !IsTyping() && !Hub.AnyModeActive && _dive == Dive.None && !ActionWheel.Open && !ToolWheel.Open && _host != null;
			if (_emoteSwallow && !PlayInput.GetMouseButton(0) && !PlayInput.GetMouseButton(1))
				_emoteSwallow = false;
			if (_emoteSwallow) PossessionHooks.BlockAttack = true;
			if (_emoteDownAt < 0f)
			{
				if (flag && PlayInput.RawKeyDown(KeyCode.T))
				{
					_emoteDownAt = unscaledTime;
					_emoteDownFrame = Time.frameCount;
					PlayInput.HeldBack2 = KeyCode.T;
				}
				return;
			}
			if (!flag)
			{
				if (EmoteWheel.Open) EmoteWheel.Close();
				EndEmoteHold();
				return;
			}
			if (!PlayInput.RawKey(KeyCode.T))
			{
				if (_emotePicked) { EndEmoteHold(); return; }
				if (EmoteWheel.Open)
				{
					int num = EmoteWheel.Close();
					if (num >= 0) Emotes.Do(num, _host, Toast);
					else Sounds.PlayUI("event:/SFX/UI/WindowClose");
				}
				else
				{
					Emotes.Do(0, _host, Toast);
				}
				EndEmoteHold();
				return;
			}
			if (!EmoteWheel.Open && !_emotePicked && unscaledTime - _emoteDownAt >= 0.25f && Time.frameCount - _emoteDownFrame > 2)
			{
				EmoteWheel.Clear();
				for (int i=0;i<Emotes.Names.Length;i++) EmoteWheel.Add(Emotes.Names[i], false, Emotes.Chat[i]);
				EmoteWheel.Show();
			}
			if (!EmoteWheel.Open) return;
			PossessionHooks.BlockAttack = true;
			if (PlayInput.GetMouseButtonDown(0) || PlayInput.GetMouseButtonDown(1))
			{
				int num2 = EmoteWheel.Close();
				_emoteSwallow = true;
				_emotePicked = true;
				if (num2 >= 0) Emotes.Do(num2, _host, Toast);
				EndEmoteHold();
			}
		}

		private void EndEmoteHold()
		{
			_emoteDownAt = -1f;
			_emoteDownFrame = -1;
			_emotePicked = false;
		}

		private void ToolWheelKeys(bool menu)
		{
			float unscaledTime = Time.unscaledTime;
			bool flag = !menu && DeathTimer < 0f && !IsTyping() && !Hub.AnyModeActive && _dive == Dive.None && !Mounted && !ActionWheel.Open && _host != null;
			if (_toolSwallow && !PlayInput.GetMouseButton(0) && !PlayInput.GetMouseButton(1))
			{
				_toolSwallow = false;
			}
			if (_toolSwallow)
			{
				PossessionHooks.BlockAttack = true;
			}
			if (flag && !ToolWheel.Open)
			{
				if (PadControls.Down(KeyCode.J))
				{
					UseTool(Tool.Eat);
				}
				if (PadControls.Active && Input.GetKeyDown(KeyCode.H))
				{
					UseTool(Tool.PutAway);
				}
				if (VrControls.Down(KeyCode.O))
				{
					UseTool(Tool.Compass);
				}
			}
			if (_toolDownAt < 0f)
			{
				if (flag && PlayInput.RawKeyDown(KeyCode.Q))
				{
					_toolDownAt = unscaledTime;
					_toolDownFrame = Time.frameCount;
					PlayInput.HeldBack2 = KeyCode.Q;
				}
				return;
			}
			if (!flag)
			{
				if (ToolWheel.Open)
				{
					ToolWheel.Close();
				}
				EndToolHold();
				return;
			}
			if (!PlayInput.RawKey(KeyCode.Q))
			{
				if (_toolPicked)
				{
					EndToolHold();
					return;
				}
				if (ToolWheel.Open)
				{
					int num = ToolWheel.Close();
					if (num >= 0)
					{
						UseTool(_tools[num]);
					}
					else
					{
						Sounds.PlayUI("event:/SFX/UI/WindowClose");
					}
				}
				else if (LastTool.HasValue)
				{
					UseTool(LastTool.Value);
				}
				else
				{
					Toast("Hold Q for your tools");
				}
				EndToolHold();
				return;
			}
			if (!ToolWheel.Open && !_toolPicked && unscaledTime - _toolDownAt >= 0.25f && Time.frameCount - _toolDownFrame > 2)
			{
				ReadTools();
				ToolWheel.Show();
			}
			if (!ToolWheel.Open)
			{
				return;
			}
			PossessionHooks.BlockAttack = true;
			if (PlayInput.GetMouseButtonDown(0) || PlayInput.GetMouseButtonDown(1))
			{
				int num2 = ToolWheel.Close();
				_toolSwallow = true;
				_toolPicked = true;
				if (num2 >= 0)
				{
					UseTool(_tools[num2]);
				}
			}
		}

		private void EndToolHold()
		{
			_toolPicked = false;
			_toolDownAt = -1f;
			if (PlayInput.HeldBack2 == KeyCode.Q)
			{
				PlayInput.HeldBack2 = KeyCode.None;
			}
		}

		internal void UseTool(Tool t)
		{
			Actor host = _host;
			if (host == null)
			{
				return;
			}
			if (t != Tool.PutAway)
			{
				LastTool = t;
			}
			switch (t)
			{
			case Tool.Torch:
				if (!Torches.Out && Torches.InMain(host) && !Holstered)
				{
					Holstered = true;
					Toast("You put away your torch");
				}
				else if (!Torches.Out && Torches.InMain(host))
				{
					Holstered = false;
					Toast("Torch taken up");
				}
				else
				{
					Torches.Toggle(host, Toast);
				}
				break;
			case Tool.Compass:
				_compassWanted = true;
				break;
			case Tool.Eat:
				Eating.Eat(Body, Toast);
				break;
			case Tool.Bandage:
				Bandages.Quick(host, Toast);
				break;
			case Tool.PutAway:
				Holstered = !Holstered;
				Toast(Holstered ? "You put away what you were holding" : "You take it up again");
				break;
			}
		}

		private void TorchKeys(bool menu)
		{
			_torchBlocks = false;
			_torchReach = false;
			Actor host = _host;
			bool flag = false;
			try
			{
				flag = host?.isAlive() ?? false;
			}
			catch
			{
			}
			if (!flag || Mounted || DeathTimer >= 0f)
			{
				_torchHold = 0f;
				_torchPress = false;
				return;
			}
			bool torchRight = _torchRight;
			bool flag2 = _hands.TorchLeft.PosedAt >= Time.unscaledTime - 0.2f;
			bool flag3 = Torches.Lit(host);
			float unscaledTime = Time.unscaledTime;
			if (!flag3 && (torchRight || flag2) && unscaledTime - _torchLookedAt > 0.25f)
			{
				_torchLookedAt = unscaledTime;
				_torchFireNear = FireNearYou(host);
				if (_torchFireNear && unscaledTime - _torchHintAt > 30f)
				{
					_torchHintAt = unscaledTime;
					Toast("Hold the left button to hold your torch into the fire");
				}
			}
			else if (flag3 || !(torchRight || flag2))
			{
				_torchFireNear = false;
			}
			bool flag4 = torchRight || (flag2 && !flag3 && _torchFireNear);
			bool flag5 = !menu && !ClickGrace && (PlayInput.GetMouseButton(0) || TorchReachForTest);
			if (torchRight && flag5 && PlayInput.GetMouseButtonDown(0) && TorchClickFree)
			{
				_torchPress = true;
			}
			if (!torchRight || menu || ClickGrace)
			{
				_torchPress = false;
			}
			float torchHold = _torchHold;
			_torchHold = ((flag4 && flag5) ? (_torchHold + Time.unscaledDeltaTime) : 0f);
			if (torchRight)
			{
				_torchReach = _torchHold > 0.25f || TorchReachForTest;
			}
			else if (flag4)
			{
				_torchReach = flag5;
			}
			if (torchRight && _torchPress && !flag5)
			{
				_torchPress = false;
				if (torchHold <= 0.25f)
				{
					_melee.QueueClick();
				}
			}
			_torchBlocks = _torchReach || (torchRight && _torchPress);
		}

		private BaseSimObject TorchWorkPick(Actor host)
		{
			if (!(_picked is Building building) || !_torchRight)
			{
				return _picked;
			}
			try
			{
				return (building.asset != null && (building.asset.flora || building.asset.building_type == BuildingType.Building_Mineral)) ? null : _picked;
			}
			catch
			{
				return _picked;
			}
		}

		private bool FireNearYou(Actor host)
		{
			try
			{
				IList<RoomLight> list = TorchRoomLights();
				Vector2 current_position = host.current_position;
				if (list != null)
				{
					return Torches.FireInRoom(list, new Vec3(_view.X, _view.Y, _view.Z - 0.5f), 3f);
				}
				TerrainCache cache = _sampler.Cache;
				return Torches.FireAt(new Vec3(current_position.x, current_position.y, cache.SampleHeight(current_position.x, current_position.y) + 0.5f), 3f, (float x, float y) => cache.SampleHeight(x, y));
			}
			catch
			{
				return false;
			}
		}

		private IList<RoomLight> TorchRoomLights()
		{
			if (Cabin.Inside)
			{
				return Cabin.Room?.Lights;
			}
			if (Mine.Inside)
			{
				return Mine.Room?.Lights;
			}
			if (Interior.Inside)
			{
				return Interior.Room?.Lights;
			}
			return null;
		}

		private void TorchHands(Actor host, ref HandsInput input)
		{
			_torchRight = false;
			MeshModel meshModel = Models.Get("held_torch");
			if (meshModel != null && host != null && Torches.Count(host) > 0)
			{
				bool flag = Torches.InMain(host) && !Holstered && !_bow.InHand && !_blaster.InHand && !_shotgun.InHand;
				bool flag2 = input.ItemId == null && input.HeldModel == null && input.Bow == null && input.GunModel == null && input.Shotgun == null;
				bool flag3 = !flag && Torches.Out && flag2;
				bool flag4 = flag || flag3;
				if (flag4)
				{
					input.HeldModel = meshModel;
					input.HeldKind = WeaponKind.Dagger;
				}
				_torchRight = flag4;
				input.TorchModel = meshModel;
				input.TorchMain = flag4;
				input.TorchLeft = Torches.Out && !flag3 && !flag;
				input.TorchReach = _torchReach;
				input.TorchLit = Torches.Lit(host);
				input.TorchSputter = _torchSputter;
			}
		}

		private void TorchFx(List<VoxelObject> voxels, Actor host, float dt)
		{
			bool paused = GameClock.EffectsPaused;
			People.TorchNight = Renderer.Air.Night;
			float now = Time.unscaledTime;
			Vec3 eye = new Vec3(_view.X, _view.Y, _view.Z);
			FxParticles fx = SceneCollector.FxPool;
			int count = voxels.Count;
			int count2 = _scene.Billboards.Count;
			bool flag = Cabin.Inside || Mine.Inside;
			List<PointLight> lights = (flag ? _roomLights : Renderer.Lights);
			int count3 = lights.Count;
			if (HelmetLight.Enabled && !flag)
			{
				try { HelmetLight.Emit(eye, _view.Yaw, _pitchDegrees, lights, dt); } catch {}
			}
			IList<RoomLight> list = TorchRoomLights();
			bool flag2 = list != null || Interior.Inside;
			TorchFire torchFire = (_hands.TorchShown ? _hands.Torch : null);
			TorchFire torchFire2 = (_hands.TorchLeftShown ? _hands.TorchLeft : null);
			bool flag3 = host != null && Torches.Lit(host);
			Torches.MainLit = torchFire != null && flag3;
			if (host != null && !paused && dt > 0f)
			{
				if (flag3 && (Swimming || TorchWaterForTest))
				{
					Douse(host, "The water put out your torch");
					flag3 = false;
				}
				float num = ((TorchRainForTest >= 0f) ? TorchRainForTest : _rain);
				if (flag3 && (torchFire != null || torchFire2 != null) && !flag2 && num > 0.1f)
				{
					_torchSputter = Mathf.Min(1f, _torchSputter + dt * num / 4f);
					if (_torchSputter >= 1f)
					{
						Douse(host, "The rain put out your torch");
						flag3 = false;
					}
				}
				else
				{
					_torchSputter = Mathf.Max(0f, _torchSputter - dt * 0.5f);
				}
				TorchFire torchFire3 = torchFire ?? torchFire2;
				if (!flag3 && torchFire3 != null && (_torchFireNear || _torchReach))
				{
					TorchFire torchFire4 = ((torchFire != null && torchFire2 != null) ? torchFire2 : null);
					float reach = 0.1f * torchFire3.Scale + 0.08f;
					bool flag4 = TorchInFire(torchFire3, list, reach) || (torchFire4 != null && TorchInFire(torchFire4, list, reach));
					_torchCatch = (flag4 ? (_torchCatch + dt) : Mathf.Max(0f, _torchCatch - dt));
					if (_torchCatch >= 0.45f)
					{
						_torchCatch = 0f;
						Torches.SetLit(host, lit: true);
						flag3 = true;
						TorchLastForTest = "caught";
						Toast("Your torch catches");
						Log.Info("torch: caught from a fire");
					}
				}
				else
				{
					_torchCatch = 0f;
				}
				TorchFire torchFire5 = (_torchReach ? (torchFire ?? torchFire2) : null);
				if (flag3 && torchFire5 != null && !flag2)
				{
					TerrainCache cache = _sampler.Cache;
					Building building = Torches.Against(torchFire5.Flame, _picked);
					bool flag5 = building == null && torchFire5.Flame.Z - cache.SampleHeight(torchFire5.Flame.X, torchFire5.Flame.Y) < 0.45f;
					_torchAgainst = ((building != null || flag5) ? (_torchAgainst + dt) : 0f);
					if (_torchAgainst >= 0.7f)
					{
						_torchAgainst = 0f;
						if ((building != null) ? Torches.Ignite(host, building, Toast) : Torches.IgniteGround(torchFire5.Flame))
						{
							TorchLastForTest = ((building != null) ? ("ignited " + building.asset?.id) : "ignited the ground");
						}
					}
				}
				else
				{
					_torchAgainst = 0f;
				}
				if (Feedback.Attacks != _torchAttacks)
				{
					if (_torchAttacks >= 0 && Torches.MainLit && Feedback.LastSwing == Swing.Attack)
					{
						_torchBlowAt = now + StrikeTiming.For(WeaponKind.None, melee: true, hands: true).Hit;
					}
					_torchAttacks = Feedback.Attacks;
				}
				if (_torchBlowAt > 0f && now >= _torchBlowAt)
				{
					_torchBlowAt = -1f;
					Building building2 = ((torchFire != null && !flag2) ? Torches.Against(torchFire.Flame, _picked) : null);
					if (building2 != null && Torches.Ignite(host, building2, Toast))
					{
						TorchLastForTest = "struck alight " + building2.asset?.id;
					}
				}
			}
			float glow = (flag2 ? 0.85f : (0.9f * Mathf.Clamp01((Renderer.Air.Night - 0.12f) / 0.4f)));
			Burn(torchFire);
			Burn(torchFire2);
			TorchLightsForTest = lights.Count - count3;
			Torches.Others.Clear();
			int num2 = 0;
			foreach (TorchFire item in People.TorchesDrawn)
			{
				if (!item.Shown || now - item.PosedAt > 0.1f)
				{
					continue;
				}
				Torches.Others.Add(item);
				float num3 = item.Flame.X - _view.X;
				float num4 = item.Flame.Y - _view.Y;
				float num5 = num3 * num3 + num4 * num4;
				if (num5 > 1600f)
				{
					continue;
				}
				item.AddFlames(_scene.Billboards, now, num5 < 196f);
				if (!(num5 > 576f))
				{
					if (!paused)
					{
						item.Emit(fx, dt, now, (num5 < 100f) ? 0.4f : 0.2f);
					}
					if (glow > 0f && num2++ < 5)
					{
						item.Light(lights, now, 0.75f * glow);
					}
				}
			}
			TorchSpritesForTest = voxels.Count - count;
			TorchOthersForTest = _scene.Billboards.Count - count2;
			void Burn(TorchFire f)
			{
				if (f != null)
				{
					f.AddFlames(voxels, eye, now, GpuWanted);
					if (!paused)
					{
						f.Emit(fx, dt, now);
					}
					if (glow > 0f)
					{
						f.Light(lights, now, glow);
					}
				}
			}
		}

		private bool TorchInFire(TorchFire f, IList<RoomLight> room, float reach)
		{
			if (room != null)
			{
				return Torches.FireInRoom(room, f.Flame, reach);
			}
			TerrainCache cache = _sampler.Cache;
			return Torches.FireAt(f.Flame, reach, (float x, float y) => cache.SampleHeight(x, y));
		}

		private void Douse(Actor host, string why)
		{
			Torches.SetLit(host, lit: false);
			_torchSputter = 0f;
			TorchLastForTest = "doused";
			Toast(why);
			Log.Info("torch: " + why);
		}

		private void ResetTown()
		{
			TownShown = null;
			TownUntil = 0f;
			_townHere = null;
			_townLastShown = null;
			_townLeftAt = -1000f;
		}

		private void UpdateTown(Actor host)
		{
			City city = null;
			try
			{
				WorldTile worldTile = host?.current_tile;
				if (worldTile != null && worldTile.zone != null && worldTile.zone.city != null && worldTile.zone.city.isAlive())
				{
					city = worldTile.zone.city;
				}
			}
			catch
			{
			}
			float unscaledTime = Time.unscaledTime;
			if (city != _townHere)
			{
				if (_townHere != null && city == null)
				{
					_townLeftAt = unscaledTime;
				}
				_townHere = city;
				if (city != null && (city != _townLastShown || unscaledTime - _townLeftAt > 20f))
				{
					TownShown = city;
					TownUntil = unscaledTime + 5.5f;
					_townLastShown = city;
				}
			}
		}

		private void Unstick(Actor host, float dt)
		{
			if (host == null || PoseLocks || Swimming || TalkBusy || Interior.Inside || Mine.Inside || Cabin.Inside || PossessionHooks.Frozen)
			{
				_heldFor = 0f;
				return;
			}
			Vector2 vector = ControllableUnit.getMovementVector() + PossessionHooks.ScriptedInput;
			Vector2 current_position;
			try
			{
				if (host.position_height > 0.05f || vector.sqrMagnitude < 0.01f)
				{
					_heldFor = 0f;
					return;
				}
				current_position = host.current_position;
			}
			catch
			{
				_heldFor = 0f;
				return;
			}
			if (_heldFor <= 0f || (current_position - _heldAt).sqrMagnitude > 0.0036f)
			{
				_heldAt = current_position;
				_heldFor = dt * 0.5f;
			}
			else
			{
				if ((_heldFor += dt) < 1.2f)
				{
					return;
				}
				_heldFor = 0f;
				float bodyRadius = PossessionHooks.BodyRadius;
				int num = 0;
				string text = "";
				for (int i = 0; i < 8; i++)
				{
					Vector2 move = new Vector2(Mathf.Cos((float)i * MathF.PI / 4f), Mathf.Sin((float)i * MathF.PI / 4f)) * 0.1f;
					Vector2 vector2 = current_position;
					for (int j = 0; j < 6; j++)
					{
						Vector2 vector3 = Colliders.Slide(vector2, move, bodyRadius, 0.3f);
						if (vector3.sqrMagnitude < 1E-06f)
						{
							break;
						}
						vector2 += vector3;
					}
					float magnitude = (vector2 - current_position).magnitude;
					text = text + magnitude.ToString("F2") + " ";
					if (magnitude > 0.25f)
					{
						num++;
					}
				}
				string text2 = $"at {current_position.x:F2},{current_position.y:F2}, ground {_sampler.Cache.SampleHeight(current_position.x, current_position.y):F2}, {Colliders.BodyForTest}, ways {text}; near: {Colliders.DescribeNear(current_position, 4)}";
				if (num > 0)
				{
					if (Time.unscaledTime - _heldLogged > 15f)
					{
						_heldLogged = Time.unscaledTime;
						Log.Info($"held: pressing on without moving for {1.2f:F1} s, {num} of 8 ways open (a wall ahead, or the game's own block) {text2}");
					}
					return;
				}
				WorldTile worldTile = null;
				for (float num2 = 0.1f; num2 <= 3f; num2 += 0.1f)
				{
					int num3 = Mathf.Max(8, (int)(num2 * 24f));
					for (int k = 0; k < num3; k++)
					{
						float f = (float)k * MathF.PI * 2f / (float)num3;
						Vector2 vector4 = current_position + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num2;
						if (!Colliders.ClearOf(vector4, bodyRadius + 0.03f))
						{
							continue;
						}
						try
						{
							worldTile = World.world.GetTile((int)vector4.x, (int)vector4.y);
						}
						catch
						{
							worldTile = null;
						}
						if (worldTile != null && worldTile.Type != null && !worldTile.Type.liquid && !worldTile.Type.lava && !Climbing.Stops(host, worldTile))
						{
							try
							{
								host.current_position = vector4;
								host.dirty_current_tile = true;
							}
							catch
							{
								return;
							}
							_eyeOffset -= vector4 - current_position;
							UnstuckForTest++;
							Log.Info($"stuck: every way shut for {1.2f:F1} s, slipped out {num2:F1} tiles to {vector4.x:F2},{vector4.y:F2}; held {text2}");
							return;
						}
					}
				}
				Log.Info("stuck: every way shut, and nowhere clear within 3 tiles; held " + text2);
			}
		}

		internal RenderTexture VrEyeForTest(int i)
		{
			return _vrEyes[i];
		}

		internal EyeCamera VrEyeCameraForTest(int i)
		{
			return _vrEyeCam[i];
		}

		internal void VrResetForTest()
		{
			try
			{
				_vr?.Tick(want: false);
				_vr?.Dispose();
			}
			catch
			{
			}
			_vr = null;
			_vrLastYaw = float.NaN;
		}

		private void VrUpdate()
		{
			if (_vr == null)
			{
				if (!VrWanted)
				{
					return;
				}
				_vr = new VrSystem
				{
					Simulated = (VrSimulate || VrByEnvironment == "sim")
				};
			}
			if (!_vrLoop)
			{
				_vrLoop = true;
				StartCoroutine(VrEndOfFrames());
			}
			_vr.LeftHanded = Settings.VrHanded == 1;
			try
			{
				_vr.SyncControls();
			}
			catch (Exception ex)
			{
				Log.Error("vr: controls: " + ex.Message);
			}
			VrPointerSetup();
			bool flag = _settingsMenu.Open || (_active && (AnyMenu || TalkBusy));
			bool flag2 = VrRunning && !_active && _dive == Dive.None && Config.game_loaded;
			_vrWindowUp = _active && (flag || VrMenu.Open);
			VrWristLook();
			VrWristPoint();
			VrControls.Update(_vr, Settings, flag, flag2, flag2 || _vrWindowUp, Time.unscaledDeltaTime, _active && Converse.Open);
			VrNavUpdate();
			try
			{
				VrMapControls(Time.unscaledDeltaTime);
			}
			catch (Exception ex2)
			{
				Log.Error("vr: map controls: " + ex2.Message);
			}
			try
			{
				VrMapTakeOver();
			}
			catch (Exception ex3)
			{
				Log.Error("vr: taking over: " + ex3.Message);
			}
			if (_settingsMenu.VrRow != null)
			{
				_settingsMenu.VrRow.Note = (VrWanted ? _vr.Status : "For Quest Link, Air Link, SteamVR or Virtual Desktop");
			}
		}

		private void VrBeginFrame()
		{
			_vrHeadSet = (_vrStereoPlan = (_vrGodDrawn = (_vrRoomStereo = false)));
			SceneCollector.AllAround = 0f;
			if (_vr == null)
			{
				return;
			}
			try
			{
				_vr.Tick(VrWanted);
			}
			catch (Exception ex)
			{
				Log.Error("vr: " + ex);
			}
		}

		private void VrPacing()
		{
			if (VrRunning)
			{
				if (_vrSavedVsync < 0)
				{
					_vrSavedVsync = QualitySettings.vSyncCount;
					_vrSavedFps = Application.targetFrameRate;
					_vrSavedBackground = Application.runInBackground;
				}
				if (QualitySettings.vSyncCount != 0)
				{
					QualitySettings.vSyncCount = 0;
				}
				if (Application.targetFrameRate != -1)
				{
					Application.targetFrameRate = -1;
				}
				Application.runInBackground = true;
				VrRumble();
			}
			else if (_vrSavedVsync >= 0)
			{
				QualitySettings.vSyncCount = _vrSavedVsync;
				Application.targetFrameRate = _vrSavedFps;
				Application.runInBackground = _vrSavedBackground;
				_vrSavedVsync = -1;
			}
		}

		private void VrRumble()
		{
			CombatFeedback feedback = Feedback;
			if (feedback == null || !_active)
			{
				_vrHits = (_vrHurts = (_vrAttacks = -1));
				return;
			}
			if (_vrHits >= 0)
			{
				if (feedback.Hurts > _vrHurts)
				{
					_vr.Rumble(0, 0.8f, 0.18f);
					_vr.Rumble(1, 0.8f, 0.18f);
				}
				else if (feedback.Hits > _vrHits)
				{
					_vr.Rumble(_vr.Main, feedback.HitWasKill ? 0.9f : 0.5f, feedback.HitWasKill ? 0.15f : 0.07f);
				}
				else if (feedback.Attacks > _vrAttacks)
				{
					_vr.Rumble(_vr.Main, 0.18f, 0.04f);
				}
			}
			_vrHits = feedback.Hits;
			_vrHurts = feedback.Hurts;
			_vrAttacks = feedback.Attacks;
		}

		private void VrLook()
		{
			if (!VrRunning || !_vr.HeadValid)
			{
				_vrLastYaw = float.NaN;
				return;
			}
			Xr.Quat orientation = _vr.Head.Orientation;
			Vector3 forward = ToWorld(Rotate(orientation, 0f, 0f, -1f), 0f);
			Vector3 up = ToWorld(Rotate(orientation, 0f, 1f, 0f), 0f);
			float num = YawOf(forward, up);
			float num2 = Mathf.Asin(Mathf.Clamp(forward.z, -1f, 1f));
			if (float.IsNaN(_vrLastYaw))
			{
				_vrYaw = _view.Yaw - num;
			}
			else
			{
				_vrYaw += Mathf.DeltaAngle(_vrLastYaw * 57.29578f, _view.Yaw * 57.29578f) * (MathF.PI / 180f);
			}
			_vrYaw += VrControls.TakeTurn();
			_view.Yaw = _vrYaw + num;
			_vrLastYaw = _view.Yaw;
			_pitchDegrees = Mathf.Clamp(num2 * 57.29578f, -85f, 85f);
			_vrHeadSet = true;
		}

		private void VrHead(Vector2 head, float lift, bool dead, float dt)
		{
			if (_vrHeadSet)
			{
				float num = Mathf.Max(0.15f, _eyeZ - _groundZ - lift);
				float num2 = num / 1.6f / Mathf.Clamp(Settings.VrWorldSize, 0.25f, 4f);
				_vrScale = (_vrScaleSet ? Mathf.Lerp(_vrScale, num2, 1f - Mathf.Exp(-2f * dt)) : num2);
				_vrScaleSet = true;
				_vrAnchor = new Vector3(head.x + _eyeOffset.x, head.y + _eyeOffset.y, _eyeZ);
				Vector3 vector = VrHeadAt();
				if (!Swimming && !dead)
				{
					vector.z = Mathf.Max(vector.z, _sampler.Cache.SampleHeight(vector.x, vector.y) + 0.05f);
				}
				_view.X = vector.x;
				_view.Y = vector.y;
				_view.Z = vector.z;
				_view.Pitch = Mathf.Tan(_pitchDegrees * (MathF.PI / 180f));
				_view.Fov = 1.8325957f;
				_view.Shear = 0f;
				_vrStereoPlan = _vr.ShouldRender && _vr.ViewsValid && !dead && _dive != Dive.In && !_leaping && Time.unscaledTime >= _gpuRetryAt && (_gpu == null || _gpu.Available);
				if (_vrStereoPlan)
				{
					SceneCollector.AllAround = 2f + num * 4f;
				}
			}
		}

		private Vector3 VrHeadAt()
		{
			Xr.Vec3 position = _vr.Head.Position;
			float num = position.X;
			float num2 = position.Z;
			float num3 = Mathf.Sqrt(num * num + num2 * num2);
			if (num3 > 0.45f)
			{
				num *= 0.45f / num3;
				num2 *= 0.45f / num3;
			}
			float num4 = Mathf.Clamp(position.Y, -1.1f, 0.35f);
			_vrHeadLocal = new Xr.Vec3
			{
				X = num,
				Y = num4,
				Z = num2
			};
			_vrHeadShift = new Xr.Vec3
			{
				X = num - position.X,
				Y = num4 - position.Y,
				Z = num2 - position.Z
			};
			return _vrAnchor + ToWorld(_vrHeadLocal, _vrYaw) * _vrScale;
		}

		private ViewState VrHandsView(ViewState view)
		{
			if (!_vrHeadSet)
			{
				return view;
			}
			view.Pitch = Mathf.Clamp(view.Pitch, -0.7f, 3.73f);
			return view;
		}

		private bool VrStereo(ViewState camera, bool diving, bool dead, Actor host, float body, float ground, double collectMs)
		{
			if (!_vrStereoPlan || diving || dead)
			{
				return false;
			}
			GpuMake();
			double totalMilliseconds = _frameClock.Elapsed.TotalMilliseconds;
			if (!VrEyeCameras(camera.Yaw, SceneCollector.AllAround))
			{
				return false;
			}
			int num = _gpu.FrameNumber + 1;
			for (int i = 0; i < 4; i++)
			{
				_gpuTagged[(num + i) & 0xF].Clear();
				_gpuTagged[(num + i) & 0xF].AddRange(_scene.Tagged);
			}
			_vrFarProps.Clear();
			_vrFarProps.AddRange(Renderer.FarProps);
			_vrFarActors.Clear();
			_vrFarActors.AddRange(Renderer.FarActors);
			_vrStreaks.Clear();
			_vrStreaks.AddRange(Renderer.Streaks);
			bool flag = _gpu.Available;
			try
			{
				for (int j = 0; j < 2 && flag; j++)
				{
					if (j == 1)
					{
						Renderer.FarProps.Clear();
						Renderer.FarProps.AddRange(_vrFarProps);
						Renderer.FarActors.Clear();
						Renderer.FarActors.AddRange(_vrFarActors);
						Renderer.Streaks.Clear();
						Renderer.Streaks.AddRange(_vrStreaks);
					}
					VrDoorwayEye(j, camera, new Vector3(camera.X, camera.Y, camera.Z));
					VrEyeOn(j, new Vector3(camera.X, camera.Y, camera.Z));
					flag = _gpu.Render(Renderer, _sampler.Cache, camera, _vrEyeW[j], _vrEyeH[j], _scene.Billboards, _scene.Voxels, Settings.GpuLook == 0);
					VrEyeOff();
					if (flag)
					{
						VrFlip(_gpu.Output, ref _vrEyes[j], ref _vrEyePtr[j]);
					}
				}
			}
			finally
			{
				VrEyeOff();
			}
			if (!flag)
			{
				string text = (_gpu.Available ? _gpu.Error : (GpuShaders.Error ?? "compute buffers not supported"));
				if (text != _gpuLastError)
				{
					Log.Error("vr: drawing a flat picture for now: " + text);
					_gpuLastError = text;
				}
				GpuRetryLater();
				_vrStereoPlan = false;
				return false;
			}
			_gpuShown = true;
			_gpuFails = 0;
			GpuProven(room: false);
			_vrStereoDrawn = true;
			VrStereoFrames++;
			BaseSimObject picked = null;
			List<BaseSimObject> list = _gpuTagged[_gpu.PickFrame & 0xF];
			if (_gpu.PickTag >= 0 && _gpu.PickTag < list.Count && _gpu.FrameNumber - _gpu.PickFrame <= 15)
			{
				picked = list[_gpu.PickTag];
			}
			_picked = picked;
			Renderer.PickTag = -1;
			Renderer.PickDepth = ((_gpu.PickDepth >= float.MaxValue) ? float.MaxValue : (_gpu.PickDepth * Mathf.Cos(Mathf.Atan(camera.Pitch))));
			AimFrom(camera, host, body, ground);
			FramesShown++;
			_shown = HudProjection(camera);
			_eyeNow = _shown.View;
			_perf.AddLand(_sampler.TakeTimes());
			double drawMs = (VrDrawMsForTest = _frameClock.Elapsed.TotalMilliseconds - totalMilliseconds);
			GpuPerf(collectMs, drawMs);
			VrPerf(drawMs);
			return true;
		}

		private bool VrEyeCameras(float yaw, float behind)
		{
			Vector3 lhs = ToWorld(Rotate(_vr.Head.Orientation, 0f, 0f, -1f), _vrYaw);
			int num = VrEyeWidths[Mathf.Clamp(Settings.VrEyeWidth, 0, VrEyeWidths.Length - 1)];
			for (int i = 0; i < 2; i++)
			{
				Xr.View view = _vr.Views[i];
				Xr.Quat orientation = view.Pose.Orientation;
				Xr.Vec3 vec = new Xr.Vec3
				{
					X = view.Pose.Position.X + _vrHeadShift.X,
					Y = view.Pose.Position.Y + _vrHeadShift.Y,
					Z = view.Pose.Position.Z + _vrHeadShift.Z
				};
				Vector3 vector = ToWorld(Rotate(orientation, 1f, 0f, 0f), _vrYaw);
				Vector3 vector2 = ToWorld(Rotate(orientation, 0f, 1f, 0f), _vrYaw);
				Vector3 vector3 = ToWorld(Rotate(orientation, 0f, 0f, -1f), _vrYaw);
				float num2 = Mathf.Tan(view.Fov.AngleLeft);
				float num3 = Mathf.Tan(view.Fov.AngleRight);
				float num4 = Mathf.Tan(view.Fov.AngleUp);
				float num5 = Mathf.Tan(view.Fov.AngleDown);
				if (num3 - num2 < 0.05f || num4 - num5 < 0.05f)
				{
					return false;
				}
				float num6 = (float)num / (num3 - num2);
				int num7 = Mathf.Clamp(Mathf.RoundToInt(num6 * (num4 - num5)), 64, 4096);
				float num8 = (0f - num2) * num6;
				float num9 = num4 * num6;
				Vector3 position = _vrAnchor + ToWorld(vec, _vrYaw) * _vrScale;
				float num10 = Vector3.Dot(lhs, vector3);
				float value = ((num10 > 0.1f) ? (num8 + num6 * Vector3.Dot(lhs, vector) / num10) : num8);
				float value2 = ((num10 > 0.1f) ? (num9 - num6 * Vector3.Dot(lhs, vector2) / num10) : num9);
				_vrEyeCam[i] = new EyeCamera
				{
					Position = position,
					Right = vector,
					Up = vector2,
					Forward = vector3,
					Focal = num6,
					Cx = num8,
					Cy = num9,
					Yaw = yaw,
					Behind = behind,
					PickX = Mathf.Clamp(value, 0f, num - 1),
					PickY = Mathf.Clamp(value2, 0f, num7 - 1)
				};
				_vrEyePose[i] = new Xr.Pose
				{
					Orientation = orientation,
					Position = vec
				};
				_vrEyeFov[i] = new Xr.Fov
				{
					AngleLeft = Mathf.Atan((0f - num8) / num6),
					AngleRight = Mathf.Atan(((float)num - num8) / num6),
					AngleUp = Mathf.Atan(num9 / num6),
					AngleDown = 0f - Mathf.Atan(((float)num7 - num9) / num6)
				};
				_vrEyeW[i] = num;
				_vrEyeH[i] = num7;
			}
			return true;
		}

		private void VrEyeOn(int i, Vector3 head)
		{
			_gpu.HasEye = true;
			_gpu.Eye = _vrEyeCam[i];
			_gpu.EyeSupersample = Mathf.Clamp(Settings.VrSupersample, 1, 2);
			_gpu.Stereo = true;
			_gpu.StereoHead = head;
			_gpu.ShadowsKept = i == 1;
			_gpu.StereoPair = Time.frameCount;
		}

		private void VrEyeOff()
		{
			if (_gpu != null)
			{
				_gpu.HasEye = false;
				_gpu.Stereo = false;
				_gpu.ShadowsKept = false;
			}
		}

		private FrameProjection HudProjection(ViewState camera)
		{
			int num = Mathf.Max(16, Screen.width);
			int num2 = Mathf.Max(16, Screen.height);
			ViewState view = camera;
			view.Fov = VrHudDegrees * (MathF.PI / 180f);
			view.Shear = 0f;
			float num3 = (float)num * 0.5f / Mathf.Tan(view.Fov * 0.5f);
			Lens lens = Lens.Of(in view, (float)num2 / (float)num);
			return new FrameProjection
			{
				View = view,
				Width = num,
				Height = num2,
				Focal = num3,
				Horizon = (float)num2 * 0.5f + lens.Shift * num3
			};
		}

		private void VrPerf(double drawMs)
		{
			_vrDrawMs += drawMs;
			_vrFrames++;
			if (!(Time.unscaledTime - _vrPerfSince < 10f))
			{
				if (_vrFrames > 0 && _vrPerfSince > 0f)
				{
					Log.Info($"vr perf: {(float)_vrFrames / (Time.unscaledTime - _vrPerfSince):F1} stereo frames/s, both eyes {_vrDrawMs / (double)_vrFrames:F2} ms on the game's thread, {_vrEyeW[0]}x{_vrEyeH[0]} an eye, waited {_vr.FramesWaited}, sent {_vr.FramesSubmitted}");
				}
				_vrPerfSince = Time.unscaledTime;
				_vrDrawMs = 0.0;
				_vrFrames = 0;
			}
		}

		private void OnGUI()
		{
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				OnGUIMod();
			}
			finally
			{
				Hitch.Gui(Stopwatch.GetTimestamp() - timestamp);
			}
		}

		private void OnGUIMod()
		{
			if (VrMouse.Patched || VrRunning || PadCursorOn)
			{
				VrGuiLaser();
			}
			bool flag = Event.current.type == EventType.Repaint && VrHudWanted;
			RenderTexture active = null;
			if (flag)
			{
				VrTarget(ref _vrHud, Screen.width, Screen.height, point: false);
				active = RenderTexture.active;
				RenderTexture.active = _vrHud;
				GL.Clear(clearDepth: true, clearColor: true, new Color(0f, 0f, 0f, 0f));
				_hud.NoWorld = true;
				_hud.VrView = true;
				GameArt.VrBoost = 1.35f;
				_vrHudPass = true;
				_vrWinDrawn = false;
			}
			try
			{
				OnGUIBody();
			}
			finally
			{
				if (flag)
				{
					VrHudPassEnd();
					if (Cursor.visible)
					{
						DrawVrCursor();
					}
					_hud.NoWorld = false;
					_vrHudPass = false;
					VrWristPass();
					RenderTexture.active = active;
					_vrHudDrawn = true;
					VrMirror();
				}
			}
		}

		private void VrHudPassEnd()
		{
			_hud.VrView = false;
			GameArt.VrBoost = 1f;
		}

		private void VrWindowsLayer()
		{
			if (_vrHudPass)
			{
				VrHudPassEnd();
				if (_vrWindowUp)
				{
					VrTarget(ref _vrWin, Screen.width, Screen.height, point: false);
					RenderTexture.active = _vrWin;
					GL.Clear(clearDepth: true, clearColor: true, new Color(0f, 0f, 0f, 0f));
					_vrWinDrawn = true;
				}
			}
			if (VrMenu.Open && _active)
			{
				_hud.DrawVrMenu(Screen.width, Screen.height);
			}
		}

		private void VrMirror()
		{
			float num = Screen.width;
			float num2 = Screen.height;
			Rect position = new Rect(0f, 0f, num, num2);
			if (ShownFrame != null)
			{
				Texture shownFrame = ShownFrame;
				float num3 = Mathf.Min(1f, num2 / num * (float)shownFrame.width / Mathf.Max(1f, shownFrame.height));
				GUI.DrawTextureWithTexCoords(position, shownFrame, new Rect(0f, 0.5f - num3 * 0.5f, 1f, num3), alphaBlend: false);
			}
			GUI.DrawTexture(position, _vrHud, ScaleMode.StretchToFill, alphaBlend: true);
			if (_vrWinDrawn && _vrWin != null)
			{
				GUI.DrawTexture(position, _vrWin, ScaleMode.StretchToFill, alphaBlend: true);
			}
		}

		private void EnsureVrCursor()
		{
			if (_vrCursor != null)
			{
				return;
			}
			_vrCursor = new Texture2D(16, 16, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				hideFlags = HideFlags.HideAndDontSave
			};
			Color32[] array = new Color32[256];
			for (int i = 0; i < 16; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					int num = 15 - i;
					bool flag = (float)j <= (float)num * 0.6f && num < 14;
					bool flag2 = flag && (j == 0 || j >= (int)((float)num * 0.6f) || num == 13);
					array[i * 16 + j] = (flag ? (flag2 ? new Color32(0, 0, 0, byte.MaxValue) : new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue)) : new Color32(0, 0, 0, 0));
				}
			}
			_vrCursor.SetPixels32(array);
			_vrCursor.Apply(updateMipmaps: false, makeNoLongerReadable: true);
		}

		private void DrawVrCursor()
		{
			EnsureVrCursor();
			Vector2 mousePosition = Event.current.mousePosition;
			float num = Mathf.Max(1f, (float)Screen.height / 540f);
			GUI.DrawTexture(new Rect(mousePosition.x, mousePosition.y, 16f * num, 16f * num), _vrCursor);
			if (_vrOtherOnPanel && VrMouse.Driving)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, 0.5f);
				GUI.DrawTexture(new Rect(_vrOtherPixel.x, (float)Screen.height - _vrOtherPixel.y, 16f * num, 16f * num), _vrCursor);
				GUI.color = color;
			}
		}

		private IEnumerator VrEndOfFrames()
		{
			WaitForEndOfFrame end = new WaitForEndOfFrame();
			while (true)
			{
				yield return end;
				try
				{
					VrEndOfFrame();
				}
				catch (Exception ex)
				{
					Log.Error("vr: end of frame: " + ex);
					try
					{
						_vr?.Submit(null);
					}
					catch
					{
					}
				}
			}
		}

		private void VrEndOfFrame()
		{
			if (_vr == null || !_vr.Waited)
			{
				return;
			}
			VrSystem.Layers vrLayers = _vrLayers;
			vrLayers.Clear();
			bool flag = _vr.Recentred && _vr.ShouldRender;
			if (flag)
			{
				_vr.Recentred = false;
			}
			_vrRecentredNow = flag;
			VrSystem.Quad quad = null;
			int num = 0;
			if (_vr.ShouldRender)
			{
				if (_vrHudDrawn)
				{
					num = 1;
					if (_vrStereoDrawn && _vrEyes[0] != null && _vrEyes[1] != null)
					{
						vrLayers.Eyes = 2;
						for (int i = 0; i < 2; i++)
						{
							vrLayers.Pose[i] = _vrEyePose[i];
							vrLayers.Fov[i] = _vrEyeFov[i];
							vrLayers.Picture[i] = _vrEyePtr[i];
							vrLayers.Width[i] = _vrEyes[i].width;
							vrLayers.Height[i] = _vrEyes[i].height;
						}
					}
					else if (ShownFrame != null)
					{
						VrMonoLayer(vrLayers);
					}
					VrFlip(_vrHud, ref _vrPanel, ref _vrPanelPtr);
					float num2 = VrHudWide(1.4f);
					vrLayers.Panel = true;
					vrLayers.PanelPicture = _vrPanelPtr;
					vrLayers.PanelWidth = _vrPanel.width;
					vrLayers.PanelHeight = _vrPanel.height;
					vrLayers.PanelOnHead = true;
					vrLayers.PanelPose = new Xr.Pose
					{
						Orientation = new Xr.Quat
						{
							W = 1f
						},
						Position = new Xr.Vec3
						{
							Z = -1.4f
						}
					};
					vrLayers.PanelMetresWide = num2;
					vrLayers.PanelMetresHigh = num2 * (float)_vrPanel.height / Mathf.Max(1f, _vrPanel.width);
					vrLayers.PanelSeeThrough = true;
					if (_vrWinDrawn && _vrWin != null)
					{
						VrFlip(_vrWin, ref _vrWinPanel, ref _vrWinPanelPtr);
						VrSystem.Quad quad2 = vrLayers.AddQuad();
						if (quad2 != null)
						{
							quad2.Picture = _vrWinPanelPtr;
							quad2.Width = _vrWinPanel.width;
							quad2.Height = _vrWinPanel.height;
							quad2.MetresWide = num2;
							quad2.MetresHigh = vrLayers.PanelMetresHigh;
							quad2.Pose = VrPinned(1, quad2.MetresWide, quad2.MetresHigh);
							quad2.OnHead = false;
							quad2.SeeThrough = true;
							quad = quad2;
						}
					}
					VrWristLayer(vrLayers);
				}
				else if (_vrGodDrawn && _vrStereoDrawn && _vrEyes[0] != null && _vrEyes[1] != null)
				{
					num = 2;
					VrGodLayers(vrLayers);
				}
				else
				{
					num = 3;
					VrScreenLayer(vrLayers);
				}
			}
			bool flag2 = vrLayers.Panel && (num != 1 || _vrWindowUp);
			_vrAimPanel = ((!flag2) ? default(VrPanelPlace) : ((quad != null) ? new VrPanelPlace
			{
				Up = true,
				Pose = quad.Pose,
				Wide = quad.MetresWide,
				High = quad.MetresHigh,
				Solid = true,
				OnHead = false
			} : new VrPanelPlace
			{
				Up = true,
				Pose = vrLayers.PanelPose,
				Wide = vrLayers.PanelMetresWide,
				High = vrLayers.PanelMetresHigh,
				Solid = (!vrLayers.PanelSeeThrough || num == 1),
				OnHead = vrLayers.PanelOnHead
			}));
			VrFadeLayer(vrLayers, num);
			_vr.Submit(vrLayers);
			_vrHudDrawn = (_vrStereoDrawn = (_vrGodDrawn = (_vrWinDrawn = false)));
		}

		private void VrFadeLayer(VrSystem.Layers layers, int showing)
		{
			if (showing != 0)
			{
				if (_vrShowing != 0 && showing != _vrShowing)
				{
					if (_vrFade < 0.5f)
					{
						VrFadesForTest++;
					}
					_vrFade = 1f;
				}
				_vrShowing = showing;
			}
			if (_vrFade <= 0f)
			{
				return;
			}
			RenderTexture vrFadePicture = _vrFadePicture;
			VrTarget(ref _vrFadePicture, 16, 16, point: true);
			if (_vrFadePicture != vrFadePicture || _vrFadePtr == IntPtr.Zero)
			{
				if (_vrFadePtr != IntPtr.Zero)
				{
					_vr.Forget(_vrFadePtr);
				}
				_vrFadePtr = _vrFadePicture.GetNativeTexturePtr();
			}
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = _vrFadePicture;
			GL.Clear(clearDepth: false, clearColor: true, new Color(0f, 0f, 0f, _vrFade));
			RenderTexture.active = active;
			layers.Fade = _vrFade;
			layers.FadePicture = _vrFadePtr;
			layers.FadeSize = 16;
			_vrFade = Mathf.MoveTowards(_vrFade, 0f, Mathf.Min(Time.unscaledDeltaTime, 0.05f) / 0.4f);
		}

		private void VrMonoLayer(VrSystem.Layers layers)
		{
			FrameProjection shown = _shown;
			if (shown.Width > 0 && shown.Height > 0 && !(shown.Focal <= 0f))
			{
				VrFlip(ShownFrame, ref _vrMono, ref _vrMonoPtr);
				Lens frameLens = shown.FrameLens;
				float num = shown.View.Yaw - _vrYaw;
				Xr.Quat orientation = Mul(new Xr.Quat
				{
					Y = Mathf.Sin(num * 0.5f),
					W = Mathf.Cos(num * 0.5f)
				}, new Xr.Quat
				{
					X = Mathf.Sin(frameLens.Tilt * 0.5f),
					W = Mathf.Cos(frameLens.Tilt * 0.5f)
				});
				float f = (float)shown.Width * 0.5f / shown.Focal;
				Xr.Fov fov = new Xr.Fov
				{
					AngleLeft = 0f - Mathf.Atan(f),
					AngleRight = Mathf.Atan(f),
					AngleUp = Mathf.Atan(shown.Horizon / shown.Focal),
					AngleDown = 0f - Mathf.Atan(((float)shown.Height - shown.Horizon) / shown.Focal)
				};
				layers.Eyes = 2;
				for (int i = 0; i < 2; i++)
				{
					layers.Pose[i] = new Xr.Pose
					{
						Orientation = orientation,
						Position = _vrHeadLocal
					};
					layers.Fov[i] = fov;
					layers.Picture[i] = _vrMonoPtr;
					layers.Width[i] = _vrMono.width;
					layers.Height[i] = _vrMono.height;
				}
			}
		}

		private void VrScreenLayer(VrSystem.Layers layers)
		{
			int width = Screen.width;
			int height = Screen.height;
			if (width >= 16 && height >= 16)
			{
				VrTarget(ref _vrScreen, width, height, point: false);
				ScreenCapture.CaptureScreenshotIntoRenderTexture(_vrScreen);
				if (_vrScreenPtr == IntPtr.Zero)
				{
					_vrScreenPtr = _vrScreen.GetNativeTexturePtr();
				}
				if ((VrMouse.Driving && VrMouse.Now.OnScreen) || (Cursor.visible && Cursor.lockState != CursorLockMode.Locked))
				{
					EnsureVrCursor();
					VrScreenPointer(_vrScreen);
				}
				float num = VrHudWide(1.4f);
				float num2 = num * (float)height / (float)width;
				layers.Panel = true;
				layers.PanelPicture = _vrScreenPtr;
				layers.PanelWidth = width;
				layers.PanelHeight = height;
				layers.PanelOnHead = false;
				layers.PanelPose = VrPinned(3, num, num2);
				layers.PanelMetresWide = num;
				layers.PanelMetresHigh = num2;
				layers.PanelSeeThrough = false;
			}
		}

		private void VrScreenPointer(RenderTexture screen)
		{
			if (!(_vrCursor == null))
			{
				Vector3 mousePosition = Input.mousePosition;
				float num = Mathf.Max(1f, (float)screen.height / 540f) * 16f;
				RenderTexture active = RenderTexture.active;
				RenderTexture.active = screen;
				GL.PushMatrix();
				GL.LoadPixelMatrix(0f, screen.width, 0f, screen.height);
				Graphics.DrawTexture(new Rect(mousePosition.x, (float)screen.height - mousePosition.y, num, num), _vrCursor, new Rect(0f, 1f, 1f, -1f), 0, 0, 0, 0);
				if (_vrOtherOnPanel && VrMouse.Driving)
				{
					Graphics.DrawTexture(new Rect(_vrOtherPixel.x, (float)screen.height - _vrOtherPixel.y, num, num), _vrCursor, new Rect(0f, 1f, 1f, -1f), 0, 0, 0, 0, new Color(0.5f, 0.5f, 0.5f, 0.25f));
				}
				GL.PopMatrix();
				RenderTexture.active = active;
			}
		}

		private void VrTarget(ref RenderTexture rt, int w, int h, bool point)
		{
			RenderTextureFormat renderTextureFormat = ((_vr != null && _vr.Bgra) ? RenderTextureFormat.BGRA32 : RenderTextureFormat.ARGB32);
			if (rt != null && rt.width == w && rt.height == h && rt.format == renderTextureFormat)
			{
				return;
			}
			if (rt != null)
			{
				if (rt == _vrScreen)
				{
					_vr?.Forget(_vrScreenPtr);
					_vrScreenPtr = IntPtr.Zero;
				}
				rt.Release();
				UnityEngine.Object.Destroy(rt);
			}
			rt = new RenderTexture(w, h, 0, renderTextureFormat)
			{
				filterMode = ((!point) ? FilterMode.Bilinear : FilterMode.Point),
				wrapMode = TextureWrapMode.Clamp,
				name = "FirstPersonVr"
			};
			rt.Create();
		}

		private void VrFlip(Texture src, ref RenderTexture dst, ref IntPtr ptr)
		{
			if (src == null)
			{
				return;
			}
			if (dst == null || dst.width != src.width || dst.height != src.height || (_vr != null && dst.format != (RenderTextureFormat)(_vr.Bgra ? 20 : 0)))
			{
				if (ptr != IntPtr.Zero)
				{
					_vr?.Forget(ptr);
				}
				ptr = IntPtr.Zero;
				RenderTexture rt = dst;
				VrTarget(ref rt, src.width, src.height, point: true);
				dst = rt;
			}
			RenderTexture active = RenderTexture.active;
			Graphics.Blit(src, dst, new Vector2(1f, -1f), new Vector2(0f, 1f));
			RenderTexture.active = active;
			if (ptr == IntPtr.Zero)
			{
				ptr = dst.GetNativeTexturePtr();
			}
		}

		private void DisposeVr()
		{
			try
			{
				_vr?.Dispose();
			}
			catch
			{
			}
			VrControls.Active = false;
			RenderTexture[] array = new RenderTexture[11]
			{
				_vrEyes[0],
				_vrEyes[1],
				_vrMono,
				_vrHud,
				_vrPanel,
				_vrScreen,
				_vrFadePicture,
				_vrWin,
				_vrWinPanel,
				_vrWrist,
				_vrWristPanel
			};
			foreach (RenderTexture renderTexture in array)
			{
				if (renderTexture != null)
				{
					renderTexture.Release();
				}
			}
		}

		private static Vector3 ToWorld(Xr.Vec3 v, float yaw)
		{
			float num = Mathf.Cos(yaw);
			float num2 = Mathf.Sin(yaw);
			return new Vector3(v.X * num2 - v.Z * num, (0f - v.X) * num - v.Z * num2, v.Y);
		}

		private static Xr.Vec3 Rotate(Xr.Quat q, float x, float y, float z)
		{
			float num = 2f * (q.Y * z - q.Z * y);
			float num2 = 2f * (q.Z * x - q.X * z);
			float num3 = 2f * (q.X * y - q.Y * x);
			return new Xr.Vec3
			{
				X = x + q.W * num + (q.Y * num3 - q.Z * num2),
				Y = y + q.W * num2 + (q.Z * num - q.X * num3),
				Z = z + q.W * num3 + (q.X * num2 - q.Y * num)
			};
		}

		private static Xr.Quat Mul(Xr.Quat a, Xr.Quat b)
		{
			return new Xr.Quat
			{
				X = a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
				Y = a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
				Z = a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
				W = a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
			};
		}

		private static float YawOf(Vector3 forward, Vector3 up)
		{
			float num = forward.x - up.x * forward.z;
			float num2 = forward.y - up.y * forward.z;
			if (num * num + num2 * num2 < 1E-08f)
			{
				num = forward.x;
				num2 = forward.y;
			}
			return Mathf.Atan2(num2, num);
		}

		private bool VrGodGather(ViewState camera, Vector2 at, ref float reach)
		{
			_godTaskRoom.Valid = false;
			_vrTurnSinceGather = 0f;
			if (!VrRunning || !_vr.ShouldRender || !GpuWanted || Time.unscaledTime < _gpuRetryAt || _godAlpha < 1f || _dive != Dive.None)
			{
				return false;
			}
			Vector3 godLook = _godLook;
			Vector3 vector = new Vector3(camera.X, camera.Y, camera.Z);
			Vector3 vector2 = vector - godLook;
			float magnitude = new Vector2(vector2.x, vector2.y).magnitude;
			Vector2 vector3 = ((magnitude > 0.001f) ? (new Vector2(vector2.x, vector2.y) / magnitude) : (-new Vector2(Mathf.Cos(camera.Yaw), Mathf.Sin(camera.Yaw))));
			float num = Mathf.Max(0.5f, vector.z - _godGround);
			float num2 = Mathf.Lerp(3f, 1.6f, GodCloseness) * Mathf.Clamp(Settings.VrWorldSize, 0.25f, 4f);
			float a = reach;
			reach = Mathf.Min(400f, Mathf.Max(a, num * 4f + 60f));
			_godTaskDetail = Mathf.Min(reach, Mathf.Max(a, num * 2f + 30f));
			_godTaskRoom = new VrGodRoom
			{
				Valid = true,
				Seat = vector,
				SeatFromMap = new Vector3(vector.x - at.x, vector.y - at.y, vector.z),
				Yaw = Mathf.Atan2(0f - vector3.y, 0f - vector3.x),
				Scale = num / num2,
				Tilt = Mathf.Clamp(Mathf.Atan2(vector2.z, Mathf.Max(0.001f, magnitude)), 0f, 1.3962634f)
			};
			float f = Mathf.Clamp(0f - ((float.IsNaN(_vrGodHeadYaw) ? (0f - _godTaskRoom.Tilt) : _vrGodHeadPitch) - MathF.PI / 3f) - MathF.PI / 2f, 0f, 1.3089969f);
			_godTaskRoom.Behind = Mathf.Min(reach * 0.8f, num * Mathf.Tan(f) + num * 0.3f + 8f);
			return true;
		}

		private static Xr.Quat GodTilt(Xr.Quat head, float tilt)
		{
			Xr.Vec3 vec = Rotate(head, 0f, 0f, -1f);
			Xr.Vec3 vec2 = Rotate(head, 0f, 1f, 0f);
			float num = vec.X - vec2.X * vec.Y;
			float num2 = vec.Z - vec2.Z * vec.Y;
			if (num * num + num2 * num2 < 1E-08f)
			{
				num = vec.X;
				num2 = vec.Z;
			}
			float num3 = Mathf.Atan2(0f - num, 0f - num2);
			Xr.Quat a = new Xr.Quat
			{
				Y = Mathf.Sin(num3 * 0.5f),
				W = Mathf.Cos(num3 * 0.5f)
			};
			Xr.Quat b = new Xr.Quat
			{
				Y = 0f - a.Y,
				W = a.W
			};
			Xr.Quat b2 = new Xr.Quat
			{
				X = Mathf.Sin((0f - tilt) * 0.5f),
				W = Mathf.Cos((0f - tilt) * 0.5f)
			};
			return Mul(Mul(a, b2), b);
		}

		private Xr.Vec3 VrGodPivotStep()
		{
			float unscaledTime = Time.unscaledTime;
			Xr.Vec3 position = _vr.Head.Position;
			if (!_vrGodPivotSet || unscaledTime - _vrGodPivotAt > 0.5f)
			{
				_vrGodPivot = position;
			}
			else
			{
				float num = 1f - Mathf.Exp((0f - Mathf.Max(0f, unscaledTime - _vrGodPivotAt)) / 1.2f);
				_vrGodPivot = new Xr.Vec3
				{
					X = _vrGodPivot.X + (position.X - _vrGodPivot.X) * num,
					Y = _vrGodPivot.Y + (position.Y - _vrGodPivot.Y) * num,
					Z = _vrGodPivot.Z + (position.Z - _vrGodPivot.Z) * num
				};
			}
			_vrGodPivotSet = true;
			_vrGodPivotAt = unscaledTime;
			return _vrGodPivot;
		}

		private static Xr.Pose Tipped(Xr.Pose p, Xr.Quat tilt, Xr.Vec3 head)
		{
			Xr.Vec3 vec = Rotate(tilt, p.Position.X - head.X, p.Position.Y - head.Y, p.Position.Z - head.Z);
			return new Xr.Pose
			{
				Orientation = Mul(tilt, p.Orientation),
				Position = new Xr.Vec3
				{
					X = head.X + vec.X,
					Y = head.Y + vec.Y,
					Z = head.Z + vec.Z
				}
			};
		}

		private ViewState VrGodGatherView()
		{
			VrGodRoom godTaskRoom = _godTaskRoom;
			float yaw = (float.IsNaN(_vrGodHeadYaw) ? godTaskRoom.Yaw : _vrGodHeadYaw);
			float value = (float.IsNaN(_vrGodHeadYaw) ? (0f - godTaskRoom.Tilt) : _vrGodHeadPitch);
			return new ViewState
			{
				X = godTaskRoom.Seat.x,
				Y = godTaskRoom.Seat.y,
				Z = godTaskRoom.Seat.z,
				Yaw = yaw,
				Pitch = Mathf.Tan(Mathf.Clamp(value, -1.48f, 1.48f)),
				Fov = 1.9198622f,
				Shear = 0f
			};
		}

		private bool VrGodEyes()
		{
			VrGodRoom godTaskRoom = _godTaskRoom;
			if (!godTaskRoom.Valid || !VrRunning || !_vr.Waited || !_vr.ShouldRender || !_vr.ViewsValid || !_vr.HeadValid)
			{
				return false;
			}
			if (!GpuWanted || Time.unscaledTime < _gpuRetryAt)
			{
				return false;
			}
			GpuMake();
			if (!_gpu.Available)
			{
				return false;
			}
			long timestamp = Stopwatch.GetTimestamp();
			Xr.Quat tilt = GodTilt(_vr.Head.Orientation, godTaskRoom.Tilt);
			Xr.Vec3 vec = VrGodPivotStep();
			Xr.Pose pose = Tipped(_vr.Head, tilt, vec);
			Xr.Quat orientation = pose.Orientation;
			Vector3 forward = ToWorld(Rotate(orientation, 0f, 0f, -1f), godTaskRoom.Yaw);
			Vector3 up = ToWorld(Rotate(orientation, 0f, 1f, 0f), godTaskRoom.Yaw);
			float num = YawOf(forward, up);
			float vrGodHeadPitch = Mathf.Asin(Mathf.Clamp(forward.z, -1f, 1f));
			int num2 = VrEyeWidths[Mathf.Clamp(Settings.VrEyeWidth, 0, VrEyeWidths.Length - 1)];
			for (int i = 0; i < 2; i++)
			{
				Xr.View view = _vr.Views[i];
				Xr.Pose pose2 = Tipped(view.Pose, tilt, vec);
				Xr.Quat orientation2 = pose2.Orientation;
				Vector3 right = ToWorld(Rotate(orientation2, 1f, 0f, 0f), godTaskRoom.Yaw);
				Vector3 up2 = ToWorld(Rotate(orientation2, 0f, 1f, 0f), godTaskRoom.Yaw);
				Vector3 forward2 = ToWorld(Rotate(orientation2, 0f, 0f, -1f), godTaskRoom.Yaw);
				float num3 = Mathf.Tan(view.Fov.AngleLeft);
				float num4 = Mathf.Tan(view.Fov.AngleRight);
				float num5 = Mathf.Tan(view.Fov.AngleUp);
				float num6 = Mathf.Tan(view.Fov.AngleDown);
				if (num4 - num3 < 0.05f || num5 - num6 < 0.05f)
				{
					return false;
				}
				float num7 = (float)num2 / (num4 - num3);
				int num8 = Mathf.Clamp(Mathf.RoundToInt(num7 * (num5 - num6)), 64, 4096);
				float num9 = (0f - num3) * num7;
				float num10 = num5 * num7;
				_vrEyeCam[i] = new EyeCamera
				{
					Position = godTaskRoom.Seat + ToWorld(pose2.Position, godTaskRoom.Yaw) * godTaskRoom.Scale,
					Right = right,
					Up = up2,
					Forward = forward2,
					Focal = num7,
					Cx = num9,
					Cy = num10,
					Yaw = num,
					Behind = godTaskRoom.Behind,
					PickX = num9,
					PickY = num10
				};
				_vrEyePose[i] = view.Pose;
				_vrEyeFov[i] = new Xr.Fov
				{
					AngleLeft = Mathf.Atan((0f - num9) / num7),
					AngleRight = Mathf.Atan(((float)num2 - num9) / num7),
					AngleUp = Mathf.Atan(num10 / num7),
					AngleDown = 0f - Mathf.Atan(((float)num8 - num10) / num7)
				};
				_vrEyeW[i] = num2;
				_vrEyeH[i] = num8;
			}
			Vector3 vector = godTaskRoom.Seat + ToWorld(pose.Position, godTaskRoom.Yaw) * godTaskRoom.Scale;
			ViewState view2 = new ViewState
			{
				X = vector.x,
				Y = vector.y,
				Z = vector.z,
				Yaw = num,
				Pitch = 0f,
				Fov = 1.7453293f,
				Shear = 0f
			};
			int count = _scene.Voxels.Count;
			if (VrMouse.Driving)
			{
				if (_vrAimHand >= 0 && (GodView.LaserOn || _vrBeamMetres > 0f))
				{
					AddVrBeam(_scene.Voxels, godTaskRoom, tilt, vec, vector, _vrAimHand, BeamBright, _vrBeamMetres);
				}
				if (_vrOtherHand >= 0 && !_vrOtherOnPanel)
				{
					AddVrBeam(_scene.Voxels, godTaskRoom, tilt, vec, vector, _vrOtherHand, BeamDim);
				}
			}
			for (int j = 1; j <= 2; j++)
			{
				_gpuTagged[(_gpu.FrameNumber + j) & 0xF].Clear();
			}
			_vrFarProps.Clear();
			_vrFarProps.AddRange(Renderer.FarProps);
			_vrFarActors.Clear();
			_vrFarActors.AddRange(Renderer.FarActors);
			_vrStreaks.Clear();
			_vrStreaks.AddRange(Renderer.Streaks);
			_gpu.ShadowReach = Mathf.Clamp(_godTaskZoom * 2.2f, GpuRenderer.EyeShadowReach, 200f);
			_gpu.PixelFar = false;
			bool flag = true;
			try
			{
				for (int k = 0; k < 2 && flag; k++)
				{
					if (k == 1)
					{
						Renderer.FarProps.Clear();
						Renderer.FarProps.AddRange(_vrFarProps);
						Renderer.FarActors.Clear();
						Renderer.FarActors.AddRange(_vrFarActors);
						Renderer.Streaks.Clear();
						Renderer.Streaks.AddRange(_vrStreaks);
					}
					VrEyeOn(k, vector);
					flag = _gpu.Render(Renderer, _sampler.Cache, view2, _vrEyeW[k], _vrEyeH[k], _scene.Billboards, _scene.Voxels, Settings.GpuLook == 0);
					VrEyeOff();
					if (flag)
					{
						VrFlip(_gpu.Output, ref _vrEyes[k], ref _vrEyePtr[k]);
					}
				}
			}
			finally
			{
				VrEyeOff();
				_gpu.ShadowReach = GpuRenderer.EyeShadowReach;
				if (_scene.Voxels.Count > count)
				{
					_scene.Voxels.RemoveRange(count, _scene.Voxels.Count - count);
				}
			}
			if (!flag)
			{
				string text = (_gpu.Available ? _gpu.Error : (GpuShaders.Error ?? "compute buffers not supported"));
				if (text != _gpuLastError)
				{
					Log.Error("vr: the god view drawn flat for now: " + text);
					_gpuLastError = text;
				}
				GpuRetryLater();
				return false;
			}
			_gpuShown = true;
			_gpuFails = 0;
			FramesShown++;
			_godShown = _godTaskCamera;
			_vrGodDrawn = (_vrStereoDrawn = true);
			_vrGodRoom = Turned(godTaskRoom, _vrTurnSinceGather);
			_vrGodHeadYaw = num;
			_vrGodHeadPitch = vrGodHeadPitch;
			VrGodFrames++;
			if (_godDepth.Length != 0)
			{
				_godDepth = new float[0];
			}
			SetGodMouseFrame(_godTaskCamera, _vrEyeW[1], _vrEyeH[1], _godTaskGround, _godTaskAt, SightCut.Off);
			VrPerf(VrDrawMsForTest = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency);
			return true;
		}

		private void VrGodCover(Camera cam)
		{
			if (_godScreen != null && _godScreen.activeSelf)
			{
				_godScreen.SetActive(value: false);
			}
			if (_godCam != cam && _godScreen != null)
			{
				UnityEngine.Object.Destroy(_godScreen);
				_godScreen = null;
			}
			_godCam = cam;
			if (_godSavedMask == -1)
			{
				_godSavedMask = cam.cullingMask;
			}
			if (cam.cullingMask != 0)
			{
				cam.cullingMask = 0;
			}
			if (!_vrCovered || _vrCoveredCam != cam)
			{
				VrGodUncover();
				_vrCovered = true;
				_vrCoveredCam = cam;
				_vrSavedClear = cam.clearFlags;
				_vrSavedClearColor = cam.backgroundColor;
				Behaviour[] components = cam.GetComponents<Behaviour>();
				foreach (Behaviour behaviour in components)
				{
					if (behaviour != null && behaviour.enabled && behaviour.GetType().Name.IndexOf("SleekRender", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						behaviour.enabled = false;
						_vrPostOff.Add(behaviour);
					}
				}
			}
			cam.clearFlags = CameraClearFlags.Color;
			cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
			MuteOtherCameras(mute: true, cam);
		}

		private void VrGodUncover()
		{
			if (!_vrCovered)
			{
				return;
			}
			_vrCovered = false;
			try
			{
				if (_vrCoveredCam != null)
				{
					_vrCoveredCam.clearFlags = _vrSavedClear;
					_vrCoveredCam.backgroundColor = _vrSavedClearColor;
				}
				foreach (Behaviour item in _vrPostOff)
				{
					if (item != null)
					{
						item.enabled = true;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("vr: the map camera back: " + ex.Message);
			}
			_vrPostOff.Clear();
			_vrCoveredCam = null;
		}

		private void VrGodLayers(VrSystem.Layers layers)
		{
			layers.Eyes = 2;
			for (int i = 0; i < 2; i++)
			{
				layers.Pose[i] = _vrEyePose[i];
				layers.Fov[i] = _vrEyeFov[i];
				layers.Picture[i] = _vrEyePtr[i];
				layers.Width[i] = _vrEyes[i].width;
				layers.Height[i] = _vrEyes[i].height;
			}
			int width = Screen.width;
			int height = Screen.height;
			if (width >= 16 && height >= 16)
			{
				VrTarget(ref _vrScreen, width, height, point: false);
				ScreenCapture.CaptureScreenshotIntoRenderTexture(_vrScreen);
				if (_vrScreenPtr == IntPtr.Zero)
				{
					_vrScreenPtr = _vrScreen.GetNativeTexturePtr();
				}
				if (VrMouse.Driving && VrMouse.Now.OnScreen)
				{
					EnsureVrCursor();
					VrScreenPointer(_vrScreen);
				}
				VrGodMirror();
				float num = VrHudWide(1.4f);
				float num2 = num * (float)height / (float)width;
				layers.Panel = true;
				layers.PanelPicture = _vrScreenPtr;
				layers.PanelWidth = width;
				layers.PanelHeight = height;
				layers.PanelOnHead = false;
				layers.PanelPose = VrPinned(2, num, num2);
				layers.PanelMetresWide = num;
				layers.PanelMetresHigh = num2;
				layers.PanelSeeThrough = true;
			}
		}

		private void VrGodMirror()
		{
			Texture shownFrame = ShownFrame;
			if (shownFrame == null || _vrScreen == null)
			{
				return;
			}
			try
			{
				RenderTexture active = RenderTexture.active;
				float num = Mathf.Min(1f, (float)Screen.height / (float)Mathf.Max(1, Screen.width) * (float)shownFrame.width / Mathf.Max(1f, shownFrame.height));
				Graphics.Blit(shownFrame, null, new Vector2(1f, num), new Vector2(0f, 0.5f - num * 0.5f));
				if (_vrUiMaterial == null)
				{
					Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
					if (shader != null)
					{
						_vrUiMaterial = new Material(shader)
						{
							name = "FirstPersonVrMirror"
						};
					}
				}
				if (_vrUiMaterial != null)
				{
					RenderTexture.active = null;
					_vrUiMaterial.mainTexture = _vrScreen;
					_vrUiMaterial.SetPass(0);
					GL.PushMatrix();
					GL.LoadOrtho();
					GL.Begin(7);
					GL.Color(Color.white);
					GL.TexCoord2(0f, 1f);
					GL.Vertex3(0f, 0f, 0f);
					GL.TexCoord2(0f, 0f);
					GL.Vertex3(0f, 1f, 0f);
					GL.TexCoord2(1f, 0f);
					GL.Vertex3(1f, 1f, 0f);
					GL.TexCoord2(1f, 1f);
					GL.Vertex3(1f, 0f, 0f);
					GL.End();
					GL.PopMatrix();
				}
				RenderTexture.active = active;
			}
			catch (Exception ex)
			{
				if (!_vrMirrorFailed)
				{
					Log.Error("vr: the desktop's view of the god view: " + ex.Message);
				}
				_vrMirrorFailed = true;
			}
		}

		private void VrMapControls(float dt)
		{
			if (!VrRunning || !_vr.Focused || _active || _dive != Dive.None || !Config.game_loaded || World.world == null)
			{
				return;
			}
			Camera camera = World.world.camera;
			MoveCamera instance = MoveCamera.instance;
			if (camera == null || instance == null)
			{
				return;
			}
			bool flag = _vrGodRoom.Valid && GodShown && _vrCovered;
			if (VrControls.Down(KeyCode.Escape) && !_settingsMenu.Open)
			{
				try
				{
					if (ScrollWindow.isWindowActive())
					{
						ScrollWindow.hideAllEvent(pWithAnimation: false);
					}
					else if (!NoPowerPicked())
					{
						PowerButtonSelector.instance.unselectAll();
						GodToast("Power put down");
					}
				}
				catch (Exception ex)
				{
					Log.Error("vr: closing the window: " + ex.Message);
				}
			}
			if (_vr.BinocularsButton && !_vrStickClickWas)
			{
				_vrPinRecentre = true;
			}
			_vrStickClickWas = _vr.BinocularsButton;
			if (IsTyping() || _settingsMenu.Open)
			{
				_vrZoomWheel = 0f;
				_vrPanning = false;
				return;
			}
			float f = MathF.PI / 2f;
			if (flag && _vr.HeadValid)
			{
				Xr.Quat orientation = _vr.Head.Orientation;
				f = YawOf(ToWorld(Rotate(orientation, 0f, 0f, -1f), _vrGodRoom.Yaw), ToWorld(Rotate(orientation, 0f, 1f, 0f), _vrGodRoom.Yaw));
			}
			Vector2 vector = Dead(_vr.MoveStick, 0.18f);
			if (vector.sqrMagnitude > 0f)
			{
				float num = dt * 55f * instance._target_zoom * instance.camera_move_speed;
				float num2 = Mathf.Cos(f);
				float num3 = Mathf.Sin(f);
				instance._move_velocity.x += (num2 * vector.y + num3 * vector.x) * num;
				instance._move_velocity.y += (num3 * vector.y - num2 * vector.x) * num;
				instance.clampVelocity();
				instance._mouse_controls_used_last = false;
				_vrPanning = true;
			}
			Vector2 turnStick = _vr.TurnStick;
			bool flag2 = Mathf.Abs(turnStick.y) > 0.25f && Mathf.Abs(turnStick.y) > Mathf.Abs(turnStick.x);
			if (flag && !flag2)
			{
				float x = turnStick.x;
				float num4 = 0f;
				if (Settings.VrTurn == 0)
				{
					if (Mathf.Abs(x) > 0.75f)
					{
						if (_vrSnapArmed)
						{
							num4 = (0f - Mathf.Sign(x)) * Settings.VrSnapAngle * (MathF.PI / 180f);
						}
						_vrSnapArmed = false;
					}
					else if (Mathf.Abs(x) < 0.35f)
					{
						_vrSnapArmed = true;
					}
				}
				else
				{
					float num5 = ((Mathf.Abs(x) < 0.15f) ? 0f : ((x - Mathf.Sign(x) * 0.15f) / 0.85f));
					num4 = (0f - num5) * Mathf.Abs(num5) * Settings.VrTurnSpeed * (MathF.PI / 180f) * dt;
				}
				if (num4 != 0f)
				{
					VrGodTurnRound(camera, num4);
				}
			}
			if (flag2 && !VrMouse.Now.OnScreen)
			{
				float num6 = (Mathf.Abs(turnStick.y) - 0.25f) / 0.75f * Mathf.Sign(turnStick.y);
				instance.setTargetZoom(Mathf.Max(10f, instance._target_zoom * Mathf.Exp((0f - num6) * 1.5f * dt)));
				_vrZoomWheel += num6 * 3f * dt;
			}
			if (!flag)
			{
				_vrZoomWheel = 0f;
				_vrPanning = false;
			}
		}

		private void VrGodTurnRound(Camera cam, float delta)
		{
			VrGodRoom vrGodRoom = _vrGodRoom;
			Vector2 vector = cam.transform.position;
			Vector3 vector2 = ToWorld(_vr.HeadValid ? _vr.Head.Position : default(Xr.Vec3), vrGodRoom.Yaw) * vrGodRoom.Scale;
			Vector2 vector3 = vector + new Vector2(vrGodRoom.SeatFromMap.x + vector2.x, vrGodRoom.SeatFromMap.y + vector2.y);
			float num = Mathf.Cos(delta);
			float num2 = Mathf.Sin(delta);
			Vector2 vector4 = vector - vector3;
			Vector2 vector5 = vector3 + new Vector2(vector4.x * num - vector4.y * num2, vector4.x * num2 + vector4.y * num);
			cam.transform.position = new Vector3(vector5.x, vector5.y, cam.transform.position.z);
			_godCamWas += new Vector3(vector5.x - vector.x, vector5.y - vector.y, 0f);
			_godTurn = DivePath.Wrap(_godTurn + delta);
			GodView.Turn = _godTurn;
			_vrGodRoom = Turned(vrGodRoom, delta);
			_vrTurnSinceGather += delta;
		}

		private static VrGodRoom Turned(VrGodRoom r, float delta)
		{
			if (delta == 0f)
			{
				return r;
			}
			float num = Mathf.Cos(delta);
			float num2 = Mathf.Sin(delta);
			r.SeatFromMap = new Vector3(r.SeatFromMap.x * num - r.SeatFromMap.y * num2, r.SeatFromMap.x * num2 + r.SeatFromMap.y * num, r.SeatFromMap.z);
			r.Yaw += delta;
			return r;
		}

		private static Vector2 Dead(Vector2 v, float dead)
		{
			float magnitude = v.magnitude;
			if (magnitude <= dead)
			{
				return Vector2.zero;
			}
			return v / magnitude * Mathf.Min(1f, (magnitude - dead) / (1f - dead));
		}

		private void AddVrBeam(List<VoxelObject> voxels, VrGodRoom room, Xr.Quat tilt, Xr.Vec3 pivot, Vector3 eye, int which, uint color, float toPanel = -1f)
		{
			Xr.Pose pose = Tipped(_vr.HandValid[which] ? _vr.Hands[which] : _vrHandLast[which], tilt, pivot);
			Vector3 vector = room.Seat + ToWorld(pose.Position, room.Yaw) * room.Scale;
			Vector3 vector2 = ToWorld(Rotate(pose.Orientation, 0f, 0f, -1f), room.Yaw);
			Vec3 vec = new Vec3(vector.x, vector.y, vector.z);
			Vec3 direction = new Vec3(vector2.x, vector2.y, vector2.z);
			if (!MarkShapes.TryGetValue(color, out var value))
			{
				value = VoxelShape.NewGrid(1, 1, 1);
				value.Box(0, 0, -1, 1, 1, 1, color);
				MarkShapes[color] = value;
			}
			Vec3 point;
			float num = ((toPanel > 0f) ? (toPanel * room.Scale) : (GodCamera.GroundAlong(_sampler.Cache, vec, direction, out point) ? Mathf.Sqrt((point.X - vec.X) * (point.X - vec.X) + (point.Y - vec.Y) * (point.Y - vec.Y) + (point.Z - vec.Z) * (point.Z - vec.Z)) : (room.Scale * 8f)));
			for (int i = 0; i < 40; i++)
			{
				float num2 = num * Mathf.Pow(((float)i + 0.5f) / 40f, 1.6f);
				float num3 = vec.X + direction.X * num2;
				float num4 = vec.Y + direction.Y * num2;
				float num5 = vec.Z + direction.Z * num2;
				float num6 = Mathf.Sqrt((num3 - eye.x) * (num3 - eye.x) + (num4 - eye.y) * (num4 - eye.y) + (num5 - eye.z) * (num5 - eye.z));
				float num7 = Mathf.Max(0.02f, num6 * 0.004f);
				voxels.Add(new VoxelObject
				{
					Shape = value,
					Emissive = true,
					NoShadow = true,
					Tag = -1,
					U = new Vec3(1f, 0f, 0f),
					V = new Vec3(0f, 0f, 1f),
					N = new Vec3(0f, 1f, 0f),
					SizeU = num7,
					SizeV = num7,
					SizeN = num7,
					Origin = new Vec3(num3 - num7 * 0.5f, num4 - num7 * 0.5f, num5 - num7 * 0.5f),
					SortDepth = num6
				});
			}
		}

		internal Vector3 VrRoomToWorldForTest(Xr.Vec3 p)
		{
			return _vrAnchor + ToWorld(new Xr.Vec3
			{
				X = p.X + _vrHeadShift.X,
				Y = p.Y + _vrHeadShift.Y,
				Z = p.Z + _vrHeadShift.Z
			}, _vrYaw) * _vrScale;
		}

		internal Xr.Vec3 VrWorldToRoomForTest(Vector3 w)
		{
			Vector3 vector = (w - _vrAnchor) / Mathf.Max(0.0001f, _vrScale);
			float num = Mathf.Cos(_vrYaw);
			float num2 = Mathf.Sin(_vrYaw);
			return new Xr.Vec3
			{
				X = vector.x * num2 - vector.y * num - _vrHeadShift.X,
				Y = vector.z - _vrHeadShift.Y,
				Z = (0f - vector.x) * num - vector.y * num2 - _vrHeadShift.Z
			};
		}

		private void VrHandsInput(ref HandsInput input, bool outdoors)
		{
			_vrHandsOn = outdoors && VrHandsTracked;
			_bow.Tracked = _vrHandsOn && !_vrRoomStereo;
			if (_vrHandsOn)
			{
				input.Tracked = true;
				input.TrackR = VrHandTrack(_vr.Main);
				input.TrackL = VrHandTrack(_vr.Off);
				input.BowOnString = _bow.OnString;
			}
		}

		private HandTrack VrHandTrack(int i)
		{
			if (!_vr.HandValid[i] || !_vr.GripValid[i])
			{
				return default(HandTrack);
			}
			Xr.Pose pose = _vr.Hands[i];
			Xr.Pose pose2 = _vr.Grips[i];
			Xr.Vec3 v = new Xr.Vec3
			{
				X = pose2.Position.X + _vrHeadShift.X,
				Y = pose2.Position.Y + _vrHeadShift.Y,
				Z = pose2.Position.Z + _vrHeadShift.Z
			};
			Vector3 vector = _vrAnchor + ToWorld(v, _vrYaw) * _vrScale;
			Vector3 vector2 = ToWorld(Rotate(pose.Orientation, 0f, 0f, -1f), _vrYaw);
			Vector3 vector3 = ToWorld(Rotate(pose.Orientation, 0f, 1f, 0f), _vrYaw);
			return new HandTrack
			{
				Valid = true,
				At = new Vec3(vector.x, vector.y, vector.z),
				Forward = new Vec3(vector2.x, vector2.y, vector2.z),
				Up = new Vec3(vector3.x, vector3.y, vector3.z)
			};
		}

		private void VrHandsAfter(List<VoxelObject> voxels, Actor host)
		{
			VrAimDotShown = false;
			if (!_vrHandsOn || !_hands.TrackAimValid || _vrRoomStereo)
			{
				VrSwings(host);
				FirstPersonAim.ClearHand();
				_vrNockWas = false;
				_vrDrawTick = 0f;
			}
			else
			{
				FirstPersonAim.SetHand(_hands.TrackAimFrom, _hands.TrackAimDir, _hands.TrackFreeFrom, _hands.TrackFreeDir);
				VrBowFeel();
				AddVrAimDot(voxels, host);
				VrSwings(host);
			}
		}

		private void VrBowFeel()
		{
			if (!_bow.InHand)
			{
				_vrNockWas = false;
				_vrDrawTick = 0f;
				return;
			}
			bool trackNockNear = _hands.TrackNockNear;
			if (trackNockNear && !_vrNockWas)
			{
				_vr.Rumble(_vr.Main, 0.25f, 0.03f);
			}
			_vrNockWas = trackNockNear;
			if (_bow.OnString)
			{
				float num = Mathf.Clamp01(_hands.TrackPull);
				if (num >= _vrDrawTick + 0.12f)
				{
					_vr.Rumble(_vr.Off, 0.15f + 0.35f * num, 0.025f);
					_vr.Rumble(_vr.Main, 0.1f + 0.25f * num, 0.025f);
					_vrDrawTick = num;
				}
				else if (num < _vrDrawTick - 0.12f)
				{
					_vrDrawTick = num;
				}
			}
			else
			{
				_vrDrawTick = 0f;
			}
			if (_bow.State.Fired)
			{
				_vr.Rumble(_vr.Off, 0.75f, 0.08f);
				_vr.Rumble(_vr.Main, 0.45f, 0.05f);
			}
		}

		private void AddVrAimDot(List<VoxelObject> voxels, Actor host)
		{
			bool flag = _bow.InHand || _blaster.InHand || _shotgun.InHand;
			if (!flag && host != null)
			{
				try
				{
					flag = !Holstered && host.getWeapon() != null && !host.hasMeleeAttack();
				}
				catch
				{
				}
			}
			if (flag && FirstPersonAim.ShotPoint(out var point, out var _, pastFriends: true))
			{
				if (!MarkShapes.TryGetValue(AimDotColor, out var value))
				{
					value = VoxelShape.NewGrid(1, 1, 1);
					value.Box(0, 0, -1, 1, 1, 1, AimDotColor);
					MarkShapes[AimDotColor] = value;
				}
				Vec3 vec = new Vec3(_view.X, _view.Y, _view.Z);
				float num = Mathf.Sqrt(Vec3.Dot(point - vec, point - vec));
				float num2 = Mathf.Max(0.01f, num * 0.005f);
				voxels.Add(new VoxelObject
				{
					Shape = value,
					Overlay = true,
					Emissive = true,
					NoShadow = true,
					Tag = -1,
					U = new Vec3(1f, 0f, 0f),
					V = new Vec3(0f, 0f, 1f),
					N = new Vec3(0f, 1f, 0f),
					SizeU = num2,
					SizeV = num2,
					SizeN = num2,
					Origin = new Vec3(point.X - num2 * 0.5f, point.Y - num2 * 0.5f, point.Z - num2 * 0.5f),
					SortDepth = num
				});
				VrAimDotShown = true;
				VrAimDotForTest = point;
			}
		}

		private void VrNavUpdate()
		{
			if (!(PadNav.VrOn = VrRunning && _vrWindowUp && !VrMenu.Open && (!_active || !Converse.Open)))
			{
				_vrNavOn = (_vrNavPress = false);
				_vrNavClick = 0;
				VrControls.TakeNav();
				VrControls.TakeTab();
				return;
			}
			float unscaledTime = Time.unscaledTime;
			Vector2Int vector2Int = VrControls.TakeNav();
			if (vector2Int != Vector2Int.zero && PadNav.Count == 0)
			{
				_vrNavLater = vector2Int;
				_vrNavLaterUntil = unscaledTime + 0.4f;
				vector2Int = Vector2Int.zero;
			}
			else if (vector2Int == Vector2Int.zero && _vrNavLater != Vector2Int.zero && PadNav.Count > 0 && unscaledTime < _vrNavLaterUntil)
			{
				vector2Int = _vrNavLater;
			}
			if (PadNav.Count > 0 || unscaledTime >= _vrNavLaterUntil)
			{
				_vrNavLater = Vector2Int.zero;
			}
			Vector2 vector = (_vrNavOn ? _vrNavGui : (VrMouse.Now.OnScreen ? PadGui(VrMouse.Now.Pixel) : new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f)));
			if (vector2Int.x != 0 && vector2Int.y == 0 && _vrNavOn && PadNav.StepSlider(vector, vector2Int.x))
			{
				vector2Int = Vector2Int.zero;
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			if (vector2Int != Vector2Int.zero && PadNav.Hop(vector, vector2Int, out var to))
			{
				VrNavTo(to);
			}
			int num = VrControls.TakeTab();
			if (num != 0 && PadNav.TabStep(vector, num, out var to2))
			{
				VrNavTo(to2);
				_vrNavClick = 2;
			}
			if (_vrNavOn)
			{
				if (_vrNavHand >= 0 && _vr.HandValid[_vrNavHand] && Vector3.Angle(VrHandWay(_vrNavHand), _vrNavHandWay) > 8f)
				{
					_vrNavOn = false;
				}
				if (_vr.TriggerOf(0) > 0.55f || _vr.TriggerOf(1) > 0.55f)
				{
					_vrNavOn = false;
				}
			}
			_vrNavPress = _vrNavOn && VrControls.NavHeld;
		}

		private void VrNavTo(Vector2 gui)
		{
			_vrNavOn = true;
			_vrNavGui = gui;
			_vrNavHand = ((_vrActiveHand >= 0) ? _vrActiveHand : _vr.Main);
			_vrNavHandWay = (_vr.HandValid[_vrNavHand] ? VrHandWay(_vrNavHand) : Vector3.forward);
			Sounds.PlayUI("event:/SFX/Ui/Click");
			_vr.Rumble(_vr.Main, 0.12f, 0.015f);
		}

		private Vector3 VrHandWay(int hand)
		{
			return V(Rotate(_vr.Hands[hand].Orientation, 0f, 0f, -1f));
		}

		private void VrNavAim(ref VrMouse.Laser laser)
		{
			if (_vrNavOn && VrRunning)
			{
				laser.InPlay = true;
				laser.OnScreen = true;
				laser.Pixel = PadPixel(_vrNavGui);
				laser.Gui = _active || _settingsMenu.Open || Run.WindowOpen;
				laser.Left = _vrNavPress || _vrNavClick > 1;
				laser.Right = (laser.Middle = false);
				if (_vrNavClick > 0)
				{
					_vrNavClick--;
				}
			}
		}

		private void DrawVrNav()
		{
			if (_vrNavOn && VrRunning && Event.current.type == EventType.Repaint && PadNav.Under(_vrNavGui, out var r))
			{
				GameArt.Ensure();
				FocusFrame(r);
			}
		}

		private static void FocusFrame(Rect r)
		{
			float num = Mathf.Max(2f, Mathf.Round(GameArt.P * 1.5f));
			Color orange = GameArt.Orange;
			GameArt.Flat(new Rect(r.x - num, r.y - num, r.width + num * 2f, num), orange);
			GameArt.Flat(new Rect(r.x - num, r.yMax, r.width + num * 2f, num), orange);
			GameArt.Flat(new Rect(r.x - num, r.y, num, r.height), orange);
			GameArt.Flat(new Rect(r.xMax, r.y, num, r.height), orange);
		}

		private bool VrWheelPoint(out Vector2 gui)
		{
			gui = default(Vector2);
			if (!VrRunning || !_vr.Focused || _vrPanel == null)
			{
				return false;
			}
			int num = _vr.Main;
			if (!_vr.HandValid[num])
			{
				num = _vr.Off;
			}
			if (!_vr.HandValid[num])
			{
				return false;
			}
			float num2 = VrHudWide(1.4f);
			VrPanelPlace p = new VrPanelPlace
			{
				Up = true,
				OnHead = true,
				Solid = true,
				Wide = num2,
				High = num2 * (float)_vrPanel.height / Mathf.Max(1f, _vrPanel.width),
				Pose = new Xr.Pose
				{
					Orientation = new Xr.Quat
					{
						W = 1f
					},
					Position = new Xr.Vec3
					{
						Z = -1.4f
					}
				}
			};
			p.Pose = PanelInRoom(p);
			Xr.Pose pose = _vr.Hands[num];
			PanelHit(p, pose.Position, Rotate(pose.Orientation, 0f, 0f, -1f), out var u, out var v, out var facing, out var _);
			if (!facing)
			{
				return false;
			}
			gui = new Vector2(u * (float)Screen.width, v * (float)Screen.height);
			return true;
		}

		private Xr.Pose VrPinned(int kind, float wide, float high)
		{
			float unscaledTime = Time.unscaledTime;
			float num = Mathf.Clamp(unscaledTime - _vrPin.LastAt, 0f, 0.1f);
			Xr.Pose pose = (_vr.HeadValid ? _vr.Head : Xr.Pose.Identity);
			Gaze(pose.Orientation, out var yaw, out var pitch);
			float num2 = Mathf.Clamp(pitch, -0.34906584f, 0.17453292f);
			bool flag = _vrPressOnPanel && (_vrTrig[0] || _vrTrig[1] || _vrGrip[0] || _vrGrip[1]);
			if (!_vrPin.Set || _vrPin.Kind != kind || unscaledTime - _vrPin.LastAt > 0.5f || _vrRecentredNow)
			{
				_vrPin = new VrPin
				{
					Set = true,
					Kind = kind,
					Yaw = yaw,
					Pitch = num2,
					From = pose.Position,
					AwaySince = -1f
				};
			}
			else
			{
				float num3 = Mathf.Atan(wide * 0.5f / 1.4f);
				float num4 = Mathf.Abs(Mathf.DeltaAngle(_vrPin.Yaw * 57.29578f, yaw * 57.29578f)) * (MathF.PI / 180f);
				float num5 = pose.Position.X - _vrPin.From.X;
				float num6 = pose.Position.Z - _vrPin.From.Z;
				bool num7 = num4 > num3 + MathF.PI * 2f / 45f || num5 * num5 + num6 * num6 > 0.36f;
				if (_vrPinRecentre)
				{
					_vrPin.Gliding = (_vrPin.Lift = true);
					_vrPinRecentre = false;
				}
				if (num7 && !flag)
				{
					if (_vrPin.AwaySince < 0f)
					{
						_vrPin.AwaySince = unscaledTime;
					}
					if (unscaledTime - _vrPin.AwaySince > 0.6f)
					{
						_vrPin.Gliding = true;
					}
				}
				else if (!_vrPin.Gliding)
				{
					_vrPin.AwaySince = -1f;
				}
				if (_vrPin.Gliding && !flag)
				{
					float num8 = 1f - Mathf.Exp((0f - num) / 0.2f);
					float num9 = Mathf.DeltaAngle(_vrPin.Yaw * 57.29578f, yaw * 57.29578f) * (MathF.PI / 180f);
					_vrPin.Yaw += num9 * num8;
					if (_vrPin.Lift)
					{
						_vrPin.Pitch += (num2 - _vrPin.Pitch) * num8;
					}
					_vrPin.From = new Xr.Vec3
					{
						X = _vrPin.From.X + (pose.Position.X - _vrPin.From.X) * num8,
						Y = _vrPin.From.Y + (pose.Position.Y - _vrPin.From.Y) * num8,
						Z = _vrPin.From.Z + (pose.Position.Z - _vrPin.From.Z) * num8
					};
					if (Mathf.Abs(num9) < MathF.PI / 120f && (!_vrPin.Lift || Mathf.Abs(num2 - _vrPin.Pitch) < MathF.PI / 120f))
					{
						_vrPin.Gliding = (_vrPin.Lift = false);
						_vrPin.AwaySince = -1f;
					}
				}
			}
			_vrPin.LastAt = unscaledTime;
			Xr.Quat quat = Mul(new Xr.Quat
			{
				Y = Mathf.Sin(_vrPin.Yaw * 0.5f),
				W = Mathf.Cos(_vrPin.Yaw * 0.5f)
			}, new Xr.Quat
			{
				X = Mathf.Sin(_vrPin.Pitch * 0.5f),
				W = Mathf.Cos(_vrPin.Pitch * 0.5f)
			});
			Xr.Vec3 vec = Rotate(quat, 0f, 0f, -1.4f);
			return new Xr.Pose
			{
				Orientation = quat,
				Position = new Xr.Vec3
				{
					X = _vrPin.From.X + vec.X,
					Y = _vrPin.From.Y + vec.Y,
					Z = _vrPin.From.Z + vec.Z
				}
			};
		}

		private static void Gaze(Xr.Quat head, out float yaw, out float pitch)
		{
			Xr.Vec3 vec = Rotate(head, 0f, 0f, -1f);
			Xr.Vec3 vec2 = Rotate(head, 0f, 1f, 0f);
			float num = vec.X - vec2.X * vec.Y;
			float num2 = vec.Z - vec2.Z * vec.Y;
			if (num * num + num2 * num2 < 1E-08f)
			{
				num = vec.X;
				num2 = vec.Z;
			}
			yaw = Mathf.Atan2(0f - num, 0f - num2);
			pitch = Mathf.Asin(Mathf.Clamp(vec.Y, -1f, 1f));
		}

		private Xr.Pose PanelInRoom(VrPanelPlace p)
		{
			if (!p.OnHead)
			{
				return p.Pose;
			}
			Xr.Pose pose = ((_vr != null && _vr.HeadValid) ? _vr.Head : Xr.Pose.Identity);
			Xr.Vec3 vec = Rotate(pose.Orientation, p.Pose.Position.X, p.Pose.Position.Y, p.Pose.Position.Z);
			return new Xr.Pose
			{
				Orientation = Mul(pose.Orientation, p.Pose.Orientation),
				Position = new Xr.Vec3
				{
					X = pose.Position.X + vec.X,
					Y = pose.Position.Y + vec.Y,
					Z = pose.Position.Z + vec.Z
				}
			};
		}

		private void VrPointerSetup()
		{
			if (VrMouse.Aim == null)
			{
				VrMouse.Aim = PointerAim;
			}
			if (RadialWheel.PointAt == null)
			{
				RadialWheel.PointAt = VrWheelPoint;
			}
			if (!VrRunning)
			{
				return;
			}
			if (!VrMouse.Patched)
			{
				VrMouse.Install();
			}
			EventSystem current = EventSystem.current;
			if (!(current != null) || current.isFocused || !(EventFocus != null))
			{
				return;
			}
			try
			{
				EventFocus.SetValue(current, true);
			}
			catch
			{
			}
		}

		private void VrAim(ref VrMouse.Laser laser)
		{
			GodView.LaserOn = false;
			_vrAimHand = -1;
			if (!VrRunning || !_vr.Focused || !Config.game_loaded || World.world == null)
			{
				VrAimIdle();
				return;
			}
			bool flag = !_active && _dive == Dive.None;
			if (!flag && (!_active || !_vrWindowUp))
			{
				VrAimIdle();
				return;
			}
			float now = Time.unscaledTime;
			float num = Mathf.Max(0.001f, Time.unscaledDeltaTime);
			for (int i = 0; i < 2; i++)
			{
				if (_vr.HandValid[i])
				{
					_vrHandLast[i] = _vr.Hands[i];
					_vrHandSeen[i] = now;
				}
			}
			int num2 = -1;
			for (int j = 0; j < 2; j++)
			{
				float num3 = _vr.TriggerOf(j);
				float num4 = _vr.GripOf(j);
				bool flag2 = Seen(j) && Pulled(num3, _vrTrig[j]);
				bool flag3 = Seen(j) && Pulled(num4, _vrGrip[j]);
				float num5 = (Seen(j) ? Mathf.Max(num3, num4) : 0f);
				_vrPullStarts[j] = num5 > 0.12f && _vrPullWas[j] <= 0.12f;
				_vrPullWas[j] = num5;
				if ((flag2 && !_vrTrig[j]) || (flag3 && !_vrGrip[j]))
				{
					num2 = ((num2 < 0 || j == _vrActiveHand) ? j : num2);
				}
				_vrTrig[j] = flag2;
				_vrGrip[j] = flag3;
				Xr.Vec3 vec = Rotate(_vrHandLast[j].Orientation, 0f, 0f, -1f);
				Vector3 vector = new Vector3(vec.X, vec.Y, vec.Z);
				_vrHandBusy[j] = _vrHandBusy[j] * Mathf.Exp((0f - num) / 0.3f) + (Seen(j) ? Vector3.Angle(vector, _vrHandWay[j]) : 0f);
				_vrHandWay[j] = vector;
			}
			int num6;
			if (_vrPressHand >= 0 && Seen(_vrPressHand))
			{
				num6 = _vrPressHand;
			}
			else if (num2 >= 0)
			{
				num6 = num2;
			}
			else
			{
				num6 = ((_vrActiveHand >= 0 && Seen(_vrActiveHand)) ? _vrActiveHand : (Seen(1) ? 1 : ((!Seen(0)) ? (-1) : 0)));
				int num7 = 1 - Mathf.Max(0, num6);
				if (num6 >= 0 && Seen(num7) && _vrHandBusy[num7] > 6f && _vrHandBusy[num7] > 2f * _vrHandBusy[num6])
				{
					num6 = num7;
				}
			}
			if (num6 < 0)
			{
				VrAimIdle();
				return;
			}
			_vrActiveHand = num6;
			laser.InPlay = true;
			_vrAimHand = num6;
			Xr.Pose p = VrSteady(num6, _vrHandLast[num6], now, num);
			Xr.Vec3 position = p.Position;
			Xr.Vec3 d = Rotate(p.Orientation, 0f, 0f, -1f);
			bool flag4 = _vrTrig[num6];
			bool flag5 = _vrGrip[num6];
			bool flag6 = flag4 || flag5;
			_vrPressHand = (flag6 ? num6 : (-1));
			bool flag7 = false;
			bool num8 = flag6 && !_vrWasAny;
			if (!flag6)
			{
				_vrPressOnPanel = (_vrPressOnLand = false);
			}
			VrPanelPlace vrAimPanel = _vrAimPanel;
			vrAimPanel.Pose = PanelInRoom(vrAimPanel);
			float u;
			float v;
			bool facing;
			float along;
			bool flag8 = PanelHit(vrAimPanel, position, d, out u, out v, out facing, out along);
			if (vrAimPanel.Up && !_vrPressOnLand && flag8)
			{
				Vector2 pixel = new Vector2(u * (float)Screen.width, (1f - v) * (float)Screen.height);
				if (vrAimPanel.Solid || _vrPressOnPanel || OverControls(pixel))
				{
					flag7 = (laser.OnScreen = true);
					laser.Pixel = pixel;
					laser.Gui = _active || _settingsMenu.Open || Run.WindowOpen;
				}
			}
			else if (vrAimPanel.Up && _vrPressOnPanel && flag6)
			{
				Vector2 pixel2 = (facing ? new Vector2(Mathf.Clamp01(u) * (float)Screen.width, (1f - Mathf.Clamp01(v)) * (float)Screen.height) : _vrPanelPixel);
				flag7 = (laser.OnScreen = true);
				laser.Pixel = pixel2;
				laser.Gui = _active || _settingsMenu.Open || Run.WindowOpen;
			}
			if (flag7)
			{
				_vrPanelPixel = laser.Pixel;
			}
			_vrBeamMetres = (flag7 ? along : (-1f));
			if (num8)
			{
				_vrPressOnPanel = flag7;
				_vrPressOnLand = !flag7;
			}
			if (num8 && flag7)
			{
				_vr.Rumble(num6, 0.3f, 0.02f);
			}
			VrHoverTick(num6, flag7 && !flag6 && !laser.Gui, laser.Pixel);
			if (flag5 && !_vrWasGrip)
			{
				_vrGripRight = flag7 && (!flag || OverControls(laser.Pixel));
			}
			if (!flag4)
			{
				_vrTakeOverPress = false;
			}
			else if (!_vrWasTrigger && flag && NoPowerPicked() && (!flag7 || (vrAimPanel.Solid && !OverControls(laser.Pixel))))
			{
				_vrTakeOverPress = (_vrTakeOverWanted = true);
			}
			_vrWasTrigger = flag4;
			laser.Left = flag4 && !_vrTakeOverPress;
			laser.Right = flag5 && _vrGripRight;
			laser.Middle = flag5 && !_vrGripRight && flag;
			_vrWasAny = flag6;
			_vrWasGrip = flag5;
			VrOtherPointer(num6, vrAimPanel, Seen(1 - num6));
			if ((flag7 || _vrNavOn) && (flag || _vrWindowUp))
			{
				float y = _vr.TurnStick.y;
				_vrScroll = ((Mathf.Abs(y) < 0.3f) ? 0f : (_vrScroll + y * 8f * Time.unscaledDeltaTime));
				if (Mathf.Abs(_vrScroll) >= 1f)
				{
					laser.Scroll = Mathf.Sign(_vrScroll);
					_vrScroll -= laser.Scroll;
				}
			}
			else
			{
				_vrScroll = 0f;
			}
			if (!flag7 && flag && (_settingsMenu.Open || Run.WindowOpen))
			{
				laser.Left = (laser.Right = (laser.Middle = false));
			}
			else if (!flag7 && flag && _vrGodRoom.Valid && GodShown && _vrCovered)
			{
				VrGodRoom vrGodRoom = _vrGodRoom;
				Xr.Pose pose = Tipped(p, GodTilt(_vr.Head.Orientation, vrGodRoom.Tilt), VrGodPivot);
				Xr.Vec3 position2 = pose.Position;
				Xr.Vec3 v2 = Rotate(pose.Orientation, 0f, 0f, -1f);
				Vector2 laserAt = World.world.camera.transform.position;
				Vector3 vector2 = new Vector3(laserAt.x + vrGodRoom.SeatFromMap.x, laserAt.y + vrGodRoom.SeatFromMap.y, vrGodRoom.SeatFromMap.z) + ToWorld(position2, vrGodRoom.Yaw) * vrGodRoom.Scale;
				Vector3 vector3 = ToWorld(v2, vrGodRoom.Yaw);
				GodView.LaserOn = true;
				GodView.LaserFrom = new Vec3(vector2.x, vector2.y, vector2.z);
				GodView.LaserDir = new Vec3(vector3.x, vector3.y, vector3.z);
				GodView.LaserAt = laserAt;
				GodView.LaserTurn = _godTurn;
				laser.Moved = Vector3.Angle(vector3, _vrLaserDirWas) > 1f;
				if (laser.Moved)
				{
					_vrLaserDirWas = vector3;
				}
			}
			bool Seen(int num9)
			{
				return now - _vrHandSeen[num9] < 0.3f;
			}
		}

		private Xr.Pose VrSteady(int hand, Xr.Pose pose, float now, float dt)
		{
			Xr.Vec3 vec = Rotate(pose.Orientation, 0f, 0f, -1f);
			Vector3 vector = new Vector3(vec.X, vec.Y, vec.Z);
			Vector3 vector2 = vector;
			Vector3 vrHoldRay = vector;
			if (now - _vrRayAt[hand] < 0.3f)
			{
				Vector3 vector3 = _vrRay[hand];
				vrHoldRay = ((_vrRayOld[hand].sqrMagnitude > 0.5f) ? _vrRayOld[hand] : vector3);
				float num = Vector3.Angle(vector3, vector) / dt;
				float num2 = 1.2f + 0.06f * num;
				vector2 = Vector3.Slerp(vector3, vector, 1f - Mathf.Exp((0f - dt) * 2f * MathF.PI * num2)).normalized;
			}
			_vrRayOld[hand] = ((now - _vrRayAt[hand] < 0.3f) ? _vrRay[hand] : vector2);
			_vrRay[hand] = vector2;
			_vrRayAt[hand] = now;
			bool flag = _vrTrig[hand] || _vrGrip[hand];
			if (_vrHoldHand >= 0 && _vrHoldHand != hand)
			{
				_vrHoldHand = -1;
			}
			if (_vrHoldHand == hand)
			{
				bool num3 = Vector3.Angle(_vrHoldRay, vector2) > 2.5f;
				bool flag2 = !flag && _vrPullWas[hand] < 0.08f;
				bool flag3 = !flag && now - _vrHoldSince > 0.6f;
				if (num3 || flag2 || flag3)
				{
					_vrHoldHand = -1;
				}
			}
			else if (_vrPullStarts[hand] || (flag && _vrHoldHand < 0 && !_vrWasAny))
			{
				_vrHoldHand = hand;
				_vrHoldRay = vrHoldRay;
				_vrHoldSince = now;
			}
			Vector3 vector4 = ((_vrHoldHand == hand) ? _vrHoldRay : vector2);
			if (Vector3.Angle(vector4, vector) < 0.01f)
			{
				return pose;
			}
			Quaternion quaternion = Quaternion.FromToRotation(vector, vector4);
			pose.Orientation = Mul(new Xr.Quat
			{
				X = quaternion.x,
				Y = quaternion.y,
				Z = quaternion.z,
				W = quaternion.w
			}, pose.Orientation);
			return pose;
		}

		private void VrHoverTick(int hand, bool over, Vector2 pixel)
		{
			Selectable selectable = null;
			if (over)
			{
				EventSystem current = EventSystem.current;
				if (current != null)
				{
					if (_vrPointerData == null || _vrPointerSystem != current)
					{
						_vrPointerData = new PointerEventData(current);
						_vrPointerSystem = current;
					}
					_vrPointerData.position = pixel;
					_vrUiHits.Clear();
					try
					{
						current.RaycastAll(_vrPointerData, _vrUiHits);
					}
					catch
					{
					}
					if (_vrUiHits.Count > 0 && _vrUiHits[0].gameObject != null)
					{
						selectable = _vrUiHits[0].gameObject.GetComponentInParent<Selectable>();
					}
					_vrUiHits.Clear();
				}
			}
			if (selectable != _vrHoverButton && selectable != null)
			{
				_vr.Rumble(hand, 0.12f, 0.012f);
			}
			_vrHoverButton = selectable;
		}

		private static bool NoPowerPicked()
		{
			try
			{
				return string.IsNullOrEmpty(World.world.getSelectedPowerID());
			}
			catch
			{
				return false;
			}
		}

		private void VrMapTakeOver()
		{
			if (!_vrTakeOverWanted)
			{
				return;
			}
			_vrTakeOverWanted = false;
			if (!VrRunning || _active || _dive != Dive.None || World.world == null)
			{
				return;
			}
			Actor actor = null;
			Vector2 at;
			if (GodView.Active)
			{
				GodView.Resolve();
				actor = GodView.MouseCreature;
				at = GodView.LandPoint;
				if (actor == null && !GodView.MouseValid)
				{
					return;
				}
			}
			else
			{
				at = World.world.getMousePos();
			}
			if (actor == null)
			{
				actor = NearestCreature(at, 1.3f);
			}
			if (actor != null)
			{
				if (!Possess.CanJumpTo(actor))
				{
					Toast("You can't take over " + Describe(actor));
				}
				else if (Possess.JumpTo(actor))
				{
					VrTakeOversForTest++;
				}
			}
		}

		private static Actor NearestCreature(Vector2 at, float within)
		{
			Actor result = null;
			float num = within * within;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive())
					{
						float sqrMagnitude = (simple.current_position - at).sqrMagnitude;
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							result = simple;
						}
					}
				}
			}
			catch
			{
			}
			return result;
		}

		private void VrAimIdle()
		{
			_vrBeamMetres = -1f;
			_vrTakeOverPress = (_vrWasTrigger = false);
			bool[] vrTrig = _vrTrig;
			bool[] vrTrig2 = _vrTrig;
			bool flag;
			_vrGrip[0] = (flag = (_vrGrip[1] = false));
			vrTrig2[1] = (flag = flag);
			vrTrig[0] = flag;
			_vrWasAny = (_vrWasGrip = (_vrGripRight = false));
			_vrPressOnPanel = (_vrPressOnLand = false);
			_vrPressHand = -1;
			_vrScroll = 0f;
			_vrOtherHand = -1;
			_vrOtherOnPanel = false;
		}

		private void VrOtherPointer(int hand, VrPanelPlace panel, bool seen)
		{
			int num = 1 - hand;
			_vrOtherHand = (seen ? num : (-1));
			_vrOtherOnPanel = false;
			if (!seen || !panel.Up)
			{
				return;
			}
			Xr.Pose pose = _vrHandLast[num];
			if (PanelHit(panel, pose.Position, Rotate(pose.Orientation, 0f, 0f, -1f), out var u, out var v, out var _, out var _))
			{
				Vector2 vector = new Vector2(u * (float)Screen.width, (1f - v) * (float)Screen.height);
				if (panel.Solid || OverControls(vector))
				{
					_vrOtherOnPanel = true;
					_vrOtherPixel = vector;
				}
			}
		}

		private static bool Pulled(float v, bool was)
		{
			if (!was)
			{
				return v > 0.55f;
			}
			return v > 0.4f;
		}

		private static bool PanelHit(VrPanelPlace p, Xr.Vec3 o, Xr.Vec3 d, out float u, out float v, out bool facing, out float along)
		{
			u = (v = (along = 0f));
			facing = false;
			Xr.Quat q = new Xr.Quat
			{
				X = 0f - p.Pose.Orientation.X,
				Y = 0f - p.Pose.Orientation.Y,
				Z = 0f - p.Pose.Orientation.Z,
				W = p.Pose.Orientation.W
			};
			Xr.Vec3 vec = Rotate(q, o.X - p.Pose.Position.X, o.Y - p.Pose.Position.Y, o.Z - p.Pose.Position.Z);
			Xr.Vec3 vec2 = Rotate(q, d.X, d.Y, d.Z);
			if (vec2.Z > -0.0001f || vec.Z <= 0f)
			{
				return false;
			}
			facing = true;
			float num = (0f - vec.Z) / vec2.Z;
			along = num * Mathf.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
			float num2 = vec.X + vec2.X * num;
			float num3 = vec.Y + vec2.Y * num;
			u = num2 / p.Wide + 0.5f;
			v = 0.5f - num3 / p.High;
			if (u >= 0f && u <= 1f && v >= 0f)
			{
				return v <= 1f;
			}
			return false;
		}

		private bool OverControls(Vector2 pixel)
		{
			if (_settingsMenu.Open || Run.WindowOpen)
			{
				return true;
			}
			EventSystem current = EventSystem.current;
			if (current == null)
			{
				return false;
			}
			if (_vrPointerData == null || _vrPointerSystem != current)
			{
				_vrPointerData = new PointerEventData(current);
				_vrPointerSystem = current;
			}
			_vrPointerData.position = pixel;
			_vrUiHits.Clear();
			try
			{
				current.RaycastAll(_vrPointerData, _vrUiHits);
			}
			catch
			{
				return false;
			}
			bool result = _vrUiHits.Count > 0;
			_vrUiHits.Clear();
			return result;
		}

		private void VrGuiLaser()
		{
			if (!VrMouse.Driving)
			{
				return;
			}
			Event current = Event.current;
			if (current == null)
			{
				return;
			}
			current.mousePosition = VrMouse.GuiPoint;
			if (VrMouse.Posts)
			{
				return;
			}
			VrMouse.GuiEvents(_vrGuiEvents);
			if (_vrGuiEvents.Count == 0)
			{
				return;
			}
			try
			{
				foreach (Event vrGuiEvent in _vrGuiEvents)
				{
					Event.current = new Event
					{
						type = EventType.Layout,
						mousePosition = vrGuiEvent.mousePosition
					};
					OnGUIBody();
					Event.current = vrGuiEvent;
					OnGUIBody();
				}
			}
			catch (Exception ex)
			{
				Log.Error("vr: the laser in a window: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}

		private void VrRoomHead(ref ViewState view, float floorZ, float dt)
		{
			_vrRoomStereo = false;
			if (_vrHeadSet && _vr.ShouldRender && _vr.ViewsValid && _dive != Dive.In && GpuRooms && GpuRoomsSafe && !(Time.unscaledTime < _gpuRetryAt) && (_gpu == null || _gpu.Available) && !(DeathTimer >= 0f))
			{
				float num = Mathf.Max(0.15f, view.Z - floorZ) / 1.6f / Mathf.Clamp(Settings.VrWorldSize, 0.25f, 4f);
				_vrScale = (_vrScaleSet ? Mathf.Lerp(_vrScale, num, 1f - Mathf.Exp(-2f * dt)) : num);
				_vrScaleSet = true;
				_vrAnchor = new Vector3(view.X, view.Y, view.Z);
				Vector3 vector = VrHeadAt();
				view.X = vector.x;
				view.Y = vector.y;
				view.Z = Mathf.Max(vector.z, floorZ + 0.1f);
				view.Pitch = Mathf.Tan(_pitchDegrees * (MathF.PI / 180f));
				view.Fov = 1.8325957f;
				view.Shear = 0f;
				_vrRoomStereo = (_vrStereoPlan = true);
			}
		}

		private bool VrRoomStereo(Interior room, ViewState view, List<VoxelObject> voxels, List<BaseSimObject> tagged, float daylight)
		{
			GpuMake();
			bool flag = _gpu.Available && VrEyeCameras(view.Yaw, 2f);
			Vector3 head = new Vector3(view.X, view.Y, view.Z);
			try
			{
				for (int i = 0; i < 2 && flag; i++)
				{
					VrEyeOn(i, head);
					flag = GpuRoomFrame(room, view, _vrEyeW[i], _vrEyeH[i], voxels, tagged, daylight);
					VrEyeOff();
					if (flag)
					{
						VrFlip(_gpu.Output, ref _vrEyes[i], ref _vrEyePtr[i]);
					}
				}
			}
			finally
			{
				VrEyeOff();
			}
			if (!flag)
			{
				_vrRoomStereo = (_vrStereoPlan = false);
				return false;
			}
			_vrStereoDrawn = true;
			VrStereoFrames++;
			VrRoomFrames++;
			_shown = HudProjection(view);
			_eyeNow = _shown.View;
			return true;
		}

		private void VrDoorwayEye(int i, ViewState camera, Vector3 head)
		{
			if (!_vrDoorReady || _gpu == null)
			{
				return;
			}
			FrameRenderer renderer = Renderer;
			_keptMeshes.Clear();
			_keptMeshes.AddRange(renderer.Meshes);
			_keptOverlay.Clear();
			_keptOverlay.AddRange(renderer.OverlayMeshes);
			_keptLights.Clear();
			_keptLights.AddRange(renderer.Lights);
			_keptWindows.Clear();
			_keptWindows.AddRange(renderer.Windows);
			Atmosphere air = renderer.Air;
			float maxDistance = renderer.MaxDistance;
			float billboardDistance = renderer.BillboardDistance;
			float fadeAt = renderer.FadeAt;
			float hazeScale = renderer.HazeScale;
			float rippleStrength = renderer.RippleStrength;
			bool flag = false;
			try
			{
				renderer.Meshes.Clear();
				renderer.Meshes.AddRange(_vrDoorMeshes);
				renderer.OverlayMeshes.Clear();
				renderer.Lights.Clear();
				renderer.Lights.AddRange(_vrDoorLights);
				renderer.Windows.Clear();
				renderer.Windows.AddRange(_vrDoorWindows);
				renderer.Air = _vrDoorAir;
				renderer.MaxDistance = 60f;
				renderer.BillboardDistance = 40f;
				renderer.FadeAt = 0f;
				renderer.HazeScale = 1f;
				renderer.RippleStrength = 0f;
				ViewState inRoom = camera;
				inRoom.X += _vrDoorShift.x;
				inRoom.Y += _vrDoorShift.y;
				inRoom.Z += _vrDoorShift.z;
				VrEyeOn(i, head + _vrDoorShift);
				EyeCamera eye = _vrEyeCam[i];
				eye.Position += _vrDoorShift;
				eye.Behind = 2f;
				_gpu.Eye = eye;
				_gpu.ShadowsKept = false;
				_gpu.Backdrop = null;
				flag = DoorwayRoomFrame(DoorRoom.Room, inRoom, _vrEyeW[i], _vrEyeH[i]);
			}
			catch (Exception ex)
			{
				Log.Error("vr doorway: " + ex.Message);
				flag = false;
			}
			finally
			{
				VrEyeOff();
				renderer.Meshes.Clear();
				renderer.Meshes.AddRange(_keptMeshes);
				renderer.OverlayMeshes.Clear();
				renderer.OverlayMeshes.AddRange(_keptOverlay);
				renderer.Lights.Clear();
				renderer.Lights.AddRange(_keptLights);
				renderer.Windows.Clear();
				renderer.Windows.AddRange(_keptWindows);
				renderer.Air = air;
				renderer.MaxDistance = maxDistance;
				renderer.BillboardDistance = billboardDistance;
				renderer.FadeAt = fadeAt;
				renderer.HazeScale = hazeScale;
				renderer.RippleStrength = rippleStrength;
			}
			_gpu.Backdrop = (flag ? _doorwayPicture : null);
		}

		private void VrSwings(Actor host)
		{
			bool flag = _vrHandsOn && _hands.TrackAimValid && !_vrRoomStereo;
			for (int i = 0; i < 2; i++)
			{
				SwingState obj = _swing[i];
				obj.Was = obj.Now;
				obj.Now = (flag ? ((i == 1) ? _hands.StrikeR : _hands.StrikeL) : default(HandStrike));
			}
			_strikeAnchorWas = _strikeAnchorNow;
			_strikeAnchorNow = _vrAnchor;
			_strikeYawWas = _strikeYawNow;
			_strikeYawNow = _vrYaw;
			_strikeAtWas = _strikeAtNow;
			_strikeAtNow = Time.unscaledTime;
			bool flag2 = flag && host != null && host.isAlive() && _active && !_vrWindowUp && !AnyMenu && _dive == Dive.None;
			float num = _strikeAtNow - _strikeAtWas;
			bool flag3 = num > 0.0001f && num < 0.1f && Mathf.Abs(Mathf.DeltaAngle(_strikeYawWas * 57.29578f, _strikeYawNow * 57.29578f)) < 0.5f;
			Vector3 vector = _strikeAnchorNow - _strikeAnchorWas;
			Vec3 vec = new Vec3(vector.x, vector.y, vector.z);
			for (int j = 0; j < 2; j++)
			{
				SwingState swingState = _swing[j];
				if (!flag2 || !swingState.Now.Valid)
				{
					End(swingState);
				}
				else
				{
					if (!flag3 || !swingState.Was.Valid || swingState.Was.Fist != swingState.Now.Fist)
					{
						continue;
					}
					Vec3 vec2 = swingState.Now.B - swingState.Was.B - vec;
					Vec3 vec3 = swingState.Now.A - swingState.Was.A - vec;
					float num2 = Mathf.Max(0.05f, _vrScale);
					float num3 = Mathf.Sqrt(Vec3.Dot(vec2, vec2)) / num / num2;
					if (Mathf.Sqrt(Vec3.Dot(vec3, vec3)) / num / num2 > 14f)
					{
						End(swingState);
						continue;
					}
					if (j == 1)
					{
						VrSwingSpeedForTest = num3;
					}
					if (!swingState.On)
					{
						if (num3 < (swingState.Now.Fist ? 1.4f : 1.6f))
						{
							continue;
						}
						swingState.On = true;
						swingState.Slow = 0f;
						swingState.Hit.Clear();
					}
					else
					{
						swingState.Slow = ((num3 < 0.9f) ? (swingState.Slow + num) : 0f);
						if (swingState.Slow > 0.12f)
						{
							End(swingState);
							continue;
						}
					}
					Strike(host, j, swingState, vec);
				}
			}
		}

		private static void End(SwingState s)
		{
			s.On = false;
			s.Slow = 0f;
			s.Hit.Clear();
		}

		private void Strike(Actor host, int h, SwingState s, Vec3 carried)
		{
			Vec3 a = s.Was.A + carried;
			Vec3 vec = s.Was.B + carried;
			Vec3 a2 = s.Now.A;
			Vec3 b = s.Now.B;
			float radius = s.Now.Radius;
			float num = Mathf.Sqrt(Vec3.Dot(b - a2, b - a2));
			float num2 = Mathf.Sqrt(Vec3.Dot(b - vec, b - vec));
			bool flag = ((h == 1 && !s.Now.Fist) ? (!host.hasMeleeAttack()) : (Holstered || host.getWeapon() != null || !host.hasMeleeAttack()));
			Hitboxes.Near((a2 + b) * 0.5f, b, num + num2 + 0.6f, host, _swingNear);
			foreach (Actor item in _swingNear)
			{
				if (item != null && item != host && !s.Hit.Contains(item) && item.isAlive() && !FriendlyFire.Holds(host, item) && Hitboxes.Get(item, out var box) && Swept(in box, a, vec, a2, b, radius))
				{
					HeadBox head;
					bool flag2 = Hitboxes.GetHead(item, out head) && SweptHead(in head, a, vec, a2, b, radius);
					s.Hit.Add(item);
					Doorstep.StepOut(item);
					if (flag ? PossessionCombat.Punch(host, item, flag2) : PossessionCombat.SwingHit(host, item, flag2))
					{
						VrSwingHitsForTest++;
						_vr.Rumble(h, flag2 ? 1f : 0.8f, 0.07f);
					}
				}
			}
			_swingSeen.Clear();
			int num3 = Mathf.FloorToInt(b.X);
			int num4 = Mathf.FloorToInt(b.Y);
			int num5 = Mathf.CeilToInt(num + num2) + 1;
			for (int i = num3 - num5; i <= num3 + num5; i++)
			{
				for (int j = num4 - num5; j <= num4 + num5; j++)
				{
					Building building;
					try
					{
						building = World.world.GetTile(i, j)?.building;
					}
					catch
					{
						building = null;
					}
					if (building == null || s.Hit.Contains(building) || !_swingSeen.Add(building) || !building.isAlive() || building.asset == null || !Work.Workable(building, out var _, out var _, out var _))
					{
						continue;
					}
					Vector2 vector = Work.PlaceOf(building);
					float num6 = GroundAt(vector);
					bool flag3 = building.asset.building_type == BuildingType.Building_Tree;
					if (Through(vector, 0.32f, num6, num6 + (flag3 ? 2.2f : 0.9f), a, vec, a2, b, radius, out var struck))
					{
						s.Hit.Add(building);
						if (Work.SwingInto(host, building, host.current_position, new Vector3(struck.X, struck.Y, struck.Z), num6, _scene, Feedback, Toast, Holstered))
						{
							VrSwingWorkForTest++;
							_vr.Rumble(h, 0.5f, 0.05f);
						}
					}
				}
			}
			if (!Butchery.CanCut(host))
			{
				return;
			}
			foreach (Butchery.Carcass item2 in Butchery.All)
			{
				if (item2.Left <= 0 || s.Hit.Contains(item2))
				{
					continue;
				}
				float num7 = GroundAt(item2.At);
				if (Through(item2.At, 0.4f, num7 - 0.2f, num7 + 0.6f, a, vec, a2, b, radius, out var _))
				{
					s.Hit.Add(item2);
					if (Butchery.Cut(host, item2, Toast) > 0)
					{
						VrSwingCutsForTest++;
						_vr.Rumble(h, 0.5f, 0.05f);
					}
				}
			}
		}

		private static bool Swept(in Hitbox box, Vec3 a0, Vec3 b0, Vec3 a1, Vec3 b1, float pad)
		{
			float t2;
			for (int i = 0; i <= 4; i++)
			{
				float t = (float)i / 4f;
				if (box.Segment(Lerp(a0, a1, t), Lerp(b0, b1, t), pad, out t2))
				{
					return true;
				}
			}
			if (!box.Segment(b0, b1, pad, out t2))
			{
				return box.Segment(Lerp(a0, b0, 0.5f), Lerp(a1, b1, 0.5f), pad, out t2);
			}
			return true;
		}

		private static bool SweptHead(in HeadBox head, Vec3 a0, Vec3 b0, Vec3 a1, Vec3 b1, float pad)
		{
			float t2;
			for (int i = 0; i <= 4; i++)
			{
				float t = (float)i / 4f;
				if (head.Segment(Lerp(a0, a1, t), Lerp(b0, b1, t), pad, out t2))
				{
					return true;
				}
			}
			return head.Segment(b0, b1, pad, out t2);
		}

		private static bool Through(Vector2 at, float radius, float lo, float hi, Vec3 a0, Vec3 b0, Vec3 a1, Vec3 b1, float pad, out Vec3 struck)
		{
			float num = (radius + pad) * (radius + pad);
			for (int i = 0; i <= 4; i++)
			{
				float t = (float)i / 4f;
				Vec3 b2 = Lerp(a0, a1, t);
				Vec3 a2 = Lerp(b0, b1, t);
				for (int j = 0; j <= 4; j++)
				{
					Vec3 vec = Lerp(a2, b2, (float)j / 4f);
					float num2 = vec.X - at.x;
					float num3 = vec.Y - at.y;
					if (num2 * num2 + num3 * num3 <= num && vec.Z >= lo && vec.Z <= hi)
					{
						struck = vec;
						return true;
					}
				}
			}
			struck = default(Vec3);
			return false;
		}

		private static Vec3 Lerp(Vec3 a, Vec3 b, float t)
		{
			return a + (b - a) * t;
		}

		private void VrViewReach()
		{
			if (_vr != null && _vr.ViewsValid)
			{
				Xr.Fov fov = _vr.Views[0].Fov;
				Xr.Fov fov2 = _vr.Views[1].Fov;
				float a = Mathf.Max(Mathf.Tan(0f - fov.AngleLeft), Mathf.Tan(0f - fov2.AngleLeft));
				float b = Mathf.Max(Mathf.Tan(fov.AngleRight), Mathf.Tan(fov2.AngleRight));
				float a2 = Mathf.Min(Mathf.Tan(fov.AngleUp), Mathf.Tan(fov2.AngleUp));
				float b2 = Mathf.Min(Mathf.Tan(0f - fov.AngleDown), Mathf.Tan(0f - fov2.AngleDown));
				float num = Mathf.Min(a, b);
				float num2 = Mathf.Min(a2, b2);
				if (num > 0.2f && num < 6f)
				{
					_vrViewAcross = num;
				}
				if (num2 > 0.2f && num2 < 6f)
				{
					_vrViewUp = num2;
				}
			}
		}

		private float VrHudWide(float metres)
		{
			return 2f * metres * Mathf.Tan(VrHudDegrees * 0.5f * (MathF.PI / 180f));
		}

		private void VrWristLook()
		{
			bool flag = false;
			int num = ((_vr != null) ? _vr.Off : 0);
			if (VrRunning && _vr.Focused && _vr.HeadValid && _vr.HandValid[num] && _active && !_vrWindowUp && DeathTimer < 0f && _dive == Dive.None)
			{
				Xr.Pose pose = _vr.Hands[num];
				Xr.Pose head = _vr.Head;
				Vector3 vector = V(head.Position) - V(pose.Position);
				float magnitude = vector.magnitude;
				if (magnitude > 0.05f)
				{
					Vector3 lhs = V(Rotate(pose.Orientation, WristSide, 0f, 0f));
					Vector3 vector2 = V(Rotate(head.Orientation, 0f, 0f, -1f));
					float num2 = Vector3.Dot(lhs, vector / magnitude);
					float num3 = Vector3.Angle(vector2, -vector);
					flag = magnitude < 0.8f && ((!_vrWristUp) ? (num2 > 0.6f && num3 < 32f) : (num2 > 0.35f && num3 < 45f));
					VrWristWhyForTest = $"facing {num2:F2}, {num3:F0} deg off the gaze, {magnitude:F2} m";
				}
			}
			else
			{
				VrWristWhyForTest = $"running {VrRunning}, hand {_vr != null && _vr.HandValid[num]}, first person {_active}, window {_vrWindowUp}, dive {_dive}";
			}
			float unscaledTime = Time.unscaledTime;
			if (!flag)
			{
				_vrWristSince = -1f;
				_vrWristUp = false;
				_vrWristShown = false;
			}
			else if (!_vrWristUp)
			{
				if (_vrWristSince < 0f)
				{
					_vrWristSince = unscaledTime;
				}
				if (unscaledTime - _vrWristSince >= 0.2f)
				{
					_vrWristUp = true;
				}
			}
		}

		private void VrWristPass()
		{
			_vrWristDrawn = false;
			if (!_vrWristUp)
			{
				return;
			}
			int width = Screen.width;
			int height = Screen.height;
			_vrWristW = Mathf.Min(1100, width, Mathf.RoundToInt((float)height * 0.8f));
			if (_vrWristW < 64)
			{
				return;
			}
			VrTarget(ref _vrWrist, width, height, point: false);
			RenderTexture.active = _vrWrist;
			GL.Clear(clearDepth: true, clearColor: true, new Color(0f, 0f, 0f, 0f));
			float num = Mathf.Max(0.01f, GameArt.GameP * GameArt.UserScale);
			GameArt.VrBoost = (float)_vrWristW / 239f / num;
			try
			{
				float f = _hud.DrawVrWrist(_vrWristW, height);
				_vrWristRows = Mathf.Clamp(Mathf.CeilToInt(f), 0, height);
				_vrWristDrawn = _vrWristRows > 16;
			}
			catch (Exception ex)
			{
				Log.Error("vr: the wrist's panel: " + ex);
			}
			finally
			{
				GameArt.VrBoost = 1f;
			}
		}

		private void VrWristPoint()
		{
			int num = -1;
			bool wristTaken = false;
			if (_vrWristUp && _vrWristShown && VrRunning && _vr.Focused)
			{
				int main = _vr.Main;
				if (_vr.HandValid[main])
				{
					Xr.Pose pose = _vr.Hands[main];
					if (PanelHit(new VrPanelPlace
					{
						Up = true,
						Solid = true,
						Pose = _vrWristPose,
						Wide = _vrWristWide,
						High = _vrWristHigh
					}, pose.Position, Rotate(pose.Orientation, 0f, 0f, -1f), out var u, out var v, out var _, out var _))
					{
						wristTaken = true;
						Vector2 point = new Vector2(u * (float)_vrWristW, v * (float)_vrWristRows);
						for (int i = 0; i < _hud.VrWristSlotCount; i++)
						{
							if (_hud.VrWristSlots[i].Contains(point))
							{
								num = i;
								break;
							}
						}
					}
				}
			}
			if (num != _hud.VrWristHover && num >= 0)
			{
				_vr.Rumble(_vr.Main, 0.1f, 0.012f);
			}
			_hud.VrWristHover = num;
			VrControls.WristTaken = wristTaken;
			bool flag = VrRunning && _vr.TriggerOf(_vr.Main) > 0.55f;
			if (flag && !_vrWristTrigWas && num >= 0 && _host != null)
			{
				ChooseSlot(_host, num, wheel: false);
				_vr.Rumble(_vr.Main, 0.35f, 0.03f);
			}
			_vrWristTrigWas = flag;
		}

		private void VrWristLayer(VrSystem.Layers layers)
		{
			int off = _vr.Off;
			if (!_vrWristDrawn || !_vrWristUp || _vrWrist == null || !_vr.HandValid[off])
			{
				_vrWristShown = false;
				return;
			}
			VrFlip(_vrWrist, ref _vrWristPanel, ref _vrWristPanelPtr);
			VrSystem.Quad quad = layers.AddQuad();
			if (quad != null)
			{
				float num = 0.33f;
				float num2 = 0.33f * (float)_vrWristRows / Mathf.Max(1f, _vrWristW);
				Xr.Pose pose = _vr.Hands[off];
				Xr.Pose head = _vr.Head;
				Vector3 vector = V(Rotate(head.Orientation, 0f, 1f, 0f));
				Vector3 vector2 = V(pose.Position) + V(Rotate(pose.Orientation, WristSide, 0f, 0f)) * 0.04f + vector * (num2 * 0.5f + 0.02f);
				Vector3 vector3 = V(head.Position) - vector2;
				if (!(vector3.sqrMagnitude < 0.0001f))
				{
					Quaternion quaternion = Quaternion.LookRotation(vector3.normalized, vector);
					quad.Picture = _vrWristPanelPtr;
					quad.Width = _vrWristW;
					quad.Height = _vrWristRows;
					quad.MetresWide = num;
					quad.MetresHigh = num2;
					quad.OnHead = false;
					quad.SeeThrough = true;
					quad.Pose = new Xr.Pose
					{
						Orientation = new Xr.Quat
						{
							X = quaternion.x,
							Y = quaternion.y,
							Z = quaternion.z,
							W = quaternion.w
						},
						Position = new Xr.Vec3
						{
							X = vector2.x,
							Y = vector2.y,
							Z = vector2.z
						}
					};
					_vrWristPose = quad.Pose;
					_vrWristWide = num;
					_vrWristHigh = num2;
					_vrWristShown = true;
				}
			}
		}

		private static Vector3 V(Xr.Vec3 v)
		{
			return new Vector3(v.X, v.Y, v.Z);
		}

		private void WeaponGlowFx()
		{
			WeaponGlowsForTest = (WeaponGlowOthersForTest = 0);
			float num = Mathf.Clamp01((Renderer.Air.Night - 0.15f) / 0.45f);
			float unscaledTime = Time.unscaledTime;
			bool flag = false;
			List<MeshInstance> overlayMeshes = Renderer.OverlayMeshes;
			for (int i = 0; i < overlayMeshes.Count; i++)
			{
				MeshInstance meshInstance = overlayMeshes[i];
				if (meshInstance != null && meshInstance.Model != null && ViewMeshes.HeldGlow(meshInstance.Model, out var colour, out var head, out var lava))
				{
					Vec3 vec = meshInstance.Position + meshInstance.Basis.Mul(head);
					flag = true;
					if (meshInstance.Model.Name.StartsWith("held_bow_", StringComparison.Ordinal))
					{
						_weaponAura.Reset();
					}
					else
					{
						_weaponAura.Emit(SceneCollector.FxPool, meshInstance.Model.Name, vec, new Vec3(_view.X, _view.Y, _view.Z), colour, lava, GameClock.EffectsPaused ? 0f : Time.unscaledDeltaTime);
					}
					if (num > 0f)
					{
						AddWeaponLight(vec, colour, lava, unscaledTime, 0.45f * num, 3f, 0);
						WeaponGlowsForTest++;
					}
					break;
				}
			}
			if (!flag)
			{
				_weaponAura.Reset();
			}
			if (num <= 0f)
			{
				return;
			}
			_glowNear.Clear();
			_glowNearD2.Clear();
			List<People.HeldGlow> glowsDrawn = People.GlowsDrawn;
			for (int j = 0; j < glowsDrawn.Count; j++)
			{
				People.HeldGlow item = glowsDrawn[j];
				if (unscaledTime - item.Time > 0.1f)
				{
					continue;
				}
				float num2 = item.At.X - _view.X;
				float num3 = item.At.Y - _view.Y;
				float num4 = num2 * num2 + num3 * num3;
				if (num4 > 256f)
				{
					continue;
				}
				int num5 = _glowNearD2.Count;
				while (num5 > 0 && _glowNearD2[num5 - 1] > num4)
				{
					num5--;
				}
				if (num5 < 4)
				{
					_glowNear.Insert(num5, item);
					_glowNearD2.Insert(num5, num4);
					if (_glowNear.Count > 4)
					{
						_glowNear.RemoveAt(4);
						_glowNearD2.RemoveAt(4);
					}
				}
			}
			for (int k = 0; k < _glowNear.Count; k++)
			{
				People.HeldGlow heldGlow = _glowNear[k];
				AddWeaponLight(heldGlow.At, heldGlow.Colour, heldGlow.Lava, unscaledTime, 0.35f * num, 2.4f, k + 1);
				WeaponGlowOthersForTest++;
			}
		}

		private void AddWeaponLight(Vec3 at, uint colour, bool lava, float now, float strength, float radius, int seed)
		{
			float num = (lava ? (0.84f + 0.1f * Mathf.Sin(now * 7.7f + (float)seed * 1.9f) + 0.06f * Mathf.Sin(now * 13.3f + (float)seed * 0.7f)) : (0.93f + 0.07f * Mathf.Sin(now * 2.2f + (float)seed * 1.3f)));
			float num2 = (float)(colour & 0xFF) / 255f;
			float num3 = (float)((colour >> 8) & 0xFF) / 255f;
			float num4 = (float)((colour >> 16) & 0xFF) / 255f;
			float num5 = strength * num / Mathf.Max(0.05f, Mathf.Max(num2, Mathf.Max(num3, num4)));
			Renderer.Lights.Add(new PointLight
			{
				X = at.X,
				Y = at.Y,
				Z = at.Z,
				Radius = radius,
				R = num2 * num5,
				G = num3 * num5,
				B = num4 * num5
			});
		}

		static WorldBoxMod()
		{
			string environmentVariable = Environment.GetEnvironmentVariable("FIRSTPERSON_VR");
			object vrByEnvironment;
			switch (environmentVariable)
			{
			default:
				vrByEnvironment = null;
				break;
			case "1":
			case "sim":
				vrByEnvironment = environmentVariable;
				break;
			}
			VrByEnvironment = (string)vrByEnvironment;
			BeamBright = TerrainCache.Rgb(150, 230, 255);
			BeamDim = TerrainCache.Rgb(70, 105, 120);
			AimDotColor = TerrainCache.Rgb(255, 236, 200);
			EventFocus = typeof(EventSystem).GetField("m_HasFocus", BindingFlags.Instance | BindingFlags.NonPublic);
		}
	}
}
